# Bald-base hero pass, 2026-10-12

Brief: improve the existing male and female heroes towards the "after" rows of `ArtDir/hero/targets-2026-10-12/bald-base/`.
They stay bald and keep their body, proportions, face shape, skin tone and rig. The animations are not touched. Nothing is committed; the changes are staged on `build-10/12`.

## What changed

| Request | Cause found | Change |
|---|---|---|
| Remove the dark, muddy shading around the nose and eyes | The renderer's **Contact AO** (URP SSAO in After Opaque mode, 0.2 m radius) is one full-screen multiply, so it filled the face's small cavities (eye sockets, nose wings, ears, the neck under the jaw and the gaps between fingers) with noisy dark blotches. The hero meshes and shaders were not the cause: with Contact AO off, the faces are clean. | New renderer feature `HeroAmbientOcclusionExemption` (in `TennisURP_Renderer.asset`, after Contact AO). It redraws only the hero renderers (the `HeroRimLight` rendering layer) right after the AO multiply. The courts, venues and hub keep their AO, the ground keeps the hero's contact darkening, and the depth texture still holds the heroes. It costs nothing when Contact AO is off (Standard and Low tiers). |
| Softer, less orange skin with a light cheek blush | Skin shading was pushed toward orange by the subsurface tint, the light-bleed tint, the face ambient tint and the ear tint, at saturation .88. | `MatchHeroLook`: saturation .74 for light and mid tones, easing back to .88 for deep tones (`SkinSaturationFor`), plus a rosier subsurface tint. `HeroFaceSkin.shader`: neutral face ambient, rosier light bleed, a rose blush on the cheek apples, lighter nostril and lower-lid pigment, and the male beard shadow halved. The palette tone (`_BaseColor`) is untouched, so every skin pick still works. |
| Ears a bit smaller and cleaner | Ears were 74 mm (male) / 92 mm (female) tall, standing 26 / 31 mm off the skull. The dark ear outline was Contact AO. | `HeroFaceReferenceFit`: the ear shrinks 14% (male) / 16% (female) toward its own centre, with a soft falloff into the scalp. Normals and blink shapes go through the same map, and the head-only invariants still hold. |
| Fingers wrap the handle cleanly, no poking through | The old corrective assumed a 22 mm handle (17 mm × the 1.3 racket scale) against a real 17.4 mm one, and skipped fingertips that were already inside it. Tips were up to 4.5 mm inside the handle. | `HeroGripPolish.WrapDigits`: each finger's three hinges are solved on its own skin samples, so no skin goes inside the handle (1.2 mm soft-contact tolerance) and every phalanx rests on it. In play mode it runs on alternate frames. (Pass 2 made it a function of the pose alone; see below.) |
| Clear sole line and clean sock edge (tennis kit) | The kit masks give about 0.6 texels per mm on the sole wall, too coarse for a crisp line. | `HeroKitEdges` stores per-vertex measurements in UV3 of the shoe and sock meshes, and `TennisCloth` (`_EdgeLines`) draws an anti-aliased grey seam where the upper stands on the sole, a light rubber band along the bottom 6.5 mm of the sole, and a ribbed sock cuff with a crisp edge. It's applied after the tone shoulder so white kit still shows it. Colour picks are unaffected. |
| Golf female skirt closer to the body, hands visible | Hem 31 cm from the hip axis over 17 cm hips/thighs, so the hanging hands (about 26.5 cm out) were inside its flare. | `HeroGolfSkirtFit` (garment only): the air beyond the body's envelope (+1.2 cm) shrinks to 36% from just below the hip joint to the hem, so the radius now grows steadily (about 16 → 21 → 23 cm). Blend shapes, normals and tangents follow the same map. |

Also: `Unity/Assets/Editor/HeroTurnaround.cs` (the staged capture tool) now draws one throwaway frame before its first shot. A fresh batch run otherwise draws that first frame before the hero shaders are ready, giving a yellow or grey placeholder hero.

## Not done / limits

* **The grip is not yet the "four fingers curled round, thumb over" fist from the target.** The racket sits under the finger pads, not across the palm. Probing more curl drives the pads 20–45 mm into the handle, so a real fist needs the racket re-seated in the hand. That would move the racket in every clip, so it was left alone because of the no-animation-changes rule. Fingers no longer pass through the handle: deepest skin contact is 1.1 mm (male) and 0.4 mm (female), down from 4.5 mm.
* The female two-hand grip in the target needs the left hand moved onto the handle, which is an animation change. Not done.
* "Chunky cartoon fingers" (detail sheet, relaxed hand) was not in the requested list, so the finger meshes are unchanged. The relaxed hand now reads cleanly only because the AO grime is gone.
* (Pass 1) The lobby was not re-exported then; pass 2 did it (below).
* The tennis sole is still the authored cream colour. Only the line and the rubber band were added.

## Proof and how to re-check

* `work/model-turnaround/run.sh` writes the new contact sheets in `work/model-turnaround/`. The old ones are kept in `work/model-turnaround/before-2026-10-12/`.
* `python3 work/bald-base-2026-10-12/make_compare.py` writes `proof/compare_<hero>.png` (changed close-ups, before over after), `proof/compare_all.png` and `proof/sheets_<hero>.png` (old and new contact sheets side by side).
* Gates and measurements: `GATE_RESULTS.md`.
* The final capture was rendered from an APFS clone of `Unity/` (`UNITY_PROJECT=<clone> run.sh`), not the shared project. A parallel session was writing `Assets/Scripts/Hub/HubClub.cs` at the time, which did not compile then. The clone matches the repo byte for byte for every file this pass touched.

---

# Pass 2 (same day): faces, ears, shoes, grip, and lobby = game

Brief, in order of importance: the lobby / locker / menu heroes must look exactly like the in-game Unity heroes, with one step that keeps them in sync. Faces softer and friendlier. Ears a bit smaller again. White sneakers without the heavy grey trim. The grip may re-seat the racket in the hand, but swings, timing and body motion stay exactly the same. Bald, same body, height, proportions, skeleton and animations. Stage only.

| Request | Change |
|---|---|
| Smaller, narrower nose | `HeroFaceReferenceFit` (head only): the lower nose narrows 24 % (male) / 22 % (female) at the nostrils, with a slight tuck. The bridge and the face outline stay put. |
| Eyes a little bigger and rounder; female eyes slightly closer together | Same warp: the eye openings grow (male 8 % wider, 55 % taller; female 20 % taller) and the irises scale with them (male 16 %, female 6 %). The female eyes move 3 mm in towards the nose. The brows stay where they were. |
| Softer, smaller smile | `HeroFaceSkin`: the painted mouth is narrower (male 41 → 35 mm, female 33 → 28 mm) with a gentler corner lift. The male mouth geometry is no longer stretched wide (was 16 %, now 6 %). |
| Cheek blush that is actually visible | `HeroFaceSkin`: a stronger rose blush on its own cheek-apple region, about 3 cm under the outer eye. |
| Ears a bit smaller again | A second ear shrink in the same warp (male 14 %, female 16 %), centred on the ear. Ears are now well under their original size on both heroes. |
| Shoes read as clean white sneakers | New `HeroShoeShape` (garment only): seen from above, the toe tapers and its corners round off, and the heel rounds a little, without ever pushing into the foot or sock. Shoe trim is now 93 % of the shoe colour (it was 65 %, the heavy grey), and the white starter shoe trim is near-white. Far-away players get the same shoe (see "Distance kit"). |
| Grip: fingers curl round the handle, thumb over | New `HeroRacketSeat`: the racket's meshes move 2 cm (male) / 0.7 cm (female) inside the racket's own frame, so the handle sits deeper in the fingers. The string-bed centre that gameplay uses moves with it, so the visible racket is still the gameplay racket. No clip, bone, swing or timing is touched. `HeroGripPolish` now closes the fingers like a grasp, and the thumb closes too. **Limit:** a full fist still isn't possible without turning the racket 25–40° in the hand, which would move the racket head about 20 cm and change every swing. So the result is a firm hook grip with the thumb engaged, not the target's full fist. |
| Grip is the same everywhere | The solver used to warm-start from its previous answer, so the same pose could give a different grip (the lobby export's self-check caught a 98 mm difference). Every solve now starts from the clip's own pose. The game, the turnaround and the lobby export get the same grip for the same frame (checked: 0.0000° difference whatever was solved before). Cost in the editor's Mono runtime: about 1.25 ms (male) / 0.54 ms (female) per solve, run every other frame. |
| Distance kit | The game swaps in a lighter kit when a player is small on screen (`HeroGarmentLOD`), and the lobby draws that same Distance export. Neither had the new shoe shape or the sole and sock lines. `MatchHeroLook.FinishFootwear` now prepares both kits the same way, as do `HeroGarmentLOD` and the export. |
| **Lobby = game** | Re-exported all 66 `MatchHero_*` / `GolfKitHero_*` files with every change above. `MatchHeroSurfaces.swift` now ports Unity's `HeroFaceSkin`: same lips, lid liner, blush, beard, face light, highlight roll-off and per-tone saturation. It also draws the shoe sole line and sock cuff, and the shoe trim follows the new rule. The plain cloth lobby exposure went from 1.4 to 1.0: the old value was tuned against a broken comparison. One step keeps them in sync: `work/hero-sync/sync_heroes.sh` (see its README). A native test fails if any hero-look file changes without a re-sync. |

Proof:
- `proof/target_*.png`: TARGET / BEFORE / NOW per hero. `proof/compare_*.png` and `proof/sheets_*.png` come from `make_compare.py`.
- `work/hero-sync/sheets/`: lobby vs game from the same cameras and lights, plus `PARITY.md`.
- Gates: `GATE_RESULTS.md`, pass 2 section.


---

# Skin pass (same day): matte, warm form, golden tan

Brief: ours looked waxy, flat and pinkish next to the target heads. The fix is skin shading only (`HeroFaceSkin` + the `MatchHeroLook` skin settings): no mesh, face shape, body, rig, animation or clothing changes, and every skin-tone pick still works. The blush is unchanged.

| Request | Change (Unity `HeroFaceSkin.shader`, mirrored in the lobby's `MatchHeroSurfaces.swift`) |
|---|---|
| Matte | **Sheen:** the broad forehead / nose / lip gloss (up to .24) is now a faint wide sheen (.045 × highlight strength .40, was .95). **Smoothness:** .26–.34 (was .32–.50). **Other:** scene reflection on skin at 30 %, skin rim .45 → .30. |
| Soft form shading | Warm, darker pigment (towards .80 / .66 / .55) under the brows, at the inner eye corners, beside the nose, along the smile lines, under the cheekbones, at the jaw sides, under the jaw and down the neck. Each zone was placed on the measured bind-pose surface of each head, and the neck zone stops at the neck's width. A soft light-from-above lift (forehead and crown up to 6 % lighter than the jaw). A lower light wrap (.20–.26, was .28–.50), so the face's own form reads. |
| Warmer colour, warm shadows | **Hue:** golden grade ×(1, 1.035, .92) on all skin (relative, so tone picks keep their depth); regional warmth retuned from pink (1.035 / .93 / .91) to golden (1.03 / .965 / .89); the female face lift is golden (1.08 / 1.09 / 1.04) instead of cool (1.10 / 1.16 / 1.26). **Shadows:** the ambient fill in shadow is warm, ×(1, .86, .72). The light bleed at the terminator is reddish-brown (.24 / .10 / .04, was .20 / .12 / .09), and the edge warmth `MatchHeroLook.SkinSubsurface` is .13 / .05 / .02. |
| Natural variation | Ears and nose tip a touch warmer (both heroes). A very faint skin grain: about 3 mm and 18 mm value noise in the bind pose, at most ±2.5 % / ±3 %, faded out before it can alias. |
| Lobby | Re-synced with `work/hero-sync/sync_heroes.sh`. Only the 6 manifests changed (the skin numbers); every mesh is byte-identical. The SceneKit port warms its ambient by adding `colour × (new − old)` to `_surface.ambient`: SceneKit adds that term on top of its own ambient, and negative values work. |

Proof:
- `proof/skin_*.png`: TARGET / BEFORE / NOW face close-ups, front, three-quarter and side, per hero. The turnaround's studio skin tone (B7744A) is darker than the target's, so compare the finish, not the tone.
- `proof/app/lobby-skin-tv.png`: the real app lobby in the simulator.
- `work/hero-sync/sheets/`: lobby vs game, same cameras and lights.

---

# Jawline pass (same day): defined, sharp, obvious

> **Replaced (same day, later):** Adnan asked to stop shaping the jaw with code. The warp block, the shader band and its lobby port described below were removed. The jaw is now sculpted into the head meshes in Blender: see `work/jaw-sculpt-2026-10-12/README.md`.

Brief: the male and female jawlines weren't close to the reference heads; they need to be defined, sharp and obvious.

**Cause.** Measured on the source heads, in the warp's own frame:
- **Male:** the face is as wide as the neck (about 71 mm each side of centre) from y −.09 down to −.15, so jaw and neck read as one block.
- **Female:** the face narrows in a smooth V straight into a neck of the same width.
- **Both:** the jaw's lower border is a gentle 45° slope, not an edge.

| Change | Where |
|---|---|
| **Jaw border curve:** measured per head. It runs from the chin (male y −.127, female −.128) up to the angle (male x 72 mm, y −.103; female x 62 mm, y −.090), and is flat beyond the angle. A border that kept rising folded the female cheek. | `HeroFaceReferenceFit.Warp` (head only) |
| **Undercut:** just under the border, the surface is pulled in toward the neck axis, up to 13 mm (male) / 9 mm (female). It is deepest in front of the neck and gentler at the sides, where the polo collar stands. It fades out 2.5–4.5 cm below the border and never reaches the neck line (`belowHeadChanges = 0`). | same |
| **Crest and jowls:** a 2.4 / 1.8 mm lift right on the border catches the light, and the lower cheek just above it leans in up to 5 / 3 mm, so the border is the widest line of the lower face. | same |
| **Shadow and light catch:** a warm shadow band hugs the new edge (0–4 cm under it), with a faint light catch on the edge itself. It replaces the old fixed-height under-jaw band. The shader frame is the warp frame plus 9.9 mm in y, fitted exactly on vertices the warp never moves. | `HeroFaceSkin.shader` and the lobby port in `MatchHeroSurfaces.swift` |

Checks:
- Mesh invariants hold: topology, weights and bind poses identical; nothing below the neck line moves. Minimum Jacobian: female .169, male .220 (> 0, no folds).
- Re-synced with `work/hero-sync/sync_heroes.sh`: 63 files changed, export self-checks ≤ .001 mm, lobby vs game within 3 % on the heads.
- `proof/jaw_*.png` is TARGET / BEFORE / NOW. Those head-and-neck framings come from a turnaround run with three extra close-ups added to a clone copy of `HeroTurnaround.cs` (`Close("jaw_front" | "jaw_three_quarter" | "jaw_side", …, Head, 0 | 40 | 90, 0, true, true, 1.55f)`). The repo tool is unchanged.
