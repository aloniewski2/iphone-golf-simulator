// Character surface for the tennis players and crowd (URP).
//
// Soft-toy look from the art targets: wrapped diffuse so light bleeds gently past the
// terminator, a warm subsurface tint in that transition, a restrained specular, a rim that
// separates the silhouette from the court, and screen-space AO where limbs meet the body.
Shader "GolfArcade/TennisCharacter"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        // The hero body has no UVs. With this on, _BumpMap is projected along the three axes from the BIND-POSE position carried in mesh channel 1
        // (MatchHeroLook writes it), so the soft break in the light stays on the skin as the body moves. _BumpTile = tiles per metre.
        _BumpTriplanar ("Bump from bind-pose position (meshes with no UVs)", Float) = 0
        _BumpTile ("Triplanar bump tiles per metre", Float) = 5
        _Saturation ("Saturation", Range(0,1.5)) = 0.82
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Wrap ("Wrap", Range(0,1)) = 0.35
        _Subsurface ("Subsurface warmth", Color) = (0,0,0,0)
        _RimColor ("Rim colour", Color) = (1,0.96,0.88,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim strength", Range(0,1)) = 0.22
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
            // multi_compile, not shader_feature: the materials are made at runtime, so no
            // material asset would keep a shader_feature variant alive in the build.
            #pragma multi_compile_local _ _NORMALMAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float4 _BaseMap_ST; half4 _Subsurface; half4 _RimColor;
                float4 _BumpMap_ST; half _Smoothness, _Wrap, _RimPower, _RimStrength, _BumpScale, _Saturation, _BumpTriplanar; float _BumpTile;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float3 bindPos : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; half fog : TEXCOORD3; float4 tangentWS : TEXCOORD4; float3 bindPos : TEXCOORD5; float3 normalOS : TEXCOORD6; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                VertexNormalInputs nrm = GetVertexNormalInputs(i.normalOS, i.tangentOS);
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.bindPos = i.bindPos; o.normalOS = i.normalOS;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half3 Shade (Light light, half3 n, half3 v, half3 albedo)
            {
                half ndl = dot(n, light.direction);
                half wrapped = saturate((ndl + _Wrap) / (1 + _Wrap));
                // Warmth where light wraps past the edge: reads as soft skin rather than plastic.
                half edge = saturate(wrapped - saturate(ndl)) * 2;
                half3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), _Smoothness * 96 + 6) * _Smoothness * 0.6;
                half atten = light.distanceAttenuation * light.shadowAttenuation;
                return (albedo * wrapped + _Subsurface.rgb * edge * albedo + spec) * light.color * atten;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                #if defined(_NORMALMAP)
                if (_BumpTriplanar > 0.5)
                {
                    // Three axis projections of the soft normal map from the bind-pose position, blended by the skinned object-space normal
                    // and added to it (UDN blend), then back to world: a break in the light that rides on the skin.
                    float3 nO = normalize(i.normalOS);
                    float3 w = pow(abs(nO), 4); w /= (w.x + w.y + w.z + 1e-5);
                    half2 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.bindPos.zy * _BumpTile), _BumpScale).xy;
                    half2 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.bindPos.xz * _BumpTile), _BumpScale).xy;
                    half2 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.bindPos.xy * _BumpTile), _BumpScale).xy;
                    nO = normalize(nO + w.x * float3(0, tx.y, tx.x) + w.y * float3(ty.x, 0, ty.y) + w.z * float3(tz.x, tz.y, 0));
                    n = normalize(TransformObjectToWorldNormal(nO));
                }
                else
                {
                    half3 ts = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    half3 t = normalize(i.tangentWS.xyz); half3 b = cross(n, t) * i.tangentWS.w;
                    n = normalize(TransformTangentToWorld(ts, half3x3(t, b, n)));
                }
                #endif
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                // The generated textures are painted warm and the resort grade warms again;
                // pull saturation back so skin reads peach rather than orange.
                c.rgb = lerp(dot(c.rgb, half3(0.2126, 0.7152, 0.0722)).xxx, c.rgb, _Saturation);
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 colour = Shade(main, n, v, c.rgb);
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                #if defined(_LIGHT_LAYERS)
                uint meshLayers = GetMeshRenderingLayer();
                #endif
                for (uint li = 0; li < count; li++)
                {
                    Light extra = GetAdditionalLight(li, i.positionWS);
                    // A light on its own rendering layer (the hero rim) only reaches meshes that carry that layer.
                    #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(extra.layerMask, meshLayers)) continue;
                    #endif
                    colour += Shade(extra, n, v, c.rgb);
                }
                #endif
                half3 ambient = SampleSH(n) * c.rgb;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                ambient *= ao.indirectAmbientOcclusion; colour *= ao.directAmbientOcclusion;
                #endif
                half rim = pow(1 - saturate(dot(v, n)), _RimPower) * _RimStrength;
                colour += ambient + _RimColor.rgb * rim * (0.35 + 0.65 * c.rgb) * main.color;
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
