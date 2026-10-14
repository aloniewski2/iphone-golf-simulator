# Scratch audit: work/, proof/, worktrees, branches, stashes, .git, junk files

Repo: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`. Branch final-build @ 14a43331. Audit date 2026-10-10, read-only: nothing in the repo or any worktree was changed.

## How to read this

- **Sizes are du-style GiB.** `du` says work/ = 651.0 GiB and proof/ = 20.6 GiB.
- **"Really freed" is not the same as du.** Most of work/ is Unity project copies. They were made as APFS clones (copy-on-write), so they share disk blocks with each other and with the live `Unity/` project. `du` counts every copy in full.
  - Method: every file in work/, proof/, the live project dirs, `.git` and the 4 other worktrees (2.81 M files) was opened read-only. `fcntl F_LOG2PHYS_EXT` returned the physical location of each file's first block. Files that report the same location share their blocks.
  - "Really freed" counts a file only when nothing outside the deleted set shares its blocks.
  - Result: **deleting ALL of work/ + proof/ would free only ≈302 GiB, not 672 GiB.**
  - Validation sample: 96.6 % of the large-file bytes in `serve-walk-arm/UnityClone` are shared, 59 % of them with the live `Unity/`.
  - These numbers use apparent file sizes (not allocated blocks) and are a little low for folders with many tiny files.
- **The disk is 99 % full:** `/System/Volumes/Data` is 926 GiB with 16 GiB free. There are no APFS local snapshots, so a delete frees its space straight away.
- **One folder holds a live mount.** `work/cloth-overhaul/toolchain/mounted` is a mounted DMG (`/dev/disk11s1`). Its backing file is `toolchain/MetalToolchain-27A266a.exportedBundle/Restore/022-20184-410.dmg`. Run `hdiutil detach` before deleting. du counted 2.9 GiB that lives on that image, not on the data volume. Xcode uses the system Metal toolchain, not this copy (`xcrun --find metal` points to the cryptex).
- **Another session is working right now.** `work/postcard-look/heavy.lock/owner` = "integrate-xcode" (Oct 10 19:19). heavy.sh is the shared Unity/xcodebuild lock, so do not touch postcard-look's lock files.
- **Columns.**
  - "newest file" is the true newest file mtime (one full stat pass).
  - "live dependencies" comes from a search of GolfArcade/, GolfArcadeTests/, GolfArcadeUITests/, Unity/Assets/, Unity/Tools/, Tools/, blender/scripts/, server/, docs/, .github/, project*.yml and root *.md for `work/<name>`, `proof/<name>` and path-joined forms. ArtDir doc mentions are noted where relevant.
  - "only source" comes from sha256-matching all 379 shipped FBX/blend/lzfse files in Unity/Assets and GolfArcade against work/, plus a search for the source .blend outside work/.
- **Verdicts.**
  - SAFE: nothing current reads it and it is not the only copy of anything shipped.
  - SAFE+FIX: safe once a small code/doc pointer is changed or retired.
  - DECIDE: owner call (history, staged-not-installed, evidence).
  - KEEP: read by current code/tests/scripts, an active job, or the only source of shipped assets.
  - KEEP/DECIDE rows still list a deletable part. Its really-freed GiB is in [brackets].

## 1. One row per work/ and proof/ subfolder (101 rows, sorted by size)

| # | folder | plain-English purpose | size du (GiB) | really freed if deleted (GiB, clone-aware) | newest file | files | tracked | live dependencies (outside work/) | only source of something shipped? | verdict | what to do (SAFE part inside it, GiB really freed) | conf |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `work/serve-walk-arm` | Task 28 serve walk / ball arm: GATE FAIL, nothing installed except the ServeWalkFilm.cs editor tool; 11 Unity clones, renders, films, stale rollback backups. | 159.6 | 41.3 | 2026-10-05 20:44 | 818354 | 0 | Unity/Assets/Editor/ServeWalkFilm.cs default output ../work/serve-walk-arm/before/male-rh-deuce (creates dir). | No | **SAFE** | Delete (keep GATE_RESULTS.md / LEFTY_GAP.md / REPRO.md if wanted). | high |
| 2 | `work/golf-look` | Golf shaders + golf kit (prompt 30) 10-06: REPORT says integrated into final-build; proof renders, device builds, 2 Unity isolation clones, rollback snapshots, draft blends. | 76.5 | 44.2 | 2026-10-06 16:02 | 220012 | 0 | ArtDir parallel prompts / 30b resume note mention it; nothing in code. | No (golf kit authority is ArtDir/hero/kit/golf) | **DECIDE** | Keep REPORT/AUDIT/RESUME/NEXT_SWING md + rollback/README; delete proof/ (27 GB), device/ (23 GB builds), isolation/ (clones), native-app/ (DerivedData), locker/. [deletable part: 72.0 du / 41.2 really freed] | medium |
| 3 | `work/serve-toss-jump` | Prompt 26 serve toss-then-jump: FAIL, not installed (5 Unity clones, r2-r4 candidates). | 63.0 | 13.5 | 2026-10-05 23:29 | 322580 | 0 | Unity/Assets/Editor/ServeJumpProof.cs default output ../work/serve-toss-jump/r2/male (creates dir). | No | **SAFE** | Delete (keep GATE_REPORT*.md / NOT_INSTALLED*.md if wanted). | high |
| 4 | `work/cloth-overhaul` | Prompt 29 tennis-kit construction overhaul (collars, buttons, folds) 10-05: Cloth_{Male,Female}.blend, exported Male/Female_Kit.fbx + npz, Unity isolation clones, iOS before/after builds, a COPY of the Metal toolchain (mounted disk image!). | 48.1 | 19.7 | 2026-10-05 18:58 | 151057 | 0 | Unity/Assets/Editor/MatchHeroKit.cs reads ../work/cloth-overhaul/export/ (rebuild source of the shipped MatchHeroKit/*_Kit.fbx); ClothCompileProbe.cs requires running inside work/cloth-overhaul/isolation; ClothProofRig.cs writes before/. | YES: export/Cloth_*.blend is the only source of the shipped match-hero kit | **KEEP** | Keep export/, scripts, rollback/, *.md. Delete isolation/ (2 Unity clones), ios_before/ + ios_after/ (Xcode exports + DerivedData), locker/, before*/ after*/ renders. toolchain/ holds a mounted DMG: run `hdiutil detach work/cloth-overhaul/toolchain/mounted` first, then delete (Xcode uses the system Metal toolchain, not this copy). [deletable part: 45.5 du / 17.2 really freed] | high |
| 5 | `work/postcard-look` | Golf postcard LOOK pass (holes 8-10) + shared heavy.sh/heavy.lock that serialises Unity/xcodebuild runs across sessions (lock held RIGHT NOW by "integrate-xcode"). | 36.3 | 22.7 | 2026-10-10 19:19 | 95151 | 0 | blender/scripts/{golf_look_textures,postcard_look_verify,postcard_props_lib,postcard_look_lib,postcard_*_smoke,hole08-10_*}.py read baseline/, baseline_signature/, v2/props/sway, v2/libs_r*/backup, v2/proof/stills_cfg*.json; Unity Editor Golf*/Postcard* tools say "always through work/postcard-look/heavy.sh" and write v3/ (guarded output root); GolfLook.cs/GolfWindSwayTests comments. | Baselines + contract docs only here | **KEEP** | Keep everything except v3/ heavy outputs: delete v3/ios (16 GB device builds), v3/proof-project (Unity clone), v3/proof, v3/flythrough (keep v3/REPORT.md). [deletable part: 28.2 du / 14.6 really freed] | high |
| 6 | `work/presentation` | Presentation implementation verification 10-08/09 (loading cover etc.): 2 Unity clones, Xcode DerivedData, test xcresults, films. | 33.0 | 7.8 | 2026-10-09 12:42 | 139975 | 0 | None | No | **SAFE** | Delete (keep GATE_RESULTS.md if wanted). UnityReview/Temp/UnityLockfile is stale (no Unity editor running). | high |
| 7 | `work/golf-party` | Golf party-mode work (active today): GolfPartyChecks/Device xcodeprojs + project.yml, helper scripts, Unity clone, DerivedData, package/Payload (app build), native-proof. | 25.6 | 8.8 | 2026-10-10 18:47 | 75434 | 0 | GolfArcadeTests/MultiplayerTests.swift testGolfPartyPhoneScreens writes work/golf-party/native-proof/phone-result.png (#filePath); main-build worktree lists it untracked. | No | **KEEP** | Active (newest 10-10 18:47). When the golf session ends: delete Unity/ clone, DeviceDerivedData/, NativeDerivedData/, package/. [deletable part: 25.6 du / 8.8 really freed] | high |
| 8 | `work/online-lobby` | Online lobby 10-05 (Island room, synced heroes/emotes): emote parity data, Unity clone, simulator DerivedData, films. | 24.6 | 12.3 | 2026-10-10 12:13 | 80397 | 0 | GolfArcadeTests/LockerMirrorTests.swift READS emote_parity/data/MatchHero_*_emote_parity.json by default and writes emote_parity/rotation-report.txt (parent must exist); LockerMirrorProofTests writes emote_parity/ (ran today 12:13). | Emote-parity fixtures only here | **KEEP** | Keep emote_parity/ (+ md); delete UnityClone/, SimDerivedData/, film/, DerivedData/, swift-cache/, export_movie/. [deletable part: 24.3 du / 12.1 really freed] | high |
| 9 | `work/golf-motion-fit` | Golf motion fit 10-06: dressed golf heroes (Golf_*_Dressed/Master.blend), swing maps, Unity isolation clone, film frames. | 21.8 | 10.5 | 2026-10-06 12:39 | 83610 | 0 | Shipped GolfArcade/Unity/CharacterAssets/GolfHero_* swing lzfse are identical to copies here; Golf_*_Master.blend == ArtDir/tennis/audience/inputs/Golf_*_Source.blend. | Partly: Golf_*_Dressed.blend not found outside work | **DECIDE** | Keep export/, maps/, inputs/, rollback/; delete isolation/ (Unity clone + DerivedData) and film-frames*/ film-scratch*/. [deletable part: 20.8 du / 9.4 really freed] | medium |
| 10 | `work/golf-course-standard` | Golf course standardisation job 10-06: 2 Unity clones (BaselineUnity/AfterUnity), compiled DLLs, proof, tree-fix metrics. | 21.3 | 3.8 | 2026-10-06 20:40 | 121010 | 0 | None | No | **SAFE** | Delete. | high |
| 11 | `work/character-finish32` | Male tennis polo "beautification" stage 1 (AGENTS.md 2026-10-08 clothes-only pass): Male_Polo_*.blend candidates, Unity review clone, capture-queue results, STATUS.md (stage NOT passed). | 20.6 | 9.4 | 2026-10-10 11:41 | 68787 | 0 | Shipped opt-in Resources/Tennis/KitsTailored/MaleDistance.fbx is identical to a file here; ArtDir/hero/clothing/tailored-2026-10-08/README.md + fixture-update-proof.json cite it. | No (final blends archived in ArtDir/hero/clothing/tailored-2026-10-08/) | **KEEP** | Active job (newest 10-10 11:41, STATUS says stage 1 still open). When Adnan closes the pass: delete review-project/ (Unity clone) and the ~150 MB matrix-*-profile.json / *-queue.json.results.json dumps; keep the accepted .blend + STATUS.md. [deletable part: 12.5 du / 1.3 really freed] | high |
| 12 | `work/gameplay-regression` | Gameplay regression run 10-06: Unity clone, DerivedData, before/after test XMLs, fixed-*.cs copies. | 20.3 | 7.8 | 2026-10-06 17:18 | 69983 | 0 | None | No | **SAFE** | Delete. | high |
| 13 | `work/postcard-refresh` | Golf + tennis visual refresh 10-08 (48/48 golf gates): release-gate stills, split-source ReferenceHole09.blend/.fbx, UnityReview clone, capture queues. | 12.9 | 0.9 | 2026-10-08 21:23 | 61496 | 77 | 77 files TRACKED in git (commit 9283f6a8). Shipped Resources/Course/Resort/ReferenceHole09.fbx is identical to split-source/ReferenceHole09.fbx. | YES: split-source/ReferenceHole09.blend is the only .blend source of a shipped course model (tracked via LFS) | **KEEP** | Keep the tracked files (better: move split-source/ to ArtDir/golf/source); delete the untracked UnityReview/ clone, *-queue.json(.results.json), compile*.log. [deletable part: 12.3 du / 0.3 really freed] | high |
| 14 | `work/golf-tailoring` | Golf clothing tailoring 10-06: candidate golf kit (Golf_*_Draft.blend, kit_v2), Unity clone, USER_REVIEW.md (latest pass NOT applied; Adnan asked for videos before more changes). | 12.6 | 3.9 | 2026-10-06 21:16 | 66241 | 0 | Shipped Characters/GolfHeroKit/*_Kit.fbx (8 files) identical to files here; ArtDir/hero/kit/golf/tailoring/source/Golf_*.blend is the authority. | No (ArtDir has the source blends) | **DECIDE** | Keep USER_REVIEW.md, export/, scripts/, rollback/production until Adnan reviews; delete Unity/ (clone) and swift-cache/. [deletable part: 11.9 du / 3.3 really freed] | medium |
| 15 | `work/emote-fluid` | Emote fluidity polish; revision 37 APPROVED by Adnan and installed (12 clips); frozen rerun recipe, rollback, 34 iteration folders, films. | 11.1 | 11.1 | 2026-10-05 23:12 | 21202 | 0 | Installed Unity/Assets/Characters/MatchHeroes/*/*_Emote_*.fbx / *_Intro_*.fbx are byte-identical to candidate/fbx here; ArtDir prompts 31-33 cite it. | YES: candidate/ + tools/approved37 + rollback/ are the only source/recipe of the installed emotes | **KEEP** | Keep candidate/, tools/, rollback/, data/, *.md; delete iterations/ (6.1 GB) and film/ (4.8 GB). [deletable part: 10.9 du / 10.9 really freed] | high |
| 16 | `work/reference-rebuild` | Reference rebuild 10-08: coastal cliffs/botany, vendor CC0 assets + provenance, face-finish30 handoff, garment passes, gameplay films. | 7.4 | 7.4 | 2026-10-08 16:29 | 17916 | 0 | VISUAL_OVERHAUL_LEDGER.md: "verified downloads live under work/reference-rebuild/vendor"; CurrentGolfSwingFilm.cs writes gameplay-films31/golf; HeroReferenceImportAudit.cs writes import-audit.txt; ArtDir golf manifests cite it. | vendor/ downloads + provenance only here (sources exist in ArtDir/golf/source) | **DECIDE** | Keep vendor/, botany/, CURRENT_CHECKPOINT.md, face-finish30/HANDOFF*; delete original-native-adoption*/ (DerivedData), gameplay-films31/, garment-*/ renders. [deletable part: 5.1 du / 5.1 really freed] | medium |
| 17 | `work/full-visual-overhaul` | Full visual overhaul (Oct 7 plan) working logs/scripts, capture queues, character exports. | 7.0 | 6.0 | 2026-10-08 19:52 | 3945 | 0 | VISUAL_OVERHAUL_LEDGER.md: "Working logs/scripts: work/full-visual-overhaul/"; Unity/Assets/Editor/HeroThumbRigProbe.cs READS characters/exports/HandsV3/<Sex>_FistGolden.json. | Hand golden JSON only here | **KEEP** | Keep; delete phone-interim-derived/ (5.7 GB Xcode DerivedData) and garment-coverage/ renders. [deletable part: 6.1 du / 5.1 really freed] | high |
| 18 | `work/iphone-current` | iPhone build 10-08: DerivedData + install logs. | 6.5 | 5.5 | 2026-10-08 22:37 | 4724 | 0 | None | No | **SAFE** | Delete. | high |
| 19 | `work/multiplayer` | Multiplayer job 10-04: integrated export, DerivedData, typecheck stubs, logs. | 5.5 | 4.6 | 2026-10-04 19:39 | 8888 | 0 | None | No | **SAFE** | Delete. | high |
| 20 | `work/phone-build-20261006` | One-off device build at 7ecf8100 (10-06): export.py (moves 3 PlayMode tests aside, exports Unity, builds + installs), build/install reports, DerivedData, copies of Unity-regenerated files. | 5.4 | 4.4 | 2026-10-06 21:54 | 1500 | 0 | None (nothing outside reads it; only export.py refers to itself) | No | **DECIDE** | Keep export.py + build-report.json (a reusable phone-build recipe, KBs) or move them to Tools/; delete DerivedData/ (5.4 GB). [deletable part: 5.4 du / 4.4 really freed] | medium |
| 21 | `work/character-female33` | Female polo + skirt (same pass, reusing the male system): FemalePolo*.fbx / Female_Polo_*.blend / Female_Skirt candidates, LOD smoke, 3.2 GB of ~145 MB profiler/matrix JSON dumps. | 3.7 | 3.7 | 2026-10-08 19:07 | 2717 | 0 | Shipped Resources/Tennis/KitsTailored/Female*.fbx (4 files) are identical to files here; final blends archived in ArtDir/hero/clothing/tailored-2026-10-08/. | No (archived in ArtDir) | **KEEP** | Part of the open 2026-10-08 clothing pass (AGENTS.md: male polo, then female polo + skirt, then stop). After sign-off: delete the matrix-*-profile.json and *-queue.json.results.json dumps (~3 GB); keep the final blends/FBX + finalize_proof.py. | medium |
| 22 | `work/menu-beta` | Beta menus job 10-08: checks xcodeproj, DerivedData, xcresults, Source copy; its evidence is the tracked proof/menu-beta. | 2.5 | 2.0 | 2026-10-08 22:39 | 12455 | 0 | None | No | **SAFE** | Delete (proof/menu-beta keeps the evidence). | high |
| 23 | `work/hero-mainstay` | HERO_MAINSTAY 10-03: new heroes replace Hero01; notes, rebuild commands, clip table, proof, films. | 2.1 | 2.1 | 2026-10-10 12:13 | 3056 | 0 | MatchHeroBuild.cs reads data/clip_table.json and writes logs/; MatchHeroImport/Probe/Proof/AB/Film/LockerExport write logs/data/proof/film; GolfArcadeTests/MatchHeroLockerTests.swift writes proof/swift_locker (ran today); NativeLaunchHeroTests writes proof/lineup. | clip_table.json only here | **KEEP** | Keep; delete film/ (1.7 GB of frames). [deletable part: 1.7 du / 1.7 really freed] | high |
| 24 | `work/face-match` | Face matching passes 10-08 (37 numbered review folders, before/after reports). | 2.0 | 1.9 | 2026-10-08 17:33 | 5069 | 0 | Unity/Assets/Editor/HeroFaceMatchCapture.cs default output ../work/face-match/before/raw (creates dir). | No | **DECIDE** | History of the current face; keep */REPORT.md, delete the PNG passes (~1.4 GB) or the whole folder. | medium |
| 25 | `work/emote-overhaul` | Spike/Scuba/Thrust re-key 10-05 (README = rollback), later polished by emote-fluid. | 1.7 | 1.7 | 2026-10-05 22:10 | 2880 | 0 | Unity/Assets/Editor/EmoteHoldCheck.cs writes proof/hold (creates dir). | Source of the 3 re-keyed clips before the fluidity pass | **KEEP** | Keep README/rollback*/scripts/data; delete film_r2/ film/ proof_r2/ (~1.6 GB). [deletable part: 1.6 du / 1.6 really freed] | high |
| 26 | `work/male-pelvis-form2` | HeroBase body/head sculpt history (09-30: male pelvis FORM2 (rejected) (staged, never installed). | 1.5 | 1.5 | 2026-10-02 16:49 | 2163 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 27 | `work/female-torso-form5` | HeroBase body/head sculpt history (09-30: female torso FORM5 (bust/underbutt/leg-top) (staged -> full merge). | 1.4 | 1.4 | 2026-10-02 22:09 | 3625 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 28 | `work/serve-toss-arm` | SERVE_TOSS_ARM 10-03: failed serve re-key attempt (superseded by serve-dip-jump). | 1.4 | 1.4 | 2026-10-03 21:34 | 2745 | 0 | Unity/Assets/Editor/ServeTossFilm.cs default output ../work/serve-toss-arm/film (creates dir). | No | **SAFE** | Delete (1.5 GB, mostly films). | high |
| 29 | `work/onboarding` | Onboarding job 10-06 (P0-P10; final gate not met, code later integrated): DerivedData, 20 xcresults, reports. | 1.4 | 1.4 | 2026-10-06 12:31 | 10642 | 0 | None | No | **SAFE** | Delete (keep FINAL_RESULT.md / BASELINE.md if wanted). | high |
| 30 | `work/male-micro1l-head` | HeroBase body/head sculpt history (09-30: Micro1l r2 chin+lips (current male head at the time) (installed (copy in Unity/Assets/Characters/HeroBase/Models)). | 1.4 | 1.4 | 2026-10-02 16:36 | 2160 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | Produced the installed male head (Micro1l r2); archive scripts/export/MALE_HEAD_RESULTS*.md, delete r/ final renders. [deletable part: 0.6 du / 0.6 really freed] | medium |
| 31 | `work/male-pelvis-form3` | HeroBase body/head sculpt history (09-30: male pelvis FORM3 (staged, never installed). | 1.1 | 1.1 | 2026-10-02 18:09 | 1436 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | STAGED, never installed (delta awaits Adnan); keep until he decides, then delete or install. [deletable part: 1.0 du / 1.0 really freed] | medium |
| 32 | `work/male-nape-pelvis-skin` | HeroBase body/head sculpt history (09-30: male nape + pelvis + skin (chat 9) (installed). | 1.1 | 1.1 | 2026-10-02 12:11 | 1414 | 0 | Unity/Assets/Editor/HeroBaseSkinProof.cs READS skin/unity_cases.json and writes skin/unity/. | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE+FIX** | Keep skin/unity_cases.json (or retire HeroBaseSkinProof.cs); delete r/ renders (0.8 GB) and the rest. [deletable part: 0.9 du / 0.9 really freed] | medium |
| 33 | `work/serve-dip-jump` | SERVE_DIP_JUMP 10-03: re-keyed Serve (hip fold + hop); export/HeroBase_*_MatchAnims.blend, fbx, films. | 0.8 | 0.8 | 2026-10-03 22:44 | 1311 | 0 | None in code (the installed Male/Female_Serve.fbx are byte-identical to export/fbx here). | YES: only source of the installed Serve clips | **KEEP** | Keep export/, scripts/, data/, NOTE; delete film_male/ film_female/ film/ sheets/ (~0.7 GB). [deletable part: 0.7 du / 0.7 really freed] | high |
| 34 | `work/golf-motion-fix` | Golf per-shot Ready + calibration fix 10-06 (GATE PASS): logs, NativeDerived, phone install proof. | 0.8 | 0.7 | 2026-10-06 21:55 | 6696 | 0 | GolfArcadeTests/MotionClubReviewTests.swift testCaptureGolfReadyAndPracticeScreens writes work/golf-motion-fix/proof (creates the dir). | No | **SAFE** | Delete (the test recreates proof/ when run). | high |
| 35 | `work/menu-flow-fix` | Menu flow fix 10-06: logs, xcresults, simulator DerivedData (changes landed). | 0.6 | 0.6 | 2026-10-06 14:18 | 7206 | 0 | None | No | **SAFE** | Delete. | high |
| 36 | `work/male-micro1h` | HeroBase body/head sculpt history (09-30: Micro1h face densify (rollback base) (superseded). | 0.6 | 0.6 | 2026-10-01 18:43 | 1028 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | Was the declared rollback base for the head; archive scripts + final/ (zip), delete r/ renders. [deletable part: 0.4 du / 0.4 really freed] | medium |
| 37 | `work/playability-fix` | Playability fix 10-06: logs, xcresults, DerivedData (changes landed). | 0.5 | 0.5 | 2026-10-06 15:25 | 5486 | 0 | None | No | **SAFE** | Delete. | high |
| 38 | `work/male-legs-feet-form2` | HeroBase body/head sculpt history (09-30: male legs+feet FORM2 (installed). | 0.4 | 0.4 | 2026-10-02 08:02 | 595 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | Produced the installed legs/feet FORM2; the 5-slit-toe carve still awaits Adnan (MEMORY); archive, delete tmp/. [deletable part: 0.2 du / 0.2 really freed] | medium |
| 39 | `work/female-micro-f2-head` | HeroBase body/head sculpt history (09-30: female head MicroF2 (staged). | 0.3 | 0.3 | 2026-10-02 14:19 | 550 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 40 | `work/male-arms-hands-form2` | HeroBase body/head sculpt history (09-30: male arms+hands FORM2 (installed then superseded). | 0.3 | 0.3 | 2026-10-02 06:54 | 528 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 41 | `work/male-micro1j-head` | HeroBase body/head sculpt history (09-30: Micro1j jaw/neck (superseded). | 0.3 | 0.3 | 2026-10-01 22:59 | 771 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 42 | `work/female-torso-form2` | HeroBase body/head sculpt history (09-30: female torso FORM2 (staged). | 0.3 | 0.3 | 2026-10-02 07:11 | 775 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 43 | `work/standard-tennis-kit-wb` | Earlier staged standard tennis kit variant 10-02. | 0.3 | 0.3 | 2026-10-02 19:18 | 628 | 0 | None (ArtDir parallel prompts mention it) | No (superseded) | **SAFE** | Delete or zip. | medium |
| 44 | `work/female-micro-f1-head` | HeroBase body/head sculpt history (09-30: female head MicroF1 (staged (copy in Unity/Assets/Characters/HeroBase/FemaleHeadF1)). | 0.3 | 0.3 | 2026-10-02 07:48 | 761 | 0 | Unity/Assets/Editor/HeroBaseFemaleHeadProof.cs READS export/HeroBase_Female_Body.fbx (identical copy shipped as Characters/HeroBase/FemaleHeadF1/HeroBase_Female_Body_F1.fbx). | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE+FIX** | Point HeroBaseFemaleHeadProof.cs at the shipped copy (or retire it), then delete. [deletable part: 0.2 du / 0.2 really freed] | medium |
| 45 | `work/male-parallel-torso2` | HeroBase body/head sculpt history (09-30: male torso FORM2 (installed then superseded). | 0.3 | 0.3 | 2026-10-01 23:16 | 622 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 46 | `work/female-micro-f3-head` | HeroBase body/head sculpt history (09-30: female head MicroF3 (staged -> full merge). | 0.3 | 0.3 | 2026-10-02 17:10 | 464 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 47 | `work/male-parallel-legs` | HeroBase body/head sculpt history (09-30: male legs+feet parallel chat 4 (installed then superseded). | 0.3 | 0.3 | 2026-10-01 23:02 | 552 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 48 | `work/male-hands-form3` | HeroBase body/head sculpt history (09-30: male hands FORM3 (installed). | 0.2 | 0.2 | 2026-10-02 07:54 | 515 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | Produced the installed hands FORM3 (hand4.py also reproduces FORM2); archive, delete renders. | medium |
| 49 | `work/male-micro1k-head` | HeroBase body/head sculpt history (09-30: Micro1k jaw/cheek (superseded). | 0.2 | 0.2 | 2026-10-01 23:39 | 731 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 50 | `work/tennis-shoes-fit` | Tennis shoes + socks fitted to the hero feet 10-03; export/StandardKit_{Male,Female}.blend that hero-dressed skinned. | 0.2 | 0.2 | 2026-10-03 12:09 | 305 | 0 | None in code (hero-dressed README cites export/StandardKit_*.blend) | Historic source of the kit before cloth-overhaul | **DECIDE** | Archive or delete; current kit source is cloth-overhaul/export. | medium |
| 51 | `work/standard-tennis-kit-worn` | Standard tennis kit WORN (untucked polo, waistband) 10-02, staged. | 0.2 | 0.2 | 2026-10-02 23:22 | 364 | 0 | None (ArtDir prompts mention it) | No (kit later rebuilt in tennis-shoes-fit -> hero-dressed -> cloth-overhaul) | **DECIDE** | Archive (zip ~0.2 GB) or delete; nothing reads it. | medium |
| 52 | `work/male-micro1e` | HeroBase body/head sculpt history (09-30: Micro1e chin + fingers (installed then superseded). | 0.2 | 0.2 | 2026-10-01 16:37 | 442 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 53 | `work/male-parallel-torso` | HeroBase body/head sculpt history (09-30: male torso parallel chat 2/8 (installed then superseded). | 0.2 | 0.2 | 2026-10-01 22:03 | 352 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 54 | `work/male-parallel-arms` | HeroBase body/head sculpt history (09-30: male arms+hands parallel chat 3 (installed then superseded). | 0.2 | 0.2 | 2026-10-01 22:25 | 403 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 55 | `work/hero-dressed` | DRESS_MATCH_HEROES 10-03: kit skinned to the match rig, kitpose dumps, baselines, Swift proof. | 0.2 | 0.2 | 2026-10-06 11:27 | 311 | 0 | Unity/Assets/Editor/MatchHeroKitTests.cs reads data/kitpose_<sex>/ (Blender reference poses); MatchHeroKitBaseline/Probe/Proof write baseline/, logs/, proof/unity; GolfArcadeTests/DressedHeroSnapshotTests.swift writes proof/swift; main-build/integrate-main worktrees list it untracked. | Kit-pose reference data only here | **KEEP** | Keep (0.2 GB). | high |
| 56 | `work/locker-mirror` | LOCKER_MIRROR 10-04: SceneKit export tooling, Unity golden vertex data, rollback baselines, proof. | 0.2 | 0.2 | 2026-10-06 11:30 | 109 | 0 | GolfArcadeTests/LockerMirrorTests.swift READS data/MatchHero_{Male,Female}_golden.json by default (testShippedRigReproducesUnitysPoses) and writes proof/; LockerMirrorProofTests writes proof/. | Golden fixtures only here (untracked -> missing on CI) | **KEEP** | Keep; better: move data/*_golden.json into GolfArcadeTests (or set VISUAL_GOLDEN_DIR) so CI can run the test. | high |
| 57 | `work/female-parallel-limbs` | HeroBase body/head sculpt history (09-30: female limbs chat 8/8 (staged). | 0.2 | 0.2 | 2026-10-01 22:27 | 544 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 58 | `work/female-hands-fingers` | HeroBase body/head sculpt history (09-30: female hands+fingers 08c (staged -> full merge). | 0.2 | 0.2 | 2026-10-02 14:35 | 413 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 59 | `work/female-torso-form4` | HeroBase body/head sculpt history (09-30: female torso FORM4 (staged). | 0.2 | 0.2 | 2026-10-02 14:15 | 581 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 60 | `work/swift-module-cache` | Swift module cache (10-05). | 0.2 | 0.2 | 2026-10-05 22:35 | 193 | 0 | None (only listed in old git-status snapshots) | No | **SAFE** | Delete (cache; rebuilt on demand). | high |
| 61 | `work/male-micro1i` | HeroBase body/head sculpt history (09-30: Micro1i chin/ear/nose/mouth (superseded). | 0.1 | 0.1 | 2026-10-01 21:24 | 655 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 62 | `work/match-anim-set` | MATCH ANIMS 2/2: Blender files + scripts + run_all.sh that produced the 16 match clips x 2 heroes. | 0.1 | 0.1 | 2026-10-05 17:19 | 148 | 0 | Unity/Assets/Editor/MatchHeroImport.cs Source = ../work/match-anim-set/export/fbx/; GolfArcade CharacterAssets/MatchHero_*.json "source" fields; comments in MatchHeroLook/TennisHeroSetup/TennisCustomization/CharacterModelPreview; MatchHeroClipMapTests/NativeLaunchHeroTests comments. | YES: export/HeroBase_*_MatchAnims.blend (+ src/, scripts/) are the only sources of the shipped match clips (30 shipped FBX identical) | **KEEP** | Keep all (0.15 GB). | high |
| 63 | `work/character-shader` | Character + cloth shader pass 10-03 (TennisCharacter/TennisCloth, rim light) + HERO_DETAIL textures generator. | 0.1 | 0.1 | 2026-10-03 22:52 | 156 | 0 | Unity/Assets/Editor/HeroDetailTextures.cs names work/character-shader/detail/make_textures.py as the generator of shipped Resources/Tennis/HeroDetail textures; CharacterShaderStill.cs / HeroDetailStill.cs write stills here (create dirs). | Yes: detail/make_textures.py + bake_kit.py are the only generator scripts for the shipped HeroDetail maps | **DECIDE** | Keep detail/*.py + NOTE/GATE md (or move the generator into Tools/); stills and logs (~0.15 GB) are disposable. | high |
| 64 | `work/lobby-parity` | LOBBY_PARITY 10-10: re-export native lobby heroes from current Unity heads; run_export.sh, goldens, NOTE with steps. | 0.1 | 0.1 | 2026-10-10 15:09 | 40 | 0 | GolfArcadeTests/VisualGolden/provenance.json "source" = work/lobby-parity/golden/*.json; VisualOverhaulNativeTests.swift tells you to run work/lobby-parity/run_export.sh. | run_export.sh + NOTE are the documented re-export procedure | **KEEP** | Keep (0.13 GB). | high |
| 65 | `work/emotes` | EMOTES + INTROS 10-04: original 6 clips x 2 heroes (scripts, export, films). | 0.1 | 0.1 | 2026-10-05 22:10 | 169 | 0 | Unity/Assets/Editor/EmoteWire.cs FbxSource = work/emotes/export/fbx/ (and writes logs/); EmoteFilm.cs writes film_frames. WARNING: these FBX are OLDER than the installed emotes (emote-fluid rev 37). | Source of the original emote keys (the installed clips are emote-fluid rev 37) | **KEEP** | Keep (0.13 GB). Fix EmoteWire to point at the rev-37 source or retire it, else a re-run reverts the approved emotes. | high |
| 66 | `work/female-arms-form2` | HeroBase body/head sculpt history (09-30: female arms FORM2 (staged -> full merge). | 0.1 | 0.1 | 2026-10-01 23:13 | 198 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 67 | `work/female-full-merge` | HeroBase body/head sculpt history (09-30: female FULL merge (+ colour pass) (staged; basis of the match heroes). | 0.1 | 0.1 | 2026-10-02 23:24 | 173 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | STAGED female FULL merge + colour pass = basis of the female match hero; archive export/ + color/ + GATE_RESULTS, delete the rest. | medium |
| 68 | `work/venue-presence` | Tennis venue presence rebuild 10-02 (PASS): tools/capture.sh, run_gates.sh, before/after code rollback. | 0.1 | 0.1 | 2026-10-02 23:10 | 89 | 0 | None in code | Rollback snapshot only here | **DECIDE** | Small (0.1 GB); archive. | medium |
| 69 | `work/serve-and-feet` | SERVE_AND_FEET 10-03: Serve/Walk/RunForward/RunLeft/RunRight clips. | 0.1 | 0.1 | 2026-10-03 21:34 | 167 | 0 | Unity/Assets/Editor/ServeAndFeetWire.cs reads export/fbx/ + data/clip_table_new.json and is called by MatchHeroImport/MatchHeroBuild; HeroTennisDriver.cs comment cites data/run_blend_model_*.json. | YES for Walk/Run (8 shipped FBX identical). WARNING: its Serve is OLDER than the installed Serve (from serve-dip-jump) | **KEEP** | Keep. Fix the wiring: re-running MatchHeroImport/MatchHeroBuild would copy this stale Serve over the installed one. | high |
| 70 | `work/dressed-look` | Look-only dressed preview 10-03 (kit + racket on the full bodies: 2 blends, sheets, turntables; right-hand finger rig + grip-search method). | 0.1 | 0.1 | 2026-10-03 01:24 | 111 | 0 | None in code | No (installed kit came from hero-dressed / cloth-overhaul) | **DECIDE** | Archive scripts/ + README (grip-search method may be reused), delete export blends/sheets; or delete whole (0.1 GB). | medium |
| 71 | `work/male-parallel-torso3` | HeroBase body/head sculpt history (09-30: male torso FORM3 (installed). | 0.1 | 0.1 | 2026-10-02 08:23 | 743 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **DECIDE** | Produced the installed torso FORM3; archive tools + HANDOFF, delete renders. [deletable part: 0.04 du / 0.04 really freed] | medium |
| 72 | `work/male-micro1g` | HeroBase body/head sculpt history (09-30: Micro1g face lock (superseded). | 0.1 | 0.1 | 2026-10-01 15:37 | 155 | 0 | Root doc GOAL_HeroBase_Micro1h_MaleFaceDensify.md cites final/HeroBase_Male_Silhouette.blend (historical). | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 73 | `work/classic-tennis-racket` | Classic tennis racket build 10-02 (Blender scripts, export incl. unity_stage materials). | 0.1 | 0.1 | 2026-10-02 23:34 | 158 | 0 | Unity/Assets/Editor/MatchHeroBuild.cs RacketMatSource = ../work/classic-tennis-racket/export/unity_stage/Materials/ (input when the hero prefabs are rebuilt). | Yes (racket sources + staged materials) | **KEEP** | Small (0.08 GB). Keep; optionally delete proof/ renders. | high |
| 74 | `work/male-head-1` | HeroBase body/head sculpt history (09-30: male head pass 1 (superseded). | 0.1 | 0.1 | 2026-10-01 09:12 | 151 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 75 | `work/female-torso-form3` | HeroBase body/head sculpt history (09-30: female torso FORM3 (staged). | 0.1 | 0.1 | 2026-10-02 10:14 | 577 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 76 | `work/male-outfit-preview` | HeroBase body/head sculpt history (09-30: male outfit mock-up (preview only) (preview only). | 0.1 | 0.1 | 2026-10-01 15:03 | 111 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 77 | `work/male-sil-fit-4` | HeroBase body/head sculpt history (09-30: male silhouette fit round 4 (superseded). | 0.1 | 0.05 | 2026-10-01 11:16 | 127 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 78 | `work/male-head-2` | HeroBase body/head sculpt history (09-30: male head pass 2 (superseded). | 0.05 | 0.05 | 2026-10-01 10:13 | 62 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 79 | `work/male-micro1f` | HeroBase body/head sculpt history (09-30: Micro1f hand form / Unity skin (superseded). | 0.05 | 0.05 | 2026-10-01 14:54 | 125 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 80 | `work/male-jaw-5` | HeroBase body/head sculpt history (09-30: male jaw/chin experiment (superseded). | 0.04 | 0.04 | 2026-10-01 11:39 | 119 | 0 | Old Micro1-era tools READ it: Tools/HeroBase/{apply,proof,verify,export}_male_form*.py, *_male_silhouette*.py (and Unity/Assets/Editor/HeroBaseMaleFormProof.cs reads male-silhouette/reference-cameras.json, backs up into male-form/baseline/unity). Root docs HANDOFF_Sonnet_MaleHero_ChinFingers.md / GOAL_HeroBase_Micro1e_ChinFingers.md cite male-jaw-5. | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE+FIX** | Delete together with (or after retiring) the obsolete Tools/HeroBase Micro1 scripts and HeroBaseMaleFormProof.cs; otherwise those tools fail if run. | medium |
| 81 | `work/golf-art-exports` | Candidate FBX exports of ArtDir/golf/source blends (export-golf-art.py output) 10-07. | 0.04 | 0.04 | 2026-10-07 09:11 | 24 | 0 | ArtDir/golf/tools/export-golf-art.py + README write here (mkdir); shipped FBX differ (newer exports). | No (ArtDir/golf/source is the authority) | **SAFE** | Delete; regenerated by ArtDir/golf/tools/export-golf-art.py. | high |
| 82 | `work/male-head-3` | HeroBase body/head sculpt history (09-30: male head pass 3 (superseded). | 0.04 | 0.04 | 2026-10-01 10:37 | 88 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 83 | `work/female-parallel-torso` | HeroBase body/head sculpt history (09-30: female torso chat 7/8 (staged, retired). | 0.04 | 0.04 | 2026-10-01 22:29 | 92 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 84 | `work/gameplay-capture` | Two-point tennis capture mp4 written by a PlayMode test 10-03. | 0.03 | 0.03 | 2026-10-03 23:10 | 5 | 0 | Unity/Assets/Tests/PlayMode/TennisSinglePointCaptureTests.cs default POINT_CAPTURE_OUTPUT ../work/gameplay-capture/two_points.mp4 (creates dir). | No | **SAFE** | Delete. | high |
| 85 | `work/hero-base` | HeroBase body/head sculpt history (09-30: first HeroBase build/inspection tooling (superseded). | 0.03 | 0.03 | 2026-09-30 21:25 | 136 | 0 | None | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE** | Delete (superseded intermediate; its result was merged/installed later and the findings are in MEMORY notes). | medium |
| 86 | `work/male-form` | HeroBase body/head sculpt history (09-30: male form fix (Micro1c) (superseded). | 0.01 | 0.01 | 2026-09-30 23:51 | 32 | 0 | Old Micro1-era tools READ it: Tools/HeroBase/{apply,proof,verify,export}_male_form*.py, *_male_silhouette*.py (and Unity/Assets/Editor/HeroBaseMaleFormProof.cs reads male-silhouette/reference-cameras.json, backs up into male-form/baseline/unity). Root docs HANDOFF_Sonnet_MaleHero_ChinFingers.md / GOAL_HeroBase_Micro1e_ChinFingers.md cite male-jaw-5. | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE+FIX** | Delete together with (or after retiring) the obsolete Tools/HeroBase Micro1 scripts and HeroBaseMaleFormProof.cs; otherwise those tools fail if run. | medium |
| 87 | `work/male-form-d` | HeroBase body/head sculpt history (09-30: male form D (Micro1d) (superseded). | 0.01 | 0.01 | 2026-10-01 11:39 | 22 | 0 | Old Micro1-era tools READ it: Tools/HeroBase/{apply,proof,verify,export}_male_form*.py, *_male_silhouette*.py (and Unity/Assets/Editor/HeroBaseMaleFormProof.cs reads male-silhouette/reference-cameras.json, backs up into male-form/baseline/unity). Root docs HANDOFF_Sonnet_MaleHero_ChinFingers.md / GOAL_HeroBase_Micro1e_ChinFingers.md cite male-jaw-5. | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE+FIX** | Delete together with (or after retiring) the obsolete Tools/HeroBase Micro1 scripts and HeroBaseMaleFormProof.cs; otherwise those tools fail if run. | medium |
| 88 | `work/male-silhouette` | HeroBase body/head sculpt history (09-30: male silhouette fit (Micro1) (superseded). | 0.01 | 0.01 | 2026-09-30 22:19 | 34 | 0 | Old Micro1-era tools READ it: Tools/HeroBase/{apply,proof,verify,export}_male_form*.py, *_male_silhouette*.py (and Unity/Assets/Editor/HeroBaseMaleFormProof.cs reads male-silhouette/reference-cameras.json, backs up into male-form/baseline/unity). Root docs HANDOFF_Sonnet_MaleHero_ChinFingers.md / GOAL_HeroBase_Micro1e_ChinFingers.md cite male-jaw-5. | No (current state is in ArtDir/hero/base_lock and the hero exports) | **SAFE+FIX** | Delete together with (or after retiring) the obsolete Tools/HeroBase Micro1 scripts and HeroBaseMaleFormProof.cs; otherwise those tools fail if run. | medium |
| 89 | `work/latency-fix` | Latency fix 10-06: test logs/XML, device logs. | 0.01 | 0.01 | 2026-10-06 16:04 | 42 | 0 | None | No | **SAFE** | Delete. | high |
| 90 | `work/menu-hub` | Walkable plaza menu hub tools: install_unity.py, menu_audit.py, perf/net scripts, previews. | 0.01 | 0.00 | 2026-10-10 18:14 | 18 | 0 | Active hub install/audit tools (newest 10-10 18:14); proof goes to proof/menu-hub. | Install script only here | **KEEP** | Keep (5 MB). | high |
| 91 | `work/controller-flow-fix` | Controller flow fix 10-06: logs, xcresults (changes landed). | 0.00 | 0.00 | 2026-10-06 15:12 | 223 | 0 | None | No | **SAFE** | Delete. | high |
| 92 | `work/timing-point-fix` | Timing point fix 10-06: logs, xcresult, screenshots (changes landed). | 0.00 | 0.00 | 2026-10-06 15:34 | 116 | 0 | None | No | **SAFE** | Delete. | high |
| 93 | `work/new-menus` | NEW_MENUS 10-03 README + DEAD_CODE_PLAN.md (dead-code plan, NOT applied) + 2 MB proof. | 0.00 | 0.00 | 2026-10-03 17:13 | 8 | 0 | None | No | **DECIDE** | Tiny; DEAD_CODE_PLAN.md is useful input for the code auditors (and references refs/backups/pre-dead-code-20261003). Keep until that review is done. | high |
| 94 | `work/(top-level files)` | 13 loose files: emote_*_r2.py/txt one-off emote edit probes (10-05) + multiplayer-baseline-check.log (10-04). | 0.00 | 0.00 | 2026-10-05 13:55 | 13 | 0 | None | No | **SAFE** | Delete. | high |
| 95 | `work/emotes-intro` | Empty job skeleton created 10-03 (only logs/job_start.txt). | 0.00 | 0.00 | 2026-10-03 21:50 | 1 | 0 | None | No | **SAFE** | Delete. | high |
| 96 | `work/lobby-preview` | Empty folder. | 0 | 0 | (empty) | 0 | 0 | None | No | **SAFE** | Delete. | high |
| 97 | `proof/full-visual-overhaul` | Proof renders/films of the Oct 7-8 visual overhaul (character passes, tennis/golf films, audience, gates). | 14.2 | 14.1 | 2026-10-07 18:52 | 35894 | 0 | Written by ~20 Unity editor capture tools (default output dirs, all create dirs); VISUAL_OVERHAUL_LEDGER.md "Proof root"; ArtDir/golf/FINAL_VALIDATION.json + turf physics-comparison.json + audience tools cite files here. | No (evidence only) | **DECIDE** | Keep GATE md/json and stills cited by the ledger/ArtDir manifests; delete film-frame folders (*-film-*, tennis-*-runtime, ~5.6 GB) and superseded character-pass-N folders. | medium |
| 98 | `proof/reference-rebuild` | Proof of the reference rebuild (character blink/cloth/coverage, golf turf/cliffs), incl. 198 JSON files >10 MB. | 6.2 | 6.2 | 2026-10-08 00:17 | 8208 | 0 | VISUAL_OVERHAUL_LEDGER.md; ArtDir/golf/coastal-botany-manifest.json and several ArtDir/golf/proof/*.json cite it. | No (evidence only) | **DECIDE** | Keep cited stills/gates; the >10 MB JSON dumps (~3.9 GB) are disposable. | medium |
| 99 | `proof/menu-hub` | Walkable plaza menu hub proof (concept rounds, examples, phase1-6, phone) + GATE_RESULTS (updated today). | 0.2 | 0.2 | 2026-10-10 18:50 | 123 | 0 | PLAN_MenuHub_WalkableWorld.md: look target proof/menu-hub/concepts/round5_simple/03_simple_evening_plaza.png, phase proofs; Unity/Assets/Scripts/Hub/HubArt.cs palette comment. | No | **KEEP** | Keep (0.2 GB, active). | high |
| 100 | `proof/menu-beta` | Tracked proof for the beta menus commit 14a43331 (GATE_RESULTS.md + 20 stills/logs). | 0.03 | 0.03 | 2026-10-08 22:26 | 21 | 21 | 21 files TRACKED in git. | No | **KEEP** | Keep (29 MB, tracked). | high |
| 101 | `proof/golf-hole-flow` | Golf three courses + hole-out card proof stills (today). | 0.00 | 0.00 | 2026-10-10 16:22 | 4 | 0 | Golf hole-flow job (MEMORY); untracked. | No | **KEEP** | Keep (5 MB); consider tracking with that commit. | medium |

## 2. Entries

### Tracked files in work/ and proof/

**SCR-01: 77 tracked files in work/postcard-refresh/**
- **What it is:** golf + tennis refresh release-gate evidence. 65 files in `release-gates/` (hole07–19 tee/approach/putting PNG + physics.json); `windmill-final/` (hole 20, 5 files); `tennis-release/` (2 files); `GATE_RESULTS.md`, `changed-files.json`, `render-gates.json`; and `split-source/ReferenceHole09.blend/.fbx` plus a manifest.
- **Size:** 120.9 MB on disk. 72 of the files go through LFS; the git blobs are pointers.
- **Evidence:** added in 9283f6a8 (2026-10-08 "Integrate October 8 …"), no local edits. The shipped `Unity/Assets/Resources/Course/Resort/ReferenceHole09.fbx` is byte-identical to `split-source/ReferenceHole09.fbx`. No `ReferenceHole09.blend` exists anywhere outside work/; `blender/scripts/build_reference_hole09.py` is the builder.
- **Dependencies:** `split-source/ReferenceHole09.blend` is the only editable source of a shipped course model. The stills are evidence only.
- **Verdict:** KEEP tracked, high confidence. Better: move `split-source/` to `ArtDir/golf/source/`, where every other course .blend lives. The stills could move to proof/.

**SCR-02: 21 tracked files in proof/menu-beta/**
- **What it is:** `GATE_RESULTS.md` + 18 menu stills + checksums + test-results.txt for commit 14a43331 (beta menus).
- **Size:** 28.5 MB.
- **Dependencies:** none in code. It is the evidence for HEAD's menu work.
- **Verdict:** KEEP tracked, high confidence.

**SCR-03: work/ and proof/ are not gitignored**
- **What it is:** `git status` shows 180 untracked entries under work/ and proof/: every job folder and 70+ loose postcard-refresh files.
- **Evidence:** `.gitignore` has no work/ or proof/ rule. The other worktrees (main-build, integrate-main) also list work/golf-party, hero-dressed, locker-mirror, hero-mainstay and online-lobby as untracked.
- **What breaks if fixed:** nothing. Tracked files stay tracked even after an ignore rule is added.
- **Verdict:** SAFE+FIX, high confidence. Add `/work/` and `/proof/` to `.gitignore`, with `!` exceptions only if new evidence is meant to be committed. This stops jobs accidentally committing GBs.

### Git worktrees (`git worktree list`: 5 entries)

**SCR-04: main worktree (this repo, final-build)**
- **What it is:** the main checkout. About 150 modified and 240 untracked files.
- **Verdict:** KEEP.

**SCR-05: base-main-wt**
- **What it is:** `…/a31568c7-…/scratchpad/base-main-wt`, detached at 9442701e (= main tip), inside another Claude session's scratchpad.
- **Size:** du 1.7 GiB, really freed 1.5 GiB.
- **Dirty state:** 50 files show as modified, but all 50 are byte-identical to HEAD (`hash-object --no-filters` = HEAD blob). That is pure LFS/attribute noise on PNG/TTF files; there is no real work.
- **Caution:** the directory mtime is Oct 10 19:17, so make sure session a31568c7 is finished first.
- **Verdict:** SAFE (`git worktree remove --force <path>`), medium-high confidence.

**SCR-06: emote-fluid-staging**
- **What it is:** `/Users/…/Codex/2026-10-05/read-artdir-hero-base-lock-parallel/work/emote-fluid-staging`, detached at 23cbedab (2026-10-05).
- **Size:** du 15.8 GiB (ArtDir 12.4 GiB, SportsLibrary 2.3 GiB, Unity 0.7 GiB, no Library). Really freed 15.7 GiB.
- **Dirty state:** 12 "modified" files, all identical to HEAD (LFS noise). Nothing in this repo references the path.
- **Why it is safe:** the emote job it staged (revision 37) is installed and approved by Adnan.
- **Verdict:** SAFE (`git worktree remove --force`), high confidence.

**SCR-07: main-build**
- **What it is:** `/Users/…/Codex/2026-10-08/open-8/work/main-build`, branch main @ 9442701e.
- **Size:** du 12.4 GiB, really freed 8.1 GiB (its Unity Library shares blocks).
- **Dirty state:** 19 real code edits, 1 deletion and 5 untracked entries.
  - The online-crash fixes (MultiplayerService, TennisMatch, the tests, `proof/multiplayer/ONLINE_CRASH_AUDIT.md`) are now also committed as local branch `local/2026-10-10-online-crash-fixes` (91c25128, 2026-10-10 18:45). `git diff 91c25128` leaves only two files.
  - Those two are device-signing edits not committed anywhere: `GolfArcade/Unity/SportsInfo.plist` and `GolfArcadeUnity.xcodeproj/project.pbxproj` (DEVELOPMENT_TEAM 9F65U6SU2W, bundle id com.adnanandrew.motionsports, display name "Motion Club Beta").
- **Verdict:** DECIDE, high confidence.
  - Keep until 91c25128 is pushed or merged and the signing edits are copied wherever Adnan wants them.
  - Then `git worktree remove`.
  - The coordinator's KEEP note was right at the time; the fixes are now duplicated on a branch.

**SCR-08: integrate-main**
- **What it is:** `/Users/…/Codex/2026-10-10/integrate-main`, branch integrate/main-2026-10-10 @ 0c322d3e (today 18:54).
- **Size:** du 1.7 GiB.
- **Dirty state:** 51 real modifications (regenerated ArtDir review/runtime screenshots) and 3 untracked work/ folders. The branch has 17 commits on no remote.
- **Verdict:** KEEP (active integration), high confidence.

### Local branches (12)

Columns: merged into final-build / main, last commit, upstream.

- **SCR-09: all-design-mockups.** In final-build yes, in main no. Last commit 2026-09-20. Same as origin/all-design-mockups. → SAFE to delete the local branch (the remote keeps it). High confidence.
- **SCR-10: codex/full-visual-overhaul.** In final-build yes (0 commits outside it). Last commit 10-06. No upstream. → SAFE. High confidence.
- **SCR-11: feature/standard-characters-integration.** In final-build yes. Last commit 09-21. No upstream. → SAFE. High confidence.
- **SCR-12: final-build.** Current branch, in sync with origin/final-build (0 ahead, 0 behind). 126 commits are not in main and main has 50 not in final-build. → KEEP.
- **SCR-13: final-characters.** In final-build yes. Last commit 09-23. Same as origin. → SAFE. High confidence.
- **SCR-14: integrate/main-2026-10-10.** Not merged: 67 commits not in final-build, 17 not in main. Today 18:54. 17 commits are on no remote. Checked out in the integrate-main worktree. → KEEP.
- **SCR-15: local/2026-10-10-final-build-work.** 3 commits not in final-build, on no remote (today 18:43–18:44): "The Plaza", "Golf: three courses …", "Lobby heroes: re-export". This is the only copy of those commits. → KEEP (push or merge it). High confidence.
- **SCR-16: local/2026-10-10-online-crash-fixes.** 1 commit not in main (91c25128), on no remote. Only copy of the crash fixes besides the main-build working tree. → KEEP (push or merge it). High confidence.
- **SCR-17: main.** Default branch, = origin/main. → KEEP.
- **SCR-18: newmapsandmenus.** In final-build yes. Last commit 10-06. 3 commits ahead of origin/newmapsandmenus, but they are inside final-build. Its reflog pins the abandoned 7771ef07 (see SCR-24). The two stashes belong to it by name only. → SAFE. High confidence.
- **SCR-19: tennis-gameplay.** In final-build yes. Last commit 09-22. 1 commit ahead of origin, already in final-build. → SAFE. High confidence.
- **SCR-20: tennisgameplaydone-needtofixcharacters.** In final-build yes. Last commit 09-28. Same as origin. Its reflog pins 7771ef07 (SCR-24). → SAFE. High confidence.

The 7 SAFE branches are all ancestors of final-build. Deleting them removes only labels; no commit is lost.

### Stashes and other refs

**SCR-21: stash@{0}** ("On newmapsandmenus: pre-final-build-merge untracked tennis/lighting", 2026-10-06)
- **What it is:** no tracked changes. The untracked part holds 4 scripts + .meta files (14.5 KB): `GolfGarmentCorrectives.cs`, `HeroLightingProfile.cs`, `TennisEmotes.cs`, `TennisGame.Network.cs`.
- **Evidence:** all 4 now exist in final-build in newer versions.
- **Verdict:** SAFE to drop, medium-high confidence.

**SCR-22: stash@{1}** ("pre-final-build-merge local WIP", 2026-10-06)
- **What it is:** 20 tracked files, +612 / −71 lines.
- **Evidence:** most added lines are already in final-build:
  - Hole.cs 168/173
  - TennisGame.cs 41/42
  - LockerStudio 53/54
  - TennisRules/TossMeter/SportsMultiplayer/SportsRuntime all present
- **Not present:**
  - HoleView.cs postcard material resolver (4/56 lines)
  - GolfGame.cs tutorial hooks (3/19; GolfGame is now split into 14 partial files)
  - CharacterModelPreview.swift (23/48)
  - SportsInfo.plist local-network keys (0/4)
  - project.yml (0/1)
- **Verdict:** DECIDE, medium confidence. Most likely superseded. Have someone eyeball those 5 files before `git stash drop`.

**SCR-23: refs/backups/pre-dead-code-20261003** (2171900f)
- **What it is:** a `git stash create` snapshot of 109 tracked files, taken before a dead-code removal that was never applied (`work/new-menus/DEAD_CODE_PLAN.md`). 88 % of its added lines are in final-build.
- **Verdict:** DECIDE, low value, medium confidence. Delete it together with SCR-22 once someone has looked.

### .git

**SCR-24: .git object packs**
- **Size:** `.git` = 32 GiB. `git count-objects -vH`: 3 packs = 8.56 GiB, 135 loose objects (1.1 MiB), no garbage.
- **What is in the packs:** ≈8.6 GB (8.0 GiB) is blobs reachable only from reflogs (HEAD, newmapsandmenus and tennisgameplaydone-needtofixcharacters). No branch, tag, stash or index reaches them.
  - They come from abandoned commit 7771ef07 (2026-09-28 14:39). It committed raw (non-LFS) mp4/png and was replaced 2 minutes later by 4588fba7 with LFS pointers.
  - Breakdown: ArtDir/screenshots 3.7 GB, ArtDir/anims 2.1 GB (e.g. FullMatch_PlayerPOV_60fps.mp4, 466 MB), ArtDir/score80 1.3 GB, ArtDir/hero 0.9 GB, ArtDir/review 0.5 GB.
- **Verdict:** DECIDE, high confidence.
  - Owner-run `git reflog expire --expire-unreachable=now --all && git gc --prune=now` reclaims ≈8 GiB. It is irreversible but only affects the abandoned commit.
  - Doing nothing: git's 30-day unreachable-reflog default lets auto-gc drop them after about 2026-10-28.

**SCR-25: .git/lfs**
- **Size:** 24 GiB (25.2 GB, 24,166 objects).
- **What is in it:**
  - 22.1 GB is used by some current tip: local or remote branch, stash, or worktree HEAD. The final-build tip alone uses 19.5 GB.
  - 3.07 GB (843 objects) is used only by older commits. These are candidates for an owner-run `git lfs prune` (not run here).
  - 221 tip objects are not present locally (pointer-only).
- **Verdict:** DECIDE, medium confidence. Only about 2.9 GiB is reclaimable.

### Junk files outside work/ and proof/ (excluding .git and Unity/Library)

**SCR-26: Blender autosaves (`*.blend1`)**
- **Size:** 57 files, 1.41 GB. SportsLibrary 0.92 GB (Arenas 362 MB, Blender 344 MB, Customization 177 MB, Clothing 41 MB); ArtDir 0.49 GB (hero 280 MB / 24 files, anims 136 MB, environments 46 MB).
- **Status:** untracked, ignored by `*.blend1`. Nothing reads them.
- **Verdict:** SAFE, high confidence.

**SCR-27: Unity crash dumps (`Unity/mono_crash.*`)**
- **Size:** 45 files, 28 MB. Gitignored.
- **Verdict:** SAFE, high confidence.

**SCR-28: Python caches**
- **Size:** 9 `__pycache__` dirs, 54 `*.pyc`, 3.6 MB, in blender/scripts, Tools/HeroBase, ArtDir/*/tools. Gitignored.
- **Verdict:** SAFE, high confidence.

**SCR-29: `*.log` files**
- 11 tracked proof logs: `ArtDir/hero/kit/golf/tailoring/proof/*.log`, `ArtDir/hero/v5/restore-*.log`, `ArtDir/review/point-clips/*.log`. They are evidence, so KEEP.
- 5 `Unity/Logs/shadercompiler-*.log` files, ignored by `Unity/.gitignore`. SAFE.
- No `*.orig`, `*.rej` or `*.tmp` files were found.

**SCR-30: .DS_Store**
- **What it is:** 1 file at the repo root, gitignored.
- **Verdict:** SAFE.

**SCR-31: junk inside work/ and proof/**
- **What it is:** 267 `__pycache__` dirs and ≈1.1 GiB of `.blend1` files, already counted inside each folder's size. The largest are reference-rebuild 0.42, golf-motion-fit 0.16 and full-visual-overhaul 0.13.
- **Verdict:** SAFE even inside KEEP folders.

### Other findings in my area

**SCR-32: Hero rebuild tools point at stale sources (cross-area: Unity/Assets/Editor)**
- `ServeAndFeetWire.cs` copies `Serve` from `work/serve-and-feet/export/fbx/` (sha eb4a7ff6… / c75f601e…). `MatchHeroImport.Run` and `MatchHeroBuild` call it.
  - The installed `Male/Female_Serve.fbx` (49038bb4… / 6f83909b…) came from `work/serve-dip-jump/export/fbx`.
- `EmoteWire.cs` copies from `work/emotes/export/fbx/`, but the installed emotes are the approved revision-37 files from `work/emote-fluid/candidate/fbx`.
- **Effect:** a hero rebuild would silently revert the Serve and the approved emotes.
- **Implication:** keep all five source folders (match-anim-set, serve-and-feet, serve-dip-jump, emotes, emote-fluid) and repoint the two tools.
- **Verdict:** KEEP + fix, high confidence.

**SCR-33: A CI-visible test reads untracked work/ goldens (cross-area: GolfArcadeTests, .github)**
- `LockerMirrorTests.testShippedRigReproducesUnitysPoses` reads `work/locker-mirror/data/MatchHero_*_golden.json`.
- `testLobbyEmoteTracksMatchLiveUnityAtTenFrames` reads `work/online-lobby/emote_parity/data/*_emote_parity.json` and writes `rotation-report.txt` there.
- Both read these only when `VISUAL_GOLDEN_DIR` is unset, and `.github/workflows/ios.yml` runs `-only-testing:GolfArcadeTests` without that variable. These files are untracked, so the tests cannot pass on CI and fail locally if the folders are deleted.
- **Verdict:** KEEP those two data/ folders. SAFE+FIX: copy the 4 JSONs into `GolfArcadeTests/` (as VisualOverhaulNativeTests already does with VisualGolden/*.lzfse). High confidence.

**SCR-34: Test and tool writers into work/ and proof/ (deletion-safe, they mkdir)**
- **Swift tests (`#filePath`):**
  - MultiplayerTests.testGolfPartyPhoneScreens → `work/golf-party/native-proof`
  - MotionClubReviewTests → `work/golf-motion-fix/proof`
  - DressedHeroSnapshotTests → `work/hero-dressed/proof/swift`
  - MatchHeroLockerTests → `work/hero-mainstay/proof/swift_locker`
  - LockerMirror(Proof)Tests → `work/locker-mirror/proof`, `work/online-lobby/emote_parity`
- **Unity PlayMode tests:**
  - TennisSinglePointCaptureTests → `work/gameplay-capture`
  - NativeLaunchHeroTests → `work/hero-mainstay/proof/lineup`
- **Unity editor tools:** about 20 capture tools default to `proof/full-visual-overhaul/*`. Serve/Emote/Face/Cloth/Hero film and proof tools default to their job folders.
- **Verdict:** nothing breaks if the folders go. High confidence.

**SCR-35: Mounted DMG inside work/**
- **What it is:** `work/cloth-overhaul/toolchain/mounted` (`/dev/disk11s1`, read-only), an exported copy of the Metal toolchain from 10-05. The system's own toolchain is what Xcode uses.
- **Verdict:** SAFE after `hdiutil detach`. Frees about 0.9 GB (the DMG). High confidence.

**SCR-36: Empty or skeleton folders**
- `work/lobby-preview` is empty.
- `work/emotes-intro` is empty subfolders plus `logs/job_start.txt`.
- **Verdict:** SAFE.

**SCR-37: Cross-area note**
- `Unity/work/` (108 MB of check.sh output, untracked, not gitignored) and `Unity/Library/Captures` (5.1 GB) were reported by another auditor. They are the same kind of clutter but are not counted here.

## 3. Totals: reclaimable disk space per verdict

All figures are GiB. "Really freed" is clone-aware, measured with the method in the header. Bucket rows that cover several folders are computed as one combined deletion: blocks shared between two deleted folders are counted once and do come back.

### work/ + proof/

| bucket | folders | du says | really freed |
|---|---|---|---|
| SAFE: delete the whole folder | 51 | 323.9 | 98.4 |
| SAFE parts inside KEEP / DECIDE / SAFE+FIX folders: clones, DerivedData, films, renders, dumps (excluding the two active jobs) | 21 | 249.9 | 131.5 |
| **Recommended now = both rows above, deleted together** | | **573.8** | **232.2** |
| SAFE+FIX: rest of those 6 folders, after the pointer fix | 6 | 1.4 | +0.3 (232.5 cumulative) |
| Later: clone/DerivedData of the active jobs golf-party + character-finish32, once they finish | 2 | 38.2 | 10.1 |
| DECIDE: rest of those folders, if Adnan deletes them whole | 21 | 150.7 whole | +31.6 (264.1 cumulative) |
| KEEP: what stays | 23 | 195.6 | ≈37.9 unique (incl. the 10.1 "later") |
| Everything in work/ + proof/ | 101 | 671.6 | 302.0 |

### Outside work/ and proof/ (my area)

| item | verdict | really freed |
|---|---|---|
| Worktrees base-main-wt + emote-fluid-staging | SAFE | 1.5 + 15.7 = 17.2 |
| Worktree main-build | DECIDE | 8.1 |
| .git: blobs reachable only from reflogs (abandoned commit 7771ef07) | DECIDE | ≈8.0 |
| .git/lfs: objects used only by history | DECIDE | ≈2.9 |
| Junk (*.blend1, mono_crash, *.pyc, .DS_Store) | SAFE | ≈1.3 |

### Grand total (my area)

| verdict | really freed |
|---|---|
| SAFE (incl. SAFE parts inside KEEP/DECIDE folders) | ≈250.7 (232.2 + 17.2 + 1.3) |
| SAFE+FIX | ≈0.3 extra |
| DECIDE | ≈50.6 extra (31.6 + 8.1 + 8.0 + 2.9) |
| KEEP | ≈38 stays in work/ + proof/ (plus the live repo, .git tips, integrate-main) |

- With 16 GiB free today, the SAFE set alone would take the data volume from 99 % to about 70 % used (≈267 GiB free).
- The big wins are the 34 Unity project clones and the Xcode DerivedData folders. Their du size (~400 GiB of clones) overstates the gain about threefold.
- Delete the biggest SAFE groups together; deleting them one by one frees less per step. For example, the 11 clones in serve-walk-arm free 41 GiB alone.
- Re-measure with `df` after the first delete.

### Verdict lists

- **KEEP (23):**
  - character-female33, character-finish32, classic-tennis-racket, cloth-overhaul, emote-fluid, emote-overhaul, emotes, full-visual-overhaul, golf-party, hero-dressed, hero-mainstay
  - lobby-parity, locker-mirror, match-anim-set, menu-hub, online-lobby, postcard-look, postcard-refresh, serve-and-feet, serve-dip-jump
  - proof/golf-hole-flow, proof/menu-beta, proof/menu-hub
- **DECIDE (21):**
  - character-shader, dressed-look, face-match, female-full-merge, golf-look, golf-motion-fit, golf-tailoring
  - male-hands-form3, male-legs-feet-form2, male-micro1h, male-micro1l-head, male-parallel-torso3, male-pelvis-form3
  - new-menus, phone-build-20261006, reference-rebuild, standard-tennis-kit-worn, tennis-shoes-fit, venue-presence
  - proof/full-visual-overhaul, proof/reference-rebuild
- **SAFE+FIX (6):** female-micro-f1-head, male-form, male-form-d, male-jaw-5, male-nape-pelvis-skin, male-silhouette.
- **SAFE (51):**
  - Job logs and builds: controller-flow-fix, gameplay-capture, gameplay-regression, golf-art-exports, golf-course-standard, golf-motion-fix, iphone-current, latency-fix, menu-beta, menu-flow-fix, multiplayer, onboarding, playability-fix, presentation, swift-module-cache, timing-point-fix
  - Serve attempts that were not used: serve-toss-arm, serve-toss-jump, serve-walk-arm
  - Female body history: female-arms-form2, female-hands-fingers, female-micro-f2-head, female-micro-f3-head, female-parallel-limbs, female-parallel-torso, female-torso-form2, female-torso-form3, female-torso-form4, female-torso-form5
  - Male body history: hero-base, male-arms-hands-form2, male-head-1, male-head-2, male-head-3, male-micro1e, male-micro1f, male-micro1g, male-micro1i, male-micro1j-head, male-micro1k-head, male-outfit-preview, male-parallel-arms, male-parallel-legs, male-parallel-torso, male-parallel-torso2, male-pelvis-form2, male-sil-fit-4
  - Other: standard-tennis-kit-wb, emotes-intro, lobby-preview, the 13 loose top-level files in work/

### Where I differ from the coordinator's starting list

- `swift-module-cache` is SAFE. It is a compiler cache and no script or code points at it.
- `dressed-look` is DECIDE (archive), not KEEP. It was a preview only; nothing installed came from it.
- `phone-build-20261006` is DECIDE. Keep `export.py`; its 5.4 GiB DerivedData is SAFE.
- `main-build` is DECIDE, not KEEP. Its crash fixes are now committed on `local/2026-10-10-online-crash-fixes`; only the signing edits are unique.
