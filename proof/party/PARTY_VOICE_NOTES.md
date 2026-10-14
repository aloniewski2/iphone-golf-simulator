# Party voice engine: notes

Voice between the members of the player's own party only (a Game Center lobby of up to 4 phones). Off until the player turns it on. The phone is the controller, Unity plays game audio on the TV over AirPlay, so the phone's mic and speaker are shared with that setup. Files:

| File | What |
|------|------|
| `VoiceCodec.swift` | Pure (AVFoundation only, macOS + iOS): packet format, codecs + runtime probe, stream encoder/decoder, jitter buffer, VAD, rate limiter, test signal/metrics. |
| `PartyVoiceEngine.swift` | iOS only: `PartyVoiceEngine` (`@MainActor @Observable`), `VoiceSettings`, `VoiceAudioBackend` seam, mixer, transmit pipeline, the real `AVVoiceAudioBackend`. |
| `GolfArcadeTests/PartyVoiceTests.swift` | XCTest: codec/packet/jitter/VAD/settings (pure) + engine tests on a `FakeVoiceBackend`. |

## Wire format

16 kHz mono, 20 ms = 320 samples. Little endian, carried by the caller as base64: `[0] version=1  [1] codec  [2..3] sequence  [4..7] timestampMs since stream start  [8] flags (bit0 speech end)  [9...] payload`. Payload at most 200 bytes for every codec (209 bytes total). `VoicePacket.parse` returns nil for wrong version, too short, unknown codec, oversize, reserved flag bits, or an empty payload without the end marker; it is total on any bytes.

Codec ids (never renumber): 1 AAC-ELD, 2 iLBC, 3 pcm16. The sender picks the best codec that passed the probe; the receiver decodes by the id in each packet, so phones on different OS versions interoperate as long as each can decode what it receives (a packet whose codec a phone cannot decode is dropped silently).

- AAC-ELD: 24 kbps cap, 480-sample (30 ms) frames, decoder config (magic cookie) derived locally from the same fixed encoder settings, never sent.
- iLBC: exists only at 8 kHz, so 480 samples at 16 kHz are low-passed (31-tap sinc) and decimated to 240, 30 ms, 50 bytes (13.3 kbps); the receiver interpolates back.
- pcm16: 8-bit mu-law, 10 ms (160 samples, 160 bytes) per packet because 20 ms would be 320 bytes (over the cap). About 128 kbps, so 100 packets/s. Always works; only used if both converters fail the probe.

A speech spurt ends with a packet carrying the speech-end flag (the tail frame zero-padded, or an empty payload if the codec failed), so receivers stop waiting immediately instead of concealing for a second. Sequence numbers are random at first start and then continue across pauses and restarts (a restart at 0 would look like late stragglers to a peer that still holds the old numbers).

## What is verified and how (2026-10-11, macOS 26 / Xcode 27 toolchain, no device, no Xcode project)

Run from scripts in the session scratchpad (not in the repo): `voice-harness/run.sh` and `voice-harness/xct/run_xctest.sh`.

1. macOS harness against the real `VoiceCodec.swift` (`swiftc -O -swift-version 6`): 89 checks pass. Includes 300k fuzzed packets, 2x5000 garbage payloads per codec, 200k random jitter push/pull, real encode/decode round trips on a synthetic speech-like signal.
2. `PartyVoiceTests.swift` executed on macOS under `xcrun xctest`: 60 tests, 0 failures, also under ThreadSanitizer (no reports). The macOS build is the real test file and the real engine; the only mechanical transforms are `#if os(iOS)` -> `#if true`, the `AVVoiceAudioBackend` section + `import UIKit` replaced by a stub, and `@testable import GolfArcade` dropped. So the engine's state machine, gating, kill switch, mixing, interruption/background/config-change handling, sink, deinit are exercised for real, with the hardware behind `FakeVoiceBackend`.
3. Typechecks (Swift 6 strict concurrency, iOS 17.0 simulator SDK): `VoiceCodec.swift` + `PartyVoiceEngine.swift` clean, no warnings; `PartyVoiceTests.swift` clean against the real `GolfArcade` module emitted from those two files (`-enable-testing`, real `@testable import GolfArcade`).

Measured on this Mac (synthetic speech-like signal, best-delay SNR; not a listening test): AAC-ELD 18.9 dB at about 14 kbps average (worst payload on full-scale noise 145 bytes, cap 200); iLBC 9.0 dB at 13.3 kbps (a speech coder, low waveform SNR is expected); pcm16 37 dB. Chosen codec on this Mac: AAC-ELD. Two engines wired back to back through the fake backend: 18.4 dB (ELD), 9.2 dB (iLBC), 37.8 dB (pcm16).

## NOT verified (needs a phone)

- Echo cancellation against TV audio: `setVoiceProcessingEnabled(true)` is requested but nothing here proves it removes what the TV plays.
- AirPlay routing: whether `.allowAirPlay` keeps the game's TV sound on the TV once `.playAndRecord` is active.
- Bluetooth (HFP mic, A2DP output), wired headsets, route changes.
- Unity's own audio session: we change the process-wide `AVAudioSession` category while Unity is playing, then restore it. Whether Unity/FMOD notices, glitches or re-inits is unknown.
- `AVAudioEngine` graph on real hardware: input format with voice processing (channel count, rate), the 16 kHz `AVAudioSourceNode` into `mainMixerNode`, the tap's resampling, `isSilence` handling, tap buffer sizes.
- Interruptions (call, Siri), media-services reset, backgrounding, with the real notifications.
- Battery, thermal, memory over a long session; latency end to end over GameKit.
- Real speech quality, subjective loudness, AGC pumping.

## Device test steps

Same list as the header of `PartyVoiceEngine.swift` (two or more phones in one party, an Apple TV / AirPlay TV with game sound):

1. Voice off: game sound on the TV. Turn voice on for phone A: game sound MUST stay on the TV; read `outputRoute`. Turn off: sound unchanged, mic indicator gone.
2. Push-to-talk on A: B and C hear A; `isTransmitting` only while the button is down.
3. Open mic on A: silence sends nothing, speech opens within a syllable, about 0.3 s hangover.
4. TV echo: loud game sound, open mic on A; B must not hear the game sound. If it does, keep push-to-talk as the default and record it here.
5. Bluetooth earbuds on A; plug/unplug wired headphones mid-call.
6. Phone call / Siri / alarm mid-call: `.paused(.interruption)`, game audio recovers, voice resumes.
7. Home button / app switch: orange mic dot disappears (no background audio mode), `.paused(.background)`, resumes on return.
8. `canTransmit = false` while talking: indicator drops at once, the others stop hearing within about 100 ms.
9. 30 minute open-mic soak: battery, `ProcessInfo.thermalState`, memory.
10. `stop()`: log `AVAudioSession` category/mode/options before `start()` and after `stop()`; they must match, and Unity's audio must still be alive (stop never calls `setActive(false)`).

## Design decisions

- Hardware behind `VoiceAudioBackend` (16 kHz mono Float in and out; the backend owns resampling and channel choice). The default `AVVoiceAudioBackend` is the only code that touches `AVAudioSession`, `AVAudioEngine`, `UIApplication`. Nothing runs and nothing is touched while `state != .live`; `stop()` on a never-started engine makes zero backend calls; `deinit` closes the IO and restores the session.
- Session options and why: `.allowAirPlay` (playAndRecord otherwise drops AirPlay output, the game's TV sound would move to the phone), `.allowBluetooth` (HFP mic; spelled `.allowBluetoothHFP` under compiler 6.2+), `.allowBluetoothA2DP` (stereo earbuds output with the phone mic), `.defaultToSpeaker` (otherwise the earpiece), `.mixWithOthers` (do not interrupt Unity or music). Mode `.default`, not `.voiceChat`: `.voiceChat` changes the route policy and is reported to interfere with AirPlay; Apple's voice processing comes from the engine's input node. Previous category/mode/options are saved on start and restored on stop; the session is never deactivated. We do not set a preferred IO buffer duration (it would change Unity's latency).
- Voice processing ducking: `voiceProcessingOtherAudioDuckingConfiguration` set to minimum, so the game's sound does not dip when voice is on.
- Real-time safety: the render block takes an `os_unfair_lock` with `tryLock` (a contended lock gives one buffer of silence, never a wait), uses only preallocated memory (jitter buffer slots, FIFO, channel frame buffers, sink ring), touches lazy statics at init. Decode happens on the rx queue, encode/VAD/gate on the tx queue. The tap thread only copies and hops.
- `captureSink`: source 1 is called from the tx queue once per 20 ms frame (silence while not transmitting, so a recording stays aligned); source 0 is written into a ring by the render thread and drained by a 20 ms timer on the tx queue. Never the main actor. The sink must still return quickly and must not block on the main thread (stop/pause wait on the tx queue).
- Kill switch: `canTransmit = false` drops `isTransmitting` synchronously and the pipeline reads it every 20 ms frame; any half-framed audio is discarded (only a silent end marker goes out). Self-mute behaves the same.
- Push-to-talk never sends audio from before the press. Open mic has a two-frame pre-roll (the VAD fires a frame late).
- Jitter buffer: target delay 40-120 ms from RFC 3550 interarrival jitter plus a bump after each underrun; sequence wrap safe; reorder window 32; a jump beyond that, or a timestamp regression over 2 s, restarts the stream; late and duplicate packets dropped; one lost packet with a newer one queued is concealed in place (last real audio faded 70 / 40 / 15 %, then silence); a hole with nothing behind it is an underrun: conceal, re-prime, skip the hole once enough later audio is queued; gives up (idle) after 1 s of nothing; catches up by dropping a frame when more than target + 100 ms is queued.
- Open-mic VAD: RMS against a tracked noise floor (on at 3x floor and above an absolute 0.010, off at 2x), 300 ms hangover; stationary "speech" for 12 s (a TV) lets the floor creep up so the mic closes. No clock inside: it only sees the frames it is fed.
- Flood and size guards: parse rejects before decode; per-peer token bucket (160 packets/s, burst 80); at most 8 peers (mixer channels and decoders); state for unknown peer ids is capped.
- Added to the requested API: `VoiceEngineState.paused(VoicePauseReason)` (mic not running, resumes by itself), `resumeIfPaused()`, `codec` (the codec in use), `settings` (read back what to persist after `setPeer`), `settle()` (test hook).
- `VoiceSettings` decoding tolerates missing keys and unknown modes (an unknown mode becomes `.off`, never an open mic). The caller persists it; the engine clamps values.

## Known risks

- Echo with TV audio. The TV plays game sound, the phone is in the same room, the open mic hears it, and the party hears the game twice (their own TV plus a delayed copy). Voice-processing AEC cancels what the phone's own engine plays; it cannot know the AirPlay stream going to the TV, and the TV delay is large. Expect leakage. Mitigation in the product: push-to-talk as the default mode, and the stationary-noise rule in the VAD. If it is bad on device, options are a duck-the-game-while-someone-speaks cue, or telling players to use earbuds.
- AirPlay routing. If `.allowAirPlay` does not behave as expected with `.mixWithOthers`, the TV sound moves to the phone when voice turns on. Check `outputRoute` and step 1 first.
- Unity audio session interplay. Both Unity and this engine own the same process-wide `AVAudioSession`. Changing category fires route/interruption notifications into Unity's audio; restoring on stop is best effort (`try?`). If Unity changes its category while voice is live, we only refresh the route names on the resulting route-change notification; our category is re-applied the next time the IO restarts (`startHardware` calls `beginSession` again), not before.
- Bluetooth. HFP forces 8/16 kHz mono and can lower output quality for the earbuds; A2DP output with the phone mic is the hoped-for combination. Latency of Bluetooth adds to the round trip.
- Battery and thermal. Voice processing + engine + 50 packets/s send and receive for up to 3 peers. Open mic only transmits during speech, but the mic and VP stay on while `.live`. Not measured.
- Voice-processing side effects. On some devices `setVoiceProcessingEnabled(true)` changes the input node format (rate, channel count) and reroutes output through the VPIO unit; the code reads the format after enabling it and takes channel 0, but this is a classic source of "engine will not start" errors that only a device shows (the failure path reports `.failed` and restores the session).
- Codec behaviour across OS versions. The probe decides per process; AAC-ELD decode depends on the locally derived cookie being identical on both phones (same settings, so expected). An old build that lacks a codec drops those packets. If both phones' probes disagree there is no negotiation: the sender uses its own best codec.
- Packet rate through GameKit. pcm16 would send 100 packets/s per phone; ELD/iLBC about 33/s. GKMatch reliability mode (unreliable is right for voice) is the caller's choice.
- Privacy. Nothing here records or stores voice; `captureSink` only hands samples to whoever the app set. Age/parental gating is the caller's job through `canTransmit`.
