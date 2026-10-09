using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Tennis;
using GolfArcade.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Real driver entry / return films for the six new reactions, both bodies.
    /// TPF_OUT=<absolute directory> Unity -executeMethod GolfArcade.EditorTools.TennisPerformanceFilm.Run
    /// The diagnostic holds the next-point timer; no gameplay code or contact timing is changed.
    [InitializeOnLoad]
    public static class TennisPerformanceFilm
    {
        const string Flag = "TennisPerformanceFilm";
        static IEnumerator script; static int lastFrame = -1;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly HeroTennisDriver.Clip[] Clips = {
            HeroTennisDriver.Clip.HitPerfect, HeroTennisDriver.Clip.MissWhiff,
            HeroTennisDriver.Clip.CelebratePoint, HeroTennisDriver.Clip.SadPointLost,
            HeroTennisDriver.Clip.MatchWin, HeroTennisDriver.Clip.MatchLose };
        static TennisPerformanceFilm() { EditorApplication.update += Tick; }
        public static void Run()
        {
            script = null; lastFrame = -1; EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized) return;
            if (script == null) script = Go(game);
            try { if (!script.MoveNext()) Done(0); }
            catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int code)
        {
            SessionState.SetBool(Flag, false); Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.isPlaying = false;
        }
        static void Hold(TennisGame game)
        {
            typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game, new object[] { TennisGame.Phase.PointOver });
            typeof(TennisGame).GetField("resetTimer", Private).SetValue(game, 1e6f);
        }
        static string F(float n) => n.ToString("0.0000", CultureInfo.InvariantCulture);
        static string V(Vector3 v) => F(v.x) + ";" + F(v.y) + ";" + F(v.z);
        static void Shot(Camera cam, string path, Vector3 from, Vector3 at)
        {
            var position = cam.transform.position; var rotation = cam.transform.rotation; float fov = cam.fieldOfView;
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from)); cam.fieldOfView = 40;
            var rt = RenderTexture.GetTemporary(640, 640, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; var active = RenderTexture.active;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev; RenderTexture.active = rt;
            var tex = new Texture2D(640, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt);
            cam.transform.SetPositionAndRotation(position, rotation); cam.fieldOfView = fov;
        }
        static IEnumerator Go(TennisGame game)
        {
            string dir = Path.GetFullPath(Environment.GetEnvironmentVariable("TPF_OUT") ?? "../proof/full-visual-overhaul/tennis-reactions-runtime");
            Directory.CreateDirectory(dir); Time.captureFramerate = 60;
            var summary = new List<string>();
            var presentation = game.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            foreach (bool female in new[] { false, true })
            {
                game.SelectCharacter(female); string rival = female ? "Viktor" : "Nadia";
                game.ConfigureMatch(TennisGame.Mode.Campaign, rival, rival, "ROUND"); if (presentation) presentation.Finish();
                for (int settle = 0; settle < 90; settle++) yield return null;
                Hold(game); for (int settle = 0; settle < 30; settle++) yield return null;
                var driver = game.Player.GetComponentInChildren<HeroTennisDriver>(); var hero = driver.matchLook;
                foreach (var skin in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
                var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
                var racket = hero.racketGrip; var hand = racket.parent;
                var meshes = racket.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh && m.name != "Collider_Racket").ToArray();
                float RacketLow()
                {
                    float y = float.MaxValue;
                    foreach (var m in meshes) foreach (var p in m.sharedMesh.vertices) y = Mathf.Min(y, m.transform.TransformPoint(p).y);
                    return y - game.Player.transform.position.y;
                }
                foreach (var clip in Clips)
                {
                    Hold(game); for (int settle = 0; settle < 30; settle++) yield return null;
                    string cdir = Path.Combine(dir, (female ? "female_" : "male_") + clip); Directory.CreateDirectory(cdir);
                    var rows = new List<string> { "frame,unityFrame,t,clip,playing,clipTime,state,reactionActive,gripHandDist,racketLowY,root,hips,rwrist,lwrist,head" };
                    driver.PlayJuice(clip); int frame = 0, after = 0; bool seen = false;
                    while (frame++ < 300)
                    {
                        yield return null;
                        bool active = (bool)typeof(HeroTennisDriver).GetField("juiceActive", Private).GetValue(driver);
                        if (active) seen = true;
                        var root = game.Player.transform; Vector3 Local(Transform t) => root.InverseTransformPoint(t.position);
                        rows.Add(string.Join(",", frame, Time.frameCount, F(Time.time), clip, driver.PlayingClip, F(driver.PlayingClipTime), driver.State.Replace(',', ';'), active,
                            F(Vector3.Distance(racket.position, hand.position)), F(RacketLow()), V(root.position),
                            V(Local(hero.Bone(HumanBodyBones.Hips))), V(Local(hero.Bone(HumanBodyBones.RightHand))),
                            V(Local(hero.Bone(HumanBodyBones.LeftHand))), V(Local(hero.Bone(HumanBodyBones.Head)))));
                        if (frame % 2 == 0)
                        {
                            GameCapture.Save(Path.Combine(cdir, $"game_{frame:0000}.png"), 960, 540);
                            Shot(cam, Path.Combine(cdir, $"front_{frame:0000}.png"), root.TransformPoint(.9f, 1.55f, 4.3f), root.TransformPoint(0, .82f, .15f));
                            Shot(cam, Path.Combine(cdir, $"side_{frame:0000}.png"), root.TransformPoint(-4.2f, 1.45f, .5f), root.TransformPoint(0, .82f, .15f));
                        }
                        if (seen && !active && ++after > 45) break;
                    }
                    bool stillActive = (bool)typeof(HeroTennisDriver).GetField("juiceActive", Private).GetValue(driver);
                    if (!seen || stillActive || after <= 45) throw new InvalidOperationException("Actual reaction entry/return incomplete: " + clip);
                    File.WriteAllText(Path.Combine(cdir, "trace.csv"), string.Join("\n", rows) + "\n");
                    summary.Add($"{(female ? "female" : "male")} {clip}: active={seen}, frames={frame}, final={driver.PlayingClip}, grip={Vector3.Distance(racket.position, hand.position):F4}");
                }
            }
            File.WriteAllText(Path.Combine(dir, "capture-contract.txt"), "Six reactions x both bodies, real PlayJuice driver entry/return. One iterator step per actual Unity Time.frameCount; 60fps evaluation and every second frame captured at30fps. Body/root/clip clocks are not sampled or manually overwritten. Front/side views use production mesh, pose, material and light. Next-point timer is parked only for this diagnostic. Main hand corrective approval and visual review remain separate requirements.\n");
            File.WriteAllText(Path.Combine(dir, "summary.txt"), string.Join("\n", summary) + "\n");
            Debug.Log("[TennisPerformanceFilm] " + string.Join(" | ", summary));
        }
    }
}
