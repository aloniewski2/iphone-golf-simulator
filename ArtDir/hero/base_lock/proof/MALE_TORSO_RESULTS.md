GATE: MALE_TORSO PASS

Male torso (chest, abs, back, waist, pelvis shell) · 2026-10-01 · parallel chat 2/8 · positions-only edit on the existing topology. Gate for silhouette is <= 5.0 % bald front/back.

| Line | Result | Evidence |
|---|---|---|
| ZONE_OUTSIDE_MOVED=0 | PASS | moved outside zone 0; head/neck, legs, feet, hands, arms = [0, 0, 0, 0, 0, 0, 0]; Face_M identical |
| TOPOLOGY_IDENTICAL | PASS | 30296 verts / 60358 tris unchanged, vertex groups identical, boundary/non-manifold edges 92/2 as baseline, height 1.702187 m unchanged |
| MESH_QUALITY | PASS | flipped faces 0; zone edges >55 deg 49 -> 2; border displacement max 0.26 mm; max edge 25.1 -> 20.4 mm |
| SIL_FRONT_BACK<=5.0% | PASS | front 4.2992 % (baseline 4.2978), back 4.5781 % (baseline 4.5897) |
| TORSO_OUTLINE_NOT_WORSE | PASS | half-width rows >1 px off plate (z 0.80-1.36): front 1 -> 1, back 6 -> 6 |
| TORSO_DEPTH_vs_SIDE_PLATES | PASS | front-to-back depth deficit vs left/right plate silhouettes, z 0.92-1.30: rms 20.2 -> 2.5 mm, mean +20.0 -> +0.7 mm (1 plate px = 5.8 mm) |
| PELVIS_SHELF_REMOVED | PASS | pelvis band z 0.77-0.90: edges >55 deg 43 -> 0, max dihedral 122 -> 50 deg |
| FORM_vs_PLATE_RELIEF | PASS | band-passed shading correlation with plate, 6 torso regions: mean +0.031 -> +0.146; front chest/abdomen/pelvis -0.08/+0.09/+0.02 -> +0.14/+0.24/+0.23 |
| FBX_REIMPORT | PASS | FBX re-import: 60358 tris, max vertex error 0.00028 mm, height 1.702187 m |

## What changed

* Start: `HeroBase_Male_Silhouette.blend` SHA-256 `68de53ca729ff04d366377e6df302952082abfb8e5621c524d39468c39f630cf` (Micro1i). ArtDir still held that SHA when this chat installed (nobody else had installed).
* **6817 of 30296 vertices moved, every one inside the declared torso zone** (zone = z 0.77..1.40, |x| <= 0.205, ramping to 0.28 over z 1.20..1.24 for the shoulder ball: 7617 vertices). Face list, vertex groups (all `LOOP_*`), UVs, materials, Face_M, cameras and empties are untouched. Max displacement 22.4 mm (glutes / pecs), mean of moved 5.7 mm, torso volume +3.2 L.
* Method: analytic displacement fields (pure front/back moves weighted by depth fraction, so the front/back outline cannot change) + Taubin fairing; no remesh, no new vertices. Scripts and parameters: `work/male-parallel-torso/sculpt3.py`, `data/params_final.json`.
  * **Depth** from the plate side silhouettes (the baseline torso was ~20 mm too shallow front-to-back): envelope +7..8 mm at the front and +5..10 mm at the back over z 0.93..1.24, with a small waist hollow at the back (z 1.0), tapering to 0 at the neck base and at the leg-top ring.
  * **Chest**: two pectoral masses (centre x +-0.085 m, z 1.238, +10 mm) with a defined lower edge (groove 3.0 mm along z 1.17..1.19) and a soft sternum valley (1.6 mm), upper-chest/clavicle ridge 0.6 mm, lower-rib fullness 4 mm.
  * **Abdomen / pelvis**: soft belly 4 mm, oblique bulges at x +-0.132 m z 0.985 (3.5 mm), pubic mound 5 mm, curved groin crease from the hip to the crotch (3.5 mm); abs kept very soft (<= 0.8 mm) because the plate barely shows them.
  * **Back**: trapezius 3 mm, scapulae 2 mm, almost no spine groove (0.4 mm), two glute lobes at x +-0.076 m z 0.866 (+19 mm, long upper slope / shorter underside) with a soft cleft that fades out by z 1.0.
  * **Pelvis shelf**: the baseline had a hard overhanging shelf at z ~0.79 whose geometry was torn (shard-like faces). 49 zone edges had a crease > 55 deg (worst 122 deg; 43 of them in the pelvis band); now 2 (worst 73 deg, both are the natural armpit crease), pelvis band 0. Done with a heavy Taubin pass on the band z 0.776..0.88 over the pinned leg-top ring; the baseline's 14-25 mm irregular edges there became regular (zone max edge 25.1 -> 20.4 mm).
  * Fairing passes: inner torso (10 it.), pelvis band (60 it.), upper-back band z 1.20..1.36 (14 it.). Zone border displacement max 0.26 mm (side/arm), 0.002 mm (legs), 0.000 mm (neck): no step against the untouched parts.

## Numbers

* Silhouette bald front **4.2992 %**, back **4.5781 %** (baseline 4.2978 / 4.5897); with Face_M identical. Left/right profile masks (not gated): 10.35/12.00 -> 9.37/9.96 % (dominated by arms/hands/head).
* Torso depth deficit vs the left/right plate silhouettes, z 0.92..1.30 (20 rows): mean +20.0 -> +0.7 mm, rms 20.2 -> 2.5 mm, worst row 5.8 mm (one plate pixel = 5.8 mm).
* Form fidelity: Pearson r of band-passed shading (DoG 1.2/5 px, interior |x|<0.12) between the plate and a clay render at plate framing. Baseline -> edit: front chest -0.08 -> +0.14, front abdomen +0.09 -> +0.24, front pelvis +0.02 -> +0.23, back upper -0.09 -> +0.05, back lower +0.26 -> +0.21, back glute zone -0.01 -> -0.00. Mean of the six +0.031 -> +0.146. These are relative numbers (the plate is an AI render under unknown lighting): absolute r is still low, and two back regions did not improve.

## Known remaining differences (not in this zone)

* **Legs / crotch (chat 4)**: the leg-top ring is pinned at z 0.77, so under the glutes the plate's gluteal fold arcs (z ~0.75) are missing, the buttock still overhangs the thigh by ~25 mm, and the gap between the thighs has a flat squared roof (the plate has a narrow V). The plate side silhouettes also want the thigh tops ~30 mm deeper at z 0.78. A few torn shards remain on the inner thighs below z 0.77 (bit-identical to the baseline).
* Back glute zone does not improve on the shading metric (-0.01 -> -0.00; DoG 2/8: -0.14 -> -0.14) for the reason above; lower back (+0.26 -> +0.21) got slightly worse by that metric although its depth now matches the side plates.
* Outline rows more than 1 plate pixel off in z 0.80..1.36: front 1, back 6 (same as baseline: armpit rows where the plate arm sits closer to the torso). Not changed on purpose: the outline was already inside the 5 % gate.
* Shoulders, deltoids, arms, hands, neck, head, legs, feet: untouched (bit-identical).
* **Unity not refreshed**: the Unity project keeps its own copy at `Unity/Assets/Characters/HeroBase/Models/HeroBase_Male_Body.fbx`; no import path was assigned to this chat and a batch import could collide with the other chats. After the merge, copy `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` there and re-run the proof capture. Blender previews use the same lights/skin as the Unity proof.

## Files and hashes

* Normals: the mesh carries a custom-normal attribute stored relative to the automatic normal frame; it follows the edited geometry (evaluated corner normals match the vertex normals to 2e-5), and in the exported FBX every corner normal equals the smooth normal of the new geometry (dot 1.0000; mean change vs the previous FBX 3.5 deg inside the zone, <= 0.6 deg outside, from border neighbours). Unity will shade the new forms.
* Blend (installed in ArtDir, = staged): `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend` SHA-256 `b9cc85967819b5fc31d323c0aa806c925a9e2e849b878e31c1866a2a8404909c`
* FBX (installed, = staged; same export flags as every earlier install, Body_M only): `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` SHA-256 `8646290043ef08e67bf22b1bd78f22c5ae8dc8f228ce41ec9d81b349899ecbff` (60358 tris, re-import max error 0.00028 mm, height 1.702187 m; exporting the start blend with this pipeline reproduces the previous FBX to 0.0 mm)
* Start / rollback: `work/male-parallel-torso/baseline/pre_install/HeroBase_Male_Silhouette.blend` (`68de53ca...`) and `HeroBase_Male_Body.fbx` (`81276df0...`). Rollback = copy those two files back.
* Machine checks: `proof/male-torso-checks.json` (zone categories, topology, integrity, silhouette, depth, form correlation, gates, FBX).
* **Merge help for the other chats**: ArtDir blend SHA is now `b9cc8596...`. Re-read it before your start. To put the torso onto a newer blend: `blender -b --factory-startup --python work/male-parallel-torso/apply_torso.py -- <current.blend> <out.blend>` (moves only the torso-zone vertices listed in `export/torso_zone_delta.npz` (6,822 indices; 5 of them change by less than float32 resolution, hence 6,817 differ in the saved blend), aborts if those vertices were edited since the start SHA).

## Proof

* `proof/02t_torso_proof_m.png` — front/back: plate | baseline | torso edit, real render and local-contrast relief, with the gate numbers.
* `proof/02t_torso_proof_q3_m.png` — four 3/4 views against the multi-angle plate sheet.
* `proof/02t_torso_proof_side_m.png` — left/right silhouettes vs the plate masks, baseline vs edit.
* `proof/02t_torso_proof_pelvis_m.png` — pelvis shelf / glute / groin under raking light, baseline vs edit.
* `proof/02t_torso_zone_m.png` — displacement heat map (grey = bit-identical): only the torso moved.

## Reproduce

`work/male-parallel-torso/`: `sculpt3.py` (fields + fairing) -> `data/V_final.npy`; `make_blend.py` (positions into a copy of the start blend); `audit_torso.py` + `finalize_checks.py` (zone, integrity, gates); `export_fbx.py`; `rt.py` / `rake.py` / `wire.py` (renders), `compose_proof.py`, `zonemap.py`; `install.sh` (SHA-guarded install with lock); `apply_torso.py` (rebase onto a newer blend).
