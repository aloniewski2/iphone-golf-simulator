using System.Collections;
using System.Collections.Generic;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Profile;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Two golfers on one phone alternate shots: P1 tees off, then P2 from the tee, then P1 from
    /// where their ball lies, and so on; whoever holes out drops out and the other plays on; the
    /// hole ends on the card with a row each. Frames go to Library/Captures/review/versus-*.png.
    public class LocalVersusTests
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

        static Vector3 BallOnCourse() => GameObject.Find("Ball").transform.position;

        [UnityTest]
        public IEnumerator TwoGolfersAlternateShots()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var cam = Camera.main;
            if (cam) cam.aspect = GameCapture.PhoneWidth / (float)GameCapture.PhoneHeight;
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            var alex = new PlayerProfile { Name = "Alex", Body = 0, Kit = 0 };
            var sam = new PlayerProfile { Name = "Sam", Body = 1, Kit = 1 };
            game.Begin(GameSetup.LocalVersus(new[] { alex, sam }, 12));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the first tee");
            var hole = game.CurrentHole;
            var tee = HoleView.ToWorld(hole.Tee);
            Assert.AreEqual(0, game.TurnIndex, "P1 tees off");

            // P1's tee shot, then P2 steps up on the tee
            yield return new WaitForSecondsRealtime(0.4f);
            game.StrikeToward(hole.RecommendedTarget(hole.Tee));
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 30, "P1's tee shot to finish");
            Assert.AreEqual(1, game.TurnIndex, "then P2");
            Assert.Less(Vector3.Distance(BallOnCourse(), tee), 1.5f, "P2 plays from the tee");
            Assert.AreEqual("Sam", game.Match.Current.Name);
            Assert.AreEqual(sam.Body, (int)GolferStyle.Body, "P2 as their own golfer");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/versus-a-p2-tee.png"));

            // then back to P1, from where P1's ball lies, and round again until both are down
            var order = new List<int> { 0, 1 };
            for (int shot = 0; shot < 16 && game.Current == GolfGame.State.Aim; shot++)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                bool onGreen = hole.LieAt(HoleView.ToCourse(BallOnCourse())).IsPuttingSurface();
                if (!onGreen || !game.StrikeHolingPutt()) game.StrikeToward(hole.Pin);
                yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.RoundDone, 40, "the shot to be played");
                if (game.Current == GolfGame.State.Aim)
                {
                    order.Add(game.TurnIndex);
                    if (order.Count == 3)
                    {
                        Assert.AreEqual(0, game.TurnIndex, "P1 again after P2");
                        Assert.Greater(Vector3.Distance(BallOnCourse(), tee), 20f, "from P1's ball, not the tee");
                        Assert.AreEqual(alex.Body, (int)GolferStyle.Body, "P1 as their own golfer again");
                        Assert.AreEqual(alex.Kit, GolferStyle.Kit);
                        yield return new WaitForSecondsRealtime(0.3f);
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/versus-b-p1-second.png"));
                    }
                }
            }
            yield return WaitFor(() => game.Current == GolfGame.State.RoundDone, 15, "the card");
            // strict turns while both are still on the hole: 0, 1, 0, 1 …
            int alternating = 0;
            for (int i = 1; i < order.Count && order[i] != order[i - 1]; i++) alternating = i;
            Assert.GreaterOrEqual(alternating, 2, $"they alternate ({string.Join(",", order)})");
            foreach (var p in game.Match.Players) Assert.IsTrue(p.Card.StrokesOn(0).HasValue, $"{p.Name} holed out");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsNotNull(GameCapture.Save($"{Dir}/versus-c-card.png"));
            game.ShowMenu();
        }
    }
}
