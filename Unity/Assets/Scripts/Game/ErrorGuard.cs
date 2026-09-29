using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Whatever goes wrong while the game runs is written down on the device and passed on, so
    /// the game can put itself right rather than freeze. Every exception and error Unity logs —
    /// from any thread, from an async call nobody awaited — goes to errors.log in the app's data
    /// (the last 256 KB of it; on the phone, pull it with `xcrun devicectl device copy from
    /// --domain-type appDataContainer --domain-identifier com.aloniewski.iphonegolfsim
    /// --source Documents/errors.log`), and exceptions raise `Caught` on the main thread.
    public static class ErrorGuard
    {
        public const long MaxLogBytes = 256 * 1024;
        static readonly object gate = new();
        static string path;
        static string pending;
        static int count;

        /// An exception was thrown somewhere (its message), raised on the main thread.
        public static event Action<string> Caught;
        /// Exceptions seen since the game started.
        public static int Count => count;

        public static string LogPath => path ??= Path.Combine(Application.persistentDataPath, "errors.log");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            path = Path.Combine(Application.persistentDataPath, "errors.log");
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
            TaskScheduler.UnobservedTaskException += OnUnobserved;
            Write("start", $"Golf Arcade {Application.version} on {SystemInfo.deviceModel}, {SystemInfo.operatingSystem}");
        }

        static void OnUnobserved(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            Write("task", e.Exception?.ToString() ?? "(no exception)");
            Flag(e.Exception?.GetBaseException().Message ?? "background task failed");
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type is not (LogType.Exception or LogType.Error or LogType.Assert)) return;
            Write(type.ToString().ToLowerInvariant(), message + "\n" + stack);
            if (type == LogType.Exception) Flag(message);
        }

        static void Flag(string message)
        {
            lock (gate) { pending = message; count++; }
        }

        /// Called each frame from the main thread: hands on the latest exception, if any.
        public static void Pump()
        {
            string message;
            lock (gate) { message = pending; pending = null; }
            if (message != null) Caught?.Invoke(message);
        }

        /// Appends to the log, keeping it to its size (the newest half when it is full).
        public static void Write(string kind, string text)
        {
            try
            {
                lock (gate)
                {
                    var p = LogPath;
                    File.AppendAllText(p, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}: {text}\n");
                    var info = new FileInfo(p);
                    if (info.Length > MaxLogBytes) Trim(p);
                }
            }
            catch (Exception) { }   // (a log that can't be written must not be another error)
        }

        static void Trim(string p)
        {
            var text = File.ReadAllText(p);
            int keep = (int)(MaxLogBytes / 2);
            int cut = text.Length - keep;
            int newline = text.IndexOf('\n', Math.Max(0, cut));
            File.WriteAllText(p, "(earlier entries trimmed)\n" + (newline >= 0 ? text.Substring(newline + 1) : text.Substring(Math.Max(0, cut))));
        }
    }
}
