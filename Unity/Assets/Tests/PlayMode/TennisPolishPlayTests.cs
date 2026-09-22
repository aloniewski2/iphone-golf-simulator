using System.Collections;
using System.Collections.Generic;
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
    public class TennisPolishPlayTests
    {
        static IEnumerator Load(System.Action<TennisGame> ready)
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            ready(game);
        }

        /// The character starts the stroke on the phone's first sign of a swing, cannot hit
        /// the ball until the phone confirms it, and backs out cleanly if it was a step.
        [UnityTest] public IEnumerator OnsetStartsTheSwingConfirmationEnablesContact()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            game.InjectBall(game.Player.transform.position + new Vector3(.6f, 1, 6), new Vector3(0, 0, -12));
            game.BeginSwing(0, 0, 1);
            Assert.IsTrue(game.Player.Swinging, "animation starts on onset");
            Assert.IsTrue(game.Player.Provisional, "but only provisionally");
            game.AbortSwing();
            Assert.IsFalse(game.Player.Swinging, "a written-off onset cancels");
            game.Player.Tick(.2f, 0);
            game.BeginSwing(0, 0, 1); game.RequestSwing(.8f, 0, 0, 1);
            Assert.IsTrue(game.Player.Swinging); Assert.IsFalse(game.Player.Provisional, "confirmation makes it real");
            Assert.AreEqual(.8f, game.Player.Power, .001f);
            // An onset that nothing ever confirms is dropped on its own.
            game.Player.Tick(1, 0);
            game.BeginSwing(0, 0, 1);
            for (int i = 0; i < 60; i++) game.Step(1f / 120);
            Assert.IsFalse(game.Player.Swinging && game.Player.Provisional, "stale onset must not hang");
        }

        /// No bone may jump between consecutive frames through the transitions that used to
        /// cut: standing to running, reversing, braking, a swing, a cancelled swing, a split
        /// step. A pop shows up as one frame moving a joint far further than its neighbours.
        [UnityTest] public IEnumerator TransitionsNeverPop()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            var actor = game.Player;
            // The fitted kit carries its own copy of the skeleton; measure the body rig only.
            var bones = new List<Transform>();
            var names = new HashSet<string> { "Hips", "Chest", "Head", "Foot.L", "Foot.R", "LowerLeg.L", "LowerLeg.R" };
            foreach (var t in actor.GetComponentsInChildren<Transform>())
                if (names.Remove(t.name)) bones.Add(t);
            Assert.AreEqual(7, bones.Count);
            // A pop is a sudden change in how a joint is moving, not fast movement: a sprinting
            // foot legitimately covers 20cm a frame. So the measure is the frame-to-frame change
            // in displacement (acceleration x dt^2), with the body carried across the court
            // the way the game carries it.
            var last = new Vector3[bones.Count]; var lastStep = new Vector3[bones.Count];
            var seen = new int[bones.Count];
            float worst = 0; string worstAt = "";
            void Frame(float speed, string label)
            {
                actor.transform.position += actor.transform.right * (speed / 60f);
                actor.Tick(1f / 60, speed);
                for (int i = 0; i < bones.Count; i++)
                {
                    Vector3 p = bones[i].position;
                    if (seen[i] > 0)
                    {
                        Vector3 step = p - last[i];
                        if (seen[i] > 1)
                        {
                            float jerk = (step - lastStep[i]).magnitude;
                            if (jerk > .05f) UnityEngine.Debug.Log($"[Pop] {label} speed={speed:0.0} {bones[i].name} {jerk:0.000}m");
                            // Feet and shins legitimately snap at toe-off in a sprint; the body must not.
                            bool limb = bones[i].name.StartsWith("Foot") || bones[i].name.StartsWith("LowerLeg");
                            float limit = limb ? .35f : .07f;
                            if (jerk / limit > worst) { worst = jerk / limit; worstAt = $"{label} {bones[i].name} {jerk:0.000}m"; }
                        }
                        lastStep[i] = step;
                    }
                    last[i] = p; seen[i]++;
                }
            }
            for (int f = 0; f < 30; f++) Frame(0, "idle");
            for (int f = 0; f < 40; f++) Frame(Mathf.Min(6, f * .4f), "accelerate");
            for (int f = 0; f < 20; f++) Frame(6 - f * .6f, "reverse");
            for (int f = 0; f < 30; f++) Frame(-5, "run left");
            // The game now decelerates into its spot (TennisRules.Deceleration) rather than
            // stopping dead, so the brake is exercised at that rate.
            for (int f = 0; f < 30; f++) Frame(Mathf.Min(0, -5 + f * TennisRules.Deceleration / 60f), "brake");
            actor.Swing(.8f, false, TennisActor.Stroke.Drive);
            for (int f = 0; f < 40; f++) Frame(0, "forehand");
            actor.Swing(.7f, true, TennisActor.Stroke.Drive, true);
            for (int f = 0; f < 5; f++) Frame(0, "provisional");
            actor.CancelSwing();
            for (int f = 0; f < 20; f++) Frame(0, "cancel");
            actor.SplitStep();
            for (int f = 0; f < 30; f++) Frame(0, "split step");
            actor.Prepare(1, true);
            for (int f = 0; f < 20; f++) Frame(0, "prepare");
            actor.Swing(.9f, true, TennisActor.Stroke.Drive);
            for (int f = 0; f < 40; f++) Frame(0, "backhand");
            // Body: 7cm of change in one frame (~250 m/s^2) is a pop. Feet: 35cm.
            Assert.Less(worst, 1f, $"worst single-frame change in joint motion: {worstAt} ({worst:0.00} of its limit)");
            yield return null;
        }

        /// Benchmark mode plays a real rally by itself, which is what a device timing run
        /// relies on. Also a budget: steady play must not churn garbage, which is what turns
        /// into periodic collection hitches on the phone.
        [UnityTest] public IEnumerator SelfPlayRalliesWithoutGarbage()
        {
            TennisGame game = null; yield return Load(g => game = g);
            game.AutoPlay = true;
            float deadline = Time.realtimeSinceStartup + 40;
            while (game.Hits < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.GreaterOrEqual(game.Hits, 2, $"self-play must return balls (flow {game.Flow}, feedback {game.Feedback})");
            // Measure allocations across steady play.
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            int frames = 0; float until = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < until) { frames++; yield return null; }
            long perFrame = (System.GC.GetAllocatedBytesForCurrentThread() - before) / Mathf.Max(1, frames);
            UnityEngine.Debug.Log($"[Budget] {perFrame} bytes/frame over {frames} frames, hits {game.Hits}");
            Assert.Less(perFrame, 1024, "steady play allocates at most a few strings per second, not per frame");
        }

        /// CPU budget for the simulation and animation of one rendered frame (two physics
        /// steps and one pose of each player). The phone has 16.7ms for everything at 60fps;
        /// gameplay code must stay a small slice of that.
        [UnityTest] public IEnumerator SimulationAndPosingFitTheFrameBudget()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            game.InjectBall(new Vector3(2, 1.2f, -2), new Vector3(0, 2, -9));
            for (int i = 0; i < 60; i++) { game.Step(1f / 120); game.Player.Pose(); }
            var watch = Stopwatch.StartNew();
            const int frames = 240;
            for (int i = 0; i < frames; i++)
            {
                if (i % 60 == 0) game.InjectBall(new Vector3(i % 120 == 0 ? 2 : -2, 1.2f, -2), new Vector3(0, 2, -9));
                game.Step(1f / 120); game.Step(1f / 120);
                game.Player.Pose(); game.Opponent.Pose();
            }
            double ms = watch.Elapsed.TotalMilliseconds / frames;
            UnityEngine.Debug.Log($"[Budget] {ms:0.000} ms per frame of simulation + posing (editor)");
            Assert.Less(ms, 4.0, "gameplay CPU per frame, measured in the editor");
        }

        /// Close-up capture of the gait, for judging footwork frame by frame.
        [UnityTest] public IEnumerator CaptureGait()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            string dir = "Library/Captures/gait"; Directory.CreateDirectory(dir);
            var actor = game.Opponent; // not moved by the serve flow while the ball is idle
            game.Opponent.gameObject.SetActive(true);
            actor = game.Player;
            game.InjectBall(new Vector3(0, 5, 20), Vector3.zero); // park the ball far away
            var cam = Camera.main;
            int frame = 0;
            var tracked = new List<Transform>(); var want = new HashSet<string> { "Foot.L", "Foot.R", "LowerLeg.L", "LowerLeg.R", "Head", "Hips" };
            foreach (var t in actor.GetComponentsInChildren<Transform>()) if (want.Remove(t.name)) tracked.Add(t);
            var prev = new Vector3[tracked.Count]; var prevStep = new Vector3[tracked.Count];
            {
                Transform B(string n) { foreach (var t in actor.GetComponentsInChildren<Transform>()) if (t.name == n) return t; return null; }
                var hip = B("UpperLeg.L"); var knee = B("LowerLeg.L"); var foot = B("Foot.L");
                UnityEngine.Debug.Log($"[GaitRig] thigh {Vector3.Distance(hip.position, knee.position):0.000} shin {Vector3.Distance(knee.position, foot.position):0.000} hipHeight {hip.position.y - actor.transform.position.y:0.000} ankleHeight {foot.position.y - actor.transform.position.y:0.000}");
            }
            float[] speeds = new float[150];
            for (int i = 0; i < 150; i++) speeds[i] = i < 20 ? 0 : i < 50 ? (i - 20) * .2f : i < 90 ? 6 : i < 110 ? 6 - (i - 90) * .55f : -5;
            foreach (float speed in speeds)
            {
                actor.transform.position += Vector3.right * (speed / 60f);
                actor.Tick(1f / 60, speed);
                for (int k = 0; k < tracked.Count; k++)
                {
                    Vector3 p = tracked[k].position, step = p - prev[k];
                    if (frame > 1 && (step - prevStep[k]).magnitude > .06f)
                        UnityEngine.Debug.Log($"[GaitPop] frame {frame} speed {speed:0.0} {tracked[k].name} jerk {(step - prevStep[k]).magnitude:0.000} step {step.magnitude:0.000}");
                    prevStep[k] = step; prev[k] = p;
                }
                yield return null;
                Vector3 at = actor.transform.position;
                cam.transform.position = at + new Vector3(0, 1.1f, -3.4f);
                cam.transform.LookAt(at + Vector3.up * .75f);
                cam.fieldOfView = 50;
                GameCapture.Save($"{dir}/frame-{frame:D4}.png", 480, 480); frame++;
            }
        }

        /// The score must actually move when a point is won in the scene, not just in the
        /// pure match model.
        [UnityTest] public IEnumerator WinningAPointChangesTheScore()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            // A ball well wide of the opponent, already on its way: a clean winner.
            game.InjectBall(new Vector3(4f, 1.2f, 7f), new Vector3(0, 1, 18));
            typeof(TennisGame).GetField("incoming", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(game, false);
            for (int i = 0; i < 240 && game.Match.PlayerPoints == 0 && game.Match.OpponentPoints == 0; i++) game.Step(1f / 120);
            Assert.AreEqual(1, game.Match.PlayerPoints + game.Match.OpponentPoints, $"flow {game.Flow} feedback {game.Feedback}");
        }

        /// Films a self-played rally at 60fps for review. Explicit only: it takes a while.
        [UnityTest, Explicit] public IEnumerator FilmRally()
        {
            TennisGame game = null; yield return Load(g => game = g);
            game.AutoPlay = true;
            string dir = "Library/Captures/rally-film"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                for (int frame = 0; frame < 1500; frame++)
                {
                    yield return null;
                    GameCapture.Save($"{dir}/frame-{frame:D4}.png", 1280, 720);
                }
            }
            finally { Time.captureFramerate = oldRate; }
            UnityEngine.Debug.Log($"[Film] hits {game.Hits} longest rally {game.LongestRally}");
        }

        /// Captures the new look for review: a serve, a two-handed backhand, a run, a rally.
        [UnityTest] public IEnumerator CaptureQualityPass()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            string dir = "Library/Captures/quality-pass"; Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                int frame = 0;
                IEnumerator Shoot(int count, System.Action each)
                {
                    for (int i = 0; i < count; i++)
                    {
                        each?.Invoke();
                        game.Step(1f / 120); game.Step(1f / 120);
                        yield return null;
                        GameCapture.Save($"{dir}/frame-{frame:D4}.png", 960, 540); frame++;
                    }
                }
                // Serve: toss, strike at the apex.
                game.Refeed();
                yield return Shoot((int)(TennisRules.ServeTossDelay * 60) + (int)(TennisRules.ServeIdealContact * 60) - 4, null);
                game.BeginSwing(0, .3f, 1); game.RequestSwing(.85f, 0, .3f, 1);
                yield return Shoot(50, null);
                // Backhand to a ball on the left.
                game.InjectBall(game.Player.transform.position + new Vector3(-.8f, 1.1f, 7), new Vector3(0, 0, -14));
                yield return Shoot(22, null);
                game.BeginSwing(-.3f, 0, -1); game.RequestSwing(.8f, -.3f, 0, -1);
                yield return Shoot(40, null);
                // Wide forehand run.
                game.InjectBall(new Vector3(3.8f, 1.3f, -4.5f), new Vector3(0, 1.5f, -6.5f));
                yield return Shoot(70, null);
            }
            finally { Time.captureFramerate = oldRate; }
        }
    }
}
