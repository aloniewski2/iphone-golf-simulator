using System.Collections;
using System.Diagnostics;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The match as the player sees it: the opening presentation (drone, rival intro, player intro, umpire, swoop)
    /// then ~40 s of autoplayed rally, 30 fps, gameplay camera. Player = girl, ponytail, deep skin, black hair
    /// (the look reported with floating hair). Output: ArtDir/review/intro/intro.mp4 + every 10th frame as PNG.
    public class IntroCaptureTests
    {
        const string Ffmpeg = "/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/0de95862-3a01-41cc-a4b3-bae639f20bb9/scratchpad/venv/lib/python3.14/site-packages/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1";

        [UnityTest, Explicit, Timeout(1800000)]
        public IEnumerator FilmIntroAndRally()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            // INTRO_CUT = haircut index (default 7 = waves), INTRO_GIRL=1 for the girl body
            int cut = int.TryParse(System.Environment.GetEnvironmentVariable("INTRO_CUT"), out var ic) ? ic : 7;
            bool girl = System.Environment.GetEnvironmentVariable("INTRO_GIRL") == "1";
            var look = HeroKit.Style.From(5, 0, 0, TennisLook.Kit.From("FF5FA2", "1E2A6E", "FFFFFF", "3FA9F5", 5), cut, girl);
            look.SkinTint = HeroKit.Hex("4E3122"); look.HairTint = HeroKit.Hex("141010");
            game.SetPlayerLook(look);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 30;
            string dir = Path.GetFullPath("../ArtDir/review/intro" + (System.Environment.GetEnvironmentVariable("INTRO_TAG") ?? "")); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            string temp = "Library/Captures/intro-current.jpg";
            var encoder = Process.Start(new ProcessStartInfo {
                FileName = Ffmpeg,
                Arguments = "-y -hide_banner -loglevel error -f image2pipe -framerate 30 -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \"" + dir + "/intro.mp4\"",
                UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true });
            try
            {
                int total = (int)((TennisPresentation.Length + 40) * 30);
                for (int frame = 0; frame < total; frame++)
                {
                    if (frame == (int)((TennisPresentation.Length + 1) * 30)) { game.AutoPlay = true; game.AutoPlayLean = true; }
                    yield return null;
                    GameCapture.Save(temp, 1280, 720);
                    byte[] jpg = File.ReadAllBytes(temp); encoder.StandardInput.BaseStream.Write(jpg, 0, jpg.Length);
                    if (frame % 10 == 0) GameCapture.Save($"{dir}/f{frame:0000}.png", 640, 360);
                }
                encoder.StandardInput.Close(); encoder.WaitForExit(); Assert.That(encoder.ExitCode, Is.Zero);
            }
            finally
            {
                game.AutoPlay = false; Time.captureFramerate = oldRate;
                if (!encoder.HasExited) { encoder.StandardInput.Close(); encoder.WaitForExit(10000); }
                encoder.Dispose();
            }
        }
    }
}
