using UnityEngine;
using UnityEngine.Rendering;
namespace GolfArcade.Tennis
{
    /// A review-scene pipeline override; does not change saved quality/project settings.
    public sealed class HeroLookLighting : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previous;
        void OnEnable() {previous=QualitySettings.renderPipeline;if(pipeline)QualitySettings.renderPipeline=pipeline;}
        void OnDisable() {if(QualitySettings.renderPipeline==pipeline)QualitySettings.renderPipeline=previous;}
    }
}
