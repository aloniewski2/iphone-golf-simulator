using System;
using System.Collections.Generic;
using GolfArcade.Shot;

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

    /// (Fringe is last so the older values keep their numbers.)
    public enum CourseLie { Tee, Fairway, Rough, Bunker, Green, Water, OutOfBounds, Fringe }

    public static class CourseLies
    {
        public static string Label(this CourseLie lie) => lie switch
        {
            CourseLie.Tee => "Tee",
            CourseLie.Fairway => "Fairway",
            CourseLie.Rough => "Rough",
            CourseLie.Bunker => "Bunker",
            CourseLie.Green => "Green",
            CourseLie.Fringe => "Fringe",
            CourseLie.Water => "Water",
            _ => "Out of bounds",
        };

        /// What the lie costs in club speed. The sand wedge is made for the sand and cuts through
        /// the rough; long clubs lose far more to both; a putt off the fringe barely notices.
        public static double PowerFactor(this CourseLie lie, GolfClub club = GolfClub.Iron) => lie switch
        {
            CourseLie.Rough => club.Family() == ClubFamily.Wedge ? 0.9 : club == GolfClub.Putter ? 0.7 : 0.85,
            CourseLie.Bunker => club.Family() == ClubFamily.Wedge ? 0.85 : club == GolfClub.Putter ? 0.4 : 0.6,
            CourseLie.Fringe => club == GolfClub.Putter ? 1 : 0.97,
            _ => 1,
        };

        /// Backspin kept from the lie. Grass trapped between the face and the ball in the rough
        /// kills the spin (a flyer: it comes out hot, won't stop and runs on); sand takes some too.
        public static double SpinFactor(this CourseLie lie) => lie switch
        {
            CourseLie.Rough => 0.55,
            CourseLie.Bunker => 0.8,
            CourseLie.Fringe => 0.9,
            _ => 1,
        };

        /// Degrees added to the launch: the club is picked steeply out of the grass or the sand.
        public static double LaunchChange(this CourseLie lie) => lie switch
        {
            CourseLie.Rough => 1.5,
            CourseLie.Bunker => 3,
            _ => 0,
        };

        /// How the ground takes a ball coming down on it: `soft` soaks up the bounce (0 fairway,
        /// 1 dead) and `grab` takes the run out of it. Greens are receptive, rough smothers the
        /// ball and sand plugs it.
        public static (double soft, double grab) Landing(this CourseLie lie) => lie switch
        {
            CourseLie.Green => (0.15, 0.1),
            CourseLie.Rough => (0.45, 0.5),
            CourseLie.Bunker => (0.9, 0.85),
            _ => (0, 0),
        };

        /// Where a putter is the club: the green, and its fringe.
        public static bool IsPuttingSurface(this CourseLie lie) => lie is CourseLie.Green or CourseLie.Fringe;

        /// The launch of a strike from this lie: the club's launch for the meter reading, then
        /// what the lie does to it — speed, spin, launch — and a thin strike's low, hot flight.
        public static BallFlight.Launch LaunchFrom(this CourseLie lie, GolfClub club, double power, double startLine, double curve, double thin = 0, double speed = 1)
        {
            var l = club.Launch(power, startLine, curve, lie.PowerFactor(club) * speed);
            l.SpinRPM *= lie.SpinFactor();
            l.LaunchAngleDegrees += lie.LaunchChange();
            double t = double.IsFinite(thin) ? Math.Max(0, Math.Min(1, thin)) : 0;
            l.LaunchAngleDegrees *= 1 - 0.45 * t;
            l.SpinRPM *= 1 - 0.5 * t;
            return l;
        }

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
        /// The height of the ground anywhere on the hole, yards (the modelled hole's meshes, set
        /// when it is placed): a ball in flight meets it — lands on a rise, or strikes a cliff
        /// and drops. Null for a hole without a model, which is flat.
        public Func<CoursePoint, double> Ground;
        /// Trees, bushes, rocks and walls standing on the hole (the modelled hole's, read off its
        /// meshes when it is placed): a ball in flight or rolling meets them.
        public Obstacle[] Obstacles = Array.Empty<Obstacle>();

        /// Rough on each side of the fairway. Beyond it (the tree line) is out of bounds.
        public double RoughWidth = 24.0;
        /// The collar of shorter grass round the green, yards.
        public double FringeWidth = 2.5;
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
            // Round a bend (the Spiral winds a whole turn), look as far along as a straight
            // shot could still go without leaving the fairway's corridor or the land.
            while (station < Centerline.Length - 1 && ball.DistanceTo(Centerline[station + 1]) <= 250 && InTheCorridor(ball, Centerline[station + 1]))
                station++;
            return Centerline[station];
        }

        bool InTheCorridor(CoursePoint from, CoursePoint to)
        {
            for (int k = 1; k <= 12; k++)
            {
                double t = k / 12.0;
                var p = new CoursePoint(from.X + (to.X - from.X) * t, from.D + (to.D - from.D) * t);
                if (!OnLand(p) || DistanceFromCenterline(p) > FairwayWidth / 2 + 10) return false;
            }
            return true;
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
            if (p.DistanceTo(Pin) <= GreenRadius + FringeWidth) return CourseLie.Fringe;
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
        static CourseHazard Water(double x, double d, double w, double l) => new(HazardKind.Water, x, d, w, l);

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
                new Hole
                {
                    Number = 13, Par = 5, Name = "The Spiral",
                    Blurb = "A par 5 that winds once round the pinnacle, climbing all the way: bend it left with the fairway, then pitch up to the summit green.",
                    Centerline = new[] { P(0.0, 0.0), P(36.8, 17.9), P(69.7, 38.8), P(93.1, 68.4), P(105.1, 103.1), P(105.1, 138.6), P(93.8, 171.0), P(73.1, 197.1), P(46.0, 214.3), P(15.9, 221.3), P(-13.8, 218.2), P(-39.7, 205.9), P(-59.3, 186.5), P(-70.9, 162.8), P(-73.9, 137.7), P(-68.6, 114.1), P(-56.4, 94.6), P(-39.3, 81.0), P(-19.6, 74.2), P(-0.0, 74.4), P(4.4, 138.9) },
                    FairwayWidth = 33, GreenRadius = 23,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(120.6, 170.4, 21.9, 13.1), Bunker(-29.6, 185.1, 19.7, 13.1), Bunker(-76.3, 82.8, 21.9, 13.1), Bunker(-25.2, 144.4, 15.3, 10.9), Bunker(23.0, 117.0, 13.1, 9.8) },
                    Shore = new[] { P(155.9, 144.2), P(158.0, 160.7), P(149.7, 186.6), P(113.9, 228.1), P(103.9, 233.7), P(101.7, 239.2), P(84.6, 253.1), P(54.7, 267.0), P(44.2, 270.0), P(38.1, 268.5), P(33.3, 271.6), P(0.3, 269.0), P(-10.6, 270.3), P(-21.1, 266.1), P(-26.8, 267.5), P(-37.2, 264.1), P(-59.6, 263.1), P(-63.9, 259.9), P(-86.2, 256.2), P(-112.8, 237.5), P(-133.7, 212.2), P(-150.0, 177.6), P(-152.2, 161.4), P(-146.0, 124.1), P(-136.0, 104.0), P(-126.8, 61.2), P(-118.5, 40.9), P(-113.6, 37.8), P(-111.7, 32.2), P(-106.1, 30.0), P(-103.6, 24.8), P(-94.0, 19.3), P(-86.9, 10.6), P(-47.9, -9.4), P(-27.2, -16.8), P(-0.3, -20.9), P(26.8, -19.7), P(42.7, -16.0), P(51.5, -8.6), P(57.0, -8.0), P(73.2, 7.6), P(78.5, 9.2), P(84.7, 19.1), P(90.2, 21.0), P(100.4, 34.0), P(105.4, 36.7), P(131.1, 72.0), P(131.5, 77.8), P(149.8, 117.9), P(150.4, 128.4) },
                },
                new Hole
                {
                    Number = 14, Par = 4, Name = "The Witch's Lair",
                    Blurb = "A par 4 to a green sunk in a crater: find the gap in the rim, or chip down over the pines into the lair.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 31.7), P(-6.6, 93.0), P(0.0, 158.6), P(10.9, 224.2), P(8.7, 278.9), P(4.4, 318.2), P(2.2, 340.1), P(2.2, 353.2), P(5.5, 382.8) },
                    FairwayWidth = 42, GreenRadius = 22,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(-35.0, 169.5, 24.1, 15.3), Bunker(35.0, 237.3, 26.2, 17.5), Bunker(20.8, 320.4, 10.9, 10.9), Bunker(-14.2, 392.6, 13.1, 8.7) },
                    Shore = new[] { P(-6.6, -40.5), P(-1.1, -38.1), P(20.6, -37.1), P(49.9, -23.0), P(64.2, -6.1), P(72.4, 14.8), P(76.1, 46.4), P(70.6, 102.9), P(76.9, 127.9), P(79.0, 155.1), P(73.7, 183.3), P(72.8, 210.6), P(87.1, 279.4), P(90.5, 286.3), P(89.4, 290.1), P(98.2, 334.4), P(101.3, 378.3), P(94.8, 422.2), P(80.4, 445.1), P(63.5, 459.1), P(38.4, 469.2), P(22.5, 473.7), P(0.6, 474.9), P(-15.7, 473.0), P(-31.6, 468.5), P(-36.4, 463.9), P(-41.8, 464.0), P(-61.0, 453.6), P(-77.0, 438.5), P(-94.9, 404.8), P(-98.7, 366.5), P(-91.4, 329.5), P(-76.3, 286.5), P(-71.7, 265.4), P(-70.9, 248.7), P(-72.2, 238.0), P(-77.4, 227.6), P(-76.7, 206.2), P(-79.3, 200.1), P(-73.7, 152.1), P(-81.3, 90.4), P(-74.2, 42.1), P(-59.6, 1.0), P(-37.5, -30.8), P(-28.0, -35.8) },
                },
                new Hole
                {
                    Number = 15, Par = 3, Name = "The Steps",
                    Blurb = "A par 3 over the water to a green of three tiers: the pin is on the middle step, so land on it or putt up and down the stairs.",
                    Centerline = new[] { P(0.0, 0.0), P(8.7, 148.7) },
                    FairwayWidth = 39, GreenRadius = 50,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(-39.4, 85.3, 19.7, 10.9), Bunker(37.2, 86.4, 19.7, 10.9) },
                    Shore = new[] { P(23.9, 9.9), P(23.0, 15.7), P(8.6, 25.0), P(-7.8, 28.7), P(-20.1, 17.4), P(-23.4, 6.3), P(-21.4, -4.9), P(-16.0, -7.8), P(-13.2, -13.0), P(-2.2, -16.6), P(3.5, -14.8), P(21.0, -1.2) },
                    Islets = new[] { new[] { P(-33.6, 66.9), P(-22.9, 64.4), P(-11.6, 65.8), P(-6.3, 63.2), P(26.4, 68.0), P(45.2, 79.0), P(55.5, 97.6), P(59.7, 147.1), P(56.3, 163.7), P(60.1, 196.9), P(59.3, 219.3), P(49.8, 239.1), P(26.6, 253.1), P(5.2, 257.4), P(-5.8, 256.6), P(-11.0, 253.4), P(-21.8, 252.8), P(-36.6, 245.4), P(-51.4, 229.7), P(-55.8, 213.2), P(-58.9, 209.7), P(-59.8, 170.8), P(-57.0, 159.8), P(-58.8, 154.2), P(-57.0, 133.1), P(-60.6, 115.4), P(-57.5, 94.3), P(-48.0, 74.7) } },
                },
                new Hole
                {
                    Number = 19, Par = 4, Name = "Volcano Rim",
                    Blurb = "A par 4 under a smoking volcano: carry the river of lava off the tee, then follow the ridge right to a green on a ledge beneath the crater.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 32.8), P(-4.4, 98.4), P(-6.6, 164.0), P(-4.4, 203.4), P(-2.2, 242.8), P(4.4, 295.3), P(15.3, 336.8), P(24.1, 369.6), P(28.4, 382.8), P(37.2, 411.2) },
                    FairwayWidth = 44, GreenRadius = 19,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(-32.8, 295.3, 21.9, 13.1), Bunker(37.2, 323.7, 19.7, 13.1), Bunker(8.7, 428.7, 15.3, 10.9), Bunker(61.2, 393.7, 13.1, 10.9), Bunker(-24.1, 131.2, 19.7, 13.1), Water(8.7, 205.6, 201.2, 13.1), Water(109.4, 284.3, 35.0, 65.6), Water(122.5, 371.8, 21.9, 87.5), Water(96.2, 492.1, 24.1, 24.1) },
                    Shore = new[] { P(-18.9, -32.1), P(2.6, -32.0), P(23.8, -26.6), P(51.7, -8.8), P(68.9, 10.8), P(82.2, 47.3), P(99.3, 135.0), P(113.3, 169.4), P(115.3, 181.3), P(118.8, 184.4), P(119.8, 196.8), P(122.8, 200.7), P(122.1, 207.5), P(127.4, 222.4), P(128.9, 239.2), P(132.4, 243.8), P(131.2, 249.8), P(135.6, 265.6), P(135.8, 276.4), P(139.7, 281.9), P(139.0, 292.3), P(146.2, 314.5), P(144.9, 318.9), P(148.6, 325.2), P(174.6, 425.9), P(181.0, 480.3), P(180.9, 502.7), P(173.2, 534.1), P(155.1, 561.3), P(125.7, 575.3), P(103.9, 577.3), P(66.8, 567.7), P(44.5, 553.6), P(29.7, 537.5), P(5.5, 501.2), P(-21.1, 450.9), P(-24.9, 448.9), P(-30.2, 437.7), P(-56.3, 403.6), P(-76.8, 364.4), P(-81.4, 348.0), P(-89.4, 333.9), P(-88.5, 327.5), P(-92.3, 323.1), P(-91.7, 317.1), P(-99.0, 302.1), P(-99.4, 291.0), P(-102.5, 285.9), P(-101.1, 280.3), P(-104.5, 275.0), P(-104.1, 258.9), P(-106.8, 253.2), P(-109.1, 214.6), P(-100.1, 105.1), P(-89.6, 45.4), P(-76.8, 9.6), P(-64.7, -6.6), P(-62.2, -13.7), P(-40.6, -29.7), P(-29.7, -30.7), P(-24.8, -34.0) },
                },
                new Hole
                {
                    Number = 20, Par = 5, Name = "Frostbite Fjord",
                    Blurb = "A long par 5 round a frozen lake: play it safe along the pines, or cut the corner over the ice for a shot at the green in two.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 32.8), P(-14.2, 98.4), P(-20.8, 175.0), P(-16.4, 251.5), P(-3.3, 328.1), P(20.8, 393.7), P(55.8, 448.4), P(93.0, 483.4), P(117.0, 498.7), P(143.3, 512.9) },
                    FairwayWidth = 44, GreenRadius = 21,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(14.2, 175.0, 19.7, 13.1), Bunker(-51.4, 284.3, 21.9, 13.1), Bunker(44.8, 406.8, 19.7, 13.1), Bunker(165.1, 494.3, 15.3, 10.9), Bunker(123.6, 529.3, 13.1, 8.7), Water(109.4, 262.5, 102.8, 177.2), Water(178.3, 287.6, 43.7, 10.9) },
                    Shore = new[] { P(204.0, 273.6), P(206.0, 312.0), P(203.4, 316.5), P(205.5, 323.1), P(198.4, 348.6), P(199.3, 355.4), P(186.0, 390.9), P(163.6, 439.9), P(160.3, 452.5), P(156.6, 454.8), P(155.1, 461.6), P(133.2, 500.0), P(105.3, 526.1), P(73.8, 534.2), P(68.3, 530.5), P(47.7, 525.1), P(28.5, 513.3), P(13.9, 507.8), P(9.8, 502.0), P(0.3, 498.2), P(-3.5, 492.6), P(-12.8, 488.4), P(-36.8, 458.4), P(-43.3, 441.7), P(-46.9, 438.7), P(-54.2, 416.6), P(-59.9, 408.7), P(-60.1, 401.4), P(-65.2, 392.8), P(-76.3, 356.2), P(-75.3, 349.3), P(-79.3, 340.0), P(-81.0, 312.2), P(-78.6, 274.1), P(-71.3, 236.3), P(-72.2, 230.6), P(-64.8, 209.9), P(-65.5, 198.1), P(-61.4, 188.5), P(-62.2, 176.4), P(-51.3, 83.5), P(-37.1, 42.0), P(-25.5, 23.6), P(1.6, -3.0), P(24.9, -18.1), P(35.3, -19.6), P(56.3, -27.8), P(88.8, -24.9), P(115.0, -5.7), P(130.5, 19.2), P(134.4, 20.9), P(156.2, 59.1), P(166.4, 85.3), P(175.8, 116.0), P(184.7, 182.6), P(187.8, 186.8), P(186.9, 193.2), P(195.8, 224.6), P(196.4, 241.1), P(200.5, 246.3), P(200.5, 262.5) },
                },
                new Hole
                {
                    Number = 21, Par = 3, Name = "Mesa Canyon",
                    Blurb = "A par 3 from one mesa to the next over a canyon of water: carry the chasm to a green on the far mesa's top, cacti all round.",
                    Centerline = new[] { P(0.0, 0.0), P(16.4, 165.1) },
                    FairwayWidth = 39, GreenRadius = 21,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(43.7, 164.0, 17.5, 13.1), Bunker(-13.1, 183.7, 13.1, 8.7) },
                    Shore = new[] { P(50.0, 25.1), P(49.7, 30.5), P(39.7, 43.8), P(23.4, 58.5), P(12.5, 60.7), P(-14.8, 63.1), P(-31.2, 59.7), P(-44.7, 42.9), P(-42.5, 4.1), P(-37.4, -11.7), P(-24.3, -22.3), P(-18.0, -21.9), P(-13.6, -25.9), P(-2.9, -29.2), P(13.1, -24.4), P(46.2, 4.6), P(50.1, 9.1) },
                    Islets = new[] { new[] { P(83.2, 174.3), P(72.1, 199.3), P(62.2, 212.4), P(21.2, 230.0), P(4.9, 232.4), P(-16.1, 226.6), P(-23.1, 217.9), P(-32.4, 197.9), P(-33.6, 186.4), P(-40.1, 176.7), P(-43.8, 154.7), P(-35.6, 140.2), P(-35.2, 134.1), P(-26.1, 127.1), P(-20.1, 117.6), P(4.2, 104.7), P(10.2, 107.5), P(15.3, 106.1), P(65.8, 127.9), P(74.5, 134.1), P(84.6, 147.1), P(85.6, 163.5) } },
                },
                new Hole
                {
                    Number = 22, Par = 4, Name = "Temple Falls",
                    Blurb = "A par 4 over the lagoon: carry the waterfall's pool and the stone step with the drive, then pitch to the green under the temple.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 27.3), P(2.2, 65.6), P(0.0, 109.4), P(0.0, 135.6), P(0.0, 164.0), P(0.0, 199.0), P(0.0, 220.9), P(-2.2, 253.7), P(-6.6, 297.5), P(-8.7, 341.2), P(-10.9, 367.4), P(-8.7, 397.0) },
                    FairwayWidth = 35, GreenRadius = 19,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(-32.8, 94.0, 19.7, 13.1), Bunker(32.8, 264.7, 19.7, 13.1), Bunker(-37.2, 400.3, 13.1, 8.7), Bunker(15.3, 380.6, 13.1, 8.7), Water(-6.6, 166.2, 126.9, 41.6) },
                    Shore = new[] { P(-26.7, -41.3), P(-16.1, -45.6), P(-10.5, -43.9), P(0.3, -46.7), P(5.8, -43.6), P(11.3, -45.4), P(22.0, -43.6), P(27.0, -39.1), P(32.4, -39.9), P(58.9, -20.9), P(67.7, -6.7), P(74.5, 7.8), P(81.6, 34.8), P(91.3, 112.0), P(94.3, 115.0), P(93.4, 122.5), P(97.9, 137.1), P(103.3, 175.0), P(102.1, 186.8), P(105.1, 208.2), P(102.2, 219.3), P(104.1, 230.3), P(99.1, 289.5), P(103.4, 367.5), P(100.1, 399.9), P(93.8, 426.8), P(75.3, 460.2), P(58.9, 474.7), P(44.1, 481.9), P(33.5, 484.4), P(22.5, 483.3), P(17.2, 486.2), P(11.6, 484.0), P(-10.1, 485.0), P(-25.7, 477.8), P(-36.2, 476.7), P(-63.0, 457.8), P(-72.8, 444.3), P(-80.4, 429.9), P(-92.2, 387.5), P(-97.5, 355.3), P(-102.6, 288.7), P(-105.5, 278.9), P(-104.2, 272.5), P(-107.8, 245.9), P(-104.9, 239.9), P(-107.7, 229.3), P(-102.8, 180.6), P(-104.5, 174.3), P(-102.8, 152.1), P(-100.3, 148.1), P(-100.3, 130.5), P(-97.9, 126.7), P(-99.4, 119.3), P(-97.1, 115.8), P(-98.1, 108.6), P(-92.0, 60.0), P(-83.1, 16.7), P(-80.4, 14.1), P(-77.7, 1.5), P(-64.1, -22.2), P(-52.1, -33.4), P(-32.1, -41.9) },
                },
                new Hole
                {
                    Number = 23, Par = 4, Name = "Windmill Links",
                    Blurb = "A par 4 through the tulip fields: two canals cross the fairway and a pond guards the green, with the windmill turning all the while.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 30.6), P(0.0, 85.3), P(0.0, 150.9), P(2.2, 216.5), P(4.4, 282.1), P(6.6, 336.8), P(10.9, 360.9), P(17.5, 391.5) },
                    FairwayWidth = 44, GreenRadius = 19,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(28.4, 129.0, 19.7, 13.1), Bunker(-26.2, 238.4, 19.7, 13.1), Bunker(-6.6, 411.2, 15.3, 10.9), Bunker(39.4, 367.4, 13.1, 8.7), Water(0.0, 156.4, 192.5, 8.7), Water(0.0, 287.6, 192.5, 8.7), Water(63.4, 387.1, 33.9, 25.2) },
                    Shore = new[] { P(-40.8, -25.0), P(-19.3, -29.6), P(-8.2, -27.2), P(30.1, -27.8), P(61.9, -19.6), P(71.7, -14.7), P(91.5, 3.3), P(103.2, 34.0), P(109.2, 82.5), P(106.9, 166.5), P(109.5, 182.0), P(108.7, 198.8), P(111.8, 209.4), P(108.9, 236.6), P(111.7, 248.2), P(108.9, 258.4), P(110.9, 264.8), P(107.5, 274.4), P(107.1, 290.7), P(109.2, 297.8), P(106.7, 312.4), P(109.0, 325.4), P(107.2, 339.6), P(109.0, 363.4), P(104.5, 407.0), P(92.0, 437.5), P(70.8, 454.4), P(65.2, 454.5), P(45.1, 463.7), P(6.8, 467.1), P(-20.4, 464.2), P(-26.1, 466.7), P(-47.8, 462.9), P(-73.3, 453.2), P(-90.7, 440.5), P(-92.7, 433.2), P(-96.8, 431.3), P(-107.7, 389.1), P(-107.7, 278.0), P(-111.7, 246.1), P(-109.1, 234.7), P(-111.9, 224.0), P(-111.2, 201.8), P(-108.5, 196.8), P(-110.1, 185.3), P(-108.1, 180.4), P(-109.2, 135.4), P(-107.2, 120.8), P(-109.6, 96.9), P(-105.8, 42.7), P(-102.9, 26.3), P(-92.1, 1.3), P(-67.5, -19.9), P(-56.7, -21.6), P(-52.0, -25.5) },
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
