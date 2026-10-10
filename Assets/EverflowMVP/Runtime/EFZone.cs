using UnityEngine;

namespace EverflowMVP
{
    public class EFZone : MonoBehaviour
    {
        public int stage;
        public bool checkpoint;
        public Transform destination;
        void OnTriggerStay(Collider other)
        {
            if (other.GetComponentInParent<EFPlayer>() == null || EFWorld.Current == null) return;
            if (checkpoint && destination != null) EFWorld.Current.SetCheckpoint(stage, destination.position);
            else if (!checkpoint) EFWorld.Current.TryComplete(stage);
        }
    }
}
