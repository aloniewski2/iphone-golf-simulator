# Camera setup and reach correction — September 16, 2026

## Live-phone follow-up: preview lifecycle and ground target

The 21:37 recording showed a black preview followed by a countdown stuck at 3. Simulator-only fixtures had not verified live AVFoundation rendering or hardware engine lifetimes.

- Course scene, audio and haptic owners now use lazy `StateObject` construction. The previous eager `State` initial values constructed/discarded new resources whenever the parent received a camera update; haptic initialization also started hardware. Haptics now allocate on demand. Debug UI regressions assert one construction of each resource under 60 Hz pose updates.
- The course clock is retained in state, and confirmation also advances from monotonic incoming-frame time. Resizing retains one preview view/layer. An unchanged capture session, rotation and mirroring are no longer repeatedly assigned on every UI update.
- Camera mode starts expanded. A provisional fitting club appears before certification. The saved body scan acquires the player; bounded torso continuity then follows them rather than rechecking standing limb proportions during every swing. Ambiguous overlapping people and distant bystanders are not silently accepted as continuity matches.
- Initial course certification requires both feet. Ground is estimated just below the lower detected ankle; the old shoulder-width shaft-length cap cannot raise the ball into the air. This is a 2D ground estimate for a stationary phone, not an AR plane measurement.
- The ball is drawn after the club, with a high-contrast ground ring, pointer and label. The expanded ground-target review now lasts **five seconds**. Missing feet after certification do not move the target or force recalibration. The phone stays awake during camera play.

Physical-device verification now checks `AVCaptureVideoPreviewLayer.isPreviewing`, stable layer identity across resizing, actual saved-player certification and the transition to the avatar. Software regression fixtures are still explicitly synthetic and cannot certify real swing accuracy. A physical shot-result test is separate from the preview/setup check.

## Follow-up: locked ball and confirmation review

The second phone recording exposed repeated readiness loss and an unwanted shot. Live capture also applied a torso-based 90-degree correction after the camera connection had already oriented its pixels. That heuristic has been removed from live capture; buffer dimensions now determine the aspect. A true change in delivered camera aspect invalidates the old lock without producing a stroke.

Current behavior supersedes the first iteration below:

- Initial grip acquisition uses a 0.35-second hold with a 0.08-shoulder-width spatial allowance. At certification, image-space origin, scale, ball position and virtual shaft calibration are frozen. Later body translation or apparent scale changes move the hands, not the ball.
- The course starts expanded and keeps the full-screen surface for a five-second ground-ball/club review after certification, with a visible BALL LOCKED marker and shots disabled. It then contracts to the avatar view. The countdown does not repeat after a brief dropout or small stance change. Recenter explicitly starts a new lock/review.
- After certification there is no return-to-the-original-grip gate. A coherent multi-frame takeaway is required; one-frame jumps cannot arm a swing. Returning from an aborted held backswing must not create a new shot.
- Short tracking gaps retain the locked ball, calibration, phase and last measured visual club for up to 0.6 seconds. Only measured endpoints separated by at most 0.12 seconds may bridge a missing contact frame. No contact is evaluated while a frame is missing. Longer unseen contact yields a no-penalty cancellation/retry, not a fabricated hit or miss.
- Longer absence hides the stale club, asks the player to return into view, and preserves the ball location. Hand navigation is suppressed during certified course camera play so a swing cannot change the selected club.
- Detector export version is `arm-swing-2d-v3-locked-camera-space` (schema 2). Raw pose traces remain opt-in. The labeled synthetic UI fixture is compiled only for Debug Simulator builds, never physical-phone builds.

This is a stationary-camera, two-dimensional game space, not AR world anchoring. The phone must not move. Initial certification cannot guarantee knowledge of a subsequently occluded entire swing or exact physical clubface/depth.

### Phone acceptance check for this iteration

1. Keep both feet visible for initial ground placement. Hold an empty-handed grip and inspect the bright ground-ball marker during full-screen confirmation. Wait for the five-second countdown to end.
2. Move slightly forward/back or sideways. Confirm the virtual ball stays at the same location in the camera image and no return-to-grip prompt appears.
3. Swing intentionally: one stroke for a hit, a visible miss for a reliably observed miss, and no stroke for unobservable contact.
4. Briefly obscure a wrist; verify that ball/setup do not reset. Leave the frame longer; verify that no stale club movement launches a shot and the ball stays fixed on return.
5. Use Recenter only to deliberately choose a new setup or after moving the phone. Export a Practice Lab pose trace if the unwanted-shot case persists.

## First iteration (historical)

## Failure observed

The supplied screen recording showed an exaggerated forward lean/reach while the course remained at “Hold a comfortable grip.” The avatar was displaying a guessed 3D club before the detector had acquired a comfortable address. The video has no raw tracking trace; it does not establish which individual readiness gate failed.

## Changes

- Address acquisition uses a 0.35-second anchored spatial window with a 0.06-shoulder-width jitter allowance. Invalid grip, low confidence, missing samples and delivery gaps clear the hold. Impact motion is not low-pass filtered, and the learned address stays fixed through a stroke, including slow putts.
- Readiness reasons distinguish missing body/hands, uncertain tracking, separated hands, raised hands, returning to a calibrated grip, holding, ready and swinging. UI progress comes from the detector instead of a separate 1.5-second timer.
- Camera setup exposes Recenter grip and setup guidance. Camera errors offer touch fallback rather than asking the player to keep adjusting their stance.
- Before playable tracking, the avatar uses a neutral pose without a club. Tracking loss clears the club; valid impact/follow-through retains the measured club briefly. Touch/phone input retains its normal procedural animations.
- Retargeting caps ambiguous forward depth, redistributes projection into the image plane, and preserves bone lengths. The calibrated grip maps to the avatar's natural address; two-bone arm solving prevents stretched forearms. The clubhead still projects gameplay contact. The constrained visual hands are not a recovered physical 3D measurement.
- Practice Lab export schema 2 / detector `arm-swing-2d-v2-stable-address` includes per-reason frame counts (`readinessFrames`). Raw pose recording remains opt-in; no video/audio recording was added.

## Regression coverage

Tests cover jitter at 15/30/60 fps (iron and putter), drifting hands, invalid grip, low confidence, delivery gaps, reset, UI/detector readiness agreement, tracking loss, a noisy setup followed by one intentional impact, foreshortened bodies, bounded arms, and exact clubhead projection. Existing direction, putting, missing-impact-frame and no-accidental-stroke regressions remain required.

## Required physical-phone check

Compilation and synthetic tests are not proof of camera accuracy. Build/run the `GolfArcade` scheme from this checkout on the phone. Use empty hands and clear space.

1. Enter a course in Camera mode. With hands out of view, expect a specific setup reason and no active avatar club.
2. Tap Camera setup. Place the phone upright on a stable surface near waist height, facing the player; avoid a bright light behind the player. Keep shoulders and hands in frame.
3. Hold a comfortable joined grip below the shoulders. Expect Ready after a brief stable hold, without reaching toward the on-screen ball.
4. Make three deliberate swings, returning to address between them. Confirm one stroke per intended swing and no exaggerated reach.
5. Reposition your hands, separate them, leave/re-enter frame, and recenter. Confirm clear feedback, no accidental strokes, and recovery.
6. Repeat a gentle putt. If acquisition or contact still fails, use Practice Lab with conditions noted and optional pose recording, then explicitly export the JSON. The new reason counts distinguish calibration failure from a missed swing.

No real-device accuracy gate is certified by these changes. Low light, occlusion and severe foreshortening still require physical validation.
