using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Builds the iOS Xcode project to Builds/iOS and writes a one-line verdict to
    /// Library/BuildResults/ios.txt, so the build can be kicked off from the menu bar by a
    /// script and its outcome read back. Sign and install with xcodebuild/devicectl after.
    public static class BuildMenu
    {
        public const string OutputPath = "Builds/iOS";

        [MenuItem("Golf Arcade/Build iOS Xcode Project")]
        public static void BuildIOS()
        {
            Directory.CreateDirectory("Library/BuildResults");
            File.Delete("Library/BuildResults/ios.txt");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            var summary = report.summary;
            string verdict = $"{summary.result} {summary.totalTime.TotalSeconds:F0}s errors={summary.totalErrors} warnings={summary.totalWarnings} -> {summary.outputPath}";
            foreach (var step in report.steps)
                foreach (var m in step.messages)
                    if (m.type == LogType.Error || m.type == LogType.Exception) verdict += "\n  " + m.content.Trim();
            File.WriteAllText("Library/BuildResults/ios.txt", verdict + "\n");
            Debug.Log("iOS build: " + verdict);
        }
    }
}
