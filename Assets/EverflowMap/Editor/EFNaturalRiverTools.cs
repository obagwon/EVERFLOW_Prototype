using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap.Editor
{
    public static class EFNaturalRiverTools
    {
        public const string Folder="Assets/EverflowWater/NaturalRiver";
        public const string Reports="docs/play-review-20261109/natural-river";
        const float Cell=.4f,Feather=1.35f;
        sealed class Reach
        {
            public string name;public Vector3[] points;public float[] widths;public Bounds bounds;
        }
        struct Vertex
        {
            public Vector3 p,n;public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
        }
        struct Field {public float distance,halfWidth,water,weight;public bool valid;}
        struct GroundTriangle {public Vector3 a,b,c;}
        static readonly Dictionary<Vector2Int,List<GroundTriangle>> groundGrid=new Dictionary<Vector2Int,List<GroundTriangle>>();
        static Reach[] reaches;static Material[] palette;
        static Vector2Int GroundCell(Vector3 p)=>new Vector2Int(Mathf.FloorToInt(p.x/2),Mathf.FloorToInt(p.z/2));
        static void BuildGroundHeights(EFNaturalRiverBackup backup,MeshFilter[] filters)
        {
            groundGrid.Clear();
            foreach(var filter in filters)
            using(var data=MeshUtility.AcquireReadOnlyMeshData(backup.ground.Single(g=>g.name==filter.name).mesh))
            {
                var d=data[0];var p=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp);
                try
                {
                    d.GetVertices(p);
                    for(int sub=0;sub<d.subMeshCount;sub++)
                    {
                        var sm=d.GetSubMesh(sub);
                        int Index(int i)=>d.indexFormat==IndexFormat.UInt16?d.GetIndexData<ushort>()[sm.indexStart+i]+sm.baseVertex:(int)d.GetIndexData<uint>()[sm.indexStart+i]+sm.baseVertex;
                        for(int i=0;i<sm.indexCount;i+=3)
                        {
                            var a=filter.transform.TransformPoint(p[Index(i)]);var b=filter.transform.TransformPoint(p[Index(i+1)]);var c=filter.transform.TransformPoint(p[Index(i+2)]);
                            if(Vector3.Cross(b-a,c-a).normalized.y<.25f)continue;
                            var bounds=new Bounds(a,Vector3.zero);bounds.Encapsulate(b);bounds.Encapsulate(c);if(!reaches.Any(r=>r.bounds.Intersects(bounds)))continue;
                            var min=GroundCell(bounds.min);var max=GroundCell(bounds.max);var tri=new GroundTriangle{a=a,b=b,c=c};
                            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++)
                            {var key=new Vector2Int(x,z);if(!groundGrid.TryGetValue(key,out var list)){list=new List<GroundTriangle>();groundGrid[key]=list;}list.Add(tri);}
                        }
                    }
                }
                finally{p.Dispose();}
            }
        }
        static float GroundTop(Vector3 p,float water)
        {
            float top=float.NegativeInfinity;
            if(groundGrid.TryGetValue(GroundCell(p),out var triangles))foreach(var t in triangles)
            {
                var v0=new Vector2(t.b.x-t.a.x,t.b.z-t.a.z);var v1=new Vector2(t.c.x-t.a.x,t.c.z-t.a.z);var v2=new Vector2(p.x-t.a.x,p.z-t.a.z);
                float det=v0.x*v1.y-v1.x*v0.y;if(Mathf.Abs(det)<.0000001f)continue;
                float u=(v2.x*v1.y-v1.x*v2.y)/det,v=(v0.x*v2.y-v2.x*v0.y)/det;
                if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
                float y=t.a.y+(t.b.y-t.a.y)*u+(t.c.y-t.a.y)*v;
                if(Mathf.Abs(y-water)<.9f)top=Mathf.Max(top,y);
            }
            return float.IsNegativeInfinity(top)?p.y:top;
        }
        static Mesh SavedMesh(string name,Mesh generated)
        {
            string path=Folder+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){generated.name=name;AssetDatabase.CreateAsset(generated,path);return generated;}
            EditorUtility.CopySerialized(generated,existing);UnityEngine.Object.DestroyImmediate(generated);existing.name=name;EditorUtility.SetDirty(existing);return existing;
        }
        static Material Mat(string name,Color color)
        {
            string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(m);return m;
        }
        static bool GroundName(string name)=>name=="T_Terrain"||name=="HALL_Floor"||new[]{"ROOF_E","ROOF_N","ROOF_S","ROOF_W","PLT_South_E","PLT_South_W","PLT_South_WinHi","PLT_South_WinLo"}.Contains(name);
        static Reach ReadReach(EFV2WaterIntegration water,string source,string name,Func<Vector3,bool> select)
        {
            var s=water.segments.Single(v=>v.label==source);var mesh=s.preparedMesh;var v=mesh.vertices;var uv=mesh.uv2;
            int count=uv.TakeWhile(u=>u.y<.5f).Count();if(count%17!=0)throw new Exception("Unexpected ribbon layout: "+source);
            var p=new List<Vector3>();var widths=new List<float>();
            for(int row=0;row<count/17;row++)
            {
                var center=s.filter.transform.TransformPoint(v[row*17+8]);if(!select(center))continue;
                p.Add(center);widths.Add(Vector3.Distance(s.filter.transform.TransformPoint(v[row*17]),s.filter.transform.TransformPoint(v[row*17+16])));
            }
            if(p.Count<2)throw new Exception("Missing natural river reach: "+name);
            var bounds=new Bounds(p[0],Vector3.zero);foreach(var point in p)bounds.Encapsulate(point);
            float margin=widths.Max()*.5f+Feather+.05f;bounds.Expand(new Vector3(margin*2,1.8f,margin*2));
            return new Reach{name=name,points=p.ToArray(),widths=widths.ToArray(),bounds=bounds};
        }
        static void ReadReaches(EFV2WaterIntegration water)
        {
            reaches=new[]{
                ReadReach(water,"W_H_to_G_Fall","Skylight creek",p=>p.y>=2.849f&&p.y<=2.901f&&p.z>=-56.71f&&p.z<=-31.499f),
                ReadReach(water,"W_H_to_G_Fall","Pond inlet",p=>p.y>=-.001f&&p.y<=.036f&&p.z>=-29.621f),
                ReadReach(water,"W_D5_SpillPool","Cave spill bed",p=>Mathf.Abs(p.y+11.85f)<.005f&&p.z>=-62.901f),
                ReadReach(water,"W_Exit_Tunnel","Gorge bed",p=>p.z>=-44.95f)
            };
        }
        static Field Sample(Vector3 p)
        {
            var best=new Field();float score=float.MaxValue;
            foreach(var reach in reaches)
            {
                if(p.x<reach.bounds.min.x||p.x>reach.bounds.max.x||p.z<reach.bounds.min.z||p.z>reach.bounds.max.z||p.y<reach.bounds.min.y||p.y>reach.bounds.max.y)continue;
                for(int i=1;i<reach.points.Length;i++)
                {
                    var a=reach.points[i-1];var b=reach.points[i];var delta=new Vector2(b.x-a.x,b.z-a.z);
                    float t=Mathf.Clamp01(Vector2.Dot(new Vector2(p.x-a.x,p.z-a.z),delta)/Mathf.Max(.00001f,delta.sqrMagnitude));
                    float y=Mathf.Lerp(a.y,b.y,t);if(Mathf.Abs(p.y-y)>.82f)continue;
                    float distance=Vector2.Distance(new Vector2(p.x,p.z),new Vector2(Mathf.Lerp(a.x,b.x,t),Mathf.Lerp(a.z,b.z,t)));
                    float width=Mathf.Lerp(reach.widths[i-1],reach.widths[i],t)*.5f;
                    float weight=1;
                    // An open river mouth must not acquire a semicircular bank/weir in its receiving pool.
                    if(i==reach.points.Length-1&&t>.999f)
                    {float over=Vector2.Dot(new Vector2(p.x-b.x,p.z-b.z),delta.normalized);if(over>0)weight=1-Mathf.SmoothStep(0,1,over/.85f);}
                    if(weight<=0)continue;
                    float s=distance-width;
                    if(s>Feather||s>=score)continue;
                    score=s;best=new Field{distance=distance,halfWidth=width,water=y,weight=weight,valid=true};
                }
            }
            return best;
        }
        static Vector3 Sculpt(Vector3 p)
        {
            var f=Sample(p);if(!f.valid)return p;
            float d=f.distance,h=f.halfWidth;
            float ground=GroundTop(p,f.water);
            float noise=(Mathf.PerlinNoise(p.x*.71f+19,p.z*.71f+7)-.5f)*.025f;
            float target;
            if(d<=h)
                target=f.water-.30f+.325f*Mathf.Pow(Mathf.Clamp01(d/h),2.4f)+noise;
            else if(d<h+.5f)
                target=Mathf.Lerp(f.water+.025f,f.water+.15f,Mathf.SmoothStep(0,1,(d-h)/.5f))+noise;
            else
                target=Mathf.Lerp(f.water+.15f,ground,Mathf.SmoothStep(0,1,(d-h-.5f)/(Feather-.5f)));
            // Never sculpt more than 0.6m: do not pull a cliff or a different floor into a river.
            // Move buried roof/rock layers by the same delta as the visible ground,
            // preserving their separation rather than collapsing them onto one plane.
            p.y+=Mathf.Clamp(target-ground,-.6f,.4f)*f.weight;return p;
        }
        static float Plane(Vector3 p,Rect r,int side)
        {switch(side){case 0:return p.x-r.xMin;case 1:return r.xMax-p.x;case 2:return p.z-r.yMin;default:return r.yMax-p.z;}}
        static void Split(List<Vertex> polygon,Rect rect,int side,out List<Vertex> inside,out List<Vertex> outside)
        {
            inside=new List<Vertex>();outside=new List<Vertex>();
            for(int i=0;i<polygon.Count;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Count];float da=Plane(a.p,rect,side),db=Plane(b.p,rect,side);bool ia=da>=0,ib=db>=0;
                (ia?inside:outside).Add(a);if(ia==ib)continue;
                var v=Vertex.Lerp(a,b,da/(da-db));inside.Add(v);outside.Add(v);
            }
        }
        static List<Vertex> Clip(List<Vertex> p,Rect r)
        {for(int side=0;side<4&&p.Count>=3;side++){Split(p,r,side,out var inner,out _);p=inner;}return p;}
        static Mesh Carve(Mesh source,Transform space,int materialCount,out int changed)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uvOut=new List<Vector2>();
            var smooth=new List<bool>();var weights=new List<Vector3>();
            var indices=Enumerable.Range(0,materialCount+palette.Length).Select(_=>new List<int>()).ToArray();int modified=0;
            using(var data=MeshUtility.AcquireReadOnlyMeshData(source))
            {
                var d=data[0];var p=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp);var n=new NativeArray<Vector3>(d.vertexCount,Allocator.Temp);var uv=new NativeArray<Vector2>(d.vertexCount,Allocator.Temp);
                try
                {
                    d.GetVertices(p);if(d.HasVertexAttribute(VertexAttribute.Normal))d.GetNormals(n);if(d.HasVertexAttribute(VertexAttribute.TexCoord0))d.GetUVs(0,uv);
                    for(int sub=0;sub<d.subMeshCount;sub++)
                    {
                        var sm=d.GetSubMesh(sub);if(sm.topology!=MeshTopology.Triangles)throw new Exception("Non-triangle ground mesh");
                        int GetIndex(int i)=>d.indexFormat==IndexFormat.UInt16?d.GetIndexData<ushort>()[sm.indexStart+i]+sm.baseVertex:(int)d.GetIndexData<uint>()[sm.indexStart+i]+sm.baseVertex;
                        for(int i=0;i<sm.indexCount;i+=3)
                        {
                            var polygon=new List<Vertex>();for(int j=0;j<3;j++){int k=GetIndex(i+j);polygon.Add(new Vertex{p=space.TransformPoint(p[k]),n=space.TransformDirection(n[k]),uv=uv[k]});}
                            var pending=new List<List<Vertex>>{polygon};
                            foreach(var reach in reaches)
                            {
                                var next=new List<List<Vertex>>();
                                foreach(var poly in pending)
                                {
                                    var bounds=new Bounds(poly[0].p,Vector3.zero);foreach(var v in poly)bounds.Encapsulate(v.p);
                                    if(!bounds.Intersects(reach.bounds)){next.Add(poly);continue;}
                                    var rect=Rect.MinMaxRect(reach.bounds.min.x,reach.bounds.min.z,reach.bounds.max.x,reach.bounds.max.z);var inner=poly;
                                    for(int side=0;side<4&&inner.Count>=3;side++)
                                    {Split(inner,rect,side,out var inside,out var outside);if(outside.Count>=3)next.Add(outside);inner=inside;}
                                    if(inner.Count<3)continue;
                                    float minX=inner.Min(v=>v.p.x),maxX=inner.Max(v=>v.p.x),minZ=inner.Min(v=>v.p.z),maxZ=inner.Max(v=>v.p.z);
                                    for(int z=Mathf.FloorToInt(minZ/Cell);z<=Mathf.FloorToInt(maxZ/Cell);z++)
                                    for(int x=Mathf.FloorToInt(minX/Cell);x<=Mathf.FloorToInt(maxX/Cell);x++)
                                    {var cell=Clip(inner,new Rect(x*Cell,z*Cell,Cell,Cell));if(cell.Count>=3)Emit(cell,sub,true);}
                                }
                                pending=next;
                            }
                            foreach(var poly in pending)Emit(poly,sub,false);
                        }
                    }
                }
                finally{p.Dispose();n.Dispose();uv.Dispose();}
            }
            void Emit(List<Vertex> poly,int sub,bool sculpt)
            {
                for(int i=1;i<poly.Count-1;i++)
                {
                    var tri=new[]{poly[0],poly[i],poly[i+1]};var before=tri.Select(v=>v.p).ToArray();var after=before.Select(v=>sculpt?Sculpt(v):v).ToArray();
                    var cross=Vector3.Cross(after[1]-after[0],after[2]-after[0]);if(cross.sqrMagnitude<.000000001f)continue;
                    bool moved=Enumerable.Range(0,3).Any(k=>Mathf.Abs(before[k].y-after[k].y)>.001f);if(moved)modified++;
                    int material=Mathf.Min(sub,materialCount-1);var field=Sample((before[0]+before[1]+before[2])/3);
                    if(sculpt&&field.valid&&cross.normalized.y>.25f)
                    {
                        float edge=field.distance-field.halfWidth;
                        int shade=field.distance<field.halfWidth*.65f?(Mathf.PerlinNoise(before[0].x*1.1f,before[0].z*1.1f)>.58f?1:0):edge<.18f?2:edge<.65f?3:4;
                        if(edge<1.05f)material=materialCount+shade;
                    }
                    int first=vertices.Count;
                    for(int k=0;k<3;k++)
                    {
                        vertices.Add(space.InverseTransformPoint(after[k]));normals.Add(space.InverseTransformDirection(moved?cross.normalized:tri[k].n));uvOut.Add(tri[k].uv);
                        smooth.Add(sculpt&&tri[k].n.y>.5f&&Sample(before[k]).valid);weights.Add(space.InverseTransformDirection(cross));
                    }
                    indices[material].AddRange(new[]{first,first+1,first+2});
                }
            }
            // Area-weighted welding of bank lighting removes skinny clipping-triangle highlights.
            // Original cliff normals and every collider vertex remain unchanged.
            var sums=new Dictionary<Vector3Int,Vector3>();
            Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x*10000),Mathf.RoundToInt(p.y*10000),Mathf.RoundToInt(p.z*10000));
            for(int i=0;i<vertices.Count;i++)if(smooth[i]){var key=Key(vertices[i]);sums.TryGetValue(key,out var sum);sums[key]=sum+weights[i];}
            for(int i=0;i<vertices.Count;i++)if(smooth[i])normals[i]=sums[Key(vertices[i])].normalized;
            var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uvOut);mesh.subMeshCount=indices.Length;
            for(int sub=0;sub<indices.Length;sub++)mesh.SetTriangles(indices[sub],sub);mesh.RecalculateBounds();changed=modified;return mesh;
        }
        static void Decorations(Transform root)
        {
            var rockVertices=new List<Vector3>();var rockTriangles=new List<int>();var grassVertices=new List<Vector3>();var grassTriangles=new List<int>();
            int seed=0;
            foreach(var reach in reaches)
            {
                float travel=0,next=1.5f;
                for(int i=1;i<reach.points.Length;i++)
                {
                    travel+=Vector3.Distance(reach.points[i-1],reach.points[i]);if(travel<next)continue;next=travel+2.8f;
                    var forward=Vector3.ProjectOnPlane(reach.points[i]-reach.points[i-1],Vector3.up).normalized;var right=Vector3.Cross(Vector3.up,forward);seed++;
                    float side=seed%2==0?1:-1;var p=reach.points[i]+right*side*(reach.widths[i]*.5f+.38f);
                    p.y=reach.points[i].y+.05f;
                    Rock(p,.16f+(seed%3)*.045f,rockVertices,rockTriangles,seed);
                    if(seed%2==0)
                    {
                        var g=p+right*side*.3f;g.y=reach.points[i].y+.12f;
                        for(int blade=0;blade<3;blade++)
                        {
                            float angle=(seed*37+blade*71)*Mathf.Deg2Rad;var r=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.035f;int first=grassVertices.Count;
                            var tip=g+Vector3.up*(.17f+blade*.035f)+right*.065f;
                            grassVertices.AddRange(new[]{g-r,g+r,tip,g-r,g+r,tip});
                            grassTriangles.AddRange(new[]{first,first+2,first+1,first+3,first+4,first+5});
                        }
                    }
                }
            }
            Make("Sparse bank pebbles",rockVertices,rockTriangles,palette[1]);
            Make("Small bank grasses",grassVertices,grassTriangles,Mat("Bank Grass",new Color(.37f,.43f,.25f)));
            void Make(string name,List<Vector3> v,List<int> t,Material material)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh=SavedMesh(name,mesh);
                var go=new GameObject(name);go.layer=2;go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        static void Rock(Vector3 center,float radius,List<Vector3> vertices,List<int> triangles,int seed)
        {
            int first=vertices.Count;vertices.Add(center+Vector3.up*radius*.55f);vertices.Add(center-Vector3.up*radius*.6f);
            for(int i=0;i<6;i++){float a=(i*60+seed*13)*Mathf.Deg2Rad;vertices.Add(center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius*.8f));}
            for(int i=0;i<6;i++){int a=first+2+i,b=first+2+(i+1)%6;triangles.AddRange(new[]{first,b,a,first+1,a,b});}
        }
        [MenuItem("Tools/EVERFLOW/Water/Natural River/Apply Carved Riverbeds")]
        public static void Apply()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=EFV2PuzzleBuild.Scene)throw new Exception("Open puzzle scene outside Play.");
            Directory.CreateDirectory(Folder);Directory.CreateDirectory(Reports);AssetDatabase.Refresh();
            var world=UnityEngine.Object.FindFirstObjectByType<EFV2PuzzleWorld>();var water=UnityEngine.Object.FindFirstObjectByType<EFV2WaterIntegration>();
            var filters=world.mapRoot.GetComponentsInChildren<MeshFilter>(true).Where(f=>GroundName(f.name)).ToArray();
            string backupPath=Folder+"/BeforeNaturalRiver.asset";var backup=AssetDatabase.LoadAssetAtPath<EFNaturalRiverBackup>(backupPath);
            if(backup==null)
            {
                backup=ScriptableObject.CreateInstance<EFNaturalRiverBackup>();backup.ground=filters.Select(f=>new EFNaturalRiverBackup.Ground{name=f.name,mesh=f.sharedMesh,materials=f.GetComponent<Renderer>().sharedMaterials}).ToArray();
                var source=water.segments.Single(s=>s.label=="W_H_to_G_Fall");AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.preparedMesh),Folder+"/BeforeMainWater.asset");
                backup.waterBefore=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/BeforeMainWater.asset");backup.impactsBefore=source.impactPoints.ToArray();AssetDatabase.CreateAsset(backup,backupPath);
            }
            EFWaterNetworkTools.RebuildNaturalCreek();ReadReaches(water);BuildGroundHeights(backup,filters);
            palette=new[]{Mat("Wet Gravel",new Color(.30f,.34f,.31f)),Mat("River Pebbles",new Color(.43f,.45f,.39f)),Mat("Wet Earth",new Color(.44f,.43f,.34f)),Mat("Bank Soil",new Color(.55f,.54f,.40f)),Mat("Soil Grass Transition",new Color(.64f,.66f,.50f))};
            var text=new StringBuilder("Natural river carving\n");
            foreach(var filter in filters)
            {
                var original=backup.ground.Single(g=>g.name==filter.name);var mesh=Carve(original.mesh,filter.transform,original.materials.Length,out int modified);
                if(modified==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}
                mesh=SavedMesh(filter.name+" Riverbed",mesh);filter.sharedMesh=mesh;
                var renderer=filter.GetComponent<Renderer>();renderer.sharedMaterials=original.materials.Concat(palette).ToArray();
                foreach(var collider in filter.GetComponents<MeshCollider>()){collider.sharedMesh=mesh;EditorUtility.SetDirty(collider);PrefabUtility.RecordPrefabInstancePropertyModifications(collider);}
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);text.AppendLine(filter.name+" modified triangles="+modified+" vertices="+mesh.vertexCount);
            }
            // Generated geometry is recoverable through BeforeNaturalRiver + the scene snapshot.
            // Avoid native Undo snapshots of this large FBX hierarchy during repeated generation.
            var previous=water.transform.Find("Natural River Banks");if(previous!=null)UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var root=new GameObject("Natural River Banks");root.transform.SetParent(water.transform,false);Decorations(root.transform);
            foreach(var reach in reaches)text.AppendLine(reach.name+" samples="+reach.points.Length+" bounds="+reach.bounds);
            AssetDatabase.SaveAssets();Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(world.gameObject.scene);EditorSceneManager.SaveScene(world.gameObject.scene);
            File.WriteAllText(Reports+"/geometry.txt",text.ToString());Debug.Log(text.ToString());
        }
    }
}
