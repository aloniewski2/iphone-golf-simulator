using UnityEngine;
using UnityEngine.SceneManagement;

namespace GolfArcade.Tennis
{
    /// Scene-level look values. No material keywords or sport-specific shader copies.
    public static class HeroLightingProfile
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            Tennis();
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        static void SceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.name!="Golf")Tennis();
        }
        static void Presentation()
        {
            Shader.SetGlobalFloat("_HeroFilmResponse",1);
            Shader.SetGlobalFloat("_HeroSkinPolish", System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SKIN_POLISH")=="1" ? 1f : 0f);
            Shader.SetGlobalFloat("_HeroPresentationFill", System.Environment.GetEnvironmentVariable("VISUAL_CHARACTER_STAGE_LIGHT")=="0" ? 0f : .12f);
        }
        public static void Tennis() { Shader.SetGlobalFloat("_HeroProfile",0); Presentation(); }
        static void Colour(string property,Color value)
        {
            var c=value.linear;
            Shader.SetGlobalVector(property,new Vector4(c.r,c.g,c.b,1));
        }
        public static void Golf(Light sun,Color sky,Color equator,Color ground,float rim,float clothExposure=3f)
        {
            Presentation();
            Shader.SetGlobalFloat("_HeroProfile",1);
            Colour("_HeroKeyColor",sun?sun.color:Color.white);
            Shader.SetGlobalFloat("_HeroKeyStrength",sun?sun.intensity:1.1f);
            Colour("_HeroAmbientSky",sky);Colour("_HeroAmbientEquator",equator);Colour("_HeroAmbientGround",ground);
            // Material rim is a surface response, separate from the course's
            // directional rim light. Keep each authored material's strength.
            Shader.SetGlobalFloat("_HeroRim",1f);
            Shader.SetGlobalFloat("_HeroExposure",1f);
            Shader.SetGlobalFloat("_HeroShoulderKnee",.88f);
            Shader.SetGlobalFloat("_HeroShoulderCeiling",.985f);
            // Let neutral garments inherit the course lighting without inverse tint compensation.
            Shader.SetGlobalVector("_HeroClothBalance",Vector4.one);
            Shader.SetGlobalFloat("_HeroClothExposure",clothExposure);
        }
    }
}
