# Hero accessories (2026-10-13)

Basic accessories as **separate, swappable items**: tennis cap, golf cap, left-hand golf glove, golf carry bag. Body, head, face, clothes, rig and animations are untouched.
References: `ArtDir/hero/targets-2026-10-13/accessories/` (01–04 product sheets, 05–06 fit sheets, README rules). Stage only: nothing is committed.

## What exists

| Item | Slot | Piece (renderer) | Attached to | Default |
|---|---|---|---|---|
| `tennis_cap` | Headwear | `Acc_Cap` | socket `Headwear` under `Head` | tennis kit heroes (match, plaza) |
| `golf_cap` | Headwear | `Acc_Cap` | socket `Headwear` under `Head` | golf kit heroes |
| `golf_glove` | Glove | `Acc_GloveL` (+ mirrored `Acc_GloveR` for a left-hander) | hand + finger bones, weights copied from the body hand | golf kit heroes (replaces the old kit glove) |
| `golf_bag` | BackCarry | `Acc_Bag` | socket `Back_Bag` under `UpperChest`, strap over the right shoulder | only where the game asks: plaza, between golf shots |

A second empty socket `HairRoot` (under `Head`) is created next to `Headwear` for the hair that comes later; the cap never changes for hair, the hair is squashed / hidden under it.

## Runtime (Unity)
- `Unity/Assets/Scripts/Tennis/HeroAccessories.cs`: one component per hero (added by `MatchHeroLook.OwnMaterials`, so every tool and the game get it; **no prefab is edited**).
  `Equip(slot, id)`, `Unequip(slot)`, `SetVisible(slot, on)`, `SetColours(slot, base, trim)`, `SetCarry(bool)` (bag), `SetGolfHand(bool)` (glove side), `Item(slot)`.
  Defaults by kit (see table); `ACC_DEFAULTS=0` turns them off for A/B captures, `ACC_ITEMS=a,b` replaces the list.
- `MatchHeroLook.AccessoryCloth / AccessoryMetal`: the same TennisCloth look as a kit V2 garment (fabric atlas v2, the piece's own N/M maps, base + trim colour mixed by M.B); `ClothFor` has `Acc_Cap / Acc_Glove / Acc_Bag`.
- Bag hooks: `GolferView.SetBagCarried` (called from `GolfGame.Enter`: carried in Result / HoleDone / RoundDone only), `HubPlayer.SetHero` and `HubRemotes.Spawn` (plaza: carried).
- Data: `Unity/Assets/Resources/Accessories/<Sex>/<item>/` (`<Piece>.asset` mesh with weights, `item.json`, `Acc_<Sex>_<item>_<Tag>_N|M.png`), installed by `Editor/AccessoriesInstall.cs` from `out/`.
- Lobby: the exporter (`MatchHeroLockerExport.Collect`) lists enabled `Acc_*` pieces like kit parts; `lobby/compress_maps.py` makes the `.ktx` the Swift shader samples; `lobby/update_look_sources.py` keeps the freshness guard list current; then `work/hero-sync/sync_heroes.sh`.

## Build chain (Blender 5.2 headless, `py/`)
`cap.py` (product) → `cap_export.py` (unwrap, pack, fit to the head with a socket pose + a smooth hug of the skull, relief pass, write, maps via `cap_maps.py` + `acc_canvas.py`) →
`prep_install.py` (→ `unity_item.json`) → `unity_iter.sh install` (APFS clone, never the shared project) → `copyback.sh`.
Glove and bag follow the same contract (`AGENT_BRIEF_*.md`). Frames: `acc_common.py` docstring (N frame, hero's left = +X).

```bash
work/accessories/py/…                                     # blender -b --factory-startup --python py/cap_export.py -- tennis both
python3 work/accessories/py/prep_install.py
work/accessories/unity_iter.sh install
work/accessories/unity_iter.sh proof <outdir> MatchHero_Male,...   # ACC_PROOF_SHOTS / ACC_PROOF_POSE / ACC_PROOF_ALONE / ACC_PROOF_BAG
ACC_CLIP_OUT=<dir> work/accessories/unity_iter.sh run GolfArcade.EditorTools.AccessoriesClipCheck.RunBatch
work/accessories/copyback.sh && python3 work/accessories/lobby/update_look_sources.py && python3 work/accessories/lobby/compress_maps.py
HERO_SYNC_CLONE=$TMPDIR/acc-hero-sync-clone work/hero-sync/sync_heroes.sh
```

## Checks
- Fit numbers per hero: `out/<Sex>/<item>/piece.json` (`fit_report`). Cap: min clearance to the skin ≥ 1.6 mm, lining gap ≈ 3 mm, nothing of the head moved.
- Clipping: `AccessoriesClipCheck` poses every emote / Walk / Run / intro / swing clip (10 samples a second) with the accessories on and counts, per piece: skin inside the accessory, cloth inside the accessory, accessory inside the body (deepest in mm, worst time). Results: `clipcheck/`.
- Proof images: `proof/` (alone: Blender product renders; on heroes: Unity renders in the game's shaders; side-by-side sheets with the reference in `proof/sheets/`).

## State at hand-off (2026-10-13, evening)
| Item | Tris | Maps | Status |
|---|---|---|---|
| tennis_cap | 10.6k | 2048 | fitted to both bodies (min clearance to the skin 1.6 mm, nothing of the head moved), clean in Walk/Run/Idle/all strokes; contact only where the hands go to the head (Emote_Shock / MissWhiff, Female Spike) |
| golf_cap | 11.5k | 2048 | same fit; only Emote_Shock touches |
| golf_glove (L, mirrored R) | 11.9k / 11.6k | 2048 | skinned to the 17 hand/finger/forearm bones, weights copied from the body; the skin it covers is not drawn (`glove_cover.py` -> `*_hide.bin`), so nothing pokes through in swings; Adnan: "gloves are perfect" |
| golf_bag | 29.7k | 4096 (non-uniform texel size, 2.5 px/mm average) | hangs on the upper back (male 20 mm at shoulder-blade level, female 33 mm, lower than the sheet), one strap over the right shoulder, legs folded; strap/bag clear of the body in Idle/Walk/Run/Wave/Cheer (strap <= 8 mm into the skin), **FistPump (arms overhead) puts the strap 28 mm into the raised arm** and swings/emotes clip (the bag is only shown in the plaza and between shots) |

Open items: FistPump strap (hide the strap or the bag during overhead emotes if it bothers), the female bag sits looser/lower, bag head covers and iron heads are stylised, hair is not built (only the empty `HairRoot` socket), no UI to pick items (the API is `HeroAccessories`).
Brim: bent bill (sides cup down ~37 deg, front curl), per Adnan's note that the first brim read as a toy hat.
