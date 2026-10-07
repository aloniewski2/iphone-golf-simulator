using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.EditorTools
{
    /// Smoke gates for the postcard look runtime (GolfLook / GolfAtmosphere / HoleView material order):
    ///   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.GolfLookSmoke.Run -logFile <abs log>
    /// Prints "GATE: <what> PASS|FAIL - <numbers>" lines; exit code 1 when any gate fails.
    /// GOLF_LOOK_SMOKE_REPORT=<path> also writes the lines to a file.
    public static class GolfLookSmoke
    {
        static readonly List<string> report = new();
        static int gates, fails;

        static void Say(string s) { report.Add(s); Debug.Log(s); }
        static void Gate(string name, bool ok, string detail) { gates++; if (!ok) fails++; Say($"GATE: {name} {(ok ? "PASS" : "FAIL")} - {detail}"); }

        public static void Run()
        {
            try { RunAll(); }
            catch (System.Exception e) { Gate("SMOKE_RAN", false, "exception: " + e); }
            Say($"SUMMARY: {gates - fails}/{gates} gates PASS");
            string path = System.Environment.GetEnvironmentVariable("GOLF_LOOK_SMOKE_REPORT");
            if (!string.IsNullOrEmpty(path)) { try { File.WriteAllLines(Path.GetFullPath(path), report.ToArray()); } catch (System.Exception e) { Debug.LogWarning(e.Message); } }
            if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        [MenuItem("Golf Arcade/Postcard look smoke")]
        static void Menu() { report.Clear(); gates = fails = 0; Run(); }

        static void RunAll()
        {
            AssetDatabase.Refresh();
            Say($"INFO: active pipeline asset '{GolfLookBoards.PipelineName()}', quality level {QualitySettings.GetQualityLevel()} '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', Unity {Application.unityVersion}, {SystemInfo.graphicsDeviceType}");
            // GOLF_LOOK_TUNE=1: only the Crater and smoke boards with the parameter sweeps (fast tuning loop; the full run is without it)
            if (System.Environment.GetEnvironmentVariable("GOLF_LOOK_TUNE") == "1")
            {
                // tuning overrides (in memory only): GOLF_LOOK_LAVA_I=<intensity>, GOLF_LOOK_BASALT_TINT=r,g,b, GOLF_LOOK_BASALT_MATTE=0|1, GOLF_LOOK_SMOKE=r,g,b,gain,emission,lit[,fogshare]
                GolfLook.ClearCache();
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                float[] F(string v) => v.Split(',').Select(x => float.Parse(x, inv)).ToArray();
                var ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_LAVA_I"); if (!string.IsNullOrEmpty(ev)) GolfLook.LavaLightIntensity = float.Parse(ev, inv);
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_BASALT_TINT"); if (!string.IsNullOrEmpty(ev)) { var f = F(ev); GolfLook.Table["LK_BASALT"].Tint = new Color(f[0], f[1], f[2]); }
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_LAVA_FOG"); if (!string.IsNullOrEmpty(ev)) GolfLook.Table["LK_LAVA"].FogShare = float.Parse(ev, inv);   // v2 repair round 3: the negative control of LAVA_FAR_ORANGE (1 = the lava takes all the fog)
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_CRATER_OLD_ATMOSPHERE");   // v2 repair round 3: the negative control of BASALT_SHADOW_SIDE_NEUTRAL (the ember-red fog / ambient of before the round)
                if (ev == "1") { var cl = GolfAtmosphere.Holes[10]; cl.FogColor = new Color(.42f, .19f, .14f); cl.AmbientEquator = new Color(.55f, .30f, .22f); cl.AmbientGround = new Color(.60f, .22f, .08f); }
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_BASALT_MATTE"); if (!string.IsNullOrEmpty(ev)) GolfLook.Table["LK_BASALT"].NoSpecular = ev == "1";
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_SMOKE"); if (!string.IsNullOrEmpty(ev)) { var f = F(ev); var sp = GolfLook.Table["LK_SMOKE"]; sp.Tint = new Color(f[0], f[1], f[2], 1); sp.AlphaGain = f[3]; sp.SelfLight = f[4]; sp.Lit = f[5]; if (f.Length > 6) sp.FogShare = f[6]; }
                // GOLF_LOOK_HOLETINT="10:LK_FAIRWAY=r,g,b;10:LK_GREEN=r,g,b" replaces GolfLook.HoleTint entries (in memory), GOLF_LOOK_GRASS_HOLE=<8|9|10> runs ONLY that hole's grass board (v2 repair round 3: fast grass tuning)
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_HOLETINT");
                if (!string.IsNullOrEmpty(ev)) foreach (var item in ev.Split(';')) { int c = item.IndexOf(':'), q = item.IndexOf('='); if (c > 0 && q > c) { var f = F(item.Substring(q + 1)); GolfLook.HoleTint[(int.Parse(item.Substring(0, c)), item.Substring(c + 1, q - c - 1))] = new Color(f[0], f[1], f[2]); } }
                ev = System.Environment.GetEnvironmentVariable("GOLF_LOOK_GRASS_HOLE");
                if (!string.IsNullOrEmpty(ev)) { GolfLookBoards.Grass(int.Parse(ev), Gate, Say, ""); return; }
                Say($"INFO: TUNE overrides: lava light intensity {GolfLook.LavaLightIntensity}, basalt tint {GolfLook.Table["LK_BASALT"].Tint}, matte {GolfLook.Table["LK_BASALT"].NoSpecular}, smoke gain {GolfLook.Table["LK_SMOKE"].AlphaGain} emission {GolfLook.Table["LK_SMOKE"].SelfLight} lit {GolfLook.Table["LK_SMOKE"].Lit} fogshare {GolfLook.Table["LK_SMOKE"].FogShare}");
                GolfLookBoards.Crater(Gate, Say, "", System.Environment.GetEnvironmentVariable("GOLF_LOOK_SWEEP") == "1"); GolfLookBoards.Smoke(Gate, Say, "", System.Environment.GetEnvironmentVariable("GOLF_LOOK_SWEEP") == "1"); return;
            }
            CheckShaders();
            CheckMaterials();
            CheckAniso();
            CheckImporters();
            CheckHoles();
            CheckImportRulesProof();
            bool sweep = System.Environment.GetEnvironmentVariable("GOLF_LOOK_SWEEP") == "1";
            GolfLookBoards.Suite(Gate, Say, "", sweep);      // Game-view numbers on test boards under the ACTIVE pipeline asset
            GolfLookBoards.NoPost(Gate, Say);
            GolfLookBoards.BothAssets(Gate, Say, sweep);     // the same suite under TennisURP and HeroBaseStudioURP
        }

        static bool Magenta(Shader s) => !s || !s.isSupported || ShaderUtil.ShaderHasError(s) || s.name == "Hidden/InternalErrorShader";

        static void CheckShaders()
        {
            foreach (var (label, shader) in new[] {
                ("GolfArcade/GolfSurf", Resources.Load<Shader>("Course/Shaders/GolfSurf")),
                ("GolfArcade/GolfLava", Resources.Load<Shader>("Course/Shaders/GolfLava")),
                ("GolfArcade/GolfPlants", Resources.Load<Shader>("Course/Shaders/GolfPlants")),
                ("GolfArcade/TennisWater", Resources.Load<Shader>("Tennis/Shaders/TennisWater")),
                ("GolfArcade/TennisSky", Resources.Load<Shader>("Tennis/Shaders/TennisSky")),
                ("Universal Render Pipeline/Lit", Shader.Find("Universal Render Pipeline/Lit")) })
            {
                string messages = shader ? string.Join(" | ", ShaderUtil.GetShaderMessages(shader).Select(m => $"{m.severity}: {m.message} ({m.platform})").ToArray()) : "";
                Gate("SHADER_OK " + label, shader && shader.name == label && !Magenta(shader),
                    shader ? $"found '{shader.name}', supported {shader.isSupported}, errors {ShaderUtil.ShaderHasError(shader)}, graphics API {SystemInfo.graphicsDeviceType}{(messages.Length > 0 ? ", messages: " + messages : "")}" : "not found");
            }
        }

        static void CheckMaterials()
        {
            GolfLook.ClearCache();
            var missingTextures = new List<string>();
            foreach (var kv in GolfLook.Table)
            {
                string name = kv.Key; var spec = kv.Value;
                var m = GolfLook.Get(name);
                string shader = m && m.shader ? m.shader.name : "(none)";
                bool magenta = !m || Magenta(m.shader);
                var problems = new List<string>();
                if (magenta) problems.Add("shader unsupported (magenta)");
                if (m && m.name != name) problems.Add($"material name '{m.name}'");
                switch (spec.Kind)
                {
                    case GolfLook.Kind.Water:
                        if (shader != "GolfArcade/TennisWater") problems.Add("shader " + shader);
                        break;
                    case GolfLook.Kind.Lava:
                        if (shader != "GolfArcade/GolfLava") problems.Add("shader " + shader);
                        foreach (var tp in new[] { "_BaseMap", "_EmissionMap", "_BumpMap" }) if (!m.GetTexture(tp)) { problems.Add(tp + " null"); missingTextures.Add(spec.Tex + tp); }
                        // v2 repair round 3: the lava takes only part of the (now neutral) Crater fog, so the cone lava 430 yd out stays orange: _FogShare must exist and equal the table
                        if (!m.HasProperty("_FogShare")) problems.Add("GolfLava has no _FogShare property");
                        else if (Mathf.Abs(m.GetFloat("_FogShare") - spec.FogShare) > 1e-4f) problems.Add($"_FogShare {m.GetFloat("_FogShare"):F2} != table {spec.FogShare:F2}");
                        foreach (var stop in new[] { "_Deep", "_Crust", "_Flow", "_Hot" })
                        {
                            Color.RGBToHSV(m.GetColor(stop), out var sh, out var ss, out var sv); sh *= 360;
                            if (sh < GolfLookBoards.LavaHueMin || sh > GolfLookBoards.LavaHueMax || ss < GolfLookBoards.LavaSatMin || (sv < GolfLookBoards.LavaValMin && stop != "_Deep"))
                                problems.Add($"ramp stop {stop} hue {sh:F0} S {ss:F2} V {sv:F2} outside the lava band (hue {GolfLookBoards.LavaHueMin}..{GolfLookBoards.LavaHueMax}, S >= {GolfLookBoards.LavaSatMin}, V >= {GolfLookBoards.LavaValMin})");
                        }
                        break;
                    case GolfLook.Kind.Surf:
                        if (shader != "GolfArcade/GolfSurf") problems.Add("shader " + shader);
                        if (!m.GetTexture("_MainTex")) { problems.Add("_MainTex null"); missingTextures.Add(spec.Tex + "_C"); }
                        if (!m.enableInstancing) problems.Add("instancing off");
                        // v2 repair round 1: _FogShare must exist on the shader and carry the table value (smoke: a far plume keeps 70 % of its colour, RUNTIME.md v2 repair round 1)
                        if (!m.HasProperty("_FogShare")) problems.Add("shader has no _FogShare property");
                        else if (Mathf.Abs(m.GetFloat("_FogShare") - spec.FogShare) > 1e-4f) problems.Add($"_FogShare {m.GetFloat("_FogShare"):F2} != table {spec.FogShare:F2}");
                        // v2 repair round 3: _SoftFade (soft intersection of a card with the geometry behind it, smoke) must exist on the shader and carry the table value
                        if (!m.HasProperty("_SoftFade")) problems.Add("shader has no _SoftFade property");
                        else if (Mathf.Abs(m.GetFloat("_SoftFade") - spec.SoftFade) > 1e-4f) problems.Add($"_SoftFade {m.GetFloat("_SoftFade"):F2} != table {spec.SoftFade:F2}");
                        break;
                    case GolfLook.Kind.Plants:
                        // v2 2026-10-05 (area U): LK_PLANTS = GolfArcade/GolfPlants with exactly what URP Lit gave it: palette atlas, tint, smoothness, metallic 0, no normal map
                        if (shader != "GolfArcade/GolfPlants") problems.Add("shader " + shader + " (want GolfArcade/GolfPlants)");
                        if (!m.GetTexture("_BaseMap")) { problems.Add("_BaseMap null"); missingTextures.Add(spec.Tex + "_C"); }
                        else if (m.GetTexture("_BaseMap").name != spec.Tex + "_C") problems.Add($"_BaseMap is '{m.GetTexture("_BaseMap").name}', want {spec.Tex}_C");
                        { var tint = m.GetColor("_BaseColor"); if (Mathf.Abs(tint.r - spec.Tint.r) > 1e-3f || Mathf.Abs(tint.g - spec.Tint.g) > 1e-3f || Mathf.Abs(tint.b - spec.Tint.b) > 1e-3f) problems.Add($"tint {Hex(tint)} != table {Hex(spec.Tint)}"); }
                        if (Mathf.Abs(m.GetFloat("_Smoothness") - spec.Smoothness) > 1e-4f) problems.Add($"smoothness {m.GetFloat("_Smoothness"):F2} != table {spec.Smoothness:F2}");
                        if (Mathf.Abs(m.GetFloat("_Metallic")) > 1e-4f) problems.Add("metallic " + m.GetFloat("_Metallic"));
                        if (m.IsKeywordEnabled("_NORMALMAP") || m.IsKeywordEnabled("_EMISSION")) problems.Add("a Lit keyword is on (_NORMALMAP / _EMISSION): the plants have neither");
                        if (!m.enableInstancing) problems.Add("instancing off");
                        break;
                    default:
                        if (shader != "Universal Render Pipeline/Lit") problems.Add("shader " + shader);
                        if (!m.GetTexture("_BaseMap")) { problems.Add("_BaseMap null"); missingTextures.Add(spec.Tex + "_C"); }
                        if (spec.Normal)
                        {
                            if (!m.GetTexture("_BumpMap")) { problems.Add("_BumpMap null"); missingTextures.Add(spec.Tex + "_N"); }
                            else if (!m.IsKeywordEnabled("_NORMALMAP")) problems.Add("_NORMALMAP keyword off");
                        }
                        if (spec.Emissive)
                        {
                            if (!m.GetTexture("_EmissionMap")) { problems.Add("_EmissionMap null"); missingTextures.Add(spec.Tex + "_E"); }
                            if (!m.IsKeywordEnabled("_EMISSION")) problems.Add("_EMISSION keyword off");
                            // post renders nowhere (no PostProcessData on the renderer): a neutral HDR multiplier clips G with R and turns the
                            // fissures cream. The multiplier must be warm (R >= G >= B) and keep G <= 1; the rendered hue is gated in
                            // GolfLookBoards.Crater (LAVA_ORANGE_NO_BLOOM / BASALT_GLOW_ORANGE), not here.
                            Vector4 e = m.GetVector("_EmissionColor");
                            if (!(e.x > 0 && e.x >= e.y && e.y >= e.z)) problems.Add($"emission multiplier ({e.x:F2},{e.y:F2},{e.z:F2}) not warm (R >= G >= B > 0)");
                            if (e.y > 1.0001f) problems.Add($"emission G {e.y:F2} > 1 (clips the fissure hue toward cream without tonemapping)");
                        }
                        float s = m.GetFloat("_Smoothness");
                        if (Mathf.Abs(s - spec.Smoothness) > 1e-4f) problems.Add($"smoothness {s:F2}");
                        if (!m.enableInstancing) problems.Add("instancing off");
                        break;
                }
                string detail = $"shader '{shader}'" +
                    (spec.Kind == GolfLook.Kind.Lit ? $", _BaseMap {Tex(m, "_BaseMap")}, _BumpMap {Tex(m, "_BumpMap")}, _EmissionMap {Tex(m, "_EmissionMap")}, S {m.GetFloat("_Smoothness"):F2}" +
                        (spec.Emissive ? $", emission (linear) {(Vector3)(Vector4)m.GetVector("_EmissionColor")}" : "") : "") +
                    (spec.Kind == GolfLook.Kind.Surf ? $", _MainTex {Tex(m, "_MainTex")}" : "") +
                    (spec.Kind == GolfLook.Kind.Plants ? $", _BaseMap {Tex(m, "_BaseMap")}, tint {Hex(m.GetColor("_BaseColor"))}, S {m.GetFloat("_Smoothness"):F2}, instancing {m.enableInstancing}" : "") +
                    (spec.Kind == GolfLook.Kind.Lava ? $", _BaseMap {Tex(m, "_BaseMap")}, _EmissionMap {Tex(m, "_EmissionMap")}, _BumpMap {Tex(m, "_BumpMap")}, ramp {Hex(m.GetColor("_Deep"))} / {Hex(m.GetColor("_Crust"))} / {Hex(m.GetColor("_Flow"))} / {Hex(m.GetColor("_Hot"))}" : "") +
                    (spec.Kind == GolfLook.Kind.Water ? $", _Shallow {Hex(m.GetColor("_Shallow"))}, _Deep {Hex(m.GetColor("_Deep"))}, _DeepDistance {m.GetFloat("_DeepDistance")}" : "") +
                    (problems.Count > 0 ? " | PROBLEMS: " + string.Join("; ", problems.ToArray()) : "");
                Gate("MATERIAL " + name, problems.Count == 0, detail);
            }
            Say("INFO: missing Look textures: " + (missingTextures.Count == 0 ? "none" : string.Join(", ", missingTextures.Distinct().ToArray())));
        }

        /// v2 repair round 2 (review, low, hole 10 lava-rim still: the far fairway smeared into horizontal streaks): every TILING look texture a material carries (ground, rock, basalt, lava; not the
        /// sky, the plant atlas or the alpha cards) is filtered at GolfLook.Aniso (16) at runtime, whatever the importer says (it says 4: a re-import resets it). The importer gate
        /// (LOOK_TEXTURE_IMPORT, >= 4) is unchanged. The active quality level must also allow per-texture aniso (QualitySettings.anisotropicTextures: Disabled on Very Low / Low), reported as INFO: it is the project's.
        static void CheckAniso()
        {
            GolfLook.ClearCache();
            int want = Mathf.Clamp(GolfLook.Aniso, 1, 16);
            var seen = new HashSet<Texture>(); var low = new List<string>(); var ok = new List<string>();
            foreach (var kv in GolfLook.Table)
            {
                if (kv.Value.Kind == GolfLook.Kind.Water) continue;
                var m = GolfLook.Get(kv.Key);
                foreach (var tp in new[] { "_BaseMap", "_BumpMap", "_EmissionMap", "_MainTex" })
                {
                    if (!m || !m.HasProperty(tp)) continue;
                    var t = m.GetTexture(tp); if (!t || !seen.Add(t)) continue;
                    string n = t.name; bool tiles = !(n.StartsWith("Sky_") || n.StartsWith("Plants_") || n.StartsWith("Surf_") || n.StartsWith("Fall_") || n.StartsWith("Smoke_"));
                    if (!tiles) continue;
                    if (t.anisoLevel >= want && t.wrapMode == TextureWrapMode.Repeat) ok.Add(n); else low.Add($"{n} aniso {t.anisoLevel} wrap {t.wrapMode}");
                }
            }
            Gate("LOOK_TEXTURE_ANISO_RUNTIME", ok.Count >= 20 && low.Count == 0,
                $"{ok.Count} tiling look textures at aniso >= {want} after GolfLook.Get on every table row" + (low.Count > 0 ? ", below: " + string.Join("; ", low.ToArray()) : "") + (ok.Count < 20 ? $" (need >= 20 textures measured)" : ""));
            Say($"INFO: active quality level anisotropic filtering = {QualitySettings.anisotropicFiltering} (Enable = per texture, ForceEnable = all, Disable = the texture level is ignored)");
        }

        static string Tex(Material m, string p) => m && m.HasProperty(p) && m.GetTexture(p) ? m.GetTexture(p).name : "null";
        static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        static void CheckImporters()
        {
            foreach (var n in new[] { "08", "09", "10" })
            {
                string path = $"Assets/Resources/Course/hole_{n}.fbx";
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                Gate($"FBX_META hole_{n}", mi && mi.isReadable && mi.importTangents == ModelImporterTangents.CalculateMikk,
                    mi ? $"isReadable {mi.isReadable}, importTangents {mi.importTangents} (meta tangentImportMode {(int)mi.importTangents})" : "no ModelImporter");
            }
            string folder = GolfLookTextureImporter.Folder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder)) { Gate("LOOK_TEXTURE_IMPORT", false, folder + " does not exist yet"); return; }
            var bad = new List<string>(); int count = 0; long bytes = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var t = AssetImporter.GetAtPath(p) as TextureImporter; if (!t) continue;
                count++; bytes += new FileInfo(p).Length;
                string f = Path.GetFileNameWithoutExtension(p);
                bool normal = f.EndsWith("_N"), clamp = f.StartsWith("Sky_") || f.StartsWith("Plants_");
                var why = new List<string>();
                if (normal != (t.textureType == TextureImporterType.NormalMap)) why.Add("type " + t.textureType);
                if (t.sRGBTexture == normal) why.Add("sRGB " + t.sRGBTexture);
                if (t.wrapMode != (clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat)) why.Add("wrap " + t.wrapMode);
                if (!t.mipmapEnabled) why.Add("no mips");
                if (t.crunchedCompression) why.Add("crunched");
                if (t.textureCompression == TextureImporterCompression.Uncompressed) why.Add("uncompressed");
                if (t.anisoLevel < 4) why.Add("aniso " + t.anisoLevel);
                if (why.Count > 0) bad.Add(f + " (" + string.Join(", ", why.ToArray()) + ")");
            }
            Gate("LOOK_TEXTURE_IMPORT", count > 0 && bad.Count == 0, $"{count} texture(s) under {folder}, {bytes / 1048576f:F2} MB on disk" + (bad.Count > 0 ? ", wrong: " + string.Join("; ", bad.ToArray()) : ""));
            Gate("LOOK_FOLDER_SIZE", bytes <= 25L * 1024 * 1024, $"{bytes / 1048576f:F2} MB <= 25 MB");
        }

        internal static Dictionary<string, Color> Palette() =>
            (Dictionary<string, Color>)typeof(HoleView).GetField("Palette", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);


        // ------------------------------------------------------------------ PostcardImportCheck's install-only lines, proven on a scratch hole

        /// GROUND_ALBEDO_AND_NORMAL and NO_FLAT_POSTCARD_GROUND can only run on a real rebuilt FBX (the flat baseline has no LK_ ground), so they are
        /// proven here on a SCRATCH hole model (primitive quads named and materialled like a rebuilt hole 10, instantiated and dressed exactly as
        /// HoleView does): the lines must PASS on it and each of three injected defects must fail the intended line.
        static void CheckImportRulesProof()
        {
            GameObject Scratch(bool flatFairwayName, string foam = null)
            {
                var root = new GameObject("scratch hole 10");
                var litShader = Shader.Find("Universal Render Pipeline/Lit");
                GameObject Part(string objName, params string[] materials)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Quad); go.name = objName; go.transform.SetParent(root.transform, false);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.GetComponent<MeshRenderer>().sharedMaterials = materials.Select(n => new Material(litShader) { name = n }).ToArray();
                    return go;
                }
                Part("TERRAIN_CRATER", "LK_ROUGH", "LK_CLIFF", "LK_CLIFF_DARK");
                Part("FAIRWAY", flatFairwayName ? "MAT_FAIRWAY" : "LK_FAIRWAY");
                Part("GREEN", "LK_GREEN"); Part("BUNKER_01", "LK_SAND"); Part("ROCK_STACK_01", "LK_ROCK");
                // a wall piece 18 yd wide 15 yd from a lava light (the chunking rule), the lava lake (120 yd = 4.6 tiles, a unit quad's UVs), and four LAVA_LIGHT empties over it
                var wallPart = Part("ROCK_WALL_01", "LK_BASALT"); wallPart.transform.position = new Vector3(-45, 6, -20); wallPart.transform.rotation = Quaternion.Euler(0, 90, 0); wallPart.transform.localScale = new Vector3(18, 12, 1);
                Part("PLANT_TUFT_01", "LK_PLANTS"); Part("WATER_OCEAN", "LK_WATER");
                var lavaPart = Part("WATER_LAVA", "LK_LAVA"); lavaPart.transform.rotation = Quaternion.Euler(90, 0, 0); lavaPart.transform.localScale = new Vector3(120, 120, 1);
                Part("FLAG", "MAT_FLAG");
                foreach (var (x, z) in new[] { (-30f, -30f), (30f, -30f), (-30f, 30f), (30f, 30f) })
                { var e = new GameObject($"LAVA_LIGHT_{root.transform.childCount:00}"); e.transform.SetParent(root.transform, false); e.transform.position = new Vector3(x, 2.4f, z); }
                // two short surf patches (6 x 2 yd, 40 yd apart): whitewater that is NOT a strip must pass
                foreach (var x in new[] { -20f, 20f }) { var patch = Part($"WATER_SURF_{(x < 0 ? "01" : "02")}", "LK_SURF"); patch.transform.rotation = Quaternion.Euler(90, 0, 0); patch.transform.position = new Vector3(x, .1f, 75); patch.transform.localScale = new Vector3(6, 2, 1); }
                if (foam == "strip") { var strip = Part("WATER_SURF_03", "LK_SURF"); strip.transform.rotation = Quaternion.Euler(90, 0, 0); strip.transform.position = new Vector3(0, .1f, 80); strip.transform.localScale = new Vector3(60, 3, 1); }   // a continuous 60 yd band
                if (foam == "close") { var near = Part("WATER_SURF_03", "LK_SURF"); near.transform.rotation = Quaternion.Euler(90, 0, 0); near.transform.position = new Vector3(-12, .1f, 75); near.transform.localScale = new Vector3(6, 2, 1); }   // 3 yd from patch 01: they merge
                if (foam == "matfoam") Part("WATER_FOAM", "MAT_FOAM");                                                                                                                                                                      // the flat baseline's white band
                return root;
            }
            Dictionary<string, bool> Run(GameObject prefab, System.Action<GameObject> tamper = null)
            {
                var model = Object.Instantiate(prefab);
                try
                {
                    var pal = Palette();
                    foreach (var rr in model.GetComponentsInChildren<Renderer>(true))      // HoleView's palette rule for the non-LK names (gameplay MAT_FLAG, a leftover MAT_FAIRWAY)
                    {
                        var ms = rr.sharedMaterials;
                        for (int i = 0; i < ms.Length; i++) if (ms[i] && pal.TryGetValue(ms[i].name.Replace(" (Instance)", ""), out var pc)) ms[i] = HoleView.Mat(pc);
                        rr.sharedMaterials = ms;
                    }
                    GolfLook.DressModel(model, 10);
                    tamper?.Invoke(model);
                    var lines = new Dictionary<string, bool>();
                    PostcardLookMaterialRules.Check(10, prefab, model.transform, Palette(), (n, ok, d) => lines[n] = ok, _ => { }, true);
                    return lines;
                }
                finally { Object.DestroyImmediate(model); }
            }
            var good = Scratch(false); var bad = Scratch(true); var withStrip = Scratch(false, "strip"); var withClose = Scratch(false, "close"); var withMatFoam = Scratch(false, "matfoam");
            try
            {
                var ok = Run(good);
                string[] want = { "PALETTE_OVERRIDES_APPLIED", "PALETTE_NO_LEAKS", "GROUND_ALBEDO_AND_NORMAL", "NO_FLAT_POSTCARD_GROUND", "NO_FOAM_STRIP_8_10", "LAVA_RENDERER_PRESENT", "LAVA_MAPPING_DENSITY", "LAVA_LIGHTS_PLACED", "ROCK_NEAR_LAVA_CHUNKED", "LAVA_READS_ORANGE", "LAVA_SHADER_RENDERS_IN_PIPELINE" };
                bool allPass = want.All(w => ok.TryGetValue(w, out var v) && v);
                // defect 1: a rebuilt-looking hole whose fairway is still the flat MAT_FAIRWAY -> NO_FLAT_POSTCARD_GROUND (and GROUND_ALBEDO_AND_NORMAL: no LK_FAIRWAY) fail
                var d1 = Run(bad);
                // defect 2: a ground slot swapped to a flat URP Lit after the dressing -> PALETTE_OVERRIDES_APPLIED fails
                var d2 = Run(good, m => m.transform.Find("GREEN").GetComponent<MeshRenderer>().sharedMaterial = HoleView.Mat(new Color(.6f, .9f, .3f)));
                // defect 3: a ground slot loses its normal map -> GROUND_ALBEDO_AND_NORMAL fails
                var d3 = Run(good, m => { var r = m.transform.Find("ROCK_STACK_01").GetComponent<MeshRenderer>(); var c = new Material(r.sharedMaterial); c.SetTexture("_BumpMap", null); c.DisableKeyword("_NORMALMAP"); r.sharedMaterial = c; });
                // defect 4: foam as a 60 yd band (WATER_SURF drawing LK_SURF), two patches 3 yd apart, and the flat baseline's WATER_FOAM / MAT_FOAM -> NO_FOAM_STRIP_8_10 fails each time
                var d4 = Run(withStrip); var d4b = Run(withClose); var d4c = Run(withMatFoam);
                // defect 5: the lava goes back to the mesh UVs, which here are a unit quad over 120 yd (a few huge swirls / flat orange) -> LAVA_MAPPING_DENSITY fails
                var d5 = Run(good, m => { var r = m.transform.Find("WATER_LAVA").GetComponent<MeshRenderer>(); var c = new Material(r.sharedMaterial); c.SetFloat("_WorldUV", 0); r.sharedMaterial = c; });
                // defect 6: the wall beside the lights becomes one 70 yd mesh -> ROCK_NEAR_LAVA_CHUNKED fails
                var d6 = Run(good, m => m.transform.Find("ROCK_WALL_01").localScale = new Vector3(70, 12, 1));
                // defect 7: two of the four LAVA_LIGHT empties are gone -> LAVA_LIGHTS_PLACED fails
                var d7 = Run(good, m => { foreach (var t in m.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("LAVA_LIGHT") && !t.GetComponent<Light>()).Take(2).ToArray()) Object.DestroyImmediate(t.gameObject); });
                // defect 8: the lava shrinks to 30 yd = 1.1 tiles -> LAVA_MAPPING_DENSITY fails (the swirl cannot show)
                var d8 = Run(good, m => m.transform.Find("WATER_LAVA").localScale = new Vector3(30, 30, 1));
                bool F(Dictionary<string, bool> d, string line) => d.TryGetValue(line, out var a) && !a;
                bool f1 = F(d1, "NO_FLAT_POSTCARD_GROUND"), f2 = F(d2, "PALETTE_OVERRIDES_APPLIED"), f3 = F(d3, "GROUND_ALBEDO_AND_NORMAL"), f4 = F(d4, "NO_FOAM_STRIP_8_10") && F(d4b, "NO_FOAM_STRIP_8_10") && F(d4c, "NO_FOAM_STRIP_8_10"), f5 = F(d5, "LAVA_MAPPING_DENSITY"),
                     f6 = F(d6, "ROCK_NEAR_LAVA_CHUNKED"), f7 = F(d7, "LAVA_LIGHTS_PLACED"), f8 = F(d8, "LAVA_MAPPING_DENSITY");
                Gate("IMPORTCHECK_INSTALL_LINES_PROVEN", allPass && f1 && f2 && f3 && f4 && f5 && f6 && f7 && f8,
                    $"scratch hole 10 (primitive quads with LK_ materials, a 120 yd lava quad, 4 LAVA_LIGHT empties, an 18 yd wall piece, two 6 yd surf patches 40 yd apart (legit whitewater: must PASS); dressed like HoleView): all {want.Length} LK-aware lines PASS = {allPass}{(allPass ? "" : " (not passing: " + string.Join(", ", want.Where(w => !ok.TryGetValue(w, out var v) || !v).ToArray()) + "; all lines: " + string.Join(", ", ok.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + ")")}; injected defects fail the intended line: flat MAT_FAIRWAY -> NO_FLAT_POSTCARD_GROUND {f1}, flat slot -> PALETTE_OVERRIDES_APPLIED {f2}, no normal map -> GROUND_ALBEDO_AND_NORMAL {f3}, 60 yd surf band / two patches 3 yd apart / MAT_FOAM -> NO_FOAM_STRIP_8_10 {f4}, lava on the mesh UVs (unit quad over 120 yd) -> LAVA_MAPPING_DENSITY {f5}, 70 yd wall beside the lights -> ROCK_NEAR_LAVA_CHUNKED {f6}, 2 of 4 lava-light empties deleted -> LAVA_LIGHTS_PLACED {f7}, 30 yd lava (1.1 tiles) -> LAVA_MAPPING_DENSITY {f8}");
            }
            finally { Object.DestroyImmediate(good); Object.DestroyImmediate(bad); Object.DestroyImmediate(withStrip); Object.DestroyImmediate(withClose); Object.DestroyImmediate(withMatFoam); }
        }

        /// Build each hole through HoleView (edit mode) and classify every material slot the game will draw.
        static void CheckHoles()
        {
            var palette = Palette();
            var parent = new GameObject("GolfLookSmoke");
            try
            {
                // hole 7: the old rule exactly (Palette name -> flat colour, nothing else touched)
                var h7 = GolfArcade.Course.Course.Cliffside().Holes[0];
                var proto7 = Resources.Load<GameObject>($"Course/hole_{h7.Number:00}");
                var v7 = HoleView.Build(h7, parent.transform);
                var live7 = v7.GetComponentsInChildren<Renderer>(true).Where(r => r.transform.IsChildOf(v7.transform.Find("Course model") ?? v7.transform)).ToArray();
                var src7 = proto7 ? proto7.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                int same = 0, flat = 0, wrong = 0;
                if (src7.Length == live7.Length)
                    for (int i = 0; i < src7.Length; i++)
                    {
                        var a = src7[i].sharedMaterials; var b = live7[i].sharedMaterials;
                        for (int j = 0; j < a.Length; j++)
                        {
                            if (!a[j]) continue;
                            string n = a[j].name.Replace(" (Instance)", "");
                            if (palette.TryGetValue(n, out var c)) { if (b[j] == HoleView.Mat(c)) flat++; else wrong++; }
                            else if (b[j] == a[j]) same++; else wrong++;
                        }
                    }
                else wrong = -1;
                Gate("HOLE7_UNCHANGED_RULE", proto7 && wrong == 0 && flat > 0, $"hole 7: {flat} palette slot(s) flat as before, {same} slot(s) kept as imported, {wrong} different (renderers {src7.Length}/{live7.Length})");
                Object.DestroyImmediate(v7.gameObject);

                foreach (var hole in GolfArcade.Course.Course.Postcards().Holes)
                {
                    var prefab = Resources.Load<GameObject>($"Course/hole_{hole.Number:00}");
                    var view = HoleView.Build(hole, parent.transform);
                    var model = view.transform.Find("Course model");
                    if (!model) { Gate($"HOLE{hole.Number}_MODEL", false, "no Course model (FBX missing?)"); Object.DestroyImmediate(view.gameObject); continue; }
                    CheckModel(hole.Number, model, "", Gate, Say);
                    // the LK-aware palette / lava rules PostcardImportCheck uses (PostcardLookMaterialRules, in PostcardImportCheck.cs)
                    PostcardLookMaterialRules.Check(hole.Number, prefab, model, palette, (n, ok, d) => Gate($"LK_RULES hole{hole.Number} {n}", ok, d), Say, false);   // false: the flat baseline FBX is still installed, install-only lines are INFO
                    Object.DestroyImmediate(view.gameObject);
                }
            }
            finally { Object.DestroyImmediate(parent); }
        }

        /// The structural gates on one dressed course model (HoleView output): water never flat, no LK_ slot flat, no prop colliders.
        internal static void CheckModel(int holeNumber, Transform model, string tag, System.Action<string, bool, string> gate, System.Action<string> say)
        {
            var palette = Palette();
            var flatSet = new HashSet<Material>(palette.Values.Select(HoleView.Mat));
            int lk = 0, lkTextured = 0, water = 0, waterWrong = 0, flatPalette = 0, kept = 0, lights = 0, surf = 0;
            var flatNames = new HashSet<string>(); var waterBad = new List<string>(); var lkFlat = new List<string>(); var plantsWrong = new List<string>(); int plantsOk = 0;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    string sh = m.shader ? m.shader.name : "";
                    bool isWater = r.name.StartsWith("WATER");
                    if (GolfLook.Handles(m.name))
                    {
                        lk++;
                        if (sh == "Universal Render Pipeline/Lit") { if (m.GetTexture("_BaseMap")) lkTextured++; else lkFlat.Add(m.name); }
                        if (sh == "GolfArcade/GolfSurf") surf++;
                        if (sh == "GolfArcade/GolfPlants") { if (m.GetTexture("_BaseMap")) { lkTextured++; plantsOk++; } else lkFlat.Add(m.name); }
                        else if (m.name.StartsWith("LK_PLANTS")) plantsWrong.Add(r.name + " '" + sh + "'");
                    }
                    else if (flatSet.Contains(m)) { flatPalette++; flatNames.Add("#" + ColorUtility.ToHtmlStringRGB(m.color)); }
                    else kept++;
                    if (isWater)
                    {
                        water++;
                        bool ok = r.name.StartsWith("WATER_LAVA") ? m.name.StartsWith("LK_LAVA") || m.GetTexture("_EmissionMap") || m.IsKeywordEnabled("_EMISSION")
                                : sh == "GolfArcade/TennisWater" || sh == "GolfArcade/GolfSurf";
                        if (!ok) { waterWrong++; waterBad.Add(r.name + "/" + m.name + " '" + sh + "'"); }
                    }
                }
            foreach (var l in model.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Point) lights++;
            var colliders = model.GetComponentsInChildren<Collider>(true).Where(c => !(c.name.StartsWith("TERRAIN") || c.name.StartsWith("FAIRWAY") || c.name.StartsWith("GREEN") || c.name.StartsWith("TEE_BOX") || c.name.StartsWith("BUNKER") || c.name.StartsWith("CART_PATH"))).Select(c => c.name).ToArray();
            say($"INFO: {tag}hole {holeNumber}: {lk} LK_ slot(s) ({lkTextured} textured URP Lit, {surf} GolfSurf), {flatPalette} flat palette slot(s) [{string.Join(", ", flatNames.OrderBy(x => x).ToArray())}], {kept} kept as imported, {water} water slot(s), {lights} lava point light(s)");
            gate($"{tag}HOLE{holeNumber}_WATER_NEVER_FLAT", water > 0 && waterWrong == 0, $"{water} WATER* slot(s), {waterWrong} not TennisWater/GolfSurf/emissive lava" + (waterBad.Count > 0 ? ": " + string.Join("; ", waterBad.Take(6).ToArray()) : ""));
            gate($"{tag}HOLE{holeNumber}_LK_NOT_FLAT", lkFlat.Count == 0, $"{lk} LK_ slot(s), {lkFlat.Count} URP Lit LK_ slot(s) without a base map" + (lkFlat.Count > 0 ? ": " + string.Join(", ", lkFlat.Distinct().Take(8).ToArray()) : ""));
            // v2 2026-10-05 (area U): every LK_PLANTS slot of a postcard hole is the sway shader (a plant on URP Lit would stand still while its neighbours move)
            gate($"{tag}HOLE{holeNumber}_PLANTS_SWAY_SHADER", plantsWrong.Count == 0, $"{plantsOk} LK_PLANTS slot(s) on GolfArcade/GolfPlants with a base map" + (plantsWrong.Count > 0 ? $", {plantsWrong.Count} on another shader: " + string.Join("; ", plantsWrong.Take(6).ToArray()) : "") + (plantsOk == 0 ? " (hole carries no LK_PLANTS slot)" : ""));
            gate($"{tag}HOLE{holeNumber}_NO_PROP_COLLIDERS", colliders.Length == 0, colliders.Length == 0 ? "colliders only on TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER/CART_PATH" : "colliders on: " + string.Join(", ", colliders.Take(8).ToArray()));
        }
    }

    /// Pixel measurements shared by the smoke, the variants and the probe (sRGB bytes as read back from the camera target).
    internal static class LookPixels
    {
        public struct Stats
        {
            public int Count;
            public Color Mean, Top, Bottom;      // 0..1 sRGB
            public float TopHue, TopGR, Cream, ClipR, MedianLum;
        }

        public static float Lum(Color32 c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;
        public static Color32 ToColor32(Color c) => new((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255), 255);
        public static string Rgb(Color c) => $"({Mathf.RoundToInt(c.r * 255)},{Mathf.RoundToInt(c.g * 255)},{Mathf.RoundToInt(c.b * 255)})";
        public static float Hue(Color c) { Color.RGBToHSV(c, out var h, out _, out _); return h * 360; }

        public static Color MeanOf(IList<Color32> px, int from, int to)
        {
            double r = 0, g = 0, b = 0; int n = Mathf.Max(1, to - from);
            for (int i = from; i < to; i++) { r += px[i].r; g += px[i].g; b += px[i].b; }
            return new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255));
        }

        /// Sorted by luminance: mean, the brightest `topFraction` (hue, G/R), the darker half, cream share (min channel >= 230),
        /// share with R clipped (>= 250), median luminance.
        public static Stats Analyse(List<Color32> px, float topFraction)
        {
            var s = new Stats { Count = px.Count };
            if (px.Count == 0) return s;
            px.Sort((a, b) => Lum(a).CompareTo(Lum(b)));
            int n = px.Count, top = Mathf.Max(1, Mathf.RoundToInt(n * topFraction));
            s.Mean = MeanOf(px, 0, n);
            s.Top = MeanOf(px, n - top, n);
            s.Bottom = MeanOf(px, 0, Mathf.Max(1, n / 2));
            s.TopHue = Hue(s.Top);
            s.TopGR = s.Top.r > 0 ? s.Top.g / s.Top.r : 0;
            int cream = 0, clipR = 0;
            foreach (var p in px) { if (Mathf.Min(p.r, Mathf.Min(p.g, p.b)) >= 230) cream++; if (p.r >= 250) clipR++; }
            s.Cream = cream / (float)n; s.ClipR = clipR / (float)n;
            s.MedianLum = Lum(px[n / 2]);
            return s;
        }

        /// Rows [y0, y1) of a bottom-up image.
        public static List<Color32> Rows(Color32[] px, int w, int h, int y0, int y1)
        {
            var list = new List<Color32>((y1 - y0) * w);
            for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++) for (int x = 0; x < w; x++) list.Add(px[y * w + x]);
            return list;
        }

        public static List<Color32> Masked(Color32[] px, bool[] mask)
        {
            var list = new List<Color32>();
            for (int i = 0; i < px.Length; i++) if (mask[i]) list.Add(px[i]);
            return list;
        }

        /// Pixels that differ between two renders by more than `threshold` (sum of |dR|+|dG|+|dB|).
        public static bool[] Diff(Color32[] a, Color32[] b, int threshold, out int count)
        {
            var mask = new bool[a.Length]; count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b) > threshold) { mask[i] = true; count++; }
            return mask;
        }

        /// Share of `block` x `block` tiles in rows [y0, y1) whose luminance standard deviation is below `stdMax` (a flat-colour
        /// surface reads ~0-1; textured grass, rock or water several levels). With a mask only tiles fully inside it count.
        public static (int flat, int total) Flat(Color32[] px, int w, int h, int y0, int y1, int block = 20, float stdMax = 1.5f, bool[] mask = null)
        {
            int flat = 0, total = 0;
            for (int by = Mathf.Max(0, y0); by + block <= Mathf.Min(h, y1); by += block)
                for (int bx = 0; bx + block <= w; bx += block)
                {
                    double sum = 0, sq = 0; bool inside = true;
                    for (int y = by; y < by + block && inside; y++)
                        for (int x = bx; x < bx + block; x++)
                        {
                            int i = y * w + x;
                            if (mask != null && !mask[i]) { inside = false; break; }
                            float l = Lum(px[i]); sum += l; sq += l * l;
                        }
                    if (!inside) continue;
                    int n = block * block;
                    double mean = sum / n, std = System.Math.Sqrt(System.Math.Max(0, sq / n - mean * mean));
                    total++; if (std < stdMax) flat++;
                }
            return (flat, total);
        }

        public static Color32[] Render(Camera cam, RenderTexture rt, Texture2D tex)
        {
            var previous = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = null; cam.targetTexture = previous;
            return tex.GetPixels32();
        }

        public static void Save(Texture2D tex, string dir, string file)
        {
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToJPG(93));
        }

        /// A textured quad (a, b, c, d counter-clockwise seen from `outward`), UV in tiles of `tile` metres from world yards.
        public static GameObject Quad(GameObject root, string name, Material m, float tile, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            var n0 = Vector3.Cross(b - a, d - a).normalized;
            bool flat = Mathf.Abs(n0.y) > .7f;
            var t = (b - a).normalized;
            Vector2 UV(Vector3 p) => flat ? new Vector2(p.x, p.z) * (.9144f / tile) : new Vector2(Vector3.Dot(p, t), p.y) * (.9144f / tile);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new[] { a, b, c, d });
            mesh.SetUVs(0, new[] { UV(a), UV(b), UV(c), UV(d) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            if (Vector3.Dot(mesh.normals[0], outward) < 0) { mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        public sealed class RenderState
        {
            public Material Skybox; public UnityEngine.Rendering.AmbientMode Mode; public Color Sky, Equator, Ground, Ambient, Fog;
            public bool FogOn; public FogMode FogMode; public float FogStart, FogEnd; public Light Sun;
        }

        public static RenderState SaveRenderSettings() => new()
        {
            Skybox = RenderSettings.skybox, Mode = RenderSettings.ambientMode, Sky = RenderSettings.ambientSkyColor, Equator = RenderSettings.ambientEquatorColor,
            Ground = RenderSettings.ambientGroundColor, Ambient = RenderSettings.ambientLight, Fog = RenderSettings.fogColor, FogOn = RenderSettings.fog,
            FogMode = RenderSettings.fogMode, FogStart = RenderSettings.fogStartDistance, FogEnd = RenderSettings.fogEndDistance, Sun = RenderSettings.sun,
        };

        public static void RestoreRenderSettings(RenderState s)
        {
            RenderSettings.skybox = s.Skybox; RenderSettings.ambientMode = s.Mode; RenderSettings.ambientSkyColor = s.Sky; RenderSettings.ambientEquatorColor = s.Equator;
            RenderSettings.ambientGroundColor = s.Ground; RenderSettings.ambientLight = s.Ambient; RenderSettings.fogColor = s.Fog; RenderSettings.fog = s.FogOn;
            RenderSettings.fogMode = s.FogMode; RenderSettings.fogStartDistance = s.FogStart; RenderSettings.fogEndDistance = s.FogEnd; RenderSettings.sun = s.Sun;
        }
    }
}

namespace GolfArcade.EditorTools
{
    /// A/B renders of the real game camera for tuning the postcard atmosphere (play mode, like PostcardCameraStills):
    ///   GOLF_LOOK_VARIANTS_OUT=<dir> Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.GolfLookVariants.Run
    /// Per view it writes <hole>_<view>_full.jpg, _nofog.jpg, _nopost.jpg and logs fog/ambient/water numbers.
    [UnityEditor.InitializeOnLoad]
    public static class GolfLookVariants
    {
        const string Flag = "GolfLookVariants";
        static int warm, step = -1, wait;
        static string outDir; static UnityEngine.RenderTexture rt; static UnityEngine.Texture2D tex;
        static GolfArcade.Game.GolfGame game; static GolfArcade.Game.CameraRig rig; static UnityEngine.Camera cam;

        // (hole index, label, ball x, ball d, aim x, aim d, putting)
        public static readonly (int, string, float, float, float, float, bool)[] Views =
        {
            (0, "layup", 2f, 38f, 19f, 178f, false),
            (1, "tee", 0f, 0f, 3f, 60f, false),
            (2, "approach", 107.1f, 159.3f, 261f, 164.7f, false),
            (2, "rim", 50.2f, 123.4f, 107.1f, 159.3f, false),
        };

        static GolfLookVariants() { UnityEditor.EditorApplication.update += Tick; }

        public static void Run()
        {
            outDir = System.Environment.GetEnvironmentVariable("GOLF_LOOK_VARIANTS_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = System.IO.Path.GetFullPath("../work/postcard-look/runtime/variants");
            System.IO.Directory.CreateDirectory(outDir);
            UnityEditor.SessionState.SetString(Flag + "out", outDir);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            UnityEditor.SessionState.SetBool(Flag, true);
            UnityEditor.EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!UnityEditor.SessionState.GetBool(Flag, false) || !UnityEditor.EditorApplication.isPlaying) return;
            if (step < 0)
            {
                if (!GolfArcade.Course.HoleView.Current || ++warm < 90) return;
                outDir = UnityEditor.SessionState.GetString(Flag + "out", outDir);
                game = UnityEngine.Object.FindFirstObjectByType<GolfArcade.Game.GolfGame>();
                rig = UnityEngine.Object.FindFirstObjectByType<GolfArcade.Game.CameraRig>();
                cam = rig.Camera;
                foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>(UnityEngine.FindObjectsSortMode.None)) c.enabled = false;
                rt = new UnityEngine.RenderTexture(900, 1600, 24, UnityEngine.RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new UnityEngine.Texture2D(900, 1600, UnityEngine.TextureFormat.RGB24, false);
                step = 0; Load(); return;
            }
            if (--wait > 0) return;
            var v = Views[step];
            var hole = GolfArcade.Course.Course.Postcards().Holes[v.Item1];
            var ballAt = new GolfArcade.Course.CoursePoint(v.Item3, v.Item4);
            var ball = GolfArcade.Course.HoleView.ToWorld(ballAt, 0.06);
            var aim = GolfArcade.Course.HoleView.ToWorld(new GolfArcade.Course.CoursePoint(v.Item5, v.Item6), 0) - GolfArcade.Course.HoleView.ToWorld(ballAt, 0);
            aim.y = 0; aim.Normalize();
            game.enabled = false; rig.enabled = false;
            rig.FrameAddress(ball, aim, v.Item7); rig.SnapNext(); rig.ApplyFrame();
            UnityEngine.Debug.Log($"[GolfLookVariants] hole {hole.Number} {v.Item2}: fog {UnityEngine.RenderSettings.fog} {UnityEngine.RenderSettings.fogMode} {UnityEngine.RenderSettings.fogStartDistance}..{UnityEngine.RenderSettings.fogEndDistance} colour {UnityEngine.RenderSettings.fogColor}, ambient {UnityEngine.RenderSettings.ambientMode} sky {UnityEngine.RenderSettings.ambientSkyColor}, camera near {cam.nearClipPlane} far {cam.farClipPlane} pos {cam.transform.position}");
            foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.Renderer>(UnityEngine.FindObjectsSortMode.None))
                if (r.name.StartsWith("WATER") && r.sharedMaterial) UnityEngine.Debug.Log($"[GolfLookVariants]   {r.name}: '{r.sharedMaterial.name}' shader '{r.sharedMaterial.shader.name}' bounds {r.bounds.min}..{r.bounds.max}");
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            bool post = data.renderPostProcessing, fog = UnityEngine.RenderSettings.fog;
            Shot($"hole{hole.Number:00}_{v.Item2}_full");
            UnityEngine.RenderSettings.fog = false; Shot($"hole{hole.Number:00}_{v.Item2}_nofog"); UnityEngine.RenderSettings.fog = fog;
            data.renderPostProcessing = false; Shot($"hole{hole.Number:00}_{v.Item2}_nopost"); data.renderPostProcessing = post;
            var swatches = Swatches(hole.Number, ballAt, aim);
            Shot($"hole{hole.Number:00}_{v.Item2}_swatch");
            UnityEngine.Object.DestroyImmediate(swatches);
            if (++step >= Views.Length)
            {
                UnityEditor.SessionState.SetBool(Flag, false);
                if (UnityEngine.Application.isBatchMode) UnityEditor.EditorApplication.Exit(0); else UnityEditor.EditorApplication.isPlaying = false;
                return;
            }
            Load();
        }

        /// Test boards of the real LK_ materials laid on the play surface in front of the camera (the hole FBXs may still be
        /// the flat baseline): rough | fairway | third strip, a rock-ish box each side, and on the crater a lava sheet.
        /// UVs in tile units from world metres, as the Blender libs author them.
        static UnityEngine.GameObject Swatches(int holeNumber, GolfArcade.Course.CoursePoint ballAt, UnityEngine.Vector3 aim)
        {
            var root = new UnityEngine.GameObject("Look swatches");
            var ground = GolfArcade.Course.HoleView.ToWorld(ballAt, 0); ground.y += .03f;
            var right = new UnityEngine.Vector3(aim.z, 0, -aim.x);
            UnityEngine.Material M(string n) => GolfArcade.Course.GolfLook.GetForHole(n, holeNumber);
            string left = holeNumber == 9 ? "LK_SCRUB" : "LK_ROUGH", third = holeNumber == 8 ? "LK_PATH" : holeNumber == 9 ? "LK_SAND" : "LK_GREEN";
            float len = holeNumber == 10 ? 22 : holeNumber == 8 ? 14 : 70;   // crater / needle: leave the view of the lava / sea open
            void Strip(string n, float a, float b) => Face(root, n, M(n), TileOf(n), ground + right * a, ground + right * b, ground + right * b + aim * len, ground + right * a + aim * len);
            Strip(left, -24, -8); Strip("LK_FAIRWAY", -8, 8); Strip(third, 8, 24);
            string rock = holeNumber == 10 ? "LK_BASALT" : "LK_ROCK", rock2 = holeNumber == 8 ? "LK_MASONRY" : holeNumber == 9 ? "LK_CLIFF" : "LK_BASALT";
            Box(root, rock, M(rock), TileOf(rock), ground + right * 5 + aim * (holeNumber == 10 ? 14 : 24), right, aim, 4, 5);
            Box(root, rock2, M(rock2), TileOf(rock2), ground + right * -6 + aim * (holeNumber == 10 ? 18 : 34), right, aim, 5, 8);
            if (holeNumber != 10)
            {
                // whitewater on the sea (vertex alpha 1 at the near edge -> 0 far) and a waterfall sheet (v down the sheet)
                var sea = new UnityEngine.Vector3(ground.x, .12f, ground.z);
                Card(root, "LK_SURF", M("LK_SURF"), sea + right * -30 + aim * 12, sea + right * 30 + aim * 12, sea + right * 30 + aim * 90, sea + right * -30 + aim * 90, 1, 0, true);
                var foot = ground + right * 1 + aim * 12; var top = foot + UnityEngine.Vector3.up * 7;     // a sheet standing on the grass, just to see the material
                Card(root, "LK_FALL", M("LK_FALL"), top + right * -2, top + right * 2, foot + right * 2, foot + right * -2, 1, 1, false);
            }
            else
            {
                var wall = ground + aim * 180 + UnityEngine.Vector3.up * 10;
                Card(root, "LK_SMOKE", M("LK_SMOKE"), wall + right * -40 + UnityEngine.Vector3.up * 40, wall + right * 40 + UnityEngine.Vector3.up * 40, wall + right * 40, wall + right * -40, 1, 1, false);
            }
            if (holeNumber == 10)
            {
                var c = new UnityEngine.Vector3(171.1f, .08f, 161.5f);
                Face(root, "LK_LAVA", M("LK_LAVA"), TileOf("LK_LAVA"), c + new UnityEngine.Vector3(-160, 0, -160), c + new UnityEngine.Vector3(-160, 0, 160), c + new UnityEngine.Vector3(160, 0, 160), c + new UnityEngine.Vector3(160, 0, -160));
            }
            return root;
        }

        static float TileOf(string n) => n switch
        {
            "LK_FAIRWAY" => 10, "LK_GREEN" => 6, "LK_ROUGH" => 12, "LK_SCRUB" => 12, "LK_SAND" => 6, "LK_CLIFF" => 12, "LK_CLIFF_DARK" => 12,
            "LK_ROCK" => 4, "LK_ROCK_WET" => 4, "LK_PATH" => 5, "LK_MASONRY" => 4, "LK_BASALT" => 8, "LK_LAVA" => 24, _ => 10,
        };

        static void Face(UnityEngine.GameObject root, string name, UnityEngine.Material m, float tile, UnityEngine.Vector3 a, UnityEngine.Vector3 b, UnityEngine.Vector3 c, UnityEngine.Vector3 d, UnityEngine.Vector3? outward = null)
        {
            var n = UnityEngine.Vector3.Cross(b - a, d - a).normalized;
            bool flat = UnityEngine.Mathf.Abs(n.y) > .7f;
            var t = (b - a).normalized;
            UnityEngine.Vector2 UV(UnityEngine.Vector3 p) => flat ? new UnityEngine.Vector2(p.x, p.z) * (.9144f / tile) : new UnityEngine.Vector2(UnityEngine.Vector3.Dot(p, t), p.y) * (.9144f / tile);
            var mesh = new UnityEngine.Mesh { name = name };
            mesh.SetVertices(new[] { a, b, c, d });
            mesh.SetUVs(0, new[] { UV(a), UV(b), UV(c), UV(d) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            if (UnityEngine.Vector3.Dot(mesh.normals[0], outward ?? UnityEngine.Vector3.up) < 0) { mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateNormals(); }
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = new UnityEngine.GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
            go.AddComponent<UnityEngine.MeshRenderer>().sharedMaterial = m;
        }

        /// A card for the GolfSurf materials: UV0 (u across 0..1, v 0 at the first edge .. 1 at the second; surf: world/8 m),
        /// vertex colour alpha from aNear (a,b) to aFar (c,d).
        static void Card(UnityEngine.GameObject root, string name, UnityEngine.Material m, UnityEngine.Vector3 a, UnityEngine.Vector3 b, UnityEngine.Vector3 c, UnityEngine.Vector3 d, float aNear, float aFar, bool worldUV)
        {
            var mesh = new UnityEngine.Mesh { name = name };
            var p = new[] { a, b, c, d };
            mesh.SetVertices(p);
            mesh.SetUVs(0, worldUV ? System.Array.ConvertAll(p, q => new UnityEngine.Vector2(q.x, q.z) * (.9144f / 8f))
                                   : new[] { new UnityEngine.Vector2(0, 0), new UnityEngine.Vector2(1, 0), new UnityEngine.Vector2(1, 1), new UnityEngine.Vector2(0, 1) });
            mesh.SetColors(new[] { new UnityEngine.Color(.3f, 1, 1, aNear), new UnityEngine.Color(.7f, 1, 1, aNear), new UnityEngine.Color(.2f, 1, 1, aFar), new UnityEngine.Color(.9f, 1, 1, aFar) });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new UnityEngine.GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<UnityEngine.MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<UnityEngine.MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void Box(UnityEngine.GameObject root, string name, UnityEngine.Material m, float tile, UnityEngine.Vector3 baseCentre, UnityEngine.Vector3 right, UnityEngine.Vector3 fwd, float size, float height)
        {
            var h = UnityEngine.Vector3.up * height; float s = size / 2;
            UnityEngine.Vector3 P(float x, float z) => baseCentre + right * x + fwd * z;
            var (p00, p10, p11, p01) = (P(-s, -s), P(s, -s), P(s, s), P(-s, s));
            var centre = baseCentre + h / 2;
            void Side(UnityEngine.Vector3 a, UnityEngine.Vector3 b) => Face(root, name, m, tile, a, b, b + h, a + h, (a + b) / 2 + h / 2 - centre);
            Side(p00, p10); Side(p10, p11); Side(p11, p01); Side(p01, p00);
            Face(root, name, m, tile, p00 + h, p10 + h, p11 + h, p01 + h, UnityEngine.Vector3.up); // top
        }

        static void Shot(string name)
        {
            cam.targetTexture = rt; cam.Render(); UnityEngine.RenderTexture.active = rt;
            tex.ReadPixels(new UnityEngine.Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            UnityEngine.RenderTexture.active = null; cam.targetTexture = null;
            System.IO.File.WriteAllBytes($"{outDir}/{name}.jpg", UnityEngine.ImageConversion.EncodeToJPG(tex, 93));
            UnityEngine.Debug.Log("[GolfLookVariants] wrote " + name);
        }

        static void Load()
        {
            int hole = Views[step].Item1;
            game.enabled = true;
            typeof(GolfArcade.Game.GolfGame).GetMethod("StartHole", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game, new object[] { hole });
            wait = 30;
        }
    }
}

namespace GolfArcade.EditorTools
{
    /// End-to-end probe of the runtime on REAL look meshes while the hole FBXs in Resources/Course are still the flat baseline
    /// (play mode, the real GolfGame / CameraRig / GolfAtmosphere, 900x1600 like PostcardCameraStills):
    ///   GOLF_LOOK_PROBE_OUT=<dir> GOLF_LOOK_PROBE_ASSETS=Assets/_GolfLookProbe Unity -batchmode -projectPath Unity
    ///       -executeMethod GolfArcade.EditorTools.GolfLookProbe.Run -logFile <abs>
    /// Per hole: the phone-camera views on the hole as shipped ("current"), then the course model is swapped (same HoleView
    /// BuildFromModel path: alignment, LK_ material resolution, DressModel, colliders) for <ASSETS>/hole_NN.fbx if present (a copy of
    /// postcard_look_lib's smoke export: the real play surfaces + LK_ ground, cliff skin, ocean grid, shelf, WATER_SURF with vertex
    /// alpha, WATER_LAVA + LAVA_LIGHT_nn) and the views are shot again ("probe"). Measured: flat-colour share of the ground / water,
    /// surf vertex alpha against distance from the shore, lava hue and the lava lights on the basalt, and the golfer at address
    /// (clipped pixels, against the legacy golf light). There is no post preview any more: post renders nowhere in this project and
    /// the golf look no longer wires it (GolfAtmosphere class comment).
    [InitializeOnLoad]
    public static class GolfLookProbe
    {
        const string Flag = "GolfLookProbe";
        static int warm, holeIndex = -1, wait, startFrame, settleTicks;
        static string outDir, assets;
        static RenderTexture rt; static Texture2D tex; static Camera cam; static Light sun;
        static GolfArcade.Game.GolfGame game; static GolfArcade.Game.CameraRig rig;
        static readonly List<string> report = new();
        static int gates, fails;
        const int W = 900, H = 1600;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // per hole: (label, ball x, ball d, aim x, aim d, putting); "edge" views walk the ball to 2 yd short of the land edge
        static readonly (string, float, float, float, float, bool)[][] Views =
        {
            new[] { ("tee", 0f, 0f, 19f, 178f, false), ("green", -3.7f, 325.2f, -6f, 340f, true), ("arch", -4f, 322f, -25.5f, 317f, false) },
            new[] { ("tee", 0f, 0f, 3f, 60f, false), ("green", -6.5f, 477.4f, -10f, 492f, true), ("ridge", -61.2f, 261.7f, -10f, 492f, false) },
            new[] { ("tee", 0f, 0f, 14.3f, 66.2f, false), ("green", 246f, 164.2f, 261f, 164.7f, true), ("lavaedge", 107.1f, 159.3f, 171.1f, 161.5f, false) },
        };

        static GolfLookProbe() { EditorApplication.update += Tick; }

        static void Say(string s) { report.Add(s); Debug.Log(s); }
        static void Gate(string name, bool ok, string detail) { gates++; if (!ok) fails++; Say($"GATE: {name} {(ok ? "PASS" : "FAIL")} - {detail}"); }

        public static void Run()
        {
            outDir = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_OUT");
            if (string.IsNullOrEmpty(outDir)) outDir = Path.GetFullPath("../work/postcard-look/runtime/probe");
            assets = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_ASSETS");
            if (string.IsNullOrEmpty(assets)) assets = "Assets/_GolfLookProbe";
            Directory.CreateDirectory(outDir);
            SessionState.SetString(Flag + "out", outDir);
            SessionState.SetString(Flag + "assets", assets);
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if (holeIndex < 0)
            {
                if (!HoleView.Current || ++warm < 90) return;
                outDir = SessionState.GetString(Flag + "out", outDir);
                assets = SessionState.GetString(Flag + "assets", assets);
                game = Object.FindFirstObjectByType<GolfArcade.Game.GolfGame>();
                rig = Object.FindFirstObjectByType<GolfArcade.Game.CameraRig>();
                cam = rig.Camera;
                sun = (Light)typeof(GolfArcade.Game.GolfGame).GetField("sun", Private).GetValue(game);
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                // GOLF_LOOK_PROBE_PIPELINE=tennis|studio: render the probe under TennisURP / HeroBaseStudioURP instead of the quality level's asset (in memory only; the project's QualitySettings are never written)
                string want = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_PIPELINE");
                if (!string.IsNullOrEmpty(want))
                {
                    string path = want == "tennis" ? "Assets/Resources/Tennis/Rendering/TennisURP.asset" : want == "studio" ? "Assets/Characters/HeroBase/Rendering/HeroBaseStudioURP.asset" : want;
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(path);
                    if (asset) { QualitySettings.renderPipeline = asset; Say($"INFO: probe: render pipeline asset forced to '{asset.name}' in memory ({path})"); }
                    else Say($"INFO: probe: GOLF_LOOK_PROBE_PIPELINE '{want}' -> {path} not found, keeping the quality level's asset");
                }
                var pipeline = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                Say($"INFO: probe: quality level {QualitySettings.GetQualityLevel()} '{QualitySettings.names[QualitySettings.GetQualityLevel()]}', pipeline asset '{(pipeline ? pipeline.name : "none")}', camera near {cam.nearClipPlane} far {cam.farClipPlane}, probe assets {assets}");
                holeIndex = 0; StartHole(0); return;
            }
            if (--wait > 0) return;
            if (!Race && !Settled(out var why))
            {
                // review K 2026-10-04: EditorApplication.update can tick faster than the player loop in batch mode, so 30 ticks did not guarantee that GolfGame.StartHole's
                // Destroy(previous HoleView) had run: the hole-8 'probe' views then rendered the old flat baseline model under the swapped one (3 of 6 runs). Wait for real frames.
                if (++settleTicks < 3000) return;
                Say($"INFO: probe: hole {holeIndex} did not settle in {settleTicks} ticks ({Frames()}); continuing, the swap gate decides");
            }
            settleTicks = 0;
            try { ProbeHole(holeIndex); }
            catch (System.Exception e) { Gate($"PROBE_HOLE_{holeIndex} ran", false, e.ToString()); }
            if (++holeIndex >= Views.Length) { Finish(); return; }
            StartHole(holeIndex);
        }

        static void StartHole(int index)
        {
            game.enabled = true;
            startFrame = Time.frameCount; settleTicks = 0;
            typeof(GolfArcade.Game.GolfGame).GetMethod("StartHole", Private).Invoke(game, new object[] { index });
            wait = 30;
        }

        /// The previous HoleView is really gone (Destroy runs at the end of a PLAYER-LOOP frame, not on an editor tick) and a few real frames have passed.
        /// GOLF_LOOK_PROBE_RACE=1 puts back the pre-review waiting (30 editor ticks, whatever the player loop did) and keeps stale HoleViews: the mutation test of PROBE_SWAP_TOOK_EFFECT.
        static bool Race => System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_RACE") == "1";

        static bool Settled(out string why)
        {
            int frames = Time.frameCount - startFrame, views = Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            why = $"{frames} player frame(s) since StartHole, {views} HoleView(s) alive";
            return frames >= 5 && views == 1;
        }

        static void Finish()
        {
            Say($"SUMMARY: {gates - fails}/{gates} probe gates PASS");
            try { File.WriteAllLines(Path.Combine(outDir, "probe_report.txt"), report.ToArray()); } catch (System.Exception e) { Debug.LogWarning(e.Message); }
            SessionState.SetBool(Flag, false);
            if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1); else EditorApplication.isPlaying = false;
        }

        // ------------------------------------------------------------------ one hole

        static void ProbeHole(int index)
        {
            var hole = GolfArcade.Course.Course.Postcards().Holes[index];
            int n = hole.Number;
            string only = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_HOLES");     // e.g. "10": probe only these holes (fast sweeps)
            if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(n.ToString())) { Say($"INFO: hole {n} skipped (GOLF_LOOK_PROBE_HOLES={only})"); return; }
            Say($"=== hole {n} ===");
            game.enabled = false; rig.enabled = false;
            foreach (var v in Views[index]) View(n, v, "current");

            var probe = AssetDatabase.LoadAssetAtPath<GameObject>($"{assets}/hole_{n:00}.fbx");
            if (!probe) { Say($"INFO: hole {n}: no probe model at {assets}/hole_{n:00}.fbx, probe views skipped"); Golfer(n); return; }
            var view = HoleView.Current;
            Say($"INFO: hole {n}: before the swap {Frames()}");
            foreach (var extra in Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (extra != view && !Race) { Say($"INFO: hole {n}: destroying a stale HoleView '{extra.name}' that was still alive"); Object.DestroyImmediate(extra.gameObject); }
            var old = view.transform.Find("Course model");
            if (old) Object.DestroyImmediate(old.gameObject);
            typeof(HoleView).GetMethod("BuildFromModel", Private).Invoke(view, new object[] { probe });
            var model = view.transform.Find("Course model");
            Say($"INFO: hole {n}: course model swapped for the probe {AssetDatabase.GetAssetPath(probe)} through HoleView.BuildFromModel");
            if (System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_SELFTEST") == "stale")
            {
                // mutation test of PROBE_SWAP_TOOK_EFFECT: put the installed baseline model back next to the probe model (what the reviewer's failing hole-8 runs rendered): the gate must FAIL
                var baseline = Resources.Load<GameObject>($"Course/hole_{n:00}");
                var ghost = Object.Instantiate(baseline, view.transform); ghost.name = "Course model (stale baseline injected by GOLF_LOOK_PROBE_SELFTEST)";
                Say($"INFO: hole {n}: SELFTEST stale: the installed baseline model was injected beside the probe model; PROBE_SWAP_TOOK_EFFECT must FAIL");
            }
            SwapGate(n, view, model);
            GolfLookSmoke.CheckModel(n, model, "PROBE ", Gate, Say);
            PostcardLookMaterialRules.Check(n, probe, model, GolfLookSmoke.Palette(), (g, ok, d) => Gate($"PROBE LK_RULES hole{n} {g}", ok, d), Say);
            if (n != 10) SurfFalloff(n, model);
            foreach (var v in Views[index]) View(n, v, "probe", model);
            Aerial(n, model);
            if (n == 10) LavaLights(n, model);
            if (n == 10 && System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_TUNE") == "1") Tune10(model);
            Golfer(n);
        }

        static string Frames() => $"{Time.frameCount - startFrame} player frame(s) since StartHole, {Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length} HoleView(s) alive";

        /// PROBE_SWAP_TOOK_EFFECT: what the camera renders IS the probe model. Exactly one HoleView with exactly one 'Course model', and no MeshFilter anywhere in the scene
        /// still carries a mesh of the installed (baseline) Resources/Course/hole_NN.fbx. Without this gate the stale-baseline renders of review K (hole 8, 3 of 6 runs) passed
        /// every material gate (those read the NEW model) while the frames showed the OLD one.
        static void SwapGate(int n, HoleView view, Transform model)
        {
            var views = Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int models = view.transform.Cast<Transform>().Count(t => t.name == "Course model");
            string installed = $"Assets/Resources/Course/hole_{n:00}.fbx";
            int stale = 0; var names = new List<string>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!mf.sharedMesh || AssetDatabase.GetAssetPath(mf.sharedMesh) != installed) continue;
                stale++; if (names.Count < 4) names.Add(mf.name);
            }
            int probeMeshes = model ? model.GetComponentsInChildren<MeshFilter>(true).Count(mf => mf.sharedMesh && AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith(assets)) : 0;
            // area U 2026-10-04: the instantiated model must CARRY LK_ materials (GolfLook.Get names its materials by key): the flat baseline FBX has none, so a render of the stale model fails loudly here
            // even if every mesh-path test above were fooled. Renderers with an LK_ slot / renderers in all / flat MAT_-style ground renderers left.
            int lkRenderers = 0, allRenderers = 0;
            if (model) foreach (var r in model.GetComponentsInChildren<Renderer>(true)) { allRenderers++; if (r.sharedMaterials.Any(m => m && m.name.StartsWith("LK_"))) lkRenderers++; }
            Gate($"PROBE_SWAP_TOOK_EFFECT hole{n}", views.Length == 1 && models == 1 && stale == 0 && probeMeshes > 0 && lkRenderers >= 20,
                $"{views.Length} HoleView(s) (need 1), {models} 'Course model' child(ren) (need 1), {stale} MeshFilter(s) still on the installed baseline {installed} (need 0){(stale > 0 ? " e.g. " + string.Join(",", names) : "")}, {probeMeshes} mesh(es) of the probe model, " +
                $"{lkRenderers} of {allRenderers} renderers of the instantiated model carry an LK_ material (need >= 20: the flat baseline carries 0); {Frames()}");
        }

        static void AimVisuals(bool on)
        {
            var line = (LineRenderer)typeof(GolfArcade.Game.GolfGame).GetField("aimLine", Private).GetValue(game);
            var marker = (Transform)typeof(GolfArcade.Game.GolfGame).GetField("landingMarker", Private).GetValue(game);
            if (line) line.enabled = on;
            if (!on && marker) marker.gameObject.SetActive(false);
        }

        static CoursePoint P(double x, double d) => new(x, d);
        static bool LandAt(double x, double d) => Physics.Raycast(new Vector3((float)x, 400, (float)d), Vector3.down, out var hit, 800, ~0, QueryTriggerInteraction.Ignore) && hit.point.y > 3f;

        /// Frame the phone camera at address (CameraRig.FrameAddress, as the game does) for one view.
        static (CoursePoint ball, Vector3 aim) Frame((string, float, float, float, float, bool) v)
        {
            var ballAt = P(v.Item2, v.Item3); var target = P(v.Item4, v.Item5);
            if (v.Item1.EndsWith("edge"))
            {
                // walk toward the target while the ground stays land, then step back 2 yd: the camera looks over the edge
                double dx = target.X - ballAt.X, dd = target.D - ballAt.D, len = System.Math.Sqrt(dx * dx + dd * dd);
                var last = ballAt;
                for (double s = 1; s < len; s += 1) { var p = P(ballAt.X + dx * s / len, ballAt.D + dd * s / len); if (!LandAt(p.X, p.D)) break; last = p; }
                ballAt = P(last.X - dx / len * 2, last.D - dd / len * 2);
            }
            var ball = HoleView.ToWorld(ballAt, 0.06);
            var aim = HoleView.ToWorld(target, 0) - HoleView.ToWorld(ballAt, 0); aim.y = 0; aim.Normalize();
            rig.FrameAddress(ball, aim, v.Item6); rig.SnapNext(); rig.ApplyFrame();
            return (ballAt, aim);
        }

        static Color32[] Shot(string file)
        {
            var px = LookPixels.Render(cam, rt, tex);
            if (file != null) LookPixels.Save(tex, outDir, file + ".jpg");
            return px;
        }

        static void View(int n, (string, float, float, float, float, bool) v, string tag, Transform model = null)
        {
            var (ballAt, _) = Frame(v);
            string name = $"hole{n:00}_{v.Item1}_{tag}";
            var full = Shot(name);
            var (flat, total) = LookPixels.Flat(full, W, H, 0, (int)(H * .55f));
            float share = total > 0 ? flat / (float)total : 1;
            Say($"INFO: {name}: ball ({ballAt.X:F1},{ballAt.D:F1}) ground {HoleView.GroundHeight(ballAt):F2} yd, lower 55 %: {flat}/{total} flat 20 px tiles ({share * 100:F1} %)");
            if (tag != "probe") return;
            Gate($"PROBE_GROUND_TEXTURED hole{n} {v.Item1}", share <= .15f, $"{flat}/{total} tiles of the lower 55 % are flat colour (luminance std < 1.5): {share * 100:F1} % <= 15 %");

            if (n != 10 && (v.Item1 == "tee" || v.Item1 == "green"))
            {
                // the sea: tiles fully inside the visible ocean/shelf (render without them, diff) must not be one flat colour
                var water = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && (r.name.StartsWith("WATER_OCEAN") || r.name.StartsWith("WATER_SHELF"))).ToArray();
                foreach (var r in water) r.enabled = false;
                var dry = Shot(null);
                foreach (var r in water) r.enabled = true;
                var mask = LookPixels.Diff(full, dry, 30, out int visible);
                var (wf, wt) = LookPixels.Flat(full, W, H, 0, H, 20, 1.5f, mask);
                var sea = LookPixels.Analyse(LookPixels.Masked(full, mask), .1f);
                Say($"INFO: {name}: visible sea {visible * 100f / full.Length:F1} % of the frame, mean {LookPixels.Rgb(sea.Mean)}, {wf}/{wt} flat tiles inside it");
                if (wt >= 8) Gate($"PROBE_SEA_NOT_FLAT hole{n} {v.Item1}", wf <= wt * .30f, $"{wf}/{wt} sea tiles flat ({(wt > 0 ? wf * 100f / wt : 0):F1} % <= 30 %), sea mean {LookPixels.Rgb(sea.Mean)}");
            }
            if (n == 10 && v.Item1 == "lavaedge") LavaEdge(n, full, model);
        }

        // ------------------------------------------------------------------ aerial: the water shelf must be a thin edge, never the loudest shape (calibration 2026-10-04)

        // world yards (x right, y up, z down the hole): camera position and look-at, a drone-like frame over the whole island(s)
        static readonly (Vector3 pos, Vector3 look)[] AerialCams =
        {
            (new Vector3(70, 120, -70), new Vector3(0, 0, 190)),      // Needle
            (new Vector3(120, 150, -50), new Vector3(-10, 0, 270)),   // Split
            (new Vector3(-70, 110, -40), new Vector3(150, 0, 150)),   // Crater
        };

        /// An aerial frame of the probe model (the ground library's real shelf geometry, thin edge + ocean + whitewater patches): where is the shelf, how bright is it next to the
        /// open sea, and what share of the frame's brightest 2 % of pixels is shelf / whitewater / sky / other. Gate PROBE_SHELF_NOT_LOUDEST: the shelf is <= 10 % of the
        /// brightest 2 % and not brighter than 1.3 x the open sea beside it (needle.jpg / split.jpg: deep blue right up to a thin teal edge).
        static void Aerial(int n, Transform model)
        {
            var (pos, look) = AerialCams[n - 8];
            rig.transform.position = pos; rig.transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up); cam.fieldOfView = 60;
            string name = $"hole{n:00}_aerial_probe";
            var full = Shot(name);
            bool[] MaskOf(string prefix)
            {
                var rs = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.name.StartsWith(prefix)).ToArray();
                if (rs.Length == 0) return new bool[full.Length];
                foreach (var r in rs) r.enabled = false;
                var dry = Shot(null);
                foreach (var r in rs) r.enabled = true;
                return LookPixels.Diff(full, dry, 30, out _);
            }
            var shelf = MaskOf("WATER_SHELF"); var surf = MaskOf("WATER_SURF"); var ocean = MaskOf("WATER_OCEAN"); var lava = n == 10 ? MaskOf("WATER_LAVA") : new bool[full.Length];
            var lums = full.Select(LookPixels.Lum).OrderBy(x => x).ToArray(); float cut = lums[(int)(lums.Length * .98f)];
            int top = 0, tShelf = 0, tSurf = 0, tLava = 0, tWater = 0, tSky = 0;
            int skyRows = 0;
            for (int i = 0; i < full.Length; i++)
            {
                if (LookPixels.Lum(full[i]) < cut) continue;
                top++;
                if (shelf[i]) tShelf++; else if (surf[i]) tSurf++; else if (lava[i]) tLava++; else if (ocean[i]) tWater++; else if (i / W > H * .80f) tSky++;
            }
            var sh = GolfLookBoards.MeanHsv(LookPixels.Masked(full, shelf)); var oc = GolfLookBoards.MeanHsv(LookPixels.Masked(full, ocean.Select((o, i) => o && !shelf[i] && !surf[i]).ToArray()));
            float shelfPct = shelf.Count(b => b) * 100f / full.Length;
            Say($"INFO: {name}: shelf {shelf.Count(b => b)} px ({shelfPct:F2} % of the frame) mean {LookPixels.Rgb(sh.Mean)} lum {sh.Lum:F0} V {sh.V:F2}; open ocean beside it {oc.N} px mean {LookPixels.Rgb(oc.Mean)} lum {oc.Lum:F0}; brightest 2 % of the frame (lum >= {cut:F0}, {top} px): shelf {tShelf * 100f / Mathf.Max(1, top):F1} %, whitewater {tSurf * 100f / Mathf.Max(1, top):F1} %, lava {tLava * 100f / Mathf.Max(1, top):F1} %, open ocean {tWater * 100f / Mathf.Max(1, top):F1} %, upper-frame sky {tSky * 100f / Mathf.Max(1, top):F1} %, everything else (land, rock, sky) {(top - tShelf - tSurf - tLava - tWater - tSky) * 100f / Mathf.Max(1, top):F1} %");
            if (n != 10)
                Gate($"PROBE_SHELF_NOT_LOUDEST hole{n}", shelf.Count(b => b) >= 300 && tShelf <= top * .10f && sh.Lum <= 1.3f * oc.Lum && sh.H >= 175 && sh.H <= 200,
                    $"aerial on the ground library's shelf: {shelf.Count(b => b)} shelf px ({shelfPct:F2} % of the frame), mean {LookPixels.Rgb(sh.Mean)} hue {sh.H:F0} lum {sh.Lum:F0} <= 1.3 x the open sea {oc.Lum:F0} = {1.3f * oc.Lum:F0}; shelf is {tShelf * 100f / Mathf.Max(1, top):F1} % of the brightest 2 % of the frame (<= 10 %)");
        }

        // ------------------------------------------------------------------ lava meets basalt (hole 10)

        static void LavaEdge(int n, Color32[] full, Transform model)
        {
            var all = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToArray();
            Color32[] Without(System.Func<Renderer, bool> pick)
            {
                var off = all.Where(pick).ToArray();
                foreach (var r in off) r.enabled = false;
                var px = Shot(null);
                foreach (var r in off) r.enabled = true;
                return px;
            }
            var lavaMask = LookPixels.Diff(full, Without(r => r.name.StartsWith("WATER_LAVA")), 30, out int lavaCount);
            var basaltMask = LookPixels.Diff(full, Without(r => r.sharedMaterials.Any(m => m && m.name.StartsWith("LK_BASALT"))), 30, out int basaltCount);
            var lights = model.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Point).ToArray();
            foreach (var l in lights) l.enabled = false;
            var dark = Shot($"hole{n:00}_lavaedge_probe_nolavalights");
            foreach (var l in lights) l.enabled = true;

            var lava = LookPixels.Analyse(LookPixels.Masked(full, lavaMask), .10f);
            var (lf, lt) = LookPixels.Flat(full, W, H, 0, H, 20, 1.5f, lavaMask);
            Say($"INFO: hole {n} lava edge: lava {lavaCount * 100f / full.Length:F1} % of the frame, mean {LookPixels.Rgb(lava.Mean)}, top-10% {LookPixels.Rgb(lava.Top)} hue {lava.TopHue:F1} G/R {lava.TopGR:F2}, darker half {LookPixels.Rgb(lava.Bottom)}, cream {lava.Cream * 100:F2} %, {lf}/{lt} flat tiles");
            Gate($"PROBE_LAVA_VISIBLE hole{n}", lavaCount >= full.Length * .03f, $"visible lava {lavaCount * 100f / full.Length:F1} % of the frame (>= 3 %)");
            Gate($"PROBE_LAVA_HUE hole{n}", lava.TopHue >= 15 && lava.TopHue <= 45 && lava.TopGR <= .80f && lava.Cream <= .01f && lava.Mean.r > lava.Mean.g && lava.Mean.g > lava.Mean.b,
                $"top-10% {LookPixels.Rgb(lava.Top)} hue {lava.TopHue:F1} (15..45), G/R {lava.TopGR:F2} (<= .80), cream {lava.Cream * 100:F2} % (<= 1 %), mean {LookPixels.Rgb(lava.Mean)} R>G>B");
            Gate($"PROBE_LAVA_NOT_FLAT hole{n}", lt == 0 || lf <= lt * .30f, $"{lf}/{lt} lava tiles flat colour (<= 30 %): not a solid orange plane");

            // round 2 (review K): the old gate here (PROBE_LAVA_CRUST_DARK, "darker half of the visible lava luminance <= 70") encoded the dark-cell lava and cannot hold with the lead's
            // >= 85 % in-band rule (it measured 87 / 86 and failed in every run since the first calibration). It is now INFO, and the real-geometry crust is gated like the board's LAVA_HAS_CRUST:
            // 2.5..10 % of the visible lava darker than V .4 (the round-2 ramp: board 3.5 %).
            float crust = LookPixels.Lum(LookPixels.ToColor32(lava.Bottom));
            var lavaList = LookPixels.Masked(full, lavaMask); int veryDark = 0, inBand = 0;
            foreach (var p in lavaList) { Color.RGBToHSV(p, out var hh, out var ss, out var vv); if (vv < .40f) veryDark++; hh *= 360; if (hh >= GolfLookBoards.LavaHueMin && hh <= GolfLookBoards.LavaHueMax && ss >= GolfLookBoards.LavaSatMin && vv >= GolfLookBoards.LavaValMin) inBand++; }
            float crustShare = veryDark / (float)Mathf.Max(1, lavaList.Count), bandShare = inBand / (float)Mathf.Max(1, lavaList.Count);
            Say($"INFO: hole {n} lava edge: darker half of the visible lava {LookPixels.Rgb(lava.Bottom)} luminance {crust:F0} (the retired gate wanted <= 70, crater.jpg 48)");
            Gate($"PROBE_LAVA_HAS_CRUST hole{n}", lavaList.Count > 20000 && crustShare >= .025f && crustShare <= .10f && bandShare >= GolfLookBoards.LavaShareMin,
                $"real geometry, phone view at the rim: {crustShare * 100:F1} % of the {lavaList.Count} lava px darker than V .4 (2.5..10 %), in band {bandShare * 100:F1} % (>= {GolfLookBoards.LavaShareMin * 100:F0})");

            // the phone view sees the far wall ring and the pillar, far from the LAVA_LIGHTs: reported, gated in LavaLights' witness view
            int lit = 0; double added = 0;
            for (int i = 0; i < full.Length; i++)
            {
                if (!basaltMask[i]) continue;
                float d = LookPixels.Lum(full[i]) - LookPixels.Lum(dark[i]);
                added += d; if (d >= 8) lit++;
            }
            var basalt = LookPixels.Analyse(LookPixels.Masked(full, basaltMask), .02f);
            float share = basaltCount > 0 ? lit / (float)basaltCount : 0;
            Say($"INFO: hole {n} phone view: basalt {basaltCount * 100f / full.Length:F1} % of the frame, median luminance {basalt.MedianLum:F0}, cracks {LookPixels.Rgb(basalt.Top)} hue {basalt.TopHue:F1}; lava lights add {(basaltCount > 0 ? added / basaltCount : 0):F1} luminance on average, >= 8 levels on {share * 100:F1} % of it (most visible basalt here is the far ring / pillar)");
        }

        // ------------------------------------------------------------------ the LAVA_LIGHT point lights on the basalt (hole 10)

        struct LightReach { public int Near, LitNear, BasaltPx, LavaPx; public float LitShare, ReachYd, BasaltAdd, HotShare, LavaAdd; }

        static List<Vector3> BasaltVertices(Transform model, int max)
        {
            var all = new List<Vector3>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<Renderer>();
                if (!r || !mf.sharedMesh || !mf.sharedMesh.isReadable || !r.sharedMaterials.Any(m => m && m.name.StartsWith("LK_BASALT"))) continue;
                var v = mf.sharedMesh.vertices; var m4 = mf.transform.localToWorldMatrix;
                foreach (var p in v) all.Add(m4.MultiplyPoint3x4(p));
            }
            if (all.Count <= max) return all;
            int stride = all.Count / max + 1;
            return all.Where((_, i) => i % stride == 0).ToList();
        }

        static float Illuminance(Light[] lights, Vector3 p)
        {
            float e = 0;
            foreach (var l in lights)
            {
                float d2 = Mathf.Max(.01f, (l.transform.position - p).sqrMagnitude), f = d2 / (l.range * l.range), s = Mathf.Clamp01(1 - f * f);
                e += l.intensity / d2 * s * s;
            }
            return e;
        }

        /// Analytic (every basalt vertex, no N.L, no per-object light limit) + rendered (a witness camera 22 yd off the wall nearest to
        /// a lava light, lights on vs off). Illuminance .5 adds about 8 luminance levels on the near-black basalt albedo.
        static LightReach Measure(Light[] lights, List<Vector3> basalt, Transform model, string file)
        {
            var r = new LightReach();
            float far = 0;
            foreach (var p in basalt)
            {
                float nearest = lights.Min(l => (l.transform.position - p).magnitude);
                float e = Illuminance(lights, p);
                if (nearest <= 20) { r.Near++; if (e >= .5f) r.LitNear++; }
                if (e >= .5f) far = Mathf.Max(far, nearest);
            }
            r.ReachYd = far;
            // the witness: the light whose nearest basalt is closest
            Light best = null; Vector3 wall = default; float bestD = float.MaxValue;
            foreach (var l in lights)
                foreach (var p in basalt) { float d = (p - l.transform.position).sqrMagnitude; if (d < bestD) { bestD = d; best = l; wall = p; } }
            if (!best) return r;
            var lp = best.transform.position;
            var q = new Vector3(wall.x, lp.y, wall.z);
            var h = lp - q; h.y = 0; h = h.sqrMagnitude > 1e-4f ? h.normalized : Vector3.forward;
            var pos = cam.transform.position; var rot = cam.transform.rotation;
            cam.transform.position = q + h * (Mathf.Sqrt(bestD) + 22) + Vector3.up * 5;
            cam.transform.LookAt(q);
            var all = model.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled).ToArray();
            Color32[] Without(System.Func<Renderer, bool> pick) { var off = all.Where(pick).ToArray(); foreach (var x in off) x.enabled = false; var px = Shot(null); foreach (var x in off) x.enabled = true; return px; }
            var full = Shot(file);
            var basaltMask = LookPixels.Diff(full, Without(x => x.sharedMaterials.Any(m => m && m.name.StartsWith("LK_BASALT"))), 30, out r.BasaltPx);
            var lavaMask = LookPixels.Diff(full, Without(x => x.name.StartsWith("WATER_LAVA")), 30, out r.LavaPx);
            foreach (var l in lights) l.enabled = false;
            var dark = Shot(null);
            foreach (var l in lights) l.enabled = true;
            int lit = 0, hot = 0; double bAdd = 0, lAdd = 0;
            for (int i = 0; i < full.Length; i++)
            {
                float d = LookPixels.Lum(full[i]) - LookPixels.Lum(dark[i]);
                if (basaltMask[i]) { bAdd += d; if (d >= 8) lit++; }
                if (lavaMask[i]) { lAdd += d; if (d >= 40) hot++; }
            }
            r.LitShare = r.BasaltPx > 0 ? lit / (float)r.BasaltPx : 0; r.BasaltAdd = r.BasaltPx > 0 ? (float)(bAdd / r.BasaltPx) : 0;
            r.HotShare = r.LavaPx > 0 ? hot / (float)r.LavaPx : 0; r.LavaAdd = r.LavaPx > 0 ? (float)(lAdd / r.LavaPx) : 0;
            cam.transform.SetPositionAndRotation(pos, rot);
            return r;
        }

        static string Describe(LightReach r, Light[] lights) =>
            $"{lights.Length} light(s) I {lights[0].intensity} range {lights[0].range} yd, {lights[0].transform.position.y:F1} yd high; basalt vertices within 20 yd of a light: {r.Near}, {(r.Near > 0 ? r.LitNear * 100f / r.Near : 0):F0} % get illuminance >= .5, farthest such vertex {r.ReachYd:F1} yd from its light; witness view: basalt {r.BasaltPx} px, +{r.BasaltAdd:F1} luminance on average, >= +8 on {r.LitShare * 100:F1} %; lava {r.LavaPx} px, +{r.LavaAdd:F1} on average, >= +40 (hot pool) on {r.HotShare * 100:F1} %";

        static void LavaLights(int n, Transform model)
        {
            var lights = model.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Point).ToArray();
            if (lights.Length == 0) { Gate($"PROBE_LAVA_LIGHTS_REACH_BASALT hole{n}", false, "no LAVA_LIGHT point lights"); return; }
            var basalt = BasaltVertices(model, 30000);
            var r = Measure(lights, basalt, model, $"hole{n:00}_lavalight_witness");
            Say($"INFO: hole {n} lava lights: {Describe(r, lights)}");
            Gate($"PROBE_LAVA_LIGHTS_REACH_BASALT hole{n}", lights.Length >= 3 && r.LitShare >= .20f,
                $"{lights.Length} LAVA_LIGHT point lights; witness view of the wall nearest a light: >= +8 luminance on {r.LitShare * 100:F1} % of the visible basalt (>= 20 %); analytic: {(r.Near > 0 ? r.LitNear * 100f / r.Near : 0):F0} % of the basalt within 20 yd of a light gets illuminance >= .5, out to {r.ReachYd:F1} yd");
            Gate($"PROBE_LAVA_LIGHTS_NO_HOT_POOLS hole{n}", r.HotShare <= .10f, $"lava pixels the lights brighten by >= 40 levels: {r.HotShare * 100:F1} % (<= 10 %) in the witness view");
        }

        // ------------------------------------------------------------------ hole 10 tuning sweep (GOLF_LOOK_PROBE_TUNE=1): INFO only

        static void Tune10(Transform model)
        {
            var look = GolfAtmosphere.Holes[10];
            var lights = model.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Point).ToArray();
            var basalt = BasaltVertices(model, 30000);
            // (a) lava lights: intensity, range, lift above the LAVA_LIGHT empty
            if (lights.Length > 0)
            {
                var basePos = lights.Select(l => l.transform.position - Vector3.up * GolfLook.LavaLightLift).ToArray();
                float i0 = lights[0].intensity, r0 = lights[0].range;
                foreach (var (I, R, lift) in new[] { (28f, 48f, 0f), (60f, 40f, 3f), (100f, 45f, 4f), (150f, 50f, 5f), (220f, 55f, 6f), (150f, 50f, 0f) })
                {
                    for (int k = 0; k < lights.Length; k++) { lights[k].intensity = I; lights[k].range = R; lights[k].transform.position = basePos[k] + Vector3.up * lift; }
                    Say($"INFO: TUNE lava lights I {I} range {R} lift {lift}: {Describe(Measure(lights, basalt, model, $"tune_lights_{I}_{lift}"), lights)}");
                }
                for (int k = 0; k < lights.Length; k++) { lights[k].intensity = i0; lights[k].range = r0; lights[k].transform.position = basePos[k] + Vector3.up * GolfLook.LavaLightLift; }
            }
            // (b) lava crust at the phone's grazing angle: material variants
            var lavaR = model.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith("WATER_LAVA")).ToArray();
            var edge = Views[2][2];
            Frame(edge);
            var shipped = lavaR.Length > 0 ? lavaR[0].sharedMaterial : null;
            if (shipped)
            {
                foreach (var (label, refl, tint, smooth) in new[] { ("shipped", -1f, -1f, -1f), ("envrefl off", 0f, -1f, -1f), ("envrefl off, tint .6", 0f, .6f, -1f), ("envrefl off, S .15", 0f, -1f, .15f), ("envrefl off, tint .6, S .15", 0f, .6f, .15f) })
                {
                    var m = new Material(shipped) { name = "LK_LAVA tune" };
                    if (refl == 0) { m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
                    if (tint > 0) m.SetColor("_BaseColor", new Color(tint, tint, tint));
                    if (smooth >= 0) m.SetFloat("_Smoothness", smooth);
                    foreach (var r in lavaR) r.sharedMaterial = m;
                    var full = Shot($"tune_lava_{label.Replace(", ", "_").Replace(" ", "")}");
                    foreach (var r in lavaR) r.enabled = false;
                    var none = Shot(null);
                    foreach (var r in lavaR) r.enabled = true;
                    var mask = LookPixels.Diff(full, none, 30, out _);
                    var s = LookPixels.Analyse(LookPixels.Masked(full, mask), .1f);
                    Say($"INFO: TUNE lava '{label}': mean {LookPixels.Rgb(s.Mean)}, top-10% {LookPixels.Rgb(s.Top)} hue {s.TopHue:F1} G/R {s.TopGR:F2}, darker half {LookPixels.Rgb(s.Bottom)} lum {LookPixels.Lum(LookPixels.ToColor32(s.Bottom)):F0}, cream {s.Cream * 100:F2} %");
                    foreach (var r in lavaR) r.sharedMaterial = shipped;
                    Object.DestroyImmediate(m);
                }
            }
            // (c) crater key / ambient ground: grass at the tee, basalt median at the edge, golfer skin
            float key0 = look.SunIntensity; Color ground0 = look.AmbientGround, equator0 = look.AmbientEquator;
            var combos = new[] { (2.3f, ground0, equator0), (2.0f, ground0, equator0), (1.8f, ground0, equator0), (2.3f, new Color(.40f, .17f, .08f), equator0), (2.0f, new Color(.40f, .17f, .08f), new Color(.45f, .28f, .22f)), (1.8f, new Color(.40f, .17f, .08f), new Color(.45f, .28f, .22f)) };
            var lines = new string[combos.Length];
            for (int c = 0; c < combos.Length; c++)
            {
                look.SunIntensity = combos[c].Item1; look.AmbientGround = combos[c].Item2; look.AmbientEquator = combos[c].Item3;
                GolfAtmosphere.Apply(sun, cam, 10);
                Frame(Views[2][0]);
                var tee = Shot($"tune_key_{combos[c].Item1}_{c}_tee");
                var grass = LookPixels.Analyse(LookPixels.Rows(tee, W, H, 0, H / 4), .1f);
                Frame(edge);
                var full = Shot(null);
                var bas = model.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.sharedMaterials.Any(m => m && m.name.StartsWith("LK_BASALT"))).ToArray();
                foreach (var r in bas) r.enabled = false;
                var nob = Shot(null);
                foreach (var r in bas) r.enabled = true;
                var bm = LookPixels.Analyse(LookPixels.Masked(full, LookPixels.Diff(full, nob, 30, out _)), .02f);
                lines[c] = $"key {combos[c].Item1} ground {LookPixels.Rgb(combos[c].Item2)} equator {LookPixels.Rgb(combos[c].Item3)}: tee grass (nearest quarter) {LookPixels.Rgb(grass.Mean)}, basalt median lum {bm.MedianLum:F0}";
            }
            var golfer = (GolfArcade.Game.GolferView)typeof(GolfArcade.Game.GolfGame).GetField("golfer", Private).GetValue(game);
            AimVisuals(true);
            typeof(GolfArcade.Game.GolfGame).GetMethod("BeginAim", Private).Invoke(game, new object[] { false });
            rig.SnapNext(); rig.ApplyFrame();
            var mine = golfer.GetComponentsInChildren<Renderer>(false);
            for (int c = 0; c < combos.Length; c++)
            {
                look.SunIntensity = combos[c].Item1; look.AmbientGround = combos[c].Item2; look.AmbientEquator = combos[c].Item3;
                GolfAtmosphere.Apply(sun, cam, 10);
                var f = Matte(mine, $"tune_key_{combos[c].Item1}_{c}_golfer");
                Say($"INFO: TUNE crater {lines[c]}; golfer {f.Describe()}");
            }
            golfer.SetVisible(false); AimVisuals(false);
            look.SunIntensity = key0; look.AmbientGround = ground0; look.AmbientEquator = equator0;
            GolfAtmosphere.Apply(sun, cam, 10);
        }

        /// Distance (yd) where a URP point light's illuminance I / d^2 * (1 - (d^2 / r^2)^2)^2 falls to `e`.
        static float Reach(float intensity, float range, float e)
        {
            for (float d = .5f; d < range; d += .1f)
            {
                float f = d * d / (range * range), smooth = Mathf.Clamp01(1 - f * f);
                if (intensity / (d * d) * smooth * smooth < e) return d;
            }
            return range;
        }

        // ------------------------------------------------------------------ surf alpha vs the shore (holes 8, 9)

        static void SurfFalloff(int n, Transform model)
        {
            var surfs = model.GetComponentsInChildren<MeshFilter>(true).Where(mf => mf.name.StartsWith("WATER_SURF") && mf.sharedMesh).ToArray();
            if (surfs.Length == 0) { Gate($"PROBE_SURF_ALPHA hole{n}", false, "no WATER_SURF meshes in the probe model"); return; }
            int total = surfs.Sum(mf => mf.sharedMesh.vertexCount), stride = Mathf.Max(1, total / 3000), noColour = 0, unreadable = 0;
            float[] edges = { 2, 4, 10 };                       // bins: <= 2 yd (incl. under the cliff), 2..4, 4..10, > 10 (reef breaks)
            var sum = new double[4]; var count = new int[4];
            foreach (var mf in surfs)
            {
                var mesh = mf.sharedMesh;
                if (!mesh.isReadable) { unreadable++; continue; }
                var v = mesh.vertices; var c = mesh.colors;
                if (c == null || c.Length != v.Length) { noColour++; continue; }
                for (int i = 0; i < v.Length; i += stride)
                {
                    var w = mf.transform.TransformPoint(v[i]);
                    float d = ShoreDistance(w);
                    int b = d <= edges[0] ? 0 : d <= edges[1] ? 1 : d <= edges[2] ? 2 : 3;
                    sum[b] += c[i].a; count[b]++;
                }
            }
            float A(int b) => count[b] > 0 ? (float)(sum[b] / count[b]) : float.NaN;
            Say($"INFO: hole {n} surf: {surfs.Length} WATER_SURF mesh(es), {total} vertices (every {stride}th sampled), {noColour} without vertex colours, {unreadable} unreadable; mean vertex alpha by distance to land: <=2 yd {A(0):F2} (n {count[0]}), 2-4 {A(1):F2} (n {count[1]}), 4-10 {A(2):F2} (n {count[2]}), >10 {A(3):F2} (n {count[3]}, shelf-edge breaks)");
            bool ok = noColour == 0 && unreadable == 0 && count[0] > 0 && A(0) >= .45f && (count[2] == 0 || A(2) <= .30f) && (count[1] == 0 || (A(0) > A(1) && (count[2] == 0 || A(1) > A(2))));
            Gate($"PROBE_SURF_ALPHA hole{n}", ok, $"vertex alpha falls off away from the shore: <=2 yd {A(0):F2} (>= .45) > 2-4 yd {A(1):F2} > 4-10 yd {A(2):F2} (<= .30); colours on every mesh {noColour == 0}");
        }

        static float ShoreDistance(Vector3 w)
        {
            if (LandAt(w.x, w.z)) return 0;
            for (float r = .5f; r <= 16; r += .5f)
                for (int k = 0; k < 16; k++)
                {
                    float a = k * Mathf.PI / 8;
                    if (LandAt(w.x + Mathf.Cos(a) * r, w.z + Mathf.Sin(a) * r)) return r;
                }
            return 99;
        }

        // ------------------------------------------------------------------ the golfer at address

        /// The golfer at address on the tee (GolfGame.BeginAim, as in play), rendered alone on black (a matte: every other renderer off,
        /// same lights and shadows on the figure) under the hole's light and under the legacy golf light (sun 1.1 + grey ambient) it
        /// was tuned for. Clipped = a channel >= 250.
        static void Golfer(int n)
        {
            var golfer = (GolfArcade.Game.GolferView)typeof(GolfArcade.Game.GolfGame).GetField("golfer", Private).GetValue(game);
            typeof(GolfArcade.Game.GolfGame).GetMethod("BeginAim", Private).Invoke(game, new object[] { false });
            rig.SnapNext(); rig.ApplyFrame();
            Shot($"hole{n:00}_golfer_full");
            var mine = golfer.GetComponentsInChildren<Renderer>(false);
            var a = Matte(mine, $"hole{n:00}_golfer_matte");
            GolfAtmosphere.Apply(sun, cam, 7);                    // the legacy numbers (what the golfer looked like before this pass)
            var legacy = Matte(mine, $"hole{n:00}_golfer_matte_legacy");
            GolfAtmosphere.Apply(sun, cam, n);
            // GOLF_LOOK_PROBE_KEYSWEEP=2.3,2.1,1.9: the golfer's clipping against the hole's key intensity (in memory only; the hole's own value is restored)
            string sweep = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_KEYSWEEP");
            if (!string.IsNullOrEmpty(sweep) && GolfAtmosphere.Holes.TryGetValue(n, out var look))
            {
                float keep0 = look.SunIntensity;
                foreach (var t in sweep.Split(','))
                {
                    if (!float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var k)) continue;
                    look.SunIntensity = k; GolfAtmosphere.Apply(sun, cam, n);
                    var f = Matte(mine, $"hole{n:00}_golfer_matte_key{k:F1}");
                    Say($"INFO: SWEEP hole {n} golfer at key {k:F2}: {f.Describe()}");
                }
                look.SunIntensity = keep0; GolfAtmosphere.Apply(sun, cam, n);
            }
            // GOLF_LOOK_PROBE_EXPOSURE="r,g,b;r,g,b": the golfer's clipping / shirt against the hole's FlatExposure (in memory only; the hole's own value is restored)
            string esweep = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_EXPOSURE");
            if (!string.IsNullOrEmpty(esweep) && GolfAtmosphere.Holes.TryGetValue(n, out var elook))
            {
                var keepE = elook.FigureExposure; int k = 0;
                foreach (var t in esweep.Split(';'))
                {
                    var q = t.Split(',');
                    if (q.Length != 3 || !float.TryParse(q[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var er) ||
                        !float.TryParse(q[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var eg) ||
                        !float.TryParse(q[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var eb)) continue;
                    elook.FigureExposure = new Color(er, eg, eb); GolfAtmosphere.Apply(sun, cam, n);
                    var f = Matte(mine, $"hole{n:00}_golfer_matte_exp{k++}");
                    Say($"INFO: SWEEP hole {n} golfer exposure ({er:F2},{eg:F2},{eb:F2}): {f.Describe()}");
                }
                elook.FigureExposure = keepE; GolfAtmosphere.Apply(sun, cam, n);
            }
            // GOLF_LOOK_PROBE_LIGHTSWEEP="name|k=1.8,kc=1:.8:.58,ae=.55:.3:.22,as=..,ag=..,fi=..,x=1:1:1;name2|..": LIGHTING-SIDE variants of the hole's look (SunIntensity, SunColor, AmbientEquator / Sky / Ground,
            // FillIntensity, FigureExposure), in memory only (everything is put back): the golfer's skin / shirt AND the grass the tee view shows (central lower third, golfer hidden), so a lighting change that
            // fixes the figure but moves the grass hue is visible in the same line (area U, 2026-10-04)
            string lightsweep = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_LIGHTSWEEP");
            if (!string.IsNullOrEmpty(lightsweep) && GolfAtmosphere.Holes.TryGetValue(n, out var llook))
            {
                var keep = (llook.SunIntensity, llook.SunColor, llook.AmbientEquator, llook.AmbientSky, llook.AmbientGround, llook.FillIntensity, llook.FigureExposure); float keepEl = llook.SunElevation, keepAz = llook.SunAzimuth;
                foreach (var variant in lightsweep.Split(';'))
                {
                    var parts = variant.Split('|'); string vname = parts[0];
                    llook.SunElevation = keepEl; llook.SunAzimuth = keepAz;
                    llook.SunIntensity = keep.Item1; llook.SunColor = keep.Item2; llook.AmbientEquator = keep.Item3; llook.AmbientSky = keep.Item4; llook.AmbientGround = keep.Item5; llook.FillIntensity = keep.Item6; llook.FigureExposure = keep.Item7;
                    if (parts.Length > 1)
                        foreach (var kv in parts[1].Split(','))
                        {
                            var q = kv.Split('='); if (q.Length != 2) continue;
                            float[] f = q[1].Split(':').Select(t => float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f).ToArray();
                            switch (q[0])
                            {
                                case "k": llook.SunIntensity = f[0]; break;
                                case "el": llook.SunElevation = f[0]; break;
                                case "az": llook.SunAzimuth = f[0]; break;
                                case "kc": llook.SunColor = new Color(f[0], f[1], f[2]); break;
                                case "ae": llook.AmbientEquator = new Color(f[0], f[1], f[2]); break;
                                case "as": llook.AmbientSky = new Color(f[0], f[1], f[2]); break;
                                case "ag": llook.AmbientGround = new Color(f[0], f[1], f[2]); break;
                                case "fi": llook.FillIntensity = f[0]; break;
                                case "x": llook.FigureExposure = new Color(f[0], f[1], f[2]); break;
                            }
                        }
                    GolfAtmosphere.Apply(sun, cam, n);
                    var fg = Matte(mine, $"hole{n:00}_golfer_matte_light_{vname}");
                    golfer.SetVisible(false);
                    var gpx = Shot($"hole{n:00}_tee_light_{vname}");
                    var grass = GolfLookBoards.MeanHsv(LookPixels.Rows(gpx, W, H, 0, H / 3).Where((c, i) => (i % W) > W / 4 && (i % W) < 3 * W / 4).ToList());
                    golfer.SetVisible(true);
                    Say($"INFO: LIGHTSWEEP hole {n} '{vname}': golfer skin clipped {fg.SkinClip * 100:F1} % shirt {LookPixels.Rgb(fg.BlueMean)} ({fg.Blue} px) figure 2+ clipped {fg.Blown * 100:F1} %; tee ground (central lower third) {LookPixels.Rgb(grass.Mean)} hue {grass.H:F0} S {grass.S:F2} V {grass.V:F2} lum {grass.Lum:F0}");
                }
                (llook.SunIntensity, llook.SunColor, llook.AmbientEquator, llook.AmbientSky, llook.AmbientGround, llook.FillIntensity, llook.FigureExposure) = keep; llook.SunElevation = keepEl; llook.SunAzimuth = keepAz;
                GolfAtmosphere.Apply(sun, cam, n);
            }
            // GOLF_LOOK_PROBE_LAVAISWEEP=600,400,300: the same for the lava point lights' intensity (hole 10; in memory only, the lights are put back)
            string lsweep = System.Environment.GetEnvironmentVariable("GOLF_LOOK_PROBE_LAVAISWEEP");
            var lamps = HoleView.Current ? HoleView.Current.GetComponentsInChildren<Light>(false).Where(l => l.type == LightType.Point).ToArray() : new Light[0];
            if (!string.IsNullOrEmpty(lsweep) && n == 10 && lamps.Length > 0)
            {
                float keep1 = lamps[0].intensity;
                foreach (var t in lsweep.Split(','))
                {
                    if (!float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var k)) continue;
                    foreach (var l in lamps) l.intensity = k;
                    var f = Matte(mine, $"hole{n:00}_golfer_matte_lava{k:F0}");
                    Say($"INFO: SWEEP hole {n} golfer with {lamps.Length} lava lights at intensity {k:F0}: {f.Describe()}");
                }
                foreach (var l in lamps) l.intensity = keep1;
            }
            golfer.SetVisible(false); AimVisuals(false);
            Say($"INFO: hole {n} golfer (hole light): {a.Describe()}");
            Say($"INFO: hole {n} golfer (legacy light): {legacy.Describe()}");
            bool ok = a.Pixels > 2000 && a.SkinClip <= .05f && a.BlueClip <= .05f && a.Blown <= Mathf.Max(.05f, legacy.Blown + .02f);
            // review K 2026-10-04: the shirt must stay a blue shirt (Crater's orange light crushed it to dark navy (37,49,64) over 5.4k px against 16.6k px of (47,90,128) under the legacy light)
            float lumA = LookPixels.Lum(LookPixels.ToColor32(a.BlueMean)), lumL = LookPixels.Lum(LookPixels.ToColor32(legacy.BlueMean));
            bool shirt = a.Blue >= .6f * legacy.Blue && lumA >= .6f * lumL && a.BlueMean.b >= a.BlueMean.r + 20f / 255f;
            Gate($"GOLFER_SHIRT_READABLE hole{n}", shirt,
                $"shirt px {a.Blue} (>= 60 % of the legacy light's {legacy.Blue}), mean {LookPixels.Rgb(a.BlueMean)} luminance {lumA:F0} (>= 60 % of the legacy {lumL:F0} = {.6f * lumL:F0}), B - R {(a.BlueMean.b - a.BlueMean.r) * 255:F0} levels (>= 20); exposure ({GolfFigureExposure.Exposure.r:F2},{GolfFigureExposure.Exposure.g:F2},{GolfFigureExposure.Exposure.b:F2})");
            Gate($"GOLFER_NOT_CLIPPED hole{n}", ok,
                $"{a.Pixels} golfer px; skin px with a channel >= 250: {a.SkinClip * 100:F1} % (<= 5 %), shirt/blue {a.BlueClip * 100:F1} % (<= 5 %), whole figure with 2+ channels >= 250 {a.Blown * 100:F1} % (<= max(5 %, legacy {legacy.Blown * 100:F1} % + 2))");
        }

        sealed class Figure
        {
            public int Pixels, Skin, Blue, White;
            public float SkinClip, BlueClip, Blown, AnyClip;
            public Color SkinMean, BlueMean, Mean;
            public string Describe() => $"{Pixels} px, mean {LookPixels.Rgb(Mean)}; skin {Skin} px mean {LookPixels.Rgb(SkinMean)} clipped {SkinClip * 100:F1} %; blue {Blue} px mean {LookPixels.Rgb(BlueMean)} clipped {BlueClip * 100:F1} %; near-white {White} px; any channel >= 250 {AnyClip * 100:F1} %, 2+ channels {Blown * 100:F1} %";
        }

        static Figure Matte(Renderer[] keep, string file)
        {
            var keepSet = new HashSet<Renderer>(keep);
            var off = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && !keepSet.Contains(r)).ToArray();
            foreach (var r in off) r.enabled = false;
            var flags = cam.clearFlags; var bg = cam.backgroundColor;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var px = Shot(file);
            cam.clearFlags = flags; cam.backgroundColor = bg;
            foreach (var r in off) r.enabled = true;

            var f = new Figure(); var all = new List<Color32>(); var skin = new List<Color32>(); var blue = new List<Color32>();
            int skinClip = 0, blueClip = 0, blown = 0, any = 0;
            foreach (var p in px)
            {
                if (p.r + p.g + p.b <= 6) continue;
                all.Add(p);
                int clipped = (p.r >= 250 ? 1 : 0) + (p.g >= 250 ? 1 : 0) + (p.b >= 250 ? 1 : 0);
                if (clipped > 0) any++;
                if (clipped >= 2) blown++;
                Color.RGBToHSV(p, out var h, out var s, out var v); h *= 360;
                if (h >= 5 && h <= 60 && s >= .15f && s <= .8f && v >= .25f) { skin.Add(p); if (clipped > 0) skinClip++; }
                else if (h >= 190 && h <= 250 && s >= .25f) { blue.Add(p); if (clipped > 0) blueClip++; }
                else if (s < .15f && v > .7f) f.White++;
            }
            f.Pixels = all.Count; f.Skin = skin.Count; f.Blue = blue.Count;
            f.Mean = LookPixels.MeanOf(all, 0, all.Count); f.SkinMean = LookPixels.MeanOf(skin, 0, skin.Count); f.BlueMean = LookPixels.MeanOf(blue, 0, blue.Count);
            f.SkinClip = skin.Count > 0 ? skinClip / (float)skin.Count : 0; f.BlueClip = blue.Count > 0 ? blueClip / (float)blue.Count : 0;
            f.Blown = all.Count > 0 ? blown / (float)all.Count : 0; f.AnyClip = all.Count > 0 ? any / (float)all.Count : 0;
            return f;
        }
    }
}

namespace GolfArcade.EditorTools
{
    /// Game-view measurements on test boards: the real LK_ materials on small test geometry, lit by the real hole atmosphere
    /// (GolfAtmosphere.Apply), seen by a phone-framed camera (CameraRig.FrameAddress geometry, 60 deg vertical FOV, near .3, 900x1600)
    /// or an aerial one. Works while the hole FBXs are still the flat baseline. There is NO tone mapper and NO post in this project:
    /// what these boards read is sRGB( clip( light x albedo + emission ) ).
    ///   GOLF_LOOK_SMOKE_SHOTS=<dir>  writes the board renders (jpg)
    ///   GOLF_LOOK_SWEEP=1            also prints lava / basalt / shelf / smoke parameter sweeps (INFO lines) for tuning
    internal static class GolfLookBoards
    {
        // ---- thresholds (the gates)
        public const float LavaHueMin = 8, LavaHueMax = 28, LavaSatMin = .75f, LavaValMin = .70f, LavaShareMin = .85f, LavaClipGMax = .05f;
        public const float ShelfHueMin = 175, ShelfHueMax = 200, ShelfValMax = .65f;
        // shelf: independent of any foam (the white foam strips are deleted): a thin edge, not a halo. Luminance <= ShelfLumMax, <= ShelfVsOcean x the open sea seen beside it, never in the brightest 2 % of the aerial frame
        public const float ShelfLumMax = 100, ShelfVsOcean = 1.3f, ShelfTopShareMax = .10f;
        // basalt (BASALT_RENDERS_DARK): dark charcoal, not brown planks
        // v2 repair round 3 (2026-10-05): SmokeAlphaMin .55 -> .30. The .55 floor was a first-build PROXY for "a plume somebody can pick out" (the .49 card moved the frame by p99 14.5); visibility is gated directly by the two delta
        // bands next to it (median >= 8, p99 >= 28 levels, both unchanged) and, on the real hole, by SMOKE_VISIBLE_STILLS (>= 1.5 % of every Crater still moves by >= 10 levels). The floor cannot stand next to the Game-view cap
        // SMOKE_SOFT (|dLum| max <= 110 over the near-black wall): the real plume is 2-3 stacked cards, so a per-card peak of .58 stacks to ~.9 and moves the approach frame by 129 levels (limit 110). With the per-card
        // peak at .33 the board card still moves the frame by median 11.9 / p99 40.2 (130 yd) and 10.5 / 37.0 (430 yd), and the real stills 1.9 / 3.2 / 3.9 % (readable share). The upper bound (a card is never a ball) is unchanged.
        public const float SmokeAlphaMin = .30f, SmokeAlphaMax = .72f, SmokeDeltaP99Min = 28, SmokeDeltaMedianMin = 8;
        public const float BasaltMedianMax = 40, BasaltSatMax = .25f, BasaltLitMedianMax = 55, BasaltLitSatMax = .65f;

        public sealed class Rig : System.IDisposable
        {
            public readonly int Hole, W, H;
            public readonly GameObject Root; public readonly Light Sun; public readonly Camera Cam;
            readonly RenderTexture rt; readonly Texture2D tex; readonly LookPixels.RenderState saved;
            public string ShotsDir;

            public Rig(int hole, int w = 900, int h = 1600)
            {
                Hole = hole; W = w; H = h;
                ShotsDir = System.Environment.GetEnvironmentVariable("GOLF_LOOK_SMOKE_SHOTS");
                saved = LookPixels.SaveRenderSettings();
                Root = new GameObject("GolfLook board " + hole);
                Sun = new GameObject("Board sun").AddComponent<Light>(); Sun.transform.SetParent(Root.transform);
                Cam = new GameObject("Board camera").AddComponent<Camera>(); Cam.transform.SetParent(Root.transform);
                Cam.fieldOfView = 60; Cam.nearClipPlane = .3f; Cam.farClipPlane = 900; Cam.aspect = w / (float)h;
                GolfAtmosphere.Apply(Sun, Cam, hole);
                rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            }

            /// Unit vector down the hole (the heading GolfAtmosphere uses when no HoleView is built).
            public Vector3 Aim { get { float a = GolfAtmosphere.Holes[Hole].FallbackHeading * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); } }
            public Vector3 Right => Vector3.Cross(Vector3.up, Aim);
            /// A point in the hole frame: x to the golfer's right, y up (world yards), z down the hole.
            public Vector3 P(float x, float y, float z) => Right * x + Vector3.up * y + Aim * z;

            /// CameraRig.FrameAddress for a full shot: 4.5 yd behind the ball, 2.4 yd up, looking 1.5 yd ahead of the ball.
            public void Phone(Vector3 ball, Vector3 aim)
            {
                aim.y = 0; aim.Normalize();
                Cam.transform.position = ball - aim * 4.5f + Vector3.up * 2.4f;
                Cam.transform.rotation = Quaternion.LookRotation(ball + aim * 1.5f - Cam.transform.position, Vector3.up);
                Cam.fieldOfView = 60;
            }

            public void Look(Vector3 from, Vector3 at, float fov = 60) { Cam.transform.position = from; Cam.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up); Cam.fieldOfView = fov; }

            public Color32[] Shot(string name = null)
            {
                var px = LookPixels.Render(Cam, rt, tex);
                if (name != null) LookPixels.Save(tex, ShotsDir, name + ".jpg");
                return px;
            }

            /// A render with some renderers (or whole objects) switched off.
            public Color32[] Without(string name, params GameObject[] off)
            {
                var rs = off.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Where(x => x.enabled).ToArray();
                foreach (var x in rs) x.enabled = false;
                var px = Shot(name);
                foreach (var x in rs) x.enabled = true;
                return px;
            }

            /// Only `keep` rendered, on black (no sky): the object's own pixels, unobstructed ones equal to the full frame.
            public bool[] SoloMask(Color32[] full, GameObject keep, int tol = 8)
            {
                var others = Root.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled && !x.transform.IsChildOf(keep.transform)).ToArray();
                foreach (var x in others) x.enabled = false;
                var flags = Cam.clearFlags; var bg = Cam.backgroundColor;
                Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Color.black;
                var solo = Shot(null);
                Cam.clearFlags = flags; Cam.backgroundColor = bg;
                foreach (var x in others) x.enabled = true;
                var m = new bool[full.Length];
                for (int i = 0; i < m.Length; i++)
                    m[i] = solo[i].r + solo[i].g + solo[i].b > 6 && Mathf.Abs(full[i].r - solo[i].r) + Mathf.Abs(full[i].g - solo[i].g) + Mathf.Abs(full[i].b - solo[i].b) <= tol;
                return m;
            }

            /// Pixels an object contributes to the frame (the frame with and without it differs by more than `threshold`).
            public bool[] MaskOf(Color32[] full, GameObject obj, int threshold = 30) => LookPixels.Diff(full, Without(null, obj), threshold, out _);

            public void Dispose()
            {
                if (Root) Object.DestroyImmediate(Root);
                if (rt) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex) Object.DestroyImmediate(tex);
                foreach (var n in new[] { "Golf fill", "Golf rim" }) { var go = GameObject.Find(n); if (go) Object.DestroyImmediate(go); }
                LookPixels.RestoreRenderSettings(saved);
            }
        }

        // ------------------------------------------------------------------ mesh helpers

        /// A regular grid: origin + u * i/nu + v * j/nv, normal up, UV0 = world (x, z) metres / uvMetres (tile units), optional vertex colours.
        public static GameObject Grid(Rig r, string name, Material m, Vector3 origin, Vector3 u, Vector3 v, int nu, int nv, float uvMetres = 8, System.Func<Vector3, Color> colour = null, bool shadows = false)
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var cols = new List<Color>(); var nrm = new List<Vector3>(); var idx = new List<int>();
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                {
                    var p = origin + u * (i / (float)nu) + v * (j / (float)nv);
                    verts.Add(p); nrm.Add(Vector3.up); uvs.Add(new Vector2(p.x, p.z) * (.9144f / uvMetres));
                    if (colour != null) cols.Add(colour(p));
                }
            bool up = Vector3.Dot(Vector3.Cross(u, v), Vector3.up) > 0;         // (a, b, c) faces cross(u, v); wind so the face points up
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int a = j * (nu + 1) + i, b = a + 1, c = a + nu + 1, d = c + 1;
                    if (up) idx.AddRange(new[] { a, b, c, b, d, c }); else idx.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts); mesh.SetNormals(nrm); mesh.SetUVs(0, uvs);
            if (colour != null) mesh.SetColors(cols);
            mesh.SetTriangles(idx, 0); mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(r.Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// A vertical card (a bottom-left, width along `across`, height up): UV (0,0)..(1,1), vertex alpha 1.
        public static GameObject Card(Rig r, string name, Material m, Vector3 bottomCentre, Vector3 across, float width, float height)
        {
            var a = bottomCentre - across * (width / 2); var b = bottomCentre + across * (width / 2);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new[] { a, b, b + Vector3.up * height, a + Vector3.up * height });
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetColors(new[] { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(r.Root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ pixel statistics

        public struct Hsv { public int N; public Color Mean; public float H, S, V, Lum; }

        public static Hsv MeanHsv(IList<Color32> px)
        {
            var o = new Hsv { N = px.Count };
            if (px.Count == 0) return o;
            o.Mean = LookPixels.MeanOf(px, 0, px.Count);
            Color.RGBToHSV(o.Mean, out var h, out var s, out var v);
            o.H = h * 360; o.S = s; o.V = v; o.Lum = LookPixels.Lum(LookPixels.ToColor32(o.Mean));
            return o;
        }

        /// Mean luminance of the brightest `fraction` of the pixels (the "brightest patch").
        public static float TopLum(IList<Color32> px, float fraction)
        {
            if (px.Count == 0) return 0;
            var l = px.Select(LookPixels.Lum).OrderByDescending(x => x).ToArray();
            int n = Mathf.Max(1, Mathf.RoundToInt(l.Length * fraction));
            return l.Take(n).Average();
        }

        public static float[] RowLum(Color32[] px, int w, int y0, int y1)
        {
            double s = 0; int n = 0;
            for (int y = y0; y < y1; y++) for (int x = 0; x < w; x++) { s += LookPixels.Lum(px[y * w + x]); n++; }
            return new[] { n > 0 ? (float)(s / n) : 0 };
        }

        public struct LavaStats { public int N; public float InBand, ClipG, Cream, Dark, Pale, Yellow, Red, MeanH, MeanS, MeanV; public Color Mean; }

        /// The lava gate's pixel test: hue 8..28 deg, S >= .75, V >= .70 (crater.jpg lava (212,62,14) h15 S.93 V.83); failures are sorted by cause.
        public static LavaStats Lava(IList<Color32> px)
        {
            var o = new LavaStats { N = px.Count };
            if (px.Count == 0) return o;
            int ok = 0, clipG = 0, cream = 0, dark = 0, pale = 0, yellow = 0, red = 0;
            foreach (var p in px)
            {
                Color.RGBToHSV(p, out var h, out var s, out var v); h *= 360;
                bool hueOk = h >= LavaHueMin && h <= LavaHueMax, satOk = s >= LavaSatMin, valOk = v >= LavaValMin;
                if (hueOk && satOk && valOk) ok++;
                else if (!valOk) dark++;
                else if (!satOk) pale++;
                else if (h > LavaHueMax) yellow++;
                else red++;
                if (p.g >= 250) clipG++;
                if (Mathf.Min(p.r, Mathf.Min(p.g, p.b)) >= 230) cream++;
            }
            float n = px.Count;
            o.InBand = ok / n; o.ClipG = clipG / n; o.Cream = cream / n; o.Dark = dark / n; o.Pale = pale / n; o.Yellow = yellow / n; o.Red = red / n;
            var m = MeanHsv(px); o.Mean = m.Mean; o.MeanH = m.H; o.MeanS = m.S; o.MeanV = m.V;
            return o;
        }

        static string LavaLine(LavaStats s) =>
            $"{s.N} px: in band {s.InBand * 100:F1} % (>= {LavaShareMin * 100:F0}), G clipped {s.ClipG * 100:F1} % (<= {LavaClipGMax * 100:F0}), cream {s.Cream * 100:F2} %; misses: dark {s.Dark * 100:F1} %, pale {s.Pale * 100:F1} %, yellow {s.Yellow * 100:F1} %, red {s.Red * 100:F1} %; mean {LookPixels.Rgb(s.Mean)} h{s.MeanH:F0} s{s.MeanS:F2} v{s.MeanV:F2}";

        public static string PipelineName()
        {
            var p = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            return p ? p.name : "built-in";
        }

        // ------------------------------------------------------------------ hole 10: lava, lava lights, basalt

        public struct CraterResult { public LavaStats Phone, Down; public float LightShare, LightGain; public LavaStats LavaLit; public bool Ran; }

        /// The Crater board: a lava sheet (240 yd, LK_LAVA, UV = metres / 24) with four LAVA_LIGHT_nn empties dressed by GolfLook.DressModel
        /// (point lights), seen from a rim-height phone camera (grazing) and from 40 degrees down; plus a basalt wall beside the lava lights.
        public static CraterResult Crater(System.Action<string, bool, string> gate, System.Action<string> say, string tag, bool sweep)
        {
            var res = new CraterResult { Ran = true };
            using var r = new Rig(10);
            var lavaMat = GolfLook.GetForHole("LK_LAVA", 10);
            string candidate = System.Environment.GetEnvironmentVariable("GOLF_LOOK_LAVA_CANDIDATE");     // a folder with Lava_C/N/E.png: tune before they are installed
            if (!string.IsNullOrEmpty(candidate)) { lavaMat = Candidate(lavaMat, candidate); Say(say, $"INFO: {tag}CRATER lava uses the CANDIDATE textures in {candidate} (not the installed Look/ ones)"); }
            var lava = Grid(r, "WATER_LAVA board", lavaMat, r.P(-150, 0, 4), r.Right * 300, r.Aim * 220, 30, 22, 24);
            // lava lights: four empties 2.4 yd over the lava (as the hole builder places LAVA_LIGHT_nn), two near the wall, two out in the pool
            var empties = new[] { r.P(-34, 2.4f, 42), r.P(-34, 2.4f, 78), r.P(30, 2.4f, 60), r.P(0, 2.4f, 110) };
            for (int i = 0; i < empties.Length; i++) { var e = new GameObject($"LAVA_LIGHT_{i + 1:00}"); e.transform.SetParent(r.Root.transform, false); e.transform.position = empties[i]; }
            // a basalt wall along x = -40 facing the hole (lit by the key and the lava lights)
            var basaltMat = GolfLook.GetForHole("LK_BASALT", 10);
            var wall = new GameObject("ROCK_WALL board"); wall.transform.SetParent(r.Root.transform, false);
            // five 20 yd chunks, like the hole builder's wall pieces: a renderer only gets the few lights nearest ITS bounds (the per-object
            // additional-lights limit is 2 in TennisURP, 4 in HeroBaseStudioURP), so one 100 yd mesh would only ever see its nearest light
            for (int c = 0; c < 5; c++)
                LookPixels.Quad(wall, "wall " + c, basaltMat, 8, r.P(-40, 0, 20 + 20 * c), r.P(-40, 0, 40 + 20 * c), r.P(-40, 26, 40 + 20 * c), r.P(-40, 26, 20 + 20 * c), r.Right);
            GolfLook.DressModel(r.Root, 10);
            int pointLights = r.Root.GetComponentsInChildren<Light>(true).Count(l => l.type == LightType.Point);
            var scene = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.gameObject.activeInHierarchy).ToArray();
            Say(say, $"INFO: {tag}CRATER board lights in the scene: {scene.Length} enabled ({string.Join(", ", scene.GroupBy(l => l.type).Select(g => g.Count() + " " + g.Key).ToArray())}); main light = '{(RenderSettings.sun ? RenderSettings.sun.name : "none")}', additional directionals {scene.Count(l => l.type == LightType.Directional && l != RenderSettings.sun)}");

            // views: (a) phone-like at rim height looking over the pool, (b) 40 degrees down
            r.Look(r.P(0, 8.96f, -6), r.P(0, 0, 40));
            var phone = r.Shot("crater_lava_phone");
            var lavaOnly = LookPixels.Diff(phone, r.Without(null, lava), 30, out _);
            res.Phone = Lava(LookPixels.Masked(phone, lavaOnly));
            Say(say, $"INFO: {tag}CRATER lava, phone view (rim height, grazing), {PipelineName()}, {pointLights} lava point lights: {LavaLine(res.Phone)}");

            r.Look(r.P(0, 40, 0), r.P(0, 0, 60));
            var down = r.Shot("crater_lava_down");
            var downMask = LookPixels.Diff(down, r.Without(null, lava), 30, out _);
            res.Down = Lava(LookPixels.Masked(down, downMask));
            Say(say, $"INFO: {tag}CRATER lava, 40 deg down view: {LavaLine(res.Down)}");

            // lava lights on the basalt wall: witness view facing the wall next to a light, lights on vs off; and the pool must not flare
            var lights = r.Root.GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Point).ToArray();
            var reach = LightReach(r, wall, lava, lights);
            res.LightShare = reach.share; res.LightGain = reach.gain;
            Say(say, $"INFO: {tag}CRATER lava lights ({pointLights} point lights I {GolfLook.LavaLightIntensity} range {GolfLook.LavaLightRange} lift {GolfLook.LavaLightLift}, additional-lights limit {AdditionalLightsLimit()}): wall {reach.wallPx} px, +{reach.gain:F1} luminance on average, >= +8 on {reach.share * 100:F1} % of it; lava pixels brightened by >= 40 levels: {reach.hot * 100:F1} %");

            // the lights must not yellow the grass: a fairway ledge 10-14 yd from a light, lights on vs off
            var ledge = new GameObject("GREEN_ledge board"); ledge.transform.SetParent(r.Root.transform, false);
            LookPixels.Quad(ledge, "ledge", GolfLook.GetForHole("LK_FAIRWAY", 10), 10, r.P(36, 6.56f, 44), r.P(36, 6.56f, 76), r.P(70, 6.56f, 76), r.P(70, 6.56f, 44), Vector3.up);
            r.Look(r.P(34, 12, 40), r.P(52, 6.56f, 62));
            var gOn = r.Shot("crater_ledge_lights_on");
            var gMask = LookPixels.Diff(gOn, r.Without(null, ledge), 30, out _);
            foreach (var l in lights) l.enabled = false;
            var gOff = r.Shot("crater_ledge_lights_off");
            foreach (var l in lights) l.enabled = true;
            var gl = MeanHsv(LookPixels.Masked(gOn, gMask)); var go0 = MeanHsv(LookPixels.Masked(gOff, gMask));
            int gClip = LookPixels.Masked(gOn, gMask).Count(p => p.g >= 250); int gN = Mathf.Max(1, gl.N);
            Say(say, $"INFO: {tag}CRATER grass ledge 10-30 yd from the lava lights: lights off {LookPixels.Rgb(go0.Mean)} hue {go0.H:F0} V {go0.V:F2}; lights on {LookPixels.Rgb(gl.Mean)} hue {gl.H:F0} V {gl.V:F2}, G clipped {gClip * 100f / gN:F1} %");
            gate($"{tag}LAVA_LIGHTS_KEEP_GRASS_GREEN", gl.N > 5000 && Mathf.Abs(gl.H - go0.H) <= 4 && gClip <= gN * .05f,
                $"grass near the lights: hue {go0.H:F0} -> {gl.H:F0} (|shift| <= 4), G clipped {gClip * 100f / gN:F1} % (<= 5); {gl.N} px");

            // basalt glow (cracks): the pixels the emission contributes, wall seen head-on in the key light
            var toward = -r.Sun.transform.forward; toward.y = 0; toward = toward.normalized;
            var side = Vector3.Cross(Vector3.up, toward).normalized;
            var glowWall = new GameObject("ROCK_GLOW board"); glowWall.transform.SetParent(r.Root.transform, false);
            LookPixels.Quad(glowWall, "glow wall", basaltMat, 8, -side * 30 + Vector3.right * 400, side * 30 + Vector3.right * 400, side * 30 + Vector3.right * 400 + Vector3.up * 30, -side * 30 + Vector3.right * 400 + Vector3.up * 30, toward);
            var camAt = Vector3.right * 400 + Vector3.up * 12;
            r.Look(camAt + toward * 24, camAt, 50);
            var bFull = r.Shot("crater_basalt");
            var noEmission = new Material(basaltMat) { name = "LK_BASALT no emission" };
            noEmission.SetVector("_EmissionColor", Vector4.zero);
            var gr = glowWall.GetComponentInChildren<Renderer>(); var shipped = gr.sharedMaterial;
            gr.sharedMaterial = noEmission;
            var bNone = r.Shot("crater_basalt_noemission");
            gr.sharedMaterial = shipped; Object.DestroyImmediate(noEmission);
            var wallPxAll = LookPixels.Diff(bFull, r.Without(null, glowWall), 30, out int wallAll);
            // what the rock itself reads (emission off, so the joints cannot lift the numbers): median luminance, mean colour / saturation
            var rockList = LookPixels.Masked(bNone, wallPxAll);
            var rockStats = LookPixels.Analyse(new List<Color32>(rockList), .02f); var rockHsv = MeanHsv(rockList);
            var glowMask = LookPixels.Diff(bFull, bNone, 24, out int glowN);
            var glow = new List<Color32>();
            for (int i = 0; i < bFull.Length; i++) if (glowMask[i] && wallPxAll[i]) glow.Add(bFull[i]);
            var gs = MeanHsv(glow);
            int cream = glow.Count(p => Mathf.Min(p.r, Mathf.Min(p.g, p.b)) >= 230), clipG = glow.Count(p => p.g >= 250);
            var whole = LookPixels.Masked(bFull, wallPxAll);
            var wholeStats = LookPixels.Analyse(new List<Color32>(whole), .02f);
            float coverage = wallAll > 0 ? glow.Count / (float)wallAll : 0;
            // the glow pixels' own hue spread: share inside hue 8..32
            int hueIn = 0; foreach (var p in glow) { Color.RGBToHSV(p, out var hh, out _, out _); hh *= 360; if (hh >= 8 && hh <= 32) hueIn++; }
            float hueShare = glow.Count > 0 ? hueIn / (float)glow.Count : 0;
            Say(say, $"INFO: {tag}BASALT glow pixels (emission adds >= 24 levels): {glow.Count} px = {coverage * 100:F1} % of the wall, mean {LookPixels.Rgb(gs.Mean)} hue {gs.H:F0} S {gs.S:F2} V {gs.V:F2}, hue 8..32 on {hueShare * 100:F0} %, cream {(glow.Count > 0 ? cream * 100f / glow.Count : 0):F2} %, G clipped {(glow.Count > 0 ? clipG * 100f / glow.Count : 0):F2} %; wall median luminance {wholeStats.MedianLum:F0}");
            res.LavaLit = res.Phone;
            if (sweep)
            {
                foreach (var m in new[] { new Vector3(1, .5f, .35f), new Vector3(1, .7f, .45f), new Vector3(1, .8f, .5f), new Vector3(1, 1, .6f), new Vector3(1.3f, .9f, .5f), new Vector3(1.6f, .9f, .5f), new Vector3(2, 1, .6f) })
                {
                    var c = new Material(basaltMat) { name = "basalt sweep" };
                    c.SetVector("_EmissionColor", new Vector4(m.x, m.y, m.z, 1));
                    gr.sharedMaterial = c;
                    var f = r.Shot(null);
                    var mk = LookPixels.Diff(f, bNone, 24, out _);
                    var gls = new List<Color32>();
                    for (int i = 0; i < f.Length; i++) if (mk[i] && wallPxAll[i]) gls.Add(f[i]);
                    var st = MeanHsv(gls);
                    int hin = 0, cr = 0; foreach (var p in gls) { Color.RGBToHSV(p, out var hh, out _, out _); hh *= 360; if (hh >= 8 && hh <= 32) hin++; if (Mathf.Min(p.r, Mathf.Min(p.g, p.b)) >= 230) cr++; }
                    say($"INFO: {tag}SWEEP basalt emission ({m.x:F2},{m.y:F2},{m.z:F2}): {gls.Count} glow px = {(wallAll > 0 ? gls.Count * 100f / wallAll : 0):F1} % of the wall, mean {LookPixels.Rgb(st.Mean)} hue {st.H:F0}, hue 8..32 on {(gls.Count > 0 ? hin * 100f / gls.Count : 0):F0} %, cream {(gls.Count > 0 ? cr * 100f / gls.Count : 0):F2} %");
                    gr.sharedMaterial = shipped; Object.DestroyImmediate(c);
                }
            }

            bool lavaPhoneOk = res.Phone.N > 20000 && res.Phone.InBand >= LavaShareMin && res.Phone.ClipG <= LavaClipGMax, lavaDownOk = res.Down.N > 20000 && res.Down.InBand >= LavaShareMin && res.Down.ClipG <= LavaClipGMax;
            gate($"{tag}LAVA_ORANGE_NO_BLOOM phone", lavaPhoneOk, LavaLine(res.Phone));
            gate($"{tag}LAVA_ORANGE_NO_BLOOM down", lavaDownOk, LavaLine(res.Down));
            gate($"{tag}LAVA_GAMEVIEW_ORANGE", lavaPhoneOk && lavaDownOk, $"calibration id (both lava views, post OFF, no bloom): phone {res.Phone.InBand * 100:F1} % / down {res.Down.InBand * 100:F1} % of the lava pixels in hue {LavaHueMin}..{LavaHueMax}, S >= {LavaSatMin}, V >= {LavaValMin} (>= {LavaShareMin * 100:F0} %), G clipped {res.Phone.ClipG * 100:F1} % / {res.Down.ClipG * 100:F1} % (<= {LavaClipGMax * 100:F0} %), mean {LookPixels.Rgb(res.Phone.Mean)} h{res.Phone.MeanH:F0} S{res.Phone.MeanS:F2} V{res.Phone.MeanV:F2} (crater.jpg pool (212,62,14) h15 S.93 V.83)");
            // LAVA_FAR_ORANGE (v2 repair round 3): the cone's lava stands 430 yd from the tee and takes Crater's distance fog; a neutral grey haze over it took its saturation below the band in the tee still (65 % in band) while the
            // board lava (4..220 yd) passed. A lava slope 160 x 90 yd at 430 yd facing the camera, real fog / lights, no post: >= 85 % of its pixels in the orange band, <= 5 % G clipped (the same bands as the near boards).
            {
                var farLavaGo = new GameObject("WATER_LAVA far board"); farLavaGo.transform.SetParent(r.Root.transform, false);
                LookPixels.Quad(farLavaGo, "lava far", GolfLook.GetForHole("LK_LAVA", 10), 24, r.P(-80, 0, 430), r.P(80, 0, 430), r.P(80, 90, 430), r.P(-80, 90, 430), -r.Aim);
                r.Look(r.P(0, 9, 0), r.P(0, 45, 430));
                var farShot = r.Shot("crater_lava_far");
                var farMask = LookPixels.Diff(farShot, r.Without(null, farLavaGo), 30, out _);
                var farLava = Lava(LookPixels.Masked(farShot, farMask));
                gate($"{tag}LAVA_FAR_ORANGE", farLava.N > 3000 && farLava.InBand >= LavaShareMin && farLava.ClipG <= LavaClipGMax, "lava slope 430 yd out through Crater's fog (GolfLava _FogShare " + GolfLook.Table["LK_LAVA"].FogShare.ToString("F2") + "): " + LavaLine(farLava));
                Object.DestroyImmediate(farLavaGo);
            }
            // review K 2026-10-04: the lava read as orange paint (no dark crust at all: V < .4 on 0.0 % of the pool) and the brightest tenth of the lit basalt read green-grey ((55,60,55) hue 121).
            // LAVA_HAS_CRUST: some real near-black crust (2.5..10 % of the lava pixels darker than V .4, inside the 85 % in-band budget) and a luminance spread (sd >= 25). Round 1 measured 0.0 % / sd 23.3; the shipped
            // round-2 ramp 3.5 % / sd 26.5. crater.jpg's pool is 12..30 % darker than V .4 (lum sd 39..44) but the lead's 85 % in-band rule leaves 15 % for everything that is not bright orange.
            {
                var lavaPx = LookPixels.Masked(phone, lavaOnly); int vd = 0; double ls = 0, lq = 0;
                foreach (var p in lavaPx) { Color.RGBToHSV(p, out _, out _, out var v); if (v < .40f) vd++; float l = LookPixels.Lum(p); ls += l; lq += l * l; }
                double lm = ls / System.Math.Max(1, lavaPx.Count), lsd = System.Math.Sqrt(System.Math.Max(0, lq / System.Math.Max(1, lavaPx.Count) - lm * lm));
                float crustShare = vd / (float)System.Math.Max(1, lavaPx.Count);
                gate($"{tag}LAVA_HAS_CRUST", lavaPx.Count > 20000 && crustShare >= .025f && crustShare <= .10f && lsd >= 25, $"phone view: {crustShare * 100:F1} % of the {lavaPx.Count} lava px darker than V .4 (2.5..10 %), luminance sd {lsd:F1} (>= 25); in band {res.Phone.InBand * 100:F1} % (>= {LavaShareMin * 100:F0})");
            }
            {
                var rockPx = new List<Color32>(LookPixels.Masked(bNone, wallPxAll));
                var top10 = MeanHsv(rockPx.OrderByDescending(LookPixels.Lum).Take(Mathf.Max(1, rockPx.Count / 10)).ToList());
                bool notGreen = top10.S <= .05f || top10.H <= 70 || top10.H >= 300;
                gate($"{tag}BASALT_NOT_GREEN", rockPx.Count > 20000 && notGreen, $"brightest tenth of the key-lit wall (emission off) mean {LookPixels.Rgb(top10.Mean)} hue {top10.H:F0} S {top10.S:F2}: neutral (S <= .05) or warm (hue <= 70 / >= 300); not a green-grey cast");
            }
            var core = glow.OrderByDescending(p => LookPixels.Lum(p)).Take(Mathf.Max(1, glow.Count / 4)).ToList();      // the brightest quarter of the glow = the crack cores
            var cs = MeanHsv(core);
            bool glowOk = glow.Count >= 200 && gs.H >= 8 && gs.H <= 32 && cs.H >= 12 && cs.H <= 34 && glow.Count(p => Mathf.Min(p.r, Mathf.Min(p.g, p.b)) >= 230) <= glow.Count * .01f && clipG <= glow.Count * .05f && coverage <= .20f;
            gate($"{tag}BASALT_JOINT_ORANGE", glowOk, $"calibration id (basalt joints orange, not cream; emission alone): {glow.Count} glow px, mean hue {gs.H:F0} (8..32), brightest quarter (crack cores) {LookPixels.Rgb(cs.Mean)} hue {cs.H:F0} (12..34), cream {(glow.Count > 0 ? cream * 100f / glow.Count : 0):F2} % (<= 1), G clipped {(glow.Count > 0 ? clipG * 100f / glow.Count : 0):F2} % (<= 5), {coverage * 100:F1} % of the wall (<= 20)");
            gate($"{tag}BASALT_GLOW_ORANGE", glowOk,
                $"{glow.Count} glow px ({coverage * 100:F1} % of the wall, <= 20): mean hue {gs.H:F0} (8..32), brightest quarter (crack cores) {LookPixels.Rgb(cs.Mean)} hue {cs.H:F0} (12..34), cream {(glow.Count > 0 ? cream * 100f / glow.Count : 0):F2} % (<= 1), G clipped {(glow.Count > 0 ? clipG * 100f / glow.Count : 0):F2} % (<= 5); mean {LookPixels.Rgb(gs.Mean)}");
            // BASALT_SHADOW_SIDE_NEUTRAL (v2 repair round 3, review high, hole 10: the Game-view basalt read dark maroon, mean S .54-.59, while BASALT_RENDERS_DARK passed): the board wall above FACES the key, but the walls the phone
            // camera sees down the hole are the shadow side - lit by ambient + FOG + lava lights only - and Crater's ember-red fog and equator ambient painted them red where no board looked. This board: a wall that faces AWAY from the
            // key (N.L < 0), 250 yd from the camera (the proof stills' walls stand 150-300 yd out: fog 120..1000 yd takes ~15 % of it), emission off. crater.jpg's basalt away from the lava: (17,17,20) .. (39,32,34), S .10-.18.
            // PASS: mean S <= .25 and median luminance <= 40.
            {
                var away = -toward; var sideA = Vector3.Cross(Vector3.up, away).normalized;
                var baseC = new Vector3(800, 0, 800);
                var sWall = new GameObject("ROCK_SHADOW board"); sWall.transform.SetParent(r.Root.transform, false);
                var noEmis = new Material(basaltMat) { name = "LK_BASALT no emission (shadow side)" }; noEmis.SetVector("_EmissionColor", Vector4.zero);
                LookPixels.Quad(sWall, "shadow wall", noEmis, 8, baseC - sideA * 120, baseC + sideA * 120, baseC + sideA * 120 + Vector3.up * 70, baseC - sideA * 120 + Vector3.up * 70, away);
                var cam2 = baseC + Vector3.up * 14;
                r.Look(cam2 + away * 250, cam2, 50);
                var sShot = r.Shot("crater_basalt_shadow_side");
                var sMask = LookPixels.Diff(sShot, r.Without(null, sWall), 30, out int sN);
                var sPx = new List<Color32>(LookPixels.Masked(sShot, sMask));
                var sHsv = MeanHsv(sPx); var sSt = LookPixels.Analyse(new List<Color32>(sPx), .02f);
                gate($"{tag}BASALT_SHADOW_SIDE_NEUTRAL", sN > 20000 && sHsv.S <= .25f && sSt.MedianLum <= 40,
                    $"wall facing AWAY from the key, 250 yd out, emission off (fog + ambient + lava lights only, the case of the proof stills): mean {LookPixels.Rgb(sHsv.Mean)} S {sHsv.S:F2} (<= .25), median luminance {sSt.MedianLum:F0} (<= 40); {sN} px; the Game-view stills measured S .54 / .58 / .53 before round 3");
                Object.DestroyImmediate(noEmis);
            }
            // crater.jpg basalt away from the lava glow (16x16 means / 200x200 crops, glow pixels left out): (26,25,29) .. (39,32,34), median luminance 30..34, mean S .14..28;
            // beside the lava it is warm-lit: (52,27,23) .. (65,40,23), median 30..41, S .5..62. A brown wall (the first build: (69,52,43), median 52, S .37) reads as wooden planks.
            gate($"{tag}BASALT_RENDERS_DARK", rockStats.MedianLum <= BasaltMedianMax && rockHsv.S <= BasaltSatMax && reach.median <= BasaltLitMedianMax && reach.wall.S <= BasaltLitSatMax,
                $"key-lit wall facing the sun (emission off, worst case): median luminance {rockStats.MedianLum:F0} (<= {BasaltMedianMax}), mean {LookPixels.Rgb(rockHsv.Mean)} S {rockHsv.S:F2} (<= {BasaltSatMax}); wall beside the lava lights (lights on): median {reach.median:F0} (<= {BasaltLitMedianMax}), mean {LookPixels.Rgb(reach.wall.Mean)} S {reach.wall.S:F2} (<= {BasaltLitSatMax}); crater.jpg: charcoal (26,25,29), median 30..34 S .2..28, lava-lit (52,27,23) S .5..62");
            gate($"{tag}LAVA_LIGHTS_REACH_BASALT", pointLights >= 3 && res.LightShare >= .20f && reach.hot <= .10f, $"{pointLights} lava point lights, additional-lights limit {AdditionalLightsLimit()}: >= +8 luminance on {res.LightShare * 100:F1} % of the wall near a light (>= 20), +{res.LightGain:F1} on average; lava pixels brightened by >= 40 levels {reach.hot * 100:F1} % (<= 10, no hot pools)");

            if (sweep) { LavaSweep(r, lava, lavaMat, say, tag); LightSweep(r, wall, lava, lights, say, tag); BasaltTintSweep(r, basaltMat, glowWall, wallPxAll, wall, lava, lights, camAt, toward, say, tag); }
            if (System.Environment.GetEnvironmentVariable("GOLF_LOOK_TONE") == "1") ToneSweep(r, lava, lavaMat, basaltMat, glowWall, wallPxAll, camAt, toward, say, tag);
            return res;
        }

        /// GOLF_LOOK_TONE=1 (calibration round 2, review K): the lava ramp's darkest stop / heat bias (is there a believable dark crust inside the 85 % band budget?) and the
        /// basalt tint's G channel (the brightest lit slabs read green-grey: hue ~100, S .08). Clones of the shipped materials, nothing installed. INFO lines + jpgs.
        static void ToneSweep(Rig r, GameObject lava, Material lavaShipped, Material basaltShipped, GameObject glowWall, bool[] wallMask, Vector3 camAt, Vector3 toward, System.Action<string> say, string tag)
        {
            var rend = lava.GetComponent<Renderer>();
            string[] names = { "A_round1", "K1_d24_c210_b-.16", "K2_d24_c216_b-.16", "K3_d24_c210_b-.15_g1.5", "K4_d24_c216_b-.17", "K5_d60_c210_b-.16", "D_round2a" };
            var variants = new (Color deep, Color crust, float bias, float gain)[]
            {
                (Rgb255(150, 33, 10), Rgb255(186, 43, 13), -.15f, 1.4f),
                (Rgb255(24, 6, 3), Rgb255(210, 52, 16), -.16f, 1.4f),
                (Rgb255(24, 6, 3), Rgb255(216, 55, 17), -.16f, 1.4f),
                (Rgb255(24, 6, 3), Rgb255(210, 52, 16), -.15f, 1.5f),
                (Rgb255(24, 6, 3), Rgb255(216, 55, 17), -.17f, 1.4f),
                (Rgb255(60, 13, 5), Rgb255(210, 52, 16), -.16f, 1.4f),
                (Rgb255(60, 13, 5), Rgb255(186, 43, 13), -.20f, 1.4f),
            };
            for (int i = 0; i < variants.Length; i++)
            {
                var (deep, crust, bias, gain) = variants[i];
                var c = new Material(lavaShipped) { name = "tone " + names[i] };
                c.SetColor("_Deep", deep); c.SetColor("_Crust", crust); c.SetFloat("_HeatBias", bias); c.SetFloat("_HeatGain", gain);
                rend.sharedMaterial = c;
                r.Look(r.P(0, 8.96f, -6), r.P(0, 0, 40));
                var a = r.Shot($"tone_lava_{names[i]}"); var mask = LookPixels.Diff(a, r.Without(null, lava), 30, out _);
                var px = LookPixels.Masked(a, mask); var sp = Lava(px);
                int veryDark = 0, bright = 0, dim = 0; double sum = 0, sq = 0;
                foreach (var p in px) { Color.RGBToHSV(p, out _, out _, out var v); if (v < .40f) veryDark++; if (v < .55f) dim++; if (v >= .93f) bright++; float l = LookPixels.Lum(p); sum += l; sq += l * l; }
                double mean = sum / px.Count, sd = System.Math.Sqrt(System.Math.Max(0, sq / px.Count - mean * mean));
                r.Look(r.P(0, 40, 0), r.P(0, 0, 60));
                var b = r.Shot($"tone_lava_down_{names[i]}"); var maskB = LookPixels.Diff(b, r.Without(null, lava), 30, out _);
                var sd2 = Lava(LookPixels.Masked(b, maskB));
                say($"INFO: {tag}TONE lava {names[i]} deep {LookPixels.Rgb(deep)} crust {LookPixels.Rgb(crust)} bias {bias:F2} gain {gain:F1}: phone in-band {sp.InBand * 100:F1} % (dark {sp.Dark * 100:F1} %, V<.4 {veryDark * 100f / px.Count:F1} %, V<.55 {dim * 100f / px.Count:F1} %, V>=.93 {bright * 100f / px.Count:F1} %), G clipped {sp.ClipG * 100:F1} %, luminance sd {sd:F1}, mean {LookPixels.Rgb(sp.Mean)} | down in-band {sd2.InBand * 100:F1} % dark {sd2.Dark * 100:F1} %");
                rend.sharedMaterial = lavaShipped; Object.DestroyImmediate(c);
            }

            var gr = glowWall.GetComponentInChildren<Renderer>(); var shipped = gr.sharedMaterial;
            var tints = new[] { new Color(.52f, .80f, .92f), new Color(.52f, .76f, .92f), new Color(.52f, .74f, .90f), new Color(.54f, .74f, .90f), new Color(.50f, .72f, .88f), new Color(.52f, .70f, .90f), new Color(.56f, .76f, .90f) };
            foreach (var t in tints)
            {
                var c = new Material(basaltShipped) { name = "tone basalt" };
                c.SetColor("_BaseColor", t); c.SetVector("_EmissionColor", Vector4.zero);
                gr.sharedMaterial = c;
                r.Look(camAt + toward * 24, camAt, 50);
                var f = r.Shot($"tone_basalt_{t.r:F2}_{t.g:F2}_{t.b:F2}");
                var rock = new List<Color32>(LookPixels.Masked(f, wallMask)); var st = LookPixels.Analyse(rock, .02f); var hs = MeanHsv(rock);
                var top = rock.OrderByDescending(LookPixels.Lum).Take(Mathf.Max(1, rock.Count / 10)).ToList(); var th = MeanHsv(top);
                var dark = rock.OrderBy(LookPixels.Lum).Take(Mathf.Max(1, rock.Count / 2)).ToList(); var dh = MeanHsv(dark);
                say($"INFO: {tag}TONE basalt tint ({t.r:F2},{t.g:F2},{t.b:F2}): key-lit wall mean {LookPixels.Rgb(hs.Mean)} hue {hs.H:F0} S {hs.S:F2} median lum {st.MedianLum:F0}; darker half {LookPixels.Rgb(dh.Mean)} hue {dh.H:F0} S {dh.S:F2}; brightest 10 % {LookPixels.Rgb(th.Mean)} hue {th.H:F0} S {th.S:F2}");
                gr.sharedMaterial = shipped; Object.DestroyImmediate(c);
            }
        }

        static Color Rgb255(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);


        /// Lights on vs off: the basalt wall beside the first light (share of its pixels that gain >= 8 luminance, mean gain, and what the lit wall itself
        /// reads: median luminance, mean colour) and the lava pool seen from the rim (share of pool pixels that gain >= 40 levels = hot spots).
        static (float share, float gain, int wallPx, float hot, float median, Hsv wall) LightReach(Rig r, GameObject wall, GameObject lava, Light[] lights)
        {
            r.Look(r.P(-12, 8, 42), r.P(-40, 8, 42));
            var on = r.Shot("crater_wall_lights_on");
            var mask = LookPixels.Diff(on, r.Without(null, wall), 30, out int wallPx);
            foreach (var l in lights) l.enabled = false;
            var off = r.Shot("crater_wall_lights_off");
            foreach (var l in lights) l.enabled = true;
            int lit = 0; double added = 0;
            for (int i = 0; i < on.Length; i++)
                if (mask[i]) { float d = LookPixels.Lum(on[i]) - LookPixels.Lum(off[i]); added += d; if (d >= 8) lit++; }
            var wallList = LookPixels.Masked(on, mask);
            var wallHsv = MeanHsv(wallList);
            float median = wallList.Count > 0 ? LookPixels.Analyse(new List<Color32>(wallList), .02f).MedianLum : 0;
            r.Look(r.P(0, 8.96f, -6), r.P(0, 0, 40));
            var pon = r.Shot(null);
            var pmask = LookPixels.Diff(pon, r.Without(null, lava), 30, out int lp);
            foreach (var l in lights) l.enabled = false;
            var poff = r.Shot(null);
            foreach (var l in lights) l.enabled = true;
            int hot = 0;
            for (int i = 0; i < pon.Length; i++) if (pmask[i] && LookPixels.Lum(pon[i]) - LookPixels.Lum(poff[i]) >= 40) hot++;
            return (wallPx > 0 ? lit / (float)wallPx : 0, wallPx > 0 ? (float)(added / wallPx) : 0, wallPx, lp > 0 ? hot / (float)lp : 0, median, wallHsv);
        }

        /// GOLF_LOOK_SWEEP=1: the basalt tint x matte (no specular / environment reflection) x smoothness x the lava light intensity: the rock itself (key-lit
        /// wall, emission off) and the wall beside the lights. INFO only. Dim LINEAR values are large in sRGB: a smoothness .15 sky reflection alone puts a floor of ~30 levels under any tint.
        static void BasaltTintSweep(Rig r, Material basaltMat, GameObject glowWall, bool[] wallMask, GameObject wall, GameObject lava, Light[] lights, Vector3 camAt, Vector3 toward, System.Action<string> say, string tag)
        {
            var shipped = basaltMat.GetColor("_BaseColor"); var emission = basaltMat.GetVector("_EmissionColor");
            float s0 = basaltMat.GetFloat("_Smoothness"), er0 = basaltMat.GetFloat("_EnvironmentReflections"), sh0 = basaltMat.HasProperty("_SpecularHighlights") ? basaltMat.GetFloat("_SpecularHighlights") : 1;
            bool k1 = basaltMat.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"), k2 = basaltMat.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF");
            float i0 = lights.Length > 0 ? lights[0].intensity : 0;
            void Matte(bool on, float smooth)
            {
                basaltMat.SetFloat("_Smoothness", smooth);
                basaltMat.SetFloat("_EnvironmentReflections", on ? 0 : 1); basaltMat.SetFloat("_SpecularHighlights", on ? 0 : 1);
                if (on) { basaltMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); basaltMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); } else { basaltMat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); basaltMat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); }
            }
            try
            {
                foreach (var (t, matte, smooth, intensities) in new[] {
                    (new Vector3(.94f, .94f, .94f), false, .15f, new[] { 200f }),
                    (new Vector3(.94f, .94f, .94f), true, .15f, new[] { 200f, 400f }),
                    (new Vector3(.70f, .78f, .92f), true, .15f, new[] { 200f, 400f, 700f }),
                    (new Vector3(.55f, .66f, .84f), true, .15f, new[] { 200f, 400f, 700f }),
                    (new Vector3(.45f, .57f, .80f), true, .15f, new[] { 200f, 400f, 700f }),
                    (new Vector3(.60f, .70f, .90f), false, .05f, new[] { 200f, 400f }),
                    (new Vector3(.40f, .52f, .76f), true, .15f, new[] { 200f, 400f, 700f }) })
                {
                    Matte(matte, smooth); basaltMat.SetColor("_BaseColor", new Color(t.x, t.y, t.z, 1)); basaltMat.SetVector("_EmissionColor", Vector4.zero);
                    r.Look(camAt + toward * 24, camAt, 50);
                    var f = r.Shot(null); var rock = LookPixels.Masked(f, wallMask); var st = LookPixels.Analyse(new List<Color32>(rock), .02f); var hs = MeanHsv(rock);
                    basaltMat.SetVector("_EmissionColor", emission);
                    string row = $"INFO: {tag}SWEEP basalt tint ({t.x:F2},{t.y:F2},{t.z:F2}) matte {matte} smooth {smooth:F2}: key-lit wall emission off mean {LookPixels.Rgb(hs.Mean)} hue {hs.H:F0} S {hs.S:F2} median lum {st.MedianLum:F0}";
                    foreach (float I in intensities)
                    {
                        foreach (var l in lights) l.intensity = I;
                        var m = LightReach(r, wall, lava, lights);
                        row += $" | lights I {I:F0}: wall median {m.median:F0}, mean {LookPixels.Rgb(m.wall.Mean)} S {m.wall.S:F2}, >= +8 on {m.share * 100:F0} %, +{m.gain:F1} mean";
                    }
                    foreach (var l in lights) l.intensity = i0;
                    say(row);
                }
            }
            finally
            {
                basaltMat.SetColor("_BaseColor", shipped); basaltMat.SetVector("_EmissionColor", emission); basaltMat.SetFloat("_Smoothness", s0); basaltMat.SetFloat("_EnvironmentReflections", er0); basaltMat.SetFloat("_SpecularHighlights", sh0);
                if (k1) basaltMat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); else basaltMat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                if (k2) basaltMat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); else basaltMat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                foreach (var l in lights) l.intensity = i0;
            }
        }

        /// GOLF_LOOK_SWEEP=1: intensity / range / lift of the lava point lights. INFO only.
        static void LightSweep(Rig r, GameObject wall, GameObject lava, Light[] lights, System.Action<string> say, string tag)
        {
            if (lights.Length == 0) return;
            var basePos = lights.Select(l => l.transform.position - Vector3.up * GolfLook.LavaLightLift).ToArray();
            float i0 = lights[0].intensity, r0 = lights[0].range;
            foreach (var (I, R, lift) in new[] { (28f, 48f, 0f), (60f, 48f, 0f), (100f, 48f, 0f), (100f, 48f, 3f), (150f, 52f, 3f), (200f, 52f, 4f), (300f, 56f, 4f), (400f, 60f, 5f) })
            {
                for (int k = 0; k < lights.Length; k++) { lights[k].intensity = I; lights[k].range = R; lights[k].transform.position = basePos[k] + Vector3.up * lift; }
                var m = LightReach(r, wall, lava, lights);
                say($"INFO: {tag}SWEEP lava lights I {I} range {R} lift {lift} (limit {AdditionalLightsLimit()}): wall >= +8 on {m.share * 100:F1} % (+{m.gain:F1} mean), lava hot {m.hot * 100:F1} %");
            }
            for (int k = 0; k < lights.Length; k++) { lights[k].intensity = i0; lights[k].range = r0; lights[k].transform.position = basePos[k] + Vector3.up * GolfLook.LavaLightLift; }
        }


        static Texture2D LoadPng(string path, bool linear)
        {
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, true, linear) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear };
            t.LoadImage(File.ReadAllBytes(path), false);
            t.Apply(true);
            return t;
        }

        /// A copy of the lava material with candidate textures from a folder (Lava_C.png / Lava_N.png / Lava_E.png): for tuning only.
        static Material Candidate(Material shipped, string dir)
        {
            var m = new Material(shipped) { name = "candidate lava (not an LK_ name: DressModel would swap it)" };
            if (File.Exists(Path.Combine(dir, "Lava_C.png"))) m.SetTexture("_BaseMap", LoadPng(Path.Combine(dir, "Lava_C.png"), false));
            if (File.Exists(Path.Combine(dir, "Lava_N.png"))) m.SetTexture("_BumpMap", LoadPng(Path.Combine(dir, "Lava_N.png"), true));
            if (File.Exists(Path.Combine(dir, "Lava_E.png"))) m.SetTexture("_EmissionMap", LoadPng(Path.Combine(dir, "Lava_E.png"), false));
            return m;
        }

        static int AdditionalLightsLimit()
        {
            var p = (QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline) as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            return p ? p.maxAdditionalLightsCount : -1;
        }

        static void Say(System.Action<string> say, string s) => say(s);

        /// Tuning sweep (GOLF_LOOK_SWEEP=1): the lava heat mapping (albedo weight, glow weight, bias, gain, relief) on the lava sheet, phone and down views. INFO only.
        static void LavaSweep(Rig r, GameObject lava, Material shipped, System.Action<string> say, string tag)
        {
            var rend = lava.GetComponent<Renderer>();
            foreach (var (ha, hg, hb, gain, rl) in new[] { (1.5f, 1.5f, -.1f, 1.5f, .15f), (1.5f, 1.5f, -.15f, 1.5f, .15f), (1.5f, 1.5f, -.2f, 1.3f, .15f), (1.5f, 1.5f, -.25f, 1.1f, .15f), (1.5f, 1.5f, -.3f, 1.0f, .15f), (1.5f, 1.5f, -.2f, 1.3f, .35f), (1.8f, 1.2f, -.3f, 1.2f, .25f) })
            {
                var c = new Material(shipped) { name = "lava sweep" };
                c.SetFloat("_HeatAlbedo", ha); c.SetFloat("_HeatGlow", hg); c.SetFloat("_HeatBias", hb); c.SetFloat("_HeatGain", gain); c.SetFloat("_Relief", rl);
                rend.sharedMaterial = c;
                r.Look(r.P(0, 8.96f, -6), r.P(0, 0, 40));
                var a = r.Shot($"sweep_lava_{ha:F1}_{hg:F1}_{hb:F1}_{gain:F1}_{rl:F2}"); var mask = LookPixels.Diff(a, r.Without(null, lava), 30, out _);
                var sp = Lava(LookPixels.Masked(a, mask));
                r.Look(r.P(0, 40, 0), r.P(0, 0, 60));
                var b = r.Shot(null); var maskB = LookPixels.Diff(b, r.Without(null, lava), 30, out _);
                var sd = Lava(LookPixels.Masked(b, maskB));
                say($"INFO: {tag}SWEEP lava heat albedo {ha:F1} glow {hg:F1} bias {hb:F2} gain {gain:F1} relief {rl:F2}: phone in-band {sp.InBand * 100:F0} % dark {sp.Dark * 100:F0} % yellow {sp.Yellow * 100:F0} % red {sp.Red * 100:F0} % mean {LookPixels.Rgb(sp.Mean)} | down in-band {sd.InBand * 100:F0} % dark {sd.Dark * 100:F0} % yellow {sd.Yellow * 100:F0} % red {sd.Red * 100:F0} % mean {LookPixels.Rgb(sd.Mean)}");
                rend.sharedMaterial = shipped; Object.DestroyImmediate(c);
            }
        }

        // ------------------------------------------------------------------ holes 8, 9: the water shelf next to foam, the open sea, the sky

        /// The sea board: ocean, the shelf strip, the far island (top + cliff). NO foam / surf strip: the white foam strips are deleted from the holes (user line 6).
        /// `shelfYd` = the shelf's width seaward of the shore: 6 yd is the worst-case pocket of the ground library's shelf, 1.5 yd its typical edge (median 1.3 m, p95 2.5 m).
        static void BuildSea(Rig r, float shore, bool nearLand, out GameObject ocean, out GameObject shelf, out GameObject land, float shelfYd = 6)
        {
            ocean = Grid(r, "WATER_OCEAN board", GolfLook.Get("LK_WATER"), r.P(-1200, 0, -400), r.Right * 2400, r.Aim * 2000, 48, 40, 8);
            shelf = Grid(r, "WATER_SHELF board", GolfLook.Get("LK_WATER_SHALLOW"), r.P(-150, .05f, shore - shelfYd), r.Right * 300, r.Aim * shelfYd, 24, 2, 8);
            land = new GameObject("TERRAIN board"); land.transform.SetParent(r.Root.transform, false);
            // the far island: top at play height and a cliff wall down to the sea at the shore line
            Grid(r, "TERRAIN_top", GolfLook.GetForHole("LK_ROUGH", r.Hole), r.P(-300, 6.56f, shore), r.Right * 600, r.Aim * 220, 12, 4, 12).transform.SetParent(land.transform, true);
            LookPixels.Quad(land, "cliff", GolfLook.Get("LK_CLIFF"), 12, r.P(300, 0, shore), r.P(-300, 0, shore), r.P(-300, 6.56f, shore), r.P(300, 6.56f, shore), -r.Aim);
            if (nearLand) Grid(r, "FAIRWAY_near", GolfLook.GetForHole("LK_FAIRWAY", r.Hole), r.P(-40, 6.56f, -14), r.Right * 80, r.Aim * 44, 8, 4, 10).transform.SetParent(land.transform, true);
        }

        public struct ShelfResult { public Hsv Shelf, Ocean, AerialOcean, ThinShelf, ThinOcean, OceanNear, OceanFar; public float SkyHorizon, ShelfLum, OceanLum, TopShare, ThinTopShare, ThinLum; public int ThinN; public bool Ran; }

        /// The stills' own water: lit mean luminance of the teal pixels (hue 170..200, S > .45) of needle.jpg (27,73,86) = 66 and split.jpg (19,76,99) = 64 (median),
        /// the deep blue (38,72,96) lum 62 / (47,96,131) lum 90. The shelf must sit near those, not above them (calibration, v2 2026-10-04).
        public static readonly Dictionary<int, float> RefTealLum = new() { [8] = 66, [9] = 64 };
        public const float ShelfVsRefTeal = 1.30f, ThinShelfYd = 1.5f;

        /// The sea: the shelf strip and the open ocean, under the hole's real light, from an aerial camera (the shelf 40 yd away, ~100 px wide) and from
        /// the phone camera (ocean + horizon). There is NO foam strip on the board any more (deleted from the holes). Two shelf widths: 6 yd = the worst-case pocket
        /// of the ground library's shelf (gate SHELF_COLOUR_NOT_LOUDEST: mean hue 175..200, V <= .65, luminance <= 100 and <= 1.3 x the open sea beside it, below the
        /// sky horizon, never among the brightest 2 % of the aerial frame) and 1.5 yd = its typical edge (gate SHELF_NOT_LOUDEST: luminance <= 1.3 x the stills' own teal
        /// (needle 66 / split 64) and <= 1.2 x the open sea beside it, never among the brightest 2 % of the aerial frame: the thin edge is not a halo).
        /// The open ocean must read like the stills' deep blue near the tee AND far away (gates OCEAN_DEEP_BLUE, SEA_NEAR_FAR_BLUE).
        public static ShelfResult Shelf(int hole, System.Action<string, bool, string> gate, System.Action<string> say, string tag, bool sweep)
        {
            var res = new ShelfResult { Ran = true };
            using var r = new Rig(hole);
            BuildSea(r, 70, false, out var ocean, out var shelf, out var land);
            r.Look(r.P(0, 45, 0), r.P(0, 0, 45));
            var full = r.Shot($"sea_hole{hole:00}_aerial");
            var shelfMask = r.SoloMask(full, shelf);
            var oceanMaskA = r.SoloMask(full, ocean);
            var shelfPx = LookPixels.Masked(full, shelfMask);
            res.Shelf = MeanHsv(shelfPx); res.ShelfLum = res.Shelf.Lum;
            res.AerialOcean = MeanHsv(LookPixels.Masked(full, oceanMaskA));
            float shelfV95 = shelfPx.Count > 0 ? shelfPx.Select(p => { Color.RGBToHSV(p, out _, out _, out var vv); return vv; }).OrderBy(x => x).ElementAt((int)(shelfPx.Count * .95f)) : 0;
            // is the shelf the brightest shape? the share of the frame's brightest 2 % of pixels that are shelf pixels
            float TopShareOf(Color32[] frame, bool[] mask)
            {
                var lums = frame.Select(LookPixels.Lum).OrderBy(x => x).ToArray(); float cut = lums[(int)(lums.Length * .98f)];
                int top = 0, topShelf = 0; for (int i = 0; i < frame.Length; i++) if (LookPixels.Lum(frame[i]) >= cut) { top++; if (mask[i]) topShelf++; }
                return top > 0 ? topShelf / (float)top : 0;
            }
            res.TopShare = TopShareOf(full, shelfMask);

            // the typical shelf edge (1.5 yd), same aerial camera
            Object.DestroyImmediate(ocean); Object.DestroyImmediate(shelf); Object.DestroyImmediate(land);
            BuildSea(r, 70, false, out ocean, out shelf, out land, ThinShelfYd);
            r.Look(r.P(0, 45, 0), r.P(0, 0, 45));
            var thin = r.Shot($"sea_hole{hole:00}_aerial_thin");
            var thinMask = r.SoloMask(thin, shelf);
            var thinPx = LookPixels.Masked(thin, thinMask);
            res.ThinShelf = MeanHsv(thinPx); res.ThinLum = res.ThinShelf.Lum; res.ThinN = thinPx.Count;
            res.ThinOcean = MeanHsv(LookPixels.Masked(thin, r.SoloMask(thin, ocean)));
            res.ThinTopShare = TopShareOf(thin, thinMask);
            // the same edge from the phone camera at the tee: where the island base meets the sea, 70 yd ahead (a thin line, must read as an edge)
            r.Look(r.P(0, 8.96f, -6), r.P(0, 0, 70));
            r.Shot($"sea_hole{hole:00}_phone_edge");

            // phone view: ocean + sky horizon
            r.Phone(r.P(0, 6.56f, 0), r.Aim);
            Object.DestroyImmediate(land); land = new GameObject("TERRAIN board"); land.transform.SetParent(r.Root.transform, false);
            Grid(r, "FAIRWAY_near", GolfLook.GetForHole("LK_FAIRWAY", hole), r.P(-40, 6.56f, -14), r.Right * 80, r.Aim * 44, 8, 4, 10).transform.SetParent(land.transform, true);
            var ph = r.Shot($"sea_hole{hole:00}_phone");
            var oceanMask = r.SoloMask(ph, ocean);
            res.Ocean = MeanHsv(LookPixels.Masked(ph, oceanMask)); res.OceanLum = res.Ocean.Lum;
            // near the tee (the lowest third of the ocean's rows) and far (the highest third, toward the horizon)
            int yMin = r.H, yMax = -1;
            for (int y = 0; y < r.H; y++) for (int x = 0; x < r.W; x += 4) if (oceanMask[y * r.W + x]) { if (y < yMin) yMin = y; if (y > yMax) yMax = y; }
            var nearPx = new List<Color32>(); var farPx = new List<Color32>();
            if (yMax > yMin)
            {
                int third = (yMax - yMin + 1) / 3;
                for (int y = yMin; y <= yMax; y++) for (int x = 0; x < r.W; x++)
                    if (oceanMask[y * r.W + x]) { if (y < yMin + third) nearPx.Add(ph[y * r.W + x]); else if (y > yMax - third) farPx.Add(ph[y * r.W + x]); }
            }
            res.OceanNear = MeanHsv(nearPx); res.OceanFar = MeanHsv(farPx);
            // the sky: no ocean / shelf / land; the band just above the geometric horizon
            var sky = r.Without(null, ocean, shelf, land);
            var hp = r.Cam.WorldToViewportPoint(r.Cam.transform.position + Vector3.Scale(r.Cam.transform.forward, new Vector3(1, 0, 1)).normalized * 2000);
            int hrow = Mathf.Clamp(Mathf.RoundToInt(hp.y * r.H), 40, r.H - 40);
            res.SkyHorizon = RowLum(sky, r.W, hrow + 3, hrow + 30)[0];
            string Hs(Hsv h) => $"{LookPixels.Rgb(h.Mean)} hue {h.H:F0} S {h.S:F2} V {h.V:F2} lum {h.Lum:F0}";
            Say(say, $"INFO: {tag}SEA hole {hole} ({PipelineName()}): shelf 6 yd (aerial) {res.Shelf.N} px mean {Hs(res.Shelf)} (p95 V {shelfV95:F2}); open ocean beside it (aerial) {res.AerialOcean.N} px mean {LookPixels.Rgb(res.AerialOcean.Mean)} lum {res.AerialOcean.Lum:F0}; shelf share of the brightest 2 % of the aerial frame {res.TopShare * 100:F1} %; open ocean (phone) {res.Ocean.N} px mean {Hs(res.Ocean)}; sky just above the horizon (row {hrow}) lum {res.SkyHorizon:F0}");
            Say(say, $"INFO: {tag}SEA hole {hole} thin shelf {ThinShelfYd} yd (aerial): {res.ThinN} px mean {Hs(res.ThinShelf)}; open ocean beside it lum {res.ThinOcean.Lum:F0}; thin shelf share of the brightest 2 % {res.ThinTopShare * 100:F1} %; stills' teal lum {RefTealLum[hole]:F0} (needle.jpg 66, split.jpg 64); open ocean (phone) NEAR the tee {res.OceanNear.N} px {Hs(res.OceanNear)}, FAR {res.OceanFar.N} px {Hs(res.OceanFar)}; stills' blue (needle.jpg (38,72,96) lum 62, split.jpg (47,96,131) lum 90)");
            gate($"{tag}SHELF_COLOUR_NOT_LOUDEST hole{hole}", res.Shelf.N >= 500 && res.AerialOcean.N >= 1000 && res.Shelf.H >= ShelfHueMin && res.Shelf.H <= ShelfHueMax && res.Shelf.V <= ShelfValMax
                    && res.ShelfLum <= ShelfLumMax && res.ShelfLum <= ShelfVsOcean * res.AerialOcean.Lum && res.TopShare <= ShelfTopShareMax && res.ShelfLum < res.SkyHorizon,
                $"6 yd worst-case shelf mean {LookPixels.Rgb(res.Shelf.Mean)} hue {res.Shelf.H:F0} ({ShelfHueMin}..{ShelfHueMax}), V {res.Shelf.V:F2} (<= {ShelfValMax}), luminance {res.ShelfLum:F0} (<= {ShelfLumMax}; the stills' teal ~65) and <= {ShelfVsOcean} x the open sea beside it {res.AerialOcean.Lum:F0} = {ShelfVsOcean * res.AerialOcean.Lum:F0}, shelf is {res.TopShare * 100:F1} % of the aerial frame's brightest 2 % (<= {ShelfTopShareMax * 100:F0}), below the sky horizon {res.SkyHorizon:F0}; {res.Shelf.N} shelf px; no foam on the board");
            float refTeal = RefTealLum[hole];
            gate($"{tag}SHELF_NOT_LOUDEST hole{hole}", res.ThinN >= 300 && res.ThinShelf.H >= ShelfHueMin && res.ThinShelf.H <= ShelfHueMax && res.ThinLum <= ShelfVsRefTeal * refTeal && res.ThinLum <= 1.2f * res.ThinOcean.Lum && res.ThinTopShare <= ShelfTopShareMax && res.ThinLum < res.SkyHorizon,
                $"typical {ThinShelfYd} yd shelf edge: {res.ThinN} px mean {Hs(res.ThinShelf)} (hue {ShelfHueMin}..{ShelfHueMax}), luminance {res.ThinLum:F0} <= {ShelfVsRefTeal} x the stills' teal {refTeal:F0} = {ShelfVsRefTeal * refTeal:F0} and <= 1.2 x the open sea beside it {res.ThinOcean.Lum:F0} = {1.2f * res.ThinOcean.Lum:F0}, {res.ThinTopShare * 100:F1} % of the aerial frame's brightest 2 % (<= {ShelfTopShareMax * 100:F0}), below the sky horizon {res.SkyHorizon:F0}");
            // the open ocean against the stills (needle.jpg deep (29,77,112) h205 V.44, (39,79,109); split.jpg (48,102,140) h202 V.55, far (80,137,182) h206 V.71)
            gate($"{tag}OCEAN_DEEP_BLUE hole{hole}", res.Ocean.N >= 20000 && res.Ocean.H >= 195 && res.Ocean.H <= 215 && res.Ocean.V >= .35f && res.Ocean.V <= .78f && res.Ocean.S >= .45f,
                $"open ocean mean {LookPixels.Rgb(res.Ocean.Mean)} hue {res.Ocean.H:F0} (195..215), S {res.Ocean.S:F2} (>= .45), V {res.Ocean.V:F2} (.35..78); stills deep blue h202..206 V .44..71");
            bool nf(Hsv h) => h.N >= 2000 && h.H >= 195 && h.H <= 215 && h.S >= .40f && h.V >= .35f && h.V <= .78f;
            gate($"{tag}SEA_NEAR_FAR_BLUE hole{hole}", nf(res.OceanNear) && nf(res.OceanFar),
                $"open ocean near the tee {Hs(res.OceanNear)} and far toward the horizon {Hs(res.OceanFar)}: both hue 195..215, S >= .40, V .35..78 (stills: needle.jpg deep (29,77,112) h205 V.44, split.jpg (48,102,140) h202 V.55 .. far (80,137,182) h206 V.71)");
            if (sweep) { ShelfSweep(r, hole, shelf, say, tag); OceanSweep(r, hole, ocean, say, tag); }
            return res;
        }


        /// GOLF_LOOK_SWEEP=1: colours of the open ocean in the phone view. INFO only.
        static void OceanSweep(Rig r, int hole, GameObject ocean, System.Action<string> say, string tag)
        {
            var rend = ocean.GetComponent<Renderer>(); var shipped = rend.sharedMaterial;
            r.Phone(r.P(0, 6.56f, 0), r.Aim);
            var land = new GameObject("TERRAIN sweep"); land.transform.SetParent(r.Root.transform, false);
            Grid(r, "FAIRWAY_near", GolfLook.GetForHole("LK_FAIRWAY", hole), r.P(-40, 6.56f, -14), r.Right * 80, r.Aim * 44, 8, 4, 10).transform.SetParent(land.transform, true);
            foreach (var (sh, de, sk) in new[] { ("#0E5088", "#08346C", "#2064A0"), ("#0E5490", "#08386C", "#1C6AA0"), ("#0E5894", "#083A70", "#1A70A4"), ("#0C5690", "#063668", "#1C6C9C"), ("#105C94", "#0A3C70", "#1C74A4"), ("#0E5C98", "#083C74", "#1870A8") })
            {
                var m = new Material(shipped) { name = "ocean sweep" };
                ColorUtility.TryParseHtmlString(sh, out var cs); ColorUtility.TryParseHtmlString(de, out var cd); ColorUtility.TryParseHtmlString(sk, out var ck);
                m.SetColor("_Shallow", cs); m.SetColor("_Deep", cd); m.SetColor("_Sky", ck);
                rend.sharedMaterial = m;
                var f = r.Shot(null); var s = MeanHsv(LookPixels.Masked(f, r.SoloMask(f, ocean)));
                say($"INFO: {tag}SWEEP ocean hole {hole} shallow {sh} deep {de} sky {sk}: mean {LookPixels.Rgb(s.Mean)} hue {s.H:F0} S {s.S:F2} V {s.V:F2} lum {s.Lum:F0}");
                rend.sharedMaterial = shipped; Object.DestroyImmediate(m);
            }
            Object.DestroyImmediate(land);
        }

        static void ShelfSweep(Rig r, int hole, GameObject shelf, System.Action<string> say, string tag)
        {
            var rend = shelf.GetComponent<Renderer>(); var shipped = rend.sharedMaterial;
            r.Look(r.P(0, 45, 0), r.P(0, 0, 45));
            foreach (var (sh, de) in new[] { ("#1AC4C4", "#1096B2"), ("#1E8E9A", "#1A7E96"), ("#1C7C86", "#187488"), ("#187882", "#146C80"), ("#206E7A", "#1A6478"), ("#1B6A78", "#165C74") })
            {
                var m = new Material(shipped) { name = "shelf sweep" };
                ColorUtility.TryParseHtmlString(sh, out var cs); ColorUtility.TryParseHtmlString(de, out var cd);
                m.SetColor("_Shallow", cs); m.SetColor("_Deep", cd);
                rend.sharedMaterial = m;
                var f = r.Shot(null); var px = LookPixels.Masked(f, r.SoloMask(f, shelf)); var s = MeanHsv(px);
                say($"INFO: {tag}SWEEP shelf hole {hole} shallow {sh} deep {de}: mean {LookPixels.Rgb(s.Mean)} hue {s.H:F0} S {s.S:F2} V {s.V:F2} lum {s.Lum:F0}");
                rend.sharedMaterial = shipped; Object.DestroyImmediate(m);
            }
        }

        // ------------------------------------------------------------------ grass hue under the hole's key light (not in the PNG)

        public struct GrassResult { public Hsv Fairway, Green, Rough, Scrub; public GrassClip FairwayClip, GreenClip, RoughClip, ScrubClip; }

        /// Share of pixels with a clipped channel (any of R, G, B at 250 or more; "255" = at the very top) over the lower 60 % of the phone frame (the ground).
        public struct GrassClip { public int N; public float Clip250, Clip255; }

        // ---- GRASS_HUE_GAMEVIEW / GRASS_NO_CLIP thresholds (the brief: the stills' greens sit at hue 64..73; rough 58..72; Needle / Split S .55..66 V .55..75; Crater S about .8; at most 3 % clipped)
        public const float GrassFairHueMin = 64, GrassFairHueMax = 73, GrassRoughHueMin = 58, GrassRoughHueMax = 72, GrassClipMax = .03f;

        /// (S min, S max, V min, V max) of a material's lit mean in the phone frame. Fairway / green: the stills' 16 x 16 samples (needle.jpg (172,186,66) S.65 V.73,
        /// split.jpg (133,152,52) S.66 V.60 .. (154,173,75) S.57 V.68, crater.jpg (136,161,26) S.84 V.63 / (156,173,28) S.84 V.68). Rough: the stills' rough / scrub run darker
        /// (needle.jpg (135,134,57) S.58 V.53, split.jpg scrub (122,111,75) S.39 V.48, crater pillar rim V .35), so its V floor is lower.
        public static (float s0, float s1, float v0, float v1) GrassBand(int hole, string mat)
        {
            bool crater = hole == 10;
            return mat switch
            {
                "LK_FAIRWAY" or "LK_GREEN" => crater ? (.72f, .88f, .55f, .75f) : (.55f, .66f, .55f, .75f),
                "LK_ROUGH" => crater ? (.65f, .88f, .40f, .75f) : (.50f, .70f, .45f, .75f),
                _ => (.30f, .50f, .40f, .60f),            // LK_SCRUB (Split's ridge)
            };
        }

        static GrassClip ClipOf(Color32[] px, int w, int h)
        {
            var c = new GrassClip();
            int rows = Mathf.RoundToInt(h * .6f), c250 = 0, c255 = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = px[y * w + x]; int mx = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                    if (mx >= 250) c250++; if (mx >= 255) c255++;
                }
            c.N = rows * w; c.Clip250 = c250 / (float)c.N; c.Clip255 = c255 / (float)c.N;
            return c;
        }

        /// The central lower third of the phone frame filled by one ground material at a time (real lights, HoleTint, no post): mean RGB / hue / S / V, and the
        /// clipped-pixel share over the lower 60 % of the frame. Gates GRASS_HUE_GAMEVIEW (hue + S + V bands) and GRASS_NO_CLIP (<= 3 % clipped, per material).
        public static GrassResult Grass(int hole, System.Action<string, bool, string> gate, System.Action<string> say, string tag)
        {
            var res = new GrassResult();
            using var r = new Rig(hole);
            (Hsv, GrassClip) One(string mat, float tile)
            {
                var g = new GameObject("ground " + mat); g.transform.SetParent(r.Root.transform, false);
                LookPixels.Quad(g, mat, GolfLook.GetForHole(mat, hole), tile, r.P(-80, 6.56f, -20), r.P(-80, 6.56f, 120), r.P(80, 6.56f, 120), r.P(80, 6.56f, -20), Vector3.up);
                r.Phone(r.P(0, 6.56f, 0), r.Aim);
                var px = r.Shot($"grass_hole{hole:00}_{mat}");
                var third = new List<Color32>();
                for (int y = 0; y < r.H / 3; y++) for (int x = r.W / 3; x < 2 * r.W / 3; x++) third.Add(px[y * r.W + x]);
                var clip = ClipOf(px, r.W, r.H);
                Object.DestroyImmediate(g);
                return (MeanHsv(third), clip);
            }
            (res.Fairway, res.FairwayClip) = One("LK_FAIRWAY", 10); (res.Green, res.GreenClip) = One("LK_GREEN", 6); (res.Rough, res.RoughClip) = One("LK_ROUGH", 12);
            string L(string n, Hsv h) => $"{n} {LookPixels.Rgb(h.Mean)} h{h.H:F0} S{h.S:F2} V{h.V:F2}";
            string C(GrassClip c) => $"clipped {c.Clip250 * 100:F1} % (>=250) / {c.Clip255 * 100:F1} % (255)";
            if (hole == 9)
            {
                (res.Scrub, res.ScrubClip) = One("LK_SCRUB", 12);
                Say(say, $"INFO: {tag}GRASS hole 9 lit: {L("scrub (Split ridge; split.jpg ridge scrub (122,111,75) h46 S.39 V.48, dark scrub (63,59,41))", res.Scrub)}; {C(res.ScrubClip)}");
            }
            Say(say, $"INFO: {tag}GRASS hole {hole} lit ({PipelineName()}): {L("fairway", res.Fairway)} {C(res.FairwayClip)}; {L("green", res.Green)} {C(res.GreenClip)}; {L("rough", res.Rough)} {C(res.RoughClip)}");
            bool Sv(Hsv h, string m) { var b = GrassBand(hole, m); return h.S >= b.s0 && h.S <= b.s1 && h.V >= b.v0 && h.V <= b.v1; }
            string Bd(string m) { var b = GrassBand(hole, m); return $"S {b.s0:F2}..{b.s1:F2} V {b.v0:F2}..{b.v1:F2}"; }
            bool hueOk = Between(res.Fairway.H, GrassFairHueMin, GrassFairHueMax) && Between(res.Green.H, GrassFairHueMin, GrassFairHueMax) && Between(res.Rough.H, GrassRoughHueMin, GrassRoughHueMax);
            bool svOk = Sv(res.Fairway, "LK_FAIRWAY") && Sv(res.Green, "LK_GREEN") && Sv(res.Rough, "LK_ROUGH");
            bool scrubOk = true; string scrubText = "";
            if (hole == 9) { scrubOk = Between(res.Scrub.H, 40, 58) && Sv(res.Scrub, "LK_SCRUB"); scrubText = $"; scrub {LookPixels.Rgb(res.Scrub.Mean)} h{res.Scrub.H:F0} S{res.Scrub.S:F2} V{res.Scrub.V:F2} (hue 40..58, {Bd("LK_SCRUB")})"; }
            gate($"{tag}GRASS_HUE_GAMEVIEW hole{hole}", hueOk && svOk && scrubOk,
                $"{L("fairway", res.Fairway)} (hue {GrassFairHueMin}..{GrassFairHueMax}, {Bd("LK_FAIRWAY")}), {L("green", res.Green)} (hue {GrassFairHueMin}..{GrassFairHueMax}, {Bd("LK_GREEN")}), {L("rough", res.Rough)} (hue {GrassRoughHueMin}..{GrassRoughHueMax}, {Bd("LK_ROUGH")}){scrubText}; hue {hueOk}, S/V {svOk}; the old build measured hue 86..89");
            bool clipOk = res.FairwayClip.Clip250 <= GrassClipMax && res.GreenClip.Clip250 <= GrassClipMax && res.RoughClip.Clip250 <= GrassClipMax && (hole != 9 || res.ScrubClip.Clip250 <= GrassClipMax);
            gate($"{tag}GRASS_NO_CLIP hole{hole}", clipOk,
                $"pixels with a channel >= 250 over the lower 60 % of the phone frame (<= {GrassClipMax * 100:F0} %): fairway {C(res.FairwayClip)}, green {C(res.GreenClip)}, rough {C(res.RoughClip)}{(hole == 9 ? ", scrub " + C(res.ScrubClip) : "")}; the stills are light, saturated and not clipped");
            // the other bright ground-level surfaces under the same key (sand, path, masonry, rock; lit from above = the brightest case): the stills' sand / stone are light but not clipped either
            var others = new List<(string, float)> { ("LK_SAND", 6), ("LK_ROCK", 4) };
            if (hole == 8) others.Add(("LK_PATH", 5));
            if (hole != 10) others.Add(("LK_MASONRY", 4));
            var oneLine = new List<string>(); bool othersOk = true;
            foreach (var (mat, tile) in others)
            {
                var (hs, cl) = One(mat, tile);
                oneLine.Add($"{mat.Substring(3).ToLowerInvariant()} {LookPixels.Rgb(hs.Mean)} h{hs.H:F0} S{hs.S:F2} V{hs.V:F2} {C(cl)}");
                othersOk &= cl.Clip250 <= GrassClipMax;
            }
            gate($"{tag}SURFACES_NO_CLIP hole{hole}", othersOk, $"sand / rock / path / masonry as flat up-facing quads under the key (pixels with a channel >= 250 <= {GrassClipMax * 100:F0} %): {string.Join("; ", oneLine.ToArray())}");
            return res;
        }

        static bool Between(float v, float a, float b) => v >= a && v <= b;

        // ------------------------------------------------------------------ smoke (hole 10 sky)

        /// SMOKE_SOFT_INTERSECTION (v2 repair round 3, review medium, hole 10 tee: "straight quad-card edges running diagonally down the cone flank"): a smoke card that passes THROUGH opaque geometry is cut by the depth
        /// test along the intersection line, a hard edge in the middle of a plume. The board: a 80 x 70 yd opaque wall at 130 yd facing the camera, a 80 yd card standing at 40 degrees to it so its left half is in front of
        /// the wall and its right half behind it (hidden). The intersection is a vertical line through the card. Measured: the largest step of the card's own signature (|dLum| with the card minus without it) between
        /// neighbouring pixels along the row, p99 over the card's boundary band, WITH the table's `_SoftFade` and with `_SoftFade` 0 (the control: the test must see the hard edge it exists to catch).
        /// PASS: soft edge step p99 <= 6 levels/px, the control's >= 2x that and > 6, and the soft card still moves >= 3000 px (it faded, it did not vanish).
        static void SmokeIntersection(System.Action<string, bool, string> gate, System.Action<string> say, string tag)
        {
            var soft = GolfLook.Get("LK_SMOKE");
            var hard = new Material(soft) { name = "control smoke (_SoftFade 0, not an LK_ name)" }; hard.SetFloat("_SoftFade", 0);
            var wallMat = GolfLook.Get("LK_BASALT");
            float[] Edge(Material m, string shot, out int moved)
            {
                using var r = new Rig(10);
                float camY = 9f;
                var toCam = -r.Aim;
                // wall: x -40..40, y 0..70 at z = 130 yd, outward = toward the camera
                LookPixels.Quad(r.Root, "DRESS_WALL board", wallMat, 8, r.P(-40, 0, 130), r.P(40, 0, 130), r.P(40, 70, 130), r.P(-40, 70, 130), toCam);
                float a = 40 * Mathf.Deg2Rad; var across = r.Right * Mathf.Cos(a) + r.Aim * Mathf.Sin(a);
                var card = Card(r, "DRESS_SMOKE board intersect", m, r.P(-6, 4, 130), across, 80, 60);
                r.Look(r.P(0, camY, 0), r.P(-6, 30, 130));
                var with = r.Shot(shot);
                var without = r.Without(shot + "_without", card);
                var delta = new float[with.Length];
                int n = 0;
                for (int i = 0; i < with.Length; i++) { delta[i] = Mathf.Abs(LookPixels.Lum(with[i]) - LookPixels.Lum(without[i])); if (delta[i] >= 1.5f) n++; }
                moved = n;
                var grads = new List<float>();
                for (int y = 1; y < r.H - 1; y++)
                    for (int x = 1; x < r.W - 1; x++)
                    {
                        int i = y * r.W + x;
                        if (delta[i] < 1.5f && delta[i - 1] < 1.5f && delta[i + 1] < 1.5f && delta[i - r.W] < 1.5f) continue;
                        grads.Add(Mathf.Max(Mathf.Abs(delta[i + 1] - delta[i - 1]), Mathf.Abs(delta[i + r.W] - delta[i - r.W])) / 2f);
                    }
                grads.Sort();
                return new[] { grads.Count > 0 ? grads[(int)(grads.Count * .99f)] : 0f, grads.Count > 0 ? grads[grads.Count - 1] : 0f };
            }
            var es = Edge(soft, "smoke_intersect_soft", out int movedSoft);
            var eh = Edge(hard, "smoke_intersect_control", out int movedHard);
            Object.DestroyImmediate(hard);
            bool ok = es[0] <= 6f && eh[0] > 6f && eh[0] >= 2f * es[0] && movedSoft >= 3000;
            gate($"{tag}SMOKE_SOFT_INTERSECTION", ok, $"a card cutting through an opaque wall at 40 deg: edge step p99 with the table's _SoftFade ({soft.GetFloat("_SoftFade"):F0} yd) {es[0]:F2} levels/px (<= 6; max {es[1]:F1}), control with _SoftFade 0 {eh[0]:F2} (> 6 and >= 2x the soft one: the test sees the hard edge), the soft card still moves {movedSoft} px (>= 3000; control {movedHard})");
        }

        public struct SmokeResult { public float AlphaMax, AlphaP95, EdgeAlpha, DeltaP99, DeltaMedian, GradP99; public int N; }

        static Color32[] ReadSource(string file)
        {
            string path = Path.Combine(Application.dataPath, "Resources/Course/Look", file + ".png");
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(path));
            var px = t.GetPixels32(); int w = t.width; Object.DestroyImmediate(t);
            return px;
        }

        /// LK_SMOKE: the effective alpha the shader draws (pow(texture A x _Color.A, _AlphaPower)) from the source PNG, and a rendered card over the
        /// Crater sky: how far it moves the frame (peak, median) and how hard its edges are. A soft translucent plume, never an opaque grey ball.
        public static SmokeResult Smoke(System.Action<string, bool, string> gate, System.Action<string> say, string tag, bool sweep = false)
        {
            var near = SmokeAt(gate, say, tag, sweep, 130f, "");
            // v2 repair round 1: the real plumes stand 400+ yd from the tee (hole 10: 390 m = 430 yd), where Crater's fog (120..1000 yd, dark red-brown) takes ~35 % of a surface's colour. The board
            // card at 130 yd (1 % fog) therefore passed while the plume in the Game view was a ghost darker than the dusk sky. Same card, same ANGULAR size, at the real distance.
            SmokeAt(gate, say, tag, false, 430f, "_FAR");
            SmokeIntersection(gate, say, tag);
            return near;
        }

        /// One smoke board: a card of 36 x 44 yd at 130 yd (scaled about the camera by dist / 130, so every distance shows the same angular footprint) over the Crater sky.
        static SmokeResult SmokeAt(System.Action<string, bool, string> gate, System.Action<string> say, string tag, bool sweep, float dist, string suffix)
        {
            var res = new SmokeResult();
            var mat = GolfLook.Get("LK_SMOKE");
            var src = ReadSource("Smoke_C");
            string cand = System.Environment.GetEnvironmentVariable("GOLF_LOOK_SMOKE_CANDIDATE");   // a folder with Smoke_C.png: gate / tuning before it is installed
            if (!string.IsNullOrEmpty(cand) && File.Exists(Path.Combine(cand, "Smoke_C.png")))
            {
                mat = new Material(mat) { name = "candidate smoke (not an LK_ name)" };
                mat.SetTexture("_MainTex", LoadPng(Path.Combine(cand, "Smoke_C.png"), false));
                var t0 = new Texture2D(2, 2, TextureFormat.RGBA32, false); t0.LoadImage(File.ReadAllBytes(Path.Combine(cand, "Smoke_C.png"))); src = t0.GetPixels32(); Object.DestroyImmediate(t0);
                Say(say, $"INFO: {tag}SMOKE uses the CANDIDATE texture in {cand}");
            }
            if (src == null) { gate($"{tag}SMOKE_SOFT{suffix}", false, "Smoke_C.png not found"); return res; }
            float ca = mat.GetColor("_Color").a, pw = mat.GetFloat("_AlphaPower"), fade = mat.GetFloat("_EdgeFade"), gain = mat.GetFloat("_AlphaGain");
            var eff = new float[src.Length]; int side = Mathf.RoundToInt(Mathf.Sqrt(src.Length));
            for (int i = 0; i < src.Length; i++)
            {
                float u = (i % side + .5f) / side, v = (i / side + .5f) / side, edgeDist = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
                float f = fade > .0001f ? Mathf.SmoothStep(0, 1, Mathf.Clamp01(edgeDist / fade)) : 1;      // the shader's smoothstep(0, _EdgeFade, edge)
                eff[i] = Mathf.Pow(Mathf.Clamp01(src[i].a / 255f * ca * gain), pw) * f;
            }
            var sorted = (float[])eff.Clone(); System.Array.Sort(sorted);
            res.AlphaMax = sorted[sorted.Length - 1]; res.AlphaP95 = sorted[(int)(sorted.Length * .95f)];
            double edge = 0; int en = 0;
            for (int i = 0; i < side; i++) { foreach (var j in new[] { i, (side - 1) * side + i, i * side, i * side + side - 1 }) { edge = System.Math.Max(edge, eff[j]); en++; } }
            res.EdgeAlpha = (float)edge;

            using var r = new Rig(10);
            float k = dist / 130f, camY = 9f;                           // scale every board point about the camera (0, camY, 0): same angles, other distance
            Vector3 Q(float x, float y, float z) => r.P(x * k, camY + (y - camY) * k, z * k);
            var card = Card(r, "DRESS_SMOKE board", mat, Q(-10, 4, 130), r.Right, 36 * k, 44 * k);
            r.Look(r.P(0, camY, 0), Q(-10, 24, 130));
            var with = r.Shot("smoke_card" + suffix.ToLowerInvariant());
            var without = r.Without("smoke_card_without" + suffix.ToLowerInvariant(), card);
            var delta = new float[with.Length]; var list = new List<float>();
            for (int i = 0; i < with.Length; i++)
            {
                float d = Mathf.Abs(LookPixels.Lum(with[i]) - LookPixels.Lum(without[i]));
                delta[i] = d; if (d >= 1.5f) list.Add(d);
            }
            res.N = list.Count;
            if (list.Count > 0) { list.Sort(); res.DeltaMedian = list[list.Count / 2]; res.DeltaP99 = list[(int)(list.Count * .99f)]; }
            // edge hardness: the largest steps of the card's own signature between neighbouring pixels
            var grads = new List<float>();
            for (int y = 1; y < r.H - 1; y++)
                for (int x = 1; x < r.W - 1; x++)
                {
                    int i = y * r.W + x;
                    if (delta[i] < 1.5f && delta[i - 1] < 1.5f && delta[i + 1] < 1.5f && delta[i - r.W] < 1.5f) continue;
                    grads.Add(Mathf.Max(Mathf.Abs(delta[i + 1] - delta[i - 1]), Mathf.Abs(delta[i + r.W] - delta[i - r.W])) / 2f);
                }
            grads.Sort();
            res.GradP99 = grads.Count > 0 ? grads[(int)(grads.Count * .99f)] : 0;
            var plume = new List<Color32>(); for (int i = 0; i < with.Length; i++) if (LookPixels.Lum(with[i]) - LookPixels.Lum(without[i]) > 8f) plume.Add(with[i]);
            var top = MeanHsv(plume.OrderByDescending(LookPixels.Lum).Take(Mathf.Max(1, plume.Count / 10)).ToList());
            Say(say, $"INFO: {tag}SMOKE{suffix} card over the crater sky at {dist:F0} yd: effective alpha max {res.AlphaMax:F2} p95 {res.AlphaP95:F2} border {res.EdgeAlpha:F3} (_Color.a {ca:F2}, _AlphaGain {gain:F2}, _AlphaPower {pw:F2}, _EdgeFade {fade:F2}, _FogShare {(mat.HasProperty("_FogShare") ? mat.GetFloat("_FogShare") : 1f):F2}); {res.N} px move the frame, |delta luminance| median {res.DeltaMedian:F1} p99 {res.DeltaP99:F1}, edge step p99 {res.GradP99:F2} levels/px; brightest tenth of the plume {LookPixels.Rgb(top.Mean)} S {top.S:F2} (crater.jpg plume (183,154,150) S .18)");
            // crater.jpg's plumes: top-10 % of a plume (183,154,150) / (165,133,129) over a sky of luminance ~100 = a peak ~50 levels above the sky; opaque-ish at the core, feathered out.
            // The first build's card (alpha peak .49) moved the frame by only median 4.6 / p99 14.5 levels: a plume nobody can pick out. Visible = peak alpha .55..72 and p99 >= 28 levels.
            bool smokeOk = res.N >= 3000 && res.AlphaMax >= SmokeAlphaMin && res.AlphaMax <= SmokeAlphaMax && res.EdgeAlpha <= .02f && res.DeltaP99 >= SmokeDeltaP99Min && res.DeltaP99 <= 90 && res.DeltaMedian >= SmokeDeltaMedianMin && res.GradP99 <= 6;
            if (suffix.Length == 0)
                gate($"{tag}SMOKE_SOFT_GAMEVIEW", smokeOk, $"calibration id (Game-view card over the Crater sky, post OFF): effective alpha max {res.AlphaMax:F2} ({SmokeAlphaMin}..{SmokeAlphaMax}), border {res.EdgeAlpha:F3} (<= .02), median |delta lum| {res.DeltaMedian:F1} (>= {SmokeDeltaMedianMin}), p99 {res.DeltaP99:F1} ({SmokeDeltaP99Min}..90), edge step p99 {res.GradP99:F2} (<= 6); {res.N} px");
            gate($"{tag}SMOKE_SOFT{suffix}", smokeOk,
                $"{(suffix.Length == 0 ? "" : $"AT THE REAL DISTANCE {dist:F0} yd (Crater fog 120..1000 yd): ")}effective alpha max {res.AlphaMax:F2} ({SmokeAlphaMin}..{SmokeAlphaMax}: translucent, never a ball), border alpha {res.EdgeAlpha:F3} (<= .02, no hard rim), card moves the frame by median {res.DeltaMedian:F1} (>= {SmokeDeltaMedianMin}) / p99 {res.DeltaP99:F1} luminance levels (p99 {SmokeDeltaP99Min}..90: readable but never a ball; {res.N} px), edge step p99 {res.GradP99:F2} levels/px (<= 6)");
            if (sweep) SmokeSweep(r, mat, card, without, say, tag);
            return res;
        }

        /// GOLF_LOOK_SWEEP=1: smoke card parameters (tint, opacity gain, self light) over the crater sky: how far the plume moves the frame. INFO only.
        static void SmokeSweep(Rig r, Material mat, GameObject card, Color32[] without, System.Action<string> say, string tag)
        {
            var c0 = mat.GetColor("_Color"); float g0 = mat.GetFloat("_AlphaGain"), e0 = mat.GetFloat("_Emission"), l0 = mat.GetFloat("_Lit"), p0 = mat.GetFloat("_AlphaPower");
            r.Look(r.P(0, 9, 0), r.P(-10, 24, 130));
            try
            {
                foreach (var (tint, g, e, lit) in new[] { (new Vector3(.62f, .56f, .56f), 1f, .05f, .6f), (new Vector3(.62f, .56f, .56f), 1.3f, .05f, .6f), (new Vector3(.8f, .74f, .72f), 1.3f, .1f, .6f), (new Vector3(.9f, .82f, .80f), 1.3f, .15f, .6f),
                                                          (new Vector3(.9f, .82f, .80f), 1.4f, .25f, .6f), (new Vector3(1f, .92f, .88f), 1.4f, .25f, .6f), (new Vector3(1f, .92f, .88f), 1.4f, .4f, .8f), (new Vector3(.9f, .82f, .80f), 1.5f, .3f, .6f) })
                {
                    mat.SetColor("_Color", new Color(tint.x, tint.y, tint.z, c0.a)); mat.SetFloat("_AlphaGain", g); mat.SetFloat("_Emission", e); mat.SetFloat("_Lit", lit);
                    var f = r.Shot(null); var deltas = new List<float>(); var plume = new List<Color32>();
                    for (int i = 0; i < f.Length; i++) { float d = LookPixels.Lum(f[i]) - LookPixels.Lum(without[i]); if (Mathf.Abs(d) >= 1.5f) { deltas.Add(Mathf.Abs(d)); if (d > 8) plume.Add(f[i]); } }
                    deltas.Sort(); var hs = MeanHsv(plume.OrderByDescending(LookPixels.Lum).Take(Mathf.Max(1, plume.Count / 10)).ToList());
                    float amax = 0; { var src = ReadSource("Smoke_C"); foreach (var px in src) amax = Mathf.Max(amax, px.a / 255f * c0.a * g); }
                    say($"INFO: {tag}SWEEP smoke tint ({tint.x:F2},{tint.y:F2},{tint.z:F2}) gain {g:F1} (alpha peak {amax:F2}) emission {e:F2} lit {lit:F1}: {deltas.Count} px, |delta| median {(deltas.Count > 0 ? deltas[deltas.Count / 2] : 0):F1} p99 {(deltas.Count > 0 ? deltas[(int)(deltas.Count * .99f)] : 0):F1}; brightest 10 % of the plume {LookPixels.Rgb(hs.Mean)} S {hs.S:F2} (crater.jpg plume (183,154,150) S .18)");
                }
            }
            finally { mat.SetColor("_Color", c0); mat.SetFloat("_AlphaGain", g0); mat.SetFloat("_Emission", e0); mat.SetFloat("_Lit", l0); mat.SetFloat("_AlphaPower", p0); }
        }

        // ------------------------------------------------------------------ no post-processing dependence

        /// Post renders nowhere in this project (no PostProcessData on either renderer) and the golf look must not use it.
        public static void NoPost(System.Action<string, bool, string> gate, System.Action<string> say)
        {
            var notes = new List<string>(); bool ok = true;
            foreach (int hole in new[] { 8, 9, 10 })
            {
                using var r = new Rig(hole);
                var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(r.Cam);
                int volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length;
                bool off = !data.renderPostProcessing;
                r.Phone(r.P(0, 6.56f, 0), r.Aim);
                var a = r.Shot(null);
                data.renderPostProcessing = true;
                var b = r.Shot(null);
                data.renderPostProcessing = false;
                int maxDiff = 0; for (int i = 0; i < a.Length; i++) maxDiff = Mathf.Max(maxDiff, Mathf.Abs(a[i].r - b[i].r) + Mathf.Abs(a[i].g - b[i].g) + Mathf.Abs(a[i].b - b[i].b));
                bool holeOk = off && volumes == 0 && maxDiff == 0;
                ok &= holeOk;
                notes.Add($"hole {hole}: renderPostProcessing after Apply {!off}, Volumes in scene {volumes}, frame with post forced on vs off max |diff| {maxDiff}");
            }
            // both pipeline assets: the renderer has no PostProcessData (so URP drops post: the frames above are identical for that reason)
            var assets = new List<string>();
            foreach (var path in PipelineAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
                var list = asset ? asset.rendererDataList : null;
                var data = list != null && list.Length > 0 ? list[0] as UnityEngine.Rendering.Universal.UniversalRendererData : null;
                assets.Add($"{System.IO.Path.GetFileNameWithoutExtension(path)} renderer '{(data ? data.name : "?")}' postProcessData {(data && data.postProcessData ? "SET" : "none")}");
            }
            // the sources: no Volume / TennisPost / bloom / ACES in code lines of the golf look
            var offenders = new List<string>();
            foreach (var f in new[] { "Scripts/Course/GolfAtmosphere.cs", "Scripts/Course/GolfLook.cs", "Scripts/Course/HoleView.cs" })
            {
                int n = 0;
                foreach (var line in File.ReadAllLines(Path.Combine(Application.dataPath, f)))
                {
                    var t = line.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
                    string code = t; int c = code.IndexOf("//"); if (c >= 0) code = code.Substring(0, c);
                    if (code.Contains("VolumeProfile") || code.Contains("TennisPost") || code.Contains(".Volume") || code.Contains("Bloom") || code.Contains("Tonemapping")) n++;
                }
                if (n > 0) offenders.Add($"{f} ({n})");
            }
            ok &= offenders.Count == 0;
            Say(say, "INFO: NO_POST " + string.Join(" | ", assets));
            gate("NO_POST_DEPENDENCE", ok, string.Join("; ", notes) + "; " + string.Join("; ", assets) + "; code lines naming Volume/TennisPost/Bloom/Tonemapping in GolfAtmosphere/GolfLook/HoleView: " + (offenders.Count == 0 ? "none" : string.Join(", ", offenders)));
        }

        // ------------------------------------------------------------------ both pipeline assets

        public static readonly string[] PipelineAssets =
        {
            "Assets/Resources/Tennis/Rendering/TennisURP.asset",
            "Assets/Characters/HeroBase/Rendering/HeroBaseStudioURP.asset",
        };

        /// One full board suite under whatever pipeline asset is active. Returns (gate lines, failures) and forwards the lines.
        public static (int gates, int fails) Suite(System.Action<string, bool, string> gate, System.Action<string> say, string tag, bool sweep)
        {
            int gates = 0, fails = 0;
            void G(string n, bool ok, string d) { gates++; if (!ok) fails++; gate(n, ok, d); }
            Crater(G, say, tag, sweep);
            foreach (int hole in new[] { 8, 9 }) Shelf(hole, G, say, tag, sweep);
            foreach (int hole in new[] { 8, 9, 10 }) Grass(hole, G, say, tag);
            Smoke(G, say, tag, sweep);
            return (gates, fails);
        }

        /// Run the suite under each pipeline asset (QualitySettings.renderPipeline swapped IN MEMORY and put back; ProjectSettings is never saved).
        /// The two assets differ only in the per-object additional-lights limit (2 / 4); HDR, MSAA 4, per-pixel additional lights, a 42 m shadow
        /// distance with 2 cascades and soft shadows are the same, and neither renderer has PostProcessData. Their settings are logged.
        /// URP_BOTH_ASSETS passes when (1) the two light gates that depend on the asset (LAVA_LIGHTS_REACH_BASALT, LAVA_LIGHTS_KEEP_GRASS_GREEN) pass under
        /// BOTH assets and (2) every gate of the suite has the same PASS / FAIL under both (nothing is asset-specific). A gate that fails under both
        /// (a texture still to land, say) is reported by its own gate line, not blamed on the pipeline.
        public static void BothAssets(System.Action<string, bool, string> gate, System.Action<string> say, bool sweep = false)
        {
            var original = QualitySettings.renderPipeline;
            var lines = new List<string>(); var results = new List<Dictionary<string, bool>>(); bool lightsOk = true;
            try
            {
                foreach (var path in PipelineAssets)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(path);
                    if (!asset) { lines.Add(path + " missing"); lightsOk = false; continue; }
                    QualitySettings.renderPipeline = asset;
                    string tag = $"[{asset.name}] ";
                    var up = asset as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                    if (up) say($"INFO: {tag}asset settings: HDR {up.supportsHDR}, MSAA {up.msaaSampleCount}, additional lights {up.additionalLightsRenderingMode}, per-object limit {up.maxAdditionalLightsCount}, main light shadows {up.supportsMainLightShadows} ({up.mainLightShadowmapResolution}), shadow distance {up.shadowDistance:F0}, cascades {up.shadowCascadeCount}, soft shadows {up.supportsSoftShadows}, render scale {up.renderScale:F2}");
                    var res = new Dictionary<string, bool>();
                    var (g, f) = Suite((n, ok, d) => { say($"GATE: {n} {(ok ? "PASS" : "FAIL")} - {d}"); res[n.Replace(tag, "")] = ok; }, say, tag, false);
                    results.Add(res);
                    var failed = res.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();
                    lines.Add($"{asset.name} (per-object additional lights {(up ? up.maxAdditionalLightsCount : -1)}): {g - f}/{g} gates PASS{(failed.Count > 0 ? ", FAIL: " + string.Join(", ", failed.ToArray()) : "")}");
                    foreach (var name in new[] { "LAVA_LIGHTS_REACH_BASALT", "LAVA_LIGHTS_KEEP_GRASS_GREEN" })
                        lightsOk &= res.TryGetValue(name, out var ok) && ok;
                }
            }
            finally { QualitySettings.renderPipeline = original; }
            var odd = new List<string>();
            if (results.Count == 2) foreach (var kv in results[0]) if (!results[1].TryGetValue(kv.Key, out var other) || other != kv.Value) odd.Add(kv.Key);
            bool parity = results.Count == 2 && odd.Count == 0;
            gate("URP_BOTH_ASSETS", lightsOk && parity, $"{string.Join("; ", lines)}; lava lights gates pass under both: {lightsOk}; same PASS/FAIL under both assets: {parity}{(odd.Count > 0 ? " (differ: " + string.Join(", ", odd.ToArray()) + ")" : "")}");
        }
    }
}
