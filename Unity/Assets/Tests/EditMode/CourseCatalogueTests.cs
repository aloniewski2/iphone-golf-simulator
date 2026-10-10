using System.Linq;
using GolfArcade.Course;
using GolfArcade.Profile;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// The sixteen modelled holes dealt out as three courses: Cliffside (5 holes), Wild Isles (6) and the
    /// Magma Open (5). The old Postcards course is gone; its holes are Cliffside's (8, 9) and the Magma Open's (10).
    public class CourseCatalogueTests
    {
        [Test]
        public void ThereAreThreeCoursesAndEveryModelledHoleIsOnExactlyOne()
        {
            var courses = Course.Course.All();
            CollectionAssert.AreEqual(new[] { "cliffside", "wildisles", "magma" }, courses.Select(c => c.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Cliffside", "Wild Isles", "Magma Open" }, courses.Select(c => c.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 5, 6, 5 }, courses.Select(c => c.Holes.Length).ToArray());
            var numbers = courses.SelectMany(c => c.Holes).Select(h => h.Number).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { 7, 8, 9, 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 }, numbers, "sixteen holes, none twice, none left out");
        }

        [Test]
        public void EachCourseHasTheHolesAndParsItWasDealt()
        {
            var cliffside = Course.Course.Cliffside();
            CollectionAssert.AreEqual(new[] { 7, 8, 9, 12, 15 }, cliffside.Holes.Select(h => h.Number).ToArray());
            Assert.AreEqual(19, cliffside.Par);
            var wild = Course.Course.WildIsles();
            CollectionAssert.AreEqual(new[] { 13, 14, 17, 18, 19, 20 }, wild.Holes.Select(h => h.Number).ToArray());
            Assert.AreEqual(25, wild.Par);
            var magma = Course.Course.Magma();
            CollectionAssert.AreEqual(new[] { 10, 16, 21, 22, 23 }, magma.Holes.Select(h => h.Number).ToArray());
            Assert.AreEqual(20, magma.Par);
        }

        [Test]
        public void HolesAreNumberedFromOneWithinTheirCourse()
        {
            foreach (var course in Course.Course.All())
                for (int i = 0; i < course.Holes.Length; i++)
                {
                    Assert.AreEqual(i + 1, course.Holes[i].Ordinal, $"{course.Name}: place of hole {course.Holes[i].Number}");
                    Assert.AreEqual(i + 1, course.Holes[i].PlayNumber, $"{course.Name}: the number the player sees");
                }
            Assert.AreEqual(2, Catalogue.Hole(8).PlayNumber, "Needle is the second hole of Cliffside");
            Assert.AreEqual(1, Catalogue.Hole(13).PlayNumber, "the Spiral opens Wild Isles");
            Assert.AreEqual(1, Catalogue.Hole(10).PlayNumber, "the Crater opens the Magma Open");
            Assert.AreEqual(2, Course.Course.Meadow().Holes[1].PlayNumber, "a hole outside the three courses shows its own number");
        }

        [Test]
        public void EveryHoleHasANameAndABlurbForItsCards()
        {
            foreach (var hole in Course.Course.All().SelectMany(c => c.Holes))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(hole.Name), $"hole {hole.Number} has a name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(hole.Blurb), $"hole {hole.Number} has a blurb");
            }
            Assert.AreEqual("Needle", Catalogue.Hole(8).Name);
            Assert.AreEqual("Split", Catalogue.Hole(9).Name);
            Assert.AreEqual("Crater", Catalogue.Hole(10).Name);
        }

        [Test]
        public void TheRetiredPostcardsKeyFindsCliffside()
        {
            Assert.AreEqual("cliffside", Course.Course.ByKey("postcards").Key, "a saved choice or a link from before still opens a course");
            Assert.AreEqual("cliffside", Course.Course.Containing(8).Key);
            Assert.AreEqual("cliffside", Course.Course.Containing(9).Key);
            Assert.AreEqual("magma", Course.Course.Containing(10).Key);
            Assert.IsNull(Course.Course.ByKey("nowhere"));
        }

        [Test]
        public void TheUnlockChainStillRunsCliffsideWildIslesMagma()
        {
            Unlocks.Everything = false;
            var fresh = new PlayerProfile();
            Assert.IsTrue(Unlocks.CourseOpen(fresh, "cliffside"), "Cliffside is everyone's");
            Assert.IsFalse(Unlocks.CourseOpen(fresh, "wildisles"));
            Assert.IsFalse(Unlocks.CourseOpen(fresh, "magma"));
            Assert.IsNull(Unlocks.CourseReward("postcards"), "the retired key opens nothing of its own");
        }
    }
}
