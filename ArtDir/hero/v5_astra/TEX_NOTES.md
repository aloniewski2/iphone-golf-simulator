# Astra texture experiment — T0

Read `PLAN_HeroV5_AstraTextureSidePass.md`. The untouched starting hero is `Hero_V5_AstraTex_Baseline.blend`. `baseline/` contains front, three-quarter, side, back and hair-close-up Blender renders, `blender-audit.json`, and the original Unity binding audit.

The newer user instruction required structural integrity repairs before texture work. Those fixes are documented separately in `../integrity_v1/INTEGRITY.md` and were applied to production after Blender/Unity/native checks. The Unity assets under `Assets/ArtDirection/HeroV5AstraTex/` became the isolated structural review sandbox; they are not a generated texture look. The archived baseline blend remains unchanged.

## UV gate

**Full-body projection bake is not ready.** Body_Skin has 38,300 triangles, of which 28,404 have zero-area UVs. Many of these belong to flat-color procedural surfaces (including the reconstructed skull), so the current material can render, but new image projection cannot be baked there without a suitable unwrap. No UVs contain nonfinite values. Eye pole triangles account for 120 degenerate UV triangles per eye. Clothing has usable atlas regions; do not overwrite their existing seams without isolating a new bake channel.

The repaired hair has new `HairUV` layouts, but those are unpainted and are not T1 projections. No ImageGen projections or texture bakes have been generated. This follows the plan's T0 instruction to stop if UVs are unusable. The next texture work needs an independent bake UV channel for the flat-UV body regions, validation of atlas separation, then strict reference projections for review.

## Structural baseline

The original avatar was valid Humanoid, the shared rig had 42 bones, and source/prefab bone order matched. The real problems were open hair shells, the older deliberate leg-volume enlargement, knee falloff and the missing visible-Hero size wiring. See the structural report for measured corrections and final native proof.
