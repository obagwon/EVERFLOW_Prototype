using UnityEngine;

namespace EverflowPuzzleLab
{
    [ExecuteAlways, DefaultExecutionOrder(310)]
    public sealed class LabShortLabel : MonoBehaviour
    {
        public LabArea area;
        public Transform follow;
        public Vector3 offset;
        public float visibleDistance=9;
        TextMesh text;
        void OnEnable() { text=GetComponent<TextMesh>(); UpdateLabel(); }
        void LateUpdate() { UpdateLabel(); }
        void UpdateLabel()
        {
            if(text==null) text=GetComponent<TextMesh>();
            LabSignFont.Apply(text);
            if(follow!=null) transform.position=follow.position+offset;
            var camera=Application.isPlaying && area!=null && area.lab!=null && area.lab.player!=null ? area.lab.player.view : null;
            if(camera==null) return;
            var renderer=GetComponent<Renderer>();
            renderer.enabled=Vector3.Distance(transform.position,camera.transform.position)<=visibleDistance;
            // Short moving labels stay vertical and readable from either approach.
            var toward=Vector3.ProjectOnPlane(transform.position-camera.transform.position,Vector3.up);
            if(toward.sqrMagnitude>.01f) transform.rotation=Quaternion.LookRotation(toward);
        }
    }
}
