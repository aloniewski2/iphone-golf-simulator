# Clothing v2 — garments built as products (2026-10-12)

**Next chat: start with [HANDOFF_ClothingV2_next.md](HANDOFF_ClothingV2_next.md).**

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


## 2026-10-13 — every hero (continuation, see HANDOFF_ClothingV2_next.md)

New builders (all share `py/export2.py` = packed UVs + restricted/own weights + MERGE into `out/<Hero>/unity/manifest.json`, `py/cover.py`, `py/maps.py` with a per-piece `detail` function in `DETAILS`):

| Garment | Script | Run | Notes |
|---|---|---|---|
| shorts / trousers / under-shorts | `py/pants2.py` (designs: `male_shorts`, `golf_male_trousers`, `female_undershorts`) | `shorts/run_shorts.sh`, `golf/run_trousers.sh` | cage: pelvis rings + crotch seam + two leg tubes, SUBSURF, waistband, drawcord + eyelets / belt loops, slanted + back pockets, hem slits, crease field |
| shoes (tennis, golf) | `py/shoe2.py` (`male_tennis`, `female_tennis`, `male_golf`, `female_golf`) | `shoes/run_shoes.sh`, `golf/run_golf_shoes.sh` | parametric last in the foot frame (footprint of the real foot + designed top line), foot-envelope containment, collar roll, tongue, laces + bow, heel tab, midsole + outsole with herringbone tread, golf cleats; plain white, no logos (Adnan, 10-13) |
| socks | `py/sock2.py` (`male_crew`, `female_ankle`) | `shoes/run_shoes.sh` | tight loft round the real leg + rib cuff, pom on the ankle sock |
| tank | `py/tank2.py` | `tank/run_tank.sh` | racerback V-neck: vest blank, implicit armhole / neckline cuts, navy binding field, rib hem band |
| pleated skirt / skort | `py/skirt2.py` | `tank/run_female_bottom.sh`, `golf/run_golf_female_bottom.sh` | equal-width knife pleats with depth, rib waistband (+ belt loops on the skort) + tight under-shorts, one `Kit_Bottom` |
| belt | `py/belt2.py` | `golf/run_belt.sh` | strap cast onto the fitted waistband, buckle, tail with holes, keeper |
| golf glove | `py/glove2.py` | `golf/run_gloves.sh` | tight leather glove on the real hand + wrist strap; role `Kit_Glove` (white, not the shirt colour) |
| polos | `py/polo2.py` (designs `tennis_male`, `golf_male`, `golf_female`) | `polo/run_polo_any.sh`, `polo/run_polo_post.sh` | golf female uses tube sleeves along the measured arm + ellipsoid armholes (the arms hang nearly straight) |

`run_all_post.sh` = cover + maps for the four heroes after any export. Installer fix: a newly created mesh asset lost its "KitV2 " name prefix (CreateAsset renames it to the file name), so the first run of a hero showed the OLD garment with the new weights; the installer now restores the prefix.


## 2026-10-13 (evening) — shoes, golf polo/trousers, female tank straps ("fixes" pass)

Targets: `ArtDir/hero/targets-2026-10-13/clothing-v2-fixes/` (05 = shoes). Review sheets: `ArtDir/hero/clothing-v2-review/fixes/`.

| Fix | What changed |
|---|---|
| **Shoes (all heroes)** | `py/shoe2.py` rewritten around a DESIGNED last: the collar/ankle half still follows the real leg + sock (hull + `sock_extra`), the front half (vamp, toe box) is a designed low profile (`vamp` knots per design) because the foot skin there is hidden by `cover.py` (whole foot below the collar zone). Tongue = dome on the designed top line, ONE continuous padded rim (collar roll -> eyestay), eyelets, real laces, heel tab, toe bumper + counter + eyestay + stripe relief (smooth field lifts), real tread geometry (tennis chevrons) / 11 cleats (golf), outsole + tread follow the shoe TRIM colour (no more off-colour `Kit_Sole`), cleats + eyelets = hardware role. Trim fields (`f_stripe`, `f_chev`, `tr_*`) drive navy-example stripes / heel chevron in `maps.py`; golf gets a hex perforation panel. Containment is checked (`py/shoe_check.py`: signed distance + ray parity). Socks (`py/sock2.py`) no longer wrap the instep/forefoot (`front_cut`). |
| **Golf female polo** | `polo2.py` `golf_female`: hem ring 0.986 (the skort waistband top is 0.990, belt 0.958–0.984) so no skin shows; stray degenerate polygon (one fin through the skort) removed by the generic long-edge face cull after the fit. |
| **Golf male trousers** | `pants2.py` `golf_male_trousers`: leg rings tapered knee -> ankle (a .089/.077/.067/.060/.057, b .108…), hem 11.8 cm above the sole plane with a slanted hem (`hem_slant` .026: back lower than front) so it rests on the shoe instead of flaring round it. |
| **Female tank straps** | `tank2.py`: the racerback armhole ellipsoids used to cut the whole shoulder, so the straps ended in loose tabs. A strap keep-band (`strap_x`, top-facing normals, smooth union) now protects a continuous strap from the neckline over the traps into the back panel; the binding is real geometry (7.5 mm ribbon sewn along every opening, `face_kind` 15 -> trim channel) instead of a texture band; solidify without even-offset (it threw spikes). |

Fast loop (seconds, not minutes): `scratch2/server.py` is a persistent headless Blender with the four heroes preloaded; `scratch2/trypar.sh <tag> <hero> <design> <side>` (shoes) and `scratch2/tank_try.sh <tag>` build and render a sheet in ~5 s (`SHOE_FAST=1` previews the trim colours from vertex fields). `scratch2/final.sh` is the one-shot full pass (build, export, skin masks, maps, correctives, Unity turnaround). Sheet maker: `py/compose_sheet.py`.
