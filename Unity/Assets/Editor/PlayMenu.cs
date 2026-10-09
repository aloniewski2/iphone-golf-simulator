using System;
using System.IO;
using GolfArcade.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Menu shortcuts for iterating without touching the mouse: open the Golf scene and play,
    /// capture what the phone would show to a PNG, and fire the synthetic swing. All of it works
    /// when driven from the menu bar by a script.
    public static class PlayMenu
    {
        const string ScenePath = "Assets/Scenes/Golf.unity";

        [MenuItem("Golf Arcade/Play Golf Scene")]
        public static void PlayPortrait()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        /// Writes what the phone would show to Library/Captures/<timestamp>.png (play mode only).
        [MenuItem("Golf Arcade/Capture Game View")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Capture needs play mode."); return; }
            string path = GameCapture.Save($"Library/Captures/{DateTime.Now:HHmmss}.png");
            var game = UnityEngine.Object.FindFirstObjectByType<GolfGame>();
            Debug.Log($"Captured {path} frame {Time.frameCount} t={Time.time:F1} state={game?.Current}");
        }

        /// A held synthetic backswing released after 0.7 s, like the PlayMode smoke test.
        [MenuItem("Golf Arcade/Debug Swing (synthetic)")]
        public static void DebugSwing()
        {
            var game = UnityEngine.Object.FindFirstObjectByType<GolfGame>();
            if (game?.Swing?.Synthetic == null) { Debug.LogWarning("No synthetic swing running."); return; }
            game.Swing.Synthetic.Backswing(true);
            double release = EditorApplication.timeSinceStartup + 0.7;
            void Tick()
            {
                if (EditorApplication.timeSinceStartup < release) return;
                EditorApplication.update -= Tick;
                game.Swing.Synthetic.Backswing(false);
            }
            EditorApplication.update += Tick;
        }
    }
}
