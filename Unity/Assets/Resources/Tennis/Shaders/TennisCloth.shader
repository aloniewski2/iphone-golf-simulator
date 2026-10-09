// CLOTH_OVERHAUL: colour-neutral garment normal/masks, six fabric IDs, derivative-filtered detail atlas.
// Same shader name and seven-role runtime API. No new keywords or pipeline settings.
Shader "GolfArcade/TennisCloth"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _TrimColor ("Trim colour", Color) = (0.035,0.035,0.0401,1)
        _NormalMap ("Garment normal (kit UV)", 2D) = "bump" {}
        _MaskMap ("AO / cavity / trim / fabric ID", 2D) = "white" {}
        _UseGarmentMaps ("Use garment construction", Float) = 0
        _FabricVersion ("Fabric encoding: 1 tennis, 2 golf", Float) = 1
        _AOStrength ("Garment AO strength", Range(0,1)) = 1
        _NormalStrength ("Garment normal strength", Range(0,2)) = 1
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
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Face culling", Float) = 2
    }
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _TrimColor; half4 _RimColor; float4 _WeaveMap_ST; float4 _NormalMap_ST, _MaskMap_ST;
                half _Smoothness, _Wrap, _RimPower, _RimStrength, _Exposure, _Knee;
                float _WeaveTile, _WeaveAngle;
                half _WeaveNormal, _WeaveThread, _SheenStrength, _SheenPower;
                half _UseGarmentMaps, _NormalStrength, _AOStrength;
                half _FabricVersion;
            CBUFFER_END
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_WeaveMap); SAMPLER(sampler_WeaveMap);

    ENDHLSL
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]
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
            #include "HeroLighting.hlsl"
            #include "HeroCloth.hlsl"

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

            // Two symmetric comparison taps keep cloth shadow sampling within the six-fetch phone budget.
            // Court/pipeline shadow settings stay unchanged; this receiver alone uses two taps.
            

            // Charlie distribution with Ashikhmin visibility. Knit fibres have a broad grazing lobe,
            // while rubber/foam have zero sheen. Numbers are shared with GARMENT_SPEC.md.
            
            

            // Roll the brightest channel off above the knee (hue kept): a lit white keeps a gradient instead of clipping.
            

            half4 frag (Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                half4 mask = half4(1, 1, 0, 0);
                half3 garment = half3(0, 0, 1);
                if (_UseGarmentMaps > .5h) {
                    mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                    garment = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                    garment.xy *= _NormalStrength;
                }
                half id = floor(mask.a * (_FabricVersion > 1.5h ? 9 : 5) + .5h), smoothness, sheen, knit, frequency;
                float2 quadrant;
                Fabric(id, smoothness, sheen, knit, quadrant, frequency);
                if (_UseGarmentMaps < .5h) { smoothness = _Smoothness; sheen = _SheenStrength; }
                float ang = radians(_WeaveAngle); float sn, cs; sincos(ang, sn, cs);
                float2 wuv = float2(cs * i.uv.x - sn * i.uv.y, sn * i.uv.x + cs * i.uv.y) * _WeaveTile;
                // Fade individual threads before their Nyquist limit; UV derivatives are evaluated before frac.
                float2 dx = ddx(wuv), dy = ddy(wuv);
                half fade = 1 - smoothstep(.32h, .72h, max(max(abs(dx.x), abs(dx.y)), max(abs(dy.x), abs(dy.y))) * frequency);
                float2 atlasUV = (quadrant + .03125 + frac(wuv) * .9375) * .5;
                if (_UseGarmentMaps < .5h) atlasUV = wuv;
                half4 weave = SAMPLE_TEXTURE2D_GRAD(_WeaveMap, sampler_WeaveMap, atlasUV, dx * .46875, dy * .46875);
                half2 slope = (weave.rg * 2 - 1) * _WeaveNormal * fade;
                if (id > 2.5h && id < 4.5h) slope = 0;
                // Rotate slopes back from the grain coordinate system (the previous male 41-degree correction).
                slope = half2(cs * slope.x + sn * slope.y, -sn * slope.x + cs * slope.y);
                half3 ts = normalize(half3(garment.xy + slope, garment.z));
                float3 tw = i.tangentWS.xyz;
                if (dot(tw, tw) > 1e-8) {
                    half3 t = normalize(tw), b = cross(n, t) * i.tangentWS.w;
                    n = normalize(TransformTangentToWorld(ts, half3x3(t, b, n)));
                }
                // A visible reverse side of thin cloth shades toward its viewer.
                n *= IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half threadBreak = lerp(1, weave.b * 1.28h, _WeaveThread * fade);
                if (id > 2.5h && id < 4.5h) threadBreak = 1;
                half3 albedo = lerp(_BaseColor.rgb, _TrimColor.rgb, mask.b) * threadBreak;
                half aoVisibility = lerp(1, max(mask.r, .70h), _AOStrength);
                half ao = lerp(1, aoVisibility, .5h);
                albedo *= lerp(.40h, 1, mask.g);
                half ndv = saturate(dot(n, v));

                Light main = HeroMain(GetMainLight());
                main.shadowAttenuation = ClothShadow(i.positionWS);
                half3 colour = Shade(main, n, v, albedo, smoothness, sheen, ao);
                if (_HeroPresentationFill > 0) colour += Shade(HeroPresentationLight(v), n, v, albedo, smoothness, sheen, ao);
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
                    colour += Shade(extra, n, v, albedo, smoothness, sheen, ao);
                }
                #endif
                half3 ambient = HeroAmbient(n) * albedo * ao;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor screenAO = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient *= _HeroProfile > .5h ? lerp(1,screenAO.indirectAmbientOcclusion,.35h) : screenAO.indirectAmbientOcclusion;
                colour *= _HeroProfile > .5h ? lerp(1,screenAO.directAmbientOcclusion,.35h) : screenAO.directAmbientOcclusion;
                #endif
                half rim = pow(1 - ndv, _RimPower) * _RimStrength * (_HeroProfile > .5h ? _HeroRim : 1);
                colour += albedo * knit * pow(1 - ndv, 5) * .045h * saturate(dot(n, main.direction)) * main.shadowAttenuation * main.color;
                colour += ambient + _RimColor.rgb * rim * saturate(dot(n,main.direction)*.7h+.3h) * (0.35 + 0.65 * albedo) * main.color;
                // Preserve moderate AO contrast through the bright-white highlight shoulder.
                if (_HeroProfile > .5h) {
                    // Neutral construction maps retain the palette under warm postcard keys.
                    // A cloth exposure controls white reflectance; trim keeps its measured dark value.
                    half neutralWhite = smoothstep(.4h,.8h,dot(albedo,half3(.2126h,.7152h,.0722h)));
                    half clothGain = lerp(min(_HeroClothExposure,3),_HeroClothExposure,neutralWhite);
                    colour *= _HeroClothBalance * lerp(clothGain, 1, mask.b);
                    colour *= lerp(.68h, 1, aoVisibility) * lerp(.75h, 1, mask.g);
                    if (_HeroFilmResponse < .5h) colour = max(colour, albedo * lerp(.35h,4.0h,mask.b));
                } else {
                    colour = (_HeroFilmResponse>.5h ? colour*_Exposure : SoftShoulder(colour * _Exposure)) * lerp(.68h, 1, aoVisibility) * lerp(.75h, 1, mask.g);
                }
                return half4(MixFog(HeroClothFinish(colour), i.fog), 1);
            }
            ENDHLSL
        }
        // Own depth/shadow passes share the exact UnityPerMaterial layout: SRP Batcher compatible.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct ShadowInput { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 shadowVert(ShadowInput i) : SV_POSITION {
                float3 p = TransformObjectToWorld(i.positionOS.xyz), n = TransformObjectToWorldNormal(i.normalOS);
                float4 clip = TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
                #if UNITY_REVERSED_Z
                clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
                #else
                clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE * clip.w);
                #endif
                return clip;
            }
            half4 shadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            float4 depthVert(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 depthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex normalVert
            #pragma fragment normalFrag
            struct NInput { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct NOutput { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float4 tangentWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            NOutput normalVert(NInput i) {
                NOutput o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz);
                VertexNormalInputs n=GetVertexNormalInputs(i.normalOS,i.tangentOS);o.normalWS=n.normalWS;
                o.tangentWS=float4(n.tangentWS,i.tangentOS.w*GetOddNegativeScale());o.uv=i.uv;return o;
            }
            half4 normalFrag(NOutput i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target {
                half3 n=normalize(i.normalWS);
                if(_UseGarmentMaps>.5h) {
                    half3 ts=UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv));ts.xy*=_NormalStrength;
                    half3 t=normalize(i.tangentWS.xyz),b=cross(n,t)*i.tangentWS.w;
                    n=normalize(TransformTangentToWorld(ts,half3x3(t,b,n)));
                }
                return half4(n * IS_FRONT_VFACE(facing, 1.0h, -1.0h),0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
