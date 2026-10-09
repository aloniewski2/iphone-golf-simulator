# MICRO GOAL 1c — Male form fix (shoulders + waist)

Prereq: Micro1b MALE_SIL PASS on bald front/back. Continue mesh — do not remesh/restart.

Open: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` (and sync into Unity candidate from `HeroBase_Male.blend` / prefab only AFTER Blender form PASS).

Read: male_body_bald.jpg, proof/01_silhouette_overlay_m.png, proof/03_unity_m.png (known defects).

## Defects to fix (hard)
1. **Broken shoulders** — remove peaked/lumpy deltoid shelves; smooth neck→shoulder→upper-arm cylinder; no pinches or horns on top of shoulder from front or back.
2. **Love handles** — taper waist; remove side bulge above hips; athletic soft-toy torso, not spare-tire silhouette. Keep stocky legs/shoulders language from plate, but waist must read clean.

## Still required
- Bald front/back silhouette error remain ≤ 5.0% vs male_body_bald.jpg (re-measure same metric).
- Metres, A-pose, ~1.70m, locked cameras.
- No female, no hair, no face texture polish, no Mixamo.

## Deliver
- Updated Blender mesh saved.
- New overlays: proof/01_silhouette_overlay_m.png
- New Blender turnaround: proof/01c_form_m.png (front/back/3q)
- Re-export FBX → refresh Unity prefab HeroBase_Male → proof/03_unity_m.png
- proof/MALE_FORM_RESULTS.md with GATE line

Reply: GATE: MALE_FORM PASS or FAIL + bald % + notes on shoulders/waist.
