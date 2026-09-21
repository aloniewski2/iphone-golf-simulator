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

        static void Run(TestMode mode)
        {
            Directory.CreateDirectory("Library/TestResults");
            File.Delete(Path(mode));
            File.WriteAllText(Path(mode) + ".running", $"RUN {mode} {System.DateTime.Now:s}\n");
            ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter { testMode = mode }));
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
                // CLI test runs bypass the menu's Run() setup on a fresh checkout.
                Directory.CreateDirectory("Library/TestResults");
                string line = $"{r.TestStatus.ToString().ToUpperInvariant()} {r.FullName} ({r.Duration:F2}s)\n";
                if (r.TestStatus == TestStatus.Failed) line += "    " + (r.Message ?? "").Trim().Replace("\n", "\n    ") + "\n";
                File.AppendAllText(Path(r.Test.TestMode) + ".running", line);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory("Library/TestResults");
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
