# Multiplayer plan — gate results

Plan: `PLAN_Multiplayer_OnlineLocal.md`. Format follows `proof/menu-beta/GATE_RESULTS.md`.
Tiers: **S** sandbox (run here), **M** needs a Mac (Xcode/Unity), **D** needs real phones.
A gate is PASS only with its evidence. Gates that need a Mac or phones are listed as NOT RUN until someone runs them.

## Phase 0 — Safety net

GATE: G0.1 Sandbox harness runs the game's real multiplayer rule tests — PASS (S). `dotnet test Tools/netsim/NetSim.csproj`: 33 passed, 0 failed (the real `MultiplayerRulesTests`: tennis host rules, golf host rules, serialization round trips).
GATE: G0.5 Syntax check for files that cannot compile here — PASS (S). `python3 Tools/check-syntax.py` (tree-sitter, C# + Swift) passes every changed file and fails a deliberately broken one. It checks syntax only, not types.
GATE: G0.3 Latency experiment runs before-vs-after from git history and the working tree — PASS (S). `proof/multiplayer/latency_experiment/run.sh` builds the sources of commit 9a305ef7 (BEFORE) and the working tree (AFTER) and prints the tables in Appendix A of the plan.
GATE: G0.2 CI runs the sandbox tests on every pull request and push to `main`/`testing` — NOT RUN (CI). `.github/workflows/netcode.yml` (YAML validated here) runs `dotnet test Tools/netsim/NetSim.csproj` on ubuntu in about a minute. It only runs once a pull request or a push to `main`/`testing` happens; no pull request has been opened from this branch (none was asked for).

## Phase 1 — Make multiplayer tennis playable and fair

### P1.2 Screen (TV) delay credited to online swings

GATE: G1.2 Online swings are judged as the player saw them — PASS (S). `dotnet test Tools/netsim/NetSim.csproj`: 49 passed, 0 failed (the game's 33 original rule tests plus `NetworkTimingTests` and `NetworkTennisPointTests`). Perfectly timed returns grade PERFECT for screen delay + network time up to 0.30 s (the test grid), against the real two-message sequence (swing start, then confirmation). Without the credit the same swing grades below PERFECT at 200 ms of screen delay.
GATE: G1.2b Claimed screen delay cannot be abused — PASS (S). The phone credits at most 0.30 s (`NetworkTuning.MaxDisplayCredit`); a 600 ms claim returns the same grade as a 300 ms claim; ages above 0.5 s are rejected by `NetworkInput.Valid`.
GATE: G1.2c Changed Unity-only file parses — PASS (S, syntax only). `SportsMultiplayer.cs` (the phone-side credit) passes the syntax check. NOT RUN (M): it needs the Unity editor/`Unity/Tools/check.sh` to compile and a device to see the effect (G1.2d, D).

Two things the work found that the plan did not list, both fixed with tests:

- **F17 — an unreturned ball gave the point to the wrong player.** In the host rules a ball that landed legally and then left the court untouched ended with `Point(receiver)`: an unreturned serve (an ace) awarded the point to the player who failed to return it. Tests `AnUnreturnedServeThatLandedLegallyIsAnAceForTheServer` and `AnUnreturnedRallyBallThatLandedInCourtWinsThePointForTheHitter` failed before the fix and pass after. A ball that leaves without ever landing is still the hitter's fault (`ABallThatNeverLandedIsTheHittersFault`).
- **F18 — the point could end before a delayed swing arrived.** An unreturned serve stays alive only ~190 ms after it passes the receiver. The host returns the ball only when a swing is *confirmed*, so crediting the screen delay alone could not help once screen delay + network time passed ~0.16 s. The host now holds a dead ball's point open for at most `NetworkTuning.PendingSwingHold` (0.35 s) **only if** the receiver has started a swing that is not yet confirmed. No swing started: no delay (`WithNoSwingStartedThePointIsNotDelayed`). Aborted or already-missed swings do not hold the point.

### P1.3 Steadier shared clock

GATE: G1.3 Filtered clock stays within 20 ms where the latest-sample clock does not — PASS (S). `dotnet test Tools/netsim/NetSim.csproj` (54 passed). `ClockFilterModelTests`: 40,000 evaluated points over 1,000 seeded trials with jitter and one-way spikes of 50–150 ms on 10% of pings. Latest-sample error > 50 ms: 4.94% of points (today). Filtered error > 20 ms: 0.028% of points, worst 27.2 ms. The 20 ms bar is met for 99.9%+ of points, not literally 100%; the worst case is under 30 ms.
GATE: G1.3b Swift port matches the model — NOT RUN (M). `testClockFilterMatchesTheSharedVector`, `testClockFilterIgnoresBadSamples` and `testASlowPongDoesNotMoveTheSharedClock` (GolfArcadeTests/MultiplayerTests.swift) pass the syntax check here; they need Xcode or CI to run. The same 18-sample vector is asserted by the C# model test.
GATE: G1.3c Rejected timestamps are counted — PASS (S). `NetworkTennisMatch.StampRejects` increments for impossible timestamps and not for sane ones (`ImpossibleTimestampsAreCountedSoASkewedClockShowsUp`). The host still rejects them (the game's `ImpossibleTimestampsAreRejected` test is unchanged).
Note: new Swift code lives in existing files (`MultiplayerModels.swift`, `MultiplayerService.swift`, `MultiplayerTests.swift`) because both Xcode projects list every Swift file explicitly; a new file needs the projects regenerated with XcodeGen on a Mac.

### P1.4 Heartbeats and pausing when a player goes quiet

GATE: G1.4 Quiet competitor pauses play and resumes — NOT RUN (M). Four XCTests were added to `GolfArcadeTests/MultiplayerTests.swift` (`testAQuietCompetitorPausesPlayWithinHalfASecondAndResumesWhenHeardAgain`, `testACompetitorQuietForTooLongIsDropped`, `testOccasionalLostPacketsDoNotPausePlay`, `testASpectatorGoingQuietNeverPausesPlay`). They pass the syntax check; they need Xcode or CI to run. The seeded loss pattern in the third was verified offline: 21 of 600 steps lost, longest run 2, and the owner only pauses after 3 lost steps in a row.
GATE: G1.4b Existing Swift tests unaffected — NOT RUN (M); read-through only. The one existing test that jumps the clock (`testCompetitorRecoveryAndExpiry`) already has the player marked disconnected, which the new check skips.
GATE: G1.4c Changed Unity-only files parse — PASS (S, syntax only): `SportsMultiplayer.cs`, `TennisGame.Network.cs`, `GolfGame.Network.cs`, `NetworkTuning.cs`.

### P1.5-1.7 Keep awake, local statistics, safe handling of big unreliable messages

GATE: G1.5 Phone stays awake during a sports session — NOT RUN (M). `testThePhoneStaysAwakeWhileASportsSessionIsActive` added to `GolfArcadeTests/MultiplayerTests.swift` (syntax-checked). Before this, only the golf scene's Unity code kept the phone awake (F10).
GATE: G1.6 Link statistics are summarized correctly — NOT RUN (M). `testNetStatsSummarizeRoundTripsAndLoss` (median 45 ms, p95 200 ms, 20% loss from a fixed list). The Unity line (`stats`) is emitted every 10 s while running; `SportsMultiplayer.cs` passes the syntax check.
GATE: G1.7 A refused big unreliable message is resent reliably once and remembered; a refused small one is not — NOT RUN (M). Two XCTests using a test link that refuses unreliable messages over a size limit. Syntax-checked.
GATE: G1.7b The start-of-match "Reconnecting…" flash is avoided — PASS (S, reading + syntax). `lastSnapshot` is reset when play starts, otherwise the guest's loading time would look like a 0.4 s gap in the host's updates.

### P1.1 Calibration before play (tennis)

GATE: G1.1 A tennis match cannot start until every competitor has set up its controller — NOT RUN (M). Nine new XCTests in `GolfArcadeTests/MultiplayerTests.swift` (`testTennisWaitsForBothPlayersToSetUpTheirControllersBeforeRunning`, `testSpectatorsNeverHoldUpTheStart`, `testGolfGoesStraightToPlayingAndIgnoresCalibration`, `testCalibratedFromAnObserverOrAnOldMatchIsIgnored`, `testAReconnectingPlayerDuringSetupReloadsAndSetsUpAgainBeforeTheMatchStarts`, `testAReloadedTennisPlayerStaysPausedUntilItHasPointedAtTheTVAgain`, `testAReloadedTennisPlayerGetsLongerThanADroppedConnectionToSetUpAgain`, `testSetupIsAbandonedWhenAPlayerLeavesDuringIt`, `testSetupThatDragsOnIsMentionedToEveryoneButNothingIsForced`). They need Xcode or CI to run. I traced each one by hand through the code and found and fixed one mistake in a test before finishing (a forged packet with a high sequence number would have made the owner ignore the real one that followed). Five existing tests and the shared `start` helper were updated for the extra phase and the version bump.
GATE: G1.1b Changed Swift and C# files parse — PASS (S, syntax only). `python3 Tools/check-syntax.py`: 9 files checked, 0 with errors (`MultiplayerModels.swift`, `MultiplayerService.swift`, `SportsSession.swift`, `SportsMotion.swift`, `TennisPlayViews.swift`, `IslandScreens.swift`, `TennisMenu.swift`, `MultiplayerTests.swift`, `MultiplayerProtocol.cs`). Types are not checked; a real compile needs Xcode.
GATE: G1.1c Both protocol version constants moved together — PASS (S). Swift `MultiplayerPacket.version` and C# `NetworkPacket.Version` are both 4 (Unity drops packets whose version differs from its own, so they must match). `dotnet test Tools/netsim/NetSim.csproj`: 54 passed, 0 failed after the change.
GATE: G1.1d After calibration a motion swing registers in a Nearby match — NOT RUN (D). Needs two phones. Without a locked court direction every motion sample is marked invalid and Unity drops it (finding F1), so this is the check that proves the fix on a device.
GATE: G1.1e The TV shows something sensible while players set up — NOT RUN (D). Unity stays paused until the phone unpauses it, so the TV should show the court with the existing "PAUSED — tap Ready on your phone" line. Unverified.

## Phase 2 — Roles for one-TV tennis

### P2.1–2.4 Lobby, protocol and roles

GATE: G2.1 The lobby assigns who shows the match from who has a TV — NOT RUN (M). Nine new XCTests in `GolfArcadeTests/MultiplayerTests.swift` (`testOneTVOnTheOwnersPhoneMakesItSplitAndTheGuestAController`, `testOneTVOnTheGuestsPhoneMakesTheOwnerAHeadlessHost`, `testTwoTVsKeepEachPhoneOnItsOwnCourt`, `testNoTVAnywhereBlocksTennisWithAClearReason`, `testPluggingInOrLosingATVReevaluatesTheLobby`, `testGolfNeedsNoScreenAndHasNoViews`, `testViewsAreClearedBetweenMatchesAndChosenAgainFromTheTVsThen`, `testAPeerCannotClaimAViewOrASetupStateInItsHello`, `testViewsMustBeConsistentInALobby`). Syntax-checked, traced by hand against the code, not run. The shared test helper `party(_:screens:)` gives every phone a TV by default, so the other tests keep describing a two-TV match.
GATE: G2.2 Unity and Swift read the same configuration file — PASS for Unity's side (S), NOT RUN for Swift's (M). `NetworkViewTests` (4 tests: every combination the owner can produce is valid; contradictory ones are rejected; a controller knows it is one; the Swift-style fixture parses and is valid) pass in the sandbox: 58 passed, 0 failed in `dotnet test Tools/netsim/NetSim.csproj`. `testTheOneTVConfigurationFixtureParsesAsTheOwnerWritesIt` reads the same `proof/multiplayer/fixtures/tennis_one_tv_config.json` and also asserts the field names Swift writes equal the file's; it needs Xcode.
GATE: G2.3 A controller-only tennis phone reaches Ready without drawing — NOT RUN (M, PlayMode). Not built yet: a controller-only tennis phone currently takes the same path as a golf guest (the game loads under the controller and does draw). Making it stop drawing is P3a/P3e and needs a device to judge battery and heat.
GATE: G2.4 Every `displayConnected` use is classified — PASS (S, by reading). 55 uses in 8 files: producers of the flag (`SportsDisplays` 28, 278, 283; now also tells the lobby); onboarding and menu code about the phone's own TV (`OnboardingConnectScreen`, `TennisMenu`, `SportsHome` menus, `ClubScreens:410`: screen-only, unchanged); the paths a controller-only phone takes in a match (`SportsSession` 366 `runtimeExternalDisplay=false`, 514/522 phone controls, 518/528 already `|| multiplayerControllerOnly`, 703/912 loading cover, `GolfController` already handles it: either, unchanged); and two that still assume a TV and are P4 work: `loadingFinished` applies the remembered TV delay only `if displayConnected` (:707, the controller-only phone should take the screen phone's measured value, P4.1), and `startTimingCheck` needs the TV (:554, the shared-TV timing check, P4.3).
GATE: G2.5 Changed files parse — PASS (S, syntax only): `python3 Tools/check-syntax.py` (7 files).

## Phase 3 — One TV for two phones

### P3a Shared view

GATE: G3a.1 Which end is drawn in front is decided as before for own-TV phones and spectators, and from seat 0's end for a shared TV and for a controller — PASS (S). `NetworkViewTests.TheNearEndIsTheOwnPlayersOnlyOnAPhoneWithItsOwnTV` (7 cases); `dotnet test Tools/netsim/NetSim.csproj`: 59 passed, 0 failed.
GATE: G3a.2 The changed Unity files parse — PASS (S, syntax only): `TennisGame.Network.cs`, `NativeSportsSession.cs`, `MultiplayerProtocol.cs`. NOT RUN (M): they need the Unity editor (`Unity/Tools/check.sh`) to compile; the edits are in dense single-line style matching the surrounding code and were re-read against the old behaviour for the own-TV and spectator cases (the code paths those use compute the same values as before).
GATE: G3a.3 On a real shared TV both players can serve, receive and see each other — NOT RUN (D). Needs two phones and one TV: check that the far player's serve and receive prompts arrive on its controller, the toss meter shows under the server for both players, the phone with no TV does not get hot, and its screen still shows the racket controller.

## Phase 4 — Both players on one TV

### P4.1 Shared TV delay

GATE: G4.1 The phone with the TV shares its delay and the controller-only phone applies it — NOT RUN (M). `testTheSharedTVsDelayReachesTheLobbyAndStaysWithinWhatTheGameWouldCredit` (lobby carries it, clamps 0–1 s, ignores NaN, ignores a phone with no TV, cleared between matches) and `testAbsurdScreenDelaysAreRejectedInALobby`; the apply step (`SportsSession.poll`) is syntax-checked only. The checked-in configuration fixture now includes the field, which the Swift test compares with what Swift writes; Unity ignores it (the value reaches Unity as the existing `latency` command).
GATE: G4.2 TV status badges for both phones — NOT BUILT. Needs the split screen (P3b) so there is a half to put the badge in; the phones themselves already say who they are waiting for.
GATE: G4.3 Swing-timing check for both players at once — NOT BUILT (milestone C2). Deferred behind the split screen; until then multiplayer uses the remembered timing for the TV, and the game refines timing during play as it does in solo.

## Phase 5 — Simple local play

GATE: G5.1 The menu routes are as designed — NOT RUN (M). `MenuBetaTests.testPlayRoutesAndBackPaths` (Play = Single Player, Local, Online, Back) and the new `testLocalPlayIsTwoTapsFromPlayAndGolfOnlyAsksHowManyAreSharingThePhone`. Syntax-checked, traced against `TennisMenu.rows/select/back`; every exhaustive `switch` over the menu screen (`rows`, `back`, the TV view, the remote title) has the new `localPlayers` case.
GATE: G5.2 Local Network denial is recognised and offered a way out — NOT RUN (M). `testOnlyTheLocalNetworkDeniedErrorIsRecognisedAsSuch`, `testADeniedLocalNetworkIsRememberedSoTheMenuCanOfferSettingsAndForgottenOnLeaving`. The Open Settings button and the iOS error code are unverified (D).
GATE: G5.3 UI test follows the new route — NOT RUN (M). `GolfArcadeUITests/MenuBetaUITests` updated: Play → Local → (golf, tennis rows present) → Join a Friend → Host a Lobby.
GATE: G5.4 Pass-the-phone starts at once; two-phone tennis starts when the friend joins; the hand-off card shows — NOT RUN (D). Needs real phones: also check that with no TV the pass-the-phone flow stops in the lobby with the AirPlay hint, that a tennis lobby does not auto-start without a TV, and that a joiner is readied by joining.
GATE: G5.5 Changed Swift files parse — PASS (S, syntax only): `python3 Tools/check-syntax.py`, 13 files.

### P3b–3d Split screen

GATE: G3.1 Every shape of half keeps the whole court and both players in view — PASS (S). `SplitScreenFramingTests` (4 tests; with the rest `dotnet test Tools/netsim/NetSim.csproj`: 63 passed, 0 failed): for half shapes from 0.60 to 1.20 (width over height; real ones are 0.80 for a 16:10 screen, 0.89 for 16:9, 1.0 for a square window) and both seats, the four court corners with alleys, the room behind both baselines and both players' heads at the widest they run are all inside the picture with the 4% margin; the far player's camera is the near one turned half way round (same picture, checked point by point); camera height is within 7–11 m; the singles court's near edge spans over half the width and the court over 30% of the height. Printed: 16:9 half → 11 m high, 8 m behind the baseline, fov 54°, court fills 26% of the half; 16:10 half → 11 m, 8.5 m, fov 58°, 24%; square → 11 m, 7.5 m, fov 50°, 30%.
GATE: G3.2 The two halves tile the display exactly — NOT RUN (M). By construction (`Rect(0,0,.5,1)` and `Rect(.5,0,.5,1)`, each with aspect = display aspect / 2); needs an EditMode test in Unity.
GATE: G3.3 Split screen renders correctly — NOT RUN (M/D). Needs the Unity editor or a device: screenshots of both servers, a rally, a point, each player visible in their half, names and scoreboard readable, crowd present in both halves (I fixed the grandstand crowd's camera-only culling for this), no blank half.
GATE: G3.4 Frame rate on a real TV over AirPlay — NOT RUN (D). Target: average ≥ 55 fps and 1% low ≥ 45 over five minutes; shadows are drawn per camera.
GATE: G3.5 Changed Unity files parse — PASS (S, syntax only): `TennisGame.Split.cs` (new), `TennisGame.Network.cs`, `TennisSplitCamera.cs`, `NativeSportsSession.cs`, `TennisStandsCrowd.cs`, `TennisResortCrowd.cs`. NOT RUN (M): Unity compile (the new camera copy code uses the URP camera-data properties `renderPostProcessing`, `antialiasing`, `renderShadows`, `volumeLayerMask`, `requiresDepthTexture`, `requiresColorTexture`, `stopNaN`, `dithering`; if the project's URP version lacks one, delete that line).

## Phase 6 — Connection check before a match

GATE: G6.1 Tennis starts only on a steady connection and offers Play anyway when it is weak — NOT RUN (M). Five service tests with a delaying, lossy test bus (`testAGoodConnectionStartsOnceItHasBeenSteadyForAWhile`, `testAWeakConnectionWaitsThenOffersPlayAnywayToTheOwnerOnly`, `testALossyConnectionIsNotSteadyEvenWhenTheRoundTripsAreFast`, `testWithoutAGuestReportingTheOwnerDoesNotCallItSteady`, `testTheConnectionCheckNeverHoldsUpGolfOrAMatchThatIsAlreadyRunning`) and two pure tests of the grading (`testLinkWindowGradesByRoundTripJitterAndLoss`, `testLinkWindowCountsAPingNobodyAnsweredAsLostAndIgnoresStrangers`). Syntax-checked; the timelines the first four assume were checked against a Python model of the same protocol (good link: start 3.1 s after setup; 300 ms round trip or 25% loss: never, decision at 8.5 s; quiet guest: back to steady 4.3 s after it returns). Not compiled or run.
GATE: G6.2 Thresholds are right in the field — NOT RUN (D). Good ≤ 80 ms / 30 ms jitter / 2% loss and Fair ≤ 150 ms / 60 ms / 5% are guesses (all in `MultiplayerTuning`); the first field test should log `rtt median` from `SportsDiagnostics.log` for wired, Wi-Fi and cellular pairs and compare.
GATE: G6.3 Changed Swift files parse — PASS (S, syntax only): `python3 Tools/check-syntax.py`.

