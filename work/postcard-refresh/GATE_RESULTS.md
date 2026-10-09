# Golf and tennis visual refresh — gate results

Implemented the nine requested visual changes in the existing Unity project on `codex/full-visual-overhaul`. Final captured golf results: **48/48 PASS**, across 16 authored holes and three gameplay poses per hole. Recorded draw calls range from **113 to 249**, including the gameplay HUD and minimap.

These are Unity 6000.3.24f1 Editor measurements on Metal / Apple M5. They establish the budget for the captured views; iPhone frame rate, thermal behavior and every possible camera position have not been profiled.

## Visual review

- [Split: tee, cliffs and surf](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole09-tee.png) · [approach](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole09-approach.png)
- [Crater: tee, basalt and lava](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole10-tee.png) · [approach](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole10-approach.png)
- [Tennis: production gameplay camera](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/tennis-gameplay.png)
- [Cold course grass](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole17-tee.png) · [Volcanic grass](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole22-tee.png) · [Windmill final budget check](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/proof/hole20-tee.png)

The `proof` directory contains all 48 golf captures plus the tennis capture. Golf captures are 900 × 1600 with 4× MSAA; tennis is 1920 × 1080.

## Requested changes

| Request | Result |
| --- | --- |
| Post-processing | Connected URP post-process resources and the actual golf camera; HDR, ACES, bloom, grading and vignette now run. Each camera owns one private profile. Low quality retains tone mapping while disabling bloom. Lighting and emissive intensity were retuned for the active pipeline. |
| Postcard turf | Brighter lime palette with bold two-tone fairway and tee stripes, coordinated blade colors and distinct rough. |
| Fog and haze | Fog starts at 150 yd, with longer blue atmospheric falloff suited to coastal and volcanic worlds. |
| Hero lighting | Reduced camera fill, removed legacy color compensation, added world-light rim and ground bounce, plus terrain-aware contact and shoe occlusion. Crater skin no longer inherits the strong blue fill. |
| Camera framing | Higher tennis camera with a longer lens; closer golf address framing. Coastal tee framing keeps the hero clear of the HUD while revealing more shoreline. |
| Grass | Enabled holes 16, 17, 18, 21, 22 and 23. Hole 10 passed its moving-turf test and is enabled. Exposed rock, snow, ash and paths remain excluded from grass placement. |
| Crater | Combined hexagonal basalt columns, glowing fissures, cooled lava plates and emissive lava; existing smoke remains integrated. The black tower is replaced visually by a continuous basalt formation. |
| Split | Authored a new reference export and coordinated shoreline mapping so near cliffs and surf read from gameplay, using the coastal composition approach established for hole 12. |
| Foliage and budget | Denser instanced clusters, cheap distant plant representations and culling. Coastal trees use shared near/far meshes. Minimap renders are cached until their framing or course changes. |

## Render and physics gates

Every accepted view reports HDR and post-processing enabled, renderer resources present, ACES and bloom active, one private volume profile, fog start at 150 yd, and exact before/after visual-dresser physics. All tee views have active grass cells.

Draw counts are the maximum recorded across settled, completed frames for each pose, with the actual gameplay camera, HUD and minimap. Shadow rendering uses a 60-unit range, one cascade and a 4096 atlas in High quality; contact occlusion supports the close hero view.

| Hole | Tee draws | Approach draws | Putting draws | Gate |
| --- | ---: | ---: | ---: | --- |
| 7 | 234 | 177 | 125 | PASS |
| 8 | 246 | 213 | 117 | PASS |
| 9 | 249 | 148 | 125 | PASS |
| 10 | 170 | 176 | 122 | PASS |
| 12 | 216 | 148 | 130 | PASS |
| 13 | 185 | 158 | 124 | PASS |
| 14 | 218 | 148 | 120 | PASS |
| 15 | 210 | 122 | 127 | PASS |
| 16 | 218 | 146 | 119 | PASS |
| 17 | 199 | 133 | 113 | PASS |
| 18 | 193 | 122 | 114 | PASS |
| 19 | 242 | 168 | 134 | PASS |
| 20 | 249 | 154 | 130 | PASS |
| 21 | 199 | 137 | 123 | PASS |
| 22 | 225 | 139 | 126 | PASS |
| 23 | 169 | 169 | 125 | PASS |

[Combined machine-readable results](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/render-gates.json) retain the provenance of two runs. The initial catalog passed 47/48 views; Windmill's tee measured 256 draws. A hole-20-only canopy batching adjustment reduced its tee to 249 without changing plant meshes, positions, materials or shadows. All three Windmill poses were then recaptured and replace its earlier rows. The original catalog and final Windmill JSON files are preserved under `evidence`.

**Physics scope:** the exactness checks compare collision state before and after runtime visual dressing. Split's near shoreline was intentionally reshaped as part of its authored art pass; this is a local course-geometry change. Tee/pin anchors and the distant layout were retained, and the analytic shoreline was updated to match the new export. Split passed 2,664 interior land/water samples with zero mismatches; samples exclude the 2-yard tessellated edge margin. The original Split source asset remains available.

## Hole 10 grass acceptance

[Moving-turf proof](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/grass-proof/moving-turf-proof.json): PASS. Seven address poses passed; a 397-frame flight across 42 coarse stations produced no flight-time turf rebuilds. Ownership, pool/material/matrix checks and static physics checks passed. Final catalog captures independently confirm active hole-10 grass after the lighting and batching refinements. The grass-proof screenshots predate those final lighting refinements and are retained as grass-behavior evidence.

## Compilation and reproducibility

- Postcard-focused checks: **5 passed, 0 failed**; runtime and test assemblies compiled.
- Tennis gameplay/camera checks: **11 passed, 0 failed**; runtime and test assemblies compiled.
- Final real-Unity catalog, Windmill and tennis captures contain no C# compiler or shader errors. Existing compiler warnings are preserved in the test logs.
- All 53 touched Unity source/asset files byte-match the isolated project used for final verification. The [54-file SHA-256 manifest](/Users/adnanyonathan/Documents/Codex/2026-10-08/open-5/outputs/evidence/changed-files.json) also includes the Split Blender authoring script.

Project: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`. The editable Split export is generated by `blender/scripts/build_reference_hole09.py`; its native Blender scene and export are retained in `work/postcard-refresh/split-source`. Render verification is implemented in `Unity/Assets/Scripts/Course/PostcardRefreshProofRunner.cs`, with entry point `GolfArcade.EditorTools.PostcardRefreshProof.Run`. The capture queue and isolated verification project are retained under `work/postcard-refresh`.

No device build, deployment or commit was made. Existing unrelated workspace changes were preserved.
