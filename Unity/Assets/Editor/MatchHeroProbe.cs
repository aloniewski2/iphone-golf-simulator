using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GolfArcade.EditorTools
{
    /// HERO_MAINSTAY evidence tools (edit mode, no Play mode, batch-safe). Each writes under work/hero-mainstay/logs or data. Nothing in the game refers to them.
    ///   ServeTimeline    the Serve clip's tossing-hand / racket heights over time (picks the serve ritual's stance / release / trophy times)
    ///   TransitionProbe  cross-fades between two clips on the Generic rig: the racket's path over the blend
    ///   MaskProbe        the upper-body layer: with Ready on the base and a swing on the upper layer, which bones follow which clip
    ///   BlenderExport    the world position of every bone at every frame of a few clips, in Blender's frame, to compare with Blender's own evaluation
    public static class MatchHeroProbe
    {
        const string Prefabs = "Assets/Resources/Tennis/Customization/Player";
        static string Logs => "../work/hero-mainstay/logs/";

        static GameObject Hero(string sex, out HeroTennisDriver drv, out MatchHeroLook look)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + sex + ".prefab"));
            drv = go.GetComponent<HeroTennisDriver>(); look = go.GetComponent<MatchHeroLook>(); return go;
        }
        static AnimationClip ClipOf(HeroTennisDriver d, HeroTennisDriver.Clip id) => d.slots.First(s => s.id == id).clip;
        static void Done(int code) { if (Application.isBatchMode) EditorApplication.Exit(code); }

        // ================================================================ serve timeline
        public static void ServeTimeline()
        {
            try
            {
                foreach (var sex in new[] { "Male", "Female" })
                {
                    var go = Hero(sex, out var drv, out var look); var clip = ClipOf(drv, HeroTennisDriver.Clip.Serve);
                    var sb = new System.Text.StringBuilder("t | leftHand(x,y,z) | rightHand(x,y,z) | strings(x,y,z) | head y | hips y\n");
                    var lh = look.Bone(HumanBodyBones.LeftHand); var rh = look.Bone(HumanBodyBones.RightHand);
                    var bed = look.racketGrip.GetComponentsInChildren<Renderer>(true).First(r => r.name == "StringBed");
                    for (int f = 0; f <= 48; f++)
                    {
                        clip.SampleAnimation(go, f / 30f);
                        Vector3 L(Vector3 w) => go.transform.InverseTransformPoint(w);
                        sb.AppendLine($"{f / 30f:0.000} | {L(lh.position):F3} | {L(rh.position):F3} | {L(bed.bounds.center):F3} | {L(look.Bone(HumanBodyBones.Head).position).y:F3} | {L(look.Bone(HumanBodyBones.Hips).position).y:F3}");
                    }
                    Directory.CreateDirectory(Logs); File.WriteAllText(Logs + "serve_timeline_" + sex + ".txt", sb.ToString());
                    UnityEngine.Object.DestroyImmediate(go);
                }
                Done(0);
            }
            catch (Exception e) { Debug.LogException(e); Done(1); }
        }

        // ================================================================ transitions
        public static void TransitionProbe()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var sex in new[] { "Male", "Female" })
                {
                    var go = Hero(sex, out var drv, out var look); var an = go.GetComponent<Animator>();
                    var bed = look.racketGrip.GetComponentsInChildren<Renderer>(true).First(r => r.name == "StringBed");
                    var K = HeroTennisDriver.Clip.Ready;
                    var cases = new (string name, HeroTennisDriver.Clip a, float ta, HeroTennisDriver.Clip b, float tb)[]
                    {
                        ("Ready->Forehand@0.484", K, 1.1f, HeroTennisDriver.Clip.Forehand, .4837f), ("Ready->Backhand@0.45", K, 1.1f, HeroTennisDriver.Clip.Backhand, .45f),
                        ("Ready->Return@0.25", K, 1.1f, HeroTennisDriver.Clip.Return, .25f), ("Ready->Serve@0.167", K, 1.1f, HeroTennisDriver.Clip.Serve, .167f),
                        ("Forehand@1.0->Ready", HeroTennisDriver.Clip.Forehand, 1.0f, K, 1.1f), ("Backhand@1.0->Ready", HeroTennisDriver.Clip.Backhand, 1.0f, K, 1.1f),
                        ("Serve@1.2->Ready", HeroTennisDriver.Clip.Serve, 1.2f, K, 1.1f), ("Overhead@1.2->Ready", HeroTennisDriver.Clip.Smash, 1.2f, K, 1.1f),
                        ("Forehand@0.48->Overhead@0.45", HeroTennisDriver.Clip.Forehand, .4837f, HeroTennisDriver.Clip.Smash, .45f),
                        ("Forehand@0.48->Volley@0.15", HeroTennisDriver.Clip.Forehand, .4837f, HeroTennisDriver.Clip.Volley, .15f),
                        ("Forehand@0.48->ForehandWide@0.7", HeroTennisDriver.Clip.Forehand, .4837f, HeroTennisDriver.Clip.ForehandWide, .7f),
                        ("Backhand@0.45->BackhandWide@0.65", HeroTennisDriver.Clip.Backhand, .45f, HeroTennisDriver.Clip.BackhandWide, .65f),
                        ("Backhand@0.45->Return@0.3", HeroTennisDriver.Clip.Backhand, .45f, HeroTennisDriver.Clip.Return, .3f),
                        ("Ready->Overhead@0.4", K, 1.1f, HeroTennisDriver.Clip.Smash, .4f),
                        ("Serve@0.567->Serve@0.9", HeroTennisDriver.Clip.Serve, .567f, HeroTennisDriver.Clip.Serve, .9f),
                    };
                    foreach (var cs in cases)
                    {
                        var graph = PlayableGraph.Create("tp"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                        var mixer = AnimationMixerPlayable.Create(graph, 2);
                        var pa = AnimationClipPlayable.Create(graph, ClipOf(drv, cs.a)); var pb = AnimationClipPlayable.Create(graph, ClipOf(drv, cs.b));
                        graph.Connect(pa, 0, mixer, 0); graph.Connect(pb, 0, mixer, 1); pa.SetTime(cs.ta); pb.SetTime(cs.tb); pa.SetSpeed(0); pb.SetSpeed(0);
                        AnimationPlayableOutput.Create(graph, "o", an).SetSourcePlayable(mixer);
                        Vector3? prev = null; float path = 0, maxStep = 0; Vector3 first = default, last = default;
                        for (int i = 0; i <= 10; i++)
                        {
                            float w = i / 10f; mixer.SetInputWeight(0, 1 - w); mixer.SetInputWeight(1, w); graph.Evaluate(0);
                            var sc = go.transform.InverseTransformPoint(bed.bounds.center);
                            if (prev.HasValue) { float d = Vector3.Distance(prev.Value, sc); path += d; maxStep = Mathf.Max(maxStep, d); }
                            prev = sc; if (i == 0) first = sc; if (i == 10) last = sc;
                        }
                        float straight = Vector3.Distance(first, last);
                        sb.AppendLine($"{sex,-6} {cs.name,-36} strings path {path:0.00} m (straight {straight:0.00} m, ratio {(path / Mathf.Max(.05f, straight)):0.0}), largest 10%-step {maxStep:0.00} m");
                        graph.Destroy();
                    }
                    UnityEngine.Object.DestroyImmediate(go);
                }
                Directory.CreateDirectory(Logs); File.WriteAllText(Logs + "transition_probe.txt", sb.ToString());
                Done(0);
            }
            catch (Exception e) { Debug.LogException(e); Done(1); }
        }

        // ================================================================ upper-body layer
        /// Base layer = ReadyIdle, upper layer = Forehand@0.6 at weight 1, masked to hips + spine and up. Reports how far each bone is from the plain Ready pose and from
        /// the plain Forehand pose: the legs must be Ready's, the arms and the racket Forehand's.
        public static void MaskProbe()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                var go = Hero("Male", out var drv, out var look); var an = go.GetComponent<Animator>();
                var graph = PlayableGraph.Create("mp"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var baseMix = AnimationMixerPlayable.Create(graph, 1); var upMix = AnimationMixerPlayable.Create(graph, 1);
                var pr = AnimationClipPlayable.Create(graph, ClipOf(drv, HeroTennisDriver.Clip.Ready)); var pf2 = AnimationClipPlayable.Create(graph, ClipOf(drv, HeroTennisDriver.Clip.Forehand));
                graph.Connect(pr, 0, baseMix, 0); graph.Connect(pf2, 0, upMix, 0); baseMix.SetInputWeight(0, 1); upMix.SetInputWeight(0, 1);
                var layers = AnimationLayerMixerPlayable.Create(graph, 2); graph.Connect(baseMix, 0, layers, 0); graph.Connect(upMix, 0, layers, 1); layers.SetInputWeight(0, 1); layers.SetInputWeight(1, 1);
                var mask = new AvatarMask(); var root = an.transform; var paths = new List<string>();
                string PathOf(Transform t) { var p = t.name; for (var q = t.parent; q && q != root; q = q.parent) p = q.name + "/" + p; return p; }
                paths.Add(PathOf(look.Bone(HumanBodyBones.Hips)));
                foreach (var t in look.Bone(HumanBodyBones.Spine).GetComponentsInChildren<Transform>(true)) paths.Add(PathOf(t));
                mask.transformCount = paths.Count; for (int i = 0; i < paths.Count; i++) { mask.SetTransformPath(i, paths[i]); mask.SetTransformActive(i, true); }
                layers.SetLayerMaskFromAvatarMask(1, mask);
                AnimationPlayableOutput.Create(graph, "o", an).SetSourcePlayable(layers);
                pr.SetTime(1.1); pf2.SetTime(.6); pr.SetSpeed(0); pf2.SetSpeed(0);
                Dictionary<string, Vector3> Snap() => look.bones.Where(b => b).GroupBy(b => b.name).ToDictionary(g => g.Key, g => go.transform.InverseTransformPoint(g.First().position));
                Dictionary<string, Vector3> Plain(HeroTennisDriver.Clip id, float t)
                {
                    var g = PlayableGraph.Create("r"); g.SetTimeUpdateMode(DirectorUpdateMode.Manual); var c = AnimationClipPlayable.Create(g, ClipOf(drv, id)); c.SetTime(t); c.SetSpeed(0);
                    AnimationPlayableOutput.Create(g, "o", an).SetSourcePlayable(c); g.Evaluate(0); var s = Snap(); g.Destroy(); return s;
                }
                var refReady = Plain(HeroTennisDriver.Clip.Ready, 1.1f); var refFore = Plain(HeroTennisDriver.Clip.Forehand, .6f);
                graph.Evaluate(0); var mixed = Snap();
                sb.AppendLine("bone | distance to plain Ready (mm) | distance to plain Forehand@0.6 (mm)");
                foreach (var b in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head, HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.RightIndexDistal })
                {
                    string n = b.ToString(); sb.AppendLine($"{n,-16} {Vector3.Distance(mixed[n], refReady[n]) * 1000,8:0.0} {Vector3.Distance(mixed[n], refFore[n]) * 1000,8:0.0}");
                }
                Directory.CreateDirectory(Logs); File.WriteAllText(Logs + "mask_probe.txt", sb.ToString());
                graph.Destroy(); UnityEngine.Object.DestroyImmediate(go);
                Done(0);
            }
            catch (Exception e) { Debug.LogException(e); Done(1); }
        }

        // ================================================================ compare with Blender
        /// Every rig bone's world position (hero space, in Blender's axes) at every frame of a few clips.
        /// Unity (x, y, z) = (-bx, bz, -by) of Blender, so Blender (bx, by, bz) = (-ux, -uz, uy).  Output: work/hero-mainstay/data/unity_bones_<sex>_<clip>.json
        public static void BlenderExport()
        {
            try
            {
                Directory.CreateDirectory("../work/hero-mainstay/data");
                foreach (var sex in new[] { "Male", "Female" })
                {
                    var go = Hero(sex, out var drv, out var look);
                    var bones = look.bones.Where(b => b).GroupBy(b => b.name).Select(g => g.First()).ToArray();
                    foreach (var id in new[] { HeroTennisDriver.Clip.Ready, HeroTennisDriver.Clip.Forehand, HeroTennisDriver.Clip.Backhand, HeroTennisDriver.Clip.Serve, HeroTennisDriver.Clip.Smash, HeroTennisDriver.Clip.Volley, HeroTennisDriver.Clip.Return })
                    {
                        var clip = ClipOf(drv, id); int frames = Mathf.RoundToInt(clip.length * 30);
                        var sb = new System.Text.StringBuilder("{\"clip\":\"" + clip.name + "\",\"fps\":30,\"bones\":[" + string.Join(",", bones.Select(b => "\"" + b.name + "\"")) + "],\"frames\":[");
                        for (int f = 0; f <= frames; f++)
                        {
                            clip.SampleAnimation(go, f / 30f);
                            if (f > 0) sb.Append(',');
                            sb.Append('[').Append(string.Join(",", bones.Select(b => { var u = go.transform.InverseTransformPoint(b.position); return $"[{-u.x:0.00000},{-u.z:0.00000},{u.y:0.00000}]"; }))).Append(']');
                        }
                        sb.Append("]}");
                        File.WriteAllText($"../work/hero-mainstay/data/unity_bones_{sex}_{id}.json", sb.ToString());
                    }
                    UnityEngine.Object.DestroyImmediate(go);
                }
                Done(0);
            }
            catch (Exception e) { Debug.LogException(e); Done(1); }
        }
    }
}
