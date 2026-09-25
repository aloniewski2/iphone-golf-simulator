using System.Linq;
using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// Wild Isles (holes 19–23, blender/scripts/hole19_volcano_design.py … hole23_windmill_design.py):
    /// five holes, each its own world, played as one round; their water — lava, a frozen lake, a
    /// canyon, a lagoon, canals — is water to the ball: a penalty, and a drop.
    public class WildIslesTests
    {
        static Hole Get(int n) => Course.Course.WildIsles().Holes.Single(h => h.Number == n);

        [Test]
        public void TheRoundPlaysAllFiveHoles()
        {
            var course = Course.Course.WildIsles();
            CollectionAssert.AreEqual(new[] { 19, 20, 21, 22, 23 }, course.Holes.Select(h => h.Number).ToArray());
            Assert.AreEqual(4 + 5 + 3 + 4 + 4, course.Par);
            Assert.AreEqual("wildisles", course.Key);
            Assert.AreSame(null, Course.Course.WildIsles().Holes.FirstOrDefault(h => !string.IsNullOrEmpty(h.Theme)), "their colours come from the models' own materials");
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
            var volcano = Get(19);
            Assert.AreEqual(CourseLie.Water, volcano.LieAt(new CoursePoint(0, 205.6)), "the lava river across the fairway");
            Assert.AreEqual(CourseLie.Fairway, volcano.LieAt(new CoursePoint(0, 250)), "past it, the fairway");
            Assert.AreEqual(CourseLie.Water, Get(20).LieAt(new CoursePoint(106.1, 249.3)), "the frozen lake");
            Assert.AreEqual(CourseLie.Water, Get(21).LieAt(new CoursePoint(0, 80)), "the canyon between the mesas");
            Assert.AreEqual(CourseLie.Water, Get(22).LieAt(new CoursePoint(-6.6, 166.2)), "the lagoon");
            Assert.AreEqual(CourseLie.Water, Get(23).LieAt(new CoursePoint(0, 156.4)), "the first canal");
        }
    }
}
