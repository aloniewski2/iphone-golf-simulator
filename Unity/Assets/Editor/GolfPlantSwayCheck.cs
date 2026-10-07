using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.EditorTools
{
    /// Gates of the plant sway (GolfArcade/GolfPlants + GolfWindSway; RUNTIME.md "v2 2026-10-05 area U"). Edit-mode renders of a test rig (an ortho camera, the REAL LK_PLANTS material) and
    /// the real hole prefabs' PLANT_* meshes:
    ///   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.GolfPlantSwayCheck.Run -logFile <abs log>     (through work/postcard-look/heavy.sh)
    /// GOLF_SWAY_REPORT=<path> writes the GATE lines; GOLF_SWAY_SHOTS=<dir> keeps the rig frames (png). Exit code 1 when a gate fails.
    ///
    ///   SWAY_SHADER_COMPILES        shader imported without errors, supported, the four passes exist, SRP Batcher compatible, Metal-safe constructs (static read of the source)
    ///   SWAY_MATERIAL_UNCHANGED     GolfLook's LK_PLANTS = GolfPlants with the same palette atlas, tint, smoothness as the table (the old URP Lit values)
    ///   SWAY_MIRROR_MATCHES_SHADER  GPU silhouettes of a weight-graded strip vs GolfWindSway.Displacement, 3 winds x 6 times x 2 camera axes + a rotated / non-uniformly scaled instance
    ///   SWAY_ROOT_PINNED            the root row (weight 0) does not move; a strip without colours and one with white colours (B = 1) do not move at all (no data = static)
    ///   SWAY_WIND_DRIVEN            amplitude monotonic in speed, idle breeze 25 % and non-zero, direction (sin d, 0, cos d) in the global vector, and on the GPU the strip leans toward the wind (4 directions)
    ///   SWAY_GENTLE_CONTRACT        the per-kind tip weights of the authoring table stay under the caps at 0 / 10 / 20 mph (mirror, sampled over the whole 600 s loop)
    ///   SWAY_GENTLE                 the REAL PLANT_* meshes of the three installed holes: per-kind max displacement at 0 / 10 / 20 mph vs the caps (needs sway colours on the meshes)
    ///   PLANT_SWAY_COLORS_UNITY     every PLANT_* mesh as Unity imported it carries the colour attribute: root weight ~0, tip weight > .6, B = 0, A = 1
    ///   SWAY_PHASE_DECORRELATED     neighbouring clumps do not move together (mirror, 300 pivot pairs)
    ///   SWAY_ONLY_PLANTS_RIG        every other LK_ material (ground, rock, lava, surf, fall, smoke, basalt ...) renders pixel-identical under two different winds / clocks
    public static class GolfPlantSwayCheck
    {
        static readonly List<string> report = new();
        static int gates, fails;
        static void Say(string s) { report.Add(s); Debug.Log(s); }
        static string tagNow = "";
        /// GOLF_SWAY_NEGATIVE=1: the NEGATIVE CONTROL of the GPU gates. The mirror's prediction is scaled by 0.5 (a mirror that disagrees with the shader), so SWAY_MIRROR_MATCHES_SHADER, SWAY_FBX_CHAIN_ON_GPU and
        /// SWAY_SHADOW_FOLLOWS_PLANT must FAIL; the run is only ever made to show the gates can fail (v2/sway/swaycheck_negative.txt).
        static readonly float NegScale = Environment.GetEnvironmentVariable("GOLF_SWAY_NEGATIVE") == "1" ? .5f : 1f;
        static void Gate(string name, bool ok, string detail) { gates++; if (!ok) fails++; Say($"GATE: {tagNow}{name} {(ok ? "PASS" : "FAIL")} - {detail}"); }

        // caps at 20 mph (user brief), yards; the tip weights the authoring table asks for (RUNTIME.md U.0): displacement = AmpFull x weight, so the cap is a cap on the tip weight
        public static readonly (string kind, string[] prefixes, float cap, float tipLo, float tipHi)[] Kinds =
        {
            ("tuft",   new[] { "PLANT_TUFT", "PLANT_GRASS" },    .06f, .8f,  1.0f),
            ("flower", new[] { "PLANT_FLOWER", "PLANT_AGAVE" },   .05f, .6f,  .84f),
            ("shrub",  new[] { "PLANT_SHRUB", "PLANT_BUSH" },     .03f, .3f,  .50f),
            ("tree",   new[] { "PLANT_TREE", "PLANT_PINE" },     .03f, .2f,  .50f),
            ("vine",   new[] { "PLANT_VINE" },                   .08f, .9f,  1.0f),
        };

        public static void Run()
        {
            try { RunAll(); }
            catch (Exception e) { Gate("SWAY_CHECK_RAN", false, "exception: " + e); }
            Say($"SUMMARY: {gates - fails}/{gates} gates PASS");
            string path = Environment.GetEnvironmentVariable("GOLF_SWAY_REPORT");
            if (!string.IsNullOrEmpty(path)) { try { File.WriteAllLines(Path.GetFullPath(path), report.ToArray()); } catch (Exception e) { Debug.LogWarning(e.Message); } }
            if (Application.isBatchMode) EditorApplication.Exit(fails == 0 ? 0 : 1);
        }

        [MenuItem("Golf Arcade/Plant sway check")]
        static void Menu() { report.Clear(); gates = fails = 0; Run(); }

        static void RunAll()
        {
            AssetDatabase.Refresh();
            var pipeline = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Say($"INFO: pipeline asset '{(pipeline ? pipeline.name : "none")}', graphics API {SystemInfo.graphicsDeviceType}, Unity {Application.unityVersion}");
            GolfWindSway.FreezeTime = null; GolfWindSway.ForcedWind = null;
            ShaderCompiles();
            MaterialUnchanged();
            WindMath();
            PhaseDecorrelated();
            GentleContract();
            RealMeshes();
            var saved = LookPixels.SaveRenderSettings();
            try
            {
                GpuSuite();
                // the same GPU gates under the OTHER URP asset (the iPhone quality level may point at either; they differ in the per-object additional-light limit), set IN MEMORY and restored
                var active = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                foreach (var name in new[] { "HeroBaseStudioURP", "TennisURP" })
                {
                    if (active && active.name == name) continue;
                    UnityEngine.Rendering.RenderPipelineAsset other = null;
                    foreach (var guid in AssetDatabase.FindAssets(name + " t:UniversalRenderPipelineAsset")) { var ap = AssetDatabase.GUIDToAssetPath(guid); if (Path.GetFileNameWithoutExtension(ap) == name) { other = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(ap); break; } }
                    if (!other) { Say("INFO: URP asset " + name + " not found: its GPU pass is skipped"); continue; }
                    var previous = QualitySettings.renderPipeline; QualitySettings.renderPipeline = other; tagNow = $"[{name}] ";
                    try { GpuSuite(); }
                    finally { tagNow = ""; QualitySettings.renderPipeline = previous; }
                }
            }
            finally
            {
                GolfWindSway.FreezeTime = null; GolfWindSway.ForcedWind = null;
                Shader.SetGlobalVector("_GolfWind", Vector4.zero);
                LookPixels.RestoreRenderSettings(saved);
            }
        }

        static void GpuSuite()
        {
            MirrorOnGpu();
            FbxChain();
            ShadowFollowsPlant();
            OnlyPlantsRig();
        }

        // ------------------------------------------------------------------ shader

        static void ShaderCompiles()
        {
            var shader = Resources.Load<Shader>("Course/Shaders/GolfPlants");
            if (!shader) { Gate("SWAY_SHADER_COMPILES", false, "Resources/Course/Shaders/GolfPlants.shader not found"); return; }
            var msgs = ShaderUtil.GetShaderMessages(shader);
            var errors = msgs.Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            var warnings = msgs.Where(m => m.severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();
            var mat = new Material(shader);
            var problems = new List<string>();
            if (shader.name != "GolfArcade/GolfPlants") problems.Add("shader name " + shader.name);
            if (!shader.isSupported) problems.Add("not supported on " + SystemInfo.graphicsDeviceType);
            if (ShaderUtil.ShaderHasError(shader)) problems.Add("ShaderUtil.ShaderHasError");
            if (errors.Length > 0) problems.Add($"{errors.Length} shader error(s): " + string.Join(" | ", errors.Take(3).Select(m => m.message + " (" + m.platform + ")").ToArray()));
            string[] want = { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" };
            var have = new List<string>();
            for (int i = 0; i < mat.passCount; i++) have.Add(mat.GetPassName(i));
            foreach (var p in want) if (mat.FindPass(p) < 0) problems.Add("pass " + p + " missing (have " + string.Join(",", have.ToArray()) + ")");
            // SRP Batcher: the layout of every pass must be compatible (UnityPerMaterial = LitInput's)
            string srp = SrpBatcher(shader, mat.passCount);
            if (!srp.StartsWith("compatible")) problems.Add("SRP Batcher: " + srp);
            // Metal-safe constructs: a static read of the source (no exclude_renderers, target <= 3.5, no geometry / tessellation / compute / per-pixel loops, a vertex-only sin / hash)
            string path = AssetDatabase.GetAssetPath(shader);
            string src = File.Exists(path) ? File.ReadAllText(path) : "";
            var metal = MetalConstructs(src);
            problems.AddRange(metal);
            // the instancing + the global
            if (!src.Contains("multi_compile_instancing")) problems.Add("no multi_compile_instancing");
            if (!src.Contains("float4 _GolfWind;")) problems.Add("_GolfWind global not declared");
            // GL / Vulkan / GLES are not compiled here (the editor runs one API): reported, not gated
            Say($"INFO: GolfPlants: {mat.passCount} pass(es) [{string.Join(", ", have.ToArray())}], {warnings.Length} warning(s){(warnings.Length > 0 ? ": " + string.Join(" | ", warnings.Take(3).Select(m => m.message).ToArray()) : "")}; compiled for {SystemInfo.graphicsDeviceType} only (the editor's API); the other APIs are checked by construction (no API-specific code, Lit's own includes)");
            Gate("SWAY_SHADER_COMPILES", problems.Count == 0, problems.Count == 0 ? $"imported without errors on {SystemInfo.graphicsDeviceType}, supported, passes {string.Join("/", want)}, SRP Batcher {srp}, Metal-safe constructs ({src.Length} chars read)" : string.Join("; ", problems.ToArray()));
            UnityEngine.Object.DestroyImmediate(mat);
        }

        static string SrpBatcher(Shader shader, int passes)
        {
            // internal API: ShaderUtil.GetSRPBatcherCompatibilityCode(Shader, int subShaderIndex) (the int is the SUB-SHADER index: a pass index crashes the editor).
            // Calibrated here against shaders whose answer is known: URP Lit / URP Unlit / GolfSurf are SRP Batcher compatible and read 1; "UI/Default" (its _Color sits outside UnityPerMaterial) reads 10
            // and the built-in "Standard" reads 0. GolfPlants must read what URP Lit reads, and the probe must tell the known-bad shaders from the known-good ones.
            var mi = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Shader), typeof(int) }, null);
            if (mi == null) return "no ShaderUtil.GetSRPBatcherCompatibilityCode(Shader, int) in this editor";
            int Code(Shader sh) { if (!sh) return -9; try { return (int)mi.Invoke(null, new object[] { sh, 0 }); } catch (Exception e) { Debug.LogWarning("SRP batcher probe failed: " + (e.InnerException ?? e).Message); return -1; } }
            int mine = Code(shader);
            var good = new[] { ("URP Lit", Shader.Find("Universal Render Pipeline/Lit")), ("URP Unlit", Shader.Find("Universal Render Pipeline/Unlit")), ("GolfSurf", Resources.Load<Shader>("Course/Shaders/GolfSurf")) };
            var bad = new[] { ("UI/Default", Shader.Find("UI/Default")), ("Standard", Shader.Find("Standard")) };
            var goodCodes = good.Select(g => (g.Item1, Code(g.Item2))).ToArray(); var badCodes = bad.Select(g => (g.Item1, Code(g.Item2))).ToArray();
            Say($"INFO: SRP Batcher probe (compatible = 1): GolfPlants {mine}; known compatible: {string.Join(", ", goodCodes.Select(g => g.Item1 + " " + g.Item2).ToArray())}; known incompatible: {string.Join(", ", badCodes.Select(g => g.Item1 + " " + g.Item2).ToArray())}");
            if (goodCodes.Any(g => g.Item2 != 1) || badCodes.Any(g => g.Item2 == 1)) return $"the probe is not trustworthy (known-compatible shaders read {string.Join("/", goodCodes.Select(g => g.Item2.ToString()).ToArray())}, known-incompatible {string.Join("/", badCodes.Select(g => g.Item2.ToString()).ToArray())})";
            return mine == 1 ? "compatible (reads 1 like URP Lit; UI/Default and Standard read 10 / 0)" : $"NOT compatible (code {mine})";
        }

        static List<string> MetalConstructs(string src)
        {
            var bad = new List<string>();
            foreach (var raw in src.Split('\n'))
            {
                string ln = raw.Split(new[] { "//" }, StringSplitOptions.None)[0].Trim();
                if (ln.Contains("exclude_renderers") && ln.Contains("metal")) bad.Add("excludes metal");
                if (ln.StartsWith("#pragma geometry") || ln.StartsWith("#pragma hull") || ln.StartsWith("#pragma domain")) bad.Add("tessellation / geometry stage: " + ln);
                if (ln.StartsWith("#pragma target"))
                {
                    var t = ln.Split(' ').Last(); if (float.TryParse(t, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 3.5f) bad.Add("target " + t + " > 3.5");
                }
                if (ln.Contains("RWTexture") || ln.Contains("RWStructuredBuffer") || ln.Contains("[unroll") || ln.Contains("tex2Dlod") || ln.Contains("InterlockedAdd")) bad.Add("construct not for a vertex-sway shader: " + ln);
            }
            return bad;
        }

        static void MaterialUnchanged()
        {
            GolfLook.ClearCache();
            var m = GolfLook.Get("LK_PLANTS"); var spec = GolfLook.Table["LK_PLANTS"];
            var problems = new List<string>();
            if (!m || !m.shader || m.shader.name != "GolfArcade/GolfPlants") problems.Add("shader " + (m && m.shader ? m.shader.name : "(none)"));
            else
            {
                var tex = m.GetTexture("_BaseMap");
                if (!tex || tex.name != "Plants_C") problems.Add("_BaseMap " + (tex ? tex.name : "null"));
                var tint = m.GetColor("_BaseColor");
                if (Mathf.Abs(tint.r - spec.Tint.r) > 1e-3f || Mathf.Abs(tint.g - spec.Tint.g) > 1e-3f || Mathf.Abs(tint.b - spec.Tint.b) > 1e-3f) problems.Add("tint " + tint);
                if (Mathf.Abs(m.GetFloat("_Smoothness") - spec.Smoothness) > 1e-4f) problems.Add("smoothness " + m.GetFloat("_Smoothness"));
                if (m.GetFloat("_Metallic") != 0) problems.Add("metallic " + m.GetFloat("_Metallic"));
                if (!m.enableInstancing) problems.Add("instancing off");
                if (m.name != "LK_PLANTS") problems.Add("name " + m.name);
            }
            // nothing else maps to the sway shader
            var others = GolfLook.Table.Where(kv => kv.Key != "LK_PLANTS").Select(kv => (kv.Key, GolfLook.Get(kv.Key))).Where(t => t.Item2 && t.Item2.shader && t.Item2.shader.name == "GolfArcade/GolfPlants").Select(t => t.Key).ToArray();
            if (others.Length > 0) problems.Add("other material(s) on the sway shader: " + string.Join(",", others));
            Gate("SWAY_MATERIAL_UNCHANGED", problems.Count == 0, problems.Count == 0 ? $"LK_PLANTS = GolfArcade/GolfPlants, Plants_C, tint {(Vector3)(Vector4)m.GetColor("_BaseColor") * 1f}, smoothness {m.GetFloat("_Smoothness"):F2}, metallic 0, instancing on; the other {GolfLook.Table.Count - 1} table rows are not on it" : string.Join("; ", problems.ToArray()));
        }

        // ------------------------------------------------------------------ wind math (mirror level)

        static void WindMath()
        {
            var problems = new List<string>();
            float prev = -1; bool mono = true;
            for (int mph = 0; mph <= 20; mph++) { float a = GolfWindSway.Amplitude(mph); if (a <= prev) mono = false; prev = a; }
            float a0 = GolfWindSway.Amplitude(0), a10 = GolfWindSway.Amplitude(10), a20 = GolfWindSway.Amplitude(20), a40 = GolfWindSway.Amplitude(40);
            if (!mono) problems.Add("amplitude not strictly increasing with speed 0..20");
            if (a0 <= 0) problems.Add("idle breeze is zero");
            if (Mathf.Abs(a0 / a20 - .25f) > .01f) problems.Add($"idle share {a0 / a20:F3} (want .25)");
            if (Mathf.Abs(a20 - GolfWindSway.AmpFullYards) > 1e-6f) problems.Add("amplitude at Wind.MaxMPH != AmpFullYards");
            if (a40 != a20) problems.Add("amplitude above 20 mph is not clamped");
            float worstDir = 0;
            foreach (int d in new[] { 0, 45, 90, 135, 180, 225, 270, 315, -90, 360 })
            {
                var v = GolfWindSway.GlobalValue(new Wind(12, d), 3f);
                var want = new Vector2(Mathf.Sin(d * Mathf.Deg2Rad), Mathf.Cos(d * Mathf.Deg2Rad));
                var got = new Vector2(v.x, v.z).normalized;
                worstDir = Mathf.Max(worstDir, Vector2.Distance(want, got));
                if (v.y != 0) problems.Add("y component " + v.y);
            }
            if (worstDir > 1e-4f) problems.Add($"direction off by {worstDir:G3}");
            // time wrap: the loop is seamless (every frequency is a whole number of cycles per LoopSeconds)
            float seam = 0;
            var wnd = new Vector2(.04f, .02f);
            for (int k = 0; k < 40; k++)
            {
                float ox = k * 1.37f - 20, oz = k * -.83f + 7, w = (k % 10 + 1) / 10f, ph = (k * .137f) % 1f;
                var a = GolfWindSway.Displacement(w, ph, ox, oz, wnd, GolfWindSway.LoopSeconds - 1e-3f);
                var b = GolfWindSway.Displacement(w, ph, ox, oz, wnd, 0f);
                seam = Mathf.Max(seam, Vector2.Distance(a, b));
            }
            if (seam > 2e-4f) problems.Add($"wrap seam {seam:G3} yd");
            // the idle breeze moves the plant (non-zero) but little; 10 mph is between
            Say($"INFO: amplitude yd: 0 mph {a0:F4}, 10 mph {a10:F4}, 20 mph {a20:F4}; clock wrap seam {seam:G2} yd");
            Gate("SWAY_WIND_DRIVEN_MATH", problems.Count == 0, problems.Count == 0 ? $"amplitude strictly increasing over 0..20 mph (0 mph {a0:F4} = {a0 / a20:P0} of full {a20:F4}, 10 mph {a10:F4}), clamped above 20, direction = (sin d, 0, cos d) for 10 headings (max error {worstDir:G2}), 600 s loop seamless ({seam:G2} yd)" : string.Join("; ", problems.ToArray()));
        }

        static void PhaseDecorrelated()
        {
            var rng = new System.Random(7);
            var wnd = new Vector2(.055f * .6f, .055f * .8f);
            int pairs = 300, together = 0; double meanAbsCorr = 0; float worstMax = 0;
            for (int i = 0; i < pairs; i++)
            {
                float ox = (float)(rng.NextDouble() * 200 - 100), oz = (float)(rng.NextDouble() * 400), dist = .3f + (float)rng.NextDouble() * 3f, ang = (float)(rng.NextDouble() * Math.PI * 2);
                float px = ox + dist * Mathf.Cos(ang), pz = oz + dist * Mathf.Sin(ang);
                int n = 400; double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0; float diffMax = 0;
                for (int k = 0; k < n; k++)
                {
                    float t = k * .3f;
                    var a = GolfWindSway.Displacement(1f, .5f, ox, oz, wnd, t); var b = GolfWindSway.Displacement(1f, .5f, px, pz, wnd, t);
                    float x = a.magnitude, y = b.magnitude;
                    sa += x; sb += y; saa += x * x; sbb += y * y; sab += x * y; diffMax = Mathf.Max(diffMax, Mathf.Abs(x - y));
                }
                double cov = sab / n - sa / n * sb / n, va = saa / n - (sa / n) * (sa / n), vb = sbb / n - (sb / n) * (sb / n);
                double corr = cov / Math.Sqrt(Math.Max(va * vb, 1e-18));
                meanAbsCorr += Math.Abs(corr) / pairs; if (corr > .9) together++; worstMax = Mathf.Max(worstMax, diffMax);
            }
            Gate("SWAY_PHASE_DECORRELATED", together <= pairs * .03 && meanAbsCorr < .5, $"{pairs} pivot pairs 0.3-3.3 yd apart, 120 s: displacement series correlation > .9 for {together} pair(s) ({together * 100.0 / pairs:F1} %, limit 3 %), mean |corr| {meanAbsCorr:F2} (limit .5); largest instantaneous difference {worstMax:F3} yd (the clumps really move differently)");
        }

        static void GentleContract()
        {
            var lines = new List<string>(); bool ok = true;
            foreach (var (kind, _, cap, lo, hi) in Kinds)
            {
                float worst = 0;
                foreach (int mph in new[] { 0, 10, 20 })
                {
                    float m = SampledMax(hi, mph);
                    worst = Mathf.Max(worst, m);
                    if (mph == 20 && m > cap) ok = false;
                    if (m > GolfWindSway.MaxDisplacement(hi, mph) + 1e-5f) ok = false;   // the analytic bound must hold
                }
                float tipLoD = SampledMax(lo, 20);
                lines.Add($"{kind}: tip weight {lo:F2}..{hi:F2}, max {SampledMax(hi, 0):F4} / {SampledMax(hi, 10):F4} / {SampledMax(hi, 20):F4} yd at 0 / 10 / 20 mph (cap {cap:F2}; weight {lo:F2} gives {tipLoD:F4})");
            }
            Gate("SWAY_GENTLE_CONTRACT", ok, "authoring-table tip weights, mirror sampled over the 600 s loop at 90 pivots: " + string.Join("; ", lines.ToArray()));
        }

        /// Largest displacement magnitude (yd) of a vertex with weight w at a wind speed, over the whole 600 s loop and 90 pivots.
        static float SampledMax(float w, double mph)
        {
            float amp = GolfWindSway.Amplitude(mph), worst = 0;
            for (int p = 0; p < 90; p++)
            {
                float ox = p * 7.31f - 300, oz = p * 3.17f + 11;
                var dir = GolfWindSway.Direction(p * 4);
                var wnd = dir * amp;
                for (float t = 0; t < GolfWindSway.LoopSeconds; t += 0.31f) worst = Mathf.Max(worst, GolfWindSway.Displacement(w, (p * .071f) % 1f, ox, oz, wnd, t).magnitude);
            }
            return worst;
        }

        // ------------------------------------------------------------------ the real meshes of the installed holes

        sealed class KindStat { public int Meshes, Coloured, Instances; public float MaxTip, MaxRoot, MaxB, MinA = 1, MaxTipWeightSeen; public readonly List<string> Problems = new(); public readonly List<Vector2> Pivots = new(); }

        /// The smallest tip weight a plant of this kind must reach (the user's "tip weight > .6"; shrubs and trees are "low" by design: their caps are .03 yd, half a tuft's, so the line is > .25 for them).
        static float TipMin(string kind) => kind == "shrub" || kind == "tree" ? .25f : .6f;

        static string KindOf(string name)
        {
            foreach (var (kind, prefixes, _, _, _) in Kinds) foreach (var p in prefixes) if (name.StartsWith(p)) return kind;
            return "other";
        }

        /// The installed holes' PLANT_* meshes, then (labelled) the props library's own plants exported by Blender as a fixture FBX (v2/sway/blender/make_lib_fixture.py): the same rules on what Unity imports from the libs'
        /// exporter before the rebuilt holes are installed.
        static void RealMeshes()
        {
            MeshGates(new[] { 8, 9, 10 }.Select(n => ($"hole_{n:00}", Resources.Load<GameObject>($"Course/hole_{n:00}"))).ToList());
            // GOLF_SWAY_EXTRA_FBX=<asset path[,asset path]>: more prefabs analysed the same way (a scratch rebuild of a hole copied under Assets for the run, never an installed one)
            var extra = Environment.GetEnvironmentVariable("GOLF_SWAY_EXTRA_FBX");
            if (!string.IsNullOrWhiteSpace(extra))
                foreach (var path in extra.Split(','))
                {
                    var g = AssetDatabase.LoadAssetAtPath<GameObject>(path.Trim());
                    tagNow = $"[{Path.GetFileNameWithoutExtension(path.Trim())}] ";
                    try { MeshGates(new List<(string, GameObject)> { (Path.GetFileName(path.Trim()), g) }); } finally { tagNow = ""; }
                }
            var fix = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/GolfPlantSwayFixture/plants_lib_fixture.fbx");
            if (fix)
            {
                tagNow = "[props library fixture] ";
                try { MeshGates(new List<(string, GameObject)> { ("plants_lib_fixture", fix) }); } finally { tagNow = ""; }
            }
        }

        static void MeshGates(List<(string label, GameObject prefab)> prefabs)
        {
            var stats = new Dictionary<string, KindStat>();
            int totalMeshes = 0, colouredMeshes = 0, plantObjects = 0;
            var bad = new List<string>(); var seenMesh = new HashSet<Mesh>();
            foreach (var (label, prefab) in prefabs)
            {
                if (!prefab) { bad.Add($"{label} prefab missing"); continue; }
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!mf.name.StartsWith("PLANT_") || !mf.sharedMesh) continue;
                    plantObjects++;
                    string kind = KindOf(mf.name);
                    if (!stats.TryGetValue(kind, out var st)) stats[kind] = st = new KindStat();
                    var mesh = mf.sharedMesh;
                    var pos = mf.transform.position; if (st.Pivots.Count < 400) st.Pivots.Add(new Vector2(pos.x, pos.z));
                    st.Instances++;
                    if (!seenMesh.Add(mesh)) continue;
                    st.Meshes++; totalMeshes++;
                    var col = mesh.colors32;
                    if (col == null || col.Length != mesh.vertexCount) { bad.Add($"{mf.name} ({mesh.name}) has no colour attribute"); continue; }
                    st.Coloured++; colouredMeshes++;
                    var v = mesh.vertices; float zmin = float.MaxValue, zmax = float.MinValue;
                    var l2w = mf.transform.localToWorldMatrix;                      // the FBX node carries the axis conversion: height = the WORLD y of the vertex
                    for (int i = 0; i < v.Length; i++) { float wy = l2w.MultiplyPoint3x4(v[i]).y; v[i].y = wy; v[i].x = 0; v[i].z = 0; zmin = Mathf.Min(zmin, wy); zmax = Mathf.Max(zmax, wy); }
                    float h = Mathf.Max(zmax - zmin, 1e-4f); bool vine = kind == "vine";
                    float root = 0, tip = 0, bMax = 0, aMin = 1;
                    for (int i = 0; i < v.Length; i++)
                    {
                        float r = col[i].r / 255f, b = col[i].b / 255f, a = col[i].a / 255f; tip = Mathf.Max(tip, r); bMax = Mathf.Max(bMax, b); aMin = Mathf.Min(aMin, a);
                        bool atRoot = vine ? v[i].y >= zmax - .08f * h : v[i].y <= zmin + .08f * h;
                        if (atRoot) root = Mathf.Max(root, r);
                    }
                    st.MaxTip = Mathf.Max(st.MaxTip, tip); st.MaxRoot = Mathf.Max(st.MaxRoot, root); st.MaxB = Mathf.Max(st.MaxB, bMax); st.MinA = Mathf.Min(st.MinA, aMin);
                    if (root > .05f) st.Problems.Add($"{mesh.name} root weight {root:F2}");
                    if (tip <= TipMin(kind)) st.Problems.Add($"{mesh.name} tip weight {tip:F2} (> {TipMin(kind):F2} for a {kind})");
                    if (bMax > .02f) st.Problems.Add($"{mesh.name} B {bMax:F2}");
                    if (aMin < .98f) st.Problems.Add($"{mesh.name} A {aMin:F2}");
                }
            }
            bool allColoured = bad.Count == 0 && plantObjects > 0;
            var probs = stats.SelectMany(kv => kv.Value.Problems).ToList();
            Gate("PLANT_SWAY_COLORS_UNITY", allColoured && probs.Count == 0,
                $"{plantObjects} PLANT_* objects on {totalMeshes} unique meshes in {string.Join(", ", prefabs.Select(p => p.label).ToArray())}; {colouredMeshes} carry the colour attribute" +
                (stats.Count > 0 ? "; " + string.Join("; ", stats.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key} {kv.Value.Coloured}/{kv.Value.Meshes}: root<= {kv.Value.MaxRoot:F2}, tip {kv.Value.MaxTip:F2}, B<= {kv.Value.MaxB:F2}, A>= {kv.Value.MinA:F2}").ToArray()) : "") +
                (bad.Count > 0 ? $" | {bad.Count} mesh(es) WITHOUT colours (static: weight 0 by the B = 1 rule): {string.Join(", ", bad.Take(4).ToArray())}" : "") + (probs.Count > 0 ? " | " + string.Join(", ", probs.Take(4).ToArray()) : ""));
            // SWAY_GENTLE on what is really there
            if (colouredMeshes == 0)
            {
                Gate("SWAY_GENTLE", false, $"NOT MEASURED on the installed meshes: none of the {totalMeshes} unique PLANT_* meshes ({plantObjects} objects) carries sway colours yet, so every installed plant is static (weight 0 by the B = 1 rule) and displaces 0.000 yd; the contract numbers are SWAY_GENTLE_CONTRACT");
                return;
            }
            var lines = new List<string>(); bool ok = true;
            foreach (var (kind, _, cap, _, _) in Kinds)
            {
                if (!stats.TryGetValue(kind, out var st) || st.Coloured == 0) { lines.Add($"{kind}: none coloured"); continue; }
                float m0 = RealMax(st, 0), m10 = RealMax(st, 10), m20 = RealMax(st, 20);
                bool kok = m20 <= cap && st.Coloured == st.Meshes;
                if (!kok) ok = false;
                lines.Add($"{kind}: {st.Coloured}/{st.Meshes} meshes, tip weight {st.MaxTip:F2}, max {m0:F4} / {m10:F4} / {m20:F4} yd at 0 / 10 / 20 mph (cap {cap:F2}){(kok ? "" : " OVER / uncoloured")}");
            }
            Gate("SWAY_GENTLE", ok, "real meshes, mirror sampled over the 600 s loop at the real pivots: " + string.Join("; ", lines.ToArray()));
        }

        static float RealMax(KindStat st, double mph)
        {
            float amp = GolfWindSway.Amplitude(mph), worst = 0; int k = 0;
            foreach (var p in st.Pivots.Take(120))
            {
                var wnd = GolfWindSway.Direction(k * 3.7) * amp; k++;
                for (float t = 0; t < GolfWindSway.LoopSeconds; t += 0.41f) worst = Mathf.Max(worst, GolfWindSway.Displacement(st.MaxTip, (k * .13f) % 1f, p.x, p.y, wnd, t).magnitude);
            }
            return worst;
        }

        // ------------------------------------------------------------------ the GPU rig

        const int N = 640;
        const float HalfSize = .55f;                 // ortho half height (yd)
        static float YdPerPx => 2 * HalfSize / N;

        sealed class StripRig : IDisposable
        {
            public readonly GameObject Root; public readonly Camera Cam; readonly RenderTexture rt; readonly Texture2D tex; public readonly Material Mat;
            public StripRig()
            {
                Root = new GameObject("GolfPlants rig");
                Cam = new GameObject("rig camera").AddComponent<Camera>(); Cam.transform.SetParent(Root.transform);
                Cam.orthographic = true; Cam.orthographicSize = HalfSize; Cam.clearFlags = CameraClearFlags.SolidColor; Cam.backgroundColor = Color.black;
                Cam.nearClipPlane = .1f; Cam.farClipPlane = 30; Cam.aspect = 1; Cam.allowMSAA = true; Cam.allowHDR = false;
                rt = new RenderTexture(N, N, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                tex = new Texture2D(N, N, TextureFormat.RGB24, false);
                RenderSettings.fog = false; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .6f); RenderSettings.skybox = null;
                Mat = new Material(GolfLook.Get("LK_PLANTS")) { name = "rig LK_PLANTS" };
                Mat.SetTexture("_BaseMap", Texture2D.whiteTexture); Mat.SetColor("_BaseColor", new Color(0, 1, 0, 1)); Mat.SetFloat("_Cull", 0);   // geometry test: a bright double-sided strip on black
            }
            public Color32[] Render() => LookPixels.Render(Cam, rt, tex);
            public void SaveLast(string name)
            {
                string dir = Environment.GetEnvironmentVariable("GOLF_SWAY_SHOTS"); if (string.IsNullOrEmpty(dir)) return;
                Directory.CreateDirectory(dir); File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            }
            public void Dispose()
            {
                if (Root) UnityEngine.Object.DestroyImmediate(Root);
                if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
                if (Mat) UnityEngine.Object.DestroyImmediate(Mat);
            }
        }

        /// A vertical strip of `rows` vertex rows, 1 yd tall (y 0..1), 0.06 wide; colours = (weight = y, phase, 0, 1) unless `colours` is null (no attribute) or white.
        static Mesh Strip(int rows, float phase, string colours)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var nrm = new List<Vector3>(); var col = new List<Color32>(); var idx = new List<int>();
            for (int i = 0; i < rows; i++)
            {
                float y = i / (float)(rows - 1);
                v.Add(new Vector3(-.03f, y, 0)); v.Add(new Vector3(.03f, y, 0)); uv.Add(new Vector2(.5f, .5f)); uv.Add(new Vector2(.5f, .5f)); nrm.Add(Vector3.forward); nrm.Add(Vector3.forward);
                if (colours == "sway") { var c = new Color32((byte)Mathf.RoundToInt(y * 255f), (byte)Mathf.RoundToInt(phase * 255f), 0, 255); col.Add(c); col.Add(c); }
                else if (colours == "white") { col.Add(new Color32(255, 255, 255, 255)); col.Add(new Color32(255, 255, 255, 255)); }
                if (i > 0) { int a = (i - 1) * 2, b = a + 1, c2 = a + 2, d = a + 3; idx.AddRange(new[] { a, b, c2, b, d, c2 }); }
            }
            var m = new Mesh { name = "rig strip " + colours };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(nrm); m.SetTriangles(idx, 0);
            if (col.Count > 0) m.SetColors(col);
            return m;
        }

        /// Row centroids (px, sub-pixel, from the green channel) at image rows of the strip's vertex heights, or NaN where the strip is absent.
        static float[] Centroids(Color32[] px, float camY, int rows, float yScale)
        {
            var c = new float[rows];
            for (int i = 0; i < rows; i++)
            {
                float y = i / (float)(rows - 1) * yScale;
                int row = Mathf.RoundToInt((y - camY) / YdPerPx + N / 2f - .5f);
                row = Mathf.Clamp(row, 3, N - 4);
                double sw = 0, sx = 0;
                for (int dr = -2; dr <= 2; dr++)
                    for (int x = 0; x < N; x++) { float g = px[(row + dr) * N + x].g / 255f; sw += g; sx += g * x; }
                c[i] = sw > 1 ? (float)(sx / sw) : float.NaN;
            }
            return c;
        }

        static void MirrorOnGpu()
        {
            using var rig = new StripRig();
            const int rows = 9;
            var mesh = Strip(rows, .37f, "sway");
            var plain = Strip(rows, 0, null);
            var white = Strip(rows, 0, "white");
            var colAfter = mesh.colors32;   // what the shader really reads (weights are 8-bit)
            // the first edit-mode render of a session can come out empty (pipeline warm-up): throw two away on a lit strip
            { var wu = new GameObject("warm-up", typeof(MeshFilter), typeof(MeshRenderer)); wu.transform.SetParent(rig.Root.transform); wu.GetComponent<MeshFilter>().sharedMesh = mesh; wu.GetComponent<MeshRenderer>().sharedMaterial = rig.Mat;
              rig.Cam.transform.position = new Vector3(0, .5f, 6); rig.Cam.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); rig.Render(); rig.Render(); UnityEngine.Object.DestroyImmediate(wu); }
            float worstPx = 0, worstYd = 0; int samples = 0; var worstCase = "";
            float rootMove = 0, noDataMove = 0, whiteMove = 0;
            float[] times = { 0f, .7f, 1.9f, 3.3f, 5.1f, 7.7f };
            (string label, double mph, double deg)[] winds = { ("20 mph toward 90", 20, 90), ("10 mph toward 270", 10, 270), ("0 mph toward 0", 0, 0), ("20 mph toward 0", 20, 0), ("14 mph toward 180", 14, 180) };

            // instance poses: plain, and rotated 25 degrees about Y with a non-uniform scale (world-space displacement must not care)
            var poses = new (string name, Vector3 pos, Quaternion rot, Vector3 scale)[]
            {
                ("plain", new Vector3(3.17f, 0, -5.21f), Quaternion.identity, Vector3.one),
                ("rotated + scaled", new Vector3(-41.62f, 0, 118.34f), Quaternion.Euler(0, 25, 0), new Vector3(1.5f, .7f, 1.2f)),
            };
            foreach (var pose in poses)
            {
                var go = new GameObject("strip", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(rig.Root.transform);
                go.transform.position = pose.pos; go.transform.rotation = pose.rot; go.transform.localScale = pose.scale;
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = rig.Mat;
                var goPlain = new GameObject("strip no colours", typeof(MeshFilter), typeof(MeshRenderer)); goPlain.transform.SetParent(rig.Root.transform);
                goPlain.GetComponent<MeshFilter>().sharedMesh = plain; goPlain.GetComponent<MeshRenderer>().sharedMaterial = rig.Mat; goPlain.SetActive(false);
                var goWhite = new GameObject("strip white", typeof(MeshFilter), typeof(MeshRenderer)); goWhite.transform.SetParent(rig.Root.transform);
                goWhite.GetComponent<MeshFilter>().sharedMesh = white; goWhite.GetComponent<MeshRenderer>().sharedMaterial = rig.Mat; goWhite.SetActive(false);
                goPlain.transform.SetPositionAndRotation(pose.pos, pose.rot); goPlain.transform.localScale = pose.scale;
                goWhite.transform.SetPositionAndRotation(pose.pos, pose.rot); goWhite.transform.localScale = pose.scale;
                float yScale = pose.scale.y;

                foreach (string axis in new[] { "x", "z" })
                {
                    // x view: the strip lies in the XY plane (camera on +z); z view: the strip is turned 90 degrees about Y so it lies in the ZY plane (camera on +x)
                    var rotNow = axis == "x" ? pose.rot : pose.rot * Quaternion.Euler(0, 90, 0);
                    go.transform.rotation = rotNow; goPlain.transform.rotation = rotNow; goWhite.transform.rotation = rotNow;
                    // camera: x view looks along -z (image right = +x); z view looks along -x (image right = -z)
                    var c = rig.Cam.transform;
                    c.position = axis == "x" ? new Vector3(pose.pos.x, .5f * yScale, pose.pos.z + 6) : new Vector3(pose.pos.x + 6, .5f * yScale, pose.pos.z);
                    c.rotation = axis == "x" ? Quaternion.LookRotation(Vector3.back, Vector3.up) : Quaternion.LookRotation(Vector3.left, Vector3.up);
                    Vector3 right = c.right;
                    // rest
                    GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
                    var restPx = rig.Render(); rig.SaveLast($"rest_{pose.name.Replace(' ', '_').Replace('+', 'p')}_{axis}");
                    var rest = Centroids(restPx, c.position.y, rows, yScale);
                    foreach (var (label, mph, deg) in winds)
                        foreach (float t in times)
                        {
                            var wind = new Wind(mph, deg);
                            GolfWindSway.ForcedWind = wind; GolfWindSway.FreezeTime = t; GolfWindSway.Push();
                            var px = rig.Render();
                            if (t == 1.9f && pose.name == "plain" && axis == "x" && (label.StartsWith("20 mph toward 90") || label.StartsWith("20 mph toward 0"))) rig.SaveLast($"wind_{label.Replace(' ', '_')}_t{t}");
                            var cent = Centroids(px, c.position.y, rows, yScale);
                            var wxz = new Vector2(GolfWindSway.GlobalValue(wind, t).x, GolfWindSway.GlobalValue(wind, t).z);
                            for (int i = 0; i < rows; i++)
                            {
                                float w = colAfter[i * 2].r / 255f, ph = colAfter[i * 2].g / 255f;
                                var d = GolfWindSway.Displacement(w, ph, pose.pos.x, pose.pos.z, wxz, GolfWindSway.GlobalValue(wind, t).w);
                                float predicted = NegScale * (d.x * right.x + d.y * right.z) / YdPerPx;      // image px (right = +)
                                if (float.IsNaN(cent[i]) || float.IsNaN(rest[i])) { worstPx = 999; worstCase = $"{pose.name}/{axis}/{label}/t{t}: strip lost at row {i}"; continue; }
                                float measured = cent[i] - rest[i];
                                float e = Mathf.Abs(measured - predicted);
                                samples++;
                                if (e > worstPx) { worstPx = e; worstYd = e * YdPerPx; worstCase = $"{pose.name}/{axis}/{label}/t{t}/row{i}: measured {measured:F2} px vs mirror {predicted:F2} px"; }
                                if (i == 0) rootMove = Mathf.Max(rootMove, Mathf.Abs(measured));
                            }
                            // no data = static: a mesh without colours, and one with white colours (B = 1), under the same wind
                            goPlain.SetActive(true); go.SetActive(false);
                            var pPlain = rig.Render(); goPlain.SetActive(false);
                            goWhite.SetActive(true);
                            var pWhite = rig.Render(); goWhite.SetActive(false); go.SetActive(true);
                            GolfWindSway.ForcedWind = null; GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
                            goPlain.SetActive(true); go.SetActive(false); var pRest = rig.Render(); goPlain.SetActive(false); go.SetActive(true);
                            noDataMove = Mathf.Max(noDataMove, MaxCentroidShift(pPlain, pRest, c.position.y, rows, yScale));
                            goWhite.SetActive(true); go.SetActive(false); var pRestW = rig.Render(); goWhite.SetActive(false); go.SetActive(true);
                            whiteMove = Mathf.Max(whiteMove, MaxCentroidShift(pWhite, pRestW, c.position.y, rows, yScale));
                        }
                }
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(goPlain); UnityEngine.Object.DestroyImmediate(goWhite);
            }
            Gate("SWAY_MIRROR_MATCHES_SHADER", samples > 300 && worstPx <= 1.2f, $"{samples} row samples (9 rows x {winds.Length} winds x {times.Length} clocks x 2 camera axes x 2 instance poses): the GPU strip silhouette vs GolfWindSway.Displacement, worst error {worstPx:F2} px = {worstYd:F4} yd (limit 1.2 px = {1.2f * YdPerPx:F4} yd; {YdPerPx * 1000:F2} mm of a yard per px), worst case: {worstCase}");
            Gate("SWAY_ROOT_PINNED", rootMove <= .35f, $"the weight-0 root row moved at most {rootMove:F2} px = {rootMove * YdPerPx:F4} yd over all {samples / rows} wind / clock / pose cases (limit .35 px: nothing stretches at the base)");
            Gate("SWAY_NO_COLOURS_STATIC", noDataMove <= .35f && whiteMove <= .35f, $"a plant mesh WITHOUT colours moved at most {noDataMove:F2} px and one with white colours (B = 1) {whiteMove:F2} px under 20 mph (limit .35 px): meshes without sway data stay static");
            // direction on the GPU: the strip tip leans toward the wind for four headings (mean over six clocks, strong wind), via the same renders' sign
            GpuDirection(rig, mesh);
        }

        static float MaxCentroidShift(Color32[] a, Color32[] b, float camY, int rows, float yScale)
        {
            var ca = Centroids(a, camY, rows, yScale); var cb = Centroids(b, camY, rows, yScale); float m = 0;
            for (int i = 0; i < rows; i++) { if (float.IsNaN(ca[i]) || float.IsNaN(cb[i])) return 999; m = Mathf.Max(m, Mathf.Abs(ca[i] - cb[i])); }
            return m;
        }

        /// SWAY_WIND_DRIVEN, GPU half: for headings 90 / 270 (x view) and 0 / 180 (z view) the time-mean lean of the strip tip points along the wind, measured from the frames.
        static void GpuDirection(StripRig rig, Mesh mesh)
        {
            const int rows = 9;
            var go = new GameObject("dir strip", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(rig.Root.transform);
            go.transform.position = new Vector3(9.4f, 0, 21.7f); go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = rig.Mat;
            var problems = new List<string>(); var lines = new List<string>();
            foreach (double deg in new[] { 90.0, 270.0, 0.0, 180.0 })
            {
                var wd = GolfWindSway.Direction(deg);
                string axis = Mathf.Abs(wd.x) > .5f ? "x" : "z";
                go.transform.rotation = axis == "x" ? Quaternion.identity : Quaternion.Euler(0, 90, 0);
                var c = rig.Cam.transform;
                c.position = axis == "x" ? new Vector3(9.4f, .5f, 21.7f + 6) : new Vector3(9.4f + 6, .5f, 21.7f);
                c.rotation = axis == "x" ? Quaternion.LookRotation(Vector3.back, Vector3.up) : Quaternion.LookRotation(Vector3.left, Vector3.up);
                GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
                var rest = Centroids(rig.Render(), .5f, rows, 1f);
                double sum = 0; int n = 0;
                for (float t = 0; t < 30; t += 1.7f)
                {
                    GolfWindSway.ForcedWind = new Wind(20, deg); GolfWindSway.FreezeTime = t; GolfWindSway.Push();
                    var cent = Centroids(rig.Render(), .5f, rows, 1f);
                    sum += cent[rows - 1] - rest[rows - 1]; n++;
                }
                float meanPx = (float)(sum / n);
                // expected sign of the mean tip shift in image px: dot(windDir, camera right)
                float expect = wd.x * c.right.x + wd.y * c.right.z;
                bool ok = expect * meanPx > 0 && Mathf.Abs(meanPx) * YdPerPx > .004f;
                lines.Add($"toward {deg:F0}: mean tip lean {meanPx:+0.0;-0.0} px ({meanPx * YdPerPx:+0.000;-0.000} yd), wind along the image axis {(expect > 0 ? "+" : "-")}");
                if (!ok) problems.Add($"toward {deg:F0}: mean tip lean {meanPx:F2} px does not follow the wind (expected sign {Math.Sign(expect)})");
            }
            UnityEngine.Object.DestroyImmediate(go);
            GolfWindSway.ForcedWind = null; GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
            Gate("SWAY_WIND_DRIVEN_GPU", problems.Count == 0, problems.Count == 0 ? "20 mph on the GPU, tip of a weight-1 strip averaged over 18 clocks: " + string.Join("; ", lines.ToArray()) : string.Join("; ", problems.ToArray()));
        }

        // ------------------------------------------------------------------ the ShadowCaster pass applies the SAME sway (a shadow that stood still under a moving plant would show)

        /// A weight-graded strip (renderer set to ShadowsOnly) casts its shadow on a ground quad from a slanted sun; a top-down ortho camera sees only the shadow. Moving the tip by d moves the shadow's far end by the same
        /// horizontal d (a horizontal shift of a point shifts its projection onto the ground by exactly that): measured against the mirror's tip displacement.
        static void ShadowFollowsPlant()
        {
            using var rig = new StripRig();
            var cam = rig.Cam; cam.orthographicSize = 1.5f; cam.transform.position = new Vector3(0, 6, 0); cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            var sun = new GameObject("rig sun").AddComponent<Light>(); sun.transform.SetParent(rig.Root.transform);
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 1f; sun.color = Color.white;
            sun.transform.rotation = Quaternion.Euler(52, 40, 0); RenderSettings.sun = sun; RenderSettings.ambientLight = new Color(.25f, .25f, .25f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>()); ground.transform.SetParent(rig.Root.transform);
            ground.transform.position = Vector3.zero; ground.transform.rotation = Quaternion.Euler(90, 0, 0); ground.transform.localScale = new Vector3(8, 8, 1);
            var lit = new Material(Shader.Find("Universal Render Pipeline/Lit")); lit.SetColor("_BaseColor", Color.white); lit.SetFloat("_Smoothness", 0f); lit.SetFloat("_Metallic", 0f); ground.GetComponent<MeshRenderer>().sharedMaterial = lit;
            var mesh = Strip(9, .37f, "sway"); var col = mesh.colors32;
            var go = new GameObject("shadow strip", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(rig.Root.transform);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = rig.Mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            var pivot = new Vector3(.3f, 0, -.2f); go.transform.position = pivot;
            Vector2 lf = new Vector2(Mathf.Sin(40 * Mathf.Deg2Rad), Mathf.Cos(40 * Mathf.Deg2Rad));            // the sun's horizontal heading = the way the shadow points
            float pxToYd = 2 * cam.orthographicSize / N;
            Vector2 ShadowTip(Color32[] px)
            {
                double med = 0; var lums = new List<float>(); for (int i = 0; i < px.Length; i += 7) lums.Add(px[i].g); lums.Sort(); med = lums[lums.Count / 2];
                var pts = new List<(float s, float x, float y)>();
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) if (px[y * N + x].g < med * .55f) pts.Add((((x - N / 2f) * lf.x + (y - N / 2f) * lf.y), x, y));
                if (pts.Count < 40) return new Vector2(float.NaN, float.NaN);
                pts.Sort((a, b) => b.s.CompareTo(a.s)); int k = Mathf.Max(8, pts.Count / 20); double sx = 0, sy = 0; for (int i = 0; i < k; i++) { sx += pts[i].x; sy += pts[i].y; }
                return new Vector2((float)(sx / k), (float)(sy / k));
            }
            rig.Render(); rig.Render();
            GolfWindSway.FreezeTime = null; GolfWindSway.ForcedWind = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
            var restPx = rig.Render(); rig.SaveLast("shadow_rest"); var rest = ShadowTip(restPx);
            float worst = 0; int n = 0, lost = 0; string at = ""; float biggest = 0; double sxx = 0, sxy = 0, syy = 0;
            foreach (var (mph, deg) in new[] { (20.0, 90.0), (20.0, 200.0), (12.0, 320.0) })
                foreach (float t in new[] { .6f, 3.1f, 8.4f })
                {
                    var wind = new Wind(mph, deg); GolfWindSway.ForcedWind = wind; GolfWindSway.FreezeTime = t; GolfWindSway.Push();
                    var px = rig.Render(); if (mph == 20 && deg == 90.0 && t == 3.1f) rig.SaveLast("shadow_wind");
                    var tip = ShadowTip(px); var g = GolfWindSway.GlobalValue(wind, t);
                    var d = NegScale * GolfWindSway.Displacement(col[col.Length - 1].r / 255f, col[col.Length - 1].g / 255f, pivot.x, pivot.z, new Vector2(g.x, g.z), g.w);       // the top row
                    n++;
                    if (float.IsNaN(tip.x) || float.IsNaN(rest.x)) { lost++; continue; }
                    // image x = world x, image y = world z (camera up = +z); the mesh's tip projects along the sun: a horizontal displacement d moves the shadow's far end by d
                    var measured = (tip - rest) * pxToYd; var err = (measured - d).magnitude; biggest = Mathf.Max(biggest, d.magnitude);
                    sxx += d.x * d.x + d.y * d.y; sxy += d.x * measured.x + d.y * measured.y; syy += measured.x * measured.x + measured.y * measured.y;
                    if (err > worst) { worst = err; at = $"{mph:F0} mph toward {deg:F0} t{t}: shadow tip moved ({measured.x:+0.000;-0.000}, {measured.y:+0.000;-0.000}) yd vs the plant tip ({d.x:+0.000;-0.000}, {d.y:+0.000;-0.000})"; }
                }
            UnityEngine.Object.DestroyImmediate(lit);
            GolfWindSway.ForcedWind = null; GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
            // the shadow map (2048 px over a 42 yd cascade, soft shadows) rounds the far end of a 6 cm wide shadow by about a centimetre, so the proof is the fit of the measured shadow-tip movement to the plant's tip
            // displacement (slope through the origin and correlation over the 9 cases), plus the worst single difference
            double slope = sxx > 0 ? sxy / sxx : 0, corr = sxx > 0 && syy > 0 ? sxy / System.Math.Sqrt(sxx * syy) : 0;
            Gate("SWAY_SHADOW_FOLLOWS_PLANT", n == 9 && lost == 0 && slope > .7 && slope < 1.3 && corr > .93 && worst <= .016f && biggest > .03f, $"the ShadowCaster pass applies the same sway: the far end of the strip's shadow on a ground quad (sun 52 deg down, ShadowsOnly strip, top-down camera) moves with the plant tip over 9 wind / clock cases ({lost} lost): slope {slope:F2} (want .7-1.3), correlation {corr:F3} (want > .93), worst single difference {worst:F4} yd (limit .016 = 4 px of the shadow map); largest plant displacement {biggest:F3} yd; worst: {at}");
        }

        // ------------------------------------------------------------------ Blender -> FBX -> Unity importer -> shader (the whole chain on a fixture the libs' exporter options wrote)

        static void FbxChain()
        {
            const string path = "Assets/Editor/GolfPlantSwayFixture/sway_fixture.fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var amf = asset ? asset.GetComponentInChildren<MeshFilter>() : null;
            if (!amf || !amf.sharedMesh) { Gate("SWAY_FBX_COLOURS_REACH_UNITY", false, path + " missing or without a mesh (v2/sway/blender/make_fixture.py writes it)"); return; }
            var mesh = amf.sharedMesh; var v = mesh.vertices; var col = mesh.colors32;
            if (col == null || col.Length != v.Length) { Gate("SWAY_FBX_COLOURS_REACH_UNITY", false, $"the imported fixture mesh has {(col == null ? 0 : col.Length)} colours for {v.Length} vertices"); return; }
            // the FBX node carries the axis conversion (Blender Z-up -> Y-up), so a mesh-space strip may stand along Z: the row axis is the longest one
            var lo3 = new Vector3(v.Min(p => p.x), v.Min(p => p.y), v.Min(p => p.z)); var hi3 = new Vector3(v.Max(p => p.x), v.Max(p => p.y), v.Max(p => p.z)); var ext = hi3 - lo3;
            int ax = ext.x >= ext.y && ext.x >= ext.z ? 0 : ext.y >= ext.z ? 1 : 2; float lo = lo3[ax], h = ext[ax];
            int worstR = 0, worstG = 0, maxB = 0, minA = 255;
            var rw = new float[9]; var rg = new float[9];
            for (int i = 0; i < v.Length; i++)
            {
                int row = Mathf.Clamp(Mathf.RoundToInt((v[i][ax] - lo) / h * 8f), 0, 8);
                worstR = Mathf.Max(worstR, Mathf.Abs(col[i].r - Mathf.RoundToInt(row / 8f * 255f)));
                worstG = Mathf.Max(worstG, Mathf.Abs(col[i].g - Mathf.RoundToInt(.37f * 255f)));
                maxB = Mathf.Max(maxB, col[i].b); minA = Mathf.Min(minA, col[i].a);
                rw[row] = col[i].r / 255f; rg[row] = col[i].g / 255f;
            }
            bool ok = v.Length == 18 && h > 1e-4f && worstR <= 1 && worstG <= 1 && maxB == 0 && minA == 255;
            Gate("SWAY_FBX_COLOURS_REACH_UNITY", ok, $"fixture written with the libs' exporter options (colors_type LINEAR): Unity imported {v.Length} vertices (18 written) with RGBA = (row/8, .37, 0, 1) as authored: max |R error| {worstR}, |G| {worstG} (of 255), B <= {maxB}, A >= {minA} (raw values, no sRGB conversion); strip along mesh axis {"xyz"[ax]}, length {h:F3} mesh units, node {amf.transform.localEulerAngles} scale {amf.transform.lossyScale}");
            // and through the shader, THE WAY A HOLE DOES IT: the whole imported hierarchy instantiated (the node's axis conversion and scale apply), measured against the mirror, 3 winds x 3 clocks
            using var rig = new StripRig();
            var inst = UnityEngine.Object.Instantiate(asset, rig.Root.transform);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterial = rig.Mat; r.enabled = true; }
            var bounds = inst.GetComponentsInChildren<Renderer>(true)[0].bounds; foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
            float worldH = bounds.size.y;
            inst.transform.localScale *= 1f / Mathf.Max(worldH, 1e-4f);                      // 1 yd tall in the world
            inst.transform.position += new Vector3(-7.77f, 0, 33.21f) - inst.GetComponentsInChildren<Renderer>(true)[0].bounds.center + Vector3.up * .5f * 0;
            var bb = inst.GetComponentsInChildren<Renderer>(true)[0].bounds; foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) bb.Encapsulate(r.bounds);
            inst.transform.position += new Vector3(0, -bb.min.y, 0);                          // the strip's bottom row on y = 0
            var imf = inst.GetComponentInChildren<MeshFilter>(); var pivot = imf.transform.position;      // the object's pivot (the shader hashes it)
            var c = rig.Cam.transform; c.position = new Vector3(pivot.x, .5f, pivot.z + 6); c.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            rig.Render(); rig.Render();
            GolfWindSway.FreezeTime = null; GolfWindSway.ForcedWind = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
            var rest = Centroids(rig.Render(), .5f, 9, 1f);
            // which end of the world strip is row 0 (weight 0)? the vertex with the lowest weight, transformed by the node
            var l2w = imf.transform.localToWorldMatrix; float yRow0 = float.MaxValue, yRow8 = float.MinValue;
            for (int i = 0; i < v.Length; i++) { float wy = l2w.MultiplyPoint3x4(v[i]).y; if (col[i].r == 0) yRow0 = Mathf.Min(yRow0, wy); if (col[i].r == 255) yRow8 = Mathf.Max(yRow8, wy); }
            bool rootAtBottom = yRow0 < yRow8;
            float worst = 0; int n = 0, lost = 0; string at = "";
            foreach (var (mph, deg) in new[] { (20.0, 90.0), (12.0, 300.0), (6.0, 35.0) })
                foreach (float t in new[] { .4f, 2.6f, 9.1f })
                {
                    var wind = new Wind(mph, deg); GolfWindSway.ForcedWind = wind; GolfWindSway.FreezeTime = t; GolfWindSway.Push();
                    var cent = Centroids(rig.Render(), .5f, 9, 1f); var g = GolfWindSway.GlobalValue(wind, t);
                    for (int i = 0; i < 9; i++)
                    {
                        int row = rootAtBottom ? i : 8 - i;                                    // image row i (from the bottom) is data row `row`
                        var d = GolfWindSway.Displacement(rw[row], rg[row], pivot.x, pivot.z, new Vector2(g.x, g.z), g.w);
                        float predicted = NegScale * (d.x * c.right.x + d.y * c.right.z) / YdPerPx; n++;
                        if (float.IsNaN(cent[i]) || float.IsNaN(rest[i])) { lost++; worst = 999; at = $"strip lost at image row {i}"; continue; }
                        float e = Mathf.Abs(cent[i] - rest[i] - predicted);
                        if (e > worst && worst < 999) { worst = e; at = $"{mph:F0} mph toward {deg:F0} t{t} row{i}: measured {cent[i] - rest[i]:F2} px vs mirror {predicted:F2} px"; }
                    }
                }
            UnityEngine.Object.DestroyImmediate(inst);
            GolfWindSway.ForcedWind = null; GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
            Gate("SWAY_FBX_CHAIN_ON_GPU", n == 81 && lost == 0 && worst <= 1.2f, $"the Blender-exported, Unity-imported fixture (its node's axis conversion applied, root {(rootAtBottom ? "at the bottom" : "at the top")}) through GolfPlants vs the mirror: {n} row samples, {lost} lost, worst error {worst:F2} px = {worst * YdPerPx:F4} yd (limit 1.2 px), worst case {at}");
        }

        // ------------------------------------------------------------------ only plants move

        static void OnlyPlantsRig()
        {
            using var rig = new StripRig();
            var names = GolfLook.Table.Keys.Where(k => k != "LK_PLANTS" && GolfLook.Table[k].Kind != GolfLook.Kind.Water).OrderBy(k => k).ToArray();   // the sea is the tennis shader's (TennisWater, its own swell, frozen file)
            rig.Cam.orthographicSize = 2f; rig.Cam.transform.position = new Vector3(0, 0, -6); rig.Cam.transform.rotation = Quaternion.identity;
            int cols = 4, rowsN = (names.Length + cols - 1) / cols;
            for (int i = 0; i < names.Length; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(rig.Root.transform);
                q.transform.position = new Vector3((i % cols - (cols - 1) / 2f) * 1.9f, ((rowsN - 1) / 2f - i / cols) * 1.2f, 0); q.transform.localScale = new Vector3(1.8f, 1.1f, 1);
                q.GetComponent<MeshRenderer>().sharedMaterial = GolfLook.Get(names[i]);
            }
            GolfWindSway.ForcedWind = null; GolfWindSway.FreezeTime = null; Shader.SetGlobalVector("_GolfWind", Vector4.zero);
            var a = rig.Render(); rig.SaveLast("only_plants_rest");
            var diffs = new List<string>(); int maxDiff = 0; int bright = 0;
            foreach (var (mph, deg, t) in new[] { (20.0, 90.0, 1.9f), (20.0, 0.0, 7.3f), (6.0, 200.0, 333.3f) })
            {
                GolfWindSway.ForcedWind = new Wind(mph, deg); GolfWindSway.FreezeTime = t; GolfWindSway.Push();
                var b = rig.Render();
                int d = 0, n = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    int e = Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                    if (e > 0) n++; d = Mathf.Max(d, e);
                }
                maxDiff = Mathf.Max(maxDiff, d); if (n > 0) diffs.Add($"{mph:F0} mph toward {deg:F0} at t={t}: {n} px differ");
            }
            foreach (var p in a) if (p.r + p.g + p.b > 60) bright++;
            Gate("SWAY_ONLY_PLANTS_RIG", maxDiff == 0 && bright > a.Length / 4, $"{names.Length} other LK_ materials ({string.Join(",", names.Take(6).ToArray())}, ...) in one frame, rendered under 3 winds / clocks: max |delta| {maxDiff} levels, {(diffs.Count == 0 ? "0 differing pixels" : string.Join("; ", diffs.ToArray()))}; {bright * 100.0 / a.Length:F0} % of the frame is lit material (not a black frame); TennisWater (the sea) is the tennis shader and is not in the rig");
            GolfWindSway.ForcedWind = null; GolfWindSway.FreezeTime = null;
        }
    }
}
