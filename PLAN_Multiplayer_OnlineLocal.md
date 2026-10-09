# PLAN — Multiplayer: fast online play, one-tap local play, two-phone tennis on one TV

**Status:** IN PROGRESS. You told me to proceed on my own, so I am working through the phases in order using the defaults in section 8. Done so far: P0 (safety net), P1.1–P1.7. Progress and evidence: `proof/multiplayer/GATE_RESULTS.md`. Gates that need a Mac or real phones are listed there as NOT RUN until someone runs them.
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
| F11 | Update messages are about 1.7 KB, sent as "unreliable" 30×/s. Apple says unreliable is for "small packets" and gives no number. | estimate (`proof/multiplayer/`), Apple docs | If rejected, online tennis stutters. **Verify on devices first.** | P1, field |
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
- **0.3** Swift `ImpairedBus`: extend `MultiplayerTests.Bus` with delay, jitter, loss, reorder, duplicate and partitions on an injected clock.
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
- **1.2 Screen-delay credit.** `SportsMultiplayer.Sample` adds `Lag` (≤ 0.30 s) to the swing `age`; `NetworkTuning.MaxRewind = 0.40`; `NetworkInput.Valid` age ≤ 0.5; history window sized from the constant.
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
- **3a Shared camera first.** Canonical coordinates (`networkNearSide = 0`), one camera behind seat 0, raised so both players are visible. Proves roles end to end.
- **3b Split screen.** Two cameras with viewport rects; `ConfigureDisplay` must stop disabling the second camera; per-half aspect; far view = mirrored `TennisShoulderCamera.Frame`; court-fit check so every court corner stays in view with a margin; divider; per-half name tags; shared scoreboard.
- **3c Cinematics.** Full-screen director camera for intros/presentations, then back to split.
- **3d Camera consumers.** Review each use of `Camera.main`/`GameplayCamera`: garment and hair LOD, rim light, crowds (`TennisResortCrowd`, `TennisStandsCrowd`), venue effects, `HeroTennisDriver:1409`, juice/shake. Mark each "primary only", "per camera" or "n/a"; fix or accept with a screenshot.
- **3e Performance.** Quality step-down in split mode if the frame governor reports pressure.
- **Gates:** **G3.1** (S): framing math test — for aspects 0.75–1.0 and a grid of ball/player positions, all four court corners and both baseline sidelines project inside each half with ≥ 4% margin. **G3.2** (M, EditMode): the two rects tile the display exactly (no gap, no overlap) and each aspect = half width / height. **G3.3** (M, PlayMode, rendered): screenshots in `proof/multiplayer/split/` — both servers, a rally, a point, each player visible in their own half, HUD readable, no blank half. **G3.4** (D): on a real TV over AirPlay, average ≥ 55 fps and 1% low ≥ 45 over 5 minutes; measured screen delay no worse than solo by more than 10 ms.

### P4 — Both players calibrate on one TV (M) — tier M/D
- **4.1** Screen phone measures the TV once (existing flash probe); `screenDelay` goes to the guest and is applied to its `Lag` prior; timing results are stored per phone per screen name.
- **4.2** Both phones scan in parallel; the TV shows status badges ("P1 ✓", "P2 scanning…") in the split halves.
- **4.3 (milestone C2)** Swing timing check for both at once, each half. Unity runs `TennisBeatCalibration` per seat, using local samples for the screen phone's player and network swing timestamps for the guest. Fallback: sequential, full screen. Fallback 2: skip and let play refine timing.
- **4.4** Calibration status chips in the lobby and loading screens.
- **Gates:** **G4.1** (M): `screenDelay` flow and application. **G4.2** (M, EditMode): the calibration state machine — both required, either may skip, timeouts. **G4.3** (M, PlayMode): two results from two synthetic swing streams. **G4.4** (D): first-time calibration for two players ≤ 60 s typical; repeat ≤ 15 s.

### P5 — Simple local flow (M) — tier M/D
- **5.1** Menu: Play → Local → { **Golf · pass the phone [2][3][4]** | **Tennis · two phones [Host][Join]** | **Nearby** (several phones) }. Pass-the-phone creates the guests and starts with no lobby stop. Tennis Host creates the lobby and shows a waiting card with a code; Join shows nearby courts with one-tap join; joiners are auto-ready; the match starts automatically when everyone is calibrated and the link check passes.
- **5.2** Hand-off cue: "Pass to {name}" card before each guest turn on the controller; the TV says "{name}'s turn".
- **5.3** Local Network permission: trigger the system prompt on entering Local; handle `.waiting` (policy denied) for the browser and the listener with a clear message and an Open Settings button.
- **5.4** Cheap local-transport hardening: TCP keepalive (idle 2 s); try a lower-latency service class (field A/B).
- **5.5** Accessibility ids and UI tests for each new control.
- **Gates:** **G5.1** (M): route tests — golf pass ≤ 3 taps from Home for any player count, with no lobby screen; tennis host ≤ 3, joiner ≤ 4. **G5.2** (M): a denied permission produces the message and the Settings button. **G5.3** (M/CI): new `SportsNavigationTests` cases. **G5.4** (D): hands-on with two phones and one TV.

### P6 — Connection check and ready gate (M) — tier M/D
- **6.1** During `calibrating` (golf: during `loading`): 20–30 pings at 10–20/s on the game channel; compute median RTT, jitter and loss; grade Good / Fair / Poor. Starting thresholds (to be tuned in the field): Good ≤ 80 ms, ≤ 30 ms jitter, ≤ 2% loss; Fair ≤ 150 ms, ≤ 60 ms, ≤ 5%.
- **6.2** "Stable" = Fair or better for 1.5 s continuously on both directions. Start only when every competitor is calibrated, loaded and stable.
- **6.3** Poor: Quick Match re-queues silently up to 2 times, then asks; friends lobbies ask "Play anyway / Leave".
- **Gates:** **G6.1** (M, `ImpairedBus`): good link starts; poor link asks; flapping link waits; boundary values exactly at each threshold; Quick Match re-queues exactly twice. **G6.2** (D): thresholds checked against field data (section 9).

### P7 — Feel (L) — all behind flags, default OFF until device-proven
- **7.1** Predict your own hit: share the shot code between host and client, derive the placement randomness from (match seed, point, contact), launch the predicted return at contact, blend to the host's result over about 100 ms, snap back if the host says miss. Flag `PredictOwnHit`.
- **7.2** Send an immediate update on contact, bounce and point (not only every 1/30 s); optionally 60/s when the link is Good.
- **7.3** Compact snapshot (≤ 600 bytes typical).
- **7.4** Replace the 60/s Swift poll with a wake signal from the bridge (static target, generation-checked, to avoid the "callback into released object" risk noted in `SportsBridge.mm`).
- **7.5** Blend the displayed ball when the host applies a retroactive hit, so the opponent sees no jump.
- **Gates:** each flag has a determinism or reconciliation test in the harness (S) and a before/after device measurement (D). A flag is turned on only with a recorded PASS.

### P8 — Docs, cleanup, field kit (S)
- Update `SportsLibrary/MULTIPLAYER.md`; mark the legacy online system as legacy in `Unity/README.md` and `server/README.md` (no deletion without your approval); finalize `proof/multiplayer/GATE_RESULTS.md`; add the field-test script (section 9).

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
6. **Feel features (P7):** build behind flags, default OFF until device-tested. *(default)*
7. **Draft pull request to `testing`** when P1 starts so CI compiles the Swift changes. *(needs your OK, because you asked me not to open PRs unprompted)*
8. **Touch-control players** may join couch tennis and skip the scan. *(default yes)*

---

## 9. Field-test kit (for when the app is live)

Record these and bring the telemetry log from "Send feedback":
1. **Apple unreliable message size:** between two real phones, send unreliable messages of 400 / 800 / 1,200 / 1,500 / 2,000 / 4,000 bytes; note which arrive. *(First test: it decides how aggressive snapshot slimming must be.)*
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
- The ~1.7 KB message size in F11 comes from a JSON mock of the same fields, not Unity's serializer, so it still needs the G1.7a check.

## Appendix B — Where to look in the code
Transports: `GolfArcade/Multiplayer/GameCenterTransport.swift`, `LocalMultiplayerTransport.swift`. Lobby and bridge: `MultiplayerService.swift`, `MultiplayerModels.swift`, `Unity/Assets/Plugins/iOS/SportsBridge.mm`. Unity side: `Multiplayer/SportsMultiplayer.cs`, `NetworkTennisMatch.cs`, `NetworkGolfRound.cs`, `Tennis/TennisGame.Network.cs`, `Game/NativeSportsSession.cs`. Calibration: `SportsSession.swift` (setup stages), `SportsMotion.swift` (axis gate, `calibrate()`), `TennisPlayViews.swift` (setup panels). Menus: `TennisMenu.swift`, `IslandScreens.swift`. Display and cameras: `SportsDisplays.swift`, `SportsRuntime.mm`, `TennisShoulderCamera.cs`, `TennisGame.cs` (`UpdateCamera`, `BuildHud`). Tests: `GolfArcadeTests/MultiplayerTests.swift`, `Unity/Assets/Tests/EditMode/MultiplayerRulesTests.cs`, `PlayMode/MultiplayerRuntimeTests.cs`.
