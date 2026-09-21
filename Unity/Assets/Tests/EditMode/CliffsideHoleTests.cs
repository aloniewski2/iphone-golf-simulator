using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The Blender island as the flight model sees it: the numbers in Course.Cliffside must agree
    /// with the mesh they were read off, or the ball would splash on grass and rest on the sea.
    public class CliffsideHoleTests
    {
        static Hole Seven => Course.Course.Cliffside().Holes[0];

        [Test]
        public void IsTheParFourFromTheBrief()
        {
            Assert.AreEqual(7, Seven.Number);
            Assert.AreEqual(4, Seven.Par);
            Assert.That(Seven.Length, Is.EqualTo(412).Within(8));
        }

        [Test]
        public void LiesFollowTheIsland()
        {
            var h = Seven;
            Assert.AreEqual(CourseLie.Tee, h.LieAt(h.Tee));
            Assert.AreEqual(CourseLie.Fairway, h.LieAt(new CoursePoint(0, 200)), "the landing area");
            Assert.AreEqual(CourseLie.Rough, h.LieAt(new CoursePoint(-60, 200)), "grass short of the west cliff");
            Assert.AreEqual(CourseLie.Bunker, h.LieAt(new CoursePoint(-41.6, 319.3)), "the upper-left fairway bunker");
            Assert.AreEqual(CourseLie.Green, h.LieAt(h.Pin));
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(120, 200)), "off the east cliff");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(45, 201)), "in the east cove");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(0, -80)), "behind the tee");
        }

        [Test]
        public void ShoreIsAClosedIslandAroundTheWholeHole()
        {
            var h = Seven;
            foreach (var p in h.Centerline) Assert.IsTrue(h.OnLand(p), $"{p} is on land");
            foreach (var b in h.Hazards) Assert.IsTrue(h.OnLand(new CoursePoint(b.X, b.Distance)), $"bunker at {b.X},{b.Distance} is on land");
        }
    }
}
