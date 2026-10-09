using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Codex's cinematic swing, played as it was animated: Resources/Course/hole_NN_cinematic.fbx
    /// (from blender/scripts/hole12_cinematic_export.py) holds the presentation ball with its
    /// alignment stripe, the three tapered trail tubes with their per-frame morph targets, and the
    /// scene's reference empties, all in one baked Legacy clip. Parented under the placed course
    /// model with no transform of its own, it lands exactly on the course, and the game scrubs
    /// the clip to the same clock as the shot's cameras (SignatureShot). The trails' colour
    /// drivers were Blender-only, so the game picks the tube for the swing's rating itself.
    public sealed class CinematicRig
    {
        readonly GameObject root;
        readonly Animation animation;
        readonly AnimationState state;
        readonly Transform ball;
        readonly SkinnedMeshRenderer[] trails = new SkinnedMeshRenderer[3];   // green, yellow, red

        public Transform Ball => ball;
        /// The presentation ball's radius in scene units (metres) — the file's stripe torus sits on it.
        public const float BallRadius = 0.45f;

        CinematicRig(GameObject root, Animation animation, AnimationState state, Transform ball)
        {
            this.root = root; this.animation = animation; this.state = state; this.ball = ball;
        }

        /// The hole's cinematic, on its course model; null when the hole has none.
        public static CinematicRig Load(int holeNumber, HoleView view)
        {
            var prefab = Resources.Load<GameObject>($"Course/hole_{holeNumber:00}_cinematic");
            if (!prefab || !view || !view.ModelRoot) return null;
            // Both files left Blender the same way, so their roots carry the same import
            // transform (the importer's axis conversion), and the course model's root wears
            // HoleView's alignment on top of it. A carrier takes exactly that alignment — the
            // model root's placement with the shared import transform divided back out — and the
            // prefab keeps its own import transform under it, which is also what the clip keys.
            var import = Matrix4x4.TRS(prefab.transform.localPosition, prefab.transform.localRotation, prefab.transform.localScale);
            var alignment = view.ModelRoot.localToWorldMatrix * import.inverse;
            var carrier = new GameObject("Cinematic swing");
            carrier.transform.SetParent(view.ModelRoot.parent, false);
            carrier.transform.SetPositionAndRotation(alignment.GetColumn(3), alignment.rotation);
            carrier.transform.localScale = alignment.lossyScale;
            var root = Object.Instantiate(prefab, carrier.transform, false);
            var animation = root.GetComponent<Animation>();
            var clip = animation ? animation.clip : null;
            if (!clip) { Debug.LogWarning("Cinematic swing: the file imported without its clip"); Object.Destroy(root); return null; }
            var state = animation[clip.name];
            state.speed = 0; state.wrapMode = WrapMode.ClampForever;
            var ball = Find(root.transform, "BALL");
            if (!ball) { Object.Destroy(root); return null; }
            var rig = new CinematicRig(carrier, animation, state, ball);
            var spec = ShotEffects.TrailRecipe();
            string[] names = { "Trail_Green", "Trail_Yellow", "Trail_Red" };
            Color[] colors = { ShotEffects.PureColor, ShotEffects.FairColor, ShotEffects.OffLineColor };
            for (int i = 0; i < 3; i++)
            {
                var t = Find(root.transform, names[i]);
                if (!t) continue;
                rig.trails[i] = Skinned(t);
                rig.Dress(rig.trails[i], colors[i], spec.headAlpha, spec.tailAlpha);
            }
            // the ball and its stripe: Codex's white, a dark stripe, lit — as the FBX brought them
            foreach (var r in ball.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            foreach (var name in new[] { "TEE_WHITE", "CUP", "MARKER_UP", "LANDING_SAFE" })
            {
                var e = Find(root.transform, name);
                if (e) e.gameObject.SetActive(false);
            }
            rig.SetQuality(ShotEffects.Quality.Pure);
            rig.Show(false);
            return rig;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        /// The tubes need a SkinnedMeshRenderer for their morph weights; the importer gives an
        /// unskinned mesh a MeshRenderer.
        static SkinnedMeshRenderer Skinned(Transform t)
        {
            if (t.TryGetComponent(out SkinnedMeshRenderer smr)) return smr;
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>();
            var mesh = mf ? mf.sharedMesh : null; var mats = mr ? mr.sharedMaterials : System.Array.Empty<Material>();
            if (mr) Object.Destroy(mr);
            if (mf) Object.Destroy(mf);
            smr = t.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.sharedMaterials = mats;
            smr.updateWhenOffscreen = true;
            return smr;
        }

        /// Codex's bands: nineteen materials down the tube, alpha 0.04 + 0.38·t² from tail to
        /// head, in the swing's colour, unlit and see-through.
        void Dress(SkinnedMeshRenderer r, Color color, float headAlpha, float tailAlpha)
        {
            var mats = r.sharedMaterials;
            int n = Mathf.Max(1, mats.Length);
            for (int i = 0; i < mats.Length; i++)
            {
                float t = (i + 1f) / n;
                var m = new Material(ShotEffects.ParticleMaterial()) { mainTexture = Texture2D.whiteTexture };
                var c = Color.Lerp(color, Color.white, 0.15f * t);
                c.a = Mathf.Lerp(tailAlpha, headAlpha, t * t);
                m.color = c;
                mats[i] = m;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public void Show(bool on) => root.SetActive(on);

        public void SetQuality(ShotEffects.Quality q)
        {
            int keep = q == ShotEffects.Quality.Pure ? 0 : q == ShotEffects.Quality.Fair ? 1 : 2;
            for (int i = 0; i < 3; i++) if (trails[i]) trails[i].gameObject.SetActive(i == keep);
        }

        /// The baked tubes are at scene scale — right up close, a hair from the air — so a wide
        /// view draws the live tube round this ball instead and puts these away.
        public void ShowTrails(bool on)
        {
            for (int i = 0; i < 3; i++) if (trails[i]) trails[i].gameObject.SetActive(on && trails[i].gameObject.activeSelf);
        }

        /// Put the clip at `seconds` from its first frame.
        public void Sample(float seconds)
        {
            state.enabled = true; state.weight = 1;
            state.time = Mathf.Clamp(seconds, 0, state.length);
            animation.Sample();
            state.enabled = false;
        }

        public void Destroy() { if (root) Object.Destroy(root); }
    }
}
