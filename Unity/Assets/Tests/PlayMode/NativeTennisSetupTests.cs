using System.Collections;
using GolfArcade.Tennis;
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
            game.SetControllerSetup(true);
            Vector3 ball = game.BallPosition;
            var phase = game.Flow;
            game.StartTimingCheck();
            float deadline = Time.realtimeSinceStartup + 30;
            while (game.CheckingTiming && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(game.CheckingTiming, Is.False, "The TV calibration must finish without starting a point");
            Assert.That(game.ControllerSetup, Is.True);
            Assert.That(game.Flow, Is.EqualTo(phase));
            Assert.That(game.BallPosition, Is.EqualTo(ball));
            game.BeginSwing(0, 0, 0); game.RequestSwing(.8f);
            Assert.That(game.Player.Swinging, Is.False);
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
