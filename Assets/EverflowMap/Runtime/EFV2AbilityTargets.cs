using System;
using System.Collections.Generic;
using System.Linq;
using EverflowPuzzleLab;
using UnityEngine;
using UnityEngine.Rendering;

namespace EverflowMap
{
    // Presentation and exact visible-surface picking only. Never changes puzzle state or physics.
    [DefaultExecutionOrder(850), DisallowMultipleComponent]
    public sealed class EFV2AbilityTargets : MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        public static readonly Color[] Colors={new Color(.59f,1,.25f),new Color(.32f,.81f,1),new Color(1,.51f,.08f)};
        sealed class Surface
        {
            public LabTarget target;
            public Renderer source;
            public MeshFilter filter;
            public MeshRenderer overlay;
            public MeshFilter overlayFilter;
            public Mesh mesh;
            public Vector3[] vertices;
            public int[] triangles;
            public Collider[] colliders;
        }
        readonly List<Surface> surfaces=new List<Surface>();
        // FLOW and FREEZE can share a model. The latest arrival owns its one visible wave.
        readonly Dictionary<Renderer, (Surface surface, LabAbility ability, float start)> impacts = new();
        const float ImpactDuration = .55f;
        int teleportVersion;
        MaterialPropertyBlock properties;
        Material glow;
        bool initialized;
        public int VisibleHighlights => world!=null&&world.AbilityInputActive
            ? surfaces.Count(s=>s.overlay!=null&&s.overlay.enabled)
            : 0;
        public Renderer[] RenderersFor(LabTarget target)=>surfaces.Where(s=>s.target==target&&s.source!=null).Select(s=>s.source).Distinct().ToArray();
        public void Initialize()
        {
            if(initialized||world==null||!world.Built)return;
            initialized=true;
            teleportVersion=world.player.TeleportVersion;
            var template=world.presentation!=null?world.presentation.targetGlowMaterial:null;
            glow=template!=null?new Material(template):new Material(Shader.Find("EVERFLOW/Ability Target Glow"));
            glow.name="Ability target glow - one shared runtime material";
            properties=new MaterialPropertyBlock();
            var all=world.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();
            foreach(var target in world.targets)
            {
                // Bind the real devices/water, including targets previously represented only by a sphere.
                switch(target.label)
                {
                    case "A ROOT STAIRS":Named(target,all,"PH_A_Tut_WitheredPad");break;
                    case "A POND CURRENT":Named(target,all,"W_A_Pond");break;
                    case "B WINDMILL":Named(target,all,"PH_B_Windmill_Rotor","PH_B_Windmill_Pole");break;
                    case "C WIND":Named(target,all,"PH_C_LeafPlatform");break;
                    case "G WATER BRANCH":Named(target,all,"PH_G_ChannelBranch");break;
                    case "D2 THREE WAY BRANCH":Named(target,all,"PH_D2_BranchGate");break;
                    case "D5 ALIGNING WIND":Named(target,all,"PH_D5_MovingChannel");break;
                    case "D2 MAIN WATER / ICE BRIDGE":Named(target,all,"W_D2_Basin");break;
                    case "D5 INTERNAL WATER - NOT STRUCTURE":Named(target,all,"W_D5_Fixed");Named(target,all,"W_D5_ChannelInterior");break;
                }
                foreach(var r in target.indicators)Add(target,r);
                if(target.growth!=null)foreach(var r in target.growth.GetComponentsInChildren<Renderer>(true))Add(target,r);
                foreach(var go in target.grownObjects)if(go!=null)foreach(var r in go.GetComponentsInChildren<Renderer>(true))Add(target,r);
                if(target.iceSupport!=null)Add(target,target.iceSupport.GetComponent<Renderer>());
            }
        }
        void Named(LabTarget target,Renderer[] all,params string[] names)
        {foreach(var name in names)foreach(var r in all)if(r.name==name)Add(target,r);}
        void Add(LabTarget target,Renderer source)
        {
            if(source==null||source.name=="Ability focus visual"||source.name.StartsWith("Ability glow /",StringComparison.Ordinal)||source.GetComponent<LabTarget>()!=null||!(source is MeshRenderer)||surfaces.Any(s=>s.target==target&&s.source==source))return;
            var filter=source.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)return;
            var go=new GameObject("Ability glow / "+target.label);go.layer=2;
            go.transform.SetParent(source.transform,false);
            var overlayFilter=go.AddComponent<MeshFilter>();var overlay=go.AddComponent<MeshRenderer>();
            overlay.shadowCastingMode=ShadowCastingMode.Off;overlay.receiveShadows=false;overlay.enabled=false;
            var surface=new Surface{target=target,source=source,filter=filter,overlayFilter=overlayFilter,overlay=overlay,
                colliders=source.GetComponents<Collider>()};
            surfaces.Add(surface);RefreshMesh(surface);
        }
        void RefreshMesh(Surface s)
        {
            if(s.mesh==s.filter.sharedMesh)return;
            s.mesh=s.filter.sharedMesh;s.overlayFilter.sharedMesh=s.mesh;
            s.overlay.sharedMaterials=Enumerable.Repeat(glow,s.mesh.subMeshCount).ToArray();
            if(s.mesh.isReadable){s.vertices=s.mesh.vertices;s.triangles=s.mesh.triangles;}
            else
            {
                var cache=world.presentation!=null?world.presentation.targetPickingData:null;
                var entry=cache!=null?cache.entries.FirstOrDefault(e=>e.source==s.mesh):null;
                s.vertices=entry?.vertices;s.triangles=entry?.triangles;
            }
        }
        bool Shown(Surface s)=>s.source!=null&&s.source.enabled&&s.source.gameObject.activeInHierarchy&&s.filter.sharedMesh!=null;
        public bool RepresentsCollider(LabTarget target,Collider collider)=>surfaces.Any(s=>s.target==target&&s.source!=null&&s.source.transform==collider.transform);
        internal void PlayImpact(LabTarget target, LabAbility ability)
        {
            Initialize();
            foreach (var surface in surfaces)
                if (surface.target == target && surface.source != null)
                    impacts[surface.source] = (surface, ability, Time.time);
        }

        void LateUpdate()
        {
            Initialize();
            if (!initialized) return;
            if (teleportVersion != world.player.TeleportVersion)
            {
                teleportVersion = world.player.TeleportVersion;
                impacts.Clear();
            }

            bool aiming = world.AbilityInputActive;
            foreach (var surface in surfaces)
            {
                if (surface.overlay == null) continue;
                if (Shown(surface))
                {
                    if (aiming && world.CanUse(surface.target, world.Selected, out _))
                    {
                        bool hovered = world.Hovered == surface.target || world.PendingFlow == surface.target;
                        float pulse = .94f + .06f * Mathf.Sin(Time.unscaledTime * 3);
                        Show(surface, world.Selected, hovered ? 1 : .64f, pulse, -1);
                        continue;
                    }
                    if (!aiming && impacts.TryGetValue(surface.source, out var impact) && impact.surface == surface)
                    {
                        float progress = (Time.time - impact.start) / ImpactDuration;
                        if (progress < 1)
                        {
                            Show(surface, impact.ability, .85f, 1, progress);
                            continue;
                        }
                        impacts.Remove(surface.source);
                    }
                }
                surface.overlay.enabled = false;
            }
        }

        void Show(Surface surface, LabAbility ability, float strength, float pulse, float progress)
        {
            RefreshMesh(surface);
            properties.Clear();
            properties.SetColor("_GlowColor", Colors[(int)ability]);
            properties.SetFloat("_Strength", strength);
            properties.SetFloat("_Pulse", pulse);
            properties.SetFloat("_WaveProgress", progress);
            if (progress >= 0)
            {
                var bounds = surface.source.bounds;
                var origin = bounds.ClosestPoint(surface.target.transform.position);
                float radius = bounds.extents.magnitude + Vector3.Distance(origin, bounds.center);
                properties.SetVector("_WaveOrigin", new Vector4(origin.x, origin.y, origin.z, 1 / Mathf.Max(.1f, radius)));
            }
            surface.overlay.SetPropertyBlock(properties);
            surface.overlay.enabled = true;
        }
        public bool Raycast(Ray ray,float maximum,out LabTarget target,out float distance)
        {
            Initialize();target=null;distance=maximum;
            foreach(var s in surfaces)
            {
                if(!Shown(s)||!s.source.bounds.IntersectRay(ray,out float entry)||entry>distance)continue;
                RefreshMesh(s);
                float hitDistance=float.MaxValue;
                if(s.vertices!=null)
                {
                    var localOrigin=s.source.transform.InverseTransformPoint(ray.origin);
                    var localDirection=s.source.transform.InverseTransformVector(ray.direction);
                    for(int k=0;k<s.triangles.Length;k+=3)
                        if(Triangle(localOrigin,localDirection,s.vertices[s.triangles[k]],s.vertices[s.triangles[k+1]],s.vertices[s.triangles[k+2]],out float t)&&t<hitDistance)hitDistance=t;
                }
                else foreach(var c in s.colliders)if(c!=null&&c.enabled&&c.Raycast(ray,out var hit,distance))hitDistance=Mathf.Min(hitDistance,hit.distance);
                // A device may be FREEZE-able and FLOW-driven. Prefer the selected valid ability
                // on an equal-depth real surface, rather than the old floating proxy.
                if(hitDistance>0&&hitDistance<=distance+.015f&&(hitDistance<distance-.015f||s.target.Accepts(world.Selected)||target==null))
                {target=s.target;distance=hitDistance;}
            }
            return target!=null;
        }
        static bool Triangle(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            distance=0;var e1=b-a;var e2=c-a;var p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);
            if(Mathf.Abs(det)<.0000001f)return false;
            float inv=1/det;var t=origin-a;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)return false;
            var q=Vector3.Cross(t,e1);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)return false;
            distance=Vector3.Dot(e2,q)*inv;return distance>0;
        }
        void OnDisable()
        {
            impacts.Clear();
            foreach (var surface in surfaces)
                if (surface.overlay != null) surface.overlay.enabled = false;
        }
        void OnDestroy(){foreach(var s in surfaces)if(s.overlay!=null)Destroy(s.overlay.gameObject);if(glow!=null)Destroy(glow);}
    }
}
