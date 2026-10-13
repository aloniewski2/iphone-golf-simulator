using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// A round's twist, dealt before the first tee shot and played all round: party rules on top
    /// of golf (PlayRules), the way a party game deals its modifiers.
    public enum Twist { MoonBall, BounceHouse, TurboGreens, BucketCup, Pinball, BananaBall, Hurricane, StickyGreens }

    /// The hole's weather. It is drawn for each hole, can turn between shots (rain rolls in, a
    /// storm breaks, the sky clears, the fog lifts), and in a gusty wind or a storm the wind
    /// shifts every shot. Fog is still air and a hidden green: the map and the flag show the way.
    public enum Weather { Clear, Gusty, Rain, Storm, Fog }

    /// What falls from the sky in rain and storms, by where the hole is: rain on the islands,
    /// snow over Frostbite Fjord's frozen lake, ash in the Magma Open's crater.
    public enum Sky { Rain, Snow, Ash }

    public static class Twists
    {
        public static readonly Twist[] All = (Twist[])Enum.GetValues(typeof(Twist));

        public static string Title(this Twist t) => t switch
        {
            Twist.MoonBall => "Moon Ball",
            Twist.BounceHouse => "Bounce House",
            Twist.TurboGreens => "Turbo Greens",
            Twist.BucketCup => "Bucket Cup",
            Twist.Pinball => "Pinball",
            Twist.BananaBall => "Banana Ball",
            Twist.Hurricane => "Hurricane",
            _ => "Sticky Greens",
        };

        /// What it does, in a line for its card.
        public static string Line(this Twist t) => t switch
        {
            Twist.MoonBall => "Low gravity: every shot floats and flies farther",
            Twist.BounceHouse => "A rubber ball: huge hops and long runs",
            Twist.TurboGreens => "Greens run twice as fast: easy does it",
            Twist.BucketCup => "The cup is the size of a bucket",
            Twist.Pinball => "Trees and rocks fire the ball back",
            Twist.BananaBall => "Draws and fades bend twice as hard",
            Twist.Hurricane => "Big wind on every hole, shifting every shot",
            _ => "Greens grab the ball: it stops where it lands",
        };

        /// The card's colour (hex) and its icon (UI/Icons, or one painted in code: EventIcons).
        public static string Colour(this Twist t) => t switch
        {
            Twist.MoonBall => "8C7BF2",
            Twist.BounceHouse => "FF7B42",
            Twist.TurboGreens => "2FCB78",
            Twist.BucketCup => "F4BE2C",
            Twist.Pinball => "FF4E98",
            Twist.BananaBall => "F7D63A",
            Twist.Hurricane => "3FA9F5",
            _ => "B26AF6",
        };

        public static string Icon(this Twist t) => t switch
        {
            Twist.MoonBall => "moon",
            Twist.BounceHouse => "bounce",
            Twist.TurboGreens => "speed",
            Twist.BucketCup => "target",
            Twist.Pinball => "star",
            Twist.BananaBall => "curve",
            Twist.Hurricane => "wind",
            _ => "flag",
        };

        /// What it does to the rules.
        public static void Bend(this Twist t, PlayRules r)
        {
            switch (t)
            {
                case Twist.MoonBall: r.Gravity *= 0.45; break;
                case Twist.BounceHouse: r.Bounce *= 2.7; r.FairwayDrag *= 0.75; break;
                case Twist.TurboGreens: r.GreenDrag *= 0.5; break;
                case Twist.BucketCup: r.Cup *= 2.0; break;
                case Twist.Pinball: r.ObstacleKick *= 1.7; break;
                case Twist.BananaBall: r.Curve *= 2.2; break;
                case Twist.StickyGreens: r.StickyGreens = true; break;
            }
        }
    }

    public static class Weathers
    {
        public static Sky SkyOf(Hole hole) =>
            hole == null ? Sky.Rain
            : hole.Theme == "magma" ? Sky.Ash
            : Array.Exists(hole.Hazards ?? Array.Empty<CourseHazard>(), h => h.Kind == HazardKind.Ice) ? Sky.Snow
            : Sky.Rain;

        public static string Title(this Weather w, Sky sky) => w switch
        {
            Weather.Gusty => "Gusty",
            Weather.Rain => sky switch { Sky.Snow => "Snow", Sky.Ash => "Ashfall", _ => "Rain" },
            Weather.Storm => sky switch { Sky.Snow => "Blizzard", Sky.Ash => "Eruption", _ => "Thunderstorm" },
            Weather.Fog => sky switch { Sky.Snow => "Freezing Fog", Sky.Ash => "Volcanic Smoke", _ => "Sea Fog" },
            _ => sky == Sky.Ash ? "Clear" : "Sunshine",
        };

        public static string Line(this Weather w, Sky sky) => w switch
        {
            Weather.Gusty => "The wind shifts every shot",
            Weather.Rain => sky switch { Sky.Snow => "Soft snow: less roll, slower greens", Sky.Ash => "Ash on the turf: less roll, slower greens", _ => "Wet turf: less roll, slower greens" },
            Weather.Storm => sky switch { Sky.Snow => "Whiteout gusts and soft snow", Sky.Ash => "The volcano rumbles: ash and wild wind", _ => "Lightning, wild wind, soaked turf" },
            Weather.Fog => "The green is hidden: trust the flag and the map",
            _ => "Fair conditions",
        };

        public static string Colour(this Weather w, Sky sky) => w switch
        {
            Weather.Gusty => "5FC6E8",
            Weather.Rain => sky switch { Sky.Snow => "9FD8F2", Sky.Ash => "9A8F8A", _ => "4D8FD6" },
            Weather.Storm => sky switch { Sky.Snow => "7FB6D9", Sky.Ash => "F06A2A", _ => "5B5FC7" },
            Weather.Fog => sky == Sky.Ash ? "8E8580" : "9DB2C2",
            _ => "F6B93B",
        };

        public static string Icon(this Weather w, Sky sky) => w switch
        {
            Weather.Gusty => "wind",
            Weather.Rain => sky switch { Sky.Snow => "snowflake", Sky.Ash => "cloud", _ => "rain" },
            Weather.Storm => sky == Sky.Ash ? "flame" : sky == Sky.Snow ? "snowflake" : "bolt",
            Weather.Fog => "fog",
            _ => "sun",
        };

        /// Rain, snow or ash on the ground: the ball sits down instead of running on.
        public static bool IsWet(this Weather w) => w is Weather.Rain or Weather.Storm;
        /// The wind changes from shot to shot.
        public static bool IsGusty(this Weather w) => w is Weather.Gusty or Weather.Storm;
        /// Weather to be proud of playing well in (a bounty asks for par in it).
        public static bool IsRough(this Weather w) => w is Weather.Storm or Weather.Fog;

        public static void Bend(this Weather w, PlayRules r)
        {
            if (!w.IsWet()) return;
            r.FairwayDrag *= 1.6;
            r.GreenDrag *= 1.3;
            r.Bounce *= 0.7;
        }
    }

    /// The round's events: the twists dealt before it starts, each hole's weather (and its turns
    /// between shots), the gusts, and the things moving about each hole. Everything is drawn from
    /// the round's seed, so the same seed gives every phone in a multiplayer round the same round,
    /// and a hole replayed from its seed plays the same. Pure C#, tested without a scene.
    public sealed class RoundEvents
    {
        /// Off for the automated tests' rounds (they set it), so plain golf stays the baseline
        /// everything else is measured against; on for everyone playing.
        public static bool Enabled = true;
        /// Twists dealt each round.
        public const int TwistsPerRound = 2;
        /// For reviews: every kind of mover a hole can take, on every hole.
        public static bool EveryMover;

        public readonly int Seed;
        public readonly Twist[] Dealt;
        public bool Any => Dealt.Length > 0 || weatherOn || challengesOn;
        readonly bool weatherOn, challengesOn;

        /// Plain golf: no twists, clear skies, an empty course, nothing to chase.
        public static readonly RoundEvents None = new(0, Array.Empty<Twist>(), false, false);

        RoundEvents(int seed, Twist[] twists, bool weather, bool challenges) { Seed = seed; Dealt = twists; weatherOn = weather; challengesOn = challenges; }

        /// The round's events from its seed, as the match settings have them: `twists` dealt or
        /// not, the `world`'s — weather and the things moving about the holes — on or not
        /// (clear skies and empty holes), and the `challenges` — golden rings to fly through and
        /// a bounty on every hole — set out or not. None when events are off altogether.
        public static RoundEvents Deal(int seed, bool twists = true, bool world = true, bool challenges = true)
        {
            if (!Enabled || (!twists && !world && !challenges)) return None;
            if (!twists) return new RoundEvents(seed, Array.Empty<Twist>(), world, challenges);
            var rng = new Dice(Mix(seed, 0, 0x7157));
            var pool = new List<Twist>(Twists.All);
            var dealt = new Twist[TwistsPerRound];
            for (int i = 0; i < dealt.Length; i++)
            {
                int k = rng.Next(pool.Count);
                dealt[i] = pool[k];
                pool.RemoveAt(k);
                // the green twists fight each other (a bucket on a turbo green is still a turbo green): one of them a round
                if (dealt[i] is Twist.TurboGreens or Twist.StickyGreens) pool.RemoveAll(t => t is Twist.TurboGreens or Twist.StickyGreens);
            }
            return new RoundEvents(seed, dealt, world, challenges);
        }

        /// The round as another phone (the host) dealt it: its seed and its Code.
        public static RoundEvents FromCode(int seed, string code)
        {
            int colon = string.IsNullOrEmpty(code) ? -1 : code.IndexOf(':');
            if (colon < 1) return None;
            string flags = code.Substring(0, colon);
            var list = new List<Twist>();
            foreach (var part in code.Substring(colon + 1).Split(','))
                if (int.TryParse(part, out int n) && Enum.IsDefined(typeof(Twist), n) && !list.Contains((Twist)n)) list.Add((Twist)n);
            return new RoundEvents(seed, list.ToArray(), flags.IndexOf('w') >= 0, flags.IndexOf('c') >= 0);
        }

        /// For the multiplayer state: "" plain golf; else the world on ('w') or still ('s'), the
        /// challenges set out ('c'), then the twists by number ("w:0,4", "wc:3,5").
        public string Code => !Any ? "" : (weatherOn ? "w" : "s") + (challengesOn ? "c" : "") + ":" + string.Join(",", Array.ConvertAll(Dealt, t => ((int)t).ToString()));

        /// Weather and the things moving about the holes are on.
        public bool World => weatherOn;
        /// Golden rings to fly through and a bounty on every hole.
        public bool Challenges => challengesOn;

        public bool Has(Twist t) => Array.IndexOf(Dealt, t) >= 0;

        Dice Rng(int holeIndex, int salt) => new(Mix(Seed, holeIndex, salt));

        /// A seed for one draw, mixed (SplitMix64) from the round's, the hole and what is drawn: System.Random's first
        /// numbers for neighbouring seeds are nearly the same, so seeds made by adding would all draw alike.
        public static int Mix(int seed, int hole, int salt)
        {
            unchecked
            {
                ulong z = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)hole * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)salt * 0x165667B19E3779F9UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (int)(z & 0x7FFFFFFF);
            }
        }

        // ----- Weather -----

        /// A hole's weather as it starts.
        public Weather WeatherFor(int holeIndex)
        {
            if (!weatherOn) return Weather.Clear;
            double u = Rng(holeIndex, 1).NextDouble();
            // (a hurricane round is wild everywhere, and blows any fog away)
            var (clear, gusty, rain, fog) = Has(Twist.Hurricane) ? (0.12, 0.42, 0.58, 0.58) : (0.36, 0.56, 0.74, 0.85);
            return u < clear ? Weather.Clear : u < gusty ? Weather.Gusty : u < rain ? Weather.Rain : u < fog ? Weather.Fog : Weather.Storm;
        }

        /// The weather turning before the hole's `shot`th swing (0 is the tee shot, which never
        /// turns): rain rolling in, a storm breaking, the sky clearing; null when it holds.
        public Weather? TurnBefore(int holeIndex, int shot, Weather now)
        {
            if (!weatherOn || shot < 1) return null;
            double u = Rng(holeIndex, 100 + shot).NextDouble();
            return now switch
            {
                Weather.Clear => u < 0.08 ? Weather.Rain : u < 0.15 ? Weather.Gusty : u < 0.18 && !Has(Twist.Hurricane) ? Weather.Fog : null,
                Weather.Gusty => u < 0.08 ? Weather.Storm : u < 0.16 ? Weather.Clear : null,
                Weather.Rain => u < 0.12 ? Weather.Storm : u < 0.26 ? Weather.Clear : null,
                Weather.Fog => u < 0.22 ? Weather.Clear : null,
                _ => u < 0.2 ? Weather.Rain : null,
            };
        }

        /// The hole's wind from the round's draw, as the weather and the twists have it: a gusty
        /// day and a storm blow at least a fresh breeze, a hurricane round a gale.
        public Wind WindFor(int holeIndex, Wind drawn, Weather weather)
        {
            if (!weatherOn && !Has(Twist.Hurricane)) return drawn;
            var rng = Rng(holeIndex, 2);
            double u = rng.NextDouble();
            double speed = drawn.SpeedMPH;
            if (Has(Twist.Hurricane)) speed = Math.Round(15 + 11 * u);
            else if (weather == Weather.Storm) speed = Math.Max(speed, Math.Round(10 + 8 * u));
            else if (weather == Weather.Gusty) speed = Math.Max(speed, Math.Round(7 + 7 * u));
            else if (weather == Weather.Fog) speed = Math.Min(speed, Math.Round(1 + 3 * u));   // fog hangs in still air
            return new Wind(speed, drawn.DirectionDegrees);
        }

        /// The wind for the hole's `shot`th swing when it gusts (a gusty day, a storm, a hurricane
        /// round): it swings round and freshens or drops from the hole's own; null when it holds.
        public Wind? GustBefore(int holeIndex, int shot, Wind holeWind, Weather now)
        {
            if (shot < 1 || !(now.IsGusty() || Has(Twist.Hurricane))) return null;
            var rng = Rng(holeIndex, 200 + shot);
            double floor = Has(Twist.Hurricane) ? 12 : 4, ceiling = Has(Twist.Hurricane) ? 28 : 22;
            double speed = Math.Round(Math.Max(floor, Math.Min(ceiling, holeWind.SpeedMPH + (rng.NextDouble() - 0.4) * 12)));
            double turn = Math.Round((rng.NextDouble() - 0.5) * 140);
            double direction = holeWind.DirectionDegrees + turn;
            direction = (direction % 360 + 540) % 360 - 180;
            return new Wind(speed, direction);
        }

        /// The rules for a hole in this weather: the twists' and the weather's together.
        public PlayRules RulesFor(Weather weather)
        {
            if (Dealt.Length == 0 && !weather.IsWet()) return PlayRules.Standard;
            var r = new PlayRules();
            foreach (var t in Dealt) t.Bend(r);
            weather.Bend(r);
            return r;
        }

        // ----- What moves about the hole -----

        /// The things moving about a hole today, placed off its shape: a hot-air balloon drifting
        /// across the line of play over the tee shot, a cart going round its loop across the
        /// fairway where the drives come down (par 4s and 5s, on dry, open ground), gulls wheeling
        /// over the landing. `ground` gives the ground's height (the hole's own, read off its
        /// model). Null when nothing moves today.
        public CourseMovers MoversFor(int holeIndex, Hole hole, Weather weather, Func<CoursePoint, double> ground = null)
        {
            if (!weatherOn || hole?.Centerline == null || hole.Centerline.Length < 2) return null;
            ground ??= hole.Ground ?? (_ => 0);
            var rng = Rng(holeIndex, 3);
            var tee = hole.Tee;
            var aim = hole.RecommendedTarget(tee);
            double leg = tee.DistanceTo(aim);
            if (leg < 60) return null;
            double ux = (aim.X - tee.X) / leg, ud = (aim.D - tee.D) / leg;   // down the line of play
            double rx = ud, rd = -ux;                                       // and to its right
            CoursePoint At(double along, double across) => new(tee.X + ux * along + rx * across, tee.D + ud * along + rd * across);

            bool magma = hole.Theme == "magma";
            bool balloon = rng.NextDouble() < 0.5 && weather != Weather.Storm || EveryMover;
            bool cart = hole.Par >= 4 && (rng.NextDouble() < 0.6 || EveryMover);
            bool gulls = !magma && (weather != Weather.Storm && rng.NextDouble() < 0.6 || EveryMover);
            // a storm over the islands can throw down a twister across the landing
            bool twister = weather == Weather.Storm && Weathers.SkyOf(hole) == Sky.Rain && hole.Par >= 4 && (rng.NextDouble() < 0.5 || EveryMover);
            if (!balloon && !cart && !gulls && !twister) { if (weather == Weather.Storm || magma) cart = hole.Par >= 4; if (!cart) { balloon = magma || weather != Weather.Storm; gulls = !balloon; } }
            var list = new List<Mover>();

            if (cart)
            {
                for (int attempt = 0; attempt < 8 && list.Count == 0; attempt++)
                {
                    double along = Math.Max(100, Math.Min(Math.Min(leg - 30, 245), leg * (0.5 + 0.3 * rng.NextDouble())));
                    double half = hole.FairwayWidth / 2 + 7 + 4 * rng.NextDouble();
                    var m = Mover.CartLoop(At(along - 2.2, -half), At(along - 2.2, half), At(along + 2.2, half), At(along + 2.2, -half),
                        4.2 + 1.2 * rng.NextDouble(), rng.NextDouble(), ground);
                    if (CartFits(m, hole)) list.Add(m);
                }
            }
            if (balloon)
            {
                double along = Math.Max(55, Math.Min(leg - 25, leg * (0.38 + 0.24 * rng.NextDouble())));
                double reach = hole.FairwayWidth / 2 + 26 + 14 * rng.NextDouble();
                bool fromLeft = rng.NextDouble() < 0.5;
                var a = At(along, fromLeft ? -reach : reach); var b = At(along + 12 * (rng.NextDouble() - 0.5), fromLeft ? reach : -reach);
                double top = double.MinValue;
                for (int k = 0; k <= 10; k++) { double t = k / 10.0; top = Math.Max(top, ground(new CoursePoint(a.X + (b.X - a.X) * t, a.D + (b.D - a.D) * t))); }
                // high over the ground and its basket clear of every tree under its drift
                double height = Math.Max(top + 21 + 9 * rng.NextDouble(), TallestNear(hole, a, b, Mover.Envelope + 2) + Mover.BasketDrop + Mover.Basket + 2);
                list.Add(Mover.Balloon(a, b, height, 70 + 30 * rng.NextDouble(), rng.NextDouble(), rng.Next(4)));
            }
            if (gulls)
            {
                double along = Math.Max(80, Math.Min(leg - 8, leg * (0.6 + 0.22 * rng.NextDouble())));
                var centre = At(along, (rng.NextDouble() - 0.5) * 20);
                double radius = 9 + 6 * rng.NextDouble();
                // over the trees they wheel above
                double height = Math.Max(ground(centre) + 11 + 7 * rng.NextDouble(), TallestNear(hole, centre, centre, radius + 3) + 3);
                double speed = (0.32 + 0.16 * rng.NextDouble()) * (rng.NextDouble() < 0.5 ? -1 : 1);
                int count = 3 + rng.Next(3);
                double start = rng.NextDouble() * Math.PI * 2;
                for (int i = 0; i < count; i++)
                    list.Add(Mover.Gull(centre, radius + (rng.NextDouble() - 0.5) * 4, height + (rng.NextDouble() - 0.5) * 3, speed,
                        start + i * 0.55 + (rng.NextDouble() - 0.5) * 0.3, i));
            }
            if (twister)
            {
                double along = Math.Max(110, Math.Min(leg - 10, leg * (0.62 + 0.2 * rng.NextDouble())));
                double reach = hole.FairwayWidth / 2 + 8 + 6 * rng.NextDouble();
                bool fromLeft = rng.NextDouble() < 0.5;
                var a = At(along - 8 + 16 * rng.NextDouble(), fromLeft ? -reach : reach);
                var b = At(along - 8 + 16 * rng.NextDouble(), fromLeft ? reach : -reach);
                list.Add(Mover.Twister(a, b, 34 + 16 * rng.NextDouble(), rng.NextDouble(), ground));
            }
            return list.Count == 0 ? null : new CourseMovers(list.ToArray());
        }

        // ----- What there is to chase: rings and bounties -----

        /// The golden rings over a hole's tee shot, laid along the flight of a straight shot to
        /// where it should go (under the round's rules: a moon ball's rings stand higher): a gold
        /// ring on its line early, another a few yards off it, and a glide ring sliding across it
        /// over the landing — threading one is a good shot, all three a great one. None on short
        /// holes, nor when the challenges are off.
        public Ring[] RingsFor(int holeIndex, Hole hole, PlayRules rules, Func<CoursePoint, double> ground = null)
        {
            if (!challengesOn || hole?.Centerline == null || hole.Centerline.Length < 2) return Array.Empty<Ring>();
            ground ??= hole.Ground ?? (_ => 0);
            var tee = hole.Tee;
            var aim = hole.Par == 3 ? hole.Pin : hole.RecommendedTarget(tee);
            if (tee.DistanceTo(aim) < 70) return Array.Empty<Ring>();
            var shot = CourseRings.StraightShot(tee, aim, rules, hole.LieAt(tee));
            if (shot is not var (flight, heading, _, _)) return Array.Empty<Ring>();
            var rng = Rng(holeIndex, 4);
            double nx = Math.Sin(heading * Math.PI / 180), nd = Math.Cos(heading * Math.PI / 180);
            var list = new List<Ring>();
            void Lay(RingKind kind, double fraction, double lateral, double lift, double glide)
            {
                var (x, y, d) = CourseRings.AlongFlight(flight, heading, tee, ground, fraction, lateral);
                double r = Ring.RadiusOf(kind);
                var at = new CoursePoint(x, d);
                // clear of the ground and of anything standing under it (it rises over the trees)
                y = Math.Max(y + lift, ground(at) + r + 1.5);
                double tallest = TallestNear(hole, new CoursePoint(x - nd * glide, d + nx * glide), new CoursePoint(x + nd * glide, d - nx * glide), r + 1);
                if (tallest > y - r - 1) y = tallest + r + 1.5;
                list.Add(new Ring(kind, x, y, d, nx, nd, r, glide, 5 + 2 * rng.NextDouble(), rng.NextDouble()));
            }
            double side = rng.NextDouble() < 0.5 ? -1 : 1;
            Lay(RingKind.Gold, 0.3 + 0.06 * rng.NextDouble(), (rng.NextDouble() - 0.5) * 2, 0, 0);
            Lay(RingKind.Gold, 0.5 + 0.06 * rng.NextDouble(), side * (8 + 3 * rng.NextDouble()), (rng.NextDouble() - 0.5) * 3, 0);
            Lay(RingKind.Glide, 0.7 + 0.06 * rng.NextDouble(), 0, 0, 7 + 4 * rng.NextDouble());
            return list.ToArray();
        }

        /// The ring the sky leaves when it clears after rain: a rainbow one, halfway along the
        /// shot from `from` to `target`, worth the most. Null when there is no flight to lay it on.
        public static Ring RainbowRing(Hole hole, CoursePoint from, CoursePoint target, PlayRules rules, Func<CoursePoint, double> ground = null)
        {
            ground ??= hole?.Ground ?? (_ => 0);
            if (hole == null || from.DistanceTo(target) < 60) return null;
            if (CourseRings.StraightShot(from, target, rules, hole.LieAt(from)) is not var (flight, heading, _, _)) return null;
            var (x, y, d) = CourseRings.AlongFlight(flight, heading, from, ground, 0.55);
            double r = Ring.RadiusOf(RingKind.Rainbow);
            y = Math.Max(y, ground(new CoursePoint(x, d)) + r + 1.5);
            double tallest = TallestNear(hole, new CoursePoint(x, d), new CoursePoint(x, d), r + 1);
            if (tallest > y - r - 1) y = tallest + r + 1.5;
            return new Ring(RingKind.Rainbow, x, y, d, Math.Sin(heading * Math.PI / 180), Math.Cos(heading * Math.PI / 180), r);
        }

        /// The hole's bounty, drawn from what it has: rings, things moving about it, its weather,
        /// its par. Null when the challenges are off.
        public Bounty BountyFor(int holeIndex, Hole hole, Weather weather, CourseMovers movers, Ring[] rings)
        {
            if (!challengesOn || hole == null) return null;
            var odds = new List<(BountyKind kind, double weight)>();
            int ringCount = rings?.Length ?? 0;
            if (ringCount >= 1) odds.Add((BountyKind.Ring, 2.2));
            if (ringCount >= 2) odds.Add((BountyKind.TwoRings, 0.6));
            var kinds = movers?.All ?? Array.Empty<Mover>();
            if (Array.Exists(kinds, m => m.Kind == MoverKind.Balloon)) odds.Add((BountyKind.Balloon, 1.6));
            if (Array.Exists(kinds, m => m.Kind == MoverKind.Gull)) odds.Add((BountyKind.Gull, 1.0));
            if (Array.Exists(kinds, m => m.Kind == MoverKind.Cart)) odds.Add((BountyKind.Cart, 1.2));
            if (hole.Par >= 4) odds.Add((BountyKind.Fairway, 1.6));
            if (hole.Par >= 4 && hole.Tee.DistanceTo(hole.RecommendedTarget(hole.Tee)) >= 230) odds.Add((BountyKind.Drive, 1.0));
            if (hole.Par == 3) odds.Add((BountyKind.Close, 2.5));
            odds.Add((BountyKind.Green, 1.4));
            odds.Add((BountyKind.Birdie, hole.Par >= 5 ? 1.5 : 1.0));
            if (weatherOn && weather.IsRough()) odds.Add((BountyKind.ParInWeather, 2.5));
            odds.Add((BountyKind.OnePutt, 1.2));
            odds.Add((BountyKind.LongPutt, 0.7));
            odds.Add((BountyKind.ChipIn, 0.4));
            double total = 0; foreach (var o in odds) total += o.weight;
            double u = Rng(holeIndex, 5).NextDouble() * total;
            var pick = odds[odds.Count - 1].kind;
            foreach (var o in odds) { if (u < o.weight) { pick = o.kind; break; } u -= o.weight; }
            return new Bounty(pick, weather.Title(Weathers.SkyOf(hole)));
        }

        /// The world height of the tallest thing standing within `reach` of the line from `a` to `b`
        /// (the lowest there could be when nothing is).
        static double TallestNear(Hole hole, CoursePoint a, CoursePoint b, double reach)
        {
            double tallest = double.MinValue;
            double lx = b.X - a.X, ld = b.D - a.D, l2 = Math.Max(1e-9, lx * lx + ld * ld);
            foreach (var o in hole.Obstacles ?? Array.Empty<Obstacle>())
            {
                double u = Math.Max(0, Math.Min(1, ((o.X - a.X) * lx + (o.D - a.D) * ld) / l2));
                double dx = o.X - (a.X + lx * u), dd = o.D - (a.D + ld * u);
                if (dx * dx + dd * dd <= Math.Pow(reach + o.Radius, 2)) tallest = Math.Max(tallest, o.Top);
            }
            return tallest;
        }

        /// A cart's loop has to be on dry, open, drivable ground: no water, no sand, nothing
        /// standing in the way, no cliff.
        static bool CartFits(Mover cart, Hole hole)
        {
            double lastY = double.NaN;
            foreach (var (x, y, d) in cart.LoopPoints())
            {
                var p = new CoursePoint(x, d);
                var lie = hole.LieAt(p);
                if (lie is CourseLie.Water or CourseLie.Bunker or CourseLie.OutOfBounds or CourseLie.Green or CourseLie.Ice) return false;
                if (hole.HazardAt(p) != null) return false;
                if (!double.IsNaN(lastY) && Math.Abs(y - lastY) > 0.45) return false;   // (samples half a yard apart: steeper than 40° is a cliff)
                lastY = y;
                foreach (var o in hole.Obstacles ?? Array.Empty<Obstacle>())
                    if (Math.Sqrt((o.X - x) * (o.X - x) + (o.D - d) * (o.D - d)) < Math.Max(o.Footprint, 0.6) + 1.6) return false;
            }
            return true;
        }
    }

    /// The events' dice: a SplitMix64 stream from a seed. Not System.Random, so the app's menus
    /// (Swift: GolfForecast) can deal the same round from the same seed — the daily forecast's
    /// twists and weather are shown before the round is played.
    public sealed class Dice
    {
        ulong state;
        public Dice(int seed) { state = (ulong)(uint)seed; }

        ulong Bits()
        {
            unchecked
            {
                state += 0x9E3779B97F4A7C15UL;
                ulong z = state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// In [0, 1).
        public double NextDouble() => (Bits() >> 11) * (1.0 / 9007199254740992.0);
        /// In [0, n).
        public int Next(int n) => n <= 0 ? 0 : Math.Min(n - 1, (int)(NextDouble() * n));
    }
}
