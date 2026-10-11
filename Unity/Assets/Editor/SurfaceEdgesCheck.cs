// Exact-equality check for GolfSurfaceEdges (the _GolfEdgeMap outline-distance texture).
// Keeps the ORIGINAL algorithm (git HEAD before the speed-up: per-triangle string keys, brute-force distance field)
// as a private reference and compares it, on the very same model, with GolfSurfaceEdges.Compute for every real
// Apply call made while HoleView.Build dresses each hole, then once more on the finished hole.
//   Unity -batchmode -nographics -projectPath Unity -executeMethod GolfArcade.EditorTools.SurfaceEdgesCheck.Run -logFile <abs log>
//   SURFACE_EDGES_REPORT=/abs/path.txt   report file
//   SURFACE_EDGES_HOLES=8,9,10           optional subset (default: every modelled hole)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.EditorTools
{
    public static class SurfaceEdgesCheck
    {
        const int Size = 256;
        static readonly List<string> report = new();
        static void Say(string line) { report.Add(line); UnityEngine.Debug.Log("EDGECHECK: " + line); }

        // ---------------------------------------------------------------------------------------------------
        // The original Apply, verbatim, returning what it used to publish instead of setting globals

        struct Segment { public Vector2 a, b; }
        sealed class Original { public Color32[] Pixels; public Vector4 Bounds; public List<Segment>[] Lines; public float[] Distances; }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static byte Byte(float x) => (byte)Mathf.RoundToInt(Mathf.Clamp01(x) * 255);
        static string Point(Vector3 p) => $"{Mathf.RoundToInt(p.x * 1000)}:{Mathf.RoundToInt(p.z * 1000)}";

        static Original Reference(GameObject model)
        {
            var lines = new List<Segment>[] { new(), new(), new(), new() };
            var lo = new Vector2(float.MaxValue, float.MaxValue); var hi = new Vector2(float.MinValue, float.MinValue);
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh; if (!mesh || !mesh.isReadable) continue;
                string name = mf.name; int kind = name.StartsWith("GREEN") ? 0 : name.StartsWith("FAIRWAY") ? 1 :
                    name.StartsWith("BUNKER") || name.StartsWith("DRESS_SAND") ? 2 : name.StartsWith("WATER_OCEAN") || name.StartsWith("WATER_SHELF") ? 3 : -1;
                if (kind < 0) continue;
                var p = mesh.vertices; for (int i = 0; i < p.Length; i++) p[i] = mf.transform.TransformPoint(p[i]);
                var counts = new Dictionary<string, int>(); var segs = new Dictionary<string, Segment>(); var tri = mesh.triangles;
                for (int j = 0; j < tri.Length; j += 3)
                {
                    Vector3 a = p[tri[j]], b = p[tri[j + 1]], c = p[tri[j + 2]];
                    if (Vector3.Cross(b - a, c - a).normalized.y < .85f) continue;
                    var t = new[] { a, b, c };
                    for (int k = 0; k < 3; k++)
                    {
                        var x = t[k]; var y = t[(k + 1) % 3]; string u = Point(x), v = Point(y);
                        if (u == v) continue; string key = string.CompareOrdinal(u, v) < 0 ? u + "/" + v : v + "/" + u;
                        counts.TryGetValue(key, out int count); counts[key] = count + 1;
                        segs[key] = new Segment { a = new Vector2(x.x, x.z), b = new Vector2(y.x, y.z) };
                        if (kind < 3) { lo = Vector2.Min(lo, new Vector2(x.x, x.z)); hi = Vector2.Max(hi, new Vector2(x.x, x.z)); }
                    }
                }
                foreach (var e in counts) if (e.Value == 1) lines[kind].Add(segs[e.Key]);
            }
            if (lo.x == float.MaxValue) return null;
            lo -= Vector2.one * 4; hi += Vector2.one * 4; var extent = hi - lo; var origin = lo;
            var pixels = new Color32[Size * Size];
            var dist = new float[4 * Size * Size];
            Parallel.For(0, Size, y => {
                for (int x = 0; x < Size; x++)
                {
                    var p = origin + new Vector2((x + .5f) / Size * extent.x, (y + .5f) / Size * extent.y);
                    var distances = new float[4];
                    for (int k = 0; k < 4; k++)
                    {
                        float best = 32 * 32;
                        foreach (var s in lines[k])
                        {
                            var d = s.b - s.a; float den = Vector2.Dot(d, d);
                            float t = den > 0 ? Mathf.Clamp01(Vector2.Dot(p - s.a, d) / den) : 0;
                            best = Mathf.Min(best, (p - s.a - d * t).sqrMagnitude);
                        }
                        distances[k] = Mathf.Sqrt(best);
                        dist[k * Size * Size + y * Size + x] = distances[k];
                    }
                    float apron = 1 - .045f * (1 - Smooth(.3f, 1.8f, distances[1]));
                    float fringe = 1 - .085f * (1 - Smooth(.6f, 2.4f, distances[0]));
                    float sand = 1 + .10f * (1 - Smooth(.3f, 1.8f, distances[2])) - .12f * (1 - Smooth(.5f, 5f, distances[3]));
                    pixels[y * Size + x] = new Color32(Byte(apron * fringe), Byte(apron), Byte(sand * .5f), 255);
                }
            });
            return new Original { Pixels = pixels, Bounds = new Vector4(lo.x, lo.y, 1 / extent.x, 1 / extent.y), Lines = lines, Distances = dist };
        }

        // ---------------------------------------------------------------------------------------------------

        static int Bits(float f) => BitConverter.SingleToInt32Bits(f);
        static double Cpu() => Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;

        sealed class Verdict
        {
            public bool Pixels, Bounds, Distances, SegmentsSequence, SegmentsSet, Counts, BothNull;
            public double OldMs, NewMs, NewBestMs, OldCpu, NewCpu; public string SegCounts = "-", Phases = "";
            public bool Identical => BothNull || (Pixels && Bounds && Distances && SegmentsSet && Counts);
            public string Detail = "";
        }

        static Verdict Compare(GameObject model)
        {
            var v = new Verdict();
            var sw = Stopwatch.StartNew(); double cpu0 = Cpu();
            var old = Reference(model);
            v.OldMs = sw.Elapsed.TotalMilliseconds; v.OldCpu = Cpu() - cpu0;
            sw.Restart(); cpu0 = Cpu();
            var now = GolfSurfaceEdges.Compute(model, true);
            v.NewMs = sw.Elapsed.TotalMilliseconds; v.NewCpu = Cpu() - cpu0; v.NewBestMs = v.NewMs; if (now != null) v.Phases = $"scan {now.ScanMs:F0} ms + map {now.FieldMs:F0} ms";
            for (int r = 0; r < 2; r++) { sw.Restart(); var again = GolfSurfaceEdges.Compute(model, false); double ms = sw.Elapsed.TotalMilliseconds; if (ms < v.NewBestMs) { v.NewBestMs = ms; if (again != null) v.Phases = $"scan {again.ScanMs:F0} ms + map {again.FieldMs:F0} ms"; } }
            if (old == null || now == null) { v.BothNull = old == null && now == null; v.Detail = $"null mismatch old={(old == null)} new={(now == null)}"; return v; }
            v.SegCounts = string.Join("/", old.Lines.Select(l => l.Count));
            v.Counts = true; for (int k = 0; k < 4; k++) if (old.Lines[k].Count != now.SegmentCounts[k]) v.Counts = false;
            v.Bounds = Bits(old.Bounds.x) == Bits(now.Bounds.x) && Bits(old.Bounds.y) == Bits(now.Bounds.y) && Bits(old.Bounds.z) == Bits(now.Bounds.z) && Bits(old.Bounds.w) == Bits(now.Bounds.w);
            v.Pixels = old.Pixels.Length == now.Pixels.Length; int pixelDiff = 0;
            if (v.Pixels) for (int i = 0; i < old.Pixels.Length; i++) { var a = old.Pixels[i]; var b = now.Pixels[i]; if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) pixelDiff++; }
            v.Pixels = v.Pixels && pixelDiff == 0;
            int distDiff = 0; for (int i = 0; i < old.Distances.Length; i++) if (Bits(old.Distances[i]) != Bits(now.Distances[i])) distDiff++;
            v.Distances = distDiff == 0;
            bool seq = true, set = true;
            for (int k = 0; k < 4; k++)
            {
                var l = old.Lines[k]; var f = now.Segments[k];
                if (l.Count * 4 != f.Length) { seq = set = false; continue; }
                var left = new List<(int, int, int, int)>(); var right = new List<(int, int, int, int)>();
                for (int i = 0; i < l.Count; i++)
                {
                    var a = (Bits(l[i].a.x), Bits(l[i].a.y), Bits(l[i].b.x), Bits(l[i].b.y)); var b = (Bits(f[i * 4]), Bits(f[i * 4 + 1]), Bits(f[i * 4 + 2]), Bits(f[i * 4 + 3]));
                    if (!a.Equals(b)) seq = false;
                    left.Add(a); right.Add(b);
                }
                left.Sort(); right.Sort(); if (!left.SequenceEqual(right)) set = false;
            }
            v.SegmentsSequence = seq; v.SegmentsSet = set;
            v.Detail = $"pixelsDiff {pixelDiff} distancesDiff {distDiff} seq {seq} set {set} bounds {v.Bounds}";
            return v;
        }

        static string Yes(bool b) => b ? "yes" : "NO";

        // ---------------------------------------------------------------------------------------------------

        static int appliesSeen, appliesLogged;
        static readonly List<string> perCall = new();
        static bool allIdentical = true, inBuild;
        static string currentHole = "?";
        static readonly Stopwatch applyClock = new();

        static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (inBuild && type == LogType.Log && condition.StartsWith("[GolfSurfaceEdges]")) { appliesLogged++; perCall.Add($"      real Apply recomputed in {applyClock.Elapsed.TotalMilliseconds:F0} ms"); }
        }

        static void BeforeApply(GameObject model)
        {
            appliesSeen++;
            // The real Apply is about to run; its time is measured from the end of this hook to its log line.
            var v = Compare(model);
            if (!v.Identical) allIdentical = false;
            perCall.Add($"    call #{appliesSeen}: equal {Yes(v.Identical)} | old {v.OldMs:F0} ms ({v.OldCpu:F0} cpu-ms) | new {v.NewMs:F0} ms ({v.NewCpu:F0} cpu-ms) (best of 3 {v.NewBestMs:F0}; {v.Phases}) | segments {v.SegCounts} | {v.Detail}");
            applyClock.Restart();
        }


        // ---------------------------------------------------------------------------------------------------
        // Float-contraction probe: do the Vector2-operator form (the original) and the scalar form round alike in this JIT?

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static float NMul(float a, float b) => a * b;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static float NAdd(float a, float b) => a + b;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)] static float NSub(float a, float b) => a - b;

        static float FormA(Vector2 p, Vector2 sa, Vector2 sb)
        {
            var d = sb - sa; float den = Vector2.Dot(d, d);
            float t = den > 0 ? Mathf.Clamp01(Vector2.Dot(p - sa, d) / den) : 0;
            return (p - sa - d * t).sqrMagnitude;
        }
        static float FormB(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay; float den = dx * dx + dy * dy;
            float qx = px - ax, qy = py - ay;
            float t = den > 0 ? Mathf.Clamp01((qx * dx + qy * dy) / den) : 0;
            float ex = qx - dx * t, ey = qy - dy * t;
            return ex * ex + ey * ey;
        }
        static float FormNC(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = NSub(bx, ax), dy = NSub(by, ay); float den = NAdd(NMul(dx, dx), NMul(dy, dy));
            float qx = NSub(px, ax), qy = NSub(py, ay);
            float t = den > 0 ? Mathf.Clamp01(NAdd(NMul(qx, dx), NMul(qy, dy)) / den) : 0;
            float ex = NSub(qx, NMul(dx, t)), ey = NSub(qy, NMul(dy, t));
            return NAdd(NMul(ex, ex), NMul(ey, ey));
        }

        public static void FloatProbe()
        {
            var rng = new System.Random(5); int a_b = 0, a_nc = 0, b_nc = 0, n = 2000000;
            for (int i = 0; i < n; i++)
            {
                float R() => (float)(rng.NextDouble() * 400 - 200);
                float px = R(), py = R(), ax = R(), ay = R(), bx = ax + (float)(rng.NextDouble() * 6 - 3), by = ay + (float)(rng.NextDouble() * 6 - 3);
                if (i % 7 == 0) { px = ax + (float)(rng.NextDouble() * 20 - 10); py = ay + (float)(rng.NextDouble() * 20 - 10); }
                float a = FormA(new Vector2(px, py), new Vector2(ax, ay), new Vector2(bx, by)), b = FormB(px, py, ax, ay, bx, by), c = FormNC(px, py, ax, ay, bx, by);
                if (Bits(a) != Bits(b)) a_b++; if (Bits(a) != Bits(c)) a_nc++; if (Bits(b) != Bits(c)) b_nc++;
            }
            Say($"FloatProbe {n} samples on {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} ({SystemInfo.processorType}): A(Vector2 ops) != B(scalar) {a_b}; A != NC(no contraction possible) {a_nc}; B != NC {b_nc}");
            string path = Environment.GetEnvironmentVariable("SURFACE_EDGES_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllLines(Path.GetFullPath(path), report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void Run()
        {
            Application.logMessageReceived += OnLog;
            try
            {
                string only = Environment.GetEnvironmentVariable("SURFACE_EDGES_HOLES");
                var want = string.IsNullOrEmpty(only) ? null : only.Split(',').Select(t => int.Parse(t.Trim())).ToHashSet();
                int identical = 0, total = 0;
                GolfSurfaceEdges.BeforeApplyForTests = BeforeApply;
                foreach (var hole in Course.Course.All().SelectMany(c => c.Holes))
                {
                    if (want != null && !want.Contains(hole.Number)) continue;
                    total++; currentHole = hole.Number.ToString();
                    appliesSeen = 0; appliesLogged = 0; perCall.Clear(); allIdentical = true;
                    var parent = new GameObject("edgecheck");
                    try
                    {
                    var sw = Stopwatch.StartNew();
                    inBuild = true;
                    var view = HoleView.Build(hole, parent.transform);
                    inBuild = false;
                    double buildMs = sw.Elapsed.TotalMilliseconds;
                    // The finished hole, compared once more, then the same GameObject applied twice (second must be reused).
                    GolfSurfaceEdges.BeforeApplyForTests = null;
                    var final = Compare(view.gameObject);
                    sw.Restart(); GolfSurfaceEdges.Apply(view.gameObject); double apply1 = sw.Elapsed.TotalMilliseconds;
                    var tex1 = Shader.GetGlobalTexture("_GolfEdgeMap");
                    sw.Restart(); GolfSurfaceEdges.Apply(view.gameObject); double apply2 = sw.Elapsed.TotalMilliseconds;
                    var tex2 = Shader.GetGlobalTexture("_GolfEdgeMap");
                    bool reused = tex1 == tex2;
                    // The published texture is 256x256 (its pixels cannot be read back: Apply makes it no-longer-readable).
                    var reference = Reference(view.gameObject);
                    bool publishedOk = tex2 is Texture2D published && published.width == Size && published.height == Size && published.name == "Golf original outline factors";
                    var bounds = Shader.GetGlobalVector("_GolfEdgeBounds");
                    bool boundsOk = reference != null && Bits(bounds.x) == Bits(reference.Bounds.x) && Bits(bounds.y) == Bits(reference.Bounds.y) && Bits(bounds.z) == Bits(reference.Bounds.z) && Bits(bounds.w) == Bits(reference.Bounds.w);
                    bool holeOk = allIdentical && final.Identical && publishedOk && boundsOk && reused;
                    if (holeOk) identical++;
                    Say($"hole {hole.Number,2} {hole.Name,-18} IDENTICAL {Yes(holeOk)} | Build {buildMs:F0} ms | Apply calls in Build {appliesSeen}, recomputed {appliesLogged}, reused {appliesSeen - appliesLogged}");
                    foreach (var line in perCall) Say(line);
                    Say($"    finished hole: equal {Yes(final.Identical)} | old {final.OldMs:F0} ms ({final.OldCpu:F0} cpu-ms) | new {final.NewMs:F0} ms ({final.NewCpu:F0} cpu-ms) (best of 3 {final.NewBestMs:F0}; {final.Phases}) | segments {final.SegCounts} | {final.Detail}");
                    Say($"    Apply on the finished hole: first {apply1:F0} ms, second {apply2:F2} ms (texture reused {Yes(reused)}); published texture is the 256x256 edge map {Yes(publishedOk)}, published bounds == reference {Yes(boundsOk)}");
                    }
                    catch (Exception e) { UnityEngine.Debug.LogException(e); Say($"hole {hole.Number} EXCEPTION {e.GetType().Name}: {e.Message}"); }
                    finally
                    {
                        GolfSurfaceEdges.BeforeApplyForTests = BeforeApply;
                        UnityEngine.Object.DestroyImmediate(parent);
                        Physics.SyncTransforms();
                    }
                }
                Say($"RESULT {identical}/{total} holes identical");
            }
            catch (Exception e) { UnityEngine.Debug.LogException(e); Say("EXCEPTION " + e); }
            finally { GolfSurfaceEdges.BeforeApplyForTests = null; Application.logMessageReceived -= OnLog; }
            string path = Environment.GetEnvironmentVariable("SURFACE_EDGES_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllLines(Path.GetFullPath(path), report);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
