// The Magma Open's sky (set by Course/HoleAtmosphere.cs): a low red-orange horizon under a deep plum
// zenith, the sun a fire-coloured glow just above the crater wall, and decks of ash cloud lit orange from
// the lava below (one baked, tiling noise, projected onto a plane overhead so it has no seam and
// recedes to the horizon). Below the horizon: the crater's haze. Listed in Always Included Shaders by
// ProjectSetup.
Shader "GolfArcade/SkyMagma"
{
    Properties
    {
        _Horizon ("Horizon", Color) = (1.0, 0.42, 0.13, 1)
        _Mid ("Mid sky", Color) = (0.62, 0.20, 0.30, 1)
        _Zenith ("Overhead", Color) = (0.10, 0.06, 0.18, 1)
        _Below ("Below the horizon", Color) = (0.42, 0.19, 0.14, 1)
        _SunDir ("Sun direction", Vector) = (0.66, 0.10, 0.70, 0)
        _SunColor ("Sun glow", Color) = (1.0, 0.5, 0.18, 1)
        _Ash ("Ash cloud (tiling, A = density)", 2D) = "black" {}
        _AshLit ("Ash lit by the lava", Color) = (0.95, 0.40, 0.18, 1)
        _AshDark ("Ash in shade", Color) = (0.24, 0.12, 0.17, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Horizon, _Mid, _Zenith, _Below, _SunColor, _AshLit, _AshDark;
            float4 _SunDir;
            sampler2D _Ash;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION) { v2f o; o.pos = UnityObjectToClipPos(vertex); o.dir = vertex.xyz; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float up = saturate(y * 1.5);
                float below = saturate(-y * 4.0);
                fixed3 c = up < 0.3 ? lerp(_Horizon.rgb, _Mid.rgb, up / 0.3) : lerp(_Mid.rgb, _Zenith.rgb, (up - 0.3) / 0.7);
                c = lerp(c, _Below.rgb, below);
                float s = max(0, dot(d, normalize(_SunDir.xyz)));
                // decks of ash: a plane overhead, so the cloud bunches toward the horizon and has no seam
                if (y > 0.015)
                {
                    float2 uv = d.xz / (y + 0.28) * 0.85 + float2(_Time.y * 0.0042, _Time.y * 0.0021);
                    float2 uv2 = d.xz / (y + 0.28) * 1.9 + float2(-_Time.y * 0.003, 0.41);
                    float n = tex2D(_Ash, uv).a * 0.7 + tex2D(_Ash, uv2).a * 0.45;
                    n = saturate((n - 0.32) * 1.9);
                    float lit = saturate(pow(saturate(1.0 - y * 1.6), 1.4) * (0.55 + 0.6 * s));
                    fixed3 cloud = lerp(_AshDark.rgb, _AshLit.rgb, lit);
                    c = lerp(c, cloud, n * saturate(y * 7.0) * 0.82);
                }
                c += _SunColor.rgb * (pow(s, 8) * 0.6 + pow(s, 70) * 1.0);
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
