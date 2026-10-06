using System;
using System.Collections.Generic;

namespace GolfArcade.Shot
{
    /// The bag. (The first four keep their old numbers; Iron is the 7-iron, Wedge the sand wedge.)
    public enum GolfClub { Driver, Iron, Wedge, Putter, Hybrid, Iron5, Iron9, PitchingWedge, LobWedge, Chipper }

    /// What a club swings like and looks like in the golfer's hands: the woods' sweeping swing,
    /// the irons', the wedges' (a half swing or a chip up close), the putt.
    public enum ClubFamily { Wood, Iron, Wedge, Putter }

    /// A full bag in steps of 15–50 yards, so there is a club for every distance and the game
    /// picks the one that gets there. Every number here is shared with the iOS app's model so
    /// both versions fly the same distances (see GolfArcade/Mock/BallFlight.swift).
    public static class GolfClubs
    {
        /// Longest to shortest, then the putter.
        public static readonly GolfClub[] All =
        {
            GolfClub.Driver, GolfClub.Hybrid, GolfClub.Iron5, GolfClub.Iron, GolfClub.Iron9,
            GolfClub.PitchingWedge, GolfClub.Wedge, GolfClub.LobWedge, GolfClub.Chipper, GolfClub.Putter,
        };

        public static ClubFamily Family(this GolfClub club) => club switch
        {
            GolfClub.Driver or GolfClub.Hybrid => ClubFamily.Wood,
            GolfClub.Iron5 or GolfClub.Iron or GolfClub.Iron9 => ClubFamily.Iron,
            GolfClub.Putter => ClubFamily.Putter,
            _ => ClubFamily.Wedge,
        };

        public static string Label(this GolfClub club) => club switch
        {
            GolfClub.Driver => "DR", GolfClub.Hybrid => "HY", GolfClub.Iron5 => "5I", GolfClub.Iron => "7I",
            GolfClub.Iron9 => "9I", GolfClub.PitchingWedge => "PW", GolfClub.Wedge => "SW", GolfClub.LobWedge => "LW",
            GolfClub.Chipper => "CH", _ => "PT",
        };

        public static string DisplayName(this GolfClub club) => club switch
        {
            GolfClub.Driver => "Driver", GolfClub.Hybrid => "Hybrid", GolfClub.Iron5 => "5 Iron", GolfClub.Iron => "7 Iron",
            GolfClub.Iron9 => "9 Iron", GolfClub.PitchingWedge => "Pitching Wedge", GolfClub.Wedge => "Sand Wedge",
            GolfClub.LobWedge => "Lob Wedge", GolfClub.Chipper => "Chipper", _ => "Putter",
        };

        /// The putter and the chipper are rated by where the ball finishes — a putt rolls all the
        /// way, a chip carries a third and runs the rest; every other club by its carry.
        public static bool RatedByTotal(this GolfClub club) => club is GolfClub.Putter or GolfClub.Chipper;

        /// Carry (where it finishes, for the putter and the chipper) of a full, fair strike, yards.
        public static double ReferenceDistanceYards(this GolfClub club) => club switch
        {
            GolfClub.Driver => 240, GolfClub.Hybrid => 190, GolfClub.Iron5 => 175, GolfClub.Iron => 150,
            GolfClub.Iron9 => 125, GolfClub.PitchingWedge => 100, GolfClub.Wedge => 80, GolfClub.LobWedge => 50,
            GolfClub.Chipper => 25, _ => 25,
        };

        public static double SmashFactor(this GolfClub club) => club switch
        {
            GolfClub.Driver => 1.46, GolfClub.Hybrid => 1.42, GolfClub.Iron5 => 1.37, GolfClub.Iron => 1.33,
            GolfClub.Iron9 => 1.28, GolfClub.PitchingWedge => 1.23, GolfClub.Wedge => 1.18, GolfClub.LobWedge => 1.12,
            GolfClub.Chipper => 1.08, _ => 1.0,
        };

        /// Launch conditions of a well-struck shot: low and hot off the driver, steeper and
        /// spinnier down the bag.
        public static double LaunchAngleDegrees(this GolfClub club) => club switch
        {
            GolfClub.Driver => 12.5, GolfClub.Hybrid => 16, GolfClub.Iron5 => 15.5, GolfClub.Iron => 18.5,
            GolfClub.Iron9 => 23, GolfClub.PitchingWedge => 27, GolfClub.Wedge => 32, GolfClub.LobWedge => 42,
            GolfClub.Chipper => 8, _ => 0,   // (the chipper runs it along the ground)
        };

        /// Backspin at full speed, rpm. Scales with club speed.
        public static double SpinRPM(this GolfClub club) => club switch
        {
            GolfClub.Driver => 2600, GolfClub.Hybrid => 4600, GolfClub.Iron5 => 5300, GolfClub.Iron => 6800,
            GolfClub.Iron9 => 8200, GolfClub.PitchingWedge => 9000, GolfClub.Wedge => 9800, GolfClub.LobWedge => 10200,
            GolfClub.Chipper => 1200, _ => 0,
        };

        /// Peak phone rotation speed (rad/s) that counts as a full swing. Short clubs need less,
        /// so a wedge swing feels like a wedge swing and not a flailing driver. A hard swing with a
        /// phone in hand peaks around 12 rad/s or more; the driver's full is set well under that,
        /// at a relaxed but committed swing, so a normal swing goes the distance and anything
        /// harder is the whole club, never more and never wild.
        public static double MotionFullSpeed(this GolfClub club) => club switch
        {
            GolfClub.Driver => 9, GolfClub.Hybrid => 8.5, GolfClub.Iron5 => 8, GolfClub.Iron => 7.5,
            GolfClub.Iron9 => 7, GolfClub.PitchingWedge => 6.5, GolfClub.Wedge => 5.5, GolfClub.LobWedge => 4.8,
            GolfClub.Chipper => 4, _ => 3,
        };

        /// The club for a shot of `yards` from a lie that keeps `reach` of each club's distance:
        /// the shortest one that gets there, so the swing is a full one or near it.
        public static GolfClub ForDistance(double yards, Func<GolfClub, double> reach)
        {
            for (int i = All.Length - 2; i >= 0; i--)
                if (All[i].ReferenceDistanceYards() * reach(All[i]) >= yards - 4) return All[i];
            return GolfClub.Driver;
        }

        /// Full swings read distance straight (0.5 on a 250-yard driver carries 125). The putter
        /// curves it (distance ∝ meter²) so the short end has room: a 3-footer is a fifth of the
        /// stroke, not a flick, and a degree of wobble in it is inches, not feet.
        public static double MeterExponent(this GolfClub club) => club == GolfClub.Putter ? 2 : 1;

        public static double DistanceYards(this GolfClub club, double meter)
        {
            double p = Clamp01(meter);
            return club.ReferenceDistanceYards() * Math.Pow(p, club.MeterExponent());
        }

        public static double MaxClubSpeedMPH(this GolfClub club) => Calibration(club).MaxSpeed;

        /// Club speed that flies (rolls, for the putter) `fraction` of the reference distance.
        /// Distance grows faster than linearly with speed, so this is what makes a half-filled
        /// meter a half-distance shot rather than a third of one.
        public static double ClubSpeedMPH(this GolfClub club, double meterFraction)
        {
            var cal = Calibration(club);
            double target = Clamp01(meterFraction) * club.ReferenceDistanceYards();
            if (target <= 0) return 0;
            var table = cal.Distances;
            double step = cal.MaxSpeed / (table.Length - 1);
            int index = 1;
            while (index < table.Length - 1 && table[index] < target) index++;
            double low = table[index - 1], high = table[index];
            double within = high > low ? Clamp01((target - low) / (high - low)) : 1;
            return (index - 1 + within) * step;
        }

        /// Launch for a meter reading. `speedFactor` scales club speed for things that really do
        /// cost speed: a thin strike, a chip motion, a buried lie.
        public static BallFlight.Launch Launch(this GolfClub club, double power, double aimDegrees, double curveDegrees, double speedFactor = 1)
        {
            double p = Clamp01(power);
            double factor = Clamp01(speedFactor);
            double clubSpeed = club.ClubSpeedMPH(Math.Pow(p, club.MeterExponent())) * factor;
            return new BallFlight.Launch
            {
                BallSpeedMPH = clubSpeed * club.SmashFactor(),
                LaunchAngleDegrees = club.LaunchAngleDegrees(),
                SpinRPM = club.SpinRPM() * clubSpeed / club.MaxClubSpeedMPH(),
                DirectionDegrees = aimDegrees,
                CurveDegrees = curveDegrees,
            };
        }

        static double Clamp01(double v) => double.IsFinite(v) ? Math.Max(0, Math.Min(1, v)) : 0;

        sealed class ClubCalibration
        {
            public double MaxSpeed;
            public double[] Distances;
        }

        static readonly Dictionary<GolfClub, ClubCalibration> calibrations = new();

        /// Calibrated once through the same aerodynamic/rolling solver, not a distance multiplier
        /// applied after landing, so every lower-power shot keeps real flight integration.
        static ClubCalibration Calibration(GolfClub club)
        {
            lock (calibrations)
            {
                if (calibrations.TryGetValue(club, out var cached)) return cached;

                double Distance(double speed, double spin)
                {
                    var flight = BallFlight.Simulate(new BallFlight.Launch
                    {
                        BallSpeedMPH = speed * club.SmashFactor(),
                        LaunchAngleDegrees = club.LaunchAngleDegrees(),
                        SpinRPM = spin,
                        DirectionDegrees = 0,
                        CurveDegrees = 0,
                    });
                    return club.RatedByTotal() ? flight.Total : flight.Carry;
                }

                double low = 1.0, high = 145.0;
                for (int i = 0; i < 24; i++)
                {
                    double speed = (low + high) / 2;
                    if (Distance(speed, club.SpinRPM()) < club.ReferenceDistanceYards()) low = speed; else high = speed;
                }
                double maxSpeed = (low + high) / 2;
                const int steps = 40;
                var distances = new double[steps + 1];
                for (int step = 0; step <= steps; step++)
                {
                    double speed = maxSpeed * step / steps;
                    distances[step] = Distance(speed, club.SpinRPM() * speed / maxSpeed);
                }
                var cal = new ClubCalibration { MaxSpeed = maxSpeed, Distances = distances };
                calibrations[club] = cal;
                return cal;
            }
        }
    }
}
