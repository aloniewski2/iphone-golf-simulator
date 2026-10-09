Shader "GolfArcade/GolfBasaltColumns" {
Properties { _BaseColor("Basalt",Color)=(.115,.13,.15,1) [HDR] _Heat("Fissure glow",Color)=(4.8,.55,.035,1) }
SubShader {
 Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
 Pass {
  Tags {"LightMode"="UniversalForward"}
  HLSLPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma multi_compile_fog
  #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
  #pragma multi_compile_fragment _ _SHADOWS_SOFT
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
  half4 _BaseColor,_Heat;
  CBUFFER_END
  struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
  struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float2 uv:TEXCOORD2;};
  V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;return o;}
  half4 frag(V i):SV_Target {
   half3 n=normalize(i.n);Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
   half noise=sin(i.w.y*4.1+sin(i.w.x*1.7)+i.w.z*.9)*.5+.5;
   half3 colour=_BaseColor.rgb*(.8+noise*.3)*(SampleSH(n)+sun.color*saturate(dot(n,sun.direction)) *sun.shadowAttenuation);
   float edge=min(i.uv.x,1-i.uv.x),aa=max(fwidth(edge),.002);
   half seam=1-smoothstep(.012-aa,.012+aa,edge);
   half hot=smoothstep(.10,.9,sin(i.w.x*.11+i.w.z*.08+i.w.y*.23)*.5+.5);
   colour+=_Heat.rgb*seam*hot*(1-smoothstep(.65,1,i.uv.y));
   return half4(MixFog(colour,InitializeInputDataFog(float4(i.w,1),0)),1);
  }
  ENDHLSL
 }
}
}
