// The golf ball: glossy white with its dimples (Resources/Effects/ball_dimples_normal, a
// Higgsfield height map turned into a normal map), lit like the course. The normal map is
// decoded by hand (rgb × 2 − 1) — it is imported as an ordinary texture, and this way no
// Standard keyword variant has to survive the build's shader stripping.
Shader "GolfArcade/GolfBall"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Dimples ("Dimples (normal map)", 2D) = "bump" {}
        _Depth ("Dimple depth", Range(0, 2)) = 1
        _Glossiness ("Smoothness", Range(0, 1)) = 0.62
    }
    // Native golf uses URP. Keep the Built-in subshader for legacy previews.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Back

        HLSLINCLUDE
        #define GOLF_BALL 1
        #include "GolfLegacyURP.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GolfLegacyVertex
            #pragma fragment GolfLegacyFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GolfLegacyShadowVertex
            #pragma fragment GolfLegacyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _Dimples;
        fixed4 _Color;
        half _Depth, _Glossiness;
        struct Input { float2 uv_Dimples; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half3 n = tex2D(_Dimples, IN.uv_Dimples).rgb * 2 - 1;
            n.xy *= _Depth;
            o.Normal = normalize(n);
            o.Albedo = _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
