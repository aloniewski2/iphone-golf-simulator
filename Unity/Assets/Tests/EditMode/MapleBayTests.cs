using System.Linq;
using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// Maple Bay (holes 16–18, blender/scripts/hole16_maple_design.py and friends): the numbers
    /// course_builder.py printed make playable holes, each with its own shape.
    public class MapleBayTests
    {
        static Hole Get(int n) => Course.Course.MapleBay().Holes.Single(h => h.Number == n);

        [Test] public void MaplePointIsPlayable() => Playable(16, 4);
        [Test] public void TheLagoonIsPlayable() => Playable(17, 3);
        [Test] public void HarvestRunIsPlayable() => Playable(18, 5);

        static void Playable(int number, int par)
        {
            var h = Get(number);
            Assert.AreEqual(par, h.Par);
            Assert.AreEqual("autumn", h.Theme);
            Assert.AreEqual(CourseLie.Tee, h.LieAt(h.Tee));
            Assert.AreEqual(CourseLie.Green, h.LieAt(h.Pin));
            foreach (var b in h.Hazards)
            {
                var at = new CoursePoint(b.X, b.Distance);
                Assert.IsTrue(h.OnLand(at), $"hole {number}: bunker at {b.X},{b.Distance} is on land");
            }
        }

        [Test]
        public void MaplePointBendsRoundTheBay()
        {
            var h = Get(16);
            var mid = new CoursePoint((h.Tee.X + h.Pin.X) / 2, (h.Tee.D + h.Pin.D) / 2);
            Assert.AreEqual(CourseLie.Water, h.LieAt(mid), "straight at the pin is across the bay");
            Assert.Greater(h.Length, h.Tee.DistanceTo(h.Pin) + 60, "the way round is much longer than the crow flies");
            for (int i = 1; i < h.Centerline.Length - 1; i++)
                Assert.AreNotEqual(CourseLie.Water, h.LieAt(h.Centerline[i]), $"station {i} is on the crescent");
        }

        [Test]
        public void TheLagoonIsACarryFromAnIslet()
        {
            var h = Get(17);
            Assert.AreEqual(1, h.Islets.Length);
            var mid = new CoursePoint((h.Tee.X + h.Pin.X) / 2, (h.Tee.D + h.Pin.D) / 2);
            Assert.AreEqual(CourseLie.Water, h.LieAt(mid), "the lagoon between tee and green");
        }

        [Test]
        public void HarvestRunIsAChainOfThreeIslands()
        {
            var h = Get(18);
            Assert.AreEqual(2, h.Islets.Length, "the fairway island and the green's mesa");
            Assert.Greater(h.Length, 470);
            var firstCarry = new CoursePoint(h.Centerline[1].X / 2, h.Centerline[1].D / 2);
            Assert.AreEqual(CourseLie.Water, h.LieAt(firstCarry), "the drive carries water");
            var last = h.Centerline[h.Centerline.Length - 2];
            var secondCarry = new CoursePoint((last.X + h.Pin.X) / 2, (last.D + h.Pin.D) / 2);
            Assert.AreEqual(CourseLie.Water, h.LieAt(secondCarry), "and so does the second, up to the mesa");
        }

        [Test]
        public void EveryCourseHasItsOwnHoleNumbers()
        {
            var numbers = Course.Course.All().SelectMany(c => c.Holes).Select(h => h.Number).ToList();
            CollectionAssert.AllItemsAreUnique(numbers);
            Assert.AreEqual("maplebay", Course.Course.Containing(17).Key);
            Assert.AreEqual("cliffside", Course.Course.Containing(12).Key);
            Assert.AreSame(null, Course.Course.Containing(99));
        }
    }
}
