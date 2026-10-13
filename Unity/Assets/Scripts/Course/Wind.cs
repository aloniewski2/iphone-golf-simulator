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
        /// From here up the wind is strong (a 7-iron drifts 20 yards and more across it); from ModerateMPH it is worth
        /// aiming off.
        public const double ModerateMPH = 10, StrongMPH = 15;

        public Wind(double speedMPH, double directionDegrees)
        {
            SpeedMPH = double.IsFinite(speedMPH) ? Math.Max(0, speedMPH) : 0;
            DirectionDegrees = double.IsFinite(directionDegrees) ? directionDegrees : 0;
        }

        public static readonly Wind Calm = new(0, 0);

        public bool IsCalm => SpeedMPH < 0.5;
        public bool IsStrong => SpeedMPH >= StrongMPH;

        /// Calm, Light, Moderate or Strong, for the HUD.
        public string Strength => IsCalm ? "Calm" : SpeedMPH < ModerateMPH ? "Light" : IsStrong ? "Strong" : "Moderate";

        /// A hole's wind, from any direction: a quarter of holes near calm (0–3 mph), most light (4–9), a quarter
        /// moderate (10–14) and only one in ten strong (15–20), so a big blow is an event, not the weather.
        public static Wind Random(System.Random rng)
        {
            double band = rng.NextDouble(), within = rng.NextDouble();
            double speed = band < 0.25 ? Math.Round(3 * within)
                : band < 0.65 ? 4 + Math.Round(5 * within)
                : band < 0.90 ? ModerateMPH + Math.Round(4 * within)
                : StrongMPH + Math.Round(5 * within);
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
