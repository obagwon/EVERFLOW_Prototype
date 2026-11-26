using System;
using UnityEngine;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    public sealed class EF1123WaterPresentation : MonoBehaviour
    {
        [Serializable]
        public sealed class Surface
        {
            public Renderer renderer;
            public bool standingWater;
            [Min(0)] public float animationRate = 1f;
            [Range(0, 1)] public float foam = .24f;
            public Renderer impactSource;
            public Vector3 localImpactPoint;
            [Range(0, 1)] public float impactStrength = .5f;
            public Renderer tailReceiver;
            [Tooltip("The world heights at the start and end of the falling tail.")]
            public Vector2 tailHeights;
            [Range(0, 1)] public float tailStartProgress;
        }

        [Serializable]
        public sealed class OverflowBasin
        {
            public EF1123Motion catchment;
            public GameObject iceSeal;
            public Renderer feed, inlet, tank, spill, pond;
            public Renderer catchmentStem, openLeak;
            public GameObject uncaughtFall;
            [Min(.01f)] public float catchmentStemHeight;
            public Transform floatingLeaves;
            public Transform ferryBuoyancy;
            public EF1123Target ferryTarget;
            [Tooltip("World-space movement from the dry bed to the ferry's boarding position at full depth.")]
            public Vector3 ferryRise;
            [Tooltip("Water heights while leaking and with the leak sealed, in metres above the scene origin.")]
            public Vector2 tankLevels;
            public float pondLevel;
            public float leakSill;
            public float spillCrest;
            public float dryLeafWaterline;
            public float ferryBedWaterline;

            float bloomTime = -1f, spillTime = -1f, sealTime = -1f;
            float dryTank, dryPond, dryLeaves;
            Vector3 dryFerry;

            internal bool WaitingForFerryWater(EF1123Target target) => target == ferryTarget && ferryBuoyancy &&
                (!Visible(pond) || pond.transform.position.y < pondLevel - .05f);

            public void Apply(float time)
            {
                if (!catchment || !catchment.IsOpen) return;
                if (catchmentStem != null)
                {
                    // Scale the stalk as the same motion lifts its leaf.
                    var stalk = catchmentStem.transform;
                    float height = catchment.transform.position.y - stalk.position.y;
                    stalk.localScale = new Vector3(1f, Mathf.Clamp01(height / catchmentStemHeight), 1f);
                    catchmentStem.enabled = height > .14f;
                }
                if (!catchment.AtOpenPose) return;
                if (bloomTime < 0f)
                {
                    bloomTime = time;
                    dryTank = tank.transform.position.y;
                    dryPond = pond.transform.position.y;
                    dryLeaves = floatingLeaves.position.y;
                    if (ferryBuoyancy) dryFerry = ferryBuoyancy.position;
                }

                float elapsed = time - bloomTime;
                feed.enabled = elapsed >= .22f;
                if (feed.enabled && uncaughtFall != null && uncaughtFall.activeSelf)
                    uncaughtFall.SetActive(false);
                inlet.enabled = elapsed >= .56f;
                bool sealedDrain = iceSeal.activeInHierarchy;
                if (sealTime < 0f && sealedDrain) sealTime = time;
                float sealedFill = sealTime < 0f ? 0f : Fill(time - sealTime, 2.2f);
                float tankTop = Mathf.Lerp(dryTank, Mathf.Lerp(tankLevels.x, tankLevels.y, sealedFill),
                    Fill(elapsed - .56f, 1.9f));
                tank.enabled = inlet.enabled;
                SetHeight(tank.transform, tankTop);
                if (openLeak != null)
                {
                    bool leaking = tank.enabled && !sealedDrain && tankTop >= leakSill;
                    if (openLeak.gameObject.activeSelf != leaking) openLeak.gameObject.SetActive(leaking);
                }

                if (sealedDrain && spillTime < 0f && tankTop > spillCrest + .025f) spillTime = time;
                spill.enabled = spillTime >= 0f;
                float pondTime = time - spillTime - .42f;
                pond.enabled = spill.enabled && pondTime >= 0f;
                if (!pond.enabled) return;

                float pondTop = Mathf.Lerp(dryPond, pondLevel, Fill(pondTime, 2.75f));
                SetHeight(pond.transform, pondTop);
                SetHeight(floatingLeaves, dryLeaves + Mathf.Max(0f, pondTop - dryLeafWaterline));
                if (ferryBuoyancy)
                {
                    float floatProgress = Mathf.InverseLerp(ferryBedWaterline, pondLevel, pondTop);
                    ferryBuoyancy.position = dryFerry + ferryRise * floatProgress;
                }
            }

            static float Fill(float elapsed, float duration) => Mathf.SmoothStep(0f, 1f, elapsed / duration);

            static void SetHeight(Transform target, float height)
            {
                var position = target.position;
                if (position.y == height) return;
                position.y = height;
                target.position = position;
            }
        }

        static readonly int TimeId = Shader.PropertyToID("_WaterTime");
        static readonly int FlowId = Shader.PropertyToID("_FlowOffset");
        static readonly int WorldUvId = Shader.PropertyToID("_WorldUV");
        static readonly int FoamId = Shader.PropertyToID("_FoamAmount");
        static readonly int ImpactId = Shader.PropertyToID("_Impact0");
        static readonly int ArrivalId = Shader.PropertyToID("_Arrival");

        public Surface[] surfaces = Array.Empty<Surface>();
        public OverflowBasin overflowBasin;
        MaterialPropertyBlock properties;

        internal void Apply(float time)
        {
            overflowBasin?.Apply(time);
            properties ??= new MaterialPropertyBlock();
            foreach (var surface in surfaces)
            {
                var renderer = surface.renderer;
                if (!Visible(renderer)) continue;

                renderer.GetPropertyBlock(properties);
                properties.SetFloat(TimeId, time * surface.animationRate);
                properties.SetVector(FlowId, Vector4.zero);
                properties.SetFloat(WorldUvId, surface.standingWater ? 1f : 0f);
                properties.SetFloat(FoamId, surface.foam);
                properties.SetFloat(ArrivalId, Visible(surface.tailReceiver)
                    ? Mathf.Lerp(surface.tailStartProgress, 1f, Mathf.InverseLerp(
                        surface.tailHeights.x, surface.tailHeights.y, surface.tailReceiver.bounds.center.y - .018f))
                    : 1f);

                // The impact follows the receiving surface when the B basin rises.
                var point = renderer.transform.TransformPoint(surface.localImpactPoint);
                float strength = Visible(surface.impactSource) ? surface.impactStrength : 0f;
                properties.SetVector(ImpactId, new Vector4(point.x, point.y, point.z, strength));
                renderer.SetPropertyBlock(properties);
            }
        }

        static bool Visible(Renderer renderer) =>
            renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
    }
}
