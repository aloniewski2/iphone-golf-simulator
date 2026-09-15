# iPhone Golf Simulator

An iPhone-first, motion-controlled golf game where the player's body is the controller.

## Current focus

The first playable milestone is **Arcade Mode**: generous swing recognition, a small club set, believable shot variety, and immediate visual feedback. The architecture keeps observed pose data separate from estimated golf metrics so Assisted and Simulation modes can become more realistic without replacing the core pipeline.

## Requirements

- Xcode 26 or newer
- iOS 17 or newer
- A physical iPhone for camera and Vision body-pose testing

## Getting started

Open `GolfArcade.xcodeproj`, select the `GolfArcade` scheme, and run it on an iPhone. The simulator is useful for UI and deterministic engine tests, but it cannot validate live camera tracking.

The Xcode project is generated from `project.yml` using [XcodeGen](https://github.com/yonaskolb/XcodeGen). Commit both the specification and generated project when project structure changes.

## Branches

- `main`: release-ready code
- `develop`: integrated, tested work for the next release
- `feature/*`: focused implementation branches merged into `develop`

See [CONTRIBUTING.md](CONTRIBUTING.md) for the pull-request workflow.

