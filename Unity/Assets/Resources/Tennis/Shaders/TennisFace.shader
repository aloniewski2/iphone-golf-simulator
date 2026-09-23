// Painted face on the face decal: one cell of the expression atlas, cut out with
// alpha-to-coverage so edges stay smooth under MSAA with no transparency sorting, and lit
// with the same soft wrap as the skin so the face sits in the same light as the head.
Shader "GolfArcade/TennisFace"
{
    Properties
    {
        [MainTexture] _BaseMap ("Expression atlas", 2D) = "black" {}
        _Cell ("Expression cell", Float) = 0
        _Grid ("Atlas columns, rows", Vector) = (4,2,0,0)
        _Wrap ("Wrap", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest+10" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            AlphaToMask On
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _Grid; float _Cell; half _Wrap;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; half fog : TEXCOORD3; };
            V vert (A i)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                float cell = floor(_Cell + 0.5);
                float2 index = float2(fmod(cell, _Grid.x), floor(cell / _Grid.x));
                // Row 0 of the atlas is at the top of the image.
                o.uv = float2((i.uv.x + index.x) / _Grid.x, 1 - (index.y + 1 - i.uv.y) / _Grid.y);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag (V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // Without MSAA, alpha-to-coverage does nothing; discard what is clearly empty.
                clip(c.a - 0.08);
                half3 n = normalize(i.normalWS);
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half wrapped = saturate((dot(n, main.direction) + _Wrap) / (1 + _Wrap));
                half3 lit = c.rgb * (main.color * wrapped * main.shadowAttenuation + SampleSH(n));
                return half4(MixFog(lit, i.fog), c.a);
            }
            ENDHLSL
        }
    }
}
