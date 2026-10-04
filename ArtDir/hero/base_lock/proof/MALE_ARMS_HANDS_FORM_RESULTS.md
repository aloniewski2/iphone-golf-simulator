GATE: MALE_ARMS_HANDS_FORM PASS

Male arms + hands FORM2 (hands primary, arms light) · 2026-10-01/02 · brief `parallel_prompts/03b_MALE_HANDS_FORM.txt` · Sonnet 5.5. Both hands were rebuilt locally (same slit topology family as Micro1e/f, new generator `work/male-arms-hands-form2/hand3.py`), arms got a light soft-plastic mass pass. Installed under the SHA guard: blend `1fe4d53b…` -> **`4287ba523218e89ae5a822483c322dc4cfa163c7af143ebcac11abbacea2db21`**, FBX `73582af6…` -> **`2d052746eb2ff26403080df24131abecd83564cf04528e77c46a9c2af245c483`**. The brief gave no numeric thresholds; the numbers in the table are the ones I set before measuring. Adnan's eye-check is final.

Proof images (this folder): `03c_hands_form2_before_after.png` (both hands, 6 views, before vs after), `03c_hands_form2_plate_silhouette.png` (plate | before | after at the locked plate cameras for the 8 hand ROIs + silhouette overlays), `03c_hands_form2_detail.png` (thumb + finger close-ups, solid and wire), `03c_arms_form2_sheet.png` (arm before/after zooms). Numbers: `male-arms-hands-form-checks.json`.

| Line | Result | Evidence |
|---|---|---|
| THUMB_ANATOMY (thenar / web) | PASS | palm vertices at the thumb root sit up to 13.5 mm above the plain palm ellipse (thenar mound, threshold >= 4 mm); web crotch is a smooth-union fillet (28 mm radius, 1.6x amplitude) plus Taubin, no slit; max dihedral within 25 mm of the root 68.2 deg (threshold <= 75; only 2 edges > 60) |
| THUMB_ANATOMY (real phalanges) | PASS | axis bends 15.5 deg at the IP joint (threshold >= 8), shaft radius has an IP bump (+0.95 mm, 13.4 -> 14.4) between a tapering proximal and distal phalanx, 17 rings per thumb |
| THUMB_ANATOMY (tip pad, not a pill, not a spike) | PASS | radius 20.0 mm at the root ring -> 10.7 mm at the last shaft ring (ratio 0.53, threshold <= 0.65), 6 cap rows with strictly decreasing radii (pad tip); min shaft radius 10.7 mm, axis length 83 mm (length/diameter 3.9, threshold <= 5) |
| FINGER_TAPER | PASS | all 8 fingers: width base 25.0 -> mid 20.6 -> tip 15.0 mm (tip/base 0.60, threshold <= 0.70; mid/base 0.82, <= 0.90), width profile monotone, longest equal-width run 2 of 12 rows (17 %, threshold <= 35 %), depth/width at the base 1.15 (<= 1.4) |
| FINGER_TAPER (knuckle hint, pad tips) | PASS | dorsal PIP bump 1.6-2.1 mm, DIP 0.8-1.1 mm (thresholds 1.0 / 0.6), 6 cap rows per fingertip (rounded pad tips), slight relaxed curl (about 8-14 / 6-10 deg at PIP / DIP, +2.5 deg at the knuckle), finger gaps 7-13 mm at 40 / 70 % of the length (>= 4) |
| MIRROR_L_R | PASS | right vs mirrored left hand, distal hand: max 0.005 mm (both hands come from the same parameters; only the weld rows follow the real, slightly asymmetric forearm ring) |
| ZONE_OUTSIDE=0 | PASS | vs the ArtDir blend at start: 6,130 original vertices moved (the 1,746 reused hand-shell slots, the rest arm-shaft vertices), 0 outside the declared arm zone; head/neck, torso+pelvis, legs/feet bit-identical; 1,332 vertices appended (hand detail) |
| NO_SELF_INTERSECT | PASS | BVH overlap among all arm + hand triangles, non-adjacent: 0 pairs (before 0) |
| SIL_BALD_FRONT <= 5.0 % | PASS | Blender-native locked camera, XOR/union vs the frozen plate mask: 4.320 -> 4.324 % |
| SIL_BALD_BACK <= 5.0 % | PASS | 4.385 -> 4.632 % (+0.25: the plates disagree on the hand; the new hand is slimmer than the plate's blade in the back view) |
| HAND_vs_PLATE (8 hand ROIs, my rasteriser) | PASS (slightly worse) | XOR px 2,218 -> 2,347 (+5.8 %); the first unconstrained fit reached 2,175 but needed 36 mm deep fingers (depth/width 1.44) and was rejected for anatomy. Other native views: left 9.008 -> 9.008 %, right 9.761 -> 9.738 %, multi front 7.179 -> 7.112 %, multi back 7.891 -> 8.086 % |
| TOPOLOGY / DATA | PASS | boundary / non-manifold edges 92 / 2 as before, 0 degenerate polygons, `custom_normal` loops 31,246 non-zero (identical to the start blend), 17 `LOOP_*` vertex groups unchanged (848 entries), no sharp flags, material `Blockout_Grey` |
| TRIANGLES | DISCLOSED | 60,358 -> **63,022** (+2,664, +4.4 %). AGENTS.md says ~35-50k per base unless Adnan approves more; the ArtDir mesh was already 60k after the head densify. 3,140 new faces (44 triangles), min area 0.38 mm2, 4 cap-pole quads have edge aspect 11-15 |
| FBX_REIMPORT | PASS | 31,628 verts / 63,022 tris, max vertex error 0.0003 mm, height 1.7022 m |
| ARMS (secondary, not worse) | PASS | mass fields (biceps/triceps +3.7 mm front-back extent at arm-axis t = -0.17, elbow waist -1.0 mm, forearm +1.4 / +2.3 mm at t = -0.045, deltoid +1 mm; max move 2.5 mm, mean 0.7 mm on 4,381 vertices), surface noise (residual vs a 6-iteration Taubin) 0.346 -> 0.086 mm rms; faded to exactly 0 at the torso seam (0.000 mm on the zone-boundary vertices, so no step) and before the wrist weld ring; the arm pass alone changes the bald silhouette by +0.02 % front / +0.05 % back |
| UNITY_REFRESH | NOT RUN | the Unity copy of the FBX is untouched. When it is refreshed, `Unity/Assets/Editor/HeroBaseMaleFormProof.cs` `ExpectedTriangles` must change from 60358 to 63022 or its contract check throws |

## What changed

* **Start SHA moved while I worked.** The brief's start blend `72fc6465…` was overtaken by torso FORM2 (`59a74b85…`) and head Micro1k (`1fe4d53b…`). I rebuilt the whole edit on `1fe4d53b…` (the pipeline reads the current blend dump, so nothing of theirs is reverted) and installed with the guard; 47 vertices at the torso/arm seam (|x| 0.215-0.22) were changed by torso FORM2 by <= 0.01 mm, my edit leaves them bit-identical. Polygon order differs from the old file (bmesh reuses freed face slots); vertex indices 0..30,295 keep their meaning, new vertices are appended (30,296..31,627).
* **Hands (`hand3.py`).** Same construction family: 42-vertex weld ring -> 13 palm rings (40 verts) -> lobed knuckle ring -> 4 slit fingers -> thumb on a hole in the palmar-radial flank. Everything that made the old hand a mitten is new: finger rings follow a bent axis with the nail-side (dorsal) line kept straight, depth and width taper from keyed profiles (PCHIP), PIP/DIP dorsal bumps and side swell, palmar pad, 6-row rounded caps; the thumb hole is 5 x 4 quads (18-vertex root loop, columns 34..39) so the thumb grows out of the thenar instead of a 10-vertex neck, the axis is a Catmull-Rom curve with MCP / IP nodes, the radius profile has a knuckle and an IP bump, the web is filled with a smooth-union style fillet and the root is relaxed with Taubin. Palm rows and wrist weld logic are the Micro1f ones ("palm OK"), only the rows near the thumb are denser.
* **Silhouette fit.** `evalp.py` / `opt3.py` (0.2 s per evaluation) fit 12 bounded parameters to the 8 hand ROIs; bounds keep depth/width <= 1.3, splay and IP bend natural. Final choice is an anatomy-leaning point near the fit (hand ROI 2,347 vs fit 2,265).
* **Arms (`arms3.py`).** Gaussian displacement fields along vertex normals (mirrored by construction), then 14 Taubin iterations on the shaft; weights fade to 0 at the zone seam and before the weld ring.

## Known remaining differences / honest limits

* The plates disagree: the bald front/back views are edge-on to the hands, the multi-angle front shows fanned, slender fingers. The hand is a compromise; Adnan's eye decides.
* Hand ROI silhouette is 5.8 % worse than the old hand and the bald back view is +0.25 % (still 4.63 %).
* Fingers are modelled relaxed and straight-ish; no Hand_Racket socket was touched (grip sockets are out of scope), the palm plane and wrist frame are unchanged.
* Not in Unity yet (see UNITY_REFRESH).

## Rollback and rebuild

* Pre-install files: `work/male-arms-hands-form2/baseline_cur/pre_install/HeroBase_Male_Silhouette.blend` (`1fe4d53b…`) and `HeroBase_Male_Body.fbx` (`73582af6…`).
* Rebuild on a newer blend: dump it (`dump_mesh.py` -> `data/mesh_cur.npz`), `py.sh run_metrics.py G1 '{}' out.json '{}'` (writes `data/cand_G1.npz`), `write_blend.py` on a copy of the new blend, `verify_blend.py`, `export_fbx.py`, then `install_form2.sh` after updating its START SHAs. Hand parameters = the defaults in `hand3.py` (`P0`), arm parameters = `arms3.ARM0`.
