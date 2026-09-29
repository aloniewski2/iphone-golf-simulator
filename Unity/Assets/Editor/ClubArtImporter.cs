using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Import settings for Adnan's "Island Sports Club" menu art in Resources/Club (from his branch's
    /// GolfArcade/Unity/MenuArt): the rendered objects (club-*.png) are UI sprites; the painted scenes
    /// (scene-*.jpg) are full-screen backdrops, drawn by the ClubBackdrop shader, so no mips and clamped.
    public sealed class ClubArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Club/")) return;
            var importer = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileName(assetPath);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (file.StartsWith("club-"))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 512;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 2048;
            }
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
