GATE: MALE_ARMS_HANDS PASS

Male arms + hands (shoulder-ball boundary through fingertips, both sides) · 2026-10-01 · parallel chat 3/8 · positions-only edit on the existing topology. Silhouette gate is <= 5.0 % bald front/back. Adnan's eye-check is final: the plates disagree with each other on the hands (see "Known remaining differences"), so this is a measured and visual PASS, not a likeness certificate.

| Line | Result | Evidence |
|---|---|---|
| ZONE_OUTSIDE_MOVED=0 | PASS | 2,502 of 30,296 vertices moved, all inside the declared arm zone: hand shell 1,746 + wrist band 756. Moved in head/neck, torso+pelvis, legs, feet, shoulder ball + upper arm (arm-axis t < -0.03) = 0 / 0 / 0 / 0 / 0; whole body outside the arm zone: 0 |
| TOPOLOGY_IDENTICAL | PASS | 30,296 verts / 60,358 tris, polygon list identical, vertex groups (all `LOOP_*`), Face_M, UVs, materials, cameras, empties unchanged; boundary / non-manifold edges 92 / 2 as before; height 1.702187 m |
| SIL_FRONT_BACK<=5.0% | PASS | Blender-native locked cameras: front 4.299 -> 4.335 %, back 4.578 -> 4.359 % (start = ArtDir `b9cc8596…`, which already contained the torso edit) |
| ARM_SHAFT_vs_PLATE | PASS (untouched) | width along the arm axis vs the bald plates, front and back: within +-2.2 px (about 6 mm at the worst station, mostly < 1.5 px), mixed sign, so upper arm and forearm above the wrist band are bit-identical to the start |
| HAND_vs_PLATE_SILHOUETTE | PASS (mixed per view) | 8 hand ROIs (bald front/back + multi-angle front/back, L and R): 2,461 -> 2,218 XOR px (-9.9 %). Per ROI start -> final: bald front L 285 -> 332 (worse), R 487 -> 438; bald back L 460 -> 367, R 430 -> 325; sheet front L 159 -> 170, R 174 -> 161; sheet back L 346 -> 335, R 120 -> 90. Arm-zone XOR in the bald views (front+back, both arms): 2,432 -> 2,283 px |
| THUMB_ROOT_INTEGRITY | PASS | self-intersecting triangle pairs in the arm zone 23 -> 0 (all were at the thumb root); edges with dihedral > 100 deg 18 -> 4 (> 90 deg: 32 -> 10); max dihedral 163 -> 104 deg; 4 faces that were folded back at the root are now consistently oriented |
| WRIST_AND_FOREARM_SURFACE | PASS | wrist/hand surface noise (normal residual vs a 6-iteration Taubin, rms) 0.46 -> 0.33 mm, p99 2.10 -> 1.21 mm; edges > 60 deg in the zone 96 -> 52. Wrist cross-section area changes: +0.0 / +0.2 / +3.9 / -0.2 % at four stations (no thinning); extents within 2 mm |
| FINGER_SEPARATION | PASS | finger gap 1.6 -> 2.8 mm and slimmer fingers (width 27.2 -> 26.8 mm), readable slits on palm and back; no finger/thumb self-intersections (0). Middle finger +12 mm (0.162 -> 0.174 m reach), index/ring/pinky +0/+4/+4 mm |
| GRIP_SOCKET_PALM | PASS | palm-centre vertices (28 per hand, within 3 cm of the palm centre) move 0.33 mm mean, 1.28 mm max; palm plane and wrist frame unchanged, so a future Hand_Racket socket keeps its place |
| MIRROR_SYMMETRY | PASS | R vs mirrored L arms nearest distance mean/p95/max 0.58 / 1.90 / 5.69 mm -> 0.58 / 1.86 / 5.74 mm (the pipeline builds both hands from the same parameters) |
| FBX_REIMPORT | PASS | 60,358 tris, max vertex error 0.00028 mm, height 1.702187 m; corner normals follow the new geometry (dot >= 0.99998 in the zone); 2,490 vertices differ from the previous FBX, 0 outside the zone |
| UNITY_REFRESH | NOT RUN | no Unity import path was assigned to this chat; the Unity copy of the FBX is untouched (see below) |

## What changed

* Start: the prompt's start SHA was `68de53ca…`. ArtDir moved to `b9cc8596…` at 22:01 (torso chat install) while this chat worked; that edit touched 328 vertices at the inner edge of my zone (the shoulder cap, |x| 0.215-0.24, arm-axis t -0.37..-0.25) and **none** of the vertices I moved, so the arms/hands delta was rebased onto `b9cc8596…` by coordinates (`work/male-parallel-arms/apply_delta.py` refuses if any delta vertex was edited by someone else; mismatch was 0).
* Method: the Micro1f hand generator (`work/male-micro1f/hand_build2.py`, bit-exact reproduction of the Micro1f hand verified before any change) copied into `work/male-parallel-arms/hand/` and extended; the new hand coordinates are patched into the current mesh through an exact-coordinate index map (22,400 vertices matched uniquely). No remesh, no new vertices.
  * **Thumb root rebuilt (the real defect).** The old thumb tube was bridged from the 10-vertex palm hole to a ring that was twice as wide and lay beyond the hole's top edge, so the strip folded back (dihedral up to 163 deg), left an overhanging ledge and 23 intersecting triangle pairs. Now the thumb axis starts at the hole centroid and the first three rings morph from the extruded hole loop into the round tube (`thumb_morph=3`, `thumb_anchor=1`, `thumb_first` 14 -> 8 mm), followed by a localized fairing (25 Taubin iterations within 4 cm of the root).
  * **Thumb shape.** radii (24 / 22.5 / 19.5 / 16.5 mm) -> (20.5 / 19.5 / 17 / 15 mm); axis shortened from 8.5 to 6.9 cm and tucked closer to the palm plane (tip a'/n/w 0.060/0.042/0.098 -> 0.063/0.030/0.088 m in hand-local coordinates); this follows the plates, which show a short stub, and it removes the sausage look.
  * **Fingers.** `r_a` 0.0136 -> 0.0134, `r_n` 0.0185 -> 0.0177, gap 1.6 -> 2.8 mm, fingertip reaches [0.150, 0.162, 0.154, 0.134] -> [0.150, 0.174, 0.158, 0.138] m, taper 0.08 -> 0.10. Chosen by a coordinate descent on the hand-ROI silhouette XOR with a penalty on thumb-axis bending: the first, unpenalized run made the silhouette numbers better and the thumb a hook with crumpled shading (rejected, see Pitfalls).
  * **Web and crease fairing.** 10 Taubin iterations (weights 0.4-0.9) on the finger-web crotch vertices and the thumb-web crease (palm rows 10-11, finger first rings, thumb rows 0-2): worst web crease 120 -> 104 deg.
  * **Wrist band.** 12 Taubin iterations on the weld ring and the forearm rows just above it (arm-axis t >= 0.04, full weight from t = 0.08) plus the first palm rows; removes the crinkles / dents that came from the irregular Hand_4-era quads next to the weld (largest single vertex move 7.6 mm, wrist section area +3.9 % at the worst station). The rest of the forearm, elbow, upper arm and shoulder are untouched on purpose ("never smooth away plate forms"; a wider fairing of the forearm was tested and gave no silhouette benefit).
* Parameters and the exact vertex set: `work/male-parallel-arms/data/final_params.json`, `export/arms_zone_delta.npz` (indices, base, new).

## Known remaining differences

* **The plates disagree on the hands** (front vs back hand size differs about 28 %): in the front plates the thumb stub is longer and wider than the new one (front-left ROI got worse, +47 px), in the back plates it is hidden (the start thumb stuck out of the back silhouette; now smaller but still visible). The hand is a compromise; the numbers above are the evidence, not a claim of likeness.
* Finger webs still have ~100 deg creases at the ring/pinky webs and the thumb web ~93 deg: the topology has a single quad per web, so they cannot be rounded further without new vertices.
* Fingertips and thumb tip are 10-sided: faceted outlines only show in extreme close-ups.
* Raking-light striations on the forearm (0.1-0.2 mm) and the muscle ridges are left as they are.
* Gaps in my own measurement: the plates are AI renders; the 3/4 plate views (not gated) cannot be fitted by a rigid yaw of the model (leg stance differs), so arm depth is not independently verified.
* **Unity not refreshed.** The Unity project keeps its own copy at `Unity/Assets/Characters/HeroBase/Models/HeroBase_Male_Body.fbx`; no import path was assigned to this chat. After the merge, copy `ArtDir/hero/base_lock/unity_import/HeroBase_Male_Body.fbx` there and re-run the proof capture. Blender previews use clay lighting, not the Unity skin.

## Files and hashes

* Staged and installed 22:20 by `work/male-parallel-arms/install.sh` (SHA-guarded, lock dir, atomic replace): blend `9f6d02bd67a8b38dc063ca5033535a385265900c2d71f959a96f65ad50d56154`, FBX `614cccf33f8401632b78605c5f81abc911077dfd9d0c68d5b6e4d66a0722cd6d` (60,358 tris; re-import max error 0.00028 mm).
* Another chat installed on top of this at 22:21 (the vertices that changed are all at z < 0.77, i.e. the legs chat): the ArtDir blend is now `0dd37baf…` and contains torso + arms/hands + legs; verified afterwards that all 2,502 arms/hands delta vertices equal their staged values in it (0 differ).
* Start / rollback: `work/male-parallel-arms/baseline_S1/pre_install/HeroBase_Male_Silhouette.blend` (`b9cc8596…`) and `HeroBase_Male_Body.fbx` (`86462900…`). Rollback of only this edit = apply the inverse delta (`base` column of `arms_zone_delta.npz`) to the current blend, or copy these two files back if nothing else was installed since.
* Machine checks: `proof/male-arms-hands-checks.json`.
* **Merge help**: to put the arms/hands onto any newer blend: `blender -b <current.blend> --python work/male-parallel-arms/apply_delta.py -- work/male-parallel-arms/export/arms_zone_delta.npz <out.blend>` (aborts if a delta vertex was edited since the start).

## Proof

* `proof/01k_arms_hands_m.png`: before / after of both hands at identical cameras (back, front, palm, 3/4 back, side, wrist), plate vs mesh at the locked plate cameras (bald front/back, multi-angle front/back), arm silhouette overlays, thumb-root wireframe before / after, and the gate numbers.

## Pitfalls worth remembering

* Silhouette-only optimization of a 3D part from two inconsistent plates will happily bend the part into a hook (first thumb run: -13 % XOR, crumpled normals on the back of the thumb). Constrain turning angles and look at the render before accepting a parameter set.
* `inspect.py` (or any file named like a stdlib module) in the working directory shadows the stdlib for system python: it broke `import inspect` inside PIL; keep script names unique.
* Taubin smoothing of a zone needs the zone mask in the *vertex space of the array being smoothed*: the first attempt used a coordinate box in the Micro1f array and smoothed legs and feet; the patch step asserts that every moved vertex lies inside the arm zone and caught it.
* Closeup camera roll: `to_track_quat('-Z','Z')` (as in some older scripts) gives arbitrary roll; use `'Y'` when the orientation matters (set `UPY=1` in `work/male-parallel-arms/closeup.py`).

## Reproduce

`work/male-parallel-arms/`: `run.py` (build -> patch -> metrics -> renders), `hand/hand_build2.py` + `hand/assemble2.py` (generator), `opt.py` (parameter search), `zone.py` / `zonelib.py` / `patchlib.py` (zone, index map), `audit.py`, `finalize_checks.py`, `selfisect.py`, `apply_delta.py`, `export_fbx.py`, `install.sh`, `proof_render.py` + `compose_proof.py` (proof sheet).
