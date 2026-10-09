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
    /// Where a ball lies. Ice (Frostbite Fjord's frozen lake) is played from like the fairway,
    /// but a ball skids across it.
    public enum CourseLie { Tee, Fairway, Rough, Bunker, Green, Water, OutOfBounds, Fringe, Ice }

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
            CourseLie.Ice => "Ice",
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

        /// How little the ground lets a landing ball's spin grip, 0–1: ice none at all.
        public static double Slide(this CourseLie lie) => lie == CourseLie.Ice ? 1 : 0;

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

    /// Lava plays as water (a penalty stroke and a drop) and is drawn as lava; ice is a surface
    /// the ball skids over (CourseLie.Ice).
    public enum HazardKind { Bunker, Water, Lava, Ice }

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
        /// The course's look for the modelled hole's colours (HoleView.Themes); "" is Cliffside's.
        public string Theme = "";
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
        /// Windmill Links' turning sails (the modelled hole's, set when it is placed): a ball in
        /// flight meets a blade or slips through, depending on when it gets there.
        public SpinningSails Windmill;

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

        /// The hazard a point is in, if any: so the game can tell lava from water (both are
        /// CourseLie.Water to the rules).
        public HazardKind? HazardAt(CoursePoint p)
        {
            foreach (var h in Hazards) if (h.Contains(p)) return h.Kind;
            // a hole in the crater floats on lava: what is not land is lava
            if (SeaIsLava && !OnLand(p)) return HazardKind.Lava;
            return null;
        }

        /// The Magma Open's holes float on the crater's lava: their sea is lava, so a ball that leaves the land burns.
        public bool SeaIsLava => Theme == "magma";

        public CourseLie LieAt(CoursePoint p)
        {
            foreach (var h in Hazards)
                if (h.Contains(p))
                    return h.Kind switch { HazardKind.Water or HazardKind.Lava => CourseLie.Water, HazardKind.Ice => CourseLie.Ice, _ => CourseLie.Bunker };
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
        /// How the server and the saved choice name it: "cliffside", "wildisles".
        public string Key = "";
        public Hole[] Holes;

        /// Every course with modelled holes, in the order the course screen shows them.
        public static Course[] All() => new[] { Cliffside(), Postcards(), WildIsles(), Magma() };

        /// The course a hole belongs to, by its number (hole numbers are unique across courses).
        public static Course Containing(int holeNumber)
        {
            foreach (var c in All()) if (Array.Exists(c.Holes, h => h.Number == holeNumber)) return c;
            return null;
        }

        public static Course ByKey(string key)
        {
            foreach (var c in All()) if (c.Key == key) return c;
            return null;
        }
        public int Par { get { int p = 0; foreach (var h in Holes) p += h.Par; return p; } }

        static CoursePoint P(double x, double d) => new(x, d);
        static CourseHazard Bunker(double x, double d, double w, double l) => new(HazardKind.Bunker, x, d, w, l);
        static CourseHazard Water(double x, double d, double w, double l) => new(HazardKind.Water, x, d, w, l);
        static CourseHazard Lava(double x, double d, double w, double l) => new(HazardKind.Lava, x, d, w, l);
        static CourseHazard Ice(double x, double d, double w, double l) => new(HazardKind.Ice, x, d, w, l);

        /// Cliffside: the Blender-built island holes, in the flight model's yards, each with its
        /// tee at the origin and the hole running straight up +D. Hole 7 (blender/hole_07.blend)
        /// is the par-4 island with the ocean off the east cliffs; its numbers come from
        /// blender/scripts/hole07_design.py, metres from the tee marker times 1.0936, and the shore
        /// is the island's control polygon. Hole 12 (blender/hole_12.blend) is the par-3 island
        /// carry: tee island, a bridge over the water, and the green on its own island; its
        /// numbers are what blender/scripts/hole12_prepare.py prints, the shores traced off the
        /// turf meshes.
        public static Course Postcards() => new()
        {
            Name = "Postcards", Key = "postcards",
            Holes = new[]
            {
                new Hole
                {
                    // Needle: 202-point shore, centerline 342.9 yd, play surface flat at 6 m in blender/hole_08.blend
                    Number = 8, Par = 4,
                    Centerline = new[] { P(0, 0), P(19, 178), P(-6, 340) },
                    FairwayWidth = 16, GreenRadius = 14, RoughWidth = 8,
                    Hazards = new[]
                    {
                        Water(10.1, 90, 48, 94),
                        Water(6.6, 258, 48, 88),
                        Bunker(-12, 316, 9, 20),
                    },
                    Shore = new[]
                    {
                        P(15.8, -1.7), P(16.2, 2.3), P(16.7, 6.3), P(16.9, 10.3), P(16.5, 14.2), P(17.2, 18.1),
                        P(18.2, 22), P(18.7, 26), P(18.7, 30), P(18.7, 34), P(17.3, 37.7), P(15.8, 41.4),
                        P(15.9, 45.4), P(16.3, 49.4), P(15.3, 53.1), P(13, 56.4), P(11.9, 60.1), P(12.4, 64.1),
                        P(12.8, 68.1), P(13.2, 72.1), P(13.6, 76), P(14.1, 80), P(14.5, 84), P(14.9, 88),
                        P(15.3, 92), P(15.8, 95.9), P(16.2, 99.9), P(16.6, 103.9), P(17, 107.9), P(17.5, 111.8),
                        P(17.9, 115.8), P(18.3, 119.8), P(20.6, 123), P(23.5, 125.8), P(24.9, 129.4), P(25.3, 133.4),
                        P(26.3, 137.2), P(28.6, 140.4), P(30.5, 143.9), P(31.1, 147.9), P(32, 151.8), P(32.6, 155.7),
                        P(32.6, 159.7), P(32.4, 163.7), P(32.3, 167.7), P(32.7, 171.7), P(33.1, 175.7), P(33.1, 179.7),
                        P(32.6, 183.6), P(32.2, 187.6), P(32.1, 191.6), P(32.1, 195.6), P(31.7, 199.6), P(30.8, 203.5),
                        P(29.8, 207.4), P(28.3, 211), P(25.8, 214.1), P(24, 217.6), P(23.4, 221.6), P(22.5, 225.5),
                        P(19.7, 228.2), P(16.8, 231), P(15.8, 234.8), P(15.2, 238.8), P(14.6, 242.7), P(14, 246.7),
                        P(13.4, 250.6), P(12.7, 254.6), P(12.1, 258.5), P(11.5, 262.5), P(10.9, 266.4), P(10.3, 270.4),
                        P(9.7, 274.4), P(9.1, 278.3), P(8.5, 282.3), P(8, 286.2), P(9.7, 289.8), P(11.8, 293.2),
                        P(11.7, 297.2), P(11.1, 301.1), P(11.4, 305.1), P(12.8, 308.8), P(13.2, 312.7), P(12.5, 316.7),
                        P(12.4, 320.7), P(12.4, 324.7), P(11.8, 328.6), P(11.2, 332.6), P(10.6, 336.5), P(10, 340.5),
                        P(9.3, 344.4), P(7.7, 348.1), P(5.3, 351.2), P(2.1, 353.7), P(-1.6, 355.3), P(-5.5, 355.9),
                        P(-9.5, 355.5), P(-13.2, 354.2), P(-16.5, 351.9), P(-19.2, 348.9), P(-21, 345.4), P(-21.8, 341.5),
                        P(-21.7, 337.5), P(-21.1, 333.5), P(-20.5, 329.6), P(-19.9, 325.6), P(-19.3, 321.6), P(-18.6, 317.7),
                        P(-18, 313.7), P(-17.4, 309.8), P(-16, 306.1), P(-13.4, 303.1), P(-11.1, 299.8), P(-10.3, 295.9),
                        P(-9.7, 292), P(-7.5, 288.7), P(-4.4, 286.2), P(-2.7, 282.7), P(-2.1, 278.7), P(-1.5, 274.8),
                        P(-0.9, 270.8), P(-0.3, 266.9), P(0.3, 262.9), P(0.9, 259), P(1.6, 255), P(2.2, 251),
                        P(2.8, 247.1), P(3.4, 243.1), P(4, 239.2), P(4.6, 235.2), P(5.2, 231.3), P(4.4, 227.5),
                        P(2.2, 224.1), P(1.3, 220.3), P(1.9, 216.4), P(2.3, 212.4), P(1.1, 208.6), P(0, 204.7),
                        P(0.7, 200.9), P(1.8, 197), P(2.5, 193.1), P(2.7, 189.1), P(2.6, 185.1), P(2.9, 181.1),
                        P(3.5, 177.2), P(2.9, 173.2), P(2.7, 169.2), P(2.8, 165.2), P(3, 161.2), P(2.8, 157.2),
                        P(2, 153.3), P(1, 149.4), P(1.1, 145.5), P(2.6, 141.8), P(3.6, 137.9), P(3.2, 134),
                        P(2.9, 130), P(4.5, 126.4), P(6.8, 123.1), P(7.2, 119.2), P(6.8, 115.2), P(6.3, 111.3),
                        P(5.9, 107.3), P(5.5, 103.3), P(5.1, 99.3), P(4.6, 95.3), P(4.2, 91.4), P(3.8, 87.4),
                        P(3.4, 83.4), P(2.9, 79.4), P(2.5, 75.4), P(2.1, 71.5), P(1.7, 67.5), P(1.2, 63.5),
                        P(0.6, 59.6), P(-2.1, 56.7), P(-4.9, 53.8), P(-5.7, 49.9), P(-6.2, 46), P(-7.5, 42.2),
                        P(-9.8, 39), P(-11.2, 35.3), P(-10.9, 31.3), P(-11.1, 27.3), P(-11.8, 23.3), P(-12.9, 19.5),
                        P(-14, 15.6), P(-14.7, 11.7), P(-15.2, 7.7), P(-15.6, 3.8), P(-15.9, -0.2), P(-15.3, -4.2),
                        P(-13.8, -7.9), P(-11.4, -11.1), P(-8.3, -13.6), P(-4.7, -15.2), P(-0.7, -15.9), P(3.2, -15.6),
                        P(7, -14.3), P(10.3, -12.1), P(13, -9.1), P(14.9, -5.6),
                    },
                },
                new Hole
                {
                    // Split: 415-point shore, centerline 494.1 yd, play surface flat at 6 m in blender/hole_09.blend
                    Number = 9, Par = 5,
                    Centerline = new[] { P(0, 0), P(3, 60), P(6.3, 125), P(9.5, 190), P(11.8, 235), P(6, 416), P(-1, 454), P(-10, 492) },
                    FairwayWidth = 18, GreenRadius = 17, RoughWidth = 90,
                    Hazards = new[]
                    {
                        Water(8.8, 352, 84, 102),
                        Bunker(-2.5, 184, 11, 24),
                        Bunker(21.8, 200, 11, 24),
                        Bunker(9, 466, 16, 16),
                    },
                    Shore = new[]
                    {
                        P(-93.6, 25.8), P(-93.5, 20.8), P(-93.2, 15.8), P(-92.6, 10.8), P(-91.8, 5.9), P(-90.6, 1),
                        P(-88, -3.3), P(-84.5, -6.9), P(-80.6, -9.9), P(-76.3, -12.5), P(-71.9, -14.8), P(-67.4, -17.1),
                        P(-62.9, -19.3), P(-58.4, -21.4), P(-53.9, -23.4), P(-49.3, -25.4), P(-44.6, -27.2), P(-39.8, -28.8),
                        P(-35, -30.1), P(-30.2, -31.2), P(-25.2, -32.1), P(-20.3, -32.8), P(-15.3, -33.3), P(-10.3, -33.6),
                        P(-5.3, -33.6), P(-0.3, -33.2), P(4.6, -32.6), P(9.5, -31.5), P(14.2, -29.8), P(18.3, -27),
                        P(21.3, -23), P(22.8, -18.3), P(23.3, -13.3), P(23, -8.3), P(22.5, -3.3), P(22.2, 1.6),
                        P(22.1, 6.6), P(22.3, 11.6), P(22.5, 16.6), P(22.7, 21.6), P(22.9, 26.6), P(23.1, 31.6),
                        P(23.3, 36.6), P(23.5, 41.6), P(23.6, 46.6), P(23.7, 51.6), P(23.9, 56.6), P(24, 61.6),
                        P(24.1, 66.6), P(24.2, 71.6), P(24.3, 76.6), P(24.4, 81.6), P(24.6, 86.6), P(24.8, 91.6),
                        P(25, 96.6), P(25.3, 101.6), P(25.5, 106.6), P(25.8, 111.6), P(26.2, 116.6), P(26.5, 121.6),
                        P(26.9, 126.6), P(27.3, 131.5), P(27.7, 136.5), P(28.1, 141.5), P(28.5, 146.5), P(28.9, 151.5),
                        P(29.3, 156.5), P(29.6, 161.5), P(30, 166.4), P(30.3, 171.4), P(30.6, 176.4), P(30.9, 181.4),
                        P(31.2, 186.4), P(31.4, 191.4), P(31.7, 196.4), P(31.9, 201.4), P(32.1, 206.4), P(32.2, 211.4),
                        P(32.4, 216.4), P(32.5, 221.4), P(32.5, 226.4), P(32.5, 231.4), P(32.5, 236.4), P(32.4, 241.4),
                        P(32.3, 246.4), P(32.2, 251.4), P(32.1, 256.4), P(32, 261.4), P(31.9, 266.4), P(31.8, 271.4),
                        P(31.7, 276.4), P(31.7, 281.4), P(31.6, 286.4), P(31.6, 291.4), P(31.4, 296.4), P(30.7, 301.3),
                        P(26.8, 303.9), P(22.1, 302.6), P(17.3, 301.1), P(12.3, 300.3), P(7.3, 300.1), P(2.4, 300.7),
                        P(-2.5, 301.8), P(-7.4, 302), P(-10.3, 298.2), P(-10.6, 293.2), P(-10.2, 288.2), P(-9.8, 283.2),
                        P(-9.6, 278.2), P(-9.3, 273.2), P(-9.1, 268.3), P(-8.9, 263.3), P(-8.7, 258.3), P(-8.6, 253.3),
                        P(-8.5, 248.3), P(-8.4, 243.3), P(-8.4, 238.3), P(-8.5, 233.3), P(-8.6, 228.3), P(-8.8, 223.3),
                        P(-9, 218.3), P(-9.4, 213.3), P(-9.7, 208.3), P(-10.1, 203.3), P(-10.5, 198.3), P(-10.9, 193.3),
                        P(-11.3, 188.3), P(-11.7, 183.4), P(-12.1, 178.4), P(-12.5, 173.4), P(-12.9, 168.4), P(-13.2, 163.4),
                        P(-13.6, 158.4), P(-13.9, 153.4), P(-14.3, 148.4), P(-14.6, 143.5), P(-14.9, 138.5), P(-15.1, 133.5),
                        P(-15.4, 128.5), P(-15.6, 123.5), P(-15.8, 118.5), P(-16, 113.5), P(-16.2, 108.5), P(-16.3, 103.5),
                        P(-16.5, 98.5), P(-16.6, 93.5), P(-16.7, 88.5), P(-16.9, 83.5), P(-17, 78.5), P(-17.1, 73.5),
                        P(-17.2, 68.5), P(-17.4, 63.5), P(-17.5, 58.5), P(-17.8, 53.5), P(-18.3, 48.5), P(-19.6, 43.7),
                        P(-22.3, 39.5), P(-26.1, 36.3), P(-30.7, 34.5), P(-35.7, 34.2), P(-40.5, 35.5), P(-44.7, 38.1),
                        P(-47.9, 42), P(-49.7, 46.6), P(-50.3, 51.6), P(-50.4, 56.6), P(-50.3, 61.6), P(-50.2, 66.6),
                        P(-50.1, 71.6), P(-50, 76.6), P(-49.8, 81.6), P(-49.7, 86.6), P(-49.5, 91.6), P(-49.3, 96.6),
                        P(-49, 101.6), P(-48.7, 106.6), P(-48.4, 111.5), P(-48.1, 116.5), P(-47.7, 121.5), P(-47.3, 126.5),
                        P(-46.9, 131.5), P(-46.4, 136.5), P(-45.9, 141.4), P(-45.4, 146.4), P(-44.8, 151.4), P(-44.3, 156.4),
                        P(-43.7, 161.3), P(-43.1, 166.3), P(-42.5, 171.3), P(-41.9, 176.2), P(-41.2, 181.2), P(-40.6, 186.1),
                        P(-40, 191.1), P(-39.4, 196.1), P(-38.8, 201), P(-38.3, 206), P(-37.7, 211), P(-37.3, 216),
                        P(-36.9, 220.9), P(-36.5, 225.9), P(-36.3, 230.9), P(-36.3, 235.9), P(-36.2, 240.9), P(-36.3, 245.9),
                        P(-36.5, 250.9), P(-36.8, 255.9), P(-37.1, 260.9), P(-37.5, 265.9), P(-38, 270.9), P(-38.5, 275.9),
                        P(-39.1, 280.8), P(-39.7, 285.8), P(-40.3, 290.7), P(-41, 295.7), P(-41.7, 300.6), P(-42.4, 305.6),
                        P(-43.1, 310.6), P(-43.8, 315.5), P(-44.5, 320.5), P(-45.1, 325.4), P(-45.7, 330.4), P(-46.3, 335.4),
                        P(-46.8, 340.3), P(-47.3, 345.3), P(-47.7, 350.3), P(-48.1, 355.3), P(-48.4, 360.3), P(-48.6, 365.3),
                        P(-48.8, 370.3), P(-49, 375.3), P(-49.1, 380.3), P(-49.1, 385.3), P(-49.2, 390.3), P(-49.2, 395.3),
                        P(-49.2, 400.3), P(-49.2, 405.3), P(-49.2, 410.3), P(-49.4, 415.3), P(-49.8, 420.2), P(-50.4, 425.2),
                        P(-51.1, 430.2), P(-51.9, 435.1), P(-52.8, 440), P(-53.6, 445), P(-54.4, 449.9), P(-53.3, 454.8),
                        P(-51.2, 459.3), P(-47.8, 462.9), P(-43.4, 465.3), P(-38.6, 466.4), P(-33.6, 465.9), P(-29, 464),
                        P(-25.2, 460.8), P(-22.6, 456.6), P(-21.1, 451.8), P(-20, 447), P(-18.9, 442.1), P(-17.9, 437.2),
                        P(-16.9, 432.3), P(-16, 427.4), P(-15.2, 422.4), P(-14.5, 417.5), P(-14, 412.5), P(-13.7, 407.5),
                        P(-12.8, 402.6), P(-8.8, 400.2), P(-4, 401.6), P(0.8, 403), P(5.7, 403.8), P(10.7, 403.8),
                        P(15.7, 403.2), P(20.6, 402.2), P(25.3, 403.1), P(27.2, 407.6), P(26.9, 412.6), P(26, 417.5),
                        P(25.2, 422.4), P(24.8, 427.4), P(24.8, 432.4), P(25.1, 437.4), P(25.7, 442.4), P(26.3, 447.3),
                        P(27, 452.3), P(27.9, 457.2), P(28.8, 462.1), P(29.9, 467), P(30.6, 471.9), P(30.9, 476.9),
                        P(30.9, 481.9), P(30.5, 486.9), P(29.6, 491.8), P(28.3, 496.7), P(26.5, 501.3), P(24.4, 505.9),
                        P(21.8, 510.1), P(18.7, 514.1), P(15.2, 517.6), P(11.3, 520.7), P(6.9, 523.1), P(2.2, 524.9),
                        P(-2.6, 526.2), P(-7.5, 527.1), P(-12.5, 527.6), P(-17.5, 527.9), P(-22.5, 527.8), P(-27.5, 527.4),
                        P(-32.4, 526.8), P(-37.3, 525.9), P(-42.2, 524.7), P(-46.9, 523.1), P(-51.6, 521.3), P(-56.1, 519.1),
                        P(-60.4, 516.6), P(-64.6, 513.8), P(-68.6, 510.9), P(-72.7, 508), P(-76.6, 504.9), P(-80.5, 501.7),
                        P(-84.2, 498.4), P(-87.4, 494.5), P(-89.7, 490.1), P(-91.2, 485.4), P(-91.5, 480.4), P(-91.2, 475.4),
                        P(-91.1, 470.4), P(-91.5, 465.5), P(-92.5, 460.6), P(-93.2, 455.6), P(-93.3, 450.6), P(-92.9, 445.6),
                        P(-92.4, 440.7), P(-91.7, 435.7), P(-90.7, 430.8), P(-89.2, 426.1), P(-88, 421.2), P(-88.4, 416.2),
                        P(-89.8, 411.5), P(-90.7, 406.5), P(-90.8, 401.6), P(-90.4, 396.6), P(-89.7, 391.6), P(-88.7, 386.7),
                        P(-87.6, 381.8), P(-86.4, 377), P(-86, 372), P(-86.5, 367), P(-87.5, 362.2), P(-87.8, 357.2),
                        P(-87.4, 352.2), P(-86.4, 347.3), P(-85.1, 342.4), P(-83.8, 337.6), P(-82.9, 332.7), P(-82.4, 327.7),
                        P(-82.5, 322.7), P(-83.1, 317.8), P(-83.6, 312.8), P(-83.4, 307.8), P(-82.6, 302.9), P(-81.3, 298.1),
                        P(-79.4, 293.4), P(-77.8, 288.7), P(-77, 283.8), P(-76.8, 278.8), P(-76.7, 273.8), P(-76.6, 268.8),
                        P(-76.2, 263.8), P(-75.4, 258.9), P(-74.1, 254), P(-72.4, 249.3), P(-70.3, 244.8), P(-69, 240),
                        P(-68.8, 235), P(-69.6, 230.1), P(-70.2, 225.1), P(-70.6, 220.1), P(-70.9, 215.1), P(-71.1, 210.1),
                        P(-71.1, 205.1), P(-70.6, 200.2), P(-70, 195.2), P(-70.7, 190.3), P(-72.8, 185.8), P(-75.1, 181.3),
                        P(-76.8, 176.6), P(-77.9, 171.8), P(-78.8, 166.9), P(-79.8, 161.9), P(-80.5, 157), P(-80.5, 152),
                        P(-80.3, 147), P(-81.4, 142.2), P(-83.7, 137.7), P(-85.9, 133.3), P(-87, 128.4), P(-87, 123.4),
                        P(-86.9, 118.4), P(-87, 113.4), P(-86.8, 108.4), P(-86, 103.5), P(-85.1, 98.6), P(-85.7, 93.7),
                        P(-87.7, 89.1), P(-89.2, 84.3), P(-89.3, 79.4), P(-88.2, 74.5), P(-87.7, 69.5), P(-87.7, 64.5),
                        P(-87.6, 59.5), P(-87.1, 54.5), P(-86.9, 49.6), P(-88.2, 44.8), P(-90.4, 40.3), P(-92.2, 35.6),
                        P(-93.3, 30.7),
                    },
                },
                new Hole
                {
                    // Crater: 175-point shore, centerline 356.5 yd, play surface flat at 6 m in blender/hole_10.blend
                    Number = 10, Par = 4,
                    Centerline = new[] { P(0, 0), P(14.3, 66.2), P(50.2, 123.4), P(107.1, 159.3), P(261, 164.7) },
                    FairwayWidth = 22, GreenRadius = 16, RoughWidth = 13,
                    Hazards = new[]
                    {
                        Water(171.1, 161.5, 112, 76),
                        Bunker(57.4, 110.3, 17, 12),
                        Bunker(74.3, 154, 18, 13),
                    },
                    Shore = new[]
                    {
                        P(-15.6, -8.6), P(-15.5, -3.8), P(-14.8, 1), P(-14, 5.7), P(-13.1, 10.5), P(-12.3, 15.2),
                        P(-12, 20), P(-11.7, 24.8), P(-11.2, 29.6), P(-10.5, 34.4), P(-9.7, 39.1), P(-8.7, 43.8),
                        P(-7.8, 48.5), P(-7, 53.3), P(-6.3, 58), P(-5.7, 62.8), P(-5, 67.6), P(-3.8, 72.2),
                        P(-1.9, 76.6), P(0.5, 80.8), P(3, 84.9), P(5.6, 88.9), P(8.3, 93), P(10.9, 97),
                        P(13.5, 101.1), P(15.9, 105.2), P(18.3, 109.4), P(20.5, 113.7), P(22.7, 118), P(25, 122.2),
                        P(27.3, 126.4), P(29.8, 130.5), P(32.6, 134.5), P(35.7, 138.1), P(39.5, 141), P(43.7, 143.4),
                        P(47.9, 145.7), P(52.1, 148.1), P(56, 150.8), P(59.7, 153.9), P(63.2, 157.2), P(66.8, 160.4),
                        P(70.7, 163.2), P(75, 165.4), P(79.5, 167.2), P(83.9, 168.9), P(88.4, 170.7), P(92.9, 172.4),
                        P(97.4, 174.2), P(102, 175.7), P(106.7, 176.3), P(111.5, 175.9), P(116.2, 175.1), P(121, 174.6),
                        P(125.8, 174.5), P(130.6, 174.6), P(135.4, 174.8), P(140.2, 175), P(145.1, 175.1), P(149.9, 175.3),
                        P(154.7, 175.5), P(159.5, 175.6), P(164.3, 175.8), P(169.1, 176), P(173.9, 176.2), P(178.7, 176.3),
                        P(183.5, 176.5), P(188.3, 176.7), P(193.1, 176.8), P(198, 177), P(202.8, 177.2), P(207.6, 177.3),
                        P(212.4, 177.5), P(217.2, 177.7), P(222, 177.8), P(226.8, 178), P(231.6, 178.1), P(236.4, 178.5),
                        P(241.1, 179.6), P(245.4, 181.7), P(249.5, 184.2), P(253.8, 186.2), P(258.5, 187.3), P(263.3, 187.4),
                        P(268, 186.4), P(272.4, 184.4), P(276.3, 181.6), P(279.4, 178), P(281.8, 173.8), P(283.2, 169.2),
                        P(283.7, 164.5), P(283.2, 159.7), P(281.6, 155.2), P(279.2, 151.1), P(275.9, 147.6), P(272, 144.8),
                        P(267.6, 142.9), P(262.9, 142), P(258.1, 142.1), P(253.4, 143.3), P(249.1, 145.3), P(244.8, 147.5),
                        P(240.2, 148.9), P(235.4, 149.3), P(230.6, 149.2), P(225.8, 149), P(221, 148.8), P(216.2, 148.6),
                        P(211.4, 148.4), P(206.6, 148.3), P(201.8, 148.1), P(196.9, 147.9), P(192.1, 147.8), P(187.3, 147.6),
                        P(182.5, 147.4), P(177.7, 147.3), P(172.9, 147.1), P(168.1, 146.9), P(163.3, 146.8), P(158.5, 146.6),
                        P(153.7, 146.4), P(148.9, 146.3), P(144, 146.1), P(139.2, 145.9), P(134.4, 145.8), P(129.6, 145.6),
                        P(124.8, 145.3), P(120, 144.7), P(115.5, 143), P(111.5, 140.4), P(107.6, 137.6), P(103.5, 135),
                        P(99.4, 132.5), P(95.5, 129.7), P(91.7, 126.8), P(87.9, 123.8), P(84.2, 120.7), P(80.4, 117.7),
                        P(76.6, 114.9), P(72.7, 112.1), P(69, 108.9), P(66, 105.2), P(63.1, 101.3), P(60.2, 97.5),
                        P(57.1, 93.8), P(54, 90.1), P(51.1, 86.3), P(48.2, 82.5), P(45.3, 78.7), P(42.3, 74.9),
                        P(39.4, 71), P(36.5, 67.2), P(34, 63.1), P(32.4, 58.6), P(31.7, 53.8), P(31.2, 49),
                        P(30.6, 44.2), P(29.7, 39.5), P(28.5, 34.9), P(26.8, 30.4), P(24.9, 25.9), P(22.9, 21.5),
                        P(21, 17.2), P(19, 12.7), P(17.3, 8.2), P(16.1, 3.6), P(15, -1.1), P(13.8, -5.7),
                        P(12.4, -10.4), P(10.2, -14.5), P(6, -16.3), P(1.2, -15.8), P(-3.5, -14.8), P(-8.2, -13.8),
                        P(-12.7, -12.2),
                    },
                },
            },
        };
        public static Course Cliffside() => new()
        {
            Name = "Cliffside", Key = "cliffside",
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
                    Blurb = "A par 5 up the road that winds round the hill: follow the bend left, or cut across the slope through the pines, then pitch onto the summit green.",
                    Centerline = new[] { P(0.0, 0.0), P(25.2, 0.0), P(60.5, 10.3), P(90.0, 29.8), P(111.5, 56.0), P(123.7, 86.1), P(126.2, 116.9), P(119.8, 145.7), P(105.8, 170.0), P(86.2, 187.9), P(63.5, 198.6), P(40.0, 201.7), P(18.2, 197.7), P(-0.2, 188.0), P(-13.6, 174.1), P(-21.4, 158.0), P(-23.6, 141.7), P(-20.8, 126.9), P(23.0, 134.5) },
                    FairwayWidth = 39, GreenRadius = 22,
                    RoughWidth = 100,
                    Hazards = new[] { Bunker(148.1, 107.4, 21.9, 13.1), Bunker(48.6, 181.1, 19.7, 13.1), Bunker(-36.5, 174.7, 17.5, 10.9), Bunker(-3.3, 142.2, 15.3, 10.9), Bunker(44.8, 115.9, 13.1, 9.8) },
                    Shore = new[] { P(185.6, 138.8), P(188.1, 155.2), P(181.2, 181.6), P(146.3, 224.2), P(136.3, 230.0), P(134.2, 235.5), P(118.0, 250.5), P(89.0, 266.3), P(78.7, 270.1), P(72.5, 269.1), P(67.9, 272.5), P(23.9, 273.7), P(13.2, 269.7), P(7.6, 271.1), P(-2.9, 267.6), P(-25.3, 266.7), P(-29.8, 263.5), P(-52.3, 261.2), P(-72.1, 252.4), P(-89.4, 239.2), P(-104.2, 222.9), P(-108.4, 212.6), P(-113.0, 209.0), P(-123.8, 189.9), P(-131.2, 163.6), P(-130.5, 136.6), P(-111.1, 85.0), P(-106.1, 51.9), P(-93.3, 27.8), P(-88.1, 25.1), P(-82.8, 15.0), P(-46.2, -9.4), P(-26.5, -19.3), P(-0.4, -27.3), P(37.5, -34.1), P(53.5, -29.8), P(59.3, -31.1), P(68.7, -24.2), P(83.9, -18.8), P(144.1, 37.8), P(153.6, 51.3), P(167.1, 81.6), P(167.9, 92.7), P(182.2, 122.9) },
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
            },
        };

        /// Wild Isles: five islands, each its own world, built by course_builder.py from
        /// blender/scripts/hole16_volcano_design.py … hole20_windmill_design.py with the themes, the
        /// water, lava and ice, and the landmarks of course_extras.py — a lava river under a smoking
        /// volcano, a frozen lake, two red mesas over a canyon, a jungle temple above a waterfall,
        /// and tulip fields round a turning windmill.
        public static Course WildIsles() => new()
        {
            Name = "Wild Isles", Key = "wildisles",
            Holes = new[]
            {
                new Hole
                {
                    Number = 16, Par = 4, Name = "Volcano Rim",
                    Blurb = "A par 4 under a smoking volcano: carry the river of lava off the tee, then follow the ridge right to a green on a ledge beneath the crater.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 32.8), P(-4.4, 98.4), P(-6.6, 164.0), P(-4.4, 203.4), P(-2.2, 242.8), P(4.4, 295.3), P(15.3, 336.8), P(24.1, 369.6), P(28.4, 382.8), P(37.2, 411.2) },
                    FairwayWidth = 44, GreenRadius = 19,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(-32.8, 295.3, 21.9, 13.1), Bunker(37.2, 323.7, 19.7, 13.1), Bunker(8.7, 428.7, 15.3, 10.9), Bunker(61.2, 393.7, 13.1, 10.9), Bunker(-24.1, 131.2, 19.7, 13.1), Lava(8.7, 205.6, 201.2, 13.1), Lava(109.4, 284.3, 35.0, 65.6), Lava(122.5, 371.8, 21.9, 87.5), Lava(96.2, 492.1, 24.1, 24.1) },
                    Shore = new[] { P(-18.9, -32.1), P(2.6, -32.0), P(23.8, -26.6), P(51.7, -8.8), P(68.9, 10.8), P(82.2, 47.3), P(99.3, 135.0), P(113.3, 169.4), P(115.3, 181.3), P(118.8, 184.4), P(119.8, 196.8), P(122.8, 200.7), P(122.1, 207.5), P(127.4, 222.4), P(128.9, 239.2), P(132.4, 243.8), P(131.2, 249.8), P(135.6, 265.6), P(135.8, 276.4), P(139.7, 281.9), P(139.0, 292.3), P(146.2, 314.5), P(144.9, 318.9), P(148.6, 325.2), P(174.6, 425.9), P(181.0, 480.3), P(180.9, 502.7), P(173.2, 534.1), P(155.1, 561.3), P(125.7, 575.3), P(103.9, 577.3), P(66.8, 567.7), P(44.5, 553.6), P(29.7, 537.5), P(5.5, 501.2), P(-21.1, 450.9), P(-24.9, 448.9), P(-30.2, 437.7), P(-56.3, 403.6), P(-76.8, 364.4), P(-81.4, 348.0), P(-89.4, 333.9), P(-88.5, 327.5), P(-92.3, 323.1), P(-91.7, 317.1), P(-99.0, 302.1), P(-99.4, 291.0), P(-102.5, 285.9), P(-101.1, 280.3), P(-104.5, 275.0), P(-104.1, 258.9), P(-106.8, 253.2), P(-109.1, 214.6), P(-100.1, 105.1), P(-89.6, 45.4), P(-76.8, 9.6), P(-64.7, -6.6), P(-62.2, -13.7), P(-40.6, -29.7), P(-29.7, -30.7), P(-24.8, -34.0) },
                },
                new Hole
                {
                    Number = 17, Par = 5, Name = "Frostbite Fjord",
                    Blurb = "A long par 5 round a frozen lake: play it safe along the pines, or skid one across the ice to cut the corner for a shot at the green in two.",
                    Centerline = new[] { P(0.0, 0.0), P(-2.2, 30.6), P(-17.5, 85.3), P(-24.1, 161.9), P(-19.7, 238.4), P(-6.6, 315.0), P(17.5, 380.6), P(52.5, 435.3), P(89.7, 468.1), P(109.4, 481.2), P(135.6, 493.2) },
                    FairwayWidth = 44, GreenRadius = 21,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(10.9, 161.9, 19.7, 13.1), Bunker(-54.7, 271.2, 21.9, 13.1), Bunker(41.6, 393.7, 19.7, 13.1), Bunker(157.5, 474.6, 15.3, 10.9), Bunker(115.9, 509.6, 13.1, 8.7), Ice(106.1, 249.3, 102.8, 177.2), Ice(175.0, 274.5, 43.7, 10.9) },
                    Shore = new[] { P(200.8, 253.1), P(202.5, 285.5), P(204.9, 291.5), P(202.5, 296.3), P(205.4, 302.6), P(200.8, 328.7), P(202.3, 335.4), P(168.8, 491.4), P(155.1, 527.2), P(141.8, 543.9), P(137.1, 545.6), P(133.2, 551.3), P(127.5, 550.7), P(117.2, 554.6), P(106.4, 554.4), P(90.7, 549.8), P(72.0, 539.5), P(67.5, 533.4), P(58.9, 529.1), P(54.7, 523.1), P(45.9, 519.2), P(28.4, 503.9), P(19.3, 497.8), P(14.2, 497.3), P(1.0, 485.8), P(-4.1, 484.9), P(-28.7, 463.3), P(-55.2, 415.3), P(-57.9, 403.1), P(-61.7, 400.1), P(-81.3, 337.0), P(-85.0, 321.2), P(-86.5, 293.6), P(-82.1, 271.2), P(-83.3, 260.6), P(-78.7, 249.8), P(-79.2, 238.9), P(-72.6, 217.9), P(-66.1, 168.3), P(-63.4, 164.2), P(-59.7, 102.7), P(-52.6, 59.7), P(-35.9, 18.9), P(-15.0, -6.5), P(-10.3, -7.9), P(11.3, -26.1), P(31.2, -35.7), P(63.3, -41.6), P(68.8, -39.6), P(74.3, -42.2), P(79.7, -40.9), P(94.8, -33.8), P(111.1, -20.2), P(144.8, 29.8), P(159.5, 59.8), P(171.1, 96.2), P(177.7, 134.0), P(178.1, 152.2), P(181.3, 155.6), P(182.1, 173.4), P(187.6, 188.1), P(185.9, 195.0), P(191.1, 204.4), P(192.1, 221.3), P(198.3, 242.3), P(197.5, 247.9) },
                },
                new Hole
                {
                    Number = 18, Par = 3, Name = "Mesa Canyon",
                    Blurb = "A par 3 from one mesa to the next over a canyon of water: carry the chasm to a green on the far mesa's top, cacti all round.",
                    Centerline = new[] { P(0.0, 0.0), P(16.4, 165.1) },
                    FairwayWidth = 39, GreenRadius = 21,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(43.7, 164.0, 17.5, 13.1), Bunker(-13.1, 183.7, 13.1, 8.7) },
                    Shore = new[] { P(50.0, 25.1), P(49.7, 30.5), P(39.7, 43.8), P(23.4, 58.5), P(12.5, 60.7), P(-14.8, 63.1), P(-31.2, 59.7), P(-44.7, 42.9), P(-42.5, 4.1), P(-37.4, -11.7), P(-24.3, -22.3), P(-18.0, -21.9), P(-13.6, -25.9), P(-2.9, -29.2), P(13.1, -24.4), P(46.2, 4.6), P(50.1, 9.1) },
                    Islets = new[] { new[] { P(83.2, 174.3), P(72.1, 199.3), P(62.2, 212.4), P(21.2, 230.0), P(4.9, 232.4), P(-16.1, 226.6), P(-23.1, 217.9), P(-32.4, 197.9), P(-33.6, 186.4), P(-40.1, 176.7), P(-43.8, 154.7), P(-35.6, 140.2), P(-35.2, 134.1), P(-26.1, 127.1), P(-20.1, 117.6), P(4.2, 104.7), P(10.2, 107.5), P(15.3, 106.1), P(65.8, 127.9), P(74.5, 134.1), P(84.6, 147.1), P(85.6, 163.5) } },
                },
                new Hole
                {
                    Number = 19, Par = 4, Name = "Temple Falls",
                    Blurb = "A par 4 over the lagoon: carry the waterfall's pool and the stone step with the drive, then pitch to the green under the temple.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 27.3), P(2.2, 65.6), P(0.0, 109.4), P(0.0, 135.6), P(0.0, 164.0), P(0.0, 199.0), P(0.0, 220.9), P(-2.2, 253.7), P(-6.6, 297.5), P(-8.7, 341.2), P(-10.9, 367.4), P(-8.7, 397.0) },
                    FairwayWidth = 35, GreenRadius = 19,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(-32.8, 94.0, 19.7, 13.1), Bunker(32.8, 264.7, 19.7, 13.1), Bunker(-37.2, 400.3, 13.1, 8.7), Bunker(15.3, 380.6, 13.1, 8.7), Water(-6.6, 166.2, 126.9, 41.6) },
                    Shore = new[] { P(-26.7, -41.3), P(-16.1, -45.6), P(-10.5, -43.9), P(0.3, -46.7), P(5.8, -43.6), P(11.3, -45.4), P(22.0, -43.6), P(27.0, -39.1), P(32.4, -39.9), P(58.9, -20.9), P(67.7, -6.7), P(74.5, 7.8), P(81.6, 34.8), P(91.3, 112.0), P(94.3, 115.0), P(93.4, 122.5), P(97.9, 137.1), P(103.3, 175.0), P(102.1, 186.8), P(105.1, 208.2), P(102.2, 219.3), P(104.1, 230.3), P(99.1, 289.5), P(103.4, 367.5), P(100.1, 399.9), P(93.8, 426.8), P(75.3, 460.2), P(58.9, 474.7), P(44.1, 481.9), P(33.5, 484.4), P(22.5, 483.3), P(17.2, 486.2), P(11.6, 484.0), P(-10.1, 485.0), P(-25.7, 477.8), P(-36.2, 476.7), P(-63.0, 457.8), P(-72.8, 444.3), P(-80.4, 429.9), P(-92.2, 387.5), P(-97.5, 355.3), P(-102.6, 288.7), P(-105.5, 278.9), P(-104.2, 272.5), P(-107.8, 245.9), P(-104.9, 239.9), P(-107.7, 229.3), P(-102.8, 180.6), P(-104.5, 174.3), P(-102.8, 152.1), P(-100.3, 148.1), P(-100.3, 130.5), P(-97.9, 126.7), P(-99.4, 119.3), P(-97.1, 115.8), P(-98.1, 108.6), P(-92.0, 60.0), P(-83.1, 16.7), P(-80.4, 14.1), P(-77.7, 1.5), P(-64.1, -22.2), P(-52.1, -33.4), P(-32.1, -41.9) },
                },
                new Hole
                {
                    Number = 20, Par = 4, Name = "Windmill Links",
                    Blurb = "A par 4 through the tulip fields: two canals cross the fairway and a pond guards the green, with the windmill turning all the while.",
                    Centerline = new[] { P(0.0, 0.0), P(0.0, 30.6), P(0.0, 85.3), P(0.0, 150.9), P(2.2, 216.5), P(4.4, 282.1), P(6.6, 336.8), P(10.9, 360.9), P(17.5, 391.5) },
                    FairwayWidth = 44, GreenRadius = 19,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(28.4, 129.0, 19.7, 13.1), Bunker(-26.2, 238.4, 19.7, 13.1), Bunker(-6.6, 411.2, 15.3, 10.9), Bunker(39.4, 367.4, 13.1, 8.7), Water(0.0, 156.4, 192.5, 8.7), Water(0.0, 287.6, 192.5, 8.7), Water(63.4, 387.1, 33.9, 25.2) },
                    Shore = new[] { P(-40.8, -25.0), P(-19.3, -29.6), P(-8.2, -27.2), P(30.1, -27.8), P(61.9, -19.6), P(71.7, -14.7), P(91.5, 3.3), P(103.2, 34.0), P(109.2, 82.5), P(106.9, 166.5), P(109.5, 182.0), P(108.7, 198.8), P(111.8, 209.4), P(108.9, 236.6), P(111.7, 248.2), P(108.9, 258.4), P(110.9, 264.8), P(107.5, 274.4), P(107.1, 290.7), P(109.2, 297.8), P(106.7, 312.4), P(109.0, 325.4), P(107.2, 339.6), P(109.0, 363.4), P(104.5, 407.0), P(92.0, 437.5), P(70.8, 454.4), P(65.2, 454.5), P(45.1, 463.7), P(6.8, 467.1), P(-20.4, 464.2), P(-26.1, 466.7), P(-47.8, 462.9), P(-73.3, 453.2), P(-90.7, 440.5), P(-92.7, 433.2), P(-96.8, 431.3), P(-107.7, 389.1), P(-107.7, 278.0), P(-111.7, 246.1), P(-109.1, 234.7), P(-111.9, 224.0), P(-111.2, 201.8), P(-108.5, 196.8), P(-110.1, 185.3), P(-108.1, 180.4), P(-109.2, 135.4), P(-107.2, 120.8), P(-109.6, 96.9), P(-105.8, 42.7), P(-102.9, 26.3), P(-92.1, 1.3), P(-67.5, -19.9), P(-56.7, -21.6), P(-52.0, -25.5) },
                },
            },
        };

        /// Magma Open: three holes on the molten lake of a crater — an obsidian slab to carry the lava to, a winding
        /// causeway of black rock, and a horseshoe of rock round a lagoon of lava that can be played round or across —
        /// in the world of Adnan's Volcano venue (LavaWorld, HoleAtmosphere). Built by course_builder.py from
        /// blender/scripts/hole21_slab_design.py … hole23_caldera_design.py; the sea is lava, so a ball that leaves the
        /// rock burns (Hole.SeaIsLava).
        public static Course Magma() => new()
        {
            Name = "Magma Open", Key = "magma",
            Holes = new[]
            {
                new Hole
                {
                    Number = 21, Par = 3, Name = "Obsidian Slab",
                    Blurb = "A par 3 over the molten lake: carry the lava from a slab of black rock to a green on the far side.",
                    Theme = "magma",
                    Centerline = new[] { P(0.0, 0.0), P(2.2, 117.0), P(7.7, 136.7), P(10.9, 149.8), P(12.0, 165.1) },
                    FairwayWidth = 33, GreenRadius = 21,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(-17.5, 143.3, 19.7, 13.1), Bunker(39.4, 156.4, 17.5, 13.1), Bunker(13.1, 188.1, 19.7, 13.1), Bunker(30.6, 133.4, 13.1, 9.8) },
                    Shore = new[] { P(-14.1, -19.9), P(-3.3, -22.3), P(17.8, -16.3), P(28.7, -4.6), P(32.5, 5.7), P(30.0, 39.3), P(24.0, 49.0), P(18.5, 50.6), P(14.5, 55.3), P(-2.2, 57.9), P(-12.8, 54.4), P(-22.4, 49.3), P(-28.4, 40.0), P(-32.6, 12.3), P(-26.2, -9.4), P(-19.7, -18.7) },
                    Islets = new[] { new[] { P(-37.9, 114.1), P(-26.4, 112.2), P(-22.5, 108.5), P(5.4, 106.0), P(33.0, 109.7), P(53.3, 119.1), P(63.7, 131.8), P(70.6, 146.9), P(72.4, 157.9), P(69.6, 163.3), P(69.8, 174.5), P(64.0, 184.2), P(52.5, 196.1), P(28.8, 209.1), P(17.1, 208.7), P(12.1, 211.0), P(-15.5, 209.1), P(-39.7, 196.9), P(-61.0, 178.6), P(-67.0, 151.7), P(-56.8, 126.1) } },
                },
                new Hole
                {
                    Number = 22, Par = 4, Name = "Ember Causeway",
                    Blurb = "A par 4 along a winding causeway of black rock: thread the neck where the lava bites in, and hit the plateau green.",
                    Theme = "magma",
                    Centerline = new[] { P(0.0, 0.0), P(2.2, 41.6), P(15.3, 90.8), P(36.1, 136.7), P(55.8, 183.7), P(60.1, 231.8), P(48.1, 277.8), P(21.9, 319.3), P(2.2, 345.6), P(-2.2, 363.1) },
                    FairwayWidth = 28, GreenRadius = 21,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(27.3, 145.3, 19.7, 14.2), Bunker(44.9, 197.4, 17.5, 13.1), Bunker(51.6, 288.0, 17.5, 13.1), Bunker(18.3, 305.6, 17.5, 13.1), Bunker(-7.1, 326.0, 17.5, 13.1), Bunker(25.4, 345.0, 15.3, 12.0), Bunker(-26.1, 386.2, 15.3, 10.9), Lava(64.4, 158.9, 17.5, 13.1) },
                    Shore = new[] { P(-17.9, -4.5), P(-16.6, -11.5), P(-9.2, -19.2), P(1.5, -20.5), P(15.1, -11.8), P(18.4, -3.3), P(18.1, 42.6), P(23.2, 63.4), P(39.5, 97.7), P(58.2, 124.0), P(65.6, 139.7), P(75.9, 151.6), P(83.5, 172.7), P(87.4, 199.4), P(86.2, 210.4), P(82.1, 215.2), P(81.9, 226.6), P(78.0, 230.8), P(78.4, 237.2), P(75.0, 241.2), P(75.3, 247.8), P(71.6, 251.0), P(57.6, 299.9), P(50.3, 312.3), P(38.8, 344.1), P(35.5, 383.2), P(28.9, 392.5), P(10.8, 403.9), P(-10.9, 406.6), P(-35.3, 395.0), P(-41.4, 383.8), P(-45.1, 382.0), P(-49.7, 366.4), P(-46.3, 344.6), P(-39.8, 335.2), P(17.2, 293.9), P(29.0, 281.9), P(38.3, 268.9), P(45.0, 253.4), P(47.0, 236.5), P(43.5, 221.2), P(29.2, 191.1), P(-5.5, 76.8), P(-13.1, 42.6) },
                },
                new Hole
                {
                    Number = 23, Par = 5, Name = "Caldera Crown",
                    Blurb = "A par 5 round a lagoon of lava: three shots along the ridge, or gamble on the carry straight across.",
                    Theme = "magma",
                    Centerline = new[] { P(0.0, 0.0), P(-37.0, 48.2), P(-44.7, 102.7), P(-25.8, 154.4), P(15.1, 191.3), P(68.4, 204.6), P(121.8, 191.3), P(162.7, 154.4), P(181.6, 102.7), P(173.9, 48.2), P(143.7, 7.0) },
                    FairwayWidth = 37, GreenRadius = 22,
                    RoughWidth = 300,
                    Hazards = new[] { Bunker(-58.4, 67.6, 19.7, 14.2), Bunker(-25.4, 108.2, 19.7, 13.1), Bunker(-21.5, 182.8, 21.9, 14.2), Bunker(117.4, 182.9, 19.7, 13.1), Bunker(190.0, 140.8, 17.5, 13.1), Bunker(174.1, 13.2, 17.5, 13.1), Bunker(143.8, 37.5, 15.3, 12.0), Lava(-27.3, 85.5, 17.5, 13.1), Lava(162.7, 179.6, 17.5, 12.0) },
                    Shore = new[] { P(-16.6, -9.7), P(-8.1, -16.1), P(2.4, -15.2), P(11.0, -7.6), P(14.8, 3.9), P(9.2, 19.0), P(-15.1, 55.1), P(-16.9, 65.9), P(-14.8, 82.8), P(-11.5, 104.3), P(-5.8, 120.0), P(-6.6, 125.8), P(12.1, 159.8), P(21.1, 166.0), P(27.7, 175.6), P(33.8, 176.0), P(37.3, 181.2), P(58.1, 188.3), P(74.6, 190.5), P(90.9, 186.8), P(95.4, 182.5), P(106.1, 179.9), P(117.6, 167.5), P(122.9, 165.5), P(124.4, 159.2), P(134.0, 146.1), P(150.6, 99.1), P(148.7, 93.9), P(150.6, 82.9), P(149.2, 60.8), P(139.2, 47.7), P(112.7, 28.2), P(105.8, 13.7), P(107.7, -9.5), P(113.3, -19.4), P(131.1, -31.8), P(141.8, -33.1), P(162.5, -26.8), P(176.9, -10.8), P(182.4, -1.1), P(183.0, 5.5), P(187.8, 8.5), P(188.3, 15.0), P(192.1, 18.9), P(207.1, 54.3), P(214.2, 86.5), P(212.2, 124.8), P(200.2, 155.6), P(187.8, 173.8), P(166.9, 191.8), P(151.0, 198.3), P(147.5, 202.5), P(121.0, 210.9), P(116.7, 214.6), P(61.9, 221.4), P(40.5, 217.2), P(34.3, 219.3), P(24.4, 214.9), P(18.0, 216.0), P(13.8, 212.3), P(-17.4, 200.9), P(-30.9, 191.5), P(-37.6, 183.3), P(-43.9, 181.1), P(-61.1, 159.5), P(-76.0, 124.0), P(-74.1, 118.3), P(-77.3, 107.5), P(-75.4, 91.2), P(-77.3, 85.5), P(-74.5, 74.8), P(-70.6, 70.1), P(-71.4, 64.3), P(-64.9, 54.9), P(-65.7, 48.7), P(-54.8, 29.5), P(-30.0, -0.2) },
                },
            },
        };

        /// Meadow Run: the iOS app's easy course, hole for hole.
        public static Course Meadow() => new()
        {
            Name = "Meadow Run", Key = "meadow",
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
