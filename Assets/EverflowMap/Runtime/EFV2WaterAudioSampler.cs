using System.Collections.Generic;
using UnityEngine;

namespace EverflowMap
{
    // A curved ribbon's bounds contain dry land too. Only its audible top triangles
    // participate; the closed volume's underwater skirt must not become a source.
    public sealed class EFV2WaterAudioSampler
    {
        public struct Sample
        {
            public bool valid;
            public Vector3 point;
            public float distance, gain;
            public string label;
        }
        sealed class Surface
        {
            public Mesh mesh;
            public Vector3[] local, world;
            public int[] triangles;
            public Matrix4x4 matrix;
            public bool transformed;
        }
        readonly Dictionary<MeshFilter,Surface> surfaces=new Dictionary<MeshFilter,Surface>();
        Surface Get(MeshFilter filter)
        {
            var mesh=filter.sharedMesh;
            if(mesh==null||!mesh.isReadable)return null;
            if(!surfaces.TryGetValue(filter,out var surface)||surface.mesh!=mesh)
            {
                var indices=mesh.triangles;var uv=mesh.uv2;var top=new List<int>(indices.Length);
                for(int i=0;i<indices.Length;i+=3)
                    if(uv.Length!=mesh.vertexCount||(uv[indices[i]].y<.5f&&uv[indices[i+1]].y<.5f&&uv[indices[i+2]].y<.5f))
                    {top.Add(indices[i]);top.Add(indices[i+1]);top.Add(indices[i+2]);}
                surface=new Surface{mesh=mesh,local=mesh.vertices,world=new Vector3[mesh.vertexCount],triangles=top.ToArray()};
                surfaces[filter]=surface;
            }
            var matrix=filter.transform.localToWorldMatrix;
            if(!surface.transformed||surface.matrix!=matrix)
            {
                for(int i=0;i<surface.local.Length;i++)surface.world[i]=matrix.MultiplyPoint3x4(surface.local[i]);
                surface.matrix=matrix;surface.transformed=true;
            }
            return surface;
        }
        public Sample Query(EFV2WaterIntegration water,Vector3 ear,float radius,float shallowGain)
        {
            var best=new Sample{distance=radius};
            if(water==null||radius<=0)return best;
            foreach(var segment in water.segments)
            {
                if(!segment.wet||segment.frozen||segment.suppressPresentation||segment.speed<=0||segment.kind==EFV2WaterKind.Foam||
                    segment.renderer==null||!segment.renderer.enabled||!segment.renderer.gameObject.activeInHierarchy||segment.filter==null||
                    segment.renderer.bounds.SqrDistance(ear)>=radius*radius)continue;
                var surface=Get(segment.filter);if(surface==null)continue;
                var v=surface.world;var t=surface.triangles;
                for(int i=0;i<t.Length;i+=3)
                {
                    var a=v[t[i]];var b=v[t[i+1]];var c=v[t[i+2]];
                    var normal=Vector3.Cross(b-a,c-a);if(normal.sqrMagnitude<1e-12f)continue;
                    var point=Closest(ear,a,b,c);float distance=Vector3.Distance(point,ear);if(distance>=radius)continue;
                    float vertical=1-Mathf.Abs(normal.normalized.y);
                    float strength=Mathf.Lerp(shallowGain,1,vertical);
                    if(segment.direction.sqrMagnitude<.001f)strength*=.25f;
                    float gain=(1-Mathf.SmoothStep(0,1,distance/radius))*strength;
                    if(gain>best.gain)best=new Sample{valid=true,point=point,distance=distance,gain=gain,label=segment.label};
                }
            }
            return best;
        }
        // Closest point on a triangle, including edges and vertices (Ericson).
        static Vector3 Closest(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
        {
            var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
            if(d1<=0&&d2<=0)return a;
            var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0&&d4<=d3)return b;
            float vc=d1*d4-d3*d2;if(vc<=0&&d1>=0&&d3<=0)return a+ab*(d1/(d1-d3));
            var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0&&d5<=d6)return c;
            float vb=d5*d2-d1*d6;if(vb<=0&&d2>=0&&d6<=0)return a+ac*(d2/(d2-d6));
            float va=d3*d6-d5*d4;if(va<=0&&d4-d3>=0&&d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
            float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
        }
    }
}
