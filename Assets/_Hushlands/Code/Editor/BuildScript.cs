using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hushlands.Editor
{
    /// <summary>Windows 64-bit player build. Batch: -executeMethod Hushlands.Editor.BuildScript.BuildWindows</summary>
    public static class BuildScript
    {
        [MenuItem("Hushlands/Build/Windows x64")]
        public static void BuildWindows()
        {
            ProjectBootstrap.Run();
            string output = Path.GetFullPath(HushPaths.WindowsBuild);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? ".");

            var options = new BuildPlayerOptions
            {
                scenes = Array.ConvertAll(EditorBuildSettings.scenes, s => s.path),
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log("[Hushlands] Build " + summary.result + " size=" + summary.totalSize +
                      " warnings=" + summary.totalWarnings + " errors=" + summary.totalErrors +
                      " path=" + output);

            if (Application.isBatchMode)
            {
                bool ok = summary.result == BuildResult.Succeeded && summary.totalWarnings == 0;
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
