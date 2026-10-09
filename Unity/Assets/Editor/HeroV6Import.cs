using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Hero V6 retarget test: the fully generated (Higgsfield / Meshy) hero imported as a Unity Humanoid, so the
    /// approved Humanoid tennis clips play on it unchanged. Writes Library/BuildResults/hero_v6_import.txt.
    public static class HeroV6Import
    {
        public const string Model = "Assets/ArtDirection/HeroV6/Hero_V6.fbx";
        [MenuItem("Golf Arcade/Hero V6/Import As Humanoid")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            var imp = (ModelImporter)AssetImporter.GetAtPath(Model);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = false; imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.isReadable = true;
            imp.SaveAndReimport();
            // the generated texture as a URP Lit material (the FBX's embedded Standard material renders white in URP)
            const string texPath = "Assets/ArtDirection/HeroV6/Hero_V6_texture_0.png", matPath = "Assets/ArtDirection/HeroV6/Hero_V6.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", tex); mat.mainTexture = tex; mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", .22f); mat.SetFloat("_Metallic", 0); EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var hd = avatar ? avatar.humanDescription : default;
            string log = $"avatar={(avatar ? avatar.name : "none")} valid={(avatar && avatar.isValid)} human={(avatar && avatar.isHuman)}\n" +
                         $"renderers={smr.Length} verts={smr.Sum(r => r.sharedMesh ? r.sharedMesh.vertexCount : 0)} tris={smr.Sum(r => r.sharedMesh ? r.sharedMesh.triangles.Length / 3 : 0)}\n" +
                         "mapped: " + string.Join(", ", hd.human.Select(h => h.humanName + "=" + h.boneName)) + "\n";
            Directory.CreateDirectory("Library/BuildResults");
            File.WriteAllText("Library/BuildResults/hero_v6_import.txt", log);
            Debug.Log("[HeroV6Import] " + log);
        }
    }
}
