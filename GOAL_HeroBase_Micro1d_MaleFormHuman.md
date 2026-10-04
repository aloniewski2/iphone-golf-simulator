# MICRO GOAL 1d — Male human form (arms / chin / feet + prior defects)

Prereq: continue `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` (or current male Body_M). No remesh restart. No female.

Read: male_body_bald.jpg, male_multiangle_body.jpg (left/right panels), male_head_detail.jpg, proof/03_unity_m.png, proof/01c_form_m.png.

## Hard visual defects to FIX (binary — Adnan will eye-check)

1. **Arms flat from side** — upper/forearm must have round soft-plastic depth in LEFT/RIGHT orthographic profile. Not a thin card / pancake silhouette. Elliptical cross-section; biceps/forearm volume readable from side.
2. **Chin + fake beard** — remove any built-in beard / stubble shelf / chin strap volume. Clean bald-plate jaw: proper chin ball, under-chin/neck join, no slab of “beard mesh.” Match male_head_detail profiles.
3. **Fingers** — separate readable soft-plastic fingers (not mitten stubs / fused paddle). Thumb opposition readable from front and 3q; match plate hand mass. No spaghetti noodles.
4. **Feet broken in side view** — fix sole, heel, ankle, toe box in LEFT/RIGHT profile so foot reads as a soft boot/foot volume standing on the ground — not a warped slab or wrong pitch. Match bald + multiangle side panels.
5. **Still fix if still present:** love-handle waist bulge; collapsed/caved shoulders (full rounded deltoid, no pit at arm join). Waist narrower than chest; athletic soft-toy taper.

## Gates
- Visual: arms depth, chin clean, fingers OK, feet side OK, shoulders/waist OK — each PASS/FAIL in report.
- Silhouette: bald_front + bald_back ≤ **5.0%** only (do NOT invent 2%). Sheet sides are diagnostic for arm/foot depth, not a pixel fail unless Adnan says.
- Metres, A-pose ~1.70m. Plain grey body OK; head can stay untextured this pass but **chin shape** must be correct.

## Deliver
- Updated blend + FBX → Unity HeroBase_Male
- proof/01d_form_m.png (front / back / left / right / 3q)
- proof/03_unity_m.png
- proof/MALE_FORM_RESULTS.md

Reply:
GATE: MALE_FORM PASS
or FAIL with failed lines: ARMS_SIDE / CHIN / FINGERS / FEET_SIDE / SHOULDERS / WAIST / SIL_%
