GATE: MALE_TORSO_FORM2 PASS

Male torso FORM2 (plate likeness: pecs + lower shelf, soft waist/abs, back planes, glute upper) · 2026-10-01 · parallel follow-up to chat 2 · positions-only edit on the existing topology, torso zone only. Silhouette gate <= 5.0 % bald front/back.

| Line | Result | Evidence |
|---|---|---|
| ZONE_OUTSIDE_MOVED=0 | PASS | moved outside zone 0; head/neck, legs, feet, hands, arms categories = [0, 0, 0, 0, 0, 0, 0]; Face_M + LOOP_* groups identical; 5782 verts moved inside the zone |
| TOPOLOGY_IDENTICAL | PASS | 30296 verts / 60358 tris, face list identical, boundary/non-manifold edges 92/2 as baseline, height 1.702187 m unchanged |
| SIL_FRONT_BACK<=5.0% | PASS | front 4.3337 % (before 4.3337), back 4.3908 % (before 4.3908); pure front/back vertex moves leave the outline unchanged |
| DEPTH_vs_SIDE_hold | PASS | depth deficit vs left/right plate silhouettes z 0.92-1.30: rms 2.53 -> 2.69 mm (limit 3.0), mean +0.73 -> +0.73 mm |
| FORM_mean_beats_+0.146 | PASS | mean of 6 torso regions +0.146 (PASS) -> +0.345 (target >= +0.25: met); front chest +0.144 -> +0.450, back upper +0.049 -> +0.413; every one of the 6 regions above its PASS value |
| PELVIS_SHELF_clean | PASS | pelvis band z 0.77-0.90: edges >55 deg 0 -> 0, max crease 50 -> 50 deg; whole zone >55 deg: 2 -> 2 |
| MESH_QUALITY | PASS | flipped faces 0; zone-border displacement max 0.001 mm; min face area 5.58 mm2 (before 5.58); zone dihedral p99 19.7 -> 19.8 deg |
| FBX_REIMPORT | PASS | FBX re-import: 60358 tris, max vertex error 0.00028 mm, height 1.702187 m |

## Zone (exact mask)

`Body_M` vertices with start position **z in [0.77, 1.40)** and **|x| <= xlim(z)**, xlim = 0.205 for z < 1.20, rising linearly to 0.28 at z = 1.24, 0.28 above (shoulder ball) = **7617 of 30296** vertices (same mask as the torso PASS). 5782 of them moved; **0 moved outside**: head/neck (z >= 1.40), arms (|x| > 0.28), arm-gap side, hands/forearms, legs (z < 0.77), feet = 0 each; Face_M, `LOOP_*` groups, cameras, materials, topology bit-identical. The displacement fields are zero at the zone border (max border displacement 0.001 mm).

## What changed

* Start: ArtDir blend `72fc6465…` = the torso PASS (`b9cc8596…`) plus the arms/legs/head installs of the other chats (the brief's start SHA had moved twice during the task); the torso zone was bit-identical to the PASS (guard: 6,248/6,248 zone vertices at their PASS positions).
* **Compatibility with the head chat's staged nape patch** (72 vertices, z 1.352-1.399, optional, pending Adnan's OK): the fit's taper and the depth guard both end below z = 1.352, so nothing at z >= 1.352 moves (a first build did move 56 of those vertices by <= 1.19 mm and was replaced). Dry run of their own `merge_j.py` on the installed blend: `MERGE_CHECK` passes, 60 vertices / 1.74 mm.
* Method: **photometric mesh fit** to the plain plate (front + back), y-displacements only (so the front/back outline cannot change), on the existing vertices. (1) Plate lighting = per-view spherical-harmonic shading model fitted on the **untouched limbs** only; it predicts the unseen torso at correlation 0.88 (front) / 0.91 (back). (2) Unknowns = mirror-symmetric Gaussian-RBF fields (12 mm spacing, 9 mm sigma) for the front and back surface. (3) Residual = plate - model shading, graph high-passed (3 rings) so only form detail counts; Levenberg-Marquardt, finite-difference Jacobian, ridge lambda 0.1. (4) Regularisation relaxed where the plate shows more form: front chest lambda x0.25, back upper z 1.12..1.32 x0.12, pelvis-band penalty of the PASS dropped; the front-chest coefficients (z 1.13..1.25) get a x1.3 gain for a clearer lower-pec shelf. (5) **Depth guard**: front-most / back-most surface per height is pulled back to the PASS depth (single pass, 20 mm smoothing; drift <= 1.8 mm). Scripts/params: `work/male-parallel-torso2/form2.py`, `data/params_FORM2.json`, `pmo_t.py`.
* Displacement: max 10.6 mm (lower-pec shelf), mean of moved 1.4 mm. Shape read-out: outward lower-pec shelf with an inward step under it and a flatter upper-pec slope; softened sternum; inward flanks at the obliques with soft belly lobes; scapula planes and a gentle spine on the back; lumbar bulge; upper-glute swell. The PASS's artificial belt line, double pec line and upper-back band are gone.
* Surface stays as smooth as the PASS (umbrella Laplacian mean 0.256 -> 0.275 mm, zone dihedral p99 19.7 -> 19.8 deg); creases > 55 deg in the zone 2 -> 2 (both are the natural armpit crease), pelvis band 0 -> 0.

## Numbers

| Region (Unity-style clay render vs plate, band-passed shading r) | PASS | FORM2 | delta |
|---|---:|---:|---:|
| front chest (z 1.15-1.32) | +0.144 | +0.450 | +0.306 |
| front abdomen (0.90-1.15) | +0.244 | +0.431 | +0.187 |
| front pelvis (0.80-0.90) | +0.228 | +0.384 | +0.156 |
| back upper (1.15-1.32) | +0.049 | +0.413 | +0.364 |
| back lower/waist (0.90-1.15) | +0.214 | +0.285 | +0.071 |
| back glute-upper (0.80-0.90) | -0.004 | +0.108 | +0.112 |
| **mean of 6** | **+0.146** | **+0.345** | **+0.199** |

* Same metric with a wider band (DoG 2/8): mean +0.197 -> +0.374. Under the **plate's own lighting** (mesh shaded with the fitted SH model): mean +0.278 -> +0.632 (front chest +0.49 -> +0.91, back upper -0.05 -> +0.42). The gate metric is capped well below 1 by the lighting difference alone: the same mesh shaded Unity-style vs plate-lit correlates only ~0.68, so +0.346 is roughly half of the attainable.
* **Held-out check** (never fitted): the sheet's two back 3/4 panels, camera calibrated from the limbs only, torso-zone shading r: left 0.53 -> 0.62, right 0.39 -> 0.52; upper back 0.36/0.26 -> 0.58/0.62; glute 0.80/0.62 -> 0.89/0.85. Hyper-parameters (lambda, blur, high-pass scale, basis size) moved this held-out score by no more than 0.015 (nine settings tried), so the result is not tuned to one image.
* Lower-pec shelf (luminance down the pec, plate-lit): crease height plate z 1.175, PASS 1.195, **FORM2 1.175**; crest-to-crease contrast plate 38 lum, PASS 26, **FORM2 33** (87 % of the plate).
* Silhouette bald front/back 4.3337 / 4.3908 % (unchanged by construction; left/right profile masks 9.03/9.77, arms/hands dominate). Depth vs left/right plate silhouettes z 0.92..1.30: rms 2.53 -> 2.69 mm, mean +0.73 mm; the rise is a few one-pixel (5.8 mm/px) flips in the side masks, the real front/back extremes moved <= 1.8 mm.

## Known remaining differences

* **Glute lobes / V-cleft**: the plate's two lobes and dark V-cleft are mostly at or below the zone floor (cleft contrast -25..-45 lum at z < 0.80, -14 at 0.84, -9 at 0.88); the in-zone fit improves the region (gate r -0.004 -> +0.108, plate-lit 0.29) but it still reads as one block. A dedicated narrow midline basis was tried and the optimiser did not use it (no gain). The plate's gluteal fold arcs, the thigh overhang and the squared crotch slot belong to the legs zone.
* Back lower/waist improved less (gate r +0.21 -> +0.29; the lighting-mismatch ceiling there is ~0.47). Absolute correlations are still modest everywhere: the mesh (10 mm vertices) cannot show the plate's finest 3-6 mm detail and the plate is lit differently from the Unity-style rig.
* The sheet's 3/4 panels and the back plate partly disagree (fitting the back 3/4 panels as well lowered the back-plate upper-back score 0.35 -> 0.20), consistent with the AI-rendered views not being fully 3D-consistent; the plain plate front/back is the fitted authority, the 3/4 panels are validation only. The front 3/4 panels could not be calibrated reliably (lighting model failed on them) and were not used.
* Not touched: shoulders/deltoids/arms/hands/neck/head/legs/feet. **Unity not refreshed**: other chats were still installing; the FBX is staged and installed in `ArtDir/hero/base_lock/unity_import/` only (the Unity project keeps its own copy). Blender previews use the same lights/skin as the Unity proof.

## Files, hashes, install

* Install: SHA-guarded **vertex-index patch** onto the then-current ArtDir blend, in two steps, no whole-file overwrite (the patch aborts if any torso-zone vertex is at none of: torso-PASS start / first FORM2 build / final). Step 1: `72fc6465…` -> `7f145b94…` (first build). Step 2 (final): `7f145b94…` -> `59a74b85…`, 0 foreign vertices. Lock dir taken/released each time; backups `work/male-parallel-torso2/baseline/pre_install_form2_72fc6465.*` (pre-FORM2) and `pre_install_form2_7f145b94.*` (first build). Installed coordinates == staged final; independent post-install audit `proof/male-torso-form2-install-audit.json` (vs the pre-FORM2 snapshot): 5782 verts differ, 0 outside the zone, 0 at z >= 1.352.
* Blend (installed): `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `59a74b8573a089198207a6155ad815625d55f4c670531d6530bc698c1b78ac28`
* FBX (installed; Body_M only, same flags, re-export of the merged blend): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `0454f6c36cdc2199d3a4e7885a7087821ea2642a764e90edda203920e89072a1` (60358 tris, re-import max error 0.00028 mm, height 1.702187 m; differs from the pre-FORM2 FBX only at zone vertices)
* Staged (pre-merge) copies: `work/male-parallel-torso2/export/HeroBase_Male_Silhouette_form2.blend` `b2b540fff9ab…`, `HeroBase_Male_Body_form2.fbx` `b645bf10ceb8…`.
* **Rollback**: copy the `pre_install_form2_72fc6465` backups (blend + FBX) back over the ArtDir files for the pre-FORM2 state. **Re-apply on a newer blend**: `blender -b --factory-startup --python work/male-parallel-torso2/apply_form2.py -- <current.blend> <out.blend>` (6,248 torso-zone vertices in `export/form2_zone_delta.npz`; accepts the PASS, first-build or final state per vertex, aborts on anything else).
* Machine checks: `proof/male-torso-form2-checks.json` (zone categories, topology, integrity, silhouette, depth, form r, plate-lit, held-out, gates, FBX, install).

## Proof

* `proof/02f_form2_proof_m.png` — front/back: plate | PASS | FORM2, real render and local-contrast relief, with the gate numbers.
* `proof/02f_form2_proof_q3_m.png` — four 3/4 views: plate | PASS | FORM2.
* `proof/02f_form2_proof_side_m.png` — left/right silhouettes vs the plate masks, PASS vs FORM2.
* `proof/02f_form2_proof_detail_m.png` — chest / waist / upper back / glute under the plate's own lighting.
* `proof/02f_form2_proof_pelvis_m.png` — pelvis shelf and glute under raking light (shelf stays clean).
* `proof/02f_form2_zone_m.png` — displacement heat map (grey = bit-identical).

## Reproduce

`work/male-parallel-torso2/`: `form2.py` (fit + depth guard -> `data/V_FORM2.npy`), `pmo_t.py` / `slab.py` / `plateview.py` (optimiser, guard, plate views), `make_blend.py` + `export_fbx.py`, `audit_torso.py` + `finalize_form2.py` (zone/integrity/gates), `rt.py` / `rake.py` / `sil.py` (renders), `compose_form2.py`, `install_form2.sh`, `apply_form2.py`; validation `calib_q3b.py`, `pmo_val.py`, `valscan.py`; metric `relcorr2.py`, `shlit.py`.
