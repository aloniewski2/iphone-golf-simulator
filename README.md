# iPhone Golf Simulator

## Current Unity iPhone app

Open **GolfArcadeUnity.xcworkspace** for the integrated device app. Unity runs golf and Tennis Rally while native menus preserve player profiles and provide phone controls. Read [build instructions and device-verification gates](SportsLibrary/IOS-UNITY-INTEGRATION.md) before running. External-display and physical tracking acceptance are pending; the older SceneKit description below is retained as historical context, not the current app navigation.

## Standard characters and sports assets

See the [Sports Library](SportsLibrary/README.md) for the permanent male/female character standards, Blender masters, sport animations, clothing, equipment, arenas, crowds, and previews. The [Unity handoff](SportsLibrary/UNITY-HANDOFF.md) distinguishes source assets from runtime-ready work. Retrieve binary assets with Git LFS.

An iPhone-first, motion-controlled golf game where the player's body is the controller.

## Current focus

**Playable mock:** pick Solo or Multiplayer (2–4 players, each with their own body scan), choose an Easy, Medium, or Hard three-hole course, and play each hole out stroke by stroke with bunkers, water, and out of bounds. The camera stays in the bottom-left corner; a comfortable held grip sets up a shared virtual club for contact and rendering. Select any target on the course map, adjust aim through a full circle, and use full swings, pitches, chips, or gentle putts. Touch is available directly from play. The avatar copies your body, reacts to shots, and appears beside other players in multiplayer. See [the testing guide](docs/PLAYABLE_MOCK.md) and [audit implementation notes](docs/AUDIT_EXECUTION.md).

The first playable milestone is **Arcade Mode**: generous swing recognition, a small club set, believable shot variety, and immediate visual feedback. The architecture keeps observed pose data separate from estimated golf metrics so Assisted and Simulation modes can become more realistic without replacing the core pipeline.

The current arcade foundation includes:

- live front-camera capture and Apple Vision body-pose tracking
- required first-run player calibration using 15 body landmarks and 45 stable frames
- calibrated candidate selection that rejects non-matching background poses
- an on-screen skeleton and tracking-confidence feedback
- address, backswing, downswing, impact, follow-through, and finish detection
- right- and left-handed interpretation
- Driver, Iron, Wedge, and Putter selection, including swipe gestures
- forgiving contact, shot-shape, distance, and ball-flight estimates
- progressive backswing tension and a synchronized two-stage impact haptic, with visual feedback for tripod play
- deterministic demo swings for simulator/UI testing

The next validation step is testing the new input/contact model with real swings on physical iPhones. Camera direction is a signed 2D arcade estimate; it cannot measure physical club-face angle, depth, or ball launch. Device observations should drive threshold tuning before expanding course content.

## Practice Lab

Open **Practice Lab** directly from the main menu, without a player scan. It provides live pose confidence, phase and processing timing, fixed 12-second direction/power/putting/non-swing trials, explicit missed/duplicate detection counts, opt-in local pose traces, deterministic replay, and versioned JSON export. Synthetic fixtures never count toward camera benchmarks. See [the pilot protocol and metric definitions](docs/PRACTICE_LAB.md). Real-device accuracy remains unmeasured until pilot data is collected.

## Requirements

- Xcode 26 or newer
- iOS 17 or newer
- A physical iPhone for camera and Vision body-pose testing

## Getting started

Open `GolfArcade.xcodeproj`, select the `GolfArcade` scheme, and run it on an iPhone. The simulator is useful for UI and deterministic engine tests, but it cannot validate live camera tracking.

The Xcode project is generated from `project.yml` using [XcodeGen](https://github.com/yonaskolb/XcodeGen). Commit both the specification and generated project when project structure changes.

## Branches

- `main`: release-ready code
- `testing`: integrated, tested work for the next release and the only permitted source for pull requests to `main`
- `feature/*`: focused implementation branches created from and merged into `testing`

See [CONTRIBUTING.md](CONTRIBUTING.md) for the pull-request workflow.
