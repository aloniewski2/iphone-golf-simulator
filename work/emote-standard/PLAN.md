# Emote standard — plan (2026-10-12)

Adnan: the characters feel flat emotionally. Build a standard set of emotes (joy, excited, mad, …), find the highest-quality way to make them first, then do a first pass of ONE emote for rating. Mid-task: **emotes must work for tennis and golf**.

## Why the characters read flat today
* The in-match point reactions (`CelebratePoint`, `HitPerfect`, `SadPointLost`, `MatchWin`, `MatchLose`, `MissWhiff`) are generated in `Unity/Assets/Editor/TennisPerformanceBuild.cs`: READY plus ONE weight envelope that lifts one arm and turns the spine a few degrees. No legs, no hips, no anticipation, no follow-through. That is most of what a player sees after every point.
* The face only has eyelid squint, a 0.6 mm brow lift and gaze (`MatchHeroFacePerformance`, `HeroFace`); the mouth is painted. Golf heroes get blinks only.
* The six authored emotes (Spike / Scuba / Thrust / Wave / BringIt / Pushups) are taunts and intros, not emotions.

## Methods considered
| Method | Verdict |
|---|---|
| Stock mocap (Meshy library behind the Higgsfield MCP, Mixamo, CMU) | Real weight, but generic "asset store" acting, no racket, needs retarget + cleanup; Mixamo needs an Adobe login and AGENTS.md keeps Mixamo out unless asked; Meshy needs paid 3D generations and its commercial terms via Higgsfield are unclear. CMU is commercial-OK (credit, no resale) but has few emotional clips. Kept as **timing reference only**. |
| AI video → pose estimation | Noisy depth/hands, heavy cleanup, still no racket. Reference only. |
| Old emote solver (work/emotes + emote-overhaul) | Works but authoring by wrist targets only; needed a separate "fluid" pass and 37 revisions to stop reading stiff. |
| **Pose-to-pose keyframing on an IK control rig, physics-timed (chosen)** | How stylised party games (Switch Sports, Fall Guys) make emotes: strong silhouettes, exaggeration, anticipation / overshoot / overlap authored in, real gravity for airtime, racket-aware. Fast to iterate with Adnan's ratings. |

## The kit (`scripts/ek.py`)
* Every channel = keys (frame, offset from READY) joined by monotone cubic (Blender auto-clamped feel); overshoot / settle authored as keys; `lag` shifts a track for overlapping action (hips → spine → chest → neck → head, shoulder → wrist).
* IK: wrists in CHEST space (ride the spine), ankles in world space, poles for knees / elbows; Blender's own IK solves each frame; the pose is read back as plain local rotations and written in the game's clip layout (LINEAR quaternions on every non-Root bone + Hips location, 30 fps). Replay check: the written clip reproduces the solved pose (≤ 0.003 mm).
* Aims: racket = shaft + string normal; free hand = knuckle direction + palm normal; fist curl on the finger hinge (local x).
* Jumps: dip → constant-acceleration push → ballistic flight (g = 9.81) → Hermite landing absorb that starts at the impact velocity; leg extension capped at 97.5 %; heel lifts roll on the forefoot.
* Two prop variants from one performance: **tennis** (racket stays in the right hand) and **golf** (`*_Golf`: no racket objects, the right hand closes into a fist — golf hides the club during emotes and plays them facing the camera, `GolferView.ClipYaw`).
* Preview: `render_preview.py` + `tile_preview.py` (game camera | front 3/4 | side, Workbench). In-game proof: `JoyFilm.cs` in an APFS clone only — tennis through `HeroTennisDriver.PlayTaunt`, golf through `GolfGame.PlayResultEmote` on the shot-result panel.

## Proposed standard set (to confirm after the first rating)
| Emotion | Emote | Where it plays |
|---|---|---|
| Joy | **Jump for Joy** (first pass) | won game / big point; golf birdie or better |
| Excited | Fist Pump "Yes!" (short, one arm, knee drive) | won point; golf good shot |
| Mad | Angry Stomp (foot stamp, fists down, head shake) | lost point; golf bogey+ |
| Sad / dejected | Slump (head drop, shoulders fall, racket/club drags) | lost point / match |
| Shock | Hands-on-head disbelief | whiff, lip-out |
| Triumph | Victory (big jump + spin or drop to knees, arms up) | match win / round win |
| Defeat | Knees + head down | match loss |
| Locomotion flavour | Happy skip / angry stomp-walk | between points, walk-ins |
Each in tennis + golf variants, both heroes, with a matching face mood. Replacing the six flat point reactions is the biggest visible win.

## Status (2026-10-12 evening)
* Adnan approved the Jump for Joy first pass ("looks good, proceed"). The whole set is built for both heroes. He then asked for:
  * standard walking and running for the menu and golf (tennis keeps its own walk);
  * golf aim footwork (shuffle steps instead of the sliding stance);
  * emotes working in tennis, golf AND the menu plaza.
* INSTALLED and staged, not committed. Details: README.md, GATE_RESULTS.md, `scripts/install.sh` / `scripts/rollback.sh`. The mood walks from the first table were dropped in favour of the standard Walk/Run.
