using UnityEngine;
namespace EverflowMap
{
    public sealed class EFMapCheckpoint : MonoBehaviour
    {
        public EFMapWorld world;
        public int stage;
        public Transform destination;
        public EFMapNode[] requirements;
        bool saved;
        public void ResetIfStage(int index) { if (stage == index) saved = false; }
        void OnTriggerStay(Collider other)
        {
            if (saved || world.StageIndex != stage || other.GetComponentInParent<EFMapPlayer>() == null) return;
            foreach (var node in requirements) if (node == null || !node.Satisfied) return;
            saved = true; world.SetCheckpoint(destination.position);
        }
    }
}
