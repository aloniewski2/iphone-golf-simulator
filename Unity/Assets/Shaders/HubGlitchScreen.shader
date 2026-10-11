// The Plaza's bay screens (PLAN_MenuHub_WalkableWorld §3): a gameplay preview shown as a stylised memory. RGB split, scanlines,
// grain, bands that jump sideways now and then, a CRT-ish edge falloff. _Glitch 0..1 sets how unsettled it is (it calms while
// the match loads); _Flash adds a bright burst (a channel change).
Shader "GolfArcade/HubGlitchScreen"
{
    Properties
    {
        _MainTex ("Video", 2D) = "black" {}
        _Glitch ("Glitch", Range(0, 1)) = 0.55
        _Flash ("Flash", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Float) = 1.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST; float _Glitch; float _Flash; float4 _Tint; float _Brightness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes a) { Varyings v; v.positionCS = TransformObjectToHClip(a.positionOS.xyz); v.uv = TRANSFORM_TEX(a.uv, _MainTex); return v; }
            float Hash(float n) { return frac(sin(n) * 43758.5453); }
            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y, g = saturate(_Glitch + _Flash);
                float2 uv = i.uv;
                float band = floor(uv.y * 22.0), tick = floor(t * 8.0);
                float jump = step(1.0 - 0.22 * g, Hash(band * 13.1 + tick * 7.7));
                uv.x += jump * (Hash(band + tick * 3.1) - 0.5) * 0.14 * g;
                uv.y = frac(uv.y + 0.012 * g * step(0.93, Hash(tick * 1.7)));
                float split = 0.004 + 0.009 * g * (0.6 + 0.4 * sin(t * 21.0));
                half r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(split, 0)).r;
                half3 mid = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                half b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(split, 0)).b;
                half3 c = half3(r, mid.g, b);
                c *= 0.84 + 0.16 * sin(i.uv.y * 720.0 + t * 26.0);
                c += (Hash(i.uv.x * 937.1 + i.uv.y * 413.7 + frac(t) * 61.0) - 0.5) * (0.04 + 0.1 * g);
                float2 d = i.uv - 0.5; c *= saturate(1.18 - dot(d, d) * 1.7);
                c = lerp(c, half3(1, 1, 1), _Flash * 0.6);
                return half4(c * _Tint.rgb * _Brightness, 1);
            }
            ENDHLSL
        }
    }
}
