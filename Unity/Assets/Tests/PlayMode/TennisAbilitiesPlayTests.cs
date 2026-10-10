using System.Collections;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class TennisAbilitiesPlayTests
    {
        static void Charge(TennisGame game) => typeof(TennisGame).GetProperty("PlayerUltimate").SetValue(game, 1f);
        static void Contact(TennisGame game) => typeof(TennisGame).GetMethod("ReturnBall", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(game, new object[] { new TennisHit { Speed = 20, Quality = .8f, Timing = 1 }, Vector2.zero });
        static void Capture(TennisGame game, string path)
        {
            var cam = game.GameplayCamera;
            cam.transform.position = game.Player.transform.position + new Vector3(4, 2.5f, -5);
            cam.transform.LookAt(game.Player.transform.position + Vector3.up * .85f);
            cam.fieldOfView = 42;
            GameCapture.Save(path, 1280, 720);
        }
        [UnityTest, Timeout(180000)] public IEnumerator SelectionDiveAndConfirmedContactRules()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
            string captures = "../ArtDir/screenshots/tennis_abilities"; Directory.CreateDirectory(captures);
            game.ConfigureMatch(TennisGame.Mode.Training, null, null, null);
            Assert.IsFalse(game.RequestDive(), "cannot dive before the rally");
            Assert.IsFalse(game.SelectUltimate(2)); game.LockLoadout();
            Assert.IsFalse(game.SelectUltimate(1), "cannot switch after Ready");
            game.Player.CancelSwing();
            game.InjectBall(game.Player.transform.position + new Vector3(2, 1, 2), new Vector3(0, 0, -4));
            var before = game.Player.transform.position;
            Assert.IsTrue(game.RequestDive()); Assert.IsFalse(game.RequestDive(), "held/double tap cannot retrigger");
            for (int f = 0; f < 20; f++) {
                game.Step(1f/60); yield return null;
                if (f == 8 || f == 16) Capture(game, captures + "/dive-" + f + ".png");
            }
            Assert.Greater(Vector3.Distance(before, game.Player.transform.position), .5f);
            Assert.Greater(game.DiveCooldownLeft, TennisAbilities.DiveCooldown - 1, "the dive recharges for most of its cooldown");
            Capture(game, captures + "/dive.png");

            // Exercise the real ball/racket contact path over a short timing sweep.
            bool saved = false;
            for (float at = 0; at < .8f && !saved; at += .04f) {
                game.ConfigureMatch(TennisGame.Mode.Training, null, null, null);
                game.Player.CancelSwing(); game.Player.Tick(2, 0);
                game.Player.transform.position = new Vector3(0,.035f,-11.2f);
                game.InjectBall(new Vector3(1.5f,1.3f,-7), new Vector3(0,1,-6));
                int returns = game.Returns; bool attempted = false;
                for (float t = 0; t < 1.5f; t += 1f/60) {
                    if (!attempted && t >= at) { attempted = game.RequestDive(); }
                    game.Step(1f/60); yield return null;
                    if (game.Returns > returns) { saved = true; break; }
                }
            }
            Assert.IsTrue(saved, "a timed dive must return a reachable incoming ball through real contact");

            game.ConfigureMatch(TennisGame.Mode.Training, null, null, null);
            game.Player.CancelSwing(); game.Player.Tick(2, 0);
            game.InjectBall(game.Player.transform.position + new Vector3(.6f, 2, 1), Vector3.zero);
            Charge(game); // Even an old full meter cannot activate an ultimate.
            Assert.IsFalse(game.CanArmUltimate);
            Assert.IsFalse(game.ToggleUltimate());
            game.Player.Swing(.7f, false, TennisActor.Stroke.Drive);
            Contact(game);
            Assert.That(game.PlayerUltimate, Is.Zero);
            Assert.IsFalse(game.UltimateArmed);
            game.ConfigureMatch(TennisGame.Mode.Training, null, null, null);
            Assert.That(game.PlayerUltimate, Is.Zero); Assert.That(game.DiveCooldownLeft, Is.Zero);
        }
    }
}
