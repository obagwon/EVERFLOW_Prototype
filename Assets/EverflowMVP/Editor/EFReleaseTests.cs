using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMVP.Editor
{
    public static class EFReleaseTests
    {
        static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); }
        static void Reject(Action validate, string name)
        {
            try { validate(); }
            catch (InvalidOperationException) { return; }
            throw new Exception("FAIL: accepted " + name);
        }

        [MenuItem("Tools/EVERFLOW/Release/Run Release Scene List Tests")]
        public static void RunSceneListTests()
        {
            const string legacy = "Assets/EverflowMVP/Scenes/Everflow_6Puzzles.unity";
            const string lab = "Assets/EverflowMVP/Scenes/MechanicsLab.unity";
            var original = new[] { new EditorBuildSettingsScene(legacy, true),
                new EditorBuildSettingsScene(EFTitleScreen.GameplayScene, false),
                new EditorBuildSettingsScene(lab, false), new EditorBuildSettingsScene(EFReleaseSetup.TitleScene, false) };
            var configured = EFReleaseSetup.CreateReleaseScenes(original);
            EFReleaseSetup.ValidateBuildScenes(configured);
            Check(configured.Length == 4 && configured[2].path == legacy && !configured[2].enabled &&
                configured[3].path == lab && !configured[3].enabled, "utility scenes preserved disabled");
            Check(original[0].enabled && !original[1].enabled, "source scene entries unchanged");
            var repeated = EFReleaseSetup.CreateReleaseScenes(configured);
            Check(repeated.Select(s => (s.path, s.enabled)).SequenceEqual(configured.Select(s => (s.path, s.enabled))),
                "repeat Release setup retains identical paths, order and enabled flags");
            EFReleaseSetup.ValidateBuildScenes(repeated);
            Reject(() => EFReleaseSetup.ValidateBuildScenes(Array.Empty<EditorBuildSettingsScene>()), "empty scene list");
            Reject(() => EFReleaseSetup.ValidateBuildScenes(new[] { configured[1], configured[0] }), "map before title");
            Reject(() => EFReleaseSetup.ValidateBuildScenes(new[] { configured[0], new EditorBuildSettingsScene(legacy, true) }), "legacy gameplay substitute");
            EFReleaseSetup.ValidateBuildScenes(new[] { configured[0], configured[1], new EditorBuildSettingsScene(lab, true) });
            Reject(() => EFReleaseSetup.ValidateBuildScenes(new[] { configured[0], configured[1], configured[1] }), "duplicate map scene");
            Debug.Log("PASS: release scene ordering, legacy rejection, disabled-scene preservation and repeated setup (no settings changed).");
        }

        [MenuItem("Tools/EVERFLOW/Release/Run Release Integration Tests")]
        public static void RunIntegration()
        {
            Run();
            RunSceneListTests();
            EFReleaseSetup.ValidateScenes();
            Reject(() => EFReleaseSetup.ValidateMapReferences(Array.Empty<EverflowMap.EFMapWorld>(),
                Array.Empty<EverflowMap.EFMapPlayer>()), "missing map world (legacy fallback)");
            var scene = EditorSceneManager.OpenPreviewScene(EFTitleScreen.GameplayScene);
            try
            {
                var roots = scene.GetRootGameObjects();
                var worlds = roots.SelectMany(r => r.GetComponentsInChildren<EverflowMap.EFMapWorld>(true)).ToArray();
                var players = roots.SelectMany(r => r.GetComponentsInChildren<EverflowMap.EFMapPlayer>(true)).ToArray();
                var world = worlds.Single();
                void Validate() => EFReleaseSetup.ValidateMapReferences(worlds, players);
                var flow = world.finalFlow;
                var camera = world.endingCamera;
                var gate = world.gate4;
                var restorationStage = world.stages.First(s => s.restoration.Length > 0);
                var restoration = restorationStage.restoration;
                try
                {
                    world.finalFlow = Array.Empty<GameObject>(); Reject(Validate, "empty final water flow");
                    world.finalFlow = new GameObject[] { null }; Reject(Validate, "missing final water segment");
                    world.finalFlow = flow;
                    world.endingCamera = world.player.view; Reject(Validate, "gameplay camera reused as ending camera");
                    world.endingCamera = camera;
                    world.gate4 = null; Reject(Validate, "missing water gate");
                    world.gate4 = gate;
                    restorationStage.restoration = new EverflowMap.EFMapAnimation[] { null };
                    Reject(Validate, "missing restoration action");
                }
                finally
                {
                    world.finalFlow = flow; world.endingCamera = camera; world.gate4 = gate;
                    restorationStage.restoration = restoration;
                }
                Validate();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("PASS: saved title/map references and rejection of broken map/ending references. Preview only; no scene saved, no Play or player build performed.");
        }

        [MenuItem("Tools/EVERFLOW/Release/Run Version Transaction Tests")]
        public static void Run()
        {
            Check(EFReleaseBuild.NextVersion("1.0.1") == "1.0.2", "first release");
            Check(EFReleaseBuild.NextVersion("2.3.99") == "2.3.100", "no downgrade or minor rollover");
            foreach (var result in new[] { BuildResult.Succeeded, BuildResult.Failed, BuildResult.Cancelled, BuildResult.Unknown })
            {
                string version = "1.0.1";
                int commits = 0;
                EFReleaseBuild.Transaction(version, "1.0.2", v => version = v, () =>
                {
                    Check(version == "1.0.2", "candidate is embedded during build");
                    return result;
                }, () => commits++);
                Check(version == (result == BuildResult.Succeeded ? "1.0.2" : "1.0.1"), "commit or rollback " + result);
                Check(commits == (result == BuildResult.Succeeded ? 1 : 0), "single success commit " + result);
            }
            string restored = "1.0.1";
            try { EFReleaseBuild.Transaction(restored, "1.0.2", v => restored = v,
                () => throw new OperationCanceledException(), () => { }); }
            catch (OperationCanceledException) { }
            Check(restored == "1.0.1", "exception rollback");
            Debug.Log("PASS: release version success/failure/cancel/exception transaction tests (no real build or version change).");
        }
    }
}
