# iPhone Golf Simulator

An iPhone-first, motion-controlled golf game where the player's body is the controller.

## Current focus

The first playable milestone is **Arcade Mode**: generous swing recognition, a small club set, believable shot variety, and immediate visual feedback. The architecture keeps observed pose data separate from estimated golf metrics so Assisted and Simulation modes can become more realistic without replacing the core pipeline.

The current arcade foundation includes:

- live front-camera capture and Apple Vision body-pose tracking
- an on-screen skeleton and tracking-confidence feedback
- address, backswing, downswing, impact, follow-through, and finish detection
- right- and left-handed interpretation
- Driver, Iron, Wedge, and Putter selection, including swipe gestures
- forgiving contact, shot-shape, distance, and ball-flight estimates
- progressive backswing tension and a synchronized two-stage impact haptic, with visual feedback for tripod play
- deterministic demo swings for simulator/UI testing

The next validation step is tuning the state-machine thresholds with real swings on a physical iPhone. Those measurements should drive the model before adding a 3D range or deeper progression systems.

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
