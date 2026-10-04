using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// SERVE_TOSS_ARM film: the first serve of a campaign match, the match hero serving by the game's own ritual (bounce, wind-up, toss, trophy, swing), recorded from the ball in the hand.
    ///   ST_OUT=<dir> ST_FEMALE=0|1 ST_RIVAL=Nadia Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.ServeTossFilm.Run     (no -quit, needs graphics)
    /// Writes <dir>/game_NNNNN.png (game camera + HUD), back_NNNNN.png / side_NNNNN.png (game camera moved to the server), every 2nd frame (30 fps film), and trace.csv with one row per 60 fps frame:
    /// the clip time, prepare amount, ball, and the world position of the tossing hand, the racket hand, the head and the strings, in the server's own frame.
    [InitializeOnLoad]
    public static class ServeTossFilm
    {
        const string Flag = "ServeTossFilm"; static IEnumerator script;
        static ServeTossFilm() { EditorApplication.update += Tick; }
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
        static string V(Vector3 v) => v.x.ToString("0.000", CultureInfo.InvariantCulture) + "," + v.y.ToString("0.000", CultureInfo.InvariantCulture) + "," + v.z.ToString("0.000", CultureInfo.InvariantCulture);

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

        static IEnumerator Go(TennisGame game)
        {
            string dir = Path.GetFullPath(Env("ST_OUT", "../work/serve-toss-arm/film")); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            Time.captureFramerate = 60; TennisGame.AutoPlayTimingJitter = 0f;
            var pres = game.GetComponent<TennisPresentation>(); if (pres) pres.Finish();
            bool female = Env("ST_FEMALE", "0") == "1"; string rival = Env("ST_RIVAL", female ? "Viktor" : "Nadia");
            game.SelectCharacter(female); game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (pres) pres.Finish();
            for (int i = 0; i < 60; i++) yield return null;
            var pd = game.Player.GetComponentInChildren<HeroTennisDriver>();
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            var rows = new List<string> { "frame,t,flow,state,clipTime,prepare,swinging,ttc,ball,Lhand,Lshoulder,Rhand,Rshoulder,head,strings,root,rootYaw,Lelbow,Relbow" };
            int guard = 0; while (game.Flow != TennisGame.Phase.PlayerServeHold && guard++ < 900) yield return null;
            game.AutoPlay = true; game.AutoPlayLean = true;
            int n = 0, after = 0; bool swung = false, started = false; int f0 = Time.frameCount;
            for (int f = 0; f < 60 * 40 && after < 130; f++)
            {
                yield return null;
                var a = pd.actor; var m = pd.matchLook; var root = a.transform;
                Vector3 Loc(Vector3 w) => root.InverseTransformPoint(w);
                Transform lel = m.Bone(HumanBodyBones.LeftLowerArm), rel = m.Bone(HumanBodyBones.RightLowerArm), lh = m.Bone(HumanBodyBones.LeftHand), ls = m.Bone(HumanBodyBones.LeftUpperArm), rh = m.Bone(HumanBodyBones.RightHand), rs = m.Bone(HumanBodyBones.RightUpperArm), hd = m.Bone(HumanBodyBones.Head);
                rows.Add(string.Join(",", Time.frameCount - f0, Time.time.ToString("0.000", CultureInfo.InvariantCulture), game.Flow, pd.State.Replace(',', ';'), pd.PlayingClipTime.ToString("0.0000", CultureInfo.InvariantCulture), a.PrepareAmount.ToString("0.000", CultureInfo.InvariantCulture),
                    a.Swinging, a.SignedTimeToContact.ToString("0.000", CultureInfo.InvariantCulture), V(Loc(game.BallPosition)), V(Loc(lh.position)), V(Loc(ls.position)), V(Loc(rh.position)), V(Loc(rs.position)), V(Loc(hd.position)), V(Loc(pd.StringCentre)), V(root.position), root.eulerAngles.y.ToString("0.0", CultureInfo.InvariantCulture), V(Loc(lel.position)), V(Loc(rel.position))));
                bool serving = game.Flow == TennisGame.Phase.PlayerServeHold || game.Flow == TennisGame.Phase.PlayerServeToss || pd.CurrentAction == HeroTennisDriver.Clip.Serve;
                if (a.PrepareAmount > .001f) started = true;
                if (a.Swinging && pd.CurrentAction == HeroTennisDriver.Clip.Serve) swung = true;
                if (swung) after++;
                if (serving && f % 2 == 0)
                {
                    string id = (n++).ToString("00000");
                    GameCapture.Save($"{dir}/game_{id}.png", 1280, 720);
                    var at = root.position;
                    Shot(cam, $"{dir}/back_{id}.png", root.TransformPoint(1.9f, 1.75f, -3.1f), at + Vector3.up * 1.2f, 40, 720, 720);
                    Shot(cam, $"{dir}/side_{id}.png", root.TransformPoint(-3.3f, 1.55f, 0.2f), at + Vector3.up * 1.2f, 40, 720, 720);
                }
                if (swung && game.Flow == TennisGame.Phase.PointOver) break;
            }
            File.WriteAllText(dir + "/trace.csv", string.Join("\n", rows) + "\n");
            Debug.Log($"[ServeTossFilm] frames={n} started={started} swung={swung}");
        }
    }
}
