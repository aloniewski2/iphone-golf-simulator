GATE: MALE_HEAD FAIL
OVERALL_LIKENESS / NOSE_WIDTH_FRONT / NOSE_ALAE / CHEEK_WIDTH / EAR_ANATOMY / SCALP_SURFACE

Micro1g · male FACE LOCK · 2026-10-01 · Body_M bald head sculpt + thin `Face_M` feature layer. Start: Micro1f lock `57a08123…`. Adnan's eye-check vs `face_lock/` plates is final; the lines below are my binary calls against FACE_SPEC.md, judged on the **Unity** captures (`03_unity_m.png`), not the flattering Blender clay.

## Gate lines (FACE_SPEC.md)

| Section | Line | Result | Evidence |
|---|---|---|---|
| Overall | OVERALL_LIKENESS | **FAIL** | Landmarks sit where the plate has them (`02_match_m_head.png`), but at arm's length the Unity front/3-4 (`03_unity_m.png`) still read as a blockout head with features applied: narrow ridge nose, flat midface planes, surface ripples. Not face_example, but not yet the plate male. |
| | BALD_NO_HAIR | PASS | no hair objects; scalp is Body_M. |
| | STYLE | PASS | smooth soft-plastic forms; no pores, no photoreal, no dough. |
| | SKIN_TONE | PASS | warm tan (Blender preview `#D9A37E`; Unity `Male_Form_Skin` sRGB .74/.555/.435, smoothness .40). |
| | DEFAULT_EXPR | PASS | closed-mouth smile, corners +3.8 mm. |
| | SHIRT_OUT_OF_SCOPE | PASS | nothing modelled below the neck. |
| 1 Cranial | CRANIAL_FRONT / CRANIAL_PROFILE / CRANIAL_TOP | PASS | cranium untouched except ≤0.6 mm fairing (114 verts); overlay row 06 top oval and ear tips match; profile dome matches 02/03. |
| | SCALP_SURFACE | **FAIL** | smooth on the dome, but the mid-face (cheeks beside the nose, under the eyes) shows low-frequency ripples from the 9.5 mm vertex spacing under the displacement fields — visible in the Unity front capture. |
| 2 Forehead | FOREHEAD / BROW_SHELF / BROW_WIDTH | PASS | two soft brow lobes (+3.2 mm) over the orbits, glabella +1 mm, temples −0.8 mm; no cliff. |
| 3 Eyebrows | BROW_SHAPE / BROW_PLACE / BROW_SEPARATION | PASS | thick dark ribbon brows on the shelf, inner x ±0.023 / outer ±0.082, arch +7 mm, 46 mm gap at the root. |
| 4 Eyes | EYE_SIZE / EYE_IRIS / EYE_LIDS / EYE_SOCKET / EYE_SPACING / EYE_SYMMETRY | PASS | eyeball ellipsoid 30×18×12 mm seated in a carved socket (skin ≥3 mm behind the ball, carve −3 mm); almond opening 47×20 mm, iris Ø21.6 mm brown, pupil, single catchlight; lid ribbons; inner corners ±0.029 (one eye width); mirrored. |
| 5 Nose | NOSE_HEIGHT | PASS | tip z 1.545 (plate row 199), root z 1.608 — plate thirds, tip not dropped. |
| | NOSE_BRIDGE_PROFILE / NOSE_TIP / NOSE_ROOT | PASS | straight ridge (profile y −0.149 → −0.1715) matches 02/03; tip ball +1.2 mm; soft root, no trench. |
| | NOSE_WIDTH_FRONT / NOSE_ALAE | **FAIL** | the ridge/width profile makes a thin wedge; in the Unity front view the nose reads ~half the plate width and the alae bulbs (+2.2 mm) do not read as wings with nostril openings in 3/4. Needs a proper nose block (bulb + alar lobes) rather than a displacement ridge. |
| 6 Cheeks | CHEEK_ATHLETIC / CHEEKBONE_3Q | PASS | no balloon under the ears, no hollows; cheekbone hint +2.2 mm at z 1.576. |
| | CHEEK_WIDTH | **FAIL** | zygomatic/jaw width is still 5–8 mm per side wider than the head-detail plate (inherited Micro1e/1f jaw, left untouched to protect the chin lock and silhouette gate). |
| 7 Ears | EAR_SIZE / EAR_PLACE / EAR_Y_PAIR / EAR_ATTACH | PASS | lump scaled to 0.70, disc y −0.059..0.000, z 1.525..1.592 (plate: ear top ≈ brow, bottom ≈ nose base); mirrored; attached. |
| | EAR_ANATOMY | **FAIL** | helix/antihelix are tube rims laid on the lump (concha −5 mm); from the front and 3/4 the ear still reads as a bean with a wire on it, not a readable helix + concha. Needs ear geometry (helix shell, concha bowl, lobe) instead of rims. |
| 8 Mouth | MOUTH_SMILE / LIP_VOLUME / PHILTRUM / TEETH_DEFAULT / MOUTH_WIDTH | PASS | seam z 1.497 ±38.5 mm, upper lip strip thinner than lower, lip mounds +3.4/+3.8 mm, philtrum −1.2 mm, no teeth. |
| 9 Jaw/chin | JAW_U / CHIN_CLEFT / UNDER_CHIN / JAW_PROFILE / NO_JOWLS | PASS | chin band (z<1.47) moved ≤0.44 mm (51 verts, fairing) — Micro1e chin plane kept; cleft −1 mm hint; no jowls. |
| 10 Stubble | STUBBLE_ROOM / STUBBLE_ZONE | PASS | jaw/chin silhouette unchanged; no sculpted hair. |
| 11 Neck | NECK_COLUMN / CHIN_LOCK / NAPE | PASS | 0 vertices moved outside the head zone; nape continuous. |
| 12 Proportions | PROP_THIRDS / PROP_EYE_SPACE / PROP_EAR_ALIGN / PROP_NOSE_MOUTH / PROP_HEAD_NECK | PASS | all landmarks placed from the plate rows (brow 146 / eye 158 / tip 199 / base 206 / mouth 235 / chin 272), overlay row 01. |
| 13 Material | MAT_SKIN / CONSTRUCT / NO_HAIR_MESH / BODY_BELOW_NECK | PASS | URP Lit skin + 10 flat face slots; primary forms in Body_M, features as one welded-clean layer object; no hair; 0 body/hand vertices moved. |
| 14 | FAIL_* tags | none | no hair bowl, no pink shirt, no dough cheeks, no lump ears, no sticker eyes, no pinched brows, nose at plate height. |
| Regress | HANDS / BODY_BELOW_NECK | PASS (unchanged) | hands 0 moved; `moved_outside_head = 0`. |
| | SIL_% | PASS | bald_front **4.0508 %**, bald_back **4.3166 %** (Body_M); with Face_M 4.0192 % / 4.2782 %. Micro1f was 3.9700 / 4.2166. |

## What changed
* Body_M: **880** head vertices moved (z > 1.43, |x| < 0.14), mean 2.1 mm, max 24.6 mm (nose tip). Bands: cranium 114 (≤0.6 mm), brow/eyes 235 (≤22.5 mm socket carve), nose/cheeks 263, mouth 216, chin 51 (≤0.44 mm). Topology unchanged (24,652 verts / 49,304 tris).
* New object `Face_M` (3,582 verts / 6,876 tris, 10 material slots: Skin, Brow, Sclera, Iris, Pupil, Catch, LidLine, Lip, Seam, Nostril): eyeballs, lids, brows, nostrils, lips, seam, ear helix/antihelix rims. All ray-cast onto the sculpted head, 0.5–3 mm proud.
* Method: plate landmarks → displacement fields (`head_lock.py`), nose = cheek-plane flatten + ridge/width profile from the plate, eye sockets carved to the shared eyeball ellipsoid, ears = scaled lump + concha/helix relief. Face features built by `face_layers.py`.
* Unity (`HeroBaseMaleFormProof.cs`): prefab now carries two renderers (Body_M + Face_M); face materials created per slot by name (`Male_Face_<slot>.mat`); 4 head close-up captures added.

## Why FAIL, and what it takes to pass
* The head has 9.5 mm vertex spacing (≈2,000 verts above the neck). Displacement fields at that density give the right landmark positions (brow/eye/nose/mouth all land within the plate overlay) but cannot form a nose bulb, alar lobes, a concha bowl or clean cheek planes — those need local topology: a nose block and ear shells either sculpted on a subdivided head region or built as welded parts (same approach Adnan approved for the hands in Micro1e). That is a documented local-remesh step, not done here because it changes Body_M topology above the neck; it needs an OK.
* Lip ribbons are flat colour strips; the plate lips have rounded volume.
* Catchlight is a fixed white disc (not view-dependent).
* Everything else on the FACE_SPEC list is in place and measured (see table); the chin lock, hands, body and silhouette gate are untouched.

## Files and hashes
* Blend: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `c6b66edefc902c38c8696d7d5b25feb846f58c60c3edb21cd7a465d3c8dfd0d4`
* FBX (Body_M + Face_M): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `b2caa3cea82f403718b12b8d46dba80f07c55b505eb7adad8f9b25da520a540f` (re-import max error 0.00028 mm both objects)
* Unity audit: `male-form-unity-audit.txt` (see FACE_SLOT lines) + captures in `male_form_d/unity/` (face_front, face_threequarter, face_profile, face_threequarter_back + body views).
* Prior state (rollback): `work/male-micro1g/baseline/` (Micro1f blend `57a08123…`, FBX `db600eb1…`, `HeroBaseMaleFormProof.cs.micro1f`).
* Checks: `male-micro1g-checks.json`.

## Proof
* `02_match_m_head.png` — six head-detail panels: plate | mesh | 50 % overlay (mesh bbox-aligned to the plate head).
* `03_unity_m.png` — Unity head close-ups + plates + body context.

## Reproduce
`work/male-micro1g/`: `head_lock.py` (sculpt fields + landmarks), `face_layers.py` (Face_M), `finalize_head.py` (lock → sculpt → Face_M → blend + FBX + re-import check), `audit_head.py` (regression + gate renders), `run_live.py` (live Blender iteration), `compose_match.py`, `compose_03g.py`.
