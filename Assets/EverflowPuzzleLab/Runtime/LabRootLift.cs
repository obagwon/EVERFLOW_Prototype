using UnityEngine;
namespace EverflowPuzzleLab
{
    public sealed class LabRootLift : MonoBehaviour
    {
        public LabTarget root;
        public Vector3 closed, opened;
        void LateUpdate() { transform.position=root.Grown?opened:closed; }
    }
}
