using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// The Open: four rounds of a course against a field of touring pros, with the clubhouse
    /// leaderboard after every round. The pros' rounds are played out hole by hole from the
    /// championship's seed and their skill, so the same event always has the same field and the
    /// same scores. Public fields only, so Unity's JsonUtility can save it between sessions.
    [Serializable]
    public sealed class Championship
    {
        public const int Rounds = 4;
        /// A card not played yet.
        public const int NotPlayed = int.MinValue;

        [Serializable]
        public sealed class Entry
        {
            public string Name = "";
            /// A profile on this phone; empty for a pro.
            public string ProfileId = "";
            /// A pro's game, 0 to 100.
            public int Skill;
            /// Against par, per round (NotPlayed until then).
            public int[] RoundToPar = { NotPlayed, NotPlayed, NotPlayed, NotPlayed };

            public bool IsPlayer => ProfileId.Length > 0;
            public int RoundsPlayed { get { int n = 0; foreach (var r in RoundToPar) if (r != NotPlayed) n++; return n; } }
            public int Total { get { int t = 0; foreach (var r in RoundToPar) if (r != NotPlayed) t += r; return t; } }
        }

        public string CourseId = "cliffside";
        public string CourseName = "Cliffside";
        public int Seed;
        /// Par of each hole of the course.
        public int[] Pars = Array.Empty<int>();
        /// The round being played next, 0 to 3; Rounds once it is over.
        public int Round;
        public List<Entry> Field = new();

        public bool IsOver => Round >= Rounds;
        public string Title => $"{CourseName} Open";

        /// The touring pros: their names and how good they are.
        static readonly (string name, int skill)[] Pros =
        {
            ("Riley Park", 86), ("Jonas Berg", 80), ("Maya Okafor", 76), ("Leo Moretti", 71),
            ("Hana Sato", 67), ("Theo Grant", 62), ("Isla Novak", 57), ("Omar Haddad", 52),
            ("Nell Brooks", 46), ("Kai Lindqvist", 40), ("Rosa Vega", 34),
        };

        /// A new event for these players (name, profile id) on a course with these pars.
        public static Championship Start(IEnumerable<(string name, string profileId)> players, string courseId, string courseName, int[] pars, int seed)
        {
            var c = new Championship { CourseId = courseId, CourseName = courseName, Pars = (int[])pars.Clone(), Seed = seed };
            foreach (var (name, id) in players) c.Field.Add(new Entry { Name = name, ProfileId = id });
            if (c.Field.Count == 0) throw new ArgumentException("the Open needs a player", nameof(players));
            foreach (var (name, skill) in Pros) c.Field.Add(new Entry { Name = name, Skill = skill });
            return c;
        }

        /// The seed for a round's winds, so every player in it faces the same conditions.
        public int RoundSeed => unchecked(Seed * 7919 + Round);

        /// A player on this phone finished the round. Once every player has, the pros play
        /// theirs and the event moves on to the next round. False if nothing changed.
        public bool RecordRound(string profileId, int toPar)
        {
            if (IsOver) return false;
            var entry = Field.Find(e => e.ProfileId == profileId && e.IsPlayer);
            if (entry == null || entry.RoundToPar[Round] != NotPlayed) return false;
            entry.RoundToPar[Round] = toPar;
            if (Field.Exists(e => e.IsPlayer && e.RoundToPar[Round] == NotPlayed)) return true;
            for (int i = 0; i < Field.Count; i++)
                if (!Field[i].IsPlayer) Field[i].RoundToPar[Round] = ProRound(i);
            Round++;
            return true;
        }

        /// A pro's round, hole by hole: the better the pro, the more birdies and the fewer bogeys.
        int ProRound(int entryIndex)
        {
            var rng = new Random(unchecked(Seed * 131 + Round * 17 + entryIndex));
            double s = Field[entryIndex].Skill / 100.0;
            int toPar = 0;
            foreach (var par in Pars)
            {
                double r = rng.NextDouble();
                double eagle = par == 5 ? 0.02 + 0.04 * s : 0.004 * s;
                double birdie = 0.07 + 0.26 * s;
                double bogey = 0.30 - 0.20 * s;
                double dbl = 0.09 - 0.08 * s;
                if (r < eagle) toPar -= 2;
                else if (r < eagle + birdie) toPar -= 1;
                else if (r < eagle + birdie + bogey) toPar += 1;
                else if (r < eagle + birdie + bogey + dbl) toPar += 2;
            }
            return toPar;
        }

        /// The clubhouse leaderboard: lowest total first, then the one who has played more,
        /// then the order they entered.
        public List<Entry> Leaderboard()
        {
            var order = new List<Entry>(Field);
            var index = new Dictionary<Entry, int>();
            for (int i = 0; i < Field.Count; i++) index[Field[i]] = i;
            order.Sort((a, b) =>
            {
                int c = a.Total.CompareTo(b.Total);
                if (c == 0) c = b.RoundsPlayed.CompareTo(a.RoundsPlayed);
                return c != 0 ? c : index[a].CompareTo(index[b]);
            });
            return order;
        }

        /// "1", "T3": the entry's place on the leaderboard, ties sharing it.
        public string Position(Entry entry)
        {
            var board = Leaderboard();
            int place = 1 + board.FindIndex(e => e.Total == entry.Total && e.RoundsPlayed == entry.RoundsPlayed);
            int same = board.FindAll(e => e.Total == entry.Total && e.RoundsPlayed == entry.RoundsPlayed).Count;
            return same > 1 ? $"T{place}" : place.ToString();
        }

        public Entry EntryFor(string profileId) => Field.Find(e => e.IsPlayer && e.ProfileId == profileId);

        /// "Riley Park wins the Cliffside Open at -9" once it is over; before that, who leads.
        public string Headline()
        {
            var lead = Leaderboard()[0];
            return IsOver ? $"{lead.Name} wins the {Title} at {Scorecard.FormatToPar(lead.Total)}"
                          : $"{lead.Name} leads at {Scorecard.FormatToPar(lead.Total)}";
        }
    }
}
