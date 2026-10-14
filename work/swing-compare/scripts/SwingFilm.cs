#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Shot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// SWING COMPARE film (APFS clone ONLY, never the shared project). Plays the golfer's real in-game swing on the first tee of
    /// Cliffside through the game's own entry points (OnLoad = the phone's backswing, then StrikeToward = the impact, which makes
    /// GolfGame.Launch call GolferView.Strike exactly as a touch / replay swing does) and films it at a fixed 60 fps game clock from
    /// seven cameras every frame: the game's own TV picture plus six body-relative views (face-on, down-the-line, target side,
    /// two three-quarters, overhead).
    ///   SF_OUT=<dir>  SF_SWINGS=male:Driver,female:Driver,male:Iron  SF_BACK=0.80 (backswing seconds)
    ///   Unity -batchmode -projectPath <clone> -executeMethod GolfArcade.EditorTools.SwingFilm.Run     (graphics on, no -quit)
    /// Output: <dir>/<sex>_<club>/<view>_#####.jpg, trace.csv, summary.txt
    [InitializeOnLoad]
    public static class SwingFilm
    {
        const string Flag = "SwingFilm";
        static IEnumerator script; static int lastFrame = -1;
        static SwingFilm() { EditorApplication.update += Tick; }
        static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;
        public static void Run()
        {
            script = null; lastFrame = -1;
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (script == null) { var g = Object.FindFirstObjectByType<GolfGame>(); if (!g) return; script = All(g); }
            try { if (!script.MoveNext()) Done(0); } catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int c)
        {
            SessionState.SetBool(Flag, false); Time.captureFramerate = 0; Time.timeScale = 1;
            if (Application.isBatchMode) EditorApplication.Exit(c); else EditorApplication.isPlaying = false;
        }
        static string F(float x) => x.ToString("0.0000", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => F(v.x) + ";" + F(v.y) + ";" + F(v.z);

        static void Shot(Camera cam, string path, Vector3 from, Vector3 look, Vector3 up, float fov, int w, int h)
        {
            var hidden = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled && c.renderMode != RenderMode.WorldSpace).ToList();
            foreach (var c in hidden) c.enabled = false;
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView; var a = cam.aspect;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from, up)); cam.fieldOfView = fov; cam.aspect = w / (float)h;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var act = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToJPG(92)); Object.DestroyImmediate(tex);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f; cam.aspect = a;
            foreach (var c in hidden) c.enabled = true;
        }

        static IEnumerator WaitFor(Func<bool> done, int frames, string what, Action each = null)
        {
            for (int i = 0; !done(); i++) { if (i > frames) throw new TimeoutException("waiting for " + what); each?.Invoke(); yield return null; }
        }

        static readonly BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        static readonly MethodInfo OnLoad = typeof(GolfGame).GetMethod("OnLoad", Any);
        static readonly MethodInfo SelectClub = typeof(GolfGame).GetMethod("SelectClub", Any);
        static readonly FieldInfo BallField = typeof(GolfGame).GetField("ball", Any);
        static readonly FieldInfo ClubField = typeof(GolfGame).GetField("club", Any);
        static readonly FieldInfo GvTime = typeof(GolferView).GetField("time", Any);
        static readonly FieldInfo GvPhase = typeof(GolferView).GetField("phase", Any);
        static readonly FieldInfo GvClip = typeof(GolferView).GetField("clipName", Any);
        static readonly FieldInfo GvTop = typeof(GolferView).GetField("TopTime", Any);
        static readonly FieldInfo GvImpact = typeof(GolferView).GetField("ImpactTime", Any);
        static readonly FieldInfo GvEnd = typeof(GolferView).GetField("EndTime", Any);
        static readonly FieldInfo GvDown = typeof(GolferView).GetField("Downswing", Any);

        static IEnumerator All(GolfGame game)
        {
            string root = Path.GetFullPath(Env("SF_OUT", "sf_out")); Directory.CreateDirectory(root);
            var savedBody = GolferStyle.Body; int savedHoles = PlayerPrefs.GetInt("holes", 0); string savedCourse = PlayerPrefs.GetString("course", "cliffside");
            var report = new List<string>();
            try
            {
                Time.captureFramerate = 60; Time.timeScale = 1; UnityEngine.Random.InitState(3108);
                GolfGame.LoadCoverMinimum = 0.5f; game.InstantReplays = false;
                for (int i = 0; i < 10; i++) yield return null;
                foreach (var spec in Env("SF_SWINGS", "male:Driver").Split(','))
                {
                    var parts = spec.Split(':'); bool female = parts[0] == "female"; var club = (GolfClub)Enum.Parse(typeof(GolfClub), parts[1]);
                    var e = OneSwing(game, root, female, club, report);
                    while (true)
                    {
                        bool more;
                        try { more = e.MoveNext(); }
                        catch (Exception ex) { report.Add($"{spec}: FAILED {ex.GetType().Name}: {ex.Message}"); Debug.LogException(ex); break; }
                        if (!more) break;
                        yield return e.Current;
                    }
                    File.WriteAllLines(Path.Combine(root, "summary.txt"), report);
                }
            }
            finally
            {
                GolferStyle.Body = savedBody; PlayerPrefs.SetInt("holes", savedHoles); PlayerPrefs.SetString("course", savedCourse); PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(root, "summary.txt"), report);
                Debug.Log("[SwingFilm] " + string.Join(" | ", report));
            }
        }

        static IEnumerator OneSwing(GolfGame game, string root, bool female, GolfClub club, List<string> report)
        {
            string tag = (female ? "female" : "male") + "_" + club;
            string dir = Path.Combine(root, tag); Directory.CreateDirectory(dir);
            if (Env("SF_KEEP", "0") != "1") foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
            var golfer = game.GetComponentInChildren<GolferView>(true); golfer.ApplyStyle();
            game.ChooseHoles(0, "cliffside");
            yield return null;
            if (game.Current != GolfGame.State.Menu) throw new InvalidOperationException("not at the menu: " + game.Current);
            game.Play();
            var wait = WaitFor(() => game.Current == GolfGame.State.Aim, 60 * 90, "the first tee", () => { if (game.PresentationBeatIndex == 0) game.SkipPresentation(); });
            while (wait.MoveNext()) yield return null;
            game.SetWind(new Wind(0, 0));
            golfer = game.GetComponentInChildren<GolferView>(true);
            if (!golfer.IsHero) throw new InvalidOperationException("golfer is not the Hero");
            if (club != GolfClub.Driver) SelectClub.Invoke(game, new object[] { club });
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            foreach (var smr in golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            for (int i = 0; i < 40; i++) { cam.aspect = 16f / 9f; yield return null; }
            var hole = game.CurrentHole;
            var ball = BallField.GetValue(game) as Transform;

            // body-relative cameras. GolferView.Stand: the golfer faces `face` (across the line), the ball is 0.75 m in front of him
            var g = golfer.transform.position; var face = golfer.transform.forward; face.y = 0; face.Normalize();
            var aim = Vector3.Cross(face, Vector3.up).normalized;           // toward the target
            float ground = ball ? ball.position.y : g.y;
            var c = new Vector3(g.x, ground, g.z) + face * 0.30f;           // between his feet and the ball
            var look = c + Vector3.up * 1.0f;
            const float D = 5.2f, H = 1.15f, Fov = 38f;
            Vector3 At(Vector3 dir, float h = H) { dir.y = 0; return c + dir.normalized * D + Vector3.up * h; }
            var views = new (string name, Vector3 from, Vector3 look, Vector3 up)[]
            {
                ("faceon", At(face), look, Vector3.up),                       // in front of him, across the ball (Adam Scott clip)
                ("dtl", At(-aim) + face * 0.35f, look + face * 0.35f, Vector3.up),  // down the line: behind the ball, looking at the target
                ("target", At(aim) + face * 0.35f, look + face * 0.35f, Vector3.up), // from the target looking back
                ("q_front_trail", At(face - aim), look, Vector3.up),          // three-quarter, front + away from the target (broadcast tee cam)
                ("q_front_target", At(face + aim), look, Vector3.up),         // three-quarter, front + toward the target
                ("overhead", c + Vector3.up * 6.4f, c + Vector3.up * 0.4f, face),
                // the TV tee camera of the reference clips: behind the golfer, 25 deg round toward the ball side, long lens
                ("broadcast", c + (-aim * Mathf.Cos(25 * Mathf.Deg2Rad) + face * Mathf.Sin(25 * Mathf.Deg2Rad)) * 8.5f + Vector3.up * 1.45f, look + face * 0.2f, Vector3.up),
            };
            var only = Env("SF_VIEWS", "");
            if (only != "") views = views.Where(v => only.Split(',').Contains(v.name)).ToArray();
            bool gameShot = only == "" || only.Split(',').Contains("game");
            string clipName = null; float top = 0, impact = 0, end = 0, down = 0;
            var rows = new List<string> { "frame,t,stage,load,phase,clip,clipTime,clubHead,ball" };
            // the body, for the swing measurements (positions relative to c; aim = +x of nothing, so the frame vectors go in too)
            string[] boneNames = { "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftUpperArm", "RightUpperArm", "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand",
                                   "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "LeftToes", "RightToes" };
            var allT = golfer.GetComponentsInChildren<Transform>(true);
            var bones = boneNames.Select(n => (n, t: allT.FirstOrDefault(t => t.name == n))).Where(b => b.t).ToList();
            var boneRows = new List<string> { "frame," + string.Join(",", bones.Select(b => b.n)) };
            File.WriteAllText(dir + "/frame.txt", $"c={V(c)} face={V(face)} aim={V(aim)} up=0;1;0\nbones={string.Join(" ", bones.Select(b => b.n))}\n");
            float back = float.Parse(Env("SF_BACK", "0.80"), CultureInfo.InvariantCulture);
            int addr = 30, backF = Mathf.RoundToInt(back * 60), hold = 3, after = 150;
            int total = addr + backF + hold + after, strikeFrame = addr + backF + hold;
            var target = hole.RecommendedTarget(hole.Tee);
            for (int k = 0; k < total; k++)
            {
                string stage = k < addr ? "address" : k < addr + backF ? "backswing" : k < strikeFrame ? "top" : "through";
                float load = 0;
                if (stage == "backswing") { float u = (k - addr + 1f) / backF; load = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * u); }
                if (stage == "top") load = 1;
                if (stage == "backswing" || stage == "top") OnLoad.Invoke(game, new object[] { (double)load });
                if (k == strikeFrame)
                {
                    if (game.Current != GolfGame.State.Aim) throw new InvalidOperationException("left Aim before the strike: " + game.Current);
                    game.StrikeToward(target);
                }
                cam.aspect = 16f / 9f;
                yield return null;
                if (clipName == null)
                {
                    clipName = (string)GvClip.GetValue(golfer); top = (float)GvTop.GetValue(golfer); impact = (float)GvImpact.GetValue(golfer);
                    end = (float)GvEnd.GetValue(golfer); down = (float)GvDown.GetValue(golfer);
                }
                string id = k.ToString("00000");
                if (gameShot) GameCapture.Save($"{dir}/game_{id}.jpg", 1280, 720);
                foreach (var v in views) Shot(cam, $"{dir}/{v.name}_{id}.jpg", v.from, v.look, v.up, v.name == "overhead" ? 30f : v.name == "broadcast" ? 24f : Fov, 720, 960);
                rows.Add(string.Join(",", k, F(k / 60f), stage, F(load), GvPhase.GetValue(golfer), GvClip.GetValue(golfer), F((float)GvTime.GetValue(golfer)),
                    golfer.ClubHeadWorld() is Vector3 h ? V(h - c) : "", ball ? V(ball.position - c) : ""));
                boneRows.Add(k + "," + string.Join(",", bones.Select(b => V(b.t.position - c))));
            }
            File.WriteAllText(dir + "/bones.csv", string.Join("\n", boneRows) + "\n");
            File.WriteAllText(dir + "/trace.csv", string.Join("\n", rows) + "\n");
            string line = $"{tag}: clip={clipName} club={ClubField.GetValue(game)} top={F(top)} impact={F(impact)} end={F(end)} downswingSeconds={F(down)} " +
                          $"address={addr}f backswing={backF}f hold={hold}f strikeFrame={strikeFrame} frames={total} state={game.Current} hole={hole.Number}";
            report.Add(line); Debug.Log("[SwingFilm] " + line);
        }
    }
}
#endif
