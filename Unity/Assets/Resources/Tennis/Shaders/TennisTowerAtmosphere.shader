Shader "GolfArcade/TennisTowerAtmosphere"
{
 Properties {
  _BaseMap("Facade",2D)="white"{} _BaseColor("Facade tint",Color)=(1,1,1,1)
  _EmissionMap("Window lamps",2D)="black"{} _EmissionColor("Lamp light",Color)=(0,0,0,1)
  _Smoothness("Glass polish",Range(0,1))=.35 _Metallic("Metal",Range(0,1))=0
  _SkyPanorama("Reflected upper sky",2D)="white"{} _SkyRotation("Sky reflection heading",Float)=15
  _CloudTops("Cloud base atmosphere",2D)="white"{} _CloudScale("World cloud scale",Float)=.0018
 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);TEXTURE2D(_CloudTops);SAMPLER(sampler_CloudTops);TEXTURE2D(_SkyPanorama);SAMPLER(sampler_SkyPanorama);
   CBUFFER_START(UnityPerMaterial)float4 _BaseMap_ST;half4 _BaseColor,_EmissionColor;half _Smoothness,_Metallic;float _CloudScale,_SkyRotation;CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;float2 uv:TEXCOORD2;half fog:TEXCOORD3;};
   V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target{
    InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=normalize(i.n);d.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(i.w));d.bakedGI=SampleSH(d.normalWS);d.shadowMask=half4(1,1,1,1);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
    half4 facade=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
    half glass=facade.a;
    SurfaceData s=(SurfaceData)0;s.albedo=facade.rgb*_BaseColor.rgb;s.emission=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;s.smoothness=lerp(.22,_Smoothness,glass);s.metallic=_Metallic;s.alpha=1;s.occlusion=1;s.normalTS=half3(0,0,1);
    half3 colour=UniversalFragmentPBR(d,s).rgb;
    float3 reflected=reflect(-d.viewDirectionWS,d.normalWS);
    float2 skyUV=float2(frac(atan2(reflected.z,reflected.x)/6.2831853+.5+_SkyRotation/360),clamp(.535+asin(max(reflected.y,0))/3.14159265*.88,.535,.975));
    half3 sky=SAMPLE_TEXTURE2D_LOD(_SkyPanorama,sampler_SkyPanorama,skyUV,3).rgb;
    half fresnel=pow(1-saturate(dot(d.normalWS,d.viewDirectionWS)),5);
    // A bounded sky reflection on glazing only. Wall/stone mullions keep their diffuse finish.
    colour=lerp(colour,sky*.72,glass*(.13+fresnel*.42));
    float2 uv=i.w.xz*_CloudScale+float2(.17,.39);
    half3 cloud=SAMPLE_TEXTURE2D(_CloudTops,sampler_CloudTops,uv).rgb;
    cloud=lerp(cloud,SAMPLE_TEXTURE2D(_CloudTops,sampler_CloudTops,float2(uv.y,-uv.x)*.43+float2(.39,.18)).rgb,.22)*.78;
    half immerse=1-smoothstep(-59,-23,i.w.y);
    return half4(MixFog(lerp(colour,cloud,immerse),i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
