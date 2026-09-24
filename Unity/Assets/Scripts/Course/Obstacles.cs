using System;

namespace GolfArcade.Course
{
    /// What something standing on the course does to a ball that meets it: a tree's crown and a
    /// bush are leaves and twigs the ball rattles into, a trunk, a rock and a wall are hard and it
    /// bounces off them.
    public enum ObstacleKind { Tree, Bush, Rock, Wall }

    /// One thing standing on the course, read off the hole's model (HoleView), in course yards;
    /// heights are world heights, the same as the ground's. A tree is a trunk up the middle and a
    /// crown over it, a pine's narrowing to its tip, a broadleaf's round; a bush and a rock are
    /// round; a wall (a lighthouse, a tower, a clubhouse) is an upright cylinder.
    public struct Obstacle
    {
        public ObstacleKind Kind;
        /// Its centre over the ground.
        public double X, D;
        /// World heights of its foot and its top.
        public double Base, Top;
        /// Across the crown, the bush, the rock or the wall at its widest, from the middle.
        public double Radius;
        /// A tree's trunk, from the middle, and the world height its branches start at.
        public double TrunkRadius, CrownBase;
        /// A pine's crown narrows to its tip; otherwise the crown is round.
        public bool Cone;

        public static Obstacle Tree(double x, double d, double baseY, double top, double radius, double crownBase, double trunk = 0.18, bool cone = true)
            => new() { Kind = ObstacleKind.Tree, X = x, D = d, Base = baseY, Top = top, Radius = radius, CrownBase = crownBase, TrunkRadius = trunk, Cone = cone };
        public static Obstacle Round(ObstacleKind kind, double x, double d, double baseY, double top, double radius)
            => new() { Kind = kind, X = x, D = d, Base = baseY, Top = top, Radius = radius };

        /// True for what the ball bounces off; false for the leaves it is caught in.
        public bool IsHard(bool trunk) => Kind switch { ObstacleKind.Tree => trunk, ObstacleKind.Bush => false, _ => true };

        /// Is a ball of radius `r` at (x, y, d) touching it? `trunk` says which part of a tree it
        /// met; `depth` is how far in the point is, 0 at the skin to 1 at the core.
        public bool Touches(double x, double y, double d, double r, out bool trunk, out double depth)
        {
            trunk = false; depth = 0;
            if (y < Base - r || y > Top + r) return false;
            double dx = x - X, dd = d - D, across = Math.Sqrt(dx * dx + dd * dd);
            switch (Kind)
            {
                case ObstacleKind.Tree:
                {
                    if (across <= TrunkRadius + r && y <= Top) { trunk = true; depth = 1; return true; }
                    if (y < CrownBase - r) return false;
                    double reach = CrownReach(y) + r;
                    if (across > reach) return false;
                    depth = 1 - across / reach;
                    return true;
                }
                case ObstacleKind.Wall:
                    if (across > Radius + r) return false;
                    depth = 1 - across / (Radius + r);
                    return true;
                default:
                {
                    // round: an ellipsoid over its footprint and its height
                    double half = Math.Max(0.05, (Top - Base) / 2), mid = Base + half;
                    double ex = across / (Radius + r), ey = (y - mid) / (half + r);
                    double k = ex * ex + ey * ey;
                    if (k > 1) return false;
                    depth = 1 - Math.Sqrt(k);
                    return true;
                }
            }
        }

        /// How far the crown reaches out from the trunk at world height y.
        public double CrownReach(double y)
        {
            double h = Math.Max(0.05, Top - CrownBase), u = (y - CrownBase) / h;
            if (u < 0 || u > 1) return 0;
            if (Cone) return Radius * (1 - u);
            double v = 2 * u - 1;
            return Radius * Math.Sqrt(Math.Max(0, 1 - v * v));
        }

        /// How deep into the leaves a ball's line goes, 0 grazing the edge to 1 straight through
        /// the trunk: from how close its path over the ground passes the middle, against the
        /// crown's (or the bush's) reach at its height. A ball dropping into it goes deep.
        public double PassDepth(double x, double y, double d, double vx, double vd, double r)
        {
            double reach = (Kind == ObstacleKind.Tree ? CrownReach(y) : Radius) + r;
            double speed = Math.Sqrt(vx * vx + vd * vd);
            if (speed < 1e-6 || reach <= 1e-6) return 1;
            double past = Math.Abs((X - x) * vd / speed - (D - d) * vx / speed);
            return Math.Max(0, Math.Min(1, 1 - past / reach));
        }

        /// Its reach at the ground, for a rolling ball: a tree's trunk, a bush's or a rock's
        /// skirt, a wall's side.
        public double Footprint => Kind switch
        {
            ObstacleKind.Tree => TrunkRadius,
            ObstacleKind.Wall => Radius,
            _ => Radius * 0.85,
        };

        /// The way out of a hard surface at (x, y, d): straight out from a trunk or a wall, out
        /// from a rock's middle.
        public (double x, double y, double d) Normal(double x, double y, double d)
        {
            double nx = x - X, nd = d - D, ny = 0;
            if (Kind == ObstacleKind.Rock || Kind == ObstacleKind.Bush)
            {
                double half = Math.Max(0.05, (Top - Base) / 2), mid = Base + half;
                nx /= Math.Max(0.05, Radius * Radius); nd /= Math.Max(0.05, Radius * Radius); ny = (y - mid) / (half * half);
            }
            double l = Math.Sqrt(nx * nx + ny * ny + nd * nd);
            if (l < 1e-9) return (1, 0, 0);
            return (nx / l, ny / l, nd / l);
        }
    }
}
