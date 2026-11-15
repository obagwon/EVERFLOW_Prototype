using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap.Editor
{
    public static class EFWaterNetworkTools
    {
        const string Folder="Assets/EverflowWater/Network",Root="Continuous Water Impacts";
        public const string Reports="docs/water-validation/network";
        static EFV2WaterIntegration water;
        static Material material,sprayMaterial;
        static HashSet<string> assigned;
        static Vector3 P(float x,float y,float z)=>new Vector3(x,y,z);
        static EFV2WaterSegment S(string label)=>water.segments.Single(s=>s.label==label);
        static void RequireScene()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)
                throw new InvalidOperationException("Open the puzzle scene outside Play mode first.");
            water=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();
        }
        [MenuItem("Tools/EVERFLOW/Water/Network/Apply All Continuous Waterways")]
        public static void Apply()
        {
            RequireScene();Directory.CreateDirectory(Folder);Directory.CreateDirectory(Reports);
            if(!File.Exists(Reports+"/before.unity.snapshot"))File.Copy(EFV2PuzzleBuild.Scene,Reports+"/before.unity.snapshot");
            AssetDatabase.Refresh();
            var backup=AssetDatabase.LoadAssetAtPath<EFWaterNetworkBackup>(Folder+"/BeforeNetwork.asset");
            if(backup==null)
            {
                backup=ScriptableObject.CreateInstance<EFWaterNetworkBackup>();
                backup.entries=water.segments.Select(s=>new EFWaterNetworkBackup.Entry{label=s.label,mesh=s.preparedMesh,material=s.materialOverride,
                    suppressed=s.suppressPresentation,network=s.networkSurface,worldUV=s.networkWorldUV,impacts=s.impactPoints,sources=s.impactSources}).ToArray();
                var prior=UnityEngine.Object.FindFirstObjectByType<EFLeakPrototypeSplash>(FindObjectsInactive.Include);
                backup.prototypeSprayActive=prior!=null&&prior.gameObject.activeSelf;
                AssetDatabase.CreateAsset(backup,Folder+"/BeforeNetwork.asset");
            }
            material=GetMaterial(Folder+"/ContinuousWater.mat","EVERFLOW/Water/Continuous Network");
            material.SetTexture("_NormalMap",water.waterMaterial.GetTexture("_NormalMap"));
            sprayMaterial=GetMaterial(Folder+"/ImpactSpray.mat","EVERFLOW/Water/Leak Spray");
            Undo.RecordObject(water,"Continuous map water network");
            foreach(var s in water.segments)
            {
                Undo.RecordObject(s.filter,"Update water surface");Undo.RecordObject(s.renderer,"Update water material");
                s.networkSurface=true;s.networkWorldUV=false;s.suppressPresentation=true;s.materialOverride=material;
                s.impactPoints=Array.Empty<Vector3>();s.impactSources=Array.Empty<string>();
                s.renderer.sharedMaterial=material;s.renderer.enabled=false;
            }
            foreach(var old in UnityEngine.Object.FindObjectsByType<EFLeakPrototypeSplash>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {Undo.RecordObject(old.gameObject,"Disable superseded prototype spray");old.gameObject.SetActive(false);}
            var existing=water.transform.Find(Root);if(existing!=null)Undo.DestroyObjectImmediate(existing.gameObject);
            var impacts=new GameObject(Root);Undo.RegisterCreatedObjectUndo(impacts,"Create water impacts");impacts.transform.SetParent(water.transform,false);
            assigned=new HashSet<string>();

            // Always-wet highland → ruin → ground leak → pond. One connected surface.
            var main=HighlandRoute();
            Bind("W_H_to_G_Fall",main,new[]{"W_H_to_G_Fall_Foam","W_G_Supply","W_G_Leak_Fall","W_G_Leak_Fall_Foam","W_Leak_S1","W_Leak_S2","W_Leak_S3","W_Leak_Fall2","W_Leak_Fall2_Foam","W_Leak_S4"});

            Pool("W_H_Pond",P(-51,20.55f,-79),P(-39,20.55f,-75));
            Pool("W_A_Pond",P(-56,0,-26),P(-34,0,-16),new[]{P(-35,0,-25.9f)},new[]{"W_H_to_G_Fall"});
            Pool("W_D2_Basin",P(-65,-11.85f,-59),P(-47,-11.85f,-47),
                new[]{P(-56,-11.85f,-58.7f),P(-65,-11.85f,-58)},new[]{"W_D5_Fall","W_Bypass_1"});

            // The cave spring is already below the highland: retain its actual ceiling outlet.
            var spring=new Ribbon();spring.Start(P(-46,1.5f,-71.25f),2);
            spring.Fall(P(-46,-5.85f,-69.8f),2);
            spring.Turn(P(-46,-5.85f,-69),P(-47,-5.85f,-69),2);
            spring.Line(P(-56,-5.85f,-69),2,.6f);
            Bind("W_H_to_D5_Fall",spring,new[]{"W_H_to_D5_Fall_Foam"});

            // Keep the freezeable fixed channel separate from the downstream supply group.
            var fixedChannel=new Ribbon();fixedChannel.Start(P(-56,-5.85f,-69),2);
            fixedChannel.Line(P(-56,-5.85f,-64),2,.65f);Bind("W_D5_Fixed",fixedChannel,Array.Empty<string>());
            var supply=new Ribbon();supply.Start(P(-56,-5.85f,-64),2);
            supply.Fall(P(-56,-11.85f,-62.9f),2);supply.Line(P(-56,-11.85f,-59),2,.65f);
            Bind("W_D5_Fall",supply,new[]{"W_D5_ToD2","W_D5_Fall_Foam"});

            var spill=new Ribbon();spill.Start(P(-55,-5.88f,-65),2);
            spill.Line(P(-50,-5.88f,-65),2,.7f);spill.Turn(P(-49,-5.88f,-65),P(-49,-5.88f,-64.15f),2);
            spill.Fall(P(-49,-11.85f,-62.9f),2);spill.Line(P(-49,-11.85f,-59),2,.65f);
            Bind("W_D5_SpillPool",spill,new[]{"W_D5_SpillFall","W_D5_SpillFloor","W_D5_SpillFall_Foam"});

            var bypass=new Ribbon();bypass.Start(P(-70,-6.6f,-67.5f),3);
            bypass.Line(P(-74.4f,-6.6f,-67.5f),3,.7f);
            bypass.Turn(P(-75.5f,-6.6f,-67.5f),P(-75.5f,-6.6f,-66.8f),3);
            bypass.Fall(P(-75.5f,-8.85f,-65.5f),3);
            bypass.Line(P(-75.5f,-8.85f,-60),3,.7f);bypass.Fall(P(-75.5f,-11.85f,-58.3f),3);
            bypass.Turn(P(-75.5f,-11.85f,-58),P(-74,-11.85f,-58),3);
            bypass.Line(P(-65,-11.85f,-58),3,.65f);
            Bind("W_Bypass_1",bypass,new[]{"W_Bypass_2","W_Bypass_Fall","W_Bypass_Fall_Foam"});

            var dock=new Ribbon();dock.Start(P(-47,-11.85f,-52),2);
            dock.Line(P(-45.5f,-11.85f,-52),2,.65f);
            dock.Turn(P(-44.6f,-11.85f,-52),P(-44.6f,-11.85f,-50.8f),2);
            dock.Line(P(-44.6f,-11.85f,-47.2f),2,.65f);
            dock.Turn(P(-44.6f,-11.85f,-46),P(-43.6f,-11.85f,-46),2);
            dock.Line(P(-42.5f,-11.85f,-46),2,.65f);Bind("W_D2_Dock",dock,new[]{"W_D2_Low"});
            var wheel=new Ribbon();wheel.Start(P(-65,-11.85f,-52.75f),2.5f);wheel.Line(P(-72.5f,-11.85f,-52.75f),2.5f,.7f);
            Bind("W_D2_Wheel",wheel,Array.Empty<string>());
            var exit=new Ribbon();exit.Start(P(-65,-11.85f,-48.5f),3);exit.Line(P(-78,-11.85f,-48.5f),3,.7f);
            Bind("W_D2_Exit",exit,Array.Empty<string>());
            var gWheel=RuinWheelRoute();
            Bind("W_G_ToWheel",gWheel,Array.Empty<string>());

            var gorge=new Ribbon();gorge.Start(P(-78,-11.85f,-48.5f),3);
            gorge.Line(P(-83,-11.85f,-48.5f),3,.7f);
            gorge.Turn(P(-86,-11.85f,-48.5f),P(-86,-12.73f,-45),3);
            // Original gorge drops to -17.90. The old wheel-feed top at -12.30
            // would require water to rise ~5.6m; feed the existing wheel pool at its real inlet.
            gorge.Line(P(-86,-17.1f,-18.5f),3,1.1f);
            gorge.Turn(P(-86,-17.775f,-14.8f),P(-86,-17.775f,-14),3);
            Bind("W_Exit_Tunnel",gorge,new[]{"W_Gorge","W_R_WheelFeed"});

            var garden=new Ribbon();garden.Start(P(-85,-17.775f,-14),10);
            garden.Line(P(-85,-17.775f,.4f),10,.6f);
            garden.Line(P(-86.5f,-17.775f,2.5f),5,.8f);
            garden.Fall(P(-86.5f,-28.4f,4.1f),5);
            garden.Line(P(-86.5f,-28.4f,5.6f),5,.65f,true);
            Bind("W_R_WheelPool",garden,new[]{"W_R_IslandFall","W_R_IslandFall_Foam"});

            if(assigned.Count!=water.segments.Length)throw new InvalidOperationException("Unassigned water: "+string.Join(",",water.segments.Where(s=>!assigned.Contains(s.label)).Select(s=>s.label)));
            foreach(var s in water.segments){EditorUtility.SetDirty(s.filter);EditorUtility.SetDirty(s.renderer);}
            EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(water.gameObject.scene);EditorSceneManager.SaveScene(water.gameObject.scene);
            File.WriteAllText(Reports+"/geometry.txt",Validate());
        }
        static Ribbon HighlandRoute()
        {
            var main=new Ribbon {volumeDepth=.16f};main.Start(P(-45,20.55f,-75),2);
            main.Line(P(-45,20.55f,-73.5f),2,1.2f);
            // Fill the existing G trough, leaving 14cm freeboard below its 15.63m rim.
            main.Fall(P(-45,15.49f,-72.4f),2);
            main.Line(P(-45,15.49f,-62),2,.65f);
            // Measured cliff cross-section at Y=15: Z=-60.38..-60.17.
            // Leave the actual lip immediately, rather than extending a flat sheet to -57.65.
            main.Line(P(-45,15.49f,-60.22f),2,2.25f);
            // Natural leak creek: lower than the surrounding 3.01m ground, broad bends
            // around the skylight, and a small continuous downstream grade.
            main.Fall(P(-45,2.90f,-56.7f),2);
            main.Line(P(-45,2.90f,-54),1.95f,.65f);
            main.Turn(P(-45.8f,2.895f,-51.5f),P(-45.8f,2.89f,-48.2f),2.15f);
            main.Turn(P(-45.8f,2.88f,-43.8f),P(-41.5f,2.875f,-43.8f),2.25f);
            main.Line(P(-39.4f,2.87f,-43.8f),2.3f,.65f);
            main.Turn(P(-35,2.86f,-43.8f),P(-35,2.855f,-39.4f),2.05f);
            main.Line(P(-35,2.85f,-31.5f),1.9f,.7f);
            main.Fall(P(-35,.035f,-29.62f),2);
            main.Line(P(-35,.02f,-27.3f),2,.7f);
            main.Line(P(-35,0,-26),2,.6f);
            return main;
        }
        static Ribbon RuinWheelRoute()
        {
            var route=new Ribbon {volumeDepth=.35f};route.Start(P(-45,15.49f,-61.75f),1.5f);
            route.Line(P(-34.8f,15.49f,-61.75f),1.5f,.7f);return route;
        }
        // Natural-river generator keeps its own asset/scene backup. Rebuilding generated
        // meshes must not record another large native Undo snapshot of the map hierarchy.
        public static void RebuildNaturalCreek()
        {
            RequireScene();var source=S("W_H_to_G_Fall");var ribbon=HighlandRoute();
            source.preparedMesh=ribbon.Mesh(source.filter.transform,source.label);source.filter.sharedMesh=source.preparedMesh;
            source.impactPoints=ribbon.impacts.Take(3).ToArray();var root=water.transform.Find(Root);
            for(int i=0;i<source.impactPoints.Length;i++)
            {var spray=root!=null?root.Find(source.label+" impact "+i):null;if(spray!=null)spray.position=source.impactPoints[i]+Vector3.up*.045f;}
            EditorUtility.SetDirty(source.filter);EditorUtility.SetDirty(water);
        }
        [MenuItem("Tools/EVERFLOW/Water/Network/Fill G Channel Water Body")]
        public static void FillRuinChannel()
        {
            RequireScene();
            Undo.RecordObject(water,"Fill G channel water level");
            var main=HighlandRoute();var source=S("W_H_to_G_Fall");
            Undo.RecordObject(source.filter,"Fill G water mesh");
            Undo.RecordObject(source.preparedMesh,"Fill connected G water body");
            source.preparedMesh=main.Mesh(source.filter.transform,source.label);
            source.filter.sharedMesh=source.preparedMesh;source.impactPoints=main.impacts.Take(3).ToArray();
            var root=water.transform.Find(Root);
            for(int i=0;i<source.impactPoints.Length;i++)
            {
                var spray=root.Find(source.label+" impact "+i);
                if(spray!=null){Undo.RecordObject(spray,"Match G water level");spray.position=source.impactPoints[i]+Vector3.up*.045f;}
            }
            var branch=S("W_G_ToWheel");Undo.RecordObject(branch.filter,"Fill wheel water mesh");Undo.RecordObject(branch.preparedMesh,"Fill supplied wheel channel");
            branch.preparedMesh=RuinWheelRoute().Mesh(branch.filter.transform,branch.label);branch.filter.sharedMesh=branch.preparedMesh;
            EditorUtility.SetDirty(source.filter);EditorUtility.SetDirty(branch.filter);EditorUtility.SetDirty(water);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(water.gameObject.scene);EditorSceneManager.SaveScene(water.gameObject.scene);
            Directory.CreateDirectory("docs/water-validation/channel-fill");
            File.WriteAllText("docs/water-validation/channel-fill/geometry.txt",Validate());
        }
        [MenuItem("Tools/EVERFLOW/Water/Network/Refine Largest Waterfall Lip")]
        public static void RefineHighlandFall()
        {
            RequireScene();Directory.CreateDirectory(Reports+"/highland-fix");
            if(!File.Exists(Reports+"/highland-fix/before.unity.snapshot"))File.Copy(EFV2PuzzleBuild.Scene,Reports+"/highland-fix/before.unity.snapshot");
            var source=S("W_H_to_G_Fall");string path=AssetDatabase.GetAssetPath(source.preparedMesh);
            string backup=Folder+"/HighlandBeforeLipFix.asset";
            if(AssetDatabase.LoadAssetAtPath<Mesh>(backup)==null&&!AssetDatabase.CopyAsset(path,backup))throw new Exception("Cannot preserve pre-fix mesh.");
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(backup);before.name="HighlandBeforeLipFix";EditorUtility.SetDirty(before);
            Undo.RecordObject(water,"Place largest waterfall at cliff lip");Undo.RecordObject(source.filter,"Refine waterfall trajectory");
            var ribbon=HighlandRoute();source.preparedMesh=ribbon.Mesh(source.filter.transform,source.label);source.filter.sharedMesh=source.preparedMesh;
            source.impactPoints=ribbon.impacts.Take(3).ToArray();
            var root=water.transform.Find(Root);
            for(int i=0;i<source.impactPoints.Length;i++)
            {
                var spray=root.Find(source.label+" impact "+i);
                if(spray==null)throw new Exception("Missing existing impact "+i);
                Undo.RecordObject(spray,"Move landing spray");spray.position=source.impactPoints[i]+Vector3.up*.045f;
            }
            EditorUtility.SetDirty(source.filter);EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(water.gameObject.scene);EditorSceneManager.SaveScene(water.gameObject.scene);
            File.WriteAllText(Reports+"/geometry.txt",Validate());
        }
        static Material GetMaterial(string path,string shader)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
            EditorUtility.SetDirty(m);return m;
        }
        static void Bind(string owner,Ribbon ribbon,string[] hidden)
        {
            var source=S(owner);source.suppressPresentation=false;source.renderer.enabled=true;
            source.preparedMesh=ribbon.Mesh(source.filter.transform,owner);source.filter.sharedMesh=source.preparedMesh;
            source.impactPoints=ribbon.impacts.Take(3).ToArray();assigned.Add(owner);
            foreach(string n in hidden){if(!assigned.Add(n))throw new Exception("Duplicate "+n);}
            foreach(var hit in source.impactPoints)Spray(owner,hit,ribbon.widths.Max());
        }
        static void Pool(string name,Vector3 min,Vector3 max,Vector3[] impacts=null,string[] sources=null)
        {
            var s=S(name);s.suppressPresentation=false;s.networkWorldUV=true;s.renderer.enabled=true;assigned.Add(name);
            s.impactPoints=impacts??Array.Empty<Vector3>();s.impactSources=sources??Array.Empty<string>();
            var v=new List<Vector3>();var uv=new List<Vector2>();var col=new List<Color>();var tris=new List<int>();
            int nx=Mathf.CeilToInt((max.x-min.x)/.6f),nz=Mathf.CeilToInt((max.z-min.z)/.6f);
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float u=x/(float)nx,t=z/(float)nz;var p=P(Mathf.Lerp(min.x,max.x,u),min.y,Mathf.Lerp(min.z,max.z,t));
                v.Add(s.filter.transform.InverseTransformPoint(p));uv.Add(new Vector2(p.x,p.z));
                float edge=Mathf.Min(Mathf.Min(u,1-u)*(max.x-min.x),Mathf.Min(t,1-t)*(max.z-min.z));
                // Keep pool boundaries intact where narrow streams meet them.
                col.Add(new Color(0,0,Mathf.Clamp01(edge*4),1));
                if(z==0||x==0)continue;int a=(z-1)*(nx+1)+x-1,b=a+1,c=z*(nx+1)+x-1,d=c+1;tris.AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=SaveMesh(name,v,uv,col,tris,s.filter.transform,null,null);
            s.preparedMesh=mesh;s.filter.sharedMesh=mesh;
        }
        static void Spray(string owner,Vector3 hit,float width)
        {
            var go=new GameObject(owner+" impact "+S(owner).impactPoints.ToList().IndexOf(hit));go.transform.SetParent(water.transform.Find(Root),true);
            go.transform.position=hit+Vector3.up*.045f;go.transform.rotation=Quaternion.Euler(-90,0,0);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.maxParticles=48;main.loop=true;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.3f,.65f);main.startSpeed=new ParticleSystem.MinMaxCurve(.65f,1.5f);
            main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);main.gravityModifier=.8f;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.startColor=new Color(.72f,.89f,.86f,.5f);ps.useAutoRandomSeed=false;ps.randomSeed=3821;
            var emission=ps.emission;emission.rateOverTime=Mathf.Min(32,width*10);
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.radius=Mathf.Min(.8f,width*.2f);shape.angle=45;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.12f),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=sprayMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var ctrl=go.AddComponent<EFWaterImpact>();ctrl.water=water;ctrl.sourceLabel=owner;ctrl.spray=ps;
        }
        sealed class Ribbon
        {
            public float volumeDepth;
            public readonly List<Vector3> points=new List<Vector3>(),impacts=new List<Vector3>();
            public readonly List<float> widths=new List<float>(),speeds=new List<float>(),falls=new List<float>(),tails=new List<float>();
            public void Start(Vector3 p,float width){Add(p,width,.65f,0,1);}
            void Add(Vector3 p,float width,float speed,float fall,float tail)
            {points.Add(p);widths.Add(width);speeds.Add(speed);falls.Add(fall);tails.Add(tail);}
            public void Line(Vector3 end,float width,float speed,bool fade=false)
            {
                var start=points.Last();float sw=widths.Last(),ss=speeds.Last();int steps=Mathf.Max(2,Mathf.CeilToInt(Vector3.Distance(start,end)/.35f));
                for(int i=1;i<=steps;i++){float t=i/(float)steps;Add(Vector3.Lerp(start,end,t),Mathf.Lerp(sw,width,t),Mathf.Lerp(ss,speed,Mathf.Clamp01(t*4)),0,fade?1-t:1);}
            }
            public void Turn(Vector3 control,Vector3 end,float width)
            {
                var a=points.Last();float sw=widths.Last();int steps=Mathf.Max(12,Mathf.CeilToInt((Vector3.Distance(a,control)+Vector3.Distance(control,end))/.2f));
                for(int i=1;i<=steps;i++){float t=i/(float)steps;Add((1-t)*(1-t)*a+2*t*(1-t)*control+t*t*end,Mathf.Lerp(sw,width,t),.7f,0,1);}
            }
            public void Fall(Vector3 end,float width)
            {
                var start=points.Last();float drop=start.y-end.y;
                if(drop<=0)throw new Exception("A waterfall cannot flow uphill.");
                float duration=Mathf.Sqrt(drop/4.905f),h=Vector2.Distance(new Vector2(start.x,start.z),new Vector2(end.x,end.z))/duration;
                int steps=Mathf.Max(32,Mathf.CeilToInt(Vector3.Distance(start,end)/.18f));
                for(int i=1;i<=steps;i++)
                {
                    float f=i/(float)steps,t=duration*f;var p=Vector3.Lerp(start,end,f);p.y=start.y-4.905f*t*t;
                    Add(p,width*(1-.05f*Mathf.Sin(f*Mathf.PI)),Mathf.Sqrt(h*h+9.81f*t*9.81f*t),Mathf.Clamp01(t/.22f),1);
                }
                impacts.Add(end);
            }
            public Mesh Mesh(Transform space,string name)
            {
                var v=new List<Vector3>();var uv=new List<Vector2>();var progress=new List<Vector2>();var col=new List<Color>();
                var tri=new List<int>();var normals=new List<Vector3>();var tangents=new List<Vector4>();
                const int across=16;float travel=0,total=0;for(int i=1;i<points.Count;i++)total+=Vector3.Distance(points[i-1],points[i]);float distance=0;
                for(int i=0;i<points.Count;i++)
                {
                    if(i>0){float d=Vector3.Distance(points[i-1],points[i]);distance+=d;travel+=d/Mathf.Max(.2f,(speeds[i]+speeds[i-1])*.5f)*.72f;}
                    var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(i-1,0)]).normalized;
                    var right=Vector3.Cross(Vector3.up,new Vector3(tangent.x,0,tangent.z).normalized);
                    if(right.sqrMagnitude<.1f)right=Vector3.right;
                    var normal=Vector3.Cross(tangent,right).normalized;
                    float impact=0;foreach(var p in impacts)impact=Mathf.Max(impact,Mathf.Exp(-Mathf.Pow(Vector3.Distance(points[i],p)/.7f,2)));
                    for(int j=0;j<=across;j++)
                    {
                        float u=j/(float)across;var p=points[i]+right*((u-.5f)*widths[i]);
                        v.Add(space.InverseTransformPoint(p));uv.Add(new Vector2(u*widths[i],travel));progress.Add(new Vector2(distance/Mathf.Max(.1f,total),0));
                        float edge=Mathf.SmoothStep(0,1,Mathf.Min(u,1-u)/.018f)*tails[i];
                        float pinned=Mathf.Clamp01(distance/.4f)*Mathf.Clamp01((total-distance)/.4f)*Mathf.Sin(u*Mathf.PI);
                        col.Add(new Color(falls[i],impact,pinned,edge));normals.Add(space.InverseTransformDirection(normal));
                        var local=space.InverseTransformDirection(right);tangents.Add(new Vector4(local.x,local.y,local.z,1));
                        if(i==0||j==0)continue;int a=(i-1)*(across+1)+j-1,b=a+1,c=i*(across+1)+j-1,d=c+1;tri.AddRange(new[]{a,c,b,b,c,d});
                    }
                }
                if(volumeDepth>0)CloseBody(v,uv,progress,col,tri,normals,tangents,space,across);
                var mesh=SaveMesh(name,v,uv,col,tri,space,normals,tangents);mesh.SetUVs(1,progress);EditorUtility.SetDirty(mesh);return mesh;
            }
            void CloseBody(List<Vector3> vertices,List<Vector2> uv,List<Vector2> progress,List<Color> colors,List<int> triangles,List<Vector3> normals,List<Vector4> tangents,Transform space,int across)
            {
                int stride=across+1,topCount=vertices.Count;
                // Duplicate the same flow surface along its normal, then close all four boundaries.
                // A single watertight mesh follows the lip and ballistic curve; no stacked planes.
                for(int i=0;i<topCount;i++)
                {
                    int row=i/stride;var p=points[row];
                    float depth=p.y>15.48f&&p.y<15.50f&&p.z>=-72.41f&&p.z<=-60.21f?.35f:volumeDepth;
                    vertices.Add(vertices[i]-space.InverseTransformVector(space.TransformDirection(normals[i]).normalized*depth));
                    uv.Add(uv[i]);progress.Add(new Vector2(progress[i].x,1));
                    colors.Add(new Color(colors[i].r,0,0,tails[row]));normals.Add(-normals[i]);tangents.Add(tangents[i]);
                }
                var body=new List<int>();
                for(int row=1;row<points.Count;row++)for(int j=1;j<=across;j++)
                {
                    int a=(row-1)*stride+j-1+topCount,b=a+1,c=row*stride+j-1+topCount,d=c+1;
                    body.AddRange(new[]{a,b,c,b,d,c});
                }
                var border=new List<int>();
                for(int j=0;j<stride;j++)border.Add(j);
                for(int row=1;row<points.Count;row++)border.Add(row*stride+across);
                for(int j=across-1;j>=0;j--)border.Add((points.Count-1)*stride+j);
                for(int row=points.Count-2;row>0;row--)border.Add(row*stride);
                for(int i=0;i<border.Count;i++)
                {
                    int a=border[i],b=border[(i+1)%border.Count],baseIndex=vertices.Count;
                    var normal=Vector3.Cross(vertices[b]-vertices[a],vertices[a+topCount]-vertices[a]).normalized;
                    foreach(int src in new[]{a,b,a+topCount,b+topCount})
                    {
                        vertices.Add(vertices[src]);uv.Add(uv[src]);progress.Add(new Vector2(progress[src].x,1));
                        colors.Add(new Color(colors[src].r,0,0,tails[src%topCount/stride]));normals.Add(normal);tangents.Add(tangents[src]);
                    }
                    body.AddRange(new[]{baseIndex,baseIndex+1,baseIndex+2,baseIndex+1,baseIndex+3,baseIndex+2});
                }
                body.AddRange(triangles);triangles.Clear();triangles.AddRange(body);
            }
        }
        static Mesh SaveMesh(string name,List<Vector3> v,List<Vector2> uv,List<Color> colors,List<int> tri,Transform space,List<Vector3> normals,List<Vector4> tangents)
        {
            string path=Folder+"/"+name+"_Network.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.name=name+"_Network";mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(tri,0);
            if(normals!=null)mesh.SetNormals(normals);else mesh.SetNormals(Enumerable.Repeat(space.InverseTransformDirection(Vector3.up),v.Count).ToList());
            if(tangents!=null)mesh.SetTangents(tangents);else mesh.SetTangents(Enumerable.Repeat(new Vector4(1,0,0,1),v.Count).ToList());
            mesh.RecalculateBounds();mesh.bounds=new Bounds(mesh.bounds.center,mesh.bounds.size+Vector3.one*.04f);EditorUtility.SetDirty(mesh);return mesh;
        }
        public static string Validate()
        {
            water=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();var text=new StringBuilder();int vertices=0;
            foreach(var s in water.segments)
            {
                if(!s.networkSurface)throw new Exception("Unconverted "+s.label);
                if(s.filter.GetComponents<Collider>().Length!=0)throw new Exception("Water collider "+s.label);
                if(s.suppressPresentation){text.AppendLine(s.label+" -> covered by continuous route (original gameplay object retained)");continue;}
                var mesh=s.preparedMesh;vertices+=mesh.vertexCount;
                if(mesh.vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)))throw new Exception("Invalid vertex "+s.label);
                if(mesh.uv.Length!=mesh.vertexCount||mesh.normals.Length!=mesh.vertexCount||mesh.tangents.Length!=mesh.vertexCount)throw new Exception("Missing channels "+s.label);
                if(!s.networkWorldUV && mesh.uv2.Length!=mesh.vertexCount)throw new Exception("Missing path progress "+s.label);
                text.AppendLine(s.label+" verts="+mesh.vertexCount+" triangles="+mesh.triangles.Length/3+" impacts="+s.impactPoints.Length+" pool="+s.networkWorldUV);
            }
            text.AppendLine("segments="+water.segments.Length+" owners="+water.segments.Count(s=>!s.suppressPresentation)+" totalVertices="+vertices);
            return text.ToString();
        }
        [MenuItem("Tools/EVERFLOW/Water/Network/Restore Before Network")]
        public static void Restore()
        {
            RequireScene();var b=AssetDatabase.LoadAssetAtPath<EFWaterNetworkBackup>(Folder+"/BeforeNetwork.asset");
            if(b==null)throw new Exception("No network baseline.");Undo.RecordObject(water,"Restore water before network");
            foreach(var e in b.entries)
            {
                var s=S(e.label);Undo.RecordObject(s.filter,"Restore mesh");Undo.RecordObject(s.renderer,"Restore material");
                s.preparedMesh=e.mesh;s.filter.sharedMesh=e.mesh;s.materialOverride=e.material;s.suppressPresentation=e.suppressed;
                s.networkSurface=e.network;s.networkWorldUV=e.worldUV;s.impactPoints=e.impacts??Array.Empty<Vector3>();s.impactSources=e.sources??Array.Empty<string>();
                s.renderer.sharedMaterial=e.material!=null?e.material:water.waterMaterial;s.renderer.enabled=!e.suppressed;
                EditorUtility.SetDirty(s.filter);EditorUtility.SetDirty(s.renderer);
            }
            var root=water.transform.Find(Root);if(root!=null)Undo.DestroyObjectImmediate(root.gameObject);
            foreach(var prior in UnityEngine.Object.FindObjectsByType<EFLeakPrototypeSplash>(FindObjectsInactive.Include,FindObjectsSortMode.None))prior.gameObject.SetActive(b.prototypeSprayActive);
            EditorUtility.SetDirty(water);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(water.gameObject.scene);EditorSceneManager.SaveScene(water.gameObject.scene);
        }
    }
}
