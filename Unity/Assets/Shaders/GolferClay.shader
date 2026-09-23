// The golfer, shaded like Adnan's standard characters in his studio renders: soft matte clay
// rather than flat plastic. A MatCap (Resources/Golfer/Look/clay_matcap.png, a Higgsfield
// render of his studio lighting on a neutral sphere) gives every surface that gentle form
// shading, darkening toward the silhouette and underneath; the sun wraps round the body so
// there is no hard terminator, and still casts and receives the course's shadows. Cloth
// (_Fabric > 0) carries the knit of his tee and joggers (knit_detail.png, grey, 0.5 = none),
// laid on in object space from three sides so the kit's missing UVs don't matter.
Shader "GolfArcade/GolferClay"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _MatCap ("MatCap (grey)", 2D) = "white" {}
        _MatCapGain ("MatCap gain", Float) = 1.0
        _MatCapStrength ("How much the MatCap shades", Range(0, 1)) = 0.85
        _Wrap ("Light wrap", Range(0, 1)) = 0.3
        _Knit ("Knit detail (grey)", 2D) = "gray" {}
        _KnitTile ("Knit tiles per metre", Float) = 9
        _Fabric ("Knit strength", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM
        #pragma surface surf Clay fullforwardshadows vertex:vert
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"
        sampler2D _MatCap, _Knit;
        fixed4 _Color;
        float _MatCapGain, _MatCapStrength, _Wrap, _KnitTile, _Fabric;

        struct Input { float3 worldNormal; float3 objPos; float3 objNormal; };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
            o.objNormal = v.normal;
        }

        half Knit(float3 p, float3 n)
        {
            float3 w = pow(abs(normalize(n)), 4);
            w /= (w.x + w.y + w.z);
            p *= _KnitTile;
            return tex2D(_Knit, p.yz).r * w.x + tex2D(_Knit, p.xz).r * w.y + tex2D(_Knit, p.xy).r * w.z;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float3 vn = normalize(mul((float3x3)UNITY_MATRIX_V, IN.worldNormal));
            half cap = tex2D(_MatCap, vn.xy * 0.49 + 0.5).r * _MatCapGain;
            half3 albedo = _Color.rgb * lerp(1, cap, _MatCapStrength);
            if (_Fabric > 0) albedo *= 1 + _Fabric * (Knit(IN.objPos, IN.objNormal) - 0.5) * 2;
            o.Albedo = albedo;
            o.Alpha = 1;
        }

        half4 LightingClay(SurfaceOutput s, half3 viewDir, UnityGI gi)
        {
            half nl = dot(s.Normal, gi.light.dir);
            half lit = saturate((nl + _Wrap) / (1 + _Wrap));
            half4 c;
            c.rgb = s.Albedo * gi.light.color * lit;
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
