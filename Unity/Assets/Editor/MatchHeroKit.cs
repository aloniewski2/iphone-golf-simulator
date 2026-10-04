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
    /// DRESS_MATCH_HEROES: the White tennis kit (polo, shorts / skort, two socks, two shoes) on the two match heroes.
    /// Source: work/hero-dressed/export/<Sex>_Kit.fbx = the SAME armature as the 16 clip FBXs (same bone names, same rest pose, nothing animated) plus the six kit meshes, skinned to it in Blender.
    /// The clip FBXs are not touched. Here the kit FBX is imported (Assets/Characters/MatchHeroKit/, outside MatchHeroPostprocessor.Root so the clip import rules do not apply) and
    /// each of its SkinnedMeshRenderers is re-created on the prefab and re-bound BY NAME to the prefab's own bones (the bindposes in the kit mesh are the ones of the identical rest pose),
    /// with the role materials Kit_Shirt, Kit_ShirtTrim, Kit_Shorts, Kit_ShortsBand, Kit_Shoe, Kit_Sole, Kit_Sock (Assets/Characters/MatchHeroes/Materials/Kit/).
    public sealed class MatchHeroKitPostprocessor : AssetPostprocessor
    {
        public static bool Owns(string path) => path.StartsWith(MatchHeroKit.KitRoot) && path.EndsWith(".fbx");

        void OnPreprocessModel()
        {
            if (!Owns(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            m.globalScale = 1; m.useFileScale = true; m.bakeAxisConversion = false;           // same as the clip FBXs: the hero faces +Z in Unity, its right hand is +X
            m.preserveHierarchy = true; m.weldVertices = false; m.optimizeMeshPolygons = false; m.optimizeMeshVertices = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.isReadable = true;                                                                // the locker export and the bind test read the vertices
            // HERO_DETAIL: the cloth shader tilts its weave normal in the mesh's tangent frame, so the kit meshes (only these; the clip FBXs keep None) carry MikkTSpace tangents
            m.importNormals = ModelImporterNormals.Import; m.importTangents = ModelImporterTangents.CalculateMikk;
            m.importCameras = false; m.importLights = false; m.importVisibility = false; m.optimizeGameObjects = false;
            m.skinWeights = ModelImporterSkinWeights.Custom; m.maxBonesPerVertex = 4; m.minBoneWeight = .001f;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;             // only to learn the slot names (the prefab uses the role material assets)
            // Generic (not None: with no rig the importer makes static meshes and drops the skin); no animation is imported, the prefab's own Animator drives the bones
            m.animationType = ModelImporterAnimationType.Generic; m.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; m.importAnimation = false;
        }
    }

    public static class MatchHeroKit
    {
        public const string KitRoot = "Assets/Characters/MatchHeroKit/";
        public const string MatDir = "Assets/Characters/MatchHeroes/Materials/Kit/";
        const string Source = "../work/hero-dressed/export/";
        public static readonly string[] Pieces = { "Kit_Top", "Kit_Bottom", "Kit_Sock_L", "Kit_Sock_R", "Kit_Shoe_L", "Kit_Shoe_R" };
        public static string FbxPath(string sex) => KitRoot + sex + "_Kit.fbx";

        /// Authored colours of the White kit (sRGB): Std_White_Lit everywhere except the waistband and the shirt piping, which the White kit authors as the
        /// black contrast (#09090A), and the shoe sole, a darker warm grey rubber (HERO_DETAIL: the trim must not be the shirt's white, the sole not the upper's).
        public static Color Authored(string role)
        {
            if (role == MatchHeroLook.RoleShortsBand || role == MatchHeroLook.RoleShirtTrim) return new Color(MatchHeroLook.KitBlackR, MatchHeroLook.KitBlackR, MatchHeroLook.KitBlackB, 1);
            if (role == MatchHeroLook.RoleSole) return MatchHeroLook.KitSole;
            return new Color(MatchHeroLook.KitWhite, MatchHeroLook.KitWhite, MatchHeroLook.KitWhite, 1);
        }

        /// Copy <Sex>_Kit.fbx byte for byte into the project and import it.
        public static void CopyAndImport()
        {
            Directory.CreateDirectory(KitRoot);
            foreach (var sex in new[] { "Male", "Female" })
            {
                string src = Source + sex + "_Kit.fbx", dst = FbxPath(sex);
                if (!File.Exists(src)) throw new FileNotFoundException(src);
                if (!File.Exists(dst) || !File.ReadAllBytes(src).SequenceEqual(File.ReadAllBytes(dst))) File.Copy(src, dst, true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var sex in new[] { "Male", "Female" }) AssetDatabase.ImportAsset(FbxPath(sex), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        /// The seven role materials (URP Lit, flat, soft: smoothness .30 like Std_*_Lit roughness .7). Shared by both heroes; MatchHeroLook clones them per instance.
        public static Dictionary<string, Material> EnsureMaterials()
        {
            Directory.CreateDirectory(MatDir);
            var skinSource = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/MatchHeroes/Materials/MatchHero_Male_Skin.mat");
            var shader = skinSource ? skinSource.shader : Shader.Find("Universal Render Pipeline/Lit");
            var map = new Dictionary<string, Material>();
            foreach (var role in MatchHeroLook.KitRoles)
            {
                string path = MatDir + role + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = skinSource ? new Material(skinSource) : new Material(shader); m.name = role; AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                var c = Authored(role);
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .30f); m.SetFloat("_Metallic", 0);
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 2);           // closed shells: back faces are culled
                EditorUtility.SetDirty(m); map[role] = m;
            }
            return map;
        }

        static string PathOf(Transform t, Transform root) { var s = new Stack<string>(); for (var c = t; c && c != root; c = c.parent) s.Push(c.name); return string.Join("/", s); }

        /// Create the kit SkinnedMeshRenderers on the (not yet saved) prefab instance `root` and wire MatchHeroLook.kit. Returns a report.
        public static string Attach(GameObject root, string sex, MatchHeroLook look, SkinnedMeshRenderer body)
        {
            var sb = new StringBuilder();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath(sex));
            if (!model) throw new FileNotFoundException("kit FBX not imported: " + FbxPath(sex));
            var mats = EnsureMaterials();
            // bones of the prefab, by name (they must be unique: the rig is named after the humanoid bones)
            var rig = root.transform.Find("Rig_" + sex);
            if (!rig) throw new InvalidOperationException("no Rig_" + sex + " on the prefab");
            var byName = new Dictionary<string, Transform>(); var dup = new HashSet<string>();
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) if (!byName.TryAdd(t.name, t)) dup.Add(t.name);
            var kitSmrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (kitSmrs.Length != Pieces.Length || Pieces.Any(p => !kitSmrs.Any(s => s.name == p))) throw new InvalidOperationException($"{sex} kit FBX has {kitSmrs.Length} skinned renderers: {string.Join(",", kitSmrs.Select(s => s.name))}");
            var made = new List<SkinnedMeshRenderer>();
            foreach (var name in Pieces)
            {
                var src = kitSmrs.First(s => s.name == name);
                var go = new GameObject(name); go.transform.SetParent(root.transform, false);
                // same local placement as the imported node relative to the FBX root (Body_* sits at the same place)
                go.transform.localPosition = model.transform.InverseTransformPoint(src.transform.position);
                go.transform.localRotation = Quaternion.Inverse(model.transform.rotation) * src.transform.rotation;
                go.transform.localScale = src.transform.localScale;
                var smr = go.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = src.sharedMesh;
                var bones = new Transform[src.bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    string bn = src.bones[i].name;
                    if (dup.Contains(bn)) throw new InvalidOperationException("bone name is not unique on the prefab: " + bn);
                    if (!byName.TryGetValue(bn, out bones[i])) throw new InvalidOperationException($"{name}: bone '{bn}' of the kit FBX does not exist on the prefab rig");
                }
                smr.bones = bones;
                smr.rootBone = byName[src.rootBone ? src.rootBone.name : "Root"];
                smr.localBounds = body.localBounds;                      // same cull volume as the body: they share the root bone and the animated pose
                smr.updateWhenOffscreen = body.updateWhenOffscreen; smr.quality = body.quality;
                smr.shadowCastingMode = body.shadowCastingMode; smr.receiveShadows = body.receiveShadows; smr.lightProbeUsage = body.lightProbeUsage; smr.reflectionProbeUsage = body.reflectionProbeUsage;
                smr.motionVectorGenerationMode = body.motionVectorGenerationMode; smr.skinnedMotionVectors = body.skinnedMotionVectors; smr.allowOcclusionWhenDynamic = body.allowOcclusionWhenDynamic;
                var slots = src.sharedMaterials; var assign = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++)
                {
                    string role = slots[i] ? slots[i].name.Replace(" (Instance)", "") : "";
                    if (!mats.TryGetValue(role, out assign[i])) throw new InvalidOperationException($"{name}: material slot {i} is '{role}', not one of the kit roles");
                }
                smr.sharedMaterials = assign;
                made.Add(smr);
                int tris = 0; for (int s = 0; s < src.sharedMesh.subMeshCount; s++) tris += (int)(src.sharedMesh.GetIndexCount(s) / 3);
                sb.AppendLine($"  kit '{name}' mesh={src.sharedMesh.name} verts={src.sharedMesh.vertexCount} tris={tris} subs={src.sharedMesh.subMeshCount} bones={bones.Length} root={smr.rootBone.name} roles={string.Join(",", assign.Select(a => a.name))}");
            }
            look.kit = made.ToArray();
            return sb.ToString();
        }
    }
}
