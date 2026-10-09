using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// Score-80 plan proof harness: one autoplay match at a fixed 60 fps, filmed from the real game camera
    /// with a fixed side "body cam" on the player next to it, plus a per-frame metrics CSV (body chain,
    /// foot skate, hit-stop, camera shot / FOV while the ball is live). Both ultimates are forced once.
    ///   SCORE80_TAG     output folder name under ArtDir/score80 (default "run")
    ///   SCORE80_FRAMES  frames to film (default 3600 = 60 s)
    ///   SCORE80_VIDEO   0 = metrics only (fast)
    ///   SCORE80_SEED    Random seed (default 11)
    public class Score80CaptureTests
    {
        static string Env(string k, string d) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator FilmAndMeasure()
        {
            string tag = Env("SCORE80_TAG", "run");
            int frames = int.Parse(Env("SCORE80_FRAMES", "3600"));
            bool video = Env("SCORE80_VIDEO", "1") != "0";
            UnityEngine.Random.InitState(int.Parse(Env("SCORE80_SEED", "11")));
            float oldJitter = TennisGame.AutoPlayTimingJitter; TennisGame.AutoPlayTimingJitter = float.Parse(Env("SCORE80_JITTER", "0"), Inv);
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            string dir = Path.GetFullPath("../ArtDir/score80/" + tag); Directory.CreateDirectory(dir);
            Process encoder = null;
            var hitStopField = typeof(TennisGame).GetField("hitStop", BindingFlags.NonPublic | BindingFlags.Instance);
            var fxField = typeof(TennisGame).GetField("fx", BindingFlags.NonPublic | BindingFlags.Instance);
            var ultProp = typeof(TennisGame).GetProperty("PlayerUltimate");
            var rivalUltProp = typeof(TennisGame).GetProperty("RivalUltimate");
            string evt = "";
            Action<Vector2, Timing, bool> onContact = (f, g, s) => evt += "P:" + g + (s ? "+S" : "") + ";";
            Action onRival = () => evt += "R;";
            Action onWhiff = () => evt += "W;";
            TennisGame.ContactMade += onContact; TennisGame.OpponentStruck += onRival; TennisGame.Whiffed += onWhiff;
            RenderTexture gameRT = null, bodyRT = null; Texture2D frameTex = null; Camera bodyCam = null;
            var csv = new StringBuilder();
            try
            {
                for (int f = 0; f < (TennisPresentation.Length + 2) * 60; f++) yield return null;
                game.ConfigureMatch(TennisGame.Mode.Exhibition, null, "Rival", "SCORE 80");
                game.SelectUltimate((int)TennisUltimate.Curveball);
                game.AutoPlay = true; game.AutoPlayLean = true;
                HeroTennisDriver ph = null, rh = null;
                foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) if (d.isPlayer) ph = d; else rh = d;
                Assert.IsNotNull(ph); Assert.IsNotNull(rh);
                if (video)
                {
                    gameRT = new RenderTexture(1280, 720, 24); bodyRT = new RenderTexture(640, 720, 24);
                    frameTex = new Texture2D(1920, 720, TextureFormat.RGB24, false);
                    bodyCam = new GameObject("Score80 body cam").AddComponent<Camera>(); bodyCam.enabled = false; bodyCam.fieldOfView = 30; bodyCam.nearClipPlane = .1f; bodyCam.farClipPlane = 80;
                    bodyCam.targetTexture = bodyRT;
                    encoder = Process.Start(new ProcessStartInfo
                    {
                        FileName = "/private/tmp/tennis-preview-runtime/lib/python3.14/site-packages/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1",
                        Arguments = "-y -hide_banner -loglevel error -f image2pipe -framerate 60 -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -r 60 -movflags +faststart \"" + dir + "/capture.mp4\"",
                        UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true
                    });
                }
                csv.AppendLine("f,flow,shot,ts,hitstop,fov,camx,camy,camz,bx,by,bz,live,pstate,pswing,pttc,pkind,phy,pcy,phx,phy2,phz,pskate,paw,pchain,rstate,rswing,rttc,rhy,rcy,rskate,shake,grade,evt,pux,puz,rux,ruz,dfov,pdip,plean,povershoot,rdip,rlean");
                int armedAt = 60 * 18, rivalAt = 60 * 38; float lastFov = 0;
                for (int f = 0; f < frames; f++)
                {
                    if (f == armedAt) { ultProp.SetValue(game, 1f); }
                    if (f >= armedAt && game.PlayerUltimate >= 1 && !game.UltimateArmed && game.Flow == TennisGame.Phase.Rally) game.ToggleUltimate();
                    if (f == rivalAt) rivalUltProp.SetValue(game, 1f);
                    evt = "";
                    yield return null;
                    var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
                    var fx = fxField.GetValue(game) as TennisFx;
                    float hs = (float)hitStopField.GetValue(game);
                    var hy = ph.BodyYaw(); var ry = rh.BodyYaw();
                    var hand = ph.look.animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    var b = game.BallPosition; var cp = cam.transform.position;
                    string I(float v) => v.ToString("0.####", Inv);
                    csv.Append(f).Append(',').Append(game.Flow).Append(',').Append(game.Juice ? game.Juice.ShotName : "").Append(',').Append(I(Time.timeScale)).Append(',').Append(I(hs)).Append(',')
                       .Append(I(cam.fieldOfView)).Append(',').Append(I(cp.x)).Append(',').Append(I(cp.y)).Append(',').Append(I(cp.z)).Append(',')
                       .Append(I(b.x)).Append(',').Append(I(b.y)).Append(',').Append(I(b.z)).Append(',').Append(game.Flow == TennisGame.Phase.Rally ? 1 : 0).Append(',')
                       .Append(ph.State.Replace(',', ' ')).Append(',').Append(game.Player.Swinging ? 1 : 0).Append(',').Append(I(game.Player.Swinging ? game.Player.SignedTimeToContact : 9)).Append(',').Append(game.Player.Kind).Append(',')
                       .Append(I(hy.x)).Append(',').Append(I(hy.y)).Append(',').Append(I(hand.x)).Append(',').Append(I(hand.y)).Append(',').Append(I(hand.z)).Append(',')
                       .Append(I(ph.FootSkate)).Append(',').Append(I(ph.ActionWeight)).Append(',').Append(I(ph.ChainWeight)).Append(',')
                       .Append(rh.State.Replace(',', ' ')).Append(',').Append(game.Opponent.Swinging ? 1 : 0).Append(',').Append(I(game.Opponent.Swinging ? game.Opponent.SignedTimeToContact : 9)).Append(',')
                       .Append(I(ry.x)).Append(',').Append(I(ry.y)).Append(',').Append(I(rh.FootSkate)).Append(',').Append(I(fx ? fx.Shake : 0)).Append(',').Append(game.LastGrade).Append(',').Append(evt).Append(',')
                       .Append(I(game.Player.transform.position.x)).Append(',').Append(I(game.Player.transform.position.z)).Append(',').Append(I(game.Opponent.transform.position.x)).Append(',').Append(I(game.Opponent.transform.position.z)).Append(',')
                       .Append(I(cam.fieldOfView - lastFov)).Append(',').Append(I(ph.PlantDip)).Append(',').Append(I(ph.LeanDegrees)).Append(',').Append(I(ph.TorsoOvershoot)).Append(',').Append(I(rh.PlantDip)).Append(',').Append(I(rh.LeanDegrees)).AppendLine();
                    lastFov = cam.fieldOfView;
                    if (video)
                    {
                        // body cam: fixed court orientation (so turns read), off the player's right/front
                        var pp = ph.transform.position;
                        bodyCam.transform.position = pp + new Vector3(3.6f, 1.25f, 2.2f);
                        bodyCam.transform.LookAt(pp + Vector3.up * .85f);
                        bodyCam.Render();
                        var prev = cam.targetTexture; cam.targetTexture = gameRT; cam.Render(); cam.targetTexture = prev;
                        var active = RenderTexture.active;
                        RenderTexture.active = gameRT; frameTex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                        RenderTexture.active = bodyRT; frameTex.ReadPixels(new Rect(0, 0, 640, 720), 1280, 0);
                        RenderTexture.active = active; frameTex.Apply(false);
                        var jpg = frameTex.EncodeToJPG(88); encoder.StandardInput.BaseStream.Write(jpg, 0, jpg.Length);
                    }
                    if (f % 600 == 0) UnityEngine.Debug.Log($"[Score80] f={f} score={game.Match.Scoreboard} hits={game.Hits} ult={game.Juice.Ultimates}");
                }
                File.WriteAllText(dir + "/metrics.csv", csv.ToString());
                File.WriteAllText(dir + "/summary.txt", $"frames={frames} hits={game.Hits} longest={game.LongestRally} ultimates={game.Juice.Ultimates} perfects={game.Juice.Perfects} whiffs={game.Juice.Whiffs} score={game.Match.Scoreboard} playerGap mean={(game.PlayerGaps.Count > 0 ? game.PlayerGaps.Sum / game.PlayerGaps.Count : 0):0.000} max={game.PlayerGaps.Max:0.000} visible={game.PlayerGaps.Visible}/{game.PlayerGaps.Count} rivalGap mean={(game.OpponentGaps.Count > 0 ? game.OpponentGaps.Sum / game.OpponentGaps.Count : 0):0.000} max={game.OpponentGaps.Max:0.000} steps={ph.StepsTaken}/{rh.StepsTaken} kicks={ph.KicksFired} liveCutsRefused={game.Juice.LiveCutsRefused}\n");
                if (encoder != null) { encoder.StandardInput.Close(); encoder.WaitForExit(); Assert.That(encoder.ExitCode, Is.Zero); }
            }
            finally
            {
                TennisGame.ContactMade -= onContact; TennisGame.OpponentStruck -= onRival; TennisGame.Whiffed -= onWhiff;
                game.AutoPlay = false; Time.captureFramerate = oldRate; TennisGame.AutoPlayTimingJitter = oldJitter; Time.timeScale = 1;
                if (csv.Length > 0 && !File.Exists(dir + "/metrics.csv")) File.WriteAllText(dir + "/metrics.csv", csv.ToString());
                if (encoder != null && !encoder.HasExited) { encoder.StandardInput.Close(); encoder.WaitForExit(10000); }
                encoder?.Dispose();
                if (bodyCam) Object.Destroy(bodyCam.gameObject);
            }
        }
    }
}
