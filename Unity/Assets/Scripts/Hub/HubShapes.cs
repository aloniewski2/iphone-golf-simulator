using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Hub
{
    /// Smooth, soft-edged meshes for the Plaza's look (concept 03: rounded plastic pavilions, planters, lamp posts): surfaces of
    /// revolution from 2D profiles with filleted corners, rounded boxes, faceted low-poly crowns. All built once and cached.
    public static class HubShapes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// A profile point (radius, height). `Fillet` turns a polyline's corners into arcs before it is lathed.
        public static List<Vector2> Fillet(IList<Vector2> pts, float radius, int steps = 5)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == 0 || i == pts.Count - 1 || radius <= 0) { o.Add(pts[i]); continue; }
                var a = pts[i - 1]; var b = pts[i]; var c = pts[i + 1];
                var da = (a - b); var dc = (c - b); float la = da.magnitude, lc = dc.magnitude;
                if (la < 1e-4f || lc < 1e-4f) { o.Add(b); continue; }
                da /= la; dc /= lc;
                float r = Mathf.Min(radius, la * .45f, lc * .45f);
                var p0 = b + da * r; var p1 = b + dc * r;
                for (int s = 0; s <= steps; s++)
                {
                    float t = s / (float)steps;
                    // quadratic Bezier through the corner: a smooth round
                    var q = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * b + t * t * p1;
                    o.Add(q);
                }
            }
            return o;
        }

        /// A surface of revolution around +y from a profile of (radius, height) points, smooth-shaded along the profile except at
        /// `hard` corners (sharp edges get split normals). Profile order bottom -> top gives outward faces.
        public static Mesh Lathe(string key, IList<Vector2> profile, int segments = 64, float hardAngle = 50f)
        {
            if (cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            // profile normals: per segment, averaged at smooth joints, split at hard corners
            int count = profile.Count;
            var segN = new Vector2[count - 1];
            for (int i = 0; i < count - 1; i++) { var d = profile[i + 1] - profile[i]; segN[i] = new Vector2(d.y, -d.x).normalized; }
            float along = 0;
            for (int i = 0; i < count - 1; i++)
            {
                var p0 = profile[i]; var p1 = profile[i + 1];
                Vector2 n0 = segN[i], n1 = segN[i];
                if (i > 0 && Vector2.Angle(segN[i - 1], segN[i]) < hardAngle) n0 = (segN[i - 1] + segN[i]).normalized;
                if (i < count - 2 && Vector2.Angle(segN[i], segN[i + 1]) < hardAngle) n1 = (segN[i] + segN[i + 1]).normalized;
                float len = (p1 - p0).magnitude;
                int start = v.Count;
                for (int s = 0; s <= segments; s++)
                {
                    float a = s * Mathf.PI * 2 / segments; float sx = Mathf.Sin(a), cz = Mathf.Cos(a);
                    v.Add(new Vector3(sx * p0.x, p0.y, cz * p0.x)); n.Add(new Vector3(sx * n0.x, n0.y, cz * n0.x).normalized); uv.Add(new Vector2(s / (float)segments, along));
                    v.Add(new Vector3(sx * p1.x, p1.y, cz * p1.x)); n.Add(new Vector3(sx * n1.x, n1.y, cz * n1.x).normalized); uv.Add(new Vector2(s / (float)segments, along + len));
                }
                along += len;
                for (int s = 0; s < segments; s++)
                {
                    int b = start + s * 2;
                    t.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
                }
            }
            var m = new Mesh { name = key, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
            cache[key] = m; return m;
        }

        /// A box with rounded edges and corners (radius r), centred on the origin: a cube grid whose points are pulled onto the
        /// rounded surface (clamp to the inner box, push out by r along the difference).
        public static Mesh RoundedBox(Vector3 size, float r, int div = 5)
        {
            string key = $"rbox {size.x:0.###} {size.y:0.###} {size.z:0.###} {r:0.###} {div}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            var half = size / 2;
            r = Mathf.Max(.001f, Mathf.Min(r, half.x - .001f, half.y - .001f, half.z - .001f));
            var inner = half - Vector3.one * r;
            List<float> Samples(float H, float I)
            {
                var l = new List<float>();
                for (int k = 0; k <= div; k++) l.Add(Mathf.Lerp(-H, -I, k / (float)div));
                for (int k = 0; k <= div; k++) l.Add(Mathf.Lerp(I, H, k / (float)div));
                return l;
            }
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            void Face(int axis, float sign)
            {
                int ua = (axis + 1) % 3, wa = (axis + 2) % 3;
                var us = Samples(half[ua], inner[ua]); var ws = Samples(half[wa], inner[wa]);
                int start = v.Count;
                foreach (var uu in us) foreach (var ww in ws)
                {
                    var p = Vector3.zero; p[axis] = sign * half[axis]; p[ua] = uu; p[wa] = ww;
                    var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    var d = (p - q); d = d.sqrMagnitude > 1e-12f ? d.normalized : Vector3.zero;
                    if (d == Vector3.zero) d[axis] = sign;
                    v.Add(q + d * r); n.Add(d);
                }
                int cols = ws.Count;
                for (int a = 0; a < us.Count - 1; a++)
                    for (int b = 0; b < cols - 1; b++)
                    {
                        int i0 = start + a * cols + b, i1 = i0 + 1, i2 = i0 + cols, i3 = i2 + 1;
                        t.AddRange(new[] { i0, i1, i2, i2, i1, i3 });
                    }
            }
            for (int axis = 0; axis < 3; axis++) { Face(axis, 1); Face(axis, -1); }
            var mesh = new Mesh { name = key }; mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(t, 0);
            FixWinding(mesh); mesh.RecalculateBounds();
            cache[key] = mesh; return mesh;
        }

        /// Makes every triangle face along its vertices' normals (front = clockwise seen from outside, Unity's convention).
        public static void FixWinding(Mesh m)
        {
            var v = m.vertices; var n = m.normals; var t = m.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = v[t[i]]; var b = v[t[i + 1]]; var c = v[t[i + 2]];
                var fn = Vector3.Cross(b - a, c - a);
                var avg = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(fn, avg) < 0) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
            }
            m.triangles = t;
        }

        /// A faceted low-poly blob (a tree crown or bush): a few overlapping balls with a seeded wobble, flat-shaded.
        public static Mesh Crown(int seed, int lumps = 4, float wobble = .12f)
        {
            string key = $"crown {seed} {lumps} {wobble}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            var rng = new System.Random(seed);
            var ball = HubKit.Ball(1);
            var bv = ball.vertices; var bt = ball.triangles;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int l = 0; l < lumps; l++)
            {
                float sc = l == 0 ? 1f : .55f + (float)rng.NextDouble() * .3f;
                var off = l == 0 ? Vector3.zero : new Vector3((float)rng.NextDouble() * 1.2f - .6f, (float)rng.NextDouble() * .5f - .1f, (float)rng.NextDouble() * 1.2f - .6f);
                // per-vertex wobble keyed on the position so shared corners stay welded
                Vector3 W(Vector3 p) { int h = Mathf.RoundToInt(p.x * 97 + p.y * 57 + p.z * 31 + seed * 13 + l * 7); float k = 1 + wobble * Mathf.Sin(h * 1.37f); return p * k; }
                for (int i = 0; i < bt.Length; i += 3)
                {
                    var a = W(bv[bt[i]]) * sc + off; var b = W(bv[bt[i + 1]]) * sc + off; var c = W(bv[bt[i + 2]]) * sc + off;
                    var fn = Vector3.Cross(b - a, c - a).normalized;
                    t.Add(v.Count); v.Add(a); n.Add(fn); t.Add(v.Count); v.Add(b); n.Add(fn); t.Add(v.Count); v.Add(c); n.Add(fn);
                }
            }
            var m = new Mesh { name = key }; m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            cache[key] = m; return m;
        }

        /// A flat disc (y up) of radius 1 with UVs mapped across it (0..1), for floor decals and icons.
        public static Mesh Disc(int segments = 64)
        {
            string key = $"disc {segments}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(.5f, .5f) }; var t = new List<int>();
            for (int i = 0; i <= segments; i++) { float a = i * Mathf.PI * 2 / segments; var p = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); v.Add(p); uv.Add(new Vector2(p.x * .5f + .5f, p.z * .5f + .5f)); }
            for (int i = 0; i < segments; i++) t.AddRange(new[] { 0, i + 1, i + 2 });
            var m = new Mesh { name = key }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            var normals = new Vector3[v.Count]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up; m.normals = normals; m.RecalculateBounds();
            cache[key] = m; return m;
        }

        /// A quad (x right, y up, facing -z) of `size`, UV 0..1.
        public static Mesh Quad(Vector2 size)
        {
            string key = $"quad {size.x} {size.y}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            var m = new Mesh { name = key };
            float w = size.x / 2, h = size.y / 2;
            m.vertices = new[] { new Vector3(-w, -h, 0), new Vector3(-w, h, 0), new Vector3(w, h, 0), new Vector3(w, -h, 0) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.RecalculateBounds(); cache[key] = m; return m;
        }
    }
}
