using System.Collections.Generic;
using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class MatchTests
    {
        static MatchPlayer Local(string name) => new() { Name = name, IsLocal = true };
        static MatchPlayer Remote(string name, string id) => new() { Name = name, RemoteId = id, IsLocal = false };
        /// One hole of Cliffside (hole 7, a par 4).
        static Course.Course OneHole() => new() { Name = "Cliffside", Holes = new[] { Course.Course.Cliffside().Holes[0] } };

        [Test]
        public void TwoPlayersTakeTurnsHoleByHole()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Local("Alex"), Local("Sam") }, 7);
            var order = new List<(string, int)>();
            Assert.IsTrue(match.Advance());
            do
            {
                order.Add((match.Current.Name, match.HoleIndex));
                match.RecordCurrent(4);
            } while (match.Advance());
            CollectionAssert.AreEqual(new[] { ("Alex", 0), ("Sam", 0), ("Alex", 1), ("Sam", 1), ("Alex", 2), ("Sam", 2) }, order);
            Assert.IsTrue(match.LocalPlayIsOver);
            Assert.IsTrue(match.EveryoneFinished);
            Assert.IsFalse(match.Advance(), "stays over");
        }

        [Test]
        public void AdvanceBeforeAnyScoreKeepsTheFirstTurn()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Local("Alex"), Local("Sam") }, 1);
            Assert.IsTrue(match.Advance());
            Assert.AreEqual("Alex", match.Current.Name);
            Assert.AreEqual(0, match.HoleIndex);
        }

        [Test]
        public void EveryoneOnAHoleGetsTheSameWindAndTheSeedDecidesIt()
        {
            var a = new Match(Course.Course.Meadow(), new[] { Local("Alex") }, 12345);
            var b = new Match(Course.Course.Meadow(), new[] { Local("Sam") }, 12345);
            for (int h = 0; h < 3; h++)
            {
                Assert.AreEqual(a.WindFor(h).SpeedMPH, b.WindFor(h).SpeedMPH);
                Assert.AreEqual(a.WindFor(h).DirectionDegrees, b.WindFor(h).DirectionDegrees);
                Assert.AreEqual(a.WindFor(h).SpeedMPH, a.WindFor(h).SpeedMPH, "asking again gives the same wind");
            }
            bool differs = false;
            var c = new Match(Course.Course.Meadow(), new[] { Local("Kim") }, 999);
            for (int h = 0; h < 3; h++) differs |= c.WindFor(h).DirectionDegrees != a.WindFor(h).DirectionDegrees;
            Assert.IsTrue(differs, "another seed, another day");
        }

        [Test]
        public void StandingsAndTheHeadline()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Local("Alex"), Local("Sam") }, 3);
            match.Advance();
            foreach (var strokes in new[] { 5, 4, 4, 4, 3, 3 }) { match.RecordCurrent(strokes); match.Advance(); }
            // Alex 5,4,3 = +1; Sam 4,4,3 = E
            Assert.AreEqual("Sam", match.Standings()[0].Name);
            Assert.AreEqual("Sam wins by 1", match.Headline());
            Assert.AreEqual("Sam", match.HoleWinner(0).Name);
            Assert.IsNull(match.HoleWinner(1), "a halved hole has no winner");
            CollectionAssert.AreEqual(new[] { 0 }, match.OpponentsToPar(match.Players[0]));
        }

        [Test]
        public void ATieReadsAsATie()
        {
            var match = new Match(OneHole(), new[] { Local("Alex"), Local("Sam") }, 3);
            match.Advance(); match.RecordCurrent(4);
            match.Advance(); match.RecordCurrent(4);
            Assert.AreEqual("Tied at E", match.Headline());
        }

        [Test]
        public void OnlineOnlyTheLocalPlayerTakesTurnsAndRemoteScoresArriveOnce()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Remote("Sam", "s"), Local("Alex"), Remote("Kim", "k") }, 3);
            Assert.IsTrue(match.Advance());
            Assert.AreEqual("Alex", match.Current.Name);
            Assert.IsTrue(match.RecordRemote("s", 0, 3));
            Assert.IsFalse(match.RecordRemote("s", 0, 1), "a repeated message cannot change a score");
            Assert.IsFalse(match.RecordRemote("s", 5, 3), "no such hole");
            Assert.IsFalse(match.RecordRemote("nobody", 0, 3));
            Assert.AreEqual(3, match.Players[0].Card.StrokesOn(0));

            match.RecordCurrent(4);
            Assert.IsTrue(match.Advance());
            Assert.AreEqual(1, match.HoleIndex, "straight on to the next hole: the others play on their own phones");
            Assert.IsFalse(match.EveryoneFinished);
        }

        [Test]
        public void ThePhonePassesOnOnlyWhileSomeoneHereHasTheHoleToPlay()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Local("Alex"), Remote("Kim", "k"), Local("Sam") }, 3);
            match.Advance();
            Assert.IsTrue(match.LocalsLeftOn(0));
            match.RecordCurrent(4);
            Assert.IsTrue(match.LocalsLeftOn(0), "Sam still has the hole to play");
            match.Advance();
            Assert.AreEqual("Sam", match.Current.Name);
            match.RecordCurrent(5);
            Assert.IsFalse(match.LocalsLeftOn(0), "Kim plays on another phone");
        }

        [Test]
        public void ARoundNeedsSomeoneOnThisPhone()
        {
            Assert.Throws<System.ArgumentException>(() => new Match(Course.Course.Meadow(), new[] { Remote("Sam", "s") }, 1));
            Assert.Throws<System.ArgumentException>(() => new Match(Course.Course.Meadow(), new MatchPlayer[0], 1));
        }
    }
}
