# Hero 01 — UnityLook

## Delivered

Unity **6000.3.24f1**, URP **17.3.0**. The approved hero is imported as a valid **Humanoid** with a working Idle PlayableGraph and five replaceable wardrobe slots.

- Prefab: `Unity/Assets/ArtDirection/Hero01/Prefabs/Hero_01_Modular.prefab`
- Review scene: `Unity/Assets/Scenes/Hero01Look.unity`
- Materials, textures, models and review URP assets: `Unity/Assets/ArtDirection/Hero01/`
- Comparison: `ArtDir/screenshots/unity_hero_vs_plate01.png`
- Uncropped Unity hero capture: `ArtDir/screenshots/unity_hero.png`
- Court capture: `ArtDir/screenshots/unity_gameplay_or_court.png`
- Import and live animation evidence: `ArtDir/screenshots/unity_import.json`, `unity_playmode.json`

Open **Hero01Look** and press **Play** to see the default wardrobe Idle. The saved camera frames the hero on an ivory review apron beside the court. **Golf Arcade → Art Direction → Build and Capture Hero 01** rebuilds the prefab/review scene and captures the hero and court in Play Mode. It opens the dedicated scene; save any other scene edits before using the command.

The court image is a **look-review court**, not a claim that the new hero is playing live tennis. The existing Tennis scene, menu, campaign, physics and contact/reach logic are unchanged. This phase does not put an idle-only model into the live tennis actor.

## Asset provenance

Used `ArtDir/anims/Hero_01_Mixamo_Bind.fbx`, `Idle.fbx`, `Ready.fbx`, and the **corrected** wardrobe exports in `ArtDir/anims/wardrobe/`. MIXAMO.md identifies a joint-depth correction and weight fixes after Modularize; the older `ArtDir/hero/modular/` FBXs must not be mixed into this corrected skeleton.

No hero geometry was regenerated, retopologized or resculpted. All the wardrobe mesh partitions and vertex positions come from the approved Mixamo-stage exports. The normal map is supplied in Textures but is not enabled in the matte look; no pore/detail layer was added.

## Humanoid vs Generic

`HeroLookImporter.cs` applies **only** under `Assets/ArtDirection/Hero01/`:

- The full body bind creates a Humanoid Avatar with explicit Hips/Spine/Chest/Neck/Head and paired shoulder/arm/hand/leg/foot/toe mapping.
- Idle and Ready use **Copy From Other Avatar**, referencing the body Avatar. Imported clips retain their names and use loop time, uncompressed curves, and locked root translation/rotation for this look review.
- Wardrobe FBXs are mesh sources with a retained skeleton and **no independent Avatar**. Their Generic import setting only preserves their skinned mesh/bone data; the assembled character and animation clips are Humanoid.
- Hierarchy is preserved; game-object optimization is off so sockets and bone-name binding remain accessible. Maximum four influences; vertex welding and mesh reorder optimization are disabled for this review import.

`GolferModelImporter.cs` still forces Generic for its existing StandardCharacters/Golfer/Tennis player paths. It was **not rewritten**. The new folder does not match those filters, so there is no broad importer exception or Generic fallback for the main hero.

## Default wardrobe and swap hooks

`ModularHeroLook` references one `Animator`, one `skeletonRoot`, Idle, and a serialized five-entry default wardrobe:

| Slot | Default mesh source |
|---|---|
| Hair | Hero_01_Hair_Default.fbx |
| Hat | Hero_01_Hat_Visor.fbx |
| Shirt | Hero_01_Shirt_Default.fbx |
| Shorts | Hero_01_Shorts_Default.fbx |
| Shoes | Hero_01_Shoes_Default.fbx, including socks |

Body_Skin and the two eye meshes remain on the body. The default wardrobe is saved in the prefab and rebuilt on enable at runtime. `Equip(Slot, GameObject)` validates/rebinds a replacement, then removes the old slot. It copies meshes/materials, not an additional animated skeleton.

`TennisCustomization.RebindCosmetic` is the small shared helper added to the existing customization class. It follows the existing AttachBase pattern: resolve bones by name, preserve mesh placement, assign the original mesh/materials to a SkinnedMeshRenderer, and point it at the body's transforms. Existing AttachBase/Apply behavior was not replaced.

Sockets retained: **Hat, Hand_R, Hand_L, Back, FaceExtra**. Grip offsets remain provisional. No scale, hitbox, timing window, ball contact or online power changes accompany wardrobe swaps.

### Skin tint

`SetSkinTone(Color)` uses the existing **KitRecolor** shader and supplied mask alpha / skin reference **0.44107563**. It allocates one reusable recolored texture per hero and uses private body materials; it does not recolor another character or the wardrobe.

- Default loadout preserves the approved atlas (`useSkinTint = false`).
- Inspector skin target is the bible's **#FFE0C2**; enable the option before Play to apply it.
- Other documented examples: **#F2C093**, **#B77950**, **#6A4030**.
- Changing tone repeatedly reuses the render texture; owned textures/materials are released when the hero is destroyed.
- This is an API/prefab hook. Wiring the native locker controls to the new hero remains a separate integration step; this phase did not redesign UI.

## Materials

Final materials use **standard Universal Render Pipeline/Lit**. No new shader framework or change to the legacy character shader is required.

| Surface | Smoothness | Metallic |
|---|---:|---:|
| Cloth / hair / shoes | 0.25 | 0 |
| Skin materials | 0.32 | 0 |
| Visor | 0.40 | 0 |
| Eyes | 0.45 | 0 |

Original 2K painted atlas and 256px iris are retained. Normal/mask textures are imported as linear; color/iris as sRGB. Mipmaps are enabled. Review textures are uncompressed; mobile ASTC/LOD profiling remains a release step.

**Import/display issue fixed:** the approved exported surfaces contain mixed-facing/thin regions. Single-sided Unity materials exposed gaps under the visor and in the sleeves that Blender's two-sided review did not show. The final hero materials render both sides. Self-shadow receiving is disabled on the hero materials to avoid dark thin-surface artifacts; the hero still casts soft shadows onto the court, and reduced screen-space AO supplies local grounding. This is a material accommodation, not a mesh replacement. Two-sided surfaces cost more than a consistently wound single-sided mesh; consider winding cleanup in a separately approved mesh pass.

## Lighting and palette

All look adjustments are in **Hero01Look**, with dedicated copied URP assets. `HeroLookLighting` sets the review pipeline for that scene in Play Mode and restores the previous pipeline on disable. Saved project quality settings and the live Tennis renderer were not retuned.

- Warm directional key: **40°** elevation, **#FFF1DE**, intensity **1.15**.
- Soft fill: **20°** elevation, **#FFF6EE**, intensity **0.46** (40% of key), no extra shadow map.
- Flat warm ambient fill: **#FFF1E5 × 0.65**; no crushed face shadows.
- Key shadows: soft, strength **0.42**; existing 2048 shadow map / two cascades retained in the copied pipeline.
- Review SSAO: intensity **0.35**, radius **0.08**, direct-lighting influence **0**, down from the existing renderer's 1.4 / 0.3 / 0.25.
- Neutral tone mapping, exposure **+0.15**, saturation **+3**. No bloom, grain, vignette or motion blur.
- Sky/background: **#7EC8E3**; court/apron: **#F4F0E6**; lines: **#2B2B2B**; net: **#4A5A78** and white. Navy/orange clothing accents remain in the approved atlas, rather than flattening the texture into solid replacement colors.

## Verification

The final Unity run completed without C# compilation or rig errors.

- Body Avatar: `isValid = true`, `isHuman = true`.
- Idle clip: `humanMotion = true`.
- Eight assembled SkinnedMeshRenderers: body, two eyes, five wardrobe pieces.
- All wardrobe bones resolve inside the same skeleton.
- Idle was advancing at capture (~1.98 s); the head moved ~0.0387 m during the observation interval. This is a live Play Mode capture, not an imported static pose.
- The full bind and separate wardrobe import matrices were compared during diagnosis and matched exactly.
- Skin tint was exercised in Play Mode before restoring the approved atlas default.

The comparison image only crops/resizes the supplied plate and actual Unity capture and adds labels. It does not repaint or generate the Unity result.

## Known gaps vs plates

- The reference face, cheeks and cloth folds remain softer and more deliberately sculpted. Unity reveals the approved source mesh's angular folds and small shoulder/visor irregularities; lighting cannot eliminate those geometry differences.
- The default skin reads more golden, and the cloth less diffuse, than the plate's film lighting. The delivered surfaces remain matte and readable; this is not claimed as a perfect plate match.
- No racket/grip pose is equipped. The plate's character gesture is not reproduced by the generic Breathing Idle.
- The review court matches the major palette/readability pattern, not the plate's tropical resort, ocean, spectators or party UI. Environment production is outside this character look pass.
- Two-sided thin surfaces and the self-shadow accommodation should be assessed on an iPhone; this editor run is not a 60-fps device-performance certification.

## Next Animator notes — not started

1. Adapt the live TennisActor PlayableGraph to this Avatar and bone/socket map, without replacing gameplay timing/contact logic.
2. Start with Idle/Ready transitions; Ready is the existing generic defensive stance, not final tennis anticipation.
3. Retarget serve/swing/recovery only after validating joint range, shoulder/hip weights and wardrobe coverage at extremes.
4. Align racket/golf grip sockets, then compare left/right-handed reach while preserving identical gameplay power.
5. Validate root-motion policy, loop seams, foot placement and the Unity Humanoid pose conversion before switching the live player.

**STOP:** await Adnan's approval before Animator or deeper tennis retargeting.

## UnityLookPolish — 2026-09-26 (v2; awaiting approval)

**Review:** `screenshots/unity_hero_vs_plate01_v2.png`. Original failure comparison is preserved. `screenshots/unity_hero_polish_before_after.png` uses the same Unity camera/crop for both versions. These are actual Unity Play Mode captures, not generated or painted corrections.

### Shoulder cause and correction

- **Weights plus runtime influence truncation, not an Avatar remap or scale repair.** Source sleeve-cap samples had up to 41% Neck influence and abrupt transitions into UpperArm. `anims/polish_shoulders.py` replaces the lateral shoulder region with continuous Chest / Shoulder / UpperArm blends; shared slot-boundary vertices retain identical normalized weights.
- The remaining cap notch was absent in Blender idle and Unity bind pose, but present in Unity idle. CPU-baked and skinned captures agreed. Humanoid joint positions closely matched the Blender clip. A controlled four-influence quality run removed the notch; returning to the project's two-influence tier reproduced it, even with renderer overrides. Thus renderer overrides alone were insufficient in this configuration.
- `ModularHeroLook` now requests four-influence skinning while one or more modular heroes are active, with reference-counted restoration of the prior setting when the last is disabled. All its renderers also explicitly request Bone4. This temporarily applies to other skinned renderers while the hero is active; saved project quality defaults are unchanged. Capture shutdown explicitly disables the review hero and restores the setting. No changes to `GolferModelImporter` in this phase.
- No sleeves removed, enlarged, or replaced. No rig scale/bone mapping changes. Shoulder tops are now continuous and rounded instead of notched.

### Hands, face, materials, lighting

- Existing hand vertices reduced by up to **14%**, blending to zero at the wrist; finger forms, weights, hand bones, and sockets retained. Grip offsets remain provisional until an approved racket/animation pass.
- Added two small white surface catchlights, joined into the existing eye meshes and weighted to Head. Dark brown iris and smile retained. No extra renderer or skeleton.
- Dedicated hair material variants brighten the blonde without tinting other atlas users. Existing atlas/normal maps retained; subtle normal-map strength added to URP materials. 181 existing shoe accent faces use a crisp `#FF6B3D` material. White/navy/orange polo remains intact.
- URP Lit remains the shader. Cloth smoothness .25, skin .32, visor .40; eye .38. No new shader framework.
- Review lighting: warm key .95, fill .38, soft frontal/upward face bounce .22; warm-neutral ambient .78. AO reduced to .18 / radius .05. Floor shadow strength .28 with high-quality soft filtering and 1024 main-light shadow map. Original camera/framing preserved; existing 4× MSAA pipeline setting and SMAA retained. The face/chin and floor shadows are softer. Lighting remains scoped to the review scene.

### Updated sources and validation

- Editable source: `anims/Hero_01_Mixamo_QA.blend`.
- Updated bind: `anims/Hero_01_Mixamo_Bind.fbx`; updated compatible Idle/Ready exports and all `anims/wardrobe/` FBXs. Pre-polish source retained in `anims/polish_before/`.
- Unity: `Assets/ArtDirection/Hero01/Models/`, materials, `Prefabs/Hero_01_Modular.prefab`, and `Assets/Scenes/Hero01Look.unity`.
- Ordered review captures: `polish_01_shoulder_weights.png`, `polish_02_shoulders.png`, `polish_03_hands.png`, `polish_04_face_materials.png`, `polish_05_lighting_final.png` under `screenshots/`.
- Blender verification passed: one armature, maximum four influences, no unweighted vertices or bad weight sums, zero shared-seam weight difference, identical skeleton signatures across all FBX exports (`anims/verification.json`).
- Unity validation passed: valid Humanoid Avatar, Humanoid Idle running, all wardrobe bones shared, eight renderers; captured with four skinning influences (`screenshots/unity_import.json`, `unity_playmode.json`). Saved `QualitySettings.asset` has no diff after the capture.

**Remaining visual gaps:** the inner sleeve underside has a small rough edge, fringe/ear geometry remains more angular than the plate, and orange shoe patch outlines still follow the original irregular mesh/atlas boundaries. Skin is warmer/yellower and the idle is more upright than the plate's racket pose. No claim of exact plate parity or validation on a physical iPhone. Sports grip/deep motion deformation remains for the approved animation phase.

**STOP:** v2 is ready for Adnan's review. Animator / tennis clips have not started.

## UnityLookPolish_v3 — 2026-09-26 (awaiting approval)

**Outputs:** `screenshots/unity_hero_vs_plate01_v3.png` (same camera/crop as v2), `screenshots/unity_hero_v2_vs_v3.png`, and staged captures `v3_01_limbs.png` / `v3_02_lighting.png`. Updated source remains `anims/Hero_01_Mixamo_QA.blend`, with bind/wardrobe/Idle/Ready FBXs re-exported and the Unity modular prefab rebuilt. The v2 source and capture are preserved in `anims/polish_v3_before/`.

### Leg diagnosis and Blender changes

- **Primary cause: existing rest-form geometry**, with inward knee/calf contours visible in Blender before animation. Existing custom normals and the skin normal map amplified the lumpy shading. All source faces were already smooth shaded; this was not simply an auto-smooth/crease toggle, an Avatar mapping error, or a need to hide the legs with clothing.
- Reshaped 1,352 exposed leg vertices into continuous, tapered cross-sections. Broadened the knee's normalized UpperLeg/LowerLeg weight transition. Recomputed local geometric normals instead of retaining the old sculpt's split-normal detail. The skeletal hierarchy and limb lengths are unchanged.
- Relaxed wrist/forearm surfaces with a gradual falloff while preserving finger separations. Small local sharpening restores existing finger grooves (maximum 1 mm displacement); no fused fingers or new hand rig.
- Lightly relaxed the polo sleeve caps while retaining the collar, cuffs, and Shirt slot. Original lower-body wardrobe boundary positions/weights are locked, with a 35 mm blend into the smoothed limbs. No new gap at the shorts or sock boundaries.
- Sharpened existing hair-clump ridges by at most 2 mm; reused the existing hair mesh. Shoe shells are up to 6% slimmer/shorter, fading to zero before the sock. Fragmented atlas-orange patches were cleared locally and replaced with fitted, outward-facing orange side stripes, using interpolated shoe skin weights. Added 160 triangles within the existing Shoes mesh; no new wardrobe system or renderer.

### Materials and lights

- Peach skin now uses the existing masked tint system, default input `#D5A080`, rather than preserving the orange source atlas. The final on-screen color includes lighting/tonemapping. A small optional `_SkinShading` property on the existing `KitRecolor` shader reduces retained baked shading to .45 for this hero; its default is 1, preserving existing callers' behavior. Skin normal mapping is disabled and smoothness is .25. No SSS or new shader framework was added.
- Visor smoothness reduced to .18. Hair's bright emission is reduced and its color multiplier is less yellow; stronger existing hair normal detail complements the clump geometry. Eye catchlights remain; eye smoothness is .44.
- White polo uses restrained normal detail. Warm key .9, cool-neutral fill .4, face bounce .27, ambient .68, exposure .08, saturation 0, AO .10. Existing soft-shadow filtering, 4× MSAA setting, and SMAA retained. Saved project quality settings remain unchanged; v2's scoped four-influence skinning remains required.
- Repeated runtime tint updates reuse the same texture/materials and update covered-foundation color as well. The review capture exercises that repeat-update path.

### Validation and intentional differences

- Blender verification passed: one shared armature, normalized weights, maximum four influences, zero unweighted vertices, identical exported skeletons and zero shared-seam weight differences. Direct comparison with original seam pairs gives **0 m separation** for 306 Shirt, 236 Shorts, and 79 Shoes boundary pairs. See `anims/v3-validation.json` and `anims/verification.json`.
- Unity capture: valid Humanoid Avatar, Idle running, shared wardrobe bones, eight renderers. No importer rewrite; neither `HeroLookImporter` nor `GolferModelImporter` was changed for v3.
- Intentional: existing upright breathing idle, relaxed/open hands, no held racket and no authored swing. This differs from the plate's wider racket-ready gesture. A static racket was optional and omitted so this review stays focused on the silhouette.
- Remaining: the fringe and inner collar/cuff edges are still more angular than the illustration; the sock boundary retains its original small contour variations. Finger detail is mesh detail, not newly articulated finger bones. This is an editor look review, not a physical-iPhone performance test.

**STOP:** await approval of v3 before Animator / tennis clips.


## UnityLookPolish_v3_SPECIFIC — 2026-09-26

**Acceptance status: FAIL / incomplete. Do not advance to Animator.** Numeric midpoint width checks and export/runtime validation pass, but the final capture still falls short of the locked head/hair design and completely smooth lower-leg contours. This section supersedes the earlier v3 “awaiting approval” wording. The previous v3 capture/source are preserved in `anims/specific_before/`; V2 remains in `anims/polish_v3_before/`.

### Measured DIFF

Actual Unity captures, unchanged camera/crop. Normalize sole-to-polo-hem height to the plate's 404 pixels (V2 389 px, new 387 px). Measure five cuts perpendicular to each limb around its anatomical midpoint (±8 pixels); report the larger left/right local maximum, not the mean. Cyan scan annotations: `screenshots/specific_measure_{plate,v2,after}.png`. Raw samples: `anims/specific-measurements-before.json` (includes final `after`). This is a 2D comparison with pose/perspective and approximately 1–2 pixel boundary uncertainty; it is not proof of the global maximum at every point along each limb.

| Cross-section | V2 normalized px | New normalized px | Plate px | New / V2 | Difference vs plate |
|---|---:|---:|---:|---:|---:|
| Biceps | 69.32 | 59.24 | 60.00 | 0.855 (-14.5%) | -1.3% |
| Forearm | 78.41 | 63.16 | 63.00 | 0.805 (-19.5%) | +0.3% |
| Thigh | 92.17 | 82.21 | 78.75 | 0.892 (-10.8%) | +4.4% |
| Calf | 95.81 | 85.60 | 86.25 | 0.893 (-10.7%) | -0.8% |

All four sampled maxima are below 90% of V2 and within ±5% of the plate. The earlier v3 smoothing had widened the thighs; this pass corrects that actual mesh volume. Blender radial factors relative to the archived *working v3* are biceps .865, forearm .84, thigh .635, calf .80. These differ from the V2 screen ratios because the working v3 geometry and projection differ. Full before/after metre measurements are in `anims/specific-blender-report.json`.

Shoes: bind-space plan width 0.26949 → 0.24254 m (left), 0.26921 → 0.24229 m (right); length 0.45485 → 0.40936 m / 0.45504 → 0.40953 m. Both axes −10%. Sole height remains 0 m / approximately 0.00004 m; scaling pivots at the ground. A defensible plate-space shoe length/width match and numerical ≤2 px edge test are **not yet established**; do not treat the 3D reduction alone as acceptance D.

### Geometry / weight edits

- Bind-space limb corrections affect Body plus matching Shirt/Shorts/Shoes regions, with smooth falloffs. No Unity transform squashing. Leg bone chain and corresponding mesh move inward 8 mm per side. Plate actually has a wider foot spread than this idle; the additional narrowing follows the requested direction but is not a pose match.
- Existing four-influence Chest / Shoulder.L/R / UpperArm.L/R cap blend retained, with local sleeve-cap smoothing and lowered shoulder peaks. 658 matching Body/Shirt cuff vertices assigned UpperArm.L/R only to remove Chest/Shoulder/LowerArm bleed and the inward hanging cuff flap. Shared boundary positions/weights remain matched.
- Forearm/hand correction follows LowerArm.L/R and Hand.L/R regions, hand factor .92, blended across wrist; retained normalized wrist transition. Existing UpperLeg/LowerLeg knee blends retained; thinning addresses geometry rather than disguising it with weights or light. Body/Shirt geometric normals recalculated. Remaining scalloping above socks is geometry and is not declared solved.
- Head/face width .93; small cheek/nose depth adjustment; eye spheres inset 1.5 mm, catchlights retained. Visor/socket raised 18 mm. Hair has four new Head-weighted swept clumps within the existing Hair slot. Lifted fringe boundary is closed with 103 skin bridge quads. **Result is still too capsule-like on top, with angular temple/fringe transitions.** It does not yet reproduce the plate's swept, layered hair silhouette.

### Materials / lighting DIFF

- Hair uses a solid matte `#D9AE70` material with soft self-shadowing, no atlas normal or emission. Eye smoothness .44 → .35. Visor remains .18, skin .25, cloth .22. Skin remains tintable peach input `#D5A080`, baked skin shading influence .45. Shoe orange remains solid non-emissive `#FF6B3D`; clean white `#EDEBE6`.
- Lighting unchanged from the preceding v3 pass: warm key .90, fill .40, face bounce .27, ambient .68; AO .10/radius .05; shadow strength .28, soft filtering; exposure .08; 4× MSAA plus SMAA. No darker lighting used to conceal defects. Soft hair self-shadow reception is the only new shadow-material change in this specific pass.
- Polo remains near-white and skin is less orange. Collar edge geometry still has small jagged patches, visible in the actual capture.

### Verification and acceptance audit

- `anims/verification.json`: passed; one armature, zero unweighted vertices, zero bad weight sums, maximum four influences, zero shared-seam weight difference, identical skeleton signatures across bind/wardrobe/Idle/Ready FBX exports. All five sockets retained.
- `screenshots/unity_import.json` / `unity_playmode.json`: valid Human Avatar, Humanoid Idle running, all wardrobe bones shared, eight renderers, four skinning influences. Updated modular prefab and review scene saved. No broad importer change or animation authoring.
- **A: partial / fail overall.** Sampled widths pass; small lower-leg/sock-edge lumps remain and a global maximum-width sweep has not been certified.
- **B: visual pass in this idle capture.** Armpit flap removed, sleeve caps rounded, no constriction ring at wrists. Extreme sports poses remain untested.
- **C: fail.** Higher matte visor, narrower face and eye catchlights are present, but hair/forehead/temple silhouette is still visibly unlike the plate.
- **D: unverified.** Shoes are 10% smaller with separate crisp-color geometry; exact plate shoe ratio and ≤2 px softness limit have not been numerically certified.
- **E: substantially improved, not an exact match.** Peach skin and near-white polo, matte visor and soft fill; collar boundary artifacts remain.
- **F: pass.** `screenshots/unity_hero_vs_plate01_v3.png` uses the same crop/framing; `screenshots/unity_hero_v2_vs_v3.png` shows the real before/after. No capture retouching.

**STOP: not approved for Animator. Review the v3 image and the failed criteria above before authorizing the next phase.**


## UnityLookPolish_v4_MICRO — 2026-09-26

### HERO LOOK LOCKED: NO — awaiting Adnan's explicit “lock”

V4 review artifact: `screenshots/unity_hero_vs_plate01_v4.png`. Same 900×1100 Unity camera capture and comparison crop/layout as V3. `screenshots/unity_hero_v3_vs_v4.png` shows the actual before/after. These are unretouched Unity Play Mode captures. V3 source/capture/build script preserved under `anims/v4_before/`.

| Category | Result | Evidence / remaining issue |
|---|---|---|
| Eyes | PASS for this capture | Dark brown irises, two distinct sharp white dots per eye. Existing domes were behind the original face surface; moved them forward 6 mm without scaling, changing topology, or altering the face mesh. Front-projected UVs keep both dots visible. 512px uncompressed procedural iris map; specular/environment reflections disabled to remove competing highlights. Old extra glint faces are transparent. |
| Hair | FAIL strict clean-edge target | Warm clean blonde `#DDB275`; cap shading normals repaired and self-shadow reception disabled to remove dark artifacts. Hair is opaque geometry, not alpha cards; alpha clipping/texture upscaling would not repair its stepped temple silhouette. Four existing tufts retained. Boundary geometry remains visibly angular under the visor and at temples. |
| Shoes | PASS for micro shading target | Isolated clean-white material removes baked grey patches; existing orange geometry remains crisp/non-emissive. Smoothed area-weighted normals and .18 smoothness. Shoe size/outline unchanged from V3. This is a visual review result, not a new numerical edge-width certification. |
| Cloth | PASS shading; FAIL neck-edge finish | Near-white polo retains broad soft folds; shorts retain navy shading. No thick new orange trim or emission. The upper collar/neck boundary still has small jagged gaps/steps. No new pocket piping was added. |
| Light | PASS for micro target | Soft peach skin, matte visor, soft chin shading, no hard black armpit pits. Key .90→.86, fill .40→.34, face bounce .27→.30, ambient .68→.60; AO .10/radius .05 retained. Capture RT and review pipeline explicitly use 4× MSAA, with SMAA. |

### Scope and verification

- **V3 limb/body proportions, stance, hair/hat/clothing coordinates, topology and weights remain locked.** No limb re-thinning, body remesh, extra polygons or new skeleton. The only coordinate exception is the existing eye domes' 6 mm depth correction. Eye UVs and shading normals changed. `anims/v4-mesh-lock.json` records the pre-eye-depth mesh hashes and explicit exception.
- Edited source `anims/Hero_01_Mixamo_QA.blend`, re-exported bind/wardrobe/Idle/Ready FBXs, rebuilt the Unity modular prefab and review scene. No importer rewrite, no new shader framework, and no animation authoring.
- Stock URP Lit remains in use. The invisible legacy auxiliary glint material uses stock URP Unlit transparency. Shoe-white material is isolated to avoid recoloring shared cloth materials.
- Skin retains the existing masked peach tint (`#D5A080`) and .45 baked shading influence. No SSS implementation or cheek decal was added; warm albedo and softened face normals are used within the bible's inexpensive fallback. Existing eye socket/eyelid mesh remains unchanged.
- `anims/verification.json`: one armature, normalized weights, maximum four influences, no unweighted vertices, identical FBX skeletons. `screenshots/unity_playmode.json`: valid Human Avatar, Idle running, all wardrobe bones shared, four skinning influences. Eight renderers retained.
- No optional racket or stance change. No physical-phone performance validation in this phase.

**The full clean-hair/neck target is not met.** Geometry outlines cannot be fully corrected by this shading-only pass without relaxing the locked geometry constraint. Review the delivered V4 capture; only Adnan's explicit “lock” authorizes the next phase. Animator / Mixamo tennis clips have not started.

## WardrobeMatch_LegsHatHairShorts — V5 — 2026-09-26

**Review, not user-locked:** `screenshots/unity_hero_vs_plate01_v5.png`, same camera/crop/layout as V4. `screenshots/unity_hero_v4_vs_v5.png` provides the direct before/after. Actual Unity Play Mode, shared Humanoid Idle; no image retouching. Prior source and capture preserved under `anims/v5_before/`.

### Mesh work, in requested order

1. **Shorts_Default:** eased each leg opening outward, maximum 10% local radial flare fading to zero above the hem region. Waistband/crotch retained. Added 856 vertices of surface-conforming orange pocket/side piping, 5 mm diameter. Smoothed paths and blended source-face bone influences, normalized and limited to four. Navy atlas retained; orange is a separate opaque, non-emissive material `#FF6B3D`. New slot total: 7,530 triangles.
2. **Hat_Visor:** replaced the cut-off cap with a narrow 28 mm headband and curved brim; approximately 3 mm brim shell. White main surface with 6 mm navy rim and 1.5 mm orange edge. Forehead and eyebrows remain visible. Head-weighted; same Hat_Visor slot and existing Hat socket. Total: 1,856 triangles. White material uses a small .14 emission fill to keep the downward-facing underside from reading grey; there is no bloom/glowing orange accent.
3. **Hair_Default:** replaced the former cap/added upright bumps with a continuous smooth opaque cap and four broad swept crown ribbons. Clean blonde `#EDBB68`, no alpha texture, no noisy normal map, no hair self-shadow artifacts. Same Hair_Default slot, Head weighting. Total: 6,912 triangles. Body hair-colored material patches at the join were reassigned to the existing skin foundation material; no head geometry rebuild.
4. **Body_Skin legs:** locally relaxed 4,305 vertices within z .235–.64 m, preserving boundary vertices and applying smooth falloffs near joins. Recalculated leg normals to remove faceted/dented shading. Existing thigh/knee/calf volume retained with smoother transitions toward ankles; no arm bulk increase, no skeleton edits. Warm peach tint remains `#D5A080`, skin shading influence .45. No SSS framework added.

### Changed mesh/export files

Editable assembly: `anims/Hero_01_Mixamo_QA.blend`.

Geometry changed in:
- `anims/wardrobe/Hero_01_Shorts_Default.fbx`
- `anims/wardrobe/Hero_01_Hat_Visor.fbx`
- `anims/wardrobe/Hero_01_Hair_Default.fbx`
- `anims/wardrobe/Hero_01_Body.fbx`
- `anims/wardrobe/Hero_01_Body_DefaultCoverage.fbx`
- `anims/Hero_01_Mixamo_Bind.fbx`

`Idle.fbx` and `Ready.fbx` re-exported with updated body references; no clip authoring. Shirt/Shoes exports refreshed by the existing export script but their source coordinates are unchanged. Corresponding Unity `Assets/ArtDirection/Hero01/Models/` assets, materials, `Prefabs/Hero_01_Modular.prefab` and `Assets/Scenes/Hero01Look.unity` updated. Existing importer remains unchanged. Authoring script: `anims/wardrobe_v5.py`.

### Acceptance review

| Requirement | Capture assessment |
|---|---|
| A — legs | Pass for this idle view: smoother rounded calf/thigh gradients and ankle taper, peach rather than grey. No new long cylinder primitive or global limb scale. |
| B — visor | Pass for this view: thin curved white visor, navy/orange rim, visible forehead and unobstructed brows. Brim pitch remains an interpretation of the illustrated plate rather than an exact traced profile. |
| C — hair | New opaque mesh passes the clean shading/bright blonde requirement; four authored crown clumps, three most prominent from this angle. The existing ear/temple face geometry is still angular below the hair and remains a visual gap against the plate. Adnan must judge final silhouette closeness. |
| D — shorts | Pass: orange pocket and side lines immediately visible, retained navy, subtly flared hem. Existing upright idle differs from the plate's wide ready stance. |
| E — output | Pass: V5 comparison and file list supplied. |

**Verification:** `anims/v5-scope-verification.json` confirms unchanged Shirt, Shoes, both eyes, skeleton, and zero body coordinate changes outside the leg region. `anims/verification.json` passes: one armature, no unweighted vertices, normalized weights, maximum four influences, zero shared-seam weight difference, identical skeleton signatures across all exports. Unity validates a Human Avatar, running Idle and shared wardrobe bones. Four skinning influences and eight renderers retained. Physical-device performance and sports-motion extremes were not tested in this look phase.

**STOP:** Await Adnan's approval of V5; no Animator or tennis clips started. The remaining angular ear/temple geometry is disclosed rather than claiming exact plate parity.

## Active selection — V4 restored — 2026-09-26

Adnan requested “nvm use v4.” Restored the final V4 Blender source and Unity review/material setup from `anims/v5_before/`, re-exported bind/wardrobe/Idle/Ready FBXs, and rebuilt the Unity prefab and scene. Current active look is **POLISH V4**, not Wardrobe V5. Fresh Unity capture confirms the restored V4 wardrobe; Human Avatar, Idle and shared wardrobe binding pass.

V5 is retained under `anims/v5_archived/` (source, review script, capture), with its authoring script and V5 comparison preserved for reference. V5 acceptance notes above are historical and do not describe the active model. This selection does not start Animator or tennis clip work.
