using UnityEditor;

namespace GolfArcade.EditorTools
{
    /// Import settings for Adnan's modular Hero in Resources/Hero (blender/scripts/hero_golf_retarget.py
    /// and hero_parts_export.py): one Generic rig with the golf clips, and skinned parts (body, hair
    /// cuts, headwear, clothes, shoes) that the game rebinds to that rig by bone name. The clips are kept
    /// uncompressed (the phone scrubs the backswing through them) and blend shapes are kept, since the
    /// girl's shape is a blend shape ("Female") on the body and the clothes.
    public sealed class HeroModelImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Hero/")) return;
            var importer = (ModelImporter)assetImporter;
            bool clips = assetPath.EndsWith("/hero_golf.fbx");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = clips;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.useFileScale = true;
            importer.globalScale = 1f;
        }

        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith("Assets/Resources/UI/Look/"))
            {
                // the style thumbnails of the locker's tiles: small, clear, no mip blur
                var thumb = (TextureImporter)assetImporter;
                thumb.mipmapEnabled = false; thumb.alphaIsTransparency = true; thumb.maxTextureSize = 256;
                thumb.textureCompression = TextureImporterCompression.CompressedHQ;
                thumb.filterMode = UnityEngine.FilterMode.Bilinear; thumb.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                return;
            }
            if (!assetPath.StartsWith("Assets/Resources/Hero/Look/")) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("hero_mask.png"))
            {
                // region weights, not colour: no sRGB, no compression, nothing smeared between regions
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.filterMode = UnityEngine.FilterMode.Bilinear;
            }
            else if (assetPath.EndsWith("hero_normal.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
        }
    }
}
