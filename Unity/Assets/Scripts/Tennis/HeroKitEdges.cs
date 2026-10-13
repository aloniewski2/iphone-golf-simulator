using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Bald-base pass 2026-10-12: a clear sole line on the tennis shoes and a clean ribbed cuff on the tennis socks.
    ///
    /// The kit's garment masks are too coarse there (about 0.6 texels per millimetre on the sole wall), so the lines are drawn by
    /// TennisCloth from per-vertex measurements carried in mesh UV channel 3 and anti-aliased in the shader (_EdgeLines 1 = shoe,
    /// 2 = sock). Measurements are taken once per source mesh in its bind pose, in metres:
    ///   shoe  x = height above the sole's own bottom (sole submesh 1; -1 on the upper), y = signed height above the sole's top edge
    ///         (the ledge the upper stands on, ~10 mm inside the sole's outer wall), z = 1
    ///   sock  x = depth below the sock's top opening, y / z = direction round the leg (cos, sin), w = rib count (4 mm ribs)
    /// The distance LOD kit gets the same data (MatchHeroLook.FinishFootwear); a mesh without the channel reads zeros and the shader draws nothing (z / w = 0).
    /// Positions, topology, weights and UVs 0-2 are untouched; the colour picks still recolour the shoe and sock as before.
    public static class HeroKitEdges
    {
        public const float Shoe = 1, Sock = 2;
        const float Reach = .008f, RibPitch = .004f, CuffDepth = .022f;
        static readonly Dictionary<Mesh, Mesh> cache = new();

        /// The kit piece's mesh with its edge measurements, or the mesh unchanged when it cannot be read. `root` is the upright hero.
        public static Mesh Prepare(SkinnedMeshRenderer piece, Transform root, float kind)
        {
            var source = piece ? piece.sharedMesh : null;
            if (!source || !source.isReadable || !root) return source;
            if (cache.TryGetValue(source, out var ready) && ready) return ready;
            // mesh space = the renderer's own space at bind; the hero's vertical, and metres, expressed in it
            var up = piece.transform.InverseTransformDirection(root.up).normalized;
            float metres = Mathf.Abs(piece.transform.lossyScale.x / Mathf.Max(1e-6f, Mathf.Abs(root.lossyScale.x)));
            var side = Vector3.Cross(up, Mathf.Abs(up.x) < .9f ? Vector3.right : Vector3.forward).normalized;
            var depth = Vector3.Cross(up, side);
            var v = source.vertices; int n = v.Length;
            var h = new float[n]; var plane = new Vector2[n];
            for (int i = 0; i < n; i++) { h[i] = Vector3.Dot(v[i], up) * metres; plane[i] = new Vector2(Vector3.Dot(v[i], side), Vector3.Dot(v[i], depth)) * metres; }
            var edge = new List<Vector4>(new Vector4[n]);
            if (kind == Shoe)
            {
                var sole = new bool[n];
                if (source.subMeshCount > 1) foreach (int i in source.GetTriangles(1)) sole[i] = true;
                var soleBottom = Local(plane, h, sole, -1);
                var soleTop = Local(plane, h, sole, 1);
                for (int i = 0; i < n; i++)
                {
                    float x = sole[i] && !float.IsNaN(soleBottom[i]) ? h[i] - soleBottom[i] : -1;
                    float y = float.IsNaN(soleTop[i]) ? 1 : h[i] - soleTop[i];
                    edge[i] = new Vector4(x, y, 1, 0);
                }
            }
            else
            {
                float top = float.MinValue; for (int i = 0; i < n; i++) top = Mathf.Max(top, h[i]);
                // leg centre at each height: mean of the sock's ground-plane positions within 6 mm of that height
                var bands = new Dictionary<int, (Vector2 sum, int count)>();
                int Band(float y) => Mathf.FloorToInt(y / .006f);
                for (int i = 0; i < n; i++) { int b = Band(h[i]); bands.TryGetValue(b, out var s); bands[b] = (s.sum + plane[i], s.count + 1); }
                Vector2 Centre(float y)
                {
                    Vector2 sum = Vector2.zero; int count = 0;
                    for (int b = Band(y) - 1; b <= Band(y) + 1; b++) if (bands.TryGetValue(b, out var s)) { sum += s.sum; count += s.count; }
                    return count > 0 ? sum / count : Vector2.zero;
                }
                float radius = 0; int cuff = 0;
                for (int i = 0; i < n; i++) if (top - h[i] < CuffDepth) { radius += (plane[i] - Centre(h[i])).magnitude; cuff++; }
                radius = cuff > 0 ? radius / cuff : .05f;
                float ribs = Mathf.Max(8, Mathf.Round(2 * Mathf.PI * radius / RibPitch));
                for (int i = 0; i < n; i++)
                {
                    var r = (plane[i] - Centre(h[i])).normalized;
                    edge[i] = new Vector4(top - h[i], r.x, r.y, ribs);
                }
            }
            ready = Object.Instantiate(source); ready.name = source.name + " (kit edges)"; ready.hideFlags = HideFlags.DontSave;
            ready.SetUVs(3, edge);
            cache[source] = ready;
            return ready;
        }

        /// For every vertex: the lowest (sign -1) or highest (sign 1) sole height within Reach in the ground plane, NaN if none.
        static float[] Local(Vector2[] plane, float[] h, bool[] sole, float sign)
        {
            var grid = new Dictionary<Vector2Int, List<int>>();
            Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / Reach), Mathf.FloorToInt(p.y / Reach));
            for (int i = 0; i < h.Length; i++)
                if (sole[i]) { var c = Cell(plane[i]); if (!grid.TryGetValue(c, out var list)) grid[c] = list = new List<int>(); list.Add(i); }
            var result = new float[h.Length];
            for (int i = 0; i < h.Length; i++)
            {
                float extreme = float.NaN; var c = Cell(plane[i]);
                for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
                    if (grid.TryGetValue(new Vector2Int(c.x + x, c.y + y), out var list))
                        foreach (int j in list)
                            if ((plane[j] - plane[i]).sqrMagnitude < Reach * Reach && !(sign * (extreme - h[j]) >= 0)) extreme = h[j];
                result[i] = extreme;
            }
            return result;
        }
    }
}
