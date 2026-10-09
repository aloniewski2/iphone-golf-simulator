using UnityEditor;

namespace GolfArcade.EditorTools
{
    /// Import settings for the Higgsfield character maps baked by
    /// blender/scripts/fit_higgs_characters.py: the normal map must be imported as a normal map
    /// (the shader unpacks it as one), and both are capped for the phone.
    public sealed class TennisCharacterTextureImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Tennis/Characters/Higgs", Island = "Assets/Resources/Tennis/Island/Island_";

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Resources/Tennis/Customization/")) {
                var custom=(TextureImporter)assetImporter;
                custom.textureType=TextureImporterType.Default;
                custom.sRGBTexture=!assetPath.Contains("Mask");
                custom.alphaIsTransparency=false;
                custom.maxTextureSize=512;
                custom.mipmapEnabled=true;
                custom.textureCompression=TextureImporterCompression.Uncompressed;
                return;
            }
            if (!assetPath.StartsWith(Folder) && !assetPath.StartsWith(Island)) return;
            var t = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_Normal.png");
            bool mask = assetPath.EndsWith("_Mask.png");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !normal && !mask;
            if(mask){t.alphaIsTransparency=false;t.alphaSource=TextureImporterAlphaSource.FromInput;}
            t.maxTextureSize = normal ? 1024 : 2048;
            t.mipmapEnabled = true;
            t.anisoLevel = 4;
            t.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
