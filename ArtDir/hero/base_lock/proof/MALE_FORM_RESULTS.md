GATE: MALE_FORM PASS

Micro1f · male HANDS + UNITY_SKIN · 2026-10-01 · supersedes the Micro1e result. Gate is ≤ 5.0 % bald front/back (never 2 %).

| Line | Result | Evidence |
|---|---|---|
| HANDS | PASS | Both hands re-posed in place (positions only, same topology as Micro1e): thenar + hypothenar bulk, dorsal knuckle ridge and MCP/PIP/DIP pads, finger taper with a joint curl, fingertip pads, fingers closer with readable gaps, thumb rotated into opposition with a fatter root. Not tubes / mitt / claw. `01f_hands_m.png`, Unity hand close-ups in `03_unity_m.png`. Adnan's eye-check vs plate is final. |
| UNITY_SKIN | PASS | `HeroBase_Male` now uses `Male_Form_Skin.mat`: URP Lit opaque, base colour sRGB (0.74, 0.555, 0.435) sampled from `male_head_detail.jpg`, smoothness 0.40 (roughness 0.60, inside the art-bible 0.55–0.70), metallic 0, no texture. Studio: tri-light ambient (sky/equator/ground), soft key 1.05 / fill 0.38 / rim 0.62 / bounce 0.22. Reads warm soft-plastic, not grey clay, not candy. `03_unity_m.png`. |
| SIL_% | PASS | bald_front **3.9700 %**, bald_back **4.2166 %** (Micro1e: 4.0007 / 4.1899). |
| CHIN | PASS (unchanged) | 0 head/neck vertices moved (bit-identical to the Micro1e lock). |
| SHOULDERS | PASS (unchanged) | 4,806 verts in region, 0 moved. |
| WAIST | PASS (unchanged) | 1,832 verts, 0 moved. |
| ARMS_SIDE | PASS (unchanged) | 3,366 verts above the wrist, 0 moved; the 84 wrist-weld ring vertices are bit-identical. |
| FEET_SIDE | PASS (unchanged) | 3,482 verts, 0 moved; 318 vertices below 4 mm, lowest 2.472 mm. |

## What changed

* Start: Micro1e lock `HeroBase_Male_Silhouette.blend` SHA-256 `35766aea1c493bab0ce1bddd6bc7639c4b304630c500c1d654b4336c2e113d90`.
* Only **1,746** vertices moved, all of them hand-shell vertices distal of the wrist weld (freed-slot + appended indices from the Micro1e rebuild); **0** original body vertices moved; the weld rings (42 per hand) are bit-identical; the face list is identical to the lock (24,652 verts / 24,670 polys / 49,304 tris). Max displacement 23.7 mm (thumb tip). No local remesh was needed.
* Method: the Micro1e procedural hand (`work/male-micro1e/hand_build.py`) reproduces the lock bit-exactly from the Hand_4 baseline (`assemble2.py reproduce`); `hand_build2.py` keeps that topology and changes positions only. Smoothing is weighted (weld 1.0 → palm 0.55 → fingers 0.18) so pads and joint bends survive; forearm/weld have weight 0.
* Hand parameters (`work/male-micro1f/final/k5_build.json`): finger radius 13.6 × 18.5 mm, gap 1.6 mm, splay ±3.5°/±1.2°, taper 0.11, PIP/DIP hinges 20/16 mm·s, pads MCP/PIP/DIP 0.20/0.18/0.14 rn, fingertip pad 0.14, knuckle ridge 3.5 mm, thenar 9 mm / hypothenar 3.5 mm, palm +3 mm thick at the knuckles, thumb radii 24/22.5/19.5/16.5 mm with the tip at (a 0.060, n 0.042, w 0.098) in the hand frame.
* Unity: `Unity/Assets/Editor/HeroBaseMaleFormProof.cs` — new skin material + studio lighting constants (`SkinColor`, `SkinSmoothness`, tri-light ambient, light intensities). Importer unchanged: scale 1, file scale 1, compression off, normals imported; one renderer, no hair, no texture. Triangle contract 49,304.

## Known remaining differences (not in this pass)

* The plate hand reads more mitt-like (fingers fused further toward the tips, palm faces the camera more); the mesh keeps readable gaps as the goal asks. Fingertips are still rounder/simpler than the plate's.
* Face is a blank blockout (nose low, cheeks/ears wide); scalp/nape untouched.
* `Tools/HeroBase/export_male_form.py`, `verify_male_form.py`, `verify_male_form_d.py` still hard-code the Hand_4 counts and fail by design.

## Files and hashes

* Blend: `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `57a08123bd714e58d744e2a59780e6aacb81dd71879f3ffeb14b31725eee6d73`
* FBX (source = Unity import): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `db600eb1fd9eec00b5077da2a9ed49fe489e4619fffb4b38b97df91617f962cc` (Blender re-import max vertex error 0.00028 mm, 49,304 tris, height 1.702187 m)
* Unity prefab `HeroBase_Male.prefab` SHA-256 `f65086bee4edcb9fca339ee58ba4f81a7a851e9428e95757f3354c43738a3338`; material `Unity/Assets/Characters/HeroBase/Materials/Male_Form_Skin.mat` SHA-256 `5a253517a3ca18d7db42725d0cf3073fa4d45a25c5dcea758242cc30cf29776c`
* Unity audit `male-form-unity-audit.txt`: MALE_FORM_SAVED_UNITY_VERIFY=PASS, 13 captures + masks in `male_form_d/unity/`.
* Prior state for rollback: `work/male-micro1f/baseline/` (Micro1e blend, FBX, 03_unity_m.png, MALE_FORM_RESULTS.md).
* Machine checks: `male-micro1f-checks.json`.

## Proof

* `01f_hands_m.png` — both hands palm/back/front/side/3-4, before vs after, plate vs mesh at plate framing (front, back, side).
* `03_unity_m.png` — refreshed Unity captures: five body views, skin read, hand close-ups, authority plates.

## Reproduce

`work/male-micro1f/`: `hand_build2.py`, `assemble2.py` (`reproduce` / `build k5 '<json>'`), `live_hand.py` (live Blender renders), `compose_01f.py`, `compose_03.py`; shared tooling in `work/male-micro1e/` (`finalize_blend.py`, `sil_body.py` + `sil_gate.py`, `audit_final.py`, `export_fbx.py`).
