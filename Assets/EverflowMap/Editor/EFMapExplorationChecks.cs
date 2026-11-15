using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using InputProbe = EverflowGraybox.EFInput;

namespace EverflowMap.Editor
{
    // Saved-scene smoke tests. No geometry is changed or saved by this tool.
    public static class EFMapExplorationChecks
    {
        const string Key = "EVERFLOW.V2Exploration.Checks";
        [MenuItem("Tools/EVERFLOW/V2 Exploration/Run Open Scene Play Smoke Checks")]
        public static void Run()
        {
            var cameraRig = UnityEngine.Object.FindFirstObjectByType<EFMapCinemachineRig>();
            if (cameraRig != null) { EFMapCinemachineChecks.Run(); return; }
            var manager = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || manager.path != "Assets/Scenes/Everflow_Map_v2_test.unity")
                throw new InvalidOperationException("Open the saved v2 exploration scene in Edit mode first.");
            if (UnityEngine.SceneManagement.SceneManager.sceneCount != 1 || manager.isDirty)
                throw new InvalidOperationException("Save edits and open only the v2 scene before running checks.");
            SessionState.SetBool(Key, true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 90);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                    new GameObject("V2 exploration runtime smoke probe").AddComponent<EFMapExplorationProbe>();
                if (state != PlayModeStateChange.EnteredEditMode) return;
                ResetInput();
                if (!SessionState.GetBool(Key, false)) return;
                Finish("INCOMPLETE: Play stopped before the smoke checks finished.\n");
            };
            EditorApplication.update += () =>
            {
                if (!SessionState.GetBool(Key, false) || EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + ".deadline", 0)) return;
                Finish("INCOMPLETE: checks exceeded 90 seconds. No passing result is implied.\n");
                EditorApplication.ExitPlaymode();
            };
        }
        internal static void ResetInput()
        {
            InputProbe.TestMode = false; InputProbe.TestMovement = Vector2.zero; InputProbe.TestPress = KeyCode.None;
            EFMapInput.TestLookDelta = Vector2.zero; EFMapInput.TestClick = false;
        }
        internal static void Finish(string report)
        {
            SessionState.SetBool(Key, false);
            ResetInput();
            Directory.CreateDirectory("docs/v2-exploration");
            File.WriteAllText("docs/v2-exploration/play-smoke-checks.txt", DateTime.UtcNow.ToString("O") + "\n" + report);
        }
    }
    public sealed class EFMapExplorationProbe : MonoBehaviour
    {
        EFMapExploration host;
        string report = "Unity Play: injected movement/look/key input; teleports are fixtures, not full-map traversal proof. No human playthrough.\n";
        int failures, checks;
        readonly List<string> errors = new List<string>();
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors.Add(message);
        }
        void OnEnable() { Application.logMessageReceived += Log; }
        void OnDisable() { Application.logMessageReceived -= Log; }
        void Check(bool value, string message)
        {
            checks++; if (!value) failures++;
            report += (value ? "PASS " : "FAIL ") + message + "\n";
        }
        IEnumerator Start()
        {
            host = FindFirstObjectByType<EFMapExploration>();
            InputProbe.TestMode = true;
            // Advance the iterator explicitly so exceptions still produce an incomplete/failing report.
            var stack = new Stack<IEnumerator>(); stack.Push(Exercise());
            while (stack.Count > 0)
            {
                bool more = false; object next = null;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception error) { Check(false, "EXCEPTION " + error); break; }
                if (!more) stack.Pop();
                else if (next is IEnumerator iterator) stack.Push(iterator);
                else yield return next;
            }
            Check(errors.Count == 0, "No observed runtime errors: " + string.Join(" | ", errors));
            report += $"TOTAL {checks} checks; {failures} failures\n";
            EFMapExplorationChecks.Finish(report);
            Debug.Log(report);
            EditorApplication.ExitPlaymode();
        }
        IEnumerator Wait(float seconds) { float until = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < until) yield return null; }
        IEnumerator Press(KeyCode key) { InputProbe.TestPress = key; yield return null; InputProbe.TestPress = KeyCode.None; yield return null; }
        IEnumerator Exercise()
        {
            yield return null;
            if (host.Paused) yield return Press(KeyCode.Escape);
            EFMapExplorationTools.Validate();
            var p = host.player; var cc = p.GetComponent<CharacterController>(); var follow = p.follow;
            Check(host.EnsureTerrainColliders() == 0, "Collision setup is idempotent after automatic Awake");
            Check(Vector3.Distance(p.transform.position, host.spawnPoint.position) < .5f, "Direct Play starts at saved SpawnPoint");
            Check(Mathf.Abs(Mathf.DeltaAngle(p.transform.eulerAngles.y, host.spawnPoint.eulerAngles.y)) < 1, "Initial heading matches spawn");
            yield return Wait(.4f);
            Check(cc.isGrounded, "CharacterController settles on overlook floor");
            Vector3 start = p.transform.position;
            InputProbe.TestMovement = Vector2.up; yield return Wait(.6f); InputProbe.TestMovement = Vector2.zero;
            Check(Vector3.Dot(p.transform.position - start, follow.PlanarForward) > 2, "Injected WASD moves toward A approach");
            float baseY = p.transform.position.y, peak = baseY;
            yield return Press(KeyCode.Space);
            float until = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < until) { peak = Mathf.Max(peak, p.transform.position.y); yield return null; }
            Check(peak > baseY + .8f && cc.isGrounded, "Jump rises and returns grounded");
            float yaw = follow.yaw;
            EFMapInput.TestLookDelta = new Vector2(100, 0); yield return null; EFMapInput.TestLookDelta = Vector2.zero; yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw, follow.yaw)) > 5, "Injected mouse delta rotates camera");
            yield return Press(KeyCode.R); yield return Wait(.15f);
            Check(Vector3.Distance(p.transform.position, host.spawnPoint.position) < .4f && Mathf.Abs(Mathf.DeltaAngle(follow.yaw, host.spawnPoint.eulerAngles.y)) < 1, "Manual R resets player and camera heading");
            p.Teleport(new Vector3(-46, host.fallHeight - 2, -9)); yield return null; yield return null;
            Check(Vector3.Distance(p.transform.position, host.spawnPoint.position) < .4f, "Fall below threshold returns to SpawnPoint");
            yield return Wait(.4f);
            start = p.transform.position; InputProbe.TestMovement = Vector2.up; yield return Wait(.3f); InputProbe.TestMovement = Vector2.zero;
            Check(Vector3.Distance(p.transform.position, start) > 1 && cc.enabled, "Movement remains active after both respawns");
            // Query actual imported geometry at representative floors. These are not walked routes.
            foreach (var point in new[] {new Vector3(-30.5f,-1,-40), new Vector3(-56,-10,-53), new Vector3(-59.5f,-2,-50)})
                Check(Physics.Raycast(point, Vector3.down, out _, 15, ~(1 << 2), QueryTriggerInteraction.Ignore), "Imported floor/step query at " + point);
            Check(Physics.Raycast(new Vector3(-26, 1, -65), Vector3.left, out _, 3, ~(1 << 2), QueryTriggerInteraction.Ignore), "Tower wall has physical collision");
            yield return Press(KeyCode.Escape);
            Check(host.Paused && !p.enabled && !follow.enabled, "Escape releases cursor and stops movement/camera");
            yield return Press(KeyCode.Escape);
            Check(!host.Paused && p.enabled && follow.enabled, "Escape resumes both controllers");
        }
    }
}
