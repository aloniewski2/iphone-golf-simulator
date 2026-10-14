using System.Linq;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The Wild Isles holes (16–20, blender/scripts/hole16_volcano_design.py … hole20_windmill_design.py):
    /// five holes, each its own world, now dealt out over Wild Isles and the Magma Open. Their water — the lava, a canyon, a
    /// lagoon, canals — is water to the ball: a penalty, and a drop; the frozen lake is ice it
    /// skids across and plays on from.
    public class WildIslesTests
    {
        static Hole Get(int n) => Catalogue.Hole(n);

        [Test]
        public void TheRoundPlaysAllSixHoles()
        {
            var course = Course.Course.WildIsles();
            CollectionAssert.AreEqual(new[] { 13, 14, 17, 18, 19, 20 }, course.Holes.Select(h => h.Number).ToArray());
            Assert.AreEqual(5 + 4 + 5 + 3 + 4 + 4, course.Par);
            Assert.AreEqual("wildisles", course.Key);
            Assert.AreSame(null, Course.Course.WildIsles().Holes.FirstOrDefault(h => !string.IsNullOrEmpty(h.Theme)), "their colours come from the models' own materials");
        }

        [Test]
        public void EveryCourseHasItsOwnHoleNumbers()
        {
            var numbers = Course.Course.All().SelectMany(c => c.Holes).Select(h => h.Number).ToList();
            CollectionAssert.AllItemsAreUnique(numbers);
            Assert.AreEqual("wildisles", Course.Course.Containing(18).Key);
            Assert.AreEqual("cliffside", Course.Course.Containing(12).Key);
            Assert.AreEqual("magma", Course.Course.Containing(16).Key, "the volcano's rim is one of the fire holes");
            Assert.AreSame(null, Course.Course.Containing(99));
        }

        [Test]
        public void EveryHoleIsPlayable()
        {
            foreach (var h in Course.Course.WildIsles().Holes)
            {
                Assert.IsTrue(h.OnLand(h.Tee), $"hole {h.Number}: the tee is on land");
                Assert.IsTrue(h.OnLand(h.Pin), $"hole {h.Number}: the green is on land");
                Assert.AreNotEqual(CourseLie.Water, h.LieAt(h.Pin), $"hole {h.Number}: the pin is dry");
            }
        }

        [Test]
        public void TheirWaterIsWater()
        {
            var volcano = Get(16);
            Assert.AreEqual(CourseLie.Water, volcano.LieAt(new CoursePoint(0, 205.6)), "the lava river across the fairway");
            Assert.AreEqual(CourseLie.Fairway, volcano.LieAt(new CoursePoint(0, 250)), "past it, the fairway");
            Assert.AreEqual(HazardKind.Lava, volcano.HazardAt(new CoursePoint(0, 205.6)), "and it is lava");
            Assert.AreEqual(CourseLie.Ice, Get(17).LieAt(new CoursePoint(106.1, 249.3)), "the frozen lake is ice");
            Assert.AreEqual(0, CourseLie.Ice.PenaltyStrokes(), "and no penalty");
            Assert.AreEqual(CourseLie.Water, Get(18).LieAt(new CoursePoint(0, 80)), "the canyon between the mesas");
            Assert.AreEqual(CourseLie.Water, Get(19).LieAt(new CoursePoint(-6.6, 166.2)), "the lagoon");
            Assert.AreEqual(CourseLie.Water, Get(20).LieAt(new CoursePoint(0, 156.4)), "the first canal");
        }

        static Hole Flat(params CourseHazard[] hazards) => new()
        {
            Number = 99, Par = 4, Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 420) },
            FairwayWidth = 80, GreenRadius = 15, RoughWidth = 80, Hazards = hazards,
        };

        /// The same iron onto grass and onto ice: on the ice it skids on several times as far,
        /// and it is played from where it stops.
        [Test]
        public void AShotSkidsAcrossTheIce()
        {
            var impact = new SwingImpact { Power = 0.85 };
            var grass = new CourseShot(GolfClub.Iron, impact, 0, new CoursePoint(0, 0), Flat(), 1, Wind.Calm);
            var ice = new CourseShot(GolfClub.Iron, impact, 0, new CoursePoint(0, 0), Flat(new CourseHazard(HazardKind.Ice, 0, 300, 120, 500)), 1, Wind.Calm);
            Assert.AreEqual(CourseLie.Fairway, grass.Lie);
            Assert.AreEqual(CourseLie.Ice, ice.Lie, "it stops on the ice");
            Assert.AreEqual(grass.Carry, ice.Carry, 0.01, "the ice changes nothing in the air");
            Assert.Greater(ice.Roll, grass.Roll * 3, $"it skids on: {ice.Roll:F0} yd on the ice, {grass.Roll:F0} on the grass");
            Assert.AreEqual(ice.Rest, ice.NextPosition, "played from where it stops");
            // a fade keeps bending right across the ice
            var fade = new CourseShot(GolfClub.Iron, new SwingImpact { Power = 0.85, CurveDegrees = 8 }, 0, new CoursePoint(0, 0), Flat(new CourseHazard(HazardKind.Ice, 0, 300, 120, 500)), 1, Wind.Calm);
            var fadeGrass = new CourseShot(GolfClub.Iron, new SwingImpact { Power = 0.85, CurveDegrees = 8 }, 0, new CoursePoint(0, 0), Flat(), 1, Wind.Calm);
            Assert.Greater(fade.Rest.X - fade.Touchdown.X, fadeGrass.Rest.X - fadeGrass.Touchdown.X + 1, $"the spin bends it on the ice: {fade.Rest.X - fade.Touchdown.X:F1} yd right on the ice, {fadeGrass.Rest.X - fadeGrass.Touchdown.X:F1} on grass (rolls {fade.Roll:F0}, {fadeGrass.Roll:F0}; lie {fade.Lie})");
        }

        /// Lava is water to the rules — a penalty stroke and a drop back on dry land.
        [Test]
        public void LavaIsAPenalty()
        {
            var hole = Flat(new CourseHazard(HazardKind.Lava, 0, 128, 120, 40));
            var shot = new CourseShot(GolfClub.Iron, new SwingImpact { Power = 0.85 }, 0, new CoursePoint(0, 0), hole, 1, Wind.Calm);
            Assert.AreEqual(CourseLie.Water, shot.Lie, $"in the lava: carry {shot.Carry:F0}, rest {shot.Rest}, landing {shot.Landing}");
            Assert.AreEqual(HazardKind.Lava, hole.HazardAt(shot.Rest));
            Assert.AreEqual(1, shot.PenaltyStrokes);
            Assert.AreNotEqual(CourseLie.Water, hole.LieAt(shot.NextPosition), "the drop is on dry land");
        }

        /// The same iron at a turning blade: with the blade there when it arrives it is knocked
        /// down well short; a quarter turn later the blade has gone by and it flies through.
        [Test]
        public void TheWindmillsSailsKnockItDownOrLetItThrough()
        {
            var impact = new SwingImpact { Power = 0.85 };
            var clear = new CourseShot(GolfClub.Iron, impact, 0, new CoursePoint(0, 0), Flat(), 1, Wind.Calm);
            double tc = 0;
            for (double t = 0; t < clear.LandingTime; t += 0.002) if (clear.PositionAt(t).d >= 60) { tc = t; break; }
            var at = clear.PositionAt(tc);
            Assert.Greater(at.h, 8, "it is well up in the air at the sails");
            // one blade, straight up from a hub 20 yd under the ball's line, turning 36°/s
            SpinningSails Sails(double angleWhenItArrives) => new(at.x, at.h - 20, 60, 0, 0, 1, 1, 0, 0,
                new double[] { -3, 0, 3, 0, 3, 40, -3, 0, 3, 40, -3, 40 })
            { DegreesPerSecond = 36, AngleAtLaunch = angleWhenItArrives - 36 * tc };
            var blocked = Flat(); blocked.Windmill = Sails(0);
            var struck = new CourseShot(GolfClub.Iron, impact, 0, new CoursePoint(0, 0), blocked, 1, Wind.Calm);
            Assert.GreaterOrEqual(struck.Knocks.Count, 1, "it met the blade");
            Assert.Less(struck.Rest.D, clear.Rest.D - 30, $"knocked down short: {struck.Rest.D:F0} yd against {clear.Rest.D:F0}");
            var open = Flat(); open.Windmill = Sails(90);
            var through = new CourseShot(GolfClub.Iron, impact, 0, new CoursePoint(0, 0), open, 1, Wind.Calm);
            Assert.AreEqual(0, through.Knocks.Count, "the blade had turned away");
            Assert.AreEqual(clear.Rest.D, through.Rest.D, 0.01, "so it flies on as if the windmill were not there");
        }
    }
}
