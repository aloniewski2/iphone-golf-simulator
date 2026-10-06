// DRAFT. Editor proof only. Never assign this shader to a gameplay asset.
Shader "Hidden/GolfArcade/PostcardV3Proof"
{
    Properties
    {
        _Role ("0 occluder, 1 landmark, 2 cliff, 3 top, 4 fairway band, 5 green band, 6 channel", Float) = 0
        _BandMap ("Exact generator primary band float field", 2D) = "gray" {}
        _BaseMap_ST ("Original base-map UV scale/offset", Vector) = (1,1,0,0)
        _PlayY ("Frozen play surface, world yards", Float) = 6.56168
        _ProofPlantWind ("Live wind copied for proof occlusion", Vector) = (0,0,0,0)
        _PlantSway ("Copy actual plant displacement for occlusion", Float) = 0
        _Cull ("Original material culling", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On ZTest LEqual Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BandMap); SAMPLER(sampler_BandMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ProofPlantWind;
                float _Role, _PlayY, _PlantSway, _Cull;
                float _ChannelCount;
                float4 _ChannelPoints[16];
            CBUFFER_END
            float hash(float2 p) { float3 q=frac(float3(p.xyx)*.1031); q+=dot(q,q.yzx+33.33); return frac((q.x+q.y)*q.z); }
            float3 plantOffset(float3 p, float4 c)
            {
                float amp=length(_ProofPlantWind.xz);
                float w=(amp>1e-5 && c.b<.5) ? saturate(c.r) : 0;
                float2 dir=_ProofPlantWind.xz/max(amp,1e-5);
                float2 origin=GetObjectToWorldMatrix()._m03_m23;
                float2 cell=round(origin*100);
                float h=hash(cell), h2=hash(cell+31.7), h3=hash(cell+71.3), u=_ProofPlantWind.w/600;
                float travel=dot(origin,dir)*.025;
                float s1=sin(6.2831853*((270+6*floor(h2*4.999))*u+frac(h+c.g)));
                float s2=sin(6.2831853*((498+6*floor(h3*4.999))*u+frac(h2+c.g*.5)));
                float sc=sin(6.2831853*(360*u+h3));
                float gust=.8+.2*sin(6.2831853*(30*u-travel));
                float lean=.5+.5*gust*(.62*s1+.38*s2), side=.2*gust*sc;
                float2 d=(dir*lean+float2(-dir.y,dir.x)*side)*(amp*w);
                return TransformWorldToObjectDir(float3(d.x,0,d.y),false);
            }
            struct A { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            V vert(A a)
            {
                V o; a.positionOS+=_PlantSway*plantOffset(a.positionOS,a.color);
                o.world=TransformObjectToWorld(a.positionOS); o.positionCS=TransformWorldToHClip(o.world);
                o.normal=TransformObjectToWorldNormal(a.normalOS); o.uv=a.uv*_BaseMap_ST.xy+_BaseMap_ST.zw; return o;
            }
            bool inChannel(float2 p)
            {
                bool hit=false; int count=(int)_ChannelCount;
                for(int i=0,j=count-1;i<count;j=i++)
                {
                    float2 a=_ChannelPoints[i].xy,b=_ChannelPoints[j].xy;
                    if((a.y>p.y)!=(b.y>p.y))
                        if(p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) hit=!hit;
                }
                return hit;
            }
            half4 frag(V i):SV_Target
            {
                // Every excluded surface writes black/depth rather than being clipped away.
                if(_Role==1) return half4(1,1,1,1);
                if(_Role==2 && i.world.y<_PlayY-.02 && abs(normalize(i.normal).y)<.9) return half4(1,0,0,1);
                if(_Role==3 && abs(i.world.y-_PlayY)<.15 && normalize(i.normal).y>.8) return half4(0,1,0,1);
                if(_Role==6 && _ChannelCount>=3 && inChannel(i.world.xz)) return half4(1,1,1,1);
                if(_Role==4 || _Role==5)
                {
                    float b=SAMPLE_TEXTURE2D(_BandMap,sampler_BandMap,i.uv).r;
                    if(_Role==4 && b>=.9) return half4(1,0,0,1);
                    if(_Role==4 && b<=.1) return half4(0,1,0,1);
                    if(_Role==5 && b>=.9) return half4(0,0,1,1);
                    if(_Role==5 && b<=.1) return half4(1,0,1,1);
                }
                return half4(0,0,0,1);
            }
            ENDHLSL
        }
    }
}
