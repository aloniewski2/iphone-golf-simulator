# LOBBY_PARITY — lobby / menu heroes re-exported from the current Unity heroes (2026-10-10)

## Problem
The online lobby (and locker, menus, loading screen) draw the **exported** SceneKit hero (`GolfArcade/Unity/CharacterAssets/MatchHero_*`, `GolfKitHero_*`), not the Unity prefab.
Those files were exported from the "face29" head (`OriginalSeamBlinkFinish/{Male,Female}.fbx` sha 106171a7… / 02ac48c1…). Unity's head moved on (composed-ears head, sha b6e2342a… / 2f8cedf0…,
`HeroFaceSkin` head surface, 8 face morphs, authored finger/face bones, ArmCorrected Forehand). Nobody re-exported because `MatchHeroLockerExport` rejected the new `HeroFaceSkin` shader name,
so the lobby showed a visibly different face (leaner jaw, different cheeks/ear/eyes) and an older rig.

## What changed
* `Unity/Assets/Editor/MatchHeroLockerExport.cs`: one line, `HeroFaceSkin` is recorded as `TennisCharacter` (same properties) so the export no longer throws.
* `GolfArcade/Unity/CharacterAssets/`: 67 files replaced (tennis full, tennis Distance, golf; manifests, base poses, rigs, swing frames, `MatchHero_OriginalEye_RelativeSclera.png`). Body is the only mesh that
  changed (male 33,694 -> 44,168 verts, female 23,230 -> 34,443); kit, racket and every other map are byte-identical. Tennis rigs now carry 144 (male) / 158 (female) bones (were 53) and the Face part
  carries 8 morphs (were 2). Golf rigs: 54 bones as before.
* `GolfArcadeTests/VisualGolden/*_golden.lzfse` + `provenance.json`: six goldens re-sampled from Unity's CPU skinning of the new export.
* Tests: body vertex counts, bone/morph contract, emote-fixture size updated to the new export; new `testNativeHeroesAreExportedFromTheCurrentUnityHeads` fails whenever the Unity head FBX hashes differ from the ones in the provenance.

## Re-export (when the Unity heroes change again)
1. `cp -c -R Unity <scratch>/UnityClone` (APFS clone, never the shared project).
2. `work/lobby-parity/run_export.sh` (needs `S`/`R` paths edited to the clone; runs `Run`, `RunDistance`, `RunGolf`, default look flags, -nographics).
3. Copy changed files from `work/lobby-parity/export/` into `GolfArcade/Unity/CharacterAssets/`; pack `work/lobby-parity/golden/*_golden.json` with `work/reference-rebuild/original-native-adoption29/PackGolden.swift` into `GolfArcadeTests/VisualGolden/` and rewrite `provenance.json`.
4. Compare: Unity stills with `capture_method.cs` (spliced into the clone's exporter, `LP_OUT=<dir> ... CaptureLobbyParity`) against `harness/` (macOS SceneKit render of the same files, `LP_ASSETS=<dir>`).

## Evidence
* Unity in-game resort captures (`TennisVenueCapture`, current HEAD) vs Unity studio capture vs lobby before/after: heads/jaw/ears/eyes now match; whole-body renders match (grip, fingers, clips).
* Native tests: `VisualOverhaulNativeTests` (contract + Unity golden vertex parity for all 6 variants), lobby 4-hero render 60 fps (tennis 303 MB, golf 377 MB footprint in the simulator; was 242 / 320 MB).
* Baseline: 16 tests in `MatchHeroLockerTests` / `LockerMirrorTests` already fail on the committed state (stale cloth-shader / clip-name expectations); none was fixed or newly broken by this change.
* Pre-existing, untouched: `GolfArcadeTests/MotionClubReviewTests.swift` does not compile at HEAD (`SportsSession.golfCalibration*` was removed), so the whole test target fails to build; tests were run from a scratch xcodegen project that excludes that file.

## Rollback
All replaced files are tracked in git at HEAD: `git checkout -- GolfArcade/Unity/CharacterAssets GolfArcadeTests/VisualGolden GolfArcadeTests/*.swift Unity/Assets/Editor/MatchHeroLockerExport.cs`.
`baseline/install-record.json` lists every replaced file with before/after SHA-256.

## Not done / disclosed
* Remaining Unity-vs-SceneKit render differences are shading only (fine cloth weave/seams, under-eye shading, male forehead ~10 % brighter than Unity); the lobby is still SceneKit, Unity renders only on the TV / device build.
* No physical iPhone / TV measured; bone count 144-158 on device GPU skinning is unmeasured.
* Not committed.
