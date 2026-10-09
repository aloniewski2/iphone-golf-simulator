GATE: MALE_HEAD PASS
failed lines: none

Micro1k · male HEAD + NECK jaw / cheek fix · 2026-10-01 · position-only edits on the installed Micro1j `Body_M` (30,296 verts, 60,358 tris, topology unchanged), `Face_M` untouched. Brief: `parallel_prompts/01b_MALE_HEAD_Micro1k.txt`. Adnan's notes on Micro1j: the "jawline" is on the neck, not the jaw; cheekbones too pronounced. Work dir `work/male-micro1k-head/`. Scripts: `run_k.py`, `k_edit.py`, `k_mand.py`, `merge_j.py`, `audit_j.py`, `checks_k.py`.

## Adnan's hard fails
| Line | Result | Evidence |
|---|---|---|
| **KILL_NECK_JAW_CREASE** | **PASS** | The Micro1j recessed jaw-border relief is removed exactly (`V − Djaw`, 240 vertices; the Adam's apple / throat / nape work of Micro1j is kept). On its lateral footprint (194 verts) the normal-direction recess against the pre-relief surface was **-5.98 mm** and is now **-0.65 mm** (the only positive residual is the new jaw edge, max 1.84 mm). Low-pass groove residual on that footprint: Micro1j -1.01 / pre-relief -1.06 / now -1.05 mm (the natural neck-side concavity is unchanged, nothing new is carved). Unity captures (three-quarter, profile, 3/4 back, head side): no dark groove on the neck. |
| **REAL_MANDIBLE_PROFILE** | **PASS** | A soft-plastic bone edge now sits ON the jaw: a convex crest along the mandible (chin end → gonial angle, fading out up the ramus toward the lobe), geodesic-distance asymmetric Gaussian (σ 6 mm each side, no recess), displacement along smoothed normals. Crest height along the arc (mm from the chin end): 0: 0.6–0.6, 10: 1.5–1.7, 20: 2.0–2.0, 30: 2.3–2.4, 40: 2.5–2.5, 50: 2.3–2.3, 60: 2.2–2.2, 70: 2.0–2.1, 80: 1.9–1.9, 90: 1.5–1.5, 100: 0.5–0.5, 110: 0.3–0.3, 120: 0.3–0.3, 130: 0.2–0.2, 140: -0.0–-0.0; max **2.49 mm**, edge slope max 0.26. Softer (1.5 mm) at the gonial angle / ramus because that surface faces sideways (any ridge there is lateral growth); the remaining ≤ 0.3 mm of width growth is trimmed back. Jaw-row front half-width error vs plate: max **1.96 mm** (Micro1j 1.85); profile front-y / back-y contours change **0.0 / 0.0 mm**; 3/4 contour max change 1.83 mm. See the placement note below. |
| **CHEEK_SOFTEN** | **PASS** | Malar relief pulled toward the smooth plate-like cheek (Micro1h surface) with a blend of 0.6 and a smooth region weight that stays 7–18 mm from the mouth, 5–15 mm from the nose, 9–22 mm from the eyes and 14–30 mm from the ear loops. Deviation of the cheek surface from the smooth Micro1h cheek (917 verts): RMS **1.78 → 0.87 mm** (−51 %), p95 3.32 → 1.66 mm, strongest ridge +3.77 → +2.55 mm, deepest hollow -5.30 → -3.67 mm. About half of the malar definition is kept (not a blank cheek). |
| **NAPE_LUMP** | **PASS** | Adnan-approved strip below the old z = 1.40 line: **20 vertices**, z 1.3619–1.3947, |x| ≤ 24.5 mm, max move **1.95 mm** (≤ 2 mm; the staged Micro1j strip × 1.12, tails below 0.05 mm dropped). C7 bump over the chord z 1.371–1.402: **3.26 → 1.35 mm** (−59 %). The knee where the nape diagonal meets the upper back is anatomy beyond a 2 mm budget and stays gentle (Unity head-side capture). |
| **ADAMS_APPLE_ROUND** (keep Micro1j) | **PASS** | Midline front contour at the apple rows z 1.4033–1.4464 changes at most **0.27 mm** (error vs plate max 1.9 → 1.6 mm); the top row z 1.4503 improves -3.7 → -2.5 mm. 20 lateral throat vertices (|x| 15–30 mm, z 1.40–1.445, ≤ 1.7 mm) move only because the crease undo restores the pre-relief surface there. |
| **OVERALL_LIKENESS closer** | **PASS** | The three flagged defects are fixed and the silhouette gate improves slightly: bald front / back silhouette 4.3203 % / 4.3854 % (Micro1j 4.3337 / 4.3908 %); 3/4 contour rows within 3 mm of the plate on the jaw/cheek rows 32 → 34 of 60; jaw half-width error 1.85 → 1.96 mm. Unity before/after: `proof/03b_unity_m_micro1k_before_after.png`. Local outline rows move by ≤ 2.3 mm (see the tables; all still within 3.5 mm of the plate). Expression / stubble-tone differences from the Micro1i/j notes are not part of this brief and are unchanged. |

## Other gate lines
| Line | Result | Evidence |
|---|---|---|
| ZONE_OUTSIDE | **PASS** | Moved vertices 3,048; **below z 1.40: 20 = the documented nape strip only** (z 1.3619–1.3947, ≤ 1.95 mm). Hands 0, arms 0, torso / pelvis / legs / feet outside the strip 0. Merged onto the CURRENT shared blend (`59a74b85…`, torso FORM2 installs): merge check clean (5,782 vertices differ from Micro1j, none in the head zone or under the delta). |
| SIL_% | **PASS** | bald_front **4.3203 %**, bald_back **4.3854 %** (≤ 5.0 %; Body_M alone and with Face_M identical). |
| NO_STICKER_VOLUME | **PASS** | Pure surface displacement, no new geometry; crest ≤ 2.49 mm, crest edge slope ≤ 0.26 mm/mm; no flipped faces (face-normal rotation max 31° at the restored neck side). |
| NO_HAIR / BALD / Face_M | PASS | `Face_M` vertices + materials bit-identical to the start blend; no hair mesh. |
| TOPOLOGY / LOOPS / TRI_BUDGET | PASS | faces, 17 `LOOP_*` groups identical; Body_M **60,358** tris (~60k approved) + Face_M 5,416. Mouth / nostril / ala / concha loops move ≤ 0.000001 mm, ear / helix loops ≤ 0.0025 mm; the jaw loop (`LOOP_jaw_ear_*`) follows the jaw ≤ 1.86 mm. |
| NORMALS | PASS | stored custom normals kept; only rotated by the geometric change on the edit + 1-ring (audit: far-from-edit corners unchanged). |
| UNITY | PASS | `HeroBaseMaleFormProof.Run` batchmode: saved-prefab verify PASS, Body_M 60,358 tris / 30,223 verts (FBX merge), 1.7022 m, FBX hash == source `73582af619f6…`, `normals=Import`, mesh compression Off. Captures: `proof/male_form_d/unity/*` (copy `work/male-micro1k-head/proof/unity_captures_k11/`). |

## Method (what changed)
1. **Undo** the Micro1j jaw-border relief: `V − Djaw` (the exact per-vertex delta Micro1j added). Everything else Micro1j did stays.
2. **Cheek**: blend 0.6 toward the Micro1h smooth cheek surface inside a smooth cheek-region weight (never near the lip / nose / eye / ear loops).
3. **Mandible edge**: crest on the mesh's natural jaw border (n_z = −0.5 iso-line) + the plate angle / ramus points, height 2.7 mm on the jaw body (measured peak 2.5 mm after smoothing), 1.5 mm at the angle / ramus, σ 6 mm up and down, along 8× smoothed normals, displacement field smoothed 3×, ear clearance 8–24 mm, fade-in after the chin, only |x| > 26 mm. A broad Gaussian inward trim (σ 16 mm round the angle / ramus) takes back the crest's lateral growth above 0.3 mm so the front / 3/4 jaw outline stays on the plate.
4. **Nape**: the staged Micro1j strip (`optional_nape_strip/D_strip.npy`) × 1.12, tails dropped: 20 vertices, ≤ 1.95 mm.
5. **Normals**: the base blend's own stored custom normals rotated by the geometric change (Rodrigues); installed by delta merge onto the current shared blend (`merge_j.py` + `data/D_k11.npy`).

## Placement note (honest)
The crest follows the mesh's own visible jaw border. The plate-derived mandible border (the profile-plate tone edge between the stubble and the neck, `proof/arc_overlay_k11.png`, magenta) lies 6–12 mm LOWER along the jaw body and coincides at the gonial angle / ramus; on the mesh that line falls on the under-jaw / neck plane, which is exactly where Micro1j put its crease and Adnan rejected it. The edge is therefore on the jaw, but it is not carved down to the plate's tone edge. The plate edge is largely stubble pigment, not geometry.

## Evidence tables (mm)
Jaw rows vs the plate outline (mesh − plate; front-y / back-y / front half-width) — Micro1j → now:
| row | z | front-y | back-y | half-width |
|---|---|---|---|---|
| 228 | 1.506 | +1.7 → +1.7 | -0.8 → -0.8 | +0.4 → +0.2 |
| 232 | 1.501 | -1.5 → -1.5 | -1.1 → -1.1 | -0.1 → -0.2 |
| 236 | 1.496 | -0.9 → -0.9 | -1.2 → -1.2 | +0.8 → +0.7 |
| 240 | 1.491 | +0.3 → +0.3 | -1.3 → -1.3 | +1.9 → +2.0 |
| 244 | 1.486 | -1.2 → -1.2 | -0.8 → -0.8 | +1.1 → +1.7 |
| 248 | 1.480 | -1.2 → -1.2 | -1.6 → -1.6 | -0.7 → +1.6 |
| 252 | 1.475 | -0.7 → -0.7 | -1.0 → -1.0 | -0.8 → +1.7 |
| 256 | 1.470 | +0.0 → +0.0 | -1.7 → -1.7 | +0.8 → +1.6 |
| 260 | 1.465 | +2.3 → +2.3 | -2.1 → -2.1 | +1.3 → +1.3 |
| 264 | 1.459 | +1.9 → +1.9 | -1.8 → -1.8 | +1.0 → +1.0 |

Rows 244–256 (z 1.486–1.470): the front half-width error goes from ≈ 0 to +1.6 … +1.7 mm because the crease undo brings the pre-relief neck-side width back (undo alone: +1.4 … +1.7 mm); still ≤ 2 mm, and the crest itself adds ≤ 0.3 mm.

3/4 contour vs plate on the jaw / cheek rows (mesh − plate; q3L left/right edge, q3R left/right edge) — Micro1j → now:
| row | q3L L | q3L R | q3R L | q3R R |
|---|---|---|---|---|
| 494 | +2.4 → +2.4 | +7.2 → +7.2 | +4.3 → +4.3 | +3.6 → +3.6 |
| 500 | +4.3 → +3.5 | +9.5 → +9.5 | +4.1 → +3.4 | +5.7 → +5.7 |
| 506 | +3.3 → +2.2 | +12.0 → +12.0 | +4.2 → +3.8 | +8.1 → +8.1 |
| 512 | +1.4 → +0.7 | +14.5 → +14.5 | +2.4 → +2.6 | +9.5 → +9.5 |
| 518 | -1.8 → -1.7 | +16.0 → +16.0 | -0.0 → +0.6 | +9.8 → +9.8 |
| 524 | -4.1 → -3.0 | +12.0 → +12.0 | -1.7 → -0.6 | +9.8 → +9.8 |
| 530 | -4.1 → -2.9 | +7.9 → +7.9 | -1.5 → -0.5 | +8.8 → +8.8 |
| 536 | -4.3 → -2.4 | +5.6 → +5.6 | -2.6 → -1.2 | +3.3 → +3.4 |
| 542 | -3.0 → -2.1 | +5.4 → +5.5 | -1.7 → -0.5 | +3.3 → +3.5 |
| 548 | -1.0 → -0.4 | +4.3 → +4.3 | -0.5 → +0.3 | +1.8 → +1.9 |
| 554 | -1.8 → -1.0 | +2.9 → +2.9 | -1.6 → -0.4 | +1.7 → +1.8 |
| 560 | -3.1 → -2.6 | +0.8 → +0.8 | -2.8 → -2.1 | +0.7 → +0.7 |
| 566 | -2.4 → -3.5 | +2.6 → +2.6 | -1.8 → -2.9 | +0.9 → +0.9 |
| 572 | -1.9 → -2.4 | +2.4 → +2.4 | -1.6 → -3.1 | +1.9 → +1.9 |
| 578 | -2.1 → -2.1 | +1.2 → +1.2 | -0.7 → -0.9 | +0.6 → +0.6 |

Rows 566 / 572 (z 1.484 / 1.476) get slightly wider than the plate: q3L left edge row 566 −2.4 → −3.5, q3R left edge row 572 −1.6 → −3.1. Part of it is the pre-relief neck-side bulge coming back with the crease undo (row 566 with the undo alone: −2.9 / −2.2), the rest is the jaw crest (≤ 1.5 mm at the angle). It is not a crease and the silhouette gate is unaffected (4.32 / 4.39 %).

Nape midline (|x| < 7.5 mm, back side), y in mm vs z:
| z | y Micro1j | y now |
|---|---|---|
| 1352.8 | 91.73 | 91.73 |
| 1361.9 | 88.26 | 88.33 |
| 1370.6 | 86.64 | 86.58 |
| 1378.1 | 88.20 | 86.92 |
| 1385.5 | 88.54 | 86.59 |
| 1394.0 | 86.82 | 85.39 |
| 1401.6 | 83.79 | 83.79 |
| 1409.0 | 79.31 | 79.31 |

Neck front contour at the apple rows (profile-plate average):
| z | plate y | Micro1j | now | err Micro1j | err now |
|---|---|---|---|---|---|
| 1.4033 | -77.8 | -78.5 | -78.5 | -0.6 | -0.6 |
| 1.4112 | -79.2 | -79.4 | -79.4 | -0.3 | -0.3 |
| 1.4190 | -81.8 | -81.4 | -81.4 | +0.4 | +0.4 |
| 1.4229 | -83.1 | -82.5 | -82.5 | +0.6 | +0.6 |
| 1.4307 | -88.2 | -87.2 | -87.2 | +1.0 | +1.0 |
| 1.4347 | -89.6 | -88.7 | -88.8 | +0.8 | +0.8 |
| 1.4386 | -90.2 | -88.8 | -89.0 | +1.4 | +1.2 |
| 1.4425 | -90.8 | -88.9 | -89.2 | +1.9 | +1.6 |
| 1.4464 | -90.2 | -89.3 | -89.3 | +0.9 | +0.9 |
| 1.4503 | -88.2 | -92.0 | -90.7 | -3.7 | -2.5 |

## Known residuals (not in this brief, unchanged)
- Expression / smile and plate stubble tone (see `MALE_HEAD_micro1j_RESULTS.md` OVERALL_LIKENESS note).
- A darker nasolabial line beside one nose wing (inside the nose / mouth protection weights, left alone).
- A soft horizontal light band across the neck base in the Unity head-side capture (also in Micro1j) along the coarse neck cage z 1.40–1.444 — left alone.

## Files
- Installed blend `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 **`1fe4d53b3bcb9cfaef885d64350150e323a1d726cc4d54cc305ea71e323119bb`**; FBX `unity_import/HeroBase_Male_Body.fbx` **`73582af619f63ba34db74a390eccfcca2f321b123761d4f3d971278a1aaa3f90`**; Unity prefab `f65086bee4edcb9f…`.
- Start blend (Micro1j) `72fc6465f0623a3c…`; shared blend at install `59a74b8573a08919…` (FBX `0454f6c36cdc…`).
- Delta: `work/male-micro1k-head/data/D_k11.npy` (+ `N_k11.npy` normals) relative to Micro1j `72fc6465…`; rebase onto any newer blend with `blender -b --factory-startup --python work/male-micro1k-head/merge_j.py -- <current.blend> work/male-micro1k-head/data/D_k11.npy <out.blend>` (refuses if the head zone differs).
- Rollback: `work/male-micro1k-head/baseline/artdir_pre_k/` (blend `59a74b85…`, FBX `0454f6c3…`, previous Unity captures) and `work/male-micro1k-head/baseline/HeroBase_Male_Silhouette.blend` (Micro1j `72fc6465…`).
- Proof: `proof/02_match_m_head.png`, `proof/03_unity_m.png`, `proof/03b_unity_m_micro1k_before_after.png`, `work/male-micro1k-head/proof/arc_overlay_k11.png`, `proof/progress_final_plate_vs_micro1j_vs_k11.png`, checks `work/male-micro1k-head/proof/male-micro1k-checks.json`.
