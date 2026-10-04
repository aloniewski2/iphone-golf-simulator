# Recorder checks
From the repo root on a Mac with Metal:

```sh
xcrun clang++ -fobjc-arc -std=c++17 -mmacosx-version-min=14.0 \
  -I Unity/Assets/Plugins/iOS Tools/PointClips/RecorderSmoke.mm \
  Unity/Assets/Plugins/iOS/SportsPointRecorder.mm \
  -framework Foundation -framework AVFoundation -framework Metal \
  -framework CoreVideo -framework CoreMedia -o /tmp/motionclub-recorder-smoke
/tmp/motionclub-recorder-smoke
/tmp/motionclub-recorder-smoke 30
/tmp/motionclub-recorder-smoke pause
xcrun swift Tools/PointClips/InspectClip.swift /absolute/path/from/saved/event.mp4
```

Harness uses actual Metal render/encode work and actual monotonic clock cadence. `30` must reject; `pause` must discard a partial point and save a subsequent complete point. Saved smoke-test files go to the current user's Documents/PointClips, as reported in the event output. Tests are sensitive to real CPU/GPU scheduling hitches by design; don't run during a heavy build. This checks native encoding, not the physical AirPlay display hook. The synthetic clip is a color sweep plus tone, not game footage.

Swift recording-state checks: `-only-testing:GolfArcadeTests/PointClipsTests` with the native simulator scheme.
