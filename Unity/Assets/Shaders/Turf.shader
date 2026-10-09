// The course's grass and sand, lit like the rest of the course (Standard): the surface's own palette colour (HoleView) with a grey detail
// tile from blender/scripts/turf_textures.py laid over it in world space — fairway grain, the
// green's mowing stripes, the rough's tufts, the sand's speckle — so the ground reads as a
// surface at every distance and the ball has something to be seen against. A second sample
// of the same tile at a much larger scale breaks up the repeat across the hole.
Shader "GolfArcade/Turf"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Detail ("Detail (grey, 0.5 = no change)", 2D) = "gray" {}
        _Tile ("Yards per tile", Float) = 8
        _Strength ("How much the detail shows", Range(0, 2)) = 0.5
    }
    // Native golf uses URP. Keep the Built-in subshader for legacy previews.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Back

        HLSLINCLUDE
        #define GOLF_TURF 1
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
        sampler2D _Detail;
        fixed4 _Color;
        float _Tile, _Strength;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz / _Tile;
            half near = tex2D(_Detail, uv).r - 0.5;
            half far = tex2D(_Detail, uv * 0.131 + 0.37).r - 0.5;
            o.Albedo = _Color.rgb * (1 + _Strength * (near * 1.5 + far * 0.5));
            o.Metallic = 0;
            o.Smoothness = 0.1;      // as the flat course materials: lit the same, only the grain is new
        }
        ENDCG
    }
    FallBack "Diffuse"
}
