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
    public static partial class TennisVenueBuilder
    {
        /// Arena materials that make up the court kit (the Blender names travel in materials.json). The resort's
        /// dressing -- planters (017), sofas (020) and garden lanterns (033) -- is deliberately not in it: on the
        /// rooftop and the crater those read as the beach club, so each venue builds its own props instead.
        static readonly string[] CourtKit =
        {
            "TropicalV3_002",   // court
            "TropicalV3_003",   // lines
            "TropicalV3_008",   // net posts
            "TropicalV3_011",   // run-off apron
            "TropicalV3_012",   // net
            "TropicalV3_023",   // umpire chair
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
            var clock = System.Diagnostics.Stopwatch.StartNew();
            switch (kind)
            {
                case TennisVenueKind.Skyscraper: Skyscraper.Build(root); break;
                case TennisVenueKind.Volcano: Volcano.Build(root); break;
            }
            Log(kind + " venue built in " + clock.ElapsedMilliseconds + " ms", 0);
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
    }
}
