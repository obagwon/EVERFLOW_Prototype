using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EverflowGraybox.Editor
{
    public static class EFGrayboxBuild
    {
        [MenuItem("Tools/EVERFLOW/Graybox/Build Windows Test Player")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows Build Support is not installed.");
            Directory.CreateDirectory("Build/Graybox");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { EFGrayboxBuilder.ScenePath },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = "Build/Graybox/EVERFLOW_Graybox.exe",
                options = BuildOptions.Development
            });
            string summary = $"{result.summary.result}\nErrors: {result.summary.totalErrors}\nWarnings: {result.summary.totalWarnings}\nScene: {EFGrayboxBuilder.ScenePath}\n";
            Directory.CreateDirectory("docs/graybox-captures");
            File.WriteAllText("docs/graybox-captures/build-test-results.txt",summary);
            if (result.summary.result != BuildResult.Succeeded) throw new Exception(summary);
            Debug.Log("GRAYBOX WINDOWS BUILD PASS\n"+summary);
        }
    }
}
