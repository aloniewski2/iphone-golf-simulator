using System;
using System.Collections.Generic;

namespace GolfArcade.Course
{
    /// The shape of the ground a ball rolls over, in yards. The roll accelerates down its
    /// gradient, the green-reading grid is draped over its height and coloured by its slope, so
    /// what the player sees is exactly what the putt will do.
    public interface ISurface
    {
        /// Height above the hole's datum.
        double Height(CoursePoint p);
        /// Rise per yard toward +X (right) and +D (down the hole). A ball accelerates the other way.
        (double dx, double dd) Gradient(CoursePoint p);
    }

    public static class Surfaces
    {
        /// Steepness as a fraction (0.02 = 2%).
        public static double Slope(this ISurface s, CoursePoint p)
        {
            var g = s.Gradient(p);
            return Math.Sqrt(g.dx * g.dx + g.dd * g.dd);
        }
    }

    public sealed class FlatSurface : ISurface
    {
        public static readonly FlatSurface Instance = new();
        public double Height(CoursePoint p) => 0;
        public (double dx, double dd) Gradient(CoursePoint p) => (0, 0);
    }

    /// Ground described by hand: an overall tilt plus mounds, swales and ridges. The hole's
    /// primitive greens use it, and so do the tests, since it has an exact gradient.
    public sealed class Contours : ISurface
    {
        public struct Feature
        {
            public CoursePoint Center;
            /// A ridge runs from `Center` to `End`; a mound or swale has no end.
            public CoursePoint? End;
            public double Radius;
            /// Yards; negative sinks a swale or hollow.
            public double Height;

            /// Distance from the feature's spine, and the unit direction away from it.
            internal (double distance, double awayX, double awayD) Span(CoursePoint p)
            {
                var nearest = Center;
                if (End is CoursePoint end)
                {
                    double dx = end.X - Center.X, dd = end.D - Center.D;
                    double length = Math.Max(dx * dx + dd * dd, 0.0001);
                    double t = Math.Min(1, Math.Max(0, ((p.X - Center.X) * dx + (p.D - Center.D) * dd) / length));
                    nearest = new CoursePoint(Center.X + dx * t, Center.D + dd * t);
                }
                double distance = p.DistanceTo(nearest);
                if (distance <= 0.0001) return (0, 0, 0);
                return (distance, (p.X - nearest.X) / distance, (p.D - nearest.D) / distance);
            }
        }

        public double TiltX, TiltD;
        public List<Feature> Features = new();

        public static Feature Mound(double x, double d, double radius, double height) =>
            new() { Center = new CoursePoint(x, d), Radius = radius, Height = height };
        public static Feature Ridge(double x1, double d1, double x2, double d2, double radius, double height) =>
            new() { Center = new CoursePoint(x1, d1), End = new CoursePoint(x2, d2), Radius = radius, Height = height };

        public double Height(CoursePoint p)
        {
            double h = TiltX * p.X + TiltD * p.D;
            foreach (var f in Features)
            {
                double u = f.Span(p).distance / Math.Max(f.Radius, 0.001);
                if (u >= 1) continue;
                // (1 - u²)²: flat on top and at the rim, so nothing kinks.
                h += f.Height * (1 - u * u) * (1 - u * u);
            }
            return h;
        }

        public (double dx, double dd) Gradient(CoursePoint p)
        {
            double dx = TiltX, dd = TiltD;
            foreach (var f in Features)
            {
                var span = f.Span(p);
                double radius = Math.Max(f.Radius, 0.001);
                double u = span.distance / radius;
                if (u >= 1 || u <= 0) continue;
                double slope = f.Height * -4 * u * (1 - u * u) / radius;
                dx += slope * span.awayX; dd += slope * span.awayD;
            }
            return (dx, dd);
        }
    }

    /// Ground read off a modelled hole: heights sampled on a regular grid (from the mesh the
    /// player sees), bilinear in between, so a green sculpted in Blender breaks putts exactly as
    /// it looks. Outside the grid the ground is taken as flat at the nearest edge.
    public sealed class HeightGrid : ISurface
    {
        readonly double x0, d0, step;
        readonly int nx, nd;
        readonly double[] heights;

        public HeightGrid(double x0, double d0, double step, int nx, int nd, double[] heights)
        {
            if (nx < 2 || nd < 2 || heights.Length != nx * nd) throw new ArgumentException("a height grid needs at least 2×2 samples");
            this.x0 = x0; this.d0 = d0; this.step = step; this.nx = nx; this.nd = nd; this.heights = heights;
        }

        /// Sample `height` over the rectangle from (x0, d0) spanning `width` × `length` yards.
        public static HeightGrid Sample(double x0, double d0, double width, double length, double step, Func<CoursePoint, double> height)
        {
            int nx = Math.Max(2, (int)Math.Ceiling(width / step) + 1), nd = Math.Max(2, (int)Math.Ceiling(length / step) + 1);
            var h = new double[nx * nd];
            for (int j = 0; j < nd; j++)
                for (int i = 0; i < nx; i++)
                    h[j * nx + i] = height(new CoursePoint(x0 + i * step, d0 + j * step));
            return new HeightGrid(x0, d0, step, nx, nd, h);
        }

        public bool Covers(CoursePoint p) => p.X >= x0 && p.X <= x0 + (nx - 1) * step && p.D >= d0 && p.D <= d0 + (nd - 1) * step;

        public double Height(CoursePoint p)
        {
            double fx = Math.Min(Math.Max((p.X - x0) / step, 0), nx - 1 - 1e-9), fd = Math.Min(Math.Max((p.D - d0) / step, 0), nd - 1 - 1e-9);
            int i = (int)fx, j = (int)fd;
            double tx = fx - i, td = fd - j;
            double a = heights[j * nx + i], b = heights[j * nx + i + 1], c = heights[(j + 1) * nx + i], d = heights[(j + 1) * nx + i + 1];
            return (a * (1 - tx) + b * tx) * (1 - td) + (c * (1 - tx) + d * tx) * td;
        }

        public (double dx, double dd) Gradient(CoursePoint p)
        {
            if (!Covers(p)) return (0, 0);
            double h = step / 2;
            return ((Height(new CoursePoint(p.X + h, p.D)) - Height(new CoursePoint(p.X - h, p.D))) / (2 * h),
                    (Height(new CoursePoint(p.X, p.D + h)) - Height(new CoursePoint(p.X, p.D - h))) / (2 * h));
        }
    }
}
