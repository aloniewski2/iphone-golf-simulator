using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GolfArcade.Tennis
{
    /// Seated spectators in the stands: the fitted Higgsfield characters (light crowd builds
    /// from blender/scripts/build_crowd.py), sitting in the seats the arena's placeholder blob
    /// spectators used to fill, idling on mocap and rising into a seated cheer on big points.
    public sealed class TennisStandsCrowd : MonoBehaviour
    {
        /// Seats measured from the arena (three tiers along the west stand), in world space:
        /// x, seat height, z. The tiers step 0.65 m up and 1.2 m back.
        static readonly float[] TierX = { -13.8f, -15.0f, -16.2f }, TierY = { .63f, 1.28f, 1.93f };
        const float FirstZ = -9.0f, Spacing = 2.2f; const int PerRow = 12;
        /// Hips-to-backside offset of the crowd rig when sitting (crowd-clips.json).
        const float SeatOffset = .406f;

        sealed class Fan { public PlayableGraph graph; public AnimationMixerPlayable mixer; public float cheerAt = -9, cheerLength; public float phase; }
        readonly List<Fan> fans = new();
        float cheerLength = 2.7f;

        public void Build(Transform parent)
        {
            var prefabs = new[] { Resources.Load<GameObject>("Tennis/Crowd/crowd_male"), Resources.Load<GameObject>("Tennis/Crowd/crowd_female") };
            if (!prefabs[0] || !prefabs[1]) { Debug.LogWarning("[Crowd] seated crowd missing"); return; }
            var skins = new[] { new Color(.95f, .65f, .35f), new Color(.88f, .58f, .30f) };
            int i = 0;
            for (int tier = 0; tier < TierX.Length; tier++)
                for (int s = 0; s < PerRow; s++, i++)
                {
                    int g = (s + tier) % 2;
                    var fan = Instantiate(prefabs[g], parent);
                    fan.name = "Seated fan " + i;
                    var seat = new Vector3(TierX[tier], TierY[tier], FirstZ + s * Spacing);
                    fan.transform.SetPositionAndRotation(seat - Vector3.up * SeatOffset, Quaternion.Euler(0, 90, 0));
                    TennisLook.PrepareCharacter(fan, skins[g]);
                    foreach (var r in fan.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        r.quality = SkinQuality.Bone2; r.updateWhenOffscreen = false;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    // (Unity's missing-component object is not C# null, so no `??` here.)
                    var animator = fan.GetComponent<Animator>();
                    if (!animator) animator = fan.AddComponent<Animator>();
                    animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    var clips = Resources.LoadAll<AnimationClip>("Tennis/Crowd/crowd_" + (g == 0 ? "male" : "female"));
                    AnimationClip idle = System.Array.Find(clips, c => c.name == "SitIdle"), cheer = System.Array.Find(clips, c => c.name == "SitCheer");
                    if (!idle) continue;
                    var f = new Fan { graph = PlayableGraph.Create("Seated fan"), phase = (i * .37f) % 1f };
                    f.graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                    f.mixer = AnimationMixerPlayable.Create(f.graph, 2);
                    var a = AnimationClipPlayable.Create(f.graph, idle); a.SetTime(f.phase * idle.length);
                    f.graph.Connect(a, 0, f.mixer, 0); f.mixer.SetInputWeight(0, 1);
                    if (cheer) { var b = AnimationClipPlayable.Create(f.graph, cheer); f.graph.Connect(b, 0, f.mixer, 1); cheerLength = cheer.length; }
                    AnimationPlayableOutput.Create(f.graph, "Fan", animator).SetSourcePlayable(f.mixer);
                    f.graph.Play();
                    fans.Add(f);
                }
        }

        /// Big point: a share of the stand, growing with `strength`, rises into the cheer.
        public void Cheer(float strength)
        {
            for (int i = 0; i < fans.Count; i++)
            {
                var f = fans[i];
                if (((i * 7919) % 100) / 100f > .25f + .7f * strength) continue;
                f.cheerAt = Time.time + ((i * 13) % 7) * .06f;   // a ripple, not a single jolt
                if (f.mixer.GetInputCount() > 1) f.mixer.GetInput(1).SetTime(0);
            }
        }

        void Update()
        {
            foreach (var f in fans)
            {
                if (f.mixer.GetInputCount() < 2) continue;
                float t = Time.time - f.cheerAt;
                float w = t < 0 || t > cheerLength ? 0 : Mathf.Clamp01(Mathf.Min(t / .25f, (cheerLength - t) / .4f));
                if (t >= 0 && t < .05f) f.mixer.GetInput(1).SetTime(t);
                f.mixer.SetInputWeight(0, 1 - w); f.mixer.SetInputWeight(1, w);
            }
        }

        void OnDestroy() { foreach (var f in fans) if (f.graph.IsValid()) f.graph.Destroy(); }
    }
}
