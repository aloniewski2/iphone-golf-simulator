// Gate tests for Course.Postcards() (Unity/Assets/Scripts/Course/Hole.cs), the brief in
// ArtDir/environments/13_POSTCARD_HOLES.txt asserted against the numbers the shot code really uses.
// Plain C# 9 + the NUnit subset used by CliffsideHoleTests.cs (Test, TestCase, Assert.AreEqual/IsTrue/IsFalse/Less/Greater/
// GreaterOrEqual/LessOrEqual, Assert.That with Is.EqualTo(..).Within(..)); no UnityEngine, so it also runs without Unity:
//     Tools/PostcardCheck/run.sh tests Unity/Assets/Tests/EditMode/PostcardHoleTests.cs
using System;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The three postcard holes (8 Needle, 9 Split, 10 Crater) as Hole.cs scores them.
    public class PostcardHoleTests
    {
        // number, name, par, centerline length range (yd), longest allowed carry over water (yd), water ellipses, bunkers
        static readonly (int number, string name, int par, double minLength, double maxLength, double carryMax, int water, int bunker)[] Brief =
        {
            (8, "Needle", 4, 320, 360, 140, 2, 1),
            (9, "Split", 5, 460, 500, 140, 1, 3),
            (10, "Crater", 4, 340, 380, 150, 1, 2),
        };

        /// Brief: at least this much dry fairway on the centerline short of every carry.
        const double LayUpYards = 25;
        const double SampleStep = 0.25;

        static Hole[] Holes => Course.Course.Postcards().Holes;
        static Hole HoleNumber(int number) => Holes.Single(h => h.Number == number);

        // ------------------------------------------------------------------ course shape

        [Test]
        public void PostcardsAreHolesEightNineAndTenWithTheBriefPars()
        {
            var course = Course.Course.Postcards();
            Assert.AreEqual("Postcards", course.Name);
            var holes = course.Holes;
            Assert.AreEqual(3, holes.Length);
            Assert.AreEqual(8, holes[0].Number);
            Assert.AreEqual(9, holes[1].Number);
            Assert.AreEqual(10, holes[2].Number);
            Assert.AreEqual(4, holes[0].Par, "Needle is a par 4");
            Assert.AreEqual(5, holes[1].Par, "Split is a par 5");
            Assert.AreEqual(4, holes[2].Par, "Crater is a par 4");
            Assert.AreEqual(13, course.Par);
        }

        [Test]
        public void CliffsideIsStillHoleSevenAndTheNumbersDoNotClash()
        {
            var cliffside = Course.Course.Cliffside();
            Assert.AreEqual(5, cliffside.Holes.Length);
            var seven = cliffside.Holes[0];
            Assert.AreEqual(7, seven.Number);
            Assert.AreEqual(4, seven.Par);
            Assert.AreEqual(6, seven.Hazards.Length, "hole 7 keeps its six bunkers");
            var numbers = string.Join(",", Course.Course.All().SelectMany(c => c.Holes).Select(h => h.Number));
            Assert.AreEqual("7,8,9,12,15,13,14,17,18,19,20,10,16,21,22,23", numbers, "the three courses deal the sixteen modelled holes out with distinct numbers");
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void EachHoleHasOneShoreOneCenterlineAndOnePin(int number)
        {
            var h = HoleNumber(number);
            Assert.IsTrue(h.Shore != null, "one shore polygon");
            Assert.GreaterOrEqual(h.Shore.Length, 40, "a dense shore, not a box");
            Assert.AreEqual(0, SelfIntersections(h.Shore), "the shore is one simple polygon");
            Assert.GreaterOrEqual(h.Centerline.Length, 3, "tee, at least one landing station, pin");
            Assert.AreEqual(0.0, h.Tee.X, 1e-9, "the tee is the origin");
            Assert.AreEqual(0.0, h.Tee.D, 1e-9, "the tee is the origin");
            Assert.AreEqual(CourseLie.Tee, h.LieAt(h.Tee));
            Assert.AreEqual(CourseLie.Green, h.LieAt(h.Pin));
            Assert.Greater(h.Tee.DistanceTo(h.Pin), 250, "the pin is a full hole away from the tee (Crater bends sideways, so not +D only)");
            Assert.IsTrue(h.OnLand(h.Tee) && h.OnLand(h.Pin), "tee and pin are on land");
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void FairwayRoughAndGreenNumbersAreSane(int number)
        {
            var h = HoleNumber(number);
            Assert.GreaterOrEqual(h.FairwayWidth, 12, "fairway width, yards");
            Assert.LessOrEqual(h.FairwayWidth, 24, "fairway width, yards");
            Assert.GreaterOrEqual(h.GreenRadius, 10, "green radius, yards");
            Assert.LessOrEqual(h.GreenRadius, 20, "green radius, yards");
            Assert.Greater(h.RoughWidth, 0, "a rough band exists");
            Assert.Less(h.RoughWidth, 200, "rough width, yards");
            if (number == 8)
            {
                Assert.That(h.FairwayWidth, Is.EqualTo(16).Within(1), "Needle fairway is about 16 yd");
                Assert.That(h.GreenRadius, Is.EqualTo(14).Within(1), "Needle green radius is about 14 yd");
                Assert.That(h.RoughWidth, Is.EqualTo(8).Within(1), "Needle rough band is about 8 yd, then the shore");
            }
            if (number == 9) Assert.That(h.FairwayWidth, Is.EqualTo(18).Within(1), "Split ribbon fairway is about 18 yd");
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void CenterlineLengthMatchesTheBrief(int number)
        {
            var spec = Brief.Single(b => b.number == number);
            var h = HoleNumber(number);
            Assert.GreaterOrEqual(h.Length, spec.minLength, spec.name + " centerline length, yards");
            Assert.LessOrEqual(h.Length, spec.maxLength, spec.name + " centerline length, yards");
            Assert.AreEqual(spec.par, h.Par, spec.name + " par");
        }

        // ------------------------------------------------------------------ hazards

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void HazardCountsMatchTheBrief(int number)
        {
            var spec = Brief.Single(b => b.number == number);
            var h = HoleNumber(number);
            Assert.AreEqual(spec.water, h.Hazards.Count(z => z.Kind == HazardKind.Water), spec.name + " water ellipses");
            Assert.AreEqual(spec.bunker, h.Hazards.Count(z => z.Kind == HazardKind.Bunker), spec.name + " bunker ellipses");
            Assert.AreEqual(spec.water + spec.bunker, h.Hazards.Length, spec.name + " has no other hazards");
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void EveryBunkerIsOnLand(int number)
        {
            var h = HoleNumber(number);
            foreach (var z in h.Hazards.Where(z => z.Kind == HazardKind.Bunker))
            {
                var centre = new CoursePoint(z.X, z.Distance);
                Assert.IsTrue(h.OnLand(centre), $"bunker at {centre} is on land");
                Assert.AreEqual(CourseLie.Bunker, h.LieAt(centre), $"bunker at {centre} scores as a bunker");
                for (int k = 0; k < 24; k++)
                {
                    double a = 2 * Math.PI * k / 24;
                    var rim = new CoursePoint(z.X + z.Width / 2 * Math.Cos(a), z.Distance + z.Length / 2 * Math.Sin(a));
                    Assert.IsTrue(h.OnLand(rim), $"the rim of the bunker at {centre} stays inside the shore ({rim})");
                }
            }
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void NoHazardCoversTheTeeOrTheInnerGreen(int number)
        {
            var h = HoleNumber(number);
            Assert.AreEqual(CourseLie.Green, h.LieAt(h.Pin), "the cup is on the green");
            for (int k = 0; k < 16; k++)
            {
                double a = 2 * Math.PI * k / 16;
                var inner = new CoursePoint(h.Pin.X + 0.6 * h.GreenRadius * Math.Cos(a), h.Pin.D + 0.6 * h.GreenRadius * Math.Sin(a));
                Assert.AreEqual(CourseLie.Green, h.LieAt(inner), $"the inner green at {inner} is green, not a hazard");
                var teeRing = new CoursePoint(h.Tee.X + 8 * Math.Cos(a), h.Tee.D + 8 * Math.Sin(a));
                var lie = h.LieAt(teeRing);
                Assert.IsTrue(lie == CourseLie.Fairway || lie == CourseLie.Rough || lie == CourseLie.Tee, $"8 yd from the tee at {teeRing} is {lie}");
            }
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void WaterCentresAndEverythingOutsideTheShoreAreWater(int number)
        {
            var h = HoleNumber(number);
            foreach (var z in h.Hazards.Where(z => z.Kind == HazardKind.Water))
                Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(z.X, z.Distance)), $"the water ellipse at ({z.X}, {z.Distance})");
            double minX = h.Shore.Min(p => p.X), maxX = h.Shore.Max(p => p.X), minD = h.Shore.Min(p => p.D), maxD = h.Shore.Max(p => p.D);
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(minX - 300, h.Tee.D)), "far left");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(maxX + 300, h.Pin.D)), "far right");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(h.Tee.X, minD - 300)), "far behind the tee");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(h.Pin.X, maxD + 300)), "far past the green");
        }

        /// Brief: "rough about 8, then the shore, so a big miss is water" (Needle) and the whole left ridge is rough (Split).
        /// Dry land that is out-of-bounds grass would be a +1 stroke replay with no water to blame.
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void NoDryLandIsOutOfBounds(int number)
        {
            var h = HoleNumber(number);
            int dry = 0, outOfBounds = 0;
            DryCells(h, 1.0, (p, lie) => { dry++; if (lie == CourseLie.OutOfBounds) outOfBounds++; });
            Assert.Greater(dry, 3000, "the sweep found the island");
            Assert.AreEqual(0, outOfBounds, $"{outOfBounds} of {dry} dry 1 yd cells are out of bounds");
        }

        // ------------------------------------------------------------------ Split: one ribbon fairway, one ridge in the rough

        [Test]
        public void SplitHasOneFairwayRibbonAndTheLeftRidgeIsRough()
        {
            var h = HoleNumber(9);
            int ridgeCells = 0, fairwayCells = 0;
            DryCells(h, 2.0, (p, lie) =>
            {
                if (lie == CourseLie.Fairway)
                {
                    fairwayCells++;
                    Assert.Greater(p.X, -25, $"fairway at {p}: there is no second fairway on the left ridge");
                }
                if (p.X < -40 && lie != CourseLie.Bunker) { ridgeCells++; Assert.AreEqual(CourseLie.Rough, lie, $"the left ridge at {p} plays as rough"); }
            });
            Assert.Greater(ridgeCells, 1000, "the left ridge exists");
            Assert.Greater(fairwayCells, 500, "the right ribbon is the fairway");
            foreach (var spot in new[] { new CoursePoint(-60, 100), new CoursePoint(-60, 200), new CoursePoint(-55, 250), new CoursePoint(-60, 350) })
                Assert.AreEqual(CourseLie.Rough, h.LieAt(spot), $"ridge spot {spot}");
        }

        // ------------------------------------------------------------------ the line the aim assist walks

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void EveryCenterlineStationIsDry(int number)
        {
            var h = HoleNumber(number);
            for (int i = 0; i < h.Centerline.Length; i++)
            {
                var lie = h.LieAt(h.Centerline[i]);
                bool dry = i == 0 ? lie == CourseLie.Tee : i == h.Centerline.Length - 1 ? lie == CourseLie.Green : lie == CourseLie.Fairway;
                Assert.IsTrue(dry, $"station {i} {h.Centerline[i]} is {lie}; RecommendedTarget aims at the stations");
            }
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void AimAssistWalksTheStationsToThePinWithoutTouchingWater(int number)
        {
            var h = HoleNumber(number);
            var ball = h.Tee;
            for (int step = 0; step < 12; step++)
            {
                var target = h.RecommendedTarget(ball);
                var lie = h.LieAt(target);
                Assert.IsTrue(lie == CourseLie.Fairway || lie == CourseLie.Green, $"aim assist from {ball} targets {target}, which is {lie}");
                if (target.DistanceTo(h.Pin) < 1e-9) return;
                ball = target;
            }
            Assert.Fail("aim assist never reached the pin");
        }

        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void NoForcedCarryOverTheLimitAndDryFairwayShortOfEach(int number)
        {
            var spec = Brief.Single(b => b.number == number);
            var h = HoleNumber(number);
            var samples = CenterlineSamples(h, SampleStep);
            var runs = new List<double[]>();   // {arc length of the first water sample, of the last}
            double[] open = null;
            foreach (var (s, p) in samples)
            {
                bool water = h.LieAt(p) == CourseLie.Water;
                if (water && open == null) open = new[] { s, s };
                else if (water) open[1] = s;
                else if (open != null) { runs.Add(open); open = null; }
            }
            if (open != null) runs.Add(open);

            Assert.AreEqual(spec.water, runs.Count, $"{spec.name}: every water ellipse is crossed once by the centerline");
            double previousEnd = 0;
            for (int i = 0; i < runs.Count; i++)
            {
                double carry = runs[i][1] - runs[i][0] + SampleStep;
                Assert.LessOrEqual(carry, spec.carryMax, $"{spec.name} carry {i + 1} is {carry:F1} yd");
                Assert.GreaterOrEqual(runs[i][0] - previousEnd, LayUpYards, $"{spec.name}: dry fairway short of carry {i + 1}");
                foreach (var (s, p) in samples.Where(t => t.s >= runs[i][0] - LayUpYards && t.s < runs[i][0]))
                {
                    var lie = h.LieAt(p);
                    Assert.IsTrue(lie == CourseLie.Fairway || lie == CourseLie.Tee, $"{spec.name}: {runs[i][0] - s:F1} yd short of carry {i + 1} the centerline is {lie}");
                }
                foreach (var (s, p) in samples.Where(t => t.s > runs[i][1] && t.s <= runs[i][1] + LayUpYards))
                {
                    var lie = h.LieAt(p);
                    Assert.IsTrue(lie == CourseLie.Fairway || lie == CourseLie.Fringe || lie == CourseLie.Green, $"{spec.name}: {s - runs[i][1]:F1} yd past carry {i + 1} the centerline is {lie}");
                }
                previousEnd = runs[i][1];
            }
        }

        // ------------------------------------------------------------------ rules the brief says not to change

        [Test]
        public void LiePowerFactorsAndPenaltiesAreUnchanged()
        {
            Assert.AreEqual(0.85, CourseLie.Rough.PowerFactor(), 1e-9, "rough is 0.85 power");
            Assert.AreEqual(0.6, CourseLie.Bunker.PowerFactor(), 1e-9, "bunker is 0.6 power");
            foreach (var lie in new[] { CourseLie.Tee, CourseLie.Fairway, CourseLie.Green, CourseLie.Water, CourseLie.OutOfBounds })
                Assert.AreEqual(1.0, lie.PowerFactor(), 1e-9, lie + " costs no club speed");
            Assert.AreEqual(1, CourseLie.Water.PenaltyStrokes(), "water is +1 stroke");
            Assert.AreEqual(1, CourseLie.OutOfBounds.PenaltyStrokes(), "out of bounds is +1 stroke");
            foreach (var lie in new[] { CourseLie.Tee, CourseLie.Fairway, CourseLie.Rough, CourseLie.Bunker, CourseLie.Green })
                Assert.AreEqual(0, lie.PenaltyStrokes(), lie + " is not penalised");
        }

        // ------------------------------------------------------------------ helpers

        /// Calls `visit(point, lie)` for every grid cell centre that is inside the shore and not under a water ellipse.
        static void DryCells(Hole h, double step, Action<CoursePoint, CourseLie> visit)
        {
            double minX = h.Shore.Min(p => p.X), maxX = h.Shore.Max(p => p.X), minD = h.Shore.Min(p => p.D), maxD = h.Shore.Max(p => p.D);
            for (double d = minD; d <= maxD; d += step)
                for (double x = minX; x <= maxX; x += step)
                {
                    var p = new CoursePoint(x, d);
                    var lie = h.LieAt(p);
                    if (lie != CourseLie.Water) visit(p, lie);
                }
        }

        /// (arc length, point) every `step` yards along the centerline, ending on the pin.
        static List<(double s, CoursePoint p)> CenterlineSamples(Hole h, double step)
        {
            var list = new List<(double, CoursePoint)>();
            double acc = 0;
            for (int i = 1; i < h.Centerline.Length; i++)
            {
                var a = h.Centerline[i - 1]; var b = h.Centerline[i];
                double len = a.DistanceTo(b);
                int n = Math.Max(1, (int)Math.Ceiling(len / step));
                for (int k = 0; k < n; k++)
                {
                    double t = (double)k / n;
                    list.Add((acc + len * t, new CoursePoint(a.X + (b.X - a.X) * t, a.D + (b.D - a.D) * t)));
                }
                acc += len;
            }
            list.Add((acc, h.Pin));
            return list;
        }

        static double Cross(CoursePoint p, CoursePoint q, CoursePoint r) => (q.X - p.X) * (r.D - p.D) - (q.D - p.D) * (r.X - p.X);

        /// Number of pairs of non-adjacent shore edges that properly cross.
        static int SelfIntersections(CoursePoint[] poly)
        {
            int hits = 0, n = poly.Length;
            for (int i = 0; i < n; i++)
                for (int j = i + 2; j < n; j++)
                {
                    if ((j + 1) % n == i) continue;
                    var a = poly[i]; var b = poly[(i + 1) % n]; var c = poly[j]; var d = poly[(j + 1) % n];
                    if (Cross(a, b, c) * Cross(a, b, d) < 0 && Cross(c, d, a) * Cross(c, d, b) < 0) hits++;
                }
            return hits;
        }
    }
}
