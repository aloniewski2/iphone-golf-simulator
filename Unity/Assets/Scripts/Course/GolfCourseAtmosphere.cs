using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Profiles share the postcard lighting model while retaining the legacy courses' settings.
    public static class GolfCourseAtmosphere
    {
        static readonly Dictionary<int, GolfAtmosphere.Look> profiles = new();
        public static GolfAtmosphere.Look For(int number)
        {
            if (!GolfCourseLook.Handles(number)) return null;
            if (profiles.TryGetValue(number, out var found)) return found;
            bool volcanic = number == 16 || number >= 21;
            var look = GolfAtmosphere.Holes[volcanic ? 10 : 9].Copy();
            look.Name = volcanic ? "Volcanic" : "Coastal";
            look.SunIntensity = 2.05f;
            look.RimIntensity = volcanic ? 0 : .25f;
            look.FogStart = 280; look.FogEnd = volcanic ? 2300 : 1600;
            look.BallExposure = Color.white; look.BallSelfLight = volcanic ? .10f : .025f;
            if (number == 14)
            {
                look.Name = "Woodland crater"; look.SunElevation = 32;
                look.SunColor = new Color(.93f,.92f,1); look.AmbientEquator = new Color(.46f,.53f,.57f);
                look.FogColor = new Color(.57f,.68f,.73f);
            }
            if (number == 17)
            {
                look.Name = "Alpine"; look.SunColor = new Color(.92f,.96f,1); look.SunElevation = 36;
                look.SunIntensity = 1.65f; look.AmbientEquator = new Color(.57f,.66f,.72f);
                look.AmbientGround = new Color(.35f,.43f,.51f); look.FogColor = new Color(.74f,.85f,.93f);
                look.ShadowStrength = .60f;
            }
            if (number == 18)
            {
                look.Name = "Desert"; look.SunColor = new Color(1,.90f,.73f); look.SunElevation = 38;
                look.AmbientEquator = new Color(.65f,.54f,.43f); look.AmbientGround = new Color(.38f,.28f,.21f);
                look.FogColor = new Color(.84f,.72f,.60f); look.CoolSky = false; look.SkyHeadingOffset = 30;
            }
            if (number == 19)
            {
                look.Name = "Jungle lagoon"; look.SunElevation = 45; look.FillIntensity = .65f;
                look.AmbientEquator = new Color(.44f,.58f,.49f); look.AmbientGround = new Color(.19f,.29f,.22f);
                look.FogColor = new Color(.60f,.77f,.73f); look.FogStart = 300; look.FogEnd = 1300;
            }
            if (number == 20) { look.Name = "Windmill links"; look.SunElevation = 34; look.SunColor = new Color(1,.94f,.81f); }
            profiles.Add(number,look); return look;
        }
    }
}
