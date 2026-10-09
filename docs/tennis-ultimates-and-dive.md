# Tennis: three ultimates and one dive

Implemented 2026-09-27. All choices are equally available; no perks or paid ability tiers.

## Player flow

Choose one ultimate on the phone before the first Ready. Selection is remembered on this device and locked for the match. Both the AirPlay racket controller and on-phone preview expose the same controls.

Real contacts fill the meter. At 100%, tap **Arm ultimate** (tap again to cancel). Arming does not spend charge; only a qualifying real return spends it. A miss preserves charge; a new point clears the armed state. A new match resets the meter.

| Choice | Effect | Requirement |
|---|---|---|
| Skybreaker | Overhead speed boost, 1.3× capped at 34 m/s | A real smash contact; ordinary returns preserve the armed charge |
| Rescue Lob | High, deep return with a 6 m minimum apex and deep target | Next real return; it does not grant automatic contact or extra reach |
| Curveball | Sideways bend, ±4 m/s², toward the selected aim | Next real return; flat spin with a solved landing trajectory |

The AI uses Curveball at full charge. Its defensive lobs on selected pressured rallies create overhead opportunities. This is initial balance, not a competitive balance lock.

## Dive

**Dive** is the only new movement action. Tap during an incoming rally ball to travel toward its predicted position: 1.65 m over 0.42 s, four-second simulation-time cooldown, stamina cost on the attempt, including misses. The hero leans into the reach and recovers using the existing skeleton and stroke system. Successful dive saves produce a weaker high return and cannot extend the supercharge streak.

This first pass uses an additive leaning/reaching pose over existing motion, not a newly authored full-body prone dive. Actual racket contact is still required. Dive pose runs before contact assistance; the former expanded dive assist radius is disabled for player contact because physical travel now supplies reach.

Buttons are gated during pause, result screens, serve setup and cinematics. Keyboard testing: E = Dive, Q = arm/cancel.

## Integration

- `TennisAbilities.cs`: IDs, selection, arm/spend rules, dive movement and flight helpers.
- Existing `TennisGame`, ball integrator and opponent predictions share curve acceleration.
- Existing `NativeSportsSession` bridge: `ultimateSelect`, `ultimate`, `dive`; `abilities` feedback carries meter, armed flag, cooldown and availability.
- `SportsSession.swift`: persisted selection and controls; shared SwiftUI picker/actions on both gameplay surfaces.
- Hero import fix: Read/Write enabled only on FingerBody, Mixamo Bind, default Hair and Shorts used by the existing body-clearance proxy. No mesh rebuild or broad importer rewrite.

## Verification

- Unity Edit Mode: 3 passing trajectory tests (both curve directions and rescue lob landing/apex).
- Unity Play Mode: passing ability test covering selection lock, cooldown/retrigger, movement, a successful live-ball dive contact with the rendered hero advancing, unarmed full-meter behavior, overhead eligibility and all three spends/reset rules.
- Ultimate-specific spends are tested at the confirmed-contact boundary; this does not claim three complete physical-phone rallies.
- Native iOS simulator build and controller snapshot test pass. Reviewed the complete 375×667 controller: Dive and Arm ultimate remain visible with Swing and Pause.
- Captures: `ArtDir/screenshots/tennis_abilities/` (dive, ultimate states, picker, full phone controller and preview controls).
- No new physical iPhone build installed in this task. Device latency, motion-input ergonomics and final balance still need playtesting.
