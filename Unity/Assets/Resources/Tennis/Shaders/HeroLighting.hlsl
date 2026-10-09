#ifndef GOLF_ARCADE_HERO_LIGHTING_INCLUDED
#define GOLF_ARCADE_HERO_LIGHTING_INCLUDED
// Both sports preserve display headroom under their actual scene lights. Golf
// additionally publishes its profile exposure and warm course key parameters.
float _HeroProfile;
float _HeroFilmResponse;
half4 _HeroKeyColor, _HeroAmbientSky, _HeroAmbientEquator, _HeroAmbientGround;
half _HeroKeyStrength, _HeroRim, _HeroExposure, _HeroShoulderKnee, _HeroShoulderCeiling;
half3 _HeroClothBalance;
half _HeroClothExposure;
half _HeroPresentationFill;
half _HeroSkinPolish;
// A broad camera-side reflector for character surfaces. It consumes no URP
// per-object light slot and never changes the course, court, or ball lighting.
Light HeroPresentationLight(half3 viewDirection)
{
    half3 right = SafeNormalize(cross(half3(0,1,0),viewDirection));
    Light light = (Light)0;
    light.direction = SafeNormalize(viewDirection*.80h + half3(0,.60h,0) - right*.30h);
    light.color = half3(1,.95h,.88h)*_HeroPresentationFill;
    light.distanceAttenuation = 1;
    light.shadowAttenuation = 1;
    return light;
}
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
    if (_HeroFilmResponse > .5 || _HeroProfile < .5) return c;
    return HeroHighlightRoll(c*_HeroExposure,_HeroShoulderKnee,_HeroShoulderCeiling);
}
half3 HeroClothFinish(half3 c)
{
    if (_HeroFilmResponse > .5) return c;
    if (_HeroProfile < .5) return HeroHighlightRoll(c*.98h,.72h,.96h);
    return HeroHighlightRoll(c*.945h,.64h,.88h);
}
half3 HeroCharacterFinish(half3 c,half3 reflection)
{
    if (_HeroFilmResponse > .5) return c;
    half exposure=_HeroProfile > .5 ? _HeroExposure : 1.0h;
    half3 base=_HeroProfile > .5 ? HeroHighlightRoll(max(c-reflection,0)*(.95h*exposure),.70h,.90h) : HeroHighlightRoll(max(c-reflection,0),.76h,.96h);
    half3 room=max(.985h-base,.001h);
    // Retain the authored specular/rim shape after diffuse highlight handling.
    // Each channel approaches its remaining display headroom without clipping.
    reflection=max(reflection,0)*exposure;
    return base+reflection*room/(room+reflection);
}
half HeroWrapped(half3 n,half3 direction,half wrap)
{ return saturate((dot(n,direction)+wrap)/(1+wrap)); }
// A normalized broad lobe for skin plus a narrower varnish lobe for smooth eyes/equipment.
// Grazing reflectance and direct-light visibility prevent the flat clay/plastic silhouette.
half3 HeroCharacterShade (Light light, half3 n, half3 v, half3 albedo, half wrap, half smoothness, half3 subsurface, half specularStrength, out half3 reflection)
{
    half ndl = dot(n, light.direction), nl = saturate(ndl);
    half wrapped = saturate((ndl + wrap) / (1 + wrap));
    half edge = saturate(wrapped - nl) * 1.35h;
    half3 h = SafeNormalize(light.direction + v);
    half nh = saturate(dot(n, h));
    half broad = pow(nh, 10 + smoothness * 30) * .045h;
    half varnish = pow(nh, 24 + smoothness * smoothness * 200) * smoothness * .25h;
    half fresnel = 1 + pow(1 - saturate(dot(v, h)), 5) * 1.5h;
    half spec = (broad + varnish) * specularStrength * fresnel * saturate(ndl * 3);
    half atten = light.distanceAttenuation * light.shadowAttenuation;
    reflection = spec * light.color * atten;
    return (albedo * wrapped + subsurface * edge * albedo) * light.color * atten + reflection;
}
// Work-only skin response, selected per Body material. Other character roles
// retain HeroCharacterShade. Keep direct/fill energy bounded and the skin lobe
// broad, rather than using the narrow varnish contribution of eyes/equipment.
half3 HeroSkinShade (Light light, half3 n, half3 v, half3 albedo, half wrap, half smoothness, half3 subsurface, half specularStrength, out half3 reflection)
{
    half ndl = dot(n, light.direction);
    half wrapped = saturate((ndl + wrap) / (1 + wrap));
    half edge = saturate(wrapped - saturate(ndl));
    half3 h = SafeNormalize(light.direction + v);
    half nh = saturate(dot(n, h));
    half broad = pow(nh, 18 + smoothness * 18) * .075h;
    if (_HeroSkinPolish > .5h) {
        // Softbox skin: concentrate the broad wash into a visible satin core.
        // Lower integrated reflection energy preserves the side-plane colour;
        // the soft shoulder keeps the highlight transition from becoming oily.
        // Rougher lips/neck remain below the forehead/nose response.
        half glossWeight = .16h + .84h * saturate((smoothness - .32h) / .06h);
        broad = (pow(nh, 5 + smoothness * 6) * .035h
               + pow(nh, 18 + smoothness * 18) * .40h) * glossWeight;
    }
    half visibility = smoothstep(-.06h, .24h, ndl);
    half shadow = lerp(light.shadowAttenuation, 1, .12h);
    half atten = light.distanceAttenuation * shadow;
    reflection = broad * specularStrength * visibility * light.color * atten;
    return albedo * (wrapped * .84h + subsurface * edge) * light.color * atten + reflection;
}
#endif
