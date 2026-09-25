using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// One golfer in a round: who they are, how they look (GolferStyle's golfer, kit and shirt), and
    /// their card. Local players take turns on this phone; remote players are on their own phones
    /// and their cards fill in from the network.
    public sealed class MatchPlayer
    {
        public string Name;
        public int Body;
        public int Kit;
        public int Shirt;
        /// The PlayerProfile on this phone (local players).
        public string ProfileId = "";
        /// The player's id on the game server (online rounds).
        public string RemoteId = "";
        public bool IsLocal = true;
        public Scorecard Card;
    }

    /// A round of stroke play for one or more golfers. At home the players alternate shots: the
    /// first tees off, then the second, then the first plays their second shot, and so on round
    /// (PassTurn); whoever has holed out drops out of the order, and once everyone is down the
    /// round moves to the next hole with the first player up (Advance). Everyone on a hole gets
    /// the same wind, from the round's seed, so the same seed on two phones gives an online match
    /// the same conditions. Pure C#, tested without a scene.
    public sealed class Match
    {
        public readonly Course Course;
        public readonly IReadOnlyList<MatchPlayer> Players;
        public readonly int WindSeed;

        public int HoleIndex { get; private set; }
        /// Index into Players of the local golfer who is up.
        public int TurnIndex { get; private set; }
        /// Every local player has holed out on every hole.
        public bool LocalPlayIsOver { get; private set; }

        public Match(Course course, IEnumerable<MatchPlayer> players, int windSeed)
        {
            Course = course ?? throw new ArgumentNullException(nameof(course));
            var list = new List<MatchPlayer>();
            foreach (var p in players)
            {
                p.Card = new Scorecard(course);
                list.Add(p);
            }
            if (list.Count == 0) throw new ArgumentException("a round needs a golfer", nameof(players));
            Players = list;
            WindSeed = windSeed;
            TurnIndex = list.FindIndex(p => p.IsLocal);
            if (TurnIndex < 0) throw new ArgumentException("a round needs a golfer on this phone", nameof(players));
        }

        public MatchPlayer Current => Players[TurnIndex];
        public Hole Hole => Course.Holes[HoleIndex];
        public bool IsSolo => Players.Count == 1;
        public int LocalCount { get { int n = 0; foreach (var p in Players) if (p.IsLocal) n++; return n; } }
        public bool EveryoneFinished { get { foreach (var p in Players) if (!p.Card.IsComplete) return false; return true; } }

        /// The hole's wind, the same for every player on it and on every phone with this seed.
        public Wind WindFor(int holeIndex) => Wind.Random(new Random(unchecked(WindSeed * 31 + holeIndex)));

        /// The player who is up has holed out (or picked up) in `strokes`.
        public void RecordCurrent(int strokes) => Current.Card.Record(HoleIndex, strokes);

        /// A local player still has `holeIndex` to play: the phone passes to them before the card.
        public bool LocalsLeftOn(int holeIndex)
        {
            foreach (var p in Players) if (p.IsLocal && !p.Card.StrokesOn(holeIndex).HasValue) return true;
            return false;
        }

        /// The first local player with a hole still to play, on the first such hole: who tees off
        /// when the round moves on. False once every local player has finished the round.
        public bool Advance()
        {
            if (LocalPlayIsOver) return false;
            for (int hole = HoleIndex; hole < Course.Holes.Length; hole++)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    var p = Players[i];
                    if (p.IsLocal && !p.Card.StrokesOn(hole).HasValue)
                    {
                        HoleIndex = hole;
                        TurnIndex = i;
                        return true;
                    }
                }
            }
            LocalPlayIsOver = true;
            return false;
        }

        /// Alternate shots on this phone: the turn passes to the next local player in tee order who
        /// hasn't holed out on this hole — P1, P2, P1, P2… — skipping anyone who has, and staying
        /// with the last one left. False once every local player has holed out.
        public bool PassTurn()
        {
            for (int k = 1; k <= Players.Count; k++)
            {
                int i = (TurnIndex + k) % Players.Count;
                if (Players[i].IsLocal && !Players[i].Card.StrokesOn(HoleIndex).HasValue) { TurnIndex = i; return true; }
            }
            return false;
        }

        /// A remote player's hole from the network. Ignores anything out of range or a hole
        /// already on their card, so a repeated message cannot change a score.
        public bool RecordRemote(string remoteId, int holeIndex, int strokes)
        {
            if (holeIndex < 0 || holeIndex >= Course.Holes.Length || strokes < 1) return false;
            foreach (var p in Players)
            {
                if (p.IsLocal || p.RemoteId != remoteId) continue;
                if (p.Card.StrokesOn(holeIndex).HasValue) return false;
                p.Card.Record(holeIndex, strokes);
                return true;
            }
            return false;
        }

        /// Leaderboard order: best against par over the holes played, then whoever has played
        /// more, then the order they teed off in.
        public List<MatchPlayer> Standings()
        {
            var order = new List<MatchPlayer>(Players);
            var index = new Dictionary<MatchPlayer, int>();
            for (int i = 0; i < Players.Count; i++) index[Players[i]] = i;
            order.Sort((a, b) =>
            {
                int c = a.Card.ToPar.CompareTo(b.Card.ToPar);
                if (c == 0) c = b.Card.HolesPlayed.CompareTo(a.Card.HolesPlayed);
                return c != 0 ? c : index[a].CompareTo(index[b]);
            });
            return order;
        }

        /// Who won a hole outright, once everyone has played it; null for a halved hole.
        public MatchPlayer HoleWinner(int holeIndex)
        {
            MatchPlayer best = null;
            int bestStrokes = int.MaxValue;
            bool tied = false;
            foreach (var p in Players)
            {
                var s = p.Card.StrokesOn(holeIndex);
                if (!s.HasValue) return null;
                if (s.Value < bestStrokes) { best = p; bestStrokes = s.Value; tied = false; }
                else if (s.Value == bestStrokes) tied = true;
            }
            return tied ? null : best;
        }

        /// The result in words for the end-of-round card.
        public string Headline()
        {
            if (IsSolo) return Scorecard.FormatToPar(Players[0].Card.ToPar);
            var order = Standings();
            int lead = order[0].Card.ToPar;
            int tied = 0;
            foreach (var p in order) if (p.Card.ToPar == lead) tied++;
            if (tied > 1) return $"Tied at {Scorecard.FormatToPar(lead)}";
            int margin = order[1].Card.ToPar - lead;
            return $"{order[0].Name} wins by {margin}";
        }

        /// The other players' results against par, for a player's match record.
        public List<int> OpponentsToPar(MatchPlayer player)
        {
            var list = new List<int>();
            foreach (var p in Players) if (p != player && p.Card.IsComplete) list.Add(p.Card.ToPar);
            return list;
        }
    }
}
