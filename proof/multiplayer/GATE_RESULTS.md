# Multiplayer plan — gate results

Plan: `PLAN_Multiplayer_OnlineLocal.md`. Format follows `proof/menu-beta/GATE_RESULTS.md`.
Tiers: **S** sandbox (run here), **M** needs a Mac (Xcode/Unity), **D** needs real phones.
A gate is PASS only with its evidence. Gates that need a Mac or phones are listed as NOT RUN until someone runs them.

## Phase 0 — Safety net

GATE: G0.1 Sandbox harness runs the game's real multiplayer rule tests — PASS (S). `dotnet test Tools/netsim/NetSim.csproj`: 33 passed, 0 failed (the real `MultiplayerRulesTests`: tennis host rules, golf host rules, serialization round trips).
GATE: G0.5 Syntax check for files that cannot compile here — PASS (S). `python3 Tools/check-syntax.py` (tree-sitter, C# + Swift) passes every changed file and fails a deliberately broken one. It checks syntax only, not types.
GATE: G0.3 Latency experiment runs before-vs-after from git history and the working tree — PASS (S). `proof/multiplayer/latency_experiment/run.sh` builds the sources of commit 9a305ef7 (BEFORE) and the working tree (AFTER) and prints the tables in Appendix A of the plan.

## Phase 1 — Make multiplayer tennis playable and fair

### P1.2 Screen (TV) delay credited to online swings

GATE: G1.2 Online swings are judged as the player saw them — PASS (S). `dotnet test Tools/netsim/NetSim.csproj`: 49 passed, 0 failed (the game's 33 original rule tests plus `NetworkTimingTests` and `NetworkTennisPointTests`). Perfectly timed returns grade PERFECT for screen delay + network time up to 0.30 s (the test grid), against the real two-message sequence (swing start, then confirmation). Without the credit the same swing grades below PERFECT at 200 ms of screen delay.
GATE: G1.2b Claimed screen delay cannot be abused — PASS (S). The phone credits at most 0.30 s (`NetworkTuning.MaxDisplayCredit`); a 600 ms claim returns the same grade as a 300 ms claim; ages above 0.5 s are rejected by `NetworkInput.Valid`.
GATE: G1.2c Changed Unity-only file parses — PASS (S, syntax only). `SportsMultiplayer.cs` (the phone-side credit) passes the syntax check. NOT RUN (M): it needs the Unity editor/`Unity/Tools/check.sh` to compile and a device to see the effect (G1.2d, D).

Two things the work found that the plan did not list, both fixed with tests:

- **F17 — an unreturned ball gave the point to the wrong player.** In the host rules a ball that landed legally and then left the court untouched ended with `Point(receiver)`: an unreturned serve (an ace) awarded the point to the player who failed to return it. Tests `AnUnreturnedServeThatLandedLegallyIsAnAceForTheServer` and `AnUnreturnedRallyBallThatLandedInCourtWinsThePointForTheHitter` failed before the fix and pass after. A ball that leaves without ever landing is still the hitter's fault (`ABallThatNeverLandedIsTheHittersFault`).
- **F18 — the point could end before a delayed swing arrived.** An unreturned serve stays alive only ~190 ms after it passes the receiver. The host returns the ball only when a swing is *confirmed*, so crediting the screen delay alone could not help once screen delay + network time passed ~0.16 s. The host now holds a dead ball's point open for at most `NetworkTuning.PendingSwingHold` (0.35 s) **only if** the receiver has started a swing that is not yet confirmed. No swing started: no delay (`WithNoSwingStartedThePointIsNotDelayed`). Aborted or already-missed swings do not hold the point.
