using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Shared geometry and texture tools for the sky and crater builders: a mesh accumulator (a whole
    /// family of towers, lumps or rocks becomes one mesh and one draw call), ramp textures, cellular
    /// noise and a textured sky dome. Nothing here touches play.
    ///
    /// Winding rule used throughout (Unity is left-handed): a triangle (a, b, c) faces the viewer when
    /// cross(b - a, c - a) points toward the viewer.
    public static partial class TennisVenueBuilder
    {
        internal sealed class MB
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<Color32> Col = new List<Color32>();
            public readonly List<int> T = new List<int>();
            public readonly List<int> T2 = new List<int>();            // optional second submesh (a second material on part of the surface)
            public readonly List<Vector3> N = new List<Vector3>();   // analytic normals (lumps); used only when every vertex has one
            public bool Colors;
            public int Count => V.Count;
            public int Tris => T.Count / 3;

            public int Add(Vector3 p, Vector2 uv) => Add(p, uv, new Color32(255, 255, 255, 255));
            public int Add(Vector3 p, Vector2 uv, Color32 c) { V.Add(p); UV.Add(uv); Col.Add(c); return V.Count - 1; }

            /// A quad given counter-clockwise as seen from the front (bottom-left, bottom-right, top-right, top-left).
            public void Quad(int a, int b, int c, int d) { T.Add(a); T.Add(c); T.Add(b); T.Add(a); T.Add(d); T.Add(c); }
            /// A triangle given counter-clockwise as seen from the front.
            public void Tri(int a, int b, int c) { T.Add(a); T.Add(c); T.Add(b); }

            /// Four corners (counter-clockwise from the front) with u0..u1 across and v0..v1 up.
            public void Face(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, float u0, float u1, float v0, float v1)
            {
                int a = Add(bl, new Vector2(u0, v0)), b = Add(br, new Vector2(u1, v0)), c = Add(tr, new Vector2(u1, v1)), d = Add(tl, new Vector2(u0, v1));
                Quad(a, b, c, d);
            }

            /// A quad with a UV for each corner (counter-clockwise from the front): for faces whose top edge is not level.
            public void Face4(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, Vector2 uBl, Vector2 uBr, Vector2 uTr, Vector2 uTl)
            {
                int a = Add(bl, uBl), b = Add(br, uBr), c = Add(tr, uTr), d = Add(tl, uTl);
                Quad(a, b, c, d);
            }

            /// A box whose side faces carry facade UVs in world units: u runs along the face (left to right as seen
            /// from outside), v is height, so floors line up across tiers. uvM is metres per texture tile.
            public void Box(Vector3 c, Vector3 s, Vector2 uvM, Vector2 uvOff, bool roof = false, bool floor = false)
            {
                float x0 = c.x - s.x / 2, x1 = c.x + s.x / 2, y0 = c.y - s.y / 2, y1 = c.y + s.y / 2, z0 = c.z - s.z / 2, z1 = c.z + s.z / 2;
                float v0 = y0 / uvM.y + uvOff.y, v1 = y1 / uvM.y + uvOff.y, ox = uvOff.x;
                Face(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), x0 / uvM.x + ox, x1 / uvM.x + ox, v0, v1);       // -z
                Face(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), z0 / uvM.x + ox, z1 / uvM.x + ox, v0, v1);       // +x
                Face(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), -x1 / uvM.x + ox, -x0 / uvM.x + ox, v0, v1);     // +z
                Face(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), -z1 / uvM.x + ox, -z0 / uvM.x + ox, v0, v1);     // -x
                if (roof) Face(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), 0, 1, 0, 1);
                if (floor) Face(new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), 0, 1, 0, 1);
            }

            /// A solid-colour box (every face maps to one texel): trims, roofs, machinery.
            public void Solid(Vector3 c, Vector3 s, Vector2? texel = null)
            {
                var t = texel ?? new Vector2(.5f, .5f);
                float x0 = c.x - s.x / 2, x1 = c.x + s.x / 2, y0 = c.y - s.y / 2, y1 = c.y + s.y / 2, z0 = c.z - s.z / 2, z1 = c.z + s.z / 2;
                void F(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl) { int a = Add(bl, t), b = Add(br, t), cc = Add(tr, t), d = Add(tl, t); Quad(a, b, cc, d); }
                F(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0));
                F(new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0));
                F(new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), new Vector3(x0, y1, z1), new Vector3(x1, y1, z1));
                F(new Vector3(x0, y0, z1), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1));
                F(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1));
                F(new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y0, z1), new Vector3(x1, y0, z1));
            }

            /// A cylinder or cone frustum standing on `bottom`, radius r0 at the foot and r1 at the top. Facade UVs
            /// when uvM.x > 0 (u in metres round the girth, v = height), otherwise one texel.
            public void Cyl(Vector3 bottom, float r0, float r1, float h, int sides, Vector2 uvM, Vector2 uvOff, bool capTop = true, bool capBottom = false)
            {
                bool facade = uvM.x > 0;
                float girth = 2 * Mathf.PI * Mathf.Max(r0, r1);
                float tiles = facade ? Mathf.Max(1, Mathf.Round(girth / uvM.x)) : 1;     // whole tiles round the drum: no seam
                int[] lo = new int[sides + 1], hi = new int[sides + 1];
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    float u = facade ? i / (float)sides * tiles + uvOff.x : .5f;
                    lo[i] = Add(bottom + new Vector3(ca * r0, 0, sa * r0), new Vector2(u, facade ? bottom.y / uvM.y + uvOff.y : .5f));
                    hi[i] = Add(bottom + new Vector3(ca * r1, h, sa * r1), new Vector2(u, facade ? (bottom.y + h) / uvM.y + uvOff.y : .5f));
                }
                for (int i = 0; i < sides; i++) Quad(lo[i], lo[i + 1], hi[i + 1], hi[i]);
                if (capTop && r1 > .001f)
                {
                    int c = Add(bottom + Vector3.up * h, new Vector2(.5f, .5f)); var ring = new int[sides + 1];
                    for (int i = 0; i <= sides; i++) { float a = i / (float)sides * Mathf.PI * 2; ring[i] = Add(bottom + new Vector3(Mathf.Cos(a) * r1, h, Mathf.Sin(a) * r1), new Vector2(.5f, .5f)); }
                    for (int i = 0; i < sides; i++) Tri(c, ring[i], ring[i + 1]);
                }
                if (capBottom)
                {
                    int c = Add(bottom, new Vector2(.5f, .5f)); var ring = new int[sides + 1];
                    for (int i = 0; i <= sides; i++) { float a = i / (float)sides * Mathf.PI * 2; ring[i] = Add(bottom + new Vector3(Mathf.Cos(a) * r0, 0, Mathf.Sin(a) * r0), new Vector2(.5f, .5f)); }
                    for (int i = 0; i < sides; i++) Tri(c, ring[i + 1], ring[i]);
                }
            }

            /// A half sphere (dome) sitting on `bottom`, one texel.
            public void Dome(Vector3 bottom, float r, int sides, int rings, float squash = 1f)
            {
                var grid = new int[rings + 1][];
                for (int j = 0; j <= rings; j++)
                {
                    float p = j / (float)rings * Mathf.PI / 2; grid[j] = new int[sides + 1];
                    for (int i = 0; i <= sides; i++)
                    {
                        float a = i / (float)sides * Mathf.PI * 2;
                        grid[j][i] = Add(bottom + new Vector3(Mathf.Cos(a) * Mathf.Cos(p) * r, Mathf.Sin(p) * r * squash, Mathf.Sin(a) * Mathf.Cos(p) * r), new Vector2(.5f, .5f));
                    }
                }
                for (int j = 0; j < rings; j++) for (int i = 0; i < sides; i++) Quad(grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]);
            }

            /// A lumpy sphere (subdivided icosahedron, shared vertices so it shades smooth) scaled by `radii`.
            /// uv.y runs 0..1 from the bottom of the lump to its top (for height ramps), uv.x is `u`.
            public void Lump(Vector3 centre, Vector3 radii, int subdiv, float seed, float u, float lumpiness = .2f)
            {
                var (dirs, tris) = Ico(subdiv);
                int baseIndex = V.Count;
                var inv = new Vector3(1 / radii.x, 1 / radii.y, 1 / radii.z);
                for (int i = 0; i < dirs.Length; i++)
                {
                    var d = dirs[i];
                    float n = 1 + (Noise(d.x * 1.4f + seed, d.y * 1.2f + d.z * 1.0f + seed * .7f) - .5f) * 2 * lumpiness;
                    Add(centre + Vector3.Scale(d, radii) * n, new Vector2(u, d.y * .5f + .5f));
                    N.Add(Vector3.Scale(d, inv).normalized);       // the ellipsoid's own normal: smooth shading at any subdivision
                }
                for (int i = 0; i < tris.Length; i += 3) { T.Add(baseIndex + tris[i]); T.Add(baseIndex + tris[i + 1]); T.Add(baseIndex + tris[i + 2]); }
            }

            /// A low-poly rock: an unshared (flat-shaded) icosphere with planar UVs from world position.
            public void FlatLump(Vector3 centre, Vector3 radii, float seed, float lumpiness)
            {
                var (dirs, tris) = Ico(1);
                var pos = new Vector3[dirs.Length];
                for (int i = 0; i < dirs.Length; i++)
                {
                    var d = dirs[i];
                    float n = 1 + (Noise(d.x * 2.3f + seed, d.y * 2.0f + d.z * 1.7f + seed * .7f) - .5f) * 2 * lumpiness;
                    pos[i] = centre + Vector3.Scale(d, radii) * n;
                }
                Vector2 Uv(Vector3 p) => new Vector2((p.x + p.z * .3f) / 9f, (p.y + p.z * .4f) / 9f);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = Add(pos[tris[i]], Uv(pos[tris[i]])), b = Add(pos[tris[i + 1]], Uv(pos[tris[i + 1]])), c = Add(pos[tris[i + 2]], Uv(pos[tris[i + 2]]));
                    T.Add(a); T.Add(b); T.Add(c);
                }
            }

            public Mesh ToMesh(string name, bool normals = true)
            {
                var m = new Mesh { name = name, indexFormat = V.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(V); m.SetUVs(0, UV); if (Colors) m.SetColors(Col);
                if (T2.Count > 0) { m.subMeshCount = 2; m.SetTriangles(T, 0); m.SetTriangles(T2, 1); } else m.SetTriangles(T, 0);
                if (N.Count == V.Count && N.Count > 0) m.SetNormals(N);
                else if (normals) m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static readonly Dictionary<int, (Vector3[], int[])> icoCache = new Dictionary<int, (Vector3[], int[])>();
        /// Unit icosphere: directions and triangle indices, wound to face outward in Unity.
        static (Vector3[], int[]) Ico(int subdiv)
        {
            if (icoCache.TryGetValue(subdiv, out var hit)) return hit;
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var v = new List<Vector3> {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1) };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdiv; s++)
            {
                var cache = new Dictionary<long, int>(); var nf = new List<int>();
                int Mid(int a, int b)
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (cache.TryGetValue(key, out int m)) return m;
                    v.Add(((v[a] + v[b]) * .5f).normalized); return cache[key] = v.Count - 1;
                }
                for (int i = 0; i < f.Count; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nf.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                f = nf;
            }
            for (int i = 0; i < f.Count; i += 3)   // make every face wind outward for Unity
            {
                var a = v[f[i]]; var b = v[f[i + 1]]; var c = v[f[i + 2]];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0) { int tmp = f[i + 1]; f[i + 1] = f[i + 2]; f[i + 2] = tmp; }
            }
            return icoCache[subdiv] = (v.ToArray(), f.ToArray());
        }

        // ---------------------------------------------------------------- noise and textures

        /// Cellular (Worley) noise: distance to the nearest and second-nearest feature point on a unit grid.
        internal static void Worley(float x, float y, int seed, out float f1, out float f2)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); f1 = f2 = 9;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = x0 + i, cy = y0 + j;
                    float px = cx + .15f + .7f * Hash(cx, cy, seed), py = cy + .15f + .7f * Hash(cx, cy, seed + 101);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
        }

        /// Worley noise that tiles with an integer period (for textures that wrap).
        internal static void WorleyTile(float u, float v, int period, int seed, out float f1, out float f2)
        {
            float x = Mathf.Repeat(u, 1) * period, y = Mathf.Repeat(v, 1) * period; int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); f1 = f2 = 9;
            for (int j = -1; j <= 1; j++)
                for (int i = -1; i <= 1; i++)
                {
                    int cx = x0 + i, cy = y0 + j, wx = Wrap(cx, period), wy = Wrap(cy, period);
                    float px = cx + .15f + .7f * Hash(wx, wy, seed), py = cy + .15f + .7f * Hash(wx, wy, seed + 101);
                    float d = Mathf.Sqrt((px - x) * (px - x) + (py - y) * (py - y));
                    if (d < f1) { f2 = f1; f1 = d; } else if (d < f2) f2 = d;
                }
        }

        internal static float Hash01(int x, int y, int seed) => Hash(x, y, seed);

        /// A colour ramp texture: v runs 0..1 along `stops`, u adds a little per-column variation. Clamped in v, repeating in u.
        internal static Texture2D Ramp(string name, (float at, Color c)[] stops, int w = 8, int h = 128, float variation = .03f)
        {
            var t = Tex(w, h, (u, v) =>
            {
                Color c = stops[0].c;
                for (int i = 1; i < stops.Length; i++) c = Color.Lerp(c, stops[i].c, Sm(stops[i - 1].at, stops[i].at, v) * (v >= stops[i - 1].at ? 1 : 0));
                float k = 1 + (Hash(Mathf.FloorToInt(u * w), 0, 3) - .5f) * 2 * variation;
                return new Color(c.r * k, c.g * k, c.b * k, 1);
            }, true, name);
            t.wrapModeV = TextureWrapMode.Clamp; t.wrapModeU = TextureWrapMode.Repeat;
            return t;
        }

        /// Two textures from one pass (albedo and emission), for facades whose lit windows glow.
        internal static (Texture2D albedo, Texture2D emission) TexPair(int w, int h, System.Func<float, float, (Color a, Color e)> f, string name)
        {
            var a = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var e = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = name + " glow", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var pa = new Color32[w * h]; var pe = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) { var r = f((x + .5f) / w, (y + .5f) / h); pa[y * w + x] = r.a; pe[y * w + x] = r.e; }
            a.SetPixels32(pa); e.SetPixels32(pe); a.Apply(true, true); e.Apply(true, true);
            return (a, e);
        }

        /// A matte lit material for clouds: no specular, no reflections.
        internal static Material Matte(Color color, Texture tex = null)
        {
            var m = Lit(color, 0f, tex, Vector2.one);
            m.SetFloat("_SpecularHighlights", 0); m.SetFloat("_EnvironmentReflections", 0);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return m;
        }

        /// A sky dome that follows the camera and wears a baked texture: u runs once round (angle from +x toward +z),
        /// v runs from elevation `elMin` to 90 degrees. `paint(az radians, el degrees)` returns the colour.
        static readonly Dictionary<string, Texture2D> domeCache = new Dictionary<string, Texture2D>();

        /// `key` caches the baked texture for the life of the app: a match restarts without paying for the sky again.
        internal static GameObject TexDome(Transform parent, string key, System.Func<float, float, Color> paint, float elMin = -12f, int texW = 1024, int texH = 256, float radius = 3000f)
        {
            if (!domeCache.TryGetValue(key, out var tex) || !tex)
            {
                var bake = System.Diagnostics.Stopwatch.StartNew();
                tex = new Texture2D(texW, texH, TextureFormat.RGBA32, true, false) { name = "Sky dome texture " + key, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 1 };
                tex.wrapModeV = TextureWrapMode.Clamp;
                var px = new Color32[texW * texH];
                for (int y = 0; y < texH; y++)
                {
                    float el = Mathf.Lerp(elMin, 90f, (y + .5f) / texH);
                    for (int x = 0; x < texW; x++)
                    {
                        var c = paint((x + .5f) / texW * Mathf.PI * 2, el);
                        float dither = (Hash(x, y, 77) - .5f) * (1.6f / 255f);          // breaks 8-bit banding in the long gradients
                        px[y * texW + x] = new Color(Mathf.Clamp01(c.r + dither), Mathf.Clamp01(c.g + dither), Mathf.Clamp01(c.b + dither), 1);
                    }
                }
                tex.SetPixels32(px); tex.Apply(true, true);
                domeCache[key] = tex;
                Log("sky dome '" + key + "' baked in " + bake.ElapsedMilliseconds + " ms (first match only; cached after)", 0);
            }
            float[] els = { elMin, -6, -3, -1, 0, 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 18, 22, 26, 30, 35, 40, 45, 52, 60, 70, 80, 90 };
            const int lon = 96;
            var mb = new MB();
            for (int j = 0; j < els.Length; j++)
            {
                float phi = els[j] * Mathf.Deg2Rad;
                for (int i = 0; i <= lon; i++)
                {
                    float th = i / (float)lon * Mathf.PI * 2;
                    mb.Add(new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th)) * radius, new Vector2(i / (float)lon, (els[j] - elMin) / (90f - elMin)));
                }
            }
            for (int j = 0; j < els.Length - 1; j++)
                for (int i = 0; i < lon; i++)
                {
                    int a = j * (lon + 1) + i, b = a + lon + 1;
                    mb.T.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = mb.ToMesh("Sky dome", false); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            var mat = new Material(Alpha) { name = "Sky dome", renderQueue = 1000 };
            mat.mainTexture = tex; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            var go = MeshObject(parent, "Sky dome", mesh, mat);
            go.AddComponent<TennisVenueFx.FollowCamera>();
            return go;
        }
    }
}
