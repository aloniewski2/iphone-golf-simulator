using System;
using System.Collections.Generic;
using GolfArcade.Course;

namespace GolfArcade.Profile
{
    /// Someone who plays on this phone: their name, which golfer they play as (the male or female
    /// Higgsfield golfer, and the kit and shirt colours — GolferStyle's choices), and their record.
    /// Public fields and no nullables so Unity's JsonUtility can save it as it is.
    [Serializable]
    public sealed class PlayerProfile
    {
        public const int MaxNameLength = 16;

        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Player 1";
        /// 0 male, 1 female: GolferStyle.BodyKind.
        public int Body;
        /// Indexes into GolferStyle.KitColors and GolferStyle.ShirtColors (0 is the kit as it comes).
        public int Kit;
        public int Shirt;

        /// The rest of how the golfer looks (skin, hair, headwear, colours): CharacterLook. Version 0 until made (an older
        /// save: ProfileStore fills it from Body, Kit and Shirt above, which are kept in step for the server).
        public CharacterLook Look = CharacterLook.Unset();

        /// The look, made from Body, Kit and Shirt first if it hasn't been made yet (a profile built in code, or an older save).
        public CharacterLook LookOrMigrated()
        {
            Look ??= CharacterLook.Unset();
            if (Look.Version == 0) Look.MigrateFrom(Body, Kit, Shirt);
            return Look;
        }

        public const int Colours = 6;

        /// The account on the game server, once this profile has signed in there.
        public string ServerId = "";
        public string ServerToken = "";

        public ProfileStats Stats = new();

        /// What they play with, from what they have earned (Unlocks): the ball, the trail behind
        /// it, the club finish. Ids from Unlocks.All; Unlocks.Chosen falls back to the free one.
        public string Ball = "ball.white";
        public string Trail = "trail.none";
        public string Club = "club.classic";
        /// Rewards they have been told they earned, so each is announced once.
        public List<string> Announced = new();
        /// Everything open to them, whatever their record (Unlocks.GrantAll).
        public bool AllUnlocked;

        public bool IsSignedIn => !string.IsNullOrEmpty(ServerId) && !string.IsNullOrEmpty(ServerToken);

        /// Trimmed, single-line, at most MaxNameLength characters; blank keeps the old name.
        public void Rename(string name)
        {
            var clean = CleanName(name);
            if (clean.Length > 0) Name = clean;
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var chars = new List<char>();
            foreach (var c in name.Trim()) if (!char.IsControl(c)) chars.Add(c);
            var clean = new string(chars.ToArray()).Trim();
            return clean.Length > MaxNameLength ? clean.Substring(0, MaxNameLength).TrimEnd() : clean;
        }
    }

    /// A profile's record, updated after every finished round.
    [Serializable]
    public sealed class ProfileStats
    {
        public int RoundsPlayed;
        public int HolesPlayed;
        public int TotalStrokes;
        /// Par over the same holes, for the average against par.
        public int TotalPar;
        /// Best round against par; only meaningful when HasBest.
        public int BestToPar;
        public bool HasBest;
        public int HolesInOne;
        public int Eagles;
        public int Birdies;
        public int Pars;
        /// Rounds against other players: won outright, tied for first, or lost.
        public int MatchesWon;
        public int MatchesTied;
        public int MatchesLost;
        /// Their longest drive on the fairway, yards (solo rounds).
        public int LongestDrive;
        /// Each course's full rounds: how many, and the best against par.
        public List<CourseBest> Courses = new();

        public int MatchesPlayed => MatchesWon + MatchesTied + MatchesLost;

        /// A full round of a course (by its key, "cliffside") finished at `toPar`.
        public void RecordCourse(string courseKey, int toPar)
        {
            if (string.IsNullOrEmpty(courseKey)) return;
            var c = Courses.Find(x => x.Course == courseKey);
            if (c == null) { Courses.Add(new CourseBest { Course = courseKey, Rounds = 1, BestToPar = toPar }); return; }
            c.Rounds++;
            c.BestToPar = Math.Min(c.BestToPar, toPar);
        }

        /// The best full round of a course against par; null before the first.
        public int? BestOn(string courseKey) => Courses.Find(x => x.Course == courseKey) is CourseBest c && c.Rounds > 0 ? c.BestToPar : null;

        public int RoundsOn(string courseKey) => Courses.Find(x => x.Course == courseKey)?.Rounds ?? 0;

        /// Average strokes against par per hole; 0 before the first hole.
        public double AverageToParPerHole => HolesPlayed == 0 ? 0 : (double)(TotalStrokes - TotalPar) / HolesPlayed;

        /// Counts a finished card. `opponents` are the other players' results in the same
        /// round: an empty list is a solo round and counts toward no match record.
        public void RecordRound(Scorecard card, IReadOnlyList<int> opponentsToPar = null)
        {
            if (card == null || !card.IsComplete) return;
            RecordCard(card);
            if (opponentsToPar == null || opponentsToPar.Count == 0) return;
            int best = int.MaxValue;
            foreach (var o in opponentsToPar) best = Math.Min(best, o);
            RecordResult(card.ToPar < best ? 1 : card.ToPar == best ? 0 : -1);
        }

        /// A match against others: 1 won, 0 tied for first, -1 lost.
        public void RecordResult(int result)
        {
            if (result > 0) MatchesWon++;
            else if (result == 0) MatchesTied++;
            else MatchesLost++;
        }

        /// A finished card's holes and best round, without a match result.
        public void RecordCard(Scorecard card)
        {
            if (card == null || !card.IsComplete) return;
            RoundsPlayed++;
            for (int i = 0; i < card.Course.Holes.Length; i++)
            {
                int strokes = card.StrokesOn(i) ?? 0;
                int par = card.Course.Holes[i].Par;
                HolesPlayed++;
                TotalStrokes += strokes;
                TotalPar += par;
                if (strokes == 1) HolesInOne++;
                else if (strokes - par <= -2) Eagles++;
                else if (strokes - par == -1) Birdies++;
                else if (strokes == par) Pars++;
            }
            if (!HasBest || card.ToPar < BestToPar) { BestToPar = card.ToPar; HasBest = true; }
        }
    }

    [Serializable]
    public sealed class CourseBest
    {
        public string Course = "";
        public int Rounds;
        public int BestToPar;
    }
}
