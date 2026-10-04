# BUILD: Hero Base Male + Female Lock (character first)

Paste into **Codex / Claude / Astra**. Execute in order. Stop only at GATE.

**Goal:** Ship **Hero Base M/F** that look **exactly** like the locked plates — soft plastic, expressive faces, solid opaque hair — game-ready Unity URP. **Just make the character.** Rig/Mixamo/existing-animator binding is **out of scope** unless Adnan asks later.

**Project:** `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`  
Read: `AGENTS.md`, `art-bible.md`

**Authority plates (NON-NEGOTIABLE):**
- `ArtDir/hero/base_lock/male_body_plain.jpg`
- `ArtDir/hero/base_lock/female_body_plain.jpg`

Clothes / bandana / fashion are **OUT OF SCOPE**. Body = grey mannequin suit; identity = head + hair + proportions.

---

## Why Blender usually degrades
Remeshing, over-smoothing, wrong scale, hair cards, baking lighting into albedo, crunch textures, wrong FBX units, “stylizing further.” **Forbidden.** Pixel-match the plates.

## Product locks
1. **Exact look** — overlay vs plates ≤ ~5% silhouette.
2. **Opaque solid hair** as separate mesh.
3. **Mobile URP** — Lit; hair Opaque; ~35–50k tris; head 1K–2K, body 1K.
4. **No Tripo full regen** that drifts from plates.

---

## PHASE 0 — Freeze (likely done)
Plates + `ACCEPTANCE.md` + `METRICS.md` + folders `proof/`, `blender/`, `unity_import/`. Verify then continue.

## PHASE 1 — Blockout
Blender meters; height ≈ 1.65–1.75m; origin feet. Silhouette overlays → `proof/01_silhouette_overlay_m.png` / `_f.png`.

## PHASE 2 — Model to plate
Background Images locked. Objects: `Body_M`, `Hair_M`, `Body_F`, `Hair_F`. Solid opaque hair. Grey body; head/hair match plate. Overlays → `proof/02_match_m.png` / `_f.png`.

## PHASE 3 — Unity import (look gate)
FBX scale 1, FBX All, -Z/Y; apply modifiers; freeze transforms. URP Lit; Hair Opaque; no face crunch. Prefabs `HeroBase_Male` / `HeroBase_Female` under `Assets/Characters/HeroBase/`. Unity proofs → `proof/03_unity_m.png` / `_f.png`.

## GATE (all PASS or FAIL)
### Fidelity
- [ ] Male front/back overlay ≤ ~5%
- [ ] Female front/back overlay ≤ ~5%
- [ ] Face / hair match plate language; hair opaque
- [ ] Unity not softer/flatter/yellower than Blender match

### Tech
- [ ] Separate Body + Hair meshes
- [ ] Height/scale correct; tris in budget
- [ ] Prefabs + proof images written

### Process
- [ ] `proof/GATE_RESULTS.md` with PASS/FAIL lines
- [ ] No fashion meshes as “done”

**Reply:** `GATE: BASE PASS` or `GATE: BASE FAIL` + lines.

## Don’t
- Improve proportions off plates · transparent hair · clothes/bandana · Mixamo/animator work · done without Unity proof

## Bald reference update — 2026-09-30

Adnan supplied replacement bald male and female references. The current base_lock male_body_plain.jpg and female_body_plain.jpg are those unchanged files; earlier hair-on plates are archived under base_lock/archive/with_hair_20260930/. This explicit update supersedes earlier hair-on identity statements. Match the complete bald head, face and grey body. Main HeroBase_Male/Female prefabs are bald; hair stays a separate optional asset and must never supply missing scalp, ears, face or neck. Optional hairstyle previews are separate from the base comparison. No clothes, rig, Mixamo or Animator.
