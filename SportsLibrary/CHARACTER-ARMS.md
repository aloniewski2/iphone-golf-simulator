# Shared Unity arm correction

Golf now adds a [reference-led shared grip correction](GOLF-GRIP-REFINEMENT.md) before this solver. For golf, preserved hand targets mean the corrected common-grip targets, not the old cramped authored positions. Other sports retain their existing targets.

Applies to both permanent standard characters, independent of sport. Implemented in `Unity/Assets/Scripts/Game/StandardCharacterArms.cs` and automatically added by the standard-model importer. Golf explicitly evaluates it after its manually scrubbed animation graph; normal Animator-driven sports use `LateUpdate` after animation.

## Cause and correction

The V4 source skeleton has approximately 21 cm upper arms and 21 cm forearms. Its visible limbs are separate rigid tubes with an elbow sphere, which leaves an abrupt joint and inconsistent silhouette when bent. This is not simply an FBX scale setting.

- Shared proportions: upper arm **28.5 cm**, forearm **26.5 cm**, in character meters before the game's meters-to-yards conversion. Both standard identities use the same profile.
- A two-bone solve reconstructs the elbow from the shoulder and authored wrist position, using a stable chest-relative bend direction.
- The hand's authored position and rotation are restored after the solve so equipment grips do not move.
- Original upper-arm, forearm and elbow renderers are disabled, not deleted. Each arm is replaced by one tapered continuous surface with a rounded elbow transition.
- Clothing remains on its upper-arm bone, so sleeves follow the corrected upper arm. Source hand meshes, identity, head, torso, legs, clothing colors, and original animation assets are preserved.

This is a **Unity-side** correction; opening the unchanged Blender master alone still shows its original arm construction. Re-exporting through the standard importer reapplies the shared component. Future standard-character FBXs should use `Assets/Resources/StandardCharacters/`, or another importer must attach this same component. Do not implement separate per-sport arm proportions.

## Evaluation contract

Call `ApplyAfterAnimation()` after evaluating a manual graph or sampled clip, with `ManualEvaluation = true`. For a normal Animator, leave manual evaluation false so the component runs once in LateUpdate. Do not solve before animation, because animation would overwrite the correction.

The solver preserves existing hand targets rather than lengthening a held club or moving a racket independently. Out-of-range legacy poses may extend the solved segments to preserve their targets. Extreme close-to-shoulder or unreachable targets still require authoring review; numerical stability does not imply anatomically ideal posing. This pass is not a new finger rig or cloth collision system.

## Cross-sport validation

Verified in Unity 6000.3.24f1: **52 EditMode tests and 3 PlayMode tests passed**, including the checks below. Hand and segment errors are checked during all 360 recorded frames for each golfer.

- Both golfers: frame-by-frame gameplay assertions for corrected segment lengths and unchanged hand targets while the real game performs a shot.
- Both tennis characters: actual V4 forehand clip sampled at 61 times.
- Both bowling characters: actual V4 straight-delivery clip sampled at 61 times.
- Both boxing characters: actual V4 lead-hook clip sampled at 61 times.
- Checks cover hand position and rotation preservation, corrected lengths, finite mesh vertices, bounded surfaces, and hidden original disconnected meshes.
- Six actual source-rig FBX fixtures live in `Unity/Assets/Tests/Fixtures/StandardCharacters`; regenerate with `blender/scripts/export_arm_validation.py`. They are test fixtures, not new runtime sports or equipment assets.

The remaining sport gameplay controllers are not yet built. This shared correction is installed and tested on their representative imported rigs; it does not claim every V4 clip and clothing combination has been visually reviewed.

See [updated gameplay videos](Previews/UnityArms/README.md). Offline captures are not device-performance benchmarks.
