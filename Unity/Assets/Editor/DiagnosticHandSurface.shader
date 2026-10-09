Shader "GolfArcade/DiagnosticHandSurface" {
Properties {_BaseColor("Colour",Color)=(.72,.48,.30,1) _Cull("Cull",Float)=2 _Flat("Flat",Float)=0}
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass {Cull [_Cull] ZWrite On
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
half _Flat;
CBUFFER_END
struct A {float4 p:POSITION;float3 n:NORMAL;};
struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;};
V vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.n=TransformObjectToWorldNormal(i.n);return o;}
half4 frag(V i,FRONT_FACE_TYPE front:FRONT_FACE_SEMANTIC):SV_TARGET {if(_Flat>.5h)return _BaseColor;half3 n=normalize(i.n)*IS_FRONT_VFACE(front,1,-1);return half4(_BaseColor.rgb*(.35h+.65h*saturate(dot(n,normalize(float3(.35,.45,.75))))),1);}
ENDHLSL
}}
}
