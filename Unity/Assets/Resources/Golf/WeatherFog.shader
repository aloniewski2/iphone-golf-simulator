// Fog that lies on the ground: for every pixel, the world behind it from the camera's depth, and
// the fog the view ray passes through on its way there — thick at `_Base` (a little under the
// hole's ground) and thinning by `_Falloff` a yard as it climbs, so sea fog fills the coves and the
// low fairway while the cliff tops and the tall pines stand out of it; drifting banks of it from
// noise; brighter where the ray looks toward the sun. Drawn by WeatherView over the scene and the
// sky alike (the sky is "far away": the fog closes it at the horizon), after the cloud dome. Rain
// and snow lay a lighter one down, a blizzard a white one.
Shader "GolfArcade/WeatherFog"
{
    Properties
    {
        _FogColor ("Fog", Color) = (0.8, 0.83, 0.85, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunDir ("Toward the sun", Vector) = (0, 0.5, 1, 0)
        _Density ("Density at its base (per yard)", Float) = 0.02
        _Falloff ("Thinning per yard up", Float) = 0.06
        _Base ("Its base (world height)", Float) = 0
        _Banks ("Drifting banks", Range(0, 1)) = 0.7
        _Drift ("Drift (x, z) yards a second", Vector) = (1, 0.4, 0, 0)
        _Near ("Clear for this many yards", Float) = 2
        _Far ("The sky's distance", Float) = 1500
        _MaxFog ("Most it hides", Range(0, 1)) = 0.985
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-470" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Front
        Pass
        {
            Name "Fog"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "WeatherCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor, _SunColor;
                float4 _SunDir, _Drift;
                float _Density, _Falloff, _Base, _Banks, _Near, _Far, _MaxFog;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }

            half4 frag(V i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool sky = raw <= 1e-6;
                #else
                    raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                    bool sky = raw >= 1 - 1e-6;
                #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 ray = world - _WorldSpaceCameraPos;
                float length0 = length(ray);
                float3 d = ray / max(length0, 1e-4);
                float far = sky ? _Far : min(length0, _Far);
                float run = max(far - _Near, 0);
                float3 from = _WorldSpaceCameraPos + d * _Near;

                // the fog along the ray: density × e^(−falloff × height above its base), integrated
                float k = max(_Falloff, 1e-4);
                float base = _Density * exp(clamp(-k * (from.y - _Base), -40.0, 8.0));
                float climb = k * d.y;
                float depth = abs(climb) < 1e-5 ? base * run : base * (1 - exp(clamp(-climb * run, -40.0, 40.0))) / climb;

                // banks drifting through it: thicker and thinner, read partway along the ray
                float3 probe = from + d * min(run, 140.0) * 0.6;
                float banks = WeatherFbm(probe.xz * 0.014 + _Drift.xy * _Time.y * 0.012, 3);
                depth *= lerp(1.0, 0.25 + 1.5 * banks, _Banks);

                float fog = min(1 - exp(-max(depth, 0)), _MaxFog);
                half3 col = _FogColor.rgb + _SunColor.rgb * pow(saturate(dot(d, normalize(_SunDir.xyz))), 8) * 0.3;
                // lightning lights the fog too
                col += _GolfWeatherFlashColor.rgb * _GolfWeatherFlash * 0.25;
                return half4(col, fog);
            }
            ENDHLSL
        }
    }
}
