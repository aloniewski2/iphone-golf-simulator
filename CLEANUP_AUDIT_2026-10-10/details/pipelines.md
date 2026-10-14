# Pipelines audit — SportsLibrary/, blender/, Tools/HeroBase/

Repo: `iphone-golf-simulator`, branch `final-build`, HEAD `14a43331`, audited 2026-10-10. Read-only: nothing in the repo was changed; no Blender or `git lfs` commands were run. The `.blend` files were inspected by decompressing them in memory and listing the file paths stored inside.

## How to read this

- **KEEP**: the item makes, or is the only source of, something the game uses today.
- **DECIDE**: the game does not load it, but it is the source (or the only rebuild path) of something live, or it is a deliberately parked option. This is your call.
- **SAFE**: nothing in the game, the build, CI, tests or a current pipeline reads it. Removing it changes nothing. Some code comments still mention it, as noted.
- **SAFE+FIX**: removable once the named follow-up edit is made, because a script or Unity importer reads it by path.
- "Live" means loaded by runtime C#/Swift in the shipped build scenes (Golf, Tennis, Hub) or the Swift app.
- Sizes are bytes on disk in decimal MB. The SportsLibrary figure of "≈3.2 GB" (from `du`) includes 0.92 GB of gitignored `.blend1` backups; the tracked part is 2.52 GB.
- **LFS**: 617 SportsLibrary files and 36 blender files are Git LFS objects, and all of them are present locally. Deleting them in a new commit does not free LFS storage or shrink history; that takes a history rewrite or LFS prune (owner action).
- **Tracked state**: none of these three trees has modified or untracked files in the working tree. Every item is either tracked and clean, or gitignored. Only gitignored files (`.blend1`, `__pycache__`, 3 previews) are untracked.
- `blender/` LFS files show 2026-10-06 (`7ecf8100`) as their last commit. That commit only converted them to LFS pointers, so the "content" dates below skip it.

## Headline results

1. **Golf content is fully live and comes from `blender/`.** All 16 `hole_NN.blend` files belong to the 3 live courses. Unity never reads a `.blend`; it loads `Resources/Course/hole_NN.fbx`. The course builders (`course_builder`, the postcard libs, the Hole 7 phase scripts) are needed to rebuild any hole.
2. **The live golfer is `matchhero_golf.py` output** (`Resources/Hero/golfer_m|f.fbx`). Its rebuild depends on gitignored sources that are **not present** on this machine (`blender/matchhero`, `blender/mhhair`) and on `Resources/Golfer/golfer_m.fbx`, the old V4 golfer, which it reads as its `--v4` input.
3. **SportsLibrary is mostly dead weight, with three live exceptions:**
   - `Blender/sports-animation-studio-v4.blend` is the source of the hidden tennis gameplay rig (`StandardCharacters/standard_*_tennis.fbx`), which `TennisActor.Build` loads every match.
   - `Arenas/Tennis/tropical-tennis-resort-v3.blend` is the source of the live resort arena. Its exporter lives outside the repo.
   - `ArtDirection/Tennis/Generated/models/{umpire,island}.glb` are the sources of the live umpire and island.

   About 1.7 GB of tracked SportsLibrary content is SAFE: bowling/boxing, crowds, old characters, clothing, old player bases, generated GLBs, video frames, concept art, previews.
4. **`blender/tennis-clips.json` is load-bearing.** `Unity/Assets/Editor/GolferModelImporter.cs` reads it on every import of the standard tennis FBX. Without it the hidden gameplay rig would import without clip splits.
5. **Superseded character pipelines in `blender/scripts`:**
   - Sept 22–24 V4/V5/Higgsfield/video tennis chain: 35 scripts.
   - Old player bases: 6 scripts, plus 309 MB in `SportsLibrary/Customization`.
   - Scanned Hero01 repair: 8 scripts.
   - Andrew's original golfer: 3 scripts + 2 blends.
   - `turf_textures.py`: its output was replaced on 2026-09-23.
   - The icon avatar kit (9 scripts) was retired on 2026-10-04, but the docs say it was kept on purpose, so it is DECIDE. `avatar_kit.py` is still imported by the live golfer build.
6. **Tools/HeroBase** (2026-09-30..10-02) has no consumer in the game or in the live MatchHero build (`work/match-anim-set`). Only untracked work/ jobs import it, and 3 of its verify scripts fail by design.

## Findings — blender/ (golf courses)

### PIPE-01 — Hole source scenes `blender/hole_07.blend … hole_23.blend` (16 files)
- **What:** One Blender scene per golf hole, saved by the build scripts alongside the FBX that Unity loads.
- **Size / git:** 19.0 MB. All tracked. 15 are LFS (converted in `7ecf8100`); `hole_07.blend` is a plain git blob. Content dates run from 2026-09-20 to 2026-10-05; each `.blend` was committed together with its `Resources/Course/hole_NN.fbx` (table below).
- **Evidence:**
  - `Course.All()` (Hole.cs:289) lists 3 live courses that use all 16 holes: Cliffside 7, 8, 9, 12, 15; Wild Isles 13, 14, 17, 18, 19, 20; Magma Open 10, 16, 21, 22, 23.
  - `HoleView.cs:58` loads `Course/hole_NN` (the FBX). No C# reads a `.blend`, and they sit outside `Unity/Assets`, so Unity never imports them. They are **source only**.
  - `postcard_lib.import_rocks` copies the rock/tree library out of `hole_07.blend` (sha-checked). `course_builder.py` re-runs Hole 7's tree and rock parts.
  - `hole_08/09/10.blend` store absolute paths to 30 textures in `Unity/Assets/Resources/Course/Look/`.
- **If removed:** The game is unaffected. You lose the editable source of every hole: a fix would mean rebuilding from scripts (holes 7, 8–10, 13–23 are scripted; hole 12 cannot be rebuilt, see PIPE-05). `postcard_lib` would also lose its rock library.
- **Verdict: KEEP.** Confidence: high.

### PIPE-02 — Hole 7 phase scripts + `course_builder.py` family (24 scripts)
- **What:** The scripts that build Hole 7 (`build_all.py`, `phase1…phase11_13_*.py`, `hole07_design.py`, `hole07_lib.py`) and the generic builder for holes 13–23 (`course_builder.py`, `course_extras.py`, `magma_shapes.py`, `hole13…hole23_*_design.py`).
- **Size / git:** 0.2 MB, tracked, not LFS, last commits 2026-09-20 to 2026-10-04.
- **Evidence:**
  - `course_builder.py` imports `hole07_lib`, `hole07_design` and `course_extras`, and runs parts of `phase6_trees.py` and `phase7_rocks.py`.
  - Each run writes `blender/hole_NN.blend`, `Resources/Course/hole_NN.fbx`, `hole_NN_card.jpg` and `blender/holes_mock/hole_NN_numbers.txt`.
  - EditMode tests (`NewHolesTests`, `WildIslesTests`, `MagmaOpenTests`) assert the numbers these design modules produced.
  - Recent use: work/golf-course-standard (10-06) and work/full-visual-overhaul (10-07).
  - Caveat: `hole07_lib.py` and `build_all.py` still carry Andrew's absolute paths (`/Users/andrewloniewski/...`). Rebuilding Hole 7 itself needs a path edit; `course_builder` overrides the path.
- **If removed:** None of the 16 live holes could be rebuilt or changed.
- **Verdict: KEEP.** Confidence: high.

### PIPE-03 — Postcard pipeline for holes 8 Needle, 9 Split, 10 Crater (26 files)
- **What:** The design → build → look → verify → emit-C# chain:
  - `hole08/09/10_{design,build,look}.py`, `hole09_{masonry,stone,qa}.py`
  - `postcard_{lib,look_lib,props_lib,check,emit_cs,verify,look_verify,plan_png}.py`
  - smoke tests `postcard_*_smoke.py` and `postcard_smoke_design.py`
  - `golf_look_textures.py`, `POSTCARDS_README.md`
- **Size / git:** 2.2 MB, tracked, last commits 2026-10-05/06.
- **Evidence:**
  - Produces `Resources/Course/hole_08/09/10.fbx` and the `Course/Look/*.png` textures; both are live.
  - `postcard_emit_cs.py --check` keeps `Hole.cs` `Course.Postcards()` in sync with the design modules.
  - `Tools/PostcardCheck` (gates_diff.py, equiv.py) imports `postcard_check.py` and `hole0x_design.py`.
  - `postcard_props_lib` is imported by `golf_course_standard_props.py` (PIPE-04).
- **If removed:** Holes 8–10 could not be rebuilt, and `Tools/PostcardCheck` would break.
- **Verdict: KEEP.** The smoke tests are test-only but belong to the same live pipeline. Confidence: high.

### PIPE-04 — Other live course-asset builders (6 files)
- **What and output:**
  - `pin_build.py` + `blender/pin.blend` → `Course/pin.fbx` (HoleView.cs:469).
  - `backdrop_import.py` → `Course/Backdrop/island|sailboat` (`Backdrop.Place`, HoleView.cs:341).
  - `golf_course_standard_props.py` → `Course/Standard/Fringe.fbx` (`GolfCourseFringe`).
  - `golf_course_standard_textures.py` → `Course/Standard/*_C|N.png` (`GolfCourseLook`).
  - `build_reference_hole09.py` (2026-10-08) → `Course/Resort/ReferenceHole09.fbx`. `GolfCoastalComposition.Enabled` defaults on, so hole 9's model is swapped for this file at runtime.
- **Size / git:** 0.1 MB, tracked (`pin.blend` is LFS), last commits 2026-09-22 to 2026-10-08.
- **Caveat:** `backdrop_import.py`'s input GLBs are not in the repo, so the existing backdrop cannot be reproduced exactly; the script still works as a tool for new backdrops.
- **If removed:** These live assets could not be regenerated.
- **Verdict: KEEP.** Confidence: high.

### PIPE-05 — Hole 12 import/cinematic scripts (3 scripts)
- **What:** `hole12_prepare.py`, `hole12_animate_finish.py` and `hole12_cinematic_export.py` turned Codex's externally modelled Hole 12 into `blender/hole_12.blend`, `hole_12.fbx`, `hole_12_cinematic.fbx`, `hole_12_shot.json` and `Course/swing_trail.json`.
- **Size / git:** 0.1 MB, tracked, last commits 2026-09-21.
- **Evidence:**
  - All of those outputs are live: `HoleView`, `CinematicRig.Load`, `SignatureShot.Load`, `GolfGame.DressBall` and `ShotEffects` load them.
  - The inputs (`Hole_12_Cinematic.blend` / `Hole_12_Animated.blend`, from `…/Codex/2026-09-20/i-j/outputs` on Andrew's machine) are **not on this disk**, so the scripts cannot run.
- **If removed:** No effect on the game. Only the record of how those files were made is lost.
- **Verdict: DECIDE** (provenance only). Confidence: high.

### PIPE-06 — `turf_textures.py` (1 script)
- **What:** Baked grey turf tiles into `Resources/Course/Turf/{fairway,green,rough,sand}.png`.
- **Size / git:** tracked, last commit 2026-09-22.
- **Evidence:** All four PNGs were replaced on 2026-09-23 by Higgsfield art (`e9544183`). The script's output no longer exists. Only comments mention it (HoleView.cs:685, Turf.shader).
- **If removed:** Nothing changes.
- **Verdict: SAFE** (optionally update those 2 comments). Confidence: high.

### PIPE-07 — Andrew's original procedural golfer (5 files)
- **What:** `golfer_build.py`, `golfer_export.py`, `golfer_preview.py`, `blender/golfer_m.blend` and `golfer_f.blend` (2026-09-20). These made the first low-poly golfer.
- **Size / git:** 0.3 MB, tracked; the blends are plain git blobs.
- **Evidence:**
  - Its output `Resources/Golfer/golfer_m|f.fbx` was overwritten on 2026-09-21/23 by `adnan_golfer_export.py`; the golfer has since moved to `Resources/Hero/` (PIPE-08).
  - Only referenced by commented lines in `build_all.py` (lines 11–14) and a comment in `GolferModelImporter.cs`.
  - The paths are hard-coded to Andrew's machine. If those were fixed, re-running would **overwrite** today's V4 golfer, which `matchhero_golf.py` reads as an input.
- **If removed:** Nothing changes, and that overwrite hazard goes away.
- **Verdict: SAFE.** Confidence: high.

## Findings — blender/ (golf characters)

### PIPE-08 — Live golf-hero pipeline (9 scripts)
- **What:** The scripts that build the game's golfer:
  - `matchhero_golf.py`, with `matchhero_hair.py`, `matchhero_mhhair.py`, `mhhair_sources.py`, `golf_swing.py`
  - `matchhero_icons.py`, `fetch_mhhair.py`
  - `avatar_kit.py` (imported by `matchhero_hair` and `matchhero_golf`)
  - `golf_clubs.py`
- **Size / git:** 0.2 MB, tracked, last commits 2026-10-04 to 2026-10-08.
- **Output:**
  - `Resources/Hero/golfer_m|f.fbx` + `_clips.json`: `HeroGolfer.PathFor`, and `GolferView.BuildHero` every round. Rig, clips, clubs and hair come from it; the body and kit are swapped for the `PlayerMaleGolfKit` prefab at runtime.
  - `Resources/Hero/Hair/*.png`.
  - `Resources/UI/Look/hair_*_m|f.png` and `tab_outfit.png` (locker tiles).
  - `golf_clubs.py` also feeds `work/full-visual-overhaul/characters/export_native_equipment.py` (10-08), which writes `GolfArcade/Unity/CharacterAssets/NativeGolfEquipment_*.json` (loaded by `NativeGolfEquipment.swift`) and `Unity/Assets/Resources/Golf/Equipment`.
- **Rebuild needs these inputs:**
  - `blender/matchhero/` (gitignored, **absent**, see PIPE-09).
  - `blender/mhhair/` (gitignored, **absent**; re-download with `fetch_mhhair.py`).
  - `Unity/Assets/Resources/Golfer/golfer_m.fbx` + `golfer_m_clips.json`: the `--v4` default input, which supplies the clubs and the Idle/Wave/Cheer/FistPump motion. **Do not delete these two without moving them.**
- **If removed:** The live golfer could not be rebuilt, and the native club meshes could not be regenerated.
- **Verdict: KEEP.** Confidence: high.

### PIPE-09 — `fetch_matchheroes.sh` (1 script)
- **What:** Downloads 22 match-hero source FBXs from `origin/newmapsandmenus` through GitHub LFS media URLs (using the `gh` token) into `blender/matchhero/`: the ReadyIdle rigs, the kits, the HeroBase `Hair_Default_M|F` hair, and Walk/Run/emote/intro clips.
- **Size / git:** tracked, last commit 2026-10-05.
- **Evidence:**
  - It is step 1 of the documented golfer rebuild (`docs/matchhero-golfer.md`, `matchhero_golf.py` docstring, `.gitignore`).
  - The same files now exist in this branch under `Unity/Assets/Characters/{MatchHeroes,MatchHeroKit,HeroBase/Models}`, possibly newer than the branch snapshot. `matchhero_golf.py --src` could point at them instead.
- **If removed:** The documented rebuild step breaks unless you switch to local sources (edit the docstring and doc).
- **Verdict: DECIDE.** Choose between rebuilding the golfer from the branch snapshot or from the current local heroes. Confidence: medium.

### PIPE-10 — `cmu_golf_measure.py` (1 script)
- **What:** Measures real golf form from CMU mocap BVHs in `blender/cmugolf/` (gitignored, **absent**).
- **Size / git:** tracked, last commit 2026-10-06.
- **Evidence:** Nothing imports it. Its results are written into `golf_swing.py`'s constants and comments.
- **If removed:** No effect. You lose the record of how the swing numbers were measured.
- **Verdict: DECIDE** (provenance only). Confidence: medium.

### PIPE-11 — `adnan_golfer_export.py` (1 script)
- **What:** Exports the V4 studio golfer (from `SportsLibrary/Blender/sports-animation-studio-v4.blend` + `golf_clubs.py`) to `Resources/Golfer/golfer_m|f.fbx`, `_clips.json` and `higgs_*_color.png`.
- **Size / git:** tracked, last commit 2026-09-23.
- **Evidence:**
  - At runtime that FBX is only a fallback: `GolferStyle.UseHero` is true whenever `Hero/golfer_m` exists. The PlayMode test `HeroGolferTests.TheV4GolferForComparison` forces it on.
  - It **is** the `--v4` input of the live `matchhero_golf.py` (PIPE-08).
- **If removed:** Nothing changes until someone needs to regenerate the V4 input.
- **Verdict: DECIDE.** Confidence: high.

### PIPE-12 — Higgsfield golf bodies: `fit_golf_characters.py` + `blender/characters/golf/*` (8 files)
- **What:** Golf versions of the Sept Higgsfield players (2 Tripo GLBs, design sheets, target art, README) and the script that fitted them to the V4 golf rig.
- **Size / git:** 6.9 MB, tracked, 5 LFS, content 2026-09-23.
- **Evidence:** They only shaped the body inside the fallback V4 golfer. `matchhero_golf.py` ignores that body, and the fitting step is optional in `adnan_golfer_export.py`. No other reader.
- **If removed:** No effect on the game or on any live rebuild.
- **Verdict: SAFE.** Confidence: medium-high.

### PIPE-13 — Icon avatar kit `avatar_{base,catalog,export,head_parts,icons,mock,parts,styles,wardrobe}.py` (9 scripts)
- **What:** The "3D icon avatar" kit: a body, head and 15 haircuts/hats as parts.
- **Size / git:** 0.1 MB, tracked, last commit 2026-10-04.
- **Evidence:**
  - Its outputs (`Resources/Hero/hero_*.fbx`, icon tiles) were deleted from Resources on 2026-10-05 (`7c790cf6`). Copies sit in `Unity/Assets/Archive/IconKit/` (18 MB, Unity auditor's area).
  - `docs/avatar-kit.md` says it is "retired from the game", and `docs/matchhero-golfer.md` says it is kept "for when its hats and hair are fitted to these heads".
  - `avatar_kit.py` is excluded from this group (PIPE-08 needs it).
- **If removed:** No game effect. You lose a parked hats/hair option.
- **Verdict: DECIDE.** Confidence: high.

### PIPE-14 — Scanned Hero01 repair chain `hero_{avatar,face,golf_retarget,hair,hats,head,parts_export,repair}.py` (8 scripts)
- **What:** Repaired and re-exported Adnan's scanned Hero01 parts and retargeted the golf clips onto them, producing `Resources/Hero/hero_*.fbx`.
- **Size / git:** 0.1 MB, tracked, last commits 2026-09-29/10-04.
- **Evidence:**
  - `docs/hero-golfer.md` (2026-09-30) says these are "no longer used by the game", and the outputs were deleted on 2026-10-05.
  - Only comments in `HeroKit.shader` still name `hero_head`/`hero_repair`.
- **If removed:** Nothing changes.
- **Verdict: SAFE.** Confidence: high.
## Findings — blender/ (tennis and other)

### PIPE-15 — Hidden tennis gameplay rig export: `export_tennis_runtime.py`, `tennis_clips.py`, `blender/tennis-clips.json`, `SportsLibrary/Blender/sports-animation-studio-v4.blend` (4 files)
- **What:** The V4 "standard" tennis rig with its 44 named clips. The studio `.blend` (357.8 MB, LFS) is exported to `Unity/Assets/Resources/StandardCharacters/standard_{male,female}_tennis.fbx`, and the clip splits are written to `blender/tennis-clips.json`.
- **Size / git:** 357.9 MB, tracked. The studio was last committed 2026-09-24; the scripts 2026-09-22/23.
- **Evidence:**
  - `TennisActor.Build` (TennisActor.cs:245) loads `StandardCharacters/standard_*_tennis` for the player and the opponent **every match**.
  - That actor is hidden but is the gameplay authority: `HeroTennisDriver` reads its timing and contact. It plays the FBX clips (`TennisActor.cs:302`).
  - `Unity/Assets/Editor/GolferModelImporter.cs:74` reads `../../blender/tennis-clips.json` on every import of that FBX. If the file is missing, the rig imports "without clip splits".
  - The same studio blend is the source of `standard_*_golf.fbx` (PIPE-20) and the V4 golfer (PIPE-11), and the space `golf_clubs.py` builds in.
  - It stores image paths to sibling folders outside the repo (`../sports-character-library/...`). Those are reference image planes only.
- **If removed:** Deleting the json breaks the next reimport of the hidden rig. Deleting the blend or scripts leaves the live gameplay rig impossible to rebuild.
- **Verdict: KEEP.** Confidence: high.

### PIPE-16 — Umpire and island builders: `prepare_umpire.py`, `prepare_island.py` + their sources (6 files)
- **What:** The scripts that made the umpire and the resort island, plus their inputs:
  - `SportsLibrary/ArtDirection/Tennis/Generated/models/umpire.glb` and `umpire-turnaround.jpg` → `Resources/Tennis/Umpire/TennisUmpire.fbx` + `Tennis/Characters/HiggsUmpire_*`.
  - `models/island.glb` and `island-design.png` → `Resources/Tennis/Island/TennisIsland.fbx` + `Island_*`.
- **Size / git:** 11.4 MB, tracked, 4 LFS, last commit 2026-09-23.
- **Evidence:**
  - `TennisUmpire.Spawn` runs in every match (TennisGame.cs:247).
  - The island loads for the Resort venue (TennisGame.cs:239) and is preloaded in the hub (`NativeSportsSession.Hub.cs:115`).
- **If removed:** The game is unaffected, but the live umpire and island could not be rebuilt.
- **Verdict: DECIDE.** Confidence: high.

### PIPE-17 — Sept 22–24 tennis character/animation chain (35 scripts)
- **What:** The V4/V5 + Higgsfield + video-retarget era of tennis characters:
  - author_tennis_clips, fix_tennis_racket_and_backhand, author_tennis_motion, tennis_motion
  - retarget_intro_emotes, retarget_emotes, retarget_video_pose, soften_knees, settle_clips, build_v5_rig
  - build_video_character, install_tripo_kit, kit_compare, fit_higgs_characters, check_character_anatomy, measure_landmarks
  - fit_opponents, fit_avatar, prepare_avatar, coherent_avatar_body, real_arms
  - author_tennis_faces, build_face_atlas, build_avatar_face_atlas, bake_kit_mask
  - render_clip_sheets, make_clip_sheets, reference_spec
  - rotomate_{fuse,sam,sheets,track}, sam_tiles_to_poses, tile_video_frames, track_video_racket
- **Size / git:** 0.3 MB, tracked, last commits 2026-09-22..24. No work/ job has run them since (the work/ hits are copies).
- **Evidence:**
  - They edited the studio blend in place (PIPE-15) or wrote character outputs. Their results are now baked into the studio and the hidden rig FBX, which carries the "V4 Higgs body" and "V4 face decal".
  - The visible characters are now MatchHeroes (from untracked `work/match-anim-set`).
  - Dead outputs: `Tennis/Opponents/*.fbx` (`TennisActor.WearBody` returns false; `TennisRoster`: "not loaded"); `FaceAtlas_Avatar*` and `Higgs<Rival>_*` textures (Unity auditor's area).
  - These scripts import each other (e.g. `fit_avatar` is imported by 6 of them), so delete them as a unit.
  - Only C# comments name them: TennisActor.cs, ClipContactAudit.cs, TennisLook.cs, TennisGame.cs, GolferModelImporter.cs, TennisCharacterTextureImporter.cs.
- **If removed:** The game and the live pipelines are unaffected. You lose the ability to re-author the hidden rig's clips, which nobody does now.
- **Verdict: SAFE** (as a unit). Confidence: medium-high.

### PIPE-18 — Seated tennis crowd: `build_crowd.py` + `blender/crowd-clips.json` (2 files)
- **What:** Built `Resources/Tennis/Crowd/crowd_{male,female}.fbx` and their clip manifest.
- **Size / git:** tracked, last commit 2026-09-23.
- **Evidence:**
  - The live stands use `Tennis/Premium/TennisSeatedHero3` (`TennisStandsCrowd.cs:51`). No runtime code loads `Tennis/Crowd`.
  - `Unity/Assets/Editor/TennisCrowdImporter.cs:25` still reads `../../blender/crowd-clips.json` whenever those FBXs are reimported, and warns if it is missing.
- **If removed:** Fine, provided the follow-up below is done.
- **Verdict: SAFE+FIX.** Delete it together with `Resources/Tennis/Crowd/*.fbx` and `Editor/TennisCrowdImporter.cs`, or keep the json. Confidence: high.

### PIPE-19 — Old customizable player bases: 6 scripts + `SportsLibrary/Customization/**` (22 files)
- **What:** The Sept 25–28 player-base system:
  - Scripts: `export_player_bases.py`, `export_customization.py`, `fit_base_hair.py`, `fit_golf_bases.py`, `build_sportswear.py`, `render_sportswear_study.py`.
  - Sources: `Customization/Bases/Player{Male,Female}-rigged.blend` (84 MB each), `{male,female}-source.glb`, the 4 `*-fitted.blend`, and 4 Tripo hair/head FBX + task.json.
- **Size / git:** 308.9 MB, tracked, 12 LFS, last commit 2026-09-28.
- **Evidence:**
  - Outputs `Resources/Tennis/Customization/{Custom*,Player*Bob/Curls/Swept/Color/Mask*,Player*Golf.fbx,HiggsAvatar*}` and `GolfArcade/Unity/CharacterAssets/{Custom*,PlayerMale*,PlayerFemale*,HiggsAvatar*}` are not loaded:
    - `TennisCustomization.Apply` returns early on the hidden rig.
    - `AttachBase(golf:true)` is never called.
    - Swift loads only `MatchHero_*`, `GolfKitHero_*` and `NativeGolfEquipment_*`.
    - No prefab or scene references their GUIDs.
  - `Resources/Tennis/Customization/PlayerMale|Female.prefab` are now the MatchHeroes (built in Unity from work/), not from here.
  - The rigged blends point at `Tennis/Customization/Player*Color.png`.
- **If removed:** No effect.
- **Verdict: SAFE.** The dead Unity/Swift outputs belong to other auditors. Confidence: high.

### PIPE-20 — V4 standard test-fixture exporters: `export_standard_golf.py`, `export_arm_validation.py`, `SportsLibrary/Animations/V4/Reports/action-manifest.json` (3 files)
- **What:** They export `Resources/StandardCharacters/standard_*_golf.fbx` and `Unity/Assets/Tests/Fixtures/StandardCharacters/*.fbx`.
- **Size / git:** tracked, last commit 2026-09-20.
- **Evidence:** No runtime reader. They are used only by the EditMode tests `StandardGolfGripTests` and `StandardArmSportTests` and by `GolferModelImporter`. The golf runtime uses `Hero/golfer_*`.
- **If removed:** Those fixtures could not be regenerated.
- **Verdict: DECIDE** (follows whether those tests are kept). Confidence: high.

### PIPE-21 — `blender/holes_mock/` (20 files)
- **What:**
  - 8 Higgsfield concept PNGs for holes 13–20.
  - 11 `hole_NN_numbers.txt` files: C# hole blocks that `course_builder.py` writes on every build, creating the folder if needed.
  - A README.
- **Size / git:** 8.8 MB, tracked, 8 LFS, content 2026-09-23..10-04.
- **Evidence:** Referenced only in design-module docstrings and `docs/magma-open.md`.
- **If removed:** No effect.
- **Verdict: SAFE** (keep the PNGs only if you want the concept art). Confidence: high.

### PIPE-22 — `blender/previews/` (9 tracked + 3 gitignored)
- **What:**
  - Tracked render stills: `golfer_closeups`, `hole_07/12` overviews, `pin`.
  - UI option mockups: `home_screen/C-on-the-tee`, `landing_popups/A-stamp-badges`, `ui_options/*`.
  - Gitignored: `hole_08/09/10_overview.png`.
- **Size / git:** 14.9 MB (9.6 MB tracked, 7 LFS; 5.3 MB ignored).
- **Evidence:** Build scripts write here and recreate the folder (`makedirs`). Nothing reads the files.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-23 — Python caches: `blender/scripts/__pycache__/` (17 files) + `Tools/HeroBase/__pycache__/` (11 files)
- **Size / git:** 3.1 MB, gitignored, untracked.
- **Verdict: SAFE.** Confidence: high.
## Findings — SportsLibrary/

Overall: 801 tracked files (2,515 MB), 617 of them LFS, plus 10 gitignored `.blend1` files (925 MB).

**No runtime C# or Swift, no CI, no Xcode project and no Unity importer reads anything under `SportsLibrary/`.** The only Unity mention is a comment in `TennisReferenceMatch.cs`. Every live use is indirect: a `blender/scripts` export, or an exporter outside the repo, wrote a file into `Unity/Assets`.

The source library dates from 2026-09-20..24, with snapshots on 09-28/29. `asset-inventory.json` covers only 268 of the 801 files.

### PIPE-24 — `Arenas/Tennis/tropical-tennis-resort-v3.blend` (1 file)
- **What:** The source of the live resort arena, `Resources/Tennis/TropicalV3/TropicalTennisResort.fbx` (loaded by TennisGame.cs:223 for every venue; the sky and crater venues keep its court kit).
- **Size / git:** 358.7 MB, LFS, last commit 2026-09-28. That commit is a snapshot newer than the 2026-09-21 export now in Unity.
- **Evidence:**
  - Its exporter is **outside the repo**: `../tennis-tropical-rebuild-v3/Tools/export_tropical_unity.py`.
  - The only external paths it stores point at that sibling folder. It does not link the Tripo-v3 GLBs (PIPE-25).
- **If removed:** The game is unaffected, but the live arena could no longer be edited or re-exported from this repo.
- **Verdict: DECIDE** (KEEP if the arena might be re-exported). Confidence: high.

### PIPE-25 — `Arenas/Tennis/Tripo-v3/Assets/*` (69 files)
- **What:** 23 raw generated GLBs (palms, clubhouse, stands, court, net and more), with a preview PNG and task.json each.
- **Size / git:** 91.4 MB, tracked, 46 LFS, last commit 2026-09-22.
- **Evidence:** The v3 arena blend contains its own imported copies (no external path to them). No script reads them.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: medium-high.

### PIPE-26 — Coastal tennis resort v1/v2, the archived predecessor (8 files)
- **What:** `coastal-tennis-resort-v1.blend`, `coastal-tennis-resort-gameplay-v2.blend`, `gameplay-landscaping-validation.json`, `gameplay.png`, `resort-overview.png`, `clubhouse-detail.png`, `scene-report.json`, `validation.json`.
- **Size / git:** 12.1 MB, tracked, 5 LFS, last commit 2026-09-28.
- **Evidence:**
  - `Arenas/Tennis/README.md` calls it the "archived predecessor".
  - Its export `Resources/Tennis/CoastalTennisResort.fbx` has no C# reader (dead; Unity auditor).
  - The arena half of `export_tennis_runtime.py` opens v1 and **rewrites** v2 and the json unless run with `--tennis-only`.
- **If removed:** Fine once the follow-up below is done.
- **Verdict: SAFE+FIX.** Remove the arena half of `export_tennis_runtime.py`, or always pass `--tennis-only`. Confidence: high.

### PIPE-27 — Bowling and boxing arenas, original arena FBXs, crowds (41 files)
- **What:** `Arenas/Bowling/*`, `Arenas/Boxing/*`, `Arenas/Originals/*` (3 supplied court/lane/ring FBXs) and `Crowds/*` (4 crowd blends and renders).
- **Size / git:** 74.6 MB, tracked, 29 LFS, last commit 2026-09-20.
- **Evidence:** Bowling and boxing are not in the game. The live crowds are `Tennis/Premium/*Hero3`. No reader.
- **If removed:** No effect.
- **Verdict: SAFE** (keep only if those sports are planned). Confidence: high.

### PIPE-28 — `Blender/all-arenas-and-animations-v4.blend` (1 file)
- **What:** A combined master of all arenas and animations.
- **Size / git:** 104.1 MB, LFS, last commit 2026-09-20.
- **Evidence:** No script reads it. It is referenced only by SportsLibrary docs and the inventory, and the studio blend (PIPE-15) supersedes it.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-29 — `Characters/*` (11 files)
- **What:** The original "permanent standard" models `StandardMale/Female.glb` (121 MB), 8 identity reference PNGs and `standard-characters.json`.
- **Size / git:** 127.4 MB, tracked, 10 LFS, last commit 2026-09-20.
- **Evidence:** No script reads them; the rigged versions live in the studio blend. The json is read only by the stale `Tools/verify_library.py`. `AGENTS.md` names `ArtDir/hero/base_lock` as the character authority, and the MatchHeroes are the live characters.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: medium-high.

### PIPE-30 — `Clothing/*` (29 files)
- **What:** `wardrobe-catalog.png`, 4 concept PNGs, and `Tennis/Tripo-v3` (3 kit blends of 21/21/77 MB plus 7 GLB garments).
- **Size / git:** 149.0 MB, tracked, 22 LFS, last commit 2026-09-22.
- **Evidence:**
  - The external `build_runtime_tennis_kits.py` turned them into `Resources/Tennis/KitsV3/standard_*_kit.fbx`.
  - `TennisKitV3.Dress` only runs when the body is not the skinned Higgs body. The hidden rig *is* skinned, so the kit is never dressed (dead; Unity auditor).
  - The fitted-kit blends point at `Unity/Assets/Resources/Tennis/KitsV3/Textures`.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: medium-high.

### PIPE-31 — `Equipment/Tennis/Tripo-v3/*` (9 files)
- **What:** The racket and ball master `tennis-equipment-v3.blend` (20 MB) plus 4 GLBs.
- **Size / git:** 43.9 MB, tracked, 7 LFS, last commit 2026-09-22.
- **Evidence:** Source of `Resources/Tennis/KitsV3/racket.fbx` / `RacketGameplay`, which `TennisKitV3.Equip` puts on the hidden gameplay actor every match. The visible heroes use the classic racket from work/.
- **If removed:** The game is unaffected, but that racket could not be rebuilt.
- **Verdict: DECIDE.** Confidence: medium.

### PIPE-32 — `Equipment/*` generic V4 equipment (40 files)
- **What:** Golf, tennis, bowling and boxing FBX/GLB pairs, `standard-sports-equipment.blend`, overview PNGs and manifests.
- **Size / git:** 15.0 MB, tracked, 38 LFS, last commit 2026-09-20.
- **Evidence:** None of these are in Unity. The clubs come from `golf_clubs.py`. No reader.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-33 — `ArtDirection/Tennis/Generated/models/*` except umpire/island (26 files)
- **What:**
  - Raw base-avatar GLBs (198 MB) and Meshy emote GLBs (270 MB).
  - 4 opponent GLBs and 2 player GLBs, with their design sheets.
- **Size / git:** 518.1 MB, tracked, all LFS, last commit 2026-09-24.
- **Evidence:** Inputs of the retired PIPE-17 chain. Opponents and avatars are not loaded. The player bodies survive only as the invisible body of the hidden rig, already baked into the studio.
- **If removed:** No effect.
- **Verdict: SAFE.** This is the single biggest removable group. Confidence: high.

### PIPE-34 — `ArtDirection/Tennis/Generated/video/tennis-gameplay-concept.mp4` (1 file)
- **What:** The Higgsfield concept video.
- **Size / git:** 7.9 MB, LFS, last commit 2026-09-24.
- **Evidence:** It is the approved target for tennis look and motion. `docs/tennis-motion-reference.md` and your memory notes cite it, and `TennisReferenceMatch.cs` mentions it in a comment.
- **Verdict: KEEP** (reference). Confidence: high.

### PIPE-35 — Video working data: `frames24/` (241 jpg), `tracks/` (13 json), `serve-side/serve-side.mp4` (255 files)
- **What:** Frames extracted from the concept video, pose tracks, and a separate generated side-view serve clip.
- **Size / git:** 68.1 MB, tracked, 242 LFS, last commit 2026-09-24.
- **Evidence:** Only read by the retired rotomation/retarget scripts. The frames and tracks can be re-derived from the mp4.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-36 — Other ArtDirection images (76 files)
- **What:** `target-*.png`, `source-*.jpg`, and under `Generated/`: `base-avatars/`, `face-parts/`, `fx/`, `ui/` (including `ui/menu`), `FaceAtlas*.png`, `sky-panorama-source.png`, `Reference/`.
- **Size / git:** 80.9 MB, tracked, 75 LFS, last commit 2026-09-24.
- **Evidence:**
  - Unity holds its own shipped copies: 16 of the 29 checked are byte-identical to `Resources/Tennis/UI|Fx|Characters`, and 13 are processed variants.
  - No runtime or script reader except the retired chain.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-37 — `Design/VenueConcepts/*` skyscraper and volcano court concepts (6 files)
- **What:** Concept art for the live sky and crater tennis venues.
- **Size / git:** 36.7 MB, LFS, last commit 2026-09-29.
- **Evidence:** No reader. No other copy exists in ArtDir or work/venue-presence.
- **If removed:** No game effect. You lose the only copy of the art reference.
- **Verdict: DECIDE.** Confidence: high.

### PIPE-38 — `Design/InitialConceptLibrary-Historical/*`, `Design/VenueConcepts-Historical/*` (105 files)
- **What:** Labelled historical concept sheets.
- **Size / git:** 98.0 MB, tracked, 58 LFS, last commit 2026-09-20.
- **Evidence:** No reader.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-39 — `Previews/*` motion-review mp4s and `Animations/*` concept PNGs and V4 action jsons (44 files)
- **Size / git:** 51.3 MB, tracked, 33 LFS, last commit 2026-09-20.
- **Evidence:** No reader except `verify_library.py`, which is stale. The studio clip list is derived from NLA strips, not from these jsons.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-40 — `asset-inventory.json`, `Tools/verify_library.py`, `SportsLibrary/.gitignore` (3 files)
- **What:** The library's packaging inventory, its checker, and a local ignore file.
- **Evidence:**
  - The inventory lists 268 of 801 files.
  - The studio blend's size no longer matches, so the verifier would fail.
  - The `.gitignore` rules duplicate the root `.gitignore`.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

### PIPE-41 — SportsLibrary `*.md` (54 files): not judged here (docs auditor)
- **Evidence for the docs auditor:**
  - `TENNIS-GAMEPLAY.md` and `MULTIPLAYER.md` were edited on 2026-10-04/06 and are living docs.
  - The root `README.md` links `SportsLibrary/README.md`, `UNITY-HANDOFF.md` and `IOS-UNITY-INTEGRATION.md`.
- **If the whole folder goes:** Move those docs first and fix the README links.

### PIPE-44 — `.blend1` backups (10 files)
- **What:** Blender's automatic one-step backups of the studio, tropical v3, kits, player bases and fitted heads.
- **Size / git:** 924.7 MB, gitignored, local disk only.
- **If removed:** No effect.
- **Verdict: SAFE.** Confidence: high.

## Findings — Tools/HeroBase and local-only dirs

### PIPE-42 — `Tools/HeroBase/*` (26 tracked; pycache in PIPE-23)
- **What:** The 2026-09-30..10-02 plate-measured HeroBase toolchain:
  - `plate_profiles`, `measured_volume`, `build_static`, `build_male_silhouette`, `refine_male_silhouette`, `apply_male_form(_d)`, `build_albedo`
  - proof, verify and package scripts, `orbit_review.html`
- **Size / git:** 0.2 MB, tracked, not LFS. Committed in bulk on 2026-10-04 (`48f18349`); files last edited 2026-10-02.
- **Evidence:**
  - It writes `ArtDir/hero/base_lock/*` and `Unity/Assets/Characters/HeroBase/*`. The HeroBase prefabs are referenced only by `HeroBase_Studio.unity`, a review scene.
  - It reads untracked `work/male-silhouette`, `male-form`, `male-form-d` and `male-jaw-5`. Untracked `work/hero-base`, `female-parallel-limbs`, `female-hands-fingers` and `female-arms-form2` import it.
  - The live MatchHero build (`work/match-anim-set`) does **not** use it.
  - `ArtDir/hero/base_lock/proof/MALE_FORM_RESULTS.md` says `export_male_form.py`, `verify_male_form.py` and `verify_male_form_d.py` "fail by design".
  - One live trace: `build_static.py` exported `Hair_Default_{Male,Female}.fbx`. `fetch_matchheroes.sh` pulls those into the golfer build, where they become the golfer's "Classic" hair.
- **If removed:** No game effect. HeroBase plate-fitting could not resume, and the old work/ jobs would break if re-run.
- **Verdict: DECIDE.** It is tied to the LOCKED `base_lock` authority in `AGENTS.md`; archive it with `work/hero-base` if that phase is finished. Confidence: medium.

### PIPE-43 — Gitignored source dirs `blender/matchhero/`, `blender/mhhair/`, `blender/cmugolf/`
- **Status:** **Not present** on disk, so there is nothing to remove.
- **Note:** Rebuilding the live golfer (PIPE-08) first needs:
  - `fetch_matchheroes.sh`, which uses the `gh` token and branch `origin/newmapsandmenus`.
  - `fetch_mhhair.py`, which downloads from MakeHuman (CC0).
  - Optionally, the CMU BVH files for `cmu_golf_measure.py`.

## Hole .blend → course map (all 16 are live; Unity reads only the FBX, never the .blend)

| .blend | Hole | Live course | Unity runtime model | .blend size | Content last changed (FBX same commit?) | Built by | Notes |
|---|---|---|---|---|---|---|---|
| hole_07.blend | 7 Cliffside (island par 4) | Cliffside | Resources/Course/hole_07.fbx (0.5 MB) | 0.38 MB | 2026-09-20 (FBX 2026-09-20) | build_all.py + phase*.py + hole07_design/lib | plain git blob (not LFS); also the rock/tree library read by postcard_lib.import_rocks (sha-checked) and course_builder; stores Andrew's render path |
| hole_08.blend | 8 Needle | Cliffside | Resources/Course/hole_08.fbx (4.4 MB) | 2.19 MB | 2026-10-05 (FBX 2026-10-05) | hole08_build/design/look + postcard_* libs | material images point at /Users/adnanyonathan/.../Unity/Assets/Resources/Course/Look/*.png (30 abs. paths) |
| hole_09.blend | 9 Split | Cliffside | Resources/Course/hole_09.fbx (6.1 MB) | 3.01 MB | 2026-10-05 (FBX 2026-10-05) | hole09_build/design/look/masonry/stone + postcard_* libs | same Look-texture abs. paths; at runtime GolfCoastalComposition swaps in Course/Resort/ReferenceHole09.fbx (built from hole_09.fbx) unless VISUAL_COASTAL_REFERENCE=0 |
| hole_10.blend | 10 Crater | Magma Open | Resources/Course/hole_10.fbx (7.9 MB) | 4.79 MB | 2026-10-05 (FBX 2026-10-05) | hole10_build/design/look + postcard_* libs | same Look-texture abs. paths |
| hole_12.blend | 12 Island carry | Cliffside | Resources/Course/hole_12.fbx (2.0 MB) | 1.32 MB | 2026-09-21 (FBX 2026-09-21) | hole12_prepare.py (from Codex scene, not in repo) | stores /Users/andrewloniewski/... path; runtime swaps in Course/Resort/ReferenceHole12.fbx (work/full-visual-overhaul, untracked) unless VISUAL_COASTAL_REFERENCE=0 |
| hole_13.blend | 13 The Spiral | Wild Isles | Resources/Course/hole_13.fbx (0.7 MB) | 0.56 MB | 2026-09-26 (FBX 2026-09-26) | course_builder + hole13_spiral_design |  |
| hole_14.blend | 14 The Witch's Lair | Wild Isles | Resources/Course/hole_14.fbx (0.7 MB) | 0.61 MB | 2026-09-23 (FBX 2026-09-23) | course_builder + hole14_lair_design |  |
| hole_15.blend | 15 The Steps | Cliffside | Resources/Course/hole_15.fbx (0.4 MB) | 0.43 MB | 2026-09-23 (FBX 2026-09-23) | course_builder + hole15_steps_design |  |
| hole_16.blend | 16 Volcano Rim | Magma Open | Resources/Course/hole_16.fbx (1.6 MB) | 1.15 MB | 2026-09-26 (FBX 2026-09-26) | course_builder + course_extras + hole16_volcano_design |  |
| hole_17.blend | 17 Frostbite Fjord | Wild Isles | Resources/Course/hole_17.fbx (1.1 MB) | 0.89 MB | 2026-09-26 (FBX 2026-09-26) | course_builder + course_extras + hole17_fjord_design |  |
| hole_18.blend | 18 Mesa Canyon | Wild Isles | Resources/Course/hole_18.fbx (0.5 MB) | 0.39 MB | 2026-09-26 (FBX 2026-09-26) | course_builder + course_extras + hole18_mesa_design |  |
| hole_19.blend | 19 Temple Falls | Wild Isles | Resources/Course/hole_19.fbx (1.7 MB) | 1.21 MB | 2026-09-26 (FBX 2026-09-26) | course_builder + course_extras + hole19_temple_design |  |
| hole_20.blend | 20 Windmill Links | Wild Isles | Resources/Course/hole_20.fbx (0.9 MB) | 0.68 MB | 2026-09-26 (FBX 2026-09-26) | course_builder + course_extras + hole20_windmill_design |  |
| hole_21.blend | 21 Obsidian Slab | Magma Open | Resources/Course/hole_21.fbx (0.5 MB) | 0.36 MB | 2026-10-04 (FBX 2026-10-04) | course_builder + course_extras + magma_shapes + hole21_slab_design |  |
| hole_22.blend | 22 Ember Causeway | Magma Open | Resources/Course/hole_22.fbx (0.6 MB) | 0.43 MB | 2026-10-04 (FBX 2026-10-04) | course_builder + course_extras + magma_shapes + hole22_causeway_design |  |
| hole_23.blend | 23 Caldera Crown | Magma Open | Resources/Course/hole_23.fbx (0.8 MB) | 0.55 MB | 2026-10-04 (FBX 2026-10-04) | course_builder + course_extras + magma_shapes + hole23_caldera_design |  |

Other blend files in `blender/`: `pin.blend` is the source of the live `Course/pin.fbx` (PIPE-04). `golfer_m.blend` and `golfer_f.blend` are Andrew's superseded golfer (PIPE-07).

## Per-script table — blender/scripts (140 tracked files + __pycache__)

Sorted by finding. "Last commit" is the newest commit that touched the file.

| Script | Lines | Last commit | Purpose | Called by / reads | Output still used in game? | Verdict | Finding |
|---|---|---|---|---|---|---|---|
| `build_all.py` | 14 | 2026-09-20 | Runs the Hole 7 phase scripts in order | manual (hard-coded Andrew paths via hole07_lib) | hole_07.blend/.fbx LIVE | **KEEP** | PIPE-02 |
| `course_builder.py` | 572 | 2026-10-04 | Builds holes 13-23 (.blend, FBX, card, numbers) from design modules | manual; used by work/golf-course-standard, full-visual-overhaul | hole_13..23.fbx + cards LIVE | **KEEP** | PIPE-02 |
| `course_extras.py` | 769 | 2026-10-04 | Themes/water/lava/landmarks for course_builder | imported by course_builder | LIVE holes 16-23 | **KEEP** | PIPE-02 |
| `hole07_design.py` | 152 | 2026-09-20 | Hole 7 design data (pure math) | phase scripts, course_builder, postcard lib | LIVE hole 7 (+numbers in Hole.cs) | **KEEP** | PIPE-02 |
| `hole07_lib.py` | 262 | 2026-09-25 | Hole 7 build helpers (Andrew's absolute BLEND_PATH) | phase scripts; imported by course_builder | LIVE holes 7, 13-23 | **KEEP** | PIPE-02 |
| `hole13_spiral_design.py` | 131 | 2026-09-26 | Hole 13 design module | course_builder | LIVE (Wild Isles) | **KEEP** | PIPE-02 |
| `hole14_lair_design.py` | 90 | 2026-09-23 | Hole 14 design module | course_builder | LIVE (Wild Isles) | **KEEP** | PIPE-02 |
| `hole15_steps_design.py` | 89 | 2026-09-23 | Hole 15 design module | course_builder | LIVE (Cliffside) | **KEEP** | PIPE-02 |
| `hole16_volcano_design.py` | 124 | 2026-09-26 | Hole 16 design module | course_builder | LIVE (Magma Open) | **KEEP** | PIPE-02 |
| `hole17_fjord_design.py` | 103 | 2026-09-26 | Hole 17 design module | course_builder | LIVE (Wild Isles) | **KEEP** | PIPE-02 |
| `hole18_mesa_design.py` | 74 | 2026-09-26 | Hole 18 design module | course_builder | LIVE (Wild Isles) | **KEEP** | PIPE-02 |
| `hole19_temple_design.py` | 91 | 2026-09-26 | Hole 19 design module | course_builder | LIVE (Wild Isles) | **KEEP** | PIPE-02 |
| `hole20_windmill_design.py` | 92 | 2026-09-26 | Hole 20 design module | course_builder | LIVE (Wild Isles) | **KEEP** | PIPE-02 |
| `hole21_slab_design.py` | 81 | 2026-10-04 | Hole 21 design module | course_builder | LIVE (Magma Open) | **KEEP** | PIPE-02 |
| `hole22_causeway_design.py` | 93 | 2026-10-04 | Hole 22 design module | course_builder | LIVE (Magma Open) | **KEEP** | PIPE-02 |
| `hole23_caldera_design.py` | 118 | 2026-10-04 | Hole 23 design module | course_builder | LIVE (Magma Open) | **KEEP** | PIPE-02 |
| `magma_shapes.py` | 107 | 2026-10-04 | Pure geometry helpers for holes 21-23 | hole21-23 design modules | LIVE | **KEEP** | PIPE-02 |
| `phase11_13_look.py` | 130 | 2026-09-20 | Hole 7 materials/look | build_all | LIVE hole 7 | **KEEP** | PIPE-02 |
| `phase1_setup.py` | 84 | 2026-09-20 | Hole 7 scene setup | build_all | LIVE hole 7 | **KEEP** | PIPE-02 |
| `phase2_5_course.py` | 219 | 2026-09-20 | Hole 7 terrain/course | build_all | LIVE hole 7 | **KEEP** | PIPE-02 |
| `phase6_trees.py` | 202 | 2026-09-20 | Hole 7 trees (also exec'd by course_builder) | build_all, course_builder | LIVE | **KEEP** | PIPE-02 |
| `phase7_rocks.py` | 150 | 2026-09-20 | Hole 7 rocks (also exec'd by course_builder) | build_all, course_builder | LIVE | **KEEP** | PIPE-02 |
| `phase8_path.py` | 42 | 2026-09-20 | Hole 7 path | build_all | LIVE hole 7 | **KEEP** | PIPE-02 |
| `phase9_10_clubhouse_gameplay.py` | 145 | 2026-09-20 | Hole 7 clubhouse + gameplay markers | build_all | LIVE hole 7 | **KEEP** | PIPE-02 |
| `POSTCARDS_README.md` | 98 | 2026-10-06 | Contract/how-to for the holes 8/9/10 build (design -> build -> verify -> emit C#) | doc for the postcard scripts | describes live pipeline | **KEEP** | PIPE-03 |
| `golf_look_textures.py` | 3249 | 2026-10-05 | Generates Course/Look textures for postcard holes | manual | LIVE (Course/Look; also referenced by hole_08/09/10.blend) | **KEEP** | PIPE-03 |
| `hole08_build.py` | 572 | 2026-10-05 | Builds hole 8 Needle (.blend, FBX, overview) | manual | hole_08.fbx LIVE | **KEEP** | PIPE-03 |
| `hole08_design.py` | 445 | 2026-10-06 | Hole 8 design numbers (yards) | postcard_check/emit_cs; Tools/PostcardCheck | LIVE (Hole.cs numbers) | **KEEP** | PIPE-03 |
| `hole08_look.py` | 2596 | 2026-10-05 | Hole 8 look pass | hole08_build | LIVE | **KEEP** | PIPE-03 |
| `hole09_build.py` | 240 | 2026-10-05 | Builds hole 9 Split | manual | hole_09.fbx LIVE (input of ReferenceHole09) | **KEEP** | PIPE-03 |
| `hole09_design.py` | 457 | 2026-10-06 | Hole 9 design numbers | postcard_check; Tools/PostcardCheck | LIVE | **KEEP** | PIPE-03 |
| `hole09_look.py` | 1447 | 2026-10-05 | Hole 9 look pass | hole09_build | LIVE | **KEEP** | PIPE-03 |
| `hole09_masonry.py` | 1247 | 2026-10-05 | Hole 9 ruin masonry | hole09_look | LIVE | **KEEP** | PIPE-03 |
| `hole09_qa.py` | 810 | 2026-10-05 | Hole 9 gates (read-only) | manual | gate tool | **KEEP** | PIPE-03 |
| `hole09_stone.py` | 304 | 2026-10-05 | Planar stones for hole 9 ruin | hole09_look/masonry | LIVE | **KEEP** | PIPE-03 |
| `hole10_build.py` | 638 | 2026-10-05 | Builds hole 10 Crater | manual | hole_10.fbx LIVE | **KEEP** | PIPE-03 |
| `hole10_design.py` | 463 | 2026-10-06 | Hole 10 design numbers | postcard_check; Tools/PostcardCheck | LIVE | **KEEP** | PIPE-03 |
| `hole10_look.py` | 1353 | 2026-10-05 | Hole 10 walls/crust/cones | hole10_build | LIVE | **KEEP** | PIPE-03 |
| `postcard_check.py` | 447 | 2026-10-05 | Pure-python mirror of Hole.cs scoring gates | design modules; Tools/PostcardCheck | gate tool for live holes | **KEEP** | PIPE-03 |
| `postcard_emit_cs.py` | 130 | 2026-10-05 | Emits/checks Course.Postcards() C# block | manual | keeps Hole.cs in sync | **KEEP** | PIPE-03 |
| `postcard_lib.py` | 2072 | 2026-10-05 | bpy library for postcard builds (reads hole_07.blend rocks) | hole08/09/10_build | LIVE | **KEEP** | PIPE-03 |
| `postcard_lib_smoke.py` | 540 | 2026-10-05 | Self-test of postcard_lib (synthetic hole 90) | manual | test only | **KEEP** | PIPE-03 |
| `postcard_look_lib.py` | 3478 | 2026-10-05 | Ground/water/wall/lava look pass | builds | LIVE | **KEEP** | PIPE-03 |
| `postcard_look_smoke.py` | 328 | 2026-10-05 | Smoke test/gates for look lib | manual | test only | **KEEP** | PIPE-03 |
| `postcard_look_verify.py` | 9313 | 2026-10-05 | Blender-side look gates | builds/qa | gate tool | **KEEP** | PIPE-03 |
| `postcard_plan_png.py` | 67 | 2026-10-05 | Plan-view PNG of a design | manual | review only | **KEEP** | PIPE-03 |
| `postcard_props_lib.py` | 5408 | 2026-10-05 | Procedural props (rocks, plants, stacks) | builds, golf_course_standard_props | LIVE | **KEEP** | PIPE-03 |
| `postcard_props_smoke.py` | 2793 | 2026-10-05 | Props gates/contact sheets | manual | test only | **KEEP** | PIPE-03 |
| `postcard_smoke_design.py` | 104 | 2026-10-05 | Synthetic hole-90 design for postcard_lib_smoke | postcard_lib_smoke | test only | **KEEP** | PIPE-03 |
| `postcard_verify.py` | 1438 | 2026-10-05 | Mesh-level gate checker for built holes | builds | gate tool | **KEEP** | PIPE-03 |
| `backdrop_import.py` | 101 | 2026-09-22 | Decimates a generated GLB into Resources/Course/Backdrop (island, sailboat) | manual; input GLBs not in repo | Course/Backdrop/island/sailboat.fbx LIVE (Backdrop.Place in HoleView.cs:341) | **KEEP** | PIPE-04 |
| `build_reference_hole09.py` | 108 | 2026-10-08 | Alternate coastal composition of hole 9 from Course/hole_09.fbx -> Course/Resort/ReferenceHole09.fbx | manual (2026-10-08) | LIVE: GolfCoastalComposition swaps hole 9 to it by default | **KEEP** | PIPE-04 |
| `golf_course_standard_props.py` | 16 | 2026-10-06 | Exports shared postcard tufts -> Course/Standard/Fringe.fbx | manual; imports postcard_props_lib | LIVE (GolfCourseFringe.cs) | **KEEP** | PIPE-04 |
| `golf_course_standard_textures.py` | 81 | 2026-10-06 | Generates Course/Standard specialty textures | manual (numpy/Pillow) | LIVE (GolfCourseLook.cs) | **KEEP** | PIPE-04 |
| `pin_build.py` | 238 | 2026-09-22 | Builds cup/flagstick/flag -> blender/pin.blend + Course/pin.fbx | manual | Course/pin.fbx LIVE (HoleView.cs:469) | **KEEP** | PIPE-04 |
| `hole12_animate_finish.py` | 385 | 2026-09-21 | Finished Codex 'animated hole 12' (ball json, exports) | manual; input Hole_12_Animated.blend NOT in repo | part of hole 12 provenance | **DECIDE** | PIPE-05 |
| `hole12_cinematic_export.py` | 145 | 2026-09-21 | Exports Codex cinematic: hole_12_cinematic.fbx, hole_12_shot.json, swing_trail.json | manual; input Hole_12_Cinematic.blend NOT in repo | outputs LIVE (CinematicRig, SignatureShot, ShotEffects, DressBall) | **DECIDE** | PIPE-05 |
| `hole12_prepare.py` | 434 | 2026-09-21 | Turned Codex hole-12 scene into blender/hole_12.blend + hole_12.fbx | manual; input not in repo | hole_12.fbx LIVE (input of ReferenceHole12) | **DECIDE** | PIPE-05 |
| `turf_textures.py` | 103 | 2026-09-22 | Baked grey turf detail tiles -> Course/Turf/*.png | none; comments in HoleView.cs:685, Turf.shader | OUTPUT REPLACED 2026-09-23 by Higgsfield tiles (e9544183) | **SAFE** | PIPE-06 |
| `golfer_build.py` | 494 | 2026-09-20 | Andrew's original procedural low-poly golfer (hard-coded /Users/andrewloniewski paths) | build_all.py comment only | Golfer/golfer_m/f.fbx overwritten 9/21-23; re-running would overwrite the current V4 golfer | **SAFE** | PIPE-07 |
| `golfer_export.py` | 21 | 2026-09-20 | FBX export for golfer_build | build_all.py comment only | superseded | **SAFE** | PIPE-07 |
| `golfer_preview.py` | 18 | 2026-09-20 | Preview renders for golfer_build | none | superseded | **SAFE** | PIPE-07 |
| `avatar_kit.py` | 436 | 2026-10-04 | Shared Blender helpers (tube/blob/join, clay materials) | IMPORTED by matchhero_hair.py and matchhero_golf.py (live golfer build) | needed to rebuild Hero/golfer_m/f.fbx | **KEEP** | PIPE-08 |
| `fetch_mhhair.py` | 107 | 2026-10-06 | Downloads CC0 MakeHuman hairs into blender/mhhair/ and stylises textures | step of the live golfer rebuild | Hero/Hair/*.png LIVE | **KEEP** | PIPE-08 |
| `golf_clubs.py` | 305 | 2026-10-08 | Builds the 4 golf club meshes | imported by adnan_golfer_export and work/full-visual-overhaul/characters/export_native_equipment.py (10-08) | NativeGolfEquipment_*.json (Swift) + Resources/Golf/Equipment + clubs in golfer FBX LIVE | **KEEP** | PIPE-08 |
| `golf_swing.py` | 954 | 2026-10-06 | Authors the golf swings on the match-hero rig | imported by matchhero_golf.py | LIVE clips in Hero/golfer_*.fbx | **KEEP** | PIPE-08 |
| `matchhero_golf.py` | 1403 | 2026-10-06 | Builds Hero/golfer_m/f.fbx (match hero + clubs + 9 golf clips + hair) + clips json | manual (last run 10-06); reads blender/matchhero, Resources/Golfer/golfer_m.fbx (--v4) | LIVE (HeroGolfer.PathFor) | **KEEP** | PIPE-08 |
| `matchhero_hair.py` | 234 | 2026-10-06 | Hair styles for the match-hero golfer | imported by matchhero_golf | LIVE | **KEEP** | PIPE-08 |
| `matchhero_icons.py` | 31 | 2026-10-06 | Locker tiles -> Resources/UI/Look/hair_*.png, tab_outfit.png | manual | LIVE | **KEEP** | PIPE-08 |
| `matchhero_mhhair.py` | 368 | 2026-10-06 | Fits MakeHuman hairs to the match heroes | imported by matchhero_golf | LIVE | **KEEP** | PIPE-08 |
| `mhhair_sources.py` | 15 | 2026-10-06 | Which MakeHuman hairs are used | fetch_mhhair, matchhero_mhhair | LIVE | **KEEP** | PIPE-08 |
| `fetch_matchheroes.sh` | 22 | 2026-10-05 | Downloads match-hero FBX sources from origin/newmapsandmenus LFS into blender/matchhero/ | step 1 of the live golfer rebuild (docs/matchhero-golfer.md) | inputs of Hero/golfer_*.fbx; same files now exist locally under Unity/Assets/Characters | **DECIDE** | PIPE-09 |
| `cmu_golf_measure.py` | 260 | 2026-10-06 | Measures real golf form from CMU mocap (blender/cmugolf, not present) | nothing imports it; its numbers are written into golf_swing.py | indirect (tuning provenance of live swings) | **DECIDE** | PIPE-10 |
| `adnan_golfer_export.py` | 283 | 2026-09-23 | Exports the old V4 studio golfer (+golf_clubs meshes) to Resources/Golfer/golfer_m/f.fbx + _clips.json | run in Blender on SportsLibrary studio blend; imports golf_clubs | Golfer/golfer_m.fbx is the default --v4 INPUT of matchhero_golf.py and the runtime fallback (V4 comparison test only) | **DECIDE** | PIPE-11 |
| `fit_golf_characters.py` | 516 | 2026-09-23 | Fits Higgsfield golf GLBs (blender/characters/golf) onto V4 golf rig | run before adnan_golfer_export (optional) | only the body inside the fallback V4 golfer; matchhero_golf ignores it | **SAFE** | PIPE-12 |
| `avatar_base.py` | 230 | 2026-10-04 | Icon-avatar kit: base body/head numbers | imported by other avatar_* scripts | outputs retired 10-04 (Unity/Assets/Archive/IconKit) | **DECIDE** | PIPE-13 |
| `avatar_catalog.py` | 42 | 2026-10-04 | Icon-avatar kit: style catalog/id order | avatar_export/mock | retired | **DECIDE** | PIPE-13 |
| `avatar_export.py` | 196 | 2026-10-04 | Icon-avatar kit: writes Resources/Hero/hero_*.fbx parts | manual Blender run | hero_*.fbx deleted 2026-10-05 (7c790cf6); copies in Archive/IconKit | **DECIDE** | PIPE-13 |
| `avatar_head_parts.py` | 308 | 2026-10-04 | Icon-avatar kit: haircuts/hats/glasses/beards | avatar_styles/catalog | retired | **DECIDE** | PIPE-13 |
| `avatar_icons.py` | 23 | 2026-10-04 | Icon-avatar kit: locker tile PNGs -> UI/Look | manual | UI/Look tiles replaced by matchhero_icons output (10-05/06) | **DECIDE** | PIPE-13 |
| `avatar_mock.py` | 140 | 2026-10-04 | Icon-avatar kit: turnaround/closet renders | manual | retired | **DECIDE** | PIPE-13 |
| `avatar_parts.py` | 114 | 2026-10-04 | Icon-avatar kit: parts registry | avatar_* scripts | retired | **DECIDE** | PIPE-13 |
| `avatar_styles.py` | 261 | 2026-10-04 | Icon-avatar kit: more head styles | avatar_catalog | retired | **DECIDE** | PIPE-13 |
| `avatar_wardrobe.py` | 114 | 2026-10-04 | Icon-avatar kit: more tops/bottoms | avatar_catalog | retired | **DECIDE** | PIPE-13 |
| `hero_avatar.py` | 542 | 2026-10-04 | Scanned Hero01 rebuilt as clean low-poly (renders) | none | superseded (docs/hero-golfer.md) | **SAFE** | PIPE-14 |
| `hero_face.py` | 115 | 2026-10-04 | Painted face decal for hero_avatar | none | superseded | **SAFE** | PIPE-14 |
| `hero_golf_retarget.py` | 403 | 2026-09-29 | Retargeted V4 golf clips onto Hero01 rig -> Hero/hero_golf.fbx | none | hero_golf.fbx deleted 2026-10-05 | **SAFE** | PIPE-14 |
| `hero_hair.py` | 538 | 2026-10-04 | Hero01 haircuts | hero_parts_export | deleted outputs | **SAFE** | PIPE-14 |
| `hero_hats.py` | 375 | 2026-10-04 | Hero01 headwear | hero_parts_export | deleted outputs | **SAFE** | PIPE-14 |
| `hero_head.py` | 377 | 2026-10-04 | Closed Hero01 head | hero_parts_export; comment in HeroKit.shader | deleted outputs | **SAFE** | PIPE-14 |
| `hero_parts_export.py` | 138 | 2026-10-04 | Hero01 parts -> Resources/Hero/hero_*.fbx | none | hero_*.fbx deleted 2026-10-05 | **SAFE** | PIPE-14 |
| `hero_repair.py` | 185 | 2026-10-04 | Repairs on Hero01 meshes | hero_parts_export; comment in HeroKit.shader | deleted outputs | **SAFE** | PIPE-14 |
| `export_tennis_runtime.py` | 106 | 2026-09-22 | Exports hidden gameplay rig standard_*_tennis.fbx + writes blender/tennis-clips.json (and, without --tennis-only, the dead CoastalTennisResort.fbx) | manual; imports tennis_clips | standard_*_tennis.fbx LIVE (TennisActor.Build loads it every match) | **KEEP** | PIPE-15 |
| `tennis_clips.py` | 226 | 2026-09-23 | Derives clip splits from studio NLA strips/markers | imported by export_tennis_runtime | writes blender/tennis-clips.json (read on every import by Editor/GolferModelImporter.cs) | **KEEP** | PIPE-15 |
| `prepare_island.py` | 127 | 2026-09-23 | Fits Higgsfield island GLB to resort -> Tennis/Island/TennisIsland.fbx + Island_* maps | manual; reads SportsLibrary .../models/island.glb | LIVE (Resort venue: TennisGame.cs:239) | **DECIDE** | PIPE-16 |
| `prepare_umpire.py` | 100 | 2026-09-23 | Rigs chair-umpire GLB -> Tennis/Umpire/TennisUmpire.fbx + HiggsUmpire maps | manual; reads SportsLibrary .../models/umpire.glb | LIVE (TennisUmpire.Spawn every match) | **DECIDE** | PIPE-16 |
| `author_tennis_clips.py` | 95 | 2026-09-22 | Converted leftover V3 tennis actions into the studio V4 track | edits studio blend; none call it | baked into studio/standard_*_tennis.fbx (hidden rig) | **SAFE** | PIPE-17 |
| `author_tennis_faces.py` | 193 | 2026-09-23 | Added 'V4 face decal' + hair to V4 tennis rigs in the studio | edits studio blend | decal is on the hidden gameplay rig only | **SAFE** | PIPE-17 |
| `author_tennis_motion.py` | 386 | 2026-09-24 | Re-authored V4 tennis clips from key poses (uses tennis_motion, fit_avatar) | none; comment in Editor/ClipContactAudit.cs | baked into hidden rig clips | **SAFE** | PIPE-17 |
| `bake_kit_mask.py` | 135 | 2026-09-24 | Bakes Higgs*_Mask.png/json recolour masks for Avatar/Male/Female bodies | none; comment in TennisLook.cs | masks still loaded by TennisLook.RecolorKit for the hidden rig/crowd; frozen, no rebuild needed | **SAFE** | PIPE-17 |
| `build_avatar_face_atlas.py` | 152 | 2026-09-24 | Draws FaceAtlas_Avatar(F).png | none | Avatar bodies no longer loaded | **SAFE** | PIPE-17 |
| `build_face_atlas.py` | 85 | 2026-09-23 | Assembles FaceAtlas.png from SportsLibrary face-parts | none; comment in TennisActor.cs | FaceAtlas still loaded for the hidden rig's decal; frozen | **SAFE** | PIPE-17 |
| `build_v5_rig.py` | 86 | 2026-09-24 | Re-proportioned V4 tennis rigs (V5) in the studio | none | baked into hidden rig | **SAFE** | PIPE-17 |
| `build_video_character.py` | 362 | 2026-09-24 | Concept-video toy character on V5 rigs (Higgs*_Color/Normal) | imports fit_avatar, fit_opponents | not used (MatchHeroes are the visible heroes) | **SAFE** | PIPE-17 |
| `check_character_anatomy.py` | 38 | 2026-09-24 | Rejects Tripo meshes with wrong limb count | used by fit_higgs_characters/measure_landmarks | n/a (tool) | **SAFE** | PIPE-17 |
| `coherent_avatar_body.py` | 155 | 2026-09-24 | Rebuilt avatar torso/arms as one surface | imported by fit_avatar, render_clip_sheets | dead | **SAFE** | PIPE-17 |
| `fit_avatar.py` | 313 | 2026-09-24 | Fits base avatar onto male tennis rig -> Opponents/Avatar(F).fbx | imported by 6 chain scripts; comment in TennisGame.cs | Avatar bodies not loaded | **SAFE** | PIPE-17 |
| `fit_higgs_characters.py` | 529 | 2026-09-24 | Fits Higgsfield tennis players onto V4 rig (V4 Higgs body) | imports real_arms, check_character_anatomy | body is inside the hidden rig FBX (invisible) | **SAFE** | PIPE-17 |
| `fit_opponents.py` | 99 | 2026-09-24 | Fits campaign opponents -> Resources/Tennis/Opponents/*.fbx | none; comment in GolferModelImporter.cs | Opponents NOT loaded (TennisActor.WearBody returns false) | **SAFE** | PIPE-17 |
| `fix_tennis_racket_and_backhand.py` | 174 | 2026-09-22 | Fixed racket keys/backhand in studio V4 track | none; comment in TennisActor.cs | baked into hidden rig | **SAFE** | PIPE-17 |
| `install_tripo_kit.py` | 217 | 2026-09-24 | Dressed V5 tennis players in the Tripo kit | none | dead | **SAFE** | PIPE-17 |
| `kit_compare.py` | 151 | 2026-09-24 | Tripo vs Higgsfield outfit comparison renders | imported by install_tripo_kit | n/a | **SAFE** | PIPE-17 |
| `make_clip_sheets.py` | 27 | 2026-09-24 | Contact sheets from render_clip_sheets frames | none | review only | **SAFE** | PIPE-17 |
| `measure_landmarks.py` | 100 | 2026-09-24 | Measures Tripo A-pose landmarks for fit_opponents | fit_opponents | dead | **SAFE** | PIPE-17 |
| `prepare_avatar.py` | 39 | 2026-09-24 | Decimates raw Tripo avatar GLB | fit_avatar | dead | **SAFE** | PIPE-17 |
| `real_arms.py` | 137 | 2026-09-24 | Replacement arms for fitted avatar bodies | fit_avatar, coherent_avatar_body, fit_higgs_characters | dead | **SAFE** | PIPE-17 |
| `reference_spec.py` | 91 | 2026-09-24 | Measured concept-video character spec (character-spec.json) | none | feeds build_video_character (dead) | **SAFE** | PIPE-17 |
| `render_clip_sheets.py` | 83 | 2026-09-24 | Renders tennis clip review frames | none | review only | **SAFE** | PIPE-17 |
| `retarget_emotes.py` | 77 | 2026-09-24 | Retargeted Meshy emotes onto V4 tennis rigs | none; comment in TennisActor.cs | hidden rig only; visible emotes are match-hero clips | **SAFE** | PIPE-17 |
| `retarget_intro_emotes.py` | 228 | 2026-09-24 | Retargeted intro emotes onto V4 rigs | imported by 5 chain scripts | hidden rig only | **SAFE** | PIPE-17 |
| `retarget_video_pose.py` | 505 | 2026-09-24 | Retargeted MediaPipe/SAM video poses onto V4 rig | none; comment in ClipContactAudit.cs | hidden rig clips | **SAFE** | PIPE-17 |
| `rotomate_fuse.py` | 143 | 2026-09-24 | Rotomation step 3b | none | dead | **SAFE** | PIPE-17 |
| `rotomate_sam.py` | 53 | 2026-09-24 | Rotomation step 3a (SAM dump) | none | dead | **SAFE** | PIPE-17 |
| `rotomate_sheets.py` | 45 | 2026-09-24 | Rotomation step 2 (sheets) | none | dead | **SAFE** | PIPE-17 |
| `rotomate_track.py` | 69 | 2026-09-24 | Rotomation step 1 (2D targets from serve-side.mp4) | none | dead | **SAFE** | PIPE-17 |
| `sam_tiles_to_poses.py` | 78 | 2026-09-24 | SAM tile results -> MediaPipe-layout joints | none | dead | **SAFE** | PIPE-17 |
| `settle_clips.py` | 168 | 2026-09-24 | Smoothing/hip fixes after video retarget | none | hidden rig clips | **SAFE** | PIPE-17 |
| `soften_knees.py` | 200 | 2026-09-24 | Reduced knee squat in V4 clips | none | hidden rig clips | **SAFE** | PIPE-17 |
| `tennis_motion.py` | 362 | 2026-09-24 | Key-pose clip description language | author_tennis_motion | hidden rig clips | **SAFE** | PIPE-17 |
| `tile_video_frames.py` | 33 | 2026-09-24 | Tiles video frames for SAM body fitting | none | dead | **SAFE** | PIPE-17 |
| `track_video_racket.py` | 71 | 2026-09-24 | Tracks racket colour in video frames | none | dead | **SAFE** | PIPE-17 |
| `build_crowd.py` | 111 | 2026-09-23 | Seated tennis crowd FBX (Resources/Tennis/Crowd) + blender/crowd-clips.json | none; Editor/TennisCrowdImporter.cs reads its json | Tennis/Crowd/*.fbx NOT loaded (crowd is Tennis/Premium/TennisSeatedHero3) | **SAFE+FIX** | PIPE-18 |
| `build_sportswear.py` | 197 | 2026-09-28 | Sportswear study at 3 sizes for the supplied player bases | imported by export_player_bases.py | dead (player bases not loaded) | **SAFE** | PIPE-19 |
| `export_customization.py` | 78 | 2026-09-28 | Fitted Tripo hair/head -> Tennis/Customization/Custom*.json + GolfArcade CharacterAssets | manual | not loaded by Unity runtime or Swift | **SAFE** | PIPE-19 |
| `export_player_bases.py` | 159 | 2026-09-28 | Binds supplied player-base GLBs to the sports rig -> Player*.json/png + rigged blends | manual; imports build_sportswear | not loaded (TennisCustomization skips; Swift loads only MatchHero_*) | **SAFE** | PIPE-19 |
| `fit_base_hair.py` | 46 | 2026-09-28 | Fitted hair caps for supplied bases -> Player*{Swept,Curls,Bob}.json | manual | not loaded | **SAFE** | PIPE-19 |
| `fit_golf_bases.py` | 38 | 2026-09-28 | Supplied bases on golf rig -> Tennis/Customization/Player*Golf.fbx | manual | not loaded (AttachBase golf path never called) | **SAFE** | PIPE-19 |
| `render_sportswear_study.py` | 27 | 2026-09-28 | Renders the sportswear study | none | review only | **SAFE** | PIPE-19 |
| `export_arm_validation.py` | 22 | 2026-09-20 | Exports V4 pose fixtures to Unity/Assets/Tests/Fixtures/StandardCharacters | manual; reads SportsLibrary action-manifest + studio | fixtures used only by EditMode StandardArmSportTests | **DECIDE** | PIPE-20 |
| `export_standard_golf.py` | 66 | 2026-09-20 | Exports StandardCharacters/standard_*_golf.fbx from the studio | manual | used only by EditMode StandardGolfGripTests + importer | **DECIDE** | PIPE-20 |
| `__pycache__/` (17 .pyc, gitignored, 2.9 MB) | – | untracked | Python bytecode cache | – | no | **SAFE** | PIPE-23 |

## Verdict totals

Counted by finding. Sizes are bytes on disk in decimal MB; "ignored" means gitignored local files that are not in git.

| Verdict | Findings | Tracked files | Tracked MB | Ignored files | Ignored MB |
|---|---|---|---|---|---|
| KEEP | 7 (PIPE-01, 02, 03, 04, 08, 15, 34) | 86 | 387.5 | 0 | 0 |
| DECIDE | 11 (PIPE-05, 09, 10, 11, 13, 16, 20, 24, 31, 37, 42) | 66 | 451.1 | 0 | 0 |
| SAFE+FIX | 2 (PIPE-18, 26) | 10 | 12.1 | 0 | 0 |
| SAFE | 22 (PIPE-06, 07, 12, 14, 17, 19, 21, 22, 23, 25, 27, 28, 29, 30, 32, 33, 35, 36, 38, 39, 40, 44) | 808 | 1,712.9 | 41 | 933.1 |
| n/a, docs auditor | 1 (PIPE-41, SportsLibrary *.md) | 54 | 0.2 | 0 | 0 |
| info only | 1 (PIPE-43, absent gitignored dirs) | 0 | 0 | 0 | 0 |
| **Total** | 44 | **1,024** (801 SportsLibrary + 197 blender + 26 Tools/HeroBase) | **2,563.7** | 51 | 933.3 |

The same totals split by area:

| Verdict | Area | Tracked files | Tracked MB | Ignored files | Ignored MB |
|---|---|---|---|---|---|
| KEEP | blender | 84 | 21.8 | 0 | 0 |
| KEEP | SportsLibrary | 2 | 365.7 | 0 | 0 |
| DECIDE | blender | 19 | 0.2 | 0 | 0 |
| DECIDE | SportsLibrary | 21 | 450.7 | 0 | 0 |
| DECIDE | Tools/HeroBase | 26 | 0.2 | 0 | 0 |
| SAFE+FIX | blender | 2 | 0.0 | 0 | 0 |
| SAFE+FIX | SportsLibrary | 8 | 12.1 | 0 | 0 |
| SAFE | blender | 92 | 26.1 | 20 | 8.2 |
| SAFE | SportsLibrary | 716 | 1,686.8 | 10 | 924.7 |
| SAFE | Tools/HeroBase (pycache) | 0 | 0 | 11 | 0.2 |
| n/a (docs) | SportsLibrary | 54 | 0.2 | 0 | 0 |

Per-script verdicts in `blender/scripts` (140 tracked files): 66 KEEP, 19 DECIDE, 1 SAFE+FIX, 54 SAFE.

## Cross-area links (for the other auditors)

### Scripts that write into Unity or Swift, and whether that output is live

**Live outputs:**
- `export_tennis_runtime.py` + `tennis_clips.py` + `SportsLibrary/Blender/sports-animation-studio-v4.blend` → `Unity/Assets/Resources/StandardCharacters/standard_{male,female}_tennis.fbx`. This is the hidden gameplay rig `TennisActor.Build` loads every match. They also write `blender/tennis-clips.json`, which `Editor/GolferModelImporter.cs` reads on import.
- `matchhero_golf.py` (+ `golf_swing`, `matchhero_hair`/`mhhair`, `avatar_kit`) → `Resources/Hero/golfer_{m,f}.fbx`, `_clips.json` and `Hero/Hair/*.png`. `matchhero_icons.py` → `Resources/UI/Look/hair_*.png` and `tab_outfit.png`.
- `golf_clubs.py`, via `work/full-visual-overhaul/characters/export_native_equipment.py` (untracked) → `GolfArcade/Unity/CharacterAssets/NativeGolfEquipment_*.json` and `Unity/Assets/Resources/Golf/Equipment/*`. `HeroGolfer.InstallEquipment` swaps these clubs in at runtime.
- `course_builder.py` → `Resources/Course/hole_13..23.fbx` and `hole_NN_card.jpg`.
- `hole08/09/10_build.py` → `hole_08/09/10.fbx`; `golf_look_textures.py` → `Course/Look/*`.
- `golf_course_standard_*.py` → `Course/Standard/*`.
- `pin_build.py` → `Course/pin.fbx`.
- `backdrop_import.py` → `Course/Backdrop/*`.
- `build_reference_hole09.py` → `Course/Resort/ReferenceHole09.fbx`, which is live and replaces hole 9's model by default. `ReferenceHole12.fbx` comes from untracked `work/full-visual-overhaul/coastal12-reference/build-reference-hole12.py`.
- `hole12_*.py` → `hole_12.fbx`, `hole_12_cinematic.fbx`, `hole_12_shot.json` and `Course/swing_trail.json`.
- `prepare_umpire.py` → `Tennis/Umpire/TennisUmpire.fbx` and `Tennis/Characters/HiggsUmpire_*`; `prepare_island.py` → `Tennis/Island/*`.
- `SportsLibrary/Arenas/Tennis/tropical-tennis-resort-v3.blend` → `Tennis/TropicalV3/TropicalTennisResort.fbx`, via `export_tropical_unity.py` **outside the repo** (`../tennis-tropical-rebuild-v3/Tools/`).
- `SportsLibrary/Equipment/Tennis/Tripo-v3` → `Tennis/KitsV3/racket.fbx` and `RacketGameplay`, used on the hidden actor. The external `build_runtime_tennis_kits.py` made them.

**Test-only outputs:**
- `export_standard_golf.py` → `StandardCharacters/standard_*_golf.fbx`, used only by `StandardGolfGripTests`.
- `export_arm_validation.py` → `Unity/Assets/Tests/Fixtures/StandardCharacters/*`, used only by `StandardArmSportTests`.

**Dead outputs, with no runtime reader:**
- `Tennis/CoastalTennisResort.fbx` (export_tennis_runtime, arena half).
- `Tennis/Crowd/*.fbx` (build_crowd); `Editor/TennisCrowdImporter.cs` still reads `blender/crowd-clips.json`.
- `Tennis/Opponents/*.fbx` and `Tennis/Characters/Higgs<Rival>_*` and `FaceAtlas_Avatar*` (fit_opponents, fit_avatar, build_avatar_face_atlas).
- `Tennis/Customization/{Custom*, Player*{Bob,Curls,Swept,Color,Mask}*, Player*Golf.fbx, HiggsAvatar*}` and `GolfArcade/Unity/CharacterAssets/{Custom*, PlayerMale*, PlayerFemale*, HiggsAvatar*}` (export_customization, export_player_bases, fit_base_hair, fit_golf_bases).
- `Tennis/KitsV3/standard_*_kit.fbx` (from `SportsLibrary/Clothing/Tennis/Tripo-v3`).
- `Course/Turf/*.png`: still live, but no longer made by `turf_textures.py`.
- `Golfer/crowd_{m,f}.fbx`: no producer in `blender/scripts` and no runtime reader.

**Other things that look dead but are not:**
- **Do not delete `Unity/Assets/Resources/Golfer/golfer_m.fbx` + `golfer_m_clips.json` without moving them.** `matchhero_golf.py` reads them by default (`--v4`) for the clubs and the Idle/Wave/Cheer/FistPump motion. At runtime they are only a fallback, plus the V4 comparison PlayMode test.
- Hidden-rig textures `Tennis/Characters/Higgs{Male,Female}_*` and `FaceAtlas.png` are still loaded by `TennisLook.PrepareCharacter` and `RecolorKit` on the hidden actor, so they are frozen, not dead, even though their scripts are SAFE.

### Files the .blend sources depend on

These were found by decompressing each `.blend` and reading its stored paths:
- `blender/hole_08/09/10.blend` → `/Users/adnanyonathan/.../Unity/Assets/Resources/Course/Look/*.png` (absolute paths).
- `hole_07.blend` and `hole_12.blend` store Andrew's paths.
- `SportsLibrary/Clothing/Tennis/Tripo-v3/standard-*-fitted-kit-v3.blend` → `Unity/Assets/Resources/Tennis/KitsV3/Textures/*` (relative).
- `SportsLibrary/Customization/Bases/Player*-rigged.blend` → `Unity/Assets/Resources/Tennis/Customization/Player*Color.png`.
- `sports-animation-studio-v4.blend` and the crowd and arena blends → sibling folders outside the repo (`../sports-character-library`, `../sports-venue-concepts`, `../familiar-crowds-v1`, `../tennis-tropical-rebuild-v3`, and others). These are reference images and render paths, not needed to open the files.

### Tools and docs that depend on these areas
- `Tools/PostcardCheck/{gates_diff.py, equiv.py}` imports `blender/scripts/postcard_check.py` and `hole0x_design.py`.
- `Tools/HeroBase/build_static.py` made `Unity/Assets/Characters/HeroBase/Models/Hair_Default_{Male,Female}.fbx`. `fetch_matchheroes.sh` pulls those (from branch `newmapsandmenus`), and they become the golfer's "Classic" hair.
- Per the docs auditor's note, the live tennis MatchHeroes are built by Unity editor tools from untracked `work/match-anim-set/export/fbx`, `work/cloth-overhaul/export`, `work/hero-mainstay/data/clip_table.json` and `work/classic-tennis-racket/…`. **Nothing in SportsLibrary or `blender/scripts` is on that path.**
- The root `README.md` links into SportsLibrary docs. `docs/tennis-motion-reference.md` cites the concept video. `docs/avatar-kit.md`, `docs/hero-golfer.md` and `docs/matchhero-golfer.md` describe the PIPE-13, PIPE-14 and PIPE-08 status.
- CI (`.github/workflows`), `project.yml`, `project-unity.yml` and both Xcode projects reference none of SportsLibrary, `blender/` or `Tools/HeroBase`.

## Method notes
- **Inventory:** `git ls-files` and `git status --ignored` on the three trees. "Last commit" is the newest non-merge commit touching the file, with the LFS-conversion commit `7ecf8100` skipped for content dates.
- **Searches:**
  - Code: `Unity/Assets/{Scripts,Editor,Tests}` (C#), `GolfArcade*` (Swift), `blender/scripts`, `Tools`, `docs`, root md/yml, `.github`, Xcode projects.
  - `work/`: only `*.{py,sh,cs,js,md,json,txt}` under 2 MB, with Unity clones and script copies (`/Assets*/`, `ctrl_repo`, `libs_r*`) excluded.
- **Blend paths:** listed by streaming zstd decompression in Python 3.14 (`compression.zstd`). Nothing was written.
- **Not done:** no Blender runs and no LFS fetch. Untracked work/ jobs were looked at only for whether they reference these trees; the work/ auditor covers them.
