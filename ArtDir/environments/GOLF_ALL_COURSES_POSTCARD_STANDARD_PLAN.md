# Golf course quality standardization plan

Bring every non-Postcards golf course to the visual standard of the installed Postcards holes: detailed grass, convincing water, textured cliffs, natural surface transitions, cohesive lighting, and well-finished scenery at the actual gameplay camera. Reuse the postcard material and texture library as the foundation, with profiles that preserve each course’s setting.

Planning baseline: October 6, 2026, `final-build`, HEAD `217570aa`, including the current uncommitted work. Implementation proceeded automatically on October 6, 2026. All 13 selectable targets and procedural Meadow materials are now migrated; local validation is complete. iPhone performance and native build verification remain open because the device is offline and iOS Build Support is absent. See `ArtDir/screenshots/golf_course_standard/GATE_RESULTS.md` for results.

## Scope and completion target

The selectable catalog contains 16 holes. Postcards 8–10 are the visual reference and regression controls. Upgrade all 13 remaining holes:

| Course | Hole | Visual treatment and special checks |
| --- | --- | --- |
| Cliffside | 7 Cliffside | Full textured turf, coastal rock, layered sea, trees, clubhouse, paths, and readable bunker edges. Preserve existing island geometry. |
| Cliffside | 12 Island Carry | Coastal pilot. Deep sea, shallow shelves, broken surf, textured island cliffs, bridge and lighthouse. Preserve water blendshapes and island boundaries. |
| Cliffside | 13 The Spiral | Consistent turf density and mowing along the winding fairway; textured slopes and pine scenery. Preserve all elevation and shortcut behavior. |
| Cliffside | 14 The Witch’s Lair | Distinctive shaded crater setting, detailed ground and rock, finished structures and vegetation. Keep the entrance and recessed green readable. |
| Cliffside | 15 The Steps | Fine putting grass and clear tier edges; coastal water and cliff treatment. Preserve every step and the pin’s tier. |
| Wild Isles | 16 Volcano Rim | Shared lava, basalt, ash, smoke, and volcanic lighting; green turf remains distinct. Preserve the lava river’s penalty boundaries. |
| Wild Isles | 17 Frostbite Fjord | Dedicated snow and ice surfaces, cold rock, winter vegetation, restrained blue water. Ice remains a playable sliding surface. |
| Wild Isles | 18 Mesa Canyon | Sandstone strata, dry ground, sand, cacti, and canyon water. Preserve the two mesas and the carry. |
| Wild Isles | 19 Temple Falls | Jungle grass, mossy masonry, wet rock, lagoon, waterfall, and tropical foliage. Preserve the stone step and pool hazard. |
| Wild Isles | 20 Windmill Links | Links grass, canal and pond water, tulips, wood, and masonry. Turning sails must stay animated and retain their collision timing. |
| Magma Open | 21 Obsidian Slab | Volcanic pilot. Textured obsidian and turf, detailed lava lake, warm contact lighting, and cohesive crater backdrop. |
| Magma Open | 22 Ember Causeway | Carry the volcanic standard across the narrow winding route; maintain clear playable edges against lava. |
| Magma Open | 23 Caldera Crown | Apply it across the larger caldera and distant scenery; preserve the horseshoe route and across-lagoon options. |

`Course.Meadow()` also defines procedural holes 1–3 but is absent from `Course.All()`. Include its generated surfaces in the shared material integration and fallback checks; keep its catalog availability as it is. Enumerate actual entry points during the audit so no reachable golf view retains an accidental legacy path.

Completion means every listed hole meets the visual, gameplay, and performance gates. A successful material assignment alone does not establish quality parity.

## Why the quality differs today

The current source exposes several concrete gaps:

- `GolfLook` builds the postcard textures and materials, but `DressModel` is restricted to holes 8–10. Most other holes resolve through `HoleView`’s older `MAT_*` palette, `TurfMat`, flat Lit materials, and separate water or lava code.
- Hole 7 receives `DressLegacy`, but that method replaces the surface albedo with `Texture2D.whiteTexture` and sets `_NormalEnabled` to zero. It gains some shader treatment without the postcard texture detail.
- `GolfAtmosphere` has authored profiles for 8–10. Other courses use the legacy atmosphere or Magma theme. Lighting, fog, and sky therefore need to be migrated with the materials.
- Visible sea is also created by `Backdrop.Place`. `HoleView` disables the imported `WATER_OCEAN`, so improving that mesh’s material alone will miss the sea the player actually sees. Magma’s background is built separately by `LavaWorld`.
- The old turf table renders snow using the rough tile and desert using sand. Specialty environments need their own surface profiles and, where necessary, new shared texture sets.
- Postcard proof tools assume holes 8–10 and load `Course.Postcards()`. Their capture and validation coverage must expand before rollout.

Use the installed look the user likes as the benchmark. Existing postcard images are useful references, but fresh captures must match the current working tree. Historical postcard phase notes and the later golf report contain incomplete validation; they are not a current all-pass certificate or a reason to restart those projects.

## The shared visual standard

### Materials and textures

Retain the existing `GolfGround`, `GolfRock`, `GolfPlants`, `GolfSurf`, and `GolfLava` families. Use a common catalog of named surface roles and reusable settings. A course profile selects textures, tint, physical texture scale, sheen, water character, and atmosphere. Per-hole overrides should be small and explicit.

| Surface | Foundation | Required result |
| --- | --- | --- |
| Fairway | `Fairway_C`, `Fairway_N`, `GolfGround` | Fine blade detail, broad natural variation, soft mowing bands, and controlled sheen from albedo alpha. |
| Green and tee | `Green_C`, `Green_N`, explicit surface roles | Finer, quieter texture; clear putting surface and tee separation. |
| Fringe and first cut | Shared grass maps with dedicated role settings | A readable transition around the green and fairway without a harsh painted outline. |
| Rough and scrub | `Rough_*`, `Scrub_*` | Coarser texture and coherent color variation; selective tufts along visual edges. |
| Bunkers and sand | `Sand_*`, `GolfGround` sand mode | Fine grain, subdued ripples, lighter lips, and grounded edges. |
| Cliffs and stone | Existing `Cliff`, `CliffDark`, `Rock`, `Masonry`, and `Basalt` maps | Consistent world scale, stratification, wet bases where appropriate, and no stretched faces. |
| Water | Postcard water settings and the existing water shader implementation | Distinct deep and shallow regions, restrained reflections and wave detail, consistent foreground and backdrop water. |
| Surf and waterfalls | `Surf_C`, `Fall_C`, `GolfSurf` | Broken natural foam, soft intersections, readable falling water, and no continuous opaque white shoreline band. |
| Lava and smoke | `Lava_*`, `Basalt_*`, `Smoke_C` | Orange heat with darker crust, readable charcoal rock, controlled glow, and soft smoke. |
| Plants | `Plants_C`, `GolfPlants`, existing sway support | Cohesive foliage color and detail, grounded roots, restrained motion, clear ball sightlines. |
| Snow and ice | New reusable snow and ice sets | Snow grain and soft shading; readable ice depth/cracks and restrained sheen. Do not approximate snow with green rough detail. |
| Desert and jungle | Shared dry-ground, sandstone, moss, and wet-stone variants | The same finish quality while retaining each setting’s palette and material identity. |
| Buildings and props | Shared stone, wood, bark, painted-surface, and roof materials | Bridges, lighthouse, temple, cabins, clubhouse, and windmill match the terrain’s finish. |

The existing rock shader uses triplanar color and packed emission textures from `Course/Surface`; its current rock branch does not sample the assigned normal map. Validate actual rendered relief rather than declaring normal detail present from a material property. Extend the rock implementation only where visual evidence justifies the cost.

Use the existing texture convention: `_C` for color, `_N` for normal, `_E` for emission; preserve the packed `_CE` rock textures and the grass alpha channel used for sheen. New data masks must import as linear data. Share assets across holes instead of making one texture copy per hole.

Keep tiling textures generally at 1024, smaller props at 512 where sufficient, and panoramas at 2048, consistent with the current importer. Use mipmaps, appropriate filtering, and non-crunched compression. Specify and verify iOS ASTC overrides. Preserve postcard sampling settings; tune new profiles only after checking oblique gameplay views for blur and shimmer. Document the channel meaning of every packed map.

### Texture scale and mowing

The postcard contract authors UVs in metres per tile, while the runtime world uses yards. Preserve that conversion explicitly. Start from the existing fairway 10 m, green 6 m, and rough 12 m tile scales, then validate them beside the reference surfaces.

Audit UVs and tangents before applying normal maps. Repair visual UVs or provide a profile-controlled world-projection path where necessary; retain existing collision geometry. On curved holes such as The Spiral, use the authored centerline to orient the mowing field. A single tee-to-pin direction is insufficient there.

Choose one deliberate source for visible mowing contrast. Existing legacy material stripes, postcard texture stripes, and shader bands must not stack into dark zebra bands. Lock the combined result through reference captures and moving-camera checks.

### Lighting and scenery

Create coherent coastal, volcanic, alpine, desert, jungle, and links profiles. Each owns sun, ambient light, sky, fog, water palette, exposure, and distance treatment. Maintain the postcard approach to soft stylized lighting and clear silhouettes. Achieve the standard without adding a global post-processing dependency.

Finish the whole visible scene: cliffs, shoreline shelves, vegetation, paths, buildings, landmarks, and background terrain. Material replacement is the first pass. Follow it with targeted visual geometry and placement repairs where flat silhouettes, repetitive cliffs, empty edges, or crude props still lower the result.

Keep playable geometry and obstacle volumes fixed. Add decorative geometry only through an explicitly visual path. `ObstacleScan` derives obstacles from mesh names and plant markers, so a mesh without a collider can still alter gameplay. Snapshot and compare obstacle data as well as MeshColliders.

## Runtime and asset integration

1. Add a golf course look profile and registry, keyed by course and hole. Suggested new types are `GolfCourseLookProfile` and `GolfCourseLookRegistry`. Keep the postcard entries equivalent to their current values.
2. Add an explicit legacy material adapter. Resolve original `MAT_*` names and exceptional mesh slots before `HoleView` replaces their names with generated materials. Cover fairway stripes, first cut, bunker lips, themed rough, snow, ice, lava, props, and mixed terrain/cliff meshes. Report unmapped slots.
3. Replace hole-number eligibility checks with profile eligibility for non-postcard upgrades. Preserve a per-hole rollout switch and the original postcard behavior. Do not repurpose `IsPostcard` globally; it is also a course identity and proof assumption.
4. Route both imported surfaces and runtime-created geometry through the same catalog. Include `BuildGeometry`, `Backdrop`, and `LavaWorld`. Give lava-water remapping explicit precedence for Magma so it cannot become ordinary ocean water.
5. Reuse the existing water implementation by reference. Consolidate golf’s settings and ownership without modifying tennis visuals. The postcard water’s shallow/deep gradient uses distance from the origin, not shoreline depth; use authored shelves or a golf-owned shore mask for credible shallows around long courses, islands, canals, and ponds.
6. Consolidate atmosphere application into one final per-hole decision. Test the interaction between `HoleAtmosphere`, `GolfAtmosphere`, course browsing, and gameplay so later calls do not overwrite the selected look.
7. Make shared material caches profile-aware. Include every rendering parameter that changes the result, particularly texture scale and mowing direction. Reset per-hole edge maps and atmosphere state during transitions; avoid mutable cached materials leaking one hole’s values into another.
8. Adapt `GolfSurfaceEdges` to the actual names and roles of legacy greens, tiered surfaces, shallows, canals, and ponds. Preserve scoring edges exactly. Cache or bake derived masks if generating them during a hole switch causes a hitch.
9. Extend visual batching only after material conversion. Exclude moving sails, water blendshapes, flags, particles, and their animated parents. Preserve plant pivot/weight data and keep batches bounded for useful culling and local lights.
10. Add a staged visual post-pass for the non-postcard Blender assets. Reuse the postcard look/props libraries where their assumptions fit. Do not directly run a postcard builder on sloped legacy terrain. Keep original vertices, transforms, markers, and gameplay inputs as the comparison baseline.

The source edit surface is concentrated in `GolfLook`, `HoleView`, `GolfAtmosphere`, `HoleAtmosphere`, `Backdrop`, `LavaWorld`, the edge/batching utilities, texture importing, and golf-specific proof tools. Extend Blender materials, UVs, and visual dressing separately from the hole design scripts.

## Ordered implementation

### Phase 0 Capture and inventory

Record the current commit, working-tree file hashes, and relevant uncommitted changes. Preserve the current index. Establish a fresh baseline for all 13 target holes and postcard controls 8–10.

For every hole, capture tee, normal approach, putting, shoreline or hazard, and aerial views. Include the golfer, ball, cup, and HUD in gameplay captures; also save clean environment views. Inventory visible material slots, shader paths, textures, UV density, tangents, vertex channels, dynamic meshes, atmosphere, draw calls, triangles, resident texture memory, and frame times.

Create one checklist per hole with its actual gaps and its relevant postcard reference. Capture deterministic camera positions and fixed wind/time settings for still comparisons. Use real motion for temporal checks.

Exit: all 13 targets identified, reference captures current, material coverage mapped, baseline gameplay data and performance recorded. Fresh baselines supersede assumptions in older proof notes.

### Phase 1 Build and validate the shared system

Implement the profiles, legacy adapter, texture rules, water/backdrop routing, atmosphere routing, and cache lifecycle. Expand capture tooling to resolve holes through the actual catalog rather than `Course.Postcards()`.

Build a small material comparison scene under the reference lighting. Validate the same grass, sand, stone, water, and lava assets in both the reference and new profiles. Keep unmapped environment slots visible in an audit report instead of silently counting fallback materials as migrated.

Exit: all required surface roles work, representative UVs and normal maps render correctly, both supported URP assets compile, and postcard controls retain their baseline appearance.

### Phase 2 Complete two pilot holes

Finish **12 Island Carry**, then **21 Obsidian Slab**. These cover the ocean/backdrop and lava/crater paths. Each pilot includes surfaces, lighting, vegetation, props, and transitions, followed by actual gameplay and performance proof.

Produce side-by-side comparisons: original hole, upgraded hole, and the matching postcard material reference at comparable camera scale. Correct the pilots until they meet the full gates, then freeze the shared presets for rollout. Pilot sheets are concrete review artifacts; routine corrections continue within the agreed scope.

Exit: both pilots pass visual, gameplay, transition, and device-performance gates. If a shared treatment fails, fix it before multiplying the problem across the catalog.

### Phase 3 Complete the remaining Cliffside and Magma holes

Upgrade 7, 13, 14, 15, 22, and 23 using the proven coastal and volcanic foundations. Tune atmosphere and scenery for each hole. Pay special attention to curved mowing on 13, shaded readability on 14, tiered putting surfaces on 15, and the larger visible scenes on 22–23.

Exit: all five Cliffside and all three Magma Open holes pass individually. No course is accepted from a single representative screenshot.

### Phase 4 Complete Wild Isles and procedural coverage

Upgrade 16 Volcano Rim, 19 Temple Falls, 20 Windmill Links, 18 Mesa Canyon, and 17 Frostbite Fjord. This order reuses volcanic and coastal work before introducing desert and winter materials. Complete the snow/ice sets as part of this phase, rather than leaving them as recolored grass.

Route Meadow’s generated surfaces through the shared catalog and validate its existing fallback behavior. Recheck all reachable golf previews and native entry paths.

Exit: all 13 target holes pass; procedural surface coverage is verified; special hazards and moving obstacles retain their behavior.

### Phase 5 Verify the complete catalog

Run a catalog sweep using the integrated build, including repeated transitions between coastal, postcard, volcanic, alpine, and jungle holes. Measure memory after repeated browsing/rounds to detect retained materials, textures, meshes, or lights. Regenerate any course cards that visibly misrepresent the finished maps.

Save per-hole comparison sheets, short moving-camera clips, native gameplay evidence, a performance table, and `GATE_RESULTS.md`. Report every unmeasured or failed item explicitly. Keep the rollout switch available until the complete sweep passes.

## Acceptance gates

| Gate | Passing evidence |
| --- | --- |
| Coverage | 13 of 13 selectable targets use approved profiles; every visible environment slot resolves to the catalog or a documented intentional special material. Missing required textures or silent flat fallbacks fail. |
| Grass and ground | Tee, approach, and putting views show reference-level detail and distinct fairway, green, fringe, rough, and sand. No UV stretching, double stripes, harsh outlines, or visible tiling seams. Combined mowing contrast is measured against the freshly captured postcard reference band. |
| Water and shore | The sea actually rendered by the runtime matches the profile; shallow regions follow the shore, foam breaks naturally, and foreground/background seams are absent. Water motion has no visible flicker or competing deformation. |
| Theme quality | Every specialty surface and major visible prop meets the same finish standard. Snow, ice, jungle masonry, sandstone, lava, and wood retain their intended identity. |
| Gameplay readability | Ball, cup, aiming cues, landing areas, and hazards remain readable with HUD and golfer present, including putting and dark volcanic views. No foliage, foam, or highlights obscure them. |
| Geometry and scoring | Original playable vertices and transforms, tee/pin markers, shore/islet definitions, hazards, par, surface samples, obstacle volumes, and moving-sail behavior match baseline. Maximum authored play-position delta is 0.000 m; runtime samples compare with a recorded numerical tolerance. |
| Motion and transitions | Camera sweeps show no grass/rock shimmer, alpha sorting faults, popping, or shadow instability. Course switches restore the correct sky, lights, material values, edge masks, and exposure. |
| Postcard regression | Holes 8–10 preserve baseline assets and appearance. Use deterministic paired frames with identical cameras and effect clocks; establish repeat-capture noise before setting image-difference tolerance. |
| Performance | Retain the existing course targets of at most 300,000 rendered scene triangles and 250 draw calls, measured in the agreed gameplay views with characters and effects identified separately. Profile the worst view and full flyover as well. |
| Native rendering | Both current URP assets and the iOS Metal build render every material correctly. Verify the actual app entry path, course selection, gameplay, and course exit on a supported iPhone. |

The game targets 60 fps. For this rollout, propose CPU and GPU p95 frame times within 16.7 ms during a warmed gameplay route on the agreed target iPhone; report p99, stalls, thermal conditions, resolution, and render scale as well. Establish the baseline on the same device and settings. If the current build is already over budget, classify the limitation and optimize it explicitly; do not label it a pass or silently lower rendering quality.

Proposed texture guardrail: at most 32 MiB for the resident shared course texture library plus 8 MiB for the active theme, including mipmaps. Count hero/UI resources separately. Confirm this provisional allocation against Phase 0 measurements before rollout; report GPU residency and retained cache memory, not PNG disk size. Optimize reuse, culling, batches, and overdraw before reducing the approved visual standard.

Run existing relevant checks: `CliffsideHoleTests`, `IslandCarryHoleTests`, `NewHolesTests`, `SurfaceTests`, `ObstacleTests`, `StrikeAndLieTests`, `PostcardHoleTests`, `NewHolesPlayTests`, `CourseReviewTests`, and `NativeGolfFlowTests`. Add focused tests for material-role coverage, profile/cache isolation, atmosphere restoration, specialty hazard preservation, and animated objects excluded from batching. Adapt existing golf smoke, import, grass, sway, and capture tools where applicable; postcard-only assumptions must not be applied blindly to the other courses.

## Change control and deliverables

The implementation should preserve each hole’s layout, difficulty, and identity while upgrading its presentation. The user’s request includes hole 7’s visual quality; older postcard-only exclusions do not remove it from this plan. Keep its original source geometry protected and apply visual overlays or runtime materials where practical.

Use the current working tree as the starting point, including existing golf shader work. Preserve unrelated tennis, character, clothing, menu, and multiplayer changes. Run Unity and Xcode jobs serially using the repository’s existing heavy-job coordination, and never overwrite files being modified by another active task.

Store implementation scratch and rollback snapshots under `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/golf-course-standard/`. Store final course proof under `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/ArtDir/screenshots/golf_course_standard/`.

Final deliverables are the shared material/profile catalog, all 13 completed holes, specialty texture sets, updated runtime routing, coverage and regression checks, per-hole visual proof, native performance measurements, and a gate report. Keep a per-hole rollback to the baseline appearance so a failed rollout can be isolated without undoing other work.

## Source locations

- [Course catalog and gameplay definitions](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Scripts/Course/Hole.cs)
- [Runtime material routing and geometry loading](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Scripts/Course/HoleView.cs)
- [Postcard materials and partial legacy adapter](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Scripts/Course/GolfLook.cs)
- [Postcard atmosphere](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Scripts/Course/GolfAtmosphere.cs)
- [Ground shader](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Resources/Course/Shaders/GolfGround.shader) and [rock shader](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Resources/Course/Shaders/GolfRock.shader)
- [Texture importer](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/Unity/Assets/Editor/GolfLookTextureImporter.cs)
- [Legacy Blender builder](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/blender/scripts/course_builder.py) and [postcard visual library](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/blender/scripts/postcard_look_lib.py)
- [Postcard material and export contract](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/postcard-look/LOOK_CONTRACT.md)
- [Current golf implementation report](/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/golf-look/REPORT.md)



## Completed tree repair follow-up

Repaired 85 legacy palms on holes 16, 19, 21, 22, and 23 with continuous trunks and attached fronds. Corrected vegetation palette handling across the upgraded courses and added two-sided foliage lighting. Original tree obstacle meshes and course collision/hazard data remain intact. The final Play Mode catalog regression and 18 Edit Mode material checks pass; full before/after close-ups and results are in `ArtDir/screenshots/golf_course_standard/tree_repair/` and the shared gate report.
