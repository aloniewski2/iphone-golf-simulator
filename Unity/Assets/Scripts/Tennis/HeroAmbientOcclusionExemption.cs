using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.Tennis
{
    /// Keeps the renderer's screen-space "Contact AO" off the heroes (bald-base pass 2026-10-12).
    ///
    /// Contact AO runs in After Opaque mode: one full-screen multiply over everything drawn so far, with a 0.2 m radius that suits the
    /// court and the venues but fills a face's small cavities (eye sockets, nose wings, ears, the gaps between fingers, the neck under
    /// the jaw) with dark, noisy blotches. The hero shaders already shade their own soft depth, so this pass draws the hero renderers
    /// (everything on the HeroRimLight rendering layer, which MatchHeroLook gives every hero renderer and nothing else) once more right
    /// after the AO multiply and before transparents. The world keeps its AO, the ground keeps the hero's contact darkening, the depth
    /// texture still holds the heroes (depth of field, water edges), and nothing is drawn twice when Contact AO is off (Standard / Low tier).
    [DisallowMultipleRendererFeature("Hero Ambient Occlusion Exemption")]
    public sealed class HeroAmbientOcclusionExemption : ScriptableRendererFeature
    {
        /// The renderer's Contact AO feature; the exemption follows its on/off state (TennisQuality turns it off below the High tier).
        public ScriptableRendererFeature occlusion;

        Pass pass;

        public override void Create() { pass = new Pass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents }; }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (occlusion && !occlusion.isActive) return;
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;
            renderer.EnqueuePass(pass);
        }

        sealed class Pass : ScriptableRenderPass
        {
            static readonly ShaderTagId[] Tags = { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };
            sealed class PassData { public RendererListHandle heroes; }

            public Pass() { profilingSampler = new ProfilingSampler("Hero AO exemption"); }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();
                var resources = frameData.Get<UniversalResourceData>();

                var tags = new System.Collections.Generic.List<ShaderTagId>(Tags);
                var drawing = RenderingUtils.CreateDrawingSettings(tags, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                var filtering = new FilteringSettings(RenderQueueRange.opaque, ~0, HeroRimLight.Layer);

                using var builder = renderGraph.AddRasterRenderPass<PassData>("Hero AO exemption", out var data, profilingSampler);
                // Same depth test and write as the opaque pass: the redraw lands exactly on the hero's own pixels.
                data.heroes = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering));
                builder.UseRendererList(data.heroes);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Write);
                if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                if (resources.additionalShadowsTexture.IsValid()) builder.UseTexture(resources.additionalShadowsTexture, AccessFlags.Read);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((PassData d, RasterGraphContext context) => context.cmd.DrawRendererList(d.heroes));
            }
        }
    }
}
