# Hero V6 Polish customization contract

Candidate only. Not promoted over V5. Existing face coordinates and bone rest matrices are preserved. Overall gate: FAIL; see GATE_RESULTS.md.

Prefab: `Unity/Assets/ArtDirection/HeroV6Polish/Hero_V6_Polish.prefab`
Authoring: `ArtDir/hero/v6_polish/Hero_V6_Polish.blend`

| Product slot | Existing runtime slot | V6 asset | Notes |
|---|---|---|---|
| Hair | Slot_Hair | Hero_V6_Hair.prefab | Opaque fitted and free variants; Hair_Bald is an empty selection, restored source skull remains on body; neck join currently fails |
| Visor | Slot_Hat | Hero_V6_Hat.prefab | Existing headwear enum unchanged; legacy cap/band retained for regression, not rebuilt |
| Top | Slot_Shirt | Hero_V6_Shirt.prefab | Same skeleton, original shoulder weighting with torso-side correction |
| Bottom | Slot_Shorts | Hero_V6_Shorts.prefab | Separate quad garment and orange piping |
| Shoes | Slot_Shoes | Hero_V6_Shoes.prefab | Existing textured shoe shape narrowed 7% around foot centers |
| Racket | ModularHeroLook.racketGrip | Original serialized racket | Grip bone, local transform and animation wiring retained |
| Skin / face | Hero_V6_Body/Body_Skin | Hero_V6_Polish.fbx | Skin only, plus existing eyes; default coverage exported from reversible authoring masks |

Aliases Hair/Visor/Top/Bottom/Shoes live inside their Slot_* containers. Racket alias is under the preserved racketGrip. Use the existing Equip method. The WardrobeChanged event now refreshes the V6 contract automatically after direct replacements. The same candidate prefab is used for review/customization and sports motion.

Slim/default/broad range is ±12% bounded radial displacement, applied to body, legs, shirt, shorts and piping. Bone positions, head geometry, hand positions and gameplay reach do not scale. No claim of all possible shape combinations passing is made without the proof gate.

Hair LOD0/1/2 use opaque closed meshes. HeroV6PolishHairLOD swaps meshes at distance without a transparency fade. Body/outfit retain their geometry at distance in this candidate to retain morph compatibility; this is not a fully optimized mobile LOD pack. Existing cap/band and future wardrobe need their own fitting/LOD acceptance.

Body-only authoring contains no clothing. Body_Legs_Full preserves the unmasked lower skin. The current export uses a default-coverage Body_Skin; outfits exposing more skin require an appropriate coverage export, not reuse of the shorts mask. Upper/lower skin join remains a review item.

Mesh FBX is imported as Generic data only. Its meshes are rebound to the original, valid Humanoid Avatar in the duplicated gameplay prefab. The animation driver and clip bindings remain the project's original ones. GolferModelImporter was not rewritten.

## Verified active runtime alias paths

- Hair: `Hero_01_Mixamo_Bind/Slot_Hair/Hair`
- Visor: `Hero_01_Mixamo_Bind/Slot_Hat/Visor`
- Top: `Hero_01_Mixamo_Bind/Slot_Shirt/Top`
- Bottom: `Hero_01_Mixamo_Bind/Slot_Shorts/Bottom`
- Shoes: `Hero_01_Mixamo_Bind/Slot_Shoes/Shoes`
- Racket: `Hero_01_Mixamo_Bind/Hero_01_Rig/Root/Hips/Spine/Chest/Shoulder.R/UpperArm.R/LowerArm.R/Hand.R/Hero_Racket_GripSocket/Racket`

Slot aliases are grouping anchors. Skinned wardrobe attaches through the original skeleton, not by rigid-parenting clothes to these aliases. Future rigid props use the preserved hand grip. The active-container check avoids attaching aliases to old, disabled containers awaiting destruction. The V6 adapter subscribes to ModularHeroLook.WardrobeChanged, so direct Equip updates hooks automatically.

Production continuation: V6 now reuses one kit render texture and owned material copies, and skips rebuilding unchanged headwear. The foundation4 review verifies 25 repeated customization calls keep the same atlas and material count. New shorts base tint and default reset are verified there. The skin foundation and overall visual gates remain incomplete.
