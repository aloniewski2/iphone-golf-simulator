// The Plaza's lawns, hedges, trees, gravel and cobbles (country-club look): one cheap lit shader for everything soft and
// organic. Base map sampled from the mesh UVs, or planar in world XZ (_UVMode 1), or in rings around a centre (_UVMode 2, the
// cobbles round the medallion); vertex colour tints it; a world-space value noise breaks up big flat colours (leafy clumps,
// lawn patches); wrap lighting + a little back light keep foliage soft. Trees and roses sway in the global _HubWind
// (xz = wind in metres, w = time, set by HubLife) by their vertex alpha x _Sway; with _Sway 0 nothing moves (lawns, gravel).
// Only the main light casts shadows; extra (door) lights add plain Lambert. No textures besides the base map, no keywords
// of its own: one variant set, SRP-batcher compatible.
Shader "GolfArcade/HubSurface"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base map", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _VertexTint ("Vertex colour tint", Range(0, 1)) = 1
        _Sway ("Sway (x vertex alpha), m", Float) = 0
        _Noise ("World noise", Range(0, 1)) = 0
        _NoiseScale ("Noise frequency (1/m)", Float) = 2
        _Wrap ("Wrap lighting", Range(0, 1)) = .35
        _Gloss ("Sheen", Range(0, 1)) = .1
        _Leaf ("Back light", Range(0, 1)) = 0
        _UVMode ("UV: 0 mesh, 1 world XZ, 2 rings", Float) = 0
        _Tile ("Tile size, m (x along, y across)", Vector) = (1, 1, 0, 0)
        _Centre ("Ring centre (xz)", Vector) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor;
            half _VertexTint, _Sway, _Noise, _NoiseScale, _Wrap, _Gloss, _Leaf, _UVMode;
            float4 _Tile, _Centre;
        CBUFFER_END
        float4 _HubWind;
        struct A { float4 p : POSITION; float3 n : NORMAL; half4 c : COLOR; float2 uv : TEXCOORD0; };
        struct V { float4 p : SV_POSITION; float3 w : TEXCOORD0; half3 n : TEXCOORD1; half4 c : COLOR; float2 uv : TEXCOORD2; };
        float3 Sway(float3 w, half weight)
        {
            float phase = dot(w.xz, float2(.31, .23));
            float2 gust = _HubWind.xz * (.62 + .38 * sin(_HubWind.w * 1.9 + phase));
            float2 side = float2(-_HubWind.z, _HubWind.x) * .35 * sin(_HubWind.w * 4.7 + phase * 2.3);
            w.xz += (gust + side) * weight * _Sway;
            return w;
        }
        V vert(A a)
        {
            V o; o.w = Sway(TransformObjectToWorld(a.p.xyz), a.c.a);
            o.p = TransformWorldToHClip(o.w); o.n = TransformObjectToWorldNormal(a.n);
            o.c = a.c; o.uv = TRANSFORM_TEX(a.uv, _BaseMap); return o;
        }
        float Hash(float3 p) { p = frac(p * .3183099 + .1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
        float Noise(float3 x)
        {
            float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x), lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x), lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        half3 Albedo(V i)
        {
            half4 tex;
            if (_UVMode < .5) tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
            else
            {
                float2 planar = i.w.xz / _Tile.xy, uv = planar;
                if (_UVMode > 1.5)
                {
                    float2 d = i.w.xz - _Centre.xz; float r = length(d);
                    float row = floor(r / _Tile.y), rr = (row + .5) * _Tile.y;
                    uv = float2(atan2(d.y, d.x) * rr / _Tile.x, r / _Tile.y);
                }
                tex = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, ddx(planar), ddy(planar));
            }
            half3 c = tex.rgb * _BaseColor.rgb * lerp(half3(1, 1, 1), i.c.rgb, _VertexTint);
            if (_Noise > 0)
            {
                float3 q = i.w * _NoiseScale;
                half n = Noise(q) * .65 + Noise(q * 2.7 + 11.3) * .35;
                c *= 1 + _Noise * (n * 2 - 1) * .55;
            }
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit" Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            half4 frag(V i, FRONT_FACE_TYPE front : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half3 n = normalize(i.n) * IS_FRONT_VFACE(front, 1, -1);
                half3 albedo = Albedo(i);
                uint layers = GetMeshRenderingLayer();
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.w));
                half3 ambient = SampleSH(n), direct = 0, spec = 0;
                half aoDirect = 1;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.p));
                    ambient *= ao.indirectAmbientOcclusion; aoDirect = ao.directAmbientOcclusion;
                #endif
                half3 view = SafeNormalize(GetWorldSpaceViewDir(i.w));
                if (IsMatchingLightLayer(sun.layerMask, layers))
                {
                    half ndl = dot(n, sun.direction), shade = sun.shadowAttenuation * sun.distanceAttenuation;
                    half wrapped = saturate((ndl + _Wrap) / (1 + _Wrap));
                    direct += sun.color * (wrapped * shade * aoDirect + saturate(-ndl) * _Leaf * .35 * shade);
                    half3 h = SafeNormalize(sun.direction + view);
                    spec = sun.color * pow(saturate(dot(n, h)), 24) * _Gloss * .25 * shade * aoDirect;
                }
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint k = 0; k < count; k++)
                    {
                        Light l = GetAdditionalLight(k, i.w);
                        if (IsMatchingLightLayer(l.layerMask, layers))
                            direct += l.color * l.distanceAttenuation * saturate((dot(n, l.direction) + _Wrap) / (1 + _Wrap));
                    }
                #endif
                return half4(albedo * (ambient + direct) + spec, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex shadow
            #pragma fragment zero
            float3 _LightDirection;
            float4 shadow(A a) : SV_POSITION
            {
                float3 w = Sway(TransformObjectToWorld(a.p.xyz), a.c.a), n = TransformObjectToWorldNormal(a.n);
                float4 p = TransformWorldToHClip(ApplyShadowBias(w, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    p.z = min(p.z, UNITY_NEAR_CLIP_VALUE * p.w);
                #else
                    p.z = max(p.z, UNITY_NEAR_CLIP_VALUE * p.w);
                #endif
                return p;
            }
            half4 zero() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment depth
            half depth(V i) : SV_Target { return i.p.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals" Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment normals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 normals(V i, FRONT_FACE_TYPE front : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.n) * IS_FRONT_VFACE(front, 1, -1);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = saturate(PackNormalOctQuadEncode(n) * .5 + .5);
                    return half4(PackFloat2To888(oct), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
    Fallback Off
}
