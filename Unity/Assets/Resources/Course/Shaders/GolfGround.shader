Shader "GolfArcade/GolfGround" {
Properties {
 _GeologicalMacro("Broad geological weathering",Range(0,1))=0
 _WetFoot("Wet lower coastal cliff",Range(0,1))=0

 [MainTexture] _BaseMap("Albedo / band sheen",2D)="white"{}
 [MainColor] _BaseColor("Tint",Color)=(1,1,1,1)
 _BumpMap("Blade / ripple normal",2D)="bump"{}
 _EmissionMap("Rock fissures",2D)="black"{}
 _EmissionColor("Emission",Color)=(0,0,0,1)
 _Smoothness("Sheen",Float)=.12
 _BumpScale("Normal strength",Float)=.6
 _Surface("0 rough 1 fairway 2 green 3 tee 4 fringe 5 sand",Float)=0
 _Bands("Band amplitude",Float)=.05
 _StripeWidth("Band period (yards)",Float)=5
 _StripeDirection("Mowing direction",Vector)=(1,0,0,0)
 _SheenFromAlpha("Albedo alpha sheen",Float)=0
 _Wrap("Shared wrap",Float)=.5
 _Rock("Triplanar rock",Float)=0
 _Basalt("Column rhythm",Float)=0
 _Cap("Grass cap",Float)=0
 _NormalEnabled("Normal map present",Float)=0
 _EmissionEnabled("Emission map present",Float)=0
 _WorldUV("World mapped legacy surface",Float)=0
 _TileYards("Texture repeat in yards",Float)=10.936
 _FollowCourse("Follow the fairway centerline",Float)=0
 _MowingMap("Course coordinates",2D)="black"{}
 _MowingBounds("Course coordinate bounds",Vector)=(0,0,1,1)
 _RockScale("Rock repeats per yard",Float)=.09
 _StrataStrength("Rock strata strength",Float)=.075
 _TriplanarNormals("Triplanar normal detail",Float)=0
 _RockMipBias("Rock mip bias",Float)=1.5
 _PaletteMode("Quiet colour palette",Float)=0
 _LowColor("Low palette",Color)=(.25,.45,.19,1)
 _HighColor("High palette",Color)=(.46,.66,.29,1)
 _DetailContrast("Texture contrast",Float)=1
 _TurfManaged("Managed turf hierarchy",Float)=0
 _PaletteDetail("Calibrated dense turf map",Range(0,1))=0
 _TurfMidpoint("Measured turf atlas luminance",Float)=.103

}
SubShader {
Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
HLSLINCLUDE
#include "GolfSurface.hlsl"
ENDHLSL
Pass { Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma target 3.0
#pragma vertex GolfLitVertex
#pragma fragment GolfFragment
#pragma multi_compile_instancing
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fog
#pragma multi_compile_local_fragment _ _GOLF_SAND

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../../Tennis/Shaders/HeroLighting.hlsl"
#include "GolfTurfField.hlsl"
half GolfShadowAttenuation(float3 world)
            {
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                return MainLightRealtimeShadow(ComputeScreenPos(TransformWorldToHClip(world)));
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 sc = TransformWorldToShadowCoord(world);
                ShadowSamplingData data = GetMainLightShadowSamplingData();
                half attenuation = SampleShadowmapFilteredLowQuality(TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture,sampler_LinearClampCompare),sc,data);
                attenuation=LerpWhiteTo(attenuation,GetMainLightShadowParams().x);
                if (BEYOND_SHADOW_FAR(sc)) attenuation=1;
                return lerp(attenuation, 1, GetMainLightShadowFade(world));
                #else
                return 1;
                #endif
            }

half GolfBand(float phase) {
 // Integrate away bands smaller than one pixel. No frame-dependent inputs.
 half fade=saturate(1-fwidth(phase)*2);
 return (smoothstep(-.14h,.14h,sin(phase*6.2831853))*2-1)*fade;
}
V GolfLitVertex(A a) {
 V o=GolfVertex(a);
 Light main=HeroMain(GetMainLight());
 o.key=HeroWrapped(o.n,main.direction,_Wrap)*main.color;
 o.illumination=o.key+HeroAmbient(o.n);
 #if defined(_ADDITIONAL_LIGHTS)
 uint count=GetAdditionalLightsCount();
 for(uint li=0;li<count;li++) {
  Light l=GetAdditionalLight(li,o.w);
  o.illumination+=HeroWrapped(o.n,l.direction,_Wrap)*l.color*l.distanceAttenuation*l.shadowAttenuation;
 }
 #endif
 return o;
}
half4 GolfFragment(V i):SV_Target {
 UNITY_SETUP_INSTANCE_ID(i);
 float3 view=GetWorldSpaceViewDir(i.w);
 half3 n=normalize(i.n),v=SafeNormalize(view);
 half4 base;
 #if defined(GOLF_ROCK)
  // Metric world coordinates: cliff UV aspect never stretches a stratum.
  half3 weight=abs(n);weight*=weight;weight/=max(dot(weight,half3(1,1,1)),.001h);
  float3 p=i.w*.09;
  base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.zy)*weight.x;
  base+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz)*weight.y;
  base+=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy)*weight.z;
  half strata=GolfBand(i.w.y*.22+sin(i.w.x*.025)*.10);
  base.rgb*=1+strata*.075h;
  half cap=smoothstep(.65h,.94h,n.y)*_Cap;
  base.rgb=lerp(base.rgb,base.rgb*half3(.70h,1.10h,.55h),cap);
  // Basalt is a broad static column rhythm, filtered before a phone can alias it.
  half columns=GolfBand(i.w.x*.16+i.w.z*.12);
  base.rgb*=1+columns*_Basalt*.07h;
 #else
  float2 groundUV=i.uv;
  half3 surfaceT=i.t.xyz,surfaceB=cross(n,surfaceT)*i.t.w;
  if(_WorldUV>.5h) {
   groundUV=i.w.xz/max(_TileYards,.5);
   surfaceT=SafeNormalize(half3(1,0,0)-n*n.x);surfaceB=cross(surfaceT,n);
  }
  base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,groundUV);
  if(_PaletteMode>.5h) {
   // Preserve authored blade detail with a controlled green hue and quiet contrast.
   half value=dot(base.rgb,half3(.2126h,.7152h,.0722h));
   // The cut-turf atlas has measured linear mean .1029, not the generic
   // .25 midpoint. Its local material enables calibration; other courses
   // retain their existing mapping with the default zero value.
   half midpoint=lerp(.25h,max(_TurfMidpoint,.001h),saturate(_PaletteDetail));
   half blend=saturate(.5h+(value-midpoint)*_DetailContrast);
   if(_TurfManaged>.5h&&_Surface<.5h) blend=lerp(blend,GolfTurfGrowth(i.w.xz),.65h);
   base.rgb=lerp(_LowColor.rgb,_HighColor.rgb,blend);
   half macro=sin(i.w.x*.047h+sin(i.w.z*.028h))*sin(i.w.z*.039h);
   if(_TurfManaged>.5h) {
    half amount=_Surface<.5h?1:_Surface<1.5h?.35h:_Surface<2.5h?.30h:_Surface<3.5h?.12h:.50h;
    base.rgb*=lerp(1,GolfTurfBroad(i.w.xz),amount);
   } else base.rgb*= _PaletteDetail>.5h ? GolfTurfBroad(i.w.xz) : 1+macro*.025h;
  }
  // Keep texel density on steep legacy fairway banks. Course coordinates drive
  // only broad mowing bands; microscopic blades must never collapse at a bend.
  if(_WorldUV>.5h && abs(n.y)<.75h) {
   half side=1-smoothstep(.35h,.75h,abs(n.y));
   float2 sideUV=(abs(n.x)>abs(n.z)?i.w.zy:i.w.xy)/max(_TileYards,.5);
   half4 sideBase=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,sideUV);
   if(_PaletteMode>.5h) sideBase.rgb=lerp(_LowColor.rgb,_HighColor.rgb,saturate(.5h+(dot(sideBase.rgb,half3(.2126h,.7152h,.0722h))-.25h)*_DetailContrast));
   base=lerp(base,sideBase,side);
   if(side>.5h) {
    groundUV=sideUV;
    half3 axis=abs(n.x)>abs(n.z)?half3(0,0,1):half3(1,0,0);
    surfaceT=SafeNormalize(axis-n*dot(n,axis));surfaceB=cross(n,surfaceT);
   }
  }
  if (_NormalEnabled>.5h) {
   half3 ts=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,groundUV));
   half fade=saturate(1-dot(view,view)*.000025h);
   ts.xy*=_BumpScale*fade;
   n=SafeNormalize(surfaceT*ts.x+surfaceB*ts.y+n*ts.z);
  }
  half4 edges=SAMPLE_TEXTURE2D(_GolfEdgeMap,sampler_GolfEdgeMap,(i.w.xz-_GolfEdgeBounds.xy)*_GolfEdgeBounds.zw);
  #if defined(_GOLF_SAND)
   base.rgb*=edges.b*2;
   half ripple=GolfBand(dot(i.w.xz,float2(.8,.6))*1.4);
   n=SafeNormalize(n+half3(.025h*ripple,0,.018h*ripple));
  #else
   half phase=dot(i.w.xz,_StripeDirection.xz)/max(_StripeWidth,.5h);
   if(_WorldUV>.5h && _FollowCourse>.5h) {
    float4 course=SAMPLE_TEXTURE2D(_MowingMap,sampler_MowingMap,(i.w.xz-_MowingBounds.xy)*_MowingBounds.zw);
    phase=course.x/max(_StripeWidth,.5h);
   }
   half bands=GolfBand(phase)*_Bands;
   if (_Surface>1.5h&&_Surface<2.5h) bands+=GolfBand(dot(i.w.xz,_StripeDirection.zx*float2(-1,1))/max(_StripeWidth,.5h))*_Bands*.35h;
   base.rgb*=1+bands;
   base.rgb*=(_Surface>1.5h&&_Surface<3.5h)?edges.r:edges.g;
  #endif
 #endif
 half3 albedo=base.rgb*_BaseColor.rgb;
 half gloss=_Smoothness*lerp(1,base.a,_SheenFromAlpha);
 Light main=HeroMain(GetMainLight());
 main.shadowAttenuation=GolfShadowAttenuation(i.w);
 half3 colour=albedo*(i.illumination-i.key+HeroWrapped(n,main.direction,_Wrap)*main.color*main.shadowAttenuation);
 half3 h=SafeNormalize(main.direction+v);
 half spec=pow(saturate(dot(n,h)),lerp(24.0h,90.0h,saturate(gloss)));
 colour+=main.color*main.shadowAttenuation*spec*(.008h+gloss*.025h);
 half edge=1-saturate(dot(n,v));edge*=edge;edge*=edge;
 colour+=albedo*edge*gloss*.10h*main.color*main.shadowAttenuation;
 #if defined(GOLF_ROCK)
 if(_EmissionEnabled>.5h)colour+=base.aaa*_EmissionColor.rgb;
 #endif
 return half4(MixFog(colour,i.fog),1);
}

ENDHLSL
}
Pass {Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
HLSLPROGRAM
#pragma target 3.0
#pragma vertex GolfShadow
#pragma fragment GolfZero
#pragma multi_compile_instancing
ENDHLSL
}
Pass {Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
HLSLPROGRAM
#pragma target 3.0
#pragma vertex GolfDepth
#pragma fragment GolfZero
#pragma multi_compile_instancing
ENDHLSL
}
Pass {Name "DepthNormals" Tags {"LightMode"="DepthNormals"} ZWrite On
HLSLPROGRAM
#pragma target 3.0
#pragma vertex GolfVertex
#pragma fragment GolfNormals
#pragma multi_compile_instancing
half4 GolfNormals(V i):SV_Target {return half4(normalize(i.n),0);}
ENDHLSL
}
}
FallBack Off
}
