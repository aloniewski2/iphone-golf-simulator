using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GolfArcade.Tennis;

namespace GolfArcade.EditorTools
{
    /// Hero V5: the body / wardrobe FBXs were re-exported from the 42-bone finger rig, so their bone order differs
    /// from what the hero prefabs serialized for each SkinnedMeshRenderer. For every prefab renderer whose mesh
    /// lives in an FBX, take the bone NAMES that FBX's own renderer uses (same order as the mesh bind poses) and
    /// rebind to the prefab's single skeleton. Writes Library/BuildResults/hero_v5_rebind.txt.
    public static class HeroV5Rebind
    {
        static readonly string[] Prefabs = {
            "Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab",
            "Assets/ArtDirection/Hero01/Prefabs/Hero_01_RestoreMotion.prefab",
        };

        /// Materials for a hair-FBX mesh, by the FBX renderer's material names: the body's skin materials (Hair_Bald
        /// is the scalp in skin), Hero_01_HairTuft_<Style> = the hair material x Textures/Hair_<Style>.png
        /// (buzz / waves detail maps, tinted by HeroKit like all hair), anything else = the Swept hair material.
        static Material[] MaterialsFor(string meshName, string fbxPath, SkinnedMeshRenderer hairR, GameObject root)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var src = model ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.sharedMesh && r.sharedMesh.name == meshName) : null;
            if (!src) return hairR.sharedMaterials;
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Body_Skin");
            var outM = new Material[src.sharedMaterials.Length];
            for (int i = 0; i < outM.Length; i++)
            {
                string n = src.sharedMaterials[i] ? src.sharedMaterials[i].name.Replace(" (Instance)", "") : "";
                Material m = body ? body.sharedMaterials.FirstOrDefault(b => b && b.name.Replace(" (Instance)", "") == n) : null;
                if (!m && n.StartsWith("Hero_01_HairTuft_"))
                {
                    string style = n.Substring("Hero_01_HairTuft_".Length), path = "Assets/ArtDirection/Hero01/Materials/" + n + ".mat";
                    m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!m) { m = new Material(hairR.sharedMaterial) { name = n }; AssetDatabase.CreateAsset(m, path); }
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ArtDirection/Hero01/Textures/Hair_" + style + ".png");
                    if (tex) { m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
                    EditorUtility.SetDirty(m);
                }
                outM[i] = m ? m : hairR.sharedMaterial;
            }
            return outM;
        }

        [MenuItem("Golf Arcade/Hero V5/Rebind Prefab Bones")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var log = new List<string>();
            foreach (var path in Prefabs)
            {
                if (!File.Exists(path)) { log.Add("missing " + path); continue; }
                var root = PrefabUtility.LoadPrefabContents(path);
                var look = root.GetComponent<ModularHeroLook>();
                Transform skel = look && look.skeletonRoot ? look.skeletonRoot : root.transform;
                var map = new Dictionary<string, Transform>();
                foreach (var t in skel.GetComponentsInChildren<Transform>(true))
                    if (!map.ContainsKey(t.name) && t.GetComponent<Renderer>() == null) map[t.name] = t;
                int fixedN = 0;
                // Hero V5: every other hair mesh in the hair FBX (haircuts + band fills) becomes a sibling renderer of
                // Hair_Default, off by default (HeroKit enables the chosen haircut)
                var hairR = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Hair_Default");
                if (hairR)
                {
                    var hp = AssetDatabase.GetAssetPath(hairR.sharedMesh);
                    foreach (var hm in AssetDatabase.LoadAllAssetsAtPath(hp).OfType<Mesh>().Where(m => m.name.StartsWith("Hair_") && m.name != "Hair_Default"))
                    {
                        var existing = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == hm.name);
                        var mats = MaterialsFor(hm.name, hp, hairR, root);
                        if (existing) { existing.sharedMesh = hm; existing.sharedMaterials = mats; existing.enabled = false; continue; }   // only Hair_Default draws by default
                        var go = new GameObject(hm.name); go.transform.SetParent(hairR.transform.parent, false);
                        go.transform.localPosition = hairR.transform.localPosition; go.transform.localRotation = hairR.transform.localRotation; go.transform.localScale = hairR.transform.localScale;
                        var f = go.AddComponent<SkinnedMeshRenderer>(); f.sharedMesh = hm; f.sharedMaterials = mats; f.enabled = false; f.updateWhenOffscreen = true;
                        log.Add($"{Path.GetFileName(path)}: added {hm.name} ({hm.vertexCount} verts)");
                    }
                }
                foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = smr.sharedMesh; if (!mesh) continue;
                    var mp = AssetDatabase.GetAssetPath(mesh); if (!mp.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(mp); if (!model) continue;
                    var src = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.sharedMesh == mesh);
                    if (!src) { log.Add($"{path}: {smr.name} no source renderer in {mp}"); continue; }
                    var names = src.bones.Select(b => b ? b.name : null).ToArray();
                    var nb = new Transform[names.Length]; var miss = new List<string>();
                    for (int i = 0; i < names.Length; i++)
                        if (names[i] != null && map.TryGetValue(names[i], out var t)) nb[i] = t; else miss.Add(names[i] ?? "null");
                    int before = smr.bones.Length;
                    smr.bones = nb;
                    if (src.rootBone && map.TryGetValue(src.rootBone.name, out var rb)) smr.rootBone = rb;
                    smr.localBounds = mesh.bounds;
                    fixedN++;
                    log.Add($"{Path.GetFileName(path)}: {smr.name} <- {Path.GetFileName(mp)} bones {before}->{nb.Length} verts={mesh.vertexCount} bindposes={mesh.bindposes.Length}" + (miss.Count > 0 ? " MISSING " + string.Join(",", miss) : ""));
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                log.Add($"{path}: rebound {fixedN} renderers");
            }
            Directory.CreateDirectory("Library/BuildResults");
            File.WriteAllText("Library/BuildResults/hero_v5_rebind.txt", string.Join("\n", log) + "\n");
            Debug.Log("[HeroV5Rebind]\n" + string.Join("\n", log));
        }
    }
}
