# Hero Base static scale and modularity contract

Blender uses metres, body origin at foot midpoint, identity source transforms. FBX uses scale 1, FBX All, -Z forward/Y up. Unity global and file scales are 1. Axis conversion appears on imported mesh children; prefab roots remain identity. Actual default-hair and body-only heights are in METRICS.md.

The complete scalp, ears and face belong to Body_M/Body_F. Hair belongs to independent hairstyle assets. Body-only FBXs and Bodies/ prefabs contain no Hair mesh, material, or hairstyle dependency. Assembled prefabs add an optional HairRoot with a separate nested hairstyle prefab. Remove HairRoot or swap only its child; the body mesh stays unchanged.

Default styles are exported in head-local coordinates. Unity Y-up pivots: male (0,1.4881459,0), female (0,1.5180602,0) metres. New styles must be fitted to their chosen head, around the corresponding pivot; child transform position/rotation zero, scale one. Hair is closed opaque solid volume, URP Lit. Pigment atlases have no baked hair cap or directional lighting.

Current scope is static: no Mixamo, skeleton, Animator, clips or clothes. Future rigging should attach HairRoot to the head transform. It does not require merging the hair into the body.

LOD0 target is 35–50k triangles with default hair. Later hand-authored LOD1 18–25k and LOD2 8–12k are suggestions, not delivered now. Preserve face, scalp and hair forms, and reverify at each LOD. Head albedo is 2048, sRGB, mipmapped, Crunch off, default uncompressed.

A third body needs its own locked plates, complete scalp and measured head pivot. Reuse the separation and attachment contract; fit hair to the head shape. Reverify plate and Unity gates.
