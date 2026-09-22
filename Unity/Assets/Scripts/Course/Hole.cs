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
        /// What the hole is called on the card and in the showcase.
        public string Name = "";
        /// A line about the hole for the showcase card.
        public string Blurb = "";
        /// The render of the hole for its card, a Resources path.
        public string Picture => $"Course/hole_{Number:00}_card";
        public CoursePoint[] Centerline;
        public double FairwayWidth;
        public double GreenRadius;
        public CourseHazard[] Hazards = Array.Empty<CourseHazard>();
        /// Dry land, as a polygon in course yards; anything outside it is water. Null means the
        /// hole is inland and the tree line is the only edge.
        public CoursePoint[] Shore;
        /// Further dry land, one polygon each, for holes that carry water between islands.
        public CoursePoint[][] Islets = Array.Empty<CoursePoint[]>();

        /// The lie of the land: the roll follows its slope and the green read draws it. A
        /// modelled hole samples it off its meshes when the model is placed.
        public ISurface Surface = FlatSurface.Instance;

        /// Rough on each side of the fairway. Beyond it (the tree line) is out of bounds.
        public double RoughWidth = 24.0;
        /// The game's ball is drawn about 2.55× a real one (0.12 yd across); the cup is scaled a
        /// little past that — 3.2× a regulation 4¼" cup, about three balls across — so from the
        /// putting view the hole reads clearly bigger than the ball, the way the arcade games draw it.
        public const double CupScale = 3.2;
        /// The cup's mouth as drawn, yards: a ball whose centre crosses it slowly enough drops,
        /// so a putt that rolls over the dark of the hole goes in.
        public const double CupCaptureRadius = 0.054 * 1.0936 * CupScale;

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

        public bool OnLand(CoursePoint p)
        {
            if (Shore == null || Inside(Shore, p)) return true;
            foreach (var islet in Islets) if (Inside(islet, p)) return true;
            return false;
        }

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

        /// Cliffside: the Blender-built island holes, in the flight model's yards, each with its
        /// tee at the origin and the hole running straight up +D. Hole 7 (blender/hole_07.blend)
        /// is the par-4 island with the ocean off the east cliffs; its numbers come from
        /// blender/scripts/hole07_design.py, metres from the tee marker times 1.0936, and the shore
        /// is the island's control polygon. Hole 12 (blender/hole_12.blend) is the par-3 island
        /// carry: tee island, a bridge over the water, and the green on its own island; its
        /// numbers are what blender/scripts/hole12_prepare.py prints, the shores traced off the
        /// turf meshes.
        public static Course Cliffside() => new()
        {
            Name = "Cliffside",
            Holes = new[]
            {
                new Hole
                {
                    Number = 7, Par = 4, Name = "Cliffside",
                    Blurb = "A long par 4 along the clifftop: the fairway bends right past the bunkers to a green perched above the sea.",
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
                new Hole
                {
                    Number = 12, Par = 3, Name = "Island Carry",
                    Blurb = "A par 3 across the water: carry the channel to the green island, where three bunkers guard the front and the lighthouse keeps watch.",
                    Centerline = new[] { P(0, 0), P(0, 197.9) },
                    FairwayWidth = 36, GreenRadius = 22,
                    RoughWidth = 100, // both islands play as rough off the fairway; the shores decide water
                    Hazards = new[]
                    {
                        Bunker(-24.8, 166.4, 24.2, 34.9), Bunker(28.8, 164.3, 24.2, 32.4), Bunker(-28.6, 200.6, 14.5, 22.4),
                    },
                    // The tee island with its bridge approach and the eastern promontory...
                    Shore = new[]
                    {
                        P(26.3, 23.4), P(17.6, 27.9), P(10.6, 36.2), P(5.5, 38.3), P(-18.4, 29.1), P(-39.2, 14.8), P(-40.1, 9.8), P(-35.8, 0.0),
                        P(-34.6, -13.1), P(-25.3, -30.1), P(-20.7, -32.7), P(-4.5, -31.1), P(5.0, -34.5), P(15.2, -33.6), P(28.5, -26.1), P(31.6, -12.0),
                        P(40.0, -1.0), P(43.5, 0.4), P(51.0, -3.3), P(58.5, 2.5), P(65.4, 32.4), P(68.2, 34.4), P(74.3, 28.4), P(77.6, 30.5),
                        P(83.3, 41.8), P(84.6, 63.0), P(88.5, 80.9), P(84.8, 110.2), P(79.4, 120.6), P(74.6, 137.4), P(71.1, 137.0), P(65.1, 127.5),
                        P(54.3, 103.2), P(57.4, 52.8), P(51.3, 64.3), P(47.0, 64.6), P(39.7, 57.9), P(29.8, 45.1), P(28.9, 37.3), P(30.9, 25.0),
                    },
                    // ...and the green island across the water.
                    Islets = new[]
                    {
                        new[]
                        {
                            P(46.8, 185.9), P(34.5, 214.0), P(20.1, 225.3), P(12.1, 239.3), P(6.3, 242.9), P(0.0, 241.4), P(-21.0, 227.4), P(-29.3, 217.8),
                            P(-40.2, 209.6), P(-44.8, 203.3), P(-45.8, 194.8), P(-40.9, 178.3), P(-39.6, 156.1), P(-28.9, 127.4), P(-23.6, 123.0),
                            P(-5.1, 125.9), P(5.7, 120.0), P(11.7, 119.2), P(28.0, 129.0), P(32.6, 134.3), P(35.1, 141.9), P(36.1, 158.1), P(46.6, 178.3),
                        },
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
