# GATE LOCKER_MIRROR — definitions (written with the tests, before the final results; 3 checks were corrected after their first run — listed in GATE_RESULTS.md under "Adjustments")

Job: the Swift locker mirror (`CharacterModelPreview.swift` + `MatchHeroData/Surfaces/Motion.swift`) draws the match heroes the way Unity's cloth/skin pass does and moves them by playing
Unity's own clips on their skeleton. Scope: the Swift mirror and `Unity/Assets/Editor/MatchHeroLockerExport.cs`. Every line below is PASS or FAIL; the checks are XCTest cases in
`GolfArcadeTests/LockerMirrorTests.swift` (gate) and `LockerMirrorProofTests.swift` (recorders), run on the iPhone 17 Pro simulator. `GATE_RESULTS.md` quotes their output.

Method note: the clips are exported as **bone tracks** (ReadyIdle and Serve, 60 Hz, hero space) with the bind-pose meshes and bone weights and played on SceneKit skinners, instead of as morph
targets "like the Forehand". One morph frame is 2.4 MB (106 k vertices); a Serve needs 40+ frames (≥100 MB bundle, ≈200 MB resident) and linear vertex blends flatten rotating limbs. Bone tracks
cost 3 MB per sex, play at any frame rate, and are checked against Unity's own CPU skinning. The Forehand morph export is unchanged and still drives the loading screen's practice swing.

## Line 1 — Kit samples the weave and the thread map. Default trim is dark. Sole is darker and rougher than the upper. A picked shirt still uses kitDerive.

| # | check (test) | pass condition |
|---|---|---|
| 1a | `testKitMaterialsSampleTheWeaveTheThreadMapAndTheSeamMap` | for Kit_Shirt, Kit_Shorts, Kit_Sock, Kit_Shoe (both sexes, every piece): the material is TennisCloth, runs the cloth surface shader, binds the weave map as a LINEAR image, has weave-normal strength > 0 **and** thread-break strength > 0, the exported tile on the material, and the kit's seam map bound as its diffuse |
| 1b | `testWeaveTileIsMetresPerUvOverTheRolesTileSize` | "tiled the same way TennisCloth does": for every kit submesh `weaveTile` = sqrt(3D area / UV area) of the submesh in the bind pose ÷ the role's tile size in metres (`MatchHeroLook.ClothFor`: shirt .026, trim .022, shorts .029, band .022, shoe .024, sole .020, sock .020), recomputed from the shipped rig + UVs within 1 %; the exported `tileMetres` equals that table; male shirt `weaveAngle` 41°, female 0° |
| 1c | `testSeamMapOrientationPutsTheDarkeningOnTheHemsAndCollar` | "seams stay darker": sampled at the kit's UVs the way Unity samples them, hem and collar vertices are darker than the rest by > 0.004 and by > 0.004 more than with the rows flipped (shirts, both sexes; male shorts) |
| 1d | `testShirtIsNotOneFlatWhite` | FAIL if the shirt is still flat: at the locker's shirt close-up, fine detail (mean \|luma − 7×7 blur\|) ≥ 2.0 × the flat Blinn look of before this job; mean luma > 0.6 (still a white shirt); switching the weave normal off, the thread break off, or the seam map off each changes the shirt (mean luma change > 0.01 / 0.01 / 0.001, seam map 99th pct > 0.02) |
| 1e | `testClothHasASheenAndNoHighlightDot` | every cloth material: gloss exponent 28·smoothness + 2 ≤ 9 (a broad lobe, never a dot), smoothness ≤ 0.25, sheen power exported; the shirt's sheen strength > 0.3; the lighting source grows the sheen with `pow(1 − n·v, power)` |
| 1f | `testDefaultTrimIsTheAuthoredDarkAndAPickedShirtStillDerivesItsTrim` | default Kit_ShirtTrim and Kit_ShortsBand = #09090A (±1/255) and not the garment's white; with a shirt pick, trim = `kitTint(kitDerive(pick))` (within 0.005) — `kitDerive`/`kitTint` unchanged |
| 1g | `testSoleIsDarkerAndRougherThanTheUpperAndIsNotRecoloured` | sole luminance < upper − 0.15, sole smoothness < upper's, no sheen, weave normal < upper's; a shoe pick recolours the upper and leaves the sole's colour bit-identical |

## Line 2 — Skin smoothness is about 0.2 with a weak normal. No weave on skin or eyes.

| # | check (test) | pass condition |
|---|---|---|
| 2a | `testSkinIsAboutSmoothness02WithAWeakNormalAndNoWeaveOnSkinOrEyes` | skin smoothness 0.2 ± 0.03; bump strength in (0.05, 0.25]; triplanar from the bind-pose position (the body's two texcoord channels carry the bind pose; shader projects along x/y/z); soft normal map bound; skin, eyes (Sclera/Iris/Pupil/Catch, each ≥ skin + 0.4 smoother), face decals, racket frame and strings have no weave shader and no weave sampling; grip dark (luma < 0.2), rougher than the shirt, strings smoother than the grip by < 0.15, grip is the UV-less cloth variant |
| 2b | same test, pixels | on the skin pixels, switching the soft normal off changes the picture by > 0.0003 and < 0.02 of the range (wired, and weak) |

## Line 3 — Locker tile and tennis idle play ReadyIdle. Play tile plays one Serve, then Ready. No root spin.

| # | check (test) | pass condition |
|---|---|---|
| 3a | `testShippedRigReproducesUnitysPoses` | the shipped rig (bind meshes, weights, inverse binds, bone tracks) skinned in Swift matches Unity's own CPU skinning of the live rig at golden clip times (≈25 000 vertices per sex, both clips, every part incl. face and racket) within 0.5 mm |
| 3b | `testSkinnedHeroAtReadyFrameZeroIsTheStaticHero` | the SceneKit-skinned hero at ReadyIdle[0] renders as the static Ready hero (mean abs diff < 0.5/255) |
| 3c | `testLockerTileAndTennisIdlePlayReadyIdleInPlace` | locker, settings, store tiles and `configureIdle(.tennis, animate: true)` (both sexes): motion = ReadyIdle on the skeleton; over 2.2 s the racket hand travels > 20 mm; at 2.2 s the pose is the t = 0 pose again (< 0.1 mm); the hero root's yaw stays π + 0.35 and the character node's rotation stays 0 at every sampled time; no actions on the character, no morph on the body, no practice timer |
| 3d | `testPlayTilePlaysOneServeThenReady` | play tile: Ready until the Serve starts, the Serve (clip `<Sex>_Serve`) lifts the hips > 150 mm, every hop lies inside the Serve, none after it; after the Serve the pose equals the Ready loop's exactly, for 12 s; no pop at either seam; root never yaws; no Forehand morph/timer; racket held |
| 3e | `testReducedMotionStaysAStillReadyPose` | animate = false: no motion, no skeleton, body has no skinner, no actions, root yaw fixed |
| 3f | `testBothMenuViewsRunAt60fpsAndMoveTheHeroForReal` | the real SwiftUI views (club locker tile, club play tile, tennis idle view): `preferredFramesPerSecond` = 60; the skeleton moves; > 150 pixels of the real, multisampled view change in 0.7 s; root yaw unchanged; with reduced motion on: render loop off, no skeleton |
| 3g | proof | `proof/locker_tile_*.mp4`, `proof/play_tile_*.mp4` (clip-time frames with the root yaw and the body travel written on them), `proof/real_club_home_tiles_tv.mp4`, `proof/real_club_home_phone.mp4`, `proof/real_app_simulator_phone.mp4` (the real app, screen recording), `proof/fps_and_pose_cost.txt` |

## Line 4 — Serve FBX, walk, runs, emotes, shaders, and the course files are untouched.

`scripts/protected_files.sh` hashes (SHA-256) every clip FBX + .meta under `Unity/Assets/Characters/MatchHeroes`, every shader under `Resources/Tennis/Shaders`, `MatchHeroLook.cs`, `MatchHeroKit.cs`,
every `Unity/Assets/Scripts/Course/*.cs` and the HeroDetail source maps. `sha_before.txt` was taken before any edit; `sha_after.txt` at the end. PASS if no file this job wrote differs; a
protected file that differs and is not in this job's write list is reported with its mtime as a foreign edit (another chat working in the same tree).
This job's write list: `Unity/Assets/Editor/MatchHeroLockerExport.cs`; `GolfArcade/Unity/{CharacterModelPreview,MatchHeroData,MatchHeroSurfaces,MatchHeroMotion}.swift`;
`GolfArcade/Unity/CharacterAssets/MatchHero_*` (regenerated + new maps/rig); `GolfArcadeTests/{LockerMirrorTests,LockerMirrorProofTests,DressedHeroSnapshotTests}.swift`; the two generated `.xcodeproj`s; `work/locker-mirror/**`.

## Out of scope / not touched by design
`TennisCharacter.shader`, `TennisCloth.shader`, `MatchHeroLook.cs`, every clip FBX, the golf course, emotes, the golf idle's prop animation (`configureIdle(.golf)` keeps its own root sway: it is not a tennis idle),
the loading screen's practice swing (still the Forehand morph, still frame between swings), `HeroTennisDriver`.
