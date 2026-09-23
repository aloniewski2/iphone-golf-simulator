using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// The demo reel: Hole 12 played start to finish by script — the menu, the title flyover and
    /// the introductions on the tee, the tee shot over the water from the air, the read, the putt,
    /// the card — recorded at a steady 30 frames a second. Time.captureFramerate holds the game
    /// to 1/30 s a frame however long a frame takes to save, so the video runs at real speed;
    /// AudioRenderer takes the sound in step with it. Frames and sound land in
    /// Library/Captures/demo, and Tools/demo_video.sh makes the MP4. Explicit: it runs from
    /// Golf Arcade → Record Demo Video, not with the other tests.
    public class DemoVideoTests
    {
        const int Fps = 30;
        static string Dir => Path.GetFullPath("Library/Captures/demo");

        static IEnumerator Seconds(float s)
        {
            for (int i = 0, n = Mathf.RoundToInt(s * Fps); i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> done, float seconds, string what)
        {
            for (int i = 0, n = Mathf.RoundToInt(seconds * Fps); !done(); i++)
            {
                if (i > n) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        /// The backswing drawn back to `load` over `seconds`, eased like a real one.
        static IEnumerator Backswing(GolfGame game, double load, float seconds)
        {
            int n = Mathf.RoundToInt(seconds * Fps);
            for (int i = 1; i <= n; i++) { game.ShowBackswing(load * Mathf.SmoothStep(0, 1, (float)i / n)); yield return null; }
        }

        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator RecordsHoleTwelve()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            Directory.CreateDirectory(Dir);
            Time.timeScale = 1f;
            Time.captureFramerate = Fps;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            Camera.main.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game);
            int holes = game.ChosenHoles;
            Recorder rec = null;
            try
            {
                game.Demo = true;
                game.ChooseHoles(12);
                yield return null;
                rec = new GameObject("Recorder").AddComponent<Recorder>();
                rec.Begin(Dir);

                // the menu, over the island
                yield return Seconds(2.6f);
                game.Play();
                // the title flyover, the signature shot, you and the gallery on the tee
                yield return Until(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                yield return Seconds(1.8f);

                // the tee shot: onto the green a putt away, watched from the air
                var target = game.GreenTarget(7, 13, 11) ?? game.GreenTarget(4, 18, 10.5);
                Assert.IsTrue(target.HasValue, "no tee shot finishes on the green");
                yield return Backswing(game, 0.9, 0.85f);
                yield return Seconds(0.2f);
                game.StrikeToward(target.Value);
                yield return Until(() => game.Current == GolfGame.State.Result, 20, "the tee shot to finish");
                yield return Until(() => game.Current == GolfGame.State.Aim, 8, "the putt");
                // the read
                yield return Seconds(2.4f);

                for (int putt = 0; putt < 3 && game.Current == GolfGame.State.Aim; putt++)
                {
                    yield return Backswing(game, 0.55, 0.6f);
                    yield return Seconds(0.15f);
                    game.StrikeHolingPutt();
                    yield return Until(() => game.Current != GolfGame.State.Flight, 20, "the putt to stop");
                    yield return Until(() => game.Current != GolfGame.State.Result, 8, "after the putt");
                    if (game.Current == GolfGame.State.Aim) yield return Seconds(1.2f);
                }
                yield return Until(() => game.Current == GolfGame.State.RoundDone, 10, "the card");
                yield return Seconds(3.5f);
                rec.End();
                Debug.Log($"demo: {rec.Frames} frames, {rec.AudioSeconds:F1} s of sound in {Dir}");
                Assert.Greater(rec.Frames, Fps * 20);
            }
            finally
            {
                if (rec && rec.On) rec.End();
                Time.captureFramerate = 0;
                PlayerPrefs.SetInt("holes", holes); PlayerPrefs.Save();
            }
        }

        /// Saves each frame once everything has moved and the camera has followed, and the sound
        /// of that frame with it.
        [DefaultExecutionOrder(32000)]
        sealed class Recorder : MonoBehaviour
        {
            public bool On { get; private set; }
            public int Frames { get; private set; }
            public float AudioSeconds => rate > 0 ? sound.Count / (float)(rate * channels) : 0;
            string dir;
            readonly List<float> sound = new();
            int channels, rate;
            bool audio;

            public void Begin(string into)
            {
                dir = into;
                channels = AudioSettings.speakerMode switch
                {
                    AudioSpeakerMode.Mono => 1, AudioSpeakerMode.Quad => 4, AudioSpeakerMode.Surround => 5,
                    AudioSpeakerMode.Mode5point1 => 6, AudioSpeakerMode.Mode7point1 => 8, _ => 2,
                };
                rate = AudioSettings.outputSampleRate;
                audio = AudioRenderer.Start();
                On = true;
            }

            void LateUpdate()
            {
                if (!On) return;
                GameCapture.Save($"{dir}/f_{Frames:D5}.jpg");
                Frames++;
                if (!audio) return;
                int n = AudioRenderer.GetSampleCountForCaptureFrame();
                if (n <= 0) return;
                var buffer = new NativeArray<float>(n * channels, Allocator.Temp);
                AudioRenderer.Render(buffer);
                sound.AddRange(buffer);
                buffer.Dispose();
            }

            public void End()
            {
                On = false;
                if (audio) { AudioRenderer.Stop(); WriteWav($"{dir}/sound.wav"); }
            }

            void WriteWav(string path)
            {
                using var w = new BinaryWriter(File.Create(path));
                int bytes = sound.Count * 2;
                w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
                w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)channels);
                w.Write(rate); w.Write(rate * channels * 2); w.Write((short)(channels * 2)); w.Write((short)16);
                w.Write("data".ToCharArray()); w.Write(bytes);
                foreach (float s in sound) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
        }
    }
}
