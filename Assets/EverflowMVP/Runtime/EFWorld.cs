using System;
using System.Collections.Generic;
using UnityEngine;

namespace EverflowMVP
{
    [Serializable]
    public class EFStage
    {
        public string title;
        public string hint;
        public Transform spawn;
        public EFNode[] nodes = new EFNode[0];
        public EFNode[] goalRequirements = new EFNode[0];
    }
    [DefaultExecutionOrder(-200)]
    public class EFWorld : MonoBehaviour
    {
        public static EFWorld Current { get; private set; }
        public EFPlayer player;
        public EFStage[] stages = new EFStage[0];
        public bool debugStageSelection = true;
        [Range(0, 5)] public int startStage;
        public int StageIndex { get; private set; }
        public string Message { get; set; } = "BLOOM / FREEZE / PULSE";
        public bool[] Completed { get; private set; }
        public Vector3 Checkpoint { get; private set; }
        readonly Queue<EFNode> pending = new Queue<EFNode>();
        readonly HashSet<EFNode> visited = new HashSet<EFNode>();
        void Awake() { Current = this; Completed = new bool[stages.Length]; }
        void Start() { EnterStage(startStage); }
        void OnDestroy() { if (Current == this) Current = null; }
        void Update()
        {
            RefreshPower();
            if (EFInput.KeyDown(KeyCode.R)) ResetStage();
            if (debugStageSelection)
            {
                KeyCode[] keys = { KeyCode.F1, KeyCode.F2, KeyCode.F3, KeyCode.F4, KeyCode.F5, KeyCode.F6 };
                for (int i = 0; i < Mathf.Min(keys.Length, stages.Length); i++)
                    if (EFInput.KeyDown(keys[i])) EnterStage(i);
            }
            if (Completed.Length > 0 && Completed[StageIndex] && EFInput.KeyDown(KeyCode.Return) && StageIndex + 1 < stages.Length)
                EnterStage(StageIndex + 1);
        }
        public void RefreshPower()
        {
            pending.Clear(); visited.Clear();
            foreach (EFStage stage in stages)
                foreach (EFNode node in stage.nodes)
                {
                    if (node == null) continue;
                    node.Powered = false;
                    if (node.isActiveAndEnabled && node.IsSource && node.SourceOn) pending.Enqueue(node);
                }
            while (pending.Count > 0)
            {
                EFNode node = pending.Dequeue();
                if (node == null || !node.isActiveAndEnabled || !visited.Add(node)) continue;
                // A blocked conductor still receives power, but cannot pass it on.
                node.Powered = true;
                if (!node.CanConduct) continue;
                foreach (EFNode output in node.outputs) if (output != null) pending.Enqueue(output);
            }
        }
        public void EnterStage(int index)
        {
            if (player == null || index < 0 || index >= stages.Length || stages[index].spawn == null) return;
            StageIndex = index;
            Checkpoint = stages[index].spawn.position;
            player.Teleport(Checkpoint, true);
            Message = stages[index].hint;
        }
        public void SetCheckpoint(int stage, Vector3 position)
        {
            if (stage != StageIndex || Vector3.Distance(position, Checkpoint) < .5f) return;
            Checkpoint = position;
            Message = "Checkpoint saved. Falling returns here; R resets this puzzle.";
        }
        public void Respawn()
        {
            foreach (EFNode node in stages[StageIndex].nodes) if (node != null) node.ResetRideVisual();
            player.Teleport(Checkpoint, false);
            Message = "Returned to checkpoint. Puzzle states preserved.";
        }
        public void ResetStage()
        {
            foreach (EFNode node in stages[StageIndex].nodes) if (node != null) node.ResetNode();
            Completed[StageIndex] = false;
            RefreshPower();
            EnterStage(StageIndex);
        }
        public bool ConditionsMet(int index)
        {
            if (index < 0 || index >= stages.Length) return false;
            foreach (EFNode node in stages[index].goalRequirements)
            {
                if (node == null) return false;
                switch (node.kind)
                {
                    case NodeKind.Flower: case NodeKind.Grow: case NodeKind.Vine:
                        if (!node.Grown) return false; break;
                    case NodeKind.Falling:
                        if (!node.Frozen) return false; break;
                    case NodeKind.Rotor:
                        if (!node.Frozen || !node.Aligned || node.requiresPower && !node.Powered) return false; break;
                    case NodeKind.Water:
                        if (!node.Filled || !node.Frozen) return false; break;
                    case NodeKind.Door:
                        if (!node.Powered) return false; break;
                    case NodeKind.Core:
                        if (!node.Restored) return false; break;
                }
            }
            return true;
        }
        public void TryComplete(int index)
        {
            if (index != StageIndex || Completed[index]) return;
            RefreshPower();
            if (!ConditionsMet(index)) { Message = "Reached the goal, but its current connection is incomplete."; return; }
            Completed[index] = true;
            Message = index == stages.Length - 1 ? "EVERFLOW restored. All six rooms are available with F1-F6." : "Puzzle complete! Press Enter for the next puzzle.";
        }
    }
}
