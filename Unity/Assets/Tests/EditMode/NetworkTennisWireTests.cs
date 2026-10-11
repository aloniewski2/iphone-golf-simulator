using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;

namespace GolfArcade.Tests {
    /// The compact tennis update must carry everything the full one does, read back the same (to the millimetre / millisecond it is
    /// rounded to), and be small enough to go as an unreliable message. Plain C#, so these also run without Unity (Tools/netsim).
    public class NetworkTennisWireTests {
        // Give every public field of an object a distinct, non-default value, so a field the wire form forgets cannot hide behind a
        // default. `flip` inverts every on/off field, so each flag is checked both set and clear across the two fills.
        static void Fill(object target, ref int seed, bool flip) {
            foreach (var f in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                seed++;
                if (f.FieldType == typeof(int)) f.SetValue(target, seed);
                else if (f.FieldType == typeof(long)) f.SetValue(target, (long)seed * 100 + 7);
                else if (f.FieldType == typeof(float)) f.SetValue(target, seed * 0.123f);
                else if (f.FieldType == typeof(double)) f.SetValue(target, seed * 1.234);
                else if (f.FieldType == typeof(bool)) f.SetValue(target, (seed % 2 == 0) != flip);
                else if (f.FieldType == typeof(string)) f.SetValue(target, f.Name == "phase" ? "rally" : "text" + seed);
                else if (f.FieldType == typeof(NetworkVector)) f.SetValue(target, new NetworkVector(seed * .11f, seed * .22f, seed * .33f));
                else if (f.FieldType == typeof(List<string>)) f.SetValue(target, new List<string> { "6–4", "3–6", "7–6" });
            }
        }
        static NetworkTennisState Filled(bool flip) {
            int seed = 0;
            var s = new NetworkTennisState();
            Fill(s, ref seed, flip);
            s.players = new[] { new NetworkTennisPlayer(), new NetworkTennisPlayer() };
            foreach (var p in s.players) Fill(p, ref seed, flip);
            object score = new TennisMatch();   // boxed, so the reflection below changes the struct that is then copied out
            Fill(score, ref seed, flip);
            s.score = (TennisMatch)score;
            return s;
        }
        static string Describe(object value) => value switch {
            null => "", float f => Math.Round(f, 3).ToString("R"), double d => Math.Round(d, 3).ToString("R"),
            NetworkVector v => $"({Describe(v.x)},{Describe(v.y)},{Describe(v.z)})",
            IEnumerable<string> list => string.Join("|", list),
            _ => value.ToString(),
        };
        static string Wire(NetworkTennisState s) => JsonUtility.ToJson(NetworkTennisWire.From(s));

        static void AssertSameFields(object expected, object actual, string where) {
            foreach (var f in expected.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType != typeof(NetworkTennisPlayer[]) && f.FieldType != typeof(TennisMatch))
                    Assert.AreEqual(Describe(f.GetValue(expected)), Describe(f.GetValue(actual)), where + "." + f.Name);
        }

        [Test] public void EveryFieldOfTheFullUpdateSurvivesTheCompactForm([Values(false, true)] bool flip) {
            var original = Filled(flip);
            var back = NetworkTennisWire.Read(Wire(original));
            Assert.IsNotNull(back);
            AssertSameFields(original, back, "state");
            for (int i = 0; i < 2; i++) AssertSameFields(original.players[i], back.players[i], "player " + i);
            object a = original.score, b = back.score;   // boxed so the same helper can read the struct's fields
            AssertSameFields(a, b, "score");
        }

        [Test] public void ARealRallyUpdateIsUnderAThirdOfTheFullSizeAndFitsTheUnreliableLimit() {
            var m = NetworkTimingTests.ServeToRally();
            string full = JsonUtility.ToJson(m.State);
            string compact = Wire(m.State);
            TestContext.Progress.WriteLine($"full {full.Length} bytes, compact {compact.Length} ({100 * compact.Length / full.Length}%)");
            TestContext.Progress.WriteLine(compact);
            Assert.Less(compact.Length, full.Length * 0.4, "well under half the size");
            Assert.Less(compact.Length, 520, "with the packet around it, well under the ~1 KB an unreliable message can safely carry");
        }

        [Test] public void ThroughoutARallyTheGuestSeesTheHostsStateToTheMillimetre() {
            var m = NetworkTimingTests.ServeToRally();
            for (int k = 1; k <= 1200; k++) {   // ten seconds at 120 Hz, an update every fourth step (30 Hz)
                m.Step(1.0 / 120);
                if (k % 4 != 0) continue;
                var s = m.State; var back = NetworkTennisWire.Read(Wire(s));
                Assert.IsNotNull(back, "step " + k);
                Assert.AreEqual(s.phase, back.phase); Assert.AreEqual(s.tick, back.tick); Assert.AreEqual(s.contact, back.contact);
                Assert.AreEqual(s.ball.x, back.ball.x, .0006f); Assert.AreEqual(s.ball.y, back.ball.y, .0006f); Assert.AreEqual(s.ball.z, back.ball.z, .0006f);
                Assert.AreEqual(s.velocity.z, back.velocity.z, .0006f);
                Assert.AreEqual(s.time, back.time, .0006);
                for (int i = 0; i < 2; i++) { Assert.AreEqual(s.players[i].x, back.players[i].x, .0006f); Assert.AreEqual(s.players[i].z, back.players[i].z, .0006f); }
                Assert.AreEqual(s.score.Scoreboard, back.score.Scoreboard);
            }
        }

        [Test] public void TheScoreRidesInEveryUpdateWithAnyNumberOfFinishedSets() {
            foreach (var sets in new[] { new string[0], new[] { "6–4" }, new[] { "6–4", "3–6", "7–6" } }) {
                var s = Filled(false); s.score.SetScores = new List<string>(sets);
                var back = NetworkTennisWire.Read(Wire(s));
                CollectionAssert.AreEqual(sets, back.score.SetScores);
            }
            var none = Filled(false); none.score.SetScores = null;
            Assert.IsNotNull(NetworkTennisWire.Read(Wire(none)).score.SetScores, "a missing list reads as an empty one");
            Assert.AreEqual(0, NetworkTennisWire.Read(Wire(none)).score.SetScores.Count);
        }

        [Test] public void EitherFormIsReadAndGarbageIsRefused() {
            var s = Filled(false);
            var full = NetworkTennisWire.Read(JsonUtility.ToJson(s));
            Assert.AreEqual(s.tick, full.tick, "the full form is still accepted");
            // The full form must carry the score too. Unity's JsonUtility leaves out a nested struct that is not [Serializable]
            // (the sandbox writer does not), so this is the check that TennisMatch has the attribute.
            Assert.AreEqual(s.score.PlayerGames, full.score.PlayerGames);
            Assert.AreEqual(s.score.PlayerPoints, full.score.PlayerPoints);
            CollectionAssert.AreEqual(s.score.SetScores, full.score.SetScores);
            Assert.AreEqual(s.tick, NetworkTennisWire.Read(Wire(s)).tick, "and so is the compact one");
            Assert.IsNull(NetworkTennisWire.Read("")); Assert.IsNull(NetworkTennisWire.Read("not json")); Assert.IsNull(NetworkTennisWire.Read(null));
            Assert.IsNull(NetworkTennisWire.Read("{\"v\":2,\"ph\":\"rally\"}"), "a compact update with no players is unusable");
            Assert.IsNull(NetworkTennisWire.Read(Wire(s).Replace("\"v\":2", "\"v\":3")), "a compact version this build does not know is refused, not misread as the full form");
        }

        [Test] public void AnUpdateWithTheWrongShapeIsRefusedWhole() {
            var w = NetworkTennisWire.From(Filled(false));
            Assert.IsNotNull(w.To());
            var shortBall = NetworkTennisWire.From(Filled(false)); shortBall.f = new int[5];
            Assert.IsNull(shortBall.To(), "the ball block is too short");
            var onePlayer = NetworkTennisWire.From(Filled(false)); onePlayer.p = new[] { onePlayer.p[0] };
            Assert.IsNull(onePlayer.To(), "both players are required");
            var holeInPlayers = NetworkTennisWire.From(Filled(false)); holeInPlayers.p[1] = null;
            Assert.IsNull(holeInPlayers.To());
            var badPlayer = NetworkTennisWire.From(Filled(false)); badPlayer.p[0].i = new int[2];
            Assert.IsNull(badPlayer.To(), "a player block of the wrong length");
            var noPhase = NetworkTennisWire.From(Filled(false)); noPhase.ph = "";
            Assert.IsNull(noPhase.To(), "an update with no phase is not a match state");
        }

        [Test] public void TheHostsSelfTestPassesWithAWorkingJsonWriterAndReader() {
            Assert.IsTrue(NetworkTennisWire.SelfTest());
        }

        // JsonUtility silently leaves out any field whose type is not [Serializable]. TennisMatch was exactly that: the full update (the
        // checkpoints, and the fallback when the compact form fails its self-test) reached the guest without its score, and the
        // guest's scoreboard sat at 0-0 for the whole match. Unity's JsonUtility is not available to the plain-.NET harness, so this
        // checks the rule itself: every type inside the full update, however deep, is [Serializable] (or a primitive, string, enum).
        [Test] public void EveryTypeInsideTheFullUpdateIsSerializableOrJsonUtilityLeavesItOut() {
            var checkedTypes = new HashSet<Type>();
            void Walk(Type type, string path) {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) {
                    var t = field.FieldType;
                    if (t.IsArray) t = t.GetElementType();
                    else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) t = t.GetGenericArguments()[0];
                    if (t.IsPrimitive || t.IsEnum || t == typeof(string)) continue;
                    Assert.IsTrue(t.IsDefined(typeof(SerializableAttribute), false),
                        path + "." + field.Name + " is a " + t.Name + " without [Serializable]: JsonUtility would leave it out of every full update");
                    if (checkedTypes.Add(t)) Walk(t, t.Name);
                }
            }
            Walk(typeof(NetworkTennisState), nameof(NetworkTennisState));
            Assert.IsTrue(checkedTypes.Contains(typeof(TennisMatch)), "the walk reached the score");
        }

        [Test] public void TheBallIsRoundedToTheMillimetreAndNoFurther() {
            var s = Filled(false); s.ball = new NetworkVector(1.23456f, 0.98765f, -11.88349f);
            var back = NetworkTennisWire.Read(Wire(s));
            Assert.AreEqual(1.235f, back.ball.x, 1e-4f); Assert.AreEqual(0.988f, back.ball.y, 1e-4f); Assert.AreEqual(-11.883f, back.ball.z, 1e-4f);
        }
    }
}
