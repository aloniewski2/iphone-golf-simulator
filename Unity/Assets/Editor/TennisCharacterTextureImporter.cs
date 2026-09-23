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
            if (!assetPath.StartsWith(Folder) && !assetPath.StartsWith(Island)) return;
            var t = (TextureImporter)assetImporter;
            bool normal = assetPath.EndsWith("_Normal.png");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !normal;
            t.maxTextureSize = normal ? 1024 : 2048;
            t.mipmapEnabled = true;
            t.anisoLevel = 4;
            t.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
