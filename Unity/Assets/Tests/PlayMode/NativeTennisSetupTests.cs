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
