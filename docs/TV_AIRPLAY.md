# TV / AirPlay mode

In Settings → TV / AirPlay (or the course menu), enable **Landscape TV mode**. On the iPhone, open Control Center → Screen Mirroring and choose your Apple TV or compatible receiver. A wired external display follows the same presentation path.

The phone stays portrait for controls. Enabling TV mode or connecting a display during a round requests fresh stance/ball certification. If a ball is in flight, certification waits for the next shot without resetting or rescoring that flight. The TV shows a letterboxed camera view, the solver's frozen club/ball overlay, large stance guidance, and a five-second ball review/close-up before allowing swings. Missing/stalled tracking holds the countdown. The course view retains a camera inset so the ball reference remains visible.

Reposition through the phone's Recenter grip or TV settings → Reposition player and ball. Lower ball / Raise ball corrects the floor marker. This is front-camera calibrated geometry, not an AR-measured floor plane. The phone yields its preview to the TV while dedicated TV mode is connected and restores it on disconnect. Only one live preview is attached to the shared tracking session. The TV does not render an extra 3D course behind setup. Pose updates/s is diagnostic, not an accuracy certification. Initial player registration still uses the phone. Keep Golf Arcade foreground and the phone awake.

Course play now requests the wide-front capture profile: front ultra-wide when available, widest eligible tracking format, minimum zoom, square dynamic aspect ratio where supported, and no automatic Center Stage framing. The baseline remains available in Practice Lab for comparison. Actual coverage depends on the device; the larger TV inset is presentation only. Before certification, step back until feet and fully extended arms fit with overhead clearance. A brief loss of hands retains the ball and course view instead of reopening setup. Missing impact evidence still produces a no-penalty retry, not an invented shot.

Body turns settle over a 0.2-second stability window at address and drive the golfer/trajectory together; aim freezes on takeaway. The viewpoint does not orbit with stance aim, making that turn visible. Manual target/buttons still take priority; **Resume body aiming** restores motion control relative to the selected line, without jumping the aim or relocating the ball. Both handedness mappings remain bounded to ±30°. These are virtual aiming controls, not measured physical club-face angles.

Disable Landscape TV mode to use ordinary screen mirroring. Some receivers may need Screen Mirroring disconnected and reconnected when switching. TV mode and screen mirroring both use AirPlay when connected wirelessly; a separate landscape layout does not by itself remove streaming delay.

## Ownership and compatibility

There is one round, camera tracker and animated course scene, owned by the phone. The TV is a second renderer and read-only HUD. It never advances the round, handles swings, changes calibration or scores a shot. Disconnecting the TV releases only its window. Reconnecting observes the current round. iOS 17–26 use external scene connection; iOS 27 registers an explicit external-display scene accessory. TV mode locks the primary phone window to portrait while the external scene supports landscape.

The first implementation keeps the normal phone course presentation, providing a local reference for responsiveness. Rendering both outputs adds GPU work and must be measured on hardware.

The iOS 27 registration is compiled with Xcode 27 / Swift 6.4 or newer; older compiler builds retain the legacy delegate path. The deployment floor remains iOS 17. The supplied build uses Xcode 27.

## Delay comparison

1. Enable Show comparison flash and record the phone and TV together with another camera (preferably high frame rate).
2. Tap Flash both screens. Match the numbered flash on the two displays.
3. Extra display delay is `(TV appearance frame - phone appearance frame) / recording fps × 1000` milliseconds. Repeat at least 20 times and report the distribution, receiver, network and TV picture mode.
4. Repeat in standard mirroring with the same setup. The phone's flash is then mirrored normally.
5. Separately record physical movement and screen response together to measure end-to-end motion latency. The flash comparison does not include capture, pose inference or swing detection.

## Hardware acceptance checks

- Cold launch with a display connected; connect during a round; disconnect during flight and after impact; reconnect without duplicate strokes or a restarted round.
- Portrait phone and full-width landscape TV, including switching back to mirroring and restoring TV mode.
- Camera confirmation, countdown, aim, strike, ball flight, automatic next shot, club selection, putting, player changes and hole changes on both views.
- Foreground/background behavior, audio route, sustained frame rate, heat and tracking rate over a full round.
- Test both the iOS 17–26 scene lifecycle and the iOS 27 accessory lifecycle on appropriate devices.

External-display presentation and delay remain unverified until tested with a real receiver. Compiling the app is not an AirPlay certification.
