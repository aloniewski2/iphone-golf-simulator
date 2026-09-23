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

        public static double RollingDeceleration(CourseLie lie) => lie switch
        {
            CourseLie.Green => GreenDeceleration,
            CourseLie.Fringe => (GreenDeceleration + FairwayDeceleration) / 2,
            CourseLie.Rough or CourseLie.OutOfBounds => FairwayDeceleration * 2,
            CourseLie.Bunker => FairwayDeceleration * 2.5,
            CourseLie.Water => FairwayDeceleration * 4,
            _ => FairwayDeceleration,
        };

        /// The fastest a ball rolling dead centre over a regulation cup can go and still drop:
        /// it has to fall its own radius before it reaches the far wall (Holmes, 1991), yards/s.
        public const double CentreCaptureSpeed = 1.63 / BallFlight.MetersPerYard;
        /// How deep the drawn ball dips crossing the hole: into it when it drops, a lip's worth
        /// when it catches the far edge and spins out.
        const double DropDepth = 0.12, LipDip = 0.035;

        /// Whether a ball crossing the cup `offset` yards from its centre at `speed` yards/s
        /// drops: the chord it crosses shrinks toward the edge, so the faster it comes the closer
        /// to the middle it has to be — dead centre holds 1.63 m/s, a ball through the side door
        /// has to be dying. Offsets are read against the drawn cup, so the hole is as wide as it
        /// looks and as fussy about pace as a real one.
        public static double CaptureSpeed(double offset)
        {
            double u = offset / Hole.CupCaptureRadius;
            return u >= 1 ? 0 : CentreCaptureSpeed * Math.Sqrt(1 - u * u);
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

            // Roll on the course: the ground steers it. From the moment the ball is rolling it
            // accelerates down any slope it is on and is slowed by the grass it is on, so uphill
            // comes up short, downhill runs on and a side slope breaks the line; the cup has its
            // say, and a lip-out keeps some pace.
            var surface = hole.Surface ?? FlatSurface.Instance;
            var last = path[path.Count - 1];
            double x = last.x, d = last.d;
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
                // Stopped, unless the face it sits on is steep enough to start it rolling again.
                if (speed < 0.02 && downhill < decel * 0.9) break;

                // At the cup: decided once, as the ball's centre crosses onto the hole (see
                // CrossTheCup) — it drops, catches the far lip and spins out, or skims over.
                double toCup = here.DistanceTo(hole.Pin);
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
                if (speed > 0.0001)
                {
                    double friction = Math.Min(decel, speed / dt); // never reverses the ball
                    ax -= friction * vx / speed; ad -= friction * vd / speed;
                }
                vx += ax * dt; vd += ad * dt;
                x += vx * dt; d += vd * dt;
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
