using EverflowPuzzleLab;
using UnityEngine;
namespace EverflowMap
{
    public sealed class EFV2TargetProxy : MonoBehaviour
    {
        public LabTarget target;
        public static LabTarget Resolve(Collider collider)
        {
            var direct=collider.GetComponentInParent<LabTarget>();
            if(direct!=null)return direct;
            var proxy=collider.GetComponentInParent<EFV2TargetProxy>();return proxy!=null?proxy.target:null;
        }
    }
}
