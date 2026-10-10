using System;
using UnityEditor;
using UnityEngine;

namespace EverflowMVP.Editor
{
    public static class EFStateTests
    {
        [MenuItem("Tools/EVERFLOW/Run State Tests (Edit Mode)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before state tests.");
            var root = new GameObject("Temporary EVERFLOW state tests") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                EFNode Make(string name, NodeKind kind)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform);
                    var n = go.AddComponent<EFNode>(); n.kind = kind; n.label = name; return n;
                }
                void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); }
                var source = Make("source", NodeKind.Source); source.acceptsPulse = true;
                var vine = Make("vine", NodeKind.Grow);
                var rotor = Make("rotor", NodeKind.Rotor); rotor.requiresPower = rotor.alignmentConducts = true;
                rotor.initialAngle = 45; rotor.targetAngle = 0; rotor.speed = -30; rotor.tolerance = 4;
                var core = Make("core", NodeKind.Core);
                source.outputs = new[] { vine }; vine.outputs = new[] { rotor }; rotor.outputs = new[] { core, vine };
                var world = root.AddComponent<EFWorld>();
                world.stages = new[] { new EFStage { nodes = new[] { source, vine, rotor, core } } };
                foreach (EFNode n in world.stages[0].nodes) n.ResetNode();
                source.Apply(Ability.Pulse); world.RefreshPower();
                Check(!rotor.Powered && !core.Powered, "ungrown root blocks power");
                vine.Apply(Ability.Bloom); world.RefreshPower();
                Check(rotor.Powered && !core.Powered, "grown root powers rotor but unaligned rotor blocks core");
                rotor.Tick(1.5f); rotor.Apply(Ability.Freeze); world.RefreshPower();
                Check(rotor.Aligned && core.Powered, "aligned frozen rotor conducts; cycle terminates");
                float angle = rotor.Angle; rotor.Tick(20); Check(Mathf.Approximately(angle, rotor.Angle), "frozen rotor holds angle");
                core.Apply(Ability.Pulse); Check(core.Restored, "powered core restores");
                rotor.Apply(Ability.Freeze); world.RefreshPower(); Check(!core.Powered, "release removes downstream power");
                rotor.Tick(.1f); Check(Mathf.Abs(Mathf.DeltaAngle(rotor.Angle, angle)) < 4, "release resumes without time jump");
                source.Apply(Ability.Pulse); world.RefreshPower(); Check(!rotor.Powered && !core.Powered, "power loss propagates");
                var water = Make("water", NodeKind.Water); water.ResetNode();
                water.Apply(Ability.Freeze); Check(!water.Frozen, "dry water cannot freeze");
                water.Powered = true; water.Tick(0); water.Apply(Ability.Freeze); Check(water.Filled && water.Frozen, "filled water freezes");
                water.Powered = false; water.Tick(0); Check(water.Filled, "stored water survives supply off");
                water.ResetNode(); Check(!water.Filled && !water.Frozen, "reset removes water and ice");
                core.ResetNode(); Check(!core.Restored, "reset clears restoration");
                Debug.Log("PASS: 13 EVERFLOW state assertions. Geometry, input, camera and trajectory still require Play testing.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
