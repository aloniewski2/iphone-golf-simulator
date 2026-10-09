using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class WindTests
    {
        static BallFlight Fly(GolfClub club, double mph, double toward)
        {
            var launch = club.Launch(1, 0, 0);
            launch.WindMPH = mph; launch.WindDegrees = toward;
            return BallFlight.Simulate(launch);
        }

        [Test]
        public void CalmAirIsTheSameFlight()
        {
            var calm = BallFlight.Simulate(GolfClub.Driver.Launch(1, 0, 0));
            var zero = Fly(GolfClub.Driver, 0, 90);
            Assert.AreEqual(calm.Carry, zero.Carry, 1e-9);
            Assert.AreEqual(calm.Landing.LateralYards, zero.Landing.LateralYards, 1e-9);
        }

        [Test]
        public void HeadwindShortensAndTailwindLengthensTheCarry()
        {
            var calm = Fly(GolfClub.Driver, 0, 0);
            var into = Fly(GolfClub.Driver, 15, 180);
            var helping = Fly(GolfClub.Driver, 15, 0);
            Assert.Less(into.Carry, calm.Carry - 12, "15 mph into the face costs real yards");
            Assert.Greater(helping.Carry, calm.Carry + 8, "and helps when it is behind");
            Assert.Greater(into.Apex, calm.Apex, "a headwind balloons the ball");
        }

        [Test]
        public void CrosswindPushesTheBallDownwind()
        {
            var leftToRight = Fly(GolfClub.Iron, 15, 90);
            var rightToLeft = Fly(GolfClub.Iron, 15, -90);
            Assert.Greater(leftToRight.Landing.LateralYards, 5);
            Assert.Less(rightToLeft.Landing.LateralYards, -5);
            Assert.AreEqual(leftToRight.Landing.LateralYards, -rightToLeft.Landing.LateralYards, 0.5);
        }

        [Test]
        public void HigherShotsFeelMoreWind()
        {
            double driverLoss = Fly(GolfClub.Driver, 0, 0).Carry - Fly(GolfClub.Driver, 15, 180).Carry;
            double wedgeLoss = Fly(GolfClub.Wedge, 0, 0).Carry - Fly(GolfClub.Wedge, 15, 180).Carry;
            Assert.Greater(wedgeLoss / GolfClub.Wedge.ReferenceDistanceYards(), driverLoss / GolfClub.Driver.ReferenceDistanceYards() * 1.3,
                "a lofted wedge loses a bigger share of its distance into the wind than a driver");
        }

        [Test]
        public void WindTurnsWithTheAim()
        {
            var wind = new Wind(12, 0); // blowing straight down the hole
            Assert.AreEqual(0, wind.RelativeTo(0), 1e-9);
            Assert.AreEqual(-90, wind.RelativeTo(90), 1e-9, "aiming right, a down-the-hole wind comes over the left shoulder");
            Assert.AreEqual(180, wind.RelativeTo(180), 1e-9);
            Assert.AreEqual(170, new Wind(5, -10).RelativeTo(180), 1e-9, "wraps into (-180, 180]");
            StringAssert.Contains("helping", wind.Describe(10));
            StringAssert.Contains("into", wind.Describe(175));
            StringAssert.Contains("right to left", wind.Describe(90));
            StringAssert.Contains("left to right", wind.Describe(-90));
            Assert.AreEqual("Calm", Wind.Calm.Describe(45));
            Assert.AreEqual("Calm", default(Wind).Describe(0), "the default wind is no wind");
        }

        [Test]
        public void CourseShotsFlyInTheHoleWind()
        {
            var hole = Course.Course.Meadow().Holes[0];
            var impact = new SwingImpact { Power = 1 };
            var calm = new CourseShot(GolfClub.Driver, impact, 0, hole.Tee, hole, 1, Wind.Calm);
            var into = new CourseShot(GolfClub.Driver, impact, 0, hole.Tee, hole, 1, new Wind(15, 180));
            var acrossToTheRight = new CourseShot(GolfClub.Driver, impact, 0, hole.Tee, hole, 1, new Wind(15, 90));
            Assert.Less(into.Carry, calm.Carry - 12);
            Assert.Greater(acrossToTheRight.Rest.X, calm.Rest.X + 5);
            // Aimed 90° right, the same 90° wind is straight behind the ball.
            var aimedRight = new CourseShot(GolfClub.Driver, impact, 90, hole.Tee, hole, 1, new Wind(15, 90));
            var aimedRightCalm = new CourseShot(GolfClub.Driver, impact, 90, hole.Tee, hole, 1, Wind.Calm);
            Assert.Greater(aimedRight.Carry, aimedRightCalm.Carry + 8);
        }

        [Test]
        public void RandomWindStaysInRange()
        {
            var rng = new System.Random(3);
            for (int i = 0; i < 200; i++)
            {
                var w = Wind.Random(rng);
                Assert.That(w.SpeedMPH, Is.InRange(0, Wind.MaxMPH));
                Assert.That(w.DirectionDegrees, Is.InRange(-180, 180));
            }
        }
    }
}
