using UnityEngine;

namespace EverflowMap
{
    // Local poses let an entire course be adjusted in the Scene view.
    [DisallowMultipleComponent]
    public sealed class EF1123Motion : MonoBehaviour
    {
        public Vector3 offset, turn, openScale = Vector3.one;
        [Min(.05f)] public float seconds = .8f;
        [Min(0f)] public float openDelay;
        public Vector3 sway;
        [Min(.2f)] public float period = 3.5f;
        public float alignmentTolerance = 17;
        public bool beginsOpen;
        public bool IsOpen { get; private set; }
        public bool AtOpenPose => IsOpen && value >= 1;

        Vector3 closedPosition, closedScale;
        Quaternion closedRotation;
        float value, clock, remainingDelay;
        bool initialized;

        public void Initialize()
        {
            if (initialized) return;
            closedPosition = transform.localPosition;
            closedRotation = transform.localRotation;
            closedScale = transform.localScale;
            initialized = true;
            ResetPose();
        }

        public void ResetPose()
        {
            IsOpen = beginsOpen;
            value = beginsOpen ? 1 : 0;
            clock = 0;
            remainingDelay = beginsOpen ? 0f : openDelay;
            ApplyPose();
        }

        public void Open() => IsOpen = true;

        public bool IsAligned => Mathf.Abs(Mathf.Sin(clock * Mathf.PI * 2 / period)) *
                                 sway.magnitude <= alignmentTolerance;

        public void Tick(float delta)
        {
            if (!IsOpen && sway.sqrMagnitude > 0) clock += delta;
            if (IsOpen && remainingDelay > 0f)
            {
                float waiting = Mathf.Min(delta, remainingDelay);
                remainingDelay -= waiting;
                delta -= waiting;
            }
            value = Mathf.MoveTowards(value, IsOpen ? 1 : 0, delta / seconds);
            ApplyPose();
        }

        void ApplyPose()
        {
            float blend = Mathf.SmoothStep(0, 1, value);
            float oscillation = (1 - blend) * Mathf.Sin(clock * Mathf.PI * 2 / period);
            transform.localPosition = closedPosition + offset * blend;
            transform.localRotation = closedRotation * Quaternion.Euler(turn * blend + sway * oscillation);
            transform.localScale = Vector3.Scale(closedScale, Vector3.Lerp(Vector3.one, openScale, blend));
        }
    }
}

