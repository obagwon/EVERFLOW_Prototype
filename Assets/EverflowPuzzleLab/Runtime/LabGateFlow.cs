using UnityEngine;
namespace EverflowPuzzleLab
{
    public sealed class LabGateFlow : MonoBehaviour
    {
        public LabTarget flow;
        public GameObject[] wheelWater, spillWater;
        void Update()
        {
            foreach(var go in wheelWater) go.SetActive(flow.Direction==0);
            foreach(var go in spillWater) go.SetActive(flow.Direction==1);
        }
    }
}
