using System.Linq;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Reports which animation clips the tennis FBXs actually expose at runtime, so a clip
    /// that exists in the .meta but never reaches Resources is visible immediately.
    public static class ClipAudit
    {
        /// This project has Auto Refresh disabled, so editing an .fbx.meta on disk does not
        /// by itself invalidate Unity's cached import — the clip list silently stays stale.
        /// Regenerating clip splits therefore has to force the reimport explicitly.
        public static void Reimport()
        {
            foreach (var gender in new[] { "male", "female" })
                AssetDatabase.ImportAsset($"Assets/Resources/StandardCharacters/standard_{gender}_tennis.fbx",
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            Run();
        }

        public static void Run()
        {
            foreach (var gender in new[] { "male", "female" })
            {
                string path = $"Assets/Resources/StandardCharacters/standard_{gender}_tennis.fbx";
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .Where(c => !c.name.StartsWith("__")).OrderBy(c => c.name).ToList();
                Debug.Log($"AUDIT {gender} count={clips.Count}");
                Debug.Log($"AUDIT {gender} names={string.Join(",", clips.Select(c => $"{c.name}:{c.length:0.00}s"))}");
                // Report vertical root travel that survived import: this is what decides
                // whether the serve actually leaves the ground.
                foreach (var clip in clips)
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if (!binding.propertyName.Contains("Position.y") && !binding.propertyName.EndsWith("m_LocalPosition.y")) continue;
                        if (!string.IsNullOrEmpty(binding.path) && !binding.path.EndsWith("Root")) continue;
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        foreach (var key in curve.keys) { lo = Mathf.Min(lo, key.value); hi = Mathf.Max(hi, key.value); }
                    }
                    if (hi > lo) Debug.Log($"ROOTY {gender} {clip.name} travel={hi - lo:0.000}");
                }
            }
        }
    }
}
