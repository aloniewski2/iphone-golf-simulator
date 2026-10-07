# Golf course standard — implementation and validation

Implemented October 6, 2026 in the golf simulator’s existing `final-build` working tree, following the saved plan and the user’s instruction to proceed automatically. The rollout is enabled for all **13 selectable non-Postcard holes**. The procedural Meadow holes 1–3 also use the shared ground materials. Postcards 8–10 remain the reference.

[Open the before/after viewer](golf-course-review.html) · [Implementation plan](GOLF_ALL_COURSES_POSTCARD_STANDARD_PLAN.md) · [Course metrics](course-metrics.csv)

## Tree and palm repair follow-up

The tree repair is implemented in the existing golf working tree. [Open the close-up comparison](golf-tree-repair.html).

- Repaired **85 palms**: Volcano Rim (32), Temple Falls (38), Obsidian Slab (5), Ember Causeway (3), and Caldera Crown (7). Offset cylinder segments are replaced by continuous curved trunks; disconnected leaf blobs are replaced by attached, tapered fronds.
- Corrected vegetation colors: imported FBX diffuse values were being reused as shader colors and darkened again. Foliage, bark, tree wood, and coconuts now use the course's authored palette. Volcanic trees retain their olive and charred-brown theme.
- Added optional two-sided leaf lighting and modest light transmission, keeping fronds visible and shaded from below. Original Postcard rock-material defaults remain back-face culled with foliage lighting disabled.
- Retained the original tree meshes for obstacle extraction. New visual meshes are added after collision data is built and disposed with the hole. Jungle bushes on elevated cliffs retain their original geometry, including their shared palm-leaf material.

| Follow-up gate | Result | Evidence |
| --- | --- | --- |
| Palm geometry and vegetation rendering | **PASS** | 85 palms; five close-up pairs, five canopy-underside pairs, five tee pairs, and five aerial pairs. Live Metal rendering has no shader compilation errors. |
| Course regressions | **PASS** | The Play Mode comparison covers all 13 upgraded holes, unchanged terrain/lie sampling, hazards, obstacle values, collider counts, original tree-mesh identity, finite repair geometry, foliage settings, windmill animation, material cleanup, and Meadow 1–3. |
| Material routing checks | **PASS** | 18/18 focused Edit Mode tests. |
| Collision and obstacle capture hashes | **PASS** | All 16 captured holes match the prior release's collision geometry and obstacle hashes. |
| Protected authored assets | **PASS** | 106 protected files retain their baseline SHA-256, including the original course FBX/Blend models, shared Look/Surface textures, and Hole.cs. |
| Postcard controls | **PASS** | No geometry-inventory change on holes 8–10; tee-image mean absolute differences versus the prior release are 0.0392, 0.00464, and 0.000125 on a 0–255 scale. Animated effects remain subject to the capture limits below. |
| Device build/performance | **FAIL — still unverified** | No new iPhone build or device performance result. The earlier environment limits below still apply. |

The five affected tee views add **432–4,752 visible mesh triangles** each and **0–2 material passes** versus the prior release. These are camera mesh inventories, not GPU performance timings. The new palms have 792 triangles each, including both the trunk and fronds. Updated images for all 16 holes, four camera clips, and the current inventory CSV are included in the review.

Implementation: `GolfCoursePalms.cs`, vegetation routing in `GolfCourseLook.cs`/`HoleView.cs`, optional foliage lighting in `GolfRock.shader`/`GolfSurface.hlsl`, and expanded capture/regression checks. Before/after source and logs are under `work/golf-course-standard/tree-fix/`; full-resolution evidence is under `ArtDir/screenshots/golf_course_standard/tree_repair/`. This follow-up was applied locally; no commit, push, deployment, or app installation was performed.

## Delivered

- Shared runtime catalog for grass, green, fringe, rough, sand, cliff, rock, masonry, paths, water, surf, falls, basalt, lava, ash, snow, ice, desert, wood, bark, foliage, and painted props.
- The existing Postcard texture and shader library is reused. Eight new specialty surfaces provide 16 deterministic, seamless 512-pixel color/normal maps. iPhone import settings use ASTC 6×6; mipmaps, repeat sampling, and anisotropic filtering are enabled.
- World-scaled ground textures, steep-bank projection, broad mowing bands following the centerline, and optional triplanar normal detail for new rock materials. Postcard shader defaults retain their previous behavior.
- Coastal, volcanic, alpine, desert, woodland, jungle, and links lighting profiles. The actual runtime backdrop sea and Magma lava lake use the shared materials.
- Feathered shoreline foam on visual mesh copies, and the existing Postcard plant atlas, tuft meshes, wind weights, and batching on suitable rough. The new tuft layer is capped at 320 instances / 18,000 triangles per hole.
- Static material batching excludes moving sails and animated water. Per-hole material, texture-map, and visual-mesh ownership supports cleanup and individual rollback.
- Five views for all 16 holes: tee, approach, putting, aerial, and gameplay. Four 4-second camera sweeps cover Island Carry, Frostbite Fjord, Temple Falls, and Obsidian Slab.

No original hole FBX or Blender source was rewritten. Layouts, scoring definitions, physics, characters, and native menus were not changed by this implementation.

## Gate results

| Gate | Result | Evidence |
| --- | --- | --- |
| Catalog coverage | **PASS** | 13/13 selectable targets; three procedural Meadow holes also build with the shared catalog. |
| Materials and profiles | **PASS** | 18/18 focused Edit Mode tests. Required specialty maps load; profile objects are independent of the Postcard reference. |
| Ground and hazards | **PASS** | Final Play Mode comparison covers all 13 targets: ground samples, green height grid, lies, hazards, obstacle data, collider counts, and the windmill remain equal before/after. |
| Original geometry | **PASS** | All 233 captured collision meshes across 16 holes have identical world-space vertex/triangle hashes. All 33 protected FBX/Blend files match the snapshot. `Hole.cs` is unchanged. |
| Material lifetime | **PASS** | Repeated transitions retain only the active hole’s named materials and one course-coordinate map after normal frame-end disposal. This checks new runtime ownership, not total GPU residency. |
| Visual proof | **PASS** | 80 final stills and 80 baseline stills, 16 comparison sheets, plus 384 moving-camera frames. Steep grass stretching and coastal rock emission found in pilots were corrected before final capture. |
| Postcard reference | **PASS, with animated-frame limits** | Source assets unchanged; reference tee views preserve appearance. Baseline-to-final RGB mean absolute differences: 0.0933/255 for 8, 0.0120/255 for 9, 0.00054/255 for 10. See the animation limits below. |
| Existing gameplay checks | **PASS for 7 broad checks; one baseline failure** | Tree collisions, all-hole review, looking toward the pin, controller maps, TV HUD routing, and both native-session flow tests pass in Unity. The legacy next-shot test times out identically on the untouched baseline. |
| Target-device performance | **FAIL — not measured** | No iPhone is connected. Editor counters include additional cameras/passes and cannot certify the 250-draw / 300,000-rendered-triangle / 16.7 ms device target. |
| iOS build and native app rendering | **FAIL — unavailable here** | The installed Unity 6000.3.24f1 has only MacStandaloneSupport. iOS Build Support is absent, and the remembered iPhone is offline. No updated native app binary was built or installed. |

The implementation and local validation are complete. Device performance and native shipping gates remain open; this report does not certify an iPhone release.

## Measurements

Final rendering used **Unity 6000.3.24f1, URP, Metal, Apple M5**, at 900×1600 for stills. New-hole course meshes intersecting the tee camera frustum range from **39,398 to 139,476 triangles**, with **31–92 material passes** before shadow passes, characters, UI, and other cameras. These are mesh inventories, not measured GPU draw counts. The whole Editor frame counters are included separately in the CSV and are not directly comparable to a single-device gameplay budget.

Referenced course-texture runtime allocations range from **10.72 to 18.35 MiB** for the upgraded holes. Textures are deduplicated within each report. This excludes hero/UI resources and does not measure the entire retained cache or actual iPhone GPU residency; the proposed shared-residency budget is still provisional.

| Hole | Collision meshes | Obstacles | Referenced texture MiB | Camera mesh triangles | Camera material passes |
| --- | ---: | ---: | ---: | ---: | ---: |
| 7 | 21 | 119 | 15.73 | 54,412 | 64 |
| 12 | 14 | 161 | 16.78 | 139,476 | 61 |
| 13 | 16 | 138 | 14.52 | 58,950 | 31 |
| 14 | 14 | 247 | 15.13 | 69,552 | 52 |
| 15 | 11 | 97 | 15.13 | 58,866 | 51 |
| 16 | 16 | 178 | 18.35 | 89,552 | 53 |
| 17 | 19 | 186 | 14.78 | 72,190 | 44 |
| 18 | 9 | 84 | 10.73 | 39,398 | 40 |
| 19 | 14 | 171 | 16.49 | 106,320 | 60 |
| 20 | 14 | 125 | 15.13 | 84,486 | 92 |
| 21 | 15 | 66 | 15.68 | 93,996 | 55 |
| 22 | 20 | 75 | 15.68 | 97,090 | 57 |
| 23 | 20 | 84 | 15.68 | 100,540 | 34 |

## Reference-frame limits

The static Postcard surfaces and sky match. Animated flags, ball presentation, waves, water highlights, and particles are not locked to an identical Unity effect clock. A repeat run of the unchanged final implementation establishes that the large aerial water differences are normal capture variance: hole 8 changes 31.44% of pixels by more than 10/255 against baseline and 33.50% against a repeat; hole 9 measures 16.61% versus 17.54%. Putting views also contain different flag poses and ball presentation. These images support visual preservation, not pixel identity for every dynamic object. Detailed numbers are saved in `reference-differences.json`.

## Test accounting and baseline issues

The broad Edit Mode run returned **86 passed / 3 failed**. All three failures reproduce with identical messages on the untouched source snapshot: `CliffsideIsStillHoleSevenAndTheNumbersDoNotClash` still expects one Cliffside hole; `NoForcedCarryOverTheLimitAndDryFairwayShortOfEach(8)` and `(10)` reject existing Fringe lies. The final focused material suite is **18/18 passed**.

The broad Play Mode run returned **7 passed / 2 failed**. The new comparison initially sampled temporary primitive colliders before their scheduled removal; the harness now waits for normal cleanup. Its final run passes for all 13 targets, material disposal, and Meadow 1–3. The other failure, `NewHolesPlayTests.TheNewHolesPlay` timing out while waiting for the next shot, reproduces unchanged on the baseline.

Three unrelated older Play Mode source files referenced retired character APIs and prevented compilation. They were excluded only from both disposable capture projects (`NativeSessionTests`, `PlayerBaseTests`, `StandardCharacterGameplayTests`); the main project’s files were preserved. Runtime source compilation passes, with the existing `TennisGame.audio` hiding warning. Unity’s editor search-index exception appears in both baseline and upgraded captures; all capture jobs finish successfully.

## Files and rollback

Runtime code is in `Unity/Assets/Scripts/Course/GolfCourseLook.cs`, `GolfCourseAtmosphere.cs`, `GolfCourseFringe.cs`, and `GolfCourseFoam.cs`, with scoped integration in `HoleView`, `GolfAtmosphere`, `LavaWorld`, surface batching, edges, and the ground/rock shaders. Assets are in `Unity/Assets/Resources/Course/Standard/`. The source generators are `blender/scripts/golf_course_standard_textures.py` and `golf_course_standard_props.py`.

`GolfCourseLook.Enabled = false` restores the pre-upgrade routing. Add a hole number to `GolfCourseLook.DisabledHoles` to disable just that hole. Postcard-only material defaults are unchanged. Clear/rebuild the current hole after changing a switch.

Original snapshots, compiler logs, NUnit XML, and complete material audits are under `work/golf-course-standard/`. Full-resolution images and comparison sheets are in `ArtDir/screenshots/golf_course_standard/`. Isolated Unity projects and compiler caches are excluded by the scratch directory’s `.gitignore`. No commit, push, deployment, or app installation was performed.
