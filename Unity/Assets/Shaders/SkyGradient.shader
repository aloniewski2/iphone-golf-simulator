// The sky, the way Golf Dreams paints it: clear cyan overhead fading to a bright, near-white
// horizon, and below the horizon the haze colour, so the sea's far edge melts into it. Set at
// start-up by GolfGame (and made the ambient light's source).
Shader "GolfArcade/SkyGradient"
{
    Properties
    {
        _Top ("Overhead", Color) = (0.24, 0.70, 0.97, 1)
        _Horizon ("Horizon", Color) = (0.80, 0.94, 1.0, 1)
        _Below ("Below the horizon", Color) = (0.74, 0.90, 0.99, 1)
        _Falloff ("How fast it deepens above the horizon", Float) = 0.45
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
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION) { v2f o; o.pos = UnityObjectToClipPos(vertex); o.dir = vertex.xyz; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                float y = normalize(i.dir).y;
                if (y < 0) return lerp(_Horizon, _Below, saturate(-y * 8));
                return lerp(_Horizon, _Top, pow(saturate(y), _Falloff));
            }
            ENDCG
        }
    }
}
