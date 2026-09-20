# Local lagoon reflection captures

Generated offline from the actual authored Sunward Resort course by `SunwardUpgradeTests.testOptInBakeLagoonReflections` (GOLF_BAKE_REFLECTIONS=1). These are rendered game assets, not Higgsfield concepts. Re-bake after changing terrain, scenery or lighting.

Six 256px faces per water-bearing hole, at the lake center 0.05 yards above the authoritative water elevation. Face order +X, -X, +Y, -Y, +Z, -Z. Captures are horizontally flipped for the explicit atlas projection. Gameplay overlays, avatars and balls are excluded by capturing a separate static scene. Base water is used during baking to prevent reflection feedback. Sky pixels are transparent; runtime computes a continuous sky gradient, avoiding baked sky-color seams.

Runtime assembles a 1536×256 RGBA atlas on hole load and uses one water-only texture sample and local box projection; no per-frame capture or global environment replacement. Static scenery only: avatars, balls and tree breeze are not dynamically reflected. Physics and lake heights are unchanged. Unsupported/custom holes fall back to the ordinary water material.

## Native RealityKit consumer

`NativeWaterSurface` now assembles these same six faces on a worker and uploads the atlas
as a color texture during asynchronous hole loading. `nativeLagoonSurface` uses a
camera-relative reflected ray, the original yard-space capture center/box, and explicit
face projection. Typed custom uniforms carry two aligned float4 values; the existing
session-time/depth-field channel remains unchanged. Reflected radiance is added as emission
so the baked lighting is not lit a second time. Non-water materials and global illumination
are not modified. Missing required resort captures fail loading explicitly; modified/custom
holes use ordinary water rather than an unrelated capture.

The native atlas can still need re-baking as converted scenery/lighting reaches final visual
parity. These static source-renderer captures are not proof of dynamic reflections or complete
native visual parity. `testNativeLagoonCaptureContract` validates capture coverage and uniform
layout, and `testNativeLagoonReflectionChangesWaterOnly` compares actual rendered water against
unchanged sky/terrain. Run `testNativeResortLagoonReviewCaptures` with `GOLF_LAGOON_REVIEW=1` for
all six native lake review images.

## Capture checkpoint

September 19, 2026, simulator export result `19:56:39 / db7d1d96` (36 faces), including revised crown assets and clustered backdrop groves. Course data SHA256 `91f30bc801413644f95e432d0048cb55089e83b5b760daceeed58ce11913772d`; CourseScene source SHA256 `7691fb1ac2a78286016c639f11f4118a028e9d2dae4ee386a8433434535c69e4`; CourseSurfaceDetail source SHA256 `6355cbe4a9955fbadf676f163d3415f76ee2f94a5fddb4114f62e07ffc59b2b5`; tree importer SHA256 `3c3d1a18cbdef884d91bac3b80d769faccc0f476f46cdb970cefbf556a4ad5f7`. Rebuild these derived images when scenery or lighting changes; the runtime hole equality guard is not a source-code invalidation mechanism.
