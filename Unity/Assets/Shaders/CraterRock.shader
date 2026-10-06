// The crater's rock (Course/LavaWorld.cs): dark basalt in weathered strata, raked by low light through a
// relief map, with lava running down it: a baked mask (Resources/Course/Lava/lava_streams, or the
// cracks) glowing through as emission, breathing slowly. Lit by the sun and the ambient, no
// shadows (the wall is far off). Listed in Always Included Shaders by ProjectSetup.
Shader "GolfArcade/CraterRock"
{
    Properties
    {
        _MainTex ("Rock", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _BumpMap ("Relief", 2D) = "bump" {}
        _BumpScale ("Relief strength", Range(0, 3)) = 1.6
        _Glow ("Glow mask", 2D) = "black" {}
        _GlowColor ("Glow", Color) = (1.0, 0.36, 0.06, 1)
        _GlowAmount ("Glow amount", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        CGPROGRAM
        #pragma surface surf Lambert noshadow
        #pragma target 3.0
        sampler2D _MainTex, _BumpMap, _Glow;
        fixed4 _Color, _GlowColor;
        float _BumpScale, _GlowAmount;
        struct Input { float2 uv_MainTex; float2 uv_BumpMap; float2 uv_Glow; float3 worldPos; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
            float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
            o.Normal = normalize(float3(n.xy * _BumpScale, n.z));
            float g = tex2D(_Glow, IN.uv_Glow).r;
            float breathe = 0.86 + 0.24 * sin(_Time.y * 1.3 + IN.worldPos.x * 0.011 + IN.worldPos.z * 0.007);
            o.Emission = _GlowColor.rgb * g * _GlowAmount * breathe;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
