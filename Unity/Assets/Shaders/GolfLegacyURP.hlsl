// URP rendering for the existing golf assets. Keep their authored textures, material
// properties and vertex colours; Unity surface-shader passes only run in Built-in.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../Resources/Tennis/Shaders/HeroLighting.hlsl"

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_MatCap); SAMPLER(sampler_MatCap);
TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
TEXTURE2D(_Knit); SAMPLER(sampler_Knit);
TEXTURE2D(_Detail); SAMPLER(sampler_Detail);
TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
TEXTURE2D(_Glow); SAMPLER(sampler_Glow);
TEXTURE2D(_Dimples); SAMPLER(sampler_Dimples);
CBUFFER_START(UnityPerMaterial)
float4 _Color, _MainTex_ST, _Shirt, _Shorts, _Accent, _Skin, _ScalpSkin, _Ref;
float4 _KitColor, _ShirtColor, _HairSway, _HatHold;
float _UseAtlas, _MatCapStrength, _Wrap, _SkinShading, _UseFlex, _UseScalp, _HairAmount, _Clear, _Fill;
float _MatCapGain, _KnitTile, _Fabric, _KitOn, _ShirtOn;
float _SurfaceSmoothness, _SurfaceSpecular, _SurfaceSheen; float4 _SurfaceWarmth;
float4 _GlowColor; float _GlowAmount, _BumpScale;
float _Tile, _Strength, _Gain, _Cutoff, _Sheen, _SheenPower, _Depth, _Glossiness;
CBUFFER_END

struct GolfAttributes
{
    float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT;
    float2 uv : TEXCOORD0; float4 color : COLOR;
};
struct GolfVaryings
{
    float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float4 color : COLOR;
    float4 tangentWS : TEXCOORD3; float3 positionOS : TEXCOORD4;
    float3 normalOS : TEXCOORD5; half fog : TEXCOORD6;
};

GolfVaryings GolfLegacyVertex(GolfAttributes a)
{
    GolfVaryings o;
    #if defined(GOLF_HERO)
    float held = saturate(dot(a.color.gba, _HatHold.xyz));
    a.positionOS.xyz += mul((float3x3)unity_WorldToObject, _HairSway.xyz) * a.color.r * (1 - held) * _UseFlex;
    #endif
    VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz);
    VertexNormalInputs n = GetVertexNormalInputs(a.normalOS, a.tangentOS);
    o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = n.normalWS;
    o.tangentWS = float4(n.tangentWS, a.tangentOS.w * GetOddNegativeScale());
    o.uv = TRANSFORM_TEX(a.uv, _MainTex); o.color = a.color;
    #if defined(GOLF_BALL)
    o.uv = a.uv; // GolfBall uses _Dimples, and has no _MainTex scale/offset property.
    #endif
    o.positionOS = a.positionOS.xyz; o.normalOS = a.normalOS;
    o.fog = ComputeFogFactor(p.positionCS.z);
    return o;
}

half3 GolfTint(half3 c, half lum, half4 tint, half reference, half weight, half lift)
{
    return lerp(c, tint.rgb * clamp(lum / max(reference, .01), .25, lift), saturate(weight * tint.a));
}

half3 GolfLegacyAlbedo(GolfVaryings i, half3 normal)
{
    half3 colour = _Color.rgb;
    #if defined(GOLF_TURF)
    float2 uv = i.positionWS.xz / max(_Tile, .01);
    half nearDetail = SAMPLE_TEXTURE2D(_Detail, sampler_Detail, uv).r - .5;
    half farDetail = SAMPLE_TEXTURE2D(_Detail, sampler_Detail, uv * .131 + .37).r - .5;
    return colour * (1 + _Strength * (nearDetail * 1.5 + farDetail * .5));
    #elif defined(GOLF_BALL)
    return colour;
    #elif defined(GOLF_CRATER)
    return colour * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
    #else
    half4 map = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
    #if defined(GOLF_HERO)
    clip(.5 - _Clear);
    if (_UseAtlas > .5)
    {
        half4 mask = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv);
        half lum = dot(map.rgb, half3(.2126, .7152, .0722));
        half3 c = GolfTint(map.rgb, lum, _Shirt, _Ref.x, mask.r, 1.1);
        c = GolfTint(c, lum, _Shorts, _Ref.y, mask.g, 1.35);
        c = GolfTint(c, lum, _Accent, _Ref.z, mask.b, 1.35);
        colour *= GolfTint(c, lerp(_Ref.w, lum, _SkinShading), _Skin, _Ref.w, mask.a, 1);
    }
    if (_UseScalp > .5) colour = lerp(_ScalpSkin.rgb, _Color.rgb * map.rgb, saturate(i.color.r * _HairAmount));
    if (_UseFlex > .5) colour *= lerp(.82, 1.1, saturate(i.color.r * 2.4));
    #elif defined(GOLF_HAIR)
    clip(map.a * i.color.a - _Cutoff);
    colour *= map.rgb * _Gain;
    #elif defined(GOLF_CLAY)
    // The existing golfer atlas uses dark blue for shorts and near-white for its shirt.
    half hi = max(map.r, max(map.g, map.b)), lo = min(map.r, min(map.g, map.b));
    half navy = smoothstep(.02, .08, map.b - map.r) * smoothstep(-.03, .01, map.b - map.g) * (1 - smoothstep(.55, .7, hi));
    half white = smoothstep(.6, .72, lo) * (1 - smoothstep(.1, .16, hi - lo));
    half lum = dot(map.rgb, half3(.3, .59, .11));
    map.rgb = lerp(map.rgb, _KitColor.rgb * min(lum / .21, 1.6), navy * _KitOn);
    map.rgb = lerp(map.rgb, _ShirtColor.rgb * min(lum / .92, 1.1), white * _ShirtOn);
    colour *= map.rgb;
    if (_Fabric > 0)
    {
        float3 weights = pow(abs(normalize(i.normalOS)), 4);
        weights /= max(dot(weights, 1), .001);
        float3 p = i.positionOS * _KnitTile;
        half knit = SAMPLE_TEXTURE2D(_Knit, sampler_Knit, p.yz).r * weights.x
                  + SAMPLE_TEXTURE2D(_Knit, sampler_Knit, p.xz).r * weights.y
                  + SAMPLE_TEXTURE2D(_Knit, sampler_Knit, p.xy).r * weights.z;
        colour *= 1 + _Fabric * (knit - .5) * 2;
    }
    #endif
    float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_V, normal));
    half cap = SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, viewNormal.xy * .49 + .5).r;
    #if defined(GOLF_CLAY)
    cap *= _MatCapGain;
    #endif
    return colour * lerp(1, cap, _MatCapStrength);
    #endif
}

half4 GolfLegacyFragment(GolfVaryings i, FRONT_FACE_TYPE front : FRONT_FACE_SEMANTIC) : SV_Target
{
    half3 n = normalize(i.normalWS);
    #if defined(GOLF_HAIR)
    n *= IS_FRONT_VFACE(front, 1, -1);
    #endif
    #if defined(GOLF_BALL)
    half3 detail = SAMPLE_TEXTURE2D(_Dimples, sampler_Dimples, i.uv).xyz * 2 - 1;
    detail.xy *= _Depth;
    half3 tangent = normalize(i.tangentWS.xyz);
    n = normalize(TransformTangentToWorld(normalize(detail), half3x3(tangent, cross(n, tangent) * i.tangentWS.w, n)));
    #endif
    #if defined(GOLF_CRATER)
    half3 detail = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
    half3 tangent = normalize(i.tangentWS.xyz);
    n = normalize(TransformTangentToWorld(detail, half3x3(tangent, cross(n, tangent) * i.tangentWS.w, n)));
    #endif
    half3 albedo = GolfLegacyAlbedo(i, n);
    Light light = HeroMain(GetMainLight(TransformWorldToShadowCoord(i.positionWS)));
    half wrap = .25;
    #if defined(GOLF_HERO) || defined(GOLF_CLAY) || defined(GOLF_HAIR)
    wrap = _Wrap;
    #endif
    half3 lighting = HeroAmbient(n) + light.color * HeroWrapped(n, light.direction, wrap) * light.shadowAttenuation;
    #ifdef _ADDITIONAL_LIGHTS
    for (uint index = 0; index < GetAdditionalLightsCount(); index++)
    {
        Light extra = GetAdditionalLight(index, i.positionWS);
        lighting += extra.color * HeroWrapped(n, extra.direction, wrap) * extra.distanceAttenuation * extra.shadowAttenuation;
    }
    #endif
    half3 colour = albedo * lighting;
    #if defined(GOLF_HERO) || defined(GOLF_CLAY)
    half3 view = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
    half3 reflected;
    // Keep the semantic old material API; replace its diffuse-only clay response.
    half3 direct = HeroCharacterShade(light, n, view, albedo, wrap, _SurfaceSmoothness, _SurfaceWarmth.rgb, _SurfaceSpecular, reflected);
    half3 diffuse = albedo * light.color * HeroWrapped(n, light.direction, wrap) * light.shadowAttenuation;
    colour += direct - diffuse;
    half graze = pow(1 - saturate(dot(n, view)), 4) * _SurfaceSheen;
    colour += sqrt(max(albedo, 0)) * graze * saturate(dot(n, light.direction)) * light.color * light.shadowAttenuation * .12h;
    #endif
    #if defined(GOLF_BALL)
    half3 view = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
    colour += light.color * pow(saturate(dot(n, SafeNormalize(light.direction + view))), 64) * .25 * light.shadowAttenuation;
    #endif
    #if defined(GOLF_CRATER)
    half glow = SAMPLE_TEXTURE2D(_Glow, sampler_Glow, i.uv).r;
    colour += _GlowColor.rgb * glow * _GlowAmount * (.86 + .24 * sin(_Time.y * 1.3 + i.positionWS.x * .011 + i.positionWS.z * .007));
    #endif
    return half4(MixFog(colour, i.fog), 1);
}

float3 _LightDirection, _LightPosition;
GolfVaryings GolfLegacyShadowVertex(GolfAttributes a)
{
    GolfVaryings o = GolfLegacyVertex(a);
    float3 direction = _LightDirection;
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    direction = normalize(_LightPosition - o.positionWS);
    #endif
    o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS, o.normalWS, direction));
    #if UNITY_REVERSED_Z
    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
    #else
    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE * o.positionCS.w);
    #endif
    return o;
}
half4 GolfLegacyShadowFragment(GolfVaryings i) : SV_Target
{
    GolfLegacyAlbedo(i, normalize(i.normalWS));
    return 0;
}
