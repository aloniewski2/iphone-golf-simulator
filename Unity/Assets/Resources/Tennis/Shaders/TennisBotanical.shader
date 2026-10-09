Shader "GolfArcade/TennisBotanical"
{
 Properties {
  [MainColor] _BaseColor("Botanical colour",Color)=(.25,.45,.18,1)
  _Gloss("Waxy sheen",Range(0,1))=.15
  _Leaf("Leaf translucency",Range(0,1))=1
  _ResortResponse("Resort leaf response",Range(0,1))=0
  _VertexPalette("Use vertex botanical palette",Float)=1
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Cull [_Cull]
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;half _Gloss,_Leaf,_VertexPalette,_ResortResponse;
  CBUFFER_END
  float4 _GolfWind;
  struct A {float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;float2 pivot:TEXCOORD2;};
  struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half weight:TEXCOORD2;half3 color:COLOR;};
  float3 Sway(float3 world,half weight,float2 pivot) {
   float phase=dot(pivot,float2(.47,.31));
   world.xz+=_GolfWind.xz*weight*(.55+.45*sin(_GolfWind.w*2.81+phase));return world;
  }
  V vert(A a) {V o;o.weight=lerp(a.c.r,a.c.a,_VertexPalette);o.w=Sway(TransformObjectToWorld(a.p.xyz),o.weight,a.pivot);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.color=lerp(half3(1,1,1),a.c.rgb,_VertexPalette);return o;}
  ENDHLSL
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   half4 frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {
    half3 n=normalize(i.n)*IS_FRONT_VFACE(front,1,-1);
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    half wrap=lerp(.60h,.32h,_ResortResponse);
    half wrapped=saturate((dot(n,sun.direction)+wrap)/(1+wrap));
    half3 ambient=SampleSH(n)+half3(.06h,.07h,.055h)*_Leaf;
    AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.p));
    ambient*=lerp(1,ao.indirectAmbientOcclusion,.30h*_ResortResponse);
    half rootShade=lerp(.78h,1.0h,saturate(i.weight*3));
    half3 albedo=_BaseColor.rgb*i.color;
    half3 c=albedo*(ambient+sun.color*wrapped*sun.shadowAttenuation*.75h)*rootShade;
    half back=saturate(-dot(n,sun.direction));
    half leaf=saturate(i.weight*4);
    half transmission=lerp(.22h,.30h*leaf,_ResortResponse);
    c+=albedo*sun.color*back*transmission*_Leaf*sun.shadowAttenuation;
    half3 h=SafeNormalize(sun.direction+SafeNormalize(GetWorldSpaceViewDir(i.w)));
    half sheen=lerp(.075h,.12h*leaf,_ResortResponse);
    c+=sun.color*pow(saturate(dot(n,h)),36.0h)*_Gloss*sheen*sun.shadowAttenuation;
    return half4(MixFog(c,InitializeInputDataFog(float4(i.w,1),0)),1);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex shadow
   #pragma fragment zero
   float3 _LightDirection;
   float4 shadow(A a):SV_POSITION {
    float3 w=Sway(TransformObjectToWorld(a.p.xyz),lerp(a.c.r,a.c.a,_VertexPalette),a.pivot),n=TransformObjectToWorldNormal(a.n);
    float4 p=TransformWorldToHClip(ApplyShadowBias(w,n,_LightDirection));
    #if UNITY_REVERSED_Z
    p.z=min(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
    #else
    p.z=max(p.z,UNITY_NEAR_CLIP_VALUE*p.w);
    #endif
    return p;
   }
   half4 zero():SV_Target{return 0;}
   ENDHLSL
  }
 }
 Fallback Off
}
