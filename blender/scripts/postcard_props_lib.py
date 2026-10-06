"""postcard_props_lib: procedural, instanced, textured props for the postcard holes (8 Needle, 9 Split, 10 Crater).

POSTCARD_LOOK role A4 (v2 2026-10-04 + review-P repair: tapering sea stacks with satellites, no-stretch / no-stacking legacy swap,
land-only scatter, plants refused on the sea, shape gates; see the "v2" and "v2 repair" sections of work/postcard-look/LIB_PROPS.md for
every changed signature; v2 2026-10-05 grass pass: SWAY DATA on every plant, tall + filler tufts, scatter_fringe, attach_vines,
tuftify_shrubs / tuftify_crowns, cheapen_tufts, arc-following arch haunch - section "v2 sway + fringe" at the end of LIB_PROPS.md). Replaces the flat icosahedron props of postcard_lib (import_rocks / place_rock / build_arch /
build_ruin_wall / scatter_rocks) with real library meshes: rocks with chips, ledges and planes; basalt hex-column clusters
and wall segments; a voussoir masonry arch and broken masonry ruins; grass tufts, toy shrubs, flower clusters, hanging
vines, agave, a pine and a small cliff tree on ONE palette atlas (Plants_C.png, written here); a waterfall sheet; a
background volcano cone and smoke cards; a deterministic Poisson-ish scatter with ready masks. Everything is bmesh/numpy,
seeded, no animation, and every name uses the LOOK_CONTRACT section 2 prefixes (ROCK_ / PLANT_ / DRESS_ / WATER_), never a
collision prefix (TERRAIN, FAIRWAY, GREEN, TEE_BOX, BUNKER, CART_PATH).

LIBRARY + INSTANCES. A library mesh is built once per .blend (on first use, or ensure_library()) into the hidden, excluded
collection ASSET_LIBRARY (not exported by postcard_lib.export_fbx). Library object name == mesh name == the KIND
(e.g. 'ROCK_BOULDER_A'). Placed props are LINKED DUPLICATES (bpy.data.objects.new(name, library_mesh)): they share the mesh
datablock, so the FBX exporter writes each geometry once. Instance names are '<KIND>.<nnn>' (ROCK_BOULDER_A.007).
Collections: rocks -> ENVIRONMENT/ROCKS, plants -> ENVIRONMENT/PLANTS, smoke/cone/waterfall -> ENVIRONMENT,
arch/ruin -> STRUCTURES; everything is parented to the hole root (HOLE_NN_ROOT) when a postcard_lib D is given.

ORIGIN CONVENTION (every library mesh): origin at the footprint centre, z = 0 is the ground (or water) contact line, the mesh
reaches a little below z = 0 so it can sit on uneven ground; +Z up. Vines: origin at the TOP attachment point, they hang
down -Z and bulge toward +Y (= away from the wall: rotate +Y onto the wall's outward normal). Basalt walls: the front
(lower) rows face -Y. Sea stacks: z = 0 is the waterline, they reach 3 m below it.

MATERIALS (LOOK_CONTRACT section 3; textures from Unity/Assets/Resources/Course/Look/, flat fallback colours until they exist):
  LK_ROCK, LK_ROCK_WET (rocks at/in the water: '<KIND>_WET' twin meshes), LK_BASALT, LK_MASONRY, LK_PLANTS (Plants_C.png
  palette atlas, 256x256, written by write_plants_atlas(); the plants and the shrub CROWN of every sea stack use it), LK_FALL (waterfall), LK_SMOKE (smoke cards),
  LK_ROUGH (only the grass cap of ROCK_SEASTACK_B). lk_material(name) is get-or-create: a material another lib already made
  (no 'lk_props' custom property) is reused untouched.
UVs: UV0 only, in TILE UNITS. Rock / basalt / masonry (and the cone) = object-space box projection (per face, the dominant
  normal axis picks the plane; side faces v = z) divided by the contract tile (LK_ROCK 4 m, LK_BASALT 8 m, LK_MASONRY 4 m,
  LK_ROUGH 12 m). Plants: every part's UV island sits inside one 64x64 px swatch of the atlas (6 px margin), base of a part
  at the dark bottom of the swatch, tip at the light top. Waterfall: u across 0..1, v 0 (top) .. 1 (foot). Smoke card: 0..1.
  Instances of library meshes keep the library UVs, so a strongly scaled instance stretches its texture: library meshes are
  built at their natural size (scale ~0.6-1.6 is what they are made for).

KINDS (build_<kind> happens lazily on the first place/scatter)
  rocks   ROCK_BOULDER_A (round boulder 2.5 m), ROCK_BOULDER_B (lumpy 1.7 m), ROCK_SLAB_A (flat stepped ledge slab 4.2 m),
          ROCK_BLOCK_A (chipped block 1.9 m), ROCK_CAIRN_A (cluster of 6 stones 1.6 m tall), ROCK_STONE_A / ROCK_STONE_B
          (small stones 0.6-0.8 m), ROCK_LEDGE_A (long low outcrop 6.5 m), ROCK_CRAG_A / ROCK_CRAG_B (big faceted outcrops
          9 x 6 x 5 m / 6.5 x 5.5 x 6.4 m: the ROCK_LARGE / CLIFF_ROCK replacements), ROCK_OUTCROP_A (flat-topped rock island,
          top exactly flat, grass cap: the arch stands on it).
  spires  ROCK_SEASTACK_A / _B / _C / _D / _E (13 / 18 / 24 / 16 / 21 m above the water, +3 m under it) and ROCK_SPIRE_A (11 m needle): ONE
          tapering spire each (stacked irregular 5-9 corner slices that shrink, lean and twist, shelves, an overhang, chipped
          facets; round 2026-10-04 #2: bulbous low, bent / leaning axis, an overhanging shrub dome of LK_PLANTS faces (muted atlas greens) instead of a green
          pyramid, summit exactly at the design height). 380-480 triangle faces.
          '<KIND>_WET' = the same geometry on LK_ROCK_WET, '<KIND>_BASALT' = on LK_BASALT (crater loose rocks; the cap too).
  basalt  ROCK_BASALT_A (4.6 m dome cluster), _B (2.4 m stepped ridge), _C (10 m pillar, tapered capped underside), _D (1.5 m
          low shelf), _E (12 m cluster), _F (8 m cluster); wall-ring TOWERS ROCK_WALL_BASALT_S / _M / _L (14 x 15 x 26 m,
          20 x 21 x 38 m, 35 x 28 x 54 m incl. 6 m buried; 19-23 hex columns each, 10-14 top levels, chipped; UV baked for the
          nominal instance scale 1.3 / 1.5 / 1.5 so the 8 m basalt tile keeps its size); long wall SEGMENTS ROCK_WALL_BASALT_A /
          _B / _C (41 x 11 x 49 m ... chain them, never stretch them). Clusters reach 1.5 m, towers / walls 6 m below z = 0.
  masonry DRESS_BLOCK_A / DRESS_BLOCK_B (fallen cut blocks); arches/ruins are unique meshes from build_masonry_arch /
          build_ruin.
  tufts   v2 2026-10-05: PLANT_TUFT_D (tall upright green clump, 72 faces), PLANT_TUFT_E (arching fountain, 81 faces), PLANT_TUFT_F (tall olive / straw, 72 faces),
          PLANT_TUFT_S / _T (cheap 36-face green / olive fillers); TUFT_KINDS_TALL / TUFT_KINDS_FILL / TUFT_KINDS_ALL. Every PLANT_ mesh carries SWAY DATA (colour attribute
          'Col': R weight, G phase, B 0, A 1, see SWAY_* below and sway_class / sway_arrays / mesh_sway / ensure_static_sway); the shrub crown of a sea stack carries a static one.
  plants  PLANT_TUFT_A (green), PLANT_TUFT_B (dark green), PLANT_TUFT_C (olive/straw scrub), PLANT_SHRUB_A (round green),
          PLANT_SHRUB_B (dark, taller), PLANT_SHRUB_C (olive scrub with straw blades), PLANT_FLOWER_PURPLE / _WHITE /
          _YELLOW, PLANT_VINE_S / _M / _L (1.0 / 2.0 / 3.5 m), PLANT_AGAVE_A (blue-green), PLANT_AGAVE_B (green spiky,
          crater shelf), PLANT_PINE_A (2.6 m), PLANT_TREE_A (windswept 3.2 m).
  other   DRESS_SMOKE_CARD (unit card, kept in the library but no longer instanced: build_smoke_cards bakes each card's size).

NO STRETCH (v2): every library instance keeps each scale component in [0.5, 2.0] and max/min <= 2 (SCALE_LIMITS, clamp_scale):
place() / place_fit() clamp, fit_plan() / swap_legacy_rocks() compose several instances instead of stretching one, and
stretch_report() measures it. A shared mesh's box UV only stays right inside these limits.

API (D = the postcard_lib handle from P.start(); most functions also work with D=None in a plain scene)
  ensure_library(kinds=None) -> {kind: obj}            build (once) every library mesh, or the listed ones
  library_object(kind) -> obj                           one library mesh (built on demand); '<KIND>_WET' / '_BASALT' twins too
  place(D, kind, loc, yaw=0.0, scale=1.0, tilt=(0, 0), name=None, clamp=True, allow_low=False) -> obj   one linked duplicate; scale float
                                                        or (sx,sy,sz), clamped into SCALE_LIMITS (custom property lk_clamped = 1 when it changed);
                                                        a PLANT_ kind below z = 0.3 m (the sea) raises ValueError unless allow_low=True
  place_fit(D, kind, center, dims, R=None) -> obj       instance whose rotated bbox is ~`dims` big (scale clamped), centred on `center`
  scatter(D, kind, count, region=None, masks=(), seed=0, min_gap=None, scale_range=None, avoid=(), z=None, sink=None,
          tilt=None, safe=True, corridor_m=None, respect=True, register=True, wet=False, max_tries=12, yaw=True,
          margin_m=None, footprint_k=0.45, surface=None, shore_margin_m=1.0, allow_low=False) -> [objs]
      D=None with plants / dressing raises ValueError (no land mask exists) unless allow_low=True (test scenes on a reference ground).
      kind: 'PLANT_TUFT_A' | ['A', 'B'] | {'PLANT_TUFT_A': 3, 'PLANT_TUFT_B': 1}.  LAND RULE: surface 'land' (default for all
      non-ROCK kinds: always intersected with mask_real_land = the TERRAIN top minus the water ellipses minus a 1.0 m shore margin;
      region=None means the whole land bbox; any region you pass is intersected with it, so nothing lands on the sea), 'sea'
      (rocks only), 'any' (default for ROCK_ kinds). z=None stands a land prop on the ray-cast TOP of the TERRAIN/FAIRWAY/GREEN
      meshes (ground_sampler), sunk by its sink (plants <= 0.045 m). region: None, (x0, y0, x1, y1), or a callable(rng, n) ->
      (X, Y, Z|None) such as region_band / region_polyline / region_on_objects / region_disc / region_land. masks: callables
      (X, Y) -> bool arrays (all must hold). safe=True (default) adds mask_off_play (never inside fairway / first cut / green /
      apron / tee box / bunker + lip, per-kind margin), mask_pin_clear(6 m), mask_off_objects(DRESS_PATH, 0.4 m) (the path footprint
      read from the scene NOW: build the path first), mask_off_markers(1.5 m) (TEE_MARKER_1/2, HOLE_CUP, BALL_START, FLAG*), and
      for tall kinds (rocks, shrubs, trees, agave B) corridor_m (default fairway half-width + 3 m) off the centerline. The whole
      footprint (8 points at 0.75 x the scaled half extent) must pass the safety masks and stay on land. Footprints are registered on
      D so later scatter calls keep their distance (respect): scatter big things first. Plants refuse surface='sea' (ValueError).
  masks:   mask_land(D), mask_real_land(D, shore_margin_m=1.0, ring=12), mask_water(D), mask_rough(D), mask_off_play(D, margin_m),
           mask_off_objects(prefixes=('DRESS_PATH',), margin_m=0.4), mask_off_markers(r_m=1.5), mask_pin_clear(D, r_m=6),
           mask_corridor(D, half_width_m), mask_band(D, lo_m, hi_m) (land lo..hi m inside the cliff edge),
           mask_sea(D, lo_m, hi_m) (water lo..hi m off the edge), mask_lies(D, names), mask_not_near(points, r)
  regions: region_rect(x0, y0, x1, y1), region_disc(cx, cy, r), region_band(D, lo_m, hi_m, side='land'|'sea'),
           region_polyline(points, half_width_m, z=None), region_on_objects(objs, min_up=0.8, inset_m=0.15)
  ground_sampler(D=None) -> callable(X, Y) -> Z   top height of the TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER meshes (ray cast), D.play_z if none
  scatter_rocks(D, count=20, zone='sea'|'foot'|'rim'|'ledge', seed=0, kinds=None, scale_range=None, min_gap=None,
                avoid=(), region=None, masks=(), variant=None) -> [objs]   the new-mesh replacement of
                postcard_lib.scatter_rocks (wet in sea/foot; variant='BASALT' for the crater, '' = plain LK_ROCK)
  place_sea_stack(D, x, y, height_m, radius_m, seed=0, kind=None, wet_foot=True, skirt=3, satellites='auto') -> [objs]   main summit
           height_m above the water; kind default = seastack_kind_for(height_m, main radius, rng) among A-E / SPIRE_A (uniform scale 0.5-2);
           satellites 'auto' = 0 (radius < 5 m) / 1 (< 8.5 m) / 2 lower spires of other kinds beside it; [main, satellites..., skirt...]
  seastack_kind_for(height_m, radius_m=None, rng=None, near=0.25) -> kind   (height only: A/B/C, SPIRE_A below 8 m)
  place_outcrop(D, x, y, top_z, radius_m, seed=0) -> obj   flat-topped rock island, top exactly at top_z
  swap_legacy_rocks(D, objs=None, basalt=False, rename=True, seed=0, purge=True, report=None) -> [objs]   replace EVERY legacy
           stand-in (icosahedron instances ROCK_SMALL/MEDIUM/LARGE, CLIFF_ROCK, ROCK_WALL_FLAT/BLUNT, hole 9's ROCK_STACK_nn,
           any other <60-vertex or blob non-library ROCK_*) by upright library instances WITHOUT stretching (scales in [0.5, 2.0],
           max/min <= 2): tall sea boxes and piles of rocks resting on each other become ONE tapering spire (+ satellites + skirt),
           every other box one instance or an xy tiling (ONE layer, never stacked, varied kinds / scales / yaws); basalt=True = basalt
           family (wall-ring boxes become ONE ROCK_WALL_BASALT_S/M/L each); ends with assert_no_legacy_rocks (RuntimeError if a rock
           is left); every replacement carries lk_swap = legacy name
  swap_composition_report(objs=None) -> dict(ok, groups, pieces, vertical_stacks, clones)   gate numbers: no piece stacked on another
           piece of the same legacy box, no identical clones
  assert_no_legacy_rocks(D=None, objs=None, raise_on_fail=True) -> dict   hole scripts call this after the swap: no exported
           ROCK_*/CLIFF_ROCK* with < 60 welded vertices, a legacy mesh or a blob shape
  stretch_report(objs=None) / instance_scales(objs=None) / clamp_scale(sc)   the SCALE_LIMITS gate numbers
  rock_shape_report(me) / seastack_report(me) / welded_vertex_count(me) / SHAPE_TH   the shape gates (blob test, taper test)
  build_basalt_cluster(kind, rx, ry, height, col_r, seed, ...) / build_basalt_wall(kind, length, depth, height, col_r, seed,
           ...) -> library obj (new kinds, any name starting 'ROCK_')
  build_basalt_skin(D, loop_xy, top_z, bottom_z, col_r=0.9, inset_m=0.15, seed=0, name='ROCK_SKIN_BASALT', rows=2,
           closed=True) -> [objs]
           a ring of basalt columns just INSIDE a closed outline (never past it in plan), tops at top_z (pillar sides)
  build_masonry_arch(D, center, facing_deg, span_m=7.0, height_m=10.0, depth_m=None, broken=False, seed=0, base_rock=True,
           vines=True, name=None) -> dict(arch=obj, base=[objs], vines=[objs], fallen=[objs], vine_points=[...])
  build_ruin(D, path, height_m=4.0, thickness_m=1.0, doorway_at=0.45, doorway_w=1.6, doorway_h=2.3, seed=0, closed=False,
           clip=None, base_z=None, name=None, fallen=True) -> dict(walls=[objs], fallen=[objs])
  hang_vines(D, points, seed=0, max_drop=None, kinds=('PLANT_VINE_S', 'PLANT_VINE_M', 'PLANT_VINE_L')) -> [objs]
           points = [((x, y, z), (nx, ny)), ...] (attachment point, outward horizontal direction)
  cliff_lip_points(D, spacing_m=6.0, seed=0, masks=(), z=None) -> [((x, y, z), (nx, ny)), ...]  points on the cliff lip
  build_waterfall_sheet(D, top, out_dir, width_m=3.0, foot_z=0.0, widen=1.7, reach_m=1.6, seed=0, name=None) -> obj
  waterfall_foot(top, out_dir, width_m=3.0, foot_z=0.0, widen=1.7, reach_m=1.6) -> dict(pos, width, dir)
  build_background_cone(D, center, base_r=115.0, height=118.0, seed=0, name='DRESS_CONE', notch_deg=None) -> obj
  build_smoke_cards(D, items, seed=0) -> [objs]        items = [((x, y, z), width_m, height_m), ...] -> DRESS_SMOKE_nn
  scatter_fringe(D, kinds=None, tri_budget=20000, seed=0, density=1.0, sources=('play', 'path', 'lip'), ball_zones=None, focus=None, ...) -> dict(objs, count, tris, clusters, singles,
           edge, edge_focus, ...)   v2 2026-10-05: the EDGE FRINGE - clumps (3-7 + singles) of tall leaning tufts hugging the rough edge of fairway / green / tee rims, path edges and cliff
           lips; density falls off with the distance to the edge, lean <= 12 deg, scale 0.6-1.8, tops <= 0.55 yd near the ball corridor, never on play surfaces / water / within 4 m of a
           shot ball / 6 m of the pin; triangle budget; see its docstring + LIB_PROPS.md 'v2 sway + fringe'. Helpers: still_ball_zones(D), play_edge_distance(D), object_edge_distance()
  cheapen_tufts(D, near_m=60.0) -> dict(swapped, kept, tris_saved)   old 117-triangle tufts farther than 60 m from the shot balls become 36-triangle fillers (pays for the fringe)
  tuftify_shrubs(D, shrubs=None, ...) / tuftify_crowns(D, stacks, ...) -> dict(objs, tris, shrubs)   olive tufts poking out of the bushes / dark tufts on the sea-stack crowns
  attach_vines(D, vines=None, max_gap_m=0.05, reach_m=1.0, delete=True) -> dict ; vine_root_gaps(vines=None) -> [(vine, gap m)]   every vine root touches a surface (<= 5 cm) or is deleted
  write_plants_atlas(path=PLANTS_PNG) -> (path, sha256)   deterministic 256x256 RGB PNG (zlib/struct writer)
  library_report() -> {kind: dict(verts, faces, tris, mats)}   ;  SWATCH / swatch_uv(name, s, t) for the atlas layout

EXAMPLES (copy-paste into a hole build after P.build_terrain / build_play_surfaces / build_gameplay):

    import postcard_props_lib as PL
    # Needle: scatter around the pads (big things first: later calls keep clear of the registered footprints)
    PL.scatter_rocks(D, 24, zone="rim", seed=4); PL.scatter_rocks(D, 30, zone="sea", seed=5)
    PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B"], 40, region=PL.region_band(D, 0.6, 6.0), seed=2)
    PL.scatter(D, {"PLANT_FLOWER_PURPLE": 3, "PLANT_FLOWER_WHITE": 1}, 80, region=PL.region_band(D, 0.4, 10.0), seed=3)
    PL.scatter(D, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2}, 600, region=PL.region_band(D, 0.3, 14.0), seed=1)
    PL.place_outcrop(D, x_m, y_m, 5.0, 6.4, seed=8)                                      # the arch's own rock
    arch = PL.build_masonry_arch(D, (x_m, y_m, 5.0), 171.2, span_m=7.5, height_m=11.0, seed=8, base_rock=False)
    PL.hang_vines(D, PL.cliff_lip_points(D, spacing_m=7.0, seed=6), seed=6)
    fall = PL.build_waterfall_sheet(D, (x, y, D.play_z - 0.3), (nx, ny), width_m=3.0)
    foot = PL.waterfall_foot((x, y, D.play_z - 0.3), (nx, ny), 3.0)        # -> A3 builds WATER_SURF there
    PL.place_sea_stack(D, x, y, 16.0, 4.0, seed=3)
    # Split: olive scrub on the left ridge (lie stays Rough), ruin as broken masonry
    PL.scatter(D, {"PLANT_TUFT_C": 4, "PLANT_TUFT_A": 1}, 500, masks=[my_left_ridge_mask], seed=11)
    PL.build_masonry_arch(D, (xa, ya), 30.0, span_m=6.0, height_m=9.0, broken=True, seed=12)   # the ruined arch
    PL.build_ruin(D, [(x0, y0), (x1, y1), (x2, y2)], seed=12, clip=lambda x, y: on_ridge(x, y))
    # Crater: basalt
    PL.swap_legacy_rocks(D, basalt=True)       # every old ROCK_WALL_* / CLIFF_ROCK instance becomes a column cluster/wall
    PL.scatter_rocks(D, 20, zone="foot", seed=7, variant="BASALT")                       # basalt rubble at the lava edge
    PL.build_basalt_skin(D, pillar_loop_xy, D.play_z - 0.04, -6.0, col_r=1.1, seed=3)
    PL.scatter(D, {"PLANT_AGAVE_B": 2, "PLANT_TUFT_B": 3}, 60, region=PL.region_band(D, 0.4, 3.0), seed=13)
    PL.build_background_cone(D, (x, y, -8.0), seed=1, notch_deg=250)      # notch toward the camera side
    PL.build_smoke_cards(D, [((x, y, 60.0), 40.0, 55.0), ((x2, y2, 50.0), 30.0, 40.0)])

Gates: blender/scripts/postcard_props_smoke.py (prints GATE lines, renders ArtDir/screenshots/golf_postcards/look/props_sheet.png).
Notes: work/postcard-look/LIB_PROPS.md.
"""
import bpy
import bmesh
import math
import os
import sys
import random
import struct
import zlib
import hashlib

import numpy as np
from mathutils import Vector, Matrix
from mathutils import noise as mnoise

try:
    HERE = os.path.dirname(os.path.abspath(__file__))
except NameError:  # exec() without __file__
    HERE = os.getcwd()
if HERE not in sys.path:
    sys.path.insert(0, HERE)
REPO = os.path.dirname(os.path.dirname(HERE))
LOOK_DIR = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "Look")
PLANTS_PNG = os.path.join(LOOK_DIR, "Plants_C.png")

import postcard_lib as P  # noqa: E402  (geometry helpers, lie mirror, collections; never modified here)

YD = P.YD
GROUND_PREFIXES = P.GROUND_PREFIXES
NAME_PREFIXES = ("ROCK_", "PLANT_", "DRESS_", "WATER_", "LAVA_")
LIB_COLLECTION = "ASSET_LIBRARY"

# ----------------------------------------------------------------------------- materials (LOOK_CONTRACT section 3)
# name: (albedo stem, normal stem, emission stem, tile m (None = no box UV), roughness (1 - S), fallback sRGB, tint, alpha)
LK_SPECS = {
    "LK_ROCK": ("Rock_C", "Rock_N", None, 4.0, 0.88, (124, 118, 108), None, False),
    "LK_ROCK_WET": ("Rock_C", "Rock_N", None, 4.0, 0.65, (78, 76, 72), (0.62, 0.63, 0.66), False),
    "LK_BASALT": ("Basalt_C", "Basalt_N", "Basalt_E", 8.0, 0.85, (46, 43, 46), None, False),
    "LK_MASONRY": ("Masonry_C", "Masonry_N", None, 4.0, 0.90, (178, 166, 146), None, False),
    "LK_PLANTS": ("Plants_C", None, None, None, 0.90, (90, 150, 62), None, False),
    "LK_FALL": ("Fall_C", None, None, None, 0.30, (226, 242, 252), None, True),
    "LK_SMOKE": ("Smoke_C", None, None, None, 1.00, (120, 108, 104), None, True),
    "LK_ROUGH": ("Rough_C", "Rough_N", None, 12.0, 0.92, (70, 138, 50), None, False),
}
TILE = {k: v[3] for k, v in LK_SPECS.items()}


def _rgb(c):
    return P.rgb(*c)


def _look_path(stem):
    return os.path.join(LOOK_DIR, stem + ".png")


def _image(stem, non_color=False):
    if stem is None:
        return None
    path = _look_path(stem)
    if not os.path.isfile(path):
        return None
    img = None
    for im in bpy.data.images:
        try:
            if os.path.abspath(bpy.path.abspath(im.filepath)) == os.path.abspath(path):
                img = im
                break
        except Exception:
            pass
    if img is None:
        img = bpy.data.images.load(path, check_existing=True)
    if non_color:
        try:
            img.colorspace_settings.name = 'Non-Color'
        except Exception:
            pass
    return img


def textures_present(name):
    """True when the albedo PNG of LK material `name` exists in Look/."""
    spec = LK_SPECS[name]
    return spec[0] is not None and os.path.isfile(_look_path(spec[0]))


def lk_material(name, rebuild=False):
    """Get-or-create the LK_ material `name` (Principled BSDF + Image Texture nodes on the Look/ PNGs, flat fallback colour
    while a PNG is missing). A material that exists WITHOUT our 'lk_props' flag (made by another lib) is returned untouched;
    one we made flat earlier is rebuilt as soon as its textures exist (or when rebuild=True)."""
    spec = LK_SPECS[name]
    mat = bpy.data.materials.get(name)
    if mat is not None and not rebuild:
        if not mat.get("lk_props"):
            return mat
        if mat.get("lk_textured") or not textures_present(name):
            return mat
    if mat is None:
        mat = bpy.data.materials.new(name)
    mat["lk_props"] = 1
    if mat.node_tree is None:
        mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (400, 0)
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    c_stem, n_stem, e_stem, _tile, rough, fb, tint, alpha = spec
    col = _rgb(fb)
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Roughness"].default_value = rough
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.25
    textured = False
    cimg = _image(c_stem)
    if cimg is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = cimg
        tex.location = (-600, 200)
        if name == "LK_PLANTS":
            tex.extension = 'EXTEND'
        src = tex.outputs["Color"]
        if tint is not None:
            vm = nt.nodes.new("ShaderNodeVectorMath")
            vm.operation = 'MULTIPLY'
            vm.inputs[1].default_value = tint
            nt.links.new(src, vm.inputs[0])
            src = vm.outputs[0]
        nt.links.new(src, bsdf.inputs["Base Color"])
        if alpha:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        textured = True
    nimg = _image(n_stem, non_color=True)
    if nimg is not None:
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = nimg
        tn.location = (-600, -150)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nm.location = (-250, -150)
        nt.links.new(tn.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    eimg = _image(e_stem)
    if eimg is not None and "Emission Color" in bsdf.inputs:
        te = nt.nodes.new("ShaderNodeTexImage")
        te.image = eimg
        te.location = (-600, -450)
        nt.links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0
    if alpha:
        for attr, val in (("surface_render_method", 'BLENDED'), ("blend_method", 'BLEND')):
            try:
                setattr(mat, attr, val)
                break
            except Exception:
                pass
        if cimg is None:
            bsdf.inputs["Alpha"].default_value = 0.75
    mat.diffuse_color = col
    mat.use_backface_culling = False
    mat["lk_textured"] = int(textured)
    return mat


def refresh_materials():
    """Rebuild every LK_ material this lib made flat once its PNGs exist (call after the texture agent ran)."""
    for name in LK_SPECS:
        m = bpy.data.materials.get(name)
        if m is not None and m.get("lk_props") and not m.get("lk_textured") and textures_present(name):
            lk_material(name, rebuild=True)


# ----------------------------------------------------------------------------- plants palette atlas (Plants_C.png)
# 4 x 4 swatches of 64 x 64 px, index i -> column i % 4, row i // 4 (row 0 at the TOP of the image)
SWATCHES = [
    # v2 2026-10-04: the greens moved from hue 92-129 to 66-96 (the stills' vegetation sits at hue 64-77, see needle.jpg / split.jpg /
    # crater.jpg) so tufts and shrubs match the retuned ground; olives, straw, flowers and trunk are unchanged.
    ("G_DEEP", (55, 87, 35)), ("G_DARK", (87, 117, 47)), ("G_MID", (125, 153, 58)), ("G_LIGHT", (165, 189, 72)),
    ("G_LIME", (201, 214, 86)), ("G_EMERALD", (56, 128, 54)), ("OLIVE_DARK", (98, 104, 46)), ("OLIVE", (132, 134, 62)),
    ("OLIVE_KHAKI", (160, 150, 92)), ("STRAW", (208, 184, 112)), ("FLOWER_PURPLE", (150, 88, 210)), ("FLOWER_WHITE", (248, 246, 240)),
    ("FLOWER_YELLOW", (252, 204, 52)), ("VINE", (100, 138, 58)), ("TRUNK", (112, 78, 52)), ("AGAVE", (105, 148, 89)),
]
SWATCH = {n: i for i, (n, _c) in enumerate(SWATCHES)}
ATLAS_SIZE = 256
SW_PX = 64
SW_MARGIN = 6.0 / SW_PX         # UV islands stay this far inside their swatch (mip bleed)


def swatch_rect(name):
    """(u0, v0, u1, v1) UV rectangle of a swatch (whole 64 px cell)."""
    i = SWATCH[name] if isinstance(name, str) else int(name)
    c, r = i % 4, i // 4
    return (c / 4.0, 1.0 - (r + 1) / 4.0, (c + 1) / 4.0, 1.0 - r / 4.0)


def swatch_uv(name, s, t):
    """UV inside swatch `name`: s across (0..1), t up (0 = dark bottom .. 1 = light top), kept SW_MARGIN inside the cell."""
    u0, v0, u1, v1 = swatch_rect(name)
    s = min(max(s, 0.0), 1.0)
    t = min(max(t, 0.0), 1.0)
    w = (u1 - u0)
    m = SW_MARGIN * w
    return (u0 + m + s * (w - 2 * m), v0 + m + t * (w - 2 * m))


def _png_bytes(rgb):
    """Minimal deterministic PNG (8-bit RGB, filter 0, zlib level 9). rgb: (H, W, 3) uint8, row 0 = top."""
    h, w, _ = rgb.shape
    raw = b"".join(b"\x00" + rgb[y].tobytes() for y in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) +
            chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def plants_atlas_pixels():
    """(256, 256, 3) uint8: every swatch is its colour with a vertical gradient (x0.68 at the bottom .. x1.12 at the top,
    flowers x0.82 .. x1.08) and a faint +-3 percent hash noise. Deterministic."""
    N = ATLAS_SIZE
    img = np.zeros((N, N, 3), np.float64)
    yy, xx = np.mgrid[0:SW_PX, 0:SW_PX]
    hsh = ((xx * 73856093) ^ (yy * 19349663) ^ 0x5bd1e995) & 0xFFFF
    for i, (name, col) in enumerate(SWATCHES):
        c, r = i % 4, i // 4
        t = 1.0 - (yy + 0.5) / SW_PX
        lo, hi = (0.82, 1.08) if name.startswith("FLOWER") else (0.68, 1.12)
        f = lo + (hi - lo) * t
        n = ((hsh * (i + 7)) % 1000) / 1000.0
        f = f * (0.97 + 0.06 * n)
        blk = np.clip(np.array(col, float)[None, None, :] * f[:, :, None], 0, 255)
        img[r * SW_PX:(r + 1) * SW_PX, c * SW_PX:(c + 1) * SW_PX] = blk
    return np.round(img).astype(np.uint8)


def write_plants_atlas(path=PLANTS_PNG):
    """Write Plants_C.png (only when the bytes differ). Returns (path, sha256)."""
    data = _png_bytes(plants_atlas_pixels())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    old = None
    if os.path.isfile(path):
        with open(path, "rb") as f:
            old = f.read()
    if old != data:
        with open(path, "wb") as f:
            f.write(data)
    return path, hashlib.sha256(data).hexdigest()


# ----------------------------------------------------------------------------- mesh builder
class _MB:
    """Vertex/face lists + per-face material slot + optional per-vertex UV (plants, waterfall, cards)."""

    def __init__(self, mats):
        self.mats = list(mats)
        self.V, self.F, self.M, self.UV = [], [], [], []
        self.T = []                    # per-face tag: 0 = ordinary box-UV face, 1 = mortar joint face (UV on a mortar row of the albedo, see _write_uv), 2 = own UV list (self.FU)
        self.FU = {}                   # face index -> [(u, v) per corner] for tag-2 faces (plants atlas swatches on a rock crown)
        self.grp = 0                   # v2 2026-10-05 (sway data): the part (blade / stem / lump / leaf) the NEXT vertices belong to; PLANT_ builders set it per part
        self.GR = []                   # per-vertex part id (a part shares one sway phase, so a blade / stem moves as one piece)

    def v(self, co, uv=None):
        self.V.append((float(co[0]), float(co[1]), float(co[2])))
        self.UV.append(uv)
        self.GR.append(self.grp)
        return len(self.V) - 1

    def f(self, idx, mat=0, tag=0, uv=None):
        self.F.append(tuple(idx))
        self.M.append(mat)
        if uv is not None:
            tag = 2
            self.FU[len(self.F) - 1] = [tuple(q) for q in uv]
        self.T.append(tag)

    def add(self, verts, faces, mat=0, uv=None):
        base = len(self.V)
        for k, co in enumerate(verts):
            self.v(co, None if uv is None else uv[k])
        for fc in faces:
            self.f([base + i for i in fc], mat)
        return base


# ----------------------------------------------------------------------------- sway data (v2 2026-10-05: grass liveliness + gentle sway)
# Every PLANT_ library mesh carries ONE colour attribute "Col" (FLOAT_COLOR, POINT) that the GolfArcade/GolfPlants vertex shader reads
# (exported by postcard_look_lib.export_look_fbx with colors_type LINEAR = raw numbers, Unity mesh.colors):
#     R = sway weight 0..1  (0 at the root / pinned point, rising to the free tip; displacement = R x amplitude(wind) x wave)
#     G = per-vertex phase 0..1 (one value per part: a blade / stem / lump / leaf moves as one piece; the SHADER adds a per-instance phase
#         hashed from the world XZ position, so two clumps never move together)
#     B = 0   (authored data; Unity's default colour of a mesh that has NO colour channel is (1, 1, 1, 1): B = 1 means "no data" -> static)
#     A = 1
# R is expressed in units of SWAY_REF_AMP_YD (the displacement of weight 1.0 at 20 mph, full gust = the tuft cap, 0.06 yd), so the
# per-kind caps hold by construction for ANY shader amplitude <= SWAY_REF_AMP_YD: tuft 1.0 -> 0.06, flower / agave 0.80 -> 0.048 (cap
# 0.05), shrub 0.48 -> 0.029 (cap 0.03), vine 1.0 -> 0.06 (cap 0.08), pine / tree 0.30 -> 0.018 ("small"). Meshes that are not PLANT_
# but carry LK_PLANTS faces (the shrub crown of every sea stack, hole 10's DRESS_PADEDGE ribbon) get a STATIC attribute (R = 0, B = 0).
SWAY_ATTR = "Col"
SWAY_REF_AMP_YD = 0.06
SWAY_CAP_YD = dict(tuft=0.06, flower=0.05, agave=0.05, shrub=0.03, vine=0.08, tree=0.03)
SWAY_TIP = dict(tuft=1.0, vine=1.0, flower=0.80, agave=0.80, shrub=0.48, tree=0.30)       # the largest R of a mesh of that class
SWAY_POW = dict(tuft=1.4, vine=1.3, flower=1.3, agave=1.6, shrub=1.5, tree=1.5)           # R = tip x t ** pow (a cantilever: the lower half hardly moves)
SWAY_ROOT_MAX = 0.05                 # R at the root / pinned point (gate PLANT_SWAY_COLORS)
SWAY_TIP_MIN = dict(tuft=0.9, vine=0.9, flower=0.7, agave=0.7, shrub=0.4, tree=0.2)       # gate floor of the largest R per class (tuft / flower / agave / vine > 0.6)
TRUNK_GROUP = 99                     # part id of a trunk (never moves)


def sway_class(kind):
    """'tuft' | 'flower' | 'agave' | 'shrub' | 'vine' | 'tree' for a PLANT_ kind, 'static' for everything else."""
    if kind.startswith("PLANT_TUFT"):
        return "tuft"
    if kind.startswith("PLANT_FLOWER"):
        return "flower"
    if kind.startswith("PLANT_AGAVE"):
        return "agave"
    if kind.startswith("PLANT_SHRUB"):
        return "shrub"
    if kind.startswith("PLANT_VINE"):
        return "vine"
    if kind.startswith(("PLANT_PINE", "PLANT_TREE")):
        return "tree"
    return "static"


def _group_phase(kind, grp):
    """Deterministic phase 0.35..0.65 of a part (a narrow band: the blades of one tuft move as a clump, the shader adds the per-instance phase)."""
    return 0.5 + 0.30 * (random.Random(_seed_int("sway", kind, grp)).random() - 0.5)


def sway_arrays(kind, V, GR):
    """(weight (n,), phase (n,)) per vertex for a mesh of `kind` with vertices V (n, 3) (library space: z up, z = 0 ground, vines hang down from z = 0)
    and part ids GR (n,). See the block comment above for the per-class profile."""
    V = np.asarray(V, float).reshape(-1, 3)
    GR = np.asarray(GR, int)
    n = len(V)
    cls = sway_class(kind)
    w = np.zeros(n)
    ph = np.array([_group_phase(kind, int(g)) for g in GR]) if n else np.zeros(0)
    if n == 0 or cls == "static":
        return w, ph
    z = V[:, 2]
    tip, p = SWAY_TIP[cls], SWAY_POW[cls]
    if cls in ("tuft", "flower", "shrub"):
        t = np.clip(z / max(float(z.max()), 1e-6), 0.0, 1.0)
        w = tip * t ** p
    elif cls == "vine":
        L = max(float(-z.min()), 1e-6)
        t = np.clip(-z / L, 0.0, 1.0)
        w = tip * t ** p
        ph = 0.40 + 0.20 * t                       # continuous down the vine (the leaves share the stalk's phase at their height)
    elif cls == "agave":
        c = np.array([0.0, 0.0, 0.04])
        d = np.linalg.norm(V - c, axis=1)
        w = tip * np.clip(d / max(float(d.max()), 1e-6), 0.0, 1.0) ** p
    elif cls == "tree":
        crown = GR != TRUNK_GROUP
        if crown.any():
            zc = z[crown]
            t = np.clip((z - float(zc.min())) / max(float(zc.max() - zc.min()), 1e-6), 0.0, 1.0)
            w = np.where(crown, tip * t ** p, 0.0)
    return np.clip(w, 0.0, 1.0), ph


def _write_sway(me, mb, kind):
    """Add the sway attribute (see above) to mesh `me` built from builder `mb`. A kind that is not a PLANT_ gets the static attribute."""
    w, ph = sway_arrays(kind, mb.V, mb.GR)
    n = len(mb.V)
    for nm in [a.name for a in me.color_attributes]:
        me.color_attributes.remove(me.color_attributes[nm])
    att = me.color_attributes.new(name=SWAY_ATTR, type='FLOAT_COLOR', domain='POINT')
    col = np.zeros((n, 4))
    col[:, 0] = w
    col[:, 1] = ph if len(ph) == n else 0.5
    col[:, 3] = 1.0
    att.data.foreach_set("color", col.ravel())
    try:
        me.color_attributes.active_color = att
        me.color_attributes.render_color_index = 0
    except Exception:
        pass
    cls = sway_class(kind)
    me["lk_sway"] = cls
    me["lk_sway_tip"] = float(w.max()) if n else 0.0


def mesh_sway(me):
    """Read back the sway attribute of a mesh: dict(has, domain, cls, n, R, G, B, A (arrays or None)). `has` is False when the mesh has no colour
    attribute. Linear / raw values (FLOAT_COLOR .color)."""
    if me is None or len(me.color_attributes) == 0:
        return dict(has=False, n=len(me.vertices) if me is not None else 0, cls=None)
    a = me.color_attributes.get(SWAY_ATTR) or me.color_attributes[0]
    arr = np.empty(len(a.data) * 4)
    a.data.foreach_get("color", arr)
    c = arr.reshape(-1, 4)
    return dict(has=True, domain=a.domain, dtype=a.data_type, cls=me.get("lk_sway"), n=len(c), R=c[:, 0], G=c[:, 1], B=c[:, 2], A=c[:, 3])


def ensure_static_sway(objs=None):
    """Give every mesh that uses LK_PLANTS but has NO colour attribute a STATIC sway attribute (R = 0, G = 0.5, B = 0, A = 1) and return the mesh names touched.
    Unity gives a mesh without a colour channel the default colour (1, 1, 1, 1): the sway shader would move it at full weight (a ground ribbon, a terrain
    patch, a rock crown). postcard_look_lib.export_look_fbx calls this on the export set; hole scripts may call it earlier. A PLANT_ mesh is never
    touched here (a missing attribute on a PLANT_ library mesh is a bug: mesh_sway(...)['has'] is the gate)."""
    done = []
    seen = set()
    for ob in (objs if objs is not None else bpy.data.objects):
        if ob.type != 'MESH' or ob.data is None or ob.data.name in seen:
            continue
        seen.add(ob.data.name)
        me = ob.data
        if any(m is not None and m.name == "LK_PLANTS" for m in me.materials) and len(me.color_attributes) == 0 and not ob.data.name.startswith("PLANT_"):
            att = me.color_attributes.new(name=SWAY_ATTR, type='FLOAT_COLOR', domain='POINT')
            col = np.zeros((len(me.vertices), 4))
            col[:, 1] = 0.5
            col[:, 3] = 1.0
            att.data.foreach_set("color", col.ravel())
            me["lk_sway"] = "static"
            done.append(me.name)
    return done


def _lib_collection():
    col = bpy.data.collections.get(LIB_COLLECTION)
    if col is None:
        col = bpy.data.collections.new(LIB_COLLECTION)
        bpy.context.scene.collection.children.link(col)
    try:
        lc = bpy.context.view_layer.layer_collection.children.get(LIB_COLLECTION)
        if lc is not None:
            lc.exclude = True
        col.hide_viewport = True
        col.hide_render = True
    except Exception:
        pass
    return col


def _finalize(kind, mb, uv="box", smooth_angle=35.0, all_smooth=False, library=True, ground=None, uv_scale=1.0, uv_voff=0.0):
    """Mesh datablock `kind` from the builder: materials (LK_), per-face slots, UV0 (box in tile units or per-vertex),
    shading (smooth with sharp edges above smooth_angle, all smooth, or flat when smooth_angle is None). library=True also
    makes the hidden library object of the same name in ASSET_LIBRARY. ground: shift the mesh so its lowest point is at
    z = ground (props sink a few cm into the ground at placement z). uv_voff: V phase of the standing faces in metres (masonry arch).
    Returns the object (library) or the mesh."""
    if ground is not None and mb.V:
        dz = ground - min(v[2] for v in mb.V)
        mb.V = [(x, y, z + dz) for x, y, z in mb.V]
    old = bpy.data.meshes.get(kind) if library else None
    if old is not None:
        old.name = kind + "_old"
    me = bpy.data.meshes.new(kind)
    me.from_pydata(mb.V, [], mb.F)
    for mn in mb.mats:
        me.materials.append(lk_material(mn))
    me.polygons.foreach_set("material_index", np.array(mb.M, np.int32))
    me.update()
    if uv_scale != 1.0:
        me["lk_uv_scale"] = float(uv_scale)            # nominal instance scale the box UVs were baked for (tile / scale)
    if uv_voff:
        me["lk_uv_voff"] = float(uv_voff)              # V phase of the standing faces (metres, see box_uv_expected)
    _write_uv(me, mb, uv)
    if smooth_angle is None and not all_smooth:
        me.polygons.foreach_set("use_smooth", np.zeros(len(me.polygons), bool))
    else:
        me.polygons.foreach_set("use_smooth", np.ones(len(me.polygons), bool))
        if not all_smooth:
            try:
                me.set_sharp_from_angle(angle=math.radians(smooth_angle))
            except Exception:
                _sharp_from_angle_fallback(me, smooth_angle)
    me.update()
    me["lk_kind"] = kind
    if kind.startswith("PLANT_") or "LK_PLANTS" in mb.mats:             # v2 2026-10-05: sway data (a PLANT_ mesh sways, any other LK_PLANTS mesh is static)
        _write_sway(me, mb, kind)
    if old is not None and old.users == 0:
        bpy.data.meshes.remove(old)
    if not library:
        return me
    ob = bpy.data.objects.get(kind)
    if ob is not None:
        bpy.data.objects.remove(ob, do_unlink=True)
    ob = bpy.data.objects.new(kind, me)
    _lib_collection().objects.link(ob)
    ob.hide_viewport = True
    ob.hide_render = True
    return ob


def _sharp_from_angle_fallback(me, deg):
    bm = bmesh.new()
    bm.from_mesh(me)
    lim = math.radians(deg)
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > lim:
            e.smooth = False
    bm.to_mesh(me)
    bm.free()


def _loop_arrays(me):
    nl = len(me.loops)
    vidx = np.empty(nl, np.int32)
    me.loops.foreach_get("vertex_index", vidx)
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    npoly = len(me.polygons)
    lt = np.empty(npoly, np.int32)
    me.polygons.foreach_get("loop_total", lt)
    ls = np.empty(npoly, np.int32)
    me.polygons.foreach_get("loop_start", ls)
    nrm = np.empty(npoly * 3)
    me.polygons.foreach_get("normal", nrm)
    mi = np.empty(npoly, np.int32)
    me.polygons.foreach_get("material_index", mi)
    pol = np.empty(nl, np.int32)
    for p in range(npoly):
        pol[ls[p]:ls[p] + lt[p]] = p
    return vidx, co.reshape(-1, 3), pol, nrm.reshape(-1, 3), mi


def box_uv_expected(me, tiles):
    """Box-projected UV per loop (object space / tile of the face's material slot). tiles: per slot tile metres. A mesh baked
    for a nominal instance scale s (custom property lk_uv_scale) uses tile / s, so the tile is the contract size at that scale.
    A mesh with the custom property lk_uv_voff (metres) shifts the V of its standing faces: v = (z - voff) / tile (the masonry arch
    puts its course joints on the mortar rows of Masonry_C that way; top / bottom faces keep v = y / tile)."""
    tiles = [t / float(me.get("lk_uv_scale", 1.0)) for t in tiles]
    voff = float(me.get("lk_uv_voff", 0.0))
    vidx, co, pol, nrm, mi = _loop_arrays(me)
    c = co[vidx]
    n = nrm[pol]
    ax = np.abs(n).argmax(1)
    sg = np.where(n[np.arange(len(n)), ax] >= 0, 1.0, -1.0)
    u = np.where(ax == 0, c[:, 1] * sg, np.where(ax == 1, -c[:, 0] * sg, c[:, 0]))
    v = np.where(ax == 2, c[:, 1] * sg, c[:, 2] - voff)
    t = np.array(tiles, float)[mi[pol]]
    return np.stack([u / t, v / t], 1)


_MORTAR = {}
MORTAR_V_SPAN = 0.011            # the V extent (0..1 of the tile) a mortar face may cover: ~5.6 px of the 512 px albedo, inside the ~8 px mortar row


def masonry_grid(force=False):
    """(pitch_m, phase_m, v_row, depth): the mortar rows of Masonry_C.png, MEASURED from the PNG (so a re-generated texture keeps the arch joints on its
    mortar): pitch_m = distance between two mortar rows over the contract tile (4 m), phase_m = height of the first mortar row centre above v = 0 (m),
    v_row = that row's V coordinate (0..1, v = 0 at the bottom of the image), depth = mean luminance of the stone minus the mortar row (0..1). Falls back
    to (0.5, 0.25, 0.0615, 0.0) with depth 0 when the PNG is missing or flat. Cached per session."""
    if _MORTAR and not force:
        return _MORTAR["v"]
    res = (0.5, 0.25, 31.5 / 512.0, 0.0)
    try:
        img = _image("Masonry_C")
        w, h = int(img.size[0]), int(img.size[1])
        a = np.array(img.pixels[:], np.float32).reshape(h, w, 4)[:, :, :3].mean(2)
        prof = a.mean(1)
        pc = prof - prof.mean()
        ac = np.array([float((pc * np.roll(pc, k)).sum()) for k in range(20, h // 2)])
        pitch_px = 20 + int(ac.argmax())
        y = np.arange(h)
        zc = float((pc * np.exp(-2j * math.pi * y / pitch_px)).sum())
        y0 = (math.atan2(zc.imag, zc.real) / (2 * math.pi) * pitch_px + pitch_px / 2.0) % pitch_px
        # refine on the pixel grid: the darkest row of a fold
        fold = np.array([prof[int(round(y0 + k * pitch_px)) % h] for k in range(h // pitch_px)]).mean()
        depth = float(prof.mean() - fold)
        tile = TILE["LK_MASONRY"]
        if depth > 0.02 and 0.05 < pitch_px / h * tile < 2.0:
            res = (pitch_px / h * tile, ((y0 + 0.5) / h * tile) % (pitch_px / h * tile), ((y0 + 0.5) / h) % 1.0, depth)
    except Exception:
        pass
    _MORTAR["v"] = res
    return res


def _mortar_uv(me, p):
    """UV of a mortar joint face (4 corners p, object space): the face is laid flat in its own plane, isotropic at the contract density (u along the longest edge, v across,
    the face centred on V = the row's V), the albedo under it is a mortar ROW of the stone texture: the joint is the mortar colour whatever the stone texture does."""
    pitch, phase, v_row, _d = masonry_grid()
    tile = TILE["LK_MASONRY"] / float(me.get("lk_uv_scale", 1.0))
    q = [Vector(x) for x in p]
    # longest edge = the u axis; the in-plane perpendicular = the v axis
    edges = [(q[(k + 1) % len(q)] - q[k]) for k in range(len(q))]
    along = max(edges, key=lambda e: e.length)
    ua = along.normalized()
    nrm = Vector((0.0, 0.0, 0.0))
    for k in range(len(q)):
        a_, b_ = q[k], q[(k + 1) % len(q)]
        nrm += Vector(((a_.y - b_.y) * (a_.z + b_.z), (a_.z - b_.z) * (a_.x + b_.x), (a_.x - b_.x) * (a_.y + b_.y)))
    if nrm.length < 1e-12:
        nrm = Vector((0.0, 1.0, 0.0))
    va = nrm.normalized().cross(ua).normalized()
    ctr = sum(q, Vector()) / len(q)
    k = (int(round(ctr.x * 37.0)) * 73856093 ^ int(round(ctr.y * 37.0)) * 19349663 ^ int(round(ctr.z * 37.0)) * 83492791) & 0xFFFF
    u0 = (k / 65535.0)
    vm = sum((x - q[0]).dot(va) for x in q) / len(q)
    span = max((x - q[0]).dot(va) for x in q) - min((x - q[0]).dot(va) for x in q)
    sq = min(1.0, MORTAR_V_SPAN * tile / max(span, 1e-6))          # a wide strip is squeezed (<= ~3:1) so that it stays inside the mortar row
    return [(u0 + (x - q[0]).dot(ua) / tile, v_row + ((x - q[0]).dot(va) - vm) * sq / tile) for x in q]


def _write_uv(me, mb, mode):
    layer = me.uv_layers.new(name="UVMap")
    if mode == "box":
        tiles = [TILE.get(mn) or 4.0 for mn in mb.mats]
        uv = box_uv_expected(me, tiles)
        if any(mb.T):
            ls = np.empty(len(me.polygons), np.int32)
            me.polygons.foreach_get("loop_start", ls)
            lt = np.empty(len(me.polygons), np.int32)
            me.polygons.foreach_get("loop_total", lt)
            vidx = _loop_arrays(me)[0]
            for pi, tg in enumerate(mb.T):
                if tg == 2 and pi in mb.FU and len(mb.FU[pi]) == int(lt[pi]):
                    for j, t in enumerate(mb.FU[pi]):
                        uv[ls[pi] + j] = t
                if tg != 1:
                    continue
                pts = [mb.V[int(vidx[ls[pi] + j])] for j in range(int(lt[pi]))]
                if len(pts) == 4:
                    for j, t in enumerate(_mortar_uv(me, pts)):
                        uv[ls[pi] + j] = t
    else:
        vidx = _loop_arrays(me)[0]
        per_v = np.array([u if u is not None else (0.5, 0.5) for u in mb.UV], float)
        uv = per_v[vidx]
    try:
        layer.uv.foreach_set("vector", uv.ravel())
    except Exception:
        layer.data.foreach_set("uv", uv.ravel())


# ----------------------------------------------------------------------------- convex chunks (rock relief)
_GOLDEN = math.pi * (3.0 - math.sqrt(5.0))


def _chunk_points(rng, center, radii, n, block=0.3, jitter=0.08, chips=3, chip_range=(0.62, 0.86),
                  flat_top=None, flat_bottom=None, top_tilt=0.0, chip_dirs=None):
    """n points on a superellipsoid (block 0 = ellipsoid .. 1 = box) with radius jitter, clamped by plane cuts: `chips`
    random chip planes (each cuts off a corner at chip_range of the support distance), an optional flat top (fraction of
    the top support, tilted by top_tilt radians) and flat bottom. Returns world points (Vectors)."""
    e = 2.0 + 6.0 * block
    pts = []
    for i in range(n):
        zz = 1.0 - 2.0 * (i + 0.5) / n
        rr = math.sqrt(max(0.0, 1.0 - zz * zz))
        th = _GOLDEN * i + rng.uniform(-0.3, 0.3)
        d = (math.cos(th) * rr, math.sin(th) * rr, zz)
        s = (abs(d[0]) ** e + abs(d[1]) ** e + abs(d[2]) ** e) ** (-1.0 / e)
        j = 1.0 + rng.uniform(-jitter, jitter)
        pts.append(Vector((d[0] * s * radii[0] * j, d[1] * s * radii[1] * j, d[2] * s * radii[2] * j)))
    planes = []
    for k in range(chips):
        if chip_dirs is not None and k < len(chip_dirs):
            nv = Vector(chip_dirs[k]).normalized()
        else:
            nv = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.35, 1.0))).normalized()
        planes.append((nv, rng.uniform(*chip_range)))
    if flat_top is not None:
        a = rng.uniform(0, math.tau)
        planes.append((Vector((math.sin(top_tilt) * math.cos(a), math.sin(top_tilt) * math.sin(a), math.cos(top_tilt))), flat_top))
    if flat_bottom is not None:
        planes.append((Vector((0, 0, -1)), flat_bottom))
    for nv, frac in planes:
        h = max(p.dot(nv) for p in pts)
        lim = h * frac
        for p in pts:
            x = p.dot(nv)
            if x > lim:
                p -= nv * (x - lim)
    c = Vector(center)
    return [p + c for p in pts]


def _hull(points, merge=0.02, dissolve_deg=1.0):
    """Convex hull of the points (coplanar facets merged into planar n-gons). Returns (verts, faces) lists, outward."""
    bm = bmesh.new()
    vs = [bm.verts.new(p) for p in points]
    bmesh.ops.remove_doubles(bm, verts=vs, dist=merge)
    res = bmesh.ops.convex_hull(bm, input=bm.verts[:], use_existing_faces=False)
    kill = list({g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert) and g.is_valid})
    if kill:
        bmesh.ops.delete(bm, geom=kill, context='VERTS')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
    if dissolve_deg:
        bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(dissolve_deg), use_dissolve_boundaries=False,
                                 verts=bm.verts[:], edges=bm.edges[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.verts.index_update()
    verts = [v.co.copy() for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return verts, faces


def _chunks_mesh(kind, rng, chunks, mats=("LK_ROCK",), smooth_angle=13.0, mat_fn=None, ground=-0.08):
    """Build a library rock from a list of chunk dicts (kwargs of _chunk_points + 'c', 'r', 'n'; or 'pts' = explicit points).
    Each chunk is a convex hull with planar facets; overlapping chunks give the ledges / steps / buttresses."""
    mb = _MB(mats)
    for ch in chunks:
        ch = dict(ch)
        if "pts" in ch:
            pts = ch["pts"]
            V, F = _hull(pts, merge=ch.get("merge", 0.02))
        else:
            c, r, n = ch.pop("c"), ch.pop("r"), ch.pop("n")
            pts = _chunk_points(rng, c, r, n, **ch)
            V, F = _hull(pts, merge=0.015 * min(r))
        mb.add(V, F, 0)
        if mat_fn is not None:
            for k in range(len(mb.F) - len(F), len(mb.F)):
                mb.M[k] = mat_fn(mb, k)
    return _finalize(kind, mb, "box", smooth_angle=smooth_angle, ground=ground)


# ----------------------------------------------------------------------------- rock library
# Stylised = few, big planar facets (hulls of 16-32 points cut by chip planes), a flat base, chunk overlaps for ledges.
def _rock_boulder_a():
    rng = random.Random(101)
    return _chunks_mesh("ROCK_BOULDER_A", rng, [
        dict(c=(0, 0, 0.62), r=(1.30, 1.08, 0.86), n=30, block=0.35, chips=6, chip_range=(0.7, 0.86), flat_bottom=0.75,
             flat_top=0.9, top_tilt=0.12),
        dict(c=(1.02, 0.45, 0.4), r=(0.66, 0.6, 0.5), n=18, block=0.45, chips=3, chip_range=(0.7, 0.85), flat_bottom=0.6),
        dict(c=(-0.75, -0.62, 0.3), r=(0.58, 0.48, 0.38), n=16, block=0.5, chips=2, flat_bottom=0.5),
    ])


def _rock_boulder_b():
    rng = random.Random(202)
    return _chunks_mesh("ROCK_BOULDER_B", rng, [
        dict(c=(0, 0, 0.5), r=(0.9, 0.8, 0.66), n=32, block=0.45, chips=5, chip_range=(0.68, 0.85), flat_bottom=0.72),
        dict(c=(0.32, 0.22, 1.0), r=(0.58, 0.5, 0.4), n=18, block=0.5, chips=3, chip_range=(0.7, 0.86), flat_bottom=0.65,
             flat_top=0.85, top_tilt=0.2),
        dict(c=(-0.58, 0.45, 0.32), r=(0.5, 0.42, 0.4), n=16, block=0.5, chips=2, flat_bottom=0.55),
    ])


def _rock_slab_a():
    rng = random.Random(303)
    return _chunks_mesh("ROCK_SLAB_A", rng, [
        dict(c=(0, 0, 0.28), r=(2.15, 1.45, 0.42), n=34, block=0.7, chips=8, chip_range=(0.62, 0.86),
             flat_top=0.78, flat_bottom=0.6, top_tilt=0.09),
        dict(c=(0.45, -0.2, 0.72), r=(1.45, 1.0, 0.32), n=26, block=0.75, chips=6, chip_range=(0.66, 0.88),
             flat_top=0.8, flat_bottom=0.5, top_tilt=0.12),
        dict(c=(-1.25, 0.55, 0.62), r=(0.62, 0.55, 0.3), n=16, block=0.6, chips=3, flat_top=0.75, flat_bottom=0.5, top_tilt=0.2),
    ])


def _rock_block_a():
    rng = random.Random(404)
    corners = [(sx, sy, sz) for sx in (-1, 1) for sy in (-1, 1) for sz in (1, 1, -0.2)]
    return _chunks_mesh("ROCK_BLOCK_A", rng, [
        dict(c=(0, 0, 0.66), r=(0.98, 0.82, 0.74), n=56, block=0.95, chips=5, chip_range=(0.74, 0.88),
             flat_bottom=0.8, flat_top=0.94, top_tilt=0.07, chip_dirs=[corners[1], corners[4], corners[6], corners[9], (0.2, -1, 0.6)]),
        dict(c=(0.95, -0.55, 0.3), r=(0.42, 0.36, 0.34), n=30, block=0.8, chips=2, flat_bottom=0.6, flat_top=0.85),
    ], smooth_angle=10.0)


def _rock_cairn_a():
    rng = random.Random(505)
    stones = [((0.42, 0.0, 0.3), (0.5, 0.42, 0.34)), ((-0.3, 0.36, 0.28), (0.45, 0.4, 0.32)), ((-0.25, -0.4, 0.27), (0.44, 0.38, 0.3)),
              ((0.12, 0.12, 0.78), (0.4, 0.34, 0.26)), ((-0.16, -0.12, 0.74), (0.36, 0.3, 0.24)), ((0.0, 0.0, 1.16), (0.3, 0.26, 0.22))]
    return _chunks_mesh("ROCK_CAIRN_A", rng, [dict(c=c, r=r, n=16, block=0.5, chips=2, flat_bottom=0.55, flat_top=0.85,
                                                   top_tilt=0.15) for c, r in stones])


def _rock_stone_a():
    rng = random.Random(606)
    return _chunks_mesh("ROCK_STONE_A", rng, [
        dict(c=(0, 0, 0.17), r=(0.34, 0.27, 0.22), n=34, block=0.4, chips=3, flat_bottom=0.6),
        dict(c=(0.2, -0.12, 0.12), r=(0.2, 0.17, 0.14), n=22, block=0.5, chips=2, flat_bottom=0.5),
        dict(c=(-0.22, 0.1, 0.1), r=(0.17, 0.15, 0.12), n=20, block=0.5, chips=1, flat_bottom=0.5)])


def _rock_stone_b():
    rng = random.Random(707)
    return _chunks_mesh("ROCK_STONE_B", rng, [
        dict(c=(0, 0, 0.22), r=(0.42, 0.32, 0.28), n=46, block=0.65, chips=4, flat_bottom=0.6, flat_top=0.88, top_tilt=0.2),
        dict(c=(0.3, 0.18, 0.14), r=(0.2, 0.18, 0.15), n=36, block=0.5, chips=1, flat_bottom=0.5)])


def _rock_ledge_a():
    rng = random.Random(808)
    return _chunks_mesh("ROCK_LEDGE_A", rng, [
        dict(c=(-2.0, 0.1, 0.5), r=(1.6, 1.0, 0.7), n=24, block=0.65, chips=4, flat_top=0.82, flat_bottom=0.6, top_tilt=0.08),
        dict(c=(0.2, -0.1, 0.62), r=(1.7, 1.05, 0.82), n=26, block=0.65, chips=4, flat_top=0.8, flat_bottom=0.6, top_tilt=0.06),
        dict(c=(2.2, 0.15, 0.42), r=(1.3, 0.9, 0.58), n=22, block=0.6, chips=3, flat_top=0.8, flat_bottom=0.6, top_tilt=0.1),
    ])


def _rock_crag_a():
    """Big faceted outcrop 8 x 7 x 5.5 m (a ROCK_LARGE / CLIFF_ROCK replacement: scale 0.7-1.8 covers 6-14 m rocks)."""
    rng = random.Random(1201)
    return _chunks_mesh("ROCK_CRAG_A", rng, [
        dict(c=(0.0, 0.0, 2.1), r=(3.7, 3.1, 2.2), n=36, block=0.45, chips=6, chip_range=(0.7, 0.86), flat_bottom=0.75,
             flat_top=0.86, top_tilt=0.12),
        dict(c=(2.5, 1.2, 1.35), r=(2.0, 1.8, 1.45), n=22, block=0.5, chips=4, chip_range=(0.7, 0.86), flat_bottom=0.7),
        dict(c=(-2.4, -1.3, 1.3), r=(1.9, 1.6, 1.3), n=20, block=0.5, chips=3, chip_range=(0.7, 0.86), flat_bottom=0.7),
        dict(c=(0.4, -0.6, 3.7), r=(1.6, 1.4, 1.55), n=22, block=0.55, chips=4, flat_top=0.8, flat_bottom=0.6, top_tilt=0.2),
        dict(c=(-0.9, 1.7, 0.95), r=(1.5, 1.2, 1.0), n=16, block=0.5, chips=2, flat_bottom=0.6),
    ])


def _rock_crag_b():
    """Taller faceted outcrop 6.5 x 6 x 7.5 m (stacked shoulders, a narrower knob on top)."""
    rng = random.Random(1202)
    return _chunks_mesh("ROCK_CRAG_B", rng, [
        dict(c=(0.0, 0.0, 2.6), r=(2.8, 2.5, 2.7), n=34, block=0.5, chips=6, chip_range=(0.68, 0.86), flat_bottom=0.75,
             flat_top=0.9, top_tilt=0.1),
        dict(c=(1.1, 0.6, 5.0), r=(1.7, 1.6, 2.0), n=24, block=0.55, chips=4, chip_range=(0.7, 0.86), flat_top=0.82, top_tilt=0.22),
        dict(c=(-1.6, -0.7, 1.45), r=(1.7, 1.5, 1.45), n=20, block=0.5, chips=3, flat_bottom=0.7),
        dict(c=(1.9, -1.3, 1.2), r=(1.6, 1.4, 1.2), n=20, block=0.5, chips=3, flat_bottom=0.7),
        dict(c=(-0.3, 1.5, 1.1), r=(1.5, 1.3, 1.1), n=16, block=0.5, chips=2, flat_bottom=0.6),
    ])


def _core_pts(rng, z0, z1, r0, r1, off0, off1, levels=5, per=6, jag=0.14):
    pts, cen = [], []
    for i in range(levels):
        t = i / (levels - 1)
        z = z0 + (z1 - z0) * t
        r = (r0 + (r1 - r0) * t) * rng.uniform(0.9, 1.08)
        cx = off0[0] + (off1[0] - off0[0]) * t + rng.uniform(-jag, jag) * r
        cy = off0[1] + (off1[1] - off0[1]) * t + rng.uniform(-jag, jag) * r
        cen.append((cx, cy, z, r))
        a0 = rng.uniform(0, math.tau)
        for k in range(per):
            a = a0 + math.tau * k / per + rng.uniform(-0.3, 0.3)
            rr = r * rng.uniform(0.78, 1.14)
            pts.append(Vector((cx + math.cos(a) * rr, cy + math.sin(a) * rr * 0.86, z)))
    return pts, cen


def _grass_cap(z_from):
    """mat_fn: faces pointing up (n.z > 0.8) entirely above z_from get slot 1 (LK_ROUGH grass)."""
    def f(mb, k):
        idx = mb.F[k]
        if min(mb.V[i][2] for i in idx) < z_from:
            return 0
        a, b, c = (Vector(mb.V[idx[0]]), Vector(mb.V[idx[1]]), Vector(mb.V[idx[2]]))
        nrm = (b - a).cross(c - a)
        return 1 if nrm.length > 1e-12 and nrm.normalized().z > 0.8 else 0
    return f


# ----------------------------------------------------------------------------- tapering spires (sea stacks, needles)
def _bridge_rings(ringA, ringB):
    """Triangles between two closed rings with DIFFERENT corner counts (A below B, both counter-clockwise seen from above,
    so the faces point outward). ringX = [(vertex_index, angle), ...] with ascending angles in [0, 2 pi). Walks both rings by
    angle (zipper): every step adds one triangle, so 5 -> 8 corners just makes a few extra triangles."""
    na, nb = len(ringA), len(ringB)
    A = [v for v, _t in ringA]
    a_ang = [t for _v, t in ringA]
    j0 = min(range(nb), key=lambda j: abs(((ringB[j][1] - a_ang[0] + math.pi) % math.tau) - math.pi))
    Bs = ringB[j0:] + ringB[:j0]
    B = [v for v, _t in Bs]
    d0 = ((Bs[0][1] - a_ang[0] + math.pi) % math.tau) - math.pi
    b_ang = [a_ang[0] + d0 + ((t - Bs[0][1]) % math.tau) for _v, t in Bs]
    ea = a_ang + [a_ang[0] + math.tau]
    eb = b_ang + [b_ang[0] + math.tau]
    tris = []
    i = j = 0
    while i < na or j < nb:
        if i < na and (j >= nb or ea[i + 1] <= eb[j + 1]):
            tris.append((A[i], A[(i + 1) % na], B[j % nb]))
            i += 1
        else:
            tris.append((A[i % na], B[(j + 1) % nb], B[j]))
            j += 1
    return tris


SPIRE_SPECS = {
    # kind: (seed, height above the waterline, base radius, top radius / base radius, lean (x, y) as a share of the height,
    #        slice count, ledge heights (share), overhang heights (share), twist rad, summit style, taper exponent,
    #        bend (S-curve share of the height), bulge (radius swell at height bulge_u, share))
    "ROCK_SEASTACK_A": dict(seed=9101, height=13.0, base_r=3.1, top_ratio=0.35, lean=(0.09, -0.04), slices=13,
                            ledges=(0.40,), overhangs=(0.64,), twist=0.35, top="chisel", p=1.2, bend=(0.03, 0.02), bulge=0.16, bulge_u=0.20),
    "ROCK_SEASTACK_B": dict(seed=9102, height=18.0, base_r=2.9, top_ratio=0.30, lean=(-0.12, 0.05), slices=15,
                            ledges=(0.36,), overhangs=(0.62,), twist=-0.4, top="chisel", p=1.25, bend=(-0.03, 0.04), bulge=0.20, bulge_u=0.16),
    "ROCK_SEASTACK_C": dict(seed=9103, height=24.0, base_r=3.5, top_ratio=0.25, lean=(0.05, 0.11), slices=17,
                            ledges=(0.30, 0.60), overhangs=(0.45,), twist=0.5, top="double", p=1.3, bend=(0.04, -0.03), bulge=0.18, bulge_u=0.22),
    "ROCK_SPIRE_A": dict(seed=9104, height=11.0, base_r=1.8, top_ratio=0.27, lean=(0.05, 0.10), slices=12,
                         ledges=(0.45,), overhangs=(), twist=0.3, top="point", p=1.3, bend=(0.04, 0.04), bulge=0.10, bulge_u=0.24),
    # v2 repair: two more variants so that a hole with a dozen stacks does not repeat 4 meshes (D 16 m pointed, E 21 m slender twin summit)
    "ROCK_SEASTACK_D": dict(seed=9105, height=16.0, base_r=3.3, top_ratio=0.33, lean=(-0.09, -0.07), slices=14,
                            ledges=(0.42,), overhangs=(0.70,), twist=-0.3, top="point", p=1.3, bend=(-0.04, 0.03), bulge=0.18, bulge_u=0.18),
    "ROCK_SEASTACK_E": dict(seed=9106, height=21.0, base_r=2.7, top_ratio=0.28, lean=(0.12, -0.03), slices=16,
                            ledges=(0.47,), overhangs=(0.30,), twist=0.6, top="double", p=1.35, bend=(0.05, -0.04), bulge=0.20, bulge_u=0.20),
}
SEASTACK_CORE = ("ROCK_SEASTACK_A", "ROCK_SEASTACK_B", "ROCK_SEASTACK_C")          # the three documented sizes 13 / 18 / 24 m (height-only choice)
SEASTACK_KINDS = SEASTACK_CORE + ("ROCK_SEASTACK_D", "ROCK_SEASTACK_E")
SPIRE_KINDS = SEASTACK_KINDS + ("ROCK_SPIRE_A",)
SPIRE_SMOOTH = 30.0          # 2026-10-04 review (stacks read as faceted grey blobs): smooth shading inside a bed, sharp only at ledges / chips / big arrises
SPIRE_MAX_FACES = 470        # library budget 500 (verifier PROP_FACE_BUDGET), headroom for the wet / basalt twins (same polygons)


WAVE_AMP, WAVE_FREQ = 0.075, 2.3     # round 3: radius modulation of the spire profile: +-7.5 % with 2.3 periods over the height (a smooth waist / swell, which adds no flat faces)
SPIRE_CAP_AIM = 0.20         # library aim for the verifier's flat-cap number (postcard_look_verify.stack_metrics, limit 25 %): the group of up-facing triangles (nz >= 0.9, above 20 % of the height) that touch
#                              each other at a vertex, as a share of the hull of the section at 10 % of the height. Big ledge steps raise it; a stack that would be near the limit is re-rolled.


def _hull_area_2d(pts):
    P_ = sorted(set((round(float(x), 6), round(float(y), 6)) for x, y in pts))
    if len(P_) < 3:
        return 0.0

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for q in P_:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], q) <= 0:
            lo.pop()
        lo.append(q)
    for q in reversed(P_):
        while len(up) >= 2 and cross(up[-2], up[-1], q) <= 0:
            up.pop()
        up.append(q)
    h = lo[:-1] + up[:-1]
    return 0.5 * abs(sum(h[i][0] * h[(i + 1) % len(h)][1] - h[(i + 1) % len(h)][0] * h[i][1] for i in range(len(h))))


def spire_flat_cap(ob, height):
    """The verifier's flat-cap number of a library spire (world = local, waterline z = 0): largest vertex-connected group of triangles with nz >= 0.9 whose centre is above
    20 % of the height, area / area of the convex hull of the section at 10 % of the height."""
    me = ob.data
    me.calc_loop_triangles()
    nv = len(me.vertices)
    co = np.empty(nv * 3)
    me.vertices.foreach_get("co", co)
    W = co.reshape(-1, 3)
    nt = len(me.loop_triangles)
    tv = np.empty(nt * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", tv)
    T = tv.reshape(-1, 3)
    a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
    nrm = np.cross(b - a, c - a)
    ln = np.linalg.norm(nrm, axis=1)
    ok = ln > 1e-12
    nz = np.zeros(len(T))
    nz[ok] = nrm[ok, 2] / ln[ok]
    area = 0.5 * ln
    z0 = max(float(W[:, 2].min()), 0.0)
    H = float(W[:, 2].max()) - z0
    flat = (nz >= 0.9) & (area > 1e-12) & (W[T][:, :, 2].mean(axis=1) >= z0 + 0.2 * H)
    if not flat.any():
        return 0.0
    Tf, Af = T[flat], area[flat]
    parent = {}

    def find(x):
        r = x
        while parent.get(r, r) != r:
            r = parent[r]
        while parent.get(x, x) != r:
            parent[x], x = r, parent[x]
        return r
    for ta, tb, tc in Tf.tolist():
        ra = find(ta)
        for v_ in (tb, tc):
            rv = find(v_)
            if rv != ra:
                parent[rv] = ra
    lab = {}
    for (ta, _tb, _tc), ar in zip(Tf.tolist(), Af.tolist()):
        r_ = find(ta)
        lab[r_] = lab.get(r_, 0.0) + ar
    z10 = z0 + 0.1 * H
    pts = []
    for p_, q_ in ((a, b), (b, c), (c, a)):
        cr = (p_[:, 2] - z10) * (q_[:, 2] - z10) < 0
        if cr.any():
            t_ = (z10 - p_[cr, 2]) / (q_[cr, 2] - p_[cr, 2])
            pts.append((p_[cr] + (q_[cr] - p_[cr]) * t_[:, None])[:, :2])
    a10 = _hull_area_2d(np.vstack(pts)) if pts else 0.0
    return max(lab.values()) / max(a10, 1e-9)


def _spire_mesh(kind, seed, height, base_r, top_ratio, lean=(0.0, 0.0), slices=13, ledges=(), overhangs=(), twist=0.4,
                top="point", p=1.25, under=3.0, amp=0.24, mats=("LK_ROCK", "LK_ROUGH", "LK_PLANTS"), bed_m=1.8, bend=(0.0, 0.0),
                bulge=0.0, bulge_u=0.2):
    """Builds the spire with a face budget: bed_m (mean bed thickness) grows by 10 % per attempt until the mesh has <= SPIRE_MAX_FACES
    polygons (the gate numbers are checked by seastack_report). Returns the library object."""
    last, best = None, None
    for attempt in range(10):
        ob = _spire_mesh_try(kind, seed, height, base_r, top_ratio, lean, slices, ledges, overhangs, twist, top, p, under, amp, mats,
                             bed_m * (1.1 ** attempt), bend, bulge, bulge_u)
        last = ob
        if len(ob.data.polygons) <= SPIRE_MAX_FACES:
            cap = spire_flat_cap(ob, height)
            if cap <= SPIRE_CAP_AIM:
                return ob
            if best is None or cap < best[0]:                    # fits the budget, but the verifier's flat-cap number (SEASTACK_TAPERED <= 25 %) is too close: try the next random stream
                best = (cap, attempt)
    if best is not None:                                         # none reached the aim: rebuild the best one (deterministic: same seed, same bed_m)
        return _spire_mesh_try(kind, seed, height, base_r, top_ratio, lean, slices, ledges, overhangs, twist, top, p, under, amp, mats,
                               bed_m * (1.1 ** best[1]), bend, bulge, bulge_u)
    return last


def _spire_mesh_try(kind, seed, height, base_r, top_ratio, lean, slices, ledges, overhangs, twist, top, p, under, amp, mats, bed_m, bend=(0.0, 0.0),
                    bulge=0.0, bulge_u=0.2):
    """ONE tapering spire (sea stack / needle), review round 2026-10-04 (stacks read as faceted grey blobs, then as stacked plates):
    a dense stack of irregular polygon rings (5-9 corners, ring spacing ~bed_m / 2.4) whose CORNERS PERSIST from ring to ring (the
    corner count changes by one only now and then, angles drift a few hundredths of a radian), so the arrises run up the spire as
    vertical striation; the radius of a corner is a low-frequency-in-height noise (ridges and grooves that climb through many rings),
    plus vertical chips (a corner pushed in for 15-35 % of the height), plus a faint strata saw (hard / soft layers every ~bed_m,
    +-2.5 %), plus ledge PAIRS (two rings 12-20 cm apart: a thin up-facing mossy ledge where the spire steps in, or an undercut where it
    swells out) at the spec's shelves / overhangs and at two more heights; a buttress on one side of the lower third; lean + twist; a
    shrub crown on the summit. Round 2026-10-04 #2 (review: 'tapered obelisks with bright green pyramid caps'): (a) the summit is no longer a
    pointed pyramid of LK_ROUGH: the top ring flares out (an overhang, 18-38 %) into a lumpy 7-corner shrub dome of LK_PLANTS faces (atlas swatches G_DEEP / G_DARK /
    OLIVE_DARK / G_MID, dark base to a lighter top, one swatch per face), the dome off-centre on its neck; (b) the axis bends (bend = S-curve share of the height) and the
    radius swells low on the stack (bulge x at height bulge_u), so no two sides are symmetric and the foot is bulbous. Profile concave (top radius = top_ratio x base radius, (1-u)^p). z runs from
    -under to the summit. Shading: smooth, sharp only above SPIRE_SMOOTH degrees (ledges, big arrises, chips). The point attribute
    'lk_slice' stores every vertex's ring index (-1 = summit points). Returns the library object."""
    rng = random.Random(seed)
    H = float(height)
    R0 = float(base_r)
    R1 = R0 * top_ratio
    s1, s2, s3 = rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)
    butt_az = rng.uniform(0, math.tau)
    butt_amp = rng.uniform(0.16, 0.30)
    wave_ph = (seed % 97) / 97.0
    HC_T = min(1.7, 0.085 * H + 0.2)                               # height of the shrub dome: the neck ends this far under the summit (the mesh keeps its documented height H)
    U_TOP = 1.0 - HC_T / H

    def prof(u):
        r_ = R1 + (R0 - R1) * (1.0 - min(max(u, 0.0), 1.0)) ** p
        if bulge:
            r_ *= 1.0 + bulge * math.exp(-(((u - bulge_u) / 0.16) ** 2))        # a swelling low on the stack: bulbous, not a straight cone
        r_ *= 1.0 + WAVE_AMP * math.sin(math.tau * (WAVE_FREQ * u + wave_ph)) * min(1.0, 3.0 * (1.0 - u))     # round 3: smooth swells and waists (an eroded stack, not a straight cone); no flat faces, no mushroom
        return r_

    # ---- ledge pairs: (u, factor above the ledge); factor < 1 = steps in, > 1 = swells out; relaxes to 1 over 14 % of the height
    steps = []
    for uL in ledges:
        steps.append((uL, rng.uniform(0.85, 0.90)))                  # round 3 (review 3: 'Washington monument' silhouettes): the main shelf is a real shoulder (10-15 % step in, was 7-11 %; the verifier's flat-cap limit 25 % bounds it)
    for uO in overhangs:
        steps.append((uO, rng.uniform(1.09, 1.13)))
    for uE in (rng.uniform(0.20, 0.40), rng.uniform(0.55, 0.78)):
        if all(abs(uE - u0) > 0.12 for u0, _f in steps):
            steps.append((uE, rng.uniform(0.85, 0.91) if rng.random() < 0.7 else rng.uniform(1.07, 1.11)))
    for _try in range(40):                                           # minor bedding: thin ledges that step in / out by 2-4 % (at least 7 ledges in all)
        if len(steps) >= (8 if H > 15 else 7):
            break
        uE = rng.uniform(0.08, 0.86)
        if all(abs(uE - u0) > 0.06 for u0, _f in steps):
            steps.append((uE, rng.uniform(0.96, 0.98) if rng.random() < 0.6 else rng.uniform(1.02, 1.035)))
    steps.sort()
    gap = {u0: (rng.uniform(0.12, 0.20) if abs(f - 1.0) > 0.05 else rng.uniform(0.06, 0.10)) for u0, f in steps}

    def mstep(u):
        m = 1.0
        for u0, f in steps:
            if u >= u0 - 1e-9 and u < u0 + 0.14:
                m *= f + (1.0 - f) * min(1.0, (u - u0) / 0.14)      # each step relaxes to 1 over 14 % of the height; steps multiply
        return m

    # ---- ring heights
    n_target = 28.0 * 1.8 / bed_m                                  # rings: bed_m grows 10 % per budget attempt (_spire_mesh), so fewer rings
    sp = min(max(H * U_TOP / n_target, 0.5), 1.15)
    zs_levels = [0.0]
    while zs_levels[-1] < H * U_TOP - 0.6 * sp:
        zs_levels.append(zs_levels[-1] + sp * rng.uniform(0.8, 1.2))
    zs_levels = [z for z in zs_levels if z < H * U_TOP - 0.45 * sp] + [H * U_TOP]
    for u0, _f in steps:                                          # a ledge pair replaces the ring nearest to u0 * H
        zc = u0 * H
        k = min(range(1, len(zs_levels) - 1), key=lambda i: abs(zs_levels[i] - zc))
        zs_levels[k] = zc - 0.5 * gap[u0]
        zs_levels.insert(k + 1, zc + 0.5 * gap[u0])
        zs_levels.sort()
    saw_ph = rng.uniform(0, 1)
    saw_p = bed_m * rng.uniform(0.9, 1.15)
    # ---- corner bookkeeping: persistent angles
    mb = _MB(mats)
    rings, slice_of = [], []
    k_cur = int(rng.choice((6, 7, 7, 8)))
    ang = sorted(math.tau * (c + rng.uniform(-0.25, 0.25)) / k_cur for c in range(k_cur))
    chips = []                                                    # (angle, u_from, u_to, depth)
    for _ in range(max(4, int(H / 3.0))):                          # round 3: more and deeper bites, so the silhouette from any side is broken (was H / 4.5 chips, 16-30 % deep)
        u_a = rng.uniform(0.0, 0.8)
        chips.append((rng.uniform(0, math.tau), u_a, u_a + rng.uniform(0.12, 0.32), rng.uniform(0.22, 0.40)))

    def add_ring(z, uu, r_base, tag, k_fixed=None):
        nonlocal k_cur, ang
        # corner count: change by one now and then (never above 9 / below 5, fewer corners higher up)
        k_max = 9 if uu < 0.5 else 7
        if k_fixed is not None:
            want = k_fixed
        else:
            want = k_cur
            if rng.random() < 0.26:
                want = k_cur + rng.choice((-1, 1))
            want = min(k_max, max(5, want))
            if uu > 0.85:
                want = min(want, 6)
        while len(ang) < want:                                    # add a corner in the widest gap
            gaps = [((ang[(i + 1) % len(ang)] - ang[i]) % math.tau, i) for i in range(len(ang))]
            gmax, gi = max(gaps)
            ang.append((ang[gi] + gmax * rng.uniform(0.4, 0.6)) % math.tau)
            ang.sort()
        while len(ang) > want:                                    # drop the corner with the smallest gap
            gaps = [(((ang[(i + 1) % len(ang)] - ang[i]) % math.tau) + ((ang[i] - ang[i - 1]) % math.tau), i) for i in range(len(ang))]
            _g, gi = min(gaps)
            ang.pop(gi)
        k_cur = len(ang)
        ang = sorted((a + rng.uniform(-0.035, 0.035) + twist * 0.045 * (sp / max(H, 1.0)) * 10) % math.tau for a in ang)
        cx = lean[0] * H * max(uu, 0.0) ** 1.4 + bend[0] * H * math.sin(math.pi * min(max(uu, 0.0), 1.0)) + 0.12 * R0 * mnoise.noise(Vector((uu * 2.0 + s1, s2, 0.5)))
        cy = lean[1] * H * max(uu, 0.0) ** 1.4 + bend[1] * H * math.sin(math.pi * min(max(uu, 0.0), 1.0)) + 0.12 * R0 * mnoise.noise(Vector((s2, uu * 2.0 + s3, 0.5)))
        tl = (0.10 * mnoise.noise(Vector((uu * 1.3 + s1, 3.1, s3))), 0.10 * mnoise.noise(Vector((2.7, uu * 1.3 + s2, s3))))
        ring = []
        for a in ang:
            rf = 1.0 + amp * (1.0 + 0.5 * max(0.0, 0.4 - uu)) * mnoise.noise(Vector((math.cos(a) * 2.3 + s1, math.sin(a) * 2.3 + s2, uu * 0.7 + s3)))
            rf *= 1.0 + rng.uniform(-0.04, 0.04)
            for (ca, ua, ub_, dep) in chips:
                if ua <= uu <= ub_ and abs(((a - ca + math.pi) % math.tau) - math.pi) < 0.45:
                    rf *= 1.0 - dep
            rr = r_base * rf
            bw = max(0.0, 1.0 - uu / 0.55) * butt_amp * max(0.0, math.cos(a - butt_az)) ** 2
            rr *= 1.0 + bw
            x = cx + math.cos(a) * rr
            y = cy + math.sin(a) * rr * 0.9
            zz = z + tl[0] * (x - cx) + tl[1] * (y - cy)
            vi = mb.v((x, y, zz))
            slice_of.append(len(rings))
            ring.append((vi, a))
        rings.append(ring)

    # under the water: two plain wide rings (never seen)
    add_ring(-under, 0.0, R0 * 1.16, "under", k_fixed=8)
    add_ring(-under * 0.4, 0.0, R0 * 1.06, "under", k_fixed=8)
    for z in zs_levels:
        uu = z / H
        saw = 0.025 * (((z / saw_p + saw_ph) % 1.0) ** 2 - 0.33)       # hard / soft layers: swells slowly, steps back at the bed boundary
        add_ring(z, uu, prof(uu) * mstep(uu) * (1.0 + saw), "vis")
    # ---- wall faces
    wall = []
    for si in range(len(rings) - 1):
        for tri in _bridge_rings(rings[si], rings[si + 1]):
            wall.append(tri)
    # ---- summit: a flared neck + an overhanging shrub dome (no pointed pyramid)
    topring = rings[-1]
    tcx = sum(mb.V[v][0] for v, _a in topring) / len(topring)
    tcy = sum(mb.V[v][1] for v, _a in topring) / len(topring)
    zt = sum(mb.V[v][2] for v, _a in topring) / len(topring)
    rt = max(0.3, prof(U_TOP))
    ridge = rng.uniform(0, math.tau)
    roof = []                                                      # rock faces of the summit (the flared neck)
    crown = []                                                     # (tri, rings-above-the-neck index) faces of the shrub dome
    n_c = 7
    r_top = sum(math.hypot(mb.V[v][0] - tcx, mb.V[v][1] - tcy) for v, _a in topring) / len(topring)    # the MEASURED neck radius (ledge steps + chips make it ~0.6 x the profile)
    rc = max(0.55, r_top * rng.uniform(1.0, 1.15))                 # the dome overhangs the neck by 0-15 % (the neck's silhouette is narrower than its corner radius): a bush growing from the top, not a hat
    hc = max(H - zt, 0.5)                                          # the apex is exactly at H; HC_T keeps the 90 % band of the gate (SEASTACK_TAPERED) under the dome
    stretch = 1.35 if top == "double" else (1.18 if top == "chisel" else 1.0)
    off = rng.uniform(0.0, 0.22) * rt
    oa = rng.uniform(0, math.tau)
    ccx, ccy = tcx + math.cos(oa) * off, tcy + math.sin(oa) * off

    def crown_ring(z, r, jit, k=n_c):
        a0_ = rng.uniform(0, math.tau)
        angs = sorted((a0_ + math.tau * (c_ + rng.uniform(-0.22, 0.22)) / k) % math.tau for c_ in range(k))
        ring_ = []
        for a_ in angs:
            rr_ = r * rng.uniform(1.0 - jit, 1.0 + jit)
            ex = math.cos(a_) * rr_ * stretch
            ey = math.sin(a_) * rr_ * 0.9
            x_ = ccx + ex * math.cos(ridge) - ey * math.sin(ridge)
            y_ = ccy + ex * math.sin(ridge) + ey * math.cos(ridge)
            ring_.append((mb.v((x_, y_, z)), a_))
            slice_of.append(-1)
        return ring_
    R_b0 = crown_ring(zt + 0.06 * hc, rc * 1.04, 0.20)                    # the overhang rim (its underside is rock)
    R_g1 = crown_ring(zt + 0.36 * hc, rc * 1.12, 0.24)             # the widest part of the dome
    R_g2 = crown_ring(zt + 0.74 * hc, rc * 0.72, 0.34)
    apexes = [(ccx + rng.uniform(-0.2, 0.2) * rc, ccy + rng.uniform(-0.2, 0.2) * rc, zt + hc)]
    if top == "double":                                            # a second, lower lump on the same crown
        apexes = [(ccx + math.cos(ridge) * 0.42 * rc, ccy + math.sin(ridge) * 0.42 * rc, zt + hc),
                  (ccx - math.cos(ridge) * 0.46 * rc, ccy - math.sin(ridge) * 0.46 * rc, zt + 0.84 * hc)]
    apex = []
    for q in apexes:
        apex.append(mb.v(q))
        slice_of.append(-1)
    for tri in _bridge_rings(topring, R_b0):
        roof.append(tri)
    for tri in _bridge_rings(R_b0, R_g1):
        crown.append((tri, 0))
    for tri in _bridge_rings(R_g1, R_g2):
        crown.append((tri, 1))
    m2 = len(R_g2)
    if len(apex) == 1:
        for i in range(m2):
            crown.append(((R_g2[i][0], R_g2[(i + 1) % m2][0], apex[0]), 2))
    else:
        pa, pb = [Vector(q) for q in apexes]
        owner = []
        for v, _a in R_g2:
            co = Vector(mb.V[v])
            owner.append(0 if (co - pa).length_squared <= (co - pb).length_squared else 1)
        if len(set(owner)) < 2:
            owner = [0 if i < m2 // 2 else 1 for i in range(m2)]
        for i in range(m2):
            j = (i + 1) % m2
            vi, vj = R_g2[i][0], R_g2[j][0]
            if owner[i] == owner[j]:
                crown.append(((vi, vj, apex[owner[i]]), 2))
            else:
                crown.append(((vi, vj, apex[owner[j]]), 2))
                crown.append(((vi, apex[owner[j]], apex[owner[i]]), 2))
    # ---- bottom cap
    bottom = [v for v, _a in rings[0]][::-1]
    # ---- materials: grass on the roof, the crown and on up-facing ledges (patchy: never one continuous stripe), never the foot
    rg = random.Random(seed + 77)           # own stream: the geometry above is untouched
    for tri in wall + roof:
        a, b, c = (Vector(mb.V[i]) for i in tri)
        nrm = (b - a).cross(c - a)
        nz = nrm.normalized().z if nrm.length > 1e-12 else 0.0
        zlo = min(a.z, b.z, c.z)
        patch = rg.random() < 0.55
        grass = nz > 0.55 and zlo > 0.12 * H and patch                  # moss on up-facing ledges only (patchy); the summit carries the shrub dome
        mb.f(tri, 1 if grass else 0)
    # the shrub dome: LK_PLANTS atlas swatches, ONE swatch per face (muted greens: dark base, lighter top)
    SW_LOW, SW_MID, SW_HI = ("G_DEEP", "G_DEEP", "OLIVE_DARK"), ("G_DEEP", "G_DARK", "OLIVE_DARK"), ("G_DARK", "OLIVE_DARK", "G_MID")
    for tri, lv in crown:
        zq = [mb.V[i][2] for i in tri]
        zm = (sum(zq) / 3.0 - zt) / max(hc, 1e-6)
        sw = rg.choice(SW_LOW if zm < 0.3 else (SW_MID if zm < 0.65 else SW_HI))
        mb.f(tri, 2, uv=[swatch_uv(sw, rg.uniform(0.25, 0.75), min(1.0, max(0.0, 0.12 + 0.8 * (mb.V[i][2] - zt) / max(hc, 1e-6)))) for i in tri])
    mb.f(bottom, 0)
    ob = _finalize(kind, mb, "box", smooth_angle=SPIRE_SMOOTH, ground=None)
    me = ob.data
    att = me.attributes.get("lk_slice")
    if att is None:
        att = me.attributes.new("lk_slice", 'INT', 'POINT')
    att.data.foreach_set("value", np.array(slice_of, np.int32))
    me["lk_spire"] = 1
    return ob


def _spire_builder(kind):
    spec = SPIRE_SPECS[kind]
    return lambda: _spire_mesh(kind, **spec)


def _rock_outcrop_a():
    """Flat-topped rock island (an arch / ruin stands on it): one faceted core whose top ring is exactly flat at z = 8 m (grass
    cap), buttresses round the foot, base 3 m under the water. place_outcrop() fits it to a radius and a top height."""
    rng = random.Random(1101)
    core, cen = _core_pts(rng, -3.0, 8.0, 7.0, 5.6, (0, 0), (0.3, -0.2), levels=5, per=9, jag=0.05)
    chunks = [dict(pts=core, merge=0.05)]
    for j in range(5):
        cx, cy, z, r = cen[rng.randrange(0, 3)]
        aa = math.tau * j / 5 + rng.uniform(-0.4, 0.4)
        br = r * rng.uniform(0.32, 0.45)
        hz = rng.uniform(1.6, 2.8)
        chunks.append(dict(c=(cx + math.cos(aa) * r * 0.85, cy + math.sin(aa) * r * 0.78, rng.uniform(-0.5, 3.5)),
                           r=(br, br * 0.9, hz), n=16, block=0.6, chips=3, chip_range=(0.7, 0.88), flat_top=0.8,
                           flat_bottom=0.8, top_tilt=0.1))
    return _chunks_mesh("ROCK_OUTCROP_A", rng, chunks, mats=("LK_ROCK", "LK_ROUGH"), mat_fn=_grass_cap(7.9), ground=None)


ROCK_KINDS = ("ROCK_BOULDER_A", "ROCK_BOULDER_B", "ROCK_SLAB_A", "ROCK_BLOCK_A", "ROCK_CAIRN_A", "ROCK_STONE_A",
              "ROCK_STONE_B", "ROCK_LEDGE_A", "ROCK_SEASTACK_A", "ROCK_SEASTACK_B", "ROCK_SEASTACK_C", "ROCK_SEASTACK_D",
              "ROCK_SEASTACK_E", "ROCK_SPIRE_A", "ROCK_OUTCROP_A", "ROCK_CRAG_A", "ROCK_CRAG_B")


# ----------------------------------------------------------------------------- basalt columns
def _hex_lattice(col_r, x0, y0, x1, y1, rng, jitter=0.03, rows_x=False):
    """Centres of a tight hex packing (circumradius col_r): hexagons with a vertex at angle 0 (rows_x=False) or at 30 deg
    (rows_x=True: straight rows along X at constant y, used by the walls so a row is one height step)."""
    if rows_x:
        a1 = (math.sqrt(3) * col_r, 0.0)
        a2 = (math.sqrt(3) / 2 * col_r, 1.5 * col_r)
    else:
        a1 = (1.5 * col_r, math.sqrt(3) / 2 * col_r)
        a2 = (0.0, math.sqrt(3) * col_r)
    out = []
    imax = int(math.ceil(max(abs(x0), abs(x1)) / a1[0])) + 2
    jmax = int(math.ceil((max(abs(y0), abs(y1)) + imax * a1[1]) / a2[1])) + 2
    for i in range(-imax, imax + 1):
        for j in range(-jmax, jmax + 1):
            x = i * a1[0] + j * a2[0]
            y = i * a1[1] + j * a2[1]
            if x0 <= x <= x1 and y0 <= y <= y1:
                out.append((x + rng.uniform(-jitter, jitter) * col_r, y + rng.uniform(-jitter, jitter) * col_r))
    return out


def _hex_column(mb, rng, cx, cy, top, bottom, col_r, gap=0.045, tilt_max=0.2, chip_p=0.35, rot_jit=0.035,
                cap_bottom=False, mat=0, a_off=0.0, top_max=None):
    """One hex prism (6 side quads + top hexagon), top plane tilted up to tilt_max rad, a chipped top corner with
    probability chip_p (one corner lowered, a triangular chip facet), optional bottom cap. Outward winding."""
    r = col_r * (1.0 - gap) * rng.uniform(0.97, 1.03)
    a0 = a_off + rng.uniform(-rot_jit, rot_jit)
    ring = [(cx + r * math.cos(a0 + k * math.pi / 3), cy + r * math.sin(a0 + k * math.pi / 3)) for k in range(6)]
    t = rng.uniform(0.0, tilt_max)
    ph = rng.uniform(0.0, math.tau)
    gx, gy = math.cos(ph) * math.tan(t), math.sin(ph) * math.tan(t)
    tz = [top + (x - cx) * gx + (y - cy) * gy for x, y in ring]
    if top_max is not None and max(tz) > top_max:      # a tilted top never rises above top_max (skins under a play surface)
        dz = top_max - max(tz)
        tz = [z + dz for z in tz]
    lo = max(tz) - min(tz)
    bz = [bottom] * 6
    B = [mb.v((x, y, z)) for (x, y), z in zip(ring, bz)]
    chip = rng.random() < chip_p and (min(tz) - bottom) > 1.2 * r
    if not chip:
        T = [mb.v((x, y, z)) for (x, y), z in zip(ring, tz)]
        for k in range(6):
            k1 = (k + 1) % 6
            mb.f((B[k], B[k1], T[k1], T[k]), mat)
        mb.f(T, mat)
    else:
        k0 = rng.randrange(6)
        km, kp = (k0 - 1) % 6, (k0 + 1) % 6
        depth = rng.uniform(0.3, 0.65) * r + lo * 0.5
        T = [None] * 6
        for k in range(6):
            x, y = ring[k]
            T[k] = mb.v((x, y, tz[k] - (depth if k == k0 else 0.0)))
        f1, f2 = rng.uniform(0.35, 0.6), rng.uniform(0.35, 0.6)
        pa = [ring[k0][i] + (ring[km][i] - ring[k0][i]) * f1 for i in range(2)]
        pb = [ring[k0][i] + (ring[kp][i] - ring[k0][i]) * f2 for i in range(2)]
        A = mb.v((pa[0], pa[1], tz[k0] + (tz[km] - tz[k0]) * f1))
        Bp = mb.v((pb[0], pb[1], tz[k0] + (tz[kp] - tz[k0]) * f2))
        for k in range(6):
            k1 = (k + 1) % 6
            if k == km:                     # edge km -> k0 holds A
                mb.f((B[k], B[k1], T[k1], A, T[k]), mat)
            elif k == k0:                   # edge k0 -> kp holds Bp
                mb.f((B[k], B[k1], T[k1], Bp, T[k]), mat)
            else:
                mb.f((B[k], B[k1], T[k1], T[k]), mat)
        top_poly = []
        for k in range(6):
            if k == k0:
                top_poly += [A, Bp]
            else:
                top_poly.append(T[k])
        mb.f(top_poly, mat)
        mb.f((Bp, A, T[k0]), mat)
    if cap_bottom:
        mb.f(list(reversed(B)), mat)


def build_basalt_cluster(kind, rx=3.4, ry=3.0, height=4.5, col_r=0.75, seed=0, dome=0.55, var=0.18, terrace=None,
                         bury=1.5, taper=0.0, tilt_max=0.2, chip_p=0.35, ramp=None, edge_noise=0.18, max_faces=500,
                         uv_scale=1.0):
    """Library basalt column cluster `kind` (must start 'ROCK_'): tight hex prisms (circumradius col_r) inside a noisy
    ellipse rx x ry, heights = height x dome profile x (1 +- var) (optional terrace step in metres, optional ramp (dx, dy)
    adding height along a direction), tops tilted / chipped, bottoms at -bury (taper > 0 lifts the outer bottoms into an
    inverted dome and caps them). Columns are dropped from the edge inward until faces <= max_faces. Returns the object."""
    rng = random.Random(seed * 7907 + 17)
    cs = _hex_lattice(col_r, -rx, -ry, rx, ry, rng)
    keep = []
    for x, y in cs:
        ang = math.atan2(y / ry, x / rx)
        lim = 1.0 + edge_noise * (mnoise.noise(Vector((math.cos(ang) * 1.7 + seed, math.sin(ang) * 1.7, 0.3))))
        d = math.hypot(x / rx, y / ry)
        if d <= lim:
            keep.append((x, y, d))
    keep.sort(key=lambda p: p[2])
    per_col = 8.4 + (1.0 if taper > 0 else 0.0)
    keep = keep[:max(1, int(max_faces / per_col))]
    mb = _MB(("LK_BASALT",))
    for x, y, d in keep:
        h = height * (1.0 - dome * d * d) * (1.0 + rng.uniform(-var, var))
        if ramp is not None:
            h += ramp[0] * x + ramp[1] * y
        if terrace:
            h = max(terrace, round(h / terrace) * terrace + rng.uniform(-0.08, 0.08) * terrace)
        h = max(h, 0.35)
        zb = -bury + (taper * height * d ** 1.6 if taper > 0 else 0.0)
        zb = min(zb, h - 0.4)
        _hex_column(mb, rng, x, y, h, zb, col_r, tilt_max=tilt_max, chip_p=chip_p, cap_bottom=taper > 0)
    while len(mb.F) > max_faces:      # safety: should not trigger
        mb.F.pop()
        mb.M.pop()
        mb.T.pop()
    return _finalize(kind, mb, "box", smooth_angle=None, uv_scale=uv_scale)


def build_basalt_wall(kind, length=40.0, depth=11.0, height=46.0, col_r=2.4, seed=0, bury=6.0, front_ratio=0.6,
                      broken=0.18, low_mid=0.0, end_drop=0.35, tilt_max=0.16, chip_p=0.4, max_faces=500, max_tris=500):
    """Library basalt WALL segment `kind` ('ROCK_WALL_*'): hex columns in a length x depth band, front rows (-Y) at
    front_ratio x height rising to height at the back, random broken columns (share `broken`), optional dip in the middle
    (low_mid 0..1), ends dropped by end_drop so neighbouring segments overlap cleanly; bottoms at -bury. Returns the object."""
    rng = random.Random(seed * 6151 + 29)
    cs = _hex_lattice(col_r, -length / 2, -depth / 2, length / 2, depth / 2, rng, rows_x=True)
    cs.sort(key=lambda p: (abs(p[0]) / length + 0.3 * rng.random()))
    cs = cs[:max(1, min(int(max_faces / 8.4), int(max_tris / 18.6)))]     # ~8.4 faces / ~18.4 triangles per column
    mb = _MB(("LK_BASALT",))
    for x, y in cs:
        fy = (y + depth / 2) / depth
        # stepped rows (front lower), a smooth low-frequency skyline, small per-column jitter: a cliff, not battlements
        h = height * (front_ratio + (1 - front_ratio) * fy)
        h *= 1.0 + 0.09 * mnoise.noise(Vector((x / (length * 0.5) + seed * 3.7, fy * 0.6, 0.5)))
        h *= 1.0 + rng.uniform(-0.025, 0.025)
        ex = abs(x) / (length / 2)
        if ex > 0.7:
            h *= 1.0 - end_drop * min(1.0, (ex - 0.7) / 0.3) ** 1.5
        if low_mid > 0:
            h *= 1.0 - low_mid * math.exp(-((x / (length * 0.22)) ** 2))
        if mnoise.noise(Vector((x / (col_r * 3.0) + seed * 1.3, y / (col_r * 3.0), 2.5))) > 0.55 - broken:
            h *= 0.86                               # broken groups of columns (a step in the skyline)
        _hex_column(mb, rng, x, y, h, -bury, col_r, tilt_max=tilt_max, chip_p=chip_p, a_off=math.pi / 6)
    return _finalize(kind, mb, "box", smooth_angle=None)


def _basalt_a():
    return build_basalt_cluster("ROCK_BASALT_A", 3.5, 3.2, 4.6, 0.72, seed=1, dome=0.55, terrace=0.45)


def _basalt_b():
    return build_basalt_cluster("ROCK_BASALT_B", 3.6, 1.5, 2.4, 0.66, seed=2, dome=0.25, ramp=(0.55, 0.0), terrace=0.4)


def _basalt_c():
    return build_basalt_cluster("ROCK_BASALT_C", 2.4, 2.2, 10.0, 0.7, seed=3, dome=0.3, var=0.12, taper=0.35, bury=2.0,
                                tilt_max=0.24)


def _basalt_d():
    return build_basalt_cluster("ROCK_BASALT_D", 4.6, 3.6, 1.5, 0.88, seed=4, dome=0.6, var=0.3, tilt_max=0.28, chip_p=0.25,
                                bury=1.0)


def _basalt_e():
    """12 m column cluster (tall: scale 0.7-1.6 covers 8-19 m)."""
    return build_basalt_cluster("ROCK_BASALT_E", 4.6, 4.0, 12.0, 0.95, seed=5, dome=0.5, var=0.15, bury=1.5, tilt_max=0.2,
                                chip_p=0.4, uv_scale=1.0)


def _basalt_f():
    """8 m column cluster."""
    return build_basalt_cluster("ROCK_BASALT_F", 3.7, 3.3, 8.0, 0.8, seed=6, dome=0.45, var=0.18, bury=1.5, tilt_max=0.22,
                                chip_p=0.4, terrace=0.8, uv_scale=1.0)


def _tower_s():
    """Wall-ring tower, SMALL: 14 x 13 m footprint, 22 m tall (6 m of it buried), >= 20 hex columns of 1.4 m."""
    return build_basalt_cluster("ROCK_WALL_BASALT_S", 7.0, 6.4, 16.0, 1.7, seed=11, dome=0.2, var=0.3, bury=6.0, tilt_max=0.2,
                                chip_p=0.5, terrace=1.2, edge_noise=0.22, uv_scale=1.3)


def _tower_m():
    """Wall-ring tower, MEDIUM: 22 x 20 m footprint, 34 m tall (6 m buried), hex columns of 2.1 m."""
    return build_basalt_cluster("ROCK_WALL_BASALT_M", 11.0, 10.0, 28.0, 2.5, seed=12, dome=0.2, var=0.3, bury=6.0, tilt_max=0.2,
                                chip_p=0.5, terrace=2.0, edge_noise=0.22, uv_scale=1.5)


def _tower_l():
    """Wall-ring tower, LARGE: 30 x 27 m footprint, 48 m tall (6 m buried), hex columns of 2.8 m."""
    return build_basalt_cluster("ROCK_WALL_BASALT_L", 15.0, 13.5, 42.0, 3.2, seed=13, dome=0.2, var=0.3, bury=6.0, tilt_max=0.2,
                                chip_p=0.5, terrace=3.0, edge_noise=0.22, uv_scale=1.5)


def _wall_a():
    return build_basalt_wall("ROCK_WALL_BASALT_A", 40.0, 11.0, 46.0, 2.4, seed=1)


def _wall_b():
    return build_basalt_wall("ROCK_WALL_BASALT_B", 36.0, 10.0, 42.0, 2.3, seed=2, low_mid=0.32, broken=0.3)


def _wall_c():
    return build_basalt_wall("ROCK_WALL_BASALT_C", 28.0, 9.0, 32.0, 2.0, seed=3, front_ratio=0.5)


BASALT_KINDS = ("ROCK_BASALT_A", "ROCK_BASALT_B", "ROCK_BASALT_C", "ROCK_BASALT_D", "ROCK_BASALT_E", "ROCK_BASALT_F")
WALL_KINDS = ("ROCK_WALL_BASALT_A", "ROCK_WALL_BASALT_B", "ROCK_WALL_BASALT_C")        # long thin wall segments
TOWER_KINDS = ("ROCK_WALL_BASALT_S", "ROCK_WALL_BASALT_M", "ROCK_WALL_BASALT_L")      # the 3 sizes of wall-ring column towers


# ----------------------------------------------------------------------------- masonry blocks
def _block_points(rng, sx, sy, sz, ch, z0=0.0):
    pts = []
    for X in (-1, 1):
        for Y in (-1, 1):
            for Z in (-1, 1):
                cx, cy, cz = X * sx / 2, Y * sy / 2, z0 + sz / 2 + Z * sz / 2
                pts.append(Vector((cx - X * ch, cy, cz)))
                pts.append(Vector((cx, cy - Y * ch, cz)))
                pts.append(Vector((cx, cy, cz - Z * ch)))
    return pts


def _dress_block(kind, size, seed, chips):
    rng = random.Random(seed)
    pts = _block_points(rng, size[0], size[1], size[2], 0.05)
    for p in pts:
        p += Vector((rng.uniform(-0.015, 0.015), rng.uniform(-0.015, 0.015), rng.uniform(-0.015, 0.015)))
    for _ in range(chips):
        nv = Vector((rng.choice((-1, 1)) * rng.uniform(0.5, 1), rng.choice((-1, 1)) * rng.uniform(0.5, 1), rng.uniform(0.3, 1))).normalized()
        cz = Vector((0, 0, size[2] / 2))
        h = max((p - cz).dot(nv) for p in pts)
        lim = h * rng.uniform(0.72, 0.85)
        for p in pts:
            x = (p - cz).dot(nv)
            if x > lim:
                p -= nv * (x - lim)
    V, F = _hull(pts, merge=0.01)
    mb = _MB(("LK_MASONRY",))
    mb.add(V, F)
    return _finalize(kind, mb, "box", smooth_angle=40.0)


def _dress_block_a():
    return _dress_block("DRESS_BLOCK_A", (1.0, 0.6, 0.55), 11, 1)


def _dress_block_b():
    return _dress_block("DRESS_BLOCK_B", (1.4, 0.7, 0.62), 12, 2)


# ----------------------------------------------------------------------------- plants
def _ccw(pts2):
    a = 0.0
    for i in range(len(pts2)):
        x0, y0 = pts2[i]
        x1, y1 = pts2[(i + 1) % len(pts2)]
        a += x0 * y1 - x1 * y0
    return a > 0


def _blade(mb, base, height, width, thick, dirv, lean, sw, s, bend=0.35, mat=0, sw_tip=None):
    """A chunky grass blade: a tapered 3-sided prism (base triangle, mid ring, tip point), leaning along dirv."""
    dx, dy = dirv
    sx, sy = -dy, dx
    bx, by, bz = base
    sec = [(bx + sx * width / 2, by + sy * width / 2), (bx + dx * thick, by + dy * thick), (bx - sx * width / 2, by - sy * width / 2)]
    if not _ccw(sec):
        sec.reverse()
    mh = 0.5
    mx, my = dx * lean * height * bend, dy * lean * height * bend
    tx, ty = dx * lean * height, dy * lean * height
    tip_sw = sw_tip or sw
    Bi = [mb.v((x, y, bz), swatch_uv(sw, s + (j - 1) * 0.09, 0.03)) for j, (x, y) in enumerate(sec)]
    cxm, cym = bx + mx, by + my
    Mi = [mb.v((cxm + (x - bx) * 0.6, cym + (y - by) * 0.6, bz + height * mh), swatch_uv(sw, s + (j - 1) * 0.07, 0.5))
          for j, (x, y) in enumerate(sec)]
    T = mb.v((bx + tx, by + ty, bz + height * (1.0 - 0.18 * lean)), swatch_uv(tip_sw, s, 0.97))
    for k in range(3):
        k1 = (k + 1) % 3
        mb.f((Bi[k], Bi[k1], Mi[k1], Mi[k]), mat)
        mb.f((Mi[k], Mi[k1], T), mat)


def _blade_arc(mb, base, height, width, thick, dirv, lean, sw, s, ts=(0.5,), curve=1.7, taper=(0.72,), mat=0, drop=0.2):
    """A TALL curved blade (v2 2026-10-05, the fringe tufts): a 3-sided prism like _blade, but the axis bends along dirv: the horizontal offset at
    height fraction t is lean x height x t ** curve (the lower part stands, the tip arches over) and the section tapers at every ring in `ts`
    (taper = the section scale there). Faces: 3 quads per segment + 3 tip triangles (len(ts) = 1 -> 6 faces / 9 triangles, 2 -> 9 / 15)."""
    dx, dy = dirv
    sx, sy = -dy, dx
    bx, by, bz = base
    off = [(sx * width / 2, sy * width / 2), (dx * thick, dy * thick), (-sx * width / 2, -sy * width / 2)]
    if not _ccw(off):
        off.reverse()

    def axis(t):
        o = lean * height * t ** curve
        return bx + dx * o, by + dy * o, bz + height * (t - drop * lean * t * t)
    rings = [[mb.v((bx + ox, by + oy, bz), swatch_uv(sw, s + (j - 1) * 0.09, 0.03)) for j, (ox, oy) in enumerate(off)]]
    for k, t in enumerate(ts):
        cx, cy, cz = axis(t)
        f = taper[k]
        rings.append([mb.v((cx + ox * f, cy + oy * f, cz), swatch_uv(sw, s + (j - 1) * 0.07, 0.03 + 0.94 * t)) for j, (ox, oy) in enumerate(off)])
    tx, ty, tz = axis(1.0)
    T = mb.v((tx, ty, tz), swatch_uv(sw, s, 0.97))
    for r in range(len(rings) - 1):
        A, B = rings[r], rings[r + 1]
        for k in range(3):
            k1 = (k + 1) % 3
            mb.f((A[k], A[k1], B[k1], B[k]), mat)
    last = rings[-1]
    for k in range(3):
        k1 = (k + 1) % 3
        mb.f((last[k], last[k1], T), mat)


def _blade_cone(mb, base, height, width, dirv, lean, sw, s, mat=0):
    """The CHEAP blade of the filler tufts: a 3-sided tapered cone (base triangle -> tip), 3 faces / 3 triangles."""
    dx, dy = dirv
    sx, sy = -dy, dx
    bx, by, bz = base
    sec = [(bx + sx * width / 2, by + sy * width / 2), (bx + dx * width * 0.45, by + dy * width * 0.45), (bx - sx * width / 2, by - sy * width / 2)]
    if not _ccw(sec):
        sec.reverse()
    Bi = [mb.v((x, y, bz), swatch_uv(sw, s + (j - 1) * 0.09, 0.03)) for j, (x, y) in enumerate(sec)]
    T = mb.v((bx + dx * lean * height, by + dy * lean * height, bz + height * (1.0 - 0.2 * lean)), swatch_uv(sw, s, 0.97))
    for k in range(3):
        mb.f((Bi[k], Bi[(k + 1) % 3], T), mat)


def _tuft(kind, seed, swatches, n_blades=13, h=(0.24, 0.44), w=(0.055, 0.08), spread=0.1):
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    for i in range(n_blades):
        mb.grp = i
        a = math.tau * i / n_blades + rng.uniform(-0.25, 0.25)
        rr = rng.uniform(0.0, spread)
        base = (math.cos(a) * rr, math.sin(a) * rr, -0.03)
        d = (math.cos(a + rng.uniform(-0.3, 0.3)), math.sin(a + rng.uniform(-0.3, 0.3)))
        lean = rng.uniform(0.18, 0.62) if rr > 0.02 else rng.uniform(0.05, 0.25)
        sw = swatches[i % len(swatches)]
        _blade(mb, base, rng.uniform(*h), rng.uniform(*w), 0.024, d, lean, sw, rng.uniform(0.2, 0.8))
    return _finalize(kind, mb, "vertex", all_smooth=True)


def _ico_lump(rng, center, radii, seed_off, flatten=-0.35, subdiv=2, wobble=0.12):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    verts, faces = [], []
    bm.verts.index_update()
    rot = Matrix.Rotation(rng.uniform(0, math.tau), 3, 'Z')
    for v in bm.verts:
        p = v.co.copy()
        p *= 1.0 + wobble * mnoise.noise(p * 1.6 + Vector((seed_off, seed_off * 0.7, 0.0)))
        p = Vector((p.x * radii[0], p.y * radii[1], max(p.z, flatten) * radii[2]))
        verts.append(rot @ p + Vector(center))
    faces = [[v.index for v in f.verts] for f in bm.faces]
    bm.free()
    return verts, faces


def _shrub(kind, seed, lumps, swatches, blades=0, blade_sw=None, fringe=0, fringe_h=(0.3, 0.6), fringe_sw=None, fringe_tip=None, wobble=0.12, mottle=0.06):
    """Shrub = a few low, overlapping lumps + (round 3) a FRINGE of leaf blades that grow out of the upper half of the lumps and lean outward: the silhouette is a tufty,
    irregular clump instead of a stack of round balls (review 3, hole 9: 'bushes like lime blobs'). fringe = number of blades, fringe_h = (min, max) height in m. OPTIONAL and unused by
    the three shrubs (the spiky fringe read as thorns in the EEVEE check); the shrubs are low overlapping lumps with olive swatches (wobble 0.18-0.22)."""
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    allz = [c[2] + r[2] for c, r in lumps]
    zmax = max(allz)
    for li, (c, r) in enumerate(lumps):
        mb.grp = li
        V, F = _ico_lump(rng, c, r, seed * 3.1 + li * 1.7, wobble=wobble)
        sw = swatches[li % len(swatches)]
        uvs = []
        for p in V:
            t = 0.1 + 0.82 * min(1.0, max(0.0, p.z / zmax)) + mottle * mnoise.noise(p * 3.1 + Vector((5.0, 1.0, 2.0)))
            uvs.append(swatch_uv(sw, 0.5 + 0.35 * mnoise.noise(p * 2.0), t))
        mb.add(V, F, 0, uvs)
    for i in range(blades):
        mb.grp = 50 + i
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(0.25, 0.6)
        _blade(mb, (math.cos(a) * rr, math.sin(a) * rr, 0.0), rng.uniform(0.45, 0.75), 0.06, 0.024,
               (math.cos(a), math.sin(a)), rng.uniform(0.2, 0.45), blade_sw, rng.uniform(0.2, 0.8))
    if fringe:
        fsw = fringe_sw or swatches
        wts = [r[0] * r[1] for _c, r in lumps]
        for i in range(fringe):
            mb.grp = 100 + i
            li = rng.choices(range(len(lumps)), weights=wts)[0]
            (cx, cy, cz), (rx, ry, rz) = lumps[li]
            a = rng.uniform(0, math.tau)
            el = rng.uniform(0.25, 1.15)                                  # rad above the horizon: the blade starts on the upper half of the lump
            bx, by, bz = cx + math.cos(el) * math.cos(a) * rx * 0.8, cy + math.cos(el) * math.sin(a) * ry * 0.8, cz + math.sin(el) * rz * 0.8
            d = (math.cos(a + rng.uniform(-0.4, 0.4)), math.sin(a + rng.uniform(-0.4, 0.4)))
            h = rng.uniform(*fringe_h) * (0.75 if el > 0.9 else 1.0)
            _blade(mb, (bx, by, bz), h, rng.uniform(0.13, 0.18), 0.05, d, rng.uniform(0.3, 0.65), fsw[i % len(fsw)], rng.uniform(0.2, 0.8),
                   bend=0.5, sw_tip=fringe_tip)
    return _finalize(kind, mb, "vertex", all_smooth=True, ground=-0.04)


def _stalk(mb, base, top, w, sw, mat=0):
    bx, by, bz = base
    tx, ty, tz = top
    ring = [(math.cos(a) * w, math.sin(a) * w) for a in (0.0, math.tau / 3, 2 * math.tau / 3)]
    Bi = [mb.v((bx + x, by + y, bz), swatch_uv(sw, 0.35 + 0.15 * j, 0.05)) for j, (x, y) in enumerate(ring)]
    Ti = [mb.v((tx + x * 0.7, ty + y * 0.7, tz), swatch_uv(sw, 0.35 + 0.15 * j, 0.6)) for j, (x, y) in enumerate(ring)]
    for k in range(3):
        k1 = (k + 1) % 3
        mb.f((Bi[k], Bi[k1], Ti[k1], Ti[k]), mat)


def _octa(mb, c, rx, ry, rz, sw, t_lo=0.35, t_hi=0.95, rot=0.0, mat=0):
    ca, sa = math.cos(rot), math.sin(rot)

    def R(x, y):
        return (c[0] + x * ca - y * sa, c[1] + x * sa + y * ca)
    pts = [R(rx, 0), R(0, ry), R(-rx, 0), R(0, -ry)]
    mid = [mb.v((x, y, c[2]), swatch_uv(sw, 0.3 + 0.13 * k, (t_lo + t_hi) / 2)) for k, (x, y) in enumerate(pts)]
    top = mb.v((c[0], c[1], c[2] + rz), swatch_uv(sw, 0.5, t_hi))
    bot = mb.v((c[0], c[1], c[2] - rz), swatch_uv(sw, 0.5, t_lo))
    for k in range(4):
        k1 = (k + 1) % 4
        mb.f((mid[k], mid[k1], top), mat)
        mb.f((mid[k1], mid[k], bot), mat)


def _star_bloom(mb, c, r, sw, centre_sw, rng, mat=0):
    n = 5
    rim = []
    for k in range(2 * n):
        rr = r if k % 2 == 0 else r * 0.45
        a = math.pi * k / n + rng.uniform(-0.08, 0.08)
        rim.append(mb.v((c[0] + math.cos(a) * rr, c[1] + math.sin(a) * rr, c[2] + (0.0 if k % 2 == 0 else 0.012)),
                        swatch_uv(sw, 0.5 + 0.4 * math.cos(a), 0.7)))
    top = mb.v((c[0], c[1], c[2] + r * 0.3), swatch_uv(sw, 0.5, 0.95))
    bot = mb.v((c[0], c[1], c[2] - r * 0.25), swatch_uv(sw, 0.5, 0.4))
    m = len(rim)
    for k in range(m):
        k1 = (k + 1) % m
        mb.f((rim[k], rim[k1], top), mat)
        mb.f((rim[k1], rim[k], bot), mat)
    _octa(mb, (c[0], c[1], c[2] + r * 0.3), r * 0.3, r * 0.3, r * 0.18, centre_sw, 0.6, 0.98)


def _flower(kind, seed, bloom):
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    for i in range(5):
        mb.grp = i
        a = math.tau * i / 5 + rng.uniform(-0.3, 0.3)
        _blade(mb, (math.cos(a) * 0.04, math.sin(a) * 0.04, -0.02), rng.uniform(0.16, 0.26), 0.06, 0.022,
               (math.cos(a), math.sin(a)), rng.uniform(0.35, 0.7), "G_MID", rng.uniform(0.2, 0.8))
    nb = {"purple": 8, "white": 7, "yellow": 8}[bloom]
    for i in range(nb):
        mb.grp = 10 + i                                   # stalk + bloom of one stem share a part (they move as one)
        a = math.tau * i / nb + rng.uniform(-0.35, 0.35)
        rr = rng.uniform(0.05, 0.2) if i else 0.0
        h = rng.uniform(0.28, 0.46)
        top = (math.cos(a) * rr * 1.25, math.sin(a) * rr * 1.25, h)
        _stalk(mb, (math.cos(a) * rr * 0.4, math.sin(a) * rr * 0.4, -0.02), top, 0.011, "G_DARK")
        if bloom == "purple":
            for k in range(2):
                _octa(mb, (top[0], top[1], top[2] + 0.045 + k * 0.085), 0.042, 0.042, 0.06, "FLOWER_PURPLE",
                      rot=rng.uniform(0, 1.5))
        elif bloom == "white":
            _star_bloom(mb, (top[0], top[1], top[2] + 0.01), rng.uniform(0.07, 0.09), "FLOWER_WHITE", "FLOWER_YELLOW", rng)
        else:
            V, F = _ico_lump(rng, (top[0], top[1], top[2] + 0.04), (0.065, 0.065, 0.05), seed + i, flatten=-1.0, subdiv=1,
                             wobble=0.15)
            mb.add(V, F, 0, [swatch_uv("FLOWER_YELLOW", 0.5 + 0.3 * (p.x - top[0]) / 0.065, 0.45 + 0.5 * (p.z - top[2]) / 0.09)
                             for p in V])
    return _finalize(kind, mb, "vertex", all_smooth=True)


def _vine(kind, seed, length):
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    nseg = max(4, int(round(length / 0.25)))
    ph = rng.uniform(0, math.tau)
    pts = []
    for k in range(nseg + 1):
        t = k / nseg
        z = -length * t
        x = 0.06 * math.sin(ph + t * 5.0) * t
        y = 0.04 + 0.12 * math.sin(math.pi * min(1.0, t * 1.3)) * (1 - 0.4 * t)
        pts.append((x, y, z))
    w = 0.026                                   # round 2026-10-04 (review: a 1 px white line hangs from a vine tip): the stalk was 4 cm wide at the top and 1.7 cm at the tip = under
    rings = []                                  # one pixel at 25 m, so MSAA drew it as a faint pale line; now 5.2 cm -> 3.4 cm (1.9 px at the tip at 25 m)
    for k, (x, y, z) in enumerate(pts):
        ww = w * (1.0 - 0.35 * k / nseg)
        rings.append([mb.v((x + math.cos(a) * ww, y + math.sin(a) * ww, z), swatch_uv("VINE", 0.3 + 0.2 * j, 0.15 + 0.2 * (k % 2)))
                      for j, a in enumerate((0.0, math.tau / 3, 2 * math.tau / 3))])
    for k in range(nseg):
        for j in range(3):
            j1 = (j + 1) % 3
            mb.f((rings[k + 1][j], rings[k + 1][j1], rings[k][j1], rings[k][j]))
    step = 0.22
    n_leaf = max(3, int(length / step))
    for i in range(n_leaf):
        t = (i + 0.6) / n_leaf
        k = min(nseg, int(t * nseg))
        x, y, z = pts[k]
        side = 1 if i % 2 == 0 else -1
        sz = rng.uniform(0.1, 0.15) * (1.15 if i == n_leaf - 1 else 1.0)
        sw = ("G_DARK", "VINE", "G_MID")[i % 3]
        _octa(mb, (x + side * rng.uniform(0.04, 0.08), y + rng.uniform(0.01, 0.06), z + rng.uniform(-0.05, 0.05)),
              sz, sz * 0.75, sz * 0.62, sw, rot=rng.uniform(0, math.pi))
    return _finalize(kind, mb, "vertex", all_smooth=True)


def _leaf(mb, base, dirv, elev, length, width, thick, droop, sw, s, mat=0):
    """Thick agave/yucca leaf: diamond cross-section base, mid ring, tip. dirv horizontal unit, elev radians."""
    dx, dy = dirv
    ce, se = math.cos(elev), math.sin(elev)
    fwd = Vector((dx * ce, dy * ce, se))
    side = Vector((-dy, dx, 0.0))
    up = fwd.cross(side).normalized()
    b = Vector(base)
    sec = [side * width / 2, up * thick / 2, -side * width / 2, -up * thick / 2]
    Bi = [mb.v(b + o, swatch_uv(sw, s + (j - 1.5) * 0.06, 0.05)) for j, o in enumerate(sec)]
    mid = b + fwd * length * 0.5 + Vector((0, 0, -droop * 0.25 * length))
    Mi = [mb.v(mid + o * 0.85, swatch_uv(sw, s + (j - 1.5) * 0.05, 0.55)) for j, o in enumerate(sec)]
    tip = b + fwd * length + Vector((0, 0, -droop * length))
    T = mb.v(tip, swatch_uv(sw, s, 0.97))
    for k in range(4):
        k1 = (k + 1) % 4
        mb.f((Bi[k], Bi[k1], Mi[k1], Mi[k]), mat)
        mb.f((Mi[k], Mi[k1], T), mat)


def _agave(kind, seed, n, length, width, sw, elev=(0.45, 1.25), droop=0.25):
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    for i in range(n):
        mb.grp = i
        a = _GOLDEN * i * 2.0 + rng.uniform(-0.1, 0.1)
        t = i / max(1, n - 1)
        e = elev[0] + (elev[1] - elev[0]) * t
        L = length * (1.0 - 0.35 * t) * rng.uniform(0.9, 1.1)
        _leaf(mb, (math.cos(a) * 0.03, math.sin(a) * 0.03, 0.02 + 0.03 * t), (math.cos(a), math.sin(a)), e, L, width, width * 0.45,
              droop * (1 - t), sw, rng.uniform(0.25, 0.75))
    mb.grp = 60
    _octa(mb, (0, 0, 0.04), 0.07, 0.07, 0.06, sw)
    return _finalize(kind, mb, "vertex", smooth_angle=60.0)


def _trunk(mb, pts, radii, sides=6, sw="TRUNK"):
    prev_grp = mb.grp
    mb.grp = TRUNK_GROUP                                  # a trunk never sways (sway_arrays: weight 0)
    rings = []
    for k, ((x, y, z), r) in enumerate(zip(pts, radii)):
        t = k / max(1, len(pts) - 1)
        rings.append([mb.v((x + math.cos(a) * r, y + math.sin(a) * r, z), swatch_uv(sw, (j + 0.5) / sides, 0.15 + 0.6 * t))
                      for j, a in enumerate([math.tau * j / sides for j in range(sides)])])
    for k in range(len(rings) - 1):
        for j in range(sides):
            j1 = (j + 1) % sides
            mb.f((rings[k][j], rings[k][j1], rings[k + 1][j1], rings[k + 1][j]))
    mb.grp = prev_grp


def _pine(kind, seed):
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    _trunk(mb, [(0, 0, -0.1), (0, 0, 0.7), (0, 0, 1.4)], [0.13, 0.1, 0.07])
    tiers = [(0.45, 1.05, 1.05), (1.1, 0.82, 0.95), (1.7, 0.56, 0.9)]
    n = 10
    for ti, (z0, R, h) in enumerate(tiers):
        mb.grp = ti
        sw = ("G_DEEP", "G_EMERALD", "G_EMERALD")[ti]
        lip, low, upr = [], [], []
        for k in range(n):
            a = math.tau * k / n + ti * 0.3
            rr = R * rng.uniform(0.88, 1.08)
            dz = rng.uniform(-0.06, 0.04)
            low.append(mb.v((math.cos(a) * rr * 0.86, math.sin(a) * rr * 0.86, z0 - 0.1 + dz),
                            swatch_uv(sw, (k + 0.5) / n, 0.08 + 0.05 * math.sin(a * 3.0))))
            lip.append(mb.v((math.cos(a) * rr, math.sin(a) * rr, z0 + dz), swatch_uv(sw, (k + 0.5) / n, 0.3)))
            upr.append(mb.v((math.cos(a) * rr * 0.5, math.sin(a) * rr * 0.5, z0 + h * 0.45 + dz * 0.5), swatch_uv(sw, (k + 0.5) / n, 0.7)))
        apex = mb.v((rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), z0 + h), swatch_uv(sw, 0.5, 0.97))
        mb.f(list(reversed(low)))
        for k in range(n):
            k1 = (k + 1) % n
            mb.f((low[k], low[k1], lip[k1], lip[k]))
            mb.f((lip[k], lip[k1], upr[k1], upr[k]))
            mb.f((upr[k], upr[k1], apex))
    return _finalize(kind, mb, "vertex", smooth_angle=55.0)


def _tree(kind, seed):
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    _trunk(mb, [(0, 0, -0.1), (0.15, 0.05, 0.8), (0.42, 0.1, 1.6), (0.75, 0.12, 2.15)], [0.16, 0.12, 0.09, 0.07])
    lumps = [((0.75, 0.1, 2.45), (0.95, 0.8, 0.62)), ((0.05, 0.05, 2.3), (0.72, 0.65, 0.5)), ((1.35, -0.1, 2.25), (0.6, 0.55, 0.42))]
    zmax = 3.1
    for li, (c, r) in enumerate(lumps):
        mb.grp = li
        V, F = _ico_lump(rng, c, r, seed + li * 2.3, flatten=-0.55)
        sw = ("G_DARK", "G_MID", "G_DARK")[li]
        mb.add(V, F, 0, [swatch_uv(sw, 0.5 + 0.3 * mnoise.noise(p * 1.7), 0.1 + 0.82 * min(1.0, max(0.0, (p.z - 1.6) / (zmax - 1.6)))
                                   + 0.06 * mnoise.noise(p * 3.1 + Vector((5.0, 1.0, 2.0)))) for p in V])
    return _finalize(kind, mb, "vertex", smooth_angle=70.0)


def _plant_tuft_a():
    return _tuft("PLANT_TUFT_A", 31, ("G_MID", "G_LIGHT", "G_MID", "G_LIME"))


def _plant_tuft_b():
    return _tuft("PLANT_TUFT_B", 32, ("G_DARK", "G_MID", "G_EMERALD"), h=(0.26, 0.48))


def _plant_tuft_c():
    return _tuft("PLANT_TUFT_C", 33, ("OLIVE", "STRAW", "OLIVE_KHAKI", "OLIVE_DARK"), h=(0.22, 0.42), w=(0.05, 0.07))


def _tuft_tall(kind, seed, swatches, n_blades, h, w, spread, lean, curve, ts, taper, drop=0.2, prevail=0.5):
    """Fringe tuft: n_blades tall curved blades (_blade_arc) in a tight clump; the centre blades stand taller and straighter, the outer ones
    arch further, and every blade leans toward a mix of 'outward' and the clump's own prevailing direction (a wind-combed clump, not a rosette)."""
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    pa = rng.uniform(0, math.tau)
    for i in range(n_blades):
        mb.grp = i
        a = math.tau * i / n_blades + rng.uniform(-0.3, 0.3)
        rr = rng.uniform(0.2, 1.0) * spread
        base = (math.cos(a) * rr, math.sin(a) * rr, -0.03)
        vx = math.cos(a) * (1.0 - prevail) + math.cos(pa) * prevail + rng.uniform(-0.25, 0.25)
        vy = math.sin(a) * (1.0 - prevail) + math.sin(pa) * prevail + rng.uniform(-0.25, 0.25)
        nv = max(math.hypot(vx, vy), 1e-6)
        k = rr / max(spread, 1e-6)
        _blade_arc(mb, base, rng.uniform(*h) * (1.0 - 0.3 * k), rng.uniform(*w), 0.024, (vx / nv, vy / nv), rng.uniform(*lean) * (0.5 + 0.8 * k),
                   swatches[i % len(swatches)], rng.uniform(0.2, 0.8), ts, curve, taper, drop=drop)
    return _finalize(kind, mb, "vertex", all_smooth=True)


def _tuft_fill(kind, seed, swatches, n_blades=12, h=(0.2, 0.42), w=(0.055, 0.075), spread=0.1, lean=(0.12, 0.6)):
    """Cheap filler tuft: n_blades 3-face cone blades = 3 x n_blades faces / triangles (12 -> 36)."""
    rng = random.Random(seed)
    mb = _MB(("LK_PLANTS",))
    for i in range(n_blades):
        mb.grp = i
        a = math.tau * i / n_blades + rng.uniform(-0.25, 0.25)
        rr = rng.uniform(0.0, spread)
        base = (math.cos(a) * rr, math.sin(a) * rr, -0.03)
        aa = a + rng.uniform(-0.3, 0.3)
        _blade_cone(mb, base, rng.uniform(*h), rng.uniform(*w), (math.cos(aa), math.sin(aa)), rng.uniform(*lean) if rr > 0.02 else rng.uniform(0.04, 0.2),
                    swatches[i % len(swatches)], rng.uniform(0.2, 0.8))
    return _finalize(kind, mb, "vertex", all_smooth=True)


def _plant_tuft_d():          # tall upright green clump, 12 blades x 6 faces = 72 faces / 108 triangles
    return _tuft_tall("PLANT_TUFT_D", 34, ("G_MID", "G_LIGHT", "G_DARK", "G_MID", "G_LIME"), 12, (0.52, 0.82), (0.045, 0.065), 0.09,
                      (0.06, 0.30), 1.8, (0.5,), (0.72,), prevail=0.55)


def _plant_tuft_e():          # arching fountain (dark / mid greens), 9 blades x 9 faces = 81 faces / 135 triangles
    return _tuft_tall("PLANT_TUFT_E", 35, ("G_DARK", "G_MID", "G_EMERALD", "G_LIGHT"), 9, (0.42, 0.70), (0.05, 0.07), 0.06,
                      (0.45, 0.95), 1.25, (0.4, 0.72), (0.85, 0.55), prevail=0.2)


def _plant_tuft_f():          # tall olive / straw clump (Split scrub, Crater shelf), 72 faces / 108 triangles
    return _tuft_tall("PLANT_TUFT_F", 36, ("OLIVE", "STRAW", "OLIVE_KHAKI", "OLIVE_DARK"), 12, (0.45, 0.78), (0.04, 0.06), 0.09,
                      (0.10, 0.42), 1.7, (0.5,), (0.7,), prevail=0.5)


def _plant_tuft_s():          # cheap green filler, 36 faces / 36 triangles
    return _tuft_fill("PLANT_TUFT_S", 37, ("G_MID", "G_LIGHT", "G_DARK", "G_LIME"))


def _plant_tuft_t():          # cheap olive / straw filler, 36 faces / 36 triangles
    return _tuft_fill("PLANT_TUFT_T", 38, ("OLIVE", "STRAW", "OLIVE_KHAKI", "OLIVE_DARK"), h=(0.18, 0.4))


def _plant_shrub_a():
    # round 3: low overlapping lumps (no top ball), olive-green swatches (the stills' scrub sits at hue 46-62, the brightest old swatch G_LIGHT popped as lime, review 3)
    return _shrub("PLANT_SHRUB_A", 41, [((0, 0, 0.36), (0.68, 0.62, 0.42)), ((0.5, 0.2, 0.24), (0.44, 0.4, 0.28)),
                                        ((-0.44, 0.28, 0.26), (0.44, 0.4, 0.3)), ((0.06, -0.46, 0.24), (0.42, 0.38, 0.28))],
                  ("OLIVE", "OLIVE_KHAKI", "OLIVE_DARK", "OLIVE"), fringe=0, wobble=0.2, mottle=0.1)


def _plant_shrub_b():
    # round 3: taller dark olive clump, the top lump sunk into the body (no pyramid of five balls), no emerald / light-green swatch
    return _shrub("PLANT_SHRUB_B", 42, [((0, 0, 0.52), (0.56, 0.52, 0.56)), ((0.42, 0.2, 0.34), (0.4, 0.38, 0.38)),
                                        ((-0.38, 0.24, 0.38), (0.4, 0.36, 0.4)), ((0.1, 0.06, 0.86), (0.32, 0.3, 0.34))],
                  ("OLIVE_DARK", "OLIVE", "OLIVE_DARK", "OLIVE"), fringe=0, wobble=0.22, mottle=0.1)


def _plant_shrub_c():
    # round 3: the top ball sunk into the mound (was a third storey at z 0.55) and a fifth low lump on the fourth side: one irregular olive mound with straw blades, not a snowman
    return _shrub("PLANT_SHRUB_C", 43, [((0, 0, 0.3), (0.74, 0.64, 0.38)), ((0.62, 0.1, 0.2), (0.46, 0.42, 0.26)),
                                        ((-0.56, 0.22, 0.2), (0.48, 0.4, 0.26)), ((0.1, 0.04, 0.4), (0.46, 0.4, 0.24)),
                                        ((-0.1, -0.52, 0.18), (0.44, 0.36, 0.24))],
                  ("OLIVE", "OLIVE_KHAKI", "OLIVE_DARK", "OLIVE", "OLIVE_DARK"), blades=8, blade_sw="STRAW", fringe=0, wobble=0.2, mottle=0.1)


ROCK_BUILDERS = {
    "ROCK_CRAG_A": _rock_crag_a, "ROCK_CRAG_B": _rock_crag_b,
    "ROCK_BOULDER_A": _rock_boulder_a, "ROCK_BOULDER_B": _rock_boulder_b, "ROCK_SLAB_A": _rock_slab_a,
    "ROCK_BLOCK_A": _rock_block_a, "ROCK_CAIRN_A": _rock_cairn_a, "ROCK_STONE_A": _rock_stone_a,
    "ROCK_STONE_B": _rock_stone_b, "ROCK_LEDGE_A": _rock_ledge_a, "ROCK_OUTCROP_A": _rock_outcrop_a,
}
for _k in SPIRE_KINDS:
    ROCK_BUILDERS[_k] = _spire_builder(_k)
BUILDERS = dict(ROCK_BUILDERS)
BUILDERS.update({
    "ROCK_BASALT_A": _basalt_a, "ROCK_BASALT_B": _basalt_b, "ROCK_BASALT_C": _basalt_c, "ROCK_BASALT_D": _basalt_d,
    "ROCK_BASALT_E": _basalt_e, "ROCK_BASALT_F": _basalt_f,
    "ROCK_WALL_BASALT_S": _tower_s, "ROCK_WALL_BASALT_M": _tower_m, "ROCK_WALL_BASALT_L": _tower_l,
    "ROCK_WALL_BASALT_A": _wall_a, "ROCK_WALL_BASALT_B": _wall_b, "ROCK_WALL_BASALT_C": _wall_c,
    "DRESS_BLOCK_A": _dress_block_a, "DRESS_BLOCK_B": _dress_block_b,
    "PLANT_TUFT_A": _plant_tuft_a, "PLANT_TUFT_B": _plant_tuft_b, "PLANT_TUFT_C": _plant_tuft_c,
    "PLANT_TUFT_D": _plant_tuft_d, "PLANT_TUFT_E": _plant_tuft_e, "PLANT_TUFT_F": _plant_tuft_f,
    "PLANT_TUFT_S": _plant_tuft_s, "PLANT_TUFT_T": _plant_tuft_t,
    "PLANT_SHRUB_A": _plant_shrub_a, "PLANT_SHRUB_B": _plant_shrub_b, "PLANT_SHRUB_C": _plant_shrub_c,
    "PLANT_FLOWER_PURPLE": lambda: _flower("PLANT_FLOWER_PURPLE", 51, "purple"),
    "PLANT_FLOWER_WHITE": lambda: _flower("PLANT_FLOWER_WHITE", 52, "white"),
    "PLANT_FLOWER_YELLOW": lambda: _flower("PLANT_FLOWER_YELLOW", 53, "yellow"),
    "PLANT_VINE_S": lambda: _vine("PLANT_VINE_S", 61, 1.0),
    "PLANT_VINE_M": lambda: _vine("PLANT_VINE_M", 62, 2.0),
    "PLANT_VINE_L": lambda: _vine("PLANT_VINE_L", 63, 3.5),
    "PLANT_AGAVE_A": lambda: _agave("PLANT_AGAVE_A", 71, 13, 0.62, 0.11, "AGAVE"),
    "PLANT_AGAVE_B": lambda: _agave("PLANT_AGAVE_B", 72, 16, 0.85, 0.075, "G_DARK", elev=(0.55, 1.35), droop=0.18),
    "PLANT_PINE_A": lambda: _pine("PLANT_PINE_A", 81),
    "PLANT_TREE_A": lambda: _tree("PLANT_TREE_A", 82),
    "DRESS_SMOKE_CARD": lambda: _smoke_card(),
})
PLANT_KINDS = tuple(k for k in BUILDERS if k.startswith("PLANT_"))
TUFT_KINDS = ("PLANT_TUFT_A", "PLANT_TUFT_B", "PLANT_TUFT_C")                  # the three original library tufts (78 faces each, unchanged)
TUFT_KINDS_TALL = ("PLANT_TUFT_D", "PLANT_TUFT_E", "PLANT_TUFT_F")             # v2 2026-10-05: tall curved fringe tufts (<= 120 faces)
TUFT_KINDS_FILL = ("PLANT_TUFT_S", "PLANT_TUFT_T")                             # v2 2026-10-05: cheap filler tufts (<= 40 faces)
TUFT_KINDS_ALL = TUFT_KINDS + TUFT_KINDS_TALL + TUFT_KINDS_FILL
VINE_KINDS = ("PLANT_VINE_S", "PLANT_VINE_M", "PLANT_VINE_L")
VINE_LENGTH = {"PLANT_VINE_S": 1.0, "PLANT_VINE_M": 2.0, "PLANT_VINE_L": 3.5}
TALL_KINDS = ("ROCK_", "PLANT_SHRUB", "PLANT_TREE", "PLANT_PINE", "PLANT_AGAVE_B")


# ----------------------------------------------------------------------------- library access
def _variant(kind):
    """'<BASE>_WET' / '<BASE>_BASALT' twins of a rock (same geometry, other material, box UV for that tile)."""
    for suf, mat in (("_WET", "LK_ROCK_WET"), ("_BASALT", "LK_BASALT")):
        if kind.endswith(suf) and kind[:-len(suf)] in ROCK_BUILDERS:
            src = library_object(kind[:-len(suf)]).data
            me = src.copy()
            me.name = kind
            me["lk_kind"] = kind
            for i, m in enumerate(me.materials):
                if m is not None and (m.name == "LK_ROCK" or (mat == "LK_BASALT" and m.name == "LK_ROUGH")):
                    me.materials[i] = lk_material(mat)
            tiles = [(TILE.get(m.name) or 4.0) if m is not None else 4.0 for m in me.materials]
            uv = box_uv_expected(me, tiles)
            lay = me.uv_layers.active
            plant_slots = [i for i, m in enumerate(me.materials) if m is not None and m.name == "LK_PLANTS"]
            if plant_slots:                                  # a shrub crown (LK_PLANTS faces): its atlas-swatch UVs are not box-projected, keep them
                old_uv = np.empty(len(me.loops) * 2)
                try:
                    lay.uv.foreach_get("vector", old_uv)
                except Exception:
                    lay.data.foreach_get("uv", old_uv)
                _v, _c, pol, _n, mi = _loop_arrays(me)
                sel = np.isin(mi[pol], plant_slots)
                uv[sel] = old_uv.reshape(-1, 2)[sel]
            try:
                lay.uv.foreach_set("vector", uv.ravel())
            except Exception:
                lay.data.foreach_set("uv", uv.ravel())
            ob = bpy.data.objects.new(kind, me)
            _lib_collection().objects.link(ob)
            ob.hide_viewport = ob.hide_render = True
            return ob
    return None


def library_object(kind):
    """The hidden library object (and mesh) of `kind`, built on first use."""
    ob = bpy.data.objects.get(kind)
    if ob is not None and ob.type == 'MESH' and ob.data is not None and ob.data.get("lk_kind") == kind:
        return ob
    if kind in BUILDERS:
        return BUILDERS[kind]()
    v = _variant(kind)
    if v is not None:
        return v
    raise KeyError(f"unknown prop kind {kind}")


def ensure_library(kinds=None):
    """Build every library mesh (or the listed kinds). Also writes Plants_C.png first. Returns {kind: obj}."""
    write_plants_atlas()
    out = {}
    for k in (kinds or BUILDERS.keys()):
        out[k] = library_object(k)
    return out


def library_report():
    out = {}
    for ob in _lib_collection().objects:
        me = ob.data
        if me is None or not me.get("lk_kind"):
            continue
        me.calc_loop_triangles()
        out[ob.name] = dict(verts=len(me.vertices), faces=len(me.polygons), tris=len(me.loop_triangles),
                            mats=[m.name for m in me.materials if m])
    return out


def lib_bbox(kind):
    me = library_object(kind).data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    return Vector(co.min(0)), Vector(co.max(0))


# ----------------------------------------------------------------------------- shape analysis (the gates reuse these)
SHAPE_TH = dict(
    min_verts=60, min_faces=100,          # welded vertices / polygons of a library rock
    facet_deg=14.0, facet_share=0.02, min_facets=6,   # >= 6 clearly distinct planar facet orientations (clusters >= 2 % of the area)
    blob_radius_cv=0.10,                  # normalised radius spread below this = a sphere / icosphere in disguise
    blob_edge_cv=0.20, blob_area_cv=0.45, blob_tri_radius_cv=0.18,   # a regular triangle tessellation of a roundish solid
    taper_max=0.45, cap_max=0.25, spire_facets=6, fill_max=0.62, band_rise_max=1.12, spire_slices=10,
)


def _np_mesh(me):
    """(co (n, 3), tris (m, 3), tri_poly (m,)) of a mesh datablock (loop triangles)."""
    me.calc_loop_triangles()
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    nt = len(me.loop_triangles)
    idx = np.empty(nt * 3, np.int32)
    me.loop_triangles.foreach_get("vertices", idx)
    tp = np.empty(nt, np.int32)
    me.loop_triangles.foreach_get("polygon_index", tp)
    return co.reshape(-1, 3), idx.reshape(-1, 3), tp


def welded_vertex_count(me, tol=1e-4):
    """Vertices after merging coincident ones (flat-shaded / split meshes count as their real vertex count)."""
    co = _np_mesh(me)[0]
    if len(co) == 0:
        return 0
    return int(len(np.unique(np.round(co / tol).astype(np.int64), axis=0)))


def facet_clusters(normals, areas, deg=None, share=None):
    """Greedy area-weighted clustering of face normals: returns [(unit normal, area share)] for clusters holding >= share
    of the total area (cluster = all normals within `deg` degrees of its seed, seeds taken largest area first)."""
    deg = SHAPE_TH["facet_deg"] if deg is None else deg
    share = SHAPE_TH["facet_share"] if share is None else share
    n = np.asarray(normals, float)
    a = np.asarray(areas, float)
    tot = float(a.sum()) or 1.0
    order = np.argsort(-a)
    left = np.ones(len(n), bool)
    cos_t = math.cos(math.radians(deg))
    out = []
    for i in order:
        if not left[i]:
            continue
        sel = left & (n @ n[i] >= cos_t)
        sh = float(a[sel].sum()) / tot
        left &= ~sel
        if sh >= share:
            v = (n[sel] * a[sel][:, None]).sum(0)
            ln = float(np.linalg.norm(v)) or 1.0
            out.append((v / ln, sh))
    return out


def _poly_arrays(me):
    npoly = len(me.polygons)
    nrm = np.empty(npoly * 3)
    me.polygons.foreach_get("normal", nrm)
    area = np.empty(npoly)
    me.polygons.foreach_get("area", area)
    lt = np.empty(npoly, np.int32)
    me.polygons.foreach_get("loop_total", lt)
    return nrm.reshape(-1, 3), area, lt


def rock_shape_report(me, th=None):
    """Numbers that tell a real faceted rock from a blob. Keys: verts (welded), faces, tri_share, facets (clusters >= 2 % of
    the area, 14 degree cones), radius_cv (spread of the bbox-normalised distance from the centre: 0 = a sphere, whatever its
    stretch), edge_cv, area_cv, blob (bool), ok (bool) and why (list of failed tests). A rock passes when it has >= 60 welded
    vertices, >= 100 faces, >= 6 facet orientations and is NOT a blob: radius_cv < 0.10 (sphere / icosphere / UV sphere,
    split or not, stretched or not) or (all triangles and edge_cv < 0.20 and area_cv < 0.45 and radius_cv < 0.18) (a regular
    tessellation of a roundish solid, e.g. a subdivided or noised icosahedron)."""
    th = th or SHAPE_TH
    co, tris, _tp = _np_mesh(me)
    nrm, area, lt = _poly_arrays(me)
    nv = welded_vertex_count(me)
    nf = len(me.polygons)
    rep = dict(verts=nv, faces=nf, tri_share=float((lt == 3).mean()) if nf else 0.0)
    if len(co) == 0 or nf == 0:
        rep.update(facets=0, radius_cv=0.0, edge_cv=0.0, area_cv=0.0, blob=True, ok=False, why=["empty"])
        return rep
    cl = facet_clusters(nrm, area, th["facet_deg"], th["facet_share"])
    mn, mx = co.min(0), co.max(0)
    ctr, half = (mn + mx) / 2, np.maximum((mx - mn) / 2, 1e-9)
    r = np.linalg.norm((co - ctr) / half, axis=1)
    rcv = float(r.std() / max(r.mean(), 1e-9))
    ed = np.empty(len(me.edges) * 2, np.int32)
    me.edges.foreach_get("vertices", ed)
    ed = ed.reshape(-1, 2)
    el = np.linalg.norm(co[ed[:, 0]] - co[ed[:, 1]], axis=1)
    ecv = float(el.std() / max(el.mean(), 1e-9)) if len(el) else 0.0
    acv = float(area.std() / max(area.mean(), 1e-9))
    all_tri = rep["tri_share"] > 0.98
    blob = rcv < th["blob_radius_cv"] or (all_tri and ecv < th["blob_edge_cv"] and acv < th["blob_area_cv"] and rcv < th["blob_tri_radius_cv"])
    why = []
    if nv < th["min_verts"]:
        why.append(f"{nv} welded vertices < {th['min_verts']}")
    if nf < th["min_faces"]:
        why.append(f"{nf} faces < {th['min_faces']}")
    if len(cl) < th["min_facets"]:
        why.append(f"{len(cl)} facet orientations < {th['min_facets']}")
    if blob:
        why.append(f"blob (radius_cv {rcv:.3f}, edge_cv {ecv:.3f}, area_cv {acv:.3f}, all triangles {all_tri})")
    rep.update(facets=len(cl), radius_cv=rcv, edge_cv=ecv, area_cv=acv, blob=bool(blob), ok=not why, why=why)
    return rep


def mesh_section(co, tris, z):
    """Points where the horizontal plane at z cuts the triangle edges (k, 3)."""
    a, b, c = co[tris[:, 0]], co[tris[:, 1]], co[tris[:, 2]]
    pts = []
    for p, q in ((a, b), (b, c), (c, a)):
        dz = q[:, 2] - p[:, 2]
        cross = ((p[:, 2] - z) * (q[:, 2] - z) < 0)
        t = (z - p[cross, 2]) / dz[cross]
        pts.append(p[cross] + (q[cross] - p[cross]) * t[:, None])
    return np.vstack(pts) if pts else np.zeros((0, 3))


def section_width(co, tris, z):
    """Diameter (largest distance between two points) of the horizontal section of the mesh at z; 0 when it does not reach z."""
    pts = mesh_section(co, tris, z)[:, :2]
    if len(pts) < 2:
        return 0.0
    d = pts[:, None, :] - pts[None, :, :]
    return float(np.sqrt((d ** 2).sum(-1)).max())


def _poly_components(me, mask):
    """Connected components (shared edges) of the polygons selected by mask -> list of index arrays."""
    sel = set(np.nonzero(mask)[0].tolist())
    edge_faces = {}
    for p in me.polygons:
        if p.index not in sel:
            continue
        vs = list(p.vertices)
        for i in range(len(vs)):
            k = tuple(sorted((vs[i], vs[(i + 1) % len(vs)])))
            edge_faces.setdefault(k, []).append(p.index)
    adj = {i: set() for i in sel}
    for fs in edge_faces.values():
        for i in fs:
            adj[i].update(f for f in fs if f != i)
    seen, comps = set(), []
    for s in sel:
        if s in seen:
            continue
        stack, comp = [s], []
        while stack:
            q = stack.pop()
            if q in seen:
                continue
            seen.add(q)
            comp.append(q)
            stack.extend(adj[q] - seen)
        comps.append(np.array(comp))
    return comps


def silhouette_fill(co, tris, azimuth_deg, z_min=None, res=96):
    """Bounding-box fill ratio of the silhouette seen horizontally from `azimuth_deg`: covered area / (width x height) of the
    silhouette's own bounding box (1.0 = a rectangle). z_min cuts the mesh (e.g. 0 = only what stands above the water)."""
    a = math.radians(azimuth_deg)
    u = co[:, 0] * math.cos(a) + co[:, 1] * math.sin(a)
    v = co[:, 2]
    if z_min is not None:
        v = np.maximum(v, z_min)
        keep = (co[tris[:, 0], 2] > z_min) | (co[tris[:, 1], 2] > z_min) | (co[tris[:, 2], 2] > z_min)
        tris = tris[keep]
    if len(tris) == 0:
        return 0.0
    P = np.stack([u, v], 1)
    pts = P[tris].reshape(-1, 2)
    u0, u1, v0, v1 = pts[:, 0].min(), pts[:, 0].max(), pts[:, 1].min(), pts[:, 1].max()
    W, Hh = max(u1 - u0, 1e-6), max(v1 - v0, 1e-6)
    nx = res
    ny = max(8, int(round(res * Hh / W)))
    xs = u0 + (np.arange(nx) + 0.5) * W / nx
    ys = v0 + (np.arange(ny) + 0.5) * Hh / ny
    cov = np.zeros((ny, nx), bool)
    for t in tris:
        A, B, C = P[t[0]], P[t[1]], P[t[2]]
        i0 = max(0, int(math.floor((min(A[0], B[0], C[0]) - u0) / W * nx)) - 1)
        i1 = min(nx, int(math.ceil((max(A[0], B[0], C[0]) - u0) / W * nx)) + 1)
        j0 = max(0, int(math.floor((min(A[1], B[1], C[1]) - v0) / Hh * ny)) - 1)
        j1 = min(ny, int(math.ceil((max(A[1], B[1], C[1]) - v0) / Hh * ny)) + 1)
        if i1 <= i0 or j1 <= j0:
            continue
        X, Y = np.meshgrid(xs[i0:i1], ys[j0:j1])
        d = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
        if abs(d) < 1e-12:
            continue
        l1 = ((B[1] - C[1]) * (X - C[0]) + (C[0] - B[0]) * (Y - C[1])) / d
        l2 = ((C[1] - A[1]) * (X - C[0]) + (A[0] - C[0]) * (Y - C[1])) / d
        l3 = 1.0 - l1 - l2
        inside = (l1 >= -1e-9) & (l2 >= -1e-9) & (l3 >= -1e-9)
        cov[j0:j1, i0:i1] |= inside
    return float(cov.sum()) / float(nx * ny)


def seastack_report(me, th=None, azimuths=(0.0, 45.0, 90.0, 135.0)):
    """Gate numbers of a tapering spire / sea stack mesh: width at 10 % .. 90 % of the height (diameters of the horizontal
    sections), taper = w(90 %) / w(10 %) (<= 0.45), the largest rise between two neighbouring bands (<= 1.12: ledges and
    overhangs allowed, a stepped tower is not), the largest connected horizontal-ish face group (normal.z >= 0.9) as a share
    of the base cross-section area (<= 0.25: no flat cap / lid), facet orientations (>= 6), silhouette bounding-box fill from
    4 azimuths measured on the part above the waterline (<= 0.62: not a rectangle), the slices read back from the point
    attribute lk_slice (>= 10 visible slices, every outline different, 5-9 corners each) and the face count. ok + why."""
    th = th or SHAPE_TH
    co, tris, _tp = _np_mesh(me)
    nrm, area, lt = _poly_arrays(me)
    zmin, zmax = float(co[:, 2].min()), float(co[:, 2].max())
    zv0 = max(0.0, zmin)                               # visible height starts at the waterline (z 0) when the stack stands in water
    H = zmax - zmin
    fr = [0.1 * k for k in range(1, 10)]
    w = [section_width(co, tris, zmin + f * H) for f in fr]
    taper = w[-1] / max(w[0], 1e-9)
    rise = max((w[i + 1] / max(w[i], 1e-9) for i in range(len(w) - 1)), default=1.0)
    base_area = 0.25 * math.pi * w[0] ** 2 * 0.8       # ellipse-ish cross-section at 10 %: 0.8 = typical axis ratio of our slices
    pa = _section_area(co, tris, zmin + 0.1 * H)
    base_area = pa if pa > 0 else base_area
    horiz = nrm[:, 2] >= 0.9
    comps = _poly_components(me, horiz)
    cap = max((float(area[c].sum()) for c in comps), default=0.0)
    cap_share = cap / max(base_area, 1e-9)
    cl = facet_clusters(nrm, area, th["facet_deg"], th["facet_share"])
    fills = [silhouette_fill(co, tris, a, z_min=0.0 if zmin < -0.5 else None) for a in azimuths]
    sl = me.attributes.get("lk_slice")
    n_sl, distinct, corner_ok = 0, True, True
    if sl is not None:
        v = np.empty(len(me.vertices), np.int32)
        sl.data.foreach_get("value", v)
        outlines = []
        for s in sorted(set(v.tolist())):
            if s < 0:
                continue
            q = co[v == s]
            if q[:, 2].mean() < 0.0:
                continue
            n_sl += 1
            if not (5 <= len(q) <= 9):
                corner_ok = False
            c = q[:, :2] - q[:, :2].mean(0)
            key = tuple(np.round(np.sort(np.hypot(c[:, 0], c[:, 1])), 2).tolist())
            outlines.append(key + (len(q),))
        distinct = len(set(outlines)) == len(outlines)
    why = []
    if taper > th["taper_max"]:
        why.append(f"taper {taper:.2f} > {th['taper_max']}")
    if rise > th["band_rise_max"]:
        why.append(f"band widens by {rise:.2f}x > {th['band_rise_max']} (stepped / boxy)")
    if cap_share > th["cap_max"]:
        why.append(f"flat cap {cap_share:.0%} of the base > {th['cap_max']:.0%}")
    if len(cl) < th["spire_facets"]:
        why.append(f"{len(cl)} facet orientations < {th['spire_facets']}")
    if max(fills) > th["fill_max"]:
        why.append(f"silhouette fill {max(fills):.2f} > {th['fill_max']} (a rectangle-ish outline)")
    if sl is not None:
        if n_sl < th["spire_slices"]:
            why.append(f"{n_sl} slices < {th['spire_slices']}")
        if not distinct:
            why.append("two slices share one outline")
        if not corner_ok:
            why.append("a slice has < 5 or > 9 corners")
    return dict(height=H, widths=w, taper=taper, band_rise=rise, cap_share=cap_share, facets=len(cl), fills=fills,
                slices=n_sl, distinct=distinct, faces=len(me.polygons), ok=not why, why=why)


def _section_area(co, tris, z):
    """Area of the horizontal section at z (convex-hull area of the section points: fine for the near-convex spires)."""
    pts = mesh_section(co, tris, z)[:, :2]
    if len(pts) < 3:
        return 0.0
    c = pts.mean(0)
    ang = np.arctan2(pts[:, 1] - c[1], pts[:, 0] - c[0])
    q = pts[np.argsort(ang)]
    x, y = q[:, 0], q[:, 1]
    return float(0.5 * abs(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1))))


# ----------------------------------------------------------------------------- scale limits (no stretching of shared meshes)
# every library instance: each scale component in [lo, hi], max / min <= aniso. The contract is 0.5 .. 2.0 / 2.0; the limits keep a 1e-3 margin because the
# FBX float round trip turned a clamp at exactly 0.5 into 0.49999997 (hole 8 / 10 builders, LIB_BUGS: NO_ROCK_STRETCH_2X@fbx failed on a piece "at the limit")
SCALE_LIMITS = dict(lo=0.501, hi=1.999, aniso=1.998)


def clamp_scale(sc, lo=None, hi=None, aniso=None):
    """Scale (sx, sy, sz) pulled into the SCALE_LIMITS: each component into [0.5, 2.0], then the small ones raised so that
    max / min <= 2. A shared library mesh keeps its box UV and its shape only inside these limits."""
    lo = SCALE_LIMITS["lo"] if lo is None else lo
    hi = SCALE_LIMITS["hi"] if hi is None else hi
    an = SCALE_LIMITS["aniso"] if aniso is None else aniso
    c = [min(max(float(v), lo), hi) for v in sc]
    m = max(c)
    return tuple(max(v, m / an) for v in c)


# ----------------------------------------------------------------------------- placing instances
_COUNTERS = {}


def _collection_for(kind):
    if kind.startswith("ROCK_"):
        name, parent = "ROCKS", "ENVIRONMENT"
    elif kind.startswith("PLANT_"):
        name, parent = "PLANTS", "ENVIRONMENT"
    elif kind.startswith(("DRESS_ARCH", "DRESS_RUIN", "DRESS_BLOCK")):
        return P.get_collection("STRUCTURES")
    else:
        return P.get_collection("ENVIRONMENT")
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        P.get_collection(parent).children.link(col)
    return col


def _new_name(base, fmt="{}.{:03d}"):
    n = _COUNTERS.get(base, 0)
    while bpy.data.objects.get(fmt.format(base, n)) is not None:
        n += 1
    _COUNTERS[base] = n + 1
    return fmt.format(base, n)


def _parent(D, ob):
    if D is not None:
        ob.parent = P.get_root()


PLANT_MIN_Z = 0.3       # a PLANT_ prop below this height is standing in the sea (the water is at z = 0, the land at play height 6)


def place(D, kind, loc, yaw=0.0, scale=1.0, tilt=(0.0, 0.0), name=None, clamp=True, allow_low=False):
    """One linked duplicate of library mesh `kind` at loc (x, y, z) metres (origin = footprint centre at ground contact),
    yaw radians about Z, tilt (rx, ry) radians, scale float or (sx, sy, sz). With clamp=True (default) the scale is pulled
    into the SCALE_LIMITS (each component 0.5-2.0, max/min <= 2: clamp_scale) and the object gets the custom property
    lk_clamped = 1 when that changed it, so no library instance is ever stretched. A PLANT_ kind below z = 0.3 m (the sea) is
    REFUSED (ValueError) unless allow_low=True (test scenes on a reference ground): place() has no land mask, scatter() has.
    Returns the object."""
    if kind.startswith("PLANT_") and not allow_low and float(Vector(loc).z) < PLANT_MIN_Z:
        raise ValueError(f"place({kind}) at z={float(Vector(loc).z):.2f} m is below {PLANT_MIN_Z} m: plants never stand on the sea "
                         f"(use scatter(), which masks the water, or allow_low=True in a test scene)")
    lib = library_object(kind)
    ob = bpy.data.objects.new(name or _new_name(kind), lib.data)
    _collection_for(kind).objects.link(ob)
    _parent(D, ob)
    ob.location = Vector(loc)
    ob.rotation_euler = (tilt[0], tilt[1], yaw)
    if isinstance(scale, (int, float)):
        scale = (scale, scale, scale)
    scale = tuple(float(v) for v in scale)
    if clamp:
        c = clamp_scale(scale)
        if max(abs(c[i] - scale[i]) for i in range(3)) > 1e-6:
            ob["lk_clamped"] = 1
        scale = c
    ob.scale = scale
    return ob


def place_fit(D, kind, center, dims, R=None, name=None):
    """Instance whose (unrotated) library bbox is scaled to dims (x, y, z metres) and centred on `center` after rotation R
    (3x3 Matrix or None). The scale is clamped into the SCALE_LIMITS (0.5-2.0, max/min <= 2), so the instance only
    APPROXIMATES dims when they are far from the library size (use fit_plan / several instances for big boxes)."""
    mn, mx = lib_bbox(kind)
    size, ctr = mx - mn, (mn + mx) * 0.5
    sc = clamp_scale((dims[0] / max(size.x, 1e-6), dims[1] / max(size.y, 1e-6), dims[2] / max(size.z, 1e-6)))
    sc = Vector(sc)
    R = R if R is not None else Matrix.Identity(3)
    loc = Vector(center) - R @ Vector((ctr.x * sc.x, ctr.y * sc.y, ctr.z * sc.z))
    ob = place(D, kind, loc, 0.0, tuple(sc), name=name)
    ob.rotation_euler = R.to_euler()
    return ob


# ----------------------------------------------------------------------------- masks + regions
def _xy(X, Y):
    return np.asarray(X, float), np.asarray(Y, float)


def mask_land(D):
    return lambda X, Y: P.lie_codes_m(D, X, Y) != P.LIE_WATER


def mask_water(D):
    return lambda X, Y: P.lie_codes_m(D, X, Y) == P.LIE_WATER


def mask_lies(D, names):
    codes = [P.LIE_NAMES.index(n) for n in names]
    return lambda X, Y: np.isin(P.lie_codes_m(D, X, Y), codes)


def mask_rough(D):
    """Rough or out-of-bounds grass (the scrub / rough lie), never water/fairway/green/tee/bunker."""
    return mask_lies(D, ("Rough", "OutOfBounds"))


def mask_off_play(D, margin_m=0.5):
    """True where no play-surface MESH is within margin_m: beyond fairway + first cut (SCENERY firstcut_yd, used as metres
    exactly like postcard_lib), beyond green + apron (GREEN_RADIUS + apron_yd), outside the tee box (half diagonal of
    tee_box_m) and outside every bunker lip (1.22 x the ellipse)."""
    h = D.hole
    sc = D.scenery
    fc_m = float(sc.get("firstcut_yd", 3.0))
    ap_yd = float(sc.get("apron_yd", 3.0))
    tw, td = sc.get("tee_box_m", (9.0, 7.0))
    tee_r = 0.5 * math.hypot(tw, td) + 0.4

    def f(X, Y):
        X, Y = _xy(X, Y)
        codes, off = P._lie_all(D, X, Y)
        ok = off * YD > (h.fw / 2 + fc_m / YD) * YD + margin_m
        ok &= np.hypot(X / YD - h.pin[0], Y / YD - h.pin[1]) * YD > (h.gr + ap_yd) * YD + margin_m
        ok &= np.hypot(X - h.tee[0] * YD, Y - h.tee[1] * YD) > tee_r + margin_m
        for kind, hx, hd, w, l, _tag in h.hazards:
            if kind != "bunker":
                continue
            a = 1.22 * w / 2 * YD + margin_m
            b = 1.22 * l / 2 * YD + margin_m
            ok &= ((X - hx * YD) / a) ** 2 + ((Y - hd * YD) / b) ** 2 > 1.0
        ok &= codes != P.LIE_BUNKER
        return ok
    return f


def mask_pin_clear(D, r_m=6.0):
    px, py = D.hole.pin[0] * YD, D.hole.pin[1] * YD
    return lambda X, Y: np.hypot(np.asarray(X) - px, np.asarray(Y) - py) >= r_m


def mask_corridor(D, half_width_m):
    """Outside a corridor of half_width_m metres around the whole tee-to-pin centerline polyline (land AND water legs):
    the ball camera looks along it, so tall props stay out."""
    def f(X, Y):
        _c, off = P._lie_all(D, X, Y)
        return off * YD > half_width_m
    return f


def mask_band(D, lo_m, hi_m):
    """Land lo_m..hi_m metres inside the cliff edge (terrain boundary loops; needs build_terrain)."""
    def f(X, Y):
        sd = P.signed_dist_m(D, X, Y)
        return (sd <= -lo_m) & (sd >= -hi_m)
    return f


def mask_sea(D, lo_m, hi_m):
    """Water (sea, or lava on the crater) lo_m..hi_m metres off the cliff edge."""
    def f(X, Y):
        sd = P.signed_dist_m(D, X, Y)
        return (sd >= lo_m) & (sd <= hi_m)
    return f


def mask_not_near(points, r_m):
    pts = np.asarray(points, float).reshape(-1, 2)

    def f(X, Y):
        X, Y = _xy(X, Y)
        ok = np.ones(X.shape, bool)
        for px, py in pts:
            ok &= np.hypot(X - px, Y - py) >= r_m
        return ok
    return f


def region_rect(x0, y0, x1, y1):
    def f(rng, n):
        return (np.array([rng.uniform(x0, x1) for _ in range(n)]), np.array([rng.uniform(y0, y1) for _ in range(n)]), None)
    return f


def region_disc(cx, cy, r):
    def f(rng, n):
        a = np.array([rng.uniform(0, math.tau) for _ in range(n)])
        rr = r * np.sqrt(np.array([rng.random() for _ in range(n)]))
        return cx + rr * np.cos(a), cy + rr * np.sin(a), None
    return f


def region_band(D, lo_m, hi_m, side="land"):
    """Candidates within the land (or sea) band lo..hi metres from the cliff edge (rejection-sampled in the shore bbox)."""
    m = mask_band(D, lo_m, hi_m) if side == "land" else mask_sea(D, lo_m, hi_m)
    x0, y0, x1, y1 = P._shore_bbox_m(D, (hi_m + 2.0) if side != "land" else 2.0)

    def f(rng, n):
        X = np.array([rng.uniform(x0, x1) for _ in range(n)])
        Y = np.array([rng.uniform(y0, y1) for _ in range(n)])
        ok = m(X, Y)
        return X[ok], Y[ok], None
    return f


def region_polyline(points, half_width_m, z=None):
    """Candidates within half_width_m of a polyline [(x, y), ...] (uniform along the length)."""
    pts = np.asarray(points, float)
    seg = np.hypot(*(pts[1:] - pts[:-1]).T)
    cum = np.concatenate([[0], np.cumsum(seg)])

    def f(rng, n):
        s = np.array([rng.uniform(0, cum[-1]) for _ in range(n)])
        off = np.array([rng.uniform(-half_width_m, half_width_m) for _ in range(n)])
        i = np.clip(np.searchsorted(cum, s) - 1, 0, len(seg) - 1)
        t = (s - cum[i]) / np.maximum(seg[i], 1e-9)
        a, b = pts[i], pts[i + 1]
        d = (b - a) / np.maximum(seg[i], 1e-9)[:, None]
        p = a + (b - a) * t[:, None] + np.stack([-d[:, 1], d[:, 0]], 1) * off[:, None]
        return p[:, 0], p[:, 1], (None if z is None else np.full(n, float(z)))
    return f


def region_on_objects(objs, min_up=0.8, inset_m=0.15):
    """Candidates on the upward-facing faces (normal.z >= min_up) of the given objects (world space, area weighted),
    for plants on ledges, sea-stack tops, arch tops. Returns xyz (z = the surface)."""
    tris = []
    for ob in objs:
        me = ob.data
        me.calc_loop_triangles()
        mw = ob.matrix_world
        for lt in me.loop_triangles:
            a, b, c = (mw @ me.vertices[i].co for i in lt.vertices)
            nv = (b - a).cross(c - a)
            area = nv.length / 2
            if area < 1e-4:
                continue
            if nv.normalized().z >= min_up:
                tris.append((a, b, c, area))
    if not tris:
        return lambda rng, n: (np.zeros(0), np.zeros(0), np.zeros(0))
    w = np.array([t[3] for t in tris])
    cw = np.cumsum(w) / w.sum()

    def f(rng, n):
        X, Y, Z = [], [], []
        for _ in range(n):
            k = int(np.searchsorted(cw, rng.random()))
            a, b, c, _ar = tris[min(k, len(tris) - 1)]
            r1, r2 = rng.random(), rng.random()
            if r1 + r2 > 1:
                r1, r2 = 1 - r1, 1 - r2
            p = a + (b - a) * r1 + (c - a) * r2
            cen = (a + b + c) / 3
            if inset_m > 0 and (p - cen).length > inset_m:
                p = p + (cen - p).normalized() * inset_m
            X.append(p.x)
            Y.append(p.y)
            Z.append(p.z)
        return np.array(X), np.array(Y), np.array(Z)
    return f


# ----------------------------------------------------------------------------- ground + land masks (scatter safety)
GROUND_TOP_PREFIXES = ("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER")      # the collision tops a prop may stand on
_GLOBAL_GROUND = {}


def ground_sampler(D=None):
    """callable(X, Y) -> Z array (NaN where no ground): the height of the TOP surface of the TERRAIN / FAIRWAY / GREEN / TEE_BOX
    / BUNKER meshes of the scene, found by casting rays straight down onto their upward faces (a BVH built from the scene as it
    is NOW and cached until those objects change). With no such mesh in the scene (only P.start was called) it returns D.play_z
    everywhere. This is what scatter() stands props on, so every prop base sits on a real ground surface."""
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.data is not None and o.name.startswith(GROUND_TOP_PREFIXES)
            and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    key = tuple(sorted((o.name, len(o.data.vertices), len(o.data.polygons)) for o in objs))
    store = D.__dict__ if D is not None else _GLOBAL_GROUND
    cache = store.get("_props_ground")
    if cache is not None and cache[0] == key:
        return cache[1]
    zdef = float(D.play_z) if D is not None else 0.0
    if not objs:
        def flat(X, Y):
            return np.full(np.shape(np.asarray(X, float)), zdef)
        store["_props_ground"] = (key, flat)
        return flat
    Vs, Fs = [], []
    for o in objs:
        me = o.data
        co, tris, _tp = _np_mesh(me)
        mw = np.array(o.matrix_world)
        w = co @ mw[:3, :3].T + mw[:3, 3]
        a, b, c = w[tris[:, 0]], w[tris[:, 1]], w[tris[:, 2]]
        nv = np.cross(b - a, c - a)
        up = nv[:, 2] > 0.2 * np.maximum(np.linalg.norm(nv, axis=1), 1e-12)       # a ground top is facing up (< 78 degrees)
        base = len(Vs)
        Vs.extend(map(tuple, w.tolist()))
        Fs.extend((tris[up] + base).tolist())
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromPolygons(Vs, [tuple(f) for f in Fs])
    ztop = max(v[2] for v in Vs) + 5.0
    down = Vector((0.0, 0.0, -1.0))

    def sample(X, Y):
        X = np.asarray(X, float)
        Y = np.asarray(Y, float)
        out = np.full(X.shape, np.nan)
        flat_x, flat_y, flat_o = X.ravel().tolist(), Y.ravel().tolist(), out.ravel()
        for i, (x, y) in enumerate(zip(flat_x, flat_y)):
            hit = bvh.ray_cast(Vector((x, y, ztop)), down, ztop + 50.0)
            if hit[0] is not None:
                flat_o[i] = hit[0].z
        return flat_o.reshape(X.shape)
    store["_props_ground"] = (key, sample)
    return sample


def mask_real_land(D, shore_margin_m=1.0, ring=12):
    """The REAL land: the point is land by the lie mirror (inside the shore, outside every water ellipse = the TERRAIN top
    outline minus the water ellipses) AND so is every one of `ring` points on a circle of shore_margin_m round it, i.e. at
    least shore_margin_m (default 1.0 m) from any shore edge, ellipse edge or cliff lip; once build_terrain has run the exact
    distance to the terrain top outlines (D.loops) must also be >= shore_margin_m. Works after P.start alone (ring test only)."""
    ang = np.linspace(0.0, math.tau, ring, endpoint=False)
    dx, dy = np.cos(ang) * shore_margin_m, np.sin(ang) * shore_margin_m

    def f(X, Y):
        X, Y = _xy(X, Y)
        ok = P.lie_codes_m(D, X, Y) != P.LIE_WATER
        if shore_margin_m > 0:
            for k in range(ring):
                idx = np.nonzero(ok)[0]
                if len(idx) == 0:
                    break
                ok[idx] = P.lie_codes_m(D, X[idx] + dx[k], Y[idx] + dy[k]) != P.LIE_WATER
            idx = np.nonzero(ok)[0]
            if len(idx) and D.loops:                         # exact distance to the terrain top outlines (cliff lip + ellipses)
                ok[idx] = P.signed_dist_m(D, X[idx], Y[idx]) <= -shore_margin_m
        return ok
    return f


def _object_footprint_grid(objs, margin_m=0.4, cell=0.25):
    """Rasterise the XY footprint of mesh objects (every triangle, world space) on a regular grid, dilated by margin_m.
    Returns (x0, y0, cell, bool grid) or None when the objects have no triangles."""
    tri_pts = []
    for o in objs:
        co, tris, _tp = _np_mesh(o.data)
        if len(tris) == 0:
            continue
        mw = np.array(o.matrix_world)
        w = co @ mw[:3, :3].T + mw[:3, 3]
        tri_pts.append(w[tris][:, :, :2])
    if not tri_pts:
        return None
    T = np.concatenate(tri_pts, 0)
    pad = margin_m + 2 * cell
    x0, y0 = float(T[:, :, 0].min()) - pad, float(T[:, :, 1].min()) - pad
    nx = int(math.ceil((float(T[:, :, 0].max()) + pad - x0) / cell)) + 1
    ny = int(math.ceil((float(T[:, :, 1].max()) + pad - y0) / cell)) + 1
    g = np.zeros((nx, ny), bool)
    for A, B, C in T:
        i0 = max(0, int(math.floor((min(A[0], B[0], C[0]) - x0) / cell)))
        i1 = min(nx - 1, int(math.floor((max(A[0], B[0], C[0]) - x0) / cell)))
        j0 = max(0, int(math.floor((min(A[1], B[1], C[1]) - y0) / cell)))
        j1 = min(ny - 1, int(math.floor((max(A[1], B[1], C[1]) - y0) / cell)))
        d = (B[1] - C[1]) * (A[0] - C[0]) + (C[0] - B[0]) * (A[1] - C[1])
        if abs(d) < 1e-12:
            g[i0:i1 + 1, j0:j1 + 1] = True
            continue
        X = x0 + (np.arange(i0, i1 + 1) + 0.5)[:, None] * cell
        Y = y0 + (np.arange(j0, j1 + 1) + 0.5)[None, :] * cell
        l1 = ((B[1] - C[1]) * (X - C[0]) + (C[0] - B[0]) * (Y - C[1])) / d
        l2 = ((C[1] - A[1]) * (X - C[0]) + (A[0] - C[0]) * (Y - C[1])) / d
        l3 = 1.0 - l1 - l2
        e = 0.5 * cell / max(1e-6, min(abs(d) ** 0.5, 1.0))        # touch tolerance: slivers still mark their cells
        g[i0:i1 + 1, j0:j1 + 1] |= (l1 >= -e) & (l2 >= -e) & (l3 >= -e)
    r = int(math.ceil(margin_m / cell))
    if r > 0:
        out = g.copy()
        for sx in range(-r, r + 1):
            for sy in range(-r, r + 1):
                if sx * sx + sy * sy <= r * r + 1:
                    out |= np.roll(np.roll(g, sx, 0), sy, 1)
        g = out
    return x0, y0, cell, g


def mask_off_objects(prefixes=("DRESS_PATH",), margin_m=0.4, cell=0.25):
    """True away from the XY footprint (plus margin_m) of every mesh object in the scene whose name starts with one of
    `prefixes` (default the Needle stone path DRESS_PATH*). The footprint is read when the mask is CREATED, so build the
    path before scattering. Objects that are not in the scene yet simply do not exclude anything."""
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.data is not None and o.name.startswith(tuple(prefixes))
            and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    fp = _object_footprint_grid(objs, margin_m, cell) if objs else None

    def f(X, Y):
        X, Y = _xy(X, Y)
        if fp is None:
            return np.ones(X.shape, bool)
        x0, y0, c, g = fp
        i = np.floor((X - x0) / c).astype(int)
        j = np.floor((Y - y0) / c).astype(int)
        inside = (i >= 0) & (i < g.shape[0]) & (j >= 0) & (j < g.shape[1])
        hit = np.zeros(X.shape, bool)
        hit[inside] = g[i[inside], j[inside]]
        return ~hit
    return f


def mask_off_markers(r_m=1.5, names=("TEE_MARKER", "HOLE_CUP", "BALL_START", "FLAG")):
    """True at least r_m metres from the gameplay markers in the scene (TEE_MARKER_1/2, HOLE_CUP, BALL_START, FLAG)."""
    pts = [(o.matrix_world.translation.x, o.matrix_world.translation.y) for o in bpy.data.objects
           if o.name.startswith(tuple(names)) and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    return mask_not_near(pts, r_m) if pts else (lambda X, Y: np.ones(np.shape(np.asarray(X, float)), bool))


def region_land(D):
    """Candidates over the land bounding box (the shore polygon's bbox) - the default scatter region. Feed it to scatter() as
    it is: the real-land mask removes everything that is not land."""
    x0, y0, x1, y1 = P._shore_bbox_m(D, 0.0)
    return region_rect(x0, y0, x1, y1)


# ----------------------------------------------------------------------------- scatter
# per-kind defaults: (min_gap m, scale range, sink m (rocks: fraction of height), tilt rad, off-play margin m)
# Plants sink at most 0.045 m: the base of every PLANT_ stays within 0.05 m of the ground it stands on.
def _defaults(kind):
    if kind.startswith("PLANT_TUFT"):
        return 0.45, (0.8, 1.25), 0.02, 0.08, 0.35
    if kind.startswith("PLANT_FLOWER"):
        return 0.6, (0.85, 1.2), 0.02, 0.06, 0.4
    if kind.startswith("PLANT_SHRUB"):
        return 1.5, (0.8, 1.3), 0.04, 0.05, 0.8
    if kind.startswith(("PLANT_TREE", "PLANT_PINE")):
        return 3.5, (0.8, 1.2), 0.045, 0.04, 2.5
    if kind.startswith("PLANT_AGAVE"):
        return 0.9, (0.8, 1.25), 0.03, 0.05, 0.8
    if kind.startswith("PLANT_VINE"):
        return 0.8, (0.8, 1.2), 0.0, 0.0, 0.0
    if kind.startswith(("ROCK_SEASTACK", "ROCK_SPIRE", "ROCK_WALL", "ROCK_BASALT")):
        return 10.0, (0.7, 1.2), 0.0, 0.05, 3.0
    if kind.startswith("ROCK_"):
        return 2.0, (0.7, 1.3), 0.2, 0.12, 1.5
    return 1.0, (0.9, 1.1), 0.0, 0.0, 1.0


def _kinds_weights(kind):
    if isinstance(kind, str):
        return [kind], [1.0]
    if isinstance(kind, dict):
        ks = list(kind.keys())
        return ks, [float(kind[k]) for k in ks]
    ks = list(kind)
    return ks, [1.0] * len(ks)


def _occ(D):
    if D is None:
        return _GLOBAL_OCC
    occ = D.__dict__.get("_props_occ")
    if occ is None:
        occ = []
        D.__dict__["_props_occ"] = occ
    return occ


_GLOBAL_OCC = []


def reset_scatter(D=None):
    """Forget the registered footprints (D=None: the plain-scene registry)."""
    if D is None:
        _GLOBAL_OCC.clear()
    else:
        D.__dict__["_props_occ"] = []


def _seed_int(*parts):
    return zlib.crc32("|".join(str(p) for p in parts).encode()) & 0x7FFFFFFF


def scatter(D, kind, count, region=None, masks=(), seed=0, min_gap=None, scale_range=None, avoid=(), z=None, sink=None,
            tilt=None, safe=True, corridor_m=None, respect=True, register=True, wet=False, max_tries=12, yaw=True,
            margin_m=None, footprint_k=0.45, surface=None, shore_margin_m=1.0, allow_low=False):
    """Deterministic Poisson-ish scatter of linked duplicates. See the module docstring. Returns the placed objects.

    LAND RULE (v2): `surface` is 'land' (default for everything that is not a ROCK_), 'sea' (rocks only: the centre must be
    water) or 'any' (default for ROCK_ kinds: the old behaviour, region and z decide). A 'land' scatter ALWAYS intersects
    whatever region you pass (None = the whole land bounding box, a rect tuple, a callable, a huge disc ...) with the real land:
    mask_real_land (lie != water at the point and on a ring of shore_margin_m = 1.0 m round it, i.e. >= 1 m from every shore /
    ellipse / lip edge), so nothing can land on the water; PLANT_ kinds refuse surface='sea'. Then, when D is given and safe:
    mask_off_play (never inside fairway + first cut / green + apron / tee box / bunker + lip, per-kind margin), the pin (6 m),
    the DRESS_PATH footprint (+0.4 m, read from the scene now) and 1.5 m round the gameplay markers; tall kinds also keep the
    corridor along the centerline. With z=None a land prop stands on the TOP surface found by a ray cast onto the TERRAIN /
    FAIRWAY / GREEN meshes of the scene (ground_sampler; D.play_z when there is none) minus its sink (plants <= 0.045 m), so
    |base - ground| <= 0.05 m. Regions that supply their own heights (region_on_objects: ledges, arch and stack tops) bypass
    the land mask and the ray cast and mark the props lk_on_object = 1."""
    kinds, weights = _kinds_weights(kind)
    if wet:
        kinds = [k + "_WET" if k in ROCK_BUILDERS else k for k in kinds]
    for k in kinds:
        library_object(k)
    base = kinds[0].replace("_WET", "")
    all_rock = all(k.startswith("ROCK_") for k in kinds)
    if surface is None:
        surface = "any" if all_rock else "land"
    if surface not in ("land", "sea", "any"):
        raise ValueError("surface must be 'land', 'sea' or 'any'")
    if surface != "land" and not all_rock:
        raise ValueError("only ROCK_ kinds may be scattered on the sea / anywhere: plants and dressing stay on the land")
    g0, sr0, sk0, tl0, mg0 = _defaults(base)
    min_gap = g0 if min_gap is None else min_gap
    scale_range = sr0 if scale_range is None else scale_range
    tilt = tl0 if tilt is None else tilt
    margin_m = mg0 if margin_m is None else margin_m
    rng = random.Random(_seed_int(seed, "|".join(kinds), getattr(D, "number", 0) if D is not None else 0))
    all_masks = list(masks)
    ring_masks = []
    if D is not None and safe:
        ring_masks = [mask_off_play(D, margin_m), mask_pin_clear(D, 6.0), mask_off_objects(("DRESS_PATH",), 0.4),
                      mask_off_markers(1.5)]
        tall = base.startswith(TALL_KINDS)
        cm = corridor_m
        if cm is None and tall:
            cm = D.hole.fw / 2 * YD + 3.0
        if cm:
            ring_masks.append(mask_corridor(D, cm))
        all_masks += ring_masks
    land_mask = None
    if D is not None and surface == "land":
        land_mask = mask_real_land(D, shore_margin_m)
        all_masks.append(land_mask)
    elif D is not None and surface == "sea":
        all_masks.append(mask_water(D))
    elif D is None and surface == "land" and not allow_low:
        raise ValueError(f"scatter(D=None, {base}): plants and dressing need D for the land mask (nothing else knows where the water is); "
                         f"pass the postcard_lib handle, use surface='any' / 'sea' with ROCK_ kinds, or allow_low=True in a test scene "
                         f"on a reference ground")
    if region is None:
        if D is None:
            raise ValueError("scatter without D needs a region")
        region = region_land(D)
    elif isinstance(region, (tuple, list)) and len(region) == 4 and not callable(region):
        region = region_rect(*region)
    ground = ground_sampler(D) if (D is not None and surface == "land" and z is None) else None
    occ = _occ(D)
    cell = max(min_gap, 0.5)
    grid = {}

    def gkey(x, y):
        return (int(math.floor(x / cell)), int(math.floor(y / cell)))
    max_r = [0.0]
    if respect:
        for (ox, oy, orr) in occ:
            grid.setdefault(gkey(ox, oy), []).append((ox, oy, orr))
            max_r[0] = max(max_r[0], orr)
    bbs = {k: lib_bbox(k) for k in kinds}
    cw = np.cumsum(weights) / sum(weights)
    placed, objs = [], []
    batch = max(2000, count * 30)
    for _ in range(max_tries):
        if len(placed) >= count:
            break
        X, Y, Z = region(rng, batch)
        X = np.asarray(X, float)
        Y = np.asarray(Y, float)
        if len(X) == 0:
            continue
        has_z = Z is not None
        ok = np.ones(len(X), bool)
        for m in all_masks:
            if m is land_mask and has_z:
                continue                                  # a region with its own heights stands on objects, not on the land
            sub = np.nonzero(ok)[0]
            if len(sub) == 0:
                break
            ok[sub] = np.asarray(m(X[sub], Y[sub]), bool)
        for ax, ay, ar in avoid:
            ok &= np.hypot(X - ax, Y - ay) >= ar
        idx = np.nonzero(ok)[0]
        if D is not None and len(idx):
            wet_c = P.lie_codes_m(D, X[idx], Y[idx]) == P.LIE_WATER
            water_at = dict(zip(idx.tolist(), wet_c.tolist()))
        else:
            water_at = {}
        for i in idx:
            if len(placed) >= count:
                break
            x, y = float(X[i]), float(Y[i])
            k = kinds[int(np.searchsorted(cw, rng.random()))]
            s = rng.uniform(*scale_range)
            mn, mx = bbs[k]
            foot = max(mx.x - mn.x, mx.y - mn.y) * 0.5 * s
            rad = max(min_gap * 0.5, foot * footprint_k)
            gx, gy = gkey(x, y)
            reach = int(math.ceil((rad + max_r[0]) / cell)) + 1
            clash = False
            for ii in range(gx - reach, gx + reach + 1):
                for jj in range(gy - reach, gy + reach + 1):
                    for (ox, oy, orr) in grid.get((ii, jj), ()):
                        if math.hypot(x - ox, y - oy) < max(rad + orr, min_gap):
                            clash = True
                            break
                    if clash:
                        break
                if clash:
                    break
            if clash:
                continue
            if ring_masks and foot > 0.25:               # the whole footprint, not just the centre, must pass the safety masks
                aa = np.linspace(0.0, math.tau, 8, endpoint=False)
                RX, RY = x + 0.75 * foot * np.cos(aa), y + 0.75 * foot * np.sin(aa)
                rok = np.ones(8, bool)
                for m in ring_masks:
                    rok &= np.asarray(m(RX, RY), bool)
                if not water_at.get(int(i), True):       # a land prop keeps its whole footprint on land (no overhang)
                    rok &= P.lie_codes_m(D, RX, RY) != P.LIE_WATER
                if not rok.all():
                    continue
            if z is None:
                if has_z:
                    zz = Z[i]
                elif ground is not None:
                    zz = float(ground(np.array([x]), np.array([y]))[0])
                    if not math.isfinite(zz):
                        continue                          # no ground under it: not a place for a land prop
                elif D is not None:
                    zz = 0.0 if water_at.get(int(i), False) else D.play_z
                else:
                    zz = 0.0
            elif callable(z):
                zz = z(x, y)
            elif isinstance(z, (tuple, list)):
                zz = rng.uniform(*z)
            else:
                zz = float(z)
            sk = sk0 if sink is None else sink
            if base.startswith("ROCK_") and not base.startswith(("ROCK_SEASTACK", "ROCK_SPIRE", "ROCK_WALL", "ROCK_BASALT")) \
                    and sink is None:
                sk = sk0 * (mx.z - max(mn.z, 0.0)) * s
            if base.startswith("PLANT_"):
                sk = min(sk, 0.045)
            if k.startswith("ROCK_"):
                scl = (s * rng.uniform(0.9, 1.1), s * rng.uniform(0.9, 1.1), s * rng.uniform(0.85, 1.1))
            else:
                scl = (s, s, s * rng.uniform(0.92, 1.08))
            ob = place(D, k, (x, y, float(zz) - sk), rng.uniform(0, math.tau) if yaw else 0.0, scl,
                       (rng.uniform(-tilt, tilt), rng.uniform(-tilt, tilt)), allow_low=allow_low)
            if has_z:
                ob["lk_on_object"] = 1
            objs.append(ob)
            placed.append((x, y))
            item = (x, y, rad)
            grid.setdefault((gx, gy), []).append(item)
            max_r[0] = max(max_r[0], rad)
            if register:
                occ.append(item)
    return objs


# ----------------------------------------------------------------------------- edge fringe (v2 2026-10-05: grass liveliness)
FRINGE_TOP_CAP_M = 0.55 * YD            # 0.503 m: tuft tops near the ball corridor / a shot ball stay under 0.55 yd above the ground
FRINGE_PATH_PREFIXES = ("DRESS_PATH", "CART_PATH", "DRESS_PADEDGE", "DRESS_PADRIM")
FRINGE_BALL_R = 4.0                     # no tuft footprint within 4 m of a shot ball position of the stills configs (1.5 m round the tee ball)
FRINGE_TEE_BALL_R = 1.5
FRINGE_KINDS = dict(tall={"PLANT_TUFT_D": 3, "PLANT_TUFT_E": 2}, fill={"PLANT_TUFT_S": 3})
FRINGE_STILL_FILES = ("work/postcard-look/v2/hole{tag}/landmarks.json", "work/postcard-look/v2/proof/stills_cfg_9.json",
                      "work/postcard-look/v2/proof/stills_cfg_extra.json", "work/postcard-look/v2/runtime/stills_cfg_example.json")
# the shot balls of the stills configs (course yards x, d), the fallback of still_ball_zones when the json files are missing
FRINGE_STILL_BALLS = {8: [(0, 0), (19, 178), (-1, 11), (-4, 322)], 9: [(0, 0), (13.5, 269), (11.8, 235)], 10: [(0, 0), (107.1, 159.3)]}


def still_ball_zones(D=None, number=None, repo=None):
    """[(x_m, y_m, r_m)] of every shot ball of the stills configs of hole `number` (or D.number): read from the hole's landmarks.json and the proof stills configs
    (every dict that has a 'ball': [x_yd, d_yd] and 'hole' == number), merged with FRINGE_STILL_BALLS. r = 4.0 m (FRINGE_BALL_R), 1.5 m for the tee ball (0, 0).
    Course yards -> metres: X = x * YD, Y = d * YD."""
    import json
    n = int(number if number is not None else getattr(D, "number", 0))
    tag = f"{n:02d}"
    balls = set(tuple(float(v) for v in b) for b in FRINGE_STILL_BALLS.get(n, []))
    base = repo or REPO
    for rel in FRINGE_STILL_FILES:
        pth = os.path.join(base, rel.format(tag=tag))
        if not os.path.isfile(pth):
            continue
        try:
            with open(pth) as f:
                data = json.load(f)
        except Exception:
            continue
        stack = [data]
        while stack:
            x = stack.pop()
            if isinstance(x, dict):
                if "ball" in x and isinstance(x["ball"], (list, tuple)) and len(x["ball"]) == 2 and int(x.get("hole", data.get("hole", n) if isinstance(data, dict) else n)) == n:
                    balls.add((float(x["ball"][0]), float(x["ball"][1])))
                stack.extend(v for v in x.values() if isinstance(v, (dict, list)))
            elif isinstance(x, list):
                stack.extend(v for v in x if isinstance(v, (dict, list)))
    return [(bx * YD, by * YD, FRINGE_TEE_BALL_R if (abs(bx) < 1e-6 and abs(by) < 1e-6) else FRINGE_BALL_R) for bx, by in sorted(balls)]


def play_edge_distance(D):
    """callable(X, Y, lie=None) -> metres from the nearest PLAY surface footprint (positive outside, <= 0 inside or on it): the analytic outlines of the
    fairway + first cut (centerline distance - fw / 2 - firstcut_yd as metres, like postcard_lib), the green + apron, the tee box and every bunker lip (1.22 x
    the ellipse). Same outlines as mask_off_play (which only answers >= margin), but with the distance. `lie` = a (codes, off) pair from postcard_lib._lie_all
    to save the second evaluation."""
    h = D.hole
    sc = D.scenery
    fc_m = float(sc.get("firstcut_yd", 3.0))
    ap_yd = float(sc.get("apron_yd", 3.0))
    tw, td = sc.get("tee_box_m", (9.0, 7.0))
    tee_r = 0.5 * math.hypot(tw, td) + 0.4
    bunk = [(hx, hd, w, l) for kind, hx, hd, w, l, _t in h.hazards if kind == "bunker"]

    def f(X, Y, lie=None):
        X, Y = _xy(X, Y)
        codes, off = lie if lie is not None else P._lie_all(D, X, Y)
        d = off * YD - (h.fw / 2 * YD + fc_m)
        d = np.minimum(d, np.hypot(X / YD - h.pin[0], Y / YD - h.pin[1]) * YD - (h.gr + ap_yd) * YD)
        d = np.minimum(d, np.hypot(X - h.tee[0] * YD, Y - h.tee[1] * YD) - tee_r)
        for hx, hd, w, l in bunk:
            a = 1.22 * w / 2 * YD
            b = 1.22 * l / 2 * YD
            q = np.sqrt(((X - hx * YD) / max(a, 1e-3)) ** 2 + ((Y - hd * YD) / max(b, 1e-3)) ** 2)
            d = np.minimum(d, (q - 1.0) * min(a, b))
        return np.where(codes == P.LIE_BUNKER, np.minimum(d, -0.01), d)
    return f


def object_edge_distance(prefixes=FRINGE_PATH_PREFIXES, cell=0.25):
    """callable(X, Y) -> signed metres to the XY footprint of every non-library mesh object whose name starts with one of `prefixes` (path ribbons, tee-pad
    edge ribbons): positive outside, 0 inside. Read from the scene NOW (build the path first). None when there is no such object."""
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.data is not None and o.name.startswith(tuple(prefixes))
            and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    fp = _object_footprint_grid(objs, 0.0, cell) if objs else None
    if fp is None:
        return None
    x0, y0, c, g = fp
    er = g.copy()
    er[1:-1, 1:-1] = g[1:-1, 1:-1] & g[:-2, 1:-1] & g[2:, 1:-1] & g[1:-1, :-2] & g[1:-1, 2:]
    bi, bj = np.nonzero(g & ~er)
    from mathutils import kdtree
    kd = kdtree.KDTree(max(1, len(bi)))
    for n_, (i, j) in enumerate(zip(bi.tolist(), bj.tolist())):
        kd.insert((x0 + (i + 0.5) * c, y0 + (j + 0.5) * c, 0.0), n_)
    kd.balance()

    def f(X, Y):
        X, Y = _xy(X, Y)
        out = np.empty(X.shape)
        fx, fy, fo = X.ravel(), Y.ravel(), out.ravel()
        for n_ in range(len(fx)):
            fo[n_] = max(kd.find((float(fx[n_]), float(fy[n_]), 0.0))[2] - 0.5 * c, 0.0)
        i = np.floor((X - x0) / c).astype(int)
        j = np.floor((Y - y0) / c).astype(int)
        inside = (i >= 0) & (i < g.shape[0]) & (j >= 0) & (j < g.shape[1])
        hit = np.zeros(X.shape, bool)
        hit[inside] = g[i[inside], j[inside]]
        out[hit] = 0.0
        return out
    return f


def _fringe_noise(seed):
    """Smooth 2-D modulation 0.15..1 (about 8-20 m wavelength): the fringe is patchy (thick and thin stretches), never an even band."""
    r = random.Random(_seed_int("fringe-noise", seed))
    p = [r.uniform(0, math.tau) for _ in range(6)]
    k = [r.uniform(0.9, 1.25) for _ in range(4)]

    def f(X, Y):
        a = np.sin(X * 0.17 * k[0] + 1.4 * np.sin(Y * 0.11 * k[1] + p[0]) + p[1])
        b = np.sin(Y * 0.21 * k[2] + 1.2 * np.sin(X * 0.09 * k[3] + p[2]) + p[3])
        c = np.sin((X + Y) * 0.31 + p[4]) * 0.5
        return np.clip(0.58 + 0.30 * a + 0.28 * b + 0.12 * c, 0.15, 1.0)
    return f


def _lib_dims(kind):
    """(height m, horizontal reach m = the largest XY distance of any vertex from the origin, triangles) of a library kind."""
    mn, mx = lib_bbox(kind)
    me = library_object(kind).data
    me.calc_loop_triangles()
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    return float(mx.z), float(np.hypot(co[:, 0], co[:, 1]).max()), len(me.loop_triangles)


def scatter_fringe(D, kinds=None, tri_budget=20000, seed=0, density=1.0, sources=("play", "path", "lip"), ball_zones=None, focus=None, focus_boost=3.0,
                   corridor_m=None, top_cap_m=FRINGE_TOP_CAP_M, cap_radius_m=12.0, cluster=(3, 7), single_share=0.18, lean_deg=12.0,
                   height_range=(0.6, 1.8), masks=(), avoid=(), region=None, respect=True, register=True, pin_clear_m=6.0, play_margin_m=0.35,
                   lip_margin_m=0.9, path_margin_m=0.3, path_prefixes=FRINGE_PATH_PREFIXES, falloff_m=(2.4, 1.0, 1.7), reach_m=(11.0, 3.5, 6.5),
                   cluster_gap_m=1.5, tall_share=0.34, max_tufts=None, edge_sample_m=2.0):
    """EDGE FRINGE of tall leaning tuft clumps (v2 2026-10-05): the 'living grass' along the rough edge of the fairway / green / pad rims (sources 'play'), the
    edges of a stone path (sources 'path': DRESS_PATH* / CART_PATH* / DRESS_PADEDGE* footprints, read from the scene NOW: build them first), and the shore tops /
    cliff lips ('lip'). Deterministic (seed). Needs D (the postcard_lib handle after build_terrain / build_play_surfaces / build_gameplay).

    DENSITY: a candidate at distance d outside a play surface (path, lip: inside the cliff edge) has weight exp(-(d - margin) / falloff) up to `reach_m`
    (falloff_m / reach_m = (play, path, lip)), x a smooth patch modulation (0.15..1), x density, x (1 + focus_boost x exp(-(dist / 45 m)^2), default 3) near the `focus` points
    (default: the shot balls of the stills configs), accepted by rejection, so the fringe is densest hugging the edge and thins outward. Cluster seeds keep
    cluster_gap_m (1.5 m) apart.
    CLUSTERS: 80 % of the seeds become a clump of cluster = (3, 7) tufts (a common lean direction + per-tuft jitter, positions within ~0.3-0.9 m), the rest
    (single_share) are single tufts. Each tuft: a kind from kinds = {'tall': {kind: weight}, 'fill': {kind: weight}} (default FRINGE_KINDS: tall D / E, fill S;
    tall_share = chance of a tall kind, higher near the edge), uniform scale f drawn from height_range = (0.6, 1.8) (a clump has its own height factor; the
    scale stays inside SCALE_LIMITS), lean <= lean_deg degrees (the tilt of the instance, any direction), random yaw.
    CAPS: within corridor_m of the tee-to-pin centerline (default fw / 2 + first cut + 6 m) or cap_radius_m (12 m) of a shot ball, the EXACT top of a tuft (the
    highest vertex of the scaled, leaned instance above the ground, incl. the rise of an arching tuft on its low side) stays <= top_cap_m (0.55 yd = 0.503 m, aimed
    at 0.498): a taller draw is scaled down uniformly to the cap.
    EXCLUSIONS (centre AND an 8-point ring at the tuft's own radius + lean shift must pass): never within play_margin_m of a play surface footprint / on a bunker,
    never on or near (path_margin_m) a path ribbon, never on water (real land mask incl. a lip_margin_m 0.9 m margin and a ring on land), >= 6 m from the pin,
    >= 1.5 m from the tee markers / BALL_START, >= 4 m (tee ball 1.5 m) from every shot ball zone (ball_zones = [(x_m, y_m, r_m)], default still_ball_zones(D)),
    out of the registered footprints of earlier scatter calls (respect; rocks, shrubs), outside `avoid` = [(x, y, r)] and every extra mask in `masks`.
    BUDGET: placing stops at tri_budget triangles (default 20000) or max_tufts; the library triangles are D 108, E 135, F 108, S 36, T 36.
    Returns a dict: objs, count, tris, budget, clusters, singles, by_kind {kind: n}, scale_min / scale_max, lean_max_deg, top_max_m, capped (tufts scaled down to a cap),
    edge {'play' | 'path' | 'lip': (sample points, covered)} + edge_samples / edge_covered (the share of rough-edge sample points, spaced edge_sample_m, on the
    strips hugging the play surfaces / the path / the lip, with a fringe tuft within 2.2 m), edge_focus (sample points, covered) of the part of that edge within 45 m of a
    focus point = where the stills cameras look, seeds (accepted cluster seeds), params."""
    if D is None:
        raise ValueError("scatter_fringe needs the postcard_lib handle D (land mask, play surfaces, pin, markers)")
    kd_ = dict(FRINGE_KINDS)
    if kinds:
        kd_ = dict(kinds)
    tall = dict(kd_.get("tall") or {})
    fill = dict(kd_.get("fill") or {})
    if not tall and not fill:
        raise ValueError("scatter_fringe: kinds needs a 'tall' and / or a 'fill' dict {kind: weight}")
    dims = {k: _lib_dims(k) for k in list(tall) + list(fill)}
    for k in dims:
        if not k.startswith("PLANT_TUFT"):
            raise ValueError(f"scatter_fringe places tufts only (got {k})")
    rng = random.Random(_seed_int("fringe", seed, int(D.number), "|".join(sorted(dims))))
    nrng = np.random.default_rng(_seed_int("fringe-np", seed, int(D.number)))
    h = D.hole
    if ball_zones is None:
        ball_zones = still_ball_zones(D)
    ball_zones = [tuple(float(v) for v in z) for z in ball_zones]
    if focus is None:
        focus = [(z[0], z[1]) for z in ball_zones]
    fo = np.asarray(focus, float).reshape(-1, 2) if len(focus) else np.zeros((0, 2))
    if corridor_m is None:
        corridor_m = h.fw / 2 * YD + float(D.scenery.get("firstcut_yd", 3.0)) + 6.0
    px, py = h.pin[0] * YD, h.pin[1] * YD
    markers = [(o.matrix_world.translation.x, o.matrix_world.translation.y) for o in bpy.data.objects
               if o.name.startswith(("TEE_MARKER", "HOLE_CUP", "BALL_START", "FLAG")) and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    d_play_fn = play_edge_distance(D)
    d_path_fn = object_edge_distance(path_prefixes) if "path" in sources else None
    use_play, use_lip = "play" in sources, "lip" in sources
    land = mask_real_land(D, lip_margin_m)
    noise = _fringe_noise(_seed_int(seed, D.number))
    fall_p, fall_a, fall_l = falloff_m
    reach_p, reach_a, reach_l = reach_m
    ground = ground_sampler(D)
    extra = list(masks)
    x0, y0, x1, y1 = P._shore_bbox_m(D, 0.0) if region is None else region
    occ = _occ(D)

    def weight_and_ok(X, Y):
        """(weight, ok) of candidate points: the edge density, the hard (centre) exclusions."""
        lie = P._lie_all(D, X, Y)
        dpl = d_play_fn(X, Y, lie)
        ok = (lie[0] != P.LIE_WATER) & (dpl >= play_margin_m)
        w = np.zeros(X.shape)
        if use_play:
            m = ok & (dpl <= reach_p)
            w = np.where(m, np.exp(-(dpl - play_margin_m) / fall_p), w)
        idx = np.nonzero(ok)[0]
        sd = np.full(X.shape, -1e9)
        if len(idx):
            sd[idx] = P.signed_dist_m(D, X[idx], Y[idx])
        dl = -sd
        ok &= dl >= lip_margin_m
        if use_lip:
            m = ok & (dl <= reach_l)
            w = np.maximum(w, np.where(m, np.exp(-(dl - lip_margin_m) / fall_l), 0.0))
        dpa = np.full(X.shape, 99.0)
        if d_path_fn is not None:
            sub = np.nonzero(ok)[0]
            if len(sub):
                dpa[sub] = d_path_fn(X[sub], Y[sub])
                ok &= dpa >= path_margin_m
                m = ok & (dpa <= reach_a)
                w = np.maximum(w, np.where(m, np.exp(-(dpa - path_margin_m) / fall_a), 0.0))
        w = np.where(ok, w, 0.0) * noise(X, Y)
        if len(fo):
            dmin = np.min(np.hypot(X[:, None] - fo[None, :, 0], Y[:, None] - fo[None, :, 1]), axis=1)
            w = w * (1.0 + focus_boost * np.exp(-(dmin / 45.0) ** 2))
        ok &= np.hypot(X - px, Y - py) >= pin_clear_m + 0.5
        for mx_, my_ in markers:
            ok &= np.hypot(X - mx_, Y - my_) >= 1.5 + 0.5
        for bx, by, br in ball_zones:
            ok &= np.hypot(X - bx, Y - by) >= br + 0.4
        for ax_, ay_, ar_ in avoid:
            ok &= np.hypot(X - ax_, Y - ay_) >= ar_
        for m_ in extra:
            sub = np.nonzero(ok)[0]
            if len(sub):
                ok[sub] = np.asarray(m_(X[sub], Y[sub]), bool)
        return w * density, ok, lie, dict(dpl=dpl, dl=dl, dpa=dpa)

    # ---- 1. cluster seeds: rejection sampling on the edge density
    avg_tris = 0.5 * (np.mean([dims[k][2] for k in tall]) if tall else 60.0) * tall_share * 2 + (1 - tall_share) * (np.mean([dims[k][2] for k in fill]) if fill else 60.0)
    n_target = int(max(8, tri_budget / max(avg_tris, 30.0) / 4.2 * 2.0))       # 4.2 tufts per seed on average, x2 spare (rejected members, cap swaps): the budget binds
    seeds = []
    cell = cluster_gap_m
    sgrid = {}
    batch = 60000
    for _try in range(14):
        if len(seeds) >= n_target:
            break
        X = nrng.uniform(x0, x1, batch)
        Y = nrng.uniform(y0, y1, batch)
        w, ok, lie, _inf = weight_and_ok(X, Y)
        acc = ok & (nrng.random(batch) < w) & land(X, Y)
        for i in np.nonzero(acc)[0].tolist():
            x, y = float(X[i]), float(Y[i])
            gx, gy = int(x // cell), int(y // cell)
            if any(math.hypot(x - sx_, y - sy_) < cluster_gap_m for ii in (gx - 1, gx, gx + 1) for jj in (gy - 1, gy, gy + 1) for (sx_, sy_, _w) in sgrid.get((ii, jj), ())):
                continue
            sgrid.setdefault((gx, gy), []).append((x, y, float(w[i])))
            seeds.append((x, y, float(w[i]), float(lie[1][i])))
            if len(seeds) >= n_target:
                break
    rng.shuffle(seeds)

    # ---- 2. clusters
    objs, by_kind = [], {}
    tris_used, n_cluster, n_single, n_capped = 0, 0, 0, 0
    lean_max, top_max, s_min, s_max = 0.0, 0.0, 9.0, 0.0
    placed_xy = []
    grid = {}
    pcell = 0.5
    rad_max = [0.0]
    if respect:
        for (ox, oy, orr) in occ:
            grid.setdefault((int(ox // pcell), int(oy // pcell)), []).append((ox, oy, orr))
            rad_max[0] = max(rad_max[0], orr)
    lean_rad = math.radians(lean_deg)
    csizes = list(range(cluster[0], cluster[1] + 1))
    cw = [1.0 + 0.6 * min(k - cluster[0], cluster[1] - k) for k in csizes]
    ring_a = np.linspace(0.0, math.tau, 8, endpoint=False)
    kinds_t, w_t = list(tall), [float(tall[k]) for k in tall]
    kinds_f, w_f = list(fill), [float(fill[k]) for k in fill]
    libco = {}
    for k in dims:
        co_ = np.empty(len(library_object(k).data.vertices) * 3)
        library_object(k).data.vertices.foreach_get("co", co_)
        libco[k] = co_.reshape(-1, 3)
    sink = 0.02

    def pick(pool, wts):
        return pool[int(np.searchsorted(np.cumsum(wts) / sum(wts), rng.random()))] if pool else None

    def try_place(x, y, kind, f, cl_id, phi):
        """Validate (hard exclusions on the centre and an 8-point ring at the tuft's exact reach) and place ONE tuft; True when placed."""
        nonlocal tris_used, lean_max, top_max, s_min, s_max, n_capped
        hh, rf, tr = dims[kind]
        if tris_used + tr > tri_budget:
            return False
        lie = P._lie_all(D, np.array([x]), np.array([y]))
        in_cap = float(lie[1][0]) * YD <= corridor_m or any(math.hypot(x - bx, y - by) <= cap_radius_m for bx, by, _r in ball_zones)
        th = min(lean_rad, max(math.radians(1.5), lean_rad * math.sqrt(rng.random())))      # tilt magnitude <= lean_deg
        ang = phi + rng.uniform(-0.6, 0.6)
        yaw = rng.uniform(0, math.tau)
        Rm = Matrix.Rotation(th, 3, Vector((-math.sin(ang), math.cos(ang), 0.0))) @ Matrix.Rotation(yaw, 3, 'Z')
        Rn = np.array(Rm)
        f = min(max(f, SCALE_LIMITS["lo"]), SCALE_LIMITS["hi"])
        sxs, sys_ = f * rng.uniform(0.94, 1.06), f * rng.uniform(0.94, 1.06)
        Wv = (libco[kind] * np.array([sxs, sys_, f])) @ Rn.T                 # the instance's vertices relative to its origin (rotation applied last = world axes)
        top0 = float(Wv[:, 2].max())
        capped = False
        if in_cap and top0 - sink > top_cap_m - 0.005:                         # the EXACT top (incl. the lean of an arching tuft) stays under the cap
            k = (top_cap_m - 0.005 + sink) / top0
            f, sxs, sys_, top0, Wv = f * k, sxs * k, sys_ * k, top0 * k, Wv * k
            capped = True
            if f < SCALE_LIMITS["lo"]:
                return False
        r_f = float(np.hypot(Wv[:, 0], Wv[:, 1]).max()) + 0.02
        RX, RY = x + r_f * np.cos(ring_a), y + r_f * np.sin(ring_a)
        QX, QY = np.concatenate([[x], RX]), np.concatenate([[y], RY])
        lie9 = P._lie_all(D, QX, QY)
        dpl = d_play_fn(QX, QY, lie9)
        if (lie9[0] == P.LIE_WATER).any() or (dpl < play_margin_m * 0.5).any():
            return False
        if d_path_fn is not None and (d_path_fn(QX, QY) < path_margin_m * 0.5).any():
            return False
        if not np.asarray(land(np.array([x]), np.array([y])), bool)[0]:
            return False
        if (P.signed_dist_m(D, RX, RY) > -0.25).any():
            return False
        if math.hypot(x - px, y - py) < pin_clear_m + r_f or any(math.hypot(x - mx_, y - my_) < 1.5 + r_f for mx_, my_ in markers):
            return False
        if any(math.hypot(x - bx, y - by) < br + r_f for bx, by, br in ball_zones) or any(math.hypot(x - ax_, y - ay_) < ar_ + r_f for ax_, ay_, ar_ in avoid):
            return False
        if extra and not all(bool(np.asarray(m_(QX, QY), bool).all()) for m_ in extra):
            return False
        reach = int(math.ceil((r_f + rad_max[0]) / pcell)) + 1
        gx, gy = int(x // pcell), int(y // pcell)
        for ii in range(gx - reach, gx + reach + 1):
            for jj in range(gy - reach, gy + reach + 1):
                for (ox, oy, orr) in grid.get((ii, jj), ()):
                    if math.hypot(x - ox, y - oy) < 0.6 * orr + 0.5 * r_f + 0.08:
                        return False
        zz = float(ground(np.array([x]), np.array([y]))[0])
        if not math.isfinite(zz):
            return False
        ob = place(D, kind, (x, y, zz - sink), yaw, (sxs, sys_, f))
        ob.rotation_euler = Rm.to_euler()
        ob["lk_fringe"] = cl_id
        ob["lk_fringe_f"] = round(f, 3)
        ob["lk_fringe_lean_deg"] = round(math.degrees(th), 2)
        objs.append(ob)
        placed_xy.append((x, y))
        by_kind[kind] = by_kind.get(kind, 0) + 1
        tris_used += tr
        lean_max = max(lean_max, math.degrees(th))
        top_max = max(top_max, top0 - sink)
        s_min, s_max = min(s_min, f), max(s_max, f)
        n_capped += 1 if capped else 0
        item = (x, y, 0.5 * r_f)
        grid.setdefault((gx, gy), []).append(item)
        rad_max[0] = max(rad_max[0], item[2])
        if register:
            occ.append(item)
        return True

    for (sx, sy, sw_, soff) in seeds:
        if (max_tufts is not None and len(objs) >= max_tufts) or tris_used >= tri_budget:
            break
        single = rng.random() < single_share
        n = 1 if single else rng.choices(csizes, weights=cw)[0]
        cf = min(1.35, max(0.7, rng.lognormvariate(0.0, 0.22)))               # the clump's own height factor
        phi = rng.uniform(0, math.tau)                                       # the clump's common lean direction
        spread = 0.3 + 0.07 * n
        cl_id = n_cluster + n_single + 1
        near_edge = sw_ > 0.45
        got, here = 0, []
        for mi in range(n):
            if max_tufts is not None and len(objs) >= max_tufts:
                break
            for attempt in range(5):
                if single or (mi == 0 and attempt == 0):
                    x, y = sx, sy
                else:
                    rr_ = abs(rng.gauss(0.0, spread)) + 0.12
                    aa_ = rng.uniform(0, math.tau)
                    x, y = sx + rr_ * math.cos(aa_), sy + rr_ * math.sin(aa_)
                if any(math.hypot(x - mx_, y - my_) < 0.16 for mx_, my_ in here):
                    continue
                use_tall = bool(kinds_t) and (not kinds_f or rng.random() < min(0.85, tall_share * (1.35 if near_edge else 0.8)))
                kind = pick(kinds_t, w_t) if use_tall else pick(kinds_f, w_f)
                f = min(height_range[1], max(height_range[0], cf * (0.6 + 1.2 * rng.betavariate(1.6, 2.6))))
                if try_place(x, y, kind, f, cl_id, phi):
                    got += 1
                    here.append((x, y))
                    break
        if got:
            if single or n == 1:
                n_single += 1
            else:
                n_cluster += 1

    # ---- 3. coverage of the rough edge: sample points (spacing edge_sample_m) on the strip hugging the play surfaces / the path edge / the lip, vs the nearest fringe tuft
    es = edge_sample_m
    SX, SY = np.meshgrid(np.arange(x0, x1, es), np.arange(y0, y1, es), indexing="ij")
    SX, SY = SX.ravel(), SY.ravel()
    strips = {"play": [], "path": [], "lip": []}
    for a in range(0, len(SX), 60000):
        xs, ys = SX[a:a + 60000], SY[a:a + 60000]
        w, ok, _lie, inf = weight_and_ok(xs, ys)
        ok = ok & np.asarray(land(xs, ys), bool)
        if use_play:
            strips["play"].append(np.stack([xs, ys], 1)[ok & (inf["dpl"] <= play_margin_m + 1.6)])
        if d_path_fn is not None:
            strips["path"].append(np.stack([xs, ys], 1)[ok & (inf["dpa"] <= path_margin_m + 1.2)])
        if use_lip:
            strips["lip"].append(np.stack([xs, ys], 1)[ok & (inf["dl"] <= lip_margin_m + 1.6) & (inf["dpl"] > play_margin_m + 1.6)])
    pa = np.asarray(placed_xy) if placed_xy else np.zeros((0, 2))
    edge = {}
    f_n, f_c = 0, 0
    for k_, lst in strips.items():
        E = np.concatenate(lst, 0) if lst else np.zeros((0, 2))
        cov = 0
        if len(E) and len(pa):
            for a in range(0, len(E), 2000):
                chunk = E[a:a + 2000]
                hit = np.hypot(chunk[:, None, 0] - pa[None, :, 0], chunk[:, None, 1] - pa[None, :, 1]).min(1) <= 2.2
                cov += int(hit.sum())
                if len(fo):
                    near = np.hypot(chunk[:, None, 0] - fo[None, :, 0], chunk[:, None, 1] - fo[None, :, 1]).min(1) <= 45.0
                    f_n += int(near.sum())
                    f_c += int((near & hit).sum())
        elif len(E) and len(fo):
            f_n += int((np.hypot(E[:, None, 0] - fo[None, :, 0], E[:, None, 1] - fo[None, :, 1]).min(1) <= 45.0).sum())
        edge[k_] = (int(len(E)), cov)
    EN = sum(v[0] for v in edge.values())
    EC = sum(v[1] for v in edge.values())
    return dict(objs=objs, count=len(objs), tris=tris_used, budget=tri_budget, clusters=n_cluster, singles=n_single, by_kind=by_kind,
                scale_min=(s_min if objs else 0.0), scale_max=s_max, lean_max_deg=lean_max, top_max_m=top_max, capped=n_capped,
                edge=edge, edge_samples=EN, edge_covered=EC, edge_focus=(f_n, f_c), seeds=len(seeds), corridor_m=corridor_m,
                params=dict(seed=seed, density=density, sources=tuple(sources), cluster=cluster, height_range=height_range, lean_deg=lean_deg,
                            top_cap_m=top_cap_m, cap_radius_m=cap_radius_m, ball_zones=len(ball_zones)))


# ----------------------------------------------------------------------------- rock helpers (postcard_lib replacements)
def scatter_rocks(D, count=20, zone="sea", seed=0, kinds=None, scale_range=None, min_gap=None, avoid=(), region=None,
                  masks=(), variant=None):
    """New-mesh scatter_rocks. zone 'sea': boulders/slabs/ledges standing in the water 3-26 m off the cliff (wet);
    'foot': rubble at the cliff foot 0.5-6 m off the edge (wet); 'rim': rocks on the land 0.4-10 m inside the edge
    (never on play surfaces, out of the camera corridor, on the real land only); 'ledge': small stones on the land 0.2-2.5 m
    inside the edge. variant: None = the zone default (sea/foot '_WET'), 'BASALT' = '<KIND>_BASALT' twins (crater: rocks in/at
    the lava), 'WET', or '' = plain LK_ROCK. Sizes stay inside the SCALE_LIMITS."""
    zones = {
        "sea": (region_band(D, 3.0, 26.0, "sea"), ("ROCK_BOULDER_A", "ROCK_SLAB_A", "ROCK_LEDGE_A", "ROCK_BLOCK_A", "ROCK_BOULDER_B"),
                (1.0, 2.0), 6.0, (-0.6, -0.1), True, "sea"),
        "foot": (region_band(D, 0.5, 6.0, "sea"), ("ROCK_BOULDER_B", "ROCK_BLOCK_A", "ROCK_SLAB_A", "ROCK_CAIRN_A"),
                 (0.7, 1.3), 3.0, (-0.45, -0.05), True, "sea"),
        "rim": (region_band(D, 0.4, 10.0), ("ROCK_BOULDER_B", "ROCK_STONE_A", "ROCK_STONE_B", "ROCK_BLOCK_A", "ROCK_CAIRN_A"),
                (0.6, 1.2), 3.0, None, False, "land"),
        "ledge": (region_band(D, 0.2, 2.5), ("ROCK_STONE_A", "ROCK_STONE_B", "ROCK_BOULDER_B"), (0.6, 1.1), 2.0, None, False, "land"),
    }
    reg, k_def, s_def, g_def, z_def, wet, surf = zones[zone]
    ks = list(kinds or k_def)
    if variant is not None:
        wet = False
        if variant:
            ks = [k + "_" + variant.upper().lstrip("_") if k in ROCK_BUILDERS else k for k in ks]
    return scatter(D, ks, count, region=region or reg, masks=masks, seed=seed,
                   min_gap=g_def if min_gap is None else min_gap, scale_range=scale_range or s_def, avoid=avoid, z=z_def,
                   wet=wet, surface=surf)


def seastack_kind_for(height_m, radius_m=None, rng=None, near=0.25):
    """The sea-stack kind whose natural height (A 13 m, B 18 m, C 24 m above the water) needs the scale closest to 1.1 to
    reach height_m (uniform scale 0.8-1.6 covers 10-35 m); below 8 m (where A would need a scale under 0.62) the needle ROCK_SPIRE_A
    (11 m, scale 0.5-0.73 for 5.5-8 m). With radius_m (the footprint radius the spire should take) the thin ROCK_SPIRE_A competes
    for every height and the cost also counts how far the kind's base radius x scale is from radius_m (0.6 x |log ratio|); kinds
    that would need a scale outside 0.5-2 are never picked; with an rng one of the kinds within `near` of the best cost is drawn
    (variety between the stacks of a hole). Without radius_m only A / B / C (and SPIRE_A below 8 m) are considered."""
    if radius_m is None:
        if height_m < 8.0:
            return "ROCK_SPIRE_A"
        return min(SEASTACK_CORE, key=lambda k: abs(math.log(height_m / (SPIRE_SPECS[k]["height"] * 1.1))))

    def cost(k):
        sp = SPIRE_SPECS[k]
        sc = height_m / sp["height"]
        c = abs(math.log(sc / 1.1)) + 0.6 * abs(math.log(max(radius_m, 0.3) / (sp["base_r"] * sc)))
        return c + (5.0 if not SCALE_LIMITS["lo"] <= sc <= SCALE_LIMITS["hi"] else 0.0)
    costs = {k: cost(k) for k in SPIRE_KINDS}
    best = min(costs.values())
    if rng is None:
        return min(SPIRE_KINDS, key=lambda k: costs[k])
    pool = [k for k in SPIRE_KINDS if costs[k] <= best + near and costs[k] < 5.0]
    return pool[rng.randrange(len(pool))]


SEASTACK_SAT_MIN_H = 5.5          # a satellite spire is never lower than this above the water (SPIRE_A at the 0.5 scale limit)


def _stack_sat_count(radius_m, satellites):
    if satellites == "auto":
        return 0 if radius_m < 5.0 else (1 if radius_m < 8.5 else 2)
    return max(0, int(satellites))


def _stack_cluster(D, x, y, height_m, radius_m, rng, kind=None, wet_foot=True, skirt=3, satellites="auto"):
    """The body of place_sea_stack: ONE main tapering spire (summit height_m above the water) + `satellites` lower spires of OTHER
    kinds beside it (0.5-0.72 x the main height, own yaw / scale, their feet overlap the main foot) + `skirt` wet boulders.
    Returns [main, satellites..., skirt...]. Never tiles, never stacks a piece on another one: every piece stands on the water."""
    n_sat = _stack_sat_count(radius_m, satellites)
    r_main = radius_m * (0.62 if n_sat else 1.0)
    kind = kind or seastack_kind_for(height_m, r_main, rng)
    spec = SPIRE_SPECS.get(kind)
    mn, mx = lib_bbox(kind)
    vis = spec["height"] if spec else float(mx.z)
    s = min(SCALE_LIMITS["hi"], max(SCALE_LIMITS["lo"], height_m / vis))
    base_r = spec["base_r"] if spec else 0.25 * float(mx.x - mn.x + mx.y - mn.y)
    sxy = s * min(1.25, max(0.8, r_main / max(base_r * s, 1e-6)))
    objs = [place(D, kind, (x, y, 0.0), rng.uniform(0, math.tau), (sxy, sxy * rng.uniform(0.92, 1.08), s),
                  (rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03)))]
    rb = base_r * sxy
    feet = [(x, y, rb)]                                      # footprint circles of the spires: every skirt boulder touches one of them
    a0 = rng.uniform(0, math.tau)
    used = {kind}
    for i in range(n_sat):
        h_t = height_m * rng.uniform(0.5, 0.72)
        if h_t < SEASTACK_SAT_MIN_H:
            continue
        cands = [k for k in SPIRE_KINDS if k not in used] or [k for k in SPIRE_KINDS if k != kind]
        k2 = min(cands, key=lambda k: abs(math.log(h_t / SPIRE_SPECS[k]["height"])))
        used.add(k2)
        s2 = min(1.6, max(SCALE_LIMITS["lo"], h_t / SPIRE_SPECS[k2]["height"]))
        r2 = SPIRE_SPECS[k2]["base_r"] * s2
        ang = a0 + (i + 0.5) * math.tau / max(n_sat, 1) + rng.uniform(-0.35, 0.35)
        d = rb * 0.95 + r2 * 0.65
        sxy2 = s2 * rng.uniform(0.92, 1.12)
        objs.append(place(D, k2, (x + math.cos(ang) * d, y + math.sin(ang) * d, 0.0), rng.uniform(0, math.tau),
                          (sxy2, sxy2 * rng.uniform(0.92, 1.08), s2),
                          (rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03))))
        feet.append((x + math.cos(ang) * d, y + math.sin(ang) * d, r2 * sxy2 / s2))
    k0 = rng.randrange(3)
    for i in range(skirt):
        k = ("ROCK_BOULDER_A", "ROCK_BOULDER_B", "ROCK_SLAB_A")[(k0 + i) % 3] + ("_WET" if wet_foot else "")      # distinct kinds, no clones
        sk = min(2.0, max(0.55, radius_m / 3.0 * rng.uniform(0.6, 1.0)))
        mn_b, mx_b = lib_bbox(k)
        rbd = 0.5 * float(max(mx_b.x - mn_b.x, mx_b.y - mn_b.y)) * sk
        for _try in range(24):
            fx, fy, fr = feet[rng.randrange(len(feet))]
            a = rng.uniform(0, math.tau)
            d = max(fr + 0.2 * rbd, 0.75 * rbd) + rbd * rng.uniform(0.0, 0.25)    # touches the foot of its spire (d < fr + rbd) but never stands ON its origin
            bx, by = fx + math.cos(a) * d, fy + math.sin(a) * d
            # and never on ANY other spire's origin either (a boulder swallowing a satellite foot reads as a spire standing on a rock)
            if all(math.hypot(bx - gx, by - gy) >= 0.5 * min(2.0 * rbd, 2.2 * gr) + 0.3 for gx, gy, gr in feet):
                break
        objs.append(place(D, k, (bx, by, -0.35 * sk), rng.uniform(0, math.tau), sk))
    return objs


def place_sea_stack(D, x, y, height_m, radius_m, seed=0, kind=None, wet_foot=True, skirt=3, satellites="auto"):
    """A sea stack whose main summit is height_m above the water. ONE tapering spire (kind default = seastack_kind_for(height_m, r) with r
    the main spire's footprint radius: the ROCK_SEASTACK_A / _B / _C / ROCK_SPIRE_A whose natural height needs a uniform scale nearest
    1.1 and whose base fits r, so 5.5-48 m at scale 0.5-2; its footprint follows radius_m only within x0.8-1.25, never stretched past
    the SCALE_LIMITS), plus `satellites` lower spires of other kinds
    standing beside it ('auto' = 0 for radius_m < 5, 1 below 8.5, else 2; satellites are 0.5-0.72 x the main height, own yaw and
    scale, feet overlapping the main foot; with satellites the main spire takes 0.62 x radius_m) and `skirt` wet boulders round
    the foot. Never tiles or stacks pieces. Returns [main, satellites..., skirt...] (main first)."""
    rng = random.Random(_seed_int("stack", seed, x, y))
    return _stack_cluster(D, x, y, height_m, radius_m, rng, kind=kind, wet_foot=wet_foot, skirt=skirt, satellites=satellites)


def place_outcrop(D, x, y, top_z, radius_m, seed=0, kind="ROCK_OUTCROP_A", yaw=None):
    """A flat-topped rock island (grass cap) whose top is exactly top_z and whose top is about radius_m in radius, its
    foot 3 m (scaled) under the water. The horizontal scale follows radius_m only inside the SCALE_LIMITS relative to the
    vertical one (so the top height stays exact). For the Needle arch: place_outcrop(D, x, y, 5.0, 6.4) then
    build_masonry_arch(D, (x, y, 5.0), ..., base_rock=False). Returns the object."""
    rng = random.Random(_seed_int("outcrop", seed, x, y))
    mn, mx = lib_bbox(kind)
    sxy = radius_m / (0.5 * max(mx.x - mn.x, mx.y - mn.y) * 0.8)
    sz = min(SCALE_LIMITS["hi"], max(SCALE_LIMITS["lo"], (top_z + 3.0) / (mx.z + 3.0)))
    sxy = min(SCALE_LIMITS["hi"], max(SCALE_LIMITS["lo"], sz / SCALE_LIMITS["aniso"], min(sxy, sz * SCALE_LIMITS["aniso"])))
    ob = place(D, kind, (x, y, top_z - mx.z * sz), rng.uniform(0, math.tau) if yaw is None else yaw, (sxy, sxy, sz))
    return ob


LEGACY_MESHES = ("ROCK_SMALL", "ROCK_MEDIUM", "ROCK_LARGE", "CLIFF_ROCK", "ROCK_WALL_FLAT", "ROCK_WALL_BLUNT")

# Plain-rock family (LK_ROCK, _WET twin in the water) and basalt family (LK_BASALT) the swap may pick from.
SWAP_ROCK_FAMILY = ("ROCK_BOULDER_A", "ROCK_BOULDER_B", "ROCK_SLAB_A", "ROCK_BLOCK_A", "ROCK_CAIRN_A", "ROCK_STONE_A",
                    "ROCK_STONE_B", "ROCK_LEDGE_A", "ROCK_CRAG_A", "ROCK_CRAG_B", "ROCK_SEASTACK_A", "ROCK_SEASTACK_B",
                    "ROCK_SEASTACK_C", "ROCK_SEASTACK_D", "ROCK_SEASTACK_E", "ROCK_SPIRE_A")
SWAP_BASALT_FAMILY = ("ROCK_BASALT_A", "ROCK_BASALT_B", "ROCK_BASALT_C", "ROCK_BASALT_D", "ROCK_BASALT_E", "ROCK_BASALT_F",
                      "ROCK_WALL_BASALT_A", "ROCK_WALL_BASALT_B", "ROCK_WALL_BASALT_C",
                      "ROCK_WALL_BASALT_S", "ROCK_WALL_BASALT_M", "ROCK_WALL_BASALT_L")
SWAP_MAX_TILES = 4                      # tiles per HORIZONTAL axis; the cost function keeps real plans at 1-4 instances in total
SWAP_MAX_TILES_Z = 1                    # NEVER tile along z: stacking identical rocks on each other reads as stacked boxes
SWAP_TILE_OVERLAP = 0.85                # tile pitch = 0.85 x tile extent (neighbouring tiles overlap, no gaps)
SWAP_TALL = dict(min_h=8.0, aspect=1.15, sea_floor=-3.0, min_top=8.0)   # sea-stack class (see swap_legacy_rocks)
SWAP_SPIRES = SPIRE_KINDS + tuple(k + "_WET" for k in SPIRE_KINDS)


def fit_plan(W, D, H, kinds, rng, pref_scale=1.15, near=0.35, max_tiles=None, overlap=None):
    """Compose library instances for a legacy bounding box W x D x H metres WITHOUT stretching: for every kind, both
    quarter turns and nx x ny x nz tiles (max_tiles default (4, 4, 1): NEVER more than one tile along z, tiles on the same
    axis overlap by 1 - overlap = 15 %), the per-axis scales (box size / (tile span x library size)) are clamped into
    [0.5, 2.0] with max/min <= 2 (clamp_scale); the cost is the leftover size error (height error counts double: the top line
    matters more than the width) + the number of instances + the anisotropy + the distance of the mean scale from pref_scale. Spire kinds (ROCK_SEASTACK_* / ROCK_SPIRE_*) are only
    offered as a single instance (several identical spires side by side read as organ pipes). One of the plans within `near`
    of the cheapest is picked at random (variety). Returns dict(kind, swap90, n=(nx, ny, nz), scale=(sx, sy, sz) in the
    tile's LOCAL axes, size=(a, b, c) library size, err, cost, ideal)."""
    mt = max_tiles or (SWAP_MAX_TILES, SWAP_MAX_TILES, SWAP_MAX_TILES_Z)
    ov = SWAP_TILE_OVERLAP if overlap is None else overlap
    lo_s, hi_s = SCALE_LIMITS["lo"], SCALE_LIMITS["hi"]
    plans = []
    for kind in kinds:
        mn, mx = lib_bbox(kind)
        a0, b0, c0 = float(mx.x - mn.x), float(mx.y - mn.y), float(mx.z - mn.z)
        faces = len(library_object(kind).data.polygons)
        spire = kind in SWAP_SPIRES
        for swap90 in (False, True):
            aa, bb = (b0, a0) if swap90 else (a0, b0)
            axes = []
            for L, s, nmax in ((W, aa, mt[0]), (D, bb, mt[1]), (H, c0, mt[2])):
                nmax = 1 if spire else nmax

                def ideal_n(n, L=L, s=s):
                    return max(L / (s * (1 + (n - 1) * ov)), 1e-6)
                ok = [n for n in range(1, nmax + 1) if lo_s - 1e-9 <= ideal_n(n) <= hi_s + 1e-9]
                if not ok:      # nothing fits inside the limits: the tile count whose scale is nearest to the allowed range
                    ok = [min(range(1, nmax + 1), key=lambda n: abs(math.log(ideal_n(n) / min(max(ideal_n(n), lo_s), hi_s))))]
                axes.append(ok)
            for nx in axes[0]:
                for ny in axes[1]:
                    for nz in axes[2]:
                        ideal = (W / ((1 + (nx - 1) * ov) * aa), D / ((1 + (ny - 1) * ov) * bb), H / ((1 + (nz - 1) * ov) * c0))
                        sc = clamp_scale(ideal)
                        err = sum(abs(math.log(sc[i] / ideal[i])) for i in range(3))
                        errw = abs(math.log(sc[0] / ideal[0])) + abs(math.log(sc[1] / ideal[1])) + 2.0 * abs(math.log(sc[2] / ideal[2]))
                        n = nx * ny * nz
                        lg = math.log(max(sc) / min(sc))
                        cost = 4.0 * errw + 1.1 * (n - 1) + 0.25 * n * faces / 300.0 + 2.5 * lg * lg \
                            + 0.8 * abs(math.log(sum(sc) / 3.0 / pref_scale))
                        loc = (sc[1], sc[0], sc[2]) if swap90 else sc          # legacy axes -> the tile's local axes
                        plans.append(dict(kind=kind, swap90=swap90, n=(nx, ny, nz), scale=loc, size=(a0, b0, c0), err=err,
                                          cost=cost, ideal=ideal))
    best = min(p["cost"] for p in plans)
    pool = [p for p in plans if p["cost"] <= best + near]
    return pool[rng.randrange(len(pool))]


_BLOB_CACHE = {}


def _is_legacy_rock(ob):
    """A rock object that is NOT a library instance and is a low-poly or blob stand-in: the old icosahedron instances (mesh data
    named ROCK_SMALL / ROCK_MEDIUM / ROCK_LARGE / CLIFF_ROCK / ROCK_WALL_FLAT / ROCK_WALL_BLUNT), hole 9's custom ROCK_STACK_nn, any
    other non-library ROCK_* / CLIFF_ROCK* mesh with fewer than 60 welded vertices, and any other non-library ROCK_* / CLIFF_ROCK*
    mesh that rock_shape_report calls a blob (icosphere / UV sphere / noised sphere of any vertex count). Never the ROCK_SKIN_*
    column skins (assert_no_legacy_rocks still counts their vertices)."""
    if ob.type != 'MESH' or ob.data is None or ob.data.get("lk_kind"):
        return False
    if any(c.name == LIB_COLLECTION for c in ob.users_collection):
        return False
    n = ob.name
    if not (n.startswith("ROCK_") or n.startswith("CLIFF_ROCK")) or n.startswith("ROCK_SKIN_"):
        return False
    if ob.data.name.split(".")[0] in LEGACY_MESHES or n.startswith("ROCK_STACK_"):
        return True
    if welded_vertex_count(ob.data) < SHAPE_TH["min_verts"]:
        return True
    key = (ob.data.name, len(ob.data.vertices), len(ob.data.polygons))
    if key not in _BLOB_CACHE:
        _BLOB_CACHE[key] = bool(rock_shape_report(ob.data)["blob"])
    return _BLOB_CACHE[key]


def legacy_rocks(objs=None):
    """The legacy rock objects of the scene (or of objs): see _is_legacy_rock."""
    pool = objs if objs is not None else bpy.data.objects
    return [o for o in pool if _is_legacy_rock(o)]


def assert_no_legacy_rocks(D=None, objs=None, raise_on_fail=True):
    """Hole scripts call this after swap_legacy_rocks (and the verify step may call it again): no exported ROCK_* / CLIFF_ROCK*
    object may be a legacy low-poly stand-in or have fewer than 60 welded vertices (library instances are checked by their
    mesh; ROCK_SKIN_* column skins by their column count). Library-collection objects (hidden, never exported) are skipped.
    Returns dict(ok, rocks, min_verts, offenders); raises RuntimeError when raise_on_fail and not ok."""
    pool = objs if objs is not None else bpy.data.objects
    bad, nrock, vmin, seen = [], 0, 10 ** 9, {}
    for ob in pool:
        if ob.type != 'MESH' or ob.data is None or any(c.name == LIB_COLLECTION for c in ob.users_collection):
            continue
        n = ob.name
        if not (n.startswith("ROCK_") or n.startswith("CLIFF_ROCK")):
            continue
        nrock += 1
        key = ob.data.name
        if key not in seen:
            seen[key] = welded_vertex_count(ob.data)
        nv = seen[key]
        vmin = min(vmin, nv)
        if _is_legacy_rock(ob) or nv < SHAPE_TH["min_verts"]:
            bad.append(f"{n} ({ob.data.name}, {nv} verts)")
    rep = dict(ok=not bad, rocks=nrock, min_verts=(vmin if nrock else 0), offenders=bad)
    if bad and raise_on_fail:
        raise RuntimeError(f"{len(bad)} legacy / <{SHAPE_TH['min_verts']}-vertex rocks left: {bad[:6]}")
    return rep


def instance_scales(objs=None, only_library=True):
    """[(object, (sx, sy, sz))] of the library instances (objects whose mesh is a hidden-library mesh), world-scale included
    when the parent is scaled. Used by the SWAP_NO_STRETCH_2X gate."""
    out = []
    for ob in (objs if objs is not None else bpy.data.objects):
        if ob.type != 'MESH' or ob.data is None or any(c.name == LIB_COLLECTION for c in ob.users_collection):
            continue
        lo = bpy.data.objects.get(ob.data.name)
        is_lib = lo is not None and lo.data is ob.data and any(c.name == LIB_COLLECTION for c in lo.users_collection)
        if only_library and not is_lib:
            continue
        out.append((ob, tuple(float(v) for v in ob.matrix_world.to_scale())))
    return out


def stretch_report(objs=None):
    """Statistics of the library instance scales: count, per-axis min / max, max of max/min, and every violation of the
    SCALE_LIMITS (each component in [0.5, 2.0] and max/min <= 2). ok = no violation."""
    rows = instance_scales(objs)
    bad, mx_ratio, lo, hi = [], 1.0, 9e9, 0.0
    for ob, sc in rows:
        ratio = max(sc) / max(min(sc), 1e-9)
        mx_ratio = max(mx_ratio, ratio)
        lo, hi = min(lo, min(sc)), max(hi, max(sc))
        if min(sc) < SCALE_LIMITS["lo"] - 1e-4 or max(sc) > SCALE_LIMITS["hi"] + 1e-4 or ratio > SCALE_LIMITS["aniso"] + 1e-4:
            bad.append((ob.name, tuple(round(v, 3) for v in sc)))
    return dict(ok=not bad, n=len(rows), min_scale=(lo if rows else 0.0), max_scale=hi, max_ratio=mx_ratio, violations=bad)


def _world_rot(ob):
    return ob.matrix_world.to_3x3().normalized()


def _legacy_frame(ob):
    """Real-vertex extents of a legacy rock in its own yaw frame: dict(W, D, z0, z1, cx, cy, yaw, ax, ay). yaw = the Euler Z rotation
    of the object (tilt and flips of the old icosahedra are ignored: the replacement stands upright), W / D = extents along the
    yaw axes, (cx, cy) = centre of that box in world metres, z0 / z1 = lowest / highest world vertex."""
    me = ob.data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    M = np.array(ob.matrix_world)
    w = co @ M[:3, :3].T + M[:3, 3]
    yaw = float(ob.matrix_world.to_euler('XYZ').z)
    ax = np.array([math.cos(yaw), math.sin(yaw)])
    ay = np.array([-math.sin(yaw), math.cos(yaw)])
    u, v = w[:, :2] @ ax, w[:, :2] @ ay
    u0, u1, v0, v1 = float(u.min()), float(u.max()), float(v.min()), float(v.max())
    c = ax * (u0 + u1) / 2 + ay * (v0 + v1) / 2
    return dict(W=u1 - u0, D=v1 - v0, z0=float(w[:, 2].min()), z1=float(w[:, 2].max()), cx=float(c[0]), cy=float(c[1]), yaw=yaw,
                ax=ax, ay=ay)


def _adopt(new, ob, cols, parent, pinv, mw):
    """Give a freshly placed replacement the legacy object's collections and parent, keeping its world matrix mw."""
    for c in list(new.users_collection):
        c.objects.unlink(new)
    for c in cols:
        c.objects.link(new)
    new.parent = parent
    if parent is not None:
        new.matrix_parent_inverse = pinv.copy()
    new.matrix_world = mw


def _distinct(tiles):
    """Make sure no two tiles of one legacy box are identical clones (same kind, scale within 3 %, yaw mod 90 degrees within
    4 degrees): the later one gets a 7 % scale change and a yaw offset. tiles = [dict(kind, sc, yaw)] (edited in place)."""
    for j in range(1, len(tiles)):
        for _ in range(4):
            clash = any(tiles[i]["kind"] == tiles[j]["kind"] and
                        max(abs(tiles[i]["sc"][k] / tiles[j]["sc"][k] - 1.0) for k in range(3)) < 0.03 and
                        abs(((tiles[i]["yaw"] - tiles[j]["yaw"]) + math.pi / 4) % (math.pi / 2) - math.pi / 4) < math.radians(4.0)
                        for i in range(j))
            if not clash:
                break
            t = tiles[j]
            t["sc"] = clamp_scale(tuple(v * (1.07 if (_ % 2 == 0) else 0.93) for v in t["sc"]))
            t["yaw"] += math.radians(17.0)


def _resting_groups(pool, frames):
    """Legacy rocks piled on each other (the hole 8 rock-on-rock towers): b RESTS ON a when b's lowest vertex is >= 1 m above a's lowest, inside
    the upper 75 % of a's height range (embedded, not floating), with the footprint centres closer than 0.5 x a's larger extent. Chains are
    unioned. Returns [[objs lowest first]] for every pile of >= 2 whose lowest rock stands in the sea (z0 < 0.3) and whose top is >= 8 m
    above the water (SWAP_TALL min_top): such a pile is ONE sea stack, not a stack of boulders."""
    n = len(pool)
    par = list(range(n))

    def find(i):
        while par[i] != i:
            par[i] = par[par[i]]
            i = par[i]
        return i
    fr = [frames[o.name] for o in pool]
    for i in range(n):
        a = fr[i]
        for j in range(n):
            if i == j:
                continue
            b = fr[j]
            if b["z0"] > a["z0"] + 1.0 and b["z0"] <= a["z1"] - 0.5 and b["z0"] >= a["z1"] - 0.75 * (a["z1"] - a["z0"]) and \
                    math.hypot(b["cx"] - a["cx"], b["cy"] - a["cy"]) <= 0.5 * max(a["W"], a["D"]):
                par[find(j)] = find(i)
    comp = {}
    for i in range(n):
        comp.setdefault(find(i), []).append(i)
    out = []
    for idx in comp.values():
        if len(idx) < 2:
            continue
        idx.sort(key=lambda i: fr[i]["z0"])
        if fr[idx[0]]["z0"] < 0.3 and max(fr[i]["z1"] for i in idx) >= SWAP_TALL["min_top"]:
            out.append([pool[i] for i in idx])
    return out


def swap_legacy_rocks(D, objs=None, basalt=False, rename=True, seed=0, purge=True, report=None):
    """Replace every legacy low-poly / blob rock (see _is_legacy_rock: the icosahedron instances ROCK_SMALL / ROCK_MEDIUM / ROCK_LARGE /
    CLIFF_ROCK / ROCK_WALL_FLAT / ROCK_WALL_BLUNT, hole 9's ROCK_STACK_nn, any other <60-vertex or blob non-library ROCK_*) by library
    instances, NEVER stretching (every scale component in [0.5, 2.0], max/min <= 2) and never stacking identical pieces:

    * the legacy box is measured on its real vertices in its own yaw frame (W x D x H, lowest / highest world z); the replacement
      stands UPRIGHT (yaw only, no legacy tilt or flip) on the box centre;
    * PILES: legacy rocks resting on each other (hole 8's rock-on-rock towers, basalt=False only: the lowest stands in the sea and the
      top is >= 8 m above the water) are ONE sea stack: the upper rocks are deleted and the whole pile becomes one spire cluster (summit
      at the pile's top, footprint of the lowest rock); lk_swap = the lowest rock's name, report entry 'members';
    * SEA STACKS (basalt=False; the rock reaches below z 0.3, its top is >= 8 m above the water and either it is a ROCK_STACK_nn or
      it is >= 8 m tall above the sea floor clamp of -3 m, i.e. 11 m in all, and >= 1.15 x its footprint): ONE tapering spire (_stack_cluster) with
      its summit at the legacy top, plus 0-2 lower satellite spires of other kinds for wide boxes (radius 5 m / 8.5 m) and 2 wet
      skirt boulders. No tiling. The old tall box is never rebuilt from stacked slabs;
    * every other rock: fit_plan picks the kind (plain-rock family with the _WET twin for rocks reaching below z 0.3, or the basalt
      family with basalt=True: wall-ring boxes become ONE ROCK_WALL_BASALT_S/M/L each) and tiles it nx x ny (<= 4 x 4, ONE layer, 15 %
      overlap) only where one instance would need more than 2x; every tile of a multi-tile box gets its own kind / scale / yaw jitter
      so neighbours are never identical clones. Anchoring: a rock in the sea keeps its legacy TOP (never lifted out of the water), a
      rock that rests on something keeps its legacy BOTTOM (real lowest vertex);
    * the first replacement reuses the legacy object (renamed '<KIND>.nnn' when rename); extras are new linked duplicates with the
      same parent / collections; the custom property lk_swap holds the legacy object name (swap_composition_report groups by it);
    * purge=True deletes the hidden legacy template objects; report = optional list receiving one dict per legacy rock
      (legacy, mesh, box, class 'stack'|'rock', kind, n, scale, err, instances); at the end assert_no_legacy_rocks(D, objs) runs and
      RuntimeError is raised if any legacy / <60-vertex / blob rock is left. Returns all replacement objects. D may be None."""
    rng = random.Random(_seed_int("swap", seed, getattr(D, "number", 0)))
    bpy.context.view_layer.update()
    fam = SWAP_BASALT_FAMILY if basalt else SWAP_ROCK_FAMILY
    pool = legacy_rocks(objs)
    frames = {o.name: _legacy_frame(o) for o in pool}
    piles = {} if basalt else {g[0].name: g for g in _resting_groups(pool, frames)}
    upper = {o.name for g in piles.values() for o in g[1:]}
    out = []
    for nm, ob in [(o.name, o) for o in pool]:                  # names first: an upper rock is deleted while its pile is built
        if nm in upper:
            continue                                            # deleted with its pile below
        fr = frames[nm]
        W, Dd = max(fr["W"], 1e-3), max(fr["D"], 1e-3)
        z_top, z_bot = fr["z1"], fr["z0"]
        pile = piles.get(ob.name)
        if pile:
            z_top = max(frames[o.name]["z1"] for o in pile)
        sea = (not basalt) and z_bot < 0.3
        z_floor = max(z_bot, SWAP_TALL["sea_floor"]) if sea else z_bot
        Hh = max(z_top - z_floor, 0.2)
        tall = bool(pile) or (sea and z_top >= SWAP_TALL["min_top"] and (ob.name.startswith("ROCK_STACK_") or
                                                                         (Hh >= SWAP_TALL["min_h"] and Hh >= SWAP_TALL["aspect"] * max(W, Dd))))
        parent, pinv, cols = ob.parent, ob.matrix_parent_inverse.copy(), list(ob.users_collection)
        info = dict(legacy=ob.name, mesh=ob.data.name, box=(round(W, 2), round(Dd, 2), round(Hh, 2)),
                    cls="stack" if tall else "rock", z=(round(z_bot, 2), round(z_top, 2)),
                    members=[o.name for o in pile] if pile else [ob.name])
        news = []
        if tall:
            radius = 0.5 * max(W, Dd)
            sat = _stack_cluster(None, fr["cx"], fr["cy"], z_top, radius, rng, wet_foot=True, skirt=2, satellites="auto")
            for new in sat:
                mw = Matrix.LocRotScale(new.location, new.rotation_euler, new.scale)        # no parent yet: local == world
                _adopt(new, ob, cols, parent, pinv, mw)
                new["lk_swap"] = ob.name
                news.append(new)
            if pile:
                for o in pile[1:]:
                    bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.objects.remove(ob, do_unlink=True)
            info.update(kind=sat[0].data.name, n=(len(sat), 1, 1), scale=tuple(round(v, 3) for v in sat[0].scale), err=0.0,
                        instances=len(sat))
            out += news
            if report is not None:
                report.append(info)
            continue
        kinds = [k + "_WET" if (sea and k in ROCK_BUILDERS) else k for k in fam if sea or k not in SPIRE_KINDS]    # spires only in the sea
        plan = fit_plan(W, Dd, Hh, kinds, rng)
        nx, ny = plan["n"][0], plan["n"][1]
        ov = SWAP_TILE_OVERLAP
        cw, cd = W / (1 + (nx - 1) * ov), Dd / (1 + (ny - 1) * ov)
        tiles = []
        for ix in range(nx):
            for iy in range(ny):
                p = plan if nx * ny == 1 else fit_plan(cw, cd, Hh, kinds, rng, max_tiles=(1, 1, 1))
                sx, sy, sz = p["scale"]
                if nx * ny > 1:
                    sc = clamp_scale((sx * rng.uniform(0.94, 1.06), sy * rng.uniform(0.94, 1.06), sz * rng.uniform(0.94, 1.06)))
                    jit = math.radians(rng.uniform(-14.0, 14.0))
                else:
                    sc, jit = (sx, sy, sz), 0.0
                yaw = fr["yaw"] + (math.pi / 2 if p["swap90"] else 0.0) + (math.pi if rng.random() < 0.5 else 0.0) + jit
                gx = (ix - (nx - 1) / 2.0) * cw * ov + (rng.uniform(-0.06, 0.06) * cw if nx > 1 else 0.0)
                gy = (iy - (ny - 1) / 2.0) * cd * ov + (rng.uniform(-0.06, 0.06) * cd if ny > 1 else 0.0)
                tiles.append(dict(kind=p["kind"], sc=sc, yaw=yaw, g=(gx, gy), err=p["err"]))
        _distinct(tiles)
        first = True
        for t in tiles:
            kind = t["kind"]
            mn, mx = lib_bbox(kind)
            sx, sy, sz = t["sc"]
            ha = float(mx.z - mn.z) * sz
            if sea:
                z0 = min(z_top - ha, min(z_floor, -0.1))            # the legacy top, but the foot stays in the water (never above the legacy foot)
            else:
                z0 = z_bot                                          # rests on something: keep the legacy bottom
            Rt = Matrix.Rotation(t["yaw"], 3, 'Z')
            cx_l, cy_l = float((mn.x + mx.x) / 2) * sx, float((mn.y + mx.y) / 2) * sy
            base = Vector((fr["cx"], fr["cy"], 0.0)) + Vector((fr["ax"][0] * t["g"][0] + fr["ay"][0] * t["g"][1],
                                                                fr["ax"][1] * t["g"][0] + fr["ay"][1] * t["g"][1], 0.0))
            off = Rt @ Vector((cx_l, cy_l, 0.0))
            loc = Vector((base.x - off.x, base.y - off.y, z0 - float(mn.z) * sz))
            Mx = Matrix.Translation(loc) @ Rt.to_4x4() @ Matrix.Diagonal(Vector((sx, sy, sz, 1.0)))
            if first:
                new = ob
                new.data = library_object(kind).data
                first = False
            else:
                new = bpy.data.objects.new("tmp", library_object(kind).data)
                _adopt(new, ob, cols, parent, pinv, Mx)
            new.matrix_world = Mx
            new["lk_swap"] = info["legacy"]
            if rename:
                new.name = _new_name(kind)
            if new.name.startswith(GROUND_PREFIXES):
                raise RuntimeError(f"swap_legacy_rocks produced a collision name {new.name}")
            news.append(new)
        info.update(kind=tiles[0]["kind"], n=(nx, ny, 1), scale=tuple(round(v, 3) for v in tiles[0]["sc"]),
                    err=round(float(np.mean([t["err"] for t in tiles])), 3), instances=len(tiles))
        out += news
        if report is not None:
            report.append(info)
    if purge:
        for t in list(bpy.data.objects):
            if t.type == 'MESH' and t.data is not None and not t.data.get("lk_kind") and \
                    t.data.name.split(".")[0] in LEGACY_MESHES and any(c.name == LIB_COLLECTION for c in t.users_collection):
                if all(o is t or o.data is not t.data for o in bpy.data.objects if o.type == 'MESH'):
                    d = t.data
                    bpy.data.objects.remove(t, do_unlink=True)
                    if d.users == 0:
                        bpy.data.meshes.remove(d)
    for o in list(bpy.data.meshes):
        if o.users == 0 and o.name.split(".")[0] in LEGACY_MESHES or (o.users == 0 and o.name.startswith("ROCK_STACK_")):
            bpy.data.meshes.remove(o)
    left = legacy_rocks(objs)
    if left:
        raise RuntimeError(f"swap_legacy_rocks left {len(left)} legacy rocks: {[o.name for o in left][:6]}")
    assert_no_legacy_rocks(D, objs, raise_on_fail=True)
    return out


def swap_composition_report(objs=None):
    """The 'no stacked boxes, no organ pipes' numbers of a swap, per legacy box (objects carrying lk_swap): VERTICAL STACKS = pairs of
    pieces of the same legacy box whose ORIGINS (= where they stand) are within 0.25 x the smaller footprint in plan but whose
    bbox centres differ in height by more than 0.35 x the smaller height (one sits on the other); IDENTICAL CLONES = pairs of the same kind with every scale component within 3 %
    and the same yaw (mod 90 degrees, within 4 degrees). Returns dict(ok, groups, pieces, vertical_stacks [(a, b)], clones [(a, b)])."""
    groups = {}
    bpy.context.view_layer.update()
    for ob in (objs if objs is not None else bpy.data.objects):
        if ob.type == 'MESH' and ob.data is not None and ob.get("lk_swap") is not None:
            groups.setdefault(ob["lk_swap"], []).append(ob)
    stacks, clones, npc = [], [], 0
    for key, g in groups.items():
        info = []
        for ob in g:
            mn, mx = lib_bbox(ob.data.name)
            sc = ob.matrix_world.to_scale()
            mw = ob.matrix_world
            c = mw @ ((mn + mx) * 0.5)
            fp = max(float(mx.x - mn.x) * sc.x, float(mx.y - mn.y) * sc.y)
            h = float(mx.z - mn.z) * sc.z
            yaw = float(mw.to_euler('XYZ').z)
            info.append((ob, c, fp, h, tuple(sc), yaw, mw.translation.copy()))
        npc += len(info)
        for i in range(len(info)):
            for j in range(i + 1, len(info)):
                a, b = info[i], info[j]
                dxy = math.hypot(a[6].x - b[6].x, a[6].y - b[6].y)
                if dxy < 0.25 * min(a[2], b[2]) and abs(a[1].z - b[1].z) > 0.35 * min(a[3], b[3]):
                    stacks.append((a[0].name, b[0].name))
                if a[0].data.name == b[0].data.name and max(abs(a[4][k] / b[4][k] - 1.0) for k in range(3)) < 0.03 and \
                        abs(((a[5] - b[5]) + math.pi / 4) % (math.pi / 2) - math.pi / 4) < math.radians(4.0):
                    clones.append((a[0].name, b[0].name))
    return dict(ok=not stacks and not clones, groups=len(groups), pieces=npc, vertical_stacks=stacks, clones=clones)


# ----------------------------------------------------------------------------- basalt skin (pillar sides)
def build_basalt_skin(D, loop_xy, top_z, bottom_z, col_r=0.9, inset_m=0.15, seed=0, name="ROCK_SKIN_BASALT", max_faces=500,
                      rows=2, closed=True):
    """Unique column-skin meshes along a CLOSED outline loop_xy [(x, y), ...] metres (land on the LEFT of the travel
    direction, like D.loops): `rows` staggered rows of hex columns whose OUTER faces stay inset_m inside the outline (never
    past it in plan), tops at top_z minus 0..0.35 m, bottoms bottom_z (+ a little jitter). Split into objects of at most
    max_faces faces: <name>_00, _01 ... (ROCK_ prefix, no collider). closed=False treats loop_xy as an open polyline (only
    that stretch of the edge is skinned). Returns [objs]."""
    rng = random.Random(_seed_int("skin", seed, name))
    P2 = np.asarray(loop_xy, float)
    n = len(P2)
    seg = np.hypot(*(np.roll(P2, -1, 0) - P2).T)
    if not closed:
        seg[-1] = 0.0                               # no closing edge
    cum = np.concatenate([[0], np.cumsum(seg)])
    L = cum[-1]
    step = math.sqrt(3) * col_r
    centres = []
    for row in range(rows):
        k = int(L / step)
        for i in range(k):
            s = (i + 0.5 * (row % 2)) * L / k
            j = min(int(np.searchsorted(cum, s, side="right") - 1), n - 1)
            while seg[j] < 1e-9 and j > 0:
                j -= 1
            t = min(1.0, (s - cum[j]) / max(seg[j], 1e-9))
            a, b = P2[j], P2[(j + 1) % n]
            p = a + (b - a) * t
            d = (b - a) / max(seg[j], 1e-9)
            inward = np.array([-d[1], d[0]])
            off = inset_m + col_r + row * 1.5 * col_r
            c = p + inward * off
            centres.append((float(c[0]), float(c[1]), row))
    out = []
    mb = _MB(("LK_BASALT",))
    part = 0
    # even pieces: every ROCK_SKIN_* object has the same number of columns (<= max_faces) so none is a stub of a few columns
    pieces = max(1, int(math.ceil(len(centres) * 9.4 / max_faces)))
    per_piece = int(math.ceil(len(centres) / pieces))
    in_piece = 0

    def flush(mb, part):
        if not mb.F:
            return None
        nm = _new_name(name, "{}_{:02d}")
        me = _finalize(nm, mb, "box", smooth_angle=None, library=False)
        ob = bpy.data.objects.new(nm, me)
        _collection_for("ROCK_").objects.link(ob)
        _parent(D, ob)
        return ob
    for (x, y, row) in centres:
        if in_piece >= per_piece or len(mb.F) + 10 > max_faces:
            out.append(flush(mb, part))
            part += 1
            mb = _MB(("LK_BASALT",))
            in_piece = 0
        top = top_z - rng.uniform(0.0, 0.35) - row * rng.uniform(0.2, 0.9)
        _hex_column(mb, rng, x, y, top, bottom_z + rng.uniform(-0.5, 0.5), col_r, tilt_max=0.12, chip_p=0.3, top_max=top_z)
        in_piece += 1
    o = flush(mb, part)
    if o is not None:
        out.append(o)
    return out


# ----------------------------------------------------------------------------- masonry arch
def _box_block(mb, cx, cy, cz, sx, sy, sz, rng, yaw=0.0, jit=0.03, bottom=False, top_jit=0.04, mat=0):
    """An irregular cut block (8 verts), 5 or 6 faces, centred on (cx, cy, cz), rotated yaw about Z."""
    ca, sa = math.cos(yaw), math.sin(yaw)
    hx, hy, hz = sx / 2, sy / 2, sz / 2
    pts = []
    for (X, Y, Z) in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1), (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)):
        x = X * hx + rng.uniform(-jit, jit) * 0.5
        y = Y * hy + rng.uniform(-jit, jit) * 0.5
        z = Z * hz + (rng.uniform(-top_jit, top_jit * 0.3) if Z > 0 else 0.0)
        pts.append((cx + x * ca - y * sa, cy + x * sa + y * ca, cz + z))
    idx = [mb.v(p) for p in pts]
    b0, b1, b2, b3, t0, t1, t2, t3 = idx
    mb.f((b0, b1, t1, t0), mat)
    mb.f((b1, b2, t2, t1), mat)
    mb.f((b2, b3, t3, t2), mat)
    mb.f((b3, b0, t0, t3), mat)
    mb.f((t0, t1, t2, t3), mat)
    if bottom:
        mb.f((b3, b2, b1, b0), mat)


def _voussoir(mb, zs, a0, a1, r_in, r_out, y0, y1, mat=0):
    """Wedge block of a round arch in the x-z plane (centre (0, zs)), angles a0 < a1 (0 = right spring), depth y0..y1."""
    def p(r, a, y):
        return (r * math.cos(a), y, zs + r * math.sin(a))
    i0f, i1f, o1f, o0f = mb.v(p(r_in, a0, y0)), mb.v(p(r_in, a1, y0)), mb.v(p(r_out, a1, y0)), mb.v(p(r_out, a0, y0))
    i0b, i1b, o1b, o0b = mb.v(p(r_in, a0, y1)), mb.v(p(r_in, a1, y1)), mb.v(p(r_out, a1, y1)), mb.v(p(r_out, a0, y1))
    mb.f((i0f, o0f, o1f, i1f), mat)        # front (y0 = -Y face)
    mb.f((i0b, i1b, o1b, o0b), mat)        # back
    mb.f((o0f, o0b, o1b, o1f), mat)        # extrados
    mb.f((i0f, i1f, i1b, i0b), mat)        # intrados (faces down/in)
    mb.f((i0f, i0b, o0b, o0f), mat)        # side a0
    mb.f((i1f, o1f, o1b, i1b), mat)        # side a1


def _inset_poly(poly, c):
    """Inward offset of a convex counter-clockwise 2D polygon [(x, z), ...] by c metres on every edge (vertex i of the result is
    the corner that belongs to vertex i of the input). c is clamped to 45 % of the inradius so a small polygon never collapses."""
    n = len(poly)
    P = [Vector((p[0], p[1])) for p in poly]
    cen = sum(P, Vector((0.0, 0.0))) / n
    lim = 1e9
    for i in range(n):
        a, b = P[i], P[(i + 1) % n]
        d = b - a
        if d.length < 1e-9:
            continue
        nin = Vector((-d.y, d.x)) / d.length
        lim = min(lim, (cen - a).dot(nin))
    c = max(0.0, min(c, 0.45 * lim))
    lines = []
    for i in range(n):
        a, b = P[i], P[(i + 1) % n]
        d = (b - a) / max((b - a).length, 1e-9)
        lines.append((a + Vector((-d.y, d.x)) * c, d))
    out = []
    for i in range(n):
        p0, d0 = lines[i - 1]
        p1, d1 = lines[i]
        den = d0.x * d1.y - d0.y * d1.x
        if abs(den) < 1e-9:
            out.append(p1)
        else:
            w = p1 - p0
            out.append(p0 + d0 * ((w.x * d1.y - w.y * d1.x) / den))
    return out


def _cut_block(mb, poly, yf, yb, c=0.022, cy=0.014, bevel_back=True, hide=(), mat=0, joints=None, strips=None):
    """A cut stone: the convex counter-clockwise (x, z) polygon `poly` (seen from the front, -Y) extruded along Y from yf (front) to
    yb. The edges of the front face (and of the back face when bevel_back) are chamfered by c (in the plane) x cy (in depth), so two
    stones that touch exactly leave a closed V-groove for a joint: never a see-through gap, never a sliver. hide = polygon edge
    indices whose side face is left out (it coincides with a neighbour's side face and can never be seen). joints = the edges that are
    JOINTS with a neighbouring stone (default = hide): their chamfer faces carry the mortar UV (tag 1, see _write_uv), so a joint is
    mortar-coloured whatever the stone texture does; free arrises keep the stone texture. strips = {edge: width}: the side face of that edge gets a
    mortar-UV STRIP of `width` metres along its front (and back) edge (the step between a proud ring and the recessed masonry beside it: a lit, textured
    step face seen at a grazing angle is a pale 1 px line; as mortar it is a dark joint line). Faces point outward."""
    n = len(poly)
    if joints is None:
        joints = hide
    inner = _inset_poly(poly, c)
    f_in = [mb.v((q.x, yf, q.y)) for q in inner]
    f_out = [mb.v((q[0], yf + cy, q[1])) for q in poly]
    if bevel_back:
        b_out = [mb.v((q[0], yb - cy, q[1])) for q in poly]
        b_in = [mb.v((q.x, yb, q.y)) for q in inner]
    else:
        b_out = [mb.v((q[0], yb, q[1])) for q in poly]
    mb.f(f_in, mat)
    for i in range(n):
        j = (i + 1) % n
        mb.f((f_out[i], f_out[j], f_in[j], f_in[i]), mat, 1 if i in joints else 0)
    strips = strips or {}
    for i in range(n):
        if i in hide:
            continue
        j = (i + 1) % n
        if i in strips:
            sw = min(strips[i], 0.45 * (yb - yf))
            fo, bo = mb.V[f_out[i]], mb.V[f_out[j]]
            fs = [mb.v((mb.V[f_out[k]][0], mb.V[f_out[k]][1] + sw, mb.V[f_out[k]][2])) for k in (i, j)]
            bs = [mb.v((mb.V[b_out[k]][0], mb.V[b_out[k]][1] - sw, mb.V[b_out[k]][2])) for k in (i, j)]
            mb.f((f_out[i], fs[0], fs[1], f_out[j]), mat, 1)                 # front strip (mortar UV)
            mb.f((fs[0], bs[0], bs[1], fs[1]), mat)                           # the rest of the side face (buried behind the recessed masonry)
            mb.f((bs[0], b_out[i], b_out[j], bs[1]), mat, 1)                 # back strip
        else:
            mb.f((f_out[i], b_out[i], b_out[j], f_out[j]), mat)
    if bevel_back:
        for i in range(n):
            j = (i + 1) % n
            mb.f((b_out[i], b_in[i], b_in[j], b_out[j]), mat, 1 if i in joints else 0)
        mb.f(b_in[::-1], mat)
    else:
        mb.f(b_out[::-1], mat)


HAUNCH_RECESS = 0.02            # the haunch / spandrel masonry stands this far behind the front (and back) face of the ring: the ring's extrados is an ordinary V-groove joint
#                                 (round 3: it was 0.12 m = a 12 cm deep step whose mortar-UV strip read as a long brown rope across the pier face, review 3). 2 cm = the depth of the ring's front chamfer
#                                 (cy 0.02): the haunch front plane meets the chamfer's outer edge, so the boundary is ONE 3 cm bevel (0.36 px at 17 m) and the part of the haunch that
#                                 overlaps the ring (up to 0.2 m behind the extrados) stays 2 cm behind the ring's front face (no z-fight).
ARCH_MAX_FACES = 490          # the smoke gate MASONRY_ARCH allows 500 polygons per DRESS_ARCH mesh


def _arch_mesh(name, span, height, depth, seed, broken):
    """Builds the arch (see _arch_mesh_try) with a face budget: the course heights and the running-bond splits are random, so the face count
    varies with the seed / name; up to 12 attempts (a different random stream each) must give <= ARCH_MAX_FACES, else the stones lose the chamfer
    on their back face (half the faces) and the smallest build wins. Deterministic for a given (seed, name)."""
    best = None
    for attempt in range(12):
        r = _arch_mesh_try(name, span, height, depth, seed, broken, attempt, True)
        if len(r[0].F) <= ARCH_MAX_FACES:
            return r
        if best is None or len(r[0].F) < len(best[0].F):
            best = r
    for attempt in range(4):
        r = _arch_mesh_try(name, span, height, depth, seed, broken, attempt, False)
        if len(r[0].F) <= ARCH_MAX_FACES:
            return r
        if len(r[0].F) < len(best[0].F):
            best = r
    return best


def _grid_rows(rng, n, choices):
    """Random partition of n grid steps into rows of `choices` steps each (a row is never longer than what is left)."""
    rows, rem = [], n
    while rem > 0:
        k = min(rng.choice(choices), rem)
        rows.append(k)
        rem -= k
    return rows


def _arch_mesh_try(name, span, height, depth, seed, broken, attempt, back_bevel):
    """Block arch. Round 2026-10-04 #1 (review: dark slivers + notched edge): every stone is an exact-fit cut block (_cut_block), piers are courses that tile the pier
    section exactly, the ring is `n` wedges that share their radial faces, joints are V-grooves (chamfered edges), nothing is see-through and nothing is a thin quad.
    Round #2 (review: hairline slivers across the arch face): (a) the wedges are FLUSH with each other and with the piers (equal neighbours: the round-1 build drew the
    extrados radius and the front offset per wedge and hid the shared side faces, so a 0-9 mm step between two wedges left a hollow slot an oblique ray entered: gate
    ARCH_NO_OBLIQUE_LEAK); (b) every joint chamfer carries the MORTAR UV (a joint is a dark mortar line, not a pale / dark lighting scratch whose colour depends on the sun
    angle); (c) the courses of the piers and the rows of the haunch lie on the MORTAR GRID of Masonry_C (measured, masonry_grid): the joint plane of the geometry is
    the painted mortar row, one bold joint instead of an extra line through a painted stone (the mesh carries lk_uv_voff so that v = (z - voff) / tile puts a mortar row on the
    spring line)."""
    rng = random.Random(_seed_int("arch", seed, name, attempt))
    mb = _MB(("LK_MASONRY",))
    pitch, phase, _vrow, _depth = masonry_grid()
    t = min(max(0.16 * span, 0.75), 1.3)
    r_in = span / 2.0
    r_out = r_in + t
    key = 0.22 * t
    zs = height - r_out - key
    voff = zs - phase                                       # a mortar row centre on the spring line z = zs (and every pitch above / below it)
    pw = t * 1.18
    y0, y1 = -depth / 2, depth / 2
    n_grid = max(2, int((zs - 0.30) / pitch))               # pier courses fill n_grid mortar pitches below the spring line
    plinth_h = zs - n_grid * pitch                          # 0.30 .. 0.30 + pitch
    vines = []
    big = max(1.0, span / 8.0) >= 1.15
    course_choices = (1, 2, 2, 2, 3) if big else (1, 2, 2, 2)
    for side in (-1, 1):
        xin = side * r_in
        xi, xo = (r_in, r_in + pw) if side > 0 else (-(r_in + pw), -r_in)            # pier section [xi, xo]
        top_cut = zs
        n_rows = n_grid
        if broken and side < 0:                                                    # the left pier lost its top courses
            n_rows = max(1, n_grid - rng.choice((2, 3)))
            top_cut = plinth_h + n_rows * pitch
        # plinth: wider, proud, the pier stands on it
        pl = [(xi - 0.15, 0.0), (xo + 0.15, 0.0), (xo + 0.15, plinth_h), (xi - 0.15, plinth_h)]
        _cut_block(mb, pl, y0 - 0.12, y1 + 0.12, c=0.04, cy=0.026, hide=(0,), bevel_back=back_bevel, joints=())
        z = plinth_h
        k = 0
        rows = _grid_rows(rng, n_rows, course_choices)
        for ri, nr in enumerate(rows):
            ch = nr * pitch
            za, zb = z, z + ch
            last = ri == len(rows) - 1
            blocks = []                                                            # (xa, xb, hide-left, hide-right)
            if k % 2 == 0:
                blocks.append((xi, xo, False, False))
            else:
                w1 = pw * rng.uniform(0.4, 0.6)
                if side > 0:
                    blocks += [(xi, xi + w1, False, True), (xi + w1, xo, True, False)]
                else:
                    blocks += [(xi, xo - w1, False, True), (xo - w1, xo, True, False)]
            for (xa, xb, hl, hr) in blocks:
                ta, tb = zb, zb
                if last and broken and side < 0:                                   # a broken top: each block tilts / is shorter
                    ta = zb - rng.uniform(0.0, 0.35) * ch
                    tb = zb - rng.uniform(0.0, 0.35) * ch
                poly = [(xa, za), (xb, za), (xb, tb), (xa, ta)]
                hide = {0}
                if not last:
                    hide.add(2)
                joints = {0} | ({2} if not last else set())
                if not (last and broken and side < 0):                             # a broken top: the two blocks end at different heights, their shared side face is NOT hidden
                    if hl:
                        hide.add(3)
                    if hr:
                        hide.add(1)
                if hl:
                    joints.add(3)
                if hr:
                    joints.add(1)
                _cut_block(mb, poly, y0, y1, c=0.024, cy=0.015, hide=tuple(hide), bevel_back=back_bevel, joints=tuple(joints))
            z = zb
            k += 1
        vines.append(((xin + side * pw * 0.5, y0 - 0.03, top_cut - 0.1), (0.0, -1.0)))
        vines.append(((xin + side * pw * 0.5, y1 + 0.03, top_cut - 0.1), (0.0, 1.0)))
    # ring: n wedges that share their radial faces (no gap); odd n so that a keystone sits on the crown
    n = int(round(math.pi * (r_in + t / 2) / max(0.7, 0.12 * span)))
    n += 1 - n % 2
    mid = n // 2
    missing = set(range(mid - 1, n)) if broken else set()     # broken: the crown and the whole left half fell
    for i in range(n):
        if i in missing:
            continue
        a0 = math.pi * i / n
        a1 = math.pi * (i + 1) / n
        is_key = (i == mid)
        # every NON-key wedge: the same extrados radius, flush with the piers (see the docstring); the keystone is larger than both neighbours and keeps its side faces
        ro = r_out + (key if is_key else 0.0)
        proud = 0.10 if is_key else 0.0

        def pt(r, a):
            return (r * math.cos(a), zs + r * math.sin(a))
        poly = [pt(r_in, a0), pt(ro, a0), pt(ro, a1), pt(r_in, a1)]
        hide = set()
        if not is_key:
            hide.add(0)
            if (i + 1) < n and (i + 1) not in missing and (i + 1) != mid:
                hide.add(2)
            if i + 1 == mid:
                hide.add(2)
        joints = {0, 1, 2}                                     # the extrados edge too: the step between the ring and the recessed haunch is a mortar line, not a pale lit chamfer
        if i == 0:
            joints.discard(0)                                  # the spring: the wedge sits on the pier top, a free edge of the ring (the pier's own top chamfer is the joint)
        if i == n - 1 or (i + 1) in missing:
            joints.discard(2)
        _cut_block(mb, poly, y0 - proud, y1 + proud, c=0.03, cy=0.02, hide=tuple(hide), bevel_back=back_bevel, joints=tuple(joints),
                   strips=None if is_key else {1: HAUNCH_RECESS + 0.015})
        am = (a0 + a1) / 2
        if i % 2 == 1 and not (broken and i > mid):
            vines.append(((math.cos(am) * r_in, y0 - 0.04, zs + math.sin(am) * r_in), (0.0, -1.0)))
            vines.append(((math.cos(am) * r_out, y1 + 0.04, zs + math.sin(am) * r_out), (0.0, 1.0)))
    # haunch / spandrel: stepped courses outside the ring (a staircase of whole blocks, each row a little narrower than the one
    # below), HAUNCH_RECESS (2 cm = the ring's chamfer depth) behind the ring's front so the ring / haunch boundary is one V-groove like every other joint (round 3: a 12 cm step
    # read as a long brown rope across the pier face); every row block reaches inside the extrados.
    # The rows are whole mortar pitches (the spring line is on the grid), so every row joint is a painted mortar row.
    n_ring_faces = len(mb.F)
    for side in (-1, 1):
        if broken and side < 0:
            continue
        x_outer = r_in + pw
        top_rel = 0.80 * r_out if side > 0 else 0.74 * r_out
        n_h = max(2, int(round(top_rel / pitch)))
        rows = _grid_rows(rng, n_h, (1, 2, 2) if not big else (2, 2, 3))
        zbs = [zs]
        for nr in rows:
            zbs.append(zbs[-1] + nr * pitch)
        # round 2026-10-05 (review: 'Minecraft staircase on the pier tops'): the courses end on a circle CONCENTRIC with the arch (radius = the pier's outer face), each course's
        # outer face SLANTED from the circle point at its bottom to the one at its top (no horizontal step ledges), with 5-15 cm random chips per course and a dropped, chipped
        # top corner: the shoulder reads as a rounded broken arch, not as pixel stairs. Same number of blocks / faces as the stair version (4-gon courses).
        jit = [0.0] + [rng.uniform(-0.15, 0.15) * (0.4 + 0.6 * min(1.0, j / max(1, len(rows) - 1))) for j in range(len(rows))]

        def arcx(z, jj):
            return math.sqrt(max(x_outer * x_outer - (z - zs) ** 2, 0.0)) + jj
        for j, nr in enumerate(rows):
            za, zb = zbs[j], zbs[j + 1]
            last = j == len(rows) - 1
            dzt = min(zb - zs, r_out - 0.01)
            x_t = math.sqrt(max(r_out * r_out - dzt * dzt, 0.0)) - 0.12              # inside the extrados at the row's top
            xb = max(arcx(za, jit[j]), x_t + 0.30)
            xt = max(arcx(zb, jit[j + 1]) - (rng.uniform(0.15, 0.35) if last else 0.0), x_t + 0.25)
            ztop = zb - (rng.uniform(0.05, 0.15) if last else 0.0)                    # the top course's outer corner is chipped down
            if side > 0:
                poly = [(x_t, za), (xb, za), (xt, ztop), (x_t, zb)]
            else:
                poly = [(-xb, za), (-x_t, za), (-x_t, zb), (-xt, ztop)]
            _cut_block(mb, poly, y0 + HAUNCH_RECESS, y1 - HAUNCH_RECESS, c=0.022, cy=0.014, bevel_back=False, hide=(0,), joints=(0, 2) if j < len(rows) - 1 else (0,))
    parts = dict(ring_and_piers=n_ring_faces, haunch=len(mb.F) - n_ring_faces)
    return mb, vines, dict(r_in=r_in, r_out=r_out, zs=zs, pw=pw, t=t, parts=parts, voff=voff, plinth_h=plinth_h, pitch=pitch)


def build_masonry_arch(D, center, facing_deg, span_m=7.0, height_m=10.0, depth_m=None, broken=False, seed=0, base_rock=True,
                       vines=True, name=None, vine_seed=None):
    """A real block arch (visual only): two piers of cut blocks on a plinth, a round voussoir ring (flush wedges that share their radial faces, joints =
    V-grooves painted as mortar) with a proud keystone, haunch blocks 2 cm behind the ring face (HAUNCH_RECESS: the ring's extrados is an ordinary joint, no step); the piers' courses and the haunch
    rows lie on the mortar rows of Masonry_C (masonry_grid(), mesh property lk_uv_voff); broken=True drops the crown voussoirs and the left haunch.
    center = (x, y, z_base) metres (z_base = pier foot; a 2-tuple uses D.play_z); facing_deg = direction you look THROUGH the
    opening, clockwise from +Y (postcard_lib.build_arch convention). base_rock places ROCK_SLAB_A / ROCK_BLOCK_A instances
    under the piers (their tops at z_base); vines hangs PLANT_VINE_* from the returned vine points; fallen DRESS_BLOCK_*
    lie at the feet. Returns dict(arch, base, vines, fallen, vine_points, dims)."""
    if len(center) == 2:
        center = (center[0], center[1], D.play_z if D is not None else 0.0)
    depth_m = depth_m or max(1.0, 0.17 * span_m)
    name = name or _new_name("DRESS_ARCH", "{}_{:02d}")
    mb, vpts, dims = _arch_mesh(name, span_m, height_m, depth_m, seed, broken)
    me = _finalize(name, mb, "box", smooth_angle=None, library=False, uv_voff=dims.get("voff", 0.0))
    ob = bpy.data.objects.new(name, me)
    _collection_for("DRESS_ARCH").objects.link(ob)
    _parent(D, ob)
    phi = math.radians(facing_deg)
    yaw = -phi                              # local +Y (through the opening) -> facing direction
    ob.location = Vector(center)
    ob.rotation_euler = (0.0, 0.0, yaw)
    M = Matrix.Translation(Vector(center)) @ Matrix.Rotation(yaw, 4, 'Z')
    R3 = Matrix.Rotation(yaw, 3, 'Z')
    rng = random.Random(_seed_int("archprops", seed, name))
    res = dict(arch=ob, base=[], vines=[], fallen=[], dims=dims)
    world_vines = []
    for p, nrm in vpts:
        w = M @ Vector(p)
        nv = R3 @ Vector((nrm[0], nrm[1], 0.0))
        world_vines.append(((w.x, w.y, w.z), (nv.x, nv.y)))
    res["vine_points"] = world_vines
    if base_rock:
        for side in (-1, 1):
            xc = side * (dims["r_in"] + dims["pw"] / 2)
            for j, (kind, dx, dy, s) in enumerate((("ROCK_SLAB_A", 0.0, 0.0, 1.0), ("ROCK_BLOCK_A", 0.35, 0.55, 0.8))):
                mn, mx = lib_bbox(kind)
                sc = max(dims["pw"] + 1.2, depth_m + 1.4) / max(mx.x - mn.x, mx.y - mn.y) * s
                local = Vector((xc + side * dx, dy, 0.0))
                wp = M @ local
                top = mx.z * sc
                ob_r = place(D, kind, (wp.x, wp.y, center[2] - top + 0.06 - 0.3 * j), yaw + rng.uniform(-0.3, 0.3), sc)
                res["base"].append(ob_r)
    if vines:
        res["vines"] = hang_vines(D, world_vines, seed=vine_seed if vine_seed is not None else seed,
                                  max_drop=lambda p: p[2] - center[2] - 0.3)
    nf = 4 if broken else 2
    for i in range(nf):
        a = rng.uniform(0, math.tau)
        rr = rng.uniform(dims["r_in"] * 0.6, dims["r_in"] + dims["pw"] + 1.5)
        local = Vector((math.cos(a) * rr * (-1 if broken else 1), math.sin(a) * rr * 0.6, 0.0))
        wp = M @ local
        k = rng.choice(("DRESS_BLOCK_A", "DRESS_BLOCK_B"))
        res["fallen"].append(place(D, k, (wp.x, wp.y, center[2] - 0.12), rng.uniform(0, math.tau), rng.uniform(0.8, 1.1),
                                   (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25))))
    return res


# ----------------------------------------------------------------------------- ruin
def add_cut_stone(mb, cx, cy, cz, sx, sy, sz, rng, yaw=0.0, broken=False):
    """Add a genuinely chamfered cut block to an _MB, in world metres.

    Unequal clipped top corners produce a broken arris rather than an eight-
    vertex box. The lower edge is planar, so courses and fallen stones can be
    grounded exactly. Mortar bevels use the existing measured mortar-row UV.
    Returns the number of faces added; does not touch a library mesh.
    """
    x0, x1, z0, z1 = -sx / 2, sx / 2, -sz / 2, sz / 2
    cut0 = min(sx * .18, sz * rng.uniform(.10, .25))
    cut1 = min(sx * .14, sz * rng.uniform(.07, .18))
    if broken:
        cut0 = min(sx * .30, sz * rng.uniform(.25, .45))
    poly = [(x0, z0), (x1, z0), (x1, z1 - cut1),
            (x1 - cut1, z1), (x0 + cut0, z1 - (sz * .07 if broken else 0)),
            (x0, z1 - cut0)]
    vi, fi = len(mb.V), len(mb.F)
    _cut_block(mb, poly, -sy / 2, sy / 2, c=min(.045, .08 * sz),
               cy=min(.025, .06 * sy), bevel_back=False, joints=(0, 1, 5))
    cs, sn = math.cos(yaw), math.sin(yaw)
    for i in range(vi, len(mb.V)):
        x, y, z = mb.V[i]
        mb.V[i] = (cx + cs * x - sn * y, cy + sn * x + cs * y, cz + z)
    return len(mb.F) - fi


def build_ruin(D, path, height_m=4.0, thickness_m=1.0, doorway_at=0.45, doorway_w=1.6, doorway_h=2.3, seed=0, closed=False,
               clip=None, base_z=None, name=None, fallen=True, course_h=0.62, block_l=(0.9, 1.5), max_faces=500):
    """Broken stone masonry (visual only) along a polyline `path` [(x, y), ...] metres: coursed cut blocks in running bond,
    a jagged broken height profile (full stretches, stubs and collapsed gaps), a doorway (jambs + lintel) at fraction
    doorway_at of the length (None = no doorway), fallen DRESS_BLOCK_* instances beside the collapsed parts. clip(x, y) ->
    bool skips blocks whose centre fails (e.g. lambda x, y: on land). Unique meshes DRESS_RUIN_nn_00, _01 ... (<= max_faces
    each). Returns dict(walls=[objs], fallen=[objs])."""
    rng = random.Random(_seed_int("ruin", seed))
    pts = [Vector((p[0], p[1], 0.0)) for p in path]
    if closed:
        pts.append(pts[0].copy())
    base_z = (D.play_z if D is not None else 0.0) if base_z is None else base_z
    name = name or _new_name("DRESS_RUIN", "{}_{:02d}")
    seglens = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]
    total = sum(seglens)
    ctrl_n = max(4, int(total / 2.2))
    ctrl = []
    for i in range(ctrl_n + 1):
        r = rng.random()
        if r < 0.45:
            v = rng.uniform(0.8, 1.0)
        elif r < 0.75:
            v = rng.uniform(0.45, 0.72)
        elif r < 0.9:
            v = rng.uniform(0.15, 0.32)
        else:
            v = 0.0
        ctrl.append(v)
    ctrl[0] = min(ctrl[0], 0.45)
    ctrl[-1] = min(ctrl[-1], 0.45)
    s_door = None if doorway_at is None else doorway_at * total
    n_door = max(2, int(round(doorway_h / course_h)))       # the opening is n_door courses tall, the lintel is course n_door
    door_top = n_door * course_h

    def prof(s):
        f = s / total * ctrl_n
        i = min(int(f), ctrl_n - 1)
        t = f - i
        v = ctrl[i] * (1 - t) + ctrl[i + 1] * t
        h = height_m * v
        if s_door is not None and abs(s - s_door) < doorway_w / 2 + 1.4:
            h = max(h, door_top + course_h * 1.6)
        return h
    walls, fallen_spots = [], []
    mb = _MB(("LK_MASONRY",))
    part = [0]

    def flush():
        if mb.F:
            me = _finalize(f"{name}_{part[0]:02d}", mb, "box", smooth_angle=None, library=False)
            ob = bpy.data.objects.new(f"{name}_{part[0]:02d}", me)
            _collection_for("DRESS_RUIN").objects.link(ob)
            _parent(D, ob)
            walls.append(ob)
            part[0] += 1
    s_acc = 0.0
    for si in range(len(pts) - 1):
        a, b = pts[si], pts[si + 1]
        L = seglens[si]
        if L < 0.3:
            s_acc += L
            continue
        d = (b - a) / L
        yaw = math.atan2(d.y, d.x)
        start = thickness_m * 0.5 if si > 0 else 0.0
        nc = int(math.ceil(height_m / course_h)) + 1
        for c in range(nc):
            z0 = c * course_h
            s = start - (block_l[0] * 0.5 if c % 2 else 0.0)
            while s < L - 0.05:
                bl = rng.uniform(*block_l)
                s0, s1 = max(s, start if c % 2 == 0 else 0.0), min(s + bl, L)
                s = s + bl
                if s1 - s0 < 0.35:
                    continue
                sm = (s0 + s1) / 2
                sg = s_acc + sm
                h = prof(sg)
                if z0 + course_h * rng.uniform(0.35, 0.75) > h:
                    if z0 < h + course_h * 2 and rng.random() < 0.5:
                        fallen_spots.append((a + d * sm, yaw))
                    continue
                if s_door is not None and c <= n_door and abs(sg - s_door) < doorway_w / 2 + (0.35 if c == n_door else 0.0):
                    continue
                cpos = a + d * sm
                if clip is not None and not clip(cpos.x, cpos.y):
                    continue
                if len(mb.F) + 22 > max_faces:
                    flush()
                    mb.__init__(("LK_MASONRY",))
                th = thickness_m * rng.uniform(0.94, 1.04)
                add_cut_stone(mb, cpos.x, cpos.y, base_z + z0 + course_h / 2 - 0.08, s1 - s0 - 0.05, th, course_h, rng,
                              yaw + rng.uniform(-0.025, 0.025), broken=z0 + 1.5 * course_h >= h)
        if s_door is not None and s_acc <= s_door <= s_acc + L:
            sm = s_door - s_acc
            cpos = a + d * sm
            if prof(s_door) >= door_top + course_h * 0.9:
                if len(mb.F) + 22 > max_faces:
                    flush()
                    mb.__init__(("LK_MASONRY",))
                add_cut_stone(mb, cpos.x, cpos.y, base_z + door_top + course_h / 2 - 0.08, doorway_w + 0.7, thickness_m * 1.05,
                              course_h, rng, yaw)
        s_acc += L
    flush()
    fobs = []
    if fallen:
        rng.shuffle(fallen_spots)
        for (p, yaw) in fallen_spots[:max(2, min(14, len(fallen_spots) // 3))]:
            side = rng.choice((-1, 1))
            off = Vector((-math.sin(yaw), math.cos(yaw), 0.0)) * side * rng.uniform(0.9, 2.6)
            q = p + off + Vector((math.cos(yaw), math.sin(yaw), 0)) * rng.uniform(-0.8, 0.8)
            if clip is not None and not clip(q.x, q.y):
                continue
            k = rng.choice(("DRESS_BLOCK_A", "DRESS_BLOCK_B"))
            fobs.append(place(D, k, (q.x, q.y, base_z - 0.1), rng.uniform(0, math.tau), rng.uniform(0.7, 1.0),
                              (rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3))))
    return dict(walls=walls, fallen=fobs)


# ----------------------------------------------------------------------------- vines
def hang_vines(D, points, seed=0, max_drop=None, kinds=VINE_KINDS, weights=(3, 3, 2), skip=0.0):
    """Hang PLANT_VINE_* instances from attachment points [((x, y, z), (nx, ny)), ...]: the vine's +Y bulge points along
    (nx, ny) (away from the wall), length picked at random (weights) but never longer than max_drop (float metres, or a
    callable(point) -> metres; default down to z 0.3). skip = chance to leave a point empty. Returns [objs]."""
    rng = random.Random(_seed_int("vines", seed, len(points)))
    out = []
    for (p, nrm) in points:
        if skip and rng.random() < skip:
            continue
        drop = max_drop(p) if callable(max_drop) else (max_drop if max_drop is not None else p[2] - 0.3)
        options = [(k, w) for k, w in zip(kinds, weights) if VINE_LENGTH[k] * 0.8 <= drop]
        if not options:
            continue
        tot = sum(w for _k, w in options)
        r = rng.uniform(0, tot)
        k = options[-1][0]
        for kk, w in options:
            r -= w
            if r <= 0:
                k = kk
                break
        sz = min(rng.uniform(0.8, 1.2), drop / VINE_LENGTH[k])
        yaw = math.atan2(nrm[1], nrm[0]) - math.pi / 2       # local +Y -> (nx, ny)
        loc = (p[0] + nrm[0] * 0.02, p[1] + nrm[1] * 0.02, p[2])
        out.append(place(D, k, loc, yaw, (rng.uniform(0.85, 1.15), 1.0, sz)))
    return out


def cliff_lip_points(D, spacing_m=6.0, seed=0, masks=(), z=None, jitter=0.35, out_m=0.04):
    """Attachment points along every terrain boundary loop (the cliff lip, D.loops; land on the left), spacing_m apart with
    jitter, z = D.play_z - 0.02 (or z). Filter with masks (callables on x, y). Returns [((x, y, z), (nx, ny)), ...] with
    (nx, ny) the outward horizontal normal."""
    rng = random.Random(_seed_int("lip", seed, getattr(D, "number", 0)))
    zz = (D.play_z - 0.02) if z is None else z
    out = []
    for L in D.loops:
        Pn = np.asarray(L, float)
        n = len(Pn)
        seg = np.hypot(*(np.roll(Pn, -1, 0) - Pn).T)
        cum = np.concatenate([[0], np.cumsum(seg)])
        k = int(cum[-1] / spacing_m)
        for i in range(k):
            s = (i + 0.5 + rng.uniform(-jitter, jitter)) * cum[-1] / k
            j = min(int(np.searchsorted(cum, s, side="right") - 1), n - 1)
            t = (s - cum[j]) / max(seg[j], 1e-9)
            a, b = Pn[j], Pn[(j + 1) % n]
            p = a + (b - a) * t
            d = (b - a) / max(seg[j], 1e-9)
            nrm = (d[1], -d[0])
            q = (p[0] + nrm[0] * out_m, p[1] + nrm[1] * out_m)
            ok = True
            for m in masks:
                ok = ok and bool(np.asarray(m(np.array([p[0]]), np.array([p[1]])))[0])
            if ok:
                out.append(((float(q[0]), float(q[1]), zz), (float(nrm[0]), float(nrm[1]))))
    return out


def hang_vines_on_cliff(D, spacing_m=6.0, seed=0, masks=(), skip=0.25):
    """cliff_lip_points + hang_vines in one call (vines down the cliff walls, max drop = the play height above the water)."""
    pts = cliff_lip_points(D, spacing_m, seed, masks)
    return hang_vines(D, pts, seed=seed, max_drop=lambda p: p[2] - 0.4, skip=skip)


TUFT_CHEAP_SWAP = {"PLANT_TUFT_A": "PLANT_TUFT_S", "PLANT_TUFT_B": "PLANT_TUFT_S", "PLANT_TUFT_C": "PLANT_TUFT_T"}


def cheapen_tufts(D=None, near_m=60.0, focus=None, objs=None, swap=None):
    """PAY FOR THE FRINGE (v2 2026-10-05 budget: <= 250k triangles per hole): swap every old 117-triangle PLANT_TUFT_A / _B / _C instance that stands farther than near_m
    (60 m) from every focus point (default: the shot balls of the stills configs, still_ball_zones(D)) for the 36-triangle filler (A, B -> PLANT_TUFT_S, C -> PLANT_TUFT_T, same
    object, transform and scale; the object is renamed after its new kind). Near the cameras the full tufts stay. Returns dict(swapped, kept, tris_saved). The fillers are a
    third of the cost, about the same height (0.37 / 0.35 m against 0.39 / 0.43 / 0.37) and the same palette family."""
    swap = swap or TUFT_CHEAP_SWAP
    objs = objs if objs is not None else [o for o in bpy.data.objects if o.type == 'MESH' and o.data is not None and o.data.name in swap
                                          and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    if focus is None:
        focus = [(z[0], z[1]) for z in still_ball_zones(D)] if D is not None else []
    fo = np.asarray(focus, float).reshape(-1, 2) if len(focus) else np.zeros((0, 2))
    bpy.context.view_layer.update()
    swapped, kept, saved = 0, 0, 0
    for ob in objs:
        old = ob.data.name
        if old not in swap:
            continue
        loc = ob.matrix_world.translation
        d = float(np.hypot(fo[:, 0] - loc.x, fo[:, 1] - loc.y).min()) if len(fo) else 1e9
        if d <= near_m:
            kept += 1
            continue
        new = swap[old]
        lib_old, lib_new = library_object(old).data, library_object(new).data
        lib_old.calc_loop_triangles()
        lib_new.calc_loop_triangles()
        saved += len(lib_old.loop_triangles) - len(lib_new.loop_triangles)
        ob.data = lib_new
        ob.name = _new_name(new)
        ob["lk_cheapened_from"] = old
        swapped += 1
    return dict(swapped=swapped, kept=kept, tris_saved=saved)


def _world_tri_soup(objs):
    """(vertices (n, 3) list, triangles list) of mesh objects in world space (loop triangles, so n-gons are triangulated properly)."""
    bpy.context.view_layer.update()                       # objects just created / moved have a stale matrix_world until the depsgraph runs
    V, F = [], []
    for o in objs:
        co, tris, _tp = _np_mesh(o.data)
        if len(tris) == 0:
            continue
        mw = np.array(o.matrix_world)
        w = co @ mw[:3, :3].T + mw[:3, 3]
        base = len(V)
        V.extend(map(tuple, w.tolist()))
        F.extend(tuple(t) for t in (tris + base).tolist())
    return V, F


VINE_ATTACH_PREFIXES = ("ROCK_", "DRESS_ARCH", "DRESS_RUIN", "DRESS_BLOCK", "TERRAIN", "FAIRWAY", "GREEN")


def vine_root_gaps(vines=None, targets=None, prefixes=VINE_ATTACH_PREFIXES):
    """[(vine object, gap m)]: the distance from each vine's ROOT (its origin = the top attachment point) to the nearest surface of the scene objects whose name
    starts with `prefixes` (never PLANT_ or the library) or of the given `targets`. Gate numbers of attach_vines (VINE_ROOT_GAP: every gap <= 5 cm)."""
    vines = vines if vines is not None else [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("PLANT_VINE")
                                             and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    objs = targets if targets is not None else [o for o in bpy.data.objects if o.type == 'MESH' and o.data is not None and o.name.startswith(tuple(prefixes))
                                                 and not o.name.startswith("PLANT_") and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    V, F = _world_tri_soup(objs)
    if not F:
        return [(v, float("inf")) for v in vines]
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromPolygons(V, F)
    out = []
    for v in vines:
        hit = bvh.find_nearest(v.matrix_world.translation)
        out.append((v, float(hit[3]) if hit[0] is not None else float("inf")))
    return out


def attach_vines(D, vines=None, targets=None, max_gap_m=0.05, reach_m=1.0, delete=True, prefixes=VINE_ATTACH_PREFIXES):
    """Make every hanging vine TOUCH a surface (review 3: 8 of 131 vines hung 0.6-0.86 m off the wall). For each vine (default: every PLANT_VINE* in the scene) the nearest
    point of the target meshes (ROCK_*, DRESS_ARCH / RUIN / BLOCK, TERRAIN, FAIRWAY, GREEN, or `targets`) is found: within max_gap_m (5 cm) the vine stays; within
    reach_m (1.0 m) its root moves onto that surface point (the bulge +Y turns onto the horizontal part of the surface normal, unless the surface faces up / down:
    then the yaw stays); farther away it is DELETED (delete=True) or only reported. Call it after the last rock / skin / arch / vine of the hole (the hole scripts do).
    Returns dict(n, ok, moved, deleted, left, gap_before_max, gap_after_max, gaps_after)."""
    gaps = vine_root_gaps(vines, targets, prefixes)
    objs = targets if targets is not None else [o for o in bpy.data.objects if o.type == 'MESH' and o.data is not None and o.name.startswith(tuple(prefixes))
                                                 and not o.name.startswith("PLANT_") and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    V, F = _world_tri_soup(objs)
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromPolygons(V, F) if F else None
    res = dict(n=len(gaps), ok=0, moved=0, deleted=0, left=0, gap_before_max=max([g for _v, g in gaps if math.isfinite(g)] or [0.0]), gaps_after=[])
    for v, g in gaps:
        if g <= max_gap_m:
            res["ok"] += 1
            res["gaps_after"].append(g)
            continue
        hit = bvh.find_nearest(v.matrix_world.translation) if bvh is not None else (None,) * 4
        if hit[0] is not None and g <= reach_m:
            loc, nrm = Vector(hit[0]), Vector(hit[1])
            v.location = loc + nrm * 0.005
            if abs(nrm.z) < 0.7:
                v.rotation_euler.z = math.atan2(nrm.y, nrm.x) - math.pi / 2
            res["moved"] += 1
            res["gaps_after"].append(0.005)
        elif delete:
            data = v.data
            bpy.data.objects.remove(v, do_unlink=True)
            res["deleted"] += 1
        else:
            res["left"] += 1
            res["gaps_after"].append(g)
    res["gap_after_max"] = max(res["gaps_after"] or [0.0])
    bpy.context.view_layer.update()
    return res


def tuftify_shrubs(D, shrubs=None, per=(4, 8), kinds=None, seed=0, tri_budget=None, lean_deg=12.0, height_range=(0.75, 1.25), top_cap_m=FRINGE_TOP_CAP_M, top_only_m=None):
    """Break the smooth lumps of the PLANT_SHRUB_* instances into TUFTY, irregular clumps (review 3, hole 9: 'the bushes are still smooth stacked balls'): per shrub
    per = (4, 8) olive / straw tufts (kinds, default PLANT_TUFT_T / _T / _F: the shrubs' own olive palette, never the bright greens) stand ON the top and rim of the shrub
    (their base is the ray-cast surface of the shrub's own lumps, sunk 4 cm into it, so no floating blade) and lean outward <= lean_deg, at height_range (0.75-1.25) x their
    library height, so the blades poke out of the smooth lumps. Deterministic (seed). tri_budget stops the call. top_only_m (metres): only the TOP that deep of each target
    (rays that hit lower than zmax - top_only_m are dropped, the sampling radius is the target's reach inside that band) - tuftify_crowns uses it for the grass crown of a sea
    stack. The target meshes themselves stay byte-identical (any mesh object works, not only PLANT_SHRUB_*). Returns dict(objs, tris, shrubs)."""
    shrubs = shrubs if shrubs is not None else [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("PLANT_SHRUB")
                                                 and not any(c.name == LIB_COLLECTION for c in o.users_collection)]
    kinds = list(kinds or ("PLANT_TUFT_T", "PLANT_TUFT_T", "PLANT_TUFT_F"))
    dims = {k: _lib_dims(k) for k in set(kinds)}
    from mathutils.bvhtree import BVHTree
    rng = random.Random(_seed_int("tuftify", seed, len(shrubs)))
    objs, tris = [], 0
    lean = math.radians(lean_deg)
    for sh in shrubs:
        V, F = _world_tri_soup([sh])
        if not F:
            continue
        bvh = BVHTree.FromPolygons(V, F)
        co = np.array(V)
        ztop = float(co[:, 2].max())
        band = co if top_only_m is None else co[co[:, 2] >= ztop - top_only_m]
        cx, cy = float(band[:, 0].mean()), float(band[:, 1].mean())
        rmax = float(np.hypot(band[:, 0] - cx, band[:, 1] - cy).max())
        n = rng.randint(per[0], per[1])
        got = 0
        for _try in range(n * 4):
            if got >= n:
                break
            a = rng.uniform(0, math.tau)
            r = rmax * math.sqrt(rng.uniform(0.04, 0.8))
            x, y = cx + r * math.cos(a), cy + r * math.sin(a)
            hit = bvh.ray_cast(Vector((x, y, ztop + 2.0)), Vector((0, 0, -1)), 10.0 + (ztop - float(co[:, 2].min())))
            if hit[0] is None or (top_only_m is not None and float(hit[0].z) < ztop - top_only_m):
                continue
            got += 1
            k = kinds[int(rng.random() * len(kinds)) % len(kinds)]
            hh, rf, tr = dims[k]
            if tri_budget is not None and tris + tr > tri_budget:
                return dict(objs=objs, tris=tris, shrubs=len(shrubs))
            f = rng.uniform(*height_range)
            th = min(lean, max(math.radians(3.0), lean * rng.random()))
            ang = math.atan2(y - cy, x - cx) + rng.uniform(-0.5, 0.5)
            ob = place(D, k, (x, y, float(hit[0].z) - 0.04), rng.uniform(0, math.tau), f * rng.uniform(0.9, 1.1), allow_low=True)
            Rm = Matrix.Rotation(th, 3, Vector((-math.sin(ang), math.cos(ang), 0.0))) @ Matrix.Rotation(ob.rotation_euler.z, 3, 'Z')
            ob.rotation_euler = Rm.to_euler()
            ob["lk_shrub_tuft"] = 1
            objs.append(ob)
            tris += tr
    return dict(objs=objs, tris=tris, shrubs=len(shrubs))


def tuftify_crowns(D, stacks, per=(8, 14), kinds=None, seed=0, tri_budget=None, crown_m=2.0, height_range=(1.3, 1.95)):
    """Tufts on the grass crown of sea stacks (review 3: 'sea-stack caps are faceted saturated green hats'): tuftify_shrubs restricted to the top crown_m (2 m) of each
    stack, with the dark green / olive palette of the crown (default PLANT_TUFT_B / _T / _F), per = (8, 14) tufts per stack at 1.3-1.95 x their library size (0.5-0.9 m blades
    on a 2 m dome: shaggy at the 30-50 px tee view, inside the SCALE_LIMITS). The blades break the dome's hard silhouette and hang over its rim. `stacks` =
    the ROCK_SEASTACK_* / ROCK_SPIRE_* instances (place_sea_stack returns them first). Returns dict(objs, tris, shrubs)."""
    spires = [o for o in stacks if o.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE"))]
    return tuftify_shrubs(D, spires, per=per, kinds=kinds or ("PLANT_TUFT_B", "PLANT_TUFT_T", "PLANT_TUFT_F"), seed=seed, tri_budget=tri_budget, height_range=height_range, top_only_m=crown_m)


# ----------------------------------------------------------------------------- waterfall
def _fall_frame(top, out_dir, width_m, foot_z, widen, reach_m):
    o = Vector((out_dir[0], out_dir[1], 0.0))
    o = o.normalized() if o.length > 1e-9 else Vector((0, 1, 0))
    side = Vector((o.y, -o.x, 0.0))
    return Vector(top), o, side


def waterfall_foot(top, out_dir, width_m=3.0, foot_z=0.0, widen=1.7, reach_m=1.6):
    """Where the waterfall sheet built with the same arguments meets foot_z: dict(pos=(x, y, z), width=m, dir=(dx, dy)).
    A3 (postcard_look_lib) lays its WATER_SURF whitewater patch there."""
    t, o, _s = _fall_frame(top, out_dir, width_m, foot_z, widen, reach_m)
    p = t + o * reach_m
    return dict(pos=(p.x, p.y, foot_z), width=width_m * widen, dir=(o.x, o.y))


def build_waterfall_sheet(D, top, out_dir, width_m=3.0, foot_z=0.0, widen=1.7, reach_m=1.6, seed=0, name=None, cols=8,
                          rows=12, ribs=3, bow=0.12):
    """WATER_FALL_nn (LK_FALL, static, no collider): a gently curved sheet leaving the ledge point `top` (x, y, z) along
    out_dir (horizontal), falling to foot_z on a ballistic profile (outward offset = reach_m x sqrt(fall fraction)),
    widening to width x widen at the foot, bowed outward across (bow x width), with `ribs` raised streak columns.
    UV u across 0..1, v 0 at the top .. 1 at the foot. Faces point outward (toward the viewer in front of the cliff)."""
    rng = random.Random(_seed_int("fall", seed))
    t0, o, side = _fall_frame(top, out_dir, width_m, foot_z, widen, reach_m)
    H = t0.z - foot_z
    name = name or _new_name("WATER_FALL", "{}_{:02d}")
    mb = _MB(("LK_FALL",))
    rib_cols = set(rng.sample(range(1, cols), min(ribs, cols - 1)))
    grid = []
    for r in range(rows + 1):
        f = (r / rows) ** 1.35
        z = t0.z - H * f
        out = reach_m * math.sqrt(f)
        w = width_m * (1.0 + (widen - 1.0) * f ** 1.5)
        row = []
        for c in range(cols + 1):
            u = c / cols
            a = (u - 0.5)
            bulge = bow * w * (1.0 - (2 * a) ** 2) + (0.07 * min(1.0, 4 * f) if c in rib_cols else 0.0)
            wav = 0.04 * math.sin(c * 1.9 + r * 0.7 + seed) * f
            p = t0 + side * (a * w) + o * (out + bulge + wav) + Vector((0, 0, z - t0.z))
            row.append(mb.v(p, (u, r / rows)))
        grid.append(row)
    for r in range(rows):
        for c in range(cols):
            a, b, cc, d = grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]
            mb.f((a, d, cc, b))
    me = _finalize(name, mb, "vertex", all_smooth=True, library=False)
    # make sure the faces look out along o (toward the viewer)
    me.calc_loop_triangles()
    nz = sum((Vector(pl.normal).dot(o)) for pl in me.polygons)
    if nz < 0:
        try:
            me.flip_normals()
        except Exception:
            bm = bmesh.new()
            bm.from_mesh(me)
            bmesh.ops.reverse_faces(bm, faces=bm.faces[:], flip_multires=False)
            bm.to_mesh(me)
            bm.free()
    ob = bpy.data.objects.new(name, me)
    _collection_for("WATER_").objects.link(ob)
    _parent(D, ob)
    return ob


# ----------------------------------------------------------------------------- backdrop: cone + smoke
def build_background_cone(D, center, base_r=115.0, height=118.0, seed=0, name="DRESS_CONE", notch_deg=None, sides=28,
                          bands=7, crater_frac=0.2, notch_depth=0.2, crater_depth=0.1):
    """DRESS_CONE (LK_BASALT, visual only): a broad volcano with chunky strata (each band = a steep cliff step + a
    gentler slope, per-band angular jitter), never undercut (every ring radius <= the ring below at the same angle), a
    V-notch in the crater rim at notch_deg (default random) and a shallow crater dish. center = (x, y, z_base); faces <= 500."""
    rng = random.Random(_seed_int("cone", seed, name))
    cx, cy, cz = center
    mb = _MB(("LK_BASALT",))
    notch = math.radians(notch_deg if notch_deg is not None else rng.uniform(0, 360))
    angs = [math.tau * i / sides + rng.uniform(-0.25, 0.25) * math.tau / sides for i in range(sides)]
    rim_r = base_r * crater_frac
    rings = []
    prev = [float("inf")] * sides
    levels = []
    for b in range(bands):
        z0 = height * (b / bands)
        z1 = height * ((b + 1) / bands)
        levels.append((z0, 0, z0))
        levels.append((z0 + (z1 - z0) * rng.uniform(0.38, 0.5), 1, z0))
    levels.append((height, 2, height))
    jit = [[1.0 + rng.uniform(-0.11, 0.11) for _ in range(sides)] for _ in range(bands + 1)]
    prev_z = [-1e9] * sides
    for li, (z, kind, zband) in enumerate(levels):
        tz = z / height
        r_prof = rim_r + (base_r - rim_r) * (1.0 - tz) ** 1.6
        if kind == 1:                               # strata: a steep cliff step from the band foot, then the slope
            r_prof = (rim_r + (base_r - rim_r) * (1.0 - zband / height) ** 1.6) * 0.95
        bidx = min(li // 2, bands)
        ring = []
        for i, a in enumerate(angs):
            r = r_prof * jit[bidx][i]
            r = min(r, prev[i])                     # never undercut: radius never grows upward
            prev[i] = r
            zz = z
            da = math.atan2(math.sin(a - notch), math.cos(a - notch))
            if tz > 0.55:                           # the notch cuts the upper bands, deepest at the rim
                zz -= notch_depth * height * max(0.0, math.cos(da)) ** 6 * ((tz - 0.55) / 0.45) ** 1.2
            if kind == 2:
                zz += rng.uniform(-0.015, 0.015) * height
            zz = max(zz, prev_z[i] + 0.006 * height)  # z keeps rising up every column: no fold
            prev_z[i] = zz
            ring.append(mb.v((cx + math.cos(a) * r, cy + math.sin(a) * r, cz + zz)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            mb.f((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    rim = rings[-1]
    inner = []
    floor_z = min(min(prev_z) - 0.03 * height, height * (1.0 - crater_depth))
    for i, a in enumerate(angs):
        r = rim_r * 0.62
        inner.append(mb.v((cx + math.cos(a) * r, cy + math.sin(a) * r, cz + floor_z)))
    for i in range(sides):
        j = (i + 1) % sides
        mb.f((rim[i], rim[j], inner[j], inner[i]))
    mb.f(inner)
    me = _finalize(name, mb, "box", smooth_angle=None, library=False)
    if bpy.data.objects.get(name) is not None:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    _collection_for("DRESS_CONE").objects.link(ob)
    _parent(D, ob)
    return ob


def _smoke_card():
    mb = _MB(("LK_SMOKE",))
    cols, rows = 4, 2
    grid = []
    for r in range(rows + 1):
        row = []
        for c in range(cols + 1):
            u, v = c / cols, r / rows
            x = u - 0.5
            row.append(mb.v((x, -0.18 * (1 - (2 * x) ** 2), v), (u, v)))
        grid.append(row)
    for r in range(rows):
        for c in range(cols):
            mb.f((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
    return _finalize("DRESS_SMOKE_CARD", mb, "vertex", all_smooth=True)


def build_smoke_cards(D, items, seed=0, face=None):
    """DRESS_SMOKE_nn: slightly curved cards (LK_SMOKE) of width x height with the bottom centre at the given point, turned to
    face `face` (x, y) (default the tee at (0, 0)). items = [((x, y, z), w, h), ...]. v2: every card is its OWN mesh with the
    size baked in (scale 1), not a stretched instance of the unit card DRESS_SMOKE_CARD, so no library instance is scaled
    by 35-90x. UV 0..1 over the card."""
    rng = random.Random(_seed_int("smoke", seed))
    fx, fy = face or (0.0, 0.0)
    out = []
    for (p, w, h) in items:
        yaw = math.atan2(fy - p[1], fx - p[0]) - math.pi / 2 + math.pi
        name = _new_name("DRESS_SMOKE", "{}_{:02d}")
        mb = _MB(("LK_SMOKE",))
        cols, rows = 4, 2
        grid = []
        for r in range(rows + 1):
            row = []
            for c in range(cols + 1):
                u, v = c / cols, r / rows
                x = u - 0.5
                row.append(mb.v((x * w, -0.18 * (1 - (2 * x) ** 2) * w * 0.5, v * h), (u, v)))
            grid.append(row)
        for r in range(rows):
            for c in range(cols):
                mb.f((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
        me = _finalize(name, mb, "vertex", all_smooth=True, library=False)
        ob = bpy.data.objects.new(name, me)
        _collection_for("DRESS_").objects.link(ob)
        _parent(D, ob)
        ob.location = Vector(p)
        ob.rotation_euler = (0.0, 0.0, yaw + rng.uniform(-0.15, 0.15))
        out.append(ob)
    return out


# Goal 15 generators. Kept outside BUILDERS: the historic mesh fingerprints stay
# useful negative controls while rebuilt holes explicitly choose these shapes.
def build_column_wall(D, path, top_z, bottom_z=-1.6, width_m=(0.8, 1.5), rows=1,
                      closed=False, seed=0, name="ROCK_WALL_V3", max_faces=490,
                      relief_m=0.16, clip=None, max_span_m=20.0,
                      paired_uv=True, paired_hot_fraction=.80):
    """World-sized 5/6-sided column cladding along an arbitrary metre polyline.

    top_z may be one height or one height per path point. Across-corner diameter
    is width_m, NEVER an instance scale. Core prisms have horizontal tops; a
    clipped corner adds a small sloping chip. Slanted intermediate fractures
    vary independently, and wall-space UVs continue across adjacent columns.
    clip(x,y) can reject a cap footprint (e.g. scoring-water exclusion).
    Returns the chunk objects. Every chunk has actual-column metadata.
    """
    if not (.6 <= width_m[0] <= width_m[1] <= 1.8):
        raise ValueError("column diameters must stay in 0.6..1.8 metres")
    if not (1.9 <= max_span_m <= 20.0):
        raise ValueError('world chunk span must stay in 1.9..20 metres')
    rng = random.Random(_seed_int("columns_v3", seed, name))
    pts = np.asarray(path, float)[:, :2]
    if len(pts) < 2:
        return []
    heights = np.full(len(pts), float(top_z)) if np.isscalar(top_z) else np.asarray(top_z, float)
    if len(heights) != len(pts):
        raise ValueError("one top_z per path point required")
    if closed and np.linalg.norm(pts[0] - pts[-1]) > 1e-6:
        pts = np.vstack([pts, pts[:1]])
        heights = np.concatenate([heights, heights[:1]])
    seg = np.diff(pts, axis=0)
    lengths = np.linalg.norm(seg, axis=1)
    accum = np.concatenate([[0.0], np.cumsum(lengths)])
    out, specs = [], []
    mb = _MB(("LK_BASALT",))

    def flush():
        nonlocal mb, specs
        if not mb.F:
            return
        nm = f"{name}_{len(out):03d}"
        me = _finalize(nm, mb, "box", smooth_angle=None, library=False)
        ob = bpy.data.objects.new(nm, me)
        _collection_for("ROCK_").objects.link(ob)
        _parent(D, ob)
        import json
        ob["lk_v3_columns"] = json.dumps(specs)
        ob["lk_v3_world_sized"] = True
        if paired_uv:
            import postcard_look_lib as L
            # Only this new world-sized generator opts in. Legacy BUILDERS
            # and their original UV/geometry fingerprints remain untouched.
            L.author_basalt_paired_columns(ob, seed=seed + len(out),
                                           hot_fraction=paired_hot_fraction)
        out.append(ob)
        mb, specs = _MB(("LK_BASALT",)), []

    for row in range(max(1, int(rows))):
        s = rng.uniform(0, .5) if row else 0.0
        while s < accum[-1]:
            w = rng.uniform(*width_m)
            sc = min(s + .45 * w, accum[-1] - 1e-5)
            s += w * rng.uniform(.78, .90)
            si = min(len(seg) - 1, max(0, int(np.searchsorted(accum, sc, side="right") - 1)))
            if lengths[si] < 1e-8:
                continue
            t = (sc - accum[si]) / lengths[si]
            tangent = seg[si] / lengths[si]
            normal = np.array([tangent[1], -tangent[0]])
            r = .5 * w
            cen = pts[si] + seg[si] * t + normal * (relief_m - .88 * r - row * .72 * w)
            ht = heights[si] * (1 - t) + heights[si + 1] * t - rng.uniform(.02, .12)
            if rng.random() < .42:
                ht -= rng.uniform(.15, .65)
            n = 5 if rng.random() < .25 else 6
            rot = math.atan2(tangent[1], tangent[0]) + math.pi / 6 + rng.uniform(-.08, .08)
            xy = [(cen[0] + r * math.cos(rot + math.tau * k / n),
                   cen[1] + r * math.sin(rot + math.tau * k / n)) for k in range(n)]
            if clip is not None and not all(clip(x, y) for x, y in xy):
                continue
            if ht < bottom_z + .4:
                continue
            nseg = max(1, min(5, int((ht - bottom_z) / rng.uniform(2.3, 3.5))))
            cuts = [bottom_z] + sorted(rng.uniform(bottom_z + .6, ht - .5) for _ in range(nseg - 1)) + [ht]
            candidate=np.vstack([np.array(mb.V)[:,:2],np.asarray(xy)]) if mb.V else np.asarray(xy)
            span=float(np.linalg.norm(np.ptp(candidate,axis=0)))
            if len(mb.F) + (n + 3) * nseg > max_faces or span+.04 > max_span_m:
                flush()
            vi0, fi0 = len(mb.V), len(mb.F)
            for gi, (za, zb) in enumerate(zip(cuts[:-1], cuts[1:])):
                # Each break jogs and tips a little; the overall diameter stays
                # inside the explicit bound rather than scaling a giant shaft.
                scale = rng.uniform(.96, 1.0)
                j = rng.uniform(-.015, .015)
                bot = [mb.v((cen[0] + (x - cen[0]) * scale + tangent[0] * j,
                             cen[1] + (y - cen[1]) * scale + tangent[1] * j,
                             za - (.015 if gi else 0))) for x, y in xy]
                top = [mb.v((cen[0] + (x - cen[0]) * scale,
                             cen[1] + (y - cen[1]) * scale, zb)) for x, y in xy]
                chip = gi == nseg - 1 and rng.random() < .65
                ci = rng.randrange(n)
                cap = list(top)
                if chip:
                    prev, nex = (ci - 1) % n, (ci + 1) % n
                    p0, pc, p1 = np.array(mb.V[top[prev]]), np.array(mb.V[top[ci]]), np.array(mb.V[top[nex]])
                    a = mb.v(tuple(pc * .70 + p0 * .30))
                    b = mb.v(tuple(pc * .70 + p1 * .30))
                    mb.V[top[ci]] = (pc[0], pc[1], pc[2] - rng.uniform(.08, .18) * w)
                    cap = top[:ci] + [a, b] + top[ci + 1:]
                for k in range(n):
                    kn = (k + 1) % n
                    fc = (bot[k], bot[kn], top[kn], top[k])
                    if chip and k == (ci - 1) % n:
                        fc = (bot[k], bot[kn], top[kn], a, top[k])
                    elif chip and k == ci:
                        fc = (bot[k], bot[kn], top[kn], b, top[k])
                    uv = [(sc + float((np.array(mb.V[v][:2]) - cen) @ tangent)) / 8.0 for v in fc]
                    mb.f(fc, uv=[(u, mb.V[v][2] / 8.0) for u, v in zip(uv, fc)])
                if chip:
                    mb.f((top[ci], b, a))
                mb.f(cap)
                if gi == 0:
                    mb.f(list(reversed(bot)))
            specs.append(dict(sides=n, width=w, top=ht, bottom=bottom_z,
                              chipped=chip, verts=[vi0, len(mb.V)], faces=[fi0, len(mb.F)]))
    flush()
    return out


def build_needle(D, center, height_m=18.0, base_width_m=4.5, seed=0, name=None, surf=True):
    """One layered eroded needle with a muted scrub crown and optional surf.

    Geometry is generated at its final metre size (object scale remains 1).
    The widest section in the lowest 10% above water is measured and limited
    to height/3.15, providing margin above the fixed 3x height/base requirement.
    A surf ring is broken into uneven short arcs, never a closed white band.
    """
    h = float(height_m)
    width = min(float(base_width_m), h / 3.15)
    if h < 6 or width <= 0:
        raise ValueError("needle needs height >=6m and positive base width")
    nm = name or _new_name("ROCK_NEEDLE", "{}_{:02d}")
    ob = _spire_mesh(nm, _seed_int("needle_v3", seed), h, width / 2.35, .22,
                     lean=(.04, -.035), slices=17, ledges=(.28, .53, .73),
                     overhangs=(.43,), twist=.35, top="chisel", p=1.3,
                     under=2.4, bend=(.025, .025), bulge=.16, bulge_u=.20,
                     mats=("LK_ROCK_WET", "LK_ROUGH", "LK_PLANTS"))
    me = ob.data
    # The v3 summit is turf directly on the rock, with a shallow irregular
    # crown. Keep the historic library's shrub dome byte-compatible.
    sl = np.array([v.value for v in me.attributes['lk_slice'].data])
    crown = sl < 0
    neck = sl == sl.max()
    xyz = np.array([v.co[:] for v in me.vertices])
    nc = xyz[neck, :2].mean(0)
    neck_z = float(xyz[neck, 2].mean())
    crown_h = min(.35, h * .02)
    top0, top1 = float(xyz[crown, 2].min()), float(xyz[crown, 2].max())
    neck_r = float(np.linalg.norm(xyz[neck, :2] - nc, axis=1).max())
    crown_r = float(np.linalg.norm(xyz[crown, :2] - nc, axis=1).max())
    for i, v in enumerate(me.vertices):
        if crown[i]:
            v.co.z = h - crown_h + crown_h * (xyz[i, 2] - top0) / max(top1 - top0, 1e-9)
            v.co.x, v.co.y = nc + (xyz[i, :2] - nc) * min(1.0, .98 * neck_r / max(crown_r, 1e-9))
        elif v.co.z > 0:
            v.co.z *= (h - crown_h) / neck_z
    ob['lk_v3_crown_height'] = crown_h
    co, tris, _tp = _np_mesh(me)
    sections = [mesh_section(co, tris, z)[:, :2] for z in np.linspace(0, .1 * h, 9)]
    bw = max((max(np.ptp(q[:, 0]), np.ptp(q[:, 1])) for q in sections if len(q)), default=width)
    factor = min(1.0, width / max(bw, 1e-9))
    for v in me.vertices:
        v.co.x *= factor
        v.co.y *= factor
    me.update()
    # Recompute rock UVs after geometric sizing; atlas crown UVs are retained.
    uv = box_uv_expected(me, [TILE[m.name] or 4 for m in me.materials])
    old_uv = np.array([q.uv[:] for q in me.uv_layers.active.data])
    for p in me.polygons:
        if me.materials[p.material_index].name == "LK_PLANTS":
            uv[p.loop_start:p.loop_start + p.loop_total] = old_uv[p.loop_start:p.loop_start + p.loop_total]
    me.uv_layers.active.data.foreach_set("uv", uv.ravel())
    for coll in list(ob.users_collection):
        coll.objects.unlink(ob)
    _collection_for("ROCK_").objects.link(ob)
    ob.hide_viewport = ob.hide_render = False
    ob.location = Vector(center)
    _parent(D, ob)
    ob["lk_v3_needle"] = True
    ob["lk_v3_base_width"] = bw * factor
    ob["lk_v3_height"] = h
    ob["lk_v3_shape_factor"] = factor
    res = dict(needle=ob, surf=[])
    if surf and D is not None:
        import postcard_look_lib as L
        rr = bw * factor * .52
        rng = random.Random(_seed_int("needle_surf", seed))
        for a in (0.0, 1.8, 3.8):
            aa = a + rng.uniform(-.22, .22)
            c = (center[0] + rr * math.cos(aa), center[1] + rr * math.sin(aa))
            patch = L.surf_patch(D, c, radius_m=min(1.8, rr * .65), seed=seed + int(a * 10))
            if patch is not None:
                res["surf"].append(patch)
    return res
