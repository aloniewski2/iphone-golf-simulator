# Repo cleanup audit: what's unused, and what removing it would affect

**Repo:** `iphone-golf-simulator` · **branch:** `final-build` (HEAD `14a43331`) · **date:** 2026-10-10 · **scope:** everything in the folder: the iPhone app, Unity, art, Blender, docs, tests, CI, the server, and the scratch folders. Uncommitted and untracked files are included.

**Nothing was deleted or changed.** This is a read-only audit. The only new thing in the repo is this report folder. The detailed evidence for every item (file paths, line numbers, how each was checked) is in `details/`, one file per area.

---

## The short version

| | What's unused | How much |
|---|---|---|
| **Size of the iPhone app** | Old characters, textures, models and menu art that ship but are never shown. **Biggest single item:** the old Hero01 tennis player, which still ships in full (87 MB) even though nothing loads it. | **~566 MB** clear-cut, **~750 MB** counting judgement calls, out of a ~2.3 GB device build. Another 100–250 MB is possible by slimming two large files the game does use. |
| **iPhone app code (Swift)** | The **original camera body-tracking golf game** is still in the app but can't be reached. The app now opens straight into the new menus. | **~14,300 lines, about half the Swift app** (61 whole files) |
| **Unity code** | Code for the retired heroes, the old Wi-Fi "phone as golf club" mode, and menus Unity used to draw itself | ~3,000 lines clear-cut, ~5,400 more your call. Plus ~2,500 lines of developer tools for retired heroes. |
| **Files tracked in git** | Mostly screenshots, films and Blender files of the retired Hero01 character (ArtDir), and the old "Sports Library" | **~14 GB** of the working tree (~12 GB ArtDir, 1.7 GB SportsLibrary, the rest app and Unity files). *Deleting does not shrink git history; see the cleanup order.* |
| **This Mac's disk** | Job folders from earlier AI sessions (`work/`, `proof/`): Unity project copies, Xcode build caches, renders, films | **~250 GB** safe to free now, plus ~6 GB of Unity caches. **Your disk is 99 % full (14 GB free).** |
| **Docs** | Finished plans and one-off AI prompts | **33 of the 42 root `.md` files**, plus 2 AI "skills" for a technology the project doesn't use |

**Also found, and probably more important than the clutter:**
- **Today's work is only on this Mac.** The Plaza hub, the golf three-courses work and the online crash fixes sit on local branches that were never pushed.
- **The safety net is off.** The unit tests can't compile, CI only tests the dead old game, and `final-build` gets no CI at all.
- **A few real bugs:**
  - Golf instant replay quietly stopped working on Oct 6.
  - The tennis announcer has no sounds.
  - The Plaza controller's Party button does nothing when you're not in a party.
  - A hero rebuild would bring back the old Serve and the old emotes.

See "Problems found along the way" below.

## How to read this

Every item has one of four verdicts:

| Verdict | Meaning |
|---|---|
| **Remove** | Nothing live uses it. Removing it changes nothing else. |
| **Remove + fix** | Removable, but something small points at it: a test, a config line, a tool or a doc link. That something has to change in the same edit, and the fix is named. |
| **Your call** | Nothing in the game needs it, but it's history, reference art, the source of something live, or a feature you might bring back. |
| **Keep** | Looks unused, but something live needs it. Listed so nobody deletes it by mistake. |

Before acting on it:
- **`final-build` and `main` have split.** About 126 commits exist only on `final-build` and about 50 only on `main`. Do the cleanup on whichever branch you're keeping, or every merge will conflict. A few things exist only on `main` (CI jobs, the website, an app icon fix). Don't lose them in the merge.
- **Other AI sessions are editing this folder right now.** One is holding the shared Unity/Xcode lock as this is written. Don't clean up underneath them.
- **Removing files doesn't make the git repo smaller.** Old versions stay in git history, in the local large-file cache (`.git/lfs`, 24 GB), and on GitHub until history is rewritten. That rewrite is a separate, risky step. It needs the repo owner (**aloniewski2**, who also pushes to this repo).

---

## Do these first, before deleting anything

1. **Push the three branches that exist only on this Mac:**
   - `local/2026-10-10-final-build-work`: today's Plaza hub, golf three courses + hole flow, lobby hero re-export.
   - `local/2026-10-10-online-crash-fixes`: the online tennis crash fixes.
   - `integrate/main-2026-10-10`: 17 commits.
   
   In the `final-build` checkout itself, the hub files (`Hub.unity`, `Scripts/Hub/*`, `HubSession.swift`, `HubControllerView.swift`, the hub tests, `PLAN_MenuHub_WalkableWorld.md`, the hub preview videos and the new flags) are still untracked. A `git clean` would erase them.
2. **Copy the few `work/` files that live tools and tests read into the repo.** They look like scratch, but:
   - The hero rebuild tools read `work/match-anim-set`, `serve-and-feet`, `emotes`, `cloth-overhaul/export`, `classic-tennis-racket` and `hero-mainstay/data/clip_table.json`.
   - `work/serve-dip-jump` and `work/emote-fluid` hold the only sources of the installed Serve and the approved emotes.
   - `LockerMirrorTests` reads `work/locker-mirror/data` and `work/online-lobby/emote_parity/data`.
   - Golf look tools read `work/postcard-look` baselines.
   - `work/character-shader/detail/*.py` is the only generator of the shipped HeroDetail textures.
   - `work/postcard-refresh/split-source/ReferenceHole09.blend` is the only source of a shipped golf hole.
3. **Repoint two hero-rebuild tools.** `ServeAndFeetWire.cs` and `EmoteWire.cs` copy *older* animation files than the ones installed, so a rebuild would silently bring back the old Serve and emotes.
4. **Free disk space.** The data disk is 99 % full; Unity imports and Xcode builds start failing when it fills. The safe `work/` deletions in section 10 free about 250 GB. **First run `hdiutil detach work/cloth-overhaul/toolchain/mounted`,** because that folder holds a mounted disk image. Leave `work/postcard-look/heavy.sh` and `heavy.lock` alone.

---

## Biggest wins, ranked

| # | What | Where | Win | Watch out for |
|---|---|---|---|---|
| 1 | Delete the safe job folders and the big throwaway parts of the others; remove 2 unused extra git checkouts; delete Blender backups | `work/`, `proof/` (section 10) | **~250 GB disk** | Detach the disk image first; keep the folders listed under "Keep" |
| 2 | Take the old Hero01 player out of Unity's "always ship" folder | `Unity/Assets/Resources/Tennis/Hero/` (section 5) | **87 MB** off the app | 3 old developer tools/tests need a path change, or retire them |
| 3 | Delete the rest of Unity's never-loaded "always ship" files | `Unity/Assets/Resources/` (section 5) | **~290 MB** off the app (+130 MB with judgement calls) | A handful of small fixes, all named |
| 4 | Stop shipping the iPhone app's unused files: old golf hero copy, three 3D menu rooms nobody sees, unused swing frames, old portraits | `GolfArcade/Unity/` (section 3) | **~190 MB** off the app (+56 MB judgement calls) | Re-run `xcodegen` for both projects |
| 5 | Delete the old camera golf game and its tests | `GolfArcade/` + tests (sections 1–2) | **~14,000 lines** (half the Swift app) | Delete as one batch, with its 14 test files and 2 UI tests |
| 6 | Delete Hero01-era art and review captures | `ArtDir/` (section 7) | **~12 GB** of the repo | First stop the tests that write into ArtDir, and fix `NativeLaunchHeroTests` |
| 7 | Delete the Sports Library's dead weight | `SportsLibrary/` (section 8) | **~1.7 GB** of the repo | Keep the 4–5 source files named in section 8 |
| 8 | Remove 7 unused Unity add-on packages, especially analytics | `Unity/Packages/manifest.json` (section 6) | 15 libraries, **8 launch-time routines** and 3 plugins out of the app | Re-check the privacy manifest after the next export |
| 9 | Delete the old hero code, 22 old developer tools and the old hero asset folders, as one set | Unity (sections 4 and 6) | ~5,000 lines + ~155 MB | Do them together; they reference each other |
| 10 | Fix `AGENTS.md`; delete 33 finished root plans and the vgpu skills | repo root (section 9) | Every AI session gets correct instructions | Remove the "Active handoff" section first |
| 11 | *Optional:* slim the two biggest live files: the baked polo meshes (282 MB) and the hidden stand-in's uncompressed animations (146 MB) | `Resources/Tennis/KitsTailored`, `Resources/StandardCharacters` | 100–250 MB off the app | Needs gameplay and look re-testing |

---

## Problems found along the way (not clutter, but worth fixing)

| # | Problem | Where | Section |
|---|---|---|---|
| 1 | **The Mac's disk is 99 % full** (14 GB free) | — | 10 |
| 2 | **Today's work is only on local branches**, and untracked in the `final-build` checkout. Both Xcode projects lack the new hub files, so the committed projects don't compile with today's files. | git, `*.xcodeproj` | 2, 10 |
| 3 | **The unit tests can't compile.** One stale file (`MotionClubReviewTests.swift`) blocks all 493 tests. | `GolfArcadeTests/` | 2 |
| 4 | **CI's on-screen tests only test the dead old game**, iOS CI has been red since Oct 9 (missing `lfs: true`), and `final-build` gets no CI at all | `.github/workflows/ios.yml` | 2 |
| 5 | **Golf instant replay can never start.** The only call was removed on Oct 6, and `ReplayTests` should now fail. | `Unity/Assets/Scripts/Game/GolfGame.cs` | 4 |
| 6 | **The tennis announcer has no sounds.** The code loads `Resources/Tennis/Announcer`, which doesn't exist. | `TennisAnnouncer.cs:52` | 5 |
| 7 | **The Plaza controller's Party button does nothing** when you're not in a party. It taps a `"homePlay"` button that no screen has. | `HubControllerView.swift:39` | 1 |
| 8 | **A hero rebuild would revert the installed Serve and the approved emotes**, because two tools copy older files | `ServeAndFeetWire.cs`, `EmoteWire.cs` | 4, 10 |
| 9 | **The golf scene broadcasts a Wi-Fi signal every half second on the phone**, for a "phone as Mac golf club" mode that no longer exists | `NetworkMotionSource.cs`, `SwingController.cs` | 4 |
| 10 | **The invisible tennis stand-in gets its outfit recoloured on every launch**, which is GPU work for nothing | `TennisActor.WearKit`, `TennisLook.RecolorKit` | 4, 5 |
| 11 | **Game Center sign-in isn't connected to the build** (since Oct 6), and `final-build` has **no app icon and no privacy manifest**. `main` has the fix in commit `862bca5f`. | `project.yml`, `GolfArcade.entitlements` | 3 |
| 12 | **Tests overwrite tracked ArtDir pictures on every run.** That's why 20 files always show as modified, and history is growing (some folders are already 3× larger in history than on disk). | `TennisMenuSnapshotTests` and 4 others | 2, 7 |
| 13 | **`AGENTS.md`, which every AI session loads, is out of date.** It points agents at finished jobs, says clothes are out of scope (they shipped Oct 8) and calls the game tennis-only. | `AGENTS.md` | 9 |
| 14 | **Two committed shortcuts point into `work/` by absolute path.** They pull draft tools into this Mac's Unity editor and point at nothing on any other computer. | `Unity/Assets/Editor/PostcardV3Scratch` | 4 |
| 15 | **A test relies on data that isn't in git**, so it can never pass on CI | `LockerMirrorTests` ← `work/locker-mirror/data` | 2, 10 |
| 16 | **Nothing can regenerate the compressed outfit textures (`.ktx`).** They were converted by hand, so a re-export can leave stale outfit textures in the app. | `GolfArcade/Unity/CharacterAssets/*.ktx` | 3 |
| 17 | **The Unity-to-Xcode export is stale** (Oct 8, before the Hub). The device app needs a fresh export to include the Plaza. | `Unity/Builds/iOS` | 6 |
| 18 | **Large files stored the wrong way in git:** 541 MB of big Unity files, plus the IconKit and MenuVideo folders, sit as normal git files instead of large-file storage. About 8 GB of `.git` is an abandoned Sept 28 commit of raw videos. | git | 5, 6, 10 |
| 19 | **One developer probe is already broken**: `ClothCompileProbe` reads a file that doesn't exist. | `Unity/Assets/Editor` | 4 |

---

# Everything, area by area

## 1. iPhone app code (Swift)

*114 Swift files, 28,877 lines, under `GolfArcade/`. Checked with the Swift compiler's own reference data, not just text search. Detail: `details/swift-app.md`.*

**In short:** about **half the app's Swift code never runs: 61 whole files plus pieces of live files, about 14,300 lines.** Nearly all of it is the **original game**, the camera body-tracking golf game (body scan, Practice Lab, the 3-hole Easy/Medium/Hard courses). The app now opens straight into the new menus (`SportsHome`). The old game's front door, `ContentView`, is only ever created by Xcode's design-time preview, so no player can reach it. It loads no pictures or files, so deleting it strands nothing. **The old files depend on each other, so delete them as one batch, together with their tests** (section 2). Deleting only some of them breaks the build.

### The old camera golf game: remove + fix (about 12,960 lines)

| Item | What it is | Lines | What has to change at the same time |
|---|---|---|---|
| `App/ContentView.swift` | The old game's screen switcher | 104 | Delete the UI tests `CameraPlayTests`, `CoursePlayTests` and `UITestSupport`. They drive this screen and already can't pass. Fix CI's UI-test list (section 2). |
| `Features/**` (16 files) | Old screens: main menu, player setup, body scan, course select, course play, Practice Lab | 3,357 | Old-game tests |
| `Game/**` (6) | The old game's rules: courses, rounds, shots, lies. **Not** the live golf, which runs in Unity. | 1,368 | Old-game tests |
| `Mock/**` (6) | Despite the name, the old game's engine: camera swing detector, ball physics, 3D course renderer, phone-motion detector, sounds | 3,433 | Old-game tests |
| `Avatar/**` (7) | The 3D golfer that copied your body from the camera. Unrelated to today's heroes. | 1,192 | Old-game tests |
| `Benchmark/**` (4) | Practice Lab recorder, display-latency probe, fake test golfer | 888 | Old-game tests; `docs/PRACTICE_LAB.md` |
| `Swing/`, `Shot/`, `Scene/`, `Feedback/` (6) | Old swing phases, shot engine, shot camera, feedback sounds | 876 | Old-game tests |
| `Tracking/**` except `PlayerCalibration.swift` (9) | Camera body tracker, hand-gesture menu control, camera settings, video recorder | 1,472 | Old-game tests. (The live app's camera steering is separate code.) |
| `Domain/Club.swift`, `Domain/PoseFrame.swift`, most of `Domain/SwingMetrics.swift` | Old shared data types. **Keep `Handedness`, which is live.** | ~300 | |
| The body-scan logic in `Tracking/PlayerCalibration.swift` | Measures body proportions from camera frames | ~240 | Must go together with `PoseFrame`. |

### Dead pieces inside the live app

**Safe to remove** (no test touches them):

| Item | What it is | Lines |
|---|---|---|
| `Onboarding/GolfTutorialController.swift`, `OnboardingMotionPrimer.swift`, `OnboardingRewardScreen.swift` | Setup screens that are never shown | 50 |
| `Unity/PlayfulGraphics.swift` | An unused colour and font palette | 19 |
| `Unity/TennisMenuViews.swift`: `Plaque`, `MenuBackdrop`, `HeroArt`, `Stars`, `OpponentCard` | Old arcade-style menu parts, including the only code that showed the painted portraits (section 3) | ~140 |
| `Unity/ArcadeScreens.swift`: `PresentationPortraits`, `PresentationRosterCard`, `LoadingScreen.roster` | A loading-screen "you vs rival" block that is never displayed | ~85 |
| `Unity/ClubDesign.swift` + `ClubScreens.swift`: `ClubCard`, `ClubPartyScreen`, `ClubEntrance` and unused styles | Unused menu parts | ~95 |
| About 30 small unused members across live files, e.g. `CharacterOptions.hair/faces/heights`, `Sport.clips`, `LoadingModel.practice()`, `MultiplayerService.clearError`, `SportsRuntime -loadInWindow:error:` | Leftovers | ~130 |

**Remove + fix** (a test or another file must change in the same edit):

| Item | What it is | Lines | Fix |
|---|---|---|---|
| Tutorial/reward leftovers in `OnboardingFlow`, `OnboardingStore`, `Progression`, `TennisMenu`, `TennisStory` | The removed first-time tutorial and its reward | ~115 | Trim `OnboardingTests`, `PostMatchSnapshotTests`, `MenuFlowRegressionTests`, `TennisMenuSnapshotTests`. |
| Retired tennis "aim calibration lesson" (`TennisAimLesson` and its state in `SportsSession`, `SportsMotion`, `TennisPlayViews`) | Can never start: its state is never set | ~135 | Trim `SportsIntegrationTests`. |
| `SettingsScreen` view + `PartyLoadingTips` | A settings view that is never shown, and loading tips nobody sees | ~72 | Move the `describe` helper (which is live) into `TennisMenu` first. |
| `MenuScreen.locked` / `ClubLockedScreen` | The "coming soon" sport screen. It **can't be reached**, and the hub's `menu_audit.py` doesn't notice. | ~55 | Remove the case from 4 switch statements. |
| `LoopingVideo`, `HowTo.tips`, golf-lesson branches in `HowToCards`/`ClubScreens` | Old menu video player and the removed golf lesson | ~70 | Keep `LoopingPlayer`, which is live. Trim `TennisMenuSnapshotTests`. |
| Dead `TennisMenu` members and menu ids (`confirmCampaignRound`, `continueJourney`, `setBodySize`, `"homePlay"`, `"rival<N>"`, labels for removed buttons, the ignored `onPhone:` parameter) | Leftovers from removed screens | ~75 | Trim `TennisCampaignTests` / `TennisMenuSnapshotTests`. |
| Retired tennis "ultimates" (`TennisUltimate`, `chooseUltimate`, `ultimateMeter`, …) | The removed ultimates feature | ~22 | Trim `TennisMenuSnapshotTests`. |

### Your call

| Item | Lines | The question |
|---|---|---|
| Onboarding (first-run setup: account → character → connect TV → pick game) | ~386 | Only appears with a developer launch flag (`-forceOnboarding`); the shipped app skips it. Keep it if onboarding comes back. |
| Developer-only "classic options menu", `PlayabilityCheck`, `OnlineLobbyProofDriver`, some developer launch flags | ~330 | Developer tools. Keep `-benchTennis`/`-benchGolf`, which docs and Unity use. |
| `Player.calibration` (a "body scan" field in saved player profiles) | ~75 | Only save-file plumbing; nothing creates scans any more. Removing it touches the save format for little gain. |

### Looks unused, but keep

- Test hooks the unit tests drive: `TennisMenu.debugShow/debugStory/debugPostMatch`, `HubSession.debug*`, `MultiplayerService.mockState/connect(using:)`, and others.
- Things the system calls without our code naming them: `SportsAppDelegate`, `SportsExternalScene` (named in the app's plist), saved/loaded data types, display-link callbacks.
- `Handedness`, `PlayerRosterStore`, `LoopingPlayer`, `AxisGatePanel`, `NativeGolfEquipment`, `SettingsScreen.describe`, and every `SportsRuntime` bridge method except `-loadInWindow:error:`.

### A small bug found on the way

`GolfArcade/Unity/HubControllerView.swift:39` calls `tap("homePlay")`, but no screen has a `"homePlay"` button. So the Plaza controller's **Party button silently does nothing** when you're not in a party. It probably should be `tap("play")`.

## 2. Automated tests, CI, Xcode projects and the server

*`GolfArcadeTests/`, `GolfArcadeUITests/`, `project.yml`/`project-unity.yml` and the generated Xcode projects, `.github/workflows/`, `Tools/PointClips`, `Tools/PostcardCheck`, `server/`. Compiles were checked with `swiftc` in a scratch folder, without touching the repo. Detail: `details/tests-ci-server.md`.*

**In short:** the safety net is mostly switched off.
- **The unit tests can't build.** One stale file stops all 493 of them from compiling.
- **CI's on-screen tests only test the dead old golf game**, so no live UI test runs anywhere.
- **`final-build` gets no CI at all**: the workflows only run for `main` and `testing`.
- **14 unit-test files (233 tests) test only the dead old game.**

### Remove + fix

| Item | What it is | Size | What to do |
|---|---|---|---|
| `GolfArcadeTests/MotionClubReviewTests.swift` | Tests a golf "three practice swings" setup that was removed, plus a menu transition | 246 lines | **It blocks every unit test from compiling.** At minimum delete its `GolfReadyControllerTests` class and the 2 `.golfLesson` lines; deleting the whole file is also safe. It also writes 111 images into ArtDir. |
| 14 old-game unit-test files: `AimSignalRecognizerTests`, `AvatarTests`, `BenchmarkTests`, `CameraSwingTests`, `CaptureEvidenceTests`, `ClubSignRecognizerTests`, `CourseTests`, `GolfArcadeTests`, `HandGestureRecognizerTests`, `MotionSwingDetectorTests`, `PresentationTests`, `RangeSimulationTests`, `SyntheticGolferTests`, `TerrainTests` | Tests for the dead camera golf game (section 1) | 3,935 lines, 233 tests | Delete together with that code. **First move 2 tests** from `GolfArcadeTests.swift` (the saved-player migration checks) into `LockerTests.swift`. |
| `GolfArcadeUITests/CameraPlayTests.swift`, `CoursePlayTests.swift`, `UITestSupport.swift` | On-screen tests for the dead game; every one fails at its first step | 510 lines | Delete. In the same change, point CI's UI-test list at live suites (`MenuNavigationRegressionTests`, `MenuBetaUITests`, `SportsNavigationTests`), update `CONTRIBUTING.md`, and drop the 4 "legacy" launch flags in `Onboarding/OnboardingFlow.swift:29`. |
| 5 of 8 tests in `OnboardingUITests.swift`; 3 of 5 in `SportsNavigationTests.swift` | Wait for setup screens and buttons that were removed, or for the developer-only classic menu | — | Rewrite or delete those tests. |
| `.github/workflows/ios.yml` | The iOS CI job | — | Add `lfs: true` (it has been red since Oct 9 because the images arrive as placeholders), fix the compile, swap the UI list to live suites, and drop the stale "iOS 26 SDK" note, which refers to dead code. |
| Live tests that are red because their expectations are stale (11 menu tests; 14–16 locker tests; `MenuBetaTests.testFeedbackAndBundledGameplay`) | Real tests of live code, e.g. one still expects 4 golf maps instead of 3 courses | — | **Update, don't delete.** |
| Tests that overwrite tracked ArtDir images on every run: `TennisMenuSnapshotTests`, `PostMatchSnapshotTests`, `ClubEnvironmentTests`, `MotionClubReviewTests` | Snapshot recorders | — | This is why 20 ArtDir pictures always show as modified, and git history keeps growing. Make them opt-in, or write to a temp folder. |
| Both generated Xcode projects (`GolfArcade.xcodeproj`, `GolfArcadeUnity.xcodeproj`) | Generated from `project.yml` / `project-unity.yml` | — | **Stale:** they don't include today's `HubSession.swift`, `HubControllerView.swift` or the 2 hub test files, so the committed projects don't compile with today's files. Re-run `xcodegen` for both specs when the hub work is committed. |

### Safe to remove

| Item | What it is |
|---|---|
| `Tools/PostcardCheck/PostcardHoleTests.cs.pending`, `PostcardImportCheck.cs.pending` | Drafts that were later installed into Unity as larger, newer copies |
| `Tools/PostcardCheck/.build/` (7 MB, ignored) | Rebuilt by `run.sh` |
| Two unused server routes (`GET /v1/players/:id`, the WebSocket ping) | Tiny; only matters if the server stays |

### Your call

| Item | The question |
|---|---|
| **`server/`** (Node backend: profiles, round history, leaderboard, online golf rooms) and `.github/workflows/server.yml` | **The iPhone app doesn't use it.** The app's multiplayer is Game Center plus the local network. Only Unity's own stand-alone lobby talks to the server, and that lobby never appears in the app. Its default address is a home Mac, and nothing deploys it. Keep it only if internet play or leaderboards are planned. If removed, Unity's `Net/Online/*` scripts and `UI/Lobby.cs` become dead code too (section 4). |
| `project.yml` exclude lines + the 85 old files they keep out of the app (176 MB) | Delete the files and the exclude lines together (section 3). Keep the `SportsInfo.plist` exclude. |
| Proof recorders inside the unit tests that write into `work/` (`LockerMirrorProofTests`, `DressedHeroSnapshotTests`, some `MatchHeroLockerTests`) | They make review images rather than check anything, and they slow the suite. Keep them as opt-in tools or move them out. |
| `Tools/PostcardCheck/` harness | Still works: it checks golf-hole design rules without Unity. Keep it while hole design continues. |

### Keep

- The 21 live unit-test files (250 tests) and the live UI tests (`MenuNavigationRegressionTests`, `MenuBetaUITests`). **`HubPartyTests.swift` and `HubSessionTests.swift` aren't in git yet.**
- `GolfArcadeTests/VisualGolden/`: Unity reference data that proves the phone's heroes match the game.
- **Test data that sits in `work/`:** `work/locker-mirror/data` and `work/online-lobby/emote_parity/data`. `LockerMirrorTests` reads them. They aren't in git, so that test can never pass on CI; move them into `GolfArcadeTests/`.
- **Both Xcode projects.** `GolfArcade.xcodeproj` is the version without Unity (simulator, tests, CI). `GolfArcadeUnity.xcodeproj`, opened through `GolfArcadeUnity.xcworkspace`, is **the real device app**. They aren't duplicates.
- `.github/workflows/server.yml` (while `server/` exists), `.github/CODEOWNERS`, `Tools/PointClips/` (a smoke test for the live "save last point" recorder).
- **Only on `main`, not on this branch:** `netcode.yml`, `static.yml` (the website), `Tools/netsim`, `site/`. Whoever merges must not drop them.

## 3. Files packed into the iPhone app itself

*Pictures, 3D hero copies, videos, fonts and sounds under `GolfArcade/` (not the Unity part). Detail: `details/app-resources.md`.*

**In short:** the app packs about **601 MB** of its own files. About **244 MB** of that is never shown or used (about **255 MB** in the real phone build). Another **184 MB** sits in the folder but is already left out of the app, so it only weighs down the repo.

> **Before deleting anything here:** both Xcode projects list every packed file by name. After removing one, run `xcodegen` for `project.yml` **and** `project-unity.yml`, or the build fails with "file not found". Files that are already excluded (below) need no project change.

### Safe to remove

| Item | What it is | Size | Notes |
|---|---|---|---|
| `CharacterAssets/GolfHero_*` (30 files) | First phone-menu copy of the golf heroes (Oct 6), replaced the same week by `GolfKitHero_*` | 48 MB | Nothing loads it and nothing creates it any more. Ships in the app today. |
| `CharacterAssets/MatchHero_Golf_*.ktx` (29) | Outfit textures for that old golf hero | 11 MB | Only the dead `GolfHero_*` files point at them. |
| `CharacterAssets/MatchHero_GolfKit_*_Head_*.ktx` (4) | Golf cap textures for a body part the current golf heroes don't have | 0.4 MB | |
| `CharacterAssets/MatchHero_Skin_Soft_N.png` | Old skin texture | 40 KB | |
| `CharacterAssets/NativeGolfEquipment_{Driver,Putter,Wedge}.json` | Golf club models for the phone menus | 0.7 MB | Only the Iron is ever used. |
| Old hero families already excluded from the app: `HeroV4*`, `HeroMenu*`, `HeroSwing_*`, `HeroLounge`, `Higgs*`, `Player*`, `Custom*` (63 files) | Three older character generations | 143 MB | Already left out by `project.yml`. Keep the `Hero*` exclude line as long as the old Unity exporter `HeroLockerExport.cs` exists, because it can still write `HeroV4` files here. |
| `MenuVideo/*` (22 files) | Sept 24 menu video loops (bowling, boxing, football, golf, tennis) | 41 MB | Already excluded from the app. The video player that used them is never created. Stored as normal git files, not large-file storage. |
| `MenuArt/menu-hero-*` + `menu-trophy` (14) | Painted 2D portraits of rivals, the coach and the player | 6.9 MB (9.3 MB in the build) | **Not on the campaign screen any more**: it shows 3D heroes now. The code that loaded them is dead. |
| `MenuArt/island-intro.png`, `island-terrace.png` | Painted island scenes | 4.7 MB (8.2 MB in the build) | |
| `MenuArt/club-locker.png` | Still image of the locker room | 2.2 MB (4.3 MB in the build) | The locker room is drawn in 3D now. |
| `MenuArt/club-{crown,dial,kit,lesson,play,quick,tennis,training,trophy,whistle}.png`, `scene-{trophy,stands,office,locker}.jpg` (14) | Icons and scenes from the Sept 28 "Island Sports Club" menu | 3.7 MB | Unity keeps its own identical copies in `Resources/Club` (section 5). |
| `MenuArt/scene-home.jpg`, `club-friends.png`, `club-lock.png` | Art for screens nothing opens | 0.7 MB | |
| `MenuArt/menu-backdrop.jpg`, `map-golf-postcards.jpg` | Old menu backdrop; card for the retired "Postcards" course | 0.4 MB | |

### Safe after a small fix

| Item | What it is | Size | The fix |
|---|---|---|---|
| `ClubEnvironments/Club_{entrance,loading,terrace}.*` (6) | Three 3D menu rooms that are never shown; only the locker room is 3D | 61.5 MB | `ClubEnvironmentTests` loops over all four rooms, so limit it to the locker. Also stop `build_club_environments.py` writing the other three rooms. |
| `CharacterAssets/MatchHero_*_Distance_Swing_*.lzfse` (24) | Swing animation frames for the far-away lobby heroes | 38 MB | Nothing ever reads them. Re-exported today and not yet committed. Change `MatchHeroLockerExport.cs:336` or the next export writes them back. |
| 51 `.png` outfit textures (`MatchHero_Kit_*_{M,N}.png`, `MatchHero_GolfKit_*_{M,N}.png`, fabric atlases, `Cloth_Weave`) | Uncompressed copies of outfit textures; the app reads the compressed `.ktx` twins | 8.6 MB | **Don't delete.** They are the source for the `.ktx` files, and the exporter recreates them anyway. Add exclude patterns to `project.yml` so they stop shipping. |
| `CharacterAssets/MatchHero_Kit_*_{Top,Bottom,Shoe,Sock}.png` (12, no suffix) | Old "seam darkening" textures from the first outfit shader | 1.2 MB | Retire `LockerMirrorTests.testSeamMapOrientation…`, which still opens 3 of them. |

### Your call

| Item | What it is | Size | The question |
|---|---|---|---|
| `CharacterAssets/MatchHero_*_Swing_*.lzfse` (24) | Swing frames for the loading screen's "practice swing" hero | 48 MB | That feature was unplugged on Oct 8; only tests use the frames now. Bring the practice swing back, or delete the frames together with its code and tests. Re-exported today, not committed. |
| 6 polo/skirt `.ktx` textures (`MatchHero_Kit_{Male,Female}_Top_*`, `Female_Bottom_*`) | Outfit textures that are switched off while the clothing pass settles | 6.3 MB | **If deleted and later switched back on, the app crashes on purpose** (`fatalError`). Keep until the clothes are final. |
| "Coming soon" sport art (`scene-pavilion`, `club-crest`, `club-{bowling,football,boxing}`) + `Fonts/Bricolage.ttf` + its licence | Art for a locked-sport screen | 1.7 MB | That screen can't be opened today. Keep it if more sports are planned. |

### Looks unused, but keep

| Item | Why |
|---|---|
| Current hero data: `MatchHero_*`, `MatchHero_*_Distance*`, `GolfKitHero_*`, 44 `.ktx`, 3 `MatchHero_Original*` PNGs, the Iron club (322 MB) | Drawn in the locker, menus, portraits, onboarding and online lobby. The far-away "distance" heroes cost **121 MB** for the online lobby alone, so a cheaper version could be worth considering. |
| `ClubEnvironments/Club_locker.*` | The 3D locker room. |
| `Multiplayer/GolfArcade.entitlements` | The Game Center permission file. **It hasn't been connected to the build since Oct 6** (see "Problems found along the way"). |
| `Unity/SportsInfo.plist` | The app's settings file. It is left out of the copied files on purpose. |
| 16 live menu images and videos, `Fonts/Rubik.ttf`, 3 menu sounds | On screen today. |

## 4. Unity game code, developer tools and Unity tests

*`Unity/Assets/Scripts` (254 game files, 56,000 lines), `Unity/Assets/Editor` (138 developer-tool files, 22,364 lines), `Unity/Assets/Tests` (111 files, 14,920 lines), the iOS bridge, the shaders and `Unity/Tools`. Detail: `details/unity-code.md`.*

**In short:** about **18 % of the game code (~10,000 lines)** isn't on any path the shipped game takes. Most of it falls into two groups. The first is code for the old heroes (Hero01 to V6). The second is the **menus Unity used to draw itself** before the native iPhone menus existed: golf home screen, locker, online lobby, championships, AirPlay screen. Developer tools ("editor scripts") never ship in the app, so removing them only tidies the project; **about 2,500 lines of them are for retired heroes.** A few dead bits still do work on the phone, listed first.

### Dead code that still does something on the phone

| What | Effect today | Verdict |
|---|---|---|
| **Old "phone as a Mac golf club" Wi-Fi mode** (`Game/AppLauncher.cs`, `Net/PhoneController.cs`; the listener in `Net/NetworkMotionSource.cs`, `ControllerProtocol.cs`, `SwingController`, `TennisPhoneInput`) | While the golf scene is loaded, which happens at Unity start-up, the app **opens a network port and sends a broadcast signal every half second** for a client that no longer exists. Real controller input arrives another way. | Remove `AppLauncher` + `PhoneController` (nothing uses them). Remove + fix the listener; the minimum fix is to turn it off when the native app is in charge. |
| **Unity's own golf menus** (see "Your call" below) | Golf is Unity's first scene, so these menus probably get **built once at every launch, hidden behind the native menus**. Wasted start-up work. | At minimum, skip building them on the phone. |
| **Outfit recolouring for the hidden tennis "stand-in" body** (`TennisCustomization.Apply`, `TennisActor.WearKit`, `TennisLook.RecolorKit`, `TennisGame.ApplyOutfit`) | Recolours, on every launch, the outfit of an invisible body that only drives movement; the visible hero gets its look elsewhere. Also loads textures for it (section 5). | Remove + fix: drop the 2 calls in `NativeSportsSession.Load` and the old test. |
| **Old hero code compiled into the game** (`ModularHeroLook`, `HeroCosmetics`, `HeroFace`, `HeroHeadOccluder`, `HeroBodyProxy`, `HeroV6*`, `V4GameplayArmRacket`, `HeroLookLighting`, plus the old-hero branch inside `HeroTennisDriver`) | All 4 hero prefabs that ship use the new heroes, so this code never runs. It ships because the old Hero01 prefab sits in the "always ship" folder (section 5). | Remove + fix (~1,300 lines). It goes together with the old hero assets and ~22 old developer tools. |
| **Golf instant replay** (`GolfGame` `StartReplay`/`PlayReplay`/`ReplayCamera`, `StrikeHud.ReplayBadge`) | **The slow-motion replay of a great golf shot quietly stopped working on Oct 6**, when commit `7ecf8100` removed the only call that starts it. `ReplayTests` should now fail. | Remove + fix, **or bring the feature back if you still want it.** |

### Other dead game code

| What | Lines | Verdict |
|---|---|---|
| 53 methods nothing calls (camera shots, venue helpers, old rules helpers; full list in details) | 469 | Remove |
| 37 unused settings/values and 3 unused small types | ~55 | Remove |
| Face/hand authoring helpers from the Oct 8 pass (`HeroEyeApertureForm`, `HeroContinuousHandMesh`, `HeroHandNormalFields`) | ~100 | Remove (with the `HeroThumbRigProbe` tool) |
| `GolfTutorial` (a golf lesson overlay that is never created) | 100 | Remove + fix: delete `GolfTutorialTests`. Or keep it if a golf tutorial is planned. |
| `Resources/Course/Shaders/PostcardV3Proof.shader` | 92 | Remove + fix: a proof-only shader that ships because of where it sits. Move it to `Editor/` or delete it with its tools. |
| iOS bridge: `SportsPollInput` and its input queue, `SportsClear`, `GolfAirPlay_ScreenCount` | ~25 | Remove + fix. **The iPhone app refuses to start Unity if `SportsPushInput` is missing** (`SportsRuntime.mm:85`), so remove that requirement in the same change. |

### Your call

| What | Lines | The question |
|---|---|---|
| **Unity-drawn stand-alone golf app layer:** `UI/HomeScreen`, `Lobby`, `Locker`, `LockerColor`, `ControllerSheet`; `Game/BigScreen`, `ClubStage`; `Net/Online/*`; `Course/Championship`; `Profile/ChampionshipStore`; plus ~650 lines inside `GolfGame.cs` and `Hud.cs` | ~4,100 | Menus Unity drew itself before the native SwiftUI menus. The native app never shows them. The online part talks to `server/` at a home-Mac address. **Biggest code win, but testing golf inside the Unity editor still relies on these menus**, and 8 tests use them. Before deleting, move `Lobby.PlayerColor`, which live play uses. |
| Old V4 golfer fallback (`GolferView.BuildModel`/`BuildFigure`/`Clay`, the V4 part of `GolferStyle`) | ~150 | Used only if the current golfer file is missing, plus one comparison test. Goes with `Resources/Golfer` (section 5). |
| Commands the iPhone app never sends: tennis "aim practice", golf "overview" fly-through on request, old "ultimate" commands; the hub's new `matchReady` event has no listener yet | ~80 | Drop them, or connect them to the app. A few tests use them. |
| `StandardGolfGrip`, `GolfSwingCalibration` (used only by tests) | 137 | `main`'s CI compiles `GolfSwingCalibration`. |
| `TennisReferenceMatch` + `TennisReference.unity` (the reference-video matching stage) | 147 | Delete together if that pass is finished. |
| Editor-only "proof runners" sitting in the game folder (`GolfMovingTurfProofRunner`, `PostcardRefreshProofRunner`, `GolfVisualPhysicsGate`) | 759 | Never reach the phone. Delete, or move to `Editor/`. |
| Shaders only legacy code uses (`GolferClay`, `ClubBackdrop`, `ClubDisc`, `KitRecolor`) | 308 | They are forced into every build by `ProjectSetup.RuntimeShaders`. Remove the names there if their code goes. |

### Developer tools (editor scripts; never in the app)

| What | Files / lines | Verdict |
|---|---|---|
| **Retired Hero01–V6 tools:** 10 Hero01 review-capture tools, `HeroGameplayBuild`, `HeroLockerExport` (still writes old `HeroV4` files into the app folder if run), `HeroIntegrityAudit`/`Review`, `HeroV5Rebind`, `HeroV6Import`, `HeroV6Polish*` ×3, `LegacyHero01`, `MatchHeroAB`, `HeroLookImporter` | 22 / 2,539 | Remove, together with the old hero assets (sections 5–6). `HeroLookImporter` is why Hero01 is stored uncompressed (83 MB). |
| **Two committed shortcuts into `work/`:** `Editor/PostcardV3Scratch` + `.meta` | 2 links | Remove + fix. They are absolute symlinks into `work/postcard-look/v3`. On this Mac they pull 3 "DRAFT" tools (one is an importer) into the editor; on any other computer they point at nothing. |
| `ClothCompileProbe` | 70 | Remove: **already broken** (it reads a file that doesn't exist). |
| HeroBase proof tools (`HeroBaseFemaleHeadProof`, `HeroBaseMaleFormProof`, `HeroBaseSkinProof`, `HeroBaseStaticImport`) | 4 / 555 | Your call (tied to the base_lock reference). |
| Golf look-gate tools (`GolfLookSmoke`, `PostcardImportCheck`, `PostcardLookStills`, the `PostcardV3*` drafts, `GolfPlantSwayCheck`, …) | 16 / 6,848 | Your call. Keep `GolfLookSmoke` + `PostcardImportCheck` if look checks still run; the `PostcardV3` drafts are the most disposable. |
| Current-hero, serve, emote and cloth proof/film tools | 18 / 3,505 | Your call: reusable when heroes, clips or outfits change. |
| Visual-overhaul capture, cost and film tools | 24 / 3,320 | Your call (recent). |
| One-off probes and repair tools | 19 / 1,349 | Your call. |
| Early tennis scene/review tools (`TropicalArenaReview`, `TennisReferenceSceneBuilder`, `TennisSceneBuilder`, `ClipAudit`, `ClipContactAudit`) | 5 / 375 | Your call. Keep `ClipContactAudit`, which regenerates a file the game loads. |

### Unity tests

| What | Verdict |
|---|---|
| Tests of retired heroes and hair (`HeroV6RetargetTests`, `HairFitAuditTests`, `Score80HairABTests`, `Score80WardrobeAuditTests`) | Remove (with the old heroes). |
| `ReplayTests` | Goes with golf replay (above). |
| "Score-80" harness (`Score80CaptureTests`, `Score80ClipProbeTests`, `Score80EnvAuditTests`) | Your call. They only make archive captures. |
| Tests of the Unity-drawn menus/online/championship (8 files, 954 lines) | Your call; they follow that code. |
| 12 capture/film tools written as tests (`DemoVideoTests`, `IntroCaptureTests`, `TennisFullMatchCaptureTests`, …), all manual-only | Your call. Keep `HubPreviewCapture`, which makes the Hub's preview videos. |
| **Last recorded failures:** 5 golf PlayMode tests (Oct 10); several `TennisGameplayTests`/`TennisPolishPlayTests`/`TennisTutorialPlayTests` (Oct 6) | Fix or retire. Listed in details (UCODE-35). |
| `Unity/Tools/demo_video.sh`, `swing_video.py` | Your call: the golf demo-reel scripts, last used Sept 27. |

### Looks unused, but keep

- **The build/import pipeline (30 files):** `BuildMenu`, `ProjectSetup`, `UrpSetup`, the current hero builders (`MatchHeroBuild`, `MatchHeroImport`, `MatchHeroKit`, `MatchHeroLockerExport`, `EmoteWire`, `ServeAndFeetWire`, …) and 11 importers. **Removing an importer changes how its folder imports.**
- `GameCapture.cs` (a screenshot helper that has to live in game code so tests can use it), `CourseLies`/`Surfaces` (extension methods), `TennisSynth`'s nested types, `GolfPostcardBasalt` (hole 10's basalt), the start-up hooks (`ErrorGuard`, `MuteWhenAutomated`, `HeroLightingProfile`).
- **The hidden tennis "stand-in":** `TennisActor`, `StandardCharacterArms`, `TennisKitV3`. These drive the movement and racket contact behind the visible hero.
- `Unity/Tools/check.sh` + `TestRunner.cs`, `build-integrated-ios.sh`, `install-phone.sh`, `stamp.sh`, `run-playmode-windowed.sh`, the 4 asmdefs and `link.xml`.
- **Watch out:** two hero rebuild tools copy **older** animation files than the ones installed. `ServeAndFeetWire.cs` copies the Serve from `work/serve-and-feet` (the installed one came from `work/serve-dip-jump`). `EmoteWire.cs` copies emotes from `work/emotes` (the approved ones are `work/emote-fluid` revision 37). **Rebuilding the heroes would silently bring back the old Serve and the old emotes.** Repoint both tools.

## 5. Unity's "always ship" folder (`Unity/Assets/Resources`): the biggest phone-size win

*754 files, 2.6 GB on disk. In the last iPhone build (Oct 8) this folder took **1,352 MB of the 1,522 MB** of Unity game data. Checked by resolving every one of the 219 places the game loads a file, and against the real build report. Detail: `details/unity-resources.md`.*

**In short:** Unity copies **everything** in this folder into the app, used or not. About **378 MB (a quarter of Unity's part of the app) is never loaded by the game** and can go with certainty. About **130 MB more** is only reachable through developer switches or fallbacks. Two items that *are* used take **440 MB** between them and are the biggest size levers after that (see "Keep").

Two things to know:
- **The new hub files here aren't in git yet:** `Hub/HubGlitch.mat`, 6 `Hub/Previews/*.mp4`, `Course/flag_1..6.png`. They must be committed with the hub code and the modified `Hole.cs`.
- Some textures are linked to their models at import time, and only the build report sees those links. Check the build report before deleting a material or texture that sits next to a model file.

### Safe to remove (never loaded)

| Item | What it is | App size |
|---|---|---|
| `Tennis/Hero/Hero_01_Tennis.prefab`, `HeroV4_KitMask.png`, `HeroV4_KitRef.json` | **The old Hero01 tennis player.** Nothing loads it, but it drags all of `ArtDirection/Hero01` into the app, stored uncompressed. | **87 MB** |
| `Tennis/Premium/HandRest`, `Hands`, `HandNormals`, `HandPose` (10 files) | An earlier "hand v3" experiment. Every loader is dead or switched off. | **101 MB** |
| `Tennis/KitsV3` old kits: `standard_*_kit.fbx`, 14 materials, 15 textures (10 are duplicates) | Outfits for the old body pieces; the code that would put them on never runs | 43 MB |
| `Tennis/Customization`: `Custom*`, `HiggsAvatar*`, `Player*{Color,Mask,Bob,Curls,Swept}`, `Player*Golf.fbx` (24 files) | The old "customize your player" data. **Keep the 4 `Player*.prefab` files: they are today's heroes.** | 39 MB |
| `Tennis/Opponents/*.fbx` (12) | Old per-rival bodies | 16 MB |
| `Golfer/crowd_*` (4) | Old golf spectators (replaced) | 10 MB |
| `Tennis/CoastalTennisResort.fbx` | An earlier tennis venue | 9 MB |
| `Tennis/HeroDetail/Kit_*_{Top,Bottom,Shoe,Sock}.png` (12, no `_N`/`_M`) | Old plain seam maps; the newer pairs always exist | 9 MB |
| `Club/` 11 unused pictures (`club-bowling/boxing/dial/football/friends/lesson/whistle`, `scene-cliff/pavilion/stands`, `menu-backdrop`) | Old club-menu art | 9 MB |
| `Course/Look/Sky_Crater_C.png`, `Tennis/Environment/SkyPanorama.png` | Skies named in a setting nothing reads (`Sky_Crater_C` alone is 9.6 MB in the app) | 11 MB |
| `Tennis/Characters` rival/crowd textures (28) | Textures for the unused old rivals and crowd | 7.5 MB |
| `Tennis/UI/call-*.png`, `grade-*.png` (19) | Painted umpire-call and grade banners; the HUD draws text now. **Keep `racket-map.png`.** | 4 MB |
| `Course/hole_*_card.jpg` (13) | Hole preview pictures the HUD ignores | 4 MB |
| Smaller items: old tennis crowds, old island scan textures, `Tennis/EnvV4` unused props, `Course/Resort/SkyCoastal.png`, non-toon club pictures, Nunito and Roboto fonts (+ their licence files), golf cap maps, `Golf/Fitted_Female_Body.asset`, single golf-club models, `BillingMode.json` (left over from a removed in-app-purchase package), `TropicalV3/export-report.json` | Various leftovers | ~14 MB |

### Safe after a small fix

| Item | App size | The fix |
|---|---|---|
| `StandardCharacters/standard_*_golf.fbx` | 7 MB | Used only by a test; move it to `Tests/Fixtures/`. |
| `Course/flag_7.png`, `flag_12…23.png` (13) | 1 MB | Dead only once today's uncommitted `Hole.cs` and the new `flag_1..6` files are committed. Commit those first. |
| `Course/Look` rock/scrub textures (9) | 3 MB | Two developer gate tools still check them; update the tools. |
| `Course/Lava/ash_gravel`, `lava_cracks`, `crater_rock`, `crater_relief` | 0.3 MB | Remove the fallbacks and stop the texture baker writing them. |
| `Course/Shaders/PostcardV3Proof.shader` | tiny | A developer-only shader; move it out of this folder. |
| The kit `racket.fbx` source | — | Move it out of this folder so the arena importer can still rebuild the racket. **Keep `RacketGameplay.*`: it is the live racket.** |

### Your call (about 130 MB more)

| Item | App size | Why it's a judgement call |
|---|---|---|
| `Tennis/Correctives/*.json` (5) | 32 MB | Fit tables for the old shirt. Only used if the tailored polo is switched off with a developer switch. |
| `Course/hole_09.fbx`, `hole_12.fbx` | 16 MB | **Loaded and then thrown away**: the game swaps in better versions of those holes. Developer tools still need them, so move them out of this folder and skip the load. |
| `Golfer/golfer_*` (old V4 golfer, 8 files) | 18 MB | A fallback plus one comparison test. **The golfer build script reads `golfer_m.fbx` as an input**, so move it rather than delete it (section 8). |
| `Tennis/TropicalV3` textures for parts hidden in every venue (7) | 17.5 MB | Needs the arena re-exported without those parts. |
| `Tennis/Characters` skin/face textures for the **invisible** tennis stand-in (11) | 13 MB | Becomes dead once the stand-in's pointless recolouring is removed (section 4). |
| Face-finish review variants, reference-head pilot, old hole-12 cliffs, old fitted shirts, a replaced forehand clip, cliff scans that get overwritten, turf fallback tiles | ~30 MB | Each is only reachable through a developer switch or fallback. |
| 3 byte-identical duplicate files of live files | 4 MB | Point each loader at one copy. |
| `Tennis/Shaders/TennisSky.shader` | tiny | Forced into the build, but only a developer tool uses it. |

### Keep, including the two biggest items in the app

| Item | App size | Note |
|---|---|---|
| `Tennis/KitsTailored/*Polo{Full,Distance}.asset` (4 baked polo-shirt animation meshes) | **282 MB** | Live and required. 88 animation frames each. **The single biggest size lever:** fewer frames or a smarter format could save a lot. (They also take 1.2 GB of git storage.) |
| `StandardCharacters/standard_*_tennis.fbx` (the invisible tennis stand-in) | **158 MB** | Live: it drives movement and racket timing behind the visible hero. 146 MB of it is 120 animation clips **imported with compression turned off**. Turning compression on, or trimming unused clips, could save 100+ MB; re-test gameplay afterwards. |
| Everything else: 428 files, about 487 MB in the app | | Arenas, heroes, golf courses, menus, fonts and shaders in use. Also 3 shader-variant materials that look unused but stop needed shader versions being stripped, and the `.hlsl` include files. |

**Also found:** `TennisAnnouncer` loads every clip in `Tennis/Announcer`, but **that folder doesn't exist, so the announcer has no sounds.** And 41 files here over 1 MB (541 MB, such as 407 MB of animation text files) aren't in git's large-file storage, which bloats the repo.

## 6. Other Unity assets, Unity add-on packages, and Unity's generated folders

*Models, scenes and materials outside the "always ship" folder; the add-on packages; the folders Unity writes on its own. Detail: `details/unity-assets-other.md`.*

**In short:** none of the old character folders here ship in the game. Only old developer tools use them, so they can go together with those tools (section 4). Seven Unity add-on packages are unused, and one of them (analytics) still adds start-up code to the app. Unity's own cache folders are about 8.6 GB of rebuildable data.

### Old characters and scenes (only old developer tools use them)

| Item | What it is | Size | What removing it affects | Verdict |
|---|---|---|---|---|
| `Unity/Assets/ArtDirection/Hero01/` (125 files) | The September "Hero01 / V4" tennis player: body, outfits, 5 rounds of animation fixes. Replaced Oct 3. | 91 MB | Game: nothing, once the Hero01 prefab leaves the "always ship" folder (section 5). About 16 old review/rebuild tools, the 8 scenes below, and the V5/V6 folders point into it, so remove them together. `LegacyHero01.cs` says it was kept on purpose "to rebuild the old look by hand". | **Your call** (archive or delete as one set) |
| `Unity/Assets/ArtDirection/HeroV5AstraTex/` (23) | Frozen before/after copy from the V5 texture experiment | 14 MB | 3 old review tools (Sept 29–30) stop working; delete them too. | Remove + fix |
| `Unity/Assets/ArtDirection/HeroV6/` (3) | The AI-generated V6 hero, imported once to test animation transfer ("experiment only") | 16 MB | A manual-only test (`HeroV6RetargetTests`) and an import menu break; delete both. | Remove + fix |
| `Unity/Assets/ArtDirection/HeroV6Polish/` (30) | The V6 polish pass (a failed candidate) | 9 MB | 3 old tools break, and 3 small game scripts become fully dead (section 4). | Remove + fix |
| `Unity/Assets/Scenes/Hero01*.unity` (8 scenes) | Stages saved by the Hero01 review tools, one per review round | 2 MB | Only those old tools open them. | Remove + fix (with the Hero01 tools) |
| `Unity/Assets/Scenes/TennisReference.unity` | Empty scene for lining up the game camera with the AI reference video | 6 KB | 2 manual-only tests and a menu (which recreates the scene if missing) use it. The small `TennisReferenceMatch` script ships in the game only for it. | Your call |
| `Unity/Assets/Characters/HeroBase/` (39 of its 73 files: grey bodies, hair, prefabs, studio scene, textures, other materials) | The grey bald "base_lock" mannequins (no skeleton), used to prove the body matched the reference photos (Sept 30–Oct 2) | 4.7 MB | Game: nothing. 4 proof tools and the `Tools/HeroBase` scripts break. AGENTS.md calls base_lock the locked reference. | Your call |
| `Unity/Assets/Characters/Archive/OldPlayerBase/` (2) | The pre-October tennis bodies, already archived | 1 MB | Nothing. Optional tidy: remove the now-pointless archive step in `MatchHeroBuild.cs`. | Remove |
| `Unity/Assets/Archive/IconKit/` (70) | The retired "3D icon" avatar kit (hats, glasses, hair, old locker pictures), kept in case parts are reused | 19 MB (normal git files, not large-file storage) | Nothing uses it; three docs mention it. | Your call |

### Looks unused, but keep

| Item | Why |
|---|---|
| `Characters/HeroBase/Materials/Male_*.mat` + `FemaleHeadF1/Materials/Female_*.mat` (32) | The current-hero builder (`MatchHeroBuild`) copies these face and skin materials and **errors if they're missing**. |
| `Characters/HeroBase/Rendering/HeroBaseStudioURP.asset` + renderer | The golf look checks (`GolfLookSmoke`) fail without it. |
| `Unity/Assets/Tests/Fixtures/StandardCharacters/` (6 models, 15 MB) | A normal automated test (`StandardArmSportTests`) needs them. They go only if the "Standard Characters" feature goes (section 5). |
| `Unity/Assets/Editor/GolfPlantSwayFixture/` | Test models for the golf plant-sway check. |
| `Editor/DiagnosticHandSurface.shader`, `ClothProofMask.shader` | Used by 2 probe tools; remove them only together with those tools. |
| `DefaultVolumeProfile.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `link.xml` | Core render settings, and a list that stops needed code being stripped from the app. |
| `Characters/MatchHeroes`, `MatchHeroKit`, `GolfHeroKit` (163 MB) | The current heroes and their outfits. |
| `Scenes/Golf.unity`, `Tennis.unity`, `Hub.unity` | The three game scenes. **`Hub.unity` isn't in git yet** (see "Problems found along the way"). |

### Unity add-on packages (`Unity/Packages/manifest.json`)

| Package | What it is | What removing it affects | Verdict |
|---|---|---|---|
| `com.unity.analytics` (pulls in Unity Services) | Unity's analytics service. The game never calls it, but it still adds 15 code libraries, **8 routines that run at every app launch** and 3 native plugins to the app. | Game: nothing. The app gets lighter and starts with less work. Re-check the privacy manifest after the next export. | Remove |
| `com.unity.timeline` | Cutscene editor; never used | Nothing. | Remove |
| `com.unity.ai.navigation` | Path-finding for characters; never used (also adds 4 launch routines) | Nothing. | Remove |
| `com.unity.multiplayer.center` | A Unity template default; the game has its own multiplayer | Nothing. | Remove |
| `com.unity.collab-proxy` | Plastic SCM version control; the repo uses git | Nothing. | Remove |
| `com.unity.xr.legacyinputhelpers` | VR/AR helpers; no XR in the game | Nothing. | Remove |
| `com.unity.2d.sprite`, `com.unity.2d.tilemap` | 2D sprite and tilemap editors; not used | Nothing. | Remove |
| `com.coplaydev.unity-mcp` | Lets AI agents drive the Unity editor | Agents lose that bridge. | Your call |
| Rider / Visual Studio integrations | Code-editor plugins (editor only) | Keep the one you use. | Your call |
| URP, UGUI, Test Framework, engine modules | Needed | — | Keep |

### Unity's generated folders (not in git)

| Item | What it is | Size | Verdict |
|---|---|---|---|
| `Unity/Library/` (everything except `Captures`) | Unity's import and build cache, rebuilt automatically. The next open takes hours. It holds `LastBuild.buildreport`, the only record of what the last build shipped. | ~5.9 GB | Remove (only with the editor closed) |
| `Unity/Library/Captures/` | Screenshots and frames written by 36 automated tests and tools | 5.1 GB | Your call (old proof images) |
| `Unity/Builds/iOS/` | The Unity-to-Xcode export the integrated app is built from. Needed to build. **Stale**: made Oct 8, before the Hub scene existed. | 2.5 GB | Your call (it needs a fresh export anyway) |
| `Unity/work/` | Leftover output of compile checks. Git doesn't ignore it, so it shows as untracked. | 108 MB | Remove, and add it to `.gitignore` |
| `Unity/mono_crash.*` (45 files) | Unity editor crash dumps (Sept 26–Oct 5) | 28 MB | Remove |
| `Unity/Assets/InitTestScene*.unity` (3) | Left behind by interrupted test runs | tiny | Remove (not during a test run) |
| `Unity/Logs/`, `Unity/Temp/` | Logs and temp files | tiny | Remove (editor closed) |
| `Unity/UserSettings/` | Your editor layout | tiny | Keep |

## 7. ArtDir: reference art, review screenshots and proof videos

*15.1 GB, 24,651 files, nearly all in git large-file storage. Detail: `details/artdir.md`.*

**In short:** the game never opens anything in ArtDir. About **83 % (12.6 GB)** is about the retired Hero01 character and its V4–V6 versions: screen captures, frame dumps, review films and test runs. Three things to know before deleting:

- **Tests write into ArtDir.** Several automated tests save their screenshots here on every run, which is why 20 ArtDir pictures always show as "modified" in git. The test output has already tripled some folders' size in git history. Point those tests at a temporary folder.
- **One normal Unity test breaks if `ArtDir/score80/` is deleted.** `NativeLaunchHeroTests` writes a text file there without creating the folder.
- **Deleting doesn't shrink git history.** ArtDir also takes about 13.8 GB in the local git large-file cache (`.git/lfs`), and the server keeps everything unless history is rewritten. See "Git history" in the cleanup order.

### Safe to remove (6.5 GB)

| Item | What it is | Size |
|---|---|---|
| `screenshots/tennis_complete/` (3,545 files) | Frame captures of the retired Hero01 playing tennis (Sept 26–27) | 2.6 GB |
| 8 Hero01 frame-dump folders in `screenshots/`: `knees_offarm_playmode`, `backhand_run_playmode`, `anim_repair_playmode`, `gameplay_fixed_playmode`, `fullstroke_grip_playmode`, `v4_legacy`, `restore_motion_playmode`, `owned` | Frame-by-frame dumps from Hero01 animation fixes | 860 MB |
| `hero/v5_target/BEFORE/` | "Before" stills plus a review film; 565 MB of it is an exact copy of `anims/multi_angle_review` | 886 MB |
| `review/unimate-tennis/` | An AI motion experiment on the V5 hero, never adopted | 488 MB |
| `screenshots/online_lobby/` | Proof screenshots of the online lobby (Oct 5) | 454 MB |
| `hero/v5` render folders (`iter1`–`iter22`, `probe`, `nohat`, sheets) | V5 hero render iterations | 419 MB |
| `hero/v5/archive_V4/` + `hero/v5/viewer/` | Backup of the V4 locker files and a web viewer | 95 MB |
| `screenshots/golf_flyover/` | One golf flyover film | 99 MB |
| `screenshots/golf_postcards/frames/` | Raw frames used to make a golf video | 91 MB |
| `blockout/`, `env_v5/` | Early AI-generated hero blockout and tree test (Sept 25–27) | 32 MB |
| `review/current_model`, `review/extra`, `screenshots/character_ears_fix`, `screenshots/body_clearance` | Hero01 screenshots and notes | 10 MB |
| `ui/toybox-simple-v6/source/preview-raw` (already git-ignored) | Old menu bakes from a retired exporter | 167 MB |
| Git-ignored Blender backups (`*.blend1`) and `__pycache__` across ArtDir | Automatic backup copies | 464 MB |

### Safe after a small fix (6.05 GB)

| Item | What it is | Size | The fix |
|---|---|---|---|
| `score80/` (2,205 files) | The Sept 27 "Score 64→80" Hero01 evaluation runs | 1.2 GB | First change `NativeLaunchHeroTests` to write to a temp folder (or create the folder), or it fails. A code comment cites `probe/hair_winding.txt`. |
| `anims/multi_angle_review/` | 12-angle review films of Hero01 | 878 MB | Fix one link in `docs/character-handoff/README.md`. |
| `anims/captures/` | Gameplay films from the September tennis plans | 782 MB | Links from the old PLAN docs, which are removable themselves (section 9). |
| Other `anims/` Hero01 folders (432 files) | Blender work files and renders from the retired hero's animation phases | 611 MB | Drop one doc mention. Some are one-of-a-kind Blender work on a retired character. |
| `screenshots/golf_course_standard/` | Golf course before/after captures | 343 MB | Keep its `GATE_RESULTS.md` if you want that record. |
| `review/intro/`, `intro_waves/` | Hero01 intro-movie captures | 298 MB | Change the output folder of a manual-only test (`IntroCaptureTests`). |
| `ui/motion-club-radial-v1/runtime/` | Recordings that `MotionClubReviewTests` makes on every run | 283 MB (422 MB already in history) | Point the test at a temp folder; fix one doc link. |
| `hero/v6_full`, `v6_polish`, `v6_proof`, `v6_target` | The failed V6 hero | 208 MB | Retire the V6 developer tools and their test (section 4). |
| `hero/v5_proof/` | V5 proof images | 206 MB | **Move `LIGHTING.md` to `docs/` first.** Code comments in `CharacterModelPreview.swift` and `TennisLook.cs` cite it as the light recipe. |
| `hero/integrity_v1/` | A rejected "integrity repair" experiment | 167 MB | Retire 2 old developer tools (`HeroIntegrityAudit`/`Review`). |
| `screenshots/plan*` (8 folders) | Proof stills from the September tennis plans | 159 MB | Remove together with those PLAN docs. |
| `review/v6_retarget/` | V6 animation-transfer frames | 119 MB | Retire `HeroV6RetargetTests`. |
| `review/venues/`, `review/receive-camera/` | Venue films; receive-camera stills that a normal Unity test rewrites | 118 MB | Move that test's output; fix one doc link. |
| `screenshots/gameplay_fixed/` | Hero01 frame dump plus a data file 5 old tools read | 115 MB | Delete those tools too. |
| `screenshots/cloth_overhaul/` | Cloth-overhaul proof | 104 MB | Update `hero/kit/COPY_TO_GOLF.md`. |
| `anims/restore_motion/` | Hero01 restore-motion work | 78 MB | 3 data files are read by old tools; retire the tools. |
| `environments/motion-club-3d` review images | Reviews; one folder is rewritten by `ClubEnvironmentTests` on every run | 72 MB | Redirect the test; fix one link. |
| `ui/toybox-simple-v6/runtime/` | Screenshots `TennisMenuSnapshotTests` rewrites on every run (16 modified now) | 69 MB (208 MB in history) | Point the test at a temp folder. |
| `screenshots/golf_postcards/` stills + `look/` | Hole 8/9/10 proof stills | 67 MB | A checker script (`postcard_look_verify.py`) reads `look/`, so re-capture before running it. |
| `screenshots/venues/` | Tennis venue captures | 59 MB | The capture tool recreates it; change its default output folder. |
| `screenshots/stance/`, `screenshots/tennis_abilities/` | Unity test captures; `tennis_abilities` is rewritten every run | 56 MB | Redirect the test output. |
| `tennis/audience` older versions (Hero4/Hero5) | Older crowd source files | 50 MB | Update `source-manifest.json`. |
| `review/tv-menus/` | TV menu tour film | 32 MB | Fix one doc link. |
| `review/post-match/` | Test output, plus 2 photos from a real phone | 29 MB | Redirect the test; keep the 2 real-device files next to their doc. |
| `screenshots/emote_loadout/`, `golf_locker_swap/` | Test screenshots (4 modified now) and 2 gate notes | 19 MB | Same test as above; move the notes if wanted. |
| `screenshots/` loose files (37) | Hero01 vs reference-plate comparisons | 11.5 MB | Fix one doc link. |
| `golf/source/GolfGalleryHero3.blend` | A superseded debugging checkpoint | 5.8 MB | Remove its manifest entry and the matching `hero3` tools. |

### Your call (1.86 GB)

| Item | What it is | Size | Note |
|---|---|---|---|
| `hero/kit/premium/source/` (49 Blender files) | Oct 7 iteration chain for hands, gloves and sleeves | 826 MB | Keep the newest of each line; about 316 MB are superseded versions. Confirm which file produced `Resources/Tennis/Premium`. |
| `hero/v5` sources, `v5_astra`, `v5_tools`, `v5_styles`, `v5_tripo`, `v5_scalp` | V5 Blender sources and hair experiments | 328 MB | At minimum drop the duplicate 51 MB blend. |
| `anims/source/` | A free third-party tennis motion-capture pack with its licence note, plus Mixamo files | 249 MB | It can be downloaded again. No current animation comes from it. |
| `hero/base_lock/proof/current_models_20261002/` | Oct 2 turnaround snapshot of every body part | 216 MB | History. |
| `hero/` Hero01 originals (+ `_rejected_mannequin`, `turnarounds`, `modular`) | The very first hero | 86 MB | Archive or delete. |
| `ui/` superseded concept rounds (6 folders) | Menu explorations made before the approved design | 77 MB | Removing is safe. |
| `golf/proof/` | Turf and cliff proof; 52 MB is duplicated in `proof/` | 67 MB | Keep one copy. |
| `hero/base_lock/preview/` | Oct 1 outfit/face mock-up; its index says not to use it | 18 MB | |
| `ui/motion-club-radial-v1` docs/mockups + `ui/motion-club-gameplay-hud-v1` | Menu and HUD direction | 18 MB | |
| `plates/` | Six original concept plates (Sept 25, marked "not locked") | 17 MB | |
| `hero/v5_target/` images, `review/tennis-timing-20261006/`, `review/point-clips/`, 68 prompt and brief text files | Small records | 2 MB | |

### Keep (0.7 GB)

- `hero/base_lock/`: the locked character reference (plates, Blender files, face packs, proof gates). `Tools/HeroBase` scripts, 35+ `work/` jobs and 2 Unity tools read it.
- `hero/clothing/tailored-2026-10-08/`: sources of the shipped polo and skirt.
- `hero/kit/` (except `premium/source`): the garment standard. Its texture maps match the files the app ships.
- `hero/kits/`, `hero/source/reference/`: the racket and kit references, and the anatomy source with its licence.
- `golf/` and `tennis/` current sources; `environments/refs/` (a checker reads it); `environments/motion-club-3d` sources (where the 3D locker room comes from); the environment briefs.
- `env_v4/`: the source of a tennis prop kit the game still uses.
- `ui/toybox-simple-v6/` approved set: the app's code names it as the approved menu design.

**Duplicates:** about 1.09 GB inside ArtDir is byte-identical copies. Keeping one of each would free that much.

## 8. Blender files, the SportsLibrary, and the hero-base tools

*`blender/` (golf holes, golfer, scripts), `SportsLibrary/` (the Sept 20–24 "sports library") and `Tools/HeroBase/`. About 2.6 GB tracked plus 0.9 GB of local Blender backups. Detail: `details/pipelines.md`, which has a one-line verdict for each of the 140 Blender scripts.*

**In short:** Unity never opens a `.blend` file. These are **source files**: the game uses the models exported from them. So "removing it breaks the game" is never the issue here. The real question is whether you'd still be able to rebuild or change something the game uses. **All the golf hole sources are live and must stay.** The SportsLibrary is mostly dead weight (about 1.7 GB), except for three files that are the only source of things still in the game.

### Safe to remove

| Item | What it is | Size |
|---|---|---|
| `SportsLibrary/ArtDirection/Tennis/Generated/models/*` (all except `umpire.glb`, `island.glb`) | Raw AI-generated avatar, opponent and emote models from Sept 24 | 518 MB |
| `SportsLibrary/Customization/**` + 6 scripts (`export_player_bases.py`, `export_customization.py`, `fit_base_hair.py`, `fit_golf_bases.py`, `build_sportswear.py`, `render_sportswear_study.py`) | The Sept 25–28 customizable player bases. Their outputs (in Unity and the app) aren't loaded any more. | 309 MB |
| `SportsLibrary/Clothing/**` | Sept 22 outfit concepts and generated kits. The kit they produced is never put on anyone. | 149 MB |
| `SportsLibrary/Characters/**` | The original "permanent standard" character models (Sept 20) | 127 MB |
| `SportsLibrary/Blender/all-arenas-and-animations-v4.blend` | Combined master file, superseded by the studio file below | 104 MB |
| `SportsLibrary/Design/*-Historical/` | Concept sheets already labelled "historical" | 98 MB |
| `SportsLibrary/Arenas/Tennis/Tripo-v3/Assets/` | 23 raw generated arena pieces; the arena file holds its own copies | 91 MB |
| `SportsLibrary/ArtDirection/` other images | Reference and target images; Unity has its own copies | 81 MB |
| Bowling and boxing arenas, original arena models, crowds | Bowling and boxing aren't in the game | 75 MB |
| Video working frames and tracks (`frames24/`, `tracks/`, `serve-side.mp4`) | Data pulled out of the concept video for retired scripts | 68 MB |
| `SportsLibrary/Previews/`, `Animations/` concept images and V4 action lists | Old previews | 51 MB |
| `SportsLibrary/Equipment/*` (generic V4 golf/tennis/bowling/boxing gear) | Not in Unity; clubs come from `golf_clubs.py` | 15 MB |
| `SportsLibrary/asset-inventory.json`, `Tools/verify_library.py`, `SportsLibrary/.gitignore` | Packaging list (covers 268 of 801 files) and its checker, which would fail anyway | tiny |
| 35 tennis scripts from Sept 22–24 (`author_tennis_*`, `retarget_*`, `rotomate_*`, `fit_avatar`, `fit_opponents`, `build_video_character`, …) | The retired tennis-character chain. They import each other, so delete them as a unit. | 0.3 MB |
| 8 Hero01 repair scripts (`hero_avatar`, `hero_face`, `hero_golf_retarget`, `hero_hair`, `hero_hats`, `hero_head`, `hero_parts_export`, `hero_repair`) | Repaired the scanned Hero01; outputs deleted Oct 5 | 0.1 MB |
| `golfer_build.py`, `golfer_export.py`, `golfer_preview.py`, `blender/golfer_m.blend`, `golfer_f.blend` | Andrew's original low-poly golfer (Sept 20). Re-running it would **overwrite** a file the current golfer build needs, so removing it also removes that risk. | 0.3 MB |
| `fit_golf_characters.py` + `blender/characters/golf/*` | Fitted Sept 23 AI golf bodies; the current golfer ignores them | 7 MB |
| `turf_textures.py` | Its output was replaced on Sept 23 | tiny |
| `blender/holes_mock/` | Concept pictures for holes 13–20, plus number files the course builder rewrites itself | 9 MB |
| `blender/previews/` | Render stills and old UI mock-ups; nothing reads them | 15 MB |
| Local Blender backups (`*.blend1`) and `__pycache__` | Automatic backup copies (not in git) | 933 MB |

### Safe after a small fix

| Item | The fix |
|---|---|
| `build_crowd.py` + `blender/crowd-clips.json` | Built the old seated crowd, which the game no longer loads. Delete together with `Resources/Tennis/Crowd/*.fbx` and `Editor/TennisCrowdImporter.cs` (that importer reads the json). |
| `SportsLibrary/Arenas/Tennis/coastal-tennis-resort-v1/v2` (8 files, 12 MB) | The archived earlier resort. Remove the arena half of `export_tennis_runtime.py` (or always run it with `--tennis-only`); otherwise it rewrites these files. |

### Your call

| Item | Size | Why it's a judgement call |
|---|---|---|
| `SportsLibrary/Arenas/Tennis/tropical-tennis-resort-v3.blend` | 359 MB | **The source of the tennis resort arena the game uses.** The game doesn't need it to run, but you couldn't edit the arena without it. Its exporter script lives outside the repo. |
| `SportsLibrary/ArtDirection/.../models/umpire.glb`, `island.glb` + `prepare_umpire.py`, `prepare_island.py` | 11 MB | Sources of the umpire (every match) and the resort island. |
| `SportsLibrary/Equipment/Tennis/Tripo-v3/` | 44 MB | Source of the racket on the hidden gameplay stand-in (see section 5). |
| `SportsLibrary/Design/VenueConcepts/` (sky and volcano courts) | 37 MB | The only copy of the concept art for two live tennis venues. |
| `Tools/HeroBase/` (26 scripts) | 0.2 MB | The Sept 30–Oct 2 body-fitting tools. Nothing current uses them, and 3 fail by design. They're tied to the "locked" base_lock reference in AGENTS.md. |
| Icon avatar kit scripts (`avatar_base`, `avatar_catalog`, `avatar_export`, `avatar_head_parts`, `avatar_icons`, `avatar_mock`, `avatar_parts`, `avatar_styles`, `avatar_wardrobe`) | 0.1 MB | Retired Oct 4 but parked on purpose for hats and hair. **`avatar_kit.py` is not in this group: the current golfer build needs it.** |
| `hole12_*.py` (3), `cmu_golf_measure.py`, `adnan_golfer_export.py`, `fetch_matchheroes.sh`, `export_standard_golf.py`, `export_arm_validation.py` | small | Records of how live files were made, or rebuild steps. Their inputs are mostly not on this computer. `adnan_golfer_export.py` makes the old V4 golfer, which the current golfer build reads as an input. |

### Keep

- **All 16 golf hole sources** (`blender/hole_07…23.blend`). Each belongs to one of the 3 live courses.
- **The scripts that build the holes:** the Hole 7 phase scripts, `course_builder.py` + design modules, the holes 8/9/10 "postcard" pipeline, `pin_build.py`, `backdrop_import.py`, the golf standard props and textures, `build_reference_hole09.py`.
- **The current golfer build:** `matchhero_golf.py`, `matchhero_hair.py`, `matchhero_mhhair.py`, `mhhair_sources.py`, `golf_swing.py`, `matchhero_icons.py`, `fetch_mhhair.py`, `avatar_kit.py`, `golf_clubs.py`.
- **The hidden tennis gameplay stand-in:** `SportsLibrary/Blender/sports-animation-studio-v4.blend` (358 MB), `export_tennis_runtime.py`, `tennis_clips.py`, and **`blender/tennis-clips.json`**. Unity re-reads that json every time it imports the stand-in, so deleting it would quietly break the stand-in's animations.
- `SportsLibrary/ArtDirection/Tennis/Generated/video/tennis-gameplay-concept.mp4`: the approved look-and-motion target video.

## 9. Docs, plans and AI-agent instructions

*294 `.md` files outside `work/` and `proof/` (42 of them at the repo root), plus the AI "skill" files. Detail: `details/docs.md`, which also has the exact wording to fix in each live doc.*

**In short:** 33 of the 42 root-level docs are finished or abandoned plans and can go. The more important problem is the opposite: **`AGENTS.md` is loaded automatically into every AI session in this repo, and parts of it are out of date.** It still sends agents to finished jobs, says clothes are out of scope, and says the product is tennis-only.

Good to know:
- No build or test reads these docs, and the GitHub Pages site doesn't publish `docs/`, so removing docs can't break the app.
- Two tool scripts do read docs. `SportsLibrary/Tools/verify_library.py` reads 8 SportsLibrary docs. `Tools/HeroBase/phase0.py` and `package_review.py` read the `base_lock` contract docs.
- `final-build` and `main` have different versions of 15 docs. Do doc cleanup on whichever branch you keep, or it will collide when you merge.

### Safe to remove

| Item | What it is |
|---|---|
| `CLAUDE_KICKOFF.md` | Old AI read order pointing at a plan finished on Sept 30. `AGENTS.md` already covers this. |
| `PLAN1_…`, `PLAN1B_…`, `PLAN1C_…`, `PLAN2_…`, `PLAN2B_…`, `PLAN2C_…`, `PLAN2D_…`, `PLAN3_…`, `PLAN3B_…` (9) | September tennis phase briefs for the retired Hero01; all executed. |
| `PLAN_Score80_PartyTennis.md`, `PLAN_Score80_PHASE_A_START.md` | The Sept 27 "Score 80" plan; finished. |
| `PLAN_HeroLookShip_FinishedProduct.md`, `PLAN_HeroV5_OpaqueHair_ProperLegs.md`, `PLAN_HeroV5_AstraTextureSidePass.md`, `PLAN_HeroV6_ZeroDeficiency.md`, `CONCEPTS_HeroV6_FiveDirections.md` | Hero V5/V6 plans. Those heroes failed and were replaced; copies of these plans also live in ArtDir. |
| `docs/tennis-ultimates-and-dive.md` | Describes "ultimates", a removed feature. |
| `SportsLibrary/Design/ANIMATION-PRODUCTION.md`, `CONTROL-AND-CAMERA-BRIEF.md`, `SportsLibrary/Animations/V4/ORIGINAL-NOTES.md` | Sept 20 briefs; the last one has 13 broken links. |
| `ArtDir/hero/base_lock/parallel_prompts/README_PARALLEL_8.md`, `SKIN_LIVELINESS_BRIEF.md` | Finished work-split and skin briefs. |
| `ArtDir/review/unimate-tennis/UNIMATE_REVIEW.md` | Review of an abandoned AI-motion experiment (goes with its files in section 7). |
| The "vgpu" AI skills: `.agents/skills/vgpu*`, `.claude/skills/vgpu*` (shortcuts), `skills-lock.json` | Third-party skills that teach AI agents to build WebGPU apps. The project has no WebGPU at all, and two project briefs forbid installing vgpu. They also make every AI session list two irrelevant skills, and one tells agents to follow "AGENTS.md", which here is the game's rulebook. |

### Safe after a small fix

| Item | The fix |
|---|---|
| The 15 `GOAL_HeroBase_*.md` prompts and `HANDOFF_Sonnet_MaleHero_ChinFingers.md` | These were one-off prompts for building the hero body (Sept 30–Oct 1); the work is done. First delete the "Active handoff — 2026-10-01" section of `AGENTS.md`, which still points AI agents at them. |

### Your call: history; archive or delete with the code or assets it describes

| Item | Note |
|---|---|
| `docs/ARCHITECTURE.md`, `PLAYABLE_MOCK.md`, `PRACTICE_LAB.md`, `AUDIT_EXECUTION.md`, `CAMERA_SETUP_FIX.md`, `SWING_FEEDBACK.md`, `VISUAL_QUALITY.md` | Describe the old camera golf game. They go with that code (section 1); `README.md` links three of them. **There is no doc for the current architecture**, which is worth writing. |
| `PLAN_HeroBase_MaleFemale_Lock.md` | Where the base_lock rules come from; 4 live docs link it. Archive it after `AGENTS.md` and `HERO.md` are refreshed. |
| `PROCESS_Blender_Claude.md` | The only written Blender working method. Worth keeping as a general how-to. |
| `VISUAL_OVERHAUL_LEDGER.md` | Record of the Oct 7 visual overhaul, frozen part-way. |
| 9 dated tennis tuning notes in `docs/`, `docs/party-mechanics-research-…`, `docs/club-ui.md`, `docs/avatar-kit.md`, `docs/hero-golfer.md`, `docs/character-customization.md`, `docs/character-handoff/` | History. Some are the only explanation of a tuning number. Suggest merging the tennis notes into one log. |
| SportsLibrary docs: navigation (8; `verify_library.py` reads them), V4 integration notes (6), concept prompts (23), arena/crowd/preview READMEs (10) | Go with the SportsLibrary decision (section 8). |
| ArtDir docs for Hero01/V4 (23), V5/V6 (17), superseded base_lock proof rounds (20), the archived with-hair contracts (3), mock-ups, kit READMEs, September plan results (10), Motion Club and UI exploration notes | Go with their images (section 7). Keep `anims/source/mocapdata-tennis/SOURCE.md` (a licence) while those files exist. **Keep or move `hero/v5_proof/LIGHTING.md`, which code comments cite.** |
| `blender/characters/golf/README.md`, `blender/holes_mock/README.md` | Go with their files (section 8). |

### Keep (several need edits)

- **Keep and fix:** `AGENTS.md`, `README.md`, `CONTRIBUTING.md`, `art-bible.md`, `Unity/README.md`, `ArtDir/hero/HERO.md`, `SportsLibrary/IOS-UNITY-INTEGRATION.md` (the only build-and-install guide), `docs/magma-open.md` (wrong hole list).
- **Keep:** `VISUAL_OVERHAUL_ART_BIBLE.md` (the newest whole-game art direction; no read order mentions it yet), `PLAN_MenuHub_WalkableWorld.md` (the current plan; **not in git**), `docs/tennis-playtest.md`, `docs/matchhero-golfer.md`, `SportsLibrary/TENNIS-GAMEPLAY.md`, `MULTIPLAYER.md` (the only description of the native multiplayer protocol), `TV-INTEGRATION-VALIDATION.md`, the base_lock contracts and face specs, `INSTALL_LOG_MALE.md` (how to roll back the installed body), current gate results, the clothing and kit docs, the licence/provenance files, the golf and tennis source docs, `review/point-clips/RECORDING.md`, the presentation and menu design docs, `Unity/Assets/Characters/HeroBase/README.md`, `server/README.md`, `Tools/PointClips/README.md`, `blender/scripts/POSTCARDS_README.md`.

**The biggest stale passages to fix** (the full list with suggested wording is in `details/docs.md`, section G):
- **`AGENTS.md`:** the title says "Party Tennis" (golf is live too). It says clothes are out of scope (they shipped Oct 8). The "Active handoff 10-01" and the 10-08 clothes instruction are finished. The 35–50k triangle budget is stale (the installed male body is 66,158). `VISUAL_OVERHAUL_ART_BIBLE.md` is missing from the authority list.
- **`README.md`:** mostly describes the old camera golf game. "Getting started" says to open `GolfArcade.xcodeproj`, which builds without Unity.
- **`CONTRIBUTING.md`:** describes a `testing` → `main` branch flow that isn't used (`testing` hasn't moved since Sept 20). It says `main` is protected (GitHub says it isn't), and lists UI-test options for the old golf game.
- **`Unity/README.md`, `docs/magma-open.md`:** list the old courses. The real ones are Cliffside 7/8/9/12/15, Wild Isles 13/14/17–20, Magma Open 10/16/21–23.

## 10. Scratch folders (`work/`, `proof/`), extra git copies and leftovers

*Job folders from earlier AI sessions, plus git's own clutter. Detail: `details/scratch.md`, which has a 101-row table with one row per folder.*

**In short:** `work/` reports 651 GB and `proof/` 21 GB, but almost none of it is in git. Most of it is copies of the Unity project and Xcode build caches. Many of those copies share disk space with each other (macOS clones files without duplicating them), so **deleting everything would free about 302 GB, not 672 GB.** The safe set alone frees about **250 GB**. That matters, because **this Mac's data disk is 99 % full (14 GB free).**

Before deleting anything in `work/`:
- **`work/cloth-overhaul/toolchain/mounted` is a mounted disk image.** Run `hdiutil detach work/cloth-overhaul/toolchain/mounted` first. (Xcode doesn't use it; it uses the system's own copy.)
- **`work/postcard-look/heavy.sh` and `heavy.lock` are the "take turns" lock** that AI sessions use so Unity and Xcode never run at the same time. Another session is holding it right now. Leave both alone.
- **Some live tools and tests read files from `work/`** (listed under Keep). Copy those into the repo before any big cleanup, or rebuilds and tests break.
- **Git doesn't ignore `work/` and `proof/`**, which is why `git status` lists about 180 untracked entries. Adding both to `.gitignore` changes nothing else.
- Use `du` with care: because of the shared copies it shows about 2–3× more than deleting actually frees. Check `df` after the first big delete.

### Safe to remove (about 250 GB actually freed)

| What | Size freed | Notes |
|---|---|---|
| **51 whole folders.** Finished job logs and builds: `controller-flow-fix`, `gameplay-capture`, `gameplay-regression`, `golf-art-exports`, `golf-course-standard`, `golf-motion-fix`, `iphone-current`, `latency-fix`, `menu-beta`, `menu-flow-fix`, `multiplayer`, `onboarding`, `playability-fix`, `presentation`, `swift-module-cache`, `timing-point-fix`. Three serve attempts that failed and weren't used: `serve-walk-arm` (41 GB alone), `serve-toss-jump`, `serve-toss-arm`. Superseded body-sculpting rounds: 10 `female-*` and 18 `male-*`/`hero-base` folders (full list in details). Other: `standard-tennis-kit-wb`, `emotes-intro`, the empty `lobby-preview`, 13 loose files. | 98 GB | A few editor tools still name some of these as their default output folder, but they recreate the folder when run. |
| **Throwaway parts inside folders you keep:** Unity project copies, Xcode build caches, films, renders and profiler dumps inside `golf-look`, `cloth-overhaul` (detach the disk image first), `postcard-look/v3` outputs, `online-lobby`, `emote-fluid` iterations and films, `golf-motion-fit`, `full-visual-overhaul`, `reference-rebuild`, `phone-build-20261006`, `golf-tailoring`, `hero-mainstay` films, `emote-overhaul` films, and more | 131 GB | The details table says, per folder, exactly which subfolders to delete and which to keep. |
| Two extra git checkouts that hold no unsaved work: `…/2026-10-05/…/work/emote-fluid-staging` and `…/scratchpad/base-main-wt` | 17 GB | Their "modified" files are byte-identical to git. Remove with `git worktree remove --force <path>`. For `base-main-wt`, first make sure the session that made it has finished. |
| Junk: Blender backups (`*.blend1`, 1.4 GB), Unity crash dumps, Python caches, `.DS_Store` | 1.4 GB | All already ignored by git. |
| 7 local branches already fully merged into `final-build`: `all-design-mockups`, `codex/full-visual-overhaul`, `feature/standard-characters-integration`, `final-characters`, `newmapsandmenus`, `tennis-gameplay`, `tennisgameplaydone-needtofixcharacters` | — | Deleting them only removes labels; no work is lost. |
| `stash@{0}` | — | 4 scripts that now exist in newer versions. |

### Safe after a small fix

| What | The fix |
|---|---|
| 6 small body-sculpting folders: `female-micro-f1-head`, `male-form`, `male-form-d`, `male-jaw-5`, `male-nape-pelvis-skin`, `male-silhouette` | Old proof tools still read them: the `Tools/HeroBase` Micro1 scripts, `HeroBaseMaleFormProof.cs`, `HeroBaseFemaleHeadProof.cs` and `HeroBaseSkinProof.cs`. Retire those tools first (sections 4 and 8). |

### Your call

| What | Size | The question |
|---|---|---|
| 21 history folders. Examples: `golf-tailoring` (awaiting your review), `male-pelvis-form3` (staged, waiting for your OK), `female-full-merge` (basis of the female hero), `reference-rebuild` (downloaded assets and their licences), `phone-build-20261006` (a reusable phone-build recipe), `character-shader`, `new-menus` (an earlier dead-code plan that was never applied) | about 32 GB beyond the safe parts | Mostly records of how current things were made. **`character-shader/detail/*.py` is the only generator of the shipped HeroDetail textures**, so move it into `Tools/` if you delete the folder. |
| `proof/full-visual-overhaul/` (14 GB) and `proof/reference-rebuild/` (6 GB) | 20 GB | Evidence for the Oct 7–8 visual overhaul. Keep the gate files and the images the docs cite; the film frames and giant data dumps can go. |
| The `main-build` checkout (`…/2026-10-08/open-8/work/main-build`) | 8 GB | Its online-crash fixes are now committed on branch `local/2026-10-10-online-crash-fixes`. Only its phone-signing settings exist nowhere else. Keep it until that branch is pushed and the signing settings are copied. |
| About 8 GB inside `.git` from an abandoned Sept 28 commit (raw videos) | 8 GB | Only git's undo history (the reflog) still points at it. Git drops it automatically after about Oct 28, or you can reclaim it now with one command. |
| Old versions in git's large-file cache that only history uses | 2.9 GB | `git lfs prune`. |
| `stash@{1}` and the backup ref `refs/backups/pre-dead-code-20261003` | — | Mostly already in `final-build`. Have someone look at 5 files before dropping them. |

### Keep

- **Only copies of the sources of things that ship.** `match-anim-set` (all the tennis animation clips), `serve-dip-jump` (the installed Serve), `emote-fluid` (the approved emotes), `emote-overhaul`, `emotes`, `serve-and-feet` (walk/run clips), `cloth-overhaul/export` (the tennis outfit), `classic-tennis-racket`, `hero-mainstay` (clip table), and `postcard-refresh/split-source/ReferenceHole09.blend` (source of a shipped golf hole; it belongs in `ArtDir/golf/source/`).
- **Read by live tests and tools.** `locker-mirror/data` and `online-lobby/emote_parity/data` hold comparison data for `LockerMirrorTests`. These files aren't in git, so that test can never pass on CI; move the 4 JSON files into `GolfArcadeTests/`. Also `hero-dressed` (outfit pose data), `full-visual-overhaul` (hand data a probe reads), `lobby-parity` (the documented re-export procedure) and `postcard-look` (baselines plus the shared lock).
- **Active right now:** `golf-party`, `character-finish32` and `character-female33` (the clothing pass), `menu-hub` (hub install tools), `proof/menu-hub`, `proof/golf-hole-flow`, `proof/menu-beta` (tracked).
- **Branches that exist only on this Mac (push them):**
  - `local/2026-10-10-final-build-work`: today's Plaza hub, golf three-courses and lobby-hero commits.
  - `local/2026-10-10-online-crash-fixes`: the online-tennis crash fixes.
  - `integrate/main-2026-10-10`: 17 commits.

---

## The surprising "keeps": things that look dead but would break something

- **`blender/tennis-clips.json`.** Unity re-reads it every time it imports the hidden tennis stand-in. Without it, the stand-in's animations import broken.
- **`Unity/Assets/Resources/Golfer/golfer_m.fbx` + `golfer_m_clips.json`.** The game only uses them as a fallback, but the golfer build script (`matchhero_golf.py`) reads them as input. Move them; don't delete them.
- **`Characters/HeroBase/Materials/Male_*` and `FemaleHeadF1/Materials/Female_*`.** The current-hero builder errors without them.
- **`Characters/HeroBase/Rendering/HeroBaseStudioURP.asset`.** The golf look checks fail without it.
- **`Unity/Assets/Tests/Fixtures/StandardCharacters/`.** A normal test needs these models.
- **`SportsLibrary/Blender/sports-animation-studio-v4.blend`, `Arenas/Tennis/tropical-tennis-resort-v3.blend`, `umpire.glb`, `island.glb`.** The only sources of the hidden tennis stand-in, the resort arena, the umpire and the island.
- **`ArtDir/score80/`.** A normal Unity test fails if this folder is gone, until the test is fixed.
- **`ArtDir/hero/v5_proof/LIGHTING.md`.** Code comments cite it as the lighting recipe.
- **The 6 polo/skirt `.ktx` textures.** The app crashes on purpose if they're missing when switched back on.
- **`GolfArcade.xcodeproj` *and* `GolfArcadeUnity.xcodeproj`.** Not duplicates: one is for the simulator and tests (no Unity), the other is the real device app.
- **`work/postcard-look/heavy.sh` + `heavy.lock`.** The lock other AI sessions use to take turns running Unity and Xcode.
- **The 11 Unity importers** (`*Importer.cs`, `MatchHeroImport`, `MatchHeroKit`, …). Removing one changes how its folder imports.
- **`GameCapture.cs`.** It sits in the game code, but it's a screenshot helper the tests need there.

## Suggested cleanup order

1. **Protect** (above): push the local-only branches; commit the hub files and re-run `xcodegen` for both specs; copy the `work/` dependencies into the repo; repoint `ServeAndFeetWire`/`EmoteWire`; pick the branch you're keeping.
2. **Free disk space.** This can't affect the game.
   - Delete the safe `work/`/`proof/` folders and throwaway parts (section 10). Detach the disk image first.
   - Remove the 2 unused extra checkouts and the Blender backups.
   - Optionally clear the Unity cache with the editor closed. The next open re-imports for hours.
3. **Shrink the app:**
   - Take Hero01 out of the "always ship" folder (87 MB).
   - Delete the other never-loaded Resources (section 5).
   - Stop bundling the unused app files (section 3), then run `xcodegen` for both specs.
   - Remove the unused Unity packages.
   - Re-export Unity, which is needed anyway for the Hub.
4. **Delete dead code together with its tests, one batch at a time:**
   - (a) the old camera golf game + 14 test files + 2 UI tests + the CI test list;
   - (b) the old heroes: code, 22 developer tools, asset folders and scenes, all as one set;
   - (c) the small dead pieces inside live files;
   - (d) the "your call" items you decide against.
   
   Fix `MotionClubReviewTests` in the first batch so the unit tests build again.
5. **Stop tests writing into ArtDir**, then delete the Hero01-era art (section 7) and the SportsLibrary dead weight (section 8).
6. **Docs:** fix `AGENTS.md`, `README.md` and `CONTRIBUTING.md`; delete the finished plans and the vgpu skills (section 9).
7. **Optional: shrink git itself.**
   - Expire the reflog for the abandoned Sept 28 commit (~8 GB, or wait until ~Oct 28, when git drops it on its own).
   - `git lfs prune` (~3 GB).
   - Rewrite history to make the GitHub repo smaller. This affects everyone who has cloned the repo, so agree it with the repo owner first.

## Where the evidence is

`details/` holds the full report for each area, with every file, line number and check:

| File | Area |
|---|---|
| `details/swift-app.md` | Section 1: Swift app code (SWIFT-01…29) |
| `details/tests-ci-server.md` | Section 2: tests, CI, Xcode projects, server (TEST-01…26) |
| `details/app-resources.md` | Section 3: files packed into the app (RES-01…27) |
| `details/unity-code.md` | Section 4: Unity code, developer tools, Unity tests (UCODE-01…38) |
| `details/unity-resources.md` | Section 5: Unity's "always ship" folder (URES-01…50) |
| `details/unity-assets-other.md` | Section 6: other Unity assets, packages, caches (UASSET-01…38) |
| `details/artdir.md` | Section 7: ArtDir (ART-01…72) |
| `details/pipelines.md` | Section 8: Blender, SportsLibrary, Tools/HeroBase (PIPE-01…44, plus a verdict for each of the 140 Blender scripts) |
| `details/docs.md` | Section 9: docs and AI instructions (DOC-01…72, plus the exact stale passages to edit) |
| `details/scratch.md` | Section 10: `work/`, `proof/`, git worktrees, branches, stashes (a 101-row folder table + SCR-01…37) |

The detail files mention helper data under a temporary `scratchpad/audit/…` folder. That folder lived outside the repo and is temporary.
