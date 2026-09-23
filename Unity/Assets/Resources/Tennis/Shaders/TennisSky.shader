// Painted sky dome. A wide panorama (generated as a matte painting) is wrapped across the
// half of the sky the gameplay camera faces and mirrored behind, with a procedural gradient
// taking over below the painting's horizon and a soft sun glow on top.
Shader "GolfArcade/TennisSky"
{
    Properties
    {
        _Panorama ("Panorama", 2D) = "white" {}
        _Horizon ("Horizon colour", Color) = (1,0.86,0.7,1)
        _Zenith ("Zenith colour", Color) = (0.18,0.45,0.9,1)
        _SunDir ("Sun direction", Vector) = (-0.5,0.35,0.75,0)
        _SunColor ("Sun glow", Color) = (1,0.85,0.6,1)
        _Arc ("Degrees the painting spans", Float) = 180
        _Heading ("Heading of the painting's centre", Float) = 0
        _Exposure ("Exposure", Float) = 0.95
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Panorama); SAMPLER(sampler_Panorama);
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon, _Zenith, _SunColor; float4 _SunDir; float _Arc, _Heading, _Exposure;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert (A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.dir = i.positionOS.xyz; return o; }
            half4 frag (V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float elevation = asin(saturate(d.y)) / (PI * 0.5);           // 0 horizon .. 1 zenith
                // Heading relative to the painting's centre, folded with a triangle wave so the
                // painting mirrors seamlessly instead of showing a seam behind the camera.
                float heading = degrees(atan2(d.x, d.z)) - _Heading + _Arc * 0.5;
                float x = fmod(fmod(heading, 2 * _Arc) + 2 * _Arc, 2 * _Arc);
                float u = (x > _Arc ? 2 * _Arc - x : x) / _Arc;
                float v = saturate(elevation * 1.35);
                half3 painted = SAMPLE_TEXTURE2D(_Panorama, sampler_Panorama, float2(u, v)).rgb;
                half3 gradient = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(elevation), 0.55));
                half3 sky = lerp(gradient, painted, smoothstep(0.0, 0.06, elevation) * (1 - smoothstep(0.72, 0.74, elevation * 1.35)) );
                float sun = saturate(dot(d, normalize(_SunDir.xyz)));
                sky += _SunColor.rgb * (pow(sun, 64) * 1.5 + pow(sun, 6) * 0.25);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
