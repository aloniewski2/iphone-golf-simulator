using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.EditorTools
{
    /// Renders a scripted camera flyover of the Cliffside hole to numbered JPG frames:
    ///   GOLF_FLY_OUT=/some/dir Unity -projectPath Unity -executeMethod GolfArcade.EditorTools.GolfFlyoverCapture.Run
    /// GOLF_FLY_SECONDS (default 48), GOLF_FLY_FPS (default 30). Encode the frames afterwards.
    [InitializeOnLoad]
    public static class GolfFlyoverCapture
    {
        const string Flag = "GolfFlyoverCapture";
        static int warm, frame, total;
        static RenderTexture rt; static Texture2D tex; static Camera cam; static string outDir;

        // (x, height above ground, z, lookX, lookHeight, lookZ, fov, seconds-to-reach)
        static readonly float[][] Keys =
        {
            new[]{ -70f, 90f,-150f,   0f, 5f, 160f, 52f, 0f },   // establishing, high behind the tee
            new[]{ -35f, 45f, -80f,   0f, 5f, 180f, 50f, 5f },
            new[]{  12f,  7f, -30f,   0f, 4f, 110f, 62f, 9f },   // low over the tee box
            new[]{ -22f, 16f,  40f,  -5f, 2f, 190f, 58f, 13f },  // down the fairway, left
            new[]{  20f, 22f, 130f,  -5f, 2f, 230f, 56f, 18f },
            new[]{ -10f, 14f, 215f, -35f, 0f, 300f, 56f, 22f },  // toward the left-hand bunkers
            new[]{  10f, 10f, 262f, -38f, 0f, 318f, 60f, 26f },  // low across the bunkers
            new[]{  60f, 20f, 330f,  18f, 0f, 415f, 54f, 30f },  // approach the green
            new[]{  70f, 20f, 440f,  18f, 0f, 415f, 50f, 34f },  // swing round the green
            new[]{  18f, 18f, 485f,  18f, 0f, 410f, 50f, 38f },
            new[]{ -45f, 20f, 430f,  10f, 0f, 380f, 52f, 41f },
            new[]{ 150f, 55f, 340f,   5f, 0f, 250f, 56f, 45f },  // out over the east cliffs and ocean
            new[]{ 190f,120f, 120f,   0f, 0f, 250f, 58f, 48f },  // final pull-back
        };

        static GolfFlyoverCapture() { EditorApplication.update += Tick; }

        public static void Run()
        {
            // Proof-only stills branch: preserve the original Cliffside movie path
            // below. The existing still runner owns the actual StartHole, lighting,
            // fixed portrait Game camera and asynchronous session state.
            string stillsCfg = System.Environment.GetEnvironmentVariable("GOLF_FLY_STILLS_CFG");
            if (!string.IsNullOrEmpty(stillsCfg))
            {
                string stillsOut = System.Environment.GetEnvironmentVariable("GOLF_FLY_OUT");
                if (!File.Exists(Path.GetFullPath(stillsCfg)) || string.IsNullOrEmpty(stillsOut))
                {
                    Debug.LogError("[GolfFly] stills branch requires an existing GOLF_FLY_STILLS_CFG and explicit GOLF_FLY_OUT");
                    if (Application.isBatchMode) EditorApplication.Exit(1);
                    return;
                }
                string previousCfg = System.Environment.GetEnvironmentVariable("GOLF_STILLS_CFG");
                string previousOut = System.Environment.GetEnvironmentVariable("GOLF_STILLS_OUT");
                SessionState.SetBool(Flag, false);
                try
                {
                    System.Environment.SetEnvironmentVariable("GOLF_STILLS_CFG", Path.GetFullPath(stillsCfg));
                    System.Environment.SetEnvironmentVariable("GOLF_STILLS_OUT", Path.GetFullPath(stillsOut));
                    Debug.Log("[GolfFly] configured portrait stills through PostcardLookStills.Run: " + Path.GetFullPath(stillsCfg) + " -> " + Path.GetFullPath(stillsOut) + "; actual GolfGame/CameraRig/atmosphere at 900x1600");
                    PostcardLookStills.Run();
                }
                finally
                {
                    // Run stores these values in its own SessionState before entering
                    // play mode; restoring the process env cannot change the queue.
                    System.Environment.SetEnvironmentVariable("GOLF_STILLS_CFG", previousCfg);
                    System.Environment.SetEnvironmentVariable("GOLF_STILLS_OUT", previousOut);
                }
                return;
            }
            outDir = System.Environment.GetEnvironmentVariable("GOLF_FLY_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../ArtDir/screenshots/golf_flyover/frames");
            Directory.CreateDirectory(outDir);
            SessionState.SetString(Flag + "out", outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static float Env(string n, float d) { var s = System.Environment.GetEnvironmentVariable(n); return float.TryParse(s, out var v) ? v : d; }

        static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            => 0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t * t * t);

        static float Ease(float t) => t * t * (3 - 2 * t);

        static void Sample(float time, float seconds, out Vector3 pos, out Vector3 look, out float fov)
        {
            time *= Keys[Keys.Length - 1][7] / seconds;
            int i = 0; while (i < Keys.Length - 2 && time > Keys[i + 1][7]) i++;
            float t = Mathf.Clamp01((time - Keys[i][7]) / (Keys[i + 1][7] - Keys[i][7]));
            Vector3 P(int k, int off) { k = Mathf.Clamp(k, 0, Keys.Length - 1); var a = Keys[k]; return new Vector3(a[off], a[off + 1], a[off + 2]); }
            pos = CR(P(i - 1, 0), P(i, 0), P(i + 1, 0), P(i + 2, 0), t);
            look = CR(P(i - 1, 3), P(i, 3), P(i + 1, 3), P(i + 2, 3), t);
            fov = Mathf.Lerp(Keys[i][6], Keys[i + 1][6], Ease(t));
            // heights are above the ground
            float gp = (float)HoleView.GroundHeight(new CoursePoint(pos.x, pos.z));
            float gl = (float)HoleView.GroundHeight(new CoursePoint(look.x, look.z));
            pos.y = Mathf.Max(pos.y + gp, gp + 3f);
            look.y += gl;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if (frame == 0 && total == 0)
            {
                if (!HoleView.Current || ++warm < 90) return;
                Setup();
            }
            if (frame >= total)
            {
                SessionState.SetBool(Flag, false);
                Debug.Log($"[GolfFly] wrote {frame} frames to {outDir}");
                if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying = false;
                return;
            }
            float seconds = total / Env("GOLF_FLY_FPS", 30f);
            Sample(frame / Env("GOLF_FLY_FPS", 30f), seconds, out var p, out var l, out var f);
            cam.transform.position = p; cam.transform.rotation = Quaternion.LookRotation(l - p, Vector3.up); cam.fieldOfView = f;
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
            File.WriteAllBytes($"{outDir}/f_{frame:D5}.jpg", tex.EncodeToJPG(93));
            frame++;
        }

        static void Setup()
        {
            outDir = SessionState.GetString(Flag + "out", outDir);
            var game = Object.FindFirstObjectByType<GolfArcade.Game.GolfGame>();
            if (game) game.enabled = false;
            var rig = Object.FindFirstObjectByType<GolfArcade.Game.CameraRig>();
            cam = rig ? rig.GetComponentInChildren<Camera>() : Camera.main;
            if (!cam) cam = Camera.main;
            if (rig) rig.enabled = false;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            int w = (int)Env("GOLF_FLY_W", 1920), h = (int)Env("GOLF_FLY_H", 1080);
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 2000);
            RenderSettings.fogStartDistance = 500; RenderSettings.fogEndDistance = 1600; // see the whole island
            total = (int)(Env("GOLF_FLY_SECONDS", 48f) * Env("GOLF_FLY_FPS", 30f));
            Debug.Log($"[GolfFly] cam={cam.name} frames={total} {w}x{h}");
        }
    }
}
