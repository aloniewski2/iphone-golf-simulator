// The lava lake of the Magma Open's crater (Course/LavaWorld.cs): molten rock all the way to the crater
// wall, bright throughout, with hotter veins and slow swirls that crawl. Unlit (it glows), textured by
// two baked layers (Resources/Course/Lava/lava_base and lava_glow, blended in world space so the
// rings of the lake mesh never stretch them), fogged like everything else. Listed in Always Included
// Shaders by ProjectSetup.
Shader "GolfArcade/LavaLake"
{
    Properties
    {
        _MainTex ("Crust", 2D) = "white" {}
        _GlowTex ("Glow", 2D) = "white" {}
        _Tile ("Yards per repeat", Float) = 110
        _Bright ("Brightness", Float) = 1.12
        _Flow ("Crawl", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex, _GlowTex;
            float _Tile, _Bright, _Flow;
            struct v2f { float4 pos : SV_POSITION; float2 w : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.w = mul(unity_ObjectToWorld, v.vertex).xz / _Tile;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Flow;
                float2 a = i.w + float2(t * 0.006, t * 0.0035);
                float2 b = i.w * 1.73 + float2(-t * 0.004, t * 0.005) + 0.37;
                fixed3 crust = tex2D(_MainTex, a).rgb;
                fixed3 glow = tex2D(_GlowTex, b).rgb;
                fixed3 c = crust * (0.72 + 0.62 * glow);                                // the fire under the crust
                c *= _Bright * (0.93 + 0.09 * sin(t * 0.9 + i.w.x * 5.0 + i.w.y * 3.0));   // it breathes
                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
