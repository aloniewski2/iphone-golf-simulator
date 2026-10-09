# Updated user direction — sports controls and cameras

This document supersedes previous camera/control assumptions. It records design intent, not implemented phone tracking or approved sensor thresholds.

## Tennis
- Moving the person/phone left or right drives character running across the court. Swinging the phone drives a racket swing.
- Close third-person gameplay camera behind the player. Keep the ball, racket, and reachable court visible; exact offset/FOV requires playtesting.
- Unity must distinguish locomotion from racket motion and blend run/brake/recovery with strokes. Whether tracking uses camera-assisted position or a gesture approximation remains unselected.
- No automatic hand switching: use chosen dominant hand; two-handed backhands retain support-hand contact. Explicit hand-switch behavior requires its own decision and transition.

## Boxing
- FIRST-PERSON gameplay, superseding the earlier exterior-ring gameplay camera. Exterior view remains useful for scene review/replays.
- Phone movement drives character movement; punching motion counts as a punch. Phone buttons select punch types. Exact mapping for movement versus punching, guard/dodge inputs, off-hand control, and punch-side selection remains open.
- First-person arms/gloves and full-body opponent motion are separate animation presentations. Match hit timing and prevent camera clipping; camera bob/comfort requires testing.

## Bowling
- Approach/run-up follows the person's forward movement toward the lane center. A held phone button retains the ball; release triggers ball release during the swing.
- Third-person behind bowler. Approach speed, timing, aim and release should provide skill expression; thresholds and scoring are not set here.
- Use normalized approach progress and a named release event as handoff concepts, not a fixed pre-timed canned run-up. Handling early/late release, lost tracking, cancellation and foul-line clamping requires game implementation.

## Golf
- Phone controls swing only. Third-person camera. No character locomotion tied to phone movement.
- Hips/torso must drive full swings; preserve both-hand grip and balanced follow-through. Short game needs smaller motions, not just a full swing played faster.

## Scope ownership
- Game rules, scoring, UI screens and phone button layout: user/Unity team. No UI work in this package.
- Color customization now; modular character/wardrobe customization later.
- This pass delivers additional animation concept sheets and a standard color-separated 3D equipment kit. It does not implement phone input, gameplay, new rigged character clips, or Unity integration.
