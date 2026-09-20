#include <metal_stdlib>
#include <RealityKit/RealityKit.h>
using namespace metal;

// Static surface samples move entirely on the GPU. Time comes from GameSession,
// not the renderer's independent clock, so pause and replay remain deterministic.
[[visible]] void nativeGreenReadMotion(realitykit::geometry_parameters params) {
    const float4 state = params.uniforms().custom_parameter();
    const float2 flow = params.geometry().uv0();
    const float2 curve = params.geometry().uv1();
    const float speed = params.geometry().uv2().x;
    const float travel = (fract(state.x * speed * 0.5) * 2.0 - 1.0) * state.y;
    params.geometry().set_model_position_offset(float3(flow.x * travel,
        curve.x * travel + curve.y * travel * travel, flow.y * travel) * state.z);
}

[[visible]] void nativeGreenReadSurface(realitykit::surface_parameters params) {
    const float slope = params.geometry().uv2().y;
    const uint bucket = min(4u, uint(max(0.0, slope) / 0.01));
    const half4 colors[5] = { half4(0.78, 1, 0.9, 0.85), half4(1, 0.95, 0.55, 0.9),
        half4(1, 0.78, 0.3, 0.95), half4(1, 0.55, 0.2, 1), half4(1, 0.3, 0.25, 1) };
    params.surface().set_base_color(colors[bucket].rgb);
    params.surface().set_emissive_color(colors[bucket].rgb);
    params.surface().set_opacity(colors[bucket].a);
}
