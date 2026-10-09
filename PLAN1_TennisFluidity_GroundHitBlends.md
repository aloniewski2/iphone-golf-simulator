# PHASE: TennisFluidity_GroundHitBlends (Plan 1 — Fluid play)

Paste this entire brief into Claude Opus. One PHASE only. Stop when DONE WHEN is met — do not auto-advance to Plan 2 / juice / cosmetics.

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

## Context (read once — do not reopen)

### Locked / ship-ready (DO NOT REWRITE)

| Asset | Status |
|-------|--------|
| Hero Unity POLISH V4 look | **LOCKED** (blonde, white visor orange brim, white polo navy+orange, navy shorts). Known micro-debt on legs taper / hat-hair / shorts piping — **leave it**. No regen. |
| Core tennis gameplay anims (montage) | **APPROVED / BASELINE** — Ready, Serve, Forehand, Backhand (2HBH), Run F/R/L, Forehand Volley, Overhead Smash. Held grip, correct hand orientation, clearance, full motion. Treat as sacred source clips. |
| Product pillars | Cosmetics = identity only; friends-first; no ranked-at-home; no P2W |

### The real problem (why this phase exists)

Clean **montage ≠ in-game**. A real match capture showed **integration / continuity** failures, not “we need new stroke designs.” Gameplay systems (rules, scoring intent) are mostly fine. Work is: **wire Hero V4 + make in-match motion continuous and honest.**

### In-game audit (fix these — this is the checklist)

**P0 — must fix**

1. **Feet sinking into court** — feet/colliders/root penetrate court mesh during ready, run, swing, land.
2. **Serve desync** — serve swing passes **under** the ball, but the serve still **counts** as a successful hit. Contact logic is lying; animation and ball event are not locked to the same frame/pose.

**P1 — must fix**

3. **Skating runs** — locomotion slides without grounded plant / root motion / foot IK mismatch.
4. **Fake rally contacts** — ball “hits” and/or VFX fire on **input** (or early state), not when racket face actually meets ball.
5. **Arm / racket through body** — clipping on **whiff** and **run-stop** (and any similar recover transitions).
6. **Serve torso twist** — unnatural torso spin during serve that reads broken on camera.
7. **Post-whiff freeze** — after a miss, character sticks / freezes instead of recovering into Ready / loco.

**P2 — fix in this phase if cheap; otherwise note for Plan 2**

8. **AI freezes after point** — opponent does not reset to Ready for the next point.
9. **VFX desync** (secondary) — trails/sparks out of phase with contact; prefer fixing with hit-sync, defer “juice design” to Plan 2.

### Explicit non-goals (Plan 2 / 3 — DO NOT DO HERE)

- No new spectacle cameras, hit-stop tuning as a product pass, power-ups, or “fun feel” redesign.
- No missing juice clips (Miss / Perfect / Celebrate) as new authored art — recover with existing states if possible; Plan 3 owns new clips.
- No cosmetics / wardrobe / sockets for Customize beyond what wiring requires.
- No online / Lobby / Relay / second sport.
- Do **not** replace Eyes path with Asset Store tennis packs or Mixamo fight proxies.
- Do **not** regenerate Hero V4 meshes.
- Do **not** casually rewrite approved Ready / Serve / FH / 2HBH / Runs / Volley / Smash source clips. If a clip must change, it is only to fix a **continuity bug** (e.g. serve contact pose alignment), and you must call it out with before/after proof. **Runs stay untouched** unless skating is caused by bad run clip itself (prefer root/IK/Animator first).

**Comp note (awareness only):** *just volleyball* (temkosoft) is the fluidity/over-animation reference for **later** Plan 2. This phase is continuity plumbing, not volleyball copy.

---

## PHASE name

`TennisFluidity_GroundHitBlends`

**One-line goal:** Montage-quality tennis **in a real local match** — grounded feet, honest hit sync, fluid blends, no skate/pops/clipping on common paths, AI ready for next point.

---

## Inputs (inspect first — report what you find)

Before changing anything, inventory and report:

1. **Hero prefab / model** — where POLISH V4 lives; Animator Controller / Playables graph; Avatar; which clips are assigned to Ready / Serve / FH / BH / Runs / Volley / Smash / recover / idle.
2. **Racket** — grip socket / parent bone; how contact collider or trigger is defined; any ball-hit event API.
3. **Locomotion** — root motion on/off; CharacterController / Rigidbody / NavMesh; foot IK; court collider layers.
4. **Serve / rally hit pipeline** — who decides “ball was hit”: animation event, ball proximity, input window, physics overlap, scripted timeline? Where serve success is granted even if racket is under the ball.
5. **VFX hooks** — which FX spawn on input vs on confirmed contact.
6. **AI state after point** — what state machine owns point-end → next-point Ready; why it freezes.
7. **Last known-good** — any prior prefab/controller that had better grounding or sync (restore surgically rather than invent).

Write a short **Diagnosis** section in your reply (bullets + file paths). Then execute the work below.

---

## Work order (do in this sequence)

### Step 0 — Wire Hero V4 (only if not already)

If gameplay still uses an old proxy / wrong mesh / unbound Animator:

- Bind locked Hero V4 to existing tennis gameplay (Avatar, Animator, racket socket, ball/hit hooks).
- Keep Customize-safe sockets intact (do not break future cosmetics).
- Do **not** rewrite tennis rules or scoring design.

If already wired, say so and skip.

### Step 1 — Grounding (P0)

- Feet stay on court surface in Ready, Run F/R/L, swing plants, serve land, recover.
- Fix sink / float / skate via the real cause (root Y, capsule, foot IK, court collider, animation root curves) — not by hiding feet in camera.
- Prove with stills: side view feet at ready; run mid-stride; serve land.

### Step 2 — Hit sync honesty (P0 + P1)

**Hard rule:** Ball contact event, racket contact pose, VFX, and SFX must share the **same logical contact frame**. No success without a real meet.

Specifically:

- **Serve:** If racket is under / past / missing the ball at the claimed contact frame → serve must **fail** (or not grant hit). Align toss height / contact window / animation event / ball position so a good serve shows strings meeting ball.
- **Rally:** Contacts must fire when racket face (string bed) meets ball within a fair gameplay window — **not** on button-down alone.
- Remove or retarget any VFX that currently pops on input; move to confirmed contact (Plan 2 can juice later; this phase only makes them honest).
- Keep party-friendly generous windows if they already exist — do **not** turn this into sniper timing. Fairness of window size is Plan 2; **honesty of “did we hit”** is Plan 1.

### Step 3 — Blends & state continuity (P1)

- Ready ↔ Run ↔ Swing ↔ Recover without pops, freezes, or skate.
- **Post-whiff:** must exit miss into Ready or loco within a short, readable recover — no infinite freeze.
- Prefer Animator transition / blend tree / cancel windows over rewriting sacred swing FBXs.
- If a transition requires a tiny recover clip stub, reuse existing frames or in-place blend; do not author a full Miss/Perfect juice set (Plan 3).

### Step 4 — Clipping & serve torso (P1)

- Arm / racket through torso on **whiff** and **run-stop** (and serve if still present): fix via pose constraints, off-arm clear, transition poses, or socket offset — **without** killing locked swing arcs or implanting the grip.
- Serve torso twist: remove unnatural spin; keep readable kinetic chain (hips → torso → shoulder → arm) per art bible.
- Off-arm: clear of body; never dead-glued to gut (bible law).

### Step 5 — AI next-point Ready (P2 in-phase)

- On point end, AI must return to Ready (or equivalent) and be able to play the next point.
- No stuck idle / frozen celebrate / dead brain.
- If celebrate anim missing, use Ready + short delay rather than inventing Plan 3 juice clips.

### Step 6 — Light VFX desync only if leftover after Step 2

- If contact-locked and still wrong, sync spawn to contact event.
- Do **not** design new perfect/smash camera packages here.

---

## Surgical constraints (from AGENTS / art bible — enforce)

1. Full clips, never collapse to frozen stubs.
2. Hands **hold** racket (finger cradle); no implanted pole-through-fist.
3. At contact, **string bed faces ball/net** — not edge-sword.
4. Runs are sacred — touch only if proof shows run clip itself causes skate; prefer locomotion/root/IK first.
5. One PHASE — stop at proof; do not start Plan 2.
6. Prefer before/after capture over “feels better.”

---

## Outputs (required)

Create/update under project (match existing folder conventions; suggest):

```
ArtDir/screenshots/plan1_fluidity/
  00_diagnosis_notes.md          # what you found (paths, root cause bullets)
  01_feet_ready_side.png
  02_feet_run_mid.png
  03_serve_contact_frame.png     # strings meet ball OR honest miss
  04_rally_contact_frame.png
  05_whiff_no_clip.png
  06_runstop_no_clip.png
  07_postwhiff_recover.png       # or short clip
  08_ai_nextpoint_ready.png
```

Plus a **short in-match capture** (local play, mute-readable) showing:

- Grounded ready + run
- One honest serve (contact or honest fault)
- One rally contact with VFX on contact
- One whiff with recover (no freeze, no body clip)
- AI ready for next point after a point ends

Name suggestion: `ArtDir/anims/captures/Plan1_Fluidity_InMatch.mp4` (or project’s existing capture folder).

---

## DONE WHEN (all must pass)

Adnan can watch a local match clip and confirm:

1. Feet do not sink into the court on ready / run / land.
2. Serve success only when racket meets ball; under-ball swing does not count.
3. Rally hits / VFX are contact-locked, not input-locked.
4. No skate on runs; no freeze after whiff.
5. No arm/racket-through-body on whiff and run-stop (common paths).
6. Serve torso reads natural (no broken twist).
7. AI resets to Ready for the next point.
8. Locked Hero V4 + approved swing set still visibly in use (no proxy swap, no frozen stubs).

Reply format when done:

- **Diagnosis** (short)
- **What changed** (concrete file/controller/script paths)
- **Output paths**
- **Still open / deferred to Plan 2–3**
- **Do not** propose auto-starting Plan 2

---

## STOP

End the phase. Wait for Adnan approval before Plan 2 (Fun feel).
