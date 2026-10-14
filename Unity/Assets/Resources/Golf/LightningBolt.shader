// A lightning bolt: a white-hot core and a violet-blue glow round it, added onto the picture, along
// a LineRenderer (u along the bolt, v across it). `_Intensity` flickers it (WeatherView); the line's
// colour gradient fades a branch toward its tip. No fog: it is light.
Shader "GolfArcade/LightningBolt"
{
    Properties
    {
        _Core ("Core", Color) = (1, 1, 1, 1)
        _Glow ("Glow", Color) = (0.55, 0.6, 1, 1)
        _Intensity ("Intensity", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Bolt"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Core, _Glow;
                float _Intensity;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.color = i.color; return o; }
            half4 frag(V i) : SV_Target
            {
                float x = (i.uv.y - 0.5) * 2;
                float core = exp(-x * x * 55);
                float glow = exp(-x * x * 5) * 0.45;
                // brighter in pulses along its length, as a real stroke looks
                float pulse = 0.8 + 0.2 * sin(i.uv.x * 40 + _Time.y * 90);
                half3 light = (_Core.rgb * core * 2.2 + _Glow.rgb * glow) * pulse * _Intensity * i.color.a;
                return half4(light, 0);
            }
            ENDHLSL
        }
    }
}
