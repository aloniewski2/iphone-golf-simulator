using System;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// What there is to chase on a round: golden rings laid along each tee shot's flight that a
    /// good shot threads (a glide ring only when it is there), a bounty on every hole drawn from
    /// what the hole has, done or missed by what the strokes do; and the dice the app shares to
    /// show the day's forecast round before it is played.
    public class ChallengeTests
    {
        bool wasEnabled;
        [SetUp] public void On() { wasEnabled = RoundEvents.Enabled; RoundEvents.Enabled = true; }
        [TearDown] public void Back() => RoundEvents.Enabled = wasEnabled;

        static Hole Straight(int par = 4, double length = 400) => new()
        {
            Number = 99, Par = par,
            Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, length) },
            FairwayWidth = 60, GreenRadius = 12, RoughWidth = 30,
            Obstacles = Array.Empty<Obstacle>(),
        };

        static SwingImpact Full(double power = 1, double curve = 0) => new() { Power = power, CurveDegrees = curve };

        [Test]
        public void RingsStandAlongTheTeeShotsFlight()
        {
            RoundEvents.Enabled = true;
            try
            {
                var hole = Straight();
                var e = RoundEvents.Deal(11);
                var rings = e.RingsFor(0, hole, PlayRules.Standard);
                Assert.AreEqual(3, rings.Length);
                CollectionAssert.AreEqual(new[] { RingKind.Gold, RingKind.Gold, RingKind.Glide }, rings.Select(r => r.Kind).ToArray());
                foreach (var r in rings)
                {
                    Assert.Greater(r.Y, r.Radius + 1, "up off the ground");
                    Assert.AreEqual(1, r.NormalD, 1e-6, "facing down the hole");
                }
                Assert.IsTrue(rings[0].D < rings[1].D && rings[1].D < rings[2].D, "one after another down the line");
                Assert.AreEqual(rings.Select(r => r.Y).ToArray(), RoundEvents.Deal(11).RingsFor(0, hole, PlayRules.Standard).Select(r => r.Y).ToArray(), "the same every time from the seed");
                var moon = new PlayRules(); Twist.MoonBall.Bend(moon);
                Assert.Greater(e.RingsFor(0, hole, moon)[0].Y, rings[0].Y + 3, "a moon ball's rings stand higher");
                var moonShot = CourseRings.StraightShot(hole.Tee, hole.RecommendedTarget(hole.Tee), moon).Value;
                Assert.IsTrue(e.RingsFor(0, hole, moon).All(r => r.Y <= moonShot.flight.Apex + 2), "on the arc of the club the game would pick, not above it");
                Assert.AreEqual(0, RoundEvents.Deal(11, challenges: false).RingsFor(0, hole, PlayRules.Standard).Length, "none with the challenges off");
                Assert.AreEqual(0, e.RingsFor(0, Straight(3, 60), PlayRules.Standard).Length, "none on a pitch");
            }
            finally { RoundEvents.Enabled = wasEnabled; }
        }

        [Test]
        public void AGoodStraightShotThreadsTheFirstRing()
        {
            RoundEvents.Enabled = true;
            try
            {
                var hole = Straight();
                var rings = RoundEvents.Deal(11).RingsFor(0, hole, PlayRules.Standard);
                var (_, heading, club, power) = CourseRings.StraightShot(hole.Tee, hole.RecommendedTarget(hole.Tee), PlayRules.Standard).Value;
                var shot = new CourseShot(club, Full(power), heading, hole.Tee, hole);
                var through = CourseRings.Through(shot, rings, 0, _ => 0);
                Assert.IsTrue(through.Any(p => p.Ring == 0), "the one on its line");
                Assert.IsTrue(through.All(p => p.Time > 0 && p.Time < shot.LandingTime), "in the air");
                Assert.IsFalse(CourseRings.Through(shot, rings, 0, _ => 0, new[] { true, false, false }).Any(p => p.Ring == 0), "a ring taken is gone");
                var wild = new CourseShot(club, Full(power), heading + 25, hole.Tee, hole);
                Assert.AreEqual(0, CourseRings.Through(wild, rings, 0, _ => 0).Count, "a wild one misses them all");
                var putt = new CourseShot(GolfClub.Putter, Full(1), 0, hole.Tee, hole);
                Assert.AreEqual(0, CourseRings.Through(putt, rings, 0, _ => 0).Count);

                // the glide ring slides across the line: only when it's there
                var glide = rings[2];
                bool met = false, missed = false;
                for (double phase = 0; phase < glide.Period; phase += glide.Period / 24)
                {
                    bool hit = CourseRings.Through(shot, new[] { glide }, phase, _ => 0).Count > 0;
                    met |= hit; missed |= !hit;
                }
                Assert.IsTrue(met && missed, "timed right it's threaded, timed wrong it's not");
            }
            finally { RoundEvents.Enabled = wasEnabled; }
        }

        [Test]
        public void TheBountyIsDrawnFromWhatTheHoleHas()
        {
            RoundEvents.Enabled = true;
            try
            {
                var four = Straight(); var three = Straight(3, 160);
                var balloon = new CourseMovers(new[] { Mover.Balloon(new CoursePoint(-40, 120), new CoursePoint(40, 120), 30, 60, 0, 0) });
                var rings = new[] { new Ring(RingKind.Gold, 0, 20, 100, 0, 1, 3.6) };
                var onFour = Enumerable.Range(1, 300).Select(s => RoundEvents.Deal(s).BountyFor(0, four, Weather.Storm, balloon, rings).Kind).ToList();
                var onThree = Enumerable.Range(1, 300).Select(s => RoundEvents.Deal(s).BountyFor(0, three, Weather.Clear, null, null).Kind).ToList();
                CollectionAssert.IsSubsetOf(new[] { BountyKind.Ring, BountyKind.Balloon, BountyKind.Fairway, BountyKind.ParInWeather, BountyKind.Birdie }, onFour.Distinct().ToArray());
                CollectionAssert.DoesNotContain(onFour, BountyKind.Close);
                CollectionAssert.Contains(onThree, BountyKind.Close);
                foreach (var k in new[] { BountyKind.Fairway, BountyKind.Ring, BountyKind.Balloon, BountyKind.ParInWeather, BountyKind.Drive })
                    CollectionAssert.DoesNotContain(onThree, k, "only what the hole has");
                Assert.IsNull(RoundEvents.Deal(1, challenges: false).BountyFor(0, four, Weather.Clear, null, null));
                StringAssert.Contains("thunderstorm", new Bounty(BountyKind.ParInWeather, Weather.Storm.Title(Sky.Rain)).Title);
            }
            finally { RoundEvents.Enabled = wasEnabled; }
        }

        static BountyShot S(int stroke, CourseLie rest, bool holed = false, bool putt = false, double toPin = 30, double fromPin = 100, double total = 200, int rings = 0, bool fromGreen = false) =>
            new() { Stroke = stroke, Par = 4, Rest = rest, Holed = holed, Putt = putt, RestPinYards = toPin, FromPinYards = fromPin, Total = total, Rings = rings, FromPuttingSurface = fromGreen };

        [Test]
        public void ABountyIsDoneOrMissedByTheStrokes()
        {
            var fairway = new Bounty(BountyKind.Fairway);
            Assert.IsTrue(fairway.Shot(S(1, CourseLie.Fairway)));
            Assert.AreEqual(BountyState.Done, fairway.State);
            Assert.IsFalse(fairway.Shot(S(2, CourseLie.Green)), "paid once");
            var rough = new Bounty(BountyKind.Fairway);
            Assert.IsFalse(rough.Shot(S(1, CourseLie.Rough)));
            Assert.AreEqual(BountyState.Missed, rough.State, "the tee shot found the rough");

            var birdie = new Bounty(BountyKind.Birdie);
            birdie.Shot(S(1, CourseLie.Fairway)); birdie.Shot(S(2, CourseLie.Green));
            Assert.IsTrue(birdie.Shot(S(3, CourseLie.Green, holed: true, putt: true)));
            var par = new Bounty(BountyKind.Birdie);
            par.Shot(S(1, CourseLie.Fairway)); par.Shot(S(2, CourseLie.Green));
            par.Shot(S(3, CourseLie.Green, putt: true));
            Assert.AreEqual(BountyState.Missed, par.State, "it can't be a birdie now");

            var onePutt = new Bounty(BountyKind.OnePutt);
            onePutt.Shot(S(1, CourseLie.Fairway)); onePutt.Shot(S(2, CourseLie.Green));
            onePutt.Shot(S(3, CourseLie.Green, putt: true));
            Assert.AreEqual(BountyState.Missed, onePutt.State, "a putt that stayed out");
            var chipIn = new Bounty(BountyKind.OnePutt);
            Assert.IsTrue(chipIn.Shot(S(2, CourseLie.Green, holed: true)), "holed without a putt is fine");

            var ring = new Bounty(BountyKind.Ring);
            Assert.IsFalse(ring.Shot(S(1, CourseLie.Fairway)));
            Assert.IsTrue(ring.Shot(S(2, CourseLie.Green, rings: 1)), "any shot of the hole will do");
            var two = new Bounty(BountyKind.TwoRings);
            Assert.IsFalse(two.Shot(S(1, CourseLie.Fairway, rings: 1)));
            Assert.IsTrue(two.Shot(S(2, CourseLie.Fairway, rings: 2)));

            var longPutt = new Bounty(BountyKind.LongPutt);
            Assert.IsFalse(longPutt.Shot(S(3, CourseLie.Green, holed: false, putt: true, fromPin: 9)));
            Assert.IsTrue(longPutt.Shot(S(4, CourseLie.Green, holed: true, putt: true, fromPin: 7.5)), "22 ft");
            var chip = new Bounty(BountyKind.ChipIn);
            Assert.IsFalse(chip.Shot(S(3, CourseLie.Green, holed: true, putt: true, fromGreen: true)));
            Assert.AreEqual(BountyState.Missed, chip.State, "a putt isn't a chip-in");

            var close = new Bounty(BountyKind.Close);
            Assert.IsTrue(close.Shot(S(1, CourseLie.Green, toPin: 4)));
            var drive = new Bounty(BountyKind.Drive);
            drive.Shot(S(1, CourseLie.Water, total: 290));
            Assert.AreEqual(BountyState.Missed, drive.State, "not into the water");

            var open = new Bounty(BountyKind.LongPutt);
            open.Close();
            Assert.AreEqual(BountyState.Missed, open.State, "the hole's over");
        }

        [Test]
        public void TheCodeCarriesTheChallengesSwitch()
        {
            RoundEvents.Enabled = true;
            var on = RoundEvents.Deal(5);
            Assert.IsTrue(on.Challenges);
            StringAssert.StartsWith("wc:", on.Code);
            Assert.IsTrue(RoundEvents.FromCode(5, on.Code).Challenges);
            Assert.IsTrue(RoundEvents.FromCode(5, on.Code).World);
            var off = RoundEvents.Deal(5, challenges: false);
            StringAssert.StartsWith("w:", off.Code);
            Assert.IsFalse(RoundEvents.FromCode(5, off.Code).Challenges, "a multiplayer round's (the host deals none)");
            Assert.IsTrue(RoundEvents.Deal(5, false, false, true).Challenges, "rings and bounties on their own");
            Assert.IsFalse(RoundEvents.Deal(5, false, false, true).World);
        }

        /// The app deals the day's forecast round itself to show it on the course screen
        /// (GolfArcade/Unity/GolfForecast.swift): these numbers are its test's too.
        [Test]
        public void TheDiceAreTheAppsDice()
        {
            RoundEvents.Enabled = true;
            var d = new Dice(20261013);
            Assert.AreEqual(0.4590061925890735, d.NextDouble(), 0);
            Assert.AreEqual(0.4882282494219624, d.NextDouble(), 0);
            Assert.AreEqual(0.3800729629255928, d.NextDouble(), 0);
            Assert.AreEqual(1878393768, RoundEvents.Mix(20261013, 0, 0x7157));
            CollectionAssert.AreEqual(new[] { Twist.Pinball, Twist.StickyGreens }, RoundEvents.Deal(20261013).Dealt);
            CollectionAssert.AreEqual(new[] { Twist.Hurricane, Twist.MoonBall }, RoundEvents.Deal(20261014).Dealt);
            var day = RoundEvents.Deal(20261013);
            CollectionAssert.AreEqual(new[] { Weather.Storm, Weather.Storm, Weather.Rain, Weather.Gusty, Weather.Clear, Weather.Storm }, Enumerable.Range(0, 6).Select(day.WeatherFor).ToArray());
            var seven = RoundEvents.Deal(7);
            CollectionAssert.AreEqual(new[] { Weather.Gusty, Weather.Clear, Weather.Rain, Weather.Fog, Weather.Fog, Weather.Storm }, Enumerable.Range(0, 6).Select(seven.WeatherFor).ToArray());
        }
    }
}
