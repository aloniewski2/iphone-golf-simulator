# Swing compare: our in-game golf swing vs pros (2026-10-12)

**References:** Adnan's two YouTube Shorts.
- `EndA6Mw2S4w` is "Top 12 Smoothest Swings in Golf | Part 1" (41 s). Part 1 holds 6 pros (#12 Cantlay to #7 Singh) across 20 broadcast clips.
- `h-JQsjjsE6Y` is "Adam Scott driver swing face on" (15 s, real time, 30 fps).

The frames were captured in the Browser pane through contact sheets (`reference/raw/`). They are third-party footage, so they stay local only: do not commit or publish them.

**Ours:** the real Unity Golf scene on the Cliffside hole 1 tee, filmed in an APFS clone of `Unity/`.
- The editor script is `scripts/SwingFilm.cs` and the launcher is `scripts/film.sh`.
- The golfer is driven through the game's own entry points. The phone backswing goes through `OnLoad` over 0.80 s. Then `StrikeToward` → `Launch` → `GolferView.Strike()`.
- Frames are captured at a 60 fps game clock from 8 cameras:
  - face-on
  - broadcast tee cam (behind, 25°)
  - down the line
  - 3/4 trail
  - from the target
  - 3/4 target
  - overhead
  - the TV game camera
- Takes: male driver, female driver, male iron. `frames/<sex>_<club>/trace.csv` holds the clip time, club head and ball per frame. `bones.csv` holds the skeleton.

## Outputs
| file | what |
|---|---|
| `film/01_our_swing_8_angles.mp4` | our swing, 8 angles, real speed then 0.5x |
| `film/02_faceon_ours_vs_scott.mp4` | face-on, ours vs Adam Scott, synced at impact, real speed then 0.25x |
| `sheets/01_our_swing_all_angles.jpg` | 8 phases × 8 angles |
| `sheets/02_faceon_ours_vs_scott.jpg` | 8 phases, ours vs Scott |
| `sheets/03_behind_ours_vs_pros.jpg` | ours (broadcast + DTL) vs Åberg, Kim, Homa, Singh, Cantlay, Schauffele |
| `sheets/05_our_male_female_iron.jpg` | male vs female, driver vs iron |
| `measurements_<sex>_Driver.txt` | body angles at address / top / impact / finish (`scripts/measure.py`) |

## Findings
### Tempo (measured; pros at 30 fps, ±0.03 s)
| | top → impact | impact → finish |
|---|---|---|
| **ours** | **0.38 s** (`GolferView` Downswing = 0.38) | settles **0.62 s**, the clip runs to **1.02 s** |
| Adam Scott | 0.21 s (top 12.74 → impact 12.95) | ~0.35 s |
| Åberg | 0.19 s | |
| Kim | 0.21 s | |
| Homa | ~0.22 s | |
| Singh | 0.19 s | |
| Cantlay | ~0.22 s | |
| Schauffele | 0.23 s | |

- Scott's backswing takes 0.59 s, so his back:down ratio is ≈ 2.8 : 1.
- Our backswing follows the player's phone (0.80 s in this film), so only the downswing and the through are ours to tune.
- In the synced video at −0.12 s, Scott is still at the top while our club is already at hip height.

### Body: OK, close to pro shape
- At the top, the shoulder turn is 100° and the hip turn 46°.
- At impact, the hips are open 29°.
- At the finish, the hips are turned 88° and the spine stands upright (6°). The lead leg is straight and the trail foot is up 11 cm.

### Body: differs from every reference
1. **Arms bent the whole way.**
   - At address the lead elbow is bent 68°.
   - At the top it is bent 38° (female 57°). The hands sit by the ear, only 9 cm above the head and 35 cm from it.
   - At impact the lead elbow is bent **93°** (a chicken wing).
   - The pros hang straight arms at address, set the hands high and wide at the top, and keep a straight lead arm through impact.
2. **Dives at the ball.** From address to impact:
   - the head drops **14.5 cm** and moves **13 cm** toward the ball;
   - the spine bend goes from 25° to **43°**;
   - the trail knee goes from 26° to **62°** flex.
   The pros' heads stay near address height and their chest stays up while the hips open.
3. **Slow, floaty through.** The release and the finish run ~2–3× the pro time (see the tempo table).

### Added on the second pass (animation only)
**Casting / early release.** The lead forearm-to-shaft hinge:
| frame | moment | hinge |
|---|---|---|
| f80 | top | 88° |
| f88 | | 57° |
| f92 | lead arm 17° above horizontal | 31° |
| f96 | | 20° |

Pros still hold about 90° when the lead arm is parallel to the ground on the way down. That early release is why our club head rises 0.6 m at the start of the downswing.

**Where the faults come from.** They are authored in the clips. I measured the raw FBX takes in Blender (`golfer_m/f.fbx`). The lead elbow bend matches the game:
| take | address | top | impact |
|---|---|---|---|
| Drive, male | 67° | 38° | 109° (the game's grip solver eases it to 93°) |
| Drive, female | 65° | 56° | 106° |
| IronSwing | 45–48° | 19° (male) / 46° (female) | 82° |
| HalfSwing | | | ~68° |
| Chip | | | ~74° |

The knees match the game exactly. So the posture and arm fixes go in `blender/scripts/golf_swing.py` and the re-export. The tempo fixes go in `GolferView` (Downswing / EaseOut / throughSeconds).

## Side notes
- Skin pokes through the polo on the chest at the top and the finish (orange patches in the face-on f80 / f164).

## Rebuild
```
scripts/film.sh logs/film.log SF_OUT=$PWD/frames SF_SWINGS=male:Driver,female:Driver,male:Iron   # clone path inside film.sh
python3 scripts/measure.py male_Driver
python3 scripts/compose.py
```
- The clone is my session scratchpad `UnityClone` (cp -c of `Unity/`). Before reusing it, refresh it from `Unity/` and copy `scripts/SwingFilm.cs` into `Assets/Editor/`.
- `SF_VIEWS=a,b` limits the cameras (`none` measures only).
- `SF_KEEP=1` keeps the earlier frames.
