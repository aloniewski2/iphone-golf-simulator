# Serve / court pace adjustment

Scope: serving difficulty and movement speed only. Animation driver, assets, aim interpretation and spin unchanged.

- CourtMovementScale: 0.88. Player run 5.6 → 4.928 m/s; sprint 6.6 → 5.808 m/s. Opponent rally repositioning uses its existing difficulty/campaign speed × 0.88. Pre-serve walking and explicit dive distance are unchanged.
- Serve power timing window: ±0.50 → ±0.40 game seconds. With the unchanged 0.12 minimum power for legality, the effective legal timing half-window is ±0.44 → ±0.352 (20% tighter). ServeLegalWindow now derives from these actual judging constants.
- Perfect serve half-window: ±0.100 → ±0.065 game seconds (35% tighter). Existing top-speed reward, toss requirement, second-serve reduction and ball physics remain.
- Existing TV meter derives its perfect band from these constants, so the visual target stays aligned.

Validation: 51 selected Edit Mode tests executed; 49 pass. Two contact-assist expectations fail in TennisRulesTests (ReachAssistAcceptsNearbySwingsButNotFarOrUntimedBalls and HardStrokesHaveSmallerContactWindowAndReach). Their exercised contact-assist/evaluation functions do not depend on the constants changed here. No contact-assist tuning was made. Serve/scoring tests pass. No new phone build installed; feel needs a device playtest.
