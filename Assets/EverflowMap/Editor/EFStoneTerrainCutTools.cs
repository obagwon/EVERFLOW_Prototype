using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMap.Editor
{
    public static class EFStoneTerrainCutTools
    {
        public const string Folder="Assets/EverflowMap/StoneArchitecture";
        const string Backup=Folder+"/TerrainBeforeStairCut.asset",Output=Folder+"/TerrainStairCut.asset";
        const string GorgeOutput="Assets/Resources/Everflow/TerrainGorgeCut.asset";
        static readonly Rect Patch=Rect.MinMaxRect(-63.5f,-64.3f,-54.5f,-57.7f);
        static readonly Rect GorgePatch=Rect.MinMaxRect(-85.27f,-49.3f,-78.45f,-1.5f);
        static readonly Vector3 From=new Vector3(-61,11.9f,-60),To=new Vector3(-57,15.15f,-62);
        struct Vertex
        {public Vector3 p,n;public Vector2 uv;public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t),uv=Vector2.Lerp(a.uv,b.uv,t)};}
        static float Side(Vector3 p,Rect r,int side)=>side==0?p.x-r.xMin:side==1?r.xMax-p.x:side==2?p.z-r.yMin:r.yMax-p.z;
        static void Split(List<Vertex> input,Rect rect,int side,out List<Vertex> inside,out List<Vertex> outside)
            =>Split(input,p=>Side(p,rect,side),out inside,out outside);
        static void Split(List<Vertex> input,Func<Vector3,float> side,out List<Vertex> inside,out List<Vertex> outside)
        {
            inside=new List<Vertex>();outside=new List<Vertex>();
            for(int i=0;i<input.Count;i++)
            {
                var a=input[i];var b=input[(i+1)%input.Count];float da=side(a.p),db=side(b.p);bool ia=da>=0,ib=db>=0;
                (ia?inside:outside).Add(a);if(ia!=ib){var v=Vertex.Lerp(a,b,da/(da-db));inside.Add(v);outside.Add(v);}
            }
        }
        static List<Vertex> Clip(List<Vertex> p,Rect rect)
        {for(int side=0;side<4&&p.Count>=3;side++){Split(p,rect,side,out var inner,out _);p=inner;}return p;}
        static Vector3 Sculpt(Vector3 p)
        {
            var forward=Vector3.ProjectOnPlane(To-From,Vector3.up);float length=forward.magnitude;forward/=length;
            float along=Vector3.Dot(p-From,forward),cross=Mathf.Abs(Vector3.Dot(p-From,Vector3.Cross(Vector3.up,forward)));
            float floor=Mathf.Lerp(From.y,To.y,Mathf.Clamp01(along/length));
            if(p.y<floor-.45f||p.y>18.5f)return p;
            float Fade(float value,float a,float b)=>1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,value));
            float weight=Fade(cross,1.30f,1.85f);
            if(along<-.2f)weight*=Fade(-along,.2f,.65f);
            if(along>length+.2f)weight*=Fade(along-length,.2f,.65f);
            p.y=Mathf.Lerp(p.y,floor-.45f,weight);return p;
        }
        static Vector3 SculptGorge(Vector3 p)
        {
            float floor=Mathf.Lerp(-11.9f,-17.35f,Mathf.Clamp01((p.z+47)/35));
            float center=Mathf.Lerp(-82.3f,-83.4f,Mathf.InverseLerp(-14,-11.5f,p.z));
            float west=Mathf.Max(-84.53f,center-2.16f);
            float east=center+2.32f;
            float westFeather=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(west,GorgePatch.xMin,p.x));
            float eastFeather=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(east,GorgePatch.xMax,p.x));
            float atDoor=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-49.3f,-47.05f,p.z));
            float atGarden=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-3.3f,-1.5f,p.z));
            p.y=Mathf.Lerp(p.y,Mathf.Min(p.y,floor-.63f),westFeather*eastFeather*atDoor*atGarden);
            return p;
        }

        [MenuItem("Tools/EVERFLOW/Architecture/Rebuild Gorge and D1 Rock Cuts")]
        public static void ApplyGorge()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            var preview=EditorSceneManager.OpenPreviewScene(EFV2PuzzleBuild.Scene);
            try
            {
                var world=preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<EFV2PuzzleWorld>(true)).Single();
                var terrain=world.mapRoot.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="T_Terrain");
                var source=terrain.sharedMesh;
                if(AssetDatabase.GetAssetPath(source)==GorgeOutput)throw new InvalidOperationException("Build the gorge from the original stair-cut source.");
                var materials=terrain.GetComponent<MeshRenderer>().sharedMaterials;
                int grass=Array.FindIndex(materials,m=>m!=null&&m.name=="M_Grass");
                int rock=Array.FindIndex(materials,m=>m!=null&&m.name=="M_Rock");
                int Finish(int sub,Vector3 normal)=>sub==grass&&rock>=0&&Mathf.Abs(normal.y)<.65f?rock:sub;
                var mesh=BuildCut(source,terrain.transform,GorgePatch,SculptGorge,-18.1f,.35f,"Gorge corridor rock cutting",out int changed,Finish);
                if(changed==0)throw new InvalidOperationException("Expected terrain within the gorge cut.");
                if(!AssetDatabase.IsValidFolder("Assets/Resources/Everflow"))AssetDatabase.CreateFolder("Assets/Resources","Everflow");
                SaveMesh(mesh,GorgeOutput);
                foreach(string name in new[]{"TEMP_Bridge_MoundB","TEMP_Bridge_MoundC"})
                {
                    var mound=world.mapRoot.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name==name);
                    SaveMesh(TrimD1Overhang(mound),"Assets/Resources/Everflow/"+name+"_Trimmed.asset");
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"Gorge cut saved to {GorgeOutput}, {changed} changed triangles. Source {AssetDatabase.GetAssetPath(source)}.");
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
        }
        public static void Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first.");
            var world=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();
            var filter=world.mapRoot.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="T_Terrain");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            if(AssetDatabase.LoadAssetAtPath<Mesh>(Backup)==null)
            {
                var path=AssetDatabase.GetAssetPath(filter.sharedMesh);
                if(!path.StartsWith(EFNaturalRiverTools.Folder)||!AssetDatabase.CopyAsset(path,Backup))throw new Exception("Expected natural-river terrain copy before stair cut.");
            }
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(Backup);
            var mesh=BuildCut(source,filter.transform,Patch,Sculpt,10.8f,.3f,"C stone stair rock cutting",out int changed);
            var saved=SaveMesh(mesh,Output);
            filter.sharedMesh=saved;EditorUtility.SetDirty(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            foreach(var collider in filter.GetComponents<MeshCollider>()){collider.sharedMesh=saved;EditorUtility.SetDirty(collider);PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            File.WriteAllText("docs/play-review-20261109/stone-architecture/terrain-cut.txt","Original natural river mesh retained in "+Backup+"\nOnly C stair corridor cut; same visible and collision mesh.\nChanged triangles="+changed+" vertices="+saved.vertexCount+"\n");
        }

        static Mesh SaveMesh(Mesh mesh,string path)
        {
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,saved);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);return saved;
        }

        static Mesh TrimD1Overhang(MeshFilter mound)
        {
            var source=mound.sharedMesh;
            var space=mound.transform;
            var positions=source.vertices;
            var normals=source.normals;
            var uv=source.uv;
            var vertices=new List<Vector3>();
            var outputNormals=new List<Vector3>();
            var outputUV=new List<Vector2>();
            var indices=Enumerable.Range(0,source.subMeshCount).Select(_=>new List<int>()).ToArray();

            void Emit(List<Vertex> polygon,int slot)
            {
                for(int i=1;i<polygon.Count-1;i++)
                {
                    var a=polygon[0];var b=polygon[i];var c=polygon[i+1];
                    if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-10f)continue;
                    foreach(var vertex in new[]{a,b,c})
                    {
                        indices[slot].Add(vertices.Count);
                        vertices.Add(space.InverseTransformPoint(vertex.p));
                        outputNormals.Add(vertex.n.normalized);
                        outputUV.Add(vertex.uv);
                    }
                }
            }

            for(int slot=0;slot<source.subMeshCount;slot++)
            {
                var triangles=source.GetTriangles(slot);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var polygon=new List<Vertex>(3);
                    for(int j=0;j<3;j++)
                    {
                        int k=triangles[i+j];
                        polygon.Add(new Vertex{p=space.TransformPoint(positions[k]),n=normals[k],uv=uv[k]});
                    }
                    // Cut faces end inside the south wall and the ceiling slab.
                    // Surface platforms and everything outside the hall keep their original shape.
                    Split(polygon,p=>p.z+35.96f,out var exterior,out var interior);
                    Split(interior,p=>p.y-1.18f,out var aboveCeiling,out _);
                    Emit(exterior,slot);
                    Emit(aboveCeiling,slot);
                }
            }
            var mesh=new Mesh{name=mound.name+" trimmed at D1 ceiling",subMeshCount=source.subMeshCount};
            mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetUVs(0,outputUV);
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildCut(Mesh source,Transform space,Rect patch,Func<Vector3,Vector3> deform,float minY,float cell,string name,out int changed,Func<int,Vector3,int> finish=null)
        {
            var positions=source.vertices;var normals=source.normals;var uv=source.uv;
            var vertices=new List<Vector3>();var outputNormals=new List<Vector3>();var outputUV=new List<Vector2>();
            var indices=Enumerable.Range(0,source.subMeshCount).Select(_=>new List<int>()).ToArray();int movedTriangles=0;
            void Emit(List<Vertex> poly,int sub,bool sculpt)
            {
                for(int j=1;j<poly.Count-1;j++)
                {
                    var tri=new[]{poly[0],poly[j],poly[j+1]};var after=tri.Select(v=>sculpt?deform(v.p):v.p).ToArray();
                    var normal=Vector3.Cross(after[1]-after[0],after[2]-after[0]);if(normal.sqrMagnitude<1e-10f)continue;
                    bool moved=Enumerable.Range(0,3).Any(k=>(after[k]-tri[k].p).sqrMagnitude>1e-7f);if(moved)movedTriangles++;
                    var faceNormal=normal.normalized;
                    int slot=moved&&finish!=null?finish(sub,faceNormal):sub;
                    for(int k=0;k<3;k++){indices[slot].Add(vertices.Count);vertices.Add(space.InverseTransformPoint(after[k]));outputNormals.Add(space.InverseTransformDirection(moved?faceNormal:tri[k].n));outputUV.Add(tri[k].uv);}
                }
            }
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var t=source.GetTriangles(sub);
                for(int i=0;i<t.Length;i+=3)
                {
                    var poly=new List<Vertex>();for(int j=0;j<3;j++){int k=t[i+j];poly.Add(new Vertex{p=space.TransformPoint(positions[k]),n=space.TransformDirection(normals[k]),uv=uv[k]});}
                    if(poly.All(v=>v.p.y<minY)||poly.Min(v=>v.p.x)>patch.xMax||poly.Max(v=>v.p.x)<patch.xMin||poly.Min(v=>v.p.z)>patch.yMax||poly.Max(v=>v.p.z)<patch.yMin){Emit(poly,sub,false);continue;}
                    for(int side=0;side<4&&poly.Count>=3;side++){Split(poly,patch,side,out var inner,out var outside);if(outside.Count>=3)Emit(outside,sub,false);poly=inner;}
                    if(poly.Count<3)continue;
                    for(int x=Mathf.FloorToInt(poly.Min(v=>v.p.x)/cell);x<=Mathf.FloorToInt(poly.Max(v=>v.p.x)/cell);x++)
                    for(int z=Mathf.FloorToInt(poly.Min(v=>v.p.z)/cell);z<=Mathf.FloorToInt(poly.Max(v=>v.p.z)/cell);z++)
                    {var clipped=Clip(poly,new Rect(x*cell,z*cell,cell,cell));if(clipped.Count>=3)Emit(clipped,sub,true);}
                }
            }
            var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(outputNormals);mesh.SetUVs(0,outputUV);mesh.subMeshCount=source.subMeshCount;
            for(int i=0;i<indices.Length;i++)mesh.SetTriangles(indices[i],i);mesh.RecalculateBounds();
            changed=movedTriangles;return mesh;
        }
    }
}
