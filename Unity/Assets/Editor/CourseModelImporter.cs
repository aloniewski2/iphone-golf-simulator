using UnityEditor;

namespace GolfArcade.EditorTools
{
    /// Import settings for the modelled courses in Resources/Course (FBX out of Blender): the
    /// ground meshes become MeshColliders at runtime, which needs their data readable in a
    /// player build; nothing else in the file is wanted but meshes, transforms and material names
    /// — and the sea's blendshapes: Hole 12's wave grid carries its swell as four morph targets
    /// that WaterMotion cross-fades.
    public sealed class CourseModelImporter : AssetPostprocessor
    {
        /// Bumped when the rules change, so the models re-import with them.
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Course/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.isReadable = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.useFileScale = true;
            importer.globalScale = 1f;
        }
    }
}
