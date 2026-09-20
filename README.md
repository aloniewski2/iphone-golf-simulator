# iPhone Golf Simulator

An iPhone-first arcade golf game with phone-motion controls, a touch fallback, and a shared phone/TV RealityKit viewport.

## Current focus

The full native rebuild is in progress. Normal Run/Profile/Archive now use RealityKit,
but final visual review, sustained performance and physical phone/TV acceptance are not
complete. See [the rebuild evidence and remaining gates](docs/REALITYKIT_REBUILD.md).

The target is Wii Sports-style readability and quick, forgiving play—not precision simulation.
Solo and multiplayer select **Sunward Resort · Nine**, the only course exposed in the menu.
The larger presentation ball does not change the authoritative physics or scoring radius.

- Swift 6, RealityKit and native SwiftUI/UIKit controls; no camera or body scan required.
- One persistent non-AR viewport that moves between phone and external display.
- Serial CoreMotion processing, explicit Ready/Cancel arming, assisted direction and touch shots.
- Worker-built trajectories preserving the existing scoring, replay, saves and turn rules.
- USDZ courses and a skinned golfer with editable appearances and handedness.
- Shot planning, flyovers, green reads, effects and automatically advancing turns/results.

## Practice

Practice uses the native controller and golfer without charging a scored stroke. Historical
camera/Practice Lab documents describe the earlier prototype, not the current main-menu flow.

## Requirements

- Xcode with the iOS 27 SDK (the currently verified build toolchain)
- iOS 26 or newer
- A physical iPhone for live CoreMotion and phone/TV acceptance testing

## Getting started

Open `GolfArcade.xcodeproj`, select `GolfArcade`, and run. Its Run/Profile/Archive actions use
native `Release`; its regression Test action uses `Debug`. Use `GolfArcadeRelease` for the
ordinary-menu Release UI tests. Simulator checks cannot certify physical swing quality or
AirPlay responsiveness, and an unsigned iPhone build is only a compile/link check.

For development comparisons, `GolfArcadeVisualAudit` uses the optimized `Comparison`
configuration. Debug/Comparison retain SceneKit; `-realityKit` selects the native path there.
Shipping Release excludes the old renderer regardless of launch arguments. The earlier
`GolfArcadeNativeOnly` configuration remains available to reproduce its recorded measurements.

The Xcode project is generated from `project.yml` using [XcodeGen](https://github.com/yonaskolb/XcodeGen). Commit both the specification and generated project when project structure changes.

## Branches

- `main`: release-ready code
- `testing`: integrated, tested work for the next release and the only permitted source for pull requests to `main`
- `feature/*`: focused implementation branches created from and merged into `testing`

See [CONTRIBUTING.md](CONTRIBUTING.md) for the pull-request workflow.
