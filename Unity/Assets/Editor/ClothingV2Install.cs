using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// CLOTHING V2: installs the garments built in work/clothing-v2 (each a complete garment with thickness, finished edges and its own
    /// skin weights over the hero's real body) on the four hero prefabs. One SkinnedMeshRenderer per piece (Kit_Top, Kit_Bottom, Kit_Sock_L/R,
    /// Kit_Shoe_L/R, Kit_Belt, Kit_Glove_L/R), bound BY NAME to the prefab's own bones, role materials per submesh, MatchHeroLook.kitV2 set.
    /// Source: work/clothing-v2/out/<Hero>/unity/manifest.json + <Piece>.bin (positions, normals, uv0, bone indices, weights, triangles; body mesh
    /// space; bind poses of the hero's body). Meshes: Assets/Characters/KitsV2/<Hero>/<Piece>.asset; maps: Resources/<Tennis|Golf>/KitsV2/<Sex>/.
    /// Body, face, rig, clips and the old kit sources are not touched. Rebuild: work/clothing-v2/run.sh.
    public static class ClothingV2Install
    {
        const string MeshRoot = "Assets/Characters/KitsV2/";
        static readonly (string hero, string prefab, string matDir)[] Heroes =
        {
            ("MatchHero_Male", "Assets/Resources/Tennis/Customization/PlayerMale.prefab", "Assets/Characters/MatchHeroes/Materials/Kit/"),
            ("MatchHero_Female", "Assets/Resources/Tennis/Customization/PlayerFemale.prefab", "Assets/Characters/MatchHeroes/Materials/Kit/"),
            ("GolfKitHero_Male", "Assets/Resources/Tennis/Customization/PlayerMaleGolfKit.prefab", "Assets/Characters/GolfHeroKit/Materials/"),
            ("GolfKitHero_Female", "Assets/Resources/Tennis/Customization/PlayerFemaleGolfKit.prefab", "Assets/Characters/GolfHeroKit/Materials/"),
        };

        [Serializable] class Sub { public string role; public int start, count; }
        [Serializable] class Piece { public string name; public int vertexCount, triangleCount; public Sub[] submeshes; public string[] bones; public float[] bindposes; public float metresPerUv; public int texture; }
        [Serializable] class Manifest { public string hero; public Piece[] pieces; }

        static string Source => Environment.GetEnvironmentVariable("CV2_SRC") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../work/clothing-v2/out"));

        public static void Run()
        {
            int code = 0;
            try
            {
                var log = new StringBuilder();
                string only = Environment.GetEnvironmentVariable("CV2_HEROES");
                InstallMaps(only, log);
                foreach (var h in Heroes)
                    if (string.IsNullOrEmpty(only) || only.Split(',').Contains(h.hero)) log.Append(Install(h.hero, h.prefab, h.matDir));
                AssetDatabase.SaveAssets();
                File.WriteAllText(Path.Combine(Source, "install_report.txt"), log.ToString());
                Debug.Log("[ClothingV2] " + log);
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// N / M maps of every piece -> Resources/<Tennis|Golf>/KitsV2/<Sex>/KitV2_<Sport>_<Sex>_<Tag>_N|M.png, the fabric atlas ->
        /// Resources/Tennis/KitsV2/KitV2_FabricAtlas.png; all linear (data, not colour), mipmapped, clamped, ASTC on iOS (8x8 maps, 4x4 atlas).
        static void InstallMaps(string only, StringBuilder log)
        {
            var copied = new List<(string path, bool normal, int size, bool atlas)>();
            void Copy(string src, string dst, bool normal, int size, bool atlas)
            {
                if (!File.Exists(src)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                if (!File.Exists(dst) || !File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst))) File.Copy(src, dst, true);
                copied.Add((dst, normal, size, atlas));
            }
            Copy(Path.Combine(Source, "KitV2_FabricAtlas.png"), "Assets/Resources/Tennis/KitsV2/KitV2_FabricAtlas.png", false, 512, true);
            foreach (var h in Heroes)
            {
                if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(h.hero)) continue;
                string sport = h.hero.StartsWith("Golf") ? "Golf" : "Tennis", sex = h.hero.EndsWith("Female") ? "Female" : "Male";
                string dir = Path.Combine(Source, h.hero, "unity");
                var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(dir, "manifest.json")));
                foreach (var p in man.pieces)
                {
                    string tag = p.name.Substring(4).Replace("_", "");
                    foreach (var kind in new[] { "N", "M" })
                        Copy(Path.Combine(dir, $"{tag}_{kind}.png"), $"Assets/Resources/{sport}/KitsV2/{sex}/KitV2_{sport}_{sex}_{tag}_{kind}.png", kind == "N", p.texture, false);
                    string corr = Path.Combine(dir, p.name + "_correctives.json"), corrDst = $"Assets/Resources/{sport}/KitsV2/{sex}/KitV2_{sport}_{sex}_{tag}_correctives.json";
                    if (File.Exists(corr) && (!File.Exists(corrDst) || !File.ReadAllBytes(corr).SequenceEqual(File.ReadAllBytes(corrDst)))) { Directory.CreateDirectory(Path.GetDirectoryName(corrDst)); File.Copy(corr, corrDst, true); }
                    string contact = Path.Combine(dir, p.name + "_contact.bin"), contactDst = $"Assets/Resources/{sport}/KitsV2/{sex}/KitV2_{sport}_{sex}_{tag}_contact.bytes";
                    if (File.Exists(contact) && (!File.Exists(contactDst) || !File.ReadAllBytes(contact).SequenceEqual(File.ReadAllBytes(contactDst)))) { Directory.CreateDirectory(Path.GetDirectoryName(contactDst)); File.Copy(contact, contactDst, true); }
                    string hide = Path.Combine(dir, p.name + "_hide.bin"), hideDst = $"Assets/Resources/{sport}/KitsV2/{sex}/KitV2_{sport}_{sex}_{tag}_hide.bytes";
                    if (File.Exists(hide) && (!File.Exists(hideDst) || !File.ReadAllBytes(hide).SequenceEqual(File.ReadAllBytes(hideDst)))) { Directory.CreateDirectory(Path.GetDirectoryName(hideDst)); File.Copy(hide, hideDst, true); }
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var (path, normal, size, atlas) in copied)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Trilinear;
                ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = false;
                ti.isReadable = false; ti.maxTextureSize = size; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "iPhone", overridden = true, maxTextureSize = size,
                    format = atlas ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_8x8, compressionQuality = 100 });
                ti.SaveAndReimport();
            }
            log.AppendLine($"  maps: {copied.Count} textures");
        }

        static Mesh BuildMesh(string dir, Piece p, string assetPath)
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
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool created = !mesh;
            if (created) mesh = new Mesh();
            mesh.Clear();
            mesh.name = "KitV2 " + Path.GetFileNameWithoutExtension(assetPath);
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
            // pose correctives (KitV2PoseCorrectives): bind-space position + normal deltas per anchor pose
            string shapesFile = Path.Combine(dir, p.name + "_shapes.bin");
            mesh.ClearBlendShapes();
            if (File.Exists(shapesFile))
            {
                var sb = File.ReadAllBytes(shapesFile); int so = 0;
                int count = BitConverter.ToInt32(sb, so); so += 4; int nv = BitConverter.ToInt32(sb, so); so += 4;
                if (nv != n) throw new InvalidOperationException($"{p.name}: corrective shapes have {nv} vertices, mesh has {n}");
                for (int k = 0; k < count; k++)
                {
                    string name = System.Text.Encoding.ASCII.GetString(sb, so, 16).TrimEnd('\0'); so += 16;
                    var dp = new Vector3[n]; var dn = new Vector3[n];
                    for (int i = 0; i < n; i++) { dp[i] = new Vector3(BitConverter.ToSingle(sb, so), BitConverter.ToSingle(sb, so + 4), BitConverter.ToSingle(sb, so + 8)); so += 12; }
                    for (int i = 0; i < n; i++) { dn[i] = new Vector3(BitConverter.ToSingle(sb, so), BitConverter.ToSingle(sb, so + 4), BitConverter.ToSingle(sb, so + 8)); so += 12; }
                    mesh.AddBlendShapeFrame(name, 100, dp, dn, null);
                }
            }
            if (created) { AssetDatabase.CreateAsset(mesh, assetPath); mesh.name = "KitV2 " + Path.GetFileNameWithoutExtension(assetPath); }   // CreateAsset renames the mesh to the file name: put the KitV2 prefix back
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Material RoleMaterial(string dir, string role, Material like)
        {
            string path = dir + role + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = like ? new Material(like) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = role;
            var c = role == MatchHeroLook.RoleBelt ? new Color(.36f, .22f, .13f) : role == MatchHeroLook.RoleBuckle ? new Color(.72f, .73f, .74f) : role == MatchHeroLook.RoleSole ? MatchHeroLook.KitSole : new Color(.93f, .93f, .93f);
            m.SetColor("_BaseColor", c); if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .3f); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static string Install(string hero, string prefabPath, string matDir)
        {
            var sb = new StringBuilder();
            string dir = Path.Combine(Source, hero, "unity");
            var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(dir, "manifest.json")));
            Directory.CreateDirectory(MeshRoot + hero);
            var meshes = new Dictionary<string, Mesh>();
            foreach (var p in man.pieces) meshes[p.name] = BuildMesh(dir, p, MeshRoot + hero + "/" + p.name + ".asset");
            AssetDatabase.SaveAssets();
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var look = root.GetComponent<MatchHeroLook>();
                var body = look.body;
                var byName = new Dictionary<string, Transform>();
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (!byName.ContainsKey(t.name)) byName[t.name] = t;
                var like = AssetDatabase.LoadAssetAtPath<Material>(matDir + "Kit_Shirt.mat");
                // pieces are swapped one at a time: only the pieces in the manifest are replaced, the rest of the kit stays as it is
                var kit = (look.kit ?? new SkinnedMeshRenderer[0]).ToList();
                var names = (look.kitV2Bones ?? new MatchHeroLook.KitBones[0]).ToList();
                while (names.Count < kit.Count) names.Add(null);
                foreach (var p in man.pieces)
                {
                    int slot = kit.FindIndex(r => r && r.name == p.name);
                    var old = slot >= 0 ? kit[slot] : null;
                    var go = new GameObject(p.name);
                    go.transform.SetParent(old ? old.transform.parent : body.transform.parent, false);
                    go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
                    if (old) go.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    var smr = go.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = meshes[p.name];
                    // ArmBlend_* support bones exist only at runtime (HeroArmSkinning; identity bind poses relative to the body): until
                    // MatchHeroLook.BindKitV2 binds them by name, the body renderer's own transform stands in (identity -> rest shape).
                    smr.bones = p.bones.Select(n => byName.TryGetValue(n, out var t) ? t : n.StartsWith("ArmBlend_") ? body.transform : throw new InvalidOperationException($"{hero} {p.name}: bone {n} missing on {prefabPath}")).ToArray();
                    smr.rootBone = body.rootBone;
                    smr.localBounds = body.localBounds;
                    smr.updateWhenOffscreen = body.updateWhenOffscreen; smr.quality = body.quality;
                    smr.shadowCastingMode = body.shadowCastingMode; smr.receiveShadows = body.receiveShadows; smr.lightProbeUsage = body.lightProbeUsage; smr.reflectionProbeUsage = body.reflectionProbeUsage;
                    smr.motionVectorGenerationMode = body.motionVectorGenerationMode; smr.skinnedMotionVectors = body.skinnedMotionVectors; smr.allowOcclusionWhenDynamic = body.allowOcclusionWhenDynamic;
                    smr.sharedMaterials = p.submeshes.Select(s_ => RoleMaterial(matDir, s_.role, like)).ToArray();
                    var entry = new MatchHeroLook.KitBones { names = p.bones };
                    if (slot >= 0) { kit[slot] = smr; names[slot] = entry; UnityEngine.Object.DestroyImmediate(old.gameObject); }
                    else { kit.Add(smr); names.Add(entry); }
                    sb.AppendLine($"  {hero} {p.name}: v={p.vertexCount} tris={p.triangleCount} bones={p.bones.Length} roles={string.Join(",", p.submeshes.Select(s_ => s_.role))}{(old ? " (replaced the old piece)" : " (new piece)")}");
                }
                look.kit = kit.ToArray();
                look.kitV2Bones = names.ToArray();
                look.kitV2 = true;
                if (look.golfKit) look.SetGolfHand(false);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return sb.ToString();
        }
    }
}
