// The Plaza's water (country-club look): the calm sea round the island, the fountain basins, the pond. Opaque and cheap: the
// base colour (base map x colour), a few moving sine ripples for the normal, a sky-coloured fresnel, the low sun's glitter
// (a sharp highlight broken into twinkling cells) and the main light's shadow. No depth or opaque texture reads.
Shader "GolfArcade/HubWater"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (.2, .45, .65, 1)
        _SkyColor ("Sky reflection", Color) = (.75, .82, .92, 1)
        _Ripple ("Ripple strength", Range(0, 1)) = .25
        _RippleScale ("Ripple frequency (1/m)", Float) = 1.5
        _Speed ("Ripple speed", Float) = 1
        _Glitter ("Sun glitter", Float) = 2
        _GlitterCells ("Glitter cells (1/m)", Float) = 3
        _Fresnel ("Fresnel", Range(0, 1)) = .5
        _SunDir ("Glitter sun direction (0 = the main light)", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor, _SkyColor;
            half _Ripple, _RippleScale, _Speed, _Glitter, _GlitterCells, _Fresnel; float4 _SunDir;
        CBUFFER_END
        struct A { float4 p : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
        struct V { float4 p : SV_POSITION; float3 w : TEXCOORD0; float2 uv : TEXCOORD1; half3 n : TEXCOORD2; };
        V vert(A a) { V o; o.w = TransformObjectToWorld(a.p.xyz); o.p = TransformWorldToHClip(o.w); o.uv = TRANSFORM_TEX(a.uv, _BaseMap); o.n = TransformObjectToWorldNormal(a.n); return o; }
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * _Speed; float2 q = i.w.xz * _RippleScale;
                float2 g = float2(cos(q.x * 1.3 + t * 1.1) + .6 * cos(q.x * .7 - q.y * 1.9 + t * 1.7), cos(q.y * 1.1 - t * .9) + .6 * cos(q.y * 2.3 + q.x * .8 + t * 1.3));
                half3 n = normalize(normalize(i.n) + half3(g.x, 0, g.y) * _Ripple * .25);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(i.w));
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.w));
                half shade = lerp(.55h, 1.0h, sun.shadowAttenuation);
                half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half fres = pow(1 - saturate(dot(n, view)), 4) * _Fresnel;
                half3 c = lerp(base * (SampleSH(half3(0, 1, 0)) * .6 + sun.color * .35 * shade), _SkyColor.rgb, fres);
                half3 r = reflect(-view, n);
                half3 L = dot(_SunDir.xyz, _SunDir.xyz) > .01 ? normalize(_SunDir.xyz) : sun.direction;
                half spot = pow(saturate(dot(r, L)), 180);
                float2 cell = floor(i.w.xz * _GlitterCells);
                half twinkle = step(.82, Hash(cell + floor(t * 5 + Hash(cell) * 7)));
                c += sun.color * (spot * 1.5 + pow(saturate(dot(r, L)), 24) * twinkle * .9) * _Glitter * sun.shadowAttenuation;
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment depth
            half depth(V i) : SV_Target { return i.p.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment normals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 normals(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * .5 + .5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
