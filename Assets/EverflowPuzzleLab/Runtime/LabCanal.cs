using UnityEngine;

namespace EverflowPuzzleLab
{
    // D1-D5/W form one hydraulic circuit. No visit, cast count or StageIndex conditions.
    public sealed class LabCanal : MonoBehaviour
    {
        public LabMover aqueduct, dockLeaf;
        public LabTarget bypassRoot, upperIce, mainIce, branch;
        public LabGate gate;
        public Transform wheel;
        public GameObject[] connectedWater, bypassWater, dockWater, turbineWater, mainWater, gateApproachWater, exitWater, leakWater;
        public GameObject movingWater;
        public Transform[] vegetation;
        public TextMesh readout;
        public float connectionTolerance = .5f;
        [Min(.1f)] public float successSeconds = 3;
        public Vector3 connectionPoint;
        public bool Connected { get; private set; }
        public bool Supply { get; private set; }
        public bool Arriving { get; private set; }
        [SerializeField] LabArrivalState arrival = new LabArrivalState();
        public bool Success => arrival.success;
        public float StableFor => arrival.stableFor;
        public string Status => $"D5 aligned={Connected} bypass={bypassRoot.Grown} upperIce={upperIce.Frozen}\n" +
            $"SUPPLY={Supply} D2={(branch.Direction < 0 ? "UNSET" : branch.directions[branch.Direction])}\n" +
            $"D4 open={gate.IsOpen} latched={gate.Latched} MAIN ICE={mainIce.Frozen}\n" +
            $"W water={Arriving} stable={StableFor:0.0}/{successSeconds:0.0}s restored={Success}";
        void Update() { Tick(Time.deltaTime); }
        static void Show(GameObject[] list, bool show) { if (list != null) foreach (var go in list) if (go != null) go.SetActive(show); }
        public void Tick(float dt)
        {
            Connected = Vector3.Distance(aqueduct.motion.position, connectionPoint) <= connectionTolerance;
            Supply = LabCircuitRules.Supply(Connected,upperIce.Frozen,bypassRoot.Grown);
            bool dock = Supply && branch.Direction == 0;
            bool turbine = Supply && branch.Direction == 1;
            bool main = Supply && branch.Direction == 2;
            Arriving = LabCircuitRules.Arriving(Supply,branch.Direction,gate.IsOpen,mainIce.Frozen);
            gate.supplied = Supply;
            dockLeaf.powered = dock;
            Show(connectedWater, Connected && !upperIce.Frozen);
            if (movingWater != null) movingWater.SetActive(Connected && !upperIce.Frozen);
            Show(leakWater, !Connected);
            Show(bypassWater, bypassRoot.Grown);
            Show(dockWater, dock); Show(turbineWater, turbine);
            Show(mainWater, main); Show(gateApproachWater, main && !mainIce.Frozen); Show(exitWater, Arriving);
            arrival.Tick(dt,Arriving,successSeconds);
            if (Arriving && wheel != null) wheel.Rotate(Vector3.forward, dt * 80);
            foreach (var plant in vegetation) if (plant != null)
            {
                plant.localScale = Success ? new Vector3(1, 2, 1) : new Vector3(.5f, .3f, .5f);
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", Success ? Color.green : Color.gray);
                plant.GetComponent<Renderer>().SetPropertyBlock(block);
            }
            if (readout != null) readout.text = "UNDERGROUND LIVE CIRCUIT\n" + Status;
        }
        public void ResetCanal() { arrival.Reset(); if (wheel != null) wheel.localRotation = Quaternion.identity; Tick(0); }
    }
}
