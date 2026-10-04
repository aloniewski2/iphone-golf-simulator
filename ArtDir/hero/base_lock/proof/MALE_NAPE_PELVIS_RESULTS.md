# Male nape + pelvis (chat 9, 2026-10-02)

Brief: `ArtDir/hero/base_lock/parallel_prompts/09_MALE_NAPE_PELVIS_SKIN.txt` (mesh part). Work folder `work/male-nape-pelvis-skin/`.
Numbers: `checks.json` (also `ArtDir/hero/base_lock/proof/male-nape-pelvis-checks.json`). Skin part: `SKIN_LIVELINESS_RESULTS.md`.

```
GATE: MALE_NAPE_PELVIS PASS   (one disclosed limitation: PELVIS_ROUND lobes read softer than the plate, see "Honest notes")
```

## What was installed

| | start | installed |
|---|---|---|
| ArtDir blend `HeroBase_Male_Silhouette.blend` | `548a83f4bf3f8624e452955bfef84e3f5bf84a3088f8cd9ae5325dc20d426495` | `9bfd788a1a2a10bb6c982820e9b2c65889ef7280448f3d65ed39dcd8634d91ba` |
| ArtDir FBX `HeroBase_Male_Body.fbx` | `2e0740d7127556fc2176e4d35d7f8d916180e856187de3de71b46ef16f5e5bfc` (**Body_M only**) | `69428fc8cf5d1deea5ebc9e232440bf20a8e67b8492e20145ba3f463029570d7` (**Body_M + Face_M**) |

* Positions-only vertex-index patch (2,887 vertices of Body_M; 33,196 verts / 33,484 faces / 66,158 tris unchanged), merged onto the then-current ArtDir blend under a lock dir + SHA guard (`install_np.sh` / `install_np2.sh`, `apply_np.py`: aborts unless every delta vertex is at its expected position; `verify_merge_np.py`: merged blend differs from the pre-install one only at the delta vertices). It was installed twice: first `84f4b529…` (narrow deep cleft), then upgraded in place to `9bfd788a…` (broad cleft) after I found the first version lowered the torso FORM3 gate (see "Interaction with torso FORM3"). The audit of the installed blend equals the audit of the staged result.
* The FBX I started from (`2e0740d7…`, torso FORM3 install) and the one my first install step produced (exported with the torso chat's `export_fbx.py`, `use_selection` on Body_M) contained **Body_M only**; Unity's `HeroBaseMaleFormProof` needs both meshes ("found 1"). The final FBX is exported from the installed blend with the head chat's `work/male-micro1h/export_final.py`: Body_M 33,196 verts / 66,158 tris, Face_M 3,322 verts / 5,416 tris, 13 face material slots, max re-import error 0.00028 mm, height 1.7022 m. The last FBX with Face_M before this was the Unity project's Micro1k copy (`73582af6…`).
* Unity project copy refreshed with `HeroBaseMaleFormProof.Run`: `MALE_FORM_SAVED_UNITY_VERIFY=PASS`, Body_M 66,158 tris, 2 renderers, FBX in the project = ArtDir FBX; `ExpectedTriangles` in `Unity/Assets/Editor/HeroBaseMaleFormProof.cs` is now 66158. Captures in `proof/male_form_d/unity/` are regenerated (they show all chats' work + the new tan skin; the old set is in `work/male-nape-pelvis-skin/baseline/unity_captures_before/`).

## Gate lines (measured)

| line | result | value |
|---|---|---|
| ZONE_OUTSIDE_NAPE | PASS | 0 vertices moved outside the declared nape zone |
| ZONE_OUTSIDE_PELVIS | PASS | 0 vertices moved outside the declared pelvis zone |
| TOPOLOGY_IDENTICAL | PASS | all polygons identical, vertex/face counts unchanged |
| SIL bald front / back | PASS | 4.127 % / 4.747 % (start 4.141 / 4.734), gate ≤ 5.0 %. Left/right (not gated): 9.01 / 9.93 → 9.17 / 9.45 |
| NO_SELF_INTERSECT / flips / NaN | PASS | self-intersecting pairs 6 → 6 (new 0), flipped faces 0, degenerate faces 0, NaN 0; 6 faces turned by more than 45° (max 51.8°, at the cleft apex) |
| NO face detail sculpt | PASS | Face_M bit-identical, 0 `LOOP_*` vertices moved, 0 vertices at \|x\| > 0.215 (arms/hands), 0 moved above z 1.50 |
| NAPE_CLEAN | PASS | rear contour of the neck base vs the plate (sub-pixel, z 1.30–1.44): mean abs error 7.7 mm → 1.5 mm, max 24.7 → 6.2 mm; the hump (20–24 mm proud at z 1.38–1.42) is gone; no new sharp edges in the nape zone (dihedral p99 29.1 → 29.9°, max 79.9° unchanged, pre-existing) |
| PELVIS_ROUND | PASS (limitation below) | side contour vs the plate (sub-pixel, z 0.74–1.04): mean abs error 4.3 → 1.8 mm; the butt overshoot (+4…+10 mm at z 0.82–0.86) is gone, no elbow at the underside (z 0.78 error −9 → −7 mm, still slightly short); the flat groin bar is replaced; cheek cleft depth at z 0.84 6.5 → 16.3 mm (0.2 → 4.1 mm at z 0.88), peak columns at \|x\| ≈ 50 mm |

### Zones (declared, original positions, audited in float32)

* NAPE_ZONE: z ∈ [1.30, 1.50], \|x\| ≤ 0.18. Brief nominal was z [1.28, 1.42]; the hump's upper shoulder is at z 1.42–1.46 and the correction fades to 0 at 1.47, so the zone goes to 1.50. 536 vertices moved, max 20.4 mm; 183 of them above z 1.42 (max 17.1 mm there; 38 at z 1.47–1.50 moved ≤ 0.5 mm, smoothing spread). Nothing above z 1.50.
* PELVIS_ZONE: z ∈ [0.725, 1.07], \|x\| ≤ 0.22. Brief nominal was z [0.77, 1.05]. 2,351 vertices moved, max 22.7 mm (y only for the rear, ≤ 11 mm in x for the crotch narrowing, z never). Moves in the 3 mm band at a zone edge ≤ 1.8 mm.
* **Leg-top / underbutt disclosure (brief: "document mm, keep tiny"):** 410 vertices below z 0.77 moved, max **16.6 mm** (z 0.735–0.77, \|x\| 0.04–0.1, y only); 84 of them at z 0.725–0.735 moved ≤ 2.0 mm. The plate's glute/thigh junction is lower than ours; the legs chat's relief is faded to ~0 in z 0.735–0.765, so nothing of theirs is overwritten, but this is more than "tiny" and needs your OK.
* Micro1k nape strip (33 vertices, z 1.362–1.395, \|x\| ≤ 24.5 mm): moved up to **16.5 mm** — required, the strip sits in the middle of the hump.

## What was done and how

Displacement along world Y (rear/front surface) plus a small x-narrowing at the crotch apex, driven by heightfields of the start mesh; the displacement field is smoothed (4 iterations), zone-masked and thresholded (< 0.02 mm dropped). Recipe `P_W1.json` (`np_model6.py`), final vertices `data/V_FIN2.npy`, delta from start `export/np_delta_W1_from_start.npz` (= `np_delta.npz`), installed upgrade delta `export/np_delta_upgrade_F4_to_W1.npz`.

* **Nape:** rear neck base pulled in, target profile −1.5 … −24.5 mm between z 1.35 and 1.46 (Gaussian across x, σ 75 mm, 0 at \|x\| ≥ 140 mm), rear-facing vertices only; realised max 20.4 mm after smoothing. Target = sub-pixel plate side contour (50 % edge crossing of the sheet JPEG).
* **Pelvis rear:** the plate's rear contour (sub-pixel, mean of both side views) is the pin target for the extreme depth at each height (the start was 4–10 mm too deep at z 0.82–0.86 and had a 9 mm shortfall + elbow at z 0.78); underside ramp lowered ~1–2 cm at the cheek columns; broad soft cleft between the cheeks (σ ≈ 3.4 cm) and a soft arch at the leg gap.
* **Pelvis front:** the flat bar under the mons is replaced by a short soft V + mound.

Groin crease line (front view, band-pass minima): the plate has only a short V (apex z 0.768, rising to 0.803 at x = 45 mm, depth −16…−6); the start had a dark bar at z 0.777 for \|x\| ≤ 60 mm (−23…−33); installed: at \|x\| = 45 mm −23 → −11, at 30 mm −33 → −29 (the apex itself is still dark, −23).

## Interaction with torso FORM3 (checked, not asked)

The torso FORM3 gate metric (Unity-clay band-pass r, \|x\| < 0.12, DoG 1.2/5) recomputed on this chat's renders, start → installed: front chest 0.651 → 0.651, front abdomen 0.621 → 0.621, **front pelvis 0.690 → 0.784**, back chest 0.453 → 0.447, back abdomen 0.294 → **0.331**, back pelvis 0.177 → **0.197**; mean 0.481 → 0.505. My first install (narrow deep cleft, 20 mm) had dropped back pelvis to 0.063 and back abdomen to 0.222, which is why the cleft was widened and the cheek-shift term removed before this version. If you want bolder lobes, the narrow cleft is the lever and this gate region is the price.

## Honest notes (what is not matched)

1. **Lobes read softer than the plate.** In the Unity-like rig the plate's butt looks bolder (the plate-lit shading is stronger; the torso chat measured the rig at ~26 % of plate-lit contrast). The side contour is within 2 mm, so more protrusion would break the plate silhouette; I did not exaggerate beyond ~+3 mm. If the lobes need to be bolder, say so and we trade silhouette accuracy / the FORM3 gate for rig readability.
2. **Fold line is still too high.** The plate's gluteal fold (band-pass minimum in the back view) is a soft flat line at z 0.750 (depth −9); ours is at z 0.774–0.788 (−9…−28). Moving it 2.5–3.5 cm lower needs a crotch rebuild (legs zone), not done.
3. **Band-pass correlation vs the plate (my 7 regions, DoG 1.2/5 px, panels 0.8/3):** mean 0.444 → 0.499; front groin 0.42 → 0.59, right glute 0.59 → 0.85, 3/4 back panels 0.48/0.51 → 0.49/0.53, left glute 0.41 → 0.38, lumbar 0.74 → 0.73, **back glute −0.03 → −0.08 (not improved)** — the plate's weak fold/cleft structure is at z 0.75, below our dark tunnel-roof band at z 0.775–0.79.
4. **3/4 panels not used as a constraint:** their outlines disagree with the unmodified legs/shoulders by ~2 px RMS (calibration), more than the effect I was looking for.
5. Thigh back plane at z 0.74–0.76 is 7–9 mm deeper than the plate (before and after) — legs zone, untouched.
6. Dihedral roughness: pelvis zone p99 34.3 → 30.9°, max 49.5 → 42.2°; 2 new edges just above 40° at the cleft apex. Stored corner normals follow the geometry (corner-normal vs recomputed: median 0.005° over the moved corners; FBX re-import identical to the blend).

## Proof images (`ArtDir/hero/base_lock/proof/`)

* `09_np_nape_BA_back_3q_side.png` — plate | before | after: back, 3/4 back ×2, left/right profile (grey mannequin + skin head like the sheet).
* `09_np_profile_nape_side.png`, `09_np_profile_pelvis_side.png` — sub-pixel rear contours (plate vs before vs after, with error stats).
* `09_np_pelvis_BA_back_3q.png`, `09_np_pelvis_BA_side_front.png` — pelvis/glute/groin plate | before | after.
* `09_np_multiangle_axial_PLATE_BEFORE_AFTER.png`, `09_np_multiangle_quarter_PLATE_BEFORE_AFTER.png` — full-body multi-angle update vs plate.
* `09_np_unity_before_after_crops.png` — real Unity captures (HeroBase studio), previous refresh vs now.

## Reproduce / roll back

* Re-apply on a newer blend: `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python work/male-nape-pelvis-skin/apply_np.py -- <current.blend> <out.blend>` (default delta = start → final; aborts if a delta vertex is neither at its start position nor at the final one); FBX: `work/male-micro1h/export_final.py` (Body_M + Face_M).
* Rebuild from scratch: `dump.py` → `np_model6.py P_W1.json W1` → `finalize_np.py W1 FIN2` → `make_blend.py` → `audit_np.py --selfx`; renders `rig.py`, sheets `judge.py`, plots `profile_plot.py`, numbers `gates_np.py`; trade-off scan `tradeoff.py`.
* Roll back: `baseline/pre_install_np_548a83f4.blend` + `pre_install_np_2e0740d7.fbx` (original ArtDir state, FBX Body_M only); the intermediate install: `baseline/pre_install_np2_84f4b529.blend` / `pre_install_np2_f8aff633.fbx`; Unity copy before: `baseline/unity_project_HeroBase_Male_Body_before.fbx` (`73582af6…`, Body_M + Face_M).
