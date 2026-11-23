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
    }
}

