// Hair made of alpha-cut strand cards (MakeHuman's CC0 hairs, fitted to Adnan's heads: blender/scripts/matchhero_mhhair.py).
// The texture is the strands as grey (luminance / 1.6 of their average) with the card's cut-out in its alpha, so any hair colour
// tints it and the strands stay: albedo = _Color * grey * _Gain. Lit like HeroKit (a MatCap for the soft form shading and a wrapped
// sun) with a soft sheen added, the highlight that makes hair read as hair. Double sided: a card is seen from both faces, and the
// back face is lit with its normal turned round.
Shader "GolfArcade/HeroHairCard"
{
    Properties
    {
        _MainTex ("Strands (grey) with alpha", 2D) = "white" {}
        _Color ("Hair colour", Color) = (0.3, 0.2, 0.12, 1)
        _Gain ("Strand gain (the texture holds luminance / 1.6)", Float) = 1.6
        _Cutoff ("Alpha cut-off", Range(0, 1)) = 0.42
        _MatCap ("MatCap (grey)", 2D) = "white" {}
        _MatCapStrength ("How much the MatCap shades", Range(0, 1)) = 0.35
        _Wrap ("Light wrap", Range(0, 1)) = 0.6
        _Fill ("Fill light: a floor under the shadows", Range(0, 1)) = 0.10
        _Sheen ("Sheen: the highlight along the strands", Range(0, 1)) = 0.34
        _SheenPower ("Sheen tightness", Range(4, 96)) = 20
    }
    // Native golf uses URP. Keep the Built-in subshader for legacy previews.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #define GOLF_HAIR 1
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
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Hair fullforwardshadows addshadow alphatest:_Cutoff
        #pragma target 3.5
        #include "UnityPBSLighting.cginc"
        sampler2D _MainTex, _MatCap;
        fixed4 _Color;
        float _Gain, _MatCapStrength, _Wrap, _Fill, _Sheen, _SheenPower;

        // The vertex colour's A is the card's own edge: 0 on its open edge (hairline, round the ears, the hem), 1 a little way in (blender/scripts/matchhero_mhhair.py edge_alpha).
        // The texture's alpha and that edge are dissolved by a fine screen-space dither (interleaved gradient noise): a soft hairline at the phone's pixel size, not a cut line.
        struct Input { float2 uv_MainTex; float4 color : COLOR; float4 screenPos; float facing : VFACE; float3 worldNormal; INTERNAL_DATA };

        void surf(Input IN, inout SurfaceOutput o)
        {
            half4 t = tex2D(_MainTex, IN.uv_MainTex);
            // a card's back face is lit as the front of a card turned round
            o.Normal = float3(0, 0, IN.facing > 0 ? 1 : -1);
            float3 wn = WorldNormalVector(IN, o.Normal);
            float3 vn = normalize(mul((float3x3)UNITY_MATRIX_V, wn));
            half cap = tex2D(_MatCap, vn.xy * 0.49 + 0.5).r;
            half3 albedo = _Color.rgb * t.rgb * _Gain * (IN.facing > 0 ? 1.0 : 0.86);       // (a card's back a touch darker: no bright rim round the edge)
            o.Albedo = albedo * lerp(1, cap, _MatCapStrength);
            float a = t.a * IN.color.a;
            #ifdef UNITY_PASS_SHADOWCASTER
            o.Alpha = a > 0.5 ? 1 : 0;
            #else
            float p = saturate((a - 0.14) / 0.5);
            float2 sp = IN.screenPos.xy / max(IN.screenPos.w, 1e-4) * _ScreenParams.xy;
            float n = frac(52.9829189 * frac(dot(sp, float2(0.06711056, 0.00583715))));
            o.Alpha = p > n ? 1 : 0;
            #endif
        }

        half4 LightingHair(SurfaceOutput s, half3 viewDir, UnityGI gi)
        {
            half nl = dot(s.Normal, gi.light.dir);
            half lit = saturate((nl + _Wrap) / (1 + _Wrap));
            half3 h = normalize(gi.light.dir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), _SheenPower) * _Sheen * saturate(nl + 0.25);
            half4 c;
            c.rgb = s.Albedo * (gi.light.color * lit + _Fill) + gi.light.color * spec * (0.75 + 0.25 * s.Albedo);
            #ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
            c.rgb += s.Albedo * gi.indirect.diffuse;
            #endif
            c.a = s.Alpha;
            return c;
        }

        void LightingHair_GI(SurfaceOutput s, UnityGIInput data, inout UnityGI gi)
        {
            gi = UnityGlobalIllumination(data, 1.0, s.Normal);
        }
        ENDCG
    }
    FallBack "Transparent/Cutout/VertexLit"
}
