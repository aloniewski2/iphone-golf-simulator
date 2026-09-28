using GolfArcade.Net.Online;
using NUnit.Framework;

namespace GolfArcade.Tests
{
    /// What the player reads when the game server can't be reached, and a room that takes
    /// whatever the socket brings without falling over.
    public class OnlineErrorTests
    {
        [Test]
        public void FailuresAreSaidInWords()
        {
            const bool conn = true;
            StringAssert.Contains("is the Mac on", BackendClient.Explain(0, conn, "Cannot resolve destination host", null));
            StringAssert.Contains("isn't running", BackendClient.Explain(0, conn, "Cannot connect to destination host", null));
            StringAssert.Contains("didn't answer", BackendClient.Explain(0, conn, "Request timeout", null));
            StringAssert.Contains("try again", BackendClient.Explain(503, false, "HTTP/1.1 503", null));
            Assert.AreEqual("name taken", BackendClient.Explain(400, false, "HTTP/1.1 400", "name taken"), "the server's own words win");
        }

        [Test]
        public void ARoomTakesWhateverComes()
        {
            var room = new OnlineRoom();
            room.Apply(null);
            room.Apply(new OnlineMessage { type = "nonsense" });
            room.Apply(new OnlineMessage { type = "room", code = null, host = null, players = null });
            Assert.IsFalse(room.InRoom);
            room.Apply(new OnlineMessage { type = "hole", playerId = "nobody", hole = 99, strokes = 4 });
            room.Apply(new OnlineMessage { type = "error", error = null });
            Assert.AreEqual("", room.LastError);
            room.Apply(new OnlineMessage { type = "room", code = "AB12", host = "p1", players = new[] { new OnlinePlayer { id = "p1", strokes = new int[3] } } });
            room.Apply(new OnlineMessage { type = "hole", playerId = "p1", hole = 7, strokes = 4 });   // a hole the card hasn't got
            Assert.IsTrue(room.InRoom);
        }
    }
}
