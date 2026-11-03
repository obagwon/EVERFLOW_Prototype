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
        TextMesh[] labels;
        void Awake() { labels=GetComponentsInChildren<TextMesh>(true); }
        void LateUpdate()
        {
            // Short scene-local labels face the gameplay camera, so a player
            // approaching the final control never sees mirrored instructions.
            if(world.player.view.enabled)
                foreach(var label in labels)
                    label.transform.rotation=Quaternion.LookRotation(label.transform.position-world.player.view.transform.position,Vector3.up);
            if(world.StageIndex!=2 || !world.GameInputActive) return;
            if(!source.SourceOn) saved=false;
            if(!saved && source.SourceOn && grounding.IsStandingOn(middle))
            { saved=true; world.SetCheckpoint(checkpoint.position); }
        }
    }
}
