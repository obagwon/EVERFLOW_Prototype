using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EverflowMVP.Editor
{
    // Independent read-only release check entry point. Never calls Configure or saves scenes.
    public static class EFReleaseIntegrationVerification
    {
        [Serializable]
        sealed class CheckResult
        {
            public string name;
            public bool passed;
            public string detail;
        }

        [Serializable]
        sealed class Report
        {
            public string kind = "Unity Editor batch checks; not Play mode or a Windows player build";
            public string unityVersion;
            public List<CheckResult> checks = new List<CheckResult>();
            public bool passed = true;
        }

        public static void Run()
        {
            var report = new Report { unityVersion = Application.unityVersion };
            void Check(string name, Action action)
            {
                try
                {
                    action();
                    report.checks.Add(new CheckResult { name = name, passed = true, detail = "PASS" });
                    Debug.Log("INDEPENDENT PASS: " + name);
                }
                catch (Exception exception)
                {
                    report.passed = false;
                    report.checks.Add(new CheckResult { name = name, passed = false, detail = exception.ToString() });
                    Debug.LogError("INDEPENDENT FAIL: " + name + "\n" + exception);
                }
            }

            Check("Current saved effective Build Settings accepted by ordinary-build scene guard",
                () => EFReleaseSetup.ValidateBuildScenes(EditorBuildSettings.scenes));
            Check("Preview-scene title and exhibition map serialized references", EFReleaseSetup.ValidateScenes);
            Check("Release regression tests", EFReleaseTests.RunIntegration);
            Directory.CreateDirectory("docs/release-integration");
            File.WriteAllText("docs/release-integration/verification-unity-results.json", JsonUtility.ToJson(report, true));
            if (!report.passed) throw new Exception("Independent release integration verification failed; see verification-unity-results.json.");
            Debug.Log("INDEPENDENT RELEASE INTEGRATION VERIFICATION PASS. Playthrough and player build remain unverified.");
        }
    }
}
