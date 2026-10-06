#ifndef GOLF_ARCADE_HERO_CLOTH_INCLUDED
#define GOLF_ARCADE_HERO_CLOTH_INCLUDED
// Shared cloth surface core. v1 map bytes and the tennis branch stay unchanged.
half ClothShadow(float3 world)
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                return MainLightRealtimeShadow(ComputeScreenPos(TransformWorldToHClip(world)));
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 sc = TransformWorldToShadowCoord(world);
                ShadowSamplingData data = GetMainLightShadowSamplingData();
                float2 stepUV = data.shadowmapSize.xy * .45;
                half a = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, sc.xyz + float3(stepUV,0));
                half b = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, sc.xyz - float3(stepUV,0));
                half attenuation = LerpWhiteTo((a+b)*.5h, GetMainLightShadowParams().x);
                if (BEYOND_SHADOW_FAR(sc)) attenuation=1;
                return lerp(attenuation, 1, GetMainLightShadowFade(world));
                #else
                return 1;
                #endif
            }
half3 Shade(Light light, half3 n, half3 v, half3 albedo, half smoothness, half sheenStrength, half ao)
            {
                half ndl = dot(n, light.direction), nl = saturate(ndl), nv = saturate(dot(n, v));
                half wrapped = saturate((ndl + _Wrap) / (1 + _Wrap));
                half3 h = SafeNormalize(light.direction + v);
                half nh = saturate(dot(n, h));
                half invR = rcp(max(1 - smoothness, .25h));
                half charlie = (2 + invR) * pow(max(1 - nh * nh, .0001h), invR * .5h) * .15915494h;
                half visibility = rcp(max(4 * (nl + nv - nl * nv), .08h));
                half3 sheen = albedo * (charlie * visibility * nl * sheenStrength);
                half spec = pow(nh, smoothness * 28 + 2) * smoothness * .22h * nl * ao;
                return (albedo * wrapped * ao + spec + sheen * ao) * light.color * light.distanceAttenuation * light.shadowAttenuation;
            }
void Fabric(half id, out half smoothness, out half sheen, out half knit, out float2 quadrant, out half frequency)
            {
                smoothness = .10h; sheen = .55h; knit = 1; quadrant = float2(0,0); frequency = 8;
                if (id > .5h && id < 1.5h) { smoothness = .13h; sheen = .38h; quadrant = float2(1,0); frequency = 12; }
                else if (id < 2.5h && id > 1.5h) { smoothness = .16h; sheen = .24h; knit = 0; quadrant = float2(0,1); }
                else if (id < 3.5h && id > 2.5h) { smoothness = .04h; sheen = 0; knit = 0; quadrant = float2(1,1); }
                else if (id < 4.5h && id > 3.5h) { smoothness = .08h; sheen = 0; knit = 0; quadrant = float2(1,1); }
                else if (id > 4.5h) { smoothness = .10h; sheen = .18h; knit = 0; quadrant = float2(1,0); }
                // Version 2 extends the decoder. Version 1 keeps its six IDs and bytes.
                if (_FabricVersion > 1.5h && id > 5.5h) {
                    if (id < 6.5h) { smoothness=.09h;sheen=.24h;knit=0;quadrant=float2(0,0);frequency=8; }
                    else if (id < 7.5h) { smoothness=.12h;sheen=.32h;knit=1;quadrant=float2(1,0);frequency=10; }
                    else if (id < 8.5h) { smoothness=.18h;sheen=.10h;knit=0;quadrant=float2(0,1);frequency=8; }
                    else { smoothness=.08h;sheen=.18h;knit=0;quadrant=float2(1,1);frequency=8; }
                }
            }
half3 SoftShoulder (half3 c)
            {
                half peak = max(max(c.r, c.g), max(c.b, 1e-4));
                half k = _Knee;
                half rolled = peak <= k ? peak : k + (1 - k) * (1 - exp(-(peak - k) / (1 - k)));
                return c * (rolled / peak);
            }

#endif
