using System;
using System.Collections.Generic;
using GolfArcade.Game;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GolfArcade.Tennis
{
    /// One tennis player: the authored clip library, blended in layers, with the procedural
    /// work the clips cannot carry layered on top (body turn, trunk rotation, head tracking,
    /// planted feet, two-handed backhand, arms).
    ///
    /// Layers, from the bottom:
    ///   locomotion  Ready / RunLeft / RunRight, weighted continuously by speed
    ///   one-shot    brake, direction change, split step, recovery, ground recovery
    ///   action      the stroke, including its preparation before the swing is triggered
    /// Each layer's weight is a smoothed scalar, so nothing ever cuts from one pose to another.
    public sealed class TennisActor : MonoBehaviour
    {
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] clips;
        float[] clipLength;
        readonly List<int> weighted = new();
        readonly Dictionary<string, int> indices = new();
        readonly Dictionary<string, int> resolved = new();
        readonly Dictionary<int, float> contacts = new();
        readonly HashSet<string> missingClips = new();
        Transform model, root, racket;
        Transform racketVisual;
        float racketScale = 1;
        Vector3 modelRest;
        float hop;
        Vector3 gripLocalOffset;
        Vector3 GripPosition => hand.TransformPoint(gripLocalOffset);
        public float RacketGripError => racketVisual ? Vector3.Distance(GripPosition, racketVisual.TransformPoint(new Vector3(0, TennisKitV3.GripInset, 0))) : float.PositiveInfinity;
        StandardCharacterArms arms;
        Transform hand, offHand, chest, hips, neck, head;
        // The tossing arm (the hand without the racket) for the serve routine, and where on
        // it the palm is.
        Transform tossUpper, tossLower, tossHand;
        Vector3 tossPalmLocal;
        Vector3 reachTarget; float reachWanted, reachWeight;
        /// Straightens the spine toward upright (used while being introduced to camera).
        public float Upright;
        // Contact guidance (see GuideContact): the racket arm, where each stroke clip holds
        // the strings at its authored contact frame, and the current steer.
        Transform armUpper, armLower;
        readonly Dictionary<int, Vector3> contactPose = new();
        Vector3 guideBall, guideOffset, guideBody;
        float guideClock, guideLead, guideWeight, swingPace = 1;
        bool guiding, guideAuthored, guideFrozen;
        float bodyYaw;
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
        float swingDuration = TennisRules.StrokeDuration;
        /// Fixed when the swing starts. A swing can now start on the phone's first sign of a
        /// stroke, before its power is known; changing the duration when power arrives would
        /// make the clip jump.
        public float SwingDuration => swingDuration;
        public float ContactAge => SwingAge * TennisRules.StrokeDuration / swingDuration;
        public bool Swinging => SwingAge < swingDuration;
        public bool Overhead { get; private set; }
        /// A swing started from the onset of phone motion, not yet confirmed as a real stroke.
        /// It animates but cannot strike the ball until confirmed.
        public bool Provisional { get; private set; }
        public bool LeftHanded { get; private set; }
        /// True for the Higgsfield bodies: one skinned mesh, no procedural arms or runtime kit.
        public bool SkinnedBody { get; private set; }
        /// Ground speed toward the net (see Advance).
        public float ForwardSpeed { get; private set; }
        /// Getting up off the court after a dive; the game slows movement meanwhile.
        public bool GroundRecovering => oneShotIndex >= 0 && oneShotClip == "GroundRecovery" && oneShotAge < oneShotDuration;
        /// Which authored animation a stroke should play. The library carries a distinct clip
        /// for each of these in both handednesses, so none of them has to be faked.
        public enum Stroke { Drive, Volley, Lob, Smash, Serve, Running, LowPickup, Dive, Missed, Slice, Topspin, Celebrate }

        /// Strokes whose clip carries its own vertical root motion. Unity now imports that
        /// height, so adding the procedural hop on top would jump twice.
        static bool ClipLeavesGround(Stroke kind) =>
            kind == Stroke.Serve || kind == Stroke.Smash || kind == Stroke.Volley
            || kind == Stroke.Running || kind == Stroke.LowPickup || kind == Stroke.Dive;
        public Stroke Kind { get; private set; } = Stroke.Drive;
        public string StrokeLabel => Overhead ? "Overhead smash" : (Power < .4f ? (backhand ? "Soft backhand" : "Soft forehand") : (backhand ? "backhand" : "forehand"));
        bool backhand, serving;
        float locomotion, idlePhase, previousSpeed, turnCooldown;
        public float Speed { get; private set; }
        /// Speed as the body shows it: lean, crouch and trunk work follow this, eased over a
        /// tenth of a second, so a sudden stop does not snap the torso upright in one frame.
        float postureSpeed;

        /// How a particular player moves. The two players used to be identical copies; the
        /// opponent now bounces on its toes between shots, turns and swings its arms harder,
        /// and holds a lower, wider waiting stance.
        public struct Style
        {
            public float Trunk, Arms, Yaw, IdleBounce, Look, Lean;
            public static Style Player => new Style { Trunk = 1, Arms = 1, Yaw = 1, IdleBounce = .35f, Look = .85f, Lean = 1 };
            public static Style Rival => new Style { Trunk = 1.35f, Arms = 1.3f, Yaw = 1.12f, IdleBounce = 1f, Look = .9f, Lean = 1.4f };
        }
        public Style Motion = Style.Player;
        /// A foot has just been planted by the gait, at this spot and body speed (for dust).
        public Action<Vector3, float> FootPlanted;
        /// A dive has just hit the ground.
        public Action<Vector3> DiveLanded;

        /// Cells of the face atlas (blender/scripts/build_face_atlas.py).
        public enum Expression { Neutral, Blink, Happy, Focus, Effort, Sad, Surprised, Cheer }
        public Expression Face { get; private set; }
        Material faceMaterial;
        Expression heldExpression; float heldFor, faceClock, nextBlink = 2f, blinkFor;
        static readonly int CellId = Shader.PropertyToID("_Cell");

        /// Show an expression for a while, over whatever the body is doing.
        public void SetExpression(Expression expression, float seconds) { heldExpression = expression; heldFor = seconds; }

        void UpdateFace(float dt)
        {
            if (!faceMaterial) return;
            faceClock += dt; heldFor -= dt; blinkFor -= dt;
            Expression wanted;
            if (heldFor > 0) wanted = heldExpression;
            else if (Swinging && Kind != Stroke.Celebrate) wanted = Power > .7f ? Expression.Effort : Expression.Focus;
            else if (prepare > .45f) wanted = Expression.Focus;
            else wanted = Expression.Neutral;
            // Blink every few seconds, irregularly, whenever the eyes are open and relaxed.
            if (faceClock >= nextBlink) { blinkFor = .11f; nextBlink = faceClock + 2.2f + Mathf.Repeat(faceClock * 7.3f, 3.1f); }
            if (blinkFor > 0 && (wanted == Expression.Neutral || wanted == Expression.Focus)) wanted = Expression.Blink;
            if (wanted == Face) return;
            Face = wanted;
            faceMaterial.SetFloat(CellId, (int)wanted);
        }

        // --- Layer state -----------------------------------------------------------------
        const float StrokeFadeIn = .05f, StrokeFadeOut = .16f, CancelFade = .10f, SwitchFade = .06f;
        const float LocomotionFade = .14f, PrepareFade = .20f;
        int actionIndex = -1, actionOutIndex = -1;
        float actionPhase, actionWeight, actionOutPhase, actionOutWeight;
        bool cancelled;
        float prepare, prepareTarget; bool prepareBackhand, prepareServe;
        float readyWeight = 1;
        int oneShotIndex = -1; string oneShotClip; float oneShotAge, oneShotDuration;
        float dejected; // procedural lost-point reaction, 1 -> 0
        Vector3 lookTarget; float lookWeight; Vector3 lookDirection; bool lookInitialised;
        Vector3 headLocalForward, chestLocalForward;
        float pendingDt;
        Vector3 lastSweetSpot;

        sealed class Leg
        {
            public Transform upper, lower, foot;
            public float upperLength, lowerLength, restHeight;
            public bool planted; public Vector3 lockPosition; public float lockWeight, stepLift;
            // Procedural gait: where the foot rests relative to the body, its half of the
            // cycle, and the current step.
            public Vector3 restOffset; public float phaseOffset;
            public bool swinging, gaitReady; public Vector3 plant, liftFrom, landAt;
            public float swingT, swingDuration, halfStance, stanceTime, lastPhase;
        }
        Leg[] legs;
        float legLength = .8f, gaitPhase, gaitWeight;
        /// A planted foot is released once the animation wants it further than this away.
        const float FootLockRange = .18f;

        public void Build(bool female, Color skin) => Build(female, skin, NativeSportsSession.Left);

        public void Build(bool female, Color skin, bool leftHanded)
        {
            LeftHanded = leftHanded;
            string path = "StandardCharacters/standard_" + (female ? "female" : "male") + "_tennis";
            var prefab = Resources.Load<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Missing permanent tennis character: " + path);
            model = Instantiate(prefab, transform).transform;
            // Left-handers get genuine left-handed clips, so the old negative-X mirror (which
            // also flipped text, kit detail and the grip) is no longer needed.
            model.name = female ? "Permanent female tennis player" : "Permanent male tennis player";
            modelRest = model.localPosition;
            // Higgsfield bodies are one skinned mesh with their clothes, arms and skin baked in;
            // the procedural arm tubes and the runtime kit are for the older piecewise bodies.
            SkinnedBody = Array.Exists(model.GetComponentsInChildren<SkinnedMeshRenderer>(true), r => r.name.StartsWith("V4 Higgs body"));
            // The separate grip hands take the body's own skin tone (sampled from its texture).
            if (SkinnedBody) skin = female ? new Color(.88f, .58f, .30f) : new Color(.95f, .62f, .26f);
            PrepareMaterials(model.gameObject, skin);
            var bones = model.GetComponentsInChildren<Transform>(true);
            Transform Bone(string n) => Array.Find(bones, t => t.name == n);
            root = Bone("Root");
            hand = Bone("Hand.R");
            offHand = Bone("Hand.L");
            string toss = leftHanded ? "R" : "L";
            tossUpper = Bone("UpperArm." + toss); tossLower = Bone("LowerArm." + toss); tossHand = Bone("Hand." + toss);
            chest = Bone("Chest");
            hips = Bone("Hips");
            neck = Bone("Neck");
            head = Bone("Head");
            SweetSpot = Bone("TennisSweetSpot");
            basisOrigin = SweetSpot;
            // Use the equipment author's exact 0.427m string-center socket for contact.
            SweetSpot = Array.Find(bones, t => t.name.EndsWith("SweetSpot") && t.name != "TennisSweetSpot") ?? SweetSpot;
            stringRight = Bone("TennisStringRight");
            stringUp = Bone("TennisStringUp");
            stringNormal = Bone("TennisStringNormal");
            if (!SweetSpot) throw new InvalidOperationException("Tennis racket sweet-spot marker missing");
            racket = SweetSpot.parent;
            strokeTrail = SweetSpot.gameObject.AddComponent<TrailRenderer>();
            strokeTrail.time = .13f; strokeTrail.startWidth = .22f; strokeTrail.endWidth = .01f;
            strokeTrail.minVertexDistance = .012f;
            // A swoosh the width of the racket head, glowing, fading from the strings back.
            var glow = Resources.Load<Shader>("Tennis/Shaders/TennisFxAdditive");
            strokeTrail.sharedMaterial = strokeMaterial = new Material(glow ? glow : Shader.Find("Sprites/Default")) { mainTexture = TennisLook.Falloff };
            strokeTrail.textureMode = LineTextureMode.Stretch;
            strokeTrail.startColor = new Color(.55f, .95f, 1, .55f); strokeTrail.endColor = new Color(.25f, .75f, 1, 0);
            strokeTrail.emitting = false;
            strokeTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!SkinnedBody)
            {
                arms = model.GetComponent<StandardCharacterArms>() ?? model.gameObject.AddComponent<StandardCharacterArms>();
                arms.ManualEvaluation = true;
                // Floating hands hide the arm meshes entirely, which is the single biggest reason
                // the characters read as toy-like.
                arms.SetFloatingHandsPreview(false);
            }
            var animator = model.GetComponent<Animator>() ?? model.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var loaded = Resources.LoadAll<AnimationClip>(path);
            graph = PlayableGraph.Create("Tennis standard character"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, loaded.Length);
            clips = new AnimationClipPlayable[loaded.Length];
            clipLength = new float[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                indices[loaded[i].name] = i;
                clipLength[i] = Mathf.Max(.01f, loaded[i].length);
                clips[i] = AnimationClipPlayable.Create(graph, loaded[i]);
                clips[i].SetApplyFootIK(false); clips[i].SetSpeed(0);
                graph.Connect(clips[i], 0, mixer, i);
            }
            LoadContacts();
            AnimationPlayableOutput.Create(graph, "Pose", animator).SetSourcePlayable(mixer);
            graph.Play();
            // Measure the rig in its rest stance before any procedural layer touches it.
            SetWeight(Resolve("Ready"), 1, 0); graph.Evaluate(0);
            if (head) headLocalForward = head.InverseTransformDirection(model.forward);
            if (chest) chestLocalForward = chest.InverseTransformDirection(model.forward);
            legs = BuildLegs(bones);
            var faceRenderer = Array.Find(model.GetComponentsInChildren<Renderer>(true), r => r.name.StartsWith("V4 face decal"));
            if (faceRenderer) { faceMaterial = faceRenderer.sharedMaterial; faceMaterial.SetFloat(CellId, 0); }
            Tick(0, 0);
            // The authored floating-hand mesh is offset from the wrist bone's origin.
            // Capture its actual palm centre once; it is rigidly weighted to this bone.
            var gripRenderer = Array.Find(model.GetComponentsInChildren<SkinnedMeshRenderer>(), r => r.name.StartsWith("V4 grip hand R"));
            if (!gripRenderer) throw new InvalidOperationException("Permanent gripping hand mesh missing");
            var gripMesh = new Mesh(); gripRenderer.BakeMesh(gripMesh);
            gripLocalOffset = hand.InverseTransformPoint(gripRenderer.transform.TransformPoint(gripMesh.bounds.center));
            Destroy(gripMesh);
            if (!SkinnedBody) TennisKitV3.Dress(model, female);
            var tossRenderer = Array.Find(model.GetComponentsInChildren<SkinnedMeshRenderer>(), r => r.name.StartsWith("V4 grip hand " + toss));
            if (tossRenderer && tossHand)
            {
                var tossMesh = new Mesh(); tossRenderer.BakeMesh(tossMesh);
                tossPalmLocal = tossHand.InverseTransformPoint(tossRenderer.transform.TransformPoint(tossMesh.bounds.center));
                Destroy(tossMesh);
            }
            racketVisual = TennisKitV3.Equip(model, racket, SweetSpot.position, StringUp, StringNormal, out racketScale);
            SweetSpot = new GameObject("Tripo string-bed contact").transform;
            SweetSpot.SetParent(racketVisual, false); SweetSpot.localPosition = new Vector3(0, TennisKitV3.HeadCentre, 0);
            AlignRacket();
            armLower = hand ? hand.parent : null; armUpper = armLower ? armLower.parent : null;
            SampleContactPoses();
            lastSweetSpot = SweetSpot.position;
        }

        /// Where the strings sit, in the body's own space, at each stroke clip's authored
        /// contact frame. Contact guidance aims the whole swing from this, so the arc keeps its
        /// shape and arrives on the ball, instead of the racket being dragged toward it.
        void SampleContactPoses()
        {
            string suffix = LeftHanded ? "_LH" : "_RH";
            foreach (var kind in (Stroke[])Enum.GetValues(typeof(Stroke)))
                foreach (bool useBackhand in new[] { false, true })
                {
                    string clip = StrokeClip(kind, useBackhand);
                    if (!indices.ContainsKey(clip + suffix) && !indices.ContainsKey(clip + "_RH") && !indices.ContainsKey(clip)) continue;
                    int index = Resolve(clip);
                    if (index < 0 || contactPose.ContainsKey(index)) continue;
                    foreach (int i in weighted) mixer.SetInputWeight(i, 0);
                    weighted.Clear();
                    SetWeight(index, 1, ContactOf(index));
                    graph.Evaluate(0);
                    StripRootTravel();
                    model.localRotation = Quaternion.identity; model.localPosition = modelRest;
                    AlignRacket();
                    contactPose[index] = model.InverseTransformPoint(SweetSpot.position);
                    if (TennisGame.LogContactGaps) Debug.Log($"[ContactPose] {name} {clip} bh={useBackhand} {contactPose[index]}");
                }
            foreach (int i in weighted) mixer.SetInputWeight(i, 0);
            weighted.Clear();
            Tick(0, 0);
        }

        /// Time until the swing reaches its authored contact frame (0 once past it).
        public float TimeToContact => Swinging ? Mathf.Max(0, TennisRules.SweetTime - ContactAge) * swingDuration / TennisRules.StrokeDuration : 0;

        /// Where the strings would be at the authored contact frame if the body stayed where
        /// it is now.
        public Vector3 AuthoredContact
        {
            get
            {
                int index = Swinging ? Resolve(StrokeClip(Kind, backhand)) : -1;
                return index >= 0 && contactPose.TryGetValue(index, out var local) ? model.TransformPoint(local) : SweetSpot.position;
            }
        }

        /// Play the rest of the swing's approach at whatever pace lands its contact frame
        /// `seconds` from now (quicker or a touch slower); the follow-through then runs at its
        /// own speed. The racket arrives as the ball does, instead of still being in the
        /// backswing or already past.
        public void PaceToContact(float seconds)
        {
            float left = TimeToContact;
            swingPace = left > 0 && seconds > 0 ? Mathf.Clamp(left / seconds, .6f, 6f) : 1;
        }

        /// Where the strings meet the ball in this stroke, for the body as it stands now.
        public Vector3 ContactPoint(Stroke kind, bool useBackhand)
        {
            int index = Resolve(StrokeClip(kind, useBackhand));
            return index >= 0 && contactPose.TryGetValue(index, out var local) ? model.TransformPoint(local) : SweetSpot.position;
        }

        /// Steer the racket so the strings meet `ball` in `inSeconds`. The swing keeps its own
        /// arc; the arm (and, for a reach, the body) carries it the difference, easing in to the
        /// contact and back out through the follow-through.
        public void GuideContact(Vector3 ball, float inSeconds)
        {
            guideBall = ball; guideLead = Mathf.Max(inSeconds, .04f); guideClock = guideLead;
            // Early enough to aim the whole swing from its authored contact; otherwise the
            // racket is eased straight to the ball.
            guideAuthored = TimeToContact >= .05f;
            guiding = true; guideFrozen = false;
        }

        public void ReleaseContact() { guiding = false; }
        /// Diagnostics: how far the arm fell short of the steer, and the steer's state.
        public float GuideShortfall { get; private set; }
        public string GuideState => $"w={guideWeight:0.00} clock={guideClock:0.000} authored={guideAuthored} offset(side,up,fwd)={transform.InverseTransformVector(guideOffset)} authoredLocal={transform.InverseTransformPoint(AuthoredContact)} ballLocal={transform.InverseTransformPoint(guideBall)} body={guideBody.magnitude:0.00} short={GuideShortfall:0.00} toBall={Vector3.Distance(SweetSpot.position, guideBall):0.00} ballY={guideBall.y:0.00}";

        const float GuideRelease = .24f;

        /// This frame's steer, in world space, and how much of it the body takes.
        void UpdateGuide()
        {
            if (!guiding && guideWeight <= 0) { guideBody = Vector3.zero; return; }
            if (guiding)
            {
                if (guideClock > 0)
                {
                    float k = 1 - guideClock / guideLead;
                    guideWeight = k * k * (3 - 2 * k);
                    if (guideAuthored) guideOffset = guideBall - AuthoredContact;
                }
                else
                {
                    float k = Mathf.Clamp01(-guideClock / GuideRelease);
                    guideWeight = 1 - k * k * (3 - 2 * k);
                    if (k >= 1) guiding = false;
                }
                if (!Swinging && guideClock > 0) guiding = false;
            }
            else guideWeight = Mathf.MoveTowards(guideWeight, 0, pendingDtUsed / .12f);
            // The body lunges for the part of a reach the arm cannot comfortably make.
            Vector3 want = guideOffset * guideWeight;
            Vector3 flat = new Vector3(want.x, 0, want.z);
            // A low ball is met by sinking at the knees: the stroke's hop gives way first, then
            // the body drops and the planted legs bend under it (PlantFeet).
            guideBody = Vector3.ClampMagnitude(flat * .55f, .45f) + Vector3.up * Mathf.Clamp(want.y * .5f, -(hop + .3f), .1f);
            if (guideBody.y < 0) hop = Mathf.Max(0, hop + guideBody.y);
        }
        float pendingDtUsed;

        /// Carry the racket the rest of the way with the racket arm, the hand keeping its
        /// angle so the string face stays as the stroke set it.
        void ApplyContactGuide()
        {
            if (guideWeight <= 0 || !armUpper || !armLower || !hand) return;
            Vector3 actual = SweetSpot.position - guideBody;       // where the swing alone has it
            if (guiding && !guideAuthored && guideClock > 0) guideOffset = guideBall - actual;
            if (guiding && guideClock <= 0 && !guideFrozen)
            {
                // Contact: from here the follow-through keeps its shape, shifted by the offset
                // the strings needed at the ball.
                guideFrozen = true;
                guideOffset = guideBall - actual;
            }
            Vector3 delta = guideOffset * guideWeight - guideBody;
            if (delta.sqrMagnitude < 1e-6f) return;
            Quaternion held = hand.rotation;
            Vector3 wristGoal = hand.position + delta;
            float armLength = Vector3.Distance(armUpper.position, armLower.position) + Vector3.Distance(armLower.position, hand.position);
            float beyond = Vector3.Distance(armUpper.position, wristGoal) - armLength * .92f;
            if (beyond > 0 && chest && hips)
            {
                // Out of the arm's reach: bend the trunk toward the ball, as a player stretches
                // for a wide or low one, before the arm takes the rest.
                var spine = chest.parent ? chest.parent : chest;
                Vector3 pivot = spine.position;
                Quaternion toward = Quaternion.FromToRotation(armUpper.position - pivot, wristGoal - pivot);
                toward = Quaternion.RotateTowards(Quaternion.identity, toward, 38f);
                spine.rotation = Quaternion.Slerp(Quaternion.identity, toward, Mathf.Clamp01(beyond / .35f)) * spine.rotation;
            }
            Vector3 shoulder = armUpper.position, elbow0 = armLower.position, wrist0 = hand.position;
            float upper = (elbow0 - shoulder).magnitude, lower = (wrist0 - elbow0).magnitude;
            Vector3 goal = wristGoal, toGoal = goal - shoulder;
            float most = (upper + lower) * .995f;
            GuideShortfall = Mathf.Max(0, toGoal.magnitude - most);
            if (toGoal.magnitude > most) goal = shoulder + toGoal.normalized * most;
            // Keep the elbow bending the way the clip bends it.
            Vector3 pole = elbow0 - (shoulder + wrist0) * .5f;
            if (pole.sqrMagnitude < 1e-4f) pole = -model.up;
            Vector3 elbow = StandardCharacterArms.SolveElbow(shoulder, goal, pole, upper, lower);
            armUpper.rotation = Quaternion.FromToRotation(elbow0 - shoulder, elbow - shoulder) * armUpper.rotation;
            Vector3 lowerPos = armLower.position;
            armLower.rotation = Quaternion.FromToRotation(hand.position - lowerPos, goal - lowerPos) * armLower.rotation;
            hand.rotation = held;
        }

        /// The authored strokes carry sideways root travel; gameplay locomotion owns that.
        void StripRootTravel()
        {
            if (!root) return;
            Vector3 shift = model.TransformVector(new Vector3(root.localPosition.x, 0, root.localPosition.z));
            root.position -= shift; racket.position -= shift;
        }

        Leg[] BuildLegs(Transform[] bones)
        {
            var built = new List<Leg>();
            foreach (var side in new[] { "L", "R" })
            {
                var leg = new Leg
                {
                    upper = Array.Find(bones, t => t.name == "UpperLeg." + side),
                    lower = Array.Find(bones, t => t.name == "LowerLeg." + side),
                    foot = Array.Find(bones, t => t.name == "Foot." + side),
                };
                if (!leg.upper || !leg.lower || !leg.foot) continue;
                leg.upperLength = Vector3.Distance(leg.upper.position, leg.lower.position);
                leg.lowerLength = Vector3.Distance(leg.lower.position, leg.foot.position);
                leg.restHeight = leg.foot.position.y - transform.position.y;
                Vector3 local = model.InverseTransformPoint(leg.foot.position);
                leg.restOffset = new Vector3(local.x, 0, local.z);
                leg.phaseOffset = side == "L" ? 0 : .5f;
                built.Add(leg);
            }
            if (built.Count > 0) legLength = Mathf.Min(built[0].upperLength + built[0].lowerLength, built[built.Count - 1].upperLength + built[built.Count - 1].lowerLength);
            return built.ToArray();
        }

        /// Measured contact frames, written by the editor's clip contact audit. Missing
        /// entries keep the old assumption of halfway.
        void LoadContacts()
        {
            var table = Resources.Load<TextAsset>("Tennis/StrokeContacts");
            if (!table) return;
            var parsed = JsonUtility.FromJson<ClipContacts>(table.text);
            if (parsed?.clips == null) return;
            foreach (var entry in parsed.clips)
                if (indices.TryGetValue(entry.clip, out int index)) contacts[index] = entry.contact;
        }
        [Serializable] public class ClipContacts { public ClipContact[] clips; }
        [Serializable] public class ClipContact { public string clip; public float contact; }
        public float ContactFor(string logicalClip) { int i = Resolve(logicalClip); return contacts.TryGetValue(i, out float c) ? c : TennisRules.StrokeContact; }

        // --- Commands ---------------------------------------------------------------------

        public void Swing(float power, bool useBackhand, bool overhead = false)
        { Swing(power, useBackhand, overhead ? Stroke.Smash : Stroke.Drive); }

        public void Swing(float power, bool useBackhand, Stroke kind) => Swing(power, useBackhand, kind, false);

        /// Start a stroke. `provisional` swings begin on the phone's first sign of a stroke and
        /// are either confirmed (`Confirm`) or written off (`CancelSwing`) a few samples later.
        public void Swing(float power, bool useBackhand, Stroke kind, bool provisional)
        {
            if (Swinging) return;
            SwingAge = 0; Power = Mathf.Clamp01(power); backhand = useBackhand; cancelled = false;
            Kind = kind; serving = kind == Stroke.Serve; Overhead = kind == Stroke.Smash || kind == Stroke.Serve;
            Provisional = provisional;
            swingDuration = serving ? TennisRules.StrokeDuration : Mathf.Lerp(.58f, .40f, Power);
            prepare = prepareTarget = 0;
            swingPace = 1; guiding = false;
            if (oneShotIndex >= 0 && oneShotClip != "GroundRecovery") oneShotIndex = -1;
            strokeTrail.Clear();
        }

        public void Serve(float power, bool provisional = false) { Swing(power, false, Stroke.Serve, provisional); }

        /// The phone has confirmed the stroke: it can now strike the ball, at this power.
        public void Confirm(float power) { if (Swinging) { Provisional = false; Power = Mathf.Clamp01(power); } }

        /// Abandon the current swing -- the "stroke" turned out to be a step. The clip fades
        /// back to movement from wherever it had got to instead of snapping.
        public void CancelSwing()
        {
            if (!Swinging) return;
            SwingAge = swingDuration; Provisional = false; cancelled = true; guiding = false; swingPace = 1;
        }

        /// Take the racket back as the ball approaches, before any swing is detected.
        public void Prepare(float amount, bool useBackhand, bool serve = false)
        { prepareTarget = Mathf.Clamp01(amount); prepareBackhand = useBackhand; prepareServe = serve; }

        public void LookAt(Vector3 target, float weight) { lookTarget = target; lookWeight = Mathf.Clamp01(weight); }

        /// Hop into a balanced stance as the other player strikes.
        public void SplitStep() { if (!Swinging) PlayOneShot("SplitStep", .36f); }

        /// Point finished: celebrate a win, slump at a loss.
        public void React(bool won)
        {
            // No celebration swing for now: it read as a random racket wave. A smile is enough.
            if (won) SetExpression(Expression.Happy, 1.8f);
            else { dejected = 1; SetExpression(Expression.Sad, 1.8f); }
        }

        /// The character's signature intro emote (a Higgsfield/Meshy mocap clip retargeted onto
        /// the rig as "Intro"), with the face to match.
        public void PlayIntro()
        {
            SetExpression(Expression.Cheer, 2.4f);
            int index = Resolve("Intro");
            if (index >= 0) PlayOneShot("Intro", clipLength[index]);
        }

        /// World position of the tossing hand's palm, where a held ball sits.
        public Vector3 TossPalm => tossHand ? tossHand.TransformPoint(tossPalmLocal) : transform.position + Vector3.up;

        /// Put the tossing hand's palm at `palm` (weight 0 releases it back to the animation):
        /// the serve routine bounces the ball and tosses it with this.
        public void ReachTossHand(Vector3 palm, float weight) { reachTarget = palm; reachWanted = Mathf.Clamp01(weight); }

        /// Two-bone reach for the tossing arm, on top of whatever the clips posed.
        void ApplyTossReach(float dt)
        {
            reachWeight = Mathf.MoveTowards(reachWeight, reachWanted, dt / .12f);
            if (reachWeight <= .001f || !tossUpper || !tossLower || !tossHand) return;
            Vector3 shoulder = tossUpper.position, elbow0 = tossLower.position, wrist0 = tossHand.position;
            float upper = (elbow0 - shoulder).magnitude, lower = (wrist0 - elbow0).magnitude;
            Vector3 wristGoal = reachTarget - (TossPalm - wrist0);
            Vector3 toGoal = wristGoal - shoulder;
            float most = (upper + lower) * .995f;
            if (toGoal.magnitude > most) wristGoal = shoulder + toGoal.normalized * most;   // reach, never stretch
            wristGoal = Vector3.Lerp(wrist0, wristGoal, reachWeight);
            // Elbow out to the tossing side and slightly back, as a tossing arm bends.
            Vector3 side = tossHand == offHand ? -model.right : model.right;
            Vector3 pole = side * .7f - model.forward * .3f + Vector3.down * .2f;
            Vector3 elbow = StandardCharacterArms.SolveElbow(shoulder, wristGoal, pole, upper, lower);
            tossUpper.rotation = Quaternion.FromToRotation(elbow0 - shoulder, elbow - shoulder) * tossUpper.rotation;
            Vector3 lowerPos = tossLower.position;
            tossLower.rotation = Quaternion.FromToRotation(tossHand.position - lowerPos, wristGoal - lowerPos) * tossLower.rotation;
        }

        /// Pull the trunk back toward upright over the hips. The ready crouch pitches it
        /// forward, and seen from the front for an introduction that foreshortens the torso
        /// until the shirt all but disappears.
        void ApplyUpright()
        {
            if (Upright <= .001f || !hips || !chest || !neck) return;
            var spineBone = chest.parent ? chest.parent : chest;
            Vector3 axis = neck.position - hips.position;
            Quaternion straighten = Quaternion.FromToRotation(axis, Vector3.up);
            spineBone.rotation = Quaternion.Slerp(Quaternion.identity, straighten, Upright) * spineBone.rotation;
        }

        public void PlayOneShot(string clip, float duration)
        {
            int index = Resolve(clip);
            if (index < 0) return;
            oneShotIndex = index; oneShotClip = clip; oneShotAge = 0; oneShotDuration = duration;
        }

        /// Clip name for the current stroke. Literals only: this runs every frame, and
        /// concatenating clip names here allocated garbage continuously.
        string StrokeClip(Stroke kind, bool useBackhand) => kind switch
        {
            Stroke.Volley => useBackhand ? "VolleyBackhand" : "VolleyForehand",
            Stroke.Running => useBackhand ? "RunningBackhand" : "RunningForehand",
            Stroke.Dive => useBackhand ? "DiveBackhand" : "DiveForehand",
            Stroke.Lob => "Lob",
            Stroke.Slice => "Slice",
            Stroke.Topspin => useBackhand ? "Backhand" : "ForehandTopspin",
            Stroke.Celebrate => "Celebrate",
            Stroke.Smash => "Smash",
            Stroke.Serve => "Serve",
            Stroke.LowPickup => "LowPickup",
            Stroke.Missed => "MissedSwing",
            _ => useBackhand ? "Backhand" : "Forehand",
        };

        /// Resolve a clip to this character's handedness. Cached, so the per-frame lookups
        /// never build strings. Falls back rather than throwing: a missing clip should look
        /// wrong, not crash a rally.
        int Resolve(string clip)
        {
            if (resolved.TryGetValue(clip, out int cached)) return cached;
            int found = Lookup(clip);
            resolved[clip] = found;
            return found;
        }

        int Lookup(string clip)
        {
            string hand = LeftHanded ? "_LH" : "_RH";
            if (indices.TryGetValue(clip + hand, out int exact)) return exact;
            if (indices.TryGetValue(clip + "_RH", out int either)) return either;
            if (indices.TryGetValue(clip, out int bare)) return bare;
            if (!missingClips.Contains(clip))
            { missingClips.Add(clip); Debug.LogWarning($"[Tennis] missing animation clip '{clip}'; falling back to Ready"); }
            if (clip != "Ready" && indices.TryGetValue("Ready" + hand, out int ready)) return ready;
            return indices.TryGetValue("Ready", out int last) ? last : (clips != null && clips.Length > 0 ? 0 : -1);
        }

        float ContactOf(int index) => contacts.TryGetValue(index, out float c) ? c : TennisRules.StrokeContact;

        // --- Frame -----------------------------------------------------------------------

        public void Tick(float dt, float speed) { Advance(dt, speed); Pose(); }

        /// Move the animation state forward. Cheap; the pose itself is only built by `Pose`,
        /// so the game can simulate at 120Hz but evaluate the skeleton once per rendered frame.
        /// `speed` is sideways (+ = the character's right); `forward` is toward the net. The
        /// procedural gait steps along the real ground velocity, so moving up to the net or
        /// back to the baseline walks rather than glides.
        public void Advance(float dt, float speed, float forward = 0)
        {
            if (!graph.IsValid()) return;
            pendingDt += dt;
            Speed = speed; ForwardSpeed = forward;
            float travel = new Vector2(speed, forward).magnitude;
            postureSpeed = Mathf.Lerp(postureSpeed, speed, 1 - Mathf.Exp(-dt / .1f));
            bool wasSwinging = Swinging;
            float toContact = TennisRules.SweetTime * swingDuration / TennisRules.StrokeDuration;
            if (swingPace != 1 && SwingAge < toContact)
            {
                float paced = SwingAge + dt * swingPace;
                SwingAge = paced < toContact ? paced : toContact + (paced - toContact) / swingPace;
                if (SwingAge >= toContact) swingPace = 1;
            }
            else { SwingAge += dt; swingPace = 1; }
            guideClock -= dt;
            locomotion += dt * TennisRules.CycleRate(speed);
            float readyIndexLength = clipLength[Mathf.Max(0, Resolve("Ready"))];
            idlePhase = Mathf.Repeat(idlePhase + dt / readyIndexLength, 1);
            // Turn the body toward the direction of travel instead of staying square to the
            // net at every speed: a shuffle stays square, a crossover sprint turns the hips.
            bodyYaw = Mathf.MoveTowards(bodyYaw, Swinging ? 0 : TennisRules.BodyYaw(speed) * Motion.Yaw, dt * 320f);

            DetectTurns(dt, speed, wasSwinging);
            if (wasSwinging && !Swinging && !cancelled)
            {
                // A dive ends on the floor; everything else pushes off back toward the middle.
                if (Kind == Stroke.Dive) { PlayOneShot("GroundRecovery", TennisRules.DiveRecovery); DiveLanded?.Invoke(transform.position); }
                else if (Mathf.Abs(speed) > 1.2f && Kind != Stroke.Celebrate)
                    PlayOneShot(speed < 0 ? "RecoverLeft" : "RecoverRight", .5f);
            }
            if (oneShotIndex >= 0) { oneShotAge += dt; if (oneShotAge >= oneShotDuration) oneShotIndex = -1; }
            dejected = Mathf.MoveTowards(dejected, 0, dt / 1.6f);

            // Locomotion weights: continuous in speed, so starting and stopping ease rather
            // than switch at a threshold. The run direction crossfades when it reverses.
            float run = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, 1.7f, travel));
            float rate = dt / LocomotionFade;
            readyWeight = Mathf.MoveTowards(readyWeight, 1 - run, rate);

            AdvanceAction(dt);
            previousSpeed = speed;
        }

        /// A hard stop plays the brake clip; reversing at speed plays the direction change.
        /// Both were authored and never requested -- reversing just blended one run cycle
        /// into the other, which is exactly the "turns look cheap" complaint.
        void DetectTurns(float dt, float speed, bool wasSwinging)
        {
            turnCooldown -= dt;
            if (Swinging || wasSwinging || turnCooldown > 0 || oneShotIndex >= 0) return;
            float before = previousSpeed;
            if (Mathf.Abs(before) > 1.8f && Mathf.Abs(speed) > 1f && Mathf.Sign(before) != Mathf.Sign(speed))
            { PlayOneShot("DirectionChange", .42f); turnCooldown = .5f; }
            else if (Mathf.Abs(before) > 3.2f && Mathf.Abs(speed) < 1.2f)
            { PlayOneShot(before < 0 ? "BrakeLeft" : "BrakeRight", .4f); turnCooldown = .5f; }
        }

        void AdvanceAction(float dt)
        {
            int wanted;
            float phase, target, fade;
            if (Swinging)
            {
                wanted = Resolve(StrokeClip(Kind, backhand));
                phase = TennisRules.StrokePlayhead(SwingAge, swingDuration, ContactOf(wanted));
                target = 1; fade = StrokeFadeIn;
            }
            else if (prepareTarget > .01f || prepare > .01f)
            {
                // Preparation holds the stroke's own backswing, so a swing that follows
                // continues from where the body already is.
                prepare = Mathf.MoveTowards(prepare, prepareTarget, dt / PrepareFade);
                wanted = Resolve(prepareServe ? "Serve" : prepareBackhand ? "Backhand" : "Forehand");
                TennisRules.ContactWindow(ContactOf(wanted), out float entry, out _);
                phase = Mathf.Lerp(.02f, entry - .02f, prepare);
                target = prepare * .7f; fade = PrepareFade;
                if (actionIndex >= 0 && actionIndex != wanted && actionWeight > .05f && actionOutWeight <= 0)
                {
                    // Let a finishing stroke play out before preparing the next one.
                    wanted = actionIndex; target = 0; fade = StrokeFadeOut;
                    phase = cancelled ? actionPhase : Mathf.Min(1, actionPhase + dt / clipLength[actionIndex]);
                }
            }
            else
            {
                wanted = actionIndex;
                // The follow-through keeps playing at authored speed while it fades out, so
                // the stroke decelerates into the next movement rather than freezing.
                phase = actionIndex >= 0 && !cancelled ? Mathf.Min(1, actionPhase + dt / clipLength[actionIndex]) : actionPhase;
                target = 0; fade = cancelled ? CancelFade : StrokeFadeOut;
            }

            if (wanted != actionIndex && wanted >= 0)
            {
                // A different clip takes over the action layer: hand the old one to the fade
                // slot so the change is a crossfade, never a pop.
                if (actionIndex >= 0 && actionWeight > .01f)
                { actionOutIndex = actionIndex; actionOutPhase = actionPhase; actionOutWeight = actionWeight; }
                actionIndex = wanted; actionWeight = 0;
            }
            actionPhase = phase;
            actionWeight = Mathf.MoveTowards(actionWeight, target, dt / Mathf.Max(.01f, fade));
            actionOutWeight = Mathf.MoveTowards(actionOutWeight, 0, dt / SwitchFade);
            if (actionOutWeight <= 0) actionOutIndex = -1;
        }

        /// Build the skeleton pose for everything advanced since the last call.
        public void Pose()
        {
            if (!graph.IsValid()) return;
            float dt = pendingDt; pendingDt = 0;
            ApplyWeights();
            graph.Evaluate(0);
            // Locomotion is authoritative in gameplay, not the preview clip's lateral root path.
            StripRootTravel();
            float lean = Mathf.Clamp(postureSpeed / TennisRules.SprintSpeed, -1, 1) * 8 * Motion.Lean;
            model.localRotation = Quaternion.Euler(0, bodyYaw, -lean);
            // Players come off the ground on a committed stroke. Strokes that animate their
            // own rise (serve, smash, volleys, dives, running strokes) are left alone.
            if (Swinging && !ClipLeavesGround(Kind))
            {
                float span = Mathf.InverseLerp(TennisRules.StrokeEntry, TennisRules.StrokeExit, TennisRules.StrokePlayhead(SwingAge, swingDuration));
                hop = Mathf.Sin(Mathf.PI * Mathf.Clamp01(span)) * Mathf.Lerp(.06f, .18f, Power);
            }
            else hop = Mathf.MoveTowards(hop, 0, dt * 2.6f);
            // Waiting players stay light on their feet: a small bounce on the toes that fades
            // out as soon as they move or swing.
            float waiting = readyWeight * (1 - actionWeight) * (oneShotIndex >= 0 ? 0 : 1);
            float bounce = Mathf.Abs(Mathf.Sin(idlePhase * Mathf.PI * 4)) * .03f * Motion.IdleBounce * waiting;
            float locomotionShare = Mathf.Clamp01(1 - actionWeight - actionOutWeight);
            // Running lowers the hips a little and bobs twice per stride; the planted feet then
            // bend the knees to suit, which is what makes a run look weighted.
            float pace = Mathf.Clamp01(Mathf.Abs(postureSpeed) / TennisRules.SprintSpeed);
            float bob = gaitWeight * pace * (.05f + .02f * (.5f + .5f * Mathf.Cos(gaitPhase * Mathf.PI * 4)));
            model.localPosition = modelRest + Vector3.up * (hop + bounce - bob);
            pendingDtUsed = dt;
            UpdateGuide();
            if (guideBody != Vector3.zero) model.position += guideBody;

            ApplyRunStyling(postureSpeed, locomotionShare);
            if (backhand && TwoHanded(Kind) && actionIndex >= 0 && (Swinging || actionWeight > .01f)) ApplyBackhandShape(actionWeight);
            if (dejected > 0) ApplyDejection(dejected);
            ApplyLook(dt);
            UpdateFace(dt);
            PlantFeet(dt, bounce, locomotionShare);
            strokeTrail.emitting = Swinging && Kind != Stroke.Celebrate && ContactAge > .06f && ContactAge < .36f;
            float trailPower = Mathf.Lerp(.35f, .8f, Power);
            strokeTrail.startColor = new Color(.55f, .95f, 1, trailPower);
            if (arms) arms.ApplyAfterAnimation();
            ApplyUpright();
            ApplyTossReach(dt);
            AlignRacket();
            if (guideWeight > 0)
            {
                ApplyContactGuide();
                if (backhand && TwoHanded(Kind) && actionIndex >= 0 && (Swinging || actionWeight > .01f)) ApplyBackhandShape(actionWeight);
                AlignRacket();
            }
            SweetVelocity = dt > 0 ? (SweetSpot.position - lastSweetSpot) / dt : Vector3.zero;
            lastSweetSpot = SweetSpot.position;
        }

        void ApplyWeights()
        {
            foreach (int i in weighted) mixer.SetInputWeight(i, 0);
            weighted.Clear();
            float action = Mathf.Clamp01(actionWeight + actionOutWeight);
            float rest = 1 - action;
            float shot = 0;
            if (oneShotIndex >= 0)
            {
                // Brakes and turns differ a lot from the run pose; a short linear fade-in moved
                // the head 15cm in a frame. Ease in and out instead.
                float fadeIn = Mathf.SmoothStep(0, 1, oneShotAge / .15f), fadeOut = Mathf.SmoothStep(0, 1, (oneShotDuration - oneShotAge) / .15f);
                shot = Mathf.Min(fadeIn, fadeOut) * rest;
            }
            float loco = rest - shot;
            // Locomotion is the ready stance plus the procedural gait (PlantFeet) and trunk and
            // arm work (ApplyRunStyling). The authored RunLeft/RunRight clips are not used:
            // measured with their root travel removed, their planted feet slide the way the
            // body is going, each step covers only ~20cm, and the cycle does not loop cleanly
            // -- the head jumped 15cm every time it wrapped.
            SetWeight(Resolve("Ready"), loco, idlePhase);
            if (oneShotIndex >= 0) SetWeight(oneShotIndex, shot, Mathf.Clamp01(oneShotAge / oneShotDuration));
            if (actionOutIndex >= 0) SetWeight(actionOutIndex, actionOutWeight, actionOutPhase);
            if (actionIndex >= 0) SetWeight(actionIndex, actionWeight, actionPhase);
        }

        /// Add weight to a clip. Clips can appear in more than one layer (the stroke being
        /// prepared can also be the one fading out); weights add, and the last phase wins.
        void SetWeight(int index, float weight, float phase)
        {
            if (index < 0 || index >= clips.Length || weight <= 0) return;
            float existing = mixer.GetInputWeight(index);
            mixer.SetInputWeight(index, existing + weight);
            clips[index].SetTime(Mathf.Clamp01(phase) * clipLength[index]);
            if (existing <= 0) weighted.Add(index);
        }

        /// Keep both hands on the grip through a two-handed backhand. The deep shoulder coil
        /// now lives in the Backhand and RunningBackhand clips themselves (see
        /// blender/scripts/fix_tennis_racket_and_backhand.py); the runtime rotation that used
        /// to fake it turned the chest the wrong way and has been removed.
        ///
        /// Runs before the arm IK so the off hand's position is what the arm solves to.
        void ApplyBackhandShape(float weight)
        {
            if (!offHand) return;
            Vector3 secondGrip = GripPosition - StringUp * (TennisRules.BackhandSecondGrip * racketScale);
            offHand.position = Vector3.Lerp(offHand.position, secondGrip, weight);
        }

        /// Two-handers keep the off hand on the racket for groundstrokes; volleys, dives,
        /// slices and overheads are played one-handed, as real two-handed players do.
        static bool TwoHanded(Stroke kind) =>
            kind == Stroke.Drive || kind == Stroke.Topspin || kind == Stroke.Running || kind == Stroke.Lob || kind == Stroke.LowPickup;

        /// Layer the parts of a run the clips do not carry: the thorax counter-rotating
        /// against the pelvis, and the arms swinging with the stride. Both scale with speed.
        void ApplyRunStyling(float speed, float share)
        {
            float effort = TennisRules.CrossoverBlend(speed) * share;
            if (effort <= .001f) return;
            float swing = Mathf.Sin(locomotion * Mathf.PI * 2);
            if (chest)
                chest.rotation = Quaternion.AngleAxis(swing * TennisRules.TrunkCounterRotation * Motion.Trunk * effort, Vector3.up) * chest.rotation;
            if (hips)
                hips.rotation = Quaternion.AngleAxis(-swing * TennisRules.TrunkCounterRotation * .55f * Motion.Trunk * effort, Vector3.up) * hips.rotation;
            Vector3 along = model.TransformDirection(Vector3.forward);
            float reach = TennisRules.ArmSwing * Motion.Arms * effort;
            if (hand) hand.position += along * (swing * reach * .45f);
            if (offHand) offHand.position += along * (-swing * reach);
        }

        /// Lost the point: shoulders drop, head goes down, the racket hangs. Procedural, so
        /// it layers over whatever the legs are doing on the walk back.
        void ApplyDejection(float amount)
        {
            float envelope = Mathf.SmoothStep(0, 1, Mathf.Min(1, (1 - amount) / .2f)) * Mathf.SmoothStep(0, 1, amount / .35f);
            Vector3 side = model.right;
            if (chest) chest.rotation = Quaternion.AngleAxis(16 * envelope, side) * chest.rotation;
            if (head) head.rotation = Quaternion.AngleAxis(24 * envelope, side)
                * Quaternion.AngleAxis(Mathf.Sin((1 - amount) * 14) * 9 * envelope, model.up) * head.rotation;
            if (hand) hand.position += Vector3.down * (.16f * envelope);
            if (offHand) offHand.position += Vector3.down * (.1f * envelope);
            lookWeight *= 1 - envelope;
        }

        /// Players watch the ball. The head turns toward the target within a natural range,
        /// split between neck and head, and eases rather than snapping.
        void ApplyLook(float dt)
        {
            if (!head || lookWeight <= .001f) return;
            float weight = lookWeight * Motion.Look;
            Vector3 body = chest ? chest.TransformDirection(chestLocalForward) : model.forward;
            Vector3 desired = lookTarget - head.position;
            if (desired.sqrMagnitude < .01f) return;
            desired = ClampCone(body, desired.normalized, 70f);
            if (!lookInitialised || dt <= 0) { lookDirection = desired; lookInitialised = true; }
            else lookDirection = Vector3.Slerp(lookDirection, desired, 1 - Mathf.Exp(-dt * 12)).normalized;
            if (neck)
            {
                Vector3 current = head.TransformDirection(headLocalForward);
                neck.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(current, lookDirection), .4f * weight) * neck.rotation;
            }
            Vector3 now = head.TransformDirection(headLocalForward);
            head.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(now, lookDirection), weight) * head.rotation;
        }

        static Vector3 ClampCone(Vector3 axis, Vector3 direction, float degrees)
        {
            float angle = Vector3.Angle(axis, direction);
            return angle <= degrees ? direction : Vector3.Slerp(axis, direction, degrees / angle).normalized;
        }

        /// Where the feet go.
        ///
        /// Standing, swinging and landing: the authored feet, held in place while they bear
        /// weight and never sunk into the court.
        ///
        /// Moving: a procedural gait. The run clips turned out to be short shuffle steps
        /// (about 20cm each) that no playback rate can stretch to a sprint, and gameplay moves
        /// the body anyway -- so feet were sliding. Now each foot plants and stays planted
        /// through its stance, then swings on an arc to a landing spot chosen from the
        /// body's velocity, with cadence and stride growing with pace. Stepping out square to
        /// the net reads as a shuffle; turned toward the run, the same logic is a stride.
        void PlantFeet(float dt, float bounce, float locomotionShare)
        {
            if (legs == null) return;
            float ground = transform.position.y;
            bool airborne = hop > .012f || bounce > .012f || (Swinging && ClipLeavesGround(Kind)) || GroundRecovering;
            float speed = new Vector2(Speed, ForwardSpeed).magnitude;
            float pace = Mathf.Clamp01(speed / TennisRules.SprintSpeed);
            float gaitTarget = airborne ? 0 : locomotionShare * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .9f, speed));
            gaitWeight = Mathf.MoveTowards(gaitWeight, gaitTarget, dt / .12f);
            float swingShare = Mathf.Lerp(.5f, .7f, pace);             // >.5 at pace: a flight phase
            // Cadence rises with pace, and never lets a stance sweep further than the leg can
            // reach either side of the hip. These legs are short (0.63m, nearly straight at
            // rest), so a planted foot only has about a quarter-metre either way; asking for
            // more drags the foot along at full stretch, which is what read as a pop.
            float cadence = Mathf.Max(Mathf.Lerp(2.6f, 4.4f, pace), speed * (1 - swingShare) / (.42f * legLength));
            float cycle = 2 / cadence;
            gaitPhase = Mathf.Repeat(gaitPhase + dt / cycle, 1);
            Vector3 velocity = transform.right * Speed + transform.forward * ForwardSpeed;
            float lift = Mathf.Lerp(.07f, .2f, pace);
            foreach (var leg in legs)
            {
                Vector3 animated = leg.foot.position;
                float floor = ground + leg.restHeight;
                // Authored feet, locked while planted.
                bool contact = !airborne && animated.y - floor < .035f;
                if (contact && !leg.planted) { leg.planted = true; leg.lockPosition = new Vector3(animated.x, floor, animated.z); }
                else if (!contact) leg.planted = false;
                else if (new Vector2(animated.x - leg.lockPosition.x, animated.z - leg.lockPosition.z).magnitude > FootLockRange)
                { leg.planted = false; leg.stepLift = 1; }
                leg.lockWeight = Mathf.MoveTowards(leg.lockWeight, leg.planted ? 1 : 0, dt / (leg.planted ? .05f : .1f));
                leg.stepLift = Mathf.MoveTowards(leg.stepLift, 0, dt / .12f);
                Vector3 goal = Vector3.Lerp(animated, leg.lockPosition, leg.lockWeight);
                goal.y += Mathf.Sin(Mathf.PI * leg.stepLift) * .05f;

                // Procedural gait.
                Vector3 home = model.TransformPoint(leg.restOffset); home.y = floor;
                // Reset only a standing foot that is somewhere it cannot be -- after the game
                // repositions the player. A swinging foot's old plant is legitimately far
                // behind at a sprint, and resetting it snapped the foot forward.
                if (gaitWeight <= .001f || !leg.gaitReady || (!leg.swinging && (leg.plant - home).sqrMagnitude > 2.25f))
                {
                    // Not walking: keep the gait's footing where the foot actually is, so the
                    // first step starts from there rather than from somewhere remembered.
                    leg.plant = leg.landAt = new Vector3(goal.x, floor, goal.z); leg.swinging = false; leg.gaitReady = true;
                }
                else
                {
                    float p = Mathf.Repeat(gaitPhase + leg.phaseOffset, 1);
                    // Each step is timed from its own lift-off, so a change of pace mid-step
                    // never runs a foot backwards.
                    // A step starts exactly when its window opens on the gait clock and gets the
                    // full window, so the two feet stay half a cycle apart. Starting late (and
                    // squeezing the step) whipped the foot; starting whenever stance allowed let
                    // the feet drift out of step until one dragged at full stretch.
                    bool windowOpened = p < leg.lastPhase;
                    leg.lastPhase = p;
                    if (windowOpened && leg.swinging)
                    { leg.swinging = false; leg.plant = leg.landAt; leg.stanceTime = 0; }
                    if (!leg.swinging && windowOpened)
                    {
                        leg.swinging = true; leg.swingT = 0; leg.liftFrom = leg.plant;
                        leg.swingDuration = swingShare * cycle;
                        leg.halfStance = (1 - swingShare) * cycle * .5f;
                    }
                    Vector3 gait;
                    if (leg.swinging)
                    {
                        leg.swingT = Mathf.Min(1, leg.swingT + dt / leg.swingDuration);
                        // Land where the body will be over the foot at mid-stance. The spot is
                        // tracked while the foot is high and fixed for the touchdown, so the
                        // foot arrives with no speed along the ground instead of stopping dead.
                        if (leg.swingT < .6f)
                        {
                            float ahead = (1 - leg.swingT) * leg.swingDuration + leg.halfStance;
                            leg.landAt = home + velocity * ahead; leg.landAt.y = floor;
                        }
                        float e = leg.swingT * leg.swingT * (3 - 2 * leg.swingT);
                        gait = Vector3.Lerp(leg.liftFrom, leg.landAt, e) + Vector3.up * (Mathf.Sin(Mathf.PI * leg.swingT) * lift);
                        if (leg.swingT >= 1) { leg.swinging = false; leg.plant = leg.landAt; leg.stanceTime = 0; FootPlanted?.Invoke(leg.plant, speed); }
                    }
                    else { gait = leg.plant; leg.stanceTime += dt; }
                    goal = Vector3.Lerp(goal, gait, gaitWeight);
                }
                if (!airborne) goal.y = Mathf.Max(goal.y, floor);
                if ((goal - animated).sqrMagnitude < .000001f) continue;
                SolveLeg(leg, goal);
            }
        }

        void SolveLeg(Leg leg, Vector3 goal)
        {
            Vector3 hip = leg.upper.position, knee = leg.lower.position;
            Quaternion footRotation = leg.foot.rotation;
            float reach = (leg.upperLength + leg.lowerLength) * .999f;
            Vector3 toGoal = goal - hip;
            if (toGoal.magnitude > reach) goal = hip + toGoal.normalized * reach;
            Vector3 axis = goal - hip;
            // Knees point where the body faces, splayed slightly outward. These legs are nearly
            // straight at rest, so the animated bend direction is a few millimetres of noise
            // that flipped from frame to frame and threw the knee across.
            float outward = leg.phaseOffset == 0 ? -1 : 1;
            Vector3 pole = model.forward + model.right * (.25f * outward) + Vector3.ProjectOnPlane(knee - hip, axis.normalized) * 2f;
            Vector3 solved = StandardCharacterArms.SolveElbow(hip, goal, pole, leg.upperLength, leg.lowerLength);
            leg.upper.rotation = Quaternion.FromToRotation(knee - hip, solved - hip) * leg.upper.rotation;
            Vector3 currentShin = leg.foot.position - leg.lower.position;
            leg.lower.rotation = Quaternion.FromToRotation(currentShin, goal - leg.lower.position) * leg.lower.rotation;
            leg.foot.rotation = footRotation;
        }

        /// Rebuild the arm surfaces from the bones as they stand -- used by replay playback,
        /// which sets the skeleton directly instead of animating it.
        public void RefreshArms() { if (arms) arms.ApplyAfterAnimation(); }

        void AlignRacket()
        {
            if (!racketVisual) return;
            // The grip inset scales with the racket, or the handle detaches from the hand.
            racketVisual.SetPositionAndRotation(GripPosition - StringUp * (TennisKitV3.GripInset * racketScale), Quaternion.LookRotation(StringNormal, StringUp));
        }

        public static void PrepareMaterials(GameObject obj, Color? skin = null) => TennisLook.PrepareCharacter(obj, skin);

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy(); if (strokeMaterial) Destroy(strokeMaterial);
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
                if (r.name.StartsWith("KitV3 ") && r.sharedMesh) Destroy(r.sharedMesh);
        }
    }
}
