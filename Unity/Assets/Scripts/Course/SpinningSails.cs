using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// Windmill Links' sails, for the ball: blades turning in an upright plane about their hub, as
    /// the model draws them. A shot through the plane meets a blade or slips between two, depending
    /// on where the blades have turned to when it gets there — so the flight's clock and the
    /// blades' share one start: `AngleAtLaunch`, their turn as the ball leaves the club. World
    /// units (course yards, world heights) throughout.
    public sealed class SpinningSails
    {
        public readonly double HubX, HubY, HubD;
        /// The axle, a unit vector; the blades turn in the plane square to it.
        public readonly double Ax, Ay, Ad;
        /// The plane's two axes with the blades at rest (V = axle × U, so a turn carries U toward V).
        public readonly double Ux, Uy, Ud, Vx, Vy, Vd;
        /// Hub to the tips of the blades.
        public readonly double Reach;
        /// How far either side of the plane a ball still meets the lattice.
        public const double Thickness = 0.5;
        /// The blades at rest as triangles in the plane: (u0, v0, u1, v1, u2, v2) each.
        readonly double[] tris;

        public double DegreesPerSecond = 36;
        /// Degrees the blades have turned from rest when the ball leaves the club, set for each shot.
        public double AngleAtLaunch;
        /// Where the drawn blades are now, and a way to hold them at an angle (null lets them turn
        /// on by themselves): the game holds them to the flight's clock while a shot is in the air.
        public Func<double> CurrentAngle = () => 0;
        public Action<double?> Drive = _ => { };

        public SpinningSails(double hubX, double hubY, double hubD, double ax, double ay, double ad, double ux, double uy, double ud, IReadOnlyList<double> triangles)
        {
            HubX = hubX; HubY = hubY; HubD = hubD;
            double al = Math.Sqrt(ax * ax + ay * ay + ad * ad);
            Ax = ax / al; Ay = ay / al; Ad = ad / al;
            // U square to the axle, then V = A × U
            double k = ux * Ax + uy * Ay + ud * Ad;
            ux -= k * Ax; uy -= k * Ay; ud -= k * Ad;
            double ul = Math.Sqrt(ux * ux + uy * uy + ud * ud);
            Ux = ux / ul; Uy = uy / ul; Ud = ud / ul;
            Vx = Ay * Ud - Ad * Uy; Vy = Ad * Ux - Ax * Ud; Vd = Ax * Uy - Ay * Ux;
            tris = new double[triangles.Count];
            double reach = 0;
            for (int i = 0; i < tris.Length; i++)
            {
                tris[i] = triangles[i];
                if (i % 2 == 1) reach = Math.Max(reach, Math.Sqrt(tris[i - 1] * tris[i - 1] + tris[i] * tris[i]));
            }
            Reach = reach;
        }

        /// The blades' turn, degrees, `t` seconds after the ball leaves the club.
        public double AngleAt(double t) => AngleAtLaunch + DegreesPerSecond * t;

        /// A world point against the sails: `s` off the plane (along the axle), (u, v) in it.
        public (double s, double u, double v) Local(double x, double y, double d)
        {
            double qx = x - HubX, qy = y - HubY, qd = d - HubD;
            return (qx * Ax + qy * Ay + qd * Ad, qx * Ux + qy * Uy + qd * Ud, qx * Vx + qy * Vy + qd * Vd);
        }

        /// Is the point (u, v) in the plane on a blade, `t` seconds after the ball left?
        public bool OnBlade(double u, double v, double t)
        {
            if (u * u + v * v > Reach * Reach) return false;
            // back to where that bit of blade was at rest: turn the point the other way
            double a = AngleAt(t) * Math.PI / 180, c = Math.Cos(a), sn = Math.Sin(a);
            double ru = u * c + v * sn, rv = -u * sn + v * c;
            for (int i = 0; i + 5 < tris.Length; i += 6)
                if (InTriangle(ru, rv, tris[i], tris[i + 1], tris[i + 2], tris[i + 3], tris[i + 4], tris[i + 5])) return true;
            return false;
        }

        static bool InTriangle(double px, double py, double ax, double ay, double bx, double by, double cx, double cy)
        {
            double d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
            double d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
            double d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// The blade's own velocity at a world point: the turn sweeps it round the hub.
        public (double x, double y, double d) BladeVelocity(double x, double y, double d)
        {
            double w = DegreesPerSecond * Math.PI / 180;
            double rx = x - HubX, ry = y - HubY, rd = d - HubD;
            return (w * (Ay * rd - Ad * ry), w * (Ad * rx - Ax * rd), w * (Ax * ry - Ay * rx));
        }

        /// The ball's velocity off a blade: back off the face with a little bounce, and swept along
        /// the way the blade is turning.
        public (double vx, double vy, double vd) Rebound(double x, double y, double d, double vx, double vy, double vd)
        {
            double vn = vx * Ax + vy * Ay + vd * Ad;
            double tx = vx - vn * Ax, ty = vy - vn * Ay, td = vd - vn * Ad;
            var b = BladeVelocity(x, y, d);
            return (0.6 * tx - 0.45 * vn * Ax + 0.8 * b.x, 0.6 * ty - 0.45 * vn * Ay + 0.8 * b.y, 0.6 * td - 0.45 * vn * Ad + 0.8 * b.d);
        }
    }
}
