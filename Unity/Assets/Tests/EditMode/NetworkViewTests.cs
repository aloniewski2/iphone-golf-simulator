using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEngine;
using GolfArcade.Multiplayer;

namespace GolfArcade.Tests {
    /// How each phone shows a tennis match ("near", "split" or "none"), as decided by the lobby owner. Plain C#, so these also run
    /// without Unity (Tools/netsim).
    public class NetworkViewTests {
        static NetworkParticipant P(string id, int seat, string view = null) => new() { id = id, name = id, seat = seat, view = view, connected = true };
        static NetworkConfiguration Config(string sport, string local, params NetworkParticipant[] people) => new() {
            lobbyID = "lobby", matchID = "match", hostID = "a", localID = local, sport = sport, venue = sport == "golf" ? "resort" : "resort",
            sets = 1, games = 3, seed = 1, participants = people };

        [Test] public void EveryCombinationTheOwnerCanProduceIsValid() {
            Assert.IsTrue(Config("tennis", "a", P("a", 0, "near"), P("b", 1, "near")).Valid, "both phones have a TV");
            Assert.IsTrue(Config("tennis", "a", P("a", 0, "split"), P("b", 1, "none")).Valid, "only the owner has a TV");
            Assert.IsTrue(Config("tennis", "b", P("a", 0, "none"), P("b", 1, "split")).Valid, "only the guest has a TV");
            Assert.IsTrue(Config("tennis", "a", P("a", 0), P("b", 1)).Valid, "no views at all (an older owner) still parses");
            Assert.IsTrue(Config("tennis", "a", P("a", 0, "near"), P("b", 1, "near"), P("c", -1)).Valid, "a spectator needs no view");
            Assert.IsTrue(Config("golf", "a", P("a", 0), P("b", 1)).Valid, "golf has no views");
        }
        [Test] public void ContradictoryViewsAreRejected() {
            Assert.IsFalse(Config("tennis", "a", P("a", 0, "split"), P("b", 1, "split")).Valid, "two phones cannot both show the shared screen");
            Assert.IsFalse(Config("tennis", "a", P("a", 0, "split"), P("b", 1, "near")).Valid, "a split phone's partner has no screen of its own");
            Assert.IsFalse(Config("tennis", "a", P("a", 0, "split"), P("b", 1)).Valid, "...and must say so");
            Assert.IsFalse(Config("tennis", "a", P("a", 0, "wide"), P("b", 1, "near")).Valid, "unknown view");
            Assert.IsFalse(Config("golf", "a", P("a", 0, "near"), P("b", 1)).Valid, "views are tennis only");
        }
        [Test] public void ALocalControllerKnowsItIsOne() {
            var c = Config("tennis", "b", P("a", 0, "split"), P("b", 1, "none"));
            Assert.AreEqual(NetworkConfiguration.ViewNone, c.LocalView);
            Assert.AreEqual(NetworkConfiguration.ViewSplit, c.ViewOf("a"));
            Assert.AreEqual("", c.ViewOf("nobody"));
        }

        [Test] public void TheNearEndIsTheOwnPlayersOnlyOnAPhoneWithItsOwnTV() {
            Assert.AreEqual(0, NetworkConfiguration.NearSide(0, "near"));
            Assert.AreEqual(1, NetworkConfiguration.NearSide(1, "near"), "each phone with a TV puts its own player in front");
            Assert.AreEqual(1, NetworkConfiguration.NearSide(1, ""), "an owner that sends no views behaves as before");
            Assert.AreEqual(0, NetworkConfiguration.NearSide(1, "split"), "a shared TV is drawn from seat 0's end whoever owns the phone");
            Assert.AreEqual(0, NetworkConfiguration.NearSide(1, "none"));
            Assert.AreEqual(0, NetworkConfiguration.NearSide(-1, "near"), "a spectator watches from seat 0's end");
            Assert.AreEqual(0, NetworkConfiguration.NearSide(-1, ""));
        }

        static string Fixture([CallerFilePath] string here = "") =>
            File.ReadAllText(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "../../../../proof/multiplayer/fixtures/tennis_one_tv_config.json")));

        // The same file is parsed by GolfArcadeTests/MultiplayerTests.swift (testTheOneTVConfigurationFixtureParsesAsTheOwnerWritesIt),
        // so a change to the fields on either side shows up as a failure here or there.
        [Test] public void TheOneTVFixtureWrittenBySwiftParsesAndIsValid() {
            var c = JsonUtility.FromJson<NetworkConfiguration>(Fixture());
            Assert.IsNotNull(c);
            Assert.IsTrue(c.Valid);
            Assert.AreEqual("tennis", c.sport);
            Assert.AreEqual("none", c.LocalView, "the local phone in the fixture is the one without a TV");
            Assert.AreEqual("split", c.ViewOf("host-phone"));
            Assert.AreEqual(new[] { 0, 1 }, c.participants.Select(p => p.seat).ToArray());
            Assert.AreEqual(1, c.Seat("guest-phone"));
        }
    }
}
