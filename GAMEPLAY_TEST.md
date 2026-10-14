# Gameplay feel test build

This build is the last commit (`2fe849f0`) plus only gameplay changes. None of the 10-12 cosmetic work is in it. Branch: `gameplay/hit-feel-responsiveness`.

- **Tennis and golf hit feel + responsiveness:** haptics, swing prediction, push motion reads, 120 Hz input, Game Mode.
- **Golf read/wind/lie pass:** from another session, applied from `work/golf-read/golf-read.patch`.
  - The putt ribbon is gone.
  - Aim dots cover half the carry and fade, and all aiming aids are drawn in still air.
  - Green grain is shown with chevrons.
  - Strong wind is rare and has a HUD arrow.
  - Rough and sand cut distance and scatter the shot, shown by a lie chip.

Not included: the golf swing *animation* fix, online versus, intro-skip votes, and the clothing, plaza and character work.

**Run it:** in Xcode, choose the **Gameplay Feel Test** scheme and your iPhone, then press Run. The scheme runs the optimised Release build, so speed and feel are real. The build is signed with team 9VP5B3Q5VQ.

## A/B switches (Settings)

| Switch | Location | What to compare |
|--------|----------|-----------------|
| **Swing prediction** | Controls | Strong swings count at their peak. Turn it off and the hit lands later. |
| **120 Hz swing input** | Controls | Your swing is read twice as often on a TV, and the TV stays at 60. Watch for any stutter on the TV. |
| **Hit strength** | Audio & voice | Soft, Medium or Full. |
| **Racket sound on the phone** | Audio & voice | A knock from the phone's own speaker. Check that it does not come out of the TV. |
| **Feel test** | Audio & voice | Every haptic, one per press. |

Swing prediction and 120 Hz input apply from your **next match**, so toggle between matches. Push motion reads and Game Mode are always on. When the game starts, look for iOS's "Game Mode" banner.

## What the log shows (Xcode console)

- `[SportsDiagnostic] swingtrace outcome=confirmed ... at=peak afterPeak=10ms`: how each swing was confirmed. `at=proof` is the old path. The same lines also go to `SportsDiagnostics.log` in the app's Documents.
- `[InputRate] swing input at 120 Hz, TV drawn at 60`: the fast loop started.
- `[InputRate] ... back to 60 Hz`: the TV frames ran late, so it fell back.
- `[TennisBalance] point ...`: per-point timing (`lastLateMs`, `lagMs`).

Details and gate results: `PLAN_HitFeel_Haptics.md` and `proof/hit-feel/GATE_RESULTS.md`.
