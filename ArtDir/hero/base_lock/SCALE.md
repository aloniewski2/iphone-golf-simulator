# Bald base scale and hair attachment contract

The primary HeroBase_Male/Female prefabs are bald. Each complete Body contains its scalp, ears, face and neck. Hair is an independent optional asset; it supplies no missing head surface.

Blender units are metres, body origins are at the foot midpoint, source transforms are identity. FBX scale 1, FBX All, -Z forward/Y up. Unity global/file scales are 1; axis conversion is confined to imported mesh children. Public prefab roots stay at position/rotation zero, scale one. Measured bald heights are recorded in METRICS.md; both are within 1.65–1.75m.

Main prefabs have one body renderer, no hairstyle dependency, and an empty HairRoot. Bodies/ contains independent body prefabs. Hair/ contains separate head-local opaque hairstyle prefabs. Previews/ contains optional assemblies. Instantiate the matching style under HairRoot with local position/rotation zero, scale one. Deleting HairRoot or replacing its style child leaves the same body mesh intact.

| Base | Unity Y-up HairRoot position in metres |
|---|---|
| Male | (0, 1.5795953, 0) |
| Female | (0, 1.5572519, 0) |

New styles must fit the complete head around the matching pivot. Do not merge hair into Body or rely on a hairstyle to cover missing scalp. Future rigging may parent HairRoot to the head transform. The delivered scope is static, with no skeleton, Mixamo, Animator, clothes or clips.

Bald body target is 35–50k triangles. Optional preview assembly counts are in METRICS.md. Head albedo is 2048 sRGB with mipmaps, no Crunch, uncompressed default importer. Pigment contains no hair cap or baked illumination.

Full body topology and reference fidelity are not approved by this technical attachment contract. See proof/GATE_RESULTS.md.
