using System.Collections.Generic;
using GolfArcade.Course;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class FormatTests
    {
        static MatchPlayer Local(string name) => new() { Name = name, IsLocal = true };

        /// Plays every turn in order, `score(player, hole)` giving strokes or the measure.
        static void PlayOut(Match match, System.Func<string, int, double> score)
        {
            Assert.IsTrue(match.Advance());
            do
            {
                double s = score(match.Current.Name, match.HoleIndex);
                if (match.IsContest) match.RecordMeasure(s); else match.RecordCurrent((int)s);
            } while (match.Advance());
        }

        [Test]
        public void ContestsArePlayedOnTheirOwnHoles()
        {
            var cliffside = Course.Course.Cliffside(); // pars 4, 3, 5, 4, 3
            CollectionAssert.AreEqual(new[] { 12, 15 }, System.Array.ConvertAll(Match.HolesFor(cliffside, MatchFormat.ClosestToPin).Holes, h => h.Number));
            CollectionAssert.AreEqual(new[] { 7, 13, 14 }, System.Array.ConvertAll(Match.HolesFor(cliffside, MatchFormat.LongestDrive).Holes, h => h.Number));
            Assert.AreSame(cliffside, Match.HolesFor(cliffside, MatchFormat.MatchPlay));
            var par4 = new Course.Course { Name = "x", Holes = new[] { cliffside.Holes[0] } };
            Assert.AreSame(par4, Match.HolesFor(par4, MatchFormat.ClosestToPin), "no par 3s: the holes there are");
        }

        [Test]
        public void MatchPlayCountsHolesNotStrokes()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Local("Alex"), Local("Sam") }, 1, MatchFormat.MatchPlay);
            // Alex: 3, 5, 3 (a blow-up on 2); Sam: 4, 4, 4 — Sam has fewer strokes, Alex more holes
            var strokes = new Dictionary<string, int[]> { ["Alex"] = new[] { 3, 8, 2 }, ["Sam"] = new[] { 4, 4, 4 } };
            PlayOut(match, (who, h) => strokes[who][h]);
            Assert.AreEqual(2, match.HolesWon(match.Players[0]));
            Assert.AreEqual("1 UP", match.Standing(match.Players[0]));
            Assert.AreEqual("1 DOWN", match.Standing(match.Players[1]));
            Assert.AreEqual("Alex wins 1 UP", match.Headline());
            Assert.AreEqual(1, match.ResultFor(match.Players[0]));
            Assert.AreEqual(-1, match.ResultFor(match.Players[1]));
        }

        [Test]
        public void MatchPlayCanFinishAllSquare()
        {
            var match = new Match(Course.Course.Meadow(), new[] { Local("Alex"), Local("Sam") }, 1, MatchFormat.MatchPlay);
            var strokes = new Dictionary<string, int[]> { ["Alex"] = new[] { 3, 5, 3 }, ["Sam"] = new[] { 4, 4, 3 } };
            PlayOut(match, (who, h) => strokes[who][h]);
            Assert.AreEqual("All square", match.Headline());
            Assert.AreEqual(0, match.ResultFor(match.Players[0]));
        }

        [Test]
        public void ClosestToThePinTakesOneShotEachAndTheNearestWins()
        {
            var course = Match.HolesFor(Course.Course.Cliffside(), MatchFormat.ClosestToPin);
            var match = new Match(course, new[] { Local("Alex"), Local("Sam") }, 1, MatchFormat.ClosestToPin);
            var order = new List<(string, int)>();
            var feet = new Dictionary<string, double[]> { ["Alex"] = new[] { 12.0, Match.MissedGreen }, ["Sam"] = new[] { 30.0, 8.0 } };
            PlayOut(match, (who, h) => { order.Add((who, h)); return feet[who][h]; });
            CollectionAssert.AreEqual(new[] { ("Alex", 0), ("Sam", 0), ("Alex", 1), ("Sam", 1) }, order, "one shot each, hole by hole");
            Assert.AreEqual("Alex", match.HoleWinner(0).Name);
            Assert.AreEqual("Sam", match.HoleWinner(1).Name, "the water loses to anything on the green");
            Assert.AreEqual(12.0, match.Best(match.Players[0]));
            Assert.AreEqual("Sam wins · 8 ft", match.Headline(), "one hole each: the nearest shot of the day decides");
            Assert.AreEqual("Missed", match.Measure(Match.MissedGreen));
            Assert.AreEqual("In the hole!", match.Measure(0));
            Assert.IsTrue(match.EveryoneFinished);
            Assert.AreEqual(0, match.Players[0].Card.HolesPlayed, "a contest writes no strokes");
        }

        [Test]
        public void TheLongestDriveOnTheFairwayWins()
        {
            var course = Match.HolesFor(Course.Course.Cliffside(), MatchFormat.LongestDrive);
            var match = new Match(course, new[] { Local("Alex"), Local("Sam") }, 1, MatchFormat.LongestDrive);
            // Alex bombs one into the rough (0), then 250 and 270; Sam 240, 260, 230
            var yards = new Dictionary<string, double[]> { ["Alex"] = new[] { 0.0, 250, 270 }, ["Sam"] = new[] { 240.0, 260, 230 } };
            PlayOut(match, (who, h) => yards[who][h]);
            Assert.AreEqual("Sam", match.HoleWinner(0).Name, "a drive off the fairway doesn't count");
            Assert.AreEqual(2, match.HolesWon(match.Players[1]));
            Assert.AreEqual("Sam wins with 2 holes", match.Headline());
            Assert.AreEqual("1 hole · 270 yd", match.Standing(match.Players[0]));
        }

        [Test]
        public void NobodyOnTheFairwayHalvesTheHole()
        {
            var course = Match.HolesFor(Course.Course.Cliffside(), MatchFormat.LongestDrive);
            var match = new Match(course, new[] { Local("Alex"), Local("Sam") }, 1, MatchFormat.LongestDrive);
            PlayOut(match, (who, h) => 0);
            Assert.IsNull(match.HoleWinner(0));
            Assert.AreEqual("It's a tie", match.Headline());
        }
    }

    public class ChampionshipTests
    {
        static Championship Start(int seed = 7) =>
            Championship.Start(new[] { ("Alex", "p1") }, "cliffside", "Cliffside", new[] { 4, 3, 5, 4, 3 }, seed);

        [Test]
        public void FourRoundsAgainstAFieldOfPros()
        {
            var open = Start();
            Assert.AreEqual(12, open.Field.Count, "the player and eleven pros");
            Assert.AreEqual("Cliffside Open", open.Title);
            for (int r = 0; r < Championship.Rounds; r++)
            {
                Assert.AreEqual(r, open.Round);
                Assert.IsTrue(open.RecordRound("p1", -1));
            }
            Assert.IsTrue(open.IsOver);
            Assert.IsFalse(open.RecordRound("p1", 0));
            foreach (var e in open.Field) Assert.AreEqual(4, e.RoundsPlayed);
            Assert.AreEqual(-4, open.EntryFor("p1").Total);
            StringAssert.Contains("wins the Cliffside Open", open.Headline());
        }

        [Test]
        public void TheProsWaitForEveryPlayerOnThePhone()
        {
            var open = Championship.Start(new[] { ("Alex", "p1"), ("Sam", "p2") }, "cliffside", "Cliffside", new[] { 4, 3, 5, 4, 3 }, 3);
            open.RecordRound("p1", 0);
            Assert.AreEqual(0, open.Round, "Sam still to play");
            Assert.IsFalse(open.RecordRound("p1", -5), "a round is recorded once");
            Assert.AreEqual(0, open.Field.Find(e => !e.IsPlayer).RoundsPlayed);
            open.RecordRound("p2", 2);
            Assert.AreEqual(1, open.Round);
            Assert.IsTrue(open.Field.TrueForAll(e => e.RoundsPlayed == 1));
        }

        [Test]
        public void TheSameEventHasTheSameScoresAndBetterProsScoreLower()
        {
            var a = Start(11); var b = Start(11);
            for (int r = 0; r < 4; r++) { a.RecordRound("p1", 0); b.RecordRound("p1", 0); }
            for (int i = 0; i < a.Field.Count; i++) CollectionAssert.AreEqual(a.Field[i].RoundToPar, b.Field[i].RoundToPar);

            // over many events the best pro averages well under the weakest
            double best = 0, worst = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var c = Start(seed);
                for (int r = 0; r < 4; r++) c.RecordRound("p1", 0);
                best += c.Field.Find(e => e.Name == "Riley Park").Total;
                worst += c.Field.Find(e => e.Name == "Rosa Vega").Total;
            }
            Assert.Less(best / 200, worst / 200 - 3);
        }

        [Test]
        public void TheLeaderboardSharesPlaces()
        {
            var open = Start();
            open.Field.Clear();
            open.Field.Add(new Championship.Entry { Name = "A", ProfileId = "p1", RoundToPar = new[] { -2, Championship.NotPlayed, Championship.NotPlayed, Championship.NotPlayed } });
            open.Field.Add(new Championship.Entry { Name = "B", RoundToPar = new[] { -3, Championship.NotPlayed, Championship.NotPlayed, Championship.NotPlayed } });
            open.Field.Add(new Championship.Entry { Name = "C", RoundToPar = new[] { -2, Championship.NotPlayed, Championship.NotPlayed, Championship.NotPlayed } });
            var board = open.Leaderboard();
            Assert.AreEqual("B", board[0].Name);
            Assert.AreEqual("1", open.Position(board[0]));
            Assert.AreEqual("T2", open.Position(open.EntryFor("p1")));
            Assert.AreEqual("B leads at -3", open.Headline());
        }
    }
}
