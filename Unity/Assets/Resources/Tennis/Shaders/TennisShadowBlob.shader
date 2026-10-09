// Bounded soft occlusion under players and the ball on the measured court plane.
Shader "GolfArcade/TennisShadowBlob"
{
    Properties
    {
        [MainTexture] _MainTex ("Falloff", 2D) = "white" {}
        _UseVertexFade ("Per-foot grounded fade", Float) = 0
        _Strength ("Strength", Range(0,1)) = .5
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1
        Pass
        {
            Name "CourtOcclusion"
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Strength,_UseVertexFade;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half fade:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=TRANSFORM_TEX(input.uv,_MainTex);
                output.fade=lerp(1,input.color.a,_UseVertexFade);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // RGBA32 stores identical falloff in every channel; this avoids
                // Alpha8 platform swizzles and samples one explicit data channel.
                half alpha=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).r * _Strength * input.fade;
                return half4((1-alpha).xxx,1);
            }
            ENDHLSL
        }
    }
}
