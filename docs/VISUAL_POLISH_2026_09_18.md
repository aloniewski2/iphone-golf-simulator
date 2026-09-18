# Existing-app visual polish — September 18, 2026

This is an incremental SceneKit update, not a Unity migration or a claim of finished production art.

## Implemented

- Refined the connected procedural golfer mesh. Clothing seams now split shared triangles at sleeve/waist/neck boundaries, rather than exposing the upper back or producing jagged material edges. Four existing appearance presets remain supported.
- Added shaped fingers/thumbs, an off-white lead glove, smaller neutral-colored eyes, fabric/skin material separation and thinner collar detailing. The original and imported comparison rigs remain selectable as before; this does not promote the experimental imported rig.
- Added club/shot-family fallback motion: compact chip, pitch, bunker and wedge arcs, reduced short-game wrist hinge/heel lift, and steadier head position. Quiet chips and pitches recover without a driver-sized celebration. Sanitized non-finite putter angles.
- Live camera poses and authoritative club endpoints remain unchanged. These improvements do not infer unseen motion or claim better tracking recognition.
- Refined the nine-hole resort terrain grid from 1.5 to 0.8 yards, with separate deep-rough material, restrained turf striping, sand grain/rake detail and earthier lighting/materials.
- Removed detached spherical horizon hills in favor of continuous rolling background terrain. Added irregular tree crowns and less repetitive spacing; kept collision trunks authoritative.
- Fixed background terrain intersecting bunker floors. Feathered only the out-of-bounds outer border into the background mesh. Playable vertex heights still come from the same surface query as simulation.
- Grounded cart paths, tee furniture and vegetation; added clubhouse foundation depth for slopes.
- Scoring, course identities, physical ball size, golf simulation versions, saves and camera tracking are not changed.

## Verification

- Signed iPhone Debug test build and optimized Release build succeeded.
- Host production-logic regression: 5,633 checks passed, including deterministic four-player nine-hole progression (124 synthetic solver inputs, zero capped hole turns).
- Existing free asset provenance and 65-bone imported-mesh weight checks passed.
- Offline SceneKit art review compiled mechanically adapted production sources on macOS. The default mesh has 17,367 vertices; every mesh edge belongs to two triangles and weights remain normalized.
- Reviewed four presets, six swing families and terrain overview renders. Sampled all nine rendered meshes against the production surface query. Mesh sizes are 49,878–181,435 vertices. Mac optimized mesh construction measured 37–212 ms in this run; this is not iPhone frame-rate evidence.
- Added `VisualAssetTests` for seams, terrain/physics agreement, geometry bounds and compact swing/contact behavior. They compile for iPhone; the new XCTest suite has not yet run on a physical device.
- No simulator motion certification, new iPhone session, AirPlay measurement or source-camera recording was performed for this update.

## Remaining qualification and art work

The current golfer is still procedural stylized geometry, not a completed artist-authored replacement. Further silhouette, clothing/face detail, deformation review from all angles, live-pose occlusion handling and animation/impact presentation timing remain. The imported rig is still a development comparison.

The course remains procedurally dressed, not a fully hand-authored resort asset set. Close-up boundary aliasing, vegetation LOD, water motion/reflections and broader environment detail remain candidates for the next art pass. Geometry construction currently occurs at hole load; measure on-device transitions and memory before increasing detail further.

Next device review: stable idle, full backswing/contact/follow-through, both handednesses, short-game motions, all nine hole transitions, phone/TV readability, sustained rendering and tracking performance. Do not treat these build/offline checks as motion or release certification.

## Build locations

- Debug/test: `/private/tmp/golf-visual-polish/Build/Products/Debug-iphoneos/GolfArcade.app`
- Release: `/private/tmp/golf-visual-polish-release/Build/Products/Release-iphoneos/GolfArcade.app`

The app has not been installed on the phone during this update.
