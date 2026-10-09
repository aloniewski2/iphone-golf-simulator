Shader "GolfArcade/GolfRock" {
Properties {
 _HeightStrength("Stone colour relief",Range(0,2))=0
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
 _AuthoredUV("Use scanned mesh UVs",Float)=0
 _RockMipBias("Rock mip bias",Float)=1.5
 _PaletteMode("Quiet colour palette",Float)=0
 _LowColor("Low palette",Color)=(.25,.45,.19,1)
 _HighColor("High palette",Color)=(.46,.66,.29,1)
 _DetailContrast("Texture contrast",Float)=1
 _PaletteDetail("Retained stone fissure colour",Range(0,1))=0
 _PaletteMidpoint("Authored scan linear-luma midpoint",Float)=.22
 _PaletteLightShoulder("Authored scan exposed-plane shoulder",Range(0,1))=1
 [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull mode",Float)=2
 _Foliage("Two sided foliage lighting",Float)=0

}
SubShader {
Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
Cull [_Cull]
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
#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
#pragma multi_compile_fog
#define GOLF_ROCK 1

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../../Tennis/Shaders/HeroLighting.hlsl"
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
 return sin(phase*6.2831853)*fade;
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
float GolfStoneHash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float GolfStoneNoise(float3 p){
 float3 cell=floor(p),q=frac(p);q=q*q*(3-2*q);
 float a=lerp(GolfStoneHash(cell),GolfStoneHash(cell+float3(1,0,0)),q.x);
 float b=lerp(GolfStoneHash(cell+float3(0,1,0)),GolfStoneHash(cell+float3(1,1,0)),q.x);
 float c=lerp(GolfStoneHash(cell+float3(0,0,1)),GolfStoneHash(cell+float3(1,0,1)),q.x);
 float e=lerp(GolfStoneHash(cell+float3(0,1,1)),GolfStoneHash(cell+1),q.x);
 return lerp(lerp(a,b,q.y),lerp(c,e,q.y),q.z);
}
half4 GolfFragment(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target {
 UNITY_SETUP_INSTANCE_ID(i);
 float3 view=GetWorldSpaceViewDir(i.w);
 half3 n=normalize(i.n),v=SafeNormalize(view);
 if(_Foliage>.5h) n*=IS_FRONT_VFACE(face,1,-1);
 half4 base;
 #if defined(GOLF_ROCK)
  // Metric world coordinates: cliff UV aspect never stretches a stratum.
  // A broad rock tile mip suppresses subpixel basalt detail while the world
  // strata and column rhythm keep their independently filtered read.
  half3 weight=abs(n);weight*=weight;weight/=max(dot(weight,half3(1,1,1)),.001h);
  float3 p=i.w*_RockScale;
  // Mirrored coordinates meet at the same image edge. The fissured limestone
  // scan is not perfectly periodic; ordinary repeat exposed rectangular seams.
  p=1-abs(frac(p*.5)*2-1);
  if(_AuthoredUV>.5h) {
   base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
   if(_NormalEnabled>.5h) {
    half3 ts=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv));
    ts.xy*=_BumpScale;half3 t=normalize(i.t.xyz),b=cross(n,t)*i.t.w;
    n=SafeNormalize(t*ts.x+b*ts.y+n*ts.z);
   }
  } else {
  base=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,p.zy,_RockMipBias)*weight.x;
  base+=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,p.xz,_RockMipBias)*weight.y;
  base+=SAMPLE_TEXTURE2D_BIAS(_BaseMap,sampler_BaseMap,p.xy,_RockMipBias)*weight.z;
  if(_TriplanarNormals>.5h && _NormalEnabled>.5h) {
   half2 nx=UnpackNormal(SAMPLE_TEXTURE2D_BIAS(_BumpMap,sampler_BumpMap,p.zy,_RockMipBias)).xy;
   half2 ny=UnpackNormal(SAMPLE_TEXTURE2D_BIAS(_BumpMap,sampler_BumpMap,p.xz,_RockMipBias)).xy;
   half2 nz=UnpackNormal(SAMPLE_TEXTURE2D_BIAS(_BumpMap,sampler_BumpMap,p.xy,_RockMipBias)).xy;
   half3 detail=half3(0,nx.y,nx.x)*weight.x+half3(ny.x,0,ny.y)*weight.y+half3(nz.x,nz.y,0)*weight.z;
   n=SafeNormalize(n+detail*_BumpScale*.35h);
  }
  }
  if(_HeightStrength>.001h) {
   // Derive consistent surface relief from the actual fissured colour scan.
   // Screen-space gradients add tactile stone response without extra map reads.
   float height=dot(base.rgb,float3(.2126,.7152,.0722));
   float3 dx=ddx(i.w),dy=ddy(i.w),r1=cross(dy,n),r2=cross(n,dx);
   float determinant=dot(dx,r1);
   float3 gradient=(r1*ddx(height)+r2*ddy(height))*sign(determinant)/max(abs(determinant),.00001);
   n=SafeNormalize(n-gradient*_HeightStrength);
  }
  if(_PaletteMode>.5h) {
   half value=dot(base.rgb,half3(.2126h,.7152h,.0722h));
   half midpoint=_AuthoredUV>.5h ? _PaletteMidpoint : .22h;
   half blend=.5h+(value-midpoint)*_DetailContrast;
   if(_AuthoredUV>.5h && _PaletteLightShoulder<.999h) {
    // A material-local shoulder holds the scan median and darker fissures.
    // Bright exposed albedo retains a gradient instead of clipping to cream.
    half above=max(blend-.5h,0.0h),span=max(_PaletteLightShoulder,.001h);
    blend=min(blend,.5h)+span*above/(span+above);
   }
   half3 palette=lerp(_LowColor.rgb,_HighColor.rgb,saturate(blend));
   // Retain broad authored fissures at a bounded strength. Full palette mapping
   // made actual rounded limestone chalk-white; full scan reads as wallpaper.
   base.rgb=lerp(palette,base.rgb*.75h,_PaletteDetail);
  }
  if(_GeologicalMacro>.001h){
   float broad=GolfStoneNoise(i.w*.020+float3(7,23,11))*.63+GolfStoneNoise(i.w*.006+float3(31,5,17))*.37;
   base.rgb*=lerp(1,lerp(.54h,1.48h,saturate((broad-.20)*1.65)),_GeologicalMacro);
   half elevation=saturate(i.w.y*.007h);
   base.rgb*=lerp(half3(.85h,.87h,.91h),half3(1.08h,1.06h,1.02h),elevation*_GeologicalMacro);
  }
  half strata=GolfBand(i.w.y*.22+sin(i.w.x*.025)*.10);
  base.rgb*=1+strata*_StrataStrength;
  half cap=smoothstep(.65h,.94h,n.y)*_Cap;
  base.rgb=lerp(base.rgb,base.rgb*half3(.70h,1.10h,.55h),cap);
  // Basalt is a broad static column rhythm, filtered before a phone can alias it.
  half columns=GolfBand(i.w.x*.16+i.w.z*.12);
  base.rgb*=1+columns*_Basalt*.07h;
 #else
  base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
  if (_NormalEnabled>.5h) {
   half3 ts=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv));
   half fade=saturate(1-dot(view,view)*.000025h);
   ts.xy*=_BumpScale*fade;
   half3 t=i.t.xyz,b=cross(n,t)*i.t.w;
   n=SafeNormalize(t*ts.x+b*ts.y+n*ts.z);
  }
  half4 edges=SAMPLE_TEXTURE2D(_GolfEdgeMap,sampler_GolfEdgeMap,(i.w.xz-_GolfEdgeBounds.xy)*_GolfEdgeBounds.zw);
  #if defined(_GOLF_SAND)
   base.rgb*=edges.b*2;
   half ripple=GolfBand(dot(i.w.xz,float2(.8,.6))*1.4);
   n=SafeNormalize(n+half3(.025h*ripple,0,.018h*ripple));
  #else
   half phase=dot(i.w.xz,_StripeDirection.xz)/max(_StripeWidth,.5h);
   half bands=GolfBand(phase)*_Bands;
   if (_Surface>1.5h&&_Surface<2.5h) bands+=GolfBand(dot(i.w.xz,_StripeDirection.zx*float2(-1,1))/max(_StripeWidth,.5h))*.018h;
   base.rgb*=1+bands;
   base.rgb*=(_Surface>1.5h&&_Surface<3.5h)?edges.r:edges.g;
  #endif
 #endif
 half3 albedo=base.rgb*_BaseColor.rgb;
 half gloss=_Smoothness*lerp(1,base.a,_SheenFromAlpha);
 half wetFoot=_WetFoot*(1-smoothstep(0.0,4.5,i.w.y));
 albedo*=lerp(1.0h,.64h,wetFoot);
 albedo*=lerp(half3(1,1,1),half3(.86h,.92h,1),wetFoot*.45h);
 gloss=saturate(gloss+wetFoot*.28h);
 Light main=HeroMain(GetMainLight());
 main.shadowAttenuation=GolfShadowAttenuation(i.w);
 half3 colour=albedo*(i.illumination-i.key+HeroWrapped(n,main.direction,_Wrap)*main.color*main.shadowAttenuation);
 colour+=albedo*(HeroAmbient(n)-HeroAmbient(normalize(i.n)));
 #if defined(_SCREEN_SPACE_OCCLUSION)
 if(_AuthoredUV>.5h) {
  // The renderer publishes pre-opaque AO. Consume only its indirect visibility
  // on authored scans: sunlight, specular and legacy rocks remain unchanged.
  AmbientOcclusionFactor screenAO=GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.p));
  half indirectVisibility=lerp(1.0h,screenAO.indirectAmbientOcclusion,.65h);
  colour+=albedo*HeroAmbient(n)*(indirectVisibility-1.0h);
 }
 #endif
 if(_Foliage>.5h) {
  // A little transmitted light keeps the underside of a frond readable.
  colour+=albedo*main.color*saturate(-dot(n,main.direction))*.12h*main.shadowAttenuation;
 }
 half3 h=SafeNormalize(main.direction+v);
 half spec=pow(saturate(dot(n,h)),lerp(24.0h,90.0h,saturate(gloss)));
 colour+=main.color*main.shadowAttenuation*spec*(.008h+gloss*.025h);
 half edge=1-saturate(dot(n,v));edge*=edge;edge*=edge;
 colour+=albedo*edge*gloss*.10h*main.color*main.shadowAttenuation;
 #if defined(GOLF_ROCK)
 if(_EmissionEnabled>.5h)colour+=base.aaa*_EmissionColor.rgb*saturate(1-length(view)/450.0);
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
half4 GolfNormals(V i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target {
 half3 n=normalize(i.n);if(_Foliage>.5h)n*=IS_FRONT_VFACE(face,1,-1);
 return half4(n,0);
}
ENDHLSL
}
}
FallBack Off
}
