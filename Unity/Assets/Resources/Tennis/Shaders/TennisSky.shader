// Painted sky dome. A wide panorama (generated as a matte painting) is wrapped across the
// half of the sky the gameplay camera faces and mirrored behind, with a procedural gradient
// taking over below the painting's horizon and a soft sun glow on top.
Shader "GolfArcade/TennisSky"
{
    Properties
    {
        _Panorama ("Panorama", 2D) = "white" {}
        _Horizon ("Horizon colour", Color) = (1,0.86,0.7,1)
        _Zenith ("Zenith colour", Color) = (0.18,0.45,0.9,1)
        _Sea ("Below-horizon sea colour", Color) = (0.66,0.75,0.86,1)
        _SunDir ("Sun direction", Vector) = (-0.5,0.35,0.75,0)
        _SunColor ("Sun glow", Color) = (1,0.85,0.6,1)
        _Arc ("Degrees the painting spans", Float) = 180
        _Heading ("Heading of the painting's centre", Float) = 0
        _Exposure ("Exposure", Float) = 0.95
        _PaintingWeight ("Painting influence", Range(0,1)) = 0
        _CloudScale ("Cloud size", Float) = 23
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Panorama); SAMPLER(sampler_Panorama);
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon, _Zenith, _SunColor, _Sea; float4 _SunDir; float _Arc, _Heading, _Exposure, _PaintingWeight, _CloudScale;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };
            V vert (A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.dir = i.positionOS.xyz; return o; }
            float Hash(float3 p) { p=frac(p*.1031); p+=dot(p,p.yzx+33.33); return frac((p.x+p.y)*p.z); }
            float Noise(float3 p)
            {
                float3 i=floor(p),f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(i),Hash(i+float3(1,0,0)),f.x),lerp(Hash(i+float3(0,1,0)),Hash(i+float3(1,1,0)),f.x),f.y),
                    lerp(lerp(Hash(i+float3(0,0,1)),Hash(i+float3(1,0,1)),f.x),lerp(Hash(i+float3(0,1,1)),Hash(i+1),f.x),f.y),f.z);
            }
            half4 frag (V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float elevation = asin(saturate(d.y)) / (PI * 0.5);           // 0 horizon .. 1 zenith
                // Heading relative to the painting's centre, folded with a triangle wave so the
                // painting mirrors seamlessly instead of showing a seam behind the camera.
                float heading = degrees(atan2(d.x, d.z)) - _Heading + _Arc * 0.5;
                float x = fmod(fmod(heading, 2 * _Arc) + 2 * _Arc, 2 * _Arc);
                float u = (x > _Arc ? 2 * _Arc - x : x) / _Arc;
                float v = saturate(elevation * 1.35);
                half3 painted = SAMPLE_TEXTURE2D(_Panorama, sampler_Panorama, float2(u, v)).rgb;
                half3 gradient = lerp(_Horizon.rgb, _Zenith.rgb, pow(saturate(elevation), 0.55));
                // Above the painting, blend into the painting's own top-edge blue (sampled across the
                // row) over a wide band — no hard ring / off-colour disc at the zenith (toss-up cams).
                half3 paintTop = SAMPLE_TEXTURE2D_LOD(_Panorama, sampler_Panorama, float2(u, 0.985), 0).rgb;
                half3 zenith = lerp(paintTop, _Zenith.rgb, smoothstep(0.8, 1.0, elevation) * 0.35);
                half3 upper = lerp(painted, zenith, smoothstep(0.5, 0.74, elevation));
                half3 sky = lerp(gradient, upper, smoothstep(0.0, 0.06, elevation) * _PaintingWeight);
                // Sparse, small white cumulus leaves a broad, calm blue window over the
                // court. Three-dimensional noise keeps the sky seamless in all cameras.
                float3 cp=d*_CloudScale+float3(3.7,1.2,7.3);
                float cloud=Noise(cp)*.65+Noise(cp*2.07)*.25+Noise(cp*4.13)*.10;
                cloud=smoothstep(.635,.705,cloud)*smoothstep(.065,.20,d.y)*(1-smoothstep(.78,.98,d.y));
                half3 cloudColour=lerp(half3(.74,.83,.89),half3(.94,.97,1),saturate(d.y*2+.25));
                sky=lerp(sky,cloudColour,cloud*.88);
                // Score80 E: below the horizon (high flyover / aerial cams see past the ocean plane's edge) the
                // warm haze fades into the far sea's own blue, so the water never ends in a beige void.
                sky = lerp(sky, _Sea.rgb, smoothstep(0.0, 0.1, -d.y));
                float sun = saturate(dot(d, normalize(_SunDir.xyz)));
                sky += _SunColor.rgb * (pow(sun, 2048) * .8 + pow(sun, 64) * .045);
                return half4(sky * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
