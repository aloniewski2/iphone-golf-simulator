using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// DRESS_MATCH_HEROES probe (read-only): which transforms do the match clips animate? -> work/hero-dressed/logs/probe_clip_paths.txt
    public static class MatchHeroKitProbe
    {
        public static void Run()
        {
            int code = 0;
            try
            {
                var sb = new StringBuilder();
                foreach (var sex in new[] { "Male", "Female" })
                    foreach (var clipName in new[] { "ReadyIdle", "Forehand" })
                    {
                        var clip = AssetDatabase.LoadAllAssetsAtPath(MatchHeroPostprocessor.FbxPath(sex, clipName)).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                        var binds = AnimationUtility.GetCurveBindings(clip);
                        var top = binds.Where(b => !b.path.Contains("/")).Select(b => b.path + ":" + b.propertyName).Distinct().OrderBy(s => s).ToArray();
                        sb.AppendLine($"{sex} {clipName}: {binds.Length} curves; paths without '/': {string.Join(", ", top)}");
                        var firstLevel = binds.Select(b => b.path.Split('/')[0] + (b.path.Contains("/") ? "/" + b.path.Split('/')[1] : "")).Distinct().OrderBy(s => s).Take(8);
                        sb.AppendLine("   first two path levels: " + string.Join(" | ", firstLevel));
                        foreach (var name in new[] { "Rig_" + sex, "Body_" + (sex == "Male" ? "M" : "F"), "Rig_" + sex + "/Root" })
                        {
                            var props = binds.Where(b => b.path == name).Select(b => b.propertyName).ToArray();
                            sb.AppendLine($"   path '{name}': {(props.Length == 0 ? "NOT animated" : string.Join(",", props))}");
                        }
                    }
                Directory.CreateDirectory("../work/hero-dressed/logs");
                File.WriteAllText("../work/hero-dressed/logs/probe_clip_paths.txt", sb.ToString());
            }
            catch (Exception e) { Debug.LogException(e); code = 1; }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
    }
}
