# Tennis motion reference pass — 24 September 2026

## Reference and review

The supplied `RnXCAMtBqvhA7UVUrA-5RA.mp4` is the same file as
`SportsLibrary/ArtDirection/Tennis/Generated/video/tennis-gameplay-concept.mp4`
(SHA-1 `312919bc068069610fe2421c5c66faf1d76debc7`). Its full 10.04 seconds
were reviewed with frames sampled at 24 fps: opening serve, ready footwork,
groundstrokes, lateral movement, overhead smash and final celebration.

The project already contains strokes retargeted from this reference. This pass
adjusts their runtime playback and the procedural movement layered on top of them.
It preserves the existing source clips and fitted player models.

## Runtime changes

- Narrower ready stance and diagonal racket carry across the chest, with the free
  hand supporting the racket until sprinting.
- One stride clock drives feet, arms, hips and chest. Forward movement now drives
  upper-body motion as lateral movement does, including mirrored left-handed arms.
- Lower foot lift, less body bounce and lean, quicker adjustment steps, and ankle
  roll through push-off and landing.
- Direction reversals preserve steps already in flight instead of restarting the
  gait clock or snapping an unfinished step to its landing position.
- Much smaller added hop on groundstrokes, allowing the captured weight transfer
  to read clearly.
- Follow-through and landing play through the end of the stroke before returning
  to ready. Recovery no longer immediately replaces them with a separate clip.
- Head tracking and procedural ready/run layers yield to authored strokes and
  reactions, preserving the overhead stretch and celebration pose.

## Verification

Unity 6000.3.24f1 PlayMode checks passed:

- All five `TennisGameplayTests` (serve flow, shot selection, movement, contact,
  both player bodies and racket attachment).
- `TransitionsNeverPop`, including acceleration, reversal, braking, cancelled
  swings, split step and backhand preparation. An initial reversal failure was
  fixed and this test rerun successfully.
- `SimulationAndPosingFitTheFrameBudget` (existing editor CPU budget).
- `OnsetStartsTheSwingConfirmationEnablesContact` and `LateConfirmationStillHits`.
- New `ForwardRunningMovesTheFreeArmAndKeepsTheGrip`, exercising male/female and
  right/left-handed variants. It checks hand movement relative to the hips and
  racket grip error below 5 mm.

`TennisMotionReferenceTests.CaptureReferenceMotions` captures the actual fitted
player through ten repeatable motions. Pass `-motionReviewOutput <directory>` and
`-motionCaptureFps 30` to export a review sequence at normal speed. Captures were
visually checked for ready, both lateral directions, forward running, forehand,
backhand, running forehand, serve, smash and celebration.

This is a closer visual match, not a measured 1:1 reconstruction of the video.
The reference supplies one camera angle; gameplay still chooses movement paths
and adapts contact to the live ball. The performance check is in the desktop
editor, not an iPhone device profile.

## Character construction follow-up

The initial motion preview exposed a separate model defect: `real_arms.py` joined
capped arm objects into the body object without joining their surfaces. The old
floating grip hands were also approximately 7 cm inward from their wrist joints.

Both `Avatar.fbx` and `AvatarF.fbx` now use `coherent_avatar_body.py`, called by
`fit_avatar.py`. It replaces the upper-body geometry with a continuous chest,
clavicle, sleeve, upper arm, elbow and forearm surface; blends weights through the
shoulders and elbows; and positions the palms along the wrist axis. The generated
upper body must pass connected-component and closed-surface assertions before
export. Existing heads, expressions, lower bodies and animation skeleton names
are retained. The new surface has about 12,000 vertices before FBX UV splitting,
with at most four bone influences per vertex.

Rebuild each avatar with Blender:

```sh
Blender --background --python blender/scripts/fit_avatar.py -- --name Avatar --head 0.8
Blender --background --python blender/scripts/fit_avatar.py -- --name AvatarF --head 0.8
```

Use `--output <directory>` to stage exports and `--render <directory>` for Blender
inspection. `render_clip_sheets.py --avatar 0.8` uses the same body construction.

The runtime running arms now start from a downward upper arm and bent elbow,
rather than rotating the raised ready stance. The free arm counter-swings and the
racket arm has a smaller excursion. Both hands use their measured palm offsets
for support and backhand poses. Left-handed animation now equips the left hand
(previously the runtime always selected `Hand.R`). The running regression also
checks elbow height and flexion across both models and handedness settings.
