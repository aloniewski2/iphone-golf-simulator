using System;

namespace GolfArcade.Course
{
    /// The hole's wind, Wii Sports style: one speed and direction for the whole hole, shown as
    /// an arrow at address. `DirectionDegrees` is where the wind blows *toward*, degrees right of
    /// straight down the hole (+D), so 0 is a following wind off the tee.
    public readonly struct Wind
    {
        public readonly double SpeedMPH;
        public readonly double DirectionDegrees;

        public const double MaxMPH = 20;

        public Wind(double speedMPH, double directionDegrees)
        {
            SpeedMPH = double.IsFinite(speedMPH) ? Math.Max(0, speedMPH) : 0;
            DirectionDegrees = double.IsFinite(directionDegrees) ? directionDegrees : 0;
        }

        public static readonly Wind Calm = new(0, 0);

        public bool IsCalm => SpeedMPH < 0.5;

        /// A hole's wind: any direction, mostly light, sometimes a proper blow.
        public static Wind Random(System.Random rng)
        {
            double speed = Math.Round(MaxMPH * Math.Pow(rng.NextDouble(), 1.6));
            return new Wind(speed, Math.Round(rng.NextDouble() * 360 - 180));
        }

        /// The wind's direction in a shot's aim frame: degrees right of the aim line it blows
        /// toward, which is what the flight model and the HUD arrow both want.
        public double RelativeTo(double headingDegrees)
        {
            double d = (DirectionDegrees - headingDegrees) % 360;
            if (d > 180) d -= 360; else if (d <= -180) d += 360;
            return d;
        }

        /// What the wind does to a shot aimed on `headingDegrees`, in the player's words.
        public string Describe(double headingDegrees)
        {
            if (IsCalm) return "Calm";
            double rel = RelativeTo(headingDegrees);
            double a = Math.Abs(rel);
            string kind = a <= 30 ? "helping" : a >= 150 ? "into" : rel > 0 ? "left to right" : "right to left";
            return $"{SpeedMPH:F0} mph {kind}";
        }

        public override string ToString() => IsCalm ? "Calm" : $"{SpeedMPH:F0} mph toward {DirectionDegrees:F0}°";
    }
}
