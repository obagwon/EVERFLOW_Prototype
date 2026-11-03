using UnityEngine;
namespace EverflowMap
{
    public sealed class EFMvpRotary : MonoBehaviour
    {
        public EFMapWorld world;
        public EFCanalExperiment grounding;
        public EFMapNode source;
        public BoxCollider middle;
        public Transform checkpoint;
        bool saved;
        void LateUpdate()
        {
            if(world.StageIndex!=2 || !world.GameInputActive) return;
            if(!source.SourceOn) saved=false;
            if(!saved && source.SourceOn && grounding.IsStandingOn(middle))
            { saved=true; world.SetCheckpoint(checkpoint.position); }
        }
    }
}
