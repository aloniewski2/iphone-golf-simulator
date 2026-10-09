// Resort lava: authored fine heat relief under broad moving cooling plates and luminous channels.
// Geometry, hazards and lava height remain authoritative gameplay data.
Shader "GolfArcade/GolfLava"
{
    Properties
    {
        _BaseMap ("Lava_C (albedo: where the flow is bright)", 2D) = "white" {}
        _EmissionMap ("Lava_E (glow: the hottest veins)", 2D) = "black" {}
        _BumpMap ("Lava_N (normal: relief in the heat)", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _HeatAlbedo ("Heat from the albedo luminance", Range(0,4)) = 1.5
        _HeatGlow ("Heat from the glow luminance", Range(0,4)) = 1.5
        _HeatBias ("Heat bias", Range(-1,1)) = -0.1
        _HeatGain ("Contrast of the heat around .5", Range(0.5,4)) = 1.5
        _Relief ("Heat added by the normal map's slope", Range(0,1)) = 0.15
        _Deep ("Coolest colour (heat 0, a few % of the texels)", Color) = (0.58,0.13,0.04,1)
        _Crust ("Crust colour (heat .2)", Color) = (0.73,0.17,0.05,1)
        _Flow ("Flow colour (heat .6)", Color) = (0.88,0.31,0.065,1)
        _Hot ("Hottest colour (heat 1)", Color) = (1,0.5,0.11,1)
        _WorldUV ("Project the maps from the world (1) or use the mesh UV (0)", Range(0,1)) = 1
        _WorldTile ("World projection: tiles per world unit (yard); .9144 / 24 = one tile per 24 m", Float) = 0.0381
        _FogShare ("Share of the distance fog the lava takes (1 = like the rest of the scene; lava glows through haze)", Range(0,1)) = 1
        _PlateScale ("Broad cooling plate size in yards",Float)=38
        _Cooling ("Cooling plate coverage",Range(0,1))=1
        _SlopeFlow ("Consistent vertical flow projection",Range(0,1))=0
        _Cascade ("Tapered cascade hot core",Range(0,1))=0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "GolfLava"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _BumpScale, _HeatAlbedo, _HeatGlow, _HeatBias, _HeatGain, _Relief;
                half4 _Deep, _Crust, _Flow, _Hot;
                half _WorldUV, _FogShare;
                float _WorldTile,_PlateScale;half _Cooling,_SlopeFlow,_Cascade;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; };

            V vert (A i)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            float2 LavaHash(float2 p) { return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453); }
            float LavaNoise(float2 p) {
                float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(LavaHash(cell).x,LavaHash(cell+float2(1,0)).x,f.x),lerp(LavaHash(cell+float2(0,1)).x,LavaHash(cell+1).x,f.x),f.y);
            }
            float2 Plate(float2 p) {
                float2 cell=floor(p),f=frac(p);float first=8,second=8;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
                    float2 offset=float2(x,y);float2 delta=offset+LavaHash(cell+offset)-f;float d=dot(delta,delta);
                    if(d<first){second=first;first=d;}else second=min(second,d);
                }
                return float2(sqrt(second)-sqrt(first),LavaHash(cell).x);
            }
            half4 frag (V i) : SV_Target
            {
                // world projection: from above on floors (|n.y| >= .5), from the side on walls; the same tile size whatever the mesh's UVs are
                float3 an = abs(normalize(i.normalWS));
                float2 pw=lerp(i.positionWS.xz,float2(i.positionWS.x+i.positionWS.z*.37,i.positionWS.y),_SlopeFlow);
                float2 uv = lerp(i.uv, pw * _WorldTile * _BaseMap_ST.xy + _BaseMap_ST.zw, _WorldUV);
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half3 e = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
                half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                // the maps are sRGB colour textures, sampled LINEAR by the GPU: the heat is their GAMMA luminance (as painted), so it does not depend on the colour space
                c = pow(max(c, 0.0001h), 0.4545h); e = pow(max(e, 0.0001h), 0.4545h);   // linear -> gamma (the heat is defined on the painted values)
                half heat = dot(c, half3(0.2126h, 0.7152h, 0.0722h)) * _HeatAlbedo + dot(e, half3(0.2126h, 0.7152h, 0.0722h)) * _HeatGlow
                          + _HeatBias + (n.x + n.y) * 0.5h * _Relief;
                heat = saturate((heat - 0.5h) * _HeatGain + 0.5h);
                // the stops arrive linear (non-HDR Color properties are converted from sRGB by Material.SetColor); interpolated in linear
                half3 ramp = heat < 0.2h ? lerp(_Deep.rgb, _Crust.rgb, heat * 5.0h)
                           : heat < 0.6h ? lerp(_Crust.rgb, _Flow.rgb, (heat - 0.2h) * 2.5h)
                           : lerp(_Flow.rgb, _Hot.rgb, (heat - 0.6h) * 2.5h);
                // Warped iso-flow regions make asymmetrical warm channels and
                // cooling islands. No Voronoi joint lattice dominates the lake.
                float2 field=pw/max(_PlateScale,1);
                float2 warp=float2(LavaNoise(field*.24+13),LavaNoise(field*.24+47))*4.6;
                float2 flow=field+warp+float2(.009,-.006)*_Time.y;
                half broad=LavaNoise(flow*.48+float2(8,31));
                half meander=LavaNoise(flow*.69+float2(47,9));
                half temperature=LavaNoise(field*.17+float2(19,31));
                float width=.060+temperature*.065;
                half lane=1-smoothstep(width*.30,width,abs(meander-.49));
                half pool=smoothstep(.57,.76,broad);
                half flowing=saturate(lane*(.55+temperature*.45)+pool*.84);
                half warm=1-smoothstep(width,width+.10,abs(meander-.49));
                half crustGrain=LavaNoise(pw*.16+float2(17,29))*.55h+LavaNoise(pw*.047+float2(3,61))*.45h;
                half3 basalt=half3(.009h,.012h,.016h)*(.62h+crustGrain*.72h+heat*.16h);
                basalt+=half3(.024h,.004h,.001h)*warm*flowing;
                half3 channel=lerp(_Flow.rgb*.65h,_Hot.rgb,saturate(.22h+heat*.58h+temperature*.20h));
                half3 colour=lerp(basalt,channel,flowing);
                colour=lerp(ramp,colour,_Cooling);
                if(_Cascade>.5h){
                    // Mesh U is cross-stream: a white-hot interior grades through
                    // orange into cooling dark margins, flowing down the slope.
                    float crossFlow=abs(i.uv.x*2-1);
                    float streak=LavaNoise(float2(i.uv.x*8,i.uv.y*18-_Time.y*.16));
                    float core=1-smoothstep(.30,.93,crossFlow+streak*.085);
                    half3 edgeColour=half3(.019h,.023h,.029h)*(.85h+heat*.24h);
                    half3 coreColour=lerp(_Flow.rgb,_Hot.rgb,saturate(.52h+heat*.40h+streak*.12h));
                    colour=lerp(edgeColour,coreColour,core);
                }
                // Keep the cooling crust dark while hot channels cross the HDR bloom threshold.
                colour *= lerp(1.0h, 3.0h, smoothstep(.06h,.55h,max(colour.r,max(colour.g,colour.b))));
                half fog = InitializeInputDataFog(float4(i.positionWS, 1), 0);
                return half4(lerp(colour, MixFog(colour, fog), _FogShare), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
