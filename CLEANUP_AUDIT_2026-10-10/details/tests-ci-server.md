# Audit: tests, CI and server (area "tests-ci-server")

Repo `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`, branch `final-build`, HEAD `14a43331`, working tree as of 2026-10-10 evening. Read-only audit: nothing in the repo was changed.

## How this was checked

- Read or searched every test file. Matched each launch argument and button ID that the UI tests use against the app code.
- **Compile check without touching the repo.** I ran `swiftc -emit-module` / `-typecheck` (iOS 17 simulator, Swift 6, `-D DEBUG`, the app's bridging header). Every output went to scratch (`audit/tests-ci-server/tc/`). No xcodebuild, xcodegen or simulator was used.
  - (a) Working-tree app, plus all 36 unit-test files and all 7 UI-test files.
  - (b) HEAD: app and tests exported with `git archive` into scratch.
  - (c) Only the 55 app files reachable from the app's entry point (`GolfArcadeApp` → `SportsHome`, `SportsAppDelegate`, the `SportsDisplays` external scene), plus the 21 "live" unit-test files.
- **Results:**
  - (a) Only `MotionClubReviewTests.swift` fails: 17 error sites. All other unit and UI test files typecheck.
  - (b) At HEAD the same file fails, but only on the golf-calibration errors.
  - (c) The 55 live app files typecheck on their own, and the 21 live test files typecheck against them. So nothing live needs the other 59 app files (the old SceneKit/camera golf flow behind `ContentView`) or the 15 test files that use them.
- **Not run:** individual test pass/fail comes from existing logs: `proof/menu-hub/phase6/menu_tests.txt` (2026-10-10) and `work/lobby-parity/NOTE_lobby_heroes.md`.
- **Totals:** the unit-test target has 36 files with 493 test methods: 21 live files with 250 tests, 14 old-flow files with 233 tests, and 1 broken file with 10 tests. 34 of the 36 files are tracked; 2 are untracked.

---

## Findings

### TEST-01 — `GolfArcadeTests/MotionClubReviewTests.swift` stops the whole unit-test target from compiling

**Path:** 1 file, 246 lines, 19 KB. Tracked and unmodified; last commit 9283f6a8 on 2026-10-08.

**What it is:** two test classes.
- `MotionClubReviewTests` (7 tests) checks the 3D "page-flip" menu transition. It also records review videos and screenshots of the older "Motion Club" menus.
- `GolfReadyControllerTests` (3 tests) checks the golf "take three practice swings before you play" setup on the phone.

**Evidence:** compile errors.
- `SportsSession` has no `golfCalibrating`, `golfCalibrationCount`, `golfCalibrationRequired` or `finishGolfCalibration()` (lines 201–229). These were removed in HEAD commit 14a43331, so the practice-swing setup no longer exists.
- In the working tree only, `MenuScreen` has no `.golfLesson` (lines 104 and 180). The uncommitted menu-hub work removed it (`proof/menu-hub/GATE_RESULTS.md`: "Removed screens: .quickPlay, .golfLesson").
- It is the only test file that fails to compile, at HEAD and in the working tree.

**What depends on it / what breaks:**
- Xcode builds a test target all at once, so this one file stops all 493 unit tests from building. That covers CI `build-and-test` and the `xcodebuild test` command in CONTRIBUTING. Recent sessions work around it with scratch xcodegen projects that leave the file out.
- No other file uses its classes.
- Its recorders write into tracked `ArtDir/ui/motion-club-radial-v1/runtime` (111 files) and untracked `work/golf-motion-fix/proof`.

**Verdict: SAFE+FIX.**
- Minimum fix: delete the `GolfReadyControllerTests` class (it tests a removed feature) and the two `.golfLesson` lines.
- Deleting the whole file is also safe. Whether to keep the 7 Motion-Club recorder tests is the owner's call.

**Confidence:** high.

### TEST-02 — 14 unit-test files test only the retired camera/body-tracking SceneKit golf game

**Paths:** `GolfArcadeTests/{AimSignalRecognizerTests, AvatarTests, BenchmarkTests, CameraSwingTests, CaptureEvidenceTests, ClubSignRecognizerTests, CourseTests, GolfArcadeTests, HandGestureRecognizerTests, MotionSwingDetectorTests, PresentationTests, RangeSimulationTests, SyntheticGolferTests, TerrainTests}.swift`. 14 files, 3,935 lines, 233 test methods, about 209 KB. All tracked; none touched since 2026-09-17/18.

**What it is:** tests for the first version of the app, where the phone's front camera watched your body swing a golf club. They cover:
- pose tracking and body calibration
- swing and putt detection
- hand-gesture and finger-count menus
- the 3D avatar
- Practice Lab benchmarks and recordings
- the SceneKit course: ball flight, slopes, cups, sounds

**Evidence:**
- The app starts at `SportsHome` (`App/GolfArcadeApp.swift`). The old flow's root, `ContentView`, is referenced only by its own `#Preview`.
- A type-reference graph from the entry point reaches 55 of the 114 app Swift files. These tests only use types from the other 59, such as:
  - `CameraSwingController` and `ArmSwingDetector` (Mock/)
  - `Course`, `CourseRound`, `CourseScene`
  - `PracticeLab` and `SyntheticGolfer` (Benchmark/)
  - `HandGestureRecognizer`, which only `Mock/CameraSwing.swift` uses
- Otherwise they only use plain data types (`PoseFrame`, `BodyJoint`, `Player`).
- Proof: the 55 live app files typecheck alone, and the remaining live tests typecheck without these 14 files.

**What depends on it / what breaks:**
- Nothing depends on these files; each one only declares its own XCTestCase classes. Deleting them on their own breaks nothing.
- If the old-flow app code is deleted (App/ContentView, Features/, Tracking/, Mock/, Game/, Avatar/, Benchmark/, Shot/, Swing/, Scene/, Domain/Club.swift, …), these files must go in the same change. Otherwise the test target stops compiling.
- Exception: two tests in `GolfArcadeTests.swift` exercise live code: `testCalibrationStoreRoundTripsAndClears` and `testRosterMigratesTheSinglePlayerScan`. They test `PlayerRosterStore`'s migration of an old single-player camera scan (`Domain/Player.swift:103` → `PlayerCalibrationStore`).

**Verdict: SAFE+FIX.** Delete together with the old-flow app code. If the `PlayerCalibrationStore` migration stays, move those 2 tests into `LockerTests.swift`, which already covers normal roster save/load.

**Confidence:** high.

### TEST-03 — UI tests for the retired flow: `CameraPlayTests.swift`, `CoursePlayTests.swift`, `UITestSupport.swift`

**Path:** `GolfArcadeUITests/`. 3 files, 510 lines, 14 tests, 36 KB. Tracked; untouched since 2026-09-17.

**What it is:** automated "tap through the app" tests for camera-mode golf, touch-pad course play, multiplayer turns and the Practice Lab.

**Evidence:**
- They launch with `-skipPlayerCalibration -startInCameraMode -startCourse -fixturePlayers -fixtureLockedCamera -fixtureSwingAfterLock -positionReviewSeconds -manualProgression -flightTimeScale -holdBanners -recordCameraTrace -range.swingInput -gestures.enabled`.
- Only `App/ContentView.swift` and old-flow files read those arguments: `Mock/CameraSwing.swift`, `Features/Course/CourseScreen.swift`, `Game/CourseRound.swift`, `Tracking/PoseDelivery.swift`.
- The running app (`SportsHome`) uses four of them only to *skip onboarding* (`Onboarding/OnboardingFlow.swift:29`), then shows the Island menus.
- The buttons they wait for (`menuSolo`, `menuPractice`, `continueToCourses`, `course-easy|medium|hard`, `swingPad`, `holeOverview`, `practiceLab`, `lab*`) exist only in Features/Menu, Features/Course and Features/Practice views, which only ContentView shows. So every test fails at its first wait.
- The helpers in `UITestSupport.swift` (`any`, `waitUntil`, `screenshot`) are used only by these two files.

**What depends on it:**
- The `ui-tests` matrix in `.github/workflows/ios.yml` runs exactly `[CameraPlayTests, CoursePlayTests]`.
- CONTRIBUTING.md documents them.
- So does the legacy launch-argument list in OnboardingFlow (TEST-25).

**Verdict: SAFE+FIX.** Delete the 3 files. In the same change, point the CI matrix at live suites (MenuNavigationRegressionTests, MenuBetaUITests, SportsNavigationTests) and update CONTRIBUTING.md.

**Confidence:** high.

### TEST-04 — `GolfArcadeUITests/OnboardingUITests.swift`: 5 of its 8 tests target removed setup screens

**Path:** 1 file, 116 lines. Tracked; last commit 2026-10-06.

**What it is:** UI tests of the first-run setup: account → character → TV connect → choose a game.

**Evidence:** the tests wait for buttons and text that no longer exist:
- `onboarding-phone`, `onboarding-coach-play` and `onboarding-tutorial-retry` were removed in c74e5927 (2026-10-06).
- `onboarding-primer-touch` and `onboarding-reward-continue` exist only in `Onboarding/OnboardingMotionPrimer.swift` and `OnboardingRewardScreen.swift`. `OnboardingRoot.swift` no longer shows either screen; the motion-primer, tutorial and reward steps all show the game picker.
- The text "Cliffside · tutorial + 1 round" is gone.

| Status | Tests |
|---|---|
| Stale | `testCharacterSavesInSixtySecondsAndKeepsNameAndBody`, `testNoDisplayContinuesToChoose`, `testGameCardsSelectBothTutorials`, `testEverySetupStepResumesAfterProcessDeath`, `testPartialPhoneWalkthroughStopsAtRealRuntimeFailure` |
| Still valid | `testFreshAccountAppearsBeforeAnySystemAlert`, `testDeclinedAndUnavailableContinueAsGuest`, `testExistingSaveSkipsAndReplayKeepsHero` |

**What depends on it:** nothing; CI does not run it.

**Verdict: SAFE+FIX.** Rewrite or delete the 5 stale tests.

**Confidence:** medium-high (not executed).

### TEST-05 — `GolfArcadeUITests/SportsNavigationTests.swift`: 3 of 5 tests target the developer-only "classic" menu and a removed button

**Path:** 1 file, 103 lines. Tracked; last commit 2026-10-08.

**Evidence:**
- `testNativeMenuHasNoScanGateAndExplainsMissingUnity` expects a "Sports Arcade" navigation bar at launch. That bar exists only in `ClassicSportsHome` (`SportsHome.swift:194`), which appears only after choosing the developer setting "Classic options menu". The test also expects a `startUnityPreview` button, removed in c74e5927. It fails.
- `testPhysicalExternalControllerGolfAndTennis` and `testPhysicalExternalTennisMap` run only on a device (they skip in the Simulator). They drive the same classic menu and `startUnityPreview`, so they are stale.
- `testGolfControllerOnlyAfterLoadingAndReady` and `testGolfPauseProvidesSettingsResumeAndLeaveWithoutCalibration` use `-golfControllerDesign`, which SportsHome handles, so they test live code. The second one's check that "golf-calibration-status" is absent is a harmless leftover.

**Verdict: SAFE+FIX.** Delete or rewrite the 3 classic-menu tests.

**Confidence:** medium.

### TEST-06 — Live UI tests: `MenuNavigationRegressionTests.swift` and `MenuBetaUITests.swift`

**Path:** 2 files, 136 lines, 4 tests. Tracked; MenuNavigationRegressionTests has uncommitted changes.

**Evidence:**
- Their launch arguments (`-skipOnboarding`, `-controllerPhaseCheck`, `-controllerMenuCheck`, `--lobby`) are handled in OnboardingFlow and SportsHome.
- Every ID they use exists. Some are built at runtime, for example `controller-emote-0` = `"\(identifier)-\(slot)"`, `remote-select`, `map-*`, `hub-*`.
- CI does not run them (see TEST-16).

**Verdict: KEEP.**

**Confidence:** medium (not executed).

### TEST-07 — The 21 live unit-test files

**Paths:** `AnalyticsTests, ClubEnvironmentTests, DressedHeroSnapshotTests, HubPartyTests, HubSessionTests, LockerMirrorProofTests, LockerMirrorTests, LockerTests, MatchHeroLockerTests, MenuBetaTests, MenuFlowRegressionTests, MultiplayerTests, NewMenuSnapshotTests, OnboardingTests, PointClipsTests, PostMatchSnapshotTests, SportsDelayProbeTests, SportsIntegrationTests, TennisCampaignTests, TennisMenuSnapshotTests, VisualOverhaulNativeTests`. 5,291 lines, 250 tests.
- 19 are tracked; 8 of those have uncommitted changes.
- `HubPartyTests.swift` (6 tests) and `HubSessionTests.swift` (10 tests) are untracked.

**What it is:** tests of what ships today:
- menus (Island screens, locker, campaign, onboarding logic, post-match)
- phone-controller and motion maths
- multiplayer and the new hub plaza
- the SceneKit lobby heroes compared with Unity
- analytics and point clips

**Evidence:** every app type they use is in the live set, and all 21 typecheck.

**Verdict: KEEP.** TEST-08 to TEST-11 cover their red tests and side effects. The two Hub test files must be committed together with `GolfArcade/Unity/HubSession.swift` and `HubControllerView.swift`.

**Confidence:** high.

### TEST-08 — Live tests that are known to be red because of stale expectations (not dead code)

**Menu suites:** 11 failures, the same before and after the hub work (`proof/menu-hub/phase6/menu_tests.txt`, 2026-10-10 run):
- MenuFlowRegressionTests: `testBothSportsChooseMapsBeforeStartingTheRuntime`. Line 82 expects 4 golf maps; there are now 3 courses.
- OnboardingTests (2): `testAimStepSurvivesRestartAndHubFinishesInChosenSport`, `testEverySavedStepResumesAndReplaySurvivesTutorialFlag`.
- TennisCampaignTests (4): `testEveryScreenIsReachableFromTheTitle`, `testGameSelectOffersTheTwoPlayableSports`, `testMainMenuReachesEverySection`, `testTheTutorialUnlocksEverythingElseInASport`.
- TennisMenuSnapshotTests (4): `testApprovedIslandScreens`, `testEmoteLoadoutScreens`, `testLoadingIdlesRespectReducedMotionAndSport`, `testSportLockerPreviewSwitchesOutfitEquipmentAndBack`.

**Other suites:**
- 14–16 failures in MatchHeroLockerTests and LockerMirrorTests, from stale cloth-shader and clip-name expectations (`work/lobby-parity/NOTE_lobby_heroes.md`; individual names were not logged).
- `MenuBetaTests.testFeedbackAndBundledGameplay` (baseline named by the coordinator).

**Side notes:**
- `TennisCampaignTests.testGameSelectOffersTheTwoPlayableSports` also has a check that does nothing: it loops over `sport.clips` to assert menu b-roll mp4s are bundled, but `Sport.clips` is always `[]`.
- The MultiplayerTests baseline failures named in the brief (`testLinkWindowGradesByRoundTripJitterAndLoss`, `testTheOneTVConfigurationFixtureParsesAsTheOwnerWritesIt`) **do not exist in this tree**. They exist only on `main`, `integrate/main-2026-10-10` and `local/2026-10-10-online-crash-fixes`.

**Verdict: SAFE+FIX.** Update the expectations; nothing here should be deleted.

**Confidence:** medium (taken from logs, not re-run).

### TEST-09 — Unit tests overwrite tracked ArtDir images on every run

All of these build their path from `#filePath`, so the Simulator run writes straight into the repo.

| Test | Writes to |
|---|---|
| `TennisMenuSnapshotTests.testEmoteLoadoutScreens` | `ArtDir/screenshots/emote_loadout/{locker_phone, locker_tv, controller_intro_layout, controller_rally_layout}.png` |
| `TennisMenuSnapshotTests.testApprovedIslandScreens` | `ArtDir/ui/toybox-simple-v6/runtime/*.png` (intro, home, sports, courts, tennis, campaign, quick-match, … loading, loading-swing, loading-repeat) |
| `TennisMenuSnapshotTests.testSportLockerPreviewSwitchesOutfitEquipmentAndBack` | `ArtDir/screenshots/golf_locker_swap/` |
| `TennisMenuSnapshotTests.testRecordTVMenuTour` | `ArtDir/review/tv-menus` (only with `RECORD_TV_MENU=1`, the one gated recorder) |
| `PostMatchSnapshotTests` | `ArtDir/review/post-match` (21 tracked files) |
| `ClubEnvironmentTests.testCaptureRealMenuEnvironments` / `testCaptureAlternateViewOfBakedGeometry` | `ArtDir/environments/motion-club-3d/review-live-images` (8 tracked) |
| `MotionClubReviewTests` | `ArtDir/ui/motion-club-radial-v1/runtime` (111 tracked) |

**Evidence:**
- The 20 modified PNGs in git status (4 in `emote_loadout`, 16 in `toybox-simple-v6/runtime`) are exactly the files those two TennisMenuSnapshotTests write.
- Both tests are on the known-failing list, but they write their images before they assert.
- I searched GolfArcadeTests, GolfArcadeUITests, Unity/Assets/{Tests,Editor,Scripts}, Tools, blender/scripts and docs and found no other writer. `Unity/Assets/Editor/HeroLockerExport.cs` writes a different folder, `toybox-simple-v6/source/preview-raw`.

**What depends on it:** reverting the 20 PNGs is safe; the next test run regenerates them. The tests are the only thing that produces these ArtDir folders.

**Verdict: SAFE+FIX.**
- Make these recorders opt-in with an environment variable (like `RECORD_TV_MENU`), or write to `$MENU_SNAP_DIR` / tmp.
- The owner decides whether to commit or revert the 20 modified PNGs.

**Confidence:** high.

### TEST-10 — Proof recorders inside the unit suite that write into untracked `work/`

| Test | Writes to |
|---|---|
| `MultiplayerTests.testGolfPartyPhoneScreens` | `work/golf-party/native-proof/phone-result.png` |
| `DressedHeroSnapshotTests` (2 record tests + proof stills) | `work/hero-dressed/proof/swift` |
| `LockerMirrorProofTests` (all 9 tests are recorders or measurements: films and stills, 407 lines) | `work/locker-mirror/proof` (or `$VISUAL_NATIVE_FILM_DIR`) and `work/online-lobby/emote_parity` |
| `LockerMirrorTests` | `work/locker-mirror/proof` and `work/online-lobby/emote_parity/rotation-report.txt` |
| `MatchHeroLockerTests` (`testRecordLockerMirrorProof`, `testRecordHostedLockerView`) | `work/hero-mainstay/proof/swift_locker` |
| `MotionClubReviewTests` | `work/golf-motion-fix/proof` |

These write only to a folder named by an environment variable, which is fine:
- NewMenuSnapshotTests, HubSessionTests and `TennisMenuSnapshotTests.saveHosted` use `$MENU_SNAP_DIR`, otherwise tmp.
- MenuBetaTests uses `$MENU_BETA_PROOF_PATH`, otherwise tmp.
- MenuBetaUITests writes only if `MENU_BETA_PROOF_PATH` is set.

**What it is:** these make review images and films; they are not regression checks. They slow the unit suite (SceneKit rendering, sleeps) and recreate folders in `work/`.

**Verdict: DECIDE.** Keep them as opt-in proof tools, or move them out of the unit suite.

**Confidence:** high.

### TEST-11 — Untracked files in `work/` that tests READ (they look like scratch but are in use)

- `work/locker-mirror/data/MatchHero_{Male,Female}_golden.json` (2.2 MB) is read by `LockerMirrorTests.testShippedRigReproducesUnitysPoses`.
- `work/online-lobby/emote_parity/data/MatchHero_{Male,Female}_emote_parity.json` (0.8 MB) is read by `LockerMirrorTests.testLobbyEmoteTracksMatchLiveUnityAtTenFrames`.
- Both apply unless `VISUAL_GOLDEN_DIR` is set. That folder also holds 8.6 MB of `MatchHero_*_golden.json` for `VISUAL_GOLDEN_DIR` runs.
- `VisualOverhaulNativeTests.testNativeHeroesAreExportedFromTheCurrentUnityHeads` hashes `Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/{Male,Female}.fbx` and skips if they are missing.

**What breaks:** a `work/` cleanup, a fresh clone, or CI makes those 2 LockerMirrorTests fail with an error. The data is not a duplicate of `GolfArcadeTests/VisualGolden` (1080 samples there vs 144 here).

**Verdict: KEEP** until the fixtures are committed next to VisualGolden or the tests are gated (optional fix).

**Confidence:** high.

### TEST-12 — `GolfArcadeTests/VisualGolden/` (reference data from Unity)

**Path:** 7 files: six `*_golden.lzfse` files of about 2.3–2.5 MB each (14 MB in total, stored in Git LFS) plus `provenance.json` (5.5 KB). Tracked; all 7 have uncommitted changes (repacked on 2026-10-10).

**What it is:** reference poses and vertices exported from Unity. They prove the phone's SceneKit heroes match the Unity heroes.

**Evidence:**
- They are copied into the test bundle (Resources phase in the pbxproj).
- `VisualOverhaulNativeTests.testShippedGolfAndTennisRigsMatchUnityGoldenVertices` loads all 6.
- `testNativeHeroesAreExportedFromTheCurrentUnityHeads` reads `provenance.json`.
- CI checks out without LFS, so in CI these are pointer files and the test would fail (TEST-16).

**Verdict: KEEP.**

**Confidence:** high.

### TEST-13 — The generated Xcode projects are stale compared with the working tree

**Paths:** `GolfArcade.xcodeproj/project.pbxproj` (254 KB) and `GolfArcadeUnity.xcodeproj/project.pbxproj` (258 KB). Tracked; both were last regenerated in 14a43331 and match the YAML at HEAD.

**Evidence:** I parsed both projects' file trees and compared them with disk.
- They have no references to missing files.
- 4 files on disk are in neither project:
  - `GolfArcade/Unity/HubSession.swift` and `HubControllerView.swift`. Both are untracked, and these modified files already need them: SportsHome, SportsSession, TennisMenu, IslandScreens, TennisPlayViews, SportsDisplays, ArcadeScreens.
  - `GolfArcadeTests/HubPartyTests.swift` and `HubSessionTests.swift`.
- The hub proof notes that its tests ran from a scratch xcodegen project.

**What breaks:** opening either committed project with the current tree fails to compile ("cannot find HubSession").

**Verdict: SAFE+FIX.** When the hub work is committed:
- re-run `xcodegen` (project.yml) and `xcodegen --spec project-unity.yml`
- add the 4 files to git

**Confidence:** high.

### TEST-14 — Which project builds what (`GolfArcade.xcodeproj`, `GolfArcadeUnity.xcodeproj`, `GolfArcadeUnity.xcworkspace`)

- **`project.yml` → `GolfArcade.xcodeproj`:** the native app *without* Unity. `SportsRuntime.mm` leaves Unity out via `__has_include(<UnityFramework/…>) && !TARGET_OS_SIMULATOR`. Used for the Simulator, every XCTest run and CI.
- **`project-unity.yml` → `GolfArcadeUnity.xcodeproj`:** includes project.yml and adds the UnityFramework embed and `Unity/Builds/iOS/Data`. You open it through `GolfArcadeUnity.xcworkspace`, together with `Unity/Builds/iOS/Unity-iPhone.xcodeproj` (a git-ignored local Unity build output, on disk since 2026-10-08). **This is the shipping device app.**
- The two schemes are identical apart from the container. They are not duplicates.
- Leftovers: `GolfArcadeUnity.xcworkspace/xcshareddata/swiftpm/configuration` is an empty folder (nothing tracked). The `xcuserdata` folders are git-ignored local state.

**Verdict: KEEP** both.

**Confidence:** high.

### TEST-15 — `project.yml` exclude entries and the tracked assets they keep out of the app

All 6 exclude patterns match real files; none points at nothing.

| Exclude | Files | What it is |
|---|---|---|
| `Unity/SportsInfo.plist` | 1 | The app's Info.plist. Correctly excluded from sources; KEEP. |
| `Unity/MenuVideo` | 22 tracked mp4/jpg, 39 MB | Menu b-roll videos |
| `Unity/CharacterAssets/Custom*`, `Higgs*`, `Hero*`, `Player*` | 9 + 6 + 20 + 28 = 63 tracked, 137 MB | Old character assets (HeroV4, HeroMenu, Higgs and Player bases, custom hair/bodies) |

**Evidence:**
- Excluded files never reach the app.
- `MatchHeroLockerTests.testBothMatchHeroesShipAndTheOldHeroDoesNot` asserts the old hero (HeroMenu, HeroV4, HeroLounge, HeroSwing_*, PlayerMale*) is *not* bundled.
- MenuVideo's only consumer, `LoopingVideo` (`Unity/HowToCards.swift`), is never used, and `Sport.clips` is `[]`.
- The Unity exporters `HeroLockerExport.cs` and `MatchHeroLockerExport.cs` still produce Hero*/Player* names (cross-area).

**What breaks:**
- Deleting the 85 files breaks no build or test; the test asserts they are absent.
- Removing only the exclude lines while keeping the files would bundle about 176 MB of old assets and fail that test.

**Verdict: DECIDE.** Likely SAFE: delete the files and their exclude lines together, and keep the SportsInfo.plist exclude. Cross-area with the app-assets auditor.

**Confidence:** medium-high.

### TEST-16 — `.github/workflows/ios.yml` (and `.github/actions/simulator/action.yml`)

**What it is:** CI that runs only on pushes and PRs to `main` and `testing`. It has three jobs:
- `main-source-policy`
- `build-and-test`: builds `GolfArcade.xcodeproj` with xcodebuild and runs `GolfArcadeTests`
- `ui-tests`: a matrix of UI-test suites

**Problems:**
1. `actions/checkout@v4` has no `lfs: true`, so LFS assets arrive as pointer files. The job has been red since 2026-10-09.
2. The unit-test target cannot compile (TEST-01).
3. LFS goldens and untracked `work/` fixtures are missing in CI (TEST-11, TEST-12).
4. The `ui-tests` matrix is `[CameraPlayTests, CoursePlayTests]`, which are only the dead suites (TEST-03). No live UI test runs anywhere.
5. The step message "CameraPoseTracker needs the iOS 26 SDK" is out of date: the app's only iOS 26 API use is in dead `Tracking/CameraPoseTracker.swift`.

Everything the workflow references exists: the GolfArcade.xcodeproj project, the GolfArcade scheme and `.github/actions/simulator`. No workflow in this tree builds Unity or runs Unity tests.

**Verdict: SAFE+FIX.**
- Add `lfs: true`.
- Fix TEST-01.
- Swap the UI matrix to live suites.
- Drop the iOS-26 justification once the old code is gone.

**Confidence:** high.

### TEST-17 — `.github/workflows/server.yml`

**What it is:** runs the server tests (Node 22 and Postgres 16) on PRs that touch `server/` and on pushes to main/testing.

**Evidence:** its paths are valid, including `server/package-lock.json`.

**Verdict: KEEP** while `server/` exists. Remove it together with the server if TEST-23 decides to drop it.

**Confidence:** high.

### TEST-18 — Workflows and tools that exist only on other branches

- `main` (and `origin/main`) also has:
  - `.github/workflows/netcode.yml`: runs `dotnet test Tools/netsim/NetSim.csproj`, the only CI that touches Unity C# (pure rules and EditMode tests, no Unity)
  - `static.yml`: deploys `site/` to GitHub Pages on pushes to `main` and `final-build-10/9`
  - `Tools/netsim` (5 files), `Tools/check-syntax.py` and `site/` (4 files)
- None of these exist in this `final-build` tree, which is 126 commits ahead of `main` and 50 behind. There is nothing to delete here. Whoever merges must not drop them.
- `.github/CODEOWNERS` (`* @aloniewski2`) is in use.

**Verdict: KEEP** (note only).

**Confidence:** high.

### TEST-19 — `Tools/PointClips/` (`InspectClip.swift`, `RecorderSmoke.mm`, `README.md`)

**Path:** 3 files, 88 lines, 12 KB. Tracked.

**What it is:** manual smoke tests for the live "save the last point as a video" recorder.

**Evidence:**
- They compile against `Unity/Assets/Plugins/iOS/SportsPointRecorder.{h,mm}`, which exist.
- The README points to the live `PointClipsTests`.
- No build or CI uses them.

**Verdict: KEEP** (developer tool).

**Confidence:** high.

### TEST-20 — `Tools/PostcardCheck/PostcardHoleTests.cs.pending` and `PostcardImportCheck.cs.pending`

**Path:** 2 files: 281 lines (14 KB) and 459 lines (30 KB). Tracked.

**What it is:** drafts kept outside Unity until `Course.Postcards()` existed. Their own headers say "copy into Unity/Assets once Course.Postcards() exists".

**Evidence:**
- `Course.Postcards()` now exists.
- Newer and larger copies are installed: `Unity/Assets/Tests/EditMode/PostcardHoleTests.cs` (358 lines) and `Unity/Assets/Editor/PostcardImportCheck.cs` (928 lines).
- Nothing references the `.pending` copies.

**Verdict: SAFE.**

**Confidence:** high.

### TEST-21 — `Tools/PostcardCheck/` harness

**Path:** `run.sh`, `src/{Common,Gates,NUnitShim,Planner,Program,Report,TestRunner}.cs`, `equiv.py`, `gates_diff.py`, `.gitignore`. 11 tracked files, about 150 KB.

**What it is:** a C# harness that runs without Unity. It compiles the real golf-hole code and checks hole-design gates and shot planning. The Python helpers cross-check the Blender-side copy of the rules.

**Evidence:**
- Every source it compiles exists: Course/Hole.cs, CourseShot.cs, Wind.cs, Scorecard.cs, Shot/BallFlight.cs, GolfClub.cs, Swing/MotionSwingDetector.cs and Net/ControllerProtocol.cs.
- So do `blender/scripts/postcard_check.py` and `hole0{8,9,10}_design.py`.
- `Unity/Assets/Tests/EditMode/PostcardHoleTests.cs` tells you to run it.
- The "Postcards" course it defaults to is no longer in the menu: `Course.All()` returns Cliffside, WildIsles and Magma, and `ByKey("postcards")` maps to cliffside. Holes 8/9/10 still play inside Cliffside, and editor tools and tests still use `Course.Postcards()`.
- No CI runs it.

**Verdict: DECIDE.** It still works; keep it as a developer tool unless golf-hole design work is finished.

**Confidence:** medium.

### TEST-22 — `Tools/PostcardCheck/.build/`

**Path:** 7.4 MB. Git-ignored and untracked.

**What it is:** the compiled harness plus a 7.5 MB `equiv_points_7.txt` dump. `run.sh` regenerates it.

**Verdict: SAFE.**

**Confidence:** high.

### TEST-23 — `server/` (Node backend)

**Path:** 18 tracked files, 104 KB:
- `src/{index,server,rooms,game}.js`
- `src/store/{memory,postgres}.js` and `schema.sql`
- 4 test files
- package.json and its lock file, Dockerfile, .dockerignore, .env.example, .gitignore, README.md

`node_modules` is not installed. README, `src/game.js` and 2 test files have uncommitted changes from today: `COURSE_HOLES` now follows the 3 golf courses.

**What it is:** a small backend for golf profiles, round history, a leaderboard and online golf "rooms". It serves HTTP and WebSocket on port 8080, with Postgres or in-memory storage.

**Evidence that the shipping app does not use it:**
- The native app contains no server URL, host or route. I searched `GolfArcade/` for `/v1/`, `:8080`, `BackendConfig` and `ws(s)://`.
- The app's online play is `MultiplayerService` over Game Center (`GameCenterTransport`, GKMatch) and the local network (`LocalMultiplayerTransport`).
- Only Unity's `BackendClient`, `OnlineSession` and `OnlineRoom`, plus Unity's own `UI/Lobby.cs`, use the server.
- In the embedded app, Unity's golf runs in `NativeControlled` mode. `ShowMenu()` calls `RequestNativeExit()`, and `OpenLobby()` requires `State.Menu`. So the Unity sign-up, online and leaderboard screens never appear.
- `BackendClient.SubmitRound` and `PushProfile` (`Game/GolfGame.cs:699` and `:2497`) do nothing unless the profile has a server token, and only that lobby creates one.
- `BackendConfig.DefaultServerUrl` is `http://Andrews-MacBook-Pro-8.local:8080`, a home Mac. There is no deployment configuration beyond the Dockerfile and README instructions.

**What breaks if it is removed:**
- `.github/workflows/server.yml` must be deleted with it.
- Unity's Online* scripts and the online pages of `Lobby.cs` would point at nothing, which is already true in practice.
- Unity EditMode `OnlineRoomTests`/`OnlineErrorTests` and PlayMode `ErrorHandlingTests` test only the client side and keep compiling.
- `server/README.md` goes with it.

**Verdict: DECIDE.** The server is dormant. Keep it only if internet play or leaderboards are planned.

**Confidence:** medium-high.

### TEST-24 — Unused pieces inside the server (matters only if the server is kept)

- `GET /v1/players/:id`: no client calls it; only `test/api.test.js:76` does.
- `GET /v1/me`: called only by `BackendClient.FetchProfile`, which nothing calls.
- WebSocket message `{"type":"ping"}` → `{"type":"pong"}` (`src/rooms.js:39`): the Unity client never sends it (it uses `ClientWebSocket.KeepAliveInterval`), and no test covers it.
- All 7 modules and `schema.sql` are imported, so there are no dead files.

**Verdict: SAFE** (tiny).

**Confidence:** high.

### TEST-25 — Launch-argument code kept only for the dead UI tests

**Evidence:**
- `Onboarding/OnboardingFlow.swift:29` treats `-skipPlayerCalibration`, `-startInCameraMode`, `-startCourse` and `-range.swingInput` as "legacy" arguments that skip onboarding. `OnboardingTests.testLegacyArgumentsSkipUnlessForced` (line 34) asserts that list.
- Those four arguments exist only for CameraPlayTests, CoursePlayTests and ContentView.
- The old flow also handles `-fixtureHumanSwing` (`Mock/CameraSwing.swift:186`), which no test uses any more.
- CONTRIBUTING mentions `-fixtureStanceYaw`, which nothing handles.

**Verdict: SAFE+FIX.** In the same change that deletes TEST-03, remove those 4 entries and trim the unit test. Keep `--lobby`, `--lobby-mock`, `--character-editor`, `-benchTennis` and `--resume-tennis-round-*`.

**Confidence:** medium-high.

### TEST-26 — Stale test and CI documentation

- **CONTRIBUTING.md:**
  - It says `ui-tests` runs CameraPlayTests and CoursePlayTests.
  - It documents the old launch arguments (`-skipPlayerCalibration`, `-startInCameraMode`, `-fixtureLockedCamera`, `-fixtureHumanSwing`, `-fixtureStanceYaw`, `-manualProgression`, …).
  - Its local `xcodebuild test … -only-testing:GolfArcadeTests` command cannot build (TEST-01).
- **README.md:**
  - "Getting started" says to run `GolfArcade.xcodeproj` on an iPhone, but that project has no Unity.
  - Most of the file describes the old camera golf game.
- **`ArtDir/ui/motion-club-radial-v1/IMPLEMENTATION.md`** cites MotionClubReviewTests.

**Verdict: SAFE+FIX** (documentation edits; cross-area with the docs auditor).

**Confidence:** high.

---

## Verdict counts

| Verdict | Count | IDs |
|---|---|---|
| SAFE | 3 | TEST-20, TEST-22, TEST-24 |
| SAFE+FIX | 11 | TEST-01, 02, 03, 04, 05, 08, 09, 13, 16, 25, 26 |
| DECIDE | 4 | TEST-10, 15, 21, 23 |
| KEEP | 8 | TEST-06, 07, 11, 12, 14, 17, 18, 19 |
| **Total** | **26** | |

## Links to other auditors' areas

**App code**
- `App/ContentView.swift` is referenced only by its own `#Preview`.
- The compile check shows the other 59 of 114 app Swift files are not needed by the live app; the list of 55 live files is in scratch `tc/livefiles.txt`.
- The 14 tests in TEST-02, the 3 UI files in TEST-03 and the OnboardingFlow legacy list in TEST-25 must be deleted together with that code.

**UI tests and ContentView:** CameraPlayTests and CoursePlayTests rely on launch arguments that only ContentView and old-flow files handle. Their button IDs exist only in Features/* views.

**Tests that write ArtDir** (each folder's only generator is a test):

| Test | Folder |
|---|---|
| TennisMenuSnapshotTests | `ArtDir/screenshots/emote_loadout`, `ArtDir/screenshots/golf_locker_swap`, `ArtDir/ui/toybox-simple-v6/runtime`, `ArtDir/review/tv-menus` |
| PostMatchSnapshotTests | `ArtDir/review/post-match` |
| ClubEnvironmentTests | `ArtDir/environments/motion-club-3d/review-live-images` |
| MotionClubReviewTests | `ArtDir/ui/motion-club-radial-v1/runtime` |

**Tests that use `work/`**
- READS (do not delete): `work/locker-mirror/data`, `work/online-lobby/emote_parity/data`.
- WRITES: `work/{golf-party/native-proof, hero-dressed/proof/swift, locker-mirror/proof, hero-mainstay/proof/swift_locker, golf-motion-fix/proof, online-lobby/emote_parity}`.

**Unity**
- `VisualOverhaulNativeTests` hashes `Unity/Assets/Resources/Tennis/OriginalSeamBlinkFinish/{Male,Female}.fbx`. Do not move them without updating `GolfArcadeTests/VisualGolden/provenance.json`.
- `server/` connects to these Unity files: `Unity/Assets/Scripts/Net/Online/{BackendClient,OnlineSession,OnlineRoom,OnlineProtocol}.cs`, `UI/Lobby.cs`, `Game/GolfGame.cs:699` and `:2497`, and the EditMode/PlayMode online tests.
- No workflow in this tree runs Unity. The coordinator notes 36 Unity test files write about 5.1 GB of captures into `Unity/Library/Captures`. On `main`, the only Unity-related CI is `netcode.yml`, which runs pure C# without Unity.

**App assets:** the project.yml excludes cover `GolfArcade/Unity/MenuVideo` and `CharacterAssets/{Custom,Higgs,Hero,Player}*`. `LoopingVideo` in HowToCards.swift is never used. `HeroLockerExport.cs` and `MatchHeroLockerExport.cs` still produce old Hero*/Player* outputs.

**Hub work:** `HubSession.swift`, `HubControllerView.swift` and the 2 Hub test files are untracked and missing from both pbxproj files. The hub cleanup's removal of `MenuScreen.golfLesson` adds 4 new compile errors to MotionClubReviewTests in the working tree.

**Docs:** CONTRIBUTING.md, README.md and `ArtDir/ui/motion-club-radial-v1/IMPLEMENTATION.md` (TEST-26).

**Branches:** `netcode.yml`, `static.yml`, `Tools/netsim`, `Tools/check-syntax.py` and `site/` exist only on `main`. The MultiplayerTests baseline-failure names in the brief exist only on `main` / `integrate/main-2026-10-10` / `local/2026-10-10-online-crash-fixes`.

## Scratch evidence

Under `scratchpad/audit/tests-ci-server/`:
- `tc/tests_tc.log`, `tc/tests_tc2.log`, `tc/head_tests.log`, `tc/live_app_tc.log`, `tc/live_tests_vs_livemod.log`: compiler output
- `reach.json` and `test_usage.txt`: reachability, and which app types each test uses
- `pbx_main.json` and `pbx_unity.json`: parsed project file lists
- `tools/`: the scripts used
