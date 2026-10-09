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
        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Resources/Course/")) return;
            var importer = (ModelImporter)assetImporter;
            // Codex's cinematic swing (hole_NN_cinematic.fbx) is an animation: one baked clip
            // moving the ball and its stripe and the trail tubes' morph targets, scrubbed by
            // CinematicRig — so it comes in as a Legacy clip, uncompressed.
            bool cinematic = assetPath.EndsWith("_cinematic.fbx");
            importer.isReadable = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = cinematic;
            importer.importBlendShapes = true;
            importer.importVisibility = false;
            importer.animationType = cinematic ? ModelImporterAnimationType.Legacy : ModelImporterAnimationType.None;
            if (cinematic) { importer.animationCompression = ModelImporterAnimationCompression.Off; importer.resampleCurves = true; }
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.useFileScale = true;
            importer.globalScale = 1f;
        }
    }
}
