using System.Linq;
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

        /// The racket meets the ball before the phone has confirmed the swing. The hit must be
        /// credited when confirmation arrives, not lost, and the ball must leave from the racket.
        [UnityTest] public IEnumerator LateConfirmationStillHits()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            Vector3 p = game.Player.transform.position;
            // Arrives at the hitting zone about 0.47s from now at racket height.
            game.InjectBall(new Vector3(p.x, 2.17f, -4f), new Vector3(0, 0, -14f));
            for (int i = 0; i < 34; i++) game.Step(1f / 120);
            game.BeginSwing(0, 0, 1);
            int hits = game.Hits;
            for (int i = 0; i < 24 && game.Hits == hits; i++) game.Step(1f / 120);
            Assert.AreEqual(hits, game.Hits, "no hit is awarded while the swing is unconfirmed");
            game.RequestSwing(.8f, 0, 0, 1);
            for (int i = 0; i < 3; i++) game.Step(1f / 120);
            Assert.AreEqual(hits + 1, game.Hits, $"confirmation must credit the held contact (feedback {game.Feedback})");
            Assert.Greater(game.BallVelocity.z, 0, "and send the ball back over the net");
            yield return null;
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
            float nextLog = 0;
            while (game.Hits < 2 && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup > nextLog) { nextLog = Time.realtimeSinceStartup + 1; UnityEngine.Debug.Log($"[SelfPlay] flow={game.Flow} score={game.Match.Scoreboard} feedback={game.Feedback} ball={game.BallPosition} player={game.Player.transform.position}"); }
                yield return null;
            }
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

        /// Front three-quarter portraits of both players, for art direction.
        [UnityTest, Explicit] public IEnumerator CapturePortraits()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            string dir = "Library/Captures/portraits"; Directory.CreateDirectory(dir);
            var cam = Camera.main;
            var hud = GameObject.Find("Tennis HUD"); if (hud) hud.SetActive(false);
            Vector3 p = game.Player.transform.position;
            game.Opponent.transform.position = p + new Vector3(1.5f, 0, .2f);
            game.Opponent.transform.rotation = Quaternion.Euler(0, -15, 0);
            game.Player.transform.rotation = Quaternion.Euler(0, 15, 0);
            for (int i = 0; i < 30; i++) { game.Player.Tick(1f / 60, 0); game.Opponent.Tick(1f / 60, 0); yield return null; }
            void Aim() { cam.transform.position = p + new Vector3(.75f, 1.35f, 4.6f); cam.transform.LookAt(p + new Vector3(.75f, .85f, 0)); cam.fieldOfView = 38; }
            Aim(); yield return null; Aim();
            GameCapture.Save($"{dir}/pair.png", 1280, 720);
            // Faces, without rackets in the way.
            game.Player.transform.rotation = Quaternion.Euler(0, 180, 0);
            game.Opponent.transform.rotation = Quaternion.Euler(0, 180, 0);
            for (int i = 0; i < 3; i++) { game.Player.Tick(1f / 60, 0); game.Opponent.Tick(1f / 60, 0); yield return null; }
            cam.transform.position = p + new Vector3(.75f, 1.45f, -2.6f); cam.transform.LookAt(p + new Vector3(.75f, 1.25f, 0)); cam.fieldOfView = 34;
            GameCapture.Save($"{dir}/faces.png", 1280, 720);
            foreach (var e in new[] { TennisActor.Expression.Cheer, TennisActor.Expression.Effort, TennisActor.Expression.Sad })
            {
                game.Player.SetExpression(e, 5); game.Opponent.SetExpression(e, 5);
                game.Player.Tick(1f / 60, 0); game.Opponent.Tick(1f / 60, 0); yield return null;
                cam.transform.position = p + new Vector3(.75f, 1.45f, -2.6f); cam.transform.LookAt(p + new Vector3(.75f, 1.25f, 0));
                GameCapture.Save($"{dir}/faces-{e}.png", 640, 360);
            }
        }

        /// Balance report from self-play: how often a player reaches the ball without reading
        /// it (no lean) and with a perfect read, how long rallies last, how often they dive.
        [UnityTest, Explicit, Timeout(900000)] public IEnumerator GameplayBalance()
        {
            TennisGame game = null; yield return Load(g => game = g);
            float oldScale = Time.timeScale; Time.timeScale = 4;
            try
            {
                foreach (bool lean in new[] { false, true })
                {
                    game.AutoPlay = true; game.AutoPlayLean = lean;
                    int balls0 = game.IncomingBalls, reach0 = game.ReachablePlans, hits0 = game.Returns, dives0 = game.Dives, jumps0 = game.GoodJumps, rally0 = game.LongestRally;
                    var score0 = game.Match;
                    float until = Time.time + 360;
                    while (Time.time < until) yield return null;
                    int balls = game.IncomingBalls - balls0, hits = game.Returns - hits0;
                    UnityEngine.Debug.Log($"[Balance] lean={lean} incoming={balls} returned={hits} ({(balls > 0 ? 100f * hits / balls : 0):0}%) " +
                        $"reachable={game.ReachablePlans - reach0} dives={game.Dives - dives0} jumps={game.GoodJumps - jumps0} longestRally={game.LongestRally} score={game.Match.Scoreboard}");
                }
            }
            finally { Time.timeScale = oldScale; game.AutoPlay = false; }
        }

        /// Self-played rallies, measuring how far the strings are from the ball at each hit.
        [UnityTest, Explicit, Timeout(900000)] public IEnumerator ContactGap()
        {
            TennisGame.LogContactGaps = true;
            TennisGame game = null; yield return Load(g => game = g);
            float oldScale = Time.timeScale; Time.timeScale = 4;
            try
            {
                game.AutoPlay = true; TennisGame.LogContactGaps = true;
                float until = Time.time + 150;
                while (Time.time < until) yield return null;
                UnityEngine.Debug.Log($"[ContactGap] player {game.PlayerGaps}  opponent {game.OpponentGaps}");
                Assert.Greater(game.PlayerGaps.Count, 10);
                // The strings are steered onto the ball; what is left is under a frame of flight.
                Assert.Less(game.PlayerGaps.Mean, .25f, "player's racket misses the ball at contact");
                Assert.Less(game.OpponentGaps.Mean, .25f, "rival's racket misses the ball at contact");
            }
            finally { Time.timeScale = oldScale; game.AutoPlay = false; TennisGame.LogContactGaps = false; }
        }

        /// Each campaign opponent in the arena, close up, after ConfigureMatch has dressed the
        /// rival in their own body. Explicit and windowed only.
        [UnityTest, Explicit] public IEnumerator CaptureOpponents()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            string dir = "Library/Captures/opponents"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            foreach (var rival in TennisRoster.All)
            {
                game.ConfigureMatch(TennisGame.Mode.Campaign, rival.Key, rival.Key, "QUARTERFINAL");
                Assert.AreEqual(rival.Key, game.Opponent.name.Replace("Opponent — ", ""));
                Assert.IsTrue(game.Opponent.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.name == "V4 Higgs body " + rival.Key && r.gameObject.activeInHierarchy),
                    $"{rival.Key} should wear their own body");
                for (int f = 0; f < 20; f++)
                {
                    game.Step(1f / 120);
                    yield return null;
                    var t = game.Opponent.transform;
                    game.GameplayCamera.transform.position = t.position + t.forward * 4.2f + t.right * 1.2f + Vector3.up * 1.3f;
                    game.GameplayCamera.transform.LookAt(t.position + Vector3.up * 1f);
                    game.GameplayCamera.fieldOfView = 40;
                }
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes($"{dir}/{rival.Key}.png", tex.EncodeToPNG()); Object.Destroy(tex);
            }
        }

        /// Aerial views of the island and resort. Explicit and windowed only.
        [UnityTest, Explicit] public IEnumerator CaptureIsland()
        {
            TennisGame game = null; yield return Load(g => game = g); game.ManualSimulation = true;
            string dir = "Library/Captures/island"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            var views = new[] { (new Vector3(0, 260, 1), new Vector3(0, 0, 0)), (new Vector3(-170, 110, -170), new Vector3(0, 0, 0)),
                                (new Vector3(160, 80, 150), new Vector3(0, 0, 0)), (new Vector3(26, 34, 120), new Vector3(0, 0, 30)) };
            for (int i = 0; i < views.Length; i++)
            {
                for (int f = 0; f < 3; f++)
                {
                    yield return null;
                    game.GameplayCamera.transform.position = views[i].Item1;
                    game.GameplayCamera.transform.LookAt(views[i].Item2);
                    game.GameplayCamera.fieldOfView = 50; game.GameplayCamera.farClipPlane = 1500;
                }
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes($"{dir}/view-{i}.png", tex.EncodeToPNG()); Object.Destroy(tex);
            }
        }

        /// Screenshots the pre-match presentation at its key beats (drone, rival, player,
        /// umpire, swoop), HUD and cards included. Explicit and windowed only.
        [UnityTest, Explicit] public IEnumerator CapturePresentation()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/presentation"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 30;
            float[] beats = { 1.2f, 5.6f, 6.0f, 6.4f, 6.8f, 7.2f, 7.6f, 8.0f, 8.4f, 9.4f, 9.8f, 10.2f, 10.6f, 11.0f, 11.4f, 11.8f, 12.2f, 13.4f, 14.6f, 15.8f };
            try
            {
                int shot = 0; float t = 0;
                for (int frame = 0; frame < 30 * 17 && shot < beats.Length; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    t += 1f / 30;
                    if (t >= beats[shot])
                    {
                        var tex = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes($"{dir}/beat-{shot:D2}.png", tex.EncodeToPNG());
                        Object.Destroy(tex); shot++;
                    }
                }
                Assert.IsFalse(game.IntroPlaying, "the presentation should hand over to play");
            }
            finally { Time.captureFramerate = oldRate; }
        }

        /// Screenshots the whole screen, HUD included, right after the player's hits during a
        /// self-played rally. Explicit and windowed only: overlay UI needs a real frame.
        [UnityTest, Explicit] public IEnumerator CaptureHud()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/hud"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                int shots = 0, callShots = 0, score = 0;
                for (int frame = 0; frame < 3600 && (shots < 4 || callShots < 3); frame++)
                {
                    if (frame == (int)(TennisGame.IntroSeconds * 60) + 6) game.AutoPlay = true;
                    yield return new WaitForEndOfFrame();
                    // A few frames after a hit, once the card has slid in.
                    int now = game.Match.PlayerPoints + game.Match.OpponentPoints + game.Match.PlayerGames * 10 + game.Match.OpponentGames * 10;
                    if (now != score && callShots < 3)
                    {
                        score = now;
                        for (int wait = 0; wait < 20; wait++) yield return null;
                        yield return new WaitForEndOfFrame();
                        var call = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes($"{dir}/call-{callShots++}.png", call.EncodeToPNG());
                        Object.Destroy(call);
                    }
                    if (game.Hits > 0 && game.Hits != shots && shots < 4)
                    {
                        for (int wait = 0; wait < 30; wait++) yield return null;
                        yield return new WaitForEndOfFrame();
                        var shot = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes($"{dir}/hud-{shots}.png", shot.EncodeToPNG());
                        Object.Destroy(shot); shots = game.Hits;
                    }
                }
            }
            finally { Time.captureFramerate = oldRate; }
        }

        /// Frames of the player's serve routine: bounces, toss, strike. Explicit.
        [UnityTest, Explicit] public IEnumerator CaptureServeRoutine()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/serve"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            game.AutoPlay = true;
            try
            {
                for (int frame = 0; frame < 200; frame++)
                {
                    yield return null;
                    if (frame % 5 == 0 || frame >= 150) GameCapture.Save($"{dir}/frame-{frame:D4}.jpg", 960, 540);
                }
            }
            finally { Time.captureFramerate = oldRate; game.AutoPlay = false; }
        }

        /// A demo reel at normal game speed: the whole presentation, then the game playing
        /// itself (reading every ball well) for about 45 seconds. HUD included, 1280x720 60fps.
        [UnityTest, Explicit, Timeout(3600000)] public IEnumerator FilmDemo()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/demo"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            float unscaled0 = Time.unscaledTime, time0 = Time.time;
            int introFrames = (int)(TennisPresentation.Length * 60) + 20, total = introFrames + 45 * 60;
            try
            {
                for (int frame = 0; frame < total; frame++)
                {
                    if (frame == introFrames) { game.AutoPlay = true; game.AutoPlayLean = true; }
                    yield return null;
                    GameCapture.Save($"{dir}/frame-{frame:D5}.jpg", 1280, 720);
                }
            }
            finally { Time.captureFramerate = oldRate; game.AutoPlay = false; }
            UnityEngine.Debug.Log($"[Demo] frames {total} game {Time.time - time0:0.0}s unscaled {Time.unscaledTime - unscaled0:0.0}s hits {game.Hits} longest {game.LongestRally} score {game.Match.Scoreboard}");
        }

        /// Close-ups of self-played rallies from beside whoever is about to hit, logging the
        /// frame of each contact, to check the racket visibly meets the ball.
        [UnityTest, Explicit, Timeout(1800000)] public IEnumerator CaptureContacts()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/contacts"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                game.AutoPlay = true;
                int returns = game.Returns, shots = game.RallyShots;
                for (int frame = 0; frame < 60 * 40; frame++)
                {
                    yield return null;
                    var hitter = game.BallPosition.z < 0 ? game.Player.transform : game.Opponent.transform;
                    float toNet = hitter.position.z < 0 ? 1 : -1;
                    var cam = game.GameplayCamera;
                    cam.transform.position = hitter.position + new Vector3(3.4f, 1.3f, toNet * 2.6f);
                    cam.transform.LookAt(hitter.position + new Vector3(.2f, .9f, toNet * .4f));
                    cam.fieldOfView = 42;
                    if (game.Returns != returns) UnityEngine.Debug.Log($"[Hit] frame {frame} P");
                    else if (game.RallyShots != shots) UnityEngine.Debug.Log($"[Hit] frame {frame} O");
                    returns = game.Returns; shots = game.RallyShots;
                    GameCapture.Save($"{dir}/frame-{frame:D4}.jpg", 960, 540);
                }
            }
            finally { Time.captureFramerate = oldRate; game.AutoPlay = false; }
        }

        /// The timing check in the running game: play holds, a ball bounces on the TV, and
        /// swings that land 150ms after each bounce leave the game compensating 150ms.
        [UnityTest, Timeout(60000)] public IEnumerator TimingCheckSetsTheLagFromSwingsOnTheBeat()
        {
            TennisGame game = null; yield return Load(g => game = g);
            float result = float.NaN;
            System.Action<float> done = lag => result = lag;
            TennisGame.TimingChecked += done;
            try
            {
                int hitsBefore = game.Hits;
                game.StartTimingCheck();
                Assert.IsTrue(game.CheckingTiming);
                float start = Time.unscaledTime;
                int next = 0; bool captured = false;
                while (game.CheckingTiming && Time.unscaledTime - start < 20)
                {
                    float beat = start + TennisBeatCalibration.LeadIn + next * TennisBeatCalibration.Interval;
                    if (next < TennisBeatCalibration.Beats && Time.unscaledTime >= beat + .15f) { game.BeginSwing(0, 0, 1); next++; }
                    if (!captured && next == 4) { captured = true; Directory.CreateDirectory("Library/Captures/timing-check"); GameCapture.Save("Library/Captures/timing-check/check.png", 1280, 720); }
                    yield return null;
                }
                Assert.IsFalse(game.CheckingTiming, "the check ends by itself");
                Assert.AreEqual(.15f, result, .035f, "measured the swings' lag behind the beat");
                Assert.AreEqual(result, game.Lag, 1e-4f, "and every swing is now compensated by it");
                Assert.AreEqual(hitsBefore, game.Hits, "check swings never reach the ball");
                for (int i = 0; i < 90; i++) yield return null;
                GameCapture.Save("Library/Captures/timing-check/done.png", 1280, 720);
            }
            finally { TennisGame.TimingChecked -= done; }
        }

        /// Close review of the player's own character in real play (runtime layers included:
        /// gait, run styling, racket guidance): a camera beside and behind the player, every
        /// third frame of a self-played rally, for the male and female avatars. Explicit.
        [UnityTest, Explicit, Timeout(1800000)] public IEnumerator CaptureCharacterInPlay()
        {
            foreach (bool female in new[] { false, true })
            {
                TennisGame game = null; yield return Load(g => game = g);
                game.SelectCharacter(female);
                string dir = $"Library/Captures/character-{(female ? "female" : "male")}";
                if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
                int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
                try
                {
                    game.AutoPlay = true;
                    for (int frame = 0; frame < 60 * 24; frame++)
                    {
                        yield return null;
                        var p = game.Player.transform.position;
                        var cam = game.GameplayCamera;
                        cam.transform.position = p + new Vector3(1.6f, 1.7f, -3.2f);
                        cam.transform.LookAt(p + new Vector3(0, 1.0f, .4f));
                        cam.fieldOfView = 45;
                        if (frame % 3 == 0) GameCapture.Save($"{dir}/frame-{frame:D4}.jpg", 960, 540);
                    }
                }
                finally { Time.captureFramerate = oldRate; game.AutoPlay = false; }
            }
        }

        /// Self-play with the swing cue in view: frames while a ball comes in, plus where each
        /// hit met the strings and why each opponent miss happened. Explicit: for review.
        [UnityTest, Explicit, Timeout(1800000)] public IEnumerator CaptureSwingCue()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/swing-cue"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                game.AutoPlay = true;
                int returns = game.Returns; string lastFeedback = "";
                for (int frame = 0; frame < 60 * 90; frame++)
                {
                    yield return null;
                    if (game.Returns != returns)
                        UnityEngine.Debug.Log($"[Face] {TennisRules.FaceError(game.LastFaceOffset):0.00} grade {game.LastGrade} late {TennisGame.LastLateness * 1000:0}ms");
                    returns = game.Returns;
                    if (game.Feedback != lastFeedback) { lastFeedback = game.Feedback; UnityEngine.Debug.Log($"[Call] {lastFeedback.Replace('\n', ' ')}"); }
                    if (game.Flow == TennisGame.Phase.Rally && game.BallVelocity.z < -1 && game.BallPosition.z > game.Player.transform.position.z && frame % 3 == 0)
                        GameCapture.Save($"{dir}/frame-{frame:D4}.jpg", 1280, 720);
                }
            }
            finally { Time.captureFramerate = oldRate; game.AutoPlay = false; }
        }

        /// Films a self-played rally at 60fps for review. Explicit only: it takes a while.
        [UnityTest, Explicit] public IEnumerator FilmRally()
        {
            TennisGame game = null; yield return Load(g => game = g);
            string dir = "Library/Captures/rally-film"; if (Directory.Exists(dir)) Directory.Delete(dir, true); Directory.CreateDirectory(dir);
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                for (int frame = 0; frame < 1740; frame++)
                {
                    // The opening flyover plays first; then the game plays itself.
                    if (frame == (int)(TennisGame.IntroSeconds * 60) + 6) game.AutoPlay = true;
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
