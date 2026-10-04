using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.Tennis
{
    /// The one rim light on the match heroes (the match and the locker view are the same scene, so one light serves both).
    /// A warm directional light, dimmer than the sun, no shadows, that sits behind the heroes as the camera sees them and shines back
    /// toward the camera from above and to its right, so a shoulder and the top of an arm catch an edge of light.
    /// It lives on its own rendering layer: MatchHeroLook puts the hero renderers on it and the character / cloth shaders honour it, so the
    /// court, the ball and the venue never receive this light (the pipeline asset's Use Rendering Layers is on for that).
    [DisallowMultipleComponent]
    public sealed class HeroRimLight : MonoBehaviour
    {
        /// Rendering layer 1, "Hero rim" (defined in ProjectSettings/TagManager; layer 0 is Default, which everything else is on). URP only honours defined layers.
        public const uint Layer = 1u << 1;
        public const float Intensity = 1.8f;   // the sun is 2.3
        public static readonly Color Colour = new Color(1f, .74f, .48f);
        /// Degrees: how far round to the camera's right the light sits behind the heroes, and how high above them.
        public float yaw = 38, elevation = 18;

        Light lit; Camera cam;

        /// The scene's rim light, made once (TennisLook.LightScene calls this next to the camera-side fill).
        public static HeroRimLight Ensure()
        {
            var go = GameObject.Find("Hero rim"); if (!go) go = new GameObject("Hero rim");
            var rim = go.GetComponent<HeroRimLight>(); if (!rim) rim = go.AddComponent<HeroRimLight>();
            rim.Configure(); return rim;
        }

        void Configure()
        {
            lit = GetComponent<Light>(); if (!lit) lit = gameObject.AddComponent<Light>();
            lit.type = LightType.Directional; lit.shadows = LightShadows.None;
            lit.color = Colour; lit.intensity = Intensity;
            lit.GetUniversalAdditionalLightData().renderingLayers = Layer;   // URP reads the layer from here (it syncs Light.renderingLayerMask)
            Aim();
        }

        void LateUpdate() => Aim();

        /// (Public so a proof that moves the camera and renders in the same frame can re-aim it first.)
        /// Direction the heroes are seen along (camera forward, level), turned toward the camera's right and raised: that is where the light is; it shines the other way.
        public void Aim()
        {
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;
            if (!cam) return;
            Vector3 forward = cam.transform.forward; forward.y = 0;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            float e = elevation * Mathf.Deg2Rad;
            Vector3 toLight = Quaternion.AngleAxis(yaw, Vector3.up) * forward * Mathf.Cos(e) + Vector3.up * Mathf.Sin(e);
            transform.rotation = Quaternion.LookRotation(-toLight);
        }
    }
}
