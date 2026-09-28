# Score 64 → 80 plan: Phases A–F (2026-09-27)

**Status:** review candidate. All six phases were run in one pass, because the brief said "proceed fully" instead of stopping after each phase. **Stopped for Adnan's re-score.**

## Hero capture

- `ArtDir/anims/captures/Score80_HeroCapture_60fps.mp4` (20.7 s, game camera). Contents:
  1. Serve toss cam, then a five-shot rally ending in a supercharged hit and the point.
  2. The rival's ultimate charge, cut back to your receive view.
  3. The return rally, a supercharged hit, and the point.
- `Score80_HeroCapture_BodyCam_60fps.mp4`: the same clip, with a fixed side "body cam" on the player to the right.
- Stills are in `ArtDir/score80/stills/`.

## How it was measured

`Score80CaptureTests.FilmAndMeasure` runs the same seeded autoplay match at a fixed 60 fps (`SCORE80_JITTER=0.07` gives a mix of grades). It writes `ArtDir/score80/<run>/{capture.mp4, metrics.csv, summary.txt}`.

- `baseline` = before this plan; `phaseF2` = final.
- Hair and wardrobe: `Score80WardrobeAuditTests` (magenta orbit) and `Score80HairABTests` (`HERO_HAIRFIX=0` = original materials).
- Environment: `Score80EnvAuditTests` (reaction, low, aerial angles and the flyover).
- Clip facts: `Score80ClipProbeTests`.

| Metric | Baseline | Final |
|---|---|---|
| Run-cycle foot skate, p90 | 3.55 m/s | 2.54 m/s |
| Rival stroke-window skate, p90 | 5.29 m/s | 4.06 m/s |
| Player stroke-window skate, p90 | 1.69 m/s | 1.68 m/s (the rest is crossover steps at contact) |
| Swings in hips → chest → hand order (unwind timing) | 8 / 11 | 5 / 6 |
| Racket-hand speed, last 2 frames into contact | 8.2 / 4.8 m/s (slowing into the ball) | 18.2 / 12.2 m/s (fastest into the ball) |
| Hit-stop, held frames after contact | Perfect 4, supercharged 6 | Great / Excellent 1, Perfect 2, supercharged / smash / ultimate 3 |
| Frames on a cinematic cam while the ball is live | 0 | 0 (plus a new hard guard, 0 refusals) |
| Player honest-contact gap | — | mean 0.09 m, max 0.17 m |

## Phase A: animation fluidity
All changes are in `Unity/Assets/Scripts/Tennis/HeroTennisDriver.cs`. The approved `Hero_*` clips are untouched.

- **Skate root cause:** a planted foot's lock released and the foot slid 0.35–0.6 m back along the court.
  - Now a lock that drifts past 0.2 m turns into a real step: the foot lifts on a 0.12 s arc and re-plants, one foot at a time (`LockFeet`).
  - The serve's grounded frames now plant too.
- **Leg source follows speed faster** (0.5 → 1.8 m/s over 0.14 s): a follow-through while you run back to the middle rides running legs instead of skating on stroke legs.
- **Plant beat:**
  - The body leans into its acceleration (braces back into a stop, drives forward out of one), up to 8.5°, 80% at the waist.
  - A hard stop gets a small underdamped knee dip (≤ 5 cm, legs re-solved so the feet stay put).
- **Kinetic chain:**
  - The hip and chest leads are now in real time. Lead taken in clip time collapsed where the eased swing is fastest.
  - Groundstroke leads: hips 140 ms, chest 80 ms, with the hip lead ×1.3.
  - The pelvis turns on pinned feet, so the legs don't pop.
- **Swing curve.** Probe finding: the approved clips square the strings at contact, where the racket is slow (forehand 4.8 m/s); the fast part is about 110 ms earlier and edge-on. So the contact frame stays (face lock), and:
  - the ease into the ball is steeper (u^1.75);
  - a release burst follows the hit-stop (+1.5× clip rate, 90 ms decay): freeze, then the racket snaps out through the ball.
- **Overshoot / settle:**
  - A soft spring on chest and neck (3.1 Hz, ζ 0.4) runs outside the swing-to-contact window, so follow-throughs and recoveries carry on a few degrees and settle.
  - Weight is light on runs (the run cycle is authored).
- **Blends:**
  - Cross-fades and emote / recovery weights are eased (SmoothDamp), not linear ramps.
  - Stroke onset stays snappy.

## Phase B: hit weight and juice
- **Hit-stop** (`TennisRules.HitStopFor`, `HeavyHitStop`): supercharged, smash and ultimate hold 3 frames; Perfect 2; Great / Excellent 1; routine 0.
- **Body reaction:** heavy contact kicks the chest spring back and squashes the root after the hit-stop. Great gets a light version, routine none.
- **VFX:** heavy contact only adds radial impact streaks and a flat shock ring (`TennisFx.Heavy`). The rival's ultimate contact now gets the heavy set.
- **Shake:**
  - Tiered (routine 0.06 → supercharged 1).
  - Multiplied by (1 − approach), so it never shakes while you're timing an incoming ball.
- **Comic fails:**
  - A whiff kicks the torso forward and boosts the motion it already carries ×1.9, so misses overshoot more than hits.
  - A ball in the tape: a boing ring and felt puff on the net, a NET! call, and the hitter slumps (`TennisGame.Netted`).
- **Sound:** SFX already fire on the contact step (same frame), and the rival's ultimate hit is louder.

## Phase C: camera life
- **Live-ball law as code:** `TennisJuice.BallLive` refuses any cut, ball-track, reaction or unfrozen ultimate shot while a rally ball is live, and hands back to the play camera the same frame.
- **FOV pulse:** Perfect / smash / supercharged / ultimate only, ≤ 1.8° (was up to 5°), settling in about 0.2 s. Excellent no longer punches.
- **Mid-rally breath:** a slow Perlin drift on the play camera (about 5 cm, 0.2 Hz, 0.35° roll). It calms by 70% while you time an incoming ball.
- **Kept:** toss flip, ultimate charge → play cam before contact, reaction cams in dead time only.

## Phase D: hair and customize
- **Root causes** (probe: `ArtDir/score80/probe/hair_winding.txt`):
  1. 34% of `Hair_Default`'s triangles (and 56% of the body's baked side-hair layer) are wound against their own outward normals. Two-sided URP Lit lit the shell's inside with the outward normal: under the visor band and at the nape it caught the sun as bright cream "holes" (serve toss cam, ultimate close-ups).
  2. From behind, the crown tier flares over a groove above the lower back hair, and nothing (no skull) sits behind it. That was the horizontal "slice of sky" gap. It is identical with the original materials.
- **Fixes** (at load, per mesh, cached):
  - Meshes are re-wound to agree with their normals.
  - The shell's outside is drawn one-sided, plus a dark interior pass (0.36× hair colour).
  - The side-hair layer is one-sided.
  - A **hair core** is fitted inside the hair from its own shape: 70th-percentile radius per direction, face / eyes / ears kept clear, 0.6× hair colour. It fills the groove so it reads as a shadowed crease.
  - Hat liners stay two-sided. HeroKit recolours every new part with the hair colour.
- **Verified:**
  - Before/after stills: `D_toss_cam_hair_before_after.png`, `D_hair_groove_orig_top_fixed_bottom.png`.
  - A/B enclosed see-through pixels over 24 head views: 19.7k → 2.2k.
  - Slot swaps: all headwear × 2 hair colours × 6 poses sit on the head with no poke-through. The floating band in the first audit was an audit artifact: a freshly equipped hat skins a frame late.
- Racket socket and two-hand grip untouched.

## Phase E: environment
- **Sky:** `TennisSky.shader` painted every direction below the horizon warm beige. From the high flyover / aerial, the ocean plane visibly ended in a beige void. It now fades into the far sea's blue (`_Sea`).
- **Audited angles:** 9 flyover frames plus 36 reaction / low / aerial renders (`ArtDir/score80/phaseE/{before,after}`). Trees, beach house, stands, fence and wall from Plan 2D read finished at every angle. No magenta pixels anywhere.

## Pre-existing test failures (same result on the untouched scripts)
- `TennisRulesTests.HardStrokesHaveSmallerContactWindowAndReach` and `ReachAssistAcceptsNearbySwingsButNotFarOrUntimedBalls`
- `TennisTutorialTests.KitColoursParseFromTheLaunchMessage`
- `TennisGameplayTests.DisplayDelayCompensationRestoresLateSwings` and `StrengthAndPhonePositionChooseReadableStrokes`
- `TennisPolishPlayTests.TimingCheckSetsTheLagFromSwingsOnTheBeat`

Everything else in EditMode passes (153 / 156).

## Self-score (mine; yours is the one that counts)
| Category | Plan baseline | My estimate |
|---|---|---|
| Animation fluidity / body | 60 | ~72 |
| Hit feel / sync / weight | 65 | ~77 |
| Camera and spectacle | 68 | ~75 |
| Character / art in frame | 62 | ~72 |
| Juice / fun density | 65 | ~74 |
| **Overall** | **64** | **~74** |

I don't think this clears 80 yet. The hotfix loops in Phase F were the chain-lead / foot pivot and the hair-groove core.

**Smallest next fix:** re-author the run cycles' foot timing, or feed stride length into the run playback. Runs are still the largest skate source (p90 2.5 m/s) and were locked as sacred.

## Remaining debt
- **Runs:** authored run skate (locked).
- **Face:** the mouth can't emote (no mouth rig).
- **Sample size:** autoplay rallies are short (7 hits per minute); no whiff or net cord happened in the proof match.
- **Rival contact gap:** mean 0.21 m is within its honest allowance (0.26 m) but worth a look.
- **Device:** not tested on iPhone. New per-frame cost is small: one extra hair draw (7k tris), a ~500-vertex core, springs, and up to 48 streak particles on heavy hits.
- **Locker:** SceneKit shows the same hair and wasn't re-exported with the core (the locker draws two-sided and faces front).
