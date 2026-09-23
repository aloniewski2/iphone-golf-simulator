using System.Collections;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The good ones come round again: an approach struck pure to a few feet, and a holed putt,
    /// are each replayed (low behind the golfer, out by the landing; behind the rolling putt)
    /// before play goes on. Saves frames of the strike's word and the replays to
    /// Library/Captures/review.
    public class ReplayTests
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
        public IEnumerator APureApproachAndAHoledPuttAreReplayed()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            Assert.IsNotNull(game);
            yield return null;
            game.Play();
            yield return null;
            game.JumpToHole(12);
            yield return new WaitForSecondsRealtime(0.5f);
            var hole = game.CurrentHole;

            // The tee shot, struck pure at the flag: the word pops at the strike.
            game.DropBall(hole.Tee);
            yield return null;
            var target = game.GreenTarget(0.5, 3.5, 20);
            Assert.IsTrue(target.HasValue, "somewhere on 12 an approach finishes by the pin");
            game.StrikeToward(target.Value);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 3, "the strike");
            yield return new WaitForSecondsRealtime(0.75f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/r-1-perfect.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 25, "the approach to finish");
            Assert.LessOrEqual(game.LastShot.Rest.DistanceTo(hole.Pin), 4, "it finished by the pin");
            yield return WaitFor(() => game.Replaying, 5, "the approach's replay");
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/r-2-replay-launch.png"));
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/r-3-replay-follow.png"));
            yield return WaitFor(() => !game.Replaying || game.Bounces > 0 && Time.timeScale == 1f && game.ReplayCut, 12, "the replay's landing camera");
            yield return new WaitForSecondsRealtime(0.8f);
            if (game.Replaying) Assert.IsNotNull(GameCapture.Save($"{Dir}/r-3b-replay-landing.png"));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 25, "the replay to end and the putt to set up");
            Assert.AreEqual(1f, Time.timeScale, "the slow motion is over");

            // The putt, holed: replayed from behind the ball before the hole is scored.
            Assert.IsTrue(game.StrikeHolingPutt(), "the putt drops");
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the putt to drop");
            if (game.LastShot.Origin.DistanceTo(hole.Pin) > 3)
            {
                yield return WaitFor(() => game.Replaying, 5, "the holed putt's replay");
                yield return new WaitForSecondsRealtime(1.3f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/r-4-replay-putt.png"));
            }
            yield return WaitFor(() => game.Current == GolfGame.State.HoleDone, 20, "the hole to be scored");
        }
    }
}
