using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GolfArcade.Course;
using GolfCourse = GolfArcade.Course.Course;

namespace GolfArcade.PostcardCheck
{
    /// The brief's numbers for one hole (13_POSTCARD_HOLES.txt + the lead's task text).
    sealed class Limits
    {
        public int Number; public string Name = "?";
        public double LengthMin = 0, LengthMax = 1e9;
        public int Par; public double CarryMax = 140;
        public int Water, Bunker;
        public bool Forced = true; public int? Pads;
        public double MinDryBefore = 40, MinDryAfter = 30, MinPadArea = 500, RouteClearanceMin = 6, OobLandMaxPct = 1, ShoreEdgeMargin = 1;
        public string Note = "";

        /// Needle 8 par 4 length 320-360 carry<=140 2 water 1 bunker; Split 9 par 5 460-500 carry<=140 1 water 3 bunkers;
        /// Crater 10 par 4 340-380 carry<=150 1 water 2 bunkers. Cliffside 7 (the existing hole) is the harness's control.
        public static Limits For(Hole h)
        {
            switch (h.Number)
            {
                case 8: return new Limits { Number = 8, Name = "Needle", Par = 4, LengthMin = 320, LengthMax = 360, CarryMax = 140, Water = 2, Bunker = 1, Forced = true, Pads = 3 };
                case 9: return new Limits { Number = 9, Name = "Split", Par = 5, LengthMin = 460, LengthMax = 500, CarryMax = 140, Water = 1, Bunker = 3, Forced = false,
                    Note = "Split is one island with a dry ridge route: ONE_ISLAND_NO_SLIVERS + DRY_ROUTE_AROUND_THE_WATER instead of separated pads" };
                case 10: return new Limits { Number = 10, Name = "Crater", Par = 4, LengthMin = 340, LengthMax = 380, CarryMax = 150, Water = 1, Bunker = 2, Forced = true, Pads = 2,
                    Note = "Crater: the green bulb is a pillar cut off by the lava ellipse, so the carry is forced (2 dry pads); override with --set forced=false if the design keeps a dry route" };
                case 7: return new Limits { Number = 7, Name = "Cliffside", Par = 4, LengthMin = 404, LengthMax = 420, CarryMax = 140, Water = 0, Bunker = 6, Forced = false,
                    Note = "Cliffside control: CliffsideHoleTests says 412 +- 8 yd, par 4, 6 bunkers, sea beyond the shore" };
                default:
                    return new Limits { Number = h.Number, Name = "?", Par = h.Par, Water = h.Hazards.Count(z => z.Kind == HazardKind.Water), Bunker = h.Hazards.Count(z => z.Kind == HazardKind.Bunker), Forced = false,
                        Note = $"no brief limits for hole {h.Number}: par, hazard counts taken from the hole itself, length unconstrained" };
            }
        }

        public void Apply(string key, string value)
        {
            var inv = Fmt.Inv;
            switch (key.ToLowerInvariant())
            {
                case "length_min": LengthMin = double.Parse(value, inv); break;
                case "length_max": LengthMax = double.Parse(value, inv); break;
                case "par": Par = int.Parse(value, inv); break;
                case "carry_max": CarryMax = double.Parse(value, inv); break;
                case "water": Water = int.Parse(value, inv); break;
                case "bunker": Bunker = int.Parse(value, inv); break;
                case "forced": Forced = bool.Parse(value); break;
                case "pads": Pads = int.Parse(value, inv); break;
                case "min_dry_before": MinDryBefore = double.Parse(value, inv); break;
                case "min_dry_after": MinDryAfter = double.Parse(value, inv); break;
                case "min_pad_area": MinPadArea = double.Parse(value, inv); break;
                case "route_clearance_min": RouteClearanceMin = double.Parse(value, inv); break;
                case "oob_land_max_pct": OobLandMaxPct = double.Parse(value, inv); break;
                case "shore_edge_margin": ShoreEdgeMargin = double.Parse(value, inv); break;
                default: throw new ArgumentException("unknown limit key '" + key + "'");
            }
        }
    }

    sealed class GateLine
    {
        public string Name; public bool Ok; public string Detail;
        public override string ToString() => $"GATE: {Name} {(Ok ? "PASS" : "FAIL")} - {Detail}";
    }

    /// A lie map of the whole dry area at one resolution (cell centres), used for pads, routes and out-of-bounds share.
    sealed class LieGrid
    {
        public readonly Hole H; public readonly double Cell, X0, D0; public readonly int Nx, Nd;
        public readonly CourseLie[] Lie;   // index i * Nd + j

        public LieGrid(Hole h, double cell, double margin)
        {
            H = h; Cell = cell;
            double x1, d1;
            if (h.Shore != null)
            {
                X0 = h.Shore.Min(p => p.X) - margin; D0 = h.Shore.Min(p => p.D) - margin;
                x1 = h.Shore.Max(p => p.X) + margin; d1 = h.Shore.Max(p => p.D) + margin;
            }
            else { var b = Geo.Bounds(h, margin); X0 = b.x0; D0 = b.d0; x1 = b.x1; d1 = b.d1; }
            Nx = (int)Math.Ceiling((x1 - X0) / cell); Nd = (int)Math.Ceiling((d1 - D0) / cell);
            Lie = new CourseLie[Nx * Nd];
            for (int i = 0; i < Nx; i++) for (int j = 0; j < Nd; j++) Lie[i * Nd + j] = h.LieAt(Centre(i, j));
        }

        public CoursePoint Centre(int i, int j) => new(X0 + (i + 0.5) * Cell, D0 + (j + 0.5) * Cell);
        public (int i, int j) CellOf(CoursePoint p) => ((int)Math.Floor((p.X - X0) / Cell), (int)Math.Floor((p.D - D0) / Cell));
        public bool In(int i, int j) => i >= 0 && j >= 0 && i < Nx && j < Nd;
        public CourseLie At(int i, int j) => Lie[i * Nd + j];

        /// 4-connected components of cells whose lie satisfies `ok`; returns a component id per cell (-1 = none) and the sizes.
        public (int[] id, List<int> sizes) Components(Func<CourseLie, bool> ok)
        {
            var id = Enumerable.Repeat(-1, Lie.Length).ToArray();
            var sizes = new List<int>();
            var stack = new Stack<int>();
            for (int c = 0; c < Lie.Length; c++)
            {
                if (id[c] >= 0 || !ok(Lie[c])) continue;
                int comp = sizes.Count, n = 0;
                id[c] = comp; stack.Push(c);
                while (stack.Count > 0)
                {
                    int cur = stack.Pop(); n++;
                    int ci = cur / Nd, cj = cur % Nd;
                    foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int ni = ci + di, nj = cj + dj;
                        if (!In(ni, nj)) continue;
                        int nc = ni * Nd + nj;
                        if (id[nc] < 0 && ok(Lie[nc])) { id[nc] = comp; stack.Push(nc); }
                    }
                }
                sizes.Add(n);
            }
            return (id, sizes);
        }

        /// Approximate distance in yards from every ok-cell to the nearest not-ok cell (two-pass chamfer 1 / sqrt2); off-grid is not ok.
        public double[] Clearance(Func<CourseLie, bool> ok)
        {
            const double INF = 1e9;
            var dist = new double[Lie.Length];
            for (int c = 0; c < Lie.Length; c++) dist[c] = ok(Lie[c]) ? INF : 0;
            double a = 1, b = Math.Sqrt(2);
            double Get(int i, int j) => In(i, j) ? dist[i * Nd + j] : 0;
            for (int i = 0; i < Nx; i++)
                for (int j = 0; j < Nd; j++)
                {
                    int c = i * Nd + j; if (dist[c] == 0) continue;
                    double best = dist[c];
                    best = Math.Min(best, Get(i - 1, j) + a); best = Math.Min(best, Get(i, j - 1) + a);
                    best = Math.Min(best, Get(i - 1, j - 1) + b); best = Math.Min(best, Get(i + 1, j - 1) + b);
                    dist[c] = best;
                }
            for (int i = Nx - 1; i >= 0; i--)
                for (int j = Nd - 1; j >= 0; j--)
                {
                    int c = i * Nd + j; if (dist[c] == 0) continue;
                    double best = dist[c];
                    best = Math.Min(best, Get(i + 1, j) + a); best = Math.Min(best, Get(i, j + 1) + a);
                    best = Math.Min(best, Get(i + 1, j + 1) + b); best = Math.Min(best, Get(i - 1, j + 1) + b);
                    dist[c] = best;
                }
            for (int c = 0; c < dist.Length; c++) dist[c] *= Cell;
            return dist;
        }

        /// Max over paths start->goal of the minimum clearance along the path (8-neighbour), or (0, false).
        public (double bottleneck, bool reachable) WidestPath((int i, int j) start, (int i, int j) goal, Func<CourseLie, bool> ok)
        {
            var clr = Clearance(ok);
            if (!In(start.i, start.j) || !In(goal.i, goal.j)) return (0, false);
            int s = start.i * Nd + start.j, g = goal.i * Nd + goal.j;
            if (clr[s] <= 0 || clr[g] <= 0) return (0, false);
            var best = new double[Lie.Length];
            best[s] = clr[s];
            var pq = new PriorityQueue<int, double>();
            pq.Enqueue(s, -clr[s]);
            while (pq.TryDequeue(out int cur, out double neg))
            {
                double val = -neg;
                if (cur == g) return (val, true);
                if (val < best[cur]) continue;
                int ci = cur / Nd, cj = cur % Nd;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = ci + di, nj = cj + dj;
                        if (!In(ni, nj)) continue;
                        int nc = ni * Nd + nj;
                        if (clr[nc] <= 0) continue;
                        double v = Math.Min(val, clr[nc]);
                        if (v > best[nc]) { best[nc] = v; pq.Enqueue(nc, -v); }
                    }
            }
            return (0, false);
        }

        public string Ascii(int step = 4)
        {
            var sb = new StringBuilder();
            sb.Append($"        x from {Fmt.F(X0, 0)} to {Fmt.F(X0 + Nx * Cell, 0)}, one char = {step} yd; ~ sea, w = water hazard over land, = fairway, . rough, b bunker, G green, T tee, # OOB land\n");
            for (int j = 0; j < Nd; j += step)
            {
                var row = new StringBuilder();
                for (int i = 0; i < Nx; i += step)
                {
                    var l = At(i, j);
                    row.Append(l switch
                    {
                        CourseLie.Tee => 'T', CourseLie.Fairway => '=', CourseLie.Rough => '.', CourseLie.Bunker => 'b', CourseLie.Green => 'G',
                        CourseLie.Water => H.OnLand(Centre(i, j)) ? 'w' : '~', _ => '#',
                    });
                }
                sb.Append($"{Fmt.F(D0 + (j + 0.5) * Cell, 0),7} |{row}\n");
            }
            return sb.ToString().TrimEnd('\n');
        }
    }

    /// Independent C# implementation of the design gates, run on the REAL Hole.cs numbers and LieAt.
    /// Same gate names and thresholds as blender/scripts/postcard_check.py so the two can be compared line by line.
    static class Gates
    {
        static bool IsLand(CourseLie l) => l is CourseLie.Tee or CourseLie.Fairway or CourseLie.Rough or CourseLie.Bunker or CourseLie.Green;
        static bool RoundedOk(double v) => Math.Abs(v * 10 - Math.Round(v * 10)) < 1e-6;
        static string Dry(double v) => Fmt.F(v, 1);

        public static List<GateLine> Run(Hole h, Limits L, bool fast, bool showMap, out LieGrid grid)
        {
            var lines = new List<GateLine>();
            void Gate(string name, bool ok, string detail) => lines.Add(new GateLine { Name = name, Ok = ok, Detail = detail });

            var coords = new List<(double, double)>();
            foreach (var p in h.Centerline) coords.Add((p.X, p.D));
            if (h.Shore != null) foreach (var p in h.Shore) coords.Add((p.X, p.D));
            foreach (var z in h.Hazards) { coords.Add((z.X, z.Distance)); coords.Add((z.Width, z.Length)); }
            int bad = coords.Count(c => !(RoundedOk(c.Item1) && RoundedOk(c.Item2)));
            Gate("NUMBERS_ROUNDED_0.1", bad == 0 && RoundedOk(h.FairwayWidth) && RoundedOk(h.GreenRadius) && RoundedOk(h.RoughWidth),
                bad > 0 ? $"{bad} coordinate(s) not on a 0.1 yd grid" : "all coordinates on a 0.1 yd grid");
            Gate("NUMBER_AND_PAR", h.Par == L.Par && (L.Number == 0 || h.Number == L.Number), $"hole {h.Number} '{L.Name}' par {h.Par}");
            double len = h.Length;
            Gate("LENGTH", len >= L.LengthMin && len <= L.LengthMax, $"centerline {Fmt.F(len, 1)} yd, brief {Fmt.G(L.LengthMin)}-{Fmt.G(L.LengthMax)}");

            // ---- one shore, one centerline, one pin
            var sh = h.Shore;
            if (sh == null)
            {
                Gate("ONE_SHORE", false, "Hole.Shore is null: no shore polygon (anything outside it must be water)");
            }
            else
            {
                int hits = Geo.SelfIntersections(sh).Count;
                double minEdge = Enumerable.Range(0, sh.Length).Min(i => sh[i].DistanceTo(sh[(i + 1) % sh.Length]));
                double area = Math.Abs(Geo.SignedArea(sh));
                Gate("ONE_SHORE", sh.Length >= 12 && hits == 0 && area > 1000 && minEdge >= 0.5,
                    $"{sh.Length} pts, area {Fmt.F(area, 0)} yd2, self-intersections {hits}, min edge {Fmt.F(minEdge, 2)} yd");
            }
            bool dup = false;
            for (int i = 1; i < h.Centerline.Length; i++) if (h.Centerline[i - 1].DistanceTo(h.Centerline[i]) < 1.0) dup = true;
            Gate("ONE_CENTERLINE", h.Centerline.Length >= 3 && h.Tee.X == 0 && h.Tee.D == 0 && !dup, $"{h.Centerline.Length} stations, tee {Fmt.Pt(h.Tee)}, pin {Fmt.Pt(h.Pin)}");
            var stationLies = h.Centerline.Select(h.LieAt).ToList();
            Gate("STATIONS_DRY", stationLies[0] == CourseLie.Tee && stationLies[stationLies.Count - 1] == CourseLie.Green &&
                                 stationLies.All(l => l is CourseLie.Fairway or CourseLie.Green or CourseLie.Tee),
                $"station lies [{string.Join(", ", stationLies.Select(l => "'" + l + "'"))}] (RecommendedTarget aims at stations: they must be dry fairway)");
            var teeRing = Enumerable.Range(0, 16).Select(k => new CoursePoint(h.Tee.X + 8 * Math.Cos(k / 8.0 * Math.PI), h.Tee.D + 8 * Math.Sin(k / 8.0 * Math.PI))).ToList();
            Gate("TEE_ON_LAND", teeRing.Concat(new[] { h.Tee }).All(p => IsLand(h.LieAt(p))), "8 yd around the tee is land with no hazard");
            var greenRing = Enumerable.Range(0, 36).Select(k => new CoursePoint(h.Pin.X + (h.GreenRadius + 1.5) * Math.Cos(k / 18.0 * Math.PI), h.Pin.D + (h.GreenRadius + 1.5) * Math.Sin(k / 18.0 * Math.PI))).ToList();
            Gate("GREEN_ON_LAND", h.LieAt(h.Pin) == CourseLie.Green && greenRing.All(h.OnLand), "pin is Green; the green disc + 1.5 yd is inside the shore");

            // ---- hazards
            int nw = h.Hazards.Count(z => z.Kind == HazardKind.Water), nb = h.Hazards.Count(z => z.Kind == HazardKind.Bunker);
            Gate("HAZARD_COUNTS", nw == L.Water && nb == L.Bunker, $"{nw} water + {nb} bunker, brief {L.Water} + {L.Bunker}");
            var landIssues = new List<string>();
            for (int i = 0; i < h.Hazards.Length; i++)
            {
                var z = h.Hazards[i];
                if (z.Kind != HazardKind.Bunker) continue;
                var pts = Geo.EllipsePoints(z, 48);
                bool onLand = pts.All(h.OnLand);
                double edge = h.Shore == null ? double.PositiveInfinity : pts.Min(p => Geo.DistToPolygonEdge(p, h.Shore));
                if (!onLand || edge < L.ShoreEdgeMargin) landIssues.Add($"bunker [{i}] ({Fmt.F(z.X, 1)}, {Fmt.F(z.Distance, 1)}) touches the shore");
            }
            Gate("BUNKERS_ON_LAND", landIssues.Count == 0, landIssues.Count == 0 ? "every bunker ellipse sits inside the shore with margin" : string.Join("; ", landIssues));
            var clash = new List<string>();
            for (int i = 0; i < h.Hazards.Length; i++)
                for (int j = i + 1; j < h.Hazards.Length; j++)
                {
                    if (h.Hazards[i].Kind == HazardKind.Water && h.Hazards[j].Kind == HazardKind.Water) continue;
                    double gap = Geo.EllipseGap(h.Hazards[i], h.Hazards[j]);
                    if (gap < 2.0) clash.Add($"{i}/{j} gap {Fmt.F(gap, 1)} yd");
                }
            for (int i = 0; i < h.Hazards.Length; i++)
            {
                var z = h.Hazards[i];
                if (h.Centerline.Any(z.Contains) || z.Contains(h.Tee) || teeRing.Any(z.Contains)) clash.Add($"hazard [{i}] covers the tee area or a station");
                if (z.Kind == HazardKind.Bunker && Enumerable.Range(0, 16).Any(k => z.Contains(new CoursePoint(h.Pin.X + 0.6 * h.GreenRadius * Math.Cos(k / 8.0 * Math.PI), h.Pin.D + 0.6 * h.GreenRadius * Math.Sin(k / 8.0 * Math.PI)))))
                    clash.Add($"bunker [{i}] eats the inner green");
            }
            Gate("HAZARD_CLEARANCE", clash.Count == 0, clash.Count == 0 ? "bunkers/waters apart (>= 2 yd), tee, stations and the inner green are clear" : string.Join("; ", clash));

            // ---- carries along the centerline
            var samples = Geo.Samples(h.Centerline, 0.25);
            var runs = new List<(double a, double b)>();
            double ra = 0, rb = 0; bool open = false;
            foreach (var (s, p) in samples)
            {
                bool w = h.LieAt(p) == CourseLie.Water;
                if (w && !open) { open = true; ra = rb = s; }
                else if (w) rb = s;
                else if (open) { runs.Add((ra, rb)); open = false; }
            }
            if (open) runs.Add((ra, rb));
            double total = samples[samples.Count - 1].s;
            var carries = runs.Select(r => r.b - r.a + 0.25).ToList();
            Gate("CARRY_MAX", runs.Count == nw && carries.All(c => c <= L.CarryMax),
                $"{runs.Count} water crossing(s) on the centerline, carries {Fmt.List(carries, 1)} yd, max allowed {Dry(L.CarryMax)} (brief: no forced carry over {Dry(L.CarryMax)})");
            var dryIssues = new List<string>();
            double prevEnd = 0;
            for (int k = 0; k < runs.Count; k++)
            {
                double before = runs[k].a - prevEnd;
                double next = k + 1 < runs.Count ? runs[k + 1].a : total;
                double after = next - runs[k].b;
                if (before < L.MinDryBefore) dryIssues.Add($"crossing {k + 1}: only {Fmt.F(before, 1)} yd dry ground short of the water");
                if (after < L.MinDryAfter) dryIssues.Add($"crossing {k + 1}: only {Fmt.F(after, 1)} yd dry ground after it");
                prevEnd = runs[k].b;
            }
            for (int k = 0; k < runs.Count; k++)
            {
                var shortLies = samples.Where(t => t.s >= runs[k].a - 25 && t.s < runs[k].a).Select(t => h.LieAt(t.p)).Distinct().OrderBy(l => l.ToString(), StringComparer.Ordinal).ToList();
                if (!shortLies.All(l => l is CourseLie.Fairway or CourseLie.Tee or CourseLie.Green))
                    dryIssues.Add($"crossing {k + 1}: the 25 yd short of it is not clean fairway ([{string.Join(", ", shortLies.Select(l => "'" + l + "'"))}])");
            }
            bool dryOk = runs.Count > 0 ? dryIssues.Count == 0 : nw == 0;
            Gate("DRY_GROUND_SHORT_OF_EACH_CARRY", dryOk,
                dryIssues.Count > 0 ? string.Join("; ", dryIssues) : $"lay-up room >= {Dry(L.MinDryBefore)} yd short, landing room >= {Dry(L.MinDryAfter)} yd after, every crossing");

            // ---- grid: pads, routes, out-of-bounds land
            grid = new LieGrid(h, fast ? 2.0 : 1.0, 4.0);
            var g = grid;
            var (id, sizes) = g.Components(IsLand);
            var areas = sizes.Select(n => n * g.Cell * g.Cell).OrderByDescending(x => x).ToList();
            var tc = g.CellOf(h.Tee); var pc = g.CellOf(h.Pin);
            bool connected = g.In(tc.i, tc.j) && g.In(pc.i, pc.j) && id[tc.i * g.Nd + tc.j] >= 0 && id[tc.i * g.Nd + tc.j] == id[pc.i * g.Nd + pc.j];
            if (L.Forced)
            {
                int want = L.Pads ?? nw + 1;
                Gate("PADS_SEPARATED", !connected && sizes.Count == want && areas.All(x => x >= L.MinPadArea),
                    $"{sizes.Count} separate dry pads (want {want}), areas {Fmt.List(areas.Select(x => Math.Round(x)), 0)} yd2 (min {Dry(L.MinPadArea)}), tee->pin dry route exists: {Fmt.Bool(connected)}");
            }
            else
            {
                Gate("ONE_ISLAND_NO_SLIVERS", sizes.Count == 1 && connected,
                    $"{sizes.Count} dry component(s), areas {Fmt.List(areas.Select(x => Math.Round(x)), 0)} yd2, tee->pin connected: {Fmt.Bool(connected)} (one island, one pin)");
                var (bott, reach) = g.WidestPath(tc, pc, l => l is CourseLie.Tee or CourseLie.Fairway or CourseLie.Rough or CourseLie.Green);
                Gate("DRY_ROUTE_AROUND_THE_WATER", reach && bott >= L.RouteClearanceMin,
                    $"widest bunker-free, water-free route tee->pin has half-width {Fmt.F(bott, 1)} yd (need >= {Dry(L.RouteClearanceMin)}): the safe route exists");
            }
            int oob = g.Lie.Count(l => l == CourseLie.OutOfBounds);
            int land = g.Lie.Count(l => IsLand(l) || l == CourseLie.OutOfBounds);
            double pct = 100.0 * oob / Math.Max(1, land);
            Gate("NO_OUT_OF_BOUNDS_LAND", pct <= L.OobLandMaxPct,
                $"{Fmt.F(pct, 2)}% of the dry area is out-of-bounds grass (max {Dry(L.OobLandMaxPct)}%): the shore must sit inside fairway/2 + rough or RoughWidth is too small");

            // ---- C#-only checks that use the real enums (not in the Python)
            bool outsideWater = true; string outsideDetail = "";
            if (h.Shore != null)
            {
                var far = new[] { new CoursePoint(h.Shore.Min(p => p.X) - 500, h.Tee.D), new CoursePoint(h.Shore.Max(p => p.X) + 500, h.Pin.D), new CoursePoint(h.Tee.X, h.Shore.Min(p => p.D) - 500), new CoursePoint(h.Pin.X, h.Shore.Max(p => p.D) + 500) };
                outsideWater = far.All(p => h.LieAt(p) == CourseLie.Water);
                outsideDetail = $"4 points 500 yd outside the shore are Water: {outsideWater}";
            }
            var badCentres = h.Hazards.Where(z => z.Kind == HazardKind.Water && h.LieAt(new CoursePoint(z.X, z.Distance)) != CourseLie.Water).ToList();
            Gate("OUTSIDE_AND_WATER_CENTRES_ARE_WATER", outsideWater && badCentres.Count == 0, (outsideDetail + $"; {nw - badCentres.Count}/{nw} water-ellipse centres are Water").TrimStart(';', ' '));
            if (showMap) Console.WriteLine(g.Ascii(4));
            return lines;
        }

        static GateLine RulesLine()
        {
            bool ok = CourseLie.Rough.PowerFactor() == 0.85 && CourseLie.Bunker.PowerFactor() == 0.6 &&
                      new[] { CourseLie.Tee, CourseLie.Fairway, CourseLie.Green, CourseLie.Water, CourseLie.OutOfBounds }.All(l => l.PowerFactor() == 1) &&
                      CourseLie.Water.PenaltyStrokes() == 1 && CourseLie.OutOfBounds.PenaltyStrokes() == 1 &&
                      new[] { CourseLie.Tee, CourseLie.Fairway, CourseLie.Rough, CourseLie.Bunker, CourseLie.Green }.All(l => l.PenaltyStrokes() == 0);
            return new GateLine { Name = "RULES_UNCHANGED", Ok = ok, Detail = $"rough x{CourseLie.Rough.PowerFactor()}, bunker x{CourseLie.Bunker.PowerFactor()}, water +{CourseLie.Water.PenaltyStrokes()}, out of bounds +{CourseLie.OutOfBounds.PenaltyStrokes()}" };
        }

        public static int Command(GolfCourse course, Args a)
        {
            bool fast = a.Has("fast"), map = a.Has("map");
            bool all = true;
            var rules = RulesLine();
            Console.WriteLine($"=== {course.Name}: {course.Holes.Length} hole(s) ===");
            Console.WriteLine(rules);
            all &= rules.Ok;
            foreach (var h in CourseSelect.Holes(course, a))
            {
                var L = Limits.For(h);
                foreach (var kv in a.All("set")) { var t = kv.Split('=', 2); L.Apply(t[0], t[1]); }
                Console.WriteLine($"=== {course.Name}: hole {h.Number} {L.Name} par {h.Par} ===");
                if (L.Note.Length > 0) Console.WriteLine($"NOTE: {L.Note}");
                var lines = Run(h, L, fast, map, out _);
                foreach (var l in lines) { Console.WriteLine(l); all &= l.Ok; }
            }
            Console.WriteLine("RESULT: " + (all ? "ALL PASS" : "FAILURES"));
            return all ? 0 : 1;
        }
    }
}
