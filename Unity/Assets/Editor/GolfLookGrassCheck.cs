using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.EditorTools
{
    /// Grass mow-band sheen (v9, area T, work/postcard-look/TEXTURES.md "v9"): the editor-side gates of the albedo-ALPHA smoothness.
    ///   Fairway_C / Green_C carry the per-band smoothness in their alpha channel; GolfLook turns on URP Lit's "Smoothness Source = Albedo Alpha"
    ///   (_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A) for the two rows. A player build strips shader variants no material in the build uses, and the
    ///   LK_ materials are created at run time, so Resources/Course/GolfLookVariants.mat (created here, kept in the build because it sits in Resources)
    ///   is the "keeper" that holds the {_NORMALMAP, _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A} variant.
    ///
    ///   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.GolfLookGrassCheck.Run -logFile <abs log>     (always through work/postcard-look/heavy.sh)
    ///   GOLF_GRASS_CHECK_REPORT=<file>  also writes the GATE lines there.   GOLF_GRASS_CREATE_KEEPER=1  creates / refreshes the keeper material first.
    /// Exit code 1 when a gate fails.
    public static class GolfLookGrassCheck
    {
        const string KeeperPath = "Assets/Resources/Course/GolfLookVariants.mat";
        const string KeeperResource = "Course/GolfLookVariants";
        static readonly string[] Keywords = { "_NORMALMAP", "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A" };
        static readonly List<string> lines = new();
        static int fails;

        static void Gate(string id, bool pass, string detail)
        {
            string l = $"GATE: {id} {(pass ? "PASS" : "FAIL")} - {detail}";
            lines.Add(l); Debug.Log(l); if (!pass) fails++;
        }

        public static void Run()
        {
            lines.Clear(); fails = 0;
            if (System.Environment.GetEnvironmentVariable("GOLF_GRASS_CREATE_KEEPER") == "1") EnsureKeeper();
            GolfLook.ClearCache();
            foreach (var (name, tex, min, max) in new[] { ("LK_FAIRWAY", "Fairway", .08f, .22f), ("LK_GREEN", "Green", .08f, .22f) })
            {
                var spec = GolfLook.Table[name];
                var m = GolfLook.Get(name);
                string png = $"Assets/Resources/Course/Look/{tex}_C.png";
                byte colorType = 0;
                using (var f = File.OpenRead(png)) { var head = new byte[26]; f.Read(head, 0, 26); colorType = head[25]; }
                var imp = AssetImporter.GetAtPath(png) as TextureImporter;
                bool srcAlpha = imp != null && imp.DoesSourceTextureHaveAlpha();
                bool kw = m.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                float chan = m.GetFloat("_SmoothnessTextureChannel"), sm = m.GetFloat("_Smoothness");
                bool lit = m.shader && m.shader.name == "Universal Render Pipeline/Lit";
                bool maps = m.GetTexture("_BaseMap") && m.GetTexture("_BumpMap");
                // the PNG as Unity decodes it: alpha statistics (light bands glossy, dark bands matte; the generator's GRASS_SHEEN_ALPHA gate measures the bands, here the whole tile)
                var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                ImageConversion.LoadImage(tmp, File.ReadAllBytes(png));
                var px = tmp.GetPixels32(); double sum = 0; int amin = 255, amax = 0;
                foreach (var p in px) { sum += p.a; amin = Mathf.Min(amin, p.a); amax = Mathf.Max(amax, p.a); }
                float mean = (float)(sum / px.Length / 255.0);
                Object.DestroyImmediate(tmp);
                bool ok = spec.SmoothnessFromAlbedoAlpha && colorType == 6 && srcAlpha && kw && Mathf.Approximately(chan, 1f) && Mathf.Approximately(sm, spec.Smoothness) && lit && maps && mean >= min && mean <= max && amax / 255f <= .60f;
                Gate($"GRASS_SHEEN_WIRED {name}", ok,
                    $"{tex}_C.png colour type {colorType} (6 = RGBA), importer source alpha {srcAlpha}, material {m.shader?.name}: keyword _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A {kw}, _SmoothnessTextureChannel {chan}, _Smoothness (alpha scale) {sm:F2} (table {spec.Smoothness:F2}), _BaseMap + _BumpMap {maps}; decoded alpha mean {mean:F3} ({min:F2}..{max:F2}), min {amin / 255f:F3}, max {amax / 255f:F3} (<= .60)");
                // per-hole tinted copies keep the keyword (GetForHole = new Material(shared))
                bool allKeep = true; var kept = new StringBuilder();
                foreach (int hole in new[] { 8, 9, 10 })
                {
                    var h = GolfLook.GetForHole(name, hole);
                    bool k = h.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") && Mathf.Approximately(h.GetFloat("_Smoothness"), spec.Smoothness);
                    allKeep &= k; kept.Append($" hole {hole} {(h == m ? "shared" : h.name)} {(k ? "ok" : "LOST")};");
                }
                Gate($"GRASS_SHEEN_PER_HOLE {name}", allKeep, "per-hole material copies keep the sheen keyword and scale:" + kept);
            }
            var keeper = Resources.Load<Material>(KeeperResource);
            bool kOk = keeper && keeper.shader && keeper.shader.name == "Universal Render Pipeline/Lit";
            var missing = new List<string>();
            if (kOk) foreach (var k in Keywords) if (!keeper.IsKeywordEnabled(k)) missing.Add(k);
            Gate("GRASS_SHEEN_VARIANT_KEPT", kOk && missing.Count == 0,
                kOk ? $"Resources/{KeeperResource}.mat (URP Lit) is in the build and enables {string.Join(" + ", Keywords)}: a player build keeps the variant the run-time LK_FAIRWAY / LK_GREEN materials need (missing: {(missing.Count == 0 ? "none" : string.Join(",", missing))})"
                    : $"Resources/{KeeperResource}.mat missing (run with GOLF_GRASS_CREATE_KEEPER=1): a player build would strip the {Keywords[1]} variant and the sheen would silently fall back to smoothness = the scale (1.0 = a mirror)");
            Gate("SUMMARY", fails == 0, $"{lines.Count - 0} gate lines, {fails} failed");
            var report = System.Environment.GetEnvironmentVariable("GOLF_GRASS_CHECK_REPORT");
            if (!string.IsNullOrEmpty(report)) File.WriteAllText(report, string.Join("\n", lines) + "\n");
            if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        /// Creates (or refreshes) the keeper material: URP Lit with the two keywords the run-time grass materials use. Idempotent.
        public static void EnsureKeeper()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) { Debug.LogError("[GolfLookGrassCheck] URP Lit not found"); return; }
            var m = AssetDatabase.LoadAssetAtPath<Material>(KeeperPath);
            bool created = !m;
            if (created) { m = new Material(shader) { name = "GolfLookVariants" }; AssetDatabase.CreateAsset(m, KeeperPath); }
            m.shader = shader;
            foreach (var k in Keywords) m.EnableKeyword(k);
            m.SetFloat("_SmoothnessTextureChannel", 1);
            m.enableInstancing = true;                      // the run-time LK_ materials are GPU-instanced: keep the instancing variant of the same combination too
            EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
            Debug.Log($"[GolfLookGrassCheck] keeper material {(created ? "created" : "refreshed")}: {KeeperPath}");
        }
    }
}
