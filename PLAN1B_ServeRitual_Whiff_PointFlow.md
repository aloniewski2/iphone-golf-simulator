# PHASE: ServeRitual_Whiff_PointFlow (Plan 1 residual)

Paste this entire brief into Claude Opus. One PHASE only. Stop when DONE WHEN is met — do not auto-advance to Plan 2 / cosmetics / custom emotes.

---

## Who you are

You are the **execution agent** for a friends-first iPhone party sports game. Adnan is director. Read and obey, in order:

1. Project root `AGENTS.md`
2. Project root `art-bible.md` (v1.1+) — especially **§7 Animation law**
3. **This phase brief only**

If bible/AGENTS conflict with this brief on scope, **this brief wins for what to touch**; bible wins for look + motion law.

---

## Project

**Path:** `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`  
(Repo name says golf; product is multi-sport party sports. **Tennis** is the active sport.)

**Stack:** Unity URP · Mixamo-compatible Humanoid · locked chibi Hero Unity POLISH V4 · Eyes Japan–derived first-party `Hero_*` tennis clips.

---

## Context

### What already passed (DO NOT reopen / rewrite)

From Adnan’s latest in-match clip review, these Plan 1 items are **accepted as fixed** — leave them alone unless you break them:

- Feet on court (no sink)
- Honest serve *contact* when swinging at the toss (strings meet ball)
- No skating runs
- Early swing = real whiff (no fake hit / input VFX)
- Arm/racket clearance through body on the *old* whiff path (you will replace whiff feel — keep clearance)
- Serve torso twist at trophy/contact (the *swing* — separate from dribble deformation below)
- No infinite post-whiff freeze

### Locked assets

| Asset | Status |
|-------|--------|
| Hero Unity POLISH V4 look | **LOCKED**. No regen. Known micro-debt legs/hat/hair/shorts — leave it. |
| Core swing set (Ready / Serve contact stroke / FH / 2HBH / Runs / Volley / Smash) | **BASELINE** — do not casually rewrite. Serve *ritual* (dribble/toss) and *whiff* are in scope. |
| Product | Cosmetics = identity only; friends-first; no P2W |

### Hard product rule (new)

**Remove after-point emotes entirely.** Current point-end celebrate / disappointment / taunt / dance — delete or disable triggers. Custom emotes will be added later (Plan 3). Do **not** leave placeholder emotes that fire. After a point: score update → reset to Ready / next-serve state. No emote clip.

---

## PHASE name

`ServeRitual_Whiff_PointFlow`

**One-line goal:** Fix serve dribble/toss ritual, head eye mesh, expressive whiff, ball gravity, AI shadow, serve-side alternation, and strip after-point emotes — without breaking accepted Plan 1 continuity.

---

## Scope (fix these — Adnan-sourced + P0 residuals)

### A — Serve ritual (P0)

1. **Dribble through body** — ball must not clip through torso/legs while bouncing before serve.
2. **Dribble stance unnatural** — plant a real ready-to-serve stance (feet, hips, racket hand, off-hand). Not a stiff T / broken squat.
3. **Dribble torso deformed** — no mesh warp / spine crush / candy-wrap while bouncing. If deformation is weight/blend/IK, fix root cause.
4. **No hand toss** — serve must **toss the ball with the hand** (visible off-hand release upward), then racket meets ball at contact. Not “ball levitates from nowhere” or only bounce→instant hit.

### B — Head mesh (P0 / P1)

5. **Eyes visible through back of head** — fix mesh / normals / materials / double-sided eyes so eyes are **not** visible from behind. Do not regen whole head; surgical mesh/material fix on locked V4.

### C — Whiff (P1)

6. **Whiff is crooked, stiff, no emotion** — replace/retarget whiff recover so arms aren’t broken, body isn’t rigid mannequin. Party miss should read **funny failure** (art bible): anticipation → miss → overshoot → settle, readable silhouette. Still no arm/racket through body. Prefer stylize existing motion or a short new `Hero_Whiff` / recover — **not** Mixamo fight proxies as final.

### D — Point flow & physics residuals (P0)

7. **Strip after-point emotes** — remove/disable all current point-end emote triggers (player + AI). Next state = Ready / serve reset only.
8. **Ball gravity / arc** — AI (and player) returns must have real gravity arc and bounce on court. Kill flat laser waist-high trajectories.
9. **AI casts a shadow** — same shadow system as player; opponent must not float.
10. **Serve side alternates** — after point, next serve uses correct court side (Deuce ↔ Ad) per standard tennis serving rules for this scoring mode. Document if your mode is simplified — but do **not** always reset to the same Deuce spot after every point.

### Soft include if cheap (same phase)

11. Player **racket shadow** (body has shadow, racket doesn’t).
12. **AI contact VFX** on confirmed hit (same honesty as player).
13. **Landing ring** clears when ball is hit / point moves on.
14. **Stumble → Ready blend** — no hard snap/teleport (ties to whiff work).
15. **AI unfreezes** after point → Ready for next point (no emote; just Ready).
16. Premature **serve meter** — hide until serve state actually starts.

### Explicit non-goals

- No Plan 2 spectacle cameras / hit-stop product pass / power-ups.
- No custom celebrate/disappoint clips (those come later — that’s why emotes are stripped now).
- No cosmetics wardrobe pass.
- No online / Lobby / Relay / second sport.
- No hero regen; no replacing Eyes swing set with store packs.
- Do not “fix” replay with a hack that re-breaks hit honesty — if instant replay is still broken, **disable or stub it off** this phase rather than shipping a lying replay. Note it as deferred if disabled.

---

## Work order

### Step 0 — Diagnosis (report first)

Inventory and report paths for:

- Serve state machine: dribble → toss → swing → contact
- Ball attach / bounce logic during serve ritual
- Whiff / miss state + transitions to Ready
- Point-end emote triggers (player + AI) — list every clip/event to disable
- Ball physics (gravity, bounce, AI shot spawner)
- Shadow casters on AI + racket
- Serve-side / court-side selection after point
- Head/eye materials on Hero V4

### Step 1 — Kill after-point emotes

Disable all point-end emote playback. Verify point end goes: resolve → (optional short delay) → Ready / next serve. No celebrate/taunt.

### Step 2 — Serve ritual

Fix dribble stance, torso deformation, ball-through-body, and **hand toss**. Prove with stills + short capture: bounce clear of body → hand toss → strings meet ball (or honest fault).

### Step 3 — Eyes through skull

Surgical mesh/material fix. Prove with back-of-head still: no eyes visible.

### Step 4 — Whiff emotion + blends

New/cleaned whiff: readable party miss, arms not crooked, not stiff, clears body, blends into Ready (no snap). No emote on point end after whiff loss — just Ready / flow.

### Step 5 — Ball arc + AI shadow + serve side

Gravity/bounce; AI shadow on; serve side alternates correctly.

### Step 6 — Soft polish if time

Racket shadow, AI hit VFX, ring clear, serve meter gate, AI Ready after point.

### Step 7 — Replay

If still lying: turn off instant replay for now; note in report. Do not ship fake center-baseline idle as the whiff.

---

## Surgical constraints

1. Full clips, never frozen stubs.
2. Hands hold racket; string bed to ball at contact.
3. Runs sacred — don’t touch unless proven cause.
4. Funny failure > sweaty sim on whiff.
5. One PHASE — stop at proof.

---

## Outputs (required)

```
ArtDir/screenshots/plan1b_serve_whiff/
  00_diagnosis_notes.md
  01_dribble_stance_side.png          # no through-body, no torso warp
  02_hand_toss.png                    # ball leaving hand upward
  03_serve_contact.png                # strings meet ball
  04_head_back_no_eyes.png            # back of head clean
  05_whiff_silhouette.png             # expressive, arms OK
  06_whiff_to_ready_blend.png         # or short clip
  07_ai_shadow.png
  08_ball_arc_bounce.png              # gravity arc / bounce
  09_serve_side_ad_or_deuce.png       # proves alternation after a point
  10_no_emote_point_end.png           # Ready only after point
```

Capture: `ArtDir/anims/captures/Plan1B_ServeRitual_Whiff_PointFlow.mp4`

Must show: dribble clean → hand toss → serve contact; one whiff with emotion + blend to Ready; point ends with **no emote**; AI shadowed; ball arcs/bounces; next serve on correct side.

---

## DONE WHEN (all must pass)

1. Dribble does not go through body; stance reads as real serve prep; torso not deformed.
2. Serve includes a visible **hand toss**.
3. Eyes not visible through back of head.
4. Whiff reads party-fail (not stiff crooked arms); blends to Ready; no body clip.
5. After-point emotes are **gone** (player + AI).
6. Ball has gravity arc / bounce (no laser returns).
7. AI casts a ground shadow.
8. Serve side alternates after points (not always same Deuce reset).
9. Accepted Plan 1 hits (feet, honest contact, no skate, no fake input hits) still hold.
10. Replay either fixed to truth or disabled — never lying.

Reply format:

- **Diagnosis**
- **What changed** (paths)
- **Emotes removed** (list)
- **Output paths**
- **Deferred** (replay if off; soft items skipped)
- **Do not** auto-start Plan 2

---

## STOP

End phase. Wait for Adnan approval.
