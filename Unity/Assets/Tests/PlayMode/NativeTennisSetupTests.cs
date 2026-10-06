using System.Collections;
using System.Reflection;
using System.IO;
using GolfArcade.Tennis;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests {
    public sealed class NativeTennisSetupTests {
        [UnityTest] public IEnumerator MotionSteeringRespondsWithoutWaitingForTheAutomaticReaction() {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true; game.ManualSimulation = true;
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
            game.SetControllerSetup(true); game.SetControllerSetup(false);
            game.Player.transform.position = new Vector3(0, .035f, -11.2f);
            game.InjectBall(new Vector3(-3, 1.4f, -4), new Vector3(0, 0, -4));
            Assert.That(game.PlayerUsesTrackedMovement, Is.True);
            game.SetLateralInput(1, true);
            float before = game.Player.transform.position.x;
            game.Step(1f / 120);
            Assert.That(game.Player.transform.position.x, Is.GreaterThan(before), "A right step must react on the first input frame, even while AI reads a leftward ball");
            for (int i = 0; i < 12; i++) game.Step(1f / 120);
            Assert.That(game.LateralSpeed, Is.GreaterThan(0));
            game.SetLateralInput(-1, true);
            for (int i = 0; i < 15; i++) game.Step(1f / 120);
            Assert.That(game.LateralSpeed, Is.LessThan(0), "Changing direction must not wait through an AI reaction");
            var driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
            typeof(HeroTennisDriver).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, null);
            Assert.That((Vector3)typeof(HeroTennisDriver).GetField("smoothLocal", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver), Is.EqualTo(Vector3.zero), "The visible player must stay on the controller-driven actor");
        }

        [UnityTest] public IEnumerator ServeMovesOnOnsetButCannotLaunchBeforeConfirmation() {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true; game.ManualSimulation = true;
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
            game.SetControllerSetup(true); game.SetControllerSetup(false);
            game.DisplayLatency = .18f;
            game.Toss(1);
            for (int i = 0; i < 300 && game.Flow != TennisGame.Phase.PlayerServeToss; i++) game.Step(1f / 120);
            Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.PlayerServeToss));
            Vector3 planted = game.Player.transform.position;
            // A swing at the apex as seen on this screen arrives after the measured
            // transport delay and detector onset. Grade that original moment.
            for (int i = 0; i < 101; i++) game.Step(1f / 120);
            Assert.That(game.Player.transform.position, Is.EqualTo(planted), "The server must not keep walking away from an in-flight toss");
            var committed = typeof(TennisGame).GetField("serveCommitted", BindingFlags.Instance | BindingFlags.NonPublic);
            var launch = typeof(TennisGame).GetField("serveLaunchPending", BindingFlags.Instance | BindingFlags.NonPublic);
            game.BeginSwing(0, .3f, 1);
            Assert.That(game.Player.Swinging, Is.True, "The first stroke sample must move the server immediately");
            Assert.That(game.Player.Provisional, Is.True);
            Assert.That(committed.GetValue(game), Is.EqualTo(false));
            Assert.That(launch.GetValue(game), Is.EqualTo(false));
            game.AbortSwing();
            Assert.That(game.Player.Swinging, Is.False);
            Assert.That(committed.GetValue(game), Is.EqualTo(false), "A rejected step cannot commit a serve");
            game.BeginSwing(0, .3f, 1);
            for (int i = 0; i < 8; i++) game.Step(1f / 120);
            var driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
            typeof(HeroTennisDriver).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, null);
            game.RequestSwing(.8f, 0, .3f, 1);
            Assert.That(game.Player.Swinging, Is.True);
            Assert.That(game.Player.Provisional, Is.False);
            Assert.That(committed.GetValue(game), Is.EqualTo(true));
            Assert.That(launch.GetValue(game), Is.EqualTo(true));
            Assert.That((float)typeof(TennisGame).GetField("serveSwingIn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game), Is.LessThan(0), "Confirmation must not add a second animation wait");
            for (int i = 0; i < 60 && game.Flow == TennisGame.Phase.PlayerServeToss; i++) game.Step(1f / 120);
            Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.Rally), "An on-time confirmed serve must leave the strings and start play");
            Assert.That(game.LastContactGap, Is.LessThanOrEqualTo(.23f), "Immediate animation must preserve visible racket-ball contact");
        }

        [UnityTest] public IEnumerator PhoneRallyStrokeHasFullArmWeightInItsFirstRenderedFrame() {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true; game.ManualSimulation = true;
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
            game.SetControllerSetup(true); game.SetControllerSetup(false);
            game.DisplayLatency = .18f;
            game.InjectBall(game.Player.transform.position + new Vector3(.6f, 1.3f, 2), new Vector3(0, 0, -8));
            var driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
            Assert.That(driver, Is.Not.Null);
            game.BeginSwing(0, 0, 1);
            Assert.That(game.Player.Provisional, Is.True);
            game.Step(1f / 120);
            typeof(HeroTennisDriver).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(driver, null);
            Assert.That(driver.ActionWeight, Is.EqualTo(1).Within(.001f), "No animation fade may hide a time-critical phone stroke");
            Assert.That(driver.UpperLayerWeight, Is.GreaterThan(.9f), "The arms react while the legs keep their normal transition");
            var arms = (float[])typeof(HeroTennisDriver).GetField("upperBlend", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
            Assert.That(arms[(int)driver.CurrentAction], Is.EqualTo(1).Within(.001f), "The first frame plays the new stroke instead of crossfading from a stale clip");
            game.AbortSwing();
            Assert.That(game.Player.Swinging, Is.False);
        }

        [UnityTest] public IEnumerator TimingFinishesWithTheMatchStillHeldForReady() {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true;
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
            game.DisplayLatency = .72f;
            game.SetControllerSetup(true);
            Vector3 ball = game.BallPosition;
            var phase = game.Flow;
            game.StartTimingCheck();
            var check = (TennisBeatCalibration)typeof(TennisGame).GetField("calibration", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            for (int beat = 0; beat < TennisBeatCalibration.Beats; beat++) check.Swing(check.BeatTime(beat) + .82f);
            float deadline = Time.realtimeSinceStartup + 30;
            while (game.CheckingTiming && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.CheckingTiming, Is.False, "The TV calibration must finish without starting a point");
            Assert.That(game.Lag, Is.EqualTo(.72f).Within(.001f), "The rhythm check must preserve the measured screen delay");
            Assert.That(game.ControllerSetup, Is.True);
            Assert.That(game.Flow, Is.EqualTo(phase));
            Assert.That(game.BallPosition, Is.EqualTo(ball));
            game.BeginSwing(0, 0, 0); game.RequestSwing(.8f);
            Assert.That(game.Player.Swinging, Is.False);
        }
        [UnityTest] public IEnumerator PointWinnerStaysVisibleAndCelebrationsHoldTheNextServe() {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true; game.ManualSimulation = true;
            var hud = game.GetComponent<TennisHud>();
            var award = typeof(TennisGame).GetMethod("AwardPoint", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (bool won in new[] { true, false }) {
                game.ConfigureMatch(TennisGame.Mode.Exhibition, null, "RIVAL", null);
                game.SetControllerSetup(true); game.SetControllerSetup(false);
                award.Invoke(game, new object[] { won, false });
                for (int frame = 0; frame < 8; frame++) { hud.Refresh(game); yield return null; }
                Assert.That(game.ScoreOnlyText, Is.True);
                Assert.That(hud.PointResultText, Is.EqualTo((won ? hud.PlayerName : hud.OpponentName).ToUpperInvariant() + " SCORED"));
                typeof(TennisGame).GetMethod("UpdateCamera", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, new object[] { true });
                yield return new WaitForSecondsRealtime(.2f);
                string dir = System.Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR") ?? "Library/Captures/timing-point-fix";
                Directory.CreateDirectory(dir); GameCapture.Save(dir + (won ? "/point-you.png" : "/point-rival.png"), 1280, 720);
                for (int i = 0; i < 600; i++) game.Step(TennisGame.GameSpeed / 120f);
                Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.PointOver), "Point stays open for at least five real seconds");
                Assert.That(game.EmoteWindow, Is.EqualTo(won ? "point" : ""));
                game.HoldNextPoint(3f * TennisGame.GameSpeed);
                for (int i = 0; i < 240; i++) game.Step(TennisGame.GameSpeed / 120f);
                Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.PointOver), "A late celebration extends the point break");
                for (int i = 0; i < 180; i++) game.Step(TennisGame.GameSpeed / 120f);
                Assert.That(game.Flow, Is.Not.EqualTo(TennisGame.Phase.PointOver));
            }
        }
        [UnityTest] public IEnumerator CalibrationHoldsPointPlayUntilReadyWithoutATutorial() {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true;
            game.ConfigureMatch(TennisGame.Mode.Tutorial, null, null, null);
            yield return null;
            Assert.That(game.PlayMode, Is.EqualTo(TennisGame.Mode.Exhibition));
            Assert.That(game.GetComponent<TennisTutorial>(), Is.Null);
            game.SetControllerSetup(true);
            Vector3 ball = game.BallPosition;
            var phase = game.Flow;
            for (int i = 0; i < 120; i++) game.Step(1f/120);
            game.Toss(1); game.RequestSwing(.8f);
            Assert.That(game.BallPosition, Is.EqualTo(ball));
            Assert.That(game.Flow, Is.EqualTo(phase));
            Assert.That(game.CanDive, Is.False);
            Assert.That(game.AimPractice, Is.False);
            game.StartTimingCheck();
            Assert.That(game.CheckingTiming, Is.True);
            game.RequestSwing(.8f); // The phone's swing reaches the timing check while play is held.
            game.CancelTimingCheck();
            Assert.That(game.ControllerSetup, Is.True);
            Assert.That(game.CheckingTiming, Is.False);
            game.SetControllerSetup(false);
            Assert.That(game.ControllerSetup, Is.False);
            game.Toss(1);
            for (int i = 0; i < 120; i++) game.Step(1f/120);
            Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.PlayerServeToss));
        }
    }
}
