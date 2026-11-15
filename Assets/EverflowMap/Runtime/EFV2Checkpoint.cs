using EverflowPuzzleLab;
using UnityEngine;

namespace EverflowMap
{
    public sealed class EFV2Checkpoint : MonoBehaviour
    {
        public EFV2PuzzleWorld world;
        public string label;
        public Vector3 safePosition;
        public bool workbench, garden;
        public LabMover localMover;
        void OnTriggerStay(Collider other)
        {
            if(other.GetComponentInParent<EFMapPlayer>()!=world.player) return;
            if(!world.player.GetComponent<CharacterController>().isGrounded) return;
            world.Reach(this);
        }
    }
}
