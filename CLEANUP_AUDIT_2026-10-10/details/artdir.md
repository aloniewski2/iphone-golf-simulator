# ArtDir audit: unused, obsolete and duplicate material (read-only)

Area: `ArtDir/**`. Audited 2026-10-10 against the working tree as found (branch `final-build`, HEAD `14a43331`). Nothing in the repo was changed.
Units are binary (1 MB = 1,048,576 bytes, 1 GB = 1,073,741,824 bytes). Scratch data (inventory, hashes, reference lists) is in `scratchpad/audit/artdir/`.

## Headline

- **Size.** ArtDir has 24,651 files on disk, 15.14 GB in total.
  - 24,553 files are tracked in git. 23,440 of them are Git LFS pointers (14.38 GB of content). The other 1,113 are ordinary git blobs, about 150 MB, mostly JSON plus some `.npz`/`.csv`.
  - 1 file is untracked: `hero/38_LATEST_HEROES_EVERYWHERE.txt`, an agent prompt.
  - 97 files are git-ignored, about 630 MB: Blender `.blend1` backups, `__pycache__`, and the `preview-raw` bakes.
  - Every LFS file has real content locally. There are no unfetched pointer stubs.
- **The game never loads ArtDir.**
  - No file in `GolfArcade/` (Swift app), `Unity/Assets` runtime scripts or `Resources`, `project.yml`, `project-unity.yml`, either `.pbxproj` or `.github/` reads a path in ArtDir. There are no symlinks into ArtDir either.
  - Runtime source mentions ArtDir only in comments:
    - `ClubDesign.swift` cites `ui/toybox-simple-v6`.
    - `CharacterModelPreview.swift` and `TennisLook.cs` cite `hero/v5_proof/LIGHTING.md`.
    - `HeroTennisDriver.cs` cites `hero/v5_tools/build_hair_v5.py` and `score80/probe/hair_winding.txt`.
    - `TennisEnvironmentV4.cs` cites `env_v4/tools/build_env_v4.py`.
    - `TennisCloth.shader` cites `GARMENT_SPEC.md`.
- **The 20 modified tracked files in ArtDir are test output.** `GolfArcadeTests/TennisMenuSnapshotTests.swift` rewrites them on every unit-test run:
  - `testEmoteLoadoutScreens` writes `screenshots/emote_loadout/*.png` (4 files).
  - `testApprovedIslandScreens` writes `ui/toybox-simple-v6/runtime/*.png` (16 of its 23 PNGs are modified).
- **Most of ArtDir is about a retired character.** About 83% (12.6 GB) is SAFE or SAFE+FIX. It is mostly Hero01 and its V4/V5/V6 versions: captures, frame dumps, review films and score80 runs. `Unity/Assets/Editor/LegacyHero01.cs` says the old Hero01 prefab "is no longer referenced by any runtime code (TennisHeroSetup loads the match heroes)".

## Writers: tests and tools that put files into ArtDir

| Writer | ArtDir target | When it runs |
|---|---|---|
| `GolfArcadeTests/TennisMenuSnapshotTests.swift` `testEmoteLoadoutScreens` | `screenshots/emote_loadout/` | every `xcodebuild test` (modified now) |
| same, `testSportLockerPreviewSwitchesOutfitEquipmentAndBack` | `screenshots/golf_locker_swap/` | every run |
| same, `testApprovedIslandScreens` | `ui/toybox-simple-v6/runtime/` | every run (modified now) |
| same, `testRecordTVMenuTour` | `review/tv-menus/` | opt-in `RECORD_TV_MENU=1` |
| `GolfArcadeTests/MotionClubReviewTests.swift` (`recordMenus` ×3, `smoothTour` ×2) | `ui/motion-club-radial-v1/runtime/` | every run unless `MENU_MOTION_DIR` is set |
| `GolfArcadeTests/ClubEnvironmentTests.swift` (2 capture tests) | `environments/motion-club-3d/review-live-images/` | every run |
| `GolfArcadeTests/PostMatchSnapshotTests.swift` | `review/post-match/` | every run |
| `Unity/Assets/Tests/PlayMode/TennisAbilitiesPlayTests.cs` | `screenshots/tennis_abilities/` | every PlayMode run (not opt-in) |
| `Unity/Assets/Tests/PlayMode/TennisReceiveCameraTests.cs` | `review/receive-camera/` (3 PNG) | every PlayMode run (not opt-in) |
| `Unity/Assets/Tests/PlayMode/NativeLaunchHeroTests.cs` | `score80/native_launch_run.txt` | every PlayMode run. **It uses `File.WriteAllText` without creating the folder, so if `ArtDir/score80/` is deleted this current MatchHero test throws before its asserts.** |
| Score80Capture, Score80ClipProbe, Score80EnvAudit, Score80HairAB, Score80WardrobeAudit tests | `score80/**` | `[Explicit]` or env-gated |
| HairFitAuditTests | `hero/v5_proof/hairfit_unity/` | `[Explicit]` |
| HeroV6RetargetTests | `review/v6_retarget/` | `[Explicit]` |
| IntroCaptureTests | `review/intro*/` | `[Explicit]` |
| TennisVenueFilmTests | `review/venues/` | `Assume` env |
| TennisStanceCaptureTests | `screenshots/stance/<tag>` | `Assume` env |
| TennisMultiAngleCaptureTests | `anims/multi_angle_review/` | `[Explicit]` |
| TennisFullMatchCaptureTests, TennisSinglePointCaptureTests | `anims/captures/` | `[Explicit]` |
| Unity editor tools, legacy Hero01: HeroGameplayBuild, HeroLookReview, HeroOwnedReview, HeroLegacyReview, HeroGameplayFixCapture, HeroGameplayPlaybackReview, HeroKneesOffArmReview, HeroBackhandRunReview, HeroAnimRepairReview, HeroFullStrokeReview, HeroRestoreMotionReview, HeroIntegrityReview, HeroIntegrityAudit, HeroV6PolishImport, HeroV6PolishReview, HeroV6PolishBeforeReview, HeroLockerExport.RunMenuPreview | `screenshots/**` folders, `anims/gameplay_fixed`, `hero/integrity_v1`, `hero/v5_astra/baseline`, `hero/v6_proof`, `hero/v6_target/BEFORE`, `ui/toybox-simple-v6/source/preview-raw` | manual `-executeMethod` |
| Unity editor tools, current: HeroBaseStaticImport, HeroBaseMaleFormProof, HeroBaseFemaleHeadProof | `hero/base_lock/proof/` | manual |
| TennisVenueCapture | `screenshots/venues/` | manual |
| GolfFlyoverCapture | `screenshots/golf_flyover/frames` | manual |
| PostcardCameraStills, PostcardPlayVideo, PostcardLookStills | `screenshots/golf_postcards/`, `/frames`, `/look` | manual |
| RallyPopHudCapture | `ui/motion-club-gameplay-hud-v1/implemented` | manual |
| `Tools/HeroBase/*.py` | `hero/base_lock/proof/*`, `unity_import/Textures` | manual |
| `blender/scripts/golf_look_textures.py` (`--sheet`), `postcard_props_smoke.py` | `screenshots/golf_postcards/look/*_sheet.png` | manual |

## Readers: removing these inputs breaks something

- **`Unity/Assets/Editor/HeroBaseStaticImport.cs`** copies these files from `hero/base_lock/unity_import/` into `Unity/Assets/Characters/HeroBase`:
  - `HeroBase_{Male,Female}_Body.fbx`
  - `Hair_Default_*.fbx`
  - `Textures/Head_*_Albedo.png`
  - `HeroBase_*_Attachments.json`
- **`Unity/Assets/Editor/HeroBaseMaleFormProof.cs`** copies `unity_import/HeroBase_Male_Body.fbx` and compares its hash.
- **23 scripts in `Tools/HeroBase/*.py`** read from `hero/base_lock/`:
  - the plates `male_body_bald.jpg`, `male_head_detail.jpg` and `male_multiangle_body.jpg`
  - `plate-measurements.json`
  - `blender/HeroBase_Male_Silhouette.blend`
  - `unity_import/*`
  - files in `proof/`, for example `01_silhouette_overlay_m.png`, `MALE_SIL_RESULTS.md` and `male-form-*.json`
- **47 `work/` jobs** (HeroBase sculpt, clothing, racket and kit jobs) reference `hero/base_lock/`; 35 of them read the plates or the `blender/HeroBase_{Male,Female,Male_Silhouette}.blend` files directly. Many of them SHA-guard these files.
- **`blender/scripts/postcard_look_verify.py`** reads `screenshots/golf_postcards/look/hole*.png` (`STILLS_DIR`) and `environments/refs/{needle,split,crater}.jpg` (`REF_STILLS_DIR`).
- **Internal tools inside ArtDir:**
  - `ArtDir/tennis/venues/architecture/build-clubhouse.py` loads `ArtDir/env_v4/tools/build_env_v4.py`.
  - `hero/kit/premium/tailoring4/tools/author-tailored-source.py` reads `hero/kit/premium/source/Tennis_{Male,Female}_SleeveFit.blend`.
- **Legacy Hero01 editor tools** read ArtDir data:
  - `HeroGameplayBuild.Build` (also called by `HeroLockerExport.Run`) reads `anims/restore_motion/unity_gameplay_config.json`.
  - `HeroRestoreMotionReview` reads `anims/restore_motion/unity_review_config.json` and `unity_hand_ref.json`.
  - HeroAnimRepairReview, HeroBackhandRunReview, HeroFullStrokeReview, HeroGameplayPlaybackReview and HeroKneesOffArmReview read `screenshots/gameplay_fixed/bind_comparison.json`.
  - `HeroIntegrityAudit` reads `hero/integrity_v1/export/*.fbx`.
- **`NativeLaunchHeroTests`** needs the `ArtDir/score80/` directory to exist (see the table above).

## Entries

Each entry gives: paths and file count; what it is; size, whether tracked, LFS bytes now and in history, and the last commit that touched it; evidence; what depends on it; the verdict and the confidence.
Verdicts: **SAFE** (regenerable, or nobody uses it), **SAFE+FIX** (safe, but name a follow-up edit), **DECIDE** (history the owner may want to keep or archive outside the repo), **KEEP** (authority, or actively used).

### screenshots/

**ART-01: `screenshots/tennis_complete/`, 3,545 files**
- **What:** Hero01 "TennisComplete_CustomizeSafe" capture frames of the retired Hero01, from 2026-09-26/27:
  - `gameplay_baseline/`: 2,700 PNG, 2.2 GB
  - `gameplay/`: 340
  - `juice/`: 436
  - `shadowtest/`: 3
  - 66 stills
- **Size:** 2.6 GB. Tracked: yes. LFS: 2.6 GB now, 2.6 GB in history. Last commit: 2026-09-28.
- **Evidence:** written only by the legacy `Unity/Assets/Editor/HeroGameplayBuild.cs`. One still is linked from `ui/gameplay-mockups-v3/README.md` (ART-57). Nothing reads it.
- **Depends:** nothing. The legacy tool recreates the folder if it is re-run.
- **Verdict:** SAFE. This is the largest single win (17% of ArtDir). Confidence: high.

**ART-02: Hero01 Play-mode frame dumps, 8 folders, 7,942 files**
- **Paths:**

  | Folder | Files | Size |
  |---|---|---|
  | `screenshots/knees_offarm_playmode` | 1,081 | 117 MB |
  | `backhand_run_playmode` | 1,082 | 117 MB |
  | `anim_repair_playmode` | 1,086 | 116 MB |
  | `gameplay_fixed_playmode` | 1,081 | 115 MB |
  | `fullstroke_grip_playmode` | 1,088 | 115 MB |
  | `v4_legacy` | 1,081 | 112 MB |
  | `restore_motion_playmode` | 902 | 98 MB |
  | `owned` | 541 | 72 MB |

- **What:** frame-by-frame review dumps of the Hero01 animation phases, from 2026-09-26.
- **Size:** 860 MB. Tracked: yes. LFS: 860 MB now, 763 MB in history. Last commit: 2026-09-28. 111 MB of the frames are byte-identical static frames.
- **Evidence:** the writers are the legacy review tools (HeroKneesOffArmReview, HeroBackhandRunReview, HeroAnimRepairReview, HeroGameplayPlaybackReview, HeroFullStrokeReview, HeroLegacyReview, HeroRestoreMotionReview, HeroOwnedReview). Only ArtDir notes mention them.
- **Depends:** nothing.
- **Verdict:** SAFE. Confidence: high.

**ART-03: `screenshots/gameplay_fixed/`, 1,082 files**
- **What:** the same kind of frame dump (1,080 PNG), plus `bind_comparison.json` and `capture_report.txt`.
- **Size:** 115 MB. Tracked: yes. LFS: 115 MB now, 82 MB in history. Last commit: 2026-09-28.
- **Evidence:** `bind_comparison.json` is read by 5 legacy editor tools: HeroAnimRepairReview, HeroBackhandRunReview, HeroFullStrokeReview, HeroGameplayPlaybackReview and HeroKneesOffArmReview.
- **Depends:** those 5 tools stop working without the JSON.
- **Verdict:** SAFE+FIX. Delete those legacy tools in the same change, or keep `bind_comparison.json`. Confidence: high.

**ART-04: `screenshots/` root files, 37 files**
- **What:** Hero01 V1→V5 comparisons against plate 01 (`unity_hero_vs_plate01*_v*.png`), polish and measurement stills, `unity_owned_*.png`, and `unity_import.json` / `unity_playmode.json`, from 2026-09-26.
- **Size:** 11.5 MB. Tracked: yes. LFS: 11 MB. Last commit: 2026-09-28.
- **Evidence:** written by HeroLookReview and HeroOwnedReview. `docs/character-handoff/README.md` links `unity_hero_vs_plate01_v4.png`.
- **Verdict:** SAFE+FIX. Fix that doc link. Confidence: high.

**ART-05: `screenshots/plan*` folders, 296 files**
- **Paths:**

  | Folder | Files |
  |---|---|
  | `plan1_fluidity` | 18 |
  | `plan1b_serve_whiff` | 185 |
  | `plan2_fun_feel` | 10 |
  | `plan2b_spectacle` | 17 |
  | `plan2c_camera_ux` | 34 |
  | `plan2d_env_postcard` | 25 |
  | `plan3a_fluidity` | 6 |
  | `plan3b_hair_customize` | 1 |

- **What:** proof stills plus `00_notes.md` for the Hero01-era tennis plans PLAN1 through PLAN3B, from 2026-09-26/27.
- **Size:** 159 MB. Tracked: yes. LFS: 159 MB now, 157 MB in history. Last commit: 2026-09-28.
- **Evidence:** written by `HeroGameplayBuild` (P1, P1B, P2, P2B/2C). Linked from PLAN1_TennisFluidity_GroundHitBlends.md, PLAN1B_ServeRitual_Whiff_PointFlow.md, PLAN2_TennisFunFeel_JuiceAndFairness.md and PLAN2B_CinematicSpectacle_Ultimate.md.
- **Verdict:** SAFE+FIX. The PLAN docs lose their evidence links; the docs auditor decides whether those plans stay. Confidence: high.

**ART-06: `screenshots/golf_postcards/frames/`, 2,148 JPG**
- **What:** raw frames used to encode `postcards_gameplay.mp4`, from 2026-10-03.
- **Size:** 91 MB. Tracked: yes. LFS: 91 MB now, 82 MB in history. Last commit: 2026-10-06.
- **Evidence:** this is the default output of `PostcardPlayVideo.cs`. No readers.
- **Verdict:** SAFE. Confidence: high.

**ART-07: `screenshots/golf_postcards/` stills, mp4 and `look/`, 31 files**
- **What:** hole 8/9/10 postcard stills, `postcards_gameplay.mp4`, and `look/` (Game-view proof stills plus `stills_stats.json`, from 2026-10-05).
- **Size:** 67 MB. Tracked: yes. LFS: 67 MB. Last commit: 2026-10-06.
- **Evidence:**
  - `blender/scripts/postcard_look_verify.py` reads `look/hole*_*.png`.
  - Written by PostcardCameraStills, PostcardLookStills, `golf_look_textures.py --sheet` and `postcard_props_smoke`.
  - Cited by the briefs `environments/14`, `14b` and `15`.
- **Depends:** the verifier needs the `look/` stills.
- **Verdict:** SAFE+FIX. Re-capture with PostcardLookStills before re-running the verifier, or point `STILLS_DIR` somewhere else. Confidence: medium.

**ART-08: `screenshots/golf_flyover/`, 1 mp4**
- **What:** a golf flyover film, from 2026-10-03.
- **Size:** 99 MB. Tracked: yes. LFS: 99 MB. Last commit: 2026-10-06.
- **Evidence:** `GolfFlyoverCapture.cs` writes its `frames/` here by default. No readers or links.
- **Verdict:** SAFE. Confidence: high.

**ART-09: `screenshots/golf_course_standard/`, 275 files**
- **What:** golf course captures in `before/`, `after/` and `tree_repair/`, comparison JPGs, 4 mp4s, test XMLs, `course-metrics.csv` and `GATE_RESULTS.md`, from 2026-10-06.
- **Size:** 343 MB. Tracked: yes. LFS: 338 MB now, 319 MB in history. Last commit: 2026-10-06. 19 MB is internal duplicates.
- **Evidence:** written by `work/golf-course-standard`. Linked from `environments/GOLF_ALL_COURSES_POSTCARD_STANDARD_PLAN.md`.
- **Verdict:** SAFE+FIX. Keep `GATE_RESULTS.md` and the comparisons if the plan's evidence matters, or update the plan. Confidence: medium.

**ART-10: `screenshots/online_lobby/`, 52 PNG**
- **What:** phone and TV shots of the Island online lobby, about 8.7 MB each, from 2026-10-05.
- **Size:** 454 MB. Tracked: yes. LFS: 454 MB now, 452 MB in history. Last commit: 2026-10-05.
- **Evidence:** proof for `work/online-lobby/GATE_RESULTS.md` and prompt `27_ONLINE_LOBBY_UI.txt`. No code reads it.
- **Verdict:** SAFE. These can be re-captured. Confidence: high.

**ART-11: `screenshots/cloth_overhaul/`, 81 files**
- **What:** cloth overhaul proof: 53 PNG sheets and 28 mp4s, from 2026-10-05.
- **Size:** 104 MB. Tracked: yes. LFS: 104 MB. Last commit: 2026-10-06.
- **Evidence:** written by `work/cloth-overhaul`. Cited by `hero/kit/COPY_TO_GOLF.md` and prompts 29 and 32.
- **Verdict:** SAFE+FIX. Update `COPY_TO_GOLF.md`. Confidence: medium.

**ART-12: `screenshots/venues/`, 33 PNG**
- **What:** tennis venue captures, from 2026-09-29.
- **Size:** 59 MB. Tracked: yes. LFS: 59 MB now, 71 MB in history. Last commit: 2026-10-04.
- **Evidence:** default output of `Unity/Assets/Editor/TennisVenueCapture.cs` (`HERO_VENUE_OUT` overrides it). Cited by `environments/12_VENUE_PRESENCE.txt` and `14_POSTCARD_LOOK.txt`.
- **Verdict:** SAFE+FIX. The tool recreates the folder; change its default output if you don't want it in the repo. Confidence: high.

**ART-13: `screenshots/stance/` (90 PNG) and `screenshots/tennis_abilities/` (13 PNG)**
- **What:** Unity PlayMode test captures.
- **Size:** 56 MB. Tracked: yes. LFS: 56 MB. Last commit: 2026-09-29.
- **Evidence:**
  - `TennisAbilitiesPlayTests` is not opt-in and rewrites `tennis_abilities` on every PlayMode run.
  - `TennisStanceCaptureTests` is opt-in.
  - `docs/tennis-ultimates-and-dive.md` links `tennis_abilities/`.
- **Verdict:** SAFE+FIX. Move the test output to a temp or ignored folder; otherwise it comes back and dirties git. Confidence: high.

**ART-14: `screenshots/emote_loadout/` (8 files) and `screenshots/golf_locker_swap/` (9 files)**
- **What:** output of `TennisMenuSnapshotTests` (4 PNGs are modified right now), plus hand-written `GATE_RESULTS.md`, `native-tests.txt` and Unity XMLs from 2026-10-05. `controller_intro_layout.png` and `controller_rally_layout.png` are byte-identical.
- **Size:** 19 MB. Tracked: yes. LFS: 19 MB. Last commit: 2026-10-06.
- **Verdict:** SAFE+FIX. Point the tests at `$TMPDIR` (the test header already says "Output: $TMPDIR/tennis-menu/") and move the GATE notes if you want them kept. Confidence: high.

**ART-15: `screenshots/character_ears_fix` (2 PNG) and `screenshots/body_clearance` (notes)**
- **What:** Hero01 notes and stills, from 2026-09-26/27.
- **Size:** 1 MB. Tracked: yes. No references. LFS: 1018.5 KB. Last commit: 2026-09-28.
- **Verdict:** SAFE. Confidence: high.

### hero/: current HeroBase authority

**ART-16: `hero/base_lock/` root files and `hero/HERO.md`, 15 files**
- **What:** the LOCKED plates and their index:
  - `male_body_plain.jpg` and `female_body_plain.jpg`, each with a byte-identical `*_body_bald.jpg` twin
  - `*_head_detail.jpg` and `*_multiangle_body.jpg`
  - `plate-measurements.json`
  - REF_INDEX, ACCEPTANCE, METRICS, SCALE and REFERENCE_CHANGE docs
- **Size:** 1 MB. Tracked: yes. LFS: 1 MB. Last commit: 2026-10-04.
- **Evidence:** named in AGENTS.md, art-bible.md and CLAUDE_KICKOFF.md. Read by 23 `Tools/HeroBase/*.py` scripts and by 35 `work/` jobs (47 reference base_lock in some way).
- **Verdict:** KEEP. The plain/bald duplicates are intentional, because scripts use both names. Confidence: high.

**ART-17: `hero/base_lock/archive/with_hair_20260930/`, 7 files**
- **What:** the earlier hair-on plates. AGENTS.md names this archive.
- **Size:** 0.3 MB. Tracked: yes. LFS: 267 KB. Last commit: 2026-10-04.
- **Verdict:** KEEP. Confidence: high.

**ART-18: `hero/base_lock/blender/`, 3 blends**
- **What:** `HeroBase_Male.blend`, `HeroBase_Female.blend` and `HeroBase_Male_Silhouette.blend`. The 3 `.blend1` backups are counted in ART-72.
- **Size:** 7.4 MB. Tracked: yes. LFS: yes. Last commit: 2026-10-04.
- **Evidence:** the "authority blend" in AGENTS.md and PROCESS_Blender_Claude.md. Used by Tools/HeroBase and referenced by 22 `work/` jobs, many of which SHA-guard it.
- **Verdict:** KEEP. Confidence: high.

**ART-19: `hero/base_lock/unity_import/`, 8 files**
- **What:** body and hair FBX, head albedo textures and attachment JSON. They are byte-identical to `Unity/Assets/Characters/HeroBase/*`.
- **Size:** 3.1 MB. Tracked: yes. LFS: 3.1 MB. Last commit: 2026-10-04.
- **Evidence:** read by HeroBaseStaticImport and HeroBaseMaleFormProof; written by `Tools/HeroBase/build_albedo.py` and the export scripts.
- **Verdict:** KEEP. Confidence: high.

**ART-20: `hero/base_lock/face_lock/` and `face_lock_f/`, 57 files**
- **What:** male and female face authority packs: crops, sheets, FACE_SPEC and PROCESS docs, paste prompts.
- **Size:** 9 MB. Tracked: yes. LFS: 9.2 MB. Last commit: 2026-10-04.
- **Evidence:** REF_INDEX.md, GOAL_Micro1g/h/i and 7 `work/` head jobs.
- **Verdict:** KEEP. Confidence: high.

**ART-21: `hero/base_lock/proof/` (excluding `current_models_20261002`), 446 files**
- **What:** gate results (GATE_RESULTS.md, MALE_*/FEMALE_*_RESULTS.md, metrics JSON), overlays, Unity and Blender proofs, and `tennis-arm-swing-repair-2026-10-08`.
- **Size:** 179 MB. Tracked: yes. LFS: 178 MB now, 175 MB in history. Last commit: 2026-10-08.
- **Evidence:**
  - AGENTS.md requires GATE_RESULTS plus proof.
  - Tools/HeroBase reads and writes files here.
  - The HeroBase*Proof editor tools write here.
  - `Unity/Assets/Characters/HeroBase/README.md` links GATE_RESULTS.md.
- **Verdict:** KEEP. It could later be slimmed to the gate files plus the latest overlays. Confidence: high.

**ART-22: `hero/base_lock/proof/current_models_20261002/`, 220 files**
- **What:** an Oct 2 snapshot of 8-angle turnarounds for every staged HeroBase part.
- **Size:** 216 MB. Tracked: yes. LFS: 216 MB. Last commit: 2026-10-04.
- **Evidence:** no code refers to it. It was superseded once the MatchHeroes shipped on Oct 3.
- **Verdict:** DECIDE: archive it or keep it as history. Confidence: medium.

**ART-23: `hero/base_lock/preview/`, 9 files**
- **What:** `face_example` and `outfit_example`, the colored and clothed mock-up from 2026-10-01.
- **Size:** 18 MB. Tracked: yes. LFS: 17.7 MB. Last commit: 2026-10-04.
- **Evidence:** REF_INDEX says to use face_lock and "not preview/face_example/". Only `work/male-outfit-preview` reads it; GOAL_Micro1g/h cite it as the bad example.
- **Verdict:** DECIDE. Confidence: medium.

### hero/: current clothing and kit

**ART-24: `hero/clothing/tailored-2026-10-08/`, 26 files**
- **What:** authoring blends, proof and README for the accepted Oct 8 polo and skirt, which is the active clothing pass. The game ships the baked meshes in `Unity/Assets/Resources/Tennis/KitsTailored`.
- **Size:** 11.5 MB. Tracked: yes. Last commit: 2026-10-08. LFS: 11.5 MB.
- **Verdict:** KEEP. Confidence: high.

**ART-25: `hero/kit/` (excluding `premium/source`), 109 files**
- **What:**
  - GARMENT_SPEC.md, GARMENT_STANDARD.json, COPY_TO_GOLF.md, the selection template and `refs/`
  - `golf/GOLF_KIT_DESIGN.md` plus `golf/tailoring/`
  - `premium/tailoring4/`, the panel-mask authoring
- **Size:** 90 MB. Tracked: yes. LFS: 67 MB. 23 MB of `.npz` files are stored as plain git blobs. Last commit: 2026-10-08.
- **Evidence:**
  - The tailoring4 panel maps are byte-identical to the shipping `GolfArcade/Unity/CharacterAssets/MatchHero_Kit_*_Top_M.png` and to the Unity `HeroDetail` maps.
  - A `TennisCloth.shader` comment says it shares its numbers with GARMENT_SPEC.md.
  - Five `work/` jobs use this folder.
- **Verdict:** KEEP. Confidence: high.

**ART-26: `hero/kit/premium/source/`, 49 blends**
- **What:** an Oct 7 "full visual overhaul" chain of iterations:
  - `Premium_{Male,Female}_*` blends: ClosedFist V3/B/C, ContinuousHands V2/V3/C, GolfGloves, FistSurface V3 to V3H, FittedLids, ThumbRig, ThumbWeights, Studio, GolfHem, Equipment
  - `Tennis_*_{MatchDistance,SleeveFit}.blend`
  - The 16 `.blend1` backups (173 MB) are counted in ART-72.
- **Size:** 826 MB. Tracked: yes. LFS: 826 MB. Last commit: 2026-10-08.
- **Evidence:**
  - Only `Tennis_*_SleeveFit.blend` is read by a tool (tailoring4).
  - Proof folder `thumb-rig-probe-13-v3h` shows V3H is the newest version.
  - No runtime use.
- **Verdict:** DECIDE. Keep the latest blend of each line and archive the rest: the 18 superseded versions within the same line (for example FistSurface V3 to V3F, ContinuousHands V2/V3, ClosedFist V3/V3B) total about 316 MB, more if whole lines are retired. The owner should confirm which blend produced `Unity/Assets/Resources/Tennis/Premium`. Confidence: medium.

**ART-27: `hero/kits/` (`standard_wb`, `racket_classic`), 10 files**
- **What:** reference photos and READMEs for the installed kit and racket, from 2026-10-02.
- **Size:** 1.4 MB. Tracked: yes. LFS: 1.4 MB. Last commit: 2026-10-04.
- **Evidence:** prompts 10, 10b, 10c and 11, plus two `work/` jobs.
- **Verdict:** KEEP. Confidence: medium.

**ART-28: `hero/source/reference/`, 14 files**
- **What:** the reference-anatomy source, derived from the CC0 Blender Human Base Meshes: blends, FBX, graft and sculpt scripts, and PROVENANCE.md.
- **Size:** 22 MB. Tracked: yes. LFS: 22.2 MB. Last commit: 2026-10-08.
- **Evidence:** the FBX are byte-identical to `Unity/Assets/Resources/Tennis/HeroReference/{Male,Female}.fbx`. Those are loaded by `HeroReferenceAnatomy.cs`, a pilot that only runs when `VISUAL_REFERENCE_HEAD=1`.
- **Verdict:** KEEP. It becomes DECIDE if that pilot is dropped. Confidence: medium.

### hero/: retired Hero01 lineage (V1 to V6)

Unity keeps its own copies of these characters under `Unity/Assets/ArtDirection/{Hero01,HeroV5AstraTex,HeroV6,HeroV6Polish}`.

**ART-29: Hero01 V1 originals, 70 files**
- **What:**
  - `hero/` root files: `Hero_01.blend/.fbx`, textures, renders, scripts, the provenance/validation/verification JSON and HERO_ARCHIVE_Hero01.md
  - `_rejected_mannequin/` and `turnarounds/`
  - `modular/`, which holds MODULAR.md and the Mixamo upload body
- **Size:** 86 MB. Tracked: yes. Last commit: 2026-10-04. LFS: 84.2 MB now, 79.6 MB in history.
- **Evidence:** `docs/character-handoff/README.md` links `modular/MODULAR.md`.
- **Verdict:** DECIDE: archive it. Confidence: medium.

**ART-30: `hero/v5` build renders, 496 files**
- **What:** the `iter1` to `iter22` render folders, `*_hair`/`*_sheet` JPGs, `probe/` and `nohat/`, from 2026-09-27.
- **Size:** 419 MB. Tracked: yes. LFS: 419 MB. No references. Last commit: 2026-09-28.
- **Verdict:** SAFE. Confidence: high.

**ART-31: `hero/v5/archive_V4/CharacterAssets_locker/` and `hero/v5/viewer/`, 70 files**
- **What:** a backup of the V4 locker assets.
  - 80 MB of it is byte-identical to files that ship today in `GolfArcade/Unity/CharacterAssets` and `Unity/Assets/Resources/Tennis/Customization`.
  - `viewer/` holds an HTML/GLB viewer and `HeroMenu.bin`, which is also in the ignored `preview-raw` folder.
- **Size:** 95 MB. Tracked: yes. About 70 MB of this is JSON stored as normal git blobs. Only RESTORE_V5.md mentions it. LFS: 24.7 MB now, 23.1 MB in history. Last commit: 2026-10-04.
- **Verdict:** SAFE. Confidence: high.

**ART-32: Hero V5 sources and experiments, 290 files**
- **What:**
  - `hero/v5`: the V5 blends, `archive_V4/unity` (the V4 FBX and prefabs) and RESTORE_V5.md
  - `v5_astra`: its baseline blend is a byte-identical copy of `hero/v5/Hero_01_V5.blend` (51 MB)
  - `v5_tools`
  - `v5_styles` and `v5_tripo`: Tripo hair generations
  - `v5_scalp`
- **Size:** 328 MB. Tracked: yes. Last commit: 2026-10-04. LFS: 327.8 MB now, 321.6 MB in history.
- **Evidence:**
  - A `HeroTennisDriver.cs` comment cites `v5_tools/build_hair_v5.py`.
  - `HeroIntegrityAudit` writes `v5_astra/baseline/unity-audit.txt`.
  - `PLAN_HeroV5_AstraTextureSidePass.md` links it.
- **Verdict:** DECIDE: archive it. At minimum, drop one of the two identical V5 blends. Confidence: medium.

**ART-33: `hero/v5_target/BEFORE/`, 49 files**
- **What:** "before" stills and a copy of the multi-angle review. 3 of its mp4s (565 MB) are byte-identical to `anims/multi_angle_review` (ART-38).
- **Size:** 886 MB. Tracked: yes. LFS: 885.9 MB. Last commit: 2026-09-28.
- **Verdict:** SAFE. Confidence: high.

**ART-34: `hero/v5_target/` target images, 5 files**
- **What:** the target images and ACCEPTANCE.md.
- **Size:** 0.9 MB. LFS: 944.6 KB. Last commit: 2026-09-28.
- **Evidence:** linked from PLAN_HeroLookShip_FinishedProduct.md, PLAN_HeroV5_*.md and PLAN_HeroV6_ZeroDeficiency.md.
- **Verdict:** DECIDE, together with those plans. Confidence: high.

**ART-35: `hero/v5_proof/`, 646 files**
- **What:** the V5 gate proof (12angle, hairfit and hairfit_unity, locker, styles, blender_*), plus LIGHTING.md and GATE_RESULTS.md.
- **Size:** 206 MB. Tracked: yes. LFS: 206 MB now, 225 MB in history. Last commit: 2026-09-29.
- **Evidence:**
  - **LIGHTING.md is cited in live code comments:** `GolfArcade/Unity/CharacterModelPreview.swift` (its light recipe) and `Unity/Assets/Scripts/Tennis/TennisLook.cs` (`SunDirection`).
  - `HairFitAuditTests` (`[Explicit]`) writes `hairfit_unity/`.
  - `docs/character-handoff/README.md` links `hairfit/`, `locker/` and `styles/`.
- **Verdict:** SAFE+FIX. Move LIGHTING.md to `docs/` and update both comments; fix the handoff links. Confidence: high.

**ART-36: `hero/integrity_v1/`, 307 files**
- **What:** the rejected 2026-09-29 integrity-repair experiment (V5 was restored afterwards). It holds renders, `Hero_Integrity_v1.blend`, `export/` FBX, `before/assets`, the `active_before_v5_restore/` snapshot and a viewer.
- **Size:** 167 MB. Tracked: yes. 23.5 MB is byte-identical to Unity copies. LFS: 164.1 MB now, 130 MB in history. Last commit: 2026-10-04.
- **Evidence:** `HeroIntegrityAudit.cs` reads `export/` and writes `before/assets`; HeroIntegrityReview writes renders here.
- **Verdict:** SAFE+FIX. Retire those two legacy tools at the same time. Confidence: high.

**ART-37: `hero/v6_full`, `v6_polish`, `v6_proof`, `v6_target`; 562 files**
- **What:** the failed V6 candidate ("GATE: V6 FAIL... do not promote"): a Tripo rigged GLB, polish blends and tools, proof, and the target/BEFORE folder.
- **Size:** 208 MB. Tracked: yes. LFS: 207.4 MB now, 204.4 MB in history. Last commit: 2026-10-04.
- **Evidence:**
  - Written by HeroV6PolishImport, HeroV6PolishReview and HeroV6PolishBeforeReview.
  - Linked by PLAN_HeroV6_ZeroDeficiency.md and `docs/character-handoff/README.md`.
- **Verdict:** SAFE+FIX. Retire the 3 HeroV6Polish* tools and HeroV6RetargetTests, and fix those docs. Confidence: high.

### anims/

**ART-38: `anims/multi_angle_review/`, 28 files**
- **What:** 12-angle review films of Hero01 gameplay, from 2026-09-27: 5 mp4s (the 60 fps master is 350 MB), chapter JPGs and a README.
- **Size:** 878 MB. Tracked: yes. LFS: 878 MB. Last commit: 2026-09-28.
- **Evidence:** default output of `TennisMultiAngleCaptureTests` (`[Explicit]`). `docs/character-handoff/README.md` links the 2x mp4.
- **Verdict:** SAFE+FIX. Fix that doc link. Confidence: high.

**ART-39: `anims/captures/`, 21 files**
- **What:** gameplay films: `FullMatch_PlayerPOV_60fps.mp4` (444 MB), OnePoint, the Plan1, 1B, 2, 2B, 2C, 2D and 3A films, the Score80 films, LobSmash_UltimateScenes, ServeWalkIn and AnimationTempo.
- **Size:** 782 MB. Tracked: yes. LFS: 782 MB. Last commit: 2026-09-28.
- **Evidence:**
  - Default output of TennisFullMatchCaptureTests and TennisSinglePointCaptureTests (`[Explicit]`).
  - Linked by the PLAN1, 1B, 1C, 2 and 2B docs, `docs/tennis-lobs-and-ultimate-scenes.md` and `score80/NOTES.md`.
- **Verdict:** SAFE+FIX. Fix the doc links, and keep the LobSmash film if that doc is current. Confidence: high.

**ART-40: `anims/source/`, 28 files**
- **What:** third-party mocap: the Eyes JAPAN/MocapData free tennis pack (`tennis.zip` plus the extracted FBX/C3D), with SOURCE.md noting a CC BY / BY-SA 2.1 JP licence caveat. Also the Mixamo bind/idle FBX and an unrigged body.
- **Size:** 249 MB. Tracked: yes. LFS: 248.7 MB. Last commit: 2026-09-28.
- **Evidence:**
  - No code refers to it.
  - The current MatchHero clips do not come from it: `work/match-anim-set` says no footage is in the repo and its motion comes from WATCH.md.
- **Verdict:** DECIDE. The pack can be downloaded again; keep it only if a shipped clip still derives from it. Confidence: medium.

**ART-41: `anims/restore_motion/`, 245 files**
- **What:** the Hero01 V5 restore-motion work. Its FBX are byte-identical to `Unity/Assets/ArtDirection/Hero01/Models/RestoreMotion`. It also holds the tools, renders, `unity_gameplay_config.json`, `unity_review_config.json` and `unity_hand_ref.json`.
- **Size:** 78 MB. Tracked: yes. LFS: 76.5 MB. Last commit: 2026-09-28.
- **Evidence:** the 3 JSON files are read by `HeroGameplayBuild.Build` (also called from `HeroLockerExport.Run`) and by HeroRestoreMotionReview.
- **Verdict:** SAFE+FIX. Retire those legacy tools (see LegacyHero01.cs) or keep the 3 JSON files. Confidence: high.

**ART-42: the other `anims/` Hero01 phases, 432 files**
- **What:**
  - Root scripts, blends and reports
  - `_archived/`, which holds the rejected OWNED v1 clips
  - Phase folders: `anim_repair`, `backhand_run`, `cosmetics`, `diagnose_restore`, `eyes_clean`, `eyes_raw_retarget`, `fix_hand_clip`, `fullstroke_grip`, `gameplay_fixed`, `knees_offarm`, `legacy_on_v4`, `own_mocap_*`, `polish_*_before`, `preview_frames`, `specific_before`, `tennis_complete`, `v4_before`, `v5_before`, `v5_archived`, `wardrobe`
  - `animator_before/staged_legacy_TennisMotion.fbx`, which is byte-identical to `Unity/Assets/Resources/StandardCharacters/standard_male_tennis.fbx` (50 MB)
- **Size:** 611 MB. Tracked: yes. 102 MB (including that 50 MB FBX) is byte-identical to copies in `Unity/Assets` (ArtDirection/Hero01 and Resources/StandardCharacters). LFS: 587.8 MB. Last commit: 2026-09-28.
- **Evidence:** HeroGameplayFixCapture writes `anims/gameplay_fixed`. `docs/character-handoff/README.md` mentions `_archived` ("do not restore").
- **Verdict:** SAFE+FIX. Drop that doc mention. Confidence: medium; some of these are unique Blender work files of a retired character.

### score80/

**ART-43: `score80/`, 2,205 files**
- **What:** the 2026-09-27 "Score 64→80" Hero01 evaluation:
  - 7 capture runs (`baseline`, `phaseA`, `A2`, `BC`, `ABCD`, `F1`, `F2`), each an mp4 plus csv and txt, about 890 MB in total
  - `phaseD` hair and wardrobe A/B orbit frames: 1,974 files, 197 MB
  - `phaseE` environment audit: 180 PNG, 169 MB
  - `lineup/`, `stills/`, `probe/` and `native_launch_*.txt`
- **Size:** 1.2 GB. Tracked: yes. LFS: 1.2 GB. Last commit: 2026-10-04.
- **Evidence:**
  - The Score80 tests (`[Explicit]`) write here.
  - **NativeLaunchHeroTests is not opt-in** and writes `native_launch_run.txt` here on every PlayMode run, without creating the folder.
  - A `HeroTennisDriver.cs` comment cites `probe/hair_winding.txt`.
- **Verdict:** SAFE+FIX. First change NativeLaunchHeroTests to write to a temp path, or make it create the folder; otherwise that test fails. Keep `probe/hair_winding.txt` or update the comment. Confidence: high.

### review/

**ART-44: `review/unimate-tennis/`, 370 files**
- **What:** UniMate AI motion tests on V5, review only and never adopted, from 2026-09-29: blends, exports, decoded motions and frames.
- **Size:** 488 MB. Tracked: yes. No code references. LFS: 484.5 MB. Last commit: 2026-10-04.
- **Verdict:** SAFE. Confidence: medium.

**ART-45: `review/v6_retarget/`, 953 files**
- **What:** Hero01-vs-V6 retarget frames and an mp4.
- **Size:** 119 MB. Tracked: yes. LFS: 119.2 MB. Last commit: 2026-09-28.
- **Evidence:** default output of `HeroV6RetargetTests` (`[Explicit]`).
- **Verdict:** SAFE+FIX. Retire that test along with V6. Confidence: high.

**ART-46: `review/intro/`, `intro_waves/`, `review_waves.mp4/.json` and `make_review.py`; 339 files**
- **What:** Hero01 intro cinematic captures, from 2026-09-28.
- **Size:** 298 MB. Tracked: yes. LFS: 297.7 MB. Last commit: 2026-09-28.
- **Evidence:** output of `IntroCaptureTests` (`[Explicit]`).
- **Verdict:** SAFE+FIX. Change that test's output path. Confidence: high.

**ART-47: `review/venues/` and `review/receive-camera/`, 14 files**
- **What:** venue films (TennisVenueFilmTests, opt-in) and receive-camera films and stills.
- **Size:** 118 MB. Tracked: yes. LFS: 118 MB. Last commit: 2026-09-29.
- **Evidence:**
  - `TennisReceiveCameraTests` is not opt-in and rewrites 3 PNGs on every PlayMode run.
  - `docs/receiving-stance-and-camera.md` links `Before_After_Receive_Camera_60fps.mp4`.
- **Verdict:** SAFE+FIX. Move that test's output, and keep the linked film if the doc stays. Confidence: high.

**ART-48: `review/tv-menus/`, 6 files**
- **What:** a TV menu tour film and stills.
- **Size:** 32 MB. Tracked: yes. LFS: 31.6 MB. Last commit: 2026-09-28.
- **Evidence:** written by the opt-in `testRecordTVMenuTour`. `docs/character-handoff/README.md` links `TV_Intro_and_Menus.mp4`.
- **Verdict:** SAFE+FIX. Fix that doc link. Confidence: high.

**ART-49: `review/post-match/`, 21 files**
- **What:**
  - Output of PostMatchSnapshotTests, rewritten on every run. LFS history already holds 79 MB of versions against 29 MB now.
  - `rep-tv-01` to `rep-tv-13` and `rep-tv-final` are 14 byte-identical images.
  - Also holds hand-made physical-device evidence, `phone-finish-real-device.png` and `device-verification.txt`, which the test cannot regenerate.
- **Size:** 29 MB. Tracked: yes. LFS: 29.4 MB now, 79.3 MB in history. Last commit: 2026-10-08.
- **Evidence:** `docs/post-game-phone-actions.md` links `phone-finish-actions.png` and `phone-finish-real-device.png`.
- **Verdict:** SAFE+FIX. Redirect the test output, and move the two device-evidence files next to the doc. Confidence: high.

**ART-50: `review/tennis-timing-20261006/` and `review/point-clips/`, 16 files**
- **What:** gate evidence and sweep CSV/XML for the Oct 6 timing pass, plus recorder smoke logs.
- **Size:** 0.9 MB. Tracked: yes. No code references. LFS: 828.7 KB. Last commit: 2026-10-06.
- **Verdict:** DECIDE. Confidence: high.

**ART-51: `review/current_model/` and `review/extra/`, 5 files**
- **What:** Hero01 screenshots from 2026-09-28.
- **Size:** 8.6 MB. Tracked: yes. LFS: 8.6 MB. Last commit: 2026-09-28.
- **Verdict:** SAFE. Confidence: high.

### ui/

**ART-52: `ui/toybox-simple-v6/` approved set, 18 files**
- **What:** APPROVED.md (Adnan approved the set on 2026-09-29), the DIRECTION, IMPLEMENTATION and MENUS docs, 10 mockups and `tools/package-preview.swift`.
- **Size:** 22 MB. Tracked: yes. LFS: 21.9 MB. Last commit: 2026-09-29.
- **Evidence:** cited by `GolfArcade/Unity/ClubDesign.swift`: "Approved Island menus (ArtDir/ui/toybox-simple-v6)".
- **Verdict:** KEEP. It becomes DECIDE once the walkable-plaza menus replace IslandUI. Confidence: medium.

**ART-53: `ui/toybox-simple-v6/runtime/`, 24 files**
- **What:** output of `testApprovedIslandScreens`, rewritten on every run. 16 files are modified now.
- **Size:** 69 MB. Tracked: yes. LFS history already holds 208 MB of versions. Last commit: 2026-10-06.
- **Verdict:** SAFE+FIX. Write the output to `$TMPDIR` (or `MENU_SNAP_DIR`). Confidence: high.

**ART-54: `ui/toybox-simple-v6/source/`, 26 files**
- **What:** 24 git-ignored `preview-raw` `.bin`/`.json` menu bakes, written by the legacy `HeroLockerExport.RunMenuPreview`, plus 2 tracked files.
- **Size:** 167 MB. Tracked: partly (166 MB is ignored). LFS: 967 KB. Last commit: 2026-09-29.
- **Verdict:** SAFE. Confidence: high.

**ART-55: `ui/motion-club-radial-v1/runtime/`, 111 files**
- **What:** MotionClubReviewTests recordings, written on every run unless `MENU_MOTION_DIR` is set.
- **Size:** 283 MB. Tracked: yes. LFS history already holds 422 MB. Last commit: 2026-10-06.
- **Evidence:** `environments/motion-club-3d/ENVIRONMENTS.md` links `Motion_Club_Living_Backgrounds.mp4`.
- **Verdict:** SAFE+FIX. Default `MENU_MOTION_DIR` to a temp folder and fix that link. Confidence: high.

**ART-56: `ui/motion-club-radial-v1` docs, mockups and tools, and `ui/motion-club-gameplay-hud-v1`; 18 files**
- **What:** the implemented Motion Club menu and HUD direction from 2026-09-29. RallyPopHudCapture writes `hud-v1/implemented`.
- **Size:** 18 MB. Tracked: yes. LFS: 17.9 MB. Last commit: 2026-10-04.
- **Verdict:** DECIDE. Confidence: medium.

**ART-57: superseded UI concept rounds, 218 files**
- **What:** six review-only rounds from 2026-09-29, all superseded by v6: `weekend-club-v1`, `gameplay-mockups-v3`, `toybox-refined-v4`, `joy-directions-v2`, `toybox-reference-v5` and `motion-club-resort-v1`.
- **Size:** 77 MB. Tracked: yes. LFS: 76.6 MB now, 72.1 MB in history. Last commit: 2026-10-04.
- **Evidence:** fonts and a few art files are byte-identical to the shipping `GolfArcade/Unity/Fonts`, `MenuArt` and `Unity/Assets/Resources/Club`; those shipping copies are independent. No code references.
- **Verdict:** DECIDE. Removing it is safe. Confidence: high.

### golf/

**ART-58: `golf/` canonical sources, 54 files**
- **What:** README ("Final golf art sources"), `source/*.blend`, `tools/*.py`, manifests and vendor licences.
- **Size:** 195 MB. Tracked: yes. Last commit: 2026-10-08. LFS: 194.7 MB.
- **Evidence:**
  - VISUAL_OVERHAUL_LEDGER.md says the source and proof are "retained in ArtDir/golf".
  - Used by `work/full-visual-overhaul` and `work/reference-rebuild`.
  - The two `gallery3-inputs` master blends are byte-identical to `tennis/audience/inputs/Golf_*_Source.blend` (76 MB).
- **Verdict:** KEEP. Confidence: high.

**ART-59: `golf/source/GolfGalleryHero3.blend`, 1 file**
- **What:** `source-manifest.json` calls it a "Superseded chest-patch debugging checkpoint, not current source".
- **Size:** 5.8 MB. LFS: 5.8 MB. Last commit: 2026-10-08.
- **Evidence:** the manifest and the `*-gallery-hero3.py` tools still list it.
- **Verdict:** SAFE+FIX. Remove its manifest entry and the hero3 tools. Confidence: high.

**ART-60: `golf/proof/`, 102 files**
- **What:** turf, cliff and scenery proof sets.
- **Size:** 67 MB. Tracked: yes. LFS: 58.9 MB. Last commit: 2026-10-08.
- **Evidence:** 52 MB is byte-identical to `proof/reference-rebuild/*` and `proof/full-visual-overhaul/*`. The golf manifests cite it.
- **Verdict:** DECIDE. Keep one copy, either here or in `proof/`. Confidence: medium.

### tennis/

**ART-61: `tennis/` current sources, 41 files**
- **What:**
  - `tennis/source/TennisHorizon2.blend`, the "canonical source" in the ledger, plus `TennisResortPalmFinish1.blend`
  - `tennis/venues`: botany, architecture and materials build scripts and blends
  - `tennis/audience` current set: README, manifest, tools, `source/*Hero6.blend`, `inputs/`, and `staging6/`, which is byte-identical to `Unity/Assets/Resources/Tennis/Premium/Tennis{Seated,Promenade}Hero3.fbx`
- **Size:** 131 MB. Tracked: yes. LFS: 130.9 MB now, 118.1 MB in history. Last commit: 2026-10-08.
- **Verdict:** KEEP. Confidence: high.

**ART-62: `tennis/audience` older iterations, 8 files**
- **What:** the `source/*Hero4.blend` and `*Hero5.blend` blends and `staging5/`.
- **Size:** 50 MB. Tracked: yes. 26 MB is duplicates. LFS: 50.4 MB now, 37.6 MB in history. Last commit: 2026-10-08.
- **Evidence:** listed in `source-manifest.json`.
- **Verdict:** SAFE+FIX. Update the manifest. Confidence: medium.

### environments/ and other folders

**ART-63: `environments/refs/`, 3 JPG**
- **What:** the user's target stills: needle, split and crater.
- **Size:** 1.4 MB. Tracked: yes. LFS: 1.4 MB. Last commit: 2026-10-06.
- **Evidence:** read by `postcard_look_verify.py`; supplies the palette for `golf_look_textures.py`; cited by POSTCARDS_README.md.
- **Verdict:** KEEP. Confidence: high.

**ART-64: `environments/motion-club-3d` sources, 12 files**
- **What:** the four `Club_*.blend` files, `tools/build_club_environments.py` and related tools, `sources/`, `previous-v1/`, ENVIRONMENTS.md and `bake-report.json`.
- **Size:** 46 MB. Tracked: yes. LFS: 46.4 MB. Last commit: 2026-10-06.
- **Evidence:** this is the source of the shipping `GolfArcade/Unity/ClubEnvironments/Club_*.json` and albedos. `ClubEnvironmentView(room: .locker)` is used in ClubDesign.swift.
- **Verdict:** KEEP. Confidence: medium.

**ART-65: `environments/motion-club-3d` review images, 34 files**
- **What:** `review/`, `review-v2/`, `mockups-v2/` and `review-live-images/`. ClubEnvironmentTests rewrites the last one on every run.
- **Size:** 72 MB. Tracked: yes. LFS: 71.5 MB now, 69 MB in history. Last commit: 2026-10-06.
- **Verdict:** SAFE+FIX. Redirect the test output and fix the ENVIRONMENTS.md link. Confidence: high.

**ART-66: `plates/`, 14 files**
- **What:** the six original concept plates from 2026-09-25 (01_hero to 06_loading), their sources and PLATES.md, which says "PENDING ... NOT LOCKED". The base_lock plates have replaced them for characters.
- **Size:** 17 MB. Tracked: yes. LFS: 17.4 MB. Last commit: 2026-09-28.
- **Evidence:** cited by the PLAN_HeroLookShip, V5 and V6 docs and by `docs/character-handoff`.
- **Verdict:** DECIDE. Confidence: medium.

**ART-67: `env_v4/`, 2 tracked files**
- **What:** `EnvV4_kit.blend` and `tools/build_env_v4.py`. Together they are the source of the EnvV4 prop kit.
- **Size:** 0.2 MB. Tracked: yes. LFS: 176.9 KB. Last commit: 2026-09-28.
- **Evidence:** `TennisEnvironmentV4.Apply` applies the kit at runtime and is called from `TennisGame.cs:242`. `tennis/venues/architecture/build-clubhouse.py` imports `build_env_v4.py`.
- **Verdict:** KEEP. Confidence: high.

**ART-68: `env_v5/`, 12 files**
- **What:** a Tripo tree experiment from 2026-09-27.
- **Size:** 4.4 MB. Tracked: yes. No references. LFS: 4.4 MB. Last commit: 2026-09-28.
- **Verdict:** SAFE. Confidence: high.

**ART-69: `blockout/`, 9 files**
- **What:** the Tripo hero blockout from 2026-09-25.
- **Size:** 28 MB. Tracked: yes. Only Hero01 scripts mention it. LFS: 28.2 MB now, 19.9 MB in history. Last commit: 2026-09-28.
- **Verdict:** SAFE. Confidence: high.

**ART-70: environment briefs, 6 files**
- **What:** `environments/12`, `13`, `14`, `14b` and `15` `.txt` briefs and `GOLF_ALL_COURSES_POSTCARD_STANDARD_PLAN.md`.
- **Size:** 66 KB. Tracked: yes. LFS: none (plain git blobs). Last commit: 2026-10-06.
- **Evidence:** cited as authority by `postcard_look_verify.py` and `postcard_look_lib.py`, POSTCARDS_README.md, and a PostcardHoleTests comment.
- **Verdict:** KEEP. The docs auditor owns the wording. Confidence: high.

**ART-71: prompts and prose docs, 68 files**
- **What:**
  - `ArtDir/*.md`
  - `ArtDir/docs/`
  - `onboarding/32`
  - `ui/33`, `34` and `35`
  - `environments/30b`
  - `hero/base_lock/parallel_prompts/` (58 prompts)
  - the untracked `hero/38_LATEST_HEROES_EVERYWHERE.txt`
- **Size:** 0.5 MB. Tracked: partly (one file is untracked). LFS: none (plain git blobs). Last commit: 2026-10-08.
- **Verdict:** DECIDE. Deferred to the docs auditor. Confidence: high.

**ART-72: git-ignored local junk, 73 files**
- **What:** 47 Blender `.blend1` backups and `__pycache__` files. The largest backups:

  | Folder | Size |
  |---|---|
  | `hero/kit/premium/source` | 173 MB |
  | `hero/v5` | 51 MB |
  | `environments/motion-club-3d` | 44 MB |
  | `anims/*` | about 120 MB |
  | `hero/v6_polish` | 27 MB |

- **Size:** 464 MB. Tracked: no (ignored via `*.blend1` and `__pycache__/`). Never committed.
- **Verdict:** SAFE. These can be deleted at any time. Confidence: high.

## Duplicates

**Inside ArtDir**

There are 900 sets of byte-identical files (2,846 files), holding 2.06 GB. Keeping one copy of each set would free 1.09 GB. The biggest sets:

| Duplicate | Size |
|---|---|
| `anims/multi_angle_review` vs `hero/v5_target/BEFORE/multi_angle_review` (3 mp4s) | 565 MB |
| identical consecutive frames in `screenshots/*_playmode` and `gameplay_fixed` | about 160 MB |
| `golf/source/gallery3-inputs` vs `tennis/audience/inputs` | 76 MB |
| `hero/v5/Hero_01_V5.blend` vs `hero/v5_astra/Hero_V5_AstraTex_Baseline.blend` | 51 MB |
| `review/post-match` `rep-tv` images (14 copies) | 21 MB |
| `tennis/audience` Hero5 triplets | 12 MB |
| base_lock `*_plain.jpg` vs `*_bald.jpg` | intentional |

**Outside ArtDir**

250 ArtDir files (314 MB) have byte-identical copies in 257 places elsewhere in the repo (334 MB). Copies in the shipping folders are the ones the game uses:

| ArtDir copy | Other copy | Size |
|---|---|---|
| `hero/v5/archive_V4/CharacterAssets_locker` | `GolfArcade/Unity/CharacterAssets` and `Unity/Assets/Resources/Tennis/Customization` | about 80 MB |
| `anims/*` FBX | `Unity/Assets/ArtDirection/Hero01` and `Resources/StandardCharacters` | about 100 MB |
| `golf/proof` | `proof/reference-rebuild` | about 52 MB |
| `hero/base_lock/unity_import` | `Unity/Assets/Characters/HeroBase` (by design) | 3 MB |
| `tennis/audience/staging6` | `Unity/Assets/Resources/Tennis/Premium` (by design) | 14 MB |
| `hero/kit` tailoring4 maps | shipping MatchHero_Kit maps (by design) | — |
| UI fonts and art | `GolfArcade/Unity/Fonts` and `MenuArt` | — |

## Git and LFS: what deleting actually frees

- **ArtDir is stored on this Mac twice.**
  - The working tree holds 15.1 GB.
  - The local LFS cache (`.git/lfs/objects`, 24 GB for the whole repo) holds the other copy: ArtDir's history has 21,827 unique LFS objects (13.8 GB) across the 12 commits that touched it on any branch, and all of them are in that cache.
  - In total ArtDir costs about 29 GB on disk here.
- **Deleting and committing only frees the working-tree copy right away.**
  - The local cache copies go only after `git lfs prune`, once no retained ref needs them.
  - The server's LFS storage keeps everything unless history is rewritten (filter-repo, force-push, then LFS cleanup on the host).
  - About 150 MB of non-LFS blobs (JSON, `.npz`, `.csv`) also stay in `.git` until history is rewritten.
- **Test output is already growing LFS history:**

  | Folder | Size now | LFS history |
  |---|---|---|
  | `ui/toybox-simple-v6/runtime` | 69 MB | 208 MB |
  | `ui/motion-club-radial-v1/runtime` | 283 MB | 422 MB |
  | `review/post-match` | 29 MB | 79 MB |

- **Reading the per-group LFS figures:** "LFS history" counts an object that two groups share in both groups, so the per-group figures add up to more than the ArtDir-wide unique total of 13.75 GB.

## Verdict summary

| Verdict | Groups | Files | Size on disk | LFS now | LFS history (per-group sum) | Not in git |
|---|---:|---:|---:|---:|---:|---:|
| SAFE | 15 | 14,801 | 6.52 GB | 5.83 GB | 5.72 GB | 630 MB |
| SAFE+FIX | 27 | 7,912 | 6.05 GB | 6.02 GB | 6.27 GB | 0 |
| DECIDE | 13 | 1,107 | 1.86 GB | 1.85 GB | 1.83 GB | 4 KB |
| KEEP | 17 | 831 | 0.70 GB | 0.68 GB | 0.66 GB | 0 |
| **Total** | **72** | **24,651** | **15.14 GB** | **14.38 GB** | **13.75 GB unique** | **630 MB** |

Groups in each verdict:

- **SAFE:** ART-01, 02, 06, 08, 10, 15, 30, 31, 33, 44, 51, 54, 68, 69, 72
- **SAFE+FIX:** ART-03, 04, 05, 07, 09, 11, 12, 13, 14, 35, 36, 37, 38, 39, 41, 42, 43, 45, 46, 47, 48, 49, 53, 55, 59, 62, 65
- **DECIDE:** ART-22, 23, 26, 29, 32, 34, 40, 50, 56, 57, 60, 66, 71
- **KEEP:** ART-16, 17, 18, 19, 20, 21, 24, 25, 27, 28, 52, 58, 61, 63, 64, 67, 70
