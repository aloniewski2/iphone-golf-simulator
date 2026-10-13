using System;

namespace GolfArcade.Course
{
    /// A hole's bounty: one thing to do on it for coins, drawn for the hole from what it has —
    /// rings to fly through, a balloon to bonk, a storm to par in, a green to find.
    public enum BountyKind { Ring, TwoRings, Balloon, Gull, Cart, Fairway, Green, Birdie, ParInWeather, OnePutt, Close, Drive, LongPutt, ChipIn }

    public enum BountyState { Open, Done, Missed }

    /// One stroke, as far as a bounty is concerned.
    public struct BountyShot
    {
        /// The hole's strokes with this one (1 for the tee shot), and its par.
        public int Stroke, Par;
        public bool Putt, Holed;
        /// It was played from the green or its fringe.
        public bool FromPuttingSurface;
        public double FromPinYards, RestPinYards, Total;
        public CourseLie Rest;
        /// Rings flown through, and what moving about the hole it met.
        public int Rings;
        public bool Balloon, Gull, Cart;
    }

    public sealed class Bounty
    {
        public readonly BountyKind Kind;
        public readonly int Coins;
        /// What to do, in a few words ("Bonk the balloon").
        public readonly string Title;
        public BountyState State { get; private set; }
        int putts;

        public Bounty(BountyKind kind, string weatherName = null)
        {
            Kind = kind;
            Coins = CoinsFor(kind);
            Title = kind switch
            {
                BountyKind.Ring => "Fly through a ring",
                BountyKind.TwoRings => "Thread two rings in one shot",
                BountyKind.Balloon => "Bonk the balloon",
                BountyKind.Gull => "Clip a gull",
                BountyKind.Cart => "Bonk the golf cart",
                BountyKind.Fairway => "Find the fairway off the tee",
                BountyKind.Green => "Hit the green in regulation",
                BountyKind.Birdie => "Make birdie",
                BountyKind.ParInWeather => $"Make par in the {(weatherName ?? "storm").ToLowerInvariant()}",
                BountyKind.OnePutt => "One-putt this green",
                BountyKind.Close => "Tee shot inside 15 ft",
                BountyKind.Drive => "Drive it 250+ yards",
                BountyKind.LongPutt => "Sink a putt from 20+ ft",
                _ => "Hole out from off the green",
            };
        }

        public static int CoinsFor(BountyKind kind) => kind switch
        {
            BountyKind.Ring => 30, BountyKind.TwoRings => 80,
            BountyKind.Balloon or BountyKind.Gull or BountyKind.Cart => 60,
            BountyKind.Fairway => 30, BountyKind.Green => 40, BountyKind.Birdie => 80, BountyKind.ParInWeather => 50,
            BountyKind.OnePutt => 40, BountyKind.Close => 60, BountyKind.Drive => 50, BountyKind.LongPutt => 70,
            _ => 120,
        };

        /// Its icon (UI/Icons, or painted: EventIcons).
        public string Icon => Kind switch
        {
            BountyKind.Ring or BountyKind.TwoRings => "ring",
            BountyKind.Balloon or BountyKind.Gull or BountyKind.Cart => "star",
            BountyKind.Fairway => "grass",
            BountyKind.Green or BountyKind.Close => "target",
            BountyKind.Drive => "ball",
            BountyKind.OnePutt or BountyKind.LongPutt => "putter",
            BountyKind.ParInWeather => "bolt",
            _ => "flag",
        };

        /// After every stroke on the hole: true the moment it is done. It is missed once it can't be.
        public bool Shot(BountyShot s)
        {
            if (State != BountyState.Open) return false;
            if (s.Putt) putts++;
            bool done = false, missed = false;
            switch (Kind)
            {
                case BountyKind.Ring: done = s.Rings >= 1; break;
                case BountyKind.TwoRings: done = s.Rings >= 2; break;
                case BountyKind.Balloon: done = s.Balloon; break;
                case BountyKind.Gull: done = s.Gull; break;
                case BountyKind.Cart: done = s.Cart; break;
                case BountyKind.Fairway:
                    if (s.Stroke == 1) { done = s.Holed || s.Rest == CourseLie.Fairway || s.Rest.IsPuttingSurface(); missed = !done; }
                    break;
                case BountyKind.Green:
                    done = s.Stroke <= s.Par - 2 && (s.Holed || s.Rest == CourseLie.Green);
                    missed = !done && s.Stroke >= s.Par - 2;
                    break;
                case BountyKind.Birdie:
                    done = s.Holed && s.Stroke <= s.Par - 1;
                    missed = !done && s.Stroke >= s.Par - 1;
                    break;
                case BountyKind.ParInWeather:
                    done = s.Holed && s.Stroke <= s.Par;
                    missed = !done && s.Stroke >= s.Par;
                    break;
                case BountyKind.OnePutt:
                    // holed with one putt or none (a chip-in counts); a putt that stays out is one too many
                    done = s.Holed && putts <= 1;
                    missed = !done && s.Putt && !s.Holed;
                    break;
                case BountyKind.Close:
                    if (s.Stroke == 1) { done = s.Holed || s.RestPinYards <= 5 && s.Rest.IsPuttingSurface(); missed = !done; }
                    break;
                case BountyKind.Drive:
                    if (s.Stroke == 1) { done = !s.Putt && s.Total >= 250 && s.Rest is not (CourseLie.Water or CourseLie.OutOfBounds); missed = !done; }
                    break;
                case BountyKind.LongPutt: done = s.Putt && s.Holed && s.FromPinYards >= 20 / 3.0; break;
                case BountyKind.ChipIn: done = s.Holed && !s.FromPuttingSurface; break;
            }
            if (done) { State = BountyState.Done; return true; }
            if (missed || s.Holed) State = BountyState.Missed;
            return false;
        }

        /// The hole's over (holed, or picked up): still open is missed.
        public void Close() { if (State == BountyState.Open) State = BountyState.Missed; }
    }
}
