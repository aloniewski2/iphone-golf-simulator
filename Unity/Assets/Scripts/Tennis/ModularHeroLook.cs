using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace GolfArcade.Tennis
{
    /// Look-only modular Humanoid. No gameplay root, hitbox, reach or timing changes.
    public sealed class ModularHeroLook : MonoBehaviour
    {
        public enum Slot { Hair, Hat, Shirt, Shorts, Shoes }
        [Serializable] public struct WardrobeSlot { public Slot slot; public GameObject asset; }
        public Transform skeletonRoot;
        public Animator animator;
        public AnimationClip idle;
        public AnimationClip forehand, backhand, serve, volley;
        public AnimationClip runForward, runRight, runLeft, smash;
        public enum Stroke { Forehand, Backhand, Serve, Volley, RunForward, RunRight, RunLeft, Smash }
        public Transform racketGrip;
        // Opt-in grip pose for Ready/swings; legacy run mesh and attachment remain exact.
        public Mesh swingGripMesh;
        public GameObject swingGripWrap;
        SkinnedMeshRenderer gripBody;
        Mesh unposedBody;
        Vector3 runSocketPosition;
        Quaternion runSocketRotation;
        void ApplyHoldingGrip(int stroke)
        {
            if(!swingGripMesh || !racketGrip) return;
            bool run=stroke>=4 && stroke<=6;
            if(swingGripWrap) swingGripWrap.SetActive(!run);
            if(!gripBody) {
                foreach(var r in GetComponentsInChildren<SkinnedMeshRenderer>()) if(r.name=="Body_Skin") {gripBody=r;break;}
                if(!gripBody) return;
                unposedBody=gripBody.sharedMesh;
                runSocketPosition=racketGrip.localPosition;runSocketRotation=racketGrip.localRotation;
            }
            gripBody.sharedMesh=run?unposedBody:swingGripMesh;
            racketGrip.localPosition=run?runSocketPosition:new Vector3(.060f,.05302886f,.0025f)/100f;
            racketGrip.localRotation=run?runSocketRotation:new Quaternion(-.25f,.25f,-.06698730f,.93301270f);
            if(!run) for(int i=0;i<gripBody.sharedMesh.blendShapeCount;i++) {
                string n=gripBody.sharedMesh.GetBlendShapeName(i); if(n.EndsWith("Female")) continue;   // body shape, not grip
                gripBody.SetBlendShapeWeight(i,n.EndsWith("Grip_R") || (stroke==1 && n.EndsWith("Grip_L"))?100:0);
            }
        }
        // Finger-rig grip: per-bone target directions in a geometric hand frame
        // (W index->pinky across knuckles, F along the middle finger, N palm-ward), rebuilt from bone positions.
        [Serializable] public struct FingerPose { public string bone; public Vector3 dir, sec, restDir, restSec; }
        public FingerPose[] gripRight, gripLeft;
        // Grip roll about the racket handle axis per Stroke index (e.g. eastern forehand vs continental volley).
        // The socket position / handle axis in the finger cradle is shared; only the face rotates in the fingers.
        public float[] strokeGripRoll = new float[8];
        Quaternion socketRotation; bool socketCaptured;
        void ApplyGripRoll(int stroke, float weight)
        {
            if(!racketGrip || strokeGripRoll==null || strokeGripRoll.Length<8 || gripRight==null || gripRight.Length==0) return;
            if(!socketCaptured){socketRotation=racketGrip.localRotation;socketCaptured=true;}
            float roll=stroke>=0?strokeGripRoll[stroke]*weight:0;
            racketGrip.localRotation=socketRotation*Quaternion.AngleAxis(roll,Vector3.up);
        }
        struct HandGrip { public Transform hand; public Transform[] bones; public Quaternion[] restRel; public Vector3 w, f, n; }
        HandGrip rightGrip, leftGrip;
        static HandGrip BuildGrip(Transform hand, FingerPose[] poses, string side)
        {
            var g=new HandGrip{hand=hand};
            if(!hand || poses==null || poses.Length==0) return g;
            var all=hand.GetComponentsInChildren<Transform>();
            Transform Find(string n){foreach(var t in all) if(t.name==n) return t; return null;}
            var index=Find("Index1."+side);var middle=Find("Middle1."+side);var middle2=Find("Middle2."+side);var pinky=Find("Pinky1."+side);var thumb=Find("Thumb1."+side);
            if(!index || !middle || !middle2 || !pinky || !thumb) return g;
            Vector3 L(Transform t)=>hand.InverseTransformPoint(t.position);
            g.f=(L(middle2)-L(middle)).normalized;g.w=Vector3.ProjectOnPlane(L(pinky)-L(index),g.f).normalized;
            g.n=Vector3.Cross(g.w,g.f).normalized;if(Vector3.Dot(g.n,L(thumb)-L(index))<0)g.n=-g.n;
            // Hand-local lengths are in bone units; only directions are used from the frame.
            g.bones=new Transform[poses.Length];g.restRel=new Quaternion[poses.Length];
            for(int i=0;i<poses.Length;i++){g.bones[i]=Find(poses[i].bone);if(g.bones[i])g.restRel[i]=Quaternion.Inverse(hand.rotation)*g.bones[i].rotation;}
            return g;
        }
        static Vector3 InFrame(in HandGrip g,Vector3 c)=>g.hand.rotation*(g.w*c.x+g.f*c.y+g.n*c.z);
        static void ApplyGrip(in HandGrip g, FingerPose[] poses)
        {
            if(g.bones==null) return;
            for(int i=0;i<poses.Length;i++) {
                var b=g.bones[i];if(!b) continue;
                var rest=Quaternion.LookRotation(InFrame(g,poses[i].restDir),InFrame(g,poses[i].restSec));
                var posed=Quaternion.LookRotation(InFrame(g,poses[i].dir),InFrame(g,poses[i].sec));
                b.rotation=posed*Quaternion.Inverse(rest)*(g.hand.rotation*g.restRel[i]);
            }
        }
        // Run carry: wrist-only offset so the racket head points forward/down/outward instead of across the legs.
        // Run clip curves are untouched; only Hand.R is re-aimed after the humanoid pose is evaluated.
        [Range(0,1)] public float runCarry = 0;
        float runCarryWeight;
        void ApplyRunCarry()
        {
            if(runCarry<=0 || runCarryWeight<=0 || !racketGrip || rightGrip.hand==null) return;
            var hand=rightGrip.hand;var fwd=transform.forward;var right=transform.right;
            var desired=(fwd*.85f+right*.30f-Vector3.up*.12f).normalized;
            var turn=Quaternion.FromToRotation(racketGrip.up,desired);
            hand.rotation=Quaternion.Slerp(Quaternion.identity,turn,runCarry*runCarryWeight)*hand.rotation;
        }
        void ApplyFingerGrips(int stroke)
        {
            ApplyRunCarry();
            ApplyGrip(rightGrip,gripRight);
            // Left hand holds the handle on the two-hand backhand and supports the throat in Ready.
            if(stroke<0 || stroke==(int)Stroke.Backhand) ApplyGrip(leftGrip,gripLeft);
            else if(leftGrip.bones!=null) for(int i=0;i<leftGrip.bones.Length;i++) if(leftGrip.bones[i]) leftGrip.bones[i].rotation=leftGrip.hand.rotation*leftGrip.restRel[i];
        }
        void LateUpdate() { if(!reviewSampling && !externalAnimation) ApplyFingerGrips(activeStroke); }
        /// When true, another component drives the Animator and calls ApplyHands after evaluating.
        public bool externalAnimation;
        /// Grip roll for a Stroke index (weighted), right-hand grip, and optional left-hand grip.
        public void ApplyHands(int rollStroke, float rollWeight, bool leftGrip)
        {
            runCarryWeight=0;
            ApplyGripRoll(rollStroke,rollWeight);
            ApplyGrip(rightGrip,gripRight);
            if(leftGrip) ApplyGrip(leftGrip_,gripLeft); else if(leftGrip_.bones!=null) for(int i=0;i<leftGrip_.bones.Length;i++) if(leftGrip_.bones[i]) leftGrip_.bones[i].rotation=leftGrip_.hand.rotation*leftGrip_.restRel[i];
        }
        HandGrip leftGrip_ => leftGrip;
        public WardrobeSlot[] defaults;
        public Texture2D skinAtlas, skinMask;
        public Color skinTone = new Color(1,.8784f,.7608f,1);
        /// Colour of the covered scalp under the hair. Unset (alpha 0) = the old behaviour (skin tone), which
        /// showed pink "holes" through the ragged hair cut-outs around the ears. Set by SetScalpTone.
        public Color scalpTone = new Color(0,0,0,0);
        Color ScalpOr(Color tone) => scalpTone.a > 0 ? scalpTone : tone;
        /// Scalp under the hair takes the hair's own (shadowed) colour, so hair gaps read as hair depth.
        public void SetScalpTone(Color c)
        {
            c.a = 1; scalpTone = c;
            foreach (var renderer in skeletonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.name == "Body_Skin") foreach (var mat in renderer.sharedMaterials)
                    if (mat && mat.name.Contains("CoveredFoundation")) { mat.SetTexture("_BaseMap", null); mat.SetColor("_BaseColor", c); }
        }
        public bool useSkinTint;
        public float skinReference = .44107563f;
        static int activeSkinningUsers;
        static SkinWeights previousSkinWeights;
        bool ownsSkinningQuality;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationMixerPlayable motionMixer;
        AnimationClipPlayable[] strokes;
        int activeStroke = -1;
        float strokeAge, strokeLength;
        bool reviewSampling;
        RenderTexture tintedSkin;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public bool StrokeRunning => activeStroke >= 0;
        public bool IdleRunning => graph.IsValid() && graph.IsPlaying();
        public double IdleTime => playable.IsValid() ? playable.GetTime() : 0;

        void OnEnable()
        {
            if (!Application.isPlaying || !animator || !idle) return;
            activeStroke=-1;strokeAge=0;reviewSampling=false;
            // This project's two-influence tier visibly truncates the shoulder blend.
            // Scope four influences to the lifetime of active modular heroes.
            if(!ownsSkinningQuality) {
                if(activeSkinningUsers++==0) {previousSkinWeights=QualitySettings.skinWeights;QualitySettings.skinWeights=SkinWeights.FourBones;}
                ownsSkinningQuality=true;
            }
            RebuildDefaultWardrobe();
            // Body seam vertices also require all four authored influences.
            foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>()) skin.quality=SkinQuality.Bone4;
            if(useSkinTint) SetSkinTone(skinTone);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (externalAnimation) {
                // A gameplay driver (HeroTennisDriver) owns the PlayableGraph; keep wardrobe + hands here.
                rightGrip=BuildGrip(animator.GetBoneTransform(HumanBodyBones.RightHand),gripRight,"R");
                leftGrip=BuildGrip(animator.GetBoneTransform(HumanBodyBones.LeftHand),gripLeft,"L");
                return;
            }
            graph = PlayableGraph.Create("Hero01 tennis");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playable = AnimationClipPlayable.Create(graph, idle);
            playable.SetApplyFootIK(false);
            motionMixer = AnimationMixerPlayable.Create(graph, 9);
            graph.Connect(playable, 0, motionMixer, 0); motionMixer.SetInputWeight(0, 1);
            strokes = new AnimationClipPlayable[8];
            var clips = new[] { forehand, backhand, serve, volley, runForward, runRight, runLeft, smash };
            for (int i=0; i<clips.Length; i++) if(clips[i]) {
                strokes[i] = AnimationClipPlayable.Create(graph, clips[i]);
                strokes[i].SetApplyFootIK(false); strokes[i].SetSpeed(0);
                graph.Connect(strokes[i], 0, motionMixer, i+1);
            }
            AnimationPlayableOutput.Create(graph, "Humanoid", animator).SetSourcePlayable(motionMixer);
            graph.Play();
            ApplyHoldingGrip(-1);
            rightGrip=BuildGrip(animator.GetBoneTransform(HumanBodyBones.RightHand),gripRight,"R");
            leftGrip=BuildGrip(animator.GetBoneTransform(HumanBodyBones.LeftHand),gripLeft,"L");
        }
        /// Visual-only hook. Gameplay owns contact, scoring and movement.
        public bool PlayStroke(Stroke stroke)
        {
            int index=(int)stroke;
            if(!graph.IsValid() || strokes==null || index<0 || index>=strokes.Length || !strokes[index].IsValid()) return false;
            ApplyHoldingGrip(index);
            reviewSampling=false; graph.Play(); activeStroke=index; strokeAge=0;
            strokeLength=strokes[index].GetAnimationClip().length;
            strokes[index].SetTime(0); strokes[index].SetSpeed(1);
            return true;
        }
        public void ReturnToReady()
        {
            activeStroke=-1; ApplyHoldingGrip(-1); ApplyGripRoll(-1,0); runCarryWeight=0;
            if(!motionMixer.IsValid()) return;
            motionMixer.SetInputWeight(0,1);
            for(int i=0;i<strokes.Length;i++) {motionMixer.SetInputWeight(i+1,0);if(strokes[i].IsValid())strokes[i].SetSpeed(0);}
        }
        void Update()
        {
            if(reviewSampling || activeStroke<0 || !graph.IsValid()) return;
            strokeAge+=Time.deltaTime;
            float weight=Mathf.Min(Mathf.Clamp01(strokeAge/.10f),Mathf.Clamp01((strokeLength-strokeAge)/.16f));
            motionMixer.SetInputWeight(0,1-weight);
            for(int i=0;i<strokes.Length;i++) motionMixer.SetInputWeight(i+1,i==activeStroke?weight:0);
            ApplyGripRoll(activeStroke,weight);
            runCarryWeight=activeStroke>=4 && activeStroke<=6?weight:0;
            if(strokeAge>=strokeLength)ReturnToReady();
        }
        /// Deterministic review of the same Humanoid PlayableGraph used at runtime.
        public void SampleForReview(int stroke, double seconds)
        {
            if(!graph.IsValid()) throw new InvalidOperationException("Hero graph is not running");
            if(stroke>=0 && (strokes==null || stroke>=strokes.Length || !strokes[stroke].IsValid())) throw new InvalidOperationException("Requested review slot has no animation: "+stroke);
            ApplyHoldingGrip(stroke);
            reviewSampling=true; graph.Stop();
            motionMixer.SetInputWeight(0,stroke<0?1:0); playable.SetTime(seconds);
            for(int i=0;i<strokes.Length;i++) {
                motionMixer.SetInputWeight(i+1,stroke==i?1:0);
                if(strokes[i].IsValid()) {strokes[i].SetTime(seconds);strokes[i].SetSpeed(0);}
            }
            graph.Evaluate(0);
            ApplyGripRoll(stroke,1);
            runCarryWeight=stroke>=4 && stroke<=6?1:0;
            ApplyFingerGrips(stroke);
        }
        [ContextMenu("Rebuild Default Wardrobe")]
        public void RebuildDefaultWardrobe()
        {
            if (defaults == null) return;
            foreach (var item in defaults) Equip(item.slot, item.asset);
        }
        public void Equip(Slot slot, GameObject asset)
        {
            if (!asset || !skeletonRoot) throw new InvalidOperationException("Missing wardrobe asset or skeleton");
            // Stage every replacement before removing the currently equipped slot.
            var container = new GameObject("Slot_" + slot).transform;
            container.SetParent(skeletonRoot, false);
            try {
                foreach(var source in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    TennisCustomization.RebindCosmetic(source, asset.transform, skeletonRoot, container);
            } catch { Release(container.gameObject); throw; }
            foreach(Transform child in skeletonRoot)
                if(child != container && child.name == container.name) { child.gameObject.SetActive(false); Release(child.gameObject); }
        }
        public void SetSkinTone(Color tone)
        {
            if(!skinAtlas || !skinMask) return;
            var recolor = new Material(Resources.Load<Shader>("Tennis/Shaders/KitRecolor"));
            recolor.SetTexture("_Mask", skinMask); recolor.SetVector("_Ref", new Vector4(.4f,.2f,.6f,skinReference));
            tone.a=1; recolor.SetColor("_Skin",tone);
            recolor.SetFloat("_SkinShading",.45f);
            bool first=!tintedSkin;
            if(first) {tintedSkin=new RenderTexture(skinAtlas.width,skinAtlas.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);owned.Add(tintedSkin);}
            Graphics.Blit(skinAtlas,tintedSkin,recolor); Release(recolor);
            if(!first) {
                foreach(var renderer in skeletonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if(renderer.name=="Body_Skin") foreach(var mat in renderer.sharedMaterials)
                        if(mat.name.Contains("CoveredFoundation")) mat.SetColor("_BaseColor",ScalpOr(tone));
                skinTone=tone;return;
            }
            foreach(var renderer in skeletonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if(renderer.name != "Body_Skin") continue;
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++) { var mat=new Material(mats[i]); if(mat.name.Contains("CoveredFoundation")) {mat.SetTexture("_BaseMap",null);mat.SetColor("_BaseColor",ScalpOr(tone));}
                    else mat.SetTexture("_BaseMap",tintedSkin); mats[i]=mat; owned.Add(mat); }
                renderer.sharedMaterials=mats;
            }
            skinTone=tone;
        }
        void OnDisable()
        {
            if(gripBody && unposedBody) gripBody.sharedMesh=unposedBody;
            if(gripBody && racketGrip) {racketGrip.localPosition=runSocketPosition;racketGrip.localRotation=runSocketRotation;}

            if(graph.IsValid()) graph.Destroy();
            if(ownsSkinningQuality) {
                ownsSkinningQuality=false;
                if(--activeSkinningUsers==0) QualitySettings.skinWeights=previousSkinWeights;
            }
        }
        void OnDestroy() { foreach(var item in owned) if(item) {if(item is RenderTexture rt)rt.Release();Release(item);} owned.Clear(); }
        static void Release(UnityEngine.Object item) { if(Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
    }
}
