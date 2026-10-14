// What falls in the weather — rain streaks, snowflakes, ash — tinted by the particle's colour:
// faded out close to the lens (a drop an inch from it is a blur, not a streak) and where it meets
// the ground or anything standing (the camera's depth), lit up by a lightning flash. Blended over
// the picture; fogged with the course.
Shader "GolfArcade/WeatherParticle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Near ("Fade in from the lens (yards)", Float) = 1.2
        _Soft ("Soft at surfaces (yards)", Float) = 0.6
        _FlashLift ("Lit by lightning", Float) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Precipitation"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "WeatherCommon.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Near, _Soft, _FlashLift;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; half fog : TEXCOORD1; };
            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex); o.color = i.color;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float here = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                float scene = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams);
                c.a *= saturate((here - 0.3) / max(_Near, 0.01)) * saturate((scene - here) / max(_Soft, 0.01));
                c.rgb += _GolfWeatherFlashColor.rgb * _GolfWeatherFlash * _FlashLift;
                return half4(MixFog(c.rgb, i.fog), saturate(c.a));
            }
            ENDHLSL
        }
    }
}
