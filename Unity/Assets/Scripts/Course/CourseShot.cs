using System;
using System.Collections.Generic;
using GolfArcade.Shot;
using GolfArcade.Swing;

namespace GolfArcade.Course
{
    /// A shot played on a hole: the flight model's arc, then a roll steered by the grass the
    /// ball is actually on, a cup that captures or lips out, and the ruling (lie, penalty, where
    /// the next stroke is played from). Sampled at 60 Hz for playback.
    public sealed class CourseShot
    {
        public const double SampleInterval = BallFlight.SampleInterval;
        /// The flight model's roll is a slow fairway. Real greens are far quicker, so a putt
        /// spends long enough rolling for the ground to move it; rough and sand grab the ball.
        public const double FairwayDeceleration = 3.2 / BallFlight.MetersPerYard;
        public const double GreenDeceleration = 1.5;
        /// g in yards/s², for the pull of a slope on a rolling ball.
        public const double GravityYards = 9.81 / BallFlight.MetersPerYard;

        public readonly GolfClub Club;
        public readonly double Power;
        public readonly double StartLine;
        public readonly double Curve;
        /// Degrees right of straight down the hole that the shot was aimed at.
        public readonly double Heading;
        public readonly Wind Wind;
        public readonly CoursePoint Origin;
        public readonly CourseLie Lie;
        public readonly double? HoledAt;
        public readonly CoursePoint NextPosition;
        public readonly CoursePoint Rest;
        public readonly double Carry;
        /// Seconds in the air, bounces included; the ball is rolling from here to `Duration`.
        public readonly double CarryTime;
        /// Where the ball starts to roll, after its bounces.
        public readonly CoursePoint Touchdown;
        /// Where the ball first comes down, and how many seconds after the strike: the end of
        /// the carry proper, before any bounce. A putt's is where it starts.
        public readonly CoursePoint Landing;
        public readonly double LandingTime;
        public readonly double Roll;
        public readonly double Apex;
        public readonly double Duration;
        public bool IsHoled => HoledAt.HasValue;
        /// It caught the cup and spun out (a groan from the gallery).
        public readonly bool LippedOut;
        public int PenaltyStrokes => IsHoled ? 0 : Lie.PenaltyStrokes();
        public double Total => Origin.DistanceTo(Rest);

        /// Course-space path: X right of the tee line, D down the hole, H height, yards.
        readonly List<(double x, double h, double d)> path;

        public (double x, double h, double d) PositionAt(double time)
        {
            if (time <= 0) return path[0];
            double index = time / SampleInterval;
            int lower = (int)index;
            if (lower >= path.Count - 1) return path[path.Count - 1];
            double t = index - lower;
            var a = path[lower]; var b = path[lower + 1];
            return (a.x + (b.x - a.x) * t, a.h + (b.h - a.h) * t, a.d + (b.d - a.d) * t);
        }

        /// When and where the ball met something standing on the course — for the picture and the
        /// sound. `Y` is the world height, NaN for a rolling ball (on the ground).
        public readonly struct Knock
        {
            public readonly double Time, X, Y, D;
            public readonly ObstacleKind Kind;
            public readonly bool Hard;
            public Knock(double time, double x, double y, double d, ObstacleKind kind, bool hard)
            { Time = time; X = x; Y = y; D = d; Kind = kind; Hard = hard; }
        }

        readonly List<Knock> knocks = new();
        /// Everything the ball hit, in order.
        public IReadOnlyList<Knock> Knocks => knocks;

        /// The ball's radius, yards, for meeting things (the real one; the drawn ball is bigger).
        public const double BallRadius = 0.047;

        /// The first thing on the course the flight runs into, and how: when, where (world
        /// heights), which one and which part of it, and the ball's velocity then.
        public readonly struct ObstacleHit
        {
            public readonly int Index;
            public readonly bool Trunk;
            public readonly double Time, X, Y, D, Vx, Vy, Vd;
            public ObstacleHit(int index, bool trunk, double time, double x, double y, double d, double vx, double vy, double vd)
            { Index = index; Trunk = trunk; Time = time; X = x; Y = y; D = d; Vx = vx; Vy = vy; Vd = vd; }
        }

        /// Along the flight as the game draws it — level with where it was struck at first,
        /// coming down to where it lands (`g0` to `gLand`) — the first obstacle it touches before
        /// `until` seconds.
        public static ObstacleHit? FirstObstacle(BallFlight flight, Func<double, double, double, (double x, double h, double d)> world, Obstacle[] obstacles, double g0, double gLand, double until)
        {
            if (obstacles == null || obstacles.Length == 0 || flight.CarryTime <= 0) return null;
            // the ones near enough to the flight's footprint to matter
            double minX = double.MaxValue, maxX = double.MinValue, minD = double.MaxValue, maxD = double.MinValue;
            for (double t = 0; t <= flight.CarryTime + 0.25; t += 0.25)
            {
                var p = flight.PositionAt(Math.Min(t, flight.CarryTime));
                var w = world(p.LateralYards, p.HeightYards, p.DistanceYards);
                minX = Math.Min(minX, w.x); maxX = Math.Max(maxX, w.x); minD = Math.Min(minD, w.d); maxD = Math.Max(maxD, w.d);
            }
            var near = new List<int>();
            for (int i = 0; i < obstacles.Length; i++)
            {
                var o = obstacles[i];
                double r = Math.Max(o.Radius, o.TrunkRadius) + 3;
                if (o.X > minX - r && o.X < maxX + r && o.D > minD - r && o.D < maxD + r) near.Add(i);
            }
            if (near.Count == 0) return null;
            double Height(double t, double h) => g0 + h + (gLand - g0) * t / flight.CarryTime;
            const double dt = 1.0 / 300;
            var p0 = flight.PositionAt(0);
            var w0 = world(p0.LateralYards, p0.HeightYards, p0.DistanceYards);
            double px = w0.x, py = Height(0, p0.HeightYards), pd = w0.d;
            for (double t = dt; t < Math.Min(until, flight.CarryTime); t += dt)
            {
                var p = flight.PositionAt(t);
                var w = world(p.LateralYards, p.HeightYards, p.DistanceYards);
                double y = Height(t, p.HeightYards);
                if (t > 0.04)
                    foreach (int i in near)
                        if (obstacles[i].Touches(w.x, y, w.d, BallRadius, out bool trunk, out _))
                            return new ObstacleHit(i, trunk, t, w.x, y, w.d, (w.x - px) / dt, (y - py) / dt, (w.d - pd) / dt);
                px = w.x; py = y; pd = w.d;
            }
            return null;
        }

        /// A stable 0–1 from where something happened: the same shot always rattles the same way.
        static double Luck(double a, double b, int i)
        {
            double s = Math.Sin(a * 12.9898 + b * 78.233 + i * 37.719) * 43758.5453;
            return s - Math.Floor(s);
        }

        /// The ball's velocity after meeting `o`. Leaves take its pace and turn it — a crown its
        /// line only clips keeps most of it, one its line goes into drops it out of the branches,
        /// a bush all but stops it; a trunk, a rock or a wall sends it back off the face with
        /// some bounce.
        public static (double vx, double vy, double vd) Rebound(Obstacle o, bool trunk, double x, double y, double d, double vx, double vy, double vd, double luck)
        {
            if (!o.IsHard(trunk))
            {
                bool clip = o.Kind == ObstacleKind.Tree && o.PassDepth(x, y, d, vx, vd, BallRadius) < 0.3;
                double keep = clip ? 0.55 + 0.2 * luck : o.Kind == ObstacleKind.Bush ? 0.08 : 0.1 + 0.2 * luck;
                double turn = (luck - 0.5) * (clip ? 30 : 110) * Math.PI / 180, c = Math.Cos(turn), s = Math.Sin(turn);
                double hx = (vx * c - vd * s) * keep, hd = (vx * s + vd * c) * keep;
                double hy = clip ? vy * 0.7 : Math.Min(vy, 0) * 0.25 - 1.0;
                return (hx, hy, hd);
            }
            var n = o.Normal(x, y, d);
            double vn = vx * n.x + vy * n.y + vd * n.d;
            if (vn >= 0) return (vx, vy, vd);
            double e = o.Kind switch { ObstacleKind.Rock => 0.5, ObstacleKind.Wall => 0.4, _ => 0.45 };
            double tx = vx - vn * n.x, ty = vy - vn * n.y, td = vd - vn * n.d;
            return (0.8 * tx - e * vn * n.x, 0.8 * ty - e * vn * n.y, 0.8 * td - e * vn * n.d);
        }

        /// From a hit, down to the ground: gravity and a little air, and anything else it meets
        /// on the way (up to four things in all). Samples at the path's times, world heights.
        public readonly struct Fall
        {
            public readonly CoursePoint Landing;
            public readonly double Time, Vx, Vy, Vd;
            public readonly List<(double t, double x, double y, double d)> Samples;
            public readonly List<Knock> Knocks;
            public Fall(CoursePoint landing, double time, double vx, double vy, double vd, List<(double, double, double, double)> samples, List<Knock> knocks)
            { Landing = landing; Time = time; Vx = vx; Vy = vy; Vd = vd; Samples = samples; Knocks = knocks; }
        }

        public static Fall FallFrom(ObstacleHit hit, Obstacle[] obstacles, Func<CoursePoint, double> ground)
        {
            var met = new List<int> { hit.Index };
            var o0 = obstacles[hit.Index];
            var knocked = new List<Knock> { new(hit.Time, hit.X, hit.Y, hit.D, o0.Kind, o0.IsHard(hit.Trunk)) };
            var (vx, vy, vd) = Rebound(o0, hit.Trunk, hit.X, hit.Y, hit.D, hit.Vx, hit.Vy, hit.Vd, Luck(hit.X, hit.D, hit.Index));
            double x = hit.X, y = hit.Y, d = hit.D, t = hit.Time;
            var samples = new List<(double, double, double, double)>();
            double next = Math.Ceiling(t / SampleInterval) * SampleInterval;
            const double dt = 1.0 / 240;
            for (int step = 0; step < 240 * 12; step++)
            {
                vy -= GravityYards * dt;
                double drag = 1 - 0.25 * dt;
                vx *= drag; vd *= drag;
                x += vx * dt; y += vy * dt; d += vd * dt; t += dt;
                if (met.Count < 4)
                    for (int i = 0; i < obstacles.Length; i++)
                    {
                        if (met.Contains(i)) continue;
                        if (!obstacles[i].Touches(x, y, d, BallRadius, out bool trunk, out _)) continue;
                        (vx, vy, vd) = Rebound(obstacles[i], trunk, x, y, d, vx, vy, vd, Luck(x, d, i));
                        met.Add(i);
                        knocked.Add(new Knock(t, x, y, d, obstacles[i].Kind, obstacles[i].IsHard(trunk)));
                        break;
                    }
                double g = ground(new CoursePoint(x, d));
                if (y <= g) return new Fall(new CoursePoint(x, d), t, vx, vy, vd, samples, knocked);
                if (t >= next) { samples.Add((next, x, y, d)); next += SampleInterval; }
            }
            return new Fall(new CoursePoint(x, d), t, vx, vy, vd, samples, knocked);
        }

        /// Down from a fall: it bounces on the ground it came down on — lively off a fairway or a
        /// green, dead in the rough, not at all in sand, where it plugs — and each bounce takes
        /// some of the run out of it. Samples go on the path (heights over the ground); returns
        /// the velocity it rolls on with.
        static (double vx, double vd) BounceOn(CourseLie lie, Fall fall, List<(double x, double h, double d)> path, Hole hole)
        {
            var (e, keep) = lie switch
            {
                CourseLie.Green or CourseLie.Fringe => (0.32, 0.8),
                CourseLie.Rough or CourseLie.OutOfBounds => (0.15, 0.45),
                CourseLie.Bunker => (0.0, 0.08),
                _ => (0.38, 0.7),
            };
            double x = fall.Landing.X, d = fall.Landing.D, h = 0;
            double vx = fall.Vx * keep, vd = fall.Vd * keep, vh = -fall.Vy * e;
            double t = fall.Time, next = path.Count * SampleInterval;
            const double dt = 1.0 / 240;
            for (int step = 0; step < 240 * 8 && vh > 0.8; step++)
            {
                h += vh * dt; vh -= GravityYards * dt;
                x += vx * dt; d += vd * dt; t += dt;
                if (h <= 0)
                {
                    h = 0; vh = -vh * e; vx *= keep; vd *= keep;
                    if (hole.LieAt(new CoursePoint(x, d)) is CourseLie.Bunker or CourseLie.Water) break;
                }
                if (t >= next) { path.Add((x, Math.Max(0, h), d)); next += SampleInterval; }
            }
            path.Add((x, 0, d));
            return (vx, vd);
        }

        /// A rolling ball against something standing on the course: off a trunk, a rock or a
        /// wall, a little livelier than dead; caught in a bush. True when it ran into it.
        static bool RollInto(in Obstacle o, ref double x, ref double d, ref double vx, ref double vd)
        {
            double dx = x - o.X, dd = d - o.D, dist = Math.Sqrt(dx * dx + dd * dd), reach = o.Footprint + BallRadius;
            if (dist >= reach || dist < 1e-9) return false;
            double nx = dx / dist, nd = dd / dist, vn = vx * nx + vd * nd;
            bool struck = vn < 0;
            if (struck)
            {
                if (o.Kind == ObstacleKind.Bush) { vx *= 0.1; vd *= 0.1; }
                else { vx -= 1.35 * vn * nx; vd -= 1.35 * vn * nd; }
            }
            x = o.X + nx * reach; d = o.D + nd * reach;
            return struck;
        }

        public static double RollingDeceleration(CourseLie lie) => lie switch
        {
            CourseLie.Green => GreenDeceleration,
            CourseLie.Fringe => (GreenDeceleration + FairwayDeceleration) / 2,
            CourseLie.Rough or CourseLie.OutOfBounds => FairwayDeceleration * 2,
            CourseLie.Bunker => FairwayDeceleration * 6,   // soft sand: a ball running in stops in a yard or two
            CourseLie.Water => FairwayDeceleration * 4,
            _ => FairwayDeceleration,
        };

        /// Where a flight first meets ground that has risen above it, if it does before it would
        /// have come down on a flat course. Its height is taken from where it was struck, so a
        /// terrace or a pinnacle in the way is met, not flown through. A gentle rise is landed on
        /// (with half its pace, as a first bounce would leave it); a face steeper than about 55°
        /// is struck, and the ball drops to its foot with a little of its pace thrown back. The
        /// velocity given for a landing is the flight's over the ground at that moment.
        public readonly struct Contact
        {
            public readonly CoursePoint Landing;
            /// True when it struck a face and dropped, rather than coming down on the slope.
            public readonly bool Wall;
            public readonly double HitTime, HitHeight, LandingTime, Vx, Vd;
            public Contact(CoursePoint landing, bool wall, double hitTime, double hitHeight, double landingTime, double vx, double vd)
            { Landing = landing; Wall = wall; HitTime = hitTime; HitHeight = hitHeight; LandingTime = landingTime; Vx = vx; Vd = vd; }
        }

        public static Contact? FirstContact(BallFlight flight, Func<double, double, double, (double x, double h, double d)> world, CoursePoint origin, Func<CoursePoint, double> ground)
        {
            double g0 = ground(origin);
            const double step = 2 * SampleInterval;
            var prev = origin;
            for (double t = step; t < flight.CarryTime; t += step)
            {
                var p = flight.PositionAt(t);
                var w = world(p.LateralYards, p.HeightYards, p.DistanceYards);
                var at = new CoursePoint(w.x, w.d);
                double y = g0 + p.HeightYards, g = ground(at);
                if (t > 0.15 && g > y + 0.15)
                {
                    double hx = (at.X - prev.X) / step, hd = (at.D - prev.D) / step;
                    double run = Math.Max(0.01, prev.DistanceTo(at)), rise = g - ground(prev);
                    if (rise < Math.Max(1.0, run * 1.4))
                        return new Contact(at, false, t, g, t, hx, hd);
                    double foot = ground(prev);
                    double fall = Math.Sqrt(2 * Math.Max(0, y - foot) / GravityYards);
                    return new Contact(prev, true, t, y, t + fall, -hx * 0.12, -hd * 0.12);
                }
                prev = at;
            }
            return null;
        }

        /// The fastest a ball rolling dead centre over the cup can go and still drop, yards/s. A
        /// regulation cup holds 1.63 m/s (it has to fall its own radius before it reaches the far
        /// wall — Holmes, 1991); the game's cup is drawn over three times as wide and forgives
        /// half as much pace again, so a putt a little firm still goes down.
        public const double CentreCaptureSpeed = 1.5 * 1.63 / BallFlight.MetersPerYard;
        /// A dying ball within this of the rim (yards; about half a foot) is drawn over the edge
        /// and in: the lip is worn soft, and a putt that would have stopped just short or trickled
        /// just past topples in.
        public const double CupEdgeReach = 0.18;
        /// Slower than this (yards/s) the edge draws it in.
        public const double CupEdgeSpeed = 0.9;
        /// How deep the drawn ball dips crossing the hole: into it when it drops, a lip's worth
        /// when it catches the far edge and spins out.
        const double DropDepth = 0.12, LipDip = 0.035;

        /// Whether a ball crossing the cup `offset` yards from its centre at `speed` yards/s
        /// drops: the chord it crosses shrinks toward the edge, so the faster it comes the closer
        /// to the middle it has to be — dead centre holds the most, a ball through the side door
        /// has to be slower. Offsets are read against the drawn cup, so the hole is as wide as it
        /// looks and as fussy about pace as a real one.
        public static double CaptureSpeed(double offset)
        {
            double u = offset / Hole.CupCaptureRadius;
            // (softer toward the rim than the chord alone, so a ball in the side door is forgiven too)
            return u >= 1 ? 0 : CentreCaptureSpeed * Math.Pow(1 - u * u, 0.35);
        }

        public static bool CupCaptures(double speed, double offset) => offset <= Hole.CupCaptureRadius && speed <= CaptureSpeed(offset);

        /// A ball crossing the cup, and how it comes out: it drops; or, too quick for the chord
        /// it is on, it falls part of its radius (`Depth`, as a share) before meeting the far rim
        /// — shallow and it skims over with a little pace lost; deeper and the rim catches it,
        /// takes most of the pace square to the rim and swings it round the lip, so a ball off
        /// the edge horseshoes and one through the middle hops out straight.
        public readonly struct CupCrossing
        {
            public readonly bool Holed, Lipped;
            public readonly double Seconds, Depth, X, D, Vx, Vd;
            public CupCrossing(bool holed, bool lipped, double seconds, double depth, double x, double d, double vx, double vd)
            { Holed = holed; Lipped = lipped; Seconds = seconds; Depth = depth; X = x; D = d; Vx = vx; Vd = vd; }
        }

        public static CupCrossing CrossTheCup(double x, double d, double vx, double vd, CoursePoint pin)
        {
            double r = Hole.CupCaptureRadius;
            double speed = Math.Sqrt(vx * vx + vd * vd);
            if (speed < 1e-6) return new CupCrossing(true, false, 0, 1, pin.X, pin.D, 0, 0);
            double dx = vx / speed, dd = vd / speed;
            double relX = pin.X - x, relD = pin.D - d;
            double along = relX * dx + relD * dd;                       // to the point nearest the middle
            double offX = x + dx * along - pin.X, offD = d + dd * along - pin.D;
            double offset = Math.Sqrt(offX * offX + offD * offD);
            double half = Math.Sqrt(Math.Max(0, r * r - offset * offset));
            double chord = Math.Max(0, along + half);                   // from here to the far rim
            double seconds = chord / speed;
            double capture = CaptureSpeed(offset);
            if (speed <= capture)
            {
                // in: over to the middle and down
                double toMiddle = Math.Max(0, along) / speed;
                return new CupCrossing(true, false, Math.Max(toMiddle, 1.0 / 60), 1, pin.X, pin.D, 0, 0);
            }
            double depth = capture > 0 ? (capture / speed) * (capture / speed) : 0;   // share of its radius it fell
            double ex = x + dx * chord, ed = d + dd * chord;           // where it meets the far rim
            if (depth < 0.3)
                return new CupCrossing(false, false, seconds, depth, ex, ed, vx * (1 - 0.3 * depth), vd * (1 - 0.3 * depth));
            // the rim: split the pace square to it and along it
            double nx = (ex - pin.X) / r, nd = (ed - pin.D) / r;
            double nl = Math.Max(1e-9, Math.Sqrt(nx * nx + nd * nd)); nx /= nl; nd /= nl;
            double vn = vx * nx + vd * nd;
            double tx = vx - vn * nx, td = vd - vn * nd;
            double outN = Math.Max(0, vn) * Math.Max(0.1, 1 - 1.2 * depth);   // climbing out takes most of it: the deeper, the more
            double keepT = 1 - 0.25 * depth;                           // riding round the lip, a little
            double nvx = tx * keepT + nx * outN, nvd = td * keepT + nd * outN;
            // (just outside the rim, so it is off the hole)
            return new CupCrossing(false, true, seconds, depth, pin.X + nx * r * 1.02, pin.D + nd * r * 1.02, nvx, nvd);
        }

        public CourseShot(GolfClub club, SwingImpact impact, double heading, CoursePoint origin, Hole hole, double lieFactor = 1, Wind wind = default)
        {
            Club = club;
            Wind = wind;
            Power = Clamp(impact.Power, 0, 1);
            StartLine = double.IsFinite(impact.StartLineDegrees) ? impact.StartLineDegrees : 0;
            Curve = club == GolfClub.Putter ? 0 : Clamp(impact.CurveDegrees, -20, 20);
            Heading = double.IsFinite(heading) ? heading : 0;
            Origin = origin;
            path = new List<(double, double, double)> { (origin.X, 0, origin.D) };
            Landing = origin;

            double speedFactor = Clamp(lieFactor, 0, 1);
            if (Power <= 0 || speedFactor <= 0)
            {
                Lie = hole.LieAt(origin); HoledAt = null; NextPosition = origin; Rest = origin;
                Carry = Roll = Apex = Duration = 0;
                return;
            }

            double launchHeading = (Heading + StartLine) * Math.PI / 180;
            double vx, vd, time;
            if (club == GolfClub.Putter)
            {
                // The putter is rated on the green: a full stroke rolls its reference distance
                // on green pace, and the meter curves so tap-ins have room.
                double distance = club.DistanceYards(Power) * speedFactor * hole.LieAt(origin).PowerFactor(club);
                double speed = Math.Sqrt(2 * GreenDeceleration * distance);
                vx = Math.Sin(launchHeading) * speed; vd = Math.Cos(launchHeading) * speed;
                time = 0; Carry = 0; Apex = 0;
            }
            else
            {
                var launch = hole.LieAt(origin).LaunchFrom(club, Power, StartLine, Curve, impact.Thin, speedFactor);
                launch.WindMPH = wind.SpeedMPH;
                launch.WindDegrees = wind.RelativeTo(Heading);
                var flight = BallFlight.Simulate(launch);
                double cosH = Math.Cos(Heading * Math.PI / 180), sinH = Math.Sin(Heading * Math.PI / 180);
                // Flight samples are in the aim frame; rotate them onto the course.
                (double x, double h, double d) World(double lateral, double height, double distance) =>
                    (origin.X + lateral * cosH + distance * sinH, height, origin.D - lateral * sinH + distance * cosH);
                var first = World(flight.CarryPoint.LateralYards, 0, flight.CarryPoint.DistanceYards);
                // The flight is the same whatever it comes down on; the bounces are not. Fly it
                // again onto the ground it actually lands on: a receptive green, smothering rough,
                // sand that plugs it.
                var (soft, grab) = hole.LieAt(new CoursePoint(first.x, first.d)).Landing();
                if (soft > 0 || grab > 0)
                {
                    launch.LandingSoftness = soft; launch.LandingGrab = grab;
                    flight = BallFlight.Simulate(launch);
                }
                Carry = flight.Carry; Apex = flight.Apex;
                var contact = hole.Ground == null ? null : FirstContact(flight, World, origin, hole.Ground);
                // Something standing in the way — a tree, a rock, a wall — met before the ground
                // is: off it or out of its branches, down, and on from where it comes to rest.
                Func<CoursePoint, double> groundAt = hole.Ground ?? (_ => 0);
                double gStart = groundAt(origin), gFlat = groundAt(new CoursePoint(first.x, first.d));
                var obstacleHit = hole.Obstacles.Length == 0 ? null
                    : FirstObstacle(flight, World, hole.Obstacles, gStart, gFlat, contact?.HitTime ?? flight.CarryTime);
                if (obstacleHit is ObstacleHit oh)
                {
                    var fall = FallFrom(oh, hole.Obstacles, groundAt);
                    knocks.AddRange(fall.Knocks);
                    double gDown = groundAt(fall.Landing);
                    Landing = fall.Landing; LandingTime = fall.Time;
                    double Level(double s) => gStart + (gDown - gStart) * s / Math.Max(1e-6, fall.Time);
                    // drawn as it flew up to the hit, then its fall, against the line the game
                    // draws from where it was struck to where it came down
                    double s0 = SampleInterval;
                    for (; s0 < oh.Time; s0 += SampleInterval)
                    {
                        var p = flight.PositionAt(s0);
                        var w = World(p.LateralYards, p.HeightYards, p.DistanceYards);
                        double y = gStart + p.HeightYards + (gFlat - gStart) * s0 / Math.Max(1e-6, flight.CarryTime);
                        path.Add((w.x, y - Level(s0), w.d));
                    }
                    foreach (var q in fall.Samples)
                        if (q.t >= s0 - 1e-9) path.Add((q.x, q.y - Level(q.t), q.d));
                    path.Add((Landing.X, 0, Landing.D));
                    Carry = origin.DistanceTo(Landing);
                    Apex = Math.Max(Apex, 0);
                    if (hole.LieAt(Landing) == CourseLie.Water)
                    {
                        Touchdown = Rest = Landing;
                        CarryTime = Duration = LandingTime;
                        Roll = 0; HoledAt = null;
                        Lie = CourseLie.Water;
                        NextPosition = Drop(Landing, origin, hole);
                        return;
                    }
                    var (bx, bd) = BounceOn(hole.LieAt(Landing), fall, path, hole);
                    vx = bx; vd = bd;
                    time = CarryTime = (path.Count - 1) * SampleInterval;
                    goto Rolling;
                }
                if (contact is Contact c)
                {
                    // It met the ground before a flat course would have brought it down: up on a
                    // rise, or into a cliff face and down to its foot. Drawn from here as it flew —
                    // the height above the line the game draws between the two grounds.
                    double g0 = hole.Ground(origin), gL = hole.Ground(c.Landing);
                    Landing = c.Landing;
                    LandingTime = c.LandingTime;
                    for (double s = SampleInterval; s < c.LandingTime; s += SampleInterval)
                    {
                        double level = g0 + (gL - g0) * s / c.LandingTime;
                        if (s <= c.HitTime)
                        {
                            var p = flight.PositionAt(s);
                            var w = World(p.LateralYards, p.HeightYards, p.DistanceYards);
                            path.Add((w.x, g0 + p.HeightYards - level, w.d));
                        }
                        else path.Add((Landing.X, c.HitHeight - 0.5 * GravityYards * (s - c.HitTime) * (s - c.HitTime) - level, Landing.D));
                    }
                    path.Add((Landing.X, 0, Landing.D));
                    Carry = origin.DistanceTo(Landing);
                    if (hole.LieAt(Landing) == CourseLie.Water)
                    {
                        Touchdown = Rest = Landing;
                        CarryTime = Duration = LandingTime;
                        Roll = 0; HoledAt = null;
                        Lie = CourseLie.Water;
                        NextPosition = Drop(Landing, origin, hole);
                        return;
                    }
                    if (c.Wall)
                    {
                        time = CarryTime = LandingTime;
                        vx = c.Vx; vd = c.Vd;
                        goto Rolling;
                    }
                    // Down on the rise: from here it bounces and checks exactly as the flight
                    // model's own landing does — the same spin, on the ground it came down on —
                    // just sooner and higher up.
                    var (soft2, grab2) = hole.LieAt(Landing).Landing();
                    if (soft2 != launch.LandingSoftness || grab2 != launch.LandingGrab)
                    {
                        launch.LandingSoftness = soft2; launch.LandingGrab = grab2;
                        flight = BallFlight.Simulate(launch);
                    }
                    var flat = flight.PositionAt(flight.CarryTime);
                    var flatW = World(flat.LateralYards, 0, flat.DistanceYards);
                    double sx = Landing.X - flatW.x, sd = Landing.D - flatW.d;
                    for (double s = flight.CarryTime + SampleInterval; s <= flight.RollStartTime; s += SampleInterval)
                    {
                        var p = flight.PositionAt(s);
                        var w = World(p.LateralYards, p.HeightYards, p.DistanceYards);
                        path.Add((w.x + sx, p.HeightYards, w.d + sd));
                    }
                    var rs = flight.PositionAt(flight.RollStartTime);
                    var rsW = World(rs.LateralYards, 0, rs.DistanceYards);
                    path.Add((rsW.x + sx, 0, rsW.d + sd));
                    time = CarryTime = LandingTime + Math.Max(0, flight.RollStartTime - flight.CarryTime);
                    vx = flight.RollStartVelocityX * cosH + flight.RollStartVelocityZ * sinH;
                    vd = -flight.RollStartVelocityX * sinH + flight.RollStartVelocityZ * cosH;
                    goto Rolling;
                }
                Landing = new CoursePoint(first.x, first.d);
                LandingTime = flight.CarryTime;
                if (flight.CarryTime > 0 && hole.LieAt(Landing) == CourseLie.Water)
                {
                    // Down in the water: it stays where it went in, bounce or no bounce.
                    for (double s = SampleInterval; s < flight.CarryTime; s += SampleInterval)
                    {
                        var p = flight.PositionAt(s);
                        path.Add(World(p.LateralYards, p.HeightYards, p.DistanceYards));
                    }
                    path.Add(first);
                    Touchdown = Rest = Landing;
                    CarryTime = Duration = flight.CarryTime;
                    Roll = 0; HoledAt = null;
                    Lie = CourseLie.Water;
                    NextPosition = Drop(Landing, origin, hole);
                    return;
                }
                double t = SampleInterval;
                for (; t <= flight.RollStartTime; t += SampleInterval)
                {
                    var p = flight.PositionAt(t);
                    path.Add(World(p.LateralYards, p.HeightYards, p.DistanceYards));
                }
                var start = flight.PositionAt(flight.RollStartTime);
                var startWorld = World(start.LateralYards, 0, start.DistanceYards);
                path.Add(startWorld);
                time = flight.RollStartTime;
                CarryTime = flight.RollStartTime;
                vx = flight.RollStartVelocityX * cosH + flight.RollStartVelocityZ * sinH;
                vd = -flight.RollStartVelocityX * sinH + flight.RollStartVelocityZ * cosH;
            }

            Rolling:
            // Roll on the course: the ground steers it. From the moment the ball is rolling it
            // accelerates down any slope it is on and is slowed by the grass it is on, so uphill
            // comes up short, downhill runs on and a side slope breaks the line; the cup has its
            // say, and a lip-out keeps some pace.
            var surface = hole.Surface ?? FlatSurface.Instance;
            var last = path[path.Count - 1];
            double x = last.x, d = last.d;
            // what it could run into on the way: trunks, bushes, rocks, walls it can reach at the
            // pace it has (as far as it would run on a green, and a little over)
            var inReach = new List<int>();
            double rollReach = Math.Min(150, (vx * vx + vd * vd) / (2 * GreenDeceleration) + 6);
            for (int i = 0; i < hole.Obstacles.Length; i++)
            {
                var o = hole.Obstacles[i];
                if (Math.Abs(o.X - x) < rollReach + o.Radius && Math.Abs(o.D - d) < rollReach + o.Radius) inReach.Add(i);
            }
            var touchdown = new CoursePoint(x, d);
            Touchdown = touchdown;
            const double dt = 1.0 / 240;
            double nextSample = time + SampleInterval;
            double? holed = null;
            bool wet = false, lippedEver = false, overCup = false;
            double elapsed = time;
            while (elapsed < 30)
            {
                double speed = Math.Sqrt(vx * vx + vd * vd);
                var here = new CoursePoint(x, d);
                var lieNow = hole.LieAt(here);
                if (lieNow == CourseLie.Water) { wet = true; break; }
                double decel = RollingDeceleration(lieNow);
                var slope = surface.Gradient(here);
                double downhill = Math.Sqrt(slope.dx * slope.dx + slope.dd * slope.dd) * GravityYards;
                double toCup = here.DistanceTo(hole.Pin);
                // on the soft edge of the cup, dying: it is drawn over the rim (see CupEdgeReach)
                double edge = toCup > Hole.CupCaptureRadius && toCup < Hole.CupCaptureRadius + CupEdgeReach && speed < CupEdgeSpeed
                    ? 1 - (toCup - Hole.CupCaptureRadius) / CupEdgeReach : 0;
                // Stopped, unless the face it sits on is steep enough to start it rolling again.
                if (speed < 0.02 && downhill < decel * 0.9 && edge <= 0) break;

                // At the cup: decided once, as the ball's centre crosses onto the hole (see
                // CrossTheCup) — it drops, catches the far lip and spins out, or skims over.
                double dx = speed > 0.001 ? vx / speed : 0, dd = speed > 0.001 ? vd / speed : 0;
                if (toCup <= Hole.CupCaptureRadius && (speed <= 0.001 || elapsed <= time))
                {
                    // Resting on the hole (or struck from over it): it drops.
                    x = hole.Pin.X; d = hole.Pin.D;
                    holed = elapsed;
                    path.Add((x, -DropDepth, d));
                    break;
                }
                if (toCup > Hole.CupCaptureRadius) overCup = false;
                else if (!overCup)
                {
                    overCup = true;
                    var cross = CrossTheCup(x, d, vx, vd, hole.Pin);
                    // the transit, drawn: across the mouth and dipping into it
                    for (double s = SampleInterval; s < cross.Seconds; s += SampleInterval)
                    {
                        double sink = Math.Min(0.5 * GravityYards * s * s, cross.Holed ? DropDepth : LipDip);
                        path.Add((x + vx * s, -sink, d + vd * s));
                    }
                    elapsed += cross.Seconds;
                    nextSample = elapsed + SampleInterval;
                    if (cross.Holed)
                    {
                        x = hole.Pin.X; d = hole.Pin.D;
                        holed = elapsed;
                        path.Add((x, -DropDepth, d));
                        break;
                    }
                    if (cross.Lipped) lippedEver = true;
                    x = cross.X; d = cross.D; vx = cross.Vx; vd = cross.Vd;
                    path.Add((x, 0, d));
                    continue;
                }

                double ax = -GravityYards * slope.dx, ad = -GravityYards * slope.dd;
                if (edge > 0)
                {
                    double pull = decel * 1.1 + 2.5 * edge;
                    ax += pull * (hole.Pin.X - x) / toCup; ad += pull * (hole.Pin.D - d) / toCup;
                }
                if (speed > 0.0001)
                {
                    double friction = Math.Min(decel, speed / dt); // never reverses the ball
                    ax -= friction * vx / speed; ad -= friction * vd / speed;
                }
                vx += ax * dt; vd += ad * dt;
                x += vx * dt; d += vd * dt;
                foreach (int i in inReach)
                    if (RollInto(hole.Obstacles[i], ref x, ref d, ref vx, ref vd) && speed > 0.4)
                        knocks.Add(new Knock(elapsed, x, double.NaN, d, hole.Obstacles[i].Kind, hole.Obstacles[i].Kind != ObstacleKind.Bush));
                elapsed += dt;
                if (elapsed + 1e-9 >= nextSample) { path.Add((x, 0, d)); nextSample += SampleInterval; }
            }
            if (path[path.Count - 1].d != d || path[path.Count - 1].x != x) path.Add((x, 0, d));
            LippedOut = lippedEver && !holed.HasValue;

            var rest = new CoursePoint(x, d);
            Rest = rest;
            HoledAt = holed;
            Duration = holed ?? (path.Count - 1) * SampleInterval;
            Roll = touchdown.DistanceTo(rest);
            if (holed.HasValue)
            {
                Lie = CourseLie.Green; NextPosition = rest; return;
            }
            Lie = wet ? CourseLie.Water : hole.LieAt(rest);
            NextPosition = Lie switch
            {
                CourseLie.Water => Drop(rest, origin, hole),
                CourseLie.OutOfBounds => origin,
                _ => rest,
            };
        }

        static double Clamp(double v, double lo, double hi) => double.IsFinite(v) ? Math.Max(lo, Math.Min(hi, v)) : lo;

        /// Walk back toward where the shot was played until the ball is on dry ground.
        static CoursePoint Drop(CoursePoint from, CoursePoint origin, Hole hole)
        {
            double length = from.DistanceTo(origin);
            if (length <= 0.5) return origin;
            double ux = (origin.X - from.X) / length, ud = (origin.D - from.D) / length;
            for (double travelled = 1; travelled < length; travelled += 1)
            {
                var p = new CoursePoint(from.X + ux * travelled, from.D + ud * travelled);
                var lie = hole.LieAt(p);
                if (lie != CourseLie.Water && lie != CourseLie.OutOfBounds) return new CoursePoint(p.X + ux * 2, p.D + ud * 2);
            }
            return origin;
        }
    }
}
