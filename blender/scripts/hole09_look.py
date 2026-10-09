"""hole09_look.py - the Split look pass (POSTCARD_LOOK CONTINUE, 2026-10-04): scenery with the look libraries + the hole's own QA gates.

hole09_build.py calls this module between the scoring build and the look-lib passes. Nothing here moves a scoring vertex: the scoring
meshes (TERRAIN / FAIRWAY / GREEN / TEE_BOX / BUNKER) are built by postcard_lib exactly as before; this module only adds NEW
non-colliding objects (ROCK_ / PLANT_ / DRESS_ names) and re-materials the ridge top (a material only: the lie stays Rough).

What replaces the legacy decorative geometry of the flat build (hole09_build.py of 2026-10-03, kept at
work/postcard-look/v2/hole09/hole09_build.py.at_start):

    legacy ROCK_*/CLIFF_ROCK icosahedra (foot / face / rim boulders)   -> library rocks (PL.scatter_rocks, PL.scatter): wet foot rocks and sea
                                                                          rocks, rim rocks and ridge outcrops (crag + boulder groups)
    ROCK_STACK_nn (custom 64-80 vertex cylinder stacks)                -> PL.place_sea_stack: tapering spires with grassy crowns (+ satellites)
    ruin_wall() / fallen_arch() (rows of ROCK_MEDIUM boulders)         -> hole09_masonry (rough flat-shaded LK_MASONRY stones on wavy bed levels, a doorway with a
                                                                          lintel, a broken voussoir arch with a rubble heap) + fallen DRESS_BLOCK rubble
    surf() WATER_FOAM / WATER_FOAM_CREST strips                        -> deleted; L.build_sea draws sparse broken whitewater patches only

ROUND 2 (2026-10-04, after the skeptic review of round 1), all visual, nothing scoring-relevant:
    bunkers                  -> DRESS_SAND_nn lobes laid on the raised lip rings (build_sand_overlay): organic scalloped sand edges, <= 9 mm over the collider
    flat ridge top           -> DRESS_KNOLL_nn rock-capped hummocks (build_knolls), more crag groups, shrub thickets round cluster centres instead of dots
    scrub / rough colour step -> a shrub + rock chain every 2.2 m along every boundary edge (cover_scrub_boundary), gap-fill stones; the chain keeps the DESIGN's own
                                 4 yd route rule (hole09_design: "keep rocks >= 4 yd off [the route]"), every other scenery piece keeps the builder's 8 yd margin
    24-vertex masonry blocks -> rough, rounded, flat-shaded variants with 98 welded vertices, 4 per kind (chip_blocks, repair round 2; round 2 had one chipped cuboid per kind)

REPAIR ROUND 2 (2026-10-05, after the review "the ruin still reads as stacked bricks"): the ruin / arch are rebuilt by hole09_masonry (flat shading, brick-interior UV windows, skyline rubble on wavy
bed levels, ragged tops, the arch seated in both piers with ONE crown wedge missing: ARCH_H_RIGHT 3.9 -> 4.4 m so no wedge pokes out of the right pier) and chip_blocks gives the fallen blocks
rough rounded variants. The design file, scoring and every non-masonry piece are unchanged.

REPAIR ROUND 3 of the CONTINUE job (2026-10-05, review "the ruin still reads as stacked boxes ... diagonal triangle shading ... floating arch crown"): the ruin / arch stones are planar-faced polyhedra (hole09_stone.py:
one polygon = one plane = one UV mapping; the round-2 hull faces were split into triangles with independent UV windows, 77 % of the near-coplanar pairs mismatched), tops and ~1 stone in 4 are LK_ROCK, the arch has an
outer haunch course (no floating crown), ARCH_H 6.2 -> 5.8 and ARCH_H_RIGHT 4.4 -> 4.9; chip_blocks keeps LK_MASONRY but each block has ONE continuous projection per axis region; N_SHRUB_RIDGE 132 -> 116 / N_TUFT_OUT 64 -> 56
(triangle budget: the props library's new shrub meshes cost +3.9k, the masonry +5k). hole09_build.py: TEE_BOX UVs compensate the 10 cm plate parallax for the address camera, the blend is saved without relative_remap.

ROUND 3 (2026-10-04, after the second skeptic review): DRESS_KNOLL_nn are faceted, steeper hummocks (H/R .42-.52, jittered rings) whose LK_ROCK part is a small irregular
crest outcrop instead of a flat grey disc; hole09_build.py pushes the strata skin further out (SKIN_DISP_M / SKIN_JAG_M / SKIN_MAX_OUT_M) for a more jagged coast. Nothing else changed.

GRASS + RUIN PASS (2026-10-05, the 'grass liveliness + gentle sway' add-on and the carry-over ruin finding; builds on the shared fixers' new tuft library / sway vertex colours):
    EDGE FRINGE      build_grass(): PL.scatter_fringe in ZONES (tee view y 30-112 m, ribbon middle, green end, ruin ridge, far field; olive tufts on the scrub, green tufts off it): clumps of 3-7 tall leaning tufts
                     along the rough edge of the fairway / green / tee and the cliff lips, 1.1 m off the lip (the hole's own H_NO_PLANT_ON_WATER margin is 1.0 m); the old 117-triangle tufts further than 25 m from a
                     shot ball became 36-triangle fillers (PL.cheapen_tufts), ridge shrubs 116 -> 76 (thickets 30 -> 18 centres: H_RIDGE_THICKETS), outer shrubs 30 -> 20, ridge stones 56 -> 44: 247.4k -> ~247.5k triangles
    OPEN SCRUB        an olive clump field on the ridge in front of the ruin landmark camera (candidate centres = ground points that project into the landmark frame), tuft clumps at the foot of the ruin walls
                     (>= 0.6 m from any masonry vertex), a tuft skirt (3-5 ground-rooted tufts) round the 12 bushes nearest the ruin
    NOT DONE         PL.tuftify_shrubs / tuftify_crowns (tufts ON a shrub lump / a sea-stack crown): the hole's H_NO_PLANT_ON_WATER wants every plant rooted on the ground and the verifier's NO_PLANT_ON_WATER wants
                     ground or a rock top within 0.5 m under the lowest blade: measured FAIL on both (r8/g1, g2), the gates stay as they are
    RUIN (hole09_masonry / hole09_stone): stones 15 % bigger, the four vertical edges chamfered 5-12 cm + weathered arris bevels (octagon-ish plan: the tops stop being rectangles), no near-coplanar side pair
                     (stone.bad_pairs: RUIN_NO_UV_SPLIT), bottom corner chips keep the old size (RUIN_BASE_BURIED), the broken arch keeps ALL 11 wedges: the crown wedge has SLIPPED 13 cm toward the opening
                     (a radial slide keeps both side planes in face contact): no two floating halves with a gap, the heap of rubble stays under it
Legacy hole gates kept in spirit (rewritten for the new pieces, see qa_gates): the 8 yd clearance between scenery rocks and the dry safe
ridge route (ROUTE_CLEAR_YD), no rim rock on the ridge lip that faces the channel (RIM_STRETCHES), nothing of the ruin on the drive landing
band or in the sight lines, the ruin 7+ yd inside the cliff, the sea stack in the channel mouth out of every sight line.
"""
import json
import math
import os
import random
import sys

sys.dont_write_bytecode = True
import numpy as np
import bpy
import bmesh
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

import postcard_lib as P  # noqa: E402
import postcard_look_lib as L  # noqa: E402
import postcard_props_lib as PL  # noqa: E402
import hole09_masonry as HM  # noqa: E402

YD = P.YD
ROUTE_CLEAR_YD = 8.0                 # legacy: no scenery rock / masonry / shrub within 8 yd of the dry safe ridge route
SCRUB_SOFT_M = 9.0                   # width of the patchy blend zone round the scrub polygon (m)
TUFT_ROUTE_CLEAR_M = 2.5             # tufts (0.25-0.45 m) may come closer to the route, but not into a ball lie on it
CHANNEL_LIP_CLEAR_M = 6.0            # legacy RIM_STRETCHES rule: no rim rock on the ridge lip that faces the channel
ARCH_FACING_DEG = 172.0              # repair round 1: the direction you look THROUGH the opening (clockwise from +Y). 172 = toward the tee along the ridge, so the opening faces the ruin landmark camera (looking north) and reads as an arch hole (the old 110 showed the arch edge-on)
RUIN_H = {1: 4.0, 2: 5.6, 3: 3.4}   # max wall heights by design wall index (the SCENERY heights 3.0 / 4.5 / 2.5 m are visual maxima; the broken profile stays below)
RUIN_SCALE = 1.1                     # plan scale of the SCENERY ruin about its centre (east edge stays >= 7 yd inside the cliff: gate H_RUIN_INSIDE_CLIFF)
RUIN_CLIFF_CLEAR_YD = 7.3            # ruin blocks / rubble centres stay this far inside the cliff (the design: 7+ yd)
RUIN_SHIFT_YD = (-1.0, 0.0)          # the scaled ruin moves 1 yd west: keeps >= 7 yd from the east cliff while the west wall stays > 6 yd off the route
RUIN_T = 1.25                        # masonry wall thickness (m) (repair round 1: was 1.5 box slabs)
RUIN_DOOR_W, RUIN_DOOR_H = 2.4, 2.4  # the doorway of the tall wall: clear width and height (m); the lintel stone bears 0.55 m on each jamb
RUIN_VERTEX_SHORE_M = 6.45           # EVERY masonry vertex stays this far inside the shore (the QA gate: 7 yd - 0.1 m = 6.3 m)
RUIN_VERTEX_ROUTE_M = 5.6            # ... and this far off the dry safe route (the QA gate: 6 yd = 5.49 m)
ARCH_R, ARCH_T, ARCH_DEPTH, ARCH_PIER_W, ARCH_SPRING, ARCH_H_RIGHT = 2.0, 0.75, 1.3, 1.7, 2.7, 4.9   # broken arch: half clear span, ring thickness, depth, pier width, spring height, right pier top (round 3: 4.4 -> 4.9, the haunch stones fill the angle to the ring)
ARCH_SHIFT_YD = (1.0, 5.5)           # the arch stands SOUTH-EAST of the SCENERY spot (repair round 1: +1.5 yd east so the west pier clears the dry safe route by >= 6 yd; the old -0.5 cut the pier)
ARCH_H = 5.8                         # the tall (left) pier top above the ridge (m) (round 3: 6.2 -> 5.8: the thin spike above the ring crown is gone)
RUIN_COURSE_H, RUIN_BLOCK_L = 0.85, (1.3, 2.1)     # chunkier courses than the lib default (0.62 / 0.9-1.5 m): reads on a phone at 40 yd
OUTCROP_GROUPS, OUTCROP_SPACING_M = 24, 11.5       # round 2: more crag groups (was 16 / 15 m: only 12 fitted), crags are what makes the flat ridge top read as a rugged crest
N_SHRUB_RIDGE, N_SHRUB_OUT, N_AGAVE, N_FLOWER_RIDGE, N_FLOWER_OUT, N_STONES = 76, 16, 6, 14, 6, 36     # grass pass 2026-10-05: 116 -> 76 ridge shrubs, 30 -> 20 outer shrubs (the fringe is paid for by trimmed shrubs + cheapened tufts + a coarser cliff skin step; 250k budget). round 3: 132 -> 116 ridge shrubs (the props library's new shrub meshes cost +3.9k triangles; the masonry stones cost +5k: 250k budget)
N_TUFT_CLUSTER, N_TUFT_SPRINKLE, N_TUFT_OUT = 60, 24, 56     # round 2: tufts are 117 triangles for a 0.4 m plant; the budget goes to crags, knolls and the sand lobes
SHRUB_CLUSTERS, SHRUB_CLUSTER_SIGMA_M, SHRUB_CLUSTER_SPACING_M = 18, 2.7, 12.0   # round 2: ridge shrubs grow in thickets round cluster centres, not as evenly spread dots
KNOLLS, KNOLL_R_M, KNOLL_H_RATIO = 30, (3.0, 5.4), (0.72, 0.96)      # Goal15: real 2–4m scenery relief, inside the old safe footprints; frozen scoring terrain stays at6m
KNOLL_CROSS_RATIO = 0.70                                         # narrow along the ridge spine; the long axis still fits the original radius/clearance bound
KNOLL_ROUTE_CLEAR_YD = 5.0           # DRESS_KNOLL hummocks (low, soft, no hazard): the design's 4 yd + 1 yd, measured on the farthest vertex of the mesh
COVER_ROUTE_CLEAR_YD = 4.0           # boundary-cover shrubs / rocks keep the DESIGN's rule (hole09_design: "keep rocks >= 4 yd off [the route]"); every other scenery piece keeps 8 yd
COVER_STEP_M = 2.2                   # spacing of the boundary-cover chain along the LK_SCRUB / rough edge
SAND_Z_OFF = 0.009                   # DRESS_SAND overlay height over the collider (the gate allows 1.2 cm at the vertices)
GRASS_ZONE_TRIS = dict(tee_view=8500, ribbon_mid=4000, green_end=7000, ruin=2500, far_green=400, far_olive=200)
GRASS_FIELD_N, GRASS_FIELD_CLUMPS, GRASS_FIELD_NEAR = 130, 32, 6            # olive clump field on the scrub ridge in front of the ruin landmark camera (open ground: the edge fringe alone leaves it a carpet)   # grass pass 2026-10-05: triangle budgets of the edge-fringe zones (see build_grass)
GRASS_WALLFOOT_N = 60                                       # tufts at the foot of the ruin walls
GRASS_TUFTIFY_SHRUBS = 12                                  # bushes (nearest the ruin) that get a tuft skirt   # bushes tuftified round the ruin; their triangle budgets
GRASS_CHEAP_NEAR_M = 25.0               # old 117-triangle tufts farther than this from every shot ball become the 36-triangle fillers (the fringe is paid for by them)
SEEDS = dict(stones=29, ruin_s=52, ruin_w=55, ruin_e=49, reef=28, stacks=11, foot=21, sea=22, gap=23, rim=24, outcrop=25, ruin=26, arch=27, shrub_ridge=31, tuft_ridge=32, flower=33,
             shrub_out=34, tuft_out=35, cover=36, agave=37, rubble=38, knoll=41, sand=43, chip=47, cover_chain=48, cluster=49, fringe_g=91, fringe_o=92)


# ----------------------------------------------------------------------------------------------- geometry helpers (metres)
def poly_m(pts_yd):
    return np.array([P.m(x, d) for x, d in pts_yd], float)


def seg_arrays(poly):
    poly = np.asarray(poly, float)
    return poly[:-1], poly[1:]


def dist_to_poly(poly, X, Y):
    A, B = seg_arrays(poly)
    pts = np.stack([np.asarray(X, float).ravel(), np.asarray(Y, float).ravel()], 1)
    return P._dist_pts_segs(pts, A, B).reshape(np.asarray(X).shape)


def route_m(D):
    return poly_m(D.SCENERY["ridge"]["route"])


def spine_m(D):
    return poly_m(D.SCENERY["ridge"]["spine"])


def center_m(D):
    return poly_m(D.CENTERLINE)


def stretch_poly_m(D, name):
    """Polyline (metres) of the SHORE points of one named SCENERY stretch."""
    for st in D.SCENERY["shore_stretches"]:
        if st["name"] == name:
            return poly_m(D.SHORE[st["first"]:st["last"] + 1])
    raise KeyError(name)


def mask_far(poly, r_m):
    return lambda X, Y: dist_to_poly(poly, X, Y) >= r_m


def mask_ridge_side(D):
    """True where the point is closer to the ridge spine than to the ribbon centerline (the verifier's ridge_side)."""
    sp, cl = spine_m(D), center_m(D)
    return lambda X, Y: dist_to_poly(sp, X, Y) < dist_to_poly(cl, X, Y)


def mask_not_ridge_side(D):
    m = mask_ridge_side(D)
    return lambda X, Y: ~m(X, Y)


def mask_in_poly_yd(poly_yd):
    pm = [P.m(x, d) for x, d in poly_yd]
    return lambda X, Y: P._inside_np(pm, X, Y)


def scrub_polygon_yd(D, ext_yd=(36.0, 36.0), seed=909):
    """Polygon (course yards) of the olive scrub on the ridge top: the design ridge spine buffered by the ridge half width + 12 yd (what the lib
    smoke does), but (a) the spine is extended `ext_yd` yd past both ends (into the tee bridge / green block, where the ridge joins the ribbon
    ends) and (b) the half width tapers to a rounded, wavy tip there, so the olive does not stop on a straight cut across the open rough
    (the old flat ends produced two ~37 yd straight seams at D 59.6 and D 461.2). The boundary then follows the 6.5 m terrain triangles."""
    R = D.SCENERY["ridge"]
    w = R.get("width_yd", 40.0)
    hw0 = float(max(w) if isinstance(w, (list, tuple)) else w) / 2 + 12.0
    S = np.array(R["spine"], float)
    t0 = S[0] - S[1]
    t0 /= np.hypot(*t0)
    t1 = S[-1] - S[-2]
    t1 /= np.hypot(*t1)
    rng = np.random.default_rng(seed)
    tips_n = [(1.0, 0.10), (0.78, 0.46), (0.52, 0.80), (0.26, 0.97)]       # (fraction of the extension from the old end... , half width fraction)
    pts, hws = [], []
    for f, hf in tips_n:                                                   # north tip -> old start
        pts.append(S[0] + t0 * ext_yd[0] * f)
        hws.append(hw0 * hf)
    for p in S:
        pts.append(p)
        hws.append(hw0)
    for f, hf in reversed(tips_n):                                         # old end -> south tip
        pts.append(S[-1] + t1 * ext_yd[1] * f)
        hws.append(hw0 * hf)
    pts = np.array(pts)
    hws = np.array(hws) * (1.0 + 0.10 * rng.uniform(-1, 1, len(pts)))     # the outline wobbles about 10 %
    tg = np.gradient(pts, axis=0)
    tg /= np.maximum(np.hypot(tg[:, 0], tg[:, 1]), 1e-9)[:, None]
    nrm = np.stack([-tg[:, 1], tg[:, 0]], 1)
    left, right = pts + nrm * hws[:, None], pts - nrm * hws[:, None]
    return [tuple(p) for p in np.vstack([right, left[::-1]])]


def region_clusters(centers, sigma):
    cen = np.asarray(centers, float).reshape(-1, 2)

    def f(rng, n):
        k = [rng.randrange(len(cen)) for _ in range(n)]
        X = np.array([cen[i, 0] + rng.gauss(0.0, sigma) for i in k])
        Y = np.array([cen[i, 1] + rng.gauss(0.0, sigma) for i in k])
        return X, Y, None
    return f


def region_union(*regs, weights=None):
    w = weights or [1.0] * len(regs)
    cw = np.cumsum(w) / sum(w)

    def f(rng, n):
        Xs, Ys = [], []
        for _ in range(n):
            r = regs[int(np.searchsorted(cw, rng.random()))]
            x, y, _z = r(rng, 1)
            if len(x):
                Xs.append(float(x[0]))
                Ys.append(float(y[0]))
        return np.array(Xs), np.array(Ys), None
    return f


def stats_of(objs):
    return len(objs)


# ----------------------------------------------------------------------------------------------- scrub material (core + soft blend zone)
def assign_scrub_soft(D, core_poly_yd, soft_m=SCRUB_SOFT_M, seed=7, log=print):
    """LK_SCRUB on every TERRAIN top face whose centroid is inside the core polygon, plus a soft zone of `soft_m` metres outside it where a
    smooth noise picks patches (the chance falls from ~85 % at the polygon to ~15 % at the zone edge), so the olive does not end on a ruler line
    but breaks into patches of scrub in the rough. Material slots only: geometry and lies are untouched. Returns (faces_core, faces_soft)."""
    ob = D.terrain or bpy.data.objects[D.terrain_name]
    me = ob.data
    W, ls, lt, lv, mi, pn = L._mesh_arrays(ob)
    pol = L._poly_of_loop(ls, lt, len(lv))
    cen = np.zeros((len(ls), 2))
    np.add.at(cen, pol, W[lv, :2])
    cen /= lt[:, None]
    top = (pn[:, 2] > 0.5) & L._top_mask(D, W, ls, lt, lv, pn)
    poly = np.array([P.m(x, d) for x, d in core_poly_yd], float)
    inside = P._inside_np([tuple(q) for q in poly], cen[:, 0], cen[:, 1])
    A = poly
    B = np.roll(poly, -1, axis=0)
    dist = P._dist_pts_segs(cen, A, B)
    prob = np.clip(1.0 - dist / soft_m, 0.0, 1.0)
    n = L.fbm(cen[:, 0], cen[:, 1], 7.0, seed, 3)
    soft = (~inside) & (dist < soft_m) & (n < 0.15 + 0.70 * prob)
    sel = top & (inside | soft)
    mat = L.look_material("LK_SCRUB", D)
    if mat.name not in [m_.name for m_ in me.materials if m_]:
        me.materials.append(mat)
    k = [m_.name if m_ else "" for m_ in me.materials].index(mat.name)
    mi[sel] = k
    me.polygons.foreach_set("material_index", mi.astype(np.int32))
    me.update()
    log(f"[hole09] scrub material: {int((top & inside).sum())} core faces + {int((top & soft).sum())} soft-zone faces ({soft_m:g} m) -> LK_SCRUB")
    return int((top & inside).sum()), int((top & soft).sum())


# ----------------------------------------------------------------------------------------------- scrub boundary
def scrub_boundary_mid(D):
    """Midpoints (metres) of the TERRAIN top edges between an LK_SCRUB face and a non-scrub top face (the zigzag at the ridge ends)."""
    ob = D.terrain or bpy.data.objects[D.terrain_name]
    me = ob.data
    names = [m.name if m else "" for m in me.materials]
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    out = []
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        f0, f1 = e.link_faces
        if f0.normal.z < 0.5 or f1.normal.z < 0.5:
            continue
        s0 = names[f0.material_index] == "LK_SCRUB"
        s1 = names[f1.material_index] == "LK_SCRUB"
        if s0 != s1:
            a, b = e.verts[0].co, e.verts[1].co
            mw = ob.matrix_world
            a, b = mw @ a, mw @ b
            out.append(((a.x + b.x) / 2, (a.y + b.y) / 2, (a - b).length))
    bm.free()
    return np.array(out, float).reshape(-1, 3)


# ----------------------------------------------------------------------------------------------- bunker sand overlay (DRESS_SAND_nn)
def bunker_lip_geometry(D):
    """The ring construction of BUNKER_nn_LIP exactly as postcard_lib.build_play_surfaces makes it (same functions, same candidate scales): per bunker the
    inner (scale 1.0, z play+0.32), mid (crest, z play+0.57) and outer (z play+0.22) ring points, the `good` mask (angles that have lip faces) and s_out.
    The play meshes are NOT touched; the overlay only reads this to lay sand on the lip surface."""
    pz = D.play_z
    out, bi = [], 0
    for hz in D.hole.hazards:
        if hz[0] != "bunker":
            continue
        bi += 1
        cx, cy = P.m(hz[1], hz[2])
        a, b = hz[3] / 2 * YD, hz[4] / 2 * YD
        ring = P._bunker_ring_pts(hz, 1.0)
        n = len(ring)
        cand = [1.22, 1.17, 1.12, 1.07, 1.03]
        ok = np.zeros((len(cand), n), bool)
        for k, sc_ in enumerate(cand):
            Pk = P._bunker_ring_pts(hz, sc_, n)
            dirv = (Pk - np.array([cx, cy])) / np.maximum(np.hypot(Pk[:, 0] - cx, Pk[:, 1] - cy), 1e-6)[:, None]
            far = Pk + dirv * 0.9
            ok[k] = (P.lie_codes_m(D, Pk[:, 0], Pk[:, 1]) != P.LIE_WATER) & (P.lie_codes_m(D, far[:, 0], far[:, 1]) != P.LIE_WATER)
        has = ok.any(axis=0)
        s_out = np.where(has, np.array(cand)[np.where(has, ok.argmax(axis=0), 0)], 1.0)
        inner_ok = P.lie_codes_m(D, ring[:, 0], ring[:, 1]) != P.LIE_WATER
        t = np.linspace(0, 2 * np.pi, n, endpoint=False)

        def rpts(sv, a=a, b=b, cx=cx, cy=cy, t=t):
            return np.stack([cx + a * sv * np.cos(t), cy + b * sv * np.sin(t)], 1)
        out.append(dict(index=bi, n=n, c=(cx, cy), a=a, b=b, inner=rpts(1.0), mid=rpts(1.0 + (s_out - 1.0) * 0.45), outer=rpts(s_out), good=has & inner_ok,
                        s_out=s_out, z=(pz + P.Z_SAND + 0.02, pz + 0.57, pz + 0.22)))
    return out


def sand_levels(bun, seed, rows=6):
    """Coverage level (0..rows) of the sand overlay on every column of one bunker's lip: smooth lobes (two sines of different wavelength, periodic in the angle),
    slope-limited to one row per column so a lobe edge is a ramp, never a radial cut. Columns: 2*i = the lip angle i, 2*i+1 = the midpoint to i+1."""
    n = bun["n"]
    C = 2 * n
    good = bun["good"]
    exist = np.zeros(C, bool)
    for i in range(n):
        exist[2 * i] = good[i]
        exist[2 * i + 1] = good[i] and good[(i + 1) % n]
    rg = np.random.default_rng(seed)
    pa, pb = rg.uniform(0, math.tau, 2)
    ka, kb = (3, 8) if bun["index"] % 2 else (4, 9)
    t = np.arange(C) * math.tau / C
    noise = 0.5 + 0.5 * (0.64 * np.sin(ka * t + pa) + 0.36 * np.sin(kb * t + pb))
    cov = np.clip((noise - 0.30) / 0.50, 0.0, 1.0)
    lvl = np.rint(cov * rows).astype(int)
    lvl[lvl == 1] = 0                                           # no 0.2 m slivers
    lvl[~exist] = 0
    for _ in range(2 * rows + 2):                               # slope limit between existing neighbours
        ch = False
        for u in range(C):
            for v in ((u - 1) % C, (u + 1) % C):
                if exist[u] and exist[v] and lvl[u] > lvl[v] + 1:
                    lvl[u] = lvl[v] + 1
                    ch = True
        if not ch:
            break
    return lvl, exist


def build_sand_overlay(D, log=print):
    """DRESS_SAND_01..03: sand (LK_SAND) laid on the raised lip ring of each scoring bunker in lobes, so the sand reads as an irregular organic patch with a
    ragged edge instead of a perfect ellipse inside a uniform green ring (split.jpg's bunkers are peanut-shaped). VISUAL ONLY: the play meshes
    (BUNKER_nn sand, BUNKER_nn_LIP, TERRAIN, FAIRWAY ...) are untouched and the lie is still decided by Hole.LieAt (the ellipse); the overlay never leaves the
    lip footprint, so it stays on one ground surface (no step to straddle) and sits SAND_Z_OFF (9 mm) over it (gate PATH_UNDER_BALL: <= 1.5 cm, >= 3 mm,
    no ball hidden). Vertices are exact points of the lip faces (ring rows at 0, 1/3, 2/3 of the way inner -> crest and crest -> outer edge, plus the midpoint
    columns in between), so the overlay follows the lip's creases: no chord can dip under the crest. Returns (objects, info)."""
    mat = L.look_material("LK_SAND", D)
    tile = L.LOOK["LK_SAND"][2]
    objs, info = [], []
    for bun in bunker_lip_geometry(D):
        n, good = bun["n"], bun["good"]
        z_in, z_mid, z_out = bun["z"]
        lip = bpy.data.objects.get(f"BUNKER_{bun['index']:02d}_LIP")
        if lip is None or not good.any():
            info.append(dict(index=bun["index"], faces=0, note="no lip"))
            continue
        # check the replica against the real lip mesh: every inner / mid / outer point of a good angle is a vertex of BUNKER_nn_LIP
        lv_ = np.array([tuple(v.co) for v in lip.data.vertices])
        for key, zz in (("inner", z_in), ("mid", z_mid), ("outer", z_out)):
            for i in np.nonzero(good)[0]:
                d = np.abs(lv_ - np.array([bun[key][i, 0], bun[key][i, 1], zz])).max(axis=1).min()
                if d > 1e-4:                                   # the mesh stores float32 coordinates
                    raise RuntimeError(f"bunker {bun['index']} lip replica mismatch ({key} point {i}, {d:.2e} m): postcard_lib changed, the sand overlay cannot follow the lip")
        # rows 0..6 of every lip angle (exact points of the lip faces)
        R = np.zeros((n, 7, 3))
        for i in range(n):
            p0 = np.array([*bun["inner"][i], z_in])
            p3 = np.array([*bun["mid"][i], z_mid])
            p6 = np.array([*bun["outer"][i], z_out])
            for k in range(7):                          # the end rows sit 1.5 % inside the lip edges: a vertex exactly on a mesh boundary edge is ray-ambiguous (it may hit the sand / fairway below)
                if k <= 3:
                    R[i, k] = p0 + (p3 - p0) * max(k / 3.0, 0.015)
                else:
                    R[i, k] = p3 + (p6 - p3) * min((k - 3) / 3.0, 0.985)
        C = 2 * n
        lvl, exist = sand_levels(bun, SEEDS["sand"] + bun["index"])
        col = np.zeros((C, 7, 3))
        for u in range(C):
            if not exist[u]:
                continue
            col[u] = R[u // 2] if u % 2 == 0 else 0.5 * (R[u // 2] + R[(u // 2 + 1) % n])
        verts, vid = [], {}

        def vtx(u, r):
            if (u, r) not in vid:
                p = col[u, r]
                vid[(u, r)] = len(verts)
                verts.append((float(p[0]), float(p[1]), float(p[2]) + SAND_Z_OFF))
            return vid[(u, r)]
        faces = []
        for u in range(C):
            v = (u + 1) % C
            if not (exist[u] and exist[v]):
                continue
            lu, lw = int(lvl[u]), int(lvl[v])
            lo, hi = min(lu, lw), max(lu, lw)
            for r in range(6):
                if r < lo:
                    faces.append((vtx(u, r), vtx(u, r + 1), vtx(v, r + 1), vtx(v, r)))
                elif r == lo and hi > lo:
                    if lu > lw:
                        faces.append((vtx(u, r), vtx(u, r + 1), vtx(v, r)))
                    else:
                        faces.append((vtx(u, r), vtx(v, r + 1), vtx(v, r)))
        faces = [f for f in faces if _poly_area(verts, f) > 1e-5]
        if not faces:
            info.append(dict(index=bun["index"], faces=0, note="no faces"))
            continue
        uvs = [[(verts[i][0] / tile, verts[i][1] / tile) for i in f] for f in faces]
        ob = L._new_piece(D, L._next_name(D, "DRESS_SAND"), verts, faces, uvs, [0] * len(faces), [mat], col="STRUCTURES", sharp_deg=50.0)
        ob["h9_class"] = "sand"
        objs.append(ob)
        ext = np.maximum(lvl[exist], 0)
        info.append(dict(index=bun["index"], faces=len(faces), name=ob.name, columns=int(exist.sum()), covered=float((lvl[exist] >= 2).mean()), max_level=int(ext.max()),
                         s_out=float(bun["s_out"][good].mean())))
        log(f"[hole09] sand overlay {ob.name}: {len(faces)} faces, {int(exist.sum())} columns, {float((lvl[exist] >= 2).mean()):.0%} of the lip columns carry sand, "
            f"max level {int(ext.max())}/6, mean lip scale {float(bun['s_out'][good].mean()):.3f}")
    return objs, info


def _poly_area(verts, f):
    p = np.array([verts[i] for i in f])
    a = 0.0
    for i in range(1, len(f) - 1):
        a += 0.5 * np.linalg.norm(np.cross(p[i] - p[0], p[i + 1] - p[0]))
    return a


# ----------------------------------------------------------------------------------------------- chipped masonry blocks
def chipped_mesh(src, name, seed, min_verts=64):
    """A worn copy of a library DRESS_BLOCK mesh with at least `min_verts` welded vertices (the library blocks have 24: bevelled cuboids): the longest edges
    are split first, then random ones, the new vertices are pushed in 1-3.5 cm (chipped edges), the old ones jitter 0.8 cm, everything is triangulated."""
    rnd = random.Random(seed)
    bm = bmesh.new()
    bm.from_mesh(src)
    bm.edges.ensure_lookup_table()
    edges = sorted(bm.edges, key=lambda e: (-round(e.calc_length(), 4), e.index))
    k = 0
    while len(bm.verts) + k < min_verts + 6 and k < len(edges):
        k += 1
    pick = edges[:k // 2] + rnd.sample(edges[k // 2:], k - k // 2) if k > 1 else edges
    old = {v.index for v in bm.verts}
    res = bmesh.ops.subdivide_edges(bm, edges=list(pick), cuts=1, use_grid_fill=False)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    for v in bm.verts:
        if v.index in old:
            v.co += Vector((rnd.uniform(-0.008, 0.008), rnd.uniform(-0.008, 0.008), rnd.uniform(-0.008, 0.008)))
        else:
            v.co -= v.normal * rnd.uniform(0.010, 0.035)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    sm = bool(src.polygons[0].use_smooth) if len(src.polygons) else False
    for f in bm.faces:
        f.smooth = sm
    new = src.copy()
    new.name = name
    bm.to_mesh(new)
    bm.free()
    new.update()
    return new


def chip_blocks(D, log=print):
    """Replace the mesh of every placed DRESS_BLOCK_A / _B instance (fallen ruin blocks, rubble) by a rough, rounded, flat-shaded variant (hole09_masonry.make_block_variant: >= 64 welded vertices,
    brick-interior UV windows). Repair round 2 (2026-10-05): four variants per kind (the instances no longer all share one shape), sized like the library block they replace; the old
    chipped_mesh (a bevelled cuboid with smooth shading and the library's tiled UVs) showed the 'paving brick' top and the box silhouette of the review."""
    protos, variants, n, counter = {}, {}, 0, {}
    for ob in list(bpy.data.objects):
        if ob.type != 'MESH' or ob.data is None:
            continue
        base = ob.data.name.split(".")[0]
        if base not in ("DRESS_BLOCK_A", "DRESS_BLOCK_B"):
            continue
        if base not in variants:
            src = ob.data
            co = np.array([tuple(v.co) for v in src.vertices], float)
            dims = tuple(float(x) for x in (co.max(0) - co.min(0)))
            protos[base] = (src, dims, [m_ for m_ in src.materials])
            variants[base] = []
            for k in range(4):
                rr = random.Random(SEEDS["chip"] * 10 + k + (0 if base.endswith("A") else 50))
                dv = (dims[0] * rr.uniform(0.9, 1.15), dims[1] * rr.uniform(0.85, 1.15), dims[2] * rr.uniform(0.85, 1.1))
                me = HM.make_block_variant(f"{base}_CHIP{k}", SEEDS["chip"] * 10 + k + (0 if base.endswith("A") else 50), dims=dv)
                for m_ in protos[base][2]:
                    me.materials.append(m_)
                if not me.materials:
                    me.materials.append(L.look_material("LK_MASONRY", D))
                variants[base].append(me)
        lib_proto = bool(ob.users_collection) and all(c.name == "ASSET_LIBRARY" for c in ob.users_collection)
        old = ob.data
        if lib_proto:
            ob.data = variants[base][0]               # the hidden library prototypes too: no 24-vertex DRESS_BLOCK mesh is left in the blend (they are not exported anyway)
        else:
            k = counter.get(base, 0)
            counter[base] = k + 1
            ob.data = variants[base][k % 4]
            n += 1
        if old.users == 0:
            bpy.data.meshes.remove(old)
    log("[hole09] rough masonry blocks: " + ", ".join(f"{k}: {len(v)} variants of {sorted(PL.welded_vertex_count(m_) for m_ in v)} welded vertices / {len(v[0].polygons)} triangles" for k, v in variants.items()) + f"; {n} instances")
    return n, {m_.name: PL.welded_vertex_count(m_) for v in variants.values() for m_ in v}


# ----------------------------------------------------------------------------------------------- ridge knolls (DRESS_KNOLL_nn)
def _knoll_mesh(D, name, cx, cy, R, H, seed, ground, tangent=(0.0, 1.0)):
    """One conformal hummock (round 3: a faceted, craggy dome): an apex + 6 rings (u = .22 .42 .62 .80 .92 1.0 of a wobbly radius R, 16 segments), height
    H*(1 - u**1.2) - 0.10 (so the last ring is 10 cm under the flat ridge and the contour is clean) plus a seeded vertical + radial jitter on the inner rings, so the
    top is a pile of tilted facets instead of a clean disc. Faces with a (triangle) normal z > 0.953 (the lawn rule's 0.95 + a margin) are LK_ROCK (the ONE_GREEN lawn rule), a few more tilted faces of the
    upper rings are LK_ROCK too (an irregular outcrop edge); the flanks are LK_SCRUB (warped planar UV like the terrain, so the olive matches)."""
    rg = np.random.default_rng(seed)
    nseg = 16
    us = [0.22, 0.42, 0.62, 0.80, 0.92, 1.0]
    ph = rg.uniform(0, math.tau, 4)
    t = np.arange(nseg) * math.tau / nseg + rg.uniform(0, 0.45)
    rf = 1 + 0.15 * np.sin(2 * t + ph[0]) + 0.09 * np.sin(3 * t + ph[1])
    hm = 1 + 0.20 * np.sin(3 * t + ph[2]) + 0.10 * np.sin(5 * t + ph[3])
    along = np.asarray(tangent, float)
    along /= max(float(np.linalg.norm(along)), 1e-12)
    across = np.array([-along[1], along[0]])
    P2 = [(cx, cy)]
    Z = []
    ring_idx = []
    for k, u in enumerate(us):
        idx = []
        jit_r = 0.06 if k < 3 else 0.0
        for j in range(nseg):
            rr = R * u * rf[j] * (1.0 + rg.uniform(-jit_r, jit_r))
            xy = np.array([cx, cy]) + rr * (math.cos(t[j]) * along + KNOLL_CROSS_RATIO * math.sin(t[j]) * across)
            x, y = float(xy[0]), float(xy[1])
            zr = H * hm[j] * (1.0 - u ** 1.1) - 0.10 + (rg.uniform(-0.07, 0.07) * H if k < 4 else 0.0)
            idx.append(len(P2))
            P2.append((x, y))
            Z.append(zr)
        ring_idx.append(idx)
    g = ground(np.array([p[0] for p in P2]), np.array([p[1] for p in P2]))
    if not np.isfinite(g).all():
        return None
    zr_apex = H * float(hm.mean()) - 0.10 + rg.uniform(-0.05, 0.05) * H
    verts = [(P2[0][0], P2[0][1], float(g[0]) + zr_apex)] + [(P2[i + 1][0], P2[i + 1][1], float(g[i + 1]) + Z[i]) for i in range(len(Z))]
    faces, face_ring = [], []
    for j in range(nseg):
        j2 = (j + 1) % nseg
        faces.append((0, ring_idx[0][j], ring_idx[0][j2]))
        face_ring.append(0)
    for k in range(len(us) - 1):
        for j in range(nseg):
            j2 = (j + 1) % nseg
            faces.append((ring_idx[k][j], ring_idx[k + 1][j], ring_idx[k + 1][j2], ring_idx[k][j2]))
            face_ring.append(k + 1)
    mscrub, mrock = L.look_material("LK_SCRUB", D), L.look_material("LK_ROCK", D)
    t_s, t_r = L.LOOK["LK_SCRUB"][2], L.LOOK["LK_ROCK"][2]
    midx, uvs = [], []
    for f, fr in zip(faces, face_ring):
        p = np.array([verts[i] for i in f])
        nz = []                                                  # the face normal as the polygon (Newell) and as BOTH triangulations of a quad see it: the verifier judges triangles
        for tri in ([(0, 1, 2)] if len(f) == 3 else [(0, 1, 2), (0, 2, 3), (1, 2, 3), (0, 1, 3)]):
            q = np.cross(p[tri[1]] - p[tri[0]], p[tri[2]] - p[tri[0]])
            nz.append(float(q[2] / max(np.linalg.norm(q), 1e-12)))
        rock = fr == 0 or max(nz) > 0.953                         # a genuine bare-rock crest; every gentle patch remains rock for the original lawn rule
        if not rock and fr <= 2 and max(nz) > 0.80 and rg.random() < (0.12, 0.06, 0.0)[fr]:
            rock = True                                          # a few more tilted facets near the top: an irregular outcrop edge, not a clean circle
        midx.append(1 if rock else 0)
        if rock:
            uvs.append(_rock_facet_uv(p, t_r))                  # real metre density along the steep facet, rather than its foreshortened XY footprint
        else:
            xw, yw = L._warp(p[:, 0], p[:, 1], *L.UV_WARP["LK_SCRUB"])
            uvs.append([(float(a / t_s), float(b / t_s)) for a, b in zip(xw, yw)])
    ob = L._new_piece(D, name, verts, faces, uvs, midx, [mscrub, mrock], col="ENVIRONMENT", sharp_deg=45.0)
    return ob


def _rock_facet_uv(points, tile):
    """World-sized UVs in each facet's best-fit plane, no geometry change."""
    q = np.asarray(points, float) - points[0]
    _u, _s, axes = np.linalg.svd(q, full_matrices=False)
    uv = np.stack([q @ axes[0], q @ axes[1]], 1) / tile
    uv += np.asarray(points[0][:2], float) / tile
    return [tuple(map(float, row)) for row in uv]


def repair_knoll_rock_uvs(D, objects=None):
    """Apply the native builder's facet mapping to saved checkpoint knolls."""
    objects = objects if objects is not None else [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith('DRESS_KNOLL')]
    count = 0
    for ob in objects:
        me = ob.data
        W = L._mesh_arrays(ob)[0]
        layer = me.uv_layers.active
        for face in me.polygons:
            if me.materials[face.material_index].name != 'LK_ROCK':
                continue
            mapped = _rock_facet_uv(W[list(face.vertices)], L.LOOK['LK_ROCK'][2])
            for li, uv in zip(face.loop_indices, mapped):
                layer.data[li].uv = uv
            count += 1
        me.update()
    return count


def repair_firstcut_uvs(D):
    """Unwarped metre-density planar UVs on the frozen flat rough collar."""
    ob = bpy.data.objects['FAIRWAY_FIRSTCUT']
    me = ob.data
    W = L._mesh_arrays(ob)[0]
    layer = me.uv_layers.active
    tile = L.LOOK['LK_ROUGH'][2]
    count = 0
    for face in me.polygons:
        if face.normal.z <= .5:
            continue
        for li in face.loop_indices:
            point = W[me.loops[li].vertex_index]
            layer.data[li].uv = (float(point[0] / tile), float(point[1] / tile))
        count += 1
    me.update()
    return count


def build_knolls(D, avoid, log=print):
    """Taller narrow scrub ridges (DRESS_KNOLL_nn: authoredH/R .72-.96) on the frozen ridge top, only where the whole footprint stays on the scrub, >=5 yd from the
    dry safe route (like every scenery rock), >= 6 m + off the channel lip, inside the cliff, clear of the ruin / crags / each other. They register their
    footprint so no plant or rock is scattered through them."""
    route = route_m(D)
    chan_lip = stretch_poly_m(D, "channel_wall_ridge")
    spine = spine_m(D)
    in_scrub = mask_in_poly_yd(scrub_polygon_yd(D))
    ridge_side = mask_ridge_side(D)
    ground = PL.ground_sampler(D)
    reg = PL.region_polyline(spine, 24.0)
    rng = random.Random(SEEDS["knoll"])
    X, Y, _ = reg(rng, 24000)
    ok = in_scrub(X, Y) & ridge_side(X, Y)
    X, Y = X[ok], Y[ok]
    sd = P.signed_dist_m(D, X, Y)
    dr = dist_to_poly(route, X, Y)
    dl = dist_to_poly(chan_lip, X, Y)
    lie = P.lie_codes_m(D, X, Y)
    chosen, objs = [], []
    r_opts = [KNOLL_R_M[1], 0.5 * (KNOLL_R_M[1] + KNOLL_R_M[0]) + 0.6, 0.5 * (KNOLL_R_M[1] + KNOLL_R_M[0]), KNOLL_R_M[0] + 0.5, KNOLL_R_M[0]]
    for i in range(len(X)):
        if len(chosen) >= KNOLLS:
            break
        if lie[i] == P.LIE_WATER:
            continue
        x, y = float(X[i]), float(Y[i])
        for R0 in r_opts:                                        # the largest hummock that fits here
            R = R0 * rng.uniform(0.94, 1.06)
            Rv = 1.26 * R                                         # the plan wobble reaches 1.24 R: every vertex of the mesh keeps the clearances
            if sd[i] > -(Rv + 0.8) or dr[i] < KNOLL_ROUTE_CLEAR_YD * YD + Rv or dl[i] < 5.0 + 0.7 * R:
                continue
            if any(math.hypot(x - ax, y - ay) < ar + Rv + 1.5 for ax, ay, ar in avoid):
                continue
            if any(math.hypot(x - cx, y - cy) < 1.25 * (Rv + 1.26 * cr) for cx, cy, cr, _h in chosen):
                continue
            break
        else:
            continue
        H = R * rng.uniform(*KNOLL_H_RATIO)
        # Actual generated footprint narrows across the local ridge, without changing the long-axis radius that the safety checks reserve.
        sk = int(np.argmin(np.linalg.norm(0.5 * (spine[:-1] + spine[1:]) - np.array([x, y]), axis=1)))
        tangent = spine[sk + 1] - spine[sk]
        ob = _knoll_mesh(D, L._next_name(D, "DRESS_KNOLL"), x, y, R, H, SEEDS["knoll"] * 100 + len(chosen), ground, tangent=tangent)
        if ob is None:
            continue
        ob["h9_class"] = "knoll"
        ob["knoll_R"], ob["knoll_H"] = float(R), float(H)
        ob["knoll_cross_ratio"] = KNOLL_CROSS_RATIO
        chosen.append((x, y, R, H))
        objs.append(ob)
        _register(D, x, y, 0.95 * R)
    log(f"[hole09] ridge knolls: {len(objs)} of {KNOLLS} requested, R {min([c[2] for c in chosen] or [0]):.1f}..{max([c[2] for c in chosen] or [0]):.1f} m, H {min([c[3] for c in chosen] or [0]):.1f}..{max([c[3] for c in chosen] or [0]):.1f} m")
    return objs, chosen


def build_split_cliff(D):
    """Four unequal eroded beds around the entire frozen Split boundary.

    The long (~2km) boundary uses the shared Goal15 strata geometry with
    four vertical beds rather than Needle's five. This is actual scenery simplification,
    preserving jagged short headlands, deep ledges and undercuts while
    leaving room under the unchanged250k triangle cap for heather/ruin.
    Split's scrub reference does not require the automatic vine curtain.
    """
    tuning = dict(L.STRATA_TUNING, goal15=True, closure=.55,
                  outline_block_min=1.8, outline_block_max=3.6,
                  outline_relief_m=.42, overhang_m=.7, undercut_m=.55)
    kwargs = dict(style="strata", seed=9, step_m=2.4,
                  bands=4, disp_m=(.3,1.5), jag_m=2.3,
                  max_out_m=2.6, piece_m=25,
                  material="LK_CLIFF", material_low="LK_CLIFF_DARK")
    made = L.build_cliff_skin(D, **kwargs, strata_tuning=tuning)
    # Supply the actual unchanged generator parameters so the existing
    # stagger gate can build its original regular-grid negative control.
    D.strata_kwargs = kwargs
    return made


def merge_static_scenery(D, scenery):
    """Coherent ruin/arch batches with shared-material submeshes.

    All plant placement/root work has finished before this call. Each whole
    disconnected stone keeps its original geometry, UVs and flat normals;
    the arch stays independently testable. Animated plants keep their
    original shared meshes, local origins and sway colours.
    """
    old = scenery['ruin']
    merged, reports = [], []
    groups = {category: [o for o in old if o.name.startswith(category)]
              for category in ('DRESS_RUIN', 'DRESS_ARCH')}
    for category in ('DRESS_RUIN', 'DRESS_ARCH'):
        sources = groups[category]
        result = HM.merge_static_masonry(D, sources, max_faces=3000, max_span_m=12.0)
        merged.extend(result['objects'])
        reports.append(result['stats'])
    scenery['ruin'] = merged
    scenery['ruin_batches'] = reports
    return reports


# ----------------------------------------------------------------------------------------------- scenery
def _register(D, x, y, r):
    """Reserve a footprint so later scatter calls keep clear of a prop that was placed by hand (place() does not register)."""
    PL._occ(D).append((float(x), float(y), float(r)))


def build_scenery(D, log=print):
    """Rocks, sea stacks, ruin + arch, plants. Returns a dict of the object lists (for the QA gates)."""
    S = D.SCENERY
    pz = D.play_z
    route = route_m(D)
    spine = spine_m(D)
    scrub_poly = scrub_polygon_yd(D)
    in_scrub = mask_in_poly_yd(scrub_poly)
    ridge_side = mask_ridge_side(D)
    off_ridge = mask_not_ridge_side(D)
    route_tuft = mask_far(route, TUFT_ROUTE_CLEAR_M)
    chan_lip = stretch_poly_m(D, "channel_wall_ridge")
    far_route = lambda r: mask_far(route, ROUTE_CLEAR_YD * YD + r + 0.1)         # noqa: E731  (the scatter masks test the centre, so add the prop's radius)
    far_lip = lambda r: mask_far(chan_lip, CHANNEL_LIP_CLEAR_M + r + 0.1)         # noqa: E731
    route_rock, chan_lip_far = far_route(2.6), far_lip(2.6)                       # loose rocks: footprint radius <= 2.6 m
    route_shrub = far_route(2.0)
    out = {}

    def band(lo, hi, side="land"):
        return PL.region_band(D, lo, hi, side)

    def at(x, y):
        return np.array([x]), np.array([y])

    # ---- 1. sea stacks (design positions; the legacy ROCK_STACK_nn columns are gone)
    stacks, stack_avoid = [], []
    for i, s in enumerate(S["sea_stacks"]):
        x, y = P.m(s["x"], s["d"])
        stacks += PL.place_sea_stack(D, x, y, float(s["height_m"]), s["radius_yd"] * YD, seed=SEEDS["stacks"] + i)
        stack_avoid.append((x, y, s["radius_yd"] * YD + 7.0))
    out["stacks"] = stacks
    log(f"[hole09] sea stacks: {len(S['sea_stacks'])} clusters, {len(stacks)} objects")

    # ---- 2. ruin (repair round 1, 2026-10-04): irregular rubble masonry (hole09_masonry: hulls of chipped stones, buried first course, support-checked courses, a lintel bearing on
    #         both jambs, a broken voussoir arch with its crown stones on the ground and a rubble heap in the gap). The old PL.build_ruin / PL.build_masonry_arch boxes are gone.
    R = S["ruin"]
    walls = R["walls"]
    on_land = lambda x, y: (P._on_land_margin(D, x, y, 1.0)          # noqa: E731
                            and float(P.signed_dist_m(D, np.array([x]), np.array([y]))[0]) <= -RUIN_CLIFF_CLEAR_YD * YD)   # centre test for the DRESS_BLOCK instances (fallen blocks / rubble)
    ruin_clip = lambda X, Y: (P.signed_dist_m(D, X, Y) <= -RUIN_VERTEX_SHORE_M) & (dist_to_poly(route, X, Y) >= RUIN_VERTEX_ROUTE_M)    # noqa: E731  every VERTEX of every masonry stone
    ground_fn = PL.ground_sampler(D)
    ruin_objs, fallen = [], []
    rc = np.array([P.m(sum(w[k][0] for w in walls for k in ("p0", "p1")) / 8.0, sum(w[k][1] for w in walls for k in ("p0", "p1")) / 8.0)])[0]
    shift = np.array(P.m(*RUIN_SHIFT_YD))
    big = lambda p: tuple(rc + (np.array(P.m(*p)) - rc) * RUIN_SCALE + shift)       # noqa: E731  the ruin is scaled up about its centre: bigger masonry reads on a phone
    path_w = [big(walls[0]["p1"]), big(walls[0]["p0"]), big(walls[1]["p1"]), big(walls[2]["p1"])]    # one polyline: south stub -> west wall -> the tall wall with the doorway (design walls 0, 1, 2)
    path_e = [big(walls[3]["p0"]), big(walls[3]["p1"])]                                              # the east stub (design wall 3)
    G0 = float(ground_fn(np.array([rc[0]]), np.array([rc[1]]))[0])
    ss = HM.StoneSet(D, "DRESS_RUIN", SEEDS["ruin_s"], clip=ruin_clip)
    door_len = float(np.hypot(*(np.array(path_w[3]) - np.array(path_w[2]))))
    wst = HM.build_wall_path(ss, path_w, [RUIN_H[1], RUIN_H[1], RUIN_H[2]], RUIN_T, (2, 0.5 * door_len, RUIN_DOOR_W, RUIN_DOOR_H), SEEDS["ruin_w"], ground_fn, G0)
    est = HM.build_wall_path(ss, path_e, [RUIN_H[3]], RUIN_T, None, SEEDS["ruin_e"], ground_fn, G0)
    dpt = np.array(wst["door_world"])
    dd = np.array(path_w[3]) - np.array(path_w[2])
    dd /= np.hypot(*dd)
    dn = np.array([-dd[1], dd[0]])

    def door_keep_out(x, y):                                    # no rubble inside the doorway corridor (the QA rays run through the wall at the doorway)
        v = np.array([x, y]) - dpt
        return abs(float(v @ dd)) < RUIN_DOOR_W / 2 + 1.2 and abs(float(v @ dn)) < 3.6
    n_rub_m = 0
    for k_, pts_ in enumerate((path_w, path_e)):
        n_rub_m += HM.scatter_rubble(ss, pts_, RUIN_T, ground_fn, door_keep_out, SEEDS["rubble"] + k_)
    ss.flush()
    ruin_objs += ss.objs
    A = R["fallen_arch"]
    ax, ay = P.m(A["x"] + ARCH_SHIFT_YD[0] + RUIN_SHIFT_YD[0], A["d"] + ARCH_SHIFT_YD[1] + RUIN_SHIFT_YD[1])
    sa = HM.StoneSet(D, "DRESS_ARCH", SEEDS["arch"], clip=ruin_clip)
    arch_info = HM.build_arch(sa, (ax, ay), ARCH_FACING_DEG, ground_fn, G0, r=ARCH_R, t=ARCH_T, depth=ARCH_DEPTH, pier_w=ARCH_PIER_W, spring=ARCH_SPRING,
                              left_top=ARCH_H, right_top=ARCH_H_RIGHT, seed=SEEDS["arch"], missing=(), slip=(6,))
    sa.flush()
    ruin_objs += sa.objs
    rng = random.Random(SEEDS["rubble"])
    rub = []
    for pile in R["rubble"]:
        px, py = P.m(pile["x"] + RUIN_SHIFT_YD[0], pile["d"] + RUIN_SHIFT_YD[1])
        rr = pile["radius_yd"] * YD
        for _ in range(int(2 + rr * 1.0)):
            a, d = rng.uniform(0, math.tau), rr * math.sqrt(rng.random())
            x, y = px + math.cos(a) * d, py + math.sin(a) * d
            if not on_land(x, y) or door_keep_out(x, y):
                continue
            k = rng.choice(("DRESS_BLOCK_A", "DRESS_BLOCK_B"))              # rubble = fallen cut blocks (masonry class: 6 yd off the route, not loose rocks)
            z = pz - 0.1
            rub.append(PL.place(D, k, (x, y, z), rng.uniform(0, math.tau), rng.uniform(0.7, 1.25),
                                (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25))))
    for i_ in range(4):                                                     # fallen DRESS_BLOCK stones at the foot of the arch piers
        a_ = rng.uniform(0, math.tau)
        x, y = ax + math.cos(a_) * rng.uniform(4.2, 5.6), ay + math.sin(a_) * rng.uniform(4.2, 5.6)
        if on_land(x, y) and ruin_clip(np.array([x]), np.array([y]))[0]:
            rub.append(PL.place(D, rng.choice(("DRESS_BLOCK_A", "DRESS_BLOCK_B")), (x, y, pz - 0.1), rng.uniform(0, math.tau), rng.uniform(0.8, 1.1),
                                (rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25))))
    out["ruin"] = ruin_objs
    out["ruin_paths"] = (path_w, path_e, RUIN_T, (ax, ay))
    out["ruin_fallen"] = fallen + rub
    out["ruin_stats"] = dict(walls=wst, east=est, arch=arch_info, blocks=ss.n_blocks + sa.n_blocks, refused=ss.n_refused + sa.n_refused, rubble_stones=n_rub_m,
                             records=ss.records + sa.records)
    for o in fallen + rub:                                   # fallen blocks / rubble reserve their footprint: no plant grows through them
        _register(D, o.location.x, o.location.y, 1.3)
    # footprints of the ruin (3 m round every 2 m of wall, 7 m round the arch): later scatter keeps off the masonry
    ruin_avoid = []
    for w in walls:
        a, b = np.array(big(w["p0"])), np.array(big(w["p1"]))
        n = max(2, int(np.hypot(*(b - a)) / 2.0))
        for u in np.linspace(0, 1, n + 1):
            p = a + (b - a) * u
            ruin_avoid.append((float(p[0]), float(p[1]), 3.0))
    ruin_avoid.append((ax, ay, 7.5))
    out["ruin_avoid"] = ruin_avoid
    log(f"[hole09] ruin walls: {wst['blocks']} stones, lintel stones {wst['lintels']}, {wst['joints']} joints of which {wst['tight']} within 12 cm of a joint of the course below; east stub {est['blocks']} stones; door jambs {wst.get('jamb')}; blocks per course per segment {wst.get('per_course')}")
    if ss.refused or sa.refused:
        log(f"[hole09] ruin clip refused: walls {[(round(x / YD, 1), round(y / YD, 1)) for x, y in ss.refused]} arch {[(round(x / YD, 1), round(y / YD, 1)) for x, y in sa.refused]} (course yards)")
    log(f"[hole09] ruin: {len(ruin_objs)} masonry meshes, {ss.n_blocks + sa.n_blocks} irregular stones ({ss.n_refused + sa.n_refused} refused by the clip), arch {arch_info['height']:.1f} m with "
        f"{arch_info['missing']} crown wedges on the ground (the keystone is slipped, not missing), {n_rub_m} wall-foot rubble stones, {len(rub)} DRESS_BLOCK rubble")

    # ---- 3. wet rocks: foot rocks, reef rocks (tall enough to break the swell: build_sea puffs the whitewater at them), the gap faces, sea rocks
    foot = PL.scatter(D, ["ROCK_BOULDER_B", "ROCK_BLOCK_A", "ROCK_SLAB_A", "ROCK_CAIRN_A", "ROCK_BOULDER_A"], 44, region=band(2.9, 7.0, "sea"),
                      seed=SEEDS["foot"], min_gap=4.0, scale_range=(0.8, 1.6), z=(-0.45, -0.05), wet=True, surface="sea", avoid=stack_avoid)
    reef = PL.scatter(D, ["ROCK_BOULDER_A", "ROCK_BLOCK_A", "ROCK_BOULDER_B"], 10, region=band(3.6, 11.0, "sea"), seed=SEEDS["reef"],
                      min_gap=14.0, scale_range=(1.25, 1.9), z=(-0.15, 0.25), sink=0.0, wet=True, surface="sea", avoid=stack_avoid)
    gap_c = P.m(S["gap"]["x"], S["gap"]["d"])
    gap = PL.scatter(D, ["ROCK_BOULDER_A", "ROCK_BLOCK_A", "ROCK_SLAB_A", "ROCK_BOULDER_B"], 12, region=PL.region_disc(gap_c[0], gap_c[1], 60.0),
                     seed=SEEDS["gap"], min_gap=5.0, scale_range=(1.1, 1.9), z=(-0.5, -0.1), wet=True, surface="sea",
                     masks=[PL.mask_sea(D, 2.9, 14.0)], avoid=stack_avoid)
    sea = PL.scatter(D, ["ROCK_BOULDER_A", "ROCK_SLAB_A", "ROCK_LEDGE_A", "ROCK_BLOCK_A", "ROCK_BOULDER_B"], 14, region=band(12.0, 26.0, "sea"),
                     seed=SEEDS["sea"], min_gap=9.0, scale_range=(1.0, 2.0), z=(-0.6, -0.1), wet=True, surface="sea", avoid=stack_avoid)
    out["foot"], out["reef"], out["gap"], out["sea"] = foot, reef, gap, sea
    log(f"[hole09] wet rocks: foot {len(foot)}, reef {len(reef)}, gap {len(gap)}, sea {len(sea)}")

    # ---- 4. ridge outcrops (big crag + boulders: the relief the flat top cannot have) and rim rocks, 8 yd off the safe route, never on the channel lip
    outcrop = []
    rrng = random.Random(SEEDS["outcrop"])
    cand = []
    for _ in range(9000):
        t = rrng.uniform(0.04, 0.98)
        k = min(int(t * (len(spine) - 1)), len(spine) - 2)
        u = t * (len(spine) - 1) - k
        c = spine[k] * (1 - u) + spine[k + 1] * u
        cand.append((c[0] + rrng.uniform(-24.0, 8.0), c[1] + rrng.uniform(-4.0, 4.0)))
    cand = np.array(cand)
    ok = in_scrub(cand[:, 0], cand[:, 1]) & ridge_side(cand[:, 0], cand[:, 1]) & (P.signed_dist_m(D, cand[:, 0], cand[:, 1]) <= -3.8)
    ok &= P.lie_codes_m(D, cand[:, 0], cand[:, 1]) != P.LIE_WATER
    cand = cand[ok]
    dr_all = dist_to_poly(route, cand[:, 0], cand[:, 1])
    dl_all = dist_to_poly(chan_lip, cand[:, 0], cand[:, 1])
    chosen = []                                             # (x, y, kind, scale, footprint radius)
    for (x, y), dr, dl in zip(cand, dr_all, dl_all):
        kind = ("ROCK_CRAG_B", "ROCK_CRAG_A")[len(chosen) % 2]
        mn, mx = PL.lib_bbox(kind)
        for sc in (1.25, 1.05, 0.88, 0.72, 0.6):            # the largest crag that fits between the route, the lip and the cliff
            rad = 0.5 * math.hypot(mx.x - mn.x, mx.y - mn.y) * sc + 0.3          # half diagonal: no vertex can be farther from the origin in plan
            if dr >= ROUTE_CLEAR_YD * YD + rad and dl >= CHANNEL_LIP_CLEAR_M + rad:
                break
        else:
            continue
        if any(math.hypot(x - rx, y - ry) < rr_ + rad + 5.0 for rx, ry, rr_ in ruin_avoid):
            continue
        if all(math.hypot(x - c[0], y - c[1]) >= OUTCROP_SPACING_M + 0.5 * (rad + c[4]) for c in chosen):
            chosen.append((x, y, kind, sc, rad))
        if len(chosen) >= OUTCROP_GROUPS:
            break
    ground = PL.ground_sampler(D)
    for gi, (cx, cy, kind, sc, rad) in enumerate(chosen):
        z0 = float(ground(*at(cx, cy))[0])
        outcrop.append(PL.place(D, kind, (cx, cy, z0 - 0.35), rrng.uniform(0, math.tau), sc, (rrng.uniform(-0.04, 0.04), rrng.uniform(-0.04, 0.04))))
        _register(D, cx, cy, 0.8 * rad)
        for j in range(rrng.randint(2, 4)):
            a = rrng.uniform(0, math.tau)
            dd = rad + rrng.uniform(1.2, 3.0)
            x, y = cx + math.cos(a) * dd, cy + math.sin(a) * dd
            if not (in_scrub(*at(x, y))[0] and route_rock(*at(x, y))[0] and chan_lip_far(*at(x, y))[0]
                    and P.lie_codes_m(D, *at(x, y))[0] != P.LIE_WATER and P.signed_dist_m(D, *at(x, y))[0] <= -2.0):
                continue
            z1 = float(ground(*at(x, y))[0])
            k2 = rrng.choice(("ROCK_BOULDER_A", "ROCK_BOULDER_B", "ROCK_BLOCK_A", "ROCK_SLAB_A", "ROCK_CAIRN_A", "ROCK_LEDGE_A"))
            s2 = rrng.uniform(0.8, 1.3)
            outcrop.append(PL.place(D, k2, (x, y, z1 - 0.12), rrng.uniform(0, math.tau), s2, (rrng.uniform(-0.1, 0.1), rrng.uniform(-0.1, 0.1))))
            _register(D, x, y, 1.6 * s2)
    # ---- 4b. round 2: low hummocks (DRESS_KNOLL) on the flat ridge, then the shrub / rock chain over the scrub boundary BEFORE the loose rocks and the plants take the room
    crag_discs = [(cx, cy, rad) for (cx, cy, kind_, sc_, rad) in chosen]
    knoll_objs, knoll_specs = build_knolls(D, ruin_avoid + crag_discs, log=log)
    out["knolls"], out["knoll_specs"] = knoll_objs, knoll_specs
    out["cover"] = cover_scrub_boundary(D, avoid=ruin_avoid, log=log)
    rim = PL.scatter(D, ["ROCK_BOULDER_B", "ROCK_STONE_A", "ROCK_STONE_B", "ROCK_BLOCK_A", "ROCK_CAIRN_A", "ROCK_BOULDER_A"], 56, region=band(0.9, 9.5),
                     seed=SEEDS["rim"], min_gap=3.4, scale_range=(0.7, 1.5), masks=[route_rock, chan_lip_far], avoid=ruin_avoid)
    stones = PL.scatter(D, {"ROCK_STONE_A": 3, "ROCK_STONE_B": 3, "ROCK_BOULDER_B": 1}, N_STONES, region=PL.region_polyline(spine, 26.0),
                        masks=[in_scrub, ridge_side, far_route(1.3), far_lip(1.3)], seed=SEEDS["stones"], min_gap=2.4, scale_range=(0.8, 1.5), avoid=ruin_avoid)
    out["outcrop"], out["rim"], out["stones"] = outcrop, rim, stones
    log(f"[hole09] ridge outcrops: {len(chosen)} groups, {len(outcrop)} objects; rim rocks {len(rim)}; ridge stones {len(stones)}")
    out["outcrop_groups"] = chosen

    # ---- 5. plants. big things first (registered footprints), tufts last
    ag = PL.scatter(D, {"PLANT_AGAVE_A": 1}, N_AGAVE, region=PL.region_polyline(spine, 20.0), masks=[in_scrub, ridge_side, route_shrub],
                    seed=SEEDS["agave"], min_gap=6.0, scale_range=(0.9, 1.3), shore_margin_m=1.5, avoid=ruin_avoid)
    # thickets: shrubs grow round ~SHRUB_CLUSTERS centres (beside the crag groups, then free scrub), not as evenly spread dots (split.jpg: dark green patches on the olive)
    crng = random.Random(SEEDS["cluster"])
    cl_cen = []

    def cl_ok(x, y):
        X, Y = at(x, y)
        return (in_scrub(X, Y)[0] and ridge_side(X, Y)[0] and route_shrub(X, Y)[0] and P.signed_dist_m(D, X, Y)[0] <= -3.5 and P.lie_codes_m(D, X, Y)[0] != P.LIE_WATER
                and all(math.hypot(x - ax, y - ay) >= ar + 1.5 for ax, ay, ar in ruin_avoid)
                and all(math.hypot(x - kx, y - ky) >= kr + 1.5 for kx, ky, kr, _kh in knoll_specs)
                and all(math.hypot(x - cx2, y - cy2) >= SHRUB_CLUSTER_SPACING_M for cx2, cy2 in cl_cen))
    for gi, (cx, cy, kind_, sc_, rad) in enumerate(chosen):                 # beside every second crag group (a thicket in its lee)
        if gi % 2:
            continue
        for _ in range(12):
            a = crng.uniform(0, math.tau)
            x, y = cx + math.cos(a) * (rad + 2.4), cy + math.sin(a) * (rad + 2.4)
            if cl_ok(x, y):
                cl_cen.append((x, y))
                break
    for _ in range(4000):
        if len(cl_cen) >= SHRUB_CLUSTERS:
            break
        X_, Y_, _z = PL.region_polyline(spine, 24.0)(crng, 1)
        if cl_ok(float(X_[0]), float(Y_[0])):
            cl_cen.append((float(X_[0]), float(Y_[0])))
    out["shrub_clusters"] = cl_cen
    shr = PL.scatter(D, {"PLANT_SHRUB_C": 2, "PLANT_SHRUB_A": 3}, N_SHRUB_RIDGE, region=region_clusters(np.array(cl_cen), SHRUB_CLUSTER_SIGMA_M), masks=[in_scrub, ridge_side, route_shrub],
                     seed=SEEDS["shrub_ridge"], min_gap=2.8, scale_range=(1.0, 1.75), shore_margin_m=1.5, avoid=ruin_avoid)
    shr2 = PL.scatter(D, {"PLANT_SHRUB_A": 2, "PLANT_SHRUB_B": 1}, N_SHRUB_OUT, region=band(1.2, 9.0), masks=[off_ridge], seed=SEEDS["shrub_out"],
                      min_gap=4.0, scale_range=(0.8, 1.35), shore_margin_m=1.5)
    fl = PL.scatter(D, {"PLANT_FLOWER_WHITE": 2, "PLANT_FLOWER_YELLOW": 2}, N_FLOWER_RIDGE, region=PL.region_polyline(spine, 18.0),
                    masks=[in_scrub, ridge_side, route_tuft], seed=SEEDS["flower"], min_gap=3.0, shore_margin_m=1.5, avoid=ruin_avoid)
    fl += PL.scatter(D, {"PLANT_FLOWER_PURPLE": 1, "PLANT_FLOWER_WHITE": 1}, N_FLOWER_OUT, region=band(1.5, 8.0), masks=[off_ridge], seed=SEEDS["flower"] + 1,
                     min_gap=4.0, shore_margin_m=1.5)
    sc = np.array([[o.location.x, o.location.y] for o in shr + ag]) if (shr or ag) else np.zeros((1, 2))
    tuft_a = PL.scatter(D, {"PLANT_TUFT_C": 5, "PLANT_TUFT_A": 1}, N_TUFT_CLUSTER, region=region_clusters(sc, 2.4), masks=[in_scrub, ridge_side, route_tuft],
                        seed=SEEDS["tuft_ridge"], min_gap=0.6, shore_margin_m=1.2, avoid=ruin_avoid)
    tuft_b = PL.scatter(D, {"PLANT_TUFT_C": 4, "PLANT_TUFT_A": 1}, N_TUFT_SPRINKLE, region=PL.region_polyline(spine, 24.0), masks=[in_scrub, ridge_side, route_tuft],
                        seed=SEEDS["tuft_ridge"] + 1, min_gap=1.4, shore_margin_m=1.2, avoid=ruin_avoid)
    tuft_o = PL.scatter(D, {"PLANT_TUFT_A": 3, "PLANT_TUFT_B": 2}, N_TUFT_OUT, region=band(0.5, 11.0), masks=[off_ridge], seed=SEEDS["tuft_out"],
                        min_gap=1.0, shore_margin_m=1.2)
    out["shrubs"], out["shrubs_out"], out["agave"], out["flowers"] = shr, shr2, ag, fl
    out["tufts"] = tuft_a + tuft_b + tuft_o
    log(f"[hole09] plants: shrubs {len(shr)} + {len(shr2)}, agave {len(ag)}, flowers {len(fl)}, tufts {len(out['tufts'])}")

    # ---- 6. GRASS PASS (2026-10-05, 'the near ground reads as a carpet'): the old 117-triangle tufts far from the cameras become 36-triangle fillers, then the EDGE FRINGE (PL.scatter_fringe: clumps of 3-7
    #         tall leaning tufts along the rough edge of the fairway / green / tee, the shore tops and cliff lips; ball zones / pin / markers / route kept clear by the library, big things registered first)
    out["grass"] = build_grass(D, out, in_scrub, route_tuft, log=log)
    return out


def build_grass(D, out, in_scrub, route_tuft, log=print):
    """Cheaper old tufts + the edge fringe, in ZONES (the stills cameras and the landmark): a fringe spread evenly over 2 km of island lip would put 90 % of the triangle budget where no camera
    looks, so each zone is a bbox (metres) with its own budget and the library's own density rules (clumps of 3-7, lean <= 12 deg, 0.6-1.8 x height, ball zones / pin / markers / route kept clear):
    the tee view (the ribbon from the tee box), the approach view (the ribbon either side of the second-shot ball), the ruin landmark (olive tufts on the scrub ridge) and a thin far-field sprinkle.
    Green tufts stand off the scrub polygon, olive / straw tufts inside it. Returns a dict for the gates."""
    ch = PL.cheapen_tufts(D, near_m=GRASS_CHEAP_NEAR_M)
    not_scrub = lambda X, Y: ~in_scrub(X, Y)                                                    # noqa: E731
    green = dict(tall={"PLANT_TUFT_D": 3, "PLANT_TUFT_E": 2}, fill={"PLANT_TUFT_S": 3})
    olive = dict(tall={"PLANT_TUFT_F": 3, "PLANT_TUFT_D": 1}, fill={"PLANT_TUFT_T": 3, "PLANT_TUFT_S": 1})
    zones = [   # name, bbox (x0, y0, x1, y1) m, kinds, mask, budget, focus point (m): where the cameras look (a camera sees the rough edge of the ribbon only from ~35 m ahead of the ball, so the tee zone starts at y 28 m)
        ("tee_view", (-45.0, 30.0, 45.0, 112.0), green, not_scrub, GRASS_ZONE_TRIS["tee_view"], (0.0, 75.0)),
        ("ribbon_mid", (-45.0, 112.0, 45.0, 285.0), green, not_scrub, GRASS_ZONE_TRIS["ribbon_mid"], (6.0, 200.0)),
        ("green_end", (-50.0, 325.0, 50.0, 470.0), green, not_scrub, GRASS_ZONE_TRIS["green_end"], (-5.0, 430.0)),
        ("ruin", (-100.0, 50.0, -15.0, 190.0), olive, in_scrub, GRASS_ZONE_TRIS["ruin"], (-58.8, 97.0)),
        ("far_green", None, green, not_scrub, GRASS_ZONE_TRIS["far_green"], None),
        ("far_olive", None, olive, in_scrub, GRASS_ZONE_TRIS["far_olive"], None),
    ]
    res = {}
    for zi, (name, box, kinds, mk, bud, foc) in enumerate(zones):
        f = PL.scatter_fringe(D, kinds=kinds, tri_budget=bud, seed=SEEDS["fringe_g"] + zi, masks=[route_tuft, mk], region=box, tall_share=0.25, focus=[foc] if foc else None, lip_margin_m=1.1)
        for o_ in f["objs"]:
            o_["h9_zone"] = name
        res[name] = f
        log(f"[hole09] grass fringe {name}: {f['count']} tufts, {f['tris']} tris, {f['clusters']} clumps + {f['singles']} singles, edge covered {f['edge_covered']}/{f['edge_samples']}, "
            f"by kind {f['by_kind']}, top max {f['top_max_m']:.2f} m")
    # the open scrub in front of the ruin landmark camera: olive clumps (3-7 fillers + a few tall tufts) on the ridge top, away from the ruin, the route and the lip. Candidate clump centres are the ground points
    # that PROJECT INTO the landmark frame (landmarks.json hole09_ruin: pos / look / fov 50, 900 x 1600), weighted 1 / depth: a field over the whole ridge would spend the triangles on ground nobody sees
    rng = random.Random(SEEDS["fringe_g"] + 77)
    route = route_m(D)
    land = PL.mask_real_land(D, 3.0)
    cam = Vector((-64.28 * YD, 145.96 * YD, 26.0 * YD))
    fwd = (Vector((-64.28 * YD, 115.96 * YD, 10.26 * YD)) - cam).normalized()
    rgt = fwd.cross(Vector((0, 0, 1))).normalized()
    upv = rgt.cross(fwd).normalized()
    th = math.tan(math.radians(25.0))
    asp = 900.0 / 1600.0

    def in_frame(x, y):
        v = Vector((x, y, D.play_z)) - cam
        zc = v.dot(fwd)
        if zc < 4.0:
            return False, zc
        return abs(v.dot(rgt) / zc / (th * asp)) < 0.92 and abs(v.dot(upv) / zc / th) < 0.95, zc
    avoid_f = list(out["ruin_avoid"][:-1]) + [(out["ruin_avoid"][-1][0], out["ruin_avoid"][-1][1], 5.4)]    # the arch's 7.5 m footprint circle -> 5.4 m: pier outer edge 3.7 m + the fallen blocks are registered footprints
    cen = []
    for _ in range(20000):
        if len(cen) >= GRASS_FIELD_CLUMPS:
            break
        near_strip = len(cen) < GRASS_FIELD_NEAR                                  # the first clumps stand in the strip between the arch and the camera (the lowest ground of the frame)
        x, y = (rng.uniform(-72.0, -46.0), rng.uniform(108.0, 124.0)) if near_strip else (rng.uniform(-100.0, -20.0), rng.uniform(60.0, 135.0))
        vis, depth = in_frame(x, y)
        if not vis or (not near_strip and rng.random() > min(1.0, 22.0 / depth)):
            continue
        X, Y = np.array([x]), np.array([y])
        if (in_scrub(X, Y)[0] and land(X, Y)[0] and float(dist_to_poly(route, X, Y)[0]) >= 5.0
                and all(math.hypot(x - ax_, y - ay_) >= ar_ + 1.2 for ax_, ay_, ar_ in avoid_f)
                and all(math.hypot(x - cx_, y - cy_) >= 2.6 for cx_, cy_ in cen)):
            cen.append((x, y))
    field = PL.scatter(D, {"PLANT_TUFT_T": 4, "PLANT_TUFT_S": 1, "PLANT_TUFT_F": 3}, GRASS_FIELD_N, region=region_clusters(np.array(cen), 0.5), masks=[in_scrub, route_tuft],
                       seed=SEEDS["fringe_g"] + 78, min_gap=0.3, scale_range=(1.1, 1.85), shore_margin_m=3.0, avoid=avoid_f)
    for o in field:
        o["h9_class"] = "field"
    res["ruin_field"] = dict(count=len(field), centres=len(cen), tris=sum(len(o.data.polygons) for o in field))
    cy_ = np.array([c[1] for c in cen]) if cen else np.zeros(1)
    log(f"[hole09] ruin scrub field: {len(field)} tufts in {len(cen)} clumps, clump y {cy_.min():.0f}..{cy_.max():.0f} m, x {min((c[0] for c in cen), default=0):.0f}..{max((c[0] for c in cen), default=0):.0f} m")
    log(f"[hole09] cheapen_tufts: {ch}")
    # the bushes round the ruin (the thicket at the landmark camera) get tufty olive / straw blades on their lumps ('the bushes are still smooth stacked balls'), the sea-stack crowns too
    ra = np.array([[x, y] for x, y, _r in out["ruin_avoid"]], float)
    c = ra.mean(0)
    allsh = list(out["shrubs"]) + list(out["shrubs_out"]) + list(out["cover"]["shrubs"])
    near = sorted(allsh, key=lambda o: math.hypot(o.location.x - c[0], o.location.y - c[1]))[:GRASS_TUFTIFY_SHRUBS]
    # wall-foot tufts: grass grows up the foot of the ruin walls (split.jpg: the ruin stands in vegetation). Clump centres every ~3.4 m along both faces of every wall path, 0.85 m off the face; every tuft stays
    # >= 0.6 m (plan) from any masonry vertex (the rubble stones lie 0.15-1.5 m off the wall), ground-rooted like every plant (scatter lands them on the ground top)
    path_w, path_e, T_, (arx, ary) = out["ruin_paths"]
    mas = [o for o in out["ruin"] if o.name.startswith(("DRESS_RUIN", "DRESS_ARCH"))]
    Vm = np.concatenate([L._mesh_arrays(o)[0][:, :2] for o in mas]) if mas else np.zeros((1, 2))
    Vm = np.unique(np.round(Vm, 2), axis=0)

    def far_masonry(X, Y, r=0.6):
        X, Y = np.atleast_1d(X), np.atleast_1d(Y)
        out_ = np.ones(len(X), bool)
        for i0 in range(0, len(X), 64):
            dd = np.hypot(X[i0:i0 + 64, None] - Vm[None, :, 0], Y[i0:i0 + 64, None] - Vm[None, :, 1])
            out_[i0:i0 + 64] = dd.min(axis=1) >= r
        return out_
    wcen = []
    for pts_ in (path_w, path_e):
        for k in range(len(pts_) - 1):
            a_, b_ = np.array(pts_[k], float), np.array(pts_[k + 1], float)
            ln_ = float(np.hypot(*(b_ - a_)))
            d_ = (b_ - a_) / ln_
            n_ = np.array([-d_[1], d_[0]])
            for j, s_ in enumerate(np.arange(0.6, ln_, 3.4)):
                side = 1.0 if (j + k) % 2 == 0 else -1.0
                p_ = a_ + d_ * s_ + n_ * side * (T_ / 2 + 0.85)
                wcen.append((float(p_[0]), float(p_[1])))
    wf = PL.scatter(D, {"PLANT_TUFT_F": 2, "PLANT_TUFT_T": 3, "PLANT_TUFT_S": 1}, GRASS_WALLFOOT_N, region=region_clusters(np.array(wcen), 0.5), masks=[in_scrub, route_tuft, far_masonry],
                    seed=SEEDS["fringe_g"] + 80, min_gap=0.28, scale_range=(1.0, 1.7), shore_margin_m=3.0)
    for o in wf:
        o["h9_class"] = "wallfoot"
    res["ruin_wallfoot"] = dict(count=len(wf), centres=len(wcen), tris=sum(len(o.data.polygons) for o in wf))
    log(f"[hole09] wall-foot tufts: {len(wf)} tufts round {len(wcen)} clump centres on the ruin walls")
    # NOT done: PL.tuftify_shrubs / tuftify_crowns (tufts standing ON the shrub lumps / the sea-stack crowns). Measured (r8/g1, g2): the hole's own H_NO_PLANT_ON_WATER wants every plant ROOTED on the ground
    # (|origin z - ground| <= 6 cm; a crown tuft has a WATER lie under its base) and the verifier's NO_PLANT_ON_WATER wants ground or a rock top within 0.5 m under the lowest blade (a shrub top is 0.7-0.9 m up):
    # both FAIL them, so the gates stay as they are. Instead each bush near the ruin gets a SKIRT: 3-5 olive / straw tufts rooted on the ground round its rim (the library scatter keeps them out of the footprints).
    sh_xy = np.array([[o.location.x, o.location.y] for o in near]) if near else np.zeros((1, 2))
    skirt = PL.scatter(D, {"PLANT_TUFT_T": 3, "PLANT_TUFT_F": 2, "PLANT_TUFT_S": 1}, 4 * len(near), region=region_clusters(sh_xy, 1.5), masks=[in_scrub, route_tuft],
                       seed=SEEDS["fringe_g"] + 79, min_gap=0.3, scale_range=(1.0, 1.7), shore_margin_m=1.5, avoid=out["ruin_avoid"])
    for o in skirt:
        o["h9_class"] = "skirt"
    ts = dict(objs=skirt, tris=sum(len(o.data.polygons) for o in skirt))
    log(f"[hole09] {len(near)} bushes round the ruin got a skirt of {len(skirt)} ground-rooted tufts ({ts['tris']} tris)")
    return dict(cheap=ch, zones=res, shrub_tufts=ts)


def scrub_boundary_edges(D):
    """End points (metres) of the TERRAIN top edges between an LK_SCRUB face and a non-scrub top face: [(ax, ay, bx, by)]."""
    ob = D.terrain or bpy.data.objects[D.terrain_name]
    me = ob.data
    names = [m.name if m else "" for m in me.materials]
    bm = bmesh.new()
    bm.from_mesh(me)
    out = []
    mw = ob.matrix_world
    for e in bm.edges:
        if len(e.link_faces) != 2:
            continue
        f0, f1 = e.link_faces
        if f0.normal.z < 0.5 or f1.normal.z < 0.5:
            continue
        if (names[f0.material_index] == "LK_SCRUB") != (names[f1.material_index] == "LK_SCRUB"):
            a, b = mw @ e.verts[0].co, mw @ e.verts[1].co
            out.append((a.x, a.y, b.x, b.y))
    bm.free()
    return out


def cover_scrub_boundary(D, avoid=(), log=print):
    """A chain of shrubs and rocks over the colour step where the olive LK_SCRUB meets the green rough (the tapered tips of the scrub polygon at the two ridge
    ends, where it follows the 6.5 m terrain triangles). One piece every COVER_STEP_M metres along every boundary edge, alternating shrub / rock, each tried
    at the sample and then at up to 14 jittered spots (+-2.2 m across, +-0.8 m along) until it passes: land with 1.5 m to the shore, off the play surfaces, 6 m off
    the pin, 1.5 m off the markers, clear of every registered footprint, the footprint off the channel lip (rocks) and COVER_ROUTE_CLEAR_YD (4.5 yd, the design's
    '>= 4 yd off the route' rule; the other scenery keeps 8 yd) off the dry safe route. Tufts are added over the same chain. Returns a dict for the gates."""
    edges = scrub_boundary_edges(D)
    mid = scrub_boundary_mid(D)
    if not edges:
        log("[hole09] scrub boundary: none")
        return dict(edges=0, length=0.0, shrubs=[], rocks=[], tufts=[], mid=mid, samples=0, unplaced=0)
    rnd = random.Random(SEEDS["cover_chain"])
    route = route_m(D)
    chan_lip = stretch_poly_m(D, "channel_wall_ridge")
    land = PL.mask_real_land(D, 1.5)
    off_play = PL.mask_off_play(D, 0.5)
    pin_clear = PL.mask_pin_clear(D, 6.0)
    markers = PL.mask_off_markers(1.5)
    ground = PL.ground_sampler(D)
    occ = PL._occ(D)
    shrubs, rocks, unplaced = [], [], 0
    k_shr = (("PLANT_SHRUB_C", 3), ("PLANT_SHRUB_A", 2), ("PLANT_SHRUB_B", 1))
    k_rock = ("ROCK_BOULDER_B", "ROCK_BLOCK_A", "ROCK_STONE_A", "ROCK_BOULDER_A", "ROCK_CAIRN_A", "ROCK_STONE_B")
    wl = [k for k, w in k_shr for _ in range(w)]
    samples = []
    for (ax, ay, bx, by) in edges:
        ln = math.hypot(bx - ax, by - ay)
        n = max(1, int(round(ln / COVER_STEP_M)))
        tx, ty = (bx - ax) / max(ln, 1e-9), (by - ay) / max(ln, 1e-9)
        for k in range(n):
            u = (k + 0.5) / n
            samples.append((ax + (bx - ax) * u, ay + (by - ay) * u, tx, ty, k))
    for si, (sx, sy, tx, ty, k) in enumerate(samples):
        shrub_first = (k + si) % 2 == 0
        placed = False
        for att in range(24):
            # the planned piece first (alternating shrub / rock), then, where the route buffer or a crowded spot refuses it, a small stone: it needs the least room
            small = att >= 8
            shrub = shrub_first and not small
            kind = rnd.choice(("ROCK_STONE_A", "ROCK_STONE_B")) if small else (rnd.choice(wl) if shrub else rnd.choice(k_rock))
            s = rnd.uniform(1.3, 1.7) if small else (rnd.uniform(1.1, 1.7) if shrub else rnd.uniform(1.0, 1.7))
            mn, mx = PL.lib_bbox(kind)
            foot = max(mx.x - mn.x, mx.y - mn.y) * 0.5 * s
            rad = max(0.8 if small else 1.0, foot * 0.55)
            rv = 0.5 * math.hypot(mx.x - mn.x, mx.y - mn.y) * s * 1.12 + 0.1        # farthest vertex from the origin in plan (scale jitter 1.1 included)
            if att % 8 == 0:
                x, y = sx, sy
            else:
                lat, al = rnd.uniform(-2.2, 2.2), rnd.uniform(-0.8, 0.8)
                x, y = sx + tx * al - ty * lat, sy + ty * al + tx * lat
            X, Y = np.array([x]), np.array([y])
            aa = np.linspace(0.0, math.tau, 8, endpoint=False)
            RX, RY = x + 0.75 * foot * np.cos(aa), y + 0.75 * foot * np.sin(aa)
            if not (land(X, Y)[0] and off_play(X, Y)[0] and pin_clear(X, Y)[0] and markers(X, Y)[0] and land(RX, RY).all() and off_play(RX, RY).all()):
                continue
            if (P.lie_codes_m(D, RX, RY) == P.LIE_WATER).any():
                continue
            if float(dist_to_poly(route, X, Y)[0]) < COVER_ROUTE_CLEAR_YD * YD + rv + 0.1:
                continue
            if not shrub and float(dist_to_poly(chan_lip, X, Y)[0]) < CHANNEL_LIP_CLEAR_M + rv:
                continue
            if any(math.hypot(x - ax_, y - ay_) < ar_ + rad + 0.3 for ax_, ay_, ar_ in avoid):
                continue
            if any(math.hypot(x - ox, y - oy) < max(orr + rad, 1.6 if small else 2.0) for ox, oy, orr in occ):
                continue
            z = float(ground(X, Y)[0])
            if not math.isfinite(z):
                continue
            if shrub:
                ob = PL.place(D, kind, (x, y, z - 0.03), rnd.uniform(0, math.tau), (s, s, s * rnd.uniform(0.92, 1.08)), (rnd.uniform(-0.05, 0.05), rnd.uniform(-0.05, 0.05)))
                shrubs.append(ob)
            else:
                hh = (mx.z - max(mn.z, 0.0)) * s
                ob = PL.place(D, kind, (x, y, z - 0.12 * hh), rnd.uniform(0, math.tau), (s * rnd.uniform(0.9, 1.1), s * rnd.uniform(0.9, 1.1), s * rnd.uniform(0.85, 1.1)),
                              (rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06)))
                rocks.append(ob)
            ob["h9_class"] = "cover"
            occ.append((x, y, rad))
            placed = True
            break
        unplaced += 0 if placed else 1
    # ---- gap fill: any 1 m boundary sample still without a shrub / rock within 3 m (and farther than 1.7 m from the route axis, where nothing may stand) gets the small stone
    #      that covers the most of them
    smp1, big_xy = [], [(o.location.x, o.location.y) for o in shrubs + rocks]
    for (ax, ay, bx, by) in edges:
        ln = math.hypot(bx - ax, by - ay)
        n = max(1, int(math.ceil(ln)))
        for k in range(n):
            u = (k + 0.5) / n
            smp1.append((ax + (bx - ax) * u, ay + (by - ay) * u))
    smp1 = np.array(smp1)
    other = [o for o in bpy.data.objects if o.type == 'MESH' and (o.name.startswith(("PLANT_SHRUB", "PLANT_AGAVE")) or (o.name.startswith("ROCK_") and not o.name.startswith(("ROCK_SKIN", "ROCK_SEASTACK", "ROCK_SPIRE")) and "_WET" not in o.name))
             and not o.hide_viewport and o.location.z > 4.0 and o.get("h9_class") != "cover"]
    big_xy += [(o.location.x, o.location.y) for o in other]
    bxy = np.array(big_xy) if big_xy else np.zeros((0, 2))
    n_fill = 0
    skip = np.zeros(len(smp1), bool)
    for _ in range(80):
        cnt = np.array([int((np.hypot(bxy[:, 0] - x, bxy[:, 1] - y) <= 3.0).sum()) if len(bxy) else 0 for x, y in smp1])
        dax = dist_to_poly(route, smp1[:, 0], smp1[:, 1])
        bad = np.nonzero((cnt == 0) & (dax >= 1.7) & ~skip)[0]
        if len(bad) == 0:
            break
        tx_, ty_ = smp1[bad[0]]
        best = None
        why = dict(land=0, route=0, lip=0, avoid=0, occ=0, z=0)
        for ox in np.arange(-2.95, 2.96, 0.25):
            for oy in np.arange(-2.95, 2.96, 0.25):
                x, y = float(tx_ + ox), float(ty_ + oy)
                if math.hypot(ox, oy) > 2.95:
                    continue
                kind = "ROCK_STONE_A" if (n_fill % 2 == 0) else "ROCK_STONE_B"
                s_ = 1.4
                mn, mx = PL.lib_bbox(kind)
                rv = 0.5 * math.hypot(mx.x - mn.x, mx.y - mn.y) * s_ * 1.12 + 0.1
                X, Y = np.array([x]), np.array([y])
                aa = np.linspace(0.0, math.tau, 8, endpoint=False)
                RX, RY = x + 0.6 * rv * np.cos(aa), y + 0.6 * rv * np.sin(aa)
                if not (land(X, Y)[0] and off_play(X, Y)[0] and pin_clear(X, Y)[0] and markers(X, Y)[0] and land(RX, RY).all() and off_play(RX, RY).all()):
                    why["land"] += 1
                    continue
                if (P.lie_codes_m(D, RX, RY) == P.LIE_WATER).any():
                    why["land"] += 1
                    continue
                if float(dist_to_poly(route, X, Y)[0]) < COVER_ROUTE_CLEAR_YD * YD + rv + 0.1:
                    why["route"] += 1
                    continue
                if float(dist_to_poly(chan_lip, X, Y)[0]) < CHANNEL_LIP_CLEAR_M + rv:
                    why["lip"] += 1
                    continue
                if any(math.hypot(x - ax_, y - ay_) < ar_ + 0.9 + 0.3 for ax_, ay_, ar_ in avoid):
                    why["avoid"] += 1
                    continue
                if any(math.hypot(x - ox_, y - oy_) < max(orr + 0.9, 1.6) for ox_, oy_, orr in occ):
                    why["occ"] += 1
                    continue
                z = float(ground(X, Y)[0])
                if not math.isfinite(z):
                    why["z"] += 1
                    continue
                gain = int(((np.hypot(smp1[bad, 0] - x, smp1[bad, 1] - y) <= 3.0)).sum())
                if best is None or gain > best[0]:
                    best = (gain, x, y, z, kind, s_, mx, mn)
        if best is None:
            log(f"[hole09] gap fill: nothing can stand within 3 m of the boundary point ({tx_:.1f}, {ty_:.1f}) (rejections {why})")
            skip[bad[0]] = True                                         # nothing can stand within reach: leave it (counted by the gate)
            continue
        gain, x, y, z, kind, s_, mx, mn = best
        hh = (mx.z - max(mn.z, 0.0)) * s_
        ob = PL.place(D, kind, (x, y, z - 0.12 * hh), rnd.uniform(0, math.tau), (s_ * rnd.uniform(0.9, 1.1), s_ * rnd.uniform(0.9, 1.1), s_ * rnd.uniform(0.85, 1.1)),
                      (rnd.uniform(-0.06, 0.06), rnd.uniform(-0.06, 0.06)))
        ob["h9_class"] = "cover"
        occ.append((x, y, 0.9))
        rocks.append(ob)
        bxy = np.vstack([bxy, [x, y]]) if len(bxy) else np.array([[x, y]])
        n_fill += 1
    cen = mid[:, :2]
    sc = np.array([[o.location.x, o.location.y] for o in shrubs]) if shrubs else cen
    tufts = PL.scatter(D, {"PLANT_TUFT_C": 2, "PLANT_TUFT_A": 2, "PLANT_TUFT_B": 1}, 40, region=region_clusters(np.vstack([cen, sc]), 1.7),
                       masks=[mask_far(route, TUFT_ROUTE_CLEAR_M)], seed=SEEDS["cover"] + 2, min_gap=0.8, shore_margin_m=1.2)
    for o in tufts:
        o["h9_class"] = "cover"
    log(f"[hole09] scrub boundary: {len(edges)} edges ({mid[:, 2].sum():.0f} m), {len(samples)} chain samples every {COVER_STEP_M} m -> {len(shrubs)} shrubs + {len(rocks)} rocks "
        f"(unplaced samples {unplaced}, {n_fill} gap-fill stones) + {len(tufts)} tufts; route clearance for the chain {COVER_ROUTE_CLEAR_YD} yd")
    return dict(edges=len(edges), length=float(mid[:, 2].sum()), shrubs=shrubs, rocks=rocks, tufts=tufts, mid=mid, samples=len(samples), unplaced=unplaced)


# P7 round2: opt-in genuine shared whole-tuft clumps.
def merge_shared_tuft_clumps(D, scn):
    """Real shared whole-tuft clumps; keep singles, cap120faces and support.

    Scenery positions/pivots may change. Actual source-copy tuple count, each
    source template's UV/Col, polygon total and scoring geometry remain intact.
    No hidden original objects or stored counts are used to satisfy a gate.
    Original unchanged full checks run after this opt-in build operation.
    """
    import collections
    import hashlib
    import postcard_look_verify as Q
    from mathutils import Matrix, Vector
    from mathutils.bvhtree import BVHTree
    design,dp=Q.load_design(9);C=Q.Ctx(9,design,dp,{})
    def snap():
        bpy.context.view_layer.update()
        return Q.Snap('blend',C)
    def _dist(poly,xy):
        return P._dist_pts_segs(np.asarray(xy,float).reshape(-1,2),poly[:-1],poly[1:])
    def _ground(objs):
        verts,faces=[],[]
        for ob in objs:
            if not ob.name.startswith(('TERRAIN','FAIRWAY','GREEN','TEE_BOX','BUNKER')):continue
            W,ls,lt,lv,mi,pn=L._mesh_arrays(ob);base=len(verts)
            verts.extend(map(tuple,W))
            for pi in range(len(ls)):
                if pn[pi,2]>.2:
                    first,n=int(ls[pi]),int(lt[pi])
                    faces.append([base+int(v) for v in lv[first:first+n]])
        return BVHTree.FromPolygons(verts,faces)
    S=snap();original=sorted((o for n,o in S.meshes.items() if n.startswith('PLANT_TUFT')),key=lambda o:o.name)
    plants_before=len(S.named('PLANT_'));tri_before=sum(S.loc(o)['ntri'] for o in S.meshes.values())
    ground=_ground(list(S.meshes.values()));route=route_m(D);shore=np.array([P.m(x,y) for x,y in D.SHORE]+[P.m(*D.SHORE[0])])
    frozen={n:dict(V=S.wld(o)['W'].copy(),T=S.wld(o)['T'].copy()) for n,o in S.meshes.items() if n.startswith(Q.COLL_PREFIXES)}
    source={o.name:dict(kind=o.data.name,V=S.wld(o)['W'].copy(),pivot=np.array(o.matrix_world.translation),uv=np.array([u.uv[:] for u in o.data.uv_layers.active.data]),rgba=Q.sway_colors(o.data)[0],faces=len(o.data.polygons),tri=S.loc(o)['ntri'],zone=str(o.get('h9_zone',o.get('h9_class','old'))),fringe=int(o.get('lk_fringe',0))) for o in original}
    # Respect original singles and original author cluster identities; no invisible
    # nodes remain. Field tufts without fringe IDs may form genuine nearby groups.
    keys=collections.defaultdict(list)
    for o in original:
     r=source[o.name];k=o.data.name
     if not k.startswith(('PLANT_TUFT_D','PLANT_TUFT_E','PLANT_TUFT_F','PLANT_TUFT_S','PLANT_TUFT_T')):continue
     keys[(r['zone'],r['fringe'])].append(o)
    candidate_groups=[];singles=0
    for key,items in sorted(keys.items()):
     if key[1]>0 and len(items)==1:singles+=1;continue
     pending=list(items)
     while pending:
      o=pending.pop(0);r=source[o.name];near=sorted((x for x in pending if np.linalg.norm(source[x.name]['pivot'][:2]-r['pivot'][:2])<=1.8),key=lambda x:np.linalg.norm(source[x.name]['pivot'][:2]-r['pivot'][:2]))
      fillers=[x for x in near if source[x.name]['faces']<=40]
      members=[o]
      if r['faces']<=40 and len(fillers)>=2:members+=fillers[:2]
      elif r['faces']>40 and fillers:members+=fillers[:1]
      elif r['faces']<=40:
       tall=[x for x in near if 40<source[x.name]['faces']<=84]
       if tall:members=[tall[0],o]
      if len(members)==1:continue
      for x in members:
       if x in pending:pending.remove(x)
      if sum(source[x.name]['faces'] for x in members)>120:raise RuntimeError('compound violates120poly cap')
      candidate_groups.append((key,members))
    # Use >=3 real shape templates per kind sequence, repeated instances. A sequence
    # with fewer six legal proposals stays unchanged rather than creating uniques.
    by=collections.defaultdict(list)
    for key,obs in candidate_groups:
     obs=sorted(obs,key=lambda o:o.data.name);by[tuple(o.data.name for o in obs)].append((key,obs))
    variants={};proposals=[];reject=collections.Counter();groups=[];replacement_by_id={};remove_queue=[]
    for kinds,items in sorted(by.items()):
     if len(items)<6:reject['insufficient_real_variant_instances']+=len(items);continue
     for j in range(3):
      key,obs=items[j];pivot=np.array(obs[0].matrix_world.translation);V=[];F=[];UV=[];colors=[];parts=[];vi=fi=li=0
      for ob in obs:
       r=source[ob.name];W=r['V']-pivot
       # All actual tuple roots are on frozen flat ground. Preserve every source
       # vertex, polygon, UV and Col float in the authored template.
       V.extend(map(tuple,W));F.extend(tuple(vi+i for i in p.vertices) for p in ob.data.polygons)
       UV.extend(map(tuple,r['uv']));colors.extend(map(tuple,r['rgba']))
       parts.append(dict(source=ob.name,kind=ob.data.name,verts=[vi,vi+len(W)],faces=[fi,fi+len(ob.data.polygons)],root_local=(r['pivot']-pivot).tolist(),source_vertex_sha=hashlib.sha256(np.asarray(W,np.float64).tobytes()).hexdigest(),rgba_sha=hashlib.sha256(r['rgba'].tobytes()).hexdigest()))
       vi+=len(W);fi+=len(ob.data.polygons)
      me=bpy.data.meshes.new('PLANT_TUFT_COMPOUND_'+str(len(variants)));me.from_pydata(V,[],F);me.materials.append(bpy.data.materials['LK_PLANTS'])
      uv=me.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',np.asarray(UV,float).ravel())
      col=me.color_attributes.new(name='Col',type='FLOAT_COLOR',domain='POINT');col.data.foreach_set('color',np.asarray(colors,np.float32).ravel());me.color_attributes.active_color_name='Col';me.color_attributes.render_color_index=0
      me.update();me['lk_v3_real_tuft_parts']=json.dumps(parts);variants[(kinds,j)]=(me,parts)
     for i,(key,obs) in enumerate(items):
      me,parts=variants[(kinds,i%3)];oldp=np.array(obs[0].matrix_world.translation);local=np.array([v.co[:] for v in me.vertices]);old_height=max(source[o.name]['V'][:,2].max() for o in obs)-oldp[2]
      scale=min(2.,old_height/max(local[:,2].max(),1e-9));scale=max(.5,scale)
      # Genuine original pivot with a shared rotated whole-clump template. Scenery
      # positions and shader pivot phase may change; scoring positions may not.
      angle=float(obs[0].rotation_euler.z);rot=np.array(Matrix.Rotation(angle,3,'Z'))
      W=(local*scale)@rot.T+oldp;roots=np.array([p['root_local'] for p in parts])*scale@rot.T+oldp
      lies=P.lie_codes_m(D,roots[:,0],roots[:,1]);vertex_route=float(_dist(route,W[:,:2]).min());rootshore=float(_dist(shore,roots[:,:2]).min())
      if np.isin(lies,[P.LIE_WATER,P.LIE_FAIRWAY,P.LIE_GREEN,P.LIE_TEE,P.LIE_BUNKER]).any():reject['root_lie']+=1;continue
      if rootshore<1.1:reject['shore']+=1;continue
      if vertex_route<2.:reject['route']+=1;continue
      if np.linalg.norm(np.ptp(W[:,:2],axis=0))>20.:reject['span']+=1;continue
      if float(np.ptp(W[:,2]))>2.0:reject['height']+=1;continue
      # Keep the original group's largest cap: this does not prune real tufts.
      if W[:,2].max()>max(source[o.name]['V'][:,2].max() for o in obs)+1e-6:reject['original_top_cap']+=1;continue
      supported=True
      for root in roots:
       hit=ground.ray_cast(Vector((float(root[0]),float(root[1]),40.)),Vector((0,0,-1)))
       if hit[0] is None or abs(float(root[2])-hit[0].z)>.06:supported=False;break
      if not supported:reject['all_root_support']+=1;continue
      prototypes=[o for o in bpy.data.objects if o.name.startswith('PLANT_TUFT_COMPOUND') and o.data==me]
      proposals.append(dict(key=key,obs=obs,me=me,parts=parts,loc=oldp,scale=scale,angle=angle,roots=roots,span=float(np.linalg.norm(np.ptp(W[:,:2],axis=0)))))
    # Insufficient real mesh users are not installed; no keepalive hidden objects.
    users=collections.Counter(x['me'].name for x in proposals)
    for row in proposals:
     if users[row['me'].name]<2:reject['one_accepted_variant_user']+=1;continue
     obs=row['obs'];ob=bpy.data.objects.new(row['me'].name+'.'+str(len(groups)).zfill(3),row['me']);obs[0].users_collection[0].objects.link(ob);ob.parent=obs[0].parent;ob.location=row['loc'];ob.scale=(row['scale'],)*3;ob.rotation_euler.z=row['angle']
     ob['h9_zone']=row['key'][0];ob['lk_v3_real_tuft_parts']=json.dumps(row['parts']);ob['lk_v3_reauthored_clump']=True
     groups.append(dict(name=ob.name,replaced=[x.name for x in obs],real_tufts=len(obs),mesh=ob.data.name,span_m=row['span'],actual_roots=row['roots'].tolist(),parts=row['parts']))
     for old in obs:replacement_by_id[id(old)]=ob;remove_queue.append(old)
    
    # Remap all real scenery references while every old Blender object is valid,
    # then delete the replaced instances. No stale wrapper or keepalive remains.
    def remap(value):
        if id(value) in replacement_by_id:return replacement_by_id[id(value)]
        if isinstance(value,list):
            out=[];seen=set()
            for item in value:
                item=remap(item)
                if isinstance(item,bpy.types.Object):
                    if id(item) in seen:continue
                    seen.add(id(item))
                out.append(item)
            return out
        if isinstance(value,dict):
            for key in list(value):value[key]=remap(value[key])
            if 'objs' in value and isinstance(value['objs'],list):
                live=[o for o in value['objs'] if isinstance(o,bpy.types.Object) and o.type=='MESH']
                value['count']=len(live)
                value['tris']=sum(len(o.data.polygons) if all(len(p.vertices)==3 for p in o.data.polygons) else sum(len(p.vertices)-2 for p in o.data.polygons) for o in live)
                value['renderer_by_kind']=dict(collections.Counter(o.data.name for o in live))
                value['actual_whole_tufts']=sum(len(json.loads(o.data.get('lk_v3_real_tuft_parts','[]'))) if o.get('lk_v3_reauthored_clump') else 1 for o in live)
            return value
        return value
    remap(scn)
    for old in remove_queue:bpy.data.objects.remove(old,do_unlink=True)
    S=snap();tri_after=sum(S.loc(o)['ntri'] for o in S.meshes.values())
    if tri_after!=tri_before:raise RuntimeError('shared clump changed actual triangle total')
    for name,row in frozen.items():
        w=S.wld(S.meshes[name])
        if not np.array_equal(w['W'],row['V']) or not np.array_equal(w['T'],row['T']):
            raise RuntimeError('shared clump changed scoring geometry')
    actual_whole_tufts=sum(len(g['parts']) for g in groups)+sum(1 for n,o in S.meshes.items() if n.startswith('PLANT_TUFT') and not o.get('lk_v3_reauthored_clump'))
    if actual_whole_tufts!=len(original):raise RuntimeError('lost a real whole tuft')
    result=dict(plants_before=plants_before,plants_after=len(S.named('PLANT_')),
                tuft_renderers_before=len(original),tuft_renderers_after=len(S.named('PLANT_TUFT')),
                actual_whole_tufts_before=len(original),actual_whole_tufts_after=actual_whole_tufts,
                triangles_before=tri_before,triangles_after=tri_after,
                compound_renderers=len(groups),real_shared_compound_variants=len({g['mesh'] for g in groups}),
                original_singles_retained=singles,rejects=dict(reject),groups=groups,
                native_draw_calls_unmeasured=True)
    D.shared_tuft_clumps=result
    return result
