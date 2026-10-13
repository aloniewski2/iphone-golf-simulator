// Weather over the hole's own sky: a dome of drifting cloud drawn just behind everything solid
// and over the sky (the sky itself is untouched), so a hole's painted sky can cloud over, darken
// for a storm and clear again by fading `_Coverage`. Grey-blue cloud masses, brighter on their
// tops, thinning to the haze (the fog's colour, `_Horizon`) at the horizon; `_Flash` is lightning
// lighting them from inside. No fog: it is the sky. Drawn by WeatherView round the camera.
Shader "GolfArcade/StormSky"
{
    Properties
    {
        _Dark ("Cloud underside", Color) = (0.33, 0.37, 0.43, 1)
        _Light ("Cloud tops", Color) = (0.62, 0.66, 0.72, 1)
        _Horizon ("Haze at the horizon", Color) = (0.62, 0.66, 0.7, 1)
        _FlashColor ("Flash", Color) = (0.85, 0.88, 1, 1)
        _Coverage ("Coverage", Range(0, 1)) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _Drift ("Drift (x, z) yards a second", Vector) = (2, 1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-480" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Front
        Pass
        {
            Name "Clouds"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Dark, _Light, _Horizon, _FlashColor;
                float _Coverage, _Flash;
                float4 _Drift;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            V vert(A i)
            {
                V o;
                float3 world = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(world);
                o.direction = world - _WorldSpaceCameraPos;
                return o;
            }
            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { v += a * noise(p); p = p * 2.03 + float2(17.1, 9.3); a *= 0.5; }
                return v;
            }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.direction);
                // the cloud deck: a plane overhead, read where the view ray meets it
                float up = max(d.y, 0.0) + 0.06;
                float2 deck = d.xz / up * 0.9 + _Drift.xy * _Time.y * 0.004;
                float n = fbm(deck * 0.55);
                float n2 = fbm(deck * 1.7 + n * 1.3 - _Time.y * 0.01);
                float mass = saturate((n * 0.7 + n2 * 0.45) * 1.25 - 0.18);
                half3 cloud = lerp(_Dark.rgb, _Light.rgb, saturate(n2 * 1.3 - 0.2));
                // toward the horizon the deck thins into the haze
                float toHorizon = 1 - saturate(d.y * 3.2);
                cloud = lerp(cloud, _Horizon.rgb, toHorizon * 0.85);
                cloud += _FlashColor.rgb * _Flash * (0.35 + 0.65 * mass);
                float alpha = _Coverage * lerp(0.82, 1.0, mass);
                alpha = lerp(alpha, _Coverage, toHorizon);   // the haze closes the sky right down to the horizon
                // below it, past the sea's far edge, all haze (the sea and the ground nearer are in front of it anyway)
                cloud = lerp(cloud, _Horizon.rgb, saturate(-d.y * 30));
                return half4(cloud, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
