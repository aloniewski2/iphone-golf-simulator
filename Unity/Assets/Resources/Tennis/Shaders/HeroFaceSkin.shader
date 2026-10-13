// Registered hero head surface; preserves the existing body response below the jaw.
//
// Resort athlete surface: scene lighting describes the form; skin, eyes and equipment
// have distinct controlled highlights. The approved geometry and paint remain intact.
Shader "GolfArcade/HeroFaceSkin"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0,2)) = 1
        [HideInInspector] _HeadOrigin ("Skin head origin", Vector) = (0,0,0,0)
        [HideInInspector] _HeadRight ("Skin head right", Vector) = (1,0,0,0)
        [HideInInspector] _HeadUp ("Skin head up", Vector) = (0,1,0,0)
        [HideInInspector] _HeadForward ("Skin head forward", Vector) = (0,0,1,0)
        [HideInInspector] _HeadEye ("Skin eye registration", Vector) = (0,0,0,0)
        // Existing-face eye treatment: UV projection is material state only.
        [HideInInspector] _EyeMapEnabled ("Existing eye albedo projection", Float) = 0
        [HideInInspector] _EyeMapL ("Left eye map centre/width", Vector) = (0,0,0,1)
        [HideInInspector] _EyeMapR ("Right eye map centre/width", Vector) = (0,0,0,1)
        [HideInInspector] _EyeMapUpL ("Left eye map up/height", Vector) = (0,1,0,1)
        [HideInInspector] _EyeMapUpR ("Right eye map up/height", Vector) = (0,1,0,1)
        [HideInInspector] _EyeMapRightL ("Left eye map right", Vector) = (1,0,0,0)
        [HideInInspector] _EyeMapRightR ("Right eye map right", Vector) = (1,0,0,0)
        [HideInInspector] _CraftedPaint ("Authored brow coverage", Float) = 0
        [HideInInspector] _PaintLift ("Paint layer separation", Float) = 0
        [HideInInspector] _FaceRole ("Face: 1 eye, 2 iris, 3 lid, 4 brow", Float) = 0
        [HideInInspector] _Blink ("Blink", Range(0,1)) = 0
        [HideInInspector] _EyeNL ("Left closure normal and width", Vector) = (0,0,1,.03)
        [HideInInspector] _EyeNR ("Right closure normal and width", Vector) = (0,0,1,.03)
        [HideInInspector] _FaceSkin ("Fitted lid skin", Color) = (1,1,1,1)
        [HideInInspector] _Gaze ("Eye gaze", Vector) = (0,0,0,0)
        [HideInInspector] _EyeL ("Left eye centre", Vector) = (0,0,0,0)
        [HideInInspector] _EyeR ("Right eye centre", Vector) = (0,0,0,0)
        [HideInInspector] _FaceUp ("Face up", Vector) = (0,1,0,0)
        [HideInInspector] _FaceRight ("Face right", Vector) = (1,0,0,0)
        [HideInInspector] _BrowLift ("Brow lift", Float) = 0
        // The hero body has no UVs. With this on, _BumpMap is projected along the three axes from the BIND-POSE position carried in mesh channel 1
        // (MatchHeroLook writes it), so the soft break in the light stays on the skin as the body moves. _BumpTile = tiles per metre.
        _BumpTriplanar ("Bump from bind-pose position (meshes with no UVs)", Float) = 0
        _BumpTile ("Triplanar bump tiles per metre", Float) = 5
        _Saturation ("Saturation", Range(0,1.5)) = 0.96
        _Smoothness ("Smoothness", Range(0,1)) = 0.28
        _SpecularStrength ("Surface highlight strength", Range(0,2)) = 1
        _EnvironmentStrength ("Scene reflection strength", Range(0,1)) = 0.12
        _OcclusionStrength ("Character ambient occlusion strength", Range(0,1)) = 0.25
        // CLOTHING V2: soft contact shadow on the skin where a garment edge meets it (mesh UV3.x, written by MatchHeroLook.ApplyKitV2Cover). 0 = off.
        [HideInInspector] _KitContact ("Garment contact shadow", Range(0,1)) = 0
        [HideInInspector] _SkinResponse ("Regional skin response pilot", Float) = 0
        [HideInInspector] _SkinFinish ("Skin finish pilot: 1 male, 2 female", Float) = 0
        [HideInInspector] _SkinPigmentUV ("Original skin pigment mask coordinates", Float) = 0
        _Wrap ("Wrap", Range(0,1)) = 0.35
        _Subsurface ("Subsurface warmth", Color) = (0,0,0,0)
        _RimColor ("Rim colour", Color) = (1,0.96,0.88,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3.5
        _RimStrength ("Rim strength", Range(0,1)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            // multi_compile, not shader_feature: the materials are made at runtime, so no
            // material asset would keep a shader_feature variant alive in the build.
            #pragma multi_compile_local _ _NORMALMAP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "HeroLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor; float4 _BaseMap_ST; half4 _Subsurface; half4 _RimColor;
                float4 _HeadOrigin, _HeadRight, _HeadUp, _HeadForward, _HeadEye;
                float4 _EyeMapL, _EyeMapR, _EyeMapUpL, _EyeMapUpR, _EyeMapRightL, _EyeMapRightR; half _EyeMapEnabled;
                float4 _EyeL, _EyeR, _FaceUp, _FaceRight, _Gaze, _FaceSkin, _EyeNL, _EyeNR; half _PaintLift, _FaceRole, _Blink, _BrowLift, _CraftedPaint;
                float4 _BumpMap_ST; half _Smoothness, _Wrap, _RimPower, _RimStrength, _BumpScale, _Saturation, _BumpTriplanar, _SpecularStrength, _EnvironmentStrength, _OcclusionStrength; float _BumpTile; half _SkinResponse, _SkinFinish, _SkinPigmentUV;
                half _KitContact;
            CBUFFER_END
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float3 bindPos : TEXCOORD1; float2 lidDistance : TEXCOORD2; float2 kitContact : TEXCOORD3; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; half fog : TEXCOORD3; float4 tangentWS : TEXCOORD4; float3 bindPos : TEXCOORD5; float3 normalOS : TEXCOORD6; float2 eyeCoord : TEXCOORD7; float2 lidDistance : TEXCOORD8; half kitContact : TEXCOORD9; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                float3 facePos = i.positionOS.xyz;
                o.eyeCoord = 0; o.lidDistance = i.lidDistance; o.kitContact = i.kitContact.x * _KitContact;
                float3 faceNormal = i.normalOS;
                if (_FaceRole > .5h) {
                    float4 eye = dot(facePos - (_EyeL.xyz + _EyeR.xyz) * .5, _FaceRight.xyz) < 0 ? _EyeL : _EyeR;
                    if (_FaceRole < 3.5h || _FaceRole > 4.5h) {
                        float4 closeNormal = dot(facePos - (_EyeL.xyz + _EyeR.xyz) * .5, _FaceRight.xyz) < 0 ? _EyeNL : _EyeNR;
                        // The sclera is the fitted surface over a real eye cavity. Keep it
                        // intact: eyelid color sweeps over it rather than opening a hole.
                        if (_FaceRole > 1.5h && _FaceRole < 2.5h) facePos += (_FaceRight.xyz * _Gaze.x + _FaceUp.xyz * _Gaze.y) * (1 - _Blink);
                        float h = dot(facePos - eye.xyz, _FaceUp.xyz);
                        o.eyeCoord = float2(dot(facePos - eye.xyz, _FaceRight.xyz) / max(closeNormal.w,.0001), h / max(eye.w, .0001));
                        if(_FaceRole>7.5h&&_FaceRole<8.5h)facePos+=_FaceUp.xyz*_BrowLift;
                        if(_FaceRole>8.5h){float crease=(-.45+.22*o.eyeCoord.x*o.eyeCoord.x)*eye.w;facePos+=_FaceUp.xyz*(crease-h)*_Blink*(1-i.uv.x);}
                        // The closure shell already carries the adjacent body normals.
                    } else facePos += _FaceUp.xyz * _BrowLift;
                }
                facePos += i.normalOS * _PaintLift;
                VertexPositionInputs p = GetVertexPositionInputs(facePos);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                VertexNormalInputs nrm = GetVertexNormalInputs(faceNormal, i.tangentOS);
                o.normalWS = nrm.normalWS;
                o.tangentWS = float4(nrm.tangentWS, i.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                if (_EyeMapEnabled > .5h) {
                    // Sample in the unchanged feature's source coordinates, before
                    // gaze/closure. The material's albedo follows the existing eye.
                    bool left = dot(i.positionOS.xyz - (_EyeMapL.xyz + _EyeMapR.xyz) * .5, _FaceRight.xyz) < 0;
                    float4 centre = left ? _EyeMapL : _EyeMapR;
                    float4 mapUp = left ? _EyeMapUpL : _EyeMapUpR;
                    float3 mapRight = left ? _EyeMapRightL.xyz : _EyeMapRightR.xyz;
                    float3 d = i.positionOS.xyz - centre.xyz;
                    o.uv = .5 + .5 * float2(dot(d, mapRight) / max(centre.w, .0001), dot(d, mapUp.xyz) / max(mapUp.w, .0001));
                }
                o.bindPos = i.bindPos; o.normalOS = i.normalOS;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            

            half SkinRegion(float3 skinPoint,float3 centre,float3 radius) {
                float3 q=(skinPoint-centre)/radius;return exp(-2*dot(q,q));
            }
            // Skin pass (matte, warm form) 2026-10-12: smooth value noise in the bind pose for a faint skin grain (rides on the skin as it moves).
            float SkinHash(float3 p){p=frac(p*.1031);p+=dot(p,p.zyx+31.32);return frac((p.x+p.y)*p.z);}
            float SkinNoise(float3 p){
                float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(SkinHash(i),SkinHash(i+float3(1,0,0)),f.x),lerp(SkinHash(i+float3(0,1,0)),SkinHash(i+float3(1,1,0)),f.x),f.y),
                            lerp(lerp(SkinHash(i+float3(0,0,1)),SkinHash(i+float3(1,0,1)),f.x),lerp(SkinHash(i+float3(0,1,1)),SkinHash(i+float3(1,1,1)),f.x),f.y),f.z);
            }
            half3 FaceSkinShade(Light light, half3 n, half3 v, half3 albedo, half wrap, half smoothness, half3 subsurface, half specularStrength, half faceAmount, out half3 reflection)
            {
                half3 previousReflection;
                half3 previous = HeroSkinShade(light,n,v,albedo,wrap,smoothness,subsurface,specularStrength,previousReflection);
                half ndl=dot(n,light.direction), nl=saturate(ndl);
                half wrapped=saturate((ndl+wrap)/(1+wrap));
                // Skin pass: only a little extra lift on the lit planes (was a full (ndl+.38)/1.38 blend, which flattened the face's form).
                wrapped=lerp(wrapped,saturate((ndl+.30h)/1.30h),smoothstep(.15h,.65h,ndl)*faceAmount*.35h);
                half nh=saturate(dot(n,SafeNormalize(light.direction+v)));
                // Skin pass: matte. A faint, wide soft sheen only (was a broad gloss up to .24 on the forehead, nose and lips: the waxy look).
                half sheen=pow(nh,8+smoothness*10)*.045h;
                half visibility=smoothstep(-.06h,.24h,ndl);
                half attenuation=light.distanceAttenuation*lerp(light.shadowAttenuation,1,.18h);
                half3 spec=sheen*specularStrength*visibility*light.color*attenuation;
                // Skin pass: a warm reddish-brown light bleed where the light rolls off (was a pale rosy .20/.12/.09).
                half3 scatter=albedo*half3(.24h,.10h,.04h)*saturate(wrapped-nl)*light.color*attenuation;
                half3 result=albedo*wrapped*.84h*light.color*attenuation+scatter+spec;
                reflection=lerp(previousReflection,spec,faceAmount);
                return lerp(previous,result,faceAmount);
            }
            half4 frag (Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                #if defined(_NORMALMAP)
                if (_BumpTriplanar > 0.5)
                {
                    // Three axis projections of the soft normal map from the bind-pose position, blended by the skinned object-space normal
                    // and added to it (UDN blend), then back to world: a break in the light that rides on the skin.
                    float3 nO = normalize(i.normalOS);
                    float3 w = pow(abs(nO), 4); w /= (w.x + w.y + w.z + 1e-5);
                    half2 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.bindPos.zy * _BumpTile), _BumpScale).xy;
                    half2 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.bindPos.xz * _BumpTile), _BumpScale).xy;
                    half2 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.bindPos.xy * _BumpTile), _BumpScale).xy;
                    nO = normalize(nO + w.x * float3(0, tx.y, tx.x) + w.y * float3(ty.x, 0, ty.y) + w.z * float3(tz.x, tz.y, 0));
                    n = normalize(TransformObjectToWorldNormal(nO));
                }
                else
                {
                    half3 ts = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                    half3 t = normalize(i.tangentWS.xyz); half3 b = cross(n, t) * i.tangentWS.w;
                    n = normalize(TransformTangentToWorld(ts, half3x3(t, b, n)));
                }
                #endif
                half3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                if (_SkinFinish > .5h && _SkinPigmentUV > .5h) {
                    // Continuous registered pigment below replaces the sparse
                    // imported vertex masks, preserving the selected base tint.
                    c.rgb = _BaseColor.rgb;
                }
                half faceAmount = 0, noseUnderside = 0;
                half lipMask = _SkinPigmentUV > .5h ? saturate((i.uv.x*64-.5h)/63) : 0;
                half lid = 0;
                if(_FaceRole>8.5h){clip(.90h-_Blink);c.rgb=_FaceSkin.rgb;}
                else if(_FaceRole>7.5h){if(i.eyeCoord.y<1.3h)clip(.2h-_Blink);}
                else if(_FaceRole>6.5h){clip(-1.0h);}
                else if(_FaceRole>5.5h) {clip(-1.0h);}
                else if (_FaceRole > .5h && (_FaceRole < 3.5h || _FaceRole > 4.5h)) {
                    if(_FaceRole>4.5h)clip(.985h-_Blink);
                    half closed = -.45h + .22h * i.eyeCoord.x * i.eyeCoord.x;
                    half progress = pow(_Blink,.8h);
                    half upper = lerp(1,closed,progress), lower=lerp(-1,closed,progress);
                    lid = max(smoothstep(upper - .025h, upper + .025h, i.eyeCoord.y), 1 - smoothstep(lower - .025h, lower + .025h, i.eyeCoord.y));
                    if (_Blink > .995h) lid = 1;
                    if (_Blink < .001h) lid = 0;
                    if (_FaceRole < 3.5h) clip(.5h - lid);
                    else {
                        clip(lid - .5h); c.rgb = _FaceSkin.rgb;
                        half creaseY = closed;
                        half crease = (1 - smoothstep(.018h,.045h,abs(i.eyeCoord.y - creaseY))) * (1 - smoothstep(.78h,.98h,abs(i.eyeCoord.x))) * smoothstep(.72h,1,_Blink);
                        c.rgb *= 1 - crease * .38h;
                    }
                }
                if(_CraftedPaint>.5h && _FaceRole>3.5h && _FaceRole<4.5h){half coverage=smoothstep(0,.16h,i.uv.y)*smoothstep(0,.16h,1-i.uv.y)*smoothstep(0,.045h,i.uv.x)*smoothstep(0,.045h,1-i.uv.x);c.rgb=lerp(_FaceSkin.rgb,c.rgb,coverage);}
                half surfaceSmoothness = lerp(_Smoothness, .28h, lid);
                half surfaceSpecular = lerp(_SpecularStrength, .8h, lid);
                half surfaceWrap = lerp(_Wrap, .32h, lid);
                // The generated textures are painted warm and the resort grade warms again;
                // pull saturation back so skin reads peach rather than orange.
                if(_HeadOrigin.w>.5h){
                    float3 delta=i.bindPos-_HeadOrigin.xyz;
                    float3 hp=float3(dot(delta,_HeadRight.xyz),dot(delta,_HeadUp.xyz),dot(delta,_HeadForward.xyz))-_HeadEye.xyz;
                    faceAmount=smoothstep(-.19h,-.12h,hp.y)*(1-smoothstep(.14h,.19h,abs(hp.x)));
                    half cheeks=SkinRegion(hp,float3(-.044,-.052,-.005),float3(.032,.031,.080))+SkinRegion(hp,float3(.044,-.052,-.005),float3(.032,.031,.080));
                    half nose=SkinRegion(hp,float3(0,-.040,.018),float3(.022,.038,.070));
                    half forehead=SkinRegion(hp,float3(0,.062,-.027),float3(.054,.044,.075));
                    half ears=SkinRegion(hp,float3(-.092,-.025,-.055),float3(.020,.042,.055))+SkinRegion(hp,float3(.092,-.025,-.055),float3(.020,.042,.055));
                    if(_SkinFinish>.5h && _SkinFinish<1.5h)
                        ears=SkinRegion(hp,float3(-.102,-.015,-.095),float3(.018,.042,.030))+SkinRegion(hp,float3(.102,-.015,-.095),float3(.018,.042,.030));
                    half warmth=saturate(cheeks*.85h+nose*.40h+ears*.7h);
                    if (_SkinFinish > .5h) {
                        // Skin pass: the female face lift is golden, not cool (was 1.10/1.16/1.26, which paled it to pink).
                        c.rgb*=lerp(half3(1,1,1),_SkinFinish<1.5h?half3(1.0h,.99h,.97h):half3(1.08h,1.09h,1.04h),faceAmount);
                        float noseBase=_SkinFinish<1.5h?-.047:-.045;
                        noseUnderside=exp(-2*pow(hp.x/.025,2)-2*pow((hp.y-noseBase)/.007,2))*smoothstep(.015,.026,hp.z);
                        // Small warm cavity pigment at the anatomical nostril
                        // bases; the broad nasal shading still comes from light.
                        if(_SkinFinish<1.5h){
                            half cavity=exp(-2*pow((abs(hp.x)-.016)/.005,2)-2*pow((hp.y+.042)/.0022,2))*smoothstep(.013,.025,hp.z);
                            c.rgb*=lerp(half3(1,1,1),half3(.58h,.44h,.38h),cavity*.40h);
                        }
                        // Bald-base pass 2: a smaller mouth with a softer curve (was .041 / .033 wide, corners up .004 / .0075).
                        float mouthWidth=_SkinFinish<1.5h?.035:.028;
                        float mx=abs(hp.x)/mouthWidth;
                        float seam=(_SkinFinish<1.5h?-.0683:-.0675)+(_SkinFinish<1.5h?.0035:.0045)*mx*mx;
                        float fullness=saturate(1-mx*mx);
                        float cupid=.0015*exp(-pow((abs(hp.x)-.008)/.005,2))*fullness;
                        float upper=seam+.0055*fullness+cupid,lower=seam-.0075*fullness;
                        float mouthFront=smoothstep(-.006,.009,hp.z);
                        lipMask=smoothstep(lower-.0009,lower+.0009,hp.y)*(1-smoothstep(upper-.0009,upper+.0009,hp.y))*(1-smoothstep(.94,1.04,mx))*mouthFront;
                        half seamMask=exp(-pow((hp.y-seam)/.00085,2))*(1-smoothstep(.88,1.03,mx))*mouthFront;
                        half3 lipTint=_SkinFinish<1.5h?half3(1.04h,.70h,.68h):half3(1.06h,.68h,.65h);
                        c.rgb*=lerp(half3(1,1,1),lipTint,lipMask);
                        c.rgb*=lerp(half3(1,1,1),half3(.50h,.30h,.26h),seamMask*.78h);
                        half upperRim=1-smoothstep(.0010h,_SkinFinish<1.5h?.0025h:.0048h,i.lidDistance.x);
                        half lowerRim=1-smoothstep(0,.0012h,i.lidDistance.y);
                        c.rgb*=lerp(half3(1,1,1),half3(.12h,.065h,.040h),upperRim*.96h);
                        c.rgb*=lerp(half3(1,1,1),half3(.74h,.60h,.55h),lowerRim*.30h);
                        // Regional pigment follows the existing registered form.
                        c.rgb *= lerp(half3(1,1,1),half3(1.03h,.965h,.89h),warmth);   // skin pass: golden warmth (was 1.035/.93/.91, pinkish)
                        // Bald-base pass: a light rose blush on the cheek apples, ~3 cm under the outer eye (pink-red, not orange), stronger on the female.
                        half apples=_SkinFinish<1.5h
                            ? SkinRegion(hp,float3(-.050,-.032,-.010),float3(.024,.020,.060))+SkinRegion(hp,float3(.050,-.032,-.010),float3(.024,.020,.060))
                            : SkinRegion(hp,float3(-.046,-.031,-.005),float3(.025,.021,.060))+SkinRegion(hp,float3(.046,-.031,-.005),float3(.025,.021,.060));
                        half blush=saturate(apples);
                        c.rgb*=lerp(half3(1,1,1),_SkinFinish<1.5h?half3(1.06h,.66h,.70h):half3(1.06h,.66h,.70h),blush);
                        // Skin pass: natural variation. The ears and the nose tip a touch warmer (both heroes).
                        bool maleFace=_SkinFinish<1.5h;
                        c.rgb*=lerp(half3(1,1,1),half3(1.04h,.93h,.86h),ears*.45h);
                        half noseTip=SkinRegion(hp,maleFace?float3(0,-.037,.054):float3(0,-.037,.032),float3(.011,.010,.020));
                        c.rgb*=lerp(half3(1,1,1),half3(1.05h,.93h,.86h),noseTip*.55h);
                        // Skin pass: soft warm form shading, darker and redder where the face turns away or hollows: under the brows, beside
                        // the nose, under the cheekbones, under the jaw and down the neck. Pigment, so it rides with the face in any light.
                        float3 hs=float3(abs(hp.x),hp.y,hp.z);
                        // centres measured on the bind-pose surface of each head (the female's eyes are taller and her face sits further back in this frame)
                        half browHollow=SkinRegion(hs,maleFace?float3(.034,.020,.017):float3(.036,.029,.006),float3(.021,.007,.035));
                        half innerCorner=SkinRegion(hs,maleFace?float3(.016,-.006,.025):float3(.016,-.006,.010),float3(.007,.011,.035));
                        half noseSide=SkinRegion(hs,maleFace?float3(.021,-.042,.044):float3(.020,-.042,.016),float3(.007,.011,.035));
                        half smileLine=SkinRegion(hs,maleFace?float3(.028,-.056,.039):float3(.026,-.056,.011),float3(.007,.013,.035));
                        half cheekHollow=SkinRegion(hs,maleFace?float3(.056,-.071,.014):float3(.053,-.071,-.004),float3(.020,.018,.040));
                        half jawSide=SkinRegion(hs,maleFace?float3(.064,-.106,-.020):float3(.060,-.100,-.040),float3(.022,.026,.050));
                        // under the jaw and down the neck, kept to the neck's width (the shoulders and chest stay as they are)
                        half neckWidth=1-smoothstep(maleFace?.055h:.048h,maleFace?.080h:.070h,abs(hp.x));
                        half neck=smoothstep(-.125h,-.160h,hp.y)*(1-smoothstep(-.270h,-.330h,hp.y))*neckWidth;
                        half form=saturate(browHollow*.85h+innerCorner*.70h+noseSide*.65h+smileLine*.35h+cheekHollow*.60h+jawSide*.55h+neck*.95h)*(1-lipMask);
                        // and a soft light-from-above: the forehead and crown a little lighter than the jaw
                        c.rgb*=1+half3(.06h,.055h,.04h)*smoothstep(-.04h,.10h,hp.y)*faceAmount;
                        c.rgb*=lerp(half3(1,1,1),half3(.80h,.66h,.55h),form);
                        if (_SkinFinish < 1.5h) {
                            half jaw = SkinRegion(hp,float3(0,-.126h,-.010h),float3(.086h,.048h,.095h));
                            half upperLip = SkinRegion(hp,float3(0,-.058h,.040h),float3(.037h,.012h,.045h));
                            half lowerCheeks=SkinRegion(hp,float3(-.060h,-.077h,.002h),float3(.032h,.052h,.055h))+SkinRegion(hp,float3(.060h,-.077h,.002h),float3(.032h,.052h,.055h));
                            half beard = saturate(jaw*.70h+upperLip*.52h+lowerCheeks*.48h)*(1-lipMask*.95h);
                            beard*=smoothstep(-.145h,-.120h,hp.y)*smoothstep(-.055h,-.025h,hp.z);
                            c.rgb *= lerp(half3(1,1,1),half3(.82h,.85h,.89h),beard);
                            float3 cell=floor(hp*1300);
                            half stipple=smoothstep(.65h,.90h,frac(sin(dot(cell,float3(12.9898,78.233,45.164)))*43758.5453));
                            half resolved=1-smoothstep(.0005,.0015,max(length(ddx(hp)),length(ddy(hp))));
                            c.rgb*=1-stipple*beard*.15h*resolved;
                        }
                        // Skin pass: matte skin. Smoothness .26-.34 (was .32-.50) and a low highlight strength; a lower wrap (.20-.26, was .28-.50) brings the form back.
                        surfaceSmoothness = lerp(.26h,.30h,saturate(nose+forehead*.55h));
                        half facialSmoothness=lerp(.27h,.32h,saturate(nose+forehead*.75h));
                        facialSmoothness=lerp(facialSmoothness,.34h,lipMask);
                        surfaceSmoothness=lerp(surfaceSmoothness,facialSmoothness,faceAmount);
                        surfaceSpecular = .40h;
                        surfaceWrap = lerp(.20h,.24h,faceAmount);
                        surfaceWrap=lerp(surfaceWrap,.26h,lipMask);
                    }
                    else {
                        c.rgb*=half3(1+.025h*warmth,1-.060h*warmth,1-.025h*warmth)*(1+.012h*forehead);
                        surfaceSmoothness=saturate(surfaceSmoothness+.08h*nose+.06h*forehead-.025h*cheeks);
                    }
                    if(_SkinFinish<.5h && _SkinResponse>.5h){
                        // Colour variation follows the original head's registered bind frame.
                        // This survives animation and every selected skin tone.
                        half blush=saturate(cheeks*.85h+nose*.30h+ears*.65h);
                        c.rgb*=lerp(half3(1,1,1),half3(1.025h,.82h,.87h),blush);
                        half chin=SkinRegion(hp,float3(0,-.126h,-.010h),float3(.086h,.048h,.095h));
                        half upperLip=SkinRegion(hp,float3(0,-.077h,.016h),float3(.048h,.016h,.060h));
                        half beard=_SkinResponse<1.5h?saturate(chin*.45h+upperLip*.24h):0;
                        c.rgb*=lerp(half3(1,1,1),half3(.79h,.80h,.82h),beard);
                        surfaceSmoothness=lerp(.29h,.40h,saturate(nose+forehead*.60h));
                        surfaceSpecular*=.72h;
                        surfaceWrap=.28h;
                    }
                }
                if (_SkinFinish > .5h && _FaceRole < .5h) {
                    // Skin pass (2026-10-12): golden tan, not pink or salmon. A relative grade, so every skin-tone pick keeps its own depth.
                    c.rgb *= half3(1.0h, 1.035h, .92h);
                    // A very faint grain: ~3 mm and ~18 mm value noise in the bind pose, faded out before it can alias.
                    float footprint = max(length(ddx(i.bindPos)), length(ddy(i.bindPos)));
                    half fine = SkinNoise(i.bindPos * 330) - .5h, coarse = SkinNoise(i.bindPos * 55) - .5h;
                    c.rgb *= 1 + fine * .05h * (1 - smoothstep(.0008, .0025, footprint));
                    c.rgb *= lerp(half3(1,1,1), half3(1.03h, .975h, .95h), coarse * 1.2h * (1 - smoothstep(.004, .012, footprint)));
                    surfaceSpecular = min(surfaceSpecular, .40h);
                }
                c.rgb = lerp(dot(c.rgb, half3(0.2126, 0.7152, 0.0722)).xxx, c.rgb, _Saturation);
                Light main = HeroMain(GetMainLight(TransformWorldToShadowCoord(i.positionWS)));
                half3 reflection;
                half3 colour;
                if (_SkinFinish > .5h) colour = FaceSkinShade(main, n, v, c.rgb, surfaceWrap, surfaceSmoothness, _Subsurface.rgb, surfaceSpecular, faceAmount, reflection);
                else colour = HeroCharacterShade(main, n, v, c.rgb, surfaceWrap, surfaceSmoothness, _Subsurface.rgb, surfaceSpecular, reflection);
                if (_HeroPresentationFill > 0) {
                    half3 fillReflection;
                    Light fill = HeroPresentationLight(v);
                    if (_SkinFinish > .5h) colour += FaceSkinShade(fill, n, v, c.rgb, surfaceWrap, surfaceSmoothness, _Subsurface.rgb, surfaceSpecular, faceAmount, fillReflection);
                    else colour += HeroCharacterShade(fill, n, v, c.rgb, surfaceWrap, surfaceSmoothness, _Subsurface.rgb, surfaceSpecular, fillReflection);
                    reflection += fillReflection;
                }
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                #if defined(_LIGHT_LAYERS)
                uint meshLayers = GetMeshRenderingLayer();
                #endif
                for (uint li = 0; li < count; li++)
                {
                    Light extra = GetAdditionalLight(li, i.positionWS);
                    // A light on its own rendering layer (the hero rim) only reaches meshes that carry that layer.
                    #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(extra.layerMask, meshLayers)) continue;
                    #endif
                    half3 extraReflection;
                    if (_SkinFinish > .5h) colour += FaceSkinShade(extra, n, v, c.rgb, surfaceWrap, surfaceSmoothness, _Subsurface.rgb, surfaceSpecular, faceAmount, extraReflection);
                    else colour += HeroCharacterShade(extra, n, v, c.rgb, surfaceWrap, surfaceSmoothness, _Subsurface.rgb, surfaceSpecular, extraReflection);
                    reflection += extraReflection;
                }
                #endif
                // Real probe reflections are restrained on skin and clear on varnished eyes/frames.
                // One shared reflection sample, no screen-space reflections or new render pass.
                half roughness = max(1 - surfaceSmoothness, .08h);
                half fresnel = .04h + .10h * pow(1 - saturate(dot(n, v)), 5);
                half3 environment = GlossyEnvironmentReflection(reflect(-v, n), i.positionWS, roughness, 1) * fresnel * lerp(_EnvironmentStrength * (_SkinFinish > .5h ? .3h : 1), .12h, lid);   // matte skin
                colour += environment; reflection += environment;
                half3 ambient = HeroAmbient(n) * c.rgb;
                // Skin pass: the fill in the shade comes back warm (skin passes light back reddish-brown) instead of grey; .82 off the face as before.
                if (_SkinFinish > .5h) ambient *= lerp(.82h,.97h,faceAmount) * half3(1.0h,.86h,.72h);
                ambient+=c.rgb*half3(.17h,.13h,.11h)*noseUnderside;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                half indirectAO = lerp(1,ao.indirectAmbientOcclusion,_OcclusionStrength);
                half directAO = lerp(1,ao.directAmbientOcclusion,_OcclusionStrength);
                ambient *= indirectAO; colour *= directAO; reflection *= directAO;
                #endif
                half rim = pow(1 - saturate(dot(v, n)), _RimPower) * _RimStrength * (_HeroProfile > .5h ? _HeroRim : 1);
                half3 rimLight = _RimColor.rgb * rim * (0.35 + 0.65 * c.rgb) * main.color;
                if (_SkinFinish > .5h) rimLight *= .30h;   // skin pass: a softer edge (was .45)
                colour += ambient + rimLight; reflection += rimLight;
                colour *= 1 - i.kitContact; reflection *= 1 - i.kitContact;   // CLOTHING V2 contact shadow (0 unless a V2 garment edge is near)
                return half4(MixFog(HeroCharacterFinish(colour, reflection), i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        Pass {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #include "HeroFaceDepth.hlsl"
            ENDHLSL
        }
        Pass {
            Name "DepthNormals" Tags {"LightMode"="DepthNormals"}
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepthNormals
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "HeroFaceDepth.hlsl"
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
