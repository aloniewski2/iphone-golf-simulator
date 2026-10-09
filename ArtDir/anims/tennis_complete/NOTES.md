# TennisComplete_CustomizeSafe — characters: anims, wiring, cosmetics (2026-09-26)

**Scope, per Adnan's mid-phase note:** gameplay (ball, timing, AI, flow) was left as is. This phase covers only character animations and motion, wiring the new hero into gameplay, and customization. **Status: review candidate. Stopped for approval; sport 2 not started.**

## 1) Animation matrix
| Clip | Status | Source / path (`Unity/Assets/ArtDirection/Hero01/Models/RestoreMotion/`) |
|---|---|---|
| Ready, Serve, Forehand, Backhand (2HBH), Volley, Smash | ✅ core, **unchanged** (files date from the end of the V6 phase) | `Hero_{Ready,Serve,Forehand,Backhand,Volley,Smash}_v5.fbx` |
| Run F / R / L | ✅ core, unchanged | `Hero_Run{Forward,Right,Left}_v5.fbx` |
| Idle (between points) | ✅ new | `Hero_Idle_v5.fbx` (3.0 s loop) |
| Walk / casual jog | ✅ no new clip | Runs play at a rate scaled to ground speed (0.45×–1.5×), so slow movement reads as a jog |
| Hit_Perfect | ✅ new | `Hero_HitPerfect_v5.fbx` (1.1 s) |
| Miss_Whiff (+ stumble) | ✅ new | `Hero_MissWhiff_v5.fbx` (1.8 s: overswing, full spin, two-step stumble, recover) |
| Celebrate_Point / Sad_PointLost | ✅ new | `Hero_CelebratePoint_v5.fbx`, `Hero_SadPointLost_v5.fbx` |
| MatchWin / MatchLose | ✅ new | `Hero_MatchWin_v5.fbx` (two jumps + spin), `Hero_MatchLose_v5.fbx` (slump, hand to forehead) |
| Net touch / ball-hit react | ⏸ stub | No hook exists in the game; not authored |

**How the new clips were made:**
- Eased key poses on the same solver as the approved set: leg IK; the racket set by its direction and face angle (the hand follows through the grip socket); the left hand set relative to the forearm, so it can't invert.
- They blend in from and out to the approved Ready pose.
- Arm-through-torso check: all seven new clips have 0 frames above 1 cm.
- Humanoid, same Avatar, racket in Hand_R.

## 2) Wiring to gameplay (no gameplay changes)
**Components** (in `Scripts/Tennis/`):
- `HeroTennisDriver.cs`: the animation driver.
- `TennisHeroSetup.cs`: attaches the hero to the game's player and rival.
- `HeroCosmetics.cs`: equip/unequip and debug controls.

**How it works:**
- The game's own `TennisActor` stays the gameplay authority (movement, swing timing, racket contact test, scoring). Its body and racket are hidden; nothing about it changed.
- The driver reads the actor each frame and plays the hero's clips:
  - **Contact-locked strokes:** the clip time is the clip's contact frame minus the actor's `SignedTimeToContact`, so the authored contact lands on the gameplay contact.
  - **Prepare:** held during the takeback, then the follow-through plays out at 1×.
  - **Runs:** blended by movement direction; the playback rate follows speed.
  - **Ready vs Idle:** Ready in play, Idle between points.
  - **Reactions:** from `React` come Celebrate or Sad, and at match end MatchWin or MatchLose.
  - **Perfect contact:** a perfect-grade contact triggers Hit_Perfect (cooldown 8 s; cancelled by movement).
  - **Whiffs:** a whiff triggers Miss_Whiff; a point reaction arriving during the whiff waits for it to finish.
  - **Contact assist:** within ±0.16 s of contact, a small arm nudge (≤0.35 m) brings the hero's string bed to the gameplay sweet spot.
- **Replays:** the hero's bones are recorded and replayed by the existing replay system; the driver pauses during replay.
- **Rival:** also Hero01, with the same animation set; identity comes from cosmetics only (sweatband + teal trim).

**Minimal read-only hooks added:**
- `TennisActor`: `Backhand`, `PrepareAmount`, `PrepareBackhand`, `PrepareServe`, `Model`, and an event `Reacted`.
- `TennisGame`: a static event `Whiffed` fired on the existing whiff line, plus one `TennisHeroSetup.Attach(this)` call in `Start`.

## 3) Cosmetics pack v1 (identity only)
**Sockets** on the locked bind, verified:
- `Hat` and `FaceExtra` under Head, `Back` under Chest.
- `Hand_L` and `Hand_R` under the hands. The racket stays on Hand_R, on its approved socket.

**Pack** (`Models/Cosmetics/`):
1. **Hat A — Cap** (`Hero_01_Hat_Cap.fbx`): the dome is fitted to enclose every hair and head vertex above the band. It replaces the visor in the Hat slot.
2. **Hat B — Sweatband** (`Hero_01_Hat_Sweatband.fbx`): the band's radius follows the head and hair profile at the band height.
3. **Trim variant:** navy→teal and orange→yellow, via a recoloured copy of the cloth texture (`Textures/Hero_01_MatteCloth_TrimTeal.png`) and `Hero_01_V5Yellow.mat` for the piping.
4. **Celebration prop:** a mini trophy (`Hero_Prop_Trophy.fbx`) on the Hand_L socket, held with the left finger grip. It shows only during Celebrate/MatchWin.

**Controls:**
- Debug hotkeys **1** Cap · **2** Sweatband · **3** Trim · **4** Trophy · **0** Default V4.
- An IMGUI panel in development builds, player only.

**Acceptance:**
- **All clips play with every cosmetic:** 5 looks × 12 poses (Ready, Idle, FH, BH, Serve, Smash, Whiff, Celebrate, Win, Sad, Run, Perfect) in `cosmetics_matrix.png`.
- **No materials missing or broken** (the pink-shader check): 60 of 60 in `stills_log.txt`.
- **Avatar** valid in all 60.
- **Unequip** (0) returns the locked V4 visor, cloth and piping materials.
- **Rig untouched:** cosmetics only swap wardrobe meshes and materials and add the prop. The Animator, Avatar, root motion and racket socket aren't touched.
- **Gameplay stats untouched:** the component has no reference to gameplay stats, timing or the actor.

## 4) Playtest capture
**Setup:** live game in self-play (the game's own AutoPlay, same code path as input). Fixed 30 fps, cap on the player.

**What happened:**
- 5 points played, score from 0–15 to 30–40.
- Long rallies of perfect-grade contacts.
- One deliberately early swing through the real `RequestSwing` input produced a genuine whiff: the whiff overswing, then the point-lost reaction.
- Three point wins with celebrations.

**Frame totals:** Run 848, Ready 592, BH 312, Serve 286, Celebrate 257, FH 235, Sad 103, MissWhiff 54, Idle 13.

## Outputs (`ArtDir/anims/tennis_complete/`)
- `NEW_clips_montage.mp4` / `.gif` and `NEW_clips_sheet.png`: new clips only.
- `GAMEPLAY_cosmetic_on.mp4` / `.gif`: 21 s cut of live play. It shows a rally with the point won and celebration, the mistimed swing and whiff, a second rally and win, then a serve. The score overlay comes from the game's score events.
- `cosmetics_matrix.png`: default vs each cosmetic across all key poses.
- Also: gameplay prefab `Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab` and editor tool `Editor/HeroGameplayBuild.cs` (`Build` / `Stills` / `Juice` / `Gameplay`).
- Authoring: `restore_motion/tools/author_juice.py`, `tools/make_cosmetics.py`; blends in `anims/cosmetics/`.

## Known debt (real leftovers)
- **Hit_Perfect rarely fires in fast self-play rallies:** it needs a calm moment after the hit (cooldown plus low movement). The game's own PERFECT effects still fire every time.
- **Contact assist unmeasured:** it is implemented, but I didn't record a numeric racket-to-ball distance per contact. Contacts look right in the frames.
- **Run playback rate:** based on a 3.2 m/s reference that I didn't measure, so some foot slide is possible at unusual speeds.
- **Replay camera:** after each point it often cuts away, so on-screen celebration time is shorter than the clip.
- **Cap size:** deliberately oversized (chibi) so it clears the big hair. The sweatband sits at the visor's height.
- **iPhone:** not tested on a device this phase (editor Play Mode only).
- **Net touch:** not authored; there's no hook for it.
- **Eyes mocap licence:** CC-BY vs CC-BY-SA terms still to settle before ship.
