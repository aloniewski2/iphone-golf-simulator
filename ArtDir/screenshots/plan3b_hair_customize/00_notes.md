# Plan 3B — HeroV4_HairAndCustomizeSafe (2026-09-27)

**Status:** review candidate. Stopped for Adnan's approval.

## Root causes of the see-through hair
1. **Locker (the screenshot's horizontal gap):** the hair shell is a thin mesh drawn two-sided in Unity (`_Cull: 0`). The locker drew it one-sided, so the inward-facing parts of the shell vanished and the shelves showed through. Dark hair made it obvious.
   - Fix: the locker draws both sides of every hero material, matching Unity (`CharacterModelPreview.swift`).
2. **Ears / nape:** the body mesh carries a baked side-hair layer whose texture is patchy skin, so it read as holes around the ears.
   - Fix: that layer now uses the hair material. The scalp under the hair is tinted to shadowed hair colour.
3. **Band hats (visor, sweatband):** the ring stands a few mm off the hair, and the sky showed through the slit behind it.
   - Fix: a hair-coloured liner (tube plus top cap) is fitted inside each band hat from the band's own shape, rigid on the head bone (`HeroCosmetics.BuildHatLiner`).
   - Sweatband see-through dropped from 17,301 to 4,438 px across 120 isolated renders.
   - What's left is the normal arm-to-head gap during the smash and whiff.
4. **Eyes through the back of the head:** the head occluder's inset was in 100×-scaled bone units (collapsed). The eyes are also now hidden per camera from behind.
5. **Shirt "crackle" lines when recoloured:** mask islands didn't cover texture-filter padding.
   - Fix: the shirt and shorts regions are grown by 4 px and the shaded creases are included (`HeroLockerExport.BuildMask`).

## Verified
- **Locker (iPhone 17 Pro simulator), black hair:** None, Visor, Cap and Sweatband are all solid (`hw_all` in the conversation).
- **Unity:** isolated magenta-background orbit, 5 poses × 24 angles × 4 headwear. No head holes except the raised-arm gap.
- **Racket seat / two-hand grip:** unchanged. No slot touches the Hand_R socket or its bones.

## Assets touched
- `Unity/Assets/Scripts/Tennis/`:
  - `HeroCosmetics.cs` (hat liner)
  - `HeroKit.cs` (liner follows headwear, lid tint)
  - `HeroTennisDriver.cs` (SealHeadUnderHair, scalp tone, liner on build)
  - `HeroHeadOccluder.cs` (units)
  - `HeroFace.cs` (back-view culling)
- `Unity/Assets/Editor/HeroLockerExport.cs`: mask dilation, liners exported per hat.
- `GolfArcade/Unity/CharacterModelPreview.swift`: double-sided materials, fixed camera distance.
- `GolfArcade/Unity/CharacterAssets/HeroV4*`: re-exported.

## Remaining cosmetic debt
- **Top and bottom are recolour slots:** there are no alternative shirt or short meshes yet, so a mesh swap for those isn't tested. Hats are the only mesh swap today.
- **Hair is one mesh:** hairstyle variety needs new hair meshes built to the slot guide.
- **Cap brim:** it clears the hair but is chunky and oversized.
- **Armpit / shoulder pinch (P2):** not touched.
