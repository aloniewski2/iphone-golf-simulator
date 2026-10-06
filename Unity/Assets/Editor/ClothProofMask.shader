// Editor-only semantic mask; the production shader's trim map defines the measured regions.
Shader "Hidden/ClothProofMask" {
 Properties { _MaskMap("Mask",2D)="white"{} _UseMaps("Use maps",Float)=0 _ShowAO("AO data",Float)=0 _ClassColour("Class",Color)=(1,0,0,1) }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
 Pass {
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MaskMap);SAMPLER(sampler_MaskMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _ClassColour;float _UseMaps;float _ShowAO;
 CBUFFER_END
 struct A {float4 p:POSITION;float2 uv:TEXCOORD0;};
 struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
 V vert(A i) {V o;o.p=TransformObjectToHClip(i.p.xyz);o.uv=i.uv;return o;}
 half4 frag(V i):SV_Target {
  half4 data=_UseMaps>.5?SAMPLE_TEXTURE2D(_MaskMap,sampler_MaskMap,i.uv):half4(1,1,0,0);
  if(_ShowAO>.5) return half4(_ClassColour.r>.5?1:_ClassColour.g>.5?.5:0,data.r,data.b,1);
  half trim=data.b;
  return trim>.5 && (_ClassColour.r>.5||_ClassColour.g>.5||_ClassColour.b>.5)?half4(0,0,1,1):_ClassColour;
 }
 ENDHLSL
 }
 }
}
