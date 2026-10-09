using UnityEditor;
namespace GolfArcade.EditorTools
{
    /// Runtime botanical batching needs source vertices; it never imports colliders or animation.
    sealed class GolfResortKitImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Course/Resort/"))return;
            var importer=(TextureImporter)assetImporter;
            bool normal=assetPath.EndsWith("_N.png");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal;importer.mipmapEnabled=true;importer.wrapMode=UnityEngine.TextureWrapMode.Repeat;
            importer.anisoLevel=8;importer.maxTextureSize=assetPath.Contains("/Sky")?2048:512;importer.alphaIsTransparency=false;
            importer.textureCompression=TextureImporterCompression.CompressedHQ;
        }
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Course/Resort/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importNormals = ModelImporterNormals.Import;
        }
    }
}
