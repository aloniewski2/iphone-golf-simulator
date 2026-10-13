# PLAN: multiplayer plays exactly like single player (tennis + golf)

**Asked by:** Adnan, 2026-10-11:
- "multiplayer should have the exact same gameplay as single player period."
- "when two people load into multiplayer online, it looks like single player but you are playing vs a real person, and if you are doing split screen its like two people are playing from a single player perspective + mechanics + everything."
- Golf: "the people waiting to hit should be stable and should be able to emote."

**Why it differs today:** `proof/multiplayer-explore/AUDIT_single_vs_multiplayer.md`.

**Status:** BUILT in the repo (uncommitted, `build-10/11`) and tested in the editor with two simulated phones; real phones not run yet. Results are in `proof/multiplayer-parity/GATE_RESULTS.md`:
- Tennis T1, T2, T3 (5/5 full matches), T5 and T6 pass; T4 is partial (the ball eases about 1 m onto the far racket).
- Golf G1, G2 and the flow checks pass; G3 fails on freezes that solo has too.

## Tennis: one match, single-player gameplay for both players

Each player's phone runs the real `TennisGame` for its own player: the same camera, serve ritual, stroke selection, honest contact, physics, HUD, crowd and umpire.

**The other player.** The opponent is the real person; the AI is switched off. Their phone sends:
- a 30 Hz pose stream: position, run, take-back and serve routine;
- events:
  - swing start, with kind, wing and power;
  - toss, with origin, drift and accuracy;
  - shot, with the exact launch (position, velocity, spin, curve, bounce) and the shared-clock time it left the strings;
  - whiff, emote, ready for the next point;
  - point and fault calls.

**Mirroring.** The receiving phone mirrors everything onto its far end (x → −x, z → −z). It drives the opponent's actor through the same calls the AI used (`Swing`, `Prepare(serve)`, `ReachTossHand`, `PaceToContact`/`GuideContact`, `SplitStep`, `React`), so every handcrafted clip plays on both screens.

**Who calls the ball.** The phone of the player the ball is coming towards calls it: in, out, net, double bounce, missed, and serve faults. It sends the call, and both phones then run the same `AwardPoint` presentation. A player's own missed toss is called by their own phone.

**Timing.**
- Every shot is replayed from its launch time on the shared clock, so both phones have the ball in the same place at the same moment.
- An incoming shot is caught up to "now".
- While the hitter's shot event is on its way, the ball is held on the opponent's strings, inside the contact hit-stop.

**Next point.** It starts when both phones are ready, so emotes and the intro never get cut off.

**Split screen (one TV).**
- The TV phone draws two single-player views: left is seat 0 from behind seat 0, right is seat 1 from behind seat 1, both with the shoulder camera.
- The controller-only phone runs its own game hidden, as the authority for its player.
- `TennisSplitCamera` (the high wide shot) is retired.

**Transport.** A new `peer` packet kind, relayed between seated phones (`MultiplayerService.swift`). The host still starts the match, pauses it, handles drops and reports the result.

## Golf

1. Waiting players stand at fixed spots, set from the hole (not the aim), facing the player who's up. They never move while that player aims, and they can emote at any time.
2. The line on the TV follows the player who's up smoothly and quickly. The phone sends its stick and heading at 30 Hz; the TV runs them forward between messages; no 15 Hz steps.
3. Single player's flow:
   - intro cut and skip;
   - result timing (3 s or the emote, skippable by the player who swung);
   - hole-out card for the player who holed;
   - hole-done card for everyone;
   - round scorecard;
   - no silent 60 s penalty.
4. The swing launches at once on the phone that swung.
5. Full locker look on every golfer, and the right nameplate.
6. Hitches over 0.5 s during replay and aim, and the loading-cover mesh exception ("Mesh can not have more than 65000 vertices").

## Gates (binary)

- T1: the opponent's serve shows the bounce, toss arm, trophy, dip and jump. Proof: frames.
- T2: backhands, volleys and slices play on the opponent's end; at least 4 stroke kinds seen in a match.
- T3: both phones agree on every point (same score sequence) over a full match, with 60 ms ±25 ms simulated network delay.
- T4: the ball leaves the opponent's strings. Measured gap at their contact under 0.3 m, mean.
- T5: the online view equals the single-player camera. The camera path code is identical, checked by diff/grep; plus frames.
- T6: split screen shows two shoulder views, and each half's player is in front.
- G1: waiting golfers move 0.00 m while the active player sweeps the aim ±30°. Logged positions.
- G2: the TV heading lags the player's phone by at most 120 ms with 60 ms network delay, and no step above 1.5°.
- G3: no hitch over 250 ms during a two-player hole in the editor (load excluded); no exceptions.
- Proof: `proof/multiplayer-parity/`.
