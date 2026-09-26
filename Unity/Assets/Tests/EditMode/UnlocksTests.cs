using System.Linq;
using GolfArcade.Profile;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// Rewards (Profile/Unlocks.cs): earned by the record, each announced once; Wild Isles opens
    /// when Cliffside is beaten; a pick not yet earned falls back to the free one.
    public class UnlocksTests
    {
        [SetUp] public void Closed() => Unlocks.Everything = false;

        [Test]
        public void WildIslesOpensWhenCliffsideIsBeaten()
        {
            var p = new PlayerProfile();
            Assert.IsFalse(Unlocks.CourseOpen(p, "wildisles"), "locked to start with");
            Assert.IsTrue(Unlocks.CourseOpen(p, "cliffside"), "Cliffside is always open");
            p.Stats.RecordCourse("cliffside", 2);
            p.Stats.RecordCourse("wildisles-17", -1);
            Assert.IsFalse(Unlocks.CourseOpen(p, "wildisles"), "two over, and a round elsewhere, are not enough");
            p.Stats.RecordCourse("cliffside", 0);
            Assert.IsTrue(Unlocks.CourseOpen(p, "wildisles"), "level par on Cliffside opens it");
            Assert.AreEqual(0, p.Stats.BestOn("cliffside"));
            Assert.AreEqual(2, p.Stats.RoundsOn("cliffside"));
        }

        [Test]
        public void EachRewardIsAnnouncedOnce()
        {
            var p = new PlayerProfile();
            Assert.IsEmpty(Unlocks.Announce(p), "nothing earned yet (the free ones are not news)");
            p.Stats.Birdies = 1;
            p.Stats.RecordCourse("cliffside", -1);
            var news = Unlocks.Announce(p).Select(r => r.Id).ToList();
            CollectionAssert.AreEqual(new[] { Unlocks.WildIsles, "outfit.4", "ball.yellow" }, news);
            Assert.IsEmpty(Unlocks.Announce(p), "and only once");
        }

        [Test]
        public void APickNotYetEarnedFallsBackToTheFreeOne()
        {
            var p = new PlayerProfile { Ball = "ball.gold", Trail = "trail.fire", Club = "nonsense" };
            Assert.AreEqual("ball.white", Unlocks.Chosen(p, RewardKind.Ball));
            Assert.AreEqual("trail.none", Unlocks.Chosen(p, RewardKind.Trail));
            Assert.AreEqual("club.classic", Unlocks.Chosen(p, RewardKind.Club));
            p.Stats.Eagles = 1; p.Stats.LongestDrive = 262;
            Assert.AreEqual("ball.gold", Unlocks.Chosen(p, RewardKind.Ball), "an eagle earns the gold ball");
            Assert.AreEqual("trail.fire", Unlocks.Chosen(p, RewardKind.Trail), "a 262-yard drive earns the fire trail");
        }

        [Test]
        public void TheFirstOutfitColoursAreEveryones()
        {
            var p = new PlayerProfile();
            for (int i = 0; i < 6; i++) Assert.AreEqual(i < 3, Unlocks.OutfitOpen(p, i), $"colour {i}");
            p.Stats.RoundsPlayed = 5;
            Assert.IsTrue(Unlocks.OutfitOpen(p, 3), "five rounds earn crimson");
            Unlocks.Everything = true;
            Assert.IsTrue(Unlocks.OutfitOpen(new PlayerProfile(), 5));
            Unlocks.Everything = false;
        }

        [Test]
        public void EveryRewardHasItsWay()
        {
            foreach (var r in Unlocks.All)
            {
                Assert.IsNotEmpty(r.Name, r.Id);
                if (!r.Free) Assert.IsNotEmpty(r.How, $"{r.Id} says how it is earned");
            }
            CollectionAssert.AllItemsAreUnique(Unlocks.All.Select(r => r.Id));
            foreach (var kind in new[] { RewardKind.Ball, RewardKind.Trail, RewardKind.Club })
                Assert.AreEqual(1, Unlocks.Of(kind).Count(r => r.Free), $"one free {kind}");
        }
    }
}
