#ifndef GOLF_ARCADE_HERO_LIGHTING_INCLUDED
#define GOLF_ARCADE_HERO_LIGHTING_INCLUDED
// Profile 0 is the unchanged tennis lighting path. Golf uses the same surface
// functions, actual course lights/shadows and a hue-preserving highlight knee.
float _HeroProfile;
half4 _HeroKeyColor, _HeroAmbientSky, _HeroAmbientEquator, _HeroAmbientGround;
half _HeroKeyStrength, _HeroRim, _HeroExposure, _HeroShoulderKnee, _HeroShoulderCeiling;
half3 _HeroClothBalance;
half _HeroClothExposure;
Light HeroMain(Light light)
{
    if (_HeroProfile > .5) light.color = _HeroKeyColor.rgb * _HeroKeyStrength;
    return light;
}
half3 HeroAmbient(half3 n)
{
    // Use the actual scene's published SH in both profiles, so a golfer and
    // neighbouring course props receive the same sky/ground ambient energy.
    // GolfAtmosphere still owns the scene sky/equator/ground look values.
    return SampleSH(n);
}
half3 HeroHighlightRoll(half3 c,half knee,half ceiling)
{
    half peak=max(max(c.r,c.g),max(c.b,1e-4h));
    half span=max(ceiling-knee,.001h);
    half excess=max(peak-knee,0);
    // A late rational shoulder preserves highlight contrast instead of rapidly
    // flattening every bright surface to a single value.
    half rolled=peak<=knee ? peak : knee+span*excess/(span+excess);
    return c*(rolled/peak);
}
half3 HeroFinish(half3 c)
{
    if (_HeroProfile < .5) return c;
    return HeroHighlightRoll(c*_HeroExposure,_HeroShoulderKnee,_HeroShoulderCeiling);
}
half3 HeroClothFinish(half3 c)
{
    if (_HeroProfile < .5) return c;
    return HeroHighlightRoll(c*.945h,.64h,.88h);
}
half3 HeroCharacterFinish(half3 c,half3 reflection)
{
    if (_HeroProfile < .5) return c;
    half3 base=HeroHighlightRoll(max(c-reflection,0)*(.95h*_HeroExposure),.70h,.90h);
    half3 room=max(.985h-base,.001h);
    // Retain the authored specular/rim shape after diffuse highlight handling.
    // Each channel approaches its remaining display headroom without clipping.
    reflection=max(reflection,0)*_HeroExposure;
    return base+reflection*room/(room+reflection);
}
half HeroWrapped(half3 n,half3 direction,half wrap)
{ return saturate((dot(n,direction)+wrap)/(1+wrap)); }
half3 HeroCharacterShade (Light light, half3 n, half3 v, half3 albedo, half _Wrap, half _Smoothness, half3 _Subsurface, out half3 reflection)
            {
                half ndl = dot(n, light.direction);
                half wrapped = saturate((ndl + _Wrap) / (1 + _Wrap));
                // Warmth where light wraps past the edge: reads as soft skin rather than plastic.
                half edge = saturate(wrapped - saturate(ndl)) * 2;
                half3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), _Smoothness * 96 + 6) * _Smoothness * 0.6;
                half atten = light.distanceAttenuation * light.shadowAttenuation;
                reflection=spec*light.color*atten;
                return (albedo * wrapped + _Subsurface * edge * albedo + spec) * light.color * atten;
            }
#endif
