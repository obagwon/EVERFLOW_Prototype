using UnityEngine;

namespace EverflowPuzzleLab
{
    public sealed class LabArea : MonoBehaviour
    {
        public PuzzleLab lab;
        public string title, objective;
        public Transform spawn, goal;
        public Transform secondaryGoal;
        public Vector3 goalSize = new Vector3(3, 3, 3);
        public Bounds bounds;
        public float recoveryHeight = -3;
        public LabGate goalGate;
        public LabCanal canal;
        public TextMesh sign;
        public bool Complete { get; private set; }
        public void Evaluate()
        {
            if (canal != null) Complete = canal.Success;
            else if ((goal != null && new Bounds(goal.position, goalSize).Contains(lab.player.transform.position) ||
                secondaryGoal != null && new Bounds(secondaryGoal.position, goalSize).Contains(lab.player.transform.position)) &&
                lab.player.GetComponent<CharacterController>().isGrounded && (goalGate == null || goalGate.IsOpen)) Complete = true;
            if (sign != null) sign.text = title + "\n" + objective + "\n" + (Complete ? "SUCCESS" : "IN PROGRESS") +
                (goalGate != null ? " | GATE " + (goalGate.Latched ? "LATCHED" : goalGate.OpenAmount.ToString("P0")) : "") + "\nR: RESET HERE  |  Backspace: SAFE RETURN";
        }
        public void ResetArea()
        {
            // Move the player before restoring geometry, including remote HUD resets.
            if (bounds.Contains(lab.player.transform.position) || lab.CurrentArea == this) lab.ReturnTo(this);
            foreach (var target in GetComponentsInChildren<LabTarget>(true)) { lab.ReleaseTarget(target); target.ResetState(); }
            foreach (var mover in GetComponentsInChildren<LabMover>(true)) mover.ResetMotion();
            foreach (var gate in GetComponentsInChildren<LabGate>(true)) gate.ResetGate();
            if (canal != null) canal.ResetCanal();
            Complete = false;
            Physics.SyncTransforms();
        }
    }
}
