# SIDE PASS: HeroV5_AstraTextureProjection (albedo / materials only)

Paste into **Codex with Astra** (or Claude if running Blender bpy). This is a **parallel texture experiment** on the current V5 / V4 hero mesh. It does **not** replace `PLAN_HeroLookShip_FinishedProduct.md` Phase 0–1 mesh gates (opaque hair volume, legs silhouette, dive shorts, sockets).

**Goal:** Use ImageGen orthographic / turnaround projections → project onto the hero → bake into real UV textures → Unity URP materials, and see if surface beauty gets closer to `ArtDir/hero/v5_target/` + plate `01_hero.png` **without changing topology or bind**.

---

## Inspiration (method claim)

> I placed an untextured 3D model in the scene and asked Astra to use ImageGen projections to texture the model, then bake those projections into actual textures and it worked on the very first try!

**Reproduce that workflow** on a **duplicate** V5 hero: untextured (or stripped) mesh in scene → ImageGen projections → project → bake to real UV textures → Unity. First-try beauty is the bar; if seams/identity fail, iterate gens — do not “fix” by reshaping the mesh.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`

**Authority look:**
- `ArtDir/hero/v5_target/HERO_V5_TARGET_turnaround.png`
- `ArtDir/hero/v5_target/HERO_V5_TARGET_hair_closeup.png`
- `ArtDir/hero/v5_target/HERO_V5_TARGET_legs.png`
- `ArtDir/plates/01_hero.png`
- `art-bible.md`, `ArtDir/hero/HERO.md`

**Product:** Party chibi tennis — Switch Sports / Wii clarity + soft plastic beauty. Cosmetics = identity. Mobile URP.

---

## Hard locks (do not break)
1. **Mesh / topology / Mixamo humanoid bind** — no remesh, no retopo, no new skeleton, no weight re-paint unless bake requires a temporary cage (discard after).
2. **Customize sockets** — Hair / Visor / Top / Bottom / Shoes / Racket paths unchanged.
3. **Hair must stay URP Lit Opaque** after bake — never reintroduce transparent/dither hair “softness.”
4. **No face Tripo / full body regen.**
5. Work on a **duplicate** prefab/mesh set: `Hero_V5_AstraTex_*` — keep V4/V5 gameplay prefab until Adnan promotes.
6. Look Ship Phase 0 inventory still required if not done; this pass may run after baseline capture.

---

## What “success” looks like
Side-by-side in customize + gameplay:
- Skin / polo / shorts / shoes / hair **color and plastic soft-toy shading** closer to plate + v5_target
- Seams acceptable under turnaround; no projection stretch on face
- Hair still solid (no see-through)
- 60fps-feel materials (no mega 4K atlases on mobile)

If mesh defects remain (leg pipes, scalp holes, dive clip), say so honestly — texture cannot fix those. Reply `GATE: TEX PASS` only if albedo/material beauty clearly improved; else `GATE: TEX FAIL` + why.

---

## PHASE T0 — Prep (inventory + UV health)

### Do
1. Duplicate hero mesh set into `ArtDir/hero/v5_astra/` working folder + Unity duplicate prefab.
2. Confirm UVs exist per part (body, hair, top, bottom, shoes, visor). If broken: light unwrap **only** the failing piece; log before/after.
3. Export clean FBX/GLB of **untextured or current** hero for Blender (T-pose or A-pose bind pose).
4. Capture baseline renders: front / 3/4 / side / back / hair closeup → `ArtDir/hero/v5_astra/baseline/`.

### Done when
Baseline folder + UV notes in `ArtDir/hero/v5_astra/TEX_NOTES.md`. **STOP if UVs are unusable** — report; don’t invent projections on overlapping islands.

---

## PHASE T1 — ImageGen projection set

### Do
1. Using Astra ImageGen (or equivalent), generate **matching projections** from authority plates (same character identity: blond, visor, polo, stocky legs):
   - Front orthographic full body
   - Back orthographic
   - Left / right orthographic (or 3/4 L/R if ortho fails identity)
   - Face + hair closeup (match `HERO_V5_TARGET_hair_closeup`)
   - Legs/shorts/shoes closeup (match `HERO_V5_TARGET_legs`)
2. Style lock: soft plastic party chibi, not photoreal, not Roblox noisy. Match plate palette.
3. Save all to `ArtDir/hero/v5_astra/projections/` with names `proj_front.png`, `proj_back.png`, etc.
4. Adnan must be able to reject bad gens — if identity drifts, regenerate before bake.

### Don’t
Generate a new body shape and sculpt to it. Projections dress **this** mesh only.

### Done when
Projection set exists and identity matches plates. **STOP for Adnan OK on projections** before bake if any face/hair drift.

---

## PHASE T2 — Project + bake in Blender

### Do
1. Import duplicate mesh; set up cameras or empties aligned to each projection.
2. Project images onto corresponding views (Blender texture paint Project from View / equivalent bpy). Prefer **per-part** bake (skin, hair, cloth, shoes) over one giant atlas if islands allow.
3. **Bake** projected colors to UV image textures (PNG). Resolve seams with clone/heal on UV paint where needed.
4. Optional light AO bake as separate map — keep subtle for mobile.
5. Export textures → `ArtDir/hero/v5_astra/baked/` + materials notes.
6. Hair: bake must remain treatable as **Opaque** base color (no alpha cutout for volume).

### Don’t
Change vertex positions; merge hair into skull; use transparency to hide gaps; bake only from one camera (will fail back/side).

### Done when
Baked maps + Blender screenshot of materials. Proof folder `ArtDir/hero/v5_astra/proof_blender/`.

---

## PHASE T3 — Unity URP wire-up

### Do
1. Import baked maps; assign URP Lit (or art-bible shader) on duplicate hero materials.
2. Hair = Opaque; skin/cloth matte per art-bible; soft specular, low bloom dependency.
3. Drop into **customize locker** and **in-game** scenes on duplicate prefab only.
4. Capture proof: customize closeup + full turn + in-game ready + serve arms-up → `ArtDir/hero/v5_astra/proof_unity/`.
5. Compare to baseline + v5_target; write `ArtDir/hero/v5_astra/GATE_TEX.md` with `GATE: TEX PASS` or `GATE: TEX FAIL` and binary lines:
   - [ ] Face identity closer to plate
   - [ ] Hair color/opacity (still opaque)
   - [ ] Polo / shorts / shoes read party soft-plastic
   - [ ] Legs texture helps silhouette read (even if mesh still wrong)
   - [ ] Seams acceptable 360°
   - [ ] Customize sockets still work
   - [ ] Mobile-sane texture sizes

### Don’t
Overwrite production hero until Adnan says promote. Don’t start Phase 2 lighting “to hide” bad bake.

### Done when
Proof pack + gate file. **STOP for Adnan.**

---

## Operating rules
1. Texture pass ≠ mesh pass. If legs/hair structure still fail Look Ship Phase 1, keep Phase 1 alive.
2. Prefer promote only materials that win side-by-side; discard the rest.
3. Log every path changed.
4. One bake loop max after Adnan feedback, then stop.

---

**Start with PHASE T0 only**, then T1 projections — pause for Adnan before T2 bake if face/hair identity is unsure.
