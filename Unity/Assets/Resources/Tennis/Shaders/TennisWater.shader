// Stylized sea: shallow turquoise near the shore deepening to blue, gentle procedural swell
// in the normals, a sky-tinted fresnel, and sun sparkle that bloom picks up.
Shader "GolfArcade/TennisWater"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.05,0.75,0.72,1)
        _Deep ("Deep", Color) = (0.02,0.28,0.55,1)
        _Sky ("Sky reflection", Color) = (0.55,0.75,0.95,1)
        _DeepDistance ("Deepening distance", Float) = 60
        _WaveScale ("Wave scale", Float) = 0.35
        _Sparkle ("Sparkle", Float) = 6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Sky; float _DeepDistance, _WaveScale, _Sparkle;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };
            V vert (A i) { V o; VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz); o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.fog = ComputeFogFactor(p.positionCS.z); return o; }
            float2 wave (float2 p, float2 dir, float freq, float speed)
            {
                float phase = dot(p, dir) * freq + _Time.y * speed;
                return dir * cos(phase) * freq;
            }
            half4 frag (V i) : SV_Target
            {
                float2 p = i.positionWS.xz * _WaveScale;
                float2 slope = wave(p, normalize(float2(1, .3)), 1.3, 1.1) * .06 + wave(p, normalize(float2(-.4, 1)), 2.1, 1.7) * .04
                             + wave(p, normalize(float2(.7, -.8)), 3.7, 2.3) * .025 + wave(p, normalize(float2(-1, -.2)), 6.1, 3.1) * .015;
                half3 n = normalize(half3(-slope.x, 1, -slope.y));
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float distance = length(i.positionWS.xz);
                half3 water = lerp(_Shallow.rgb, _Deep.rgb, saturate((distance - 20) / _DeepDistance));
                half fresnel = pow(1 - saturate(dot(n, v)), 4);
                Light sun = GetMainLight();
                half3 h = normalize(sun.direction + v);
                half sparkle = pow(saturate(dot(n, h)), 380) * _Sparkle;
                half3 lit = water * (SampleSH(n) + sun.color * saturate(dot(n, sun.direction)) * .5);
                half3 colour = lerp(lit, _Sky.rgb, fresnel * .6) + sun.color * sparkle;
                return half4(MixFog(colour, i.fog), 1);
            }
            ENDHLSL
        }
    }
}
