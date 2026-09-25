using GolfArcade.Course;
using GolfArcade.Profile;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class ProfileTests
    {
        [Test]
        public void NamesAreCleanedAndABlankNameKeepsTheOldOne()
        {
            var p = new PlayerProfile { Name = "Alex" };
            p.Rename("   ");
            Assert.AreEqual("Alex", p.Name);
            p.Rename("  Sam\n ");
            Assert.AreEqual("Sam", p.Name);
            p.Rename("A very long golfer name");
            Assert.AreEqual(PlayerProfile.MaxNameLength, p.Name.Length);
        }

        [Test]
        public void TheBookAlwaysHasSomeoneToPlayAs()
        {
            var book = new ProfileBook();
            var me = book.Active;
            Assert.IsNotNull(me);
            Assert.AreEqual(1, book.Profiles.Count);
            Assert.AreEqual(me.Id, book.ActiveId);
            Assert.IsFalse(book.Remove(me.Id), "the last profile stays");
        }

        [Test]
        public void ASecondPlayerGetsTheNextNameAndADifferentLook()
        {
            var book = new ProfileBook();
            var alex = book.Add("Alex");
            alex.Body = 0; alex.Kit = 0;
            var second = book.Opponent(alex.Id);
            Assert.AreNotEqual(alex.Id, second.Id);
            Assert.AreEqual("Player 1", second.Name);
            Assert.AreEqual(1, second.Body, "the other golfer model");
            Assert.AreNotEqual(alex.Kit, second.Kit, "in another kit");
            Assert.AreSame(second, book.Opponent(alex.Id), "an existing profile is reused");
            Assert.AreEqual(alex.Id, book.ActiveId, "adding players does not change who the phone plays as");
        }

        [Test]
        public void RemovingTheActiveProfileHandsOverToAnother()
        {
            var book = new ProfileBook();
            var a = book.Add("A");
            var b = book.Add("B");
            book.SetActive(b.Id);
            Assert.IsTrue(book.Remove(b.Id));
            Assert.AreEqual(a.Id, book.ActiveId);
        }

        [Test]
        public void TheBookStopsAtItsLimit()
        {
            var book = new ProfileBook();
            for (int i = 0; i < ProfileBook.MaxProfiles; i++) Assert.IsNotNull(book.Add());
            Assert.IsFalse(book.CanAdd);
            Assert.IsNull(book.Add());
        }

        [Test]
        public void StatsCountTheCard()
        {
            var stats = new ProfileStats();
            var card = new Scorecard(Course.Course.Meadow()); // pars 4, 4, 3
            card.Record(0, 3); card.Record(1, 4); card.Record(2, 1);
            stats.RecordRound(card);
            Assert.AreEqual(1, stats.RoundsPlayed);
            Assert.AreEqual(3, stats.HolesPlayed);
            Assert.AreEqual(1, stats.Birdies);
            Assert.AreEqual(1, stats.Pars);
            Assert.AreEqual(1, stats.HolesInOne);
            Assert.IsTrue(stats.HasBest);
            Assert.AreEqual(-3, stats.BestToPar);
            Assert.AreEqual(-1.0, stats.AverageToParPerHole, 1e-9);
            Assert.AreEqual(0, stats.MatchesPlayed, "a solo round is not a match");
        }

        [Test]
        public void MatchesAreWonTiedOrLost()
        {
            var stats = new ProfileStats();
            var oneHole = new Course.Course { Name = "Cliffside", Holes = new[] { Course.Course.Cliffside().Holes[0] } };
            Scorecard Card(int strokes) { var c = new Scorecard(oneHole); c.Record(0, strokes); return c; }
            // Opponents are given against par; the hole is a par 4.
            stats.RecordRound(Card(3), new[] { 0 });
            stats.RecordRound(Card(4), new[] { 0, 2 });
            stats.RecordRound(Card(5), new[] { 0 });
            Assert.AreEqual((1, 1, 1), (stats.MatchesWon, stats.MatchesTied, stats.MatchesLost));
            Assert.AreEqual(-1, stats.BestToPar, "the best round is kept");
        }

        [Test]
        public void AnUnfinishedCardIsNotCounted()
        {
            var stats = new ProfileStats();
            stats.RecordRound(new Scorecard(Course.Course.Meadow()));
            Assert.AreEqual(0, stats.RoundsPlayed);
        }
    }
}
