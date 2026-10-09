# Multiplayer plan — gate results

Plan: `PLAN_Multiplayer_OnlineLocal.md`. Format follows `proof/menu-beta/GATE_RESULTS.md`.
Tiers: **S** sandbox (run here), **M** needs a Mac (Xcode/Unity), **D** needs real phones.
A gate is PASS only with its evidence. Gates that need a Mac or phones are listed as NOT RUN until someone runs them.

## Phase 0 — Safety net

GATE: G0.1 Sandbox harness runs the game's real multiplayer rule tests — PASS (S). `dotnet test Tools/netsim/NetSim.csproj`: 33 passed, 0 failed (the real `MultiplayerRulesTests`: tennis host rules, golf host rules, serialization round trips).
GATE: G0.3 Latency experiment reproduces the plan's tables — PASS (S). `proof/multiplayer/latency_experiment/run.sh` after the toss-curve refactor prints the same three tables as Appendix A.
