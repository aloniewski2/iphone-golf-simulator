# PLAN — Multiplayer: fast online play, one-tap local play, two-phone tennis on one TV

**Status:** IN PROGRESS. You told me to proceed on my own, so I am working through the phases in order using the defaults in section 8. Done: P0 (safety net), P1 (fair, playable multiplayer tennis), P2 (roles), P3 (split screen), P4.1 (shared TV delay), P5 (simple local flow), P6 (connection check), P7.3 (compact updates) and P8 (docs, legacy labels, field-test script) and P9 (an independent review of the whole branch, with its fixes). Not done, with the reasons in each phase: P4.2/4.3, P6.3 (the silent Quick Match re-queue), P3e, P7.1/7.2/7.4/7.5. Nothing has been run on a Mac or a phone yet: the first things to do are in the summary of `proof/multiplayer/GATE_RESULTS.md` and the script `proof/multiplayer/FIELD_TEST.md`. Progress and evidence: `proof/multiplayer/GATE_RESULTS.md`. Gates that need a Mac or real phones are listed there as NOT RUN until someone runs them.
**Written:** 2026-10-09, from a read-only audit of the code. No phone, Unity editor or Xcode was available, so every claim below is from reading code, plus one headless experiment (Appendix A) that runs the real host rules.
**Scope:** tennis and golf. Online (Game Center), Nearby (same Wi-Fi), and Pass-the-Phone.
**Not in scope:** characters, clothes, maps, lights, or the existing single-view camera framing. The `AGENTS.md` character-pass limits do not apply to this work, and this plan does not touch those assets.

---

## 1. In plain words

What you asked for:

1. **Online play that stays connected and feels instant**, even for tennis.
2. **Local play in one tap:** Play → Local → how many players → the game loads → pass the phone.
3. **Local tennis:** two phones join one lobby, **only one phone connects to the TV**, the TV shows a **split screen** (one view per player), and **both players can calibrate**.

What the audit found that changes the order of work:

- **Multiplayer tennis skips calibration completely.** Without the "point your phone at the TV" step, the motion code marks every swing invalid, so motion swings probably don't register at all in Online or Nearby tennis. This must be fixed first, and it is the same step that "both can calibrate" needs.
- **The TV's delay is ignored online.** Measured on the real rules (Appendix A): the network is already handled well, but even 50 ms of TV delay drops your best possible serve return from PERFECT to GREAT, 100 ms to GOOD, and at 150 ms you cannot return a serve at all. Solo play already credits the TV delay. Multiplayer did not. *(Fixed in step P1.2, see below.)*
- **An unreturned ball gave the point to the wrong player.** Found while testing: in the host's rules an unreturned serve awarded the point to the player who failed to return it. *(Fixed in P1.2.)*
- **Tennis needs a TV on every phone.** The app refuses to start tennis on a phone without its own screen, so two friends with one TV cannot play. Golf already has "controller-only" phones; tennis does not.

What it will look like when done:

| Flow | Taps from Home | What happens |
|---|---|---|
| Golf, one phone, 2–4 players | Play → Local → "Golf · pass the phone [2][3][4]" = **3** | No lobby screen. Loads straight away. A "Pass to Sam" card appears before each turn. |
| Tennis, two phones, one TV | Host: Play → Local → Host = **3**. Friend: Play → Local → tap host = **4** | Both phones are in one lobby, auto-ready. One phone is on the TV (AirPlay). Each player points their phone at the TV (about 3 s). A connection check runs (about 2 s). 3-2-1, then split screen. |
| Golf, several phones | Unchanged (Nearby) | Same as today, with one-tap join. |
| Online (quick match or friends) | Unchanged entry | Calibration and connection check happen before the match. |

First time on a TV is about 45–60 s including the one-time screen measurement; later matches on the same TV are about 10–15 s.

---

## 2. What the audit found

IDs are used by the phases below. "Experiment" means Appendix A.

| ID | Finding | Evidence | Effect | Fixed in |
|---|---|---|---|---|
| F1 | Calibration is skipped in every multiplayer match. `loadingFinished()` jumps to "playing"; `motion.start` never gets an axis lock or `calibrate()`; tennis samples are `valid = calibrated`; Unity drops invalid samples. | `SportsSession.swift:667-675`, `SportsMotion.swift:158,417,509`, `SportsMultiplayer.cs:169`, `NativeSportsSession.cs:268` | Motion swings probably don't register in Online/Nearby tennis until someone does "Re-aim at the TV" from the pause menu (which pauses both players). Needs a device check. | **P1.1 done** (code and tests; device check G1.1d) |
| F2 | Tennis needs its own TV on every phone. Golf guests can be controller-only; tennis cannot. No split screen exists. | `SportsRuntime.mm:43-45`, `SportsSession.swift:334-336`, `SportsDisplays.swift:53-74`, `MultiplayerModels.swift:145` (golf only), `TennisMenu.swift:614,624` | Two friends, one TV: impossible. | P2, P3 |
| F3 | TV delay was not credited to online swings (solo does credit it). The `latency` value reaches Unity but the network swing age never included it. | `SportsMultiplayer.cs:176`, `TennisGame.cs:516`, experiment | With the real two-message swing: 50 ms TV delay = GREAT at best, 100 ms = GOOD, 150 ms = serve cannot be returned. Players with different TVs were not on equal footing. | **P1.2 done** |
| F4 | Network delay is already absorbed well (good news): perfect hits survive up to ~150 ms one-way. | experiment | Keep the rewind design; widen it only to cover the TV. | — |
| F17 | **(new, found in P1.2)** A ball that landed legally and left the court untouched awarded the point to the receiver, so an unreturned serve gave the point to the player who failed to return it. | `NetworkTennisMatch.AdvanceBall` (out-of-court rule), test `AnUnreturnedServeThatLandedLegallyIsAnAceForTheServer` | Wrong winner on every unreturned in-court ball. | **P1.2 done** |
| F18 | **(new, found in P1.2)** The point can end before a delayed swing arrives: an unreturned serve is dead ~190 ms after it passes the receiver, but the host returns the ball only on the swing *confirmation*. | experiment, `WithNoSwingStartedThePointIsNotDelayed` | Crediting the TV delay alone could not help beyond ~0.16 s of combined delay. | **P1.2 done** |
| F5 | The shared clock comes from one ping per second with no filtering. A skew above about 50 ms plus the network time makes the host drop the swing as "from the future". | `MultiplayerService.swift:395-398,536-538`, `NetworkTennisMatch.cs:57`, experiment | One slow packet and a player's swings vanish for up to a second. | P1 |
| F6 | The host never notices a player going quiet; it only reacts when Apple reports a disconnect. | `MultiplayerService.swift:545-549` (no silence check); client-only `Stale` at `SportsMultiplayer.cs:181` | The opponent keeps scoring while your connection hiccups. | P1 |
| F7 | No connection check before a match. Host is the lowest ID (effectively random). Quick Match ignores distance. | `MultiplayerService.swift:297`, `GameCenterTransport.swift:40-47` | Bad links start matches anyway. | P6 |
| F8 | The app adds its own waiting: 60/s Swift poll timer, 30/s host updates, per-frame Unity poll. Estimate 50–100 ms round trip on top of the network (estimate, not measured). | `MultiplayerService.swift:291`, `SportsMultiplayer.cs:73,84` | Slower feel; also eats the rewind budget. | P7 |
| F9 | Your own hit shows late: the ball only changes direction when the host's update arrives. | `TennisGame.Network.cs:22-25,52-54` | "Ball passes through my racket, then jumps back." | P7 |
| F10 | The tennis scene never asks the phone to stay awake. Golf's Unity scene does (`GolfGame.cs:150`, `NeverSleep`), and Unity keeps that setting for the rest of the app launch, so tennis stays awake only if golf ran earlier in the same launch. The native side sets it only on the old course and practice screens. No Wi-Fi ↔ cellular handling either. | grep: `isIdleTimerDisabled` only in `CourseScreen.swift`, `PracticeRangeScreen.swift`; `sleepTimeout` only in `GolfGame.cs`, `PhoneController.cs` | The phone can lock mid-match (the motion controller is not touched, so iOS sees no activity). Needs a device check. | P1 |
| F11 | Update messages are about 1.7 KB, sent as "unreliable" 30×/s. Apple says unreliable is for "small packets" and gives no number. | estimate (`proof/multiplayer/`), Apple docs | If rejected, online tennis stutters. **Verify on devices first.** | P1.7, P7.3, field |
| F12 | Local Network permission "Don't Allow" leaves the Nearby list silently empty (only `.failed` is handled, not `.waiting`). | `LocalMultiplayerTransport.swift:65-70,90-94` | "Nothing happens" on first run. | P5 |
| F13 | Nearby uses TCP for real-time data; a lost packet stalls everything behind it. | `LocalMultiplayerTransport.swift:44-48` | Rare stalls on flaky Wi-Fi. | field, P5 |
| F14 | Pass-the-phone has no hand-off cue, no player-count step (tap "Add guest" per player). Controller shows the same screen for every guest's turn. | `TennisMenu.swift:1103-1109`, `GolfController.swift:281-293` | Confusing who plays. | P5 |
| F15 | A second, older online system (Node server + Unity lobby, golf only) is still in the repo. The current app does not use it. | `server/`, `Unity/Assets/Scripts/UI/Lobby.cs`, `Net/Online/*` | Confusing; dead weight. | P8 (label only) |
| F16 | Tests cover rules and security well but never simulate a slow, lossy or jittery link. Docs cite `work/multiplayer/` evidence that isn't in the repo. | `MultiplayerTests.swift`, `SportsLibrary/MULTIPLAYER.md` | No safety net for the changes below. | P0 |

---

## 3. Design decisions

### 3.1 Online transport: keep Game Center peer-to-peer
Direct phone-to-phone is the lowest latency and costs nothing to run. A relay server adds a hop and a bill, and only helps when a direct path is impossible (Apple already relays then). **Revisit only if field data shows many matches failing to connect.** Everything below makes the app tolerate and detect bad links; none of it needs a server.

### 3.2 Who does what (roles)
- **Simulation host = lobby owner** (unchanged).
- **Screen phone** = the phone with an AirPlay/wired display. It renders.
- **Controller-only phone** = a phone with no display. Unity runs hidden (as golf guests do today), turns motion into inputs, and its native screen shows the usual racket controller.
- The two roles are independent: the screen phone can be the owner or the guest.

| Screens connected | Result |
|---|---|
| Neither phone | Cannot start; the lobby says "Connect one phone to a TV (AirPlay) to play." |
| One phone (owner or guest) | That phone is `split` (renders both views); the other is `none` (controller-only). |
| Both phones | Each renders its own normal view on its own TV (today's behavior). |
| Golf | Unchanged. |

### 3.3 Latency budget (from the experiment)
A swing is credited back in time by: sensor age + network one-way time + **screen delay**. Today the first two are credited, capped at 0.15 s; the screen delay is not. Proposed:
- `age = sensor + screenDelay` (guest adds its measured screen delay, clamped to 0.30 s).
- Host rewind limit 0.15 s → **0.40 s**. `NetworkInput.Valid` age limit 0.25 → 0.5.
- A dead ball's point is held open for at most 0.35 s, **only** when the receiver has started a swing whose confirmation has not arrived (otherwise a served ball is dead ~190 ms after it passes the receiver, before a delayed swing could be confirmed).
- Result (Appendix A): perfect serve returns survive about **0.30 s of screen delay + one-way network time combined** (for example 150 ms TV + 150 ms network, or 300 ms TV + 20 ms network). Rally balls, which travel slower, have more room.
- **Trade-off:** a larger rewind means the host pulls the ball back in time to judge a hit, so the opponent's screen can show a visible jump. We cap it, blend the displayed ball over about 100 ms (P7), and tune it with field data. The constants live in one place (`NetworkTuning`) so tuning is a one-line change.

### 3.4 Calibration model (what "both can calibrate" means)
Per phone, before play, in a new lobby phase `calibrating` (between `loading` and `playing`):
1. **Court direction scan** (each phone, in parallel): point the back of the phone at the TV and hold still. Reuses `AxisGatePanel`. Touch-control players skip it.
2. **Screen delay** (screen phone only, once per TV): reuse the flash probe; skipped if the TV was measured before. The value is sent to the other phone (`screenDelay`) and applied as its `Lag` prior.
3. **Swing timing check** (each player): milestone C1 uses the saved/learned timing; milestone C2 runs the check for both players at once, each in their own half of the split screen (fallback: one after the other, full screen).
4. **Ready:** each phone calls `calibrate()` (captures neutral). The owner starts the match only when both are calibrated (spectators don't count), plus the link check (P6).
Escape hatches: "Use touch controls", and a 60 s timeout that offers Skip.

### 3.5 Split screen
- Layout: **side by side on the 16:9 TV**, seat 0 left, seat 1 right, a thin divider, name tag on top of each half, one shared scoreboard top-center (existing HUD).
- Each half is about 960×1080 (aspect ≈ 0.89), so the existing portrait branch of `TennisShoulderCamera.Frame` (FOV 49°) is the starting point. The far player's camera uses the same function with the world mirrored 180° around the court center.
- Total pixels equal one full-screen view, but culling and draw calls are about doubled, so P3 adds a quality step-down and a device performance gate.
- Intros, game/set presentations and ultimates switch to one full-screen director camera, then return to split. Replays stay off in multiplayer (as today).
- The existing single-view framing is untouched; split framing exists only in couch mode.
- **Fallback if split looks poor:** one shared broadcast camera for both players (built first in P3a, so there is always a working version).

### 3.6 Protocol v4
- `MultiplayerPacket.version` and `NetworkPacket.Version` 3 → 4 (both phones must run the same build).
- `MultiplayerParticipant`: `display` (has a screen), `screenName`, `screenDelay`, `view` (`near`/`split`/`none`), `calibrated`.
- `MultiplayerPhase`: add `calibrating` (the compiler will flag every `switch` that must handle it).
- New packet kinds: `display`, `calibrated`, `screenDelay`, `hb` (heartbeat, unreliable, tiny).
- Unity `NetworkParticipant.view`; `NetworkConfiguration.Valid` updated. A shared JSON fixture (checked in) is parsed by both Swift and C# tests so the two sides cannot drift.

### 3.7 Guardrails
Do not change characters, clothes, maps, lights, or single-view camera framing. New behavior is behind flags where it affects feel (P7). Golf behavior is unchanged except the P5 menu and hand-off cue. Legacy online code is labeled, not deleted, unless you say so.

---

## 4. How each gate is checked

Every gate is binary: `GATE: <name> — PASS` or `FAIL` with the failed lines, recorded in `proof/multiplayer/GATE_RESULTS.md` (same format as `proof/menu-beta/GATE_RESULTS.md`). No proof, no PASS.

| Tier | Meaning | Who/what runs it |
|---|---|---|
| **S** | Runs in the Linux sandbox | Me, with the .NET 8 SDK (installed and working). Compiles the real C# rules with a small Unity stand-in (`proof/multiplayer/latency_experiment/Shim.cs`). Swift gets a syntax-only parse (tree-sitter). |
| **M** | Needs a Mac | Swift unit tests: `xcodebuild test -project GolfArcade.xcodeproj -scheme GolfArcade -only-testing:GolfArcadeTests -destination 'platform=iOS Simulator,name=iPhone 16 Pro'`, or CI `build-and-test` on a pull request to `testing`. Unity EditMode: `Unity/Tools/check.sh`. Unity PlayMode: `Unity/Tools/run-playmode-windowed.sh <filter>`. |
| **D** | Needs real phones/people | After the app is live; see section 9. |

CI note: `.github/workflows/ios.yml` runs only on pull requests to `main`/`testing`, so Swift compile feedback needs a (draft) pull request; I will ask before opening one. CI has no Unity job, so Unity C# is verified by tier S where the code is pure, and by your Mac otherwise. P0 adds a cheap Ubuntu job for the pure C# tests.

---

## 5. Phases

Relative size: S small, M medium, L large. Order matters; each phase ends with a push and a GATE_RESULTS update.

### P0 — Safety net (S) — tier S/M
- **0.1** `Tools/netsim/`: promote the proof harness to a `dotnet test` project that links (not copies) the real pure sources and the real EditMode multiplayer tests (tennis; golf if the pure golf files compile, else tennis only).
- **0.2** C# `LinkSim`: deterministic virtual-time link (latency, jitter, loss, reorder) used by later tests.
- **0.3 Done (reduced).** The Swift test bus (`MultiplayerTests.Bus/Link`) can now delay and lose messages on the test clock (`oneWayDelay`, seeded `lossRate`, plus the earlier `drop` and `refuseUnreliableOver`); jitter, reordering and duplicates were not needed yet.
- **0.4** `.github/workflows/netcode.yml` (Ubuntu): runs 0.1 on pull requests touching `Unity/Assets/Scripts/{Multiplayer,Tennis}` or `Tools/netsim`.
- **0.5** `Tools/check-swift-syntax.sh`: tree-sitter parse of changed Swift files (sandbox convenience).
- **Gates:** G0.1 harness runs the existing tennis multiplayer EditMode tests, all pass (S). G0.2 `netcode` workflow green (M/CI). G0.3 `latency_experiment/run.sh` reproduces Appendix A (S). G0.4 at least one XCTest uses `ImpairedBus` (M).

### P1 — Make multiplayer tennis playable and fair (L) — highest value
- **1.1 Calibration (milestone C1) — done in code; tests need a Mac, device check outstanding.** Tennis only (golf is unchanged). A new lobby phase `calibrating` sits between `loading` and `playing`:
  - **Owner.** When every competitor has loaded, the lobby moves to `calibrating`. Each competitor's phone reports `calibrated` (new control message, new participant flag); when all competitors are loaded **and** calibrated the owner schedules `run` exactly as before. Spectators never count. Golf skips the phase.
  - **Phone.** The loading cover lifts as soon as everyone is loaded (`allReady` is now true for `calibrating` too). A motion player then sees the existing TV-scan panel (`AxisGatePanel`, "point the back of your phone at the TV"), then the existing Ready panel, which takes the player's centre and sends `calibrated`; then a new "You're ready, waiting for …" panel. A touch player has nothing to set up and reports at once. When the lobby reaches `playing` the phone takes its centre again (the player may have moved while waiting; this second capture is silent so a missed camera frame cannot pause the match) and unpauses.
  - **Reconnect.** A tennis player who drops and comes back reloads the game and must scan and tap Ready again. The owner keeps play paused for that player (also against a plain "back in the foreground" message) until `calibrated` arrives, and allows 45 s for it instead of the 15 s a dropped connection gets. During the first setup a returning player is sent the match again and the lobby simply keeps waiting.
  - **Nobody is trapped.** If a player leaves or is dropped during setup the owner returns everyone to the lobby with a notice. If setup takes more than 60 s everybody is told who is holding things up. Nobody is forced to wait: Leave stays on screen, and the scan panel keeps its two escape hatches ("Use the direction I'm pointing now", "Use touch controls instead").
  - **Protocol v4.** The new phase and flag change what goes over the wire, so both version constants moved together: Swift `MultiplayerPacket.version` and C# `NetworkPacket.Version` are 4 (an old build now says "incompatible" instead of silently ignoring the lobby).
  - **Changed from the first draft.** (1) No timing check yet: the Ready panel is step 2 of 2 for an online match; the screen-delay probe and the swing-timing check wait for P4 (milestone C2). (2) The "Skip" after the 60 s timeout is not an owner button that starts the match without the slow player: that would start a match one player cannot play. The scan panel's own escape hatches are the skip, and Leave is the other way out. (3) Spike result for the loading cover (the risk row below): `LoadingModel` only needs `allReady`; it finishes the cover when everyone has loaded, whatever the lobby phase afterwards is, so no change to `LoadingModel` was needed.
  - **Unverified, to check on a device:** what the TV shows during setup. Unity stays paused until the phone unpauses it, so the TV should show the court with the existing "PAUSED — tap Ready on your phone" line (accurate, but plain). If it looks poor, the fix belongs in P3/P4 (a setup card on the TV).
  - **G1.1** (M): XCTests in `MultiplayerTests.swift` (syntax-checked here, not run): tennis cannot reach `playing` until both report; spectators never hold up the start; golf ignores calibration; a forged or old-match `calibrated` is ignored; a returning player during setup reloads and sets up again; a reloaded player stays paused until calibrated (also against foreground); a reloaded player gets longer than 15 s but not forever; setup is abandoned when a player leaves; a long setup is mentioned to everyone. Five existing tests and the shared `start` helper were updated for the extra phase and the version bump.
  - **G1.1d** (D): after calibration, a motion swing registers in a Nearby match.
- **1.2 Screen-delay credit.** `SportsMultiplayer.Sample` adds `Lag` (≤ 0.30 s) to the swing `age`; `NetworkTuning.MaxSwingRewind = 0.40` (and `MaxDisplayCredit = 0.30`); `NetworkInput.Valid` age ≤ 0.5; history window sized from the constant.
  - **G1.2** (S, **PASS**): `dotnet test Tools/netsim/NetSim.csproj` (49 pass) and the before/after experiment from the actual changed source: PERFECT wherever screen + network ≤ 0.30 s.
  - **G1.2b** (S, **PASS**): all existing tennis `MultiplayerRulesTests` still pass; a phone cannot claim more than 0.30 s.
  - Added during execution: the unreturned-ball winner fix (F17) and the bounded late-swing hold (F18), each with tests that failed before the fix.
- **1.3 Clock sync (done).** Swift `ClockFilter` (in `MultiplayerModels.swift`): keep the last 16 ping/pong samples, take the 3 with the lowest round trip, use their median, and slew at most 5 ms per update (a jump over 100 ms is followed at once). Pings are now unreliable, 5/s during a match and 2/s otherwise. A C# reference model (`Tools/netsim`) is validated by simulation, and a fixed 18-sample vector is asserted by both the C# and the Swift tests so the port cannot drift.
  - **Changed from the first draft:** the host does **not** clamp odd timestamps instead of dropping them. The game's own test `ImpossibleTimestampsAreRejected` encodes that as a deliberate rule, and a steadier clock removes most of the need. The host now **counts** rejected timestamps (`NetworkTennisMatch.StampRejects`) so field data shows whether any remain; revisit then.
  - **G1.3** (S, **PASS**): 40,000 simulated points (1,000 trials, jitter plus one-way spikes of 50–150 ms on 10% of pings): today's "latest sample" clock is off by more than 50 ms **4.94%** of the time; the filtered clock is off by more than 20 ms **0.028%** of the time (worst 27 ms). **G1.3b** (M): the Swift vector and slow-pong tests pass in Xcode/CI.
- **1.4 Heartbeats and silence (done, tests need a Mac).** A competitor's phone sends a tiny unreliable `hb` to the owner 10 times a second during play. The owner pauses everyone (the existing `suspend`) when it has heard no packet of any kind from a competitor for 0.4 s, and resumes (`resumePeer`) after 3 packets; the 15 s expiry is unchanged. Silence is not judged until 1 s after the shared start (a freshly loaded scene can stall the app), spectators and shared-phone guests are never judged, and a guest whose host goes quiet for 0.4 s sees "Reconnecting…" (the 2 s "Connection interrupted" label stays). Constants: `MultiplayerTuning` (Swift) and `NetworkTuning.QuietSeconds` (C#).
  - **G1.4** (M, not run): XCTests in `MultiplayerTests.swift` — a silent link pauses within half a second and resumes when heard again; a 16 s silence drops the player; a seeded 3% packet loss (longest run of lost steps is 2, checked offline) never pauses play; a quiet spectator never pauses play. They use a test link with a `drop` switch (connected, but nothing gets through). Syntax-checked here.
- **1.5 Keep awake (done, test needs a Mac).** `SportsSession.active` now switches `isIdleTimerDisabled` on and off, so tennis and golf both keep the phone awake no matter which scene Unity loaded first. **G1.5** (M): `testThePhoneStaysAwakeWhileASportsSessionIsActive`.
- **1.6 Local statistics (done, test needs a Mac).** Per match, `NetStats` (round-trip median/p95, jitter, ping loss, quiet pauses/resumes/drops, largest packet, refused unreliable sends) and a Unity line every 10 s (rejected timestamps, longest gap in the host's updates) are written to `SportsDiagnostics.log` in the app's Documents folder. Nothing is uploaded. *(The first draft said "attached by Send feedback"; Send feedback is a text message, which cannot carry a file, so the log stays local and is pulled from the phone when tuning.)* **G1.6** (M): `testNetStatsSummarizeRoundTripsAndLoss`.
- **1.7 Safe handling of big unreliable messages (done, tests need a Mac).** If Apple refuses an unreliable message bigger than 900 bytes, that kind of message is sent reliably from then on and the refusal is logged. Smaller messages that fail are not blamed on size. *(The first draft forced every message over 1,000 bytes to reliable up front; that would have turned all tennis snapshots reliable on an unverified assumption, so the fallback only reacts to a real refusal.)* Slimming the snapshot itself moves to P7.3. **G1.7** (M): `testARefusedBigUnreliableSendFallsBackToReliableOnceAndIsRemembered`, `testASmallUnreliableMessageIsNeverBlamedOnSize`.

### P2 — Roles and lobby for one-TV tennis (M) — tier M — lobby and protocol done; Unity camera work continues in P3a
- **2.1 Done.** Each participant now has `hasScreen` (a TV or Mac is connected) and, once a match starts, `view`: `near` (own TV, own court, today's behaviour), `split` (the one TV shows both players) or `controllerOnly` (no screen; sent to Unity as `"none"`). A tennis lobby cannot start unless at least one of the two players' phones has a TV; the Start button explains why ("Connect one phone to a TV (AirPlay) to play"). Golf asks for nothing.
- **2.2 Done.** The owner assigns views when the match starts: two TVs → both `near`; exactly one TV → that phone is `split` and the other `controllerOnly`; spectators and golf get none. Views are cleared when the match ends and chosen again from whoever has a TV then. A lobby with contradictory views (two `split`, a `split` partner with its own screen, views in golf) is rejected by `valid()`; a joining phone cannot claim a view or a setup state in its hello.
- **2.3 Done (lobby text, not a badge).** `SportsSession.displayConnected` changes tell the service (`setScreen` → `display` control message), so plugging in or losing a TV updates everyone's lobby at once. The lobby panel shows one line saying where the TV is ("The TV is connected to Sam's phone. It shows both players."). A per-player TV badge on the 3D stage was not added: it needs the hero-stage layout and is cheap to add once the stage can be seen.
- **2.4 Done.** `MultiplayerMatchConfiguration.controllerOnly` generalizes golf's `usesHostGolfDisplay`: a golf guest, or a tennis phone whose view is `controllerOnly`, loads its game hidden under the controller (the existing golf-guest path). `SportsSession` uses it. The audit of every `displayConnected` use is in `proof/multiplayer/GATE_RESULTS.md` (G2.4): the places a controller-only tennis phone touches already accept `multiplayerControllerOnly`; the ones that still assume a TV (`startingLag`, the timing check) are P4.
- **2.5 Partly done.** Unity reads `view` (`NetworkParticipant.view`, `NetworkConfiguration.LocalView/ViewOf/ViewsValid`, checked by `Valid`). What each view *does* on screen, including a `controllerOnly` phone that does not draw, is P3a.
- **Gates.** **G2.1** (M): nine XCTests cover the case table, including TV on the owner only, on the guest only, on both, on neither, TV plugged/unplugged in the lobby, golf unchanged, views cleared between matches, hello cannot claim a role, and the consistency rules. **G2.2** (S **PASS** for Unity's side / M for Swift's): `NetworkViewTests` (4 tests, run here) and a Swift test parse the same checked-in file `proof/multiplayer/fixtures/tennis_one_tv_config.json`, and the Swift test also checks the field names Swift writes equal the file's. **G2.3** (M, PlayMode): not built yet (see 2.5). **G2.4** (S): audit recorded.

### P3 — Split screen on the TV (L) — tier S/M/D
- **3a Shared view first — done in code; needs Unity and a TV to judge.** Proves the roles end to end before any split-screen work. `TennisGame` now keeps two things apart that were one: whose controller the phone is (`networkLocalSide`) and whose end of the court is drawn in front (`networkNearSide`). A phone whose TV shows both players (`split`) draws the court from seat 0's end whichever player it belongs to; a phone with its own TV (`near`) and a spectator behave exactly as before (a small tested helper, `NetworkConfiguration.NearSide`, decides, and for those cases it returns what the old code did). The pieces that were quietly "the near player is me" are now told apart: movement prediction moves the local player's own figure wherever it is drawn; a started stroke animates the local player's figure; the toss meter sits under whoever is serving and, on a shared TV, both players see it; stamina and the phone's serve/receive hints come from the local player's side. The picture uses the normal elevated court camera behind seat 0, so both players are on screen (the far one is small).
  - **Controller-only phones** (`none`): the game loads as a golf guest's does, and after the first frame has proved the scene loaded the gameplay camera is switched off (battery and heat: the phone is also tracking motion). Game sound is muted there, because the TV is the only speaker. The phone's own hit feedback (haptics) is unchanged. *Unverified:* that nothing else needs that camera; the first device test should watch heat and that the controller still gets its serve/receive prompts.
  - **Known limits of this first version:** the far player on a shared TV sees the court from the "wrong" end (their left is the screen's right), which is what the split screen below fixes; and the timing cue ring is not drawn in network matches (unchanged).
- **3b Split screen — done in code; the picture itself needs a Unity run and a TV to judge.** The phone with the TV (`split` view) splits the display down the middle: seat 0's half on the left, seat 1's on the right, a thin divider, each player's name at the bottom of their half, and the existing scoreboard unchanged (it sits top left, so the names were put at the bottom, not the top as first drafted).
  - **Framing is solved, not guessed** (`TennisSplitCamera`, pure math). A half of a TV is nearly square (960×1080 on a 16:9 TV, 720×900 on a MacBook), so a TV-wide camera would cut the court. For the exact shape of the half, the code searches heights from 7 to 11 m, distances behind the baseline, aim points and fields of view, and keeps the placement that makes the singles court largest while the whole court with its alleys, the room behind each baseline, and a player's head at the widest a player runs stay inside the picture with a 4% margin. Seat 1's camera is seat 0's turned half way round the middle of the court, so each player sees their own end in front of them and the same court. **Result for a 16:9 half:** camera 11 m high, 8 m behind the baseline, aimed at the middle of the near half, field of view 54°; the singles court fills about a quarter of the half (higher cameras fill more but look like a map; 11 m was chosen, and `MaxHeight` is one constant).
  - **Introductions** play on one full-screen camera and then split (`presentation.Playing`). Replays and the cinematic rival camera are not used in network matches.
  - **Second camera.** A copy of the gameplay camera (same post-processing volume mask, shadows, anti-aliasing) on its own object, enabled only while split; `NativeSportsSession.ConfigureDisplay` still routes everything to one display and now tells the game the display's shape.
  - **Fallback:** `TennisGame.SplitScreen = false` gives the single shared view of P3a.
- **3c Done.** See above (introductions).
- **3d Camera consumers reviewed.** The grandstand crowd drew only what the gameplay camera could see, which would have left the stands empty in the other half; it now draws everything while split, and both crowds pick their detail from the nearest camera. The garment and hair detail levels pick from the gameplay camera only, but in split the players are about 14 m from their camera, so both resolve to the same distance mesh (consistent). The rim light follows the sun; the venue's follow-the-camera effects (sky) are large enough that being centred on one camera is invisible. *Not reviewed (no way to see here):* shadows are drawn once per camera, which roughly doubles that cost.
- **3e Not done.** A quality step-down for split mode is waiting for the device frame-rate measurement (G3.4).
- **Gates:** **G3.1** (S): framing math test — for aspects 0.75–1.0 and a grid of ball/player positions, all four court corners and both baseline sidelines project inside each half with ≥ 4% margin. **G3.2** (M, EditMode): the two rects tile the display exactly (no gap, no overlap) and each aspect = half width / height. **G3.3** (M, PlayMode, rendered): screenshots in `proof/multiplayer/split/` — both servers, a rally, a point, each player visible in their own half, HUD readable, no blank half. **G3.4** (D): on a real TV over AirPlay, average ≥ 55 fps and 1% low ≥ 45 over 5 minutes; measured screen delay no worse than solo by more than 10 ms.

### P4 — Both players calibrate on one TV (M) — tier M/D
- **4.1 Done in part.** The phone with the TV tells the lobby how late its picture is (`screenDelay`, from the remembered measurement for that TV, clamped to 0–1 s); the controller-only phone hands that value to Unity (`latency`) as soon as its game is ready, and again if it changes. Both players are therefore credited with the same TV delay (P1.2's rewind). Not done: a fresh *measurement* of the TV in a multiplayer match. The existing flash probe needs the phone's camera aimed at the TV during setup; in the first online setup (P1.1) the remembered value is used, as in a rematch.
- **4.2 Not done.** TV badges in the split halves ("P1 ✓ / P2 scanning…") need Unity to know who has finished setup; Swift does (lobby `calibrated`) and each phone already says "Waiting for …". Pushing the lobby's setup state into Unity for the TV is small once the split screen exists.
- **4.3 (milestone C2)** Swing timing check for both at once, each half. Unity runs `TennisBeatCalibration` per seat, using local samples for the screen phone's player and network swing timestamps for the guest. Fallback: sequential, full screen. Fallback 2: skip and let play refine timing.
- **4.4** Calibration status chips in the lobby and loading screens.
- **Gates:** **G4.1** (M): `screenDelay` flow and application. **G4.2** (M, EditMode): the calibration state machine — both required, either may skip, timeouts. **G4.3** (M, PlayMode): two results from two synthetic swing streams. **G4.4** (D): first-time calibration for two players ≤ 60 s typical; repeat ≤ 15 s.

### P5 — Simple local flow (M) — tier M/D — done in code; tests need a Mac, hands-on check needs phones
- **5.1 Done.** Play now lists **Single Player · Local · Online**. **Local** lists **Golf · Pass the Phone**, **Tennis · Two Phones** and **Join a Friend**.
  - *Golf:* choose how many players (2, 3 or 4) and the round starts at once: the course last picked, the guests already in and ready, no lobby screen. (If no TV is connected yet it stops in the lobby and says "Connect a TV with AirPlay, then tap Start Match.") From Home that is Play → Local → Golf → number of players: four taps, three from the Play screen.
  - *Tennis, two phones:* the host taps **Tennis · Two Phones** and gets a nearby lobby that readies itself and starts as soon as the friend has joined and a TV is connected (the existing Quick Match mechanism, now usable on the same Wi-Fi). The friend taps **Join a Friend** and the host's lobby; a joiner of a tennis lobby is readied automatically. The lobby screen is the waiting card: it shows the join code and, new in P2, where the TV is.
  - Not changed: **Join a Friend** still lists every nearby lobby (golf too) and still has the code box and **Host a Lobby** for the old manual path.
- **5.2 Done (phone side only).** On a shared phone the golf controller shows a "PASS THE PHONE TO {name}" card for about 2.4 s whenever the turn moves to someone else (tap to dismiss; none before the first shot). The TV's own turn banner is unchanged.
- **5.3 Done, one assumption unverified.** If iOS reports Local Network access as denied (the DNS-SD "policy denied" waiting state, which the old code ignored), the Nearby screen now says so and offers **Open Settings**. The assumption is that iOS reports exactly that code (-65570); if it does not, the message simply never appears and nothing else changes.
- **5.4 Partly done.** TCP keepalive on the nearby connection (first probe after 2 s idle, then every second, three misses = dead), so a phone that vanishes without closing is noticed in seconds instead of hours. A lower-latency network service class is left for the field A/B in section 9, because its effect on peer-to-peer Wi-Fi is not known.
- **5.5 Done.** The new rows use the same accessibility ids as their ids (`partyLocal`, `partyLocalGolf`, `partyLocalTennis`, `partyNearby`, `localPlayers2/3/4`); the existing menu tests and the Menu Beta UI test were updated, and a new route test was added.
- **Gates.** **G5.1** (M): `MenuBetaTests` — Play lists exactly Single Player/Local/Online; Local lists the three routes; golf goes to a player-count screen; back paths. **G5.2** (M): `LocalMultiplayerTransport.isLocalNetworkDenied` and the service's Local-Network flag are tested; the Settings button itself is not (UI). **G5.3** (M/CI): `MenuBetaUITests` follows Play → Local → Join a Friend. **G5.4** (D): hands-on with two phones and one TV, and with a pass-the-phone round of 2 and 4.

### P6 — Connection check and ready gate (M) — tier M/D — done for tennis setup (the thresholds are still guesses)
- **6.1 Done.** During tennis setup each guest pings the owner 10 times a second and grades its own last 20 answers (`LinkWindow`): the typical round trip, how much it jumps about, and how many pings got no answer within a second. **Good:** ≤ 80 ms, ≤ 30 ms jitter, ≤ 2% lost. **Fair:** ≤ 150 ms, ≤ 60 ms, ≤ 5%. Worse is **Poor**; fewer than ten answers is "checking". The guest reports its grade to the owner four times a second. (A round trip covers both directions; the two directions are not separable without a one-way clock, so they are not graded separately.)
- **6.2 Done.** A guest counts as *steady* once its grade has been Fair or better for 1.5 s in a row; reports that stop arriving mean "not steady". The owner starts the match only when every competitor is loaded, set up **and** steady (the owner itself is the other end and is never measured). The waiting screen says "Waiting for a steady connection…" when that is all that is left.
- **6.3 Partly done.** If everyone is set up and the connection is still not steady after 8 s, everyone is told which connection is weak and the **owner** is offered **Play anyway** (Leave is always there). Not done: a Quick Match silently looking for another opponent up to twice before asking; that needs changes in the Game Center matchmaking.
- **Not covered.** Golf has no setup phase, so no check; pass-the-phone has no network.
- **Verified here without Xcode:** a small Python model of the protocol (not part of the repo) was used to check the timelines the XCTests assume. Good link (40 ms round trip): starts 3.1 s after setup begins; fair (70 ms): 3.1 s; zero delay: 2.5 s; weak (300 ms) or 25% loss: never starts, offers *Play anyway* at about 8.5 s; a guest that goes quiet for 1.5 s is no longer steady and needs about 4.3 s of good pings afterwards.
- **Gates.** **G6.1** (M, impaired bus): `MultiplayerTests` — good link starts only after being steady; weak link waits, then offers Play anyway to the owner only; 25% loss is not steady; silence is not steadiness; the grade boundaries are exact; golf is not held up. **G6.2** (D): thresholds checked against field data (section 9).

### P7 — Feel (L) — 7.3 done and ON by default; the others are NOT built (they need real phones to judge)
- **7.1 Not built.** Predict your own hit (share the shot code between host and client, derive the placement randomness from match seed/point/contact, launch the predicted return at contact, blend to the host's result over about 100 ms, snap back on a miss). Flag `PredictOwnHit`. This changes what a player sees at the moment of contact and can only be judged by playing it, so it stays unbuilt until there are phones to tune it on.
- **7.2 Not built.** An immediate update on contact, bounce and point, not only every 1/30 s. The idea is sound but its value depends on the real network delay, which is not known yet.
- **7.3 Done: compact updates (`NetworkTennisWire`).** The host's 30-per-second tennis update was about 1.2 KB of JSON (about 1.7 KB with the packet around it) because every field is written by its long name. The compact form groups the numbers into short arrays, sends positions as whole millimetres and times as whole milliseconds, packs the on/off fields into flags, and carries the score in every update: **366 bytes against 1,205 (30%)** for a typical rally update, measured with the sandbox JSON writer. Whole numbers were chosen deliberately: some Unity versions print a float as its long double expansion (`0.10000000149011612`), which would have made the size depend on the editor version; integers do not. Checkpoints (the reliable "full" updates at the start and at point boundaries) still use the full form, and guests read either form, so nothing else changes. Details of safety:
  - **Nothing can be silently forgotten.** A test fills every field of the full update, both players and the score with a different value (twice, with every on/off field inverted the second time) and checks each one comes back; a field added to the game later fails that test until it is added to the compact form.
  - **A check on the real device.** When a tennis match starts, the host writes and reads back a made-up update with this build's own JSON; if anything is lost it sends the full form for the whole match. The result is in the `unity network stats` log line (`"compact":true|false`).
  - **A newer or broken form is refused, not misread.** A compact update of a version this build does not know, or with a wrong-length array, or no phase, is ignored (the guest keeps the last good update).
  - **Cost:** positions are exact to half a millimetre and times to half a millisecond; the host itself never uses the rounded values, only the guests do.
  - **Switch:** `NetworkTuning.CompactSnapshots` (default `true`). This deviates from the "feel features default OFF" rule in section 8 because this is a re-encoding, not a behaviour change, it is covered by the round-trip tests and the self-check above, and the full form (1.7 KB) is the thing most likely to be refused as an unreliable message. Flip it to `false` to go back to the full form.
  - **Also updated:** the debug-only Nearby proof driver (`OnlineLobbyProofDriver` in `TennisMenu.swift`) read the update's keys directly; it now understands both forms.
  - **Gates.** **G7.3.1** (S): `NetworkTennisWireTests` — all-fields round trip (x2), ten seconds of a real rally compared at 30 Hz to the millimetre, set scores, wrong-shape and unknown-version refusal, the self-test. **G7.3.2** (S): size under 40% of the full form and under 520 bytes. **G7.3.3** (M): compile in Unity and run `NetworkTennisWireTests` in the editor (the sandbox uses a stand-in for `JsonUtility`). **G7.3.4** (D): the first field test in section 9 (message-size experiment) decides whether unreliable delivery at this size works; the fallback in 1.7 covers the case where it does not.
- **7.4 Not built.** Replacing the 60/s Swift poll with a wake signal from the bridge (static target, generation-checked, to avoid the "callback into released object" risk noted in `SportsBridge.mm`). The risk of a crash from a callback into a released object is real and cannot be tested without a device.
- **7.5 Not built.** Blending the displayed ball when the host applies a retroactive hit. Same reason as 7.1.
- **Gates:** each feel feature needs a determinism or reconciliation test (S) and a before/after device measurement (D) and is turned on only with a recorded PASS.

### P8 — Docs, cleanup, field kit (S) — done
- **8.1 Done.** `SportsLibrary/MULTIPLAYER.md` now has a section describing what this pass changed (menus, protocol 4, tennis setup phases, one TV with two phones, fair timing, connection check, compact updates, switches, what is not built) and no longer says there are no entry screens.
- **8.2 Done.** The older stand-alone online system is labelled **legacy** at the top of `server/README.md`, under the online heading of `Unity/README.md`, and on the two source-tree bullets there (`Net/Online`, `Lobby`). Nothing was deleted; deleting it is your decision (section 8, item 5).
- **8.3 Done.** `proof/multiplayer/GATE_RESULTS.md` has a summary at the top and a line for every gate, including the ones nobody has been able to run.
- **8.4 Done.** `proof/multiplayer/FIELD_TEST.md` is the script for the first session with real phones: what to bring, the nine tests with pass lines, how to pull the log off a phone, result tables, and a table of which constant to change for which symptom.
- **Gates.** **G8.1** (S): every relative link in the touched docs resolves (13 checked). **G8.2** (S): the diff to the legacy READMEs only adds text, and no file under `server/` or `Net/` was removed.

### P9 — Independent review of the whole branch (S) — done
Nothing here could be compiled on a Mac or a phone, so three separate read-only reviewers read the whole branch against the code (Swift core, Swift menus and session, Unity C#). The Unity files that use real Unity APIs were also compiled against hand-written stand-ins for the Unity, URP and UI signatures (0 errors; the stand-ins are only as good as their author's memory of the APIs, and `UniversalAdditionalCameraData.stopNaN` and `.dithering` are the two members nothing else in this repo compiles). Every finding was checked against the code before anything changed; the real ones are fixed, each with a test where a test can show it:
- **Swift core.** `isLocalNetworkDenied` is `nonisolated` (it is called from Network.framework's handlers, which are not on the main actor; Swift 6 would refuse the call). The shared fixture lacked `linkOK`, so the Swift fixture test could not decode it. The link-grading test's helper expired a lost ping "after exactly a second", which floating point sometimes misses, so its last assertion could not pass. The *Play anyway* button and the weak-connection or slow-setup notice stayed on screen for the whole match when the link recovered by itself: they are cleared when play begins. A phone that was only quiet (never dropped) lost its loaded and set-up state when any other phone left, so the other player saw "getting ready · match paused" for the rest of the match: only a phone whose connection really dropped starts again.
- **Swift menus and session.** The first pass-the-phone card was swallowed (the first turn was never remembered), and the card also appeared when the turn passed to a friend on their own phone. On online tennis, *Use the direction I'm pointing now* went into the solo timing check, whose commands a match silently drops, leaving the phone on "Swing on every bounce!"; it now goes to Ready like a finished scan, and the solo-only *Re-check swing timing* is hidden and refused in a match. The "I am set up" message was sent once: it is now repeated every 3 s until the owner's lobby shows it, so one lost message cannot leave a match waiting (or a returning player paused). The debug proof driver read the compact update's milliseconds and millimetres as seconds and metres. Back from *Join a Friend* went to Play instead of Local. The long Nearby row expression is split so the compiler has nothing to work out, and the four-player icon falls back if the symbol is missing. The snapshot tests' mock lobby now marks players as set up.
- **Unity.** *The controller's score was from the wrong side on a shared TV:* the phone showed the court-near player's score, serve and "YOU WIN" instead of its own player's (`TennisMatch.Mirrored()` now orients each; tests). *The split divider and the two names never reached the TV:* their canvas is inactive when the displays are routed, so it stayed on the phone; it is now moved with the camera. `TennisMatch` was not `[Serializable]`, so Unity's JSON writer would have left the score out of every full update (checkpoints, and the fallback when compact updates are off); fixed, with a test that only Unity's own writer can fail. On the phone's own screen the split halves used a frozen window shape. The camera search that placed the halves ran in the first frame of the match; it now runs while loading. The toss meter for a server at the far end now turns round with the server.
- **Reviewed and left alone.** Tennis spectators now lift their loading cover when the lobby reaches `calibrating` (earlier than before); Unity ignores updates until the match runs, so they see the still court; if a device shows something odd there, hold the cover until `playing`.

### Suggested order
P0 → P1 → P2 → P3a → P4.1/4.2 → P5 → P3b/3c/3d → P4.3 → P6 → P7 → P8. Reason: P1 fixes the "swings don't register / TV delay" problems for every multiplayer mode; P2–P3a prove the one-TV roles with the simplest camera before the visual work; P5 delivers the simple flow you described; split screen polish and the parallel timing check follow once the plumbing is proven.

---

## 6. Risks and fallbacks

| Risk | Mitigation / fallback |
|---|---|
| Swift and Unity edits can't be compiled in the sandbox | Swift: syntax parse here, real compile via CI on a draft PR. Unity: pure logic verified here via the .NET harness; the rest via your `check.sh` / PlayMode runs. Small commits, one gate each. |
| Split screen looks cramped or has visual defects (FOV, crowds, LOD) | P3a shared camera works first. P3d review table. The side-by-side layout can become top/bottom or switch to shared camera with a one-line change. |
| Hidden Unity on the controller-only phone costs battery/heat | Camera disabled, no frame wait; measure in G3.4; if bad, lower its tick rate while idle. |
| Larger rewind causes visible jumps for the opponent | Cap 0.40 s, 0.30 s display credit, blend (7.5), tune from telemetry. |
| Apple's unreliable-message size limit is unknown | Fallback to reliable for oversize (1.7); snapshot slimming; first field test in section 9. |
| New `calibrating` phase interacts badly with the loading cover | Spiked in P1.1 (written finding in 1.1): the cover only needs `allReady`, which is now true for `calibrating`. What the TV shows during setup still needs a device look. |
| Protocol v4 breaks old builds | App isn't live; both phones must run the same build (already true today). |
| ARKit axis capture cannot run on a phone with no TV | Not needed: the scan uses only the phone's sensors; the guest points its phone at the same TV. |

---

## 7. Deferred (not in this plan)
Host chosen by link quality or host migration; a UDP data channel for Nearby; region-based matchmaking; doubles or 3+ player tennis; spectator views; voice chat; deleting the legacy online system; any art, character, clothing, map or lighting changes.

---

## 8. Decisions needed (defaults I will use unless you say otherwise)

1. **Split layout:** side by side, seat 0 left, seat 1 right. *(default)*
2. **Which phone renders:** whichever has the TV; if both do, each uses its own TV in the normal view. *(default)*
3. **If split looks poor:** fall back to one shared camera. *(default allowed)*
4. **Delay constants:** display credit ≤ 0.30 s, total rewind ≤ 0.40 s. *(default)*
5. **Legacy online system:** leave in place, label as legacy. *(default)* Say "remove" to delete it.
6. **Feel features (P7):** build behind flags, default OFF until device-tested. Exception: 7.3 compact updates, which only re-encode the same data, are ON with a start-of-match self-check and a one-line switch. *(default)*
7. **Draft pull request to `testing`** when P1 starts so CI compiles the Swift changes. *(needs your OK, because you asked me not to open PRs unprompted)*
8. **Touch-control players** may join couch tennis and skip the scan. *(default yes)*

---

## 9. Field-test kit (for when the app is live)

The step-by-step script is `proof/multiplayer/FIELD_TEST.md`. The telemetry log is `SportsDiagnostics.log` in the app's Documents folder (nothing is uploaded; pull it with Xcode). The list below is the reasoning behind it:
1. **Apple unreliable message size:** play a normal match with `NetworkTuning.CompactSnapshots` on (about 0.5 KB with the packet) and again with it off (about 1.7 KB) and compare `unreliable refused` in the log. *(First test: it decides whether the compact form was needed and how aggressive any further slimming must be. No special test message was built; the real traffic is the experiment.)*
2. **Online link quality:** RTT, jitter, loss for Wi-Fi↔Wi-Fi, Wi-Fi↔LTE, LTE↔LTE, same city and cross-country. Tune the P6 thresholds from this.
3. **TV delay by receiver:** Apple TV, Mac AirPlay Receiver, HDMI adapter (existing flash probe). Confirm the 0.30 s credit cap covers typical TVs.
4. **Nearby path A/B:** infrastructure Wi-Fi vs peer-to-peer (`includePeerToPeer`); service class on/off; note RTT and jitter.
5. **Split-screen performance:** fps, 1% low, temperature after 10 minutes, on the oldest supported phone.
6. **Calibration:** time to finish, how often someone taps "Re-aim", how often a swing is missed right after.
7. **Retroactive-hit jumps:** have the opponent describe any visible ball jump; compare with the clamp/reject counts in telemetry.
Tuning constants live in `NetworkTuning` (C#) and `MultiplayerTuning` (Swift).

---

## Appendix A — Experiment: how the rules handle delay (before and after P1.2)

Reproduce: `proof/multiplayer/latency_experiment/run.sh` (needs only the .NET 8 SDK; BEFORE is built from the commit before P1.2, AFTER from the working tree). It serves, then delivers a perfectly timed return through the real `NetworkTennisMatch` / `TennisRules` using the two messages a phone really sends (swing started, then swing confirmed 180 ms later), adding screen delay, one-way network delay and clock error. Sensor delay is 30 ms. It assumes a perfectly timed swing on the cue the player *sees*, so it shows the bias from delay, not human variation. A serve return is the tightest case.

```
=== BEFORE: commit 9a305ef7 (the phone credits no screen delay) ===
an unreturned serve stays in play 192 ms after passing the receiver and the point goes to seat 1 (0 = the server)

SCREEN DELAY x NETWORK DELAY (a perfectly timed swing on the cue the player sees)
screen delay \ one-way network           20 ms       50 ms      100 ms      150 ms      200 ms
      0 ms                              PERFECT!    PERFECT!    PERFECT!    PERFECT!        MISS
     50 ms                                 GREAT       GREAT       GREAT        MISS        MISS
    100 ms                                  GOOD        GOOD        MISS        MISS        MISS
    150 ms                                  MISS        MISS        MISS        MISS        MISS
    200 ms                                  MISS        MISS        MISS        MISS        MISS
    300 ms                                  MISS        MISS        MISS        MISS        MISS
    400 ms                                  MISS        MISS        MISS        MISS        MISS

=== AFTER: working tree (the phone credits its screen delay; the host waits for a started swing) ===
an unreturned serve stays in play 192 ms after passing the receiver and the point goes to seat 0 (0 = the server)

SCREEN DELAY x NETWORK DELAY (a perfectly timed swing on the cue the player sees)
screen delay \ one-way network           20 ms       50 ms      100 ms      150 ms      200 ms
      0 ms                              PERFECT!    PERFECT!    PERFECT!    PERFECT!    PERFECT!
     50 ms                              PERFECT!    PERFECT!    PERFECT!    PERFECT!    PERFECT!
    100 ms                              PERFECT!    PERFECT!    PERFECT!    PERFECT!    PERFECT!
    150 ms                              PERFECT!    PERFECT!    PERFECT!    PERFECT!        MISS
    200 ms                              PERFECT!    PERFECT!    PERFECT!        MISS        MISS
    300 ms                              PERFECT!        MISS        MISS        MISS        MISS
    400 ms                                  MISS        MISS        MISS        MISS        MISS

=== CLOCK ERROR: working tree ===
an unreturned serve stays in play 192 ms after passing the receiver and the point goes to seat 0 (0 = the server)

CLOCK ERROR (a perfectly timed swing; + = the guest thinks the host is ahead)
clock error \ one-way network            20 ms       50 ms      100 ms
   -100 ms                                 GREAT       GREAT       GREAT
    -50 ms                              PERFECT!    PERFECT!    PERFECT!
      0 ms                              PERFECT!    PERFECT!    PERFECT!
     30 ms                             EXCELLENT   EXCELLENT   EXCELLENT
     50 ms                             EXCELLENT       GREAT       GREAT
     70 ms                             EXCELLENT       GREAT        GOOD
    100 ms                               DROPPED       GREAT        GOOD
    150 ms                               DROPPED     DROPPED        MISS
```

Reading it:
- **BEFORE:** the point went to seat 1 (the receiver) when nobody returned a legal serve (F17). With 0 ms of TV delay the network is fine through 150 ms. With a 50 ms TV the best grade is GREAT, with 100 ms it is GOOD, and from 150 ms a serve cannot be returned at all.
- **AFTER:** the server wins an unreturned serve. Perfect returns hold wherever screen delay + network time is about 0.30 s or less; beyond about 0.34 s the swing start reaches the host after the ball is dead, which no rewind can fix.
- **Clock error (today's rules):** a guest clock that is 100 ms ahead drops its swings entirely at 20 ms network time (`DROPPED`). Step P1.3 addresses this.
- The ~1.7 KB message size in F11 comes from a JSON mock of the same fields, not Unity's serializer, so it still needs the G1.7a check. The compact form (P7.3) is sized the same way (366 bytes for the update, about 0.5 KB with the packet around it) and is built so the size does not depend on how Unity prints floats.

## Appendix B — Where to look in the code
Transports: `GolfArcade/Multiplayer/GameCenterTransport.swift`, `LocalMultiplayerTransport.swift`. Lobby and bridge: `MultiplayerService.swift`, `MultiplayerModels.swift`, `Unity/Assets/Plugins/iOS/SportsBridge.mm`. Unity side: `Multiplayer/SportsMultiplayer.cs`, `NetworkTennisMatch.cs`, `NetworkGolfRound.cs`, `Tennis/TennisGame.Network.cs`, `Game/NativeSportsSession.cs`. Calibration: `SportsSession.swift` (setup stages), `SportsMotion.swift` (axis gate, `calibrate()`), `TennisPlayViews.swift` (setup panels). Menus: `TennisMenu.swift`, `IslandScreens.swift`. Display and cameras: `SportsDisplays.swift`, `SportsRuntime.mm`, `TennisShoulderCamera.cs`, `TennisGame.cs` (`UpdateCamera`, `BuildHud`). Tests: `GolfArcadeTests/MultiplayerTests.swift`, `Unity/Assets/Tests/EditMode/MultiplayerRulesTests.cs`, `PlayMode/MultiplayerRuntimeTests.cs`.
