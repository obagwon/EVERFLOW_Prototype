using EverflowPuzzleLab;
using UnityEngine;

namespace EverflowMap
{
    public sealed class EF1123Target : MonoBehaviour
    {
        public LabAbility ability;
        public string title, solution;
        public int course, order;
        public Transform station;
        public float stationRadius = 5;
        public EF1123Motion timedPlatform, requiredMotion;
        public EF1123Motion[] motions;
        public GameObject[] reveal, hide;
        public bool grabFirst;
        public Transform flyTo;
        public bool tutorial;
        public Vector3 windDirection = Vector3.forward;
        [Tooltip("Visible device meshes used for aiming. Leave empty for direction-only devices.")]
        public Renderer[] aimModels = System.Array.Empty<Renderer>();
        [Tooltip("Accept wind toward the first motion's destination when wind pushes this object along a path.")]
        public bool windMovesFirstMotion;
        [Min(0), Tooltip("Physical FLOW receivers use this radius and the target's forward direction. Zero uses regular FLOW targeting.")]
        public float windLaneRadius;
    }
}

