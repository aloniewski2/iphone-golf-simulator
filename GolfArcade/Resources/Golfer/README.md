# Resort golfer import (development comparison)

Source: Quaternius, Universal Base Characters **Standard / FREE** pack.
- Official listing: https://quaternius.com/packs/universalbasecharacters.html
- Official distribution: https://quaternius.itch.io/universal-base-characters
- Retrieved: 2026-09-17
- License: CC0 1.0, supplied verbatim in `Quaternius-LICENSE.txt`.
- Only the free Standard pack was used. No paid source-kit content is included.

Imported source files: `Base Characters/Godot - UE/Superhero_Male_FullBody.gltf`,
its referenced geometry buffer and `T_Eye_Brown.png`. The offline converter
`scripts/import-golfer.mjs` produces the version-1 mesh contract: 65 bones,
positions, normals, UVs, skin weights and inverse-bind matrices.

Original application work: rig-space conversion, constrained retargeting,
golf palette and existing cap/shoe/accessory geometry. This is a character
proof, **not yet a finished authored golf outfit or animation library**.
The source superhero topology still requires golf-specific visual review and
refinement. Do not promote the comparison renderer just because it imports.

Expected SHA-256:
- ResortGolfer.golfmesh: db638225173261f8cdf64d4a5bbbd32a05ccafb971499aea077e2c1013dc4218
- GolferEyes.png: d08e3356a83211bc6ca21fe3a8e39f4b5c1a3b8f85457fc2c0fb57be09935025
- Quaternius-LICENSE.txt: 0f4beaf0fe360a7732e58bbe3dbf60a2422367fbea60cb9ea4add968f383268e

No reference-video footage, Nintendo assets or third-party audio is bundled.
