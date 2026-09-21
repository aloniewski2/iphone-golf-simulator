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
            SweetSpot = Array.Find(bones, t => t.name == "TennisSweetSpot");
            basisOrigin = SweetSpot;
            // Use the equipment author's exact 0.427m string-center socket for contact.
            SweetSpot = Array.Find(bones, t => t.name.EndsWith("SweetSpot") && t.name != "TennisSweetSpot") ?? SweetSpot;
            stringRight = Array.Find(bones, t => t.name == "TennisStringRight");
            stringUp = Array.Find(bones, t => t.name == "TennisStringUp");
            stringNormal = Array.Find(bones, t => t.name == "TennisStringNormal");
            if (!SweetSpot) throw new InvalidOperationException("Tennis racket sweet-spot marker missing");
            racket = SweetSpot.parent;
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
        }

        public void Tick(float dt, float speed)
        {
            if (!graph.IsValid()) return;
            SwingAge += dt; locomotion += dt * Mathf.Max(.5f, Mathf.Abs(speed) / 2);
            string pose = Swinging ? (backhand ? "Backhand" : "Forehand") : Mathf.Abs(speed) > .15f ? (speed < 0 ? "RunLeft" : "RunRight") : "Ready";
            if (!indices.TryGetValue(pose, out int selected)) throw new InvalidOperationException("Missing tennis clip " + pose);
            for (int i = 0; i < clips.Length; i++)
            {
                mixer.SetInputWeight(i, i == selected ? 1 : 0);
                double fraction = Swinging ? Mathf.Clamp01(SwingAge / TennisRules.StrokeDuration) : Mathf.Repeat(locomotion, 1);
                clips[i].SetTime(fraction * clips[i].GetAnimationClip().length);
            }
            graph.Evaluate(0);
            // Locomotion is authoritative in gameplay, not the preview clip's lateral root path.
            if (root)
            {
                Vector3 shift = model.TransformVector(new Vector3(root.localPosition.x, 0, root.localPosition.z));
                root.position -= shift; racket.position -= shift;
            }
            arms.ApplyAfterAnimation();
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

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }
    }
}
