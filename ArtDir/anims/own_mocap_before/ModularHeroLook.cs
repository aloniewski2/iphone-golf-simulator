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
        public WardrobeSlot[] defaults;
        public Texture2D skinAtlas, skinMask;
        public Color skinTone = new Color(1,.8784f,.7608f,1);
        public bool useSkinTint;
        public float skinReference = .44107563f;
        static int activeSkinningUsers;
        static SkinWeights previousSkinWeights;
        bool ownsSkinningQuality;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        RenderTexture tintedSkin;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public bool IdleRunning => graph.IsValid() && graph.IsPlaying();
        public double IdleTime => playable.IsValid() ? playable.GetTime() : 0;

        void OnEnable()
        {
            if (!Application.isPlaying || !animator || !idle) return;
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
            graph = PlayableGraph.Create("Hero01 look idle");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            playable = AnimationClipPlayable.Create(graph, idle);
            playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph, "Humanoid", animator).SetSourcePlayable(playable);
            graph.Play();
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
                        if(mat.name.Contains("CoveredFoundation")) mat.SetColor("_BaseColor",tone);
                skinTone=tone;return;
            }
            foreach(var renderer in skeletonRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if(renderer.name != "Body_Skin") continue;
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++) { var mat=new Material(mats[i]); if(mat.name.Contains("CoveredFoundation")) {mat.SetTexture("_BaseMap",null);mat.SetColor("_BaseColor",tone);}
                    else mat.SetTexture("_BaseMap",tintedSkin); mats[i]=mat; owned.Add(mat); }
                renderer.sharedMaterials=mats;
            }
            skinTone=tone;
        }
        void OnDisable()
        {
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
