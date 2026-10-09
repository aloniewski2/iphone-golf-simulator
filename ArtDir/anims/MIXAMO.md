# Hero 01 — Mixamo phase

## Delivery and provenance

One approved hero, one shared 22-bone Humanoid hierarchy, separate default cosmetics. No Unity import or importer changes in this phase.

Mixamo accepted `ArtDir/hero/modular/Hero_01_Body.fbx` as an already-rigged character through Upload Character → Review → Confirm, under Adnan’s existing signed-in session. This uses the **compatible existing-rig route**, not a claim that Adobe generated new skin weights. The raw returned bind is `source/Mixamo_Body_Bind.fbx`. An unrigged fallback copy was prepared but was not uploaded or used.

Mixamo’s returned animations were baked in Blender onto the approved shared rig by evaluated bone matrices, accounting for FBX rest-axis differences. The provisional joints were centered +0.055 m along Blender Y to fit the actual body depth; the feet-origin Root remains unchanged. This correction is included in every final export. Do not mix the old Modularize FBXs with this corrected skeleton.

## Outputs

- `Hero_01_Mixamo_Bind.fbx`: final clean A-rest-pose rig, default visible body, eyes, all five default wardrobe pieces, and sockets; no animation.
- `Idle.fbx`, `Ready.fbx`: clip FBXs with a Body_Skin bind-reference mesh, same skeleton and rest transforms as the bind. Ignore their mesh when importing animation; use the assembled bind and wardrobe for the character.
- `Hero_01_Mixamo_QA.blend`: editable assembled hero, fixed linear skin weights, both named actions, original review setup, skin-tint controls.
- `mixamo_preview.gif`: ~5.5 seconds at 15 fps; Idle excerpt followed by the complete Ready motion, default wardrobe equipped. Clip FBXs retain 30 fps.
- `motion_contact_sheet.jpg`: samples from both motions.
- `wardrobe/`: separate re-exported Body, Body_DefaultCoverage, Hair_Default, Hat_Visor, Shirt_Default, Shorts_Default and Shoes_Default FBXs, plus original atlas/normal/iris and tint mask assets.
- `source/`: untouched downloaded Mixamo files for provenance and future rebaking.
- `verification.json`: fresh FBX-import checks, skeleton signatures, weights, and sampled motion bounds.

## Clip list

| Delivered | Mixamo title / ID | Frames at 30 fps | Duration | Use |
|---|---|---:|---:|---|
| Idle | Breathing Idle / 107900901 | 1–299 | 9.933 s | Calm menu/base idle |
| Ready | Ready Idle / 104210901 | 1–76 | 2.500 s | Temporary defensive ready stance |

Default Mixamo settings: arm-space 50, mirror off, full trim, no key reduction; downloaded without skin for motion files. Idle and Ready are distinct actions. Ready is a generic combat-ready base, **not an authored tennis/racket pose**. Walk was optional and omitted. Swing, miss, serve, celebration, racket grip, transition blending and explicit seamless-loop treatment remain for the animation phase. No root-motion gameplay behavior is implemented here.

FBX note: a skinned body reference is intentionally included in each final clip to carry the explicit rest bind matrices. A bare-skeleton FBX round trip otherwise inferred the first animated pose as its rest pose. The raw Mixamo motion downloads remain skin-free in `source/`.

## Humanoid mapping

Bone names are preserved because Mixamo recognized this hierarchy. The `mixamorig:` prefix is not required. Explicitly configure the future Unity Humanoid Avatar using this mapping, then use that one Avatar for the clips.

| Blender / FBX bone | Unity HumanBodyBones |
|---|---|
| Root | Non-Humanoid motion root |
| Hips | Hips |
| Spine | Spine |
| Chest | Chest |
| Neck | Neck |
| Head | Head |
| Shoulder.L / Shoulder.R | LeftShoulder / RightShoulder |
| UpperArm.L / UpperArm.R | LeftUpperArm / RightUpperArm |
| LowerArm.L / LowerArm.R | LeftLowerArm / RightLowerArm |
| Hand.L / Hand.R | LeftHand / RightHand |
| UpperLeg.L / UpperLeg.R | LeftUpperLeg / RightUpperLeg |
| LowerLeg.L / LowerLeg.R | LeftLowerLeg / RightLowerLeg |
| Foot.L / Foot.R | LeftFoot / RightFoot |
| Toes.L / Toes.R | LeftToes / RightToes |

Hierarchy: Root → Hips → Spine → Chest → Neck → Head; shoulders branch from Chest; arms chain shoulder/upper/lower/hand; legs branch from Hips and chain upper/lower/foot/toes. No separate costume roots in the QA scene. Optional UpperChest, finger, jaw, and eye animation bones are absent. Eyes are meshes weighted rigidly to Head. Identity object scale; meters; hero retains ~1.70 m rest height. FBX uses -Z forward / Y up conversion.

**Humanoid readiness vs verification:** all required humanoid chains are present and Mixamo successfully retargeted both clips. Unity Avatar validation has intentionally not run; this phase does not claim a Unity-validated Avatar.

## Wardrobe and weights

All eight visible meshes use `Hero_01_Rig` and the same bone names. Hair, visor and eye meshes are rigidly Head-weighted. Clothing/body weights were rebuilt together with a continuous spatial blend; exposed arms were identified by connected topology, and their weights were shared with nearby sleeve seams. This fixed sleeve trim stretching across the chest, shoulder spikes, arm/hip cross-influence, and elbow webbing seen with the original provisional weights.

Weights are normalized, with at most four influences per vertex. Preview uses **linear skinning**, not Blender-only dual-quaternion deformation. The hero mesh, face, silhouette, palette, outfit, texture resolution and studio lighting were not regenerated or redesigned.

For runtime assembly, follow the existing `TennisCustomization.AttachBase` idea: build one bone lookup on the body, rebind each selected SkinnedMeshRenderer to that skeleton by bone name, and discard the duplicate serialized armature container from each wardrobe FBX. Do not instantiate a whole independent animated character per slot.

### Coverage and skin tint

- Use `Hero_01_Body_DefaultCoverage.fbx` under the default wardrobe; it matches the visible approved hero.
- `Hero_01_Body.fbx` includes the inset coverage foundation as a future binding/coverage aid. Do not render both body variants together.
- The Blender body’s `Default outfit coverage` Mask modifier stays enabled for the default outfit. It is applied to the default visible exports.
- Torso/scalp/hips/feet underneath the approved clothing are coverage surfaces, not a finished nude/bald/barefoot character. New wardrobe silhouettes need their own coverage QA.
- Skin tint remains available in `Hero_01_SkinTint_Controls` in the blend. FBX uses the original base atlas; the existing runtime tint shader can use the supplied skin-mask alpha and reference JSON. Example tones: `#F2C093`, `#B77950`, `#6A4030`.

## Sockets

| Socket | Parent bone |
|---|---|
| Hat | Head |
| Hand_R | Hand.R |
| Hand_L | Hand.L |
| Back | Chest |
| FaceExtra | Head |

Sockets preserve their approved rest-world placement through the skeleton correction. They inherit the animated parent bone. Hand grip offsets remain provisional until the tennis/golf prop pass. No racket was introduced.

## Verification results

- One armature in the assembled QA scene; every mesh points to it.
- All ten delivered FBXs reimport with an identical 22-bone rest-skeleton signature.
- Zero unweighted vertices; zero incorrect weight sums; maximum four influences.
- 1,180 shared slot-seam points checked; maximum weight difference is zero.
- Both motions sampled every five frames for finite, bounded deformations.
- Ready reimported onto the assembled bind and checked at source frame 38: maximum vertex-position error 0.101 mm across all eight visible meshes (`roundtrip.json`).
- Blender’s FBX reimport adds one frame to the displayed frame numbers (Idle 2–300, Ready 2–77); duration and sampled motion are unchanged.
- Final preview uses the default outfit and linear skinning. Round-trip still: `roundtrip_Ready.png`.

## Known limits / later checks

- The two base motions have been reviewed with the default wardrobe. High-range swings, raised-arm serves, extreme crouches and falls are not validated; they may need additional shoulder/hip corrective weighting.
- Minor stylized inner-elbow compression and sleeve folding remain under the raised-arm Ready pose; there is no detached forearm or long stretched strip in the delivered preview.
- Fingers are modeled but share Hand weights; individual finger grips and facial expression animation are not present.
- Under-clothing body coverage is not suitable for arbitrary revealing wardrobe without further mesh work.
- This is a bind/base-clip delivery, not the final sport-animation or Unity-look approval.

## Existing Unity importer conflict — deliberately not modified

`Unity/Assets/Editor/GolferModelImporter.cs:29` forces `ModelImporterAnimationType.Generic` for StandardCharacters, Golfer, Tennis/Opponents and Tennis/Customization/Player paths. Line 31 also disables animation import for opponents/customization bodies. Dropping these files into those legacy paths would not produce the intended Humanoid setup.

During the approved Unity phase, explicitly give the new hero a Humanoid import route and configure its Avatar from this model; animation assets should copy the same Avatar. Review name filters in `TennisCustomization.AttachBase` when wiring the new slots. Do not work around this by silently using Generic. `TennisCrowdImporter.cs` and `TropicalArenaImporter.cs` also force Generic in their own unrelated paths.

## Reproduction

1. `build_mixamo.py`: import original approved assembly, correct joint depth, bake the two downloaded source animations by evaluated bone matrices.
2. `fix_weights.py`: rebuild normalized shared weights and render diagnostic Idle/Ready stills.
3. `export_mixamo.py`: emit the corrected bind, clips with a skin bind reference and swappable wardrobe FBXs.
4. `verify_mixamo.py`: reopen the final exports and check the shared skeleton / weights / motion bounds.
5. `render_preview.py`, then `package_preview.py`: render and assemble the labeled preview and contact sheet.

**Phase boundary:** stop here for Adnan’s review. UnityLook has not started.
