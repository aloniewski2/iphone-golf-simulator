# GATE_RESULTS: golf read pass (2026-10-12)

Proof sources:
- `before/` and `after/`: TV frames from the game's own camera in native (app) mode, plus `log.txt` measurements.
- `before_after.jpg`: the side-by-side sheet.
- EditMode suite run through `Unity/Tools/check.sh`.
- PlayMode runs in `playmode-before/` and `playmode-after/`.

`before` = an APFS clone of the project taken before any edit. `after` = the edited tree.

| Gate | Result | Evidence |
|------|--------|----------|
| PUTT_LINE_REMOVED | PASS | Aim line points at address: before 73 / 275 (the ribbon), after 0 / 0. `LayPuttRibbon` deleted. Frames 1, 2. |
| PUTT_SHOWS_START_ONLY | PASS | Putt dots run straight on the aim. The farthest dot is 37% (8 ft putt) and 43% (39 ft putt) of the putt, both ≤ 55%. No break and no pace. Frames 1, 2. |
| ARC_HALF_THEN_FADE | PASS | Full-shot dots reach 44% (tee, driver), 46% (rough) and 45% (sand) of the still-air carry; before they reached 97–106%. Alpha and size fade from 45% of the shown length. Frames 3, 4, 5. |
| AIDS_DONT_SOLVE_THE_WIND | PASS | The dots, landing ring, 25–100% markers and load dot are all still-air (`PreviewLaunch`). The marker assert in `GolfPolishTests.LandingMarkersMatchPowerAndCameraHoldsTheGolfer` now checks still-air carry and passes; that test fails later, at line 115, as on baseline. |
| GREEN_READ_SUBTLER | PASS | The contour grid (15,194 verts) and the beads are gone. Pale grain chevrons replace them (≈5.5k verts, alpha ≤ 0.30, hidden under 0.4% slope), pointing and drifting downhill. Frames 1, 2. **Taste call: Adnan's eye.** |
| WIND_PER_HOLE | PASS | Each hole still gets its wind from the round seed (`Match.WindFor`), the same on every phone. MatchTests pass. |
| WIND_STRONG_RARE | PASS | `StrongWindIsRareAndMostHolesAreLight` over 20,000 draws: 15+ mph 8–12%, under 10 mph 60–70%, ≤ 3 mph 20–30%. |
| WIND_PUSHES_FULL_SHOTS | PASS | An 85% driver into an 18 mph crosswind came down 17.8 yd right of the aim (frame 3b). Test: 15 mph across moves a 7-iron > 15 yd; the model gives 21.5 yd. |
| WIND_BARELY_PUTTS | PASS | Putt rest is identical in calm and 20 mph (test `AStrongWindMovesAFullShotAndNotAPutt`). Note: putts get *no* wind, not "a little". |
| WIND_SHOWN | PASS | TV HUD: drawn arrow (relative to the aim) + mph + Light/Moderate/Strong, coloured white/yellow/orange. Frames 1–5. The phone controller already showed an arrow + mph and is unchanged. |
| AIM_INTO_WIND | PASS | Aim turns freely (`Nudge` is unclamped), and the aids no longer pre-compensate, so aiming off is the player's call. Strong wind is capped at 20 mph and rare. |
| LIE_DISTANCE | PASS | `RoughCostsSomeDistanceAndSandMore`: the rough keeps 0.87–0.93 of carry and of total for DR, 5I, 7I, 9I and PW (before, PW/SW went 7% *further*). A 7-iron from sand keeps 0.72–0.78. Frames: driver 216 from rough (240 × 0.9), 7-iron 113 from sand (150 × 0.75). |
| LIE_ACCURACY | PASS | `RoughAndSandKnockTheShotOffLine`: up to ±2.5° / ±4% pace from rough and ±5° / ±7% from sand. The draw is made once at the strike, before the network send. Planned test shots stay exact. |
| LIE_SHOWN | PASS | The lie chip is under the shot info: "ROUGH -10% DISTANCE / LESS ACCURATE", "SAND -25% DISTANCE / MUCH LESS ACCURATE", "TEE CLEAN LIE", "GREEN PUTTING SURFACE". Frames 1–5. |
| EDITMODE_NO_REGRESSION | PASS | Before: 394 pass / 23 fail. After: 397 pass / 23 fail. The failure set is identical (bare-runner ECall/NRE plus 6 MultiplayerRules golf failures that predate this pass). |
| PLAYMODE_NO_REGRESSION | PASS | `GolfPolishTests` + `GolfBroadcastTests`: 2/5 pass before and after. The same 3 fail at the same lines: a face shader name (other session), face framing at the result, and `ShowingShotResult` at `GolfPolishTests.cs:115`. |
| SCOPE | PASS | No animation, hero, map, camera, lighting or Swift file touched. See the file list in `golf-read.patch`. |
| TEE_ARC_READABLE | PASS (weak) | On the island tee the first dots sit over the pale cliff and behind the "PIN" label. A dark halo per dot makes them read through cloud and sky, but the two over the cliff face are still faint. |
| DEVICE | NOT RUN | No phone or TV run. |
