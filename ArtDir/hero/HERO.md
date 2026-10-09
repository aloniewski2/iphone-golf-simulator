# Hero — Base Male / Female (LOCKED)

**Status:** Authority plates locked. Build the **character** per `PLAN_HeroBase_MaleFemale_Lock.md`. Clothes / Mixamo / animator deferred.

## Authority plates
- `ArtDir/hero/base_lock/male_body_plain.jpg`
- `ArtDir/hero/base_lock/female_body_plain.jpg`

## What ships now
| Asset | Role |
|-------|------|
| `HeroBase_Male` | Prefab + Body_M + Hair_M |
| `HeroBase_Female` | Prefab + Body_F + Hair_F |

## Out of scope now
Outfits, bandana, Mixamo, existing-animator bind, racket mesh.

## Gate
`ArtDir/hero/base_lock/proof/GATE_RESULTS.md` → `GATE: BASE PASS` or `FAIL`.

## Bald reference update — 2026-09-30

Adnan supplied replacement bald male and female references. The current base_lock male_body_plain.jpg and female_body_plain.jpg are those unchanged files; earlier hair-on plates are archived under base_lock/archive/with_hair_20260930/. This explicit update supersedes earlier hair-on identity statements. Match the complete bald head, face and grey body. Main HeroBase_Male/Female prefabs are bald; hair stays a separate optional asset and must never supply missing scalp, ears, face or neck. Optional hairstyle previews are separate from the base comparison. No clothes, rig, Mixamo or Animator.
