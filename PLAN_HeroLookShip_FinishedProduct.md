# MASTER PLAN: HeroLookShip_FinishedProduct (characters + light + scenery)

Paste into Codex / Claude as the **controlling brief**. Execute **one PHASE at a time**. Stop after each phase for Adnan approval + proof shots.

**Goal:** get **as close to a finished visual product as possible** — customize locker room AND in-game — by killing everything that currently makes characters look worse than `ArtDir/hero/v5_target/` and plate `01_hero.png`.

**New this revision:** Phase 0 is a **full inventory + starting-point audit** before any mesh work. Customizability is first-class: new clothes, skins, hair, and tools later must not break sockets, LODs, lighting, or animator wiring.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`
Read: `AGENTS.md`, `art-bible.md`, `ArtDir/hero/HERO.md`, `ArtDir/plates/01_hero.png`, `PLAN_HeroV5_OpaqueHair_ProperLegs.md`, any existing customize / wardrobe scripts.

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

**Anti-goal:** “shader tweak” partial fixes; transparent hair; flat locker lighting; blob trees next to detailed heroes; baking one outfit into the body; declaring done without GATE proof; breaking future skins/tools.

---

## Locks
- Face identity language (blond, visor, polo colors) stays unless Adnan OK.
- Mixamo humanoid + existing animator wiring where possible.
- Plan 2C camera playability soft law stays (live ball = play cam).
- No online / sport 2 / shop economy / full cosmetics catalog in this plan — but **architecture must leave slots open**.
- Body proportions / bind stay V4/V5 locked; only cosmetics swap.

---

## CUSTOMIZE CONTRACT (applies to every phase)

Future clothes, skins, hair, and tools must plug in without rewiring the hero. Treat this as law from Phase 0 onward.

### Slot map (canonical)
| Slot | Examples later | Rules |
|------|----------------|-------|
| Hair | styles / colors | Opaque mesh; scalp cover when empty/bald; never transparency as “volume” |
| Visor / Headwear | caps, bands | Sits **over** hair; no clip through locks |
| Face / Skin | skin tones, face paints | Materials only on body; no baked outfit colors on skin |
| Top | polos, jackets | Own mesh or skinned piece; armpit clearance on serve |
| Bottom | shorts, pants | Own mesh; dive must not punch thigh through cloth |
| Shoes | sneakers | Ankle-safe; no pipe calves |
| Hands / Gloves | optional later | Don’t break racket grip |
| Racket / Tool | rackets, later sports tools | Socket on grip bone; one attach point; scale locked |
| FX / Trail (optional) | cosmetic trails | Separate; never white-out face |

### Hard rules
1. **Never bake default outfit into body mesh** as the only path — default outfit is still slot content (or clearly layered).
2. **One prefab / avatar rig** for customize + gameplay; same sockets, same LODs, same materials stack.
3. **Swap = enable/disable or instantiate into named sockets** — no unique per-outfit humanoid rewires.
4. **Materials:** shared URP Lit recipes (skin / cloth / hair Opaque / plastic tool); new skins swap textures or material variants, not shader families.
5. **LODs:** every new piece ships LOD0–N that don’t reintroduce hair gaps or missing shorts.
6. **Animator / bones:** cosmetics don’t add required bones; tools parent to existing sockets only.
7. **Regression test on every mesh change:** cycle Hair on/off, Top A→B, Bottom default, Racket attach, then play serve + dive + customize camera.
8. Document the slot map + socket Transform paths in `ArtDir/hero/v5_proof/CUSTOMIZE_CONTRACT.md`.

### Don’t
Glue hair into skull permanently; rename sockets mid-plan; different customize-only vs in-game avatar; transparent cards for “soft hair”; one-off outfit meshes that skip LODs.

---

## Global GATE (final product bar)

After all phases, Adnan captures:
1. Customize UI close-up (hair + face)
2. Customize full-body turn + **at least one slot swap** (Hair off/on or Top A→B)
3. In-game ready (back playing cam)
4. Serve / ultimate closeup
5. React cam / wide env
6. Dive frame (shorts)

Independent check: hero within ~85–90% of v5_target sheets; env not embarrassing next to characters; light matches art-bible; customize contract documented. Agent must end with `GATE: ALL PASS` or `GATE: FAIL` + lines.

---

# PHASE ORDER

---

## PHASE 0 — FullLookInventory_StartingPoint (do this first)

**Goal:** Before changing anything, give Adnan and the agent a **complete, honest look at everything** that affects character beauty — so we have a shared starting point, known debt, and a ranked kill list. No mesh rebuild yet.

### Do
1. **Capture baseline proof pack** into `ArtDir/hero/v5_baseline/` (create folder):
   - Customize: face close-up, full-body front/side/back, hair detail, each equipped slot visible
   - In-game: ready stance back cam, serve arms-up, dive/land, ultimate or react if available, wide env
   - Lighting: screenshot of Light objects / URP Volume in customize scene and court scene
2. **Inventory document** `ArtDir/hero/v5_baseline/LOOK_INVENTORY.md` listing, with paths:
   - Hero prefab(s), body mesh, hair mesh, legs/shorts, visor, materials + shaders (especially hair)
   - Customize UI scene + avatar binder scripts; every cosmetic slot that exists today (even if empty)
   - Socket / attach Transform names (Hair, Visor, Top, Bottom, Shoes, Racket, …)
   - Animator / Avatar / Mixamo bind notes; known clip issues that show mesh (serve, dive)
   - Locker room lights, court lights, volumes, post
   - Scenery pieces that sit next to the hero (trees, house, backdrop shelves)
   - Rival / second character path (must share contract)
3. **Defect ledger** — binary rows, not soft notes:
   - [ ] Hair see-through / scalp holes
   - [ ] Legs silhouette vs target
   - [ ] Knee collapse
   - [ ] Shorts dive clip
   - [ ] Visor through hair
   - [ ] Armpit webbing on serve
   - [ ] Customize light flat/gray
   - [ ] Gameplay light worse than customize
   - [ ] Env undercuts hero
   - [ ] Customize vs gameplay avatar drift (different prefabs?)
   - [ ] Sockets missing / unsafe for future skins
4. **Ranked kill order** (recommended default: hair opacity → legs/shorts → sockets/contract → lights → scenery → presentation). Adjust only with evidence from the inventory.
5. **Customize readiness score:** PASS / FAIL for “can add a new Top + Hair next month without touching body rig?” — if FAIL, list exact blockers.
6. **STOP for Adnan** with inventory + baseline shots. Do **not** start Phase 1 until he affirms the kill order.

### Don’t
Silently start rebuilding hair; “quick polish” during inventory; skip rival/second character; invent slots that don’t exist without noting them as **proposed**.

### Done when
`LOOK_INVENTORY.md` + baseline folder exist, defect ledger filled, customize readiness scored, Adnan has reviewed. Reply `GATE: PHASE0 PASS` or `GATE: PHASE0 FAIL`.

---

## PHASE 1 — HeroV5_MeshRebuild (hair + legs + clip killers)

**Goal:** Structural character beauty. Nothing else compensates if this fails. **Preserve / harden Customize Contract.**

### Do
1. Archive V4 hair/legs; rebuild per `PLAN_HeroV5_OpaqueHair_ProperLegs.md` gates (opaque solid hair; plate legs; knee loops; visor over hair; no dive thigh-through-shorts; no armpit webbing on serve).
2. Materials: URP Lit **Opaque** hair locks; matte cloth/skin per art-bible; kill dither/transparent hair paths; LODs must not reintroduce gaps.
3. **Customize-safe sockets** (from Phase 0 map): Hair / Visor / Top / Bottom / Shoes / Racket; scalp cover when hair off; default outfit as slot content, not baked-only body.
4. Regression: Hair on/off + Top swap stub (even a second material) + racket socket + serve + dive.
5. Write/update `ArtDir/hero/v5_proof/CUSTOMIZE_CONTRACT.md` (slot paths + how to add a new piece).
6. Proof: `ArtDir/hero/v5_proof/` turnaround + customize closeup + knees/feet + dive frame + Inspector hair Opaque + slot-swap proof.

### Don’t
Shader-only “fix”; full face Tripo regen without OK; skip dive test; bake polo into torso as the only clothing path; break Mixamo bind.

### Done when
`GATE: ALL PASS` on Hero V5 checklist **and** customize contract regression. **STOP for Adnan.**

---

## PHASE 2 — LightingBeauty_CustomizeAndGame (characters look lit, not gray)

**Goal:** Sheet beauty is half lighting. Match flattering light in locker + court. Lights must flatter **any** future skin/cloth colors, not only default blond/polo.

### Do
1. **Art-bible light:** warm key 35–45° elev; fill 30–40% of key; soft shadows; cool shadow tint; bloom low; no grain.
2. **Customize / locker room:** rebuild or retune lights so face + hair read like `HERO_V5_TARGET_hair_closeup` (soft key, rim optional, no flat ambient-only, no harsh specular blobs).
3. **Gameplay court:** same key/fill philosophy; characters slightly more saturated than world; contact shadows readable.
4. **Cinematic moments** (toss / ultimate / react): keep 2C playability; light still flattering on closeups (no sudden underexposed faces).
5. Exposure / URP volume: one consistent look; document values in `ArtDir/hero/v5_proof/LIGHTING.md`.
6. Spot-check at least one alternate cloth/skin tint if available so lights aren’t tuned only to default.

### Don’t
Nuke readability with heavy bloom/DOF; different random looks per scene with no shared recipe; bake exposure that only works for one outfit.

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

**Goal:** In-game presentation doesn’t make characters look worse than customize. Cosmetics still readable mid-action.

### Do
1. Shadow / contact quality on court under feet (no float).
2. Outline or contrast only if already in stack — don’t invent noisy post.
3. Hit VFX: don’t overexpose / white-out the hero face on Perfect.
4. Rival + player same material/light rules (no “AI looks cheaper”).
5. UI frames (customize yellow border etc.) don’t crush exposure on the model.
6. Confirm racket/tool socket still correct under juice cams.

### Don’t
Rebuild cameras against 2C; fake hits; UI spam instead of mesh quality.

### Done when
Rally capture: hero stays readable and pretty mid-action. **STOP for Adnan.**

---

## PHASE 5 — IntegrationLookGate_FinishedProduct

### Do
1. Run full proof pack (customize + slot swap + 5 in-game angles + dive + serve arms-up).
2. Checklist:
   - [ ] Hair: zero see-through any cam
   - [ ] Legs: plate stocky; knees hold volume; no shorts punch-through on dive
   - [ ] Visor over hair; armpits OK on serve
   - [ ] Customize light ≈ hair target beauty
   - [ ] Gameplay light warm/readable; faces not gray
   - [ ] Trees/house/locker backdrop ship-quality beside hero
   - [ ] Customize contract documented; Hair off/on + Top swap regression PASS
   - [ ] Rival shares same slot/material rules
   - [ ] 60fps feel; no new hitch from lights/VFX
3. One hotfix loop max if any category fails; then stop.

### Done when
Adnan has proof pack + `GATE_RESULTS.md` + `CUSTOMIZE_CONTRACT.md`. Visual track = finished-product candidate pending his OK.

---

## Operating rules
1. **Phase 0 first** — no Phase 1 mesh work until Adnan affirms inventory + kill order.
2. Targets in `v5_target/` are law — match silhouette/materials, don’t “interpret.”
3. Partial credit = FAIL. Reply `GATE: ALL PASS` / `GATE: PHASE0 PASS` or `GATE: FAIL` + failing lines.
4. Log every path changed; keep V4 prefab backup until Adnan signs V5.
5. Motion Score80 (fluidity) can resume **after** Phase 1 mesh gates pass (anim on broken legs wastes work).
6. Never “improve looks” with more transparent cards or heavier bloom alone.
7. Every mesh/material change must re-run customize regression (slot swap + serve + dive).

---

**Start with PHASE 0 only.** Deliver inventory + baseline shots, then stop. After Adnan OK, run Phase 1.
