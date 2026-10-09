# Production goal — active

The production objective remains the complete V6 brief and customization contract. The initial `v6_proof/GATE_RESULTS.md` records the first rejected attempt. It is not a completion report for ongoing goal work.

## Foundation work completed in this goal turn

- Measured source topology: the head has a branched, torn neck boundary; source body contains disconnected torso/leg/foundation islands. Existing face features are retained.
- Built a new neck from the head cross-section, preserving all head coordinates above 1.30m. Authored head component now has **zero boundary edges**.
- Preserved original triangle winding for retained skin faces after global normal recalculation reversed original open arm surfaces. No vertex movement in the winding repair.
- Authored standard `Knee_L/R_60/110` morphs from Blender's volume-preserving deformation. At the two authoring poses, corrected linear skinning reconstructs the target to less than 0.0002 mm in the knee core. These are pose-space morphs, not Unity dual-quaternion skinning. Unity applies them after animation.
- Added a wardrobe-change event to the existing ModularHeroLook path. The V6 adapter refreshes aliases/LODs after direct Equip, without manual external calls.
- Added V6 shorts tint/reset support, owned material reuse, one reusable kit atlas, and reuse of unchanged headwear. Production regression checks compare direct swaps, color reset, and 25 repeated applies.
- Removed the obsolete runtime head filler on V6 only after verifying the authored head is closed.

## Evidence and worksets

- Authoring: `Hero_V6_Polish.blend`; checkpoint before these repairs: `checkpoints/pre-foundation.blend`.
- Source inspection: `foundation-inspection.json`, `neck-boundary-loops.json`.
- Construction: `neck-repair.json`, `coverage-repair.json`, `surface-winding.json`, `knee-correctives.json`.
- Latest Unity review: `../v6_proof/foundation4/Unity/`.
- Earlier intermediate captures kept under `foundation1/`, `foundation2/`, `foundation3/`; some expose now-addressed regressions.
- Shading diagnostic: `../v6_proof/shading_diagnostic/`. Uniform skin removes the original painted brow/mouth detail, proving those must be preserved while cleaning the atlas. Diagnostic materials were not saved into the candidate.

## Remaining production work

1. Complete a continuous, clean skin foundation under outfits: torso/pelvis/arm joins, wrist and ankle transitions. Existing hidden clothing-derived fragments are not an acceptable full-body source. Preserve face identity, finger rig, joint locations and existing clips.
2. Resolve shoulder/polo deformation and full procedural gameplay dive clearance. Current proof does not pass those gates.
3. Refine hair/visor geometry to the authority references; verify solid scalp/hair from all camera angles and headwear combinations.
4. Clean face/cloth albedo and normal maps while preserving painted eyebrows, mouth, iris identity. Verify eyes/lids in actual runtime, not only a rest render.
5. Finish non-overlapping UVs, mobile textures and all wardrobe/body LODs. Count complete runtime geometry and profile the target phone.
6. Complete customization range, all clip stress tests, actual gameplay dive, real Inspector and final proof pack. Promote only after all requirements are verified.

No goal-complete claim. No visual gate has been upgraded based solely on import, finite-motion or closed-edge checks.

## Latest verified checkpoint

Foundation4 Unity capture completed without compiler errors or runtime exceptions. All nine clips have finite skinning/motion in both shape modes. Direct Hair Equip retains its alias and LOD hooks. Twenty-five repeated Locked style applies reuse the same atlas and retain 14 owned kit materials. Blue shorts override and navy reset are captured. Original arm winding is restored and surfaces render again. Actual visible geometry: **52,809 triangles**, still above the target.

The bare-head capture still shows ragged collar/neck presentation: closing the head surface did not fix the adjacent shirt boundary or make the whole body a clean foundation. Next repair must address those surfaces together. No overall visual gate is marked passed.
