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
        /// How the server and the saved choice name it ("cliffside").
        public string Key = "";
        public Hole[] Holes;

        /// Every course with modelled holes, in the order the course screen shows them.
        public static Course[] All() => new[] { Cliffside() };

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
