Shader "GolfArcade/TennisHorizon2"
{
    Properties
    {
        _HorizonFogShare("Horizon air perspective",Range(0,1)) = .4
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1,1,1,1)
        _Smoothness("Surface polish", Range(0,1)) = .22
        _ResortResponse("Resort material response", Range(0,1)) = 0
        _ArchitecturalRelief("Architectural light relief", Range(0,1)) = 0
        _TimberGrain("Fine timber grain", Range(0,.2)) = 0
        _Metallic("Metal", Range(0,1)) = 0
        _Variation("Broad material variation", Range(0,.2)) = .04
        _Grain("Fine material grain", Range(0,.1)) = .012
        _WorldMap("World albedo detail", 2D) = "white" {}
        _WorldMapWeight("World albedo influence", Range(0,1)) = 0
        _WorldMapScale("World albedo scale", Float) = .12
        _WorldMapReference("World albedo reference", Float) = 1
        _WorldNormal("Mineral relief", 2D) = "bump" {}
        _WorldNormalStrength("Mineral relief strength", Range(0,2)) = 0
        _MicroMap("Metre-scale micro aggregate",2D) = "gray" {}
        _MicroScale("Micro aggregate tiles per metre",Float) = 1
        _MicroStrength("Aggregate normal strength",Range(0,1)) = 0
        _Paving("Cut limestone paving joints",Float) = 0
        _MasonryMap("Cut limestone albedo", 2D) = "white" {}
        _MasonryNormal("Cut limestone normal data", 2D) = "bump" {}
        _MasonrySize("Metres per masonry tile", Vector) = (2.88,1.28,0,0)
        _MasonryWeight("Masonry on vertical faces", Range(0,1)) = 0
        _CourtFinish("Fine acrylic sports surface", Float) = 0
        _CoastBlend("Coast sand / meadow blend", Float) = 0
        _VertexTint("Authored vertex albedo tint", Float) = 0
        _ReflectionMap("Reflected sky",2D) = "black" {}
        _ReflectionWeight("Glazing sky reflection",Range(0,1)) = 0
        _ReflectionHeading("Sky heading",Float) = 0
        _SandColor("Shore sand", Color) = (.83,.77,.63,1)
        _GrassColor("Meadow", Color) = (.13,.30,.09,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull [_Cull]
        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Keep URP's automatic screen AO disabled for this material. The resort
            // consumes only a bounded indirect factor below, never sun/direct AO.
            #if defined(_SCREEN_SPACE_OCCLUSION)
                #define TENNIS_RESORT_SCREEN_AO 1
                #undef _SCREEN_SPACE_OCCLUSION
            #endif
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_ReflectionMap); SAMPLER(sampler_ReflectionMap);
            TEXTURE2D(_WorldMap); SAMPLER(sampler_WorldMap);
            TEXTURE2D(_WorldNormal); SAMPLER(sampler_WorldNormal);
            TEXTURE2D(_MicroMap); SAMPLER(sampler_MicroMap);
            TEXTURE2D(_MasonryMap); SAMPLER(sampler_MasonryMap);
            TEXTURE2D(_MasonryNormal); SAMPLER(sampler_MasonryNormal);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _MasonrySize; float _MasonryWeight;
                half4 _BaseColor;
                half _Smoothness, _Metallic, _Variation, _Grain, _ResortResponse, _TimberGrain, _ArchitecturalRelief, _HorizonFogShare;
                float _WorldMapWeight, _WorldMapScale, _WorldMapReference, _WorldNormalStrength, _MicroScale, _MicroStrength, _Paving, _CoastBlend, _CourtFinish, _VertexTint, _ReflectionWeight, _ReflectionHeading; half4 _SandColor, _GrassColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 colour:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half fog:TEXCOORD3; half3 vertexLight:TEXCOORD4; half4 colour:TEXCOORD5; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.colour = v.colour;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.vertexLight = VertexLighting(o.positionWS, o.normalWS);
                return o;
            }
            float Hash(float3 p) { p = frac(p * .1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1,0,0)), f.x), lerp(Hash(i + float3(0,1,0)), Hash(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0,0,1)), Hash(i + float3(1,0,1)), f.x), lerp(Hash(i + float3(0,1,1)), Hash(i + 1), f.x), f.y), f.z);
            }
            half4 Frag(Varyings i):SV_Target
            {
                half3 colour = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                colour *= lerp(half3(1,1,1),i.colour.rgb,_VertexTint);
                if (_CoastBlend > .5)
                    colour = lerp(_SandColor.rgb, _GrassColor.rgb, smoothstep(.03,.95,i.colour.r));
                if (_WorldMapWeight > .001)
                {
                    float3 weights = pow(abs(normalize(i.normalWS)),4); weights /= max(.001,weights.x+weights.y+weights.z);
                    float3 p = i.positionWS * _WorldMapScale;
                    half3 mapped = SAMPLE_TEXTURE2D(_WorldMap,sampler_WorldMap,p.yz).rgb*weights.x
                        + SAMPLE_TEXTURE2D(_WorldMap,sampler_WorldMap,p.xz).rgb*weights.y
                        + SAMPLE_TEXTURE2D(_WorldMap,sampler_WorldMap,p.xy).rgb*weights.z;
                    if (_CoastBlend > .5)
                    {
                        half steep = (1-smoothstep(.70,.93,abs(normalize(i.normalWS).y))) * i.colour.b;
                        colour = lerp(colour,mapped * _BaseColor.rgb,steep);
                    }
                    else colour *= lerp(half3(1,1,1),clamp(mapped/max(_WorldMapReference,.01),.35,1.8),_WorldMapWeight);
                }
                // Ashlar uses world height consistently on every wall face. The
                // authored repeat and normal data keep joints at real metre scale.
                half3 masonryNormal = normalize(i.normalWS);
                if (_MasonryWeight > .001)
                {
                    float3 n = normalize(i.normalWS);
                    float useX = step(abs(n.z),abs(n.x));
                    float3 tangent = useX > .5 ? float3(0,0,-sign(n.x)) : float3(sign(n.z),0,0);
                    float2 wallUV = float2(dot(i.positionWS,tangent)/_MasonrySize.x, i.positionWS.y/_MasonrySize.y);
                    float wall = (1-smoothstep(.25,.85,abs(n.y))) * _MasonryWeight;
                    half3 stone = SAMPLE_TEXTURE2D(_MasonryMap,sampler_MasonryMap,wallUV).rgb;
                    half3 relief = SAMPLE_TEXTURE2D(_MasonryNormal,sampler_MasonryNormal,wallUV).rgb * 2 - 1;
                    colour *= lerp(half3(1,1,1),stone,wall);
                    masonryNormal = normalize(n + (tangent*relief.x + float3(0,relief.y,0))*wall);
                }
                // Object-independent world grain works on the old untextured arena too.
                float broad = Noise(i.positionWS * .65) - .5;
                float fine = Noise(i.positionWS * 24) - .5;
                float grainFootprint = max(length(ddx(i.positionWS)),length(ddy(i.positionWS))) * 24;
                fine *= 1-smoothstep(.22,1.2,grainFootprint);
                colour *= 1 + broad * _Variation * 2 + fine * _Grain * 2;
                if (_TimberGrain > .001)
                {
                    float3 normal=abs(normalize(i.normalWS));
                    float2 plane=normal.y>.65?i.positionWS.xz:(normal.x>normal.z?i.positionWS.zy:i.positionWS.xy);
                    float timber=Noise(float3(plane.x*.7,plane.y*18,3.7))-.5;
                    colour *= 1+timber*_TimberGrain;
                }
                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.normalWS = masonryNormal;
                if (_WorldNormalStrength > .001)
                {
                    float3 weights=pow(abs(normalize(i.normalWS)),4); weights/=max(weights.x+weights.y+weights.z,.001);
                    float3 p=i.positionWS*_WorldMapScale;
                    half3 nx=UnpackNormalScale(SAMPLE_TEXTURE2D(_WorldNormal,sampler_WorldNormal,p.yz),_WorldNormalStrength);
                    half3 ny=UnpackNormalScale(SAMPLE_TEXTURE2D(_WorldNormal,sampler_WorldNormal,p.xz),_WorldNormalStrength);
                    half3 nz=UnpackNormalScale(SAMPLE_TEXTURE2D(_WorldNormal,sampler_WorldNormal,p.xy),_WorldNormalStrength);
                    half3 relief=half3(0,nx.x,nx.y)*weights.x + half3(ny.x,0,ny.y)*weights.y + half3(nz.x,nz.y,0)*weights.z;
                    input.normalWS=normalize(input.normalWS+relief);
                }
                if (_MicroStrength > .001 && abs(i.normalWS.y) > .65)
                {
                    half4 micro=SAMPLE_TEXTURE2D(_MicroMap,sampler_MicroMap,i.positionWS.xz*_MicroScale);
                    input.normalWS=normalize(input.normalWS+half3(micro.r*2-1,0,micro.g*2-1)*_MicroStrength);
                    // Small retained pigment variation; mipmaps integrate real millimetre aggregate.
                    colour *= 1+(micro.a-.5)*.028;
                }
                if (_Paving > .5 && i.normalWS.y > .65)
                {
                    float2 uv=i.positionWS.xz/float2(1.2,.6);
                    uv.x+=step(.5,frac(floor(uv.y)*.5))*.5;
                    float2 cell=frac(uv), edge=min(cell,1-cell)*float2(1.2,.6);
                    float distance=min(edge.x,edge.y);
                    float width=max(fwidth(distance),.0005);
                    half joint=1-smoothstep(.001-width,.003+width,distance);
                    colour *= 1-joint*.18;
                    colour *= .975+Hash(float3(floor(uv),17))*.05;
                }
                // The old 4cm procedural normal ripples made the acrylic look wet.
                // Only the opt-in mipmapped millimetre aggregate perturbs its normal now.
                input.viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fog;
                input.vertexLighting = i.vertexLight;
                input.bakedGI = SampleSH(input.normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1,1,1,1);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = colour; surface.alpha = 1; surface.occlusion = 1;
                #if defined(TENNIS_RESORT_SCREEN_AO)
                    half ambientAO=saturate(SampleAmbientOcclusion(input.normalizedScreenSpaceUV)+(1-_AmbientOcclusionParam.x));
                    surface.occlusion=lerp(1,ambientAO,.30h*_ResortResponse);
                #endif
                surface.metallic = _Metallic;
                surface.smoothness = saturate(_Smoothness + broad * _Variation * .35 - _CourtFinish*fine*.035);
                surface.normalTS = half3(0,0,1);
                half4 result = UniversalFragmentPBR(input, surface);
                if(_ReflectionWeight>.001)
                {
                    float3 reflected=reflect(-input.viewDirectionWS,input.normalWS);
                    float2 skyUV=float2(frac(atan2(reflected.z,reflected.x)/6.2831853+.5+_ReflectionHeading/360),
                        clamp(.53+asin(max(reflected.y,0))/3.14159265,.53,.975));
                    half3 sky=SAMPLE_TEXTURE2D_LOD(_ReflectionMap,sampler_ReflectionMap,skyUV,3).rgb;
                    half fresnel=pow(1-saturate(dot(input.normalWS,input.viewDirectionWS)),5);
                    result.rgb=lerp(result.rgb,sky*.75,_ReflectionWeight*(.22+fresnel*.65));
                }
                // Retain warm ivory highlight gradients before an ungraded target
                // clips them. A common RGB scale preserves material hue; the shoulder
                // is continuous, unit-slope at its knee, and never raises exposure.
                float peak=max(result.r,max(result.g,result.b));
                float shoulder=.72+.26*(1-exp(-max(peak-.72,0)/.26));
                float scale=peak>.72?shoulder/max(peak,.001):1;
                result.rgb *= lerp(1,scale,_ArchitecturalRelief);
                // Linear fog factor is1 nearby and0 at the horizon. Keep authored
                // ridge depth readable without changing the resort global atmosphere.
                result.rgb = MixFog(result.rgb, lerp(1,i.fog,_HorizonFogShare));
                return result;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback "Universal Render Pipeline/Lit"
}
