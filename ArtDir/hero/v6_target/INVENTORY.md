# V6-0 baseline / preserved workset

Current source: `ArtDir/hero/v5/Hero_01_V5.blend`, restored V5 from 2026-09-29. Current gameplay prefab: `Unity/Assets/Resources/Tennis/Hero/Hero_01_Tennis.prefab`.

The older `Unity/Assets/ArtDirection/HeroV6` and `ArtDir/hero/v6_full` are unrelated generated-character experiments and were not used or overwritten. This phase uses `v6_polish` and `HeroV6Polish`.

Authority PNGs are copied in this folder. Full brief and binary gates are in ACCEPTANCE.md. Source asset hashes and initial dirty checkout state are recorded separately.

Baseline evidence:
- BEFORE/turn_*.png: fresh Blender default-wardrobe turnaround.
- BEFORE/hair_*.png: close-up angles, including below/behind.
- BEFORE/legs_*.png and stress_*.png: synthetic Blender stress poses; these are explicitly not gameplay animation proof.
- BEFORE/Unity/: fresh Play Mode captures of the shipping prefab's ready, serve, forehand/backhand, running, volley and smash, hair/headwear selections and build values. motion-and-swaps.txt records full-clip numerical sampling. Finite vertices are not proof of good deformation.

Measured pre-change: hair top at 1.83185 m, skin head max Z 1.62589 m, unchanged knee at Z .44 m and ankle at Z .165 m. Current calf widths vary about .164–.196 m across .28–.40 m height, with visibly irregular surfaces. These measurements describe V5; they are not a claim that the target demands thicker legs everywhere.

Existing controls: ModularHeroLook Slot enum Hair/Hat/Shirt/Shorts/Shoes; HeroCosmetics headwear/tool attachments; HeroKit appearance/material controls. Serialized game rig and all clip references must remain intact in the duplicate. No replacement animation generation is part of V6.

Current source has full hidden skin plus an outfit coverage mask. New V6 wardrobe remains independent; new lower skin has a full authoring copy and reversible default-shorts coverage. No default shirt/shorts geometry is baked into Body_Skin.
