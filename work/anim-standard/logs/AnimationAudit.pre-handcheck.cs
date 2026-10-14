#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// ANIMATION STANDARD gate (work/anim-standard/STANDARD.md). Plays EVERY clip of a hero (tennis: the driver's slots = gameplay, reactions,
    /// emotes, intros; the empty-handed HeroMoves set used by golf and the plaza; golf: the golfer's clips) frame by frame in play mode, with
    /// every runtime garment component running as in the game (arm skinning, grip, pose correctives, KitV2 correctives, cover masks), and
    /// measures on the baked meshes after LateUpdate:
    ///   HAND_IN   hand / forearm / racket points inside the CLOTHED torso, legs or head (skin + local cloth thickness), mm
    ///   HOLE      skin that is hidden under a garment (not drawn) left uncovered by every garment = a see-through hole, vertex count
    ///   POKE      drawn skin that was under cloth at rest and now sits outside it with the cloth behind = skin through cloth, vertex count
    ///   LINING    garment inner-wall vertices that end up outside the outer surface (the dark slash look), vertex count
    ///   FOLD      garment triangles flipped against their own skinned normals or crushed below 20 % of their rest area, count
    /// Writes <out>/<context>/audit.json (per clip: worst value + frame per metric, per-frame series) and close-ups of every failing clip's
    /// worst frame.   AA_OUT=<dir> AA_CONTEXTS=tennis_male,tennis_female,golf_male,golf_female AA_FPS=30 AA_CLIPS=<substring filter>
    ///   Unity -batchmode -projectPath <clone> -executeMethod GolfArcade.EditorTools.AnimationAudit.Run      (graphics on: it renders proof)
    [InitializeOnLoad]
    public static class AnimationAudit
    {
        // ---------------------------------------------------------------- thresholds (the standard's numbers; see STANDARD.md)
        public const float HandInFail = 8f;   // hand vertices deeper than HandDeepMm + racket edges crossing cloth or skin
        public const float HandDeepMm = 12f;
        public const int HoleFail = 6, PokeFail = 12, LiningFail = 30, FoldFail = 400;   // FOLD is reported, not gated (the hem band folds by design)
        const float CoverRay = .08f, RestCoverRay = .06f;

        const string Flag = "AnimationAudit";
        static IEnumerator script; static int lastFrame = -1;
        static AnimationAudit() { EditorApplication.update += Tick; }
        static string Env(string k, string d) => Environment.GetEnvironmentVariable(k) ?? d;
        public static void Run()
        {
            script = null; lastFrame = -1;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (script == null) script = Main();
            try { if (!script.MoveNext()) Done(0); } catch (Exception e) { Debug.LogException(e); Done(1); }
        }
        static void Done(int c) { SessionState.SetBool(Flag, false); if (Application.isBatchMode) EditorApplication.Exit(c); else EditorApplication.isPlaying = false; }
        static string F(float x) => x.ToString("0.###", CultureInfo.InvariantCulture);

        // ---------------------------------------------------------------- the poser: sample in LateUpdate before every garment component
        [DefaultExecutionOrder(-200)]
        public sealed class Poser : MonoBehaviour
        {
            public AnimationClip clip; public float time; public GameObject root; public MatchHeroLook look;
            public GolfSwingClearance golfClear; public bool golfSwing, golfDrive;
            void LateUpdate()
            {
                if (!clip || !root) return;
                clip.SampleAnimation(root, time);
                look?.body?.GetComponent<HeroArmSkinning>()?.Apply();
                if (golfClear != null && golfSwing) { if (golfDrive) golfClear.ApplyDriveFollowThrough(time); golfClear.Apply(); }
                if (golfClear != null && look) HeroGripPolish.ApplyGrip(look);
            }
        }
        /// runs after every garment LateUpdate (KitV2 1220, pose correctives 1200, polo 1210, grip 1160)
        [DefaultExecutionOrder(5000)]
        public sealed class Probe : MonoBehaviour
        {
            public Action measure;
            void LateUpdate() => measure?.Invoke();
        }

        // ---------------------------------------------------------------- a uniform grid of triangles
        sealed class Grid
        {
            readonly float cell; readonly Dictionary<long, List<int>> map = new Dictionary<long, List<int>>();
            public Vector3[] v; public int[] t; public int lastTri = -1;
            public Grid(float cell) { this.cell = cell; }
            static long Key(int x, int y, int z) => ((long)(x + 4096) << 26) | ((long)(y + 4096) << 13) | (long)(z + 4096);
            public void Build(Vector3[] verts, int[] tris, IList<int> triIds)
            {
                v = verts; t = tris; foreach (var l in map.Values) l.Clear();
                foreach (int i in triIds)
                {
                    Vector3 a = v[t[i * 3]], b = v[t[i * 3 + 1]], c = v[t[i * 3 + 2]];
                    var mn = Vector3.Min(a, Vector3.Min(b, c)); var mx = Vector3.Max(a, Vector3.Max(b, c));
                    int x0 = Mathf.FloorToInt(mn.x / cell), y0 = Mathf.FloorToInt(mn.y / cell), z0 = Mathf.FloorToInt(mn.z / cell);
                    int x1 = Mathf.FloorToInt(mx.x / cell), y1 = Mathf.FloorToInt(mx.y / cell), z1 = Mathf.FloorToInt(mx.z / cell);
                    for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) for (int z = z0; z <= z1; z++)
                    { long k = Key(x, y, z); if (!map.TryGetValue(k, out var l)) map[k] = l = new List<int>(); l.Add(i); }
                }
            }
            readonly HashSet<int> seen = new HashSet<int>();
            public IEnumerable<int> Near(Vector3 p, float r)
            {
                seen.Clear();
                int x0 = Mathf.FloorToInt((p.x - r) / cell), y0 = Mathf.FloorToInt((p.y - r) / cell), z0 = Mathf.FloorToInt((p.z - r) / cell);
                int x1 = Mathf.FloorToInt((p.x + r) / cell), y1 = Mathf.FloorToInt((p.y + r) / cell), z1 = Mathf.FloorToInt((p.z + r) / cell);
                for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) for (int z = z0; z <= z1; z++)
                    if (map.TryGetValue(Key(x, y, z), out var l)) foreach (int i in l) if (seen.Add(i)) yield return i;
            }
            /// the nearest point on the triangles within r: (triangle, point, barycentric), or -1
            public int Nearest(Vector3 p, float r, out Vector3 q, out Vector3 bary)
            {
                int best = -1; float bd = r * r; q = default; bary = default;
                foreach (int i in Near(p, r))
                {
                    var c = Closest(p, v[t[i * 3]], v[t[i * 3 + 1]], v[t[i * 3 + 2]], out var bc);
                    float d = (c - p).sqrMagnitude; if (d < bd) { bd = d; best = i; q = c; bary = bc; }
                }
                return best;
            }
            /// does the segment p -> p + d * len hit a triangle (Moller-Trumbore)? distance or -1
            public float Ray(Vector3 p, Vector3 d, float len)
            {
                float best = -1;
                var mid = p + d * (len * .5f);
                foreach (int i in Near(mid, len * .5f + cell))
                {
                    Vector3 a = v[t[i * 3]], b = v[t[i * 3 + 1]], c = v[t[i * 3 + 2]];
                    var e1 = b - a; var e2 = c - a; var h = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, h);
                    if (Mathf.Abs(det) < 1e-12f) continue;
                    float inv = 1 / det; var s = p - a; float u = Vector3.Dot(s, h) * inv; if (u < 0 || u > 1) continue;
                    var qv = Vector3.Cross(s, e1); float w = Vector3.Dot(d, qv) * inv; if (w < 0 || u + w > 1) continue;
                    float tt = Vector3.Dot(e2, qv) * inv; if (tt > 1e-5f && tt <= len && (best < 0 || tt < best)) { best = tt; lastTri = i; }
                }
                return best;
            }
            static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bc)
            {
                // Ericson, Real-Time Collision Detection 5.1.5
                var ab = b - a; var ac = c - a; var ap = p - a; float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0 && d2 <= 0) { bc = new Vector3(1, 0, 0); return a; }
                var bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0 && d4 <= d3) { bc = new Vector3(0, 1, 0); return b; }
                float vc = d1 * d4 - d3 * d2; if (vc <= 0 && d1 >= 0 && d3 <= 0) { float vv = d1 / (d1 - d3); bc = new Vector3(1 - vv, vv, 0); return a + vv * ab; }
                var cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0 && d5 <= d6) { bc = new Vector3(0, 0, 1); return c; }
                float vb = d5 * d2 - d1 * d6; if (vb <= 0 && d2 >= 0 && d6 <= 0) { float ww = d2 / (d2 - d6); bc = new Vector3(1 - ww, 0, ww); return a + ww * ac; }
                float va = d3 * d6 - d5 * d4; if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) { float ww = (d4 - d3) / ((d4 - d3) + (d5 - d6)); bc = new Vector3(0, 1 - ww, ww); return b + ww * (c - b); }
                float den = 1 / (va + vb + vc); float v2 = vb * den, w2 = vc * den; bc = new Vector3(1 - v2 - w2, v2, w2); return a + ab * v2 + ac * w2;
            }
        }

        // ---------------------------------------------------------------- one hero, prepared for measuring
        sealed class Subject
        {
            public string context; public GameObject root; public MatchHeroLook look; public SkinnedMeshRenderer body; public SkinnedMeshRenderer[] kit;
            public Transform racket; public MeshFilter[] racketMeshes;
            public int[] bodyTris; public bool[] drawn; public int[] obstacleTris; public int[] handPts; public int[] handSide; public bool[] sideExclude0, sideExclude1;
            public float[] clothOffset; public int[] hiddenPts; public int[] pokePts; public int[][] liningPts; public int[][] kitTris; public bool[][] kitOuterTri; public bool[][] kitInner; public bool[] bodyHead;
            public Vector3[][] kitRestN; public float[][] kitRestArea; public bool[][] kitRefSign; public bool[][] liningRefOk;
            public int[] handEdges; public int[] bareTris; public bool[] outFlip; public int[] handVerts;
            public Mesh bakedBody = new Mesh(); public Mesh[] bakedKit;
            public Vector3[] bv, bn; public Vector3[][] kv, kn;
            public Grid obst = new Grid(.04f), cloth = new Grid(.03f);
            public List<int> allClothTris = new List<int>(); public int[] clothTriKit, clothTriLocal;
            public int lastTri => outerGrid.lastTri; public Vector3[] clothV; public int[] clothT; public int[] clothOuterTris;
            public Grid outerGrid = new Grid(.03f);
        }

        static string BoneGroup(string n)
        {
            if (n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little") || n.EndsWith("Hand") || n.EndsWith("LowerArm")) return (n.StartsWith("Left") ? "L" : n.StartsWith("Right") ? "R" : (n.Contains("_8_") || n.Contains("_9_") || n.Contains("_10") ? "L" : "R")) + "fore";
            if (n.EndsWith("UpperArm") || n.EndsWith("Shoulder") || n.StartsWith("ArmBlend")) return (n.StartsWith("Left") ? "L" : n.StartsWith("Right") ? "R" : (n.Contains("_8_") || n.Contains("_9_") ? "L" : "R")) + "upper";
            if (n.Contains("Leg") || n.Contains("Foot") || n.Contains("Toes")) return "leg";
            if (n == "Head" || n == "Neck") return "head";
            return "torso";
        }

        static void Bake(SkinnedMeshRenderer r, Mesh m, out Vector3[] v, out Vector3[] n)
        {
            r.BakeMesh(m, true);
            var place = Matrix4x4.TRS(r.transform.position, r.transform.rotation, Vector3.one);
            v = m.vertices; n = m.normals;
            for (int i = 0; i < v.Length; i++) { v[i] = place.MultiplyPoint3x4(v[i]); if (i < n.Length) n[i] = place.MultiplyVector(n[i]).normalized; }
        }

        static Subject Prepare(string context, GameObject root)
        {
            var s = new Subject { context = context, root = root, look = root.GetComponent<MatchHeroLook>() };
            s.body = s.look.body;
            s.kit = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r && r.sharedMesh && r.gameObject.activeInHierarchy && r.enabled && r.name.StartsWith("Kit_")).ToArray();
            s.bakedKit = s.kit.Select(_ => new Mesh()).ToArray();
            if (s.look.racketGrip && context.StartsWith("tennis"))
            {
                s.racket = s.look.racketGrip;
                s.racketMeshes = s.racket.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && f.name != "Collider_Racket" && f.GetComponent<MeshRenderer>() && f.GetComponent<MeshRenderer>().enabled).ToArray();
            }
            // classify body vertices by their bone groups
            var mesh = s.body.sharedMesh; var bw = mesh.boneWeights; var groups = s.body.bones.Select(b => b ? BoneGroup(b.name) : "torso").ToArray();
            int nv = mesh.vertexCount; var g = new string[nv]; var wL = new float[nv]; var wR = new float[nv];
            for (int i = 0; i < nv; i++)
            {
                var acc = new Dictionary<string, float>();
                void Add(int bi, float w) { if (w <= 0) return; var k = groups[bi]; acc[k] = (acc.TryGetValue(k, out var x) ? x : 0) + w; if (k.StartsWith("L")) wL[i] += w; if (k.StartsWith("R")) wR[i] += w; }
                Add(bw[i].boneIndex0, bw[i].weight0); Add(bw[i].boneIndex1, bw[i].weight1); Add(bw[i].boneIndex2, bw[i].weight2); Add(bw[i].boneIndex3, bw[i].weight3);
                g[i] = acc.OrderByDescending(kv => kv.Value).First().Key;
            }
            s.bodyTris = mesh.triangles;   // the RUNTIME mesh: skin hidden under a garment has no triangles here
            s.drawn = new bool[nv]; foreach (int i in s.bodyTris) s.drawn[i] = true;
            var obst = new List<int>(); s.sideExclude0 = new bool[s.bodyTris.Length / 3]; s.sideExclude1 = new bool[s.bodyTris.Length / 3];
            for (int f = 0; f < s.bodyTris.Length / 3; f++)
            {
                int a = s.bodyTris[f * 3], b = s.bodyTris[f * 3 + 1], c = s.bodyTris[f * 3 + 2];
                bool limb = g[a].EndsWith("fore") || g[a].EndsWith("upper") || g[b].EndsWith("fore") || g[b].EndsWith("upper") || g[c].EndsWith("fore") || g[c].EndsWith("upper");
                if (limb) continue;
                obst.Add(f);
                s.sideExclude0[f] = wL[a] + wL[b] + wL[c] > .45f;    // the shoulder / armpit of the left arm: the left hand may lie in it
                s.sideExclude1[f] = wR[a] + wR[b] + wR[c] > .45f;
            }
            s.obstacleTris = obst.ToArray();
            var hp = new List<int>(); var hs = new List<int>();
            for (int i = 0; i < nv; i += 2) if (s.drawn[i] && g[i].EndsWith("fore")) { hp.Add(i); hs.Add(g[i][0] == 'L' ? 0 : 1); }
            s.handPts = hp.ToArray(); s.handSide = hs.ToArray();
            var edges = new HashSet<long>(); var isFore = new bool[nv]; for (int i = 0; i < nv; i++) isFore[i] = g[i].EndsWith("fore");
            for (int f = 0; f < s.bodyTris.Length / 3; f++)
                for (int e = 0; e < 3; e++)
                {
                    int a = s.bodyTris[f * 3 + e], b = s.bodyTris[f * 3 + (e + 1) % 3];
                    if (!isFore[a] || !isFore[b]) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; edges.Add(key);
                }
            s.handEdges = edges.SelectMany(k => new[] { (int)(k >> 32), (int)(k & 0xffffffff) }).ToArray();
            // the reference (current) pose: cloth thickness over every obstacle vertex, the hidden skin, the covered drawn skin, the garments' walls
            Bake(s.body, s.bakedBody, out s.bv, out s.bn);
            s.kv = new Vector3[s.kit.Length][]; s.kn = new Vector3[s.kit.Length][];
            for (int k = 0; k < s.kit.Length; k++) Bake(s.kit[k], s.bakedKit[k], out s.kv[k], out s.kn[k]);
            BuildCloth(s);
            s.clothOffset = new float[nv];
            for (int i = 0; i < nv; i++) { float d = s.cloth.Ray(s.bv[i] + s.bn[i] * .001f, s.bn[i], RestCoverRay); s.clothOffset[i] = d > 0 ? d + .001f : 0; }
            s.hiddenPts = Enumerable.Range(0, nv).Where(i => !s.drawn[i] && (g[i] == "torso" || g[i] == "leg") && s.clothOffset[i] > 0).Where((x, j) => j % 3 == 0).ToArray();
            s.pokePts = Enumerable.Range(0, nv).Where(i => s.drawn[i] && s.clothOffset[i] > 0 && (g[i] == "torso" || g[i] == "leg" || g[i].EndsWith("upper"))).Where((x, j) => j % 3 == 0).ToArray();
            // garment walls: a vertex whose normal faces the skin under it is inner wall
            s.kitInner = new bool[s.kit.Length][]; s.liningPts = new int[s.kit.Length][]; s.kitTris = new int[s.kit.Length][]; s.kitOuterTri = new bool[s.kit.Length][]; s.kitRestN = new Vector3[s.kit.Length][]; s.kitRestArea = new float[s.kit.Length][];
            s.obst.Build(s.bv, s.bodyTris, Enumerable.Range(0, s.bodyTris.Length / 3).ToList());
            for (int k = 0; k < s.kit.Length; k++)
            {
                var kv = s.kv[k]; var kn = s.kn[k]; var tris = s.bakedKit[k].triangles; s.kitTris[k] = tris;
                var inner = new bool[kv.Length];
                for (int i = 0; i < kv.Length; i++)
                {
                    int f = s.obst.Nearest(kv[i], .08f, out var q, out _);
                    inner[i] = f >= 0 && Vector3.Dot(kn[i], kv[i] - q) < 0;
                }
                s.kitInner[k] = inner;
                s.liningPts[k] = Enumerable.Range(0, kv.Length).Where(i => inner[i]).Where((x, j) => j % 7 == 0).ToArray();
                int nt = tris.Length / 3; s.kitOuterTri[k] = new bool[nt]; s.kitRestN[k] = new Vector3[nt]; s.kitRestArea[k] = new float[nt];
                for (int f = 0; f < nt; f++)
                {
                    int a = tris[f * 3], b = tris[f * 3 + 1], c = tris[f * 3 + 2];
                    s.kitOuterTri[k][f] = !(inner[a] && inner[b] && inner[c]);
                    var cr = Vector3.Cross(kv[b] - kv[a], kv[c] - kv[a]); s.kitRestArea[k][f] = cr.magnitude * .5f; s.kitRestN[k][f] = cr.normalized;
                }
            }
            // ID-pass wall flags: the side of a vertex that faces the skin is the lining side. Rays along +n and -n against the FULL body
            // (the runtime body has the covered skin removed): the nearer body hit says where the skin is. No hit either way: the old test.
            {
                var srcF = typeof(MatchHeroLook).GetField("kitV2CoverSource", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var src = srcF?.GetValue(s.look) as Mesh; var full = src && src.vertexCount == nv ? src.triangles : s.bodyTris;
                // in the BIND pose (mesh space), where the arms are away from the body and the hands are not in front of the belly
                var bm = src && src.vertexCount == nv ? src : s.body.sharedMesh; var M = s.body.transform.localToWorldMatrix;
                var bvB = bm.vertices.Select(x => M.MultiplyPoint3x4(x)).ToArray();
                var fg = new Grid(.04f); fg.Build(bvB, full, Enumerable.Range(0, full.Length / 3).ToList());
                for (int k = 0; k < s.kit.Length; k++)
                {
                    var km = s.kit[k].sharedMesh; var Mk = s.kit[k].transform.localToWorldMatrix;
                    if (km.vertexCount != s.kv[k].Length) { Debug.Log($"[Breakdown] wall flags: {s.kit[k].name} vertex count differs, old test kept"); continue; }
                    var kv = km.vertices.Select(x => Mk.MultiplyPoint3x4(x)).ToArray(); var kn = km.normals.Select(x => Mk.MultiplyVector(x).normalized).ToArray(); var inner2 = new bool[kv.Length];
                    for (int i = 0; i < kv.Length; i++)
                    {
                        float dp = fg.Ray(kv[i] + kn[i] * .0005f, kn[i], .15f), dm = fg.Ray(kv[i] - kn[i] * .0005f, -kn[i], .15f);
                        inner2[i] = dp < 0 && dm < 0 ? s.kitInner[k][i] : dp < 0 ? false : dm < 0 ? true : dp < dm;
                    }
                    s.kitInner[k] = inner2;
                }
                s.bodyHead = g.Select(x => x == "head").ToArray();
                Debug.Log($"[Breakdown] bind bounds body {new Bounds(bvB.Aggregate(Vector3.zero, (x, y) => x + y) / bvB.Length, Vector3.zero)} kit0 {(s.kit.Length > 0 ? s.kit[0].sharedMesh.bounds.center.ToString() : "-")}");
                Debug.Log($"[Breakdown] wall flags: full body tris {full.Length / 3} (source {(src ? src.name : "none")}), inner share " + string.Join(" ", s.kitInner.Select((a, k) => $"{s.kit[k].name}={a.Count(x => x) / (float)a.Length:0.00}")));
            }
            // reference state (READY): which triangles agree with their vertex normals, which inner-wall points have the outer wall in front,
            // and the bare skin (drawn, not under any cloth) the hands are checked against
            BuildCloth(s);
            s.kitRefSign = new bool[s.kit.Length][]; s.liningRefOk = new bool[s.kit.Length][];
            for (int k = 0; k < s.kit.Length; k++)
            {
                var kv = s.kv[k]; var kn = s.kn[k]; var tris = s.kitTris[k]; s.kitRefSign[k] = new bool[tris.Length / 3];
                for (int f = 0; f < tris.Length / 3; f++) { int a = tris[f * 3], b = tris[f * 3 + 1], c = tris[f * 3 + 2]; s.kitRefSign[k][f] = Vector3.Dot(Vector3.Cross(kv[b] - kv[a], kv[c] - kv[a]), kn[a] + kn[b] + kn[c]) >= 0; }
                s.liningRefOk[k] = s.liningPts[k].Select(i => s.outerGrid.Ray(kv[i] - kn[i] * .0002f, -kn[i], .012f) >= 0).ToArray();
            }
            s.obst.Build(s.bv, s.bodyTris, Enumerable.Range(0, s.bodyTris.Length / 3).ToList());
            s.outFlip = new bool[s.clothT.Length / 3];
            for (int f = 0; f < s.outFlip.Length; f++)
            {
                Vector3 a = s.clothV[s.clothT[f * 3]], b = s.clothV[s.clothT[f * 3 + 1]], c = s.clothV[s.clothT[f * 3 + 2]], m = (a + b + c) / 3;
                var n = Vector3.Cross(b - a, c - a); int g2 = s.obst.Nearest(m, .10f, out var q, out _);
                s.outFlip[f] = g2 >= 0 && Vector3.Dot(n, m - q) < 0;
            }
            s.handVerts = s.handEdges.Distinct().ToArray();
            s.bareTris = s.obstacleTris.Where(f => s.clothOffset[s.bodyTris[f * 3]] == 0 && s.clothOffset[s.bodyTris[f * 3 + 1]] == 0 && s.clothOffset[s.bodyTris[f * 3 + 2]] == 0).ToArray();
            return s;
        }

        static void BuildCloth(Subject s)
        {
            var V = new List<Vector3>(); var T = new List<int>(); var outer = new List<int>(); var tk = new List<int>(); int tri = 0;
            for (int k = 0; k < s.kit.Length; k++)
            {
                int baseV = V.Count; V.AddRange(s.kv[k]); var tris = s.bakedKit[k].triangles;
                for (int f = 0; f < tris.Length / 3; f++)
                {
                    T.Add(baseV + tris[f * 3]); T.Add(baseV + tris[f * 3 + 1]); T.Add(baseV + tris[f * 3 + 2]);
                    if (s.kitOuterTri == null || s.kitOuterTri[k] == null || s.kitOuterTri[k][f]) outer.Add(tri);
                    tk.Add(k); tri++;
                }
            }
            s.clothV = V.ToArray(); s.clothT = T.ToArray(); s.clothTriKit = tk.ToArray();
            s.cloth.Build(s.clothV, s.clothT, Enumerable.Range(0, tri).ToList());
            s.clothOuterTris = outer.ToArray();
            s.outerGrid.Build(s.clothV, s.clothT, s.clothOuterTris);
        }

        sealed class FrameResult { public float handIn; public string handWhere = ""; public int hole, poke, lining, fold; public float handDepth; public List<Vector3> pHand = new List<Vector3>(), pHole = new List<Vector3>(), pPoke = new List<Vector3>(), pLining = new List<Vector3>(), pFold = new List<Vector3>(); public Dictionary<string, int> handBy = new Dictionary<string, int>(); }

        static FrameResult Measure(Subject s, bool racketChecked)
        {
            var r = new FrameResult();
            Bake(s.body, s.bakedBody, out s.bv, out s.bn);
            for (int k = 0; k < s.kit.Length; k++) Bake(s.kit[k], s.bakedKit[k], out s.kv[k], out s.kn[k]);
            BuildCloth(s);
            s.obst.Build(s.bv, s.bodyTris, s.obstacleTris);
            // HAND_IN: hand / forearm mesh edges (and racket edges) that CROSS a garment's outer surface or bare non-arm skin = a hand through cloth / body.
            // (Edges, not points: a hand resting on the shirt touches it, a hand through it crosses it.) The armpit of the same arm is not an obstacle.
            s.obst.Build(s.bv, s.bodyTris, s.bareTris);
            string Hit(Vector3 p0, Vector3 p1)
            {
                var dv = p1 - p0; float len = dv.magnitude; if (len < 1e-6f) return null; dv /= len;
                float tc = s.outerGrid.Ray(p0, dv, len); if (tc >= 0) { int tri = s.outerGrid.lastTri; return "cloth:" + s.kit[s.clothTriKit[tri]].name; }
                if (s.obst.Ray(p0, dv, len) >= 0) return "skin";
                return null;
            }
            int handCross = 0, racketCross = 0;
            void Count(string who, Vector3 p0, Vector3 p1, ref int counter)
            {
                var h = Hit(p0, p1); if (h == null) return; counter++;
                string key = who + ">" + h; r.handBy[key] = (r.handBy.TryGetValue(key, out var c) ? c : 0) + 1;
                if (r.pHand.Count < 300) r.pHand.Add((p0 + p1) * .5f);
            }
            // depth of each hand / forearm vertex behind the nearest cloth outer surface (oriented away from the body at READY) or inside bare skin;
            // shallow contact (a forearm resting on the shirt) is allowed, a hand DEEPER than HandDeepMm is a hand through cloth / body
            float worst = 0; string worstWho = "";
            void Depth(Vector3 p, string who)
            {
                int f = s.outerGrid.Nearest(p, .05f, out var q, out _);
                if (f >= 0)
                {
                    Vector3 a = s.clothV[s.clothT[f * 3]], b = s.clothV[s.clothT[f * 3 + 1]], c = s.clothV[s.clothT[f * 3 + 2]];
                    var n = Vector3.Cross(b - a, c - a).normalized; if (s.outFlip[f]) n = -n;
                    float d = -Vector3.Dot(p - q, n) * 1000f;
                    if (d > HandDeepMm) { handCross++; string key = who + ">cloth:" + s.kit[s.clothTriKit[f]].name; r.handBy[key] = (r.handBy.TryGetValue(key, out var cc) ? cc : 0) + 1; if (r.pHand.Count < 300) r.pHand.Add(p); }
                    if (d > worst) { worst = d; worstWho = who + ">cloth:" + s.kit[s.clothTriKit[f]].name; }
                }
                int g2 = s.obst.Nearest(p, .05f, out var q2, out var bc2);
                if (g2 >= 0)
                {
                    int ia = s.bodyTris[g2 * 3], ib = s.bodyTris[g2 * 3 + 1], ic = s.bodyTris[g2 * 3 + 2];
                    var n2 = (s.bn[ia] * bc2.x + s.bn[ib] * bc2.y + s.bn[ic] * bc2.z).normalized;
                    float d2 = -Vector3.Dot(p - q2, n2) * 1000f;
                    if (d2 > HandDeepMm) { handCross++; string key = who + ">skin"; r.handBy[key] = (r.handBy.TryGetValue(key, out var cc) ? cc : 0) + 1; if (r.pHand.Count < 300) r.pHand.Add(p); }
                    if (d2 > worst) { worst = d2; worstWho = who + ">skin"; }
                }
            }
            foreach (int i in s.handVerts) Depth(s.bv[i], "hand");
            r.handDepth = worst;
            if (racketChecked && s.racketMeshes != null)
                foreach (var mf in s.racketMeshes)
                {
                    var M = mf.transform.localToWorldMatrix; var vs = mf.sharedMesh.vertices; var ts = mf.sharedMesh.triangles;
                    for (int f = 0; f < ts.Length; f += 9) Count("racket", M.MultiplyPoint3x4(vs[ts[f]]), M.MultiplyPoint3x4(vs[ts[f + 1]]), ref racketCross);
                }
            r.handWhere = string.Join(" ", r.handBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key + "=" + kv.Value));
            r.handIn = handCross + racketCross;
            // HOLE: hidden skin no garment covers any more
            foreach (int i in s.hiddenPts) if (s.cloth.Ray(s.bv[i] + s.bn[i] * .0005f, s.bn[i], CoverRay) < 0 && s.cloth.Ray(s.bv[i], -s.bn[i], .01f) < 0) { r.hole++; if (r.pHole.Count < 300) r.pHole.Add(s.bv[i]); }
            // POKE: drawn skin that was under the cloth and now pokes out of it (no cloth ahead, the cloth just behind)
            foreach (int i in s.pokePts) if (s.cloth.Ray(s.bv[i] + s.bn[i] * .0005f, s.bn[i], CoverRay) < 0 && s.cloth.Ray(s.bv[i], -s.bn[i], .02f) > 0) { r.poke++; if (r.pPoke.Count < 300) r.pPoke.Add(s.bv[i]); }
            // LINING + FOLD per garment
            int vbase = 0;
            for (int k = 0; k < s.kit.Length; k++)
            {
                var kv = s.kv[k]; var kn = s.kn[k];
                for (int j = 0; j < s.liningPts[k].Length; j++)
                {
                    // the inner wall's normal faces the skin: outward is -n. Where the outer wall WAS in front of it at READY it must still be.
                    if (!s.liningRefOk[k][j]) continue;
                    int i = s.liningPts[k][j];
                    if (s.outerGrid.Ray(kv[i] - kn[i] * .0002f, -kn[i], .012f) < 0) { r.lining++; if (r.pLining.Count < 300) r.pLining.Add(kv[i]); }
                }
                var tris = s.kitTris[k];
                for (int f = 0; f < tris.Length / 3; f++)
                {
                    if (!s.kitOuterTri[k][f]) continue;
                    int a = tris[f * 3], b = tris[f * 3 + 1], c = tris[f * 3 + 2];
                    var cr = Vector3.Cross(kv[b] - kv[a], kv[c] - kv[a]); float area = cr.magnitude * .5f;
                    var vn = kn[a] + kn[b] + kn[c];
                    if ((Vector3.Dot(cr, vn) >= 0) != s.kitRefSign[k][f] && area > 1e-7f || area < s.kitRestArea[k][f] * .2f) { r.fold++; if (r.pFold.Count < 300 && r.fold % 3 == 0) r.pFold.Add((kv[a] + kv[b] + kv[c]) / 3); }
                }
                vbase += kv.Length;
            }
            return r;
        }

        // ---------------------------------------------------------------- contexts and clips
        static GameObject BuildTennis(bool female)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Tennis/Customization/Player" + (female ? "Female" : "Male") + ".prefab");
            var go = Object.Instantiate(prefab); go.name = "Audit " + prefab.name;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var d = go.GetComponent<HeroTennisDriver>(); if (d) { d.ResolvePerformanceClips(); d.enabled = false; }
            return go;
        }
        static HeroGolfer lastGolfer;
        static GameObject BuildGolf(bool female)
        {
            var golfer = HeroGolfer.Build(null, new HeroLook { Female = female, Haircut = (int)HeroGolfer.Haircut.Bald, Skin = female ? new Color(.953f, .855f, .788f) : new Color(.949f, .816f, .686f), HairColor = new Color(.35f, .25f, .18f) });
            golfer.Root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            lastGolfer = golfer;
            return golfer.Root;
        }
        static readonly Dictionary<string, string> HeldClub = new Dictionary<string, string> { { "Drive", "DRIVER" }, { "IronSwing", "IRON" }, { "HalfSwing", "IRON" }, { "Chip", "WEDGE" }, { "Putt", "PUTTER" } };
        static List<(string name, AnimationClip clip, bool racket)> ClipsFor(string context, GameObject root, bool female)
        {
            var list = new List<(string, AnimationClip, bool)>();
            var moves = Resources.LoadAll<AnimationClip>("HeroMoves/" + (female ? "Female" : "Male")).Where(c => !c.name.StartsWith("__preview__"));
            if (context.StartsWith("tennis"))
            {
                var d = root.GetComponent<HeroTennisDriver>();
                foreach (var sl in d.slots.Where(x => x.clip).GroupBy(x => x.clip).Select(gr => gr.First())) list.Add((sl.id + ":" + sl.clip.name, sl.clip, true));
                foreach (var c in moves) list.Add(("free:" + c.name, c, false));          // the plaza (and golf) empty-handed set on the tennis clothes
            }
            else
            {
                foreach (var c in Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(female)).Where(c => !c.name.StartsWith("__preview__"))) list.Add(("golf:" + c.name, c, false));
                foreach (var c in moves) list.Add(("free:" + c.name, c, false));
            }
            string filter = Env("AA_CLIPS", "");
            if (filter.Length > 0) list = list.Where(x => filter.Split(',').Any(f => x.Item1.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            return list;
        }

        static void Shot(Camera cam, string path, Vector3 from, Vector3 look, float fov)
        {
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(look - from)); cam.fieldOfView = fov;
            var rt = RenderTexture.GetTemporary(560, 560, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var tex = new Texture2D(560, 560, TextureFormat.RGB24, false); var act = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 560, 560), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToJPG(90)); Object.DestroyImmediate(tex);
        }

        static Material markMat;
        /// little unlit dots at the failing points: red = hand / racket through, magenta = hole, yellow = skin poke, cyan = lining, white = fold
        static GameObject Markers(FrameResult r)
        {
            var root = new GameObject("audit markers");
            if (!markMat) markMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            void Dot(Vector3 p, Color c, float size)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(g.GetComponent<Collider>()); g.transform.SetParent(root.transform, false);
                g.transform.position = p; g.transform.localScale = Vector3.one * size;
                var m = new Material(markMat); m.SetColor("_BaseColor", c); g.GetComponent<Renderer>().sharedMaterial = m;
            }
            foreach (var p in r.pHand) Dot(p, Color.red, .012f);
            foreach (var p in r.pHole) Dot(p, Color.magenta, .012f);
            foreach (var p in r.pPoke) Dot(p, Color.yellow, .01f);
            foreach (var p in r.pLining) Dot(p, Color.cyan, .008f);
            foreach (var p in r.pFold) Dot(p, Color.white, .006f);
            return root;
        }

        static IEnumerator Main()
        {
            string outDir = Path.GetFullPath(Env("AA_OUT", "anim_audit")); Directory.CreateDirectory(outDir);
            float fps = float.Parse(Env("AA_FPS", "30"), CultureInfo.InvariantCulture);
            // scene: a key light, an ambient, a close camera tagged MainCamera (garment LOD stays on the full meshes)
            var lightGo = new GameObject("Audit key"); var lt = lightGo.AddComponent<Light>(); lt.type = LightType.Directional; lt.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(40, 150, 0);
            RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
            var camGo = new GameObject("Audit camera") { tag = "MainCamera" }; var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.32f, .36f, .42f);
            cam.transform.position = new Vector3(0, 1.2f, 2.2f); cam.transform.LookAt(new Vector3(0, 1, 0));
            var summary = new StringBuilder();
            if (Env("AA_MODE", "audit") == "breakdown") { var bd = Breakdown(outDir, cam); while (bd.MoveNext()) yield return bd.Current; yield break; }
            foreach (var context in Env("AA_CONTEXTS", "tennis_male,tennis_female,golf_male,golf_female").Split(','))
            {
                bool female = context.EndsWith("female");
                var root = context.StartsWith("tennis") ? BuildTennis(female) : BuildGolf(female);
                for (int i = 0; i < 6; i++) yield return null;      // Awake / Start: materials, covers, garment components
                var look = root.GetComponent<MatchHeroLook>();
                var poser = root.AddComponent<Poser>(); poser.root = root; poser.look = look;
                var probe = root.AddComponent<Probe>();
                var clips = ClipsFor(context, root, female);
                // reference pose = the first clip's first frame (READY / Idle), after the garment components ran on it
                var refClip = clips.FirstOrDefault(c => c.name.Contains("Ready") || c.name.Contains("Idle")).clip ?? clips[0].clip;
                poser.clip = refClip; poser.time = 0; yield return null; yield return null;
                Subject s = null; probe.measure = () => { if (s == null) s = Prepare(context, root); }; yield return null; yield return null;
                if (s == null) throw new InvalidOperationException("prepare failed " + context);
                string cdir = Path.Combine(outDir, context); Directory.CreateDirectory(cdir); Directory.CreateDirectory(Path.Combine(cdir, "worst"));
                var json = new List<string>();
                Debug.Log($"[Audit] {context}: body v={s.body.sharedMesh.vertexCount} kit={string.Join("|", s.kit.Select(k => k.name + ":" + k.sharedMesh.name))} hand={s.handPts.Length} hidden={s.hiddenPts.Length} poke={s.pokePts.Length} clips={clips.Count}");
                foreach (var (name, clip, racket) in clips)
                {
                    if (s.racket) foreach (var mr in s.racket.GetComponentsInChildren<Renderer>(true)) mr.enabled = racket;
                    int n = Mathf.Max(2, Mathf.CeilToInt(clip.length * fps) + 1);
                    var frames = new List<FrameResult>();
                    for (int fi = 0; fi < n; fi++)
                    {
                        poser.clip = clip; poser.time = clip.length * fi / (n - 1);
                        FrameResult fr = null; probe.measure = () => fr = Measure(s, racket);
                        yield return null;
                        frames.Add(fr ?? new FrameResult());
                    }
                    probe.measure = null;
                    int Worst(Func<FrameResult, float> m) { int b = 0; for (int i = 1; i < frames.Count; i++) if (m(frames[i]) > m(frames[b])) b = i; return b; }
                    int wh = Worst(f => f.handIn), wo = Worst(f => f.hole), wp = Worst(f => f.poke), wl = Worst(f => f.lining), wf = Worst(f => f.fold);
                    var fails = new List<string>();
                    if (frames[wh].handIn > HandInFail) fails.Add($"HAND_IN {frames[wh].handWhere} depth {F(frames[wh].handDepth)}mm @{wh}");
                    if (frames[wo].hole > HoleFail) fails.Add($"HOLE {frames[wo].hole} @{wo}");
                    if (frames[wp].poke > PokeFail) fails.Add($"POKE {frames[wp].poke} @{wp}");
                    if (frames[wl].lining > LiningFail) fails.Add($"LINING {frames[wl].lining} @{wl}");
                    
                    string safe = name.Replace(":", "_").Replace("/", "_").Replace(" ", "_");
                    json.Add($"{{\"clip\":\"{name}\",\"frames\":{n},\"fps\":{F(fps)},\"fail\":[{string.Join(",", fails.Select(x => "\"" + x + "\""))}]," +
                             $"\"handIn\":[{string.Join(",", frames.Select(x => F(x.handIn)))}],\"hole\":[{string.Join(",", frames.Select(x => x.hole))}],\"poke\":[{string.Join(",", frames.Select(x => x.poke))}]," +
                             $"\"lining\":[{string.Join(",", frames.Select(x => x.lining))}],\"fold\":[{string.Join(",", frames.Select(x => x.fold))}]}}");
                    summary.AppendLine($"{context} {name}: {(fails.Count == 0 ? "PASS" : "FAIL " + string.Join(" | ", fails))}");
                    // proof: the worst frame of each failing metric, four views of the torso
                    foreach (var wi in fails.Select(x => int.Parse(x.Substring(x.LastIndexOf('@') + 1))).Distinct().Take(3))
                    {
                        poser.clip = clip; poser.time = clip.length * wi / (n - 1); yield return null; yield return null;
                        var markers = Markers(frames[wi]);
                        var chest = look.Bone(HumanBodyBones.Chest).position; var fwd = root.transform.forward; var right = root.transform.right;
                        foreach (var (v, dv) in new[] { ("front", fwd), ("back", -fwd), ("left", -right), ("right", right) })
                            Shot(cam, Path.Combine(cdir, "worst", $"{safe}_f{wi:000}_{v}.jpg"), chest + dv * 1.7f + Vector3.up * .1f, chest - Vector3.up * .15f, 42);
                        Object.Destroy(markers);
                    }
                    Debug.Log($"[Audit] {context} {name}: {(fails.Count == 0 ? "PASS" : string.Join(" | ", fails))}");
                }
                File.WriteAllText(Path.Combine(cdir, "audit.json"), "[\n" + string.Join(",\n", json) + "\n]\n");
                Object.Destroy(root); yield return null;
            }
            File.WriteAllText(Path.Combine(outDir, "summary.txt"), summary.ToString());
        }
        // ---------------------------------------------------------------- BREAKDOWN: frame-by-frame sheets for review
        // Per clip: K frames (about 6 per second, 6..16), each from front / back / left / right at 300x420, full body, with the measured
        // hole / poke / lining points projected into each view (hints.json) for the circling pass.
        // ID render (Hidden/AAId): body and garments as baked copies (garment vertex colour = inner wall), everything else with the ID
        // material swapped in; floor off, black background, no MSAA, linear target so the codes stay exact.
        static Material idMat;
        static void IdPass(Subject s, Camera cam, GameObject floor, string path)
        {
            if (!idMat) idMat = new Material(Shader.Find("Hidden/AAId"));
            var temps = new List<GameObject>(); var hidden = new List<Renderer>(); var swapped = new List<(Renderer r, Material[] m, MaterialPropertyBlock pb)>();
            void Temp(SkinnedMeshRenderer r, int id, bool[] inner)
            {
                var m = new Mesh(); r.BakeMesh(m, true);
                var col = new Color[m.vertexCount];
                if (inner != null && inner.Length == col.Length) for (int i = 0; i < col.Length; i++) col[i] = inner[i] ? Color.white : Color.black;
                m.colors = col;
                var go = new GameObject("AAId temp") { layer = r.gameObject.layer }; go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                go.AddComponent<MeshFilter>().sharedMesh = m; var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = Enumerable.Repeat(idMat, Mathf.Max(1, m.subMeshCount)).ToArray();
                var pb = new MaterialPropertyBlock(); pb.SetFloat("_Id", id); mr.SetPropertyBlock(pb);
                temps.Add(go); hidden.Add(r); r.enabled = false;
            }
            Temp(s.body, 1, s.bodyHead);
            for (int k = 0; k < s.kit.Length && k < 7; k++) if (s.kit[k].enabled && s.kit[k].gameObject.activeInHierarchy) Temp(s.kit[k], k + 2, s.kitInner?[k]);
            foreach (var r in s.root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || hidden.Contains(r) || r.GetComponent<ParticleSystem>()) continue;
                bool prop = false; for (var t = r.transform; t && t != s.root.transform; t = t.parent) if (t.name.StartsWith("CLUB_") || t.name.Contains("Racket") || t.name.Contains("racket")) { prop = true; break; }
                var pb0 = new MaterialPropertyBlock(); r.GetPropertyBlock(pb0); swapped.Add((r, r.sharedMaterials, pb0));
                r.sharedMaterials = Enumerable.Repeat(idMat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                var pb = new MaterialPropertyBlock(); pb.SetFloat("_Id", prop ? 10 : 9); r.SetPropertyBlock(pb);
            }
            floor.SetActive(false); var bg = cam.backgroundColor; cam.backgroundColor = Color.black;
            var rt = RenderTexture.GetTemporary(400, 560, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear, 1);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var tex = new Texture2D(400, 560, TextureFormat.RGB24, false, true); var act = RenderTexture.active;
            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 400, 560), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            cam.backgroundColor = bg; floor.SetActive(true);
            foreach (var (r, m, pb0) in swapped) { r.sharedMaterials = m; r.SetPropertyBlock(pb0.isEmpty ? null : pb0); }
            foreach (var r in hidden) r.enabled = true;
            foreach (var go in temps) { Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(go); }
        }

        static IEnumerator Breakdown(string outDir, Camera cam)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad); floor.transform.rotation = Quaternion.Euler(90, 0, 0); floor.transform.localScale = Vector3.one * 6;
            var fm = new Material(Shader.Find("Universal Render Pipeline/Lit")); fm.SetColor("_BaseColor", new Color(.30f, .45f, .36f)); floor.GetComponent<Renderer>().sharedMaterial = fm;
            foreach (var context in Env("AA_CONTEXTS", "tennis_male,tennis_female,golf_male,golf_female").Split(','))
            {
                bool female = context.EndsWith("female"), golf = context.StartsWith("golf");
                var root = golf ? BuildGolf(female) : BuildTennis(female);
                for (int i = 0; i < 6; i++) yield return null;
                var look = root.GetComponent<MatchHeroLook>();
                var poser = root.AddComponent<Poser>(); poser.root = root; poser.look = look;
                if (golf) poser.golfClear = new GolfSwingClearance(lastGolfer);
                var probe = root.AddComponent<Probe>();
                var clips = ClipsFor(context, root, female);
                var clubs = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("CLUB_")).ToArray();
                var refClip = clips.FirstOrDefault(c => c.name.Contains("Ready") || c.name.Contains("Idle")).clip ?? clips[0].clip;
                foreach (var c in clubs) c.gameObject.SetActive(false);
                poser.clip = refClip; poser.time = 0; yield return null; yield return null;
                Subject s = null; probe.measure = () => { if (s == null) s = Prepare(context, root); }; yield return null; yield return null;
                string cdir = Path.Combine(outDir, context); Directory.CreateDirectory(cdir);
                var index = new List<string>();
                foreach (var (name, clip, racket) in clips)
                {
                    string bare = name.Substring(name.IndexOf(':') + 1);
                    if (s.racket) foreach (var mr in s.racket.GetComponentsInChildren<Renderer>(true)) mr.enabled = racket;
                    string held = golf && name.StartsWith("golf:") && HeldClub.TryGetValue(bare, out var hc) ? hc : null;
                    foreach (var c in clubs) c.gameObject.SetActive(c.name == "CLUB_" + held);
                    poser.golfSwing = held != null; poser.golfDrive = bare == "Drive";
                    int K = Mathf.Clamp(Mathf.RoundToInt(clip.length * 6) + 1, 6, 16);
                    string safe = name.Replace(":", "_").Replace("/", "_").Replace(" ", "_");
                    string ddir = Path.Combine(cdir, safe); Directory.CreateDirectory(ddir);
                    for (int k = 0; k < K; k++)
                    {
                        float t = clip.length * k / (K - 1);
                        poser.clip = clip; poser.time = t;
                        yield return null; yield return null;
                        var hips = look.Bone(HumanBodyBones.Hips).position; var center = new Vector3(hips.x, .98f, hips.z);
                        var fwd = root.transform.forward; var right = root.transform.right;
                        foreach (var (v, dv) in new[] { ("front", fwd), ("back", -fwd), ("left", -right), ("right", right) })
                        {
                            var from = center + dv * 3.6f + Vector3.up * .25f;
                            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(center - from)); cam.fieldOfView = 34; cam.aspect = 400f / 560f;
                            var rt = RenderTexture.GetTemporary(400, 560, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
                            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                            var tex = new Texture2D(400, 560, TextureFormat.RGB24, false); var act = RenderTexture.active;
                            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 400, 560), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
                            File.WriteAllBytes(Path.Combine(ddir, $"f{k:00}_{v}.jpg"), tex.EncodeToJPG(92)); Object.DestroyImmediate(tex);
                            IdPass(s, cam, floor, Path.Combine(ddir, $"f{k:00}_{v}_id.png"));
                        }
                        // hand close-ups: each hand from the side of the body it is on, a little in front, 240x280
                        foreach (var (hn, hb, sgn) in new[] { ("handL", HumanBodyBones.LeftHand, -1f), ("handR", HumanBodyBones.RightHand, 1f) })
                        {
                            var hand = look.Bone(hb); if (!hand) continue;
                            var mid = hand.position + (hand.position - look.Bone(sgn < 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm).position).normalized * .06f;
                            // look at the hand from outside the body: away from the chest, a little from the front and above
                            var chestP = look.Bone(HumanBodyBones.Chest).position;
                            var away = Vector3.ProjectOnPlane(mid - chestP, Vector3.up); if (away.sqrMagnitude < 1e-4f) away = root.transform.right * sgn;
                            var dirv = (away.normalized * .6f + root.transform.forward * .7f + Vector3.up * .25f).normalized;
                            var from = mid + dirv * .85f;
                            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(mid - from)); cam.fieldOfView = 30; cam.aspect = 240f / 280f;
                            var rt = RenderTexture.GetTemporary(240, 280, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
                            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                            var tex = new Texture2D(240, 280, TextureFormat.RGB24, false); var act = RenderTexture.active;
                            RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 240, 280), 0, 0); tex.Apply(); RenderTexture.active = act; RenderTexture.ReleaseTemporary(rt);
                            File.WriteAllBytes(Path.Combine(ddir, $"f{k:00}_{hn}.jpg"), tex.EncodeToJPG(92)); Object.DestroyImmediate(tex);
                        }
                    }
                    index.Add($"{{\"clip\":\"{name}\",\"dir\":\"{safe}\",\"frames\":{K},\"length\":{F(clip.length)},\"racket\":{(racket ? "true" : "false")},\"held\":\"{held ?? ""}\"}}");
                    Debug.Log($"[Breakdown] {context} {name} frames={K}");
                }
                File.WriteAllText(Path.Combine(cdir, "index.json"), "[\n" + string.Join(",\n", index) + "\n]\n");
                File.WriteAllText(Path.Combine(cdir, "legend.json"), "{\"1\":\"body\"," + string.Join(",", s.kit.Select((r, k) => $"\"{k + 2}\":\"kit:{r.name}\"")) + ",\"9\":\"head\",\"10\":\"prop\"}\n");
                Object.Destroy(root); yield return null;
            }
        }
    }
}
#endif
