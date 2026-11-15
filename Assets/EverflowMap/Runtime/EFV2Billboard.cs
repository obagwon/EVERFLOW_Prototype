using UnityEngine;
using EverflowPuzzleLab;
namespace EverflowMap
{
    public sealed class EFV2Billboard : MonoBehaviour
    {
        public Camera view;
        public float maxDistance=32;
        public LabTarget target;
        public EFV2PuzzleWorld world;
        Renderer visual;
        void Awake() {visual=GetComponent<Renderer>();}
        void LateUpdate()
        {
            if(view==null) return;
            if(world!=null)
            {
                var feedback=world.GetComponent<EFDemoFeedback>();
                if(feedback!=null&&feedback.hideWorldDebugLabels){if(visual!=null)visual.enabled=false;return;}
            }
            if(visual!=null)
            {
                Vector3 delta=transform.position-view.transform.position;
                visual.enabled=delta.magnitude<maxDistance && !Physics.Raycast(view.transform.position,delta.normalized,Mathf.Max(0,delta.magnitude-.15f),~(1<<2),QueryTriggerInteraction.Ignore);
                if(target!=null && world!=null && world.Hovered!=target && world.PendingFlow!=target)visual.enabled=false;
            }
            transform.rotation=view.transform.rotation;
        }
    }
}
