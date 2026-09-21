# TV integration validation — in progress

The authoritative app is `GolfArcadeUnity.xcworkspace`, scheme `GolfArcade`.
Build success does not establish usable external-display gameplay.

## Implemented corrections

- Explicit iOS external scene declaration and connection refresh.
- Valid Unity startup arguments and Debug-compatible executable-header lookup.
- Explicit iOS secondary-display activation (iOS `Display.active` only indicates availability).
- Phone-scene primary renderer, scene-owned landscape secondary renderer.
- Fixed unmanaged input allocation; removed capacity-changing StringBuilder marshalling.
- Tennis initialization handshake and explicit gameplay-camera routing, excluding minimap render textures.
- Golf starts paused at address, not midway through its timed flyover.
- Ready/reconnect acknowledgement waits for a gameplay-camera render callback.
- Launch timeout scoped to its session; waiting screen retained until renderer acknowledgement.

## Evidence from this repair pass

- Native input queue: 100,000 alternating empty/short/4,096-byte polls, queue bound and undersized-buffer tests, AddressSanitizer + UndefinedBehaviorSanitizer: passed.
- Unity PlayMode: 5 passed, 0 failed; native-session test now waits for a rendered camera and verifies both sports, both identities, both hands, and golf address state.
- IL2CPP export inspected: input polling now passes a raw pointer directly; no StringBuilder marshalling.
- Integrated Debug build including final waiting-screen adjustment: passed and installed on the paired iPhone. External device inventory confirms a 1920×1080 wireless display; gameplay validation is pending.

## Still required before completion

- Final Debug and Release builds and installation.
- Actual AirPlay and wired TV: landscape live golf and tennis, independent portrait phone controls.
- Touch swings/rally followed by Ready-based physical steering, swings and tracking-loss behavior.
- Repeated sport switches, pause/resume, foreground/background, connection before launch, disconnect/reconnect.
- Device sanitizer/extended stability run; the historical heap-corruption report is consistent with the repaired buffer defect, but its exact allocation was not captured by a sanitizer.
- Both character variants/handedness, correct equipment and camera framing on TV.
- Sustained rendering/input measurements and recordings of phone plus TV.

A camera render callback proves the camera was rendered by Unity; it does not prove that the receiver displayed that frame. Physical confirmation remains mandatory.

## Ready / golf motion correction

- User reports the external game is now live and touch movement works; physical swings did not work.
- Code inspection found a second, hidden golf arming requirement: the old detector required the phone's top edge to point downward even after native Ready. The generic native swing event is intentionally not used for golf because it can fire during a backswing.
- Native Ready now arms golf from the next valid attitude sample. Native golf no longer requires a prescribed gravity orientation. Legacy/non-native detectors retain their existing behavior.
- Golf still distinguishes backswing from forward swing, with one impact per stroke. Native power is peak forward angular speed divided by the club's full-speed scale, multiplied by a modest 0.9–1.0 backswing factor. This makes speed the primary factor without firing a shot on the backswing.
- Motion sensor errors now surface instead of being silently dropped. Impact diagnostics include measured speed and power.
- Unity EditMode tests: 68 passed, 0 failed, including arbitrary Ready grip, speed-dependent power, and no stationary false shot. Unity iOS export succeeded. Actual physical swing validation remains pending.

## Follow-through tuning

User confirmed physical golf swings now work. They reported unintended wind-up from adjustments and shots counting without follow-through.

Native swings now require a sustained 80 ms intentional arc past a 0.35 radian dead zone before loading (putter: 0.05 radians). A forward stroke must cross the Ready orientation and continue at least 0.18 radians past it (putter: 0.035). Deceleration and timeout cancel instead of creating an impact. Power/face measurements are captured at crossing, not at the later follow-through confirmation. This adds a short confirmation delay before the shot is committed; physical feel still requires user testing.

Validation: 75 Unity EditMode tests passed, zero failed; iOS export and integrated Debug Xcode build succeeded. Tests cover small adjustments, aborted strokes before/at/just beyond center, both directions, and a gentle putt. Physical threshold tuning remains to be verified on the phone.

## Tennis motion and rally iteration

- Fixed the Ready steering basis to use portrait-oriented camera geometry rather than the raw sensor-image X axis. See [Apple ARCamera view matrices](https://developer.apple.com/documentation/arkit/arcamera). Physical left/right still requires on-device confirmation.
- Brief tracking interruptions (at most 0.6 seconds since the last reliable AR frame) freeze steering while allowing the gyro stroke recognizer to finish. Persistent loss still pauses the game; recovery never updates position until reliable tracking returns.
- Tennis gesture recognition requires a sustained 60 ms / 0.25-radian arc above 2.2 rad/s, instead of the previous 4.5-rad/s threshold. Exactly one event per stroke with settling/rebase recovery; no accelerometer integration or tilt substitution.
- Added explicit active input mode and switch-back-to-motion control. Ready remains required after switching.
- Rally openings and resets have an opponent toss-and-serve sequence. The serve lands in the service box; follow-up feeds remain training-partner behavior, not competitive match AI/scoring.
- Ball diameter increased from 0.067 to 0.20 metres with matching contact radius and a clearer trail. Return speed range reduced from 11–34 to 9–22 m/s before quality/stamina modifiers; slower feeds use recomputed ballistic arcs, not time scaling.
- Native steering harness passed: both lateral directions, one moderate stroke through brief tracking loss, sustained-loss stop, and spike rejection. Swift iPhone compilation passed. Final Unity gameplay run: 6 passed, 0 failed, including serve-to-racket alignment changes. EditMode: 75 passed, 0 failed.
- Device validation still required: camera permission, portrait Ready, physical left/right, forehand/backhand, tracking loss, and actual perceived TV ball speed/readability.

## Tennis accessibility / stroke feel

- Ready's world-horizontal axis now derives from camera viewing direction crossed with world up. This removes the previous portrait-only assumption; rolling the phone into either landscape grip leaves left/right unchanged. The rear camera must face roughly toward the TV at Ready; rotating the grip afterward retains the established axis.
- Added swept, nearby-ball contact assistance during an active swing, bounded by reach, height, timing and whether the ball has passed the player. No input means no automatic return. Precise string-bed contacts retain their quality evaluation; assisted returns have reduced power and accuracy.
- Contact timing tolerance expanded from ±0.09 to ±0.15 reference seconds. Player strokes now last 0.62–0.90 seconds depending on strength, rather than a fixed 0.46; collision timing is normalized with the animation. Serves retain their synchronized toss/contact timing.
- Soft forehand/backhand strokes use a smaller sweep; hard strokes widen the sweep. Reliable phone-side displacement (minus steering displacement) can select the side, with ball-side fallback. High balls with raised-phone input or strong power select an overhead smash, with a higher racket pose and shorter attacking trajectory. This is contextual selection, not full skeletal tracking of the user's arm.
- Deterministic Swift harness verified roll-independent steering at 0°, ±90°, 180°, plus existing motion cases. Unity logic tests: 76 passed, zero failed. Gameplay tests: 7 passed, zero failed, including variable stroke duration, side selection and overhead racket height. Native iPhone compilation passed; physical grip/contact feel remains to be verified.

## Unresolved physical tennis failure

User subsequently reports no lateral movement and TV gameplay freezing after roughly 15 seconds, while phone controls remain responsive. Prior deterministic tests did not establish usable physical tracking. No new GolfArcade crash report was present (latest listed report predates this update). Live debugger attachment did not provide a usable stack, so a render hang has not been ruled out.

Corrected one independent defect: the input watchdog now measures from the latest sample OR the latest resume time, rather than immediately timing out a resumed session against an old sample. Timeout is 0.5 seconds and also covers no first input after resume. This is not yet proven to explain the reported failure.

Added visible TV pause reasons and a bounded 256-KiB local `Documents/SportsDiagnostics.log`. Once per second it records tracking state/reason/frame age, relative motion, phase/target, Unity frame/paused state and player X, plus explicit pause causes. It records no camera images or player identity. Export/build/install succeeded; next physical reproduction is required to identify which stage stops.

## Phone facing and observed tracking loss

Retrieved device diagnostics now establish a tracking-loss pause: ARKit reported `insufficientFeatures` followed by `excessiveMotion`, then native tracking-loss pause. Unity frames continued advancing (521, 584, 651…) while paused. In reliable sections, changing native targets reached Unity and changed player X. Later stale-camera frames followed app inactivity/interruption, rather than proving a foreground renderer hang.

Added explicit facing selection using Core Motion relative orientation: initial Ready is rear-camera-to-TV; subsequent screen-to-TV means forehand for right-handers, rear-to-TV means backhand, mirrored for left-handers. Edge-on orientations retain the last choice, and each stroke latches facing at onset. Ready on resume keeps the original world-horizontal court axis and facing reference. This mapping is independent of camera tracking validity, but physical translation still requires AR tracking; no tilt fallback is silently substituted. Device verification of this new mapping remains required.

## Tennis button-swing controller

User replaced automatic tennis motion swings with a large circular Swing button and phone movement exclusively for positioning. After successful Ready, tennis now presents a dedicated controller with a central circular Swing button, Pause/Ready, status/stamina, and bottom Quit to menu. Explicit touch fallback retains a position slider. Golf is unchanged.

Tennis disables the steering filter's gesture detector, so angular speed no longer enters swing/recovery states or rebases away lateral movement. Only button taps increment the tennis swing sequence; each tap uses medium power (0.65), and current phone facing still selects forehand/backhand. Physical positional tracking still uses ARKit and can pause on sustained tracking loss; this change does not claim to solve camera occlusion or insufficient visual features.

The Swift harness passes high-rotation left/right movement without any swing events or steering freeze. The physical iPhone/external-display UI test passed (34.9 seconds): golf touch shot regression, tennis Ready, large circular Swing hit area, enabled tap, Quit below Swing, and return to menu. This UI test explicitly selected touch mode; it does not establish physical AR tracking reliability. Xcode reported a separate diagnostics-collection failure after the test passed, not a test failure.

## Tennis physical swings restored / landscape clarification

Supersedes the button-only experiment above. The user confirmed “horizontal” means landscape/sideways, not screen-up like a tray.

- Tennis accepts physical swing events again. A stroke requires sustained angular speed plus user acceleration, then accumulated rotation. Rotation/acceleration magnitudes do not depend on device roll. Translation still comes only from ARKit, never accelerometer integration.
- Steering freezes during a detected stroke. Recovery is bounded to avoid indefinite movement lock; one rebase prevents a target jump. A new stroke requires a quiet rearm, so one sustained motion does not repeatedly hit. Golf's detector is unchanged.
- Existing world-horizontal steering math passes portrait and both landscape roll tests. Real-world landscape tracking is NOT established by these tests. Diagnostics now include gravity and acceleration, alongside AR tracking reasons and Unity movement, to correlate any remaining failure with grip orientation. Camera occlusion/insufficient features still pause the game rather than silently switching to tilt.
- Motion controller removes Swing; explicit touch fallback retains a normal Swing button and strength slider. A labeled left/right aim slider remains available after Ready, with a yellow direction target on court. Quit remains at the bottom. SwiftUI controls guidance was used for labeled native controls and local state.
- Stamina resets at point end and the next serve. Strong strokes narrow timing/reach forgiveness. Soft strokes retain more assistance. Return targeting now uses a landing location and horizontal pace rather than weak velocity producing multi-second high lobs.
- Cyan prediction shows the current ballistic flight up to the next bounce; it is not an intended perfect shot. Yellow aim marks direction at medium-power depth; actual depth depends on power and contact introduces error.
- Verified: 79 Unity EditMode tests; 7 Unity PlayMode tests; 14 native integration tests executed on the connected iPhone; standalone Swift motion harness. All passed. Unity iOS export succeeded, zero errors. Physical landscape movement and real stroke feel still require user testing, including deliberate movement versus swings and both phone facings.
- Installed the integrated build and ran a dedicated `testPhysicalExternalTennisMap` on the connected phone/display: passed in 23.591 seconds. It selected Tennis Rally, launched external gameplay, pressed Ready, verified the aim slider and explicit touch fallback Swing, and returned through Quit. Screenshot shows native Tennis controller and live `Hits 0 · backhand` feedback; diagnostics show Unity frames advancing from 6 to 161 and an unpaused session with fresh input. This is a touch-mode navigation/input check, not proof of physical motion. Two earlier combined UI attempts failed at sport-picker interaction before gameplay; the dedicated test waits for picker presentation and succeeded. The app was reopened normally afterward, restoring default motion mode.
