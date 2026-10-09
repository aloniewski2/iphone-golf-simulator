# Hero V5 "Look Ship" gate results (Phases 1–5), 2026-09-28

**GATE: FAIL.** Four lines fail; everything else passes. Details below.

## Failing lines
1. **Likeness to v5_target: about 82%, target 85–90%.**
   - The lighting now matches the target's warmth.
   - The shorts and shoes are still the V4 designs, resized. The target has a curved piped hem and an orange heel and sole.
2. **No-hat hair still shows a band line.**
   - No-hat now uses the un-pressed Tripo sculpt (`Hair_*_Free`), so the flat groove is gone.
   - Tripo's completion rebuilt a smooth hair ribbon where its visor sat, and it reads as a hair headband. See `nohat_vs_visor_locker.jpg`.
3. **Visor strap from behind.** With a visor, long locks poke through the thin back strap, so it looks broken on Bob, Long and Swept (`styles_blender.jpg`, bottom row).
4. **60 fps on device is not measured yet.**
   - What was added: the fill light (one extra directional term, no shadow map), Tripo trees (5k tris with a 900-tri LOD1), and ten hair meshes of which only one draws.
   - It needs an on-phone check.

## Passing lines
| Line | Evidence |
|---|---|
| Customize light ≈ hair-target beauty | Warm key, cool fill, rim and soft plinth shadow in the locker (`LIGHTING.md`, `locker_studio_phone.jpg`, `styles/`) |
| Gameplay light warm, faces not gray | Sun at 40° plus a camera-side fill at 35%. Rival faces are lit: `env_before_phase2.jpg` vs `env_phase2_light.jpg` |
| Trees / env beside hero | The blob broadleaf trees are replaced by a sculpted Tripo toy tree with LOD (`env_phase3_trees.jpg`). The locker backdrop is the finished painted room |
| Hit VFX doesn't white out the face | The additive impact core is capped at .95–1.2 m and 75% alpha. Screen flash is at most 25% |
| Rival = player rules | Same `HeroKit` path. Rivals get haircuts and girl bodies (`ingame_lineup_styles_racket.jpg`) |
| Hair solid, any cam | Opaque sculpted hair for all 5 cuts. No see-through in locker renders (`styles/`) |
| Females | 'Female' blend shape on body, shirt and shorts (waist, hips, shoulders, bust). Game, locker and rivals all use it |
| Multiple hairstyles | Swept, Ponytail, Bob, Long, Curly, each Tripo-sculpted. Ponytail, Long and Curly drape to the chest bone |
| Racket reads as tennis | Drawn 1.3× (head 0.38 × 0.52 m). The hitbox (`StringHalfWidth/Height` .19/.26) equals the visible head |
| Tests | NativeLaunchHeroTests ×2, Score80WardrobeAudit, locker proof + styles proof + locker-tab snapshots pass |

Known failing tests that predate this work:
- `TennisRulesTests.HardStrokesHaveSmallerContactWindowAndReach` and `ReachAssist…`: the party-window reach changes.
- `TennisTutorialTests.KitColoursParse…`: skin always counts as a recolour.
- `testLoadingIdlesRespectReducedMotionAndSport`: expects the old non-hero racket node.

## Locker redesign (phone)
- Layout follows cosmetics-shop screens: a big turntable stage, and tabs for Body, Hair, Headwear, Outfit and Racket.
- Hairstyles and headwear pick from rendered head-shot cards. Hair and headwear zoom the camera to the head.
- Every colour is a continuous range:
  - skin: a fair → deep ramp;
  - hair: a natural ramp plus a dye hue slider;
  - shirt, shorts, shoes and racket: hue + shade (dark → vivid → pastel).
- Quick swatches and "Kit" stay.
- The exact hex goes to the game. The TV remote steps along the same ranges.

## Tripo spend this pass
- 3 styles (bob, long, curly): about 130 credits each.
- Tree: text-to-image plus image-to-model.

## Smallest next fixes
1. Rebuild the shorts and shoes to the target (piped hem; orange heel and sole). This is the likeness line.
2. No-hat band ribbon: sculpt it out, either with a Tripo re-completion without a visor prompt or with a smooth-push in Blender.
3. Back strap: press the long cuts under a wider strap zone, or thicken the strap.
4. Profile on the phone.
