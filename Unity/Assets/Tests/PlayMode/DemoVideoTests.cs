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

        /// A phone held like a putter and moved by script, one sample a frame, through the real
        /// swing detector: the angle from address along a plan of legs.
        sealed class ScriptedStroke : GolfArcade.Swing.IMotionSource
        {
            static readonly System.Numerics.Vector3 Axis = System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(1, 0.2f, 0.1f));
            static readonly System.Numerics.Quaternion Address = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX, (float)(-Math.PI / 2));
            readonly Queue<double> plan = new();
            double angle;
            int lastFrame = -1;
            public bool IsAvailable => true;
            public void Start() { }
            public void Stop() { }
            public bool Idle => plan.Count == 0;

            /// To `to` radians from address over `seconds` (eased, or straight at a steady speed);
            /// a leg with the same `to` is a hold.
            public ScriptedStroke Leg(double seconds, double to, bool ease = true)
            {
                double from = plan.Count > 0 ? plan.ToArray()[plan.Count - 1] : angle;
                int n = Math.Max(1, Mathf.RoundToInt((float)seconds * Fps));
                for (int i = 1; i <= n; i++)
                {
                    float t = (float)i / n;
                    plan.Enqueue(from + (to - from) * (ease ? Mathf.SmoothStep(0, 1, t) : t));
                }
                return this;
            }

            public bool TryRead(out GolfArcade.Swing.MotionSample sample)
            {
                sample = default;
                if (lastFrame == Time.frameCount) return false;
                lastFrame = Time.frameCount;
                double next = plan.Count > 0 ? plan.Dequeue() : angle;
                double rate = (next - angle) * Fps;
                angle = next;
                sample = new GolfArcade.Swing.MotionSample
                {
                    Time = Time.timeAsDouble,
                    Attitude = System.Numerics.Quaternion.Normalize(System.Numerics.Quaternion.CreateFromAxisAngle(Axis, (float)angle) * Address),
                    RotationRate = Axis * (float)rate,
                    Gravity = System.Numerics.Vector3.UnitY,
                };
                return true;
            }
        }

        static UnityEngine.UI.Text Caption(Transform canvas, string name, float y, int size)
        {
            var box = GolfArcade.UI.UiKit.Panel(canvas, name, new Color(0.05f, 0.09f, 0.18f, 0.82f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(1000, size * 1.9f));
            box.rectTransform.pivot = new Vector2(0.5f, 0.5f); box.raycastTarget = false;
            var t = GolfArcade.UI.UiKit.Label(box.transform, "Text", size, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, GolfArcade.UI.UiKit.Display, false);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f); t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        /// The putting comparison: one green, three strokes made by a scripted phone through the
        /// real detector — the line turned and a normal putt; a stroke that stops before the
        /// ball; a quick short one that crosses the ball between two sensor readings. Captioned
        /// with the branch it was recorded on. Frames in Library/Captures/putting-<branch>.
        [UnityTest, Explicit, Timeout(3600000)]
        public IEnumerator RecordsPutting()
        {
            string head = File.Exists("../.git/HEAD") ? File.ReadAllText("../.git/HEAD").Trim() : "";
            string branch = head.StartsWith("ref: refs/heads/") ? head.Substring("ref: refs/heads/".Length) : "detached";
            string label = branch.Contains("codex") ? "CODEX'S VERSION" : branch.Contains("claude") ? "CLAUDE'S VERSION" : branch.ToUpperInvariant();
            string dir = Path.GetFullPath($"Library/Captures/putting-{branch.Replace('/', '-')}");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
            Time.timeScale = 1f;
            Time.captureFramerate = Fps;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            Camera.main.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
            Recorder rec = null;
            try
            {
                game.Demo = true;
                yield return null;
                game.Play();
                yield return null;
                game.JumpToHole(12);
                yield return null;
                var spot = new CoursePoint(-7, 186);   // 13 yd from the cup, across the shelf
                game.DropBall(spot);
                var stroke = new ScriptedStroke();
                game.Swing.UseSource(stroke);
                yield return Until(() => game.Current == GolfGame.State.Aim && game.Swing.Phase == GolfArcade.Swing.SwingPhase.Address, 10, "the putt to set up");

                var canvas = hud.GetComponentInParent<Canvas>() ? hud.GetComponentInParent<Canvas>().transform : Object.FindFirstObjectByType<Canvas>().transform;
                var title = Caption(canvas, "Version", 700, 44); title.text = label;
                var scene = Caption(canvas, "Scene", 590, 34);
                rec = new GameObject("Recorder").AddComponent<Recorder>();
                rec.Begin(dir);

                // 1: the line turned right and back, then a normal putt
                scene.text = "1 · Change the line, then a smooth putt";
                yield return Seconds(1.2f);
                var press = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current ?? Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>());
                hud.AimRight.OnPointerDown(press); yield return Seconds(1.3f); hud.AimRight.OnPointerUp(press);
                yield return Seconds(0.5f);
                hud.AimLeft.OnPointerDown(press); yield return Seconds(1.3f); hud.AimLeft.OnPointerUp(press);
                yield return Seconds(0.6f);
                stroke.Leg(0.7, 0.3).Leg(0.15, 0.3).Leg(0.9, -0.18).Leg(0.5, -0.18).Leg(0.7, 0);
                yield return Until(() => game.Current == GolfGame.State.Flight, 6, "the putt");
                yield return Until(() => game.Current != GolfGame.State.Flight, 20, "the putt to stop");
                yield return Seconds(1.5f);

                // 2: a stroke that comes down and stops before the ball
                game.DropBall(spot);
                yield return Until(() => game.Swing.Phase == GolfArcade.Swing.SwingPhase.Address && stroke.Idle, 6, "address");
                scene.text = "2 · The stroke stops before the ball";
                yield return Seconds(1.0f);
                stroke.Leg(0.6, 0.3).Leg(0.15, 0.3).Leg(0.25, 0.12).Leg(3.5, 0.12).Leg(0.8, 0);
                yield return Seconds(5.6f);
                if (game.Current == GolfGame.State.Flight) yield return Until(() => game.Current != GolfGame.State.Flight, 20, "that putt to stop");
                yield return Seconds(1.0f);

                // 3: a quick, short stroke whose pass through the ball falls between two readings
                game.DropBall(spot);
                yield return Until(() => game.Swing.Phase == GolfArcade.Swing.SwingPhase.Address && stroke.Idle, 6, "address");
                scene.text = "3 · A quick stroke, between two sensor readings";
                yield return Seconds(1.0f);
                stroke.Leg(0.4, 0.14).Leg(0.1, 0.14).Leg(2.0 / Fps, -0.3, ease: false).Leg(3.0, -0.3).Leg(0.8, 0);
                yield return Seconds(4.4f);
                if (game.Current == GolfGame.State.Flight) yield return Until(() => game.Current != GolfGame.State.Flight, 20, "that putt to stop");
                yield return Seconds(1.5f);

                rec.End();
                Debug.Log($"putting demo ({label}): {rec.Frames} frames in {dir}");
            }
            finally
            {
                if (rec && rec.On) rec.End();
                Time.captureFramerate = 0;
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
