// Stylized sea: shallow turquoise near the shore deepening to blue, gentle procedural swell
// in the normals, a sky-tinted fresnel, and sun sparkle that bloom picks up.
Shader "GolfArcade/TennisWater"
{
    Properties
    {
        _Shallow ("Shallow", Color) = (0.05,0.75,0.72,1)
        _Deep ("Deep", Color) = (0.02,0.28,0.55,1)
        _Sky ("Sky reflection", Color) = (0.55,0.75,0.95,1)
        _ShoreMap("Actual coast shallow depth",2D)="black"{}
        _UseShoreMap("Authored shoreline",Float)=0
        _ShoreBounds("Coast bounds x z width depth",Vector)=(-242.5,-187.5,477.5,632.5)
        _DeepDistance ("Deepening distance", Float) = 60
        _WaveScale ("Wave scale", Float) = 0.35
        _Sparkle ("Sparkle", Float) = 6
        _FragmentFog("Actual surface-depth fog",Range(0,1))=0
        _FilterWaves("Projected wave antialiasing",Range(0,1))=0
        _SkyMap("Existing coastal sky reflection",2D)="black"{}
        _SkyHeading("Coastal sky heading",Float)=-45
        _ReflectionWeight("Cloud reflection variation",Range(0,1))=0
        _SunSheen("Rough sun reflection",Range(0,2))=0
        _WaterDebug("Fog contribution diagnostic",Float)=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_ShoreMap); SAMPLER(sampler_ShoreMap);
            TEXTURE2D(_SkyMap); SAMPLER(sampler_SkyMap);
            float4x4 _TennisShoreWorldToUV;
            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Sky; float _DeepDistance, _WaveScale, _Sparkle, _UseShoreMap, _FragmentFog, _FilterWaves, _SkyHeading, _ReflectionWeight, _SunSheen, _WaterDebug; float4 _ShoreBounds;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };
            V vert (A i) { V o; VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz); o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.fog = ComputeFogFactor(p.positionCS.z); return o; }
            float2 wave (float2 p, float2 dir, float freq, float speed)
            {
                // A slow spatial bend breaks the parallel marching bands without
                // making this calm resort sea rough or displacing contact surfaces.
                float phase = dot(p, dir) * freq + sin(dot(p, float2(.13, .17))) * 1.8 + sin(dot(p,float2(.047,-.063))+sin(p.x*.11)) * 3.1 + _Time.y * speed;
                // At the low gameplay angle, one offshore pixel can span many
                // periods. Resolve that subpixel slope into its zero mean rather
                // than letting raw cosine samples become horizontal stair bands.
                // Gaussian footprint integration is bounded and continuous;
                // resolved near waves retain essentially their authored amplitude.
                float footprint=max(abs(ddx(phase)),abs(ddy(phase)));
                float resolved=exp2(-.36*footprint*footprint);
                return dir * cos(phase) * freq * lerp(1,resolved,_FilterWaves);
            }
            half4 frag (V i) : SV_Target
            {
                float2 p = i.positionWS.xz * _WaveScale;
                float2 slope = wave(p, normalize(float2(1, .3)), 1.3, 1.1) * .035 + wave(p, normalize(float2(-.4, 1)), 2.1, 1.7) * .022
                             + wave(p, normalize(float2(.7, -.8)), 3.7, 2.3) * .009 + wave(p, normalize(float2(-1, -.2)), 6.1, 3.1) * .004;
                half3 n = normalize(half3(-slope.x, 1, -slope.y));
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float distance = length(i.positionWS.xz);
                half3 water = lerp(_Shallow.rgb, _Deep.rgb, saturate((distance - 20) / _DeepDistance));
                if (_UseShoreMap > .5)
                {
                    float2 shoreUV=mul(_TennisShoreWorldToUV,float4(i.positionWS,1)).xy;
                    float inside=step(0,shoreUV.x)*step(shoreUV.x,1)*step(0,shoreUV.y)*step(shoreUV.y,1);
                    half shallow=SAMPLE_TEXTURE2D(_ShoreMap,sampler_ShoreMap,saturate(shoreUV)).r*inside;
                    water=lerp(_Deep.rgb,_Shallow.rgb,shallow);
                    half surf=smoothstep(.70,.92,shallow)*(1-smoothstep(.965,1,shallow));
                    surf*=.85+.15*sin(i.positionWS.x*.09+i.positionWS.z*.13+_Time.y*.32);
                    water=lerp(water,half3(.58,.77,.72),surf*.22);
                }
                half fresnel = pow(1 - saturate(dot(n, v)), 4);
                Light sun = GetMainLight();
                half3 h = normalize(sun.direction + v);
                half sparkle = pow(saturate(dot(n, h)), 380) * _Sparkle;
                half3 lit = water * (SampleSH(n) + sun.color * saturate(dot(n, sun.direction)) * .5);
                // Broad reflected sky keeps its blue / turquoise chroma rather than
                // washing the resort sea to grey at the gameplay grazing angle.
                half3 reflectedSky=_Sky.rgb;
                if(_ReflectionWeight>.001)
                {
                    half3 reflected=reflect(-v,n);
                    float2 skyUV=float2(frac(atan2(reflected.z,reflected.x)/6.2831853+.5+_SkyHeading/360),
                        clamp(.5+asin(clamp(reflected.y,-1,1))/3.14159265,0.001,.999));
                    // The existing sky contributes only broad reflected cloud variation;
                    // explicit LOD keeps calm-water reflections from aliasing at distance.
                    half3 panorama=SAMPLE_TEXTURE2D_LOD(_SkyMap,sampler_SkyMap,skyUV,2).rgb;
                    reflectedSky=lerp(reflectedSky,panorama*half3(.45,.72,.94),_ReflectionWeight);
                }
                half3 colour = lerp(water * .72 + lit * .28, reflectedSky, fresnel * .42) + sun.color * sparkle;
                if(_SunSheen>.001)
                {
                    // A restrained rough dielectric lobe keeps the sunlight tied to
                    // actual view/light direction. No painted or random white speckles.
                    half nl=saturate(dot(n,sun.direction)),nv=saturate(dot(n,v)),nh=saturate(dot(n,h));
                    half a2=.0105h,denom=nh*nh*(a2-1)+1;
                    half distribution=a2/(3.14159265h*denom*denom);
                    half viewFresnel=.02h+.98h*pow(1-saturate(dot(v,h)),5);
                    half k=.0512h;
                    half masking=(nl/(nl*(1-k)+k))*(nv/(nv*(1-k)+k));
                    half sheen=distribution*viewFresnel*masking/max(4*nl*nv,.02h);
                    colour+=sun.color*min(sheen,.12h)*_SunSheen;
                }
                // The source ocean is one 2400x1500m box. Corner fog factors are
                // clamped far outside the visible water and interpolate incorrectly.
                // URP's world-position fog evaluates the actual fragment's view depth.
                half fog=lerp(i.fog,InitializeInputDataFog(float4(i.positionWS,1),i.fog),_FragmentFog);
                if(_WaterDebug>.5)
                    return half4((IsFogEnabled()?1-ComputeFogIntensity(fog):0).xxx,1);
                return half4(MixFog(colour, fog), 1);
            }
            ENDHLSL
        }
    }
}
