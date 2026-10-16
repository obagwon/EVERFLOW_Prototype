using UnityEngine;

namespace EverflowMap
{
    // The existing arrival Transform + stage.arrivalSize remain the single source
    // of geometry. World polls only the active stage, independent of trigger/Rigidbody setup.
    [DisallowMultipleComponent]
    public sealed class EFMapPassage : MonoBehaviour
    {
        public EFMapWorld world;
        [Range(0, 4)] public int stage;
        void OnDrawGizmos()
        {
            if (world == null || stage < 0 || stage >= world.stages.Length) return;
            var area = world.stages[stage];
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(.3f, 1, .6f, .15f);
            Gizmos.DrawCube(Vector3.zero, area.arrivalSize);
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, area.arrivalSize);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position, $"P{stage + 1} passage -> P{stage + 2}\nSize: {area.arrivalSize}");
#endif
        }
    }
}
