# Tailored tennis clothing — 2026-10-08

Scope: accepted male polo, female polo and female skirt. Clothing color is customizable. Reference hues are examples, never a fidelity gate. Stop after these garments.

The shipped polo geometry is the baked mesh in `Unity/Assets/Resources/Tennis/KitsTailored/*Polo*.asset`, with corresponding `*Controls.json`. The FBX/Blender meshes are authoring inputs; the female baked mesh additionally installs the transferred rest surface. Do not replace the baked female mesh with the raw FBX and expect the same fit.

## Shared system

`TailoredPoloDeformation` drives fixed morph weights from the existing skeleton matrices. It is used by both sexes, Unity gameplay, full/distance garment selection, and native export. Full/distance assets retain their exact bone palette order. The male approved collar, placket, two buttons, cuffs and neutral fine weave are reused.

Female transfer is performed in each bone's local frame before the weights are blended. Applying the female rest bend twice creates the collapsed sleeve streaks. The female fit adds sleeve clearance and a hips-following hem with overlap over the skirt waistband. The skirt keeps its original garment bindings and undershorts, with authored pleats, eased outline and a turned hem.

Runtime palette roles remain Shirt, Shorts and ShortsBand; the neutral weave is independent of hue. Body/head/face/hair/skin, skeleton, source animation, maps, lights and cameras were not authored in this clothing pass.

## Rebuild and verification

`TailoredPoloAssetBake.Run` writes binary meshes from full/distance matrix profiles. Production playback loads these baked resources, so no authoring files in `work/` are required to run the game. The exact local reproduction inputs/scripts remain under `work/character-finish32` and `work/character-female33`: female final profiles are `matrix-direct-fit-profile.json` and `matrix-direct-fit-distance-profile.json`; do not use rejected experiments in those directories.

Native assets were merged garment-only over the current shipped buffers, retaining nongarment bytes and all original bone/rigid tracks. The golden fixtures replace only the changed garment samples. See `proof/GATE_RESULTS.md` and the recorded adoption/parity evidence.
