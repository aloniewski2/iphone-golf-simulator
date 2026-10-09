# iPhone + Unity integration

## Open the correct workspace

Open **GolfArcadeUnity.xcworkspace**, select the **GolfArcade** scheme and a physical iPhone. The existing bundle identifier is `com.aloniewski.GolfArcade`.

`GolfArcade.xcodeproj` is the native-only Simulator/test target. It intentionally reports that Unity requires the integrated device build; running that project alone does not embed Unity. `GolfArcadeUnity.xcodeproj` adds the generated UnityFramework dependency and copies Unity Data into the host bundle. Do not open the standalone Unity-iPhone project for the combined app.

Prerequisites: Unity 6000.3.24f1 with iOS Support, Xcode 27 for the conditional iOS 27 scene-accessory API, and XcodeGen. Deployment target remains iOS 17. The Unity iOS module on this Mac is at `6000.3.24f1/PlaybackEngines/iOSSupport`, beside the Unity app; it is recognized by the editor. The earlier inspection of only `Unity.app/Contents/PlaybackEngines` missed it.

## Reproduce the build

1. Open `Unity/` in the exact editor version. Save work and leave Play mode.
2. Choose **Golf Arcade → Build iOS Xcode Project**. Both Golf and Tennis must be enabled. Read `Unity/Library/BuildResults/ios.txt`; require success before continuing. This regenerates `Unity/Builds/iOS` without touching native source.
3. From the repository root run `zsh Unity/Tools/build-integrated-ios.sh`. This regenerates the integrated Xcode project and performs an unsigned Release iPhone build. Exported/generated Unity build products are deliberately not committed.
4. Open the integrated workspace and select your signing team, or use `SPORTS_TEAM=<your-team> SPORTS_DEVICE=<your-device-id> zsh Unity/Tools/install-phone.sh`. This signs, installs and launches the existing app identity. It does not silently use another developer's account or modify bundle identifiers. Installing under an incompatible existing signing identity may require user-directed migration; do not delete the old app and its data automatically.

Re-export Unity after C# or Unity asset changes. Native-only changes require an Xcode rebuild, not an asset re-export. Framework and app bundle identifiers must remain distinct.

## Implemented architecture

- Native SwiftUI sport/player menu replaces all old game entry points. Old source and stored camera calibration remain preserved, but camera scanning is not a launch requirement. Exposed games are the current Cliffside golf course and Tennis Rally, not the old course list or multiplayer.
- `SportsRuntime` hosts one UnityFramework runtime. Low-frequency version-1 JSON commands address `NativeSportsSession`. A mutex-protected native queue holds at most 64 motion inputs and 64 events, each capped at 4096 bytes. Events are polled rather than calling released Swift objects.
- Inputs include session ID, shared monotonic timestamp, physical steering target, swing sequence/power, attitude, angular rate and gravity. Managed code rejects obsolete sessions, duplicates, stale/future timestamps and non-finite target/power values. Native and Unity controllers do not compete for gameplay input.
- Native profiles own identity, skin, handedness, audio and haptics. Optional new Codable storage fields preserve existing `players.v1` decoding. Session summaries use a separate `sports.sessions.v1` key, capped at 100, without changing old results.
- Golf uses its existing calibrated motion detector for load, wrist angle, power and club behavior. Touch mode supplies explicit synthetic impacts. Tennis uses physical translation targets and the existing collision/quality/stamina rules. Both identities can use mirrored left-handed runtime geometry with unchanged equipment/hand grouping; detailed visual device acceptance is still required.
- UIKit external scene registration supports pre-iOS-27 delegates and iOS-27 scene accessories. Unity's iOS multi-display API activates display 1 and routes game cameras/canvases there; UIKit keeps the controller on the phone. Simple mirroring without an independent Unity display reports an error. Native preview uses display 0 with touch overlays.
- Disconnects, backgrounding and missing input pause play. Resume is explicit. Reconnect reroutes the Unity cameras; on-phone preview, pause, restart, refeed, aim, clubs, calibration, sound/haptics and return-to-menu require no keyboard.

## Physical tracking

ARKit world tracking supplies phone displacement; Core Motion supplies swing samples. No acceleration integration, camera recording, image upload or camera-body scan is used. Physical tennis steering requires supported hardware and camera authorization. Golf does not request camera tracking.

Face the external display with the rear camera uncovered. Establish reliable tracking, hold the phone neutral, tap Calibrate, then Resume. Configurable side travel is 15–75 cm (default 35 cm), with a 6% dead zone and exponential filtering. The lateral position maps to a bounded court target; acceleration/stamina stay in Unity.

State machine: calibrating → steering → swinging → recovering; invalid tracking enters trackingLost and requires recalibration. At 3 rad/s, freeze steering. At ≥4.5 rad/s after a 45 ms detection interval, emit one tennis swing. After 0.46 seconds, recovery requires 250 ms below 1.5 rad/s, then rebases the neutral origin without moving the current target. Camera motion blur, occlusion and poor lighting can interrupt physical steering; the explicit fallback is touch, never hidden tilt substitution. These are initial tuning values, not established device performance guarantees.

## Verification and release gates

Verified in this development environment:

- Standalone Unity iPhone export and unsigned native compilation succeeded.
- Combined app compilation succeeded; inspected package contains UnityFramework, Data and bridge exports.
- 240 native unit tests and the new navigation test passed, including profile migration and deterministic tracking-state checks. Old SceneKit-specific UI flows are intentionally no longer part of the new navigation test run.
- 66 Unity EditMode and 5 PlayMode tests passed. PlayMode includes both sports × both identities × both handedness options. These verify launch/routing/rig availability, not external-display rendering or biomechanical accuracy.

**Not verified / release blockers:** signed physical-device launch, external AirPlay and wired rendering, controller/display independence on the receiver, reconnect behavior on hardware, actual tracking reliability and swing tuning, detailed left-handed grip visuals, sustained 60 fps/thermal behavior, and end-to-end display latency. The user explicitly deferred the external-display gate until implementation was done. Do not describe this as a device-validated or release-ready game yet.

Hardware acceptance checklist:

1. Launch a signed build; choose each sport and both characters/hands. Confirm no visible arms and correct equipment grip throughout motion.
2. Connect a wired display and an AirPlay receiver separately, before launch and during a session. TV must show landscape gameplay while the phone retains portrait controls. Verify no duplicate rendering windows or black frame after reconnect.
3. Move the phone left/right without swinging, then perform 20 forehand/backhand swings. Verify correct steering direction, single hits, no return-hand steering jump and no stuck controls.
4. Cover the camera, dim the room, background the app, disconnect the display and deny camera permission. Require clear paused/error states and working recalibration/touch fallback.
5. Repeat start/pause/end/sport-switch 10 times and check profile preservation. Profile combined ARKit + Unity over 10 minutes; report render frame time separately from AirPlay latency. No 120 fps claim is made.

Reference APIs: [Unity as a Library](https://docs.unity3d.com/6000.3/Documentation/Manual/UnityasaLibrary-iOS.html), [Unity iOS multi-display](https://docs.unity3d.com/6000.3/Documentation/Manual/MultiDisplay.html), [UIKit external displays](https://developer.apple.com/documentation/uikit/presenting-content-on-a-connected-display), [ARKit world tracking](https://developer.apple.com/documentation/arkit/arworldtrackingconfiguration).
