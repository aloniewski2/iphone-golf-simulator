# Save Last Point — 2026-09-29

## Player flow
On the tennis controller, open **Pause/Options → Settings → Recording → Record points at 60 fps** before playing. The same toggle is available in Clips. The settings panel also links to Saved clips. Enabling while paused waits until Resume to run the qualification check. A roughly four-second live encode check must pass; buffering begins at the next point boundary. **Save last point** saves the last completed point, then opens playback and the system share sheet. Available on the racket controller, classic controller, on-phone preview, and match-finish controls. The gameplay renderer is recorded, including its score HUD; UIKit phone controls are excluded. Game audio is included; no microphone/camera permission or capture.

Only the latest completed point is retained in temporary storage. Saving makes a persistent copy (up to 30 clips; delete from the library). A point includes one second after the score. A two-minute ceiling discards an overlong point rather than saving a truncated point. Pausing discards the current incomplete point, retaining the previous completed one. Ending/changing the session clears the temporary buffer, preserving saved files. Recording defaults off and is opt-in per session.

## 60 fps is a gate, not just an export label
- H.264 1920×1080, 14 Mbps, AAC stereo 48 kHz/192 kbps. Letterbox when the rendered view is not 16:9.
- Unique rendered frames only; no duplicated-frame interpolation or 30 fps fallback.
- Actual capture cadence/encoder backpressure is checked while writing. Qualification allows one second of startup, then measures over three seconds. Minimum measured rate 59.5 accommodates nominal 59.94 Hz; frame gap over 25 ms rejects the take.
- A point must also pass final cadence validation. Low Power Mode, serious thermal pressure, insufficient storage, unsupported surfaces, GPU/encoder failure disable recording with a visible reason. A previous good point remains saveable.
- The encoder uses hardware acceleration when selected by AVFoundation; we do not claim a hardware-only API switch on iOS. The real workload decides eligibility.
- AirPlay itself may be 30 Hz or drop frames independently. The captured file comes from the Unity render surface, not a recording of the receiving television. No promise about the physical TV's refresh/perceived latency.

## Code map
- `GolfArcade/Unity/PointClips.swift`: encoder events, opt-in, save/preview/library/share, safe exported-path validation.
- `SportsSession.swift`: routes recording events and commands.
- `TennisPlayViews.swift`, `SportsHome.swift`, `SportsDisplays.swift`: controls, including post-match.
- `TennisGame.cs`: two observer events only, at begin-point and award-point. No hit/serve mechanics changed.
- `NativeSportsSession.cs`: native bridge commands, point observers, pause/session/display lifecycle, attaches audio tap.
- `PointClipAudioTap.cs`: AudioListener mixed-audio tap, DSP-to-monotonic timestamp mapping, background clock reset.
- `SportsPointCapture.mm`: Unity Metal plugin registration and TV/main-display selection. Wraps Unity's own `DisplayConnection.presentWith:` to supply the exact display/command buffer missing from `onFrameResolved`; no Apple private API. Recheck this adapter after a Unity trampoline upgrade. Captures once per present, after render resolve and before presentation, on Unity's command buffer.
- `SportsPointRecorder.mm`: Metal resize/color conversion into CVPixelBuffer pool, AVAssetWriter, audio mux, cadence checks, bounded pending work, thread-safe state and audio handoff.

## Verification
- Unity iOS export: succeeded.
- Integrated Swift + Unity iOS Release build: final build succeeded (unsigned validation build; not installed on the phone).
- PointClipsTests: four tests, zero failures (disabled recording stays disabled after saving, failure preserves previous point, rejects unrelated export paths, reset clears state).
- Native Metal encoder harness: 60 fps video+audio capture PASS; 30 fps rejection PASS; pause/incomplete-point discard PASS. Real wall-clock input, no synthetic timestamp bypass of the performance gate. Early NSTimer runs correctly failed on real scheduling hitches; the harness now uses a latency-critical monotonic scheduler.
- AVAssetReader validation: 1920×1080, nominal 60 fps, encoded video samples and AAC audio track present. Test footage is a color sweep with a tone, not a gameplay/AirPlay demonstration.
- Unity AudioListener probe verified mixed game sound is delivered to OnAudioFilterRead on this Unity version.
- **Still unverified:** physical iPhone + AirPlay gameplay capture, receiver surface color/orientation, real-game audio sync, thermal endurance, and controller layout on all phone sizes. Do not call this device-qualified until tested on the actual setup.

### Device acceptance run
Connect TV; start tennis; Clips → enable; close sheet; wait for qualification; finish a full point; save and play it. Confirm TV gameplay/HUD (no controller), upright color, audible synchronized hit sound, 60 fps, and share-sheet export. Repeat from match finish. Pause mid-point; confirm that partial point cannot replace the last completed one. Force Low Power Mode/30 fps and confirm capture disables without sacrificing the old clip. Background/resume then re-enable and check audio sync. Verify a sustained match while AirPlay and encoding both run.

## Streaming / filming players (recommendation, not implemented)
1. Start with OBS on a Mac: capture the clean gameplay window from the AirPlay receiver, plus a separate phone/webcam looking at the player. Test the receiver/window/audio route before prescribing hardware. OBS supports window/display capture and camera sources: https://obsproject.com/kb/sources-guide and https://obsproject.com/kb/macos-desktop-audio-capture-guide .
2. For rear-angle social clips, use a second phone on a tripod. The controller phone cannot also be a stable rear camera. Combine a large direct gameplay panel with a smaller room/player panel; for portrait, gameplay above and the player below. Avoid relying on the small/glary photographed TV as the gameplay source.
3. Future creator flow: a synchronized start cue, portrait/landscape templates, optional player camera, and a post-match Share point prompt. Keep upload/stream initiation explicit.
4. Later, tracking the four TV corners and perspective-compositing the clean feed into that rectangle can preserve the room-shot aesthetic. It needs tracking, synchronization and player occlusion handling; it is not part of this recorder.
5. Ordinary iOS screen recording is unsuitable for this AirPlay/controller split, and Apple says screen recording cannot be used together with screen mirroring: https://support.apple.com/en-us/102653 . This feature records the app's renderer directly.

## Controller settings follow-up
Recording settings now use one shared component and `SportsSession.setPointRecording`, so controller settings and the library reflect the same encoder state. Also exposed in classic session settings. Simulator build and all four PointClipsTests passed after this change.
