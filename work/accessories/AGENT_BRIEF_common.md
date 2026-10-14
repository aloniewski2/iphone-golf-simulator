# Accessories — common brief (2026-10-13)

Adnan's request (the part that matters for you): *"Add basic accessories, as separate swappable items. Don't change the body, head, face, clothes, rig, or animations. Stage only, no commits."*
Items: a tennis cap, a golf cap, a **left-hand golf glove**, a **golf carry bag**. References: `ArtDir/hero/targets-2026-10-13/accessories/` (`01..04` product sheets show how each item is BUILT, `05..06` fit sheets show only WHERE it sits — ignore the body/outfit in them; rules in that folder's README.md).

1. Build each item in Blender **on its own first, like a real product**: real thickness, seams, construction parts, then look at it alone from all sides (front/side/back/3-4/top/underside + construction close-ups).
2. Then **fit it to our two bodies (Male, Female)**. The golf and tennis heroes share these bodies and bind pose, so one fit per sex covers all four heroes.
3. Each item is its own slot and its own mesh (turned on/off/swapped without touching anything else). Never merge with / reshape the head, hand or body. Do not touch Unity, clothes, rig or animation files.

I (the lead) own: the Unity side (installer, runtime slots, lobby export), the tennis/golf caps, the clipping tool over all clips, the final review sheets. You own: **your item**, from product to per-sex fitted files + maps + your own review images.

## Environment
- Repo: `/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`; work folder `work/accessories/` (all paths below relative to it).
- Blender 5.2: `/Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python py/<script>.py -- <args>`. Its python has numpy + mathutils but NOT scipy/PIL. System `python3` has PIL (used for `py/montage.py out.png cols img...`).
- Foreground shell calls are capped ~2 min: run long jobs in the background (`run_in_background`) and poll their log.
- Look at your images (Read tool on PNG) at every step; iterate on what you see, not on numbers alone. Adnan reviews pictures.

## Frames (read the docstring of `py/acc_common.py` first)
- **N frame** = what every accessory is built and fitted in: Z up, hero faces −Y, hero's LEFT = +X (natural right-handed Blender frame; a camera at −Y looking +Y sees the hero's left on the image's right).
- `HeroData(sex)` (acc_common) = body mesh in N (`body_pos`, `body_tri`), skin weights (`bi`, `bw` over `bones`), rest bone positions `rest_pos[name]` (N), rest bone matrices `rest_L2W_unity[i]`, mesh→bone `bind[i]`, `r2w`.
- `.bin` files are written in the body's *mesh space* (X flipped vs N, winding reversed) by `acc_pipeline.write_piece`; do not do that conversion yourself.
- Male rest pose is A-pose (arms out, hands at x≈±0.53, z≈0.95), Female arms down (hands x≈±0.23, z≈0.85). Heads: top z 1.7047/1.7049, shoulders ≈1.35–1.37.

## Worked example you must read and follow: the tennis cap
`py/cap.py` (product builder) → `py/cap_export.py` (unwrap, join, pack, extract, **fit to the body**, write) → `py/cap_maps.py` (construction marks for the maps) → `py/acc_preview.py` (renders). Supporting modules (do **not edit** them — I am still changing them; copy a function into your own file if you need a different one):
- `acc_geo.py`: `thick_grid` (outer surface + inner surface offset by thickness + rounded edge, from a parametric grid), `tube`, `lathe`, attribute helpers, `boolean_diff`, `unwrap`.
- `acc_pipeline.py`: `unwrap_part`, `pack_joined`, `extract` (triangulate + UV split + fields), `write_piece`, `merge_manifest`.
- `acc_fit.py`: `Clearance` (signed distance to a body region), `nelder_mead`, socket maths (`rt4`, `product_to_socket`, `socket_to_world_N`, `bindpose_for`, `local_pose_under`), `recompute_normals`, `gaussian_transfer`.
- `acc_canvas.py`: the map baker (`Canvas`, `vertex_ao`, `bake`). Maps: **N** (tangent normal) and **M** (R AO, G cavity, **B trim mix**, A fabric id/9), same encoding as the clothing maps; the game material is TennisCloth with a base colour and a trim colour mixed by M.B. Fabric ids (face attribute `fabric`): 6 woven twill, 7 piqué knit, 8 pebble leather, 9 rib/webbing, 4 smooth plastic/rubber (id 2 = leather variant, 3 = rubber, no weave).
- Each part is a Blender mesh with **face attributes** `zone` (0 outer, 1 rim, 2 inner), `kind` (your own int per part type), `fabric`, `sub` (0 = cloth role, 1 = metal role `Kit_Buckle`) and **float point attributes** (fields such as seam distance) that the detail function turns into groove/stitch/perforation marks. Texel density: use 2048 maps, aim ≥ 3 px/mm.

## Output contract (per sex) — `out/<Male|Female>/<item>/`
- `<Piece>_final.npz` and `<Piece>.bin`: written by `acc_pipeline.write_piece(out_dir, '<Piece>', piece, bones, bi, bw, bindposes, 2048, ['Kit_<Role>', 'Kit_Buckle'], detail=...)` (positions already in the hero's N frame, fitted). `bones` = list of bone names; `bindposes[i]` = mesh→bone (use `hd.bind[hd.idx(name)]` for body bones, `F.bindpose_for(hd, W_socket)` for a socket).
- `<Tag>_N.png`, `<Tag>_M.png`: `acc_canvas.bake(...)`.
- `piece.json`: `item, sex, slot, parent_bone, npz, tag, pieces[], entries{piece: manifest entry}, socket (name or null), socket_local_position, socket_local_rotation (xyzw), fit{...}, fit_report{...}` — see `out/Male/tennis_cap/piece.json`.
- Bone names must be real skeleton bones (`Head`, `LeftHand`, `LeftIndexProximal`, `UpperChest`, ...) **or** a socket you define (`Headwear`, `Back_Bag` — child of a named bone at a fixed local pose). `ArmBlend_*` bones exist only at runtime: do not use them.
- Per-vertex weights: ≤ 4 bones per vertex (`bi` int (n,4), `bw` float (n,4), sums to 1, unused slots weight 0).
- Piece/renderer names: `Kit_Cap`, `Kit_Glove_L` (+ `Kit_Glove_R` mirrored for left-handed golfers), `Kit_Bag`.

## Quality bar / what to show (put images in `proof/<item>/`, they are the deliverable Adnan sees)
1. **Alone**: the reference sheet's own views (e.g. glove: back of hand, palm, side, 3/4, wrist opening, velcro tab open) plus construction close-ups, rendered with the maps on (colours from `acc_preview.COLOURS`). Montage them next to the matching reference image with `py/montage.py`.
2. **On both heroes**: front, 3/4, side and a close-up (use `acc_preview.render_views`; add views if you need them). Put the reference fit sheet next to them.
3. Numbers in `piece.json.fit_report`: min clearance to the body (never negative; ≥ 1 mm), max gap, tris/verts, px/mm. Say honestly what is not good.
4. Pose check: the item must not poke through / sit inside the body in the real animation poses. Offline LBS on the dumped clips is trustworthy (0.0 mm vs Unity): `work/clothing-v2/py/cv2io.py` (`Hero(id).clip(name)` → body world positions + bone matrices per frame, `lbs`), `work/clothing-v2/py/poke.py` is the polo's detector to copy from. Dumps exist for `MatchHero_Male|Female` (tennis clips incl. Walk, RunForward, emotes, intros) and `GolfKitHero_Male|Female` (Drive, IronSwing, Putt, Chip, HalfSwing, Idle, Walk, RunForward, Emote_*, Intro_*, Cheer, FistPump, Wave). Your item is skinned: transform it with ITS OWN bones/weights (for a socket bone: its parent bone's matrix × local pose).

## Budgets / constraints
- ≤ 50k tris per hero is the whole hero; your item's tris: glove ≤ 14k, bag ≤ 30k, cap ~16k (mine is 16k — too high, I will trim).
- Stage only: no git commit/push/stash/checkout. Write only inside `work/accessories/` (your own `py/<item>_*.py`, `products/`, `out/`, `proof/<item>/`).
- Don't leave Blender processes running. No network. Parallel agents exist (cap, other item): never edit someone else's files.

## Report back (one message)
Files produced (paths), commands to rebuild, the numbers above, the images you made (paths) with a one-line honest verdict each, and anything you could not solve (with what you tried).
