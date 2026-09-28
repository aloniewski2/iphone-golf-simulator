# BodyClearance_ServeStance (2026-09-26)

**Status:** review candidate. Stopped for approval.

**Clip:** `ArtDir/anims/captures/BodyClearance_ServeStance_Rally.mp4`

## Diagnosis
- **Stance square to camera:** in game the serve stance was over-rotated. The hips faced about 130° (partly toward the back fence) instead of about 90° side-on, so from the game camera the hero looked square-on.
- **Arm-only chop:** the swing jumped from the trophy hold to about 0.15 s before contact. That skipped the racket drop and the hip/torso unwind.
- **Racket through the body:** the racket-vs-body check covered only the torso. The head and thighs weren't tested.

## Fixes (all runtime, in `Unity/Assets/Scripts/Tennis/HeroTennisDriver.cs`; the approved clip files are unchanged)
- **Serve stance yaw:**
  - A −38° root yaw during the ritual brings the hips to about 91° (side-on to the baseline).
  - It eases to −30° by the trophy, which gives hips about 102° and chest about 132°, a 30° coil.
  - It unwinds to 0 exactly at contact, so contact and the honest hit are unchanged.
- **Serve swing:** clip time now runs through the whole trophy → racket drop → unwind → contact section (1.80 → 2.33 s) over the real swing time. Hips lead, then the torso uncoils, then the arm. The contact frame is unchanged.
- **Body proxies (`HeroBodyProxy.cs`):** head ellipsoid and thigh/shin capsules measured from the hero's own skinned vertices. Head radius is about 0.2 m, legs about 0.1 m.
- **Racket clearance (`ClearRacket`):**
  - Rotates the racket arm at the shoulder, then the wrist, by the smallest angle that clears the torso, head and thighs.
  - It keeps a 2 cm margin from the body and 6 cm from the head.
  - On the two-hander the top hand is carried along.
  - It is skipped in the ±0.1 s contact window.
- **Arm clearance:** the arm check now also covers the head and thighs. At the backhand contact the top hand can shift up to 5 cm on or off the handle, with an elbow swivel.

## Measured (2400-frame self-play match, before → after)
| Check | Before | After |
|---|---|---|
| Racket inside the body, frames >1 cm (player) | 7 | 0 |
| Racket inside the body, frames >1 cm (rival) | 86 | 10 (all in its contact window) |
| Arm inside the body (player) | 89 frames, max 0.113 | 48 frames, max 0.066 (backhand contact window, <1 cm per sample) |
| Feet below the court | — | 0 |
| Honest misses | — | 0 |
| Player contact gap | — | mean 0.11 m, max 0.13 m |
