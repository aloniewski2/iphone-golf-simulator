# Sunward integration — revision 7 / playable rebuild still in progress

This directory accounts for the original 32 paid Higgsfield studies plus three new Seedream terrain materials in `rebuild-ledger.json`. The game uses converted 3D assets and authored skeletal poses; Seedance videos are references, not extracted motion capture. The accepted HD course film is used directly in the menu only.

## Quality correction — September 19

The user rejected the gap between the generated menu film and the playable scene. The prior implementation is a simplified 3D interpretation, **not** a reconstruction of the film's environment at comparable fidelity. Accounting for generation outputs and passing tests do not constitute visual acceptance.

Revision 7 adds level lake planes, shared analytic banks, organic hazard outlines, deeper bunker bowls, playable approach/green-surround mounds, generated turf/sand materials, sculpted canopy prototypes and warmer light. Soil banks are part of the common terrain mesh, not floating overlays. Physics version 5 retains older best scores separately. Phone maps and regenerated course cards use the revised geometry. The film-quality target remains unmet: the rendered scene still needs stronger material/light response, composition, environmental detail and device performance validation.

The swing author now projects one shared grip into both arms' reachable space and transports the elbow bend plane through the follow-through. This removes the elbow flip exposed by dense sampling. Skinning preserves that plane, and good-shot outcomes hold the same authored finish. Tests check 301 poses for each of six families; contact sheets cover both handednesses. Camera input remains measured and is not replaced by authored poses.

The follow-up pass adds length-preserving IK transitions from finish to reactions/idle, angular interpolation of transported elbow and knee bend planes, and a consistent reaction shaft length. A missing reaction no longer snaps the golfer to address. Contact sheets now include the return to idle. Near-ground grass is actual opaque, spatially chunked geometry with a ball-clearance fade; the water-depth attribute is continuous across shared bank vertices. Lower key/fill exposure restores sand detail, weaker normal maps reduce turf checker artifacts, and opaque sphere clouds were removed.

`asset-evaluation.json` records an MCP catalog tree trial (workshop revision 5) rejected after rendering: its faceted silhouette is below the intended quality. No new catalog geometry is shipped. Tripo image-to-3D models are listed, but the installed connector does not expose their required `generate_3d` submission operation; no paid job was submitted. The original revision-3 workshop files remain the source of the bundled imported parts.

Material detail uses [SceneKit normal maps](https://developer.apple.com/documentation/scenekit/scnmaterial/normal), not extra collision geometry. The SwiftUI media guidance keeps the concept film isolated to menu presentation; simulator guidance is used for actual-course captures and navigation/shot regression checks.

## Where the work appears

- The normal golfer is the Sunward rig, including its shaped body, face, cap/visor, hands and shoes. The obsolete preview preference cannot silently select the earlier experimental character.
- Cove, Dune, Orchard and Sunset use the same rig and existing saved appearance schema. Both handednesses remain supported.
- Driver, iron, chip, pitch, bunker and putt use distinct exported hand/club/body landmarks from the reviewed motion studies. Contact remains angle zero and the existing shot event is authoritative.
- The converted fist pump appears after the cup drop, including holed putts. A short close-up shows it without hiding the ball's travel. Reduce Motion and retained legacy live-pose compatibility suppress that cut.
- Bystander knockdowns use the seated recovery conversion. The generated take's broken/disappearing club is not reproduced.
- All nine Sunward holes use imported broadleaf trees and rocks, layered out-of-play groves, warm sand, turquoise ripple water, shoreline grass, tee furniture, a veranda and coordinated daylight. Decorative breeze never changes colliders.
- “Hole flyover” in the course menu tours the actual hole; a smoothed heading prevents dogleg snaps. It cannot spend a stroke or fire a swing. Phone and TV views share the same scene.
- Course selection features Sunward first. Its nine cards are rendered from the real game, not generated imaginary layouts.
- Scene startup primes the course, character and camera before the first animation frame, avoiding the temporary world-origin/empty-sky flash.
- The accepted 1080p film is a muted looping menu background. It does not own an AirPlay route, pauses for settings/background, and releases its queue on navigation. Reduce Motion uses the static game-rendered card.

The phone-controller pivot is preserved: choose club, aim, shape and trajectory on the phone; use a gentle armed motion or touch fallback; show the course on the TV. No camera scan is required.

## Provenance and credits

- Original balance: 3,010. Last verified balance: 2,150. Paid generations: 860 credits (851 original studies + 9 new terrain materials).
- Terrain-material rebuild spending: **9**. Subsequent rig/shader/catalog evaluation pass: **0** (balance rechecked at 2,150).
- Approved generation ceiling: 3,000. The unspent credits have not been used to repeatedly regenerate references.
- `generation-ledger.json`: prompts, model/settings, job IDs and estimates.
- `integration-manifest.json`: complete 32-output disposition and runtime consumers.
- `references/`: preserved original images/videos. Alternative directions and duplicate comparison takes are archived, not misleadingly labeled as 32 separate playable assets.
- `source/SunwardWorkshop.blend` and `.glb`: original Higgsfield 3D Jutsu project `d5134a00-d000-4e27-906f-95dc8dff1d42`, committed revision 3. Read-only API audit found no animation actions. These snapshots do **not** contain the later local body refinements or authored motion library.

## Canonical editable sources and rebuilding

From the repository root:

1. `scripts/build-sunward-assets.py` and `scripts/sunward-rest-pose.json` describe the original Blender workshop. `scripts/import-sunward.mjs` converts its GLB into the version-2 golfmesh contract; the legacy importer remains available.
2. `node scripts/refine-sunward-golfer.mjs` rebuilds the production body while retaining the imported 12-bone bind data. This local script is the canonical later body source: 12,960 vertices / 25,876 triangles.
3. `AvatarRig.swift` authors the face, accessories, distinct club heads and expressions. `SunwardAsset.swift` validates and binds imported materials/meshes.
4. Edit `scripts/SunwardMotionAuthor.swift` and `scripts/ExportSunwardMotion.swift`; run `zsh scripts/export-sunward-motion.sh`. The export contains 13 clips: six swing families, six outcomes, one recovery. Only the six swing families, holed celebration and recovery correspond to generated motion categories; five existing outcome reactions remain.
5. `CourseArt` in `CourseScene.swift` authors terrain-conforming dressing and lighting. The actual terrain/hazard definitions remain authoritative for ball physics.
6. Run `SunwardUpgradeTests/testExportNineCourseCards` on the simulator, then copy the nine exported PNGs from its Documents/SunwardCards folder into `GolfArcade/Resources/Sunward/Cards`.
7. `node scripts/audit-sunward-integration.mjs` verifies every generation, consumer, clip mapping, source file and the menu video's byte-for-byte identity.

The original generation archive is development source only. It is not added to the app target; only selected runtime resources are bundled.

## Verification and limits

Simulator checks cover resource loading, exact contact, connected grips, stable shafts, grounded feet, all looks/hands, short-game amplitudes, recovery, post-cup celebration/camera, menu playback/cleanup, terrain agreement, actual-hole flyover, score retention and phone/TV scene ownership. Four-player nine-hole progression is exercised through the real round engine, including stroke-cap completion; this is not a human playtest of every shot.

Integration is not a claim of pixel-identical Seedance rendering. Real-time geometry, lighting and animation are locally authored conversions with mobile constraints. Generated comparison takes can contain defects, so copying every frame would be incorrect.

No physical phone was operated during this integration pass. Simulator evidence cannot certify iPhone thermal behavior, real motion feel, or AirPlay latency. The 20-minute physical-device/TV acceptance check remains unperformed under the user's simulator-only instruction. An earlier short phone audit is not a substitute for that check.
