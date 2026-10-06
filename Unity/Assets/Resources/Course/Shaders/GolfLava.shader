// Golf postcard lava (LK_LAVA, hole 10 only). STATIC (no _Time): the brief forbids animated lava.
// SELF-LIT, built from emission alone: there is no post-processing in this project (no PostProcessData on either renderer, so no bloom and no
// tone mapper), and URP Lit would turn the painted albedo + a scene light into a pink / yellow / black mess depending on light and angle (measured:
// 76-78 % of the lava pixels in the orange band at best, the texture's hue spread being wider than the band). So the textures only say HOW HOT each
// texel is, and a 3-stop ramp chosen in the orange band says what colour that is:
//     heat   = saturate(((lum(Lava_C) * _HeatAlbedo + lum(Lava_E) * _HeatGlow + _HeatBias + relief) - .5) * _HeatGain + .5)
//     colour = ramp(_Deep @0, _Crust @.2, _Flow @.6, _Hot @1)(heat)   // 4 stops authored in sRGB (SetColor converts them), all with hue 8..28 deg;
//                                                                       // only _Deep (a few % of the texels) may be darker than V .7
// v2 2026-10-04 (review fix): the maps are WORLD-PROJECTED by default (_WorldUV = 1: planar from above on floors, from the side on walls, _WorldTile =
// .9144 / 24 tiles per yard = one Lava tile per 24 m), so the lava never depends on the mesh's UVs: a lava mesh without UVs, or with UVs at the wrong
// density, used to render one flat orange (a baseline WATER_LAVA has none). _WorldUV = 0 uses the mesh UV (tile units = metres / 24) instead.
// v2 repair round 3 2026-10-05: _FogShare (how much of the distance fog the lava takes, default 1 = like URP Lit). Crater's fog went from ember red to a neutral smoke-grey (basalt must not read maroon), and a grey haze over
// the 430 yd cone lava took its saturation below the orange band (tee still: 65 % of the cone's lava in band, limit 85). Lava is the brightest thing in the scene and glows through haze (crater.jpg's far cone lava is not hazed).
// Distance fog is applied per fragment like URP Lit (the crater's dusk fog). No lights, no shadows, no depth pass (opaque, one pass, like TennisWater).
// Lives under Resources so a player build always carries it (Resources.Load<Shader>("Course/Shaders/GolfLava")); only multi_compile keywords (fog).
Shader "GolfArcade/GolfLava"
{
    Properties
    {
        _BaseMap ("Lava_C (albedo: where the flow is bright)", 2D) = "white" {}
        _EmissionMap ("Lava_E (glow: the hottest veins)", 2D) = "black" {}
        _BumpMap ("Lava_N (normal: relief in the heat)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _HeatAlbedo ("Heat from the albedo luminance", Range(0,4)) = 1.5
        _HeatGlow ("Heat from the glow luminance", Range(0,4)) = 1.5
        _HeatBias ("Heat bias", Range(-1,1)) = -0.1
        _HeatGain ("Contrast of the heat around .5", Range(0.5,4)) = 1.5
        _Relief ("Heat added by the normal map's slope", Range(0,1)) = 0.15
        _Deep ("Coolest colour (heat 0, a few % of the texels)", Color) = (0.58,0.13,0.04,1)
        _Crust ("Crust colour (heat .2)", Color) = (0.73,0.17,0.05,1)
        _Flow ("Flow colour (heat .6)", Color) = (0.88,0.31,0.065,1)
        _Hot ("Hottest colour (heat 1)", Color) = (1,0.5,0.11,1)
        _WorldUV ("Project the maps from the world (1) or use the mesh UV (0)", Range(0,1)) = 1
        _WorldTile ("World projection: tiles per world unit (yard); .9144 / 24 = one tile per 24 m", Float) = 0.0381
        _FogShare ("Share of the distance fog the lava takes (1 = like the rest of the scene; lava glows through haze)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "GolfLava"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _BumpScale, _HeatAlbedo, _HeatGlow, _HeatBias, _HeatGain, _Relief;
                half4 _Deep, _Crust, _Flow, _Hot;
                half _WorldUV, _FogShare;
                float _WorldTile;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; };

            V vert (A i)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                // world projection: from above on floors (|n.y| >= .5), from the side on walls; the same tile size whatever the mesh's UVs are
                float3 an = abs(normalize(i.normalWS));
                float2 pw = an.y >= 0.5 ? i.positionWS.xz : (an.x > an.z ? i.positionWS.zy : i.positionWS.xy);
                float2 uv = lerp(i.uv, pw * _WorldTile * _BaseMap_ST.xy + _BaseMap_ST.zw, _WorldUV);
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half3 e = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                // the maps are sRGB colour textures, sampled LINEAR by the GPU: the heat is their GAMMA luminance (as painted), so it does not depend on the colour space
                c = pow(max(c, 0.0001h), 0.4545h); e = pow(max(e, 0.0001h), 0.4545h);   // linear -> gamma (the heat is defined on the painted values)
                half heat = dot(c, half3(0.2126h, 0.7152h, 0.0722h)) * _HeatAlbedo + dot(e, half3(0.2126h, 0.7152h, 0.0722h)) * _HeatGlow
                          + _HeatBias + (n.x + n.y) * 0.5h * _Relief;
                heat = saturate((heat - 0.5h) * _HeatGain + 0.5h);
                // the stops arrive linear (non-HDR Color properties are converted from sRGB by Material.SetColor); interpolated in linear
                half3 ramp = heat < 0.2h ? lerp(_Deep.rgb, _Crust.rgb, heat * 5.0h)
                           : heat < 0.6h ? lerp(_Crust.rgb, _Flow.rgb, (heat - 0.2h) * 2.5h)
                           : lerp(_Flow.rgb, _Hot.rgb, (heat - 0.6h) * 2.5h);
                half3 colour = ramp;
                half fog = InitializeInputDataFog(float4(i.positionWS, 1), 0);
                return half4(lerp(colour, MixFog(colour, fog), _FogShare), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
