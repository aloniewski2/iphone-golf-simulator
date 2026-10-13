# GATE RESULTS: multiplayer plays like single player

**Date:** 2026-10-11/12.
**Plan:** `PLAN_Multiplayer_SinglePlayerParity.md`.
**Why it was different:** `proof/multiplayer-explore/AUDIT_single_vs_multiplayer.md`.

**How it was tested:** in the Unity editor on one Mac, no phones.

- **Tennis:** two editors, one per phone, joined over a simulated network with 60 ms ±25 ms delay and 3% of pose updates lost. Each player was driven only through single player's controls. See `work/multiplayer-parity/README.md`.
- **Golf:** one editor as the host phone with the TV, plus a guest phone's inputs over the same simulated network.

Everything is uncommitted on `build-10/11`.

## Tennis

| Gate | Result | Evidence |
|---|---|---|
| T1: the other player's serve shows bounce, toss arm, trophy, dip and jump | **PASS**, by construction plus frames. The rival's serve runs single player's ritual: `TennisServeRoutine.Pose`, `ReachTossHand`, `Prepare(serve)` from their phone's routine clock, then their toss and their serve swing (`RemoteServeStep`). Up to 28 rival serves per match. | `tennis_online_seat0_view.mp4`, `tennis_online_seat1_view.mp4`, `runs/*/seat*_summary.json` (rivalStrokes) |
| T2: backhands, volleys and slices on the far end; at least 4 stroke kinds in a match | **PASS**. In one match the far player played Serve, Drive, LowPickup, Lob, Dive, Smash, Volley, Slice and Running (9 kinds, `runs/v7`). Wing (backhand) is carried with every swing. | `runs/v7/compare.txt` |
| T3: both phones agree on every point in a full match | **PASS**, 5 of 5 matches. Score sequences were identical (29, 28, 21, 26 and 34 points), covering online from both seats and split screen. Zero errors and exceptions. | `runs/{v2,v3,s2,v6,v7}/compare.txt` |
| T4: the ball leaves the other player's racket | **PARTIAL.** 71 of 71 rival contacts were caught on their strings (`HELD`, none flew past). The drawn ball eases onto the strings over 0.15 s; the mean ease distance is about 1 m (the far player is small on screen, but it shows in the split-screen right half). The cause is the other player's pose arriving 60–110 ms late. | `runs/v7` contacts, `sheet_tennis_*.jpg` |
| T5: the online view is single player's camera | **PASS.** Versus never calls `NetworkFrame`; single player's `Update`, `UpdateCamera` (`TennisShoulderCamera`) and HUD run unchanged. | code; `tennis_online_seat*_view.mp4` |
| T6: split screen shows two shoulder views, each player in front of their own half | **PASS.** Right half: the same shoulder-camera rule from the far end, plus a second single-player HUD with the score from that player's side. The old `TennisSplitCamera` is not used in versus. | `tennis_split_screen.mp4`, `sheet_tennis_split.jpg` |

## Golf

| Gate | Result | Evidence |
|---|---|---|
| G1: waiting golfers move 0.00 m while the player up sweeps ±30° | **PASS.** Max 0.000 m over 5,889 samples. | `runs/golf/golf_two_phones_events.txt` |
| G2: the TV follows the player up's aim within 120 ms (60 ms net) | **PASS on lag:** 90 ms, mean error 0.15°. The "biggest step" figure (75°) counts the jump to a new player's line at each turn change, so it doesn't measure smoothness. | same |
| G3: no hitch over 250 ms in a two-player hole; no exceptions | **FAIL on hitches, PASS on exceptions.** 0 errors and 0 exceptions; the loading-cover "Mesh can not have more than 65000 vertices" crash is fixed. 1–3 s freezes remain at hole loading, replay start and some turn starts. **Solo has the same freezes** (`golf_solo_events.txt`), so they are shared, not a multiplayer difference. The profiler probe could not attach, so the cause is unknown. | same |
| Single player's flow in multiplayer | **PASS (editor).** <br>• Intro cut chosen as single player chooses it and skippable by the player up. <br>• The result screen ends on the swinging phone's single-player rule (3 s, the emote, or a tap). <br>• Single-player hole-out card for the golfer who holed. <br>• "Hole complete" card with a row per player. <br>• Round card. <br>• No silent 60 s penalty: 180 s, with a visible countdown in the last 20 s. | `golf_two_phones.mp4`, `sheet_golf_two_phones.jpg` |

## Not run, or still different

- **Real phones and AirPlay:** not run. The Swift side (`MultiplayerService.swift` peer relay, loadout fields) was syntax-checked only and not built.
- **T4 ease distance:** about 1 m (see above).
- **Golf freezes:** shared with solo (see G3).
- **Tennis timing check:** multiplayer still skips it before a match (Swift setup flow); the lag learner works in play.
- **Shared-TV split screen, controller-only player:** their swings are judged on their own phone; the TV's delay is not yet credited to them.
- **Golf guest's own TV:** a guest's swing on their own TV waits one host round trip before the flight starts.
- **Golf haircut and shoes:** now sent in the lobby look. Tennis heroes have no hair meshes, so tennis is unchanged.
