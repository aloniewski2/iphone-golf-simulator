using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// EMOTES film: the match hero plays each of the six emotes in the real Tennis scene (the game camera + HUD, plus a front and a side camera moved to the hero), and a trace row per 60 fps frame
    /// records what the driver plays and where the racket is.  The game is held between points (PointOver, next serve timer parked) so that the emote is not cut by a serve.
    ///   EF_OUT=<dir> EF_FEMALE=0|1 EF_CLIPS=Emote_Scuba,... Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.EmoteFilm.Run      (no -quit, needs graphics)
    /// Taunts start through HeroTennisDriver.PlayTaunt(name), intros through TennisActor.PlayIntro() (IntroPick set first).  After the films the game-end hook is exercised:
    /// a plain point must not taunt, a won game must taunt once, PlayTaunt(name) must play that taunt (written to <dir>/triggers.json).
    [InitializeOnLoad]
    public static class EmoteFilm
    {
        const string Flag = "EmoteFilm"; static IEnumerator script;
        static EmoteFilm() { EditorApplication.update += Tick; }
        public static void Run() { EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity"); SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true; }
        static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>(); if (!game || !game.Initialized) return;
            if (script == null) script = Go(game);
            try { if (!script.MoveNext()) Done(0); } catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int c) { SessionState.SetBool(Flag, false); Time.captureFramerate = 0; if (Application.isBatchMode) EditorApplication.Exit(c); else EditorApplication.isPlaying = false; }
        static string V(Vector3 v) => v.x.ToString("0.0000", CultureInfo.InvariantCulture) + ";" + v.y.ToString("0.0000", CultureInfo.InvariantCulture) + ";" + v.z.ToString("0.0000", CultureInfo.InvariantCulture);
        static string F(float x) => x.ToString("0.0000", CultureInfo.InvariantCulture);

        static void Shot(Camera cam, string path, Vector3 from, Vector3 look, float fov, int w, int h)
        {
            var p = cam.transform.position; var r = cam.transform.rotation; var f = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); var act = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            cam.transform.SetPositionAndRotation(p, r); cam.fieldOfView = f;
        }

        /// Park the game between points: PointOver with the next-serve timer far away (the film's own state, set by reflection; the game code is not changed).
        static void Hold(TennisGame game)
        {
            typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game, new object[] { TennisGame.Phase.PointOver });
            typeof(TennisGame).GetField("resetTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, 1e6f);
        }

        static readonly Dictionary<string, HeroTennisDriver.Clip> ClipOf = new Dictionary<string, HeroTennisDriver.Clip>
        {
            { "Emote_Scuba", HeroTennisDriver.Clip.EmoteScuba }, { "Emote_Thrust", HeroTennisDriver.Clip.EmoteThrust }, { "Emote_Spike", HeroTennisDriver.Clip.EmoteSpike },
            { "Intro_Wave", HeroTennisDriver.Clip.IntroWave }, { "Intro_BringIt", HeroTennisDriver.Clip.IntroBringIt }, { "Intro_Pushups", HeroTennisDriver.Clip.IntroPushups },
        };

        static IEnumerator Go(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("EF_OUT", "../work/emotes/film_frames")); Directory.CreateDirectory(dir);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("EF_FEMALE", "0") == "1"; string rival = Env("EF_RIVAL", female ? "Viktor" : "Nadia");
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 90; i++) yield return null;
            var pd = game.Player.GetComponentInChildren<HeroTennisDriver>();
            var actor = game.Player; var m = pd.matchLook;
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            Hold(game);
            for (int i = 0; i < 30; i++) yield return null;
            var rkMeshes = m.racketGrip.GetComponentsInChildren<MeshFilter>(true).Where(f => f.name != "Collider_Racket" && f.sharedMesh).ToArray();
            var rkVerts = rkMeshes.Select(f => f.sharedMesh.vertices).ToArray();
            var hand = m.racketGrip.parent;
            float RacketLow()
            {
                float low = float.MaxValue;
                for (int k = 0; k < rkMeshes.Length; k++) { var M = rkMeshes[k].transform.localToWorldMatrix; var vs = rkVerts[k]; for (int i = 0; i < vs.Length; i += 2) low = Mathf.Min(low, M.MultiplyPoint3x4(vs[i]).y); }
                return low;
            }
            var clipsEnv = Env("EF_CLIPS", string.Join(",", ClipOf.Keys)); var clips = clipsEnv == "none" ? new string[0] : clipsEnv.Split(',');
            var summary = new List<string>();
            foreach (var name in clips)
            {
                if (!ClipOf.TryGetValue(name, out var clipId)) throw new ArgumentException(name);
                string cdir = $"{dir}/{name}"; Directory.CreateDirectory(cdir);
                foreach (var f in Directory.GetFiles(cdir, "*.png")) File.Delete(f);
                var rows = new List<string> { "frame,t,clip,playing,clipTime,state,emoteActive,emotesPlayed,flow,grip,hand,gripHandDist,racketLowY,root,hips,rwrist,lwrist,head,racketScale,gripW" };
                Hold(game);
                for (int i = 0; i < 20; i++) yield return null;
                // start the emote through the game's own entry points
                bool started;
                if (name.StartsWith("Emote_")) started = pd.PlayTaunt(name.Substring(6));
                else { pd.IntroPick = Array.IndexOf(HeroTennisDriver.Intros, clipId); int before = pd.EmotesPlayed; actor.PlayIntro(); started = pd.EmotesPlayed == before + 1; }
                int n = 0, endHold = 0, f0 = Time.frameCount, guard = 0; bool seen = false;
                while (guard++ < 60 * 8)
                {
                    yield return null;
                    var root = actor.transform; Vector3 Loc(Vector3 w) => root.InverseTransformPoint(w);
                    if (pd.EmoteActive) seen = true;
                    rows.Add(string.Join(",", Time.frameCount - f0, Time.time.ToString("0.000", CultureInfo.InvariantCulture), name, pd.PlayingClip, F(pd.PlayingClipTime), pd.State.Replace(',', ';'), pd.EmoteActive, pd.EmotesPlayed, game.Flow,
                        V(Loc(m.racketGrip.position)), V(Loc(hand.position)), F(Vector3.Distance(m.racketGrip.position, hand.position)), F(RacketLow() - root.position.y),
                        V(root.position), V(Loc(m.Bone(HumanBodyBones.Hips).position)), V(Loc(m.Bone(HumanBodyBones.RightHand).position)), V(Loc(m.Bone(HumanBodyBones.LeftHand).position)), V(Loc(m.Bone(HumanBodyBones.Head).position)), F(m.racketGrip.lossyScale.x), V(m.racketGrip.position)));
                    if (guard % 2 == 0)
                    {
                        string id = (n++).ToString("00000"); var at = root.position;
                        GameCapture.Save($"{cdir}/game_{id}.png", 1280, 720);
                        Shot(cam, $"{cdir}/front_{id}.png", root.TransformPoint(0.9f, 1.55f, 4.3f), at + root.TransformDirection(new Vector3(0, 0.75f, 0.35f)), 42, 720, 720);
                        Shot(cam, $"{cdir}/side_{id}.png", root.TransformPoint(-4.2f, 1.45f, 0.5f), at + root.TransformDirection(new Vector3(0, 0.75f, 0.35f)), 42, 720, 720);
                    }
                    if (seen && !pd.EmoteActive && ++endHold > 40) break;
                }
                File.WriteAllText(cdir + "/trace.csv", string.Join("\n", rows) + "\n");
                summary.Add($"{name} started={started} played={pd.EmotesPlayed} frames={n} seen={seen}");
                Debug.Log($"[EmoteFilm] {summary.Last()}");
            }
            // ---- trigger checks (G3)
            var trig = new List<string>();
            Hold(game); for (int i = 0; i < 30; i++) yield return null;
            int e0 = pd.EmotesPlayed;
            actor.React(true, TennisActor.Moment.Ordinary); for (int i = 0; i < 12; i++) yield return null;
            trig.Add($"\"plain_point_emotes\": {pd.EmotesPlayed - e0}");
            // TennisGame.Match returns a COPY of the score struct: the film bumps the real one (boxed field) so that the game's own counters move
            var matchField = typeof(TennisGame).GetField("match", BindingFlags.NonPublic | BindingFlags.Instance);
            object boxed = matchField.GetValue(game); var gamesField = typeof(TennisMatch).GetField("PlayerGames"); gamesField.SetValue(boxed, (int)gamesField.GetValue(boxed) + 1); matchField.SetValue(game, boxed);
            int e1 = pd.EmotesPlayed; actor.React(true, TennisActor.Moment.Ordinary); for (int i = 0; i < 12; i++) yield return null;
            trig.Add($"\"won_game_emotes\": {pd.EmotesPlayed - e1}"); trig.Add($"\"won_game_clip\": \"{pd.LastEmotePlayed}\"");
            for (int i = 0; i < 60 * 4; i++) yield return null;   // let it finish
            int e2 = pd.EmotesPlayed; actor.React(true, TennisActor.Moment.Ordinary); for (int i = 0; i < 12; i++) yield return null;
            trig.Add($"\"point_after_game_emotes\": {pd.EmotesPlayed - e2}");
            for (int i = 0; i < 60 * 4; i++) yield return null;
            int e3 = pd.EmotesPlayed; bool ok = pd.PlayTaunt("Spike"); for (int i = 0; i < 12; i++) yield return null;
            trig.Add($"\"play_taunt_spike\": {{\"ok\": {ok.ToString().ToLower()}, \"clip\": \"{pd.LastEmotePlayed}\", \"count\": {pd.EmotesPlayed - e3}}}");
            for (int i = 0; i < 60 * 4; i++) yield return null;
            int e4 = pd.EmotesPlayed; pd.IntroPick = -1; actor.PlayIntro(); for (int i = 0; i < 12; i++) yield return null;
            trig.Add($"\"intro_hook\": {{\"count\": {pd.EmotesPlayed - e4}, \"clip\": \"{pd.LastEmotePlayed}\"}}");
            File.WriteAllText(dir + "/triggers.json", "{\n  " + string.Join(",\n  ", trig) + "\n}\n");
            File.WriteAllText(dir + "/summary.txt", string.Join("\n", summary) + "\n");
            Debug.Log("[EmoteFilm] done: " + string.Join(" | ", summary));
        }
    }
}
