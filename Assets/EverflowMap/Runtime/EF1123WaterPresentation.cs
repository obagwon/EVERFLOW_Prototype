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
            public Transform catchmentFallTail, catchmentFallContact;
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
            Mesh curvedFall;
            Vector3[] fallVertices;
            Vector2[] fallUv;
            Vector3 lastFallContact;

            const int FallRows = 33;
            const int FallColumns = 5;

            internal bool WaitingForFerryWater(EF1123Target target) => target == ferryTarget && ferryBuoyancy &&
                (!Visible(pond) || pond.transform.position.y < pondLevel - .05f);

            public void Apply(float time)
            {
                bool followsLeaf = catchmentFallTail && catchmentFallContact;
                if (followsLeaf) UpdateCatchmentFall();
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
                bool redirected = elapsed >= .22f;
                feed.enabled = !followsLeaf && redirected;
                if (!followsLeaf && redirected && uncaughtFall != null && uncaughtFall.activeSelf)
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

            void UpdateCatchmentFall()
            {
                Vector3 contact = catchmentFallContact.position;
                if (curvedFall == null)
                {
                    fallVertices = new Vector3[FallRows * FallColumns];
                    fallUv = new Vector2[fallVertices.Length];
                    curvedFall = CreateCatchmentFall(catchmentFallTail, contact, fallVertices, fallUv);
                    curvedFall.hideFlags = HideFlags.DontSave;
                    curvedFall.MarkDynamic();
                    catchmentFallTail.GetComponent<MeshFilter>().sharedMesh = curvedFall;
                    lastFallContact = contact;
                }
                else if ((contact - lastFallContact).sqrMagnitude > .0000001f)
                {
                    ShapeFall(curvedFall, catchmentFallTail, contact, fallVertices, fallUv);
                    lastFallContact = contact;
                }
            }

            internal void DisposeFall()
            {
                if (curvedFall != null) UnityEngine.Object.Destroy(curvedFall);
            }

            internal static Mesh CreateCatchmentFall(Transform outlet, Vector3 contact,
                Vector3[] vertices = null, Vector2[] uv = null)
            {
                vertices ??= new Vector3[FallRows * FallColumns];
                uv ??= new Vector2[vertices.Length];
                var mesh = new Mesh { name = "B curved catchment fall" };
                var colors = new Color[vertices.Length];
                var triangles = new int[(FallRows - 1) * (FallColumns - 1) * 6];
                int i = 0;
                for (int row = 0; row < FallRows; row++)
                for (int col = 0; col < FallColumns; col++)
                {
                    int index = row * FallColumns + col;
                    colors[index] = new Color(1f, 0f, 0f, col == 0 || col == FallColumns - 1 ? 0f : .94f);
                    if (row == FallRows - 1 || col == FallColumns - 1) continue;
                    triangles[i++] = index; triangles[i++] = index + FallColumns; triangles[i++] = index + 1;
                    triangles[i++] = index + 1; triangles[i++] = index + FallColumns; triangles[i++] = index + FallColumns + 1;
                }
                mesh.vertices = vertices;
                mesh.colors = colors;
                mesh.triangles = triangles;
                ShapeFall(mesh, outlet, contact, vertices, uv);
                return mesh;
            }

            static void ShapeFall(Mesh mesh, Transform outlet, Vector3 contact, Vector3[] vertices, Vector2[] uv)
            {
                Vector3 start = outlet.position;
                Vector3 horizontal = Vector3.ProjectOnPlane(contact - start, Vector3.up);
                Vector3 side = Vector3.Cross(Vector3.up, horizontal.normalized);
                Vector3 controlA = start + horizontal * .52f - Vector3.up * .085f;
                Vector3 controlB = contact + Vector3.up * ((start.y - contact.y) * .54f);
                Vector3 previous = start;
                float distance = 0f;
                for (int row = 0; row < FallRows; row++)
                {
                    float t = row / (float)(FallRows - 1);
                    float u = 1f - t;
                    Vector3 center = u * u * u * start + 3f * u * u * t * controlA +
                                     3f * u * t * t * controlB + t * t * t * contact;
                    distance += Vector3.Distance(center, previous);
                    previous = center;
                    for (int col = 0; col < FallColumns; col++)
                    {
                        int index = row * FallColumns + col;
                        float across = col / (float)(FallColumns - 1) * 2f - 1f;
                        Vector3 point = center + side * (across * Mathf.Lerp(.17f, .105f, t));
                        vertices[index] = outlet.InverseTransformPoint(point);
                        uv[index] = new Vector2(across * .15f, distance * .5f);
                    }
                }
                mesh.vertices = vertices;
                mesh.uv = uv;
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
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
        static readonly int SpringProfileId = Shader.PropertyToID("_SpringProfile");
        static readonly int SpringContactId = Shader.PropertyToID("_SpringContact");

        public Surface[] surfaces = Array.Empty<Surface>();
        public OverflowBasin overflowBasin;
        MaterialPropertyBlock properties;

        void OnDestroy() => overflowBasin?.DisposeFall();

        internal void Apply(float time, EF1123EastSpring eastSpring = null)
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
                float arrival = Visible(surface.tailReceiver)
                    ? Mathf.Lerp(surface.tailStartProgress, 1f, Mathf.InverseLerp(
                        surface.tailHeights.x, surface.tailHeights.y, surface.tailReceiver.bounds.center.y - .018f))
                    : 1f;
                properties.SetFloat(ArrivalId, eastSpring != null ? eastSpring.ArrivalFor(renderer, arrival) : arrival);

                // The impact follows the receiving surface when the B basin rises.
                var point = renderer.transform.TransformPoint(surface.localImpactPoint);
                float strength = Visible(surface.impactSource) ? surface.impactStrength : 0f;
                properties.SetVector(ImpactId, new Vector4(point.x, point.y, point.z, strength));
                if (eastSpring != null && eastSpring.wheelSpray != null)
                {
                    bool falling = renderer == eastSpring.spring;
                    bool race = renderer == eastSpring.tailrace;
                    if (falling || race || renderer == eastSpring.basin)
                    {
                        properties.SetVector(SpringProfileId, new Vector4(1f, falling ? 7.3f : race ? 2.72f : 0f, 0f, 0f));
                        if (falling && eastSpring.wheelSpray.isPlaying)
                        {
                            var contact = eastSpring.wheelSpray.transform.position;
                            properties.SetVector(SpringContactId, new Vector4(contact.x, contact.y, contact.z, 1f));
                        }
                    }
                }
                renderer.SetPropertyBlock(properties);
            }
        }

        static bool Visible(Renderer renderer) =>
            renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy;
    }
}
