# Plan 3A — AnimationFluidity_PartySports (2026-09-27)

**Status:** review candidate. Stopped for Adnan's approval. Cosmetics (3B) not started.

**Capture:** `ArtDir/anims/captures/Plan3A_AnimationFluidity.mp4` (60 fps, 20.6 s). It shows:
- the rally from the game camera;
- forehand, backhand and serve side by side (before | after, half speed);
- the ultimate charge and release;
- the point-end emote.

**Stills (this folder):**
- `forehand_`, `backhand_`, `serve_before_after.png`: 8 frames around contact, before vs after.
- `faces_blink_effort_happy.png`
- `head_back_occluder_fixed.png`

## Method
The approved `Hero_*` clips are untouched. Everything is a runtime motion layer in `HeroTennisDriver.cs`, plus the new `HeroFace.cs`. All of it is measured on the same 60 fps self-play match with the layers off (`HERO_FLUID=0`) and on.

## What changed
1. **Kinetic chain.** Each stroke clip's hip and chest orientation is sampled at 60 Hz at load. During a swing:
   - the hips play ahead of the clip (85 ms on groundstrokes, 70 ms on serves, 40 ms on volleys);
   - the chest plays ahead of the arm (45 / 35 / 20 ms);
   - the waist takes 30% of the hip turn;
   - the arms and racket stay on the clip's own time, so the contact frame is unchanged.

   The lead backs off automatically if it would turn the torso into the racket.
2. **Ease into contact.** The swing out of the takeback is re-timed with a u^1.45 curve (serve u^1.35): slow-in, fast through the ball. The start pose and contact frame are the same, so hit sync is unchanged.
3. **Upper/lower split.** A second, avatar-masked upper-body layer is added:
   - A stroke taken on the move keeps the run legs, so the feet stay on the run cycle, while arms and spine swing.
   - A standing stroke uses the clip's own footwork.
   - Legs blend in over 0.16 s and arms over 0.08 s, so there's no spine snap.
   - Planted feet now hold through the stroke (lock release distance 0.35 → 0.6 m).
4. **Serve and smash spine:** the spine and chest bend is amplified up to 1.45×, giving a real arch into the trophy and a crunch through contact.
5. **Ultimate charge (world frozen):** the body coils (hips counter-turned, chest wound back, spine arched) with a breathing pulse and a stretch. It releases into the real contact on the play camera, with a new 0.06 s hit-stop. Perfect (0.055 s) and smash (0.08 s) hit-stops are unchanged.
6. **Face (`HeroFace.cs`).**
   - **Aiming eyes:** the eye lenses are re-pivoted (baked at bind, no mesh edits) so they aim: at the ball in play, at the camera between points.
   - **Eyelids:** skin-coloured eyelid caps with a lash line are added. They blink every 2–5 s and change with mood:
     - effort squint with gritted inner corners (swing ±0.3 s, full prepare, ultimate charge);
     - happy squint on a won point, droop on a lost one, wide on a whiff.
   - **Head:** it tracks with lag.
   - **Back views:** eyes and lids hide for any camera behind the face (per-camera check).
7. **Emote lead-in:** point emotes start after a 0.14 s settle beat and blend in over 0.26 s (strokes still 0.08 s), so there's no hard pop into the victory or fail.
8. **Squash and stretch:** up to 4.5% on the root; stretch into an overhead contact and during the charge, squash on landing.
9. **Ready breathe:** chest ±2.2° and hips ±8 mm at 0.42 Hz.

## Measured (same match, 60 fps; before = layers off)
| Metric | Before | After |
|---|---|---|
| Forehand peak times vs contact (hips / chest / hand) | −217 / −217 / −67 ms (hips and chest together) | −233 / −83 / −50 ms (hips → chest → hand) |
| Backhand peak times (hips / chest / hand) | −233 / −233 / −67 ms | −250 / −33 / −33 ms |
| Serve order | chest before hips (0/1) | hips first (2/2) |
| Hips-first swings (all strokes) | 13 / 21 | 16 / 20 |
| Serve spine bend range | 22° | 33° |
| Foot slide in groundstroke frames, p90 | 12.5 m/s | 7.1 m/s |
| Arm in torso, frames > 1 cm | 111 | 53 |
| Racket through body / feet below court | 0 / 0 | 0 / 0 |
| Player honest-contact gap | mean 0.11 m, max 0.12 m | mean 0.11 m, max 0.12 m (unchanged) |

## Fixes found on the way (character solidity)
- **See-through at some angles:** body and eye skinned meshes kept frozen bind bounds, so Unity culled them mid-lunge or mid-swing. They now always update (`updateWhenOffscreen`).
- **Head occluder (Plan 1B) never worked:** its 6 mm inset was applied in 100×-scaled head-bone units, which collapsed the shell to a point. The earlier "no eyes from behind" still passed only because of that head angle. Now fixed and verified at the failing angle.

## Still open
- **Mouth:** the painted smile can't change (no mouth rig). Effort reads through lids, head and body. A mouth-shape decal set would be the next face step.
- **Run-cycle skate:** p90 about 6.7 m/s while running fast; runs are locked as sacred. The remaining groundstroke-frame slide comes from swings taken at a run.
- **Coverage:** the smash and the rival's ultimate charge weren't hit in this capture. Both are coded and share the same paths.
- **Device:** not tested on iPhone (editor Play Mode, 60 fps capture).
- **Wardrobe and design doc:** the clothes-swap robustness and the character / sport design standard doc you asked for earlier are not done; they fit Plan 3B.
