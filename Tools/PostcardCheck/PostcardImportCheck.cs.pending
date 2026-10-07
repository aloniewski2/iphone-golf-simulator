// PENDING: kept out of Unity/Assets on purpose. The lead copies it to Unity/Assets/Editor/PostcardImportCheck.cs at the (queued) Unity
// step, after hole_08.fbx, hole_09.fbx and hole_10.fbx (+ .meta) are in Unity/Assets/Resources/Course/. It cannot be compiled outside Unity
// (it needs UnityEngine/UnityEditor); every Unity call below is one already used elsewhere in this repo, or a long-standing Unity API.
// It was type-checked with Roslyn (-langversion:9, Unity's own csc) against a hand-written stub of exactly the Unity API surface it touches,
// and executed against a fake HoleView/FBX world driven by the real Hole.LieAt: ideal world = every gate PASS, exit 0; ten injected defects
// (mirrored ground, wrong play height, pin off, tee->pin flipped, tilted MARKER_UP, grey lava, built-in lava shader, un-palettised material,
// visible FLAG.001, missing FBX) each fail exactly the intended gate. The stubs are not in the repo (scratchpad only).
//
//   cp Tools/PostcardCheck/PostcardImportCheck.cs.pending Unity/Assets/Editor/PostcardImportCheck.cs
//   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.PostcardImportCheck.Run -logFile <log>
//   grep -E '^(GATE|INFO|WARN):|^===' <log>              # exit code 0 = every gate PASS, 1 = at least one FAIL
// Optional: POSTCARD_IMPORT_REPORT=/abs/path.txt also writes every GATE/INFO line to that file.
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
            Gate("FBX_LOADED", prefab != null, prefab != null
                ? $"Resources.Load<GameObject>(\"{path}\") -> '{prefab.name}'"
                : $"Resources.Load<GameObject>(\"{path}\") is null: HoleView would draw primitives and every ground height would read 0");
            if (prefab == null) return;

            GameObject parent = null;
            int logStart = logs.Count;
            try
            {
                parent = new GameObject("PostcardImportCheck hole " + hole.Number);
                var view = HoleView.Build(hole, parent.transform);   // what GolfGame does for the hole on screen
                var model = view.transform.Find("Course model");     // HoleView names the instantiated FBX this

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
