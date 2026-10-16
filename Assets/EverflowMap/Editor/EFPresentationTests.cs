using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EFInput = EverflowGraybox.EFInput;
using Ability = EverflowGraybox.Ability;

namespace EverflowMap.Editor
{
    public static class EFPresentationTests
    {
        [MenuItem("Tools/EVERFLOW/Presentation/Run UI and Passage Regression Tests")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EverflowMVP.Editor.EFReleaseSetup.ValidateScenes();
            EditorSceneManager.OpenScene(EverflowMVP.Editor.EFReleaseSetup.TitleScene);
            SessionState.SetBool("EFPresentationTests", true); EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Register()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("EFPresentationTests", false)) return;
                SessionState.SetBool("EFPresentationTests", false);
                new GameObject("Presentation regression probe").AddComponent<EFPresentationProbe>();
            };
        }
    }
    public sealed class EFPresentationProbe : MonoBehaviour
    {
        EFMapWorld world;
        EFMapPlayer player;
        string report = "Unity Play Mode regression: deterministic key/pointer-event injection, real scene loading and world Update. Passage fixtures teleport to explicit geometry; these do not claim physical traversal. UI captures render actual Canvas at 16:9/16:10 when a graphics device is available.\n";
        int checks, errors;
        void Check(bool valid, string message)
        {
            report += (valid ? "PASS " : "FAIL ") + message + "\n";
            if (!valid) throw new Exception(message);
            checks++;
        }
        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { errors++; report += "CONSOLE " + message + "\n"; }
        }
        IEnumerator Frames(int count, KeyCode key = KeyCode.None)
        {
            EFInput.TestPress = key;
            for (int i = 0; i < count; i++) { yield return null; EFInput.TestPress = KeyCode.None; }
        }
        void Bind()
        {
            world = FindFirstObjectByType<EFMapWorld>(); player = world.player;
        }
        void CheckHUD(Ability ability)
        {
            var hud = FindFirstObjectByType<EFMapAbilityHUD>();
            Check(player.selected == ability && hud.abilities.Count(a => a.selectedMark.activeSelf) == 1 &&
                hud.abilities.Single(a => a.ability == ability).selectedMark.activeSelf &&
                hud.abilities.Count(a => a.description.gameObject.activeSelf) == 1 &&
                hud.abilities.Single(a => a.ability == ability).description.gameObject.activeSelf, "Actual selection, single underline and Korean description agree: " + ability);
        }
        void Capture(string name, Canvas canvas, Camera camera, int width, int height)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) { report += "SKIP graphics: " + name + "\n"; return; }
            var previousMode = canvas.renderMode; var previousCamera = canvas.worldCamera;
            var scaler = canvas.GetComponent<CanvasScaler>(); bool scalerEnabled = scaler.enabled;
            float factor = canvas.scaleFactor;
            var oldTarget = camera.targetTexture;
            var target = new RenderTexture(width, height, 24); target.Create();
            var previousRT = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                scaler.enabled = false; canvas.scaleFactor = Mathf.Min(width / 1920f, height / 1080f);
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                foreach (var graphic in canvas.GetComponentsInChildren<Graphic>().Where(g => g.isActiveAndEnabled))
                {
                    graphic.rectTransform.GetWorldCorners(corners);
                    // Only the source artwork's transparent padding can extend past its visible group.
                    Check(corners.All(p => { var v = camera.WorldToViewportPoint(p); return v.x >= -.001f && v.x <= 1.001f && v.y >= -.001f && v.y <= 1.001f; }), name + ": UI rect inside viewport: " + graphic.name);
                }
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(EFPresentationSetup.Evidence + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = previousRT;
                canvas.renderMode = previousMode; canvas.worldCamera = previousCamera;
                canvas.scaleFactor = factor; scaler.enabled = scalerEnabled; Canvas.ForceUpdateCanvases();
                target.Release(); Destroy(target); if (image != null) Destroy(image);
            }
        }
        IEnumerator Tests()
        {
            EFInput.TestMode = true; Time.captureDeltaTime = 1f / 60;
            yield return Frames(3);
            var title = FindFirstObjectByType<EverflowMVP.EFTitleScreen>();
            Check(title != null && Cursor.visible, "Title has a visible cursor");
            Check(FindObjectsByType<EFMapAbilityHUD>(FindObjectsSortMode.None).Length == 0, "No gameplay HUD on title");
            Check(!title.startLabel.gameObject.activeSelf && title.startButton.targetGraphic.GetComponent<Image>().sprite != null, "Button artwork supplies text; old label hidden");
            var canvas = title.GetComponent<Canvas>(); var camera = FindFirstObjectByType<Camera>();
            Capture("title-1920x1080", canvas, camera, 1920, 1080);
            Capture("title-1920x1200", canvas, camera, 1920, 1200);
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, title.startButton.transform.position) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == title.startButton.gameObject, "Visible start button is topmost UI raycast target");
            ExecuteEvents.Execute(title.startButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            title.StartGame(); title.StartGame();
            Check(!title.startButton.interactable, "Repeated start requests disable button during single scene load");
            yield return Frames(210); Bind();
            Check(SceneManager.GetActiveScene().path == EverflowMVP.EFTitleScreen.GameplayScene && world.GameInputActive && world.WantsCursorLocked,
                "Start pointer click loads Map; intro hands control and cursor ownership to gameplay");
            Check(FindObjectsByType<EFMapAbilityHUD>(FindObjectsSortMode.None).Length == 1, "One scene-owned gameplay HUD");
            var hud = FindFirstObjectByType<EFMapAbilityHUD>();
            Check(!hud.visibility.blocksRaycasts && !hud.visibility.interactable && hud.GetComponent<GraphicRaycaster>() == null &&
                hud.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "HUD cannot intercept pointer input");
            CheckHUD(player.selected);
            for (int i = 0; i < 3; i++)
            {
                yield return Frames(2, (KeyCode)((int)KeyCode.Alpha1 + i)); CheckHUD((Ability)i);
                var text = hud.abilities[i].description;
                text.font.RequestCharactersInTexture(text.text, text.fontSize);
                Check(text.text.All(c => c == ' ' || text.font.HasCharacter(c)), "Korean font contains every description glyph: " + text.font.name);
            }
            Capture("hud-pulse-1920x1080", hud.GetComponent<Canvas>(), player.view, 1920, 1080);
            Capture("hud-pulse-1920x1200", hud.GetComponent<Canvas>(), player.view, 1920, 1200);
            yield return Frames(2, KeyCode.Alpha1);
            Capture("hud-bloom-1920x1080", hud.GetComponent<Canvas>(), player.view, 1920, 1080);
            yield return Frames(2, KeyCode.Alpha2);
            Capture("hud-freeze-1920x1080", hud.GetComponent<Canvas>(), player.view, 1920, 1080);
            Check(FindObjectsByType<EFMapPassage>(FindObjectsSortMode.None).Length == 5 && world.stages[5].arrival.GetComponent<EFMapPassage>() == null, "Only P1-P5 have passage markers; final puzzle has none");
            for (int index = 0; index < 5; index++)
            {
                var stage = world.stages[index];
                Check(world.StageIndex == index && !world.ConditionsMet(index), "P" + (index + 1) + " fixture is unsolved by original conditions");
                Check(!world.TryPassage(index + 1) && !world.TryPassage(-1), "Only the current ordinary puzzle can complete");
                player.Teleport(stage.arrival.position + Vector3.down * (stage.arrivalSize.y / 2 + 1));
                Check(!world.TryPassage(index), "P" + (index + 1) + " rejects nearby XZ location below exit height");
                player.Teleport(stage.spawn.position);
                Check(!world.TryPassage(index), "P" + (index + 1) + " rejects pre-traversal spawn");
                // Check the door approach/bridge entrance as well as spawn, without distance-only heuristics.
                Vector3 approach = index == 4 ? new Vector3(-20.5f, 11, 5) : index == 2 ? new Vector3(42, 8.6f, -42) : index == 1 ? new Vector3(44, 8.6f, -25) : index == 0 ? new Vector3(26, 4.1f, 23) : new Vector3(-19.4f, 25, -47);
                player.Teleport(GameObject.Find("EF_Terrain").transform.position + approach);
                Check(!world.TryPassage(index), "P" + (index + 1) + " rejects actual near-side approach");
                var nextTarget = world.stages[index + 1].nodes.First(n => n.interactive);
                Check(!nextTarget.CanApply(Ability.Bloom, out var reason) && reason.Contains("not active"), "Next puzzle abilities locked before passage");
                // Wide exit accepts an off-center, elevated arrival without any ability/order predicate.
                player.Teleport(stage.arrival.position + new Vector3(stage.arrivalSize.x * .3f, .8f, 0));
                yield return Frames(1);
                Check(world.Completed[index] && world.StageIndex == index + 1 && world.Completed.Count(v => v) == index + 1,
                    "P" + (index + 1) + " off-center arrival commits exactly one stage despite failed fine conditions");
                Check(world.Checkpoint == world.stages[index + 1].spawn.position && stage.restoration.All(a => a.Started), "Checkpoint and restoration use the normal completion path");
                nextTarget.CanApply(Ability.Bloom, out reason);
                Check(reason == null || !reason.Contains("not active"), "Next puzzle progression lock is immediately released");
                yield return Frames(160);
                var elapsed = stage.restoration.Select(a => a.Elapsed).ToArray();
                player.Teleport(stage.arrival.position); Check(!world.TryPassage(index) && !world.TryArrive(index), "Reentry/normal completion cannot commit previous stage again");
                foreach (var node in stage.nodes) node.ResetNode(); // Deliberate state mutation fixture, never done by passage.
                world.Respawn(); world.Retry(); yield return Frames(2);
                Check(world.Completed[index] && world.StageIndex == index + 1 && stage.restoration.Select(a => a.Elapsed).SequenceEqual(elapsed),
                    "Accepted completion and one-shot restoration survive state changes, respawn and current retry");
            }
            player.Teleport(world.stages[5].arrival.position);
            Check(!world.TryPassage(5) && !world.TryArrive(5), "Final arrival cannot bypass restoration/ending conditions through either traversal API");
            yield return Frames(400);
            Check(!world.WheelRunning && !world.Completed[5] && !world.Ended && world.finalFlow.All(f => !f.activeSelf), "Standing near final wheel does not start flow or ending");
            world.RestartWorld(); yield return Frames(210); Bind();
            Check(world.StageIndex == 0 && world.Completed.All(v => !v) && !world.WheelRunning &&
                FindObjectsByType<EFMapAbilityHUD>(FindObjectsSortMode.None).Length == 1, "Explicit world restart clears progress and creates only one HUD");
            // Explicit original normal API succeeds with original predicates, independent of passage polling.
            foreach (var n in world.stages[0].requirements) { n.Apply(Ability.Bloom); foreach (var a in n.actions) a.Tick(100); n.Tick(0); }
            player.FlowerUsed = player.VineUsed = true; player.Teleport(world.stages[0].arrival.position);
            Check(world.ConditionsMet(0) && world.TryArrive(0), "Original normal-clear API still commits when fine conditions are satisfied");
            world.ReturnToTitle(); yield return Frames(15);
            Check(FindObjectsByType<EFMapAbilityHUD>(FindObjectsSortMode.None).Length == 0 && FindFirstObjectByType<EverflowMVP.EFTitleScreen>() != null, "Return to title unloads HUD");
            FindFirstObjectByType<EverflowMVP.EFTitleScreen>().startButton.onClick.Invoke(); yield return Frames(210); Bind();
            Check(world.StageIndex == 0 && world.Completed.All(v => !v) && FindObjectsByType<EFMapAbilityHUD>(FindObjectsSortMode.None).Length == 1, "Title new-game entry resets all progress without duplicate HUD");
            Check(errors == 0, "No error/exception/assert console messages during regression");
        }
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject); Directory.CreateDirectory(EFPresentationSetup.Evidence); Application.logMessageReceived += Log;
            var stack = new Stack<IEnumerator>(); stack.Push(Tests()); bool failed = false;
            while (stack.Count > 0)
            {
                object next = null; bool more;
                try { more = stack.Peek().MoveNext(); if (more) next = stack.Peek().Current; }
                catch (Exception e) { report += "ERROR " + e + "\n"; Debug.LogException(e); failed = true; break; }
                if (!more) { stack.Pop(); continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            Application.logMessageReceived -= Log; EFInput.TestMode = false; EFInput.TestPress = KeyCode.None; Time.captureDeltaTime = 0;
            report += $"\n{checks} checks passed. Console errors: {errors}. OVERALL {(failed ? "FAIL" : "PASS")}\n";
            File.WriteAllText(EFPresentationSetup.Evidence + "/regression-results.txt", report);
            Debug.Log(report);
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0); else EditorApplication.ExitPlaymode();
        }
    }
}
