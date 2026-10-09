using System;
using System.Collections.Generic;
using GolfArcade.Tennis;
using UnityEngine;

namespace GolfArcade.Multiplayer {
    /// The compact form of the host's tennis update, sent 30 times a second. The full form (NetworkTennisState) is about 1.2 KB of JSON
    /// because it writes every field by its long name, and with the packet around it the update is about 1.7 KB. Apple does not say how
    /// big an "unreliable" message may be (about 1 KB is the usual figure), and a message that is refused has to go as reliable, which
    /// is slower on a bad link. This form groups the numbers into short arrays and sends them as whole millimetres and milliseconds
    /// (what the game can show), with the on/off fields packed into flags: about 0.36 KB, score included. Whole numbers also keep the
    /// size the same whatever way a JSON writer prints a float. The full form is still used for reliable "checkpoint" updates and is
    /// still accepted everywhere.
    ///
    /// Every field of NetworkTennisState, NetworkTennisPlayer and TennisMatch is mapped; a test fills each one with a distinct value
    /// through reflection and checks the round trip, so a field added later fails that test until it is mapped here.
    [Serializable] public sealed class NetworkTennisWire {
        public const int Marker = 2;
        public int v;                      // Marker in a compact update; absent (so 0) in the full form
        public long[] n;                   // tick, point, contact
        public long[] d;                   // milliseconds: time, phaseAt, emoteHoldUntil, presentationUntil
        public string ph, rs, bd;          // phase, reason, boundary
        public int[] i;                    // server, receiver, bounces, winner, flags
        public int[] f;                    // ball x y z and velocity x y z (millimetres, mm/s), toss accuracy, toss roll x z (thousandths)
        public NetworkTennisWirePlayer[] p;
        public int[] q;                    // score: player points, opponent points, player games, opponent games, player sets, opponent sets, sets to win, games per set, flags
        public string qs;                  // finished sets joined with "|", "6–4|3–6"

        const int SecondServe = 1, ServeFlight = 2, Paused = 4, Complete = 8;
        const int Tiebreak = 1, PlayerServes = 2, MatchComplete = 4, PlayerWon = 8;
        const string SetSeparator = "|";   // set scores are written "6–4", so this never appears inside one

        internal static int Milli(float value) => (int)Math.Round(value * 1000f);
        internal static long Milli(double value) => (long)Math.Round(value * 1000.0);
        internal static float Unit(int milli) => milli / 1000f;
        internal static double Unit(long milli) => milli / 1000.0;

        public static NetworkTennisWire From(NetworkTennisState s) {
            var m = s.score;
            var w = new NetworkTennisWire {
                v = Marker,
                n = new[] { s.tick, s.point, s.contact },
                d = new[] { Milli(s.time), Milli(s.phaseAt), Milli(s.emoteHoldUntil), Milli(s.presentationUntil) },
                ph = s.phase, rs = s.reason, bd = s.boundary,
                i = new[] { s.server, s.receiver, s.bounces, s.winner,
                            (s.secondServe ? SecondServe : 0) | (s.serveFlight ? ServeFlight : 0) | (s.paused ? Paused : 0) | (s.complete ? Complete : 0) },
                f = new[] { Milli(s.ball.x), Milli(s.ball.y), Milli(s.ball.z), Milli(s.velocity.x), Milli(s.velocity.y), Milli(s.velocity.z),
                            Milli(s.tossAccuracy), Milli(s.tossRollX), Milli(s.tossRollZ) },
                p = new NetworkTennisWirePlayer[s.players.Length],
                q = new[] { m.PlayerPoints, m.OpponentPoints, m.PlayerGames, m.OpponentGames, m.PlayerSets, m.OpponentSets, m.SetsToWin, m.GamesPerSet,
                            (m.Tiebreak ? Tiebreak : 0) | (m.PlayerServes ? PlayerServes : 0) | (m.Complete ? MatchComplete : 0) | (m.PlayerWonMatch ? PlayerWon : 0) },
                qs = m.SetScores == null ? "" : string.Join(SetSeparator, m.SetScores),
            };
            for (int k = 0; k < s.players.Length; k++) w.p[k] = NetworkTennisWirePlayer.From(s.players[k]);
            return w;
        }

        /// Back to the full form, or null when something is missing or the wrong length.
        public NetworkTennisState To() {
            if (string.IsNullOrEmpty(ph) || n == null || n.Length != 3 || d == null || d.Length != 4 || i == null || i.Length != 5 || f == null || f.Length != 9
                || q == null || q.Length != 9 || p == null || p.Length != 2 || p[0] == null || p[1] == null) return null;
            var first = p[0].To(); var second = p[1].To();
            if (first == null || second == null) return null;
            var sets = new List<string>();
            if (!string.IsNullOrEmpty(qs)) sets.AddRange(qs.Split(SetSeparator[0]));
            return new NetworkTennisState {
                tick = n[0], point = n[1], contact = n[2], time = Unit(d[0]), phaseAt = Unit(d[1]), emoteHoldUntil = Unit(d[2]), presentationUntil = Unit(d[3]),
                phase = ph, reason = rs ?? "", boundary = string.IsNullOrEmpty(bd) ? "point" : bd, server = i[0], receiver = i[1], bounces = i[2], winner = i[3],
                secondServe = (i[4] & SecondServe) != 0, serveFlight = (i[4] & ServeFlight) != 0, paused = (i[4] & Paused) != 0, complete = (i[4] & Complete) != 0,
                ball = new NetworkVector(Unit(f[0]), Unit(f[1]), Unit(f[2])), velocity = new NetworkVector(Unit(f[3]), Unit(f[4]), Unit(f[5])),
                tossAccuracy = Unit(f[6]), tossRollX = Unit(f[7]), tossRollZ = Unit(f[8]),
                players = new[] { first, second },
                score = new TennisMatch {
                    PlayerPoints = q[0], OpponentPoints = q[1], PlayerGames = q[2], OpponentGames = q[3], PlayerSets = q[4], OpponentSets = q[5],
                    SetsToWin = q[6], GamesPerSet = q[7], SetScores = sets,
                    Tiebreak = (q[8] & Tiebreak) != 0, PlayerServes = (q[8] & PlayerServes) != 0, Complete = (q[8] & MatchComplete) != 0, PlayerWonMatch = (q[8] & PlayerWon) != 0,
                },
            };
        }

        /// True when this build's JSON writer and reader carry the compact form: a made-up update with a different value in every place is
        /// written and read back. The host runs it when a match starts and sends the full form for the whole match if it fails.
        public static bool SelfTest() {
            try {
                var sample = new NetworkTennisState {
                    tick = 123456, point = 7, contact = 3, time = 61.234, phaseAt = 60.5, phase = "rally", reason = "ok", boundary = "game",
                    server = 1, receiver = 0, bounces = 1, winner = -1, secondServe = true,
                    ball = new NetworkVector(1.234f, 0.5f, -11.8f), velocity = new NetworkVector(-3.5f, 2.25f, 30.125f), tossAccuracy = .75f,
                    players = new[] { new NetworkTennisPlayer { x = -1.5f, z = 12.2f, swings = 4, swingAt = 59.9, emotePoint = 2 },
                                      new NetworkTennisPlayer { x = 2.5f, z = -12.2f, power = .4f, dives = 1, confirmedSwing = true } },
                    score = new TennisMatch { PlayerPoints = 2, OpponentPoints = 1, PlayerGames = 3, OpponentGames = 4, SetsToWin = 2, GamesPerSet = 6,
                                              SetScores = new List<string> { "6–4" }, PlayerServes = true },
                };
                var back = Read(JsonUtility.ToJson(From(sample)));
                return back != null && back.players.Length == 2
                    && back.tick == 123456 && back.point == 7 && back.contact == 3 && back.phase == "rally" && back.server == 1 && back.receiver == 0 && back.secondServe && !back.paused
                    && Math.Abs(back.time - 61.234) < .001 && Math.Abs(back.ball.x - 1.234f) < .001f && Math.Abs(back.ball.z + 11.8f) < .001f && Math.Abs(back.velocity.z - 30.125f) < .001f
                    && Math.Abs(back.players[0].x + 1.5f) < .001f && back.players[0].swings == 4 && Math.Abs(back.players[0].swingAt - 59.9) < .001
                    && back.players[1].confirmedSwing && back.players[1].dives == 1 && Math.Abs(back.players[1].power - .4f) < .001f
                    && back.score.PlayerGames == 3 && back.score.OpponentGames == 4 && back.score.SetsToWin == 2 && back.score.PlayerServes
                    && back.score.SetScores.Count == 1 && back.score.SetScores[0] == "6–4";
            } catch (Exception) { return false; }
        }

        /// Reads either form. Null when the payload cannot be read, or is a compact version this build does not know.
        public static NetworkTennisState Read(string payload) {
            if (string.IsNullOrEmpty(payload)) return null;
            try {
                var wire = JsonUtility.FromJson<NetworkTennisWire>(payload);
                if (wire == null) return null;
                if (wire.v == Marker) return wire.To();
                if (wire.v != 0) return null;   // a newer compact form: keep the last update rather than misread this one
                return JsonUtility.FromJson<NetworkTennisState>(payload);   // the full form has no "v"
            } catch (Exception) { return null; }
        }
    }

    [Serializable] public sealed class NetworkTennisWirePlayer {
        public int[] f;        // x, z (millimetres), target, aim, depth, power, stamina (thousandths)
        public int[] i;        // swings, serves, dives, flags (confirmed swing, introduction emote done), emote sequence
        public long[] d;       // milliseconds: swingAt, diveUntil, emoteUntil
        public long ep;        // emote point
        public string ei;      // emote id

        const int Confirmed = 1, IntroEmoted = 2;

        public static NetworkTennisWirePlayer From(NetworkTennisPlayer q) => new NetworkTennisWirePlayer {
            f = new[] { NetworkTennisWire.Milli(q.x), NetworkTennisWire.Milli(q.z), NetworkTennisWire.Milli(q.target), NetworkTennisWire.Milli(q.aim),
                        NetworkTennisWire.Milli(q.depth), NetworkTennisWire.Milli(q.power), NetworkTennisWire.Milli(q.stamina) },
            i = new[] { q.swings, q.serves, q.dives, (q.confirmedSwing ? Confirmed : 0) | (q.introEmoted ? IntroEmoted : 0), q.emoteSequence },
            d = new[] { NetworkTennisWire.Milli(q.swingAt), NetworkTennisWire.Milli(q.diveUntil), NetworkTennisWire.Milli(q.emoteUntil) },
            ep = q.emotePoint, ei = q.emoteID,
        };
        public NetworkTennisPlayer To() {
            if (f == null || f.Length != 7 || i == null || i.Length != 5 || d == null || d.Length != 3) return null;
            return new NetworkTennisPlayer {
                x = NetworkTennisWire.Unit(f[0]), z = NetworkTennisWire.Unit(f[1]), target = NetworkTennisWire.Unit(f[2]), aim = NetworkTennisWire.Unit(f[3]),
                depth = NetworkTennisWire.Unit(f[4]), power = NetworkTennisWire.Unit(f[5]), stamina = NetworkTennisWire.Unit(f[6]),
                swings = i[0], serves = i[1], dives = i[2], confirmedSwing = (i[3] & Confirmed) != 0, introEmoted = (i[3] & IntroEmoted) != 0, emoteSequence = i[4],
                swingAt = NetworkTennisWire.Unit(d[0]), diveUntil = NetworkTennisWire.Unit(d[1]), emoteUntil = NetworkTennisWire.Unit(d[2]),
                emotePoint = ep, emoteID = string.IsNullOrEmpty(ei) ? null : ei,
            };
        }
    }
}
