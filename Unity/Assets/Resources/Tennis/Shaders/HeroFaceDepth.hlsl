// Depth must use exactly the same face deformation/visibility as ForwardLit.
// Unclipped old corneas otherwise occlude fitted lids after their color disappears.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;float4 _BaseMap_ST;half4 _Subsurface;half4 _RimColor;
    float4 _HeadOrigin,_HeadRight,_HeadUp,_HeadForward,_HeadEye;
    float4 _EyeL,_EyeR,_FaceUp,_FaceRight,_Gaze,_FaceSkin,_EyeNL,_EyeNR;half _PaintLift,_FaceRole,_Blink,_BrowLift,_CraftedPaint;
    float4 _BumpMap_ST;half _Smoothness,_Wrap,_RimPower,_RimStrength,_BumpScale,_Saturation,_BumpTriplanar,_SpecularStrength,_EnvironmentStrength,_OcclusionStrength;float _BumpTile;half _SkinResponse;
CBUFFER_END
struct DepthAttributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;float3 bindPos:TEXCOORD1;};
struct DepthVaryings{float4 positionCS:SV_POSITION;float2 eyeCoord:TEXCOORD0;float3 normalWS:TEXCOORD1;};
DepthVaryings vertDepth(DepthAttributes i){
    DepthVaryings o;float3 p=i.positionOS.xyz;o.eyeCoord=0;
    if(_FaceRole>.5h){
        bool left=dot(p-(_EyeL.xyz+_EyeR.xyz)*.5,_FaceRight.xyz)<0;
        float4 eye=left ? _EyeL:_EyeR;float4 closeNormal=left ? _EyeNL:_EyeNR;
        if(_FaceRole<3.5h||_FaceRole>4.5h){
            if(_FaceRole>1.5h&&_FaceRole<2.5h)p+=(_FaceRight.xyz*_Gaze.x+_FaceUp.xyz*_Gaze.y)*(1-_Blink);
            o.eyeCoord=float2(dot(p-eye.xyz,_FaceRight.xyz)/max(closeNormal.w,.0001),dot(p-eye.xyz,_FaceUp.xyz)/max(eye.w,.0001));
            if(_FaceRole>7.5h&&_FaceRole<8.5h)p+=_FaceUp.xyz*_BrowLift;
            if(_FaceRole>8.5h){float h=dot(p-eye.xyz,_FaceUp.xyz);float crease=(-.45+.22*o.eyeCoord.x*o.eyeCoord.x)*eye.w;p+=_FaceUp.xyz*(crease-h)*_Blink*(1-i.uv.x);}
        }else p+=_FaceUp.xyz*_BrowLift;
    }
    p+=i.normalOS*_PaintLift;o.positionCS=TransformObjectToHClip(p);o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;
}
void ClipFaceDepth(DepthVaryings i){
    if(_FaceRole>8.5h){clip(.90h-_Blink);}
    else if(_FaceRole>7.5h){if(i.eyeCoord.y<1.3h)clip(.2h-_Blink);}
    else if(_FaceRole>6.5h){clip(-1.0h);}
    else if(_FaceRole>5.5h){clip(-1.0h);}
    else if(_FaceRole>.5h&&(_FaceRole<3.5h||_FaceRole>4.5h)){
                    if(_FaceRole>4.5h)clip(.985h-_Blink);
        half closed=-.45h+.22h*i.eyeCoord.x*i.eyeCoord.x;half progress=pow(_Blink,.8h);
        half upper=lerp(1,closed,progress),lower=lerp(-1,closed,progress);
        half lid=max(smoothstep(upper-.025h,upper+.025h,i.eyeCoord.y),1-smoothstep(lower-.025h,lower+.025h,i.eyeCoord.y));
        if(_Blink>.995h)lid=1;if(_Blink<.001h)lid=0;
        if(_FaceRole<3.5h)clip(.5h-lid);else clip(lid-.5h);
    }
}
half4 fragDepth(DepthVaryings i):SV_Target{ClipFaceDepth(i);return 0;}
half4 fragDepthNormals(DepthVaryings i):SV_Target{
    ClipFaceDepth(i);half3 n=normalize(i.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
    float2 oct=PackNormalOctQuadEncode(n);return half4(PackFloat2To888(saturate(oct*.5+.5)),0);
    #else
    return half4(n,0);
    #endif
}
