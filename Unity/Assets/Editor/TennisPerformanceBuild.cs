#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    /// Authors the formerly empty reaction slots on each hero's own Generic rig.
    /// No retargeting, skeleton edits, gameplay timing changes or prefab rewrite.
    public static class TennisPerformanceBuild
    {
        const string Output = "Assets/Resources/Tennis/Performance";
        static readonly HeroTennisDriver.Clip[] Reactions =
        {
            HeroTennisDriver.Clip.Idle, HeroTennisDriver.Clip.HitPerfect,
            HeroTennisDriver.Clip.MissWhiff, HeroTennisDriver.Clip.CelebratePoint,
            HeroTennisDriver.Clip.SadPointLost, HeroTennisDriver.Clip.MatchWin,
            HeroTennisDriver.Clip.MatchLose
        };

        [MenuItem("Golf Arcade/Visual Overhaul/Build Tennis Performances")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var report = new System.Text.StringBuilder("GENERIC TENNIS PERFORMANCE ASSETS\n");
            foreach (var sex in new[] { "Male", "Female" })
            {
                var prefab = Resources.Load<GameObject>("Tennis/Customization/Player" + sex);
                if (!prefab) throw new InvalidOperationException("Missing Player" + sex);
                var root = UnityEngine.Object.Instantiate(prefab);
                root.transform.position = Vector3.zero; root.transform.rotation = Quaternion.identity;
                try
                {
                    var hero = root.GetComponent<MatchHeroLook>();
                    var driver = root.GetComponent<HeroTennisDriver>();
                    var ready = driver.slots.First(x => x.id == HeroTennisDriver.Clip.Ready).clip;
                    foreach (var id in Reactions)
                    {
                        var clip = Author(root, hero, ready, id, sex);
                        var path = Output + "/" + sex + "_" + id + ".anim";
                        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                        if (existing) { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); }
                        else AssetDatabase.CreateAsset(clip, path);
                        report.AppendLine(sex + ": " + id + " | " + path);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            var proof = Path.GetFullPath(Path.Combine(Application.dataPath, "../../proof/full-visual-overhaul/tennis"));
            Directory.CreateDirectory(proof);
            File.WriteAllText(Path.Combine(proof, "performance-build.txt"), report.ToString());
            Debug.Log(report);
        }

        static AnimationClip Author(GameObject root, MatchHeroLook hero, AnimationClip ready,
                                    HeroTennisDriver.Clip id, string sex)
        {
            var duration = id == HeroTennisDriver.Clip.Idle ? 4f : id == HeroTennisDriver.Clip.MatchWin ? 2.5f : 1.65f;
            var clip = new AnimationClip { name = sex + "_" + id, frameRate = 60, wrapMode = id == HeroTennisDriver.Clip.Idle ? WrapMode.Loop : WrapMode.Once };
            var animated = AnimationUtility.GetCurveBindings(ready).Where(b => b.type == typeof(Transform)).Select(b => b.path)
                .Concat(hero.bones.Where(b => b).Select(b => AnimationUtility.CalculateTransformPath(b, root.transform))).Distinct().ToArray();
            var transforms = animated.Select(p => root.transform.Find(p)).ToArray();
            if (transforms.Any(t => !t)) throw new InvalidOperationException("Ready clip path missing on " + sex);
            ready.SampleAnimation(root, Mathf.Min(.6f, ready.length));
            var basePosition = transforms.Select(t => t.localPosition).ToArray();
            var baseRotation = transforms.Select(t => t.localRotation).ToArray();
            var fistShapes = hero.body && hero.body.sharedMesh ? Enumerable.Range(0,hero.body.sharedMesh.blendShapeCount).Where(i=>hero.body.sharedMesh.GetBlendShapeName(i).Contains("Hero_Fist",StringComparison.Ordinal)).ToArray() : Array.Empty<int>();
            var baseShapes = fistShapes.Select(i=>hero.body.GetBlendShapeWeight(i)).ToArray();
            var shapeCurves = fistShapes.Select(i=>new AnimationCurve()).ToArray();
            var curves = new AnimationCurve[animated.Length, 7];
            for (int b = 0; b < animated.Length; b++) for (int k = 0; k < 7; k++) curves[b, k] = new AnimationCurve();
            int frames = Mathf.RoundToInt(duration * 60);
            for (int frame = 0; frame <= frames; frame++)
            {
                float t = frame / 60f;
                // Constant finger bones may have no keys in an imported Ready clip.
                // Start every frame from the same measured pose before adding a gesture.
                for (int b = 0; b < transforms.Length; b++)
                { transforms[b].localPosition = basePosition[b]; transforms[b].localRotation = baseRotation[b]; }
                ready.SampleAnimation(root, Mathf.Min(.6f, ready.length));
                for(int k=0;k<fistShapes.Length;k++)hero.body.SetBlendShapeWeight(fistShapes[k],baseShapes[k]);
                if (id == HeroTennisDriver.Clip.Idle)
                {
                    float breath = Mathf.Sin(t / duration * Mathf.PI * 2) * .7f;
                    Turn(hero.Bone(HumanBodyBones.Chest), root.transform.right, breath);
                    Turn(hero.Bone(HumanBodyBones.Head), root.transform.up, Mathf.Sin(t / duration * Mathf.PI * 2) * 1.5f);
                }
                else
                {
                    float u = t / duration;
                    float w = u < .12f ? 0 : u < .28f ? Smooth((u - .12f) / .16f)
                        : u < .66f ? 1 : u < .92f ? 1 - Smooth((u - .66f) / .26f) : 0;
                    // One clear gesture: short preparation, firm money pose, softened settle.
                    float settle = 1 + .07f * Mathf.Exp(-Mathf.Max(0, u - .28f) * 22) * Mathf.Sin(Mathf.Max(0, u - .28f) * 45);
                    Pose(root.transform, hero, id, w * settle);
                }
                for(int k=0;k<fistShapes.Length;k++)shapeCurves[k].AddKey(t,hero.body.GetBlendShapeWeight(fistShapes[k]));
                for (int b = 0; b < animated.Length; b++)
                {
                    var tr = transforms[b]; var p = tr.localPosition; var q = tr.localRotation;
                    float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                    for (int k = 0; k < 7; k++) curves[b, k].AddKey(t, values[k]);
                }
            }
            for(int k=0;k<fistShapes.Length;k++)
            {
                var path=AnimationUtility.CalculateTransformPath(hero.body.transform,root.transform);
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(SkinnedMeshRenderer),"blendShape."+hero.body.sharedMesh.GetBlendShapeName(fistShapes[k])),shapeCurves[k]);
            }
            string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
            for (int b = 0; b < animated.Length; b++) for (int k = 0; k < 7; k++)
            {
                var curve = curves[b, k];
                for (int key = 0; key < curve.length; key++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(animated[b], typeof(Transform), properties[k]), curve);
            }
            clip.EnsureQuaternionContinuity();
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = id == HeroTennisDriver.Clip.Idle; settings.loopBlend = settings.loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            return clip;
        }

        static float Smooth(float t) => t * t * (3 - 2 * t);
        static void Turn(Transform bone, Vector3 axis, float degrees) { if (bone) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation; }
        internal static void Pose(Transform root, MatchHeroLook h, HeroTennisDriver.Clip id, float w, bool closeFist=true)
        {
            if (w <= .00001f) return;
            bool win = id == HeroTennisDriver.Clip.MatchWin;
            bool happy = win || id == HeroTennisDriver.Clip.HitPerfect || id == HeroTennisDriver.Clip.CelebratePoint;
            bool sad = id == HeroTennisDriver.Clip.SadPointLost || id == HeroTennisDriver.Clip.MatchLose;
            Turn(h.Bone(HumanBodyBones.Spine), root.up, (happy ? -6 : sad ? 3 : -5) * w);
            Turn(h.Bone(HumanBodyBones.Chest), root.right, (happy ? -3 : sad ? 5 : -3) * w);
            Turn(h.Bone(HumanBodyBones.Head), root.right, (happy ? -5 : sad ? 10 : -8) * w);
            Turn(h.Bone(HumanBodyBones.Head), root.up, (sad ? -7 : 5) * w);
            Vector3 left = win ? new Vector3(-.38f, 1.68f, .12f)
                : id == HeroTennisDriver.Clip.CelebratePoint ? new Vector3(-.53f, 1.45f, .22f)
                : id == HeroTennisDriver.Clip.HitPerfect ? new Vector3(-.52f, 1.28f, .24f)
                : id == HeroTennisDriver.Clip.MatchLose ? new Vector3(-.27f, .99f, -.04f)
                : sad ? new Vector3(-.31f, .85f, .08f) : new Vector3(-.50f, 1.16f, .14f);
            Reach(h, root, false, left, w, happy);
            if (win)
            {
                Reach(h, root, true, new Vector3(.47f, 1.55f, .24f), w);
                // The racket passes outside the face on its way up, so the win's
                // expression remains visible throughout entry and return.
                var hand = h.Bone(HumanBodyBones.RightHand);
                if (hand && h.racketGrip)
                {
                    var shaft = h.racketGrip.TransformDirection(Vector3.up);
                    var local = Vector3.Lerp(new Vector3(.9f,.3f,.12f), new Vector3(.55f,.83f,.08f), Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,.90f,w)));
                    var goal = root.TransformDirection(local.normalized);
                    hand.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(shaft,goal), Mathf.Clamp01(w*4)) * hand.rotation;
                }
            }
            else
            {
                // The free hand must not rise THROUGH the Ready racket's string bed.
                // Lower the held racket outside the right thigh before the gesture.
                Reach(h, root, true, new Vector3(.32f, sad ? .85f : .95f, .14f), w);
                var hand = h.Bone(HumanBodyBones.RightHand);
                if (hand && h.racketGrip)
                {
                    var shaft = h.racketGrip.TransformDirection(Vector3.up);
                    var goal = root.TransformDirection(new Vector3(.72f, -.50f, .16f).normalized);
                    hand.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(shaft, goal), Mathf.Clamp01(w)) * hand.rotation;
                }
            }
            // The approved rig has rolled intermediate/distal bones; the shared
            // measured hinge helper closes a real palm-facing fist on both bodies.
            // This is baked here, once after every Ready reset, never layered twice.
            if (happy && closeFist) HeroGripPolish.ApplyFist(h, true, Mathf.Clamp01(w) * (win ? .95f : .85f));
        }

        static void Reach(MatchHeroLook h, Transform root, bool right, Vector3 localTarget, float w, bool carryWrist = false)
        {
            var upper = h.Bone(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            var lower = h.Bone(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            var hand = h.Bone(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (!upper || !lower || !hand) return;
            var target = Vector3.Lerp(hand.position, root.TransformPoint(localTarget), Mathf.Clamp01(w));
            var wrist = hand.rotation;var wristLocal = hand.localRotation;
            float l1 = Vector3.Distance(upper.position, lower.position), l2 = Vector3.Distance(lower.position, hand.position);
            var dir = target - upper.position; float d = Mathf.Clamp(dir.magnitude, Mathf.Abs(l1 - l2) + .001f, l1 + l2 - .002f);
            dir.Normalize();
            var pole = Vector3.ProjectOnPlane(root.right * (right ? 1 : -1) + root.forward * .2f, dir).normalized;
            float along = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            var elbow = upper.position + dir * along + pole * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            if(carryWrist) hand.localRotation = wristLocal;else hand.rotation = wrist;
        }
    }
}
#endif
