# Audit: app resources (non-code files under `GolfArcade/`)

Read-only audit, 2026-10-10. Branch `final-build`, HEAD `14a43331`, judged on the **working tree on disk** (it includes other sessions' uncommitted edits).

## Scope and method

- **Scope:** all 409 non-code files under `GolfArcade/`:
  - `Unity/CharacterAssets`: 313 files
  - `Unity/MenuArt`: 57
  - `Unity/MenuVideo`: 22
  - `Unity/ClubEnvironments`: 8
  - `Unity/Fonts`: 4
  - `Unity/MenuSounds`: 3
  - `Unity/SportsInfo.plist`
  - `Multiplayer/GolfArcade.entitlements`

  This branch has no `*.xcassets`, app icon or colour sets (see O3).
- **Git state:**
  - All 409 files are tracked. None are untracked or ignored.
  - 67 are modified and uncommitted: the 10-10 lobby re-export of `MatchHero_*`, `MatchHero_*_Distance*` and `GolfKitHero_*`, plus `MatchHero_OriginalEye_RelativeSclera.png`.
  - No LFS pointer files are in the working tree; every file holds its real content.
- **Method:**
  1. Enumerated every resource file.
  2. Read every loader in `GolfArcade/**/*.swift` and `*.mm`. The loaders are `Bundle.main.url(forResource:)`, `UIImage(named:)`, `SKTexture(imageNamed:)`, `CTFontManagerRegisterFontsForURL`, `AVAudioPlayer`, `AVPlayerItem`, `MatchHeroData.locate`/`data`/`asset`, `MatchHeroSurfaces.image` (PNG) and `.compressed` (KTX), `ClubEnvironmentAsset.load` and `NativeGolfEquipment.asset`.
  3. Expanded every string-built name to the concrete names it can produce.
  4. Parsed the six live hero manifests and replayed the material code in Python (`scratchpad/audit/app-resources/tools/*.py`) to get the exact set of maps the app loads.
  5. Traced each loader's call sites back to `SportsHome` (`GolfArcade/App/GolfArcadeApp.swift`).
  6. Checked tests, Unity editor exporters, `project.yml`, both generated `.pbxproj` files and `.gitattributes`.
  7. Cross-checked the latest device build in DerivedData: `GolfArcadeUnity-dluvfok…/Build/Products/Debug-iphoneos/GolfArcade.app`, built Oct 9 18:19, 2.3 GB. It physically contains every "bundled" file below and none of the "excluded" ones.

### Global notes (apply to every entry)

- **G1. Deleting a bundled file needs an XcodeGen regeneration.** Both generated projects, `GolfArcade.xcodeproj` and `GolfArcadeUnity.xcodeproj` (generated 10-08, tracked), list every bundled resource file one by one (four pbxproj lines each). Delete a file without regenerating both projects and the build fails with "Build input file cannot be found". Excluded files (`MenuVideo`, `Hero*`, `Player*`, `Higgs*`, `Custom*`) are not in either pbxproj, so deleting them needs no project change.
- **G2. Git LFS.**
  - LFS: png, jpg, mp4, wav, ttf, lzfse, mask, bin.
  - Plain git blobs: json, **ktx**, txt, plist. This includes the 13–18 MB `Club_*.json` files and all KTX textures.
  - `MenuVideo/*` is also stored as plain blobs, despite the `*.mp4`/`*.jpg` LFS rules.
  - Deleting a file does not shrink git history.
- **G3. What happens if a file is missing at runtime.**
  - Every MenuArt, Font and Sound loader has a fallback: gradient, SF Symbol, system font or silence. A wrong "unused" call in MenuArt therefore causes a visual fallback, never a crash.
  - Hero data is different. `MatchHeroSurfaces.garment` calls `fatalError("Incomplete garment maps")` when a KTX referenced by a submesh with `useGarmentMaps = 1` is missing (`MatchHeroSurfaces.swift:582`).
  - `MatchHeroData.asset` returns nil, so no hero is shown, when a manifest, mesh or rig is missing.
- **G4. Sizes.** Repo sizes are raw file bytes. Xcode's PNG processing makes several PNGs larger in the device build (`island-intro`/`island-terrace` 4.7 → 8.2 MB, `club-locker` 2.2 → 4.3 MB, portraits 6.9 → 9.3 MB). The Oct 9 build carries about **255 MB** of the files flagged below.

### Who produces each CharacterAssets family

| Family | Producer today | Status |
|---|---|---|
| `MatchHero_<Sex>.json/.lzfse/_Rig.lzfse/_Swing_00-11.lzfse` | `Unity/Assets/Editor/MatchHeroLockerExport.cs` `Run` (via `work/lobby-parity/run_export.sh`, files copied in by hand) | live producer |
| `MatchHero_<Sex>_Distance.*` incl. `_Distance_Swing_NN` | `MatchHeroLockerExport.RunDistance` (same swing loop, line 336 `!golf`) | live producer |
| `GolfKitHero_<Sex>.json/.lzfse/_Rig.lzfse` (no swing frames) | `MatchHeroLockerExport.RunGolf` | live producer |
| PNG maps: `MatchHero_Original*`, `MatchHero_Kit_*_{M,N}.png`, `MatchHero_(Cloth\|GolfKit)_FabricAtlas.png`, `MatchHero_Cloth_Weave.png`, `MatchHero_GolfKit_*_{M,N}.png` | `MatchHeroLockerExport` copies every map a material references (line 354; `Original*` are generated into `OutDir/.generated-maps/`, line 403) | live producer |
| **all `*.ktx`** (KTX1, ASTC) | **No script in the repo.** The KTX metadata says `KTXwriter = Apple TextureConverter 5.0`, a manual conversion done 10-06. | manual / orphan producer |
| `GolfHero_*`, `MatchHero_Golf_*.ktx` | none: added in `4999cc7a` (10-06), replaced by `GolfKitHero_*` / `MatchHero_GolfKit_*` | **orphan** |
| `MatchHero_Kit_<Sex>_<Piece>.png` (no suffix), `MatchHero_Skin_Soft_N.png` | older exporter versions (kit `baseMap`, skin `bumpMap`); current materials no longer reference them | **orphan** |
| `NativeGolfEquipment_*.json` | none in the repo (the JSON's `source` names `blender/scripts/golf_clubs.py`, which only builds meshes) | orphan producer |
| `HeroV4*`, `HeroMenu.*`, `HeroSwing_NN`, `HeroLounge` | `Unity/Assets/Editor/HeroLockerExport.cs` (legacy). Its default mode **still writes `HeroV4.*` + maps into CharacterAssets** when run. Its menu/lounge bake modes (`HeroMenu`/`HeroSwing_NN`/`HeroLounge` stems) now write to `ArtDir/ui/toybox-simple-v6/source/preview-raw`. | stale producer, excluded output |
| `Higgs*`, `Player*`, `Custom*` | none | **orphan**, excluded |
| `ClubEnvironments/Club_<room>.json + _Albedo.png` | `ArtDir/environments/motion-club-3d/tools/build_club_environments.py` (writes all 4 rooms, line 203) | live producer |

---

## Findings

### RES-01: Old golf hero export `GolfHero_*` (30 files)

- **Path(s):** `GolfArcade/Unity/CharacterAssets/GolfHero_{Male,Female}.json`, `.lzfse`, `_Rig.lzfse`, `_Swing_00..11.lzfse`
- **What it is:** The first native copy of the golf heroes (10-06), with its own swing frames and the "Golf_" kit maps. It was replaced the same week by `GolfKitHero_*`.
- **Size:** 48.05 MB. Tracked: yes. LFS: 28 of 30 (the two json are plain). In app: **yes**. Unmodified.
- **Evidence:**
  - `MatchHeroData.asset` builds only `"GolfKitHero_"` or `"MatchHero_" + sex (+ "_Distance")` (`MatchHeroData.swift:129`).
  - `"GolfHero_"` appears in no Swift, test, `.cs`, `.py` or `.sh` file.
  - No exporter writes this name: `git log -S` shows only the adding commit.
  - It is in the Oct 9 app (30/30).
- **Depends on it:** nothing. Its manifests are the only references to RES-02 and RES-03.
- **Verdict:** **SAFE** (plus G1 regeneration). **Confidence:** high.

### RES-02: Old "Golf_" garment textures `MatchHero_Golf_*.ktx` (29 files)

- **Path(s):** `CharacterAssets/MatchHero_Golf_{Male,Female}_{Bottom,GloveL,GloveR,Head,ShoeL,ShoeR,Top}_{M,N}.ktx` and `MatchHero_Golf_FabricAtlas.ktx`
- **What it is:** Compressed normal/mask maps and a fabric atlas for the retired GolfHero kit.
- **Size:** 11.20 MB. Tracked: yes. LFS: **no** (plain blobs). In app: yes.
- **Evidence:**
  - These names are referenced only by the `GolfHero_*.json` manifests.
  - The live golf manifests reference `MatchHero_GolfKit_*` instead. The exporter renames `Golf_` to `GolfKit_` in `MapResourceName`, `MatchHeroLockerExport.cs:379`.
- **Depends on it:** nothing.
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-03: Old soft skin normal `MatchHero_Skin_Soft_N.png` (1 file)

- **Path(s):** `CharacterAssets/MatchHero_Skin_Soft_N.png`
- **What it is:** The skin "soft normal" bump map from the 10-04 locker mirror.
- **Size:** 0.04 MB. Tracked: yes. LFS: yes. In app: yes.
- **Evidence:**
  - Only `GolfHero_*.json` references it, as `bumpMap`.
  - All six live manifests have `bumpMap = ""` and `bumpTriplanar = 0`, so `MatchHeroSurfaces.swift:133` never loads a bump map.
  - The `CharacterModelPreview.swift:462` doc comment ("soft skin normal") is stale.
- **Depends on it:** nothing.
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-04: Distance-hero swing frames (24 files)

- **Path(s):** `CharacterAssets/MatchHero_{Male,Female}_Distance_Swing_00..11.lzfse`
- **What it is:** Forehand morph frames for the lighter "distance" heroes. The only user of the distance heroes is the online lobby (`LobbyHeroStage`), which plays rig clips and never morph swings.
- **Size:** 38.18 MB. Tracked: yes. LFS: yes. In app: yes.
- **State:** **all 24 modified and uncommitted** (10-10 re-export).
- **Evidence:**
  - The only reader of swing frames is `MatchHeroData.Asset.swingTargets()`.
  - Its only caller is `MatchHero.preparePractice` (`CharacterModelPreview.swift:557`), which always loads the close tennis asset `asset(female:)`.
  - No test reaches distance swing frames. `VisualOverhaulNativeTests` uses distance rigs only.
- **Depends on it:** nothing reads them. The distance manifests still list the names, which does no harm.
- **Verdict:** **SAFE+FIX**. Delete the frames, and change the exporter loop at `MatchHeroLockerExport.cs:336` from `!golf` to `!golf && !distance`. For distance, also clear `swingTimes`, as golf already does. Without the exporter change, the next `RunDistance` writes the frames again.
- **Confidence:** high.

### RES-05: Tennis practice-swing frames, reached only from tests (24 files)

- **Path(s):** `CharacterAssets/MatchHero_{Male,Female}_Swing_00..11.lzfse`
- **What it is:** The match Forehand sampled as 12 morph targets, used for the "swing to practice" hero on the loading screen.
- **Size:** 47.63 MB. Tracked: yes. LFS: yes. In app: yes.
- **State:** **all 24 modified and uncommitted** (10-10 re-export).
- **Evidence:**
  - The loading-screen hook `CharacterModelPreview(…, practiceSequence: session.loading.practiceSequence)` was removed on 10-08 (`9283f6a8`), when the loading screen switched to `PresentationRosterCard`.
  - Today nothing passes `practiceSequence` (only declared at `CharacterModelPreview.swift:100`).
  - `LoadingModel.practiceSequence` (`LoadingModel.swift:39`) is never read.
  - `LoadingModel.startPracticeInput()` (line 113) is never called.
  - So `practice()` → `startMorphPractice()` → `preparePractice()` → `swingTargets()` never runs in the app.
- **Depends on it (tests only):**
  - `GolfArcadeTests/MatchHeroLockerTests.swift` practice-swing section (~lines 138–225) and the `swingFrames.count == swingTimes.count` check at line 60.
  - `GolfArcadeTests/DressedHeroSnapshotTests.swift:170-192` (the "contact" pose).
- **Verdict:** **DECIDE**. This is a product question: bring the loading-screen practice swing back, or drop it.
  - If dropping: delete the frames, the swing loop in the exporter, `MatchHeroData.swingTargets`/`Asset.swing`, `CharacterModelPreview` `practiceSequence`/`practice`/`startMorphPractice`/`endMorphPractice`/`preparePractice`/`practiceFrame`, `LoadingModel` `practice`/`practiceSequence`/`startPracticeInput`/`CMMotionManager`, and the tests above.
  - Also update tests that still call `session.loading.practice()`: `TennisMenuSnapshotTests:180-189`, `LockerMirrorProofTests:339-366`, `MotionClubReviewTests:98,174`.
- **Confidence:** high on usage.

### RES-06: PNG copies of the KTX garment maps, plus the unused weave (51 files)

- **Path(s):**
  - `CharacterAssets/MatchHero_Kit_{Male,Female}_{Bottom,ShoeL,ShoeR,SockL,SockR,Top}_{M,N}.png` (24, 4.72 MB)
  - `MatchHero_GolfKit_{Male,Female}_{Bottom,GloveL,GloveR,ShoeL,ShoeR,Top}_{M,N}.png` (24, 3.80 MB)
  - `MatchHero_Cloth_FabricAtlas.png`, `MatchHero_GolfKit_FabricAtlas.png`
  - `MatchHero_Cloth_Weave.png`
- **What it is:** The garment normal and mask maps exactly as the exporter copies them out of Unity. They are the sources the KTX files were hand-converted from. The app reads the `.ktx` twins instead.
- **Size:** 8.61 MB. Tracked: yes. LFS: yes. In app: yes.
- **Evidence:**
  - `garment()` loads normal, mask and atlas only through `compressed()`, which is KTX only (`MatchHeroSurfaces.swift:570-582`).
  - The PNG loader `image()` is called only for `baseMap`, `bumpMap`, and `weaveMap` when `baseMap` is not empty (`:356-363`).
  - In all six live manifests every kit `baseMap` is `""`, so no kit PNG is ever opened.
  - `MatchHero_Cloth_Weave.png` is named as `weaveMap` of `Kit_Top`, but that submesh has `baseMap = ""` (`hasUV` false, `:360`), so it is never read.
  - Dimensions match the KTX twins (50/50 pairs).
- **Depends on it:** no app code and no tests. `MatchHeroLockerExport` rewrites these PNGs on every export (`File.Copy`, line 354), and anyone re-making the KTX needs them.
- **Verdict:** **SAFE+FIX**. Keep them in the repo but **exclude them from the app**, for example by adding these patterns to `project.yml` excludes:
  - `Unity/CharacterAssets/MatchHero_*_M.png`
  - `Unity/CharacterAssets/MatchHero_*_N.png`
  - `Unity/CharacterAssets/*FabricAtlas.png`
  - `Unity/CharacterAssets/MatchHero_Cloth_Weave.png`

  None of the three live PNGs (`MatchHero_Original*`) matches these patterns. The alternative is to have the exporter write garment-map PNGs to a staging folder. If the files are deleted instead, the next export brings them back.
- **Confidence:** high.

### RES-07: Retired kit seam maps (12 files)

- **Path(s):** `CharacterAssets/MatchHero_Kit_{Male,Female}_{Top,Bottom,ShoeL,ShoeR,SockL,SockR}.png` (no `_M`/`_N` suffix)
- **What it is:** The 10-04 "seam/hem darkening" base maps from the first cloth shader. They were replaced by the garment normal/mask KTX maps.
- **Size:** 1.17 MB. Tracked: yes. LFS: yes. In app: yes.
- **Evidence:** No manifest references them (kit `baseMap` is `""` everywhere), and no exporter produces them any more.
- **Depends on it:** `GolfArcadeTests/LockerMirrorTests.swift:145-170` (`testSeamMapOrientationPutsTheDarkeningOnTheHemsAndCollar`) loads `MatchHero_Kit_Male_Top`, `_Male_Bottom` and `_Female_Top` through `MatchHeroSurfaces.image`.
- **Verdict:** **SAFE+FIX**. Retire that test, which tests a pipeline the app no longer uses. The other 9 files have no reader at all.
- **Confidence:** high.

### RES-08: Golf-kit "Head" maps without a part (4 files)

- **Path(s):** `CharacterAssets/MatchHero_GolfKit_{Male,Female}_Head_{M,N}.ktx`
- **What it is:** Cap/visor garment maps.
- **Size:** 0.35 MB. Tracked: yes. LFS: no. In app: yes.
- **Evidence:** The `GolfKitHero_*` manifests have no `Kit_Head` part, so these names are referenced by nothing. Only the dead `GolfHero` had a head part.
- **Depends on it:** nothing.
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-09: Garment maps referenced but switched off: polo tops and female bottom (6 files)

- **Path(s):** `CharacterAssets/MatchHero_Kit_Male_Top_{M,N}.ktx`, `MatchHero_Kit_Female_Top_{M,N}.ktx`, `MatchHero_Kit_Female_Bottom_{M,N}.ktx`
- **What it is:** Compressed garment maps of the earlier tennis shirt and skort.
- **Size:** 6.29 MB. Tracked: yes. LFS: no. In app: yes.
- **Evidence:**
  - The current manifests (close and distance) still name them as `normalMap`/`maskMap`.
  - Those submeshes (`Kit_Top` both sexes, female `Kit_Bottom`) export `useGarmentMaps = 0`, which is the tailored polo/skirt work in progress. They go through the flat `cloth()` path, which never loads KTX.
  - Of 24 tennis KTX files, 18 are loaded and these 6 are not.
- **Depends on it:**
  - Nothing today.
  - If a later re-export sets `useGarmentMaps = 1` on these parts and the files are gone, `garment()` hits **`fatalError`** (G3).
  - They are probably stale for the new polo/skirt UVs anyway.
- **Verdict:** **DECIDE**. Keep them until the clothes pass (AGENTS.md 10-08: male polo → female polo/skirt) settles, then delete or regenerate them.
- **Confidence:** medium-high.

### RES-10: Driver, putter and wedge meshes (3 files)

- **Path(s):** `CharacterAssets/NativeGolfEquipment_{Driver,Putter,Wedge}.json`
- **What it is:** Native SceneKit club meshes.
- **Size:** 0.72 MB. Tracked: yes. LFS: no. In app: yes.
- **Evidence:**
  - The only call is `NativeGolfEquipment.make("Iron", …)` (`CharacterModelPreview.swift:548`).
  - No other id is ever requested, in the app or in tests.
  - `NativeGolfEquipment_Iron.json` is live (RES-12): it supplies the club when a tennis-dressed hero idles with golf (`configureIdle`, `CharacterModelPreview.swift:420-424`). Example: `OnboardingRewardScreen` after the player picks golf (`idleSport: .golf`, default `outfitSport: .tennis`).
- **Depends on it:** nothing. `asset("Driver")` would return nil, which is harmless.
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-11: Old hero families already excluded from the app (63 files, repo weight only)

- **What it is, family by family:**

  | Family | Files | Size | What it was |
  |---|---|---|---|
  | `HeroV4*` | 9 | 11.61 MB | Baked Hero V4 locker export with atlas, iris, hair and masks. `HeroV4.bin` is LFS. |
  | `HeroMenu.*`, `HeroLounge.lzfse`, `HeroSwing_00…19` | 11 | 53.72 MB | Menu, lounge and swing vertex bakes. |
  | `Higgs*` | 6 | 2.86 MB | Higgsfield avatar colour and mask textures. |
  | `Player*` | 28 | 23.31 MB | The old "Higgs PlayerMale/Female" per-part meshes. Two pairs are byte-identical duplicates (`PlayerMale/FemaleBob.json`, `…Swept.json`). |
  | `Custom*` | 9 | 51.91 MB | Customizable body, head and hair meshes, e.g. `CustomBodyM.json` 20.7 MB, plain blob. |

- **Size:** 143.41 MB. Tracked: yes. LFS: 32 of 63 (72.34 MB). All the json are plain git blobs (71.07 MB). In app: **no**, excluded by `project.yml` (`Custom*` since 09-28; `Hero*`, `Player*`, `Higgs*` since 10-04). They are absent from the Oct 9 app.
- **Evidence:** No live Swift code reads these names. `HeroV4` in `CharacterModelPreview.swift:447` is only a haircut-name catalogue.
- **Depends on it:**
  - `MatchHeroLockerTests.swift:63-66` asserts these files **don't** ship. It keeps passing after deletion and could then be simplified.
  - `MatchHeroData.data()`'s `.bin` fallback (`MatchHeroData.swift:85`) only ever served `HeroV4.bin`, so it becomes fully dead code.
  - `Unity/Assets/Editor/HeroLockerExport.cs` still writes `HeroV4.*` into this folder when run.
- **Verdict:** **SAFE**. Optional follow-up: keep the `Hero*` exclude glob in `project.yml` as long as `HeroLockerExport.cs` exists. Otherwise one run of it would put HeroV4 back into the app and fail the "must not ship" test. If you drop the glob, delete or retarget `HeroLockerExport.cs` too.
- **Confidence:** high.

### RES-12: Live hero data — KEEP (66 files)

- **Path(s):**
  - `MatchHero_{Male,Female}{,_Distance}.json/.lzfse/_Rig.lzfse` and `GolfKitHero_{Male,Female}.json/.lzfse/_Rig.lzfse` (18 files)
  - 44 KTX:
    - 18 tennis garment maps (male Bottom, Shoe, Sock M/N; female Shoe, Sock M/N)
    - 24 `MatchHero_GolfKit_*` M/N (Top, Bottom, gloves, shoes)
    - 2 fabric atlases
  - 3 `MatchHero_Original*` PNGs
  - `NativeGolfEquipment_Iron.json`
- **What it is:** Everything the locker, menus, rival/roster portraits, onboarding and online lobby draw.
- **Size:** 322.45 MB. **The 6 rig files alone are 285 MB** (tennis rigs 73–76 MB each, distance rigs 48–67 MB, golf rigs 8–13 MB). LFS: 15 of 66. 19 modified (10-10).
- **Evidence:** The loader replay finds 65 files loaded from the 6 manifests, none missing, plus the Iron fallback.
- **Consumers:**
  - Close tennis: `CharacterModelPreview` (Locker, Home, Onboarding, `IslandPlayer`), `RivalPortraits`, `PresentationPortraits`.
  - Distance: `LobbyHeroStage` (`CharacterModelPreview.swift:653`), the online lobby only, reached through `TennisMenuViews` → `IslandOnlineScreen`.
  - Golf: outfit sport golf.
- **Depends on it:** the app, `VisualOverhaulNativeTests`, `LockerMirrorTests`, `MatchHeroLockerTests` and the `GolfArcadeTests/VisualGolden` goldens and provenance (must be repacked on each re-export).
- **Verdict:** **KEEP**. Size note: the distance set costs 121 MB of the app (plus RES-04) for the online lobby only.

### RES-13: Baked 3D club rooms that are never shown: entrance, loading, terrace (6 files)

- **Path(s):** `GolfArcade/Unity/ClubEnvironments/Club_{entrance,loading,terrace}.json` and `Club_{entrance,loading,terrace}_Albedo.png`
- **What it is:** Baked-UV 3D menu backdrops, 64k–83k triangles each.
- **Size:** 61.52 MB. Tracked: yes. LFS: the PNGs yes; **the json no (17–18 MB plain blobs each)**. In app: yes.
- **Evidence:**
  - `ClubEnvironmentView` is created only as `ClubEnvironmentView(room: .locker)` (`ClubDesign.swift:448`).
  - Every other room goes to `ClubLivingImageView`, the 2D cinemagraph of `club-<room>.png` (`ClubDesign.swift:450`, `ClubEnvironmentView.swift:177`).
- **Depends on it:**
  - `GolfArcadeTests/ClubEnvironmentTests.swift:8-17` loops over all 4 rooms.
  - `build_club_environments.py` writes all 4 rooms.
- **Verdict:** **SAFE+FIX**. Limit that test to `.locker`, and make the builder stop writing the other three rooms into `GolfArcade/` (or exclude them).
- **Confidence:** high.

### RES-14: Locker room 3D backdrop — KEEP (2 files)

- **Path(s):** `ClubEnvironments/Club_locker.json` (13.98 MB, plain blob) and `Club_locker_Albedo.png` (3.36 MB)
- **Evidence:** Drawn behind the Locker (`LockerStudio.swift:27`, `IslandBackdrop(room: .locker)`) and every `IslandShell` titled "Locker".
- **Verdict:** **KEEP**.

### RES-15: Old menu b-roll videos `MenuVideo/*` (22 files, repo weight only)

- **Path(s):** `GolfArcade/Unity/MenuVideo/{bowling,boxing,football,golf,tennis}-{1,2}.{mp4,jpg}` and `tennis-game.{mp4,jpg}`
- **What it is:** 09-24 looping b-roll and poster frames for the old multi-sport menu.
- **Size:** 40.92 MB. Tracked: yes. **LFS: no** (plain git blobs). In app: **no**, excluded by `project.yml` since 09-24 (`cbac1684`).
- **Evidence:**
  - The only player, `LoopingVideo` (`HowToCards.swift:89-120`), is never created.
  - `Sport.clips` returns `[]` (`SportProgress.swift:37`), with a comment explaining the old clips contain players.
- **Depends on it:**
  - Nothing at runtime.
  - `TennisCampaignTests.swift:253-255` loops over the empty `clips`, so it does nothing.
  - `TennisMenuSnapshotTests.swift:355` sets `LoopingVideo.stillsOnly`.
- **Verdict:** **SAFE**. Optional cleanup:
  - Remove `Unity/MenuVideo` from the `project.yml` excludes.
  - Delete `LoopingVideo`, `Sport.clips` and those two test lines.
  - Keep `LoopingPlayer`, which RES-26's gameplay previews use.
- **Confidence:** high.

### RES-16: Old illustrated portraits `menu-hero-*` and `menu-trophy` (14 files)

- **Path(s):** `MenuArt/menu-hero-{bruno,dex,jax,lina,milo,nadia,player-female,player-male,ray,rosa,suki,tama,viktor}.png` and `MenuArt/menu-trophy.png`
- **What it is:** Painted 2D portraits of rivals, coach and player from the 09-24 arcade menu.
- **Size:** 6.86 MB (9.34 MB in the device build). Tracked: yes. LFS: yes. In app: yes.
- **Evidence:**
  - Loaders: `HeroArt` (`TennisMenuViews.swift:~146`), used only by `OpponentCard`, which nothing creates since 10-04 (`48f18349`).
  - `TennisStory.art(for:)` (`TennisStory.swift:31-38`) is never called. The story screen shows text only (`ClubScreens.swift:335-352`).
  - The campaign rival card now uses `RivalPortrait`, a rendered 3D match hero (`IslandScreens.swift:123`, `RivalLooks.swift:41-70`).
  - **The hint "still shown on the campaign screen" is outdated.**
- **Depends on it:** nothing.
- **Verdict:** **SAFE**. Optional dead-code cleanup: `OpponentCard` and its extension, `HeroArt`, `TennisStory.art`, `TennisOpponent.art` (`TennisCampaign.swift:24`).
- **Confidence:** high.

### RES-17: `MenuArt/menu-backdrop.jpg` (1 file)

- **What it is:** The old arcade key-art backdrop.
- **Size:** 0.21 MB. Tracked: yes. LFS: yes. In app: yes.
- **Evidence:** Its only loader, `MenuBackdrop` (`TennisMenuViews.swift:104-128`), is never created. The last user was removed by 10-08 (`14a43331`).
- **Verdict:** **SAFE** (optionally delete `MenuBackdrop` too). **Confidence:** high.

### RES-18: `MenuArt/island-intro.png` and `island-terrace.png` (2 files)

- **What it is:** Painted island scenes from the Island Sports Club menu.
- **Size:** 4.70 MB (8.21 MB in the device build). Tracked: yes. LFS: yes. In app: yes.
- **Evidence:** No literal or string-built reference anywhere. The only `island-*` loader is `island-campaign.png` (`IslandScreens.swift:172`).
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-19: `MenuArt/map-golf-postcards.jpg` (1 file)

- **What it is:** Course card for the retired "Postcards" golf course.
- **Size:** 0.17 MB. Tracked: yes. LFS: yes. In app: yes.
- **Evidence:** `GolfCourseChoice` is cliffside, wildisles and magma; the comment in `TennisMapScreen.swift:29` says "postcards key is retired". `GameplayPreview` fixes golf to cliffside.
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-20: `MenuArt/club-locker.png` (1 file)

- **What it is:** The 2D cinemagraph still for the locker room.
- **Size:** 2.20 MB (4.31 MB in the device build). Tracked: yes. LFS: yes. In app: yes.
- **Evidence:** `IslandBackdrop` sends `.locker` to the 3D `ClubEnvironmentView`. `ClubLivingImageView` is only reached for intro/entrance, terrace and loading (`ClubDesign.swift:447-450`). No `ClubArt("locker")` exists.
- **Verdict:** **SAFE**. **Confidence:** high.

### RES-21: Island Sports Club objects and scenes that nothing loads (14 files)

- **Path(s):** `MenuArt/club-{crown,dial,kit,lesson,play,quick,tennis,training,trophy,whistle}.png` and `MenuArt/scene-{trophy,stands,office,locker}.jpg`
- **What it is:** Rendered menu objects and painted scenes from Adnan's 09-28 "Island Sports Club" menu.
- **Size:** 3.66 MB. Tracked: yes. LFS: yes. In app: yes.
- **Evidence:**
  - `ClubArt(name:)` is called with these names only: `"golf"` and `"racket"` (onboarding), `sport.rawValue` for non-playable sports (locked screen), `"friends"`, and `"lock"`/card art.
  - `SceneImage`/`ClubBackdrop` are called with these names only: court, cliff, pavilion, home.
  - `club-tennis.png` lost its last user when the uncommitted working tree deleted `ClubQuickPlayScreen` (it used `ClubCard(art: "tennis")`).
  - Byte-identical copies live in `Unity/Assets/Resources/Club/` (Unity side, separate).
- **Verdict:** **SAFE**. **Confidence:** high (for `club-tennis`: high on the working tree).

### RES-22: Art used only by views nothing creates (3 files)

- **Path(s):** `MenuArt/scene-home.jpg`, `club-friends.png` (both in `ClubPartyScreen`, never created) and `club-lock.png` (`ClubCard`, never created on the working tree)
- **Size:** 0.74 MB. Tracked: yes. LFS: yes. In app: yes.
- **Verdict:** **SAFE**. Optional: delete `ClubPartyScreen` (`ClubScreens.swift:54-75`) and `ClubCard` (`ClubDesign.swift:343-…`).
- **Confidence:** high.

### RES-23: "Coming soon" sport screen art and the display font, only reachable through `.locked` (7 files)

- **Path(s):** `MenuArt/scene-pavilion.jpg`, `club-crest.png`, `club-{bowling,football,boxing}.png`, `Fonts/Bricolage.ttf`, `Fonts/OFL-Bricolage.txt`
- **What it is:** Backdrop, crest and sport objects of `ClubLockedScreen`, plus the Bricolage display face.
- **Size:** 1.73 MB. Tracked: yes. LFS: 6 of 7. In app: yes.
- **Evidence:**
  - Users: `ClubScreen` → `ClubBackdrop`/`ClubHeader`/`ClubCrest`, `ClubButton`, and `Club.display`/`Club.title`.
  - All of them appear only in `ClubLockedScreen`/`ClubPartyScreen`. `ClubCard` uses `Club.title` but is never created.
  - `.locked(sport)` is shown only by `tap("sport-<non-playable>")` (`TennisMenu.swift:335`), and the game-select rows list only playable sports (`TennisMenu.swift:169`).
  - `ClubFonts.register()` still registers Bricolage at runtime (`ClubDesign.swift:53`), but no reachable text uses it.
  - All loaders have fallbacks.
- **Depends on it:** the "coming soon" path if another sport is ever offered. `OFL-Bricolage.txt` must ship whenever `Bricolage.ttf` ships.
- **Verdict:** **DECIDE**. Either remove the `.locked` route and the Club scaffold (then delete all 7), or keep them for future sports.
- **Confidence:** medium-high.

### RES-24: `GolfArcade/Multiplayer/GolfArcade.entitlements` — KEEP, but it is not wired into the build (1 file)

- **What it is:** The Game Center entitlement (`com.apple.developer.game-center`).
- **Size:** 234 B. Tracked: yes. LFS: no. Not a bundle resource.
- **Evidence:**
  - No `CODE_SIGN_ENTITLEMENTS` in `project.yml` or in either `.pbxproj`. The setting existed until `7ecf8100` (10-06) regenerated the projects from `project.yml`, which never had it.
  - The app uses GameKit: `Onboarding/GameCenterAccount.swift`, `OnlineGameCenterSheet`.
  - Branch `main` fixes this in `862bca5f` (10-09, "Prepare release app icon, privacy manifest, and Game Center signing"), which is **not** on `final-build`.
- **Verdict:** **KEEP**. It looks dead only because it is unwired. Fix: add `CODE_SIGN_ENTITLEMENTS: GolfArcade/Multiplayer/GolfArcade.entitlements` to `project.yml`, or bring `862bca5f` over.
- **Confidence:** high.

### RES-25: `GolfArcade/Unity/SportsInfo.plist` — KEEP (1 file)

- **What it is:** The `INFOPLIST_FILE`, merged with the generated one: display name "Motion Club Beta", `CADisableMinimumFrameDurationOnPhone`, and the external-display scene manifest (`SportsExternalScene`).
- **Why it looks excluded:** It is correctly excluded from the resource copy phase.
- **Evidence:** Read by `MenuBetaTests:81` and `SportsIntegrationTests:224`.
- **Verdict:** **KEEP**.

### RES-26: Live MenuArt — KEEP (16 files)

- **Size:** 16.84 MB.
- **Files and users:**
  - `golf-controller-artwork.png` (`GolfController.swift:56`)
  - `island-campaign.png` (campaign map)
  - `map-{resort,skyscraper,volcano}.jpg` and `map-golf-{cliffside,wildisles,magma}.jpg` (court/course picker; `map-skyscraper` and `map-golf-cliffside` are also `GameplayPreview` posters)
  - `gameplay-golf-cliffside.mp4` and `gameplay-tennis-skyscraper.mp4` (`IslandSportsScreen` → `GameplayPreview`; required by `MenuBetaTests:78-80`)
  - `club-{entrance,terrace,loading}.png` (`ClubLivingImageView` backdrops)
  - `club-golf.png`, `scene-court.jpg`, `scene-cliff.jpg` (onboarding game picker)
- **Verdict:** **KEEP**.

### RES-27: Fonts and sounds in use — KEEP (5 files)

- **Files:** `Fonts/Rubik.ttf` + `OFL-Rubik.txt` (all `IslandUI.font` text) and `MenuSounds/{tick,pop,whoosh}.wav` (`ClubSound.play` from `TennisMenu.swift:215,270,296`, `SportsDisplays.swift:100,354`, onboarding).
- **Size:** 0.42 MB.
- **Verdict:** **KEEP**.

---

## Cross-area observations (not unused items, but found on the way)

- **O1. Nothing in the repo regenerates the KTX textures.**
  - All garment KTX were converted by hand (Apple TextureConverter 5.0). The exporter writes PNG only.
  - After the next kit re-export, the app keeps rendering the old KTX maps unless someone re-converts them.
  - The KTX are plain git blobs, not LFS.
- **O2. Committing the 10-10 lobby re-export as-is adds about 86 MB of LFS uploads the app never reads.** Of its 67 modified files, 48 are swing frames: RES-04 is never read, and RES-05 is read by tests only.
- **O3. `final-build` is behind `main` on resources.**
  - `main` has `GolfArcade/Assets.xcassets` (AppIcon), `GolfArcade/PrivacyInfo.xcprivacy` and the entitlement wiring (`862bca5f`).
  - `final-build` currently ships **no app icon and no privacy manifest**, even though both pbxproj files set `ASSETCATALOG_COMPILER_APPICON_NAME = AppIcon`.
  - `main` also carries 8 `MenuArt/course-golf-*.{jpg,mp4}` files. `final-build`'s code no longer references them, so watch for them on merge.
- **O4. Unity has its own copies.** `Unity/Assets/Resources/Club/` holds byte-identical copies of 24 MenuArt files and the 3 MenuSounds, for the Unity Unity auditor.
- **O5. Dead Swift helpers that belong to these resources:**
  - `MatchHero.previewData` (no caller)
  - `MatchHeroData.data`'s `.bin` fallback
  - `LoadingModel.startPracticeInput` (never called) and `practiceSequence` (never read)
  - `MenuBackdrop`, `OpponentCard`/`HeroArt`, `TennisStory.art`, `LoopingVideo`, `ClubPartyScreen`, `ClubCard`
  - Stale doc comments mentioning "MatchHero_<map>.png … soft skin normal" (`MatchHeroData.swift:12`, `CharacterModelPreview.swift:462`) and "loading screen's practice swing"
- **O6. The exporter writes straight into the app folder.** `MatchHeroLockerExport`'s default `OutDir` is the app's `CharacterAssets`. It also creates `CharacterAssets/.generated-maps/` there. Re-running `Run`/`RunDistance` brings back RES-04, RES-05 and RES-06. `HeroLockerExport.cs` (stale) also targets this folder.
- **O7. Missing file reference (harmless).** The onboarding tennis card asks for `club-racket.png`, which doesn't exist, so an SF Symbol shows instead.
- **O8. Tests that drive removed behaviour** (for the test auditor): `TennisMenuSnapshotTests:180-189`, `LockerMirrorProofTests:339-366`, `MotionClubReviewTests:98,174` (`session.loading.practice()`); `MatchHeroLockerTests` practice section; `LockerMirrorTests` seam test; `ClubEnvironmentTests` 4-room loop.

---

## Summary

| Verdict | Entries | Files | Bundled bytes (repo) | Excluded bytes (repo only) |
|---|---|---|---|---|
| SAFE | 14 (RES-01, 02, 03, 08, 10, 11, 15, 16, 17, 18, 19, 20, 21, 22) | 188 | 78.90 MB | 184.33 MB (RES-11 143.41 + RES-15 40.92) |
| SAFE+FIX | 4 (RES-04, 06, 07, 13) | 93 | 109.47 MB | 0 |
| DECIDE | 3 (RES-05, 09, 23) | 37 | 55.65 MB | 0 |
| KEEP | 6 (RES-12, 14, 24, 25, 26, 27) | 91 | 357.04 MB | 0 (plist excluded on purpose) |
| **Total** | 27 | 409 | 601.07 MB bundled | 184.33 MB |

- **App bytes bundled but never loaded at runtime:** **244.03 MB** of 601.07 MB of GolfArcade-native bundled resources (about 41 %), or **about 255 MB** in the actual Oct 9 device build after Xcode's PNG processing. The same 244.03 MB, split by who reads it:
  - **Never read by the app or any test:** 134.35 MB. This is everything except RES-05, RES-13 and three RES-07 files. It includes RES-09, and RES-23, where Bricolage is registered at launch but never displayed.
  - **Read only by tests:** 109.67 MB (RES-05 47.63 + RES-13 61.52 + 3 RES-07 files 0.53).
  - **Split by verdict:** SAFE 78.90 / SAFE+FIX 109.47 / DECIDE 55.65 MB (RES-05 47.63, RES-09 6.29, RES-23 1.73).
- **Repo bytes of excluded or unused files:**
  - **184.33 MB** excluded from the app (repo-only: RES-11 + RES-15).
  - Plus 244.03 MB bundled-but-unused.
  - Total **428.36 MB**.
  - 183.85 MB of that total is stored as plain git blobs, not LFS: json, ktx, txt and the MenuVideo files (G2).
