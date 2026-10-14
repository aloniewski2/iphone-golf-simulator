using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The strike's verdict (PERFECT / GREAT / GOOD / THIN, and the shape) and what the lie does
    /// to a shot: rough flyers, the sand wedge in the sand, plugged lies, receptive greens.
    public class StrikeAndLieTests
    {
        static readonly Quaternion ClubDown = Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)(-Math.PI / 2));

        static SwingImpact Swing((double seconds, double angle)[] legs)
        {
            var detector = new MotionSwingDetector();
            double time = 0, angle = 0;
            foreach (var leg in legs)
            {
                int steps = Math.Max(1, (int)(leg.seconds * 100));
                double delta = (leg.angle - angle) / steps;
                for (int i = 0; i < steps; i++)
                {
                    time += 0.01; angle += delta;
                    var attitude = Quaternion.Normalize(Quaternion.CreateFromAxisAngle(Vector3.UnitX, (float)angle) * ClubDown);
                    var e = detector.Ingest(time, attitude, new Vector3((float)(delta / 0.01), 0, 0));
                    if (e?.Kind == SwingEventKind.Impact) return e.Value.Impact;
                }
            }
            Assert.Fail("no impact");
            return default;
        }

        [Test]
        public void ACommittedSmoothSquareSwingIsPerfectAndAPushIsThin()
        {
            var firm = Swing(new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.25, -0.4), (0.6, -0.4) });
            var push = Swing(new[] { (0.6, 0.0), (0.8, 1.8), (0.2, 1.8), (0.5, -0.4), (0.6, -0.4) });
            Assert.AreEqual(0, firm.Thin, 1e-9);
            Assert.Greater(firm.Commit, 1);
            var verdict = Strikes.Judge(firm);
            Assert.AreEqual(StrikeGrade.Perfect, verdict.Grade, $"tempo {verdict.TempoRatio:F1}:1");
            Assert.AreEqual(ShotShape.Straight, verdict.Shape);
            Assert.Greater(push.Thin, 0.25, "4.4 rad/s is pushed through the ball");
            Assert.AreEqual(StrikeGrade.Thin, Strikes.Judge(push).Grade);
        }

        [Test]
        public void EachFaultCostsAGrade()
        {
            var clean = new SwingImpact { Commit = 1.2, TempoSeconds = 1.0, DownswingSeconds = 0.25 };
            Assert.AreEqual(StrikeGrade.Perfect, Strikes.Judge(clean).Grade);
            var drawn = clean; drawn.CurveDegrees = -4;
            Assert.AreEqual(StrikeGrade.Great, Strikes.Judge(drawn).Grade, "a draw is one fault");
            var hooked = clean; hooked.CurveDegrees = -12;
            Assert.AreEqual(StrikeGrade.Good, Strikes.Judge(hooked).Grade, "a hook is two");
            var snatched = clean; snatched.TempoSeconds = 0.4;
            Assert.AreEqual(StrikeGrade.Great, Strikes.Judge(snatched).Grade, "a snatched backswing is one");
            var thin = clean; thin.Thin = 0.8;
            Assert.AreEqual(StrikeGrade.Thin, Strikes.Judge(thin).Grade);
        }

        [Test]
        public void ShapesAreNamedForARightHander()
        {
            Assert.AreEqual(ShotShape.Straight, Strikes.ShapeOf(0.2));
            Assert.AreEqual(ShotShape.Fade, Strikes.ShapeOf(3));
            Assert.AreEqual(ShotShape.Slice, Strikes.ShapeOf(12));
            Assert.AreEqual(ShotShape.Draw, Strikes.ShapeOf(-3));
            Assert.AreEqual(ShotShape.Hook, Strikes.ShapeOf(-12));
        }

        /// A wide-open face is a slice you can see: tens of yards right, not a nudge.
        [Test]
        public void AnOpenFaceSlicesWellOffLine()
        {
            var detector = new MotionSwingDetector();
            double curve = Math.Min(detector.MaxCurveDegrees, (40 - detector.FaceDeadZoneDegrees) * detector.CurvePerFaceDegree);
            var slice = BallFlight.Simulate(GolfClub.Driver.Launch(1, 0, curve));
            Assert.Greater(slice.Landing.LateralYards, 30, "a slice ends up well right");
            Assert.AreEqual(ShotShape.Slice, Strikes.ShapeOf(curve));
        }

        static BallFlight Fly(CourseLie from, GolfClub club, double thin = 0, CourseLie onto = CourseLie.Fairway)
        {
            var l = from.LaunchFrom(club, 1, 0, 0, thin);
            (l.LandingSoftness, l.LandingGrab) = onto.Landing();
            return BallFlight.Simulate(l);
        }

        [Test]
        public void RoughCostsSomeDistanceAndSandMore()
        {
            foreach (var club in new[] { GolfClub.Driver, GolfClub.Iron5, GolfClub.Iron, GolfClub.Iron9, GolfClub.PitchingWedge })
            {
                var fair = Fly(CourseLie.Fairway, club);
                var rough = Fly(CourseLie.Rough, club);
                var sand = Fly(CourseLie.Bunker, club);
                Assert.That(rough.Carry / fair.Carry, Is.InRange(0.87, 0.93), $"{club}: the rough keeps about nine tenths of the carry");
                Assert.That(rough.Total / fair.Total, Is.InRange(0.87, 0.94), $"{club}: and of where it finishes (no flyer running on)");
                Assert.Less(sand.Carry, rough.Carry - 0.04 * fair.Carry, $"{club}: the sand costs more than the rough");
            }
            double iron = Fly(CourseLie.Bunker, GolfClub.Iron).Carry / Fly(CourseLie.Fairway, GolfClub.Iron).Carry;
            Assert.That(iron, Is.InRange(0.72, 0.78), "a 7-iron from the sand keeps about three quarters");
        }

        [Test]
        public void TheSandWedgeIsMadeForTheSand()
        {
            double wedge = Fly(CourseLie.Bunker, GolfClub.Wedge).Carry / Fly(CourseLie.Fairway, GolfClub.Wedge).Carry;
            double iron = Fly(CourseLie.Bunker, GolfClub.Iron).Carry / Fly(CourseLie.Fairway, GolfClub.Iron).Carry;
            Assert.Greater(wedge, iron + 0.08);
        }

        [Test]
        public void RoughAndSandKnockTheShotOffLine()
        {
            var swing = new SwingImpact { Power = 1, StartLineDegrees = 1, SpeedBonus = 0.02 };
            foreach (var lie in new[] { CourseLie.Tee, CourseLie.Fairway, CourseLie.Green, CourseLie.Fringe })
            {
                var same = lie.Scattered(swing, 0, 1);
                Assert.AreEqual(1, same.StartLineDegrees, 1e-12, $"{lie} is a clean lie");
                Assert.AreEqual(0.02, same.SpeedBonus, 1e-12);
            }
            Assert.AreEqual(1, CourseLie.Rough.Scattered(swing, 0.5, 0.5).StartLineDegrees, 1e-12, "the middle draw is the clean strike");
            double roughWorst = 0, sandWorst = 0;
            for (int i = 0; i <= 20; i++)
            {
                double u = i / 20.0;
                roughWorst = Math.Max(roughWorst, Math.Abs(CourseLie.Rough.Scattered(swing, u, u).StartLineDegrees - 1));
                sandWorst = Math.Max(sandWorst, Math.Abs(CourseLie.Bunker.Scattered(swing, u, u).StartLineDegrees - 1));
                Assert.That(CourseLie.Rough.Scattered(swing, u, u).SpeedBonus - 0.02, Is.InRange(-0.04 - 1e-9, 0.04 + 1e-9));
            }
            Assert.That(roughWorst, Is.EqualTo(2.5).Within(1e-9), "the rough: up to 2.5° off line");
            Assert.That(sandWorst, Is.EqualTo(5).Within(1e-9), "the sand: twice that");
            // a 7-iron knocked the most the rough can: a few yards off line, not a different shot
            var hole = Course.Course.Meadow().Holes[0];
            var clean = new CourseShot(GolfClub.Iron, new SwingImpact { Power = 1 }, 0, hole.Tee, hole);
            var off = new CourseShot(GolfClub.Iron, CourseLie.Rough.Scattered(new SwingImpact { Power = 1 }, 1, 0.5), 0, hole.Tee, hole);
            Assert.That(off.Landing.X - clean.Landing.X, Is.InRange(4, 9), "2.5° at 150 yd is about 6 yd");
        }

        [Test]
        public void TheGroundItLandsOnDecidesTheRun()
        {
            var fairway = Fly(CourseLie.Fairway, GolfClub.Driver);
            var green = Fly(CourseLie.Fairway, GolfClub.Driver, onto: CourseLie.Green);
            var rough = Fly(CourseLie.Fairway, GolfClub.Driver, onto: CourseLie.Rough);
            var sand = Fly(CourseLie.Fairway, GolfClub.Driver, onto: CourseLie.Bunker);
            Assert.AreEqual(fairway.Carry, sand.Carry, 1e-6, "the flight is the same");
            Assert.Less(green.Roll, fairway.Roll, "a green is receptive");
            Assert.Less(rough.Roll, fairway.Roll * 0.6, "the rough smothers it");
            Assert.Less(sand.Roll, 1.5, "the sand plugs it");
        }

        [Test]
        public void AThinStrikeFliesLowAndRuns()
        {
            var clean = Fly(CourseLie.Fairway, GolfClub.Iron);
            var thin = Fly(CourseLie.Fairway, GolfClub.Iron, thin: 1);
            Assert.Less(thin.Apex, clean.Apex * 0.7);
            Assert.Greater(thin.Roll, clean.Roll);
        }

        [Test]
        public void TheFringeRingsTheGreenAndIsPuttedFrom()
        {
            var hole = Course.Course.Meadow().Holes[0];
            var collar = new CoursePoint(hole.Pin.X, hole.Pin.D - hole.GreenRadius - 1);
            Assert.AreEqual(CourseLie.Fringe, hole.LieAt(collar));
            Assert.IsTrue(CourseLie.Fringe.IsPuttingSurface());
            var putt = new CourseShot(GolfClub.Putter, new SwingImpact { Power = 0.5 }, 0, collar, hole);
            Assert.Greater(putt.Total, 3, "a putt from the fringe rolls on");
        }
    }
}
