using GolfArcade.Net.Online;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    public class OnlineRoomTests
    {
        static OnlineMessage Room(bool started, params OnlinePlayer[] players) => new()
        {
            type = "room", code = "ABCD", host = "alex", course = "cliffside", seed = 42, started = started, players = players,
        };

        static OnlinePlayer Seat(string id, params int[] strokes) => new() { id = id, name = id, connected = true, strokes = strokes };

        [Test]
        public void TheLobbyFollowsTheServer()
        {
            var room = new OnlineRoom();
            int changes = 0;
            room.Changed = () => changes++;
            room.Apply(new OnlineMessage { type = "welcome", playerId = "alex" });
            Assert.IsFalse(room.InRoom);
            room.Apply(Room(false, Seat("alex", 0)));
            Assert.IsTrue(room.InRoom);
            Assert.IsTrue(room.IsHost);
            Assert.IsFalse(room.CanStart, "needs a second golfer");
            room.Apply(Room(false, Seat("alex", 0), Seat("sam", 0)));
            Assert.IsTrue(room.CanStart);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void TheRoundStartsOnceWithTheServersSeed()
        {
            var room = new OnlineRoom();
            int starts = 0, seed = 0;
            room.RoundStarted = (s, course) => { starts++; seed = s; };
            room.Apply(new OnlineMessage { type = "welcome", playerId = "sam" });
            room.Apply(Room(false, Seat("alex", 0), Seat("sam", 0)));
            room.Apply(new OnlineMessage { type = "start", seed = 42, course = "cliffside" });
            room.Apply(Room(true, Seat("alex", 0), Seat("sam", 0)));
            Assert.AreEqual(1, starts, "the start and the room update that follows it are one start");
            Assert.AreEqual(42, seed);
            Assert.IsFalse(room.IsHost);
        }

        [Test]
        public void ARejoiningPhoneIsSentStraightToTheRound()
        {
            var room = new OnlineRoom();
            int starts = 0;
            room.RoundStarted = (s, course) => starts++;
            room.Apply(Room(true, Seat("alex", 4), Seat("sam", 0)));
            Assert.AreEqual(1, starts);
        }

        [Test]
        public void ScoresFillInTheCard()
        {
            var room = new OnlineRoom();
            string who = null; int hole = -1, strokes = 0;
            room.HoleScored = (id, h, s) => { who = id; hole = h; strokes = s; };
            room.Apply(Room(true, Seat("alex", 0), Seat("sam", 0)));
            room.Apply(new OnlineMessage { type = "hole", playerId = "sam", hole = 0, strokes = 3 });
            Assert.AreEqual(("sam", 0, 3), (who, hole, strokes));
            Assert.AreEqual(3, room.Find("sam").strokes[0]);
            room.Apply(new OnlineMessage { type = "hole", playerId = "sam", hole = 9, strokes = 3 });
            Assert.AreEqual(3, room.Find("sam").strokes[0], "an out-of-range hole is ignored");
        }

        [Test]
        public void ErrorsAreKeptForTheMenuAndResetClearsTheRoom()
        {
            var room = new OnlineRoom();
            room.Apply(Room(false, Seat("alex", 0)));
            room.Apply(new OnlineMessage { type = "error", error = "that room is full" });
            Assert.AreEqual("that room is full", room.LastError);
            room.Reset();
            Assert.IsFalse(room.InRoom);
            Assert.AreEqual(0, room.Players.Count);
            room.Apply(null);
        }
    }
}
