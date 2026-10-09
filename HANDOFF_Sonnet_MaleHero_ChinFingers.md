# HANDOFF — Male HeroBase → Claude Sonnet (chin + fingers)

**Owner:** Adnan  
**Date:** 2026-10-01  
**Repo:** `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`  
**Product:** Friends-first iPhone party tennis (Unity URP). Soft-plastic beauty. Clothes/fashion OUT OF SCOPE.

Read this file first, then `PROCESS_Blender_Claude.md` (Claude×Blender SOP), then `AGENTS.md`, `art-bible.md`, `ArtDir/hero/HERO.md`, then the Micro1e goal.

---

## Mission (NOW)

Finish **male body only** so Adnan’s eye-check PASSes:

1. **CHIN** — match bald plate jaw/chin/under-chin/neck (no soft dough recess; no beard slab). Head can stay untextured/blank eyes+mouth this pass, but **chin silhouette + volume must match plates**.
2. **FINGERS** — readable soft-plastic individual fingers + thumb (not mitt stubs / claw spikes). Match `male_body_bald.jpg` hand mass.

Do **not** remesh restart. Do **not** touch female. Do **not** invent a ≤2% silhouette gate — use **≤5.0%** bald front/back only.

---

## Authority plates (primary)

| Asset | Path |
|-------|------|
| Male bald body (PRIMARY) | `ArtDir/hero/base_lock/male_body_bald.jpg` |
| Male multi-angle | `ArtDir/hero/base_lock/male_multiangle_body.jpg` |
| Male head detail | `ArtDir/hero/base_lock/male_head_detail.jpg` |
| Index | `ArtDir/hero/base_lock/REF_INDEX.md` |

Haired `male_body_plain.jpg` is legacy. Prefer bald.

---

## Current mesh state (2026-10-01 ~11:32 ET)

### Saved authority blend / Unity lock (Hand_4 / Form D)
- Blend: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`
- SHA-256: `d39c6c5e5157ff15428759c01eaa227a09ea1bcd4b0d1a51acfe4110795d2413`
- FBX SHA (last reported): `5dd325f0a77c30605ffa2d78b4c6457dd13781664cf5b9d716e8390aa7e3565d`
- Height ≈ **1.702 m**, A-pose, METRIC, Body_M only edits, ~24.5k verts / ~49k tris
- Unity prefab: `HeroBase_Male` (grey URP Lit, bald, no hair)

### WIP (do not confuse with ArtDir lock)
- `work/male-jaw-5/outline/candidate.blend` — chin experiment only  
- Geometry fact: **body below z≈1.35 identical to ArtDir**; only head/chin verts moved (~11 mm max). Outline chin is **softer**, not closer to plate → treat WIP as discardable unless it clearly improves CHIN vs plates.

**Start from ArtDir `HeroBase_Male_Silhouette.blend`**, not the outline candidate, unless you verify outline chin is better against `male_head_detail.jpg` / bald plate profiles.

---

## What already PASSes (Adnan + Grok Bot eye-check — do not regress)

| Check | Status | Notes |
|-------|--------|-------|
| SHOULDERS | PASS | Rounded deltoids; no collapsed pit at arm join |
| WAIST | PASS | Love-handle shelf gone; taper OK |
| ARMS_SIDE | PASS | Soft-plastic depth from side (not flat card) |
| FEET_SIDE | PASS | Plantable boot/foot; not twisted slab |

## What still FAILs (must fix)

| Check | Status | Notes |
|-------|--------|-------|
| CHIN | **FAIL** | Soft/recessed jaw→neck; blank head OK; chin shape wrong vs plate |
| FINGERS | **FAIL** | Mitt / blocky claw tips; plate has separate soft-plastic fingers + readable thumb |
| SIL_% | optional | Last measured ~**3.77% / 3.78%** bald front/back. Gate is **≤5.0%**. Do **not** use 2%. Front/back plate pair has documented ~6% mask conflict — do not chase 2%. |

Older agent reports claiming `≤2%` are **wrong for this handoff**. Adnan’s Micro1d / Micro1e rule is **≤5%**.

---

## Proof pack (latest packaged)

`ArtDir/hero/base_lock/proof/`
- `03_unity_m.png` — Unity form D sheet (GATE FAIL)
- `01d_form_m.png` — Blender body views
- `01h_hand_form_m.png` — thumb fix before/after; fingers still unfinished
- `01e_head_all_around_m.png` — head; CHIN FAIL noted
- `MALE_FORM_RESULTS.md`, `MALE_HAND_4_RESULTS.md`, `MALE_FORM_D_COMPLETION_AUDIT.md`
- Fresh eye renders (optional): `work/male-jaw-5/grok_eye/`

---

## Hard rules

1. Continue existing mesh — **no remesh / no Tripo regen / no mesh replacement**.
2. Edit `Body_M` coordinates only (or clearly document if head is a separate object — keep one exportable body).
3. Metres; FBX scale 1; Unity importer scale 1; compression off.
4. No clothes, bandana, fashion, Mixamo, animator rewiring, female work.
5. Binary report only: `GATE: MALE_FORM PASS` or `FAIL` with failed lines.
6. Persist proofs under `ArtDir/hero/base_lock/proof/` and overwrite `03_unity_m.png` when Unity updates.
7. Hair stays optional separate asset later — base stays bald.

---

## Deliverables (Micro1e)

1. Updated `HeroBase_Male_Silhouette.blend` saved to ArtDir blender path (and SHA in results).
2. Fresh FBX → Unity `HeroBase_Male`.
3. Proofs:
   - `proof/01e_chin_fingers_m.png` — front/back/side/3q body + chin closeup + both hands closeup
   - `proof/03_unity_m.png` — updated Unity captures
   - `proof/MALE_FORM_RESULTS.md` — final gate lines
4. Reply first line exactly:
   - `GATE: MALE_FORM PASS`
   - or `GATE: MALE_FORM FAIL` + `CHIN` / `FINGERS` / `SIL_%` / regress lines (`SHOULDERS` `WAIST` `ARMS_SIDE` `FEET_SIDE`)

---

## Micro goals map

| Goal | File | Status |
|------|------|--------|
| Micro1 silhouette | `GOAL_HeroBase_Micro1_MaleSilhouette.md` | FAIL (~5–8%) |
| Micro1b bald ≤5% | `GOAL_HeroBase_Micro1b_…` | PASS (~4.4/3.6) then continued |
| Micro1c form | `GOAL_HeroBase_Micro1c_…` | FAIL by eye (waist/shoulders then fixed) |
| Micro1d human | `GOAL_HeroBase_Micro1d_MaleFormHuman.md` | Partial — body PASS items; CHIN+FINGERS FAIL |
| **Micro1e chin+fingers** | `GOAL_HeroBase_Micro1e_ChinFingers.md` | **← DO THIS** |
| Micro2+ female / heads / Unity look | Micro2–5 | After male PASS |

---

## Adnan review style

He rejects soft “looks better.” He will eye-check Unity + Blender against bald plates. Prefer short binary gates. Keep runs **micro** — chin + fingers only this turn.
