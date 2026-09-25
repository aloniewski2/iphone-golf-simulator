using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// Hole 12 as the flight model sees it: the numbers in Course.Cliffside must agree with the
    /// Blender meshes they were traced from, so the carry over the water is really water and
    /// both islands are really grass.
    public class IslandCarryHoleTests
    {
        static Hole Twelve => Course.Course.Cliffside().Holes[1];

        [Test]
        public void IsTheParThreeIslandCarry()
        {
            Assert.AreEqual(12, Twelve.Number);
            Assert.AreEqual(3, Twelve.Par);
            Assert.That(Twelve.Length, Is.EqualTo(198).Within(3), "181 m white tee to cup");
            Assert.AreEqual(Twelve.Pin, Twelve.RecommendedTarget(Twelve.Tee), "a par 3 aims at the pin from the tee");
        }

        [Test]
        public void AShotThatComesDownInTheWaterStaysThere()
        {
            var h = Twelve;
            // a half iron off the tee comes down in the carry, well short of the green island
            var shot = new CourseShot(GolfArcade.Shot.GolfClub.Iron, new GolfArcade.Swing.SwingImpact { Power = 0.55 }, 0, h.Tee, h);
            Assert.AreEqual(CourseLie.Water, h.LieAt(shot.Landing), $"it should land in the sea, at {shot.Landing}");
            Assert.AreEqual(CourseLie.Water, shot.Lie);
            Assert.AreEqual(shot.Landing, shot.Rest, "no skipping out of the sea on a bounce");
            Assert.AreEqual(shot.LandingTime, shot.Duration, 1e-9);
            Assert.Greater(shot.LandingTime, 1.0, "it flew first");
            Assert.That(shot.Landing.DistanceTo(h.Tee), Is.EqualTo(shot.Carry).Within(0.5), "the landing is the end of the carry");
            Assert.AreNotEqual(CourseLie.Water, h.LieAt(shot.NextPosition), "the drop is on dry land");
            // and one that carries the water lands first, then bounces and rolls on from there
            var full = new CourseShot(GolfArcade.Shot.GolfClub.Iron, new GolfArcade.Swing.SwingImpact { Power = 1 }, 0, h.Tee, h);
            Assert.AreNotEqual(CourseLie.Water, full.Lie);
            Assert.Less(full.LandingTime, full.CarryTime, "the bounces come after the first touchdown");
            Assert.Greater(full.Landing.DistanceTo(h.Tee), 140);
        }

        [Test]
        public void LiesFollowBothIslands()
        {
            var h = Twelve;
            Assert.AreEqual(CourseLie.Tee, h.LieAt(h.Tee));
            Assert.AreEqual(CourseLie.Rough, h.LieAt(new CoursePoint(-30, 5)), "the tee island off the deck");
            Assert.AreEqual(CourseLie.Rough, h.LieAt(new CoursePoint(72, 80)), "the eastern promontory");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(0, 75)), "the carry between the islands");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(0, -60)), "behind the tee");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(-70, 190)), "west of the green island");
            Assert.AreEqual(CourseLie.Fairway, h.LieAt(new CoursePoint(8, 160)), "the safe landing area short of the green");
            Assert.AreEqual(CourseLie.Bunker, h.LieAt(new CoursePoint(-24.8, 166.4)), "the front-left bunker");
            Assert.AreEqual(CourseLie.Bunker, h.LieAt(new CoursePoint(28.8, 164.3)), "the front-right bunker");
            Assert.AreEqual(CourseLie.Green, h.LieAt(h.Pin));
            Assert.AreEqual(CourseLie.Green, h.LieAt(new CoursePoint(0, 180)));
            Assert.AreEqual(CourseLie.Rough, h.LieAt(new CoursePoint(-12, 225)), "behind the green, under the lighthouse trees");
        }

        [Test]
        public void EveryFeatureIsOnDryLand()
        {
            var h = Twelve;
            foreach (var p in h.Centerline) Assert.IsTrue(h.OnLand(p), $"{p} is on land");
            foreach (var b in h.Hazards) Assert.IsTrue(h.OnLand(new CoursePoint(b.X, b.Distance)), $"bunker at {b.X},{b.Distance} is on land");
            Assert.AreEqual(1, h.Islets.Length, "the green has its own island");
        }

        [Test]
        public void TheRoundPlaysAllTenHoles()
        {
            var course = Course.Course.Cliffside();
            CollectionAssert.AreEqual(new[] { 7, 12, 13, 14, 15, 16, 17, 18, 19, 20 }, System.Array.ConvertAll(course.Holes, h => h.Number));
            Assert.AreEqual(4 + 3 + 5 + 4 + 3 + 4 + 5 + 3 + 4 + 4, course.Par);
        }

        /// The new holes' water — lava, a frozen lake, a lagoon, canals — is water to the ball:
        /// a penalty, and a drop.
        [Test]
        public void TheNewHolesWaterIsWater()
        {
            var holes = Course.Course.Cliffside().Holes;
            Hole H(int n) => System.Array.Find(holes, h => h.Number == n);
            var volcano = H(16);
            Assert.AreEqual(CourseLie.Water, volcano.LieAt(new CoursePoint(0, 205.6)), "the lava river across the fairway");
            Assert.AreEqual(CourseLie.Fairway, volcano.LieAt(new CoursePoint(0, 250)), "past it, the fairway");
            Assert.AreEqual(CourseLie.Water, H(17).LieAt(new CoursePoint(109, 263)), "the frozen lake");
            Assert.AreEqual(CourseLie.Water, H(19).LieAt(new CoursePoint(-6.6, 166.2)), "the lagoon");
            Assert.AreEqual(CourseLie.Water, H(20).LieAt(new CoursePoint(0, 156.4)), "the first canal");
            Assert.AreNotEqual(CourseLie.Water, H(18).LieAt(H(18).Pin), "the green's mesa is land");
            Assert.AreEqual(CourseLie.Water, H(18).LieAt(new CoursePoint(0, 80)), "the canyon between the mesas");
        }
    }
}
