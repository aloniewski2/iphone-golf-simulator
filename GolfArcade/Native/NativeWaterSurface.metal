#include <metal_stdlib>
#include <RealityKit/RealityKit.h>
using namespace metal;

// Minimal control for renderer-pipeline diagnostics, with no game data, texture,
// geometry modifier or custom uniforms. Used only by the rendering regression.
[[visible]] void nativeMaterialPipelineControl(realitykit::surface_parameters params) {
    params.surface().set_base_color(half3(0.1, 0.5, 0.2));
    params.surface().set_roughness(0.8);
}

inline float3 applyNativeWater(realitykit::surface_parameters params) {
    const float4 state = params.uniforms().custom_parameter();
    const float2 p = params.geometry().world_position().xz / 0.9144;
    const float a = dot(p, float2(0.75, 0.31)) + state.x * 0.70;
    const float b = dot(p, float2(-0.35, 1.15)) - state.x * 0.52;
    const float c = dot(p, float2(1.8, -1.2)) + state.x * 0.91;
    const float2 slope = 0.035 * cos(a) * float2(0.75, 0.31)
                       + 0.018 * cos(b) * float2(-0.35, 1.15)
                       + 0.007 * cos(c) * float2(1.8, -1.2);
    // Surface normals use tangent space. Transform the world-anchored gradient
    // through the imported mesh's basis, including its yards-to-metres parent.
    const float4x4 transform = params.uniforms().model_to_world();
    const float3 t = normalize((transform * float4(params.geometry().tangent(), 0)).xyz);
    const float3 baxis = normalize((transform * float4(params.geometry().bitangent(), 0)).xyz);
    const float3 n = normalize((transform * float4(params.geometry().normal(), 0)).xyz);
    const float3 normal = normalize(float3(-slope.x, 1, -slope.y));
    params.surface().set_normal(float3(dot(normal, t), dot(normal, baxis), dot(normal, n)));
    constexpr sampler fieldSampler(coord::normalized, address::clamp_to_edge, filter::linear);
    const float shallow = params.textures().custom().sample(fieldSampler, (p - state.yz) * state.w).r;
    const half3 color = mix(half3(0.025, 0.27, 0.31), half3(0.13, 0.47, 0.39), half(shallow));
    params.surface().set_base_color(color * half(0.99 + 0.01 * sin(a) * cos(b)));
    params.surface().set_roughness(0.19);
    params.surface().set_metallic(0.05);
    return normal;
}

[[visible]] void nativeWaterSurface(realitykit::surface_parameters params) {
    applyNativeWater(params);
}

// Matches the two SIMD4<Float> fields in Swift; verified by a layout regression.
struct NativeLagoonUniforms {
    float4 center;
    float4 extent;
};

[[stitchable]] void nativeLagoonSurface(realitykit::surface_parameters params,
                                      constant NativeLagoonUniforms &lagoon) {
    const float3 normal = applyNativeWater(params);
    const float3 view = normalize(params.geometry().view_direction());
    const float3 skyDirection = reflect(-view, normal);
    const float3 position = params.geometry().world_position() / 0.9144;
    const float3 ray = select(float3(-1), float3(1), skyDirection >= 0) * max(abs(skyDirection), float3(0.0001));
    const float3 farPlane = lagoon.center.xyz + select(-lagoon.extent.xyz, lagoon.extent.xyz, ray > 0);
    const float3 distances = (farPlane - position) / ray;
    const float distance = max(0.0, min(distances.x, min(distances.y, distances.z)));
    const float3 direction = position + skyDirection * distance - lagoon.center.xyz;
    const float3 magnitude = max(abs(direction), float3(0.0001));
    float face;
    float2 uv;
    if (magnitude.x >= magnitude.y && magnitude.x >= magnitude.z) {
        face = direction.x >= 0 ? 0 : 1;
        uv = float2(direction.x >= 0 ? -direction.z : direction.z, -direction.y) / magnitude.x;
    } else if (magnitude.y >= magnitude.z) {
        face = direction.y >= 0 ? 2 : 3;
        uv = float2(direction.x, direction.y >= 0 ? direction.z : -direction.z) / magnitude.y;
    } else {
        face = direction.z >= 0 ? 4 : 5;
        uv = float2(direction.z >= 0 ? direction.x : -direction.x, -direction.y) / magnitude.z;
    }
    uv = clamp(uv * 0.5 + 0.5, float2(0.5 / 256.0), float2(1 - 0.5 / 256.0));
    uv.x = (face + uv.x) / 6;
    constexpr sampler atlasSampler(coord::normalized, address::clamp_to_edge, filter::linear);
    const half4 captured = params.textures().base_color().sample(atlasSampler, uv);
    const float zenith = pow(max(0.0, skyDirection.y), 0.45);
    const half3 sky = half3(0.86 - 0.46 * zenith, 0.89 - 0.23 * zenith, 0.89 + 0.03 * zenith);
    const half3 reflected = mix(sky * sky, captured.rgb, captured.a) * half3(0.88, 1, 0.96);
    const half fresnel = half((0.08 + 0.64 * pow(1 - saturate(dot(normal, view)), 2.0)) * lagoon.center.w);
    // Captured radiance must not be lit a second time. Retain native PBR waves and
    // sun highlights, adding only the water's camera-dependent reflected radiance.
    params.surface().set_base_color(params.surface().base_color() * (1 - fresnel));
    params.surface().set_emissive_color(reflected * fresnel);
}
