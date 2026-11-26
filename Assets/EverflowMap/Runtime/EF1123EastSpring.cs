using UnityEngine;
using UnityEngine.Serialization;

namespace EverflowMap
{
    [DisallowMultipleComponent]
    public sealed class EF1123EastSpring : MonoBehaviour
    {
        const float ShotSeconds = 3f;

        public EFVine completedBridge;
        public Renderer spring, basin;
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

        public bool IsFull => released && elapsed >= fillSeconds;

        void Awake()
        {
            dryWaterHeight = basin.transform.position.y;
            dryRotation = seesaw.localRotation;
            dryContactHeight = floatWaterContact.position.y;
            dryLeaves = new Vector3[pondLeaves.Length];
            for (int i = 0; i < pondLeaves.Length; i++) dryLeaves[i] = pondLeaves[i].position;
            spring.enabled = basin.enabled = false;
        }

        internal void Tick(EF1123World world, float delta)
        {
            if (!released)
            {
                // The walk mesh is assigned only when the vine reaches its complete length.
                if (!completedBridge.source.IsOpen || completedBridge.walkSurface.sharedMesh == null) return;
                released = true;
                spring.enabled = true;
                world.effects?.ShowCutaway(cameraPose, ShotSeconds);
                return;
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
    }
}
