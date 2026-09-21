using System;
using System.Collections.Generic;
using GolfArcade.Game;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GolfArcade.Tennis
{
    public sealed class TennisActor : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        readonly Dictionary<string, int> indices = new();
        Transform model, root, racket;
        StandardCharacterArms arms;
        Transform hand, offHand, chest;
        TrailRenderer strokeTrail;
        Material strokeMaterial;
        public Vector3 SweetVelocity { get; private set; }
        public Transform SweetSpot { get; private set; }
        Transform stringRight, stringUp, stringNormal, basisOrigin;
        public Vector3 StringRight => (stringRight.position - basisOrigin.position).normalized;
        public Vector3 StringUp => (stringUp.position - basisOrigin.position).normalized;
        public Vector3 StringNormal => (stringNormal.position - basisOrigin.position).normalized;
        public float SwingAge { get; private set; } = 10;
        public float Power { get; private set; }
        public bool Swinging => SwingAge < TennisRules.StrokeDuration;
        bool backhand;
        float locomotion;

        public void Build(bool female, Color skin)
        {
            string path = "StandardCharacters/standard_" + (female ? "female" : "male") + "_tennis";
            var prefab = Resources.Load<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Missing permanent tennis character: " + path);
            model = Instantiate(prefab, transform).transform;
            model.name = female ? "Permanent female tennis player" : "Permanent male tennis player";
            PrepareMaterials(model.gameObject, skin);
            var bones = model.GetComponentsInChildren<Transform>(true);
            root = Array.Find(bones, t => t.name == "Root");
            hand = Array.Find(bones, t => t.name == "Hand.R");
            offHand = Array.Find(bones, t => t.name == "Hand.L");
            chest = Array.Find(bones, t => t.name == "Chest");
            SweetSpot = Array.Find(bones, t => t.name == "TennisSweetSpot");
            basisOrigin = SweetSpot;
            // Use the equipment author's exact 0.427m string-center socket for contact.
            SweetSpot = Array.Find(bones, t => t.name.EndsWith("SweetSpot") && t.name != "TennisSweetSpot") ?? SweetSpot;
            stringRight = Array.Find(bones, t => t.name == "TennisStringRight");
            stringUp = Array.Find(bones, t => t.name == "TennisStringUp");
            stringNormal = Array.Find(bones, t => t.name == "TennisStringNormal");
            if (!SweetSpot) throw new InvalidOperationException("Tennis racket sweet-spot marker missing");
            racket = SweetSpot.parent;
            strokeTrail = SweetSpot.gameObject.AddComponent<TrailRenderer>();
            strokeTrail.time = .09f; strokeTrail.startWidth = .045f; strokeTrail.endWidth = .002f;
            strokeTrail.minVertexDistance = .015f;
            strokeTrail.sharedMaterial = strokeMaterial = new Material(Shader.Find("Sprites/Default"));
            strokeTrail.startColor = new Color(.25f,.92f,1,.7f); strokeTrail.endColor = new Color(.25f,.92f,1,0);
            strokeTrail.emitting = false;
            arms = model.GetComponent<StandardCharacterArms>() ?? model.gameObject.AddComponent<StandardCharacterArms>();
            arms.ManualEvaluation = true; arms.SetFloatingHandsPreview(true);
            var animator = model.GetComponent<Animator>() ?? model.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var loaded = Resources.LoadAll<AnimationClip>(path);
            graph = PlayableGraph.Create("Tennis standard character"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, loaded.Length);
            clips = new AnimationClipPlayable[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                indices[loaded[i].name] = i;
                clips[i] = AnimationClipPlayable.Create(graph, loaded[i]);
                clips[i].SetApplyFootIK(false); clips[i].SetSpeed(0);
                graph.Connect(clips[i], 0, mixer, i);
            }
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(mixer);
            graph.Play(); Tick(0, 0);
        }

        public void Swing(float power, bool useBackhand)
        {
            if (Swinging) return;
            SwingAge = 0; Power = Mathf.Clamp01(power); backhand = useBackhand;
            strokeTrail.Clear();
        }

        public void Tick(float dt, float speed)
        {
            if (!graph.IsValid()) return;
            Vector3 previousSweetSpot = SweetSpot.position;
            SwingAge += dt; locomotion += dt * Mathf.Clamp(Mathf.Abs(speed) / 2, .5f, 3.2f);
            model.localRotation = Quaternion.Euler(0,0,-Mathf.Clamp(speed / TennisRules.SprintSpeed,-1,1)*8);
            string pose = Swinging ? (backhand ? "Backhand" : "Forehand") : Mathf.Abs(speed) > .15f ? (speed < 0 ? "RunLeft" : "RunRight") : "Ready";
            if (!indices.TryGetValue(pose, out int selected)) throw new InvalidOperationException("Missing tennis clip " + pose);
            for (int i = 0; i < clips.Length; i++)
            {
                mixer.SetInputWeight(i, i == selected ? 1 : 0);
                double fraction = Swinging ? TennisRules.StrokePhase(SwingAge) : Mathf.Repeat(locomotion, 1);
                clips[i].SetTime(fraction * clips[i].GetAnimationClip().length);
            }
            graph.Evaluate(0);
            // Locomotion is authoritative in gameplay, not the preview clip's lateral root path.
            if (root)
            {
                Vector3 shift = model.TransformVector(new Vector3(root.localPosition.x, 0, root.localPosition.z));
                root.position -= shift; racket.position -= shift;
            }
            if (Swinging) ExaggerateStroke(TennisRules.StrokePhase(SwingAge));
            strokeTrail.emitting = Swinging && SwingAge > .07f && SwingAge < .30f;
            arms.ApplyAfterAnimation();
            SweetVelocity = dt > 0 ? (SweetSpot.position - previousSweetSpot) / dt : Vector3.zero;
        }

        void ExaggerateStroke(float phase)
        {
            // Wider anticipation and follow-through, with a fast, time-warped contact sweep.
            // Move the racket and gripping hand as one rigid group so the grip cannot separate.
            Vector3 handPosition = hand.position, racketPosition = racket.position, offPosition = offHand.position;
            Quaternion handRotation = hand.rotation, racketRotation = racket.rotation, offRotation = offHand.rotation;
            float envelope = Mathf.Sin(Mathf.PI * phase), side = backhand ? -1 : 1;
            float turn = side * Mathf.Sin(2*Mathf.PI*phase) * 35;
            Vector3 pivot = transform.position + Vector3.up * 1.05f;
            Quaternion sweep = Quaternion.AngleAxis(turn,Vector3.up);
            Vector3 outward = (sweep * (racketPosition-pivot)).normalized * (.22f * envelope);
            chest.rotation = Quaternion.AngleAxis(turn*.65f,Vector3.up) * chest.rotation;
            hand.SetPositionAndRotation(pivot+sweep*(handPosition-pivot)+outward,sweep*handRotation);
            racket.SetPositionAndRotation(pivot+sweep*(racketPosition-pivot)+outward,sweep*racketRotation);
            Vector3 balance = transform.TransformPoint(new Vector3(-side*.58f,1.15f,.22f));
            offHand.SetPositionAndRotation(Vector3.Lerp(offPosition,balance,envelope*.75f),offRotation);
        }

        public static void PrepareMaterials(GameObject obj, Color? skin = null)
        {
            var cache = new Dictionary<Material, Material>();
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var original = materials[i]; if (!original) continue;
                    if (!cache.TryGetValue(original, out var converted))
                    {
                        Color color = original.HasProperty("_Color") ? original.color : Color.white;
                        if (skin.HasValue && original.name.StartsWith("V4 skin")) color = skin.Value;
                        converted = GolfArcade.Course.HoleView.Mat(color); cache[original] = converted;
                    }
                    materials[i] = converted;
                }
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
            }
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); if (strokeMaterial) Destroy(strokeMaterial); }
    }
}
