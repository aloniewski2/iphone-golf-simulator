# FEMALE_HEAD_RESULTS — MicroF1 (chat 6 / 8)

**GATE: FEMALE_HEAD FAIL** — every measured gate passes, but OVERALL_LIKENESS / FAIL_WRONG_LIKENESS are **not signed off** (Adnan's eye-check is final) and five more lines are marked OPEN below because I can only judge them from my own Unity captures. Nothing is installed into the shared ArtDir blend / official female FBX or prefab paths.

Time: ≈ 2 h 20 min on 2026-10-01 (21:36 → 23:55, stopped by a usage limit) + ≈ 1 h on 2026-10-02 (06:47 → 07:45).
Legend: **PASS** = measured or true by construction · **PASS(eye)** = my visual judgement on the Unity captures, still subject to Adnan · **OPEN** = I cannot honestly call it · **FAIL**.

## Where things are
| What | Path |
|---|---|
| Start blend (SHA `200dc3b9…` verified before and after; male blends untouched) | `ArtDir/hero/base_lock/blender/HeroBase_Female.blend` |
| Staged result blend (Body_F head welded to the locked collar ring + Face_F + `LOOP_*` groups; sha256 `4450c644…`) | `work/female-micro-f1-head/export/HeroBase_Female_MicroF1_head_STAGED.blend` |
| Staged FBX (Body_F + Face_F, same flags as male: axis −Z/Y, scale 1, normals only, no leaf bones; sha256 `5f6214d9…`) | `work/female-micro-f1-head/export/HeroBase_Female_Body.fbx` |
| Unity proof stage only (not a HeroBase_Female prefab) | `Unity/Assets/Characters/HeroBase/FemaleHeadF1/`, `Unity/Assets/Editor/HeroBaseFemaleHeadProof.cs` |
| Proof | `proof/02_match_f_head.png` (plate \| mesh \| overlay, crops 01–16), `proof/03_unity_f.png`, `proof/female_head_f1/unity/*.png` (17 captures + masks), `proof/female-head-f1-unity-audit.txt`, `proof/female-microf1-checks.json` |
| Rollback of the earlier build | `work/female-micro-f1-head/data/rollback_b3/` |
| Reproduce | `blender -b --factory-startup --python work/female-micro-f1-head/build3.py -- b17` → `assemble.py -- b17 work_assembled.blend` → `verify_asm.py`, `loops_audit.py`, `loops_order.py`, `sil_body.py`, `export_fbx.py` → Unity `GolfArcade.EditorTools.HeroBaseFemaleHeadProof.Run` → `proof02.py`, `proof03.py` |

Install note: only one chat may copy into the ArtDir blend at a time. `assemble.py` splices by the vertex/face ids of the start blend (`data/ring.npz`, `*_vmap.json`), so it must run against the SHA-`200dc3b9…` blend; if another chat installs first, re-run it against the new baseline (ring ids are by position: collar ring z 1.3965–1.4053 m, 30 vertices).

## Numbers
| Item | Value |
|---|---|
| Below the neck weld (BODY_BELOW_NECK) | 12,421 body verts at/below the collar ring **bit-identical** to the start (max diff 0.0 m); non-manifold edges 0; boundary edges 256 = the two 128-vertex eye apertures, which open into the Face_F eyeball shells |
| Tris | START Body_F 44,708 (head zone 19,986, mean edge 4.6 mm) → now Body_F **42,296** (head patch 17,574) + Face_F **5,364** = **47,660** total (< 60 k flag) |
| Edge length (head patch) | mid-face (\|x\|<60 mm, z 1.44–1.60 m, front): mean **3.3 mm**, median 2.9, min 0.65, p95 6.7 · ear zone mean 2.6 mm · lids 1.3–1.4 mm · mouth chains 1.4–2.6 mm · cranial dome mean 12.6 mm (allowed coarser) |
| SIL_BALD (head + neck rows to z = 1.43 m vs `female_body_bald.jpg`) | front **2.13 %**, back **4.68 %** (limit 5.0 %). The back figure's head is ≈ 3–4 % narrower than the front figure's, so 1.3 mm/side (skull) and 1.8 % (ear standoff) were split between them. Whole-figure silhouette not measured (body is not this chat's zone) |
| Head-region silhouette IoU vs the head-detail plate | front 0.976 · left profile 0.971 · right profile 0.970 · 3/4-R 0.952 · 3/4-L 0.813 (that plate is geometrically inconsistent) · top view head-only 0.745 (perspective plate, ≈ 5–8 % magnified) · body-plate front head 0.912 · back head 0.912 |
| Ear silhouette vs plate (front rows 152–202) | within ≈ ±3 mm of the plate, lobe rows included |
| Loops | all 21 `LOOP_*` groups present and audited (closed rings closed, chains contiguous): eye 128-v ring + contiguous upper (52/53) / lower (76/75) lid chains at 1.4 mm; mouth seam 55 / lip_up 35 / lip_lo 25 v (snapped rms ≤ 1.5 mm); ear attach 212 / helix 169 / concha 22 closed rings per side (≈ 1.5–2.7 mm); alae 10-v chains; nostril rings 16 v; jaw→ear 44/46-v edge paths (4 mm) |

## Every FACE_SPEC line

### Overall likeness
| Line | Result | Evidence / note |
|---|---|---|
| OVERALL_LIKENESS | **FAIL (not signed off)** | Reads as a calm stylised young woman and every landmark is now on the plate rows, but the eyes are flat-painted (no gloss depth), brows read heavier/straighter than the plate, the 3/4 lower face reads longer/more angular than the plate's round cheeks, no cheek blush. Judged only from my own captures |
| FEMALE_READ | PASS(eye) | no brow shelf, small nose/mouth, slender locked neck, large eyes |
| BALD_NO_HAIR | PASS | smooth scalp; FBX = Body_F + Face_F only (the start blend carries a hidden, viewport-disabled `Hair_F`, 2,516 v, untouched and not exported) |
| STYLE | PASS(eye) | soft plastic, no pores/noise |
| SKIN_TONE | PASS(eye) | albedo (0.90, 0.70, 0.585); hue ratios match the plate; Unity key light clips R on lit cheeks (plate cheek 212/160/129 vs capture 255/203/172) — exposure of the shared proof rig, not hue |
| DEFAULT_EXPR | PASS | slight closed smile: seam row 218 centre → 213.7 at the corners |
| MALE_NOT_COPIED | PASS | built from the female plates only; no male feature code/params reused |
| COLLAR_OUT_OF_SCOPE | PASS | neck ends at the locked weld |

### 1 Cranial dome
| Line | Result | Evidence / note |
|---|---|---|
| CRANIAL_FRONT | PASS | front sil 2.13 %, IoU 0.976 |
| CRANIAL_PROFILE | PASS | profile IoU 0.971/0.970 |
| CRANIAL_TOP | PASS(eye) | egg, widest slightly aft, nose tip visible, ear tips peek — but from above the ears read as **pointed fins** (plate `06` shows them barely peeking; it disagrees with front plate `01`, which wins) |
| SCALP_SURFACE | PASS | smooth, no ridges/seams |

### 2 Forehead + brow arch
| Line | Result | Evidence / note |
|---|---|---|
| FOREHEAD | PASS(eye) | |
| BROW_ARCH | PASS(eye) | 3 mm soft swell, no shelf |
| BROW_WIDTH | PASS | spans the orbit |

### 3 Eyebrows
| Line | Result | Evidence / note |
|---|---|---|
| BROW_SHAPE | **OPEN** | paint ribbons from the plate's measured edges (mean thickness 11–13 mm = plate 11.3 mm), tapered tail, rounded inner end, plate-measured warm dark brown; still reads a little heavy/straight next to the plate |
| BROW_PLACE | PASS | peak above the outer iris, tail lower |
| BROW_SEPARATION | PASS | clear gap |

### 4 Eyes
| Line | Result | Evidence / note |
|---|---|---|
| EYE_SIZE | PASS | aperture polygon measured on plate `01` |
| EYE_IRIS | PASS | iris dia 32.4 mm / pupil 19.6 mm / catch-light r 2.4 mm up-right, all re-measured on the plate this session (the first build had the iris ~13 % too large) |
| EYE_LIDS | PASS | lash band + wing, thin lower lid line, lid walls, thin soft-crease line above the lash band; no 3D lashes |
| EYE_SOCKET | **OPEN** | eyeball inset 2.2 mm behind the skin with 1.6 mm lid walls; depth reads in 3/4 and profile but is modest |
| EYE_SPACING | PASS | gap 38.8 mm = 0.61 × aperture width — matches the plate; the spec's "one eye width" was wrong and is corrected in `FACE_SPEC.md` |
| EYE_SYMMETRY | PASS | mirrored construction |
| EYE_SCLERA | PASS | white on both sides of the iris |

### 5 Nose
| Line | Result | Evidence / note |
|---|---|---|
| NOSE_HEIGHT | PASS | tip row/profile from the plate; well above the lip |
| NOSE_SIZE | PASS | profile protrusion 25 mm over the base plane (plate) |
| NOSE_BRIDGE_PROFILE | PASS | profile IoU 0.97 |
| NOSE_TIP | PASS(eye) | soft rounded, slightly upturned |
| NOSE_ALAE | PASS | alar chains + painted nostril openings read in 3/4 |
| NOSE_WIDTH_FRONT | PASS | ≈ 1.1 × inner-eye gap (plate ≈ 1.05 ×) |
| NOSE_ROOT | PASS(eye) | no trench |

### 6 Cheeks
| Line | Result | Evidence / note |
|---|---|---|
| CHEEK_FULL | **OPEN** | silhouette matches; in Unity 3/4 the cheek volume reads flatter than the plate's apple cheeks |
| CHEEKBONE_3Q | **OPEN** | gentle swell present, weaker than the plate |
| CHEEK_WIDTH | PASS | widest at cheek/ear level, tapering to the chin (front sil 2.13 %) |

### 7 Ears
| Line | Result | Evidence / note |
|---|---|---|
| EAR_ANATOMY | PASS(eye) | helix rim, antihelix hint, concha bowl, lobe; stylised, tragus weak |
| EAR_SIZE | PASS | outline from the plate profile |
| EAR_STANDOFF | PASS | full outline both sides in front view, within ≈ ±3 mm of the plate (deliberately 1.8 % under it to meet the back plate) |
| EAR_PLACE | PASS | top ≈ brow bottom, bottom ≈ nose base |
| EAR_Y_PAIR | PASS | mirrored |
| EAR_ATTACH | PASS(eye) | back view: soft mound with a long retro-auricular slope (the earlier cliff + horizontal terraces are gone); faint banding remains in grazing light |

### 8 Mouth
| Line | Result | Evidence / note |
|---|---|---|
| MOUTH_SMILE | PASS | seam rows 218 → 213.7 (plate-measured) |
| LIP_VOLUME | PASS(eye) | lower lip larger than upper |
| PHILTRUM | PASS(eye) | shallow 0.9 mm groove + two 0.5 mm ridges, faint |
| TEETH_DEFAULT | PASS | none |
| MOUTH_WIDTH | PASS | seam half-width 35.4 mm = plate seam extent (≈ 0.78 × iris-centre distance; the spec's 0.7 × is approximate) |
| LIP_COLOR | PASS(eye) | muted peach-pink, upper lip one step deeper |

### 9 Jaw / chin
| Line | Result | Evidence / note |
|---|---|---|
| JAW_SOFT_U | **OPEN** | front outline is a rounded U; the 3/4 view still shows a jaw ridge that reads a bit angular |
| CHIN_SIZE | PASS | small, no cleft |
| UNDER_CHIN | PASS | clean plane into the neck (chin-underside crumples removed by implicit fairing) |
| JAW_PROFILE | PASS | profile IoU 0.971/0.970 |
| NO_JOWLS | PASS | |
| NO_STUBBLE | PASS | |

### 10 Neck
| Line | Result | Evidence / note |
|---|---|---|
| NECK_COLUMN | PASS | locked body neck, slender |
| NO_ADAMS_APPLE | PASS | smooth throat in the profile / body-side captures |
| NECK_LEAN | PASS | head carried forward (body-side capture) |
| NAPE | PASS | continuous scalp → neck from behind |
| NECK_WELD | PASS | ring bit-identical, no shelf |

### 11 Proportions
| Line | Result | Evidence / note |
|---|---|---|
| PROP_THIRDS | PASS | brow / nose tip / lip / chin rows all taken from the plate |
| PROP_EYE_SPACE | PASS | 0.61 × eye width (plate) |
| PROP_EAR_ALIGN | PASS | |
| PROP_NOSE_MOUTH | PASS | nose base row 205 → lip top row 213.8, as on the plate |
| PROP_HEAD_NECK | PASS | |
| PROP_HEAD_HEIGHT | PASS | head top → chin 0.279 m = 1/6.1 of 1.704 m |

### 12 Material / construction
| Line | Result | Evidence / note |
|---|---|---|
| MAT_SKIN | PASS | URP Lit, smoothness 0.40 (roughness 0.60), no metal |
| CONSTRUCT | PASS | forms are real head topology |
| NO_STICKER_VOLUME | PASS | all volume is Body_F geometry on a 2–4 mm graded mesh with loops; Face_F is brow / lash / lip / seam / nostril / crease paint (0.3–0.9 mm lift, projected onto the final mesh) and thin eyeball shells |
| NO_HAIR_MESH | PASS | see BALD_NO_HAIR |
| BODY_BELOW_NECK | PASS | 12,421 verts bit-identical |
| SIL_BALD | PASS | 2.13 % / 4.68 % |
| TRI_BUDGET | PASS | 47,660 total, documented above |

### 13 Explicit FAIL list
FAIL_MALE_HEAD, FAIL_HAIR_BOWL, FAIL_DOUGH_CHEEKS, FAIL_LUMP_EARS, FAIL_STICKER_EYES, FAIL_HEAVY_BROW, FAIL_LOW_NOSE, FAIL_BIG_NOSE, FAIL_ADAMS_APPLE, FAIL_STUBBLE, FAIL_SQUARE_JAW, FAIL_COLLAR_MODELED: **none triggered** (FAIL_SQUARE_JAW / FAIL_HEAVY_BROW are borderline in 3/4, see JAW_SOFT_U / BROW_SHAPE). **FAIL_WRONG_LIKENESS: not signed off.**

## Defects found and fixed this round (root causes)
1. `surf_xz` landed off the requested front projection by the tangential part of the displacement (4–8 mm on lips/nostrils) → exact fixed-point solve; seam/lips/nostrils now sit on the plate rows.
2. Chin-underside crumples and the jaw-line scar (snapping the jaw loop) → implicit local fairing, jaw loop left as an edge path; jaw angle / behind-ear / neck-top faired.
3. Ear: serrated rim, spikes, terraced cliff on the back, lobe 8–12 mm too narrow → whole-ear fairing, standoff profile smoothing, posterior root dilation (long soft slope), lobe/top rows fixed to ≈ ±3 mm.
4. Face_F: lips/seam/nostrils/crease ray-projected onto the final mesh (no skin poke-through); iris/pupil/catch-light re-measured; brow colour from the plate.
5. Loop lists tidied (closed rings are simple cycles, lid chains contiguous) so the audits are clean.

## Tried and rejected (all measured against the same plate IoUs)
- Rounder cross-section exponents (n ≈ 2.0–2.4 instead of up to 3.0) to get a more egg-shaped top view: every plate IoU got slightly worse (3/4-L 0.813 → 0.803, 3/4-R 0.952 → 0.941, top 0.745 → 0.735) — kept the original exponents.
- Rounding the ear-flap profile to remove the pointed ear tips seen from above: back silhouette 4.68 % → 5.25 % — rejected.
- Stronger jaw-ridge fairing (λ 0.006): the 3/4 ridge is a cheek-surface feature, not the under-jaw blend; changes were negligible — not adopted.

## Plate notes (also in `face_lock_f/FACE_SPEC.md`)
3/4 plates are perspective renders inconsistent with front/profile; the back figure of `female_body_bald.jpg` is ≈ 4 % narrower than the front; top view `06` hides the ears the front view shows standing 20–28 mm out.

## Next steps if Adnan wants a PASS
Eye-check the OPEN lines (BROW_SHAPE, EYE_SOCKET, CHEEK_FULL, CHEEKBONE_3Q, JAW_SOFT_U) and OVERALL_LIKENESS on `03_unity_f.png`; candidate polish: cheek apple volume (silhouette-neutral, centred x ≈ 45 mm), softer 3/4 jaw ridge, brow tail thinning, eye socket depth. Install into the ArtDir blend / female prefab only after that, one chat at a time.
