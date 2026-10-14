using UnityEngine;
using UnityEngine.Rendering.Universal;
using GolfArcade.Tennis;

namespace GolfArcade.Game
{
    /// The introductions' lens: a soft background behind the character (far blur only, so the graphics drawn at the lens
    /// stay sharp), on the camera's own post-processing volume; and off again.
    public static class IntroLens
    {
        static DepthOfField Dof(Camera camera)
        {
            var post = camera ? camera.GetComponent<SportsPostProcessing>() : null;
            return post && post.Volume && post.Volume.profile.TryGet(out DepthOfField dof) ? dof : null;
        }
        /// Sharp to `distance` (the character), soft from just behind them.
        public static void Focus(Camera camera, float distance)
        {
            var dof = Dof(camera); if (!dof) return;
            dof.active = true; dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(distance + 1.2f); dof.gaussianEnd.Override(distance + 9f);
            dof.gaussianMaxRadius.Override(1.4f); dof.highQualitySampling.Override(true);
        }
        public static void Off(Camera camera) { var dof = Dof(camera); if (dof) dof.active = false; }
    }
}
