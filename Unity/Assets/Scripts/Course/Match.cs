using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// How a round with others is won.
    public enum MatchFormat
    {
        /// Fewest strokes over the round.
        StrokePlay,
        /// Hole by hole: the fewest strokes wins the hole, and the most holes won wins.
        MatchPlay,
        /// One tee shot each on the par 3s: nearest the pin wins the hole.
        ClosestToPin,
        /// One drive each on the par 4s and 5s: the longest that finishes on the fairway wins.
        LongestDrive,
    }

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
        /// A contest's measure per hole: feet from the pin, or yards driven (0 missed the
        /// fairway); null for a hole not played yet.
        public double?[] Measures;
    }

    /// A round for one or more golfers. At home the players take turns: each plays a hole out
    /// (or, in a contest, hits their one shot), then the next player tees off on it, then everyone
    /// moves on — the same order as the iOS app. Everyone on a hole gets the same wind, from the
    /// round's seed, so the same seed on two phones gives an online match the same conditions.
    /// Pure C#, tested without a scene.
    public sealed class Match
    {
        public readonly Course Course;
        public readonly IReadOnlyList<MatchPlayer> Players;
        public readonly int WindSeed;
        public readonly MatchFormat Format;

        /// A shot that misses a closest-to-the-pin green entirely (the water, out of bounds).
        public const double MissedGreen = 9999;

        public int HoleIndex { get; private set; }
        /// Index into Players of the local golfer who is up.
        public int TurnIndex { get; private set; }
        /// Every local player has finished every hole.
        public bool LocalPlayIsOver { get; private set; }

        public Match(Course course, IEnumerable<MatchPlayer> players, int windSeed, MatchFormat format = MatchFormat.StrokePlay)
        {
            Course = course ?? throw new ArgumentNullException(nameof(course));
            Format = format;
            var list = new List<MatchPlayer>();
            foreach (var p in players)
            {
                p.Card = new Scorecard(course);
                p.Measures = new double?[course.Holes.Length];
                list.Add(p);
            }
            if (list.Count == 0) throw new ArgumentException("a round needs a golfer", nameof(players));
            Players = list;
            WindSeed = windSeed;
            TurnIndex = list.FindIndex(p => p.IsLocal);
            if (TurnIndex < 0) throw new ArgumentException("a round needs a golfer on this phone", nameof(players));
        }

        /// The holes a format is played on: the par 3s for closest to the pin, the par 4s and 5s
        /// for the long drive, all of them otherwise. `course` itself when none of its holes fit.
        public static Course HolesFor(Course course, MatchFormat format)
        {
            if (format is MatchFormat.StrokePlay or MatchFormat.MatchPlay) return course;
            var holes = Array.FindAll(course.Holes, h => format == MatchFormat.ClosestToPin ? h.Par == 3 : h.Par >= 4);
            return holes.Length == 0 ? course : new Course { Name = course.Name, Holes = holes };
        }

        public static string FormatName(MatchFormat format) => format switch
        {
            MatchFormat.MatchPlay => "Match play",
            MatchFormat.ClosestToPin => "Closest to the pin",
            MatchFormat.LongestDrive => "Longest drive",
            _ => "Stroke play",
        };

        public MatchPlayer Current => Players[TurnIndex];
        public Hole Hole => Course.Holes[HoleIndex];
        public bool IsSolo => Players.Count == 1;
        /// One shot a hole, measured, rather than played out and counted.
        public bool IsContest => Format is MatchFormat.ClosestToPin or MatchFormat.LongestDrive;
        public int LocalCount { get { int n = 0; foreach (var p in Players) if (p.IsLocal) n++; return n; } }
        public bool EveryoneFinished { get { foreach (var p in Players) if (!Finished(p)) return false; return true; } }

        /// The player has played `holeIndex`: holed out, or hit their contest shot.
        public bool Played(MatchPlayer p, int holeIndex) => IsContest ? p.Measures[holeIndex].HasValue : p.Card.StrokesOn(holeIndex).HasValue;

        public bool Finished(MatchPlayer p)
        {
            for (int h = 0; h < Course.Holes.Length; h++) if (!Played(p, h)) return false;
            return true;
        }

        public int HolesPlayed(MatchPlayer p) { int n = 0; for (int h = 0; h < Course.Holes.Length; h++) if (Played(p, h)) n++; return n; }

        /// The hole's wind, the same for every player on it and on every phone with this seed.
        public Wind WindFor(int holeIndex) => Wind.Random(new Random(unchecked(WindSeed * 31 + holeIndex)));

        /// The player who is up has holed out (or picked up) in `strokes`.
        public void RecordCurrent(int strokes) => Current.Card.Record(HoleIndex, strokes);

        /// The player who is up hit their contest shot: feet from the pin, or yards driven.
        public void RecordMeasure(double value) => Current.Measures[HoleIndex] = Math.Max(0, value);

        /// A local player still has `holeIndex` to play: the phone passes to them before the card.
        public bool LocalsLeftOn(int holeIndex)
        {
            foreach (var p in Players) if (p.IsLocal && !Played(p, holeIndex)) return true;
            return false;
        }

        /// Hands the turn on: the next local player on this hole, else the first on the next
        /// hole. False once every local player has finished the round.
        public bool Advance()
        {
            if (LocalPlayIsOver) return false;
            for (int hole = HoleIndex; hole < Course.Holes.Length; hole++)
            {
                for (int i = 0; i < Players.Count; i++)
                {
                    var p = Players[i];
                    if (p.IsLocal && !Played(p, hole))
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

        // ----- Who is winning -----

        /// A hole's result for one player: strokes, or the contest's measure made comparable
        /// (lower is better in both); null before they have played it.
        double? HoleScore(MatchPlayer p, int holeIndex) => Format switch
        {
            MatchFormat.ClosestToPin => p.Measures[holeIndex],
            MatchFormat.LongestDrive => p.Measures[holeIndex] is double yards ? -yards : null,
            _ => p.Card.StrokesOn(holeIndex),
        };

        /// Who won a hole outright, once everyone has played it; null for a halved hole (and a
        /// long drive nobody kept on the fairway).
        public MatchPlayer HoleWinner(int holeIndex)
        {
            MatchPlayer best = null;
            double bestScore = double.MaxValue;
            bool tied = false;
            foreach (var p in Players)
            {
                var s = HoleScore(p, holeIndex);
                if (!s.HasValue) return null;
                if (s.Value < bestScore) { best = p; bestScore = s.Value; tied = false; }
                else if (s.Value == bestScore) tied = true;
            }
            if (Format == MatchFormat.LongestDrive && bestScore == 0) return null;
            if (Format == MatchFormat.ClosestToPin && bestScore >= MissedGreen) return null;
            return tied ? null : best;
        }

        /// Holes won outright so far.
        public int HolesWon(MatchPlayer p)
        {
            int n = 0;
            for (int h = 0; h < Course.Holes.Length; h++) if (HoleWinner(h) == p) n++;
            return n;
        }

        /// The contest's best: nearest to a pin (feet), or longest drive on the fairway (yards).
        public double? Best(MatchPlayer p)
        {
            double? best = null;
            foreach (var m in p.Measures)
            {
                if (!m.HasValue) continue;
                if (Format == MatchFormat.ClosestToPin && m.Value < MissedGreen) best = best.HasValue ? Math.Min(best.Value, m.Value) : m.Value;
                if (Format == MatchFormat.LongestDrive && m.Value > 0) best = best.HasValue ? Math.Max(best.Value, m.Value) : m.Value;
            }
            return best;
        }

        /// Leaderboard order. Stroke play: best against par, then whoever has played more. Match
        /// play and the contests: most holes won, then (contests) the best shot of the round. Ties
        /// keep the order they teed off in.
        public List<MatchPlayer> Standings()
        {
            var order = new List<MatchPlayer>(Players);
            var index = new Dictionary<MatchPlayer, int>();
            for (int i = 0; i < Players.Count; i++) index[Players[i]] = i;
            order.Sort((a, b) =>
            {
                int c = Compare(a, b);
                return c != 0 ? c : index[a].CompareTo(index[b]);
            });
            return order;
        }

        /// Negative when `a` is ahead of `b`.
        int Compare(MatchPlayer a, MatchPlayer b)
        {
            if (Format == MatchFormat.StrokePlay)
            {
                int c = a.Card.ToPar.CompareTo(b.Card.ToPar);
                return c != 0 ? c : HolesPlayed(b).CompareTo(HolesPlayed(a));
            }
            int won = HolesWon(b).CompareTo(HolesWon(a));
            if (won != 0 || !IsContest) return won;
            double? ba = Best(a), bb = Best(b);
            if (ba == bb) return 0;
            if (!ba.HasValue) return 1;
            if (!bb.HasValue) return -1;
            return Format == MatchFormat.ClosestToPin ? ba.Value.CompareTo(bb.Value) : bb.Value.CompareTo(ba.Value);
        }

        /// A player's standing in words: "+2", "2 UP", "3 holes · 6 ft".
        public string Standing(MatchPlayer p)
        {
            switch (Format)
            {
                case MatchFormat.StrokePlay:
                    return Scorecard.FormatToPar(p.Card.ToPar);
                case MatchFormat.MatchPlay:
                    if (Players.Count == 2)
                    {
                        var other = Players[0] == p ? Players[1] : Players[0];
                        int up = HolesWon(p) - HolesWon(other);
                        return up == 0 ? "All square" : up > 0 ? $"{up} UP" : $"{-up} DOWN";
                    }
                    return Holes(HolesWon(p));
                default:
                    var best = Best(p);
                    return best.HasValue ? $"{Holes(HolesWon(p))} · {Measure(best.Value)}" : Holes(HolesWon(p));
            }
        }

        static string Holes(int n) => n == 1 ? "1 hole" : $"{n} holes";

        /// A contest measure the way it is read: "6 ft", "The hole!", "285 yd", "Missed".
        public string Measure(double value) => Format switch
        {
            MatchFormat.ClosestToPin => value >= MissedGreen ? "Missed" : value < 0.5 ? "In the hole!" : $"{value:F0} ft",
            MatchFormat.LongestDrive => value <= 0 ? "Missed" : $"{value:F0} yd",
            _ => $"{value:F0}",
        };

        /// The result in words for the end-of-round card.
        public string Headline()
        {
            if (IsSolo) return Format == MatchFormat.StrokePlay ? Scorecard.FormatToPar(Players[0].Card.ToPar) : Standing(Players[0]);
            var order = Standings();
            if (Compare(order[0], order[1]) == 0)
            {
                if (Format == MatchFormat.StrokePlay) return $"Tied at {Scorecard.FormatToPar(order[0].Card.ToPar)}";
                return Format == MatchFormat.MatchPlay && Players.Count == 2 ? "All square" : "It's a tie";
            }
            var winner = order[0];
            switch (Format)
            {
                case MatchFormat.StrokePlay:
                    return $"{winner.Name} wins by {order[1].Card.ToPar - winner.Card.ToPar}";
                case MatchFormat.MatchPlay:
                    return Players.Count == 2 ? $"{winner.Name} wins {Standing(winner)}" : $"{winner.Name} wins with {Holes(HolesWon(winner))}";
                default:
                    var best = Best(winner);
                    return best.HasValue && HolesWon(winner) == HolesWon(order[1])
                        ? $"{winner.Name} wins · {Measure(best.Value)}"
                        : $"{winner.Name} wins with {Holes(HolesWon(winner))}";
            }
        }

        /// 1 won, 0 tied for first, -1 lost: for a player's match record.
        public int ResultFor(MatchPlayer player)
        {
            if (IsSolo) return 0;
            var order = Standings();
            if (Compare(player, order[0]) != 0) return -1;
            foreach (var p in order) if (p != player && Compare(p, player) == 0) return 0;
            return 1;
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
