using System;
using UnityEngine;

namespace EverflowMap
{
    public enum EFV2WaterKind { Surface, Waterfall, Foam }
    public enum EFV2WaterIce { None, D5Internal, D2Basin }

    [Serializable]
    public sealed class EFV2WaterSegment
    {
        public string label;
        public MeshRenderer renderer;
        public MeshFilter filter;
        public Mesh preparedMesh, originalMesh;
        public Material[] originalMaterials;
        public EFV2WaterKind kind;
        public EFV2WaterIce ice;
        [Tooltip("World-space downstream direction. Zero = standing pool.")]
        public Vector3 direction=Vector3.forward;
        [Min(0)] public float speed=.55f;
        [Range(0,.12f)] public float waveHeight=.018f;
        [Range(0,1)] public float foam=.24f;
        [Tooltip("Use continuous distance UVs on a curved waterfall ribbon.")]
        public bool pathFlow;
        [Tooltip("Optional material for a local presentation prototype.")]
        public Material materialOverride;
        [Tooltip("Hide only the renderer when a neighbouring segment owns a continuous surface.")]
        public bool suppressPresentation;
        [Tooltip("Map-wide continuous water presentation; gameplay still owns the original objects.")]
        public bool networkSurface, networkWorldUV;
        public Vector3[] impactPoints=Array.Empty<Vector3>();
        public string[] impactSources=Array.Empty<string>();
        [Range(0,.6f)] public float arrivalDelay;
        [NonSerialized] public Vector3 offset, velocity;
        [NonSerialized] public float clock, arrival, wetFor;
        [NonSerialized] public bool wet, frozen;
        [NonSerialized] public MaterialPropertyBlock properties;
    }

    // Presentation reads the circuit-owned GameObject visibility after World.LateUpdate.
    // Never writes circuit state, target state, GameObject activity, transforms or colliders.
    [DefaultExecutionOrder(600), DisallowMultipleComponent]
    public sealed class EFV2WaterIntegration : MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        public Material waterMaterial;
        public EFV2WaterSegment[] segments=Array.Empty<EFV2WaterSegment>();
        [Tooltip("Used only by the isolated water test scene without a PuzzleWorld.")]
        public bool previewSupplied=true, previewFrozen;
        [Range(.05f,2)] public float directionSmoothing=.35f;
        [Range(.05f,1)] public float arrivalSeconds=.32f;
        bool initialized;
        float previousElapsed;
        MaterialPropertyBlock iceProperties;

        void Start(){Initialize();}
        void Initialize()
        {
            if(initialized)return;
            iceProperties=new MaterialPropertyBlock();
            foreach(var s in segments)
            {
                if(s.renderer==null||s.filter==null)continue;
                if(s.preparedMesh!=null)s.filter.sharedMesh=s.preparedMesh;
                s.renderer.sharedMaterials=new[]{s.materialOverride!=null?s.materialOverride:waterMaterial};
                s.renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                s.renderer.receiveShadows=false;
                s.properties=new MaterialPropertyBlock();
                // Pools already contain water; activation fronts belong to supplied channels.
                s.arrival=world!=null&&s.ice==EFV2WaterIce.D2Basin?1:0;
            }
            if(world!=null)
            {
                // Replace the lab's debug boxes, leaving all gameplay paths and water GOs intact.
                foreach(var marker in world.mapRoot.GetComponentsInChildren<EverflowPuzzleLab.LabStreamMarker>(true))
                    marker.enabled=false;
                foreach(var t in world.mapRoot.GetComponentsInChildren<Transform>(true))
                    if(t.name=="Moving water glint")t.gameObject.SetActive(false);
                ConfigureIce(world.upperIce?.iceSupport);
                ConfigureIce(world.mainIce?.iceSupport);
            }
            initialized=true;
        }
        void ConfigureIce(Collider support)
        {
            if(support==null)return;
            var r=support.GetComponent<Renderer>();if(r!=null)r.sharedMaterial=waterMaterial;
        }
        public void ResetVisuals()
        {
            foreach(var s in segments)
            {s.offset=s.velocity=Vector3.zero;s.clock=s.wetFor=s.arrival=0;s.wet=s.frozen=false;}
        }
        void LateUpdate()
        {
            Initialize();
            if(world!=null && !world.Built)return;
            if(world!=null && world.Elapsed<previousElapsed-.01f)ResetVisuals();
            if(world!=null)previousElapsed=world.Elapsed;
            float dt=world!=null&&world.SimulationPaused?0:Time.deltaTime;
            foreach(var s in segments)
            {
                if(s.renderer==null)continue;
                bool wet=world!=null?s.renderer.gameObject.activeInHierarchy:previewSupplied;
                bool frozen=world!=null ? s.ice==EFV2WaterIce.D2Basin&&world.mainIce.Frozen || s.ice==EFV2WaterIce.D5Internal&&world.upperIce.Frozen : previewFrozen;
                Vector3 direction=s.direction;
                float speed=s.speed;
                if(world!=null&&s.ice==EFV2WaterIce.D2Basin)
                {
                    speed=world.Supply&&world.branch.Direction>=0?s.speed:0;
                    direction=world.branch.Direction==0?Vector3.right:world.branch.Direction==1?Vector3.left:new Vector3(-1,0,.38f);
                }
                if(!wet){s.wetFor=0;s.arrival=0;}
                else if(!frozen)
                {
                    s.wetFor+=dt;
                    s.arrival=Mathf.Clamp01((s.wetFor-s.arrivalDelay)/arrivalSeconds);
                    s.velocity=Vector3.Lerp(s.velocity,direction.normalized*speed,1-Mathf.Exp(-dt/Mathf.Max(.01f,directionSmoothing)));
                    s.offset+=s.velocity*dt;
                    s.clock+=dt;
                }
                // Existing standing water must not blink dry when a scene/restart begins.
                if(wet&&(frozen||s.direction==Vector3.zero||s.ice==EFV2WaterIce.D2Basin))s.arrival=1;
                s.wet=wet;s.frozen=frozen;
                s.renderer.enabled=wet&&!s.suppressPresentation;
                Apply(s,s.renderer,false);
                if(world!=null&&s.ice!=EFV2WaterIce.None)
                {
                    var support=s.ice==EFV2WaterIce.D2Basin?world.mainIce.iceSupport:world.upperIce.iceSupport;
                    if(support!=null)Apply(s,support.GetComponent<Renderer>(),true);
                }
            }
        }
        void Apply(EFV2WaterSegment s,Renderer r,bool ice)
        {
            if(r==null)return;
            var p=ice?iceProperties:s.properties;
            p.Clear();
            p.SetVector("_FlowOffset",s.offset);
            p.SetFloat("_WaterTime",s.clock);
            p.SetFloat("_Frozen",ice?1:s.frozen?1:0);
            p.SetFloat("_Wet",ice?1:s.wet?1:0);
            p.SetFloat("_Arrival",ice?1:s.arrival);
            p.SetFloat("_WaveHeight",ice||s.kind!=EFV2WaterKind.Surface?0:s.waveHeight);
            p.SetFloat("_FoamAmount",ice?0:s.foam);
            p.SetFloat("_Vertical",s.kind==EFV2WaterKind.Waterfall?1:0);
            p.SetFloat("_FoamOnly",s.kind==EFV2WaterKind.Foam?1:0);
            p.SetFloat("_PathFlow",s.pathFlow?1:0);
            p.SetFloat("_PathSpeed",s.speed);
            p.SetFloat("_WorldUV",s.networkWorldUV?1:0);
            for(int i=0;i<3;i++)
            {
                Vector3 at=i<s.impactPoints.Length?s.impactPoints[i]:Vector3.zero;
                bool visible=i<s.impactPoints.Length;
                if(visible&&i<s.impactSources.Length&&!string.IsNullOrEmpty(s.impactSources[i]))
                {
                    visible=false;
                    foreach(var source in segments)if(source.label==s.impactSources[i])
                    {visible=source.renderer!=null&&source.renderer.gameObject.activeInHierarchy;break;}
                }
                p.SetVector("_Impact"+i,new Vector4(at.x,at.y,at.z,visible&&!s.frozen?1:0));
            }
            var b=r.bounds;
            p.SetVector("_SurfaceRight",s.kind==EFV2WaterKind.Waterfall&&b.size.z>b.size.x?Vector3.forward:Vector3.right);
            p.SetVector("_SurfaceForward",s.kind==EFV2WaterKind.Waterfall?Vector3.up:Vector3.forward);
            var dir=s.direction.normalized;
            p.SetVector("_ArrivalDirection",dir);
            p.SetVector("_SegmentCenter",b.center);
            p.SetFloat("_SegmentLength",Mathf.Max(.1f,Vector3.Dot(b.size,new Vector3(Mathf.Abs(dir.x),Mathf.Abs(dir.y),Mathf.Abs(dir.z)))));
            r.SetPropertyBlock(p);
        }
        void OnDestroy()
        {
            // Assets are never modified at runtime. Unity restores the serialized scene on Stop.
        }
    }
}
