using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// DRESS_MATCH_HEROES: a fingerprint of the two match-hero prefabs (hierarchy with local transforms, Body / Face mesh vertex counts and a hash of the vertex
    /// positions, bone weights and bindposes, materials per renderer, racket transform) and of the clip curve bindings. Written BEFORE the kit is added
    /// (work/hero-dressed/baseline/prefab_before.json) and again after it (prefab_after.json); gate BODY_UNCHANGED compares the two on every field that
    /// does not belong to the kit.
    ///   Unity -batchmode -nographics -quit -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroKitBaseline.Run   (env MH_FP_OUT = output json)
    public static class MatchHeroKitBaseline
    {
        [Serializable] public class NodeRow { public string path; public string parent; public float[] pos, rot, scale; public string[] components; }
        [Serializable] public class MeshRow { public string renderer, mesh, kind; public int vertexCount, triangleCount, boneCount, subMeshCount; public string positionsSha, weightsSha, bindposesSha, trianglesSha; public string[] materials; public string[] bones; public string rootBone; }
        [Serializable] public class ClipRow { public string name; public float length; public int curveCount; public string[] animatedRoots; public string sha; }
        [Serializable] public class HeroRow { public string sex, prefabYamlSha; public NodeRow[] nodes; public MeshRow[] meshes; public ClipRow[] clips; public string animatorAvatar; }
        [Serializable] public class Report { public string when, unity; public HeroRow[] heroes; }

        public static void Run()
        {
            int code = 0;
            try
            {
                var rep = new Report { when = DateTime.Now.ToString("s"), unity = Application.unityVersion };
                var list = new List<HeroRow>();
                foreach (var sex in new[] { "Male", "Female" }) list.Add(Dump(sex));
                rep.heroes = list.ToArray();
                string path = Environment.GetEnvironmentVariable("MH_FP_OUT") ?? "../work/hero-dressed/baseline/prefab_before.json";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, JsonUtility.ToJson(rep, true));
                Debug.Log("[MatchHeroKitBaseline] wrote " + path);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string Sha(byte[] b) { using var h = SHA256.Create(); return string.Concat(h.ComputeHash(b).Select(x => x.ToString("x2"))); }
        static byte[] Bytes(Vector3[] v) { var b = new byte[v.Length * 12]; for (int i = 0; i < v.Length; i++) { Buffer.BlockCopy(BitConverter.GetBytes(v[i].x), 0, b, i * 12, 4); Buffer.BlockCopy(BitConverter.GetBytes(v[i].y), 0, b, i * 12 + 4, 4); Buffer.BlockCopy(BitConverter.GetBytes(v[i].z), 0, b, i * 12 + 8, 4); } return b; }

        static string PathOf(Transform t, Transform root) { var s = new Stack<string>(); for (var c = t; c && c != root; c = c.parent) s.Push(c.name); return string.Join("/", s); }

        static HeroRow Dump(string sex)
        {
            string prefabPath = "Assets/Resources/Tennis/Customization/Player" + sex + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) throw new FileNotFoundException(prefabPath);
            var row = new HeroRow { sex = sex, prefabYamlSha = Sha(File.ReadAllBytes(prefabPath)) };
            var root = (GameObject)UnityEngine.Object.Instantiate(prefab);
            try
            {
                var nodes = new List<NodeRow>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    nodes.Add(new NodeRow
                    {
                        path = t == root.transform ? "." : PathOf(t, root.transform), parent = t.parent ? (t.parent == root.transform ? "." : PathOf(t.parent, root.transform)) : "",
                        pos = new[] { t.localPosition.x, t.localPosition.y, t.localPosition.z }, rot = new[] { t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w },
                        scale = new[] { t.localScale.x, t.localScale.y, t.localScale.z }, components = t.GetComponents<Component>().Select(c => c ? c.GetType().Name : "missing").ToArray(),
                    });
                row.nodes = nodes.ToArray();
                var meshes = new List<MeshRow>();
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var m = smr.sharedMesh; if (!m) continue;
                    var tris = new List<byte>(); for (int s = 0; s < m.subMeshCount; s++) foreach (var i in m.GetIndices(s)) tris.AddRange(BitConverter.GetBytes(i));
                    var w = new List<byte>(); foreach (var bw in m.GetAllBoneWeights()) { w.AddRange(BitConverter.GetBytes(bw.boneIndex)); w.AddRange(BitConverter.GetBytes(bw.weight)); }
                    var bp = new List<byte>(); foreach (var b in m.bindposes) for (int k = 0; k < 16; k++) bp.AddRange(BitConverter.GetBytes(b[k]));
                    meshes.Add(new MeshRow
                    {
                        renderer = PathOf(smr.transform, root.transform), mesh = m.name, kind = "skinned", vertexCount = m.vertexCount, triangleCount = (int)(tris.Count / 12), boneCount = smr.bones.Length, subMeshCount = m.subMeshCount,
                        positionsSha = Sha(Bytes(m.vertices)), weightsSha = Sha(w.ToArray()), bindposesSha = Sha(bp.ToArray()), trianglesSha = Sha(tris.ToArray()),
                        materials = smr.sharedMaterials.Select(x => x ? x.name : "null").ToArray(), bones = smr.bones.Select(b => b ? PathOf(b, root.transform) : "null").ToArray(), rootBone = smr.rootBone ? PathOf(smr.rootBone, root.transform) : "",
                    });
                }
                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mf = mr.GetComponent<MeshFilter>(); var m = mf ? mf.sharedMesh : null; if (!m) continue;
                    var tris = new List<byte>(); for (int s = 0; s < m.subMeshCount; s++) foreach (var i in m.GetIndices(s)) tris.AddRange(BitConverter.GetBytes(i));
                    meshes.Add(new MeshRow { renderer = PathOf(mr.transform, root.transform), mesh = m.name, kind = "static", vertexCount = m.vertexCount, triangleCount = (int)(tris.Count / 12), subMeshCount = m.subMeshCount,
                        positionsSha = Sha(Bytes(m.vertices)), trianglesSha = Sha(tris.ToArray()), materials = mr.sharedMaterials.Select(x => x ? x.name : "null").ToArray() });
                }
                row.meshes = meshes.ToArray();
                var anim = root.GetComponent<Animator>(); row.animatorAvatar = anim && anim.avatar ? anim.avatar.name : "";
                var clips = new List<ClipRow>();
                foreach (var clipName in MatchHeroPostprocessor.Clips)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(MatchHeroPostprocessor.FbxPath(sex, clipName)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                    var binds = AnimationUtility.GetCurveBindings(clip);
                    var sb = new StringBuilder();
                    foreach (var b in binds.OrderBy(b => b.path + b.propertyName)) { sb.Append(b.path).Append('|').Append(b.propertyName).Append('|'); var c = AnimationUtility.GetEditorCurve(clip, b); foreach (var k in c.keys) sb.Append(k.time.ToString("R")).Append(',').Append(k.value.ToString("R")).Append(';'); }
                    clips.Add(new ClipRow { name = clip.name, length = clip.length, curveCount = binds.Length, animatedRoots = binds.Select(b => b.path.Split('/')[0]).Distinct().Take(6).ToArray(), sha = Sha(Encoding.UTF8.GetBytes(sb.ToString())) });
                }
                row.clips = clips.ToArray();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return row;
        }
    }
}
