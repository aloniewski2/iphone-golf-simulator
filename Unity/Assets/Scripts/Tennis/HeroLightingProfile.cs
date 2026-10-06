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
        public static void Tennis() => Shader.SetGlobalFloat("_HeroProfile",0);
        static void Colour(string property,Color value)
        {
            var c=value.linear;
            Shader.SetGlobalVector(property,new Vector4(c.r,c.g,c.b,1));
        }
        public static void Golf(Light sun,Color sky,Color equator,Color ground,float rim,float clothExposure=3f)
        {
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
            // Balance neutral kit reflectance against the actual sun/sky energy.
            // Skin and eyes retain the unbalanced course key through the shared core.
            var key=(sun?sun.color:Color.white).linear;
            var fill=(sky.linear+equator.linear+ground.linear)/3f;
            var white=key*(sun?sun.intensity:1.1f)+fill;
            float peak=Mathf.Max(white.r,Mathf.Max(white.g,white.b));
            Shader.SetGlobalVector("_HeroClothBalance",new Vector4(peak/Mathf.Max(.1f,white.r),peak/Mathf.Max(.1f,white.g),peak/Mathf.Max(.1f,white.b),1));
            if(clothExposure>3f)Shader.SetGlobalVector("_HeroClothBalance",new Vector4(1.16f,1.06f,1,1));
            Shader.SetGlobalFloat("_HeroClothExposure",clothExposure);
        }
    }
}
