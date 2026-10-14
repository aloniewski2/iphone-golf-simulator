using System;
using System.Collections.Generic;
using GolfArcade.Shot;

namespace GolfArcade.Course
{
    /// A golden ring standing in the air over the hole (a gold one still, a glide one sliding to
    /// and fro across the line, a rainbow one when the skies clear after rain), facing down the
    /// line of play. The ball flies through it, nothing stops it, and the ring pays its coins.
    public enum RingKind { Gold, Glide, Rainbow }

    public sealed class Ring
    {
        public readonly RingKind Kind;
        /// Where it stands (course yards across, world height, yards down), the way it faces (a
        /// unit across/down: the line of play) and the opening's radius.
        public readonly double X, Y, D, NormalX, NormalD, Radius;
        /// A glide ring slides this far either side across the line, once round every `Period` seconds.
        public readonly double Glide, Period, Phase;
        public readonly int Coins;

        public Ring(RingKind kind, double x, double y, double d, double normalX, double normalD, double radius, double glide = 0, double period = 6, double phase = 0)
        {
            Kind = kind; X = x; Y = y; D = d;
            double l = Math.Max(1e-9, Math.Sqrt(normalX * normalX + normalD * normalD));
            NormalX = normalX / l; NormalD = normalD / l;
            Radius = radius; Glide = glide; Period = Math.Max(1, period); Phase = phase;
            Coins = kind switch { RingKind.Glide => 25, RingKind.Rainbow => 50, _ => 10 };
        }

        public static double RadiusOf(RingKind kind) => kind switch { RingKind.Glide => 3.9, RingKind.Rainbow => 3.6, _ => 3.6 };

        /// Its middle at `t` on the movers' clock: a gold ring bobs a little where it is, a glide one slides across.
        public (double x, double y, double d) At(double t)
        {
            double bob = 0.35 * Math.Sin(t * 1.3 + Phase * 5);
            if (Glide <= 0) return (X, Y + bob, D);
            // across the line of play is the normal turned a quarter
            double s = Glide * Math.Sin(2 * Math.PI * (t / Period + Phase));
            return (X + NormalD * s, Y + bob, D - NormalX * s);
        }
    }

    /// The rings over a hole, and which a shot flew through.
    public static class CourseRings
    {
        /// A ring the ball flew through, and when (seconds into the shot).
        public readonly struct Pass
        {
            public readonly int Ring;
            public readonly double Time;
            public Pass(int ring, double time) { Ring = ring; Time = time; }
        }

        /// The rings `shot` flew through (in the order it met them), skipping those already
        /// `taken`: each ring's middle at the movers' clock when the ball got there
        /// (`phaseAtLaunch` + the shot's time), the ball where the game draws it (CourseShot.WorldAt).
        public static List<Pass> Through(CourseShot shot, Ring[] rings, double phaseAtLaunch, Func<CoursePoint, double> ground, bool[] taken = null)
        {
            var passes = new List<Pass>();
            if (shot == null || rings == null || rings.Length == 0 || shot.Club == GolfClub.Putter || shot.LandingTime <= 0) return passes;
            const double step = 1.0 / 120;
            double until = shot.LandingTime;
            for (int i = 0; i < rings.Length; i++)
            {
                if (taken != null && i < taken.Length && taken[i]) continue;
                var r = rings[i];
                double Side(double t, out (double x, double y, double d) ball, out (double x, double y, double d) mid)
                {
                    ball = shot.WorldAt(t, ground);
                    mid = r.At(phaseAtLaunch + t);
                    return (ball.x - mid.x) * r.NormalX + (ball.d - mid.d) * r.NormalD;
                }
                double before = Side(0, out _, out _);
                for (double t = step; t <= until + 1e-9; t += step)
                {
                    double now = Side(t, out _, out _);
                    if (before < 0 && now >= 0 || before > 0 && now <= 0)
                    {
                        double u = before / (before - now), at = t - step + u * step;
                        Side(at, out var b, out var m);
                        double dx = b.x - m.x, dy = b.y - m.y, dd = b.d - m.d;
                        if (Math.Sqrt(dx * dx + dy * dy + dd * dd) <= r.Radius - CourseShot.BallRadius) { passes.Add(new Pass(i, at)); break; }
                    }
                    before = now;
                }
            }
            passes.Sort((a, b) => a.Time.CompareTo(b.Time));
            return passes;
        }

        /// The flight of a straight shot from `origin` toward `target` with the club that gets
        /// there under `rules` (swung softer when it would fly past), for laying rings along: its
        /// samples in the aim frame, the heading, the club and the swing. Null when nothing gets airborne.
        public static (BallFlight flight, double heading, GolfClub club, double power)? StraightShot(CoursePoint origin, CoursePoint target, PlayRules rules, CourseLie lie = CourseLie.Fairway)
        {
            rules ??= PlayRules.Standard;
            double distance = origin.DistanceTo(target);
            if (distance < 30) return null;
            BallFlight Fly(GolfClub c, double power)
            {
                var launch = c.Launch(power, 0, 0);
                rules.Apply(ref launch);
                return BallFlight.Simulate(launch);
            }
            // the club the game itself picks for the distance (GolfGame.AutoClub), under these rules
            var pick = GolfClubs.ForDistance(distance, c => lie.PowerFactor(c) * rules.CarryFactor(c));
            var best = Fly(pick, 1);
            if (best == null || best.Carry < 10) return null;
            double p = 1;
            for (int k = 0; k < 4 && best.Carry > distance * 1.04; k++)
            {
                p = Math.Max(0.5, p * distance / best.Carry);
                best = Fly(pick, p);
            }
            return (best, Math.Atan2(target.X - origin.X, target.D - origin.D) * 180 / Math.PI, pick, p);
        }

        /// A point on that flight `fraction` of the way through its carry, in the world: across,
        /// height (over the line the game draws between the grounds it leaves and lands on), down.
        public static (double x, double y, double d) AlongFlight(BallFlight flight, double heading, CoursePoint origin, Func<CoursePoint, double> ground, double fraction, double lateral = 0)
        {
            double cosH = Math.Cos(heading * Math.PI / 180), sinH = Math.Sin(heading * Math.PI / 180);
            (double x, double d) World(double l, double dist) => (origin.X + l * cosH + dist * sinH, origin.D - l * sinH + dist * cosH);
            var land = World(flight.CarryPoint.LateralYards, flight.CarryPoint.DistanceYards);
            double g0 = ground(origin), gL = ground(new CoursePoint(land.x, land.d));
            var p = flight.PositionAt(fraction * flight.CarryTime);
            var w = World(p.LateralYards + lateral, p.DistanceYards);
            return (w.x, g0 + (gL - g0) * fraction + p.HeightYards, w.d);
        }
    }
}
