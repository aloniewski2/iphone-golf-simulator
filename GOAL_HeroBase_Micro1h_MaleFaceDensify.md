# MICRO GOAL 1h — Male FACE densify + plate match (Body_M head topology)

Prereq: start from **Micro1g** blend (verified SHA)  
`ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`  
SHA-256 `c6b66edefc902c38c8696d7d5b25feb846f58c60c3edb21cd7a465d3c8dfd0d4`  
(same bytes as `work/male-micro1g/final/HeroBase_Male_Silhouette.blend`)

Read first (in order):
1. This file
2. `ArtDir/hero/base_lock/face_lock/PROCESS_FaceMatch.md` **(fixed pipeline — obey)**
3. `ArtDir/hero/base_lock/face_lock/FACE_SPEC.md` **(binary gates — every PASS/FAIL line)**
4. `ArtDir/hero/base_lock/face_lock/PASTE_PROMPT_MaleFace_Micro1h.txt`
5. `AGENTS.md`, `art-bible.md`
6. Contact sheets + crops in `ArtDir/hero/base_lock/face_lock/`
7. Source plates (authority JPGs only):
   - `ArtDir/hero/base_lock/male_head_detail.jpg`
   - `ArtDir/hero/base_lock/male_body_bald.jpg`
   - `ArtDir/hero/base_lock/male_multiangle_body.jpg`

**REJECT:** `ArtDir/hero/base_lock/preview/face_example/` — wrong likeness. FAIL contrast only via `face_lock/SHEET_plate_vs_bad_example.png`.

## Why Micro1g FAILED (do not repeat)

Micro1g placed landmarks correctly via **displacement fields on ~9.5 mm head spacing** + a thin **`Face_M` sticker layer**. That got brow/eye/nose/mouth *positions* on the plate overlay, but Unity still read as a blockout head:

| Failed line | Root cause |
|-------------|------------|
| OVERALL_LIKENESS | Features look applied, not formed |
| NOSE_WIDTH_FRONT / NOSE_ALAE | Ridge displacement ≠ nose **block** (bulb + alar lobes + nostrils) |
| EAR_ANATOMY | Tube rims on a lump ≠ helix shell + concha bowl |
| CHEEK_WIDTH | Midface planes need denser verts to narrow without melting chin |
| SCALP_SURFACE | Low-freq ripples from coarse verts under displacement |

**Research fix (mandatory):** stop using displacement + `Face_M` stickers as the **volume engine**. Either:
- **A)** Local densify `Body_M` head only to **~2–4 mm** average edge length (neck weld ring down), then sculpt primary forms into Body_M; **or**
- **B)** Welded **nose block** + **ear shells** (same pattern as Micro1e/1f hands: local geo welded clean), then fair into head.

Thin feature materials (iris/sclera/brow paint, lid line) OK *after* solid forms exist. Do **not** invent volume with floating Face_M shells.

## Scope (ONLY)

**HEAD TOPOLOGY + FACE FORMS** on `Body_M` bald head (and welded face parts if path B):
1. Densify head region **or** weld nose block + ear shells (document which path).
2. Mandatory **edge loops** (or equivalent loop-cut rings) for: **alae/nostril**, **lids**, **mouth**, **jaw→ear**, **helix+concha**.
3. Sculpt/edit until every `FACE_SPEC.md` line PASS vs `face_lock/` plates.
4. Keep/extend warm soft-plastic skin. No hair mesh.

## Do not touch / do not regress

- Hands (Micro1f PASS) — bit-identical preferred
- Body **below neck** (shoulders, waist, arms, torso, feet) — 0 verts moved preferred
- Silhouette ≤ **5.0%** bald front/back (report numbers) — do not invent tighter gate
- Chin plane from Micro1e — keep; prove no melt (`UNDER_CHIN` / `CHIN_LOCK`)
- No clothes, no female, no Mixamo/Animator, no **full-body** remesh
- Grey crew shirt on plates is **not** mesh scope
- Do **not** treat `Face_M` displacement stickers as the primary volume solution

Authority = `ArtDir/hero/base_lock/face_lock/` + the three JPG plates above.

## Gates (binary — see FACE_SPEC.md + PROCESS)

Minimum reply lines (expand with every FACE_SPEC region that fails):

| Line | PASS when |
|------|-----------|
| TOPO_DENSITY | Head avg edge ~2–4 mm **or** welded nose+ears with loop support; no 9.5 mm-only midface |
| MANDATORY_LOOPS | alae/nostril, lids, mouth, jaw→ear, helix+concha present and usable |
| NO_STICKER_VOLUME | Primary face volume is Body_M (or welded solids), not Face_M displacement stickers |
| OVERALL_LIKENESS | Matches plate male at arm’s length (front/3q/profile) — not face_example |
| NOSE_WIDTH_FRONT / NOSE_ALAE | Proper nose block; alae + nostril openings in 3/4 |
| EAR_ANATOMY | Readable helix + concha (not bean + wire) |
| CHEEK_WIDTH / SCALP_SURFACE | Plate zygomatic width; clean midface (no displacement ripples) |
| + all other FACE_SPEC lines | Every section 1–14 binary PASS |
| HANDS / BODY_BELOW_NECK | Unchanged PASS |
| SIL_% | bald_front + bald_back each ≤ **5.0%** |
| CHIN_LOCK | Micro1e under-chin plane held |

## Deliver

- Updated blend (ArtDir) + FBX → Unity `HeroBase_Male`
- `ArtDir/hero/base_lock/proof/02_match_m_head.png` — multi-angle overlays (plate vs mesh heads)
- `ArtDir/hero/base_lock/proof/03_unity_m.png` — refreshed Unity **head close-ups** (+ body context ok)
- `ArtDir/hero/base_lock/proof/MALE_HEAD_RESULTS.md` — overwrite with Micro1h result + every GATE line
- Optional: `proof/male-micro1h-checks.json` (include head edge-length stats)

## Reply format

```
GATE: MALE_HEAD PASS
```
or
```
GATE: MALE_HEAD FAIL
<failed lines only>
```
