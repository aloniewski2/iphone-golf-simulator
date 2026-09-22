using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

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
                importer.clipAnimations = TennisClips();
        }

        [System.Serializable] class ClipDef
        {
            public string name;
            public int firstFrame, lastFrame;
            public bool loop;
            public bool lockHeight;
        }

        [System.Serializable] class ClipManifest { public ClipDef[] clips; }

        /// Tennis clip splits, derived in Blender from the animation studio's timeline markers
        /// and published to `blender/tennis-clips.json` by `export_tennis_runtime.py`.
        ///
        /// This runs on every import and is therefore the only authority on the clip list:
        /// setting `clipAnimations` from outside, or editing the `.fbx.meta` by hand, is
        /// silently undone here. Six clips used to be hardcoded, which is why the other
        /// thirty-eight authored animations never reached the game.
        static ModelImporterClipAnimation[] TennisClips()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../blender/tennis-clips.json"));
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Tennis] clip manifest missing ({path}); importing without clip splits");
                return new ModelImporterClipAnimation[0];
            }
            var manifest = JsonUtility.FromJson<ClipManifest>(File.ReadAllText(path));
            if (manifest?.clips == null || manifest.clips.Length == 0)
            {
                Debug.LogError($"[Tennis] clip manifest empty: {path}");
                return new ModelImporterClipAnimation[0];
            }
            // Lateral root motion stays locked -- gameplay drives court position, and a run
            // clip's baked X travel would fight the simulation. Height is different: the
            // serve, smash, volley and dives all have real vertical motion animated in, and
            // locking it is why the characters never left the ground.
            return manifest.clips.Select(c => new ModelImporterClipAnimation {
                name = c.name, firstFrame = c.firstFrame, lastFrame = c.lastFrame,
                loopTime = c.loop, wrapMode = c.loop ? WrapMode.Loop : WrapMode.Once,
                lockRootRotation = true, lockRootHeightY = c.lockHeight, lockRootPositionXZ = true
            }).ToArray();
        }
    }
}
