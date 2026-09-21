# Sports character animation mockup library

Two supplied character designs, with matching animation reference coverage for golf, bowling, tennis, and boxing. This package is concept art only. It does not modify the game, rig the characters, or produce playable animation clips.

## Identity and customization

The male and female GLBs remain the identity sources. Keep the oversized blank rounded-square heads, rounded hands, tan skin, black shirts and trousers, and white shoes. Do not infer facial expressions: emotion is communicated through posture. Clothing shown here is reference clothing, not a permanent wardrobe restriction. Future clothing should be separate, fitted meshes sharing the eventual character rig.

## Contents

- `gameplay-*` folders: PRIMARY behind-character third-person gameplay sheets for both characters in all four sports. The gameplay camera stays behind the player, looking toward the fairway, pins, net, or opponent. These take priority over the studio studies below.
- `01-model-references/`: direct renders of supplied GLB geometry. Each source contains multiple character copies in a turnaround arrangement; camera views show that geometry, not a single isolated rig.
- Sport folders: three 12-panel sheets per character per sport (72 concept panels per sport).
- `shared/`: movement and body-language sheets for each character (48 panels).
- `generation-specification.json`: exact common prompt, character source paths, and panel-by-panel generation instructions.
- `*.prompt.txt`: complete prompts for each generated sheet, including revision prompts where applicable.
- `gameplay-camera-specification.json`: behind-player camera briefs and gameplay panel descriptions.
- `ANIMATION-HANDOFF.md`: how to use the references, known concept-art limitations, and rig-stage checks.
- `asset-manifest.json`: selected images, dimensions, hashes, and source-model audit.
- `index.html`: local visual browsing catalog, populated with completed images.

## Intended use

Use these sheets to communicate silhouettes, action phases, equipment interaction, and animation state coverage. Numbered panels are key-pose concepts, not uniform time samples. Timing, easing, foot planting, hand constraints, collisions, handedness, and ball contact must be authored and validated on a rig in the engine. Never derive precise joint rotations or contact coordinates from generated pixels.

Start an action from its ready pose, establish anticipation, pass through the contact/release event, finish, then blend into recovery/idle. Lock planted feet and constrained hands during contact. Preserve proportions between shots. Mirrored poses need equipment, stance, and contact review rather than a blind image flip.

## Scope

The delivered set contains 8 primary gameplay sheets / 96 panel concepts and 28 supporting studio sheets / 336 panel concepts, plus eight direct model renders. This is broad baseline coverage, not a literal claim to contain every possible animation. New rules, equipment, outfits, camera angles, injuries, celebrations, or sports may require additional references. The catalog lists only images actually saved. It opens on the primary behind-player gameplay view; switch to supporting studies to inspect other poses.

## Sources and production

Model inputs: `human figure 3d model.glb` (male) and `stylized humanoid character 3d model.glb` (female), supplied by the user. References rendered locally with Three.js. Concept sheets generated using the built-in image generation tool and the saved specification. Original GLBs remain unchanged in Downloads.
