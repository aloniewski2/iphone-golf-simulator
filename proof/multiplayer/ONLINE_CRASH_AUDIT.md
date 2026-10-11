# Online tennis "crashes" — audit and fix (2026-10-10)

Report: when playing online, the tennis game crashes; suspected cause: calibration.
Tree audited and changed: this worktree (`main`, 9442701e). **Nothing is committed.**

## Bottom line

* **No hard crash was found or reproduced.** I could inspect **one of the two phones** (the paired iPhone 17 Pro Max, last used 2026-10-09 21:20). It holds no crash report (`.ips`) for the app among its 60 diagnostic files (2026-10-09 18:00 onwards); `errors.log` has three `start:` lines and no error lines; and six independent read-throughs of the online tennis path found no trap or unhandled exception. The three `JetsamEvent` reports (18:22, 18:43, 19:47) show the app (`GolfArcade`) was **never killed while in the foreground**: it was frontmost and healthy at 18:22 (125 MB), at 18:43 it was suspended but was the largest process on the phone (1.5 GB resident, 2.6 GB peak), and at 19:47 a suspended instance was reclaimed by iOS (`long-idle-exit`), which a player experiences as the app having restarted. The other player's phone is unexamined.
* What a player sees as a "crash" is, on the evidence I could run, the match **ending, freezing or throwing the phone back to a menu**. I found several ways that happens; two were reproduced by running the real code.
* **Calibration is part of it, but it is not the most reliable cause.** Online calibration (TV scan, then Ready) has a dead end (finding 2) and a 45 s clock that kills the match if the phone is paused while waiting (observation O2). The cause that needs no unusual action from the player is the Swift→Unity message queue filling while a guest waits between matches (finding 1).
* **I cannot say which of these you hit.** That needs logs from both phones after a failure; the fix adds the log lines that will say so (section "Confirming on the phones").

## Which code the phone runs

The device log (`SportsDiagnostics.log`, pulled 2026-10-10) contains `…quiet pauses 4 (resumed 4, dropped 0), largest packet 14340 bytes, unreliable refused 0`. That string exists in `main` and **not** in `final-build` (`git grep` on both). So the phone was running the `main` line when that log was written (its last launch, 2026-10-09 21:12), and this is the tree that was fixed. **That log is from a golf session** (the only sport it names is golf; it has no tennis lines), so it says nothing about the tennis failure itself.

`final-build` (14a43331) and `main` (9442701e) share one repository and have **diverged**: 126 commits only on `final-build`, 50 only on `main`, merge base ff612b58. This patch will not apply to `final-build` as is; it needs a merge or a hand port, and that is your call (finding 8 is `final-build`-only).

## Findings

Evidence classes: **PROVEN** = reproduced by running the real code; **CODE** = read and traced in the source, not executed end to end.

| # | What | Evidence | Status |
|---|------|----------|--------|
| 1 | **A waiting guest fills the Swift→Unity queue, then cannot join the next match.** Every `pong` made `MultiplayerService` push a *reliable* `clock` packet into the native queue (128 slots; only `"reliable":false` entries can be evicted; `SportsClear` is never called; Unity drains it only while a match is active). Idle in a lobby or on the results screen at about two pongs a second and the queue is full after about a minute; the next match's `run` is refused, the native side raises `bridgeError`, and the guest ejects itself. Precondition: Unity must already have been loaded once in that app launch (`SportsRuntime.pushNetwork` returns false before that), so the first match after launch is unaffected; a later match fails if the guest sat in the lobby or on the results screen for roughly a minute or more first. | **PROVEN** two ways. Real `SportsBridge.mm` compiled verbatim: push #129 refused at 64.0 s, `bridgeError` raised, `run` refused (`online_crash_audit/bridge_queue_driver.cpp`). Real `MultiplayerService` against a model of that queue: on the unfixed code 180 pushes refused over a 3-minute wait, queue at 128, `lastError` = "The Unity multiplayer bridge is unavailable or full. Re-export Unity before playing." | Fixed: no `clock` is sent to Unity unless a match is configured, and in a match it goes as unreliable so it can be evicted. |
| 2 | **Online calibration dead end.** "Use the direction I'm pointing now" and the pause-menu "Re-check swing timing" end in `offerTimingCalibration()`, which started the offline swing-timing check. Online that check can never finish: `TennisGame.Update` returns at `if (NetworkFrame()) return;` (TennisGame.cs:985) before `TickTimingCheck()` (:1014), so no result ever comes back. The phone sits on "Swing on every bounce!" with no ball, and the pause it sends starts the owner's 15 s forfeit clock. | **CODE**, plus a test that fails on the old code (`("timing") is not equal to ("ready")`). | Fixed: online goes from the court scan straight to Ready and otherwise does nothing; the pause-menu button is hidden online. |
| 3 | **Score missing from every full update.** `TennisMatch` was not `[Serializable]`, and `JsonUtility` silently omits fields of such types. The compact update carries the score; the full update (the reliable checkpoints, and the whole stream if the compact form's self-test fails) did not, so the guest's scoreboard sat at 0–0. Wrong state, not a crash. | **PROVEN** in a throwaway Unity batch-mode project (real `JsonUtility`). | Fixed (`[System.Serializable]`) plus a reflection test that fails if any type inside the update loses it. |
| 4 | **Pause-menu "Leave" online did not leave the lobby.** It ended only this phone's session; the lobby stayed in `playing` with a phone that still sent heartbeats but had nothing behind it, and the opponent's match froze. | **CODE** | Fixed: online "Leave" runs the lobby's own `net-confirm-leave` (`leave()` → `stopRuntime()` → `SportsSession.end()`). |
| 5 | **Feedback bar at the bottom of the controller during an online match.** It opens Messages; a palm on it mid-swing leaves the app, which pauses play for both players and starts the 15 s forfeit clock for the phone that left (O1). | **CODE** | Fixed: hidden during online matches. |
| 6 | **`Int(Double)` trap in `TennisContact`.** `Double("nan")` and `Double("inf")` parse, and `Int()` of NaN, Infinity or a huge value traps. A badly formatted contact line from Unity would kill the app. | Trap itself **PROVEN** (standalone Swift: exit 133 = SIGTRAP for `nan`, `Infinity`, `1e300`). I have **no evidence** Unity ever sends such a line. | Hardened: non-finite reads as 0, whole numbers are clamped. |
| 7 | **Ready can silently do nothing.** `finishMultiplayerCalibration()` returns without a word when the court direction is not locked or `calibrate()` fails (`guard motion.axisLocked, motion.calibrate() else { return }`); the Ready panel did not show `status`, and the other player waits. | **CODE** only. Whether this happens in practice (for example if ARKit never delivers camera frames to the scan) is **unknown**: the only device log I have is a golf session. | Mitigated: the panel shows `status` online. |
| 8 | **`final-build` has no online calibration at all.** Its online path goes straight to `.playing` with no TV scan and no `calibrate()`, and `SteeringFilter` returns nothing until it is calibrated. | **PROVEN** with the real `SteeringFilter`: a forehand gives 0 swings uncalibrated, 1 calibrated. | Not changed (other branch). |

## Observations, deliberately not changed

* **O1. Pause is coupled to a forfeit.** A phone's `pause` sends `availability=false`; the owner suspends both players and, after 15 s, drops the paused player (`drop` → "interrupted"). The heartbeat goes silent after 0.4 s. By the code, anything that makes the app inactive for 15 s (an incoming call, Control Center or Notification Center pulled down) ends the match for both; I did not exercise this on a phone.
* **O2. Waiting for calibration gets 45 s** (`MultiplayerTuning.recalibrationSeconds`) before the same drop.
* **O3. Mixed builds can break the lobby.** The participant record decodes `linkOK` as a required key. `testTheOneTVConfigurationFixtureParsesAsTheOwnerWritesIt` fails with `keyNotFound: linkOK` against an older fixture; a phone still on an older build would presumably send the same shape (not tested between two builds).
* **O4. Two stale tests fail on the untouched tree** (`testLinkWindowGradesByRoundTripJitterAndLoss`, the `linkOK` fixture above) and `MenuBetaTests.testFeedbackAndBundledGameplay` fails because of the uncommitted `SportsInfo.plist` edit (it passes with the committed plist).
* **O5. `GATE_RESULTS.md` already says** the Swift tests, calibration, link-check thresholds and everything on real phones had never been run. This audit is the first Swift run of that pass.
* Also noted, not fixed: auto-resume after a system blip; the 15 s grace is short; the link gate has no auto-continue for the guest; the relaunch-rejoin `seen` reset; a guest-side grace period when GameKit flaps; if the build is signed with a free personal team, the Game Center capability is not available to it (matchmaking would fail, not crash).

## What changed

Swift: `Multiplayer/MultiplayerService.swift` (clock gating and unreliable clock; four diagnostic lines), `Unity/SportsSession.swift` (online guard in `offerTimingCalibration`; `TennisContact` hardening; DEBUG-only `debugSetOnlineMatch`; 4 s log cadence), `Unity/SportsMotion.swift` (log cap 1 MB; 4 s cadence), `Unity/ClubScreens.swift`, `Unity/SportsHome.swift`, `Unity/TennisPlayViews.swift`.
C#: `Tennis/TennisMatch.cs` (`[Serializable]`).
Tests added: `testAGuestWaitingInTheLobbyDoesNotFillTheUnityBridgeAndStillJoinsTheNextMatch`, `testAnOnlineMatchNeverOffersTimingCalibration`, `testAMalformedContactLineNeverTrapsInTheIntConversion`, and `EveryTypeInsideTheFullUpdateIsSerializableOrJsonUtilityLeavesItOut`.
New log lines (`SportsDiagnostics.log`): `multiplayer launch …`, `multiplayer interrupted: …`, `multiplayer dropped … after N s paused or silent`, `multiplayer bridge error during a match: …`, `multiplayer bridge refused a … packet`.

## Verification (2026-10-10, iPhone 17 simulator, iOS 26.5; Unity C# under .NET 8)

* Build for testing: **succeeded**, 0 errors (one warning, in a line committed by someone else).
* New tests **pass** with the fix. Run against the code with the two behavioural fixes temporarily reverted, the bridge test and the calibration test **fail** with the messages quoted in findings 1 and 2; the fixes were then restored byte for byte (SHA-256 checked) and everything re-run.
* Swift suites run (225 tests): `MultiplayerTests` (minus the one that writes a PNG into the tree), `MenuBetaTests`, `MenuFlowRegressionTests`, `SportsDelayProbeTests`, `SportsIntegrationTests`, `AvatarTests`, `LockerTests`, `OnboardingTests`, `PresentationTests`, `TennisCampaignTests`. **3 failures, all identical to the run on the untouched tree** (O4). No new failures.
* Unity-side rules under .NET (`Tools/netsim`): **73 of 73 pass**, including the new reflection test (it failed without the attribute).
* **Not run:** two real phones and a TV; the Unity player build; the pause-screen, Feedback-bar and Ready-panel changes were compiled but not exercised in a UI test. Findings 1–3 are fixed *in the code paths I could run*; whether they are what you hit is unconfirmed.

## Confirming on the phones

Reproduce with both phones, then from the Mac (phone unlocked and trusted):

```bash
xcrun devicectl device copy from --device 00008150-00041CD92287801C --domain-type appDataContainer --domain-identifier com.adnanandrew.motionsports --source Documents/SportsDiagnostics.log --destination ./owner-SportsDiagnostics.log
```

Repeat for `Documents/errors.log`, and for any `.ips` under `Settings → Privacy & Security → Analytics Data` whose name starts with `GolfArcade`. A hard crash leaves an `.ips`; a match that ends leaves one of the new `multiplayer …` lines just before it stops. Do the same on the second phone.

## Sources

* NWBrowser `PolicyDenied` on the local-network permission: https://developer.apple.com/forums/thread/780655
* `GKMatch.sendDataMode` (reliable vs unreliable delivery): https://developer.apple.com/documentation/gamekit/gkmatch/senddatamode
* Capabilities needing a paid team: https://help.apple.com/xcode/mac/9.3/en.lproj/dev88ff319e7.html
