"""Hole 8 NEEDLE: the LOOK pass (POSTCARD_LOOK continue, 2026-10-04). Look only: every scoring mesh comes from hole08_build.py unchanged.

Called by hole08_build.main() after the scoring build (terrain, play surfaces, gameplay markers) and before render / save / export:

    info = look_pass(D)           (D = postcard_lib handle; scenery data from hole08_design.SCENERY)
    ok = look_gates(D, info)      (the hole's own gates, re-expressed for the library pieces)

Order (LIB_GROUND section 1 + LIB_PROPS section 1): look_materials -> snapshot_ground -> flagstone paths -> arch + outcrop -> sea stacks ->
foot / rim rocks -> plants (scatter, land-masked) -> retarget_materials -> cliff skin -> vines hung on the skin -> waterfall sheet -> sea (thin
shelf, sparse surf) + surf patch at the waterfall foot -> assign_uvs LAST -> mow_pattern (round 2: stripe phase + cross-mown tee box, UV / material
only) -> compare_ground.

Round 2 (reviewer findings): mow_pattern (stripe phase + cross-mown tee box), shorter tufts, shelf preview colour, re-routed paths.
Round 3 (second reviewer round): both flagstone paths run along the ball's left side so the lower 60 % of the tee and approach phone frames carry stones
(PATHS, PATH_BALL_CLEAR_M, gate PATH_CLEAR_OF_BALL: the address balls stay 0.5 m off the stones); PATHBALL_YD is the optional path-ball shot.

Round 6 (repair round 2, review of the installed round-5 build): step_path_over_plate + the hidden-band UV shift of the TEE_BOX (the tee seam: a grass strip across the stones, grass and stones jumping 0.35 m at the
tee plate's edge), dress_outcrop (the grey slab of the arch shot), gates TEE_SEAM_SEEN / PATH_STEP_CONTINUOUS / ARCH_SLAB_COVERED (tee_seen_seam, arch_frame_bare_rock); the plan-view TEE_SEAM_UV gate is retired.

Round 8 (repair round 0 of the GRASS LIVELINESS + GENTLE SWAY add-on, 2026-10-05): cheapen_tufts + scatter_fringe (the edge fringe of tall tuft clumps), outcrop_fringe (clumps on the arch island), crown_tufts (tufts on the sea-stack
crowns), attach_vines (every vine root touches a wall; the outcrop vines follow the wall's slope), dress_outcrop's rim shoulder (bevel + smooth custom turf normals, gate ARCH_FOLD_SOFT), gates GRASS_* / GRASS_OUTCROP_FRINGE / CROWN_TUFTS /
VINE_ROOT_GAP. The pad fringe cannot reach the tee / approach frames' near ground: it is 100 % fairway, tee box and stone path (no tuft on a play surface), see GRASS_FRAME_COVERAGE_BLENDER.

Everything is seeded (hole number + fixed seeds), so a second run gives the same geometry (gate H_REPRODUCIBLE).
The legacy decorative geometry of the flat build (ROCK_SMALL/MEDIUM/LARGE/CLIFF_ROCK icosahedra, the arch built of ROCK_LARGE, the stacked-rock
needles, the MAT_FOAM strips) is NOT built any more. Its gates are re-expressed for the library pieces in look_gates():
  NEEDLES                  -> NEEDLES (cap 18 m, >= 25 yd from the centerline, none in a water ellipse or on land) on the tapering spires
  ARCH                     -> ARCH (cap 18 m, the masonry stands on the outcrop top, fallen blocks lie on it)
  ROCKS_OFF_PLAY_SURFACES  -> ROCKS_OFF_PLAY_SURFACES (every ROCK_/DRESS_ piece, not only the loose rocks)
  ROCKS_STAND_IN_THE_SEA   -> ROCKS_STAND_IN_THE_SEA (stacks, outcrop, foot boulders reach the water)
  ROCKS_CONTACT_CHAIN      -> ROCKS_CONTACT_CHAIN (every piece is chained by touching surfaces to a rock standing in the sea or to the ground)
  ROCKS_REST               -> ROCKS_REST (land rocks sit on the ground, fallen blocks lie on the outcrop, no overhang over open air)
  LINES_OF_PLAY_VISIBLE    -> LINES_OF_PLAY_VISIBLE (tee / landing pad / far end cameras still see the next pad and the green past every piece)
  (new) NEEDLES_NOT_BEHIND_TARGETS: no needle stands right behind the flag or the arch opening as seen from the three cameras.
"""
import json
import math
import os
import random

import numpy as np
import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from mathutils import geometry as G

import postcard_lib as P
import postcard_look_lib as L
import postcard_props_lib as PL

YD = P.YD
STACK_CAP_M = 18.0          # brief: needles may rise above the pads but never above 18 m, the tee camera must see the next pad
STACK_TIP_M = 17.6          # the tallest needle's design tip
STACK_FLOOR_M = 9.0         # the shortest needle is still well above the 6 m pads
ARCH_MOVE_X_YD = -4.0       # the arch stands 3.2 yd further out than SCENERY['arch'].x: the masonry arch is 12.6 m wide (the flat build's rock arch 10.6 m), so its right pier
                            # now stands exactly at the pad edge instead of in it
ARCH_TOP_LIFT_M = 0.2       # the outcrop top is 5.2 m, not 5.0: a shrub's lowest lump reaches 7-11 cm under its origin and the plant sinks 4 cm, and the
                            # NO_PLANT_ON_WATER gate wants every plant base at z >= PLAY_Z - 1 = 5.0 m (still 0.8 m under the pad top)
OUTCROP_R_M = 8.3           # the arch's own rock: flat top about 14 m wide (the flat build's plinth: 14 yd)
OUTCROP_YAW_ORDER = tuple(int(v) for v in os.environ["HOLE08_YAW"].split(",")) if os.environ.get("HOLE08_YAW") else tuple(range(16))      # outcrop yaw candidates (k / 16 turns), first one whose flat top carries the arch wins
ARCH_SCALE = 1.25           # as the flat build: the arch is built 25 percent larger than span_yd/height_yd so it reads from the phone camera
# Needle positions that differ from SCENERY['stacks'] (x, d in yards, key = the stack's seed): from the middle pad and from the tee the
# design's needle 7 stands exactly behind the arch opening and needle 9 exactly behind the flag, so they move 12 yd left and 30 yd right.
# Needle 5 and needle 3 stood right in front of the arch (needle 3 in its opening) as seen from the tee camera; they move 16 and 17 yd left.
STACK_MOVE = {1: (-22.0, 128.0), 3: (-28.0, 172.0), 5: (-40.0, 236.0), 6: (34.0, 320.0), 7: (-52.0, 345.0), 9: (16.0, 398.0)}
STACK_HEIGHT_M = {1: 14.0, 2: 16.0, 3: 13.0, 4: 15.5, 5: 11.0, 6: 16.5, 7: 17.0, 8: 14.5, 9: 17.6}
ARCH_DEPTH_M = 1.9
STACK_MIN_CENTERLINE_YD = 25.0

# Flagstone paths (course yards). Visual overlays: build_path guarantees <= 1.2 cm over the collider under them.
# Round 3 (reviewer, low: the lower 60 % of the tee and approach frames is still plain lawn). The camera of both address shots (CameraRig.FrameAddress: 4.5 yd behind the ball,
# 2.4 yd up, 60 degrees) sees only 2-3 yd of ground across the lower 20 % of the frame and ~7 yd at 60 %, so a path that keeps 3 yd away from the ball (round 2) lies in the
# upper part of the frame or outside it. Both paths now run along the ball's left side (the side a right-handed golfer stands on): the nearest path edge stays PATH_BALL_CLEAR_M
# (0.5 m, gate PATH_CLEAR_OF_BALL) from the ball, the path edge enters the frame from the lower left and crosses the line of play further out. The path may lie under a resting
# ball (build_path: <= 1.2 cm over the collider, PATH_UNDER_BALL), but a white ball reads best on grass beside the stones, so the ball stays off it.
PATHS = [
    # Round 5 (reviewer, medium): the tee path crosses the tee box's front edge (y = 3.5..3.8 m). The edge is a 10 cm step whose riser faces away from the camera, so about 0.4 m of the fairway
    # behind it is hidden and any bend of the path there shows as a sideways jump. The path now runs straight (dx/dy 0..0.03, parallel to the aim line of the tee shot) from y = -2 yd to
    # y = 6 yd, with its right edge 0.6 m left of the ball line, and only bends right further out (gate PATH_SEAM_JUMP measures the jump in the tee frame).
    dict(tag="tee", pts=[(-3.9, -7.5), (-2.2, -3.0), (-2.1, 0.0), (-2.05, 4.0), (-1.9, 7.5), (-1.0, 11.0), (1.0, 14.5), (3.4, 18.0), (5.2, 26.0), (5.2, 35.0), (3.5, 43.0)], width=2.6),
    dict(tag="mid", pts=[(8.0, 150.0), (12.0, 160.0), (15.9, 169.0), (16.9, 178.0), (16.5, 190.0), (15.2, 202.0), (13.8, 213.0)], width=2.4),
]
PATHBALL_YD = (-1.0, 11.0)           # the optional path-ball shot: a control point of the tee path (Catmull-Rom passes through it)
PATH_SEAM_MAX_STEP_PX = 10.0         # tee frame, gate PATH_SEAM_JUMP: raw step / fitted jump / slope change of the path edge at the tee box's front edge (round 4: 29 px / 10 px / 0.9 px per row)
PATH_SEAM_MAX_JUMP_PX = 8.0
PATH_SEAM_MAX_KINK = 0.8
OUTCROP_VINES_MIN = 8
VINE_TILT_MAX_DEG = 55.0              # round 8: an outcrop vine follows the wall's slope (tilt about its root); a wall that leans out more than this / recedes more than the min is skipped
VINE_TILT_MIN_DEG = 30.0
PATH_BALL_CLEAR_M = 0.5              # the address balls (tee (0, 0), approach (19, 178)) stay this far from every path edge
WATERFALL_HINT_YD = (9.0, 299.0)     # the green pad's north (tee-facing) cliff, right of the middle: seen from the approach camera
WATERFALL_WIDTH_M = 4.8
WATERFALL_WIDEN = 1.35
SEA_SEED = 1                          # build_sea layout seed (the surf patches)
SEA_PUFFS = 0                         # build_sea rock_puffs: the reef puffs at rocks standing 3-12 m off the shore merge with the shore patches inside the verifier's 10 m
                                      # reach (gap 1-7 m on 14 of 15 seeds, the rule wants >= 8 m); 0 keeps only the sparse shore patches and the waterfall foot
# Mow pattern (round 2). Fairway_C carries two lengthwise mow bands per 10 m tile (light centred on u = 0, dark on u = 0.5, each 5 m); with the lib's
# u = lateral offset / 10 the centreline sits in the middle of a light band, so the whole 4 m wide near field of both phone cameras was one tone (albedo
# luminance sd 7.2 / 6.4 over the lower 60 % of the tee / approach frames). Shifting u by FAIRWAY_STRIPE_SHIFT puts a band edge 0.5 m right of the centreline
# (stripe_scan.py: approach sd 12.7). The tee box (6 x 7 m, 86 % of the tee frame's lower 60 %) was LK_GREEN (Green_C sd 6.3): it is cross-mown LK_FAIRWAY
# instead (allowed: GROUND_MATERIALS TEE_BOX in {LK_GREEN, LK_FAIRWAY}): U = arc / 10 + TEE_CROSS_PHASE, V = -lateral / 10, so its bands run across the line of
# play (distinct from the fairway's) with an edge 0.5 m behind the ball. UVs and one material slot only: no vertex moves (compare_ground).
FAIRWAY_STRIPE_SHIFT = 0.20
TEE_CROSS_PHASE = 0.80
TEE_BOX_CROSS_MOWN = True        # False = leave the tee box as the library made it (LK_GREEN, lib UVs); experiments only (r3/tools/tee_variant.py)
# Round 5 (reviewer, medium: "a hard line cuts across the fairway on the tee shot"): the cross-mown tee box (stripes turned 90 degrees) met the fairway's lengthwise stripes along the
# whole 9 m front edge; the albedo luminance step across that edge was 28 levels on average (rms 32, up to 59 at the dark fairway stripe), on top of the 10 cm step. The tee box now
# continues the fairway's own UVs (same centreline mapping, same stripe shift): the stripes run straight through the seam, the albedo step across it is only the texture's own
# noise, and the tee reads as a platform by its 10 cm edge (TEE_SEAM_SOFT measures the step).
TEE_BOX_SEAMLESS = True
# Round 6 (repair round 2, reviewer, medium: "a hard horizontal seam still cuts across the fairway on the tee shot, a strip of grass slices through the stone path and the path shifts sideways at it"):
# two causes, both measured on the staged round-5 build (tools/probe in r6/): (1) build_path drapes every ribbon vertex on the LOWEST ground of the four cells around it (so that the ribbon never
# stands above a collider), so on the tee box side of the 10 cm step the ribbon sinks to the fairway level one to two lattice rows before the plate's edge and the tee plate's grass shows
# through it: a 0.2 m strip of grass across the stones; (2) the plate is a hovering 10 cm step (no riser): from the address camera it hides a 0.35 m band of the fairway behind its edge, so
# the grass and the stones jump by that band at the edge although the UVs are continuous in plan. Fix (UV / path geometry only, no vertex of a scoring mesh moves): the ribbon is cut along the
# plate outline, the part over the plate is lifted onto the plate (+ Z_PATH), the part beyond the edge stays on the fairway, a vertical riser joins them (it faces away from the camera, and it
# stands on the plate's edge line so no top-down ray meets it: the path stays <= 1.2 cm over the collider); the plate's grass UVs and the ribbon's plate part are shifted by the hidden band
# (UV_tee(p) = UV_fairway(C + k (p - C)), k = (cam z - fairway z) / (cam z - plate z): the first fairway point the camera sees past the edge is the one the UV continues to).
TEE_STEP = True
PLATE_INSET_M = 0.002
TUFT_SCALE_FRINGE = (0.55, 0.85)
TUFT_SCALE_FAR = (0.55, 0.85)
# Round 8 (repair round 0 of the GRASS LIVELINESS + GENTLE SWAY add-on, 2026-10-05): real tuft meshes standing taller and denser along the rough edge of the pads (scatter_fringe: clumps of 3-7, lean <= 12 degrees,
# height 0.6-1.8 x the library tuft, never on a play surface / the sea / within 4 m of a shot ball (1.5 m at the tee ball) / 6 m of the pin, tops <= 0.55 yd near the ball corridor). The old 117-triangle tuft carpet
# pays for it: every A / B tuft farther than CHEAPEN_NEAR_M from a shot ball becomes the 36-triangle filler S / T (same place, same scale). All three pads are within 60 m of a shot ball, so the library default
# near_m = 60 would swap nothing here; the phone cameras see ~25 m of ground.
FRINGE_ON = True
OUTCROP_FRINGE_CLUMPS = 28                # round 8: clumps of the new tufts on the arch island's grass top (rim band, pier / block bases, a few in the open): the arch frame's foreground is the one place where
OUTCROP_FRINGE_SEED = 47                  # the tee / approach frames cannot carry any fringe (their ground is fairway, first cut and the stone path: 100 % play surface)
OUTCROP_FRINGE_TRI_BUDGET = 9000
CROWN_TUFTS_ON = True                     # round 8 (libs hand-off, review 3: "sea-stack caps are faceted saturated green hats"): tuftify_crowns puts dark green / olive tufts on the top 2 m of every spire
CROWN_TUFTS_PER = (14, 22)                # a generous candidate batch: crown_tufts() keeps the ones the verifier's support rule accepts
CROWN_TUFT_H = (0.8, 1.3)                 # library default 1.3-1.95 x: blades 0.9 m long on a dome 1-1.5 m wide hang over its rim; 0.8-1.3 x keeps every blade over the crown
CROWN_M = 1.0                             # the top metre of a spire: the flat part of the dome (a tuft standing nearer the dome's rim has blades over open air: the verifier's NO_PLANT_ON_WATER wants an up-facing rock top under every blade)
CROWN_TUFTS_TRI_BUDGET = 9000
OUTCROP_FRINGE_H_MAX = 1.2                # m, tuft height above the island top (the island is not a play surface)
FRINGE_TRI_BUDGET = 52000
FRINGE_SEED = 11
FRINGE_KINDS = dict(tall={"PLANT_TUFT_D": 3, "PLANT_TUFT_E": 2}, fill={"PLANT_TUFT_S": 3})
CHEAPEN_NEAR_M = 14.0
TRI_CAP = 250000                          # the add-on's visible-triangle budget per hole (export set)
SHELF_PREVIEW_RGB = (27, 106, 120)    # GolfLook.Table LK_WATER_SHALLOW Shallow colour: the Blender preview of the shelf matches the Unity colour (preview only: the FBX has no colours)
CAMERAS = dict(
    tee=((0.0, 0.0), (19.0, 178.0)),             # ball, aim (course yards): CameraRig.FrameAddress
    mid=((19.0, 178.0), (-6.0, 340.0)),
    far=((14.0, 205.0), (-6.0, 340.0)),
)


def _gate(name, ok, detail):
    print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)
    return ok


def _log(msg):
    print(f"[hole08_look] {msg}", flush=True)


def world_verts(obs):
    bpy.context.view_layer.update()
    out = []
    for ob in obs:
        mw = np.array(ob.matrix_world)
        co = np.array([v.co[:] for v in ob.data.vertices]).reshape(-1, 3)
        out.append(co @ mw[:3, :3].T + mw[:3, 3])
    return np.vstack(out) if out else np.zeros((0, 3))


def world_tris(ob):
    """(V world (n,3), T (m,3)) triangles of a mesh object in world space."""
    bpy.context.view_layer.update()
    me = ob.data
    me.calc_loop_triangles()
    mw = np.array(ob.matrix_world)
    V = np.array([v.co[:] for v in me.vertices]).reshape(-1, 3) @ mw[:3, :3].T + mw[:3, 3]
    T = np.array([t.vertices[:] for t in me.loop_triangles]).reshape(-1, 3)
    return V, T


def bvh_of_objects(obs):
    V, F, base = [], [], 0
    for ob in obs:
        v, t = world_tris(ob)
        V += [tuple(p) for p in v.tolist()]
        F += [tuple(base + int(i) for i in tri) for tri in t.tolist()]
        base += len(v)
    return BVHTree.FromPolygons(V, F) if F else None


def stack_height_m(design_height_yd):
    """The design's 12-30 yd heights mapped onto STACK_FLOOR_M..STACK_TIP_M (9-17.6 m), keeping their order (as the flat build did)."""
    t = max(0.0, min(1.0, (design_height_yd - 12.0) / (30.0 - 12.0)))
    return STACK_FLOOR_M + (STACK_TIP_M - STACK_FLOOR_M) * t


def _alive(o):
    """False for an object that was deleted from the scene (touching its .name raises ReferenceError)."""
    try:
        return bpy.data.objects.get(o.name) is o
    except ReferenceError:
        return False


def _remove(objs):
    for o in objs:
        if o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)


# ----------------------------------------------------------------------------- the look pass
def look_pass(D, sea_seed=None):
    SC = D.scenery
    pz = D.play_z
    info = dict(stacks=[], rocks=[], plants=[], paths=[], vines=[], falls=[])
    L.look_materials(D)
    snap = L.snapshot_ground(D)
    info["snap"] = snap

    # 1. flagstone paths (BEFORE any scatter: the scatter keeps 0.4 m off every DRESS_PATH footprint)
    for i, pa in enumerate(PATHS):
        info["paths"] += L.build_path(D, pa["pts"], width_m=pa["width"], seed=i)
    path_xy = [[P.m(*p) for p in pa["pts"]] for pa in PATHS]
    info["path_xy"] = path_xy

    # 2. the arch on its own rock outcrop beside the bunker (SCENERY['arch'])
    info["arch"] = build_arch(D, SC["arch"])

    # 3. sea stacks (tapering spires with grass crowns; SCENERY['stacks'])
    for st in SC["stacks"]:
        info["stacks"].append(place_stack(D, st))

    info["plants"] += outcrop_plants(D, info["arch"])

    # 4. boulders: at the cliff foot in the water, a few stones on the pad rims
    info["rocks"] += PL.scatter_rocks(D, 24, zone="foot", seed=11)
    info["rocks"] += PL.scatter_rocks(D, 14, zone="rim", seed=12)

    # 5. plants (default regions are land-masked: nothing can land on the water)
    info["plants"] += PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B"], 34, region=PL.region_band(D, 0.8, 7.0), seed=2)
    info["plants"] += PL.scatter(D, {"PLANT_FLOWER_PURPLE": 5, "PLANT_FLOWER_WHITE": 1, "PLANT_FLOWER_YELLOW": 1}, 70,
                                 region=PL.region_band(D, 0.5, 9.0), seed=3)
    for i, xy in enumerate(path_xy):
        info["plants"] += PL.scatter(D, {"PLANT_FLOWER_PURPLE": 3, "PLANT_FLOWER_WHITE": 1}, 16, region=PL.region_polyline(xy, 2.8), seed=20 + i)
        info["plants"] += PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_B"], 5, region=PL.region_polyline(xy, 3.6), seed=30 + i)
    # tufts: scale 0.55-0.85 (library default 0.8-1.25 = up to 0.56 m tall on the pads, 0.62 on the arch outcrop): a ball resting in the rough (0.11 m) is not hidden by 0.5 m grass (reviewer, low)
    info["plants"] += PL.scatter(D, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2}, 360, region=PL.region_band(D, 0.4, 3.2), seed=1, scale_range=TUFT_SCALE_FRINGE)     # the grass fringe along the pad rims
    info["plants"] += PL.scatter(D, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2}, 300, region=PL.region_band(D, 3.2, 13.0), seed=4, scale_range=TUFT_SCALE_FAR)

    # 5b. grass liveliness (round 8): pay for the fringe with the old carpet (cheapen_tufts), then the EDGE FRINGE itself (the last scatter: it keeps off every footprint registered before it)
    info["tris_before_fringe"] = P.stats(D)["tris"]
    if FRINGE_ON:
        info["cheapen"] = PL.cheapen_tufts(D, near_m=CHEAPEN_NEAR_M)
        info["fringe"] = PL.scatter_fringe(D, kinds=FRINGE_KINDS, tri_budget=FRINGE_TRI_BUDGET, seed=FRINGE_SEED)
        info["fringe_objs"] = list(info["fringe"]["objs"])
        fr = info["fringe"]
        _log(f"cheapen_tufts(near {CHEAPEN_NEAR_M:g} m): {info['cheapen']}; fringe: {fr['count']} tufts {fr['tris']} tris, {fr['clusters']} clumps / {fr['singles']} singles, by kind {fr['by_kind']}, "
             f"edge {fr['edge']} covered {fr['edge_covered']}/{fr['edge_samples']}, focus {fr['edge_focus']}, capped {fr['capped']}, top max {fr['top_max_m']:.3f} m")

    if CROWN_TUFTS_ON:
        info["crowns"] = crown_tufts(D, [o for st in info["stacks"] for o in st["objs"]])
        _log(f"crown tufts: {len(info['crowns']['objs'])} kept of {info['crowns']['candidates']} candidates ({info['crowns']['pruned']} pruned: a blade without an up-facing rock top under it), {info['crowns']['tris']} tris on {info['crowns']['shrubs']} spires")

    # 6. materials of the flat meshes -> LK_*, cliff skin
    L.retarget_materials(D)
    info["firstcut_transition"] = firstcut_transition(D)
    info["skins"] = L.build_postcard_cliff(D, style="needle", seed=8)
    # H8 uses the stricter whole-drop support placement below; avoid overlapping batches.
    _remove([o for o in getattr(D, "postcard_cliff_vines", []) if _alive(o)])
    D.postcard_cliff_vines = []

    # 7. vines hanging on the skin; 8. the waterfall; 9. sea (thin shelf, sparse surf)
    fall = build_waterfall(D, P.m(*WATERFALL_HINT_YD))
    info["falls"].append(fall)
    info["vines"] = vines_on_skin(D, spacing=4.0, avoid=[(fall["top"][0], fall["top"][1], 6.0)])
    info["outcrop_vines"] = vines_on_outcrop(D, info["arch"])          # round 5: the outcrop's near flank is a sheer 2 m wall that reads as a flat grey slab from the pad rim
    # round 8 (libs hand-off, review 3: 8 of 131 vines hung 0.6-0.86 m off the wall): every vine root touches a surface afterwards (moved onto it within 1 m, deleted beyond)
    info["vine_attach"] = PL.attach_vines(D)
    _log(f"attach_vines: {({k: v for k, v in info['vine_attach'].items() if k != 'gaps_after'})}")
    info["vines"] = [o for o in info["vines"] if _alive(o)]
    info["outcrop_vines"] = [o for o in info["outcrop_vines"] if _alive(o)]
    info["arch"]["vines"] = [o for o in info["arch"]["vines"] if _alive(o)]
    pockets = [(fall["foot"][0], fall["foot"][1], 6.0)]
    info["pockets"] = pockets
    L.build_sea(D, pockets=pockets, seed=SEA_SEED if sea_seed is None else sea_seed, rock_puffs=SEA_PUFFS)
    impact = build_fall_impact(D, fall)
    info["surf_patch"] = impact
    info["mist"] = build_fall_vapour(D, fall)
    info["waterfall_mist_status"] = "UNMEASURED"

    info["shelf_preview"] = match_shelf_preview()

    # 10. UVs last, ground untouched; then the mow pattern (UV + one material slot only)
    L.assign_uvs(D)
    info["mow"] = mow_pattern(D)
    info["ledges"] = grass_on_skin_ledges(D, info["skins"])           # round 5: the capping ledge under the grass lip was a dark LK_CLIFF slab ("hard black box")
    info["lip"] = bunker_lip_to_rough(D)                              # round 5: the bright, streaky LK_GREEN bunker lip showed a chevron seam on its crest
    info["tee_step"] = tee_step(D, info)                              # round 6: the path over the 10 cm tee step (cut, lift, riser, UV shift)
    ok, detail = L.compare_ground(D, snap)
    info["compare_ok"] = (ok, detail)
    return info


# ----------------------------------------------------------------------------- round 2: mow pattern, shelf preview colour
def _uv_world_density(ob, mat_name):
    """UV area / world area (UV units per metre, squared root) over the faces of `ob` that use `mat_name`."""
    me = ob.data
    lay = me.uv_layers.active.data
    mw = np.array(ob.matrix_world)
    a_uv, a_w = 0.0, 0.0
    for p in me.polygons:
        m = me.materials[p.material_index] if me.materials else None
        if m is None or m.name.split(".")[0] != mat_name:
            continue
        V = np.array([me.vertices[me.loops[li].vertex_index].co[:] for li in range(p.loop_start, p.loop_start + p.loop_total)]) @ mw[:3, :3].T + mw[:3, 3]
        U = np.array([lay[li].uv[:] for li in range(p.loop_start, p.loop_start + p.loop_total)])
        a_w += 0.5 * abs(float(np.dot(V[:, 0], np.roll(V[:, 1], -1)) - np.dot(V[:, 1], np.roll(V[:, 0], -1))))
        a_uv += 0.5 * abs(float(np.dot(U[:, 0], np.roll(U[:, 1], -1)) - np.dot(U[:, 1], np.roll(U[:, 0], -1))))
    return math.sqrt(a_uv / a_w) if a_w > 0 else float("nan"), a_w


def mow_pattern(D, fshift=None, tee_phase=None):
    """Round 2. (1) u += FAIRWAY_STRIPE_SHIFT on every LK_FAIRWAY face of FAIRWAY / FAIRWAY_FIRSTCUT / GREEN_APRON (a band edge of Fairway_C's mow stripes 0.5 m
    right of the centreline instead of the middle of the light band); (2) TEE_BOX becomes LK_FAIRWAY, cross-mown: U = arc / 10 + TEE_CROSS_PHASE, V = -lateral / 10
    (a 90 degree rotation of the lib's (lateral, arc) UV: no mirroring). Only UVs and the tee box's single material slot change; no vertex moves. Returns a report."""
    fshift = FAIRWAY_STRIPE_SHIFT if fshift is None else fshift
    tee_phase = TEE_CROSS_PHASE if tee_phase is None else tee_phase
    tile = L.LOOK["LK_FAIRWAY"][2]
    rep = dict(shift=fshift, tee_phase=tee_phase, faces={})
    for name in ("FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN_APRON"):
        ob = bpy.data.objects[name]
        me = ob.data
        lay = me.uv_layers.active.data
        n = 0
        for p in me.polygons:
            m = me.materials[p.material_index] if me.materials else None
            if m is not None and m.name.split(".")[0] == "LK_FAIRWAY":
                for li in range(p.loop_start, p.loop_start + p.loop_total):
                    u, v = lay[li].uv
                    lay[li].uv = (u + fshift, v)
                n += 1
        me.update()
        rep["faces"][name] = n
    tb = bpy.data.objects["TEE_BOX"]
    me = tb.data
    before = sorted({m.name for m in me.materials if m})
    if not TEE_BOX_CROSS_MOWN:
        return dict(shift=fshift, tee_phase=None, faces=rep["faces"], tee_box_materials_before=before, tee_box_faces=len(me.polygons), density_ratio={})
    me.materials.clear()
    me.materials.append(L.look_material("LK_FAIRWAY", D))
    for p in me.polygons:
        p.material_index = 0
    cl = L.CenterlineParam(D)
    mw = np.array(tb.matrix_world)
    lay = me.uv_layers.active.data
    Vw = np.array([me.vertices[l.vertex_index].co[:] for l in me.loops]) @ mw[:3, :3].T + mw[:3, 3]
    lat, arc = cl.uv(Vw[:, 0], Vw[:, 1])
    if TEE_BOX_SEAMLESS and TEE_STEP:
        # round 6: the FAIRWAY mapping evaluated where the camera's ray over the plate's edge meets the fairway (UV_tee(p) = UV_fairway(C + k (p - C))): the 0.35 m band of fairway the
        # 10 cm step hides from the address camera is not skipped by the texture, so grass and stones run through the edge as seen from the tee
        k_h, cxy, _zt, _zf = tee_hidden_k()
        rep["hidden_k"] = round(k_h, 4)
        lat, arc = cl.uv(cxy[0] + k_h * (Vw[:, 0] - cxy[0]), cxy[1] + k_h * (Vw[:, 1] - cxy[1]))
        UV = np.stack([lat / tile + fshift, arc / tile], 1)
    elif TEE_BOX_SEAMLESS:
        UV = np.stack([lat / tile + fshift, arc / tile], 1)                  # exactly the FAIRWAY mapping (integer V shifts do not matter: the texture repeats)
    else:
        UV = np.stack([arc / tile + tee_phase, -lat / tile], 1)
    lay.foreach_set("uv", UV.ravel())
    me.update()
    rep["tee_box_materials_before"] = before
    rep["tee_box_faces"] = len(me.polygons)
    rep["density_ratio"] = {nm: round(_uv_world_density(bpy.data.objects[nm], "LK_FAIRWAY")[0] * tile, 3) for nm in ("FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN_APRON", "TEE_BOX")
                            if _uv_world_density(bpy.data.objects[nm], "LK_FAIRWAY")[1] > 0}
    return rep


def firstcut_transition(D):
    """Pale fairway mapping under/at its edge; the visible outer collar stays rough.

    The frozen first-cut surface extends under the fairway. Material choice is
    based on the actual nearest fairway surface, without moving either mesh.
    """
    ob = bpy.data.objects["FAIRWAY_FIRSTCUT"]
    me = ob.data
    fw = bvh_of_objects([bpy.data.objects["FAIRWAY"]])
    names = [m.name if m else "" for m in me.materials]
    if "LK_FAIRWAY" not in names:
        me.materials.append(L.look_material("LK_FAIRWAY", D))
        names.append("LK_FAIRWAY")
    idx = names.index("LK_FAIRWAY")
    count = 0
    for p in me.polygons:
        q = ob.matrix_world @ p.center
        near = fw.find_nearest(q, .5)
        if near[0] is not None and math.hypot(q.x - near[0].x, q.y - near[0].y) <= .35:
            p.material_index = idx
            count += 1
    me.update()
    _log(f"firstcut transition: {count}/{len(me.polygons)} faces under/within .35m of fairway; outer collar LK_ROUGH")
    return dict(fairway_faces=count, total_faces=len(me.polygons), visible_collar="LK_ROUGH", transition_m=.35)


def _stripe_phase(D, name="FAIRWAY"):
    """(median, share within 0.01 of the median) of U - lateral / tile over the LK_FAIRWAY loops of mesh `name`: the lib writes U = lateral / tile, so this is the stripe
    shift that is really in the UVs (FAIRWAY_STRIPE_SHIFT after mow_pattern)."""
    ob = bpy.data.objects[name]
    me = ob.data
    tile = L.LOOK["LK_FAIRWAY"][2]
    lay = me.uv_layers.active.data
    mw = np.array(ob.matrix_world)
    keep = [li for p in me.polygons if me.materials and me.materials[p.material_index] is not None and me.materials[p.material_index].name.split(".")[0] == "LK_FAIRWAY"
            for li in range(p.loop_start, p.loop_start + p.loop_total)]
    if not keep:
        return float("nan"), 0.0
    Vw = np.array([me.vertices[me.loops[li].vertex_index].co[:] for li in keep]) @ mw[:3, :3].T + mw[:3, 3]
    U = np.array([lay[li].uv[0] for li in keep])
    lat, _arc = L.CenterlineParam(D).uv(Vw[:, 0], Vw[:, 1])
    ph = U - lat / tile
    med = float(np.median(ph))
    return med, float((np.abs(ph - med) < 0.01).mean())


def match_shelf_preview():
    """Blender preview only (Unity builds its water materials by name and the FBX has no colours): the shelf material's base colour is the Unity colour
    (GolfLook LK_WATER_SHALLOW Shallow (27,106,120)) instead of the library's bright turquoise stand-in (40,186,196), which read lighter than the grass in
    the previews (reviewer, low). Returns (old, new) as sRGB triples."""
    mat = bpy.data.materials.get("LK_WATER_SHALLOW")
    if mat is None or not mat.use_nodes:
        return None
    b = mat.node_tree.nodes.get("Principled BSDF")
    if b is None:
        return None
    old = tuple(round(c, 4) for c in b.inputs["Base Color"].default_value[:3])
    b.inputs["Base Color"].default_value = P.rgb(*SHELF_PREVIEW_RGB)
    mat.diffuse_color = P.rgb(*SHELF_PREVIEW_RGB)
    return dict(old_linear=old, new_srgb=SHELF_PREVIEW_RGB)


# ----------------------------------------------------------------------------- round 5: cliff ledges, bunker lip, outcrop flank
SKIN_LEDGE_DEPTH_M = 1.2      # up-facing skin faces within this depth under the grass lip (the capping ledge and the first strata ledge) wear grass
SKIN_LEDGE_MIN_UP = 0.8


def grass_on_skin_ledges(D, skins, depth=None, min_up=None):
    """Reviewer (medium, arch shot): "a black, hard-edged extruded box" at the pad rim. It is the capping ledge of the strata skin (ROCK_SKIN_nn): a horizontal LK_CLIFF face
    (Cliff_C, mean luminance 63) tucked just under the grass lip, seen from above. Up-facing skin faces (normal.z >= 0.8) whose centre is within `depth` m of PLAY_Z now wear LK_ROUGH
    (the grass of the pad's own top and rim), planar UV with the same domain warp as the terrain top, so the ledge reads as turf overhanging the strata instead of a dark slab. Vertices do not
    move; the skin stays a visual shell (ROCK_ prefix, no collider). Returns dict(faces, area_m2, objects)."""
    depth = SKIN_LEDGE_DEPTH_M if depth is None else depth
    min_up = SKIN_LEDGE_MIN_UP if min_up is None else min_up
    pz = D.play_z
    mat = L.look_material("LK_ROUGH", D)
    tile = L.LOOK["LK_ROUGH"][2]
    wp = L.UV_WARP["LK_ROUGH"]
    n_f, area, n_o = 0, 0.0, 0
    for ob in skins:
        me = ob.data
        mw = np.array(ob.matrix_world)
        slot = next((i for i, m in enumerate(me.materials) if m is not None and m.name.split(".")[0] == "LK_ROUGH"), None)
        lay = me.uv_layers.active.data if me.uv_layers else None
        changed = 0
        for p in me.polygons:
            nw = mw[:3, :3] @ np.array(p.normal)
            nl = float(np.linalg.norm(nw))
            if nl < 1e-9 or nw[2] / nl < min_up:
                continue
            zc = float((mw @ np.append(np.array(p.center), 1.0))[2])
            if zc < pz - depth:
                continue
            if slot is None:
                me.materials.append(mat)
                slot = len(me.materials) - 1
            p.material_index = slot
            if lay is not None:
                for li in range(p.loop_start, p.loop_start + p.loop_total):
                    w = mw @ np.append(np.array(me.vertices[me.loops[li].vertex_index].co), 1.0)
                    xw, yw = L._warp(np.array([w[0]]), np.array([w[1]]), *wp)
                    lay[li].uv = (float(xw[0]) / tile, float(yw[0]) / tile)
            changed += 1
            n_f += 1
            area += float(p.area) * float(abs(np.linalg.det(mw[:3, :3]))) ** (2.0 / 3.0)
        if changed:
            n_o += 1
            me.update()
    return dict(faces=n_f, area_m2=round(area, 1), objects=n_o, depth=depth)


def bunker_lip_to_rough(D):
    """Reviewer (low, arch shot): "a bright, saturated, faceted grass ridge with a chevron texture seam" at the foreground bunker lip. BUNKER_01_LIP (collision-named, vertices frozen)
    wore LK_GREEN: Green_C (mean luminance 162) is the brightest, most saturated grass and a directional blade texture, and the lip's crest turns the downhill direction (a V-shaped
    bend of the ring), so the grain forms a chevron where the lit and the shaded face meet. The lip now wears LK_ROUGH (the contract allows LK_GREEN / LK_FAIRWAY / LK_ROUGH / LK_SAND on
    BUNKER_nn_LIP): darker, isotropic turf, planar UV (the lib's rule for LK_ROUGH: one UV frame over the crest, no flip). Only the material slot and the UVs change (compare_ground /
    H_PLAY_UNCHANGED see no vertex move). Returns dict. The landmark camera of the arch shot no longer frames the lip in the foreground either (make_landmarks.py)."""
    ob = bpy.data.objects["BUNKER_01_LIP"]
    me = ob.data
    before = sorted({m.name for m in me.materials if m})
    me.materials.clear()
    me.materials.append(L.look_material("LK_ROUGH", D))
    for p in me.polygons:
        p.material_index = 0
    L.assign_uvs(D, objects=[ob], fill_missing=False)
    return dict(before=before, after=sorted({m.name for m in me.materials if m}), faces=len(me.polygons))


def _outcrop_rim(outcrop, top):
    """World-space ordered rim (n x 2 array, counter-clockwise from above) of the outcrop's flat grass top (the cap face(s) at z = top)."""
    me = outcrop.data
    mw = np.array(outcrop.matrix_world)
    me.calc_loop_triangles()
    cnt = {}
    for p in me.polygons:
        nw = mw[:3, :3] @ np.array(p.normal)
        zc = float((mw @ np.append(np.array(p.center), 1.0))[2])
        if nw[2] / max(float(np.linalg.norm(nw)), 1e-9) < 0.95 or abs(zc - top) > 0.02:
            continue
        vs = [me.loops[i].vertex_index for i in range(p.loop_start, p.loop_start + p.loop_total)]
        for a, b in zip(vs, vs[1:] + vs[:1]):
            e = (min(a, b), max(a, b))
            cnt[e] = cnt.get(e, 0) + 1
    edges = [e for e, c in cnt.items() if c == 1]
    if not edges:
        return np.zeros((0, 2))
    nxt = {}
    for a, b in edges:
        nxt.setdefault(a, []).append(b)
        nxt.setdefault(b, []).append(a)
    start = edges[0][0]
    order, prev, cur = [start], None, start
    while True:
        cand = [v for v in nxt[cur] if v != prev]
        if not cand:
            break
        nv = cand[0]
        if nv == start:
            break
        order.append(nv)
        prev, cur = cur, nv
        if len(order) > len(edges) + 2:
            break
    P2 = np.array([(mw @ np.append(np.array(me.vertices[i].co), 1.0))[:2] for i in order])
    a2 = 0.5 * float(np.dot(P2[:, 0], np.roll(P2[:, 1], -1)) - np.dot(P2[:, 1], np.roll(P2[:, 0], -1)))
    return P2 if a2 > 0 else P2[::-1]


def _resample_closed(P2, step):
    seg = np.roll(P2, -1, axis=0) - P2
    ln = np.hypot(seg[:, 0], seg[:, 1])
    S = np.concatenate([[0.0], np.cumsum(ln)])
    Ltot = float(S[-1])
    k = max(3, int(round(Ltot / step)))
    out = []
    for j in range(k):
        s_ = (j + 0.5) * Ltot / k
        i = min(int(np.searchsorted(S, s_, side="right")) - 1, len(P2) - 1)
        t = (s_ - S[i]) / max(float(ln[i]), 1e-9)
        pt = P2[i] + seg[i] * t
        tg = seg[i] / max(float(ln[i]), 1e-9)
        out.append((pt, np.array([tg[1], -tg[0]])))            # counter-clockwise rim: the outward normal is the tangent turned right
    return out


OUTCROP_VINE_SPACING = 2.4


def vines_on_outcrop(D, arch, spacing=None, seed=9):
    """Round 5 (reviewer, medium, arch shot: "the island floor is a big flat grey slab"): from the pad rim the outcrop's camera-side flank is a sheer 2 m wall, seen at 15-20 degrees from
    above, which reads as a horizontal grey slab. PLANT_VINE_* now hang from the outcrop's grass rim down that wall (the cliffs of needle.jpg carry hanging vines), pushed out so the
    whole drop stays in front of the rock, scaled so the lowest leaf cluster stays at z >= VINE_MIN_TOP_M (the plant gate), skipped where the wall is more than 0.9 m proud of the rim.
    The rim is the outline of the outcrop's flat grass top. Returns the vines."""
    spacing = OUTCROP_VINE_SPACING if spacing is None else spacing
    oc = arch["outcrop"]
    top = arch["centre"][2]
    rim = _outcrop_rim(oc, top)
    if len(rim) < 3:
        return []
    bv = bvh_of_objects([oc])
    ab = bvh_of_objects([arch["arch"]])
    rng = random.Random(D.number * 173 + seed)
    out = []
    z_att = top - 0.2
    cen = rim.mean(axis=0)
    for pt, nrm in _resample_closed(rim, spacing):
        if float(np.dot(nrm, pt - cen)) < 0:
            nrm = -nrm                                          # safety: the normal points away from the rim's centroid
        roll_skip, kind_r, yaw_j, sc_r = rng.random(), rng.random(), rng.uniform(-0.15, 0.15), rng.uniform(0.9, 1.25)
        if roll_skip < 0.12:
            continue
        kind = "PLANT_VINE_M" if kind_r < 0.6 else ("PLANT_VINE_S" if kind_r < 0.85 else "PLANT_VINE_L")
        ln = PL.VINE_LENGTH[kind]
        sz = min(sc_r, (z_att - VINE_MIN_TOP_M) / (VINE_LOWEST_PIECE * ln))
        if sz < 0.55:
            kind = "PLANT_VINE_M"
            ln = PL.VINE_LENGTH[kind]
            sz = min(sc_r, (z_att - VINE_MIN_TOP_M) / (VINE_LOWEST_PIECE * ln))
            if sz < 0.55:
                continue
        zs = [z_att - ln * sz * t for t in (0.0, 0.25, 0.5, 0.75, 1.0)]
        offs = [_face_offset([bv], pt, nrm, z) for z in zs]
        if any(math.isnan(o) for o in offs):
            continue
        # round 8 (the libs agent's carry-over: 8 vines hung 0.6-0.86 m off the wall): the root TOUCHES the wall at its own height (offset = the surface at z_att + 5 mm) and the vine is tilted about its root so
        # that it follows the wall's mean slope (the bank leans out going down; a plumb vine either hung 0.9 m off the rock at the root or sank into the bulge). The older rule pushed the whole drop out in front of the widest bulge.
        ext = ln * sz * VINE_LOWEST_PIECE
        slope = (offs[-1] - offs[0]) / max(ext, 1e-6)                # metres out per metre of drop
        tilt = math.atan(slope)
        if tilt > math.radians(VINE_TILT_MAX_DEG) or tilt < -math.radians(VINE_TILT_MIN_DEG):
            continue
        off = offs[0] + 0.005
        q = (float(pt[0] + nrm[0] * off), float(pt[1] + nrm[1] * off), z_att)
        if ab.find_nearest(Vector(q), 2.0)[0] is not None:       # never in the masonry's footprint
            continue
        yaw = math.atan2(nrm[1], nrm[0]) - math.pi / 2 + yaw_j
        out.append(PL.place(D, kind, q, yaw, (sz, sz * rng.uniform(0.92, 1.08), sz), tilt=(tilt, 0.0)))
    return out


# ----------------------------------------------------------------------------- round 6: the outcrop's near flank (reviewer, medium, arch shot)
OUTCROP_DRESS = True
# the arch landmark camera (Blender metres; chosen by work/postcard-look/v2/hole08/tools/make_landmarks.py, 6 m above the green pad's north-west rim; Unity yards (-10.94, 13.21, 302.93) -> (-28.7, 11.81, 317.0)):
ARCH_CAM = dict(pos=(-10.0, 277.0, 12.08), look=(-26.243, 289.865, 10.8), fov=60.0)
ARCH_SLAB_MAX = 0.02                    # bare outcrop rock (LK_ROCK / LK_CLIFF*) share of the lower 40 % of the arch frame (round 5 build measured 22.7 %: the grey slab; 9.1 % of the whole frame)
OUTCROP_CUTS_M = (0.75, 1.5, 2.3, 3.2)        # horizontal cuts this far under the outcrop's grass top (world z): the library facets span most of the wall, so a face-by-face re-material cannot dress only its upper band
OUTCROP_TURF_M = 2.0                    # the band under the grass top that wears turf
OUTCROP_MIX_M = 3.2                     # ... and the band under it that is patchy turf / rock
OUTCROP_SHOULDER_M = 0.5                # round 8 (reviewer, medium: "a hard polygonal fold across the lower third of the arch shot, a 30-35 % step with no feather"): the flat grass top meets the steep turf bank
OUTCROP_SHOULDER_SEGS = 4               # in a 60-75 degree flat-shaded crease. dress_outcrop bevels the rim edges (offset, segments), then writes SMOOTH custom normals on the turf faces (the top stays exactly up)
OUTCROP_AMP_M = (-0.12, 0.34)           # displacement of the cut vertices, outward (+) / inward (-), a smooth noise field: no flat polygon with straight edges


def dress_outcrop(D, outcrop, top, seed=3):
    """Reviewer (medium, arch shot): "a flat grey stone slab with hard straight edges ... a bare stone plate, the reference shows a grassy island top with shrubs and rubble". The slab is the outcrop's
    camera-side wall: the library mesh ROCK_OUTCROP_A is a hull of 163 big planar facets (one facet spans 2 m x 3 m, normal (0.68, -0.68, 0.27)), seen from the pad rim at 30 degrees. This cuts the
    mesh horizontally at OUTCROP_CUTS_M under the grass top, pushes the cut vertices in / out by a smooth noise field (the wall is no longer one plane, its silhouette is irregular), puts turf
    on the upper band (LK_ROUGH, a grassy bank hanging over the rock), patchy turf / rock on the next one and rock below, and re-writes the box UVs with the props library's own rule (object
    space / the slot's contract tile). Plan positions of the top ring (the grass rim every plant and vine is placed on) do not change. Returns a report dict."""
    me = outcrop.data
    bpy.context.view_layer.update()
    mw = np.array(outcrop.matrix_world)
    sz = float(np.linalg.svd(mw[:3, :3], compute_uv=False).min())
    sxy = float(np.linalg.svd(mw[:3, :3], compute_uv=False).max())
    z0 = float(mw[2, 3])
    names = [m.name.split(".")[0] if m else "" for m in me.materials]
    if "LK_ROUGH" not in names or "LK_ROCK" not in names:
        raise RuntimeError(f"outcrop materials {names}")
    i_rock, i_rough = names.index("LK_ROCK"), names.index("LK_ROUGH")
    n_before = len(me.polygons)
    bm = bmesh.new()
    bm.from_mesh(me)
    cut_z_obj = [((top - c) - z0) / sz for c in OUTCROP_CUTS_M]
    new_verts = set()
    for zo in cut_z_obj:
        r = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], dist=1e-6, plane_co=(0.0, 0.0, zo), plane_no=(0.0, 0.0, 1.0))
        for g in r["geom_cut"]:
            if isinstance(g, bmesh.types.BMVert):
                new_verts.add(g)
    bm.verts.ensure_lookup_table()
    # displacement of the cut vertices: radial from the outcrop axis (object space x, y), world-metre amplitude
    amp_lo, amp_hi = OUTCROP_AMP_M
    n_moved = 0
    for v in new_verts:
        if not v.is_valid:
            continue
        w = mw @ np.append(np.array(v.co[:]), 1.0)
        if w[2] > top - 0.05:
            continue
        nz_ = float(fbm_pt(w[0] * 0.9 + w[2] * 0.35, w[1] * 0.9 - w[2] * 0.2, seed))
        amp = amp_lo + (amp_hi - amp_lo) * nz_
        rad = np.array([v.co.x, v.co.y])
        ln = float(np.hypot(*rad))
        if ln < 1e-6:
            continue
        d = rad / ln
        v.co.x += float(d[0]) * amp / sxy
        v.co.y += float(d[1]) * amp / sxy
        n_moved += 1
    # round 8: a rounded SHOULDER between the flat grass top and the turf bank (bevel of the rim edges: the cap's boundary moves OUTCROP_SHOULDER_M in, the bank's upper edge OUTCROP_SHOULDER_M down)
    n_shoulder = 0
    if OUTCROP_SHOULDER_M > 0:
        bm.faces.ensure_lookup_table()
        topf = set()
        for f in bm.faces:
            c = f.calc_center_median()
            nn = f.normal
            nw = mw[:3, :3] @ np.array([nn.x, nn.y, nn.z])
            if float(nw[2]) / max(float(np.linalg.norm(nw)), 1e-9) > 0.95 and abs(float((mw @ np.array([c.x, c.y, c.z, 1.0]))[2]) - top) < 0.02:
                topf.add(f)
        rim_edges = [e for e in bm.edges if sum(1 for f in e.link_faces if f in topf) == 1]
        if rim_edges:
            nf0 = len(bm.faces)
            rb = bmesh.ops.bevel(bm, geom=rim_edges, offset=OUTCROP_SHOULDER_M / sxy, offset_type='OFFSET', profile=0.5, segments=OUTCROP_SHOULDER_SEGS, affect='EDGES', clamp_overlap=True, loop_slide=True)
            n_shoulder = len(bm.faces) - nf0
            for f in rb["faces"]:                                    # the shoulder is turf, whatever the faces it was cut from carried
                f.material_index = i_rough
    # materials: band + patchy mix
    bm.faces.ensure_lookup_table()
    n_turf = n_mix = 0
    rg = random.Random(D.number * 311 + seed)
    for f in bm.faces:
        c = f.calc_center_median()
        w = mw @ np.array([c.x, c.y, c.z, 1.0])
        nn = f.normal
        nw = mw[:3, :3] @ np.array([nn.x, nn.y, nn.z])
        nw /= max(float(np.linalg.norm(nw)), 1e-9)
        patch = rg.random()
        if f.material_index != i_rock and f.material_index != i_rough:
            continue
        if w[2] > top - 0.05:
            continue
        if nw[2] < -0.05:
            f.material_index = i_rock
            continue
        if w[2] > top - OUTCROP_TURF_M:
            f.material_index = i_rough
            n_turf += 1
        elif w[2] > top - OUTCROP_MIX_M:
            f.material_index = i_rough if patch < 0.5 else i_rock
            n_mix += f.material_index == i_rough
        else:
            f.material_index = i_rock
    bm.to_mesh(me)
    bm.free()
    me.update()
    L._finish_mesh(outcrop, 13.0)
    tiles = [(PL.TILE.get(m.name.split(".")[0]) or 4.0) if m is not None else 4.0 for m in me.materials]
    uv = PL.box_uv_expected(me, tiles)
    me.uv_layers.active.data.foreach_set("uv", uv.ravel())
    me.update()
    nsm = smooth_turf_normals(outcrop, i_rough, top) if OUTCROP_SHOULDER_M > 0 else 0
    return dict(faces_before=n_before, faces_after=len(me.polygons), cut_vertices=len(new_verts), moved=n_moved, turf_faces=n_turf, mix_turf_faces=int(n_mix), cuts_obj_z=[round(z, 3) for z in cut_z_obj],
                shoulder_faces=n_shoulder, smooth_turf_loops=nsm)


def smooth_turf_normals(ob, i_rough, top):
    """Custom (split) normals of the outcrop: the flat top keeps its own up normal; every other LK_ROUGH face (the bevelled shoulder and the turf bank) gets the angle-weighted average of the
    LK_ROUGH faces around each vertex (a soft mound, whatever the hull facets' crease angles), except at the vertices shared with the flat top, which keep the top's up normal (so the top stays
    exactly flat and the shoulder rolls off it); rock faces keep the normals the sharp-edge rule gave them. Object space. Returns the number of loops written."""
    me = ob.data
    me.calc_loop_triangles()
    me.update()
    nP, nL, nV = len(me.polygons), len(me.loops), len(me.vertices)
    cn = np.empty(nL * 3)
    me.corner_normals.foreach_get("vector", cn)
    cn = cn.reshape(-1, 3).copy()
    co = np.empty(nV * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(ob.matrix_world)
    istop, isturf = np.zeros(nP, bool), np.zeros(nP, bool)
    pn = np.zeros((nP, 3))
    for p in me.polygons:
        pn[p.index] = np.array(p.normal[:])
        w = mw @ np.append(np.array(p.center[:]), 1.0)
        nw = mw[:3, :3] @ pn[p.index]
        up = float(nw[2]) / max(float(np.linalg.norm(nw)), 1e-9)
        if up > 0.95 and abs(float(w[2]) - top) < 0.02:
            istop[p.index] = True
        elif p.material_index == i_rough:
            isturf[p.index] = True
    acc = np.zeros((nV, 3))
    for p in me.polygons:
        if not isturf[p.index]:
            continue
        vs = [me.loops[i].vertex_index for i in range(p.loop_start, p.loop_start + p.loop_total)]
        for k, v in enumerate(vs):
            a, b, c = co[vs[k - 1]], co[v], co[vs[(k + 1) % len(vs)]]
            e1, e2 = a - b, c - b
            den = float(np.linalg.norm(e1) * np.linalg.norm(e2))
            ang = math.acos(max(-1.0, min(1.0, float(e1 @ e2) / den))) if den > 1e-12 else 0.0
            acc[v] += pn[p.index] * ang
    topv = {}
    for p in me.polygons:
        if istop[p.index]:
            for i in range(p.loop_start, p.loop_start + p.loop_total):
                topv.setdefault(me.loops[i].vertex_index, pn[p.index])
    n_written = 0
    for p in me.polygons:
        if istop[p.index]:
            for i in range(p.loop_start, p.loop_start + p.loop_total):
                cn[i] = pn[p.index]
        elif isturf[p.index]:
            for i in range(p.loop_start, p.loop_start + p.loop_total):
                v = me.loops[i].vertex_index
                if v in topv:
                    cn[i] = topv[v]
                else:
                    ln = float(np.linalg.norm(acc[v]))
                    cn[i] = acc[v] / ln if ln > 1e-12 else pn[p.index]
                n_written += 1
    cn /= np.maximum(np.linalg.norm(cn, axis=1, keepdims=True), 1e-12)
    me.normals_split_custom_set(cn.tolist())
    me.update()
    return n_written


def fbm_pt(x, y, seed):
    return float(L.fbm(np.array([x]), np.array([y]), 3.2, seed)[0])


# ----------------------------------------------------------------------------- the arch
def _arch_feet_xy(arch_ob, z_max):
    V = world_verts([arch_ob])
    return V[V[:, 2] <= z_max][:, :2]


def _outcrop_fit(outcrop, arch_ob, top):
    """(ok, worst) every foot vertex of the arch has the outcrop top (z = top) within 0.35 m under it."""
    bv = bvh_of_objects([outcrop])
    V = world_verts([arch_ob])
    foot = V[V[:, 2] <= top + 0.05]                        # the bottom face of the plinth
    worst = 0.0
    for x, y, z in foot.tolist():
        hit = bv.ray_cast(Vector((x, y, z + 3.0)), Vector((0, 0, -1)), 8.0)
        if hit[0] is None:
            return False, 99.0
        worst = max(worst, abs(z - hit[0].z))
    return worst < 0.35, worst


def build_arch(D, a):
    """Masonry arch (LK_MASONRY blocks, voussoirs, keystone, hanging vines) on a flat-topped rock outcrop in the sea beside the bunker. The outcrop's
    yaw is the first of 16 candidates whose flat top carries every foot vertex of the arch; the library's random 'fallen' blocks (which can land
    past the outcrop edge) are replaced by blocks dropped onto the outcrop top."""
    cx, cy = P.m(a["x"] + ARCH_MOVE_X_YD, a["d"])
    top = a["base_top_m"] + ARCH_TOP_LIFT_M
    span = a["span_yd"] * YD * ARCH_SCALE
    height = min(a["height_yd"] * YD * ARCH_SCALE, STACK_CAP_M - top - 0.6)
    arch = PL.build_masonry_arch(D, (cx, cy, top), a["facing_deg"], span_m=span, height_m=height, depth_m=ARCH_DEPTH_M, seed=8, base_rock=False, vines=False)
    _remove(arch["fallen"])
    arch_v = arch_vines(D, arch["arch"], arch["vine_points"])
    outcrop, tried = None, []
    for k in OUTCROP_YAW_ORDER:
        yaw = k * math.tau / 16
        oc = PL.place_outcrop(D, cx, cy, top, OUTCROP_R_M, seed=8, yaw=yaw)
        ok, worst = _outcrop_fit(oc, arch["arch"], top)
        tried.append((round(yaw, 2), round(worst, 2)))
        if ok:
            outcrop = oc
            break
        _remove([oc])
    if outcrop is None:
        raise RuntimeError(f"no outcrop yaw carries the arch: {tried}")
    dress = dress_outcrop(D, outcrop, top) if OUTCROP_DRESS else None
    fallen = _fallen_blocks(D, outcrop, arch["arch"], (cx, cy), top, random.Random(8200))
    return dict(dress=dress, outcrop=outcrop, arch=arch["arch"], fallen=fallen, vines=arch_v, vine_points=arch["vine_points"], centre=(cx, cy, top), span=span,
                height=height, facing=a["facing"] if "facing" in a else a["facing_deg"], dims=arch["dims"], yaw_tried=tried)


def _fallen_blocks(D, outcrop, arch_ob, centre, top, rnd, want=4):
    """DRESS_BLOCK_A/B lying on the outcrop top: every corner of the block's footprint has the flat top (z = top) under it, 0.8 m clear of the
    masonry's lowest courses."""
    bv = bvh_of_objects([outcrop])
    feet = _arch_feet_xy(arch_ob, top + 0.6)
    out = []
    for _ in range(200):
        if len(out) >= want:
            break
        ang, rr = rnd.uniform(0, math.tau), rnd.uniform(1.5, 6.0)
        x, y = centre[0] + math.cos(ang) * rr, centre[1] + math.sin(ang) * rr * 0.8
        kind = rnd.choice(("DRESS_BLOCK_A", "DRESS_BLOCK_B"))
        sc = rnd.uniform(0.85, 1.2)
        yaw = rnd.uniform(0, math.tau)
        if len(feet) and float(np.hypot(feet[:, 0] - x, feet[:, 1] - y).min()) < 0.8 + 0.7 * sc:
            continue
        if any(math.hypot(x - o.location.x, y - o.location.y) < 1.6 for o in out):
            continue
        ok = True
        for dx, dy in ((0, 0), (0.7, 0.45), (-0.7, 0.45), (0.7, -0.45), (-0.7, -0.45)):
            px, py = x + (dx * math.cos(yaw) - dy * math.sin(yaw)) * sc, y + (dx * math.sin(yaw) + dy * math.cos(yaw)) * sc
            hit = bv.ray_cast(Vector((px, py, top + 3.0)), Vector((0, 0, -1)), 8.0)
            if hit[0] is None or abs(hit[0].z - top) > 0.02 or hit[1].z < 0.95:
                ok = False
                break
        if not ok:
            continue
        out.append(PL.place(D, kind, (x, y, top - 0.02), yaw, sc, (rnd.uniform(-0.04, 0.04), rnd.uniform(-0.04, 0.04))))
    return out


def outcrop_plants(D, arch):
    """Shrubs, flowers and tufts on the outcrop's grass top, kept off the masonry piers."""
    top = arch["centre"][2]
    feet = _arch_feet_xy(arch["arch"], top + 0.6)
    avoid = []
    if len(feet):
        c = feet.mean(axis=0)
        ev = np.cov((feet - c).T)
        w, v = np.linalg.eigh(ev)
        ax = v[:, 1]                                         # long axis = the span
        u = (feet - c) @ ax
        for lo, hi in ((u.min(), u.min() + 2.4), (u.max() - 2.4, u.max())):    # the two piers
            sel = (u >= lo) & (u <= hi)
            if sel.any():
                q = feet[sel]
                cc = q.mean(axis=0)
                avoid.append((float(cc[0]), float(cc[1]), float(np.hypot(*(q - cc).T).max() + 0.7)))
    for blk in arch["fallen"]:                                # a plant never stands on a fallen block (the plant gate: a rock that floats carries nothing)
        bv = world_verts([blk])
        c = bv[:, :2].mean(axis=0)
        avoid.append((float(c[0]), float(c[1]), float(np.hypot(*(bv[:, :2] - c).T).max() + 0.5)))
    arch["avoid"] = list(avoid)
    reg0 = PL.region_on_objects([arch["outcrop"]], min_up=0.95, inset_m=0.5)

    def reg(rng, n):                                     # only the flat top (z = top), never the flat steps lower down on the rock
        X, Y, Z = reg0(rng, n)
        keep = Z >= top - 0.05
        return X[keep], Y[keep], Z[keep]
    out = PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_C"], 5, region=reg, seed=41, avoid=avoid)
    out += PL.scatter(D, {"PLANT_FLOWER_PURPLE": 2, "PLANT_FLOWER_WHITE": 1}, 10, region=reg, seed=42, avoid=avoid)
    out += PL.scatter(D, {"PLANT_TUFT_A": 2, "PLANT_TUFT_B": 1}, 30, region=reg, seed=43, avoid=avoid, scale_range=TUFT_SCALE_FAR)
    # round 5 (arch shot, "the island floor is a big flat grey slab"): a fringe of shrubs, flowers and tufts along the outcrop's grass rim, so the rim line is broken up and the grass
    # hangs over the wall like the grassy islands of needle.jpg
    rim = _outcrop_rim(arch["outcrop"], top)
    if len(rim) >= 3:
        A_, B_ = rim, np.roll(rim, -1, axis=0)
        ab_ = B_ - A_
        l2_ = np.maximum((ab_ ** 2).sum(1), 1e-12)

        def rim_dist(X, Y):
            px, py = X[:, None], Y[:, None]
            t = np.clip(((px - A_[None, :, 0]) * ab_[None, :, 0] + (py - A_[None, :, 1]) * ab_[None, :, 1]) / l2_[None], 0, 1)
            return np.sqrt((px - (A_[None, :, 0] + ab_[None, :, 0] * t)) ** 2 + (py - (A_[None, :, 1] + ab_[None, :, 1] * t)) ** 2).min(1)

        def reg_rim(rng, n):
            X, Y, Z = reg0(rng, n * 8)
            keep = (Z >= top - 0.05)
            X, Y, Z = X[keep], Y[keep], Z[keep]
            d_ = rim_dist(X, Y)
            k2 = (d_ >= 0.7) & (d_ <= 1.9)
            return X[k2], Y[k2], Z[k2]
        out += PL.scatter(D, ["PLANT_SHRUB_A", "PLANT_SHRUB_C"], 7, region=reg_rim, seed=44, avoid=avoid)
        out += PL.scatter(D, {"PLANT_FLOWER_PURPLE": 2, "PLANT_FLOWER_WHITE": 1, "PLANT_FLOWER_YELLOW": 1}, 10, region=reg_rim, seed=45, avoid=avoid)
        out += PL.scatter(D, {"PLANT_TUFT_A": 2, "PLANT_TUFT_B": 1}, 24, region=reg_rim, seed=46, avoid=avoid, scale_range=TUFT_SCALE_FAR)
    if FRINGE_ON:
        out += outcrop_fringe(D, arch, avoid)
    return out


def outcrop_fringe(D, arch, avoid, clumps=None, seed=None, tri_budget=None):
    """Round 8 (grass liveliness): clumps of 3-6 tall / filler tufts (PLANT_TUFT_D / E / S, the same library meshes as the pad fringe, so they carry the sway data) on the arch island's grass top: 70 % of the
    clump centres in the rim band (0.45-2.4 m inside the rim, so the tufts hang over the shoulder), the rest at the foot of the masonry / the fallen blocks and a few in the open; a clump has its own height
    factor (0.8-1.5 x the library tuft), a common lean direction (<= 12 degrees) and 0.3-0.8 m between members. The island is not a play surface, so the pad fringe's 0.55 yd cap does not apply (top <= OUTCROP_FRINGE_H_MAX).
    Never within 0.45 m of the rim, over the piers (`avoid` discs), the blocks, a shrub / flower / old tuft footprint, nor on a bevel strip lower than 5 cm under the top. Returns the placed objects
    (custom property lk_outcrop_fringe = clump id, not lk_fringe: the pad-fringe gates are about play land)."""
    clumps = OUTCROP_FRINGE_CLUMPS if clumps is None else clumps
    seed = OUTCROP_FRINGE_SEED if seed is None else seed
    tri_budget = OUTCROP_FRINGE_TRI_BUDGET if tri_budget is None else tri_budget
    outcrop, top = arch["outcrop"], arch["centre"][2]
    rim = _outcrop_rim(outcrop, top)
    if len(rim) < 3 or clumps <= 0:
        return []
    bv = bvh_of_objects([outcrop])
    A_, B_ = rim, np.roll(rim, -1, axis=0)
    ab_ = B_ - A_
    l2_ = np.maximum((ab_ ** 2).sum(1), 1e-12)

    def rim_dist(x, y):
        t = np.clip(((x - A_[:, 0]) * ab_[:, 0] + (y - A_[:, 1]) * ab_[:, 1]) / l2_, 0, 1)
        return float(np.sqrt((x - (A_[:, 0] + ab_[:, 0] * t)) ** 2 + (y - (A_[:, 1] + ab_[:, 1] * t)) ** 2).min())

    def top_z(x, y):
        hit = bv.ray_cast(Vector((x, y, top + 3.0)), Vector((0, 0, -1)), 8.0)
        if hit[0] is None or hit[2] is None or hit[1].z < 0.95 or hit[0].z < top - 0.05:
            return None
        return float(hit[0].z)
    occ = PL._occ(D)
    rng = random.Random(D.number * 977 + seed)
    kinds_t, kinds_f = ("PLANT_TUFT_D", "PLANT_TUFT_E"), ("PLANT_TUFT_S",)
    dims = {k: PL._lib_dims(k) for k in kinds_t + kinds_f}
    lo, hi = rim.min(axis=0), rim.max(axis=0)
    centres, out, tris = [], [], 0
    lean_max = math.radians(12.0)
    foot = [(c[0], c[1], c[2]) for c in avoid]
    for ci in range(clumps * 40):
        if len([1 for _ in centres]) >= clumps or tris >= tri_budget:
            break
        mode = rng.random()
        if mode < 0.7:                                                  # the rim band: draw until the distance fits
            x, y = rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1])
            d = rim_dist(x, y)
            if not (0.45 <= d <= 2.4):
                continue
        elif mode < 0.9 and foot:                                       # the foot of a pier / block
            ax_, ay_, ar_ = foot[rng.randrange(len(foot))]
            a = rng.uniform(0, math.tau)
            rr = ar_ + rng.uniform(0.2, 1.0)
            x, y = ax_ + rr * math.cos(a), ay_ + rr * math.sin(a)
            if rim_dist(x, y) < 0.45:
                continue
        else:                                                           # a few in the open
            x, y = rng.uniform(lo[0], hi[0]), rng.uniform(lo[1], hi[1])
            if rim_dist(x, y) < 0.45 + 1.0:
                continue
        zc = top_z(x, y)
        if zc is None or any(math.hypot(x - ax_, y - ay_) < ar_ + 0.5 for ax_, ay_, ar_ in foot):
            continue
        if any(math.hypot(x - cx_, y - cy_) < 1.35 for cx_, cy_ in centres):
            continue
        if any(math.hypot(x - ox, y - oy) < 0.6 * orr + 0.3 for ox, oy, orr in occ):
            continue
        centres.append((x, y))
        n = rng.randint(3, 6)
        fc = rng.uniform(0.8, 1.5)
        phi = rng.uniform(0, math.tau)
        k_in = 0
        for m_ in range(n * 3):
            if k_in >= n:
                break
            a = rng.uniform(0, math.tau)
            rr = rng.uniform(0.0 if k_in == 0 else 0.3, 0.8)
            qx, qy = x + rr * math.cos(a), y + rr * math.sin(a)
            zq = top_z(qx, qy)
            if zq is None or rim_dist(qx, qy) < 0.45 or any(math.hypot(qx - ax_, qy - ay_) < ar_ + 0.35 for ax_, ay_, ar_ in foot):
                continue
            kind = rng.choice(kinds_t) if rng.random() < 0.4 else kinds_f[0]
            hh, rf, tr = dims[kind]
            if tris + tr > tri_budget:
                break
            f = max(0.55, min(fc * rng.uniform(0.9, 1.1), (OUTCROP_FRINGE_H_MAX + 0.02) / max(hh, 1e-3)))
            th = min(lean_max, max(math.radians(2.0), lean_max * math.sqrt(rng.random())))
            ang = phi + rng.uniform(-0.6, 0.6)
            yaw = rng.uniform(0, math.tau)
            ob = PL.place(D, kind, (qx, qy, zq - 0.02), yaw, (f * rng.uniform(0.94, 1.06), f * rng.uniform(0.94, 1.06), f))
            Rm = Matrix.Rotation(th, 3, Vector((-math.sin(ang), math.cos(ang), 0.0))) @ Matrix.Rotation(yaw, 3, 'Z')
            ob.rotation_euler = Rm.to_euler()
            ob["lk_outcrop_fringe"] = len(centres)
            occ.append((qx, qy, 0.5 * rf * f))
            out.append(ob)
            tris += tr
            k_in += 1
    return out


def _lib_components(kind):
    """Welded connected pieces of a library mesh: list of vertex-index arrays (the verifier judges a plant piece by piece)."""
    me = PL.library_object(kind).data
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    key = {}
    wid = np.array([key.setdefault(tuple(np.round(c, 3)), len(key)) for c in co.tolist()])
    parent = list(range(len(key)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    for p_ in me.polygons:
        vs = [wid[v] for v in p_.vertices]
        for a in vs[1:]:
            ra, rb = find(vs[0]), find(a)
            if ra != rb:
                parent[ra] = rb
    groups = {}
    for i, w in enumerate(wid.tolist()):
        groups.setdefault(find(w), []).append(i)
    return [np.array(g) for g in groups.values()], co


def crown_tufts(D, stack_objs):
    """Round 8 (libs hand-off, review 3: 'sea-stack caps are faceted saturated green hats'): PL.tuftify_crowns puts dark green / olive tufts on the top CROWN_M of every spire. The hull facets of a crown are steep in
    places (normal.z 0.3), and the verifier's NO_PLANT_ON_WATER wants an UP-FACING (normal.z > 0.5) rock top under the centre of every blade piece (within 0.5 m below the lowest piece, within 6 m below the others), so the
    candidates (a generous batch) are judged the same way here and the ones a blade of which hangs over a steep facet are removed; the rest are kept up to CROWN_TUFTS_TRI_BUDGET. Returns dict(objs, tris, shrubs, candidates, pruned)."""
    spires = [o for o in stack_objs if o.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE", "ROCK_NEEDLE"))]
    cand = PL.tuftify_shrubs(D, spires, per=CROWN_TUFTS_PER, kinds=("PLANT_TUFT_B", "PLANT_TUFT_T", "PLANT_TUFT_F"),
                             seed=8, tri_budget=4 * CROWN_TUFTS_TRI_BUDGET, top_only_m=CROWN_M, height_range=CROWN_TUFT_H)
    bpy.context.view_layer.update()
    rocks = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("ROCK_") and not o.name.startswith("ROCK_SKIN_") and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
    V, F = _grass_tri_soup(rocks, up_only=0.5)
    bv = BVHTree.FromPolygons(V, F)
    comps = {}
    keep, pruned, tris = [], 0, 0
    for o in cand["objs"]:
        k = o.data.name
        if k not in comps:
            comps[k] = _lib_components(k)
        groups, co = comps[k]
        M = np.array(o.matrix_world)
        W = co @ M[:3, :3].T + M[:3, 3]
        pcs = [(float(W[g, 2].min()), W[g].min(0), W[g].max(0)) for g in groups]
        low = min(range(len(pcs)), key=lambda i: pcs[i][0])
        ok = True
        for i, (zl, lo, hi) in enumerate(pcs):
            cx, cy = 0.5 * (lo[0] + hi[0]), 0.5 * (lo[1] + hi[1])
            hit = bv.ray_cast(Vector((float(cx), float(cy), float(zl) + 1.0)), Vector((0, 0, -1)))
            if hit[0] is None or not ((zl - 0.5 <= hit[0].z <= zl + 0.4) if i == low else (zl - 6.0 <= hit[0].z <= zl + 0.4)):
                ok = False
                break
        o.data.calc_loop_triangles()
        tr = len(o.data.loop_triangles)
        if ok and tris + tr <= CROWN_TUFTS_TRI_BUDGET:
            keep.append(o)
            tris += tr
        else:
            pruned += 1
            bpy.data.objects.remove(o, do_unlink=True)
    return dict(objs=keep, tris=tris, shrubs=cand["shrubs"], candidates=len(cand["objs"]), pruned=pruned)


# ----------------------------------------------------------------------------- sea stacks
def _stack_ok(D, objs):
    """(ok, detail): the needle rules for one stack cluster: >= 25 yd from the centerline, outside every water ellipse, not on land."""
    h = D.hole
    V = world_verts(objs)
    xy = V[:, :2] / YD
    far = min(h.dist_center((float(x), float(y))) for x, y in xy.tolist())
    in_w = sum(1 for x, y in xy.tolist() if any(k[0] == "water" and h.in_ellipse(k, (x, y)) for k in h.hazards))
    on_l = sum(1 for x, y in xy.tolist() if h.on_land((x, y)))
    return far >= STACK_MIN_CENTERLINE_YD and in_w == 0 and on_l == 0, (far, in_w, on_l, float(V[:, 2].max()))


def _min_scale(objs):
    return min(float(np.linalg.svd(np.array(o.matrix_world)[:3, :3], compute_uv=False).min()) for o in objs)


def _needle_metrics(ob):
    """Actual geometry diagnostics; the unchanged verifier remains acceptance authority."""
    me = ob.data
    me.calc_loop_triangles()
    co = np.array([v.co[:] for v in me.vertices], float)
    tr = np.array([q.vertices[:] for q in me.loop_triangles], int)
    height = float(co[:, 2].max())
    def width(z):
        q = PL.mesh_section(co, tr, z)
        return float(max(np.ptp(q[:, 0]), np.ptp(q[:, 1]))) if len(q) else 0.0
    base = max(width(z) for z in np.linspace(0.0, height * 0.1, 9))
    q = np.unique(np.round(co[np.unique(tr)], 4), axis=0)
    ev = np.maximum(np.linalg.eigvalsh(np.cov(q.T)), 1e-12)
    return dict(height_m=height, base_m=base, height_base=height / max(base, 1e-9),
                width90_width10=width(height * .9) / max(width(height * .1), 1e-9),
                pca_r3=float(np.sqrt(ev[0] / ev[-1])), polygons=len(me.polygons), triangles=len(tr))


def place_stack(D, st):
    """One final-sized continuous needle, with finite geometry-only footprint retries."""
    x_yd, d_yd = STACK_MOVE.get(st["seed"], (st["x"], st["d"]))
    x, y = P.m(x_yd, d_yd)
    H = STACK_HEIGHT_M.get(st["seed"], stack_height_m(st["height_yd"]))
    width = min(5.6, H / 3.15)
    det = None
    for attempt in range(8):
        res = PL.build_needle(D, (x, y, 0.0), height_m=H, base_width_m=width, seed=st["seed"],
                              name=f"ROCK_NEEDLE_{st['seed']:02d}", surf=False)
        needle = res["needle"]
        # One actual breaking patch per foot, rather than three overlapping islands.
        # Other independent shoreline surf remains generated by build_sea.
        rr = float(needle['lk_v3_base_width']) * .52
        aa = random.Random(PL._seed_int('needle_surf', st['seed'])).uniform(-.22,.22)
        cc = (x + rr * math.cos(aa), y + rr * math.sin(aa))
        patch = L.surf_patch(D, cc, radius_m=min(1.8, rr * .65), seed=st['seed'])
        res['surf'] = [] if patch is None else [patch]
        metrics = _needle_metrics(needle)
        _log(f"needle {st['seed']} attempt {attempt}: {metrics}")
        ok, det = _stack_ok(D, [needle])
        if ok and det[3] <= STACK_CAP_M + 1e-6 and _min_scale([needle]) >= 0.505:
            return dict(objs=[needle], surf=res["surf"], H=H, seed=st["seed"], xy=(x, y),
                        radius=width * .5, detail=det, reroll=0, metrics=metrics)
        _remove([needle] + res["surf"])
        width *= 0.88
    raise RuntimeError(f"stack {st['seed']}: no footprint satisfies the needle rules (last {det})")


# ----------------------------------------------------------------------------- vines on the skin
def _face_offset(bvhs, p, n, z, reach=7.0):
    """Outward offset (m, along n from the lip point p) of the outermost skin / terrain surface at height z (nan = none)."""
    o = Vector((p[0] + n[0] * reach, p[1] + n[1] * reach, z))
    d = Vector((-n[0], -n[1], 0.0))
    best = None
    for b in bvhs:
        h = b.ray_cast(o, d, reach + 6.0)
        if h[0] is not None and (best is None or h[3] < best):
            best = h[3]
    return float("nan") if best is None else reach - best


VINE_LOWEST_PIECE = 0.93         # the lowest leaf cluster of a library vine reaches about 0.93 x its length below the attachment point
VINE_MIN_TOP_M = 3.1             # NO_PLANT_ON_WATER: every connected piece of a vine has its top at z >= 3 m (and a surface within 1.5 m)


def vines_on_skin(D, spacing=4.4, seed=6, skip=0.2, avoid=()):
    """PLANT_VINE_* hung on the cliff skin: attachment on the skin just under the grass lip (z = PLAY_Z - 0.4), the vine pushed out so its whole drop
    stays in front of the skin (outermost face over its length + 8 cm), skipped where the face is more than 0.9 m proud of the attachment
    or where the cliff is within `avoid` (x, y, r) discs (the waterfall). The scale is fitted so that the lowest leaf cluster stays at z >= 3.1 m
    (the plant gate reads every piece of a vine as an attachment point that must be >= 3 m up and within 1.5 m of a surface). Library vines,
    scale 0.5-2, never stretched."""
    pz = D.play_z
    sk = L._bvh_of(L._skin_objs())
    tr = L._bvh_of([L._terrain_ob(D)])
    rng = random.Random(D.number * 131 + seed)
    out = []
    z_att = pz - 0.4
    for li, (Pl, _) in enumerate(L.terrain_top_loops(D)):
        Pc, s, Lp, N, _ = L._loop_columns(Pl, 0.5)
        k = max(1, int(Lp / spacing))
        for j in range(k):
            s_t = (j + 0.5 + rng.uniform(-0.35, 0.35)) * Lp / k
            i = int(np.searchsorted(s, s_t)) % len(Pc)
            roll_skip, kind_r, yaw_j, sc_r = rng.random(), rng.random(), rng.uniform(-0.15, 0.15), rng.uniform(0.9, 1.35)
            if roll_skip < skip:
                continue
            p, n = Pc[i], N[i]
            if any(math.hypot(p[0] - ax, p[1] - ay) <= ar for ax, ay, ar in avoid):
                continue
            if P.signed_dist_m(D, np.array([p[0] + n[0] * 3.0]), np.array([p[1] + n[1] * 3.0]))[0] <= 0:
                continue                                           # the cliff here faces land (a neck / inside a pad notch)
            kind = "PLANT_VINE_L" if kind_r < 0.35 else ("PLANT_VINE_M" if kind_r < 0.8 else "PLANT_VINE_S")
            ln = PL.VINE_LENGTH[kind]
            sz = min(sc_r, (z_att - VINE_MIN_TOP_M) / (VINE_LOWEST_PIECE * ln))
            if sz < 0.55:
                continue
            zs = [z_att - ln * sz * t for t in (0.0, 0.25, 0.5, 0.75, 1.0)]
            offs = [_face_offset([sk, tr], p, n, z) for z in zs]
            if any(math.isnan(o) for o in offs):
                continue
            off = max(offs) + 0.08
            if off - offs[0] > 0.9:
                continue
            q = (p[0] + n[0] * off, p[1] + n[1] * off, z_att)
            yaw = math.atan2(n[1], n[0]) - math.pi / 2 + yaw_j
            out.append(PL.place(D, kind, q, yaw, (sz, sz * rng.uniform(0.92, 1.08), sz)))
    return out


def arch_vines(D, arch_ob, vine_points, seed=8):
    """Vines on the masonry arch, hung from the library's attachment points (voussoir undersides, pier faces): the longest kind and scale whose every
    leaf cluster stays within 1.3 m of the masonry (a long vine hanging free in the opening has clusters 3 m from any surface: the plant gate rejects that)
    and at z >= 3.1 m. Returns the placed vines."""
    bv = bvh_of_objects([arch_ob])
    rng = random.Random(D.number * 71 + seed)
    out = []
    for (p, n) in vine_points:
        order = ["PLANT_VINE_L", "PLANT_VINE_M", "PLANT_VINE_S"]
        start = rng.choice((0, 0, 1))
        sc_r = rng.uniform(0.9, 1.25)
        for kind in order[start:]:
            ln = PL.VINE_LENGTH[kind]
            sz = min(sc_r, (p[2] - VINE_MIN_TOP_M) / (VINE_LOWEST_PIECE * ln))
            if sz < 0.55:
                continue
            ok = True
            for t in np.linspace(0.1, VINE_LOWEST_PIECE * ln * sz, 9):
                q = Vector((p[0] + n[0] * 0.25, p[1] + n[1] * 0.25, p[2] - t))
                r = bv.find_nearest(q, 1.4)
                if r[0] is None:
                    ok = False
                    break
            if ok:
                yaw = math.atan2(n[1], n[0]) - math.pi / 2
                out.append(PL.place(D, kind, (p[0] + n[0] * 0.02, p[1] + n[1] * 0.02, p[2]), yaw, (sz, sz, sz)))
                break
    return out


# ----------------------------------------------------------------------------- the waterfall
def _inside_rock(bvhs, v, n):
    """True when point v lies inside the skin / terrain solid (the nearest surface along -n is seen from behind)."""
    d = Vector((-n[0], -n[1], 0.0))
    best = None
    for b in bvhs:
        h = b.ray_cast(Vector(v), d, 8.0)
        if h[0] is not None and (best is None or h[3] < best[3]):
            best = h
    if best is None:
        return False
    return Vector(best[1]).dot(d) > 0.0           # the face normal points the way we look: we are behind it, inside the rock


def build_waterfall(D, hint_m):
    """WATER_FALL_nn: a sheet from the cliff lip down to the sea (LK_FALL, static, no collider). The sheet starts on the skin face just under the lip and
    arcs out by `reach` (searched upward until no vertex lies inside the skin / wall), so it never sinks into the rock. Returns dict(top, foot, out, ...)."""
    pz = D.play_z
    fp = L.open_shore_point(D, hint_m, free_m=40.0)
    if fp is None:
        raise RuntimeError("no open shore point for the waterfall")
    x, y, nx, ny = fp
    sk = L._bvh_of(L._skin_objs())
    tr = L._bvh_of([L._terrain_ob(D)])
    n = (nx, ny)
    t = (-ny, nx)
    z_top = pz - 0.3
    w = WATERFALL_WIDTH_M
    offs = []
    for lat in np.linspace(-0.6 * w, 0.6 * w, 7):
        pp = (x + t[0] * lat, y + t[1] * lat)
        for z in (pz - 0.45, pz - 0.8, pz - 1.4):
            o = _face_offset([sk, tr], pp, n, z)
            if not math.isnan(o):
                offs.append(o)
    off0 = (max(offs) if offs else 0.0) + 0.12
    top = (x + nx * off0, y + ny * off0, z_top)
    last = None
    for reach in (2.4, 2.8, 3.2, 3.6, 4.0, 4.6):
        sheet = PL.build_waterfall_sheet(D, top, n, width_m=w, foot_z=0.0, widen=WATERFALL_WIDEN, reach_m=reach, seed=3, cols=10, rows=16)
        V = world_verts([sheet])
        bad = sum(1 for v in V.tolist() if _inside_rock([sk, tr], v, n))
        last = (reach, bad)
        if bad == 0:
            foot = PL.waterfall_foot(top, n, w, 0.0, WATERFALL_WIDEN, reach)
            return dict(obj=sheet, top=top, out=n, reach=reach, foot=foot["pos"], width=w, foot_width=foot["width"], inside=0,
                        lip=(x, y), search=last)
        bpy.data.objects.remove(sheet, do_unlink=True)
    raise RuntimeError(f"waterfall sheet always inside the skin: {last}")


def build_fall_impact(D, fall):
    """One real low foam patch at the waterfall impact; raised mist is absent.

    Two high-surf fan attempts failed unchanged required water/foot predicates.
    This restores horizontal impact foam instead of relabelling a raised fan.
    The optional connected 7.8cm bump is below the actual0.20m water-plane cap.
    """
    foot=np.array(fall['foot'][:2],float)
    ob=L.surf_patch(D,tuple(foot),radius_m=4.0,seed=0,z=.12)
    if ob is None:raise RuntimeError('waterfall impact foam was not built')
    for v in ob.data.vertices:
        q=np.array(v.co[:2],float)-foot
        lift=.078*math.exp(-float(q@q)/(.85**2))
        v.co.z+=lift
    ob.data.update()
    ob['lk_v3_connected_impact_foam']=True
    ob['lk_v3_impact_foam_lift_m']=.078
    ob['lk_v3_mist_absent']=True
    return ob


def build_fall_vapour(D, fall):
    """One actual curved textured vapour wisp, separate from the impact foam.

    The existing Smoke_C/GolfSurf alpha and edge/depth feathering render the
    aqueous vapour; it uses LK_SMOKE rather than foam vertex-alpha geometry.
    Visibility and resemblance remain unmeasured until the native proof.
    """
    p=(float(fall['foot'][0]),float(fall['foot'][1]),.14)
    ob=PL.build_smoke_cards(D,[(p,2.6,1.5)],seed=808,face=(0.,0.))[0]
    nm=L._next_name(D,'DRESS_SMOKE_MIST')
    ob.name=nm;ob.data.name=nm
    ob['lk_v3_waterfall_vapour']=True
    ob['lk_v3_mist_appearance']='UNMEASURED'
    return [ob]


# ----------------------------------------------------------------------------- the hole's own gates (re-expressed for the library pieces)
def _piece_objs(info):
    """name -> object for every rock-like piece built here (stacks, outcrop, foot / rim rocks, arch masonry, fallen blocks)."""
    pcs = {}
    for st in info["stacks"]:
        for o in st["objs"]:
            pcs[o.name] = o
    for o in info["rocks"]:
        pcs[o.name] = o
    a = info["arch"]
    pcs[a["outcrop"].name] = a["outcrop"]
    pcs[a["arch"].name] = a["arch"]
    for o in a["fallen"]:
        pcs[o.name] = o
    return pcs


# ----------------------------------------------------------------------------- round 8: the grass fringe gates (ported from postcard_props_smoke.fringe_checks / fringe_structure, run on THIS hole's live scene)
FRINGE_MIN_COUNT = 300
GRASS_PLAY_PREFIXES = ("FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH", "DRESS_PATH")
GRASS_GROUND_PREFIXES = ("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER")


def _scene_objs(prefixes):
    return [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(prefixes) and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]


def _grass_tri_soup(objs, up_only=None):
    V, F = [], []
    for o in objs:
        co, tris, _tp = PL._np_mesh(o.data)
        mw = np.array(o.matrix_world)
        w = co @ mw[:3, :3].T + mw[:3, 3]
        if up_only is not None:
            nv = np.cross(w[tris[:, 1]] - w[tris[:, 0]], w[tris[:, 2]] - w[tris[:, 0]])
            tris = tris[nv[:, 2] > up_only * np.maximum(np.linalg.norm(nv, axis=1), 1e-12)]
        base = len(V)
        V.extend(map(tuple, w.tolist()))
        F.extend(tuple(t) for t in (tris + base).tolist())
    return V, F


def grass_checks(D, objs, balls, corridor_m, top_cap=0.503, cap_radius=12.0):
    """The three fringe safety checks as {gate: (ok, detail)}: every vertex of every tuft ray-cast onto the play meshes / the sea / the shot balls, the pin and the markers; tuft tops in the ball corridor.
    objs = tuft instances (so the negative controls can inject bad ones)."""
    bpy.context.view_layer.update()
    cache = {}
    W = {}
    for o in objs:
        co = cache.get(o.data.name)
        if co is None:
            co = np.empty(len(o.data.vertices) * 3)
            o.data.vertices.foreach_get("co", co)
            co = co.reshape(-1, 3)
            cache[o.data.name] = co
        M = np.array(o.matrix_world)
        W[o.name] = co @ M[:3, :3].T + M[:3, 3]
    pobs = _scene_objs(GRASS_PLAY_PREFIXES)
    V, F = _grass_tri_soup(pobs)
    play = BVHTree.FromPolygons(V, F) if F else None
    pnames = [o.name for o in pobs]
    gobs = _scene_objs(GRASS_GROUND_PREFIXES)
    V2, F2 = _grass_tri_soup(gobs, up_only=0.2)
    gb = BVHTree.FromPolygons(V2, F2)

    def ground_z(x, y):
        hit = gb.ray_cast(Vector((x, y, 60.0)), Vector((0, 0, -1)), 200.0)
        return None if hit[0] is None else float(hit[0].z)
    px, py = D.hole.pin[0] * YD, D.hole.pin[1] * YD
    markers = [(o.matrix_world.translation.x, o.matrix_world.translation.y) for o in bpy.data.objects
               if o.name.startswith(("TEE_MARKER", "HOLE_CUP", "BALL_START", "FLAG")) and "ASSET_LIBRARY" not in [c.name for c in o.users_collection]]
    out = {}
    viol = []
    for o in objs:
        for x, y, _z in W[o.name][::2]:
            if play is not None and play.ray_cast(Vector((float(x), float(y), D.play_z + 30.0)), Vector((0, 0, -1)), 80.0)[0] is not None:
                viol.append((o.name, round(float(x), 2), round(float(y), 2)))
                break
    out["GRASS_NOT_ON_PLAY_SURFACES"] = (not viol, f"{len(objs)} fringe tufts, every vertex (2 of 3 sampled) ray-cast down onto {len(pnames)} play meshes ({', '.join(sorted({n.rstrip('0123456789_') for n in pnames}))}): "
                                         f"{len(viol)} over a play surface {viol[:3]}")
    X = np.array([o.location.x for o in objs])
    Y = np.array([o.location.y for o in objs])
    wv = np.concatenate([W[o.name][:, :2] for o in objs]) if objs else np.zeros((0, 2))
    water_v = int((P.lie_codes_m(D, wv[:, 0], wv[:, 1]) == P.LIE_WATER).sum()) if len(wv) else 0
    sd = P.signed_dist_m(D, X, Y) if len(X) else np.zeros(0)
    dz, zmin = [], 1e9
    for o in objs:
        g = ground_z(o.location.x, o.location.y)
        dz.append(99.0 if g is None else abs(o.location.z - g))
        zmin = min(zmin, o.location.z)
    out["GRASS_NOT_ON_WATER"] = (water_v == 0 and (len(sd) == 0 or float(sd.max()) <= -0.8) and (not dz or max(dz) <= 0.05) and zmin >= 5.0,
                                 f"{len(objs)} fringe tufts / {len(wv)} vertices: vertices over water {water_v}; nearest origin to a shore / lip edge {-float(sd.max()) if len(sd) else 0:.2f} m (>= 0.8); "
                                 f"origin vs the ray-cast ground max |dz| {max(dz) if dz else 0:.3f} m (<= 0.05); lowest origin z {zmin:.2f} (>= 5.0)")
    worst_ball, worst_pin, worst_mk, bad_top, top_max_c, top_max_all = 99.0, 99.0, 99.0, [], 0.0, 0.0
    for o in objs:
        w = W[o.name]
        xy = w[:, :2]
        for bx, by, br in balls:
            worst_ball = min(worst_ball, float(np.hypot(xy[:, 0] - bx, xy[:, 1] - by).min()) - br)
        worst_pin = min(worst_pin, float(np.hypot(xy[:, 0] - px, xy[:, 1] - py).min()) - 6.0)
        for mx_, my_ in markers:
            worst_mk = min(worst_mk, float(np.hypot(xy[:, 0] - mx_, xy[:, 1] - my_).min()) - 1.5)
        g = ground_z(o.location.x, o.location.y)
        top = float(w[:, 2].max()) - (g if g is not None else D.play_z)
        top_max_all = max(top_max_all, top)
        off = float(P._lie_all(D, np.array([o.location.x]), np.array([o.location.y]))[1][0]) * YD
        near = any(math.hypot(o.location.x - bx, o.location.y - by) <= cap_radius for bx, by, _r in balls)
        if off <= corridor_m or near:
            top_max_c = max(top_max_c, top)
            if top > top_cap + 0.004:
                bad_top.append((o.name, round(top, 3)))
    out["GRASS_BALL_CLEAR"] = (worst_ball >= -1e-6 and worst_pin >= -1e-6 and worst_mk >= -1e-6 and not bad_top,
                               f"{len(balls)} shot-ball zones {[(round(b[0] / YD, 1), round(b[1] / YD, 1), b[2]) for b in balls]} (course yd, r m): nearest tuft vertex vs the "
                               f"balls (worst margin {worst_ball:+.2f} m), margin to the 6 m pin disc {worst_pin:+.2f} m, to the 1.5 m markers {worst_mk:+.2f} m; tops in the ball corridor "
                               f"(<= {corridor_m:.1f} m off the centerline or <= {cap_radius:.0f} m from a ball): max {top_max_c:.3f} m (<= {top_cap} = 0.55 yd), over {bad_top[:3] or 'none'}; whole hole max {top_max_all:.2f} m")
    return out


def grass_structure(objs, r):
    """GRASS_FRINGE_PRESENT numbers: counts, clump sizes, lean, heights, not a grid, coverage of the rough edge."""
    cnt = len(objs)
    byc = {}
    for o in objs:
        byc.setdefault(o["lk_fringe"], []).append(o)
    sizes = np.array([len(v) for v in byc.values()])
    in_clump = int(sum(s_ for s_ in sizes if s_ >= 3))
    P_ = np.array([[o.location.x, o.location.y] for o in objs])
    nn = []
    for i in range(0, len(P_), 400):
        d = np.hypot(P_[i:i + 400, None, 0] - P_[None, :, 0], P_[i:i + 400, None, 1] - P_[None, :, 1])
        d[np.arange(d.shape[0]), np.arange(i, i + d.shape[0])] = 1e9
        nn.append(d.min(1))
    nn = np.concatenate(nn)
    lean = []
    for o in objs:
        Rm = np.array(o.matrix_world)[:3, :3]
        z = Rm @ np.array([0.0, 0.0, 1.0])
        lean.append(math.degrees(math.acos(min(1.0, float(z[2] / max(np.linalg.norm(z), 1e-9))))))
    lean = np.array(lean)
    fs = np.array([o["lk_fringe_f"] for o in objs])
    tall = sum(1 for o in objs if o.data.name in PL.TUFT_KINDS_TALL)
    hts = np.array([float(PL.lib_bbox(o.data.name)[1].z) * float(o["lk_fringe_f"]) for o in objs])
    grid_share = float((np.abs(nn - np.median(nn)) <= 0.1 * np.median(nn)).mean())
    return dict(count=cnt, clumps=int((sizes >= 3).sum()), singles=int((sizes == 1).sum()), pairs=int((sizes == 2).sum()), max_clump=int(sizes.max()), in_clump_share=in_clump / max(cnt, 1),
                nn_cv=float(nn.std() / max(nn.mean(), 1e-9)), nn_mean=float(nn.mean()), grid_share=grid_share, lean_max=float(lean.max()), lean_med=float(np.median(lean)),
                f_min=float(fs.min()), f_max=float(fs.max()), f_med=float(np.median(fs)), tall=tall, fill=cnt - tall, coverage=r["edge_covered"] / max(r["edge_samples"], 1), edge=r["edge"],
                focus_cov=r["edge_focus"][1] / max(r["edge_focus"][0], 1), focus_n=r["edge_focus"][0], h_min=float(hts.min()), h_med=float(np.median(hts)), h_max=float(hts.max()))


def grass_gates(D, info):
    """GRASS_FRINGE_PRESENT / _NOT_ON_PLAY_SURFACES / _NOT_ON_WATER / _BALL_CLEAR / _TRI_BUDGET of this hole (live scene)."""
    res = []
    r = info.get("fringe")
    if not FRINGE_ON or r is None:
        return [("GRASS_FRINGE_PRESENT", False, "FRINGE_ON is False: no fringe was built")]
    objs = [o for o in info["fringe_objs"] if o.name in bpy.data.objects]
    balls = PL.still_ball_zones(D)
    chk = grass_checks(D, objs, balls, r["corridor_m"])
    for name in ("GRASS_NOT_ON_PLAY_SURFACES", "GRASS_NOT_ON_WATER", "GRASS_BALL_CLEAR"):
        res.append((name, chk[name][0], chk[name][1]))
    st = grass_structure(objs, r)
    cov_ok = st["coverage"] >= 0.20 and (st["focus_cov"] >= 0.50 or st["focus_n"] < 30)
    ok = (st["count"] >= FRINGE_MIN_COUNT and st["tall"] >= 80 and st["fill"] >= 100 and st["max_clump"] <= 7 and st["in_clump_share"] >= 0.55 and st["singles"] >= 5
          and st["lean_max"] <= 12.25 and st["lean_med"] >= 3.0 and st["f_min"] <= 0.7 and st["f_max"] >= 1.2 and st["nn_cv"] >= 0.35 and st["grid_share"] <= 0.45 and cov_ok
          and all(PL.SCALE_LIMITS["lo"] - 1e-9 <= o.scale[i] <= PL.SCALE_LIMITS["hi"] + 1e-9 for o in objs for i in range(3)))
    res.append(("GRASS_FRINGE_PRESENT", ok,
                f"{st['count']} fringe tufts ({st['tall']} tall D/E + {st['fill']} filler S, by kind {r['by_kind']}) in {st['clumps']} clumps of 3-7 (largest {st['max_clump']}), {st['pairs']} pairs, {st['singles']} singles; "
                f"{st['in_clump_share']:.0%} of the tufts stand in a clump of >= 3; nearest-neighbour distance mean {st['nn_mean']:.2f} m, CV {st['nn_cv']:.2f} (>= 0.35), {st['grid_share']:.0%} of the distances within 10 % of the median (<= 45 %): not a grid; "
                f"lean max {st['lean_max']:.1f} deg (<= 12), median {st['lean_med']:.1f}; scale factor {st['f_min']:.2f}..{st['f_max']:.2f} median {st['f_med']:.2f}; tuft height {st['h_min']:.2f} / {st['h_med']:.2f} / {st['h_max']:.2f} m (min / median / max; "
                f"{r['capped']} scaled down to the 0.55 yd cap near the ball corridor); coverage of the rough edge {st['coverage']:.0%} ({r['edge_covered']} of {r['edge_samples']} sample points {r['edge']} (play / path / lip: (points, covered)) have a tuft within 2.2 m; >= 20 %), "
                f"{st['focus_cov']:.0%} of the {st['focus_n']} points within 45 m of a still camera's ball (>= 50 %); the stone paths run on the fairway, so the 'path' edge has no rough to stand on"))
    tri = P.stats(D)["tris"]
    before = info.get("tris_before_fringe", 0)
    ck = info.get("cheapen") or {}
    res.append(("GRASS_TRI_BUDGET", tri <= TRI_CAP and r["tris"] / max(r["count"], 1) <= 90.0 and st["fill"] >= 0.5 * st["count"],
                f"export set {tri} triangles (<= {TRI_CAP}); fringe {r['tris']} triangles = {r['tris'] / max(r['count'], 1):.0f} per tuft (<= 90; {st['fill']} of {st['count']} are 36-triangle fillers); paid for by cheapen_tufts (A / B tufts farther than {CHEAPEN_NEAR_M:g} m from a shot ball -> S / T): "
                f"{ck.get('swapped')} swapped, {ck.get('kept')} kept, {ck.get('tris_saved')} triangles saved; the scene had {before} triangles right before the fringe (round 7 staged build: 195,500)"))
    return res


def look_gates(D, info):
    h, pz = D.hole, D.play_z
    res = []
    pcs = _piece_objs(info)
    sea_level = 0.15
    # NEEDLES
    worst_far, tops, in_w, on_l = 1e9, [], 0, 0
    for st in info["stacks"]:
        ok, (far, iw, ol, top) = _stack_ok(D, st["objs"])
        worst_far, in_w, on_l = min(worst_far, far), in_w + iw, on_l + ol
        tops.append(top)
    res.append(("NEEDLES", max(tops) <= STACK_CAP_M + 1e-6 and worst_far >= STACK_MIN_CENTERLINE_YD and in_w == 0 and on_l == 0,
                f"{len(info['stacks'])} needles (clusters of spires + skirt boulders): highest point {max(tops):.2f} m (cap {STACK_CAP_M}), nearest vertex "
                f"{worst_far:.1f} yd from the centerline (>= {STACK_MIN_CENTERLINE_YD:g}), {in_w} vertices in a water ellipse, {on_l} on land"))
    # ARCH: masonry stands on the outcrop top, height cap, fallen blocks lie on it
    a = info["arch"]
    ob_bvh = bvh_of_objects([a["outcrop"]])
    AV = world_verts([a["arch"]])
    foot_pts = AV[AV[:, 2] <= a["centre"][2] + 0.05]
    gaps = []
    for x, y, z in foot_pts.tolist():
        hit = ob_bvh.ray_cast(Vector((x, y, z + 3.0)), Vector((0, 0, -1)), 8.0)
        gaps.append(float("nan") if hit[0] is None else z - hit[0].z)
    gaps = np.array(gaps)
    nohit = int(np.isnan(gaps).sum())
    fv = np.nan_to_num(gaps, nan=99.0)
    res.append(("ARCH", AV[:, 2].max() <= STACK_CAP_M + 1e-6 and nohit == 0 and float(fv.min()) > -0.35 and float(np.nanmax(gaps)) < 0.35,
                f"masonry arch {AV[:, 2].max() - AV[:, 2].min():.1f} m tall, top {AV[:, 2].max():.2f} m (cap {STACK_CAP_M}); its {len(foot_pts)} foot vertices stand on the outcrop top: "
                f"{nohit} with no outcrop under them, height above the top {float(fv.min()):.2f}..{float(np.nanmax(gaps)) if len(gaps) else 0:.2f} m (limits -0.35..0.35)"))
    fall_bad = []
    for f in a["fallen"]:
        V = world_verts([f])
        lo = V[:, 2].min()
        cen = V[:, :2].mean(axis=0)
        under = []
        for x, y in [(cen[0], cen[1])] + [(float(q[0]), float(q[1])) for q in V[V[:, 2] <= lo + 0.2]]:
            hit = ob_bvh.ray_cast(Vector((x, y, lo + 3.0)), Vector((0, 0, -1)), 8.0)
            under.append(None if hit[0] is None else lo - hit[0].z)
        if any(u is None for u in under) or min(u for u in under if u is not None) > 0.25 or min(u for u in under if u is not None) < -0.5:
            fall_bad.append((f.name, [None if u is None else round(u, 2) for u in under[:3]]))
    res.append(("ARCH_FALLEN_BLOCKS_REST", not fall_bad, f"{len(a['fallen'])} fallen blocks lie on the outcrop top (lowest vertex within 0.25 m above / 0.5 m into it, outcrop under the whole foot); bad {fall_bad}"))
    # ROCKS_OFF_PLAY_SURFACES: no vertex of any piece at or above the play height lies over a fairway / first cut / green / apron / tee box / bunker
    #   (rays straight down onto those meshes), none inside a water ellipse above z -0.5, none within 3 m of the pin
    allv = {n: world_verts([o]) for n, o in pcs.items()}
    playm = bvh_of_objects([o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(("FAIRWAY", "GREEN", "TEE_BOX", "BUNKER"))])
    poke, in_gap, near_pin = 0, 0, 0
    pin_m = P.m(*h.pin)
    for n, V in allv.items():
        for x, y, z in V.tolist():
            if z >= pz - 0.1 and playm.ray_cast(Vector((x, y, z + 0.01)), Vector((0, 0, -1)), 12.0)[0] is not None:
                poke += 1
            if any(k[0] == "water" and h.in_ellipse(k, (x / YD, y / YD)) for k in h.hazards) and z > -0.5:
                in_gap += 1
            if z >= pz - 0.1 and math.hypot(x - pin_m[0], y - pin_m[1]) < 3.0:
                near_pin += 1
    res.append(("ROCKS_OFF_PLAY_SURFACES", poke == 0 and in_gap == 0 and near_pin == 0,
                f"{len(pcs)} rock / masonry pieces, {sum(len(v) for v in allv.values())} vertices: {poke} at or above the play height over a play surface, "
                f"{in_gap} inside a water ellipse above z -0.5, {near_pin} within 3 m of the pin"))
    # ROCKS_STAND_IN_THE_SEA: stacks, outcrop and the foot boulders reach the water
    sea_pieces = [o for st in info["stacks"] for o in st["objs"]] + [a["outcrop"]] + [o for o in info["rocks"] if world_verts([o])[:, 2].max() < pz - 0.5]
    lows = [float(world_verts([o])[:, 2].min()) for o in sea_pieces]
    res.append(("ROCKS_STAND_IN_THE_SEA", max(lows) <= sea_level, f"{len(sea_pieces)} stack parts / outcrop / foot boulders: lowest vertex of each at or below the sea (+{sea_level} m): highest of those {max(lows):.2f} m"))
    # ROCKS_CONTACT_CHAIN: every piece is chained by surfaces within 5 cm to a rock that stands in the sea, or stands on / sunk into the ground (land rocks)
    ground = bvh_of_objects([o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER"))])
    names = list(pcs)
    trees = {n: bvh_of_objects([pcs[n]]) for n in names}
    lo_hi = {n: (allv[n].min(0), allv[n].max(0)) for n in names}
    tol = 0.05

    def touch(a_, b_):
        la, ha = lo_hi[a_]
        lb, hb = lo_hi[b_]
        if (la - tol > hb).any() or (lb - tol > ha).any():
            return False
        if trees[a_].overlap(trees[b_]):
            return True
        for p in allv[a_].tolist():
            r = trees[b_].find_nearest(Vector(p))
            if r[0] is not None and r[3] < tol:
                return True
        for p in allv[b_].tolist():
            r = trees[a_].find_nearest(Vector(p))
            if r[0] is not None and r[3] < tol:
                return True
        return False
    seen = {n for n in names if float(allv[n][:, 2].min()) <= sea_level}
    on_ground = set()
    for n in names:
        if n in seen:
            continue
        V = allv[n]
        for p in V[V[:, 2] <= V[:, 2].min() + 0.15].tolist():
            r = ground.find_nearest(Vector(p))
            if r[0] is not None and r[3] < 0.5:
                on_ground.add(n)
                break
    seen |= on_ground
    adj = {n: [m_ for m_ in names if m_ != n and touch(n, m_)] for n in names if n not in seen}
    changed = True
    while changed:
        changed = False
        for n in names:
            if n not in seen and any(m_ in seen for m_ in adj.get(n, [])):
                seen.add(n)
                changed = True
    floating = [n for n in names if n not in seen]
    res.append(("ROCKS_CONTACT_CHAIN", not floating,
                f"{len(names)} pieces: {sum(1 for n in names if float(allv[n][:, 2].min()) <= sea_level)} stand in the sea, {len(on_ground)} stand on / sunk into the ground, "
                f"the rest are chained to those by surfaces within {tol} m; floating: {floating}"))
    # ROCKS_REST: land rocks are sunk 0..0.45 m into the ground (lowest point within -0.45..+0.15 m of it), at most 25 percent of their lower part hangs over
    #   open air; the arch and the fallen blocks are judged against the outcrop (ARCH, ARCH_FALLEN_BLOCKS_REST)
    land_rocks = [o for o in info["rocks"] if float(world_verts([o])[:, 2].max()) >= pz - 0.5]
    hov, over = [], []
    for o in land_rocks:
        V = world_verts([o])
        lo = float(V[:, 2].min())
        g = ground.ray_cast(Vector((float(V[:, 0].mean()), float(V[:, 1].mean()), lo + 3.0)), Vector((0, 0, -1)), 8.0)
        if g[0] is None or not (-0.45 <= lo - g[0].z <= 0.15):
            hov.append((o.name, None if g[0] is None else round(lo - g[0].z, 2)))
        low = V[V[:, 2] <= lo + 0.4]
        miss = sum(1 for x, y, z in low.tolist() if ground.ray_cast(Vector((x, y, z + 2.0)), Vector((0, 0, -1)), 6.0)[0] is None)
        if miss / max(len(low), 1) > 0.25:
            over.append((o.name, round(miss / max(len(low), 1), 2)))
    res.append(("ROCKS_REST", not hov and not over,
                f"{len(land_rocks)} land rocks rest on the ground (lowest point within -0.45..+0.15 m of it; at most 25 percent of the lower part over open air); "
                f"hovering {hov}, overhanging {over}; the arch and fallen blocks rest on the outcrop (ARCH, ARCH_FALLEN_BLOCKS_REST)"))
    # LINES_OF_PLAY_VISIBLE: cameras (CameraRig.FrameAddress) see the landing pad and the green past every piece
    allobs = list(pcs.values()) + list(a["vines"])
    big = bvh_of_objects([o for o in allobs if not o.name.startswith("PLANT_")])
    pin = h.pin
    green_pts = [pin] + [(pin[0] + 10 * math.cos(t_), pin[1] + 10 * math.sin(t_)) for t_ in np.linspace(0, math.tau, 8, endpoint=False)]
    stations = [("tee", CAMERAS["tee"][0], CAMERAS["tee"][1], [(19.0, 150.0), (19.0, 178.0), (16.0, 200.0)] + green_pts),
                ("middle pad", CAMERAS["mid"][0], CAMERAS["mid"][1], green_pts),
                ("middle pad far end", CAMERAS["far"][0], CAMERAS["far"][1], green_pts)]
    blocked, tested = [], 0
    for nm, ball, aim, targets in stations:
        b = Vector((*P.m(*ball), pz + 0.3))
        dv = Vector((*P.m(*aim), 0.0)) - Vector((b.x, b.y, 0.0))
        dv.normalize()
        cam = b - dv * 4.5 * YD + Vector((0, 0, 2.4 * YD))
        for tg in targets:
            tp = Vector((*P.m(*tg), pz + 0.3))
            d = tp - cam
            hit = big.ray_cast(cam, d.normalized(), d.length - 0.05)
            tested += 1
            if hit[0] is not None:
                blocked.append((nm, tuple(round(c, 1) for c in tg)))
    res.append(("LINES_OF_PLAY_VISIBLE", not blocked,
                f"{tested} camera-to-target rays (tee, middle pad, its far end -> landing pad and green, 9 green points) past every rock / masonry / vine piece: {len(blocked)} stopped {blocked[:3]}"))
    # NEEDLES_NOT_BEHIND_TARGETS: from the three cameras, nothing of a needle stands within 1.2 degrees behind the flag top or the arch opening
    stack_objs = [o for st in info["stacks"] for o in st["objs"]]
    sbvh = bvh_of_objects(stack_objs)
    ac = a["centre"]
    targets = {"flag": Vector((*P.m(*pin), pz + 2.0)), "arch opening": Vector((ac[0], ac[1], ac[2] + 0.45 * a["height"]))}
    behind = []
    for nm, ball, aim, _t in stations:
        b = Vector((*P.m(*ball), pz + 0.3))
        dv = Vector((*P.m(*aim), 0.0)) - Vector((b.x, b.y, 0.0))
        dv.normalize()
        cam = b - dv * 4.5 * YD + Vector((0, 0, 2.4 * YD))
        for tn, tp in targets.items():
            base = (tp - cam)
            dist = base.length
            base.normalize()
            side = base.cross(Vector((0, 0, 1))).normalized()
            up = side.cross(base).normalized()
            for du in np.linspace(-1.2, 1.2, 5):
                for dvv in np.linspace(-1.2, 1.2, 5):
                    dd = (base + side * math.tan(math.radians(du)) + up * math.tan(math.radians(dvv))).normalized()
                    hit = sbvh.ray_cast(cam, dd)
                    if hit[0] is not None and hit[3] > dist * 0.98:
                        behind.append((nm, tn))
                        break
                else:
                    continue
                break
    # ARCH_NOT_HIDDEN_BY_NEEDLES: from each camera that has the arch in its frame (60 degree vertical FOV, 900x1600: +-18.4 degrees across, +-30 up/down)
    #   at most 8 percent of 150 sample points on the masonry (above the plinth) are hidden by a needle or a loose rock in front of them
    occl = bvh_of_objects([o for st in info["stacks"] for o in st["objs"]] + list(info["rocks"]))
    AVs = world_verts([a["arch"]])
    AVs = AVs[AVs[:, 2] >= ac[2] + 1.0]
    AVs = AVs[np.linspace(0, len(AVs) - 1, 150).astype(int)]
    hid_report = []
    arch_ok = True
    for nm, ball, aim, _t in stations:
        b = Vector((*P.m(*ball), pz + 0.3))
        dv = Vector((*P.m(*aim), 0.0)) - Vector((b.x, b.y, 0.0))
        dv.normalize()
        cam = b - dv * 4.5 * YD + Vector((0, 0, 2.4 * YD))
        look = (b + dv * 1.5 * YD) - cam
        look.normalize()
        right = look.cross(Vector((0, 0, 1))).normalized()
        c_ = Vector((ac[0], ac[1], ac[2] + 0.5 * a["height"])) - cam
        c_.normalize()
        horiz = math.degrees(math.atan2(c_.dot(right), c_.dot(Vector((dv.x, dv.y, 0)).normalized())))
        if abs(horiz) > 17.0:
            hid_report.append((nm, "arch outside the frame", round(horiz, 1)))
            continue
        hid = 0
        for x, y, z in AVs.tolist():
            d = Vector((x, y, z)) - cam
            hit = occl.ray_cast(cam, d.normalized(), d.length - 0.05)
            hid += hit[0] is not None
        frac = hid / len(AVs)
        hid_report.append((nm, round(frac, 3), round(horiz, 1)))
        arch_ok &= frac <= 0.08
    # ARCH_OPENING_CLEAR: the opening of the arch (a grid of free points in its plane) is not covered by a needle / loose rock in front of it, from the cameras that frame it
    through = Vector((math.sin(math.radians(a["facing"])), math.cos(math.radians(a["facing"])), 0.0))
    axis = Vector((through.y, -through.x, 0.0))
    abvh = bvh_of_objects([a["arch"]])
    opening = []
    for u in np.arange(-0.4 * a["span"], 0.4 * a["span"] + 1e-6, 0.4):
        for z in np.arange(ac[2] + 1.2, ac[2] + 0.75 * a["height"], 0.4):
            pt = Vector((ac[0], ac[1], z)) + axis * u
            if abvh.ray_cast(pt - through * 4.0, through, 8.0)[0] is None:
                opening.append(pt)
    op_report, op_ok = [], True
    for nm, ball, aim, _t in stations:
        b = Vector((*P.m(*ball), pz + 0.3))
        dv = Vector((*P.m(*aim), 0.0)) - Vector((b.x, b.y, 0.0))
        dv.normalize()
        cam = b - dv * 4.5 * YD + Vector((0, 0, 2.4 * YD))
        c_ = Vector((ac[0], ac[1], ac[2] + 0.5 * a["height"])) - cam
        horiz = math.degrees(math.atan2(c_.dot(dv.cross(Vector((0, 0, 1))).normalized() * -1), c_.dot(dv)))
        if abs(horiz) > 17.0 or not opening:
            op_report.append((nm, "outside the frame", round(horiz, 1)))
            continue
        cov = 0
        for pt in opening:
            d = pt - cam
            if occl.ray_cast(cam, d.normalized(), d.length - 0.05)[0] is not None:
                cov += 1
        op_report.append((nm, round(cov / len(opening), 3), len(opening)))
        op_ok &= cov / len(opening) <= 0.10
    res.append(("ARCH_OPENING_CLEAR", op_ok and len(opening) > 20, f"share of the arch opening ({len(opening)} free points in its plane) covered by a needle / loose rock, per camera: {op_report} (<= 0.10)"))
    res.append(("ARCH_NOT_HIDDEN_BY_NEEDLES", arch_ok, f"share of the masonry hidden by a needle / loose rock, per camera (name, share, degrees off the aim): {hid_report} (<= 0.08)"))
    res.append(("NEEDLES_NOT_BEHIND_TARGETS", not behind, f"flag top and arch opening seen from the 3 cameras: needles within 1.2 degrees behind them: {behind}"))
    # PATH_CLEAR_OF_BALL (round 3): the paths run along the ball's left side but the address balls stay off the stones: nearest path vertex (the lattice is 0.15 m, the outline is
    #   made of path vertices) to the tee ball (0, 0) and to the approach ball (19, 178) is at least PATH_BALL_CLEAR_M; and both paths reach into the lower part of the frames
    #   (path_in_frame: the share of the lower 40 percent of the frame, rays over the ground, that lands on a path, measured by frame_variation.py, is a separate gate)
    pobs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("DRESS_PATH")]
    pv = world_verts(pobs) if pobs else np.zeros((0, 3))
    clear = {}
    for nm, (bx, by) in (("tee", (0.0, 0.0)), ("approach", (19.0, 178.0))):
        clear[nm] = float(np.hypot(pv[:, 0] - bx * YD, pv[:, 1] - by * YD).min()) if len(pv) else 99.0
    res.append(("PATH_CLEAR_OF_BALL", len(pobs) >= 2 and min(clear.values()) >= PATH_BALL_CLEAR_M,
                f"{len(pobs)} DRESS_PATH meshes: nearest path vertex to the tee ball {clear['tee']:.2f} m, to the approach ball {clear['approach']:.2f} m (need >= {PATH_BALL_CLEAR_M} m)"))
    # MOW_PATTERN (round 2): UV phase + cross-mown tee box, UV / one material slot only
    mow = info["mow"]
    tb = bpy.data.objects["TEE_BOX"]
    tb_mats = sorted({m.name for m in tb.data.materials if m})
    tile_fw = L.LOOK["LK_FAIRWAY"][2]
    dens = {}                                    # recomputed from the scene (not the build-time report), so the negative controls can move it
    for nm in ("FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN_APRON", "TEE_BOX"):
        dd, area = _uv_world_density(bpy.data.objects[nm], "LK_FAIRWAY")
        if area > 0:
            dens[nm] = round(dd * tile_fw, 3)
    dens_ok = all(0.9 <= v <= 1.1 for v in dens.values())
    ph_med, ph_share = _stripe_phase(D)
    ph_ok = abs(ph_med - FAIRWAY_STRIPE_SHIFT) < 0.01 and ph_share > 0.98
    res.append(("MOW_PATTERN", tb_mats == ["LK_FAIRWAY"] and dens_ok and ph_ok and all(n > 0 for n in mow["faces"].values()),
                f"LK_FAIRWAY u shifted by {mow['shift']} on faces {mow['faces']} (live check: U - lateral / 10 on FAIRWAY has median {ph_med:.3f}, {ph_share:.0%} within 0.01; need {FAIRWAY_STRIPE_SHIFT}); "
                f"TEE_BOX {mow['tee_box_materials_before']} -> {tb_mats} ({'continuing the fairway UVs' if TEE_BOX_SEAMLESS else 'cross-mown'}), "
                f"{mow['tee_box_faces']} faces; UV density / contract (1.00 = 1 / 10 m, recomputed from the scene): {dens} (0.9..1.1)"))
    # TEE_SEAM_SEEN (round 6, replaces round 5's TEE_SEAM_UV): the plan-view UV step between the tee box outline and the fairway under it is no longer 0 by design (the plate's UVs are
    #   shifted by the 0.35 m band the 10 cm step hides from the address camera); what has to be continuous is what the camera SEES. Ray casts through the tee frame: the grass texture phase jump
    #   across the plate's front edge, extrapolated from 8 rows on either side, in every pixel column whose hits are plate / fairway grass (round 5 build: 0.035 = 35 cm in V).
    du, dv, n_ok, n_skip = tee_seam_uv_gap()
    sc_ = tee_seen_seam()
    sm_ = _seen_summary(sc_)
    res.append(("TEE_SEAM_SEEN", sm_["n_grass"] >= 30 and sm_["grass_du"] <= SEEN_MAX_GRASS_JUMP and sm_["grass_dv"] <= SEEN_MAX_GRASS_JUMP,
                f"tee frame (900 x 1600), {sm_['n_grass']} grass pixel columns (plate above / fairway beyond the edge, edge row mean {sm_['mean_row']:.0f}): texture jump across the plate's front edge, "
                f"max |dU| {sm_['grass_du']:.4f}, max |dV| {sm_['grass_dv']:.4f} tile units of the 10 m tile (<= {SEEN_MAX_GRASS_JUMP} = 4 cm; round 5 build, same rays: dU 0.0109, dV 0.0335 = 33 cm); for the record the plan-view UV step "
                f"TEE_BOX outline vs FAIRWAY under it is dU {du:.4f}, dV {dv:.4f} ({n_ok} outline vertices) = the compensation of the hidden band (k = {info['mow'].get('hidden_k')})"))
    res.append(("PATH_STEP_CONTINUOUS", sm_["n_path"] >= 20 and sm_["gap_rows"] == 0 and sm_["path_du"] <= SEEN_MAX_PATH_JUMP and sm_["path_dv"] <= SEEN_MAX_PATH_JUMP,
                f"tee frame, {sm_['n_path']} pixel columns on the stone path across the plate's front edge: {sm_['gap_rows']} rows without stones between 24 rows above and 24 below the edge "
                f"(a grass strip across the path; round 5 build, same rays with 30..40 rows margin: 43 columns, 7 to 14 rows without stones in every one, 444 in all), stone texture jump max |dU| {sm_['path_du']:.4f}, |dV| {sm_['path_dv']:.4f} tile units of the 5 m tile "
                f"(<= {SEEN_MAX_PATH_JUMP} = 4 cm)"))
    # PATH_SEAM_JUMP (round 5): the stone path crosses the tee box's front edge in a straight line along the view, so the 0.4 m of fairway hidden behind the 10 cm step does not
    #   show as a sideways jump / kink in the tee frame
    worst, jr = tee_path_seam_jump()
    if jr.get("jump_px") is None:
        res.append(("PATH_SEAM_JUMP", False, f"the path edge could not be measured at the tee box's front edge: {jr}"))
    else:
        ok_j = abs(jr["raw_step_px"]) <= PATH_SEAM_MAX_STEP_PX and abs(jr["jump_px"]) <= PATH_SEAM_MAX_JUMP_PX and abs(jr["kink_px_per_row"]) <= PATH_SEAM_MAX_KINK
        res.append(("PATH_SEAM_JUMP", ok_j,
                    f"tee frame (900 x 1600), ray casts: the path's right edge at the tee box's front edge (screen row {jr['seam']}): raw step between the edge's last row below and first row above "
                    f"the seam {jr['raw_step_px']} px (<= {PATH_SEAM_MAX_STEP_PX}), jump of the two fitted lines at the seam {jr['jump_px']} px (<= {PATH_SEAM_MAX_JUMP_PX}), slope change "
                    f"{jr['kink_px_per_row']} px per row (<= {PATH_SEAM_MAX_KINK}); slopes {jr['slope_tee']} / {jr['slope_fairway']}; round 4 (path bent right at the seam): 29 px / 10 px / -0.9"))
    # LEDGES_ARE_TURF (round 5): no up-facing cliff-skin face under the grass lip is a dark LK_CLIFF slab any more
    pz_ = D.play_z
    dark, turf, tot_up = 0, 0, 0
    for o in [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("ROCK_SKIN_")]:
        me_ = o.data
        mw_ = np.array(o.matrix_world)
        for p_ in me_.polygons:
            nw = mw_[:3, :3] @ np.array(p_.normal)
            nl = float(np.linalg.norm(nw))
            if nl < 1e-9 or nw[2] / nl < SKIN_LEDGE_MIN_UP or float((mw_ @ np.append(np.array(p_.center), 1.0))[2]) < pz_ - SKIN_LEDGE_DEPTH_M:
                continue
            tot_up += 1
            mn = me_.materials[p_.material_index].name.split(".")[0] if me_.materials and me_.materials[p_.material_index] is not None else ""
            if mn in ("LK_CLIFF", "LK_CLIFF_DARK"):
                dark += 1
            elif mn == "LK_ROUGH":
                turf += 1
    res.append(("LEDGES_ARE_TURF", tot_up >= 20 and dark == 0,
                f"{tot_up} up-facing ROCK_SKIN faces within {SKIN_LEDGE_DEPTH_M} m under the grass lip (the capping ledge and the first strata ledge): {turf} wear LK_ROUGH turf, {dark} are still dark LK_CLIFF slabs "
                f"(need 0); {info['ledges']['faces']} re-materialled at build time ({info['ledges']['area_m2']} m2); round 4: all of them were LK_CLIFF"))
    # BUNKER_LIP_NOT_GREEN (round 5): the bunker lip wears the isotropic rough turf, one material, planar UVs
    lipm = sorted({m.name for m in bpy.data.objects["BUNKER_01_LIP"].data.materials if m})
    res.append(("BUNKER_LIP_NOT_GREEN", lipm == ["LK_ROUGH"],
                f"BUNKER_01_LIP materials {lipm} (round 4: ['LK_GREEN'], the brightest, directional grass; the contract allows LK_GREEN / LK_FAIRWAY / LK_ROUGH / LK_SAND); vertices frozen (H_PLAY_UNCHANGED)"))
    # OUTCROP_VINES (round 5): vines hang from the outcrop's rim over its near flank; every drop stays within 1.4 m of the rock and its lowest leaf cluster is at z >= VINE_MIN_TOP_M
    ov = [o for o in info.get("outcrop_vines", []) if o.name in bpy.data.objects]
    abv = bvh_of_objects([info["arch"]["outcrop"]])
    far_, low_ = 0, 1e9
    for o in ov:
        Vv = world_verts([o])
        low_ = min(low_, float(Vv[:, 2].min()))
        sel = Vv[np.linspace(0, len(Vv) - 1, min(len(Vv), 60)).astype(int)]
        if any(abv.find_nearest(Vector(q.tolist()), 1.4)[0] is None for q in sel):
            far_ += 1
    res.append(("OUTCROP_VINES", len(ov) >= OUTCROP_VINES_MIN and far_ == 0 and low_ >= VINE_MIN_TOP_M - 0.2,
                f"{len(ov)} PLANT_VINE_* hang from the outcrop's grass rim over its flank (need >= {OUTCROP_VINES_MIN}); {far_} have a leaf more than 1.4 m from the rock (need 0); lowest leaf z {low_:.2f} m "
                f"(>= {VINE_MIN_TOP_M - 0.2:.1f}: the plant gate wants every piece >= 3 m up)"))
    # ARCH_SLAB_COVERED (round 6): the outcrop's camera-side wall is dressed (turf bank, patchy turf, cut + displaced facets): no bare grey rock plate in the foreground of the arch shot
    bf = arch_frame_bare_rock()
    dr = info["arch"].get("dress") or {}
    res.append(("ARCH_SLAB_COVERED", bf["bare_lower"] <= ARCH_SLAB_MAX and bf["bare_all"] <= 0.04 and dr.get("turf_faces", 0) > 0,
                f"arch landmark camera (Blender m {ARCH_CAM['pos']} -> {ARCH_CAM['look']}, fov {ARCH_CAM['fov']:g}), {bf['rays']} rays through the 900 x 1600 frame: the OUTCROP's bare rock (LK_ROCK / LK_CLIFF) is {bf['bare_lower']:.1%} of the lower 40 % "
                f"(<= {ARCH_SLAB_MAX:.0%}; round 5 build, same rays: 22.7 % = the 'flat grey slab' of 500 x 210 px; 9.1 % of the whole frame) and {bf['bare_all']:.1%} of the whole frame (<= 4 %); the pad's strata skin in the lower 40 %: {bf['skin_lower']:.1%} (reported); "
                f"outcrop dressing: {dr.get('faces_before')} -> {dr.get('faces_after')} faces, {dr.get('cut_vertices')} cut vertices ({dr.get('moved')} displaced), {dr.get('turf_faces')} turf + {dr.get('mix_turf_faces')} patchy-turf faces"))
    # ARCH_FOLD_SOFT (round 8): no hard polygonal fold in the lower third of the arch shot: the outcrop's rim is a rounded shoulder with smooth turf normals
    fm = arch_fold_metric()
    res.append(("ARCH_FOLD_SOFT", fm["pairs"] >= 2000 and fm["max_deg"] <= ARCH_FOLD_MAX_STEP_DEG and fm["max_lambert"] <= ARCH_FOLD_MAX_LAMBERT,
                f"arch landmark frame, region x {ARCH_FOLD_REGION[0]}-{ARCH_FOLD_REGION[1]} y {ARCH_FOLD_REGION[2]}-{ARCH_FOLD_REGION[3]} (the reviewer's fold), {fm['pairs']} vertically adjacent ray pairs (4 px apart) on the outcrop: "
                f"largest change of the SHADING normal {fm['max_deg']:.1f} deg at {fm['max_at']} (<= {ARCH_FOLD_MAX_STEP_DEG:g}; round 7 build, same rays: 66.9 deg, 97 pairs over 20 deg = the hard fold), 99th percentile {fm['p99_deg']:.1f} deg, {fm['over20']} pairs over 20 deg, "
                f"largest |N.sun| step {fm['max_lambert']:.3f} (<= {ARCH_FOLD_MAX_LAMBERT}; round 7: 1.08); shoulder {dr.get('shoulder_faces')} bevel faces ({OUTCROP_SHOULDER_M} m, {OUTCROP_SHOULDER_SEGS} segments), {dr.get('smooth_turf_loops')} turf loops on smooth custom normals"))
    # GRASS_OUTCROP_FRINGE (round 8): the clumps on the arch island stand on its grass top, clear of the rim / piers / blocks, and are not taller than OUTCROP_FRINGE_H_MAX
    ofr = [o for o in bpy.data.objects if o.type == 'MESH' and "lk_outcrop_fringe" in o.keys()]
    rim_o = _outcrop_rim(info["arch"]["outcrop"], info["arch"]["centre"][2])
    bvo = bvh_of_objects([info["arch"]["outcrop"]])
    topo = info["arch"]["centre"][2]
    bad_o, h_o, rd_min, tr_o = [], [], 9.0, 0
    Ao, Bo = rim_o, np.roll(rim_o, -1, axis=0)
    abo = Bo - Ao
    l2o = np.maximum((abo ** 2).sum(1), 1e-12)
    for o in ofr:
        x, y, z = o.location.x, o.location.y, o.location.z
        hit = bvo.ray_cast(Vector((x, y, topo + 3.0)), Vector((0, 0, -1)), 8.0)
        if hit[0] is None or hit[1].z < 0.95 or abs(hit[0].z - 0.02 - z) > 0.03 or hit[0].z < topo - 0.05:
            bad_o.append((o.name, "not on the island top"))
            continue
        tt = np.clip(((x - Ao[:, 0]) * abo[:, 0] + (y - Ao[:, 1]) * abo[:, 1]) / l2o, 0, 1)
        rd = float(np.sqrt((x - (Ao[:, 0] + abo[:, 0] * tt)) ** 2 + (y - (Ao[:, 1] + abo[:, 1] * tt)) ** 2).min())
        rd_min = min(rd_min, rd)
        if rd < 0.40:
            bad_o.append((o.name, f"{rd:.2f} m from the rim"))
        if any(math.hypot(x - ax_, y - ay_) < ar_ for ax_, ay_, ar_ in info["arch"].get("avoid", [])):
            bad_o.append((o.name, "inside a pier / block disc"))
        V_ = world_verts([o])
        h_o.append(float(V_[:, 2].max()) - float(hit[0].z) if hit[0] is not None else 99.0)
        o.data.calc_loop_triangles()
        tr_o += len(o.data.loop_triangles)
    nclump = len({o["lk_outcrop_fringe"] for o in ofr})
    res.append(("GRASS_OUTCROP_FRINGE", len(ofr) >= 40 and nclump >= 8 and not bad_o and (max(h_o) if h_o else 99) <= OUTCROP_FRINGE_H_MAX + 0.05,
                f"{len(ofr)} tufts (PLANT_TUFT_D / E / S, the sway-carrying library meshes, {tr_o} triangles) in {nclump} clumps on the arch island's grass top: every base on the top (ray onto the outcrop, within 3 cm of the placement, "
                f"normal up), nearest to the rim {rd_min:.2f} m (>= 0.40), none inside a pier / fallen-block disc, tallest {max(h_o) if h_o else 0:.2f} m (<= {OUTCROP_FRINGE_H_MAX + 0.05:.2f}); bad {bad_o[:3] or 'none'}"))
    # CROWN_TUFTS (round 8, libs hand-off): the grass crowns of the spires carry tufts that stand on them
    cr = info.get("crowns")
    if cr is not None:
        bad_c, dz_c = [], 0.0
        sbv = bvh_of_objects([o for st in info["stacks"] for o in st["objs"]])
        for o in cr["objs"]:
            near = sbv.find_nearest(o.location)
            d_ = float(near[3]) if near[0] is not None else 99.0
            dz_c = max(dz_c, d_)
            if d_ > 0.12:
                bad_c.append((o.name, round(d_, 3)))
        res.append(("CROWN_TUFTS", len(cr["objs"]) >= 40 and not bad_c,
                    f"{len(cr['objs'])} tufts ({cr['tris']} triangles) on the top {CROWN_M:g} m of {cr['shrubs']} sea-stack / spire meshes ({cr['candidates']} candidates, {cr['pruned']} pruned by the verifier's support rule): base to the nearest stack surface max {dz_c:.3f} m (<= 0.12); bad {bad_c[:3] or 'none'}"))
    # VINE_ROOT_GAP (round 8, libs hand-off): every hanging vine's root touches a surface (<= 5 cm), measured fresh on the live scene (independent of attach_vines' own bookkeeping)
    gaps = PL.vine_root_gaps()
    worst = max([g for _v, g in gaps] or [0.0])
    va = info.get("vine_attach", {})
    res.append(("VINE_ROOT_GAP", worst <= 0.05 + 1e-9 and len(gaps) >= 60,
                f"{len(gaps)} PLANT_VINE_* (cliff skin, outcrop flank, masonry): root to the nearest ROCK_ / DRESS_ARCH / BLOCK / TERRAIN surface max {worst * 100:.1f} cm (<= 5; before attach_vines: {va.get('gap_before_max', 0):.2f} m); "
                f"attach_vines: {va.get('ok')} already touching, {va.get('moved')} moved onto the surface, {va.get('deleted')} deleted (beyond 1 m); outcrop vines now follow the wall's slope (tilt about the root, root offset = the surface at its height)"))
    sr, st_, sn = arch_region_rock_share()
    res.append(("ARCH_LEFT_STRIP_COVERED", sr <= ARCH_STRIP_MAX_ROCK,
                f"arch frame, the reviewer's left strip x {ARCH_STRIP_REGION[0]}-{ARCH_STRIP_REGION[1]}, y {ARCH_STRIP_REGION[2]}-{ARCH_STRIP_REGION[3]} ('a grey slab and vines'), {sn} rays: bare outcrop rock {sr:.1%} (<= {ARCH_STRIP_MAX_ROCK:.0%}; round 7 build, same rays: 31.1 %), turf {st_:.1%}"))
    for g in grass_gates(D, info):
        res.append(g)
    return res


def arch_frame_bare_rock(gx=45, gy=80, W=900, H=1600):
    """First-hit classes of a gx x gy ray grid through the arch landmark frame (the full scene, plants included): share of the rays in the lower 40 % of the frame (rows >= 0.6 H) and in the whole frame
    that end on the OUTCROP's bare rock (a ROCK_OUTCROP_* face with LK_ROCK / LK_ROCK_WET / LK_CLIFF*), and on the pad's strata skin (reported only). Returns dict."""
    bpy.context.view_layer.update()
    sc = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()
    cam = Vector(ARCH_CAM["pos"])
    look = Vector(ARCH_CAM["look"])
    bare_lo = bare_all = skin_lo = n_lo = n_all = 0
    for j in range(gy):
        py = (j + 0.5) / gy * H
        for i in range(gx):
            px = (i + 0.5) / gx * W
            d = _cam_ray(cam, look, px, py, W, H, ARCH_CAM["fov"])
            ok, loc, _n, idx, ob, _m = sc.ray_cast(dg, cam, d, distance=900.0)
            n_all += 1
            lower = py >= 0.6 * H
            n_lo += lower
            if not ok or ob is None or ob.type != 'MESH':
                continue
            me = ob.data
            mat = me.materials[me.polygons[idx].material_index].name.split(".")[0] if me.materials and me.materials[me.polygons[idx].material_index] is not None else ""
            if ob.name.startswith("ROCK_OUTCROP") and mat in ("LK_ROCK", "LK_ROCK_WET", "LK_CLIFF", "LK_CLIFF_DARK"):
                bare_all += 1
                bare_lo += lower
            elif ob.name.startswith("ROCK_SKIN") and mat in ("LK_CLIFF", "LK_CLIFF_DARK") and lower:
                skin_lo += 1
    return dict(bare_lower=bare_lo / max(n_lo, 1), bare_all=bare_all / max(n_all, 1), skin_lower=skin_lo / max(n_lo, 1), rays=n_all)


ARCH_FOLD_SUN_BLENDER = (-0.673, 0.545, 0.5)        # GolfAtmosphere Needle key light: azimuth heading - 50 = -51 degrees, elevation 30 (Unity (x, y up, z) -> Blender (x, z, y))
ARCH_FOLD_REGION = (150, 750, 1280, 1600)             # x0, x1, y0, y1 of the 900 x 1600 arch frame: the lower third the reviewer measured the hard fold in
ARCH_FOLD_MAX_STEP_DEG = 30.0                         # shading normal change between two rays 4 px apart on the outcrop (a flat-shaded 75 degree crease measures 60-75)
ARCH_FOLD_MAX_LAMBERT = 0.40                          # |N.sun step| between two such rays (a 30-35 % luminance step of the Game view)


def _shading_normal_sampler(ob):
    """f(poly_index, world_point) -> unit world shading normal (the interpolated corner normal of the triangle of that polygon that contains the point)."""
    me = ob.data
    me.calc_loop_triangles()
    nl = len(me.loops)
    cn = np.empty(nl * 3)
    me.corner_normals.foreach_get("vector", cn)
    cn = cn.reshape(-1, 3)
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    nt = len(me.loop_triangles)
    tl = np.empty(nt * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("loops", tl)
    tl = tl.reshape(-1, 3)
    tv = np.empty(nt * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tv)
    tv = tv.reshape(-1, 3)
    tp = np.empty(nt, dtype=np.int64)
    me.loop_triangles.foreach_get("polygon_index", tp)
    by_poly = {}
    for t, p_ in enumerate(tp.tolist()):
        by_poly.setdefault(p_, []).append(t)
    mw = ob.matrix_world.copy()
    mwi = mw.inverted()
    nm = np.array(mw.to_3x3().inverted().transposed())

    def f(poly, loc):
        q = np.array(mwi @ Vector(loc))
        best, bw = None, -1e9
        for t in by_poly.get(int(poly), []):
            a, b, c = co[tv[t, 0]], co[tv[t, 1]], co[tv[t, 2]]
            v0, v1, v2 = b - a, c - a, q - a
            d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
            den = d00 * d11 - d01 * d01
            if abs(den) < 1e-18:
                continue
            w1 = (d11 * d20 - d01 * d21) / den
            w2 = (d00 * d21 - d01 * d20) / den
            w0 = 1.0 - w1 - w2
            m_ = min(w0, w1, w2)
            if m_ > bw:
                bw, best = m_, (t, w0, w1, w2)
        if best is None:
            return None
        t, w0, w1, w2 = best
        n = cn[tl[t, 0]] * w0 + cn[tl[t, 1]] * w1 + cn[tl[t, 2]] * w2
        n = nm @ n
        ln = float(np.linalg.norm(n))
        return n / ln if ln > 1e-12 else None
    return f


ARCH_STRIP_REGION = (0, 110, 1300, 1500)             # the reviewer's left strip: 'a grey slab and vines' at x 0-110, y 1300-1500 of the arch frame
ARCH_STRIP_MAX_ROCK = 0.10


def arch_region_rock_share(region=ARCH_STRIP_REGION, step=6, W=900, H=1600):
    """Share of the rays (step px grid over `region` of the arch frame, full scene) whose first hit is bare OUTCROP rock (a ROCK_OUTCROP_* face with LK_ROCK / LK_ROCK_WET / LK_CLIFF*), and the share on turf (LK_ROUGH). Returns (rock, turf, rays)."""
    bpy.context.view_layer.update()
    sc = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()
    cam, look = Vector(ARCH_CAM["pos"]), Vector(ARCH_CAM["look"])
    x0, x1, y0, y1 = region
    n = rock = turf = 0
    for px in range(x0, x1 + 1, step):
        for py in range(y0, y1 + 1, step):
            d = _cam_ray(cam, look, px, py, W, H, ARCH_CAM["fov"])
            ok, loc, _n, idx, ob, _m = sc.ray_cast(dg, cam, d, distance=900.0)
            n += 1
            if not ok or ob is None or ob.type != 'MESH' or not ob.name.startswith("ROCK_OUTCROP"):
                continue
            me = ob.data
            mat = me.materials[me.polygons[idx].material_index].name.split(".")[0] if me.materials and me.materials[me.polygons[idx].material_index] is not None else ""
            if mat in ("LK_ROCK", "LK_ROCK_WET", "LK_CLIFF", "LK_CLIFF_DARK"):
                rock += 1
            elif mat == "LK_ROUGH":
                turf += 1
    return rock / max(n, 1), turf / max(n, 1), n


def arch_fold_metric(region=ARCH_FOLD_REGION, dx=6, dy=4, W=900, H=1600, prefix="ROCK_OUTCROP"):
    """Reviewer (medium, arch shot): 'on the arch island the lower third shows a hard polygonal fold ... a lit-facet seam on a slope break, a 30-35 % luminance step with no AO feather'.
    Ray casts through the arch landmark frame over `region` on a dx x dy pixel grid (the full scene): for every pair of vertically adjacent rays (dy px apart) that both end on the OUTCROP
    the angle between the two SHADING normals (the interpolated corner normals: what Unity lights) and the step of N.sun (the Needle key light). A flat-shaded 75 degree crease is a 60-75
    degree step in ONE pixel pair; a rounded shoulder spreads it over many rows. Returns dict(pairs, max_deg, p99_deg, over20, max_lambert, max_at=(px, py), rays, outcrop_share)."""
    bpy.context.view_layer.update()
    sc = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()
    cam = Vector(ARCH_CAM["pos"])
    look = Vector(ARCH_CAM["look"])
    sun = np.array(ARCH_FOLD_SUN_BLENDER)
    samplers = {}
    x0, x1, y0, y1 = region
    cols = list(range(x0, x1 + 1, dx))
    rows = list(range(y0, y1 + 1, dy))
    grid = {}
    n_out = 0
    for px in cols:
        for py in rows:
            d = _cam_ray(cam, look, px, py, W, H, ARCH_CAM["fov"])
            ok, loc, _n, idx, ob, _m = sc.ray_cast(dg, cam, d, distance=900.0)
            if not ok or ob is None or ob.type != 'MESH' or not ob.name.startswith(prefix):
                continue
            smp = samplers.get(ob.name)
            if smp is None:
                smp = samplers[ob.name] = _shading_normal_sampler(ob)
            n = smp(idx, loc)
            if n is None:
                continue
            grid[(px, py)] = n
            n_out += 1
    steps, lam, where = [], [], []
    for px in cols:
        for a, b in zip(rows[:-1], rows[1:]):
            n1, n2 = grid.get((px, a)), grid.get((px, b))
            if n1 is None or n2 is None:
                continue
            ang = math.degrees(math.acos(max(-1.0, min(1.0, float(n1 @ n2)))))
            steps.append(ang)
            lam.append(abs(float(n1 @ sun) - float(n2 @ sun)))
            where.append((px, a))
    st = np.array(steps) if steps else np.zeros(1)
    k = int(np.argmax(st)) if steps else 0
    return dict(pairs=len(steps), max_deg=float(st.max()), p99_deg=float(np.percentile(st, 99)), over20=int((st > 20.0).sum()), max_lambert=float(max(lam) if lam else 0.0),
                max_at=where[k] if where else None, rays=len(cols) * len(rows), outcrop_share=n_out / max(len(cols) * len(rows), 1))


def tee_seam_uv_gap():
    """Largest UV disagreement between TEE_BOX and FAIRWAY along the tee box's outline (tile units; 0.01 = 10 cm of a 10 m tile). Every outline vertex of TEE_BOX: its UV against the
    UV of the FAIRWAY surface at the same (x, y) (ray straight down, barycentric UV of the hit triangle). dU = u_tee - u_fairway; dV = (v_tee - v_fairway) wrapped to -0.5..0.5 (the
    texture repeats, so whole tiles do not matter). The reviewer's hard line was the cross-mown tee box (u = arc / 10 + 0.8, v = -lateral / 10) meeting the lengthwise stripes of the
    fairway: dU 0.2..0.6. Returns (max |dU|, max |dV|, n vertices compared, n skipped)."""
    tb = bpy.data.objects["TEE_BOX"]
    fw = bpy.data.objects["FAIRWAY"]
    bpy.context.view_layer.update()
    me_t, me_f = tb.data, fw.data
    me_f.calc_loop_triangles()
    Vf, Tf = world_tris(fw)
    bvh = BVHTree.FromPolygons([tuple(p_) for p_ in Vf.tolist()], [tuple(int(i) for i in t) for t in Tf.tolist()])
    lay_f = me_f.uv_layers.active.data
    lay_t = me_t.uv_layers.active.data
    cnt = {}
    for p_ in me_t.polygons:
        vs = [me_t.loops[i].vertex_index for i in range(p_.loop_start, p_.loop_start + p_.loop_total)]
        for a, b in zip(vs, vs[1:] + vs[:1]):
            e = (min(a, b), max(a, b))
            cnt[e] = cnt.get(e, 0) + 1
    outline = sorted({v for e, c in cnt.items() if c == 1 for v in e})
    uv_t = {}
    for p_ in me_t.polygons:
        for i in range(p_.loop_start, p_.loop_start + p_.loop_total):
            uv_t.setdefault(me_t.loops[i].vertex_index, tuple(lay_t[i].uv))
    mw = np.array(tb.matrix_world)
    du, dv, n_ok, n_skip = 0.0, 0.0, 0, 0
    for vi in outline:
        w = mw @ np.append(np.array(me_t.vertices[vi].co), 1.0)
        hit = bvh.ray_cast(Vector((float(w[0]), float(w[1]), float(w[2]) + 1.0)), Vector((0, 0, -1)), 5.0)
        if hit[0] is None:
            n_skip += 1
            continue
        lt = me_f.loop_triangles[hit[2]]
        a, b, c = (Vf[i] for i in lt.vertices)
        wt = G.barycentric_transform(Vector((float(w[0]), float(w[1]), float(hit[0].z))), Vector(a.tolist()), Vector(b.tolist()), Vector(c.tolist()),
                                     Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)))
        l1, l2 = wt.x, wt.y
        l0 = 1 - l1 - l2
        uvs = [lay_f[i].uv for i in lt.loops]
        u_f = l0 * uvs[0][0] + l1 * uvs[1][0] + l2 * uvs[2][0]
        v_f = l0 * uvs[0][1] + l1 * uvs[1][1] + l2 * uvs[2][1]
        u_t, v_t = uv_t[vi]
        du = max(du, abs(u_t - u_f))
        d_v = (v_t - v_f + 0.5) % 1.0 - 0.5
        dv = max(dv, abs(d_v))
        n_ok += 1
    return du, dv, n_ok, n_skip


# ----------------------------------------------------------------------------- round 5: the tee-box seam (reviewer, medium: "a hard line cuts across the fairway on the tee shot and the stone path jumps sideways at it")
def tee_cam(ball=(0.0, 0.0), aim=(19.0, 178.0), ground_z=None):
    """CameraRig.FrameAddress in Blender metres: (camera, look-at, ball centre). The ball rests on the ground + 0.055 m."""
    bx, by = ball[0] * YD, ball[1] * YD
    ax, ay = aim[0] * YD, aim[1] * YD
    if ground_z is None:                                    # TEE_BOX top by a ray 1 cm off the ball (a ray exactly through a mesh vertex can slip between triangles)
        tb = bvh_of_objects([bpy.data.objects["TEE_BOX"]]) if (ball == (0.0, 0.0)) else None
        hit = tb.ray_cast(Vector((bx + 0.011, by + 0.007, 40.0)), Vector((0, 0, -1)), 80.0) if tb is not None else None
        ground_z = float(hit[0].z) if hit is not None and hit[0] is not None else D_PLAY_Z_DEFAULT
    d = Vector((ax - bx, ay - by, 0.0)).normalized()
    b = Vector((bx, by, ground_z + 0.055))
    return b - d * 4.5 * YD + Vector((0, 0, 2.4 * YD)), b + d * 1.5 * YD, b


D_PLAY_Z_DEFAULT = 6.0


def _cam_ray(cam, look, px, py, W=900, H=1600, fov=60.0):
    f = (look - cam).normalized()
    r = f.cross(Vector((0, 0, 1))).normalized()
    u = r.cross(f).normalized()
    th = math.tan(math.radians(fov / 2))
    x = ((px + 0.5) / W * 2 - 1) * th * W / H
    y = (1 - (py + 0.5) / H * 2) * th
    return (f + r * x + u * y).normalized()


def tee_path_seam_jump(xstep=2, near=10, far=44):
    """Sideways jump (pixels of the 900 x 1600 tee frame) of the stone path's right edge at the tee box's front edge. The tee box stands 10 cm above the fairway, so the riser
    faces away from the camera and a strip of the fairway (about 0.4 m deep, plus the 0.1 m where the path ribbon already dips) is hidden behind the tee edge: the path edge on the
    fairway side starts further on than the edge on the tee side, by (hidden depth) x (how far the edge leans away from the line of sight). Method: ray casts through the 900 x 1600
    frame, the right edge of the path (path -> non-path going right) on every row from `near` to `far` rows below the seam (tee side) and from `near` to `far` rows above it
    (fairway side); a straight line is fitted through each side and both are evaluated at the seam row; the jump is the difference, the kink the difference of their slopes (px per row).
    Returns (max |jump|, report). Needs the scene (TEE_BOX, FAIRWAY, DRESS_PATH_*) in memory; no render."""
    bpy.context.view_layer.update()
    sc = bpy.context.scene
    dg = bpy.context.evaluated_depsgraph_get()
    cam, look, _b = tee_cam()
    hide = ("BALL_START", "HOLE_CUP", "MARKER_TEE", "MARKER_PIN", "MARKER_UP", "TEE_MARKER_1", "TEE_MARKER_2", "FLAG", "FLAG_POLE")

    def hit_at(px, py):
        d = _cam_ray(cam, look, px, py)
        o = cam.copy()
        for _ in range(6):
            ok, loc, _n, _i, ob, _m = sc.ray_cast(dg, o, d, distance=900.0)
            if not ok:
                return ""
            if ob.name in hide:
                o = loc + d * 1e-3
                continue
            return ob.name
        return ""
    # the seam row: where the column x = 450 turns from TEE_BOX (below) to something else (above)
    seam = None
    for py in range(780, 540, -1):
        a, b = hit_at(450, py), hit_at(450, py - 1)
        if a.startswith("TEE_BOX") and b and not b.startswith("TEE_BOX"):
            seam = py
            break
    if seam is None:
        return 0.0, dict(seam=None, note="no tee box front edge in the tee frame")
    xs = list(range(0, 900, xstep))

    def right_edge(py):
        inside = [hit_at(x, py).startswith("DRESS_PATH") for x in xs]
        tr = [xs[i] for i in range(1, len(xs)) if inside[i - 1] and not inside[i]]       # path -> not path going right
        return tr[-1] if tr else None
    def robust_line(pts):
        r = np.array([q[0] for q in pts], float)
        x = np.array([q[1] for q in pts], float)
        keep = np.ones(len(r), bool)
        for _ in range(4):
            if keep.sum() < 5:
                return None
            k, c = np.polyfit(r[keep], x[keep], 1)
            res = np.abs(x - (k * r + c))
            nk = res <= max(3.0, 2.5 * float(np.median(res[keep])))
            if (nk == keep).all():
                break
            keep = nk
        if keep.sum() < 5:
            return None
        return k, c, r[keep].min(), r[keep].max(), float(res[keep].max()), int(keep.sum())
    fit = {}
    for nm, rows in (("tee", range(seam + near, seam + far, 3)), ("fairway", range(seam - far, seam - near, 3))):
        pts = [(r, right_edge(r)) for r in rows]
        fit[nm] = robust_line([(r, x) for r, x in pts if x is not None])
    if fit["tee"] is None or fit["fairway"] is None:
        return 0.0, dict(seam=seam, note="path edge not found on both sides of the tee box's front edge")
    kt, ct, t_lo, _t_hi, rt, nt = fit["tee"]
    kf, cf, _f_lo, f_hi, rf, nf = fit["fairway"]
    jump = (kf * seam + cf) - (kt * seam + ct)                     # both sides extrapolated to the seam row
    raw = (kf * f_hi + cf) - (kt * t_lo + ct)                      # what a viewer sees: lowest path-edge row above the seam vs highest below it
    kink = kf - kt
    worst = max(abs(jump), abs(raw))
    return worst, dict(seam=seam, jump_px=round(float(jump), 1), raw_step_px=round(float(raw), 1), slope_tee=round(float(kt), 2), slope_fairway=round(float(kf), 2),
                       kink_px_per_row=round(float(kink), 2), gap_rows=int(t_lo - f_hi), fit_resid_px=[round(rt, 1), round(rf, 1)], rows=[nt, nf])


# ----------------------------------------------------------------------------- round 6: the tee step (see TEE_STEP above)
def tee_hidden_k(cam=None):
    """(k, cam_xy, z_plate, z_fairway): the hidden-band factor of the plate's front edge as seen from the address camera. A ray from the camera over the plate's edge (z_plate) meets the
    fairway plate (z_fairway) beyond the edge at C + k (E - C) in plan, k = (cam z - z_fairway) / (cam z - z_plate) (1.0445 for the tee shot: the 10 cm step hides ~0.35 m)."""
    cam_, _look, _ball = tee_cam() if cam is None else cam
    tb, fw = bpy.data.objects["TEE_BOX"], bpy.data.objects["FAIRWAY"]
    zt = float(max(v.co.z for v in tb.data.vertices))
    zf = float(max(v.co.z for v in fw.data.vertices))
    k = (cam_.z - zf) / (cam_.z - zt)
    return k, (float(cam_.x), float(cam_.y)), zt, zf


def _plate_poly(name="TEE_BOX"):
    ob = bpy.data.objects[name]
    zt = float(max(v.co.z for v in ob.data.vertices))
    return _outcrop_rim(ob, zt), zt


def _poly_sd(poly, X, Y):
    """Signed distance (m) of points to a convex counter-clockwise polygon: negative inside (max over the edge half-planes)."""
    A = poly
    B = np.roll(poly, -1, axis=0)
    e = B - A
    ln = np.maximum(np.hypot(e[:, 0], e[:, 1]), 1e-12)
    nx, ny = e[:, 1] / ln, -e[:, 0] / ln
    X = np.atleast_1d(np.asarray(X, float))
    Y = np.atleast_1d(np.asarray(Y, float))
    return ((X[:, None] - A[None, :, 0]) * nx[None] + (Y[:, None] - A[None, :, 1]) * ny[None]).max(axis=1)


def _poly_boundary_dist(poly, X, Y):
    A = poly
    B = np.roll(poly, -1, axis=0)
    e = B - A
    l2 = np.maximum((e ** 2).sum(1), 1e-12)
    X = np.atleast_1d(np.asarray(X, float))
    Y = np.atleast_1d(np.asarray(Y, float))
    t = np.clip(((X[:, None] - A[None, :, 0]) * e[None, :, 0] + (Y[:, None] - A[None, :, 1]) * e[None, :, 1]) / l2[None], 0.0, 1.0)
    return np.hypot(X[:, None] - (A[None, :, 0] + e[None, :, 0] * t), Y[:, None] - (A[None, :, 1] + e[None, :, 1] * t)).min(axis=1)


def _path_lattice(me):
    """(rows n, columns nl) of a DRESS_PATH ribbon made by L.build_path: n rows of (nl top vertices + 2 skirt feet) then 2 x (nl + 1) end-skirt pairs; faces (n + 1) x (nl + 1)."""
    V, F = len(me.vertices), len(me.polygons)
    for nl in range(7, 80):
        if F % (nl + 1):
            continue
        n = F // (nl + 1) - 1
        if n > 1 and n * (nl + 2) + 4 * (nl + 1) == V:
            return n, nl
    raise RuntimeError(f"DRESS_PATH lattice not recognised: {V} vertices {F} faces")


def step_path_over_plate(D, ob, plate="TEE_BOX", cam=None):
    """Cut the flagstone ribbon `ob` along the outline of the raised `plate`, lift the part over the plate onto it, keep the rest on the fairway, join the two with a vertical
    riser, and shift the UVs of the plate part by the hidden band (see TEE_STEP). Plan positions of every original vertex are untouched. Returns a report dict."""
    poly, zt = _plate_poly(plate)
    k_h, cxy, zt2, zf = tee_hidden_k(cam)
    off = L.Z_PATH
    skirt = 0.04
    me = ob.data
    n, nl = _path_lattice(me)
    tile = L.LOOK["LK_PATH"][2]
    # per-row tangent / right vector of the original lattice (for the UV shift): centre column positions
    co0 = np.array([v.co[:] for v in me.vertices])
    stride = nl + 2
    cen = co0[np.arange(n) * stride + nl // 2][:, :2]
    tg = np.gradient(cen, axis=0)
    tg /= np.maximum(np.hypot(tg[:, 0], tg[:, 1]), 1e-9)[:, None]
    from mathutils.kdtree import KDTree
    kd = KDTree(n)
    for i, (x, y) in enumerate(cen.tolist()):
        kd.insert((x, y, 0.0), i)
    kd.balance()
    bm = bmesh.new()
    bm.from_mesh(me)
    uvl = bm.loops.layers.uv.active
    ptop = bm.faces.layers.int.new("ptop")
    bm.faces.ensure_lookup_table()
    for f in bm.faces:
        i = f.index
        f[ptop] = 1 if (i < (n - 1) * (nl + 1) and 1 <= i % (nl + 1) <= nl - 1) else 0
    n_cuts = 0
    for a, b in zip(poly, np.roll(poly, -1, axis=0)):
        e_ = b - a
        ln = float(np.hypot(*e_))
        if ln < 1e-6:
            continue
        nx_, ny_ = e_[1] / ln, -e_[0] / ln
        lo, hi = np.minimum(a, b) - 0.3, np.maximum(a, b) + 0.3
        for v in bm.verts:                         # a vertex within 0.2 mm of the cut line is put ON it (< 0.2 mm in plan, invisible): the cut then does not make a 1e-8 m2 sliver beside it
            if lo[0] <= v.co.x <= hi[0] and lo[1] <= v.co.y <= hi[1]:
                d_ = (v.co.x - a[0]) * nx_ + (v.co.y - a[1]) * ny_
                if abs(d_) < 2e-4:
                    v.co.x -= d_ * nx_
                    v.co.y -= d_ * ny_
        sel = []
        for f in bm.faces:
            xs = [v.co.x for v in f.verts]
            ys = [v.co.y for v in f.verts]
            if max(xs) < lo[0] or min(xs) > hi[0] or max(ys) < lo[1] or min(ys) > hi[1]:
                continue
            ds = [(v.co.x - a[0]) * nx_ + (v.co.y - a[1]) * ny_ for v in f.verts]
            if max(ds) > 1e-7 and min(ds) < -1e-7:
                sel.append(f)
        if not sel:
            continue
        geom = list(sel) + list({e2 for f in sel for e2 in f.edges}) + list({v2 for f in sel for v2 in f.verts})
        r = bmesh.ops.bisect_plane(bm, geom=geom, dist=1e-7, plane_co=(float(a[0]), float(a[1]), 0.0), plane_no=(float(nx_), float(ny_), 0.0),
                                   use_snap_center=False, clear_outer=False, clear_inner=False)
        n_cuts += len(r["geom_cut"])
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=2e-6)
    bm.faces.ensure_lookup_table()
    # classify faces: inside the plate outline or outside (no face may straddle the outline any more)
    eps = 2e-5
    cls = {}
    mixed = 0
    for f in bm.faces:
        sd = _poly_sd(poly, [v.co.x for v in f.verts], [v.co.y for v in f.verts])
        if (sd > eps).any() and (sd < -eps).any():
            mixed += 1
        c = f.calc_center_median()
        cls[f] = 1 if _poly_sd(poly, [c.x], [c.y])[0] < 0 else 0
    if mixed:
        raise RuntimeError(f"{ob.name}: {mixed} faces straddle the {plate} outline after the cuts")
    seam = [e for e in bm.edges if len(e.link_faces) == 2 and cls[e.link_faces[0]] != cls[e.link_faces[1]]]
    bd = _poly_boundary_dist(poly, [v.co.x for e in seam for v in e.verts], [v.co.y for e in seam for v in e.verts])
    if len(seam) == 0 or float(bd.max()) > 1e-4:
        raise RuntimeError(f"{ob.name}: {len(seam)} seam edges, farthest {float(bd.max()) if len(bd) else None} m from the {plate} outline")
    r = bmesh.ops.split_edges(bm, edges=seam)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    cls = {f: (1 if _poly_sd(poly, [f.calc_center_median().x], [f.calc_center_median().y])[0] < 0 else 0) for f in bm.faces}
    # pair the seam edges: after the split every seam edge exists twice (one on the plate part, one on the fairway part), at the same plan position
    by_key = {}
    for e in bm.edges:
        if len(e.link_faces) != 1:
            continue
        if float(_poly_boundary_dist(poly, [e.verts[0].co.x, e.verts[1].co.x], [e.verts[0].co.y, e.verts[1].co.y]).max()) > 1e-4:
            continue
        key = frozenset(((round(e.verts[0].co.x, 6), round(e.verts[0].co.y, 6)), (round(e.verts[1].co.x, 6), round(e.verts[1].co.y, 6))))
        by_key.setdefault(key, []).append(e)
    riser_pairs = []
    for key, es in by_key.items():
        if len(es) != 2:
            continue
        e_in = next((e for e in es if cls[e.link_faces[0]] == 1), None)
        e_out = next((e for e in es if cls[e.link_faces[0]] == 0), None)
        if e_in is None or e_out is None:
            continue
        a_in, b_in = e_in.verts
        a_out = next(v for v in e_out.verts if (round(v.co.x, 6), round(v.co.y, 6)) == (round(a_in.co.x, 6), round(a_in.co.y, 6)))
        b_out = next(v for v in e_out.verts if (round(v.co.x, 6), round(v.co.y, 6)) == (round(b_in.co.x, 6), round(b_in.co.y, 6)))
        riser_pairs.append((a_in, b_in, a_out, b_out, e_in.link_faces[0], e_out.link_faces[0]))
    n_in = n_out = n_seam_out = 0
    vside = {}
    for v in bm.verts:
        cs = {cls[f] for f in v.link_faces}
        if len(cs) != 1:
            raise RuntimeError(f"{ob.name}: vertex {v.index} touches faces of both sides after the split")
        vside[v] = cs.pop()
    top_v = {v for v in bm.verts if any(f[ptop] == 1 for f in v.link_faces)}
    zf_top = zf + off
    A_, B_ = poly, np.roll(poly, -1, axis=0)
    E_ = B_ - A_
    NO_ = np.stack([E_[:, 1], -E_[:, 0]], 1) / np.maximum(np.hypot(E_[:, 0], E_[:, 1]), 1e-12)[:, None]      # outward normals
    for v in bm.verts:
        on_seam = float(_poly_boundary_dist(poly, [v.co.x], [v.co.y])[0]) < 1e-4
        if vside[v] == 1:
            v.co.z = (zt + off) if v in top_v else (zt - skirt)
            if on_seam:
                # the plate part's edge stands PLATE_INSET_M inside the outline, so that a ray straight down at it meets the plate and not (by rounding at the exact outline) the fairway 10 cm
                # under it (the library gate PATH_UNDER_BALL reads the collider right below every path vertex); 2 mm of bare grass between the stones and the edge is 0.3 px
                sd_ = ((v.co.x - A_[:, 0]) * NO_[:, 0] + (v.co.y - A_[:, 1]) * NO_[:, 1])
                i_ = int(np.argmax(sd_))
                v.co.x -= NO_[i_, 0] * PLATE_INSET_M
                v.co.y -= NO_[i_, 1] * PLATE_INSET_M
            n_in += 1
        elif on_seam:
            v.co.z = zf_top if v in top_v else (zf - skirt)
            n_seam_out += 1
        else:
            n_out += 1
    # UV shift of the plate part: p' = C + k (p - C), delta in plan = (k - 1) (p - C), expressed in the ribbon's own (lateral, arc) frame
    shift_u = shift_v = 0.0
    cnt_sh = 0
    for f in bm.faces:
        if cls[f] != 1:
            continue
        for lp in f.loops:
            p = lp.vert.co
            i = kd.find((p.x, p.y, 0.0))[1]
            t = tg[i]
            rt = np.array([t[1], -t[0]])
            d = np.array([(k_h - 1.0) * (p.x - cxy[0]), (k_h - 1.0) * (p.y - cxy[1])])
            du, dv = float(d @ rt) / tile, float(d @ t) / tile
            lp[uvl].uv = (lp[uvl].uv[0] + du, lp[uvl].uv[1] + dv)
            shift_u, shift_v, cnt_sh = max(shift_u, abs(du)), max(shift_v, abs(dv)), cnt_sh + 1
    # risers: one vertical quad per seam edge pair, joining the plate part's edge (e_in) to the fairway part's edge (e_out); the pairing was made before the vertices moved
    n_riser = 0
    for (a_in, b_in, a_out, b_out, f_in, f_out) in riser_pairs:
        out_dir = f_out.calc_center_median() - f_in.calc_center_median()
        try:
            fr = bm.faces.new((a_in, b_in, b_out, a_out))
        except ValueError:
            continue
        nrm = fr.normal
        if nrm.x * out_dir.x + nrm.y * out_dir.y < 0:
            fr.normal_flip()
        fr.material_index = f_in.material_index
        fr[ptop] = 0
        fr.smooth = False
        uv_a = next(lp[uvl].uv.copy() for lp in f_in.loops if lp.vert == a_in)
        uv_b = next(lp[uvl].uv.copy() for lp in f_in.loops if lp.vert == b_in)
        for lp in fr.loops:
            lp[uvl].uv = uv_a if lp.vert in (a_in, a_out) else uv_b
        n_riser += 1
    bm.faces.layers.int.remove(ptop)
    bm.to_mesh(me)
    bm.free()
    me.update()
    L._finish_mesh(ob, 50.0)
    return dict(path=ob.name, nl=nl, rows=n, cuts=n_cuts, seam_edges=len(seam), in_verts=n_in, out_seam_verts=n_seam_out, risers=n_riser, k=round(k_h, 4), zt=zt, zf=zf,
                uv_shift_max=(round(shift_u, 4), round(shift_v, 4)), faces=len(me.polygons))


def tee_step(D, info):
    """Round 6: the whole tee-step treatment (path cut + lift + riser, UV shifts of the plate part). Returns a report."""
    if not TEE_STEP:
        return dict(enabled=False)
    rep = dict(enabled=True, paths=[])
    for ob in [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("DRESS_PATH")]:
        poly, _zt = _plate_poly("TEE_BOX")
        co = np.array([v.co[:] for v in ob.data.vertices])
        if (_poly_sd(poly, co[:, 0], co[:, 1]) < 0).any():
            rep["paths"].append(step_path_over_plate(D, ob))
    return rep


# ----------------------------------------------------------------------------- round 6: what the address camera SEES at the tee step (the gates for the reviewer's finding)
SEEN_COLS_GRASS = tuple(range(8, 892, 12))
SEEN_NEAR, SEEN_FAR = 3, 24
SEEN_MAX_GRASS_JUMP = 0.004        # tile units of the 10 m fairway tile = 4 cm of ground: the texture phase jump across the plate edge, extrapolated from both sides (round 5 build measured dV 0.0335 = 33 cm)
SEEN_MAX_PATH_JUMP = 0.008         # tile units of the 5 m path tile = 4 cm


def _tee_seen_bvh():
    """BVH (world) of the surfaces the tee camera sees at the step (TEE_BOX, FAIRWAY, FAIRWAY_FIRSTCUT, GREEN_APRON, DRESS_PATH_*) with per-triangle UVs and object names."""
    bpy.context.view_layer.update()
    Vs, Fs, tags, base = [], [], [], 0
    for ob in [o for o in bpy.data.objects if o.type == 'MESH' and (o.name in ("TEE_BOX", "FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN_APRON") or o.name.startswith("DRESS_PATH"))]:
        me = ob.data
        me.calc_loop_triangles()
        mw = np.array(ob.matrix_world)
        V = np.array([v.co[:] for v in me.vertices]).reshape(-1, 3) @ mw[:3, :3].T + mw[:3, 3]
        uv = np.array([l.uv[:] for l in me.uv_layers.active.data]).reshape(-1, 2) if me.uv_layers.active else None
        for lt in me.loop_triangles:
            Fs.append(tuple(base + int(i) for i in lt.vertices))
            tags.append((ob.name, None if uv is None else uv[list(lt.loops)]))
        Vs += [tuple(p) for p in V.tolist()]
        base += len(V)
    return BVHTree.FromPolygons(Vs, Fs), tags, np.array(Vs), Fs


def tee_seen_seam(cam=None, cols=SEEN_COLS_GRASS, near=SEEN_NEAR, far=SEEN_FAR, W=900, H=1600):
    """Ray casts through the 900 x 1600 tee frame (the address camera). Per pixel column the plate's edge row r0 is where the first hit drops from the plate's height to the fairway's (z < plate - 5 cm).
    GRASS columns (hits TEE_BOX above the seam, FAIRWAY below on both sides): the (u, v) of the hit at rows r0 + near .. r0 + far (plate side) and r0 - near .. r0 - far (fairway side) are fitted by lines
    in the row number; their values at the seam row differ by the texture JUMP the viewer sees (tile units, wrapped to -0.5..0.5). PATH columns (DRESS_PATH on both sides): the same jump of the path
    UV, and the count of rows between r0 - far and r0 + far where the first hit is NOT the path (a grass strip across the stones). Returns dict."""
    cam_, look_, _b = tee_cam() if cam is None else cam
    bvh, tags, Vs, Fs = _tee_seen_bvh()
    tb = bpy.data.objects["TEE_BOX"]
    zt = float(max(v.co.z for v in tb.data.vertices))
    hide_z = zt - 0.05

    def hit(px, py):
        d = _cam_ray(cam_, look_, px, py, W, H)
        h = bvh.ray_cast(cam_, d, 900.0)
        if h[0] is None:
            return None
        nm, uvs = tags[h[2]]
        a, b, c = (Vector(Vs[i].tolist()) for i in Fs[h[2]])
        wt = G.barycentric_transform(h[0], a, b, c, Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)))
        w1, w2 = wt.x, wt.y
        uv = (1 - w1 - w2) * uvs[0] + w1 * uvs[1] + w2 * uvs[2] if uvs is not None else np.zeros(2)
        return nm, float(h[0].z), uv

    def fit(rows, vals):
        r = np.array(rows, float)
        v = np.array(vals, float)
        k, c = np.polyfit(r, v, 1)
        return k, c, float(np.abs(v - (k * r + c)).max())

    wrap = lambda d: (d + 0.5) % 1.0 - 0.5
    grass, path = [], []
    for x in cols:
        r0 = None
        prev = None
        for py in range(900, 500, -1):
            h = hit(x, py)
            if h is None:
                prev = None
                continue
            if prev is not None and prev[1] >= hide_z and h[1] < hide_z:
                r0 = py
                break
            prev = h
        if r0 is None:
            continue
        rows_t = list(range(r0 + near, r0 + far + 1, 3))
        rows_f = list(range(r0 - far, r0 - near + 1, 3))
        ht = [hit(x, r) for r in rows_t]
        hf = [hit(x, r) for r in rows_f]
        if any(q is None for q in ht + hf):
            continue
        names_t = {q[0] for q in ht}
        names_f = {q[0] for q in hf}
        if names_t == {"TEE_BOX"} and names_f <= {"FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN_APRON"} and len(names_f) == 1:
            ju, jv = [], []
            for dim in (0, 1):
                kt, ct, rt_ = fit(rows_t, [q[2][dim] for q in ht])
                kf, cf, rf_ = fit(rows_f, [q[2][dim] for q in hf])
                # unwrap each side on its own (fits on values that may cross an integer): shift the values by their first element's nearest integer
                (ju if dim == 0 else jv).append((cf + kf * (r0 + 0.5)) - (ct + kt * (r0 + 0.5)))
            du, dv = wrap(ju[0]), wrap(jv[0])
            grass.append((x, r0, du, dv))
        elif all(n_.startswith("DRESS_PATH") for n_ in names_t | names_f):
            gap = 0
            for r in range(r0 - far, r0 + far + 1):
                q = hit(x, r)
                if q is None or not q[0].startswith("DRESS_PATH"):
                    gap += 1
            jv = []
            for dim in (0, 1):
                kt, ct, _ = fit(rows_t, [q[2][dim] for q in ht])
                kf, cf, _ = fit(rows_f, [q[2][dim] for q in hf])
                jv.append(wrap((cf + kf * (r0 + 0.5)) - (ct + kt * (r0 + 0.5))))
            path.append((x, r0, jv[0], jv[1], gap))
    return dict(grass=grass, path=path)


def _seen_summary(sc):
    g, p = sc["grass"], sc["path"]
    gu = max((abs(q[2]) for q in g), default=float("nan"))
    gv = max((abs(q[3]) for q in g), default=float("nan"))
    pu = max((abs(q[2]) for q in p), default=float("nan"))
    pv = max((abs(q[3]) for q in p), default=float("nan"))
    gaps = sum(q[4] for q in p)
    return dict(n_grass=len(g), grass_du=gu, grass_dv=gv, n_path=len(p), path_du=pu, path_dv=pv, gap_rows=gaps,
                mean_row=float(np.mean([q[1] for q in g])) if g else None)


def write_info(D, info, path):
    """look_info.json: positions the landmark / proof tools need (metres, Blender frame)."""
    a = info["arch"]
    f = info["falls"][0]
    out = dict(arch=dict(centre=list(a["centre"]), span=a["span"], height=a["height"], facing=a["facing"],
                         outcrop=a["outcrop"].name, mesh=a["arch"].name),
               waterfall=dict(top=list(f["top"]), foot=list(f["foot"]), out=list(f["out"]), width=f["width"], reach=f["reach"]),
               stacks=[dict(xy=list(s["xy"]), H=s["H"], seed=s["seed"], radius=s["radius"], metrics=s.get("metrics", {})) for s in info["stacks"]],
               paths=info["path_xy"], cameras={k: [list(v[0]), list(v[1])] for k, v in CAMERAS.items()})
    with open(path, "w") as fh:
        json.dump(out, fh, indent=1)
