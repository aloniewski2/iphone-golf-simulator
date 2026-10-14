using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// ACCESSORIES: installs the items built in work/accessories (tennis cap, golf cap, golf glove, golf bag) as runtime data for GolfArcade.Tennis.HeroAccessories:
    /// per sex and item, Assets/Resources/Accessories/<Sex>/<item>/{<Piece>.asset (mesh with weights + bind poses, readable), item.json, Acc_<Sex>_<item>_<Tag>_N|M.png}.
    /// Source: work/accessories/out/<Sex>/<item>/unity_item.json (written by py/prep_install.py) + <Piece>.bin + <Tag>_N|M.png. No prefab, body, rig or clip is touched.
    /// Run: -executeMethod GolfArcade.EditorTools.AccessoriesInstall.Run   (env ACC_SRC = out folder, ACC_ONLY = comma list of items)
    public static class AccessoriesInstall
    {
        [Serializable] class Sub { public string role; public int start, count; }
        [Serializable] class Piece { public string name, renderer, mesh, maps; public float metresPerUv; public int vertexCount, triangleCount, texture; public string[] bones, roles; public float[] bindposes; public Sub[] submeshes; }
        [Serializable] class Item { public string item, sex, slot, parentBone, socket; public float[] socketPosition, socketRotation; public Piece[] pieces; }

        static string Source => Environment.GetEnvironmentVariable("ACC_SRC") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../work/accessories/out"));

        public static void Run()
        {
            int code = 0;
            try
            {
                var log = new StringBuilder();
                string only = Environment.GetEnvironmentVariable("ACC_ONLY");
                foreach (var sex in new[] { "Male", "Female" })
                {
                    string sdir = Path.Combine(Source, sex);
                    if (!Directory.Exists(sdir)) continue;
                    foreach (var idir in Directory.GetDirectories(sdir).OrderBy(d => d))
                    {
                        string id = Path.GetFileName(idir);
                        if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(id)) continue;
                        string json = Path.Combine(idir, "unity_item.json");
                        if (!File.Exists(json)) { log.AppendLine($"  skip {sex}/{id}: no unity_item.json"); continue; }
                        log.Append(InstallItem(sex, id, idir, JsonUtility.FromJson<Item>(File.ReadAllText(json))));
                    }
                }
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(Source);
                File.WriteAllText(Path.Combine(Source, "install_report.txt"), log.ToString());
                Debug.Log("[Accessories] " + log);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        static string InstallItem(string sex, string id, string srcDir, Item item)
        {
            var sb = new StringBuilder();
            string dst = $"Assets/Resources/Accessories/{sex}/{id}";
            Directory.CreateDirectory(dst);
            var maps = new List<string>(); var mapSize = new Dictionary<string, int>();
            foreach (var p in item.pieces)
            {
                BuildMesh(srcDir, p, $"{dst}/{p.mesh}.asset", sex, id);
                foreach (var kind in new[] { "N", "M" })
                {
                    string s = Path.Combine(srcDir, $"{p.maps}_{kind}.png"), d = $"{dst}/Acc_{sex}_{id}_{p.maps}_{kind}.png";
                    if (!File.Exists(s)) throw new FileNotFoundException(s);
                    if (!File.Exists(d) || !File.ReadAllBytes(s).SequenceEqual(File.ReadAllBytes(d))) File.Copy(s, d, true);
                    if (!maps.Contains(d)) maps.Add(d);
                    // skin an accessory covers (HeroAccessories drops these body triangles): <Tag>_hide.bin -> Acc_<Sex>_<item>_<Tag>_hide.bytes
                    string hs = Path.Combine(srcDir, $"{p.maps}_hide.bin"), hd = $"{dst}/Acc_{sex}_{id}_{p.maps}_hide.bytes";
                    if (File.Exists(hs) && (!File.Exists(hd) || !File.ReadAllBytes(hs).SequenceEqual(File.ReadAllBytes(hd)))) File.Copy(hs, hd, true);
                    mapSize[d] = p.texture > 0 ? p.texture : 2048;
                }
                sb.AppendLine($"  {sex} {id} {p.renderer}: v={p.vertexCount} tris={p.triangleCount} bones={p.bones.Length} roles={string.Join(",", p.roles)}");
            }
            // the runtime reads a trimmed copy (mesh binding data stays in the mesh asset)
            var rt = new RuntimeItem { item = item.item, sex = item.sex, slot = item.slot, parentBone = item.parentBone, socket = item.socket, socketPosition = item.socketPosition, socketRotation = item.socketRotation,
                pieces = item.pieces.Select(p => new RuntimePiece { name = p.name, renderer = p.renderer, mesh = p.mesh, maps = p.maps, metresPerUv = p.metresPerUv, bones = p.bones, roles = p.roles }).ToArray() };
            File.WriteAllText($"{dst}/item.json", JsonUtility.ToJson(rt, true));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in maps)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                bool normal = path.EndsWith("_N.png");
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Trilinear;
                ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false;
                int size = mapSize[path];
                ti.isReadable = false; ti.maxTextureSize = size; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "iPhone", overridden = true, maxTextureSize = size, format = TextureImporterFormat.ASTC_8x8, compressionQuality = 100 });
                ti.SaveAndReimport();
            }
            return sb.ToString();
        }

        [Serializable] class RuntimePiece { public string name, renderer, mesh, maps; public float metresPerUv; public string[] bones, roles; }
        [Serializable] class RuntimeItem { public string item, sex, slot, parentBone, socket; public float[] socketPosition, socketRotation; public RuntimePiece[] pieces; }

        static void BuildMesh(string dir, Piece p, string assetPath, string sex, string id)
        {
            var bytes = File.ReadAllBytes(Path.Combine(dir, p.name + ".bin"));
            int n = p.vertexCount, off = 0;
            float F() { float v = BitConverter.ToSingle(bytes, off); off += 4; return v; }
            int I() { int v = BitConverter.ToInt32(bytes, off); off += 4; return v; }
            var pos = new Vector3[n]; for (int i = 0; i < n; i++) pos[i] = new Vector3(F(), F(), F());
            var nrm = new Vector3[n]; for (int i = 0; i < n; i++) nrm[i] = new Vector3(F(), F(), F());
            var uv = new Vector2[n]; for (int i = 0; i < n; i++) uv[i] = new Vector2(F(), F());
            var bi = new int[n * 4]; for (int i = 0; i < n * 4; i++) bi[i] = I();
            var bw = new float[n * 4]; for (int i = 0; i < n * 4; i++) bw[i] = F();
            var tris = new int[p.triangleCount * 3]; for (int i = 0; i < tris.Length; i++) tris[i] = I();
            if (off != bytes.Length) throw new InvalidOperationException($"{p.name}.bin: {bytes.Length - off} unread bytes (manifest says {n} vertices / {p.triangleCount} triangles)");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool created = !mesh;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = $"Acc {id} {p.mesh} {sex}";
            mesh.indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = pos; mesh.normals = nrm; mesh.uv = uv;
            mesh.subMeshCount = p.submeshes.Length;
            for (int s = 0; s < p.submeshes.Length; s++)
            {
                var sub = p.submeshes[s];
                var t = new int[sub.count * 3]; Array.Copy(tris, sub.start * 3, t, 0, t.Length);
                mesh.SetTriangles(t, s, false);
            }
            var weights = new BoneWeight[n];
            for (int i = 0; i < n; i++)
                weights[i] = new BoneWeight { boneIndex0 = bi[i * 4], boneIndex1 = bi[i * 4 + 1], boneIndex2 = bi[i * 4 + 2], boneIndex3 = bi[i * 4 + 3],
                    weight0 = bw[i * 4], weight1 = bw[i * 4 + 1], weight2 = bw[i * 4 + 2], weight3 = bw[i * 4 + 3] };
            mesh.boneWeights = weights;
            var binds = new Matrix4x4[p.bones.Length];
            for (int b = 0; b < binds.Length; b++) { var m = new Matrix4x4(); for (int k = 0; k < 16; k++) m[k % 4, k / 4] = p.bindposes[b * 16 + k]; binds[b] = m; }
            mesh.bindposes = binds;
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, assetPath); else EditorUtility.SetDirty(mesh);
        }
    }
}
