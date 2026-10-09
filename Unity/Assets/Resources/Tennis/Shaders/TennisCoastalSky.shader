Shader "GolfArcade/TennisCoastalSky"
{
 Properties { _Panorama("Authored equirectangular sky",2D)="white"{} _Rotation("Heading",Float)=0 _Exposure("Exposure",Float)=1 _CloudCompression("Cloud altitude compression",Float)=1 _LongitudeScale("Cloud angular size",Float)=1 _DayHaze("Daylight haze",Range(0,1))=0 _AirColorShare("Clear-sky colour calibration",Range(0,1))=.58 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off
  Pass {
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_Panorama);SAMPLER(sampler_Panorama);
   CBUFFER_START(UnityPerMaterial) float _Rotation,_Exposure,_CloudCompression,_LongitudeScale,_DayHaze,_AirColorShare; CBUFFER_END
   struct A {float4 p:POSITION;};struct V {float4 p:SV_POSITION;float3 direction:TEXCOORD0;};
   V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.direction=a.p.xyz;return o;}
   half4 frag(V i):SV_Target {
    float3 d=normalize(i.direction);
    float longitude=(atan2(d.z,d.x)/6.2831853+.5+_Rotation/360.0)*_LongitudeScale;
    float latitude=clamp(.5+asin(clamp(d.y,-1.0,1.0))/3.14159265*_CloudCompression,.001,.999);
    // Analytic longitude gradients do not jump at atan2 or frac seams. Implicit
    // gradients created the one-pixel bright dashed stripe in actual hole17.
    float denominator=max(dot(d.xz,d.xz),.00001)*6.2831853;
    float duX=(d.x*ddx(d.z)-d.z*ddx(d.x))/denominator*_LongitudeScale;
    float duY=(d.x*ddy(d.z)-d.z*ddy(d.x))/denominator*_LongitudeScale;
    float2 gradientX=float2(duX,ddx(latitude)),gradientY=float2(duY,ddy(latitude));
    float2 uv=float2(frac(longitude),latitude);
    half3 sky=SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,uv,gradientX,gradientY).rgb;
    half3 edges=(SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,float2(.012,latitude),gradientX,gradientY).rgb
               +SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,float2(.988,latitude),gradientX,gradientY).rgb)*.5;
    sky=lerp(edges,sky,smoothstep(0,.035,min(uv.x,1-uv.x)));
    // Keep actual cloud radiance and silhouette; calibrate only clear blue toward airy coastal cyan.
    half cloud=smoothstep(.05,.33,min(sky.r,sky.g));
    half3 clear=lerp(half3(.40,.64,.82),half3(.10,.37,.65),pow(saturate(d.y),.42));
    sky=lerp(sky,clear,_AirColorShare*(1-cloud));
    sky=lerp(sky,half3(.40,.58,.71),_DayHaze*(1-saturate(d.y)*.65)*(1-cloud*.65));
    // The visible sun and its bloom come from the same direction as world shadows.
    Light sun=GetMainLight();float alignment=saturate(dot(d,sun.direction));
    sky+=sun.color*(pow(alignment,6000)*3.5+pow(alignment,100)*.04);
    return half4(sky*_Exposure,1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
