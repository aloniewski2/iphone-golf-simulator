// Weather over the hole's own sky: a dome of cloud drawn just behind everything solid and over the
// sky (the sky itself is untouched), so a hole's painted sky can cloud over, darken for a storm and
// clear again by fading `_Coverage`. The deck is domain-warped noise read where the view ray meets
// a plane overhead, lit from the sun's side (bright rims, dark heavy bellies, a glow through thin
// cloud), with a faster, lower layer of scud blowing under it in a storm, grey curtains of rain
// hanging from it toward the horizon, lightning lighting the cloud round the strike from inside,
// fog's glow toward the sun, and the rainbow the clearing sky leaves (a primary bow red outside,
// the fainter secondary reversed, the dark band between them, the sky brighter inside). Clouds are
// premultiplied so the rainbow's light can be added. No fog: it is the sky. Drawn by WeatherView.
Shader "GolfArcade/StormSky"
{
    Properties
    {
        _Dark ("Cloud underside", Color) = (0.33, 0.37, 0.43, 1)
        _Light ("Cloud tops", Color) = (0.62, 0.66, 0.72, 1)
        _Horizon ("Haze at the horizon", Color) = (0.62, 0.66, 0.7, 1)
        _FlashColor ("Flash", Color) = (0.85, 0.88, 1, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunDir ("Toward the sun", Vector) = (0, 0.5, 1, 0)
        _FlashDir ("Toward the strike", Vector) = (0, 0.3, 1, 0)
        _RainbowDir ("The rainbow's centre", Vector) = (0, -0.25, 1, 0)
        _Coverage ("Coverage", Range(0, 1)) = 0
        _Cloudiness ("How much of the sky is cloud", Range(0, 1)) = 1
        _Storm ("Storm", Range(0, 1)) = 0
        _Shafts ("Rain shafts", Range(0, 1)) = 0
        _Mist ("Fog", Range(0, 1)) = 0
        _Flash ("Flash", Range(0, 1)) = 0
        _Rainbow ("Rainbow", Range(0, 1)) = 0
        _Drift ("Drift (x, z) yards a second", Vector) = (2, 1, 0, 0)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-480" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Front
        Pass
        {
            Name "Clouds"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "WeatherCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Dark, _Light, _Horizon, _FlashColor, _SunColor;
                float4 _SunDir, _FlashDir, _RainbowDir, _Drift;
                float _Coverage, _Cloudiness, _Storm, _Shafts, _Mist, _Flash, _Rainbow;
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

            // red outside to violet inside, along a bow 0 (inner edge) to 1 (outer)
            float3 Spectrum(float x)
            {
                float h = (1 - saturate(x)) * 0.78;
                return saturate(abs(h * 6 - float3(3, 2, 4)) * float3(1, -1, -1) + float3(-1, 2, 2));
            }

            // the bows' light at `angle` degrees from the rainbow's centre, and how much the band between them darkens
            float3 Rainbow(float angle, out float band)
            {
                float x1 = (angle - 40.2) / 2.6;                        // primary: violet 40°, red 42.8°
                float b1 = saturate(1 - abs(x1 - 0.5) * 2); b1 = b1 * b1 * (3 - 2 * b1);
                float x2 = (54.0 - angle) / 3.6;                        // secondary: reversed, wider and fainter
                float b2 = saturate(1 - abs(x2 - 0.5) * 2); b2 = b2 * b2 * (3 - 2 * b2);
                band = smoothstep(42.4, 44.0, angle) * smoothstep(51.0, 49.6, angle);   // Alexander's dark band
                float inside = smoothstep(40.6, 28.0, angle) * smoothstep(0.0, 8.0, angle);
                return Spectrum(x1) * b1 + Spectrum(x2) * b2 * 0.32 + inside * 0.07;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.direction);
                float t = _Time.y;
                float3 sun = normalize(_SunDir.xyz);
                float sunAmt = saturate(dot(d, sun));

                // the cloud deck: a plane overhead, read where the view ray meets it, its shapes warped by its own noise
                float up = max(d.y, 0.0) + 0.05;
                float2 deck = d.xz / up * 0.8;
                float2 drift = _Drift.xy * 0.004;
                float2 p = deck * 0.5 + drift * t;
                float2 warp = float2(WeatherFbm(p * 0.8 + 3.1, 3), WeatherFbm(p * 0.8 + 7.7 + t * 0.004, 3)) - 0.5;
                float2 shaped = p + warp * (1.1 + 0.9 * _Storm);
                float density = WeatherFbm(shaped, 5);
                // toward the sun the cloud is thinner where it is lit: a rim of light on the sun's side
                float2 toSun = normalize(sun.xz + 1e-4) * 0.09;
                float lit = saturate((density - WeatherFbm(shaped + toSun, 4)) * 5 + 0.5);
                float threshold = lerp(0.66, 0.2, _Cloudiness);
                float mass = smoothstep(threshold, threshold + 0.24, density);
                float thick = saturate((density - threshold) * 2.2);

                half3 col = lerp(_Light.rgb, _Dark.rgb, thick);
                col *= lerp(1.0, 1.28, lit * (1 - 0.6 * _Storm));
                col += _SunColor.rgb * lit * pow(sunAmt, 3) * 0.3 * (1 - _Storm);
                // the sun glowing through thin cloud
                col += _SunColor.rgb * pow(sunAmt, 12) * (1 - thick) * 0.5 * (1 - _Storm);

                // scud: torn low cloud racing under the deck in a storm
                float2 q = deck * 1.5 + drift * t * 3.2;
                float scud = smoothstep(0.52, 0.78, WeatherFbm(q + warp * 0.6, 4)) * _Storm;
                col = lerp(col, _Dark.rgb * 0.78, scud * 0.65);

                // the haze closes the sky right down to the horizon
                float toHorizon = 1 - saturate(d.y * 3.0);
                col = lerp(col, _Horizon.rgb, toHorizon * 0.8);

                // rain shafts: grey curtains hanging below the deck out toward the horizon, slanting with the wind
                float azimuth = atan2(d.x, d.z);
                float slant = d.y * 2.5 * sign(_Drift.x + 1e-4);
                float curtains = WeatherNoise(float2(azimuth * 7 + slant, t * 0.04)) * 0.65 + WeatherNoise(float2(azimuth * 31 + slant * 2, t * 0.08)) * 0.35;
                float shafts = smoothstep(0.42, 0.72, curtains) * saturate(1 - abs(d.y - 0.05) * 6) * _Shafts;
                col = lerp(col, _Dark.rgb * 0.82, shafts * 0.6);

                // lightning lights the cloud from inside, brightest round the strike
                float nearStrike = pow(saturate(dot(d, normalize(_FlashDir.xyz))), 9);
                col += _FlashColor.rgb * _Flash * (0.22 + 1.7 * nearStrike) * (0.35 + 0.65 * max(mass, scud));

                float alpha = _Coverage * lerp(0.4, 1.0, max(mass, scud));
                alpha = lerp(alpha, _Coverage, toHorizon);
                alpha = max(alpha, shafts * _Coverage);

                // fog: the sky is the fog, glowing toward the sun
                half3 mist = _Horizon.rgb + _SunColor.rgb * pow(sunAmt, 5) * 0.28;
                col = lerp(col, mist, _Mist * 0.9);
                alpha = max(alpha, _Mist);

                // below the horizon, past the sea's far edge, all haze
                col = lerp(col, _Horizon.rgb, saturate(-d.y * 30));

                // the rainbow, light added over whatever sky is behind it, fading where cloud covers it
                float band;
                float angle = degrees(acos(clamp(dot(d, normalize(_RainbowDir.xyz)), -1.0, 1.0)));
                float3 bow = Rainbow(angle, band) * _Rainbow * (1 - 0.75 * alpha) * smoothstep(-0.01, 0.07, d.y);
                // (the dark band between the bows is a touch of black laid over the sky there)
                float dark = band * 0.07 * _Rainbow;
                return half4(col * alpha + bow * 0.8, saturate(alpha + dark * (1 - alpha)));
            }
            ENDHLSL
        }
    }
}
