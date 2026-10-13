#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using GolfArcade.Hub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// EMOTE STANDARD film (APFS clone ONLY, never the shared project). Plays one new emote through each game's own entry point and films it at 30 fps
    /// from 60 fps game time.  In the clone the new tennis clip sits in the Spike slot (Male_Emote_Spike.fbx replaced, meta lastFrame edited) and the golf
    /// variant replaces the golfer's "Emote_Spike" clip at run time, so both games use their real taunt / result-emote code paths.
    ///   JF_MODE=tennis|golf  JF_OUT=<dir>  JF_GOLF_CLIP=Assets/EmoteStd/Male_Emote_JumpJoy_Golf.fbx  JF_HAPPY=1
    ///   Unity -batchmode -projectPath <clone> -executeMethod GolfArcade.EditorTools.JoyFilm.Run     (graphics on, no -quit)
    /// Output: <dir>/game_*.jpg (the game's own picture, TV 1280x720), front_*.jpg / side_*.jpg (body-relative 720x720), trace.csv, summary.txt
    [InitializeOnLoad]
    public static class JoyFilm
    {
        const string Flag = "JoyFilm";
        static IEnumerator script; static int lastFrame = -1;
        static JoyFilm() { EditorApplication.update += Tick; }
        static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;
        static bool Golf => Env("JF_MODE", "tennis").StartsWith("golf");
        public static void Run()
        {
            script = null; lastFrame = -1;
            if (Env("JF_MODE", "") == "huball") EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // the session loads the plaza itself, as on the phone
            else EditorSceneManager.OpenScene(Golf ? "Assets/Scenes/Golf.unity" : "Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (script == null)
            {
                string mode = Env("JF_MODE", "tennis");
                if (mode == "huball") script = HubAllFilm();
                else if (Golf) { var g = Object.FindFirstObjectByType<GolfGame>(); if (!g) return; script = mode == "golfaim" ? GolfAimFilm(g) : mode == "golfall" ? GolfAllFilm(g) : GolfFilm(g); }
                else { var t = Object.FindFirstObjectByType<TennisGame>(); if (!t || !t.Initialized) return; script = mode == "tennisall" ? TennisAllFilm(t) : mode == "closeups" ? CloseupFilm(t) : TennisFilm(t); }
            }
            try { if (!script.MoveNext()) Done(0); } catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int c)
        {
                        SessionState.SetBool(Flag, false); Time.captureFramerate = 0; Time.timeScale = 1;
            if (Application.isBatchMode) EditorApplication.Exit(c); else EditorApplication.isPlaying = false;
        }
        static string F(float x) => x.ToString("0.0000", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => F(v.x) + ";" + F(v.y) + ";" + F(v.z);

        static void Shot(Camera cam, string path, Vector3 from, Vector3 look, float fov, int w, int h)
        {
            var hidden = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled && c.renderMode != RenderMode.WorldSpace).ToList();
            foreach (var c in hidden) c.enabled = false;
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView; var a = cam.aspect;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov; cam.aspect = w / (float)h;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var act = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToJPG(92)); Object.DestroyImmediate(tex);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f; cam.aspect = a;
            foreach (var c in hidden) c.enabled = true;
        }

        /// The camera's own picture (no HUD), as it stands.
        static void Plain(Camera cam, string path, int w, int h)
        {
            var a = cam.aspect; var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.aspect = w / (float)h; cam.Render(); cam.targetTexture = prev; cam.aspect = a;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var act = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToJPG(92)); Object.DestroyImmediate(tex);
        }

        /// front (facing the body) and right-side views, body-relative: `ground` = feet point, `fwd` = where the body faces, `k` = world units per metre
        static void Views(Camera cam, string dir, string id, Vector3 ground, Vector3 fwd, float k)
        {
            fwd.y = 0; fwd.Normalize(); var right = Vector3.Cross(Vector3.up, fwd);
            var look = ground + Vector3.up * (1.15f * k);
            Shot(cam, $"{dir}/front_{id}.jpg", ground + (fwd * 5.4f + right * 1.1f + Vector3.up * 1.35f) * k, look, 40, 720, 720);
            Shot(cam, $"{dir}/side_{id}.jpg", ground + (right * 5.4f + Vector3.up * 1.25f) * k, look, 40, 720, 720);
        }

        static Vector3 Facing(Transform l, Transform r) { var v = Vector3.Cross(Vector3.up, l.position - r.position); v.y = 0; return v.normalized; }

        // ------------------------------------------------------------------ tennis
        static void Hold(TennisGame game)
        {
            typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game, new object[] { TennisGame.Phase.PointOver });
            typeof(TennisGame).GetField("resetTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, 1e6f);
        }

        static IEnumerator TennisFilm(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("JF_FEMALE", "0") == "1"; string rival = female ? "Viktor" : "Nadia";
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 90; i++) yield return null;
            var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var actor = game.Player; var m = pd.matchLook;
            foreach (var skin in m.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            Hold(game);
            for (int i = 0; i < 50; i++) yield return null;
            var rows = new List<string> { "frame,clip,clipTime,emoteActive,hips,lhand,rhand,head,racketLowY" };
            float k = (m.Bone(HumanBodyBones.Head).position.y - actor.transform.position.y) / 1.62f;
            var fwd = Facing(m.Bone(HumanBodyBones.LeftUpperArm), m.Bone(HumanBodyBones.RightUpperArm));
            // a short READY lead-in, then the emote through the game's own taunt entry point
            int n = 0, tail = 0, guard = 0; bool seen = false, started = false;
            while (guard++ < 60 * 8)
            {
                if (guard == 21) { started = pd.PlayTaunt("Spike"); }
                cam.aspect = 16f / 9f;
                yield return null;
                if (pd.EmoteActive) seen = true;
                if (seen && !pd.EmoteActive)                 if (guard % 2 == 0)
                {
                    string id = (n++).ToString("00000"); var root = actor.transform;
                    GameCapture.Save($"{dir}/game_{id}.jpg", 1280, 720);
                    Views(cam, dir, id, root.position, fwd, k);
                    Vector3 Loc(Transform t) => root.InverseTransformPoint(t.position);
                    rows.Add(string.Join(",", n - 1, pd.PlayingClip, F(pd.PlayingClipTime), pd.EmoteActive, V(Loc(m.Bone(HumanBodyBones.Hips))), V(Loc(m.Bone(HumanBodyBones.LeftHand))),
                        V(Loc(m.Bone(HumanBodyBones.RightHand))), V(Loc(m.Bone(HumanBodyBones.Head))), F(m.racketGrip.position.y - root.position.y)));
                }
                if (seen && !pd.EmoteActive && ++tail > 24) break;
            }
            File.WriteAllText(dir + "/trace.csv", string.Join("\n", rows) + "\n");
            File.WriteAllText(dir + "/summary.txt", $"tennis started={started} seen={seen} frames={n} k={F(k)}\n");
            Debug.Log($"[JoyFilm] tennis started={started} seen={seen} frames={n}");
            if (!started || !seen) throw new InvalidOperationException("tennis emote did not play");
        }

        // ------------------------------------------------------------------ golf
        static readonly FieldInfo ResultHold = typeof(GolfGame).GetField("resultHold", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo StateTime = typeof(GolfGame).GetField("stateTime", BindingFlags.NonPublic | BindingFlags.Instance);
        static void KeepResult(GolfGame game, float seconds)
        {
            float t = (float)StateTime.GetValue(game), hold = (float)ResultHold.GetValue(game);
            if (hold < t + seconds) ResultHold.SetValue(game, t + seconds);
        }
        static IEnumerator WaitFor(Func<bool> done, int frames, string what)
        {
            for (int i = 0; !done(); i++) { if (i > frames) throw new TimeoutException("waiting for " + what); yield return null; }
        }

        /// The new clip with its curve paths rewritten onto the golfer's own hierarchy (matched by bone name, using the golfer's existing emote take).
        static AnimationClip ForGolfer(AnimationClip mine, AnimationClip theirs, List<string> log)
        {
            var map = new Dictionary<string, string>();
            foreach (var b in AnimationUtility.GetCurveBindings(theirs)) { var leaf = b.path.Split('/').Last(); if (!map.ContainsKey(leaf)) map[leaf] = b.path; }
            var outClip = new AnimationClip { name = "Emote_Spike", frameRate = mine.frameRate, legacy = theirs.legacy };
            int hit = 0, miss = 0;
            foreach (var b in AnimationUtility.GetCurveBindings(mine))
            {
                var leaf = b.path.Split('/').Last();
                if (!map.TryGetValue(leaf, out var path)) { miss++; continue; }
                var nb = b; nb.path = path; AnimationUtility.SetEditorCurve(outClip, nb, AnimationUtility.GetEditorCurve(mine, b)); hit++;
            }
            outClip.EnsureQuaternionContinuity();
            log.Add($"remap: {hit} curves bound, {miss} without a golfer bone; mine first path '{AnimationUtility.GetCurveBindings(mine).First().path}', theirs '{AnimationUtility.GetCurveBindings(theirs).First().path}'");
            return outClip;
        }

        static IEnumerator GolfFilm(GolfGame game)
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            bool female = Env("JF_FEMALE", "0") == "1";
            var savedBody = GolferStyle.Body; int savedHoles = PlayerPrefs.GetInt("holes", 0); string savedCourse = PlayerPrefs.GetString("course", "cliffside");
            var log = new List<string>();
            try
            {
                Time.captureFramerate = 60; Time.timeScale = 1; UnityEngine.Random.InitState(3108);
                GolfGame.LoadCoverMinimum = 0.5f; game.InstantReplays = false;
                for (int i = 0; i < 10; i++) yield return null;
                GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                var golfer = game.GetComponentInChildren<GolferView>(true); golfer.ApplyStyle();
                game.ChooseHoles(0, "cliffside"); game.Play();
                var e1 = WaitFor(() => game.Current == GolfGame.State.Aim, 60 * 60, "the first tee"); while (e1.MoveNext()) yield return null;
                golfer = game.GetComponentInChildren<GolferView>(true);
                if (!golfer.IsHero) throw new InvalidOperationException("golfer is not the Hero");
                var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
                foreach (var smr in golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
                var hole = game.CurrentHole;
                for (int i = 0; i < 24; i++) { cam.aspect = 16f / 9f; yield return null; }
                game.StrikeToward(hole.RecommendedTarget(hole.Tee));
                var e2 = WaitFor(() => game.Current is GolfGame.State.Result or GolfGame.State.HoleDone, 60 * 60, "the tee shot result"); while (e2.MoveNext()) { cam.aspect = 16f / 9f; yield return null; }
                if (game.Current != GolfGame.State.Result || !game.ShowingShotResult) throw new InvalidOperationException("no shot result panel: " + game.Current);
                // swap in the new clip under the Spike slot's name (run time only)
                var clipsField = typeof(GolferView).GetField("clips", BindingFlags.NonPublic | BindingFlags.Instance);
                var clips = (Dictionary<string, AnimationClip>)clipsField.GetValue(golfer);
                string asset = Env("JF_GOLF_CLIP", "Assets/EmoteStd/Male_Emote_JumpJoy_Golf.fbx");
                var mine = AssetDatabase.LoadAllAssetsAtPath(asset).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                clips["Emote_Spike"] = ForGolfer(mine, clips["Emote_Spike"], log);
                game.EquipResultEmotes(new[] { "spike", "wave", "scuba" });
                for (int i = 0; i < 30; i++) { cam.aspect = 16f / 9f; KeepResult(game, 3); yield return null; }
                Transform head = golfer.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                Transform la = golfer.GetComponentsInChildren<Transform>(true).First(t => t.name == "LeftUpperArm"), ra = golfer.GetComponentsInChildren<Transform>(true).First(t => t.name == "RightUpperArm");
                float k = 1; var fwd = Vector3.forward;
                var rows = new List<string> { "frame,move,head" };
                int n = 0, tail = 0, guard = 0; bool seen = false, started = false;
                while (guard++ < 60 * 8)
                {
                    if (guard == 21)
                    {
                        started = game.PlayResultEmote(0);
                    }
                    cam.aspect = 16f / 9f; KeepResult(game, 3);
                    yield return null;
                    if (guard == 22) { k = (head.position.y - golfer.transform.position.y) / 1.62f; fwd = Facing(la, ra); }
                    if (golfer.Performing == "Emote_Spike") seen = true;
                    if (seen && golfer.Performing != "Emote_Spike")                     if (guard % 2 == 0)
                    {
                        string id = (n++).ToString("00000");
                        GameCapture.Save($"{dir}/game_{id}.jpg", 1280, 720);
                        Views(cam, dir, id, golfer.transform.position, guard >= 22 ? fwd : Facing(la, ra), guard >= 22 ? k : (head.position.y - golfer.transform.position.y) / 1.62f);
                        rows.Add(string.Join(",", n - 1, golfer.Performing, V(head.position - golfer.transform.position)));
                    }
                    if (seen && golfer.Performing != "Emote_Spike" && ++tail > 24) break;
                    if (seen && guard > 21 + 60 * 2.6f && ++tail > 24) break;    // the golfer holds the last frame of a non-looping move
                }
                File.WriteAllText(dir + "/trace.csv", string.Join("\n", rows) + "\n");
                log.Add($"golf started={started} seen={seen} frames={n} k={F(k)}");
                File.WriteAllText(dir + "/summary.txt", string.Join("\n", log) + "\n");
                Debug.Log("[JoyFilm] " + string.Join(" | ", log));
                if (!started || !seen) throw new InvalidOperationException("golf emote did not play");
            }
            finally
            {
                GolferStyle.Body = savedBody; PlayerPrefs.SetInt("holes", savedHoles); PlayerPrefs.SetString("course", savedCourse); PlayerPrefs.Save();
            }
        }
        // ------------------------------------------------------------------ golf aim footwork (before / after)
        static IEnumerator GolfAimFilm(GolfGame game)
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.jpg")) File.Delete(f);
            bool female = Env("JF_FEMALE", "0") == "1";
            GolferFootwork.Disabled = Env("JF_FOOTWORK", "1") == "0";
            var savedBody = GolferStyle.Body; int savedHoles = PlayerPrefs.GetInt("holes", 0); string savedCourse = PlayerPrefs.GetString("course", "cliffside");
            try
            {
                Time.captureFramerate = 60; Time.timeScale = 1; UnityEngine.Random.InitState(3108);
                GolfGame.LoadCoverMinimum = 0.5f; game.InstantReplays = false;
                for (int i = 0; i < 10; i++) yield return null;
                GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                var golfer = game.GetComponentInChildren<GolferView>(true); golfer.ApplyStyle();
                game.ChooseHoles(0, "cliffside"); game.Play();
                var e1 = WaitFor(() => game.Current == GolfGame.State.Aim, 60 * 60, "the first tee"); while (e1.MoveNext()) yield return null;
                golfer = game.GetComponentInChildren<GolferView>(true);
                var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
                for (int i = 0; i < 40; i++) { cam.aspect = 16f / 9f; yield return null; }
                var nudge = typeof(GolfGame).GetMethod("Nudge", BindingFlags.NonPublic | BindingFlags.Instance);
                var bones = golfer.Hero.Bones; Transform lf = bones["LeftFoot"], rf = bones["RightFoot"];
                Vector3 ball = (lf.position + rf.position) * .5f; var toBall = -golfer.transform.forward; // placeholder, replaced below
                // the ball is 0.75 m in front of the golfer's own position (GolferView.Stand)
                var gpos = golfer.transform.position; var face = golfer.transform.forward; face.y = 0; face.Normalize();
                ball = gpos + face * .75f;
                var side = Vector3.Cross(Vector3.up, face);
                Vector3 frontCam = ball + face * 2.9f + Vector3.up * 1.05f + side * .4f, frontLook = gpos + Vector3.up * .45f;
                Vector3 highCam = ball + Vector3.up * 3.4f - face * .2f + side * .01f, highLook = ball - face * .6f;
                var rows = new List<string> { "frame,heading_step,steps,lfoot,rfoot,golferPos" };
                // the script: hold, sweep right 42 deg (28 deg/s as the joystick does), hold, five taps back, hold, sweep back 28 deg, hold
                var plan = new List<float>();
                for (int i = 0; i < 30; i++) plan.Add(0);
                for (int i = 0; i < 90; i++) plan.Add(28f / 60f);
                for (int i = 0; i < 40; i++) plan.Add(0);
                for (int t = 0; t < 5; t++) { plan.Add(-1.5f); for (int i = 0; i < 14; i++) plan.Add(0); }
                for (int i = 0; i < 30; i++) plan.Add(0);
                for (int i = 0; i < 60; i++) plan.Add(-28f / 60f);
                for (int i = 0; i < 50; i++) plan.Add(0);
                int n = 0;
                for (int k = 0; k < plan.Count; k++)
                {
                    if (plan[k] != 0) nudge.Invoke(game, new object[] { (double)plan[k] });
                    cam.aspect = 16f / 9f;
                    yield return null;
                    if (k % 2 == 0)
                    {
                        string id = (n++).ToString("00000");
                        GameCapture.Save($"{dir}/game_{id}.jpg", 1280, 720);
                        Shot(cam, $"{dir}/front_{id}.jpg", frontCam, frontLook, 34, 720, 720);
                        Shot(cam, $"{dir}/side_{id}.jpg", highCam, highLook, 40, 720, 720);
                        rows.Add(string.Join(",", n - 1, F(plan[k]), golfer.Footwork != null ? golfer.Footwork.Steps : -1, V(lf.position), V(rf.position), V(golfer.transform.position)));
                    }
                }
                File.WriteAllText(dir + "/trace.csv", string.Join("\n", rows) + "\n");
                string sum = $"golfaim footwork={(GolferFootwork.Disabled ? "off" : "on")} steps={(golfer.Footwork != null ? golfer.Footwork.Steps : -1)} frames={n}";
                File.WriteAllText(dir + "/summary.txt", sum + "\n"); Debug.Log("[JoyFilm] " + sum);
            }
            finally
            {
                GolferFootwork.Disabled = false;
                GolferStyle.Body = savedBody; PlayerPrefs.SetInt("holes", savedHoles); PlayerPrefs.SetString("course", savedCourse); PlayerPrefs.Save();
            }
        }
        // ------------------------------------------------------------------ EMOTE STANDARD: everything through each game's own entry points
        static readonly string[] StdIds = { "jumpJoy", "fistPump", "stomp", "slump", "shock", "victory", "defeat" };
        static readonly string[] StdMoves = { "Emote_JumpJoy", "Emote_FistPump", "Emote_Stomp", "Emote_Slump", "Emote_Shock", "Emote_Victory", "Emote_Defeat" };

        static void Clear(string d) { Directory.CreateDirectory(d); foreach (var f in Directory.GetFiles(d, "*.jpg")) File.Delete(f); }

        static IEnumerator TennisAllFilm(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("JF_FEMALE", "0") == "1"; string rival = female ? "Viktor" : "Nadia";
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 90; i++) yield return null;
            var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var actor = game.Player; var m = pd.matchLook;
            foreach (var skin in m.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            Hold(game); for (int i = 0; i < 40; i++) yield return null;
            float k = (m.Bone(HumanBodyBones.Head).position.y - actor.transform.position.y) / 1.62f;
            var fwd = Facing(m.Bone(HumanBodyBones.LeftUpperArm), m.Bone(HumanBodyBones.RightUpperArm));
            var log = new List<string>();
            // (a) the seven as equipped emotes; (b) the automatic reactions: won point x2, lost point x2, whiff, match won, match lost
            var plan = new List<(string name, Func<bool> start, float seconds)>();
            for (int e = 0; e < StdIds.Length; e++) { int ee = e; plan.Add(("emote_" + StdMoves[e], () => { TennisEmotes.TryClip(StdIds[ee], out var c); return pd.PlayEmote(c); }, 0)); }
            plan.Add(("react_won1", () => { actor.React(true, TennisActor.Moment.Ordinary); return true; }, 3.0f));
            plan.Add(("react_won2", () => { actor.React(true, TennisActor.Moment.Ordinary); return true; }, 3.0f));
            plan.Add(("react_lost1", () => { actor.React(false, TennisActor.Moment.Ordinary); return true; }, 3.2f));
            plan.Add(("react_lost2", () => { actor.React(false, TennisActor.Moment.Ordinary); return true; }, 3.2f));
            plan.Add(("react_match_won", () => { actor.React(true, TennisActor.Moment.Match); return true; }, 4.0f));
            plan.Add(("react_match_lost", () => { actor.React(false, TennisActor.Moment.Match); return true; }, 4.2f));
            foreach (var (name, start, seconds) in plan)
            {
                string cdir = $"{dir}/{name}"; Clear(cdir);
                Hold(game); for (int i = 0; i < 24; i++) yield return null;
                bool ok = start(); int n = 0, guard = 0, tail = 0; bool seen = false; var played = new HashSet<string>();
                while (guard++ < 60 * 6)
                {
                    cam.aspect = 16f / 9f; yield return null;
                    played.Add(pd.PlayingClip);
                    if (seconds <= 0) { if (pd.EmoteActive) seen = true; if (seen && !pd.EmoteActive && ++tail > 20) break; }
                    else if (guard > seconds * 60) break;
                    if (guard % 2 == 0)
                    {
                        string id = (n++).ToString("00000");
                        GameCapture.Save($"{cdir}/game_{id}.jpg", 1280, 720);
                        Views(cam, cdir, id, actor.transform.position, fwd, k);
                    }
                }
                log.Add($"{name} ok={ok} frames={n} clips={string.Join("|", played)}");
                Debug.Log("[JoyFilm] tennis " + log.Last());
            }
            File.WriteAllText(dir + "/summary.txt", string.Join("\n", log) + "\n");
        }

        static IEnumerator GolfAllFilm(GolfGame game)
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            bool female = Env("JF_FEMALE", "0") == "1";
            var savedBody = GolferStyle.Body; int savedHoles = PlayerPrefs.GetInt("holes", 0); string savedCourse = PlayerPrefs.GetString("course", "cliffside");
            var log = new List<string>();
            try
            {
                Time.captureFramerate = 60; Time.timeScale = 1; UnityEngine.Random.InitState(3108);
                GolfGame.LoadCoverMinimum = 0.5f; game.InstantReplays = false;
                for (int i = 0; i < 10; i++) yield return null;
                GolferStyle.Body = female ? GolferStyle.BodyKind.Female : GolferStyle.BodyKind.Male;
                var golfer = game.GetComponentInChildren<GolferView>(true); golfer.ApplyStyle();
                game.ChooseHoles(0, "cliffside"); game.Play();
                var e1 = WaitFor(() => game.Current == GolfGame.State.Aim, 60 * 60, "the first tee"); while (e1.MoveNext()) yield return null;
                golfer = game.GetComponentInChildren<GolferView>(true);
                var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
                foreach (var smr in golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
                var hole = game.CurrentHole;
                for (int i = 0; i < 24; i++) { cam.aspect = 16f / 9f; yield return null; }
                game.StrikeToward(hole.RecommendedTarget(hole.Tee));
                var e2 = WaitFor(() => game.Current is GolfGame.State.Result or GolfGame.State.HoleDone, 60 * 60, "the tee shot result"); while (e2.MoveNext()) { cam.aspect = 16f / 9f; yield return null; }
                Transform head = golfer.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                Transform la = golfer.GetComponentsInChildren<Transform>(true).First(t => t.name == "LeftUpperArm"), ra = golfer.GetComponentsInChildren<Transform>(true).First(t => t.name == "RightUpperArm");
                log.Add("moves: " + string.Join(",", StdMoves.Concat(new[] { "Walk", "Run" }).Select(mv => mv + "=" + golfer.HasMove(mv))));
                var plan = new List<(string name, string id, string move, float seconds)>();
                for (int e = 0; e < StdIds.Length; e++) plan.Add(("result_" + StdMoves[e], StdIds[e], StdMoves[e], 0));
                plan.Add(("move_Walk", null, "Walk", 2.4f)); plan.Add(("move_Run", null, "Run", 1.6f));
                foreach (var (name, id, move, seconds) in plan)
                {
                    string cdir = $"{dir}/{name}"; Clear(cdir);
                    if (id != null) game.EquipResultEmotes(new[] { id }.Concat(StdIds.Where(x => x != id)).Take(3).ToArray());
                    for (int i = 0; i < 24; i++) { cam.aspect = 16f / 9f; KeepResult(game, 4); yield return null; }
                    bool ok = id != null ? game.PlayResultEmote(0) : golfer.Perform(move, 0f, true);
                    float k = (head.position.y - golfer.transform.position.y) / 1.62f; var fwd = Facing(la, ra);
                    int n = 0, guard = 0, tail = 0; bool seen = false;
                    while (guard++ < 60 * 6)
                    {
                        cam.aspect = 16f / 9f; KeepResult(game, 4); yield return null;
                        if (golfer.Performing == move) seen = true;
                        if (seconds <= 0) { if (seen && golfer.Performing != move && ++tail > 20) break; }
                        else if (guard > seconds * 60) break;
                        if (guard % 2 == 0)
                        {
                            string fid = (n++).ToString("00000");
                            GameCapture.Save($"{cdir}/game_{fid}.jpg", 1280, 720);
                            Views(cam, cdir, fid, golfer.transform.position, fwd, k);
                        }
                    }
                    if (id == null) golfer.Perform("Idle");
                    log.Add($"{name} ok={ok} seen={seen} frames={n}");
                    Debug.Log("[JoyFilm] golf " + log.Last());
                }
            }
            finally { GolferStyle.Body = savedBody; PlayerPrefs.SetInt("holes", savedHoles); PlayerPrefs.SetString("course", savedCourse); PlayerPrefs.Save(); File.WriteAllText(dir + "/summary.txt", string.Join("\n", log) + "\n"); }
        }

        static IEnumerator HubAllFilm()
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            bool female = Env("JF_FEMALE", "0") == "1";
            var log = new List<string>();
            Time.captureFramerate = 60;
            HubWorld.EditorDriven = true;
            var host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host);
            var bridge = host.AddComponent<NativeSportsSession>();
            bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = "hub-film", action = "start", sport = "hub", female = female, touch = true, shirt = "E8505B", shorts = "24345E", accent = "FFFFFF" }));
            for (int i = 0; i < 1800 && !(bridge.Ready && HubWorld.Find() && HubWorld.Find().Player && HubWorld.Find().Player.hero); i++) yield return null;
            var hub = HubWorld.Find(); var player = hub.Player;
            for (int i = 0; i < 60; i++) yield return null;
            Camera cam = Camera.main;
            if (!cam) cam = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(c => c.enabled && c.gameObject.activeInHierarchy && c.targetDisplay == 0 && !c.targetTexture).OrderByDescending(c => c.depth).FirstOrDefault();
            player.Teleport(Vector3.zero, 0); for (int i = 0; i < 30; i++) yield return null;
            log.Add($"camera={(cam ? cam.name : "none")}");
            log.Add($"hub ready={bridge.Ready} player={(player ? player.name : "none")} emotes={(player && player.hero ? string.Join(",", player.hero.Emotes) : "")}");
            Transform Bone(string n) => player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);
            Transform la = Bone("LeftUpperArm"), ra = Bone("RightUpperArm");
            IEnumerator Film(string name, Func<int, bool> step, int frames)
            {
                string cdir = $"{dir}/{name}"; Clear(cdir); int n = 0;
                for (int f = 0; f < frames; f++)
                {
                    if (!step(f)) break;
                    yield return null;
                    if (f % 2 == 0)
                    {
                        string id = (n++).ToString("00000");
                        if (cam) { Plain(cam, $"{cdir}/game_{id}.jpg", 1280, 720); Views(cam, cdir, id, player.transform.position, Facing(la, ra), 1f); }
                    }
                }
                log.Add($"{name} frames={n} speed={player.Speed:0.00}");
            }
            // walk, run, sprint round a wide circle (the stick turns slowly), then stop
            IEnumerator Go(string name, float mag, int frames)
            {
                var e = Film(name, f => { float a = (Time.frameCount % 100000) * 1.0f * Mathf.Deg2Rad; player.SetStick(Mathf.Sin(a), Mathf.Cos(a), mag, Time.realtimeSinceStartupAsDouble); return true; }, frames);
                while (e.MoveNext()) yield return e.Current;
            }
            var w = Go("hub_walk", .45f, 240); while (w.MoveNext()) yield return w.Current;
            var r = Go("hub_run", .8f, 180); while (r.MoveNext()) yield return r.Current;
            var sp = Go("hub_sprint", 1f, 150); while (sp.MoveNext()) yield return sp.Current;
            player.SetStick(0, 0, 0); for (int i = 0; i < 90; i++) yield return null;
            foreach (var mv in StdMoves)
            {
                string clip = mv.Replace("_", "");
                bool ok = player.PlayEmote(clip);
                var e = Film("hub_" + mv, f => true, 230); while (e.MoveNext()) yield return e.Current;
                log[log.Count - 1] += $" ok={ok}";
                for (int i = 0; i < 20; i++) yield return null;
            }
            File.WriteAllText(dir + "/summary.txt", string.Join("\n", log) + "\n");
            Debug.Log("[JoyFilm] hub " + string.Join(" | ", log));
            HubWorld.EditorDriven = false; Object.DestroyImmediate(host);
        }
        // ------------------------------------------------------------------ AUDIT close-ups: the torso from four sides at fixed moments of each emote
        static IEnumerator CloseupFilm(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("JF_OUT", "jf_out")); Directory.CreateDirectory(dir);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("JF_FEMALE", "0") == "1"; string rival = female ? "Viktor" : "Nadia";
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 90; i++) yield return null;
            var pd = game.Player.GetComponentInChildren<HeroTennisDriver>(); var actor = game.Player; var m = pd.matchLook;
            foreach (var skin in m.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            Hold(game); for (int i = 0; i < 40; i++) yield return null;
            var fwd = Facing(m.Bone(HumanBodyBones.LeftUpperArm), m.Bone(HumanBodyBones.RightUpperArm));
            var names = Env("JF_CLIPS", "jumpJoy,fistPump,stomp,slump,shock,victory,defeat,scuba,thrust,spike,wave,bringIt,pushups").Split(',');
            var log = new List<string>();
            foreach (var id in names)
            {
                if (!TennisEmotes.TryClip(id, out var clip)) continue;
                string cdir = $"{dir}/{id}"; Clear(cdir);
                Hold(game); for (int i = 0; i < 24; i++) yield return null;
                bool ok = pd.PlayEmote(clip); float len = pd.EmoteDuration(clip);
                int shots = 8, n = 0; var times = Enumerable.Range(0, shots).Select(k => len * (k + .5f) / shots).ToArray();
                float t0 = Time.time; int guard = 0;
                while (n < shots && guard++ < 60 * 8)
                {
                    yield return null;
                    if (Time.time - t0 + 1e-4f < times[n]) continue;
                    var chest = m.Bone(HumanBodyBones.Chest).position; var right = Vector3.Cross(Vector3.up, fwd);
                    string tag = $"{n:00}";
                    foreach (var (v, dirv) in new[] { ("front", fwd), ("back", -fwd), ("left", -right), ("right", right) })
                        Shot(cam, $"{cdir}/{v}_{tag}.jpg", chest + dirv * 1.9f + Vector3.up * .12f, chest - Vector3.up * .12f, 38, 640, 640);
                    n++;
                }
                log.Add($"{id} ok={ok} len={len:0.00} shots={n}");
                Debug.Log("[JoyFilm] closeup " + log.Last());
            }
            File.WriteAllText(dir + "/summary.txt", string.Join("\n", log) + "\n");
        }
    }
}
#endif
