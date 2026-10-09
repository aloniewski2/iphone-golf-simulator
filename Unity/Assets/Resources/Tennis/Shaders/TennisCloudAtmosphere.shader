Shader "GolfArcade/TennisCloudAtmosphere"
{
 Properties { _CloudTops("Authored horizontal cloud tops",2D)="white"{} _WorldScale("World tile scale",Float)=.0018 _Exposure("Exposure",Float)=.78 }
 SubShader {
  Tags {"Queue"="Geometry" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite On
  Pass {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_CloudTops);SAMPLER(sampler_CloudTops);
   CBUFFER_START(UnityPerMaterial)float _WorldScale,_Exposure;CBUFFER_END
   struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half fog:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target{
    float2 uv=i.w.xz*_WorldScale+float2(.17,.39);
    half3 c=SAMPLE_TEXTURE2D(_CloudTops,sampler_CloudTops,uv).rgb;
    half3 second=SAMPLE_TEXTURE2D(_CloudTops,sampler_CloudTops,float2(uv.y,-uv.x)*.43+float2(.39,.18)).rgb;
    c=lerp(c,second,.22)*_Exposure;
    return half4(MixFog(c,i.fog),1);
   }
   ENDHLSL
  }
 }
}
