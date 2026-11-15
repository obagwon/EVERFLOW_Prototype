using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using ProbeInput = EverflowGraybox.EFInput;

namespace EverflowMap.Editor
{
    public static class EFMapCinemachineChecks
    {
        const string Key = "EVERFLOW.V2Cinemachine.Checks";
        [MenuItem("Tools/EVERFLOW/V2 Exploration/Run Cinemachine Play Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != EFMapCinemachineSetup.ScenePath || scene.isDirty)
                throw new Exception("Open and save Everflow_Map_v2_test first.");
            EFMapCinemachineSetup.Validate();
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 120);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!SessionState.GetBool(Key, false)) return;
                if (state == PlayModeStateChange.EnteredPlayMode)
                    new GameObject("Cinemachine runtime probe").AddComponent<EFMapCinemachineProbe>();
                if (state == PlayModeStateChange.EnteredEditMode) Finish("INCOMPLETE: Play stopped before checks finished.\n", false);
            };
            EditorApplication.update += () =>
            {
                if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", 0))
                {
                    Finish("INCOMPLETE: checks exceeded 120 seconds.\n", false);
                    EditorApplication.ExitPlaymode();
                }
            };
        }

        internal static void Finish(string report, bool passed)
        {
            SessionState.SetBool(Key, false);
            ProbeInput.TestMode = false; ProbeInput.TestMovement = Vector2.zero; ProbeInput.TestPress = KeyCode.None;
            Time.captureDeltaTime = 0;
            Directory.CreateDirectory("docs/cinemachine");
            File.WriteAllText("docs/cinemachine/play-checks.txt", DateTime.UtcNow.ToString("O") + "\n" + report);
            Debug.Log(report);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    public sealed class EFMapCinemachineProbe : MonoBehaviour
    {
        string report = "Unity Play checks: synthetic Input System mouse events and existing locomotion test input. Runtime-only fixtures. No human mouse-feel assessment.\n";
        int checks, failures;
        Mouse mouse;
        GameObject fixtures;
        EFMapExploration host;
        EFMapPlayer player;
        EFMapCinemachineRig rig;
        readonly List<string> errors = new List<string>();
        void Log(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse); }
        void Check(bool condition, string label)
        {
            checks++; if (!condition) failures++;
            report += (condition ? "PASS " : "FAIL ") + label + "\n";
        }
        IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
        IEnumerator Look(Vector2 delta)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = delta });
            yield return Frames(2);
        }
        IEnumerator Press(KeyCode key)
        {
            ProbeInput.TestPress = key; yield return null; ProbeInput.TestPress = KeyCode.None; yield return null;
        }
        GameObject Cube(string name, Vector3 point, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(fixtures.transform); go.transform.position = point; go.transform.localScale = size;
            return go;
        }
        IEnumerator Exercise()
        {
            ProbeInput.TestMode = true; Time.captureDeltaTime = 1f / 60;
            mouse = InputSystem.AddDevice<Mouse>("CinemachineProbeMouse");
            host = FindFirstObjectByType<EFMapExploration>(); player = host.player; rig = player.cinemachineRig;
            if (host.Paused) yield return Press(KeyCode.Escape);
            yield return Frames(30);
            EFMapCinemachineSetup.Validate(); EFMapExplorationTools.Validate();
            Check(player.follow == null && player.view == Camera.main, "Legacy owner removed; existing tagged Main Camera retained");
            Check(ReferenceEquals(rig.brain.ActiveVirtualCamera, rig.virtualCamera), "Brain selects FreeLook Camera");
            fixtures = new GameObject("Runtime-only Cinemachine fixtures");
            Cube("Flat floor", new Vector3(200, 80, 200), new Vector3(80, 1, 80));
            player.Teleport(new Vector3(200, 81.55f, 200));
            rig.orbit.HorizontalAxis.Value = 0; rig.orbit.VerticalAxis.Value = 18; rig.ResetView();
            yield return Frames(90);
            float yaw = rig.orbit.HorizontalAxis.Value, pitch = rig.orbit.VerticalAxis.Value;
            var rotation = player.transform.rotation;
            yield return Look(new Vector2(100, 20));
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw, rig.orbit.HorizontalAxis.Value) - 14) < .1f, "Mouse X delta drives orbit through Look action (no RMB)");
            Check(Mathf.Abs(rig.orbit.VerticalAxis.Value - (pitch - 2.38f)) < .1f, "Mouse Y delta drives vertical orbit with Cancel Delta Time");
            Check(Quaternion.Angle(rotation, player.transform.rotation) < .01f, "Idle mouse look leaves player rotation unchanged");
            yield return Look(new Vector2(0, 10000));
            Check(Mathf.Abs(rig.orbit.VerticalAxis.Value + 30) < .1f, "Upward pitch clamps at -30 degrees");
            yield return Look(new Vector2(0, -10000));
            Check(Mathf.Abs(rig.orbit.VerticalAxis.Value - 65) < .1f, "Downward pitch clamps at 65 degrees without flipping");
            rig.orbit.VerticalAxis.Value = 18; rig.orbit.HorizontalAxis.Value = 90; rig.ResetView();
            yield return Frames(30);
            var forward = Vector3.ProjectOnPlane(player.view.transform.forward, Vector3.up).normalized;
            var before = player.transform.position;
            ProbeInput.TestMovement = Vector2.up; yield return Frames(20); ProbeInput.TestMovement = Vector2.zero;
            Check(Vector3.Dot(player.transform.position - before, forward) > 1.7f, "After 90-degree orbit, W moves along the rendered camera's horizontal forward");
            yaw = rig.orbit.HorizontalAxis.Value;
            player.transform.rotation = Quaternion.Euler(0, 270, 0); yield return Frames(30);
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw, rig.orbit.HorizontalAxis.Value)) < .01f, "Player heading change does not recenter world-space orbit");
            float baseY = player.transform.position.y, cameraY = player.view.transform.position.y;
            yield return Press(KeyCode.Space); yield return Frames(8);
            Check(player.transform.position.y > baseY + .7f && player.view.transform.position.y > cameraY + .1f, "Real CharacterController jump is followed vertically");
            yield return Frames(90);
            var stable = player.view.transform.position; yield return Frames(30);
            Check(Vector3.Distance(stable, player.view.transform.position) < .03f, "Camera settles after walking/jumping");
            var anchor = rig.virtualCamera.Follow.position;
            var direction = (player.view.transform.position - anchor).normalized;
            float open = Vector3.Distance(anchor, player.view.transform.position);
            var wall = Cube("Occluding wall", anchor + direction * (open * .5f), new Vector3(15, 12, .5f));
            wall.transform.rotation = Quaternion.LookRotation(direction);
            Physics.SyncTransforms(); yield return Frames(5);
            float close = Vector3.Distance(rig.virtualCamera.Follow.position, player.view.transform.position);
            Check(close < open - 1 && close > .3f, "Deoccluder pulls camera forward from wall");
            Check(Vector3.Distance(wall.GetComponent<Collider>().ClosestPoint(player.view.transform.position), player.view.transform.position) > .01f, "Retracted camera remains outside obstruction");
            wall.SetActive(false); yield return Frames(2);
            float returning = Vector3.Distance(rig.virtualCamera.Follow.position, player.view.transform.position);
            Check(returning > close && returning < open - .05f, "Obstacle removal starts a damped return");
            yield return Frames(100);
            Check(Mathf.Abs(Vector3.Distance(rig.virtualCamera.Follow.position, player.view.transform.position) - open) < .05f, "Camera returns to configured distance with player capsule excluded");
            yield return Press(KeyCode.Escape);
            yaw = rig.orbit.HorizontalAxis.Value;
            Check(host.Paused && !player.enabled && !rig.input.enabled, "Escape releases cursor and disables movement/look");
            yield return Look(new Vector2(100, 100));
            Check(rig.orbit.HorizontalAxis.Value == yaw, "Paused mouse input does not alter orbit");
            yield return Press(KeyCode.Escape); yield return Frames(4);
            Check(!host.Paused && player.enabled && rig.input.enabled, "Escape resume restores movement and Cinemachine input");
            player.Teleport(host.spawnPoint.position); yield return Frames(3);
            Check(Vector3.Distance(player.view.transform.position, player.transform.position) < 8, "Respawn resets camera damping without long fly-in");
            report += "N/A ability/interaction: saved scene is standalone exploration (puzzleLabLocomotion=true, world=null), with no active ability/interaction controller. Main Camera reference remains unchanged.\n";
        }

        IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Exercise());
            while (stack.Count > 0)
            {
                bool more = false; object next = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error) { Check(false, error.ToString()); break; }
                if (!more) stack.Pop(); else if (next is IEnumerator iterator) stack.Push(iterator); else yield return next;
            }
            Check(errors.Count == 0, "No runtime errors: " + string.Join(" | ", errors));
            if (fixtures != null) Destroy(fixtures);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            report += $"TOTAL {checks} checks, {failures} failures\n";
            EFMapCinemachineChecks.Finish(report, failures == 0);
            if (!Application.isBatchMode) EditorApplication.ExitPlaymode();
        }
    }
}
