# Unused Swift app code: audit of GolfArcade/ (read-only)

**Scope:** every `.swift`, `.h` and `.mm` file under `GolfArcade/`: 114 Swift files (28,877 lines), plus `Unity/GolfArcade-Bridging-Header.h`, `Unity/SportsRuntime.h` and `Unity/SportsRuntime.mm`. Working tree of branch `final-build` (HEAD 14a43331) as of 2026-10-10 19:40. No Swift file changed between the compiler dump and the end of the audit. Of these files, 112 are tracked in git. The two untracked ones, `Unity/HubSession.swift` and `Unity/HubControllerView.swift`, are live code.

**Method:**
1. Compiler-accurate reachability. `swift-frontend -dump-ast -dump-ast-format json` was run on every file (iOS 17 simulator target, Swift 6, `-D DEBUG`, the real bridging header). It compiled with 0 errors. All output went to scratch.
2. Every declaration was then linked to the declarations it really uses, through the compiler's own references and demangled type names.
3. Reachability was walked from the `@main GolfArcadeApp`.
4. The walk also counts things the framework calls without our code naming them: overrides, `@objc` methods, protocol witnesses such as `body` or delegate callbacks, data saved and loaded through `Codable`, the scene delegate named in the plist, and enum cases built from raw values or `allCases`.
5. Two cruder cross-checks gave the same file-level result: a regex type graph and a member "fixpoint".
6. Reachable but never-entered states (menu ids, flags that are never set) were read by hand.
7. Test files were checked separately. Code used only by tests counts as **not** live app code.

Scratch evidence is in `scratchpad/audit/swift-app/`:
- `astdead.txt`: every dead declaration with its line range
- `astgraph.py`
- `reach.txt`
- `deadfile_tests.txt`
- `never_instantiated.txt`

**Headline:**
- **61 Swift files (13,026 lines) are entirely unreachable.** Most of them are the old SceneKit/camera body-tracking golf game: `ContentView`, `Features/`, `Game/`, `Mock/`, `Avatar/`, `Benchmark/`, `Swing/`, `Shot/`, `Scene/`, `Feedback/`, most of `Tracking/`, and `Domain/Club.swift` + `Domain/PoseFrame.swift`.
- Another **~2.0k lines** inside live files are dead or unreachable.
- Two areas are developer/test-only (onboarding UI, classic developer menu).

Two warnings:
- The unit-test target (`GolfArcadeTests`) **already does not compile at HEAD**, independent of this audit. `MotionClubReviewTests.swift:201-229` uses `SportsSession.golfCalibrating`, `golfCalibrationCount` and `golfCalibrationRequired`, which no longer exist.
- The old-game files reference each other. Delete SWIFT-01…SWIFT-10 **as one batch**: deleting only some of them breaks the compile of the dead ones left behind.

---

## A. Old camera / body-tracking golf game (the "historical" README app)

The app root is `GolfArcadeApp → SportsHome()`. Nothing reachable from it, and nothing in `Unity/`, `Multiplayer/`, `Onboarding/` or `App/GolfArcadeApp.swift`, mentions any type in this group. This was verified by compiler references and by `rg -w` on comment- and string-stripped copies.

This code loads **no bundle resources**: it builds its scenes in code. Deleting it strands no assets. The frameworks it uses are Vision and AVFoundation camera capture. The live app still uses AVFoundation for other things, so nothing needs changing in the project's link settings.

### SWIFT-01: `GolfArcade/App/ContentView.swift`
- **What:** The old app's root screen. It switched between main menu, player setup, body scan, course select, course play and Practice Lab. It is no longer the root (`GolfArcadeApp` shows `SportsHome`).
- **Size:** 1 file, 104 lines. Tracked: yes.
- **Evidence:** `ContentView` and `CameraControllerOwner` appear only in this file and its own `#Preview`. There are 0 references elsewhere in app or tests.
- **Impact if removed:**
  - The app compiles: nothing references it.
  - It is the only reader of the UI-test launch arguments `-skipPlayerCalibration`, `-fixturePlayers`, `-startInCameraMode` and `-startCourse`.
  - So `GolfArcadeUITests/CameraPlayTests.swift` (292 lines) and `CoursePlayTests.swift` (189 lines) already cannot pass. They drive this screen through the real app, which never shows it.
- **Verdict: SAFE+FIX.** Delete the two UI-test files with it. Optionally drop those strings from the "legacy args" list at `Onboarding/OnboardingFlow.swift:28-30`, which is harmless string matching.
- **Confidence:** high

### SWIFT-02: `GolfArcade/Features/**` (old menus and screens)
- **Files:** 16 files, 3,357 lines. Tracked: yes.
  - `Arcade/{ArcadeView,RangeView,ShotResultCard,SwingFeedbackOverlay}`
  - `Calibration/CalibrationView`
  - `Course/{CameraClubOverlay,CameraStage,ClubRail,CoursePanels,CourseScreen,PowerGauge}`
  - `Menu/{CourseSelectView,MainMenuView,MenuStyle,PlayerSetupView}`
  - `Practice/PracticeRangeScreen`
- **What:** The old game's SwiftUI screens:
  - main menu and settings
  - player setup and body-scan calibration
  - course select
  - course play with camera stage, club rail, power gauge, scorecards and panels
  - Practice Lab / benchmark screens
  - `Palette`, the `SwingInput` enum and the `View.onNavGesture` extension
- **Evidence:** Only `ContentView` (SWIFT-01) and each other use these types.
- **Impact if removed:**
  - No app impact.
  - `GolfArcadeTests/CameraSwingTests.swift` uses `CameraBallFocus` (from `CameraStage.swift`).
  - `README.md` §"Playable mock"/"Practice Lab" and `docs/PLAYABLE_MOCK.md`, `docs/PRACTICE_LAB.md` and `docs/AUDIT_EXECUTION.md` describe these screens.
- **Verdict: SAFE+FIX.** Delete together with SWIFT-03…10 and the old-game tests listed in section F.
- **Confidence:** high

### SWIFT-03: `GolfArcade/Game/**` (old round/course model)
- **Files:** 6 files, 1,368 lines. Tracked: yes.
  - `ArcadeGameSession`
  - `ClubContact`
  - `Course`: `Course`, `Hole`, `Terrain`, `CoursePoint`, `CourseLie`, …
  - `CourseRound`: `CourseRound`, `HoleNavigation`, `GreenRead`
  - `CourseShot`: `RangeShot`, `ShotRequest`, `ShotType`, `ShotGeometry`
  - `GameFlow`
- **What:** The old SceneKit golf game's rules: 3-hole Easy/Medium/Hard courses, rounds, shots, lies, and the menu-flow object. This is **not** the live golf game, which runs in Unity: Cliffside, Wild Isles, Magma Open.
- **Evidence:** Referenced only by SWIFT-01/02/04/05/06.
- **Impact if removed:** Tests that reference these types:
  - `CourseTests` (whole file)
  - `TerrainTests`
  - `AvatarTests`
  - `PresentationTests`
  - `GolfArcadeTests` (`GameFlow`)
  - `RangeSimulationTests`
  - `SyntheticGolferTests`
- **Verdict: SAFE+FIX.** Delete those tests.
- **Confidence:** high

### SWIFT-04: `GolfArcade/Mock/**` (old swing detection, ball flight, 3D course renderer, audio)
- **Files:** 6 files, 3,433 lines. Tracked: yes.
  - `ArmSwing` (719)
  - `BallFlight` (262)
  - `CameraSwing` (453)
  - `CourseScene` (1,377)
  - `MotionSwing` (212)
  - `RangeAudio` (410)
- **What:** Despite the folder name, this is the old game's engine:
  - camera arm-swing detector
  - ball physics
  - `CameraSwingController`
  - the SceneKit course renderer
  - a phone-motion swing detector (`MotionSwingDetector`, `PhoneSwingController`)
  - procedural range audio

  The live golf swing is detected by `Unity/SportsMotion.swift` and Unity.
- **Evidence:** No live references.
  - The `@objc step(_:)` at `CourseScene.swift:396` is a display-link callback inside the dead `CourseScene`.
  - `-fixtureLockedCamera`, `-positionReviewSeconds`, `-fixtureSwingAfterLock` and `-flightTimeScale` are read only here and in SWIFT-03.
- **Impact if removed:** Tests that reference these types:
  - `CameraSwingTests`
  - `BenchmarkTests`
  - `RangeSimulationTests`
  - `SyntheticGolferTests`
  - `TerrainTests`
  - `MotionSwingDetectorTests` (whole file)
  - `PresentationTests` (`RangeAudio`, `CourseScene`)
  - `CourseTests`
  - `AvatarTests`
  - the UI test `CameraPlayTests`
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-05: `GolfArcade/Avatar/**` (old avatar that copied the player's body)
- **Files:** 7 files, 1,192 lines. Tracked: yes.
  - `AvatarAnimations`
  - `AvatarRig`
  - `BodyPose3D`: `GolferStance`, `CameraPoseProjection`, `AvatarSize`, `BodyPose3D` and a top-level `smoothstep`
  - `ClubGeometry`
  - `PoseRetargeter`: also `TorsoTurnEstimator`, `CameraAvatarPoseFilter`, `OneEuroFilter`
  - `SwingReaction`
  - `SwingRecorder`
- **What:** The procedural SceneKit golfer that mirrored the camera-tracked body. Not related to the live `MatchHero*` heroes exported from Unity.
- **Impact if removed:** Tests `AvatarTests` (whole file is old-game), `PresentationTests`, `SyntheticGolferTests`, `CameraSwingTests`, `CourseTests`.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-06: `GolfArcade/Benchmark/**` (Practice Lab benchmark)
- **Files:** 4 files, 888 lines. Tracked: yes.
  - `BenchmarkRecords` (9 record types)
  - `DisplayLatencyMeasurement`
  - `PracticeLab`
  - `SyntheticGolfer`
- **What:** The old Practice Lab's trial recorder, display-latency probe and synthetic test golfer. The live frame-time benchmark is the unrelated `-benchTennis`/`-benchGolf` path in Unity.
- **Impact if removed:** Tests `BenchmarkTests` (whole file), `CaptureEvidenceTests` (`DisplayLatencyMeasurement`), `SyntheticGolferTests`, `TerrainTests`, `CameraSwingTests` (`BenchmarkReplay`). Docs `docs/PRACTICE_LAB.md`.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-07: `GolfArcade/{Swing,Shot,Scene,Feedback}/**`
- **Files:** 6 files, 876 lines. Tracked: yes.
  - `Swing/BallAddress.swift` (234), `Swing/SwingStateMachine.swift` (173)
  - `Shot/FlightPath.swift` (29), `Shot/ShotEngine.swift` (90)
  - `Scene/ShotCameraDirector.swift` (207)
  - `Feedback/SwingFeedbackController.swift` (143)
- **What:** The old game's virtual club address, swing phases, arcade shot engine, flight path, shot camera and feedback sounds.
- **Impact if removed:** Tests `AvatarTests`, `CameraSwingTests`, `SyntheticGolferTests`, `GolfArcadeTests` (`SwingStateMachine`, `FlightPath`, `ArcadeShotEngine`), `PresentationTests`, `BenchmarkTests`.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-08: `GolfArcade/Tracking/**` camera files (all of `Tracking/` except `PlayerCalibration.swift`)
- **Files:** 9 files, 1,472 lines. Tracked: yes.
  - `AimSignalRecognizer` (72)
  - `CameraPoseTracker` (690)
  - `CameraPreview` (67)
  - `CaptureCapabilities` (94)
  - `ClubSignRecognizer` (73)
  - `HandGestureRecognizer` (167): `NavGesture`, `HandShape`, `HandReading`, `HandGestureRecognizer`
  - `PoseDelivery` (73)
  - `PoseSkeletonView` (67)
  - `SourceVideoRecorder` (169)
- **What:** The Vision body-pose camera tracker and everything around it: hand-gesture menu navigation, club/aim hand signs, capture-format selection, source-video recording, skeleton overlay.
- **Note:** The live app's camera use is ARKit in `Unity/SportsMotion.swift` + `SportsPreview.swift`, which is separate code.
- **Evidence:** The regex graph marked `HandGestureRecognizer` "live" only through a name clash: both it and `GolfControllerMap` declare a nested `Point`. The compiler references show nothing live uses it. `HandShape`/`HandReading` are used only by `PoseFrame` (SWIFT-09).
- **Impact if removed:** Tests `AimSignalRecognizerTests`, `ClubSignRecognizerTests`, `HandGestureRecognizerTests`, `CaptureEvidenceTests` (whole files), `CameraSwingTests`, `GolfArcadeTests` (`PlayerPoseSelector`, `CameraRotation`, `PoseSkeletonMapper`), `BenchmarkTests`.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-09: `GolfArcade/Domain/Club.swift`, `Domain/PoseFrame.swift`, and the dead part of `Domain/SwingMetrics.swift`
- **Size:** Tracked: yes. Total ≈ 301 dead lines.
  - `Club.swift` (48): `GolfClub` driver/iron/wedge/putter.
  - `PoseFrame.swift` (219): `BodyJoint`, `PosePoint`, `PoseFrame`, `BodyDepthEstimate`, `BodyOrientation`, `CameraPoseMode`, `UprightPoseResult` and an extension.
  - `SwingMetrics.swift` L9-42: `SwingPhase`, `SwingMetrics`, `SwingEvent`, plus `Handedness.displayName` at L6.
- **What:** Shared data types of the old camera game.
- **Keep:** `Handedness` (SwingMetrics.swift L3-8) **is live**: `Player.handedness`, the classic editor, and the "hand" setting.
- **Impact if removed:**
  - `PoseFrame` is still referenced by dead methods in `Tracking/PlayerCalibration.swift` (SWIFT-10). Delete those in the same change, or the build breaks.
  - Tests: `GolfArcadeTests` (`SwingMetrics`, `SwingEvent`, `PoseFrame`, `BodyJoint`), plus many old-game tests.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-10: `GolfArcade/Tracking/PlayerCalibration.swift`, the dead behaviour parts
- **Size:** ≈ 242 of 323 lines. Tracked: yes.
  - `PlayerCalibration.matchScore` L29-46
  - DEBUG `uiTestingFixture` L19-27
  - `BodySignature.init(frame:)`, `averageSegment`, `init(averaging:)`, `distance(to:)`, `appendAverageSegmentDifference` L71-165
  - `BodyAnchor.init(frame:bounds:)`, `init(averaging:)` L179-189
  - `CalibrationAssessment` L192-207
  - `CalibrationAccumulator` L209-295
  - `PlayerCalibrationStore.save/clear` L307-314
  - top-level `pointDistance`, `midpoint` L317-323
- **What:** The body-scan logic: measuring proportions from camera frames and matching a player to a pose. Only the old game used it.
- **Impact if removed:**
  - Must go together with SWIFT-09 (these methods take `PoseFrame`).
  - Tests: `GolfArcadeTests` (`CalibrationAccumulator`, `matchScore`, `PlayerCalibrationStore` save/clear), and `AvatarTests`, `PresentationTests`, `SyntheticGolferTests` (`uiTestingFixture`).
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-11: `Player.calibration` field and the remaining calibration data shape
- **What:** The saved player profile still contains an optional "body scan" field:
  - `Domain/Player.swift` L9, `isScanned` L54, the `calibration:` init parameter
  - `PlayerCalibration` / `BodySignature` / `BodyAnchor` stored fields + memberwise inits (≈70 lines)
  - `PlayerRosterStore.load` (Player.swift L88-104) clears outdated scans and migrates the pre-multiplayer `playerBodyCalibration.v2` / `range.handedness` keys through `PlayerCalibrationStore.load`
- **Status:** Live only as save-file plumbing. Nothing in the live app ever creates a scan, and `isScanned` has no live reader.
- **Size:** ≈ 75 lines. Tracked: yes.
- **Impact if removed:**
  - Old saves still load: JSONDecoder ignores unknown keys.
  - The legacy single-player migration path would no longer carry a scan. That doesn't matter, since nothing uses scans.
  - Tests `GolfArcadeTests` (roster/calibration tests) and `AvatarTests` (`BodyAnchor`) need edits.
- **Verdict: DECIDE.** Small payoff; it touches the save format.
- **Confidence:** high

---

## B. Onboarding and tutorial leftovers

### SWIFT-12: Dead onboarding screens
- **Files:**
  - `Onboarding/GolfTutorialController.swift` (7): a stub that just shows the golf controller.
  - `OnboardingMotionPrimer.swift` (14): the "your phone is the racket" step.
  - `OnboardingRewardScreen.swift` (29): the "tutorial complete +XP / White Headband" screen.
- **Size:** 3 files, 50 lines. Tracked: yes.
- **Evidence:**
  - `OnboardingRoot` never shows the `.motionPrimer` / `.reward` steps.
  - `OnboardingFlow.init` maps those steps to `.choose` (L41-43).
  - `GolfTutorialController` has no references.
- **Impact if removed:** None. No tests name these types.
- **Verdict: SAFE**
- **Confidence:** high

### SWIFT-13: Tutorial/reward pipeline leftovers inside live files
- **What:** The removed first-time tutorial and its reward:
  - `Onboarding/OnboardingFlow.swift`: `launchTutorialIfNeeded`, `tutorialStarted`, `resumeTutorial`, `showCoachIntro`, `continueIntro`, `reportedStep`, `hit`, `completed`, `replay`, and the `firstHit`/`reportedExercise`/`coachIntro`/`tutorialBegan` state (L87-119, L146-152, ≈44 lines). Nothing calls them; `replay()` is test-only.
  - `Onboarding/OnboardingStore.swift` L36-39, L67-78: `tutorialStep`, `rewardClaimed`, `anyRewardClaimed`, `markReward`, `rewardXP`, `rewardItem`, `requestedHeadband`, `requestEquip` (16 lines).
  - `Unity/Progression.swift`: `awardTutorial` (L140-168, 29 lines, called only from dead `completed()`), `LevelReward`/`LevelRewards` (L55-66, 12 lines; test-only).
  - `Unity/TennisMenu.swift`: `startTutorial`/`tutorialFinished` (L503-507; only called from dead `continueIntro`).
  - `Unity/TennisStory.swift`: `tutorialIntro`/`tutorialDone` (L42-48).
- **Size:** ≈ 115 lines. Tracked: yes.
- **Tests:**
  - `OnboardingTests` (`replay`, `awardTutorial`, `rewardItem`, `requestedHeadband`, `tutorialStep`, `LevelRewards`, `clearEvents`)
  - `PostMatchSnapshotTests` (`LevelRewards`)
  - `MenuFlowRegressionTests` (`rewardClaimed`)
  - `TennisMenuSnapshotTests` (`tutorialStep`)
- **Verdict: SAFE+FIX.** Trim those tests. `OnboardingStep.tutorial/.reward/.motionPrimer` can then go too.
- **Confidence:** high

### SWIFT-14: Onboarding UI reachable only with the `-forceOnboarding` launch argument (debug / UI tests)
- **Files:**
  - `OnboardingRoot.swift` (56)
  - `OnboardingAccountScreen.swift` (24)
  - `OnboardingCharacterScreen.swift` (70)
  - `OnboardingConnectScreen.swift` (54, incl. `HowToArt`)
  - `OnboardingGamePicker.swift` (23)
  - `GameCenterAccount.swift` (99: `GameCenterAccount`, `Live/FakeGameCenterAccount`, `OnboardingAccountModel`)
  - ≈60 lines of navigation in `OnboardingFlow` (`next`, `guest`, `playOnTV`, `primer`, `choose`, `back`, `finish`…)
- **Size:** ≈ 386 lines. Tracked: yes.
- **What:** The first-run setup (account → character → connect TV → pick a game).
- **Evidence:** `OnboardingFlow.init` L45-48: "The shipped app opens the menu directly". Without `-forceOnboarding` the step is always `.done`, so `holdsMenu` is false and `OnboardingRoot`/`OnboardingHoldingCard` never show. `replay()` is not wired to any setting.
- **Dependents:**
  - `GolfArcadeUITests/OnboardingUITests.swift`, `GolfArcadeTests/OnboardingTests.swift`
  - `SportsHome.swift:15`, `TennisPlayViews.swift:14`, `HubSession.swift:65` (the `holdsMenu` checks; keep `OnboardingFlow.shared.exitToMenu/active`)
  - It is the only live user of `ClubArt` + `Club.scene(for:)` (club-*.png / scene-*.jpg art)
- **Verdict: DECIDE.** Keep it if onboarding will come back.
- **Confidence:** high

---

## C. Dead or unreachable code inside the live `Unity/*.swift` app

### SWIFT-15: `Unity/PlayfulGraphics.swift`
- **What:** `enum Showroom`, a leftover colour/font palette ("Fall Guys / Wii showroom style"). The drawings its header describes were already removed.
- **Size:** 1 file, 19 lines. Tracked: yes.
- **Evidence:** 0 references.
- **Verdict: SAFE**
- **Confidence:** high

### SWIFT-16: `Unity/ArcadeScreens.swift` loading-screen leftovers
- **What:**
  - `PresentationPortraits` (L131-157): offscreen-rendered hero portraits.
  - `PresentationRosterCard` (L159-187).
  - `LoadingScreen.roster` (L231-251): the "you vs rival" roster block. It is computed but never put into `body`.
  - `LoadingScreen.label(_:_:)` (L265-267).
  - Empty `// MARK:` headers L9-26 and L104-105, left after the old arcade screens were removed.
- **Size:** ≈ 85 lines. Tracked: yes (file modified).
- **Evidence:** `roster` is private and unused; the other three are used only by it or by nothing.
- **Verdict: SAFE**
- **Confidence:** high

### SWIFT-17: `Unity/ArcadeScreens.swift`: never-shown `SettingsScreen` view + `PartyLoadingTips`
- **What:**
  - The `SettingsScreen` view body (L28-74, ≈47 lines) is never instantiated. Only its static `describe(_:_:_:)` (L76-101) is used, by `TennisMenu.label(for:)`. `describe` also has dead cases `"coaching"`, `"resetTips"`.
  - `PartyLoadingTips` (L107-129, 23 lines): loading tips nothing shows.
- **Size:** ≈ 72 lines.
- **Fix needed:**
  - Move `describe` into `TennisMenu` before deleting the view.
  - `TennisMenuSnapshotTests` references `PartyLoadingTips`.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-18: `Unity/TennisMenuViews.swift` old arcade-style components
- **What:**
  - `Plaque` (L89-102)
  - `MenuBackdrop` (L105-127; loads `menu-backdrop.jpg`)
  - `HeroArt` (L148-165; loads `menu-hero-*.png`)
  - `Stars` (L168-180)
  - `OpponentCard` + its `badge` extension (L281-338)
  - `Arcade.roundColors` (L24-32), `Arcade.seaDeep/crimsonDeep/lime` (L16-19)
  - unused `@Environment reduceMotion` (L232)
- **Size:** ≈ 138 lines. Tracked: yes.
- **Evidence:** Compiler references show 0 live users. `TennisCampaign.beaten(_:)` and `TennisOpponent.art`/`boss` were used only by `OpponentCard` (see SWIFT-25).
- **Verdict: SAFE**
- **Confidence:** high

### SWIFT-19: `Unity/ClubDesign.swift` + `ClubScreens.swift` unused components
- **What:**
  - `ClubCard` (ClubDesign L344-394)
  - `ClubPartyScreen` (ClubScreens L54-75, plus empty MARKs L76-95)
  - `ClubEntrance` modifier + `View.clubEntrance` (L284-294)
  - `Club.color(_:)` (L24-28)
  - `ClubButton.Style.secondary/.quiet` (L315 + 2 switch arms)
  - `ClubBadge.Kind.new/.locked/.done/.boss` (L397-406)
- **Size:** ≈ 95 lines. Tracked: yes.
- **Verdict: SAFE**
- **Confidence:** high

### SWIFT-20: Unreachable "coming soon" sport screen: `MenuScreen.locked(Sport)`
- **What:**
  - `ClubLockedScreen` (ClubScreens L28-52)
  - `ClubBadge` (ClubDesign L396-411; after SWIFT-19 only this screen uses it)
  - `Sport.tagline` (SportProgress L10-18)
  - the `.locked` arms in `TennisMenu.rows/back/select`, `TennisMenuViews` L261, `TennisPlayViews` L115
- **Evidence:**
  - `.locked` is only produced by `select()` for a `sport-x` id of an unplayable sport.
  - `rows(.gameSelect)` lists only playable sports, and `tap()` refuses ids not in `rows()`.
  - `HubSession.route` never opens it.
  - So the screen cannot appear. Bowling/football/boxing exist in `Sport` only for this.
- **Size:** ≈ 55 lines.
- **Verdict: SAFE+FIX.** Remove the enum case and its switch arms in 4 files. `menu_audit.py` (work/menu-hub) does not catch this, because it counts `show(...)` calls, not whether they can be reached.
- **Confidence:** high

### SWIFT-21: `Unity/HowToCards.swift`: old b-roll player and golf-lesson leftovers
- **What:**
  - `LoopingVideo` (L92-117): plays `MenuVideo` b-roll clips.
  - `HowTo.tips` (L53-77)
  - `HowTo.loadingCards` (L49-51)
  - `HowToCard.Art.golfSwing/golfAim/golfClubs` (L6)
  - `GolfLesson` (L80-88) plus `TennisMenu.lessonCard` (L124) and the `golf:` branch of `IslandGuideScreen` (ClubScreens L195-218). `golf:` is always `false`: the router passes `golf: false`, and the golf-lesson screen was removed on 10-10. Those golf branches can never run.
- **Size:** ≈ 70 lines.
- **Keep:** `LoopingPlayer` (L119+) **is live**; `GameplayPreview` uses it.
- **Tests:** `TennisMenuSnapshotTests` sets `LoopingVideo.stillsOnly`.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-22: Retired "aim calibration lesson" (tennis racket-face aim exercise)
- **What:**
  - `SteeringFilter.swift`:
    - `TennisAimLesson` (L352-425, 74 lines)
    - `TennisAimWing.fit` (L321-335)
    - `SportsMotionGeometry.aim/aimSpanDegrees/aimDeadZone/learnNeutral/minTVDistance/idealTVDistance` (L60-80)
  - `SportsSession.swift`:
    - `aimLesson`, `aimLessonMessage`, `aimChecked`, `aimFeedSequence`, `aimFeedAt`, `aimWaiting`, `aimSwing`, `aimFeedTask`, `aimTimeoutTask`, `aimingSetup` (L158-167)
    - the `onAimSwing` handler (L210-213)
    - resets (L342, 584, 593, 714)
  - `SportsMotion.swift` L421-424: builds a `TennisAimSwing` for a handler that always discards it.
  - `TennisPlayViews.swift`: `AimingCalibrationPanel` stub (L801-805, "retired"), unused `@State shotAim` (L257).
- **Evidence:** `aimLesson` is never set to a value and `aimWaiting` is never set `true`, so every path returns early.
- **Size:** ≈ 135 lines.
- **Tests:** `SportsIntegrationTests` (`AimingCalibrationPanel`, `aimLessonMessage`, `learnNeutral`, `aimSpanDegrees`).
- **Verdict: SAFE+FIX**
- **Related decision (DECIDE):** `SportsSession.storedAimProfile()` (L440-447) still loads aim profiles saved by older builds. Nothing writes new ones.
- **Confidence:** high

### SWIFT-23: Retired tennis "ultimates"
- **What:** In `SportsSession.swift`:
  - `TennisUltimate` (L7-17)
  - `ultimateChoice` (L103)
  - the no-op `chooseUltimate`/`armUltimate` (L111/113, "retained API for older callers")
  - write-only `ultimateMeter`, `ultimateArmed`, `canArmUltimate`, `loadoutLocked` (L104-109, 347, 524, 861-862)
- **Size:** ≈ 22 lines.
- **Tests:** `TennisMenuSnapshotTests` L339-345.
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-24: `Unity/TennisMenu.swift` dead members and menu ids
- **What:**
  - `confirmCampaignRound` + `pendingCampaignRound`: never set non-nil (L84-93).
  - `continueLabel`/`continueJourney` (L232-241).
  - `setBodySize` (L474-479).
  - `afterMatchChoices`: test-only (L650-658).
  - `beginPostMatch` (L662-665).
  - `AfterStory.round/.hub` cases, never built, and their switch arms (L130, L422-424, L535-537).
  - `storyInstant`: write-only (L132).
  - `select()` handlers for ids no screen produces: `"homePlay"` (L305), `"rival<N>"` (L358-361).
  - `label(for:)` labels for removed ids (ArcadeScreens L279-301): `homeCampaign`, `store`, `homePlay`, `tutorial`, `replayTutorial`, `golfCampaign`, `golfTraining`, `nextCard`, `randomize`, `reset`, `body`/`skin`/`shirt`/`shorts`/`accent`/`racket`, and the `rival` prefix.
  - `begin(…, onPhone:)`: the parameter is ignored (`let onPhone = false`, L564), so the phone-only-play plumbing is vestigial.
  - `debugLaunch`: unused even by tests.
  - The same dead-id pattern is in `IslandScreens.swift`: the Hub `"golfCampaign"`/Driving-Range modes (L52-55), and the Settings `"coaching"`, `"resetTips"`, `"replayOnboarding"` items (L299-301).
- **Size:** ≈ 75 lines. Tracked: yes (modified).
- **Tests:** `TennisCampaignTests`/`TennisMenuSnapshotTests` (`afterMatchChoices`).
- **Verdict: SAFE+FIX**
- **Confidence:** high

### SWIFT-25: Small unused members across live files (catch-all)
- **Size:** ≈ 130 lines total. All files are tracked, except that `HubSession.retry` is in an untracked file.
- **`CharacterModelPreview.swift`:**
  - `CharacterOptions.hair/hairColors/faces/heights/builds/scale/faceScale` (L6-18; only `hairHex` is live)
  - `PreviewFraming.torso` (L23)
  - unused `reduceMotion` (L104)
  - `headShotCamera` (L198-201)
  - `HeroV4.headwear` (L448)
  - `MatchHero` typealiases `Sub/Part/Mat/Manifest` (L473-476)
  - `previewData` (L479)
  - `LobbyHeroStage.introduce` (L593)
- **`TennisCampaign.swift`:** `TennisOpponent.boss/art`, `beaten(_:)` (L15, L24, L89).
- **`TennisStory.swift`:** `art(for:female:)` (L31-38).
- **`TennisMapScreen.swift`:** `TennisVenueChoice.badge/tint` (L18-23).
- **`SportProgress.swift`:** `Sport.colors`, `Sport.clips` (L26-37), `Outfit.color/hex/name` (L86-97).
- **`Progression.swift`:** `XPLine.title/detail`, `PostMatchSummary.lines/practice` (L71-84). These are filled but never shown.
- **`LoadingModel.swift`:**
  - `progress` (L18)
  - `practice()` + `startPracticeInput()` (L109-122): the loading-screen "practice swing" gyro input is never started
- **`SportsMotion.swift`:** `previewSession` (L130), `cancelAxisCapture` (L268), `suspend` (L523).
- **`SportsSession.swift`:** `showMatchRewards` (L739-742), `TennisContact.timingWord/gradeNames/gradeName` (L938-940).
- **`MotionClubMenus.swift`:** `ClubPreviewActivity.locker/settings/store` (L16), unused `reduceMotion` (L30), `ClubCameraHost.Controller.stop` (L118).
- **`ClubEnvironmentView.swift`:** unused `reduceMotion`/`scenePhase` (L44, L141-142).
- **`IslandKit.swift`:** `IslandUI.ring` (L7).
- **`LockerColors.swift`:** `hairPresets` (L14-15), `Player.hairDyed` (L119).
- **`MatchHeroSurfaces.swift`:** `HeroColor` typealias (L9), `blinn` (L70).
- **`MatchHeroData.swift`:** `Look.isCharacter` (L34).
- **`HubSession.swift` (untracked):** `retry` (L148).
- **`MultiplayerService.swift`:** `clearError` (L102).
- **`Domain/Player.swift`:** `isScanned` (L54).
- **`SportsRuntime.h/.mm`:** `-loadInWindow:error:` (h L20, mm L36-38). Swift always calls the `controllerReplica:` variant.
- **Tests:** These test files reference some of the above, so editing them is the "fix":
  - `TennisCampaignTests` (`showMatchRewards`, `timingWord`, `gradeName`, `beaten`)
  - `LockerMirrorProofTests` (`HeroColor`)
  - `MatchHeroLockerTests` (`MatchHero.stageHeight`)
- **Verdict: SAFE**, and **SAFE+FIX** for the test-referenced items.
- **Confidence:** high for compiler-confirmed items. Medium for "never shown" fields.

### SWIFT-26: Bug-like dead call: `HubControllerView.swift:39` `TennisMenu.shared.tap("homePlay")`
- **What:** The Plaza controller's Party button, used when no party exists, calls `tap("homePlay")`. `tap()` only acts on ids present in the current screen's `rows()`, and `"homePlay"` is in none of them. So after `showClassic()` this tap silently does nothing.
- **Probably intended:** `tap("play")` (main → Play/party), or `openFromPlaza(.party)`.
- **Size:** 1 line. The file is untracked.
- **Verdict: DECIDE.** It's a fix, not a removal: hub-area owner.
- **Confidence:** high

---

## D. Developer / DEBUG-only paths (live code, but not reachable in a shipped build)

### SWIFT-27: Classic developer form and test harnesses in `Unity/SportsHome.swift` + `TennisMenu.swift`
- **What:**
  - `ClassicSportsHome` (L144-203), private `TennisController` (L236-291), `SportsPlayerEditor` (L293-305), `SportsControls` (L307-360): ≈183 lines. This is the old multi-sport options form. It is reachable only from Settings → Developer → "Classic options menu", and `SettingsTab.visible` hides the Developer tab outside `#if DEBUG`.
  - `PlayabilityCheck` (L362-400, 39 lines): runs only with `--playability-check`. Nothing in the repo's code, tools or docs passes that flag.
  - `OnlineLobbyProofDriver` (TennisMenu.swift L1182-1265, 84 lines, `#if DEBUG`): runs only with `--lobby-proof`. No runner exists in the repo; its hooks are in `MultiplayerService`, `LocalMultiplayerTransport` and `CharacterModelPreview`.
  - Release-build developer launch args, used by nothing in tools/tests: `--lobby-mock`, `--resume-tennis-round-2…5`, `--lobby`, `--character-editor` (SportsHome L111-127).
- **Size:** ≈ 330 lines. Tracked: yes.
- **Keep:** `AxisGatePanel` (L205-234) **is live**; `TennisRacketController` uses it.
- **Dependents:**
  - `HubSessionTests`/`TennisMenuSnapshotTests` toggle `menu.classic` and snapshot `SportsHome` with it.
  - `-benchTennis`/`-benchGolf` are documented in `docs/tennis-playtest.md` and `docs/post-game-phone-actions.md` and read by `SportsRuntime.mm` and Unity's `GolfVisualBenchmark.cs`, so keep those.
- **Verdict: DECIDE.** Developer tools: keep or prune.
- **Confidence:** high

---

## E. KEEP (look unused, but are used: don't delete by mistake)

### SWIFT-28: Test-only hooks (`#if DEBUG` unless noted)
- **In the app code:**
  - `TennisMenu.debugShow/debugStory/debugPostMatch` (L747-765; ~170 test uses)
  - `HubSession.debugLive/debugBoot/debugPartyTick/debugReset` (L324-334)
  - `MultiplayerService.mockState/mockNearby` (L635-649)
  - `MultiplayerService.connect(using:)` (not DEBUG; `MultiplayerTests`/`HubPartyTests`)
  - `Analytics.clearEvents`
  - `LobbyHeroStage.Coordinator.resetMeasurement/measuredFPS`
  - `CharacterModelPreview.Coordinator.pauseDisplayLink`
  - `MatchHeroSurfaces.linearToSRGB`
  - `RigData.boneCount`
  - `MenuMotion.serveOnce`
  - `MatchHero.stageHeight/Look/Asset`
  - `PostMatchSummary.levelsGained`
- **What:** Hooks the unit tests drive. Not app code, but removing them breaks tests.
- **Verdict: KEEP**, except `debugLaunch`, which nothing uses (SWIFT-24).
- **Confidence:** high

### SWIFT-29: Looks-dead-but-live (false positives seen during the audit)
- `extension Array { subscript(safe:) }` (TennisMenu L985): used 6×.
- `Color(hex:)` (SportProgress L102).
- `SportsAppDelegate`: via `@UIApplicationDelegateAdaptor`.
- `SportsExternalScene`: named in `SportsInfo.plist` `UISceneDelegateClassName`.
- `@objc refreshNotification` and display-link callbacks.
- Decodable model types (`MatchHeroData.*`, `NativeGolfEquipment.Asset/Finish`, `ClubEnvironmentAsset`).
- UIKit/SceneKit views created through their inherited init (`ClubEnvironmentSceneView`, `LivingClubView`, `LoopingPlayer.PlayerView`).
- `Handedness`, `EmoteCatalog`, `PlayerRosterStore`.
- `HeroV4.haircuts/offered`.
- `TennisAimWing.relative/aim`, `TennisAimProfile`.
- `SettingsScreen.describe`.
- `LoopingPlayer`.
- `AxisGatePanel`.
- `NativeGolfEquipment` (used by `MatchHero` at `CharacterModelPreview.swift:548`).
- Every `SportsRuntime` method except `-loadInWindow:error:`. `pushSample:` is imported into Swift as `push(_:)`.
- `GolfArcade-Bridging-Header.h`.

**Verdict: KEEP.** Confidence: high.

---

## F. Cross-area links (for the other auditors)

- **Tests that only exercise dead old-game code.** Delete them with SWIFT-01…10. ≈3,400 lines:
  - `GolfArcadeTests/{AimSignalRecognizerTests, BenchmarkTests, CaptureEvidenceTests, ClubSignRecognizerTests, CourseTests, HandGestureRecognizerTests, MotionSwingDetectorTests, PresentationTests, RangeSimulationTests, TerrainTests, AvatarTests, CameraSwingTests, SyntheticGolferTests}.swift`
  - the old-game parts of `GolfArcadeTests.swift`
  - `GolfArcadeUITests/{CameraPlayTests, CoursePlayTests}.swift`

  The only live types these touch are `Player`, `Handedness`, `PlayerCalibration*` and `PlayerRosterStore`.
- **Smaller test links:**
  - `SportsIntegrationTests` → SWIFT-22
  - `TennisMenuSnapshotTests` → SWIFT-17/21/23/24
  - `TennisCampaignTests` → SWIFT-24/25
  - `OnboardingTests`, `PostMatchSnapshotTests`, `MenuFlowRegressionTests` → SWIFT-13
  - `OnboardingUITests` → SWIFT-14
  - `MotionClubReviewTests` already fails to compile (removed `golfCalibration*`)
- **Assets used only by dead Swift code:**
  - `GolfArcade/Unity/MenuArt/menu-backdrop.jpg`, `menu-hero-*.png` (13) and `menu-trophy.png`: 15 files, ~6.8 MB. Only SWIFT-18/25 use them.
  - `GolfArcade/Unity/MenuVideo/` (22 files, 39 MB): already excluded by `project.yml`. Only the dead `LoopingVideo` / `Sport.clips` (which returns `[]`) point at it.
  - `MenuArt/club-*.png` and `scene-*.jpg`: reached only through onboarding (SWIFT-14) and the unreachable locked screen (SWIFT-20) via `ClubArt`/`SceneImage`. Verify `scene-*.jpg` against the live `ClubScreen` usage first.
- **Bundled but never loaded by Swift:**
  - `GolfArcade/Unity/CharacterAssets/GolfHero_*`: 30 tracked files, ~46 MB, **not** excluded by `project.yml`. No Swift loads `GolfHero_`; only `MatchHero_*`, `GolfKitHero_*` and `NativeGolfEquipment_*` are loaded.
  - The excluded `Hero*`/`Player*`/`Custom*`/`Higgs*` CharacterAssets are not referenced by any Swift code either.
- **Docs describing dead code:**
  - `README.md`: "Playable mock" and "Practice Lab" sections
  - `docs/PLAYABLE_MOCK.md`, `docs/PRACTICE_LAB.md`, `docs/AUDIT_EXECUTION.md`
- **Hub:** SWIFT-26 (`tap("homePlay")` is a no-op).
- **Menu audit script:** `work/menu-hub/menu_audit.py` passes, but misses the unreachable `.locked` state (SWIFT-20) and the dead `label(for:)` ids (SWIFT-24).

---

## Summary table

| Verdict | Entries | Dead / affected Swift lines |
|---|---|---|
| SAFE | SWIFT-12, 15, 16, 18, 19, 25 (untested part) | ≈ 50 + 19 + 85 + 138 + 95 + ~110 ≈ **500** |
| SAFE+FIX | SWIFT-01–10, 13, 17, 20–24, 25 (test-referenced part) | ≈ 12,957 (the 61 dead files minus the 3 onboarding files and PlayfulGraphics) + 34 (SwingMetrics part) + 242 + 115 + 72 + 55 + 70 + 135 + 22 + 75 + ~20 ≈ **13,800** |
| DECIDE | SWIFT-11, 14, 26, 27 (+ stored aim profile) | ≈ 75 + 386 + 1 + 330 ≈ **790** |
| KEEP | SWIFT-28, 29 | (test hooks and false positives: not dead) |

**Totals:**
- **Dead app Swift ≈ 14,300 lines** (SAFE + SAFE+FIX): 61 whole files (13,026 lines) plus ≈1,270 lines inside live files.
- Another **≈ 790 lines** are unreachable in a shipped build but used by debug/tests or a pending decision (DECIDE).
- Dead code is about **50 %** of the 28,877-line Swift app (**≈ 52 %** counting the DECIDE code).
