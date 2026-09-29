using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Builds the non-resort venues around the arena's own court kit. The arena is instantiated as
    /// usual and everything resort (island, sea, stands, crowd, gardens, clubhouse) is switched off;
    /// what stays is the court, lines, net, posts, run-off, umpire chair, lanterns, benches and
    /// planters, in place, so contact, cameras and rules are untouched. Everything here is built
    /// from primitives, generated meshes and generated textures: no new imported assets.
    public static class TennisVenueBuilder
    {
        /// Arena materials that make up the court kit (the Blender names travel in materials.json).
        static readonly string[] CourtKit =
        {
            "TropicalV3_002",   // court
            "TropicalV3_003",   // lines
            "TropicalV3_008",   // net posts
            "TropicalV3_011",   // run-off apron
            "TropicalV3_012",   // net
            "TropicalV3_017",   // planters
            "TropicalV3_020",   // benches
            "TropicalV3_023",   // umpire chair
            "TropicalV3_033",   // lanterns
        };

        public static void Build(TennisVenueKind kind, GameObject arena)
        {
            foreach (var r in arena.GetComponentsInChildren<Renderer>(true))
            {
                string m = r.sharedMaterial ? r.sharedMaterial.name : "";
                bool keep = false;
                foreach (var k in CourtKit) if (m.StartsWith(k)) { keep = true; break; }
                if (!keep) r.enabled = false;
            }
            var root = new GameObject(kind + " venue").transform;
            switch (kind)
            {
                case TennisVenueKind.Skyscraper: Skyscraper.Build(root); break;
                case TennisVenueKind.Volcano: Volcano.Build(root); break;
            }
        }

        /// Sun, fill, ambient, fog and sky for the venue, after the resort's LightScene ran.
        public static void Light(TennisVenueKind kind, Light sun, Camera camera)
        {
            switch (kind)
            {
                case TennisVenueKind.Skyscraper: Skyscraper.Light(sun, camera); break;
                case TennisVenueKind.Volcano: Volcano.Light(sun, camera); break;
            }
        }

        // =====================================================================================
        // shared helpers
        // =====================================================================================

        static Shader S(string name) => Shader.Find(name);
        static Shader Alpha => Resources.Load<Shader>("Tennis/Shaders/TennisFxAlpha") ?? S("Sprites/Default");
        static Shader Additive => Resources.Load<Shader>("Tennis/Shaders/TennisFxAdditive") ?? S("Sprites/Default");

        internal static Material Lit(Color color, float smooth = .15f, Texture tex = null, Vector2? tiling = null, float metallic = 0)
        {
            var m = new Material(S("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            if (tex) { m.SetTexture("_BaseMap", tex); if (tiling.HasValue) m.SetTextureScale("_BaseMap", tiling.Value); }
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metallic);
            return m;
        }

        /// A lit material whose cracks glow: `emission` is a mask multiplied by `glow` (HDR).
        internal static Material Glowing(Color color, float smooth, Texture tex, Texture emission, Color glow, Vector2 tiling)
        {
            var m = Lit(color, smooth, tex, tiling);
            m.SetTexture("_EmissionMap", emission); m.SetTextureScale("_EmissionMap", tiling);
            m.SetColor("_EmissionColor", glow); m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        /// Adds a relief (normal) map so low sunlight rakes across the surface and shows its detail.
        internal static Material WithRelief(Material m, Texture normal, float scale, Vector2 tiling)
        {
            m.SetTexture("_BumpMap", normal); m.SetTextureScale("_BumpMap", tiling); m.SetFloat("_BumpScale", scale); m.EnableKeyword("_NORMALMAP");
            return m;
        }

        internal static Material Sprite(Shader shader, Texture tex, Color tint, int queue, Vector2? tiling = null, float intensity = -1)
        {
            // Scenery draws before the HUD (UI is queue 3000): clouds, smoke and glows must never cover it.
            var m = new Material(shader) { mainTexture = tex, renderQueue = Mathf.Min(queue, 3000) - 400 };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (intensity >= 0 && m.HasProperty("_Intensity")) m.SetFloat("_Intensity", intensity);
            if (tiling.HasValue) { m.mainTextureScale = tiling.Value; if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", tiling.Value); }
            return m;
        }

        internal static Texture2D Tex(int w, int h, System.Func<float, float, Color> f, bool repeat = true, string name = "Venue texture", bool linear = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true, linear) { name = name, wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f((x + .5f) / w, (y + .5f) / h);
            t.SetPixels32(px);
            if (System.Environment.GetEnvironmentVariable("HERO_VENUE_DEBUG") == "1") { t.Apply(false, false); System.IO.File.WriteAllBytes("/tmp/venue_tex_" + name.Replace(' ', '_') + ".png", t.EncodeToPNG()); }
            t.Apply(true, true);
            return t;
        }

        /// GLSL-style smoothstep(edge0, edge1, x) (Mathf.SmoothStep is a different function).
        internal static float Sm(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
        internal static float Noise(float x, float y) => Mathf.PerlinNoise(x + 37.1f, y + 91.7f);
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
        static int Wrap(int v, int p) => ((v % p) + p) % p;
        /// Smooth value noise that tiles with the given (integer) period, so generated textures wrap seamlessly.
        internal static float TileNoise(float u, float v, float period, float seed = 0)
        {
            int p = Mathf.Max(1, Mathf.RoundToInt(period)), sd = Mathf.RoundToInt(seed) * 7919;
            float x = Mathf.Repeat(u, 1) * p, y = Mathf.Repeat(v, 1) * p;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(Wrap(x0, p), Wrap(y0, p), sd), b = Hash(Wrap(x0 + 1, p), Wrap(y0, p), sd);
            float c = Hash(Wrap(x0, p), Wrap(y0 + 1, p), sd), d = Hash(Wrap(x0 + 1, p), Wrap(y0 + 1, p), sd);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        /// Tiling value noise with separate periods across (pu) and along (pv) the texture: stretched streaks.
        internal static float TileNoiseXY(float u, float v, float pu, float pv, float seed = 0)
        {
            int px = Mathf.Max(1, Mathf.RoundToInt(pu)), py = Mathf.Max(1, Mathf.RoundToInt(pv)), sd = Mathf.RoundToInt(seed) * 7919;
            float x = Mathf.Repeat(u, 1) * px, y = Mathf.Repeat(v, 1) * py;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(Wrap(x0, px), Wrap(y0, py), sd), b = Hash(Wrap(x0 + 1, px), Wrap(y0, py), sd);
            float c = Hash(Wrap(x0, px), Wrap(y0 + 1, py), sd), d = Hash(Wrap(x0 + 1, px), Wrap(y0 + 1, py), sd);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }
        internal static float Fbm(float u, float v, float period, int octaves, float seed = 0)
        {
            float sum = 0, amp = .5f, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += amp * TileNoise(u, v, period, seed + i * 17.3f); norm += amp; amp *= .5f; period *= 2; }
            return sum / norm;
        }

        internal static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool shadows = true)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = shadows;
            return go;
        }

        internal static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat, bool shadows = false)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off; r.receiveShadows = shadows;
            return go;
        }

        static Texture2D soft, cloud;
        /// A round soft blob (alpha), for glows, puffs and smoke.
        internal static Texture2D Soft => soft ? soft : soft = Tex(128, 128, (u, v) =>
        {
            float dx = u * 2 - 1, dy = v * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
            return new Color(1, 1, 1, a);
        }, false, "Soft blob");

        /// A lumpy cloud puff: soft blob edge eaten by noise.
        internal static Texture2D Puff => cloud ? cloud : cloud = Tex(128, 128, (u, v) =>
        {
            float dx = u * 2 - 1, dy = v * 2 - 1, r = Mathf.Sqrt(dx * dx * .9f + dy * dy * 1.5f);
            float n = Fbm(u, v, 3, 4, 5);
            float a = Mathf.Clamp01((1 - r) * 1.7f - (1 - n) * .55f); a = a * a * (3 - 2 * a);
            float shade = Mathf.Lerp(.72f, 1f, v);   // the tops catch the sun
            return new Color(shade, shade, shade, a);
        }, false, "Cloud puff");

        /// A large soft slab of cloud sheet, tileable.
        internal static Texture2D Sheet(float seed) => Tex(256, 256, (u, v) =>
        {
            float n = Fbm(u, v, 4, 5, seed);
            float a = Sm(.36f, .62f, n);
            float shade = Mathf.Lerp(.78f, 1f, Sm(.35f, .65f, Fbm(u, v, 6, 3, seed + 9)));
            return new Color(shade, shade, shade, a);
        }, true, "Cloud sheet");

        /// A sky dome that follows the camera, coloured by a gradient (up, sun) baked per vertex.
        internal static GameObject Dome(Transform parent, System.Func<Vector3, Color> paint, float radius = 3000)
        {
            const int lon = 48, lat = 24;
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float phi = Mathf.PI * (y / (float)lat - .5f) ;      // -90 .. +90
                for (int x = 0; x <= lon; x++)
                {
                    float th = 2 * Mathf.PI * x / lon;
                    var d = new Vector3(Mathf.Cos(phi) * Mathf.Cos(th), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(th));
                    verts.Add(d * radius); cols.Add(paint(d));
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = y * (lon + 1) + x, b = a + lon + 1;
                    tris.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            var mesh = new Mesh { name = "Sky dome", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts); mesh.SetColors(cols); mesh.SetTriangles(tris, 0); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2.2f);
            var mat = new Material(Alpha) { name = "Sky dome", renderQueue = 1000 };
            mat.mainTexture = Texture2D.whiteTexture; if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", Texture2D.whiteTexture);
            var go = MeshObject(parent, "Sky dome", mesh, mat);
            go.AddComponent<TennisVenueFx.FollowCamera>();
            return go;
        }

        internal static Color Grad(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));

        /// A camera-facing sprite quad.
        internal static GameObject Billboard(Transform parent, string name, Material mat, Vector3 pos, Vector2 size, bool yawOnly = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = new Vector3(size.x, size.y, 1);
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            var b = go.AddComponent<TennisVenueFx.FaceCamera>(); b.YawOnly = yawOnly;
            return go;
        }

        internal static float Rand(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// A rectangular slab with a chamfer-free clean edge: top at y=0.
        internal static void Slab(Transform parent, string name, Vector3 centre, Vector3 size, Material mat)
            => Prim(PrimitiveType.Cube, parent, name, centre, size, mat);

        // =====================================================================================
        // SKYSCRAPER: the deck on top of a tower a hundred times taller than everything around it
        // =====================================================================================
        static class Skyscraper
        {
            public static readonly Vector3 SunDir = new Vector3(-.80f, .30f, .52f).normalized;

            public static void Build(Transform root)
            {
                float hx = TennisVenue.DeckHalfX, hz = TennisVenue.DeckHalfZ;
                var stone = Lit(new Color(.60f, .62f, .64f), .28f);
                var coping = Lit(new Color(.86f, .87f, .89f), .35f);
                // deck slab and its clean bare edge: no glass, no rail, no net
                Slab(root, "Rooftop deck", new Vector3(0, -.5f - .005f, 0), new Vector3(hx * 2, 1f, hz * 2), stone);
                float e = .34f;
                Slab(root, "Coping N", new Vector3(0, -.06f, hz - e / 2), new Vector3(hx * 2, .12f, e), coping);
                Slab(root, "Coping S", new Vector3(0, -.06f, -hz + e / 2), new Vector3(hx * 2, .12f, e), coping);
                Slab(root, "Coping E", new Vector3(hx - e / 2, -.06f, 0), new Vector3(e, .12f, hz * 2 - e * 2), coping);
                Slab(root, "Coping W", new Vector3(-hx + e / 2, -.06f, 0), new Vector3(e, .12f, hz * 2 - e * 2), coping);
                Tower(root, hx, hz);
                Mast(root);
                Clouds(root);
                City(root);
                Wisps(root);
                Sky(root);
            }

            static void Tower(Transform root, float hx, float hz)
            {
                var glass = Tex(128, 256, (u, v) =>
                {
                    // curtain wall: dark glass panes between pale mullions, a spandrel band at each floor
                    float col = Mathf.Repeat(u * 4, 1), row = Mathf.Repeat(v * 8, 1);
                    bool mullion = col < .07 || col > .93, spandrel = row < .16;
                    int cx = Mathf.FloorToInt(u * 4), cy = Mathf.FloorToInt(v * 8);
                    float lit = Hash(cx, cy, 5) > .90f ? 1 : 0;
                    var pane = Color.Lerp(new Color(.20f, .30f, .44f), new Color(.42f, .58f, .74f), Hash(cx, cy, 9) * .7f + (1 - row) * .3f);
                    var c = mullion ? new Color(.78f, .82f, .86f) : spandrel ? new Color(.52f, .58f, .64f) : pane;
                    return Color.Lerp(c, new Color(1f, .82f, .5f), lit * (mullion || spandrel ? 0 : .6f));
                }, true, "Tower glass");
                // (width, depth, from, to) top to bottom; the shaft gets a little wider at each setback
                float[][] tiers =
                {
                    new[] { 1.0f, 1.0f, -1.0f, -260f }, new[] { 1.25f, 1.2f, -260f, -700f },
                    new[] { 1.6f, 1.45f, -700f, -1300f }, new[] { 2.1f, 1.8f, -1300f, -2200f },
                };
                // a slim overhang under the deck, then the shaft
                Slab(root, "Deck underside", new Vector3(0, -1.5f, 0), new Vector3(hx * 2 - .6f, 1f, hz * 2 - .6f), Lit(new Color(.55f, .58f, .62f), .4f));
                foreach (var t in tiers)
                {
                    float w = hx * 2 * t[0] * .82f, d = hz * 2 * t[1] * .70f, h = t[2] - t[3];
                    var mat = Lit(Color.white, .65f, glass, new Vector2(w / 14f, h / 30f), .15f);
                    Prim(PrimitiveType.Cube, root, "Tower shaft", new Vector3(0, (t[2] + t[3]) / 2 - 1, 0), new Vector3(w, h, d), mat);
                }
            }

            static void Mast(Transform root)
            {
                var steel = Lit(new Color(.82f, .84f, .86f), .5f, null, null, .6f);
                var m = new GameObject("Antenna mast").transform; m.SetParent(root, false); m.localPosition = new Vector3(8.2f, 0, 19.4f);
                Prim(PrimitiveType.Cube, m, "Plant room", new Vector3(0, .8f, 0), new Vector3(2.4f, 1.6f, 2.0f), Lit(new Color(.72f, .74f, .76f), .3f));
                for (int i = 0; i < 4; i++)
                {
                    float s = i < 2 ? -1 : 1, t = i % 2 == 0 ? -1 : 1;
                    var leg = Prim(PrimitiveType.Cylinder, m, "Leg", new Vector3(s * .45f, 8.5f, t * .45f), new Vector3(.09f, 7f, .09f), steel);
                    leg.transform.localRotation = Quaternion.Euler(-t * 1.6f, 0, s * 1.6f);
                }
                Prim(PrimitiveType.Cylinder, m, "Pole", new Vector3(0, 22f, 0), new Vector3(.22f, 8f, .22f), steel);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cube, m, "Panel", new Vector3(0, 9.5f + i * 3.4f, .35f), new Vector3(.5f, 1.5f, .18f), Lit(new Color(.92f, .93f, .95f), .4f)).transform.localRotation = Quaternion.Euler(0, i * 120, 0);
                var beacon = Prim(PrimitiveType.Sphere, m, "Beacon", new Vector3(0, 30.4f, 0), Vector3.one * .6f, Glowing(new Color(.5f, .05f, .05f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(3.2f, .2f, .15f), Vector2.one), false);
                beacon.AddComponent<TennisVenueFx.Blink>();
            }

            static void Clouds(Transform root)
            {
                var alpha = Alpha; var rng = new System.Random(77);
                var group = new GameObject("Cloud sea").transform; group.SetParent(root, false);
                // three stacked sheets make the sea, the sun-side warm and the shaded side lavender
                float[] ys = { -388, -376, -362, -348 }; Color[] tints = { new Color(.50f, .48f, .72f, .95f), new Color(.74f, .62f, .78f, .9f), new Color(.98f, .76f, .70f, .85f), new Color(1f, .86f, .74f, .8f) };
                for (int i = 0; i < ys.Length; i++)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Cloud sheet " + i; Object.Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(group, false); q.transform.localPosition = new Vector3(0, ys[i], 0);
                    q.transform.localRotation = Quaternion.Euler(90, i * 37, 0); q.transform.localScale = new Vector3(9000, 9000, 1);
                    var r = q.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    r.sharedMaterial = Sprite(alpha, Sheet(10 + i * 11), tints[i], 3000 + i, new Vector2(11 + i * 2, 11 + i * 2));
                }
                // cumulus heaps standing on the sea
                for (int i = 0; i < 150; i++)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = Mathf.Sqrt(Rand(rng, 0, 1)) * 2200 + 140;
                    float size = Rand(rng, 220, 640), y = -352 + size * .18f + Rand(rng, -24, 20);
                    float warm = Rand(rng, 0, 1);
                    var puff = Billboard(group, "Cumulus", Sprite(alpha, Puff, Color.Lerp(new Color(1f, .90f, .84f), new Color(1f, .78f, .70f), warm), 3010 + (i % 8)),
                        new Vector3(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r), new Vector2(size * 1.6f, size));
                }
            }

            /// The towers of the city, a long way down, poking through the cloud.
            static void City(Transform root)
            {
                var rng = new System.Random(2024);
                var win = Tex(32, 64, (u, v) =>
                {
                    bool lit = Noise(Mathf.Floor(u * 4) * 5.7f, Mathf.Floor(v * 32) * 3.3f) > .62f;
                    float band = Mathf.Repeat(v * 32, 1) > .55f ? 1 : .55f;
                    return lit ? new Color(1f, .82f, .52f) * band : new Color(.42f, .48f, .62f) * band;
                }, true, "City windows");
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                void Box(Vector3 c, float w, float d, float bottom, float top, float uTile)
                {
                    float x0 = c.x - w / 2, x1 = c.x + w / 2, z0 = c.z - d / 2, z1 = c.z + d / 2, h = top - bottom;
                    Vector3[][] faces =
                    {
                        new[] { new Vector3(x0, bottom, z0), new Vector3(x1, bottom, z0), new Vector3(x1, top, z0), new Vector3(x0, top, z0) },
                        new[] { new Vector3(x1, bottom, z0), new Vector3(x1, bottom, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0) },
                        new[] { new Vector3(x1, bottom, z1), new Vector3(x0, bottom, z1), new Vector3(x0, top, z1), new Vector3(x1, top, z1) },
                        new[] { new Vector3(x0, bottom, z1), new Vector3(x0, bottom, z0), new Vector3(x0, top, z0), new Vector3(x0, top, z1) },
                    };
                    foreach (var f in faces)
                    {
                        int k = verts.Count; verts.AddRange(f);
                        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(uTile, 0)); uvs.Add(new Vector2(uTile, h / 14f)); uvs.Add(new Vector2(0, h / 14f));
                        tris.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
                    }
                    int t = verts.Count;   // roof
                    verts.Add(new Vector3(x0, top, z0)); verts.Add(new Vector3(x1, top, z0)); verts.Add(new Vector3(x1, top, z1)); verts.Add(new Vector3(x0, top, z1));
                    uvs.Add(Vector2.zero); uvs.Add(Vector2.zero); uvs.Add(Vector2.zero); uvs.Add(Vector2.zero);
                    tris.AddRange(new[] { t, t + 2, t + 1, t, t + 3, t + 2 });
                }
                for (int i = 0; i < 380; i++)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = Mathf.Sqrt(Rand(rng, 0, 1)) * 2300 + 330;
                    var c = new Vector3(Mathf.Cos(ang) * r, 0, Mathf.Sin(ang) * r);
                    float w = Rand(rng, 26, 70), d = Rand(rng, 26, 70), top = -350 + Rand(rng, 20, 130) * Mathf.Lerp(1.4f, .7f, r / 2600f);
                    Box(c, w, d, -700, top, Mathf.Max(1, w / 10f));
                    if (rng.NextDouble() < .55) Box(c, w * .6f, d * .6f, top, top + Rand(rng, 12, 40), Mathf.Max(1, w / 16f));
                    if (rng.NextDouble() < .35) Box(c, 2.5f, 2.5f, top + 12, top + Rand(rng, 40, 90), 1);
                }
                var mesh = new Mesh { name = "City towers", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var mat = Lit(new Color(.86f, .84f, .92f), .3f, win, Vector2.one);
                MeshObject(root, "City below the clouds", mesh, mat, true);
            }

            /// Wisps of cloud drifting past the deck at its own height, for the drone shot to fly through.
            static void Wisps(Transform root)
            {
                var alpha = Alpha; var rng = new System.Random(5);
                var group = new GameObject("Wisps").transform; group.SetParent(root, false);
                for (int i = 0; i < 26; i++)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = Rand(rng, 55, 420), size = Rand(rng, 40, 130);
                    var w = Billboard(group, "Wisp", Sprite(alpha, Puff, new Color(1f, .88f, .8f, .34f), 3020 + (i % 6)),
                        new Vector3(Mathf.Cos(ang) * r, Rand(rng, -180, -14), Mathf.Sin(ang) * r), new Vector2(size * 2, size));
                    var d = w.AddComponent<TennisVenueFx.Drift>(); d.Velocity = new Vector3(Rand(rng, -2.5f, -1f), 0, Rand(rng, -1, 1)); d.Wrap = 460;
                }
            }

            static void Sky(Transform root)
            {
                Vector3 sun = SunDir;
                Dome(root, d =>
                {
                    float up = Mathf.Clamp01(d.y * 1.6f), below = Mathf.Clamp01(-d.y * 3);
                    var horizon = new Color(1f, .78f, .58f); var mid = new Color(.78f, .68f, .84f); var zenith = new Color(.28f, .46f, .86f);
                    var c = up < .35f ? Grad(horizon, mid, up / .35f) : Grad(mid, zenith, (up - .35f) / .65f);
                    c = Grad(c, new Color(.95f, .78f, .74f), below);
                    float s = Mathf.Max(0, Vector3.Dot(d, sun));
                    c += new Color(1f, .70f, .34f) * (Mathf.Pow(s, 10) * .30f + Mathf.Pow(s, 120) * .35f);
                    c.a = 1; return c;
                });
                var glow = Sprite(Additive, Soft, new Color(1f, .78f, .5f, .55f), 2999, null, .6f);
                var g = Billboard(root, "Sun glow", glow, SunDir * 2800, new Vector2(1100, 1100), false);
                var disc = Sprite(Additive, Soft, new Color(1f, .93f, .78f, .9f), 3000, null, 1f);
                Billboard(root, "Sun", disc, SunDir * 2790, new Vector2(260, 260), false);
                g.AddComponent<TennisVenueFx.FollowCamera>(); root.Find("Sun").gameObject.AddComponent<TennisVenueFx.FollowCamera>();
                g.GetComponent<TennisVenueFx.FollowCamera>().Offset = SunDir * 2800; root.Find("Sun").GetComponent<TennisVenueFx.FollowCamera>().Offset = SunDir * 2790;
            }

            public static void Light(Light sun, Camera camera)
            {
                sun.transform.rotation = Quaternion.LookRotation(-SunDir);
                sun.color = new Color(1f, .74f, .48f); sun.intensity = 2.6f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.50f, .58f, .84f);
                RenderSettings.ambientEquatorColor = new Color(.86f, .68f, .62f);
                RenderSettings.ambientGroundColor = new Color(.66f, .54f, .58f);   // the cloud sea lights the underside
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.80f, .72f, .84f);
                RenderSettings.fogStartDistance = 900; RenderSettings.fogEndDistance = 9000;
                RenderSettings.skybox = null;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.86f, .72f, .74f);
            }
        }

        // =====================================================================================
        // VOLCANO: a slab of obsidian hovering over the crater's lava lake
        // =====================================================================================
        static class Volcano
        {
            public static readonly Vector3 SunDir = new Vector3(.66f, .10f, .70f).normalized;
            const float LakeY = -46f;
            public static readonly Vector3 Lake = new Vector3(0, LakeY, 0);   // the court hovers dead centre over the lava

            static Texture2D rock, rock2, rockNormal, rivers, gravel, grassTex, crack, lavaBase, lavaGlow;

            static void Textures()
            {
                if (rock) return;
                rock = Tex(256, 256, (u, v) =>
                {
                    float n = Fbm(u, v, 5, 5, 3), g = Mathf.Lerp(.05f, .20f, n);
                    return new Color(g, g * .95f, g * 1.05f, 1);
                }, true, "Basalt");
                rock2 = Tex(512, 512, (u, v) =>
                {
                    // weathered volcanic rock: dark basalt strata, ash-dusted ledges, rust staining, rare sulphur
                    float n = Fbm(u, v, 6, 5, 21), band = TileNoiseXY(u, v, 2, 26, 8), b2 = TileNoiseXY(u, v, 3, 9, 15);
                    float ash = Sm(.52f, .68f, Fbm(u, v, 4, 4, 44)), rust = Sm(.56f, .70f, Fbm(u, v, 5, 3, 61)), sulfur = Sm(.70f, .78f, Fbm(u, v, 7, 3, 3));
                    var c = Color.Lerp(new Color(.07f, .06f, .06f), new Color(.25f, .21f, .19f), Mathf.Clamp01(n * .9f + (band - .5f) * .5f));
                    c = Color.Lerp(c, new Color(.40f, .37f, .34f), ash * .55f * Sm(.35f, .6f, b2));
                    c = Color.Lerp(c, new Color(.34f, .16f, .08f), rust * .38f);
                    c = Color.Lerp(c, new Color(.50f, .40f, .14f), sulfur * .32f);
                    float seam = .72f + .5f * Sm(.42f, .58f, b2);   // darker joints between strata
                    return new Color(c.r * seam, c.g * seam, c.b * seam, 1);
                }, true, "Volcanic rock");
                rockNormal = Tex(256, 256, (u, v) =>
                {
                    float H(float x, float y) => Fbm(x, y, 12, 4, 7) * .65f + TileNoiseXY(x, y, 4, 30, 2) * .35f;
                    const float e = 1f / 256f, k = 7f;
                    float dx = H(u + e, v) - H(u - e, v), dy = H(u, v + e) - H(u, v - e);
                    var nrm = new Vector3(-dx * k, -dy * k, 1).normalized;
                    return new Color(nrm.x * .5f + .5f, nrm.y * .5f + .5f, nrm.z * .5f + .5f, 1);
                }, true, "Rock relief", true);
                gravel = Tex(256, 256, (u, v) =>
                {
                    float n = Fbm(u, v, 8, 4, 61), speck = Hash(Mathf.FloorToInt(u * 128), Mathf.FloorToInt(v * 128), 3) * .12f;
                    float g = .20f + n * .22f + speck;
                    return new Color(g * 1.10f, g * .98f, g * .86f, 1);
                }, true, "Volcanic gravel");
                grassTex = Tex(256, 256, (u, v) =>
                {
                    float n = Fbm(u, v, 6, 4, 33), patch = Fbm(u, v, 3, 3, 12), speck = Hash(Mathf.FloorToInt(u * 128), Mathf.FloorToInt(v * 128), 9) * .10f;
                    var green = Color.Lerp(new Color(.13f, .28f, .07f), new Color(.24f, .40f, .10f), n);
                    green = Color.Lerp(green, new Color(.36f, .34f, .14f), Sm(.55f, .72f, patch) * .55f);   // dry patches
                    return new Color(green.r + speck * .4f, green.g + speck * .7f, green.b + speck * .2f, 1);
                }, true, "Grass");
                rivers = Tex(512, 512, (u, v) =>
                {
                    // lava streams running down the flank: long thin veins, some fed, some dry
                    float wob = (Fbm(u, v, 5, 3, 51) - .5f) * .09f;
                    float n = TileNoiseXY(u + wob, v, 30, 3, 88), line = 1 - Sm(0f, .07f, Mathf.Abs(n - .5f) * 2);
                    float fed = Sm(.50f, .64f, TileNoiseXY(u, v, 6, 2, 14));
                    float m = line * fed * (.75f + .25f * Fbm(u, v, 12, 2, 4));
                    return new Color(m, m * .40f, m * .07f, 1);
                }, true, "Lava streams");
                crack = Tex(256, 256, (u, v) =>
                {
                    // thin, wandering cracks: a ridged noise line
                    float n = Mathf.Abs(Fbm(u, v, 4, 4, 41) - .5f) * 2;
                    float line = 1 - Sm(.0f, .07f, n);
                    float pulse = Sm(.40f, .58f, Fbm(u, v, 3, 3, 9));
                    float m = line * pulse;
                    return new Color(m, m * .42f, m * .08f, 1);
                }, true, "Basalt cracks");
                lavaBase = Tex(256, 256, (u, v) => LavaAt(u, v, false), true, "Lava crust");
                lavaGlow = Tex(256, 256, (u, v) => LavaAt(u, v, true), true, "Lava glow");
            }

            /// Molten lava, all of it: bright throughout, with hotter veins and slow swirls (base colour or emission mask).
            static Color LavaAt(float u, float v, bool glow)
            {
                float n = Fbm(u, v, 8, 5, 71), swirl = Fbm(u, v, 4, 4, 33), fine = Fbm(u, v, 16, 3, 9);
                float vein = 1 - Sm(0f, .16f, Mathf.Abs(n - .5f) * 2);
                float heat = Mathf.Clamp01(.42f + (swirl - .5f) * 1.1f + vein * .42f + (fine - .5f) * .3f);
                if (glow) return new Color(.35f + heat * .65f, (.35f + heat * .65f) * .40f, (.35f + heat * .65f) * .07f, 1);   // never below a deep red: all molten
                return Color.Lerp(new Color(.62f, .12f, .02f), new Color(1f, .74f, .20f), heat);
            }

            public static void Build(Transform root)
            {
                Textures();
                float hx = TennisVenue.DeckHalfX, hz = TennisVenue.DeckHalfZ;
                var deck = Lit(new Color(.075f, .07f, .078f), .35f);
                Slab(root, "Obsidian deck", new Vector3(0, -.5f - .005f, 0), new Vector3(hx * 2, 1f, hz * 2), deck);
                // a molten seam round the top edge: the slab is lit from within
                var seam = Glowing(new Color(.1f, .03f, .01f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(2.3f, .5f, .07f), Vector2.one);
                float e = .34f, y = -.03f;
                Slab(root, "Seam N", new Vector3(0, y, hz - e / 2), new Vector3(hx * 2, .06f, e), seam);
                Slab(root, "Seam S", new Vector3(0, y, -hz + e / 2), new Vector3(hx * 2, .06f, e), seam);
                Slab(root, "Seam E", new Vector3(hx - e / 2, y, 0), new Vector3(e, .06f, hz * 2 - e * 2), seam);
                Slab(root, "Seam W", new Vector3(-hx + e / 2, y, 0), new Vector3(e, .06f, hz * 2 - e * 2), seam);
                EdgeLights(root, hx, hz);
                Crater(root);
                Sea(root);
                Braziers(root, hx, hz);
                Sky(root);
                Plume(root);
                Embers(root);
            }

            /// A runway of lights round the slab and a glow under it, so the court stands out from the fire.
            static void EdgeLights(Transform root, float hx, float hz)
            {
                var bulb = Glowing(new Color(.3f, .25f, .2f), .4f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(5f, 3.2f, 1.4f), Vector2.one);
                for (float x = -hx + 1.2f; x <= hx - 1.1f; x += 2.6f)
                    foreach (float z in new[] { -hz + .2f, hz - .2f })
                        Prim(PrimitiveType.Cube, root, "Edge light", new Vector3(x, .09f, z), new Vector3(.34f, .1f, .12f), bulb, false);
                for (float z = -hz + 2f; z <= hz - 1.9f; z += 2.6f)
                    foreach (float x in new[] { -hx + .2f, hx - .2f })
                        Prim(PrimitiveType.Cube, root, "Edge light", new Vector3(x, .09f, z), new Vector3(.12f, .1f, .34f), bulb, false);
                var under = new GameObject("Slab glow").AddComponent<Light>(); under.type = LightType.Point; under.transform.SetParent(root, false);
                under.transform.localPosition = new Vector3(0, -9f, 0); under.color = new Color(1f, .6f, .28f); under.range = 130; under.intensity = 14; under.shadows = LightShadows.None;
            }

            /// The floating rock the court sits on: a broad shoulder that overhangs the deck, tapering
            /// through jagged tiers to a point far below, glowing in its cracks, with heavy chunks
            /// fused to it. Reads from above as a big mass of rock, from the side as a real formation.
            static void Underside(Transform root, float hx, float hz)
            {
                const int around = 120, rings = 22; const float depth = 34f, margin = 3.2f;
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                Vector3 Rim(float t01, float k)   // point on the (overhanging) rectangle's perimeter, scaled by k
                {
                    float w = (hx + margin) * 2, d = (hz + margin) * 2, per = 2 * (w + d), sp = t01 * per;
                    float x, z;
                    if (sp < w) { x = -w / 2 + sp; z = -d / 2; }
                    else if (sp < w + d) { x = w / 2; z = -d / 2 + (sp - w); }
                    else if (sp < 2 * w + d) { x = w / 2 - (sp - w - d); z = d / 2; }
                    else { x = -w / 2; z = d / 2 - (sp - 2 * w - d); }
                    return new Vector3(x * k, 0, z * k);
                }
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    // vertical wall for the first metres (the lip), then a swelling shoulder, then a long taper
                    float k = t < .06f ? 1f : Mathf.Lerp(1.04f, .04f, Mathf.Pow((t - .06f) / .94f, .8f)) * (t < .2f ? 1f : 1f);
                    float y = -.9f - (t < .06f ? t / .06f * 2.4f : 2.4f + Mathf.Pow((t - .06f) / .94f, 1.1f) * (depth - 2.4f));
                    for (int a = 0; a <= around; a++)
                    {
                        float u = a / (float)around;
                        var p = Rim(u % 1f, k);
                        float n = (Noise(u * 11, t * 7) - .5f) * 2 * 4.2f * Mathf.Sin(Mathf.Min(1, t * 2.5f) * Mathf.PI * .5f);
                        var outward = new Vector3(p.x, 0, p.z).normalized;
                        p += outward * n;
                        p.y = y + (Noise(u * 16 + 3, t * 9) - .5f) * 2.4f * t + Mathf.Sin(t * 22f + u * 6f) * .5f * t;   // tiers
                        verts.Add(p); uvs.Add(new Vector2(u * 9, t * 3));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int a = 0; a < around; a++)
                    {
                        int i0 = r * (around + 1) + a, i1 = i0 + 1, i2 = i0 + around + 1, i3 = i2 + 1;
                        tris.AddRange(new[] { i0, i1, i2, i1, i3, i2 });   // clockwise seen from outside: faces out (Unity is left-handed)
                    }
                var mesh = new Mesh { name = "Floating rock base" };
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                var mat = Glowing(Color.white, .16f, rock2, crack, new Color(4f, 1.3f, .22f), Vector2.one);
                var go = MeshObject(root, "Floating rock base", mesh, mat, true);
                go.AddComponent<TennisVenueFx.Pulse>().Material = mat;
                // heavy chunks fused into the flanks
                var rng = new System.Random(19);
                for (int i = 0; i < 16; i++)
                {
                    float u = i / 16f + Rand(rng, -.02f, .02f), t = Rand(rng, .08f, .5f);
                    var chunk = MeshObject(root, "Base chunk", RockMesh(rng, 1f), mat, true);
                    float k = t < .06f ? 1f : Mathf.Lerp(1.04f, .04f, Mathf.Pow((t - .06f) / .94f, .8f));
                    var pos = Rim(u % 1f, k); float size = Rand(rng, 4f, 9f);
                    chunk.transform.localPosition = new Vector3(pos.x, -.9f - 2.4f - Mathf.Pow((t - .06f) / .94f, 1.1f) * (depth - 2.4f), pos.z) + new Vector3(pos.x, 0, pos.z).normalized * (size * .2f);
                    chunk.transform.localRotation = Quaternion.Euler(Rand(rng, -25, 25), Rand(rng, 0, 360), Rand(rng, -25, 25));
                    chunk.transform.localScale = new Vector3(size * Rand(rng, .9f, 1.5f), size * Rand(rng, .7f, 1.2f), size * Rand(rng, .9f, 1.5f));
                }
            }

            // ---- the landscape: a small volcano ringing the lava, rocky ground, then grass and trees down to the sea
            const float Ground = -54f, SeaY = -68f;
            static readonly float[][] Cone =   // radius from the lake centre, height (the cone ends at the ground, r ~ 520)
            {
                new[] { 0f, LakeY }, new[] { 120f, LakeY }, new[] { 133f, -30f }, new[] { 165f, -2f }, new[] { 215f, 24f }, new[] { 265f, 46f },
                new[] { 300f, 58f }, new[] { 350f, 42f }, new[] { 400f, 6f }, new[] { 450f, -36f }, new[] { 520f, Ground },
            };

            static float Periodic(float th, float scale, float seed) => Noise(Mathf.Cos(th) * scale + seed, Mathf.Sin(th) * scale + seed * 1.7f);

            static float ConeHeight(float r, float th)
            {
                float y = Ground;
                for (int i = 0; i < Cone.Length - 1; i++)
                    if (r <= Cone[i + 1][0])
                    {
                        float t = Mathf.InverseLerp(Cone[i][0], Cone[i + 1][0], r); t = t * t * (3 - 2 * t);
                        y = Mathf.Lerp(Cone[i][1], Cone[i + 1][1], t); break;
                    }
                if (r < 128) return y;
                if (r > 135 && r < 330)
                {
                    // cliff strata: the wall steps up in ledges and risers instead of one smooth slope
                    const float terr = 8f; float q = y / terr, f = q - Mathf.Floor(q);
                    float stepped = (Mathf.Floor(q) + Sm(.5f, .9f, f)) * terr;
                    y = Mathf.Lerp(y, stepped, .75f * Mathf.Clamp01((r - 135) / 20f) * Mathf.Clamp01((330 - r) / 30f));
                }
                float ridge = .55f + .9f * Periodic(th, 1.7f, 3f);
                if (y > 0) y *= ridge;
                float fade = Mathf.Clamp01((r - 128) / 30f);
                float wx = r * Mathf.Cos(th), wz = r * Mathf.Sin(th);
                y += (Noise(wx * .035f + 11, wz * .035f + 7) - .5f) * 2 * 7f * fade;
                y += (Noise(wx * .11f + 3, wz * .11f + 19) - .5f) * 2 * 3.2f * fade;
                y += (Periodic(th, 13f, 5f) - .5f) * 2 * 7f * fade;
                if (r > 235 && r < 340) y += (Periodic(th, 34f, 2f) - .5f) * 2 * 12f * Sm(235f, 290f, r);   // a toothed crest
                if (r > 300)
                {
                    float g = Mathf.Abs(Mathf.Sin(th * 19f + Periodic(th, 3f, 9f) * 6f));
                    y -= Mathf.Pow(1 - g, 3) * 16f * Mathf.Clamp01((r - 300) / 100f) * Mathf.Clamp01((520 - r) / 60f);
                }
                return y;
            }

            /// Low rolling ground round the volcano, sinking into the sea at the coast.
            static float PlainsHeight(float r, float th)
            {
                float wx = r * Mathf.Cos(th), wz = r * Mathf.Sin(th);
                return Ground + (Noise(wx * .005f + 3, wz * .005f + 9) - .5f) * 2 * 6f + (Noise(wx * .0016f + 5, wz * .0016f + 1) - .5f) * 2 * 8f - Sm(1350f, 1800f, r) * 26f;
            }

            /// Height of the whole landscape at a point (polar about the lake centre).
            public static float CraterHeight(float r, float th)
            {
                if (r <= 440) return ConeHeight(r, th);
                float t = Sm(440, 520, r);
                return Mathf.Lerp(ConeHeight(Mathf.Min(r, 520), th), PlainsHeight(r, th), t);
            }

            /// A ring of terrain between two radii; `stepAt` gives the radial spacing at a radius.
            static Mesh TerrainRing(string name, float r0, float r1, System.Func<float, float> stepAt, int around, float uTiles, float vMetres, float lift)
            {
                var radii = new List<float>();
                for (float r = r0; r < r1; r += stepAt(r)) radii.Add(r);
                radii.Add(r1);
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
                foreach (var r in radii)
                    for (int a = 0; a <= around; a++)
                    {
                        float u = a / (float)around, th = u * Mathf.PI * 2;
                        float rr = r + (r > 140 && r < 450 ? (Periodic(th, 9f, 2f + r * .01f) - .5f) * 26 + (Periodic(th, 24f, 7f) - .5f) * 10 : 0);
                        verts.Add(new Vector3(Lake.x + Mathf.Cos(th) * rr, CraterHeight(r, th) + lift, Lake.z + Mathf.Sin(th) * rr));
                        uvs.Add(new Vector2(u * uTiles, r / vMetres));
                    }
                for (int p = 0; p < radii.Count - 1; p++)
                    for (int a = 0; a < around; a++)
                    {
                        int i0 = p * (around + 1) + a, i1 = i0 + 1, i2 = i0 + around + 1, i3 = i2 + 1;
                        tris.AddRange(new[] { i0, i1, i2, i1, i3, i2 });   // clockwise seen from above (Unity is left-handed): faces up
                    }
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }

            static void Crater(Transform root)
            {
                // the cone itself: basalt with lava streams, ringing the lake
                var cone = TerrainRing("Volcano", 0, 540, r => r < 130 ? 60 : (r < 340 ? 3.5f : 5.5f), 288, 20, 90f, 0);
                MeshObject(root, "Volcano", cone, WithRelief(Glowing(Color.white, .10f, rock2, rivers, new Color(3f, 1f, .18f), Vector2.one), rockNormal, 1.8f, new Vector2(3, 3)), true);
                // rocky ground round its foot
                var apron = TerrainRing("Rocky ground", 500, 800, r => 12f, 200, 150, 26f, .06f);
                MeshObject(root, "Rocky ground", apron, Lit(Color.white, .12f, gravel, Vector2.one), true);
                // grassland out to the coast
                var grass = TerrainRing("Grassland", 760, 2100, r => r < 1100 ? 22f : 40f, 200, 230, 30f, 0);
                MeshObject(root, "Grassland", grass, Lit(Color.white, .08f, grassTex, Vector2.one), true);
                var lakeMat = Glowing(Color.white, .3f, lavaBase, lavaGlow, new Color(2.6f, 1.05f, .2f), new Vector2(2.2f, 2.2f));
                var lake = Prim(PrimitiveType.Cylinder, root, "Lava lake", Lake + Vector3.up * .3f, new Vector3(246, .2f, 246), lakeMat, false);
                var scroll = lake.AddComponent<TennisVenueFx.LavaFlow>(); scroll.Material = lakeMat;
                Boulders(root);
                Steam(root);
                Trees(root);
            }

            /// Rock outcrops and boulders over the inner wall, the crest and the flank: relief you can see from
            /// the court, so the wall is a rock face, not a wall. Batched into one draw.
            static void Boulders(Transform root)
            {
                var rng = new System.Random(64);
                var mesh = new Mesh[8]; for (int i = 0; i < mesh.Length; i++) mesh[i] = RockMesh(rng, 1f);
                var hot = WithRelief(Glowing(Color.white, .12f, rock2, rivers, new Color(2.2f, .7f, .12f), Vector2.one), rockNormal, 1.6f, Vector2.one);
                var plain = WithRelief(Lit(new Color(.85f, .80f, .76f), .14f, rock2, new Vector2(1.5f, 1.5f)), rockNormal, 1.8f, new Vector2(2, 2));
                var group = new GameObject("Boulders").transform; group.SetParent(root, false);
                int count = TennisQuality.Current == TennisQuality.Tier.Low ? 90 : 240;
                for (int i = 0; i < count; i++)
                {
                    // only on the volcano itself: nothing scattered out over the ground
                    float th = Rand(rng, 0, Mathf.PI * 2);
                    float r = i % 5 == 0 ? Rand(rng, 255, 335) : Rand(rng, 138, 300);   // every fifth one along the crest
                    float size = r > 250 ? Rand(rng, 5, 15) : Rand(rng, 2.5f, 11);
                    var go = MeshObject(group, "Boulder", mesh[i % mesh.Length], i % 4 == 0 ? hot : plain, true);
                    go.transform.position = new Vector3(Lake.x + Mathf.Cos(th) * r, CraterHeight(r, th) + size * .12f, Lake.z + Mathf.Sin(th) * r);
                    go.transform.rotation = Quaternion.Euler(Rand(rng, -20, 20), Rand(rng, 0, 360), Rand(rng, -20, 20));
                    go.transform.localScale = new Vector3(size * Rand(rng, .8f, 1.5f), size * Rand(rng, .7f, 1.6f), size * Rand(rng, .8f, 1.5f));
                }
                StaticBatchingUtility.Combine(group.gameObject);
            }

            /// Groves on the grassland (the game's own toy trees), thinning toward the rocks and the coast.
            static void Trees(Transform root)
            {
                var rng = new System.Random(11);
                var group = new GameObject("Trees").transform; group.SetParent(root, false);
                var kinds = new[] { "EnvV4_TreeBroadleafA", "EnvV4_TreeBroadleafB", "EnvV4_TreeFlowering", "EnvV4_TreeBroadleafA", "EnvV4_ShrubA", "EnvV4_ShrubC" };
                int target = TennisQuality.Current == TennisQuality.Tier.Low ? 160 : 420, made = 0;
                for (int tries = 0; tries < target * 8 && made < target; tries++)
                {
                    float th = Rand(rng, 0, Mathf.PI * 2), r = 780 + Mathf.Pow((float)rng.NextDouble(), 1.2f) * 720;
                    float wx = r * Mathf.Cos(th), wz = r * Mathf.Sin(th);
                    if (Noise(wx * .006f + 2, wz * .006f + 8) < .42f) continue;              // groves, not an even carpet
                    float y = CraterHeight(r, th); if (y < SeaY + 3.5f) continue;             // not in the water
                    int k = rng.Next(kinds.Length); bool tree = k < 4;
                    TennisEnvironmentV4.Spawn(kinds[k], group, new Vector3(Lake.x + wx, y - .2f, Lake.z + wz), Rand(rng, 0, 360), tree ? Rand(rng, 16, 28) : Rand(rng, 5, 9), Rand(rng, .85f, 1.05f));
                    made++;
                }
            }

            /// Steam curling off the inner wall.
            static void Steam(Transform root)
            {
                bool low = TennisQuality.Current == TennisQuality.Tier.Low;
                for (int i = 0; i < (low ? 4 : 12); i++)
                {
                    float th = i / 12f * Mathf.PI * 2 + .35f, r = 150 + (i % 4) * 16;
                    var go = new GameObject("Steam vent").AddComponent<ParticleSystem>(); go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3(Lake.x + Mathf.Cos(th) * r, CraterHeight(r, th), Lake.z + Mathf.Sin(th) * r);
                    go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    var m = go.main; m.loop = true; m.prewarm = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(6, 10); m.startSpeed = new ParticleSystem.MinMaxCurve(5, 11); m.startSize = new ParticleSystem.MinMaxCurve(8, 18);
                    m.maxParticles = 60; m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    var e = go.emission; e.rateOverTime = 7;
                    var sh = go.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 3;
                    var c = go.colorOverLifetime; c.enabled = true;
                    var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, .6f, .35f), 0), new GradientColorKey(new Color(.5f, .38f, .38f), 1) },
                        new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.45f, .2f), new GradientAlphaKey(0, 1) });
                    c.color = g;
                    var s2 = go.sizeOverLifetime; s2.enabled = true; s2.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 1.8f)));
                    go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Sprite(Alpha, Puff, Color.white, 3014);
                }
            }

            static void Sea(Transform root)
            {
                var sea = Lit(new Color(.03f, .14f, .20f), .9f);
                var q = Prim(PrimitiveType.Quad, root, "Sea round the island", new Vector3(0, SeaY, 0), new Vector3(20000, 20000, 1), sea, false);
                q.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }

            static void Rocks(Transform root, float hx, float hz)
            {
                var rng = new System.Random(31);
                var mat = Glowing(Color.white, .2f, rock, crack, new Color(3f, 1f, .18f), new Vector2(1.5f, 1.5f));
                for (int i = 0; i < 16; i++)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = Rand(rng, 22, 65), size = Rand(rng, .8f, 3.6f);
                    var rockGo = MeshObject(root, "Floating rock", RockMesh(rng, size), mat, true);
                    rockGo.transform.localPosition = new Vector3(Mathf.Cos(ang) * r * .8f, Rand(rng, -32, 4), Mathf.Sin(ang) * r * 1.2f);
                    var b = rockGo.AddComponent<TennisVenueFx.Bob>(); b.Amplitude = Rand(rng, .3f, 1.1f); b.Speed = Rand(rng, .25f, .6f); b.Spin = Rand(rng, -14, 14); b.Phase = Rand(rng, 0, 6);
                }
            }

            static Mesh RockMesh(System.Random rng, float size)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); var src = go.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(go);
                var v = src.vertices; float seed = Rand(rng, 0, 100);
                for (int i = 0; i < v.Length; i++)
                {
                    var d = v[i].normalized;
                    float n = Noise(d.x * 2.1f + seed, d.y * 2.1f + d.z * 1.7f + seed) * 1.1f + .55f;
                    v[i] = d * .5f * size * n * (1 - Mathf.Max(0, -d.y) * .35f);
                }
                var mesh = new Mesh { name = "Floating rock" }; mesh.vertices = v; mesh.uv = src.uv; mesh.triangles = src.triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }

            static void Braziers(Transform root, float hx, float hz)
            {
                var bowl = Lit(new Color(.09f, .08f, .09f), .3f);
                var flame = Sprite(Additive, Soft, new Color(1f, .55f, .16f, .9f), 3008);
                bool light = TennisQuality.Current != TennisQuality.Tier.Low;
                foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                {
                    var p = new Vector3(c.x * (hx - .9f), 0, c.y * (hz - .9f));
                    Prim(PrimitiveType.Cylinder, root, "Brazier stand", p + Vector3.up * .55f, new Vector3(.32f, .55f, .32f), bowl);
                    Prim(PrimitiveType.Cylinder, root, "Brazier bowl", p + Vector3.up * 1.15f, new Vector3(.9f, .16f, .9f), bowl);
                    var f = Billboard(root, "Flame", flame, p + Vector3.up * 1.9f, new Vector2(1.5f, 2.4f));
                    f.AddComponent<TennisVenueFx.Flicker>();
                    if (light)
                    {
                        var l = new GameObject("Brazier light").AddComponent<Light>(); l.type = LightType.Point; l.transform.SetParent(root, false);
                        l.transform.localPosition = p + Vector3.up * 2f; l.color = new Color(1f, .5f, .2f); l.range = 14; l.intensity = 2.2f; l.shadows = LightShadows.None;
                    }
                }
            }

            static void Sky(Transform root)
            {
                Vector3 sun = SunDir;
                Dome(root, d =>
                {
                    float up = Mathf.Clamp01(d.y * 1.5f), below = Mathf.Clamp01(-d.y * 4);
                    var horizon = new Color(1f, .42f, .13f); var mid = new Color(.62f, .20f, .30f); var zenith = new Color(.10f, .06f, .18f);
                    var c = up < .3f ? Grad(horizon, mid, up / .3f) : Grad(mid, zenith, (up - .3f) / .7f);
                    c = Grad(c, new Color(.30f, .10f, .08f), below);
                    float s = Mathf.Max(0, Vector3.Dot(d, sun));
                    c += new Color(1f, .5f, .18f) * (Mathf.Pow(s, 8) * .6f + Mathf.Pow(s, 70) * 1f);
                    c.a = 1; return c;
                });
                var glow = Sprite(Additive, Soft, new Color(1f, .55f, .22f, .6f), 2999, null, .7f);
                var g = Billboard(root, "Sun glow", glow, SunDir * 2800, new Vector2(1400, 1400), false);
                var disc = Sprite(Additive, Soft, new Color(1f, .8f, .45f, .9f), 3000, null, 1f);
                var s2 = Billboard(root, "Sun", disc, SunDir * 2790, new Vector2(240, 240), false);
                g.AddComponent<TennisVenueFx.FollowCamera>().Offset = SunDir * 2800; s2.AddComponent<TennisVenueFx.FollowCamera>().Offset = SunDir * 2790;
                // a low deck of ash cloud, lit orange from the crater below
                var rng = new System.Random(9); var alpha = Alpha;
                var group = new GameObject("Ash cloud").transform; group.SetParent(root, false);
                for (int i = 0; i < 3; i++)
                {
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Ash sheet " + i; Object.Destroy(q.GetComponent<Collider>());
                    q.transform.SetParent(group, false); q.transform.localPosition = new Vector3(0, 420 + i * 90, 0); q.transform.localRotation = Quaternion.Euler(90, i * 41, 0); q.transform.localScale = new Vector3(6000, 6000, 1);
                    var r = q.GetComponent<MeshRenderer>(); r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                    r.sharedMaterial = Sprite(alpha, Sheet(40 + i * 7), Color.Lerp(new Color(.85f, .34f, .18f, .6f), new Color(.34f, .16f, .2f, .6f), i / 2f), 3000 + i, new Vector2(6, 6));
                }
                for (int i = 0; i < 40; i++)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = Rand(rng, 500, 2200), size = Rand(rng, 260, 700);
                    Billboard(group, "Ash bank", Sprite(alpha, Puff, Color.Lerp(new Color(.72f, .28f, .16f, .9f), new Color(.3f, .14f, .17f, .9f), Rand(rng, 0, 1)), 3010 + i % 6),
                        new Vector3(Mathf.Cos(ang) * r, Rand(rng, 60, 260), Mathf.Sin(ang) * r), new Vector2(size * 1.8f, size));
                }
            }

            /// The eruption: a tall column of ash and fire fountains over the vent.
            static void Plume(Transform root)
            {
                bool low = TennisQuality.Current == TennisQuality.Tier.Low;
                var vent = new Vector3(Lake.x, LakeY + 2, Lake.z + 105);
                var smoke = new GameObject("Plume").AddComponent<ParticleSystem>(); smoke.transform.SetParent(root, false); smoke.transform.localPosition = vent;
                smoke.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var main = smoke.main; main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(12, 20); main.startSpeed = new ParticleSystem.MinMaxCurve(22, 36);
                main.startSize = new ParticleSystem.MinMaxCurve(38, 85); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                main.maxParticles = low ? 380 : 900; main.gravityModifier = -.02f;
                var em = smoke.emission; em.rateOverTime = low ? 18 : 42;
                var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 7; sh.radius = 12;
                var col = smoke.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, .62f, .24f), 0), new GradientColorKey(new Color(.86f, .40f, .26f), .2f), new GradientColorKey(new Color(.46f, .28f, .30f), .55f), new GradientColorKey(new Color(.26f, .21f, .27f), 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .08f), new GradientAlphaKey(.75f, .7f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var szl = smoke.sizeOverLifetime; szl.enabled = true; szl.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(1, 1.7f)));
                var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
                var pr = smoke.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = Sprite(Alpha, Puff, Color.white, 3012); pr.shadowCastingMode = ShadowCastingMode.Off;
                // fire fountains
                for (int i = 0; i < (low ? 2 : 4); i++)
                {
                    var f = new GameObject("Lava fountain").AddComponent<ParticleSystem>(); f.transform.SetParent(root, false);
                    f.transform.localPosition = Lake + new Vector3(Mathf.Sin(i * 1.9f) * 55, 1, 50 + Mathf.Cos(i * 2.3f) * 55); f.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    var fm = f.main; fm.loop = true; fm.prewarm = true; fm.startLifetime = 3.6f; fm.startSpeed = new ParticleSystem.MinMaxCurve(22, 42); fm.startSize = new ParticleSystem.MinMaxCurve(1.6f, 4.2f);
                    fm.gravityModifier = 2.4f; fm.simulationSpace = ParticleSystemSimulationSpace.World; fm.maxParticles = 160;
                    var fe = f.emission; fe.rateOverTime = 36;
                    var fs = f.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 14; fs.radius = 3;
                    var fc = f.colorOverLifetime; fc.enabled = true;
                    var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(new Color(1f, .8f, .3f), 0), new GradientColorKey(new Color(1f, .3f, .08f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                    fc.color = fg;
                    var fr = f.GetComponent<ParticleSystemRenderer>(); fr.sharedMaterial = Sprite(Additive, Soft, Color.white, 3011); fr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            /// Sparks and ash drifting up past the court.
            static void Embers(Transform root)
            {
                var e = new GameObject("Embers").AddComponent<ParticleSystem>(); e.transform.SetParent(root, false); e.transform.localPosition = new Vector3(0, -12, 4);
                var m = e.main; m.loop = true; m.prewarm = true; m.startLifetime = new ParticleSystem.MinMaxCurve(6, 11); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(.08f, .22f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = TennisQuality.Current == TennisQuality.Tier.Low ? 120 : 320;
                var em = e.emission; em.rateOverTime = TennisQuality.Current == TennisQuality.Tier.Low ? 14 : 34;
                var s = e.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(90, 4, 130); s.rotation = new Vector3(-90, 0, 0);
                var v = e.velocityOverLifetime; v.enabled = true; v.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f); v.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f); v.y = 0;
                var c = e.colorOverLifetime; c.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, .82f, .35f), 0), new GradientColorKey(new Color(1f, .3f, .08f), .6f), new GradientColorKey(new Color(.6f, .1f, .05f), 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0, 1) });
                c.color = g;
                var r = e.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Sprite(Additive, Soft, Color.white, 3013); r.shadowCastingMode = ShadowCastingMode.Off;
            }

            public static void Light(Light sun, Camera camera)
            {
                sun.transform.rotation = Quaternion.LookRotation(-SunDir);
                sun.color = new Color(1f, .52f, .26f); sun.intensity = 2.3f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.34f, .24f, .40f);
                RenderSettings.ambientEquatorColor = new Color(.55f, .30f, .22f);
                RenderSettings.ambientGroundColor = new Color(.60f, .22f, .08f);   // the lava lights everything from below
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.42f, .19f, .14f);
                RenderSettings.fogStartDistance = 350; RenderSettings.fogEndDistance = 3800;
                RenderSettings.skybox = null;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.42f, .19f, .14f);
                // an orange up-light from the crater, on the players' undersides and the racket
                var wallGlow = new GameObject("Lava wall glow").AddComponent<Light>(); wallGlow.type = LightType.Directional; wallGlow.shadows = LightShadows.None;
                wallGlow.transform.rotation = Quaternion.LookRotation(new Vector3(0f, .30f, .95f)); wallGlow.color = new Color(1f, .52f, .22f); wallGlow.intensity = .38f;
                var up = new GameObject("Lava up-light").AddComponent<Light>(); up.type = LightType.Directional; up.shadows = LightShadows.None;
                up.transform.rotation = Quaternion.LookRotation(new Vector3(.05f, .95f, .25f)); up.color = new Color(1f, .42f, .12f); up.intensity = .8f;
            }
        }
    }
}
