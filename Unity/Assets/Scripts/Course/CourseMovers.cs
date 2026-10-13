using System;
using System.Collections.Generic;
using GolfArcade.Shot;

namespace GolfArcade.Course
{
    /// What moves about a hole: a hot-air balloon, a cart, a gull, and in a storm a twister.
    public enum MoverKind { Balloon, Cart, Gull, Twister }

    /// One thing moving about a hole, on a path that is a function of time alone: where it is when
    /// the ball gets there is known the moment the ball is struck, a replay finds it in the same
    /// place, and so does every phone in a multiplayer round. It meets the ball as one or two
    /// spheres (its bodies). World units: course yards across and down the hole, world heights.
    public sealed class Mover
    {
        public readonly MoverKind Kind;
        /// Which one of its kind on the hole (gulls of a flock, the balloon's colours).
        public readonly int Variant;

        // A balloon drifts from A to B and back over `period` seconds at `height`, bobbing.
        CoursePoint a, b;
        double height, period, phase, bob;
        // A gull circles `centre` at `radius` and `height`, `turn` radians a second from `phase`.
        CoursePoint centre;
        double radius, turn;
        // A cart goes round its loop at `speed`: the loop sampled every half yard, with the ground's height.
        double[] lx, ly, ld, ls;
        double loopLength, speed;
        // A twister wanders from A to B and back over `period`, weaving, on the ground (sampled along its way).
        double[] gy;

        /// A twister's funnel as the ball meets it: spheres up its middle, wider as it climbs.
        public static readonly (double h, double r)[] Funnel = { (1.5, 2.2), (7, 3.4), (14, 5.0), (22, 6.8), (31, 8.6) };
        public const double TwisterWeave = 3;

        /// The balloon's envelope, from its middle, and how far below that the basket hangs.
        public const double Envelope = 4.3, BasketDrop = 6.4, Basket = 1.1;
        /// A cart's two bodies, front and back, and how far they are from its middle.
        public const double CartBody = 0.9, CartHalf = 0.75;
        public const double GullBody = 0.6;

        Mover(MoverKind kind, int variant) { Kind = kind; Variant = variant; }

        public static Mover Balloon(CoursePoint from, CoursePoint to, double height, double periodSeconds, double phase, int variant) =>
            new(MoverKind.Balloon, variant) { a = from, b = to, height = height, period = Math.Max(10, periodSeconds), phase = phase, bob = 1.1 };

        /// A twister wandering from `from` to `to` and back over `periodSeconds`, weaving as it goes,
        /// on the ground `ground` gives.
        public static Mover Twister(CoursePoint from, CoursePoint to, double periodSeconds, double phase, Func<CoursePoint, double> ground)
        {
            var m = new Mover(MoverKind.Twister, 0) { a = from, b = to, period = Math.Max(10, periodSeconds), phase = phase, gy = new double[21] };
            for (int i = 0; i < m.gy.Length; i++) { double s = i / 20.0; m.gy[i] = ground(new CoursePoint(from.X + (to.X - from.X) * s, from.D + (to.D - from.D) * s)); }
            return m;
        }

        public static Mover Gull(CoursePoint centre, double radius, double height, double radiansPerSecond, double phase, int variant) =>
            new(MoverKind.Gull, variant) { centre = centre, radius = Math.Max(3, radius), height = height, turn = radiansPerSecond, phase = phase, bob = 0.7 };

        /// A cart going round a loop through four corners (two lanes across the fairway and a half
        /// turn at each end), at `yardsPerSecond`, starting `phase` (0–1) of the way round.
        public static Mover CartLoop(CoursePoint p0, CoursePoint p1, CoursePoint p2, CoursePoint p3, double yardsPerSecond, double phase, Func<CoursePoint, double> ground)
        {
            var m = new Mover(MoverKind.Cart, 0) { speed = Math.Max(0.5, yardsPerSecond) };
            // the outline: lane p0→p1, a half turn round to p2, lane p2→p3, a half turn back to p0
            var outline = new List<(double x, double d)>();
            void Lane(CoursePoint s, CoursePoint e)
            {
                double l = s.DistanceTo(e); int n = Math.Max(1, (int)(l / 0.5));
                for (int i = 0; i < n; i++) outline.Add((s.X + (e.X - s.X) * i / n, s.D + (e.D - s.D) * i / n));
            }
            void Turn(CoursePoint s, CoursePoint e)
            {
                // a half circle from s to e bulging away from the loop's middle
                double cx = (s.X + e.X) / 2, cd = (s.D + e.D) / 2, r = s.DistanceTo(e) / 2;
                double mx = (p0.X + p1.X + p2.X + p3.X) / 4, md = (p0.D + p1.D + p2.D + p3.D) / 4;
                double ox = cx - mx, od = cd - md, ol = Math.Max(1e-6, Math.Sqrt(ox * ox + od * od)); ox /= ol; od /= ol;
                double a0 = Math.Atan2(s.D - cd, s.X - cx);
                double sweep = Math.PI; // which way round: through the outward side
                double midA = a0 + sweep / 2;
                if (Math.Cos(midA) * ox + Math.Sin(midA) * od < 0) sweep = -sweep;
                int n = Math.Max(4, (int)(Math.PI * r / 0.5));
                for (int i = 0; i < n; i++)
                {
                    double ang = a0 + sweep * i / n;
                    outline.Add((cx + Math.Cos(ang) * r, cd + Math.Sin(ang) * r));
                }
            }
            Lane(p0, p1); Turn(p1, p2); Lane(p2, p3); Turn(p3, p0);
            int count = outline.Count;
            m.lx = new double[count + 1]; m.ld = new double[count + 1]; m.ly = new double[count + 1]; m.ls = new double[count + 1];
            double run = 0;
            for (int i = 0; i <= count; i++)
            {
                var (x, d) = outline[i % count];
                if (i > 0) run += Math.Sqrt((x - m.lx[i - 1]) * (x - m.lx[i - 1]) + (d - m.ld[i - 1]) * (d - m.ld[i - 1]));
                m.lx[i] = x; m.ld[i] = d; m.ly[i] = ground(new CoursePoint(x, d)); m.ls[i] = run;
            }
            m.loopLength = Math.Max(1, run);
            m.phase = phase;
            return m;
        }

        /// The cart's loop as sampled (x, ground height, d), for checking where it may drive.
        public IEnumerable<(double x, double y, double d)> LoopPoints()
        {
            if (lx == null) yield break;
            for (int i = 0; i < lx.Length; i++) yield return (lx[i], ly[i], ld[i]);
        }

        /// Where it is at `t` seconds on the movers' clock: the balloon's envelope, the cart on the
        /// ground under its middle, the gull's body.
        public (double x, double y, double d) At(double t)
        {
            switch (Kind)
            {
                case MoverKind.Balloon:
                {
                    double s = 0.5 - 0.5 * Math.Cos(2 * Math.PI * (t / period + phase));
                    return (a.X + (b.X - a.X) * s, height + bob * Math.Sin(2 * Math.PI * t / 9.0 + phase * 7), a.D + (b.D - a.D) * s);
                }
                case MoverKind.Gull:
                {
                    double ang = phase + turn * t;
                    return (centre.X + radius * Math.Cos(ang), height + bob * Math.Sin(1.7 * t + phase * 3), centre.D + radius * Math.Sin(ang));
                }
                case MoverKind.Twister:
                {
                    double s = 0.5 - 0.5 * Math.Cos(2 * Math.PI * (t / period + phase));
                    double lx = b.X - a.X, ld = b.D - a.D, l = Math.Max(1e-6, Math.Sqrt(lx * lx + ld * ld));
                    double weave = TwisterWeave * Math.Sin(t * 0.7 + phase * 9);
                    double f = s * (gy.Length - 1); int i = Math.Min(gy.Length - 2, (int)f);
                    return (a.X + lx * s - ld / l * weave, gy[i] + (gy[i + 1] - gy[i]) * (f - i), a.D + ld * s + lx / l * weave);
                }
                default:
                {
                    double s = ((speed * t + phase * loopLength) % loopLength + loopLength) % loopLength;
                    int lo = 0, hi = ls.Length - 1;
                    while (hi - lo > 1) { int mid = (lo + hi) / 2; if (ls[mid] <= s) lo = mid; else hi = mid; }
                    double span = Math.Max(1e-9, ls[hi] - ls[lo]), u = (s - ls[lo]) / span;
                    return (lx[lo] + (lx[hi] - lx[lo]) * u, ly[lo] + (ly[hi] - ly[lo]) * u, ld[lo] + (ld[hi] - ld[lo]) * u);
                }
            }
        }

        /// Its velocity at `t`, yards a second.
        public (double x, double y, double d) VelocityAt(double t)
        {
            const double h = 1.0 / 30;
            var p = At(t - h); var q = At(t + h);
            return ((q.x - p.x) / (2 * h), (q.y - p.y) / (2 * h), (q.d - p.d) / (2 * h));
        }

        /// Which way it faces at `t`, degrees right of straight down the hole (the way it goes).
        public double HeadingAt(double t)
        {
            var v = VelocityAt(t);
            if (Kind is MoverKind.Balloon or MoverKind.Twister || v.x * v.x + v.d * v.d < 1e-6) return Math.Atan2(b.X - a.X, b.D - a.D) * 180 / Math.PI;
            return Math.Atan2(v.x, v.d) * 180 / Math.PI;
        }

        public int Bodies => Kind switch { MoverKind.Gull => 1, MoverKind.Twister => Funnel.Length, _ => 2 };

        /// One of the spheres it meets the ball with at `t`: its middle and its radius.
        public (double x, double y, double d, double r) Body(int i, double t)
        {
            var p = At(t);
            switch (Kind)
            {
                case MoverKind.Balloon:
                    return i == 0 ? (p.x, p.y, p.d, Envelope) : (p.x, p.y - BasketDrop, p.d, Basket);
                case MoverKind.Cart:
                {
                    double h = HeadingAt(t) * Math.PI / 180, along = i == 0 ? CartHalf : -CartHalf;
                    return (p.x + Math.Sin(h) * along, p.y + 0.85, p.d + Math.Cos(h) * along, CartBody);
                }
                case MoverKind.Twister:
                    return (p.x, p.y + Funnel[i].h, p.d, Funnel[i].r);
                default:
                    return (p.x, p.y, p.d, GullBody);
            }
        }

        /// Everywhere it can be, give or take its size: (min x, min y, min d, max x, max y, max d).
        public (double x0, double y0, double d0, double x1, double y1, double d1) Reach()
        {
            switch (Kind)
            {
                case MoverKind.Balloon:
                    return (Math.Min(a.X, b.X) - Envelope, height - bob - BasketDrop - Basket, Math.Min(a.D, b.D) - Envelope,
                            Math.Max(a.X, b.X) + Envelope, height + bob + Envelope, Math.Max(a.D, b.D) + Envelope);
                case MoverKind.Gull:
                    return (centre.X - radius - 1, height - bob - 1, centre.D - radius - 1, centre.X + radius + 1, height + bob + 1, centre.D + radius + 1);
                case MoverKind.Twister:
                {
                    double w = Funnel[Funnel.Length - 1].r + TwisterWeave + 1, low = double.MaxValue, high = double.MinValue;
                    foreach (var g in gy) { low = Math.Min(low, g); high = Math.Max(high, g); }
                    return (Math.Min(a.X, b.X) - w, low, Math.Min(a.D, b.D) - w, Math.Max(a.X, b.X) + w, high + Funnel[Funnel.Length - 1].h + Funnel[Funnel.Length - 1].r, Math.Max(a.D, b.D) + w);
                }
                default:
                {
                    double x0 = double.MaxValue, y0 = double.MaxValue, d0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, d1 = double.MinValue;
                    for (int i = 0; i < lx.Length; i++)
                    {
                        x0 = Math.Min(x0, lx[i]); y0 = Math.Min(y0, ly[i]); d0 = Math.Min(d0, ld[i]);
                        x1 = Math.Max(x1, lx[i]); y1 = Math.Max(y1, ly[i]); d1 = Math.Max(d1, ld[i]);
                    }
                    double r = CartHalf + CartBody + 0.2;
                    return (x0 - r, y0, d0 - r, x1 + r, y1 + 1.9, d1 + r);
                }
            }
        }

        /// It runs along the ground, where a rolling ball can meet it.
        public bool OnGround => Kind == MoverKind.Cart;
    }

    /// Everything moving about a hole, and the one clock they all keep. The clock runs on by
    /// itself (Advance); while a shot is in the air and in its replay the game holds it to the
    /// shot's clock (Hold), as it holds the windmill's sails, so the ball meets a balloon where the
    /// picture shows it. `PhaseAtLaunch` is the clock when the ball leaves the club, set for each
    /// shot before it is played (CourseShot reads it).
    public sealed class CourseMovers
    {
        public readonly Mover[] All;
        public double Clock { get; private set; }
        public double PhaseAtLaunch;
        double? held;
        /// The clock as the picture should show it: a shot's, while one holds it.
        public double Now => held ?? Clock;

        public CourseMovers(Mover[] movers) { All = movers ?? Array.Empty<Mover>(); }
        /// The golden rings over the hole (CourseRings): they keep the same clock, and nothing stops the ball in them.
        public Ring[] Rings = Array.Empty<Ring>();

        public void Advance(double seconds) { if (!held.HasValue && double.IsFinite(seconds)) Clock += Math.Max(0, seconds); }

        /// Held at `shotTime` seconds after the strike (from PhaseAtLaunch); null lets the clock
        /// run on from there.
        public void Hold(double? shotTime)
        {
            if (shotTime.HasValue) { held = PhaseAtLaunch + shotTime.Value; return; }
            if (held.HasValue) Clock = held.Value;
            held = null;
        }

        /// Set the clock outright (a multiplayer phone following the host's).
        public void SetClock(double seconds) { if (double.IsFinite(seconds)) Clock = seconds; }

        /// Where the ball met a mover: which one, which of its bodies, when (seconds after the
        /// strike) and where, and the ball's velocity then.
        public readonly struct Hit
        {
            public readonly int Mover, Body;
            public readonly double Time, X, Y, D, Vx, Vy, Vd;
            public Hit(int mover, int body, double time, double x, double y, double d, double vx, double vy, double vd)
            { Mover = mover; Body = body; Time = time; X = x; Y = y; D = d; Vx = vx; Vy = vy; Vd = vd; }
        }

        /// Along the flight as the game draws it (level with where it was struck at first, coming
        /// down to where it lands: `g0` to `gLand`), the first mover it meets before `until` seconds.
        public Hit? FirstHit(BallFlight flight, Func<double, double, double, (double x, double h, double d)> world, double g0, double gLand, double until)
        {
            if (All.Length == 0 || flight.CarryTime <= 0) return null;
            double end = Math.Min(until, flight.CarryTime);
            // only the ones whose reach the flight's footprint passes through
            double minX = double.MaxValue, maxX = double.MinValue, minD = double.MaxValue, maxD = double.MinValue, maxY = double.MinValue;
            for (double t = 0; t <= end + 0.25; t += 0.25)
            {
                var p = flight.PositionAt(Math.Min(t, end));
                var w = world(p.LateralYards, p.HeightYards, p.DistanceYards);
                minX = Math.Min(minX, w.x); maxX = Math.Max(maxX, w.x); minD = Math.Min(minD, w.d); maxD = Math.Max(maxD, w.d);
                maxY = Math.Max(maxY, Math.Max(g0, gLand) + p.HeightYards);
            }
            var near = new List<int>();
            for (int i = 0; i < All.Length; i++)
            {
                var r = All[i].Reach();
                if (r.x1 > minX && r.x0 < maxX && r.d1 > minD && r.d0 < maxD && r.y0 < maxY + 1) near.Add(i);
            }
            if (near.Count == 0) return null;
            double Height(double t, double h) => g0 + h + (gLand - g0) * t / flight.CarryTime;
            const double dt = 1.0 / 300;
            var p0 = flight.PositionAt(0);
            var w0 = world(p0.LateralYards, p0.HeightYards, p0.DistanceYards);
            double px = w0.x, py = Height(0, p0.HeightYards), pd = w0.d;
            for (double t = dt; t < end; t += dt)
            {
                var p = flight.PositionAt(t);
                var w = world(p.LateralYards, p.HeightYards, p.DistanceYards);
                double y = Height(t, p.HeightYards);
                if (t > 0.04)
                    foreach (int i in near)
                        for (int k = 0; k < All[i].Bodies; k++)
                        {
                            var body = All[i].Body(k, PhaseAtLaunch + t);
                            double dx = w.x - body.x, dy = y - body.y, dd = w.d - body.d, reach = body.r + CourseShot.BallRadius;
                            if (dx * dx + dy * dy + dd * dd <= reach * reach)
                                return new Hit(i, k, t, w.x, y, w.d, (w.x - px) / dt, (y - py) / dt, (w.d - pd) / dt);
                        }
                px = w.x; py = y; pd = w.d;
            }
            return null;
        }

        /// The ball's velocity off a mover: off its body's face with the bounce that body has (the
        /// balloon's envelope a rubbery boing, its basket and a cart a knock), carried along by the
        /// way the mover is going; a gull it ploughs through, losing most of its pace and dropping.
        /// `luck` (0–1) turns it a little, the same for the same shot.
        public (double vx, double vy, double vd) Rebound(Hit hit, double luck)
        {
            var m = All[hit.Mover];
            double t = PhaseAtLaunch + hit.Time;
            var body = m.Body(hit.Body, t);
            var bv = m.VelocityAt(t);
            double rx = hit.Vx - bv.x, ry = hit.Vy - bv.y, rd = hit.Vd - bv.d;   // against the mover
            if (m.Kind == MoverKind.Twister)
            {
                // caught in the whirl: carried round most of a turn and flung out of it, up and away
                double turn = (100 + 70 * luck) * Math.PI / 180, c = Math.Cos(turn), s = Math.Sin(turn);
                double keep = 0.6;
                return ((rx * c - rd * s) * keep + bv.x, 9 + 7 * luck, (rx * s + rd * c) * keep + bv.d);
            }
            if (m.Kind == MoverKind.Gull)
            {
                double turn = (luck - 0.5) * 70 * Math.PI / 180, c = Math.Cos(turn), s = Math.Sin(turn);
                double keep = 0.32;
                return ((rx * c - rd * s) * keep + bv.x * 0.3, Math.Min(ry, 0) * 0.3 - 1.2, (rx * s + rd * c) * keep + bv.d * 0.3);
            }
            double nx = hit.X - body.x, ny = hit.Y - body.y, nd = hit.D - body.d;
            double nl = Math.Sqrt(nx * nx + ny * ny + nd * nd);
            if (nl < 1e-9) { nx = 0; ny = 1; nd = 0; } else { nx /= nl; ny /= nl; nd /= nl; }
            double vn = rx * nx + ry * ny + rd * nd;
            var (e, keepT) = m.Kind == MoverKind.Balloon && hit.Body == 0 ? (0.82, 0.88) : m.Kind == MoverKind.Balloon ? (0.45, 0.8) : (0.42, 0.75);
            if (vn >= 0) return (hit.Vx, hit.Vy, hit.Vd);
            double tx = rx - vn * nx, ty = ry - vn * ny, td = rd - vn * nd;
            return (tx * keepT - e * vn * nx + bv.x, ty * keepT - e * vn * ny + bv.y, td * keepT - e * vn * nd + bv.d);
        }

        /// A rolling ball against the movers on the ground at `t` seconds after the strike: pushed
        /// out of a cart it ran into, or bumped along by one that ran into it. The cart it met, or -1.
        public int RollInto(double t, ref double x, ref double d, ref double vx, ref double vd)
        {
            for (int i = 0; i < All.Length; i++)
            {
                var m = All[i];
                if (!m.OnGround) continue;
                for (int k = 0; k < m.Bodies; k++)
                {
                    var body = m.Body(k, PhaseAtLaunch + t);
                    double reach = body.r + CourseShot.BallRadius + 0.1;
                    double dx = x - body.x, dd = d - body.d, dist = Math.Sqrt(dx * dx + dd * dd);
                    if (dist >= reach || dist < 1e-9) continue;
                    var bv = m.VelocityAt(PhaseAtLaunch + t);
                    double nx = dx / dist, nd = dd / dist;
                    double rvx = vx - bv.x, rvd = vd - bv.d, vn = rvx * nx + rvd * nd;
                    x = body.x + nx * reach; d = body.d + nd * reach;
                    if (vn >= 0) continue;
                    rvx -= 1.5 * vn * nx; rvd -= 1.5 * vn * nd;
                    vx = rvx + bv.x; vd = rvd + bv.d;
                    return i;
                }
            }
            return -1;
        }

        /// The kind of obstacle a mover counts as for the shot's knocks (effects and sounds).
        public static ObstacleKind KnockKind(MoverKind kind) => kind switch
        {
            MoverKind.Balloon => ObstacleKind.Balloon,
            MoverKind.Cart => ObstacleKind.Cart,
            MoverKind.Twister => ObstacleKind.Twister,
            _ => ObstacleKind.Gull,
        };
    }
}
