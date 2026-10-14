# Audit: single player vs multiplayer (tennis + golf)

2026-10-11. Read-only code audit, plus one scripted split-screen tennis run and one two-phone golf run in a scratch copy of the Unity project. Nothing in the repo was changed.

## Why multiplayer looks different

**Tennis: multiplayer is a second, simpler game.**
- `TennisGame.Update` starts with `if (NetworkFrame()) return;` (`TennisGame.cs:1024`). In any multiplayer match the whole single-player game is skipped: the simulation, movement, serve ritual, ball physics, contact, HUD, effects, crowd and umpire.
- In its place:
  - `NetworkTennisMatch.cs` runs a separate rules engine on the host. It was written on 2026-10-05 (`23cbedab`), after all the single-player feel work.
  - `TennisGame.Network.cs` draws that engine's state. It places the two players, fires a generic serve or forehand when a counter changes, and puts the ball where the host says.
- Every single-player feature added since was never brought across.

**Golf: much closer to single player, but different in several places.**
- Multiplayer golf does reuse single player's aiming, flight, replay and cameras. That came from `e6564017` on 2026-10-10, "Multiplayer golf plays like single player".
- The turn flow, timing, results, hole-out, controls and network latency are still separate. Details are below.

## Tennis: differences (C = confirmed in code, L = likely)

| # | Single player | Multiplayer | Effect |
|---|---|---|---|
| 1 | Serve ritual: two bounces in the hand, toss arm, windup, trophy and dip, driven by `TennisServeRoutine` + `Prepare(serve)` + `ReachTossHand` (`TennisGame.cs:1356-1434`). The hero's serve clip stance/release/trophy plays only while `PrepareServe` is set (`HeroTennisDriver.cs:624-638, 1131-1143`). | None of those calls. The ball is held at a fixed point 1.1 m up (`NetworkTennisMatch.cs:60`). The toss is a bare parabola. | **No dribble, no toss arm, no dip or jump.** The ball floats out of the body. C |
| 2 | The serve swing is paced so the strings meet the toss (`PaceToContact`/`GuideContact`, `TennisGame.cs:641-721`). | `actor.Serve()` fires only when the host's serve counter changes, i.e. after launch. The ball launches from a fixed point (`NetworkTennisMatch.cs:132`). | Ball leaves before the racket swings. C |
| 3 | Stroke selection: forehand/backhand, volley, smash, lob, slice, topspin, low pickup, running, dive (`TennisGame.cs:608-631`). | Always `actor.Swing(power, false)`, a forehand (`TennisGame.Network.cs:95`). The host ignores handSide/lift/facing. | **Every shot is a forehand.** Backhand, volley, smash, lob, slice, topspin, wide, pickup and dive clips never play. C |
| 4 | Honest contact on the strings; a miss over 0.22 m whiffs. | A box check (\|dx\|<1.8, \|dz\|<2). The ball leaves from wherever it is. | Racket and ball don't meet. C |
| 5 | Take-back (`Prepare`), head following the ball, split step, point reactions (Celebrate, SadPointLost, HitPerfect, MissWhiff, MatchWin/Lose, game-end taunt). | Not called. Only chosen emotes play. | **The handcrafted reactions never play.** C |
| 6 | Walk-in to the serve spot. Running with acceleration, with forward/back movement. | Positions snap each point. Constant 5.6 m/s sideways only, z fixed at ±12.2. The far player's run left/right is mirrored (velocity not sign-flipped, `TennisGame.Network.cs:107`). | Sliding, no net play, wrong-way run on the far player. C |
| 7 | Ball physics: spin (Magnus), curve, spin bounce with friction, kick second serve. | Gravity only, fixed bounce 0.75. | Different flight and bounce. C |
| 8 | Serve: power bar timed to the apex, PERFECT serve, too early/late goes into the net, second serve slower, not swinging is a catch (no fault). | Any swing 0.25–1.55 s after the toss is legal; speed comes from phone power. Not swinging is a fault after 1.65 s. | Different serve game. C |
| 9 | `GameSpeed` 1.1, `ServePace` 0.9. | Real time. | Rallies about 9% slower, serves faster. C |
| 10 | Supercharge streak, overhead bonus, dive cap, balance from lateral speed. | None. The Supercharge HUD still draws, empty, on the right edge (over the right-hand player's half). | Dead HUD on one half. C |
| 11 | Net cord drop + "NET!" call, ball keeps bouncing after the point. | Instant point; the ball freezes in mid-air. | C |
| 12 | Camera: shoulder camera, toss cam, PERFECT punch, rival cam, titles (juice). | Split screen: a new fixed high camera behind each baseline (`TennisSplitCamera.cs`, added `80f7cc66` 2026-10-09, never used in single player). Own-TV view uses the shoulder camera but no juice cams. | **The camera you've never seen** is the split-screen solver. C |
| 13 | Intro: full cut on first view, players look into the lens. | Always the short cut, unskippable. The rival is labelled "THE RIVAL" even for a friend. | C |
| 14 | HUD/audio/effects: grade + km/h, FAULT/GAME/SET calls, serve power bar, swing cue, aim/landing rings, coach tips, results card; umpire calls, crowd hush/cheer, net/gasp/applaud/whiff sounds; bounce dust, whiff/net effects; ball squash/trail; hit-stop. | Only the scoreboard, DEUCE/MATCH POINT notices, hit sound, contact effect, bounce sound, toss meter and strike haptic. | Much flatter. C |
| 15 | Phone: timing check, continuous walk before the serve (2.4 m/s). | Timing check skipped. A walk press nudges by 0.02 and the next move sample overwrites it. The racket radar stays empty. | C / L for the walk |

## Golf: differences

| # | Single player | Multiplayer | Effect |
|---|---|---|---|
| 1 | **Aim:** the TV draws your line straight from your own phone. | A guest with no TV sees only the controller sheet (its course camera is off, `NativeSportsSession.cs:342-345`). The line reaches the host's TV at up to 15 Hz (`GolfGame.Network.cs:340`), plus network delay, plus the host's AirPlay delay. The stick turns the aim at up to 42°/s (`GolfGame.cs:25, 2171-2175`). | **Most likely cause of "impossible to aim":** the stick keeps turning while the TV is still catching up, so you overshoot and the line jumps in ~3° steps. C |
| 2 | Touch mode uses the same SwiftUI joystick. | Same, but a guest is more likely to be on it. It sets an **absolute** direction: wherever the thumb lands, the aim snaps there (360°, 1–2° per point of thumb travel, no putter scaling). "Aim down course" turns the aim 180°. | Very twitchy in both modes; worth fixing too. C |
| 3 | No turn timer. | 60 s per turn, then a silent penalty stroke and the turn passes (`NetworkGolfRound.cs:157-158`). There is no countdown on screen. | Fighting the aim gets you penalised. C |
| 4 | Hole intro: full cut, skippable, Overview available. | Short cut timed by the host (2.9 s), unskippable, Overview disabled. Pass-the-phone has no intro at all. | C |
| 5 | Result screen: about 3 s or until the emote finishes, skippable, "NEXT SHOT". | Fixed 3 s (+2 s if holed) from the host, not skippable, emotes don't extend it, "NEXT PLAYER". | C |
| 6 | Hole out → HoleDone → score stamp → next hole. | No HoleDone. A holed shot still shows the **single-player hole-out card**, because the round is set up as a solo round underneath (`GameSetup.Solo`, `GolfGame.Native.cs:31`; `GolfGame.HoleOut.cs:22`). It says "NEXT HOLE" while others are still playing, and its button does nothing. | Wrong card. C |
| 7 | Round end: scorecard + NEXT/REMATCH. | Text "Round complete" only. REMATCH does nothing. | C |
| 8 | Swing launches at once. | Waits for the host's round trip. START SWING can reappear briefly. | C / L for the button |
| 9 | Your golfer wears the full locker look (hair, haircut, shoes). | Party golfers get body, skin, shirt and shorts only. The walk-on nameplate shows this phone's player even when someone else is up. | C |
| 10 | — | Each full swing costs about 24 s before the next player (flight + ~10 s tee-box replay + 3 s result); measured in the scripted run. SKIP REPLAY only skips on one phone; the host still waits. Loading between holes took about 9 s. | Slow party pace. Measured. |
| 11 | Pause freezes the game. | Pause doesn't stop time. | C |

## What was not proven

- The scripted runs drove the multiplayer inputs directly; no real phones were involved.
- The golf aim conclusion is from code plus the timing model. My first scripted aim test was invalid: the harness fired its inputs too fast, and I stopped before rerunning it.
- I did not check what the iOS export on device contains.

## What "exactly the same" would take

- **Tennis:** stop drawing multiplayer with its own thin layer. Run the single-player `TennisGame` on every screen, for both players, from the host's inputs and events:
  - Option A: the host runs the full single-player simulation for both seats and sends the results.
  - Option B: each screen runs the full presentation (ritual, stroke choice, contact, reactions, effects) driven by host events.
  - The NetworkTennisMatch rules would either be retired or brought up to the single-player rules (spin, serve, movement, GameSpeed). This is a large change and needs your call on the approach.
- **Golf:**
  - Give a guest its own course view while aiming, or stream the aim faster and smooth it on the TV.
  - Make the SwiftUI joystick turn relative to the current aim instead of setting an absolute direction.
  - Add HoleDone and a proper party hole-out card and scorecard.
  - Show or drop the 60 s limit.
  - Use single player's intro and result timing.
