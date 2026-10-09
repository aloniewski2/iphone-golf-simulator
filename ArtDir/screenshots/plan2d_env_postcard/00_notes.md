# Plan 2D — EnvironmentQuality_CourtPostcard (2026-09-27)

**Status:** review candidate. Stopped for Adnan's approval. Plan 3 not started.

**Scope:** world art only. No changes to the hero, animations, cameras, gameplay or the ultimate.

**Capture:** `ArtDir/anims/captures/Plan2D_EnvPostcard.mp4` (26.5 s): a real match (serve toss, rally, ultimate orbit, reaction cams), then before/after pairs.

**Stills:** `*_before_after.png` and `*_after.png` for 12 camera angles in this folder.

## Audit: what was wrong on camera
| Offender | Where it showed | Cause |
|---|---|---|
| Lumpy grey-green "garden shrubs" | React / orbit cams, left and right lawns | 41 Tripo meshes (6.2k triangles each, 253k total) with a noisy, patchy texture |
| Stone-pine and cypress blobs | Left side, corners | Tripo blobs (12 m flat mushroom canopies) |
| Grey clubhouse (5k triangles) plus 3 vine meshes floating on its wall | Gameplay cam top-right, landmark angles | Low-detail patchy-texture building; the vines were placed for the old facade |
| Purple disc / hard ring at the top of the sky | Serve toss cam | `TennisSky.shader` cut the painting off at about 54° elevation into an off-colour zenith gradient |
| Bare square slab edges | Aerial / flyover | The resort terrace (82 × 67 m) sits on the island with vertical sides |
| Island's own flat canopies | Behind the player in react cams | Part of the island mesh (can't be edited separately), so screened with new trees instead |

## What was swapped or added
**Kit:** `Unity/Assets/Resources/Tennis/EnvV4/`, built by `ArtDir/env_v4/tools/build_env_v4.py` (Blender; source in `ArtDir/env_v4/EnvV4_kit.blend`).
- **Shrubs (3 variants):** `EnvV4_ShrubA`, `B` and `C` (320–500 faces). Chunky clumps, with pink or yellow flowers on two variants. They take the exact spots of the 41 old shrubs.
- **Trees:** `EnvV4_TreeBroadleafA` and `B` (508 / 788 faces) are chunky toy broadleafs on short trunks; `B` has yellow blossoms. `EnvV4_TreeFlowering` (684 faces) is a three-limb tree with pink blossoms.
  - They replace the stone pines and cypress.
  - They also form a staggered double row on the island just outside the terrace, on the south side (behind the player) and the west side (the left-side cams), with one row on the east behind the house. Trees are placed by raycast onto the island surface.
- **Beach house:** `EnvV4_BeachHouse` (1.3k faces), on the old clubhouse footprint, long side along the court, facing it.
  - White stucco, terracotta hip roofs and a lookout tower.
  - Teal shutters with arched windows, coral and white striped awnings.
  - A columned veranda with a terrace railing and planters.
- **Terrace skirt:** a sloped grass apron from the terrace edge down into the island, tinted to the island's olive turf.
- **Sky:** the painting now fades into its own top-edge blue over a wide band, so there's no ring or disc at the zenith.
- **Far headlands and village:** sunk 5 m so they sit in the sea.
- **Code:** `Unity/Assets/Scripts/Tennis/TennisEnvironmentV4.cs` does the swap at load, called once from `TennisGame.Start`. `HeroGameplayBuild.ConfigureEnvV4()` sets the import settings.

## Material / LOD / perf notes
- **One material** for the whole kit: URP Lit plus a 512 × 128 palette atlas (16 gradient cells, no mipmaps, clamped), with GPU instancing. A per-instance tint is set through a MaterialPropertyBlock, so batching isn't broken.
- **Triangles:** 50 renderers hidden (308k triangles); kit added 136 objects (78k triangles). The scene is about 230k triangles lighter. No magenta or error materials.
- **No LODs yet:** every kit mesh is already under 800 faces, so the whole kit costs less than the 13 old shrubs it replaced.
- **Play corridor untouched:** nothing is placed inside the court and runoff (|x| < 13, |z| < 21) or over the ball flight. Court, collision and bounds are unchanged.
- **Locks hold:** player contact gap mean 0.11 m (max 0.13 m); honest contact and camera rules are unchanged.

## Remaining debt
- **Island canopies:** the island mesh's own flat canopies still show in the far distance (screened near the court, not replaced).
- **Old props:** the other Tripo props (hydrangeas, lanterns, parasols, stands) are unchanged. They read fine but are heavy (about 6k triangles each).
- **Beach house detail:** blocky (1.3k faces). The silhouette and palette read well at gameplay and cinematic distance, but not in a full-screen close-up.
- **Lighting:** not re-tuned (the warm key and soft shadows already match). The new art is tinted a notch under the characters so they stay the pop.
- **Device:** not tested on iPhone.
