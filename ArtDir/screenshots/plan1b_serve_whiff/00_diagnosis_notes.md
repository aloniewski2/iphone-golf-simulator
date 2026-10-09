# Plan 1B — ServeRitual_Whiff_PointFlow: diagnosis + result (2026-09-26)

**Status:** review candidate. Stopped for Adnan's approval; Plan 2 not started.

All proof comes from one 3000-frame self-play match (the game's own AutoPlay, 30 fps, editor Play Mode, audio muted), using the locked Hero V4 in its default visor look.

## Step 0: Diagnosis (paths under `Unity/Assets/`)

| Area | Where | What was wrong |
|---|---|---|
| Serve state machine | `Scripts/Tennis/TennisGame.cs` | Flow is `PlayerServeHold` (bounce until TOSS) → `PlayerServeToss` (ballistic toss) → `CommitServe` / `LaunchServe` (Plan 1 honest contact). The rival runs the same routine inside `OpponentServe`. |
| Ball during the ritual | `Scripts/Tennis/TennisServeRoutine.cs` | The ball's bounce and toss points were fixed for the *hidden* gameplay rig (actor-local `(-.24, .86, .26)`). The visible hero stands side-on, so the ball dropped through its front leg. |
| Hero during the ritual | `Scripts/Tennis/HeroTennisDriver.cs` | During the bounce the game prepares at amount 0, so the hero sat in the **Ready crouch** (both hands on the racket in front, where the ball fell). The toss-hand reach only moved the hidden rig's arm, so the hero's off-hand never touched the ball. |
| Torso "deformation" | `HeroTennisDriver.cs` | Two causes: the Ready↔serve-prepare 15 Hz flicker (fixed in Plan 1), and the Ready crouch under a bouncing ball. Both are gone now that the hero holds the serve clip's own side-on stance. |
| Eyes through the skull | Hero V4 head (Body_Skin, Hair_Default, Body_EyeSphere_L/R) | The hair shell has a thin gap under the hat band and the skull behind it is open (skin material is double-sided). From the elevated gameplay camera, the eye spheres and the inside of the face showed through the gap. |
| Whiff | `ArtDir/anims/restore_motion/tools/author_juice.py` → `Hero_MissWhiff_v5.fbx` | Old clip: a full 360° overhead spin with arms locked straight out (T-pose look) and no emotion. |
| Point-end emotes | `HeroTennisDriver.OnReacted` (CelebratePoint / SadPointLost / MatchWin / MatchLose), `TennisActor.React` (`PlayEmote("Win")`), between-point `Idle` base | All fired on `TennisGame.AwardPoint` → `Player/Opponent.React`. |
| Ball physics | `TennisBall.cs` (gravity + Magnus), `TennisRules.RallyVelocity`, `TennisGame.SendFromOpponent` | Gravity was always real. AI returns used `ServeVelocity`, the *flattest* arc clearing the tape by 0.22 m, so every AI shot skimmed the net at head height. |
| AI shadow | `TennisLook.ContactShadow`, `TennisGame` | Both players' blob contact shadows were placed at a fixed **y = 0.012**. The resort court surface is at y ≈ 0.035, so the blobs (and the ball's) were buried under the court and never drawn. The rival also usually stands in the back wall's shade, where its real cast shadow is invisible. |
| Serve side | `TennisMatch.DeuceCourt` = (points total) even; `TennisRules.ServerStanceX` | Match play already alternates correctly (proven below). The only "always deuce" path is `TennisGame.StartPlayerServe`, which the **tutorial** calls every rep. |
| Serve meter | `TennisGame` → `TennisTossMeter.Run` | It ran from the first frame of `PlayerServeHold`, while the server was still walking in. |
| Instant replay | `TennisReplay` via `replayDue` | It replays the hidden actors' recorded poses, not the visible heroes. |

## What changed

1. **Emotes stripped**
   - `HeroTennisDriver.OnReacted` plays nothing (`PointEmotes=false`, counted as `PointReactionsSuppressed`).
   - `TennisActor.React` no longer calls `PlayEmote("Win")`.
   - The between-point base pose is Ready, not Idle.
   - Hit_Perfect never starts between points.
2. **Serve ritual**
   - While serving, the hero holds the approved serve clip's own side-on pre-serve stance (t = 0.6 s ± a slow weight sway).
   - The wind-up maps onto the clip's tossing-arm rise (0.6 → 1.45 s), then the trophy.
   - The ritual ball points now come from the hero (`TennisActor.VisualTossPoint`): the bounce is out beside the front foot, clear of the belly, legs and racket head.
   - A two-bone IK puts the hero's left hand on the ball. It pushes and catches each bounce from above, lifts from below and releases the toss.
   - The approved serve clip file is **unchanged**.
3. **Head:** `HeroHeadOccluder.cs` adds an inner shell inside the head, measured at runtime from the hero's own skin, hair and eye vertices and shaded with the hair material. It is only ever visible through the band gap, where it reads as hair. The locked meshes and materials are untouched.
4. **Whiff:** the new `Hero_MissWhiff_v5` (2.0 s):
   - overswing wrap
   - momentum hop, back leg kicks
   - wobbly landing
   - double take back at the ball
   - sheepish head scratch
   - shake-off into Ready

   Elbows stay bent and the off-arm stays clear. It blends out onto the Ready pose.
5. **Ball arc**
   - AI rally shots use `TennisRules.RallyArcVelocity`, lofted to clear the tape by 0.7 m. The ball still lands where the AI aimed.
   - Clean player shots use the same 0.7 m margin; poor contacts can still find the tape.
6. **Shadows:** `ContactShadow.Surface` now puts the player, rival and ball blobs on the court surface. It's the same system for both players, and the ball's shadow now tracks its height.
7. **Serve meter:** it appears only once the server has walked in and planted at the line.
8. **Replay:** `TennisGame.InstantReplayEnabled = false`. It's off rather than shown lying.

## Measured (3000-frame match)

| Check | Result |
|---|---|
| Point reactions suppressed / emote frames played | 3 / 3 suppressed per player · **0 emote frames** (player and rival) |
| Ball through body during the serve ritual (torso volume + leg capsules) | **0 of 674 frames**; minimum clearance 0.11 m |
| Whiff arm-in-torso > 1 cm | **0 of 59 frames** (max 0.8 cm) |
| Serve side at each serve | +2.35 (deuce) → −2.35 (ad) → +2.35 → −2.35 |
| AI return, median apex / height at the net | before 1.34 m / 1.29 m → **after 1.85 m / 1.78 m** (net 0.97 m) |
| Plan 1 still holding | feet below court 0/0 frames · player contact gap mean 0.11 m / max 0.17 m · honest misses 0 · standing foot slide median 0.01 m/s · arm-in-torso only in the approved backhand finish (139 frames, the known Plan 1 residual) |

## Deferred / still open
- **Rival serve ritual:** same code path, but not shown. The player served the whole game in this capture.
- **Instant replay:** disabled. Needs a replay that records and plays back the visible hero bones.
- **Soft items not done:**
  - racket shadow (the racket already casts; the thin frame barely registers in the shadow map)
  - honest-gap check on AI contact VFX (the AI still snaps the ball up to 2 m onto its strings)
  - clearing the landing ring when the point moves on
- **Tutorial:** still resets every rep to the deuce court (drill design, not match play).
- Not tested on iPhone.
