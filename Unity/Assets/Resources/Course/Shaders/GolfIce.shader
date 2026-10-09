Shader "GolfArcade/GolfIce"
{
 Properties {
  _DeepColor("Deep glacier blue",Color)=(.11,.39,.48,1)
  _ShallowColor("Frosted glacier edge",Color)=(.42,.73,.77,1)
 }
 SubShader {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass {
   Name "GlacierIce" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial) half4 _DeepColor,_ShallowColor; CBUFFER_END
   struct A {float4 p:POSITION;float3 n:NORMAL;};
   struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);return o;}
   float2 IceHash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
   float IceNoise(float2 p){float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(IceHash(c).x,IceHash(c+float2(1,0)).x,f.x),lerp(IceHash(c+float2(0,1)).x,IceHash(c+1).x,f.x),f.y);}
   float IceJoint(float2 p){
    float2 c=floor(p),f=frac(p);float first=8,second=8;
    for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++){
     float2 offset=float2(x,y),delta=offset+IceHash(c+offset)-f;float gap=dot(delta,delta);
     if(gap<first){second=first;first=gap;}else second=min(second,gap);
    }
    return sqrt(second)-sqrt(first);
   }
   half4 frag(V i):SV_Target {
    float2 p=i.w.xz;float cloud=IceNoise(p*.035+float2(31,8));
    float2 warp=float2(IceNoise(p*.041+13),IceNoise(p*.041+51))*2.8;
    float joint=IceJoint((p+warp)/18);
    float aa=max(fwidth(joint),.0015);
    half crack=1-smoothstep(.006-aa,.017+aa,joint);
    half frost=smoothstep(.42,.84,cloud)*.55;
    half3 normal=normalize(i.n+half3((IceNoise(p*.47)-.5)*.032,0,(IceNoise(p*.47+19)-.5)*.032));
    half3 view=SafeNormalize(GetWorldSpaceViewDir(i.w));
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
    half3 albedo=lerp(_DeepColor.rgb,_ShallowColor.rgb,frost);
    albedo=lerp(albedo,half3(.51,.74,.78),crack*.62);
    half diffuse=.55+.45*saturate(dot(normal,sun.direction));
    half3 colour=albedo*(SampleSH(normal)+sun.color*diffuse*lerp(.65,1,sun.shadowAttenuation));
    half fresnel=pow(1-saturate(dot(normal,view)),4);
    colour=lerp(colour,half3(.26,.47,.62),fresnel*.42);
    half3 halfway=SafeNormalize(view+sun.direction);
    colour+=sun.color*pow(saturate(dot(normal,halfway)),150)*.50*sun.shadowAttenuation;
    colour+=half3(.02,.055,.065)*(1-frost);
    return half4(MixFog(colour,InitializeInputDataFog(float4(i.w,1),0)),1);
   }
   ENDHLSL
  }
 }
 Fallback Off
}
