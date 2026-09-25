using System;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The ground under the roll: contours have the gradient they claim, a sampled grid agrees
    /// with what it sampled, and a putt does what the read says — breaks with the slope, short
    /// uphill, long downhill.
    public class SurfaceTests
    {
        static Contours Tilted => new() { TiltX = 0.02, TiltD = -0.01, Features = { Contours.Mound(3, 8, 8, 0.3), Contours.Ridge(-4, 2, 4, 6, 6, -0.15) } };

        static Hole FlatGreen(ISurface surface) => new()
        {
            Number = 1, Par = 3, Centerline = new[] { new CoursePoint(0, -100), new CoursePoint(0, 0) },
            FairwayWidth = 40, GreenRadius = 30, Surface = surface,
        };

        static SwingImpact Impact(double power) => new() { Power = power, StartLineDegrees = 0, CurveDegrees = 0 };
        static double MeterFor(double yards) => Math.Pow(yards / GolfClub.Putter.ReferenceDistanceYards(), 1 / GolfClub.Putter.MeterExponent());

        [Test]
        public void ContourGradientIsTheDerivativeOfItsHeight()
        {
            var c = Tilted;
            var rng = new Random(3);
            for (int n = 0; n < 200; n++)
            {
                var p = new CoursePoint(rng.NextDouble() * 24 - 12, rng.NextDouble() * 24 - 12);
                const double h = 1e-4;
                double dx = (c.Height(new CoursePoint(p.X + h, p.D)) - c.Height(new CoursePoint(p.X - h, p.D))) / (2 * h);
                double dd = (c.Height(new CoursePoint(p.X, p.D + h)) - c.Height(new CoursePoint(p.X, p.D - h))) / (2 * h);
                var g = c.Gradient(p);
                Assert.AreEqual(dx, g.dx, 1e-5, $"dx at {p}");
                Assert.AreEqual(dd, g.dd, 1e-5, $"dd at {p}");
            }
            Assert.AreEqual(0, new Contours().Slope(new CoursePoint(5, 5)), 1e-12, "no contours, no slope");
        }

        [Test]
        public void SampledGridReadsLikeTheGroundItSampled()
        {
            var c = Tilted;
            var grid = HeightGrid.Sample(-15, -15, 30, 30, 0.5, c.Height);
            var rng = new Random(5);
            for (int n = 0; n < 200; n++)
            {
                var p = new CoursePoint(rng.NextDouble() * 20 - 10, rng.NextDouble() * 20 - 10);
                Assert.AreEqual(c.Height(p), grid.Height(p), 0.01, $"height at {p}");
                var a = c.Gradient(p); var b = grid.Gradient(p);
                Assert.AreEqual(a.dx, b.dx, 0.006, $"dx at {p}");
                Assert.AreEqual(a.dd, b.dd, 0.006, $"dd at {p}");
            }
            Assert.IsTrue(grid.Covers(new CoursePoint(0, 0)));
            Assert.IsFalse(grid.Covers(new CoursePoint(40, 0)));
            Assert.AreEqual((0.0, 0.0), grid.Gradient(new CoursePoint(40, 0)), "off the grid the ground is flat");
        }

        [Test]
        public void PuttBreaksDownASideSlope()
        {
            var flat = FlatGreen(FlatSurface.Instance);
            var tilted = FlatGreen(new Contours { TiltX = -0.02 }); // rises to the left: a 2% fall to the right
            var from = new CoursePoint(5, -12); // wide of the cup so nothing drops
            var straight = new CourseShot(GolfClub.Putter, Impact(MeterFor(12)), 0, from, flat);
            var broken = new CourseShot(GolfClub.Putter, Impact(MeterFor(12)), 0, from, tilted);
            Assert.AreEqual(from.X, straight.Rest.X, 0.05, "on the level a straight putt stays on its line");
            Assert.Greater(broken.Rest.X - from.X, 0.5, "on the tilt it breaks to the right");
            Assert.AreEqual(straight.Rest.D, broken.Rest.D, 1.0, "a pure side slope hardly changes the length");
        }

        [Test]
        public void UphillComesUpShortAndDownhillRunsOn()
        {
            var flat = FlatGreen(FlatSurface.Instance);
            var uphill = FlatGreen(new Contours { TiltD = 0.03 });
            var downhill = FlatGreen(new Contours { TiltD = -0.03 });
            var from = new CoursePoint(4, -15);
            double Roll(Hole h) => new CourseShot(GolfClub.Putter, Impact(MeterFor(15)), 0, from, h).Total;
            Assert.AreEqual(15, Roll(flat), 1.0);
            Assert.Less(Roll(uphill), Roll(flat) - 2);
            Assert.Greater(Roll(downhill), Roll(flat) + 2);
        }

        [Test]
        public void ABallAtRestOnASteepFaceKeepsRolling()
        {
            var steep = FlatGreen(new Contours { TiltD = -0.2 }); // falls away down the hole, far steeper than green friction holds
            var shot = new CourseShot(GolfClub.Putter, Impact(MeterFor(2)), 0, new CoursePoint(4, -20), steep);
            Assert.Greater(shot.Total, 10, "it runs away down the slope");
        }
    }
}
