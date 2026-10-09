using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using GolfArcade.Course;
using GolfCourse = GolfArcade.Course.Course;

namespace GolfArcade.PostcardCheck
{
    /// Invariant-culture number formatting so every line is reproducible and diff-able against Python.
    static class Fmt
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string F(double v, int decimals) => v.ToString("F" + decimals, Inv);
        public static string G(double v) => v.ToString("0.###", Inv);
        public static string Pt(CoursePoint p) => $"({F(p.X, 1)}, {F(p.D, 1)})";
        public static string Bool(bool b) => b ? "True" : "False";
        public static string List(IEnumerable<double> xs, int decimals) => "[" + string.Join(", ", xs.Select(x => F(x, decimals))) + "]";
    }

    static class LieLetters
    {
        public static char Letter(CourseLie l) => l switch
        {
            CourseLie.Tee => 'T',
            CourseLie.Fairway => 'F',
            CourseLie.Rough => 'R',
            CourseLie.Bunker => 'B',
            CourseLie.Green => 'G',
            CourseLie.Water => 'W',
            _ => 'O',
        };
        public static string Legend => "T tee, F fairway, R rough, B bunker, G green, W water, O out of bounds";
    }

    /// Parsed command line: positional words, --key value / --key=value options, and boolean flags.
    sealed class Args
    {
        static readonly HashSet<string> BoolFlags = new() { "map", "ridge", "quiet", "no-run", "verbose", "fast", "all-shots", "no-ridge" };
        public readonly List<string> Positional = new();
        public readonly Dictionary<string, List<string>> Options = new(StringComparer.OrdinalIgnoreCase);

        public Args(string[] argv)
        {
            for (int i = 0; i < argv.Length; i++)
            {
                string a = argv[i];
                if (a.StartsWith("--") && a.Length > 2)
                {
                    string key = a.Substring(2), val = null;
                    int eq = key.IndexOf('=');
                    if (eq >= 0) { val = key.Substring(eq + 1); key = key.Substring(0, eq); }
                    else if (!BoolFlags.Contains(key) && i + 1 < argv.Length) val = argv[++i];
                    else val = "true";
                    if (!Options.TryGetValue(key, out var list)) Options[key] = list = new List<string>();
                    list.Add(val);
                }
                else Positional.Add(a);
            }
        }

        public string Get(string key, string dflt = null) => Options.TryGetValue(key, out var l) ? l[l.Count - 1] : dflt;
        public bool Has(string key) => Options.ContainsKey(key);
        public IEnumerable<string> All(string key) => Options.TryGetValue(key, out var l) ? l : Enumerable.Empty<string>();
        public double GetD(string key, double dflt) => Options.TryGetValue(key, out var l) ? double.Parse(l[l.Count - 1], Fmt.Inv) : dflt;
        public int GetI(string key, int dflt) => Options.TryGetValue(key, out var l) ? int.Parse(l[l.Count - 1], Fmt.Inv) : dflt;
    }

    /// Picks a course by name with reflection, so the harness builds before Course.Postcards() exists.
    static class CourseSelect
    {
        public static List<MethodInfo> Factories() => typeof(GolfCourse)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters().Length == 0 && m.ReturnType == typeof(GolfCourse))
            .OrderBy(m => m.Name, StringComparer.Ordinal).ToList();

        /// Returns the course or null (and a message) when the factory does not exist.
        public static GolfCourse Find(string name, out string error)
        {
            error = null;
            var fs = Factories();
            string avail = string.Join(", ", fs.Select(m => m.Name));
            if (string.IsNullOrEmpty(name)) { error = "no --course given; available: " + avail; return null; }
            var hit = fs.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return (GolfCourse)hit.Invoke(null, null);
            foreach (var m in fs)
            {
                var c = (GolfCourse)m.Invoke(null, null);
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
            }
            error = $"Course.{name}() does not exist in the compiled Hole.cs (available: {avail})";
            return null;
        }

        public static IEnumerable<Hole> Holes(GolfCourse c, Args a)
        {
            if (!a.Has("hole")) return c.Holes;
            var want = a.All("hole").SelectMany(s => s.Split(',')).Select(s => int.Parse(s.Trim(), Fmt.Inv)).ToHashSet();
            return c.Holes.Where(h => want.Contains(h.Number));
        }
    }

    /// Polygon, ellipse and polyline helpers. Written for this harness, not copied from the Python.
    static class Geo
    {
        public static double Cross(CoursePoint p, CoursePoint q, CoursePoint r) => (q.X - p.X) * (r.D - p.D) - (q.D - p.D) * (r.X - p.X);

        public static bool ProperCross(CoursePoint a, CoursePoint b, CoursePoint c, CoursePoint d)
        {
            double o1 = Cross(a, b, c), o2 = Cross(a, b, d), o3 = Cross(c, d, a), o4 = Cross(c, d, b);
            return o1 * o2 < 0 && o3 * o4 < 0;
        }

        public static List<(int i, int j)> SelfIntersections(CoursePoint[] poly)
        {
            var hits = new List<(int, int)>();
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % n];
                for (int j = i + 2; j < n; j++)
                {
                    if ((j + 1) % n == i) continue;
                    if (ProperCross(a, b, poly[j], poly[(j + 1) % n])) hits.Add((i, j));
                }
            }
            return hits;
        }

        public static double SignedArea(CoursePoint[] poly)
        {
            double s = 0;
            for (int i = 0; i < poly.Length; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Length];
                s += p.X * q.D - q.X * p.D;
            }
            return s / 2;
        }

        public static double DistToPolygonEdge(CoursePoint p, CoursePoint[] poly)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Length];
                double abx = b.X - a.X, abd = b.D - a.D;
                double l2 = abx * abx + abd * abd + 1e-12;
                double t = Math.Min(Math.Max(((p.X - a.X) * abx + (p.D - a.D) * abd) / l2, 0), 1);
                best = Math.Min(best, p.DistanceTo(new CoursePoint(a.X + abx * t, a.D + abd * t)));
            }
            return best;
        }

        public static CoursePoint[] EllipsePoints(CourseHazard h, int n, double grow = 0)
        {
            var pts = new CoursePoint[n];
            for (int k = 0; k < n; k++)
            {
                double a = 2 * Math.PI * k / n;
                pts[k] = new CoursePoint(h.X + (h.Width / 2 + grow) * Math.Cos(a), h.Distance + (h.Length / 2 + grow) * Math.Sin(a));
            }
            return pts;
        }

        /// Smallest distance between two axis-aligned ellipses, 0 when they overlap (boundary sampling).
        public static double EllipseGap(CourseHazard a, CourseHazard b)
        {
            var pa = EllipsePoints(a, 90); var pb = EllipsePoints(b, 90);
            if (pa.Any(b.Contains) || pb.Any(a.Contains)) return 0;
            double best = double.PositiveInfinity;
            foreach (var p in pa) foreach (var q in pb) best = Math.Min(best, p.DistanceTo(q));
            return best;
        }

        /// (arc length along the centerline, point), every `step` yards, including the pin.
        public static List<(double s, CoursePoint p)> Samples(CoursePoint[] line, double step)
        {
            var outp = new List<(double, CoursePoint)>();
            double acc = 0;
            for (int i = 1; i < line.Length; i++)
            {
                var a = line[i - 1]; var b = line[i];
                double len = a.DistanceTo(b);
                int n = Math.Max(1, (int)Math.Ceiling(len / step));
                for (int k = 0; k < n; k++)
                {
                    double t = (double)k / n;
                    outp.Add((acc + len * t, new CoursePoint(a.X + (b.X - a.X) * t, a.D + (b.D - a.D) * t)));
                }
                acc += len;
            }
            outp.Add((acc, line[line.Length - 1]));
            return outp;
        }

        /// Arc length of the centerline at the point nearest `p`.
        public static double Progress(Hole h, CoursePoint p)
        {
            double best = double.PositiveInfinity, at = 0, acc = 0;
            for (int i = 1; i < h.Centerline.Length; i++)
            {
                var a = h.Centerline[i - 1]; var b = h.Centerline[i];
                double dx = b.X - a.X, dd = b.D - a.D, l2 = Math.Max(dx * dx + dd * dd, 1e-9);
                double t = Math.Min(Math.Max(((p.X - a.X) * dx + (p.D - a.D) * dd) / l2, 0), 1);
                double dist = p.DistanceTo(new CoursePoint(a.X + dx * t, a.D + dd * t));
                double seg = a.DistanceTo(b);
                if (dist < best) { best = dist; at = acc + seg * t; }
                acc += seg;
            }
            return at;
        }

        public static (double x0, double d0, double x1, double d1) Bounds(Hole h, double pad)
        {
            var xs = new List<double>(); var ds = new List<double>();
            if (h.Shore != null) foreach (var p in h.Shore) { xs.Add(p.X); ds.Add(p.D); }
            foreach (var p in h.Centerline) { xs.Add(p.X); ds.Add(p.D); }
            foreach (var z in h.Hazards) { xs.Add(z.X - z.Width / 2); xs.Add(z.X + z.Width / 2); ds.Add(z.Distance - z.Length / 2); ds.Add(z.Distance + z.Length / 2); }
            if (h.Shore == null)
            {
                double reach = h.FairwayWidth / 2 + h.RoughWidth;
                xs.Add(xs.Min() - reach); xs.Add(xs.Max() + reach); ds.Add(ds.Min() - reach); ds.Add(ds.Max() + reach);
            }
            return (xs.Min() - pad, ds.Min() - pad, xs.Max() + pad, ds.Max() + pad);
        }
    }
}
