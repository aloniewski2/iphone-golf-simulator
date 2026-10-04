GATE: MALE_HEAD FAIL
OVERALL_LIKENESS / FAIL_WRONG_LIKENESS

Micro1i · male FACE REFORM · 2026-10-01 · sculpted/faired on the existing dense `Body_M` (Micro1h topology unchanged: same 30,296 vertices and faces, only positions moved), `Face_M` rebuilt as paint only. Start: Micro1h lock `349d790e…` (verified with `shasum -a 256` before any edit). Adnan's eye-check vs `face_lock/` is final; every line below is my binary call against FACE_SPEC.md / GOAL_HeroBase_Micro1i, judged on the **real Unity captures** (`03_unity_m.png`) and plate overlays.

All Blender work ran headless (the live Blender session was not used). Unity batch import/captures ran once per candidate; the installed prefab is the final one.

## Why FAIL
The structure Adnan flagged is fixed — chin/jaw width, ear attach and rounded oval ear tops, narrow nose bridge widening into the wings, defined lips — and every measurable line holds. But at arm's length the Unity front and 3/4 still read as a sterner, older cousin of the plate man, not clearly the same friendly person:
* the plate's friendliness comes from a wider, warmer smile with cheek lift and soft stubble/AO shading; the mesh has the measured smile (corners +5.0 mm) but weaker cheek lift, and the shading is pigment, outside mesh scope;
* Adnan's in-session notes on this build: **eyes good**, **jaw "almost there"**, nose and ear-top notes addressed afterwards (shown in `work/male-micro1i/progress/`), not yet re-reviewed by him.

## Gate lines
| Line | Result | Evidence |
|---|---|---|
| **OVERALL_LIKENESS** | **FAIL** | see above; Unity `face_front/threequarter/profile` vs plates 01/04/02 (`03_unity_m.png`, `02_match_m_head.png`) |
| **FAIL_WRONG_LIKENESS** | **FAIL** | same call as OVERALL |
| CHIN_WIDTH_3Q | PASS (measured) | far-side 3/4 contour at chin rows z 1.489–1.466, both plates 04 (yaw 41°) and 05 (36°): Micro1h rms **7.9 mm** (max 11.0) → Micro1i rms **1.8 mm**, max 2.5 mm (gate ±3 mm). Adnan: jaw "almost there" — treat as open until his eye-check |
| CHIN_FRONT_PROFILE | PASS | profile front-y error rows 216–256: −1.5…+0.3 mm; front half-width at jaw rows +1.6 mm |
| EAR_ATTACH | PASS | ear rebuilt on a clean skull base (thin-plate fit through a collar 21–31 mm outside the outline), 2.6 mm foot fillet + 1.2 mm skirt (none at the top), no web; Unity `ear_side`, `head_threequarter` |
| EAR_ANATOMY | PASS | rolled helix rim on the carved helix loop, scapha, antihelix, concha bowl, tragus, antitragus, lobe; soft rather than crisp |
| EAR_SIZE / EAR_PLACE / EAR_Y_PAIR | PASS (caveat) | outline from the profile plate, mirrored. **Plates disagree on standoff:** front plate has the ear ~19 mm proud (front half-width now −3.9…+0.9 mm vs plate), back + 3/4 plates imply ~8–12 mm less (3/4 rear contour **+7…+15 mm outside** plates 04/05; Micro1h +6.6…+12.4). Micro1i favours the front plate. **Open decision for Adnan** |
| Ear top (Adnan note) | done | oval, rounded rim in side/3-4/front; crest set by absolute x targets vs ellipse angle (no dome, no knob) |
| NOSE_* (HEIGHT, BRIDGE, TIP, ALAE, ROOT, WIDTH_FRONT) | PASS | sagittal profile untouched. **Micro1h foot was inverted** (alar crease half-width 23.0 mm at z 1.560 → ~21 at the base); now 19.4 → 25.7 mm (plate wings ~25–26). Nostril openings kept near the plate (ring x 7.9–23.7 vs 8.6–20.4), nostril paint now follows the carved loops |
| CHEEK_ATHLETIC / CHEEKBONE_3Q / CHEEK_WIDTH | PASS | malar apple +2.2, infraorbital/buccal/temple hollows (interior relief, no silhouette growth); front silhouette within ~1.5 mm at cheek rows |
| MOUTH_SMILE | PASS (weak) | closed smile, seam centre→corners +5.0 mm, corner pockets, symmetric. Reads neutral-pleasant, less friendly than the plate |
| LIP_VOLUME / PHILTRUM | PASS | upper +0.7 / lower +1.1 mm lip volume on top of the Micro1h profile protrusions, seam groove 1.0 mm cut along the carved `LOOP_mouth_seam`; lip paint 4.8 / 10.4 mm tall |
| EYE_*, BROW_*, FOREHEAD, CRANIAL_*, BALD_NO_HAIR, STYLE, SKIN_TONE, DEFAULT_EXPR, JAW_U, NO_JOWLS, NECK/NAPE/PROP_* | PASS | unchanged from Micro1h (eyes: Adnan "look good") |
| NO_STICKER_VOLUME | PASS | Face_M distance from skin: brow ribbons ≤1.5 mm (hair), brow soft rim ≤0.81, lash/crease/lip/nostril 0.12–0.74 mm, **seam ribbon ≤1.07 mm at the lip corners** (mean 0.52); all face forms are Body_M geometry (`male-micro1i-checks.json` → face_m) |
| TOPO_DENSITY | PASS | unchanged topology: face-zone edge mean **3.59 mm** (median 3.41), head mean 4.31 mm |
| MANDATORY_LOOPS | PASS (see notes) | all 17 `LOOP_*` groups kept. mouth ×3, helix ×2, concha ×2, `ala_R`, lids ×2 are unbroken edge chains. **Pre-existing from Micro1h:** `nostril_L/R`, `ala_L`, `ear_attach_L/R` each have 1 jump of 1.6–4.1 mm (closing seam; `ala_L` 1.95 → 3.6 mm after the nose widening). **`jaw_ear_L/R` was stored as fragments (10 / 9 jumps of ~52 mm)** — membership replaced with a continuous edge path along the same jaw curve (30 vertices per side, ≤3.95 mm from the curve); geometry untouched |
| HANDS | PASS | 0 vertices moved |
| BODY_BELOW_NECK | PASS | 0 vertices moved outside the head zone; weld ring (10 verts) and everything below z 1.436 bit-identical |
| SIL_% | PASS | bald_front **4.2978 %**, bald_back **4.5897 %** (≤ 5.0 %); with Face_M identical (Micro1h 4.3121 / 4.6175) |
| CUSTOMIZE_SOCKETS | PASS | `HairRoot` kept in the prefab, no hair mesh, `LOOP_*` groups kept in the blend, skin is a tint (no texture) |
| TRI_BUDGET | PASS | Body_M **60,358** (unchanged; ~60k approved by Adnan) + Face_M 5,416 |

## What changed (`work/male-micro1i/`, edit chain in `chain.py`)
1. **Chin first.** Far-side tangent shave of the chin band to the plate 3/4 contours (plates 04 + 05 override the lower-face width), centre-line profile restored afterwards.
2. **Nose.** x-only warp: bridge narrowed up to 7 mm/side; wings pushed out up to 5.4 mm/side from x ≥ 11 mm (nostrils stay); speckled alar crease faired (`ed7.py`).
3. **Photometric mesh optimisation (`pmo.py`).** Lambert shading of the mesh vs plate luminance in 4 views (light az 4°/el 40° fitted on the dome, rms 3–7 lum depending on view), smooth mirror-symmetric RBF displacement basis (10 mm sigma), plate-contour hinge constraints, stubble albedo field so the optimiser does not carve stubble. Registration-sensitive regions (nose block, eye surround, lips, brows) excluded from the data term. Validated on a synthetic known field first.
4. **Lower-face + chin-band fairing**, then interior relief (infraorbital, malar apple, buccal, temple, mentolabial, jaw groove, alar lobe/groove, lip volume, seam groove along the carved seam loop, corner pockets).
5. **Ears rebuilt from scratch (`ed2v.py`).** Clean skull base, rolled helix rim whose crest follows the carved helix loop, absolute crest-x targets vs ellipse angle (top tapers, rear uniform), antihelix/concha/tragus relief. Vertices keep their (y,z), so `ear_attach/helix/concha` loops stay on their curves (≤0.06 mm in-plane movement).
6. **Face_M** rebuilt (`faceparts_i.py`): same paint layers + `BrowSoft` rim and `LidCrease` slots, taller lips, softer nostril crescents following the carved nostril rings. Unity colours in `HeroBaseMaleFormProof.cs` (two slots + softer lip tints added).
7. **Unity:** `HeroBase_Male` prefab re-imported; saved-prefab verify PASS (60,358 tris, 30,223 vertices, 1.7022 m, FBX hash matches source).

## Files and hashes
* Blend (final, with continuous jaw groups): `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `68de53ca729ff04d366377e6df302952082abfb8e5621c524d39468c39f630cf` (coordinates verified identical to `data/V_j5.npy`)
* FBX (Body_M + Face_M): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `81276df06cbd136733e20232f388f860de71276d2487b22c508613d0a6e0aa43` (re-import max error 0.00028 mm; exported from the pre-group-fix blend, identical geometry)
* **Rollback (Micro1h):** `work/male-micro1i/baseline/` — blend `349d790e…`, FBX `3b32b270…`, Unity prefab/FBX/materials backup (`unity_backup/`), Micro1h captures, proofs and results. The Micro1h results text is kept as `MALE_HEAD_micro1h_RESULTS.md`.
* Checks: `male-micro1i-checks.json` (all numbers above, loops, regression, plate-fit tables, Face_M offsets).

## Proof
* `02_match_m_head.png` — six head-detail panels, plate | mesh | 50 % overlay (3/4 at the fitted 41°/36° yaws).
* `02b_match_m_multiangle.png` — crops 07–16, per-crop yaw + scale fitted to the skull contour.
* `03_unity_m.png` — real Unity head close-ups + plates + body context.
* `_gate/01_loops_m_head.png`, `_gate/wl_*.png`, `_gate/nrm_*.png`, `_gate/02b_multiangle_fit.json`.
* Progress sheets shown during the session: `work/male-micro1i/progress/`.

## Known limits / next
* The likeness gap left is mostly expression and shading: stronger cheek lift + a wider smile would need either more corner/cheek geometry (Adnan's call vs the measured plate smile) or pigment/AO/stubble shading in the material.
* Ear standoff: front plate vs back/3-4 plates disagree (see EAR_SIZE row).
* Pre-existing and untouched: 92 boundary edges + 2 non-manifold edges (eye openings), loop closing gaps listed above.

## Reproduce
`work/male-micro1i/`: `extract0.py` (start data) → `chain.py <tag> stage1` → `pmo_run3.py c2 c3 10 8 80 4.5 30 8` → `chain.py <tag>` → `apply_preview.py` (blend + Face_M) → `fix_jaw_groups.py` → `export_final.py` (in `../male-micro1h/`) → `audit_i.py` → Unity `-executeMethod GolfArcade.EditorTools.HeroBaseMaleFormProof.Run`; proofs: `render_panels.py` + `compose_02h.py`, `render_ma.py` + `compose_ma.py`, `compose_03i.py`, `wire_loops_i.py`, `unity_like.py` (Unity-lookalike Blender preview used for iteration; it matched the real Unity captures closely).
