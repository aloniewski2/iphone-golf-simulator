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
    /// Films a venue: the opening drone shot, an autoplayed rally on the gameplay camera, then a ball
    /// hit off the edge and followed as it falls. Runs only when HERO_FILM_VENUE names a venue:
    ///   HERO_FILM_VENUE=volcano -runTests -testFilter GolfArcade.PlayTests.TennisVenueFilmTests
    /// Output: ArtDir/review/venues/<venue>.mp4. Needs an ffmpeg binary (HERO_FFMPEG).
    public class TennisVenueFilmTests
    {
        const string DefaultFfmpeg = "/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/0de95862-3a01-41cc-a4b3-bae639f20bb9/scratchpad/venv/lib/python3.14/site-packages/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1";

        [DefaultExecutionOrder(5000)]
        sealed class EdgeCam : MonoBehaviour
        {
            public TennisGame Game; public bool Active; Vector3 look;
            void LateUpdate()
            {
                if (!Active || !Game) return;
                var cam = Game.GameplayCamera;
                Vector3 target = Game.BallPosition;
                look = look == Vector3.zero ? target : Vector3.Lerp(look, target, .18f);
                cam.transform.position = new Vector3(3.5f, 2.6f, -10f);
                cam.transform.rotation = Quaternion.LookRotation(look - cam.transform.position);
                cam.fieldOfView = 62;
            }
        }

        [UnityTest, Timeout(3000000)]
        public IEnumerator FilmVenue()
        {
            var name = System.Environment.GetEnvironmentVariable("HERO_FILM_VENUE");
            Assume.That(name, Is.Not.Null.And.Not.Empty, "set HERO_FILM_VENUE to film");
            TennisVenue.Selected = TennisVenue.Parse(name);
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            var edge = game.gameObject.AddComponent<EdgeCam>(); edge.Game = game;
            int oldRate = Time.captureFramerate; Time.captureFramerate = 30;
            string dir = Path.GetFullPath("../ArtDir/review/venues"); Directory.CreateDirectory(dir);
            string temp = "Library/Captures/venue-film.jpg";
            string ffmpeg = System.Environment.GetEnvironmentVariable("HERO_FFMPEG") ?? DefaultFfmpeg;
            var encoder = Process.Start(new ProcessStartInfo {
                FileName = ffmpeg,
                Arguments = "-y -hide_banner -loglevel error -f image2pipe -framerate 30 -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \"" + dir + "/" + name + ".mp4\"",
                UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true });
            try
            {
                int drone = (int)(TennisPresentation.Length * 0 + 5.2f * 30), rally = 17 * 30, edgeShot = 5 * 30;
                for (int frame = 0; frame < drone + rally + edgeShot; frame++)
                {
                    if (frame == drone) { game.AutoPlay = true; game.AutoPlayLean = true; }
                    if (frame == drone + rally)
                    {
                        game.AutoPlay = false; edge.Active = true;
                        game.InjectBall(new Vector3(6f, 1.4f, -2f), new Vector3(11f, 2.5f, 2.5f));
                    }
                    yield return null;
                    GameCapture.Save(temp, 1280, 720);
                    byte[] jpg = File.ReadAllBytes(temp); encoder.StandardInput.BaseStream.Write(jpg, 0, jpg.Length);
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
