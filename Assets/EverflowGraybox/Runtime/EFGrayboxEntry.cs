using UnityEngine;

namespace EverflowGraybox
{
    public class EFGrayboxEntry : MonoBehaviour
    {
        public int stage;
        public EFWorld world;
        void OnTriggerStay(Collider other)
        {
            if (other.GetComponentInParent<EFPlayer>() != null) world.EnterOnFoot(stage);
        }
    }
}
