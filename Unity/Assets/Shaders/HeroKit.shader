// Modular golf compatibility surface. The shared MatchHeroLook path is preferred;
// this surface keeps legacy spectators, optional hair and painted parts grounded in scene light.
Shader "GolfArcade/HeroKit"
{
    Properties
    {
        _MainTex ("Atlas (or the part's own map)", 2D) = "white" {}
        _Mask ("Regions: R shirt, G shorts + trim, B baked hair, A skin", 2D) = "black" {}
        _Color ("Colour (flat parts)", Color) = (1, 1, 1, 1)
        _UseAtlas ("Atlas on (1) or flat colour (0)", Float) = 1
        _MatCap ("MatCap (grey)", 2D) = "white" {}
        _MatCapStrength ("How much the MatCap shades", Range(0, 1)) = .10
        _Wrap ("Light wrap", Range(0, 1)) = 0.35
        _SurfaceSmoothness ("Surface smoothness", Range(0,1)) = 0.28
        _SurfaceSpecular ("Surface highlight", Range(0,2)) = 0.8
        _SurfaceSheen ("Fabric grazing sheen", Range(0,1)) = 0
        _SurfaceWarmth ("Skin warmth", Color) = (0.12,0.035,0.02,1)
        _Shirt ("Shirt", Color) = (1, 1, 1, 0)
        _Shorts ("Shorts + trim", Color) = (1, 1, 1, 0)
        _Accent ("Baked hair", Color) = (1, 1, 1, 0)
        _Skin ("Skin", Color) = (1, 1, 1, 0)
        _SkinShading ("Skin shading kept", Range(0, 1)) = .10
        _Ref ("Region mean luminance", Vector) = (0.85, 0.19, 0.3, 0.67)
        _UseFlex ("Hair: swing by the vertex colour's R", Float) = 0
        _HairSway ("Hair sway (world offset at flex 1)", Vector) = (0, 0, 0, 0)
        _HatHold ("Hair: which hat holds it (vertex colour G visor, B cap, A sweatband)", Vector) = (0, 0, 0, 0)
        _UseScalp ("Scalp: skin to hair by the vertex colour's R (the hairline)", Float) = 0
        _ScalpSkin ("Scalp: the skin below the hairline", Color) = (1, 0.8, 0.7, 1)
        _HairAmount ("Scalp: hair above the hairline (0 bald)", Range(0, 1)) = 1
        _Fill ("Fill light: a floor under the shadows (soft clay, no black undersides)", Range(0, 1)) = 0.10
        _Clear ("Clear: draw nothing (a clear lens: the frame shows, the eyes behind it too)", Float) = 0
        [HideInInspector] _OffsetFactor ("Depth offset factor (painted face layers sit a hair above the skin)", Float) = 0
        [HideInInspector] _OffsetUnits ("Depth offset units", Float) = 0
    }
    // Native golf uses URP. Keep the Built-in subshader for legacy previews.
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Back
        Offset [_OffsetFactor], [_OffsetUnits]
        HLSLINCLUDE
        #define GOLF_HERO 1
        #include "GolfLegacyURP.hlsl"
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GolfLegacyVertex
            #pragma fragment GolfLegacyFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex GolfLegacyShadowVertex
            #pragma fragment GolfLegacyShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Back
        Offset [_OffsetFactor], [_OffsetUnits]
        CGPROGRAM
        #pragma surface surf Clay fullforwardshadows vertex:vert
        #pragma target 3.5
        #include "UnityPBSLighting.cginc"
        sampler2D _MainTex, _Mask, _MatCap;
        fixed4 _Color, _Shirt, _Shorts, _Accent, _Skin, _ScalpSkin;
        float4 _Ref;
        float _SurfaceSmoothness, _SurfaceSpecular, _SurfaceSheen; float4 _SurfaceWarmth;
        float _UseAtlas, _MatCapStrength, _Wrap, _SkinShading, _UseFlex, _UseScalp, _HairAmount, _Clear, _Fill;
        float4 _HairSway, _HatHold;

        struct Input { float2 uv_MainTex; float3 worldNormal; float4 color : COLOR; };

        // Hair follows the head with a lag: each vertex moves by _HairSway (a world offset the game
        // keeps as a damped spring on the head's motion) in proportion to its flex, the R of its vertex
        // colour (blender/scripts/hero_repair.py: 0 on the scalp, 1 at the free ends). Hair tucked in
        // under a hat is held there (G, B, A: how firmly the visor, the cap, the sweatband hold each vertex;
        // blender/scripts/hero_head.py), so it never swings out through the hat.
        void vert(inout appdata_full v)
        {
            float held = saturate(dot(v.color.gba, _HatHold.xyz));
            float3 off = mul((float3x3)unity_WorldToObject, _HairSway.xyz);
            v.vertex.xyz += off * v.color.r * (1 - held) * _UseFlex;
        }

        float lum(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

        float3 tint(float3 c, float l, float4 target, float refLum, float w, float lift)
        {
            // The shading follows the original around the new colour; highlights may lift it a little
            // (skin, already light, hardly at all: a pale tone would burn out under the course's sun).
            float3 shaded = target.rgb * clamp(l / max(refLum, 0.01), 0.25, lift);
            return lerp(c, shaded, saturate(w * target.a));
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            clip(_Clear > 0.5 ? -1 : 1);
            float3 vn = normalize(mul((float3x3)UNITY_MATRIX_V, IN.worldNormal));
            half cap = tex2D(_MatCap, vn.xy * 0.49 + 0.5).r;
            half3 map = tex2D(_MainTex, IN.uv_MainTex).rgb;
            half3 albedo = _Color.rgb;
            if (_UseAtlas > 0.5)
            {
                float4 m = tex2D(_Mask, IN.uv_MainTex);
                float l = lum(map);
                float3 c = map;
                c = tint(c, l, _Shirt, _Ref.x, m.r, 1.1);
                c = tint(c, l, _Shorts, _Ref.y, m.g, 1.35);
                c = tint(c, l, _Accent, _Ref.z, m.b, 1.35);
                c = tint(c, lerp(_Ref.w, l, _SkinShading), _Skin, _Ref.w, m.a, 1.0);
                albedo = c * _Color.rgb;
            }
            if (_UseScalp > 0.5)
            {
                // the head under the hair: skin at the forehead and temples, the hair's colour (and the short
                // hair's map) above the hairline; all skin when bald
                albedo = lerp(_ScalpSkin.rgb, _Color.rgb * map, saturate(IN.color.r * _HairAmount));
            }
            // hair: each lock a touch darker at its root than at its free end (the flex is the lock's length, 0 to 1)
            // so overlapping locks read as strands
            if (_UseFlex > 0.5) albedo *= lerp(0.82, 1.1, saturate(IN.color.r * 2.4));
            o.Albedo = albedo * lerp(1, cap, _MatCapStrength);
            o.Alpha = 1;
        }

        half4 LightingClay(SurfaceOutput s, half3 viewDir, UnityGI gi)
        {
            half nl = dot(s.Normal, gi.light.dir);
            half lit = saturate((nl + _Wrap) / (1 + _Wrap));
            half4 c;
            c.rgb = s.Albedo * (gi.light.color * lit + _Fill);
            half3 h = normalize(gi.light.dir + viewDir);
            half spec = pow(saturate(dot(s.Normal,h)), 16 + _SurfaceSmoothness * 80) * _SurfaceSpecular * .08 * saturate(nl * 3);
            c.rgb += gi.light.color * spec;
            #ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
            c.rgb += s.Albedo * gi.indirect.diffuse;
            #endif
            c.a = s.Alpha;
            return c;
        }

        void LightingClay_GI(SurfaceOutput s, UnityGIInput data, inout UnityGI gi)
        {
            gi = UnityGlobalIllumination(data, 1.0, s.Normal);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
