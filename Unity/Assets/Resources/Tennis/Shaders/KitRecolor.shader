// The character screen's outfit colours: re-tints the player's kit texture once, at load, into
// a render texture. Each mask channel (blender/scripts/bake_kit_mask.py) marks a region — shirt,
// shorts, accent, skin — and a region's new colour keeps the texel's shading by scaling with
// its luminance relative to the region's mean (_Ref). A colour's alpha of 0 leaves it alone.
Shader "Hidden/GolfArcade/KitRecolor"
{
    Properties
    {
        _MainTex ("Kit", 2D) = "white" {}
        _Mask ("Regions", 2D) = "black" {}
        _Shirt ("Shirt", Color) = (1,1,1,0)
        _Shorts ("Shorts", Color) = (1,1,1,0)
        _Accent ("Accent", Color) = (1,1,1,0)
        _Skin ("Skin", Color) = (1,1,1,0)
        _Ref ("Region mean luminance", Vector) = (.08,.08,.28,.27)
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _Mask;
            float4 _Shirt, _Shorts, _Accent, _Skin, _Ref;
            float lum(float3 c) { return dot(c, float3(.2126, .7152, .0722)); }
            float3 tint(float3 c, float l, float4 target, float refLum, float w)
            {
                // Shading follows the original, around the new colour; highlights may lift it a little.
                float3 shaded = target.rgb * clamp(l / max(refLum, .01), .25, 1.35);
                return lerp(c, shaded, saturate(w * target.a));
            }
            float4 frag (v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float4 m = tex2D(_Mask, i.uv);
                float l = lum(c.rgb);
                float3 o = c.rgb;
                o = tint(o, l, _Shirt, _Ref.x, m.r);
                o = tint(o, l, _Shorts, _Ref.y, m.g);
                o = tint(o, l, _Accent, _Ref.z, m.b);
                o = tint(o, l, _Skin, _Ref.w, m.a);
                return float4(o, c.a);
            }
            ENDCG
        }
    }
}
