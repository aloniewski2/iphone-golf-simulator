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

        public readonly GolfClub Club;
        public readonly double Power;
        public readonly double StartLine;
        public readonly double Curve;
        /// Degrees right of straight down the hole that the shot was aimed at.
        public readonly double Heading;
        public readonly CoursePoint Origin;
        public readonly CourseLie Lie;
        public readonly double? HoledAt;
        public readonly CoursePoint NextPosition;
        public readonly CoursePoint Rest;
        public readonly double Carry;
        public readonly double Roll;
        public readonly double Apex;
        public readonly double Duration;
        public bool IsHoled => HoledAt.HasValue;
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
            CourseLie.Rough or CourseLie.OutOfBounds => FairwayDeceleration * 1.6,
            CourseLie.Bunker => FairwayDeceleration * 2.5,
            CourseLie.Water => FairwayDeceleration * 4,
            _ => FairwayDeceleration,
        };

        /// Whether a ball crossing the cup `offset` yards from its centre at `speed` yards/s
        /// drops. Dead centre holds a little over 1.6 m/s, and the faster it comes the closer to
        /// the middle it has to be (Holmes' capture model), so a firm putt on the edge
        /// horseshoes out where a dying one falls in.
        public static bool CupCaptures(double speed, double offset)
        {
            double radius = Hole.CupCaptureRadius;
            if (offset > radius) return false;
            double centred = Math.Max(0, 1 - (offset / radius) * (offset / radius));
            double limit = 0.35 + 1.55 * Math.Sqrt(centred);
            return speed <= limit;
        }

        public CourseShot(GolfClub club, SwingImpact impact, double heading, CoursePoint origin, Hole hole, double lieFactor = 1)
        {
            Club = club;
            Power = Clamp(impact.Power, 0, 1);
            StartLine = double.IsFinite(impact.StartLineDegrees) ? impact.StartLineDegrees : 0;
            Curve = club == GolfClub.Putter ? 0 : Clamp(impact.CurveDegrees, -15, 15);
            Heading = double.IsFinite(heading) ? heading : 0;
            Origin = origin;
            path = new List<(double, double, double)> { (origin.X, 0, origin.D) };

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
                double distance = club.DistanceYards(Power) * speedFactor;
                double speed = Math.Sqrt(2 * GreenDeceleration * distance);
                vx = Math.Sin(launchHeading) * speed; vd = Math.Cos(launchHeading) * speed;
                time = 0; Carry = 0; Apex = 0;
            }
            else
            {
                var flight = BallFlight.Simulate(club.Launch(Power, StartLine, Curve, speedFactor));
                Carry = flight.Carry; Apex = flight.Apex;
                double cosH = Math.Cos(Heading * Math.PI / 180), sinH = Math.Sin(Heading * Math.PI / 180);
                // Flight samples are in the aim frame; rotate them onto the course.
                (double x, double h, double d) World(double lateral, double height, double distance) =>
                    (origin.X + lateral * cosH + distance * sinH, height, origin.D - lateral * sinH + distance * cosH);
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
                vx = flight.RollStartVelocityX * cosH + flight.RollStartVelocityZ * sinH;
                vd = -flight.RollStartVelocityX * sinH + flight.RollStartVelocityZ * cosH;
            }

            // Roll on the course: the grass under the ball sets the deceleration, the cup has
            // its say, and a lip-out keeps some pace.
            var last = path[path.Count - 1];
            double x = last.x, d = last.d;
            var touchdown = new CoursePoint(x, d);
            const double dt = 1.0 / 240;
            double nextSample = time + SampleInterval;
            double? holed = null;
            bool wet = false, lippedOut = false;
            double elapsed = time;
            while (elapsed < 30)
            {
                double speed = Math.Sqrt(vx * vx + vd * vd);
                var here = new CoursePoint(x, d);
                var lieNow = hole.LieAt(here);
                if (lieNow == CourseLie.Water) { wet = true; break; }
                double decel = RollingDeceleration(lieNow);
                if (speed < 0.02) break;

                // At the cup: judged by the miss distance (how far from the middle the ball's
                // line passes) and its pace: drop in, or rattle off the rim and keep going.
                double toCup = here.DistanceTo(hole.Pin);
                double dx = vx / speed, dd = vd / speed;
                if (toCup <= Hole.CupCaptureRadius)
                {
                    double relX = hole.Pin.X - x, relD = hole.Pin.D - d;
                    double along = relX * dx + relD * dd;
                    double missX = -(relX - dx * along), missD = -(relD - dd * along);
                    double miss = Math.Sqrt(missX * missX + missD * missD);
                    if (CupCaptures(speed, Math.Min(miss, toCup)))
                    {
                        x = hole.Pin.X; d = hole.Pin.D;
                        holed = elapsed;
                        path.Add((x, 0, d));
                        break;
                    }
                    if (!lippedOut)
                    {
                        // Lip-out: the rim throws the ball out to the side it is passing on and
                        // takes much of its pace, the way a firm putt horseshoes round the hole.
                        lippedOut = true;
                        double outX = miss > 0.005 ? missX / miss : -dd, outD = miss > 0.005 ? missD / miss : dx;
                        double kept = speed * 0.55;
                        double newX = dx * 0.7 + outX * 0.7, newD = dd * 0.7 + outD * 0.7;
                        double len = Math.Max(0.001, Math.Sqrt(newX * newX + newD * newD));
                        vx = newX / len * kept; vd = newD / len * kept;
                        speed = kept; dx = vx / speed; dd = vd / speed;
                    }
                }
                else if (toCup > Hole.CupCaptureRadius * 3)
                {
                    lippedOut = false;
                }

                double slowed = Math.Max(0, speed - decel * dt);
                double movingTime = Math.Min(dt, speed / decel);
                double travel = speed * movingTime - 0.5 * decel * movingTime * movingTime;
                x += dx * travel; d += dd * travel;
                vx = dx * slowed; vd = dd * slowed;
                elapsed += dt;
                if (elapsed + 1e-9 >= nextSample) { path.Add((x, 0, d)); nextSample += SampleInterval; }
            }
            if (path[path.Count - 1].d != d || path[path.Count - 1].x != x) path.Add((x, 0, d));

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
