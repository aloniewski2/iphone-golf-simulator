# MICRO GOAL 1i — Male face reform to face_lock (chin first, then ears + face shape)

Prereq: start from **Micro1h** lock (verified SHA)  
`ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`  
SHA-256 `349d790e28147aedaf7aa6aa1b0879dc2856bf0d6ba7bb054552d1177bccfc12`

Read first (in order):
1. This file
2. `ArtDir/hero/base_lock/face_lock/FACE_SPEC.md` — every line binary
3. `ArtDir/hero/base_lock/face_lock/PROCESS_FaceMatch.md`
4. `ArtDir/hero/base_lock/proof/MALE_HEAD_RESULTS.md` (Micro1h)
5. `AGENTS.md`, `art-bible.md`
6. Authority plates in `ArtDir/hero/base_lock/face_lock/` + JPG plates

**REJECT:** `preview/face_example/`, full-head remesh, Path A densify redo, sticker volume.

## Intent (Adnan)

This is still an **overall face reform** to match the **exact** `face_lock` reference — not a chin-only patch. Order of attack: **chin → ears → face shape (cheeks/malar/smile)** until Unity front/3q/profile read as the plate man at arm’s length.

## Adnan decisions

1. Micro1e chin lock **REOPENED**. 3/4 plates `04`/`05` override lower-face width; front+profile must PASS.
2. Ears must not look glued — fair attach into skull; keep helix/concha.
3. TRI_BUDGET ~60k **APPROVED** — do not undensify face zone.
4. Keep Micro1h densify + loops. No hair mesh. Face_M paint-only.
5. Customize: keep `LOOP_*` / ear_attach; future hair = separate mesh; skin = material tint.

## Scope

Reform `Body_M` head forms until **OVERALL_LIKENESS PASS**:
1. Chin/jaw/lower-face width (start here)
2. Ear attach + continuity
3. Face shape: malar/cheek lift, zygomatic width, smile corners / lip volume as needed for plate friendliness
4. Soft midface planes — soft-plastic, not artificial flat mask

Do **not** redo densify. Sculpt/fair existing dense mesh. Optional cranial-only decimate if free.

## Do not regress

Hands, body below neck, sil ≤5.0% bald front/back, face-zone ~2–4 mm, mandatory loops, no sticker volume.

## Gates

| Line | PASS when |
|------|-----------|
| **OVERALL_LIKENESS** | Unity front + 3/4 + profile clearly read as the **face_lock** male at arm’s length — not “better Micro1h,” not face_example |
| CHIN_WIDTH_3Q | Lower face matches 04/05 (±~3 mm chin rows); front/profile PASS |
| EAR_ATTACH + EAR_ANATOMY | No glued seam; helix/concha readable |
| CHEEK_ATHLETIC / CHEEKBONE_3Q / CHEEK_WIDTH | Plate face shape — malar lift present |
| MOUTH_SMILE / LIP_VOLUME | Plate smile/lip read (mesh), not thin sticker line |
| FAIL_WRONG_LIKENESS | Must be clear (same as OVERALL) |
| + remaining FACE_SPEC | Every line PASS |
| NO_STICKER_VOLUME / TOPO_DENSITY / MANDATORY_LOOPS | Held |
| HANDS / BODY_BELOW_NECK / SIL_% / CUSTOMIZE_SOCKETS | PASS |

## Deliver

Updated blend + FBX → Unity; refresh `02_match_m_head.png`, `03_unity_m.png`; `MALE_HEAD_RESULTS.md` + `male-micro1i-checks.json`.

## Reply

`GATE: MALE_HEAD PASS` or `GATE: MALE_HEAD FAIL` + failed lines only.
