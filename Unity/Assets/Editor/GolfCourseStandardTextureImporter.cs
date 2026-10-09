using System.IO;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    public sealed class GolfCourseStandardTextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Course/Standard/")) return;
            var t = (TextureImporter)assetImporter;
            bool normal = Path.GetFileNameWithoutExtension(assetPath).EndsWith("_N");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !normal;
            t.alphaSource = TextureImporterAlphaSource.None;
            t.mipmapEnabled = true; t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear; t.anisoLevel = 16;
            t.maxTextureSize = 1024; t.textureCompression = TextureImporterCompression.Compressed;
            t.crunchedCompression = false;
            t.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "iPhone", overridden = true,
                maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6, compressionQuality = 100 });
        }
    }
}
