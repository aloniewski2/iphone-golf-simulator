# Lob contact audit — 2026-09-29

## Reproduction
Unity PlayMode, Tennis scene, real hero/racket, automatic movement enabled,
AutoPlay disabled. Start at (0, .035, -11.2); inject lobs from (1, 1.5, 5)
toward x=-2/0/2, z=-8.5 with apex 4.5/6/8 metres.
Test: TennisLobCinematicTests.ReachableLobsKeepTheirOverheadContact.

The original three 6 m lobs all failed: the player reached the planned spot,
attempted a smash, then rejected contact with a 30–31 cm visible racket gap.
This is a reproducible gameplay defect, not just difficult user timing.

## Causes and changes
- General assisted contact interrupted the overhead plan while the ball was
  still high. Reserve the existing PendingHit for the descending intercept;
  continue requiring real visible racket contact (22 cm tolerance unchanged).
- Prediction integrated one step before assigning time zero. Its reported
  time now includes that step.
- The normal swing pace floor could finish the smash before a descending lob
  arrived. Allow slower overhead windup pacing.
- Allow overhead preparation during the final approach, with up to .4 s lead
  and .85 m remaining travel; the player continues toward overheadStand.
- Blend a guided smash out of running legs before contact so its full body
  contact pose can meet the planned ball. Run clips themselves are unchanged.

No phone installation performed in this task. This is a deterministic injected
lob check, not proof of every possible trajectory or physical-controller timing.

## Final verification
All 9 scenarios returned successfully, one automatic smash attempt each.
Final visible racket gaps: 0.0007–0.0051 m (under 1 cm).
Unity test result: /private/tmp/lob-planted.xml; log: /private/tmp/lob-planted.log.
Per-scenario traces: Unity/Library/Captures/lob-audit/.
