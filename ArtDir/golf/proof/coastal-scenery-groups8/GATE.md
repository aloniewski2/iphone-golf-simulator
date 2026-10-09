# Coastal hole12 obstacle-group source gate

GATE: Independent CPU/source audit PASS. Fresh Blender import of original hole_12.fbx and staged groups8/ReferenceHole12.fbx; original/candidate hashes match the staged manifest. No source edits and no Unity execution.

Both meshes replay ObstacleScan local1mm weld, Standing and TreeOf semantics as161 physical records:43trees,74bushes,43rocks,1wall. All161 have a unique one-to-one physical group match after the intended manifest whole-group sourceY translation; no merged/phantom/missing identity remains. Original pre-warp bounding pivots identify groups because equal-width shrub sorting ordinals are not a stable physical ID.

Descriptors (center/radius/Base/Top/TrunkRadius/CrownBase/Cone/pieceCount) stay within0.0000292yards (~0.0267mm). Every117 frozen tree/shrub group retains vertex, polygon and triangle counts and original vertex positions under its rigid translation; maximum source geometry displacement error0.0000308m (~0.0308mm). Original/candidate PIN/TEE/UP anchors differ by at most0.0000229m (~0.0229mm) after FBX quantization.

Evidence: group-preservation-report.json, original-obstacles.json, candidate-obstacles.json. Reproduction: dump-fbx.py (read-only background Blender import), then verify-groups.py (CPU). Blender startup requires the permitted unsandboxed Metal device enumeration on this host; no render/GPU work performed.

GATE: Ordinary Unity Default9 and Camera10 PASS:161 obstacles /14 colliders; complete ground records/hashes exact to Grass8; both ordinary runs share the exact obstacle hash. Independent source proof checks all161 descriptors/physical groups; a full Unity descriptor-array comparison was not run. Current source/ordinary defaults are accepted for this bounded pass. See current-coastal-gate.json and the two canonical actual audit files.

Portable reproduction from the repository checkout (scratch output is recreated):

```
/Applications/Blender.app/Contents/MacOS/Blender -b -t 1 --python ArtDir/golf/proof/coastal-scenery-groups8/dump-fbx.py -- --repo . --out work/reference-rebuild/coastal-scenery-repro
python3 ArtDir/golf/proof/coastal-scenery-groups8/verify-groups.py --repo . --out work/reference-rebuild/coastal-scenery-repro
```

Author the model independently with ArtDir/golf/tools/build-reference-hole12.py, using the frozen versioned Resources/Course/hole_12.fbx input. The audit uses the installed ReferenceHole12.fbx and canonical manifest; it does not depend on retained scratch files.

GATE: Catalog regression3 PASS. The existing PlayMode traversal passes13 authored holes (7/12/13/14/15/16/17/18/19/20/21/22/23) plus3 procedural Meadow routes. Focused course EditMode checks pass36/36. Root launcher39.2s; XML actual test-run30.5697s. The test-only palm correction checks the current botanical LOD/material contract; the unchanged legacy branch remains checked when active. Runtime palette logs confirm hole16 stored linear G=.046964–.162805 and display sRGB G=.240000–.440200. Full records, original XMLs and source hashes are portable under catalog-regression3/test-certificate.json. This is a gameplay/source regression gate, not a new frame-cost, phone or reference-art certificate.

Source-staleness note: accepted Grass8/Camera10 pictures remain evidence for their previous shading. Grass11 is the subsequently captured shader-only soft-shading pass (65% canopy shading blend; ground normal1→.65), with compile/render PASS24.36s and bounded parent visual acceptance. Complete actual11 audit is identical to camera10, including all14 ground records,161 obstacles and14 colliders. Current photos/hashes/parity are canonical under ArtDir/golf/proof/clustered-turf8/grass11; fuller rough edges and warmer tip variation remain below target. Archived World18 all19 costs/physics remain an older source snapshot.


Current source note: Dense15 physical grass was subsequently accepted by parent/user actual review, with compile/render PASS23.33s and exact physics entries toCamera10. Its canonical source/portable rebuild proof are under ArtDir/golf/proof/dense-turf15. Physical coverage remains12m aroundhole12tee only. Added geometry maximum542,720tri/3 instanced submissions; current phone/frame-cost certification remains absent. Hierarchy16 managed turf colors are a first-pass improvement; Green17 fine texture response is installed awaiting actual review. Earlier Grass8/Grass11 and catalogregression3 remain their original saved source snapshots.
