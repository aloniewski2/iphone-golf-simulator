using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// DRESS_MATCH_HEROES batch checks (gates 1c, 4, 5a-c, 7, 8c) on the saved prefabs:
    ///   Unity -batchmode -nographics -quit -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroKitTests.Run     (env MH_KIT_OUT = output dir, default ../work/hero-dressed/data)
    /// Writes unity_gates.json + unity_gates.txt.  Blender reference positions come from work/hero-dressed/data/kitpose_<sex>/ (scripts/dump_kit_poses.py).
    public static class MatchHeroKitTests
    {
        [Serializable] public class PieceRow
        {
            public string piece, material; public int vertices, triangles, bones, maxInfluences; public float weightSumErr, bindMaxMm, bindMeanMm;
            public float[] poseMaxMm, poseMeanMm; public int unmatched; public string[] roles; public bool bonesAreBodyBones, noMissingBone;
        }
        [Serializable] public class SexRow
        {
            public string sex; public PieceRow[] pieces; public string[] failures; public int bodyTriangles, faceTriangles, kitTriangles, racketTriangles, totalTriangles;
            public float[] centroidDeltaMm; public float shoeLowestYAtReadyMm, bodyLowestYAtReadyMm; public string[] posesTested;
            public bool boundsOk, materialsOk, tintOk, defaultIsAuthored; public string tintLog; public int rolesFound;
        }
        [Serializable] public class Report { public string when; public SexRow[] sexes; public bool pass; }

        static readonly string[] PoseNames = { "ReadyIdle@0", "Forehand@20", "Forehand@9" };
        static readonly (string clip, int frame, string key)[] Poses = { ("Ready", 0, "ReadyIdle_0"), ("Forehand", 20, "Forehand_20"), ("Forehand", 9, "Forehand_9") };

        static string OutDir => Path.GetFullPath(Environment.GetEnvironmentVariable("MH_KIT_OUT") ?? "../work/hero-dressed/data");
        static string RefDir(string sex) => Path.GetFullPath("../work/hero-dressed/data/kitpose_" + sex.ToLowerInvariant());

        public static void Run()
        {
            int code = 0;
            var txt = new StringBuilder();
            try
            {
                var rep = new Report { when = DateTime.Now.ToString("s") };
                var rows = new List<SexRow>();
                foreach (var sex in new[] { "Male", "Female" }) { var r = TestSex(sex, txt); rows.Add(r); }
                rep.sexes = rows.ToArray(); rep.pass = rows.All(r => r.failures.Length == 0);
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(Path.Combine(OutDir, "unity_gates.json"), JsonUtility.ToJson(rep, true));
                File.WriteAllText(Path.Combine(OutDir, "unity_gates.txt"), txt.ToString());
                Debug.Log("[MatchHeroKitTests]\n" + txt);
                if (!rep.pass) code = 2;
            }
            catch (Exception e) { Debug.LogException(e); txt.AppendLine("EXCEPTION " + e); File.WriteAllText(Path.Combine(OutDir, "unity_gates.txt"), txt.ToString()); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static float[] LoadF32(string path)
        {
            var b = File.ReadAllBytes(path); var f = new float[b.Length / 4]; Buffer.BlockCopy(b, 0, f, 0, b.Length); return f;
        }
        /// Blender (x, y, z) -> Unity (-x, z, -y)
        static Vector3 ToUnity(float x, float y, float z) => new Vector3(-x, z, -y);

        static long Key(Vector3 p) => ((long)Mathf.RoundToInt(p.x * 20000) * 73856093L) ^ ((long)Mathf.RoundToInt(p.y * 20000) * 19349663L) ^ ((long)Mathf.RoundToInt(p.z * 20000) * 83492791L);

        /// For every Unity vertex the Blender vertex at the same bind position (hashed probe, exhaustive fallback); -1 if none within 0.2 mm.
        static int[] MatchIdx(Vector3[] uv, Vector3[] bu, out int unmatched, out float worst, out float mean)
        {
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < bu.Length; i++) { long k = Key(bu[i]); if (!grid.TryGetValue(k, out var lst)) grid[k] = lst = new List<int>(); lst.Add(i); }
            var idx = new int[uv.Length]; worst = 0; float sum = 0; unmatched = 0;
            for (int i = 0; i < uv.Length; i++)
            {
                int best = -1; float bd = float.MaxValue;
                for (int dx = -1; dx <= 1 && bd > 1e-4f; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                {
                    var q = uv[i] + new Vector3(dx, dy, dz) / 20000f;
                    if (grid.TryGetValue(Key(q), out var lst)) foreach (int j in lst) { float d = Vector3.Distance(uv[i], bu[j]); if (d < bd) { bd = d; best = j; } }
                }
                if (best < 0 || bd > 2e-4f) { best = -1; bd = float.MaxValue; for (int j = 0; j < bu.Length; j++) { float d = Vector3.Distance(uv[i], bu[j]); if (d < bd) { bd = d; best = j; } } }
                if (best < 0 || bd > 2e-4f) { unmatched++; idx[i] = -1; continue; }
                idx[i] = best; worst = Mathf.Max(worst, bd); sum += bd;
            }
            mean = sum / Mathf.Max(1, uv.Length - unmatched);
            return idx;
        }

        static Vector3 Centroid(IEnumerable<Vector3> v) { var s = Vector3.zero; int n = 0; foreach (var p in v) { s += p; n++; } return n == 0 ? s : s / n; }

        static SexRow TestSex(string sex, StringBuilder txt)
        {
            var fail = new List<string>();
            var row = new SexRow { sex = sex, posesTested = PoseNames };
            txt.AppendLine("== " + sex);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Tennis/Customization/Player" + sex + ".prefab");
            if (!prefab) throw new FileNotFoundException("prefab " + sex);
            var root = (GameObject)Object.Instantiate(prefab); root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var keep = QualitySettings.skinWeights; QualitySettings.skinWeights = SkinWeights.FourBones;
            try
            {
                var look = root.GetComponent<MatchHeroLook>(); var driver = root.GetComponent<HeroTennisDriver>();
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.StartsWith("Body_"));
                var face = root.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.StartsWith("Face_"));
                var kit = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("Kit_")).ToArray();
                if (kit.Length != MatchHeroKit.Pieces.Length) fail.Add($"{sex}: {kit.Length} kit renderers, expected {MatchHeroKit.Pieces.Length}");
                var rig = root.transform.Find("Rig_" + sex);
                var byName = new Dictionary<string, Transform>(); foreach (var t in rig.GetComponentsInChildren<Transform>(true)) byName.TryAdd(t.name, t);
                var bodyBones = new HashSet<Transform>(body.bones);

                // ---- gate 4(a): bound to this prefab's own bones; 4(c): materials
                var pieceRows = new List<PieceRow>(); var allRoles = new HashSet<string>(); bool materialsOk = true;
                foreach (var r in kit)
                {
                    var pr = new PieceRow { piece = r.name, bones = r.bones.Length, vertices = r.sharedMesh.vertexCount };
                    pr.noMissingBone = r.bones.All(b => b) && r.rootBone;
                    pr.bonesAreBodyBones = r.bones.All(b => b && b.IsChildOf(root.transform) && byName.TryGetValue(b.name, out var t) && ReferenceEquals(t, b));
                    if (!pr.noMissingBone) fail.Add($"{sex} {r.name}: a bone is missing");
                    if (!r.bones.All(b => b && b.IsChildOf(root.transform) && ReferenceEquals(byName[b.name], b))) fail.Add($"{sex} {r.name}: bones are not the prefab's own transforms");
                    pr.roles = r.sharedMaterials.Select(m => m ? m.name : "null").ToArray();
                    foreach (var m in r.sharedMaterials)
                    {
                        if (!m) { materialsOk = false; fail.Add($"{sex} {r.name}: null material"); continue; }
                        allRoles.Add(m.name);
                        if (m.shader == null || m.shader.name != "Universal Render Pipeline/Lit") { materialsOk = false; fail.Add($"{sex} {r.name}: material {m.name} uses shader '{(m.shader ? m.shader.name : "none")}'"); }
                        if (!MatchHeroLook.KitRoles.Contains(m.name)) { materialsOk = false; fail.Add($"{sex} {r.name}: unexpected material {m.name}"); }
                    }
                    pieceRows.Add(pr);
                }
                row.rolesFound = allRoles.Count;
                foreach (var role in MatchHeroLook.KitRoles) if (!allRoles.Contains(role)) { materialsOk = false; fail.Add($"{sex}: role material {role} is on no kit renderer"); }
                row.materialsOk = materialsOk;
                if (look.kit == null || look.kit.Length != kit.Length) fail.Add($"{sex}: MatchHeroLook.kit is not wired");

                // ---- gate 1(c): the imported weights
                foreach (var pr in pieceRows)
                {
                    var r = kit.First(k => k.name == pr.piece); var m = r.sharedMesh;
                    var per = m.GetBonesPerVertex(); var all = m.GetAllBoneWeights();
                    int off = 0, maxInf = 0; float worst = 0; bool idxOk = true;
                    for (int v = 0; v < per.Length; v++)
                    {
                        int n = per[v]; float sum = 0; maxInf = Mathf.Max(maxInf, n);
                        for (int k = 0; k < n; k++) { sum += all[off + k].weight; if (all[off + k].boneIndex < 0 || all[off + k].boneIndex >= r.bones.Length) idxOk = false; }
                        if (n == 0) sum = 0; worst = Mathf.Max(worst, Mathf.Abs(sum - 1)); off += n;
                    }
                    pr.maxInfluences = maxInf; pr.weightSumErr = worst;
                    int tris = 0; for (int s = 0; s < m.subMeshCount; s++) tris += (int)(m.GetIndexCount(s) / 3); pr.triangles = tris;
                    if (maxInf > 4) fail.Add($"{sex} {pr.piece}: {maxInf} influences");
                    if (worst > 1e-3f) fail.Add($"{sex} {pr.piece}: weight sum error {worst}");
                    if (!idxOk) fail.Add($"{sex} {pr.piece}: bone index out of range");
                }

                // ---- gate 4(b): Unity-skinned kit vs the Blender-evaluated kit (bind pose identifies the vertices)
                string refDir = RefDir(sex);
                var bodyRefBind = LoadF32(Path.Combine(refDir, "body_bind.f32"));
                var clipFor = new Dictionary<string, AnimationClip>
                { { "Ready", driver.slots.First(s => s.id == HeroTennisDriver.Clip.Ready).clip }, { "Forehand", driver.slots.First(s => s.id == HeroTennisDriver.Clip.Forehand).clip } };
                row.centroidDeltaMm = new float[PoseNames.Length];
                // bind pose = the mesh's own vertices (mesh space) placed by the renderer node (the saved hierarchy is in the pose of frame 0 of the clip FBX, not in the bind pose,
                // so BakeMesh would give ReadyIdle frame 0 here: using the raw mesh keeps the vertex identification independent of any pose)
                var bindPos = new Dictionary<string, Vector3[]>();
                foreach (var pr in pieceRows)
                {
                    var r = kit.First(k => k.name == pr.piece); var verts = r.sharedMesh.vertices; var l2w = r.transform.localToWorldMatrix;
                    var pw = new Vector3[verts.Length]; for (int i = 0; i < verts.Length; i++) pw[i] = root.transform.InverseTransformPoint(l2w.MultiplyPoint3x4(verts[i]));
                    bindPos[pr.piece] = pw;
                }
                var map = new Dictionary<string, int[]>(); // unity vertex -> blender vertex
                foreach (var pr in pieceRows)
                {
                    string pc = pr.piece.Substring(4); var bl = LoadF32(Path.Combine(refDir, pc + "_bind.f32"));
                    int nb = bl.Length / 3; var grid = new Dictionary<long, List<int>>();
                    var bu = new Vector3[nb]; for (int i = 0; i < nb; i++) { bu[i] = ToUnity(bl[i * 3], bl[i * 3 + 1], bl[i * 3 + 2]); long k = Key(bu[i]); if (!grid.TryGetValue(k, out var lst)) grid[k] = lst = new List<int>(); lst.Add(i); }
                    var uv = bindPos[pr.piece]; var idx = new int[uv.Length]; float worst = 0, sum = 0; int unmatched = 0;
                    { var ub = new Bounds(uv[0], Vector3.zero); foreach (var q in uv) ub.Encapsulate(q); var bb2 = new Bounds(bu[0], Vector3.zero); foreach (var q in bu) bb2.Encapsulate(q);
                      txt.AppendLine($"  bounds {pr.piece}: Unity bind min={ub.min:F4} max={ub.max:F4} | Blender->Unity min={bb2.min:F4} max={bb2.max:F4}"); }
                    for (int i = 0; i < uv.Length; i++)
                    {
                        int best = -1; float bd = float.MaxValue;
                        // neighbouring grid cells (the key is rounded): probe the 27 cells around
                        for (int dx = -1; dx <= 1 && bd > 1e-4f; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
                        {
                            var q = uv[i] + new Vector3(dx, dy, dz) / 20000f;
                            if (grid.TryGetValue(Key(q), out var lst)) foreach (int j in lst) { float d = Vector3.Distance(uv[i], bu[j]); if (d < bd) { bd = d; best = j; } }
                        }
                        if (best < 0 || bd > 2e-4f)
                        {
                            // fallback: exhaustive scan (the hashed probe can miss a vertex sitting on a cell corner)
                            best = -1; bd = float.MaxValue; for (int j = 0; j < nb; j++) { float d = Vector3.Distance(uv[i], bu[j]); if (d < bd) { bd = d; best = j; } }
                            if (best < 0 || bd > 2e-4f) { unmatched++; idx[i] = -1; continue; }
                        }
                        idx[i] = best; worst = Mathf.Max(worst, bd); sum += bd;
                    }
                    map[pr.piece] = idx; pr.unmatched = unmatched; pr.bindMaxMm = worst * 1000f; pr.bindMeanMm = sum / Mathf.Max(1, uv.Length - unmatched) * 1000f;
                    if (unmatched > 0) fail.Add($"{sex} {pr.piece}: {unmatched} of {uv.Length} Unity vertices have no Blender vertex within 0.2 mm at the bind pose");
                    if (pr.bindMaxMm > 0.1f) fail.Add($"{sex} {pr.piece}: bind pose deviates {pr.bindMaxMm:F4} mm from the Blender kit");
                    pr.poseMaxMm = new float[PoseNames.Length]; pr.poseMeanMm = new float[PoseNames.Length];
                }
                // the body's own Unity -> Blender vertex map (the importer duplicates vertices along material / UV seams)
                var bodyBindU = body.sharedMesh.vertices.Select(v => root.transform.InverseTransformPoint(body.transform.localToWorldMatrix.MultiplyPoint3x4(v))).ToArray();
                var bodyBindB = new Vector3[bodyRefBind.Length / 3]; for (int i = 0; i < bodyBindB.Length; i++) bodyBindB[i] = ToUnity(bodyRefBind[i * 3], bodyRefBind[i * 3 + 1], bodyRefBind[i * 3 + 2]);
                var bodyMap = MatchIdx(bodyBindU, bodyBindB, out int bodyUnmatched, out _, out _);
                if (bodyUnmatched > 0) txt.AppendLine($"  note: {bodyUnmatched} body vertices without a Blender partner (excluded from the centroid)");
                for (int pi = 0; pi < Poses.Length; pi++)
                {
                    var (clipKey, frame, key) = Poses[pi];
                    clipFor[clipKey].SampleAnimation(root, frame / 30f);
                    foreach (var pr in pieceRows)
                    {
                        var r = kit.First(k => k.name == pr.piece); var baked = new Mesh(); r.BakeMesh(baked, true);
                        var l2w = r.transform.localToWorldMatrix; var verts = baked.vertices; var bl = LoadF32(Path.Combine(refDir, pr.piece.Substring(4) + "_" + key + ".f32"));
                        float worst = 0, sum = 0; int n = 0; var idx = map[pr.piece];
                        for (int i = 0; i < verts.Length; i++)
                        {
                            if (idx[i] < 0) continue;
                            var u = root.transform.InverseTransformPoint(l2w.MultiplyPoint3x4(verts[i])); var b = ToUnity(bl[idx[i] * 3], bl[idx[i] * 3 + 1], bl[idx[i] * 3 + 2]);
                            float d = Vector3.Distance(u, b); worst = Mathf.Max(worst, d); sum += d; n++;
                        }
                        pr.poseMaxMm[pi] = worst * 1000f; pr.poseMeanMm[pi] = sum / Mathf.Max(1, n) * 1000f; Object.DestroyImmediate(baked);
                        if (worst > .002f) fail.Add($"{sex} {pr.piece} @ {PoseNames[pi]}: Unity-skinned kit deviates {worst * 1000f:F3} mm from Blender (limit 2 mm)");
                    }
                    // centroid of the kit relative to the body's, Unity vs Blender
                    var bb = new Mesh(); body.BakeMesh(bb, true); var bl2w = body.transform.localToWorldMatrix;
                    var bvAll = bb.vertices; var seenB = new HashSet<int>(); var bodyUl = new List<Vector3>();
                    for (int i = 0; i < bvAll.Length; i++) if (bodyMap[i] >= 0 && seenB.Add(bodyMap[i])) bodyUl.Add(root.transform.InverseTransformPoint(bl2w.MultiplyPoint3x4(bvAll[i])));
                    var bodyU = bodyUl.ToArray(); Object.DestroyImmediate(bb);
                    var bodyRef = LoadF32(Path.Combine(refDir, "body_" + key + ".f32"));
                    var bodyB = new Vector3[bodyRef.Length / 3]; for (int i = 0; i < bodyB.Length; i++) bodyB[i] = ToUnity(bodyRef[i * 3], bodyRef[i * 3 + 1], bodyRef[i * 3 + 2]);
                    float worstC = 0;
                    foreach (var pr in pieceRows)
                    {
                        var r = kit.First(k => k.name == pr.piece); var baked = new Mesh(); r.BakeMesh(baked, true); var l2w = r.transform.localToWorldMatrix;
                        var kv = baked.vertices; var mp = map[pr.piece]; var seen = new HashSet<int>(); var ku = new List<Vector3>();
                        for (int i = 0; i < kv.Length; i++) if (mp[i] >= 0 && seen.Add(mp[i])) ku.Add(root.transform.InverseTransformPoint(l2w.MultiplyPoint3x4(kv[i])));   // one Unity vertex per Blender vertex (the importer splits hard-edge vertices)
                        Object.DestroyImmediate(baked);
                        var kb = LoadF32(Path.Combine(refDir, pr.piece.Substring(4) + "_" + key + ".f32")); var kbU = new Vector3[kb.Length / 3]; for (int i = 0; i < kbU.Length; i++) kbU[i] = ToUnity(kb[i * 3], kb[i * 3 + 1], kb[i * 3 + 2]);
                        float dc = Vector3.Distance(Centroid(ku) - Centroid(bodyU), Centroid(kbU) - Centroid(bodyB)) * 1000f;
                        worstC = Mathf.Max(worstC, dc);
                    }
                    row.centroidDeltaMm[pi] = worstC;
                    if (worstC > 2f) fail.Add($"{sex} @ {PoseNames[pi]}: kit-minus-body centroid differs from Blender by {worstC:F3} mm (limit 2 mm)");
                }
                row.pieces = pieceRows.ToArray();

                // ---- gate 8(c): the shoes' lowest vertex at the ReadyIdle stance, relative to the prefab root
                clipFor["Ready"].SampleAnimation(root, 0f);
                float lowShoe = float.MaxValue, lowBody = float.MaxValue;
                foreach (var r in kit.Where(k => k.name.StartsWith("Kit_Shoe"))) { var b = new Mesh(); r.BakeMesh(b, true); var l2w = r.transform.localToWorldMatrix; foreach (var v in b.vertices) lowShoe = Mathf.Min(lowShoe, root.transform.InverseTransformPoint(l2w.MultiplyPoint3x4(v)).y); Object.DestroyImmediate(b); }
                { var b = new Mesh(); body.BakeMesh(b, true); var l2w = body.transform.localToWorldMatrix; foreach (var v in b.vertices) lowBody = Mathf.Min(lowBody, root.transform.InverseTransformPoint(l2w.MultiplyPoint3x4(v)).y); Object.DestroyImmediate(b); }
                row.shoeLowestYAtReadyMm = lowShoe * 1000f; row.bodyLowestYAtReadyMm = lowBody * 1000f;
                txt.AppendLine($"  ReadyIdle t=0 (prefab as saved): lowest shoe vertex y = {lowShoe * 1000f:F2} mm, lowest body vertex y = {lowBody * 1000f:F2} mm relative to the prefab root");

                // ---- gate 7: triangles
                int Tris(Mesh m) { int t = 0; for (int s = 0; s < m.subMeshCount; s++) t += (int)(m.GetIndexCount(s) / 3); return t; }
                row.bodyTriangles = Tris(body.sharedMesh); row.faceTriangles = Tris(face.GetComponent<MeshFilter>().sharedMesh);
                row.kitTriangles = pieceRows.Sum(p => p.triangles);
                row.racketTriangles = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.transform.IsChildOf(look.racketGrip) && f.name != "Collider_Racket").Sum(f => Tris(f.sharedMesh));
                row.totalTriangles = row.bodyTriangles + row.faceTriangles + row.kitTriangles + row.racketTriangles;
                txt.AppendLine($"  triangles: body {row.bodyTriangles}, face {row.faceTriangles}, kit {row.kitTriangles}, racket {row.racketTriangles}, total {row.totalTriangles}");

                // ---- gate 5(a-c): role colours
                row.tintOk = TintChecks(sex, prefab, root, fail, txt, out bool defaultOk); row.defaultIsAuthored = defaultOk;
                txt.AppendLine("  kit pieces:");
                foreach (var pr in pieceRows) txt.AppendLine($"    {pr.piece,-11} verts={pr.vertices,6} tris={pr.triangles,6} bones={pr.bones,2} maxInfl={pr.maxInfluences} wsumErr={pr.weightSumErr:E1} bind max={pr.bindMaxMm:F4} mm  poses max mm=[{string.Join(", ", pr.poseMaxMm.Select(x => x.ToString("F3")))}] mean=[{string.Join(", ", pr.poseMeanMm.Select(x => x.ToString("F4")))}] roles={string.Join(",", pr.roles)}");
                txt.AppendLine($"  kit-minus-body centroid vs Blender (mm): [{string.Join(", ", row.centroidDeltaMm.Select(x => x.ToString("F3")))}]");
            }
            finally { QualitySettings.skinWeights = keep; Object.DestroyImmediate(root); }
            row.failures = fail.ToArray();
            txt.AppendLine(fail.Count == 0 ? $"  {sex}: all Unity checks PASS" : $"  {sex}: {fail.Count} FAIL\n    " + string.Join("\n    ", fail));
            return row;
        }

        // ================================================================ gate 5 (a-c)
        static Color ExpectTint(Color pick) => MatchHeroLookExpect(pick);
        /// Independent restatement of the pre-registered maths (GATE_DEFS D6), so a typo in MatchHeroLook cannot test itself.
        static Color MatchHeroLookExpect(Color p) { const float w = 0.9300f, kr = 0.0350f, kb = 0.0401f; return new Color(Mathf.Max(w * p.r, kr), Mathf.Max(w * p.g, kr), Mathf.Max(w * p.b, kb), 1); }
        static Color ExpectDerive(Color p)
        {
            float l = 0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b;
            return l >= 0.5f ? new Color(p.r * 0.65f, p.g * 0.65f, p.b * 0.65f, 1) : new Color(p.r + (1 - p.r) * 0.35f, p.g + (1 - p.g) * 0.35f, p.b + (1 - p.b) * 0.35f, 1);
        }
        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) <= 1.5f / 255 && Mathf.Abs(a.g - b.g) <= 1.5f / 255 && Mathf.Abs(a.b - b.b) <= 1.5f / 255;

        static Dictionary<string, Color> Snapshot(GameObject go)
        {
            var d = new Dictionary<string, Color>(); var look = go.GetComponent<MatchHeroLook>();
            foreach (var role in MatchHeroLook.KitRoles) if (look.TryGetKitColour(role, out var c)) d[role] = c;
            d["skin"] = look.body.sharedMaterials[0].GetColor("_BaseColor");
            d["racket"] = look.racketFrame.sharedMaterial.GetColor("_BaseColor");
            var faceMats = look.face.sharedMaterials; for (int i = 0; i < faceMats.Length; i++) d["face" + i] = faceMats[i].GetColor("_BaseColor");
            return d;
        }

        static bool TintChecks(string sex, GameObject prefab, GameObject a, List<string> fail, StringBuilder txt, out bool defaultOk)
        {
            var look = a.GetComponent<MatchHeroLook>(); var b = (GameObject)Object.Instantiate(prefab); var lookB = b.GetComponent<MatchHeroLook>();
            bool ok = true; var log = new StringBuilder();
            try
            {
                // asset colours before
                var assetBefore = MatchHeroLook.KitRoles.ToDictionary(r => r, r => AssetDatabase.LoadAssetAtPath<Material>(MatchHeroKit.MatDir + r + ".mat").GetColor("_BaseColor"));
                // (b) default: nothing called, then SetKit(null...) and SetKit("", ...)
                var authored = new Dictionary<string, Color>();
                // HERO_DETAIL: the White kit's contrast pieces are the shorts band AND the shirt piping (#09090A); the sole is its own darker grey (MatchHeroKit.Authored)
                foreach (var role in MatchHeroLook.KitRoles) authored[role] = MatchHeroKit.Authored(role);
                var s0 = Snapshot(a);
                bool def = authored.All(kv => Near(s0[kv.Key], kv.Value)); defaultOk = def;
                if (!def) { ok = false; fail.Add($"{sex}: default kit colours are not the authored White kit: " + string.Join(", ", authored.Where(kv => !Near(s0[kv.Key], kv.Value)).Select(kv => kv.Key + "=" + s0[kv.Key]))); }
                look.SetKit(null, null, null); var s1 = Snapshot(a);
                look.SetKit("", "", ""); var s2 = Snapshot(a);
                if (!(authored.All(kv => Near(s1[kv.Key], kv.Value)) && authored.All(kv => Near(s2[kv.Key], kv.Value)))) { ok = false; fail.Add($"{sex}: SetKit(null/empty) changed a kit colour"); }
                log.AppendLine($"    default and SetKit(null|empty): authored White kit, {authored.Count} roles OK={def}");

                // (c) one group at a time
                var coral = ColorUtility.TryParseHtmlString("#FF7F50", out var c1) ? c1 : Color.red; var navy = ColorUtility.TryParseHtmlString("#1E2A6E", out var c2) ? c2 : Color.blue; var lime = ColorUtility.TryParseHtmlString("#7CFC00", out var c3) ? c3 : Color.green;
                var cases = new (string name, string shirt, string shorts, string shoes, string[] changed)[]
                {
                    ("shirt only", "FF7F50", null, null, new[] { "Kit_Shirt", "Kit_ShirtTrim" }), ("shorts only", null, "1E2A6E", null, new[] { "Kit_Shorts", "Kit_ShortsBand" }),
                    ("shoes only", null, null, "7CFC00", new[] { "Kit_Shoe" }), ("all three", "FF7F50", "1E2A6E", "7CFC00", new[] { "Kit_Shirt", "Kit_ShirtTrim", "Kit_Shorts", "Kit_ShortsBand", "Kit_Shoe" }),
                    ("white shirt", "FFFFFF", null, null, new[] { "Kit_Shirt", "Kit_ShirtTrim" }), ("black shirt", "000000", null, null, new[] { "Kit_Shirt", "Kit_ShirtTrim" }),
                };
                foreach (var cs in cases)
                {
                    look.SetKit(cs.shirt, cs.shorts, cs.shoes); var s = Snapshot(a);
                    var expect = new Dictionary<string, Color>(authored);
                    if (cs.shirt != null) { ColorUtility.TryParseHtmlString("#" + cs.shirt, out var p); expect["Kit_Shirt"] = ExpectTint(p); expect["Kit_ShirtTrim"] = ExpectTint(ExpectDerive(p)); }
                    if (cs.shorts != null) { ColorUtility.TryParseHtmlString("#" + cs.shorts, out var p); expect["Kit_Shorts"] = ExpectTint(p); expect["Kit_ShortsBand"] = ExpectTint(ExpectDerive(p)); }
                    if (cs.shoes != null) { ColorUtility.TryParseHtmlString("#" + cs.shoes, out var p); expect["Kit_Shoe"] = ExpectTint(p); }
                    var changed = MatchHeroLook.KitRoles.Where(r => !Near(s[r], s0[r])).ToArray();
                    bool formula = MatchHeroLook.KitRoles.All(r => Near(s[r], expect[r]));
                    bool onlyRoles = s0.Keys.Where(k => !MatchHeroLook.KitRoles.Contains(k)).All(k => Near(s[k], s0[k]));
                    // a pick equal to the authored colour does not "change" a role (white shirt); everything else must change exactly the listed roles
                    bool setOk = cs.name == "white shirt" ? changed.Length == 0 || changed.All(r => cs.changed.Contains(r)) : changed.OrderBy(x => x).SequenceEqual(cs.changed.OrderBy(x => x));
                    log.AppendLine($"    {cs.name,-12}: changed [{string.Join(",", changed)}] formula {formula} others-untouched {onlyRoles} -> Shirt {s["Kit_Shirt"]:F3} Trim {s["Kit_ShirtTrim"]:F3} Shorts {s["Kit_Shorts"]:F3} Band {s["Kit_ShortsBand"]:F3} Shoe {s["Kit_Shoe"]:F3} Sole {s["Kit_Sole"]:F3} Sock {s["Kit_Sock"]:F3}");
                    if (!setOk) { ok = false; fail.Add($"{sex}: SetKit({cs.name}) changed [{string.Join(",", changed)}], expected [{string.Join(",", cs.changed)}]"); }
                    if (!formula) { ok = false; fail.Add($"{sex}: SetKit({cs.name}) colours differ from the D6 formula"); }
                    if (!onlyRoles) { ok = false; fail.Add($"{sex}: SetKit({cs.name}) changed the skin / face / racket"); }
                    var sb2 = Snapshot(b);
                    if (!MatchHeroLook.KitRoles.All(r => Near(sb2[r], authored[r]))) { ok = false; fail.Add($"{sex}: a second hero instance changed when the first was tinted ({cs.name})"); }
                }
                look.SetKit(null, null, null);
                var assetAfter = MatchHeroLook.KitRoles.ToDictionary(r => r, r => AssetDatabase.LoadAssetAtPath<Material>(MatchHeroKit.MatDir + r + ".mat").GetColor("_BaseColor"));
                if (!MatchHeroLook.KitRoles.All(r => assetBefore[r] == assetAfter[r])) { ok = false; fail.Add($"{sex}: a tint reached the shared material assets"); }
                log.AppendLine($"    material assets untouched by tints: {MatchHeroLook.KitRoles.All(r => assetBefore[r] == assetAfter[r])}; second instance untouched: ok");
            }
            finally { Object.DestroyImmediate(b); }
            txt.Append(log);
            return ok;
        }
    }
}
