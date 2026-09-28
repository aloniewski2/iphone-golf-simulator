# MASTER PLAN: HeroLookShip_FinishedProduct (characters + light + scenery)

Paste into Claude Opus as the **controlling brief**. Execute **one PHASE at a time**. Stop after each phase for Adnan approval + proof shots. Goal: get **as close to a finished visual product as possible** — customize locker room AND in-game — by killing everything that currently makes characters look worse than `ArtDir/hero/v5_target/` and plate `01_hero.png`.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`
Read: `AGENTS.md`, `art-bible.md`, `ArtDir/hero/HERO.md`, `ArtDir/plates/01_hero.png`.

**Authority look (non-negotiable):**
- `ArtDir/hero/v5_target/HERO_V5_TARGET_turnaround.png`
- `ArtDir/hero/v5_target/HERO_V5_TARGET_hair_closeup.png`
- `ArtDir/hero/v5_target/HERO_V5_TARGET_legs.png`
- Plate: `ArtDir/plates/01_hero.png`

**Also kill defects from 12-angle review:** shorts dive clip, hair scalp holes, pipe/wrong ankles, knee collapse, visor through hair, armpit webbing.

**Product:** Friends-first iPhone party tennis. Cosmetics = identity only (no P2W). Mobile URP, 60fps feel.

---

## North star

Customize close-up and gameplay wide shot should both read: **Switch Sports / Wii clarity + soft plastic beauty** — solid hair, stocky correct legs, warm flattering light, postcard court that doesn’t undercut the hero. Not Roblox, not film-only Pixar.

**Anti-goal:** “shader tweak” partial fixes; transparent hair; flat locker lighting; blob trees next to detailed heroes; declaring done without GATE proof.

---

## Locks
- Face identity language (blond, visor, polo colors) stays unless Adnan OK.
- Mixamo humanoid + existing animator wiring where possible.
- Plan 2C camera playability soft law stays (live ball = play cam).
- No online / sport 2 / shop economy in this plan.

---

## Global GATE (final product bar)

After all phases, Adnan captures:
1. Customize UI close-up (hair + face)
2. Customize full-body turn
3. In-game ready (back playing cam)
4. Serve / ultimate closeup
5. React cam / wide env

Independent check: hero within ~85–90% of v5_target sheets; env not embarrassing next to characters; light matches art-bible. Agent must end with `GATE: ALL PASS` or `GATE: FAIL` + lines.

---

# PHASE ORDER

---

## PHASE 1 — HeroV5_MeshRebuild (hair + legs + clip killers)

**Goal:** Structural character beauty. Nothing else compensates if this fails.

### Do
1. Archive V4 hair/legs; rebuild per `PLAN_HeroV5_OpaqueHair_ProperLegs.md` gates (opaque solid hair; plate legs; knee loops; visor over hair; no dive thigh-through-shorts; no armpit webbing on serve).
2. Materials: URP Lit **Opaque** hair locks; matte cloth/skin per art-bible; kill dither/transparent hair paths; LODs must not reintroduce gaps.
3. Customize-safe sockets: Hair / Visor / Top / Bottom / Shoes / Racket; scalp cover when hair off.
4. Proof: `ArtDir/hero/v5_proof/` turnaround + customize closeup + knees/feet + dive frame + Inspector hair Opaque screenshot.

### Don’t
Shader-only “fix”; full face Tripo regen without OK; skip dive test.

### Done when
`GATE: ALL PASS` on Hero V5 checklist. **STOP for Adnan.**

---

## PHASE 2 — LightingBeauty_CustomizeAndGame (characters look lit, not gray)

**Goal:** Sheet beauty is half lighting. Match flattering light in locker + court.

### Do
1. **Art-bible light:** warm key 35–45° elev; fill 30–40% of key; soft shadows; cool shadow tint; bloom low; no grain.
2. **Customize / locker room:** rebuild or retune lights so face + hair read like `HERO_V5_TARGET_hair_closeup` (soft key, rim optional, no flat ambient-only, no harsh specular blobs).
3. **Gameplay court:** same key/fill philosophy; characters slightly more saturated than world; contact shadows readable.
4. **Cinematic moments** (toss / ultimate / react): keep 2C playability; light still flattering on closeups (no sudden underexposed faces).
5. Exposure / URP volume: one consistent look; document values in `ArtDir/hero/v5_proof/LIGHTING.md`.

### Don’t
Nuke readability with heavy bloom/DOF; different random looks per scene with no shared recipe.

### Done when
Side-by-side: customize closeup vs hair target; gameplay face vs plate warmth. **STOP for Adnan.**

---

## PHASE 3 — SceneryPostcard_ShipQuality (env never undercuts hero)

**Goal:** Court + locker backdrop hold up next to V5 hero.

### Do
1. **Court (in-game):** replace blob/unfinished trees/foliage; beach house / postcard landmark to hero quality bar; ground/skirt/sky/water — no missing mats, seams, z-fight; LODs; mobile-honest.
2. **Play corridor clean** — no prop spam in ball path (art-bible).
3. **Locker / customize backdrop:** finish or stylize so bokeh shelves don’t fight the hero; kill pink/magenta/unlit gray; materials match soft toy world.
4. **Wide cams** (react, ultimate orbit, aerial if any): same quality as gameplay — no “only looks good from one angle.”
5. Cheap hero props only where cams look (fence, umbrella, path) — big shapes.

### Don’t
Open-world island; photoreal megascans; block on cosmetics catalog.

### Done when
Same angles that looked cheap before are postcard-finished; hero still pops. **STOP for Adnan.**

---

## PHASE 4 — PresentationPolish_InGameRead (juice supports beauty)

**Goal:** In-game presentation doesn’t make characters look worse than customize.

### Do
1. Shadow / contact quality on court under feet (no float).
2. Outline or contrast only if already in stack — don’t invent noisy post.
3. Hit VFX: don’t overexpose / white-out the hero face on Perfect.
4. Rival + player same material/light rules (no “AI looks cheaper”).
5. UI frames (customize yellow border etc.) don’t crush exposure on the model.

### Don’t
Rebuild cameras against 2C; fake hits; UI spam instead of mesh quality.

### Done when
Rally capture: hero stays readable and pretty mid-action. **STOP for Adnan.**

---

## PHASE 5 — IntegrationLookGate_FinishedProduct

### Do
1. Run full proof pack (customize + 5 in-game angles + dive + serve arms-up).
2. Checklist:
   - [ ] Hair: zero see-through any cam
   - [ ] Legs: plate stocky; knees hold volume; no shorts punch-through on dive
   - [ ] Visor over hair; armpits OK on serve
   - [ ] Customize light ≈ hair target beauty
   - [ ] Gameplay light warm/readable; faces not gray
   - [ ] Trees/house/locker backdrop ship-quality beside hero
   - [ ] 60fps feel; no new hitch from lights/VFX
3. One hotfix loop max if any category fails; then stop.

### Done when
Adnan has proof pack + `GATE_RESULTS.md`. Visual track = finished-product candidate pending his OK.

---

## Operating rules
1. Targets in `v5_target/` are law — match silhouette/materials, don’t “interpret.”
2. Partial credit = FAIL. Reply `GATE: ALL PASS` or `GATE: FAIL` + failing lines.
3. Log every path changed; keep V4 prefab backup until Adnan signs V5.
4. Motion Score80 (fluidity) can resume **after** Phase 1 mesh gates pass (anim on broken legs wastes work).
5. Never “improve looks” with more transparent cards or heavier bloom alone.

---

**Start with PHASE 1 only.** When Phase 1 gates pass, stop for Adnan before lighting/scenery.
