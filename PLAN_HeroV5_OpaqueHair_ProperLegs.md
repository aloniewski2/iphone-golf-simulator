# PHASE: HeroV5_OpaqueHair_ProperLegs (avatar rebuild — hard gates)

Paste into Claude Opus / coding agent. **This replaces soft “polish hair/legs” asks.**  
Goal: close the last 10% that agents keep missing. Ship **Hero V5 modular** that **cannot** show transparent hair gaps and has **plate-correct legs**.

**Authority images (non-negotiable — copy into `ArtDir/hero/v5_target/` before work):**
- `HERO_V5_TARGET_turnaround.png` — full body FRONT / 3-4 / SIDE / BACK
- `HERO_V5_TARGET_hair_closeup.png` — hair acceptance (opaque)
- `HERO_V5_TARGET_legs.png` — legs acceptance
- Plate lock: `ArtDir/plates/01_hero.png`
- Current broken proof (must not recur): customize hair gap; 12-angle review video knees/feet panels

Also read: `AGENTS.md`, `art-bible.md`, `ArtDir/hero/HERO.md`.

---

## Why previous passes failed (read this)

Agents treated hair/legs as “shader tweak + micro sculpt” and declared done. Failures were **structural**:
1. Hair used transparent / card / dither paths or split volumes → horizontal see-through gaps in customize UI.
2. Legs kept Tripo taper / bad knee weights → stick calves, collapse on bend, shorts look wrong.
3. No **binary acceptance tests** — “looks better” was allowed to pass.

**This phase only passes if the gate checklist is 100% green.** Partial credit = FAIL. Do not move to anim polish until gates pass.

---

## Product decision

| Keep | Rebuild |
|------|---------|
| Face identity, visor language, polo/shorts colors, Mixamo humanoid scale ~1.7m, socket names | **Hair mesh + materials** (opaque solid locks) |
| Existing animator controller wiring where possible | **Legs from pelvis to shoes** (proportions + topology + weights) |
| Customize slot architecture | Any LOD/material that reintroduces transparency on hair |

Call the result **Hero POLISH V5**. V4 stays archived; do not keep shipping V4 hair/legs.

---

## Hard rules

1. **Hair = opaque mesh volumes.** URP Lit opaque (or alpha-clip fringe only on thin wisps ≤5% of hair tris). **Forbidden on solid locks:** Transparent queue, dither fade, density maps that open holes, missing backfaces that show background, LODs that delete mid-volume.
2. **If background is visible through hair in ANY camera → FAIL.** Including customize UI, gameplay, ultimate closeup, A-pose turnaround.
3. **Legs match plate stocky toy-athlete:** full calves, thick thighs, correct shorts hem, knees with deformation loops; no stick taper, no imploded kneecap on bend.
4. **No full-face regen** unless required for seam — prefer keep head; rebuild hair as separate mesh; rebuild lower body as modular legs or full lower mesh with waist seam under polo.
5. Mobile budget: total hero ≤ ~50k tris unless Adnan approves; prefer ~45k like V4.
6. Customize-safe: Hair / Visor / Top / Bottom / Shoes / Racket sockets; scalp covered when hair off.

---

## Execution (do in order — stop only at GATE)

### Step 0 — Freeze targets
1. Copy the three `HERO_V5_TARGET_*.png` + `01_hero.png` into `ArtDir/hero/v5_target/`.
2. Write `ArtDir/hero/v5_target/ACCEPTANCE.md` with the gate list below (verbatim).
3. Screenshot current broken hair + legs into `ArtDir/hero/v5_target/BEFORE/` for before/after.

### Step 1 — Hair rebuild (Blender)
1. Delete or archive V4 hair mesh; do **not** “patch” transparent cards.
2. Sculpt / model **solid lock volumes** matching TARGET hair closeup + plate (swept blond, visor clearance).
3. Normals outward; manifold where possible; no open mid-scalp slice.
4. Material: URP Lit **Opaque**, matte hair roughness ~0.55–0.65, no alpha.
5. Optional: separate 1–2 fringe cards with **alpha clip** only at tips — never mid-skull.
6. Skin weights: ears/nape tested in run + look-up; no gap opens.

### Step 2 — Legs rebuild (Blender)
1. From plate + TARGET legs: rebuild thighs, knees, calves, ankles, shoes (or shoe mesh separate).
2. Add deformation-friendly topology around knees (edge loops).
3. Shorts: correct length, orange piping continuous, no float geo.
4. Re-bind Mixamo-compatible humanoid; re-normalize weights; test squat / run / ready / serve knee bend in Blender or Unity.
5. Compare silhouette side-by-side to `01_hero.png` and `HERO_V5_TARGET_turnaround.png` at equal height.

### Step 3 — Unity import + customize
1. Replace prefab meshes; keep bone names / avatar definition compatible.
2. Kill any Material that forces Transparent on hair.
3. Verify LOD0–LODn: hair never becomes see-through cards.
4. Customize swaps: hair A/B, visor on/off, top, bottom, shoes.

### Step 4 — Proof pack (required deliverable)
Export to `ArtDir/hero/v5_proof/`:
1. Turnaround sheet (same 4 angles as target)
2. Customize UI close-up (same framing as old hair-bug shot)
3. Knees-and-feet panel (match 12-angle “KNEES AND FEET” cam)
4. Ready + backhand + serve knee frames
5. `GATE_RESULTS.md` with pass/fail per line

---

## GATE CHECKLIST (all must be PASS or phase FAILS)

### Hair
- [ ] Customize close-up: **zero** background visible through hair
- [ ] Hair material Surface Type = **Opaque** on solid locks (screenshot Inspector)
- [ ] No horizontal slice / floating top volume
- [ ] Visor sits clean; no hair clipping through brim in ready pose
- [ ] Ultimate / low front closeup: hair still solid
- [ ] Hair unequipped → scalp or cap mesh visible (no hole to skull interior)

### Legs
- [ ] Side silhouette calves match plate thickness (not stick taper)
- [ ] Knees in ready bend: volume preserved (no candy-wrapper collapse)
- [ ] Shorts hem stable; piping unbroken
- [ ] Feet/shoes grounded; no spike ankles
- [ ] Left/right symmetry OK in front view
- [ ] Run cycle 2 steps: no mesh explosion at knee

### Identity / tech
- [ ] Still reads as same hero vs `01_hero.png` (blond + visor + polo language)
- [ ] Mixamo humanoid + existing animator plays without retarget disaster
- [ ] Racket hand sockets still valid
- [ ] Before/after proof pack written

**If any box unchecked → do not say done. Fix and re-run gates.**

---

## Don’t
- Declare success on “improved shader”
- Keep V4 transparent hair “for stylized fringe”
- Full Tripo body regen that changes face identity without Adnan OK
- Touch Score80 gameplay phases in this task
- Skip proof screenshots

---

## Done when
Adnan can flip `BEFORE/` vs `v5_proof/` and see: solid hair, stocky correct legs, same character. Agent reply starts with `GATE: ALL PASS` or `GATE: FAIL` + failing lines only.

**STOP for Adnan visual approval before deleting V4 from the live prefab permanently (keep V4 prefab backup).**

---

## 12-angle review video — confirmed defects (must eliminate)

Source: Adnan 12-angle gameplay animation review (ready / serve / smash / run / dive).

1. **Severe shorts clipping** — dive ~00:27: thigh/knee punches through shorts. Fix shorts geo + weights (or modular bottom) for extreme bends.
2. **Hair slices & scalp holes** — overhead/back: deep gaps between lock clumps; bangs as floating cards under visor. Rebuild solid volumes.
3. **Lower-leg silhouette** — calves read as thick cylinders with wrong ankle transition. Rebuild stocky toy-athlete: full calf **then** clean step into chunky shoe (not stick taper, not pipe-leg).
4. **Knee mesh collapse** — deep crouch on jump serve / landing pinches rubbery. Knee loops + weight fix.
5. **Visor intersection** — side strap clips through hair; strap must sit **over** hair volume.
6. **Armpit webbing** — arms up on serve: harsh stretch. Surgical underarm geo + weights (don’t regen torso casually).

### Extra GATE lines
- [ ] Dive / deep squat: **no** thigh through shorts
- [ ] Overhead hair: no scalp holes / dark voids between locks
- [ ] Visor strap over hair (ready + run)
- [ ] Serve arms-up: no severe armpit webbing
- [ ] Ankle→shoe transition intentional (chunky shoe), not pipe-leg


---
**Superseded for full look-ship:** use `PLAN_HeroLookShip_FinishedProduct.md` (includes this mesh work as Phase 1 + lighting + scenery + presentation).
