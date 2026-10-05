using UnityEditor;

namespace GolfArcade.EditorTools
{
    /// Import settings for the golfers in Resources/Hero (blender/scripts/matchhero_golf.py: golfer_m.fbx and
    /// golfer_f.fbx, Adnan's match heroes with the golf clips): one Generic 53-bone rig with the body, the face,
    /// the kit, the hair, the clubs and the clips. The clips are kept uncompressed (the phone scrubs the
    /// backswing through them), the skin keeps up to four bones a vertex (shoulders, wrists, fingers).
    public sealed class HeroModelImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Hero/")) return;
            var importer = (ModelImporter)assetImporter;
            bool clips = assetPath.EndsWith("/golfer_m.fbx") || assetPath.EndsWith("/golfer_f.fbx");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = clips;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 4;
            importer.minBoneWeight = 0.001f;
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
