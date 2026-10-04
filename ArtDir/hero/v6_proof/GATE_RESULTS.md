# GATE: V6 FAIL

2026-09-30 — **Rejected candidate, not a shipping hero.** No promotion over V4/V5.
The requirement is zero deficiencies. Closed meshes and successful imports do not establish visual acceptance. Visible failures below remain; missing verification is a FAIL.

## Evidence index

- [Before / after / authority](before_after.png) — identical Unity Ready sample and camera for V5/V6; authority is a separate illustration.
- [Failure evidence](failure_evidence.png) — neck, knee, serve, hair and material defects remain visible.
- [Four-angle Blender turnaround](turnaround.png).
- [Nine Unity clip samples](clip_samples.png).
- Original captures: [Unity](Unity/) and [Blender](blender/).
- [Runtime materials + sockets](Unity/runtime-materials-and-sockets.txt), [motion + swaps](Unity/motion-and-swaps.txt), [mesh audit](export-geometry-audit.json), [source hashes](source-preservation.json).
- [Customize contract](CUSTOMIZE_CONTRACT.md).

## Binary kill list

| # | Gate | Result | Evidence / failed line |
|---|---|---|---|
| 1 | Zero background visible through hair from all required views | FAIL | Crown seating was repaired, but the visor/fringe join still has exposed gaps and faceted overlaps. `Unity/customize_closeup.png`, `Unity/hair_LOD2.png`; zero-gap certification is not supported. |
| 2 | No scalp slice / floating volume / missing backfaces | FAIL | Removing the duplicate scalp dome removed the horizontal dome seam, but the crown still reads as layered shells rather than the target's integrated locks; neck triangles remain exposed. |
| 3 | Solid hair Opaque; no transparency/dither | PASS | All six default/free LOD meshes have zero boundary/non-manifold edges. Runtime URP Lit `_Surface=0`, `_AlphaClip=0`, queue 2000. This passes the technical condition only; required Inspector image is missing under proof gate. |
| 4 | Plate-stocky calves and natural ankles | FAIL | Rebuilt quad-loop calves are fuller, but silhouette/ankle join still misses the target. `Unity/knees_feet.png`. |
| 5 | Knees retain volume under bending | FAIL | The ready bend still creates a crease/concavity. `Unity/knees_feet.png`, `blender/stress_knees_side.png`. |
| 6 | Correct shorts / continuous piping / gameplay dive-safe | FAIL | New separate quad garment and piping exist. Blender lunge renders do not prove the full procedural gameplay dive. Full gameplay dive collision acceptance was not obtained. |
| 7 | Visor cleanly over hair | FAIL | Jagged fringe/visor intersections and the source visor's irregular edge remain. `Unity/customize_closeup.png`. |
| 8 | Serve: no armpit webbing or arm-through-polo | FAIL | Visible shirt distortion and dark slit near raised shoulder. `Unity/serve_armpit_back.png`. |
| 9 | Grounded feet; clean symmetric ankle joins | FAIL | Feet follow original rig but ankle/cuff joins remain angular. Full grounded dive/run acceptance is not established. |
| 10 | Hair LODs stay opaque solid meshes | PASS | LOD0 8,256 tris; LOD1 4,788; LOD2 2,476. No open/non-manifold edges at any level. `Unity/hair_LOD0.png` through `hair_LOD2.png`. Visual silhouette quality remains failed under #1/#2. |
| 11 | Hair-off complete scalp, no interior hole | FAIL | Existing crown is restored, but the bare-head review exposes ragged neck/under-head geometry. `Unity/hair_off_back.png`. Not approved as a complete bald customization. |
| 12 | Hair / Visor / Top / Bottom / Shoes / Racket sockets valid | PASS | All six aliases are present after the headwear swap cycle, original slot enums and racket grip retained. Active-slot lookup fixes the alias being attached to a pending-destruction old hat container. See runtime paths. Direct external Equip calls still require V6 RefreshSlots. |
| 13 | Soft-plastic material quality matches target | FAIL | Face/polo remain grey and patchy, visor overly glossy, shoes muddy. Source atlas and legacy runtime material overrides still need coherent authoring. No claim that roughness alone fixes this. |
| 14 | Body supports future clothes; no outfit-only dead end | FAIL | Clothes remain separate, but the default export uses covered-body masks. The upper/lower skin join and hidden foundation regions are not a clean, complete interchangeable body. Full authoring leg variant is retained; that does not pass the complete-body gate. |
| 15 | No new exposed neck/face defects | FAIL | Neck has jagged overlapping triangles after restoring source coverage. Captured eye state also shows conspicuous lid/eye joins. No face regeneration was attempted. |

## Additional required gates

| Gate | Result | Evidence |
|---|---|---|
| Original face vertex positions preserved | PASS | All staged face hashes remain `8c07039fa7338005d04b8c235f4227620d3027fed453715ad0e362dfb9bb0922`; no face regeneration. This measures coordinates, not visual plate similarity. |
| Original rig rest matrices preserved | PASS | `../v6_polish/build-stage1.json`. Mesh-only FBX rebound to existing Humanoid. |
| Original animation wiring; all nine clips move | PASS | Ready, Serve, Forehand, Backhand, Run F/R/L, Volley, Smash sampled at 60 Hz for male/female shape modes. All finite; no frozen wrist paths. This is not a collision or skin-volume test. |
| Approximately 1.7 m humanoid | PASS | Original joints retained; new crown near 1.7 m in Blender; no runtime scale squash. |
| Same hero with target-quality silhouette and face presentation | FAIL | Face coordinates are preserved, but the rendered head/hair/material treatment does not match authority quality. |
| Clean non-overlapping UVs for every part | FAIL | New parts have UV layers; complete overlap/texel-density/atlas acceptance is unverified. Existing UVs reused for retained parts. |
| Every modular part has a complete mobile LOD set | FAIL | Hair has three mesh levels; body/wardrobe retain full geometry. |
| ~50k total runtime triangles | FAIL | Base default export: 48,510. Actual visible runtime count including procedural eyes, occluder, liner and racket: **54,637**. No budget exception assumed. |
| Required proof pack complete | FAIL | Before/after, four-angle, hair-off, close-up, knees/feet, Ready/Serve, LOD and Blender lunge exist. Missing actual gameplay dive proof and actual Unity Inspector screenshot. Property dump is not represented as an Inspector screenshot. |
| All kill-list lines closed | FAIL | See failures above. |

## Work performed

1. Froze source hashes and captured V5 before views in Blender and Unity.
2. Created `Hero_V6_Polish.blend`; rebuilt default hair as closed opaque locks/core plus no-hat variant. Removed added generic scalp dome after it produced a seam; restored existing source head coverage.
3. Replaced lower legs with authored quad loops and normalized thigh/knee/ankle weights; added separate rebuilt shorts and orange piping; narrowed shoe width 7%; adjusted torso-side armpit weights. Added bounded slim/broad shapes to corresponding body/wardrobe pieces.
4. Exported duplicate mesh FBX, rebound into a copy of the existing Humanoid prefab, retained clip array and tool grip. Added V6-only LOD/slot adapter components.
5. Ran Unity Play Mode captures and finite-motion/binding checks. Found and reported visual failures rather than promoting the asset.

## Source preservation / scope

All **69** baseline-tracked source files have identical before/after hashes, including the restored V5 blend, tracked Hero01 FBXs/clips and live hero prefabs. Existing unrelated dirty work was left in place. No Tripo, Higgsfield, Mixamo operation, animation rewrite, or broad importer rewrite.

## Candidate locations

- `ArtDir/hero/v6_polish/Hero_V6_Polish.blend` — authoring, failed candidate.
- `ArtDir/hero/v6_polish/Hero_V6_Export.blend` — evaluated/export workset.
- `Unity/Assets/ArtDirection/HeroV6Polish/Hero_V6_Polish.fbx`.
- `Unity/Assets/ArtDirection/HeroV6Polish/Hero_V6_Polish.prefab`.
- Repro scripts: `ArtDir/hero/v6_polish/tools/`.

**GATE: V6 FAIL. Do not promote or label this hero finished.**
