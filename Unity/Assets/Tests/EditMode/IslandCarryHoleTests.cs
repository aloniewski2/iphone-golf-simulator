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
        public void TheRoundPlaysBothHoles()
        {
            var course = Course.Course.Cliffside();
            Assert.AreEqual(2, course.Holes.Length);
            Assert.AreEqual(7, course.Par);
        }
    }
}
