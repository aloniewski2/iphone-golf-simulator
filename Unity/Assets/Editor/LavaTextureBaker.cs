using System.IO;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Bakes the crater's textures (the Magma Open's lava world, Course/LavaWorld.cs) into
    /// Resources/Course/Lava as PNGs, so the phone loads them instead of making them at every hole. The
    /// look is Adnan's Volcano venue (Tennis/TennisVenueBuilder.cs, on origin/newmapsandmenus): his
    /// generators for the basalt, its relief, the lava streams, the cracks and the molten lake, ported
    /// as they are, plus a tiling ash cloud for the sky and the two soft sprites for smoke and sparks.
    /// Run from the menu, or headless: -executeMethod GolfArcade.EditorTools.LavaTextureBaker.Bake
    public static class LavaTextureBaker
    {
        const string Dir = "Assets/Resources/Course/Lava";

        [MenuItem("Golf Arcade/Bake Lava Textures")]
        public static void Bake()
        {
            Directory.CreateDirectory(Dir);
            Save("crater_rock", 512, (u, v) =>
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
            });
            Save("crater_relief", 256, (u, v) =>
            {
                float H(float x, float y) => Fbm(x, y, 12, 4, 7) * .65f + TileNoiseXY(x, y, 4, 30, 2) * .35f;
                const float e = 1f / 256f, k = 7f;
                float dx = H(u + e, v) - H(u - e, v), dy = H(u, v + e) - H(u, v - e);
                var nrm = new Vector3(-dx * k, -dy * k, 1).normalized;
                return new Color(nrm.x * .5f + .5f, nrm.y * .5f + .5f, nrm.z * .5f + .5f, 1);
            });
            Save("lava_streams", 512, (u, v) =>
            {
                // lava streams running down the flank: long thin veins, some fed, some dry
                float wob = (Fbm(u, v, 5, 3, 51) - .5f) * .09f;
                float n = TileNoiseXY(u + wob, v, 30, 3, 88), line = 1 - Sm(0f, .07f, Mathf.Abs(n - .5f) * 2);
                float fed = Sm(.50f, .64f, TileNoiseXY(u, v, 6, 2, 14));
                float m = line * fed * (.75f + .25f * Fbm(u, v, 12, 2, 4));
                return new Color(m, m, m, 1);
            });
            Save("lava_cracks", 256, (u, v) =>
            {
                // thin, wandering cracks: a ridged noise line
                float n = Mathf.Abs(Fbm(u, v, 4, 4, 41) - .5f) * 2;
                float line = 1 - Sm(.0f, .07f, n);
                float pulse = Sm(.40f, .58f, Fbm(u, v, 3, 3, 9));
                float m = line * pulse;
                return new Color(m, m, m, 1);
            });
            Save("lava_base", 256, (u, v) => LavaAt(u, v, false));
            Save("lava_glow", 256, (u, v) => LavaAt(u, v, true));
            Save("ash_gravel", 256, (u, v) =>
            {
                float n = Fbm(u, v, 8, 4, 61), speck = Hash(Mathf.FloorToInt(u * 128), Mathf.FloorToInt(v * 128), 3) * .12f;
                float g = .20f + n * .22f + speck;
                return new Color(g * 1.10f, g * .98f, g * .86f, 1);
            });
            Save("ash_cloud", 256, (u, v) =>
            {
                // a tiling deck of ash: alpha is its density (the sky shader tints it)
                float n = Fbm(u, v, 4, 5, 40), m = Fbm(u, v, 6, 3, 49);
                float a = Sm(.34f, .66f, n) * Mathf.Lerp(.7f, 1f, Sm(.35f, .65f, m));
                return new Color(1, 1, 1, a);
            });
            Save("soft", 128, (u, v) =>
            {
                float dx = u * 2 - 1, dy = v * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a);
                return new Color(1, 1, 1, a);
            });
            Save("puff", 128, (u, v) =>
            {
                float dx = u * 2 - 1, dy = v * 2 - 1, r = Mathf.Sqrt(dx * dx * .9f + dy * dy * 1.5f);
                float n = Fbm(u, v, 3, 4, 5);
                float a = Mathf.Clamp01((1 - r) * 1.7f - (1 - n) * .55f); a = a * a * (3 - 2 * a);
                float shade = Mathf.Lerp(.72f, 1f, v);   // the tops catch the light
                return new Color(shade, shade, shade, a);
            });
            AssetDatabase.Refresh();
            Debug.Log("Lava textures baked into " + Dir);
        }

        static void Save(string name, int size, System.Func<float, float, Color> f)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f((x + .5f) / size, (y + .5f) / size);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            File.WriteAllBytes($"{Dir}/{name}.png", t.EncodeToPNG());
            Object.DestroyImmediate(t);
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

        // ---- noise that tiles (the textures wrap seamlessly)
        static float Sm(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

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

        static float TileNoise(float u, float v, float period, float seed = 0) => TileNoiseXY(u, v, period, period, seed);

        static float TileNoiseXY(float u, float v, float pu, float pv, float seed = 0)
        {
            int px = Mathf.Max(1, Mathf.RoundToInt(pu)), py = Mathf.Max(1, Mathf.RoundToInt(pv)), sd = Mathf.RoundToInt(seed) * 7919;
            float x = Mathf.Repeat(u, 1) * px, y = Mathf.Repeat(v, 1) * py;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(Wrap(x0, px), Wrap(y0, py), sd), b = Hash(Wrap(x0 + 1, px), Wrap(y0, py), sd);
            float c = Hash(Wrap(x0, px), Wrap(y0 + 1, py), sd), d = Hash(Wrap(x0 + 1, px), Wrap(y0 + 1, py), sd);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float u, float v, float period, int octaves, float seed = 0)
        {
            float sum = 0, amp = .5f, norm = 0;
            for (int i = 0; i < octaves; i++) { sum += amp * TileNoise(u, v, period, seed + i * 17.3f); norm += amp; amp *= .5f; period *= 2; }
            return sum / norm;
        }
    }

    /// Import settings for the baked lava textures: the relief is a normal map, the masks are not colour.
    public sealed class LavaTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Course/Lava/")) return;
            var importer = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            if (name == "crater_relief") importer.textureType = TextureImporterType.NormalMap;
            else if (name is "lava_streams" or "lava_cracks") importer.sRGBTexture = false;
            else if (name is "soft" or "puff") { importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; }
        }
    }
}
