# Hit feel (custom haptics): gate results, 2026-10-12

Branch `build-10/12` (uncommitted, nothing pushed). Plan, research and design: `PLAN_HitFeel_Haptics.md`.

## Off-device gates

| Gate | Result | Evidence |
|------|--------|----------|
| SHAPES_VALID: 25,209 generated strikes and every moment are patterns Core Haptics accepts (curves ≤ 16 points and in time order, values 0..1, crack first) | PASS | `gates.txt` (`work/hit-feel/run.sh`) |
| LADDER_REWARD: the felt reward after the crack rises GREAT < EXCELLENT < PERFECT | PASS | after-crack energy .0137 < .0257 < .0472 |
| PERFECT_SIGNATURE: PERFECT rings and sparkles, EXCELLENT rings, GREAT is the clean strike alone | PASS | `gates.txt`, `hit-feel-patterns.png` |
| CRISP_WHEN_CLEAN: crack sharpness centre > off-centre > frame | PASS | .91 > .65 > .41 |
| STING_ON_MISHITS: none on clean balls; off-centre 125 ms, frame 137 ms and stronger | PASS | `gates.txt` |
| WEIGHT_BY_STROKE: smash > drive > touch > putt | PASS | .058 > .037 > .020 > .010 |
| POWER_HITS_HARDER | PASS | crack .96 at full power vs .77 at a tenth |
| SHORT_HITS: ordinary ≤ 200 ms, supercharged ≤ 350 ms | PASS | 150 / 320 / pips 300 ms |
| TAPS_COUNTABLE: taps ≥ 50 ms apart | PASS | 60 ms closest |
| STREAK_PIPS: two of three = two pips; the supercharge replaces them and outweighs a plain PERFECT | PASS | `gates.txt` |
| MOMENT_SHAPES: point won rises, lost falls, match > game > point | PASS | `gates.txt` |
| CUES_UNDER_HITS: swish and bounce lighter than the lightest racket crack | PASS | .30 and .32 under .61 |
| STRENGTH_SCALES: Soft/Medium/Full scale every layer, 0.2 floor | PASS | `gates.txt` |
| KNOCKS: five speaker knocks, 40–140 ms, picked by grade and stroke | PASS | `knocks/*.wav` (listen) |
| FEEL_TEST_LIST: 22 named entries, all valid | PASS | `gates.txt` |
| Tennis contact → feel mapping: TV grade, sweet spot, lateness, streak, stroke weight, rally heat, serve and fault, online grade | PASS (8/8 EditMode) | `TennisHitFeelTests` on an APFS clone, 0 compile errors |
| Plugin compiles against the iOS 27 SDK (`-Wall -Wextra`, clean) | PASS | `xcrun -sdk iphoneos clang++ -fsyntax-only` |
| Swift app typechecks (Swift 6, whole target, simulator SDK) | PASS | 0 errors, no warnings in changed lines |
| Integrated device build (Unity export + Xcode Release) | PASS | `work/phone-build-hitfeel/` (export 0 errors, BUILD SUCCEEDED); snapshot of the working tree at 2fe849f0 + uncommitted |
| Settings reach the plugin in the shipped binary: `GolfHaptics_Configure/DemoCount/Demo` exported | PASS (after fix) | First build had them dead-stripped (feel test and strength would have done nothing); now `visibility("default"), used`, checked with `nm -gU` |
| Installed on the iPhone | NOT RUN | Waiting for Adnan; `work/phone-build-hitfeel/install.sh` installs without launching |

## Device gates (need Adnan's hands: the simulator has no Taptic Engine)

| Gate | Result |
|------|--------|
| Feel test: each of the 22 entries is distinct, PERFECT reads as the best, the frame sting reads as a mishit | NOT RUN |
| Rally with a TV: the strike lands with the swing; bounce and point cues line up with the TV | NOT RUN |
| Voice chat on: hits are still felt (`setAllowHapticsAndSystemSoundsDuringRecording`) | NOT RUN |
| Racket sound: the knock comes from the phone, not the TV, under AirPlay; behaviour with silent mode | NOT RUN |
| Golf: buzz, then the top click, then a silent downswing, then the strike | NOT RUN |
| After backgrounding the app mid-match, the next hit still uses the custom pattern (engine restart) | NOT RUN |

## Part 2: responsiveness gates

| Gate | Result | Evidence |
|------|--------|----------|
| FAST_SWING_EARLIER: a fast forehand confirms within 10 ms of its rotation peak instead of 40 ms after | PASS | `swing-prediction-gates.txt` (the app's own SteeringFilter, built on the Mac) |
| QUICK_SWING_EARLIER: a quick flat drive confirms at its peak (0 ms) instead of 40 ms after | PASS | same |
| AT_THE_PEAK, TRUE_PEAK_POWER: prediction confirms within one sample of the peak and reads power from the true peak | PASS | same |
| SLOW_SWING_UNCHANGED, WEAK_SWING_NOT_PREDICTED: a slow lob and a sub-7 rad/s push confirm exactly as before | PASS | same |
| SIDESTEP_REJECTED, FLINCH_REJECTED: no stroke from a sidestep's arm swing or a 40 ms flick | PASS | same |
| NOISE_NOT_EARLY: 300 fast forehands with 0.1 rad/s gyro noise all confirm, none more than 10 ms early | PASS | same |
| 120 Hz input only on a TV when asked, TV drawn every other loop, stop restores 60 and one drawn frame, 10 % late TV frames ends it | PASS (12/12 EditMode with TennisHitFeelTests) | `InputRateTests`; the first run caught `120 * .1f` rounding past 12, fixed with whole-number math and re-run |
| Clean gameplay test build (2fe849f0 + these changes only): Unity export 0 errors, Xcode Release BUILD SUCCEEDED, Game Mode keys and the dlsym exports present in the shipped app | PASS | `/Users/adnanyonathan/Documents/Codex/2026-10-12/gameplay-feel` (branch `gameplay/hit-feel-responsiveness`), scheme **Gameplay Feel Test** |
| Swift app typechecks with all changes, in the clean worktree and in build-10/12 | PASS | `swiftc -typecheck`, 0 errors |
| **Device: Game Mode banner appears; swingtrace shows `at=peak afterPeak≈0–10ms`; TV stays a smooth 60 with 120 Hz input (no `[InputRate] ... back to 60 Hz` in the log); rally feels sooner with each switch on vs off** | NOT RUN | Needs Adnan's phone + TV |
