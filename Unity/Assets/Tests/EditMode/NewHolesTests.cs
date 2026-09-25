using System.Linq;
using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// Holes 13–15 (blender/scripts/course_builder.py and its design modules): the numbers the
    /// builder printed make playable holes — the tee on the tee, the pin on the green, the
    /// fairway on land, the bunkers where they were modelled, water where the sea is.
    public class NewHolesTests
    {
        static Hole Get(int n) => Course.Course.Cliffside().Holes.Single(h => h.Number == n);

        [Test] public void TheSpiralIsPlayable() => Playable(13, 5);
        [Test] public void TheWitchsLairIsPlayable() => Playable(14, 4);
        [Test] public void TheStepsIsPlayable() => Playable(15, 3);

        static void Playable(int number, int par)
        {
            var h = Get(number);
            Assert.AreEqual(par, h.Par);
            Assert.AreEqual(CourseLie.Tee, h.LieAt(h.Tee));
            Assert.AreEqual(CourseLie.Green, h.LieAt(h.Pin));
            for (int i = 1; i < h.Centerline.Length - 1; i++)
                Assert.AreEqual(CourseLie.Fairway, h.LieAt(h.Centerline[i]), $"hole {number}: fairway station {i}");
            foreach (var b in h.Hazards) Assert.IsTrue(h.OnLand(new CoursePoint(b.X, b.Distance)), $"hole {number}: bunker at {b.X},{b.Distance} is on land");
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(0, -60)), "behind the tee is the sea");
        }

        [Test]
        public void TheSpiralIsAParFiveThatWindsRound()
        {
            var h = Get(13);
            Assert.Greater(h.Length, 480, "the way round is a par 5's length");
            Assert.Less(h.Tee.DistanceTo(h.Pin), 160, "though the pin is close as the crow flies");
        }

        [Test]
        public void TheStepsIsACarryOverWater()
        {
            var h = Get(15);
            Assert.AreEqual(CourseLie.Water, h.LieAt(new CoursePoint(0, 35)), "the channel between the tee islet and the green");
            Assert.AreEqual(1, h.Islets.Length);
        }
    }
}
