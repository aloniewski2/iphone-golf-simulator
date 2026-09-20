#include <metal_stdlib>
#include <RealityKit/RealityKit.h>
using namespace metal;

[[visible]] void nativeCourseVertexColorSurface(realitykit::surface_parameters params) {
    // USD displayColor is preserved in the mesh but is not multiplied into the
    // imported white PBR material automatically. Keep the authored gradient.
    params.surface().set_base_color(half3(params.geometry().color().rgb)
        * half3(params.material_constants().base_color_tint()));
    params.surface().set_roughness(half(params.material_constants().roughness_scale()));
    params.surface().set_metallic(0);
    params.surface().set_normal(float3(0, 0, params.geometry().normal().y < 0 ? -1.0 : 1.0));
}

[[visible]] void nativeGrassMotion(realitykit::geometry_parameters params) {
    const float4 state = params.uniforms().custom_parameter();
    const float3 p = params.geometry().model_position();
    const float2 authored = params.geometry().uv0();
    // SceneKit's USD exporter flips V: these are blade angle and height, not UVs.
    const float height = max(0.0, 1.0 - authored.y);
    const float distance = length((params.uniforms().model_to_view() * float4(p, 1)).xyz) / 0.9144;
    const float fade = 1.0 - smoothstep(24.0, 52.0, distance);
    const float ballDistance = length(params.geometry().world_position().xz - state.yw) / 0.9144;
    const float clear = smoothstep(0.45, 0.85, ballDistance);
    const float breeze = sin(p.x * 0.45 + p.z * 0.29 + state.x * 1.5);
    params.geometry().set_model_position_offset(float3(breeze * height * 0.2 * fade * clear,
        -height * (1.0 - fade * clear) - 0.08 * (1.0 - fade) - 0.01 * (1.0 - clear), 0));
    params.geometry().set_uv0(float2(p.x, -p.z) / 6.0);
    params.geometry().set_custom_attribute(float4(height, fade, authored.x, 0));
    // The exported blade parameters are degenerate as UVs. Supply a stable
    // normal/basis rather than using tangents derived from angle/height data.
    float3 leaf = float3(-sin(authored.x), 0, cos(authored.x));
    const float3 viewLeaf = (params.uniforms().model_to_view() * float4(leaf, 0)).xyz;
    const float3 toView = -(params.uniforms().model_to_view() * float4(p, 1)).xyz;
    if (dot(viewLeaf, toView) < 0) leaf = -leaf;
    const float3 normal = normalize(params.geometry().normal() + leaf * 0.22 * fade);
    params.geometry().set_normal(normal);
    params.geometry().set_bitangent(normalize(cross(normal, float3(1, 0, 0))));
}

[[visible]] void nativeGrassSurface(realitykit::surface_parameters params) {
    const float4 blade = params.geometry().custom_attribute();
    constexpr sampler turfSampler(coord::normalized, address::repeat, filter::linear, mip_filter::linear);
    const half3 albedo = params.textures().base_color().sample(turfSampler, params.geometry().uv0()).rgb;
    const half3 tint = half3(params.material_constants().base_color_tint()) * half3(params.geometry().color().rgb);
    params.surface().set_base_color(albedo * tint);
    params.surface().set_roughness(0.88);
    params.surface().set_metallic(0);
    params.surface().set_ambient_occlusion(half(mix(1.0, 0.72 + 0.28 * saturate(blade.x * 12.0), blade.y)));
    // Double-sided leaf backfaces must still light from the supporting turf,
    // not the underside of the course.
    params.surface().set_normal(float3(0, 0, params.geometry().normal().y < 0 ? -1.0 : 1.0));
}
