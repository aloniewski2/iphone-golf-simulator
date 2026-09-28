# Character system audit and golf integration handoff

**Audited: 2026-09-28. Target branch: `tennisgameplaydone-needtofixcharacters`.**

## Start here — next agent brief

Implement the current modular toy-athlete characters in the receiving golf game, improve appearance and customization, and retain the existing tennis animations. Adnan considers tennis motion nearly finished; this is not authorization to replace it or restart hero generation. Start with one golf character, one complete driver swing and the default wardrobe. Prove grip/contact and preview parity before expanding cosmetics or sports.

This is a source/asset audit plus review of existing visual evidence, **not a fresh full runtime certification**. No golf migration or art repair was performed for this handoff. Historical documents use “V4” for two different character generations; do not choose assets by filename alone.

Read in order:

1. This document (current system and known gaps).
2. [Golf implementation plan](GOLF_IMPLEMENTATION.md).
3. [Acceptance checklist](ACCEPTANCE.md).
4. [Active prefab manifest](active-prefab-manifest.json): resolved direct GUID dependencies + SHA-256. Unity built-in GUIDs have no local path. This is not a recursive export package; include textures, scripts and shader dependencies too.
5. [Animation history](../../ArtDir/ANIMATOR.md), [look history](../../ArtDir/UNITY_LOOK.md), [modular design](../../ArtDir/hero/modular/MODULAR.md). Earlier “not integrated” sections are historical, not current truth.

The supplied [art bible](reference/art-bible.md) and [original agent brief](reference/original-agent-brief.md) are included for portability. Their historical phase stop rules describe earlier art phases; this handoff’s golf scope comes from Adnan’s latest request. Cosmetics remain identity-only.

## 1. Which characters are actually active?

| System | Current implementation | Use for golf handoff |
|---|---|---|
| Visible tennis player and rivals | `Unity/Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab`, attached by `TennisHeroSetup` | Primary current character/animation reference |
| Body and wardrobe | `Unity/Assets/ArtDirection/Hero01/Models/`, finger body in `Models/RestoreMotion/Hero_01_FingerBody.fbx`; one shared Humanoid skeleton | Preserve bind, bone names, UVs and slots while fixing selected meshes |
| Current tennis clips | `Models/RestoreMotion/Hero_*_v5.fbx` | Preserve these, not abandoned early retargets |
| iPhone/TV locker and menu avatar | `GolfArcade/Unity/CharacterModelPreview.swift`, `HeroV4` loader, `CharacterAssets/HeroV4.json/.bin` and atlas/masks | Separately baked SceneKit preview; must be regenerated after asset changes |
| Current golf | `GolferView` + `GolferStyle`; `Resources/StandardCharacters/standard_male_golf` / `standard_female_golf`; Generic clip `StandardGolfDrive` | Preserve golf control/contact semantics; this is not the tennis Hero |
| Older customizable player bases | `SportsLibrary/Customization/Bases/`, `Resources/Tennis/Customization/Player*`, `TennisCustomization` | Legacy golf/fallback path. Not the current visible tennis character |
| New generated V6 | `ArtDir/hero/v6_full/`, `Assets/ArtDirection/HeroV6/`, `HeroV6Import`, `HeroV6RetargetTests` | Experiment only; active tennis prefab dependencies do not reference it |
| Rejected and historical bodies | `_rejected_mannequin`, `v5/archive_V4`, raw blockouts, early review prefabs | Reference/rollback only; do not silently promote |

**Naming warning:** “standard V4” in `SportsLibrary/Animations/V4` describes the older standard characters. “locked POLISH V4” describes the later Hero look. Current Hero assets also include V5 hair/body improvements while keeping Hero_01/HeroV4 names. The serialized gameplay prefab and dependency manifest are the reliable baseline.

### Appearance and visual evidence

Target: stocky toy athlete, head about 20–25% height, peach skin, friendly inset eyes/catchlights, blonde chunky hair, thin visor, white/navy/orange polo, navy shorts, chunky shoes and blue racket. Variants currently use a `Female` blend shape plus haircut/headwear/color choices, rather than independent production bodies with unrelated rigs.

Review these local artifacts:

- [Locked target](../../ArtDir/plates/01_hero.png).
- [POLISH V4 comparison](../../ArtDir/screenshots/unity_hero_vs_plate01_v4.png).
- [Current TV menus and locker](../../ArtDir/review/tv-menus/TV_Intro_and_Menus.mp4), captured this audit session’s preceding task; locker near 25–31 seconds.
- [Multi-angle gameplay review](../../ArtDir/anims/multi_angle_review/Gameplay_AllAnimations_12Angles_2x.mp4). Earlier capture, useful motion baseline; not proof of every later edit.
- [Hair-fit example](../../ArtDir/hero/v5_proof/hairfit/Ponytail_None_34.png) and neighboring combinations.
- `ArtDir/hero/v5_proof/locker/`, `ArtDir/hero/v5_proof/styles/`, `ArtDir/score80/` contain additional historical tests; check dates and active assets before interpreting them.

Visible in reviewed evidence: the locker character reads as a coherent clothed toy athlete, but the hair/forehead join is irregular in the ponytail/no-hat review, with dark broken-looking edges near the band. Dark hair loses some clump separation. The large eye/head proportions and stiff crouched Ready pose dominate the menu avatar. The current source deliberately withholds bald/buzz/waves because the underlying head still needs repair. These observations are not a claim that every historical defect survives the latest build; reproduce each against the active prefab before editing.

## 2. Architecture and ownership

```text
Swift Player (Codable persistence)
  -> LockerStudio / TennisMenu controls
  -> SceneKit HeroV4 preview (exported, posed geometry)
  -> SportsSession start JSON
  -> SportsRuntime / iOS SportsBridge
  -> NativeSportsSession
       tennis: HeroKit.Style -> TennisGame.PlayerLook -> HeroKit.Apply
               TennisHeroSetup -> Hero_01_Tennis -> HeroTennisDriver
       golf:   GolferStyle -> GolferView -> StandardGolfDrive + legacy customization
```

Key code (paths relative to repo):

| Responsibility | Files |
|---|---|
| Appearance storage / migration | `GolfArcade/Domain/Player.swift` |
| Controls and preview | `GolfArcade/Unity/LockerStudio.swift`, `LockerColors.swift`, `CharacterModelPreview.swift`, `TennisMenu.swift` |
| Native payload | `GolfArcade/Unity/SportsSession.swift`, `Unity/Assets/Scripts/Game/NativeSportsSession.cs` |
| Hero assembly / tint / hats | `Unity/Assets/Scripts/Tennis/ModularHeroLook.cs`, `HeroKit.cs`, `HeroCosmetics.cs`, `TennisCustomization.RebindCosmetic` |
| Gameplay attachment | `TennisHeroSetup.cs`, `TennisGame.cs`, `TennisActor.cs` |
| Motion and procedural corrections | `HeroTennisDriver.cs`, `HeroBodyProxy.cs`, `HeroFace.cs`, `HeroHeadOccluder.cs` |
| Asset import / builds | `Unity/Assets/Editor/HeroLookImporter.cs`, `HeroGameplayBuild.cs`, `HeroV5Rebind.cs`, `HeroLockerExport.cs` |
| Golf legacy motion | `Unity/Assets/Scripts/Game/GolferView.cs`, `GolferStyle.cs`, `StandardGolfGrip.cs`, `StandardCharacterArms.cs` |

Important correction to old comments: `HeroTennisDriver` is no longer merely cosmetic. It redirects the actor’s strings to the visible racket and supplies `VisualContact` / `VisualTossPoint`. Changing socket, model scale or contact corrections can change where gameplay contacts occur. Preserve and test those contracts.

## 3. Customization: implemented vs missing

| Choice | Current Hero / locker | Golf gap |
|---|---|---|
| Boy/girl | `Female` blend shape on body and garments; preview filters baked Boy/Girl parts | Golf chooses separate older male/female assets |
| Skin | Discrete palette plus continuous `skinHex`; atlas tint | Golf uses discrete `skin`, ignores continuous hex in its style assignment |
| Hair color | `hairHex` / palette; material and atlas tint | Golf uses old integer palette |
| Haircut | Eight IDs; five offered while `HeadRebuild.shipped == false`: Swept, Ponytail, Bob, Long, Curly; Bald/Buzz/Waves gated | `GolferStyle` has no current Hero haircut field; legacy hair selection differs |
| Headwear | None, Visor, Cap, Sweatband; field misleadingly called `hairStyle` | Legacy `Hair` receives `hairStyle`; semantics are not equivalent |
| Shirt/shorts/shoes/racket tint | Supported; `accent` means shoes in current Hero flow | Golf receives outfit kit, but no Hero appearance parity |
| Body size | `Player.bodySize` saved and sent; legacy bases have size morphs | Current `HeroKit.Style` has no size field; current `HeroV4.build` does not apply size. A saved slider value is not proof of active Hero support |
| Face/height | Legacy fields exist | Current Hero style lacks these; do not expose inert controls |
| Handedness | Saved; locker mirrors preview; gameplay has separate handling | Validate full grip, contact side and swing behavior in golf, not just mirrored visuals |
| Clothing swap | Modular Shirt/Shorts/Shoes slots exist | Consumer menu mainly tints default clothes; no complete validated garment catalog |

Atlas contract: `HeroV4_KitMask` R=shirt, G=shorts/navy trim, B=baked hair, A=skin; reference luminance in `HeroV4_KitRef.json`. Skin also has a dedicated mask in the locker pipeline. Flat materials rely on names such as `Hero_01_HairTuft*`, `Hero_01_ShoeCleanWhite`, `Hero_Racket_Blue`. Renaming these without updating tint rules silently breaks customization. Alpha zero in a style color means retain authored default, not transparent skin.

`docs/character-customization.md` describes the older supplied-base pipeline and overstates parity for the current Hero. Treat it as legacy documentation, not the current implementation spec.

## 4. Rig, equipment, and import contracts

- One Animator/shared skeleton. Wardrobe slots: Hair, Hat, Shirt, Shorts, Shoes; runtime containers `Slot_*`. Requested attachment contract also includes Hat, Hand_R, Hand_L, Back, FaceExtra; verify actual bones/empties before assuming all exist.
- Hero humanoid map: Hips/Spine/Chest/Neck/Head, then Shoulder, UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes with `.L`/`.R` suffixes. Finger names include `Middle1.L/R`; finger grip code uses authored directions beyond the basic Humanoid mapping.
- Rebinding must follow each mesh’s **own bone-index order**, matching names onto the shared skeleton. Copying one renderer’s bone array to another caused serious deformation; see `HeroV5Rebind`.
- `HeroLookImporter` narrowly handles `Assets/ArtDirection/Hero01/`; bind and approved clips import as Humanoid, clothing as non-animated Generic meshes, clips copy the bind Avatar. Four influences/vertex, uncompressed motion, stable hierarchy.
- `GolferModelImporter` intentionally forces Generic for legacy standard/golf/customization paths. Do not broadly flip it to Humanoid. Use a new scoped golf-Hero path and explicitly valid Avatar.
- Runtime `ModularHeroLook` applies finger poses and grip roll. Tennis racket scale/contact markers and clearance live in `HeroTennisDriver`. Do not copy tennis racket offsets into a golf club.
- Unity uses linear blend skinning here. The legacy request for Blender Preserve Volume/Dual Quaternion is not satisfied by changing Unity Skin Quality; that setting controls influences, not a DQ deformation algorithm. Judge the Unity export, not just Blender.

## 5. Tennis motion to preserve

The active prefab binds 17 slots: Ready, Idle, Forehand, Backhand, Serve, Volley, Smash, RunForward, RunRight, RunLeft, HitPerfect, MissWhiff, CelebratePoint, SadPointLost, MatchWin, MatchLose, Walk. All source files resolve under `Models/RestoreMotion/Hero_*_v5.fbx`; see manifest.

`HeroGameplayBuild` reads contacts from `ArtDir/anims/restore_motion/unity_gameplay_config.json`. Runtime uses a manually evaluated PlayableGraph, base mixer plus masked upper-body stroke layer; clip sampling aligns authored contact to gameplay hit time. Current visual tempo default is 1.2; locomotion cadence uses actual travel (run reference 5.8 m/s, walk .96 m/s). Do not bake a blanket speed change into every clip.

The final look is the combination of FBX **and** procedural runtime: planted feet, body/arm reach, grip, two-hand handling, clearance, face and equipment contact. A raw FBX preview will not necessarily match gameplay. For another project, preserve the animations but replace the sport-specific controller with a documented adapter; never copy only a mesh and expect identical motion.

Rejected OWNED v1 is archived at `ArtDir/anims/_archived/hero_owned_failed_stylize_v1/`. Do not restore its arms-only motion. Historical backhand v1/v3/v4 and old review prefabs are not the current slot authority.

## 6. Priority findings

1. **P0 integration:** golf is not using the current Hero. Prove a Humanoid golf drive before migrating appearance UI.
2. **P1 parity:** skin/hair continuous colors, haircut/headwear IDs, body size and face fields diverge across native preview, tennis and golf. Publish one versioned appearance contract with stable IDs, not positional indices alone.
3. **P1 anatomy:** repair actual scalp/head coverage and haircut/hat joins; keep gated cuts hidden until front/back/top/low-angle tests pass. Do not stack opaque patches indefinitely.
4. **P1 deformation:** use shared mesh-local weights and consistent bind poses; test shoulders, knees, wrists and both club hands over complete strokes, at every body size.
5. **P2 resource lifetime:** `HeroKit.Apply` creates RenderTextures and cloned Materials; no ownership/release path is visible in that method. Add lifecycle ownership and profile repeated equips before claiming a leak-free wardrobe.
6. **P2 exporter drift:** native preview is baked geometry with SceneKit materials, not the live Unity skeletal renderer. Automate paired exports and a visual parity matrix.
7. **P2 reproducibility:** old scripts build earlier prefabs; blindly rerunning `HeroGameplayBuild.Build` may overwrite subsequent V5 mesh bindings. Snapshot dependencies, compare generated prefab, run rebind, then preview export deliberately.
8. **P2 portability:** capture scripts use machine-specific ffmpeg paths; parameterize them for the receiving agent. Assets require Git LFS.

## 7. Safe portability / first-party project integration

Copy with `.meta`: current Hero01 model/material/texture/prop dependencies, current prefab, relevant runtime and editor code, masks/shader resources, animation contact configuration. Keep `Resources/Tennis/Hero` and `Resources/Tennis/Shaders/KitRecolor` paths until you have replaced every string load. Shader pipeline must match URP (this project uses URP 17.x); old library instructions saying Built-in are historical. Check target Unity/package compatibility before importing into another golf project.

Do not copy the entire tennis game solely to display a golfer. Extract a small appearance assembly service from the current implementation once the baseline works; feed it an Animator, skeleton, wardrobe slots and appearance data. Keep TennisActor/TennisGame dependencies in the tennis adapter. Shared names can be cleaned up later, not during the first migration.

Treat provenance separately from filenames. Source material includes user-supplied GLBs, generated assets, historical mocap and archived experiments. Renaming Eyes Japan files does not remove source license obligations; consult `ArtDir/OWNED_MOCAP.md`, source records and receiving project requirements before distributing source assets. This audit does not certify external redistribution rights.

## 8. Suggested handoff prompt

> Read docs/character-handoff/README.md, GOLF_IMPLEMENTATION.md and ACCEPTANCE.md. Preserve the active tennis prefab/clip hashes. Implement the current modular Hero in golf using one shared Humanoid skeleton and a golf-specific PlayableGraph/contact adapter. First deliver default-wardrobe address→backswing→impact→finish with two-hand club grip and exact ball contact; then unify appearance serialization and implement visible size/hair/headwear/color support in both native preview and Unity. Fix character joins and deformation only against reproducible captures. Do not regenerate the hero, replace tennis swings or broadly rewrite legacy importers. Report tested versus untested combinations with videos.
