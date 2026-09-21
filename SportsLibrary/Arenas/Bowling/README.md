# Coastal bowling venue — revised v1

## Inspection and decision

The supplied `bowling lane 3d model.fbx` imports as one mesh: 5,070 vertices, 5,531 polygons, one material, no UV layers, and no texture images. Its world-space bounds are approximately 1.000 × 0.764 × 0.405 in the imported coordinates. These are normalized asset dimensions, not usable bowling-lane dimensions.

The source overview and top renders show transverse strips, distorted/fused pin forms, and a single continuous masking structure. It is not simply an untextured but otherwise usable alley. Recoloring would not fix the layout or produce movable pins.

The revised scene therefore uses clean replacement geometry. The original imported object is preserved in a hidden `00 Source archive` collection, and the original Downloads file is untouched. `source-inspection.blend` and its two PNG renders preserve the inspection result.

## Changes

- Nine parallel modular lanes; center lane is the primary gameplay lane.
- Design dimensions: 1.067 m rolling surface width, 18.288 m foul-line-to-head-pin distance, 0.381 m pin height, 0.3048 m center-to-center pin spacing. These are modeling targets, not a certified competition installation.
- Ninety separately selectable pins in 1–2–3–4 triangular racks, sharing reusable mesh data. Red neck bands and white bodies.
- Concave gutters below the rolling surface; longitudinal dividers; flat approach and rolling decks; alignment dots, foul lines, and aiming arrows.
- Honey maple boards, sea-glass teal pinsetter panels, oval light details, warm wall paneling, coastal openings, palms, and sea geometry.
- Three cameras: rear-player gameplay, venue overview, and pin-rack close-up.
- Concept image packed into the Blender file in a hidden reference collection.

The concept image is a visual target for composition and palette, not a dimensional blueprint. Its incorrect flat pin groups were intentionally not copied. The venue uses nine lanes and more plausible long-lane proportions rather than replicating every image-generation artifact.

## Files

- `coastal-bowling-resort-v1.blend`: editable Blender master with source archive, materials, lights, and cameras.
- `coastal-bowling-resort-v1.fbx`: revised visible mesh geometry, excluding archive/reference/cameras/lights.
- `gameplay.png`, `overview.png`, `pin-rack-detail.png`: Blender renders, not AI-generated mockups.
- `source-inspection.json`, `scene-report.json`: source audit and build details.

## Engine handoff limits

This is an editable visual environment, not an integrated Unity bowling game. The timber uses Blender procedural grain; FBX exports the base material colors but does not reproduce that shader. Bake or replace the wood shader in Unity for visual parity. Lights and the world are Blender-side. Pin collision shapes, mass, physics materials, pin-reset logic, lane colliders, gameplay, LODs, and draw-call optimization still need engine work. The pinsetter facade is visual scenery, not a functional mechanical pinsetter. No character animations were changed.
