using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFMapTerrainRepair
    {
        struct Vertex
        {
            public Vector3 p, n;
            public Vector2 uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
            { p = Vector3.Lerp(a.p,b.p,t), n = Vector3.Lerp(a.n,b.n,t).normalized, uv = Vector2.Lerp(a.uv,b.uv,t) };
        }
        // Remove only the 3.9m-wide strip where the supplied cliff intersects its
        // supplied staircase. The generated override is both visible and physical.
        // Original FBX bytes, import settings, materials and model transforms remain.
        public static bool Apply(EFMapWorld world)
        {
            var all=world.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
            var terrain=all.Single(t=>t.name=="EF_Terrain");
            var filter=all.Single(t=>t.name=="T_Terrain").GetComponent<MeshFilter>();
            const string path="Assets/EverflowMap/TerrainStairOpening.asset";
            var repaired=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(repaired!=null&&filter.sharedMesh==repaired)return false;
            if(repaired==null)
            {
                var source=filter.sharedMesh;
                var positions=source.vertices;var normals=source.normals;var uvs=source.uv;
                var vertices=new List<Vector3>();var normalOut=new List<Vector3>();var uvOut=new List<Vector2>();
                var submeshes=new List<int[]>();int removed=0;
                for(int sub=0;sub<source.subMeshCount;sub++)
                {
                    var indices=new List<int>();var triangles=source.GetTriangles(sub);
                    for(int i=0;i<triangles.Length;i+=3)
                    {
                        var polygon=new List<Vertex>();
                        for(int j=0;j<3;j++)
                        {
                            int k=triangles[i+j];
                            polygon.Add(new Vertex{p=filter.transform.TransformPoint(positions[k])-terrain.position,
                                n=normals.Length>k?normals[k]:Vector3.up,uv=uvs.Length>k?uvs[k]:Vector2.zero});
                        }
                        // Cheap reject preserves original triangles outside the corridor.
                        if(polygon.Max(v=>v.p.x)<-19.7f||polygon.Min(v=>v.p.x)>-8.4f||polygon.Max(v=>v.p.z)<3.05f||polygon.Min(v=>v.p.z)>6.95f)
                        {Emit(polygon);continue;}
                        var inside=polygon;
                        for(int plane=0;plane<4&&inside.Count>=3;plane++)
                        {
                            Split(inside,plane,out var remains,out var outside);
                            if(outside.Count>=3)Emit(outside);
                            inside=remains;
                        }
                        if(inside.Count>=3)removed++;
                    }
                    submeshes.Add(indices.ToArray());
                    void Emit(List<Vertex> polygon)
                    {
                        if(polygon.Count<3)return;
                        int first=vertices.Count;
                        foreach(var v in polygon)
                        {vertices.Add(filter.transform.InverseTransformPoint(terrain.position+v.p));normalOut.Add(v.n);uvOut.Add(v.uv);}
                        for(int j=1;j<polygon.Count-1;j++){indices.Add(first);indices.Add(first+j);indices.Add(first+j+1);}
                    }
                }
                repaired=new Mesh{name="Terrain with P5 stair opening",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
                repaired.SetVertices(vertices);repaired.SetNormals(normalOut);repaired.SetUVs(0,uvOut);
                repaired.subMeshCount=submeshes.Count;
                for(int i=0;i<submeshes.Count;i++)repaired.SetTriangles(submeshes[i],i);
                repaired.RecalculateBounds();AssetDatabase.CreateAsset(repaired,path);
                Debug.Log($"MAP STAIR OPENING: clipped {removed} terrain triangles in the stair corridor; materials/submeshes preserved.");
            }
            filter.sharedMesh=repaired;filter.GetComponent<MeshCollider>().sharedMesh=repaired;
            return true;
        }
        static float Distance(Vector3 p,int plane)
        {
            switch(plane){case 0:return p.x+19.7f;case 1:return -8.4f-p.x;case 2:return p.z-3.05f;default:return 6.95f-p.z;}
        }
        static void Split(List<Vertex> polygon,int plane,out List<Vertex> inside,out List<Vertex> outside)
        {
            inside=new List<Vertex>();outside=new List<Vertex>();
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=Distance(a.p,plane),db=Distance(b.p,plane);
                bool ia=da>=0,ib=db>=0;
                (ia?inside:outside).Add(a);
                if(ia==ib)continue;
                var v=Vertex.Lerp(a,b,da/(da-db));inside.Add(v);outside.Add(v);
            }
        }
    }
}
