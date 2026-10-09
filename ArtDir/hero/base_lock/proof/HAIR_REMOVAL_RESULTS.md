GATE: REMOVABLE HAIR PASS

Both primary HeroBase prefabs are bald by default, matching the replacement reference scope.

| Base | Independent body FBX | Unity primary and body-only prefab | Scalp topology | Bald height |
|---|---|---|---|---|
| Male | Body_M only; no hair material | main/base: one renderer, zero hairstyle dependencies | 0 open / 0 nonmanifold scalp edges | 1.704300 m |
| Female | Body_F only; no hair material | main/base: one renderer, zero hairstyle dependencies | 0 open / 0 nonmanifold scalp edges | 1.703894 m |

The actual Unity deletion check removes HairRoot and retains the same body mesh. Independently instantiated bald main/base prefabs and body-only prefabs produce identical native images and geometry masks in all three views. Front, back and three-quarter scalp proofs show complete heads without a hairstyle. Source, independently imported FBX and Unity body counts agree.

Use HeroBase_Male/Female.prefab for the bald base. Optional styles are under Hair/ and optional assemblies under Previews/. Attachment coordinates are in SCALE.md and the Unity README.

The overall BASE gate remains FAIL for reference silhouettes, facial fidelity and shading.
