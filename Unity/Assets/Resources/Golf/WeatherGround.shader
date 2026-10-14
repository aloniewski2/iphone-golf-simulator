// The weather on the ground, laid over the finished picture (the course's own materials are left
// exactly as they are): from the camera's depth, the world under each pixel and which way it faces.
// Rain darkens everything a little and turns what faces up glossy — the sky in it at a glance,
// a glint of sun, puddles gathering in patches, raindrops ringing them near the camera. Snow (or
// ash in the crater) settles white (or grey) on whatever faces up, thickest on the flats. Drawn by
// WeatherView before the cloud dome and the fog; nothing where the sky shows.
Shader "GolfArcade/WeatherGround"
{
    Properties
    {
        _Wetness ("Wet", Range(0, 1)) = 0
        _Dusting ("Settled snow or ash", Range(0, 1)) = 0
        _DustColor ("Snow or ash", Color) = (0.93, 0.95, 0.98, 1)
        _SkyColor ("Sky reflected", Color) = (0.55, 0.6, 0.66, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunDir ("Toward the sun", Vector) = (0, 0.5, 1, 0)
        _Raining ("Drops falling", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-490" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front
        Pass
        {
            Name "Ground"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "WeatherCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DustColor, _SkyColor, _SunColor;
                float4 _SunDir;
                float _Wetness, _Dusting, _Raining;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }

            // a raindrop's ring spreading on standing water: how much it tilts the surface at `p` (yards), and which way
            float2 Ripples(float2 p)
            {
                float2 tilt = 0;
                // two grids of drops, the second offset half a cell, each drop on its own beat
                [unroll] for (int k = 0; k < 2; k++)
                {
                    float2 g = p * 1.6 + k * 0.5;
                    float2 id = floor(g) + k * 37.0;
                    float phase = frac(_Time.y * 0.9 + WeatherHash(id));
                    float2 centre = 0.5 + (float2(WeatherHash(id + 3.7), WeatherHash(id + 9.1)) - 0.5) * 0.6;
                    float2 off = frac(g) - centre;
                    float r = length(off);
                    float ring = sin((r - phase * 0.55) * 60) * smoothstep(0.12, 0.0, abs(r - phase * 0.55)) * (1 - phase);
                    tilt += off / max(r, 1e-3) * ring;
                }
                return tilt;
            }

            half4 frag(V i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 1e-6) return 0;
                #else
                    raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                    if (raw >= 1 - 1e-6) return 0;
                #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 toCamera = _WorldSpaceCameraPos - world;
                float distance = length(toCamera);
                float3 v = toCamera / max(distance, 1e-4);
                // which way it faces, from the depth around it (a little noisy far off and at edges: faded there)
                float3 n = normalize(cross(ddy(world), ddx(world)));
                if (dot(n, v) < 0) n = -n;
                float up = smoothstep(0.72, 0.95, n.y);
                float near = saturate(1 - distance / 90.0);
                float mid = saturate(1 - distance / 420.0);

                // wet: darker all over; glossy where it faces up; puddles in patches
                float wet = _Wetness;
                float puddle = wet * up * smoothstep(0.5, 0.64, WeatherFbm(world.xz * 0.05, 3)) * mid;
                float3 nr = n;
                if (_Raining > 0.01 && near > 0)
                {
                    float2 tilt = Ripples(world.xz) * 0.22 * _Raining * near * (0.35 + 0.65 * saturate(puddle * 3));
                    nr = normalize(n + float3(tilt.x, 0, tilt.y));
                }
                float fresnel = pow(1 - saturate(dot(nr, v)), 5);
                // wet grass: a sheen at a glance; standing water: the sky in it
                float sheen = wet * up * mid * 0.65;
                float3 h = normalize(normalize(_SunDir.xyz) + v);
                float glint = pow(saturate(dot(nr, h)), 220) * 3;
                half3 wetLight = (_SkyColor.rgb * (0.04 + 0.96 * fresnel) + _SunColor.rgb * glint) * sheen
                               + _SkyColor.rgb * (0.22 + 0.78 * fresnel) * puddle * 0.9;
                float darken = wet * 0.24 + puddle * 0.45;

                // snow or ash settling on what faces up, patchy at its edges
                float settle = _Dusting * smoothstep(0.5, 0.92, n.y);
                settle *= smoothstep(0.25, 0.6, settle + (WeatherFbm(world.xz * 0.35, 3) - 0.5) * 0.6);
                half3 dust = _DustColor.rgb * lerp(0.82, 1.0, saturate(dot(n, normalize(_SunDir.xyz))));

                // premultiplied: the dust covers, the wet darkens what shows through and adds its sheen
                float alpha = 1 - (1 - settle) * (1 - darken);
                half3 rgb = dust * settle + wetLight * (1 - settle);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
