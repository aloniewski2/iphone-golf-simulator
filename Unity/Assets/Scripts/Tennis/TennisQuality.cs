using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShadowQuality = UnityEngine.ShadowQuality;
using ShadowResolution = UnityEngine.ShadowResolution;

namespace GolfArcade.Tennis
{
    /// Rendering settings chosen per phone.
    ///
    /// The project's iPhone default was the "Medium" level: no anti-aliasing and hard,
    /// lowest-resolution shadows. In motion, unsmoothed edges crawl from frame to frame,
    /// which reads as flicker -- a smoothness problem as much as a looks one. Every phone in
    /// the target range (iPhone 15 to 18) has an A16 or newer GPU, and MSAA on Apple's tiled
    /// GPUs resolves on-chip, so it is cheap there.
    ///
    /// Tiers come from the hardware model identifier, which is stable and cannot be spoofed
    /// by a setting: iPhone15,x is A16 (iPhone 14 Pro / 15), iPhone16,x A17 Pro (15 Pro),
    /// iPhone17,x A18 (16 family), iPhone18,x A19 (17 family).
    public static class TennisQuality
    {
        public enum Tier { Low, Standard, High }
        public static Tier Current { get; private set; } = Tier.High;
        /// Set when the frame governor has had to step quality down this session.
        public static int Reductions { get; private set; }

        public static Tier ForModel(string model)
        {
            if (string.IsNullOrEmpty(model) || !model.StartsWith("iPhone")) return Tier.High;
            int comma = model.IndexOf(',');
            if (comma < 0 || !int.TryParse(model.Substring(6, comma - 6), out int generation)) return Tier.Standard;
            if (generation >= 16) return Tier.High;
            if (generation == 15) return Tier.Standard;
            return Tier.Low;
        }

        public struct Settings
        {
            public int Msaa, PixelLights;
            public ShadowQuality Shadows;
            public ShadowResolution ShadowResolution;
            public float ShadowDistance;
            public int ShadowCascades;
        }

        public static Settings For(Tier tier) => tier switch
        {
            Tier.High => new Settings { Msaa = 4, PixelLights = 2, Shadows = ShadowQuality.All, ShadowResolution = ShadowResolution.VeryHigh, ShadowDistance = 42, ShadowCascades = 2 },
            Tier.Standard => new Settings { Msaa = 4, PixelLights = 1, Shadows = ShadowQuality.All, ShadowResolution = ShadowResolution.High, ShadowDistance = 38, ShadowCascades = 1 },
            _ => new Settings { Msaa = 2, PixelLights = 1, Shadows = ShadowQuality.HardOnly, ShadowResolution = ShadowResolution.Medium, ShadowDistance = 32, ShadowCascades = 1 },
        };

        public static void Apply() => Apply(ForModel(SystemInfo.deviceModel));

        static UniversalRenderPipelineAsset live;

        /// Under URP the pipeline asset owns MSAA, shadows and render scale. Each session
        /// works on its own copy so tier changes and governor step-downs never edit the asset
        /// on disk.
        public static UniversalRenderPipelineAsset Pipeline
        {
            get
            {
                if (live) return live;
                // In the editor, reassigning the pipeline writes into the project's settings;
                // the editor always runs the asset as authored (the High tier) instead.
                if (Application.isEditor) return null;
                var shared = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
                if (!shared) return null;
                live = Object.Instantiate(shared); live.name = shared.name + " (session)";
                GraphicsSettings.defaultRenderPipeline = live;
                QualitySettings.renderPipeline = live;
                return live;
            }
        }

        public static void Apply(Tier tier)
        {
            Current = tier; Reductions = 0;
            var s = For(tier);
            QualitySettings.antiAliasing = s.Msaa;
            QualitySettings.pixelLightCount = s.PixelLights;
            QualitySettings.shadows = s.Shadows;
            QualitySettings.shadowResolution = s.ShadowResolution;
            QualitySettings.shadowDistance = s.ShadowDistance;
            QualitySettings.shadowCascades = s.ShadowCascades;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.lodBias = tier == Tier.Low ? .8f : 1.2f;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.globalTextureMipmapLimit = 0;
            var urp = Pipeline;
            if (urp)
            {
                urp.msaaSampleCount = s.Msaa;
                urp.shadowDistance = s.ShadowDistance;
                urp.shadowCascadeCount = s.ShadowCascades;
                urp.mainLightShadowmapResolution = s.ShadowResolution switch
                { ShadowResolution.VeryHigh => 2048, ShadowResolution.High => 2048, ShadowResolution.Medium => 1024, _ => 512 };
                urp.supportsHDR = tier != Tier.Low;
                urp.renderScale = 1f;
            }
            SetAmbientOcclusion(tier == Tier.High);
        }

        /// Screen-space AO is the most expensive effect; only the High tier keeps it.
        static void SetAmbientOcclusion(bool on)
        {
            var data = Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");
            if (!data) return;
            foreach (var feature in data.rendererFeatures)
                if (feature is ScreenSpaceAmbientOcclusion && feature.isActive != on && !Application.isEditor) feature.SetActive(on);
        }

        /// Called by the frame governor when frames are consistently late. Each step trades
        /// a little image quality for time; smoothness wins over sharpness.
        public static bool StepDown()
        {
            var urp = Pipeline;
            if (!urp) return false;
            if (urp.msaaSampleCount > 2) urp.msaaSampleCount = 2;
            else if (Current == Tier.High && !Application.isEditor) { SetAmbientOcclusion(false); Current = Tier.Standard; }
            else if (urp.shadowCascadeCount > 1) urp.shadowCascadeCount = 1;
            else if (urp.renderScale > .86f) urp.renderScale = .85f;
            else if (urp.msaaSampleCount > 1) urp.msaaSampleCount = 1;
            else return false;
            Reductions++;
            return true;
        }
    }
}
