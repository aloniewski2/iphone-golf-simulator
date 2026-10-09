using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GolfArcade.EditorTools {
    // Export the current project as-is after controller/runtime changes. Do not run
    // venue importers or change scene, art or player settings during this build.
    public static class ControllerIOSExport {
        public static void Run() {
            int exit=1;
            try {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                    locationPathName=BuildMenu.OutputPath,target=BuildTarget.iOS,
                    options=Directory.Exists(Path.Combine(BuildMenu.OutputPath,"Unity-iPhone.xcodeproj"))
                        ? BuildOptions.AcceptExternalModificationsToPlayer : BuildOptions.None
                });
                Directory.CreateDirectory("Library/BuildResults");
                File.WriteAllText("Library/BuildResults/controller-ios.txt",
                    $"{report.summary.result} errors={report.summary.totalErrors} seconds={report.summary.totalTime.TotalSeconds:F0}\n");
                exit=report.summary.result==BuildResult.Succeeded ? 0 : 1;
            } catch(Exception error) { Debug.LogException(error); }
            if(Application.isBatchMode) EditorApplication.Exit(exit);
        }
    }
}
