GATE: MALE_HEAD FAIL
NAPE_LUMP — in-zone nape fairing done, but the visible lump (C7 bump + crease, z 1.375–1.395, plus the knee at the zone line) is BELOW the z ≥ 1.40 zone line; not touched (needs Adnan's OK, ≈40 verts, ≤ 2 mm)
OVERALL_LIKENESS / FAIL_WRONG_LIKENESS — unchanged from Micro1i (expression: smile / cheek lift; plate stubble tone is pigment). Jaw + neck are closer to the plate in profile and 3/4.

Micro1j · male HEAD + NECK finish · 2026-10-01 · position-only edits on the Micro1i `Body_M` (30,296 verts, 60,358 tris, topology unchanged), `Face_M` untouched. Brief: `parallel_prompts/01_MALE_HEAD_Micro1j.txt`. Every line below is my binary call from the **real Unity captures** (`03_unity_m.png`, `03b_unity_m_micro1j_before_after.png`) and plate overlays; Adnan's eye-check is final.

## Scope lines (the three Adnan notes)
| Line | Result | Evidence |
|---|---|---|
| **JAW_UPPER_PROFILE** (upper jawline more defined in profile) | **PASS** | Plate jaw border measured on profile plates 02/03 (contrast-enhanced): chin underside (y −0.140, z 1.455) → angle (y −0.040, z 1.484) → ramus → ear lobe (y −0.029, z 1.525). The mesh's old `LOOP_jaw_ear` (front-silhouette curve) sat 22–25 mm in front of it. Now a smooth relief follows the plate border: small ridge (+1.6 mm) on the border, inclined recess behind/below it (up to −6 mm, tapering over ~25 mm), coherent displacement directions (normals smoothed 8×, field smoothed 3×+2×). Reads as one continuous line ear lobe → angle → chin in Unity profile and 3/4. Silhouette effect: front/back width ≤ 2.1 mm at z 1.475–1.48, 3/4 plate contours ≤ 0.3 mm at the jaw rows, profile front-y unchanged (5e-8 mm). The plate's *tonal* jaw edge (darker stubbled cheek → lighter smooth neck, ~32 lum) is pigment, not geometry — left to the material. |
| **ADAMS_APPLE_ROUND** | **PASS** | The apple was a 10 mm-polygon wedge (step–plateau–step) 3–5 mm proud of the plate neck edge, merging into the chin with no throat hollow. Neck front contour vs plate average (mm, + = behind): z 1.4464 start −4.5 → now +0.9; 1.4503 −9.4 → −3.7; 1.4425 −1.2 → +1.9; 1.4347 −0.7 → +0.8; 1.4229 +0.9 → +0.6 (table below). Smooth S, round bulge ~2 mm, clean cervicomental corner like the plate's L. Under-chin plane and chin band untouched (0.0 mm). |
| **NAPE_LUMP** | **FAIL (zone-limited)** | In zone (z ≥ 1.40) the nape/diagonal is now aligned to the plate (silhouette incl. shirt): error was +1.2…+3.1 mm behind at z 1.41–1.50, now −1.4…+1.4 (table below), pulled ≤ 2.1 mm. The lump itself is the **C7 bump (+2.5 mm) with a crease (−1.8 mm) under it at z 1.375–1.395, \|x\| < 20 mm**, plus the knee where the diagonal meets the vertical upper back at the zone line (apex of the silhouette hump z ≈ 1.384). All of that is **below z 1.40 → bit-identical, not moved**. The parallel torso chat's merged upper-back change only reaches z ≤ 1.385 and leaves these rows as they were. A ≤ 2 mm local flatten of ≈ 40 verts at z 1.365–1.398 would kill it (needs Adnan's OK to leave the zone). |

## Other gate lines
| Line | Result | Evidence |
|---|---|---|
| NO_HAIR / BALD / Face_M paint-only / NO_STICKER_VOLUME | PASS | `Face_M` vertices + materials bit-identical to the start blend; no hair mesh |
| TOPO_DENSITY / MANDATORY_LOOPS | PASS | topology identical (faces, 17 `LOOP_*` groups identical); face-zone edge mean **3.59 mm** (median 3.42); ear/helix/concha/ear_attach/mouth/nostril/lid loops move ≤ 0.002 mm; `LOOP_jaw_ear_L/R` follow the reshaped border (≤ 1.13 mm) |
| EAR_* / NOSE_* / MOUTH_* / EYE_* / BROW_* / CHEEK_* / CHIN_FRONT_PROFILE | PASS (unchanged) | nothing of these moves; jaw weights are 0 within 8 mm of ear/helix loops, far from lips |
| CHIN_LOCK / UNDER_CHIN | PASS | under-chin plane and chin band: 0.0 mm; only the neck-front rows below the plane (z 1.405–1.452) are reshaped to the plate throat line (documented micro-change) |
| NECK_COLUMN / PROP_HEAD_NECK / NAPE continuity | PASS | no shelf/hole/hairline; back view + 3/4 back continuous |
| HANDS / BODY_BELOW_NECK / zone audit | **PASS** | **0 vertices moved outside the zone** (z_start ≥ 1.40); min z of moved verts 1.40009; hands 0, arms 0, torso/pelvis/legs/feet 0; weld ring (z 1.43) moved 10 verts (inside the z ≥ 1.40 zone) |
| SIL_% | PASS | on the installed (merged) model bald_front **4.3337 %**, bald_back **4.3908 %** (≤ 5.0 %); my edit alone on the Micro1i start: 4.3085 / 4.5951 (start 4.2978 / 4.5897) |
| TRI_BUDGET | PASS | Body_M **60,358** (unchanged, ~60k approved) + Face_M 5,416 |
| NORMALS | PASS | stored custom normals kept everywhere; only rotated by the geometric change on the edit + 1-ring (mean 3.1°, max 32.6° on 6,081 corners); 14 locked verts just below z 1.40 get ≤ 1.9° (shading continuity); 115,313 other corners: ≤ 0.45° (rounding) |
| UNITY | PASS | saved-prefab verify PASS: Body_M 60,358 tris / 30,223 verts (FBX merge), 1.7022 m, FBX hash == source, `normals=Import`, mesh compression Off |

## Evidence tables (mm)
Neck front contour (silhouette, profile plates 02/03 average; + = mesh behind plate):
| z | plate y | start | now | err start | err now |
|---|---|---|---|---|---|
| 1.4033 | −77.8 | −78.5 | −78.5 | −0.7 | −0.6 |
| 1.4190 | −81.8 | −81.1 | −81.4 | +0.6 | +0.4 |
| 1.4229 | −83.1 | −82.1 | −82.5 | +0.9 | +0.6 |
| 1.4307 | −88.2 | −88.0 | −87.2 | +0.2 | +1.0 |
| 1.4386 | −90.2 | −91.1 | −88.8 | −0.9 | +1.4 |
| 1.4425 | −90.8 | −92.0 | −88.9 | −1.2 | +1.9 |
| 1.4464 | −90.2 | −94.7 | −89.3 | **−4.5** | +0.9 |
| 1.4503 | −88.2 | −97.7 | −92.0 | **−9.4** | −3.7 |

Nape back contour (silhouette incl. plate shirt; + = mesh behind plate):
| z | plate y | start | now | err start | err now |
|---|---|---|---|---|---|
| 1.4112 | 78.0 | 79.2 | 77.7 | +1.2 | −0.3 |
| 1.4190 | 71.5 | 73.9 | 72.0 | +2.4 | +0.5 |
| 1.4347 | 59.1 | 60.5 | 58.5 | +1.4 | −0.7 |
| 1.4503 | 46.7 | 49.7 | 47.7 | +3.0 | +1.0 |
| 1.4660 | 41.5 | 43.6 | 41.6 | +2.1 | +0.1 |
| 1.4933 | 37.6 | 40.2 | 39.0 | +2.6 | +1.4 |

Front half-width at the jaw rows vs head-detail plate (mesh − plate): rows 248–256 (z 1.480–1.470) +1.6/+1.7/+1.6 → −0.7/−0.8/+0.8.

## Why the nape lump is not fixed here (numbers)
Back midline (x ≈ ±6 mm) y by z (start, mm): 1.3706 → 86.05 · 1.3781 → 88.24 · 1.3855 → 88.54 · 1.3940 → 86.82 · 1.4016 → 84.41 · 1.409 → 80.66 · 1.417 → 75.16. Normal elevation along the same line: +12.5° (1.362) → **−7.3° (1.378)** → +6.5° → +14° (1.394) → +22° (1.4016, first zone row) → +41.5° (1.430) → +22.8° (1.455). The region above the line is a smooth, near-straight diagonal (±2 mm of the chord nape→weld, = the plate's shirt line); the lump is the crease/bump pair under it. Strong pulls of the zone nape (−6.5 mm) only made the knee at the line sharper, rounding it (+2 mm) worsened the hump — so I left the zone at the plate-aligned ≤ 2.1 mm pull.

## What changed (`work/male-micro1j-head/`)
1. `j_neck.py` — apple/throat: per-row dy profile (PCHIP through the cage rows z 1.396…1.4533, + back at the throat ring 11.5 mm before smoothing) × lateral plateau (flat 14 mm → 0 at 40 mm); nape v2: forward pull ≤ 2.1 mm (z 1.40→1.52, lateral plateau 25 → 62 mm); displacement-field fairing (not position fairing).
2. `j_jaw.py` `edit_jaw3` — signed profile-plane distance `s` to the plate border polyline; `d(s) = −H·ramp(s0 +1.5 → −16 mm)·return(+9 mm) + ridge·gauss`, along 8×-smoothed normals, displacement field smoothed; weights: along-curve (fade in after the chin, full on the ramus), lateral (|x| > 12–30 mm), ear clearance (8 → 24 mm from ear/helix loops), protection of the silhouette-defining mastoid/neck-side bulge behind the angle below z ≈ 1.48. `H = 10, Lr = 16, Lret = 9, ridge 1.6, σr 3`.
3. `run_j.py` builds `data/V_j1.npy` + `D_j1.npy` (delta vs `68de53ca`); `merge_j.py` applies the delta by vertex index onto any base whose zone is unchanged and rotates that base's own normals; `audit_j.py` / `checks_j.py` audit it.

## Install note (parallel chats)
At install time the shared blend/FBX were no longer `68de53ca…`/`81276df0…`: another chat had installed (blend `0dd37baf…`, FBX `614cccf3…`; 18,063 verts moved vs the Micro1i start, **all z < 1.40, 0 in the head/neck zone**, topology identical). I merged the Micro1j delta onto that blend (merge-on-start reproduces the validated result bit-exactly; merged audit above) instead of overwriting it, then exported the FBX and ran the Unity proof once.

## Files and hashes
* Blend (installed): `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `72fc6465f0623a3c963ca3af58a5358c0a498265c7b9da059a941b58050c1059` (base `0dd37baf…`)
* FBX (Body_M + Face_M, installed): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `4d0c8b41e970fa3e9fe6be7f3f7fd9976fc6e490598c38a23d8d2c1db19fe55d` (re-import max error 0.00028 mm); Unity prefab SHA `f65086be…`
* Delta: `work/male-micro1j-head/data/D_j1.npy` (relative to `68de53ca…`); checks: `male-micro1j-checks.json`
* **Rollback:** `work/male-micro1j-head/baseline/artdir_now/` (shared blend `0dd37baf…` + FBX `614cccf3…` as found at install), `baseline/` (Micro1i `68de53ca…` blend + `81276df0…` FBX + Micro1i proofs), `baseline/unity_backup_pre_j/` + `baseline/unity_captures_micro1i/`.

## Proof
* `02_match_m_head.png` — six head-detail panels, plate | mesh | 50 % overlay (Micro1j blend).
* `03_unity_m.png` — real Unity head close-ups + plates + body context (this install).
* `03b_unity_m_micro1j_before_after.png` — Micro1i vs Micro1j real Unity captures next to the plates (profile, 3/4, head side).
* Work/progress renders: `work/male-micro1j-head/{r,progress}/`.

## Known limits / next
* Nape lump (above): decision for Adnan — local flatten of the C7 bump + crease at z 1.365–1.398 (≈ 40 verts, ≤ 2 mm) lies outside the z ≥ 1.40 zone.
* Jaw: geometric relief only; the plate's darker stubble under the jaw and cheek-to-neck tone ramp need pigment/AO in the material.
* Pre-existing and untouched: 92 boundary edges + 2 non-manifold edges (eye openings); OVERALL_LIKENESS gap (expression).
