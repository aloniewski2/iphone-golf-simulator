using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class ScorecardTests
    {
        [Test]
        public void TotalsFollowTheHolesPlayed()
        {
            var card = new Scorecard(Course.Course.Meadow()); // pars 4, 4, 3
            Assert.AreEqual(0, card.Total);
            Assert.AreEqual(0, card.ToPar);
            Assert.IsFalse(card.IsComplete);
            card.Record(0, 5);
            Assert.AreEqual(5, card.Total);
            Assert.AreEqual(1, card.ToPar, "par is only counted for finished holes");
            card.Record(1, 3);
            card.Record(2, 3);
            Assert.AreEqual(11, card.Total);
            Assert.AreEqual(0, card.ToPar);
            Assert.IsTrue(card.IsComplete);
            Assert.AreEqual(3, card.HolesPlayed);
        }

        [Test]
        public void RowsReadLikeACard()
        {
            var card = new Scorecard(Course.Course.Meadow());
            card.Record(1, 6);
            var rows = new System.Collections.Generic.List<(string hole, string par, string score)>(card.Rows());
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(("1", "4", "–"), rows[0]);
            Assert.AreEqual(("2", "4", "6"), rows[1]);
        }

        [Test]
        public void ScoresHaveTheirNames()
        {
            Assert.AreEqual("Hole in one!", Scorecard.ScoreName(1, 3));
            Assert.AreEqual("Eagle!", Scorecard.ScoreName(2, 4));
            Assert.AreEqual("Birdie!", Scorecard.ScoreName(3, 4));
            Assert.AreEqual("Par", Scorecard.ScoreName(4, 4));
            Assert.AreEqual("Bogey", Scorecard.ScoreName(5, 4));
            Assert.AreEqual("Double bogey", Scorecard.ScoreName(6, 4));
            Assert.AreEqual("+3", Scorecard.ScoreName(7, 4));
            Assert.AreEqual("E", Scorecard.FormatToPar(0));
            Assert.AreEqual("+2", Scorecard.FormatToPar(2));
            Assert.AreEqual("-1", Scorecard.FormatToPar(-1));
        }

        [Test]
        public void AHoleNeedsAStroke()
        {
            var card = new Scorecard(Course.Course.Meadow());
            Assert.Throws<System.ArgumentOutOfRangeException>(() => card.Record(0, 0));
        }
    }
}
