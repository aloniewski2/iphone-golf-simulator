# Permanent standard characters

## Product decision

The user-supplied **male** and **female** characters are the permanent visual identities for this multi-sport game. They are not temporary stand-ins. Every playable sport, future sport, clothing kit, and crowd system must derive from these standards rather than introducing unrelated default avatars.

| Stable ID | Original identity source | Current implementation |
|---|---|---|
| `standard_male` | [StandardMale.glb](Characters/Originals/StandardMale.glb) | V4 male sport rigs and modular kits in the Blender masters |
| `standard_female` | [StandardFemale.glb](Characters/Originals/StandardFemale.glb) | V4 female sport rigs and modular kits in the Blender masters |

The source GLBs contain multi-view/turnaround geometry, not clean production-ready single-avatar rigs. Preserve them as identity references. Use the derived V4 rigged characters as the starting point for technical export and refinement. Do not import the entire turnaround as a playable character.

## Preserve

**Permanent silhouette: floating hands, no visible shoulders or arms.** Applies to both genders, every playable sport, future sports and standard-derived crowds. Hide upper arms, forearms, shoulder pieces and sleeve geometry; preserve torso, head, legs, hands and equipment. The hidden arm skeleton remains an attachment/animation tool, not visible anatomy. This supersedes earlier arm-restoration experiments.

Unity's shared `StandardCharacterArms` component defaults to this hands-only style, including on newly imported standard rigs. Its legacy `SetFloatingHandsPreview` name is retained for compatibility; disabling it is now a technical inspection option, not the product default. Historical Blender masters/reference renders are preserved archives and can still contain visible arms—new runtime exports must apply the shared visibility standard.

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

Unity golf uses both permanent V4 standards with Golf Kit 1 and the right-handed drive clip. Tennis now has a separate coastal-resort rally prototype using both standard characters, Tennis Kit 1, ready/running/forehand/backhand clips and the standard racket. See [tennis functionality and limitations](TENNIS-GAMEPLAY.md). Full matches, LH selection, additional kits and the other sport controllers remain pending. The original golf primitive model is an emergency missing-asset fallback, not the standard character.
