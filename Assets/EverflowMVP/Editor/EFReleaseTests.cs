using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EverflowMVP.Editor
{
    public static class EFReleaseTests
    {
        [MenuItem("Tools/EVERFLOW/Release/Run Version Transaction Tests")]
        public static void Run()
        {
            void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); }
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
