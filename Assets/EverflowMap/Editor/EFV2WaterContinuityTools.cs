using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFV2WaterContinuityTools
    {
        const string ScenePath="Assets/Scenes/Everflow_Map_v2_test_with_puzzle.unity";
        [MenuItem("Tools/EVERFLOW/Water/Smooth Leak Waterfalls")]
        public static void Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.path!=ScenePath)throw new InvalidOperationException("Open the puzzle scene outside Play mode first.");
            Directory.CreateDirectory("docs/water-validation/continuity");
            string backup="docs/water-validation/continuity/before.unity.snapshot";
            if(!File.Exists(backup))File.Copy(ScenePath,backup);
            var water=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();
            Undo.RecordObject(water,"Smooth leak waterfalls");
            Build(water,"W_Leak_Fall2",new[]{
                new Vector3(-35,3.225f,-31.65f),new Vector3(-35,3.225f,-30.6f),
                new Vector3(-35,2.65f,-29.96f),new Vector3(-35,.65f,-29.96f),
                new Vector3(-35,.235f,-29.65f),new Vector3(-35,.225f,-28.6f)},2);
            Build(water,"W_G_Leak_Fall",new[]{
                new Vector3(-45,15.225f,-62.15f),new Vector3(-45,15.225f,-57.8f),
                new Vector3(-45,14.55f,-57.0f),new Vector3(-45,3.8f,-55.4f),
                new Vector3(-45,3.24f,-54.8f),new Vector3(-45,3.225f,-53.9f)},2);
            Foam(water,"W_G_Leak_Fall_Foam",new Vector3(-45,3.25f,-54.8f),2.5f,1.7f);
            Foam(water,"W_Leak_Fall2_Foam",new Vector3(-35,.25f,-29.35f),2.5f,1.6f);
            EditorUtility.SetDirty(water);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        static void Build(EFV2WaterIntegration water,string label,Vector3[] p,float width)
        {
            var s=water.segments.Single(x=>x.label==label);
            var points=new List<Vector3>();
            // Horizontal lip, smooth quarter turn, falling sheet, smooth landing.
            Add(points,p[0],Vector3.Lerp(p[0],p[1],.65f),p[1],12);
            Add(points,p[1],new Vector3(p[1].x,p[1].y,p[2].z),p[2],16);
            Add(points,p[2],Vector3.Lerp(p[2],p[3],.5f),p[3],40);
            Add(points,p[3],new Vector3(p[3].x,p[4].y,p[3].z),p[4],16);
            Add(points,p[4],new Vector3(p[4].x,p[5].y,p[5].z),p[5],16);
            var verts=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var tris=new List<int>();
            float distance=0;const int across=12;
            for(int i=0;i<points.Count;i++)
            {
                if(i>0)distance+=Vector3.Distance(points[i-1],points[i]);
                for(int j=0;j<=across;j++)
                {
                    float u=j/(float)across;
                    verts.Add(s.filter.transform.InverseTransformPoint(points[i]+Vector3.right*((u-.5f)*width)));
                    uv.Add(new Vector2(u*width,distance));
                    float edge=Mathf.SmoothStep(0,1,Mathf.Min(u,1-u)/.085f);
                    float tail=Mathf.Clamp01((points.Count-1-i)/9f);
                    float impact=Mathf.Exp(-Mathf.Pow((points[i].y-p[5].y)/.4f,2));
                    colors.Add(new Color(impact,1,1,edge*tail));
                    if(i==0||j==0)continue;
                    int a=(i-1)*(across+1)+j-1,b=a+1,c=i*(across+1)+j-1,d=c+1;
                    // Both sides visible, without adding overlapping box walls/caps.
                    tris.AddRange(new[]{a,c,b,b,c,d,b,c,a,d,c,b});
                }
            }
            string path="Assets/EverflowWater/Meshes/"+label+"_Continuous.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else Undo.RecordObject(mesh,"Rebuild waterfall ribbon");
            mesh.Clear();mesh.name=label+" continuous curved sheet";
            mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(tris,0);
            // Explicit smooth normals: reversed back faces must not cancel them.
            var normals=new Vector3[verts.Count];
            for(int i=0;i<points.Count;i++)
            {
                var tangent=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];
                var normal=Vector3.Cross(tangent.normalized,Vector3.right);
                for(int j=0;j<=across;j++)normals[i*(across+1)+j]=s.filter.transform.InverseTransformDirection(normal);
            }
            mesh.normals=normals;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            Undo.RecordObject(s.filter,"Replace leak box with curved sheet");
            s.preparedMesh=mesh;s.filter.sharedMesh=mesh;s.pathFlow=true;s.waveHeight=0;
            EditorUtility.SetDirty(s.filter);
        }
        static void Add(List<Vector3> points,Vector3 a,Vector3 b,Vector3 c,int steps)
        {
            for(int i=points.Count==0?0:1;i<=steps;i++)
            {float t=i/(float)steps;points.Add((1-t)*(1-t)*a+2*(1-t)*t*b+t*t*c);}
        }
        static void Foam(EFV2WaterIntegration water,string label,Vector3 center,float width,float length)
        {
            var s=water.segments.Single(x=>x.label==label);
            string path="Assets/EverflowWater/Meshes/"+label+"_Soft.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}
            var v=new List<Vector3>();var col=new List<Color>();var tri=new List<int>();
            v.Add(s.filter.transform.InverseTransformPoint(center));col.Add(Color.white);
            const int count=48;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;
                v.Add(s.filter.transform.InverseTransformPoint(center+new Vector3(Mathf.Cos(a)*width*.5f,0,Mathf.Sin(a)*length*.5f)));
                col.Add(new Color(1,1,1,0));tri.AddRange(new[]{0,1+(i+1)%count,1+i});
            }
            mesh.Clear();mesh.name=label+" soft landing foam";mesh.SetVertices(v);mesh.SetColors(col);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);Undo.RecordObject(s.filter,"Soften landing foam");
            s.preparedMesh=mesh;s.filter.sharedMesh=mesh;EditorUtility.SetDirty(s.filter);
        }
    }
}
