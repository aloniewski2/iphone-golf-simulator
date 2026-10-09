using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Alternate shoreline compositions authored in ReferenceHole09/12.blend.
    /// Analytic land/water regions use the same map as the imported collision terrain.
    public static class GolfCoastalComposition
    {
        static readonly HashSet<Hole> mapped = new();
        public static bool Enabled => Environment.GetEnvironmentVariable("VISUAL_COASTAL_REFERENCE") != "0";
        public static double DownrangeMetres(double y)
        {
            if (y <= 8 || y >= 110) return y;
            return y <= 60 ? 8 + (y-8)*6/52 : 14 + (y-60)*96/50;
        }
        public static double SplitAcrossMetres(double x,double y)
        {
            double factor=y<=60?.42:.42+.58*Math.Min(1,(y-60)/50);
            return x>=-5?x:-5+(x+5)*factor;
        }
        public static GameObject Model(Hole hole,GameObject original)
        {
            if (hole.Number is not (9 or 12) || !Enabled) return original;
            var reference=Resources.Load<GameObject>($"Course/Resort/ReferenceHole{hole.Number:00}");
            if (!reference) return original;
            if (mapped.Add(hole))
            {
                double origin=hole.Tee.D,scale=hole.Number==9?1/.9144:(hole.Pin.D-origin)/181.0;
                CoursePoint Point(CoursePoint p) => new CoursePoint(hole.Number==9
                    ?hole.Tee.X+SplitAcrossMetres((p.X-hole.Tee.X)/scale,(p.D-origin)/scale)*scale:p.X,
                    origin+DownrangeMetres((p.D-origin)/scale)*scale);
                CoursePoint[] Polygon(CoursePoint[] source)
                {
                    var result = new List<CoursePoint>(source.Length + 6);
                    // A straight source edge bends wherever the warp slope changes.
                    // Retain those bends so dry-land scoring agrees with the terrain.
                    for (int i = 0; i < source.Length; i++)
                    {
                        var a = source[i]; var b = source[(i + 1) % source.Length];
                        if(hole.Number==9){
                            // The near lagoon narrows in two axes. Dense analytic edges
                            // track the same continuous map used by its authored terrain.
                            int steps=Math.Max(1,(int)Math.Ceiling(a.DistanceTo(b)));
                            for(int j=0;j<steps;j++){double t=(double)j/steps;result.Add(Point(new CoursePoint(a.X+(b.X-a.X)*t,a.D+(b.D-a.D)*t)));}
                            continue;
                        }
                        result.Add(Point(a));
                        double delta = b.D - a.D;
                        if (Math.Abs(delta) < 1e-10) continue;
                        var crossings = new List<double>(3);
                        foreach (double metres in new[] { 8.0, 60.0, 110.0 })
                        {
                            double t = (origin + metres * scale - a.D) / delta;
                            if (t > 1e-9 && t < 1 - 1e-9) crossings.Add(t);
                        }
                        crossings.Sort();
                        foreach (double t in crossings)
                            result.Add(Point(new CoursePoint(a.X + (b.X - a.X) * t, a.D + delta * t)));
                    }
                    return result.ToArray();
                }
                if(hole.Shore!=null)hole.Shore=Polygon(hole.Shore);
                hole.Islets=hole.Islets.Select(Polygon).ToArray();
                // Split's straight tee route and its approach targets stay fixed; only
                // the adjacent lagoon is brought forward. Hole 12 maps its bridge route.
                if(hole.Number==12)hole.Centerline=hole.Centerline.Select(Point).ToArray();
                Debug.Log("[GolfCoastalComposition] reference shoreline and collision model selected; tee/pin retained");
            }
            return reference;
        }
    }
}
