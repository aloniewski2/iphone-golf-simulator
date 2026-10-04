// Worn cloth for the match heroes' kit: shirt, shorts / skirt, socks, shoes, the racket grip (URP).
//
// The partner of TennisCharacter, and deliberately its opposite: matte, no subsurface, a weaker rim.
// A soft wrap keeps the inside of a fold from going black, and a soft shoulder on the lit result keeps
// a white garment from clipping to one flat white in the sun, so the fold shading survives on it.
//
// HERO_DETAIL: white is no longer one flat colour.
//   * _BaseMap  : the kit mesh's own UV layout, baked from the shell geometry (work/character-shader/detail/bake_kit.py): collar, placket,
//                 raglan seams, side seams, hems and folds go darker. It multiplies the colour.
//   * _WeaveMap : one small tiling map. R,G = weave normal, B = thread break. The normal tilts the lit surface (so the weave breaks the light) and the thread
//                 break is multiplied into the colour (so the white has a faint thread in it). Tiled per UV unit (_WeaveTile, set per piece from the mesh's
//                 own metres-per-UV), turned to the cloth's grain with _WeaveAngle. Mip levels average it flat, so it fades out with distance instead of shimmering.
//   * sheen     : replaces the Blinn highlight. A soft grazing sheen that grows as the surface turns away from the camera, tinted by the cloth colour, and
//                 gated by the light on that side (dead in shadow, dead on the unlit side). Smoothness now only sets how wide a faint broad gloss is (dark trim
//                 at 0.25 is a little glossier; the cloth at <= 0.14 has no visible highlight at all).
Shader "GolfArcade/TennisCloth"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Seam / collar / hem map (kit UV)", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.1
        _Wrap ("Wrap", Range(0,1)) = 0.5
        _RimColor ("Rim colour", Color) = (1,0.96,0.88,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim strength", Range(0,1)) = 0.2
        _Exposure ("Cloth exposure", Range(0.2,1.5)) = 0.7
        _Knee ("Highlight knee", Range(0.3,0.95)) = 0.7
        _WeaveMap ("Weave: normal xy, thread break", 2D) = "gray" {}
        _WeaveTile ("Weave tiles per UV unit", Float) = 120
        _WeaveAngle ("Weave grain angle (deg)", Float) = 0
        _WeaveNormal ("Weave normal strength", Range(0,2)) = 1
        _WeaveThread ("Thread break strength", Range(0,1)) = 0.5
        _SheenStrength ("Sheen strength", Range(0,2)) = 0.55
        _SheenPower ("Sheen power", Range(1,8)) = 3
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; half4 _RimColor; float4 _BaseMap_ST; float4 _WeaveMap_ST;
                half _Smoothness, _Wrap, _RimPower, _RimStrength, _Exposure, _Knee;
                float _WeaveTile, _WeaveAngle;
                half _WeaveNormal, _WeaveThread, _SheenStrength, _SheenPower;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_WeaveMap); SAMPLER(sampler_WeaveMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3; half fog : TEXCOORD4;
            };

            Varyings vert (Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                VertexNormalInputs nrm = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                o.uv = i.uv;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // Direct light on the cloth: wrapped diffuse, a faint BROAD gloss (never a dot), and the grazing sheen.
            half3 Shade (Light light, half3 n, half3 v, half3 albedo, half3 sheenTint, half ndv)
            {
                half ndl = dot(n, light.direction);
                half wrapped = saturate((ndl + _Wrap) / (1 + _Wrap));
                half atten = light.distanceAttenuation * light.shadowAttenuation;
                half3 h = normalize(light.direction + v);
                // smoothness only widens / narrows a very low, very broad lobe: exponent 2..30, peak <= 0.35 * smoothness
                half spec = pow(saturate(dot(n, h)), _Smoothness * 28 + 2) * _Smoothness * 0.35 * saturate(ndl * 4);
                // grazing sheen: grows as the surface turns away from the eye, only where this light reaches (ndl) and not in shadow (atten)
                half graze = pow(1 - ndv, _SheenPower);
                half3 sheen = sheenTint * (graze * saturate(ndl * 1.5) * _SheenStrength);
                return (albedo * wrapped + spec + sheen) * light.color * atten;
            }

            // Roll the brightest channel off above the knee (hue kept): a lit white keeps a gradient instead of clipping.
            half3 SoftShoulder (half3 c)
            {
                half peak = max(max(c.r, c.g), max(c.b, 1e-4));
                half k = _Knee;
                half rolled = peak <= k ? peak : k + (1 - k) * (1 - exp(-(peak - k) / (1 - k)));
                return c * (rolled / peak);
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                // weave: the cloth's grain turned to the mesh, tiled small
                float ang = radians(_WeaveAngle); float sn, cs; sincos(ang, sn, cs);
                float2 wuv = float2(cs * i.uv.x - sn * i.uv.y, sn * i.uv.x + cs * i.uv.y) * _WeaveTile;
                half4 weave = SAMPLE_TEXTURE2D(_WeaveMap, sampler_WeaveMap, wuv);
                float3 tw = i.tangentWS.xyz;
                if (_WeaveNormal > 0.001 && dot(tw, tw) > 1e-8)   // a mesh with no tangents (or a strength of 0) keeps its own normal
                {
                    half2 txy = (weave.rg * 2 - 1) * _WeaveNormal;
                    txy = half2(cs * txy.x + sn * txy.y, -sn * txy.x + cs * txy.y);   // the pattern was turned by -angle: turn its slopes with it
                    half3 ts = half3(txy, sqrt(saturate(1 - dot(txy, txy))));
                    half3 t = normalize(tw); half3 b = cross(n, t) * i.tangentWS.w;
                    n = normalize(TransformTangentToWorld(ts, half3x3(t, b, n)));
                }
                half thread = lerp((half)1, weave.b * 1.28, _WeaveThread);   // a thread break multiplied into the colour; x1.28 re-centres the map's B (mean ~0.76) on 1, so the white keeps its average

                half3 albedo = _BaseColor.rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _BaseMap_ST.xy + _BaseMap_ST.zw).rgb * (1.07 * thread);   // 1.07: the seam map averages ~0.93
                half3 sheenTint = sqrt(albedo);   // tinted by the cloth: white cloth -> white sheen, black trim -> a dim one
                half ndv = saturate(dot(n, v));

                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 colour = Shade(main, n, v, albedo, sheenTint, ndv);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                #if defined(_LIGHT_LAYERS)
                uint meshLayers = GetMeshRenderingLayer();
                #endif
                for (uint li = 0; li < count; li++)
                {
                    Light extra = GetAdditionalLight(li, i.positionWS);
                    #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(extra.layerMask, meshLayers)) continue;
                    #endif
                    colour += Shade(extra, n, v, albedo, sheenTint, ndv);
                }
                #endif
                half3 ambient = SampleSH(n) * albedo;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient *= ao.indirectAmbientOcclusion; colour *= ao.directAmbientOcclusion;
                #endif
                half rim = pow(1 - ndv, _RimPower) * _RimStrength;
                colour += ambient + _RimColor.rgb * rim * (0.35 + 0.65 * albedo) * main.color;
                colour = SoftShoulder(colour * _Exposure);
                return half4(MixFog(colour, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
