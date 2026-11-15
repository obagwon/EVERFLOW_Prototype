using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap.Editor
{
    public static class EFLeakPrototypeTools
    {
        const string Folder="Assets/EverflowWater/Prototype";
        const string ScenePath="Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity";
        const string SplashName="S3 Water Prototype - Impact Spray";
        static EFV2WaterIntegration Water()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=ScenePath)
                throw new InvalidOperationException("Open the puzzle scene outside Play mode.");
            return UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();
        }
        [MenuItem("Tools/EVERFLOW/Water/Prototype/Apply S3 Unified Water")]
        public static void Apply()
        {
            var water=Water();
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory("docs/water-validation/prototype");
            if(!File.Exists("docs/water-validation/prototype/before.unity.snapshot"))
                File.Copy(ScenePath,"docs/water-validation/prototype/before.unity.snapshot");
            AssetDatabase.Refresh();
            string[] labels={"W_Leak_S3","W_Leak_Fall2","W_Leak_S4","W_Leak_Fall2_Foam"};
            var segments=labels.Select(n=>water.segments.Single(s=>s.label==n)).ToArray();
            string backupPath=Folder+"/S3_Before.asset";
            var backup=AssetDatabase.LoadAssetAtPath<EFLeakPrototypeBackup>(backupPath);
            if(backup==null)
            {
                backup=ScriptableObject.CreateInstance<EFLeakPrototypeBackup>();backup.labels=labels;
                backup.meshes=segments.Select(s=>s.preparedMesh).ToArray();
                backup.overrides=segments.Select(s=>s.materialOverride).ToArray();backup.suppressed=segments.Select(s=>s.suppressPresentation).ToArray();
                AssetDatabase.CreateAsset(backup,backupPath);
            }
            var material=Material(Folder+"/S3_Unified.mat","EVERFLOW/Water/Leak Prototype");
            material.SetTexture("_NormalMap",water.waterMaterial.GetTexture("_NormalMap"));
            var mesh=BuildMesh(segments[0].filter.transform);
            Undo.RecordObject(water,"Apply S3 unified water prototype");
            for(int i=0;i<segments.Length;i++)
            {
                var s=segments[i];Undo.RecordObject(s.renderer,"Set water prototype visibility");
                s.suppressPresentation=i>0;s.renderer.enabled=i==0;
                if(i==0)
                {
                    Undo.RecordObject(s.filter,"Set continuous water mesh");
                    s.preparedMesh=mesh;s.filter.sharedMesh=mesh;s.materialOverride=material;s.renderer.sharedMaterial=material;
                    EditorUtility.SetDirty(s.filter);
                }
                EditorUtility.SetDirty(s.renderer);
            }
            var existing=segments[0].filter.transform.Find(SplashName);
            if(existing==null)
            {
                var go=new GameObject(SplashName);Undo.RegisterCreatedObjectUndo(go,"Add impact spray");
                go.transform.SetParent(segments[0].filter.transform,false);go.transform.position=new Vector3(-35,.27f,-29.62f);
                go.transform.rotation=Quaternion.Euler(-90,0,0);
                var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=true;main.playOnAwake=false;main.maxParticles=64;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(.65f,1.5f);
                main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);main.gravityModifier=.8f;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startColor=new Color(.72f,.89f,.86f,.5f);ps.useAutoRandomSeed=false;ps.randomSeed=3821;
                var em=ps.emission;em.rateOverTime=24;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=45;shape.radius=.4f;
                var fade=ps.colorOverLifetime;fade.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                    new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.12f),new GradientAlphaKey(0,1)});fade.color=gradient;
                var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=Material(Folder+"/ImpactSpray.mat","EVERFLOW/Water/Leak Spray");
                r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
                var ctrl=go.AddComponent<EFLeakPrototypeSplash>();ctrl.water=water;ctrl.spray=ps;
            }
            var splash=segments[0].filter.transform.Find(SplashName);
            splash.position=new Vector3(-35,.27f,-29.62f);
            EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(water.gameObject.scene);EditorSceneManager.SaveScene(water.gameObject.scene);
        }
        static Material Material(string path,string shader)
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(mat,path);}
            EditorUtility.SetDirty(mat);return mat;
        }
        static Mesh BuildMesh(Transform space)
        {
            var points=new List<Vector3>();var speeds=new List<float>();var falls=new List<float>();
            // Entire S3 surface, beginning at the existing upstream boundary.
            for(int i=0;i<=64;i++)
            {
                float f=i/64f;points.Add(new Vector3(-35,3.225f,Mathf.Lerp(-44,-31.5f,f)));
                speeds.Add(Mathf.Lerp(.55f,2.4f,Mathf.Pow(f,6)));falls.Add(0);
            }
            // Ballistic trajectory: leaves the actual ledge horizontally, then accelerates down.
            float duration=Mathf.Sqrt(3f/4.905f);
            for(int i=1;i<=64;i++)
            {
                float t=duration*i/64f;
                points.Add(new Vector3(-35,3.225f-4.905f*t*t,-31.5f+2.4f*t));
                speeds.Add(Mathf.Sqrt(2.4f*2.4f+9.81f*t*9.81f*t));falls.Add(Mathf.Clamp01(t/.22f));
            }
            var hit=points[points.Count-1];
            // Lower water begins at the impact itself, sharing vertices with the falling sheet.
            for(int i=1;i<=32;i++)
            {
                float f=i/32f;points.Add(new Vector3(-35,.225f,Mathf.Lerp(hit.z,-26,f)));
                speeds.Add(Mathf.Lerp(2.5f,.55f,Mathf.Min(1,f*4)));falls.Add(0);
            }
            var verts=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();
            var normals=new List<Vector3>();var tangents=new List<Vector4>();var triangles=new List<int>();
            const int across=24;
            float travel=0;
            for(int i=0;i<points.Count;i++)
            {
                if(i>0)travel+=Vector3.Distance(points[i],points[i-1])/(.5f*(speeds[i]+speeds[i-1]))*.72f;
                var dir=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;
                var normal=Vector3.Cross(dir,Vector3.right).normalized;
                float fall=falls[i],width=2*(1-fall*.07f);
                float impact=Mathf.Exp(-Mathf.Pow((points[i].z-hit.z)/.65f,2))*Mathf.Clamp01(1-(points[i].y-.225f)/.65f);
                for(int j=0;j<=across;j++)
                {
                    float u=j/(float)across;
                    var pos=points[i]+Vector3.right*((u-.5f)*width);
                    verts.Add(space.InverseTransformPoint(pos));uv.Add(new Vector2(u*2-36,travel-44));
                    float alpha=Mathf.SmoothStep(0,1,Mathf.Min(u,1-u)/.018f);
                    colors.Add(new Color(fall,impact,Mathf.Clamp01((points[i].z+44)*2),alpha));
                    normals.Add(space.InverseTransformDirection(normal));tangents.Add(new Vector4(1,0,0,1));
                    if(i==0||j==0)continue;
                    int a=(i-1)*(across+1)+j-1,b=a+1,c=i*(across+1)+j-1,d=c+1;
                    triangles.AddRange(new[]{a,c,b,b,c,d});
                }
            }
            string path=Folder+"/S3_Fall2_S4_Unified.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.name="S3_Fall2_S4_Unified";
            mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetTriangles(triangles,0);
            mesh.RecalculateBounds();mesh.bounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*.12f);
            EditorUtility.SetDirty(mesh);return mesh;
        }
        [MenuItem("Tools/EVERFLOW/Water/Prototype/Restore S3 Before Prototype")]
        public static void Restore()
        {
            var water=Water();var b=AssetDatabase.LoadAssetAtPath<EFLeakPrototypeBackup>(Folder+"/S3_Before.asset");
            if(b==null)throw new InvalidOperationException("No saved prototype baseline.");
            Undo.RecordObject(water,"Restore S3 before prototype");
            for(int i=0;i<b.labels.Length;i++)
            {
                var s=water.segments.Single(x=>x.label==b.labels[i]);
                Undo.RecordObject(s.filter,"Restore mesh");Undo.RecordObject(s.renderer,"Restore material");
                s.preparedMesh=b.meshes[i];s.filter.sharedMesh=b.meshes[i];s.materialOverride=b.overrides[i];
                s.suppressPresentation=b.suppressed[i];s.renderer.enabled=!s.suppressPresentation;
                s.renderer.sharedMaterial=s.materialOverride!=null?s.materialOverride:water.waterMaterial;
                EditorUtility.SetDirty(s.filter);EditorUtility.SetDirty(s.renderer);
            }
            var splash=water.segments.Single(s=>s.label=="W_Leak_S3").filter.transform.Find(SplashName);
            if(splash!=null)Undo.DestroyObjectImmediate(splash.gameObject);
            EditorUtility.SetDirty(water);EditorSceneManager.MarkSceneDirty(water.gameObject.scene);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(water.gameObject.scene);
        }
        // Review only: changes are temporary in Play mode and revert on Stop.
        public static void View(string view)
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Review in Play mode.");
            var w=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();w.AutomatedCheck=true;
            foreach(var g in UnityEngine.Object.FindObjectsByType<EFV2Guidance>(FindObjectsSortMode.None))g.enabled=false;
            var c=Camera.main;c.GetComponent<Unity.Cinemachine.CinemachineBrain>().enabled=false;c.fieldOfView=48;
            Vector3 eye=view=="side"?new Vector3(-39,4,-28):view=="top"?new Vector3(-37,7,-29):new Vector3(-29,5,-23);
            Vector3 target=view=="side"?new Vector3(-35,1.9f,-31):view=="top"?new Vector3(-35,2,-31):new Vector3(-35,1.8f,-30.5f);
            c.transform.position=eye;c.transform.LookAt(target);
        }
    }
}
