using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// The round's card: strokes per hole against par, running totals, and the names golfers
    /// give scores. Pure C# so the arithmetic is testable without a scene.
    public sealed class Scorecard
    {
        public readonly Course Course;
        readonly int?[] strokes;

        public Scorecard(Course course)
        {
            Course = course;
            strokes = new int?[course.Holes.Length];
        }

        public int HolesPlayed { get { int n = 0; foreach (var s in strokes) if (s.HasValue) n++; return n; } }
        public bool IsComplete => HolesPlayed == strokes.Length;

        public int? StrokesOn(int holeIndex) => strokes[holeIndex];

        public void Record(int holeIndex, int holeStrokes)
        {
            if (holeStrokes < 1) throw new ArgumentOutOfRangeException(nameof(holeStrokes));
            strokes[holeIndex] = holeStrokes;
        }

        /// Strokes over the holes played so far.
        public int Total { get { int t = 0; foreach (var s in strokes) t += s ?? 0; return t; } }

        /// Over/under par for the holes played so far (par is only counted once a hole is done).
        public int ToPar
        {
            get
            {
                int t = 0;
                for (int i = 0; i < strokes.Length; i++) if (strokes[i] is int s) t += s - Course.Holes[i].Par;
                return t;
            }
        }

        public static string FormatToPar(int toPar) => toPar == 0 ? "E" : toPar > 0 ? $"+{toPar}" : toPar.ToString();

        /// "Birdie!", "Bogey", … for a hole's strokes against its par.
        public static string ScoreName(int strokes, int par)
        {
            if (strokes == 1) return "Hole in one!";
            int toPar = strokes - par;
            return toPar switch
            {
                <= -3 => "Albatross!",
                -2 => "Eagle!",
                -1 => "Birdie!",
                0 => "Par",
                1 => "Bogey",
                2 => "Double bogey",
                _ => $"+{toPar}",
            };
        }

        /// One row per hole for the end-of-round card: (hole, par, strokes or "–").
        public IEnumerable<(string hole, string par, string score)> Rows()
        {
            for (int i = 0; i < strokes.Length; i++)
                yield return ($"{Course.Holes[i].Number}", $"{Course.Holes[i].Par}", strokes[i]?.ToString() ?? "–");
        }
    }
}
