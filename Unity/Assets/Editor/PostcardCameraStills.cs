using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Course;
using GolfArcade.Game;

namespace GolfArcade.EditorTools
{
    /// Stills of the Postcards holes through the game's own CameraRig (address framing, portrait):
    ///   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.PostcardCameraStills.Run
    /// Output: GOLF_STILLS_OUT (default ArtDir/screenshots/golf_postcards). Not a flyover: a handful of stills.
    [InitializeOnLoad]
    public static class PostcardCameraStills
    {
        const string Flag = "PostcardCameraStills";
        static int warm, holeIndex = -1, wait, viewIndex;
        static string outDir; static RenderTexture rt; static Texture2D tex; static Camera cam;
        static GolfGame game; static CameraRig rig;

        // hole index -> (label, ball x, ball d, aim x, aim d)
        static readonly (string, float, float, float, float)[][] Views =
        {
            new[] { ("tee", 0f, 0f, 19f, 178f), ("layup", 2f, 38f, 19f, 178f), ("midpad", 19f, 178f, -6f, 340f) },
            new[] { ("tee", 0f, 0f, 3f, 60f), ("fairway", 11.8f, 235f, 6f, 416f), ("ridge", -61.2f, 261.7f, -10f, 492f) },
            new[] { ("tee", 0f, 0f, 14.3f, 66.2f), ("rim", 50.2f, 123.4f, 107.1f, 159.3f), ("approach", 107.1f, 159.3f, 261f, 164.7f) },
        };

        static PostcardCameraStills() { EditorApplication.update += Tick; }

        public static void Run()
        {
            outDir = System.Environment.GetEnvironmentVariable("GOLF_STILLS_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../ArtDir/screenshots/golf_postcards");
            Directory.CreateDirectory(outDir);
            SessionState.SetString(Flag + "out", outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if (holeIndex < 0)
            {
                if (!HoleView.Current || ++warm < 90) return;
                outDir = SessionState.GetString(Flag + "out", outDir);
                game = Object.FindFirstObjectByType<GolfGame>();
                rig = Object.FindFirstObjectByType<CameraRig>();
                cam = rig ? rig.GetComponentInChildren<Camera>() : Camera.main;
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                rt = new RenderTexture(900, 1600, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(900, 1600, TextureFormat.RGB24, false);
                holeIndex = -1; Next(); return;
            }
            if (--wait > 0) return;
            var v = Views[holeIndex][viewIndex];
            var hole = Course.Course.Postcards().Holes[holeIndex];
            var ballAt = new CoursePoint(v.Item2, v.Item3);
            var ball = HoleView.ToWorld(ballAt, 0.06);
            var aim = HoleView.ToWorld(new CoursePoint(v.Item4, v.Item5), 0) - HoleView.ToWorld(ballAt, 0);
            aim.y = 0; aim.Normalize();
            game.enabled = false; rig.enabled = false;
            rig.FrameAddress(ball, aim, false); rig.SnapNext(); rig.ApplyFrame();
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes($"{outDir}/hole{hole.Number:00}_{v.Item1}.jpg", tex.EncodeToJPG(93));
            Debug.Log($"[PostcardStills] hole {hole.Number} {v.Item1}");
            if (++viewIndex >= Views[holeIndex].Length) Next(); else wait = 3;
        }

        static void Next()
        {
            viewIndex = 0;
            if (++holeIndex >= Views.Length)
            {
                SessionState.SetBool(Flag, false);
                if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying = false;
                return;
            }
            game.enabled = true;
            typeof(GolfGame).GetMethod("StartHole", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { holeIndex });
            wait = 30;
        }
    }
}
