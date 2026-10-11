# Tennis feel pass — 2026-10-10

Why: playtest feedback was that tennis against the CPU was too hard to be fun. Aiming should be a timing
skill (Wii Sports Tennis), the ball outran the character and rallies were short, and swing and serve
response still felt slow, which made an online match hard to imagine. An audit found the causes; this pass
fixes the ones that can be fixed from the code. **Nothing here has been played on a phone and TV yet** (see
"What was and was not verified").

## What the audit found (short)

- **Aim** came from the phone's racket-face yaw (about 10 cm of landing spot per degree, read 50-100 ms before contact,
  no personal "straight") and from three depth buttons nobody looks at mid-rally. Timing only changed how accurate the
  ball was, never where it went. A Wii-style `AimFromTiming` existed, unused and with the opposite sign from its comment.
- **Reach**: the character runs on auto-pilot but waited 0.24 s, then needed ~0.5 s to reach speed; over a ~0.9 s ball that is
  2.0 m of running. The sprint boost ("good jump") needed a lean that a phone-motion player never gives (`MoveInput` is
  always 0), yet the self-play numbers that the balance was tuned against got it on every ball.
- **CPU**: fastest when comfortable (up to 33-34 m/s nominal), flat arcs (.28 m over the tape), 4.5 m reach envelope.
- **Latency**: the TV (AirPlay) picture delay dominates and cannot be coded away; on top of it the toss/serve choreography,
  touch-up buttons, the swing detector's confirmation, a 1024-sample audio buffer, and no online hit feedback.

## What changed

| Area | Knob | Before | After | Where |
|---|---|---|---|---|
| Aim | direction | racket-face yaw (phone) | **swing timing**: on the ball (within 60 ms) straight, early left, late right, full at 240 ms | `TennisRules.AimFromTiming`, `TimingPlacement`; `TennisGame.ReturnBall` |
| Aim | depth | 3 phone buttons (default 8.9 m) | **swing power**: 0.45-0.95 of the court depth | `TennisRules.DepthFromPower` |
| Aim | scope | | motion play only (`TimingAims`); touch keeps its pad, keyboard its arrows, self-play its script | `TennisGame.TimingAims` |
| Aim | online | phone aim + depth buttons | same rule, mirrored for the far seat | `NetworkTennisMatch.Return` |
| Aim | hit badge | `EARLY`/`LATE` | `EARLY · LEFT`, `STRAIGHT`, `LATE · RIGHT` | `TennisRules.TimingAimWord` |
| Movement | reaction time | 0.24 s | 0.15 s | `TennisRules.ReactionTime` |
| Movement | acceleration / braking | 10 / 14 m/s² | 16 / 18 m/s² | `TennisRules.Acceleration`, `Deceleration` |
| Movement | human top speed | 4.93 / 5.81 m/s | 5.38 / 6.34 m/s (the CPU keeps ×0.88) | `PlayerMovementScale` |
| Movement | sprint boost | needs a lean | **automatic** for a ball not reachable at a plain run | `TennisRules.EarnsAutoJump`; `TennisGame.AutoJumpAssist` |
| Movement | stamina | drain .23, floor ×0.72 | drain .11, floor ×0.85 | `StaminaStep`, `StaminaSpeedFactor` |
| Movement | dive | 1.65 m, 4 s cooldown, 1.1 s down at 8% speed | 2.1 m, 2.5 s, 0.8 s at 30% speed | `TennisAbilities`, `TennisRules.DiveRecovery`, `GroundRecoverySpeed` |
| Clock | `GameSpeed` | 1.2 | 1.1 | `TennisGame.GameSpeed` |
| CPU | top rally pace | Club 23, Pro 33, Boss 34 | Club 22, Pro 28, Boss 30 (hammer/wildcard trimmed) | `TennisOpponentProfile.cs` |
| CPU | arc over the tape | .28 m | .45 m | `TennisGame.SendFromOpponent` |
| CPU | rally builder | none | first 6 balls: targets 40% narrower, 15% softer, no wrong-footing; never changes its error chance | `TennisOpponent.RallyEase` |
| CPU | unreturnable serves | up to 35% | up to 15%; the swing lead counts | `TennisRules.UnreturnableServeShare`, `ServeSwingLead` |
| Serve | pace | 0.75 | 0.90 | `TennisRules.ServePace` |
| Serve | wind-up after TOSS | 0.5 (0.67 s real) | 0.3 (0.33 s real) | `TennisRules.ServeWindUp` |
| Serve | perfect swing window | ±25 ms | ±45 ms | `ServePerfectRealSeconds` |
| Serve | perfect toss window | ±12.5 ms | ±30 ms | `ServePerfectToss` |
| Latency | TOSS, DIVE, touch SWING | fire when the finger lifts | fire when it lands (-60 to -120 ms) | `TennisPlayViews.swift` `PressButton` |
| Latency | swing feedback | none until contact | one light haptic tick when the phone **confirms** a swing (not on raw onset: Plan 2) | `TennisGame.SwingConfirmedTick` |
| Latency | audio buffer | 1024 samples | 512 | `ProjectSettings/AudioManager.asset` |
| Online | hit sound, burst, haptic, bounce | none (view only drew snapshots) | played when the host's contact / bounce counter changes | `TennisGame.Network.cs` |
| Telemetry | | none | one `[TennisBalance]` log line per point | `TennisGame.AwardPoint` |
| Copy | how-to cards, tips, tutorial, coach | racket-face aiming | timing aiming | `HowToCards.swift`, `TennisTutorial.cs`, `TennisCoach.cs` |

The three depth buttons are gone from the phone's motion-play screen (touch play keeps its aim pad).

## What was and was not verified

Verified, without Unity or Xcode:

- `Tools/netsim` (runs in CI, `dotnet test Tools/netsim/NetSim.csproj`): 81 tests pass, including the new timing-aim and
  rally-balance tests and the online timing tests for both seats.
- A wider scratch harness that also compiled `TennisMatchTests`, `TennisCampaignBalanceTests`,
  `TennisGameplayImprovementTests`, `TennisRulesTests`, `ScreenTimingCalibrationTests`: 94 tests pass.
- Rally simulation (`TennisRallyBalanceTests`): a human who is a little late and misses one ball in twelve, against the CPU's
  real decision code. Mean rally, shots. Before: Standard 13.9, Hard 3.2, Pro 1.8, Boss 1.7 (a phone player with no lean).
  After: Standard 17.6, Hard 20.4, Pro 22.6, Boss 23.5. The simulation models reach and whiffs only; real rallies will be
  shorter (latency, aim noise), so treat it as a floor on the reach problem, not a promise.
- C# and Swift syntax (`python3 Tools/check-syntax.py`).

**Not run: Unity PlayMode tests, Xcode unit and UI tests, anything on a device.** The Swift controller changes
(`PressButton`, removed depth buttons, copy) and the Unity-only code (`TennisGame.Network.cs`, `ReturnBall`) were reviewed but
not compiled. Check the `build-and-test` and `ui-tests` CI jobs.

## Playtest checklist for this build

1. Aim: swing a touch early, on the ball, a touch late. Expect left, straight, right and the badge to say so. A full
   swing should land deeper than a soft one. Is the flat "straight" band (±60 ms) the right width?
2. A hard CPU (Pro/Boss) should now be a fight, not a first-ball loss. Count the longest rally over three points (target ≥ 6).
3. TOSS, DIVE and (touch) SWING should respond the instant the finger lands.
4. Serve: from TOSS to the ball leaving the hand is about a third of a second; a "PERFECT TOSS" should be reachable.
5. Online: hit sounds and the click in the striker's hand should now play; the second player's "early = left" should match
   their own screen.
6. Pull the device log and look for `[TennisBalance] point ...` lines: rally length, how points end, reachable balls,
   good jumps, dives, last lateness and the learned lag.

## Left for later (needs a device or a bigger change)

- **Swing detector thresholds** (`SteeringFilter.swift`: rate 4.2, force 0.45, hold 30 ms, arc 0.75). Cutting them would
  speed confirmation by 30-90 ms but risks false swings from steps; tune against real `swingtrace` lines, not by guess.
- **TV delay**: wired HDMI and the display link / drawable queue are the real fixes; measure with the existing delay probe.
- **Online prediction** (own hit shown before the host's reply), unreliable redundant input sending, 60 Hz snapshots.
- Judging a serve's perfect at contact rather than at swing onset.
- Docs `rally-contact-quality.md` and `serve-and-court-pace-tuning.md` describe older numbers; this file supersedes them.
