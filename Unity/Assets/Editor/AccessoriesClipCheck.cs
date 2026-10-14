using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// ACCESSORIES clipping check: poses a hero through its clips (10 samples a second) with the accessories on and measures, per clip and per accessory piece,
    ///   BODY   in ACC  : skin vertices inside the accessory solid (an arm / hand / racket-hand through the cap, the bag, the strap)
    ///   CLOTH  in ACC  : garment vertices (kit pieces) inside the accessory solid
    ///   ACC    in BODY : accessory vertices inside the body (a strap sunk into a shoulder, a glove into a wrist)
    /// as the number of vertices and the deepest one (mm; capped at 20 mm: the search radius), with the worst time. Skinning = Unity's own (BakeMesh), so the numbers are
    /// what the game draws. Nothing is saved to any asset.
    ///   Unity -batchmode -nographics -projectPath <clone> -executeMethod GolfArcade.EditorTools.AccessoriesClipCheck.RunBatch
    /// Env: ACC_CLIP_OUT=<dir>  ACC_CLIP_HEROES=...  ACC_CLIP_FILTER=<regex on clip names>  ACC_CLIP_BAG=0|1 (default 1: the bag is checked too)  ACC_CLIP_FPS=10
    public static class AccessoriesClipCheck
    {
        static readonly string[] AllHeroes = { "MatchHero_Male", "MatchHero_Female", "GolfKitHero_Male", "GolfKitHero_Female" };
        const string DefaultFilter = "Emote|Walk|Run|Intro|Idle|Cheer|Wave|FistPump|Swing|Drive|Putt|Chip|Celebrate|MatchWin|MatchLose|SadPoint|HitPerfect|MissWhiff";
        const float Cell = .02f, Cap = .02f;

        public static void RunBatch()
        {
            int code = 0;
            try { Run(); } catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        // ------------------------------------------------------------------ triangle soup with a hash grid
        sealed class Soup
        {
            public readonly Vector3[] P; public readonly int[] T; readonly Vector3[] N; readonly float orient;
            readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            readonly float maxY = float.MinValue;
            static long Key(int x, int y, int z) => ((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (long)(z + 4096);
            static int C(float v) => Mathf.FloorToInt(v / Cell);
            public Soup(Vector3[] p, int[] t)
            {
                P = p; T = t; N = new Vector3[t.Length / 3];
                double vol = 0;
                for (int i = 0; i < N.Length; i++)
                {
                    Vector3 a = p[t[i * 3]], b = p[t[i * 3 + 1]], c = p[t[i * 3 + 2]];
                    N[i] = Vector3.Cross(b - a, c - a).normalized;
                    maxY = Mathf.Max(maxY, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                    vol += Vector3.Dot(a, Vector3.Cross(b, c));
                    int x0 = C(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = C(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                    int y0 = C(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = C(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                    int z0 = C(Mathf.Min(a.z, Mathf.Min(b.z, c.z))), z1 = C(Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
                    for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) for (int z = z0; z <= z1; z++)
                    {
                        long k = Key(x, y, z);
                        if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                        l.Add(i);
                    }
                }
                orient = vol >= 0 ? 1f : -1f;      // outward normal = orient * cross(b-a, c-a)
            }

            /// inside a CLOSED shell by ray parity (a +Y ray from q): immune to the winding of individual parts
            public bool InsideParity(Vector3 q)
            {
                int cx = C(q.x), cz = C(q.z), y0 = C(q.y), y1 = C(maxY) + 1; int hits = 0;
                var seen = new HashSet<int>();
                for (int y = y0; y <= y1; y++)
                {
                    if (!grid.TryGetValue(Key(cx, y, cz), out var l)) continue;
                    foreach (int i in l)
                    {
                        if (!seen.Add(i)) continue;
                        Vector3 a = P[T[i * 3]], e1 = P[T[i * 3 + 1]] - a, e2 = P[T[i * 3 + 2]] - a;
                        Vector3 pv = new Vector3(e2.z, 0, -e2.x);                     // cross(dir=(0,1,0), e2)
                        float det = Vector3.Dot(e1, pv); if (Mathf.Abs(det) < 1e-12f) continue;
                        float inv = 1f / det; Vector3 tv = q - a; float u = Vector3.Dot(tv, pv) * inv; if (u < 0 || u > 1) continue;
                        Vector3 qv = Vector3.Cross(tv, e1); float v = qv.y * inv; if (v < 0 || u + v > 1) continue;
                        float tt = Vector3.Dot(e2, qv) * inv; if (tt > 1e-6f) hits++;
                    }
                }
                return (hits & 1) == 1;
            }

            public HashSet<long> OccupiedCells()
            {
                var s = new HashSet<long>(grid.Keys);
                return s;
            }

            /// distance to the nearest surface within `Cap` and whether the point is behind it (inside)
            public bool Query(Vector3 q, out float dist, out bool inside)
            {
                dist = float.MaxValue; inside = false; int best = -1; Vector3 bestCp = default;
                int cx = C(q.x), cy = C(q.y), cz = C(q.z);
                for (int x = cx - 1; x <= cx + 1; x++) for (int y = cy - 1; y <= cy + 1; y++) for (int z = cz - 1; z <= cz + 1; z++)
                {
                    if (!grid.TryGetValue(Key(x, y, z), out var l)) continue;
                    foreach (int i in l)
                    {
                        var cp = Closest(q, P[T[i * 3]], P[T[i * 3 + 1]], P[T[i * 3 + 2]]);
                        float d = (q - cp).sqrMagnitude;
                        if (d < dist) { dist = d; best = i; bestCp = cp; }
                    }
                }
                if (best < 0) { dist = float.MaxValue; return false; }
                dist = Mathf.Sqrt(dist);
                inside = Vector3.Dot(N[best] * orient, q - bestCp) < 0;
                return dist <= Cap;
            }

            static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 ab = b - a, ac = c - a, ap = p - a;
                float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0 && d2 <= 0) return a;
                Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0 && d4 <= d3) return b;
                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
                Vector3 cp_ = p - c; float d5 = Vector3.Dot(ab, cp_), d6 = Vector3.Dot(ac, cp_);
                if (d6 >= 0 && d5 <= d6) return c;
                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
                float va = d3 * d6 - d5 * d4;
                if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
                float den = 1f / (va + vb + vc), v = vb * den, w = vc * den;
                return a + ab * v + ac * w;
            }
        }

        sealed class Metric { public int count; public float maxMm; public float worstT = -1; public void Add(float depthMm, float t) { count++; if (depthMm > maxMm) { maxMm = depthMm; worstT = t; } } }
        sealed class Result { public Metric body = new Metric(), cloth = new Metric(), acc = new Metric(); public int frames; }

        static Vector3[] World(SkinnedMeshRenderer r, Mesh scratch, out int[] tris)
        {
            r.BakeMesh(scratch, true);
            var m = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
            var v = scratch.vertices; for (int i = 0; i < v.Length; i++) v[i] = m.MultiplyPoint3x4(v[i]);
            tris = scratch.triangles;
            return v;
        }

        static void Run()
        {
            var inv = CultureInfo.InvariantCulture;
            string outDir = Path.GetFullPath(Environment.GetEnvironmentVariable("ACC_CLIP_OUT") ?? Path.Combine(Application.dataPath, "../../work/accessories/clipcheck"));
            Directory.CreateDirectory(outDir);
            string filter = Environment.GetEnvironmentVariable("ACC_CLIP_FILTER") ?? DefaultFilter;
            var rx = new Regex(filter, RegexOptions.IgnoreCase);
            float fps = float.Parse(Environment.GetEnvironmentVariable("ACC_CLIP_FPS") ?? "10", inv);
            bool withBag = Environment.GetEnvironmentVariable("ACC_CLIP_BAG") != "0";
            string hf = Environment.GetEnvironmentVariable("ACC_CLIP_HEROES");
            var heroes = string.IsNullOrEmpty(hf) ? AllHeroes : hf.Split(',').Select(s => s.Trim()).Where(AllHeroes.Contains).ToArray();
            var keepWeights = QualitySettings.skinWeights; QualitySettings.skinWeights = SkinWeights.FourBones;
            var csv = new StringBuilder("hero,clip,piece,frames,body_in_acc_n,body_in_acc_mm,body_t,cloth_in_acc_n,cloth_in_acc_mm,cloth_t,acc_in_body_n,acc_in_body_mm,acc_t\n");
            var report = new StringBuilder();
            try
            {
                foreach (var id in heroes)
                {
                    var root = AccessoriesProof.BuildHero(id, out var idle);
                    var look = root.GetComponent<MatchHeroLook>();
                    var acc = HeroAccessories.For(look);
                    if (withBag) acc.SetCarry(true);
                    var clips = AccessoriesProof.ClipsFor(id, root);
                    var names = clips.Keys.Where(n => rx.IsMatch(n)).OrderBy(n => n, StringComparer.Ordinal).ToList();
                    report.AppendLine($"== {id}: accessories {string.Join(", ", new[] { AccessorySlot.Headwear, AccessorySlot.Glove, AccessorySlot.BackCarry }.Select(sl => sl + "=" + (acc.Item(sl) ?? "-") + (acc.IsVisible(sl) ? "" : " (hidden)")))}; {names.Count} clips: {string.Join(", ", names)}");
                    var scratch = new Mesh();
                    var bodyR = look.body;
                    // a glove wraps its own hand: skin inside / under the wall is the glove's job, not a clip. Triangles of that hand (+ the forearm cuff zone) are left out of the glove's checks.
                    bool[] OwnHand(string side)
                    {
                        var bw = bodyR.sharedMesh.boneWeights; var bones = bodyR.bones; var own = new bool[bw.Length];
                        bool Chain(int bi) { if (bi < 0 || bi >= bones.Length || !bones[bi]) return false; var n = bones[bi].name; return n.StartsWith(side) && (n.Contains("Hand") || n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little") || n.Contains("LowerArm")); }
                        for (int i = 0; i < bw.Length; i++)
                        {
                            float w = 0; var b = bw[i];
                            if (Chain(b.boneIndex0)) w += b.weight0; if (Chain(b.boneIndex1)) w += b.weight1; if (Chain(b.boneIndex2)) w += b.weight2; if (Chain(b.boneIndex3)) w += b.weight3;
                            own[i] = w > .2f;
                        }
                        return own;
                    }
                    var ownByPiece = new Dictionary<string, bool[]> { ["Acc_GloveL"] = OwnHand("Left"), ["Acc_GloveR"] = OwnHand("Right") };
                    var cloth = look.kit.Where(k => k && k.enabled && (k.name == "Kit_Top" || k.name == "Kit_Bottom")).ToArray();
                    foreach (var name in names)
                    {
                        var clip = clips[name];
                        int n = Mathf.Max(2, Mathf.CeilToInt(clip.length * fps) + 1);
                        var results = new Dictionary<string, Result>();
                        for (int f = 0; f < n; f++)
                        {
                            float t = Mathf.Min(clip.length, f / fps);
                            AccessoriesProof.Pose(root, idle, $"{name}@{t.ToString(inv)}", clips);
                            var pieces = acc.AllRenderers().Where(r => r.enabled).ToArray();
                            if (pieces.Length == 0) continue;
                            var bv = World(bodyR, scratch, out var bt);
                            Soup bodySoup = null; HashSet<long> bodyCells = null;
                            var garments = cloth.Select(g => (v: World(g, scratch, out var gt), t: gt)).ToArray();
                            // BASELINE for the glove: the same hand WITHOUT a glove (its skin vertices inside the rest of the body). If the glove's number equals this, the contact is the hand's own.
                            if (pieces.Any(p => p.name == "Acc_GloveL"))
                            {
                                var own = ownByPiece["Acc_GloveL"];
                                var keepTris = new List<int>(bt.Length);
                                for (int i = 0; i < bt.Length; i += 3) if (!(own[bt[i]] && own[bt[i + 1]] && own[bt[i + 2]])) { keepTris.Add(bt[i]); keepTris.Add(bt[i + 1]); keepTris.Add(bt[i + 2]); }
                                var rest = new Soup(bv, keepTris.ToArray());
                                if (!results.TryGetValue("BareHandL", out var bare)) results["BareHandL"] = bare = new Result();
                                bare.frames++;
                                for (int i = 0; i < bv.Length; i++)
                                    if (own[i] && rest.Query(bv[i], out float d, out bool inside) && inside) bare.acc.Add(d * 1000f, t);
                            }
                            foreach (var piece in pieces)
                            {
                                var av = World(piece, scratch, out var at);
                                var accSoup = new Soup(av, at);
                                if (!results.TryGetValue(piece.name, out var res)) results[piece.name] = res = new Result();
                                res.frames++;
                                var accCells = new HashSet<long>(av.Select(v => Cellof(v)));
                                bool glove = ownByPiece.TryGetValue(piece.name, out var own);
                                // body vertices near the accessory, inside it (a glove's own hand is inside it by definition)
                                if (!glove)
                                    foreach (var v in bv)
                                    {
                                        if (!NearCells(accCells, v)) continue;
                                        if (accSoup.Query(v, out float d, out _) && accSoup.InsideParity(v)) res.body.Add(d * 1000f, t);
                                    }
                                foreach (var (gv, _) in garments)
                                    foreach (var v in gv)
                                    {
                                        if (!NearCells(accCells, v)) continue;
                                        if (accSoup.Query(v, out float d, out _) && accSoup.InsideParity(v)) res.cloth.Add(d * 1000f, t);
                                    }
                                // accessory vertices near the body, inside it (the body mesh carries the closed head / torso / limbs)
                                if (bodySoup == null) { bodySoup = new Soup(bv, bt); bodyCells = new HashSet<long>(bv.Select(v => Cellof(v))); }
                                var soupFor = bodySoup; var cellsFor = bodyCells;
                                if (glove)
                                {
                                    // everything of the body EXCEPT the glove's own hand
                                    var keepTris = new List<int>(bt.Length);
                                    for (int i = 0; i < bt.Length; i += 3) if (!(own[bt[i]] && own[bt[i + 1]] && own[bt[i + 2]])) { keepTris.Add(bt[i]); keepTris.Add(bt[i + 1]); keepTris.Add(bt[i + 2]); }
                                    soupFor = new Soup(bv, keepTris.ToArray()); cellsFor = bodyCells;
                                }
                                foreach (var v in av)
                                {
                                    if (!NearCells(cellsFor, v)) continue;
                                    if (soupFor.Query(v, out float d, out bool inside) && inside) res.acc.Add(d * 1000f, t);
                                }
                            }
                        }
                        foreach (var kv in results.OrderBy(k => k.Key))
                        {
                            var r = kv.Value;
                            csv.AppendLine($"{id},{name},{kv.Key},{r.frames},{r.body.count},{r.body.maxMm.ToString("F1", inv)},{r.body.worstT.ToString("F2", inv)},{r.cloth.count},{r.cloth.maxMm.ToString("F1", inv)},{r.cloth.worstT.ToString("F2", inv)},{r.acc.count},{r.acc.maxMm.ToString("F1", inv)},{r.acc.worstT.ToString("F2", inv)}");
                        }
                        Debug.Log($"[AccessoriesClipCheck] {id} {name}: " + string.Join("; ", results.OrderBy(k => k.Key).Select(kv => $"{kv.Key} body {kv.Value.body.count}/{kv.Value.body.maxMm:F1}mm cloth {kv.Value.cloth.count}/{kv.Value.cloth.maxMm:F1}mm acc-in-body {kv.Value.acc.count}/{kv.Value.acc.maxMm:F1}mm")));
                    }
                    UnityEngine.Object.DestroyImmediate(scratch);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            finally { QualitySettings.skinWeights = keepWeights; }
            File.WriteAllText(Path.Combine(outDir, "clipcheck.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(outDir, "clipcheck.txt"), report.ToString());
            Debug.Log("[AccessoriesClipCheck] wrote " + outDir);
        }

        static long Cellof(Vector3 v) => ((long)(Mathf.FloorToInt(v.x / Cell) + 4096) << 40) | ((long)(Mathf.FloorToInt(v.y / Cell) + 4096) << 20) | (long)(Mathf.FloorToInt(v.z / Cell) + 4096);
        static bool NearCells(HashSet<long> cells, Vector3 v)
        {
            int cx = Mathf.FloorToInt(v.x / Cell), cy = Mathf.FloorToInt(v.y / Cell), cz = Mathf.FloorToInt(v.z / Cell);
            for (int x = cx - 1; x <= cx + 1; x++) for (int y = cy - 1; y <= cy + 1; y++) for (int z = cz - 1; z <= cz + 1; z++)
                if (cells.Contains(((long)(x + 4096) << 40) | ((long)(y + 4096) << 20) | (long)(z + 4096))) return true;
            return false;
        }
    }
}
