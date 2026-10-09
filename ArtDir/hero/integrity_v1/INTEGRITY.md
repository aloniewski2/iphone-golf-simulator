# Character integrity repair — 2026-09-29

> Archived experiment: Adnan requested a return to original Hero V5 on 2026-09-29. Production body/hair/clothing/prefabs and native previews have been restored; this document and the integrity renders describe the superseded repair. See `ArtDir/hero/v5/RESTORE_V5.md`.

Implemented in the current `newmapsandmenus` working tree. The current authoring source is `ArtDir/hero/v5/Hero_01_V5.blend`; `Hero_Integrity_v1.blend` is the review copy. The unchanged starting mesh is archived in `ArtDir/hero/v5_astra/Hero_V5_AstraTex_Baseline.blend`, and the replaced production FBXs/prefabs are in `before/assets/`.

## Causes and repairs

| Issue | Evidence | Change |
|---|---|---|
| Broken hair surfaces | After welding coincident vertices, the five sculpted styles had 7,104–9,380 boundary edges. Two-sided rendering concealed some holes without closing the mesh. | Closed and consolidated all five cuts and their no-hat variants in Blender; smooth outward normals, new hair UVs, normalized weights, opaque rendering. All ten repaired meshes have zero boundary edges. 6,000 triangles per Swept/Bob/Curly variant; 7,000 per Ponytail/Long variant. |
| Oversized legs | The earlier `v5_tools/build_body_v5.py` enlarged thighs/calf cross-sections by 15–18%, plus a rear calf bulge. | Radial correction around the existing bone chains; matching shorts and Female shape corrections. Ankle/foot and hip transitions taper to zero correction. |
| Knee collapse | The knee blend was concentrated at the hinge with insufficient front-cap retention. | Broader, smooth UpperLeg/LowerLeg falloff, with a modest thigh bias on the front of the kneecap. Edited 923 left / 913 right body vertices and 61 left / 59 right shorts vertices. Four skin influences retained. Unity uses linear skinning; this does not claim Dual Quaternion support. |
| Size slider did nothing on Hero01 | `bodySize` reached the hidden legacy actor, but neither the visible HeroKit style nor HeroV4 native preview used it. | `BuildSlim` / `BuildBroad` shapes on Body_Skin, Shirt_Default and Shorts_Default; shared ±16% radial field, default at 50%. Head, hands, feet, rest bones and sockets stay fixed. Native launch now forwards the selected build to the visible hero. |
| Wardrobe/preview mismatch | The phone renders separate baked geometry; changing the Unity FBX alone does not update it. | Re-baked locker, menu idle, eight loading swing targets and the seated menu pose. Exported pose-specific build deltas so size persists through preview motion. |
| Skin/hair tint joins | The side-hair material was borrowed without a distinct tint identity, and Bald inherited hair-colored scalp tint. | Separate side-hair tint identity; bald scalp takes skin tint. Added a skinned neck join with Head/Neck/Chest weights and a tintable skin material. |
| Future bad bone swaps | Cosmetic binding previously relied on the first matching name. | Validate mesh/bind-pose count and require every source bone name to resolve exactly once before staging a replacement. |

## Measured cross-sections

Measurements are Blender rest-pose world-space widths on the left leg, using an ±8 mm height band. They are not screen-space estimates.

| Region / height | Before width | After width | Ratio |
|---|---:|---:|---:|
| Lower calf / 0.30 m | 0.17435 m | 0.15155 m | 86.9% |
| Calf maximum / 0.37 m | 0.19519 m | 0.16207 m | 83.0% |
| Knee / 0.44 m | 0.18055 m | 0.16238 m | 89.9% |
| Thigh / 0.53 m | 0.17174 m | 0.15459 m | 90.0% |
| Shorts / 0.53 m | 0.20711 m | 0.18652 m | 90.1% |

Raw values and mesh counts: `repair-report.json`.

## Skeleton and motion

- The initial audit found a valid Humanoid Avatar, a shared 42-bone rig (22 Humanoid mappings plus finger bones), zero missing bones, and matching source bone order. It did not support replacing the skeleton.
- Blender rest matrices and hierarchy were asserted unchanged by the repair script. No tennis animation curves, grip rotations, racket socket, timing/contact rules or gameplay stats were rewritten.
- All 17 `Hero_*_v5.fbx` animation files have identical before/after SHA-256 hashes (`gameplay-clip-hashes-*.json`).
- Production prefab bone lists and bind-pose counts validate, weights normalize to 1, all wardrobe bones remain under the same skeleton, and four influences are used.
- An initial capture showed apparent head detachment because the editor captured a newly enabled haircut before the body's GPU skinning updated. The review tool now waits for the following rendered frame. Those diagnostic shots were rejected; use `production/` for final evidence.

## Verification and evidence

- `production/motion-and-swaps.txt`: full Ready, Serve, Forehand, Backhand, RunForward, RunRight, RunLeft, Volley and Smash clips sampled at 60 Hz on male and female shapes. No frozen slots, nonfinite joints or exploding body vertices. Rendered four time points per clip.
- Eight hair selections × four headwear selections: exactly one haircut renderer enabled; six male/female × slim/default/broad review poses captured.
- Xcode simulator: `testHeroIntegrityCustomizationAndSize` and `testHeroIntegrityPhonePreview` passed. Forty offered hair/headwear/body combinations were checked; body/clothing size deltas differ, and the loading swing retains the chosen build. Seated geometry remains finite.
- Unity Play Mode: `NativeLaunchHeroTests.EveryRivalAndThePlayerKeepTheHeroStandard` passed. Player cosmetics survive character rebuilds; all campaign rivals retain exactly one Hero01 visual each.
- `native/`: actual phone preview renderer captures, including default, no hat, ponytail, bob, slim and broad.
- `baseline/` versus `production/`: same camera/light review captures before and after. These are isolated character proofs, not gameplay lighting targets.

## Limits and maintenance

This is a structural repair, not a claim that the look matches the production plate perfectly. Some sculpted hair clumps remain angular, the no-hat source contains a smooth band-shaped section, and the existing visor/skin/shoe textures still need the separate look pass. Short scalp styles remain hidden in the native menu under its existing `HeadRebuild.shipped` gate. Physical-device performance was not measured and no new phone build was installed in this task.

Re-export from the updated canonical blend. Do not re-run historical V5 leg-enlargement or Tripo build steps on this corrected source; they would compound the old proportions. `v5_tools/export_v5.py` now also preserves the neck join. Preserve asset GUIDs and rebind by source bone order when adding a mesh. Re-run HeroLockerExport for every geometry change, then package menu/loading and lounge previews; their manifests and binary layouts must come from the same export.
