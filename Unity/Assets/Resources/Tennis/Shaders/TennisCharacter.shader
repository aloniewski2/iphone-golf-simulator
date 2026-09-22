// Character surface for the tennis players and crowd.
//
// The characters used to be rebuilt as flat Standard materials at 10% smoothness, which is
// what made them read as plastic. This keeps the authored colour and texture and adds the
// three cheap cues skin and cloth actually need at gameplay distance: wrapped diffuse (light
// bleeding softly past the terminator instead of a hard edge), a restrained specular, and a
// rim that separates the silhouette from the court.
Shader "GolfArcade/TennisCharacter"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
        _Wrap ("Wrap", Range(0,1)) = 0.35
        _RimColor ("Rim colour", Color) = (1,0.96,0.88,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim strength", Range(0,1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Wrapped fullforwardshadows
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color, _RimColor;
        half _Smoothness, _Wrap, _RimPower, _RimStrength;

        struct Input { float2 uv_MainTex; float3 viewDir; };

        half4 LightingWrapped (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half diffuse = saturate((ndl + _Wrap) / (1 + _Wrap));
            half3 h = normalize(lightDir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), s.Specular * 96 + 6) * s.Gloss;
            half4 c;
            c.rgb = (s.Albedo * diffuse + spec) * _LightColor0.rgb * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1;
            o.Specular = _Smoothness;
            o.Gloss = _Smoothness * 0.6;
            half rim = 1 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * pow(rim, _RimPower) * _RimStrength * (0.35 + 0.65 * c.rgb);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
