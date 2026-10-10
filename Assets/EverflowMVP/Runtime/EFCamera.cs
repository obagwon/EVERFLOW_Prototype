using UnityEngine;

namespace EverflowMVP
{
    public class EFCamera : MonoBehaviour
    {
        public EFPlayer player;
        public float distance = 9;
        public float pitch = 27;
        public float yaw;
        public float sensitivity = .18f;
        Vector2 previous;
        void Start() { previous = EFInput.Pointer; Snap(true); }
        void LateUpdate()
        {
            Vector2 pointer = EFInput.Pointer;
            if (EFInput.Orbit)
            {
                Vector2 delta = pointer - previous;
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, 8, 65);
            }
            previous = pointer;
            PositionCamera();
        }
        public void Snap(bool reset)
        {
            if (reset) { yaw = 0; pitch = 27; }
            previous = EFInput.Pointer;
            PositionCamera();
        }
        void PositionCamera()
        {
            if (player == null) return;
            Vector3 focus = player.transform.position + Vector3.up * .7f;
            Vector3 offset = Quaternion.Euler(pitch, yaw, 0) * (Vector3.back * distance);
            float actual = distance;
            if (Physics.SphereCast(focus, .2f, offset.normalized, out RaycastHit hit, distance,
                ~(1 << 2), QueryTriggerInteraction.Ignore)) actual = Mathf.Max(.6f, hit.distance - .2f);
            transform.position = focus + offset.normalized * actual;
            transform.LookAt(focus);
        }
    }
}
