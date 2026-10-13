# Audit: Unity assets outside Resources, Unity project clutter, Unity packages (area `unity-assets-other`)

Read-only audit of the working tree at `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator` (branch final-build, HEAD 14a43331, 2026-10-10). Nothing in the repo was changed.

## How this was checked

- **What ships:** the shared GUID index (`audit/unity_index/build_closure.json`: 3 build scenes + all `Resources/` + ProjectSettings). I cross-checked it against the last real iOS build. `Unity/Library/LastBuild.buildreport` (Oct 8 22:01) was parsed for the packed size of every source asset (sum 1,522.5 MB, which matches `resources.assets` + `.resS`). The per-asset sizes are in `audit/unity-assets-other/build_report_packed_sizes.csv` (2,413 rows), which other auditors can reuse.
- **Who uses an asset outside the build:** `rg` over `Unity/Assets/{Editor,Tests,Scripts}`, `Unity/Tools`, `Tools/`, `blender/scripts`, `.github`, `GolfArcade*`, `docs`, `SportsLibrary`, and `work/**/*.{sh,py,js}` (2 MB cap) for:
  - folder paths and file names
  - `LoadAssetAtPath`, `FindAssets`, `OpenScene`, `LoadScene*`, `Resources.Load`
  - `-executeMethod` targets
- **Packages:** I mapped every GUID in `Library/PackageCache/**.meta` to its package and searched all project asset references for those GUIDs. I also read the asmdef references, the `using`/API usage in code, and the iOS export's `ScriptingAssemblies.json` and `RuntimeInitializeOnLoads.json`.
- **Caveats:**
  - The GUID closure does not see native plugins (`Plugins/iOS/*.mm` always ship on iOS), `#include`d `.hlsl` files or `link.xml`. These are handled by hand below or belong to other areas.
  - The `.cs` hits in `work/` are mostly Unity project clones (`work/*/UnityClone`, `isolation`, …). Only `.sh`/`.py`/`.js` hits were treated as real callers.
  - The editor was not running during the audit (only Unity Hub).

Tracked = in git at HEAD. LFS = the HEAD blob is an LFS pointer. Sizes include `.meta` files.

---

## A. Unity assets outside Resources

### UASSET-01: The old Hero01 tennis hero is still inside the shipped iPhone app (~83 MB)
- **Paths:** `Unity/Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab` (1 file, 176 KB; it belongs to the app-resources area) pulls in 67 files of `Unity/Assets/ArtDirection/Hero01/**`: 27 FBX, 7 PNG, 32 materials and `Props/Hero_Racket.asset`.
- **What it is:** the old "Hero01 / V4" tennis hero prefab. Anything under `Resources/` always ships, so its whole body, wardrobe, clips and textures go into the app.
- **Size:**
  - On disk: 47.1 MB for the 67 Hero01 files.
  - In the Oct 8 iOS build: **82.8 MB** packed (`LastBuild.buildreport`). The textures import uncompressed on iOS (`HeroLookImporter` sets `textureCompression=Uncompressed`, and there is no iOS override), so they are big:
    - SkinMask: 22.4 MB, RGBA32 2048²
    - BaseColor: 16.8 MB
    - MatteCloth_TrimTeal: 16.8 MB
    - Normal, Hair_Buzz, Hair_Waves: 4.2 MB each
  - Also in the same folder: `HeroV4_KitMask.png` (4.2 MB in build) and `HeroV4_KitRef.json`. No runtime code uses them either; the legacy `HeroLockerExport.cs:118-124` writes them.
  - All tracked. The FBX/PNG are LFS.
- **Evidence it is unused:**
  - No runtime `Resources.Load` uses `Tennis/Hero/...` (all `Resources.Load*` call sites in `Scripts/` were checked).
  - `Scripts/Tennis/TennisHeroSetup.cs:8` says "The old Hero01 prefab is not loaded anywhere."
  - `Editor/LegacyHero01.cs` says "no longer referenced by any runtime code".
  - The shipped `resources.assets` contains `Hero_01_Tennis`, `Hero_01_Mixamo_Bind` and the textures by name, and the parsed headers confirm the sizes.
  - Removing just this prefab drops exactly 70 assets from the build closure: 67 Hero01 files, `HeroCosmetics.cs`, `ModularHeroLook.cs` and the prefab itself.
- **What breaks if the prefab leaves Resources:** only tools that call `Resources.Load(LegacyHero01.PrefabPath)`:
  - `Editor/MatchHeroAB.cs:56` (the A/B tool's legacy mode)
  - `Editor/HeroLockerExport.cs:81,194` (legacy exporter)
  - `Tests/PlayMode/HeroV6RetargetTests.cs:22` (an `[Explicit]` test, so it never runs in normal test runs)
  - Path strings that would need updating: `HeroGameplayBuild.cs:8` (`PrefabOut`), `HeroIntegrityAudit.cs:12`, `HeroV5Rebind.cs:17`, `HeroIntegrityReview.cs:27`, `HeroV6PolishBeforeReview.cs:27`.
- **Verdict: SAFE+FIX.** Move or delete the prefab out of `Resources/` (for example to `ArtDirection/Hero01/Prefabs/`). Then switch those callers to `AssetDatabase.LoadAssetAtPath`, or retire them with UASSET-02.
- **Confidence:** high.

### UASSET-02: `Unity/Assets/ArtDirection/Hero01/`, the archived Hero01 / V4 hero (whole folder)
- **Paths:** `ArtDirection/Hero01/**`, 125 files:
  - 67 FBX (body, wardrobe, 5 generations of clip sets: AnimRepair, BackhandRun, FullStroke, KneesOffArm, RestoreMotion)
  - 38 materials, 8 prefabs, 8 PNG
  - `HeroLookURP` / `HeroLookRenderer` / `HeroLookVolume` assets
- **What it is:** the previous tennis hero (Sept 26–Oct 3). The match heroes replaced it (HERO_MAINSTAY).
- **Size:** 90.6 MB on disk. Tracked; the 67 FBX and 8 PNG are LFS. 58 of the files (43.5 MB) are not even in the build closure, including the `Models/*_v1` clips, `Idle.fbx`, `Ready.fbx` and all 8 prefabs.
- **Evidence it is unused:** no runtime use beyond UASSET-01. Only legacy tools use it, by path or through `HeroLookImporter.Root`:
  - `HeroLookImporter` (an AssetPostprocessor scoped to this folder)
  - `HeroLookReview`, `HeroOwnedReview`, `HeroRestoreMotionReview`, `HeroGameplayFixCapture`, `HeroGameplayPlaybackReview`
  - `HeroAnimRepairReview`, `HeroBackhandRunReview`, `HeroFullStrokeReview`, `HeroKneesOffArmReview`, `HeroLegacyReview`
  - `HeroGameplayBuild`, `HeroLockerExport`, `HeroIntegrityAudit`, `HeroV5Rebind`
  - None of these is called by any `.sh`/`.py` in `work/`, `Tools/`, `Unity/Tools` or `.github`, and none is referenced by an active tool.
  - `blender/scripts/hero_parts_export.py` reads `Hero01/Models` from an old branch (historical).
- **What would break if deleted:**
  - All of the tools above, and `HeroV6RetargetTests` (Explicit).
  - These assets reference Hero01 by GUID: `HeroV5AstraTex` (89 references), `HeroV6Polish` (44) and the 8 `Hero01*.unity` scenes (27–41 each). Delete them together (UASSET-03/05/06).
  - Runtime scripts that are only attached to Hero01 assets become dead weight in `GolfArcade.dll`: `ModularHeroLook.cs`, `HeroCosmetics.cs`, `HeroLookLighting.cs`, plus the legacy-look branch in `HeroTennisDriver.cs` (unity-code area).
  - `LegacyHero01.cs` deliberately keeps this folder "so … can rebuild the old look by hand".
- **Verdict: DECIDE.** It is safe as one set: Hero01 + UASSET-01 prefab + 03 + 05 + 06 + the legacy tools. The decision is whether you still want the V4 look as a rebuildable archive. AGENTS.md calls V4/V5/V6 "archive / secondary".
- **Confidence:** high that nothing live needs it.

### UASSET-03: `Unity/Assets/ArtDirection/HeroV5AstraTex/`, the V5 "Astra texture" baseline copy
- **Paths:** 23 files: 7 FBX, 15 materials, `Hero_V5_AstraTex_Baseline.prefab`.
- **What it is:** a frozen before/after copy of the Hero01 prefab from the V5 texture side-pass (PLAN_HeroV5_AstraTextureSidePass.md).
- **Size:** 13.6 MB. Tracked; FBX are LFS. Not in the build.
- **Evidence it is unused:**
  - Written by `Editor/HeroIntegrityAudit.cs` (`Destination`).
  - Read only as the "after" baseline by `Editor/HeroIntegrityReview.cs:27` and `Editor/HeroV6PolishBeforeReview.cs:27`.
  - No GUID references from outside the folder. No callers in scripts, tests or runtime.
- **Depends on:** Hero01 (89 GUID references).
- **What would break if deleted:** those 3 editor tools (all Sept 29–30, batch-only).
- **Verdict: SAFE+FIX.** Delete the 3 tools with it (unity-code).
- **Confidence:** high.

### UASSET-04: `Unity/Assets/ArtDirection/HeroV6/`, the generated V6 hero experiment
- **Paths:** 3 files: `Hero_V6.fbx` (8.5 MB), `Hero_V6_texture_0.png` (6.9 MB), `Hero_V6.mat`.
- **What it is:** the Higgsfield/Meshy-generated V6 hero, imported to test retargeting. `docs/character-handoff/README.md` calls it "Experiment only".
- **Size:** 15.5 MB. Tracked; FBX and PNG are LFS. Not in the build. No GUID references in.
- **Used by:**
  - `Editor/HeroV6Import.cs` (menu "Golf Arcade/Hero V6/Import As Humanoid")
  - `Tests/PlayMode/HeroV6RetargetTests.cs` (`[UnityTest, Explicit]`; it also loads the Hero01 prefab)
- **What would break if deleted:** that Explicit test (`Assert.IsNotNull(src,"V6 model")`) and the import menu.
- **Verdict: SAFE+FIX.** Delete `HeroV6RetargetTests.cs` and `HeroV6Import.cs` with it.
- **Confidence:** high.

### UASSET-05: `Unity/Assets/ArtDirection/HeroV6Polish/`, the V6 polish pass
- **Paths:** 30 files: `Hero_V6_Polish.fbx`, 6 prefabs, 21 materials, `Hero_V6_Materials.json`, `Hero_V6_EmptyHair.asset`.
- **Size:** 9.3 MB. Tracked; FBX is LFS. Not in the build. No GUID references in.
- **Depends on:** Hero01 (44 references).
- **Used by:**
  - `Editor/HeroV6PolishImport.cs` and `Editor/HeroV6PolishReview.cs` (+ `HeroV6PolishBeforeReview.cs`).
  - Runtime scripts attached only to these prefabs: `HeroV6PolishContract.cs`, `HeroV6PolishHairLOD.cs`, `HeroV6KneeCorrectives.cs`. Their only code references are each other and those two editor tools.
- **What would break if deleted:** those tools. The 3 runtime scripts become dead (unity-code).
- **Verdict: SAFE+FIX.** Retire the tools and scripts with it.
- **Confidence:** high.

### UASSET-06: 8 Hero01 review scenes in `Unity/Assets/Scenes/`
- **Paths:**
  - `Hero01Look.unity`, `Hero01OwnedTennis.unity`, `Hero01GameplayFixed.unity`, `Hero01KneesOffArm.unity`
  - `Hero01BackhandRun.unity`, `Hero01AnimRepair.unity`, `Hero01FullStroke.unity`, `Hero01RestoreMotion.unity`
- **What they are:** stage scenes saved by the Sept 26 Hero01 review/capture tools (one per review round).
- **Size:** 1.92 MB, about 236 KB each. Tracked; not LFS. Not in the build settings.
- **Opened only by:**
  - `HeroLookReview`, `HeroOwnedReview`, `HeroGameplayPlaybackReview`, `HeroKneesOffArmReview`, `HeroBackhandRunReview`, `HeroAnimRepairReview`
  - `HeroFullStrokeReview`, `HeroRestoreMotionReview`, `HeroLegacyReview`, `HeroGameplayFixCapture`
  - `HeroLockerExport` (`Hero01RestoreMotion`), `HeroGameplayBuild`
  - No test, script or other scene opens them.
- **What would break if deleted:** those legacy tools. The scenes reference Hero01 assets and `ModularHeroLook` / `HeroLookLighting`.
- **Verdict: SAFE+FIX.** Delete together with UASSET-02's tool set.
- **Confidence:** high.

### UASSET-07: `Unity/Assets/Scenes/TennisReference.unity`, the reference-matching review scene
- **What it is:** an empty scene containing a `TennisGame` (`ManualSimulation`) and a `TennisReferenceMatch` component. It is used to match the game camera to the generated reference video. It is kept out of the build on purpose.
- **Size:** 6.4 KB. Tracked. Not in the build.
- **Used by:**
  - `Editor/TennisReferenceSceneBuilder.cs`: menu "Golf Arcade/Tennis/Open Reference Match Scene", which **rebuilds the scene if it is missing**.
  - `Tests/PlayMode/TennisReferenceCaptureTests.cs:121,151`: 2 `[Explicit]` tests using `LoadSceneAsyncInPlayMode`. The other tests in that file load "Tennis".
  - Runtime `Scripts/Tennis/TennisReferenceMatch.cs`, which ships in the game but is only used by this scene.
- **What would break if deleted:** nothing until someone runs those 2 explicit tests, and the menu regenerates the scene.
- **Verdict: DECIDE.** If the reference-matching pass is finished, remove the scene, the builder, `TennisReferenceMatch.cs` and the 2 explicit tests together.
- **Confidence:** high.

### UASSET-08: `Unity/Assets/Characters/HeroBase/` static bases (everything except UASSET-09/10)
- **Paths:** 39 files:
  - `Models/HeroBase_{Male,Female}_Body.fbx`, `Models/Hair_Default_{Male,Female}.fbx`, `FemaleHeadF1/HeroBase_Female_Body_F1.fbx`
  - 10 prefabs (`HeroBase_*`, `Bodies/`, `Hair/`, `Previews/`, `FemaleHeadF1/`)
  - `HeroBase_Studio.unity`, `Textures/Head_{Male,Female}_Albedo.png`, `README.md`
  - 20 materials in `Materials/` that are not `Male_*` (`Base_Grey*`, `Brow`, `Ear_Shadow_*`, `Eye_White`, `Hair_F/M`, `Iris_Dark`, `Lip*`, `Mouth*`, `Pupil`, `Skin_F/M`, `Teeth`)
- **What it is:** the static grey "base_lock" mannequins (no rig). They are the Unity proof of the HeroBase plate-fidelity work (Sept 30–Oct 2). Their own gate says FAIL.
- **Size:** 4.66 MB. Tracked; FBX/PNG are LFS. Not in the build. No GUID references from outside.
- **Used by:**
  - Proof tools: `HeroBaseStaticImport`, `HeroBaseMaleFormProof`, `HeroBaseFemaleHeadProof`, `HeroBaseSkinProof` (batch-only; last called from `work/male-micro1l-head`, `work/male-nape-pelvis-skin` on Oct 1–2).
  - `Tools/HeroBase/{verify_delivery,package_review,proof_male_form}.py` and a few one-off `work/` scripts.
  - `blender/scripts/fetch_matchheroes.sh:15` downloads `Characters/HeroBase/Models/Hair_Default_{Male,Female}.fbx`, but from **origin/newmapsandmenus** over LFS, not from this working tree. They feed `matchhero_golf.py`, which builds `Resources/Hero/golfer_*.fbx`.
- **What would break if deleted:** the 4 proof tools and the `Tools/HeroBase` scripts. The golf hair fetch only breaks if the files are also removed on that branch.
- **Verdict: DECIDE.** AGENTS.md names HeroBase / base_lock as the "LOCKED" character authority, so these are its Unity proof copies. Nothing in the game uses them.
- **Confidence:** high.

### UASSET-09: HeroBase face and skin materials (KEEP)
- **Paths:** 32 files: `Characters/HeroBase/Materials/Male_*.mat` (16) and `Characters/HeroBase/FemaleHeadF1/Materials/Female_*.mat` (16).
- **Size:** 0.13 MB. Tracked. Not in the build.
- **Why they look dead but are used:** `Editor/MatchHeroBuild.cs:98,133` (`FaceMaterial`, `LoadMat`) copies them into `Characters/MatchHeroes/Materials/MatchHero_*`. It **throws `FileNotFoundException("material missing")`** if they are gone. That is the builder behind "Golf Arcade/Match Heroes/Build Prefabs", which rebuilds `PlayerMale` / `PlayerFemale`. The materials only reference URP Lit, with no HeroBase textures.
- **Verdict: KEEP.** If UASSET-08 is deleted, keep these, or move them and update the 2 paths in `MatchHeroBuild.cs`.
- **Confidence:** high.

### UASSET-10: HeroBase studio render pipeline (KEEP)
- **Paths:** `Characters/HeroBase/Rendering/HeroBaseStudioURP.asset` and `HeroBaseStudio_Renderer.asset` (2 files, 10 KB). Tracked. Not used by any quality level (all 6 levels use TennisURP).
- **Why they look dead but are used:**
  - `Editor/GolfLookSmoke.cs:2533-2566` (`PipelineAssets` / `BothAssets`) runs the golf look gates under both URP assets and records `"... missing"`, which fails the gate, if this one is absent. `GolfLookSmoke.Run` is invoked by 8 `work/` scripts.
  - `GolfPlantSwayCheck.cs:79-85` runs a second GPU pass with it (and skips if it is missing).
  - `PostcardLookStills` / `PostcardV3ProofMasks` have a `GOLF_*_PIPELINE` option that uses it.
  - The HeroBase proof tools.
- **Verdict: KEEP**, or SAFE+FIX by dropping it from `GolfLookSmoke.PipelineAssets`.
- **Confidence:** high.

### UASSET-11: `Unity/Assets/Characters/Archive/OldPlayerBase/`, the old Higgsfield tennis bases
- **Paths:** 2 files: `PlayerMale.fbx`, `PlayerFemale.fbx`.
- **What it is:** the pre-match-hero tennis bodies. `MatchHeroBuild.ArchiveOldBases()` moved them out of `Resources/Tennis/Customization/` on Oct 3.
- **Size:** 0.99 MB. Tracked; LFS. Not in the build. No GUID references.
- **Evidence it is unused:**
  - `Scripts/Tennis/TennisCustomization.cs:29` says "archived … and are not loaded".
  - `GolfArcadeTests/MatchHeroLockerTests.swift:63` asserts the old base is not in the app.
- **What would break if deleted:** nothing. `MatchHeroBuild.Run` recreates an empty `ArchiveDir` and only moves files if the source still exists, which it does not.
- **Verdict: SAFE.** Optional fix: remove `ArchiveOldBases()` / `ArchiveDir` from `MatchHeroBuild.cs:21,45,48,63-73`.
- **Confidence:** high.

### UASSET-12: `Unity/Assets/Archive/IconKit/`, the retired "3D icon" avatar kit
- **Paths:** 70 files:
  - 13 FBX (`hero_body`, `head`, `hair`, `hats`, `glasses`, …)
  - `Look/` (6 PNG atlases + `hero_kitref.json`)
  - `UILook/` (49 old locker tile PNGs)
  - `hero_golf_clips.json`
- **What it is:** the avatar kit. `docs/avatar-kit.md` says "2026-10-04: retired from the game … FBXs and tile pictures are in Unity/Assets/Archive/IconKit/". It was kept in case its hats, glasses and hair are refitted later.
- **Size:** 19.0 MB. Tracked, but **not LFS**: stored as plain git blobs (10.3 MB FBX + 8.5 MB PNG) despite `*.fbx`/`*.png` LFS rules. Not in the build.
- **Evidence it is unused:** no GUID references in, and no code or script references. Only docs mention it (`avatar-kit.md`, `matchhero-golfer.md`, `hero-golfer.md`). The live locker tiles are in `Resources/UI/Look` (25 PNG).
- **What would break if deleted:** nothing. Sources can be regenerated with `blender/scripts/avatar_*.py`. Deleting will not shrink git history.
- **Verdict: DECIDE** (it was kept on purpose as an archive).
- **Confidence:** high.

### UASSET-13: `Unity/Assets/Tests/Fixtures/StandardCharacters/` (KEEP)
- **Paths:** 6 FBX: `{male,female}_{tennis,bowling,boxing}.fbx`.
- **Size:** 14.6 MB. Tracked; LFS. Tests do not ship.
- **Why it looks dead but is used:**
  - `Tests/EditMode/StandardArmSportTests.cs:19` loads all 6 in a normal, non-Explicit EditMode test (`Assert.IsNotNull`).
  - `Editor/GolferModelImporter.cs:13` gives them the standard-rig import.
  - `StandardCharacterArms` is still live at runtime: `TennisActor.cs:245` loads `StandardCharacters/standard_*_tennis`.
  - Regenerate with `blender/scripts/export_arm_validation.py`.
- **Verdict: KEEP** (remove only together with the StandardCharacters feature).
- **Confidence:** high.

### UASSET-14: `Unity/Assets/Editor/GolfPlantSwayFixture/` (KEEP)
- **Paths:** 2 FBX: `sway_fixture.fbx`, `plants_lib_fixture.fbx`.
- **Size:** 0.23 MB. Tracked; LFS. Editor-only.
- **Used by:** `Editor/GolfPlantSwayCheck.cs:322,669`, the GPU sway gate run by `work/postcard-look/v2/sway/run_swaycheck.sh`. Regenerated by `work/postcard-look/v2/sway/blender/make_lib_fixture.py`.
- **Verdict: KEEP.**
- **Confidence:** high.

### UASSET-15: Editor-only shaders (KEEP)
- **Paths:** `Unity/Assets/Editor/DiagnosticHandSurface.shader` and `ClothProofMask.shader` (2 files, 2.5 KB). Tracked. Never shipped (Editor folder).
- **Used by:** `Shader.Find` in `Editor/HeroThumbRigProbe.cs:142` and `Editor/ClothProofRig.cs:117`.
- **Verdict: KEEP.** Their fate follows those two probe tools (unity-code).
- **Confidence:** high.

### UASSET-16: Root assets (KEEP)
- **Paths:** `Unity/Assets/DefaultVolumeProfile.asset` (20 KB), `UniversalRenderPipelineGlobalSettings.asset` (26 KB), `link.xml` (0.4 KB). All tracked.
- **Why they are used:**
  - `GraphicsSettings` references the URP global settings.
  - The global settings reference the volume profile (`m_VolumeProfile`). Both ship.
  - `link.xml` has no GUID references. IL2CPP reads it automatically, and it protects Sphere/Capsule/Box colliders from stripping (13 `CreatePrimitive` calls in Scripts; `stripEngineCode: 1`).
- **Verdict: KEEP.**
- **Confidence:** high.

### UASSET-17: `Unity/Assets/InitTestScene*.unity` (+ `.meta`), leftover test-run scenes
- **Paths:** 3 scenes + 3 metas: `InitTestScene2f5c2859…` (Sept 27), `ef4937dc…` (Sept 27), `c6c4777f…` (Oct 10 16:11).
- **What they are:** temporary scenes the Test Framework creates for each PlayMode run. They are left behind when a run is interrupted. `Temp/__Backupscenes/0.backup` (16:11, contains `PlaymodeTestsController`) shows the Oct 10 run was cut off.
- **Size:** 18 KB. **Not tracked** (`.gitignore:20-21`). Not in the build.
- **What would break if deleted:** nothing. They only reference the test-framework controller.
- **Verdict: SAFE.** Delete only when no Unity test run is in progress.
- **Confidence:** high.

### UASSET-18: Live character folders (KEEP)
- **Paths:**
  - `Characters/MatchHeroes/**` (91 files: 52 FBX + 39 materials, 145.2 MB)
  - `Characters/MatchHeroKit/` (2 FBX, 8.2 MB)
  - `Characters/GolfHeroKit/` (2 FBX + 6 materials, 9.4 MB)
- **Status:** all tracked; FBX are LFS. All 101 files are in the build through `Resources/Tennis/Customization/Player{Male,Female}{,GolfKit}.prefab`: 79.6 MB packed (MatchHeroes 37.3, GolfHeroKit 31.6, MatchHeroKit 10.7).
- **Notes:**
  - The FBX are copies of `work/` exports. `MatchHeroImport.CopyAndImport` copies from `../work/match-anim-set/export/fbx/`, and `MatchHeroKit` from `../work/cloth-overhaul/export/`.
  - `MatchHeroBuild` also needs `../work/hero-mainstay/data/clip_table.json` and `../work/classic-tennis-racket/export/unity_stage/Materials/`.
  - `blender/scripts/fetch_matchheroes.sh` fetches the same paths from origin/newmapsandmenus.
- **Verdict: KEEP.**
- **Confidence:** high.

### UASSET-19: Build scenes `Golf.unity`, `Tennis.unity`, `Hub.unity` (KEEP, but Hub is uncommitted)
- **Size:** about 5 KB each. Everything else is built at runtime.
- **Risk:** `Hub.unity` (+ meta) is **untracked**, and the `EditorBuildSettings.asset` change that adds it is **uncommitted**. A `git clean` or checkout would silently remove a build scene. `Scripts/Hub/*.cs` are untracked too.
- **Verdict: KEEP** (commit Hub).
- **Confidence:** high.

---

## B. Unity project clutter (on disk)

### UASSET-20: `Unity/mono_crash.*`, Unity editor crash dumps
- **What they are:** 45 files: 9 `mono_crash.*.json` crash reports (Sept 26–Oct 5) and 36 `mono_crash.mem.47378.*.blob` memory dumps (Sept 28).
- **Size:** 28.2 MB. Not tracked (`.gitignore:19`). Nothing references them.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-21: `Unity/work/`, leftover compile-check output folders
- **What they are:**
  - `golf-party-check/`, `postcard-refresh-check/`, `presentation/Check/`: 154 files and about 36 MB each, Oct 8 17:19–21:42. They are the output of `OUT=work/<x> Unity/Tools/check.sh`: copies of Unity's `UnityEngine*.dll`, freshly compiled `GolfArcade*.dll`, `TestRunner.dll` and `nunit`. The default `OUT` is `Library/Check`.
  - `multiplayer/editmode.xml`: an Oct 4 EditMode result (225 tests, 1 failed).
- **Size:** 463 files, 108 MB. **Untracked and not gitignored**: shows as `?? Unity/work/` in `git status`. It is outside `Assets/`, so Unity ignores it.
- **Evidence it is unused:** nothing references it. `SportsLibrary/MULTIPLAYER.md` points at the repo-root `work/multiplayer/`, not this one.
- **Verdict: SAFE.** Optional fix: add `/work/` to `Unity/.gitignore`, or keep `check.sh` output under `Library/`.
- **Confidence:** high.

### UASSET-22: `Unity/Builds/iOS/`, the Unity iOS Xcode export
- **What it is:** the Unity iOS export: Data 1.4 GB, Il2CppOutputProject 907 MB, Libraries 220 MB.
- **Size:** 2.5 GB, 3,173 files. Not tracked (`/[Bb]uilds/`).
- **Needed by:**
  - `project-unity.yml`: the `Unity-iPhone.xcodeproj` project reference and the `Data` folder copied as app resources → `GolfArcadeUnity.xcodeproj` / `.xcworkspace`
  - `Unity/Tools/build-integrated-ios.sh` (exits if it is missing)
  - Regenerate it only with "Golf Arcade > Build iOS Xcode Project" (`BuildMenu.BuildIOS`, a long IL2CPP build).
- **Stale:** exported Oct 8 21:38–22:01 with only 2 scenes (`level0`, `level1` = Golf, Tennis; no Hub). It still contains the Hero01 assets (UASSET-01) and the unused services DLLs (UASSET-28).
- **Verdict: DECIDE.** Deleting frees 2.5 GB, but the integrated app cannot be built until you re-export. You need to re-export anyway to get Hub in.
- **Confidence:** high.

### UASSET-23: `Unity/Library/` caches (everything except Captures, see UASSET-24)
- **What it is:** Unity's regenerable import and build caches:
  - Artifacts 2.2 GB, PlayerDataCache 1.4 GB, PackageCache 1.3 GB
  - Bee 306 MB, BurstCache 137 MB, ArtifactDB 92 MB, ShaderCache 88 MB
  - ScriptAssemblies 29 MB, Search 22 MB, SourceAssetDB 17 MB, …
- **Mixed in are small tool outputs:**
  - `Check/` (37 MB, `check.sh` default output)
  - `TropicalReview/` (11 MB), `TestResults/` (6.6 MB, written by the Test menu)
  - `LastBuild.buildreport` (5.3 MB, the only per-asset record of what the app ships)
  - `ios-export.log`, `BuildResults/`
- **Size:** about 5.9 GB of 11 GB. Not tracked (`/[Ll]ibrary/`).
- **What deleting costs:** a full reimport on next open (hours: 2.4 GB of Resources, LFS FBX) and a fresh package download. `Unity/Tools/check.sh` will not work until the editor has re-imported, because it needs `Library/ScriptAssemblies` and `PackageCache`.
- **Verdict: SAFE.** Regenerable; delete only with the editor closed.
- **Confidence:** high.

### UASSET-24: `Unity/Library/Captures/`, capture output (not a cache)
- **What it is:** 36 subfolders plus 5 loose JPGs of frames and stills (review, hero, locker, golf-polish, golf-presentation, rally-film, …), updated up to Oct 10. They are written by 36 PlayMode/EditMode test files, 2 editor tools, `Unity/Tools/demo_video.sh` and `swing_video.py`, and cited in `Unity/README.md` and several docs.
- **Size:** **5.1 GB**. Not tracked (inside Library).
- **What deleting costs:** old captures are lost; they can only be remade by re-running the capture tests. Deleting `Library/` wholesale also deletes these.
- **Verdict: DECIDE.** Safe if nobody needs the old images; archive first if they back up a proof.
- **Confidence:** high.

### UASSET-25: `Unity/Logs/` and `Unity/Temp/`
- **Logs:** 5 shader-compiler logs, 30 KB.
- **Temp:** a stale `UnityLockfile` (no editor is running), `Burst`, and `__Backupscenes/0.backup` (the interrupted test-run scene), 21 KB.
- Neither is tracked.
- **Verdict: SAFE** (with the editor closed).
- **Confidence:** high.

### UASSET-26: `Unity/UserSettings/`
- **What it is:** per-user editor layout, search index and settings, 35 KB. Not tracked.
- **Verdict: KEEP.** It is harmless; deleting only resets the editor layout.
- **Confidence:** high.

### UASSET-27: `Unity/README.md`, partly out of date
- **Size:** 12.9 KB, 186 lines. Tracked.
- **Still correct:** Unity 6000.3.24f1, bundle id `com.aloniewski.golfarcade.unity`, iOS 17+ portrait, the `check.sh` and build sections, and the MCP for Unity setup.
- **Stale:**
  - It describes a golf-only "Golf Arcade — Unity" port. There is no mention of Tennis, the walkable Hub, or the 3 build scenes.
  - It says golf uses "our permanent V4 male/female characters" on branch `feature/standard-characters-integration`. That was replaced by the match heroes (`docs/matchhero-golfer.md`).
  - It lists courses as "Cliffside (holes 7, 12–15) and Wild Isles (16–20)". The code (`Course/Hole.cs:289-315`) now has 3 courses: Cliffside 7/8/9/12/15, Wild Isles 13/14/17/18/19/20, Magma Open 10/16/21/22/23.
  - The Layout section has no `Scripts/Tennis`, `Hub`, `Multiplayer` or `Characters`.
  - The Tests section says "a PlayMode smoke test"; there are now 43 EditMode and 68 PlayMode test files.
- **Verdict: KEEP**, with a follow-up to update the stale sections.
- **Confidence:** high.

**`.gitignore` coverage:** no Library, Temp, Logs, UserSettings, Builds, mono_crash, InitTestScene, `.csproj` or `.sln` file is tracked. The only gap is `Unity/work/` (UASSET-21).

---

## C. Unity packages (`Unity/Packages/manifest.json`, `packages-lock.json`)

**Ground truth:**
- All project C# is in 4 asmdefs.
- The runtime `GolfArcade.asmdef` references only `Unity.RenderPipelines.Universal.Runtime` and `Core.Runtime`, so game code cannot even reach Timeline, NavMesh, TMP, Services, XR or MCP types.
- The editor and test asmdefs add only TestRunner and URP editor.
- No project asset (`.unity`, `.prefab`, `.mat`, `.asset`, ProjectSettings) references any GUID from: analytics, services, timeline, ai.navigation, multiplayer.center, collab-proxy, xr.legacyinputhelpers, 2d.sprite, 2d.tilemap, unity-mcp or TMP.
- In the editor, about 40 of the 84 compiled script assemblies in `Library/ScriptAssemblies` (about 7.7 MB) come from these optional packages or bundled extras (analytics/services, timeline, navigation, Plastic, MCP, multiplayer center, 2D, XR, IDEs, TMP). Removing them means fewer assemblies to load on each domain reload (medium confidence on how much faster that gets).
- Not installed, so nothing to remove: Cinemachine, Input System, Visual Scripting, Recorder, ProBuilder, FBX Exporter, Addressables.

### UASSET-28: `com.unity.analytics` 3.8.2 (pulls `com.unity.services.analytics` 6.3.0 and `com.unity.services.core` 1.18.0)
- **Evidence it is unused:** no code uses `UnityEngine.Analytics` or `Unity.Services` (the Swift app's `Analytics.track` is its own Swift type). `UnityConnectSettings` has Analytics `m_Enabled: 0`.
- **But it is in the shipped app:**
  - 15 managed DLLs (14 `Unity.Services.*` + `Unity.Analytics.DataPrivacy`) plus `Newtonsoft.Json` in `ScriptingAssemblies.json`
  - **8 startup hooks** that run on every launch (`UnityServicesInitializer.EnableServicesInitializationAsync`, `CorePackageInitializer`, …)
  - 3 native plugins (`VolumeIOSPlugin.mm`, `unity_services_locale.mm`, `NSUserDefaults.mm`)
  - About 2.8 MB of generated IL2CPP C++ for Services, plus 14.2 MB for Newtonsoft (kept alive by services.core)
- **Removing it:** removes the services DLLs, hooks and plugins from the app. Newtonsoft's generated code should be stripped too, though MCP still references the DLL (medium confidence on that part). Newtonsoft stays in the editor for MCP. Nothing in the game changes.
- **Size:** about 6.3 MB in PackageCache.
- **Verdict: SAFE.** Re-check the exported privacy manifest after the next export.
- **Confidence:** high.

### UASSET-29: `com.unity.timeline` 1.8.13
- **Evidence it is unused:** no `PlayableDirector` or Timeline assets or code. The 9 files using `UnityEngine.Playables` / `PlayableGraph` use engine-core APIs, not this package. `Unity.Timeline.dll` ships but generates no code.
- **Size:** 41 MB in PackageCache.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-30: `com.unity.ai.navigation` 2.0.14
- **Evidence it is unused:** no NavMesh, NavMeshSurface or `UnityEngine.AI` usage anywhere. It still ships `Unity.AI.Navigation.dll` plus 4 startup hooks.
- **Size:** 13 MB in PackageCache.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-31: `com.unity.multiplayer.center` 1.0.1
- **What it is:** a Unity 6 template default; an editor window. Its `Unity.Multiplayer.Center.Common.dll` ships. The game's multiplayer is its own (`Scripts/Multiplayer`, `server/`).
- **Size:** 1.2 MB in PackageCache.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-32: `com.unity.collab-proxy` 2.13.6
- **What it is:** Unity Version Control (Plastic SCM) integration, editor-only. The repo uses git.
- **Size:** 44 MB in PackageCache; 3 PlasticSCM editor assemblies.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-33: `com.unity.xr.legacyinputhelpers` 2.1.13
- **Evidence it is unused:** no XR, `TrackedPoseDriver` or `SpatialTracking` usage.
- **Size:** 1 MB.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-34: `com.unity.2d.sprite` 1.0.0 and `com.unity.2d.tilemap` 1.0.0
- **What they are:** the Sprite Editor and Tilemap editor tooling. There are no tilemaps; UI sprites are created in code with the engine's `Sprite.Create`.
- **Size:** 4.1 MB.
- **Verdict: SAFE.**
- **Confidence:** high.

### UASSET-35: `com.coplaydev.unity-mcp` (git URL, v10.2.0)
- **What it is:** "MCP for Unity", which lets an agent drive the editor (documented in `Unity/README.md`). No `.mcp.json` or `.claude` config in the repo registers its server.
- **Side effects:**
  - It is a GitHub git dependency, so a fresh clone or CI needs network access.
  - It ships `MCPForUnity.Runtime.dll` in the app (stripped to nothing).
  - It runs an editor bridge.
- **Size:** 5.5 MB.
- **Verdict: DECIDE.** Keep it if you or agents use Unity MCP.
- **Confidence:** medium.

### UASSET-36: `com.unity.ide.rider` 3.0.40 and `com.unity.ide.visualstudio` 2.0.26
- **What they are:** editor-only IDE integration. Zero effect on the game.
- **Size:** 2.7 MB.
- **Verdict: DECIDE.** Keep whichever IDE you use.
- **Confidence:** high.

### UASSET-37: Required packages (KEEP)
- `com.unity.render-pipelines.universal` 17.3.0, with core, shadergraph, universal-config, burst, collections and mathematics (all URP dependencies; `Unity.Mathematics` is also referenced by `check.sh`).
- `com.unity.ugui` 2.0.0: `UnityEngine.UI` is used in 38 files. TextMeshPro is bundled inside it and unused, but cannot be removed separately.
- `com.unity.test-framework` 1.6.0 (+ `ext.nunit`). The transitive `test-framework.performance` came in through collections. FYI: it wrote `Assets/Resources/PerformanceTestRunInfo.json` into the Oct 8 build (negligible size).
- **Verdict: KEEP.**
- **Confidence:** high.

### UASSET-38: Engine modules `com.unity.modules.*` (KEEP)
- **Used:** video (`Scripts/Hub/HubBayScreen.cs` VideoPlayer), audio, particlesystem, physics, unitywebrequest, imageconversion, jsonserialize, screencapture, ui, animation, director.
- **No direct use:** cloth, vehicles, wind, umbra, vectorgraphics, accessibility, adaptiveperformance, unityanalytics, assetbundle, tilemap, xr, vr. Some are dependencies of packages above: terrain (RP core), androidjni (services), physics2d (MCP).
- **Why keep:** with `stripEngineCode: 1`, unused modules are already stripped from the iOS binary, so removing them only tidies the list and risks breaking package dependencies.
- **Verdict: KEEP** (low value).
- **Confidence:** medium.

---

## Cross-area links (for the other auditors)

- **app-resources:**
  - `Resources/Tennis/Hero/Hero_01_Tennis.prefab` is the only reason `ArtDirection/Hero01` (82.8 MB packed) ships.
  - `Resources/Tennis/Hero/HeroV4_KitMask.png` (4.2 MB packed) and `HeroV4_KitRef.json` have no runtime reader; they were written by the legacy `HeroLockerExport`.
  - The per-asset build sizes are in `audit/unity-assets-other/build_report_packed_sizes.csv`, for example `Resources/StandardCharacters` 165 MB and `Resources/Hero` 50 MB.
- **unity-code:**
  - Legacy editor tools are the only users of the UASSET-02 to 08 assets:
    - Hero01 set (16 tools): `HeroLookImporter`, `HeroLookReview`, `HeroOwnedReview`, `HeroRestoreMotionReview`, `HeroGameplayFixCapture`, `HeroGameplayPlaybackReview`, `HeroAnimRepairReview`, `HeroBackhandRunReview`, `HeroFullStrokeReview`, `HeroKneesOffArmReview`, `HeroLegacyReview`, `HeroGameplayBuild`, `HeroLockerExport`, `HeroIntegrityAudit`, `HeroV5Rebind`, `LegacyHero01`
    - V5/V6: `HeroIntegrityReview`, `HeroV6PolishBeforeReview`, `HeroV6Import`, `HeroV6PolishImport`, `HeroV6PolishReview`
    - HeroBase: `HeroBaseStaticImport`, `HeroBaseMaleFormProof`, `HeroBaseFemaleHeadProof`, `HeroBaseSkinProof`
    - `TennisReferenceSceneBuilder`
  - Runtime scripts attached only to legacy assets: `ModularHeroLook`, `HeroCosmetics`, `HeroLookLighting`, `HeroV6PolishContract`, `HeroV6PolishHairLOD`, `HeroV6KneeCorrectives`, `TennisReferenceMatch`.
  - `MatchHeroAB` has a legacy Hero01 mode.
  - `MatchHeroBuild` needs `HeroBase` materials (UASSET-09).
  - `GolfLookSmoke` needs `HeroBaseStudioURP` (UASSET-10).
- **tests-ci-server:**
  - `HeroV6RetargetTests` (Explicit) opens `HeroV6` and the Hero01 prefab.
  - `TennisReferenceCaptureTests` (2 Explicit tests) opens `TennisReference.unity`.
  - `StandardArmSportTests` (normal EditMode) needs `Tests/Fixtures`.
  - 36 test files write to `Library/Captures` (5.1 GB).
  - CI never runs Unity.
- **pipelines (work/, blender):**
  - The live hero builders read `work/match-anim-set/export/fbx`, `work/cloth-overhaul/export`, `work/hero-mainstay/data/clip_table.json` and `work/classic-tennis-racket/export/unity_stage/Materials`. Do not purge those if prefabs must be rebuildable.
  - `blender/scripts/fetch_matchheroes.sh` pulls `Characters/HeroBase/Models/Hair_Default_*.fbx` from origin/newmapsandmenus.
  - `export_arm_validation.py` regenerates `Tests/Fixtures`.
  - `work/postcard-look/v2/sway/blender/make_lib_fixture.py` regenerates `GolfPlantSwayFixture`.
  - `Unity/Tools/check.sh` with `OUT=work/…` created `Unity/work/`.
- **swift-app:**
  - The legacy `HeroLockerExport` (Hero01) is the generator of `GolfArcade/Unity/CharacterAssets/HeroV4*`, `HeroMenu*`, `HeroLounge*`, `HeroSwing_*`.
  - `MatchHeroLockerTests.swift:63` asserts those are no longer in the app bundle, so check whether the files on disk are orphans.
  - `project-unity.yml` needs `Unity/Builds/iOS` (UASSET-22).

---

## Summary table

| Verdict | Items (IDs) | On-disk size | Notes |
|---|---|---|---|
| SAFE+FIX | 01, 03, 04, 05, 06 | 40.4 MB on disk (01's files are counted under 02) | UASSET-01 alone removes **~83 MB from the shipped app** (move one prefab out of Resources) |
| SAFE | 11, 17, 20, 21, 23, 25, 28–34 | ~6.0 GB disk (Library caches 5.9 GB, `Unity/work` 108 MB, mono_crash 28 MB, OldPlayerBase 1 MB, small files) + ~111 MB PackageCache (packages) | 7 unused packages; analytics alone removes 15 Unity DLLs, 8 startup hooks and 3 native plugins from the app |
| DECIDE | 02, 07, 08, 12, 22, 24, 35, 36 | ~7.7 GB (Builds 2.5 GB, Library/Captures 5.1 GB, Hero01 90.6 MB, IconKit 19.0 MB, HeroBase static 4.7 MB, TennisReference 6 KB) + 8.2 MB packages | Archives kept on purpose, stale but needed build output, test captures, optional editor tooling |
| KEEP | 09, 10, 13, 14, 15, 16, 18, 19, 26, 27, 37, 38 | ~178 MB (MatchHeroes / MatchHeroKit / GolfHeroKit 162.7 MB, Fixtures 14.6 MB, the rest small) | Looks dead but is used: HeroBase materials (MatchHeroBuild), HeroBaseStudioURP (GolfLookSmoke gate), Fixtures (EditMode test), sway fixture, link.xml |

Total legacy hero material if you choose to drop all of it (02 + 03 + 04 + 05 + 06 + 08 + 11 + 12): **about 155 MB** in the Unity project (all tracked; most of it in LFS, but IconKit as plain git blobs). Of that, 82.8 MB packed currently ships in the app.
