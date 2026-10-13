# Unity Resources audit: `Unity/Assets/Resources/**`

Auditor: unity-resources. Date: 2026-10-10. The audit was read-only: nothing in the repo was changed.
Repo: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`, branch `final-build`, HEAD `14a43331`, working tree as found.

## Bottom line

- `Resources` holds **754 files, 2,578 MB on disk**. Of these, 740 are tracked, 13 are untracked and new (`Hub/*`, `Course/flag_1..6.png`), and 1 is gitignored (`build_id.txt`). 565 files are routed to Git LFS.
- Unity copies everything in `Resources` into the phone build. In the last iOS build (Oct 8, `Unity/Builds/iOS`), Resources content took **1,352.5 MB of the 1,522.5 MB** packed player data. That figure is per `Library/LastBuild.buildreport`. My own parse of `Data/resources.assets` matched it to within 0.1 MB per item.
- **Can be removed with no change to the game (SAFE + SAFE+FIX): 28 findings, 239 files, about 378 MB of the 1,522 MB build (25 %).** That includes 82.8 MB of `ArtDirection/Hero01` content that ships only because of one Resources prefab (URES-02).
- **Removable after a decision (DECIDE): 14 findings, 54 files, about 130 MB more (8.5 %).** These are developer switches, rollbacks, fallbacks, and assets that load but are never seen.
- Two **live** items alone take **440 MB** of the build: the invisible tennis gameplay rig with 120 uncompressed animation clips, and four baked polo morph meshes. They are kept, but they are the biggest download-size levers (URES-36 and URES-37).
- Sizes are uncompressed bytes inside the app's `Data` folder. The App Store download is zip-compressed, so the real download shrinks somewhat less than these figures.

## How this was checked

1. **Every `Resources.Load` / `LoadAll` / `LoadAsync` call was resolved.** That is 219 runtime call sites in `Scripts/**`, plus 55 in `Editor/` and `Tests/`. For each one I read the code to resolve the path: constants, string concatenation, switch tables, name arrays, `LoadAll` folders, and environment-variable gates. I also traced whether the call is actually reached. For example, a method may have no callers, or it may return before it loads anything. The resolved table is in `scratchpad/audit/unity-resources/tools/specs.py`.
2. **Reachability was computed by script.**
   - The start set is the code-loadable assets, the 3 build scenes (which hold only one component each), and the `ProjectSettings` references: the Always-Included shaders, and `TennisURP.asset` via QualitySettings.
   - I followed both the shared YAML GUID index and the build's own dependency table, `ResourceManager.m_DependentAssets` in `Builds/iOS/Data/globalgamemanagers`.
   - The YAML index misses FBX material links that the importer creates at import time. These exist only in `Library`. Examples: `TropicalTennisResort.fbx` → `TropicalV3/Materials/*.mat`, `standard_*_kit.fbx` → `KitsV3/Materials`, `island.fbx` → `island.png`. The build table catches them.
3. **Sizes** come from `stat` on disk and from packed build bytes (Oct 8 build report CSV from the unity-assets-other auditor; my parser agrees). LFS status comes from `git check-attr`. Sampled LFS files are real content, not pointers. **Duplicates** were found by SHA-256.
4. **Caveat.** "Live" assumes the top-level game classes are live: TennisGame, GolfGame, HubWorld, MatchHeroLook, HeroGolfer, HoleView, UI. For every loader that decides a verdict below, I checked its call chain by hand. Another auditor checks class reachability in general.

## Folder map (every Resources folder)

Format: files, then build MB, then split by verdict.

| Folder | Files | Disk MB | Build MB | Status (files / build MB) |
|---|---|---|---|---|
| Tennis/KitsTailored | 14 | 1,224.9 | 292.8 | LIVE 9/9.4 · KEEP 4/282.1 (URES-37) · DECIDE 1/1.3 (dup, URES-49) |
| StandardCharacters | 4 | 112.7 | 165.1 | KEEP 2/157.8 (URES-36) · SAFE+FIX 2/7.3 (URES-09) |
| Tennis/Premium | 27 | 73.7 | 160.4 | LIVE 15/55.4 · SAFE+FIX 10/100.8 (URES-01) · SAFE 1/1.8 · DECIDE 1/2.4 |
| Tennis/TropicalV3 | 60 | 193.7 | 140.3 | LIVE 51/122.8 · DECIDE 7/17.5 (URES-39) · tiny KEEP/SAFE |
| Course/Resort | 47 | 129.1 | 104.5 | LIVE 42/94.1 · DECIDE 4/7.6 · SAFE 1/2.8 |
| Course (root) | 55 | 36.2 | 76.3 | LIVE 18/54.8 · DECIDE 2/16.4 · SAFE 13/4.1 · SAFE+FIX 13/1.0 · KEEP 9/0 |
| Hero | 13 | 34.5 | 50.3 | all LIVE (golf hero) |
| Tennis/KitsV3 | 39 | 79.9 | 46.3 | LIVE 6/3.1 (racket) · SAFE+FIX 32/43.2 (URES-04) |
| Tennis/Customization | 28 | 28.4 | 39.5 | LIVE 4 (match + golf hero prefabs; ~76 MB of dependencies) · SAFE 24/39.4 (URES-05) |
| Tennis/Correctives | 5 | 32.4 | 32.4 | DECIDE 5/32.4 (URES-29) |
| Golfer | 13 | 26.3 | 28.9 | LIVE 1 · SAFE 4/10.4 · DECIDE 8/18.4 |
| Golf | 43 | 45.7 | 25.1 | LIVE 34/22.8 · SAFE 9/2.3 |
| Tennis/Characters | 41 | 20.7 | 23.8 | LIVE 2/3.1 · SAFE 28/7.5 · DECIDE 11/13.1 |
| Course/Look | 31 | 25.1 | 21.7 | LIVE 22/8.9 · SAFE 1/9.6 · SAFE+FIX 8/3.1 |
| Tennis/HeroDetail | 41 | 6.8 | 21.2 | LIVE 29/11.8 · SAFE 12/9.4 |
| Club | 27 | 6.2 | 17.6 | LIVE 16/8.9 · SAFE 11/8.7 |
| Tennis/Opponents | 12 | 11.9 | 15.8 | SAFE 12/15.8 |
| Tennis/OriginalSeamBlinkFinish | 2 | 7.5 | 13.5 | LIVE (default face finish) |
| Tennis (root) | 2 | 5.5 | 9.2 | LIVE 1 (StrokeContacts) · SAFE 1/9.2 |
| Tennis/KitsLOD | 2 | 4.9 | 6.3 | LIVE |
| Tennis/Island | 3 | 11.3 | 5.5 | LIVE 1/2.4 · SAFE 2/3.1 |
| Tennis/HeroReference, OriginalSeam, OriginalSeamEyeMaterial, KitsFitted | 8 | 14.3 | 17.5 | DECIDE (env-var switches) |
| Tennis/UI | 26 | 10.3 | 4.9 | LIVE 2 · SAFE 23/4.6 · KEEP 1 |
| Tennis/Performance | 20 | 406.9 | 4.7 | LIVE 18 · DECIDE 2/0.9 |
| Tennis/Hero | 3 | 0.3 | 4.2 (+82.8 deps) | SAFE+FIX (URES-02) |
| Tennis/EnvV4 | 11 | 1.1 | 3.3 | LIVE 2/0.4 · SAFE 9/3.0 |
| Course/Standard | 17 | 4.3 | 2.6 | LIVE |
| Course/Turf | 4 | 3.6 | 2.5 | DECIDE (URES-35) |
| Tennis/GarmentCoverage | 4 | 2.2 | 2.2 | LIVE |
| UI (Icons, Look) | 46 | 1.3 | 1.9 | LIVE |
| Course/Surface | 5 | 2.6 | 1.7 | LIVE 4 · SAFE+FIX 1 |
| Tennis/Crowd | 2 | 2.4 | 1.6 | SAFE |
| Fonts | 13 | 1.5 | 1.5 | LIVE 6 · SAFE 5 · KEEP 2 |
| Tennis/Environment | 1 | 1.8 | 1.3 | SAFE |
| Sky, Course/Backdrop, Tennis/Umpire, Effects, Tennis/Fx, Tennis/Rendering, Course/Standard | — | — | ~4.4 | LIVE |
| Clubs | 8 | 0.6 | 0.8 | LIVE 4 (toon) · SAFE 4 |
| Course/Lava | 10 | 0.7 | 0.6 | LIVE 6 · SAFE+FIX 4 |
| Tennis/Shaders, Course/Shaders, Golf/GolfWater | 40 | 0.3 | 0.8 | LIVE · KEEP (.hlsl) · SAFE+FIX 1 · DECIDE 1 |
| Hub | 7 | 1.6 | 0 (not in Oct 8 build) | KEEP: live but **untracked** (URES-47) |
| BillingMode.json, build_id.txt | 2 | 0 | 0 | SAFE / KEEP |

---

## A. SAFE / SAFE+FIX: shipped in every build, never loaded by the game

### URES-01 Old "hand v3" hand meshes and hand data
- **Paths (10):**
  - `Tennis/Premium/HandRest/{Male,Female}.fbx`
  - `Tennis/Premium/Hands/{Male,Female}.fbx`
  - `Tennis/Premium/HandNormals/{Male,Female}.json`
  - `Tennis/Premium/HandPose/{Male,Female}.{fbx,json}`
- **What it is:** full-body FBX exports and large JSON tables from an earlier hand-anatomy pilot ("hand v3": continuous hands, authored fist poses, normal fields).
- **Size:** 47.0 MB disk / **100.8 MB build**. HandRest and Hands are 21.1 + 16.2 MB each per sex; HandNormals are 14.9 + 11.1 MB. All tracked. The 6 FBX are LFS; the 4 JSON are not.
- **Evidence:**
  - `HeroContinuousHandMesh.Apply` (Hands) has no callers.
  - `HeroHandNormalFields.Prepare` (HandNormals) has no callers.
  - `HeroAuthoredHandPose.ApplyFist` is called at runtime (`HeroGripPolish.cs:28`). It returns before loading anything unless the body mesh name contains "(hand v3 bind)" (`HeroAuthoredHandPose.cs:89`).
  - Only `HeroThumbRigMigration.Prepare` sets that name (`HeroThumbRigMigration.cs:56`), and only `Editor/HeroThumbRigProbe.cs:34,47` calls it.
  - No prefab references any of these files or classes.
- **Depends on it:** only the editor probe `Editor/HeroThumbRigProbe.cs`. It re-imports and loads all four folders (lines 25–27, 127).
- **Verdict: SAFE+FIX.** Delete, or move out of Resources (for example to `Assets/Characters/MatchHeroes/HandV3/`). Then update or delete `HeroThumbRigProbe` and the four dead runtime classes above. The game's grip is unchanged, because the gate already returns false.
- **Confidence:** high.

### URES-02 Legacy Hero01 tennis prefab, plus its kit mask (pulls ArtDirection/Hero01 into the build)
- **Paths (3):**
  - `Tennis/Hero/Hero_01_Tennis.prefab`
  - `Tennis/Hero/HeroV4_KitMask.png`
  - `Tennis/Hero/HeroV4_KitRef.json`
- **What it is:** the pre-October tennis hero ("Hero01 / V4 / V5"), replaced by the match heroes in `Tennis/Customization/PlayerMale|PlayerFemale`.
- **Size:** 0.3 MB disk. The build cost is **87.0 MB**: 4.2 MB for the files themselves plus **82.8 MB of `Assets/ArtDirection/Hero01/**`** (materials, models, RestoreMotion clips, textures) that the prefab references. That 82.8 MB is used by nothing else. All tracked; the PNG is LFS.
- **Evidence:**
  - No runtime code loads `Tennis/Hero/*`. The path constant lives in the editor-only `Editor/LegacyHero01.cs:8`.
  - `HeroV4_KitMask`/`KitRef` are written by `Editor/HeroLockerExport.cs:118–122` and read by nothing at runtime.
  - The unity-assets-other auditor confirms the 67 Hero01 files and 82.8 MB.
- **Depends on it:**
  - **Editor:** `HeroGameplayBuild.cs:8` (writes the prefab), `HeroLockerExport.cs:81,194`, `MatchHeroAB.cs:56`, `HeroV5Rebind.cs:17`, `HeroIntegrityReview.cs:27`, `HeroV6PolishBeforeReview.cs:27`.
  - **Test:** `Tests/PlayMode/HeroV6RetargetTests.cs:22` (Explicit).
- **Verdict: SAFE+FIX.** Move the 3 files out of Resources (for example to `Assets/ArtDirection/Hero01/`). Switch the `LegacyHero01.PrefabPath` users from `Resources.Load` to `AssetDatabase.LoadAssetAtPath`, and change `HeroLockerExport.ResDir`. Fix or delete `HeroV6RetargetTests`.
- **Confidence:** high.

### URES-03 Per-rival opponent bodies
- **Paths (12):** `Tennis/Opponents/{Avatar,AvatarF,Bruno,Dex,Jax,Lina,Milo,Nadia,Rosa,Suki,Tama,Viktor}.fbx`
- **What it is:** old Higgsfield rival bodies from before the match-hero switch.
- **Size:** 11.9 MB disk / 15.8 MB build. Tracked, LFS.
- **Evidence:**
  - `TennisActor.WearBody` is `=> false` (`TennisActor.cs:565`).
  - The `TennisRoster.cs:11` comment says these "are not loaded".
  - Nothing references them by GUID.
- **Depends on it:**
  - `Editor/GolferModelImporter.cs:26`: an import rule keyed on the folder. It becomes a no-op.
  - `Editor/MatchHeroProof.cs:390`: an audit that checks these are *not* loaded. It still passes.
  - The rival names (Milo, Viktor…) in tests are roster keys, not file loads.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-04 Old V3 tennis kits (shirts, shorts, shoes) and their materials and textures
- **Paths (32):**
  - `Tennis/KitsV3/standard_{male,female}_kit.fbx`
  - `Tennis/KitsV3/racket.fbx`
  - `Tennis/KitsV3/Materials/KitV3_000..013.mat` (14)
  - `Tennis/KitsV3/Textures/KitV3_000..014.png` (15)
- **What it is:** the kit dressing for the old piecewise tennis bodies, plus the source racket FBX.
- **Size:** 73.5 MB disk / **43.2 MB build** (15 textures at 2.5 MB each). Tracked. FBX/PNG are LFS; .mat files are not.
  - 10 of the 15 PNGs are byte-identical copies: 002=003=009=010, 005=006=012=013, 004=011.
- **Evidence:**
  - `TennisKitV3.Dress` runs only `if (!SkinnedBody)` (`TennisActor.cs:337`). The tennis rig is always a skinned "V4 Higgs body", so it never runs.
  - `KitV3_014.png` is used by no material.
  - `racket.fbx` is referenced by nothing shipped.
  - **Keep** `RacketGameplay.prefab`, `RacketGameplayMesh.asset`, `KitV3_014/015/016.mat` and `KitV3_015.png`. These are the live racket (`TennisKitV3.cs:42`).
- **Depends on it:** `Editor/TropicalArenaImporter.cs:34–46`. It assigns `KitsV3/Materials/*` to the kit FBX by name, reads `KitsV3/materials.json`, and rebuilds `RacketGameplay` from `racket.fbx`.
- **Verdict: SAFE+FIX.** Delete the kits, their 14 materials and 15 textures. Move `racket.fbx` out of Resources and update the importer's `Kits` path, or accept that the importer can't rebuild the racket. Optional: delete the dead `TennisKitV3.Dress`.
- **Confidence:** high.

### URES-05 Old "customize your player" data (hair meshes, skin masks, golf base FBX)
- **Paths (24) in `Tennis/Customization/`:**
  - `Custom{Bob,Curls,Head,Quiff}.json`, `CustomHead.png`, `CustomHeadMask.png`
  - `HiggsAvatar{,F}_{Color,Mask}.png`
  - `Player{Male,Female}{Color.png,Mask.png,Mask.json,Bob.json,Curls.json,Swept.json}`
  - `Player{Male,Female}Golf.fbx`
- **What it is:** hair and skin data for the old Higgsfield avatar customization, and old golf base bodies.
- **Size:** 28.0 MB disk / **39.4 MB build**. Tracked; half are LFS, and the JSONs are plain git. `Player{Male,Female}{Bob,Swept}.json` are byte-identical male/female copies.
- **Evidence:**
  - `TennisCustomization.Apply` returns at `TennisCustomization.cs:91`, before any load. The tennis rig (`StandardCharacters/standard_*_tennis.fbx`) has no renderer named "Player*" (verified in the FBX and its meta).
  - `AttachBase(..., golf:true)` (which loads `Player*Golf`) is never called.
  - The `Custom*` and `HiggsAvatar*` files in this folder are referenced by no code and no asset.
  - **Keep** `PlayerMale.prefab`, `PlayerFemale.prefab`, `PlayerMaleGolfKit.prefab` and `PlayerFemaleGolfKit.prefab`. These are the live heroes.
- **Depends on it:**
  - `Editor/GolferModelImporter.cs:26` (import rule, no-op).
  - `NativeSportsSession.cs:254` calls `Player.Customize(...)`. That call is already a no-op.
  - `Tests/PlayMode/TennisTutorialPlayTests.cs:32` only checks that the component exists.
- **Verdict: SAFE.** Optional cleanup: delete `TennisCustomization.Apply/AddMesh` and the golf branch of `AttachBase`.
- **Confidence:** high.

### URES-06 Unused character textures for old rivals and the crowd
- **Paths (28) in `Tennis/Characters/`:**
  - `Higgs{Bruno,Dex,Jax,Lina,Milo,Nadia,Rosa,Suki,Tama,Viktor}_{Color,Normal}.png` (20 tiny placeholders)
  - `FaceAtlas_Avatar.png`, `FaceAtlas_AvatarF.png`
  - `Higgs{Male,Female}_Mask.{png,json}`
  - `HiggsAvatar{,F}_Normal.png`
- **What it is:** textures for the URES-03 opponents and for an old crowd-recolor feature.
- **Size:** 1.0 MB disk / **7.5 MB build**. The two mask PNGs are 2.5 MB each in the build. Tracked, mostly LFS.
- **Evidence:**
  - These names are reached only from materials inside `Tennis/Opponents/*.fbx` (URES-03, not loaded), or from `TennisLook.ApplyCrowdPalette` (`TennisLook.cs:128`), which has no callers.
- **Depends on it:** `Editor/TennisCharacterTextureImporter.cs:10` (import rule, no-op).
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-07 Old coastal tennis arena
- **Path (1):** `Tennis/CoastalTennisResort.fbx`
- **What it is:** an earlier tennis venue, replaced by `TropicalV3/TropicalTennisResort`.
- **Size:** 5.5 MB disk / 9.2 MB build. Tracked, LFS.
- **Evidence:** no code, asset or editor reference.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-08 Umpire-call and grade banner pictures
- **Paths (19):**
  - `Tennis/UI/call-{ace,deuce,double-fault,fault,game,match-point,missed,net,out,play,point,second-serve,winner}.png`
  - `Tennis/UI/grade-{ok,good,great,excellent,super,perfect}.png`
- **What it is:** Higgsfield-painted HUD banners that the HUD no longer uses. The calls are drawn as text now.
- **Size:** 9.3 MB disk / 4.2 MB build. Tracked, LFS.
- **Evidence:** no `call-` or `grade-` load anywhere in `Scripts` or `Editor`. The only match is a test that *writes* `call-N.png` screenshots (`TennisPolishPlayTests.cs:404`).
- **Depends on it:** nothing.
- **Verdict: SAFE.** **Keep** `Tennis/UI/racket-map.png` (`TennisHitMap.cs:57`).
- **Confidence:** high.

### URES-09 Old standard golf bodies (test fixtures living in Resources)
- **Paths (2):** `StandardCharacters/standard_{male,female}_golf.fbx`
- **What it is:** pre-hero golf bodies.
- **Size:** 7.3 MB disk / 7.3 MB build. Tracked, LFS.
- **Evidence:** no runtime reference. Golf uses `Hero/golfer_*`, with `Golfer/golfer_*` as fallback.
- **Depends on it:**
  - `Tests/EditMode/StandardGolfGripTests.cs:15`, through `AssetDatabase` on this exact path.
  - `Editor/GolferModelImporter.cs:13` (import rule).
- **Verdict: SAFE+FIX.** Move both to the existing `Assets/Tests/Fixtures/StandardCharacters/` (already LFS-tracked) and update the test path, or delete the test.
- **Confidence:** high.

### URES-10 Golf crowd figures
- **Paths (4):** `Golfer/crowd_{m,f}.fbx`, `Golfer/crowd_{m,f}_clips.json`
- **What it is:** old spectator golfers. They were replaced by `Course/Resort/GolfGallerySpectators`.
- **Size:** 10.4 MB disk / 10.4 MB build. Tracked; FBX are LFS. The clip JSONs are byte-identical to `golfer_*_clips.json`.
- **Evidence:** no reference anywhere. `GolferView.CreateSpectator` has no callers either.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-11 Old tennis crowd models
- **Paths (3):** `Tennis/Crowd/crowd_{male,female}.fbx`, `Tennis/Premium/TennisSpectators.fbx`
- **What it is:** earlier tennis crowd meshes. The live crowds are `TennisPromenadeHero3` and `TennisSeatedHero3`.
- **Size:** 3.2 MB disk / 3.3 MB build. Tracked, LFS.
- **Evidence:** no code or GUID reference.
- **Depends on it:** `Editor/TennisCrowdImporter.cs:12` (import rule, no-op).
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-12 Old island scan textures
- **Paths (2):** `Tennis/Island/Island_Color.png` (6.9 MB), `Tennis/Island/Island_Normal.png`
- **What it is:** the scan atlas for the first tennis island.
- **Size:** 7.7 MB disk / 3.1 MB build. Tracked, LFS.
- **Evidence:**
  - `TennisLook.StyleIsland` returns at `TennisLook.cs:27` whenever `Tennis/Premium/TennisCoast` exists. The code comment says "avoid loading the obsolete scan atlas".
  - `TennisIsland.fbx`'s materials do not use them (checked in both the YAML index and the build dependency table).
  - **Keep** `TennisIsland.fbx`, which is live.
- **Depends on it:** `Editor/TennisCharacterTextureImporter.cs:10` (import rule). These are only needed again if `TennisCoast` is ever removed.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-13 Two sky panoramas named in data that is never read
- **Paths (2):** `Tennis/Environment/SkyPanorama.png`, `Course/Look/Sky_Crater_C.png`
- **What it is:** painted skies for the golf holes.
- **Size:** 2.4 MB disk / **10.9 MB build**. `Sky_Crater_C` is 0.66 MB on disk but **9.6 MB** in the build, so it is imported nearly uncompressed. Tracked, LFS.
- **Evidence:**
  - These names exist only as values of `GolfAtmosphere.Look.SkyTexture` (`GolfAtmosphere.cs:58,75,103`). That field is never read.
  - The sky loader uses `SkyCoastalSmall`/`SkyVolcanic` (`GolfAtmosphere.cs:288`).
  - `CoolPanorama` (line 307) and `CraterFallback` (line 344) have no callers.
- **Depends on it:** nothing.
- **Verdict: SAFE.** Optional: delete the dead field and both functions.
- **Confidence:** high.

### URES-14 Hole "card" pictures
- **Paths (13):** `Course/hole_{07,12..23}_card.jpg`
- **What it is:** per-hole preview pictures.
- **Size:** 1.3 MB disk / 4.1 MB build. Tracked, LFS.
- **Evidence:** `Hole.Picture` (`Hole.cs:137`) is passed to `Hud.SetHole(..., picture, ...)` (`Hud.cs:934`), which ignores the argument. Nothing else loads `_card`.
- **Depends on it:** nothing.
- **Verdict: SAFE.** Optional: remove `Hole.Picture` and the unused parameter.
- **Confidence:** high.

### URES-15 Hole-number flag textures for the old numbering
- **Paths (13):** `Course/flag_7.png`, `Course/flag_12.png` … `Course/flag_23.png`
- **What it is:** flag pictures showing the catalogue hole number.
- **Size:** 2.4 MB disk / 1.0 MB build. Tracked, LFS.
- **Evidence:**
  - The working tree's **uncommitted** `Hole.cs` makes every dealt course number its holes 1–6 (`Ordinal`, `Hole.cs:133,338`). The flag loaders (`HoleView.cs:659`, `ControllerSheet.cs:331`) now ask for `flag_1..6`. Those are new and **untracked**.
  - Holes with `Ordinal == 0` exist only in `Course.Postcards()` (editor look tools and tests) and `Course.Meadow()` (tests).
  - At HEAD (committed code) these 13 files are still live.
- **Depends on it:** nothing in the working tree.
- **Verdict: SAFE+FIX.** First commit `Hole.cs` together with `flag_1..6.png` (URES-47), then delete.
- **Confidence:** high, conditional on that commit.

### URES-16 Course look textures the surface pipeline never asks for
- **Paths (9):**
  - `Course/Look/{Cliff_C,CliffDark_C,CliffDark_N,Rock_C,Masonry_C,Basalt_C,Scrub_C,Scrub_N}.png`
  - `Course/Surface/CliffDark_CE.png`
- **What it is:** rock and scrub textures for the "LK_" golf materials.
- **Size:** 6.4 MB disk / 3.3 MB build. Tracked, LFS.
- **Evidence:**
  - `GolfLook.UseSurfaceShaders` is always true (`GolfLook.cs:17`). Rock albedo comes from `Course/Surface/<Tex>_CE` (`GolfLook.cs:24`), so the `Look/*_C` rock albedos are read only on the dead `BuildLit` rollback.
  - `LK_SCRUB` and `LK_CLIFF_DARK` are re-routed to `LK_ROUGH`/`LK_CLIFF` by `GolfCourseLook.Role` (`GolfCourseLook.cs:42–48`) before `GolfLook.Get` is ever called with them.
- **Depends on it:**
  - Editor gates `Editor/GolfLookSmoke.cs:2289` ("LK_SCRUB" grass check) and `Editor/GolfLookGrassCheck.cs:42`.
  - `Editor/GolfLookTextureImporter.cs` (folder import rule).
- **Verdict: SAFE+FIX.** Delete, and update or retire those two editor gates.
- **Confidence:** medium. This is a long material chain; I traced every caller of `GolfLook.Get`.

### URES-17 Unused full-size coastal sky
- **Path (1):** `Course/Resort/SkyCoastal.png`
- **Size:** 1.4 MB disk / 2.8 MB build. Tracked, LFS.
- **Evidence:** code loads only `SkyCoastalSmall`. The text hits for "SkyCoastal" were all `SkyCoastalSmall`.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-18 Lava textures never used or used only as fallbacks
- **Paths (4):** `Course/Lava/{ash_gravel,lava_cracks}.png` (unused), `Course/Lava/{crater_rock,crater_relief}.png` (fallback only)
- **Size:** 0.45 MB disk / 0.3 MB build. Tracked, LFS.
- **Evidence:**
  - `ash_gravel` and `lava_cracks` are never loaded.
  - `crater_rock` and `crater_relief` are loaded only if `Course/Resort/ResortBasalt_C/_N` were missing (`LavaWorld.cs:64`).
- **Depends on it:** `Editor/LavaTextureBaker.cs:50,61,163` generates all of them.
- **Verdict: SAFE+FIX.** Delete, drop the `?? T(...)` fallbacks, and stop the baker from writing them.
- **Confidence:** high.

### URES-19 Unused club-menu art
- **Paths (11):**
  - `Club/club-{bowling,boxing,dial,football,friends,lesson,whistle}.png`
  - `Club/scene-{cliff,pavilion,stands}.jpg`
  - `Club/menu-backdrop.jpg`
- **What it is:** art from the pre-Oct-3 club menu.
- **Size:** 2.9 MB disk / **8.7 MB build** (each scene is 2.1 MB). Tracked, LFS.
- **Evidence:** `Club.Art` and `Club.Scene` take dynamic names. Every name that is produced (literals, `UnlockToast.ArtFor`, `ClubStage`) is checked. None of these 11 names appears anywhere.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** medium-high, because the names are built at runtime.

### URES-20 Non-toon club pictures
- **Paths (4):** `Clubs/{driver,iron,putter,wedge}.png`
- **Size:** 0.33 MB disk / 0.6 MB build. Tracked, LFS.
- **Evidence:** `ControllerSheet.cs:58` loads only the `*_toon` versions.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-21 Unused fonts (Nunito, Roboto)
- **Paths (9):** `Fonts/Nunito-{Regular,SemiBold,Bold,ExtraBold}.ttf`, `Fonts/Nunito-OFL.txt`, `Tennis/UI/Fonts/Roboto-{Light,Regular,Medium}.ttf`, `Tennis/UI/Fonts/ROBOTO-LICENSE.txt`
- **Size:** 0.9 MB disk / 0.9 MB build. Tracked; the TTFs are LFS.
- **Evidence:** the UI loads only `Fonts/Club/*` (`UiKit.cs:51–54`, `Club.cs:28–35`) and `Tennis/UI/Fonts/Rubik-Bold`. No asset references these fonts.
- **Depends on it:** nothing.
- **Verdict: SAFE.** Remove each license file together with its font.
- **Confidence:** high.

### URES-22 Old environment props for the resort
- **Paths (9):** `Tennis/EnvV4/EnvV4_Shrub{A,B,C}.fbx`, `EnvV4_TreeBroadleaf{A,B}.fbx`, `EnvV4_TreeFlowering.fbx`, `EnvV5_TreeA.fbx`, `EnvV5_TreeA.png`, `EnvV5_TreeA_LOD1.fbx`
- **Size:** 1.0 MB disk / 3.0 MB build. Tracked, LFS.
- **Evidence:**
  - `TennisEnvironmentV4.Spawn` sends every shrub and tree name to `QueuePlant` and returns before loading (`TennisEnvironmentV4.cs:39–46`).
  - The EnvV5 swap on line 48 is therefore unreachable.
  - **Keep** `EnvV4_Palette.png` and `EnvV4_BeachHouse.fbx`, which are live.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-23 Superseded plain seam maps for the tennis kit
- **Paths (12):** `Tennis/HeroDetail/Kit_{Male,Female}_{Top,Bottom,ShoeL,ShoeR,SockL,SockR}.png`. These are the versions *without* `_N`/`_M`.
- **Size:** 1.2 MB disk / **9.4 MB build** (the two Top maps are 2.5 MB each). Tracked, LFS.
- **Evidence:** `MatchHeroLook.ClothSurface` loads a plain map only when the `_N`+`_M` pair is missing (`MatchHeroLook.cs:232–257`). All 12 pairs exist.
- **Depends on it:** `Editor/HeroDetailTextures.cs` (import rules). This would only matter for a future garment shipped without its `_N`/`_M` pair.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-24 Golf cap maps that are never used
- **Paths (4):** `Golf/HeroDetail/Golf_{Male,Female}_Head_{M,N}.png`
- **Size:** 0.07 MB disk / 0.35 MB build. Tracked, LFS.
- **Evidence:** `HeroGolfer.InstallMatchSurfaces` skips `Kit_Head` (`HeroGolfer.cs:168`, "keep the bald silhouette"), so tag "Head" is never requested.
- **Depends on it:** nothing.
- **Verdict: SAFE**, until caps come back.
- **Confidence:** high.

### URES-25 Unused fitted female golf body mesh
- **Path (1):** `Golf/Fitted_Female_Body.asset`
- **Size:** 3.5 MB disk / 1.7 MB build. Tracked; not LFS.
- **Evidence:** `PlayerFemaleGolfKit.prefab` uses `Golf/Covered_Female_Body.asset`. No code or asset references this one.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-26 Single golf-club FBX
- **Paths (4):** `Golf/Equipment/{Driver,Iron,Putter,Wedge}.fbx`
- **Size:** 0.2 MB disk / 0.24 MB build. Tracked, LFS.
- **Evidence:** the runtime loads only `Golf/Equipment/{Male,Female}` (`HeroGolfer.cs:120`).
- **Depends on it:** `Editor/MatchHeroKit.cs:19`, an import-rule path prefix only.
- **Verdict: SAFE.**
- **Confidence:** high.

### URES-27 Proof-only shader
- **Path (1):** `Course/Shaders/PostcardV3Proof.shader`
- **Size:** 7.8 KB build. Tracked.
- **Evidence:** used only by `Editor/PostcardV3FrameProof.cs` and `Editor/PostcardV3SplitOutline.cs`.
- **Depends on it:** those two editor tools.
- **Verdict: SAFE+FIX.** Move it to a non-Resources folder; `Shader.Find` still works in the editor.
- **Confidence:** high.

### URES-48 Leftover metadata
- **Paths (2):** `BillingMode.json`, `Tennis/TropicalV3/export-report.json`
- **Size:** about 0.
- **Evidence:**
  - `BillingMode.json` is written by Unity In-App Purchasing. That package is **not installed** (not in `Packages/manifest.json` or `Library/PackageCache`), and no code reads it.
  - `export-report.json` is read by nothing.
- **Depends on it:** nothing.
- **Verdict: SAFE.**
- **Confidence:** high.

---

## B. DECIDE: reachable only via developer switches, fallbacks, or a code change

### URES-28 Legacy V4 golfer (fallback figure)
- **Paths (8):** `Golfer/golfer_{m,f}.fbx`, `Golfer/golfer_{m,f}_clips.json`, `Golfer/higgs_{m,f}_color.png`, `Golfer/Look/FaceAtlas.png`, `Golfer/Look/knit_detail.png`
- **Size:** 15.8 MB disk / 18.4 MB build. Tracked; the FBX/PNG are LFS. `Golfer/Look/FaceAtlas.png` is a byte-identical copy of `Tennis/Characters/FaceAtlas.png`.
- **Evidence:**
  - `GolferStyle.UseHero` is true whenever `Hero/golfer_m` exists (`GolferStyle.cs:198`).
  - The legacy branch (`GolferView.cs:326,402,426,149,253`) runs only if the Hero is missing.
- **Depends on it:** `Tests/PlayMode/HeroGolferTests.cs:50` (`TheV4GolferForComparison`) forces it on purpose as a yardstick. `Golfer/Look/clay_matcap.png` stays live.
- **Verdict: DECIDE.** If the V4 yardstick test is no longer wanted, delete it and these 8 files (SAFE+FIX).
- **Confidence:** high.

### URES-29 Sleeve-fit and underarm corrective tables for the old shirt
- **Paths (5):** `Tennis/Correctives/SleeveFit_{Male,Female}.json`, `UnderarmTrace_Female.json`, `UnderarmGusset_Female_{Full,LOD}.json`
- **Size:** 32.4 MB disk / **32.4 MB build**. Tracked; plain git, not LFS.
- **Evidence:**
  - `HeroGarmentPoseCorrectives`, `HeroGarmentLocalFit` and `HeroGarmentGusset` run only for a `Kit_Top` that is not the tailored polo (`HeroGarmentTailoring.cs:16`).
  - By default `MatchHeroLook.cs:351–354` swaps in the tailored polo and renames it "TailoredPolo32". So they are only reached with env `VISUAL_TAILORED_POLO=0` / `VISUAL_TAILORED_FEMALE_POLO=0`.
- **Depends on it:** that rollback switch.
- **Verdict: DECIDE.** Drop them if the pre-tailoring shirt is retired.
- **Confidence:** high.

### URES-30 Pre-tailoring fitted shirts
- **Paths (2):** `Tennis/KitsFitted/{Male,Female}.fbx`
- **Size:** 2.2 MB disk / 2.7 MB build. Tracked, LFS.
- **Evidence:** the same rollback switch (`MatchHeroLook.cs:351`).
- **Depends on it:** that switch.
- **Verdict: DECIDE.** Retire it together with URES-29.
- **Confidence:** high.

### URES-31 Face-finish review variants
- **Paths (4):** `Tennis/OriginalSeam/{Male,Female}.fbx`, `Tennis/OriginalSeamEyeMaterial/{Male,Female}.fbx`
- **Size:** 7.8 MB disk / 9.6 MB build. Tracked, LFS.
- **Evidence:** the default is `Tennis/OriginalSeamBlinkFinish/` (live). The other two folders load only with env `VISUAL_ORIGINAL_BLINK=0` (`HeroOriginalSeamAnatomy.cs:54–56`).
- **Depends on it:** that review switch.
- **Verdict: DECIDE.**
- **Confidence:** high.

### URES-32 Reference-head pilot
- **Paths (2):** `Tennis/HeroReference/{Male,Female}.fbx`
- **Size:** 4.3 MB disk / 5.2 MB build. Tracked, LFS.
- **Evidence:** loads only with env `VISUAL_REFERENCE_HEAD=1` (`HeroReferenceAnatomy.cs:13`).
- **Depends on it:** `Editor/HeroReferenceImportAudit.cs`.
- **Verdict: DECIDE.**
- **Confidence:** high.

### URES-33 Old hole-12 cliffs
- **Path (1):** `Course/Resort/Coastal12_Cliffs.fbx`
- **Size:** 4.1 MB disk / 6.5 MB build. Tracked, LFS.
- **Evidence:** loads only with env `VISUAL_COASTAL_REFERENCE=0` (`GolfCoastalCliff12.cs:21–22`). The default is `ReferenceCliffs12`.
- **Depends on it:** that rollback switch.
- **Verdict: DECIDE.**
- **Confidence:** high.

### URES-34 Original hole 9 and hole 12 models, loaded and then thrown away
- **Paths (2):** `Course/hole_09.fbx`, `Course/hole_12.fbx`
- **Size:** 8.1 MB disk / **16.4 MB build**. Tracked, LFS.
- **Evidence:**
  - `HoleView.cs:58` loads `hole_09`/`hole_12`.
  - `GolfCoastalComposition.Model` (`GolfCoastalComposition.cs:24–38`) then returns `ReferenceHole09`/`12` instead, without using the original. So the game pays the load time and the download for nothing, unless `VISUAL_COASTAL_REFERENCE=0`.
- **Depends on it:**
  - **Editor:** `GolfLookSmoke`, `PostcardImportCheck`, `GolfPlantSwayCheck` mesh gates (8/9/10).
  - **Test:** `Tests/PlayMode/GolfCourseStandardPlayTests.cs:46`.
- **Verdict: DECIDE.** If the rollback is retired, move them out of Resources for the tools and skip the load in `HoleView` for 9 and 12.
- **Confidence:** high.

### URES-35 Turf detail tiles (fallback only)
- **Paths (4):** `Course/Turf/{fairway,green,rough,sand}.png`
- **Size:** 3.6 MB disk / 2.5 MB build. Tracked, LFS.
- **Evidence:** `HoleView.TurfMat` (`HoleView.cs:703`) is reached only when `GolfCourseLook` is off. Only `GolfCourseStandardPlayTests.cs:28` turns it off.
- **Depends on it:** that test.
- **Verdict: DECIDE.**
- **Confidence:** medium-high.

### URES-38 Skin and face textures loaded for the *invisible* tennis rig, and a recolor whose result is thrown away
- **Paths (11):**
  - `Tennis/Characters/Higgs{Male,Female}_{Color,Normal}.png`
  - `Tennis/Characters/FaceAtlas.png`
  - `Tennis/Characters/HiggsAvatar{,F}_Color.png`
  - `Tennis/Characters/HiggsAvatar{,F}_Mask.{png,json}`
- **Size:** 15.8 MB disk / 13.1 MB build. Tracked.
  - `HiggsAvatar_Color` is byte-identical to `HiggsMale_Color`, and `HiggsAvatarF_Color` to `HiggsFemale_Color`. That is 2 × 2.5 MB of pure duplicates.
- **Evidence:**
  - `TennisActor.Build` restyles the hidden gameplay body (`TennisActor.cs:261` → `TennisLook.PrepareCharacter`). `TennisHeroSetup.cs:51` then disables all its renderers.
  - `TennisGame.ApplyOutfit` → `TennisActor.WearKit` → `TennisLook.RecolorKit("Avatar")` blits a recolored texture. It applies the result to materials named "Higgs Avatar …", and none exist (`TennisActor.cs:218`).
  - `HiggsUmpire_*` stays live, because the umpire is visible.
- **Depends on it:** these loaders.
- **Verdict: DECIDE.** This is a code change: skip material prep for the hidden rig and drop the `RecolorKit` call. After that, all 11 files are dead, and the rig also stops doing GPU work every match.
- **Confidence:** high.

### URES-39 Arena textures for parts that are hidden in every venue
- **Paths (7):** `Tennis/TropicalV3/Textures/TropicalV3_0{14,15,18,26,28,30,34}.png`
- **Size:** 43.5 MB disk / **17.5 MB build**. Tracked, LFS.
- **Evidence:**
  - The resort disables these renderers and replaces them: `TennisEnvironmentV4.cs:137–160`, `TennisVenueArt.cs:155–166`.
  - The sky and volcano venues keep only the court kit (`TennisVenueBuilder.cs:33–34`).
  - The textures are still referenced by the arena FBX materials, so they load and ship.
- **Depends on it:** `TropicalTennisResort.fbx` (live).
- **Verdict: DECIDE.** Re-export the arena without those parts. Their meshes inside the 85 MB FBX would also go.
- **Confidence:** medium-high.

### URES-40 Cliff scan textures that are loaded, then overwritten
- **Paths (2):** `Course/Resort/CoastalCliffScan_C.jpg`, `Course/Resort/CoastalCliffScan_N.png`
- **Size:** 28.0 MB disk / 0.7 MB build. Tracked, LFS.
- **Evidence:** loaded at `GolfCoastalCliff12.cs:83–84`, then replaced by `ReferenceCliff02_*` on the default path (lines 95–98).
- **Depends on it:** the `VISUAL_COASTAL_REFERENCE=0` path only.
- **Verdict: DECIDE.** Small build cost, large repo cost.
- **Confidence:** high.

### URES-41 Forehand clip that is loaded, then replaced
- **Paths (2):** `Tennis/Performance/{Male,Female}_ForehandPolished.anim`
- **Size:** 22.0 MB disk (YAML, not LFS) / 0.9 MB build. Tracked.
- **Evidence:** `HeroTennisDriver.cs:227` puts it in the Forehand slot, and lines 228–231 immediately replace it with `*_ForehandArmCorrected` (`TENNIS_ARM_REPAIR` is on by default).
- **Depends on it:** only the `TENNIS_ARM_REPAIR=0` path.
- **Verdict: DECIDE.**
- **Confidence:** high.

### URES-49 Byte-identical duplicates of live files
- **Paths (3):**
  - `Tennis/Premium/TennisResortPalmFinish1.fbx`, a copy of `Course/Resort/GolfTropicalCanopy22.fbx`
  - `Tennis/KitsTailored/FemaleSkirtDistance.fbx`, a copy of `FemaleSkirt.fbx`
  - `Course/Resort/CoastalBotanyMaps/SaplingNeedles_N.png`, a copy of `FirNeedles_N.png`
- **Size:** 3.1 MB disk / 4.1 MB build. Tracked, LFS.
- **Evidence:** identical SHA-256. Unity ships each copy separately.
- **Depends on it:** each copy is loaded by name (`TennisVenueArt.cs:299`, `HeroGarmentLOD.cs:67`, `GolfCoastalBotany.cs:77`).
- **Verdict: DECIDE.** Point each loader at one file. Also listed elsewhere: Higgs duplicates (URES-38), KitsV3 textures (URES-04), FaceAtlas (URES-28), Bob/Swept JSON (URES-05).
- **Confidence:** high.

### URES-50 Shader with no runtime user, shipped through Always-Included
- **Path (1):** `Tennis/Shaders/TennisSky.shader`
- **Size:** 8.6 KB build. Tracked.
- **Evidence:** used only by `Editor/GolfLookSmoke.cs:83`. It is in GraphicsSettings Always-Included because `Editor/ProjectSetup.cs:51 RuntimeShaders` keeps re-adding it.
- **Depends on it:** that editor tool.
- **Verdict: DECIDE.** Owner: the settings auditor.
- **Confidence:** high.

---

## C. KEEP: loaded (or looks dead but is needed)

### URES-36 Invisible tennis gameplay rig: KEEP, but it is the second-biggest download item
- **Paths (2):** `StandardCharacters/standard_{male,female}_tennis.fbx`
- **Size:** 105.4 MB disk / **157.8 MB build**. Tracked, LFS.
- **Evidence:**
  - Live: `TennisActor.cs:245,302` loads the body and `LoadAll`s its clips to drive the hidden gameplay rig (racket sockets, contact timing).
  - In the build, **146 MB is 120 animation clips** (60 per sex) imported with `animationCompression: 0` (Off).
  - The 11.6 MB of meshes are never shown (`TennisHeroSetup.cs:51` disables them).
- **Recommendation:** turn on clip compression, keep only the clips the rig actually plays, or replace the body with a skeleton-only rig. This could save 100+ MB. It needs gameplay re-verification.
- **Confidence:** high.

### URES-37 Baked polo morph meshes: KEEP, the biggest download item
- **Paths (4):** `Tennis/KitsTailored/{Male,Female}Polo{Full,Distance}.asset`
- **Size:** **1,216 MB disk** (YAML, LFS) / **282.1 MB build**. Tracked.
- **Evidence:**
  - Required: `TailoredPoloDeformation.cs:131` returns these. The `*Controls.json` files carry no vertex data (`vertices: 0`), so the runtime cannot rebuild the meshes.
  - Each mesh has 88 morph frames × 22,886 vertices.
- **Recommendation:** fewer frames, sparse deltas, or let the distance LOD reuse fewer frames. Each 88-frame mesh costs about 80 MB in the build.
- **Confidence:** high.

### URES-42 Shader-variant "keeper" materials: look dead, KEEP
- **Paths (3):** `Course/GolfBotanicalInstancing.mat`, `Course/GolfCoastalInstancing.mat`, `Course/GolfLookVariants.mat`
- **Size:** 0.01 MB.
- **Evidence:**
  - Nothing loads them. They exist so the build keeps the GPU-instancing variants of `GolfBotanical`/`GolfCoastalFoliage` (`m_EnableInstancingVariants: 1`) for runtime materials created with `enableInstancing = true`.
  - `GolfLookVariants` keeps a URP Lit variant (checked by `Editor/GolfLookGrassCheck.cs:21–77`). It now serves only the `BuildLit` rollback, so it could be retired with URES-16.
- **Confidence:** high.

### URES-43 Shader include files: KEEP
- **Paths (5):** `Course/Shaders/{GolfSurface,GolfTurfField}.hlsl`, `Tennis/Shaders/{HeroCloth,HeroFaceDepth,HeroLighting}.hlsl`
- **Size:** 0 build. They are compiled into the shaders, not shipped as files.
- **Depends on it:** shaders `#include` them by relative path. Moving them breaks compilation.

### URES-44 Build stamp: KEEP
- **Path:** `build_id.txt`. Generated and gitignored (`.gitignore:23`).
- **Evidence:** read by `GolfGame.cs:319`.

### URES-45 Licenses of live fonts: KEEP
- **Paths:** `Fonts/Club/OFL-{Bricolage,Rubik}.txt`, `Tennis/UI/Fonts/RUBIK-LICENSE.txt`
- **Size:** 13 KB.
- **Reason:** the OFL asks that the license travel with the font.

### URES-46 Importer manifests: KEEP
- **Paths:** `Tennis/TropicalV3/materials.json`, `Tennis/KitsV3/materials.json`
- **Size:** 14 KB in build.
- **Evidence:** read from disk by `Editor/TropicalArenaImporter.cs:38,75`. Moving them requires editing the importer.

### URES-47 Live but untracked: KEEP and commit
- **Paths (13):** `Hub/HubGlitch.mat`, `Hub/Previews/{golf_cliffside,golf_magma,golf_wildisles,tennis_resort,tennis_skyscraper,tennis_volcano}.mp4`, `Course/flag_1..6.png`
- **Size:** 2.4 MB disk. Not in the Oct 8 build.
- **Evidence:** loaded by `HubBayScreen.cs:31–56` (all 6 bay clips are mapped) and by the flag loaders (URES-15).
- **Action:** they must be committed together with the untracked `Scripts/Hub/*.cs`, `Scenes/Hub.unity` and the modified `Hole.cs`, or a clean checkout loses the plaza screens and hole flags. The 12 binaries will go to LFS by the `*.png`/`*.mp4` rules.

### Everything else (428 files, 834 MB disk, 487 MB build): live, no finding
- Main live sets:
  - Tennis: TropicalV3 arena plus all 35 materials and the other 15 textures, match heroes (`Tennis/Customization/Player*.prefab`), tailored FBX and controls, KitsLOD, OriginalSeamBlinkFinish, Premium venue kits/crowds/skies, Performance clips, HeroDetail `_N`/`_M` maps, GarmentCoverage, Umpire, Fx, UI racket-map, Rubik-Bold font, all Shaders/Rendering.
  - Golf: `Hero/*` (golf hero plus hair cards), Golf meshes, correctives and HeroDetail, Course holes, Resort, Standard, Look, Surface and Lava sets used by the surface pipeline.
  - Menus: Club live art, fonts and sounds, UI Icons and Look pictures, Clubs toon, Effects, Sky, Backdrop.

---

## Cross-area links

1. **unity-assets-other:** `Assets/ArtDirection/Hero01/**` (67 files, 82.8 MB packed) ships *only* because `Resources/Tennis/Hero/Hero_01_Tennis.prefab` references it (URES-02). Moving that prefab out of Resources removes it from the build.
2. **unity-code (dead loaders):**
   - `HeroContinuousHandMesh` and `HeroHandNormalFields` have no callers.
   - `HeroThumbRigMigration` is reached only by `Editor/HeroThumbRigProbe`.
   - `HeroAuthoredHandPose`'s loader is gated off (URES-01).
   - `TennisCustomization.Apply/AddMesh` and the golf `AttachBase` are dead (URES-05).
   - `TennisLook.ApplyCrowdPalette` is dead (URES-06).
   - `TennisLook.RecolorKit` via `WearKit` produces nothing visible (URES-38).
   - `TennisKitV3.Dress` is unreachable (URES-04).
   - `GolferView.CreateSpectator` has no callers.
   - `GolfAtmosphere.SkyTexture`/`CoolPanorama`/`CraterFallback` are dead (URES-13).
   - The `Hud.SetHole` picture parameter is ignored (URES-14).
   - The `TennisEnvironmentV4` EnvV5 swap is unreachable (URES-22).
   - `TennisAnnouncer.cs:52` runs `LoadAll("Tennis/Announcer")`, and **that folder does not exist**, so the announcer has no clips.
   - `HoleView` loads `hole_09`/`hole_12` and throws them away (URES-34).
3. **Editor tools that read Resources by path** (they need an edit if files move): HeroThumbRigProbe; HeroGameplayBuild, HeroLockerExport, MatchHeroAB, HeroV5Rebind, HeroIntegrityReview, HeroV6PolishBeforeReview (Hero01); TropicalArenaImporter; LavaTextureBaker; GolfLookSmoke and GolfLookGrassCheck; PostcardV3FrameProof and PostcardV3SplitOutline. Import-rule postprocessors keyed on Resources folders: GolferModelImporter, TennisCrowdImporter, TennisCharacterTextureImporter, HeroDetailTextures, GolfLookTextureImporter, MatchHeroKit.Owns.
4. **tests-ci:**
   - Test-only users of shipped Resources: `HeroV6RetargetTests` (Hero01), `StandardGolfGripTests` (standard golf FBX), `HeroGolferTests.TheV4GolferForComparison` (legacy golfer), `GolfCourseStandardPlayTests` (Turf and hole_09/12).
   - Packages also ship tiny Resources into the player: URP `DebugUICanvas`, and Performance-Testing `PerformanceTestRunInfo`/`Settings`.
5. **settings:** `TennisSky.shader` is Always-Included with no runtime user (URES-50). `BillingMode.json` is a leftover of the removed IAP package.
6. **swift-app / unity-code:** the 13 live untracked Resources files (URES-47) must land in the same commit as the Hub code and `Hole.cs`. Flags 7 and 12–23 become dead only after that commit (URES-15).
7. **Shared GUID index caveat (all Unity auditors):** `unity_index/refs_out.json` misses FBX material links made at import time (Library only), for example TropicalV3 and KitsV3 materials and `island.png`. Use the build report (`unity-assets-other/build_report_packed_sizes.csv`) or `m_DependentAssets` to confirm before deleting materials or textures beside an FBX.
8. **Repo hygiene:** 41 Resources files over 1 MB (541 MB) are not in LFS. These include `Tennis/Performance/*.anim` (407 MB of YAML that packs to 4.7 MB), `Golf/*.asset`, and the Correctives and Hand JSON files.

---

## Summary

| Verdict | Findings | Files | Disk | Oct 8 iOS build (packed) |
|---|---|---|---|---|
| SAFE | 20 (URES-03, 05–08, 10–14, 17, 19–26, 48) | 165 | 92.3 MB | **135.6 MB** |
| SAFE+FIX | 8 (URES-01, 02, 04, 09, 15, 16, 18, 27) | 74 | 137.4 MB | **242.9 MB** (160.0 own + 82.8 ArtDirection/Hero01 + 0.2 other deps) |
| DECIDE | 14 (URES-28–35, 38–41, 49, 50) | 54 | 190.6 MB | 130.0 MB (possible) |
| KEEP (listed) | 8 (URES-36, 37, 42–47) | 33 | 1,323.8 MB | 439.9 MB (URES-36 + 37 are 439.9 of it) |
| Live, no finding | — | 428 | 834.2 MB | 486.8 MB |
| **All Resources** | | **754** | **2,578 MB** | **1,352.5 MB own** (+ dependencies) of 1,522.5 MB total |

**Build-size saving**

| | Packed MB | Share of 1,522.5 MB |
|---|---|---|
| Certain (SAFE + SAFE+FIX) | **≈ 378.5** | ≈ 25 % |
| Plus DECIDE items | ≈ 130 more, ≈ 508 total | ≈ 33 % total |
| Optimizing the two live giants (URES-36/37) | could add another 100–250 | — |

Working files: `scratchpad/audit/unity-resources/`. Key files there:
- `classified.json`: every file with its status, loader and referrers.
- `group_savings.json`: per finding.
- `tools/specs.py`: resolved load table.
- `build_sizes.json` / `build_edges.json`: my own parse of the iOS build.
- `dupes.json`.
