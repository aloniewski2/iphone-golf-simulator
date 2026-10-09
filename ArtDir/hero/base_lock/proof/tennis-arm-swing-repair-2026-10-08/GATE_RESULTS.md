# Tennis arm swing repair — 2026-10-08

Installed in the active iPhone tennis Unity project. Human scope: use the supplied forehand/backhand references to repair elbows, hands and body motion; preserve the accepted forehand; exclude clothing work.

## Reference and retarget

Source: https://www.youtube.com/watch?v=UsR2o_koFyU

46 consecutive viewport captures at 30 fps cover the opening backhand and recovery (0.000–1.500 s). This does not include the later replay. Original captures are in `backhand-reference-frames/`, with a full contact sheet in `backhand-reference-all-frames.jpg`.

The backhand is manually retargeted from these visual phases onto each Generic skeleton, with reconstructed joint depth. It is not measured 3D motion capture or an exact coordinate copy. Its 73 samples at 60 Hz retain the game's 1.2 s clip and contact timestamp at F38 (0.63333 s). The game-ready transition is F0–7; setup F8 aligns to reference F0, load F24/F10, drop F32/F18, contact F38/F21, rise F48/F27, wrap F56/F33 and recovery F72/F45.

Both palms are solved against a common handle before either arm is baked. Wrist-joint offsets are removed from the reference palm path. Elbow hinge direction, forearm roll and palm bend are bounded; elbow candidates receive a strong torso-clearance penalty. Supporting fingers receive at most 10 degrees of additional rotation per joint. The existing source torso rotations are retained; hips are eased 4 cm during load and source foot positions are replanted.

A body-only runtime dual-quaternion support palette prevents arm-joint collapse. Original rest vertices, normals, UVs and topology are retained. Body asset bindings and source FBX files are not edited; the runtime mesh has new arm-support weights/bind-palette entries. Clothing palettes/assets are untouched.

## Gates

GATE: ACCEPTED FOREHAND HASH LOCK PASS — both clips and metadata match the recorded accepted SHA256 hashes.

GATE: REST GEOMETRY PASS — exact vertex and triangle equality against baseline for both characters; normalized four-influence weights; 144 male / 158 female palette bones, within 256.

GATE: FRAME COVERAGE PASS — all 73 frames per side/player view, both strokes and both characters. Backhand V5 was captured afresh; forehand captures remain from the accepted version.

GATE: ELBOW DIRECTION PASS — positive local hinge flex throughout all sampled strokes; no reversed elbow bend.

GATE: BACKHAND PALM BEND PASS — maximum 60.0001 degrees relative to the forearm axis. Total hand rotation includes axial orientation and is checked separately.

GATE: TWO-HAND HANDLE PASS — maximum held supporting-palm gap (F8–58): 0.000001 m male / 0.000155 m female.

GATE: CONTINUITY PASS — hand and support-palette steps remain below the 65-degree sampling limit; no half-turn palette jumps in these clips.

GATE: TORSO CLEARANCE PROXY PASS — 5 points per forearm at all 73 backhand frames stay outside the chest-space ellipsoid, within 0.001 normalized-radius numerical tolerance. This checks centreline clearance against a conservative proxy, not exact triangle/capsule collision. Both rendered angles were inspected at loading, drop, extension and wrap.

GATE: ARM SKIN VOLUME PASS — minimum determinant of each actual weighted skin transform exceeds 0.2; no negative/folded arm transform among checked arm/hand vertex groups. Per-clip minima: male_forehand 0.317, male_backhand 0.232, female_forehand 0.497, female_backhand 0.694. This is a deformation gate, not proof that every mesh triangle is collision-free.

GATE: SOURCE FEET PASS — maximum position change under 0.002 mm.

GATE: CLIP MAP PASS — `MatchHeroClipMapTests`: 1 passed, 0 failed. New standard forehand/backhand assets resolve for both characters; contact/release values and other slots remain intact.

GATE: INSTALLATION PASS — surgical anchored patches preserve concurrent clothing edits; installed file hashes and accepted forehand hashes verified. Shared-file backups are retained in the chat work directory.

## Validation limits and integration

The corrected backhand changes the old source string-centre contact position by 0.245 m male / 0.213 m female. `BuildContactModel` samples the resolved corrected clip and calibrates that position automatically; contact time is preserved.

Proof uses deterministic clip samples in the current Unity scene, with current materials and fixed review cameras. It does not certify live running/ball-contact blends or an iPhone build. Wide/short/return/serve clips were not rebaked or comprehensively reviewed. Native preview/export bundles and the iPhone build were not regenerated.

A broader gameplay test, `LateConfirmationStillHits`, failed both with the repair and in the repair-disabled baseline (expected one hit, actual zero; shot classified as overhead). It is an existing gameplay-test failure and is not counted as a passing test.

Female unclothed proof shows preexisting coverage holes beneath garments. Clothes are under separate work and remain outside this patch.

## Proof

- `male_backhand-slow.gif` and `female_backhand-slow.gif`: side + elevated player views.
- `reference-backhand.html`: captured reference beside either model, with every model frame selectable.
- `male_backhand-review.jpg` / `female_backhand-review.jpg`: loading, contact and rise.
- `male-forehand-elbow-review.jpg`: accepted F40/F48.
- `verification.json`, `after/skin-analysis.json`, `torso-clearance-proxy.json`, `clip-map-results.xml`.
- `installed.patch`, `installed-files.json`, `forehand-lock.json`.
