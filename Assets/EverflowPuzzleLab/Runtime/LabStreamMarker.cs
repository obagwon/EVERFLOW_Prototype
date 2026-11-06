using UnityEngine;
namespace EverflowPuzzleLab
{
    public sealed class LabStreamMarker : MonoBehaviour
    {
        public Transform marker;
        public Vector3 localStart, localEnd;
        public float cyclesPerSecond = .4f;
        void Update() { marker.localPosition=Vector3.Lerp(localStart,localEnd,Mathf.Repeat(Time.time*cyclesPerSecond,1)); }
    }
}
