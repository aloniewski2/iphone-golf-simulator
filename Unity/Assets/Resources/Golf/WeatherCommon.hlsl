// What the weather's shaders share: cheap value noise in 2D and 3D and their fractal sums, the
// world under a pixel from the camera's depth, and the flash of lightning (set by WeatherView as
// _GolfWeatherFlash, a global of the weather's own: no other shader reads it).
#ifndef GOLF_WEATHER_COMMON
#define GOLF_WEATHER_COMMON

float _GolfWeatherFlash;
float4 _GolfWeatherFlashColor;

float WeatherHash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
float WeatherHash3(float3 p) { p = frac(p * float3(0.1031, 0.1030, 0.0973)); p += dot(p, p.yxz + 33.33); return frac((p.x + p.y) * p.z); }

float WeatherNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3 - 2 * f);
    return lerp(lerp(WeatherHash(i), WeatherHash(i + float2(1, 0)), u.x), lerp(WeatherHash(i + float2(0, 1)), WeatherHash(i + float2(1, 1)), u.x), u.y);
}

float WeatherNoise3(float3 p)
{
    float3 i = floor(p), f = frac(p);
    float3 u = f * f * (3 - 2 * f);
    float a = lerp(WeatherHash3(i), WeatherHash3(i + float3(1, 0, 0)), u.x);
    float b = lerp(WeatherHash3(i + float3(0, 1, 0)), WeatherHash3(i + float3(1, 1, 0)), u.x);
    float c = lerp(WeatherHash3(i + float3(0, 0, 1)), WeatherHash3(i + float3(1, 0, 1)), u.x);
    float d = lerp(WeatherHash3(i + float3(0, 1, 1)), WeatherHash3(i + float3(1, 1, 1)), u.x);
    return lerp(lerp(a, b, u.y), lerp(c, d, u.y), u.z);
}

/// Fractal value noise, `octaves` deep (at most 5), roughly 0–1.
float WeatherFbm(float2 p, int octaves)
{
    float v = 0, a = 0.5;
    [unroll(5)] for (int k = 0; k < octaves; k++) { v += a * WeatherNoise(p); p = p * 2.03 + float2(17.1, 9.3); a *= 0.5; }
    return v / (1 - pow(0.5, octaves));
}

float WeatherFbm3(float3 p)
{
    float v = 0.5 * WeatherNoise3(p);
    v += 0.25 * WeatherNoise3(p * 2.07 + 11.3);
    v += 0.125 * WeatherNoise3(p * 4.11 + 23.7);
    return v / 0.875;
}

#endif
