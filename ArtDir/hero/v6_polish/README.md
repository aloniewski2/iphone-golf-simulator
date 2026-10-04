# Hero V6 Polish — FAILED CANDIDATE

**GATE: V6 FAIL. Not approved; do not promote.**

Read `../v6_proof/GATE_RESULTS.md` and view `../v6_proof/failure_evidence.png` before reusing these meshes. This is a duplicate of the restored V5 workset with new hair, lower limbs and shorts. The live V5 assets and animation clips were not replaced.

Build order (each Blender script opens/saves its own workset; do not rerun middle steps on an already edited result):

1. `tools/build_v6.py` — starts fresh from restored V5; closed default hair/core.
2. `tools/build_lower.py` — removes source lower surfaces and authors lower limbs.
3. `tools/finish_mesh.py` — new shorts, shape keys, aliases.
4. `tools/crown_refine.py` — added swept crown locks.
5. `tools/repair_joins.py` — seated crown locks and removed duplicate scalp dome.
6. `tools/export_v6.py` — evaluated FBX + LOD meshes.
7. Unity `GolfArcade.EditorTools.HeroV6PolishImport.Run` — duplicate prefab and wardrobe only.
8. Unity `GolfArcade.EditorTools.HeroV6PolishReview.Baseline` — despite legacy method name, reviews V6.
9. Blender `tools/audit_export.py`; `../v5_tools/render_review.py` for authoring captures; `tools/package_proof.py` for evidence sheets.

Blender stress lunge is not a recorded gameplay dive. Hair mesh closure is not proof of absence of gaps between separate locks. Numeric animation tests are not a visual acceptance.

## Ongoing production goal

Read `PRODUCTION_PROGRESS.md` for the live state. The sequence above reproduces the first attempt; later foundation scripts (`repair_neck.py`, `knee_correctives.py`, `fix_coverage.py`, `restore_surface_winding.py`) extend it. `repair_neck.py` deliberately opens the pre-foundation checkpoint; do not rerun it after other repairs unless rebuilding the sequence. The latest Unity review writes `v6_proof/foundation4/Unity`.
