using System.Collections;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GolfArcade.PlayTests
{
    /// The arcade cards through a round of the island hole: the yardage card at address, the swing
    /// card through the tee shot (carry and total filled in once the ball has run out), and the
    /// round's card at the end, whose Play Again goes straight back to the tee. Phone and TV
    /// frames of each go to Library/Captures/review.
    public class ArcadeCardsTests
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

        /// The course and the HUD's own camera into one 16:9 frame, as the big screen shows them.
        static void SaveTv(string path)
        {
            var tvHud = GameObject.Find("TV HUD camera")?.GetComponent<Camera>();
            Assert.IsNotNull(tvHud, "the HUD has a camera of its own for the TV");
            var course = Camera.main;
            var rt = new RenderTexture(1920, 1080, 24);
            var was = course.targetTexture;
            course.targetTexture = rt; course.aspect = 16f / 9f; tvHud.targetTexture = rt;
            course.Render(); tvHud.Render();
            RenderTexture.active = rt;
            var png = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); png.Apply();
            RenderTexture.active = null;
            course.targetTexture = was; course.ResetAspect(); tvHud.targetTexture = null;
            System.IO.Directory.CreateDirectory(Dir);
            System.IO.File.WriteAllBytes(path, png.EncodeToPNG());
            Object.Destroy(rt); Object.Destroy(png);
        }

        static string TileValue(GameObject card, int i) => System.Array.Find(card.GetComponentsInChildren<Transform>(true), t => t.name == $"Tile {i}")
            .Find("Value").GetComponent<Text>().text;

        /// Every landing badge, stamped over the island hole's tee, a frame each.
        [UnityTest]
        public IEnumerator TheLandingBadges()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            int holes = game.ChosenHoles;
            try
            {
                yield return null;
                game.ChooseHoles(12);
                game.Play();
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                var badges = new (GolfArcade.UI.LandingBadge.Kind kind, string word, string detail, string stats)[]
                {
                    (GolfArcade.UI.LandingBadge.Kind.Green, "On the green!", "12 ft to the hole", "Carry 173  ·  Total 190"),
                    (GolfArcade.UI.LandingBadge.Kind.Fairway, "Fairway", "142 yd to the pin", "Carry 173  ·  Total 190"),
                    (GolfArcade.UI.LandingBadge.Kind.Rough, "Rough", "Lie: rough  ·  158 yd", "Carry 173  ·  Total 190"),
                    (GolfArcade.UI.LandingBadge.Kind.Bunker, "Bunker!", "Sand  ·  64 yd", "Carry 173  ·  Total 190"),
                    (GolfArcade.UI.LandingBadge.Kind.Water, "Splash!  +1", "Drop  ·  penalty stroke", "Carry 173  ·  Total 190"),
                    (GolfArcade.UI.LandingBadge.Kind.OutOfBounds, "Out of bounds  +1", "Replay from the tee", "Carry 173  ·  Total 190"),
                    (GolfArcade.UI.LandingBadge.Kind.Putt, "So close!", "24 ft putt", null),
                    (GolfArcade.UI.LandingBadge.Kind.Holed, "Birdie!", "Hole 12  ·  par 3", "2 strokes"),
                };
                for (int i = 0; i < badges.Length; i++)
                {
                    var b = badges[i];
                    hud.ShowLanding(b.kind, b.word, b.detail, b.stats, 5f);
                    yield return new WaitForSecondsRealtime(0.5f);
                    Assert.IsNotNull(GameCapture.Save($"{Dir}/landing-{i}-{b.kind}.png"));
                }
                hud.HideLanding();
            }
            finally
            {
                PlayerPrefs.SetInt("holes", holes); PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator TheCardsThroughARound()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            int holes = game.ChosenHoles;
            try
            {
                yield return null;
                game.ChooseHoles(12);
                game.Play();
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
                yield return new WaitForSecondsRealtime(0.8f);

                // the yardage card: the distance, the club, what it plays, the wind, the lie
                var yardage = GameObject.Find("Shot card");
                Assert.IsTrue(yardage && yardage.activeInHierarchy, "the yardage card is up");
                var texts = yardage.GetComponentsInChildren<Text>();
                Assert.IsTrue(System.Array.Exists(texts, t => t.text == "PLAYS"), "it says what the shot plays");
                Assert.IsTrue(System.Array.Exists(texts, t => t.text == "LIE"), "it gives the lie");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/arcade-a-yardage.png"));
                game.ReviewTvHud(true);
                yield return null;
                SaveTv($"{Dir}/arcade-a-yardage-tv.png");
                game.ReviewTvHud(false);

                // the tee shot and its swing card
                var target = game.GreenTarget(7, 13, 11) ?? game.GreenTarget(4, 18, 10.5);
                Assert.IsTrue(target.HasValue, "no tee shot finishes on the green");
                game.StrikeToward(target.Value);
                yield return WaitFor(() => GameObject.Find("Swing card"), 10, "the swing card");
                var swing = GameObject.Find("Swing card");
                Assert.AreEqual("—", TileValue(swing, 4), "no carry until it comes down");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/arcade-b-swing-flight.png"));
                yield return WaitFor(() => game.Current == GolfGame.State.Result, 20, "the tee shot to finish");
                yield return new WaitForSecondsRealtime(0.3f);
                var landing = GameObject.Find("Landing badge");
                Assert.IsTrue(landing && landing.activeInHierarchy, "the landing is stamped");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/arcade-c2-landing.png"));
                Assert.AreEqual($"{game.LastShot.Carry:F0} YD", TileValue(swing, 4), "the carry, once it landed");
                Assert.AreEqual($"{game.LastShot.Total:F0} YD", TileValue(swing, 5), "the total, once it stopped");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/arcade-c-swing-done.png"));
                game.ReviewTvHud(true);
                yield return null;
                SaveTv($"{Dir}/arcade-c-swing-done-tv.png");
                game.ReviewTvHud(false);

                // hole out, and the round's card
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 8, "the putt");
                Assert.IsFalse(GameObject.Find("Swing card"), "the swing card is gone for the next shot");
                for (int putt = 0; putt < 4 && game.Current == GolfGame.State.Aim; putt++)
                {
                    yield return new WaitForSecondsRealtime(0.5f);
                    game.StrikeHolingPutt();
                    yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.HoleDone or GolfGame.State.RoundDone, 30, "the putt to be played");
                }
                yield return WaitFor(() => game.Current == GolfGame.State.RoundDone, 10, "the round's card");
                yield return new WaitForSecondsRealtime(0.6f);
                var card = GameObject.Find("Scorecard");
                Assert.IsTrue(card, "the round's card is up");
                var words = card.GetComponentsInChildren<Text>();
                Assert.IsTrue(System.Array.Exists(words, t => t.text == "PUTTS"), "a highlight counts the putts");
                Assert.IsNotNull(GameCapture.Save($"{Dir}/arcade-d-round.png"));

                // Play Again: straight back to the first tee, the card gone
                var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                hud.PlayAgain.Pressed();
                yield return WaitFor(() => game.Current is GolfGame.State.Intro or GolfGame.State.Aim, 10, "the round to start again");
                Assert.IsFalse(GameObject.Find("Scorecard"), "the card is put away");
                Assert.AreEqual(12, game.CurrentHole.Number);
            }
            finally
            {
                PlayerPrefs.SetInt("holes", holes); PlayerPrefs.Save();
            }
        }
    }
}
