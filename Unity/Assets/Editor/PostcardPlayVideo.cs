using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Shot;
using GolfArcade.Swing;

namespace GolfArcade.EditorTools
{
    /// Numbered JPG frames of the Postcards holes played with the real CourseShot and the game's CameraRig
    /// (address, Follow chase, HoldOn), 30 fps portrait. Encode afterwards.
    ///   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.PostcardPlayVideo.Run
    [InitializeOnLoad]
    public static class PostcardPlayVideo
    {
        const string Flag = "PostcardPlayVideo";
        static int warm; static IEnumerator script; static string outDir; static int frame;
        static RenderTexture rt; static Texture2D tex; static Camera cam; static GolfGame game; static CameraRig rig; static Transform ball;

        // hole index, club, power, heading, from x, from d
        static readonly (int, GolfClub, double, double, double, double)[] Shots =
        {
            (0, GolfClub.Driver, 0.80, 5.0, 0, 0), (0, GolfClub.Driver, 0.40, -11.6, 19.0, 217.8),
            (1, GolfClub.Driver, 1.00, 1.8, 0, 0), (1, GolfClub.Driver, 0.80, -4.8, 8.6, 269.5),
            (2, GolfClub.Driver, 0.70, 36.7, 0, 0), (2, GolfClub.Driver, 0.50, 86.1, 115.5, 154.7),
        };

        static PostcardPlayVideo() { EditorApplication.update += Tick; }

        public static void Run()
        {
            outDir = System.Environment.GetEnvironmentVariable("GOLF_VIDEO_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../ArtDir/screenshots/golf_postcards/frames");
            Directory.CreateDirectory(outDir);
            SessionState.SetString(Flag + "out", outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if (script == null)
            {
                if (!HoleView.Current || ++warm < 90) return;
                outDir = SessionState.GetString(Flag + "out", outDir);
                Time.captureFramerate = 30;
                game = Object.FindFirstObjectByType<GolfGame>();
                rig = Object.FindFirstObjectByType<CameraRig>();
                cam = rig.GetComponentInChildren<Camera>();
                ball = (Transform)typeof(GolfGame).GetField("ball", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                rt = new RenderTexture(720, 1280, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(720, 1280, TextureFormat.RGB24, false);
                script = Play();
            }
            if (!script.MoveNext())
            {
                SessionState.SetBool(Flag, false);
                Debug.Log($"[PostcardVideo] wrote {frame} frames to {outDir}");
                if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying = false;
            }
        }

        static void Snap()
        {
            rig.ApplyFrame();
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes($"{outDir}/f_{frame++:D5}.jpg", tex.EncodeToJPG(90));
        }

        static IEnumerator Play()
        {
            int current = -1;
            foreach (var s in Shots)
            {
                var hole = Course.Course.Postcards().Holes[s.Item1];
                if (s.Item1 != current)
                {
                    current = s.Item1;
                    typeof(GolfGame).GetMethod("StartHole", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { current });
                    game.enabled = false; rig.enabled = false;
                    for (int i = 0; i < 20; i++) yield return null;
                    foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                    ball = (Transform)typeof(GolfGame).GetField("ball", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
                    var tr = ball.GetComponent<TrailRenderer>(); if (tr) tr.emitting = false;
                }
                var from = new CoursePoint(s.Item5, s.Item6);
                var lie = hole.LieAt(from);
                var impact = new SwingImpact { Power = s.Item3, Backswing = s.Item3, PeakSpeed = s.Item3 * 16, TempoSeconds = 0.7 };
                var shot = new CourseShot(s.Item2, impact, s.Item4, from, hole, lie.PowerFactor());
                var rest = shot.NextPosition;
                ball.gameObject.SetActive(true);
                ball.position = HoleView.ToWorld(from, 0.06);
                var aim = Quaternion.Euler(0, (float)s.Item4, 0) * Vector3.forward;
                rig.FrameAddress(ball.position, aim, false); rig.SnapNext();
                for (int i = 0; i < 45; i++) { Snap(); yield return null; }
                var last = ball.position;
                for (double t = 0; t < shot.Duration + 0.2; t += 1.0 / 30)
                {
                    var p = shot.PositionAt(t);
                    var pos = HoleView.ToWorld(new CoursePoint(p.x, p.d), p.h + 0.06);
                    ball.position = pos;
                    rig.Follow(pos, (pos - last) * 30f, false); last = pos;
                    Snap(); yield return null;
                }
                var restW = HoleView.ToWorld(shot.Rest, 0.06);
                ball.position = restW;
                rig.HoldOn(restW, HoleView.ToWorld(hole.Pin) - restW, false);
                for (int i = 0; i < 60; i++) { Snap(); yield return null; }
                Debug.Log($"[PostcardVideo] hole {hole.Number} shot from {from} -> rest {shot.Rest} {shot.Lie}");
            }
        }
    }
}
