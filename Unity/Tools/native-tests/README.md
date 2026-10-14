# native-tests: TV session recorder checks (no Unity, no Xcode project)

Covers `Unity/Assets/Plugins/iOS/SportsAudioMixer.h`, `SportsSessionRecorder.{h,mm}`,
`SportsSessionCapture.mm`, the shared hook in `SportsPointCapture.mm`, and the Swift-app bridge
`GolfArcade/Unity/SportsRuntime.{h,mm}`. Run everything from the repo root.

## 1. Audio mixer unit tests (macOS, clang++ -std=c++17)

```
Unity/Tools/native-tests/run.sh
```

Builds `audio_mixer_test.cpp` three times (plain, `-fsanitize=thread`, `-fsanitize=address`) and
runs each. Covers: 8/16/22.05/44.1/48 kHz mono + stereo resampling (frequency/amplitude), per-source
gains, the soft limiter (bounded, monotone, transparent below the knee), underrun silence,
prefill, overrun drops oldest and keeps order, consumer stall beyond the ring, the 10 ms cadence
(N pulls = N*480 frames), the wall-clock pump clock (no drift, late-timer catch-up, stall resync),
audio PTS counting, disabled sources, invalid/NaN input, rate change mid-stream, and a
producer-thread vs consumer-thread stress test (no reorder, no duplicates).
Expected last line: `ALL AUDIO MIXER TESTS PASSED` (16 tests, 47278 checks per variant).

## 2. Syntax/type check against the real iOS SDK and the exported Unity headers

```
Unity/Tools/native-tests/compile_check.sh            # all five files
Unity/Tools/native-tests/compile_check.sh <file>...  # selected files (repo-relative)
```

Runs `xcrun --sdk iphoneos clang++ -fsyntax-only -std=c++17 -fobjc-arc -target arm64-apple-ios17.0`
with the Unity version defines, `Unity/Builds/iOS/Classes/Prefix.pch`, and include paths
`Classes`, `Classes/Unity`, `Builds/iOS`, plus a temp `UnityFramework/` shim directory (the
exported `UnityAppController.h` imports `<UnityFramework/RenderPluginDelegate.h>`; in the plain
export those headers sit in `Classes/PluginBase`). `Unity/Builds/iOS` is an older export, which is
fine for type checking. Expected: `compile check OK`.

## 3. Real recorder pipeline on macOS (extra, ~50 s, needs a Metal GPU)

```
Unity/Tools/native-tests/run_session_harness.sh              # plain
SANITIZE=address Unity/Tools/native-tests/run_session_harness.sh   # or thread
SOAK_SECONDS=120 Unity/Tools/native-tests/run_session_harness.sh   # optional memory soak
```

Compiles the real `SportsSessionRecorder.mm` for macOS (a three-symbol UIKit shim in `uikit_shim/`
stands in for `UIApplication` and the resign-active notification) and drives it with synthetic
Metal frames and synthetic game/voice/mic audio at real-time pace, then decodes the MP4 with
AVAssetReader. It checks: refusal paths, size/duration, tracks, monotonic PTS, the three tones
present at the right amplitudes, config switches (voice/mic/game off, no audio at all, 30 fps cap),
A/V alignment with flash-frame and click markers at the start, middle and end (measured pipeline
lag is about +35 ms audio late, flat over 8 s), the 90 minute limit path (shortened with
`maxMinutes`), resign-active auto-finish, "no TV frames" failure, display refusal, an unsupported
surface format, and that a snapshot of the file taken mid-recording is readable (movie fragments).
The finished file is an ordinary `ftyp + mdat + moov` MP4 (not fragmented).

## NOT verified without a device

None of the following has been run on an iPhone, an Apple TV or AirPlay:

- Real Metal capture of Unity's TV surface: the `onFrameResolved` hook, `targetColorRB` vs
  drawable texture, the encode pass inside Unity's open command buffer, the point recorder and the
  session recorder sharing one frame, `framebufferOnly`/format on the real TV surface, the GPU cost of
  a second blit at 60 fps next to the point clips.
- A/V sync on the device: the Mac numbers above come from synthetic sources. Real Unity audio
  callback cadence/size, the host app's mic and party-voice pacing, and AirPlay latency are untested.
  Voice/mic prefill is 100 ms by default, so they trail the picture by that much.
- Thermal and background auto-finish on the device: `NSProcessInfoThermalStateCritical`,
  `UIApplicationWillResignActiveNotification` (only simulated with a posted notification on the Mac),
  and whether the finalization completes inside the short background-task window.
- Display unplug: the probe-based "TV surface missing for 2 s" rule and the 15 s no-frame backstop.
- Low free space (< 1 GB refuse, < 300 MB auto-finish), the 24 h cleanup, and `NSTemporaryDirectory()`
  behaviour on iOS.
- Long runs: only ~2 minutes of soak on the Mac (flat RSS). Hour-long memory, encoder backlog drops
  and the 90 minute limit on the device are untested. The 4-frame backlog rule rarely fires on the Mac.
- Crash recovery on iOS: movie fragments let a mid-recording snapshot be read on macOS; the same on iOS
  and Photos' handling of a crashed partial file are untested.
- Saving the file to Photos (the Swift app's job) and the Swift side of `SportsRuntime` (dlsym of the five
  `SportsCapture*` symbols on a real device; only the compile of `SportsRuntime.mm` is checked).
- The Unity C# side (`CaptureAudioTapKeeper`, the one-line edit in `NativeSportsSession.Boot`) was not
  compiled or run (Unity must not be run here).
