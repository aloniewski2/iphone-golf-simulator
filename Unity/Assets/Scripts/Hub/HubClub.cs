using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// The country-club kit (ArtDir/ui/hub-targets-2026-10-12/country-club): cream stone, navy slate, gold lettering, lawns,
    /// hedges and roses at golden hour. Materials, the small textures painted once at build time (stone courses, slate, gravel,
    /// cobbles, lawn stripes, planks, the lit rooms behind the windows, the clock), a mesh builder for faceted architecture, and
    /// the props HubArt and HubRooms place. Everything here is static scenery (static-batched by HubWorld); what moves lives in
    /// HubLife. Soft organic things use GolfArcade/HubSurface (wind sway, world noise), water GolfArcade/HubWater.
    public static class HubClub
    {
        // ---- palette (picked from the AFTER sheets)
        public static readonly Color Stone = C("D9CBB0"), StoneLight = C("E8DECB"), StoneShade = C("BBAA8D"),
            Navy = C("1F2C5C"), NavyDeep = C("16204A"), Slate = C("34437A"), Gold = C("E6B94E"), Brass = C("C99A3C"),
            Wood = C("8C5A33"), WoodDark = C("5C371F"), WoodWarm = C("9A5E30"), Lawn = C("427F27"), HedgeGreen = C("2F6524"),
            Leaf = C("3A7529"), LeafDark = C("1F4A19"), White = C("F6F3EB"), Iron = C("2A2C33"), Gravel = C("D3C8B4"), Rose = C("EC6F95"),
            RosePale = C("F7B5C8"), RoseRed = C("D64759"), Sand = C("EEDDB2"), Cliff = C("9C8466"), CliffDark = C("6F5D49"), Terracotta = C("B9734C");
        public static readonly Color WarmGlow = new Color(1.6f, .92f, .38f), LanternGlow = new Color(2.6f, 1.35f, .42f);
        /// The slate's tint (the slate texture is greyscale).
        public static readonly Color SlateTint = C("394A7C");
        public static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        // ================================================================== materials
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        public static Material Lit(Color c, float smooth = .3f) => HubArt.Lit(c, smooth);
        public static Material Glow(Color hdr) => HubArt.Glow(hdr);
        /// URP Lit with a painted base map (UVs in metres, so `tile` is the texture's size in metres).
        public static Material LitTex(Texture2D tex, Color tint, float smooth, float tile)
        {
            string key = "littex" + tex.name + tint + smooth + tile;
            if (mats.TryGetValue(key, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Hub " + tex.name, enableInstancing = true };
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0);
            m.SetTextureScale("_BaseMap", Vector2.one / tile);
            return mats[key] = m;
        }
        /// An unlit painted picture (a lit room seen through glass, the clock face): `hdr` > 1 glows a little under bloom.
        public static Material Painted(Texture2D tex, float hdr = 1f)
        {
            string key = "painted" + tex.name + hdr;
            if (mats.TryGetValue(key, out var m) && m) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Hub painted " + tex.name };
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(hdr, hdr, hdr, 1));
            return mats[key] = m;
        }
        /// A painted picture fitted to a rectangle given in its mesh's UVs (metres): x0..x0+w across, y0..y0+h up.
        public static Material PaintedFit(Texture2D tex, float hdr, float w, float h, float x0 = 0, float y0 = 0)
        {
            string key = "paintedfit" + tex.name + hdr + w + h + x0 + y0;
            if (mats.TryGetValue(key, out var m) && m) return m;
            m = new Material(Painted(tex, hdr)) { name = "Hub painted " + tex.name };
            m.SetTextureScale("_BaseMap", new Vector2(1 / w, 1 / h)); m.SetTextureOffset("_BaseMap", new Vector2(-x0 / w, -y0 / h));
            return mats[key] = m;
        }
        static Shader surfaceShader, waterShader;
        /// GolfArcade/HubSurface (falls back to URP Lit if the shader is missing). uvMode 0 mesh UVs (metres), 1 world XZ, 2 rings.
        public static Material Surface(string key, Color c, Texture2D tex = null, int uvMode = 0, Vector2 tile = default, float noise = 0, float noiseScale = 2,
            float wrap = .35f, float gloss = .08f, float leaf = 0, float sway = 0, float vertexTint = 1, Vector3 centre = default, bool twoSided = false)
        {
            key = "surf " + key;
            if (mats.TryGetValue(key, out var m) && m) return m;
            if (!surfaceShader) surfaceShader = Shader.Find("GolfArcade/HubSurface");
            if (!surfaceShader || !surfaceShader.isSupported) { m = new Material(Lit(c, .15f)) { name = "Hub " + key }; if (tex) { m.SetTexture("_BaseMap", tex); m.SetTextureScale("_BaseMap", Vector2.one / Mathf.Max(.01f, tile.x)); } return mats[key] = m; }
            m = new Material(surfaceShader) { name = "Hub " + key, enableInstancing = true };
            if (tex) m.SetTexture("_BaseMap", tex);
            if (uvMode == 0 && tex) m.SetTextureScale("_BaseMap", new Vector2(1 / Mathf.Max(.01f, tile.x), 1 / Mathf.Max(.01f, tile.y > 0 ? tile.y : tile.x)));
            m.SetColor("_BaseColor", c); m.SetFloat("_UVMode", uvMode); m.SetVector("_Tile", new Vector4(Mathf.Max(.01f, tile.x), Mathf.Max(.01f, tile.y > 0 ? tile.y : tile.x), 0, 0));
            m.SetVector("_Centre", centre); m.SetFloat("_Noise", noise); m.SetFloat("_NoiseScale", noiseScale); m.SetFloat("_Wrap", wrap); m.SetFloat("_Gloss", gloss);
            m.SetFloat("_Leaf", leaf); m.SetFloat("_Sway", sway); m.SetFloat("_VertexTint", vertexTint); m.SetFloat("_Cull", twoSided ? 0 : 2);
            return mats[key] = m;
        }
        public static Material Foliage => Surface("foliage", Color.white, null, 0, default, .55f, 1.6f, .38f, .05f, .28f, .35f);
        public static Material HedgeMat => Surface("hedge", Color.white, null, 0, default, .75f, 5.5f, .35f, .04f, .15f, 0);
        public static Material Water(string key, Color c, Color sky, Texture2D tex = null, float ripple = .25f, float scale = 1.5f, float glitter = 2f, float cells = 3f, float speed = 1f)
        {
            key = "water " + key;
            if (mats.TryGetValue(key, out var m) && m) return m;
            if (!waterShader) waterShader = Shader.Find("GolfArcade/HubWater");
            if (!waterShader || !waterShader.isSupported) return mats[key] = new Material(Lit(c, .9f)) { name = "Hub " + key };
            m = new Material(waterShader) { name = "Hub " + key };
            if (tex) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", c); m.SetColor("_SkyColor", sky); m.SetFloat("_Ripple", ripple); m.SetFloat("_RippleScale", scale);
            m.SetFloat("_Glitter", glitter); m.SetFloat("_GlitterCells", cells); m.SetFloat("_Speed", speed);
            return mats[key] = m;
        }

        // ================================================================== painted textures (once, cached)
        static readonly Dictionary<string, Texture2D> texs = new Dictionary<string, Texture2D>();
        static float H(int x, int y, int s = 0) { uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xffffff) / 16777216f; }
        static float N(float x, float y) => Mathf.PerlinNoise(x, y);
        /// Paints a texture once: `f(u, v)` with u, v in 0..1 (v up). Repeats unless `clamp`.
        public static Texture2D Paint(string name, int w, int h, Func<float, float, Color> f, bool clamp = false, bool mips = true)
        {
            if (texs.TryGetValue(name, out var t) && t) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = name, wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f((x + .5f) / w, (y + .5f) / h);
            t.SetPixels32(px); t.Apply(mips, true); return texs[name] = t;
        }
        static Color Mul(Color c, float k) { c.r *= k; c.g *= k; c.b *= k; return c; }
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));

        /// Ashlar: four courses per tile, blocks staggered, thin mortar, each block a slightly different cream. Tile = 1.2 m.
        public static Texture2D StoneTex => Paint("stone courses", 256, 256, (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 4); float rv = v * 4 - row;
            float off = row % 2 == 0 ? 0 : .25f; float bu = (u + off) * 2; int col = Mathf.FloorToInt(bu); float cu = bu - col;
            float k = .93f + H(col, row, 1) * .12f + (N(u * 18, v * 18) - .5f) * .06f;
            var c = Mul(Color.white, k);
            float mortar = Mathf.Min(Mathf.Min(rv, 1 - rv) * 4 * .3f / .3f, Mathf.Min(cu, 1 - cu) * 2 * .6f / .3f);
            c = Color.Lerp(Mul(Color.white, .76f), c, Smooth(.012f, .05f, mortar));
            c = Mul(c, 1 - .05f * Smooth(.6f, 1, rv));   // a soft shade under each course
            c.a = 1; return c;
        });
        /// Slate shingles: staggered rows, each slate a shade of navy-grey, a dark line under every row. Tile = 1.6 m (8 rows).
        public static Texture2D SlateTex => Paint("slate", 256, 256, (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 8); float rv = v * 8 - row;
            float bu = (u + (row % 2) * .5f / 6) * 6; int col = Mathf.FloorToInt(bu); float cu = bu - col;
            float k = .88f + H(col, row, 2) * .2f;
            var c = Mul(Color.white, k * Mathf.Lerp(.82f, 1.02f, rv));
            if (rv < .07f) c = Mul(c, .55f);
            if (cu < .03f || cu > .97f) c = Mul(c, .75f);
            c.a = 1; return c;
        });
        /// Pale gravel: speckles of light and dark grains. Tile = 1.5 m.
        public static Texture2D GravelTex => Paint("gravel", 256, 256, (u, v) =>
        {
            float g = H(Mathf.FloorToInt(u * 128), Mathf.FloorToInt(v * 128), 3), g2 = H(Mathf.FloorToInt(u * 64), Mathf.FloorToInt(v * 64), 4);
            float k = .92f + (g - .5f) * .16f + (g2 - .5f) * .1f + (N(u * 6, v * 6) - .5f) * .08f;
            if (g > .94f) k *= .78f; if (g < .05f) k *= 1.07f;
            var c = Mul(Color.white, k); c.a = 1; return c;
        });
        /// Cobbles: two staggered rows of rounded setts with dark joints. Tile = 1.2 m along x 0.6 m across.
        public static Texture2D CobbleTex => Paint("cobbles", 256, 128, (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 2); float rv = v * 2 - row;
            float bu = (u + (row % 2) * .125f) * 4; int col = Mathf.FloorToInt(bu); float cu = bu - col;
            float dx = Mathf.Abs(cu - .5f) * 2, dy = Mathf.Abs(rv - .5f) * 2;
            float d = Mathf.Pow(Mathf.Pow(dx, 4) + Mathf.Pow(dy, 4), .25f);
            float k = .86f + H(col, row, 5) * .2f;
            var c = Mul(Color.white, k * (1.04f - d * .14f) + (N(u * 30, v * 30) - .5f) * .05f);
            c = Color.Lerp(Mul(Color.white, .55f), c, Smooth(.97f, .84f, d));
            c.a = 1; return c;
        });
        /// Flagstones for the paths: big irregular slabs in courses. Tile = 2.4 m.
        public static Texture2D FlagTex => Paint("flagstones", 256, 256, (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 3); float rv = v * 3 - row;
            float bu = (u + H(row, 0, 6) * .7f) * (2 + (row % 2)); int col = Mathf.FloorToInt(bu); float cu = bu - col;
            float k = .9f + H(col, row, 7) * .16f + (N(u * 14, v * 14) - .5f) * .07f;
            var c = Mul(Color.white, k);
            float edge = Mathf.Min(Mathf.Min(rv, 1 - rv) * 3, Mathf.Min(cu, 1 - cu) * 2.5f);
            c = Color.Lerp(Mul(Color.white, .7f), c, Smooth(.01f, .05f, edge));
            c.a = 1; return c;
        });
        /// Lawn grass: fine blades and soft patches. Tile = 2 m.
        public static Texture2D GrassTex => Paint("grass", 256, 256, (u, v) =>
        {
            float b = H(Mathf.FloorToInt(u * 256), Mathf.FloorToInt(v * 96), 8);
            float k = .94f + (b - .5f) * .14f + (N(u * 5, v * 5) - .5f) * .1f + (N(u * 23 + 7, v * 23) - .5f) * .06f;
            var c = Mul(Color.white, k); c.a = 1; return c;
        });
        /// Mown stripes: bands across v (across a court's length), light and dark. Tile = 2.6 m along v.
        public static Texture2D StripeTexZ => Paint("mown stripes z", 128, 128, (u, v) =>
        {
            float band = Smooth(.46f, .54f, v) * (1 - Smooth(.96f, 1f, v));
            float b = H(Mathf.FloorToInt(v * 128), Mathf.FloorToInt(u * 48), 19);
            float k = Mathf.Lerp(1.08f, .9f, band) + (b - .5f) * .1f + (N(u * 4, v * 4) - .5f) * .06f;
            var c = Mul(Color.white, k); c.a = 1; return c;
        });
        /// Diagonal mown stripes (the practice range). Tile = 4.2 m.
        public static Texture2D StripeTexD => Paint("mown stripes diagonal", 128, 128, (u, v) =>
        {
            float d = Mathf.Repeat(u + v, 1f); float band = Smooth(.46f, .54f, d) * (1 - Smooth(.96f, 1f, d));
            float b = H(Mathf.FloorToInt(u * 128), Mathf.FloorToInt(v * 128), 29);
            float k = Mathf.Lerp(1.08f, .91f, band) + (b - .5f) * .1f + (N(u * 4, v * 4) - .5f) * .06f;
            var c = Mul(Color.white, k); c.a = 1; return c;
        });
        /// Wood planks along u. Tile = 1 m.
        public static Texture2D PlankTex => Paint("planks", 128, 128, (u, v) =>
        {
            int row = Mathf.FloorToInt(v * 6); float rv = v * 6 - row;
            float k = .88f + H(row, 0, 10) * .18f + Mathf.Sin((u * 9 + N(u * 3, row * 3.1f) * 2) * 6.28f) * .03f + (N(u * 40, v * 6) - .5f) * .08f;
            var c = Mul(Color.white, k); if (rv < .08f) c = Mul(c, .6f);
            c.a = 1; return c;
        });
        /// Rock strata for the cliffs. Tile = 3 m.
        public static Texture2D CliffTex => Paint("cliff", 256, 256, (u, v) =>
        {
            float layer = v * 7 + N(u * 3, v * 2) * 1.6f; int l = Mathf.FloorToInt(layer); float lf = layer - l;
            float k = .82f + H(l, 0, 11) * .26f + (N(u * 12, v * 12) - .5f) * .14f;
            if (lf < .1f) k *= .7f;
            float crack = Mathf.Abs(N(u * 5 + 3, v * 1.5f) - .5f); if (crack < .02f) k *= .72f;
            var c = Mul(Color.white, k); c.a = 1; return c;
        });
        /// Awning stripes, navy and cream, across u (2 pairs per tile; tile = 1.2 m).
        public static Texture2D AwningTex => Paint("awning", 64, 16, (u, v) =>
        {
            bool navy = Mathf.FloorToInt(u * 4) % 2 == 0;
            var c = navy ? Navy : C("F3EAD6"); c = Mul(c, .96f + (N(u * 30, v * 3) - .5f) * .06f); c.a = 1; return c;
        });

        // ---- painted rooms behind the glass (unlit, a little over 1 so they glow)
        static float Box2(float u, float v, float cx, float cy, float hw, float hh, float r = 0) { float dx = Mathf.Max(Mathf.Abs(u - cx) - hw + r, 0), dy = Mathf.Max(Mathf.Abs(v - cy) - hh + r, 0); return Mathf.Sqrt(dx * dx + dy * dy) - r; }
        static Color Over(Color under, Color c, float d, float soft = .004f) => Color.Lerp(under, c, Smooth(soft, -soft, d));
        /// The locker shop: warm wood lockers at the back, a rail of shirts, spot lights, a polished floor.
        public static Texture2D LockerInterior => Paint("locker shop", 256, 256, (u, v) =>
        {
            var c = Color.Lerp(C("7A4A25"), C("E7B676"), Smooth(.15f, .95f, v));
            // lockers: vertical doors with dark seams
            if (v > .28f && v < .93f) { float cu = (u * 7) % 1; c = Color.Lerp(C("9A6435"), C("C98E52"), (v - .28f) * 1.2f); if (cu < .05f || cu > .95f) c = Mul(c, .6f); if (Mathf.Abs(v - .8f) < .012f) c = Mul(c, .7f); }
            // the rail and shirts (navy, white, sky, cream, red)
            c = Over(c, C("D9C9A8"), Mathf.Abs(v - .74f) - .008f);
            var shirts = new[] { C("2B3D78"), C("F4F1EA"), C("8DB9E3"), C("EFE2C4"), C("C9434F"), C("2B3D78"), C("F4F1EA"), C("6F9C4E"), C("8DB9E3") };
            for (int i = 0; i < shirts.Length; i++) { float cx = .1f + i * .1f; c = Over(c, Mul(shirts[i], .95f + .1f * Smooth(.4f, .72f, v)), Box2(u, v, cx, .6f, .043f, .13f, .02f)); }
            // floor and spots
            if (v < .28f) c = Color.Lerp(C("5E3A1F"), C("8F5C31"), v / .28f) * (1 + .15f * Mathf.Sin(u * 40));
            for (int i = 0; i < 4; i++) c += C("FFE8B8") * Mathf.Exp(-((u - .15f - i * .23f) * (u - .15f - i * .23f) * 900 + (v - .97f) * (v - .97f) * 400)) * .8f;
            c.a = 1; return c;
        }, true);
        /// The trophy lounge: shelves of gold cups, a warm lamp, leather chairs.
        public static Texture2D LoungeInterior => Paint("trophy lounge", 256, 256, (u, v) =>
        {
            var c = Color.Lerp(C("6B3B1E"), C("E2A866"), Smooth(.1f, .9f, v));
            for (int s = 0; s < 3; s++)
            {
                float sy = .42f + s * .19f;
                c = Over(c, C("5A3218"), Mathf.Abs(v - sy) - .012f);
                for (int i = 0; i < 5; i++)
                {
                    float cx = .14f + i * .18f + (s % 2) * .05f, h = .06f + H(i, s, 12) * .03f;
                    float cup = Mathf.Min(Box2(u, v, cx, sy + .018f + h * .55f, .028f, h * .45f, .02f), Box2(u, v, cx, sy + .02f, .02f, .008f));
                    c = Over(c, Color.Lerp(C("B8862E"), C("FFE08A"), Smooth(sy, sy + h, v)), cup);
                }
            }
            for (int k = 0; k < 2; k++) c = Over(c, Color.Lerp(C("3E2414"), C("7A4425"), v * 3), Box2(u, v, .22f + k * .56f, .14f, .15f, .1f, .05f));
            c += C("FFD9A0") * Mathf.Exp(-((u - .5f) * (u - .5f) * 30 + (v - .95f) * (v - .95f) * 60)) * .5f;
            c.a = 1; return c;
        }, true);
        /// Through the PLAY doors: a short lit passage opening onto a lawn court and a green with a flag under a golden sky.
        public static Texture2D PlayGlimpse => Paint("play glimpse", 256, 256, (u, v) =>
        {
            float horizon = .42f; Color c;
            if (v > horizon) c = Color.Lerp(C("FFD9A0"), C("9EC0E6"), Smooth(horizon, 1, v));
            else
            {
                float depth = (horizon - v) / horizon;
                c = Color.Lerp(C("8CC25A"), C("4E8E31"), depth);
                if (Mathf.FloorToInt((u - .5f) / (.04f + depth * .3f) + 20) % 2 == 0) c = Mul(c, 1.06f);
                float half = .1f + depth * .55f;
                if (Mathf.Abs(Mathf.Abs(u - .5f) - half) < .003f + depth * .006f) c = Color.white;
                if (Mathf.Abs(v - horizon * .75f) < .004f && Mathf.Abs(u - .5f) < .1f + .25f * .55f) c = Color.white;
                if (Mathf.Abs(v - horizon * .86f) < .02f && Mathf.Abs(u - .5f) < .16f) c = Color.Lerp(c, C("1C2333"), .7f);   // the net
            }
            // the tree line on the horizon
            float trees = horizon + .05f + .03f * Mathf.Sin(u * 40) + .02f * Mathf.Sin(u * 97);
            if (v > horizon && v < trees) c = Color.Lerp(C("3F6E2C"), C("5D8C3A"), (v - horizon) * 10);
            // the golf flag
            if (Mathf.Abs(u - .56f) < .004f && v > .3f && v < .5f) c = Color.white;
            if (u > .56f && u < .62f && v > .45f - (u - .56f) * .3f && v < .5f) c = C("E8433A");
            // the lit passage round the edges (the arch's sides and floor), brightest near the opening
            float edge = Mathf.Min(Mathf.Min(u, 1 - u) * 3.2f, v * 4f);
            c = Color.Lerp(Color.Lerp(C("9A5A26"), C("F2C27A"), Smooth(0, .6f, Mathf.Min(u, 1 - u) * 3 + v)), c, Smooth(.55f, .8f, edge));
            c.a = 1; return c;
        }, true);
        /// The clock face: cream dial, bold hour bars, minute dots, black hands at ten past ten, a gold rim.
        public static Texture2D ClockFace => Paint("clock", 256, 256, (u, v) =>
        {
            var p = new Vector2(u - .5f, v - .5f); float r = p.magnitude, a = Mathf.Atan2(p.x, p.y);
            if (r > .5f) return new Color(0, 0, 0, 0);
            var c = Color.Lerp(C("FBF4E2"), C("EADCC0"), r * 1.6f);
            float hourA = Mathf.Repeat(a, Mathf.PI * 2 / 12), minA = Mathf.Repeat(a, Mathf.PI * 2 / 60);
            float hd = Mathf.Min(hourA, Mathf.PI * 2 / 12 - hourA) * r, md = Mathf.Min(minA, Mathf.PI * 2 / 60 - minA) * r;
            if (r > .33f && r < .42f && hd < .012f) c = C("1D1A18");
            if (r > .4f && r < .42f && md < .004f) c = C("1D1A18");
            float Hand(float ang, float len, float w) { var d = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang)); float along = Vector2.Dot(p, d); float across = Mathf.Abs(p.x * d.y - p.y * d.x); return along > -.04f && along < len ? across - w * (1 - along / len * .6f) : 1; }
            if (Hand(Mathf.PI * 2 * (10 + 10 / 60f) / 12, .22f, .018f) < 0 || Hand(Mathf.PI * 2 * 10 / 60, .33f, .012f) < 0 || r < .025f) c = C("15130F");
            if (r > .445f) c = Color.Lerp(C("A87A28"), C("F4D27A"), .5f + .5f * Mathf.Sin(a * 3 + 1));
            c.a = 1; return c;
        }, true);
        /// The green windscreen of the tennis fences: a fine weave.
        public static Texture2D WindscreenTex => Paint("windscreen", 64, 64, (u, v) =>
        {
            float w = (Mathf.FloorToInt(u * 64) + Mathf.FloorToInt(v * 64)) % 2 == 0 ? 1 : .94f;
            var c = Mul(C("234A2F"), w * (.95f + (N(u * 8, v * 8) - .5f) * .1f)); c.a = 1; return c;
        });
        /// A tennis net: dark mesh squares, read from a distance.
        // ================================================================== mesh builder
        /// A local frame for a facade: origin on the ground at the facade's middle, R = the viewer's right, U = up, O = out toward
        /// the viewer. P maps local (x right, y up, z out) to world.
        public struct Frame
        {
            public Vector3 origin, R, U, O;
            public Frame(Vector3 origin, Vector3 outward) { this.origin = origin; O = new Vector3(outward.x, 0, outward.z).normalized; U = Vector3.up; R = Vector3.Cross(O, U); }
            public Vector3 P(float x, float y, float z) => origin + R * x + U * y + O * z;
            public Vector3 D(float x, float y, float z) => R * x + U * y + O * z;
            public Quaternion Facing => Quaternion.LookRotation(O, Vector3.up);
        }

        /// Collects triangles (any winding: Build fixes it from the normals), UVs in metres and vertex colours (rgb tint, a = sway).
        public sealed class MB
        {
            readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Color> col = new List<Color>(); readonly List<int> t = new List<int>();
            public Color colour = new Color(1, 1, 1, 0);
            public int Count => v.Count;
            int Add(Vector3 p, Vector3 nn, Vector2 u) { v.Add(p); n.Add(nn); uv.Add(u); col.Add(colour); return v.Count - 1; }
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 nn, Vector2 ua, Vector2 ub, Vector2 uc) { t.Add(Add(a, nn, ua)); t.Add(Add(b, nn, ub)); t.Add(Add(c, nn, uc)); }
            /// a b c d round the quad; UVs from a (0,0) along a->d (u) and a->b (v), in metres.
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nn, Vector2 uvOrigin = default)
            {
                float w = (d - a).magnitude, h = (b - a).magnitude; var ud = (d - a).normalized; var vb = (b - a).normalized;
                Vector2 U(Vector3 p) => uvOrigin + new Vector2(Vector3.Dot(p - a, ud), Vector3.Dot(p - a, vb));
                int i0 = Add(a, nn, U(a)), i1 = Add(b, nn, U(b)), i2 = Add(c, nn, U(c)), i3 = Add(d, nn, U(d));
                t.AddRange(new[] { i0, i1, i2, i0, i2, i3 });
            }
            /// A box in a frame: centre (local), size (x right, y up, z out). UVs: metres on each face (horizontal u, vertical v).
            public void Box(Frame f, Vector3 c, Vector3 s, bool bottom = false)
            {
                Vector3 P(float x, float y, float z) => f.P(c.x + x * s.x / 2, c.y + y * s.y / 2, c.z + z * s.z / 2);
                Quad(P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), P(1, -1, 1), f.O, new Vector2(c.x - s.x / 2, c.y - s.y / 2));
                Quad(P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), P(-1, -1, -1), -f.O);
                Quad(P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), P(1, -1, -1), f.R, new Vector2(c.z, c.y - s.y / 2));
                Quad(P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), P(-1, -1, 1), -f.R, new Vector2(c.z, c.y - s.y / 2));
                Quad(P(-1, 1, 1), P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), f.U, new Vector2(c.x, c.z));
                if (bottom) Quad(P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), -f.U);
            }
            /// An outline in the frame's x-y plane (any orientation), extruded from z0 to z1 (z1 > z0, toward the viewer).
            public void Extrude(Frame f, IList<Vector2> outline, float z0, float z1, bool back = false, bool sides = true)
            {
                int k = outline.Count; float area = 0;
                for (int i = 0; i < k; i++) { var a = outline[i]; var b = outline[(i + 1) % k]; area += a.x * b.y - b.x * a.y; }
                float sgn = Mathf.Sign(area);
                Vector3 Pt(Vector2 p, float z) => f.P(p.x, p.y, z);
                // front cap (fan from the centroid: the outlines used are star-shaped about it)
                var mid = Vector2.zero; foreach (var p in outline) mid += p; mid /= k;
                for (int i = 0; i < k; i++)
                {
                    var a = outline[i]; var b = outline[(i + 1) % k];
                    Tri(Pt(mid, z1), Pt(a, z1), Pt(b, z1), f.O, mid, a, b);
                    if (back) Tri(Pt(mid, z0), Pt(b, z0), Pt(a, z0), -f.O, mid, b, a);
                    if (!sides) continue;
                    var e = b - a; var nn2 = new Vector2(e.y, -e.x).normalized * sgn;
                    var nn = (f.R * nn2.x + f.U * nn2.y).normalized;
                    Quad(Pt(a, z0), Pt(a, z1), Pt(b, z1), Pt(b, z0), nn);
                }
            }
            /// A smooth ball (vertex normals from its centre), radius `r` scaled by `s`, in the current colour.
            public void Ball(Vector3 c, Vector3 s, int subdiv = 2, float wobble = 0, int seed = 0, Func<Vector3, Color> paint = null)
            {
                var (sv, st) = Sphere(subdiv); int b0 = v.Count;
                foreach (var d in sv)
                {
                    float k = 1 + wobble * (Mathf.Sin(d.x * 5.1f + seed) * Mathf.Sin(d.y * 4.3f + seed * .7f) * Mathf.Sin(d.z * 4.7f + seed * 1.3f));
                    var p = c + Vector3.Scale(d * k, s);
                    if (paint != null) colour = paint(p);
                    Add(p, new Vector3(d.x / s.x, d.y / s.y, d.z / s.z).normalized, new Vector2(d.x, d.z));
                }
                foreach (var i in st) t.Add(b0 + i);
            }
            /// A unit sphere from a subdivided octahedron with shared vertices (smooth, a fifth of the flat ball's vertices).
            static readonly Dictionary<int, (Vector3[], int[])> spheres = new Dictionary<int, (Vector3[], int[])>();
            static (Vector3[] v, int[] t) Sphere(int subdiv)
            {
                if (spheres.TryGetValue(subdiv, out var got)) return got;
                var verts = new List<Vector3> { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
                var tris = new List<int> { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
                for (int s = 0; s < subdiv; s++)
                {
                    var next = new List<int>(); var mid = new Dictionary<long, int>();
                    int Mid(int a, int b)
                    {
                        long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                        if (mid.TryGetValue(k, out var i)) return i;
                        verts.Add(((verts[a] + verts[b]) * .5f).normalized); return mid[k] = verts.Count - 1;
                    }
                    for (int i = 0; i < tris.Count; i += 3)
                    {
                        int a = tris[i], b = tris[i + 1], c = tris[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                        next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                    }
                    tris = next;
                }
                return spheres[subdiv] = (verts.ToArray(), tris.ToArray());
            }
            /// A lathe (profile of (radius, height) from bottom to top) at `c`, smooth normals; UVs (around, along) in metres.
            public void Lathe(Vector3 c, IList<Vector2> profile, int seg = 16, Vector3? scale = null)
            {
                var sc = scale ?? Vector3.one;
                var key = new System.Text.StringBuilder("mb lathe ").Append(seg);
                foreach (var q in profile) key.Append(' ').Append(q.x.ToString("F4")).Append(',').Append(q.y.ToString("F4"));
                var m = HubShapes.Lathe(key.ToString(), profile, seg);
                var mv = m.vertices; var mn = m.normals; var mt = m.triangles; var mu = m.uv; int b = v.Count;
                for (int i = 0; i < mv.Length; i++) Add(c + Vector3.Scale(mv[i], sc), new Vector3(mn[i].x / sc.x, mn[i].y / sc.y, mn[i].z / sc.z).normalized, mu[i]);
                foreach (var i in mt) t.Add(b + i);
            }
            /// Another mesh's triangles, placed by `m`.
            public void Mesh(Mesh mesh, Matrix4x4 m)
            {
                var mv = mesh.vertices; var mn = mesh.normals; var mt = mesh.triangles; var mu = mesh.uv; int b = v.Count;
                for (int i = 0; i < mv.Length; i++) Add(m.MultiplyPoint3x4(mv[i]), m.MultiplyVector(mn[i]).normalized, mu.Length > i ? mu[i] : Vector2.zero);
                foreach (var i in mt) t.Add(b + i);
            }
            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetColors(col); m.SetTriangles(t, 0);
                HubShapes.FixWinding(m); m.RecalculateBounds(); return m;
            }
        }

        public static GameObject Put(string name, Transform parent, Mesh mesh, Material mat, bool shadows = true)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; if (!shadows) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
        public static GameObject Put(string name, Transform parent, Mesh mesh, Material mat, Vector3 pos, Quaternion? rot = null, Vector3? scale = null, bool shadows = true)
        {
            var go = Put(name, parent, mesh, mat, shadows); go.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
            if (scale.HasValue) go.transform.localScale = scale.Value; return go;
        }
        /// Material -> builder: props add their parts here and Flush makes one renderer per material (fewer objects to build and batch).
        public sealed class Batch
        {
            readonly Dictionary<Material, MB> parts = new Dictionary<Material, MB>(); readonly List<Material> order = new List<Material>();
            public readonly HashSet<Material> noShadow = new HashSet<Material>();
            /// False: nothing in this batch casts a shadow (the PLAY Hall's shell, so the sun reaches the grounds outside its arches).
            public bool shadows = true;
            public MB this[Material m] { get { if (!parts.TryGetValue(m, out var b)) { parts[m] = b = new MB(); order.Add(m); } return b; } }
            public void Flush(Transform parent, string name)
            {
                foreach (var m in order) { var b = parts[m]; if (b.Count == 0) continue; Put(name + " " + m.name, parent, b.Build(name + " " + m.name), m, shadows && !noShadow.Contains(m)); }
                parts.Clear(); order.Clear();
            }
        }
        public static void BoxCollider(Transform parent, Vector3 centre, Vector3 size, Quaternion rot)
        {
            var go = new GameObject("Collider"); go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(centre, rot);
            go.AddComponent<BoxCollider>().size = size;
        }

        // ================================================================== signs
        /// The club's sign: a navy board with a thin gold frame and gold lettering (optional icon left of the word), facing
        /// `facing` (toward the reader). `width` 0 = fit the text.
        public static void SignBoard(Transform parent, string text, string icon, Vector3 centre, Vector3 facing, float capHeight, float width = 0, float heightK = 1.75f, bool arch = false)
        {
            var f = new Frame(centre, facing); f.origin = centre;
            float textW = TextWidth(text, capHeight), iconW = icon != null ? capHeight * 1.35f : 0, gap = icon != null ? capHeight * .45f : 0;
            float w = width > 0 ? width : textW + iconW + gap + capHeight * 1.6f, h = capHeight * heightK;
            var b = new MB();
            if (arch) b.Extrude(f, ArchOutline(w * .5f + .07f, -h * .5f - .07f, h * .5f + .07f, w * .2f), -.11f, -.01f);
            else b.Box(f, new Vector3(0, 0, -.06f), new Vector3(w + .14f, h + .14f, .1f));
            Put("Sign frame", parent, b.Build("sign frame " + text), Lit(Gold, .62f));
            var p = new MB();
            if (arch) p.Extrude(f, ArchOutline(w * .5f, -h * .5f, h * .5f, w * .2f - .05f), -.07f, .005f);
            else p.Box(f, new Vector3(0, 0, -.03f), new Vector3(w, h, .07f));
            Put("Sign board", parent, p.Build("sign board " + text), Lit(Navy, .45f));
            var go = new GameObject("Sign " + text); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre + f.O * .012f, Quaternion.LookRotation(-f.O, Vector3.up));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(w * 100, h * 100); rt.localScale = Vector3.one / 100;
            float span = textW + iconW + gap, x0 = -span / 2;
            if (icon != null)
            {
                var img = new GameObject("Icon").AddComponent<RawImage>(); img.transform.SetParent(go.transform, false); img.texture = HubIcons.Get(icon); img.color = GoldText; img.raycastTarget = false;
                var irt = img.rectTransform; irt.sizeDelta = new Vector2(iconW * 100, iconW * 100); irt.anchoredPosition = new Vector2((x0 + iconW / 2) * 100, 0);
                var sh = img.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(.05f, .06f, .15f, .7f); sh.effectDistance = new Vector2(1.5f, -2f);
            }
            var t = new GameObject("Text").AddComponent<Text>(); t.transform.SetParent(go.transform, false);
            t.font = HubKit.Font; t.text = text; t.fontSize = 100; t.color = GoldText; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = t.rectTransform; trt.sizeDelta = new Vector2(textW * 100 / (capHeight / .7f), 140); trt.localScale = Vector3.one * (capHeight / .7f);
            trt.anchoredPosition = new Vector2((x0 + iconW + gap + textW / 2) * 100, 0);
            var shadow = t.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.04f, .05f, .14f, .8f); shadow.effectDistance = new Vector2(2.2f, -2.8f);
        }
        public static readonly Color GoldText = C("F3CD6A");
        /// A tall sign with its letters stacked (PLAY down the clock tower): navy board, gold frame, gold letters; `centre` is its middle.
        public static void SignBoardVertical(Transform parent, string text, Vector3 centre, Vector3 facing, float capHeight, float width)
        {
            var f = new Frame(centre, facing); f.origin = centre;
            float step = capHeight * 1.32f, h = step * text.Length + capHeight * .7f;
            var b = new MB(); b.Box(f, new Vector3(0, 0, -.06f), new Vector3(width + .14f, h + .14f, .1f));
            Put("Sign frame", parent, b.Build("vertical sign frame " + text), Lit(Gold, .62f));
            var p = new MB(); p.Box(f, new Vector3(0, 0, -.03f), new Vector3(width, h, .07f));
            Put("Sign board", parent, p.Build("vertical sign board " + text), Lit(Navy, .45f));
            var go = new GameObject("Sign " + text); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre + f.O * .012f, Quaternion.LookRotation(-f.O, Vector3.up));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(width * 100, h * 100); rt.localScale = Vector3.one / 100;
            for (int i = 0; i < text.Length; i++)
            {
                var t = new GameObject("Letter " + text[i]).AddComponent<Text>(); t.transform.SetParent(go.transform, false);
                t.font = HubKit.Font; t.text = text[i].ToString(); t.fontSize = 100; t.color = GoldText; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
                var trt = t.rectTransform; trt.sizeDelta = new Vector2(140, 140); trt.localScale = Vector3.one * (capHeight / .7f);
                trt.anchoredPosition = new Vector2(0, (h / 2 - capHeight * .35f - step * (i + .5f)) * 100);
                var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(.04f, .05f, .14f, .8f); sh.effectDistance = new Vector2(2.2f, -2.8f);
            }
        }
        /// The PLAY Hall's arch signs: an arched navy board, gold frame, the icon above the word.
        public static void SignBoardStacked(Transform parent, string text, string icon, Vector3 centre, Vector3 facing, float capHeight, float width)
        {
            var f = new Frame(centre, facing); f.origin = centre;
            float h = capHeight * 3.2f, rise = width * .16f;
            var b = new MB(); b.Extrude(f, ArchOutline(width * .5f + .06f, -h * .5f - .06f, h * .5f - rise + .06f, rise), -.1f, -.01f);
            Put("Sign frame", parent, b.Build("stacked sign frame " + text), Lit(Gold, .62f));
            var p = new MB(); p.Extrude(f, ArchOutline(width * .5f, -h * .5f, h * .5f - rise, rise - .03f), -.07f, .005f);
            Put("Sign board", parent, p.Build("stacked sign board " + text), Lit(Navy, .45f));
            var go = new GameObject("Sign " + text); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre + f.O * .012f, Quaternion.LookRotation(-f.O, Vector3.up));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(width * 100, h * 100); rt.localScale = Vector3.one / 100;
            if (icon != null)
            {
                float iw = capHeight * 1.5f;
                var img = new GameObject("Icon").AddComponent<RawImage>(); img.transform.SetParent(go.transform, false); img.texture = HubIcons.Get(icon); img.color = GoldText; img.raycastTarget = false;
                var irt = img.rectTransform; irt.sizeDelta = new Vector2(iw * 100, iw * 100); irt.anchoredPosition = new Vector2(0, h * .17f * 100);
            }
            var t = new GameObject("Text").AddComponent<Text>(); t.transform.SetParent(go.transform, false);
            t.font = HubKit.Font; t.text = text; t.fontSize = 100; t.color = GoldText; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            float tw = TextWidth(text, capHeight);
            var trt = t.rectTransform; trt.sizeDelta = new Vector2(tw * 100 / (capHeight / .7f), 140); trt.localScale = Vector3.one * (capHeight / .7f);
            trt.anchoredPosition = new Vector2(0, -h * .24f * 100);
            var shadow = t.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.04f, .05f, .14f, .8f); shadow.effectDistance = new Vector2(2.2f, -2.8f);
        }
        /// The word's width in metres at `capHeight` (Rubik caps are ~0.7 em).
        public static float TextWidth(string text, float capHeight)
        {
            var font = HubKit.Font; font.RequestCharactersInTexture(text, 100, FontStyle.Normal);
            float w = 0; foreach (var ch in text) w += font.GetCharacterInfo(ch, out var info, 100) ? info.advance : 60;
            return w * capHeight / 70f;
        }
        /// A round-topped outline: a rectangle of half width hw from y = bottom up to the spring, then an arch `rise` high
        /// (flat when 0).
        public static List<Vector2> ArchOutline(float hw, float bottom, float spring, float rise, int steps = 12)
        {
            var o = new List<Vector2> { new Vector2(-hw, bottom), new Vector2(hw, bottom) };
            if (rise <= 0) { o.Add(new Vector2(hw, spring)); o.Add(new Vector2(-hw, spring)); return o; }
            for (int i = 0; i <= steps; i++) { float a = i * Mathf.PI / steps; o.Add(new Vector2(Mathf.Cos(a) * hw, spring + Mathf.Sin(a) * rise)); }
            return o;
        }

        // ================================================================== architecture
        /// A hip roof over x0..x1 (frame x) and z0..z1 (frame z), eaves at y, ridge `rise` higher, slate on top and a cream fascia.
        public static void HipRoof(Batch b, Frame f, float x0, float x1, float z0, float z1, float y, float rise, float fascia = .22f)
        {
            float w = x1 - x0, d = z1 - z0, inset = Mathf.Min(w, d) / 2;
            float rx0 = x0 + inset, rx1 = x1 - inset, rz0 = z0 + inset, rz1 = z1 - inset;
            var slate = b[LitTex(SlateTex, SlateTint, .25f, 1.6f)];
            Vector3 P(float x, float yy, float z) => f.P(x, yy, z);
            Vector3 top0 = P(rx0, y + rise, (rz0 + rz1) / 2), top1 = P(rx1, y + rise, (rz0 + rz1) / 2);
            if (w < d) { top0 = P((rx0 + rx1) / 2, y + rise, rz0); top1 = P((rx0 + rx1) / 2, y + rise, rz1); }
            Vector3 a = P(x0, y, z1), bb = P(x1, y, z1), c = P(x1, y, z0), dd = P(x0, y, z0);
            void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                var nn = Vector3.Cross(p1 - p0, p3 - p0).normalized; if (nn.y < 0) nn = -nn;
                // slate rows run along the eave: u along the eave, v up the slope
                var along = (p3 - p0).normalized; var up = Vector3.Cross(nn, along).normalized; if (Vector3.Dot(up, Vector3.up) < 0) up = -up;
                Vector2 U(Vector3 p) => new Vector2(Vector3.Dot(p - p0, along), Vector3.Dot(p - p0, up));
                slate.Tri(p0, p1, p2, nn, U(p0), U(p1), U(p2)); slate.Tri(p0, p2, p3, nn, U(p0), U(p2), U(p3));
            }
            if (w >= d) { Face(a, top0, top1, bb); Face(c, top1, top0, dd); Face(bb, top1, top1, c); Face(dd, top0, top0, a); }
            else { Face(dd, top0, top0, c); Face(a, top1, top1, bb); Face(c, top0, top1, bb); Face(a, top1, top0, dd); }
            // ridge cap and the cream fascia under the eaves
            var cap = b[Lit(C("2A3766"), .4f)];
            var mid = (top0 + top1) / 2; float len = (top1 - top0).magnitude;
            if (len > .05f) cap.Box(new Frame(mid - Vector3.up * .06f, w >= d ? f.O : f.R), Vector3.zero, new Vector3(len + .2f, .14f, .16f));
            var trim = b[Lit(StoneLight, .3f)];
            trim.Box(f, new Vector3((x0 + x1) / 2, y - fascia / 2, z1 - .06f), new Vector3(w, fascia, .12f));
            trim.Box(f, new Vector3((x0 + x1) / 2, y - fascia / 2, z0 + .06f), new Vector3(w, fascia, .12f));
            trim.Box(f, new Vector3(x0 + .06f, y - fascia / 2, (z0 + z1) / 2), new Vector3(.12f, fascia, d));
            trim.Box(f, new Vector3(x1 - .06f, y - fascia / 2, (z0 + z1) / 2), new Vector3(.12f, fascia, d));
        }
        /// A dormer on a roof slope: a small cream box with a lit window and its own little gable roof.
        public static void Dormer(Batch b, Frame f, float x, float z, float y, float w = 1.2f, float h = 1.1f)
        {
            var stone = b[LitTex(StoneTex, StoneLight, .25f, 1.2f)];
            stone.Box(f, new Vector3(x, y + h / 2, z - .7f), new Vector3(w, h, 1.4f));
            b[Lit(StoneLight, .35f)].Box(f, new Vector3(x, y + h * .45f, z + .02f), new Vector3(w * .62f + .12f, h * .62f + .12f, .06f));
            b[Glow(WarmGlow * .75f)].Box(f, new Vector3(x, y + h * .45f, z + .05f), new Vector3(w * .62f, h * .62f, .02f));
            b[Lit(StoneLight, .35f)].Box(f, new Vector3(x, y + h * .45f, z + .07f), new Vector3(.05f, h * .62f, .02f));
            // the gable roof: two slopes meeting over the dormer's middle
            var slate = b[LitTex(SlateTex, SlateTint, .25f, 1.6f)];
            float ov = .12f, ry = y + h + w * .38f;
            foreach (int s in new[] { -1, 1 })
            {
                var p0 = f.P(x + s * (w / 2 + ov), y + h - .05f, z + ov); var p1 = f.P(x, ry, z + ov); var p2 = f.P(x, ry, z - 1.4f); var p3 = f.P(x + s * (w / 2 + ov), y + h - .05f, z - 1.4f);
                var nn = Vector3.Cross(p1 - p0, p3 - p0).normalized; if (nn.y < 0) nn = -nn;
                slate.Quad(p0, p1, p2, p3, nn);
            }
            // the gable's face
            stone.Tri(f.P(x - w / 2, y + h, z), f.P(x, ry - .04f, z), f.P(x + w / 2, y + h, z), f.O, new Vector2(-w / 2, 0), new Vector2(0, .4f), new Vector2(w / 2, 0));
        }
        /// A lit window: cream surround, glowing glass with glazing bars; arched or square.
        /// (The frame's origin is the window's foot, in the wall plane.)
        public static void Window(Batch b, Frame f, float sill, float w, float h, bool arched, Material inside = null, float surround = .14f, Color? frame = null)
        {
            var trim = b[Lit(frame ?? StoneLight, .35f)];
            var glass = b[inside ?? Glow(WarmGlow * .8f)];
            float rise = arched ? w / 2 : 0, spring = sill + h - rise;
            trim.Extrude(f, ArchOutline(w / 2 + surround, sill - surround, spring + (arched ? 0 : surround), arched ? rise + surround : 0, 10), 0f, .09f);
            glass.Extrude(f, ArchOutline(w / 2, sill, spring, rise, 10), 0f, .095f, false, false);
            trim.Box(f, new Vector3(0, (sill + spring + rise * .7f) / 2, .1f), new Vector3(.045f, spring + rise * .7f - sill, .02f));
            trim.Box(f, new Vector3(0, sill + (spring - sill) * .55f, .1f), new Vector3(w, .045f, .02f));
            trim.Box(f, new Vector3(0, sill - surround - .04f, .1f), new Vector3(w + surround * 2 + .14f, .08f, .2f));
        }
        /// A wall lantern (bracket + glowing glass box with a cap), on a wall facing f.O.
        public static void WallLantern(Batch b, Frame f, float x, float y)
        {
            var iron = b[Lit(Iron, .55f)];
            iron.Box(f, new Vector3(x, y + .1f, .06f), new Vector3(.12f, .18f, .12f));
            iron.Box(f, new Vector3(x, y + .16f, .22f), new Vector3(.05f, .05f, .32f));
            iron.Box(f, new Vector3(x, y - .02f, .42f), new Vector3(.3f, .06f, .3f));
            iron.Box(f, new Vector3(x, y + .44f, .42f), new Vector3(.34f, .06f, .34f));
            iron.Lathe(f.P(x, y + .47f, .42f), new[] { new Vector2(.17f, 0), new Vector2(.02f, .16f), new Vector2(0, .17f) }, 8);
            b[Glow(LanternGlow)].Box(f, new Vector3(x, y + .21f, .42f), new Vector3(.24f, .38f, .24f));
            foreach (int s in new[] { -1, 1 }) foreach (int k in new[] { -1, 1 }) iron.Box(f, new Vector3(x + s * .125f, y + .21f, .42f + k * .125f), new Vector3(.025f, .42f, .025f));
        }
        /// A Victorian lamp post: dark iron, a fluted post, a lantern with warm glass, a little cap.
        public static void LampPost(Batch b, Vector3 at, float height = 2.9f)
        {
            var iron = b[Lit(Iron, .55f)];
            iron.Lathe(at, new[] { new Vector2(.2f, 0), new Vector2(.2f, .25f), new Vector2(.09f, .45f), new Vector2(.06f, height - .55f), new Vector2(.12f, height - .45f), new Vector2(0, height - .4f) }, 10);
            var lamp = at + Vector3.up * (height - .42f);
            iron.Lathe(lamp, new[] { new Vector2(0, 0), new Vector2(.17f, .02f), new Vector2(.19f, .06f), new Vector2(0, .07f) }, 8);
            b[Glow(LanternGlow)].Lathe(lamp + Vector3.up * .06f, new[] { new Vector2(0, 0), new Vector2(.15f, 0), new Vector2(.19f, .42f), new Vector2(0, .42f) }, 6);
            // the lantern's frame: a bar up each corner of the glass
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var p0 = lamp + Vector3.up * .06f + d * .155f; var p1 = lamp + Vector3.up * .48f + d * .195f;
                iron.Box(new Frame((p0 + p1) / 2, d), Vector3.zero, new Vector3(.03f, .43f, .03f));
            }
            iron.Lathe(lamp + Vector3.up * .47f, new[] { new Vector2(.26f, 0), new Vector2(.04f, .2f), new Vector2(.05f, .28f), new Vector2(0, .3f) }, 8);
            var brass = b[Lit(Brass, .7f)]; brass.Lathe(lamp + Vector3.up * .74f, new[] { new Vector2(0, 0), new Vector2(.05f, .03f), new Vector2(0, .1f) }, 6);
        }
        /// A park bench: wooden slats on dark iron ends. `centre` on the ground, the sitter faces `facing`.
        public static void Bench(Batch b, Vector3 centre, Vector3 facing, float width = 1.8f, float seat = .44f)
        {
            var f = new Frame(centre, facing); var wood = b[LitTex(PlankTex, C("B07440"), .35f, 1f)]; var iron = b[Lit(Iron, .5f)];
            for (int i = 0; i < 4; i++) wood.Box(f, new Vector3(0, seat, .2f - i * .13f), new Vector3(width, .045f, .11f));
            for (int i = 0; i < 3; i++) wood.Box(f, new Vector3(0, .62f + i * .14f, -.3f - i * .02f), new Vector3(width, .1f, .04f));
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * (width / 2 - .1f);
                iron.Box(f, new Vector3(x, seat / 2 - .01f, .18f), new Vector3(.06f, seat - .02f, .06f)); iron.Box(f, new Vector3(x, .45f, -.3f), new Vector3(.06f, .9f, .06f));
                iron.Box(f, new Vector3(x, seat - .04f, -.05f), new Vector3(.06f, .05f, .55f)); iron.Box(f, new Vector3(x, .62f, .05f), new Vector3(.06f, .05f, .4f));
                iron.Box(f, new Vector3(x, .66f, .2f), new Vector3(.06f, .3f, .05f));
            }
        }

        // ================================================================== garden
        /// A clipped box hedge from a to b (ground points), `h` tall, `d` deep; optional roses tucked into its face.
        public static void Hedge(Batch b, Vector3 a, Vector3 c, float h = .8f, float d = .7f, int roses = 0, int seed = 0)
        {
            var dir = c - a; dir.y = 0; float len = dir.magnitude; if (len < .05f) return;
            var f = new Frame((a + c) / 2, Vector3.Cross(dir / len, Vector3.up));
            var m = b[HedgeMat]; m.colour = new Color(HedgeGreen.r, HedgeGreen.g, HedgeGreen.b, 0);
            m.Box(f, new Vector3(0, h / 2 - .02f, 0), new Vector3(len, h, d));
            m.colour = new Color(LeafDark.r, LeafDark.g, LeafDark.b, 0);
            m.Box(f, new Vector3(0, .06f, 0), new Vector3(len + .02f, .12f, d + .04f));
            if (roses > 0) RoseSpray(b, f, len, h, d, roses, seed);
        }
        static readonly Color[] RoseColours = { Rose, RosePale, RoseRed, C("FFF1F3"), Rose };
        static void RoseSpray(Batch b, Frame f, float len, float h, float d, int n, int seed)
        {
            var m = b[Foliage];
            for (int i = 0; i < n; i++)
            {
                float x = (H(i, seed, 20) - .5f) * len * .92f, y = h * (.45f + H(i, seed, 21) * .5f); int side = H(i, seed, 22) > .5f ? 1 : -1;
                var col = RoseColours[(int)(H(i, seed, 23) * RoseColours.Length) % RoseColours.Length]; m.colour = new Color(col.r, col.g, col.b, .05f);
                m.Ball(f.P(x, y, side * d / 2), Vector3.one * (.07f + H(i, seed, 24) * .04f), 1);
            }
        }
        /// A rose bush: a leafy mound dotted with flowers (one colour family per bush).
        public static void RoseBush(Batch b, Vector3 at, float s, int seed)
        {
            var m = b[Foliage];
            m.colour = new Color(Leaf.r * .95f, Leaf.g * .95f, Leaf.b, .12f);
            m.Ball(at + Vector3.up * .32f * s, new Vector3(.5f, .38f, .5f) * s, 2, .12f, seed);
            m.colour = new Color(LeafDark.r, LeafDark.g, LeafDark.b, .1f);
            m.Ball(at + new Vector3(.25f, .22f, -.1f) * s, new Vector3(.3f, .24f, .3f) * s, 1, .1f, seed + 3);
            var col = RoseColours[seed % 3];
            for (int i = 0; i < 9; i++)
            {
                float a = H(i, seed, 30) * Mathf.PI * 2, r = .2f + H(i, seed, 31) * .26f, y = .3f + H(i, seed, 32) * .32f;
                var c2 = H(i, seed, 33) > .8f ? RoseColours[3] : col; m.colour = new Color(c2.r, c2.g, c2.b, .2f);
                m.Ball(at + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r) * s, Vector3.one * (.075f + H(i, seed, 34) * .03f) * s, 1);
            }
        }
        /// A round deciduous tree: a trunk and a crown of soft lumps, darker underneath, that sways in the wind.
        public static void Tree(Batch b, Vector3 at, float s, int seed, Color? tint = null)
        {
            var bark = b[Lit(C("6E4A30"), .2f)];
            bark.Lathe(at, new[] { new Vector2(.24f, 0), new Vector2(.17f, .4f), new Vector2(.13f, 2.3f), new Vector2(.05f, 2.9f), new Vector2(0, 2.95f) }, 8, Vector3.one * s);
            var m = b[Foliage]; var baseC = tint ?? Leaf;
            var crown = at + Vector3.up * 3.3f * s;
            int lumps = 6 + seed % 3;
            for (int i = 0; i < lumps; i++)
            {
                float a = i * 2.4f + seed, r = i == 0 ? 0 : (.55f + H(i, seed, 40) * .45f) * s, y = i == 0 ? 0 : (H(i, seed, 41) - .35f) * 1.1f * s;
                float k = (i == 0 ? 1.35f : .8f + H(i, seed, 42) * .35f) * s;
                var c = crown + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                m.Ball(c, new Vector3(1.1f, .95f, 1.1f) * k, 2, .1f, seed + i, p =>
                {
                    float up = Mathf.InverseLerp(crown.y - 1.4f * s, crown.y + 1.4f * s, p.y);
                    var col = Color.Lerp(LeafDark, Color.Lerp(baseC, C("5E9A3A"), .35f), up) * (.92f + H(i, seed, 43) * .14f);
                    col.a = Mathf.Clamp01((p.y - at.y) / (4.5f * s)) * .8f; return col;
                });
            }
        }
        /// A clipped topiary in a stone urn: a cone (true) or a ball.
        public static void Topiary(Batch b, Vector3 at, float s, bool cone)
        {
            var urn = b[Lit(StoneLight, .3f)];
            urn.Lathe(at, new[] { new Vector2(.22f, 0), new Vector2(.3f, .08f), new Vector2(.2f, .2f), new Vector2(.36f, .55f), new Vector2(.4f, .62f), new Vector2(0, .62f) }, 14, Vector3.one * s);
            var m = b[HedgeMat]; m.colour = new Color(HedgeGreen.r, HedgeGreen.g, HedgeGreen.b, 0);
            if (cone) m.Lathe(at + Vector3.up * .6f * s, new[] { new Vector2(.42f, 0), new Vector2(.4f, .15f), new Vector2(.06f, 1.65f), new Vector2(0, 1.7f) }, 14, Vector3.one * s);
            else m.Ball(at + Vector3.up * 1.05f * s, Vector3.one * .5f * s, 2);
        }
        /// A white picket fence from a to c (ground points).
        public static void PicketFence(Batch b, Vector3 a, Vector3 c, float h = .9f)
        {
            var dir = c - a; dir.y = 0; float len = dir.magnitude; if (len < .1f) return;
            var f = new Frame((a + c) / 2, Vector3.Cross(dir / len, Vector3.up)); var paint = b[Lit(White, .35f)];
            int n = Mathf.Max(2, Mathf.RoundToInt(len / .16f));
            for (int i = 0; i <= n; i++)
            {
                float x = -len / 2 + len * i / n;
                paint.Extrude(f, new List<Vector2> { new Vector2(x - .035f, 0), new Vector2(x + .035f, 0), new Vector2(x + .035f, h - .06f), new Vector2(x, h), new Vector2(x - .035f, h - .06f) }, -.015f, .015f);
            }
            paint.Box(f, new Vector3(0, h * .3f, -.04f), new Vector3(len, .06f, .03f)); paint.Box(f, new Vector3(0, h * .72f, -.04f), new Vector3(len, .06f, .03f));
            for (int i = 0; i <= Mathf.CeilToInt(len / 2.2f); i++) paint.Box(f, new Vector3(-len / 2 + len * i / Mathf.CeilToInt(len / 2.2f), h * .52f, -.05f), new Vector3(.1f, h * 1.05f, .1f));
        }
        /// A round bed of roses inside a low stone kerb with a clipped hedge ring.
        public static void RoseBed(Batch b, Vector3 at, float r, int seed)
        {
            b[Lit(StoneLight, .3f)].Lathe(at, new[] { new Vector2(r, 0), new Vector2(r + .02f, .22f), new Vector2(r - .12f, .26f), new Vector2(r - .16f, .2f) }, 32);
            b[Lit(C("6B4E38"), .05f)].Lathe(at, new[] { new Vector2(r - .15f, .2f), new Vector2(0, .24f) }, 24);
            var m = b[HedgeMat]; m.colour = new Color(HedgeGreen.r, HedgeGreen.g, HedgeGreen.b, 0);
            m.Lathe(at, new[] { new Vector2(r - .16f, .2f), new Vector2(r - .14f, .5f), new Vector2(r - .3f, .58f), new Vector2(r - .44f, .5f), new Vector2(r - .44f, .2f) }, 28);
            int n = Mathf.Max(3, Mathf.RoundToInt(r * r * 2.2f));
            for (int i = 0; i < n; i++)
            {
                float a = i * 2.39996f + seed, rr = Mathf.Sqrt((i + .5f) / n) * (r - .75f);
                RoseBush(b, at + new Vector3(Mathf.Cos(a) * rr, .2f, Mathf.Sin(a) * rr), .9f + H(i, seed, 50) * .35f, seed + i);
            }
        }
        /// A tiered stone fountain: a round basin of sparkling water, a column, a bowl, a small jet.
        public static void Fountain(Batch b, Vector3 at, float r)
        {
            var stone = b[Lit(StoneLight, .35f)];
            stone.Lathe(at, new[] { new Vector2(r, 0), new Vector2(r + .05f, .45f), new Vector2(r + .1f, .5f), new Vector2(r - .12f, .55f), new Vector2(r - .16f, .3f), new Vector2(0, .3f) }, 40);
            b[Water("fountain", C("4F8FB8"), C("CFE3EE"), null, .55f, 3.2f, 2.4f, 9f, 1.6f)].Lathe(at + Vector3.up * .44f, new[] { new Vector2(r - .15f, 0), new Vector2(0, 0) }, 32);
            stone.Lathe(at + Vector3.up * .3f, new[] { new Vector2(.22f, 0), new Vector2(.2f, .3f), new Vector2(.12f, .9f), new Vector2(.16f, 1.1f), new Vector2(0, 1.1f) }, 16);
            float br = r * .42f;
            stone.Lathe(at + Vector3.up * 1.3f, new[] { new Vector2(0, -.12f), new Vector2(.18f, -.12f), new Vector2(br, .12f), new Vector2(br + .05f, .2f), new Vector2(br - .06f, .22f), new Vector2(0, .14f) }, 24);
            b[Water("fountain bowl", C("5D9CC2"), C("D7E9F2"), null, .8f, 5f, 2.8f, 14f, 2.2f)].Lathe(at + Vector3.up * 1.48f, new[] { new Vector2(br - .07f, 0), new Vector2(0, 0) }, 20);
            stone.Lathe(at + Vector3.up * 1.48f, new[] { new Vector2(.08f, 0), new Vector2(.06f, .35f), new Vector2(.11f, .45f), new Vector2(0, .5f) }, 10);
            var jet = b[Glow(new Color(1.25f, 1.45f, 1.6f))]; b.noShadow.Add(Glow(new Color(1.25f, 1.45f, 1.6f)));
            jet.Lathe(at + Vector3.up * 1.95f, new[] { new Vector2(.05f, 0), new Vector2(.035f, .45f), new Vector2(.1f, .6f), new Vector2(.02f, .64f), new Vector2(0, .64f) }, 10);
            // little falls from the bowl's lip into the basin
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                jet.Box(new Frame(at + d * (br + .02f) + Vector3.up * 1.05f, d), Vector3.zero, new Vector3(.1f, .5f, .02f));
            }
        }
        /// A patio set: a white parasol over a round table and two (or three) white chairs.
        public static void Parasol(Batch b, Vector3 at, Vector3 facing, int chairs = 2)
        {
            var white = b[Lit(White, .4f)]; var cloth = b[Lit(C("F7F1E4"), .2f)];
            white.Lathe(at, new[] { new Vector2(.06f, 0), new Vector2(.04f, 2.35f), new Vector2(0, 2.4f) }, 8);
            cloth.Lathe(at + Vector3.up * 2.0f, new[] { new Vector2(1.45f, 0), new Vector2(1.4f, .06f), new Vector2(.06f, .55f), new Vector2(0, .58f) }, 12);
            cloth.Lathe(at + Vector3.up * 1.88f, new[] { new Vector2(1.45f, 0), new Vector2(1.46f, .14f), new Vector2(1.4f, .14f) }, 12);
            b[Lit(C("6A4630"), .4f)].Lathe(at, new[] { new Vector2(.25f, 0), new Vector2(.05f, .1f), new Vector2(.05f, .68f), new Vector2(.5f, .7f), new Vector2(.5f, .76f), new Vector2(0, .76f) }, 16);
            var f0 = new Frame(at, facing);
            for (int i = 0; i < chairs; i++)
            {
                float a = (i - (chairs - 1) / 2f) * 1.9f + Mathf.PI; var d = f0.D(Mathf.Sin(a), 0, Mathf.Cos(a));
                var f = new Frame(at + d * .95f, -d);
                white.Box(f, new Vector3(0, .42f, 0), new Vector3(.55f, .06f, .55f));
                white.Box(f, new Vector3(0, .78f, -.3f), new Vector3(.55f, .7f, .06f));
                foreach (int s in new[] { -1, 1 }) { white.Box(f, new Vector3(s * .29f, .55f, 0), new Vector3(.06f, .06f, .6f)); white.Box(f, new Vector3(s * .25f, .21f, .22f), new Vector3(.05f, .42f, .05f)); white.Box(f, new Vector3(s * .25f, .21f, -.24f), new Vector3(.05f, .42f, .05f)); }
            }
        }
        /// A gold trophy (lathe), `s` = height scale.
        public static void Trophy(Batch b, Vector3 at, float s)
        {
            b[Lit(Gold, .8f)].Lathe(at, new[] { new Vector2(.16f, 0), new Vector2(.16f, .08f), new Vector2(.06f, .12f), new Vector2(.04f, .3f), new Vector2(.08f, .34f), new Vector2(.2f, .46f), new Vector2(.22f, .7f), new Vector2(.2f, .72f), new Vector2(0, .5f) }, 16, Vector3.one * s);
            b[Lit(C("3A2A1E"), .4f)].Box(new Frame(at - Vector3.up * .08f * s, Vector3.forward), Vector3.zero, new Vector3(.36f, .16f, .36f) * s);
        }
        /// A hanging banner on a pole with a gold finial: navy cloth, gold border, a gold icon.
        public static void Banner(Batch b, Transform parent, Vector3 at, Vector3 facing, string icon, float h = 2.6f)
        {
            var f = new Frame(at, facing);
            b[Lit(Iron, .5f)].Lathe(at, new[] { new Vector2(.07f, 0), new Vector2(.05f, h + 1.4f), new Vector2(0, h + 1.42f) }, 8);
            b[Lit(Gold, .75f)].Ball(at + Vector3.up * (h + 1.55f), Vector3.one * .13f, 1);
            b[Lit(Gold, .75f)].Box(f, new Vector3(0, h + 1.15f, .08f), new Vector3(1.1f, .06f, .06f));
            var cloth = new List<Vector2> { new Vector2(-.45f, 0), new Vector2(0, -.3f), new Vector2(.45f, 0), new Vector2(.45f, h), new Vector2(-.45f, h) };
            var trim = new List<Vector2> { new Vector2(-.5f, -.06f), new Vector2(0, -.38f), new Vector2(.5f, -.06f), new Vector2(.5f, h), new Vector2(-.5f, h) };
            var raise = new Frame(f.P(0, 1.2f - .05f, .1f), facing);
            b[Lit(Gold, .6f)].Extrude(raise, trim, -.02f, 0f, true);
            b[Lit(Navy, .35f)].Extrude(raise, cloth, 0f, .02f);
            HubArt.FlatIcon(parent, icon, f.P(0, 1.2f + h * .58f, .135f), .78f, GoldText, Mathf.Atan2(-f.O.x, -f.O.z) * Mathf.Rad2Deg, true);
        }

        // ================================================================== sport grounds
        /// White court lines (thin boxes) for a tennis court centred at `c` (ground), long axis `along`; net line through c.
        public static void CourtLines(Batch b, Vector3 c, Vector3 along, float scale = 1f, bool farHalf = true, bool nearHalf = true)
        {
            var f = new Frame(c, along); var paint = b[Lit(C("F7F7F2"), .25f)]; b.noShadow.Add(Lit(C("F7F7F2"), .25f));
            float L = 11.885f * scale, Wd = 5.485f * scale, Ws = 4.115f * scale, S = 6.4f * scale, lw = .06f;
            void Line(float x0, float z0, float x1, float z1) { paint.Box(f, new Vector3((x0 + x1) / 2, .006f, (z0 + z1) / 2), new Vector3(Mathf.Max(lw, Mathf.Abs(x1 - x0)), .012f, Mathf.Max(lw, Mathf.Abs(z1 - z0)))); }
            float zA = farHalf ? -L : 0, zB = nearHalf ? L : 0;
            foreach (float x in new[] { -Wd, -Ws, Ws, Wd }) Line(x, zA, x, zB);
            if (farHalf) { Line(-Wd, -L, Wd, -L); Line(-Ws, -S, Ws, -S); Line(0, -S, 0, 0); Line(-.05f, -L, .05f, -L + .15f); }
            if (nearHalf) { Line(-Wd, L, Wd, L); Line(-Ws, S, Ws, S); Line(0, 0, 0, S); Line(-.05f, L - .15f, .05f, L); }
        }
        /// The net across a court at `c`, `half` = half its length, along the frame's x; posts and white tape.
        public static void Net(Batch b, Vector3 c, Vector3 courtAlong, float half)
        {
            // the mesh as real cords (thin, opaque): it reads as a net from any distance and needs no transparency
            var f = new Frame(c, courtAlong); var cord = b[Lit(C("1E2226"), .2f)]; b.noShadow.Add(Lit(C("1E2226"), .2f));
            int nx = Mathf.RoundToInt(half * 2 / .12f);
            for (int i = 0; i <= nx; i++) cord.Box(f, new Vector3(-half + half * 2 * i / nx, .5f, 0), new Vector3(.016f, .86f, .016f));
            for (int k = 0; k < 8; k++) cord.Box(f, new Vector3(0, .1f + k * .11f, 0), new Vector3(half * 2, .016f, .016f));
            b[Lit(White, .3f)].Box(f, new Vector3(0, .5f, 0), new Vector3(.05f, .9f, .025f));
            b[Lit(White, .3f)].Box(f, new Vector3(0, .96f, 0), new Vector3(half * 2, .07f, .04f));
            foreach (int s in new[] { -1, 1 }) b[Lit(C("1F3B2A"), .4f)].Lathe(f.P(s * half, 0, 0), new[] { new Vector2(.05f, 0), new Vector2(.05f, 1.07f), new Vector2(.07f, 1.1f), new Vector2(0, 1.12f) }, 8);
        }
        /// The umpire's tall chair (green-painted wood) at the net post.
        public static void UmpireChair(Batch b, Vector3 at, Vector3 facing)
        {
            var f = new Frame(at, facing); var green = b[Lit(C("2F5A3A"), .35f)]; var wood = b[LitTex(PlankTex, C("B07A48"), .3f, 1f)];
            foreach (int s in new[] { -1, 1 }) foreach (int k in new[] { -1, 1 }) green.Box(f, new Vector3(s * .38f, 1.0f, k * .38f), new Vector3(.07f, 2.0f, .07f));
            for (int i = 0; i < 4; i++) wood.Box(f, new Vector3(0, .35f + i * .4f, .42f), new Vector3(.8f, .05f, .14f));
            wood.Box(f, new Vector3(0, 2.0f, 0), new Vector3(.9f, .08f, .9f));
            green.Box(f, new Vector3(0, 2.3f, -.4f), new Vector3(.9f, .55f, .07f));
            foreach (int s in new[] { -1, 1 }) green.Box(f, new Vector3(s * .43f, 2.2f, -.05f), new Vector3(.06f, .35f, .75f));
        }
        /// A green windscreen fence from a to c: posts and a dark green screen, `h` tall.
        public static void Windscreen(Batch b, Vector3 a, Vector3 c, float h = 2.6f)
        {
            var dir = c - a; dir.y = 0; float len = dir.magnitude; if (len < .1f) return;
            var f = new Frame((a + c) / 2, Vector3.Cross(dir / len, Vector3.up));
            b[LitTex(WindscreenTex, Color.white, .15f, .6f)].Box(f, new Vector3(0, h / 2 + .05f, 0), new Vector3(len, h - .1f, .04f));
            var post = b[Lit(C("1D3324"), .45f)];
            int n = Mathf.CeilToInt(len / 3f);
            for (int i = 0; i <= n; i++) post.Box(f, new Vector3(-len / 2 + len * i / n, h / 2 + .05f, .06f), new Vector3(.08f, h + .1f, .08f));
            post.Box(f, new Vector3(0, h + .08f, .06f), new Vector3(len, .06f, .06f));
        }
        /// A low cream stone wall with a navy band and a coping, a to c, `h` tall, with pillars every ~4 m.
        public static void StoneWall(Batch b, Vector3 a, Vector3 c, float h = .9f, bool pillars = true)
        {
            var dir = c - a; dir.y = 0; float len = dir.magnitude; if (len < .1f) return;
            var f = new Frame((a + c) / 2, Vector3.Cross(dir / len, Vector3.up));
            b[LitTex(StoneTex, Stone, .25f, 1.2f)].Box(f, new Vector3(0, h / 2, 0), new Vector3(len, h, .45f));
            b[Lit(Navy, .4f)].Box(f, new Vector3(0, h - .2f, 0), new Vector3(len + .01f, .1f, .47f));
            b[Lit(StoneLight, .3f)].Box(f, new Vector3(0, h + .05f, 0), new Vector3(len + .08f, .1f, .58f));
            if (!pillars) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 4f));
            for (int i = 0; i <= n; i++)
            {
                float x = -len / 2 + len * i / n;
                b[LitTex(StoneTex, StoneLight, .25f, 1.2f)].Box(f, new Vector3(x, (h + .35f) / 2, 0), new Vector3(.62f, h + .35f, .62f));
                b[Lit(StoneLight, .3f)].Box(f, new Vector3(x, h + .4f, 0), new Vector3(.74f, .1f, .74f));
            }
        }
        /// A golf bag (navy with a tan trim) with a few club heads, standing.
        public static void GolfBag(Batch b, Vector3 at)
        {
            var f = new Frame(at, Vector3.forward);
            b[Lit(Navy, .5f)].Lathe(at, new[] { new Vector2(.15f, 0), new Vector2(.17f, .1f), new Vector2(.16f, .82f), new Vector2(.18f, .9f), new Vector2(0, .9f) }, 12);
            b[Lit(C("D8C29A"), .4f)].Lathe(at + Vector3.up * .5f, new[] { new Vector2(.172f, 0), new Vector2(.172f, .08f) }, 12);
            b[Lit(C("D8C29A"), .4f)].Lathe(at + Vector3.up * .88f, new[] { new Vector2(.185f, 0), new Vector2(.185f, .06f) }, 12);
            var club = b[Lit(C("C9CDD3"), .8f)];
            for (int i = 0; i < 4; i++) club.Box(f, new Vector3((i - 1.5f) * .07f, 1.0f + (i % 2) * .04f, (i % 2) * .06f - .03f), new Vector3(.06f, .14f, .1f));
        }
        /// A wire bucket of white golf balls.
        public static void BallBucket(Batch b, Vector3 at)
        {
            b[Lit(C("B8BDC4"), .7f)].Lathe(at, new[] { new Vector2(.14f, 0), new Vector2(.19f, .3f), new Vector2(.17f, .3f), new Vector2(0, .02f) }, 12);
            var ball = b[Lit(White, .5f)];
            for (int i = 0; i < 9; i++) { float a = i * 2.4f, r = i == 0 ? 0 : .11f; ball.Ball(at + new Vector3(Mathf.Cos(a) * r, .3f + (i % 3) * .015f, Mathf.Sin(a) * r), Vector3.one * .045f, 1); }
        }
        /// A golf pin: a white pole and a cup rim (the flag itself flutters in HubLife).
        public static void Pin(Batch b, Vector3 at, float h = 2.2f)
        {
            b[Lit(White, .5f)].Lathe(at, new[] { new Vector2(.025f, 0), new Vector2(.025f, h), new Vector2(0, h + .02f) }, 6);
            b[Lit(C("2A2A2A"), .2f)].Lathe(at + Vector3.up * .005f, new[] { new Vector2(.12f, 0), new Vector2(0, 0) }, 12);
        }
        /// A little wooden footbridge from a to c, arched, with rails.
        public static void Bridge(Batch b, Vector3 a, Vector3 c, float w = 1.6f, float rise = .55f)
        {
            var dir = c - a; dir.y = 0; float len = dir.magnitude; var along = dir / len;
            var f = new Frame((a + c) / 2, Vector3.Cross(along, Vector3.up));
            var wood = b[LitTex(PlankTex, C("A9713E"), .3f, 1f)]; var dark = b[Lit(WoodDark, .3f)];
            int n = 14;
            for (int i = 0; i < n; i++)
            {
                float t0 = i / (float)n, t1 = (i + 1) / (float)n, x0 = -len / 2 + len * t0, x1 = -len / 2 + len * t1;
                float y0 = Mathf.Sin(t0 * Mathf.PI) * rise, y1 = Mathf.Sin(t1 * Mathf.PI) * rise;
                wood.Box(new Frame(f.P((x0 + x1) / 2, (y0 + y1) / 2 + .05f, 0), f.O), Vector3.zero, new Vector3(len / n + .02f, .07f, w));
                foreach (int s in new[] { -1, 1 }) dark.Box(new Frame(f.P((x0 + x1) / 2, (y0 + y1) / 2 + .62f, s * (w / 2 - .05f)), f.O), Vector3.zero, new Vector3(len / n + .03f, .06f, .06f));
                if (i % 3 == 0) foreach (int s in new[] { -1, 1 }) dark.Box(new Frame(f.P(x0, y0 + .32f, s * (w / 2 - .05f)), f.O), Vector3.zero, new Vector3(.08f, .62f, .08f));
            }
        }

        // ================================================================== interiors (Locker, Clubhouse, PLAY Hall)
        /// Lit, with a painted picture fitted to a rectangle in its mesh's UVs (metres): x0..x0+w across, y0..y0+h up.
        public static Material LitFit(Texture2D tex, float smooth, float w, float h, float x0 = 0, float y0 = 0)
        {
            string key = "litfit" + tex.name + smooth + w + h + x0 + y0;
            if (mats.TryGetValue(key, out var m) && m) return m;
            m = new Material(LitTex(tex, Color.white, smooth, 1)) { name = "Hub " + tex.name };
            m.SetTextureScale("_BaseMap", new Vector2(1 / w, 1 / h)); m.SetTextureOffset("_BaseMap", new Vector2(-x0 / w, -y0 / h));
            return mats[key] = m;
        }
        /// Checkered marble: cream and navy squares with faint veins and thin joints. Tile = two squares a side (1.8 m).
        public static Texture2D MarbleTex => Paint("marble checker", 256, 256, (u, v) =>
        {
            int cx = Mathf.FloorToInt(u * 2), cy = Mathf.FloorToInt(v * 2); bool dark = (cx + cy) % 2 == 1;
            float vein = Mathf.Abs(Mathf.Sin((u * 2.3f + v * 1.7f + N(u * 3 + cx, v * 3 + cy) * 1.4f) * 9.4f));
            var c = dark ? C("2A3A69") : C("EFE7D6");
            c = Mul(c, .95f + (N(u * 11 + cx * 3, v * 11) - .5f) * .1f);
            c = Color.Lerp(c, dark ? C("4A5E92") : C("D8CDB6"), Smooth(.95f, 1f, vein) * .35f);
            float fu = u * 2 - cx, fv = v * 2 - cy; float edge = Mathf.Min(Mathf.Min(fu, 1 - fu), Mathf.Min(fv, 1 - fv));
            c = Color.Lerp(Mul(c, .82f), c, Smooth(.004f, .016f, edge)); c.a = 1; return c;
        });
        /// Soft plaster: a warm cream with a faint mottle. Tile = 2 m.
        public static Texture2D PlasterTex => Paint("plaster", 128, 128, (u, v) =>
        {
            var c = Mul(Color.white, .96f + (N(u * 5, v * 5) - .5f) * .07f + (N(u * 19, v * 19) - .5f) * .03f); c.a = 1; return c;
        });
        /// A woven rug: a field with a border, corner blocks, a central lozenge and fine weave. Painted to fit (0..1).
        public static Texture2D RugTex(string name, Color field, Color border, Color figure) => Paint("rug " + name, 256, 128, (u, v) =>
        {
            float bu = Mathf.Min(u, 1 - u) * 2, bv = Mathf.Min(v, 1 - v);
            var c = field;
            if (bv < .14f || bu < .07f) c = border;
            if ((bv > .05f && bv < .07f) || (bu > .025f && bu < .035f && bv > .05f)) c = figure;
            float d = Mathf.Abs(u - .5f) * 2.2f + Mathf.Abs(v - .5f) * 2.6f;
            if (d < .55f) c = Color.Lerp(figure, border, Smooth(.3f, .5f, d));
            if (d > .7f && d < .76f && bv > .14f) c = figure;
            c = Mul(c, .93f + ((Mathf.FloorToInt(u * 256) + Mathf.FloorToInt(v * 128)) % 2) * .07f + (N(u * 40, v * 20) - .5f) * .06f);
            c.a = 1; return c;
        }, true);
        /// Two paintings for the walls: a tennis player mid-serve on a lawn court, a golfer at the top of the follow-through.
        public static Texture2D PaintingTex(bool golf) => Paint(golf ? "painting golf" : "painting tennis", 192, 256, (u, v) =>
        {
            Color c = v > .45f ? Color.Lerp(C("F6D9AE"), C("8FB5DE"), Smooth(.45f, 1, v)) : Color.Lerp(C("4C8A33"), C("6FA845"), v / .45f);
            if (v > .42f && v < .5f + .04f * Mathf.Sin(u * 18)) c = Color.Lerp(c, C("3C6B2A"), .8f);   // the tree line
            if (!golf && v < .42f) { if (Mathf.Abs(v - .2f - (u - .5f) * .1f) < .006f || Mathf.Abs(u - .5f - (.42f - v) * .3f) < .006f) c = Color.white; }
            var p = new Vector2(u, v); var skin = C("E8B48F"); var kit = golf ? C("2B3B6E") : C("F5F2EA");
            float body = Box2(u, v, .5f, .36f, .06f, .1f, .04f), head = (p - new Vector2(.5f, .52f)).magnitude - .045f;
            float legs = Mathf.Min(Box2(u, v, .47f, .2f, .02f, .09f, .01f), Box2(u, v, .54f, .2f, .02f, .09f, .01f));
            float arm = golf ? Mathf.Min(Box2(u, v, .58f, .48f, .015f, .07f, .01f), Box2(u, v, .44f, .46f, .015f, .07f, .01f)) : Box2(u, v, .6f, .5f, .015f, .08f, .01f);
            float tool = golf ? Mathf.Abs((u - .62f) * .8f + (v - .58f) * .6f) - .005f + Mathf.Max(0, (p - new Vector2(.5f, .66f)).magnitude - .22f) * 9 : (p - new Vector2(.62f, .66f)).magnitude - .05f;
            if (legs < 0) c = golf ? C("3A3A40") : C("F5F2EA");
            if (body < 0) c = kit; if (arm < 0) c = skin; if (head < 0) c = skin;
            if (tool < 0) c = golf ? C("C8CCD2") : C("2A2C33");
            if (!golf && Mathf.Abs((p - new Vector2(.62f, .66f)).magnitude - .05f) < .008f) c = C("2A2C33");
            c.a = 1; return c;
        }, true);

        /// Panelling along the foot of a wall from x0 to x1 (frame x), in front of the wall's face (frame z = 0): a base, raised
        /// panel mouldings every ~1 m, a gold chair rail, a dark skirting.
        public static void Panelling(Batch b, Frame f, float x0, float x1, Color tone, float h = 1.12f)
        {
            float len = x1 - x0; if (len < .05f) return; float mid = (x0 + x1) / 2;
            var basePanel = b[Lit(tone, .42f)]; var moulding = b[Lit(Color.Lerp(tone, Color.white, .14f), .45f)];
            basePanel.Box(f, new Vector3(mid, h / 2, .04f), new Vector3(len, h, .08f));
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.05f)); float bay = len / n;
            for (int i = 0; i < n; i++)
            {
                float cx = x0 + bay * (i + .5f), hw = bay / 2 - .13f; if (hw < .08f) continue;
                moulding.Box(f, new Vector3(cx, .23f, .09f), new Vector3(hw * 2, .04f, .03f)); moulding.Box(f, new Vector3(cx, h - .2f, .09f), new Vector3(hw * 2, .04f, .03f));
                moulding.Box(f, new Vector3(cx - hw, (h - .2f + .23f) / 2, .09f), new Vector3(.04f, h - .43f, .03f)); moulding.Box(f, new Vector3(cx + hw, (h - .2f + .23f) / 2, .09f), new Vector3(.04f, h - .43f, .03f));
            }
            b[Lit(Gold, .7f)].Box(f, new Vector3(mid, h + .03f, .07f), new Vector3(len, .06f, .1f));
            b[Lit(Color.Lerp(tone, Color.black, .35f), .4f)].Box(f, new Vector3(mid, .07f, .07f), new Vector3(len, .14f, .14f));
        }
        /// A crown moulding at the top of a wall (cream, stepped), x0..x1.
        public static void Crown(Batch b, Frame f, float x0, float x1, float top)
        {
            float len = x1 - x0, mid = (x0 + x1) / 2; var cream = b[Lit(StoneLight, .35f)];
            cream.Box(f, new Vector3(mid, top - .1f, .06f), new Vector3(len, .2f, .12f));
            cream.Box(f, new Vector3(mid, top - .26f, .03f), new Vector3(len, .12f, .06f));
            b[Lit(Gold, .7f)].Box(f, new Vector3(mid, top - .33f, .05f), new Vector3(len, .025f, .04f));
        }
        /// A coffered ceiling seen from below: navy panels between walnut beams, `w` x `d` round `c` (its underside at c.y).
        public static void Coffers(Batch b, Vector3 c, float w, float d, float pitch = 2.0f)
        {
            var f = new Frame(c, Vector3.forward);
            b[Lit(C("1E2A52"), .3f)].Box(f, new Vector3(0, .1f, 0), new Vector3(w, .2f, d), true);
            var beam = b[LitTex(PlankTex, C("6E4428"), .4f, 1f)];
            for (float x = -w / 2; x <= w / 2 + .01f; x += w / Mathf.Max(1, Mathf.Round(w / pitch))) beam.Box(f, new Vector3(x, -.07f, 0), new Vector3(.2f, .16f, d), true);
            for (float z = -d / 2; z <= d / 2 + .01f; z += d / Mathf.Max(1, Mathf.Round(d / pitch))) beam.Box(f, new Vector3(0, -.06f, z), new Vector3(w, .14f, .18f), true);
        }
        /// A brass lantern hanging on a chain from `top`, its glass `drop` metres below.
        public static void HangingLantern(Batch b, Vector3 top, float drop, float s = 1)
        {
            var iron = b[Lit(Iron, .5f)]; var brass = b[Lit(Brass, .75f)]; var f = new Frame(top, Vector3.forward);
            for (float y = .06f; y < drop - .05f; y += .12f) iron.Box(f, new Vector3(0, -y, 0), new Vector3(.03f, .08f, .015f + (Mathf.RoundToInt(y / .12f) % 2) * .02f));
            var at = top + Vector3.down * drop;
            brass.Lathe(at, new[] { new Vector2(0, 0), new Vector2(.06f, .02f), new Vector2(.26f, -.14f), new Vector2(.2f, -.18f), new Vector2(0, -.17f) }, 8, Vector3.one * s);
            b[Glow(LanternGlow)].Lathe(at + Vector3.down * .62f * s, new[] { new Vector2(0, 0), new Vector2(.14f, 0), new Vector2(.2f, .44f), new Vector2(0, .44f) }, 6, Vector3.one * s);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var p0 = at + Vector3.down * .62f * s + d * .145f * s; var p1 = at + Vector3.down * .18f * s + d * .205f * s;
                brass.Box(new Frame((p0 + p1) / 2, d), Vector3.zero, new Vector3(.03f, .45f, .03f) * s);
            }
            brass.Lathe(at + Vector3.down * .75f * s, new[] { new Vector2(0, 0), new Vector2(.06f, .08f), new Vector2(.16f, .13f), new Vector2(0, .14f) }, 8, Vector3.one * s);
        }
        /// A tall wooden trophy cabinet against a wall (frame origin on the wall, O into the room): navy cupboard doors below,
        /// open glazed shelves of gold cups above; the carcass gets a collider.
        public static void Cabinet(Batch b, Transform parent, Frame f, float w, float h, int seed)
        {
            var wood = b[LitTex(PlankTex, C("6B4024"), .45f, 1f)]; var dark = b[Lit(C("3A2414"), .4f)];
            wood.Box(f, new Vector3(0, .45f, .24f), new Vector3(w, .9f, .48f));                       // the cupboard
            wood.Box(f, new Vector3(0, (h + .9f) / 2, .01f), new Vector3(w, h - .9f, .02f));             // the back
            foreach (int sx in new[] { -1, 1 }) wood.Box(f, new Vector3(sx * (w / 2 - .03f), (h + .9f) / 2, .24f), new Vector3(.06f, h - .9f, .48f));
            dark.Box(f, new Vector3(0, (h + .95f) / 2, .03f), new Vector3(w - .14f, h - 1.1f, .06f));   // the back of the open part
            b[Lit(Navy, .5f)].Box(f, new Vector3(0, .5f, .49f), new Vector3(w - .12f, .76f, .03f));
            b[Lit(Brass, .8f)].Box(f, new Vector3(-.06f, .62f, .52f), new Vector3(.03f, .1f, .03f)); b[Lit(Brass, .8f)].Box(f, new Vector3(.06f, .62f, .52f), new Vector3(.03f, .1f, .03f));
            int shelves = 3; float y0 = .95f, top = h - .15f;
            for (int k = 0; k <= shelves; k++)
            {
                float y = y0 + (top - y0) * k / shelves;
                wood.Box(f, new Vector3(0, y, .3f), new Vector3(w - .1f, .04f, .38f));
                if (k == shelves) break;
                int cups = 2 + (seed + k) % 2;
                for (int i = 0; i < cups; i++) Trophy(b, f.P((i - (cups - 1) / 2f) * (w - .3f) / cups, y + .1f, .28f), .42f + H(i, k, seed) * .18f);
            }
            foreach (int sx in new[] { -1, 1 }) wood.Box(f, new Vector3(sx * (w / 2 - .03f), (h + .95f) / 2, .5f), new Vector3(.06f, h - 1.0f, .04f));
            wood.Box(f, new Vector3(0, (h + .95f) / 2, .5f), new Vector3(.04f, h - 1.0f, .04f));
            wood.Box(f, new Vector3(0, h + .05f, .26f), new Vector3(w + .1f, .1f, .54f));
            BoxCollider(parent, f.P(0, h / 2, .24f), new Vector3(w, h, .48f), f.Facing);
        }
        /// A framed painting on a wall (frame origin on the wall at the picture's foot, O into the room).
        public static void Picture(Batch b, Frame f, float w, float h, Texture2D art)
        {
            var gold = b[Lit(Gold, .7f)];
            gold.Box(f, new Vector3(0, h / 2, .03f), new Vector3(w + .16f, h + .16f, .06f));
            b[LitFit(art, .2f, w, h, -w / 2, 0)].Quad(f.P(-w / 2, 0, .065f), f.P(-w / 2, h, .065f), f.P(w / 2, h, .065f), f.P(w / 2, 0, .065f), f.O, new Vector2(-w / 2, 0));
        }
        /// A rug flat on the floor, `len` along `along`, `width` across.
        public static void Rug(Batch b, Vector3 centre, Vector3 along, float len, float width, Texture2D tex)
        {
            var f = new Frame(centre, Vector3.Cross(Vector3.up, along)); var mat = LitFit(tex, .05f, len, width, -len / 2, -width / 2);
            b.noShadow.Add(mat); var m = b[mat];
            m.Quad(f.P(-len / 2, .012f, -width / 2), f.P(-len / 2, .012f, width / 2), f.P(len / 2, .012f, width / 2), f.P(len / 2, .012f, -width / 2), Vector3.up, new Vector2(-len / 2, -width / 2));
        }
        /// A wall with round-topped openings (frame origin at the wall's inner face, O into the room, the wall `thick` behind it):
        /// plain piers between the openings, spandrels over them, the arch soffits; `openings` = (centre x, half width, spring, rise).
        public static void ArchedWall(Batch b, Frame f, float x0, float x1, float h, float thick, Material plaster, params (float cx, float hw, float spring, float rise)[] openings)
        {
            var m = b[plaster]; var sorted = new List<(float cx, float hw, float spring, float rise)>(openings); sorted.Sort((p, q) => p.cx.CompareTo(q.cx));
            float x = x0;
            foreach (var o in sorted) { if (o.cx - o.hw > x + .01f) m.Box(f, new Vector3((x + o.cx - o.hw) / 2, h / 2, -thick / 2), new Vector3(o.cx - o.hw - x, h, thick)); x = o.cx + o.hw; }
            if (x1 > x + .01f) m.Box(f, new Vector3((x + x1) / 2, h / 2, -thick / 2), new Vector3(x1 - x, h, thick));
            const int N = 16;
            foreach (var o in sorted)
            {
                for (int k = 0; k < N; k++)
                {
                    float a0 = k * Mathf.PI / N, a1 = (k + 1) * Mathf.PI / N;
                    float px0 = o.cx + Mathf.Cos(a0) * o.hw, py0 = o.spring + Mathf.Sin(a0) * o.rise, px1 = o.cx + Mathf.Cos(a1) * o.hw, py1 = o.spring + Mathf.Sin(a1) * o.rise;
                    m.Quad(f.P(px0, py0, 0), f.P(px0, h, 0), f.P(px1, h, 0), f.P(px1, py1, 0), f.O);
                    m.Quad(f.P(px0, py0, -thick), f.P(px0, h, -thick), f.P(px1, h, -thick), f.P(px1, py1, -thick), -f.O);
                    var nn = f.D(o.cx - (px0 + px1) / 2, o.spring - (py0 + py1) / 2, 0).normalized;
                    m.Quad(f.P(px0, py0, 0), f.P(px0, py0, -thick), f.P(px1, py1, -thick), f.P(px1, py1, 0), nn);
                }
            }
        }
        /// A moulded surround round an arched opening (on the wall face, standing `proud` in front of it).
        public static void ArchSurround(Batch b, Frame f, float cx, float hw, float spring, float rise, float width, Color tone, float proud = .07f)
        {
            var m = b[Lit(tone, .45f)]; const int N = 16;
            for (int k = 0; k < N; k++)
            {
                float a0 = k * Mathf.PI / N, a1 = (k + 1) * Mathf.PI / N;
                Vector3 In(float a, float z) => f.P(cx + Mathf.Cos(a) * hw, spring + Mathf.Sin(a) * rise, z);
                Vector3 Out(float a, float z) => f.P(cx + Mathf.Cos(a) * (hw + width), spring + Mathf.Sin(a) * (rise + width), z);
                m.Quad(In(a0, proud), Out(a0, proud), Out(a1, proud), In(a1, proud), f.O);
                var nOut = f.D(Mathf.Cos((a0 + a1) / 2), Mathf.Sin((a0 + a1) / 2), 0);
                m.Quad(Out(a0, 0), Out(a0, proud), Out(a1, proud), Out(a1, 0), nOut);
                m.Quad(In(a0, 0), In(a0, proud), In(a1, proud), In(a1, 0), -nOut);
            }
            foreach (int s in new[] { -1, 1 }) m.Box(f, new Vector3(cx + s * (hw + width / 2), spring / 2, proud / 2), new Vector3(width, spring, proud));
            m.Box(f, new Vector3(cx, spring + rise + width * .55f, proud / 2 + .02f), new Vector3(.3f, width * 1.4f, proud + .04f));   // keystone
        }
        /// A pair of wooden doors filling an arched opening (frame origin at the opening's foot, O toward the viewer).
        public static void ArchDoors(Batch b, Frame f, float hw, float spring, float rise)
        {
            var wood = b[LitTex(PlankTex, C("7A4A26"), .45f, .8f)]; var panel = b[Lit(C("5A3418"), .45f)];
            foreach (int s in new[] { -1, 1 })
            {
                var pts = new List<Vector2> { new Vector2(0, 0), new Vector2(s * hw, 0), new Vector2(s * hw, spring) };
                for (int k = 1; k <= 8; k++) { float a = (s > 0 ? 0 : Mathf.PI) + s * k * Mathf.PI / 16; pts.Add(new Vector2(Mathf.Cos(a) * hw, spring + Mathf.Sin(a) * rise)); }
                wood.Extrude(f, pts, 0, .08f);
                float px = s * hw * .5f;
                panel.Box(f, new Vector3(px, .75f, .09f), new Vector3(hw * .62f, .9f, .03f));
                panel.Box(f, new Vector3(px, 1.95f, .09f), new Vector3(hw * .62f, 1.0f, .03f));
                b[Lit(Brass, .8f)].Box(f, new Vector3(s * .11f, 1.25f, .12f), new Vector3(.04f, .32f, .05f));
            }
            b[Lit(C("3E2412"), .4f)].Box(f, new Vector3(0, (spring + rise) / 2, .09f), new Vector3(.03f, spring + rise, .02f));
        }

        // ================================================================== a country house (the clubhouse on the tennis and golf views)
        /// A cream stone house with a navy hip roof, dormers, a chimney, a cupola and lit windows, front facing `facing`.
        public static void Mansion(Batch b, Vector3 at, Vector3 facing, float s = 1f)
        {
            var f = new Frame(at, facing); float W = 14 * s, D = 8 * s, eave = 5f * s;
            b[LitTex(StoneTex, Stone, .25f, 1.2f)].Box(f, new Vector3(0, eave / 2, -D / 2), new Vector3(W, eave, D));
            b[LitTex(StoneTex, StoneShade, .25f, 1.2f)].Box(f, new Vector3(0, .3f, -D / 2), new Vector3(W + .3f, .6f, D + .3f));
            HipRoof(b, f, -W / 2 - .4f, W / 2 + .4f, -D - .4f, .4f, eave, 3.6f * s);
            // a central gabled porch
            b[LitTex(StoneTex, StoneLight, .25f, 1.2f)].Box(f, new Vector3(0, eave * .55f, .6f * s), new Vector3(4f * s, eave * 1.1f, 1.4f * s));
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) { Window(b, new Frame(f.P(0, 0, 1.31f * s), f.O), .1f, 1.6f * s, 2.6f * s, true, Glow(WarmGlow), .16f); continue; }
                float x = i * 2.6f * s + Mathf.Sign(i) * .6f * s;
                Window(b, new Frame(f.P(x, 0, .01f), f.O), 1.0f * s, 1.1f * s, 1.9f * s, true, Glow(WarmGlow * .9f), .14f);
            }
            foreach (int i in new[] { -1, 1 }) Dormer(b, f, i * 3.2f * s, -1.0f * s, eave + .7f * s, 1.3f * s, 1.2f * s);
            b[LitTex(StoneTex, StoneShade, .25f, 1.2f)].Box(f, new Vector3(4.8f * s, eave + 3.4f * s, -D * .55f), new Vector3(.9f * s, 3f * s, .9f * s));
            // the cupola on the ridge
            b[Lit(White, .4f)].Box(f, new Vector3(0, eave + 3.6f * s + .6f * s, -D / 2), new Vector3(1.4f * s, 1.2f * s, 1.4f * s));
            b[LitTex(SlateTex, SlateTint, .25f, 1.6f)].Lathe(f.P(0, eave + 3.6f * s + 1.2f * s, -D / 2), new[] { new Vector2(1.0f * s, 0), new Vector2(0, 1.2f * s) }, 4);
            b[Lit(Gold, .8f)].Ball(f.P(0, eave + 3.6f * s + 2.5f * s, -D / 2), Vector3.one * .14f * s, 1);
        }
    }
}
