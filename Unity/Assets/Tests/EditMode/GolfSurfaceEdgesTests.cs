// GolfSurfaceEdges.ComputeWorld (the _GolfEdgeMap outline-distance texture) against a verbatim copy of the ORIGINAL algorithm
// (per-triangle string keys, brute-force distance field): the pixels, the bounds, every clamped distance (bit for bit) and the
// segment lists must be identical. Pure C# (no Mesh/Transform), so it also runs in Tools/check.sh.
// The real-hole version of the same comparison is Unity/Assets/Editor/SurfaceEdgesCheck.cs (all 16 modelled holes).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GolfArcade.Course;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    public class GolfSurfaceEdgesTests
    {
        const int Size = 256;
        struct Segment { public Vector2 a, b; }
        sealed class Original { public Color32[] Pixels; public Vector4 Bounds; public List<Segment>[] Lines; public float[] Distances; }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static byte Byte(float x) => (byte)Mathf.RoundToInt(Mathf.Clamp01(x) * 255);
        static string Point(Vector3 p) => $"{Mathf.RoundToInt(p.x * 1000)}:{Mathf.RoundToInt(p.z * 1000)}";

        static Original Reference(IList<GolfSurfaceEdges.WorldMesh> meshes)
        {
            var lines = new List<Segment>[] { new(), new(), new(), new() };
            var lo = new Vector2(float.MaxValue, float.MaxValue); var hi = new Vector2(float.MinValue, float.MinValue);
            foreach (var m in meshes)
            {
                int kind = m.Kind; var p = m.Positions;
                var counts = new Dictionary<string, int>(); var segs = new Dictionary<string, Segment>(); var tri = m.Triangles;
                for (int j = 0; j < tri.Length; j += 3)
                {
                    Vector3 a = p[tri[j]], b = p[tri[j + 1]], c = p[tri[j + 2]];
                    if (Vector3.Cross(b - a, c - a).normalized.y < .85f) continue;
                    var t = new[] { a, b, c };
                    for (int k = 0; k < 3; k++)
                    {
                        var x = t[k]; var y = t[(k + 1) % 3]; string u = Point(x), v = Point(y);
                        if (u == v) continue; string key = string.CompareOrdinal(u, v) < 0 ? u + "/" + v : v + "/" + u;
                        counts.TryGetValue(key, out int count); counts[key] = count + 1;
                        segs[key] = new Segment { a = new Vector2(x.x, x.z), b = new Vector2(y.x, y.z) };
                        if (kind < 3) { lo = Vector2.Min(lo, new Vector2(x.x, x.z)); hi = Vector2.Max(hi, new Vector2(x.x, x.z)); }
                    }
                }
                foreach (var e in counts) if (e.Value == 1) lines[kind].Add(segs[e.Key]);
            }
            if (lo.x == float.MaxValue) return null;
            lo -= Vector2.one * 4; hi += Vector2.one * 4; var extent = hi - lo; var origin = lo;
            var pixels = new Color32[Size * Size]; var dist = new float[4 * Size * Size];
            Parallel.For(0, Size, y => {
                for (int x = 0; x < Size; x++)
                {
                    var p = origin + new Vector2((x + .5f) / Size * extent.x, (y + .5f) / Size * extent.y);
                    var distances = new float[4];
                    for (int k = 0; k < 4; k++)
                    {
                        float best = 32 * 32;
                        foreach (var s in lines[k])
                        {
                            var d = s.b - s.a; float den = Vector2.Dot(d, d);
                            float t = den > 0 ? Mathf.Clamp01(Vector2.Dot(p - s.a, d) / den) : 0;
                            best = Mathf.Min(best, (p - s.a - d * t).sqrMagnitude);
                        }
                        distances[k] = Mathf.Sqrt(best); dist[k * Size * Size + y * Size + x] = distances[k];
                    }
                    float apron = 1 - .045f * (1 - Smooth(.3f, 1.8f, distances[1]));
                    float fringe = 1 - .085f * (1 - Smooth(.6f, 2.4f, distances[0]));
                    float sand = 1 + .10f * (1 - Smooth(.3f, 1.8f, distances[2])) - .12f * (1 - Smooth(.5f, 5f, distances[3]));
                    pixels[y * Size + x] = new Color32(Byte(apron * fringe), Byte(apron), Byte(sand * .5f), 255);
                }
            });
            return new Original { Pixels = pixels, Bounds = new Vector4(lo.x, lo.y, 1 / extent.x, 1 / extent.y), Lines = lines, Distances = dist };
        }

        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);

        static void AssertIdentical(IList<GolfSurfaceEdges.WorldMesh> meshes, string label)
        {
            var old = Reference(meshes); var now = GolfSurfaceEdges.ComputeWorld(meshes, true);
            if (old == null) { Assert.IsNull(now, label + ": both must report no map"); return; }
            Assert.IsNotNull(now, label);
            for (int k = 0; k < 4; k++)
            {
                var l = old.Lines[k]; var f = now.Segments[k];
                Assert.AreEqual(l.Count * 4, f.Length, $"{label}: segment count of kind {k}");
                for (int i = 0; i < l.Count; i++)
                    Assert.IsTrue(Bits(l[i].a.x) == Bits(f[i * 4]) && Bits(l[i].a.y) == Bits(f[i * 4 + 1]) && Bits(l[i].b.x) == Bits(f[i * 4 + 2]) && Bits(l[i].b.y) == Bits(f[i * 4 + 3]), $"{label}: segment {k}/{i}");
            }
            Assert.IsTrue(Bits(old.Bounds.x) == Bits(now.Bounds.x) && Bits(old.Bounds.y) == Bits(now.Bounds.y) && Bits(old.Bounds.z) == Bits(now.Bounds.z) && Bits(old.Bounds.w) == Bits(now.Bounds.w), label + ": bounds");
            for (int i = 0; i < old.Distances.Length; i++) if (Bits(old.Distances[i]) != Bits(now.Distances[i])) Assert.Fail($"{label}: distance {i} {old.Distances[i]:R} vs {now.Distances[i]:R}");
            for (int i = 0; i < old.Pixels.Length; i++)
            {
                var a = old.Pixels[i]; var b = now.Pixels[i];
                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) Assert.Fail($"{label}: pixel {i}");
            }
        }

        // ---- synthetic worlds

        /// A rough heightfield patch with random holes, steep bits (rejected as non-top triangles), repeated and sub-millimetre-apart vertices and degenerate triangles.
        static GolfSurfaceEdges.WorldMesh Grid(System.Random rng, int kind, int nx, int nz, float cell, Vector2 at, float roughness, float holeChance)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var id = new int[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++) for (int j = 0; j <= nz; j++)
            {
                float x = at.x + i * cell + (float)(rng.NextDouble() - .5) * cell * .3f, z = at.y + j * cell + (float)(rng.NextDouble() - .5) * cell * .3f;
                id[i, j] = v.Count; v.Add(new Vector3(x, (float)(Math.Sin(x * .05) * Math.Cos(z * .07) * 4 + (rng.NextDouble() - .5) * roughness), z));
            }
            int Dup(int s) { var p = v[s]; v.Add(new Vector3(p.x + (float)(rng.NextDouble() - .5) * .0006f, p.y, p.z + (float)(rng.NextDouble() - .5) * .0006f)); return v.Count - 1; }
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++)
            {
                if (rng.NextDouble() < holeChance) continue;
                int a = id[i, j], b = id[i + 1, j], c = id[i + 1, j + 1], d = id[i, j + 1];
                if (rng.NextDouble() < .3) { a = Dup(a); c = Dup(c); }
                if (rng.NextDouble() < .5) { t.AddRange(new[] { a, d, c }); t.AddRange(new[] { a, c, b }); }
                else { t.AddRange(new[] { a, d, b }); t.AddRange(new[] { b, d, c }); }
                if (rng.NextDouble() < .03) t.AddRange(new[] { a, a, c });
            }
            return new GolfSurfaceEdges.WorldMesh { Positions = v.ToArray(), Triangles = t.ToArray(), Kind = kind };
        }

        /// Flat triangles scattered at random sizes, sharing vertices now and then.
        static GolfSurfaceEdges.WorldMesh Scatter(System.Random rng, int kind, int count, float spread, float maxSize, Vector2 at)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < count; i++)
            {
                float cx = at.x + (float)(rng.NextDouble() - .5) * spread, cz = at.y + (float)(rng.NextDouble() - .5) * spread;
                float size = (float)Math.Pow(rng.NextDouble(), 3) * maxSize + .0004f; int basis = v.Count;
                for (int k = 0; k < 3; k++)
                {
                    if (v.Count > 3 && rng.NextDouble() < .35) v.Add(v[rng.Next(v.Count)]);
                    else v.Add(new Vector3(cx + (float)(rng.NextDouble() - .5) * size * (rng.Next(4) == 0 ? 8 : 1), (float)(rng.NextDouble() - .5) * size * .05f, cz + (float)(rng.NextDouble() - .5) * size));
                }
                if (Vector3.Cross(v[basis + 1] - v[basis], v[basis + 2] - v[basis]).y < 0) t.AddRange(new[] { basis, basis + 2, basis + 1 }); else t.AddRange(new[] { basis, basis + 1, basis + 2 });
            }
            return new GolfSurfaceEdges.WorldMesh { Positions = v.ToArray(), Triangles = t.ToArray(), Kind = kind };
        }

        static GolfSurfaceEdges.WorldMesh Quad(int kind, float x0, float z0, float x1, float z1)
            => new GolfSurfaceEdges.WorldMesh { Kind = kind, Positions = new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) }, Triangles = new[] { 0, 3, 2, 0, 2, 1 } };

        [Test]
        public void TerrainPatchesGiveTheOriginalEdgeMap()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var rng = new System.Random(seed); var meshes = new List<GolfSurfaceEdges.WorldMesh>();
                float world = seed % 2 == 0 ? 200f : 600f;
                for (int i = 0; i < 1 + rng.Next(3); i++)
                    meshes.Add(Grid(rng, rng.Next(4), 8 + rng.Next(20), 8 + rng.Next(20), world / (30 + rng.Next(40)), new Vector2((float)(rng.NextDouble() - .5) * world * .5f, (float)(rng.NextDouble() - .5) * world * .5f), rng.Next(2) == 0 ? .1f : 5f, (float)rng.NextDouble() * .3f));
                if (seed == 3) meshes.Add(Quad(3, -3000, -3000, 3000, 3000));
                AssertIdentical(meshes, "terrain seed " + seed);
            }
        }

        [Test]
        public void ScatteredLongAndTinyTrianglesGiveTheOriginalEdgeMap()
        {
            for (int seed = 1; seed <= 3; seed++)
            {
                var rng = new System.Random(seed * 7919 + 13); var meshes = new List<GolfSurfaceEdges.WorldMesh>();
                float offset = seed == 2 ? 5000f : 0;
                for (int i = 0; i < 1 + rng.Next(3); i++)
                    meshes.Add(Scatter(rng, rng.Next(4), 150 + rng.Next(500), rng.Next(2) == 0 ? 80f : 700f, rng.Next(2) == 0 ? 3f : 60f, new Vector2(offset + (float)(rng.NextDouble() - .5) * 100, -offset)));
                AssertIdentical(meshes, "scatter seed " + seed);
            }
        }

        [Test]
        public void NestedQuadsAndWaterOnlyBehaveLikeTheOriginal()
        {
            AssertIdentical(new List<GolfSurfaceEdges.WorldMesh> { Quad(0, -10, -10, 10, 10), Quad(1, -100, -100, 100, 100), Quad(2, 5, 5, 6, 6), Quad(3, -3000, -3000, 3000, 3000) }, "quads");
            AssertIdentical(new List<GolfSurfaceEdges.WorldMesh> { Quad(3, -50, -50, 50, 50) }, "water only");
            Assert.IsNull(GolfSurfaceEdges.ComputeWorld(new List<GolfSurfaceEdges.WorldMesh> { Quad(3, -50, -50, 50, 50) }), "water alone gives no bounds, so Apply resets to the neutral map");
        }
    }
}
