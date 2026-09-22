// Soft contact shadow under players and the ball. Multiplies the court down rather than
// painting black over it, so it reads as occlusion on any surface colour.
Shader "GolfArcade/TennisShadowBlob"
{
    Properties { _MainTex ("Falloff", 2D) = "white" {} _Strength ("Strength", Range(0,1)) = 0.5 }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; half _Strength;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                half a = tex2D(_MainTex, i.uv).a * _Strength;
                return fixed4((1 - a).xxx, 1);
            }
            ENDCG
        }
    }
}
