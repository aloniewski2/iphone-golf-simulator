using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using GolfArcade.Course;

namespace GolfArcade.Game
{
    /// The golfer on screen. With the permanent V4 model in Resources/StandardCharacters (with a
    /// full driver swing baked as one clip) the phone drives the clip: the backswing is scrubbed
    /// by the detector's load, so the figure winds up exactly as far as the player does, and on
    /// impact the downswing runs through to the finish at real speed. Without the model a
    /// Mii-simple figure of primitives stands in.
    public sealed class GolferView : MonoBehaviour
    {
        // Permanent V4 drive: source frames 1..91 at 30 fps, top 59%, contact 80%.
        const float TopTime = 1.77f, ImpactTime = 2.4f, EndTime = 3f;
        /// A full downswing, top to ball (matches the clip); a partial backswing comes down proportionally faster.
        const float FullDownswing = ImpactTime - TopTime;
        const float MetresToYards = 1.0936f;

        // rigged model
        PlayableGraph graph;
        AnimationClipPlayable clip;
        bool hasModel;
        float time;            // clip time being shown
        float loadTarget;      // where the backswing should be, from the phone
        int phase;             // 0 posing with the load, 1 unwinding a partial backswing, 2 swinging through, 3 holding the finish
        float unwindSpeed;

        // primitive fallback
        Transform pivot, club, body;
        float shownLoad;
        float swingThrough = -1;
        const float rest = 20f;

        /// Blender material name → flat game colour, matching the character sheet.
        static readonly Dictionary<string, Color> Palette = new()
        {
            ["MAT_SKIN"] = Rgb(226, 160, 110), ["MAT_SHIRT"] = Rgb(38, 84, 176), ["MAT_TROUSERS"] = Rgb(214, 196, 160),
            ["MAT_BELT"] = Rgb(34, 40, 62), ["MAT_EYE"] = Rgb(28, 24, 22), ["MAT_LOGO"] = Rgb(38, 84, 176),
            ["MAT_CAP"] = Rgb(245, 245, 245), ["MAT_GLOVE"] = Rgb(240, 240, 240), ["MAT_SHOE"] = Rgb(240, 240, 240),
            ["MAT_SHOE_SOLE"] = Rgb(40, 40, 44), ["MAT_SHAFT"] = Rgb(190, 192, 198), ["MAT_CLUBHEAD"] = Rgb(40, 42, 48),
            ["MAT_GRIP"] = Rgb(30, 30, 34), ["MAT_HAIR"] = Rgb(70, 48, 30), ["MAT_LIPS"] = Rgb(196, 84, 90), ["MAT_SHIRT_DARK"] = Rgb(28, 62, 136),
        };
        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        GameObject modelGo;
        StandardCharacterArms standardArms;
        StandardGolfGrip standardGrip;
        public bool UsesStandardCharacter => hasModel;
        public bool FloatingHandsPreview { get; private set; } = true;

        public void SetFloatingHandsPreview(bool enabled)
        {
            FloatingHandsPreview = enabled;
            standardArms?.SetFloatingHandsPreview(enabled);
        }

        public static GolferView Create(Transform parent)
        {
            var go = new GameObject("Golfer");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<GolferView>();
            v.ApplyStyle();
            return v;
        }

        /// (Re)build the figure from GolferStyle: body model and skin tone. Comes up at address.
        public void ApplyStyle()
        {
            if (graph.IsValid()) graph.Destroy();
            if (modelGo) Destroy(modelGo);
            if (body) Destroy(body.gameObject);
            hasModel = false; body = null; modelGo = null;
            phase = 0; loadTarget = 0; time = 0; swingThrough = -1; shownLoad = 0;
            var model = Resources.Load<GameObject>(GolferStyle.ModelPath);
            if (model && !BuildModel(model, GolferStyle.ModelPath)) Debug.LogWarning($"{GolferStyle.ModelPath} has no Swing clip; using the primitive golfer");
            if (!hasModel) BuildFigure();
        }

        // ----- Rigged model -----

        bool BuildModel(GameObject prefab, string path)
        {
            AnimationClip swing = null;
            foreach (var c in Resources.LoadAll<AnimationClip>(path))
                if (c.name == "StandardGolfDrive") { swing = c; break; }
            if (!swing) return false;

            var model = Instantiate(prefab, transform);
            modelGo = model;
            model.name = "Permanent standard golfer";
            model.transform.localPosition = Vector3.zero;
            // V4 golf authoring includes a 90-degree stance rotation inside the root pose.
            model.transform.localRotation = Quaternion.Euler(0, -90, 0);
            model.transform.localScale = Vector3.one * MetresToYards; // the FBX is in metres, the course in yards
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    string name = mats[i].name.Replace(" (Instance)", "");
                    if (name == "MAT_SKIN" || name.StartsWith("V4 skin")) mats[i] = HoleView.Mat(GolferStyle.SkinColor);
                    else if (name.StartsWith("V4 "))
                    {
                        // Imported FBX carries the authored color; use the game's supported shader.
                        var importedColor = mats[i].HasProperty("_Color") ? mats[i].color : Color.white;
                        mats[i] = HoleView.Mat(importedColor);
                    }
                    else if (Palette.TryGetValue(name, out var color)) mats[i] = HoleView.Mat(color);
                }
                r.sharedMaterials = mats;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            standardArms = model.GetComponent<StandardCharacterArms>() ?? model.AddComponent<StandardCharacterArms>();
            standardArms.ManualEvaluation = true;
            standardArms.SetFloatingHandsPreview(FloatingHandsPreview);
            standardGrip = model.AddComponent<StandardGolfGrip>();
            standardGrip.Initialize();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("Golfer swing");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "Swing", animator);
            clip = AnimationClipPlayable.Create(graph, swing);
            clip.SetApplyFootIK(false);
            clip.SetApplyPlayableIK(false);
            output.SetSourcePlayable(clip);
            graph.Play();
            hasModel = true;
            // Align the authored club-head contact to the ball while preserving the game's stance.
            Show(ImpactTime);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "StandardClubContact")
                {
                    model.transform.localPosition += new Vector3(0, 0, .75f) - transform.InverseTransformPoint(t.position);
                    break;
                }
            Show(0);
            return true;
        }

        void Show(float t)
        {
            time = Mathf.Clamp(t, 0, EndTime);
            clip.SetTime(time);
            graph.Evaluate();
            standardGrip?.Apply(time / EndTime);
            standardArms?.ApplyAfterAnimation();
        }

        void OnDestroy() { if (graph.IsValid()) graph.Destroy(); }

        // ----- Shared API -----

        /// Stand beside the ball, facing across the aim line (a right-hander stands to the
        /// ball's left as seen from behind).
        public void Stand(Vector3 ball, Vector3 aimDirection)
        {
            var side = Vector3.Cross(Vector3.up, aimDirection).normalized; // right of the line
            transform.position = ball - side * 0.75f;
            transform.rotation = Quaternion.LookRotation(side, Vector3.up);
        }

        public void SetVisible(bool on) => gameObject.SetActive(on);

        /// The backswing follows the phone: 0 is address, 1 the top.
        public void ShowLoad(float load)
        {
            shownLoad = load;
            if (phase == 0 || phase == 3) { phase = 0; loadTarget = Mathf.Clamp01(load); }
        }

        /// Swing through from wherever the backswing got to. Returns the seconds until the club
        /// reaches the ball, so the ball can leave exactly then.
        public float Strike()
        {
            swingThrough = 0;
            if (!hasModel) return 0.12f;
            if (time >= TopTime * 0.85f)
            {
                // a full swing: the clip's own downswing, hips first, club lagging
                phase = 2;
                return Mathf.Max(0.05f, ImpactTime - time);
            }
            // a shorter swing comes back down the way it went up, then releases through the ball
            phase = 1;
            float down = Mathf.Max(0.12f, FullDownswing * (time / TopTime));
            unwindSpeed = time / down;
            return down;
        }

        public void Settle()
        {
            swingThrough = -1; shownLoad = 0;
            phase = 0; loadTarget = 0;
            if (hasModel) Show(0);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.T)) UnityEngine.SceneManagement.SceneManager.LoadScene("Tennis");
            if (hasModel) UpdateModel(); else UpdateFigure();
        }

        void UpdateModel()
        {
            float dt = Time.deltaTime;
            switch (phase)
            {
                case 0:
                    // ease toward the phone's load; quick enough to feel live, smooth enough not to jitter
                    Show(Mathf.MoveTowards(time, loadTarget * TopTime, dt * TopTime * 6f));
                    break;
                case 1:
                    Show(time - unwindSpeed * dt);
                    if (time <= 0.001f) { phase = 2; Show(ImpactTime); }
                    break;
                case 2:
                    Show(time + dt);
                    if (time >= EndTime) phase = 3;
                    break;
            }
        }

        // ----- Primitive fallback -----

        void BuildFigure()
        {
            var skin = GolferStyle.SkinColor;
            var shirt = new Color(0.2f, 0.45f, 0.85f);
            var trousers = new Color(0.25f, 0.25f, 0.3f);

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            var legs = HoleView.Primitive(PrimitiveType.Capsule, "Legs", trousers, body);
            legs.transform.localPosition = new Vector3(0, 0.45f, 0);
            legs.transform.localScale = new Vector3(0.35f, 0.45f, 0.3f);
            var torso = HoleView.Primitive(PrimitiveType.Capsule, "Torso", shirt, body);
            torso.transform.localPosition = new Vector3(0, 1.05f, 0);
            torso.transform.localScale = new Vector3(0.45f, 0.35f, 0.32f);
            var head = HoleView.Primitive(PrimitiveType.Sphere, "Head", skin, body);
            head.transform.localPosition = new Vector3(0, 1.62f, 0);
            head.transform.localScale = Vector3.one * 0.34f;
            var cap = HoleView.Primitive(PrimitiveType.Cylinder, "Cap", Color.white, body);
            cap.transform.localPosition = new Vector3(0, 1.78f, 0.02f);
            cap.transform.localScale = new Vector3(0.36f, 0.04f, 0.36f);

            pivot = new GameObject("Shoulders").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(0, 1.3f, 0);

            var arms = HoleView.Primitive(PrimitiveType.Capsule, "Arms", skin, pivot);
            arms.transform.localPosition = new Vector3(0, -0.35f, 0.15f);
            arms.transform.localScale = new Vector3(0.12f, 0.35f, 0.12f);

            club = new GameObject("Club").transform;
            club.SetParent(pivot, false);
            var shaft = HoleView.Primitive(PrimitiveType.Cylinder, "Shaft", new Color(0.75f, 0.75f, 0.78f), club);
            shaft.transform.localPosition = new Vector3(0, -0.85f, 0.2f);
            shaft.transform.localScale = new Vector3(0.03f, 0.55f, 0.03f);
            var headGo = HoleView.Primitive(PrimitiveType.Cube, "Clubhead", new Color(0.3f, 0.3f, 0.32f), club);
            headGo.transform.localPosition = new Vector3(0.06f, -1.38f, 0.2f);
            headGo.transform.localScale = new Vector3(0.16f, 0.07f, 0.09f);
        }

        void UpdateFigure()
        {
            float angle;
            if (swingThrough >= 0)
            {
                swingThrough += Time.deltaTime;
                float t = Mathf.Clamp01(swingThrough / 0.35f);
                float eased = 1 - Mathf.Pow(1 - t, 3);
                angle = Mathf.Lerp(-(rest + shownLoad * 150f), 170f, eased);
                if (t >= 1 && swingThrough > 1.2f) { swingThrough = -1; shownLoad = 0; }
            }
            else
            {
                angle = -(rest + shownLoad * 150f);
            }
            pivot.localRotation = Quaternion.AngleAxis(-angle + rest, Vector3.forward) * Quaternion.AngleAxis(25f, Vector3.right);
            float turn = swingThrough >= 0 ? Mathf.Lerp(-shownLoad * 30f, 40f, Mathf.Clamp01(swingThrough / 0.35f)) : -shownLoad * 30f;
            body.localRotation = Quaternion.AngleAxis(turn, Vector3.up);
        }
    }
}
