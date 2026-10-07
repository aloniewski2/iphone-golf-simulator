#ifndef GOLF_SURFACE_INCLUDED
#define GOLF_SURFACE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseColor,_BaseMap_ST,_EmissionColor,_StripeDirection;
float4 _MowingBounds;
float _WorldUV,_TileYards,_FollowCourse,_RockScale,_StrataStrength,_RockMipBias,_TriplanarNormals;
half _Smoothness,_BumpScale,_Surface,_Bands,_StripeWidth,_SheenFromAlpha;
half _Wrap,_Rock,_Basalt,_Cap,_NormalEnabled,_EmissionEnabled;
half _Foliage;
CBUFFER_END
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);
TEXTURE2D(_GolfEdgeMap);SAMPLER(sampler_GolfEdgeMap);
TEXTURE2D(_MowingMap);SAMPLER(sampler_MowingMap);
float4 _GolfEdgeBounds;
struct A {float4 p:POSITION;float3 n:NORMAL;float4 t:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct V {float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 t:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;half3 illumination:TEXCOORD5;half3 key:TEXCOORD6;UNITY_VERTEX_INPUT_INSTANCE_ID};
V GolfVertex(A a) {
 V o=(V)0;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);
 VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);VertexNormalInputs n=GetVertexNormalInputs(a.n,a.t);
 o.p=p.positionCS;o.w=p.positionWS;o.n=n.normalWS;o.t=half4(n.tangentWS,a.t.w*GetOddNegativeScale());
 o.uv=a.uv*_BaseMap_ST.xy+_BaseMap_ST.zw;o.fog=ComputeFogFactor(p.positionCS.z);return o;
}
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
float3 _LightDirection;
float4 GolfShadow(A a):SV_POSITION {
 UNITY_SETUP_INSTANCE_ID(a);
 float3 p=TransformObjectToWorld(a.p.xyz),n=TransformObjectToWorldNormal(a.n);
 float4 clip=TransformWorldToHClip(ApplyShadowBias(p,n,_LightDirection));
 #if UNITY_REVERSED_Z
 clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
 #else
 clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE*clip.w);
 #endif
 return clip;
}
float4 GolfDepth(A a):SV_POSITION {UNITY_SETUP_INSTANCE_ID(a);return TransformObjectToHClip(a.p.xyz);}
half4 GolfZero():SV_Target {return 0;}
#endif
