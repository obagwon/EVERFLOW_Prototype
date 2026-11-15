using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EverflowMap
{
    // Scene-local dressing. Original walking treads, landings, triggers and moving
    // puzzle geometry stay in place. Added masonry has matching solid mesh collision.
    [DisallowMultipleComponent]
    public sealed class EFV2StoneArchitecture:MonoBehaviour
    {
        public const float CStairLandingAt=.75f;
        public struct Anchor {public Vector3 top,foot;public string surface;}
        public readonly List<Anchor> Anchors=new List<Anchor>();
        public readonly List<MeshCollider> Masonry=new List<MeshCollider>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        Material stone,trim,shade;EFV2PuzzleWorld world;Transform runtime;
        public int Flights {get;private set;}
        public int DressedDecks {get;private set;}
        public static void Build(EFV2PuzzleWorld world,Transform runtime)
        {
            var item=runtime.gameObject.AddComponent<EFV2StoneArchitecture>();item.world=world;item.runtime=runtime;item.Create();
        }
        Material Material(string name,Color color)
        {return new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};}
        Transform Find(string name)=>runtime.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        sealed class Builder
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int material)
            {int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles[material].AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,int material)
            {
                Vector3 P(float x,float y,float z)=>center+rotation*Vector3.Scale(size,new Vector3(x,y,z))*.5f;
                Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),material);
                Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),material);
                Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),material);
                Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),material);
                Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),material);
                Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),material);
            }
            // A closed, solid longitudinal ribbon. Coordinates are world-space.
            public void Band(Vector3 a,Vector3 b,float width,float topA,float topB,float bottomA,float bottomB,int material)
            {
                var right=Vector3.Cross(Vector3.up,(b-a).normalized).normalized*(width*.5f);
                var at=a+Vector3.up*topA;var bt=b+Vector3.up*topB;var ab=a+Vector3.up*bottomA;var bb=b+Vector3.up*bottomB;
                Quad(at-right,bt-right,bt+right,at+right,material);
                Quad(ab+right,bb+right,bb-right,ab-right,material);
                Quad(ab-right,bb-right,bt-right,at-right,material);
                Quad(bb+right,ab+right,at+right,bt+right,material);
                Quad(ab+right,ab-right,at-right,at+right,material);
                Quad(bb-right,bb+right,bt+right,bt-right,material);
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.subMeshCount=3;
                for(int i=0;i<3;i++)mesh.SetTriangles(triangles[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
            }
        }
        void Install(Transform parent,Builder builder)
        {
            var go=new GameObject("Ruins masonry / "+parent.name);go.transform.SetParent(parent,true);
            // Builders operate in world space, including inactive conditional routes.
            go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var mesh=builder.Mesh(go.name);ownedMeshes.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials=new[]{stone,trim,shade};
            var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Masonry.Add(collider);
        }
        bool Ground(Vector3 p,out RaycastHit hit)
        {
            hit=default;
            foreach(var h in Physics.RaycastAll(p+Vector3.up*.1f,Vector3.down,18,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                if(h.collider.transform.IsChildOf(world.mapRoot)&&h.point.y<p.y-.2f&&h.normal.y>.35f){hit=h;return true;}
            return false;
        }
        void Pier(Builder mesh,Vector3 top,float width,float depth,Quaternion rotation)
        {
            if(!Ground(top,out var ground))return;
            float bottom=ground.point.y-.12f,height=top.y-bottom;if(height<.3f)return;
            // Stacked courses are slightly recessed; no new walkable cap exceeds the old deck.
            int courses=Mathf.Max(1,Mathf.CeilToInt(height/.8f));
            for(int i=0;i<courses;i++)
            {float h=height/courses;mesh.Box(new Vector3(top.x,bottom+h*(i+.5f),top.z),new Vector3(width*(i==0?1.06f:1),h,depth),rotation,i%3==0?2:0);}
            mesh.Box(top-Vector3.up*.12f,new Vector3(width+.16f,.24f,depth+.12f),rotation,1);
            Anchors.Add(new Anchor{top=top,foot=ground.point,surface=ground.collider.name});
        }
        void Flight(string name,Vector3 from,Vector3 to,float width,bool piers=true,bool parapets=true)
        {
            var group=Find(name);var mesh=new Builder();var direction=Vector3.ProjectOnPlane(to-from,Vector3.up).normalized;var rotation=Quaternion.LookRotation(direction);var right=rotation*Vector3.right;
            foreach(var renderer in group.GetComponentsInChildren<MeshRenderer>(true))renderer.sharedMaterial=stone;
            float landingAt=name=="C ascent to ruin"?CStairLandingAt:1;
            Vector3 Point(float t){var p=Vector3.Lerp(from,to,t);p.y=Mathf.Lerp(from.y,to.y,Mathf.Min(1,t/landingAt));return p;}
            void Bands(float start,float end,Vector3 offset,float bandWidth,float top,float bottom,int material)
            {
                if(landingAt<1&&start<landingAt&&end>landingAt)
                {mesh.Band(Point(start)+offset,Point(landingAt)+offset,bandWidth,top,top,bottom,bottom,material);mesh.Band(Point(landingAt)+offset,Point(end)+offset,bandWidth,top,top,bottom,bottom,material);}
                else mesh.Band(Point(start)+offset,Point(end)+offset,bandWidth,top,top,bottom,bottom,material);
            }
            // Continuous stone soffit fills the floating zigzag without changing the treads.
            Bands(0,1,Vector3.zero,width-.01f,-.21f,-1.05f,0);
            if(parapets)foreach(float side in new[]{-1f,1f})
            {
                var offset=right*side*(width*.5f+.115f);
                float inset=Mathf.Min(.16f,.7f/Vector3.ProjectOnPlane(to-from,Vector3.up).magnitude);
                float end=name=="D3 workbench to D5 ledge"?.76f:1-inset;
                Bands(inset,end,offset,.20f,.43f,-.7f,0);
                Bands(inset,end,offset,.26f,.52f,.40f,1);
            }
            if(piers)foreach(float f in new[]{.15f,.52f,.85f})Pier(mesh,Point(f)-Vector3.up*.92f,width*.72f,.85f,rotation);
            if(name=="Safe exit gorge corridor")
            {
                foreach(float side in new[]{-1f,1f})
                {
                    var offset=Vector3.right*(side*1.46f);
                    Bands(.014f,.988f,offset,.22f,1.04f,-.23f,0);
                    Bands(.014f,.988f,offset,.30f,1.14f,1.04f,1);
                }
                // The upper descent hugs the hall's exterior wall. Tie the stone stringer
                // into that existing wall instead of inventing piers ending in the void.
                foreach(float f in new[]{.08f,.20f,.30f})
                {
                    var top=Vector3.Lerp(from,to,f)-Vector3.up*.7f;
                    var hits=Physics.RaycastAll(top+Vector3.down*.65f,Vector3.right,5,~(1<<2),QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance);
                    var hit=hits.FirstOrDefault(h=>h.collider.transform.IsChildOf(world.mapRoot));if(hit.collider==null)continue;
                    var end=hit.point+Vector3.right*.18f;float distance=end.x-top.x;
                    mesh.Box((top+end)*.5f-Vector3.up*.35f,new Vector3(distance,.7f,.8f),Quaternion.identity,0);
                    Anchors.Add(new Anchor{top=top,foot=hit.point,surface=hit.collider.name});
                }
            }
            Install(group,mesh);Flights++;
        }
        void Deck(string name,bool raised=false)
        {
            var deck=Find(name);var box=deck.GetComponent<BoxCollider>();var bounds=box.bounds;
            // Inactive conditional deck bounds are empty; read the unchanged local box.
            if(bounds.size==Vector3.zero)bounds=new Bounds(deck.TransformPoint(box.center),Vector3.Scale(box.size,deck.lossyScale));
            deck.GetComponent<MeshRenderer>().sharedMaterial=stone;var mesh=new Builder();
            var p=bounds.center;float top=bounds.max.y;
            mesh.Box(new Vector3(p.x,top-.45f,p.z),new Vector3(bounds.size.x,.26f,bounds.size.z),Quaternion.identity,0);
            // Cornice is below the original top, so no new lip appears at the joint.
            mesh.Box(new Vector3(p.x,top-.29f,p.z),new Vector3(bounds.size.x+.08f,.12f,bounds.size.z+.08f),Quaternion.identity,1);
            if(raised)
            {
                bool alongX=bounds.size.x>bounds.size.z;float length=alongX?bounds.size.x:bounds.size.z;
                foreach(float f in new[]{.12f,.88f})
                {
                    var anchor=new Vector3(p.x,top-.5f,p.z)+(alongX?Vector3.right:Vector3.forward)*(f-.5f)*length;
                    // Leave the leaf's arriving front edge unaltered on C's high ledge.
                    if(name=="C visible high landing")anchor.z=bounds.min.z+.48f;
                    Pier(mesh,anchor,alongX?.75f:bounds.size.x*.8f,alongX?bounds.size.z*.8f:.75f,Quaternion.identity);
                }
            }
            Install(deck,mesh);DressedDecks++;
        }
        void SwitchingBridge()
        {
            var deck=Find("B-C intermediate switching walkway");deck.GetComponent<MeshRenderer>().sharedMaterial=stone;var mesh=new Builder();
            // Pier stations avoid the natural creek's crossing near x=-35.
            float[] stations={-60.5f,-55,-49.5f,-44,-39,-31.5f,-27.5f};const float surface=6.1f;
            var feet=new float[stations.Length];
            for(int i=0;i<stations.Length;i++)
            {
                var p=new Vector3(stations[i],surface-.38f,-40);feet[i]=Ground(p,out var hit)?hit.point.y:surface-1;
                Pier(mesh,p,.7f,1.85f,Quaternion.identity);
            }
            for(int span=0;span<stations.Length-1;span++)
            {
                float a=stations[span],b=stations[span+1];const int count=14;
                float spring=Mathf.Min(surface-.9f,Mathf.Max(feet[span],feet[span+1])+.45f);
                float Soffit(float u)=>Mathf.Lerp(spring,surface-.7f,Mathf.Sqrt(Mathf.Max(0,1-Mathf.Pow(u*2-1,2))));
                for(int j=0;j<count;j++)
                {
                    float u=j/(float)count,v=(j+1)/(float)count;
                    var p=new Vector3(Mathf.Lerp(a,b,u),0,-40);var q=new Vector3(Mathf.Lerp(a,b,v),0,-40);
                    mesh.Band(p,q,1.96f,surface-.36f,surface-.36f,Soffit(u),Soffit(v),0);
                    // Distinct voussoirs on the two visible arch faces.
                    foreach(float side in new[]{-1f,1f})mesh.Band(p+Vector3.forward*side*.99f,q+Vector3.forward*side*.99f,.045f,Soffit(u)+.22f,Soffit(v)+.22f,Soffit(u),Soffit(v),j%2==0?1:2);
                }
            }
            foreach(float side in new[]{-1f,1f})
            {
                // Open approaches accommodate the existing diagonal A/B junction.
                var a=new Vector3(stations[0]+2,surface,-40+side*1.13f);var b=new Vector3(stations[stations.Length-1]-2,surface,a.z);
                mesh.Band(a,b,.20f,.52f,.52f,-.36f,-.36f,0);mesh.Band(a,b,.27f,.64f,.64f,.50f,.50f,1);
            }
            Install(deck,mesh);DressedDecks++;
        }
        void Create()
        {
            stone=Material("EVERFLOW ruins / limestone",new Color(.56f,.57f,.53f));trim=Material("EVERFLOW ruins / coping",new Color(.69f,.69f,.62f));shade=Material("EVERFLOW ruins / courses",new Color(.46f,.49f,.46f));
            Flight("B ascent to ruin",new Vector3(-25,6.1f,-52),new Vector3(-34.5f,15.15f,-58.8f),2.8f);
            Flight("C ascent to ruin",new Vector3(-61,11.9f,-60),new Vector3(-57,15.15f,-62),2.4f);
            Flight("D3 workbench to D5 ledge",EFV2UndergroundWorks.D5StairFoot,EFV2UndergroundWorks.D5StairTop,2.4f);
            Flight("D5 ledge to bypass access",new Vector3(-70.1f,-5.95f,-65),new Vector3(-74,-8.9f,-64.8f),2.3f);
            Flight("D3 return stairs",new Vector3(-45.15f,-7.45f,-59.2f),new Vector3(-50,-11.5f,-59.2f),2.4f);
            // These two short ramps already sit on ledges: no foundation underneath
            // the channel crossing, and no new walls near the moving alignment puzzle.
            Flight("D5 service bridge west ramp",new Vector3(-58.6f,-5.95f,-63.9f),new Vector3(-57.6f,-5.35f,-63.9f),2.4f,false,false);
            Flight("D5 service bridge east ramp",new Vector3(-54.4f,-5.35f,-63.9f),new Vector3(-53.4f,-5.95f,-63.9f),2.4f,false,false);
            Flight("Safe exit gorge corridor",new Vector3(-82.3f,-11.9f,-47),new Vector3(-82.3f,-17.35f,-12),2.7f,false,false);
            foreach(var r in runtime.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Corridor guard"))r.sharedMaterial=stone;
            foreach(string name in new[]{"C visible high landing","C ruin edge link","G door approach","D5 west service walkway","D5 east service walkway"})Deck(name,true);
            foreach(string name in new[]{"D3 return edge landing","D3 return perimeter","D3 return to hub","D2 to D4 dry perimeter","D2 perimeter connector","D4 west perimeter connector","D4 dry control approach","D4 control landing","D4 threshold - follows opened gate passage","Exit outer landing"})Deck(name);
            SwitchingBridge();Physics.SyncTransforms();
        }
        void OnDestroy()
        {foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);foreach(var material in new[]{stone,trim,shade})if(material!=null)Destroy(material);}
    }
}
