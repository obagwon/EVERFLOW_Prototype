using UnityEngine;

namespace EverflowPuzzleLab
{
    public sealed class LabGate : MonoBehaviour
    {
        public LabTarget root, flow;
        public int wheelDirection;
        public Transform door, wheel, latch;
        public Vector3 closed;
        public float lift = 3.6f, speed = 1.8f;
        public bool supplied = true;
        [SerializeField] LabGateState state = new LabGateState();
        public bool Latched => state.latched;
        public float OpenAmount => state.open;
        public bool IsOpen => Latched || OpenAmount >= .99f;
        void Update() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            bool driven = supplied && flow != null && flow.Direction == wheelDirection;
            if (driven && wheel != null) wheel.Rotate(Vector3.forward, 90 * dt);
            state.Tick(dt,driven,root != null && root.Grown,speed,lift);
            door.position = closed + Vector3.up * (OpenAmount * lift);
            if (latch != null) latch.localEulerAngles = new Vector3(0, 0, Latched ? 90 : 0);
        }
        public void ResetGate() { state.Reset(); door.position = closed; if (wheel != null) wheel.localRotation = Quaternion.identity; if (latch != null) latch.localRotation = Quaternion.identity; }
    }
}
