# Hit feel: custom haptics and every iPhone channel (2026-10-12)

Adnan: "every hit [should] feel good, feel responsive, and feel rewarding as much as possible. Explore all iPhone technology including haptics."

Gate results: `proof/hit-feel/GATE_RESULTS.md`. Off-device harness: `work/hit-feel/run.sh`. Pattern chart: `proof/hit-feel/hit-feel-patterns.png`.

## What was wrong before this pass

| # | Problem | Effect in play | Status |
|---|---------|----------------|--------|
| 1 | Party voice runs the mic under `.playAndRecord` without `setAllowHapticsAndSystemSoundsDuringRecording(true)`. iOS's default policy mutes haptics while recording. | With voice on, every hit was probably silent. | Fixed in `PartyVoiceEngine.beginSession`. Device check still open. |
| 2 | `GolfHaptics_Strike` (every tennis hit) never restarted a stopped engine. iOS stops it on background, audio interruptions and resets. | After any trip out of the app, hits fell back to a cold, unprepared UIKit heavy tap that felt different. | Fixed. Every play path restarts the engine and retries once. |
| 3 | The engine was created lazily, on the first strike. | The first hit of a session paid the engine start-up cost. | Fixed. `Haptics.Warm()` runs when a match or the plaza starts, and on returning to the foreground. |
| 4 | The "swing confirmed" UIKit tick fired in the same frame as the strike, or just before a pending one. | "tick-THOCK" flam that muddied the hit. | Fixed. There is no tick when a hit is pending. The swing cue is held 35 ms and cancelled if a strike lands in that window. |
| 5 | The strike encoded one number (quality) plus a supercharge double tap. | The hand could not tell sweet spot from frame, early from on time, smash from volley, or how close the streak was. | Fixed: the layered hit synth below. |
| 6 | Point won/lost used UIKit success/warning at game time. | The phone buzzed 100–250 ms before the TV showed the point. | Fixed. These moments are now scheduled `Lag` later, on the engine clock. |
| 7 | Golf: the wind-up buzz carried on through the downswing. | The strike landed under the buzz. | Fixed. Tension stops at the top click, so the downswing is silent and the strike lands in a quiet hand. |

Known and not changed (truthfulness gates; see "Next"):
- **Assisted contacts:** these hit 45–180 ms after the swing peak, because the ball flies to the strings before the honest-contact check.
- **Serves:** the strike is felt 200–450 ms after the swing. The animated racket meets the toss later, and can still miss it.
- **Swing detector:** confirms 10–50 ms after the physical peak.
- **Host-authoritative online path:** feels the hit a round trip late. Versus mode, the default, is local.

## The hit language

Built in `Unity/Assets/Plugins/iOS/GolfHitFeel.h`, a plain C++ header that is tested on the Mac, and played by `GolfHaptics.mm`. Each layer is its own Core Haptics player with its own intensity curve.

| Layer | When | What it says |
|-------|------|--------------|
| **Crack** (transient, t = 0) | Every strike | Sharpness follows how clean the contact was: the sweet spot and timing, pulled halfway toward the grade the TV calls. Intensity follows stroke weight × power. |
| **Body** (continuous, 20–90 ms, decaying) | Every strike | Weight. A smash or serve carries the most, then a drive, then touch shots (volley, lob, slice, low pickup), then a putt. Long rallies add up to 15 % ("heat"). |
| **Sting** (low-sharpness buzz with ~40 Hz tremolo, 70–160 ms) | Off-centre or mistimed balls only | The frame rattling in the hand. Longer and stronger off the frame. Absent from clean balls. |
| **Reward** (after 30 ms, so it never muddies the crack) | EXCELLENT: a bright ring. PERFECT: a ring and a sparkle tap at 140 ms. SUPERCHARGED: a second crack at 70 ms and a 220 ms surge. | The grade ladder, felt. |
| **Shock** | Smash | A low aftershock at 80 ms. |
| **Pips** (220 ms +) | Each clean ball banked toward a supercharge | "Two of three." Replaced by the surge on the supercharged ball. |

Moments, sitting under the lightest strike so they never compete with it:

| Moment | Played at | Notes |
|--------|-----------|-------|
| Swing swish | When the swing is confirmed, held 35 ms | Cancelled if a strike lands in that window. |
| Whiff | Immediately | An airy whoosh. |
| Bounce | TV time | The rival's ball landing in on your side: the beat to swing off. |
| Point won / point lost | TV time | Won rises; lost falls. |
| Game, match | TV time | Rising taps and a crowd roll. |
| Golf top click | Immediately | Unchanged. |
| Golf holed | Immediately | Unchanged. |

**Two clocks.** Anything caused by the player's own arm (strike, swish, whiff) plays immediately, because the hand is the reference. Anything the player learns from the TV (bounce, point, game, match) plays `TennisGame.Lag` later: the TV-delay estimate the timing judge already uses, scheduled on the haptic engine's clock.

**Phone-speaker knock (opt-in).** `AudioServicesPlaySystemSound` "plays locally on the device speakers; it does not use audio routing". That makes it the Wii Remote speaker equivalent while the TV carries the game audio. Five synthesized knocks (tok, ting, clunk, click, tock) are written to tmp as WAV at first use and picked by grade and stroke. They are pitched unlike the TV's hit sound: Nintendo found that a remote sound identical to the TV's "lost all meaning". **Off by default until a device check confirms it stays on the phone during AirPlay.**

## iPhone technology survey (what we can use)

| Technology | Verdict | Why |
|------------|---------|-----|
| **Core Haptics:** transients, continuous, curves, scheduling, cancel | **Used** (the core of this pass) | Each layer is a player with its own curve. Moments are scheduled at `engine.currentTime + Lag`. |
| `setAllowHapticsAndSystemSoundsDuringRecording` | **Used** | iOS mutes haptics during recording by default. |
| System Sound Services (local speaker, ignores routing) | **Used, opt-in** | An on-time "pock" from the racket itself. Needs a device check under AirPlay, silent mode and the voice mic. |
| Core Haptics audio (`AudioCustom`) | Next experiment | Sample-locked audio + haptic. It goes through the app's audio session (the TV), unless a separate engine with no session plays on the speaker (UNVERIFIED). |
| Predictive contact (schedule the crack from the 100 Hz gyro trajectory) | Next | Could take back the 10–50 ms the detector confirms after the peak. |
| ProMotion / EDR flash / torch on PERFECT or SUPERCHARGED | Spectator flair only | The player is looking at the TV. A torch blink on a supercharge could be fun in a room of friends. |
| `CMBatchedSensorManager` (800 Hz) | Not usable | Apple Watch only, batched once a second. |
| Camera Control, Action button, volume buttons | Not usable in play | Capture apps only, or user-assigned shortcuts only. |
| Apple Watch haptics | Not worth it | Fixed types, 100 ms minimum spacing, WatchConnectivity is not real-time. |
| AirPods | Not useful for hits | They would also take the audio route away from the TV. |
| Game controllers (DualSense adaptive triggers) | Later, if controllers are ever supported | The same CHHapticEngine API; triggers could carry golf backswing tension. |
| AHAP authoring (Haptrix live sync, Meta Haptics Studio audio-to-haptic) | Later | For hand-tuning beyond the parametric synth. |

## Settings (Settings → Audio & voice)

- **Haptics** (existing): on/off.
- **Hit strength:** Soft, Medium or Full (0.55, 0.8, 1.0; Full is the default). Every layer scales with it, down to a 0.2 floor. Changing it plays a hit.
- **Racket sound on the phone:** the knock. Off by default; turning it on plays a hit.
- **Feel test:** each press plays the next of 22 entries and names it ("3/22 · Great drive"). Left goes back.

The app reaches the plugin through dlsym (`GolfArcade/Unity/RacketFeel.swift`), like the capture calls, so these settings need Unity loaded (the plaza or a match).

## Device checklist (Adnan)

1. Settings → Audio & voice → **Feel test**: press through all 22 entries. Does each grade feel distinct? Is PERFECT clearly the best? Is the frame sting unpleasant enough, but not too much?
2. Play a rally with a TV. Does the strike land with the swing? Does the bounce knock line up with the bounce on the TV? Does the point roll line up with the TV's call?
3. Turn voice chat on. Hits should still be felt.
4. Turn **Racket sound** on with the TV connected. The knock should come from the phone, not the TV. Check with silent mode on and off.
5. Golf: the backswing buzz, the click at the top, a silent downswing, then the strike.

## Next (ranked)

1. Device feel tuning from the checklist. The numbers live in one header.
2. Predictive crack: schedule the contact transient from the gyro peak prediction on the motion queue, with the grade layers following once Unity resolves the hit.
3. Serve haptic at the moment the swing is judged, once the honest-toss check can be predicted at commit.
4. Sensor hygiene: check that the strike buzz does not disturb the swing detector's follow-through reads.
5. A torch or screen blink on SUPERCHARGED for the room (party flair).

## Files

- `Unity/Assets/Plugins/iOS/GolfHitFeel.h` (new): the synth, knocks and feel-test list.
- `Unity/Assets/Plugins/iOS/GolfHaptics.mm`: engine lifecycle, layered playback, scheduling, swing-cue cancel, knock. Adds the AudioToolbox framework to the meta.
- `Unity/Assets/Scripts/Game/Haptics.cs`: `Hit`, `Play(Cue)`, `SwingFelt`, `Putt`, `Warm`, and silence after the golf top.
- `Unity/Assets/Scripts/Tennis/TennisHitFeel.cs` (new): tennis contact → `HitFeel`. Tests in `Unity/Assets/Tests/EditMode/TennisHitFeelTests.cs`.
- `TennisGame.cs` (rally, serve, swing tick, whiff, bounce, point), `TennisGame.Network.cs`, `GolfGame.cs` (putt), `NativeSportsSession(.Hub).cs` (warm).
- Swift:
  - `GolfArcade/Party/PartyVoiceEngine.swift`
  - `GolfArcade/Unity/RacketFeel.swift` (new)
  - `SportsSession.swift`, `HubSession.swift` (apply settings after the Unity load)
  - `TennisMenu.swift`, `IslandScreens.swift`, `ArcadeScreens.swift` (rows)

## Part 2: responsiveness (swing prediction, push motion, 120 Hz input, Game Mode)

Every change has a switch in Settings → Controls, so each can be compared on the device. Both switches default to on.

| Change | What it does | Where | Gain |
|--------|--------------|-------|------|
| **Swing prediction** | A strong swing (≥ 7 rad/s, a stroke's force, ≥ 0.3 rad turned) is confirmed when its rotation peaks. The peak is seen when the curve through the last three gyro samples tops out, or the rate has dropped 1 %. Weaker swings wait for the old proof (60 ms + 0.75 rad). Power is read from the true peak. | `SteeringFilter.peakConfirm` (switch: **Swing prediction**) | Fast swings confirm 0–10 ms after the peak instead of 40 ms (Mac gates). The strike, its haptic and the knock all move with it. |
| **Push motion reads** | CoreMotion hands every sample to the motion queue as it is measured. This replaces a 10 ms timer that read only the latest value and so lost or duplicated samples. Samples older than 60 ms when their turn comes are skipped, so a stall never replays old arm motion. | `SportsMotion.startDeviceMotion` | No dropped peaks, no 0–10 ms timer jitter, ~5 ms sooner on average. |
| **120 Hz swing input** | On a TV, the match loop runs at 120 Hz and draws every other frame, so the TV still gets a steady 60 (`OnDemandRendering`). If 10 % of TV frames in a 2 s window run late, the session falls back to plain 60 Hz. That covers a slow phone, heat, or Low Power Mode switched on mid-match. The app asks only on a ProMotion panel outside Low Power Mode. The frame governor judges drawn frames. | `InputRate.cs`, `NativeSportsSession` (switch: **120 Hz swing input**) | Swing → game decision wait halves (up to 16.7 → 8.3 ms, average ~8 → ~4 ms). |
| **iOS Game Mode** | The game category plus `GCSupportsGameMode` / `LSSupportsGameMode` let iOS 18+ recognise the app as a game. iOS then gives it the CPU and GPU first and doubles Bluetooth sampling. | `SportsInfo.plist` | Steadier frame times. Check on device that the "Game Mode" banner appears. |

The swingtrace in the device log now says how each swing was confirmed and how long after its peak: `at=peak afterPeak=10ms` or `at=proof afterPeak=40ms`. That measures the real gain from a session.

Gates: `work/hit-feel/test_swing_prediction.swift` (9 gates) and `InputRateTests` (4, EditMode).
