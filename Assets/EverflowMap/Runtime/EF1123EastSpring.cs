using UnityEngine;
using UnityEngine.Serialization;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    public sealed class EF1123EastSpring : MonoBehaviour
    {
        const float ShotSeconds = 3f;
        const float FallSeconds = .52f;
        const float WheelDrainSeconds = .42f;
        const float RaceSeconds = .72f;
        const float RaceStart = FallSeconds + WheelDrainSeconds;
        const float PondStart = RaceStart + RaceSeconds;

        public EFVine completedBridge;
        public Renderer spring, basin;
        public EFPressurePlate lever;
        public EF1123Motion sluice;
        public Transform wheel;
        public Vector3 wheelAxis = Vector3.right;
        public Renderer tailrace;
        public ParticleSystem wheelSpray;
        public Transform cameraPose, seesaw;
        [FormerlySerializedAs("floatBody")] public Transform floatWaterContact;
        public Transform[] pondLeaves;
        public float fullWaterHeight;
        public float rotationToRock;
        [Min(.1f)] public float fillSeconds = 2.65f;

        float elapsed, dryWaterHeight;
        bool released;
        Quaternion dryRotation;
        float dryContactHeight;
        Vector3[] dryLeaves;
        float flowTime, wheelSpeed;

        public bool IsFull => released && elapsed >= fillSeconds;
        internal bool CanUseLever => lever != null && lever.CanPress;
        internal bool WaterReleased => lever != null && sluice.IsOpen;

        internal bool NearLever(Vector3 position) => lever != null &&
            (position - lever.transform.position).sqrMagnitude < 16f;

        internal bool TryUseLever(EF1123World world)
        {
            if (lever == null || !lever.TryPress()) return false;
            world.effects?.ShowCutaway(cameraPose, sluice.seconds + PondStart + Mathf.Max(ShotSeconds, fillSeconds));
            return true;
        }

        void Awake()
        {
            dryWaterHeight = basin.transform.position.y;
            dryRotation = seesaw.localRotation;
            dryContactHeight = floatWaterContact.position.y;
            dryLeaves = new Vector3[pondLeaves.Length];
            for (int i = 0; i < pondLeaves.Length; i++) dryLeaves[i] = pondLeaves[i].position;
            spring.enabled = basin.enabled = false;
            if (tailrace != null) tailrace.enabled = false;
        }

        internal void Tick(EF1123World world, float delta)
        {
            if (!released)
            {
                bool sourceOpen = lever != null ? sluice.AtOpenPose :
                    completedBridge != null && completedBridge.source.IsOpen && completedBridge.walkSurface.sharedMesh != null;
                if (!sourceOpen) return;
                released = true;
                spring.enabled = true;
                if (lever == null) world.effects?.ShowCutaway(cameraPose, ShotSeconds);
                return;
            }
            if (lever != null)
            {
                flowTime = Mathf.Min(flowTime + delta, PondStart);
                if (flowTime >= FallSeconds)
                {
                    wheelSpeed = Mathf.MoveTowards(wheelSpeed, 32f, 24f * delta);
                    wheel.Rotate(wheelAxis, wheelSpeed * delta, Space.Self);
                    if (wheelSpray != null && !wheelSpray.isPlaying) wheelSpray.Play();
                }
                tailrace.enabled = flowTime >= RaceStart;
                if (flowTime < PondStart) return;
            }
            if (IsFull) return;

            elapsed = Mathf.Min(fillSeconds, elapsed + delta);
            float progress = Mathf.SmoothStep(0, 1, elapsed / fillSeconds);
            basin.enabled = progress > .012f;
            float rise = (fullWaterHeight - dryWaterHeight) * progress;
            var surface = basin.transform.position;
            surface.y = dryWaterHeight + rise;
            basin.transform.position = surface;
            // The ramp, rod, and float share the hinge transform.
            float tilt = Mathf.InverseLerp(dryContactHeight, fullWaterHeight, surface.y);
            seesaw.localRotation = dryRotation * Quaternion.Euler(rotationToRock * tilt, 0, 0);

            for (int i = 0; i < pondLeaves.Length; i++)
                pondLeaves[i].position = dryLeaves[i] + Vector3.up * rise;
        }

        internal float ArrivalFor(Renderer renderer, float existing)
        {
            if (lever == null) return existing;
            if (renderer == spring) return Mathf.Clamp01(flowTime / RaceStart);
            if (renderer == tailrace) return Mathf.Clamp01((flowTime - RaceStart) / RaceSeconds);
            return existing;
        }
    }
}
