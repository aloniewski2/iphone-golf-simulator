Shader "GolfArcade/CoastalFoliage"
{
 Properties {
  [MainTexture] _BaseMap("Artist diffuse and needle coverage",2D)="white"{}
  [Normal] _BumpMap("Artist normal",2D)="bump"{}
  [MainColor] _BaseColor("Colour",Color)=(1,1,1,1)
  _Cutoff("Needle alpha cutoff",Range(0,1))=.30
  _NormalStrength("Relief",Range(0,1))=.45
  _Leaf("Needle transmission",Range(0,1))=1
  _Baked("Distant baked view",Range(0,1))=0
  _CanopyBrightness("Live canopy brightness",Range(1,3))=1.7
  _BakedBrightness("Baked exposure compensation",Range(1,8))=4.6
  _ShadeTransmission("Light through shaded needles",Range(0,.5))=.28
  _CanopyFill("Sky fill",Range(0,.3))=.14
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=0
 }
 SubShader {
  Tags {"RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest"}
  Cull [_Cull] ZWrite On AlphaToMask On
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
  CBUFFER_START(UnityPerMaterial)
   float4 _BaseMap_ST;half4 _BaseColor;half _Cutoff,_NormalStrength,_Leaf,_Baked,_CanopyBrightness,_BakedBrightness,_ShadeTransmission,_CanopyFill;
  CBUFFER_END
  float4 _GolfWind;
  struct A {float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float2 uv:TEXCOORD0;half4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 t:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;};
  float3 Sway(float3 w,half weight) {w.xz+=_GolfWind.xz*weight*(.35+.25*sin(_GolfWind.w*2.3+dot(w.xz,float2(.47,.31))));return w;}
  V vert(A a) {UNITY_SETUP_INSTANCE_ID(a);V o;o.w=Sway(TransformObjectToWorld(a.p.xyz),a.c.a*(1-_Baked));o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.t=half4(TransformObjectToWorldDir(a.t.xyz),a.t.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;}
  half4 Coverage(float2 uv) {half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv)*_BaseColor;clip(c.a-_Cutoff);return c;}
  half3 Normal(V i,half front) {half3 n=normalize(i.n);half3 t=normalize(i.t.xyz);half3 b=cross(n,t)*i.t.w;half3 tn=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_NormalStrength);return normalize(t*tn.x+b*tn.y+n*tn.z)*front;}
  ENDHLSL
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma multi_compile_instancing
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
   #pragma multi_compile_fog
   half4 frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {
    half4 c=Coverage(i.uv);half3 n=Normal(i,IS_FRONT_VFACE(front,1,-1));Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    half wrapped=saturate((dot(n,sun.direction)+.45h)/1.45h);
    // Native scans have very dark foliage reflectance and baked self-shadow.
    // Lift exposure per representation while preserving the source's variation.
    c.rgb*=lerp(1.0h,lerp(_CanopyBrightness,_BakedBrightness,_Baked),_Leaf);
    half leafShadow=lerp(sun.shadowAttenuation,_ShadeTransmission+(1-_ShadeTransmission)*sun.shadowAttenuation,_Leaf);
    half3 skyFill=half3(.88h,1.0h,.64h)*_CanopyFill*lerp(.65h,1.2h,saturate(n.y*.5h+.5h));
    half3 ambient=max(SampleSH(n),skyFill*_Leaf);
    AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(i.p.xy/_ScaledScreenParams.xy);
    half3 lit=ambient*ao.indirectAmbientOcclusion+sun.color*wrapped*leafShadow*.88h*ao.directAmbientOcclusion;
    lit+=sun.color*saturate(-dot(n,sun.direction))*.24h*_Leaf*leafShadow;
    half3 viewDir=SafeNormalize(GetWorldSpaceViewDir(i.w));
    half sunBehind=pow(saturate(dot(sun.direction,-viewDir)),2.0h);
    half rim=pow(1-saturate(dot(n,viewDir)),2.5h)*sunBehind;
    lit+=sun.color*rim*.16h*_Leaf*leafShadow;
    // Distant views already contain their own small branch/needle relief.
    lit=lerp(lit,half3(.89h,.92h,.85h)*lerp(.80h,1.0h,sun.shadowAttenuation),_Baked);
    half3 result=c.rgb*lit;
    return half4(MixFog(result,i.fog),c.a);
   }
   ENDHLSL
  }
  Pass {
   Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ColorMask 0 ZTest LEqual
   HLSLPROGRAM
   #pragma target 3.0
   #pragma multi_compile_instancing
   #pragma vertex shadow
   #pragma fragment mask
   float3 _LightDirection;
   V shadow(A a) {V o=vert(a);float3 w=Sway(TransformObjectToWorld(a.p.xyz),a.c.a*(1-_Baked));o.p=TransformWorldToHClip(ApplyShadowBias(w,TransformObjectToWorldNormal(a.n),_LightDirection));
    #if UNITY_REVERSED_Z
    o.p.z=min(o.p.z,UNITY_NEAR_CLIP_VALUE*o.p.w);
    #else
    o.p.z=max(o.p.z,UNITY_NEAR_CLIP_VALUE*o.p.w);
    #endif
    return o;}
   half4 mask(V i):SV_Target {Coverage(i.uv);return 0;}
   ENDHLSL
  }
  Pass {
   Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask R
   HLSLPROGRAM
   #pragma multi_compile_instancing
   #pragma vertex vert
   #pragma fragment depth
   half4 depth(V i):SV_Target {Coverage(i.uv);return 0;}
   ENDHLSL
  }
  Pass {
   Name "DepthNormals" Tags {"LightMode"="DepthNormals"}
   HLSLPROGRAM
   #pragma multi_compile_instancing
   #pragma vertex vert
   #pragma fragment normals
   half4 normals(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_Target {Coverage(i.uv);return half4(Normal(i,IS_FRONT_VFACE(front,1,-1)),0);}
   ENDHLSL
  }
 }
 Fallback Off
}
