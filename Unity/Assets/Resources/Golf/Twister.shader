// A twister's funnel wall: streaks of cloud wound round it and spun up and round as you watch (3D
// noise read on a turning spiral, so the wall never shows a seam), denser at its silhouette where
// the eye looks through more of it, lit from the sun's side, browner where it is full of dirt near
// the ground and soft where it meets the ground (the camera's depth), flashing with the lightning.
// Its mesh (MoverView) carries u round the funnel and v up it, and fades its top and foot in the
// vertex alpha. Fogged like the rest of the course.
Shader "GolfArcade/Twister"
{
    Properties
    {
        _Dark ("Cloud", Color) = (0.26, 0.28, 0.31, 1)
        _Light ("Lit cloud", Color) = (0.6, 0.62, 0.66, 1)
        _Dust ("Dirt", Color) = (0.42, 0.36, 0.28, 1)
        _Spin ("Spin", Float) = 2.2
        _Density ("Density", Range(0, 1)) = 1
        _Soft ("Soft at the ground (yards)", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            Name "Funnel"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "WeatherCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Dark, _Light, _Dust;
                float _Spin, _Density, _Soft;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; half4 color : COLOR; half fog : TEXCOORD3; };
            V vert(A i)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = i.uv; o.color = i.color;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float t = _Time.y;
                // round the funnel, wound up it, turning
                float angle = i.uv.x * 6.2831853 + i.uv.y * 4.0 - t * _Spin;
                float3 spiral = float3(cos(angle) * 1.3, sin(angle) * 1.3, i.uv.y * 5.0 - t * 0.9);
                float n = WeatherFbm3(spiral);
                float streaks = smoothstep(0.28, 0.78, n);
                float3 nrm = normalize(i.normalWS);
                float3 view = normalize(_WorldSpaceCameraPos - i.positionWS);
                float rim = 1 - abs(dot(nrm, view));
                float density = (0.58 + 0.42 * streaks) * (0.68 + 0.32 * rim) * _Density * i.color.a * 1.35;

                Light sun = GetMainLight();
                float lit = saturate(dot(nrm, sun.direction) * 0.5 + 0.5);
                half3 col = lerp(_Dark.rgb, _Light.rgb, saturate(n * 0.7 + i.uv.y * 0.35)) * lerp(0.7, 1.15, lit);
                col = lerp(_Dust.rgb, col, smoothstep(0.02, 0.28, i.uv.y));
                col += _GolfWeatherFlashColor.rgb * _GolfWeatherFlash * 0.7;

                // soft where it meets the ground
                float2 screen = GetNormalizedScreenSpaceUV(i.positionCS);
                float scene = LinearEyeDepth(SampleSceneDepth(screen), _ZBufferParams);
                float here = LinearEyeDepth(i.positionCS.z, _ZBufferParams);
                density *= saturate((scene - here) / max(_Soft, 0.01));

                return half4(MixFog(col, i.fog), saturate(density));
            }
            ENDHLSL
        }
    }
}
