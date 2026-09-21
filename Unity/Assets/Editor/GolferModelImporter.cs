using UnityEditor;

namespace GolfArcade.EditorTools
{
    /// Import settings for the rigged golfer in Resources/Golfer (FBX out of blender/golfer.blend):
    /// a generic rig with its baked Swing clip kept uncompressed, since the game scrubs it by the
    /// phone's backswing and every frame of the IK-baked arms matters.
    public sealed class GolferModelImporter : AssetPostprocessor
    {
        bool IsStandard => assetPath.StartsWith("Assets/Resources/StandardCharacters/") || assetPath.StartsWith("Assets/Tests/Fixtures/StandardCharacters/");
        void OnPostprocessModel(UnityEngine.GameObject root)
        {
            if (IsStandard && !root.GetComponent<GolfArcade.Game.StandardCharacterArms>())
                root.AddComponent<GolfArcade.Game.StandardCharacterArms>();
        }

        void OnPreprocessModel()
        {
            bool standard = IsStandard;
            if (!standard && !assetPath.StartsWith("Assets/Resources/Golfer/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            if (standard && assetPath.EndsWith("_golf.fbx"))
                importer.clipAnimations = new[] { new ModelImporterClipAnimation {
                    name = "StandardGolfDrive", firstFrame = 0, lastFrame = 90,
                    loopTime = false, lockRootRotation = true,
                    lockRootHeightY = true, lockRootPositionXZ = true
                }};
            if (standard && assetPath.EndsWith("_tennis.fbx") && assetPath.Contains("Resources/"))
            {
                string[] names = { "Ready", "SplitStep", "RunLeft", "RunRight", "Forehand", "Backhand" };
                int[] starts = { 0, 69, 138, 207, 552, 621 };
                var clips = new ModelImporterClipAnimation[names.Length];
                for (int i = 0; i < names.Length; i++) clips[i] = new ModelImporterClipAnimation {
                    name = names[i], firstFrame = starts[i], lastFrame = starts[i] + 60,
                    loopTime = i < 4, lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true
                };
                importer.clipAnimations = clips;
            }
        }
    }
}
