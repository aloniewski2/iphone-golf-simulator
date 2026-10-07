// Import check for the postcard holes 8 Needle, 9 Split, 10 Crater (Course.Postcards()) and the unchanged hole 7:
//   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.PostcardImportCheck.Run -logFile <abs log>
//   grep -E '^(GATE|INFO|WARN):|^===' <log>              # exit code 0 = every gate PASS, 1 = at least one FAIL
// Optional: POSTCARD_IMPORT_REPORT=/abs/path.txt also writes every GATE/INFO line to that file.
// POSTCARD_LOOK edit (2026-10-04): the palette / lava lines are LK-aware for holes 8-10 (PostcardLookMaterialRules below), two lines prove the
// Game-view rule "albedo AND normal" (GROUND_ALBEDO_AND_NORMAL, NO_FLAT_POSTCARD_GROUND) and HOLE7_MATERIALS_FLAT_AS_BEFORE pins hole 7 to the old
// flat rule. The ground-height / lie / marker / collider gates are untouched.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.EditorTools
{
    /// Import check for Course.Postcards(): builds each hole's HoleView exactly as GolfGame does (HoleView.Build) in the editor and
    /// asserts what the game relies on, hole by hole:
    ///   (a) the FBX loads (Resources/Course/hole_NN) and HoleView logs none of "Course model needs MARKER_*", "pin is N yd off", "up is N deg off";
    ///   (b) ~3000 jittered points: land lies read a ground height inside the play band (6 m = 6.56 yd, up to +0.7 yd), Water reads 0
    ///       (points whose lie differs from any of 8 neighbours 1.5 yd away are skipped);
    ///   (c) the model's tee/pin markers land on ToWorld(Tee)/ToWorld(Pin) with X right and Z down the hole (no mirroring: bunker and water
    ///       centres are probed too, because markers alone cannot tell a mirrored model);
    ///   (d) the number of MeshColliders, which renderers got the HoleView.Palette override, and the colour of the hole-10 WATER_LAVA material;
    ///   (e) no exceptions.
    /// Everything it prints starts with GATE:, INFO:, WARN: or === so a log can be grepped. Exit code 0 when every gate passes, else 1.
    public static class PostcardImportCheck
    {
        const double YardsPerMetre = 1.0936;
        /// Every postcard hole is one play height: 6 m above the sea (blender/scripts/hole0N_design.py PLAY_Z).
        const double PlayMetres = 6.0;
        const double BandLow = PlayMetres * YardsPerMetre - 0.05;     // terrain top; 5 cm of float slack
        const double BandHigh = PlayMetres * YardsPerMetre + 0.7;     // bunker lip and green/tee offsets stay within +0.6 m
        const int SamplesPerHole = 3000;
        const double NeighbourYards = 1.5;
        const double WaterTolerance = 0.01;                           // "GroundHeight == 0" (a ray that misses returns exactly 0)
        const int MinLandSamples = 200, MinWaterSamples = 300;        // so a hole cannot pass by sampling nothing (Crater keeps ~320 land points: its island is small)

        /// Same prefixes as HoleView.GroundPrefixes: these meshes become MeshColliders, everything else is scenery.
        static readonly string[] GroundPrefixes = { "TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH" };
        /// HoleView hides the exact names; any other visible object with one of these prefixes would double the game's own flag/cup/markers.
        static readonly string[] PlaceholderPrefixes = { "FLAG", "HOLE_CUP", "BALL_START", "MARKER_" };
        static readonly string[] ImportWarnings = { "Course model needs MARKER", "Course model pin is", "Course model up is" };

        struct LogEntry { public string Text; public LogType Type; }

        static readonly List<string> report = new List<string>();
        static readonly List<LogEntry> logs = new List<LogEntry>();
        static int gateCount, failCount, caught;

        public static void Run()
        {
            report.Clear(); logs.Clear(); gateCount = 0; failCount = 0; caught = 0;
            Application.logMessageReceived += OnLog;
            try { RunChecks(); }
            catch (System.Exception e)
            {
                caught++;
                Debug.LogException(e);
                Gate("NO_EXCEPTIONS", false, "unhandled " + e.GetType().Name + ": " + e.Message);
            }
            finally { Application.logMessageReceived -= OnLog; }

            Say($"GATE: POSTCARD_IMPORT_CHECK {(failCount == 0 ? "PASS" : "FAIL")} - {gateCount} gate lines, {failCount} failed");
            WriteReport();
            if (Application.isBatchMode) EditorApplication.Exit(failCount == 0 ? 0 : 1);
        }

        static void OnLog(string condition, string stackTrace, LogType type) { logs.Add(new LogEntry { Text = condition, Type = type }); }

        static void Say(string line) { report.Add(line); Debug.Log(line); }

        static void Gate(string name, bool ok, string detail)
        {
            gateCount++;
            if (!ok) failCount++;
            Say($"GATE: {name} {(ok ? "PASS" : "FAIL")} - {detail}");
        }

        static void WriteReport()
        {
            string path = System.Environment.GetEnvironmentVariable("POSTCARD_IMPORT_REPORT");
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllLines(Path.GetFullPath(path), report.ToArray()); }
            catch (System.Exception e) { Debug.LogWarning("WARN: could not write the report: " + e.Message); }
        }

        static void RunChecks()
        {
            AssetDatabase.Refresh();   // new FBX files are imported before Resources.Load looks for them
            var course = GolfArcade.Course.Course.Postcards();
            var pipeline = Pipeline();
            Say($"INFO: course '{course.Name}', {course.Holes.Length} hole(s), Unity {Application.unityVersion}, render pipeline {(pipeline != null ? pipeline.GetType().Name : "built-in")}");

            var paletteField = typeof(HoleView).GetField("Palette", BindingFlags.NonPublic | BindingFlags.Static);
            var palette = paletteField != null ? paletteField.GetValue(null) as Dictionary<string, Color> : null;
            Gate("PALETTE_READABLE", palette != null && palette.Count > 0, palette != null ? $"HoleView.Palette has {palette.Count} entries (MAT_LAVA {(palette.ContainsKey("MAT_LAVA") ? "is" : "is not")} one of them)" : "HoleView.Palette not found by reflection: HoleView changed, update this check");

            foreach (var hole in course.Holes) CheckHole(hole, palette);
            CheckHole7Unchanged(palette);

            Say("=== whole run ===");
            int destroyNotices = logs.Count(l => IsEditModeDestroyNotice(l.Text));
            if (destroyNotices > 0) Say($"INFO: {destroyNotices} 'Destroy may not be called from edit mode' notice(s) from HoleView.Primitive (it uses Destroy; harmless here, the pin primitives' colliders are removed with DestroyImmediate before sampling)");
            int exceptionLogs = logs.Count(l => l.Type == LogType.Exception);
            Gate("NO_EXCEPTIONS", exceptionLogs == 0 && caught == 0, $"{exceptionLogs} exception log(s), {caught} caught while building or checking");
            var errors = logs.Where(l => (l.Type == LogType.Error || l.Type == LogType.Assert) && !IsEditModeDestroyNotice(l.Text)).Select(l => l.Text).ToList();
            Gate("NO_ERROR_LOGS", errors.Count == 0, errors.Count == 0 ? "no error logs apart from the edit-mode Destroy notices" : errors.Count + " error log(s), first: " + Short(errors[0]));
        }

        /// HoleView.Primitive calls Destroy, which edit mode refuses with an error log ("Destroy may not be called from edit mode! Use DestroyImmediate instead.").
        static bool IsEditModeDestroyNotice(string text)
        {
            return text != null && (text.Contains("Destroy may not be called from edit mode") || text.Contains("Use DestroyImmediate instead"));
        }

        static UnityEngine.Rendering.RenderPipelineAsset Pipeline()
        {
            return QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        }

        // ------------------------------------------------------------------ one hole

        static void CheckHole(Hole hole, Dictionary<string, Color> palette)
        {
            Say($"=== hole {hole.Number} (par {hole.Par}) ===");
            string path = $"Course/hole_{hole.Number:00}";
            var prefab = Resources.Load<GameObject>(path);
            // POSTCARD_IMPORT_ASSETS=<Assets/...folder holding hole_NN.fbx + .meta>: PREVIEW mode (area U, 2026-10-04) - every gate below runs on the STAGED FBX of that folder instead of the installed
            // Resources/Course one (the model is swapped through HoleView.BuildFromModel, the same path the real load takes), so the LK-aware lines can be proven on a rebuilt hole BEFORE it is installed.
            string previewDir = System.Environment.GetEnvironmentVariable("POSTCARD_IMPORT_ASSETS");
            bool preview = false;
            if (!string.IsNullOrEmpty(previewDir))
            {
                var staged = AssetDatabase.LoadAssetAtPath<GameObject>($"{previewDir}/hole_{hole.Number:00}.fbx");
                if (staged != null) { prefab = staged; preview = true; Say($"INFO: hole {hole.Number}: PREVIEW mode - checking the STAGED FBX {previewDir}/hole_{hole.Number:00}.fbx, not the installed {path}"); }
                else Say($"INFO: hole {hole.Number}: POSTCARD_IMPORT_ASSETS={previewDir} has no hole_{hole.Number:00}.fbx: checking the installed {path}");
            }
            Gate("FBX_LOADED", prefab != null, prefab != null
                ? (preview ? $"STAGED {AssetDatabase.GetAssetPath(prefab)} -> '{prefab.name}'" : $"Resources.Load<GameObject>(\"{path}\") -> '{prefab.name}'")
                : $"Resources.Load<GameObject>(\"{path}\") is null: HoleView would draw primitives and every ground height would read 0");
            if (prefab == null) return;

            GameObject parent = null;
            int logStart = logs.Count;
            try
            {
                parent = new GameObject("PostcardImportCheck hole " + hole.Number);
                var view = HoleView.Build(hole, parent.transform);   // what GolfGame does for the hole on screen
                var model = view.transform.Find("Course model");     // HoleView names the instantiated FBX this
                if (preview)
                {
                    if (model != null) Object.DestroyImmediate(model.gameObject);
                    typeof(HoleView).GetMethod("BuildFromModel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, new object[] { prefab });
                    model = view.transform.Find("Course model");
                }

                // (a) HoleView's own import diagnostics
                var bad = logs.Skip(logStart).Where(l => ImportWarnings.Any(w => l.Text != null && l.Text.Contains(w))).Select(l => l.Text).ToList();
                Gate("MODEL_LOGS_CLEAN", bad.Count == 0 && model != null, bad.Count == 0
                    ? (model != null ? "none of 'Course model needs MARKER_*', 'pin is N yd off', 'up is N deg off' was logged" : "HoleView has no 'Course model' child")
                    : string.Join(" | ", bad.ToArray()));
                if (model == null) return;

                // HoleView.Primitive uses Destroy, which edit mode refuses; the pin's flagstick/flag colliders would then
                // hide the ground from GroundHeight. The game removes them at the end of the frame, so remove them now.
                foreach (var c in view.GetComponentsInChildren<Collider>(true))
                    if (c != null && !(c is MeshCollider)) Object.DestroyImmediate(c);
                Physics.SyncTransforms();

                CheckColliders(prefab, view);
                CheckMarkers(hole, model);
                CheckGround(hole);
                CheckMaterials(hole, prefab, model, palette);
                CheckPlaceholders(model);
            }
            catch (System.Exception e)
            {
                caught++;
                Debug.LogException(e);
                Gate("HOLE_BUILD_NO_EXCEPTION", false, "hole " + hole.Number + ": " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                if (parent != null) Object.DestroyImmediate(parent);   // the next hole must not see this hole's colliders
                Physics.SyncTransforms();
            }
        }

        // ------------------------------------------------------------------ (d) colliders

        static void CheckColliders(GameObject prefab, HoleView view)
        {
            var colliders = view.GetComponentsInChildren<MeshCollider>(true);
            int expected = prefab.GetComponentsInChildren<MeshFilter>(true).Count(mf => mf.sharedMesh != null && StartsWithAny(mf.name, GroundPrefixes));
            var names = colliders.Select(c => c.name).OrderBy(n => n).ToArray();
            var notGround = names.Where(n => !StartsWithAny(n, GroundPrefixes)).ToArray();
            Gate("COLLIDERS_ON_GROUND_MESHES", colliders.Length == expected && expected > 0 && notGround.Length == 0,
                $"{colliders.Length} MeshCollider(s) created, {expected} ground-prefixed mesh(es) in the FBX ({string.Join(", ", GroundPrefixes)}); " +
                $"on: {Preview(names, 14)}");
        }

        // ------------------------------------------------------------------ (c) markers and orientation

        static void CheckMarkers(Hole hole, Transform model)
        {
            var tee = FindDeep(model, "MARKER_TEE");
            var pin = FindDeep(model, "MARKER_PIN");
            var up = FindDeep(model, "MARKER_UP");
            if (tee == null || pin == null || up == null)
            {
                Gate("TEE_PIN_WORLD", false, $"markers missing in the FBX (MARKER_TEE {(tee != null)}, MARKER_PIN {(pin != null)}, MARKER_UP {(up != null)})");
                return;
            }
            var teeW = HoleView.ToWorld(hole.Tee);
            var pinW = HoleView.ToWorld(hole.Pin);
            // ToWorld is the identity on X and D: world X is course X and world Z is course D (X right, Z down the hole)
            bool wiring = Mathf.Abs(teeW.x - (float)hole.Tee.X) < 1e-3f && Mathf.Abs(teeW.z - (float)hole.Tee.D) < 1e-3f
                       && Mathf.Abs(pinW.x - (float)hole.Pin.X) < 1e-3f && Mathf.Abs(pinW.z - (float)hole.Pin.D) < 1e-3f;
            float teeResidual = new Vector2(tee.position.x - teeW.x, tee.position.z - teeW.z).magnitude;
            float pinResidual = new Vector2(pin.position.x - pinW.x, pin.position.z - pinW.z).magnitude;
            // no mirror, no flip: the model's tee->pin run has the same sign as the course's on both axes
            double cdx = hole.Pin.X - hole.Tee.X, cdz = hole.Pin.D - hole.Tee.D;
            float mdx = pin.position.x - tee.position.x, mdz = pin.position.z - tee.position.z;
            bool orientation = (System.Math.Abs(cdx) < 1 || cdx * mdx > 0) && (System.Math.Abs(cdz) < 1 || cdz * mdz > 0);
            bool ok = wiring && teeResidual <= 0.05f && pinResidual <= 0.5f && orientation;
            Gate("TEE_PIN_WORLD", ok,
                $"tee marker ({tee.position.x:F2}, {tee.position.z:F2}) vs ToWorld(Tee) ({teeW.x:F2}, {teeW.z:F2}) off {teeResidual:F3} yd (<= 0.05); " +
                $"pin marker ({pin.position.x:F2}, {pin.position.z:F2}) vs ToWorld(Pin) ({pinW.x:F2}, {pinW.z:F2}) off {pinResidual:F3} yd (<= 0.5, the game's own warning limit); " +
                $"tee->pin model ({mdx:F1}, {mdz:F1}) vs course ({cdx:F1}, {cdz:F1}) same direction {orientation}; ToWorld keeps X and D {wiring}");

            var rise = up.position - tee.position;
            float tilt = Vector3.Angle(rise, Vector3.up);
            float expectedRise = (float)(50.0 * YardsPerMetre);       // MARKER_UP sits 50 m straight above the tee marker
            Gate("MARKER_UP_50M", tilt <= 1f && Mathf.Abs(rise.magnitude - expectedRise) <= 1f,
                $"MARKER_UP is {rise.magnitude:F2} yd above the tee marker (want {expectedRise:F2} = 50 m), {tilt:F2} deg off vertical (<= 1)");
        }

        // ------------------------------------------------------------------ (b) ground heights

        static void CheckGround(Hole hole)
        {
            var points = new List<CoursePoint>();
            double minX, maxX, minD, maxD;
            if (hole.Shore != null && hole.Shore.Length > 0)
            {
                minX = hole.Shore.Min(p => p.X); maxX = hole.Shore.Max(p => p.X);
                minD = hole.Shore.Min(p => p.D); maxD = hole.Shore.Max(p => p.D);
            }
            else
            {
                minX = hole.Centerline.Min(p => p.X) - 60; maxX = hole.Centerline.Max(p => p.X) + 60;
                minD = hole.Centerline.Min(p => p.D) - 60; maxD = hole.Centerline.Max(p => p.D) + 60;
            }
            minX -= 8; maxX += 8; minD -= 8; maxD += 8;                // some open sea around the island
            double width = maxX - minX, depth = maxD - minD;
            double cell = System.Math.Sqrt(width * depth / SamplesPerHole);
            int nx = (int)System.Math.Ceiling(width / cell), nz = (int)System.Math.Ceiling(depth / cell);
            var rng = new System.Random(20261003 + hole.Number);       // jittered grid, deterministic
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                    points.Add(new CoursePoint(minX + (ix + rng.NextDouble()) * cell, minD + (iz + rng.NextDouble()) * cell));
            int jittered = points.Count;
            points.Add(hole.Tee); points.Add(hole.Pin);                // fixed probes: tee, pin and every hazard centre
            foreach (var z in hole.Hazards) points.Add(new CoursePoint(z.X, z.Distance));

            int land = 0, water = 0, skipped = 0;
            double lowest = double.MaxValue, highest = double.MinValue;
            var lieCounts = new Dictionary<CourseLie, int>();
            var topByLie = new Dictionary<string, int>();
            var landBad = new List<string>(); var waterBad = new List<string>();
            foreach (var p in points)
            {
                var lie = hole.LieAt(p);
                if (!Stable(hole, p, lie)) { skipped++; continue; }
                double h = HoleView.GroundHeight(p);
                int n; lieCounts.TryGetValue(lie, out n); lieCounts[lie] = n + 1;
                string top = TopColliderName(p);
                string key = lie + " <- " + (StartsWithAnyOf(top, GroundPrefixes) ?? top);
                int t; topByLie.TryGetValue(key, out t); topByLie[key] = t + 1;
                if (lie == CourseLie.Water)
                {
                    water++;
                    if (System.Math.Abs(h) > WaterTolerance && waterBad.Count < 6) waterBad.Add($"({p.X:F1}, {p.D:F1}) reads {h:F2} yd under '{top}'");
                    else if (System.Math.Abs(h) > WaterTolerance) waterBad.Add(null);
                }
                else
                {
                    land++;
                    lowest = System.Math.Min(lowest, h); highest = System.Math.Max(highest, h);
                    if ((h < BandLow || h > BandHigh) && landBad.Count < 6) landBad.Add($"{lie} ({p.X:F1}, {p.D:F1}) reads {h:F2} yd under '{top}'");
                    else if (h < BandLow || h > BandHigh) landBad.Add(null);
                }
            }
            Say($"INFO: hole {hole.Number} sampled {jittered} jittered point(s) + {points.Count - jittered} fixed probe(s): {land} land, {water} water, {skipped} skipped near a lie boundary; " +
                "lies " + string.Join(", ", lieCounts.OrderBy(kv => kv.Key).Select(kv => kv.Key + " " + kv.Value).ToArray()));
            Say($"INFO: hole {hole.Number} topmost ground collider by lie: " + string.Join(", ", topByLie.OrderBy(kv => kv.Key).Select(kv => kv.Key + " x" + kv.Value).ToArray()));

            Gate("SAMPLE_COVERAGE", land >= MinLandSamples && water >= MinWaterSamples, $"{land} land and {water} water sample(s) kept (need >= {MinLandSamples} and >= {MinWaterSamples})");
            Gate("GROUND_HEIGHT_LAND_BAND", landBad.Count == 0 && land > 0,
                landBad.Count == 0 ? $"{land} land sample(s) read {lowest:F2}..{highest:F2} yd, band {BandLow:F2}..{BandHigh:F2} yd (6 m = {PlayMetres * YardsPerMetre:F2} yd, up to +0.7)"
                                   : $"{landBad.Count} of {land} land sample(s) outside {BandLow:F2}..{BandHigh:F2} yd, e.g. " + string.Join("; ", landBad.Where(s => s != null).ToArray()));
            Gate("GROUND_HEIGHT_WATER_ZERO", waterBad.Count == 0 && water > 0,
                waterBad.Count == 0 ? $"{water} water sample(s) read 0 (|h| <= {WaterTolerance})"
                                    : $"{waterBad.Count} of {water} water sample(s) are above the sea, e.g. " + string.Join("; ", waterBad.Where(s => s != null).ToArray()));

            // the hazard centres: bunkers sit on the BUNKER mesh at play height, water ellipses are cut down to the sea.
            // A mirrored or shifted model fails here even when its markers line up.
            var hz = new List<string>();
            foreach (var z in hole.Hazards)
            {
                var c = new CoursePoint(z.X, z.Distance);
                double h = HoleView.GroundHeight(c);
                string top = TopColliderName(c);
                if (z.Kind == HazardKind.Water) { if (System.Math.Abs(h) > WaterTolerance) hz.Add($"water ({c.X:F1}, {c.D:F1}) reads {h:F2} yd under '{top}'"); }
                else if (h < BandLow || h > BandHigh || !top.StartsWith("BUNKER")) hz.Add($"bunker ({c.X:F1}, {c.D:F1}) reads {h:F2} yd under '{top}'");
            }
            Gate("HAZARD_CENTRES_ON_MODEL", hz.Count == 0,
                hz.Count == 0 ? $"{hole.Hazards.Count(z => z.Kind == HazardKind.Water)} water centre(s) read 0, {hole.Hazards.Count(z => z.Kind == HazardKind.Bunker)} bunker centre(s) read a BUNKER mesh at play height"
                              : string.Join("; ", hz.ToArray()));
        }

        /// The lie is the same on the 8 points 1.5 yd around `p`, so the model's edges (which are not the polygon's edges to the centimetre) cannot flip it.
        static bool Stable(Hole hole, CoursePoint p, CourseLie lie)
        {
            for (int k = 0; k < 8; k++)
            {
                double a = k * System.Math.PI / 4;
                var q = new CoursePoint(p.X + NeighbourYards * System.Math.Cos(a), p.D + NeighbourYards * System.Math.Sin(a));
                if (hole.LieAt(q) != lie) return false;
            }
            return true;
        }

        /// The collider HoleView.GroundHeight's ray meets first: same ray as HoleView.GroundHeight.
        static string TopColliderName(CoursePoint p)
        {
            var from = new Vector3((float)p.X, 400, (float)p.D);
            RaycastHit hit;
            return Physics.Raycast(from, Vector3.down, out hit, 800, ~0, QueryTriggerInteraction.Ignore) ? hit.collider.name : "(miss)";
        }

        // ------------------------------------------------------------------ (d) palette override and lava

        static void CheckMaterials(Hole hole, GameObject prefab, Transform model, Dictionary<string, Color> palette)
        {
            // POSTCARD_LOOK: holes 8-10 draw LK_ materials (GolfLook), TennisWater / GolfSurf on WATER*, and an emissive textured LK_LAVA, so the
            // flat-palette rules below (kept for any non-postcard hole) no longer apply there. Same gate names, LK-aware meaning, plus the
            // albedo+normal lines. The two lines that need the rebuilt LK meshes fail on the old flat FBXs by design (requireLkMeshes).
            if (GolfLook.IsPostcard(hole.Number)) { PostcardLookMaterialRules.Check(hole.Number, prefab, model, palette, Gate, Say, true); return; }
            var proto = prefab.GetComponentsInChildren<Renderer>(true);
            var live = model.GetComponentsInChildren<Renderer>(true);
            if (palette == null || proto.Length != live.Length)
            {
                Gate("PALETTE_OVERRIDES_APPLIED", false, palette == null ? "no palette to compare with" : $"the FBX has {proto.Length} renderer(s) but its instance has {live.Length}");
                return;
            }
            var overridden = new Dictionary<string, List<string>>();   // palette material -> renderers that got the override
            var kept = new Dictionary<string, List<string>>();         // material names HoleView leaves as the FBX made them
            var notApplied = new List<string>();
            int nullSlots = 0;
            for (int i = 0; i < proto.Length; i++)
            {
                var before = proto[i].sharedMaterials;
                var after = live[i].sharedMaterials;
                for (int j = 0; j < before.Length; j++)
                {
                    if (before[j] == null) { nullSlots++; continue; }
                    string name = before[j].name.Replace(" (Instance)", "");
                    Color want;
                    if (palette.TryGetValue(name, out want))
                    {
                        AddTo(overridden, name, proto[i].name);
                        if (j >= after.Length || after[j] == null || !Near(MainColor(after[j]), want)) notApplied.Add(name + " on " + proto[i].name);
                    }
                    else AddTo(kept, name, proto[i].name);
                }
            }
            int slots = overridden.Sum(kv => kv.Value.Count);
            Say($"INFO: hole {hole.Number} Palette override on {slots} material slot(s), {live.Length} renderer(s) in the model, {nullSlots} empty slot(s): " +
                string.Join("; ", overridden.OrderBy(kv => kv.Key).Select(kv => kv.Key + " x" + kv.Value.Count + " [" + Preview(kv.Value.Distinct().ToArray(), 4) + "]").ToArray()));
            Gate("PALETTE_OVERRIDES_APPLIED", notApplied.Count == 0 && slots > 0,
                notApplied.Count == 0 ? $"{slots} slot(s) across {overridden.Count} palette material(s) carry the flat game colour" : "override missing: " + Preview(notApplied.ToArray(), 6));

            // anything that is not in the palette keeps the FBX's own material: only the crater's MAT_LAVA, on WATER_LAVA, may
            var leaks = new List<string>();
            foreach (var kv in kept)
            {
                bool lava = kv.Key == "MAT_LAVA" && hole.Number == 10 && kv.Value.All(n => n.StartsWith("WATER_LAVA"));
                if (!lava) leaks.Add(kv.Key + " on " + Preview(kv.Value.Distinct().ToArray(), 4));
            }
            Gate("PALETTE_NO_LEAKS", leaks.Count == 0,
                leaks.Count == 0 ? (kept.Count == 0 ? "every material name is in HoleView.Palette" : "every material name is in HoleView.Palette except MAT_LAVA on the crater's WATER_LAVA")
                                 : "materials HoleView does not recolour: " + string.Join("; ", leaks.ToArray()));

            var lavaRenderers = live.Where(r => r.name.StartsWith("WATER_LAVA")).ToArray();
            if (hole.Number != 10)
            {
                Gate("NO_LAVA_OUTSIDE_HOLE_10", lavaRenderers.Length == 0, lavaRenderers.Length == 0 ? "no WATER_LAVA renderer" : "WATER_LAVA found: " + Preview(lavaRenderers.Select(r => r.name).ToArray(), 4));
                return;
            }
            Gate("LAVA_RENDERER_PRESENT", lavaRenderers.Length > 0, lavaRenderers.Length > 0 ? $"{lavaRenderers.Length} WATER_LAVA renderer(s): {Preview(lavaRenderers.Select(r => r.name).ToArray(), 4)}" : "no renderer whose name starts with WATER_LAVA");
            var pipeline = Pipeline();
            var notOrange = new List<string>(); var wrongShader = new List<string>();
            foreach (var r in lavaRenderers)
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    Color c = MainColor(m);
                    string shader = m.shader != null ? m.shader.name : "(none)";
                    bool orange = c.r > 0.8f && c.g > 0.2f && c.g < 0.6f && c.b < 0.25f;
                    Color lin = c.linear;
                    Say($"INFO: hole 10 {r.name} material '{m.name}' shader '{shader}' sRGB ({c.r:F3}, {c.g:F3}, {c.b:F3}) #{ColorUtility.ToHtmlStringRGB(c)} (linear {lin.r:F3}, {lin.g:F3}, {lin.b:F3}) reads orange: {orange}");
                    if (!orange) notOrange.Add(r.name + "/" + m.name + " " + ColorUtility.ToHtmlStringRGB(c));
                    // a built-in shader (Standard) renders magenta under URP; MAT_LAVA is not in HoleView.Palette, so the FBX's material is what the game draws
                    bool srpShader = shader.StartsWith("Universal Render Pipeline/") || shader.StartsWith("Shader Graphs/") || shader.StartsWith("Sprites/");
                    if (pipeline != null && !srpShader) wrongShader.Add(r.name + "/" + m.name + " uses '" + shader + "'");
                }
            Gate("LAVA_READS_ORANGE", lavaRenderers.Length > 0 && notOrange.Count == 0,
                notOrange.Count == 0 ? "the WATER_LAVA material colour is orange (r > 0.8, 0.2 < g < 0.6, b < 0.25)" : "not orange: " + string.Join("; ", notOrange.ToArray()));
            Gate("LAVA_SHADER_RENDERS_IN_PIPELINE", wrongShader.Count == 0,
                wrongShader.Count == 0 ? "the lava material's shader is one the active render pipeline draws (or there is no SRP)" : string.Join("; ", wrongShader.ToArray()) + " (would draw magenta; add MAT_LAVA to HoleView.Palette or fix the FBX material)");
        }

        // ------------------------------------------------------------------ hole 7 keeps the old flat rule

        /// Hole 7 (Cliffside, not a postcard) goes through exactly the old HoleView rule: Palette name -> the flat game colour, every other material
        /// as imported; no LK_ material, no golf look dressing (point lights), shadows as before (WATER* never casts).
        static void CheckHole7Unchanged(Dictionary<string, Color> palette)
        {
            Say("=== hole 7 (Cliffside, unchanged rule) ===");
            GameObject parent = null;
            try
            {
                var h7 = GolfArcade.Course.Course.Cliffside().Holes[0];
                var proto = Resources.Load<GameObject>($"Course/hole_{h7.Number:00}");
                if (proto == null || palette == null) { Gate("HOLE7_MATERIALS_FLAT_AS_BEFORE", false, proto == null ? "hole_07 FBX not found" : "no palette"); return; }
                parent = new GameObject("PostcardImportCheck hole 7");
                var view = HoleView.Build(h7, parent.transform);
                var model = view.transform.Find("Course model");
                if (model == null) { Gate("HOLE7_MATERIALS_FLAT_AS_BEFORE", false, "no Course model"); return; }
                var src = proto.GetComponentsInChildren<Renderer>(true);
                var live = model.GetComponentsInChildren<Renderer>(true);
                int flat = 0, same = 0, wrong = 0, lk = 0, shadowWrong = 0;
                var bad = new List<string>();
                if (src.Length != live.Length) { Gate("HOLE7_MATERIALS_FLAT_AS_BEFORE", false, $"renderer count differs: FBX {src.Length}, instance {live.Length}"); return; }
                for (int i = 0; i < src.Length; i++)
                {
                    var a = src[i].sharedMaterials; var b = live[i].sharedMaterials;
                    bool expectCast = !live[i].name.StartsWith("WATER");
                    if ((live[i].shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On) != expectCast) shadowWrong++;
                    for (int j = 0; j < a.Length; j++)
                    {
                        if (a[j] == null) continue;
                        string n = a[j].name.Replace(" (Instance)", "");
                        if (GolfLook.Handles(n) || (b[j] != null && GolfLook.Handles(b[j].name))) lk++;
                        Color want;
                        if (palette.TryGetValue(n, out want)) { if (b[j] == HoleView.Mat(want)) flat++; else { wrong++; bad.Add(n + " on " + src[i].name); } }
                        else if (b[j] == a[j]) same++;
                        else { wrong++; bad.Add(n + " on " + src[i].name + " (not as imported)"); }
                    }
                }
                int lights = model.GetComponentsInChildren<Light>(true).Length;
                Gate("HOLE7_MATERIALS_FLAT_AS_BEFORE", wrong == 0 && flat > 0 && lk == 0 && lights == 0 && shadowWrong == 0,
                    $"{flat} palette slot(s) are HoleView's flat game colour, {same} slot(s) as imported, {wrong} different{(bad.Count > 0 ? " (" + Preview(bad.Take(4).ToArray(), 4) + ")" : "")}, {lk} LK_ slot(s), {lights} light(s) added, {shadowWrong} renderer(s) with a different shadow mode than the old rule; {live.Length} renderers");
            }
            catch (System.Exception e)
            {
                caught++;
                Debug.LogException(e);
                Gate("HOLE7_MATERIALS_FLAT_AS_BEFORE", false, e.GetType().Name + ": " + e.Message);
            }
            finally { if (parent != null) Object.DestroyImmediate(parent); Physics.SyncTransforms(); }
        }

        static void CheckPlaceholders(Transform model)
        {
            var visible = model.GetComponentsInChildren<Transform>(false)       // active in hierarchy only
                .Where(t => t != model && StartsWithAny(t.name, PlaceholderPrefixes)).Select(t => t.name).ToArray();
            Gate("GAMEPLAY_PLACEHOLDERS_HIDDEN", visible.Length == 0,
                visible.Length == 0 ? "FLAG*, HOLE_CUP*, BALL_START*, MARKER_* objects are all hidden by HoleView" : "still visible (HoleView hides exact names only): " + Preview(visible, 6));
        }

        // ------------------------------------------------------------------ helpers

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        static Color MainColor(Material m)
        {
            if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
            if (m.HasProperty("_Color")) return m.GetColor("_Color");
            return Color.clear;
        }

        static bool Near(Color a, Color b) { return Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f; }

        static bool StartsWithAny(string name, string[] prefixes) { return StartsWithAnyOf(name, prefixes) != null; }

        /// The first of `prefixes` that `name` starts with, or null.
        static string StartsWithAnyOf(string name, string[] prefixes)
        {
            foreach (var p in prefixes) if (name.StartsWith(p)) return p;
            return null;
        }

        static void AddTo(Dictionary<string, List<string>> map, string key, string value)
        {
            List<string> list;
            if (!map.TryGetValue(key, out list)) { list = new List<string>(); map[key] = list; }
            list.Add(value);
        }

        static string Preview(string[] items, int max)
        {
            if (items.Length <= max) return string.Join(", ", items);
            return string.Join(", ", items.Take(max).ToArray()) + ", ... (" + items.Length + " total)";
        }

        static string Short(string s) { return s != null && s.Length > 200 ? s.Substring(0, 200) + "..." : s; }
    }
}

namespace GolfArcade.EditorTools
{
    /// PostcardImportCheck's palette / lava / albedo+normal lines for the postcard holes (8, 9, 10), LK-aware. `gate` and `say` are the
    /// caller's. Same gate names as the old flat-palette block (PALETTE_OVERRIDES_APPLIED, PALETTE_NO_LEAKS, NO_LAVA_OUTSIDE_HOLE_10 |
    /// LAVA_RENDERER_PRESENT, LAVA_READS_ORANGE, LAVA_SHADER_RENDERS_IN_PIPELINE), new meaning:
    ///   - a slot named LK_* must render GolfLook's material for that hole (textured URP Lit: _BaseMap set; water: TennisWater; surf: GolfSurf);
    ///   - a WATER* slot must be TennisWater / GolfSurf (lava: the emissive LK_LAVA), never a flat colour;
    ///   - any other Palette name keeps the flat game colour (gameplay MAT_FLAG / POLE / CUP / BALL, the baseline MAT_* meshes);
    ///   - a material that already carries an albedo / normal / emission map may stay as imported; anything else is a leak;
    ///   - the crater lava "reads orange" when it is emissive, its emission multiplier is modest (<= 1.5, G <= 1: it must not need bloom) and its
    ///     brightest emission texels (Lava_E.png, top 5 %) times that multiplier encode to an orange (hue 8..38, S >= .6, no clipped channel).
    /// New lines (Game-view rule "holes 8-10 show albedo AND normal"):
    ///   GROUND_ALBEDO_AND_NORMAL   every LK_ ground material in the instantiated hole has a base map AND a normal map with _NORMALMAP on
    ///                              (LK_PLANTS, the sea and the GolfSurf cards are exempt: their table rows have no normal map), and, with
    ///                              requireLkMeshes, the hole carries LK_FAIRWAY, LK_GREEN and LK_ROUGH at all;
    ///   NO_FLAT_POSTCARD_GROUND    no renderer of the instantiated hole (hidden gameplay placeholders aside) is a flat _BaseColor-only URP Lit,
    ///                              except the gameplay parts MAT_FLAG / POLE / CUP / BALL. With requireLkMeshes false (the old flat FBX still
    ///                              installed) the line is reported as INFO "waits for install" instead of failing.
    /// Review-fix lines (v2 2026-10-04; the first three wait for install exactly like the two above):
    ///   NO_FOAM_STRIP_8_10         user line 6 (the white foam strips are deleted), runtime half of GATES.md NO_FOAM_STRIP: no WATER_FOAM* / DRESS_FOAM* renderer, no MAT_FOAM / MAT_SURF slot,
    ///                              every LK_SURF piece is a short patch (<= 12 yd long) and two patches are >= 7 yd apart (they would merge into a band)
    ///   LAVA_LIGHTS_PLACED         hole 10 has 3..5 LAVA_LIGHT_nn empties, >= 8 yd apart, 0.5..6 yd above the lava
    ///   ROCK_NEAR_LAVA_CHUNKED     every ROCK_ piece within 25 yd of a LAVA_LIGHT empty is <= 24 yd across (a renderer only gets the lights nearest ITS bounds: RUNTIME.md v2.2)
    ///   LAVA_MAPPING_DENSITY       (always runs) the lava never shows one flat orange: GolfLava projects its maps from the world at one tile per 24 m (any mesh, UVs or not), or, with
    ///                              the mesh-UV mode, the mesh's UV density is metres / 24 +- 40 %; and the lava covers >= 2 tiles across
    public static class PostcardLookMaterialRules
    {
        static readonly HashSet<string> GameplayFlat = new HashSet<string> { "MAT_FLAG", "MAT_POLE", "MAT_CUP", "MAT_BALL" };

        public static void Check(int holeNumber, GameObject prefab, Transform model, Dictionary<string, Color> palette,
                                 System.Action<string, bool, string> gate, System.Action<string> say, bool requireLkMeshes = true)
        {
            var proto = prefab ? prefab.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            var live = model.GetComponentsInChildren<Renderer>(true);
            if (palette == null || proto.Length != live.Length)
            {
                gate("PALETTE_OVERRIDES_APPLIED", false, palette == null ? "no palette to compare with" : $"the FBX has {proto.Length} renderer(s) but its instance has {live.Length}");
                return;
            }
            int flat = 0, lk = 0, water = 0, keptMaps = 0, empty = 0, plantsOff = 0, plantSlots = 0;
            string clean0(string n) { int d = n.IndexOf('.'); if (d > 0) n = n.Substring(0, d); int a = n.IndexOf('@'); return a > 0 ? n.Substring(0, a) : n; }
            var wrong = new List<string>(); var leaks = new List<string>();
            var groundWrong = new List<string>(); var groundNames = new HashSet<string>();
            var flatRenderers = new Dictionary<string, int>();
            for (int i = 0; i < proto.Length; i++)
            {
                var before = proto[i].sharedMaterials; var after = live[i].sharedMaterials;
                string rn = proto[i].name;
                bool visible = live[i].gameObject.activeInHierarchy;
                for (int j = 0; j < before.Length; j++)
                {
                    if (!before[j]) { empty++; continue; }
                    var now = j < after.Length ? after[j] : null;
                    string name = before[j].name.Replace(" (Instance)", "");
                    string shader = now && now.shader ? now.shader.name : "(none)";
                    if (GolfLook.Handles(name))
                    {
                        lk++; if (clean0(name) == "LK_PLANTS") plantSlots++;
                        var want = GolfLook.GetForHole(name, holeNumber);
                        bool ok = now == want && ((shader != "Universal Render Pipeline/Lit" && shader != "GolfArcade/GolfPlants") || now.GetTexture("_BaseMap"));   // v2 2026-10-05: LK_PLANTS is GolfArcade/GolfPlants (needs its palette atlas like URP Lit)
                        if (clean0(name) == "LK_PLANTS" && shader != "GolfArcade/GolfPlants") { ok = false; plantsOff++; }   // the sway shader (area U): a plant on another shader would stand still
                        if (!ok) wrong.Add($"{name} on {rn} -> '{(now ? now.name : "null")}' ({shader})");
                        string clean = name.Contains(".") ? name.Substring(0, name.IndexOf('.')) : name;
                        if (GolfLook.Table.TryGetValue(clean, out var spec) && spec.Kind == GolfLook.Kind.Lit && spec.Normal)
                        {
                            groundNames.Add(clean);
                            bool albedo = now && now.GetTexture("_BaseMap"), normal = now && now.GetTexture("_BumpMap") && now.IsKeywordEnabled("_NORMALMAP");
                            if (!albedo || !normal) groundWrong.Add($"{clean} on {rn}: _BaseMap {(albedo ? "ok" : "null")}, _BumpMap/_NORMALMAP {(normal ? "ok" : "missing")}");
                        }
                    }
                    else if (rn.StartsWith("WATER"))
                    {
                        water++;
                        bool ok = rn.StartsWith("WATER_LAVA") ? Emissive(now) : shader == "GolfArcade/TennisWater" || shader == "GolfArcade/GolfSurf";
                        if (!ok) wrong.Add($"{name} on {rn} -> '{(now ? now.name : "null")}' ({shader}) is not TennisWater/GolfSurf/emissive lava");
                    }
                    else if (palette.TryGetValue(name, out var colour))
                    {
                        flat++;
                        if (!now || !Near(MainColor(now), colour)) wrong.Add($"{name} on {rn}: flat colour missing");
                        if (visible && !GameplayFlat.Contains(name) && now) { flatRenderers.TryGetValue(name, out int c); flatRenderers[name] = c + 1; }
                    }
                    else if (HasMaps(before[j]) && now == before[j]) keptMaps++;
                    else leaks.Add($"{name} on {rn}");
                }
            }
            say($"INFO: hole {holeNumber} material rules: {lk} LK_ slot(s) -> GolfLook ({groundNames.Count} ground kinds: {string.Join(", ", groundNames.OrderBy(x => x).ToArray())}), {water} WATER* slot(s) -> TennisWater/GolfSurf/lava, {flat} palette slot(s) flat, {keptMaps} kept (own maps), {empty} empty");
            gate("PALETTE_OVERRIDES_APPLIED", wrong.Count == 0 && lk + water + flat > 0,
                wrong.Count == 0 ? $"{lk} LK_ slot(s) textured by GolfLook, {water} water slot(s) on TennisWater/GolfSurf/emissive lava, {flat} palette slot(s) flat" : "wrong: " + Preview(wrong, 6));
            gate("PALETTE_NO_LEAKS", leaks.Count == 0, leaks.Count == 0 ? "every slot is LK_, water/lava, a Palette name, or carries its own maps" : "unhandled: " + Preview(leaks.Distinct().ToList(), 6));
            gate("PLANTS_USE_SWAY_SHADER", plantsOff == 0, plantsOff == 0 ? $"{plantSlots} LK_PLANTS slot(s) render GolfArcade/GolfPlants (the gentle wind sway; plants only: no other LK_ material uses it)" : $"{plantsOff} LK_PLANTS slot(s) not on GolfArcade/GolfPlants");

            // ---- albedo AND normal in the Game view
            var required = new[] { "LK_FAIRWAY", "LK_GREEN", "LK_ROUGH" };
            var missing = required.Where(n => !groundNames.Contains(n)).ToArray();
            bool groundOk = groundWrong.Count == 0 && (!requireLkMeshes || (missing.Length == 0 && groundNames.Count >= 5));
            string groundDetail = $"{groundNames.Count} LK_ ground kind(s) in the instantiated hole, {groundWrong.Count} without base map + normal map + _NORMALMAP" +
                (groundWrong.Count > 0 ? ": " + Preview(groundWrong, 4) : "") +
                (requireLkMeshes ? (missing.Length > 0 ? "; the hole does not carry " + string.Join(", ", missing) + " (the rebuilt look meshes are not installed)" : "; LK_FAIRWAY, LK_GREEN, LK_ROUGH present") : "");
            if (requireLkMeshes) gate("GROUND_ALBEDO_AND_NORMAL", groundOk, groundDetail + "; ground kinds >= 5 needed");
            else say("INFO: (waits for install) GROUND_ALBEDO_AND_NORMAL would read: " + groundDetail);

            string flatDetail = flatRenderers.Count == 0 ? "no flat _BaseColor-only URP Lit left on a visible renderer (gameplay MAT_FLAG/POLE/CUP/BALL aside)"
                : "flat palette material(s) still drawn: " + string.Join(", ", flatRenderers.OrderBy(kv => kv.Key).Select(kv => kv.Key + " x" + kv.Value).ToArray());
            if (requireLkMeshes) gate("NO_FLAT_POSTCARD_GROUND", flatRenderers.Count == 0, flatDetail);
            else say("INFO: (waits for install) NO_FLAT_POSTCARD_GROUND would read: " + flatDetail);

            void Install(string name, bool ok, string detail)
            {
                if (requireLkMeshes) gate(name, ok, detail);
                else say($"INFO: (waits for install) {name} would {(ok ? "PASS" : "FAIL")}: {detail}");
            }
            var foam = FoamRenderers(proto, live);
            Install("NO_FOAM_STRIP_8_10", foam.Count == 0, foam.Count == 0 ? "no foam strip: no WATER_FOAM / DRESS_FOAM / MAT_FOAM, every LK_SURF piece is a patch <= 12 yd long and >= 7 yd from the next"
                : $"{foam.Count} foam renderer(s) still in the hole: {Preview(foam, 5)}");

            var lava = live.Where(r => r.name.StartsWith("WATER_LAVA")).ToArray();
            if (holeNumber != 10)
            {
                gate("NO_LAVA_OUTSIDE_HOLE_10", lava.Length == 0, lava.Length == 0 ? "no WATER_LAVA renderer" : "WATER_LAVA found: " + Preview(lava.Select(r => r.name).ToList(), 4));
                return;
            }
            gate("LAVA_RENDERER_PRESENT", lava.Length > 0, lava.Length > 0 ? $"{lava.Length} WATER_LAVA renderer(s)" : "no renderer whose name starts with WATER_LAVA");
            {
                string mapDetail = LavaMapping(lava, out bool mapOk);
                gate("LAVA_MAPPING_DENSITY", mapOk, mapDetail);
                var empties = LavaEmpties(model);
                string placeDetail = LavaLightsPlaced(empties, lava, out bool placedOk);
                Install("LAVA_LIGHTS_PLACED", placedOk, placeDetail);
                string chunkDetail = RockChunking(live, empties, out bool chunkOk);
                Install("ROCK_NEAR_LAVA_CHUNKED", chunkOk && empties.Count > 0, empties.Count == 0 ? "no LAVA_LIGHT empties, so nothing could light the walls (see LAVA_LIGHTS_PLACED)" : chunkDetail);
            }
            var pipeline = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var notOrange = new List<string>(); var wrongShader = new List<string>(); var lavaNotes = new List<string>();
            foreach (var r in lava)
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    string shader = m.shader ? m.shader.name : "(none)";
                    Vector4 e = m.HasProperty("_EmissionColor") ? m.GetVector("_EmissionColor") : Vector4.zero;
                    string detail = "not emissive (no emission map or _EMISSION off)";
                    bool orange = Emissive(m) && EmissionReadsOrange(m, e, out detail);
                    say($"INFO: hole 10 {r.name} '{m.name}' shader '{shader}' _BaseColor #{ColorUtility.ToHtmlStringRGB(MainColor(m))}, emission map {(m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") ? m.GetTexture("_EmissionMap").name : "none")}, _EMISSION {m.IsKeywordEnabled("_EMISSION")}, multiplier ({e.x:F2},{e.y:F2},{e.z:F2}): {detail}");
                    lavaNotes.Add(detail);
                    if (!orange) notOrange.Add($"{r.name}/{m.name}: {detail}");
                    bool srp = shader.StartsWith("Universal Render Pipeline/") || shader.StartsWith("Shader Graphs/") || shader.StartsWith("GolfArcade/");
                    if (pipeline && !srp) wrongShader.Add($"{r.name}/{m.name} uses '{shader}'");
                }
            gate("LAVA_READS_ORANGE", lava.Length > 0 && notOrange.Count == 0,
                notOrange.Count == 0 ? "WATER_LAVA is emissive and its brightest emission texels x the multiplier encode to orange without bloom: " + string.Join("; ", lavaNotes.ToArray()) : "not orange: " + string.Join("; ", notOrange.ToArray()));
            gate("LAVA_SHADER_RENDERS_IN_PIPELINE", wrongShader.Count == 0, wrongShader.Count == 0 ? "the lava shader is drawn by the active pipeline" : string.Join("; ", wrongShader.ToArray()));
        }

        // ------------------------------------------------------------------ review-fix rules (foam, lava mapping, lava lights, rock chunks)

        static readonly string[] FoamRendererPrefixes = { "WATER_FOAM", "DRESS_FOAM" };
        static readonly HashSet<string> FoamMaterials = new HashSet<string> { "MAT_FOAM", "WATER_FOAM", "MAT_SURF" };
        /// A surf patch (LK_SURF, short WATER_SURF_nn pieces: the ground library builds <= 8 m patches, <= 25 % of the shore) may be at most this long and this close to its neighbour.
        const float SurfPatchMaxYards = 12f, SurfPatchMinGapYards = 7f;

        /// The runtime half of NO_FOAM_STRIP (GATES.md has the Blender half: shore coverage <= 25 %, runs <= 10 m, gaps >= 8 m, no ribbons): visible renderers that are a foam STRIP.
        /// Foam as a name / material (WATER_FOAM*, DRESS_FOAM*, MAT_FOAM, MAT_SURF) is always one (the flat baseline's white band); LK_SURF pieces are short patches: a piece longer than
        /// SurfPatchMaxYards is a band, and two pieces closer than SurfPatchMinGapYards merge into one.
        static List<string> FoamRenderers(Renderer[] proto, Renderer[] live)
        {
            var found = new List<string>(); var patches = new List<Renderer>();
            for (int i = 0; i < live.Length && i < proto.Length; i++)
            {
                if (!live[i].gameObject.activeInHierarchy) continue;
                string pn = proto[i].name;
                bool legacy = FoamRendererPrefixes.Any(p => pn.StartsWith(p)) || proto[i].sharedMaterials.Any(m => m && FoamMaterials.Contains(m.name.Replace(" (Instance)", "")));
                if (legacy) { found.Add(pn + " (foam name / material)"); continue; }
                bool surf = proto[i].sharedMaterials.Any(m => m && m.name.StartsWith("LK_SURF"))
                         || live[i].sharedMaterials.Any(m => m && (m.name.StartsWith("LK_SURF") || (m.shader && m.shader.name == "GolfArcade/GolfSurf" && m.HasProperty("_VertexAlpha") && m.GetFloat("_VertexAlpha") >= .5f)));
                if (!surf) continue;
                var b = live[i].bounds; float longest = Mathf.Max(b.size.x, b.size.z);
                if (longest > SurfPatchMaxYards) found.Add($"{pn} is {longest:F0} yd long (a surf patch is <= {SurfPatchMaxYards:F0} yd: this is a band)"); else patches.Add(live[i]);
            }
            for (int a = 0; a < patches.Count; a++)
                for (int c = a + 1; c < patches.Count; c++)
                {
                    Bounds x = patches[a].bounds, y = patches[c].bounds;
                    float gap = new Vector2(Mathf.Max(0, Mathf.Max(x.min.x - y.max.x, y.min.x - x.max.x)), Mathf.Max(0, Mathf.Max(x.min.z - y.max.z, y.min.z - x.max.z))).magnitude;
                    string how = "bounds";
                    // area U 2026-10-04: axis-aligned BOUNDS over-state how close two DIAGONAL patches are (the staged hole 8's WATER_SURF_13 / _14 read 5.8 yd by bounds while the Blender gate measures the shore gap
                    // as 15 m): when the bounds say 'too close', measure the real plan distance between the two patch meshes (vertex to edge, containment = 0). The 7 yd limit is unchanged; a patch pair that
                    // really is < 7 yd apart still fails, and a patch whose mesh cannot be read keeps the bounds measure.
                    if (gap < SurfPatchMinGapYards && PlanDistance(patches[a], patches[c], out float real)) { gap = real; how = "mesh"; }
                    if (gap < SurfPatchMinGapYards) found.Add($"{patches[a].name} and {patches[c].name} only {gap:F1} yd apart ({how}; patches merge into a strip below {SurfPatchMinGapYards:F0} yd)");
                }
            return found;
        }

        /// Smallest distance in the ground plane (x, z) between the triangle meshes of two renderers: vertex to edge both ways, 0 when a vertex of one lies inside a triangle of the other. False when a mesh is unreadable.
        static bool PlanDistance(Renderer ra, Renderer rb, out float distance)
        {
            distance = 0;
            var fa = ra.GetComponent<MeshFilter>(); var fb = rb.GetComponent<MeshFilter>();
            if (!fa || !fb || !fa.sharedMesh || !fb.sharedMesh || !fa.sharedMesh.isReadable || !fb.sharedMesh.isReadable) return false;
            Vector2[] pa = PlanVertices(fa), pb = PlanVertices(fb);
            int[] ta = fa.sharedMesh.triangles, tb = fb.sharedMesh.triangles;
            float best = float.MaxValue;
            best = Mathf.Min(best, VertexToEdges(pa, pb, tb)); best = Mathf.Min(best, VertexToEdges(pb, pa, ta));
            if (best > 0 && (AnyInside(pa, pb, tb) || AnyInside(pb, pa, ta))) best = 0;
            distance = best;
            return true;
        }

        static Vector2[] PlanVertices(MeshFilter f)
        {
            var v = f.sharedMesh.vertices; var m = f.transform.localToWorldMatrix; var o = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) { var w = m.MultiplyPoint3x4(v[i]); o[i] = new Vector2(w.x, w.z); }
            return o;
        }

        static float VertexToEdges(Vector2[] points, Vector2[] verts, int[] tris)
        {
            float best = float.MaxValue;
            for (int t = 0; t + 2 < tris.Length; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    Vector2 a = verts[tris[t + e]], b = verts[tris[t + (e + 1) % 3]], ab = b - a; float len2 = ab.sqrMagnitude;
                    foreach (var p in points)
                    {
                        float k = len2 > 1e-12f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0;
                        best = Mathf.Min(best, (p - (a + ab * k)).magnitude);
                    }
                }
            return best;
        }

        static bool AnyInside(Vector2[] points, Vector2[] verts, int[] tris)
        {
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector2 a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                foreach (var p in points)
                {
                    float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
                    if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return true;
                }
            }
            return false;
        }

        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

        /// The LAVA_LIGHT_nn empties (the point light GolfLook.DressModel spawns is a child that carries a Light: not an empty).
        static List<Transform> LavaEmpties(Transform model) =>
            model.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("LAVA_LIGHT") && !t.GetComponent<Light>()).ToList();

        static string LavaLightsPlaced(List<Transform> empties, Renderer[] lava, out bool ok)
        {
            var problems = new List<string>();
            if (empties.Count < 3 || empties.Count > 5) problems.Add($"{empties.Count} LAVA_LIGHT empties (want 3..5)");
            float nearest = float.MaxValue;
            for (int a = 0; a < empties.Count; a++)
                for (int b = a + 1; b < empties.Count; b++)
                    nearest = Mathf.Min(nearest, new Vector2(empties[a].position.x - empties[b].position.x, empties[a].position.z - empties[b].position.z).magnitude);
            if (empties.Count >= 2 && nearest < 8f) problems.Add($"two empties only {nearest:F1} yd apart (want >= 8)");
            if (lava.Length > 0)
            {
                var b = lava[0].bounds; foreach (var r in lava) b.Encapsulate(r.bounds);
                var off = new List<string>();
                foreach (var e in empties)
                {
                    float above = e.position.y - b.max.y;
                    bool inside = e.position.x >= b.min.x - 8 && e.position.x <= b.max.x + 8 && e.position.z >= b.min.z - 8 && e.position.z <= b.max.z + 8;
                    if (above < .5f || above > 6f || !inside) off.Add($"{e.name} {above:F1} yd above the lava{(inside ? "" : ", outside it")}");
                }
                if (off.Count > 0) problems.Add("not over the lava (0.5..6 yd above its top): " + Preview(off, 3));
            }
            else problems.Add("no lava renderer to place them over");
            ok = problems.Count == 0;
            return ok ? $"{empties.Count} LAVA_LIGHT empties, nearest pair {(empties.Count >= 2 ? nearest.ToString("F1") : "-")} yd apart (>= 8), each 0.5..6 yd over the lava" : string.Join("; ", problems.ToArray());
        }

        /// A renderer only gets the lights nearest ITS bounds (per-object limit 2 in TennisURP, 4 in HeroBaseStudioURP): a ROCK_ piece within 25 yd of a lava light must be <= 24 yd across.
        static string RockChunking(Renderer[] live, List<Transform> empties, out bool ok)
        {
            var big = new List<string>(); int near = 0;
            foreach (var r in live)
            {
                if (!r.name.StartsWith("ROCK_") || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                if (!empties.Any(e => b.SqrDistance(e.position) <= 25f * 25f)) continue;
                near++;
                float across = Mathf.Max(b.size.x, b.size.z);
                if (across > 24f) big.Add($"{r.name} {across:F0} yd");
            }
            ok = big.Count == 0;
            return ok ? $"{near} ROCK_ piece(s) within 25 yd of a lava light, none wider than 24 yd" : $"{big.Count} of {near} ROCK_ piece(s) within 25 yd of a lava light are wider than 24 yd (they would see only their nearest light, or none): {Preview(big, 4)}";
        }

        /// The lava must show its swirl on ANY mesh: world projection at one tile per 24 m (GolfLava _WorldUV 1), or mesh UVs at metres / 24 +- 40 %; and cover >= 2 tiles.
        static string LavaMapping(Renderer[] lava, out bool ok)
        {
            var problems = new List<string>(); var notes = new List<string>();
            if (lava.Length == 0) { ok = false; return "no lava renderer"; }
            var all = lava[0].bounds; foreach (var r in lava) all.Encapsulate(r.bounds);
            float tiles = Mathf.Max(all.size.x, all.size.z) * GolfLook.LavaTilePerYard;
            foreach (var r in lava)
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) continue;
                    bool golfLava = m.shader && m.shader.name == "GolfArcade/GolfLava";
                    bool world = golfLava && m.GetFloat("_WorldUV") >= .5f;
                    if (world)
                    {
                        var st = m.GetTextureScale("_BaseMap");
                        float tx = m.GetFloat("_WorldTile") * st.x, tz = m.GetFloat("_WorldTile") * st.y, want = GolfLook.LavaTilePerYard;
                        bool good = Mathf.Abs(tx / want - 1) <= .05f && Mathf.Abs(tz / want - 1) <= .05f;
                        notes.Add($"{r.name}: world projection {tx / want:F2}x of one tile per 24 m ({1f / tx / 1.0936f:F0} m per tile)");
                        if (!good) problems.Add($"{r.name}: world tile {tx:F4}/{tz:F4} per yd, want {want:F4} +-5 %");
                    }
                    else
                    {
                        var mf = r.GetComponent<MeshFilter>(); var mesh = mf ? mf.sharedMesh : null;
                        if (!mesh || !mesh.isReadable || mesh.uv == null || mesh.uv.Length != mesh.vertexCount) { problems.Add($"{r.name}: mesh-UV mode but the mesh has no readable UVs"); continue; }
                        float density = UvDensity(r, mesh, out float span);
                        float want = GolfLook.LavaTilePerYard;
                        notes.Add($"{r.name}: mesh UV density {density:F4} tiles/yd (want {want:F4} +-40 %), UV span {span:F1} tiles");
                        if (density < want * .6f || density > want * 1.4f) problems.Add($"{r.name}: UV density {density:F4} tiles/yd, want {want * .6f:F4}..{want * 1.4f:F4} (the lava would show a few huge swirls or one flat colour)");
                        if (span < 2f) problems.Add($"{r.name}: UV span {span:F1} tiles (< 2)");
                    }
                }
            if (tiles < 2f) problems.Add($"the lava covers only {tiles:F1} tiles across ({Mathf.Max(all.size.x, all.size.z):F0} yd; < 2)");
            ok = problems.Count == 0;
            return (ok ? "lava mapping reads: " : "lava would render flat or with the wrong swirl size: " + string.Join("; ", problems.ToArray()) + "; ") + string.Join("; ", notes.ToArray()) + $"; covers {tiles:F1} tiles across (>= 2)";
        }

        /// Median sqrt(UV area / world area) over up to 3000 triangles = tiles per yard, and the UV extent of the mesh.
        static float UvDensity(Renderer r, Mesh mesh, out float span)
        {
            var v = mesh.vertices; var uv = mesh.uv; var tris = mesh.triangles;
            var ratios = new List<float>(); int step = Mathf.Max(1, tris.Length / 3 / 3000);
            for (int t = 0; t < tris.Length; t += 3 * step)
            {
                Vector3 a = r.transform.TransformPoint(v[tris[t]]), b = r.transform.TransformPoint(v[tris[t + 1]]), c = r.transform.TransformPoint(v[tris[t + 2]]);
                float area = Vector3.Cross(b - a, c - a).magnitude * .5f;
                Vector2 ua = uv[tris[t]], ub = uv[tris[t + 1]], uc = uv[tris[t + 2]];
                float uvArea = Mathf.Abs((ub.x - ua.x) * (uc.y - ua.y) - (uc.x - ua.x) * (ub.y - ua.y)) * .5f;
                if (area > 1e-4f) ratios.Add(Mathf.Sqrt(uvArea / area));
            }
            ratios.Sort();
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var p in uv) { minU = Mathf.Min(minU, p.x); maxU = Mathf.Max(maxU, p.x); minV = Mathf.Min(minV, p.y); maxV = Mathf.Max(maxV, p.y); }
            span = Mathf.Max(maxU - minU, maxV - minV);
            return ratios.Count > 0 ? ratios[ratios.Count / 2] : 0;
        }

        /// The brightest 5 % of the emission map's texels (read from the source PNG) times the material's linear multiplier, sRGB encoded: orange?
        static bool EmissionReadsOrange(Material m, Vector4 mult, out string detail)
        {
            detail = "";
            if (SelfLitLava(m))
            {
                // GolfLava: every stop of the ramp is orange (hue 8..28, S >= .75) and all but the coolest (a few % of the texels) are bright (V >= .7)
                var bad = new List<string>(); var parts = new List<string>();
                foreach (var stop in new[] { "_Deep", "_Crust", "_Flow", "_Hot" })
                {
                    Color.RGBToHSV(m.GetColor(stop), out var sh, out var ss, out var sv); sh *= 360;
                    parts.Add($"{stop} hue {sh:F0} S {ss:F2} V {sv:F2}");
                    if (sh < 8 || sh > 28 || ss < .75f || (sv < .7f && stop != "_Deep")) bad.Add(stop);
                }
                detail = "self-lit GolfLava: ramp " + string.Join(", ", parts.ToArray()) + " (hue 8..28, S >= .75, V >= .7 except _Deep)" + (bad.Count > 0 ? "; OUT OF BAND: " + string.Join(", ", bad.ToArray()) : "") + "; no emission multiplier, no bloom";
                return bad.Count == 0;
            }
            var tex = m.GetTexture("_EmissionMap");
            string file = tex ? Path.Combine(Application.dataPath, "Resources/Course/Look", tex.name + ".png") : null;
            if (file == null || !File.Exists(file)) { detail = "emission source PNG not found"; return false; }
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            t.LoadImage(File.ReadAllBytes(file));
            var px = t.GetPixels32(); Object.DestroyImmediate(t);
            var order = px.Select((p, i) => (lum: .2126f * p.r + .7152f * p.g + .0722f * p.b, i)).OrderByDescending(x => x.lum).Take(Mathf.Max(1, px.Length / 20)).ToArray();
            float r = 0, g = 0, b = 0;
            foreach (var (_, i) in order) { r += px[i].r; g += px[i].g; b += px[i].b; }
            float n = order.Length * 255f;
            Color peak = new Color(Mathf.Clamp01(Mathf.GammaToLinearSpace(r / n) * mult.x), Mathf.Clamp01(Mathf.GammaToLinearSpace(g / n) * mult.y), Mathf.Clamp01(Mathf.GammaToLinearSpace(b / n) * mult.z));
            var srgb = new Color(Mathf.LinearToGammaSpace(peak.r), Mathf.LinearToGammaSpace(peak.g), Mathf.LinearToGammaSpace(peak.b));
            Color.RGBToHSV(srgb, out var h, out var s, out var v); h *= 360;
            bool clipped = Mathf.GammaToLinearSpace(r / n) * mult.x > 1.001f || Mathf.GammaToLinearSpace(g / n) * mult.y > 1.001f || Mathf.GammaToLinearSpace(b / n) * mult.z > 1.001f;
            bool modest = Mathf.Max(mult.x, Mathf.Max(mult.y, mult.z)) <= 1.5f && mult.y <= 1.0001f;
            bool ok = h >= 8 && h <= 38 && s >= .6f && !clipped && modest;
            detail = $"top-5% emission texels x multiplier = ({Mathf.RoundToInt(srgb.r * 255)},{Mathf.RoundToInt(srgb.g * 255)},{Mathf.RoundToInt(srgb.b * 255)}) hue {h:F0} (8..38) S {s:F2} (>= .6), clipped {clipped}, multiplier modest (max <= 1.5, G <= 1) {modest}";
            return ok;
        }

        static bool SelfLitLava(Material m) => m && m.shader && m.shader.name == "GolfArcade/GolfLava" && m.GetTexture("_EmissionMap") && m.GetTexture("_BaseMap");
        /// Emissive = URP Lit with an emission map and _EMISSION on, or the self-lit GolfArcade/GolfLava (its textures are the heat, its ramp the colour).
        static bool Emissive(Material m) => SelfLitLava(m) || (m && m.HasProperty("_EmissionMap") && m.GetTexture("_EmissionMap") && m.IsKeywordEnabled("_EMISSION"));
        static bool HasMaps(Material m)
        {
            foreach (var p in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_EmissionMap" }) if (m.HasProperty(p) && m.GetTexture(p)) return true;
            return false;
        }
        static Color MainColor(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.clear;
        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;
        static string Preview(List<string> items, int max) => items.Count <= max ? string.Join(", ", items.ToArray()) : string.Join(", ", items.Take(max).ToArray()) + $", ... ({items.Count} total)";
    }
}
