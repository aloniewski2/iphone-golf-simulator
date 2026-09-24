using System.Collections;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Holes 13–15 in the game: each model loads and lines up with its numbers (the ball sits on
    /// the tee's ground, the pin on the green's), a tee shot down the recommended line flies and
    /// finishes on the island, and the Spiral's pinnacle stops a ball struck straight at the
    /// summit. Frames of each go to Library/Captures/review, with the menu's five course cards.
    public class NewHolesPlayTests
    {
        const string Dir = "Library/Captures/review";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheNewHolesPlay()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            game.ChooseHoles(0);
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/n-0-menu.png"));
            game.Play();
            yield return null;
            foreach (int number in new[] { 13, 14, 15 })
            {
                game.JumpToHole(number);
                yield return new WaitForSecondsRealtime(2.0f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/n-{number}-a-intro.png"));
                var hole = game.CurrentHole;
                Assert.AreEqual(number, hole.Number);
                Assert.IsNotNull(hole.Ground, $"hole {number}'s model gave it ground");
                double tee = HoleView.GroundHeight(hole.Tee), pin = HoleView.GroundHeight(hole.Pin);
                Assert.Greater(tee, 5, $"hole {number}: the tee is up on the island ({tee:F1})");
                Assert.Greater(pin, 5, $"hole {number}: the green is up on the island ({pin:F1})");
                game.DropBall(hole.Tee);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 5, "the tee shot to set up");
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/n-{number}-b-address.png"));
                game.StrikeToward(hole.RecommendedTarget(hole.Tee));
                yield return new WaitForSecondsRealtime(2.2f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/n-{number}-c-flight.png"));
                yield return WaitFor(() => game.Current == GolfGame.State.Result, 25, $"hole {number}'s tee shot to finish");
                Assert.IsTrue(hole.OnLand(game.LastShot.Rest), $"hole {number}: the recommended tee shot finishes on the island ({game.LastShot.Lie})");
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/n-{number}-d-result.png"));
                if (game.LastShot.IsHoled) { yield return WaitFor(() => game.Current == GolfGame.State.HoleDone, 10, "the ace to be scored"); continue; }
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next shot");
                if (number == 13)
                {
                    // Straight at the summit from the tee: into the pinnacle, not over it.
                    game.DropBall(hole.Tee);
                    yield return null;
                    game.StrikeToward(hole.Pin);
                    yield return WaitFor(() => game.Current == GolfGame.State.Result, 25, "the shot at the summit to finish");
                    Assert.Greater(game.LastShot.Rest.DistanceTo(hole.Pin), 30, "the pinnacle stopped it short of the summit green");
                    yield return WaitFor(() => game.Current == GolfGame.State.Aim, 10, "the next shot");
                }
            }
        }
    }
}
