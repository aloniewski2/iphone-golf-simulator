using System;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class ShotModelTests
    {
        [Test]
        public void FullSwingsCarryTheRatedDistance()
        {
            foreach (var club in new[] { GolfClub.Driver, GolfClub.Iron, GolfClub.Wedge })
            {
                var flight = BallFlight.Simulate(club.Launch(1, 0, 0));
                Assert.AreEqual(club.ReferenceDistanceYards(), flight.Carry, 2, club.ToString());
                Assert.Greater(flight.Apex, 8, club.ToString());
            }
        }

        [Test]
        public void MeterReadsDistanceStraight()
        {
            var half = BallFlight.Simulate(GolfClub.Driver.Launch(0.5, 0, 0));
            Assert.AreEqual(125, half.Carry, 3);
            Assert.AreEqual(0, BallFlight.Simulate(GolfClub.Iron.Launch(0, 0, 0)).Total, 0.01);
        }

        [Test]
        public void SpinShapesTheRoll()
        {
            var driver = BallFlight.Simulate(GolfClub.Driver.Launch(1, 0, 0));
            var wedge = BallFlight.Simulate(GolfClub.Wedge.Launch(1, 0, 0));
            Assert.Greater(driver.Roll, 10, "a driver runs out");
            Assert.Less(wedge.Roll, 6, "a wedge checks up");
        }

        [Test]
        public void CurveBendsRightForPositiveTilt()
        {
            var fade = BallFlight.Simulate(GolfClub.Driver.Launch(1, 0, 10));
            var draw = BallFlight.Simulate(GolfClub.Driver.Launch(1, 0, -10));
            Assert.Greater(fade.Landing.LateralYards, 8);
            Assert.Less(draw.Landing.LateralYards, -8);
            Assert.AreEqual(fade.Landing.LateralYards, -draw.Landing.LateralYards, 0.5);
        }

        [Test]
        public void StartLineTurnsTheWholeShot()
        {
            var pushed = BallFlight.Simulate(GolfClub.Iron.Launch(1, 10, 0));
            Assert.AreEqual(Math.Tan(10 * Math.PI / 180) * pushed.Landing.DistanceYards, pushed.Landing.LateralYards, 1.5);
        }

        static Hole Hole1 => Course.Course.Meadow().Holes[0];

        static SwingImpact Impact(double power, double start = 0, double curve = 0) => new() { Power = power, StartLineDegrees = start, CurveDegrees = curve };

        [Test]
        public void TeeShotDownTheMiddleFindsTheFairway()
        {
            // Hole 1 doglegs right at 185, so the tee shot that finds the fairway is an iron.
            var hole = Hole1;
            var shot = new CourseShot(GolfClub.Iron, Impact(1), 0, hole.Tee, hole);
            Assert.AreEqual(CourseLie.Fairway, shot.Lie);
            Assert.AreEqual(160, shot.Carry, 2);
            Assert.Greater(shot.Total, 162);
            Assert.AreEqual(shot.Rest.X, shot.NextPosition.X, 1e-9);
            Assert.Greater(shot.Duration, 5);
        }

        [Test]
        public void HeadingRotatesTheShotOntoTheCourse()
        {
            var hole = Hole1;
            var shot = new CourseShot(GolfClub.Iron, Impact(1), 90, hole.Tee, hole);
            Assert.AreEqual(160, shot.Rest.X, 20, "aimed hard right the ball goes right");
            Assert.AreEqual(0, shot.Rest.D, 5);
        }

        [Test]
        public void RoughAndBunkerCostPowerAndStopTheBall()
        {
            var hole = Hole1;
            var fair = new CourseShot(GolfClub.Iron, Impact(1), 0, hole.Tee, hole, 1);
            var buried = new CourseShot(GolfClub.Iron, Impact(1), 0, hole.Tee, hole, CourseLie.Bunker.PowerFactor());
            Assert.Less(buried.Total, fair.Total * 0.75);
        }

        [Test]
        public void WildHookIsOutOfBoundsAndReplayed()
        {
            // The dogleg forgives a slice; a hook goes through the trees on the left.
            var hole = Hole1;
            var shot = new CourseShot(GolfClub.Driver, Impact(1, -12, -15), 0, hole.Tee, hole);
            Assert.AreEqual(CourseLie.OutOfBounds, shot.Lie);
            Assert.AreEqual(1, shot.PenaltyStrokes);
            Assert.AreEqual(hole.Tee.D, shot.NextPosition.D, 1e-9);
        }

        [Test]
        public void PuttRollsItsRatedDistanceOnTheGreen()
        {
            var hole = Hole1;
            var from = new CoursePoint(hole.Pin.X, hole.Pin.D - 15);
            var shot = new CourseShot(GolfClub.Putter, Impact(1), 25, from, hole); // aimed wide of the cup
            Assert.AreEqual(25, shot.Total, 1.5, "the full stroke rolls its rating, hole aside");
            Assert.AreEqual(CourseLie.Green, shot.Lie);
        }

        [Test]
        public void DyingPuttDropsAndFirmOneLipsOut()
        {
            var hole = Hole1;
            var from = new CoursePoint(hole.Pin.X, hole.Pin.D - 3);
            double meterFor(double yards) => Math.Pow(yards / 25.0, 1 / GolfClub.Putter.MeterExponent());
            var dying = new CourseShot(GolfClub.Putter, Impact(meterFor(3.4)), 0, from, hole);
            Assert.IsTrue(dying.IsHoled, "a putt with a little more than enough drops");
            Assert.AreEqual(0, dying.PenaltyStrokes);
            var firm = new CourseShot(GolfClub.Putter, Impact(meterFor(20)), 0, from, hole);
            Assert.IsFalse(firm.IsHoled, "rammed at the cup it horseshoes out");
            Assert.Greater(firm.Rest.DistanceTo(hole.Pin), 0.5);
        }

        /// Too quick for the hole: through the middle it hops the back lip and runs on along its
        /// line; off the edge the rim swings it round (a horseshoe); a slow one off the edge drops.
        [Test]
        public void TheRimDecidesHowAMissComesOut()
        {
            var pin = new CoursePoint(0, 10);
            double r = Hole.CupCaptureRadius, v = CourseShot.CentreCaptureSpeed * 1.25;
            var middle = CourseShot.CrossTheCup(0, 10 - r, 0, v, pin);
            Assert.IsFalse(middle.Holed);
            Assert.AreEqual(0, middle.Vx, 1e-6, "hops out straight on");
            Assert.Less(middle.Vd, v * 0.8, "and loses pace doing it");
            var edge = CourseShot.CrossTheCup(r * 0.6, 10 - r * 0.8, 0, v, pin);
            Assert.IsFalse(edge.Holed);
            Assert.IsTrue(edge.Lipped);
            double turned = Math.Atan2(edge.Vx, edge.Vd) * 180 / Math.PI;
            Assert.Greater(Math.Abs(turned), 15, "the lip swings it round");
            var dying = CourseShot.CrossTheCup(r * 0.8, 10 - r * 0.6, 0, CourseShot.CentreCaptureSpeed * 0.4, pin);
            Assert.IsTrue(dying.Holed, "a dying ball drops in the side door");
            var skim = CourseShot.CrossTheCup(r * 0.97, 10 - r * 0.3, 0, v * 2, pin);
            Assert.IsFalse(skim.Holed); Assert.IsFalse(skim.Lipped, "a quick one on the very edge skims over");
        }

        /// A cliff in the way is met, not flown through: the ball strikes the face and drops to
        /// its foot; a shelf above the landing catches the ball earlier than the flat would.
        [Test]
        public void TheGroundInTheWayIsMet()
        {
            var hole = Course.Course.Meadow().Holes[0];
            var flat = new CourseShot(GolfClub.Iron, Impact(1), 0, hole.Tee, hole);
            hole.Ground = p => p.D > 90 && p.D < 100 ? 40 : 0;             // a wall 40 yards high
            var walled = new CourseShot(GolfClub.Iron, Impact(1), 0, hole.Tee, hole);
            Assert.Less(walled.Rest.D, 92, "stopped by the wall");
            Assert.Greater(walled.Rest.D, 60, "at its foot, not back at the tee");
            Assert.Less(walled.Carry, flat.Carry * 0.7);
            hole.Ground = p => p.D > 110 ? 12 : p.D > 100 ? (p.D - 100) * 1.2 : 0;   // a ramp up to a shelf
            var shelf = new CourseShot(GolfClub.Iron, Impact(1), 0, hole.Tee, hole);
            Assert.Less(shelf.Landing.D, flat.Landing.D - 5, "it comes down on the shelf, earlier");
            Assert.Greater(shelf.Landing.D, 110);
        }

        [Test]
        public void CupCaptureFollowsTheSpeedAndOffsetRule()
        {
            Assert.IsTrue(CourseShot.CupCaptures(1.0, 0));
            Assert.IsFalse(CourseShot.CupCaptures(2.5, 0));
            Assert.IsFalse(CourseShot.CupCaptures(1.0, Hole.CupCaptureRadius + 0.01));
            Assert.IsTrue(CourseShot.CupCaptures(0.3, Hole.CupCaptureRadius * 0.95));
        }

        [Test]
        public void LiesAreReadFromTheCourse()
        {
            var hole = Hole1;
            Assert.AreEqual(CourseLie.Tee, hole.LieAt(hole.Tee));
            Assert.AreEqual(CourseLie.Fairway, hole.LieAt(new CoursePoint(10, 100)));
            Assert.AreEqual(CourseLie.Rough, hole.LieAt(new CoursePoint(35, 100)));
            Assert.AreEqual(CourseLie.OutOfBounds, hole.LieAt(new CoursePoint(60, 100)));
            Assert.AreEqual(CourseLie.Bunker, hole.LieAt(new CoursePoint(-22, 174)));
            Assert.AreEqual(CourseLie.Green, hole.LieAt(hole.Pin));
        }
    }
}
