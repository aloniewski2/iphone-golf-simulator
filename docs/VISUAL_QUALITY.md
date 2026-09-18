# Sports-course visual pass — September 17, 2026

The direction is original, rounded, warm sports-game art: readable course features and a friendly golfer, not photorealistic terrain or Nintendo assets. All new meshes and small turf/sky textures are generated locally; there are no downloaded assets or added package dependencies.

## Course

`CourseScene` and `CourseArt` provide shared mown-fairway/green materials, contrasting fringes, smooth distant terrain, rounded deciduous trees and layered evergreens, sky/cloud layers, warm directional lighting with soft shadows, tee markers/signage, cart paths and a distant clubhouse. Bunkers and water retain their original ellipses, with a single low-cost rim mesh per hazard. The curved pin flag moves on the existing display link rather than creating a perpetual independent action.

Terrain elevation starts outside the playable rough. Fairway surfaces follow the original segment-distance capsule rule. Scenery does not mutate `Hole`, lie checks, shot flight, hazard penalties or cup capture. The subsequent routing/scale pass below replaces the enlarged scene ball; the camera-ground confirmation marker remains readable.

## Golfer

`AvatarRig` builds a tapered polo mesh, rounded limbs, sleeve cuffs, collar/placket/buttons/chest detail, hair, ears, eyes/pupils/highlights/brows, nose/smile, a domed cap and brim, a glove, layered golf shoes and a metallic club. Head and torso orientation follow the supplied joint frame. Left-handed mirroring and authoritative club grip/head endpoints are retained.

The rig allocates meshes and nodes once; pose application changes transforms and visibility only. This is still stylized procedural geometry, not a production skinned/artist-sculpted character asset. Camera tracking fidelity is unchanged by the visual pass.

## Runtime safeguards

- Shared deterministic texture/material resources and shared tree templates.
- No turf generation or scene rebuilding in SwiftUI body evaluation.
- Course loading is idempotent; repeated frame updates do not duplicate scenery.
- Navigation owns the camera through a stable holder without observing every pose. Individual camera screens remain live subscribers. This reduces broad SwiftUI invalidation without throttling tracking or scoring events.
- Regression tests bound each hole's scene-node count and verify that repeated avatar poses neither allocate nodes nor introduce non-finite transforms.

A Debug Simulator stack sample during a UI-test transition showed substantial SwiftUI graph/body rebuilding. Removing navigation-level camera observation resolved the previously failing left-handed transition in the targeted retest. Shadow disabling alone did not resolve it; shadows remain enabled. An additional HUD-throttling experiment was reverted because it delayed setup transitions. Do not interpret simulator test timing or a single process CPU sample as a phone performance benchmark.

## Device validation still required

Check a normal-launch 20-minute camera session on actual supported iPhones, including GPU/CPU frame pacing, thermals, memory, dynamic shadows, both handedness modes and a full hole. The target is still responsive camera play; a successful Release build does not certify sustained 60 fps or motion-to-display latency.

## Course routing and physical scale follow-up

All three opening holes are now multi-shot par-4 doglegs. Meadow Run bends right around landing-side bunkers; Pine Bend has a narrower right turn; Cliffwater bends left beside a long lateral lake. Existing par-3 holes still provide direct green approaches. Hole dimensions and hazards remain shared between rendering, the map and lie scoring.

`Hole.recommendedTarget` projects the ball onto the nearest fairway segment and selects the next station, skipping stations already reached. `CourseRound` uses this route target for default heading and club recommendation. Manual targets and fine aim remain supported, with an explicit Follow fairway reset. Best scores use a new routing-version key, retaining old data without comparing scores from different pars.

A compact hole overview opens the existing planning sheet. It shows ball, pin, hazards and intended landing target; target distance is separate from distance to the pin. SwiftUI UI-pattern guidance kept map state local and reused the existing sheet rather than adding another navigation flow.

Rig coordinates remain stable for camera retargeting but render at 0.32 course yards per rig unit. The scene ball has radius 0.0235 yards; the clubhead's long dimension is about 2.6 ball diameters. Shafts, grips, tees and flags are scaled accordingly. A flat locator ring is a visibility aid, not ball geometry or a larger collision zone. The canned shaft addresses the same ball as the camera projection; the solid clubhead is placed behind its contact point so it does not hide the ball at address. Camera address/hero framings use the same scale.

This changes course layouts, default aiming and visual proportions, not the camera swing detector or the shot-physics solver. Camera tracking remains calibrated 2.5D, not measured club-face or world-space depth.
