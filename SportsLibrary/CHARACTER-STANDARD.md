# Permanent standard characters

## Product decision

The user-supplied **male** and **female** characters are the permanent visual identities for this multi-sport game. They are not temporary stand-ins. Every playable sport, future sport, clothing kit, and crowd system must derive from these standards rather than introducing unrelated default avatars.

| Stable ID | Original identity source | Current implementation |
|---|---|---|
| `standard_male` | [StandardMale.glb](Characters/Originals/StandardMale.glb) | V4 male sport rigs and modular kits in the Blender masters |
| `standard_female` | [StandardFemale.glb](Characters/Originals/StandardFemale.glb) | V4 female sport rigs and modular kits in the Blender masters |

The source GLBs contain multi-view/turnaround geometry, not clean production-ready single-avatar rigs. Preserve them as identity references. Use the derived V4 rigged characters as the starting point for technical export and refinement. Do not import the entire turnaround as a playable character.

## Preserve

- Rounded oversized heads, familiar face treatment, compact stylized proportions, and the existing soft resort-game aesthetic.
- The same identities across golf, tennis, bowling, boxing, and all future sports.
- Shared standard-derived crowd characters, varied through skin tone and clothing colors.
- Separate clothing and equipment instead of baking a particular sport outfit into the permanent identity.

## Customization and handedness

Skin and outfit colors are customizable now. More character customization can be added later without replacing the standard identities. The established palette includes navy, teal, ivory, sand, coral, and champagne accents; it is a default, not a restriction on color customization.

Handedness is a player setting independent of male/female choice. Preserve RH and LH actions. A tennis backhand does not automatically transfer the racket to the other hand; make grip and hand changes intentional and explicitly animated. Bowling release must detach the ball at the gameplay release event, not merely when an offline preview trajectory starts.

## What can change

Topology, skin weights, rig structure, finger articulation, UVs, LODs, clothing fit, animation curves, and performance implementation can improve while preserving recognizable identity. The 22-bone V4 rig and current action timing are **not** declared immutable. Coordinate rig changes with all dependent clothing, clips, attachments, and both character variants.

Substituting another character design or changing the recognizable identity requires the project owner's explicit approval. Do not interpret incomplete animation polish as permission to replace the character standards.

## Runtime adoption status

The existing Unity golfer remains procedural in this commit. This document establishes which assets must replace it during integration; it does not claim that replacement has already happened. Keep the game functioning until the standard-character import is validated, then migrate the runtime visual layer without rewriting unrelated input or shot logic.
