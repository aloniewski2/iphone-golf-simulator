# Clothing v2 — garments built as products (2026-10-12)

Adnan's process: build each garment on its own like a real product (panels, collar, placket, cuffs, hem with thickness), look at it
off the body, then fit it to the base, copy weights, check poses, keep it on its slot. One garment at a time: **polo done (male tennis),
then shorts, then shoes**. Stage only.

The first attempt in this folder (`py/tops.py`, `bottoms.py`, `shoes.py`, `designs.py`, `build.py`, `finalize.py`, `run.sh`) derived the
cloth from the body surface pushed outward. Adnan rejected it ("bigger skin"). It is kept for reference only and is not installed.

## Polo (male tennis) — `polo/run_polo.sh`

| Step | Script | What it does |
|---|---|---|
| 1 build | `py/polo2.py` | Low-poly cage (front panel, back panel, two sleeves, shoulder roof, neckline) placed from a few measurements (shoulder points, neck base, arm axis) plus the shirt's own girths; Catmull-Clark x3; pattern UVs unwrapped on the cage (seams at the side, shoulder and armhole lines + the sleeve underarm). Construction as separate geometry: collar (stand, roll, fall, pointed ends), placket + 2 buttons, rib cuffs, turned hem (proud band), side vents. Soft folds at the waist sides and underarms. Thickness 2.5 mm (collar 2.8, cuffs 3, placket 2) with bevelled rims; the inner wall is kept 6 cm in from every opening. |
| 2 fit | `py/fit.py` | Only where the shirt comes closer than 6 mm to the skin (or to the shorts under it) is it moved out, by a smooth field spread over the cloth. No body shape is copied. |
| 3 export | `py/polo2_export.py` | Packed UVs (average island scale), weights copied from the body (nearest skin; the sleeves follow their arm only, smoothed 8x), the `.bin` + manifest the Unity installer reads. `polo/polo_v2_final.blend` = the exported mesh. |
| 4 skin | `py/cover.py`, `py/correctives.py` | Skin that stays deep under the shirt in every pose is not drawn; soft contact shadow on the skin at the garment's edges; 24 pose correctives (blend shapes) that lift the cloth where skin would poke through. |
| 5 maps | `py/maps.py`, `py/atlas.py` | N / M maps from the garment's own fields: seams + topstitching, twin-needle hem, rib cuffs + tipping, collar tipping, placket box stitch, buttons; AO against the body. Fabric atlas: pique, rib, twill, leather. |
| 6 Unity | `Unity/Assets/Editor/ClothingV2Install.cs` | Replaces only the pieces in the manifest (Kit_Top) on the prefab, same renderer name and slot, role material Kit_Shirt; colour picks unchanged. Run in an APFS clone (`unity_iter.sh`), then copy the assets into the repo. |

Runtime: `MatchHeroLook` treats any mesh named `KitV2 …` as a finished garment (no old polo repairs, LOD swap or coverage mask for it),
binds its arm support bones, applies the skin cover + contact shadow, and `KitV2PoseCorrectives` blends the pose correctives.

Review sheets: `ArtDir/hero/clothing-v2-review/polo_1_alone.png`, `polo_2_on_character_vs_target.png`, `polo_3_poses.png`.
Checks: `py/poke.py` (skin through cloth over every clip), `py/cross2.py` (arm sunk in the shirt), `py/pokesum.py`.

Lobby (SceneKit): `python3 work/clothing-v2/lobby/compress_maps.py` (the Swift garment shader samples .ktx; without them it stops with
"Incomplete garment maps"), then `work/hero-sync/sync_heroes.sh`. The body of a hero wearing a V2 garment carries 2 extra texture channels
(the contact shadow, `edgeOffset`); `LockerMirrorTests.testTextureChannelsMatchTheirDeclaredMaterialRole` and
`VisualOverhaulNativeTests.testOriginalSurfaceAndBlinkContractAcrossAllShippedVariants` expect exactly that.
