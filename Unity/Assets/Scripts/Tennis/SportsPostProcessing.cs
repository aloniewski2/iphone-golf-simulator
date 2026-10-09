using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.Tennis
{
    /// One owned volume per gameplay camera. Tier changes preserve tone mapping;
    /// only the expensive glow is dropped on Low.
    [DisallowMultipleComponent]
    public sealed class SportsPostProcessing : MonoBehaviour
    {
        public Volume Volume { get; private set; }
        Bloom bloom;
        public void Initialize(Volume volume)
        {
            Volume = volume;
            Volume.profile.TryGet(out bloom);
            Refresh();
        }
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (bloom) bloom.active = TennisQuality.Current != TennisQuality.Tier.Low;
        }
        void OnDestroy()
        {
            if (!Volume || !Volume.profile) return;
            foreach (var component in Volume.profile.components) if (component) Destroy(component);
            Destroy(Volume.profile);
        }
    }
}
