using UnityEngine;

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
            // Shadows only need to cover the court; spending the map on the far scenery is
            // what made the old ones blocky.
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.lodBias = tier == Tier.Low ? .8f : 1.2f;
            QualitySettings.skinWeights = SkinWeights.FourBones;
            QualitySettings.globalTextureMipmapLimit = 0;
        }

        /// Called by the frame governor when frames are consistently late. Each step trades
        /// a little image quality for time; smoothness wins over sharpness.
        public static bool StepDown()
        {
            if (QualitySettings.antiAliasing > 2) QualitySettings.antiAliasing = 2;
            else if (QualitySettings.shadows == ShadowQuality.All) QualitySettings.shadows = ShadowQuality.HardOnly;
            else if (QualitySettings.shadowCascades > 1) QualitySettings.shadowCascades = 1;
            else if (QualitySettings.antiAliasing > 0) QualitySettings.antiAliasing = 0;
            else return false;
            Reductions++;
            return true;
        }
    }
}
