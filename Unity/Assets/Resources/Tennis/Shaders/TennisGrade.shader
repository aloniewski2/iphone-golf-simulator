// Final colour grade for the tennis camera: a filmic shoulder so bright court and sky roll
// off instead of clipping, a little saturation and contrast for the tropical light, a warm
// lift in the shadows, and a soft vignette that keeps the eye on the players.
Shader "Hidden/GolfArcade/TennisGrade"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half _Exposure, _Saturation, _Contrast, _Vignette, _Shoulder;
            half4 _Lift, _Gain;

            // The project renders in gamma space, where a full filmic curve lifts the
            // mid-tones and washes the image out. Only values above the knee are compressed,
            // so HDR highlights (sun on the court, white kit) roll off instead of clipping
            // while skin and court keep their authored brightness.
            half3 Shoulder (half3 x)
            {
                const half knee = 0.78;
                half3 over = max(x - knee, 0);
                return min(x, knee) + (1 - knee) * (1 - exp(-over / (1 - knee)));
            }

            fixed4 frag (v2f_img i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb * _Exposure;
                c = lerp(saturate(c), Shoulder(c), _Shoulder);
                half luma = dot(c, half3(0.2126, 0.7152, 0.0722));
                c = lerp(luma.xxx, c, _Saturation);
                c = (c - 0.5) * _Contrast + 0.5;
                c = c * _Gain.rgb + _Lift.rgb;
                half2 d = i.uv - 0.5;
                c *= 1 - dot(d, d) * _Vignette;
                return fixed4(saturate(c), 1);
            }
            ENDCG
        }
    }
}
