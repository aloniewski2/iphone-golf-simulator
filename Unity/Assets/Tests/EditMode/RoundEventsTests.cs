using System;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The round's events: twists dealt from the seed and the same on every phone, each twist
    /// doing what its card says to the ball, the weather and its gusts, and the balloons, carts
    /// and gulls the ball can meet where their paths have them when it gets there.
    public class RoundEventsTests
    {
        bool wasEnabled;
        [SetUp] public void On() { wasEnabled = RoundEvents.Enabled; RoundEvents.Enabled = true; }
        [TearDown] public void Back() => RoundEvents.Enabled = wasEnabled;

        static Hole Straight(PlayRules rules = null, params Obstacle[] obstacles) => new()
        {
            Number = 99, Par = 4,
            Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 400) },
            FairwayWidth = 60, GreenRadius = 12, RoughWidth = 30,
            Obstacles = obstacles ?? Array.Empty<Obstacle>(),
            Rules = rules ?? PlayRules.Standard,
        };

        static PlayRules With(Twist t) { var r = new PlayRules(); t.Bend(r); return r; }
        static SwingImpact Full(double power = 1, double curve = 0) => new() { Power = power, CurveDegrees = curve };

        [Test]
        public void TheSameSeedDealsTheSameRoundAndTheCodeCarriesIt()
        {
            for (int seed = 1; seed < 200; seed++)
            {
                var a = RoundEvents.Deal(seed); var b = RoundEvents.Deal(seed);
                CollectionAssert.AreEqual(a.Dealt, b.Dealt);
                Assert.AreEqual(RoundEvents.TwistsPerRound, a.Dealt.Length);
                Assert.AreEqual(a.Dealt.Length, a.Dealt.Distinct().Count(), "no twist twice");
                Assert.IsFalse(a.Has(Twist.TurboGreens) && a.Has(Twist.StickyGreens), "the green twists don't come together");
                var c = RoundEvents.FromCode(seed, a.Code);
                CollectionAssert.AreEqual(a.Dealt, c.Dealt, "another phone plays the host's twists");
                for (int h = 0; h < 5; h++)
                {
                    Assert.AreEqual(a.WeatherFor(h), c.WeatherFor(h));
                    for (int shot = 1; shot < 5; shot++) Assert.AreEqual(a.TurnBefore(h, shot, Weather.Rain), c.TurnBefore(h, shot, Weather.Rain));
                }
            }
            var dealt = Enumerable.Range(1, 400).SelectMany(s => RoundEvents.Deal(s).Dealt).Distinct().Count();
            Assert.AreEqual(Twists.All.Length, dealt, "every twist comes up");
        }

        [Test]
        public void TheMatchSettingsTurnThemOff()
        {
            Assert.AreEqual(0, RoundEvents.Deal(7, twists: false).Dealt.Length, "no twists");
            Assert.IsTrue(RoundEvents.Deal(7, twists: false).World, "but the weather still blows");
            var still = RoundEvents.Deal(7, world: false);
            Assert.AreEqual(2, still.Dealt.Length);
            for (int h = 0; h < 10; h++) Assert.AreEqual(Weather.Clear, still.WeatherFor(h), "clear skies all round");
            Assert.IsNull(still.MoversFor(0, Straight(), Weather.Clear), "and nothing moving about");
            var plain = RoundEvents.Deal(7, false, false, false);
            Assert.AreSame(RoundEvents.None, plain);
            Assert.IsTrue(plain.RulesFor(Weather.Clear).IsStandard);
            Assert.AreEqual("", plain.Code);
            Assert.AreSame(RoundEvents.None, RoundEvents.FromCode(7, ""));
            Assert.IsFalse(RoundEvents.FromCode(7, still.Code).World, "the code carries the world switch too");
            RoundEvents.Enabled = false;
            try { Assert.AreSame(RoundEvents.None, RoundEvents.Deal(7), "off altogether (the automated tests)"); }
            finally { RoundEvents.Enabled = true; }
        }

        [Test]
        public void PlainGolfIsUntouched()
        {
            var plain = new CourseShot(GolfClub.Driver, Full(), 0, new CoursePoint(0, 0), Straight());
            var standard = new CourseShot(GolfClub.Driver, Full(), 0, new CoursePoint(0, 0), Straight(new PlayRules()));
            Assert.AreEqual(plain.Rest.D, standard.Rest.D, 1e-9);
            Assert.AreEqual(plain.Carry, standard.Carry, 1e-9);
        }

        [Test]
        public void LowGravityFloatsFartherAndTheClubsKnowIt()
        {
            // (no twist lightens gravity any more, but the rules still can)
            var moon = new PlayRules { Gravity = 0.45 };
            var plain = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            var floaty = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight(moon));
            Assert.Greater(floaty.Carry, plain.Carry * 1.3, "it carries much farther");
            Assert.Greater(floaty.Apex, plain.Apex * 1.3, "and higher");
            Assert.Greater(floaty.CarryTime, plain.CarryTime * 1.2, "and hangs");
            Assert.Greater(moon.CarryFactor(GolfClub.Iron), 1.3);
            Assert.AreEqual(1, PlayRules.Standard.CarryFactor(GolfClub.Iron));
            // the pick for 150 yards: a shorter club under the moon
            var pick = GolfClubs.ForDistance(150, c => moon.CarryFactor(c));
            Assert.Greater(Array.IndexOf(GolfClubs.All, pick), Array.IndexOf(GolfClubs.All, GolfClub.Iron));
        }

        [Test]
        public void BounceHouseHopsAndRuns()
        {
            var plain = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            var bouncy = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight(With(Twist.BounceHouse)));
            Assert.AreEqual(plain.Carry, bouncy.Carry, 0.5, "the carry is the same");
            Assert.Greater(bouncy.Total, plain.Total + 8, "but it bounds on");
        }

        [Test]
        public void TurboGreensRunAndStickyGreensHold()
        {
            var green = new Hole { Number = 98, Par = 3, Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 30) }, FairwayWidth = 30, GreenRadius = 40 };
            var putt = new CourseShot(GolfClub.Putter, Full(0.5), 20, new CoursePoint(0, 0), green);
            green.Rules = With(Twist.TurboGreens);
            var quick = new CourseShot(GolfClub.Putter, Full(0.5), 20, new CoursePoint(0, 0), green);
            Assert.Greater(quick.Total, putt.Total * 1.6, "the same stroke rolls far past");
            Assert.AreEqual(2, green.Rules.CarryFactor(GolfClub.Putter), 1e-9, "and the putter's yardage says so");

            var hole = Straight();
            var approach = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 252), hole);
            hole.Rules = With(Twist.StickyGreens);
            var held = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 252), hole);
            Assert.AreEqual(CourseLie.Green, hole.LieAt(held.Landing), "(it comes down on the green)");
            Assert.Less(held.Roll, approach.Roll * 0.5, "the green grabs it");
        }

        [Test]
        public void TheBucketCupSwallowsWhatTheCupWouldMiss()
        {
            var hole = new Hole { Number = 97, Par = 3, Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 6) }, FairwayWidth = 30, GreenRadius = 20 };
            // a putt just wide of the cup (past where its soft edge draws a dying ball in), rolled along the line
            double wide = Hole.CupCaptureRadius + 0.22;
            var from = new CoursePoint(wide, 0);
            double aim = from.HeadingTo(new CoursePoint(wide, 6));
            bool Holes(double power) => new CourseShot(GolfClub.Putter, Full(power), aim, from, hole).IsHoled;
            Assert.IsFalse(Enumerable.Range(10, 60).Any(p => Holes(p / 100.0)), "the cup it slides by");
            hole.Rules = With(Twist.BucketCup);
            Assert.AreEqual(Hole.CupCaptureRadius * 2, hole.CupRadius, 1e-9);
            Assert.IsTrue(Enumerable.Range(10, 60).Any(p => Holes(p / 100.0)), "the bucket takes it");
        }

        [Test]
        public void PinballFiresItBackAndBananaBallBends()
        {
            var wall = Obstacle.Round(ObstacleKind.Wall, 0, 25, 0, 40, 3);
            var thud = new CourseShot(GolfClub.Iron, Full(0.8), 0, new CoursePoint(0, 0), Straight(null, wall));
            var pinball = new CourseShot(GolfClub.Iron, Full(0.8), 0, new CoursePoint(0, 0), Straight(With(Twist.Pinball), wall));
            Assert.Less(pinball.Rest.D, thud.Rest.D - 5, "it comes back much farther");

            var fade = new CourseShot(GolfClub.Iron, Full(1, 6), 0, new CoursePoint(0, 0), Straight());
            var banana = new CourseShot(GolfClub.Iron, Full(1, 6), 0, new CoursePoint(0, 0), Straight(With(Twist.BananaBall)));
            Assert.Greater(banana.Landing.X, fade.Landing.X * 1.6, "the same fade bends twice as hard");
        }

        [Test]
        public void WetTurfHoldsTheBall()
        {
            var dry = new CourseShot(GolfClub.Driver, Full(), 0, new CoursePoint(0, 0), Straight());
            var r = new PlayRules(); Weather.Rain.Bend(r);
            var wet = new CourseShot(GolfClub.Driver, Full(), 0, new CoursePoint(0, 0), Straight(r));
            Assert.AreEqual(dry.Carry, wet.Carry, 0.5);
            Assert.Less(wet.Roll, dry.Roll * 0.8, "it sits down");
            Assert.IsTrue(RoundEvents.Deal(1).RulesFor(Weather.Storm).FairwayDrag > 1);
        }

        [Test]
        public void TheWeatherTurnsAndTheWindGusts()
        {
            int turns = 0, gusts = 0, kinds = 0;
            var seen = new System.Collections.Generic.HashSet<Weather>();
            for (int seed = 1; seed <= 300; seed++)
            {
                var e = RoundEvents.Deal(seed);
                for (int h = 0; h < 4; h++)
                {
                    var w = e.WeatherFor(h); seen.Add(w);
                    var wind = e.WindFor(h, new Wind(3, 40), w);
                    if (w == Weather.Storm) Assert.GreaterOrEqual(wind.SpeedMPH, 10, "a storm blows");
                    if (e.Has(Twist.Hurricane)) Assert.GreaterOrEqual(wind.SpeedMPH, 15, "a hurricane blows harder");
                    Assert.IsNull(e.TurnBefore(h, 0, w), "never on the tee");
                    for (int shot = 1; shot < 4; shot++)
                    {
                        if (e.TurnBefore(h, shot, w) != null) turns++;
                        if (e.GustBefore(h, shot, wind, w) is Wind g)
                        {
                            gusts++;
                            Assert.IsTrue(w.IsGusty() || e.Has(Twist.Hurricane));
                            Assert.LessOrEqual(g.SpeedMPH, 28);
                            Assert.GreaterOrEqual(g.DirectionDegrees, -180); Assert.LessOrEqual(g.DirectionDegrees, 180);
                        }
                    }
                }
            }
            kinds = seen.Count;
            Assert.AreEqual(5, kinds, "every kind of weather comes");
            Assert.Greater(turns, 50, "it turns now and then");
            Assert.Greater(gusts, 200, "and gusts in a gusty wind");
        }

        // ----- The things moving about the hole -----

        /// A balloon parked right on the flight's line at the height it passes, where `drift` puts it.
        static (CourseMovers movers, CourseShot clear) BalloonOnTheLine(double atD)
        {
            var clear = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            double t = 0; while (clear.PositionAt(t).d < atD && t < clear.CarryTime) t += 0.01;
            var p = clear.PositionAt(t);
            // it drifts across the line, there and back once a minute: in the middle at 15 s (and 45)
            var balloon = Mover.Balloon(new CoursePoint(-30, atD), new CoursePoint(30, atD), p.h, 60, 0, 0);
            return (new CourseMovers(new[] { balloon }), clear);
        }

        [Test]
        public void TheBallMeetsTheBalloonOnlyWhenItIsThere()
        {
            var (movers, clear) = BalloonOnTheLine(70);
            var hole = Straight(); hole.Movers = movers;
            movers.PhaseAtLaunch = 15 - 1.0;   // across the line as the ball gets there
            var hit = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), hole);
            Assert.IsTrue(hit.Knocks.Any(k => k.Kind == ObstacleKind.Balloon), "boing");
            Assert.Less(hit.Rest.D, clear.Rest.D - 30, "and it came back off it");
            movers.PhaseAtLaunch = 0;   // drifted out to the side
            var missed = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), hole);
            Assert.AreEqual(clear.Rest.D, missed.Rest.D, 1e-6, "the sky was clear when it went through");
            Assert.AreEqual(0, missed.Knocks.Count);
        }

        [Test]
        public void AGullTakesThePaceOutOfIt()
        {
            var clear = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            double t = 0; while (clear.PositionAt(t).d < 90 && t < clear.CarryTime) t += 0.01;
            var p = clear.PositionAt(t);
            // a gull circling a point 5 yards right of the line, at the ball's height there, passing over the line at t
            double phase = Math.PI - 0.4 * t;
            var gull = Mover.Gull(new CoursePoint(5, p.d), 5, p.h - 0.7 * Math.Sin(1.7 * t + phase * 3), 0.4, phase, 0);
            var hole = Straight(); hole.Movers = new CourseMovers(new[] { gull });
            var hit = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), hole);
            Assert.IsTrue(hit.Knocks.Any(k => k.Kind == ObstacleKind.Gull && !k.Hard), "a birdie");
            Assert.Less(hit.Rest.D, 120, "it drops short");
        }

        [Test]
        public void ACartBumpsARollingBall()
        {
            // a cart going round a loop across the line of a putt-length roll
            var cart = Mover.CartLoop(new CoursePoint(-12, 9), new CoursePoint(12, 9), new CoursePoint(12, 11), new CoursePoint(-12, 11), 4, 0, _ => 0);
            var hole = Straight(); hole.Movers = new CourseMovers(new[] { cart });
            CourseShot met = null;
            for (double phase = 0; phase < 14 && met == null; phase += 0.25)
            {
                hole.Movers.PhaseAtLaunch = phase;
                var s = new CourseShot(GolfClub.Putter, Full(0.9), 0, new CoursePoint(0, 0), hole);
                if (s.Knocks.Any(k => k.Kind == ObstacleKind.Cart)) met = s;
            }
            Assert.IsNotNull(met, "somewhere round its loop it is in the way");
            Assert.IsTrue(double.IsNaN(met.Knocks.First(k => k.Kind == ObstacleKind.Cart).Y), "on the ground");
        }

        [Test]
        public void ATwisterFlingsTheBallAside()
        {
            var clear = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), Straight());
            // parked across the line 100 yards out (it barely moves over the shot)
            var twister = Mover.Twister(new CoursePoint(-0.01, 100), new CoursePoint(0.01, 100), 1000, 0, _ => 0);
            var hole = Straight(); hole.Movers = new CourseMovers(new[] { twister });
            var hit = new CourseShot(GolfClub.Iron, Full(), 0, new CoursePoint(0, 0), hole);
            Assert.IsTrue(hit.Knocks.Any(k => k.Kind == ObstacleKind.Twister && !k.Hard), "caught in it");
            Assert.Greater(Math.Abs(hit.Rest.X), 10, "and flung out to the side");
            Assert.Less(hit.Rest.D, clear.Rest.D - 20, "nowhere near where it was going");
            var calm = RoundEvents.Deal(3);
            RoundEvents.EveryMover = true;
            try
            {
                var movers = calm.MoversFor(0, Straight(), Weather.Storm);
                Assert.IsTrue(movers.All.Any(m => m.Kind == MoverKind.Twister), "storms throw them down");
                Assert.IsFalse(calm.MoversFor(0, Straight(), Weather.Rain).All.Any(m => m.Kind == MoverKind.Twister), "only storms");
            }
            finally { RoundEvents.EveryMover = false; }
        }

        [Test]
        public void TheClockHoldsForAShotAndRunsOnFromIt()
        {
            var m = new CourseMovers(new[] { Mover.Gull(new CoursePoint(0, 50), 10, 15, 0.4, 0, 0) });
            m.Advance(3); Assert.AreEqual(3, m.Now, 1e-9);
            m.PhaseAtLaunch = m.Clock + 0.5;
            m.Hold(2); Assert.AreEqual(5.5, m.Now, 1e-9);
            m.Advance(10); Assert.AreEqual(5.5, m.Now, 1e-9, "held to the flight");
            m.Hold(null); Assert.AreEqual(5.5, m.Clock, 1e-9, "and on from where the flight left it");
        }

        [Test]
        public void MoversArePlacedWhereTheyFit()
        {
            int balloons = 0, carts = 0, gulls = 0;
            foreach (var course in Course.Course.All())
                foreach (var hole in course.Holes)
                    for (int seed = 1; seed <= 20; seed++)
                    {
                        var e = RoundEvents.Deal(seed);
                        var movers = e.MoversFor(0, hole, Weather.Clear);
                        if (movers == null) continue;
                        foreach (var m in movers.All)
                        {
                            if (m.Kind == MoverKind.Balloon) { balloons++; Assert.Greater(m.At(0).y, 15, "a balloon flies"); }
                            if (m.Kind == MoverKind.Gull) gulls++;
                            if (m.Kind != MoverKind.Cart) continue;
                            carts++;
                            Assert.GreaterOrEqual(hole.Par, 4, "carts cross par 4s and 5s");
                            foreach (var (x, _, d) in m.LoopPoints())
                                Assert.AreNotEqual(CourseLie.Water, hole.LieAt(new CoursePoint(x, d)), $"hole {hole.Number}: never in the water");
                        }
                    }
            Assert.Greater(balloons, 0); Assert.Greater(carts, 0); Assert.Greater(gulls, 0);
        }
    }
}
