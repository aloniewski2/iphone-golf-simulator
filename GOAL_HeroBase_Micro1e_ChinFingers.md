# MICRO GOAL 1e — Male chin + fingers only

Prereq: start from  
`ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`  
(SHA `d39c6c5e…` Hand_4 lock). **Do not** start from `work/male-jaw-5/outline/candidate.blend` unless you prove its chin is closer to plates (currently softer — likely worse).

Read first: `HANDOFF_Sonnet_MaleHero_ChinFingers.md`, then plates:
- `ArtDir/hero/base_lock/male_body_bald.jpg`
- `ArtDir/hero/base_lock/male_multiangle_body.jpg`
- `ArtDir/hero/base_lock/male_head_detail.jpg`
- `ArtDir/hero/base_lock/proof/03_unity_m.png`
- `ArtDir/hero/base_lock/proof/01h_hand_form_m.png`

## Scope (ONLY)

1. **CHIN** — proper chin ball + under-chin/neck join matching bald/head-detail profiles. No beard slab. No soft recessed dough chin. Upper face may stay blank.
2. **FINGERS** — separate readable soft-plastic fingers + thumb (front + 3q + side). Not mitt / paddle / claw spikes. Match plate hand mass.

## Do not touch / do not regress

Shoulders, waist, arm side depth, feet side (already PASS). No remesh. No female. No clothes. No Mixamo. No ≤2% silhouette invention.

## Gates

- Eye: `CHIN` PASS + `FINGERS` PASS (Adnan will verify).
- Silhouette: bald_front + bald_back ≤ **5.0%** (report numbers; fail only if >5% or you regress body).
- Height ≈ 1.70 m; metres; A-pose.

## Deliver

- Updated ArtDir blend + FBX → Unity `HeroBase_Male`
- `ArtDir/hero/base_lock/proof/01e_chin_fingers_m.png`
- `ArtDir/hero/base_lock/proof/03_unity_m.png`
- `ArtDir/hero/base_lock/proof/MALE_FORM_RESULTS.md`

Reply:
```
GATE: MALE_FORM PASS
```
or
```
GATE: MALE_FORM FAIL
CHIN / FINGERS / SIL_% / SHOULDERS / WAIST / ARMS_SIDE / FEET_SIDE
```
(list only failed lines)
