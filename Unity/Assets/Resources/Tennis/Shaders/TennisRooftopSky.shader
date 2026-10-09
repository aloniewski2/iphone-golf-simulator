Shader "GolfArcade/TennisRooftopSky"
{
 Properties { _Panorama("Authored upper sky / cirrus",2D)="white"{} _Rotation("Heading",Float)=0 _Exposure("Exposure",Float)=.9 }
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
   CBUFFER_START(UnityPerMaterial)float _Rotation,_Exposure;CBUFFER_END
   struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 d:TEXCOORD0;};
   V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.d=a.p.xyz;return o;}
   half4 frag(V i):SV_Target{
    float3 d=normalize(i.d);
    float longitude=atan2(d.z,d.x)/6.2831853+.5+_Rotation/360;
    // Only the photograph's sky above its cloud horizon maps to the dome.
    // Lower cloud shapes remain the existing horizontal cloud plane, never a spherical ground.
    float latitude=clamp(.535+asin(max(d.y,0))/3.14159265*.88,.535,.975);
    float denominator=max(dot(d.xz,d.xz),.00001)*6.2831853;
    float2 gx=float2((d.x*ddx(d.z)-d.z*ddx(d.x))/denominator,ddx(latitude));
    float2 gy=float2((d.x*ddy(d.z)-d.z*ddy(d.x))/denominator,ddy(latitude));
    float u=frac(longitude);
    half3 sky=SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,float2(u,latitude),gx,gy).rgb;
    half3 edges=(SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,float2(.012,latitude),gx,gy).rgb+SAMPLE_TEXTURE2D_GRAD(_Panorama,sampler_Panorama,float2(.988,latitude),gx,gy).rgb)*.5;
    sky=lerp(edges,sky,smoothstep(0,.035,min(u,1-u)));
    half3 lower=half3(.34,.34,.46);
    sky=lerp(lower,sky,smoothstep(-.12,.015,d.y));
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
