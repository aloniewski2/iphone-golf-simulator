using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Course;
using GolfCourse = GolfArcade.Course.Course;

namespace GolfArcade.PostcardCheck
{
    /// `report`, `lies` and `probe`: what the REAL Hole.cs says about a course.
    static class Report
    {
        // ------------------------------------------------------------------ report
        public static int Run(GolfCourse course, Args a)
        {
            var o = Console.Out;
            o.WriteLine($"COURSE {course.Name}: {course.Holes.Length} hole(s), par {course.Par}");
            foreach (var h in CourseSelect.Holes(course, a))
            {
                double len = h.Length;
                o.WriteLine($"HOLE {h.Number} par {h.Par} length {Fmt.F(len, 1)} yd");
                o.WriteLine($"  centerline ({h.Centerline.Length} stations): " + string.Join(" ", h.Centerline.Select(Fmt.Pt)));
                o.WriteLine("  station lies: " + string.Join(" ", h.Centerline.Select(p => h.LieAt(p).ToString())));
                var legs = new List<string>();
                for (int i = 1; i < h.Centerline.Length; i++) legs.Add($"{Fmt.F(h.Centerline[i - 1].DistanceTo(h.Centerline[i]), 1)}@{Fmt.F(h.Centerline[i - 1].HeadingTo(h.Centerline[i]), 1)}deg");
                o.WriteLine("  legs (length@heading): " + string.Join(" ", legs));
                o.WriteLine($"  fairway width {Fmt.G(h.FairwayWidth)} yd (half {Fmt.G(h.FairwayWidth / 2)}), green radius {Fmt.G(h.GreenRadius)} yd, rough width {Fmt.G(h.RoughWidth)} yd -> rough out to {Fmt.G(h.FairwayWidth / 2 + h.RoughWidth)} yd from the centerline, beyond = out of bounds");
                if (h.Shore == null) o.WriteLine("  shore: none (inland hole, the tree line is the only edge)");
                else
                {
                    var xs = h.Shore.Select(p => p.X); var ds = h.Shore.Select(p => p.D);
                    int hits = Geo.SelfIntersections(h.Shore).Count;
                    double minEdge = Enumerable.Range(0, h.Shore.Length).Min(i => h.Shore[i].DistanceTo(h.Shore[(i + 1) % h.Shore.Length]));
                    double maxEdge = Enumerable.Range(0, h.Shore.Length).Max(i => h.Shore[i].DistanceTo(h.Shore[(i + 1) % h.Shore.Length]));
                    o.WriteLine($"  shore: {h.Shore.Length} points, area {Fmt.F(Math.Abs(Geo.SignedArea(h.Shore)), 0)} yd2, x [{Fmt.F(xs.Min(), 1)}, {Fmt.F(xs.Max(), 1)}] d [{Fmt.F(ds.Min(), 1)}, {Fmt.F(ds.Max(), 1)}], edges {Fmt.F(minEdge, 1)}..{Fmt.F(maxEdge, 1)} yd, self-intersections {hits}");
                }
                int nw = h.Hazards.Count(z => z.Kind == HazardKind.Water), nb = h.Hazards.Count(z => z.Kind == HazardKind.Bunker);
                o.WriteLine($"  hazards: {nw} water + {nb} bunker");
                for (int i = 0; i < h.Hazards.Length; i++)
                {
                    var z = h.Hazards[i];
                    o.WriteLine($"    [{i}] {z.Kind,-6} centre ({Fmt.F(z.X, 1)}, {Fmt.F(z.Distance, 1)}) width {Fmt.F(z.Width, 1)} (X) x length {Fmt.F(z.Length, 1)} (D), centre lie {h.LieAt(new CoursePoint(z.X, z.Distance))}, centre on land {h.OnLand(new CoursePoint(z.X, z.Distance))}");
                }
                // water crossings along the centerline = the carries
                var runs = new List<(double a, double b)>();
                double? start = null, last = 0;
                foreach (var (s, p) in Geo.Samples(h.Centerline, 0.25))
                {
                    bool w = h.LieAt(p) == CourseLie.Water;
                    if (w && start == null) start = s;
                    if (!w && start != null) { runs.Add((start.Value, last.Value)); start = null; }
                    last = s;
                }
                if (start != null) runs.Add((start.Value, last.Value));
                o.WriteLine("  centerline water runs (carries, yd, from arc length a to b): " + (runs.Count == 0 ? "none" : string.Join(", ", runs.Select(r => $"{Fmt.F(r.b - r.a + 0.25, 1)} [{Fmt.F(r.a, 1)}..{Fmt.F(r.b, 1)}]"))));
                o.WriteLine($"  rules in this build: rough x{CourseLie.Rough.PowerFactor()}, bunker x{CourseLie.Bunker.PowerFactor()}, water +{CourseLie.Water.PenaltyStrokes()} stroke, out of bounds +{CourseLie.OutOfBounds.PenaltyStrokes()} stroke, cup capture radius {Hole.CupCaptureRadius} yd");
            }
            return 0;
        }

        // ------------------------------------------------------------------ lies
        /// Grid of every `step` yards over the hole (bounding box of shore, centerline and hazards, padded 30 yd):
        /// the point of cell (i, j) is (x0 + i*step, d0 + j*step). One row of letters per j, d ascending.
        public static int Lies(GolfCourse course, Args a)
        {
            string pointsFile = a.Get("points");
            var o = Console.Out;
            if (pointsFile != null)
            {
                var hole = CourseSelect.Holes(course, a).First();
                var sb = new StringBuilder();
                sb.Append($"# POINTS hole {hole.Number}\n");
                foreach (var line in pointsFile == "-" ? ReadAll(Console.In) : File.ReadLines(pointsFile))
                {
                    var t = line.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length < 2 || line.StartsWith("#")) continue;
                    sb.Append(LieLetters.Letter(hole.LieAt(new CoursePoint(double.Parse(t[0], Fmt.Inv), double.Parse(t[1], Fmt.Inv))))).Append('\n');
                }
                o.Write(sb.ToString());
                return 0;
            }
            double step = a.GetD("step", 2);
            if (step <= 0) { Console.Error.WriteLine("--step must be > 0"); return 2; }
            foreach (var h in CourseSelect.Holes(course, a))
            {
                var (bx0, bd0, bx1, bd1) = Geo.Bounds(h, 30);
                double x0 = Math.Floor(bx0 / step) * step, d0 = Math.Floor(bd0 / step) * step;
                int nx = (int)Math.Floor((bx1 - x0) / step) + 1, nd = (int)Math.Floor((bd1 - d0) / step) + 1;
                var sb = new StringBuilder();
                sb.Append($"# HOLE {h.Number} step {Fmt.G(step)} x0 {Fmt.G(x0)} d0 {Fmt.G(d0)} nx {nx} nd {nd}  ({LieLetters.Legend}; row j is d = d0 + j*step, column i is x = x0 + i*step)\n");
                for (int j = 0; j < nd; j++)
                {
                    for (int i = 0; i < nx; i++) sb.Append(LieLetters.Letter(h.LieAt(new CoursePoint(x0 + i * step, d0 + j * step))));
                    sb.Append('\n');
                }
                o.Write(sb.ToString());
            }
            return 0;
        }

        static IEnumerable<string> ReadAll(TextReader r) { string s; while ((s = r.ReadLine()) != null) yield return s; }

        // ------------------------------------------------------------------ probe
        /// Every sub-expression of Hole.LieAt at one point, to find which one differs from a mirror.
        public static int Probe(GolfCourse course, Args a)
        {
            var o = Console.Out;
            foreach (var h in CourseSelect.Holes(course, a))
            {
                foreach (var at in a.All("at"))
                {
                    var t = at.Split(',');
                    var p = new CoursePoint(double.Parse(t[0], Fmt.Inv), double.Parse(t[1], Fmt.Inv));
                    o.WriteLine($"PROBE hole {h.Number} at ({p.X:R}, {p.D:R}): LieAt = {h.LieAt(p)}");
                    for (int i = 0; i < h.Hazards.Length; i++)
                    {
                        var z = h.Hazards[i];
                        double dx = (p.X - z.X) / Math.Max(z.Width / 2, 0.001), dz = (p.D - z.Distance) / Math.Max(z.Length / 2, 0.001);
                        o.WriteLine($"  hazard[{i}] {z.Kind}: ((x-X)/(W/2))^2 + ((d-D)/(L/2))^2 = {dx * dx + dz * dz:R} (<= 1 is inside) -> Contains {z.Contains(p)}");
                    }
                    o.WriteLine($"  OnLand (even-odd shore test) = {h.OnLand(p)}");
                    o.WriteLine($"  |p-pin| = {p.DistanceTo(h.Pin):R}, GreenRadius = {h.GreenRadius:R}, green = {p.DistanceTo(h.Pin) <= h.GreenRadius}");
                    o.WriteLine($"  |p-tee| = {p.DistanceTo(h.Tee):R}, tee = {p.DistanceTo(h.Tee) <= 4}");
                    double off = h.DistanceFromCenterline(p);
                    o.WriteLine($"  centerline offset = {off:R}; fairway <= {h.FairwayWidth / 2:R}: {off <= h.FairwayWidth / 2}; rough <= {h.FairwayWidth / 2 + h.RoughWidth:R}: {off <= h.FairwayWidth / 2 + h.RoughWidth}");
                }
            }
            return 0;
        }
    }
}
