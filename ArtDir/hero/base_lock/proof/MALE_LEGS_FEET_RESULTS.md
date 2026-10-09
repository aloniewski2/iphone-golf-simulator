# MALE_LEGS_FEET — parallel chat 4/8 (legs + feet) — 2026-10-01

**GATE: MALE_LEGS_FEET PASS** on every measured/visual line below. **Unity capture NOT run** (parallel-safety: no Unity prefab path was assigned to this chat) — so "looks the same in Unity" is *not* claimed. Adnan's eye-check is final.

Zone owned: `Body_M` vertices with start **z < 0.765 m** (crotch apex) and **|x| < 0.27 m** — thigh, knee, calf, ankle, foot (8,745 verts, 8,744 moved). Nothing else was touched.

## Gate lines

| # | Line | Result | Evidence |
|---|------|--------|----------|
| 1 | ZONE_ISOLATION — 0 verts moved outside the zone (bit-identical float32), head / torso / arms / hands / Face_M untouched | **PASS** | `male-legs-checks.json` → `moved_outside_zone 0`, `moved_head 0`, `moved_torso 0`, `moved_arms_hands 0`, `face_m_unchanged true`; heat map `04l_legs_07_zone_heatmap_m.png` (grey = bit-identical) |
| 2 | TOPOLOGY_INTACT — same verts/faces/tris, no flips, no new open edges | **PASS** | 30,296 verts · 30,518 faces · 60,358 tris (same); 0 flipped faces; boundary/non-manifold edges 92/2 (same); no NaN; vertex groups + materials equal; zone quad angles 19–170° (start 23–162°), max zone edge 21 mm (start 19) |
| 3 | FOOT_PLACEMENT vs both profile plates (toe, heel, ankle) | **PASS** | toe error 14.7 → 1.1 mm, heel 21.5 → ≤ 6.2 mm, ankle centre @ z 0.19: −18 mm → +1 mm (`04l_legs_02_profiles_m.png`) |
| 4 | INSTEP + ANKLE sagittal contour vs plate near-foot contour | **PASS** | mean 15.3 → **1.1 mm**, max 30.6 → 6.6 mm (instep part: 16.7 → 1.9 mm) — `04l_legs_06_sagittal_contour_m.png` |
| 5 | Silhouette bald front/back ≤ 5.0 % (whole figure, locked cameras) | **PASS** | 4.323 % / 4.385 % installed (4.335 / 4.359 before legs). Not worse; see "plates disagree" below |
| 6 | SURFACE_CLEAN — no banding / pits / creases / chopped bevels | **PASS** (visual + numbers) | vertical-ripple residual 480 → 235 µm rms, normal roughness 414 → 257 µm; clay close-ups `04l_legs_04/05` |
| 7 | KNEECAP — plates show a round proud kneecap | **PASS** (visual) | front-surface relief at the knee: −6.0 mm (a dent) → **+6.3 mm**; `04l_legs_05_knee_leg_before_after_m.png`, `04l_legs_01_front_back_m.png` |
| 8 | GROUND_CONTACT + scale | **PASS** | min z 0.002473 (unchanged, flat soles), height 1.70219 m (unchanged), foot origin intact |
| 9 | Unity render vs Blender match | **NOT RUN** | FBX staged only (below) |

Silhouette XOR on the front/back plates is at its practical floor: the two plates disagree with each other by up to ~2 cm in calf width, and the left/right profile plates disagree by ~2 cm at the shin front edge; the mesh already sits at the consensus (mean |dev| x-edges 1.0 mm, y-edges 2.6 mm — was 4.2 mm).

## What changed (chain `work/male-parallel-legs/chain.sh`, deterministic from the base blend)

1. **A** de-banding — vertical-anisotropic Taubin (40 it, normal-only): removes the horizontal ripple rings on thigh/shin/calf.
2. **W** foot/ankle placement from the plates: heel stretched ~17–20 mm back and toe ~12–20 mm forward (foot 27.5 → ~30.4 cm; both profile plates say 31 cm), ankle +20 mm back (it sat 1.7 cm too far forward), 3° toe-out.
3. **F5/F6** biharmonic fairing of the feet (constrained, soles stay on the ground plane): round heel, no chamfer ridges.
4. **P** sagittal-profile warp (2D thin-plate spline in y,z): the instep was **2–3.5 cm too high** and the ankle front ~3 cm too far forward versus the near-foot contour read from both profile plates; now on the plate contour.
5. **R** tangential relaxation re-projected onto the surface (even quads again), **L** light isotropic fairing of the leg tube (cm-scale lumps on the shins), **K** kneecap relief (+8 mm bump, ring groove, ⌀ ≈ 8 cm at z 0.52).

Zone displacement vs base: thigh/knee/calf (z 0.3–0.765) mean 0.3–1.2 mm (knee relief max 7.9 mm); ankle/foot (z < 0.3) mean 5.7–19 mm, max 33 mm. Top 3 cm of the zone moves ≤ 0.9 mm (continuous with the torso).

## Plate findings worth knowing

- The profile plates contain a **second, raised foot** (camera pitch): the "far foot" blue wedge in any profile XOR is a plate artifact, not a mesh error. I used only the near-foot contour (scan of both profile plates; left/right agree within ~1 cm).
- Front vs back plates disagree on foot width at z < 5 cm (front foot union x 0.16–0.29, back 0.135–0.245). The silhouette optimum for toe-out is ~2–4° (cost is flat 0–4°, rises fast beyond 6° in the back view); I used 3°. The 3/4 plates *look* more toe-out; raising it costs back-view silhouette.
- Custom split normals on `Body_M` are stored relative to the geometry (INT16_2D corner attribute), so moved vertices keep consistent shading and FBX normals; checked (zone mean deviation 0.3°).

## Not mine / open

- Outside the zone (torso/pelvis chat): a small dash-like crease near z ≈ 0.78 on the outer thigh/hip and the flat "roof" bridging the thigh gap at z ≈ 0.765–0.775.
- Calf muscle definition is only as strong as the silhouette consensus (plates show a slightly brighter/higher gastrocnemius); no relief added there.
- Unity: re-import `HeroBase_Male_Body` from the merged blend and recapture; I did not touch `ArtDir/.../unity_import` or any prefab.

## Install state and rollback

- `ArtDir/hero/base_lock/blender/HeroBase_Male_Silhouette.blend`: legs patch installed with `work/male-parallel-legs/export/merge_legs.py` (zone-index patch; aborts if the legs zone was touched by someone else; idempotent).
  - replaced file (= start + torso + arms): sha `9f6d02bd…`, backup `work/male-parallel-legs/baseline/artdir_pre_legs_install.blend`
  - installed file (= start + torso + arms + legs): sha `0dd37baf20fcc3524ee1d8557b88b87ce0d2d3782d13c5eb15a8df9fe38802b7`
  - verified against the original start (`68de53ca…`): 8,744 legs-zone + 6,817 torso + 2,502 arms verts moved, 0 head, 0 elsewhere.
- Re-apply after any later overwrite: `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python work/male-parallel-legs/export/merge_legs.py -- <target.blend>`
- Rollback: copy `baseline/artdir_pre_legs_install.blend` back over the ArtDir blend.
- Staged (not installed): `export/HeroBase_Male_Body_integrated_torso_arms_legs.fbx` (sha `a3979a30…`, 60,358 tris, re-import max error 0.0003 mm) built from the installed blend; `export/HeroBase_Male_Body_legs.fbx` (legs on the torso-only base).

Proof images (this folder): `04l_legs_01_front_back_m` · `02_profiles_m` · `03_threequarter_m` · `03b_feet_threequarter_m` · `03c_sheet_front_back_m` · `04_feet_before_after_m` · `05_knee_leg_before_after_m` · `06_sagittal_contour_m` · `07_zone_heatmap_m`. Renders are Blender clay (EEVEE, 3 suns) from the locked plate cameras and fitted orthographic 3/4 cameras — not Unity captures.
