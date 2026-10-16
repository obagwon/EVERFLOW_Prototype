using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EverflowMVP.Editor
{
    public static class EFReleaseBuild
    {
        const string JournalPath = "ProjectSettings/EverflowRelease.pending.json";
        static bool building;

        [Serializable]
        sealed class Journal
        {
            public string previous;
            public string candidate;
            public bool succeeded;
        }

        [InitializeOnLoadMethod]
        static void OnReload() { EditorApplication.delayCall += RecoverInterruptedBuild; }

        static void RecoverInterruptedBuild()
        {
            if (building || BuildPipeline.isBuildingPlayer || !File.Exists(JournalPath)) return;
            var journal = JsonUtility.FromJson<Journal>(File.ReadAllText(JournalPath));
            if (journal == null || string.IsNullOrEmpty(journal.previous) || string.IsNullOrEmpty(journal.candidate))
                throw new InvalidOperationException("Invalid release journal. Inspect " + JournalPath);
            string desired = journal.succeeded ? journal.candidate : journal.previous;
            if (PlayerSettings.bundleVersion != journal.previous && PlayerSettings.bundleVersion != journal.candidate)
                throw new InvalidOperationException("Version was edited during an interrupted build. Inspect " + JournalPath);
            PlayerSettings.bundleVersion = desired;
            AssetDatabase.SaveAssets();
            File.Delete(JournalPath);
            Debug.Log("Recovered interrupted EVERFLOW release transaction: " + desired);
        }

        public static string NextVersion(string current)
        {
            string[] parts = current.Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[0], out int major) || major < 0 ||
                !int.TryParse(parts[1], out int minor) || minor < 0 ||
                !int.TryParse(parts[2], out int patch) || patch < 0)
                throw new InvalidOperationException("Expected major.minor.patch; found " + current);
            return major + "." + minor + "." + checked(patch + 1);
        }

        // Shared by the real build and failure/cancellation tests. Never invoked on compilation or Play.
        public static BuildResult Transaction(string previous, string candidate, Action<string> setVersion,
            Func<BuildResult> build, Action commitSuccess)
        {
            bool committed = false;
            try
            {
                setVersion(candidate);
                var result = build();
                if (result == BuildResult.Succeeded)
                {
                    commitSuccess();
                    committed = true;
                }
                return result;
            }
            finally { setVersion(committed ? candidate : previous); }
        }

        [MenuItem("Tools/EVERFLOW/Release/3. Build Windows Release (Next Patch)")]
        public static void BuildWindows()
        {
            if (building || BuildPipeline.isBuildingPlayer || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode and wait for any running build.");
            RecoverInterruptedBuild();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("Select Windows x86_64 in Build Profiles first.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows Build Support for this Unity editor.");
            var profile = BuildProfile.GetActiveBuildProfile();
            if (profile != null)
            {
                var settings = new SerializedObject(profile).FindProperty("m_PlayerSettingsYaml.m_Settings");
                if (settings != null && settings.arraySize > 0)
                    throw new InvalidOperationException("Use the Windows platform profile without Player Settings overrides so the release version and icon stay consistent.");
            }
            EFReleaseSetup.Configure();
            string previous = PlayerSettings.bundleVersion;
            string candidate = NextVersion(previous);
            string folder = Path.Combine("Build", candidate);
            if (Directory.Exists(folder)) folder += "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            Directory.CreateDirectory(folder);
            var journal = new Journal { previous = previous, candidate = candidate };
            File.WriteAllText(JournalPath, JsonUtility.ToJson(journal, true));
            building = true;
            try
            {
                var result = Transaction(previous, candidate, version =>
                {
                    PlayerSettings.bundleVersion = version;
                    AssetDatabase.SaveAssets();
                }, () =>
                {
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { EFReleaseSetup.TitleScene, EFTitleScreen.GameplayScene },
                        target = BuildTarget.StandaloneWindows64,
                        locationPathName = Path.Combine(folder, "EVERFLOW.exe"),
                        options = BuildOptions.None
                    });
                    File.WriteAllText(Path.Combine(folder, "build-result.txt"), report.summary.result + "\nVersion: " + candidate +
                        "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings);
                    return report.summary.result;
                }, () =>
                {
                    journal.succeeded = true;
                    File.WriteAllText(JournalPath, JsonUtility.ToJson(journal, true));
                });
                Debug.Log("EVERFLOW release " + result + ": " + folder + "; project version " + PlayerSettings.bundleVersion);
            }
            finally
            {
                building = false;
                // Retain the journal if persisting the final settings failed; recover on reload.
                if (PlayerSettings.bundleVersion == (journal.succeeded ? candidate : previous))
                {
                    AssetDatabase.SaveAssets();
                    File.Delete(JournalPath);
                }
            }
        }
    }

    // Applies to ordinary Build/Build And Run as well, but deliberately never increments versions.
    public sealed class EFReleaseSceneGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                EFReleaseSetup.ValidateBuildScenes(EditorBuildSettings.scenes);
                EFReleaseSetup.ValidateScenes();
            }
            catch (InvalidOperationException exception)
            {
                throw new BuildFailedException(exception.Message);
            }
        }
    }
}
