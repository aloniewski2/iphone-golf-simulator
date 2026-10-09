Shader "GolfArcade/TennisSpectator"
{
 Properties { _Smoothness("Sport fabric polish",Range(0,1))=.17 _ColorsAreSRGB("FBX color convention",Range(0,1))=0 _SurfaceRoles("Authored vertex surface roles",Range(0,1))=0 }
 SubShader {
  Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
  Pass {
   Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial) half _Smoothness,_ColorsAreSRGB,_SurfaceRoles; CBUFFER_END
   struct A{float4 p:POSITION;float3 n:NORMAL;half4 c:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;half4 c:COLOR;half fog:TEXCOORD2;};
   V vert(A a){UNITY_SETUP_INSTANCE_ID(a);V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(a.n);o.c=a.c;o.fog=ComputeFogFactor(o.p.z);return o;}
   half4 frag(V i):SV_Target{
    InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=normalize(i.n);d.viewDirectionWS=SafeNormalize(GetWorldSpaceViewDir(i.w));d.shadowCoord=TransformWorldToShadowCoord(i.w);d.bakedGI=SampleSH(d.normalWS);d.shadowMask=half4(1,1,1,1);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);
    SurfaceData s=(SurfaceData)0;s.albedo=lerp(i.c.rgb,SRGBToLinear(i.c.rgb),_ColorsAreSRGB);half cloth=step(.8,i.c.a),eye=step(.4,i.c.a)*(1-cloth),hair=step(.13,i.c.a)*(1-eye)*(1-cloth);
    half skin=1-cloth-eye-hair;half roleGloss=skin*.28+hair*.22+eye*.43+cloth*.16;
    s.smoothness=lerp(_Smoothness,roleGloss,_SurfaceRoles);s.alpha=1;s.occlusion=1;s.normalTS=half3(0,0,1);
    half4 c=UniversalFragmentPBR(d,s);half fill=skin*.11+hair*.075+eye*.025+cloth*.08;c.rgb+=s.albedo*lerp(half3(.18,.16,.13),half3(fill,fill,fill),_SurfaceRoles);c.rgb=MixFog(c.rgb,i.fog);return c;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
