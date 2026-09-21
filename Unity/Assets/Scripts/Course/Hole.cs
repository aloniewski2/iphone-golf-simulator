using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// A spot on the ground, in yards: `X` to the right of the tee line, `D` down the hole.
    public struct CoursePoint
    {
        public double X, D;
        public CoursePoint(double x, double d) { X = x; D = d; }
        public static readonly CoursePoint Zero = new(0, 0);
        public double DistanceTo(CoursePoint o) { double dx = X - o.X, dd = D - o.D; return Math.Sqrt(dx * dx + dd * dd); }
        /// Degrees right of straight down the hole (+D) toward `o`.
        public double HeadingTo(CoursePoint o) => Math.Atan2(o.X - X, o.D - D) * 180 / Math.PI;
        public override string ToString() => $"({X:F1}, {D:F1})";
    }

    public enum CourseLie { Tee, Fairway, Rough, Bunker, Green, Water, OutOfBounds }

    public static class CourseLies
    {
        public static string Label(this CourseLie lie) => lie switch
        {
            CourseLie.Tee => "Tee",
            CourseLie.Fairway => "Fairway",
            CourseLie.Rough => "Rough",
            CourseLie.Bunker => "Bunker",
            CourseLie.Green => "Green",
            CourseLie.Water => "Water",
            _ => "Out of bounds",
        };

        /// What the lie costs in club speed.
        public static double PowerFactor(this CourseLie lie) => lie switch
        {
            CourseLie.Rough => 0.85,
            CourseLie.Bunker => 0.6,
            _ => 1,
        };

        public static int PenaltyStrokes(this CourseLie lie) => lie is CourseLie.Water or CourseLie.OutOfBounds ? 1 : 0;
    }

    public enum HazardKind { Bunker, Water }

    /// An elliptical hazard, in yards.
    public struct CourseHazard
    {
        public HazardKind Kind;
        public double X, Distance, Width, Length;

        public CourseHazard(HazardKind kind, double x, double distance, double width, double length)
        {
            Kind = kind; X = x; Distance = distance; Width = width; Length = length;
        }

        public bool Contains(CoursePoint p)
        {
            double dx = (p.X - X) / Math.Max(Width / 2, 0.001);
            double dz = (p.D - Distance) / Math.Max(Length / 2, 0.001);
            return dx * dx + dz * dz <= 1;
        }
    }

    /// One hole, in the flight model's yards so drawing and scoring share one source of truth.
    public sealed class Hole
    {
        public int Number;
        public int Par;
        public CoursePoint[] Centerline;
        public double FairwayWidth;
        public double GreenRadius;
        public CourseHazard[] Hazards = Array.Empty<CourseHazard>();
        /// Dry land, as a polygon in course yards; anything outside it is water. Null means the
        /// hole is inland and the tree line is the only edge.
        public CoursePoint[] Shore;

        /// Rough on each side of the fairway. Beyond it (the tree line) is out of bounds.
        public double RoughWidth = 24.0;
        /// Small, explicit arcade tolerance around the cup, in yards.
        public const double CupCaptureRadius = 0.12;

        public CoursePoint Tee => Centerline[0];
        public CoursePoint Pin => Centerline[Centerline.Length - 1];

        public double Length
        {
            get { double l = 0; for (int i = 1; i < Centerline.Length; i++) l += Centerline[i - 1].DistanceTo(Centerline[i]); return l; }
        }

        /// Follow the next landing station, not a straight shortcut through a dogleg.
        public CoursePoint RecommendedTarget(CoursePoint ball)
        {
            if (Centerline.Length <= 2 || ball.DistanceTo(Pin) <= GreenRadius + 20) return Pin;
            double closest = double.PositiveInfinity;
            int segment = 0;
            for (int i = 0; i < Centerline.Length - 1; i++)
            {
                var a = Centerline[i]; var b = Centerline[i + 1];
                double dx = b.X - a.X, dd = b.D - a.D;
                double t = Math.Min(1, Math.Max(0, ((ball.X - a.X) * dx + (ball.D - a.D) * dd) / Math.Max(0.001, dx * dx + dd * dd)));
                double distance = ball.DistanceTo(new CoursePoint(a.X + t * dx, a.D + t * dd));
                if (distance <= closest) { closest = distance; segment = i; }
            }
            int station = segment + 1;
            while (station < Centerline.Length - 1 && ball.DistanceTo(Centerline[station]) < 35) station++;
            return Centerline[station];
        }

        public double DistanceFromCenterline(CoursePoint p)
        {
            double best = double.PositiveInfinity;
            for (int i = 1; i < Centerline.Length; i++)
            {
                var a = Centerline[i - 1]; var b = Centerline[i];
                double dx = b.X - a.X, dd = b.D - a.D;
                double l2 = Math.Max(dx * dx + dd * dd, 0.0001);
                double t = Math.Min(Math.Max(((p.X - a.X) * dx + (p.D - a.D) * dd) / l2, 0), 1);
                best = Math.Min(best, p.DistanceTo(new CoursePoint(a.X + dx * t, a.D + dd * t)));
            }
            return best;
        }

        public bool OnLand(CoursePoint p) => Shore == null || Inside(Shore, p);

        /// Even-odd point-in-polygon.
        static bool Inside(CoursePoint[] poly, CoursePoint p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[i]; var b = poly[j];
                if ((a.D > p.D) != (b.D > p.D) && p.X < (b.X - a.X) * (p.D - a.D) / (b.D - a.D) + a.X) inside = !inside;
            }
            return inside;
        }

        public CourseLie LieAt(CoursePoint p)
        {
            foreach (var h in Hazards) if (h.Contains(p)) return h.Kind == HazardKind.Water ? CourseLie.Water : CourseLie.Bunker;
            if (!OnLand(p)) return CourseLie.Water;
            if (p.DistanceTo(Pin) <= GreenRadius) return CourseLie.Green;
            if (p.DistanceTo(Tee) <= 4) return CourseLie.Tee;
            double offset = DistanceFromCenterline(p);
            if (offset <= FairwayWidth / 2) return CourseLie.Fairway;
            if (offset <= FairwayWidth / 2 + RoughWidth) return CourseLie.Rough;
            return CourseLie.OutOfBounds;
        }
    }

    public sealed class Course
    {
        public string Name;
        public Hole[] Holes;
        public int Par { get { int p = 0; foreach (var h in Holes) p += h.Par; return p; } }

        static CoursePoint P(double x, double d) => new(x, d);
        static CourseHazard Bunker(double x, double d, double w, double l) => new(HazardKind.Bunker, x, d, w, l);

        /// Cliffside: the Blender-built island hole (blender/hole_07.blend), in the flight model's
        /// yards. Tee at the origin, the hole running straight up +D, ocean off the east cliffs.
        /// The numbers come from blender/scripts/hole07_design.py: metres from the tee marker
        /// times 1.0936; the shore is the island's control polygon.
        public static Course Cliffside() => new()
        {
            Name = "Cliffside",
            Holes = new[]
            {
                new Hole
                {
                    Number = 7, Par = 4,
                    Centerline = new[] { P(0, 0), P(-8.7, 206.7), P(8.7, 312.8), P(18.6, 415.6) },
                    FairwayWidth = 48, GreenRadius = 26,
                    RoughWidth = 100, // the whole island top plays as rough; the shore decides water
                    Hazards = new[]
                    {
                        Bunker(-41.6, 319.3, 35.0, 21.9), Bunker(-30.6, 280.0, 26.2, 17.5), Bunker(50.3, 253.7, 32.8, 21.9),
                        Bunker(-35.0, 382.8, 28.4, 19.7), Bunker(54.7, 374.0, 30.6, 21.9), Bunker(50.3, 430.9, 24.1, 17.5),
                    },
                    Shore = new[]
                    {
                        P(-17, -46), P(33, -42), P(77, -15), P(94, 28), P(79, 79), P(85, 120), P(68, 162), P(39, 201), P(57, 241), P(101, 273),
                        P(92, 319), P(70, 359), P(98, 394), P(79, 435), P(57, 468), P(24, 499), P(-13, 490), P(-42, 464), P(-59, 427), P(-63, 383),
                        P(-90, 339), P(-70, 295), P(-94, 249), P(-101, 206), P(-116, 160), P(-107, 101), P(-92, 52), P(-77, 7), P(-46, -28),
                    },
                },
            },
        };

        /// Meadow Run: the iOS app's easy course, hole for hole.
        public static Course Meadow() => new()
        {
            Name = "Meadow Run",
            Holes = new[]
            {
                new Hole { Number = 1, Par = 4, Centerline = new[] { P(0, 0), P(0, 185), P(38, 245), P(105, 330) }, FairwayWidth = 50, GreenRadius = 20,
                    Hazards = new[] { Bunker(-22, 174, 16, 27), Bunker(60, 255, 18, 30), Bunker(86, 324, 14, 21) } },
                new Hole { Number = 2, Par = 4, Centerline = new[] { P(0, 0), P(0, 170), P(-18, 280) }, FairwayWidth = 46, GreenRadius = 20,
                    Hazards = new[] { Bunker(23, 172, 16, 22), Bunker(-38, 272, 12, 16) } },
                new Hole { Number = 3, Par = 3, Centerline = new[] { P(0, 0), P(8, 115) }, FairwayWidth = 50, GreenRadius = 20,
                    Hazards = new[] { Bunker(-12, 108, 14, 18) } },
            },
        };
    }
}
