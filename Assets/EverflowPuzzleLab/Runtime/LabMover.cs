using UnityEngine;

namespace EverflowPuzzleLab
{
    // Bounded rails avoid rigidbody jitter. Freeze only pauses, never resets the pose.
    [DefaultExecutionOrder(-100)]
    public sealed class LabMover : MonoBehaviour
    {
        public LabTarget target, flow;
        public Transform motion, rotor;
        public Vector3 home;
        public Vector3[] destinations = new Vector3[0];
        public float speed = 1.2f, rotorSpeed = 60;
        [Range(.05f, 1)] public float smallReach = 1;
        [Range(.05f, 1)] public float smallSpeed = .3f;
        public bool oscillate;
        public bool powered = true;
        bool returning;
        public bool AtDestination => flow != null && flow.Direction >= 0 && flow.Direction < destinations.Length &&
            Vector3.Distance(motion.position, destinations[flow.Direction]) < .45f;
        void Update() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if (motion == null || target == null || flow == null || target.Frozen || !powered || flow.Direction < 0 || flow.Direction >= destinations.Length) return;
            bool expanded = !target.acceptsBloom || target.Grown;
            Vector3 end = Vector3.Lerp(home, destinations[flow.Direction], expanded ? 1 : smallReach);
            Vector3 goal = oscillate && returning ? home : end;
            motion.position = Vector3.MoveTowards(motion.position, goal, speed * (expanded ? 1 : smallSpeed) * dt);
            if (oscillate && Vector3.Distance(motion.position, goal) < .01f) returning = !returning;
            if (rotor != null) rotor.Rotate(Vector3.forward, rotorSpeed * dt, Space.Self);
        }
        public void ResetMotion() { returning = false; motion.position = home; if (rotor != null) rotor.localRotation = Quaternion.identity; }
    }
}
