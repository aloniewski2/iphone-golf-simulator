# Motion Club — gameplay HUD mockups

Review concepts created with the built-in image-generation tool. No gameplay HUD, serve meter, controller, characters or production game code changed.

## Images

1. **Club Broadcast** — ivory/navy stacked scorebug, serving-ball indicator, distinct game and point columns, compact cream shot-feedback pill. Closest fit to the current Motion Club resort menu direction.
2. **Centre Court** — connected horizontal scorebug centred at the top; points dominate with names and game counts at the sides.
3. **Rally Pop** — navy frame, lime point cells, more energetic feedback.
4. **Match states** — Club Broadcast vocabulary extended to deuce, break point, advantage, shot quality, point/ace/out calls and let/fault/double-fault notices.

The three full-screen concepts use the same 3–2 games / 30–15 points sample. The lime tennis ball marks the server. State examples use tennis-consistent scoring: Milo serves in the break-point / advantage examples, with Adnan leading the points. Speeds are illustrative sample values, not tuning changes.

## Inputs and constraints

- Gameplay reference: `ArtDir/ui/toybox-refined-v4/assets/gameplay.png` — actual Unity capture, used as image-edit target.
- Brand reference: `ArtDir/ui/motion-club-resort-v1/02-terrace-home.png` — navy, ivory, lime and country-club feel.
- Reference is an existing capture; these are generated concept composites, not a new runtime recording or an exact pixel-preserving overlay export.
- Serve meter unchanged; absent in this mid-rally view. Controller keeps Dive as its only special move.
- Court centre / ball / near player remain clear; UI sits near screen edges. No ultimate, ranked status, currency or power stats.
- Use temporary point/shot calls, then clear them. Keep active gameplay scorebug stable.

Full generation prompts: `PROMPTS.json`. These are approval targets; no production implementation was requested in this turn.

