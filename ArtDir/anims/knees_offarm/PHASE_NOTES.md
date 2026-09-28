# FixKneesAndOffArm — review candidate

## Source and scope

Input: locked V4 `Hero_01_Mixamo_QA.blend` and the previous fixed gameplay pose bake. No Owned v1 source, mesh regeneration, materials, lights, hit timing, ball simulation or right-arm changes.

Outputs are isolated under this directory and `Unity/Assets/ArtDirection/Hero01/Models/KneesOffArm/`. The preceding `gameplay_fixed` artifacts remain available for comparison.

## Knees: weights + bend planes, not DQ

- Original body: 19,485 vertices, 33,169 polygons before the existing outfit coverage modifier. Knee neighborhoods already contain ~1,100 vertices per side across ~178 sampled height levels. Missing tessellation was not the blocker; no edge insertion or wholesale remesh was needed.
- Previous falloff was inconsistent between the visible leg shell and inner body surface. Reweighted **1,814 left / 1,773 right** vertices in `UpperLeg.L`, `LowerLeg.L`, `UpperLeg.R`, `LowerLeg.R`.
- Unified smooth upper/lower influence transition over bind Z **0.31–0.57 m**, versus the earlier visible-shell transition at 0.345–0.535 m. Other influences retain their original share; weights stay normalized.
- Refreshed only leg-region custom normals, Z 0.29–0.59 m. Rest coordinates, vertex count, topology, UVs, torso/arm weights and wardrobe assets stay unchanged.
- Reconstructed knee pole toward foot heading, bounded to a 28-degree correction and 80% blend. Preserves original hip/ankle targets and foot orientation. Bounded correction avoids flipping the airborne serve/smash knees to a new plane.
- Blender Preserve Volume / dual quaternion is **not enabled**: it does not transfer as a skinning mode through this FBX/standard Unity renderer path. Unity `SkinQuality.Bone4` remains active; that sets influence count, not DQ. No new shader framework. Source: https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/skinnedmeshrenderer/quality

## Off arm

Edited `Shoulder.L`, `UpperArm.L`, `LowerArm.L`, `Hand.L` keys on the existing clips. The initial targeted pass left the common ready pose buried in other clips, so the same clearance rule now covers all nine clips.

- Left clavicle rotates **12 degrees forward**; clavicle length retained.
- Project wrist outside a chest-relative front ellipse (0.40 m lateral / 0.345 m forward radii).
- Solve fixed-length upper/lower arm and choose elbow pole using five-sample forearm-to-torso clearance checks (nine samples in Unity verification). Aim for 4.5 cm surface gap at the forearm; hand aims for approximately a fist width.
- Limit extra hand travel to 6.5 cm per sampled frame before clearance projection; bias toward the previous elbow solution. This reduces snapping at the original fast stroke transitions.
- Limit left hand orientation transition to 30 degrees per 30 fps frame. All keys are re-baked, not removed.
- Root, Hips, Spine, Chest, Neck, Head and the complete right-arm transforms are protected. RightHand socket T/R and the V4 arm correction are unchanged.

## Reproduction

1. Blender: run `tools/fix_and_bake.py` (source originals remain read-only).
2. Copy the produced FBXs into `Unity/Assets/ArtDirection/Hero01/Models/KneesOffArm/`.
3. Unity: execute `GolfArcade.EditorTools.HeroKneesOffArmReview.Run` in batch graphics mode. Optional `-quickReview` renders representative frames while sampling all 1,080 poses.
4. Run `tools/package_review.py` after a full capture.

Unity scene: `Assets/Scenes/Hero01KneesOffArm.unity`.
Prefab: `Assets/ArtDirection/Hero01/Prefabs/Hero_01_KneesOffArm.prefab`.

The scene/prefab retain the existing PlayableGraph and clip slot names. The replacement Body_Skin mesh uses the same bind and maps all bone names onto the shared skeleton. The wardrobe remains modular.

## Review limits / priority 3

The lower-priority backhand choreography has not been rewritten. It receives knee and off-arm clearance fixes, while preserving its existing hit motion and PlayableGraph slot. This phase review stops on priorities 1–2 rather than mixing in another swing redesign.

The knees remain soft stylized joints; no sculpted bony patella or body silhouette redesign. Clearance numbers are capsule proxies plus visual review, not an exact mesh collision guarantee. Fast original source body transitions remain. No phone build or live campaign actor replacement is included.

See `fix_report.json`, `protected_curve_check.json`, `bake_verification.json`, and `../../screenshots/knees_offarm_playmode/verification.txt` for measurements.

**STOP: awaiting Adnan approval.**
