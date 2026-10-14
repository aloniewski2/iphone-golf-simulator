# Emote standard (2026-10-12)

Adnan: the heroes feel flat emotionally. Make a standard emote set (joy, excited, mad, ...) by the best method, plus standard walk and run
cycles, and golf aim footwork. Everything has to work in **tennis, golf and the menu plaza**.
Method and reasoning: [PLAN.md](PLAN.md).

## What was made
| Clip | Emotion | Tennis | Golf | Menu plaza |
|---|---|---|---|---|
| Emote_JumpJoy | joy | equippable; auto on a won point (CelebratePoint) | result emote; hole-out on an eagle | equippable emote |
| Emote_FistPump | excited | equippable; auto on a won point (HitPerfect) | result emote; hole-out on a birdie | equippable emote |
| Emote_Stomp | mad | equippable; auto on every other lost point | result emote; hole-out at double bogey or worse | equippable emote |
| Emote_Slump | sad | equippable; auto on a lost point (SadPointLost) | result emote; hole-out on a bogey | equippable emote |
| Emote_Shock | shock | equippable; auto on a whiff (MissWhiff) | result emote | equippable emote |
| Emote_Victory | big win | equippable; auto on a won match (MatchWin) | result emote; hole-out on an ace | equippable emote |
| Emote_Defeat | big loss | equippable; auto on a lost match (MatchLose) | result emote | equippable emote |
| Walk / Run | locomotion | (tennis keeps its own walk and court runs) | golfer moves "Walk" / "Run" (no golf flow moves the golfer yet) | the plaza's walk / run / sprint |

* Two versions of every emote from one performance:
  * **tennis**: the racket stays in the right hand. Files: `Unity/Assets/Characters/MatchHeroes/<Sex>/<Sex>_Emote_*.fbx`, plus 7 slots on `PlayerMale` / `PlayerFemale`.
  * **free**: empty hands, for golf and the plaza. Skeleton-only FBX in `Unity/Assets/Resources/HeroMoves/<Sex>/<Name>.fbx`, loaded by `GolferView` and `HubHeroAnimator`.
* Faces: `MatchHeroFacePerformance` moods are happy / sad / angry (squint, low brow) / shock (wide, high brow). In tennis they follow the clip; in golf the move sets them through `ForcedMood`.
* Golf aim footwork: `Unity/Assets/Scripts/Game/GolferFootwork.cs`.
  * The shoes stay planted while the aim turns the stance round the ball.
  * They shuffle after it in small lateral steps, lead foot first.
  * Steps are at most 0.30 m; a flick past 0.45 m re-places the feet.
* Locomotion: `scripts/gait.py`.
  * Phase-function loops: frame P equals frame 0.
  * The stance foot slides back at exactly the clip speed, so feet don't slide when the game moves the root at speed / clip speed.
  * Walk: 1.2 m/s, 112 steps/min. Run: 3.5 m/s, 180 steps/min.
  * A reach guard keeps the legs at 98.5 % of their length or less.

## How it is built
* `scripts/ek.py`: the IK control-rig kit (keys are offsets from READY, PCHIP, overlap lags, chest-space wrists, aims, fingers, heel/toe roll).
* `scripts/std.py`: the per-frame pose layer, plus jump physics and flight feet.
* `scripts/clip_*.py`: one per clip.
* `scripts/build_all.sh <male|female>`: every clip (tennis + free) plus the FBX.
* Blender previews: `render_preview.py` and `tile_preview.py`.
* In-game films, in an APFS clone only:
  * `JoyFilm.cs` (modes `tennisall`, `golfall`, `huball`, `golfaim`), run through `film_all.sh` and `urun.sh`.
  * Reels are made with `reel_film.py` / `compose_beforeafter.py`.
* Install:
  * `install.sh` refuses if a hero prefab moved, keeps the originals, copies `install/Unity/**`, then runs `patch_unity.py` + `patch_swift.py`.
  * Undo: `rollback.sh`.
  * Unity wiring tool: `Unity/Assets/Editor/EmoteStandardWire.cs`. It was run in the clone; the staged prefabs differ from the real ones by exactly the 28 slot lines.
* iPhone app: `EmoteCatalog` now has 13 ids, and the TV locker focus rows grow by one row per three emotes.
* Locker preview clips come from `MatchHeroLockerExport.RigClips` (7 ids added). They need `work/hero-sync/sync_heroes.sh` after install.

## Pitfalls found
* Everything is an offset from the tennis READY crouch, which leans 11° forward with bent knees. Upright moves (walk, slump) must take that back out (`upright`).
* PCHIP eases into every extreme. Impacts (stomps, the drop to the knees) need `Tracks.impact()` to accelerate into the contact.
* Racket aims are in CHEST space. When the trunk folds, "down" in chest space is no longer down in the world; tune with MIN_RACKET_Z per frame.
* Heel lifts must roll about the floor ~3 cm ahead of the ball joint, or the sole dips 1.7 cm.
* `HeroModelImporter` turns animation import OFF for everything under `Resources/Hero/`, hence the separate `Resources/HeroMoves/`.
* The plaza camera is not tagged MainCamera, so `GameCapture.Save` skips it there.
