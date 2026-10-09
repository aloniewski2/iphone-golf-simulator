// Golf postcard whitewater, waterfall sheet and smoke cards (LK_SURF / LK_FALL / LK_SMOKE).
// v2 2026-10-04: _EdgeFade (cards fade out over the UV edge, default 0 = off), _AlphaGain (opacity multiplier before the contrast power, default 1 = as painted).
// v2 repair round 1 2026-10-04: _FogShare (how much of the distance fog the surface takes, default 1 = like every other surface; the smoke cards stand 400+ yd out on Crater, where the
// dark red-brown fog (.42,.19,.14) took 35 % of their colour and left a plume DARKER than the bright dusk sky behind it: a ghost, review finding "no readable smoke plume").
// v2 repair round 3 2026-10-05: _SoftFade (soft intersection: the alpha fades to 0 over this many world units in front of any opaque surface behind the card; default 0 = off). Review (medium, hole 10): the
// tee wisp had a straight diagonal edge down the cone flank - a vertical card that passes through the cone is cut by the depth test along the intersection line, a hard edge in the middle of a plume. Needs the depth
// texture (both URP assets have m_RequireDepthTexture 1); where it is missing the sampled depth is "far" and the fade is 1 (no fade), never a black card.
// STATIC on purpose (no _Time): the brief forbids flowing shaders. Alpha-blended, no depth write,
// fog-aware (per fragment), lit softly by the main light + ambient so foam is not a glowing white strip at dusk.
// Lives under Resources so a player build always carries it (Resources.Load<Shader>("Course/Shaders/GolfSurf")).
// Only multi_compile keywords (fog, instancing): nothing a build could strip away from a runtime material.
Shader "GolfArcade/GolfSurf"
{
    Properties
    {
        _MainTex ("Texture (RGBA)", 2D) = "white" {}
        _Color ("Tint (rgb) and opacity (a)", Color) = (1,1,1,1)
        _VertexAlpha ("Multiply alpha by vertex colour A (surf = 1)", Range(0,1)) = 0
        _AlphaPower ("Alpha contrast (1 = as painted)", Range(0.25,4)) = 1
        _AlphaGain ("Opacity gain on the painted alpha (1 = as painted; the plume's peak is scaled, its feathered edge stays feathered)", Range(0,3)) = 1
        _Lit ("Lit by sun + ambient (0 = unlit tint)", Range(0,1)) = 1
        _SunShare ("Share of the sun in the lighting", Range(0,1)) = 0.45
        _Emission ("Self light (keeps foam white in shade)", Range(0,2)) = 0.15
        _Wrap ("Two-sided wrap (sheets)", Range(0,1)) = 0.5
        _EdgeFade ("Fade the alpha to 0 over this share of the UV edge (cards: no hard rim; 0 = off)", Range(0,0.5)) = 0
        _SoftFade ("Soft intersection: fade the alpha to 0 over this many world units in front of an opaque surface behind the card (0 = off; smoke cards: a plume must not end in a straight line where it meets a wall)", Range(0,200)) = 0
        _FogShare ("Share of the distance fog this surface takes (1 = like the rest of the scene; smoke: lower, a far plume must not sink into the fog colour)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Pass
        {
            Name "GolfSurf"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _VertexAlpha, _AlphaPower, _AlphaGain, _Lit, _SunShare, _Emission, _Wrap, _EdgeFade, _FogShare, _SoftFade;
            CBUFFER_END

            struct A
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            V vert (A i)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.color = i.color;
                o.positionWS = p.positionWS;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                half alpha = pow(saturate(tex.a * _AlphaGain), _AlphaPower) * lerp(1.0h, i.color.a, _VertexAlpha);
                // cards (smoke): the painted alpha already feathers to 0, this guarantees no hard rim whatever the texture does at its border
                half edge = min(min(i.uv.x, 1.0h - i.uv.x), min(i.uv.y, 1.0h - i.uv.y));
                alpha *= _EdgeFade > 0.0001h ? smoothstep(0.0h, _EdgeFade, edge) : 1.0h;
                // soft intersection (smoke): distance, along the view ray, between this fragment and the opaque surface behind it
                if (_SoftFade > 0.0001h)
                {
                    float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams);
                    float fragEye = LinearEyeDepth(i.positionWS, GetWorldToViewMatrix());
                    alpha *= (half)saturate((sceneEye - fragEye) / _SoftFade);
                }
                // soft, two-sided light: wrap the main light so a sheet seen from behind is not black
                half3 n = normalize(i.normalWS);
                Light sun = GetMainLight();
                half ndl = dot(n, sun.direction);
                ndl = max(ndl, -ndl * _Wrap);              // both faces of a sheet catch the sun
                ndl = saturate(ndl * (1 - _Wrap * 0.5) + _Wrap * 0.5);
                half3 light = SampleSH(half3(0, 1, 0)) + sun.color * ndl * _SunShare + _Emission;
                half3 colour = tex.rgb * lerp(half3(1, 1, 1), light, _Lit);
                // fog per FRAGMENT (like URP Lit): a vertex fog factor is clamped at the vertices and goes wrong on big cards
                half fog = InitializeInputDataFog(float4(i.positionWS, 1), 0);
                return half4(lerp(colour, MixFog(colour, fog), _FogShare), saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
