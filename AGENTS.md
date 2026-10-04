# AGENTS.md — Party Tennis (iPhone)

You are a coding/art agent working in this repo. Read this file first on every task, then the linked authority docs. Prefer binary gates over vibes.

## Product
Friends-first **iPhone party tennis** (Wii / Switch Sports / Fall Guys energy). Soft plastic beauty, readable at phone distance. Unity **URP**.

**Repo path:** `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`  
(Name says golf; **tennis is active**.)

## Current character authority (LOCKED)
**Hero Base Male + Female** — grey mannequin body + head/hair only.

| Plate | Path |
|-------|------|
| Male | `ArtDir/hero/base_lock/male_body_plain.jpg` |
| Female | `ArtDir/hero/base_lock/female_body_plain.jpg` |
| Build brief | `PLAN_HeroBase_MaleFemale_Lock.md` |
| Art rules | `art-bible.md` |
| Hero notes | `ArtDir/hero/HERO.md` |

**Clothes / bandana / fashion are OUT OF SCOPE** until Adnan says otherwise.

Older V4/V5/V6 look-ship plates and plans are **archive / secondary**. Do not override `base_lock`.

## How to work
1. Focus on **building the character meshes** that match the plates. Rig/anim/animator wiring is optional later unless Adnan asks.
2. **Binary gates.** `GATE: … PASS` or `GATE: … FAIL` + failed lines. “Looks better” is not done.
3. Proof with screenshots/overlays in the plan’s `proof/` folder. No proof = FAIL.
4. Stop for Adnan when identity/silhouette drifts — not for filler questions.

## Hard technical locks
- Height ≈ **1.65–1.75 m**. Blender units = meters; FBX scale 1 → Unity.
- Hair = **separate mesh**, URP Lit **Opaque** solid volumes. Forbidden: transparent/dither/particle hair as volume.
- Tris ≈ **35–50k** per base unless Adnan approves more.
- Face textures: no crunch compression that kills quality.
- Never remesh/smooth away plate forms. Overlay silhouette vs plate ≤ ~5%.
- Empty customize sockets OK to add later: HairRoot, Headwear, Chest_Top, Hip_Bottom, Foot_L/R_Shoe, Hand_Racket — not blocking character ship.

## Anti-degrade (Blender → Unity)
Match the plates. If Unity looks softer/flatter/yellower than the plate match → FAIL and fix. No Mixamo requirement.

## Do not
- Full Tripo / regen that drifts from `base_lock` without Adnan OK
- Bake a default outfit into the only body mesh
- Declare done without `GATE_RESULTS.md` + proof images
- Scope-creep into clothes, Mixamo, animator rewiring, shop, sport 2 unless asked

## Priority order
1. Plate fidelity (`base_lock`)
2. Mobile readability + soft-plastic look in Unity
3. Clean export (Body + Hair separate)
4. Fancy materials / rig / anim extras

## Active handoff — 2026-10-01 (Sonnet)

Male HeroBase: body form mostly PASS; **CHIN + FINGERS still FAIL**.  
Start: `HANDOFF_Sonnet_MaleHero_ChinFingers.md` → `PROCESS_Blender_Claude.md` → `GOAL_HeroBase_Micro1e_ChinFingers.md`.  
Authority blend: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`. Silhouette gate ≤**5%** bald front/back (not 2%).

## Bald reference update — 2026-09-30

Adnan supplied replacement bald male and female references. The current base_lock male_body_plain.jpg and female_body_plain.jpg are those unchanged files; earlier hair-on plates are archived under base_lock/archive/with_hair_20260930/. This explicit update supersedes earlier hair-on identity statements. Match the complete bald head, face and grey body. Main HeroBase_Male/Female prefabs are bald; hair stays a separate optional asset and must never supply missing scalp, ears, face or neck. Optional hairstyle previews are separate from the base comparison. No clothes, rig, Mixamo or Animator.
