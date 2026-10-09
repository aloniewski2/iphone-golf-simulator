Shader "GolfArcade/GolfCoastalTurf"
{
 Properties {
  _RootColor("Dense cut grass root",Color)=(.15,.34,.065,1)
  _TipColor("Fresh cut blade",Color)=(.36,.55,.12,1)
  _Smoothness("Blade sheen",Range(0,1))=.12
  _StripeDirection("Mowing direction",Vector)=(0,0,1,0)
  _StripeWidth("Mowing band yards",Float)=3.28
  _Bands("Cut turf mowing amplitude",Float)=.075
  _FairwayRootColor("Fairway roots",Color)=(.245,.395,.215,1)
  _FairwayTipColor("Fairway tips",Color)=(.370,.505,.290,1)
  _FringeRootColor("Collar roots",Color)=(.165,.345,.205,1)
  _FringeTipColor("Collar tips",Color)=(.255,.445,.260,1)
  _RoughRootColor("Unmown rough roots",Color)=(.170,.315,.180,1)
  _RoughTipColor("Warm rough tips",Color)=(.370,.455,.245,1)
  _PatchCenter("Moving patch centre and enabled",Vector)=(0,0,0,0)
  _PatchFade("Moving patch fade yards",Vector)=(9,12,0,0)
  _TurfSurface("Instanced0 rough1 fairway3 tee4 fringe",Float)=3
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Cull Off
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "../../Tennis/Shaders/HeroLighting.hlsl"
   #include "GolfTurfField.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _RootColor,_TipColor,_FairwayRootColor,_FairwayTipColor,_FringeRootColor,_FringeTipColor,_RoughRootColor,_RoughTipColor;float4 _StripeDirection,_PatchCenter,_PatchFade;half _Smoothness,_StripeWidth,_Bands;
   CBUFFER_END
   UNITY_INSTANCING_BUFFER_START(TurfTypes)
    UNITY_DEFINE_INSTANCED_PROP(float,_TurfSurface)
   UNITY_INSTANCING_BUFFER_END(TurfTypes)
   float4 _GolfWind;
   struct A {float4 p:POSITION;float3 n:NORMAL;float2 role:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 role:TEXCOORD2;half fog:TEXCOORD3;UNITY_VERTEX_INPUT_INSTANCE_ID};
   V vert(A a) {
    V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
    o.w=TransformObjectToWorld(a.p.xyz);
    // A few millimeters of motion keep short groomed grass quiet in wind.
    o.w.xz+=_GolfWind.xz*a.role.y*.010*(.5+.5*sin(_GolfWind.w*1.9+dot(o.w.xz,float2(.73,.51))));
    o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.role=half4(a.role,UNITY_ACCESS_INSTANCED_PROP(TurfTypes,_TurfSurface),lerp(1,1-smoothstep(_PatchFade.x,_PatchFade.y,distance(o.w.xz,_PatchCenter.xz)),_PatchCenter.w));o.fog=ComputeFogFactor(o.p.z);return o;
   }
   half4 frag(V i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target {
    UNITY_SETUP_INSTANCE_ID(i);
    // Stable per-blade rank thins the boundary into the matching ground texture.
    // UV.x is constant on each authored blade; no screen-space stipple or time noise.
    clip(i.role.w-frac(i.role.x*53.179h));
    half3 normal=normalize(i.n)*IS_FRONT_VFACE(face,1,-1);
    Light sun=HeroMain(GetMainLight());
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    sun.shadowAttenuation=MainLightRealtimeShadow(ComputeScreenPos(TransformWorldToHClip(i.w)));
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    sun.shadowAttenuation=MainLightRealtimeShadow(TransformWorldToShadowCoord(i.w));
    #endif
    half3 rootColour=_RootColor.rgb,tipColour=_TipColor.rgb;
    if(i.role.z<.5h) {rootColour=_RoughRootColor.rgb;tipColour=_RoughTipColor.rgb;}
    else if(i.role.z<1.5h) {rootColour=_FairwayRootColor.rgb;tipColour=_FairwayTipColor.rgb;}
    else if(i.role.z>3.5h) {rootColour=_FringeRootColor.rgb;tipColour=_FringeTipColor.rgb;}
    half rough=1-step(.5h,i.role.z);
    half growth=GolfTurfGrowth(i.w.xz);
    half tip=saturate(i.role.y+rough*(growth-.5h)*.30h);
    half3 albedo=lerp(rootColour,tipColour,tip)*(.82h+.18h*i.role.x);
    half rooted=lerp(.72h,1.0h,saturate(i.role.y*2));
    half phase=dot(i.w.xz,_StripeDirection.xz)/max(_StripeWidth,.5h);
    half band=sin(phase*6.2831853)*saturate(1-fwidth(phase)*2)*_Bands*(i.role.z>.5h&&i.role.z<1.5h?1:i.role.z>2.5h&&i.role.z<3.5h?.22h:0);
    albedo*=lerp(1,GolfTurfBroad(i.w.xz),rough>.5h?1:.30h)*(1+band);
    // Thin grass transmits light from both faces. The previous one-sided
    // blade response made the same groomed field look like black stubble.
    half wrapped=saturate((abs(dot(normal,sun.direction))+.35h)/1.35h);
    // A dense short canopy scatters light between neighbouring blades. Keep
    // their directional detail while avoiding isolated dark, needle-like faces.
    half canopy=saturate((abs(sun.direction.y)+.35h)/1.35h);
    wrapped=lerp(wrapped,canopy,.65h);
    half3 colour=albedo*(HeroAmbient(half3(0,1,0))+sun.color*wrapped*sun.shadowAttenuation)*rooted;
    // Restrained back-light transmission, no plastic-white highlight.
    colour+=albedo*sun.color*saturate(-dot(normal,sun.direction))*.14h*sun.shadowAttenuation;
    half3 halfVector=SafeNormalize(sun.direction+SafeNormalize(GetWorldSpaceViewDir(i.w)));
    colour+=sun.color*pow(saturate(dot(normal,halfVector)),24.0h)*_Smoothness*.025h*sun.shadowAttenuation;
    return half4(MixFog(colour,i.fog),1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
