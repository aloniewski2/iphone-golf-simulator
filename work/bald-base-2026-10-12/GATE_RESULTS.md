# GATE_RESULTS: bald-base pass 2026-10-12

Measured on the turnaround renders (`work/model-turnaround/`, studio lights, skin B7744A, VISUAL_CHARACTER_SKIN_POLISH / VISUAL_BLINK_NORMAL_FIELD off) and on bind- and posed-mesh dumps.
"Before" = `work/model-turnaround/before-2026-10-12/` (the 00:00 capture) and the same dumps taken before any change.

| # | Gate | Result | Evidence |
|---|---|---|---|
| G1 | BALD: no hair or headwear added | PASS | no hair renderer in any capture (report.txt parts lists unchanged) |
| G2 | NO_ANIMATION_CHANGE: no clip / FBX / prefab / rig file touched | PASS | `git status`: no change under `Characters/MatchHeroes`, `Resources/Hero`, `Resources/Tennis/Customization`, `Resources/Tennis/OriginalSeam*` |
| G3 | SHAPE_KEPT: body proportions, height, skeleton unchanged | PASS | rest bounds height 1.735 / 1.730 / 1.738 / 1.733 m, the same as before (widths: golf female 0.619 → 0.521 m from the closer skirt; tennis male 1.327 → 1.318 m from the right-hand fingers closing on the hidden racket in the bind pose); only the head-only ear warp and garment-only skirt warp move vertices (`HeroFaceReferenceFit` invariants: belowHeadChanges=0, topology / weights / binds identical) |
| G4 | MUDDY_SHADING_GONE: no Contact AO blotches on hero skin | PASS | heroes are redrawn after the AO multiply; side-by-side identical to an AO-off reference render; male face dark-blotch share 7.7% → 2.4% (what remains is lit form and brows) |
| G5 | LESS_ORANGE, TONE_KEPT: forehead saturation down ≥10%, brightness within ±5% | PASS | male sat .457 → .392 (−14%), value .849 → .819 (−3.5%); female .428 → .372 (−13%), .878 → .852 (−3.0%); palette colour unchanged |
| G6 | BLUSH: cheek apples redder than before, relative to the forehead | PASS (light) | male apple-minus-forehead redness +.034 → +.040, female −.020 → −.007; visible in `proof/compare_*` three-quarter views |
| G7 | EARS_SMALLER | PASS | male height 74 → 65 mm, width 47 → 39–41 mm, off-skull 26 → 23 mm; female 92 → 85 mm, 61 → 57 mm, 31 → 25 mm |
| G8 | GRIP_NO_POKE: no finger skin deeper than 1.5 mm inside the handle (ready + Forehand / Backhand / Serve samples) | PASS | deepest: male 1.1 mm, female 0.4 mm (before: male 4.5 mm) |
| G9 | GRIP_FIST: four fingers curled round the handle, thumb over | **FAIL** | blocked: the racket sits under the finger pads; more curl drives them 20–45 mm into the handle. Needs the racket re-seated in the hand, which would change every clip, outside "don't touch the animations" |
| G10 | SOLE_LINE (tennis shoes) | PASS | crisp grey seam where the upper meets the sole, plus a rubber band at the base (`MatchHero_*/ready_shoes_*`) |
| G11 | SOCK_EDGE (tennis socks) | PASS | ribbed cuff with a crisp lower edge and a soft rolled top (`ready_shoes_*`, full-body shots) |
| G12 | SKIRT_CLOSE (golf female): hem radius down, monotonic flare, hands outside | PASS | hem 31 → 23 cm; radius by height 16.4 / 19.4 / 20.9 / 21.9 / 22.3 / 22.6 / 22.7 cm (waist → hem); idle wrists at 26.5 cm vs 22 cm skirt |
| G13 | SKIRT_NO_NEW_CLIPPING: legs outside the skirt in 8 IronSwing samples ≤ before | PASS | e.g. t=0: 280 → 214 vertices, t=.35: 284 → 199, t=.8: 306 → 305 (the original already shows legs past its hem mid-swing) |
| G14 | CAPTURE_CLEAN: no placeholder-shader frames | PASS | 204 / 204 stills checked against their before counterparts |
| G15 | NO_NEW_EDITMODE_FAILURES | PASS | 475 / 483 pass. The 6 `MultiplayerRulesTests` golf failures also fail with this pass removed (baseline run); 2 more fail only because the clone has no repo folders beside it (`HeroAssetRegistryTests`, `NetworkViewTests`) |
| G16 | PROOF exists | PASS | `proof/compare_*.png`, `proof/sheets_*.png`, `work/model-turnaround/*_contact_sheet.png` + spin mp4s |
| G17 | ADNAN_EYE | PENDING | your call on the compare sheets |

## Pass 2 (2026-10-12, afternoon): faces, ears, shoes, grip, lobby = game

Measured on the final turnaround (`work/model-turnaround/`), the export reports (`work/hero-sync/out/logs/`), the lobby-vs-game sheets (`work/hero-sync/sheets/PARITY.md`) and the test runs. G8, G10–G14 above still hold; G9 is updated by P9.

| # | Gate | Result | Evidence |
|---|---|---|---|
| P1 | LOBBY_REEXPORTED: every MatchHero_* / GolfKitHero_* export carries this pass | PASS | 66 files in `GolfArcade/Unity/CharacterAssets/` (6 manifests, base poses, rigs, swing frames). Export self-checks: CPU skin vs Unity BakeMesh ≤ 0.001 mm; exported tracks vs live skinning ≤ 0.001 mm on every clip. |
| P2 | LOBBY_EQUALS_GAME: same hero, pose, cameras and lights; app SceneKit code vs Unity | PASS | 56 shots (4 heroes × body / torso / head / feet, tennis × 6 looks): lobby / game luminance 0.91–1.03, mean absolute difference 5.3–12.6 / 255 (was 25–48 before the shader port). Key light only: face skin within 1–2 % in every brightness quartile. `proof/lobby_*.png` = lobby before / lobby now / game. |
| P3 | ONE_SYNC_STEP + DRIFT_GUARD | PASS | `work/hero-sync/sync_heroes.sh` ran end to end three times. The last re-export was byte-identical (0 files changed). Provenance hashes 39 look sources. The native guard `testNativeHeroesMatchTheCurrentUnityHeroLook` passes. |
| P4 | NOSE_SMALLER_NARROWER | PASS (applied; eye check pending) | Nostril width −24 % (male) / −22 % (female), head-only warp (`belowHeadChanges=0`, topology / weights / binds identical, minimum Jacobian 0.21 > 0). See `proof/target_*`. |
| P5 | EYES_BIGGER_ROUNDER, FEMALE_CLOSER | PASS (applied; eye check pending) | Male openings 8 % wider and 55 % taller, irises +16 %. Female openings 20 % taller, irises +6 %, eyes 3 mm closer together. Brows unchanged. |
| P6 | SMILE_SOFTER_SMALLER | PASS | Painted mouth width 41 → 35 mm (male), 33 → 28 mm (female), gentler corner lift. Male mouth-area stretch 16 % → 6 %. |
| P7 | BLUSH_VISIBLE | PASS (eye check pending) | Visible on both heroes in the turnaround (`proof/target_*`), the in-app locker (`proof/app/*_face.png`) and the lobby sheets. |
| P8 | EARS_SMALLER_AGAIN | PASS | A second shrink in the same warp (male 14 %, female 16 %) on top of pass 1. |
| P9 | GRIP: fingers curl round the handle, thumb over; no motion change | **PARTIAL** | Racket re-seated in the hand by moving its meshes (2.1 cm male / 0.7 cm female, racket-local). The gameplay string centre moves with it. No clip, bone, swing or timing changed. Fingers now grasp, and the thumb closes. A full fist would need the racket turned 25–40° in the hand (about 20 cm of racket-head travel on every swing), so it was not done. |
| P10 | GRIP_DETERMINISTIC | PASS | Grip difference with a different frame solved first: 0.0000° on Ready, Forehand, Backhand and Serve, both heroes. Cost 1.25 ms (male) / 0.54 ms (female) per solve in editor Mono, every other frame in play. |
| P11 | CLEAN_WHITE_SNEAKER | PASS (eye check pending) | Trim 93 % of the pick (was 65 %), near-white starter trim. Toe taper and rounded corners: minimum Jacobian ≥ 0.68, never closer than 4 mm to the foot / sock. The same shoe is applied to the Distance kit (far players + lobby). |
| P12 | NO_BODY / SKELETON / ANIMATION CHANGE, STILL BALD | PASS | Body 44,168 / 34,443 vertices as before. Bone counts 144 / 158 / 54 as before. No clip, FBX, prefab or rig file staged. No hair renderer. |
| P13 | UNITY_EDITMODE | PASS | 475 / 483, the same 8 failures as at the start of the day: 6 `MultiplayerRulesTests` golf baseline failures, and 2 that fail only because the clone has no repo folders beside it. |
| P14 | NATIVE_TESTS | PASS (no new failures from this pass) | 494 run, 28 failing. 26 fail identically at HEAD (same number of failed checks each; stale locker / cloth / clip-name expectations). The other 2 (`LockerTests…OpensOnTheShelf`, `MenuBetaTests…BundledGameplay`) come from another session's unstaged edits to `LockerCatalog.swift` and `SportsInfo.plist`, which this pass did not touch. All `VisualOverhaulNativeTests` pass, including the Unity golden vertex parity of all six exports. |
| P15 | ADNAN_EYE | PENDING | `proof/target_all.png`, `proof/lobby_all.png`, `work/hero-sync/sheets/` |

## Skin pass (2026-10-12, after pass 2): matte, warm form, golden tan

Measured on the turnaround three-quarter faces (studio rig, skin B7744A): BEFORE = `skin_before/`, NOW = `work/model-turnaround/`. Target values come from the target heads' "after" tiles.

| # | Gate | Result | Evidence |
|---|---|---|---|
| S1 | MATTE: no broad pale sheen on the forehead, nose or lips | PASS | Brightest 500 forehead / scalp pixels: saturation .34 → .48 (target forehead .41–.49), value .87 / .90 → .81 / .84. Sheen peak .24 → .018. |
| S2 | WARMER / GOLDEN, LESS PINK | PASS | Hue 19.9–20.9° → 21.8–23.0° on forehead, scalp, cheek, jaw and neck (target 21–25°). Saturation .35–.48 → .47–.52 (target .41–.63). |
| S3 | WARM SHADOWS, NOT GREY | PASS | Shadowed jaw saturation .43 → .48. The ambient fill is warm ×(1, .86, .72) and the terminator bleed reddish-brown. No grey AO patches (the Contact AO exemption is unchanged). |
| S4 | SOFT FORM SHADING (brows, nose sides, cheekbones, jaw, neck) | PASS (eye check pending) | Zones placed on the measured bind-pose surface of each head (`proof/skin_*`). The neck is now the darkest skin (male neck .66 vs forehead .79; female .70 vs .82). The target's lighting is more dramatic (neck ≈ .56 of the forehead); kept "slightly darker" as asked. |
| S5 | NATURAL VARIATION | PASS | Ears and nose tip warmer. Faint 3 mm / 18 mm grain in the bind pose, faded before it can alias. |
| S6 | SHADING ONLY | PASS | Changes are limited to `HeroFaceSkin.shader`, `MatchHeroLook.SkinSubsurface` and `MatchHeroSurfaces.swift`. Re-export: only the 6 manifests changed, meshes and rigs byte-identical. Blush unchanged. |
| S7 | TONE PICKS KEEP WORKING | PASS | The grade is relative (multiplies the picked tone). Lobby-vs-game sheets cover 6 tone / kit cases (`work/hero-sync/sheets/tennis_colours.png`). |
| S8 | LOBBY = GAME | PASS | Re-synced. 56 shots: lobby / game luminance .91–1.03, mean absolute difference 5.3–12.3 / 255 (heads .97–1.03, 5.3–6.5). |
| S9 | ADNAN_EYE | PENDING | `proof/skin_all.png`, `proof/app/lobby-skin-tv.png` |

## Jawline pass (2026-10-12): defined, sharp, obvious

| # | Gate | Result | Evidence |
|---|---|---|---|
| J1 | JAW_DEFINED: a visible jaw edge chin → angle with a shadow under it, front / three-quarter / side | PASS (eye check pending) | `proof/jaw_all.png` (TARGET / BEFORE / NOW, head-and-neck framings) |
| J2 | NO_FOLDS | PASS | `HeroFaceReferenceFit` minimum Jacobian: female .169, male .220 (a rising border first gave −.12 on the female; fixed by flattening the border past the angle) |
| J3 | HEAD_ONLY / RIG / ANIMATION UNCHANGED | PASS | `belowHeadChanges=0`; topology, weights and bind poses identical; export rig self-checks ≤ .001 mm; no clip, prefab or FBX touched |
| J4 | LOBBY = GAME | PASS | Re-synced (63 files). Heads: lobby / game luminance .968–1.029, mean absolute difference 5.3–6.6 / 255. `proof/app/lobby-jaw-tv.png` is the real app lobby. |
| J5 | NATIVE TESTS | PASS for this pass | The lobby suites fail exactly as at HEAD. `testNativeHeroesMatchTheCurrentUnityHeroLook` fails only because another session edited 4 guarded files (`MatchHeroLook`, `HeroGarmentLOD`, `MatchHeroLockerExport`, `HeroGolfer`; CLOTHING V2, unstaged) after this export. The staged versions match the export record, so that session must re-run `work/hero-sync/sync_heroes.sh` when it is done. |
