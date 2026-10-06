using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using GolfArcade.Game;

namespace GolfArcade.PlayTests {
    public sealed class NativeGolfFlowTests {
        GolfGame game;
        [UnityTest] public IEnumerator NativeRoundAcceptsAShotAndExitsWithoutOpeningTheLegacyMenu() {
            game = new GameObject("Native golf flow check").AddComponent<GolfGame>();
            yield return null;
            game.PrepareNativeAddress();
            yield return null;
            Assert.That(game.NativeControlled, Is.True);
            Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim));
            Assert.That(GameObject.Find("Menu"), Is.Null);
            Assert.That(game.TutorialHud.AimLeft.gameObject.activeSelf, Is.False);
            Assert.That(game.TutorialHud.SwingHold.gameObject.activeSelf, Is.False);
            Assert.That(game.Card, Is.Not.Null);
            game.NativeSwing(.65f);
            Assert.That(game.Current, Is.EqualTo(GolfGame.State.Flight));
            Assert.That(game.LastShot.Carry, Is.GreaterThan(10));
            Time.timeScale = 4;
            float deadline = Time.realtimeSinceStartup + 45;
            while (game.Current != GolfGame.State.Aim && game.Current != GolfGame.State.RoundDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim).Or.EqualTo(GolfGame.State.RoundDone));
            // Finish a short putt through the normal shot/result/card flow, then advance
            // using the same command exposed on the phone controller.
            if (game.Current == GolfGame.State.Aim) {
                var hole = game.CurrentHole;
                var pin = hole.Pin;
                double distance = pin.DistanceTo(hole.Tee);
                game.DropBall(new GolfArcade.Course.CoursePoint(pin.X + (hole.Tee.X - pin.X) * 4 / distance, pin.D + (hole.Tee.D - pin.D) * 4 / distance));
                for (int putt = 0; putt < 4 && game.Current == GolfGame.State.Aim; putt++) {
                    game.StrikeHolingPutt();
                    deadline = Time.realtimeSinceStartup + 30;
                    while (game.Current != GolfGame.State.Aim && game.Current != GolfGame.State.RoundDone && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
            }
            Assert.That(game.Current, Is.EqualTo(GolfGame.State.RoundDone));
            int expectedHole = game.NativeHasNextHole ? game.TutorialHole + 1 : 0;
            game.NativeContinue();
            yield return null;
            Assert.That(game.Current, Is.EqualTo(GolfGame.State.Aim));
            Assert.That(game.TutorialHole, Is.EqualTo(expectedHole));
            Assert.That(GameObject.Find("Menu"), Is.Null);
            bool exited = false;
            System.Action exit = () => exited = true;
            GolfGame.NativeExitRequested += exit;
            try {
                game.ShowMenu();
                Assert.That(exited, Is.True);
                Assert.That(game.enabled, Is.False);
                Assert.That(GameObject.Find("Menu"), Is.Null);
            } finally { GolfGame.NativeExitRequested -= exit; }
        }
        [UnityTearDown] public IEnumerator Cleanup() {
            Time.timeScale = 1;
            if (game) {
                Object.Destroy(game.GameplayCamera.gameObject);
                Object.Destroy(game.TutorialHud.gameObject);
                Object.Destroy(game.gameObject);
            }
            yield return null;
        }
    }
}
