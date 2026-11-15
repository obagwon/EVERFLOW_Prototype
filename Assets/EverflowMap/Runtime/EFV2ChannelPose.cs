using EverflowPuzzleLab;
using UnityEngine;

namespace EverflowMap
{
    [DefaultExecutionOrder(-90)]
    public sealed class EFV2ChannelPose : MonoBehaviour
    {
        public LabMover mover;
        public Transform model;
        public Quaternion initial, aligned;
        const float DockHold = .85f;
        const float HomeHold = .16f;
        int activeDirection = -1;
        bool outbound;
        float elapsed, duration, hold;
        Vector3 from, to;
        Quaternion fromRotation, toRotation;

        public bool Aligned => Quaternion.Angle(model.rotation,aligned)<3;

        public Quaternion DestinationRotation(int direction) => direction == 0 ? aligned : initial;

        void Update()
        {
            float dt = Time.deltaTime;
            int direction = mover.flow.Direction;
            if (dt <= 0 || mover.target.Frozen || !mover.powered || direction < 0 || direction >= mover.destinations.Length) return;

            if (direction != activeDirection)
            {
                activeDirection = direction;
                // Switch rail branches at their common home position.
                BeginLeg((mover.motion.position-mover.home).sqrMagnitude < .0001f);
            }
            else if (elapsed >= duration)
            {
                hold -= dt;
                if (hold > 0) return;
                BeginLeg(!outbound);
            }

            elapsed = Mathf.Min(elapsed + dt, duration);
            float progress = Mathf.SmoothStep(0, 1, elapsed / duration);
            mover.motion.position = Vector3.Lerp(from, to, progress);
            model.rotation = Quaternion.Slerp(fromRotation, toRotation, progress);
            if (elapsed >= duration) hold = outbound ? DockHold : HomeHold;
        }

        void BeginLeg(bool moveOut)
        {
            outbound = moveOut;
            from = mover.motion.position;
            fromRotation = model.rotation;
            to = outbound ? mover.destinations[activeDirection] : mover.home;
            toRotation = outbound ? DestinationRotation(activeDirection) : initial;
            duration = Mathf.Max(.18f, Vector3.Distance(from, to) / mover.speed);
            elapsed = 0;
        }

        public void ResetPose()
        {
            activeDirection = -1;
            elapsed = duration = hold = 0;
            model.rotation = initial;
        }
    }
}
