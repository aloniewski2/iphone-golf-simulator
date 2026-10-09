// Golf ocean: irregular metric ripples, depth-aware turquoise shelves and filtered sun highlights.
Shader "GolfArcade/GolfOcean"
{
 Properties {
  _Shallow("Shallow turquoise",Color)=(.06,.65,.68,1)
  _Deep("Deep ocean",Color)=(.025,.28,.47,1)
  _Sky("Sky reflection",Color)=(.38,.66,.88,1)
  _DeepDistance("Fallback depth distance",Float)=100
  _WaveScale("Wave scale",Float)=.35
  _Sparkle("Sun sparkle",Float)=1.5
  _WaterOrigin("Course origin",Vector)=(0,0,0,0)
  _DepthShore("Depth shore enabled",Float)=1
  _ResortPop("Bright coastal water response",Range(0,1))=0
 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10"}
  Pass {
   Name "Ocean" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
   CBUFFER_START(UnityPerMaterial)
    half4 _Shallow,_Deep,_Sky;float4 _WaterOrigin;
    float _DeepDistance,_WaveScale,_Sparkle,_DepthShore,_ResortPop;
   CBUFFER_END
   struct A {float4 p:POSITION;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;};
   V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 cell=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);}
   float2 wave(float2 p,float2 direction,float frequency,float speed) {
    float phase=dot(p,direction)*frequency+_Time.y*speed;
    // Broad modulated phases avoid a regular specular checkerboard.
    phase+=(noise(p*.43+_Time.y*.025)-.5)*8.0;
    float filtering=saturate(1-length(fwidth(p*frequency))*.42);
    return direction*cos(phase)*frequency*filtering;
   }
   half4 frag(V i):SV_Target {
    float2 p=i.w.xz*_WaveScale;
    float2 slope=wave(p,normalize(float2(1,.31)),1.10,.62)*.085
     +wave(p,normalize(float2(-.43,1)),1.73,.83)*.052
     +wave(p,normalize(float2(.77,-.83)),3.12,1.17)*.022;
    slope+=float2(noise(p*1.23+17),noise(p*1.41+51))*.05-.025;
    half3 n=normalize(half3(-slope.x,1,-slope.y));
    half3 v=SafeNormalize(GetWorldSpaceViewDir(i.w));
    float distanceToCourse=length(i.w.xz-_WaterOrigin.xz);
    float deep=saturate((distanceToCourse-25)/max(_DeepDistance,1));
    // A disabled/unavailable depth texture leaves the deterministic distance fallback.
    float raw=SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy);
    float sceneEye=LinearEyeDepth(raw,_ZBufferParams);
    float waterEye=-TransformWorldToView(i.w).z;
    float thickness=max(0,sceneEye-waterEye);
    float valid=step(.001,thickness)*step(sceneEye,_ProjectionParams.z*.995)*_DepthShore;
    deep=lerp(deep,saturate(thickness*.16),valid);
    half3 water=lerp(_Shallow.rgb,_Deep.rgb,deep);
    Light sun=GetMainLight();half3 h=SafeNormalize(sun.direction+v);
    half fresnel=pow(1-saturate(dot(n,v)),4);
    half spec=pow(saturate(dot(n,h)),180)*(0.20+0.80*noise(p*2.1));
    half3 colour=water*(SampleSH(half3(0,1,0))*.55+half3(.48,.48,.48));
    colour=lerp(colour,_Sky.rgb,fresnel*.30);
    colour+=sun.color*spec*min(_Sparkle,2.0)*.85;
    // Filtered moving crests catch the bright sky outside the small sun glint.
    // They retain the deep/shallow palette and fade below a pixel at the horizon.
    float crestPhase=dot(p,float2(4.7,1.8))+noise(p*.31)*8-_Time.y*1.3;
    half crestFilter=saturate(1-fwidth(crestPhase)*.32);
    half crest=pow(saturate(sin(crestPhase)*.5+.5),18)*smoothstep(.58,.84,noise(p*.9+_Time.y*.03))*crestFilter;
    half broadRipple=sin(dot(p,float2(1.3,-.62))+noise(p*.18)*7-_Time.y*.5)*.5+.5;
    colour*=lerp(1,.82h+.34h*broadRipple,_ResortPop);
    colour=lerp(colour,half3(.63h,.86h,.94h),crest*.42h*_ResortPop);
    half foam=(1-smoothstep(.10,.65,thickness))*valid;
    foam*=.55+.25*sin(dot(i.w.xz,float2(1.21,.73))-_Time.y*.6);
    colour=lerp(colour,half3(.69,.89,.87),foam*.38);
    half fog=InitializeInputDataFog(float4(i.w,1),0);
    return half4(MixFog(colour,fog),1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
