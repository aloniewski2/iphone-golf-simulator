# CREATE: Hero POLISH V6 — Zero Deficiency Rebuild

Paste into **Codex / Claude Opus / Astra**. One phase. Stop only at GATE.

**Goal:** Ship **Hero POLISH V6** that is strictly better than current V4/V5 shipping hero with **zero remaining visual/tech deficiencies** vs plate + targets. Not a shader tweak. Not “looks better.” Binary gates only.

**Project:** `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`  
Read first: `AGENTS.md`, `art-bible.md`, `ArtDir/hero/HERO.md`, `PLAN_HeroLookShip_FinishedProduct.md` (Customize Contract), `PLAN_HeroV5_OpaqueHair_ProperLegs.md`.

**Authority (non-negotiable):**
- `ArtDir/hero/v5_target/HERO_V5_TARGET_turnaround.png`
- `ArtDir/hero/v5_target/HERO_V5_TARGET_hair_closeup.png`
- `ArtDir/hero/v5_target/HERO_V5_TARGET_legs.png`
- `ArtDir/plates/01_hero.png`

**Product:** Friends-first iPhone party tennis — Switch Sports / Wii clarity + soft plastic beauty. Cosmetics = identity. Unity URP mobile ~60fps.

---

## Why V6 (not another soft polish)

Previous passes soft-failed the last 10%: transparent/gap hair, stick legs, dive shorts punch-through, weak ankles, customize sockets fragile, materials that look cheap next to the plate. **V6 only passes if every known deficiency is closed** and proof shows it.

Name output: **Hero POLISH V6**. Archive V4/V5 meshes as reference; do not ship them.

---

## Keep vs rebuild

| KEEP | REBUILD / FIX TO ZERO DEFECT |
|------|------------------------------|
| Face identity language (blond, visor, soft smile) | Hair = solid opaque lock volumes |
| Mixamo humanoid scale ~1.7m + bone names where possible | Legs pelvis→shoes (stocky toy-athlete) |
| Existing animator / Hero_* clip set wiring | Shorts geometry + dive-safe weights |
| Customize slot architecture (names) | Ankles/shoes silhouette; knee deformation loops |
| Soft plastic party-chibi style | Materials: skin/cloth/hair Opaque/plastic tool |
| Racket as tool socket | Armpit clearance on serve; no webbing |
| | Visor sits clean over hair |
| | Clean UVs + LOD0–N that don’t reintroduce gaps |
| | Customize Contract sockets documented |

**Forbidden:** full Tripo body regen that changes face without Adnan OK; transparent/dither hair as volume; baking default outfit into body-only mesh; declaring done without GATE pack.

---

## Known deficiencies that MUST be gone (kill list)

Copy into `ArtDir/hero/v6_target/KILL_LIST.md` and check off only with proof:

1. Background visible through hair (customize or gameplay)
2. Horizontal scalp slice / floating hair volume / missing backfaces
3. Hair Transparent queue / dither / density holes on solid locks
4. Stick-taper calves / pipe ankles / Tripo thin lower legs
5. Knee candy-wrapper collapse on ready / run / serve bend
6. Shorts hem wrong length; orange piping broken or diving through thighs
7. Visor clipping through hair locks
8. Armpit webbing / arm-through-polo on serve
9. Feet not grounded; spike ankles; L/R asymmetry
10. LOD that turns hair into see-through cards
11. Customize hair-off → skull hole / interior
12. Broken or renamed Hair/Visor/Top/Bottom/Shoes/Racket sockets
13. Flat/cheap materials vs plate soft-plastic beauty (albedo/roughness)
14. Any mesh that blocks future clothes/skins (baked-only outfit)

If a defect isn’t on this list but appears in proof → add it and FAIL until fixed.

---

## Execution order (do not skip)

### V6-0 — Freeze + inventory (≤1 hour)
1. Copy authority plates into `ArtDir/hero/v6_target/`.
2. Capture CURRENT shipping hero into `ArtDir/hero/v6_target/BEFORE/` (customize close-up, turnaround, knees/feet, dive, serve).
3. Write this kill list + gate checklist into `ACCEPTANCE.md`.
4. Duplicate workset: `Hero_V6_*` meshes/prefab — never overwrite V4/V5 until Adnan promotes.

### V6-1 — Silhouette + proportions (block first)
1. Match turnaround + plate: head, torso, arm length, thigh/calf thickness, shoe mass.
2. Side silhouette must read stocky toy-athlete, not stick legs.
3. Stop for Adnan if face identity drifts > ~10%.

### V6-2 — Hair (opaque volumes only)
1. Delete transparent/card hair; model solid locks matching hair closeup + plate.
2. URP Lit **Opaque** on solid locks (alpha-clip fringe ≤5% tris at tips only).
3. Scalp cover mesh for hair-off.
4. Visor clearance tested in ready + look-up.

### V6-3 — Legs + shorts + shoes
1. Rebuild thighs/knees/calves/ankles/shoes with deformation loops.
2. Shorts: correct hem, continuous piping, dive test (thigh must not punch through).
3. Re-bind Mixamo-compatible; normalize weights; squat/run/ready/serve in Blender or Unity.

### V6-4 — Upper body clearance + sockets
1. Polo armpit clearance on serve; no arm-through-body.
2. Confirm Customize Contract slots: Hair, Visor, Top, Bottom, Shoes, Racket.
3. Document socket paths in `ArtDir/hero/v6_proof/CUSTOMIZE_CONTRACT.md`.

### V6-5 — UVs, materials, LODs
1. Clean non-overlapping UVs per part; mobile-friendly atlases (no useless 4K).
2. Soft plastic: skin / cloth / Opaque hair / plastic tool recipes matching art-bible.
3. LOD0–N: hair never becomes transparent cards; shorts never disappear.

### V6-6 — Unity wire + regression
1. Import V6; keep avatar definition / animator compatible.
2. Cycle: hair on/off, top A→B if exists, racket attach, customize cam + gameplay.
3. Play: ready, run 2 steps, serve, dive, ultimate closeup.

### V6-7 — Proof pack (required)
Export `ArtDir/hero/v6_proof/`:
- Turnaround (same 4 angles as target)
- Customize face + hair close-up (same framing as old gap bug)
- Hair-off scalp
- Knees-and-feet panel
- Ready / serve / dive frames
- Inspector screenshot: hair Opaque
- `GATE_RESULTS.md` line-by-line PASS/FAIL
- Before/after collage vs BEFORE/

---

## GATE CHECKLIST — all PASS or V6 FAILS

### Hair
- [ ] Zero background through hair (customize + gameplay + ultimate)
- [ ] Solid locks Opaque (Inspector proof)
- [ ] No scalp slice / floating volume
- [ ] Visor clean over hair
- [ ] Hair-off → scalp/cap, no interior hole
- [ ] LODs never go transparent-card

### Legs / shorts / shoes
- [ ] Calves match plate thickness (not stick)
- [ ] Knees keep volume on bend
- [ ] Shorts hem + piping OK; dive no thigh punch-through
- [ ] Ankles/shoes grounded; no pipes; L/R OK
- [ ] Run 2 steps: no knee explosion

### Upper / identity / tech
- [ ] Same hero vs `01_hero.png` (blond + visor + polo language)
- [ ] Serve: no armpit webbing / arm through polo
- [ ] Mixamo humanoid + existing animator plays
- [ ] All Customize Contract sockets valid
- [ ] Materials read soft plastic, not flat/cheap
- [ ] Tris ~≤50k (prefer ~45k) unless Adnan approves
- [ ] Proof pack complete; kill list all closed

**Reply exactly:** `GATE: V6 PASS` or `GATE: V6 FAIL` + failed lines. Partial credit = FAIL. Do not promote over V4/V5 until Adnan OK.

---

## Don’t
- Soft “improved” language without gates
- Keep V4/V5 transparent hair “for style”
- Full body Tripo regen without Adnan OK
- Bake one outfit into the only body mesh
- Skip dive / hair-off / LOD tests
- Rename sockets or split customize-only vs gameplay avatar

## Start now
1. V6-0 freeze + BEFORE pack  
2. Then V6-1 → V6-2 (silhouette + hair)  
3. Stop for Adnan after hair+silhouette proof before finishing legs if identity drifts  

When done, paste `GATE_RESULTS.md` summary here with proof paths.
