# Boxing arena — revised v1

## Source inspection

The supplied boxing FBX imports as one mesh with 4,868 vertices, 4,756 polygons, one material, no UV layers, and no texture images. Its world-space bounds are approximately 0.717 × 1.000 × 0.172 in normalized import coordinates. It is visibly rectangular rather than square. The recognizable platform, four pads, posts, and ropes make this a more usable starting point than the bowling source.

The Downloads original is untouched. An exact imported source object is preserved in the hidden `00 Original FBX` collection. `source-inspection.blend` and the source PNGs preserve the before state.

## Adaptation

- Retained the original platform mesh and four corner-pad components, transformed to square-plan proportions and assigned materials.
- Added a flat cyan canvas and clean deep-blue apron surfaces.
- Replaced the source posts and ropes with four round posts, turnbuckle connectors, and sixteen individually editable rope runs, plus rope separators.
- Ring design: 6.32 m square between rope centerlines, on a 7.4 m square platform. These are project design dimensions, not a certified competition installation.
- Red/white corner pads and red/white/blue/white rope colors follow the latest concept.
- Added surrounding stepped seating, simplified stylized audience figures, aisle lighting, walls, carpet, and focused ring lighting.
- Main camera is elevated outside the near side of the ring, centered toward the opposite side. It is not a first-person or inside-ring camera.
- Hidden packed concept image included for reference.

## Files and scenes

- `boxing-arena-v1.blend`: editable master. Main scene `BOXING | full venue` retains every rope.
- Alternate linked scene `BOXING | clear gameplay preview`: only near-side ropes/separators excluded for visibility demonstration. This is not an implemented dynamic fade system.
- `boxing-arena-v1.fbx`: visible venue geometry and materials, including all ropes, excluding source archive and reference.
- `gameplay.png`: full-ring exterior gameplay view.
- `gameplay-clear-view.png`: same camera with near ropes hidden.
- `arena-overview.png` and `corner-detail.png`: alternate Blender renders.
- `source-inspection.json`, `scene-report.json`, `validation.json`: audit/build/check results.

## Unity handoff limits

This is an editable visual scene. It does not include game integration, fighter animations, collision setup, cloth/rope physics, dynamic rope fading, LODs, crowd optimization, or light baking. The audience figures are simple environment placeholders, not the user's playable characters. FBX transfers mesh geometry and base materials; Blender lighting, camera setup, and the alternate scene need equivalent Unity setup. Only one full-rope venue is exported to FBX.
