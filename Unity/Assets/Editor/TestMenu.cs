using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Runs the test suites from the menu and writes a plain-text report to
    /// Library/TestResults/<Mode>.txt, so a script (or Claude) can kick off and read a PlayMode
    /// run in an editor that already has the project open — where -runTests cannot start.
    /// The reporter is registered on every domain load: entering play mode reloads the domain,
    /// so anything registered only when the run was started would miss the results.
    [InitializeOnLoad]
    public static class TestMenu
    {
        static TestMenu()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Report());
        }

        [MenuItem("Golf Arcade/Run EditMode Tests")] public static void RunEditMode() => Run(TestMode.EditMode);
        [MenuItem("Golf Arcade/Run PlayMode Tests")] public static void RunPlayMode() => Run(TestMode.PlayMode);
        /// Hole 12 played by script and recorded to Library/Captures/demo (DemoVideoTests);
        /// Tools/demo_video.sh turns it into the MP4.
        [MenuItem("Golf Arcade/Record Putting Demo")] public static void RecordPuttingDemo() => Run(TestMode.PlayMode, "GolfArcade.PlayTests.DemoVideoTests.RecordsPutting");
        [MenuItem("Golf Arcade/Record Demo Video")] public static void RecordDemo() => Run(TestMode.PlayMode, "GolfArcade.PlayTests.DemoVideoTests.RecordsHoleTwelve");

        /// The PlayMode tests named in Library/TestResults/chosen.txt (full names, one a line):
        /// one suite at a time while working on it, instead of the whole run.
        [MenuItem("Golf Arcade/Run Chosen PlayMode Tests")]
        public static void RunChosen()
        {
            var names = File.Exists("Library/TestResults/chosen.txt") ? File.ReadAllLines("Library/TestResults/chosen.txt") : new string[0];
            names = System.Array.FindAll(names, n => n.Trim().Length > 0);
            if (names.Length == 0) { Debug.LogWarning("no tests in Library/TestResults/chosen.txt"); return; }
            Run(TestMode.PlayMode, names);
        }

        static void Run(TestMode mode, params string[] tests)
        {
            Directory.CreateDirectory("Library/TestResults");
            File.Delete(Path(mode));
            File.WriteAllText(Path(mode) + ".running", $"RUN {mode} {System.DateTime.Now:s}\n");
            var filter = new Filter { testMode = mode };
            if (tests != null && tests.Length > 0) filter.testNames = tests;
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(filter));
        }

        static string Path(TestMode mode) => $"Library/TestResults/{mode}.txt";

        /// Appends each result as it lands (survives the reload), then seals the file.
        sealed class Report : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                string line = $"{r.TestStatus.ToString().ToUpperInvariant()} {r.FullName} ({r.Duration:F2}s)\n";
                if (r.TestStatus == TestStatus.Failed) line += "    " + (r.Message ?? "").Trim().Replace("\n", "\n    ") + "\n";
                // a leaf test's own TestMode is often unset: file it with the run in progress
                var mode = r.Test.TestMode;
                if (mode != TestMode.EditMode && mode != TestMode.PlayMode)
                    mode = File.Exists(Path(TestMode.PlayMode) + ".running") ? TestMode.PlayMode : TestMode.EditMode;
                File.AppendAllText(Path(mode) + ".running", line);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                var mode = result.Test.TestMode;
                int passed = result.PassCount, failed = result.FailCount, skipped = result.SkipCount + result.InconclusiveCount;
                string summary = $"DONE {mode}: {passed} passed, {failed} failed, {skipped} skipped";
                File.AppendAllText(Path(mode) + ".running", summary + "\n");
                File.Delete(Path(mode));
                File.Move(Path(mode) + ".running", Path(mode));
                if (failed > 0) Debug.LogError(summary); else Debug.Log(summary);
            }
        }
    }
}
