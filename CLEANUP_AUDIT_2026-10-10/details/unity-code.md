# Unity code audit: what is no longer used (unity-code)

Area: `Unity/Assets/Scripts/**` (254 game files, 56,000 lines), `Unity/Assets/Editor/**` (138 editor files, 22,364 lines, plus 3 more reached through a symlink), `Unity/Assets/Tests/**` (111 files, 14,920 lines), `Unity/Assets/Plugins/iOS/**`, every shader, the asmdefs, `link.xml` and `Unity/Tools/*`.
Branch `final-build` at 14a43331, working tree as it is on disk on 2026-10-10 (including the untracked Hub and hole-flow files). This was a read-only audit: nothing in the repo was edited, built, run or deleted.

## How this was checked

- **Declaration index.** Every C# type and method in the area was listed, together with which files mention it. Comments were stripped first. Strings were kept, so name lookups done by reflection or `SendMessage` still count as a use.
- **Reachability.** Type-level reachability was traced from the real starting points of the shipped game:
  - the components attached in the 3 build scenes (`Golf`, `Tennis`, `Hub`) and in the prefabs under `Resources/` (from the shared GUID index);
  - the `[RuntimeInitializeOnLoadMethod]` hooks (`NativeSportsSession`, `ErrorGuard`, `MuteWhenAutomated`, `HeroLightingProfile`);
  - the only native-to-Unity message (`GolfArcade/Unity/SportsRuntime.mm:124` sends `NativeSportsSession.Receive`).
  - Every result was then checked by hand. That caught three false "dead" readings that are in fact used: extension-method classes, `using static`, and a call placed right after an `#endif` line.
- **Dead branches inside live code.** These were found by reading the start-up paths:
  - the native start message;
  - the hero prefabs that actually ship, by checking their serialized `matchLook` / `look` fields;
  - the Swift commands that are actually sent (every `action` string in the Unity handler was looked up in `GolfArcade/**/*.swift`).
- **Editor tools.** Each one was classified by its hooks (`AssetPostprocessor`, `[InitializeOnLoad]`, `PostProcessBuild`), its menu items, the other editor code that uses it, and its `-executeMethod` callers. Those callers were found by searching `Unity/Tools`, `Tools`, `docs` and the `.sh/.py/.js` files in `work/` and `proof/` (`rg --no-ignore`, files under 2 MB, `Library/` and `review-project/` clones excluded).
- **Dates.** "git" below is `git log -1 --format=%cs`. Many files show the Oct 4/6/8 bulk-integration commits rather than their real last edit.
- **Tests.** Tests were not run (no Unity). The last recorded results come from `Unity/Library/TestResults/0.txt.running` (Oct 6) and `EditMode.txt.running` (Oct 10).
- **Reused from the unity-assets-other report** (UASSET-01 to 07): which legacy assets carry which scripts, and how big Hero01 is in the build.

Verdicts: **SAFE** = can go, nothing else changes. **SAFE+FIX** = can go after the named small edit. **DECIDE** = the owner's call (a tool or feature that may be wanted again). **KEEP** = looks dead but is used.

---

## A. Game code (`Unity/Assets/Scripts`)

### UCODE-01: The old hero generations (Hero01 / V4 / V5 / V6) still compiled into the game (SAFE+FIX)
- **Paths (10 files + part of 1):**
  - **No game path at all** (attached only to non-shipping assets):
    - `Tennis/HeroV6KneeCorrectives.cs` (67)
    - `Tennis/HeroV6PolishContract.cs` (46)
    - `Tennis/HeroV6PolishHairLOD.cs` (11)
    - `Tennis/V4GameplayArmRacket.cs` (55)
    - `Tennis/HeroLookLighting.cs` (13)
  - **Reached only through the old-hero branch of the tennis hero driver:**
    - `Tennis/ModularHeroLook.cs` (303)
    - `Tennis/HeroCosmetics.cs` (221)
    - `Tennis/HeroFace.cs` (207)
    - `Tennis/HeroHeadOccluder.cs` (119)
    - `Tennis/HeroBodyProxy.cs` (94)
  - **Inside `Tennis/HeroTennisDriver.cs`:**
    - the `!Match` branch at lines 181-193;
    - the hair and hat helpers: `HairTone` 1519-1536, `SealHeadUnderHair` 1540-1551, `SolidHairInterior` 1582-1618, `BuildHairCore` 1625-1670, `EnsureOwnScalpMaterial` 1672-1680, plus `Rewound`;
    - the `look` and `cosmetics` fields;
    - the old-body "torso kick" calls (461, 465, 484);
    - the environment-variable test switches `HERO_BASELINE`, `HERO_FLUID`, `HERO_FACE`, `HERO_HAIRFIX`.
- **What it is:** the code for the earlier tennis characters: the Hero01 "modular" body, its hats and haircuts, the painted face, the head occluder, the V6 test hero and the V4 arm fit. The game now uses only the two "match heroes".
- **Size:** about 1,300 lines in total: 192 + 944 in whole files, and about 170 inside `HeroTennisDriver`. All tracked.
- **Evidence:**
  - All 4 hero prefabs that ship are `Resources/Tennis/Customization/PlayerMale`, `PlayerFemale`, `PlayerMaleGolfKit` and `PlayerFemaleGolfKit`. They serialize `matchLook: {fileID: …}`, `look: {fileID: 0}` and `cosmetics: {fileID: 0}`.
  - The driver's switch is `bool Match => matchLook != null` (`HeroTennisDriver.cs:60`), so on these prefabs every `!Match` block is skipped.
  - `TennisHeroSetup.cs:8` says "The old Hero01 prefab is not loaded anywhere", and the driver itself says "until it is retired, the legacy look" (line 57).
  - The GUID index shows where the dead-path scripts are attached:
    - `HeroV6*` only on `ArtDirection/HeroV6Polish/*.prefab`;
    - `HeroLookLighting` only on the 8 `Scenes/Hero01*.unity` scenes;
    - `ModularHeroLook` and `HeroCosmetics` on `Resources/Tennis/Hero/Hero_01_Tennis.prefab`, which no runtime code loads (UASSET-01).
- **What depends on it:**
  - About 16 legacy editor tools would stop compiling (UCODE-22). The test `Tests/PlayMode/HeroV6RetargetTests.cs` (Explicit) would too.
  - `Hero_01_Tennis.prefab`, the ArtDirection V6 prefabs and the Hero01 scenes would show "missing script". Retire them together (UASSET-01 to 06).
  - Removing `Hero_01_Tennis.prefab` from `Resources/` also takes about 83 MB out of the app (UASSET-01).
- **Follow-up edit:**
  - In `HeroTennisDriver`, delete the `!Match` branches, the legacy helpers, the `look` and `cosmetics` fields and the env-var switches.
  - Keep `Match`-only behaviour.
  - Line 61 `Anim => matchLook ? … : look.animator` and lines 62-63 become plain `matchLook` reads.
- **Verdict:** SAFE+FIX. **Confidence:** high.

### UCODE-02: New-hero helper classes nothing calls (SAFE)
- **Paths (3 files + 1 method group):**
  - `Tennis/HeroEyeApertureForm.cs` (23): no reference anywhere.
  - `Tennis/HeroContinuousHandMesh.cs` (18) and `Tennis/HeroHandNormalFields.cs` (56): used only by the editor probe `Editor/HeroThumbRigProbe.cs`.
  - `Tennis/HeroThumbRigMigration.cs`: `BeforeSample`, `AfterSample`, `ToSourcePose` (lines 76-84) are never called.
- **What it is:** one-off face and hand authoring helpers from the Oct 8 character-finish pass.
- **Size:** 97 lines + 6 lines. All tracked (git 2026-10-08).
- **Evidence:** a name search over all C#, Swift and ObjC code finds no runtime caller.
- **What breaks:** `HeroThumbRigProbe.cs` if the two hand classes go. Remove that probe with them (UCODE-28).
- **Verdict:** SAFE (with the probe). **Confidence:** high.

### UCODE-03: Golf "instant replay" can never start any more (SAFE+FIX)
- **Paths:**
  - `Game/GolfGame.cs`: `ReplayWorthy` 2681-2689, `StartReplay` 2694-2709, `PlayReplay` 2711-2754, `ReplayCamera` 2756-2789, `EndReplay` 2791-2801, and the replay fields and `State.Replay` cases.
  - `UI/StrikeHud.cs`: `ReplayBadge` (lines 134 to end), plus `Hud.ShowReplay`.
  - `Tests/PlayMode/ReplayTests.cs` (79).
- **What it is:** the slow-motion replay of a great golf shot.
- **Size:** about 140 lines of game code + a 79-line test. All tracked.
- **Evidence:**
  - Commit 7ecf8100 (2026-10-06) replaced `if (ReplayWorthy()) StartReplay(); else AfterResult();` with `rig.FrameShotResult();`.
  - Since then nothing calls `StartReplay` or `ReplayWorthy`, so `State.Replay` is never entered.
  - `ReplayTests.APureApproachAndAHoledPuttAreReplayed` waits for `game.Replaying`, so it must now time out. It was not re-run here.
- **What breaks:** nothing in the game.
- **Follow-up edit:** delete `ReplayTests` (or restore the feature) along with the code.
- **Verdict:** SAFE+FIX. If Adnan wants replays back, this is the place to restore them. **Confidence:** high.

### UCODE-04: Editor-only "proof runners" sitting inside the game code folder (DECIDE)
- **Paths (3 files):**
  - `Course/GolfMovingTurfProofRunner.cs` (538)
  - `Course/PostcardRefreshProofRunner.cs` (121)
  - `Course/GolfVisualPhysicsGate.cs` (100)
- **What it is:** desktop proofs from the Oct 5 postcard-look and turf pass. They are all wrapped in `#if UNITY_EDITOR`, so they never reach the phone build.
- **Size:** 759 lines, tracked (git 2026-10-08).
- **Evidence:**
  - Nothing outside these files calls the runners.
  - `GolfVisualPhysicsGate` is opt-in through `Enabled` or the `GOLF_VISUAL_PHYSICS_GATE=1` environment variable. The two runners set it, and `GolfCourseLook.cs` calls it at lines 315/356/363/374 inside `#if UNITY_EDITOR`.
  - The only script mentioning them is `work/postcard-refresh/package_results.py`.
- **What breaks:** `GolfCourseLook.FinishModelSteps` and `FinishModel`, through the 4 gate calls (editor-only). Remove those calls too.
- **Verdict:** DECIDE (delete, or move into `Assets/Editor`). **Confidence:** high.

### UCODE-05: The old "phone as a club for the Mac" Wi-Fi mode (SAFE for 2 files, SAFE+FIX for the rest)
- **Paths:**
  - **Fully dead:**
    - `Game/AppLauncher.cs` (66): a MonoBehaviour that is not in any scene.
    - `Net/PhoneController.cs` (287): reached only from AppLauncher.
  - **Still running:**
    - `Net/NetworkMotionSource.cs` (136)
    - `Net/ControllerProtocol.cs` (144)
    - `Tennis/TennisPhoneInput.cs` (70): turns itself off under a native session.
  - **Hooks into it:** `Game/SwingController.cs` (`listenForController` and the `Network` source) and `GolfGame.cs` (address and hint text at about lines 1305-1330).
- **What it is:** the first desktop design, where the Mac showed the course and an iPhone running the Unity app was the club, linked over UDP on the local network.
- **Size:** 353 dead lines + 350 conditional lines. Tracked.
- **Evidence:**
  - `AppLauncher` has no scene or prefab reference (GUID index) and no code reference.
  - On the phone, `GolfGame.Awake` still builds `new SwingController(...)` with `listenForController=true`. `SwingController.Start()` calls `Network?.Start()` unconditionally.
  - So while the golf scene is loaded (it is build scene 0, loaded at Unity start-up), the app binds UDP port 47800 and sends a broadcast beacon to port 47801 every 0.5 s (`NetworkMotionSource.cs:108-116`).
  - Nothing speaks this protocol any more: the Swift app has no client for it, and real controller input arrives through `SportsBridge`.
  - Its last other use is `Tests/EditMode/MotionSwingDetectorTests.cs`, through `ControllerProtocol`.
- **What breaks:**
  - Removing `AppLauncher` and `PhoneController` breaks nothing.
  - Removing the listener needs these edits:
    - `SwingController` (drop the `Network` source) and `GolfGame.RefreshControls` (hint text, `Swing.UsingNetwork`, `ControllerButtons`);
    - `TennisPhoneInput` (the LAN part);
    - the ControllerProtocol cases in `MotionSwingDetectorTests`;
    - the stale Info.plist text in `Editor/BuildMenu.cs:46-53`: "finds your Mac on the Wi-Fi to use the phone as its club…" (cross-area).
- **Verdict:**
  - SAFE for `AppLauncher` and `PhoneController`.
  - SAFE+FIX for the listener. The minimum fix is to pass `listenForController:false` when a native session is active.
- **Confidence:** high.

### UCODE-06: The Unity-drawn stand-alone golf app layer (DECIDE)
- **Paths (13 whole files + parts of 5):**
  - **Whole files:**
    - `UI/HomeScreen.cs` (307: `HomeScreen`, `CourseScreen`)
    - `UI/Lobby.cs` (647)
    - `UI/Locker.cs` (863)
    - `UI/LockerColor.cs` (74)
    - `UI/ControllerSheet.cs` (433)
    - `Game/BigScreen.cs` (103)
    - `Game/ClubStage.cs` (198)
    - `Net/Online/OnlineSession.cs` (209)
    - `Net/Online/OnlineRoom.cs` (106)
    - `Net/Online/OnlineProtocol.cs` (53)
    - `Net/Online/BackendClient.cs` (176)
    - `Course/Championship.cs` (141)
    - `Profile/ChampionshipStore.cs` (46)
  - **Parts of files:**
    - `GolfGame.cs` sections "The menu", "Profiles … online", "The course screen", "The locker" and the online-room listeners (about lines 286-760, ~500 lines) and the tournament/online round recording;
    - `Hud.cs` menu, locker and controller-sheet hooks (~150 lines);
    - `UI/Club.cs` `ClubWipe` (509-553);
    - `UI/ArcadeCards.cs` `CardPop` (479-494);
    - `GameSetup` LocalVersus, Online and Tournament.
- **What it is:** the menus Unity used to draw itself before the native SwiftUI menus existed:
  - the golf home screen, course picker and golfer locker;
  - the profiles and online lobby, talking to a home server at `http://Andrews-MacBook-Pro-8.local:8080` (`BackendClient.cs:17`);
  - championships;
  - the Unity-managed AirPlay "big screen" and its phone controller sheet;
  - the painted clubhouse stage.
- **Size:** about 3,356 lines in whole files, about 4,100 including the parts. Tracked.
- **Evidence:**
  - `GolfGame.Awake` shows the menu only `if (!NativeSportsSession.Active)` (line 283).
  - Native golf goes through `PrepareNativeAddress`, which hides home, courses, locker and stage, disables BigScreen and always uses `GameSetup.Solo` (`GolfGame.Native.cs:16-29`).
  - Native multiplayer uses `SportsMultiplayer`, not `OnlineSession`.
  - The locker opens only from `State.Menu` (`OpenGolferPicker`).
  - **Caveat:** on the phone, `Golf.unity` is build scene 0 and loads at Unity start-up before any native "start" message arrives. So `ShowMenu()` probably runs once at boot and builds this UI behind the native menus, wasted work.
  - Its real users are editor play-mode testing and 8 PlayMode/EditMode tests (UCODE-33).
- **What breaks:**
  - `Lobby.PlayerColor` is used in play (`GolfGame.cs:994`, `2439`); move it before deleting `Lobby`.
  - `ClubStage` uses the `ClubBackdrop`/`ClubDisc` shaders and `Resources/Club` art (cross-area).
  - `BigScreen` is the only caller of the native `GolfAirPlay_ShowPicker` (UCODE-17).
  - Tests: UCODE-33.
  - `BuildMenu.cs:25` (`insecureHttpOption = AlwaysAllowed`) exists only for the home server.
- **Verdict:** DECIDE. Removing it is the biggest code win, but editor-only golf testing relies on it today. At minimum, skipping `ShowMenu()` on device would stop the boot-time build. **Confidence:** medium-high.

### UCODE-07: Dressing an invisible body: the hidden tennis gameplay rig's cosmetics (SAFE+FIX)
- **Paths:**
  - `Tennis/TennisCustomization.cs`:
    - `Apply` 89-167, which returns at line 93 on the tennis rig;
    - `AddMesh` 169-191;
    - the golf branch of `AttachBase` 40-57, which no caller ever uses (`golf` is always false);
    - `BodyScale` 17.
  - `Tennis/TennisActor.cs`: `WearKit` (215-233) and the `WearBody` stub (565, always `false`).
  - `Tennis/TennisLook.cs`: `RecolorKit` (90-125) and `ApplyCrowdPalette` (128-140, never called).
  - `TennisGame.ApplyOutfit` (899-903).
- **What it is:** kit recolouring and hair meshes for the old "Avatar" bodies. They are now applied to the hidden gameplay skeleton that only drives movement. The visible match hero gets its look from `HeroKit.Apply`.
- **Size:** about 170 lines. Tracked.
- **Evidence:**
  - `Apply`: "The hidden tennis gameplay rig wears no player base … nothing to dress" (line 92), so it returns immediately.
  - `AttachBase(model, female)` is the only call (`TennisActor.cs:255`).
  - `ApplyOutfit` → `WearKit` → `RecolorKit` still loads `Tennis/Characters/HiggsAvatar_*` and does a GPU blit on every launch. The result goes onto renderers that `TennisHeroSetup` has hidden.
- **What breaks:**
  - `NativeSportsSession.cs:253-254` (`ApplyOutfit` and `Player.Customize` calls).
  - `Tests/PlayMode/TennisTutorialPlayTests.cs:31` and `EditMode/TennisTutorialTests.KitColoursParseFromTheLaunchMessage`, which tests `TennisLook.Kit` and is a reported pre-existing failure.
  - Cross-area, these resources would also become unused: `Resources/Tennis/Customization/*.json` (Custom*, Player*Bob, Curls, Swept, Mask), `*Color.png`/`*Mask.png`, `CustomHead*.png`, `HiggsAvatar*`, `PlayerMaleGolf.fbx`/`PlayerFemaleGolf.fbx`, `Resources/Tennis/Characters/Higgs*`, and the `KitRecolor` shader.
- **Verdict:** SAFE+FIX (drop the calls in `NativeSportsSession.Load`). **Confidence:** medium-high.

### UCODE-08: The older V4 golfer, kept only as a fallback (DECIDE)
- **Paths:**
  - `Game/GolferView.cs`: `BuildModel` 398-465, `BuildFigure` 796-834, `Clay` 143-163, `CreateSpectator` 302-310 (never called).
  - `Look.ModelPath` and `HairMesh`.
  - `Game/GolferStyle.cs`: "The older V4 figure" block 216-224 (`HairKind`, `HairNames`, `HairMesh`, `ShirtColor`, `TrousersColor`, `ModelPath`).
- **What it is:** the pre-match-hero golfer from `Resources/Golfer/golfer_m|f`, and a primitive stand-in figure.
- **Size:** about 150 lines. Tracked.
- **Evidence:**
  - `GolferStyle.UseHero` is true whenever `Resources/Hero/golfer_m` exists, and it does (`HeroGolfer.Available`).
  - The fallback runs only if that is missing, or when a test forces `HeroOverride=false`: `HeroGolferTests.TheV4GolferForComparison` (passed in the Oct 10 log).
  - `GolferClay.shader` is used only by `Clay`.
- **What breaks:** that one test. Cross-area: `Resources/Golfer/golfer_*.fbx` and `higgs_*_color.png` lose their only reader.
- **Verdict:** DECIDE (a comparison and safety net vs ~150 lines + ~shipped resources). **Confidence:** high.

### UCODE-09: Native commands and events the iPhone app no longer sends or listens for (DECIDE)
- **Paths:**
  - `Tennis/TennisAimPractice.cs` (59, a partial class of TennisGame).
  - `NativeSportsSession.cs`:
    - `case "aimPractice"`, `"aimFeed"` (171-172), the `aimLanding`/`aimMiss` emitters (121-128) and their event hooks;
    - `case "golfOverview"` (178) → `GolfGame.Overview()` (`GolfGame.Presentation.cs:63-68`);
    - the no-op `"ultimateSelect"`/`"ultimate"` (163-164).
  - Coaching tips in `Tennis/TennisCoach.cs`: native launch sets `TipsEnabled=false` (`NativeSportsSession.cs:252`), so the `"coaching"` command only resets tips that never show.
  - The Hub's `Emit("matchReady")` (`NativeSportsSession.Hub.cs:136`) has no Swift listener yet. This is new, uncommitted work.
- **What it is:** the tennis "aim practice" drill, the golf hole-overview fly-through on request, and the old ultimate-ability command.
- **Size:** about 80 lines. Tracked except the Hub file.
- **Evidence:**
  - Swift sends none of `aimPractice`, `aimFeed`, `golfOverview`, `ultimate*`, and handles none of `aimLanding`, `aimMiss`, `matchReady`.
  - Every action string in `Receive` was counted in `GolfArcade/**/*.swift`. Swift still keeps unused `aimLesson`/`aimFeed*` state in `SportsSession.swift:161-213` (cross-area).
- **What breaks:** tests:
  - `TennisGameplayImprovementPlayTests` (lines 133-158);
  - `NativeTennisSetupTests:171`;
  - `PresentationFlowTests` (2 `GolfOverview…` tests).
- **Verdict:** DECIDE (drop them, or wire them into the app). **Confidence:** high.

### UCODE-10: The golf tutorial component is never created (SAFE+FIX)
- **Path:** `Game/GolfTutorial.cs` (100): `GolfTutorial` MonoBehaviour 36-99 plus `GolfTutorialLesson` 7-34.
- **What it is:** a golf lesson overlay.
- **Size:** 100 lines, tracked (git 2026-10-06).
- **Evidence:** no `AddComponent<GolfTutorial>`, no scene or prefab use. Only `EditMode/GolfTutorialTests.cs` (27) tests the lesson state.
- **What breaks:** that test, so delete both.
- **Verdict:** SAFE+FIX (or DECIDE if a golf tutorial is planned). **Confidence:** high.

### UCODE-11: Game classes used only by tests (DECIDE)
- **Paths:**
  - `Game/StandardGolfGrip.cs` (99): tested by `EditMode/StandardGolfGripTests.cs`.
  - `Swing/GolfSwingCalibration.cs` (38): tested by `EditMode/MotionSwingDetectorTests.cs`. It is also linked by `Tools/netsim/NetSim.csproj` on `main`, which does not exist on final-build.
- **What it is:** a golf hand-grip constraint for the old "standard" characters, and a three-practice-swing calibration that the native flow does not use.
- **Size:** 137 lines. Tracked.
- **Evidence:** no game caller, no scene or prefab use.
- **What breaks:** those tests. On `main`, the netcode CI job compiles `GolfSwingCalibration.cs`.
- **Verdict:** DECIDE. **Confidence:** high.

### UCODE-12: The tennis reference-matching stage (DECIDE)
- **Path:** `Tennis/TennisReferenceMatch.cs` (147).
- **What it is:** freezes the real tennis game in one moment to compare it against the approved concept video.
- **Evidence:** attached only to `Scenes/TennisReference.unity`, which is not a build scene (UASSET-07). Used by `Editor/TennisReferenceSceneBuilder.cs` and `PlayMode/TennisReferenceCaptureTests.cs` (6 Explicit tests).
- **Verdict:** DECIDE, together with UASSET-07. **Confidence:** high.

### UCODE-13: 53 methods nobody calls inside live classes (SAFE)
- **What it is:** helpers left behind by earlier passes.
- **Size:** 469 lines. Mostly tracked; the Hub ones are new.
- **Evidence:** the method name appears nowhere in any C#, Swift or ObjC code except its own declaration (strings included, so reflection or SendMessage use would show). Unity callbacks, overrides and externs were excluded.
- **The methods (start-end lines):**
  - `Course/GolfAtmosphere.cs`: RegisterBall (188-195), CoolPanorama (307-341), CraterFallback (344-359)
  - `Course/GolfCourseLook.cs`: FinishModel (304-308)
  - `Course/GolfFigureExposure.cs`: IsWhite (16)
  - `Game/CameraRig.cs`: HoleCam (226-234), FrameWindow (238-242), BallPov (302-320), Flyover (354-360), FramePortrait (438-450), FrameHome (478-491), EaseHorizontalFov (558-565)
  - `Game/CinematicRig.cs`: ShowTrails (129-132)
  - `Game/GolfGame.cs`: PlayOverview (1464-1470), PlaySignature (1474-1503: the old intro fly-through playback; `SignatureShot` itself is still loaded for timing), PinTargets (1604-1619), FinishFor (1651-1657)
  - `Game/GolfTutorial.cs`: SkipStep (75-80)
  - `Game/GolferView.cs`: CreateSpectator (302-310)
  - `Game/LandingZone.cs`: SetRadius (31)
  - `Hub/HubKit.cs`: Textured (25-29), Obj (31-39). Hub code is new and untracked; these may simply not be wired yet.
  - `Net/Online/BackendClient.cs`: FetchProfile (75-80)
  - `Tennis/HeroGarmentGusset.cs`: Synchronize (316-321), DualPart (438-442), BlendDual (452-463)
  - `Tennis/HeroGripPolish.cs`: AlignWrist (80-87)
  - `Tennis/HeroKit.cs`: HairRenderer (18), BandFillRenderer (19), DetailTint (33-37)
  - `Tennis/HeroThumbRigMigration.cs`: BeforeSample (76), AfterSample (77), ToSourcePose (81-84)
  - `Tennis/TennisActor.cs`: ContactFor (606)
  - `Tennis/TennisCustomization.cs`: BodyScale (17)
  - `Tennis/TennisGame.cs`: PointContext (883-889), UltimateGain (1770)
  - `Tennis/TennisJuice.cs`: SetBall (111)
  - `Tennis/TennisLook.cs`: ApplyCrowdPalette (128-140), ReplayFocus (260-265)
  - `Tennis/TennisRules.cs`: PredictInterceptX (348-370), MovementAssist (375-381), OnStringBed (634-640), BackhandCoilAt (748-754)
  - `Tennis/TennisTutorial.cs`: SkipStep (92-97)
  - `Tennis/TennisVenueBuilder.Mesh.cs`: FlatLump (141-157), Hash01 (237), TexDome (279-325)
  - `Tennis/TennisVenueBuilder.Skyscraper.cs`: SkyBand (622-634), SunRings (643-652)
  - `Tennis/TennisVenueBuilder.Volcano.cs`: VolcanoSun (602-611)
  - `UI/Club.cs`: SetHighlight (342-347)
  - `UI/Locker.cs`: ShowGear (509)
- **What breaks:** nothing. `TennisRules` is also linked by `main`'s netsim but these four helpers are unused there too.
- **Verdict:** SAFE. **Confidence:** high (medium for the Hub helpers).

### UCODE-14: 37 unused constants and properties, and 3 unused nested types (SAFE)
- **What it is:** values and small types nothing reads.
- **Size:** about 55 lines.
- **Evidence:** the same name search as UCODE-13.
- **The members (line numbers):**
  - `GolfPlantInstances.ProofMask` (12)
  - `GolfWindSway.HasWind` (42)
  - `CameraRig.PovLensHorizontal` (294)
  - `GolfGame.Presentation.PresentationPlaying` (12)
  - `GolferStyle`: SkinToneNames (24), HairColorNames (31), ShirtNames (38), HairNames (218), Summary (246), BodyLabel (247)
  - `HubArt.DoorGlow` (22), `HubPlayer.StickTime` (39), `HubWorld.FadeAlpha` (434)
  - `NetworkMotionSource.PacketsDropped` (38)
  - `OnlineSession.KeptHoles` (92)
  - `HeroKit.HairHex` (13)
  - `HeroTennisDriver.MissingFromMatchSet` (83)
  - `HeroTennisPerformance.GazeDegrees` (18)
  - `TennisGame.BallReachable` (1137)
  - `TennisHud`: PearlTop (16), CaptionTop (17)
  - `TennisJuice.PerfectBeatCooldown` (36)
  - `TennisMusic.Master` (38)
  - `TennisOpponent.EasyThreshold` (49)
  - `TennisResortCrowd`: VisitorCount (14), LegacySkinnedCount (15)
  - `TennisRules`: OpponentSpeed (255), BackhandCoil (743), TrunkCounterRotation (773), ArmSwing (774)
  - `TennisStandsCrowd.FanCount` (36)
  - `Club.Lagoon` (20)
  - `HoleOutCard.NextText` (68)
  - `Hud.LandingZoneAmber` (75)
  - `Locker.ShowingGear` (508)
  - `UiKit`: SurfaceRaised (17), AccentStrong (22)
- **The nested types:**
  - `GolferStyle.HairKind` (217)
  - `HeroGolfer.Headwear` enum (39, a single "None" value)
  - `TennisVenueFx.Bob` (90-100, a "floating rock" component never added)
  - `HeroGolfer.Haircut` (38) is used only by editor tools and tests (the game stores ints); KEEP.
- **What breaks:** nothing.
- **Verdict:** SAFE. **Confidence:** high.

### UCODE-15: `Game/GameCapture.cs` (77): a test helper inside the game (KEEP)
- **What it is:** helpers that save PNG/JPG screenshots.
- **Evidence:** no game caller. It is used by 17 editor tools and 35 test files.
- **Why it stays in the game assembly:** the PlayMode test assembly cannot reference the Editor assembly (see the asmdefs), so this file must live in the game code.
- **Verdict:** KEEP, while the capture tests and tools exist.

### UCODE-16: Looks dead but is used (KEEP)
- **`CourseLies` (`Hole.cs:24`) and `Surfaces` (`Surface.cs:17`):** extension-method classes. They are used as `lie.Label()`, `lie.SpinFactor()`, `surface.Slope()` (`GreenRead.cs:116`).
- **`TennisSynth.Biquad` / `Voice`:** used through `using static` in `TennisMusic` and `TennisAmbience`.
- **`GolfPostcardBasalt`** and its `GolfBasaltColumns` shader: called by `GolfCourseLook.cs:317` for hole 10 (the Crater). The call sits right after an `#endif`.
- **Start-up hooks:** `MuteWhenAutomated` and `ErrorGuard` (`[RuntimeInitializeOnLoadMethod]`), and `HeroLightingProfile`.
- **The hidden tennis gameplay rig:** `TennisActor`, `StandardCharacterArms` (elbow math), and `TennisKitV3` (`Equip`, `GripInset`). These drive movement and contact.
- **`Course.Postcards()`** (`Hole.cs:328`): a one-line course used only by editor look tools and `PostcardHoleTests`. The `"postcards"` key is aliased to Cliffside.
- **`link.xml`:** keeps 3 collider types for procedural arenas.
- **The 4 asmdefs.**

---

## B. Native bridge (`Unity/Assets/Plugins/iOS`)

### UCODE-17: Bridge functions with no caller on either side (SAFE+FIX)
- **Paths:** `Plugins/iOS/SportsBridge.mm` and `Plugins/iOS/GolfAirPlay.mm`.
- **The functions:**
  - `SportsPollInput` and the `inputs` queue:
    - Nothing ever reads the queue: no C# extern and no host call.
    - The host still writes to it through `SportsPushInput`, which `GolfArcade/Unity/SportsRuntime.mm:76` looks up with `dlsym`. But its `push:(NSString*)` method is never called; all 3 Swift `push(...)` calls pass a `SportsSample`.
  - `SportsClear` (never called).
  - `GolfAirPlay_ScreenCount` (never called).
  - `GolfAirPlay_ShowPicker`: called only by `Game/BigScreen.cs`, which is the legacy UI in UCODE-06.
- **Size:** about 25 lines. Tracked.
- **What breaks:** the host refuses to start Unity if `SportsPushInput` is missing (`SportsRuntime.mm:85`, "Unity bridge is missing").
- **Follow-up edit:** remove `_push` from that check, and the unused `push:` method in `SportsRuntime.h/.mm` (cross-area: swift-app).
- **Live (KEEP):**
  - `SportsNetworkPush`/`Poll`/`Emit`/`PollOutput`, `Sports(Set|Read)TennisResult`, `SportsClock`, `SportsPushSample`/`PollSample`, `SportsEmit`/`PollEvent`;
  - all `SportsRecorder*`, `SportsRegisterPointCapture`, `SportsMakePointRenderDelegate`, `SportsPrepareExternalDisplay`;
  - all 8 `GolfHaptics_*`.
- **Verdict:**
  - SAFE+FIX for the input queue.
  - SAFE for `SportsClear` and `GolfAirPlay_ScreenCount`.
  - `GolfAirPlay_ShowPicker` follows UCODE-06.
- **Confidence:** high.

---

## C. Shaders

### UCODE-18: `Resources/Course/Shaders/PostcardV3Proof.shader` (92) ships but is a proof-only shader (SAFE+FIX)
- **What it is:** a hidden shader used only by the editor tools `PostcardV3FrameProof` and `PostcardV3SplitOutline`.
- **Evidence:** no material and no game code uses it. Because it sits in `Resources/`, it is in the app anyway.
- **Follow-up edit:** move it to `Assets/Editor/`, or delete it with the PostcardV3 tools (UCODE-25).
- **Verdict:** SAFE+FIX. **Confidence:** high.

### UCODE-19: Shaders used only by legacy code paths (DECIDE, follows the code)
- **The shaders:**
  - `Shaders/GolferClay.shader` (142): only the V4 golfer fallback (UCODE-08).
  - `Shaders/ClubBackdrop.shader` (84) and `ClubDisc.shader` (29): only `ClubStage` (UCODE-06).
  - `Resources/Tennis/Shaders/KitRecolor.shader` (53): only the hidden-rig recolour (UCODE-07) and `ModularHeroLook` (UCODE-01).
- **Why they still ship:** `ProjectSetup.RuntimeShaders` (`Editor/ProjectSetup.cs:51`) and GraphicsSettings "Always Included" force the first three into every build.
- **Follow-up edit:** if the code goes, drop their names from that list and from GraphicsSettings.
- **Every other shader is live:** referenced by a material or loaded by name from live code. This includes `HubGlitchScreen` (Hub material, untracked), `GolfBasaltColumns`, `TennisHorizon2`, `TennisVolcanicSky`, `HeroFaceSkin` and all `.hlsl` includes (`GolfLegacyURP.hlsl` is included by 6 shaders).

### UCODE-20: Editor shaders `Editor/ClothProofMask.shader` (28) and `Editor/DiagnosticHandSurface.shader` (20) (KEEP while their tools exist)
- **What they are:** used only by `ClothProofRig` and `HeroThumbRigProbe`. Being in `Editor/`, they never ship.
- **Verdict:** delete them with those tools (UCODE-26 and UCODE-28).

---

## D. Editor tools (`Unity/Assets/Editor`)

These tools do nothing unless someone runs them. All the `[InitializeOnLoad]` ones wait for their own SessionState key, so leaving them in changes no asset. The exceptions are the importers (`AssetPostprocessor`): removing one changes how assets import (UCODE-29).

### UCODE-21: A committed symlink pulls draft tools from `work/` into the editor (SAFE+FIX)
- **Paths:** `Unity/Assets/Editor/PostcardV3Scratch` and `PostcardV3Scratch.meta`.
- **What it is:** two git symlinks (mode 120000) with absolute targets: `/Users/adnanyonathan/…/work/postcard-look/v3/unity-proof` and `…/unity-proof.folder.meta`.
- **What it brings in:**
  - 3 editor scripts, 225 lines, all marked "DRAFT": `PostcardV3FixtureCapture.cs`, `PostcardV3FixtureSway.cs`, and the `AssetPostprocessor` `PostcardV3ScratchImporter.cs`;
  - nested symlinks to hole 8/9 stage FBXs and to `work/postcard-look/v3/lib/unity-inputs`.
- **Size:** 2 tracked symlinks (git 2026-10-08).
- **Effect:** on this Mac they compile into the editor and import scratch FBXs. On any other clone the link is dangling.
- **Evidence:** no tracked code references these classes. Only `work/postcard-look/v3/proof-tools/prepare_scratch_import.py` and `…/proof/runtime/prepare_stage_capture.py` use them.
- **What breaks:** nothing in the game. The Oct 5 PostcardV3 fixture proofs stop working from inside the project.
- **Verdict:** SAFE+FIX (`git rm` the two links; the drafts stay in `work/`). **Confidence:** high.

### UCODE-22: Retired Hero01 / V4 / V5 / V6 tools (22 files, 2,539 lines) (SAFE, together with UCODE-01 and UASSET-01 to 06)
- **Files:**
  - `HeroAnimRepairReview` 96, `HeroBackhandRunReview` 72, `HeroFullStrokeReview` 109, `HeroGameplayFixCapture` 121, `HeroGameplayPlaybackReview` 50, `HeroKneesOffArmReview` 64, `HeroLegacyReview` 103, `HeroRestoreMotionReview` 160, `HeroOwnedReview` 109 (menu item), `HeroLookReview` 188 (menu item): Play-mode review captures of Hero01, written to `ArtDir/screenshots/<name>_playmode`. git 2026-09-28.
  - `HeroGameplayBuild` 328: builds the Hero01 gameplay prefab and cosmetics.
  - `HeroLockerExport` 310: exported Hero V4 to `GolfArcade/Unity/CharacterAssets/HeroV4*` and to `Resources/Tennis/Hero/HeroV4_KitMask.png`/`_KitRef.json`.
  - `HeroIntegrityAudit` 130, `HeroIntegrityReview` 97: V5 Astra-texture copy.
  - `HeroV5Rebind` 108 (menu item), `HeroV6Import` 42 (menu item), `HeroV6PolishImport` 60, `HeroV6PolishReview` 139, `HeroV6PolishBeforeReview` 97.
  - `LegacyHero01` 10: keeps the old path alive on purpose.
  - `MatchHeroAB` 89: the A/B of new vs Hero01 on court.
  - `HeroLookImporter` 57: an `AssetPostprocessor` for `ArtDirection/Hero01/`. It sets `textureCompression=Uncompressed`, which is why Hero01 weighs 83 MB in the app.
- **Dates:** git 2026-09-28 to 2026-10-04. None has a script caller in `work/` after 2026-10-04.
- **What breaks:** nothing in the game. They depend on UCODE-01 types, so remove them together. `HeroLookImporter` matters only while `ArtDirection/Hero01` is kept.
- **Verdict:** SAFE together with the assets (or DECIDE if the Hero01 archive is kept on purpose: `LegacyHero01` says it is). **Confidence:** high.

### UCODE-23: HeroBase plate-matching proofs (4 files, 555 lines) (DECIDE)
- **Files:** `HeroBaseFemaleHeadProof` 120, `HeroBaseMaleFormProof` 158, `HeroBaseSkinProof` 88, `HeroBaseStaticImport` 189.
- **What they are:** studio renders of the grey HeroBase bodies against the `base_lock` plates. They write to `ArtDir/hero/base_lock/proof` (cross-area).
- **Date:** the last work-script use was 2026-10-02.
- **Note:** `AGENTS.md` still names HeroBase as the character authority, so these may be wanted again.
- **Verdict:** DECIDE.

### UCODE-24: Early tennis scene and review tools (5 files, 375 lines) (DECIDE)
- **Files:**
  - `TropicalArenaReview` 108 (2026-09-22)
  - `TennisReferenceSceneBuilder` 34 (menu; builds the reference scene, UCODE-12)
  - `TennisSceneBuilder` 33 (menu "Open Rally Lab"; it only opens `Tennis.unity` and is used by `TennisReviewCommands`)
  - `ClipAudit` 49
  - `ClipContactAudit` 151: regenerates `Resources/Tennis/StrokeContacts.json`, which `TennisActor` still loads.
- **Verdict:** DECIDE. Keep `ClipContactAudit` if the hidden-rig contact table may need regenerating.

### UCODE-25: Postcard-look v2/v3 golf proof tools (16 files, 6,848 lines) (DECIDE)
- **Files:**
  - `GolfLookSmoke` 2,587 (menu): the look smoke gates; uses `HeroBaseStudioURP` per UASSET-10.
  - `PostcardImportCheck` 928: also used by `Tools/PostcardCheck/PostcardImportCheck.cs.pending`.
  - `PostcardLookStills` 702, `PostcardProofMasks` 331, `PostcardCameraStills` 89, `PostcardPlayVideo` 121.
  - The PostcardV3 set: `CostDriver` 30, `FrameCosts` 192, `FrameProof` 295, `IOSExport` 70, `ProofMasks` 373, `SplitOutline` 70, `StageModel` 47. Several headers say "DRAFT FOR P5" or "DRAFT: … install and Unity-compile before use".
  - `GolfLookGrassCheck` 100, `GolfPlantSwayCheck` 765 (menu; uses `Editor/GolfPlantSwayFixture/`), `GolfFlyoverCapture` 148.
- **Use:** heavily used by `work/postcard-look/**` scripts up to 2026-10-05. Nothing newer.
- **Writes:** `ArtDir/screenshots/golf_postcards` and work folders.
- **Verdict:** DECIDE: this is the golf look gate suite. Keep `GolfLookSmoke` and `PostcardImportCheck` if look gates are still run; the PostcardV3 drafts are the most disposable. **Confidence:** high.

### UCODE-26: Match-hero, serve, emote and cloth proof and film tools (18 files, 3,505 lines) (DECIDE)
- **Files:**
  - `MatchHeroProof` 395, `MatchHeroFilm` 52, `MatchHeroProbe` 182
  - `MatchHeroKitProof` 211, `MatchHeroKitTests` 352, `MatchHeroKitBaseline` 104, `MatchHeroKitProbe` 41
  - `ServeAndFeetProof` 414, `ServeJumpProof` 283, `ServeTossFilm` 89, `ServeWalkFilm` 418
  - `EmoteFilm` 152, `EmoteHoldCheck` 97
  - `ClothProofRig` 283, `CharacterShaderStill` 152, `HeroDetailStill` 157
  - `PointClipAudioProbe` 28, `RallyPopHudCapture` 95
- **Dates:** git 2026-10-04 to 2026-10-08; last script use 2026-10-03 to 2026-10-07.
- **What they are:** the gate proofs for the current heroes (Oct 3-7 tasks). They are reusable when heroes, clips or kit change.
- **Verdict:** DECIDE (keep while character work continues).

### UCODE-27: Visual-overhaul capture, cost and film tools (24 files, 3,320 lines) (DECIDE)
- **Files:**
  - `VisualOverhaulCharacterCapture` 418, `VisualOverhaulGolfCharacterCapture` 131, `VisualOverhaulFrameCosts` 203, `VisualOverhaulCostDriver` 30 ("DRAFT")
  - `TennisFrameCosts` 149, `TennisRenderBreakdown` 205, `TennisNpcInventory` 47, `TennisAudienceProof` 104, `TennisCrowdProof` 55
  - `TennisPromenadeFilm` 78, `TennisActionFilm` 74, `TennisDiveFilm` 82, `TennisLocomotionFilm` 68, `TennisPerformanceFilm` 130
  - `CurrentGolfSwingFilm` 85, `CurrentTennisPointFilm` 218, `GolfMotionFilm` 98
  - `GolfBroadcastReview` 26, `GolfCourseStandardCapture` 402, `GolfGalleryProof` 76, `GolfPartyProof` 76
  - `HeroFaceMatchCapture` 166, `StrokeArmReview` 77 (used by `TennisArmStrokeBuild`)
  - `TennisVenueCapture` 322 (writes `ArtDir/screenshots/venues`)
- **Dates:** git 2026-10-08; script use up to 2026-10-08.
- **Verdict:** DECIDE (recent and likely reused).

### UCODE-28: One-off probes and repair tools (19 files, 1,349 lines) (DECIDE; one already broken)
- **Files:**
  - `HeroFingerProbe` 53, `HeroLidProbe` 51, `HeroThumbRigProbe` 172 (needs UCODE-02 classes), `TennisFistProbe` 85, `TennisSoleProbe` 178
  - `HeroStarterPaletteProbe` 18, `HeroReferenceImportAudit` 16, `HeroFeaturePoseAudit` 14, `HeroGarmentPoseSnapshot` 58, `HeroGarmentGussetRuntimeGate` 192 (header: "Place under Assets/Editor only when root integrates it")
  - `HeroBodyCoverageRepair` 63, `GolfGarmentRepair` 69, `GolfGripAudit` 58, `GolfGripNativeExport` 48
  - `TailoredPoloAssetBake` 52, `TailoredPoloPoseProbe` 30, `PremiumCharacterBuild` 93
  - `ClothCompileProbe` 70: **already broken**; it reads `Assets/Editor/ClothBaseline.shader`, which does not exist.
  - `ControllerIOSExport` 29
- **Dates:** git 2026-10-06 to 2026-10-08. Most have no script caller.
- **Dependencies:** `HeroFeaturePoseAudit`, `HeroGarmentPoseSnapshot`, `HeroGarmentGussetRuntimeGate` and `TailoredPoloPoseProbe` are used by `VisualOverhaulCharacterCapture` (UCODE-27). Remove them only with it.
- **Verdict:** DECIDE. SAFE for `ClothCompileProbe`.

### UCODE-29: The build and import pipeline (30 files, 3,873 lines) (KEEP)
- **Build and setup:**
  - `BuildMenu` 58: the iOS export. It calls `TropicalArenaImporter.Prepare` and `ProjectSetup.EnsureShadersShip`.
    - Stale bits: the Mac-club Info.plist text and the always-allowed insecure http setting (UCODE-05/06). Only the Unity-iPhone shell's plist is affected, not the host app's.
  - `ProjectSetup` 91 (`RuntimeShaders` list), `UrpSetup` 186.
  - `TestMenu` 86 (menus; drives `DemoVideoTests`), `PlayMenu` 52.
  - `TennisReviewCommands` 31: polls `Library/TennisReview.request`, last used per `Library/TennisReview.response`.
- **Hero builders and exporters:**
  - `MatchHeroBuild` 230, `MatchHeroImport` 177 (an `AssetPostprocessor`), `MatchHeroKit` 171 (an `AssetPostprocessor`)
  - `MatchHeroLockerExport` 901 (used 2026-10-10 by `work/lobby-parity`)
  - `EmoteWire` 115, `ServeAndFeetWire` 134 (used by `MatchHeroBuild`), `TennisPerformanceBuild` 203, `TennisArmStrokeBuild` 278, `TennisStrokeBuild` 62, `GolfHeroKit` 54
- **Texture rules:** `HeroDetailTextures` 80 and `LavaTextureBaker` 167 (both `AssetPostprocessor`).
- **`SurfaceEdgesCheck`** 278 (untracked, new): the equality check for the GolfSurfaceEdges speed-up.
- **11 importers** (`AssetPostprocessor`; every target folder exists):
  - `ClubArtImporter` → `Resources/Club`
  - `CourseModelImporter` → `Resources/Course`
  - `GolfCourseStandardTextureImporter`, `GolfKitMapPostprocessor`, `GolfLookTextureImporter`, `GolfResortKitImporter`
  - `GolferModelImporter` → also `Resources/StandardCharacters`, `Tennis/Opponents`, `Tests/Fixtures`
  - `HeroModelImporter`: its `Resources/Hero/Look/` rule is dead because the folder is missing
  - `TennisCharacterTextureImporter`, `TennisCrowdImporter`, `TropicalArenaImporter`
- **Warning:** removing any importer changes how its folder imports.
- **Verdict:** KEEP.

---

## E. Tests (`Unity/Assets/Tests`)

### UCODE-30: Tests of the retired heroes and hair (4 files, 329 lines) (SAFE with UCODE-01 and 22)
- **Files:**
  - `PlayMode/HeroV6RetargetTests` 109 (Explicit; loads `Hero_01_Tennis` and V6)
  - `HairFitAuditTests` 75 (Explicit; V5 haircuts × headwear, writes `ArtDir/hero/v5_proof/hairfit_unity`)
  - `Score80HairABTests` 58 (Explicit; `HERO_HAIRFIX` is the legacy switch, writes `ArtDir/score80/phaseD`)
  - `Score80WardrobeAuditTests` 87 (Explicit; headwear and hair holes)
- **Why they are obsolete:** the match heroes are bald, with no headwear system.
- **Verdict:** SAFE.

### UCODE-31: The "Score-80" plan harness (3 files, 276 lines) (DECIDE)
- **Files:** `PlayMode/Score80CaptureTests` 133, `Score80ClipProbeTests` 78, `Score80EnvAuditTests` 65.
- **What they are:** all Explicit. The Sept 28 Score-80 proof captures, written to `ArtDir/score80`.
- **Verdict:** DECIDE (archive-only value).

### UCODE-32: Tests whose subject is test-only or a review scene (4 files, 300 lines) (DECIDE)
- **`EditMode/StandardGolfGripTests`** 47 (UCODE-11).
- **`EditMode/StandardArmSportTests`** 63: arms on `Tests/Fixtures/StandardCharacters/{male,female}_{tennis,bowling,boxing}.fbx`. Bowling and boxing are not in the game.
- **`EditMode/GolfTutorialTests`** 27 (UCODE-10).
- **`PlayMode/TennisReferenceCaptureTests`** 163 (6 Explicit; UCODE-12).
- **Verdict:** DECIDE. `GolfTutorialTests` goes with UCODE-10.

### UCODE-33: Tests of the Unity-drawn menu, online and championship layer (8 files, 954 lines) (DECIDE, follows UCODE-06)
- **Files:**
  - EditMode: `OnlineErrorTests` 37, `OnlineRoomTests` 84, `FormatAndChampionshipTests` 172 (the match-format part may stay)
  - PlayMode: `ErrorHandlingTests` 171 (partly ErrorGuard, keep that part), `HomeScreenTests` 134, `LocalVersusTests` 94, `GolferSelectTests` 151, `BigScreenFlowTests` 111
- **Also touched:** `HeroGolferTests` uses the Unity locker in one test, and `MagmaOpenTests` uses `Championship.TitleOf`.
- **Verdict:** DECIDE.

### UCODE-34: Capture and film tools written as tests (12 files, 1,407 lines + 13 Explicit captures inside `TennisPolishPlayTests`) (DECIDE)
- **Files:**
  - `BrollCaptureTests` 111
  - `DemoVideoTests` 415: driven by the `TestMenu` "Record …" items and `Unity/Tools/demo_video.sh` / `swing_video.py`
  - `IntroCaptureTests` 60, `PresentationFilmTests` 125, `TennisFullMatchCaptureTests` 56, `TennisMotionReferenceTests` 131, `TennisMultiAngleCaptureTests` 145, `TennisSinglePointCaptureTests` 96, `TennisTempoCaptureTests` 54
  - `TennisStanceCaptureTests` 59: runs only with `HERO_CAPTURE_STANCE`; writes `ArtDir/screenshots/stance`
  - `TennisVenueFilmTests` 79: runs only with `HERO_FILM_VENUE`; writes `ArtDir/review/venues`
  - `HubPreviewCapture` 76: untracked; runs only with `HUB_CAPTURE_OUT`; makes the Hub bay previews in `Resources/Hub/Previews`. KEEP while the Hub is in progress.
- **How they run:** all are Explicit or env-gated, so they never run in a normal pass.
- **Writes:** 36 test files write to `Unity/Library/Captures` (5.1 GB, per UASSET-24).
- **Verdict:** DECIDE (they are tools; keep the ones still used for reviews).

### UCODE-35: Tests that last failed (DECIDE: fix or retire)
These are the last recorded results. They were not re-run in this audit.
- **Oct 10 log (`Library/TestResults/EditMode.txt.running`, a PlayMode run):**
  - `ArcadeCardsTests.TheCardsThroughARound`
  - `GolfBroadcastTests.FlightCutsOnceAtLandingThenBothGolfersCanEmoteAndContinue`
  - `GolfPolishTests.DetailedBodiesAndEveryClubClearTheHead`
  - `GolfPresentationTests.SwingToResultShowsStatsAndLetsBothGolfersEmoteUntilContinue`
  - `HeroGolferTests.EveryLookBuilds`
- **Oct 6 log (`0.txt.running`):**
  - `TennisGameplayTests.ApprovedArenaAndBothStandardsRunTennisPhysics`, `DisplayDelayCompensationRestoresLateSwings`, `StrengthAndPhonePositionChooseReadableStrokes`
  - `TennisPolishPlayTests.CaptureHud`, `CaptureIsland`, `CaptureOpponents`, `CapturePresentation`, `TimingCheckSetsTheLagFromSwingsOnTheBeat`
  - 4 × `TennisTutorialPlayTests`
  - `TennisRulesTests.HardStrokesHaveSmallerContactWindowAndReach`
- **Reported baseline** (from the brief, not re-verified):
  - `check.sh`: 2 "ECall" failures (its plain reflection runner cannot call Unity engine internals) + 2 `TennisCampaignBalance` null-reference errors.
  - `TennisTutorialTests.KitColoursParseFromTheLaunchMessage` (it tests the vestigial `TennisLook.Kit`, UCODE-07).
- **Plus:** `ReplayTests` is expected to fail since Oct 6 (UCODE-03).

### UCODE-36: `Tests/EditMode/CourseCatalogue.cs` (11, untracked) (KEEP)
- **What it is:** a helper for `CourseCatalogueTests` (hole by number across the 3 courses).

---

## F. `Unity/Tools`

### UCODE-37: The golf demo-reel scripts `demo_video.sh` (28) and `swing_video.py` (77) (DECIDE)
- **What they are:** turn `DemoVideoTests` frames into MP4s.
- **Evidence of disuse:**
  - The last demo capture is `Library/Captures/demo`, dated 2026-09-27.
  - `Library/Captures/swing` has never been made on this clone.
  - git dates: 2026-09-23 and 2026-10-06.
- **Verdict:** DECIDE (with `DemoVideoTests` and the `TestMenu` record items).

### UCODE-38: `check.sh` (41) + `TestRunner.cs` (39), `build-integrated-ios.sh` (12), `install-phone.sh` (15), `stamp.sh` (8), `run-playmode-windowed.sh` (20) (KEEP)
- `check.sh` and `TestRunner.cs`: headless compile plus the EditMode runner.
- `build-integrated-ios.sh`, `install-phone.sh`: the phone build path.
- `stamp.sh`: writes `Resources/build_id.txt`, which the Unity home menu reads.
- `run-playmode-windowed.sh`: still the documented way to run rendering PlayMode tests (HoleFlowTests).

## G. asmdefs, link.xml, CI
- **The 4 asmdefs and `link.xml`:** KEEP.
- **The Editor asmdef** references the test runner only for `TestMenu`.
- **No netcode CI here:** `final-build` has no `Tools/netsim` and no netcode workflow; `.github/workflows` here has only `ios.yml` (xcodebuild) and `server.yml` (node). No CI job compiles Unity code on this branch.
- **On `main`:** `Tools/netsim/NetSim.csproj` links Multiplayer, Tennis rules, Course, Shot and Swing sources plus 6 EditMode tests. 8 of those files are absent here (`NetworkTennisWire.cs`, `NetworkTuning.cs`, `TennisSplitCamera.cs`, `TennisTossCurve.cs` and 4 network tests), so the branches have diverged.

---

## Cross-area links
- **app-resources:**
  - `Hero_01_Tennis.prefab` keeps `ModularHeroLook` and `HeroCosmetics` in the build (UCODE-01).
  - `Resources/Tennis/Customization/*.json`, `*Color.png`/`*Mask.png`, `CustomHead*`, `HiggsAvatar*`, `Player*Golf.fbx` and `Resources/Tennis/Characters/Higgs*` are read only by the vestigial hidden-rig code (UCODE-07).
  - `Resources/Golfer/golfer_*.fbx` and `higgs_*_color.png` are read only by the V4 fallback (UCODE-08).
  - `Resources/Club` is read only by `ClubStage` and `ClubArtImporter` (UCODE-06).
  - `Resources/Course/Shaders/PostcardV3Proof.shader` ships but is editor-only (UCODE-18).
  - `Resources/build_id.txt` is written by `stamp.sh` and read only by the legacy Unity home menu.
  - `Resources/Tennis/StrokeContacts.json` is written by `ClipContactAudit` and read by `TennisActor`.
- **artdir:** legacy tools and tests write to:
  - `ArtDir/screenshots/{anim_repair,backhand_run,fullstroke_grip,gameplay_fixed,gameplay_fixed_playmode,knees_offarm,v4_legacy,owned}_playmode…` (UCODE-22);
  - `ArtDir/hero/base_lock/proof` (UCODE-23);
  - `ArtDir/screenshots/golf_postcards` (UCODE-25);
  - `ArtDir/screenshots/venues` (UCODE-27);
  - `ArtDir/score80/**` (UCODE-30/31);
  - `ArtDir/hero/v5_proof/hairfit_unity` (UCODE-30);
  - `ArtDir/screenshots/stance`, `ArtDir/review/venues` (UCODE-34).
- **swift-app:**
  - `SportsRuntime.mm` requires `SportsPushInput` but never pushes JSON (UCODE-17).
  - Swift keeps dead aim-lesson state (UCODE-09).
  - The legacy `HeroLockerExport` wrote `GolfArcade/Unity/CharacterAssets/HeroV4*`; no Swift code loads `HeroV4.json`/`.bin` (only a `HeroV4` constants type).
  - `BuildMenu`'s Info.plist text describes the removed Mac-club mode.
- **pipelines (work/):**
  - `Editor/PostcardV3Scratch` symlinks into `work/postcard-look/v3` (UCODE-21).
  - Live builders read `work/match-anim-set`, `work/cloth-overhaul`, `work/hero-mainstay` and `work/classic-tennis-racket` (UASSET).
  - The last script callers of each tool are in `scratchpad/audit/unity-code/work/execmethods_scripts.json` and `classname_in_scripts.txt`.
- **tests-ci-server:**
  - Fixtures: `StandardArmSportTests` needs `Tests/Fixtures`.
  - Expected failures: UCODE-35.
  - `Unity/Assets/InitTestScene*.unity`: ignored test-run leftovers, one dated today (Oct 10, 16:11), so a run may be in progress. Do not touch them now.
  - `Tools/PostcardCheck` compiles `Course/Hole.cs`, `CourseShot.cs`, `Wind.cs`, `Scorecard.cs`, `Shot/BallFlight.cs`, `GolfClub.cs` and `Swing/MotionSwingDetector.cs` with dotnet. Its two `.cs.pending` files depend on `Editor/PostcardImportCheck`.

## Summary table

| Verdict | Findings | Dead / removable lines |
|---|---|---|
| SAFE | 02, 13, 14, 22, 30; 05 (AppLauncher+PhoneController); 17 (part) | game 974 (02 97 + 13 469 + 14 55 + 05 353); editor 2,539; tests 329; native ~15 |
| SAFE+FIX | 01, 03, 07, 10, 18, 21; 05 (listener); 17 (input queue) | game ~2,070 (01 ~1,306 + 03 ~140 + 07 ~170 + 10 100 + 05 listener 350) + 1 shader (92); editor 2 symlinks (225 lines outside the repo); tests 79 (ReplayTests) |
| DECIDE | 04, 06, 08, 09, 11, 12, 19, 23, 24, 25, 26, 27, 28, 31, 32, 33, 34, 35, 37 | game ~5,370 (04 759, 06 ~4,100, 08 ~150, 09 ~80, 11 137, 12 147) + shaders 308; editor 15,952; tests 2,937; tools 105 |
| KEEP | 15, 16, 20, 29, 36, 38 + asmdefs/link.xml | — |

Dead or removable lines per folder:

| Folder | Lines in area | SAFE | SAFE+FIX | DECIDE |
|---|---|---|---|---|
| Scripts/Tennis | 22,285 | ~345 | ~1,550 | ~220 |
| Scripts/Game | 10,536 | ~230 | ~245 | ~1,110 |
| Scripts/UI | 6,970 | ~15 | ~15 | ~2,535 |
| Scripts/Course | 9,757 | ~65 | — | ~900 (759 editor-only) |
| Scripts/Net | 1,111 | ~295 | 280 | ~545 |
| Scripts/Profile, Swing, Shot, Multiplayer | 2,349 | — | — | ~85 |
| Scripts/Hub (new) | 2,910 | ~17 | — | — |
| Shaders (all folders) | — | — | 92 | 308 |
| Plugins/iOS | ~400 | ~15 | ~10 | ~10 |
| Editor | 22,364 (+225 symlinked) | 2,539 | symlink | 15,952 (KEEP 3,873) |
| Tests | 14,920 | 329 | 79 | 2,937 |
| Unity/Tools | 240 | — | — | 105 |

Notes:
- Line figures for parts of live files are estimates (±15 %).
- The "Hub" items are new, uncommitted code. Their unused helpers may only be unwired.
- About 18 % of the game code (~10,000 of 56,000 lines) is legacy or tooling not on any shipping path, but ~4,100 of that is the Unity-menu layer that editor testing still uses (UCODE-06, DECIDE).
- Scratch evidence is in `scratchpad/audit/unity-code/work/`: `graph3.json` (type graph), `reach3.json`, `unref_methods.json`, `unref_members.json`, `table_Unity_Assets_Editor.json`, `tests_table.json`, `shaders.json`, `execmethods_scripts.json`.
