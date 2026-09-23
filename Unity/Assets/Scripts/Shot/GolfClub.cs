using System;
using System.Collections.Generic;

namespace GolfArcade.Shot
{
    public enum GolfClub { Driver, Iron, Wedge, Putter }

    /// Standard virtual bag. Every number here is shared with the iOS app's model so both
    /// versions fly the same distances (see GolfArcade/Mock/BallFlight.swift).
    public static class GolfClubs
    {
        public static readonly GolfClub[] All = { GolfClub.Driver, GolfClub.Iron, GolfClub.Wedge, GolfClub.Putter };

        public static string Label(this GolfClub club) => club switch
        {
            GolfClub.Driver => "DR",
            GolfClub.Iron => "7I",
            GolfClub.Wedge => "SW",
            _ => "PT",
        };

        public static string DisplayName(this GolfClub club) => club switch
        {
            GolfClub.Driver => "Driver",
            GolfClub.Iron => "7 Iron",
            GolfClub.Wedge => "Sand Wedge",
            _ => "Putter",
        };

        /// Carry (total roll for the putter) of a full, fair strike, in yards.
        public static double ReferenceDistanceYards(this GolfClub club) => club switch
        {
            GolfClub.Driver => 250,
            GolfClub.Iron => 160,
            GolfClub.Wedge => 90,
            _ => 25,
        };

        public static double SmashFactor(this GolfClub club) => club switch
        {
            GolfClub.Driver => 1.46,
            GolfClub.Iron => 1.33,
            GolfClub.Wedge => 1.18,
            _ => 1.0,
        };

        /// Launch conditions of a well-struck shot (driver, 7-iron, sand wedge).
        public static double LaunchAngleDegrees(this GolfClub club) => club switch
        {
            GolfClub.Driver => 12.5,
            GolfClub.Iron => 17,
            GolfClub.Wedge => 29,
            _ => 0,
        };

        /// Backspin at full speed, rpm. Scales with club speed.
        public static double SpinRPM(this GolfClub club) => club switch
        {
            GolfClub.Driver => 2600,
            GolfClub.Iron => 6800,
            GolfClub.Wedge => 9800,
            _ => 0,
        };

        /// Peak phone rotation speed (rad/s) that counts as a full swing. Short clubs need less,
        /// so a wedge swing feels like a wedge swing and not a flailing driver. A hard swing with a
        /// phone in hand peaks around 12 rad/s; the driver's full is set well under that, at a
        /// relaxed but committed swing, so a normal swing goes the distance and only a real lash
        /// (twice this) is over the top.
        public static double MotionFullSpeed(this GolfClub club) => club switch
        {
            GolfClub.Driver => 9,
            GolfClub.Iron => 7.5,
            GolfClub.Wedge => 5.5,
            _ => 3,
        };

        /// Full swings read distance straight (0.5 on a 250-yard driver carries 125). The putter
        /// curves it (distance ∝ meter^1.5) so the short end has room for a tap-in.
        public static double MeterExponent(this GolfClub club) => club == GolfClub.Putter ? 1.5 : 1;

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
                    return club == GolfClub.Putter ? flight.Total : flight.Carry;
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
