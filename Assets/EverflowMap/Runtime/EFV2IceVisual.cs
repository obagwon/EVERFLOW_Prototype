using EverflowPuzzleLab;
using UnityEngine;
namespace EverflowMap
{
    public sealed class EFV2IceVisual : MonoBehaviour
    {
        public LabTarget target;
        public Renderer ice;
        void LateUpdate() {ice.enabled=target.Frozen;}
    }
}
