// The inside of the cup — the cut turf, the soil, the white liner, the dark depth — drawn
// through the green wherever the mouth (HoleMask) marked the stencil, ignoring the green's depth,
// so the hole has real depth even though the putting surface is not cut. Vertex colour carries
// the materials and the shade that deepens toward the bottom; a touch of the sun's direction
// keeps the sunlit wall lighter than the shaded one.
Shader "GolfArcade/HoleInside"
{
    Properties { _Color ("Tint", Color) = (1, 1, 1, 1) }
    SubShader
    {
        Tags { "Queue" = "Geometry+2" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ZWrite On
        ZTest Always
        Cull Back
        Stencil { Ref 64 ReadMask 64 Comp Equal }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = UnityObjectToWorldNormal(v.normal);
                float lit = 0.72 + 0.28 * saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                o.color = v.color * _Color * lit;
                o.color.a = 1;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target { return i.color; }
            ENDCG
        }
    }
}
