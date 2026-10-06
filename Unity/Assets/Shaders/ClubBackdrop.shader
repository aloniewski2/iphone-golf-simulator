// Adnan's ClubBackdrop (GolfArcade/Unity/ClubDesign.swift on his branch), drawn by the game camera on a
// quad far behind the golfer (Game/ClubStage.cs): a painted scene covering the screen and drifting very
// slightly, light shafts from the top right, dust drifting up through them, and a lagoon tint so the type
// on top always reads. The second shader is the lit disc the golfer stands on.
Shader "GolfArcade/ClubBackdrop"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "black" {}
        _Tint ("Lagoon tint", Range(0, 1)) = 0.35
        _Deep ("Lagoon deep", Color) = (0.031, 0.102, 0.18, 1)
        _Cover ("Cover fit: uv scale (xy), offset (zw)", Vector) = (1, 1, 0, 0)
        _Motion ("Motion (0 still)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "IgnoreProjector" = "True" }
        ZWrite Off Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Tint, _Motion;
            float4 _Deep, _Cover;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float hash(float n) { return frac(sin(n * 12.9898) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Motion;
                float2 uv = i.uv;
                // the scene, covering the screen, a touch larger than it and drifting
                float2 drift = float2(sin(t * 0.05) * 0.012, cos(t * 0.04) * 0.01);
                float2 suv = (uv - 0.5) * _Cover.xy / 1.06 + 0.5 + _Cover.zw + drift * _Cover.xy;
                float3 c = tex2D(_MainTex, suv).rgb;

                // light shafts from the top right, swaying, fading down the screen
                float shafts = 0;
                for (int k = 0; k < 4; k++)
                {
                    float x0 = 0.55 + k * 0.11 + sin(t * 0.3 + k) * 0.02;
                    float down = 1 - uv.y;
                    float centre = lerp(x0 + 0.025, x0 - 0.215, down);
                    float hw = lerp(0.025, 0.035, down);
                    shafts += smoothstep(hw, hw * 0.4, abs(uv.x - centre));
                }
                c += shafts * 0.10 * uv.y;

                // dust drifting up through the light
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float dust = 0;
                for (int m = 0; m < 16; m++)
                {
                    float s = m * 7.31;
                    float2 p = float2(frac(s * 0.37 + 0.13) + sin(t + s) * 0.02, frac(hash(s) + t * (0.012 + (m % 5) * 0.006)));
                    float r = (1.2 + (m % 3)) * 0.0022;
                    float2 d = (uv - p) * float2(aspect, 1);
                    dust += smoothstep(r, r * 0.3, length(d));
                }
                c += saturate(dust) * 0.35 * _Motion;

                // the lagoon tint: deeper at the top and the foot, lighter through the middle
                float a = uv.y > 0.5 ? lerp(_Tint * 0.3, _Tint + 0.25, (uv.y - 0.5) * 2) : lerp(_Tint + 0.35, _Tint * 0.3, uv.y * 2);
                c = lerp(c, _Deep.rgb, saturate(a));
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
