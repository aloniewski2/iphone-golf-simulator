# Reference anatomy source

Derived from the official Blender Human Base Meshes bundle v1.4.1. The vendor bundle remains unaltered at `work/reference-rebuild/vendor/human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend`; its embedded README declares the asset CC0.

Male head: `GEO-head_animation_realistic`, with authored iris/sclera meshes. Female head: head region of `GEO-body_female_stylized`, with authored eye spheres. Work sources and staged portrait comparisons remain in `work/reference-rebuild/characters`.

The selected anatomy was adapted toward the approved bald character references: broader and shorter male lower face, larger modeled eyes, female ear/nose reduction, curved physical irises, modeled lips and eyelids, solid brows, and a bounded closed-mouth expression. No painted replacement mouth or nostril geometry was used.

The head is integrated with the canonical Body rest mesh using a continuous neck loft; the original 53 bone names, hierarchy and rest transforms remain. Existing body vertices and weights below the mid-neck cut are unchanged, as recorded in graft-manifest.json. ReferenceFeatures uses Head weights and semantic Sclera/Iris/Pupil/Brow material names.

This pilot has Expression2 baked into its base. Physical Hero_Blink/Hero_Smile targets are pending actual in-game face acceptance; no facial-morph completion is claimed. Full sport/native verification remains pending.

Canonical FBX export explicitly uses scene metre scale1 and `FBX_SCALE_ALL`, matching the original MatchAnims exporter. The initial default-scale pilot was rejected by Unity import because its mesh and bind translations were divided100 while its node carried scale100; the corrected resources supersede that pilot.
