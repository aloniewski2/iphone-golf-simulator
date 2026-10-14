using System.Collections;
using System.Collections.Generic;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GolfArcade.PlayTests
{
    /// What follows holing out, and the way to the next hole: one result card (HoleOutCard) with the score, the
    /// round so far and NEXT HOLE; then the loading screen (HoleLoadingCover) up and drawn BEFORE the next hole is
    /// built, the hole built in slices behind it, and the new hole's tee. Phone and TV frames of each go to
    /// Library/Captures/review (run windowed: Unity/Tools/run-playmode-windowed.sh HoleFlowTests).
    public class HoleFlowTests
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

        /// The hole played quickly: the tee shot where the hole says, then the ball a few yards from the pin and
        /// putted out; returns with the result on screen.
        static IEnumerator PlayOutToTheResult(GolfGame game)
        {
            var hole = game.CurrentHole;
            yield return new WaitForSecondsRealtime(0.4f);
            game.StrikeToward(hole.RecommendedTarget(hole.Tee));
            yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.Result or GolfGame.State.HoleDone, 40, "the tee shot to finish");
            if (game.Current == GolfGame.State.Result) yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.HoleDone, 20, "the result to pass");
            if (game.Current == GolfGame.State.Aim)
            {
                var pin = hole.Pin; double d = pin.DistanceTo(hole.Tee);
                game.DropBall(new GolfArcade.Course.CoursePoint(pin.X + (hole.Tee.X - pin.X) * 4 / d, pin.D + (hole.Tee.D - pin.D) * 4 / d));
                for (int putt = 0; putt < 4 && game.Current == GolfGame.State.Aim; putt++)
                {
                    yield return new WaitForSecondsRealtime(0.4f);
                    game.StrikeHolingPutt();
                    yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.Result, 30, "the putt to be played");
                }
            }
            yield return WaitFor(() => game.Current == GolfGame.State.Result, 10, "the hole's result");
            yield return new WaitForSecondsRealtime(0.5f);
        }

        static void Shot(Camera cam, string name, bool tv, GolfGame game = null)
        {
            float was = cam.aspect;
            cam.aspect = tv ? 16f / 9 : GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            game?.ReframeResultForTests();
            Assert.IsNotNull(tv ? GameCapture.Save($"{Dir}/{name}-tv.png", 1920, 1080) : GameCapture.Save($"{Dir}/{name}-phone.png"));
            cam.aspect = was;
            game?.ReframeResultForTests();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator HolingOutShowsOneCardAndTheNextHoleLoadsBehindAScreen()
        {
            Time.timeScale = 1f;
            GolfGame.LoadCoverMinimum = 0.5f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            int holes = game.ChosenHoles; string chosen = PlayerPrefs.GetString("course", "cliffside");
            try
            {
                yield return null;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "the first tee");
                var first = game.CurrentHole;
                Assert.AreEqual(1, first.PlayNumber);

                yield return PlayOutToTheResult(game);
                Assert.IsTrue(game.HoleOutShowing, "the card for the hole just holed is up");
                var card = GameObject.Find("Hole out card");
                Assert.IsTrue(card && card.activeInHierarchy);
                var words = System.Array.ConvertAll(card.GetComponentsInChildren<Text>(true), t => t.text);
                Assert.IsTrue(System.Array.Exists(words, w => w.StartsWith("HOLE 1 OF 5")), "it says which hole of the round: " + string.Join(" | ", words));
                foreach (var label in new[] { "HOLE", "PAR", "YOU", "TOTAL" })
                    Assert.IsTrue(System.Array.Exists(words, w => w == label), $"the scoreboard has its {label} row or column: " + string.Join(" | ", words));
                Assert.IsNotNull(game.ResultEmote, "the golfer is reacting to the score, in the clear of the card");
                Assert.IsTrue(game.PlayResultEmote(0), "an emote can be played on the spot");
                float before = game.ResultWaitSeconds;
                yield return null;
                Assert.IsNotNull(game.ResultEmote);
                Assert.Greater(game.ResultWaitSeconds, game.ResultSeconds + 3f, "the game waits for the emote to play out before it goes on by itself");
                Assert.IsTrue(System.Array.Exists(words, w => w.StartsWith("NEXT HOLE")), "it offers the next hole");
                Assert.IsTrue(System.Array.Exists(words, w => w.StartsWith("2  ·")), "and says which: " + string.Join(" | ", words));
                Assert.IsFalse(GameObject.Find("Shot summary") && GameObject.Find("Shot summary").activeInHierarchy, "the shot's stats panel stays away");
                Assert.IsFalse(GameObject.Find("Scorecard"), "and so does the full scorecard");
                yield return new WaitForSecondsRealtime(1.3f);   // the card has finished its reveal
                Shot(cam, "holeout", false, game);
                Shot(cam, "holeout", true, game);

                // on: the loading screen goes up and is OPAQUE before the first slice of the build runs
                game.ContinueShotResult();
                bool sawLoading = false, coverOpaqueBeforeBuild = false, shotLoadingPhone = false, shotLoadingTv = false;
                float until = Time.realtimeSinceStartup + 90;
                while (!(game.Current is GolfGame.State.Intro or GolfGame.State.Aim) || game.LoadingHole)
                {
                    if (Time.realtimeSinceStartup > until) Assert.Fail("timed out waiting for the next hole");
                    if (game.LoadingHole)
                    {
                        sawLoading = true;
                        var cover = game.LoadingCover;
                        Assert.IsNotNull(cover);
                        if (game.HoleLoadLog.Count == 0 && cover.Opaque) coverOpaqueBeforeBuild = true;
                        Assert.AreEqual("Loading", game.NativeState, "the phone is told");
                        if (game.HoleLoadLog.Count >= 3 && cover.Opaque && !shotLoadingPhone) { shotLoadingPhone = true; Shot(cam, "loading", false); }
                        else if (shotLoadingPhone && !shotLoadingTv && game.HoleLoadLog.Count >= 4) { shotLoadingTv = true; Shot(cam, "loading", true); }
                    }
                    yield return null;
                }
                Assert.IsTrue(sawLoading, "the next hole was loaded behind a screen");
                Assert.IsTrue(coverOpaqueBeforeBuild, "the screen was fully up before any of the hole was built");
                Assert.GreaterOrEqual(game.HoleLoadLog.Count, 4, "the hole was built in slices, the screen drawing between them");
                Assert.AreEqual(2, game.CurrentHole.PlayNumber, "on to the second hole");
                Assert.AreEqual(first.Number, game.Card.Course.Holes[0].Number, "the same round");
                Debug.Log($"[HoleFlowTests] hole {game.CurrentHole.Number} loaded in {game.LastHoleLoadMilliseconds:F0} ms, longest slice {game.LongestLoadSliceMilliseconds:F0} ms, {game.HoleLoadLog.Count} slices");
                yield return new WaitForSecondsRealtime(GolfArcade.UI.HoleLoadingCover.FadeOut + 0.3f);
                Assert.IsFalse(game.LoadingCover.Visible, "the screen is gone");
                Assert.IsFalse(game.HoleOutShowing, "and so is the card");
            }
            finally
            {
                GolfGame.LoadCoverMinimum = 0.9f;
                PlayerPrefs.SetInt("holes", holes); PlayerPrefs.SetString("course", chosen); PlayerPrefs.Save();
            }
        }

        /// The last hole: the card says SCORECARD, and the round's own card follows it.
        [UnityTest, Timeout(600000)]
        public IEnumerator TheLastHoleEndsOnTheRoundCardAndARematchLoadsBehindTheScreen()
        {
            Time.timeScale = 1f;
            GolfGame.LoadCoverMinimum = 0.3f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
            int holes = game.ChosenHoles; string chosen = PlayerPrefs.GetString("course", "cliffside");
            try
            {
                yield return null;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "the first tee");
                var holesOfRound = game.Card.Course.Holes;
                game.JumpToHole(holesOfRound[holesOfRound.Length - 1].Number);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "the last tee");
                yield return PlayOutToTheResult(game);
                Assert.IsTrue(game.HoleOutShowing);
                var words = System.Array.ConvertAll(GameObject.Find("Hole out card").GetComponentsInChildren<Text>(true), t => t.text);
                Assert.IsTrue(System.Array.Exists(words, w => w.StartsWith("SCORECARD")), "the way on is the scorecard: " + string.Join(" | ", words));
                Assert.IsTrue(System.Array.Exists(words, w => w.StartsWith("HOLE 5 OF 5")), "the round's last hole");
                yield return new WaitForSecondsRealtime(1.3f);
                Shot(cam, "holeout-last", false, game);
                game.ContinueShotResult();
                yield return WaitFor(() => game.Current == GolfGame.State.RoundDone, 20, "the round's card");
                yield return new WaitForSecondsRealtime(0.6f);
                var card = GameObject.Find("Scorecard");
                Assert.IsTrue(card, "the round's card is up");
                words = System.Array.ConvertAll(card.GetComponentsInChildren<Text>(), t => t.text);
                Assert.IsTrue(System.Array.Exists(words, w => w == "ROUND COMPLETE"));
                Assert.IsFalse(GameObject.Find("Next hole"), "no next hole after the last");
                Assert.IsFalse(game.HoleOutShowing);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/round-card-phone.png"));

                // PLAY AGAIN: back to the first tee, the screen up while it is built
                hud.PlayAgain.Pressed();
                yield return null; yield return null;
                Assert.IsTrue(game.LoadingHole, "the rematch is loaded behind the screen");
                yield return WaitFor(() => !game.LoadingHole && game.Current is GolfGame.State.Intro or GolfGame.State.Aim, 90, "the first tee again");
                Assert.AreEqual(1, game.CurrentHole.PlayNumber);
                Assert.IsFalse(GameObject.Find("Scorecard"), "the card is put away");
            }
            finally
            {
                GolfGame.LoadCoverMinimum = 0.9f;
                PlayerPrefs.SetInt("holes", holes); PlayerPrefs.SetString("course", chosen); PlayerPrefs.Save();
            }
        }
    }
}
