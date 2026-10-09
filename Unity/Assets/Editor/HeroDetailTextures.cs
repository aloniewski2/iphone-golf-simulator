using System.IO;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// HERO_DETAIL: import rules for Assets/Resources/Tennis/HeroDetail/ (made by work/character-shader/detail/make_textures.py).
    ///   Cloth_Weave.png   packed data (R,G weave normal, B thread break, A height): linear, tiles, high quality compression, never crunched.
    ///   Skin_Soft_N.png   a normal map: tiles.
    ///   Kit_*.png         the baked seam / collar / hem multipliers in the kit meshes' UV layouts: sRGB colour, CLAMPED (UV islands, not a tile).
    /// All keep mip maps (the weave must average out to flat with distance instead of shimmering) and trilinear / anisotropic filtering; iOS gets ASTC.
    public sealed class HeroDetailTexturePostprocessor : AssetPostprocessor
    {
        public const string Root = "Assets/Resources/Tennis/HeroDetail/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            ti.mipmapEnabled = true; ti.streamingMipmaps = false; ti.isReadable = false; ti.crunchedCompression = false;
            ti.npotScale = TextureImporterNPOTScale.None; ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 4;
            ti.alphaIsTransparency = false;
            var ios = new TextureImporterPlatformSettings { name = "iPhone", overridden = true, maxTextureSize = 2048, textureCompression = TextureImporterCompression.Compressed };
            if (n.EndsWith("_N") && n.StartsWith("Kit_"))
            {
                ti.textureType = TextureImporterType.NormalMap; ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaSource = TextureImporterAlphaSource.None; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.maxTextureSize = 2048; ios.maxTextureSize = 2048; ios.format = TextureImporterFormat.ASTC_8x8;
            }
            else if (n.EndsWith("_M") && n.StartsWith("Kit_"))
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.maxTextureSize = 2048; ios.maxTextureSize = 2048; ios.format = TextureImporterFormat.ASTC_8x8;
            }
            else if (n == "Cloth_FabricAtlas")
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.wrapMode = TextureWrapMode.Clamp;
                ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.maxTextureSize = 512; ios.maxTextureSize = 512; ios.format = TextureImporterFormat.ASTC_4x4;
            }
            else if (n == "Skin_Soft_N")
            {
                ti.textureType = TextureImporterType.NormalMap; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 2;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ios.format = TextureImporterFormat.ASTC_4x4; ios.maxTextureSize = 256;
            }
            else if (n == "Cloth_Weave")
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.wrapMode = TextureWrapMode.Repeat;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = 256; ios.format = TextureImporterFormat.ASTC_4x4; ios.maxTextureSize = 256;
            }
            else
            {
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaSource = TextureImporterAlphaSource.None; ti.wrapMode = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = 2048; ios.format = TextureImporterFormat.ASTC_6x6;
            }
            ti.SetPlatformTextureSettings(ios);
        }
    }

    /// One-shot setup after the kit FBXs / textures change: re-imports the two kit FBXs (they now carry tangents) and the detail textures.
    ///   Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.HeroDetailSetup.Run -quit
    public static class HeroDetailSetup
    {
        [MenuItem("Golf Arcade/Match Heroes/Reimport kit + detail textures")]
        public static void Run()
        {
            foreach (var sex in new[] { "Male", "Female" }) AssetDatabase.ImportAsset(MatchHeroKit.FbxPath(sex), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Tennis/HeroDetail" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (var sex in new[] { "Male", "Female" })
                foreach (var r in AssetDatabase.LoadAllAssetsAtPath(MatchHeroKit.FbxPath(sex)))
                    if (r is Mesh mesh && mesh.name.StartsWith("Kit_"))
                        Debug.Log($"[HeroDetailSetup] {sex} {mesh.name}: verts={mesh.vertexCount} tangents={(mesh.tangents != null && mesh.tangents.Length == mesh.vertexCount)} uv={(mesh.uv != null && mesh.uv.Length == mesh.vertexCount)}");
            Debug.Log("[HeroDetailSetup] done");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
