using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// The light, the haze and the sky a hole is played under. Every hole is under the resort's (GolfGame sets
    /// it up at start: a clear cyan sky, a bright sun, a pale haze); the Magma Open's holes, whose Theme is
    /// "magma", sit in a crater, so they get the fire's: a sun the colour of embers over the crater wall, an
    /// ambient that is lit orange from below by the lava, a sky of ash and fire (GolfArcade/SkyMagma) and a
    /// haze that reddens the far wall. HoleView.Build applies it whenever a hole is built, and the next hole
    /// puts the resort's back, so the course screen can browse from one to the other.
    public static class HoleAtmosphere
    {
        /// The haze, in yards: where it starts and ends. GolfGame pushes them out on the course screen.
        public static float FogStart = 320, FogEnd = 1100;
        /// How far the camera sees, yards: the crater's far wall is a kilometre off.
        public static float FarClip = 900;

        /// Tests can put any hole in the crater.
        public static bool Force;

        public static bool IsMagma(Hole hole) => hole != null && (Force || hole.Theme == "magma");

        // what the resort had, saved the first time a hole is built
        static bool saved;
        static Color sunColor, ambient, fogColor;
        static float sunIntensity, sunStrength;
        static Quaternion sunRotation;
        static AmbientMode ambientMode;
        static Material sky;
        static float defaultFogStart, defaultFogEnd, defaultFar;

        static Material magmaSky;

        static Light Sun()
        {
            var go = GameObject.Find("Sun");
            return go ? go.GetComponent<Light>() : null;
        }

        public static void Apply(Hole hole)
        {
            var sun = Sun();
            if (!saved) Save(sun);
            if (IsMagma(hole)) Magma(sun); else Restore(sun);
            foreach (var cam in Camera.allCameras)
                if (cam && cam.clearFlags == CameraClearFlags.Skybox) cam.farClipPlane = FarClip;
        }

        static void Save(Light sun)
        {
            saved = true;
            if (sun) { sunColor = sun.color; sunIntensity = sun.intensity; sunStrength = sun.shadowStrength; sunRotation = sun.transform.rotation; }
            ambientMode = RenderSettings.ambientMode; ambient = RenderSettings.ambientLight;
            fogColor = RenderSettings.fogColor; sky = RenderSettings.skybox;
            defaultFogStart = FogStart; defaultFogEnd = FogEnd; defaultFar = FarClip;
        }

        static void Restore(Light sun)
        {
            if (sun) { sun.color = sunColor; sun.intensity = sunIntensity; sun.shadowStrength = sunStrength; sun.transform.rotation = sunRotation; }
            RenderSettings.ambientMode = ambientMode; RenderSettings.ambientLight = ambient;
            RenderSettings.fogColor = fogColor; RenderSettings.skybox = sky;
            FogStart = defaultFogStart; FogEnd = defaultFogEnd; FarClip = defaultFar;
            RenderSettings.fogStartDistance = FogStart; RenderSettings.fogEndDistance = FogEnd;
        }

        static void Magma(Light sun)
        {
            if (!magmaSky)
            {
                var shader = Shader.Find("GolfArcade/SkyMagma");
                magmaSky = new Material(shader) { name = "Magma sky" };
                magmaSky.SetTexture("_Ash", Resources.Load<Texture2D>("Course/Lava/ash_cloud"));
                magmaSky.SetVector("_SunDir", LavaWorld.SunToward);
            }
            RenderSettings.skybox = magmaSky;
            if (sun)
            {
                sun.color = new Color(1f, 0.72f, 0.52f); sun.intensity = 1.25f; sun.shadowStrength = 0.7f;
                sun.transform.rotation = Quaternion.LookRotation(-LavaWorld.SunToward);
            }
            // the lava lights everything from below
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.32f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.42f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.70f, 0.30f, 0.12f);
            RenderSettings.fogColor = new Color(0.46f, 0.21f, 0.15f);
            FogStart = 380; FogEnd = 2300; FarClip = 2600;
            RenderSettings.fogStartDistance = FogStart; RenderSettings.fogEndDistance = FogEnd;
        }
    }
}
