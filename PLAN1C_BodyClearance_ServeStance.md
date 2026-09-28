# PHASE: BodyClearance_ServeStance (Plan 1 residual)

Paste this entire brief into Claude Opus. One PHASE only. Stop when DONE WHEN is met — do not auto-advance to Plan 2 / cosmetics / emotes.

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

### Status

Rally loop, hit sync, footwork, UI meter/rings, ball arc feel are **close / accepted**. Do not reopen Plan 1A/1B wins (feet, honest contact, no skate, no fake input hits, emotes stripped, etc.) unless you break them.

Adnan’s latest clip: **we are so close**, but:

1. **Serving stance is still wrong**
2. **Limbs and racket still go through the body sometimes** — need a **hard standard**: limbs + racket must never clip through the body

### Locked

| Asset | Status |
|-------|--------|
| Hero Unity POLISH V4 | **LOCKED** — no regen. Chibi = big head + thick torso — animations must **route around** that volume. |
| Core swing identities | Keep recognizable Serve / FH / 2HBH / Volley / Smash / Runs. Fix paths/poses for clearance + serve plant — do not freeze stubs or swap to Mixamo fight proxies. |
| After-point emotes | Stay **off** (custom later). |

### Clip evidence to fix (timestamps from Adnan’s capture)

**Serve stance**
- 0:00–0:03 setup **square to net** — must be **sideways / side-on** to baseline (standard serve plant) so torso can coil.
- 0:04–0:05 toss/swing has **no hip/shoulder coil** — arm-only chop. Fix kinetic chain: hips → torso → shoulder → arm.

**Through-body**
- 0:00–0:03 — forearm + racket shaft **buried in stomach** during serve charge (**P0** — static on screen while meter charges).
- 0:04 — racket/arm into head/neck on serve backswing.
- 0:07 — FH follow-through racket through face/shoulder.
- 0:11 — BH prep racket through chest/thigh.
- 0:15 — FH again through back/head/shoulder.

---

## PHASE name

`BodyClearance_ServeStance`

**One-line goal:** Sideways serve plant with real coil, and a hard no-clip law so limbs/racket never pass through the chibi body — especially serve charge and swing follow-throughs.

---

## Hard law (new — enforce everywhere in this phase)

**BODY CLEARANCE STANDARD**

1. At **no** keyed frame may the racket mesh, handle, or forearm **intersect** the torso, head, pelvis, or thighs.
2. Off-arm must **clear** the torso (bible: never glued through gut).
3. Prefer fixing **animation paths / stance / socket offsets / constraints** over hiding clips.
4. Chibi proportions are the constraint: widen arcs, raise elbows, delay wrap, or shorten follow-through so the racket goes **around** the big head — not through it.
5. If a clip cannot clear without destroying the stroke, call it out with stills — do not ship through-body.

---

## Scope

### A — Serve stance + charge (P0)

1. Serve ready / dribble / charge: **side-on** plant (feet + hips roughly perpendicular to net/baseline), not square-to-net.
2. Racket + forearm **outside** the body during charge — never through stomach.
3. Hands hold racket correctly; ball/hand toss from prior phase must still work if present.
4. No torso mesh deformation / candy-wrap during charge.

### B — Serve toss + swing coil (P1)

5. Visible unit turn / coil on toss and swing (hips → torso → arm), not arm-only.
6. Backswing clears head/neck (fix 0:04 class clip).
7. Contact still honest: strings meet ball when serve counts.

### C — Rally clearance (P1)

8. Forehand wind-up + follow-through: racket path clears head, shoulders, torso (fix 0:07, 0:15).
9. Backhand (2HBH) prep + swing: racket clears chest/thighs (fix 0:11).
10. Spot-check volley/smash/whiff if cheap — same clearance law. Don’t expand into new juice clips.

### Explicit non-goals

- No Plan 2 cameras / hit-stop product / power-ups.
- No cosmetics, emotes, online, second sport.
- No hero regen; no replacing Eyes set with store packs.
- Don’t touch Runs unless a clearance fix forces a tiny transition — prefer leave Runs alone.
- Don’t re-enable after-point emotes.

---

## Work order

### Step 0 — Diagnosis

Report paths for: serve charge/ready clips + Animator states; FH/BH clip sources; racket socket; any IK/collision already attempted; whether square stance is root rotation vs authored anim.

### Step 1 — Serve plant + charge clearance

Author/adjust serve ready/charge so stance is side-on and racket is fully outside the body. Prove with front + side stills while meter is up.

### Step 2 — Serve coil + backswing clearance

Add readable coil; clear head/neck on backswing; keep contact honesty.

### Step 3 — FH / BH path clearance

Widen/reroute follow-through and prep so racket never intersects face/torso/thigh. Re-export/reassign clips; montage proof.

### Step 4 — Regression check

Quick local rally: serve charge → toss → hit → FH → BH. Confirm no through-body on those paths; feet/hit sync still OK.

---

## Outputs (required)

```
ArtDir/screenshots/plan1c_body_clearance/
  00_diagnosis_notes.md
  01_serve_charge_front.png       # side-on plant; racket NOT in gut
  02_serve_charge_side.png
  03_serve_toss_coil.png          # hips/shoulders turned
  04_serve_backswing_clear.png    # no head/neck clip
  05_serve_contact.png
  06_fh_followthrough_clear.png
  07_bh_prep_clear.png
  08_bh_or_fh_contact_clear.png
```

Capture: `ArtDir/anims/captures/Plan1C_BodyClearance_ServeStance.mp4`  
Must show: serve charge (clear + side-on) → toss/coil → contact → at least one FH and one BH with no through-body.

---

## DONE WHEN (all must pass)

1. Serve charge stance is **side-on**, not square-to-net.
2. During serve charge, racket/forearm are **never** inside the torso.
3. Serve has readable coil; backswing clears head/neck.
4. FH and BH prep/follow-through do **not** send racket through face/torso/thighs on the common paths from the clip.
5. Body Clearance Standard holds on the proof capture (no “sometimes” on those beats).
6. Prior wins intact: feet, honest hits, no skate, no emotes, no frozen stubs, held grip + strings-to-ball at contact.

Reply format:

- **Diagnosis**
- **What changed** (paths)
- **Still clipping?** (honest — if any residual, timestamp + plan)
- **Output paths**
- **Do not** auto-start Plan 2

---

## STOP

End phase. Wait for Adnan approval.
