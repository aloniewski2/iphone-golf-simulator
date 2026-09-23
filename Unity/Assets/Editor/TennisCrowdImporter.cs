using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Clip splits for the seated crowd FBXs built by blender/scripts/build_crowd.py, read from
    /// blender/crowd-clips.json (the only authority: edits to the .meta are undone here).
    public sealed class TennisCrowdImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Resources/Tennis/Crowd/";

        [System.Serializable] class Clip { public string name; public int firstFrame, lastFrame; public bool loop; }
        [System.Serializable] class Gender { public Clip[] clips; public float seatOffset; }
        [System.Serializable] class Manifest { public Gender male, female; }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../blender/crowd-clips.json"));
            if (!File.Exists(path)) { Debug.LogWarning("[Crowd] clip manifest missing: " + path); return; }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            var gender = assetPath.Contains("female") ? manifest.female : manifest.male;
            if (gender?.clips == null) return;
            importer.clipAnimations = gender.clips.Select(c => new ModelImporterClipAnimation
            {
                name = c.name, firstFrame = c.firstFrame, lastFrame = c.lastFrame,
                loopTime = c.loop, wrapMode = c.loop ? WrapMode.Loop : WrapMode.Once,
                lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true
            }).ToArray();
        }
    }
}
