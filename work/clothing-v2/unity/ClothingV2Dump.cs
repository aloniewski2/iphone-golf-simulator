using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// CLOTHING V2 (work/clothing-v2): dumps each hero's body exactly as the game builds it (bind-pose mesh, weights, bind poses), the current kit,
    /// and the body + bone matrices over every clip the hero plays, so new garments can be authored on the real body and checked for skin
    /// poke-through without Unity in the loop. Tool only: runs in an APFS clone, saves nothing in the project.
    public static class ClothingV2Dump
    {
        static readonly string[] AllHeroes = { "MatchHero_Male", "MatchHero_Female", "GolfKitHero_Male", "GolfKitHero_Female" };
        const string PrefabDir = "Assets/Resources/Tennis/Customization/";

        public static void Run()
        {
            int code = 0;
            try
            {
                string outRoot = Environment.GetEnvironmentVariable("CV2_OUT");
                float fps = float.TryParse(Environment.GetEnvironmentVariable("CV2_FPS"), out var f) ? f : 12f;
                string filter = Environment.GetEnvironmentVariable("CV2_HEROES");
                var heroes = string.IsNullOrEmpty(filter) ? AllHeroes : filter.Split(',');
                QualitySettings.skinWeights = SkinWeights.FourBones;
                foreach (var id in heroes) DumpHero(id, Path.Combine(outRoot, id), fps);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static void W(string path, float[] data) { using var w = new BinaryWriter(File.Create(path)); foreach (var x in data) w.Write(x); }
        static void W(string path, int[] data) { using var w = new BinaryWriter(File.Create(path)); foreach (var x in data) w.Write(x); }
        static float[] V3(Vector3[] v) { var a = new float[v.Length * 3]; for (int i = 0; i < v.Length; i++) { a[i * 3] = v[i].x; a[i * 3 + 1] = v[i].y; a[i * 3 + 2] = v[i].z; } return a; }
        static float[] M(Matrix4x4 m) { var a = new float[16]; for (int i = 0; i < 16; i++) a[i] = m[i % 4, i / 4]; return a; }   // row-major
        static string J(Matrix4x4 m) => "[" + string.Join(",", M(m).Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]";
        static string Q(string s) => "\"" + s.Replace("\"", "'") + "\"";

        static GameObject Build(string id, out List<AnimationClip> clips)
        {
            bool golf = id.StartsWith("GolfKit"), female = id.EndsWith("Female");
            string sex = female ? "Female" : "Male";
            GameObject root;
            if (golf)
            {
                var golfer = HeroGolfer.Build(null, new HeroLook { Female = female, Haircut = (int)HeroGolfer.Haircut.Bald, Skin = female ? new Color(.953f, .855f, .788f) : new Color(.949f, .816f, .686f), HairColor = new Color(.35f, .25f, .18f) });
                root = golfer.Root;
            }
            else root = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + ".prefab"));
            root.name = id;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
            var driver = root.GetComponent<HeroTennisDriver>();
            if (driver) driver.ResolvePerformanceClips();
            var look = root.GetComponent<MatchHeroLook>();
            typeof(MatchHeroLook).GetMethod("OwnMaterials", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(look, null);
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { smr.quality = SkinQuality.Bone4; smr.updateWhenOffscreen = true; }
            if (golf) clips = Resources.LoadAll<AnimationClip>(HeroGolfer.PathFor(female)).Where(c => !c.name.StartsWith("__preview__")).ToList();
            else clips = driver.slots.Where(s => s.clip).Select(s => s.clip).Distinct().ToList();
            return root;
        }

        static void BodyCorrectives(GameObject root)
        {
            var look = root.GetComponent<MatchHeroLook>();
            HeroGripPolish.ApplyGrip(look);
            look?.body?.GetComponent<HeroArmSkinning>()?.Apply();
        }

        static void RestPose(GameObject root)
        {
            var body = root.GetComponent<MatchHeroLook>().body;
            var bones = body.bones; var bind = body.sharedMesh.bindposes; var l2w = body.transform.localToWorldMatrix;
            int Depth(Transform t) { int d = 0; for (; t; t = t.parent) d++; return d; }
            foreach (int i in Enumerable.Range(0, bones.Length).Where(i => bones[i]).OrderBy(i => Depth(bones[i])))
            {
                var m = l2w * bind[i].inverse;
                bones[i].SetPositionAndRotation(m.GetColumn(3), Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));
            }
        }

        static string DumpSkinned(SkinnedMeshRenderer r, string dir, string tag)
        {
            var mesh = r.sharedMesh;
            W(Path.Combine(dir, tag + "_pos.f32"), V3(mesh.vertices));
            W(Path.Combine(dir, tag + "_nrm.f32"), V3(mesh.normals));
            var tris = new List<int>(); var subs = new List<string>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var t = mesh.GetTriangles(s); subs.Add($"{{\"start\":{tris.Count / 3},\"count\":{t.Length / 3},\"material\":{Q(s < r.sharedMaterials.Length && r.sharedMaterials[s] ? r.sharedMaterials[s].name : "")}}}");
                tris.AddRange(t);
            }
            W(Path.Combine(dir, tag + "_tri.i32"), tris.ToArray());
            var bw = mesh.boneWeights; var wi = new int[bw.Length * 4]; var ww = new float[bw.Length * 4];
            for (int i = 0; i < bw.Length; i++)
            {
                wi[i * 4] = bw[i].boneIndex0; wi[i * 4 + 1] = bw[i].boneIndex1; wi[i * 4 + 2] = bw[i].boneIndex2; wi[i * 4 + 3] = bw[i].boneIndex3;
                ww[i * 4] = bw[i].weight0; ww[i * 4 + 1] = bw[i].weight1; ww[i * 4 + 2] = bw[i].weight2; ww[i * 4 + 3] = bw[i].weight3;
            }
            W(Path.Combine(dir, tag + "_bi.i32"), wi); W(Path.Combine(dir, tag + "_bw.f32"), ww);
            if (mesh.uv.Length == mesh.vertexCount) W(Path.Combine(dir, tag + "_uv.f32"), mesh.uv.SelectMany(u => new[] { u.x, u.y }).ToArray());
            var sb = new StringBuilder();
            sb.Append($"{{\"name\":{Q(r.name)},\"mesh\":{Q(mesh.name)},\"vertexCount\":{mesh.vertexCount},\"submeshes\":[{string.Join(",", subs)}],");
            sb.Append($"\"bones\":[{string.Join(",", r.bones.Select(b => Q(b ? b.name : "")))}],\"rootBone\":{Q(r.rootBone ? r.rootBone.name : "")},");
            sb.Append($"\"bindposes\":[{string.Join(",", mesh.bindposes.Select(J))}],\"rendererToWorld\":{J(r.transform.localToWorldMatrix)},");
            sb.Append($"\"parent\":{Q(r.transform.parent ? r.transform.parent.name : "")},\"localPosition\":[{r.transform.localPosition.x:R},{r.transform.localPosition.y:R},{r.transform.localPosition.z:R}],");
            sb.Append($"\"blendShapes\":[{string.Join(",", Enumerable.Range(0, mesh.blendShapeCount).Select(i => Q(mesh.GetBlendShapeName(i))))}]}}");
            return sb.ToString();
        }

        static void DumpHero(string id, string dir, float fps)
        {
            Directory.CreateDirectory(dir);
            var root = Build(id, out var clips);
            try
            {
                var look = root.GetComponent<MatchHeroLook>();
                var body = look.body;
                RestPose(root); BodyCorrectives(root);
                var parts = new List<string> { DumpSkinned(body, dir, "body") };
                foreach (var k in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("Kit_") && r.sharedMesh))
                    parts.Add(DumpSkinned(k, dir, k.name));
                // rest pose baked body (to validate LBS) and the rest bone matrices
                var baked = new Mesh(); body.BakeMesh(baked, true);
                var place = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                W(Path.Combine(dir, "rest_body_world.f32"), V3(baked.vertices.Select(v => place.MultiplyPoint3x4(v)).ToArray()));
                W(Path.Combine(dir, "rest_bones.f32"), body.bones.SelectMany(b => M(b.localToWorldMatrix)).ToArray());
                // every transform of the rig (sockets, equipment) at rest, by name
                var allT = root.GetComponentsInChildren<Transform>(true);
                var rig = "{" + string.Join(",", allT.GroupBy(t => t.name).Select(g => Q(g.Key) + ":" + J(g.First().localToWorldMatrix))) + "}";
                var clipJson = new List<string>();
                var bodyBones = body.bones;
                foreach (var clip in clips)
                {
                    int n = Mathf.Max(2, Mathf.CeilToInt(clip.length * fps) + 1);
                    var bodyPos = new List<float>(); var boneM = new List<float>();
                    for (int fi = 0; fi < n; fi++)
                    {
                        float t = clip.length * fi / (n - 1);
                        clip.SampleAnimation(root, t);
                        BodyCorrectives(root);
                        body.BakeMesh(baked, true);
                        var pl = Matrix4x4.TRS(body.transform.position, body.transform.rotation, Vector3.one);
                        bodyPos.AddRange(V3(baked.vertices.Select(v => pl.MultiplyPoint3x4(v)).ToArray()));
                        foreach (var b in bodyBones) boneM.AddRange(M(b.localToWorldMatrix));
                    }
                    string safe = clip.name.Replace("/", "_").Replace(" ", "_");
                    W(Path.Combine(dir, "clip_" + safe + "_body.f32"), bodyPos.ToArray());
                    W(Path.Combine(dir, "clip_" + safe + "_bones.f32"), boneM.ToArray());
                    clipJson.Add($"{{\"name\":{Q(clip.name)},\"file\":{Q(safe)},\"length\":{clip.length:R},\"frames\":{n}}}");
                }
                Object.DestroyImmediate(baked);
                File.WriteAllText(Path.Combine(dir, "hero.json"), $"{{\"id\":{Q(id)},\"parts\":[{string.Join(",", parts)}],\"clips\":[{string.Join(",", clipJson)}],\"rig\":{rig}}}");
                Debug.Log($"[CV2] dumped {id}: body v={body.sharedMesh.vertexCount} bones={body.bones.Length} clips={clips.Count}");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
