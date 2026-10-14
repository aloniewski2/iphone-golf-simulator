# Bald bases and independent optional hairstyles

The primary `HeroBase_Male.prefab` and `HeroBase_Female.prefab` are bald. Their reference plates are the user's replacement bald images supplied on 2026-09-30. Each main prefab nests its complete body and an empty `HairRoot` attachment transform. It has one body renderer and no hairstyle dependency.

`Bodies/HeroBase_Male_Body.prefab` and `Bodies/HeroBase_Female_Body.prefab` are independent body assets with complete scalp, ears, face and neck. Body-only FBXs contain no hair mesh or hair material. Public prefab roots have position/rotation zero and scale one; FBX axis conversion is confined to internal mesh children.

`Hair/Hair_Default_Male.prefab` and `Hair/Hair_Default_Female.prefab` are separate optional opaque hairstyle assets. To add hair, instantiate the matching hairstyle under HairRoot with local position/rotation zero and scale one. Delete or swap only that hairstyle child. The body mesh does not change. `Previews/` contains optional hairstyle assembly previews; these are not the base-reference proof prefabs.

Unity Y-up hair pivots in metres:

| Base | HairRoot position |
|---|---|
| Male | (0, 1.5795953, 0) |
| Female | (0, 1.5572519, 0) |

Fit new hairstyles around the matching head shape and pivot. These static models have no skeleton, Mixamo, Animator or clothes. Future rigging should parent the attachment to the head transform without merging hair into the body.

The overall plate-fidelity gate remains FAIL. See `ArtDir/hero/base_lock/proof/GATE_RESULTS.md` in the project.

The male base uses clean skin without stubble so the jaw, chin and neck can be assessed from their forms. The female head geometry is preserved. Full-body proofs include eight views every 45 degrees around each base.

For the same shading as the proof captures, open `HeroBase_Studio.unity` and select `Rendering/HeroBaseStudioURP.asset` as the active Quality render pipeline. It includes a dedicated renderer with full-resolution SSAO at a 0.025m radius, 12 samples, intensity 0.75 and 4x MSAA. The capture tool restores the previous Quality pipeline after it finishes. The studio asset is included in the candidate package.
