using System;
using System.Collections.Generic;

namespace GolfArcade.Shot
{
    /// A point on the ball's path. x = lateral (right), y = height, z = downrange, in yards,
    /// relative to where the ball was struck and the line it was aimed on.
    public struct FlightPoint
    {
        public double LateralYards;
        public double HeightYards;
        public double DistanceYards;

        public FlightPoint(double lateral, double height, double distance)
        {
            LateralYards = lateral; HeightYards = height; DistanceYards = distance;
        }

        public static FlightPoint Lerp(FlightPoint a, FlightPoint b, double t) => new(
            a.LateralYards + (b.LateralYards - a.LateralYards) * t,
            a.HeightYards + (b.HeightYards - a.HeightYards) * t,
            a.DistanceYards + (b.DistanceYards - a.DistanceYards) * t);
    }

    /// Physical ball flight: gravity, aerodynamic drag, Magnus lift from backspin (tilted for a
    /// draw or fade), then bounce and roll on a fairway. Integrated once per shot and sampled
    /// for playback. Units are SI inside, yards outside. Same fit as the iOS app.
    public sealed class BallFlight
    {
        public struct Launch
        {
            public double BallSpeedMPH;
            public double LaunchAngleDegrees;
            public double SpinRPM;
            /// Initial direction, degrees right of the target line.
            public double DirectionDegrees;
            /// Tilt of the spin axis, degrees. Positive curves right (fade/slice).
            public double CurveDegrees;
            /// Wind the ball flies through: speed in mph and the direction it blows toward,
            /// degrees right of the target line (0 helps, 180 is into the face).
            public double WindMPH;
            public double WindDegrees;
        }

        public const double SampleInterval = 1.0 / 60;
        public const double MaxDuration = 16.0;
        public const double MetersPerYard = 0.9144;

        readonly List<FlightPoint> samples;
        public double Carry { get; }
        /// Where the ball first touched down (the end of the carry), aim frame.
        public FlightPoint CarryPoint { get; }
        /// Seconds from the strike to that first touchdown.
        public double CarryTime { get; }
        public double Roll { get; }
        public double Apex { get; }
        /// Seconds into the flight at which the ball stopped bouncing and began to roll (0 for a
        /// putt), and its horizontal velocity then, yards/s. The course re-rolls from here on
        /// whatever grass the ball is actually on.
        public double RollStartTime { get; }
        public double RollStartVelocityX { get; }
        public double RollStartVelocityZ { get; }
        public double Duration => (samples.Count - 1) * SampleInterval;
        public double Total => Carry + Roll;
        public FlightPoint Landing => samples[samples.Count - 1];
        public IReadOnlyList<FlightPoint> Samples => samples;

        BallFlight(List<FlightPoint> samples, double carry, FlightPoint carryPoint, double carryTime, double roll, double apex, double rollStartTime, double rollVx, double rollVz)
        {
            this.samples = samples; Carry = carry; CarryPoint = carryPoint; CarryTime = carryTime; Roll = roll; Apex = apex;
            RollStartTime = rollStartTime; RollStartVelocityX = rollVx; RollStartVelocityZ = rollVz;
        }

        public FlightPoint PositionAt(double time)
        {
            if (time <= 0) return samples[0];
            double last = Duration;
            if (time >= last) return samples[samples.Count - 1];
            double index = time / SampleInterval;
            int lower = Math.Min((int)index, samples.Count - 2);
            return FlightPoint.Lerp(samples[lower], samples[lower + 1], index - lower);
        }

        public static BallFlight Simulate(Launch launch)
        {
            const double mass = 0.04593;
            const double radius = 0.02135;
            double area = Math.PI * radius * radius;
            const double airDensity = 1.225;
            const double gravity = 9.81;
            const double dt = 1.0 / 240;
            const double restitution = 0.35;
            const double bounceFriction = 0.6;
            const double rollingDeceleration = 3.2;

            double speed = Math.Max(0, launch.BallSpeedMPH) * 0.44704;
            double elevation = launch.LaunchAngleDegrees * Math.PI / 180;
            double azimuth = launch.DirectionDegrees * Math.PI / 180;
            // x = right, y = up, z = downrange
            double px = 0, py = 0, pz = 0;
            double vx = Math.Sin(azimuth) * Math.Cos(elevation) * speed;
            double vy = Math.Sin(elevation) * speed;
            double vz = Math.Cos(azimuth) * Math.Cos(elevation) * speed;
            double spin = Math.Max(0, launch.SpinRPM) * 2 * Math.PI / 60;
            double tilt = launch.CurveDegrees * Math.PI / 180;
            // The air moves too: aerodynamic forces act on the ball's speed through the air,
            // not over the ground, which is all a head, tail or cross wind is.
            double windSpeed = (double.IsFinite(launch.WindMPH) ? Math.Max(0, launch.WindMPH) : 0) * 0.44704;
            double windAzimuth = (double.IsFinite(launch.WindDegrees) ? launch.WindDegrees : 0) * Math.PI / 180;
            double windX = Math.Sin(windAzimuth) * windSpeed, windZ = Math.Cos(windAzimuth) * windSpeed;

            var samples = new List<FlightPoint> { new(0, 0, 0) };
            double time = 0;
            double nextSample = SampleInterval;
            bool airborne = speed > 0.5 && elevation > 0.002; // a putt rolls from the first inch
            bool rolling = !airborne;
            double rollStartTime = 0, rollVx = vx, rollVz = vz;
            double? carryMeters = null;
            double carryX = 0, carryZ = 0, carryTime = 0;
            double apexMeters = 0;

            while (time < MaxDuration)
            {
                if (airborne)
                {
                    double rx = vx - windX, ry = vy, rz = vz - windZ; // velocity through the air
                    double v = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                    double ax = 0, ay = -gravity, az = 0;
                    if (v > 0.01)
                    {
                        // Fits to launch-monitor flights: drag grows with spin ratio, lift rises
                        // quickly then saturates, and spin fades slowly.
                        double spinRatio = radius * spin / v;
                        double drag = 0.20 + 0.35 * spinRatio;
                        double lift = Math.Min(0.32, 0.45 * Math.Pow(spinRatio, 0.4));
                        double dynamicPressure = 0.5 * airDensity * area * v * v;
                        double dx = rx / v, dy = ry / v, dz = rz / v;
                        // Backspin axis lies flat and perpendicular to travel; tilting it adds sideways lift.
                        double fx = -dz, fz = dx;
                        double fl = Math.Sqrt(fx * fx + fz * fz);
                        if (fl < 1e-9) { fx = 1; fz = 0; fl = 1; }
                        fx /= fl; fz /= fl;
                        double axX = fx * Math.Cos(tilt), axY = Math.Sin(tilt), axZ = fz * Math.Cos(tilt);
                        double al = Math.Sqrt(axX * axX + axY * axY + axZ * axZ);
                        axX /= al; axY /= al; axZ /= al;
                        // lift direction = axis × direction
                        double lx = axY * dz - axZ * dy;
                        double ly = axZ * dx - axX * dz;
                        double lz = axX * dy - axY * dx;
                        double k = dynamicPressure / mass;
                        ax += (-dx * drag + lx * lift) * k;
                        ay += (-dy * drag + ly * lift) * k;
                        az += (-dz * drag + lz * lift) * k;
                        spin *= Math.Exp(-dt / 45);
                    }
                    vx += ax * dt; vy += ay * dt; vz += az * dt;
                    px += vx * dt; py += vy * dt; pz += vz * dt;
                    apexMeters = Math.Max(apexMeters, py);
                    if (py <= 0 && vy < 0)
                    {
                        py = 0;
                        if (carryMeters is null) { carryMeters = Math.Sqrt(px * px + pz * pz); carryX = px; carryZ = pz; carryTime = time + dt; }
                        vy = -vy * restitution;
                        // Backspin grips the turf on the first bounce: a driver keeps rolling, a
                        // spinning iron hops and stops, a wedge checks up almost where it lands.
                        double grip = Math.Max(0.08, bounceFriction - 0.5 * (spin * 60 / (2 * Math.PI)) / 10_000);
                        vx *= grip; vz *= grip;
                        spin = 0;
                        if (vy < 1.2) { vy = 0; airborne = false; rolling = true; rollStartTime = time; rollVx = vx; rollVz = vz; }
                    }
                }
                else if (rolling)
                {
                    double ground = Math.Sqrt(vx * vx + vz * vz);
                    if (ground <= 0) break;
                    double slowed = Math.Max(0, ground - rollingDeceleration * dt);
                    double dx = vx / ground, dz = vz / ground;
                    double movingTime = Math.Min(dt, ground / rollingDeceleration);
                    double travel = ground * movingTime - 0.5 * rollingDeceleration * movingTime * movingTime;
                    vx = dx * slowed; vy = 0; vz = dz * slowed;
                    px += dx * travel; pz += dz * travel; py = 0;
                }
                time += dt;
                if (time + 1e-9 >= nextSample)
                {
                    samples.Add(new FlightPoint(px / MetersPerYard, Math.Max(0, py) / MetersPerYard, pz / MetersPerYard));
                    nextSample += SampleInterval;
                }
            }
            double finalDistance = Math.Sqrt(px * px + pz * pz);
            samples.Add(new FlightPoint(px / MetersPerYard, 0, pz / MetersPerYard));
            double carry = (carryMeters ?? 0) / MetersPerYard; // a shot that never flew is all roll
            var carryPoint = new FlightPoint(carryX / MetersPerYard, 0, carryZ / MetersPerYard);
            return new BallFlight(samples, carry, carryPoint, carryTime, Math.Max(0, finalDistance / MetersPerYard - carry), apexMeters / MetersPerYard,
                rollStartTime, rollVx / MetersPerYard, rollVz / MetersPerYard);
        }
    }
}
