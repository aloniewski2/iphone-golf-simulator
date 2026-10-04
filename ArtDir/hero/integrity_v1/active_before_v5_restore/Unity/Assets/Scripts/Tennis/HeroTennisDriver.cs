using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GolfArcade.Tennis
{
    /// Visual puppet for the locked Hero01 on top of a gameplay TennisActor.
    /// The actor (hidden) stays the gameplay authority: movement, swing timing, racket contact test,
    /// scoring. This component only READS it and plays the approved Hero_* clips so that each stroke
    /// clip's authored contact frame lands on the actor's gameplay contact. No gameplay effect.
    [DefaultExecutionOrder(1000)]
    public sealed class HeroTennisDriver : MonoBehaviour
    {
        public enum Clip { Ready, Idle, Forehand, Backhand, Serve, Volley, Smash, RunForward, RunRight, RunLeft,
                           HitPerfect, MissWhiff, CelebratePoint, SadPointLost, MatchWin, MatchLose, Walk }
        const int Count = 17;
        // Visual tempo only: contact remains anchored to the actor's authoritative hit time.
        public static float AnimationTempo = 1.2f;
        [Serializable] public struct ClipSlot { public Clip id; public AnimationClip clip; public float contact; }
        public ClipSlot[] slots = new ClipSlot[0];
        public TennisActor actor;
        public TennisGame game;
        public ModularHeroLook look;
        public HeroCosmetics cosmetics;
        public bool isPlayer;
        [Tooltip("Hero V5: the hair mesh is a closed opaque volume; skip the V4 hair-shell workarounds.")] public bool closedHairV5 = true;
        [Tooltip("Planted-foot speed (m/s) of the run clips at 1x, measured from the baked clips (feet_report.json).")] public float runClipSpeed = 5.8f;
        [Tooltip("Planted-foot speed of Hero_Walk_v5 at 1x (2 x 0.26 m stride over 0.54 s stance).")] public float walkClipSpeed = .96f;
        float walkYaw;
        [Tooltip("Max distance the right arm is nudged so the racket meets the gameplay ball at contact.")] public float contactAssist = .5f;
        [Tooltip("Max body lunge toward the ball near contact (m).")] public float bodyReach = .3f;

        /// Editor proof only: HERO_BASELINE=1 disables the Plan 1 fixes (measurements stay on).
        public static readonly bool Baseline = Environment.GetEnvironmentVariable("HERO_BASELINE") == "1";
        PlayableGraph graph; AnimationMixerPlayable mixer;
        // Plan 3A: an upper-body layer (duplicate action clips, avatar-masked) so a swing can ride on running
        // legs: the arms / spine swing while the legs keep their planted run cycle (no skate into swings).
        AnimationMixerPlayable upperMixer; AnimationLayerMixerPlayable layers;
        readonly AnimationClipPlayable[] upperPlayables = new AnimationClipPlayable[Count];
        /// Editor proof only: HERO_FLUID=0 turns the Plan 3A motion layers off (before/after captures).
        public static readonly bool FluidOff = Environment.GetEnvironmentVariable("HERO_FLUID") == "0" || Baseline;
        float legsFromRun, actionLegs; HeroFace face;
        readonly AnimationClipPlayable[] playables = new AnimationClipPlayable[Count];
        readonly float[] length = new float[Count], contact = new float[Count], weight = new float[Count];
        readonly double[] time = new double[Count];
        readonly bool[] valid = new bool[Count];
        readonly float[] targetBuf = new float[Count], weightVel = new float[Count];
        float actionWeightVel;
        Clip action = Clip.Ready; bool actionActive, followThrough; float actionWeight; double actionTime;
        float lead = .4f; float lastPerfect = -99; bool wasSwinging; Clip juice; bool juiceActive; double juiceTime; bool perfectPending;
        public Clip CurrentAction => actionActive ? action : juiceActive ? juice : Clip.Ready;
        public string State { get; private set; } = "Ready";

        public void Build()
        {
            foreach (var s in slots) { int i = (int)s.id; if (!s.clip) continue; valid[i] = true; length[i] = s.clip.length; contact[i] = s.contact; }
            var animator = look.animator; look.externalAnimation = true;
            graph = PlayableGraph.Create(name + " hero driver");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, Count);
            foreach (var s in slots)
            {
                if (!s.clip) continue; int i = (int)s.id;
                playables[i] = AnimationClipPlayable.Create(graph, s.clip); playables[i].SetApplyFootIK(false); playables[i].SetSpeed(0);
                graph.Connect(playables[i], 0, mixer, i);
            }
            upperMixer = AnimationMixerPlayable.Create(graph, Count);
            foreach (var s in slots)
            {
                if (!s.clip) continue; int i = (int)s.id; if (!IsStroke((Clip)i)) continue;
                upperPlayables[i] = AnimationClipPlayable.Create(graph, s.clip); upperPlayables[i].SetApplyFootIK(false); upperPlayables[i].SetSpeed(0);
                graph.Connect(upperPlayables[i], 0, upperMixer, i);
            }
            layers = AnimationLayerMixerPlayable.Create(graph, 2);
            graph.Connect(mixer, 0, layers, 0); graph.Connect(upperMixer, 0, layers, 1);
            layers.SetInputWeight(0, 1); layers.SetInputWeight(1, 0);
            var upper = new AvatarMask();
            for (int bp = 0; bp < (int)AvatarMaskBodyPart.LastBodyPart; bp++) upper.SetHumanoidBodyPartActive((AvatarMaskBodyPart)bp, false);
            foreach (var bp in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK })
                upper.SetHumanoidBodyPartActive(bp, true);
            layers.SetLayerMaskFromAvatarMask(1, upper);
            AnimationPlayableOutput.Create(graph, "Hero", animator).SetSourcePlayable(layers);
            weight[(int)Clip.Ready] = 1;
            BuildContactModel();
            // The body and eye meshes shipped with frozen bind-pose bounds: with lunges, crouches and big
            // swings Unity culled them at some camera angles (the hero "went transparent"). Always skin.
            foreach (var smr in look.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
            if (!Baseline) HeroHeadOccluder.Build(look);
            if (!Baseline) { var hairTone = HairTone(); if (hairTone.a > 0) { EnsureOwnScalpMaterial(); look.SetScalpTone(hairTone); } }
            if (!Baseline) SealHeadUnderHair();
            // Hero V5 hair is a closed, consistently wound, opaque volume (ArtDir/hero/v5_tools/build_hair_v5.py): the
            // V4 runtime workarounds (re-wind, dark interior pass, hair core) are only for the archived V4 shell.
            if (!Baseline && !closedHairV5 && Environment.GetEnvironmentVariable("HERO_HAIRFIX") != "0") { SolidHairInterior(); BuildHairCore(); }
            if (!Baseline && cosmetics) cosmetics.EquipHat(cosmetics.CurrentHat);   // fits the hat liner for the default visor too
            if (!FluidOff && Environment.GetEnvironmentVariable("HERO_FACE") != "0") face = HeroFace.Build(look, look.skinTone * new Color(.97f, .9f, .86f, 1));
            body = HeroBodyProxy.Build(look.animator, look.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            if (Baseline) runClipSpeed = 3.2f;
            if (actor && Baseline) actor.Reacted += OnReacted;
            else if (actor)
            {
                actor.Reacted += OnReacted;
                // The racket the player sees IS the gameplay racket: contact tests, launch point,
                // contact-frame ball and planned meet points all use the hero's strings.
                actor.RedirectStrings(stringCentre, stringRight, stringUp, stringNormal);
                actor.VisualContact = VisualContact;
                actor.VisualTossPoint = VisualTossPoint;
                actor.Posed += OnActorPosed;
            }
            if (isPlayer) { TennisGame.ContactMade += OnContact; TennisGame.Whiffed += OnWhiff; }
            else TennisGame.OpponentStruck += OnRivalStruck;
            TennisGame.Netted += OnNetted;
            BuildTrail();
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            if (actor) { actor.Reacted -= OnReacted; actor.Posed -= OnActorPosed; actor.VisualContact = null; actor.VisualTossPoint = null; }
            TennisGame.ContactMade -= OnContact; TennisGame.Whiffed -= OnWhiff; TennisGame.OpponentStruck -= OnRivalStruck; TennisGame.Netted -= OnNetted;
        }

        // ---- visible racket = gameplay racket
        Transform stringCentre, stringRight, stringUp, stringNormal; bool racketScaled;
        readonly Vector3[] contactLocal = new Vector3[Count]; readonly bool[] hasContact = new bool[Count];
        Transform[] feet = new Transform[4]; float heelRest, toeRest; Transform hips;
        Vector3 torsoFwdHips, torsoFwdChest; readonly Vector3[] handAxis = new Vector3[2];
        void BuildContactModel()
        {
            var a = look.animator; var grip = look.racketGrip;
            Transform Mk(string n, Vector3 p) { var t = new GameObject(n).transform; t.SetParent(grip, false); t.localPosition = p; return t; }
            // racket-local metres (the grip socket compensates bone scale); head centre at y=.395 on the sculpt.
            // The racket is drawn TennisRules.HeroRacketScale bigger about the grip (a tennis racket, not a
            // badminton one next to the big chibi head); the contact markers move with it, so the visible
            // head, the swing's aim point and the rules' string bed are the same thing.
            // (racketGrip IS the racket mesh, pivot at the grip: scaling it scales the markers below with it)
            const float hc = .395f;
            if (!racketScaled) { grip.localScale *= TennisRules.HeroRacketScale; racketScaled = true; }
            stringCentre = Mk("Hero strings centre", new Vector3(0, hc, 0));
            stringRight = Mk("Hero strings right", new Vector3(.1f, hc, 0));
            stringUp = Mk("Hero strings up", new Vector3(0, hc + .1f, 0));
            stringNormal = Mk("Hero strings normal", new Vector3(0, hc, .1f));
            hips = a.GetBoneTransform(HumanBodyBones.Hips);
            feet[0] = a.GetBoneTransform(HumanBodyBones.LeftFoot); feet[1] = a.GetBoneTransform(HumanBodyBones.LeftToes);
            feet[2] = a.GetBoneTransform(HumanBodyBones.RightFoot); feet[3] = a.GetBoneTransform(HumanBodyBones.RightToes);
            // bind pose (not yet evaluated): ankle / toe-joint heights above the soles
            heelRest = transform.InverseTransformPoint(feet[0].position).y; toeRest = transform.InverseTransformPoint(feet[1].position).y;
            if (Environment.GetEnvironmentVariable("HERO_DEBUG") == "1") Debug.Log($"[HeroDbg] {name} heelRest={heelRest:F3} toeRest={toeRest:F3}");
            CaptureTorsoBind();
            spineBindLocal = a.GetBoneTransform(HumanBodyBones.Spine).localRotation; chestBindLocal = a.GetBoneTransform(HumanBodyBones.Chest).localRotation;
            for (int h = 0; h < 2; h++) { var hb = a.GetBoneTransform(h == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand); Transform mid = null; foreach (var t in hb.GetComponentsInChildren<Transform>()) if (t.name == (h == 0 ? "Middle1.L" : "Middle1.R")) { mid = t; break; }
                handAxis[h] = Quaternion.Inverse(hb.rotation) * ((mid ? mid.position : hb.position + hb.up * .07f) - hb.position); }
            var chest = a.GetBoneTransform(HumanBodyBones.Chest);
            torsoFwdHips = Quaternion.Inverse(hips.rotation) * transform.forward; torsoFwdChest = Quaternion.Inverse(chest.rotation) * transform.forward;
            // Where the strings are at each stroke's authored contact frame, in hero space.
            foreach (var c in new[] { Clip.Forehand, Clip.Backhand, Clip.Serve, Clip.Volley, Clip.Smash })
            {
                int i = (int)c; if (!valid[i]) continue;
                for (int k = 0; k < Count; k++) if (valid[k]) { mixer.SetInputWeight(k, k == i ? 1 : 0); playables[k].SetTime(k == i ? contact[i] : 0); }
                graph.Evaluate(0);
                contactLocal[i] = transform.InverseTransformPoint(stringCentre.position); hasContact[i] = true;
            }
            BuildChainTables();
            for (int k = 0; k < Count; k++) if (valid[k]) mixer.SetInputWeight(k, k == (int)Clip.Ready ? 1 : 0);
            graph.Evaluate(0);
        }

        // ---- Plan 3A kinetic chain: hips-, chest- and arm-segment orientations per stroke clip, sampled at 60 Hz
        const float TableRate = 60;
        readonly Quaternion[][] hipsTable = new Quaternion[Count][], chestTable = new Quaternion[Count][];
        Transform spineB, chestB, neckB; Quaternion spineBind, chestBind;
        void BuildChainTables()
        {
            var a = look.animator; spineB = a.GetBoneTransform(HumanBodyBones.Spine); chestB = a.GetBoneTransform(HumanBodyBones.Chest); neckB = a.GetBoneTransform(HumanBodyBones.Neck);
            foreach (var c in new[] { Clip.Forehand, Clip.Backhand, Clip.Serve, Clip.Volley, Clip.Smash })
            {
                int i = (int)c; if (!valid[i]) continue;
                int n = Mathf.CeilToInt(length[i] * TableRate) + 1; hipsTable[i] = new Quaternion[n]; chestTable[i] = new Quaternion[n];
                for (int f = 0; f < n; f++)
                {
                    for (int k = 0; k < Count; k++) if (valid[k]) { mixer.SetInputWeight(k, k == i ? 1 : 0); playables[k].SetTime(k == i ? f / TableRate : 0); }
                    graph.Evaluate(0);
                    hipsTable[i][f] = Quaternion.Inverse(transform.rotation) * hips.rotation;
                    chestTable[i][f] = Quaternion.Inverse(transform.rotation) * chestB.rotation;
                }
            }
        }
        static Quaternion SampleTable(Quaternion[] t, float time)
        {
            float x = Mathf.Clamp(time * TableRate, 0, t.Length - 1); int i = Mathf.Min((int)x, t.Length - 2);
            return Quaternion.Slerp(t[i], t[i + 1], x - i);
        }
        Vector3? VisualContact(TennisActor.Stroke kind, bool backhand)
        {
            Clip c;
            switch (kind)
            {
                case TennisActor.Stroke.Serve: c = Clip.Serve; break;
                case TennisActor.Stroke.Smash: c = Clip.Smash; break;
                case TennisActor.Stroke.Volley: c = backhand ? Clip.Backhand : Clip.Volley; break;
                case TennisActor.Stroke.Missed: case TennisActor.Stroke.Celebrate: return null;
                default: c = backhand ? Clip.Backhand : Clip.Forehand; break;
            }
            return hasContact[(int)c] ? actor.transform.TransformPoint(contactLocal[(int)c]) : (Vector3?)null;
        }
        /// The game poses the actor on its precise contact sub-steps: pose the hero racket for the
        /// same instant so string-crossing tests and launch points see the racket on screen.
        void OnActorPosed()
        {
            if (!graph.IsValid() || (game && game.ReplayPlaying) || !actor.Swinging || !actionActive || !IsStroke(action)) return;
            int i = (int)action;
            playables[i].SetTime(StrokeTime(i, actor.SignedTimeToContact));
            if (upperPlayables[i].IsValid()) upperPlayables[i].SetTime(StrokeTime(i, actor.SignedTimeToContact));
            graph.Evaluate(0);
            PostProcess(true);
        }
        /// After-point emotes are removed (Plan 1B; custom emotes come in Plan 3): a point ending
        /// plays nothing — a whiff finishes its recover, then Ready / the next serve.
        /// Plan 2B: point-end auto emotes are back ON as a temporary placeholder set until custom emotes
        /// (Plan 3): winner cheers (racket-up jump) or fist-pumps, loser slumps or facepalms, match
        /// win/lose get the big ones. Automatic, no picker; the next serve cuts them off (~2.4 s cap).
        public static bool PointEmotes = true;
        public int PointReactionsSuppressed { get; private set; }
        public int PointEmotesPlayed { get; private set; }
        public string LastEmote { get; private set; }
        int emoteRoll;
        void OnReacted(bool won, TennisActor.Moment moment)
        {
            if (!PointEmotes) { PointReactionsSuppressed++; return; }
            emoteRoll++;
            var c = moment == TennisActor.Moment.Match ? (won ? Clip.MatchWin : Clip.MatchLose)
                : won ? (emoteRoll % 2 == 0 ? Clip.CelebratePoint : Clip.HitPerfect) : (emoteRoll % 2 == 0 ? Clip.SadPointLost : Clip.MatchLose);
            PointEmotesPlayed++; LastEmote = c.ToString();
            // Let a whiff finish its overswing before the point reaction.
            if (juiceActive && juice == Clip.MissWhiff) { queued = c; hasQueued = true; } else PlayJuice(c);
        }
        Clip queued; bool hasQueued;
        void OnContact(Vector2 face, Timing grade, bool super)
        {
            // Score80 B: heavy contact = the body takes the hit after the hit-stop (chest recoil + root squash);
            // Great gets a light one; routine hits stay clean.
            bool ult = isPlayer && game && game.UltimateArmed && game.PlayerUltimate >= 1;   // this contact spends the ultimate
            bool heavy = super || grade >= Timing.Perfect || actor.Kind == TennisActor.Stroke.Smash || ult;
            if (!FluidOff && (heavy || grade >= Timing.Great)) KickTorso(new Vector3(ult ? -3.2f : heavy ? -2.3f : -1.1f, 0, 0), ult ? 1 : heavy ? .75f : .3f);
            if (grade >= Timing.Perfect || super) perfectPending = true; Trail(grade >= Timing.Perfect || super ? .32f : .2f, grade >= Timing.Perfect || super ? new Color(1, .9f, .45f, .9f) : new Color(1, 1, 1, .6f)); }
        void OnRivalStruck() => Trail(.2f, new Color(1, 1, 1, .5f));
        /// Score80 B: whoever put it in the tape slumps (the comic beat before the point emote).
        void OnNetted(bool byPlayer) { if (!FluidOff && byPlayer == isPlayer) KickTorso(new Vector3(2.2f, 0, 0), .6f); }
        // ---- racket trail: lit only AFTER a confirmed contact (the follow-through) or a called whiff
        TrailRenderer trail; float trailFor;
        void BuildTrail()
        {
            if (!stringCentre) return;
            trail = stringCentre.gameObject.AddComponent<TrailRenderer>();
            trail.time = .16f; trail.startWidth = .22f; trail.endWidth = 0; trail.minVertexDistance = .03f; trail.emitting = false;
            var glow = Resources.Load<Shader>("Tennis/Shaders/TennisFxAdditive");
            trail.sharedMaterial = new Material(glow ? glow : Shader.Find("Sprites/Default")) { name = "Hero racket trail", mainTexture = TennisLook.Falloff };
            trail.textureMode = LineTextureMode.Stretch; trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
        }
        void Trail(float seconds, Color c)
        {
            if (!trail) return; trailFor = seconds; trail.Clear(); trail.emitting = true;
            trail.startColor = c; trail.endColor = new Color(c.r, c.g, c.b, 0);
        }
        void OnWhiff() { actionActive = false; PlayJuice(Clip.MissWhiff); if (!FluidOff) KickTorso(new Vector3(1.4f, 0, 0), .5f, 1.9f);   // Score80 B: a miss carries on further than a hit (comic overshoot)
            Trail(.4f, new Color(.75f, .88f, 1f, .75f)); }
        public void PlayJuice(Clip c) { if (!valid[(int)c]) return; juice = c; juiceActive = true; juiceTime = FluidOff || c == Clip.MissWhiff || c == Clip.HitPerfect ? 0 : -.14; perfectPending = false; }   // point emotes: a short beat to settle, then the eased lead-in

        static bool IsStroke(Clip c) => c >= Clip.Forehand && c <= Clip.Smash;
        Clip StrokeClip()
        {
            switch (actor.Kind)
            {
                case TennisActor.Stroke.Serve: return Clip.Serve;
                case TennisActor.Stroke.Smash: return Clip.Smash;
                case TennisActor.Stroke.Volley: return actor.Backhand ? Clip.Backhand : Clip.Volley;
                default: return actor.Overhead ? Clip.Smash : actor.Backhand ? Clip.Backhand : Clip.Forehand;
            }
        }

        void LateUpdate()
        {
            if (!graph.IsValid() || !actor || (game && game.ReplayPlaying)) return;
            float dt = Time.deltaTime;
            // ---- action layer: prepare / swing / follow-through, contact-locked to gameplay
            bool swinging = actor.Swinging;
            if (swinging)
            {
                var c = StrokeClip(); int i = (int)c;
                float ttc = actor.SignedTimeToContact;
                if (!wasSwinging || !actionActive || action != c) { lead = Mathf.Lerp(lead, Mathf.Clamp(ttc, .15f, .9f), .5f); action = c; actionActive = true; juiceActive = false; swingStartTtc = Mathf.Max(ttc, .05f); }
                actionTime = StrokeTime(i, ttc);
                followThrough = true;
                State = c + (ttc > 0 ? " swing" : " follow");
            }
            else if (actionActive && (followThrough || Baseline) && IsStroke(action))
            {
                actionTime += dt * AnimationTempo;   // quicker authored follow-through, same contact anchor
                if (actionTime >= length[(int)action] - .05f || (actor.PrepareAmount > .05f && !BetweenPoints()) || Moving() > .6f || (BetweenPoints() && juiceActive)) actionActive = false;
                if (!actionActive && perfectPending && !juiceActive && !BetweenPoints() && Time.time - lastPerfect > 8f && Moving() < .6f) { PlayJuice(Clip.HitPerfect); lastPerfect = Time.time; }
                if (!actionActive) perfectPending = false;
                State = action + " finish";
            }
            else if ((actor.PrepareAmount > .02f || (actor.PrepareServe && !Baseline && !WalkingIn())) && !BetweenPoints() && !(juiceActive && juice == Clip.MissWhiff))
            {
                var c = actor.PrepareServe ? Clip.Serve : actor.PrepareBackhand ? Clip.Backhand : Clip.Forehand; int i = (int)c;
                action = c; actionActive = true; juiceActive = false; followThrough = false;
                if (c == Clip.Serve && !Baseline)
                {
                    // The server owns the whole body from stepping up to the line: the serve clip's own
                    // side-on pre-serve stance (with a slow weight sway) while the ball is bounced, its
                    // tossing-arm rise through the wind-up, then the trophy as the toss goes up.
                    float p = actor.PrepareAmount;
                    actionTime = p <= .001f ? ServeStanceT + .1f * Mathf.Sin(Time.time * 1.7f)
                        : p < .4f ? Mathf.Lerp(ServeStanceT, ServeReleaseT, p / .4f) : Mathf.Lerp(ServeReleaseT, ServeTrophyT, (p - .4f) / .6f);
                }
                else actionTime = actor.PrepareAmount * Mathf.Max(0, contact[i] - lead);
                State = c + " prepare " + actor.PrepareAmount.ToString("0.00");
            }
            else actionActive = false;
            wasSwinging = swinging;
            // Mid-rally flourishes give way to real play: moving or preparing cancels them.
            if (juiceActive && juice == Clip.HitPerfect && !BetweenPoints() && (Moving() > .9f || actionActive) && juiceTime > .35f) juiceActive = false;
            if (juiceActive) { juiceTime += dt * AnimationTempo; if (juiceTime >= length[(int)juice]) { juiceActive = false; if (hasQueued) { hasQueued = false; PlayJuice(queued); } } else State = juice.ToString(); }
            if (!actionActive && !juiceActive) State = Moving() > .15f ? "Run" : "Ready";

            // ---- base layer: ready / idle / runs by ground velocity
            var target = targetBuf; Array.Clear(target, 0, Count);
            float move = Moving();
            Vector2 v = new Vector2(actor.Speed, actor.ForwardSpeed);
            float runW = Mathf.Clamp01((move - .15f) / .9f);
            var baseIdle = Baseline && BetweenPoints() && valid[(int)Clip.Idle] ? Clip.Idle : Clip.Ready;   // no between-point idle emote: Ready
            target[(int)baseIdle] = 1 - runW;
            bool walkMode = WalkMode();
            if (runW > 0 && walkMode) target[(int)Clip.Walk] += runW;
            else if (runW > 0)
            {
                float side = Mathf.Abs(v.x), fwd = Mathf.Abs(v.y), sum = Mathf.Max(1e-4f, side + fwd);
                target[(int)Clip.RunForward] += runW * fwd / sum;
                target[(int)(v.x >= 0 ? Clip.RunRight : Clip.RunLeft)] += runW * side / sum;
            }
            // ---- overlay action or juice on top of the base
            float want = actionActive || juiceActive ? 1 : 0;
            // emotes ease in (lead-in) instead of popping; strokes stay snappy
            float rise = juiceActive && !actionActive ? .26f : .08f;
            if (FluidOff || (want > 0 && actionActive)) actionWeight = Mathf.MoveTowards(actionWeight, want, dt / (want > 0 ? rise : .2f));
            // Score80 A: ease-in/ease-out (not a linear ramp) out of a follow-through and into / out of emotes.
            else { actionWeight = Mathf.SmoothDamp(actionWeight, want, ref actionWeightVel, want > 0 ? rise * .55f : .1f, Mathf.Infinity, dt); if (Mathf.Abs(actionWeight - want) < .002f) actionWeight = want; }
            // Legs: a stroke taken on the move keeps the run legs (planted cycle) and swings only the upper body;
            // standing, the stroke's own footwork plants. Legs blend slower than arms so nothing snaps.
            // Score80 A: the leg source follows the ground speed quickly (0.5 -> 1.8 m/s over 0.14 s) so a
            // follow-through while the body already runs back to the middle is carried by running legs, and a
            // swing into a stop gets the stroke's own planted footwork (no skate either way).
            // The tracked overhead meets a standing smash contact. Finish blending
            // out of running legs before impact, including the smash's hip/torso
            // contribution; an upper-body-only smash leaves the racket short.
            bool plantingOverhead = action == Clip.Smash && actor.Guiding && Mathf.Abs(actor.SignedTimeToContact) < .18f;
            bool strokeLegs = actionActive && IsStroke(action) && action != Clip.Serve && !plantingOverhead && !FluidOff;
            legsFromRun = Mathf.MoveTowards(legsFromRun, strokeLegs ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 1.8f, move)) : 0, dt / .14f);
            actionLegs = FluidOff ? actionWeight : Mathf.MoveTowards(actionLegs, want, dt / (want > 0 ? .16f : .2f));
            float fullAction = FluidOff ? actionWeight : Mathf.Min(actionWeight, actionLegs) * (1 - legsFromRun);
            if (fullAction > 0)
            {
                for (int k = 0; k < Count; k++) target[k] *= 1 - fullAction;
                if (actionActive) target[(int)action] += fullAction; else if (juiceActive) target[(int)juice] += fullAction;
            }
            float upperW = FluidOff || !actionActive || !IsStroke(action) ? 0 : actionWeight * Mathf.Max(legsFromRun, 1 - Mathf.Min(1, actionLegs / Mathf.Max(.01f, actionWeight)));
            // ---- advance clip times and blend
            float runRate = Mathf.Clamp(move / runClipSpeed, .25f, 1.6f);
            float fwdSign = v.y < -.05f && !Baseline ? -1 : 1;   // backing away from the net: forward run in reverse
            for (int k = 0; k < Count; k++)
            {
                if (!valid[k]) continue;
                weight[k] = FluidOff ? Mathf.MoveTowards(weight[k], target[k], dt / .12f)
                    : Mathf.Abs(weight[k] - target[k]) < .002f ? target[k] : Mathf.SmoothDamp(weight[k], target[k], ref weightVel[k], .06f, Mathf.Infinity, dt);   // eased cross-fades
                var id = (Clip)k;
                if (id == Clip.Ready || id == Clip.Idle) time[k] = (time[k] + dt) % length[k];
                else if (id == Clip.RunForward) time[k] = Mathf.Repeat((float)time[k] + dt * runRate * fwdSign, length[k]);
                else if (id == Clip.RunRight || id == Clip.RunLeft) time[k] = (time[k] + dt * runRate) % length[k];
                else if (id == Clip.Walk) time[k] = (time[k] + dt * Mathf.Clamp(move / walkClipSpeed, .3f, 1.8f)) % length[k];
                else if (actionActive && id == action) { time[k] = actionTime; if (upperPlayables[k].IsValid()) upperPlayables[k].SetTime(actionTime); }
                else if (juiceActive && id == juice) time[k] = Math.Max(0, juiceTime);
                playables[k].SetTime(time[k]);
            }
            // Humanoid mixers fill any missing weight with the default (muscle-zero) pose, which
            // drops the hips toward the ground: the blend must always sum to exactly 1.
            float total = 0; for (int k = 0; k < Count; k++) if (valid[k]) total += weight[k];
            if (total < 1e-4f) { weight[(int)Clip.Ready] = 1; total = 1; }
            if (!Baseline) for (int k = 0; k < Count; k++) if (valid[k]) weight[k] /= total;
            for (int k = 0; k < Count; k++) if (valid[k]) mixer.SetInputWeight(k, weight[k]);
            for (int k = 0; k < Count; k++) if (upperPlayables[k].IsValid()) upperMixer.SetInputWeight(k, actionActive && k == (int)action ? 1 : 0);
            layers.SetInputWeight(1, upperW); UpperLayerWeight = upperW;
            WeightSum = total;
            float yawWant = ServeYawTarget();
            serveYaw = actor.Swinging && action == Clip.Serve ? yawWant : Mathf.MoveTowards(serveYaw, yawWant, dt * 120f);
            // Walking in: face the direction of travel (a person walks forward, not sideways), then turn into
            // the serve stance once planted.
            float yawTo = walkMode && move > .25f ? Mathf.Atan2(v.x, v.y) * Mathf.Rad2Deg : 0;
            walkYaw = Mathf.MoveTowardsAngle(walkYaw, yawTo, dt * 420f);
            transform.localRotation = Quaternion.Euler(0, serveYaw + walkYaw, 0);
            graph.Evaluate(0);
            PostProcess(swinging);
            if (!Baseline && !(isPlayer && game && game.DiveActive)) LockFeet(dt);
            MeasureSkate(dt);
            FinalArmPenetration = FinalArms();
            if (trail && trail.emitting && (trailFor -= Time.deltaTime) <= 0) trail.emitting = false;
            FinalRacketPenetration = RacketCost(out finalRacketPart);
            MeasureServeBall();
            FinalSink = 0; for (int f = 0; f < 4; f++) if (feet[f]) FinalSink = Mathf.Max(FinalSink, -(transform.InverseTransformPoint(feet[f].position).y - (f % 2 == 0 ? heelRest : toeRest)));
        }
        /// Deepest sole below the court on the pose actually rendered this frame.
        public float FinalSink { get; private set; }
        // ---- foot plant lock: a foot the clip has on the ground stays put in world space (leg IK)
        readonly bool[] locked = new bool[2]; readonly Vector3[] lockPos = new Vector3[2]; readonly float[] lockW = new float[2];
        readonly bool[] stepping = new bool[2]; readonly float[] stepT = new float[2]; readonly Vector3[] stepFrom = new Vector3[2];
        const float StepTime = .12f, StepDrift = .2f;
        public int StepsTaken { get; private set; }
        void LockFeet(float dt)
        {
            var a = look.animator;
            for (int f = 0; f < 2; f++)
            {
                var upper = a.GetBoneTransform(f == 0 ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                var lower = a.GetBoneTransform(f == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                var foot = feet[f * 2]; var toe = feet[f * 2 + 1]; if (!upper || !lower || !foot || !toe) continue;
                float h = Mathf.Min(transform.InverseTransformPoint(foot.position).y - heelRest, transform.InverseTransformPoint(toe.position).y - toeRest);
                float legLen = Vector3.Distance(upper.position, lower.position) + Vector3.Distance(lower.position, foot.position);
                // Score80 A: the serve's grounded frames plant too (the stance unwinds on the feet instead of sliding
                // them round the root); the jump itself still lifts them off (h > 4.5 cm releases).
                bool plantedNow = h < .02f && (!FluidOff || !(actor.Swinging && actor.Kind == TennisActor.Stroke.Serve));
                // Score80 A: a planted foot the body has moved away from is picked up and re-planted (a quick
                // arced step), never dragged back along the court -- run -> plant -> pivot instead of skate.
                if (stepping[f] && !FluidOff)
                {
                    stepT[f] += dt / StepTime;
                    float u = Mathf.SmoothStep(0, 1, Mathf.Clamp01(stepT[f]));
                    var goal = Vector3.Lerp(stepFrom[f], new Vector3(foot.position.x, stepFrom[f].y, foot.position.z), u);
                    goal.y = foot.position.y + Mathf.Sin(Mathf.PI * Mathf.Clamp01(stepT[f])) * .065f;
                    var fr = foot.rotation; TwoBone(upper, lower, foot, goal); foot.rotation = fr;
                    if (stepT[f] >= 1) { stepping[f] = false; locked[f] = plantedNow; lockPos[f] = foot.position; lockW[f] = plantedNow ? 1 : 0; StepsTaken++; }
                    continue;
                }
                if (!locked[f] && plantedNow) { locked[f] = true; lockPos[f] = foot.position; }
                float slip = !FluidOff && actionActive && IsStroke(action) ? .6f : .35f;   // Plan 3A: plant through the stroke (stop-into-swing)
                float drift = Vector3.Distance(Flat(foot.position), Flat(lockPos[f]));
                if (!FluidOff && locked[f] && plantedNow && lockW[f] > .9f && drift > StepDrift && !stepping[1 - f] && dt > 0
                    && Vector3.Distance(upper.position, foot.position) < legLen * .985f)
                {
                    stepping[f] = true; stepT[f] = 0; stepFrom[f] = lockPos[f]; locked[f] = false; lockW[f] = 0;
                    var fr = foot.rotation; TwoBone(upper, lower, foot, lockPos[f]); foot.rotation = fr;   // this frame still on the old spot
                    continue;
                }
                if (locked[f] && (h > .045f || Vector3.Distance(upper.position, lockPos[f]) > legLen * .985f || drift > slip)) locked[f] = false;
                lockW[f] = Mathf.MoveTowards(lockW[f], locked[f] ? 1 : 0, dt / (locked[f] ? .05f : .1f));
                if (lockW[f] <= 0) continue;
                var target = Vector3.Lerp(foot.position, new Vector3(lockPos[f].x, foot.position.y, lockPos[f].z), lockW[f]);
                var footRot = foot.rotation;
                TwoBone(upper, lower, foot, target);
                foot.rotation = footRot;
            }
        }
        static float ClearScale(Func<float, float> cost, float baseCost)
        {
            for (float sc = 1; sc > 0; sc -= .1f) if (cost(sc) <= baseCost + .005f) return sc;
            return 0;
        }
        /// Elbow position a two-bone solve would give for `target` (pole from the current elbow), without applying it.
        static Vector3 ElbowFor(Transform up, Transform lo, Transform end, Vector3 target)
        {
            float l1 = Vector3.Distance(up.position, lo.position), l2 = Vector3.Distance(lo.position, end.position);
            var toT = target - up.position; float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            var dir = toT.normalized; var pole = Vector3.ProjectOnPlane(lo.position - up.position, dir).normalized;
            float along = (l1 * l1 - l2 * l2 + d * d) / (2 * d), hh = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            return up.position + dir * along + pole * hh;
        }
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        static void TwoBone(Transform up, Transform lo, Transform end, Vector3 target)
        {
            float l1 = Vector3.Distance(up.position, lo.position), l2 = Vector3.Distance(lo.position, end.position);
            var toT = target - up.position; float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            var dir = toT.normalized; var pole = Vector3.ProjectOnPlane(lo.position - up.position, dir).normalized;
            if (pole.sqrMagnitude < 1e-6f) return;
            float along = (l1 * l1 - l2 * l2 + d * d) / (2 * d), hh = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            var knee = up.position + dir * along + pole * hh;
            up.rotation = Quaternion.FromToRotation(lo.position - up.position, knee - up.position) * up.rotation;
            lo.rotation = Quaternion.FromToRotation(end.position - lo.position, up.position + dir * d - lo.position) * lo.rotation;
        }
        readonly Vector3[] lastToe = new Vector3[2];
        /// Fastest planted-toe ground slide this frame (m/s). 0 = feet locked to the court.
        public float FootSkate { get; private set; }
        void MeasureSkate(float dt)
        {
            FootSkate = 0; if (dt <= 0) return;
            for (int f = 0; f < 2; f++)
            {
                var toe = feet[f * 2 + 1]; if (!toe) continue; var p = toe.position;
                float h = transform.InverseTransformPoint(p).y - toeRest;
                var d = p - lastToe[f]; d.y = 0;
                if (h < .015f && lastToe[f] != Vector3.zero) FootSkate = Mathf.Max(FootSkate, d.magnitude / dt);
                lastToe[f] = p;
            }
        }

        Vector3 lungeOffset;
        void PostProcess(bool swinging)
        {
            // Ease any contact lunge back under the actor (feet re-centre over ~0.25 s).
            if (lungeOffset != Vector3.zero && !(swinging && IsStroke(action) && Mathf.Abs(actor.SignedTimeToContact) < .16f))
            { lungeOffset = Vector3.MoveTowards(lungeOffset, Vector3.zero, Time.deltaTime * 1.2f); }
            transform.localPosition = lungeOffset;
            if (!FluidOff) { BodyMotion(swinging); ReceivingStance(Time.deltaTime); }
            Ground();
            // ---- hands: grip roll (eastern FH / continental volley), left-hand grip where it holds something
            int roll = -1; float rollW = 0;
            if (actionActive && action == Clip.Forehand) { roll = (int)ModularHeroLook.Stroke.Forehand; rollW = actionWeight; }
            if (actionActive && action == Clip.Volley) { roll = (int)ModularHeroLook.Stroke.Volley; rollW = actionWeight; }
            bool fist = juiceActive && (juice == Clip.HitPerfect || juice == Clip.CelebratePoint || juice == Clip.MatchWin);
            bool leftGrip = weight[(int)Clip.Ready] > .5f || (actionActive && action == Clip.Backhand) || fist;
            look.ApplyHands(roll, rollW, leftGrip);
            if (cosmetics) cosmetics.SetCelebrating(juiceActive && (juice == Clip.CelebratePoint || juice == Clip.MatchWin) && actionWeight > .5f);
            // One additive dive pose, also evaluated on contact substeps. Gear follows the same skeleton.
            if (isPlayer && game && game.DiveActive) {
                float d = game.DivePose;
                transform.localRotation = Quaternion.Euler(0, serveYaw + walkYaw, -game.DiveSide * 58f * d);
                Ground();
            } else transform.localRotation = Quaternion.Euler(0, serveYaw + walkYaw, 0);
            if (swinging && IsStroke(action)) AssistContact();
            if (!Baseline) TossArm();
            ClearArm(true); ClearArm(false);
            ClearRacket(swinging);

        }

        /// Racket through the body (serve charge into the stomach, forehand wrap through the face...):
        /// rotate the racket arm at the shoulder, then at the wrist, by the smallest angle that takes the
        /// racket and forearm out of the torso, head and thighs. A two-handed grip carries the top hand
        /// along the handle. Never inside the contact window, so the strings still meet the ball.
        public float RacketBeforeClear { get; private set; }
        public float FinalRacketPenetration { get; private set; }
        string finalRacketPart; public string FinalRacketPart => finalRacketPart;
        static readonly int[] ClearDegs = { 6, -6, 12, -12, 18, -18, 26, -26, 34, -34, 44, -44, 56, -56 };
        void ClearRacket(bool swinging)
        {
            RacketBeforeClear = RacketCost(out _);
            if (Baseline || body == null) return;
            if (swinging && IsStroke(action) && Mathf.Abs(actor.SignedTimeToContact) < (FluidOff ? .1f : .05f)) return;   // strings stay on the ball at the hit
            var a = look.animator; var grip = look.racketGrip;
            Transform up = a.GetBoneTransform(HumanBodyBones.RightUpperArm), lo = a.GetBoneTransform(HumanBodyBones.RightLowerArm), hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lu = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), ll = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), lh = a.GetBoneTransform(HumanBodyBones.LeftHand);
            bool twoHand = actionActive && action == Clip.Backhand && actionWeight > .5f;
            Vector3 lhOnGrip = grip.InverseTransformPoint(lh.position); Quaternion lhRot = Quaternion.Inverse(grip.rotation) * lh.rotation;
            float LeftCost()
            {
                if (!twoHand) return 0;
                Quaternion u0 = lu.rotation, l0 = ll.rotation;
                TwoBone(lu, ll, lh, grip.TransformPoint(lhOnGrip));
                float c = ArmCost(lu.position, ll.position, lh.position, (lh.rotation * handAxis[0]).normalized);
                lu.rotation = u0; ll.rotation = l0; return c;
            }
            float Cost(float deg) => RacketCost(out _, .02f, .06f) + ArmCost(up.position, lo.position, hand.position, (hand.rotation * handAxis[1]).normalized) + LeftCost() + Mathf.Abs(deg) * .0006f;
            if (Cost(0) < .004f) return;
            // stage 1: whole arm about the shoulder; stage 2: the hand about the wrist
            foreach (var stage in new[] { 0, 1 })
            {
                var bone = stage == 0 ? up : hand; var q0 = bone.rotation;
                float best = Cost(0); Quaternion bestQ = q0;
                var axes = stage == 0 ? new[] { transform.up, transform.forward, transform.right } : new[] { hand.right, hand.forward, hand.up };
                foreach (var ax in axes)
                    foreach (var d in ClearDegs)
                    {
                        bone.rotation = Quaternion.AngleAxis(d, ax) * q0;
                        float c = Cost(d); if (c < best - 1e-4f) { best = c; bestQ = bone.rotation; }
                    }
                bone.rotation = bestQ;
                if (Cost(0) < .004f) break;
            }
            if (twoHand) { TwoBone(lu, ll, lh, grip.TransformPoint(lhOnGrip)); lh.rotation = grip.rotation * lhRot; }
        }

        /// Serve ritual proof: how far the bounced / held ball stays outside the body (torso volume and
        /// leg capsules), in metres; negative = through the body. Only while this hero is serving.
        public float ServeBallClearance { get; private set; } = 1;
        void MeasureServeBall()
        {
            ServeBallClearance = 1;
            if (!game || !actor.PrepareServe) return;
            bool mine = (actor == game.Player && (game.Flow == TennisGame.Phase.PlayerServeHold || game.Flow == TennisGame.Phase.PlayerServeToss))
                     || (actor == game.Opponent && game.Flow == TennisGame.Phase.OpponentServe);
            if (!mine) return;
            var b = game.BallPosition; const float br = .033f;
            float c = -Penetration(b, br);
            if (c >= 0)
            {
                c = 1; var a = look.animator;
                foreach (var side in new[] { true, false })
                {
                    var u = a.GetBoneTransform(side ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
                    var k = a.GetBoneTransform(side ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                    var f = a.GetBoneTransform(side ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                    c = Mathf.Min(c, SegDist(b, u.position, k.position) - .085f - br, SegDist(b, k.position, f.position) - .065f - br);
                }
            }
            ServeBallClearance = c;
        }
        static float SegDist(Vector3 p, Vector3 a, Vector3 b) { var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return Vector3.Distance(p, a + ab * t); }

        // ---- serve ritual: the tossing hand really bounces and tosses the ball
        const float ServeStanceT = .6f, ServeReleaseT = 1.45f, ServeTrophyT = 1.80f;
        float swingStartTtc = .3f;
        const float SwingEase = 1.75f, ReleaseBurst = 1.5f, ReleaseTau = .09f;
        /// Clip time for a stroke at signed time-to-contact `ttc`: contact-locked. The serve's swing
        /// runs the clip's whole trophy -> racket drop -> unwind -> contact section (1.80 -> 2.33 s)
        /// over however long the gameplay swing takes to reach the ball, so hips lead, the torso
        /// uncoils and the arm comes last (a straight contact-minus-ttc map started it 0.15 s before
        /// contact: an arm-only chop). Contact frame and follow-through are unchanged.
        float StrokeTime(int i, float ttc, bool burst = true)
        {
            float sample = BaseStrokeTime(i, ttc, burst);
            float earliest = i == (int)Clip.Serve && !Baseline && ttc > 0 ? ServeTrophyT : 0;
            return Mathf.Clamp(contact[i] + (sample - contact[i]) * AnimationTempo, earliest, length[i]);
        }
        float BaseStrokeTime(int i, float ttc, bool burst = true)
        {
            if (i == (int)Clip.Serve && !Baseline && ttc > 0)
            {
                float u0 = Mathf.Clamp01(1 - ttc / swingStartTtc);
                return Mathf.Lerp(ServeTrophyT, contact[i], FluidOff ? u0 : Mathf.Pow(u0, 1.35f));
            }
            // Plan 3A: slow-in out of the takeback, fast-out through contact (the clip's own timing, re-eased so
            // the racket is fastest AT the ball). Same start pose, same contact frame -> hit sync is unchanged.
            // Score80 A/B: the approved clips square the strings AT contact where the racket is slow (the fast part of the
            // arm path is ~0.1 s earlier, edge-on), so the curve is steeper into the ball (u^1.75: clip rate ~2.1x at
            // contact) and releases with a short burst after it (+1.5x, 90 ms decay): hit-stop, then the racket snaps
            // out through the ball into the follow-through. The contact instant itself is unchanged (honest sync).
            if (!FluidOff && ttc > 0 && swingStartTtc > .08f && ttc <= swingStartTtc)
            {
                float u = 1 - ttc / swingStartTtc;
                return Mathf.Clamp(contact[i] - swingStartTtc * (1 - Mathf.Pow(u, SwingEase)), 0, length[i]);
            }
            if (!FluidOff && burst && ttc < 0) return Mathf.Clamp(contact[i] - ttc + ReleaseBurst * ReleaseTau * (1 - Mathf.Exp(ttc / ReleaseTau)), 0, length[i]);
            return Mathf.Clamp(contact[i] - ttc, 0, length[i]);
        }
        /// Serve stance yaw (deg) on the hero root: the clip's stance is turned ~130 deg (back to the
        /// camera-side fence); side-on to the baseline is ~90. Held through the ritual and trophy, then
        /// unwound to zero exactly at contact (so the contact model and honest hit are unchanged).
        float serveYaw;
        float ServeYawTarget()
        {
            if (Baseline || !actionActive || action != Clip.Serve) return 0;
            if (!actor.Swinging) return Mathf.Lerp(-38f, -30f, Mathf.Clamp01(actor.PrepareAmount));
            float ttc = actor.SignedTimeToContact; if (ttc <= 0) return 0;
            float u = Mathf.Clamp01(1 - ttc / swingStartTtc); return -30f * (1 - u * u * (3 - 2 * u));
        }
        // Ball points in hero space for the serve clip's side-on stance (chest faces +x, front foot toward
        // the net): bounced out in front of the front hip, clear of the belly, legs and the racket head
        // held across the body, then lifted and released from the hand.
        static readonly Vector3[] TossPoints = { new Vector3(.26f, .84f, .30f), new Vector3(.36f, 1.28f, .05f), new Vector3(.40f, 1.58f, .03f) };
        Vector3? VisualTossPoint(int k) => k == 0 && Mathf.Abs(walkYaw) > 1f
            ? Quaternion.Euler(0, walkYaw, 0) * CarryPoint   // walking in: ball carried low at the tossing-hand side
            : Quaternion.Euler(0, serveYaw + walkYaw, 0) * TossPoints[Mathf.Clamp(k, 0, 2)];
        static readonly Vector3 CarryPoint = new Vector3(-.3f, .66f, .08f);
        float tossWeight; Transform leftMiddle;
        void TossArm()
        {
            float want = actor.PrepareServe || (actionActive && action == Clip.Serve && actor.Swinging) ? actor.TossReachWanted : 0;
            tossWeight = Mathf.MoveTowards(tossWeight, want, Time.deltaTime / .12f);
            if (tossWeight <= .001f) return;
            var a = look.animator;
            Transform up = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), lo = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(HumanBodyBones.LeftHand);
            if (!leftMiddle) foreach (var t in hand.GetComponentsInChildren<Transform>()) if (t.name == "Middle1.L") { leftMiddle = t; break; }
            // The routine's palm target is the ball centre while in hand: the fingers go over the ball while
            // pushing and catching bounces, under it once the wind-up lifts it and through the toss.
            Vector3 ball = actor.TossReachTarget;
            bool bouncing = actor.PrepareAmount <= .001f;
            Vector3 palm = leftMiddle ? Vector3.Lerp(hand.position, leftMiddle.position, .7f) : hand.position;
            Vector3 goal = ball + Vector3.up * (bouncing ? .045f : -.045f);
            var hr = hand.rotation;
            TwoBone(up, lo, hand, Vector3.Lerp(hand.position, hand.position + (goal - palm), tossWeight));
            hand.rotation = hr;
        }

        // ================================================================ Plan 3A body motion
        float chainW, breatheT, squash, coil; Vector3 baseScale = Vector3.zero;
        public float ChainWeight => chainW;
        public float ActionWeight => actionWeight;
        void BodyMotion(bool swinging)
        {
            float dt = Time.deltaTime, udt = Time.timeScale > 0 ? dt : (Time.captureFramerate > 0 ? 1f / Time.captureFramerate : Time.unscaledDeltaTime);
            int i = (int)action;
            bool stroke = actionActive && IsStroke(action) && hipsTable[i] != null;
            chainW = Mathf.MoveTowards(chainW, stroke ? actionWeight : 0, dt / .1f);
            if (chainW > .001f && stroke)
            {
                // hips lead the chest, the chest leads the arm: sample the segments ahead in the clip
                float t = (float)actionTime;
                // Score80 A: a wider hips -> chest -> arm stagger on groundstrokes (hips 110 ms ahead, chest 55 ms)
                // and a bigger hip lead (x1.3), so the pelvis visibly fires first and the chest follows it.
                float dh = action == Clip.Serve ? .08f : action == Clip.Volley ? .05f : .14f;
                float dc = action == Clip.Serve ? .04f : action == Clip.Volley ? .025f : .08f;
                float hipGain = action == Clip.Volley ? 1.1f : 1.3f;
                var rot = transform.rotation;
                // The lead is in REAL time on the live swing: the hips show the pose the clip reaches dh seconds from now
                // on the eased, contact-locked schedule (a clip-time lead collapses where the swing is fastest).
                float th = t + dh * chainW, tc = t + dc * chainW;
                if (swinging && actor.Swinging) { float ttc = actor.SignedTimeToContact; th = StrokeTime(i, ttc - dh * chainW, false); tc = StrokeTime(i, ttc - dc * chainW, false); }
                else if (followThrough) { th = t + dh * chainW * AnimationTempo; tc = t + dc * chainW * AnimationTempo; }
                var hipLead = rot * (SampleTable(hipsTable[i], th) * Quaternion.Inverse(SampleTable(hipsTable[i], t))) * Quaternion.Inverse(rot);
                var chestLead = rot * (SampleTable(chestTable[i], tc) * Quaternion.Inverse(SampleTable(chestTable[i], t))) * Quaternion.Inverse(rot);
                var a = look.animator;
                Transform ua = a.GetBoneTransform(HumanBodyBones.RightUpperArm), la = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Quaternion spine0 = spineB.rotation, ua0 = ua.rotation, la0 = la.rotation, neck0 = neckB ? neckB.rotation : Quaternion.identity;
                float rk0 = RacketCost(out _);
                Quaternion hips0 = hips.rotation, chest0 = chestB.rotation;
                // the pelvis turns ON the feet: grounded feet are pinned and the legs re-solved after the turn
                var a2 = look.animator; Transform lf = feet[0], rf = feet[2];
                Vector3 lfp = lf.position, rfp = rf.position; Quaternion lfr = lf.rotation, rfr = rf.rotation;
                bool lfDown = transform.InverseTransformPoint(lf.position).y - heelRest < .03f, rfDown = transform.InverseTransformPoint(rf.position).y - heelRest < .03f;
                for (float k = 1; k >= 0; k -= .5f)   // back the lead off if it would turn the torso into the racket
                {
                    hips.rotation = hips0; spineB.rotation = spine0; chestB.rotation = chest0; ua.rotation = ua0; la.rotation = la0;
                    var hl = Quaternion.SlerpUnclamped(Quaternion.identity, hipLead, k * hipGain); var cl = Quaternion.Slerp(Quaternion.identity, chestLead, k);
                    hips.rotation = hl * hips.rotation;                                            // hips turn first...
                    spineB.rotation = Quaternion.Slerp(spine0, hl * spine0, .3f);                  // ...the waist lags them...
                    chestB.rotation = cl * Quaternion.Inverse(hl) * Quaternion.Slerp(Quaternion.identity, hl, .3f) * chestB.rotation; // ...the chest leads the arm
                    ua.rotation = ua0; la.rotation = la0;                                          // the arms (racket) stay on the clip's time
                    if (k <= 0 || RacketCost(out _) <= rk0 + .004f) break;
                }
                if (neckB) neckB.rotation = Quaternion.Slerp(neck0, neckB.rotation, .5f);
                if (lfDown) { TwoBone(a2.GetBoneTransform(HumanBodyBones.LeftUpperLeg), a2.GetBoneTransform(HumanBodyBones.LeftLowerLeg), lf, lfp); lf.rotation = Quaternion.Slerp(lfr, lf.rotation, .35f); }
                if (rfDown) { TwoBone(a2.GetBoneTransform(HumanBodyBones.RightUpperLeg), a2.GetBoneTransform(HumanBodyBones.RightLowerLeg), rf, rfp); rf.rotation = Quaternion.Slerp(rfr, rf.rotation, .35f); }
            }
            // serve / smash: unlock the spine (bigger arch into the trophy, crunch through contact)
            bool overhead = actionActive && (action == Clip.Serve || action == Clip.Smash);
            if (overhead && actionWeight > .2f)
            {
                float amp = 1 + .45f * actionWeight;
                spineB.localRotation = Quaternion.SlerpUnclamped(spineBindLocal, spineB.localRotation, amp);
                chestB.localRotation = Quaternion.SlerpUnclamped(chestBindLocal, chestB.localRotation, amp);
            }
            // ULTIMATE charge (world frozen): coil up — hips counter-turn, chest wound back, spine arched, breathing pulse
            var j = game ? game.Juice : null;
            bool charging = j && j.UltimateActive && j.ChargeSubject == actor.transform;
            coil = Mathf.MoveTowards(coil, charging ? 1 : 0, udt / (charging ? .35f : .06f));
            if (coil > .001f)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * 9) * .5f + .5f;
                var up = transform.up; var side = transform.right;
                float dir = actor.Backhand ? -1 : 1;
                hips.rotation = Quaternion.AngleAxis(-14 * dir * coil, up) * hips.rotation;
                spineB.rotation = Quaternion.AngleAxis(-10 * dir * coil, up) * Quaternion.AngleAxis(-6 * coil, side) * spineB.rotation;
                chestB.rotation = Quaternion.AngleAxis(-18 * dir * coil, up) * Quaternion.AngleAxis((-8 - 3 * pulse) * coil, side) * chestB.rotation;
            }
            // squash & stretch on the root: stretch into an overhead contact / the charge, squash on landing
            float target = 0;
            if (overhead && swinging) { float ttc = actor.SignedTimeToContact; target = ttc > 0 ? Mathf.Clamp01(1 - ttc / .25f) : -Mathf.Clamp01(1 + ttc / .25f) * Mathf.Clamp01(-ttc / .08f); }
            if (coil > 0) target = Mathf.Max(target, .6f * coil + .25f * Mathf.Sin(Time.unscaledTime * 9) * coil);
            squash = Mathf.MoveTowards(squash, target, udt * 6);
            if (baseScale == Vector3.zero) baseScale = transform.localScale;
            float st = .045f * squash - .06f * impactSquash * Mathf.Sin(Mathf.Clamp01(impactSquash) * Mathf.PI); transform.localScale = new Vector3(baseScale.x * (1 - st * .5f), baseScale.y * (1 + st), baseScale.z * (1 - st * .5f));
            // ready breathe
            float readyW = weight[(int)Clip.Ready] * (1 - actionWeight);
            breatheT += dt;
            if (readyW > .05f)
            {
                float b = Mathf.Sin(breatheT * 2 * Mathf.PI * .42f);
                chestB.rotation = Quaternion.AngleAxis(-2.2f * b * readyW, transform.right) * chestB.rotation;
                hips.position += transform.up * (.008f * b * readyW);
            }
            PlantAndSettle(swinging, charging);
            UpdateFace(swinging, charging);
        }

        // ================================================================ Score80 A: plant + overshoot / settle
        // Weight: the body leans into its acceleration (braces back into a stop, drives forward out of one) and the
        // knees absorb a hard stop with a small underdamped dip, so run -> plant -> swing reads heavy. The chest and
        // neck ride a soft spring outside the swing window: a follow-through or a recovery that stops fast carries on
        // a few degrees and settles back (no linear, robotic stop). Never inside the swing-to-contact window, so the
        // strings stay on the authored contact.
        Vector2 velPrev, accSm, velDir = Vector2.up; float dipPos, dipVel, settleW, leanW; int springFrame = -1;
        Quaternion chestSpring, chestPrevT, neckSpring, neckPrevT; Vector3 chestOmega, neckOmega, pendingKick; bool springInit;
        public float PlantDip => dipPos;
        public float TorsoOvershoot { get; private set; }
        public float LeanDegrees { get; private set; }
        /// Queue an angular kick (rad/s, hero frame: -x = pitch back, y = yaw) into the chest spring, a root squash
        /// and a boost of the motion already carried; it fires as soon as the spring owns the chest (after the
        /// contact window / hit-stop), so a heavy hit freezes, then the body takes the recoil.
        public void KickTorso(Vector3 heroOmega, float squashAmount = 0, float carryBoost = 1) { pendingKick += heroOmega; pendingSquash = Mathf.Max(pendingSquash, squashAmount); pendingBoost = Mathf.Max(pendingBoost, carryBoost); kickQueued = true; }
        float pendingSquash, pendingBoost = 1, impactSquash; bool kickQueued;
        public int KicksFired { get; private set; }
        void PlantAndSettle(bool swinging, bool charging)
        {
            if (!spineB || !chestB) return;
            float dt = Time.deltaTime; bool newFrame = springFrame != Time.frameCount; springFrame = Time.frameCount;
            bool serving = actionActive && action == Clip.Serve;
            bool contactWindow = swinging && IsStroke(action) && actor.SignedTimeToContact > -.07f;
            if (newFrame && dt > 0)
            {
                var v = new Vector2(actor.Speed, actor.ForwardSpeed);
                var acc = (v - velPrev) / dt; velPrev = v;
                if (acc.sqrMagnitude > 900) acc = Vector2.zero;   // teleports between points
                accSm = Vector2.Lerp(accSm, acc, 1 - Mathf.Exp(-dt / .07f));
                if (v.sqrMagnitude > .25f) velDir = Vector2.Lerp(velDir, v.normalized, 1 - Mathf.Exp(-dt / .15f)).normalized;
                float decel = Mathf.Max(0, -Vector2.Dot(accSm, velDir));
                float dipWant = serving || charging ? 0 : Mathf.Clamp(decel * .0038f, 0, .05f);
                const float wd = 2 * Mathf.PI * 3.4f;
                dipVel += (wd * wd * (dipWant - dipPos) - 2 * .45f * wd * dipVel) * dt; dipPos = Mathf.Clamp(dipPos + dipVel * dt, -.015f, .065f);
                leanW = Mathf.MoveTowards(leanW, serving || charging || (game && game.DiveActive && isPlayer) ? 0 : contactWindow && Mathf.Abs(actor.SignedTimeToContact) < .1f ? .45f : 1, dt / .12f);
                // chest / neck spring weight: off through the swing to contact, full in follow-throughs, recoveries and
                // emotes, light on runs (the run cycle is authored and near the spring frequency)
                float wantW = contactWindow || charging || coil > .01f ? 0 : actionActive || juiceActive ? 1 : Moving() > 1.2f ? .25f : .7f;
                settleW = Mathf.MoveTowards(settleW, wantW, dt / (wantW > settleW ? .06f : .04f));
            }
            // lean into the acceleration: 80% at the waist, 20% at the pelvis (the planted feet are re-locked after)
            var accW = (actor ? actor.transform : transform).TransformDirection(new Vector3(accSm.x, 0, accSm.y));
            float lean = Mathf.Min(accW.magnitude * .75f, 8.5f) * leanW; LeanDegrees = lean;
            if (lean > .05f)
            {
                var axis = Vector3.Cross(transform.up, accW.normalized);
                hips.rotation = Quaternion.AngleAxis(lean * .2f, axis) * hips.rotation;
                spineB.rotation = Quaternion.AngleAxis(lean * .8f, axis) * spineB.rotation;
            }
            if (dipPos > .002f) Crouch(dipPos);
            // chest + neck spring
            var chestT = chestB.localRotation; var neckT = neckB ? neckB.localRotation : Quaternion.identity;
            if (!springInit) { chestSpring = chestPrevT = chestT; neckSpring = neckPrevT = neckT; springInit = true; }
            if (newFrame && dt > 0)
            {
                bool live = settleW > .01f;
                if (live && kickQueued)
                {
                    var pk = Quaternion.Inverse(spineB.rotation) * (transform.right * pendingKick.x + transform.up * pendingKick.y + transform.forward * pendingKick.z);
                    chestOmega = chestOmega * pendingBoost + pk; neckOmega = neckOmega * pendingBoost + pk * .6f;
                    impactSquash = Mathf.Max(impactSquash, pendingSquash);
                    pendingKick = Vector3.zero; pendingSquash = 0; pendingBoost = 1; kickQueued = false; KicksFired++;
                }
                impactSquash = Mathf.MoveTowards(impactSquash, 0, dt / .2f);
                Spring(ref chestSpring, ref chestOmega, ref chestPrevT, chestT, dt, 3.1f, .4f, live);
                if (neckB) Spring(ref neckSpring, ref neckOmega, ref neckPrevT, neckT, dt, 2.6f, .5f, live);
                TorsoOvershoot = Quaternion.Angle(chestSpring, chestT) * settleW;
            }
            if (settleW > .001f)
            {
                chestB.localRotation = Quaternion.Slerp(chestT, chestSpring, settleW * .85f);
                if (neckB) neckB.localRotation = Quaternion.Slerp(neckT, neckSpring, settleW * .7f);
            }
        }
        static void Spring(ref Quaternion s, ref Vector3 omega, ref Quaternion prevT, Quaternion target, float dt, float hz, float zeta, bool live)
        {
            if (!live)
            {
                // track the animation exactly, carrying its angular velocity so the hand-over continues the motion
                (target * Quaternion.Inverse(prevT)).ToAngleAxis(out float a, out Vector3 ax); if (a > 180) a -= 360;
                omega = float.IsFinite(ax.x) ? Vector3.ClampMagnitude(ax * (a * Mathf.Deg2Rad / dt), 14f) : Vector3.zero;
                s = target; prevT = target; return;
            }
            prevT = target;
            (target * Quaternion.Inverse(s)).ToAngleAxis(out float e, out Vector3 eax); if (e > 180) e -= 360;
            if (!float.IsFinite(eax.x) || Mathf.Abs(e) < 1e-3f) eax = Vector3.up;
            if (Mathf.Abs(e) > 50) { s = Quaternion.Slerp(s, target, .5f); omega *= .5f; return; }   // a clip cut: don't flail
            float w0 = 2 * Mathf.PI * hz;
            omega += (eax * (e * Mathf.Deg2Rad) * (w0 * w0) - omega * (2 * zeta * w0)) * dt;
            omega = Vector3.ClampMagnitude(omega, 14f);
            s = Quaternion.AngleAxis(omega.magnitude * dt * Mathf.Rad2Deg, omega.sqrMagnitude > 1e-8f ? omega.normalized : Vector3.up) * s;
        }
        float stanceW, stanceClock;
        /// Waiting to return a serve: the baked Ready clip is a deep, wide defensive squat with the head
        /// down. A returner stands taller than that: knees soft, weight forward on the balls of the feet,
        /// chest slightly over the front knee, head up watching the toss, and a slow shift of weight from
        /// foot to foot. Feet stay planted; only the hips, trunk and head change.
        void ReceivingStance(float dt)
        {
            bool receiving = game && actor && ((isPlayer && game.Flow == TennisGame.Phase.OpponentServe)
                || (!isPlayer && (game.Flow == TennisGame.Phase.PlayerServeHold || game.Flow == TennisGame.Phase.PlayerServeToss))) && !actor.Swinging && !actionActive && !juiceActive && Moving() < .3f;
            stanceW = Mathf.MoveTowards(stanceW, receiving ? 1 : 0, dt / (receiving ? .35f : .18f));
            if (stanceW < .005f) return;
            stanceClock += dt;
            float w = Mathf.SmoothStep(0, 1, stanceW);
            float sway = Mathf.Sin(stanceClock * 2.6f);
            var a = look.animator;
            var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot); var rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 footL = lf.position, footR = rf.position;
            Quaternion footRotL = lf.rotation, footRotR = rf.rotation;
            Crouch(-StanceRise * w);                                            // hips up: legs straighten to soft knees
            hips.position += transform.right * (sway * .012f * w);              // weight shifts foot to foot
            hips.rotation = Quaternion.AngleAxis(sway * 2.2f * w, transform.up) * hips.rotation;
            spineB.rotation = Quaternion.AngleAxis(StanceLean * w, transform.right) * spineB.rotation;   // chest over the front knee
            if (chestB) chestB.rotation = Quaternion.AngleAxis(-StanceHead * w, transform.right) * chestB.rotation;   // head comes back up
            // Racket up and in front of the chest, both hands on it, instead of hanging at the hip. Both hands move by
            // the same offset, so the grip on the handle is kept.
            var an = look.animator;
            Transform ru = an.GetBoneTransform(HumanBodyBones.RightUpperArm), rl = an.GetBoneTransform(HumanBodyBones.RightLowerArm), rh = an.GetBoneTransform(HumanBodyBones.RightHand);
            Transform lu = an.GetBoneTransform(HumanBodyBones.LeftUpperArm), ll = an.GetBoneTransform(HumanBodyBones.LeftLowerArm), lh = an.GetBoneTransform(HumanBodyBones.LeftHand);
            if (ru && rl && rh && lu && ll && lh)
            {
                Vector3 lift = (transform.up * StanceHandsUp + transform.forward * StanceHandsForward) * w;
                Vector3 rp = rh.position + lift, lp = lh.position + lift;
                Quaternion rightGrip = rh.rotation, leftGrip = lh.rotation;
                TwoBone(ru, rl, rh, rp); rh.rotation = rightGrip;
                TwoBone(lu, ll, lh, lp); lh.rotation = leftGrip;
            }
            // Replant after the complete hip sway, not before it.
            TwoBone(a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), lf, footL); lf.rotation = footRotL;
            TwoBone(a.GetBoneTransform(HumanBodyBones.RightUpperLeg), a.GetBoneTransform(HumanBodyBones.RightLowerLeg), rf, footR); rf.rotation = footRotR;
        }
        public float StanceRise = .14f, StanceLean = 7f, StanceHead = 9f, StanceHandsUp = .17f, StanceHandsForward = .07f;

        /// Drop the hips by `drop` and re-solve both legs so the feet stay where they are (knees bend).
        void Crouch(float drop)
        {
            var a = look.animator;
            Transform lu = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), ll = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform ru = a.GetBoneTransform(HumanBodyBones.RightUpperLeg), rl = a.GetBoneTransform(HumanBodyBones.RightLowerLeg), rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 lp = lf.position, rp = rf.position; Quaternion lr = lf.rotation, rr = rf.rotation;
            hips.position += Vector3.down * drop;
            TwoBone(lu, ll, lf, lp); lf.rotation = lr; TwoBone(ru, rl, rf, rp); rf.rotation = rr;
        }
        Quaternion spineBindLocal, chestBindLocal;

        void UpdateFace(bool swinging, bool charging)
        {
            if (!face) return;
            bool over = BetweenPoints();
            var cam = Camera.main;
            if (juiceActive && juice == Clip.MissWhiff) face.mood = HeroFace.Mood.Shock;
            else if (juiceActive && (juice == Clip.CelebratePoint || juice == Clip.HitPerfect || juice == Clip.MatchWin)) face.mood = HeroFace.Mood.Happy;
            else if (juiceActive && (juice == Clip.SadPointLost || juice == Clip.MatchLose)) face.mood = HeroFace.Mood.Sad;
            else if (charging || (swinging && Mathf.Abs(actor.SignedTimeToContact) < .3f) || actor.PrepareAmount > .5f) face.mood = HeroFace.Mood.Effort;
            else face.mood = HeroFace.Mood.Neutral;
            // look: the ball in play; the camera in reaction shots / between points
            if (over && cam) { face.lookTarget = cam.transform; face.headWeight = .55f; face.lookWeight = 1; }
            else if (game) { face.lookTarget = null; face.lookPoint = game.BallPosition.y > -5 ? game.BallPosition : transform.position + transform.forward * 10 + Vector3.up; face.headWeight = swinging ? .15f : .45f; face.lookWeight = 1; }
        }

        /// Feet never below the court: lift the whole body by the deepest sole.
        void Ground()
        {
            if (Baseline) { LastGroundLift = 0; for (int f = 0; f < 4; f++) if (feet[f]) LastGroundLift = Mathf.Max(LastGroundLift, -(transform.InverseTransformPoint(feet[f].position).y - (f % 2 == 0 ? heelRest : toeRest))); return; }
            float low = float.MaxValue;
            for (int f = 0; f < 4; f++) { if (!feet[f]) continue; float h = transform.InverseTransformPoint(feet[f].position).y - (f % 2 == 0 ? heelRest : toeRest); low = Mathf.Min(low, h); }
            SinkBeforeGround = Mathf.Max(0, -low);
            if (low < -.002f && low > -.3f) { hips.position += transform.up * -low; LastGroundLift = -low; } else LastGroundLift = 0;
        }
        public float LastGroundLift { get; private set; }
        public float WeightSum { get; private set; }
        /// Walking to the serve spot (or back into position between points): walk cycle, not the run or a frozen stance.
        bool WalkMode() => !Baseline && valid[(int)Clip.Walk] && game && !actor.Swinging && (WalkingIn() || BetweenPoints()) && Moving() < 2.4f;
        bool WalkingIn() => game && actor.PrepareServe && actor.PrepareAmount <= .001f && Moving() > .15f;
        public float UpperLayerWeight { get; private set; }
        public Vector3 StringCentre => stringCentre ? stringCentre.position : transform.position;
        public float LastArmPenetration { get; private set; }
        public float ArmBeforeClear { get; private set; }
        /// Arm-in-torso depth sum for both arms on the pose actually rendered this frame.
        public float FinalArmPenetration { get; private set; }
        float FinalArms()
        {
            var a = look.animator; float c = 0;
            foreach (bool r in new[] { true, false })
            {
                Transform up = a.GetBoneTransform(r ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm), lo = a.GetBoneTransform(r ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(r ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                c = Mathf.Max(c, ArmCost(up.position, lo.position, hand.position, (hand.rotation * handAxis[r ? 1 : 0]).normalized));
            }
            return c;
        }
        public float SinkBeforeGround { get; private set; }

        // Torso volume measured from the V4 body/shirt/shorts mesh (restore_motion/tools/body_volume.json):
        // one ellipse per 4 cm slice of bind height, evaluated in each torso bone's bind frame -- the same
        // model the Blender solver used when the approved clips were cleared.
        static readonly float[] SliceZ={0.700f,0.740f,0.780f,0.820f,0.860f,0.900f,0.940f,0.980f,1.020f,1.060f,1.100f,1.140f,1.180f,1.220f};
        static readonly float[] SliceA={0.201f,0.209f,0.213f,0.218f,0.206f,0.193f,0.190f,0.187f,0.127f,0.137f,0.148f,0.164f,0.171f,0.191f};
        static readonly float[] SliceYmin={-0.069f,-0.106f,-0.145f,-0.149f,-0.141f,-0.136f,-0.130f,-0.124f,-0.122f,-0.120f,-0.112f,-0.106f,-0.095f,-0.027f};
        static readonly float[] SliceYmax={0.157f,0.180f,0.219f,0.220f,0.214f,0.204f,0.200f,0.201f,0.200f,0.198f,0.194f,0.186f,0.171f,0.130f};
        const float UpperArmR = .0915f * .85f, LowerArmR = .0595f, HandR = .0668f * .9f;
        readonly Transform[] torsoBones = new Transform[3]; readonly Vector3[] torsoBindPos = new Vector3[3]; readonly Quaternion[] torsoBindRot = new Quaternion[3];
        static readonly float[] torsoMinZ = { 1.03f, .87f, -9f };   // Chest, Spine, Hips
        void CaptureTorsoBind()
        {
            var a = look.animator; torsoBones[0] = a.GetBoneTransform(HumanBodyBones.Chest); torsoBones[1] = a.GetBoneTransform(HumanBodyBones.Spine); torsoBones[2] = hips;
            for (int i = 0; i < 3; i++) { torsoBindPos[i] = transform.InverseTransformPoint(torsoBones[i].position); torsoBindRot[i] = Quaternion.Inverse(transform.rotation) * torsoBones[i].rotation; }
        }
        float Penetration(Vector3 p, float r)
        {
            var pl = transform.InverseTransformPoint(p); Vector3 rest = pl;
            for (int i = 0; i < 3; i++)
            {
                var bl = transform.InverseTransformPoint(torsoBones[i].position); var br = Quaternion.Inverse(transform.rotation) * torsoBones[i].rotation;
                rest = torsoBindPos[i] + torsoBindRot[i] * (Quaternion.Inverse(br) * (pl - bl));
                if (rest.y >= torsoMinZ[i]) break;
            }
            float z = rest.y; if (z < SliceZ[0] || z > SliceZ[SliceZ.Length - 1]) return 0;
            int k = 0; while (k < SliceZ.Length - 2 && z > SliceZ[k + 1]) k++;
            float t = (z - SliceZ[k]) / (SliceZ[k + 1] - SliceZ[k]);
            float ax = Mathf.Lerp(SliceA[k], SliceA[k + 1], t), ymin = Mathf.Lerp(SliceYmin[k], SliceYmin[k + 1], t), ymax = Mathf.Lerp(SliceYmax[k], SliceYmax[k + 1], t);
            float cy = (ymin + ymax) / 2, by = (ymax - ymin) / 2;
            float x = rest.x, y = -rest.z - cy;            // Unity forward (+z) = Blender -y
            float s = Mathf.Sqrt(x * x / (ax * ax) + y * y / (by * by)); if (s < 1e-6f) return r + Mathf.Min(ax, by);
            return Mathf.Max(0, r + .005f - (s - 1) * Mathf.Sqrt(x * x + y * y) / s);
        }
        // ---- racket / limb clearance against torso, head and legs
        HeroBodyProxy body; public string BodyProxyInfo => body?.ToString();
        static readonly float[] RimAngles = { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 };
        /// Summed depth (m) of racket samples (shaft + head rim + strings centre) inside the body.
        public float RacketCost(out string part) => RacketCost(out part, 0, 0);
        /// With clearance margins: the solver keeps the racket `margin` off the body and `headMargin`
        /// off the head, so a finish passes beside the face instead of skimming across it.
        public float RacketCost(out string part, float margin, float headMargin)
        {
            string worstPart = null; part = null; if (!stringCentre) return 0;
            float c = 0, worst = 0;
            Vector3 ctr = stringCentre.position, right = (stringRight.position - ctr) / .1f, up = (stringUp.position - ctr) / .1f;
            Vector3 grip = look.racketGrip.position;
            void Test(Vector3 q, float r)
            {
                float d = Penetration(q, r + margin); string pt = d > 0 ? "torso" : null;
                if (body != null) { float d2 = body.Depth(q, r + margin, out var p2); if (p2 == "head") d2 = body.Depth(q, r + headMargin, out p2); if (d2 > d) { d = d2; pt = p2; } }
                c += d; if (d > worst) { worst = d; worstPart = pt; }
            }
            for (int k = 1; k <= 3; k++) Test(Vector3.Lerp(grip, ctr - up * .13f, k / 3f), .015f);   // handle + throat
            Test(ctr, .02f);
            foreach (var a in RimAngles) { float r = a * Mathf.Deg2Rad; Test(ctr + right * (Mathf.Cos(r) * .11f) + up * (Mathf.Sin(r) * .135f), .012f); }
            part = worstPart; return c;
        }
        /// Forearm / hand of either arm inside the head or legs (the torso is ClearArm's job).
        public float LimbHeadLegCost(bool right)
        {
            if (body == null) return 0; var a = look.animator;
            Transform lo = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            float c = 0;
            for (int k = 0; k <= 4; k++) c += body.Depth(Vector3.Lerp(lo.position, hand.position, k / 4f), LowerArmR, out _);
            return c;
        }
        /// Average colour of the hair texture, shadowed a touch (the scalp sits under the hair).
        Color HairTone()
        {
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.name.StartsWith("Hair")) continue; var m = r.sharedMaterial; if (!m) continue;
                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                Color tint = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                if (!tex) return tint * .8f;
                var rt = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(tex, rt); var prev = RenderTexture.active; RenderTexture.active = rt;
                var t = new Texture2D(8, 8, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, 8, 8), 0, 0); t.Apply();
                RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
                Color sum = Color.black; foreach (var c in t.GetPixels()) sum += c; Destroy(t);
                sum /= 64f; var avg = new Color(sum.r * tint.r, sum.g * tint.g, sum.b * tint.b, 1);
                return new Color(avg.r * .82f, avg.g * .8f, avg.b * .78f, 1);
            }
            return new Color(0, 0, 0, 0);
        }
        /// The body mesh carries its own baked side-hair layer (skin_BlondHair submesh) under the hair shell. Its
        /// texture is patchy skin, so around the ears it read as holes through the head. Draw it with the hair
        /// shell's own material: the ears and nape read as one continuous head of hair from every angle.
        void SealHeadUnderHair()
        {
            Material hair = null;
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.name == "Hair_Default" && r.sharedMaterial) { hair = r.sharedMaterial; break; }
            if (!hair) return;
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != "Body_Skin") continue; var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name.StartsWith("skin_BlondHair")) { mats[i] = new Material(hair) { name = "Hero side hair" }; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
        /// Score80 D: the hair shell is a thin mesh drawn two-sided with URP Lit, which lights a backface with the
        /// OUTWARD normal: looking up under the visor band / nape edge (serve toss cam, ultimate close-ups) the inside
        /// of the shell caught the sun and read as bright cream holes in the hair. The shell now draws its outside
        /// one-sided and its inside as a second pass in dark, shadowed hair colour (same Lit shader, culling front
        /// faces), so every view into the hair reads as deep hair, never as a gap. HeroKit keeps its colour in step.
        /// Root cause (probe 2026-09-27, ArtDir/score80/probe/hair_winding.txt): 34% of the hair shell's triangles and
        /// 56% of the body's baked side-hair layer are wound against their own (outward) vertex normals. So the meshes
        /// are re-wound to agree with their normals first (a per-mesh copy, cached), which is what makes one-sided
        /// outside + dark inside correct everywhere.
        public const float HairInteriorShade = .36f, HairCoreShade = .6f;
        static readonly System.Collections.Generic.Dictionary<Mesh, Mesh> wound = new System.Collections.Generic.Dictionary<Mesh, Mesh>();
        public static int TrianglesRewound { get; private set; }
        static Mesh Rewound(Mesh src, int onlySub)
        {
            if (!src || !src.isReadable) return src;
            if (wound.TryGetValue(src, out var done)) return done;
            var m = Instantiate(src); m.name = src.name + " (wound)";
            var v = m.vertices; var n = m.normals;
            if (n.Length == v.Length)
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    if (onlySub >= 0 && s != onlySub) continue;
                    var t = m.GetTriangles(s); bool changed = false;
                    for (int i = 0; i < t.Length; i += 3)
                        if (Vector3.Dot(Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]), n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0)
                        { (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]); changed = true; TrianglesRewound++; }
                    if (changed) m.SetTriangles(t, s, false);
                }
            wound[src] = m; return m;
        }
        void SolidHairInterior()
        {
            // the body's baked side-hair layer: re-wound and drawn one-sided (its inside sits over the dark scalp)
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != "Body_Skin" || !r.sharedMesh) continue;
                var mats = r.sharedMaterials; int hairSub = -1;
                for (int i = 0; i < mats.Length; i++) if (mats[i] && (mats[i].name.StartsWith("Hero_01_HairTuft") || mats[i].name.StartsWith("skin_BlondHair"))) hairSub = i;
                if (hairSub < 0 || hairSub >= r.sharedMesh.subMeshCount) continue;
                r.sharedMesh = Rewound(r.sharedMesh, hairSub);
                var one = new Material(mats[hairSub]) { name = mats[hairSub].name.Replace(" (Instance)", "") + " (layer)" };
                if (one.HasProperty("_Cull")) one.SetFloat("_Cull", 2);
                mats[hairSub] = one; r.sharedMaterials = mats;
            }
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!r.name.StartsWith("Hair") || !r.sharedMesh || !r.sharedMaterial) continue;
                r.sharedMesh = Rewound(r.sharedMesh, -1);
                var front = new Material(r.sharedMaterial) { name = r.sharedMaterial.name.Replace(" (Instance)", "") + " (solid)" };
                if (front.HasProperty("_Cull")) front.SetFloat("_Cull", 2);
                var inside = new Material(r.sharedMaterial) { name = "Hero hair interior" };
                if (inside.HasProperty("_Cull")) inside.SetFloat("_Cull", 1);
                var c = front.HasProperty("_BaseColor") ? front.GetColor("_BaseColor") : Color.white;
                inside.SetColor("_BaseColor", new Color(c.r * HairInteriorShade, c.g * HairInteriorShade, c.b * HairInteriorShade, 1));
                if (inside.HasProperty("_Smoothness")) inside.SetFloat("_Smoothness", 0);
                inside.SetFloat("_SpecularHighlights", 0); inside.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                inside.SetFloat("_EnvironmentReflections", 0); inside.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                if (r.sharedMesh.subMeshCount == 1) { r.sharedMaterials = new[] { front, inside }; continue; }   // an extra material redraws the last submesh
                // several submeshes: a twin renderer on the same bones draws the interior
                var twin = new GameObject(r.name + " interior").AddComponent<SkinnedMeshRenderer>();
                twin.transform.SetParent(r.transform.parent, false); twin.transform.localPosition = r.transform.localPosition; twin.transform.localRotation = r.transform.localRotation; twin.transform.localScale = r.transform.localScale;
                twin.sharedMesh = r.sharedMesh; twin.bones = r.bones; twin.rootBone = r.rootBone; twin.localBounds = r.localBounds; twin.updateWhenOffscreen = true;
                var ms = new Material[r.sharedMesh.subMeshCount]; for (int i = 0; i < ms.Length; i++) ms[i] = inside; twin.sharedMaterials = ms;
                twin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var fr = r.sharedMaterials; for (int i = 0; i < fr.Length; i++) fr[i] = front; r.sharedMaterials = fr;
            }
        }
        /// Score80 D: from behind / above there was a horizontal slice through the hair where NOTHING sits behind the
        /// shell (the body mesh has no skull under that band of hair), so the background showed through (A/B:
        /// identical with the original materials). A closed "hair core" is fitted just inside the hair shell from
        /// its own shape (per direction on a lat-long grid: the nearest hair vertex in a 15 deg cone, x0.95), drawn
        /// in the dark hair-interior colour, rigid on the head bone. Directions with no hair, and the face
        /// region, collapse deep inside the skull so it can never cover the face, ears or eyes.
        void BuildHairCore()
        {
            var head = look.animator ? look.animator.GetBoneTransform(HumanBodyBones.Head) : null; if (!head) return;
            SkinnedMeshRenderer hairR = null; Material interior = null;
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.StartsWith("Hair") && r.sharedMesh) { hairR = r; foreach (var m in r.sharedMaterials) if (m && m.name.StartsWith("Hero hair interior")) interior = m; break; }
            if (!hairR || !interior) return;
            var baked = new Mesh(); hairR.BakeMesh(baked, false);
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var v in baked.vertices) pts.Add(head.InverseTransformPoint(hairR.transform.position + hairR.transform.rotation * v));
            Destroy(baked); if (pts.Count < 50) return;
            Vector3 up = head.InverseTransformDirection(look.transform.up).normalized, fwd = Vector3.ProjectOnPlane(head.InverseTransformDirection(look.transform.forward), up).normalized, right = Vector3.Cross(up, fwd);
            Vector3 c = Vector3.zero; foreach (var q in pts) c += q; c /= pts.Count;
            float rAll = float.MaxValue; foreach (var q in pts) rAll = Mathf.Min(rAll, (q - c).magnitude);
            const int Rings = 14, Segs = 36; const float LatLow = -35, LatHigh = 88;
            var rad = new float[Rings, Segs]; float cone = Mathf.Cos(15 * Mathf.Deg2Rad);
            Vector3 Dir(int i, int j) { float lat = Mathf.Lerp(LatLow, LatHigh, i / (float)(Rings - 1)) * Mathf.Deg2Rad, lon = j / (float)Segs * 2 * Mathf.PI;
                return (fwd * (Mathf.Cos(lat) * Mathf.Cos(lon)) + right * (Mathf.Cos(lat) * Mathf.Sin(lon)) + up * Mathf.Sin(lat)).normalized; }
            float deep = rAll * .45f; var near = new System.Collections.Generic.List<float>();
            for (int i = 0; i < Rings; i++) for (int j = 0; j < Segs; j++)
            {
                var d = Dir(i, j); float best = float.MaxValue; near.Clear();
                foreach (var q in pts) { var o = q - c; float m = o.magnitude; if (m > 1e-6f && Vector3.Dot(o / m, d) > cone) near.Add(m); }
                // the 70th-percentile radius (not the nearest): across a groove between two hair tiers the core rises to
                // most of the tiers' height, so the silhouette groove reads as a shadowed crease, not a slice of sky
                if (near.Count > 0) { near.Sort(); best = near[Mathf.Min(near.Count - 1, (int)(near.Count * .7f))]; }
                float lat = Mathf.Lerp(LatLow, LatHigh, i / (float)(Rings - 1)); float yaw = Mathf.Abs(Mathf.DeltaAngle(0, j * 360f / Segs));
                bool face = (lat < 30 && yaw < 75) || (lat < 15 && yaw < 125);   // face, eyes and ears stay clear
                rad[i, j] = best == float.MaxValue || face ? deep : best * .96f;
            }
            for (int pass = 0; pass < 2; pass++)   // soften spikes: average with the neighbours, never rising above its own value
            { var n = (float[,])rad.Clone(); for (int i = 0; i < Rings; i++) for (int j = 0; j < Segs; j++) { float sum = rad[i, j], cnt = 1; foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int ii = i + di; if (ii < 0 || ii >= Rings) continue; sum += rad[ii, (j + dj + Segs) % Segs]; cnt++; } n[i, j] = Mathf.Min(rad[i, j], sum / cnt); } rad = n; }
            var verts = new System.Collections.Generic.List<Vector3>(); var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Rings; i++) for (int j = 0; j < Segs; j++) verts.Add(c + Dir(i, j) * rad[i, j]);
            for (int i = 0; i < Rings - 1; i++) for (int j = 0; j < Segs; j++)
            { int a0 = i * Segs + j, a1 = i * Segs + (j + 1) % Segs, b0 = a0 + Segs, b1 = a1 + Segs; tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 }); }
            int top = verts.Count; verts.Add(c + up * rad[Rings - 1, 0]); int bot = verts.Count; verts.Add(c - up * deep * .6f);
            for (int j = 0; j < Segs; j++) { int t0 = (Rings - 1) * Segs + j, t1 = (Rings - 1) * Segs + (j + 1) % Segs; tris.AddRange(new[] { t0, top, t1 }); tris.AddRange(new[] { j, (j + 1) % Segs, bot }); }
            var mesh = new Mesh { name = "Hair core" }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            foreach (Transform ch in head) if (ch.name == "HairCore") Destroy(ch.gameObject);
            var go = new GameObject("HairCore"); go.transform.SetParent(head, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(interior) { name = "Hero hair core" }; if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0);
            var hc = hairR.sharedMaterials[0].GetColor("_BaseColor"); mat.SetColor("_BaseColor", new Color(hc.r * HairCoreShade, hc.g * HairCoreShade, hc.b * HairCoreShade, 1));
            mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        /// The covered-scalp material may be shared with other heroes / the prefab asset: give this hero its own.
        void EnsureOwnScalpMaterial()
        {
            foreach (var r in look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name != "Body_Skin") continue; var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++) if (mats[i] && mats[i].name.Contains("CoveredFoundation") && !mats[i].name.EndsWith("(scalp)")) { mats[i] = new Material(mats[i]) { name = mats[i].name + " (scalp)" }; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
        /// Motion probe: hips / chest yaw (court frame), spine bend (deg between hips-up and chest-up),
        /// racket-hand world position, time to contact.
        public string MotionProbe()
        {
            var y = BodyYaw(); var a = look.animator;
            var chest = a.GetBoneTransform(HumanBodyBones.Chest); var hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            float bend = Vector3.Angle(hips.up, chest.up);
            var hp = hand.position; var hip = hips.position;
            return $"hy={y.x:0.0} cy={y.y:0.0} bend={bend:0.0} hand={hp.x:0.000},{hp.y:0.000},{hp.z:0.000} hip={hip.x:0.000},{hip.y:0.000},{hip.z:0.000} ttc={(actor.Swinging ? actor.SignedTimeToContact : 9):0.000} aw={actionWeight:0.00} wyaw={walkYaw:0} vx={actor.Speed:0.00} vy={actor.ForwardSpeed:0.00} wm={(WalkMode()?1:0)} walkW={weight[(int)Clip.Walk]:0.00}";
        }
        /// Hips / chest facing in hero space (deg, 0 = facing the net, +90 = facing the right sideline).
        public Vector2 BodyYaw()
        {
            var chest = look.animator.GetBoneTransform(HumanBodyBones.Chest);
            var frame = actor ? actor.transform : transform;   // facing relative to the court (includes the serve stance yaw)
            float Y(Quaternion q, Vector3 f) { var v = frame.InverseTransformDirection(q * f); return Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg; }
            return new Vector2(Y(hips.rotation, torsoFwdHips), Y(chest.rotation, torsoFwdChest));
        }

        /// Review/probe: torso penetration of both arms in the current pose, plus the deepest sample (hero space).
        public string ProbeArms()
        {
            var a = look.animator; string o = "";
            foreach (bool r in new[] { true, false })
            {
                Transform up = a.GetBoneTransform(r ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm), lo = a.GetBoneTransform(r ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(r ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                var hd = (hand.rotation * handAxis[r ? 1 : 0]).normalized;
                o += (r ? "R=" : " L=") + ArmCost(up.position, lo.position, hand.position, hd).ToString("0.000") + " elbow=" + transform.InverseTransformPoint(lo.position).ToString("F2") + " hand=" + transform.InverseTransformPoint(hand.position).ToString("F2");
            }
            float rk = RacketCost(out var rkPart); var yw = BodyYaw();
            return o + $" racket={rk:0.000}({rkPart}) limbHL={LimbHeadLegCost(true):0.000}/{LimbHeadLegCost(false):0.000} yawHips={yw.x:0} yawChest={yw.y:0}" + " strings=" + transform.InverseTransformPoint(stringCentre.position).ToString("F2") + " tip=" + transform.InverseTransformPoint(stringUp.position).ToString("F2") + " chestBind=" + torsoBindPos[0].ToString("F3") + " hipsBind=" + torsoBindPos[2].ToString("F3");
        }
        float ArmCost(Vector3 sh, Vector3 el, Vector3 wr, Vector3 handDir)
        {
            float c = Penetration(Vector3.Lerp(sh, el, .8f), UpperArmR) + Penetration(el, UpperArmR);
            for (int k = 0; k <= 4; k++) c += Penetration(Vector3.Lerp(el, wr, k / 4f), LowerArmR);
            c += Penetration(wr + handDir * .07f, HandR);
            // head and legs too: a forearm or fist must not pass through the big chibi head or the thighs
            if (body != null) { c += body.Depth(el, UpperArmR * .9f, out _); for (int k = 1; k <= 4; k++) c += body.Depth(Vector3.Lerp(el, wr, k / 4f), LowerArmR, out _); c += body.Depth(wr + handDir * .07f, HandR, out _); }
            return c;
        }
        /// Blends between clips (run-stop, whiff recover...) can sweep an arm through the chibi torso.
        /// Swivel the elbow around the shoulder->wrist line (hand, grip and racket stay put) to clear it.
        void ClearArm(bool right)
        {
            var a = look.animator;
            Transform up = a.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm), lo = a.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm), hand = a.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            Vector3 S = up.position, E = lo.position, W = hand.position;
            var hd = (hand.rotation * handAxis[right ? 1 : 0]).normalized; float c0 = ArmCost(S, E, W, hd); if (right) LastArmPenetration = c0; else LastArmPenetration = Mathf.Max(LastArmPenetration, c0); if (right) ArmBeforeClear = c0; else ArmBeforeClear = Mathf.Max(ArmBeforeClear, c0);
            if (c0 < .01f || Baseline) return;
            var a2 = a;
            var axis = (W - S).normalized; float best = c0; float bestA = 0;
            for (int deg = -100; deg <= 100; deg += 10) { if (deg == 0) continue; var e2 = S + Quaternion.AngleAxis(deg, axis) * (E - S); float c = ArmCost(S, e2, W, hd) + Mathf.Abs(deg) * .00005f; if (c < best) { best = c; bestA = deg; } }
            var handRot = hand.rotation;
            if (bestA != 0)
            {
                var newE = S + Quaternion.AngleAxis(bestA, axis) * (E - S);
                up.rotation = Quaternion.FromToRotation(E - S, newE - S) * up.rotation;
                lo.rotation = Quaternion.FromToRotation(hand.position - lo.position, W - lo.position) * lo.rotation;
                hand.rotation = handRot;
            }
            // Stage 2 (a blend put the hand itself inside the torso): swing the whole arm outward from
            // the shoulder. Not for the top hand on a two-handed grip or the racket arm at contact.
            bool gripLocked = !right && actionActive && action == Clip.Backhand;
            bool atContact = right && actor.Swinging && Mathf.Abs(actor.SignedTimeToContact) < .2f;
            if (best > .01f && !gripLocked && !atContact)
            {
                var chestP = a.GetBoneTransform(HumanBodyBones.Chest).position;
                var outward = Vector3.ProjectOnPlane(hand.position - chestP, transform.up); if (outward.sqrMagnitude < 1e-4f) outward = right ? transform.right : -transform.right;
                var swingAxis = Vector3.Cross(hand.position - S, outward.normalized); if (swingAxis.sqrMagnitude < 1e-6f) swingAxis = transform.forward;
                swingAxis.Normalize();
                for (int deg = 5; deg <= 60; deg += 5)
                {
                    var q = Quaternion.AngleAxis(deg, swingAxis);
                    float c = ArmCost(S, S + q * (lo.position - S), S + q * (hand.position - S), q * hd);
                    if (c < .01f || deg == 60)
                    {
                        up.rotation = q * up.rotation; hand.rotation = q * handRot; break;
                    }
                }
            }
            // Stage 3 (two-handed top hand still in the chest, e.g. at the approved 2HBH contact): let the
            // left hand shift a few cm along and off the handle, away from the body. The racket, the
            // strings and the right hand do not move, so the hit is untouched.
            if (gripLocked && ArmCost(up.position, lo.position, hand.position, hd) > .01f && look.racketGrip)
            {
                var grip = look.racketGrip; var along = (stringCentre.position - grip.position).normalized;
                var chestP = a.GetBoneTransform(HumanBodyBones.Chest).position;
                var away = Vector3.ProjectOnPlane(hand.position - chestP, along); away = away.sqrMagnitude > 1e-6f ? away.normalized : transform.forward;
                Vector3 h0 = hand.position; Quaternion u0 = up.rotation, l0 = lo.rotation, r0 = hand.rotation;
                float bestC = ArmCost(S, lo.position, h0, hd); Vector3 bestT = h0;
                float bestSw = 0;
                foreach (var da in new[] { 0f, -.02f, .02f, -.04f })
                    foreach (var dw in new[] { .01f, .025f, .04f, .05f })
                    {
                        var t = h0 + along * da + away * dw;
                        TwoBone(up, lo, hand, t); hand.rotation = r0;
                        // with an elbow swivel about the shoulder-wrist line at each candidate
                        Vector3 s0 = up.position, e0 = lo.position, w0 = hand.position; var ax = (w0 - s0).normalized;
                        foreach (var sw in new[] { 0f, -20f, 20f, -40f, 40f })
                        {
                            var e2 = s0 + Quaternion.AngleAxis(sw, ax) * (e0 - s0);
                            float c = ArmCost(s0, e2, w0, hd) + (da * da + dw * dw) * 2f + Mathf.Abs(sw) * .0001f;
                            if (c < bestC) { bestC = c; bestT = t; bestSw = sw; }
                        }
                        up.rotation = u0; lo.rotation = l0; hand.rotation = r0;
                    }
                if (bestT != h0 || bestSw != 0)
                {
                    TwoBone(up, lo, hand, bestT); hand.rotation = r0;
                    if (bestSw != 0)
                    {
                        Vector3 s0 = up.position, e0 = lo.position, w0 = hand.position;
                        var e2 = s0 + Quaternion.AngleAxis(bestSw, (w0 - s0).normalized) * (e0 - s0);
                        up.rotation = Quaternion.FromToRotation(e0 - s0, e2 - s0) * up.rotation;
                        lo.rotation = Quaternion.FromToRotation(hand.position - lo.position, w0 - lo.position) * lo.rotation;
                        hand.rotation = r0;
                    }
                }
            }
            float after = ArmCost(up.position, lo.position, hand.position, hd);
            if (right) LastArmPenetration = after; else LastArmPenetration = Mathf.Max(LastArmPenetration, after);
        }

        /// Deterministic review: one clip at one time, with the same hands/props logic as gameplay.
        public void Sample(Clip c, float t)
        {
            for (int k = 0; k < Count; k++) { if (!valid[k]) continue; mixer.SetInputWeight(k, k == (int)c ? 1 : 0); playables[k].SetTime(k == (int)c ? t : 0); }
            graph.Evaluate(0);
            int roll = c == Clip.Forehand ? (int)ModularHeroLook.Stroke.Forehand : c == Clip.Volley ? (int)ModularHeroLook.Stroke.Volley : -1;
            bool left = c == Clip.Ready || c == Clip.Backhand || c == Clip.HitPerfect || c == Clip.CelebratePoint || c == Clip.MatchWin;
            look.ApplyHands(roll, 1, left);
            if (cosmetics) cosmetics.SetCelebrating(c == Clip.CelebratePoint || c == Clip.MatchWin);
        }
        public float ContactOf(Clip c) => contact[(int)c];
        public float LengthOf(Clip c) => length[(int)c];

        float Moving() => new Vector2(actor.Speed, actor.ForwardSpeed).magnitude;
        bool BetweenPoints() => game && (game.Flow == TennisGame.Phase.PointOver || game.Flow == TennisGame.Phase.MatchOver);

        /// Near contact, nudge the right arm (two-bone, hand rotation kept) so the hero's string bed
        /// meets the gameplay racket's sweet spot, i.e. where the ball is actually struck.
        public float LastCrouch { get; private set; }
        void AssistContact()
        {
            float ttc = Mathf.Abs(actor.SignedTimeToContact);
            float w = Mathf.Clamp01(1 - ttc / .16f); if (w <= 0 || !look.racketGrip) return;
            if (Baseline) { if (action == Clip.Serve || !actor.SweetSpot) return; }
            else if (!actor.Guiding) return;
            var a = look.animator;
            Transform up = a.GetBoneTransform(HumanBodyBones.RightUpperArm), lo = a.GetBoneTransform(HumanBodyBones.RightLowerArm), hand = a.GetBoneTransform(HumanBodyBones.RightHand);
            if (!Baseline)
            {
                // A reach is carried by the body too: lunge the whole hero toward the ball.
                var toBall = (game && actor == game.Opponent && game.Flow == TennisGame.Phase.Rally ? game.BallPosition : actor.GuideBall) - stringCentre.position; toBall.y = 0;
                var lunge = Vector3.ClampMagnitude(toBall, bodyReach) * w;
                transform.position += lunge; lungeOffset = Vector3.ClampMagnitude(transform.localPosition, bodyReach); transform.localPosition = lungeOffset;
                // A low ball is reached by bending the knees, not by driving the arm down through the
                // thigh: drop the hips and re-solve both legs so the feet stay planted on the court.
                Vector3 g0 = game && actor == game.Opponent && game.Flow == TennisGame.Phase.Rally ? game.BallPosition : actor.GuideBall;
                float low = stringCentre.position.y - g0.y - .08f;
                if (low > 0 && hips)
                {
                    float drop = Mathf.Min(.34f, low * .9f) * w;
                    Transform lu = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), ll = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
                    Transform ru = a.GetBoneTransform(HumanBodyBones.RightUpperLeg), rl = a.GetBoneTransform(HumanBodyBones.RightLowerLeg), rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
                    Vector3 lp = lf.position, rp = rf.position; Quaternion lr = lf.rotation, rr = rf.rotation;
                    hips.position += Vector3.down * drop;
                    TwoBone(lu, ll, lf, lp); lf.rotation = lr; TwoBone(ru, rl, rf, rp); rf.rotation = rr;
                    LastCrouch = drop;
                }
            }
            var sweet = stringCentre.position;
            // The rival's racket reaches for the live ball (its planned meet point can sit off the real
            // flight), so the honest-contact check judges what is on screen.
            Vector3 goal = Baseline ? actor.SweetSpot.position : game && actor == game.Opponent && game.Flow == TennisGame.Phase.Rally ? game.BallPosition : actor.GuideBall;
            var delta = Vector3.ClampMagnitude(goal - sweet, contactAssist) * w;
            if (delta.sqrMagnitude < 1e-6f) return;
            // Never buy reach by driving an arm into the torso. The hitting arm keeps the nudge only as far
            // as it stays clear (past that the honest-contact rule calls a real miss); on the two-hander the
            // top hand rides the handle only as far as it stays clear, then lets go (a stretched one-hand reach).
            var hdR = (hand.rotation * handAxis[1]).normalized;
            float baseR = ArmCost(up.position, ElbowFor(up, lo, hand, hand.position), hand.position, hdR);
            delta *= ClearScale(sc => ArmCost(up.position, ElbowFor(up, lo, hand, hand.position + delta * sc), hand.position + delta * sc, hdR), baseR);
            if (delta.sqrMagnitude < 1e-6f) return;
            if (action == Clip.Backhand)
            {
                Transform lu = a.GetBoneTransform(HumanBodyBones.LeftUpperArm), ll = a.GetBoneTransform(HumanBodyBones.LeftLowerArm), lh = a.GetBoneTransform(HumanBodyBones.LeftHand);
                var hdL = (lh.rotation * handAxis[0]).normalized;
                float baseL = ArmCost(lu.position, ElbowFor(lu, ll, lh, lh.position), lh.position, hdL);
                var ld = delta * ClearScale(sc => ArmCost(lu.position, ElbowFor(lu, ll, lh, lh.position + delta * sc), lh.position + delta * sc, hdL), baseL);
                if (ld.sqrMagnitude > 1e-6f) { var lr = lh.rotation; TwoBone(lu, ll, lh, lh.position + ld); lh.rotation = lr; }
            }
            var handRot = hand.rotation; var target = hand.position + delta;
            float l1 = Vector3.Distance(up.position, lo.position), l2 = Vector3.Distance(lo.position, hand.position);
            var toT = target - up.position; float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-3f, l1 + l2 - 1e-3f);
            var pole = Vector3.ProjectOnPlane(lo.position - up.position, toT.normalized).normalized;
            float along = (l1 * l1 - l2 * l2 + d * d) / (2 * d), h = Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            var elbow = up.position + toT.normalized * along + pole * h;
            up.rotation = Quaternion.FromToRotation(lo.position - up.position, elbow - up.position) * up.rotation;
            lo.rotation = Quaternion.FromToRotation(hand.position - lo.position, up.position + toT.normalized * d - lo.position) * lo.rotation;
            hand.rotation = handRot;
        }
    }
}
