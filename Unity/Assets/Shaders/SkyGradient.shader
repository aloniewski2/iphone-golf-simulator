// The sky, the way Golf Dreams paints it: clear cyan overhead fading to a bright, near-white
// horizon, and below the horizon the haze colour, so the sea's far edge melts into it; and a
// ring of soft stylised cumulus low round the horizon (Resources/Sky/clouds.png — a Higgsfield
// painting, the clouds lifted off its flat sky into alpha and the ends blended so it wraps),
// fading toward the haze as they near the horizon. Set at start-up by GolfGame.
Shader "GolfArcade/SkyGradient"
{
    Properties
    {
        _Top ("Overhead", Color) = (0.24, 0.70, 0.97, 1)
        _Horizon ("Horizon", Color) = (0.72, 0.90, 1.0, 1)
        _Below ("Below the horizon", Color) = (0.74, 0.90, 0.99, 1)
        _Falloff ("How fast it deepens above the horizon", Float) = 0.32
        _Clouds ("Clouds (RGBA strip, bottom row on the horizon)", 2D) = "black" {}
        _CloudRepeat ("Times the strip goes round the horizon", Float) = 2
        _CloudTop ("Elevation of the strip's top edge, degrees", Float) = 56
        _CloudStrength ("Clouds", Range(0, 1)) = 0.95
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
            fixed4 _Top, _Horizon, _Below;
            float _Falloff;
            sampler2D _Clouds;
            float _CloudRepeat, _CloudTop, _CloudStrength;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION) { v2f o; o.pos = UnityObjectToClipPos(vertex); o.dir = vertex.xyz; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                if (y < 0) return lerp(_Horizon, _Below, saturate(-y * 8));
                fixed4 c = lerp(_Horizon, _Top, pow(saturate(y), _Falloff));
                float v = asin(y) / radians(_CloudTop);
                if (v < 1)
                {
                    // round the horizon by heading; level 0 so the wrap at the back has no seam
                    float u = atan2(d.x, d.z) / (2 * UNITY_PI) * _CloudRepeat;
                    fixed4 cloud = tex2Dlod(_Clouds, float4(u, v, 0, 0));
                    cloud.rgb = lerp(_Horizon.rgb, cloud.rgb, saturate(0.8 + v * 0.8));    // the most distant sink a little into the haze
                    c.rgb = lerp(c.rgb, cloud.rgb, cloud.a * _CloudStrength);
                }
                return c;
            }
            ENDCG
        }
    }
}
