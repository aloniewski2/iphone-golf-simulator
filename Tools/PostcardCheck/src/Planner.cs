using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GolfArcade.Shot;
using GolfArcade.Swing;
using GolfArcade.Course;
using GolfCourse = GolfArcade.Course.Course;

namespace GolfArcade.PostcardCheck
{
    /// One stroke the planner played with the REAL CourseShot.
    struct ShotRec
    {
        public GolfClub Club; public double Power, Heading;
        public CoursePoint From; public CourseLie FromLie;
        public CoursePoint Touchdown, Rest, Next; public CourseLie Lie;
        public int Penalty; public double Carry, Total; public bool Holed;
    }

    sealed class PlanNode
    {
        public int Id, Parent, Cost;
        public CoursePoint Pos;
        public ShotRec Via;
    }

    sealed class PlanOptions
    {
        public double Cell = 2.0;
        public double[] Powers = Enumerable.Range(0, 15).Select(i => Math.Round(0.30 + 0.05 * i, 9)).ToArray();   // 0.30 .. 1.00 step 0.05
        public double HeadingStep = 3.0, HeadingSpan = 30.0;
        public GolfClub[] Clubs = { GolfClub.Driver, GolfClub.Iron, GolfClub.Wedge };
        public Wind Wind = Wind.Calm;
        public int Threads = Environment.ProcessorCount;
        public double MaxTotalYd;            // longest single stroke the clubs can play (measured), used only to prune hopeless states
        public bool Quiet;
    }

    sealed class SearchResult
    {
        public bool Found, Incomplete;
        public int Strokes;                  // strokes to reach the green, penalties included
        public int Checked;                  // every finish of up to this many strokes was tested (the search is exhaustive up to here, 2 yd cells)
        public List<ShotRec> Shots = new();
        public long ShotsEvaluated; public int States; public double Seconds;
        public string Note = "";
    }

    /// Deterministic shot planner: breadth-first (Dijkstra with integer costs) over ball positions quantised to 2 yd cells,
    /// every transition played with the real CourseShot (clubs Driver/Iron/Wedge, power 0.30..1.00 step 0.05, SwingImpact built the way
    /// GolfGame.NativeSwing builds it, lieFactor = LieAt(origin).PowerFactor()), cost = 1 per stroke + shot.PenaltyStrokes.
    ///
    /// Headings: the 3 degree grid anchored on the bearing from the ball to the pin, kept wherever it is within +-30 degrees of the bearing to the pin
    /// or to any centerline station still ahead of the ball (arc length >= ball's - 25 yd, >= 10 yd away) = "spanning" every station and the pin.
    /// Last-stroke test: with calm air the ball flies and rolls on one straight line, so a shot can end on the green only if that line crosses
    /// the green disc; the success test therefore tries just the headings within asin(GreenRadius / distance) + 0.6 degrees of the pin (exact, not a heuristic).
    static class Planner
    {
        // ---------------------------------------------------------------- calibration
        public static double MeasureMaxTotal(Wind wind)
        {
            var flat = new Hole
            {
                Number = 0, Par = 4, Centerline = new[] { new CoursePoint(0, 0), new CoursePoint(0, 3000) }, FairwayWidth = 6000, GreenRadius = 1,
            };
            double best = 0;
            foreach (var club in new[] { GolfClub.Driver, GolfClub.Iron, GolfClub.Wedge })
            {
                var s = new CourseShot(club, Impact(1), 0, flat.Tee, flat, 1, wind);
                best = Math.Max(best, s.Total);
            }
            return best;
        }

        public static SwingImpact Impact(double power) => new() { Power = power, Backswing = power, PeakSpeed = power * 16, TempoSeconds = .7 };

        // ---------------------------------------------------------------- headings
        static double Wrap(double deg) { deg %= 360; if (deg > 180) deg -= 360; if (deg <= -180) deg += 360; return deg; }

        public static double[] Headings(Hole hole, CoursePoint pos, PlanOptions o, bool successOnly)
        {
            double bPin = pos.HeadingTo(hole.Pin), dPin = pos.DistanceTo(hole.Pin);
            double step = o.HeadingStep;
            var set = new SortedSet<int>();
            bool calm = o.Wind.IsCalm;
            if (successOnly && calm)
            {
                if (dPin <= hole.GreenRadius) return Array.Empty<double>();
                double half = Math.Asin(Math.Min(1, hole.GreenRadius / dPin)) * 180 / Math.PI + 0.6;
                int kk = (int)Math.Floor(half / step);
                for (int k = -kk; k <= kk; k++) set.Add(k);
            }
            else
            {
                var bearings = new List<double> { bPin };
                double prog = Geo.Progress(hole, pos);
                double acc = 0;
                for (int i = 1; i < hole.Centerline.Length - 1; i++)
                {
                    acc += hole.Centerline[i - 1].DistanceTo(hole.Centerline[i]);
                    if (acc >= prog - 25 && pos.DistanceTo(hole.Centerline[i]) >= 10) bearings.Add(pos.HeadingTo(hole.Centerline[i]));
                }
                foreach (double b in bearings)
                {
                    double rel = Wrap(b - bPin);
                    int lo = (int)Math.Ceiling((rel - o.HeadingSpan) / step - 1e-9), hi = (int)Math.Floor((rel + o.HeadingSpan) / step + 1e-9);
                    for (int k = lo; k <= hi; k++) set.Add(k);
                }
                if (calm && dPin > hole.GreenRadius)
                {
                    double half = Math.Asin(Math.Min(1, hole.GreenRadius / dPin)) * 180 / Math.PI + 0.6;
                    int kk = (int)Math.Floor(half / step);
                    for (int k = -kk; k <= kk; k++) set.Add(k);
                }
            }
            return set.Select(k => bPin + k * step).ToArray();
        }

        static long Key(CoursePoint p, double cell) => ((long)(int)Math.Round(p.X / cell) << 32) ^ (uint)(int)Math.Round(p.D / cell);

        // ---------------------------------------------------------------- the search
        public delegate bool Accept(Hole hole, CourseShot shot, CoursePoint touchdown);

        /// Fewest strokes (penalties counted) to finish a shot on the green, at most `maxStrokes`; null accept = any shot.
        public static SearchResult Search(Hole hole, PlanOptions o, bool allowPenalty, int maxStrokes, Accept accept, Stopwatch clock, double deadline, string label)
        {
            var sw = Stopwatch.StartNew();
            var res = new SearchResult();
            var nodes = new List<PlanNode> { new PlanNode { Id = 0, Parent = -1, Cost = 0, Pos = hole.Tee } };
            var best = new Dictionary<long, int> { [Key(hole.Tee, o.Cell)] = 0 };
            var buckets = new List<List<int>> { new List<int> { 0 } };
            long shots = 0;
            var po = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, o.Threads) };
            const int batch = 40;                       // fixed, so the result and the counts do not depend on the thread count
            double reachMargin = (o.Wind.IsCalm ? 1.25 : 1.9);
            double reach(int remaining) => remaining * o.MaxTotalYd * reachMargin + 30 + hole.GreenRadius;

            for (int c = 0; c < maxStrokes; c++)
            {
                if (c >= buckets.Count) break;
                var ids = buckets[c].Where(id => nodes[id].Cost == c && best[Key(nodes[id].Pos, o.Cell)] == id).ToList();
                if (ids.Count == 0) { if (buckets.Skip(c + 1).All(b => b.Count == 0)) { res.Checked = maxStrokes; break; } res.Checked = c + 1; continue; }

                // ---- phase A: can any node of this cost finish on the green with its next stroke?
                int stroke = c + 1;
                // nearest the pin first: the longest drives that still leave a finish are tried (and reported) first
                var live = ids.Where(id => nodes[id].Pos.DistanceTo(hole.Pin) <= reach(1)).OrderBy(id => nodes[id].Pos.DistanceTo(hole.Pin)).ThenBy(id => id).ToList();
                for (int b0 = 0; b0 < live.Count; b0 += batch)
                {
                    if (clock.Elapsed.TotalSeconds > deadline) { res.Incomplete = true; res.Note = $"{label}: time budget reached while checking {stroke}-stroke finishes"; goto done; }
                    int n = Math.Min(batch, live.Count - b0);
                    var found = new ShotRec?[n]; var counts = new long[n];
                    Parallel.For(0, n, po, bi =>
                    {
                        var node = nodes[live[b0 + bi]];
                        var fromLie = hole.LieAt(node.Pos); double lf = fromLie.PowerFactor();
                        var hs = Headings(hole, node.Pos, o, true);
                        ShotRec? pick = null; double pickD = double.MaxValue; long cnt = 0;
                        foreach (var club in o.Clubs)
                            foreach (double power in o.Powers)
                                foreach (double h in hs)
                                {
                                    var s = new CourseShot(club, Impact(power), h, node.Pos, hole, lf, o.Wind); cnt++;
                                    if (s.Lie != CourseLie.Green) continue;
                                    var td = Touchdown(node.Pos, s);
                                    if (accept != null && !accept(hole, s, td)) continue;
                                    double d = s.Rest.DistanceTo(hole.Pin);
                                    if (d < pickD) { pickD = d; pick = Rec(node.Pos, fromLie, s, td); }
                                }
                        found[bi] = pick; counts[bi] = cnt;
                    });
                    shots += counts.Sum();
                    int win = -1;                                           // nodes are ordered by distance to the pin: the first with a finish wins
                    for (int bi = 0; bi < n && win < 0; bi++) if (found[bi].HasValue) win = bi;
                    if (win >= 0)
                    {
                        res.Found = true; res.Strokes = stroke;
                        var chain = new List<ShotRec> { found[win].Value };
                        for (var nd = nodes[live[b0 + win]]; nd.Parent >= 0; nd = nodes[nd.Parent]) chain.Add(nd.Via);
                        chain.Reverse(); res.Shots = chain;
                        goto done;
                    }
                }

                res.Checked = stroke;
                // ---- phase B: expand every node of this cost into the next states (only if a later stroke could still matter)
                if (stroke >= maxStrokes) continue;
                var expand = ids.Where(id => nodes[id].Pos.DistanceTo(hole.Pin) <= reach(maxStrokes - c)).ToList();
                int before = nodes.Count;
                for (int b0 = 0; b0 < expand.Count; b0 += batch)
                {
                    if (clock.Elapsed.TotalSeconds > deadline) { res.Incomplete = true; res.Note = $"{label}: time budget reached while expanding {c}-stroke states ({b0}/{expand.Count} done)"; goto done; }
                    int n = Math.Min(batch, expand.Count - b0);
                    var outs = new List<(long key, ShotRec rec, int cost)>[n]; var counts = new long[n];
                    Parallel.For(0, n, po, bi =>
                    {
                        var node = nodes[expand[b0 + bi]];
                        var fromLie = hole.LieAt(node.Pos); double lf = fromLie.PowerFactor();
                        var hs = Headings(hole, node.Pos, o, false);
                        var list = new List<(long, ShotRec, int)>(); long cnt = 0;
                        foreach (var club in o.Clubs)
                            foreach (double power in o.Powers)
                                foreach (double h in hs)
                                {
                                    var s = new CourseShot(club, Impact(power), h, node.Pos, hole, lf, o.Wind); cnt++;
                                    if (s.Lie == CourseLie.Green) continue;                    // goals were collected in phase A
                                    if (s.PenaltyStrokes > 0 && !allowPenalty) continue;
                                    int cost = c + 1 + s.PenaltyStrokes;
                                    if (cost >= maxStrokes) continue;                           // would leave no stroke to finish with
                                    if (s.NextPosition.DistanceTo(hole.Pin) > reach(maxStrokes - cost)) continue;
                                    var td = Touchdown(node.Pos, s);
                                    if (accept != null && !accept(hole, s, td)) continue;
                                    long key = Key(s.NextPosition, o.Cell);
                                    if (best.TryGetValue(key, out int have) && nodes[have].Cost <= cost) continue;
                                    list.Add((key, Rec(node.Pos, fromLie, s, td), cost));
                                }
                        outs[bi] = list; counts[bi] = cnt;
                    });
                    shots += counts.Sum();
                    for (int bi = 0; bi < n; bi++)
                    {
                        int parent = expand[b0 + bi];
                        foreach (var (key, rec, cost) in outs[bi])
                        {
                            if (best.TryGetValue(key, out int have) && nodes[have].Cost <= cost) continue;
                            var nn = new PlanNode { Id = nodes.Count, Parent = parent, Cost = cost, Pos = rec.Next, Via = rec };
                            nodes.Add(nn); best[key] = nn.Id;
                            while (buckets.Count <= cost) buckets.Add(new List<int>());
                            buckets[cost].Add(nn.Id);
                        }
                    }
                }
                if (!o.Quiet) Console.Error.WriteLine($"    [{label}] {c}-stroke states {expand.Count} expanded -> {nodes.Count - before} new states; {shots:N0} shots so far, {sw.Elapsed.TotalSeconds:F1}s");
            }
            if (!res.Found && !res.Incomplete) res.Checked = maxStrokes;
            if (!res.Found && !res.Incomplete) res.Note = $"{label}: exhausted every state within {maxStrokes} strokes (2 yd cells) without finishing on the green";
        done:
            res.ShotsEvaluated = shots; res.States = nodes.Count; res.Seconds = sw.Elapsed.TotalSeconds;
            return res;
        }

        static CoursePoint Touchdown(CoursePoint origin, CourseShot s)
        {
            double h = s.Heading * Math.PI / 180;     // first impact of the flight: carry yards along the aim line (no curve, no start line)
            return new CoursePoint(origin.X + s.Carry * Math.Sin(h), origin.D + s.Carry * Math.Cos(h));
        }

        static ShotRec Rec(CoursePoint from, CourseLie fromLie, CourseShot s, CoursePoint touchdown) => new()
        {
            Club = s.Club, Power = s.Power, Heading = s.Heading, From = from, FromLie = fromLie,
            Touchdown = touchdown, Rest = s.Rest, Next = s.NextPosition, Lie = s.Lie, Penalty = s.PenaltyStrokes, Carry = s.Carry, Total = s.Total, Holed = s.IsHoled,
        };

        // ---------------------------------------------------------------- the ridge route (hole 9)
        /// The "left ridge" route: touchdown, rest and the middle of the roll are never on the right ribbon, i.e. never closer to the centerline
        /// than FairwayWidth/2 + margin yards (a point on the green is exempt, it is the target). Uses the centerline offset, as the brief says.
        public static Accept Ridge(double margin) => (hole, s, td) =>
        {
            bool Off(CoursePoint p) => hole.LieAt(p) == CourseLie.Green || hole.DistanceFromCenterline(p) > hole.FairwayWidth / 2 + margin;
            var mid = new CoursePoint((td.X + s.Rest.X) / 2, (td.D + s.Rest.D) / 2);
            return Off(td) && Off(s.Rest) && Off(mid);
        };

        // ---------------------------------------------------------------- self-check of the two prunings
        /// `plan-selfcheck`: brute-forces random states on every hole (all clubs, all 15 powers, every 3 degree heading all the way round)
        /// and checks that the two prunings the search relies on lose nothing: (1) every shot that ends on the green has a heading inside the
        /// asin(GreenRadius/distance)+0.6 degree window around the pin bearing; (2) no state farther from the pin than the reach bound has any finish.
        public static int SelfCheck(GolfCourse course, Args a)
        {
            var o = new PlanOptions { Threads = a.GetI("threads", Environment.ProcessorCount), Wind = Wind.Calm };
            foreach (var club in o.Clubs) club.Launch(1, 0, 0);
            o.MaxTotalYd = MeasureMaxTotal(o.Wind);
            int samples = a.GetI("samples", 120);
            bool allOk = true;
            foreach (var h in CourseSelect.Holes(course, a))
            {
                var rng = new Random(20261003 + h.Number);
                var (bx0, bd0, bx1, bd1) = Geo.Bounds(h, 20);
                var states = new List<CoursePoint>();
                // a quarter of the states are drawn near the pin so that finishes actually occur
                while (states.Count < samples)
                {
                    bool near = states.Count % 4 == 0;
                    var p = near ? new CoursePoint(h.Pin.X + (rng.NextDouble() * 2 - 1) * 300, h.Pin.D + (rng.NextDouble() * 2 - 1) * 300)
                                 : new CoursePoint(bx0 + rng.NextDouble() * (bx1 - bx0), bd0 + rng.NextDouble() * (bd1 - bd0));
                    var lie = h.LieAt(p);
                    if (lie is CourseLie.Water or CourseLie.OutOfBounds or CourseLie.Green) continue;
                    states.Add(p);
                }
                long shots = 0, finishes = 0, outsideWindow = 0, beyondReach = 0, farStates = 0;
                var lk = new object();
                Parallel.For(0, states.Count, new ParallelOptions { MaxDegreeOfParallelism = o.Threads }, si =>
                {
                    var p = states[si];
                    double lf = h.LieAt(p).PowerFactor();
                    double bPin = p.HeadingTo(h.Pin), dPin = p.DistanceTo(h.Pin);
                    double half = dPin > h.GreenRadius ? Math.Asin(Math.Min(1, h.GreenRadius / dPin)) * 180 / Math.PI + 0.6 : 180;
                    double reach = o.MaxTotalYd * 1.25 + 30 + h.GreenRadius;
                    long sh = 0, fin = 0, outw = 0, beyond = 0;
                    foreach (var club in o.Clubs)
                        foreach (double power in o.Powers)
                            for (int k = -60; k <= 60; k++)
                            {
                                double heading = bPin + 3 * k;
                                var s = new CourseShot(club, Impact(power), heading, p, h, lf, o.Wind); sh++;
                                if (s.Lie != CourseLie.Green) continue;
                                fin++;
                                if (Math.Abs(3 * k) > half) outw++;
                                if (dPin > reach) beyond++;
                            }
                    lock (lk) { shots += sh; finishes += fin; outsideWindow += outw; beyondReach += beyond; if (dPin > reach) farStates++; }
                });
                bool ok = outsideWindow == 0 && beyondReach == 0 && finishes > 0;
                Console.WriteLine($"GATE: PLAN_PRUNING_EXACT {(ok ? "PASS" : "FAIL")} - hole {h.Number}: {states.Count} random states ({farStates} beyond the reach bound), {shots:N0} shots over every 3 degree heading all the way round, {finishes:N0} finishes on the green, {outsideWindow} outside the pin window, {beyondReach} from beyond the reach bound");
                allOk &= ok;
            }
            return allOk ? 0 : 1;
        }

        // ---------------------------------------------------------------- command
        static string Describe(ShotRec r, int n)
        {
            string pen = r.Penalty > 0 ? $"  (+{r.Penalty} penalty: {r.Lie}, next stroke from {Fmt.Pt(r.Next)})" : "";
            string holed = r.Holed ? " HOLED" : "";
            return $"      {n}. {r.Club,-6} power {Fmt.F(r.Power, 2)} aim {(r.Heading >= 0 ? "+" : "")}{Fmt.F(r.Heading, 1)} deg from {Fmt.Pt(r.From)} [{r.FromLie} x{Fmt.F(r.FromLie.PowerFactor(), 2)}] -> touchdown {Fmt.Pt(r.Touchdown)}, rest {Fmt.Pt(r.Rest)} {r.Lie}{holed}, carry {Fmt.F(r.Carry, 0)} total {Fmt.F(r.Total, 0)}{pen}";
        }

        /// Plays the reported shots again from the tee with the real CourseShot and checks the chain: every shot starts where the previous
        /// one left the ball (its NextPosition), rests and lies are the same, and the last one ends on the green.
        static string Replay(Hole hole, PlanOptions o, SearchResult r)
        {
            var at = hole.Tee;
            for (int i = 0; i < r.Shots.Count; i++)
            {
                var rec = r.Shots[i];
                if (rec.From.DistanceTo(at) > 1e-9) return $"MISMATCH shot {i + 1} starts at {Fmt.Pt(rec.From)} but the ball is at {Fmt.Pt(at)}";
                var s = new CourseShot(rec.Club, Impact(rec.Power), rec.Heading, at, hole, hole.LieAt(at).PowerFactor(), o.Wind);
                if (s.Rest.DistanceTo(rec.Rest) > 1e-9 || s.Lie != rec.Lie || s.PenaltyStrokes != rec.Penalty) return $"MISMATCH shot {i + 1} replays to {Fmt.Pt(s.Rest)} {s.Lie}";
                at = s.NextPosition;
            }
            return r.Shots.Count > 0 && r.Shots[r.Shots.Count - 1].Lie == CourseLie.Green ? "replay of the real CourseShot chain verified" : "MISMATCH last shot is not on the green";
        }

        static void Print(string title, SearchResult r, int par, Hole hole, PlanOptions o)
        {
            Console.WriteLine($"  {title}");
            if (r.Found)
            {
                Console.WriteLine($"    strokes to the green: {r.Strokes} (par {par}; {r.Shots.Sum(s => s.Penalty)} penalty stroke(s))   [{r.States:N0} states, {r.ShotsEvaluated:N0} shots, {r.Seconds:F1}s]   {Replay(hole, o, r)}");
                for (int i = 0; i < r.Shots.Count; i++) Console.WriteLine(Describe(r.Shots[i], i + 1));
            }
            else Console.WriteLine($"    NOT FOUND{(r.Incomplete ? " (INCOMPLETE, budget ran out)" : "")}: {r.Note}   [{r.States:N0} states, {r.ShotsEvaluated:N0} shots, {r.Seconds:F1}s]");
        }

        public static int Command(GolfCourse course, Args a)
        {
            var o = new PlanOptions
            {
                Cell = a.GetD("cell", 2), HeadingStep = a.GetD("heading-step", 3), HeadingSpan = a.GetD("span", 30),
                Threads = a.GetI("threads", Environment.ProcessorCount), Quiet = a.Has("quiet"),
            };
            if (a.Has("wind")) { var t = a.Get("wind").Split(','); o.Wind = new Wind(double.Parse(t[0], Fmt.Inv), t.Length > 1 ? double.Parse(t[1], Fmt.Inv) : 0); }
            double budget = a.GetD("budget", 160);
            // warm up the club calibration once, single-threaded, before the parallel searches
            foreach (var club in o.Clubs) club.Launch(1, 0, 0);
            o.MaxTotalYd = MeasureMaxTotal(o.Wind);
            var clock = Stopwatch.StartNew();
            Console.WriteLine($"PLAN {course.Name}: clubs Driver/Iron/Wedge, power {o.Powers[0]:0.00}..{o.Powers[o.Powers.Length - 1]:0.00} step 0.05, heading grid {Fmt.G(o.HeadingStep)} deg anchored on the pin bearing within +-{Fmt.G(o.HeadingSpan)} of every station ahead and the pin, {Fmt.G(o.Cell)} yd cells, wind {o.Wind}, longest stroke {Fmt.F(o.MaxTotalYd, 0)} yd, {o.Threads} threads, total budget {Fmt.G(budget)} s");
            bool allOk = true;
            var holes = CourseSelect.Holes(course, a).ToList();
            for (int hi = 0; hi < holes.Count; hi++)
            {
                var h = holes[hi];
                int cap = a.GetI("max-strokes", h.Par);
                Console.WriteLine($"HOLE {h.Number} par {h.Par}, centerline {Fmt.F(h.Length, 1)} yd, pin {Fmt.Pt(h.Pin)}, planning up to {cap} strokes");
                double holeEnd = clock.Elapsed.TotalSeconds + Math.Max(1, (budget - clock.Elapsed.TotalSeconds) / (holes.Count - hi));
                bool ridge = a.Has("ridge") || (h.Number == 9 && !a.Has("no-ridge"));
                double Slice(double share) => clock.Elapsed.TotalSeconds + Math.Max(0.5, (holeEnd - clock.Elapsed.TotalSeconds) * share);

                int need = Math.Max(1, Math.Min(h.Par - 2, cap));
                // the gate first: is there a zero-penalty finish within par-2 strokes? (cheap: only the first par-3 levels are expanded)
                var clean = Search(h, o, false, need, null, clock, Slice(ridge ? 0.35 : 0.5), "no-penalty");
                bool ok = clean.Found;
                bool decided = ok || clean.Checked >= need;
                var shown = clean;
                if (!ok && cap > need)
                {
                    // the gate failed: find out how many strokes the cheapest zero-penalty route takes (information only, budget limited)
                    var more = Search(h, o, false, cap, null, clock, Slice(ridge ? 0.4 : 0.5), "no-penalty (info)");
                    if (more.Found || more.Incomplete) shown = more;
                }
                Print("(b) MIN_STROKES_NO_PENALTY (zero penalty strokes):", shown, h.Par, h, o);
                int capAny = shown.Found ? shown.Strokes : cap;     // penalties can only shorten a route, never lengthen it
                var any = Search(h, o, true, capAny, null, clock, Slice(shown.Found ? (ridge ? 0.4 : 1.0) : 0.25), "penalties-allowed");   // information only: a smaller slice when even the clean route is unknown
                Print("(a) MIN_STROKES_ANY (penalty strokes allowed and counted):", any, h.Par, h, o);

                string why = ok
                    ? $"hole {h.Number}: green in {clean.Strokes} stroke(s) with {clean.Shots.Sum(s => s.Penalty)} penalty strokes by {string.Join(" + ", clean.Shots.Select(s => $"{s.Club} {Fmt.F(s.Power, 2)}"))} (par {h.Par} needs <= {need})"
                    : $"hole {h.Number}: no zero-penalty route to the green within {need} stroke(s) ({(decided ? "exhaustive on the 2 yd grid" : "INCOMPLETE, budget ran out after testing " + clean.Checked + "-stroke finishes")}); cheapest found: {(shown.Found ? shown.Strokes + " strokes" : "none within the budget")}";
                Console.WriteLine($"GATE: PAR_REACHABLE {(ok ? "PASS" : "FAIL")} - {why}");
                allOk &= ok;

                if (ridge)
                {
                    double margin = a.GetD("ridge-margin", 4);
                    var rr = Search(h, o, false, cap, Ridge(margin), clock, Slice(1.0), "ridge");
                    Print($"(d) RIDGE_ROUTE: zero penalty, touchdown/rest/roll-middle never within {Fmt.G(h.FairwayWidth / 2 + margin)} yd of the centerline (the right ribbon = fairway half-width {Fmt.G(h.FairwayWidth / 2)} + {Fmt.G(margin)}); green exempt:", rr, h.Par, h, o);
                    bool rok = rr.Found;
                    Console.WriteLine($"GATE: RIDGE_ROUTE_REACHABLE {(rok ? "PASS" : "FAIL")} - hole {h.Number}: " + (rr.Found ? $"the ridge route reaches the green in {rr.Strokes} stroke(s) with 0 penalty strokes (par {h.Par}; direct route {(shown.Found ? shown.Strokes.ToString() : "n/a")})" : $"no ridge route within {cap} strokes ({(rr.Incomplete ? "INCOMPLETE: " : "")}{rr.Note})"));
                    allOk &= rok;
                }
            }
            Console.WriteLine($"RESULT: {(allOk ? "ALL PASS" : "FAILURES")}   [{clock.Elapsed.TotalSeconds:F1}s]");
            return allOk ? 0 : 1;
        }
    }
}
