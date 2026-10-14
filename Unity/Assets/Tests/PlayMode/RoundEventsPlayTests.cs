using System.Collections;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The round's events in the game: the twists dealt and revealed over the first hole, every
    /// kind of weather on the courses it belongs to (a storm with its lightning, a blizzard over
    /// the fjord, an eruption in the crater), and the balloons, carts and gulls about the holes —
    /// drawn where their paths have them, held to the flight's clock, met by the ball. Frames go to
    /// Library/Captures/events for a look.
    public class RoundEventsPlayTests
    {
        const string Dir = "Library/Captures/events";

        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        static IEnumerator Load()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheTwistsAreDealtOverTheFirstHole()
        {
            RoundEvents.Enabled = true;
            try
            {
                yield return Load();
                var cam = Camera.main;
                cam.aspect = 16f / 9f;
                var game = Object.FindFirstObjectByType<GolfGame>();
                game.InstantReplays = false;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return null;
                Assert.AreEqual(RoundEvents.TwistsPerRound, game.Events.Dealt.Length, "two twists dealt");
                var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                Assert.IsTrue(hud.TwistReveal.Showing, "revealed over the opening");
                yield return new WaitForSecondsRealtime(0.7f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/reveal-spinning.jpg", 1600, 900));
                yield return new WaitForSecondsRealtime(1.6f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/reveal-dealt.jpg", 1600, 900));
                // the rules the twists make are the hole's
                // (Hurricane and Point Frenzy leave the rules as they are: the wind and the rings are theirs)
                bool ruleFree = game.Events.Dealt.All(t => t is Twist.Hurricane or Twist.PointFrenzy);
                Assert.IsTrue(ruleFree || !game.CurrentHole.Rules.IsStandard, "the hole plays to the twists' rules");
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "the tee");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/aim-with-chips.jpg", 1600, 900));
            }
            finally { RoundEvents.Enabled = false; cleanUp(); }
        }

        static void cleanUp() { GolfGame.ForcedWeather = null; GolfGame.ForcedTwists = null; RoundEvents.EveryMover = false; Time.timeScale = 1f; }

        [UnityTest, Timeout(420000)]
        public IEnumerator EveryWeatherAndEveryMoverOnItsCourse()
        {
            RoundEvents.Enabled = true; RoundEvents.EveryMover = true;
            try
            {
                yield return Load();
                var cam = Camera.main;
                cam.aspect = 16f / 9f;
                var game = Object.FindFirstObjectByType<GolfGame>();
                game.InstantReplays = false;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return null;
                foreach (var (number, weather) in new[] { (7, Weather.Storm), (9, Weather.Rain), (14, Weather.Gusty), (17, Weather.Storm), (21, Weather.Storm), (19, Weather.Clear) })
                {
                    GolfGame.ForcedWeather = weather;
                    game.JumpToHole(number);
                    yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, $"hole {number}'s tee");
                    Assert.AreEqual(weather, game.CurrentWeather);
                    var movers = game.CurrentHole.Movers;
                    Assert.IsNotNull(movers, $"hole {number}: things moving about it");
                    yield return new WaitForSecondsRealtime(1.0f);
                    Assert.IsNotNull(GameCapture.Save($"{Dir}/{number:00}-{weather}-aim.jpg", 1600, 900));
                    if (weather == Weather.Storm)
                    {
                        game.StrikeLightning();
                        yield return null; yield return null;
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/{number:00}-{weather}-flash.jpg", 1600, 900));
                        Assert.Greater(game.WeatherShown.Strikes, 0);
                    }
                    // a look at each mover from close by, the HUD out of the way
                    var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>().GetComponent<Canvas>();
                    hud.enabled = false;
                    foreach (var kind in new[] { MoverKind.Balloon, MoverKind.Cart, MoverKind.Gull, MoverKind.Twister })
                    {
                        var m = movers.All.FirstOrDefault(x => x.Kind == kind);
                        if (m == null) continue;
                        var (x, y, d) = m.At(movers.Now);
                        var at = new Vector3((float)x, (float)y, (float)d);
                        var tee = HoleView.ToWorld(game.CurrentHole.Tee);
                        var from = tee - at; from.y = 0; from = from.normalized;
                        float back = kind == MoverKind.Balloon ? 26 : kind == MoverKind.Cart ? 9 : kind == MoverKind.Twister ? 55 : 6;
                        game.enabled = false;
                        cam.transform.position = at + from * back + Vector3.up * (kind == MoverKind.Balloon ? -4 : kind == MoverKind.Twister ? 6 : 2.5f);
                        cam.transform.LookAt(at + (kind == MoverKind.Balloon ? Vector3.down * 2.5f : kind == MoverKind.Twister ? Vector3.up * 14 : Vector3.up * 0.6f));
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/{number:00}-{weather}-{kind}.jpg", 1600, 900));
                        game.enabled = true;
                    }
                    // and the whole hole from high behind the tee: where they all are
                    {
                        var hole = game.CurrentHole;
                        var tee = HoleView.ToWorld(hole.Tee); var pin = HoleView.ToWorld(hole.Pin);
                        var along = pin - tee; along.y = 0; float length = along.magnitude; along /= length;
                        game.enabled = false;
                        cam.transform.position = tee - along * 40 + Vector3.up * (length * 0.3f + 30);
                        cam.transform.LookAt((tee + pin) / 2);
                        Assert.IsNotNull(GameCapture.Save($"{Dir}/{number:00}-{weather}-overview.jpg", 1600, 900));
                        game.enabled = true;
                    }
                    hud.enabled = true;
                }
            }
            finally { RoundEvents.Enabled = false; cleanUp(); }
        }

        /// The weather turning before a shot, stamped like a landing, and the word over the picture
        /// when the ball meets something moving about the hole.
        [UnityTest, Timeout(180000)]
        public IEnumerator TheEventBadgeAndTheCallout()
        {
            RoundEvents.Enabled = true;
            try
            {
                yield return Load();
                var cam = Camera.main;
                cam.aspect = 16f / 9f;
                var game = Object.FindFirstObjectByType<GolfGame>();
                game.InstantReplays = false;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "the tee");
                var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                hud.ShowEvent("Storm!", Weather.Storm.Line(Sky.Rain), new Color(0.36f, 0.37f, 0.78f), "bolt");
                yield return new WaitForSecondsRealtime(0.6f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/event-badge.jpg", 1600, 900));
                hud.HideLanding();
                hud.ShowCallout("Boing!", new Color(1f, 0.44f, 0.66f));
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/callout.jpg", 1600, 900));
            }
            finally { RoundEvents.Enabled = false; cleanUp(); }
        }

        /// The rings over a tee shot, the bounty and the coins in the HUD, a shot flown at them;
        /// fog closing in, and a rainbow over the hole when the skies clear.
        [UnityTest, Timeout(300000)]
        public IEnumerator RingsBountiesFogAndTheRainbow()
        {
            RoundEvents.Enabled = true;
            // (the rings stand only in a Point Frenzy round)
            GolfGame.ForcedTwists = new[] { Twist.PointFrenzy, Twist.BananaBall };
            try
            {
                yield return Load();
                var cam = Camera.main;
                cam.aspect = 16f / 9f;
                var game = Object.FindFirstObjectByType<GolfGame>();
                game.InstantReplays = false;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return null;
                GolfGame.ForcedWeather = Weather.Clear;
                game.JumpToHole(7);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "hole 7's tee");
                var hud = Object.FindFirstObjectByType<GolfArcade.UI.Hud>();
                hud.HideTwists();
                Assert.IsNotNull(game.HoleBounty, "a bounty on the hole");
                var rings = game.CurrentHole.Movers?.Rings;
                Assert.IsNotNull(rings); Assert.AreEqual(3, rings.Length, "rings over the tee shot");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/rings-aim.jpg", 1600, 900));
                // the rings from beside the line, the HUD out of the way
                var canvas = hud.GetComponent<Canvas>();
                {
                    canvas.enabled = false; game.enabled = false;
                    var (x, y, d) = rings[1].At(game.CurrentHole.Movers.Now);
                    var mid = new Vector3((float)x, (float)y, (float)d);
                    cam.transform.position = mid + new Vector3(-26, 4, -34);
                    cam.transform.LookAt(mid + Vector3.forward * 8);
                    Assert.IsNotNull(GameCapture.Save($"{Dir}/rings-close.jpg", 1600, 900));
                    game.enabled = true; canvas.enabled = true;
                }
                // a tee shot down the line at them, in still air
                game.SetWind(new Wind(0, 0));
                game.StrikeToward(game.CurrentHole.RecommendedTarget(game.CurrentHole.Tee));
                yield return WaitFor(() => game.Current == GolfGame.State.Flight, 10, "the flight");
                float until = Time.realtimeSinceStartup + 8;
                while (game.RoundRings == 0 && game.Current == GolfGame.State.Flight && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(0.12f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/rings-flight.jpg", 1600, 900));
                yield return WaitFor(() => game.Current != GolfGame.State.Flight, 40, "the shot to finish");
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/rings-after.jpg", 1600, 900));
                Debug.Log($"rings flown {game.RoundRings}, coins {game.RoundCoins}, bounty {game.HoleBounty.Title} {game.HoleBounty.State}");

                // fog on the next hole
                GolfGame.ForcedWeather = Weather.Fog;
                game.JumpToHole(9);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "hole 9's tee");
                Assert.AreEqual(Weather.Fog, game.CurrentWeather);
                Assert.LessOrEqual(game.Wind.SpeedMPH, 4, "fog hangs in still air");
                yield return new WaitForSecondsRealtime(5.5f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/09-Fog-aim.jpg", 1600, 900));
                // and a rainbow once it has rained
                GolfGame.ForcedWeather = Weather.Clear;
                game.JumpToHole(12);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "hole 12's tee");
                game.WeatherShown.Rainbow(game.CurrentHole.Pin);
                yield return new WaitForSecondsRealtime(4f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/12-rainbow.jpg", 1600, 900));
            }
            finally { RoundEvents.Enabled = false; cleanUp(); }
        }

        /// A hole played out with the rings and bounty on: the card after it counts the round's
        /// coins and bounties where the strike and speed tiles were.
        [UnityTest, Timeout(300000)]
        public IEnumerator TheCardCountsTheCoins()
        {
            RoundEvents.Enabled = true;
            try
            {
                yield return Load();
                var cam = Camera.main;
                cam.aspect = 16f / 9f;
                var game = Object.FindFirstObjectByType<GolfGame>();
                game.InstantReplays = false;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return null;
                GolfGame.ForcedWeather = Weather.Clear;
                game.JumpToHole(12);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "hole 12's tee");
                float until = Time.realtimeSinceStartup + 200;
                while (game.Current != GolfGame.State.RoundDone && Time.realtimeSinceStartup < until)
                {
                    if (game.Current == GolfGame.State.Aim) { game.SetWind(new Wind(0, 0)); game.StrikeToward(game.CurrentHole.Pin); }
                    else if (game.Current == GolfGame.State.Result) game.ContinueShotResult();
                    yield return null;
                }
                Assert.AreEqual(GolfGame.State.RoundDone, game.Current, "the hole played out");
                Assert.AreNotEqual(BountyState.Open, game.HoleBounty.State, "the bounty settled with the hole");
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.IsNotNull(GameCapture.Save($"{Dir}/card-coins.jpg", 1600, 900));
            }
            finally { RoundEvents.Enabled = false; cleanUp(); }
        }

        /// A tee shot played through the hole's movers: the flight holds their clock, the picture
        /// shows them where the ball meets them, and after the shot they move on from there.
        [UnityTest, Timeout(240000)]
        public IEnumerator MoversKeepTheFlightsClock()
        {
            RoundEvents.Enabled = true; RoundEvents.EveryMover = true;
            try
            {
                yield return Load();
                var game = Object.FindFirstObjectByType<GolfGame>();
                game.InstantReplays = false;
                game.ChooseHoles(0, "cliffside");
                game.Play();
                yield return null;
                GolfGame.ForcedWeather = Weather.Clear;
                game.JumpToHole(7);
                yield return WaitFor(() => game.Current == GolfGame.State.Aim, 60, "hole 7's tee");
                var movers = game.CurrentHole.Movers;
                Assert.IsNotNull(movers);
                double before = movers.Now;
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.Greater(movers.Now, before + 0.3, "they move by themselves");
                game.StrikeToward(game.CurrentHole.RecommendedTarget(game.CurrentHole.Tee));
                yield return WaitFor(() => game.Current == GolfGame.State.Flight, 10, "the flight");
                yield return new WaitForSecondsRealtime(1.0f);
                Assert.AreEqual(movers.PhaseAtLaunch + 1.0, movers.Now, 0.35, "held to the flight's clock");
                yield return WaitFor(() => game.Current != GolfGame.State.Flight, 40, "the shot to finish");
                double after = movers.Now;
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.Greater(movers.Now, after + 0.3, "and on they go");
            }
            finally { RoundEvents.Enabled = false; cleanUp(); }
        }
    }
}
