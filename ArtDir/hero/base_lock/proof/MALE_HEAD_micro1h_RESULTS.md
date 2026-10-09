GATE: MALE_HEAD FAIL
OVERALL_LIKENESS / FAIL_WRONG_LIKENESS / TRI_BUDGET (AGENTS.md 35–50k lock — needs Adnan's OK)

Micro1h · male FACE LOCK · 2026-10-01 · **Path A: local densify of the Body_M head** (weld ring bit-unchanged), forms sculpted into Body_M, mandatory loops carved, thin `Face_M` kept for paint only. Start: Micro1g lock `c6b66ede…` (verified with `shasum -a 256` before any edit). Adnan's eye-check vs `face_lock/` is final; every line below is my binary call against FACE_SPEC.md, judged on the **Unity** captures (`03_unity_m.png`) and the plate overlays, not on the Blender clay.

All Blender work ran headless. The live Blender session / its open scene was not touched.

## Gate lines (FACE_SPEC.md + Micro1h)

| Section | Line | Result | Evidence |
|---|---|---|---|
| Overall | **OVERALL_LIKENESS** | **FAIL** | Profile and landmarks match (overlays rows 01–06 in `02_match_m_head.png`; profile front-y rms 1.4 mm excluding the nose-base rows the plate skin-mask cannot read, half-widths rms 1.0 mm). Eyes, brows, nose, ears and mouth are now formed and clean, not "features applied". But the Unity front/3-4 still do not read as the plate man at arm's length: cheeks are flat (no malar/smile lift), the lower face is heavier than the plate, lips are thin lines, and the plate's soft AO/stubble shading is absent. Not face_example, not a blob — not "clearly the plate male" either. |
| | BALD_NO_HAIR | PASS | No hair object; scalp is Body_M. |
| | STYLE | PASS | Smooth soft-plastic forms; no pores, no dough, no Roblox. |
| | SKIN_TONE | PASS | Unity `Male_Form_Skin` sRGB .74/.555/.435, smoothness .40. Lit forehead in the Unity capture ≈ (232,180,148) vs plate forehead (238,180,143). |
| | DEFAULT_EXPR | PASS | Seam centre→corners rise 5.0 mm (plate-measured), corner hooks + nasolabial folds, no teeth. Reads neutral-pleasant; weaker than the plate's smile (see OVERALL). |
| | SHIRT_OUT_OF_SCOPE | PASS | Nothing modelled below the neck. |
| 1 Cranial | CRANIAL_FRONT / CRANIAL_PROFILE / CRANIAL_TOP | PASS | Front contour rows 100–160 within ±2.6 mm; profile dome within ±3 mm (forehead 2.5–2.9 mm proud at z 1.63–1.64); top view `02_match_m_head.png` row 06 is an approximate fit (plate top view is perspective): egg wider aft than fore, ear tips peek. |
| | SCALP_SURFACE | PASS | Analytic ellipsoid dome + 2 Taubin passes in the face zone; no ripples in the normal-colour maps (`_gate/nrm_front.png`, `nrm_q3L.png`). |
| 2 Forehead | FOREHEAD / BROW_SHELF / BROW_WIDTH | PASS | Four brow lobes (3.6/5.8/5.3/3.2 mm ×1.24) + orbit recess 7 mm give a soft shelf spanning the orbit; glabella 0.9 mm; no cliff. |
| 3 Eyebrows | BROW_SHAPE / BROW_PLACE / BROW_SEPARATION | PASS | Dark ribbons (≤2.15 mm proud) with rounded blunt inner ends and tapered tails on the plate centreline; ~35 mm gap above the nose root. |
| 4 Eyes | EYE_SIZE / EYE_IRIS | PASS | Opening 36.3 × 13.5 mm (carved margin loop), iris Ø15.2 mm incl. limbal ring (plate ≈ 15), brown with dark limbal ring, one catchlight; upper lid covers 2.6 mm of the iris as in the plate. |
| | EYE_LIDS / EYE_SOCKET | PASS | Lash line is built on the real carved lid-margin loop; upper lid roll 2.3 mm, lower 1.2 mm, crease groove 1.4 mm; orbit recess + funnel to the eyeball shell (`_gate/wl_eyes.png`, Unity `eye_front.png`). |
| | EYE_SPACING / EYE_SYMMETRY | PASS | Inner corners ±19.9 mm (39.8 mm apart vs 36.3 mm eye width); built mirrored. |
| 5 Nose | NOSE_HEIGHT | PASS | Tip y −0.1718 at z 1.5455 vs plate −0.1711 (−0.7 mm); thirds from the plate rows. |
| | NOSE_BRIDGE_PROFILE / NOSE_TIP / NOSE_ROOT | PASS | Straight bridge, rounded tip bulb, soft root (Unity `face_profile.png`). |
| | NOSE_ALAE / NOSE_WIDTH_FRONT | PASS | Alar lobes + alar crease chains (`ala_L/R`), nostril pits 3.2 mm deep with rims carved (`nostril_L/R`), nostril openings visible in the Unity 3/4. Foot half-width 24 mm (plate ±24). Reads soft rather than crisp. |
| 6 Cheeks | CHEEK_ATHLETIC / CHEEKBONE_3Q | PASS | Malar bump 6.4 mm at x 51 / z 1.546; no balloon; faint cheekbone plane in 3/4 (weak — see OVERALL). |
| | CHEEK_WIDTH | PASS | Front silhouette within 2.6 mm at rows 220–252 (1.3 mm typical); 3/4 face-side contour within ±2.6 mm over rows 430–560 for both 3/4 plates except the brow-shadow rows 470–480 the plate skin-mask misreads (yaws re-fitted, see below). |
| 7 Ears | EAR_ANATOMY | PASS | Helix rim, scapha, antihelix + crura, concha bowl, tragus, antitragus, lobe on 1.2–2.4 mm cells; readable in Unity `ear_side.png`, soft. |
| | EAR_SIZE / EAR_PLACE / EAR_Y_PAIR | PASS | Outline taken from the profile plate (top z 1.599, bottom z 1.527, 72.8 mm); mirrored. Front view: lobe rows 3–6 mm narrower than the plate; 3/4 views: rim 4–12 mm wider — the plates disagree, outline follows the profile plate. |
| | EAR_ATTACH | PASS | One smooth shell on the head (attach loop 117 verts per ear, 4 Taubin passes); clean in Unity `ear_back.png` and the back overlays (`02b_match_m_multiangle.png` rows 08, 10, 15, 16). |
| 8 Mouth | MOUTH_SMILE / MOUTH_WIDTH | PASS | Seam ±36.3 mm at corners (plate), corners 5.0 mm up. |
| | LIP_VOLUME / PHILTRUM | PASS | Lips protrude 6.9/5.3 mm in profile (plate profile), upper thinner than lower, philtrum groove −0.8 mm; from the front the lips read faint (see OVERALL). |
| | TEETH_DEFAULT | PASS | None. |
| 9 Jaw/chin | JAW_U / JAW_PROFILE / NO_JOWLS | PASS | Chin band unchanged; profile overlay clean; no jowls. |
| | CHIN_CLEFT | PASS | 0.8 mm hint at z 1.4725–1.496 (above the locked band). |
| | UNDER_CHIN | PASS | Under-chin plane held: chin band moved ≤0.444 mm (94 of 270 verts, fairing only). |
| 10 Stubble | STUBBLE_ROOM / STUBBLE_ZONE | PASS | Jaw/chin silhouette untouched; no sculpted hair. |
| 11 Neck | NECK_COLUMN / CHIN_LOCK / NAPE | PASS | 0 vertices moved outside the head zone; nape continuous (Unity `back.png`, `02b` rows 08/10). |
| 12 Proportions | PROP_THIRDS / PROP_EYE_SPACE / PROP_EAR_ALIGN / PROP_NOSE_MOUTH / PROP_HEAD_NECK | PASS | Landmarks placed from plate rows (brow 146 / eye 168 / tip 198 / base 207 / seam 228 / chin 272); neck unchanged. |
| 13 Material | MAT_SKIN / CONSTRUCT / NO_HAIR_MESH | PASS | URP Lit skin, smoothness .40; forms are in Body_M; Face_M is paint + eyeballs; no hair. |
| | BODY_BELOW_NECK | PASS | Bit-identical (see Regression). |
| 14 Tags | FAIL_WRONG_LIKENESS | **FAIL** | Same call as OVERALL_LIKENESS. |
| | other FAIL_* | none | No hair bowl, pink shirt, dough cheeks, lump ears, sticker eyes, pinched brows or low nose. |
| Micro1h | TOPO_DENSITY | PASS | Mean head edge 9.60 → 4.32 mm; face zone 9.96 → **3.59 mm** (median 3.40); nose+mouth 10.0 → **2.65 mm** (88 % ≤ 4 mm); eyes 10.9 → 3.28 mm; ears 9.3 → 3.03 mm (81 % ≤ 4 mm). Cranial dome stays coarse (p95 10.7 mm head-wide); face-zone p95 6.6 mm at grading transitions. |
| | MANDATORY_LOOPS | PASS | alae/nostril: `ala_L/R` 20/19 + `nostril_L/R` 23/23 · lids: `lid_margin_L/R` 46/46 · mouth: `mouth_seam` 59, `mouth_lip_upper` 39, `mouth_lip_lower` 49 · jaw→ear: `jaw_ear_L/R` 71/70 · helix+concha: `helix_L/R` 65/65, `concha_L/R` 50/50, `ear_attach_L/R` 117/117. Wireframes: `_gate/01_loops_m_head.png`, `wl_*.png`; vertex groups `LOOP_*` in the blend. |
| | NO_STICKER_VOLUME | PASS | Face_M distance from the skin: brows ≤2.15 mm (hair ribbons), lash/lip/seam/nostril strips 0.10–0.34 mm; eyeballs sit behind the lid margin. Nose, cheeks, ears, lids, lips, brow ridge are Body_M geometry. |
| | HANDS | PASS | 0 vertices moved in the hands. |
| | SIL_% | PASS | bald_front **4.3121 %**, bald_back **4.6175 %** (≤ 5.0 %); with Face_M identical. Micro1g was 4.05 / 4.32. |
| | CHIN_LOCK | PASS | max 0.444 mm. |
| AGENTS.md | **TRI_BUDGET** | **FAIL (needs approval)** | Body_M **60,358 tris** (was 49,304; head 15,008) vs the 35–50k lock. The densify the brief mandates is the cause. Face_M adds 4,580. If you want it back under 50k, the cranial dome/neck weld can be decimated (~−3k) but the 2–4 mm face zone cannot. |

## Why FAIL, and what it takes to pass
* **The plates disagree with each other, and the chin lock sits on the wrong side of it.** Fitting each 3/4 plate properly (pupil spacing + contour residual) gives yaws of **41° (plate 04) and 36° (plate 05)**, not the 32.5°/30° used in Micro1g and earlier h-builds — Micro1g's 3/4 overlays were mis-posed. With the right yaws the face now matches both 3/4 contours within ±2.6 mm everywhere **except** z 1.466–1.479, where the locked Micro1e chin band is 8–10 mm wider than the plate on the near side in *both* 3/4 plates, while the front/profile plates (which that lock was fitted to) agree with it. Which plate is authority for the lower face is an identity call for Adnan; I did not reopen the lock.
* The plate's friendliness comes from cheek lift, the smile and soft AO/stubble shading. Cheeks/smile are sculptable (next pass); the shading is pigment/AO and URP lighting, outside mesh scope.
* Eye openings are open boundaries (92 boundary edges = the two lid-margin rims, eyeballs behind). The 2 non-manifold edges at (±0.2375, 0.031, 1.304) are pre-existing.

## What changed
* **Method (Path A).** The cage head region above the weld ring (z ≥ 1.43, ring bit-unchanged) is replaced by an error-driven graded quad subdivision (level 1 = 4.7 mm cells over the face, level 2 = 2.4 mm on ears, nose block, lids and mouth; loop carving adds ~1.2–2.4 mm spacing along the chains), positions given by an analytic head function: plate-silhouette loft (superellipse sections, exponents refitted to the 3/4 contours) + sculpt fields (brows, orbit, nose block + alae + nostril pits, malar, mouth/philtrum/chin hint, nasolabial, lids, ears). No displacement maps/modifiers, no sticker volume. Eye openings are carved holes whose boundary is the margin loop (skin dipped to the eyeball shell + 1.4 mm lid thickness, 46-vertex loop per eye). Loops are carved as real edge chains. Ear (4×) and face zone (2×, eyes excluded) get Taubin smoothing, which removed the sawtooth rim and the nose/mouth normal speckle.
* **Volume parameters** (cheeks, nose foot, alae, muzzle, nasolabial, brows, orbit, glabella) fitted by analysis-by-synthesis: Lambert light fitted on the plate dome, predicted shading vs plate luminance over the face, plus profile/3-4 contour constraints (`opt_shape.py`, `shade2.py`, `fit_nf3q.py`). The first fit improved the objective only ~6 %, so most of the gap is not in these parameters.
* **Face_M** (2,686 verts / 4,580 tris, 11 slots): eyeballs with iris/pupil/limbal/catchlight geometry, brows with rounded ends, lash lines built on the margin loop, lip tints, seam, nostril liners. Unity colours/smoothness set in `HeroBaseMaleFormProof.cs` (iris smoothness .20 so it reads brown, not grey).
* **Unity:** `HeroBase_Male` prefab re-imported (60,358 tris, 30,223 vertices, 1.7022 m, scale 1, compression off, normals imported); saved-prefab verify PASS.

## Files and hashes
* Blend: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `349d790e28147aedaf7aa6aa1b0879dc2856bf0d6ba7bb054552d1177bccfc12`
* FBX (Body_M + Face_M): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `3b32b270ba6aefb74f96d5301f7d98ba71ed0d3acbc3e47a1d3cd821ebaaced8` (re-import max error 0.00028 mm both objects)
* Unity audit: `male-form-unity-audit.txt`; captures in `male_form_d/unity/`.
* **Rollback:** `work/male-micro1h/baseline/` (start blend `c6b66ede…`, FBX, `HeroBaseMaleFormProof.cs.micro1g`); Micro1g result kept as `MALE_HEAD_micro1g_RESULTS.md`; intermediate builds `work/male-micro1h/final/…_f1…f18`.
* Checks: `male-micro1h-checks.json` (edge stats before/after, loops, regression, Face_M offsets, plate-fit numbers, fitted parameters).

## Proof
* `02_match_m_head.png` — six head-detail panels: plate | mesh | 50 % overlay (3/4 panels at the fitted 41°/36° yaws, pupil-aligned; top view approximate).
* `02b_match_m_multiangle.png` — crops 07–16 (body-bald and multiangle heads incl. back/3-4-back for nape and ear attach), per-crop yaw + scale/translation fitted to the skull contour.
* `03_unity_m.png` — Unity head close-ups + plates + body context.
* `_gate/01_loops_m_head.png`, `_gate/wl_*.png` (loop wireframes), `_gate/nrm_*.png` (normal-colour diagnostics).

## Reproduce
`work/male-micro1h/`: `blender -b --factory-startup --python run_final.py -- f18 1 2 0.45 2 data/opt_theta_final.json 2.2 0 4 2` (build + blend), `audit_h.py` (regression/silhouette/topology), `export_final.py` (FBX + re-import), then `Unity -batchmode -projectPath Unity -executeMethod GolfArcade.EditorTools.HeroBaseMaleFormProof.Run`; proofs: `render_panels.py` + `compose_02h.py`, `render_ma.py` + `compose_ma.py`, `compose_03h.py`, `wire_loops.py`, `normals_view.py`. Core: `h1lib.py`, `loft.py`, `sculpt.py`, `stage2.py`, `finalize.py`, `faceparts.py`.
