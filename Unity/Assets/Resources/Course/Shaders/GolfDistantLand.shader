Shader "GolfArcade/GolfDistantLand"
{
 Properties { _BaseColor("Distant island colour",Color)=(.23,.40,.43,1) }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass {
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   half4 frag(V i):SV_Target {
    half shade=.84+.16*saturate(dot(normalize(i.n),normalize(half3(-.45,.75,.25))));
    half haze=saturate((distance(_WorldSpaceCameraPos,i.w)-450)/2400)*.65;
    half3 colour=lerp(_BaseColor.rgb*shade,half3(.30,.46,.58),haze);
    return half4(colour,1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
