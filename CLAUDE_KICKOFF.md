# Claude Opus kickoff — Party Sports (tennis anim)

Paste this whole file into Claude first, then paste `AGENTS.md` + `art-bible.md` into the project root (overwrite/align with existing copies).

---

## Who you are / who Adnan is
You are the hands-on executor (Blender, Unity, mocap retarget, Animator). Adnan is the director. Grok Bot was the prior router drafting Codex phases; quality on tennis anim **regressed** across iterative “surgical” passes, so Adnan is rotating execution to **you (Opus)**.

Project: friends-first iPhone party sports game (Wii Sports / Switch Sports / Fall Guys energy).  
Path: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`

---

## What is DONE (do not reopen)

| Area | Status |
|------|--------|
| Art bible + pipeline | Locked |
| Higgsfield plates (hero look direction) | Locked |
| Hero mesh look — Unity POLISH V4 | **LOCKED** (blonde, white visor orange brim, white polo navy+orange, navy shorts). Known micro debt on legs/hat/hair/shorts — **leave it** |
| Mixamo humanoid bind | In place |
| Product: cosmetics-only, friends-first, no ranked-at-home | Locked |

---

## What we are working on NOW

**Tennis gameplay animation ownership** for the locked chibi hero:
Ready, Serve, Forehand, Backhand (two-hand), Forehand Volley, Overhead Smash, Run F / R / L.

Goal: first-party `Hero_*` clips that read on a phone mute: full strokes, racket **held**, **strings face the ball**, party stylize on top of real tennis timing.

Preferred source: **Eyes Japan** tennis mocap (better kinetic chain than Mixamo proxies or a weak ~$19 Asset Store tennis pack). Pipeline intent: retarget → clean (in-place root, feet, clasp) → stylize → rename — never ship raw Eyes branding.

---

## What we are struggling with (read carefully)

### The pattern of failure
Earlier Eyes-based set had **better hitting mechanics** but broken grips/poses. We tried many surgical Codex phases (arm candy-wrap, socket, backhand side, volley, grip/face). Each pass was supposed to fix one thing. The **latest montage is worse than before**: clips collapsed into **frozen single poses**, runs that were “perfect” **regressed**, and grip/face are still wrong everywhere.

### Latest montage verdict (2026-09-26, post BackhandFull_VolleySwing_GripFace attempt)

Universal fails:
1. **Frozen / stubbed** — almost every “clip” is a static keyframe, not a fluid stroke.
2. **Implanted grip** — racket handle goes through closed fists (doughnut), not curled fingers holding.
3. **Edge-flat racket** — swung like a sword; string bed does not face ball/net at contact.
4. **Dead left arm** — often glued across the stomach.
5. **No hip unit turn** on strokes that need it.
6. **Volley** — racket covers the face; not a short punch swing.
7. **Runs regressed** — Run F has dead right arm dangling; R/L stiff. (Previously Runs were the one thing that looked shippable — touching them was a mistake.)

What still works:
- Hero mesh itself intact (no melt / T-pose disaster).
- Intended action per slot still vaguely recognizable from silhouette alone.

### Recurring technical suspects (investigate; don’t guess-fix forever)
- Right-hand **socket / parent** wrong → mesh-through-palm “implant”
- Finger / hand pose never authored (fist + cylinder)
- Retarget or Animator showing **wrong clip / wrong frame / frozen blend** (montage looks like stills)
- Over-aggressive “cleanup” that deleted motion and left end poses
- Two-hand BH authored as mirrored FH or arm-only flick
- Volley never given prep+punch+recover keys
- Runs overwritten when the brief said do not touch

### Explicit non-goals right now
- Do **not** unlock hero wardrobe / proportions / new Tripo
- Do **not** switch to a cheap Asset Store tennis pack as the “solution”
- Do **not** use Mixamo combat proxies as final tennis swings
- Do **not** “fix” by baking a nicer T-pose or single still per state

---

## What success looks like (next milestone)

Ship a new Unity montage (same clip order) where:

| Clip | Pass bar |
|------|----------|
| Ready | Athletic; both hands make sense; racket held; face not edge-weird |
| Serve | Full motion (toss/load → hit → land), held grip, strings meaningful at contact |
| Forehand | Full stroke + hip/shoulder turn; held; strings to ball at contact |
| Backhand | **Full 2HBH**: unit turn, drop, low→high, wrap; two hands **holding** handle; strings to ball; racket not covering face all takeback |
| Run F/R/L | Natural run with racket carried (held), arms alive — if a prior good take still exists, **restore it** rather than reinvent |
| Forehand volley | Short real swing: prep + punch + recover; face to ball; face not covered by frame |
| Smash | Full overhead follow; held; strings readable |

Plus stills: hand close-up proving grip cradle; BH load (hips) + contact (face + 2 hands); volley contact.

**Done when** Adnan says the montage passes — then stop.

---

## How to work with Adnan
1. Read `AGENTS.md` + `art-bible.md`.
2. First reply: short diagnosis of **why** the last pass froze clips / broke grip (inspect project assets, Animator, FBX, sockets) — then propose **one** PHASE plan.
3. Wait for Adnan to confirm the PHASE (or he pastes one).
4. Execute only that phase. Prove with montage + stills. Do not auto-chain five more “quick fixes.”

### Suggested first PHASE name
`Animator_DiagnoseAndRestore`  
- Inventory current Hero_* / Eyes / Mixamo tennis clips and which Controller/Playable slots they feed.  
- Diff vs last known-good Runs (restore if overwritten).  
- Find root cause of frozen montage + implant grip (socket vs missing finger pose vs wrong clip assignment).  
- Report findings + proposed single fix phase (likely: restore motion sources, then GripSocket once, then BH+Volley only).

---

## One-line struggle summary for Claude

> We have a locked chibi tennis hero; Eyes Japan mocap had the right energy, but iterative “surgical” anim passes destroyed fluid motion into frozen poses, left the racket implanted through fists and swung edge-on like a sword, glued the off-arm to the gut, and even broke runs that were already good — we need diagnosis + restore of real strokes, then a real hold grip and string-face-to-ball, without regenerating the character or swapping to weak store packs.
