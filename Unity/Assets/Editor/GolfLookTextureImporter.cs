using System.IO;
using UnityEditor;

namespace GolfArcade.EditorTools
{
    /// Import settings for the postcard holes' generated look textures (Assets/Resources/Course/Look/,
    /// LOOK_CONTRACT.md section 3), modelled on TennisCharacterTextureImporter:
    ///   *_N.png  tangent-space normal map (OpenGL +Y, linear)
    ///   *_C.png, *_E.png  sRGB colour (albedo / emission); RGBA cards (Surf, Fall, Smoke) keep their alpha as transparency
    ///   wrap Repeat (tiling ground), except Plants_C (palette atlas) and Sky_* (panorama): Clamp
    ///   mipmaps on, aniso 4, max 1024 (a 512 source stays 512; the 2048-wide sky keeps 2048), compressed, never crunched.
    public sealed class GolfLookTextureImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Resources/Course/Look/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            string file = Path.GetFileNameWithoutExtension(assetPath);
            bool normal = file.EndsWith("_N");
            bool sky = file.StartsWith("Sky_");
            bool atlas = file.StartsWith("Plants_");
            bool card = file.StartsWith("Surf_") || file.StartsWith("Fall_") || file.StartsWith("Smoke_");

            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !normal;
            t.alphaSource = card ? TextureImporterAlphaSource.FromInput : (normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput);
            t.alphaIsTransparency = card;
            t.wrapMode = sky || atlas ? UnityEngine.TextureWrapMode.Clamp : UnityEngine.TextureWrapMode.Repeat;
            t.mipmapEnabled = true;
            t.anisoLevel = 4;
            t.filterMode = UnityEngine.FilterMode.Trilinear;
            t.maxTextureSize = sky ? 2048 : 1024;
            t.textureCompression = TextureImporterCompression.Compressed;
            t.crunchedCompression = false;
            t.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
