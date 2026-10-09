"""hole09_qa.py - the hole's own gates for the Split look build (POSTCARD_LOOK CONTINUE). Reads a staged hole_09.blend (and its FBX), changes nothing.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/hole09_qa.py -- \
        --blend DIR/hole_09.blend [--fbx DIR/hole_09.fbx] [--baseline work/postcard-look/baseline/blender/hole_09.blend] [--json OUT.json]

Prints `GATE: <ID> PASS|FAIL - <measured numbers>` lines (exit 1 on any FAIL). It covers the lines of the brief that postcard_look_verify.py does not
(it is run next to it, not instead of it): play surfaces vs the baseline blend, no 12-vertex rock, no stretch past 2x, no plant on the sea, no foam
strip, triangle / FBX budget, the Split features (ridge scrub, ruin with a doorway and a broken arch, sea stacks, jagged shore, scrub boundary
covered), the legacy hole rules re-expressed for the new pieces (route clearance, channel lip, landing band, 7 yd inside the cliff, sight lines),
pin / tee clearance, and a geometry hash for the reproducibility gate.
"""
import hashlib
import json
import math
import os
import sys
import time

sys.dont_write_bytecode = True
import numpy as np
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
REPO = os.path.dirname(os.path.dirname(HERE))

import postcard_lib as P  # noqa: E402
import postcard_look_lib as L  # noqa: E402
import postcard_props_lib as PL  # noqa: E402
import hole09_look as HL  # noqa: E402

YD = P.YD
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def opt(name, default=None):
    return ARGS[ARGS.index(name) + 1] if name in ARGS and ARGS.index(name) + 1 < len(ARGS) else default


BLEND = os.path.abspath(opt("--blend"))
FBX = os.path.abspath(opt("--fbx")) if opt("--fbx") else None
BASE = os.path.abspath(opt("--baseline", os.path.join(REPO, "work", "postcard-look", "baseline", "blender", "hole_09.blend")))
JSON_OUT = opt("--json")
RES = []


def gate(gid, ok, detail):
    RES.append(dict(id=gid, ok=bool(ok), detail=str(detail)))
    print(f"GATE: {gid} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)


# ----------------------------------------------------------------------------------------------- scene helpers
GROUND = P.GROUND_PREFIXES


def in_library_only(o):
    return all(c.name == "ASSET_LIBRARY" for c in o.users_collection) and len(o.users_collection) > 0


def export_meshes():
    return [o for o in bpy.data.objects if o.type == 'MESH' and not in_library_only(o)]


def world_xyz(o):
    return L._mesh_arrays(o)[0]


def sv(o):
    """Singular values of the 3x3 world matrix."""
    return np.linalg.svd(np.array(o.matrix_world.to_3x3()), compute_uv=False)


def dist_poly(poly, xy):
    A, B = poly[:-1], poly[1:]
    return P._dist_pts_segs(np.asarray(xy, float).reshape(-1, 2), A, B)


def is_loose_rock(n):
    return n.startswith(("ROCK_", "CLIFF_ROCK")) and not n.startswith(("ROCK_SKIN", "ROCK_WALL"))


def group_of(o):
    n = o.name
    if n.startswith("PLANT_"):
        return "plant"
    if n.startswith("ROCK_SKIN"):
        return "skin"
    if n.startswith(("ROCK_SEASTACK", "ROCK_SPIRE")):
        return "stack"
    if n.startswith("ROCK_"):
        return "rock"
    if n.startswith("DRESS_RUIN") or n.startswith("DRESS_ARCH"):
        return "masonry"
    if n.startswith("DRESS_BLOCK"):
        return "block"
    if n.startswith("DRESS_SAND"):
        return "sand"
    if n.startswith("DRESS_KNOLL"):
        return "knoll"
    if n.startswith("WATER_"):
        return "water"
    if n.startswith(GROUND):
        return "ground"
    return "other"


# ----------------------------------------------------------------------------------------------- gates
def g_play_unchanged(D):
    names = [n for n in bpy.data.objects.keys() if n.startswith(GROUND)]
    cur = {n: bpy.data.objects[n] for n in names}
    with bpy.data.libraries.load(BASE, link=False) as (src, dst):
        want = [n for n in src.objects if n.startswith(GROUND)]
        dst.objects = list(want)
    base = dict(zip(want, dst.objects))
    miss = sorted(set(want) ^ set(cur))
    worst, nv, topo_bad, nmat_diff = 0.0, 0, [], 0
    for n in sorted(set(want) & set(cur)):
        a, b = base[n].data, cur[n].data
        va = np.array([tuple(v.co) for v in a.vertices])
        vb = np.array([tuple(v.co) for v in b.vertices])
        if va.shape != vb.shape:
            topo_bad.append(f"{n} verts {len(va)}->{len(vb)}")
            continue
        worst = max(worst, float(np.abs(va - vb).max()) if len(va) else 0.0)
        nv += len(va)
        pa = [tuple(p.vertices) for p in a.polygons]
        pb = [tuple(p.vertices) for p in b.polygons]
        if pa != pb:
            topo_bad.append(f"{n} polygon index lists differ")
        mw = np.array(cur[n].matrix_world)
        if np.abs(mw - np.eye(4)).max() > 1e-9:
            topo_bad.append(f"{n} matrix_world not identity")
    ok = not miss and not topo_bad and worst <= 1e-5
    return ok, (f"{len(cur)} ground meshes (TERRAIN/FAIRWAY/GREEN/TEE_BOX/BUNKER) vs the baseline blend {os.path.relpath(BASE, REPO)}: {nv} vertices, "
                f"max |delta| {worst:.2e} m (tol 1e-5), identical polygon index lists, identity transforms; name set differences {miss or 'none'}; problems {topo_bad or 'none'}")


def g_no_12_vertex_rock(label, objs):
    rocks = [o for o in objs if is_loose_rock(o.name) or o.name.startswith("ROCK_SKIN")]
    meshes = {}
    for o in rocks:
        meshes.setdefault(o.data.name, (o.data, o.name))
    rows = {k: PL.welded_vertex_count(me) for k, (me, _n) in meshes.items()}
    nmin = min(rows.values()) if rows else 0
    bad = {k: v for k, v in rows.items() if v < 60}
    legacy_meshes = sorted(k for k in rows if k.split(".")[0] in ("ROCK_SMALL", "ROCK_MEDIUM", "ROCK_LARGE", "CLIFF_ROCK", "ROCK_WALL_FLAT", "ROCK_WALL_BLUNT") or k.startswith("ROCK_STACK"))
    ico = [k for k, (me, _n) in meshes.items() if len(me.vertices) <= 12 or (len(me.vertices), len(me.polygons)) == (12, 20)]
    # round 2: the fallen masonry blocks / rubble (DRESS_BLOCK_*) are held to the same 60-vertex floor (the library blocks have 24; chip_blocks gives them 70)
    blocks = [o for o in objs if o.name.startswith("DRESS_BLOCK")]
    bmesh_ = {}
    for o in blocks:
        bmesh_.setdefault(o.data.name, o.data)
    brows = {k: PL.welded_vertex_count(me) for k, me in bmesh_.items()}
    bbad = {k: v for k, v in brows.items() if v < 60}
    ok = bool(rocks) and not bad and not legacy_meshes and not ico and not bbad and bool(blocks)
    return ok, (f"[{label}] {len(rocks)} rock objects on {len(meshes)} mesh datablocks: min welded vertices {nmin} (need >= 60), "
                f"meshes under 60: {bad or 'none'}, 12-vertex / 20-face icosahedra: {ico or 'none'}, legacy mesh names (ROCK_SMALL/MEDIUM/LARGE, CLIFF_ROCK, "
                f"ROCK_WALL_FLAT/BLUNT, ROCK_STACK_nn): {legacy_meshes or 'none'}; {len(blocks)} fallen DRESS_BLOCK instances on {len(bmesh_)} meshes "
                f"{brows} (need >= 60 welded vertices each; round 1 had 24: they were chipped), blocks under 60: {bbad or 'none'}")


def g_no_stretch(objs):
    pool = [o for o in objs if o.name.startswith(("ROCK_", "DRESS_", "CLIFF_ROCK")) and not o.name.startswith(("ROCK_SKIN", "DRESS_SMOKE", "DRESS_CONE"))]
    worst_ratio, lo, hi, bad = 1.0, 9.0, 0.0, []
    for o in pool:
        s = sv(o)
        lo, hi = min(lo, float(s.min())), max(hi, float(s.max()))
        ratio = float(s.max() / max(s.min(), 1e-9))
        worst_ratio = max(worst_ratio, ratio)
        if s.min() < 0.5 - 1e-3 or s.max() > 2.0 + 1e-3 or ratio > 2.0 + 1e-3:
            bad.append(f"{o.name} sv {np.round(s, 3).tolist()}")
    rep = PL.stretch_report([o for o in pool])
    ok = not bad and rep["ok"]
    return ok, (f"{len(pool)} ROCK_/DRESS_ instances: singular values of matrix_world in [{lo:.2f}, {hi:.2f}] (limits 0.5..2.0), worst max/min {worst_ratio:.2f} (limit 2.0); "
                f"PL.stretch_report on the library instances: n {rep['n']}, scale {rep['min_scale']:.2f}..{rep['max_scale']:.2f}, max/min {rep['max_ratio']:.2f}, "
                f"violations {len(rep['violations'])}; offenders {bad[:5] or 'none'}")


def ground_bvh(objs):
    V, F = [], []
    for o in objs:
        if not o.name.startswith(("TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER")):
            continue
        W, ls, lt, lv, mi, pn = L._mesh_arrays(o)
        b = len(V)
        V += [tuple(p) for p in W.tolist()]
        for p in range(len(ls)):
            if pn[p, 2] > 0.2:
                a, n = int(ls[p]), int(lt[p])
                F.append([b + int(v) for v in lv[a:a + n]])
    return BVHTree.FromPolygons(V, F)


def g_no_plant_on_water(label, D, objs, shore_xy):
    plants = [o for o in objs if o.name.startswith("PLANT_")]
    bvh = ground_bvh(objs)
    n_water, n_low, n_off, n_shore, n_support, worst_dz, min_sh = 0, 0, 0, 0, 0, 0.0, 99.0
    worst_vtx, who_dz, who_vtx = 0.0, "", ""
    for o in plants:
        W = world_xyz(o)
        bx, by, bz = float(o.matrix_world.translation.x), float(o.matrix_world.translation.y), float(o.matrix_world.translation.z)
        zlow = float(W[:, 2].min())
        lie = int(P.lie_codes_m(D, np.array([bx]), np.array([by]))[0])
        if lie == P.LIE_WATER:
            n_water += 1
        if bz < D.play_z - 1.0:
            n_low += 1
        sh = float(dist_poly(shore_xy, [(bx, by)])[0])
        min_sh = min(min_sh, sh)
        if sh < 1.0:
            n_shore += 1
        if lie in (P.LIE_FAIRWAY, P.LIE_GREEN, P.LIE_TEE, P.LIE_BUNKER):
            n_off += 1
        h = bvh.ray_cast((bx, by, 40.0), (0, 0, -1), 80.0)
        if h[0] is None:
            n_support += 1
        else:
            if abs(bz - h[0].z) > worst_dz:
                worst_dz, who_dz = abs(bz - h[0].z), o.name
            if abs(zlow - h[0].z) > worst_vtx:
                worst_vtx, who_vtx = abs(zlow - h[0].z), o.name
    ok = bool(plants) and not (n_water or n_low or n_off or n_shore or n_support) and worst_dz <= 0.06 and worst_vtx <= 0.30
    return ok, (f"[{label}] {len(plants)} PLANT_* objects: base on water {n_water}, base below z {D.play_z - 1.0:g} m {n_low}, on a play surface lie (fairway/green/tee/bunker) {n_off}, "
                f"within 1.0 m of the shore {n_shore} (nearest {min_sh:.2f} m), no ground under the base {n_support}, worst |origin z - ground| {worst_dz:.3f} m ({who_dz}; the lib sinks a plant 0.02-0.045 m: need <= 0.06), "
                f"worst lowest-vertex offset {worst_vtx:.3f} m ({who_vtx}; tilt + sink, need <= 0.30)")


def g_foam(objs, v2):
    foam_names = [o.name for o in bpy.data.objects if "FOAM" in o.name.upper()]
    foam_mats = [m.name for m in bpy.data.materials if "FOAM" in m.name.upper() and m.users > 0]
    uses = [o.name for o in objs if any(sl.material is not None and sl.material.name == "MAT_FOAM" for sl in o.material_slots)]
    lines = {n: (ok, d) for n, ok, d in v2}
    nf = lines.get("NO_FOAM_STRIP")
    sh = lines.get("SHELF_THIN")
    ok = not foam_names and not foam_mats and not uses and nf is not None and nf[0]
    return ok, (f"MAT_FOAM / *FOAM* objects {foam_names or 'none'}, materials {foam_mats or 'none'}, mesh users {uses or 'none'}; lib NO_FOAM_STRIP: "
                f"{nf[1][:380] if nf else 'missing'}")


def g_budget(objs, fbx):
    tris = 0
    by = {}
    for o in objs:
        me = o.data
        me.calc_loop_triangles()
        t = len(me.loop_triangles)
        tris += t
        by[group_of(o)] = by.get(group_of(o), 0) + t
    mb = os.path.getsize(fbx) / 1048576 if fbx and os.path.exists(fbx) else float("nan")
    big = [(o.name, len(o.data.polygons)) for o in objs if group_of(o) in ("plant", "rock", "stack") and len(o.data.polygons) > 500]
    ok = tris <= 250000 and mb <= 20.0 and not big
    return ok, (f"{tris} triangles over {len(objs)} mesh objects (limit 250000; instances counted), FBX {mb:.2f} MB (limit 20); by group {by}; "
                f"prop meshes over 500 faces: {big or 'none'}")


def g_ridge_features(D, objs):
    sc = D.SCENERY["ridge"]
    spine = HL.spine_m(D)
    cl = HL.center_m(D)
    side = HL.mask_ridge_side(D)
    plants = [o for o in objs if o.name.startswith("PLANT_")]
    rocks = [o for o in objs if is_loose_rock(o.name)]

    def xy(o):
        return o.matrix_world.translation.x, o.matrix_world.translation.y
    pl_xy = np.array([xy(o) for o in plants])
    rk_xy = np.array([xy(o) for o in rocks])
    pl_ridge = int(side(pl_xy[:, 0], pl_xy[:, 1]).sum()) if len(plants) else 0
    rk_ridge = int(side(rk_xy[:, 0], rk_xy[:, 1]).sum()) if len(rocks) else 0
    kinds = {}
    for o in plants:
        k = o.name.split(".")[0]
        kinds[k] = kinds.get(k, 0) + 1
    n_shr = sum(v for k, v in kinds.items() if "SHRUB" in k)
    n_tuft = sum(v for k, v in kinds.items() if "TUFT" in k)
    n_fl = sum(v for k, v in kinds.items() if "FLOWER" in k)
    ok = pl_ridge >= 30 and rk_ridge >= 8 and n_shr >= 6 and n_tuft >= 40
    return ok, (f"ridge side (closer to the ridge spine than to the ribbon centerline): {pl_ridge} PLANT_* and {rk_ridge} loose ROCK_* (need >= 30 / >= 8); whole hole: "
                f"{len(plants)} plants = {n_shr} shrubs, {n_tuft} tufts, {n_fl} flowers (sparse), {kinds.get('PLANT_AGAVE_A', 0)} agave; plant kinds {sorted(kinds)}")


def ray_obj_bvh(objs):
    V, F = [], []
    owner = []
    for o in objs:
        W, ls, lt, lv, mi, pn = L._mesh_arrays(o)
        b = len(V)
        V += [tuple(p) for p in W.tolist()]
        for p in range(len(ls)):
            a, n = int(ls[p]), int(lt[p])
            F.append([b + int(v) for v in lv[a:a + n]])
            owner.append(o.name)
    return BVHTree.FromPolygons(V, F), owner


def g_ruin(D, objs):
    ru = [o for o in objs if o.name.startswith("DRESS_RUIN")]
    ar = [o for o in objs if o.name.startswith("DRESS_ARCH")]
    blocks = [o for o in objs if o.name.startswith("DRESS_BLOCK")]
    mats = {sl.material.name for o in ru + ar + blocks for sl in o.material_slots if sl.material}
    # separate mesh islands = masonry pieces
    n_pieces = 0
    for o in ru + ar:
        me = o.data
        bm_islands = len(L_islands(me))
        n_pieces += bm_islands
    allm = ru + ar
    V = np.vstack([world_xyz(o) for o in allm])
    top = float(V[:, 2].max() - D.play_z)
    # doorway: rays through the tall wall, perpendicular to it, at the doorway point
    R = D.SCENERY["ruin"]
    walls = R["walls"]
    rc = np.array(P.m(sum(w[k][0] for w in walls for k in ("p0", "p1")) / 8.0, sum(w[k][1] for w in walls for k in ("p0", "p1")) / 8.0))
    big = lambda p: rc + (np.array(P.m(*p)) - rc) * HL.RUIN_SCALE + np.array(P.m(*HL.RUIN_SHIFT_YD))      # noqa: E731
    a, b = big(walls[2]["p0"]), big(walls[2]["p1"])
    d = (b - a) / np.hypot(*(b - a))
    nrm = np.array([-d[1], d[0]])
    bvh, owner = ray_obj_bvh(ru)
    mid = (a + b) / 2
    best = None
    for off in np.linspace(-3.0, 3.0, 25):                 # along the wall: find the widest free column through the full height range
        c = mid + d * off
        free = 0
        for z in np.arange(0.3, 2.45, 0.25):
            o0 = Vector((c[0] - nrm[0] * 3.0, c[1] - nrm[1] * 3.0, D.play_z + z))
            h = bvh.ray_cast(o0, Vector((nrm[0], nrm[1], 0.0)), 6.0)
            free += h[0] is None
        if best is None or free > best[0]:
            best = (free, off)
    n_levels = len(np.arange(0.3, 2.45, 0.25))
    # width of the free opening around the best column, and a lintel above it
    free_cols = []
    for off in np.linspace(-4.0, 4.0, 81):
        c = mid + d * off
        ok_all = True
        for z in np.arange(0.3, 2.45, 0.25):
            o0 = Vector((c[0] - nrm[0] * 3.0, c[1] - nrm[1] * 3.0, D.play_z + z))
            if bvh.ray_cast(o0, Vector((nrm[0], nrm[1], 0.0)), 6.0)[0] is not None:
                ok_all = False
                break
        if ok_all:
            free_cols.append(off)
    span = (max(free_cols) - min(free_cols)) if free_cols else 0.0
    lint = False
    if free_cols:
        c = mid + d * float(np.mean(free_cols))
        for z in np.arange(2.5, 4.5, 0.2):
            o0 = Vector((c[0] - nrm[0] * 3.0, c[1] - nrm[1] * 3.0, D.play_z + z))
            if bvh.ray_cast(o0, Vector((nrm[0], nrm[1], 0.0)), 6.0)[0] is not None:
                lint = True
                break
    # arch: opening clear under the crown, crown / haunch present above
    arch_h = float(world_xyz(ar[0])[:, 2].max() - D.play_z) if ar else 0.0
    n_fall = len(blocks)
    # repair round 3: the stone TOPS and about one stone in four are LK_ROCK (weathered, one continuous box projection; the 'flat-colour green slab' tops of the round-2 brick windows). The contract of the
    # masonry class stays: only LK_MASONRY / LK_ROCK, and every DRESS_RUIN / DRESS_ARCH mesh keeps >= 50 % of its area on LK_MASONRY (PROP_MATERIALS) and < 50 % on LK_ROCK (not a rock mesh).
    low_m, high_r = 1.0, 0.0
    for o in ru + ar:
        me_ = o.data
        ar_ = {}
        for p_ in me_.polygons:
            m_ = me_.materials[min(p_.material_index, len(me_.materials) - 1)] if len(me_.materials) else None
            ar_[m_.name.split(".")[0] if m_ else ""] = ar_.get(m_.name.split(".")[0] if m_ else "", 0.0) + float(p_.area)
        tot_ = sum(ar_.values()) or 1.0
        low_m, high_r = min(low_m, ar_.get("LK_MASONRY", 0.0) / tot_), max(high_r, ar_.get("LK_ROCK", 0.0) / tot_)
    ok_mat = mats <= {"LK_MASONRY", "LK_ROCK"} and "LK_MASONRY" in mats and low_m >= 0.5 and high_r < 0.5
    ok = ok_mat and n_pieces >= 12 and top >= 2.0 and span >= 1.5 and lint and bool(ar) and n_fall >= 4
    return ok, (f"{len(ru)} DRESS_RUIN_* + {len(ar)} DRESS_ARCH_* meshes, materials {sorted(mats)} (lowest LK_MASONRY area share of a mesh {low_m:.0%} >= 50 %, highest LK_ROCK share {high_r:.0%} < 50 %), {n_pieces} separate masonry pieces (need >= 12), top {top:.1f} m above the ridge (need >= 2), "
                f"a through-doorway {span:.1f} m wide x 2.4 m clear with a lintel above it ({lint}) in the tall wall (need >= 1.5 m), broken arch {arch_h:.1f} m tall, "
                f"{n_fall} fallen DRESS_BLOCK + rubble stones, no 12-vertex rock stands in for masonry")


def L_islands(me):
    """Connected components (by shared vertices) of a mesh, as sets of polygon indices."""
    parent = list(range(len(me.vertices)))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for p in me.polygons:
        vs = list(p.vertices)
        for v in vs[1:]:
            ra, rb = find(vs[0]), find(v)
            if ra != rb:
                parent[ra] = rb
    comp = {}
    for p in me.polygons:
        comp.setdefault(find(p.vertices[0]), []).append(p.index)
    return comp


def g_legacy_rules(D, objs, shore_xy):
    route = HL.route_m(D)
    chan = HL.stretch_poly_m(D, "channel_wall_ridge")
    cl = HL.center_m(D)
    ridge_side = HL.mask_ridge_side(D)
    R = D.SCENERY["ruin"]
    out = {}
    # 1. route clearance (legacy ROUTE_CLEAR_YD = 8 yd): rocks (not wet / sea ones), shrubs, masonry, outcrops. Round 2: the shrub / rock CHAIN over the scrub boundary
    #    (custom prop h9_class = "cover") is the one class held to the design's own rule (hole09_design: "keep rocks >= 4 yd off [the route]" = 3.66 m), because the scrub
    #    boundary crosses the route at both ridge ends and an 8 yd buffer there leaves 20 m of the colour step bare (the reviewer's bare runs). Everything else keeps 8 yd.
    land_all = [o for o in objs if (is_loose_rock(o.name) and "_WET" not in o.name) or o.name.startswith(("PLANT_SHRUB", "PLANT_AGAVE"))]
    land_all = [o for o in land_all if not o.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE")) and float(o.matrix_world.translation.z) > 4.0]
    land = [o for o in land_all if o.get("h9_class") != "cover"]
    cover_ = [o for o in land_all if o.get("h9_class") == "cover"]
    dr = sorted((float(dist_poly(route, world_xyz(o)[:, :2]).min()), o.name) for o in land)
    dmin_rock, who = dr[0]
    who = str([(round(a, 1), b) for a, b in dr[:3]])
    dcov = min([float(dist_poly(route, world_xyz(o)[:, :2]).min()) for o in cover_] or [99.0])
    mas = [o for o in objs if o.name.startswith(("DRESS_RUIN", "DRESS_ARCH"))]
    dmin_mas = min(float(dist_poly(route, world_xyz(o)[:, :2]).min()) for o in mas)
    knolls_ = [o for o in objs if o.name.startswith("DRESS_KNOLL")]
    dknoll = min([float(dist_poly(route, world_xyz(o)[:, :2]).min()) for o in knolls_] or [99.0])
    tufts = [o for o in objs if o.name.startswith(("PLANT_TUFT", "PLANT_FLOWER"))]
    dmin_tuft = min(float(dist_poly(route, world_xyz(o)[:, :2]).min()) for o in tufts)
    out["ROUTE_CLEAR"] = (dmin_rock >= 8 * YD - 0.05 and dmin_mas >= 6 * YD - 0.05 and dmin_tuft >= 2.0 and dcov >= 4 * YD - 0.05 and dknoll >= 5 * YD - 0.05,
                          f"nearest vertex to the dry safe ridge route: land rocks / shrubs / agave {dmin_rock:.2f} m ({len(land)} pieces; {who}; need >= 8 yd = {8 * YD:.2f}), DRESS_KNOLL hummocks {dknoll:.2f} m (need >= 5 yd = {5 * YD:.2f}: the design's 4 yd + 1; low soft hummocks, their own class), "
                          f"ruin masonry {dmin_mas:.2f} m (need >= 6 yd = {6 * YD:.2f}: the design's own ruin clearance), tufts / flowers {dmin_tuft:.2f} m (need >= 2.0); the {len(cover_)} shrubs / rocks of the scrub-boundary "
                          f"chain {dcov:.2f} m (need >= 4 yd = {4 * YD:.2f} m: the design's own 'keep rocks >= 4 yd off the route' rule, the one class split from the 8 yd margin, see the comment)")
    # 2. no rim rock on the ridge lip that faces the channel
    rim = [o for o in objs if is_loose_rock(o.name) and "_WET" not in o.name and not o.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE")) and float(o.matrix_world.translation.z) > 4.0]
    dl = sorted((float(dist_poly(chan, world_xyz(o)[:, :2]).min()), o.name) for o in rim)
    dlip = dl[0][0]
    out["RIM_ROCKS_OFF_CHANNEL_LIP"] = (dlip >= 5.9, f"{len(rim)} land rocks: nearest vertex to the channel_wall_ridge lip {dlip:.2f} m (closest {[(round(a, 1), b) for a, b in dl[:3]]}) (the legacy rule kept rim rocks off the lip that faces the channel; need >= 5.9)")
    # 3. ruin off the drive landing band (x -75..-35, D 225-275 yd) and 7 yd inside the cliff
    V = np.vstack([world_xyz(o) for o in mas])
    Vyd = V[:, :2] / YD
    in_band = ((Vyd[:, 0] >= -75) & (Vyd[:, 0] <= -35) & (Vyd[:, 1] >= 225) & (Vyd[:, 1] <= 275)).sum()
    dshore = float(dist_poly(shore_xy, V[:, :2]).min())
    blocks_ = [o for o in objs if o.name.startswith("DRESS_BLOCK")]
    dshore_blocks = min([float(dist_poly(shore_xy, world_xyz(o)[:, :2]).min()) for o in blocks_] or [99.0])
    out["RUIN_LANDING_BAND_AND_CLIFF"] = (in_band == 0 and dshore >= 7 * YD - 0.1,
                                          f"ruin + arch vertices in the drive landing band x -75..-35 / D 225-275 yd: {int(in_band)}; nearest distance to the shore {dshore:.1f} m = {dshore / YD:.1f} yd (need >= 7; fallen blocks / rubble: {dshore_blocks / YD:.1f} yd); "
                                          f"ruin spans D {Vyd[:, 1].min():.0f}..{Vyd[:, 1].max():.0f} yd (design 93-121 + arch), x {Vyd[:, 0].min():.0f}..{Vyd[:, 0].max():.0f}")
    # 4. sight lines: nothing tall within 12 m of the centerline legs (tee -> stations -> pin) except the sea stacks, which the design placed >= 15 yd off
    tall = [o for o in objs if group_of(o) in ("stack", "masonry") or (o.name.startswith("ROCK_CRAG"))]
    dsl = min(float(dist_poly(cl, world_xyz(o)[:, :2]).min()) for o in tall)
    mouth = [o for o in objs if group_of(o) == "stack" and abs(o.matrix_world.translation.x / YD - 34.0) < 12 and abs(o.matrix_world.translation.y / YD - 372.0) < 12]
    dm = min(float(dist_poly(cl, world_xyz(o)[:, :2]).min()) for o in mouth) if mouth else float("nan")
    out["SIGHT_LINES"] = (dsl >= 12.0, f"{len(tall)} tall pieces (sea stacks, ruin, arch, crags): nearest vertex to the centerline tee -> stations -> pin {dsl:.1f} m (need >= 12); the mouth stack (34, 372) pieces: "
                                      f"nearest {dm:.1f} m ({dm / YD:.1f} yd; the design moved it to 24 yd off the carry line)")
    # 5. pin / tee clearance
    pin = np.array(P.m(*D.hole.pin))
    marks = {"MARKER_TEE": None, "MARKER_PIN": None, "TEE_MARKER_1": None, "TEE_MARKER_2": None, "HOLE_CUP": None}
    pts = []
    for nm in marks:
        o = bpy.data.objects.get(nm)
        if o is not None:
            pts.append(np.array(o.matrix_world.translation)[:2])
    pts = np.array(pts)
    props = [o for o in objs if group_of(o) in ("plant", "rock", "masonry", "block", "stack")]
    dmin, dpin = 99.0, 99.0
    for o in props:
        W = world_xyz(o)[:, :2]
        for p in pts:
            dmin = min(dmin, float(np.hypot(W[:, 0] - p[0], W[:, 1] - p[1]).min()))
        dpin = min(dpin, float(np.hypot(W[:, 0] - pin[0], W[:, 1] - pin[1]).min()))
    out["PIN_TEE_CLEAR"] = (dmin >= 1.5 and dpin >= 6.0, f"{len(props)} props: nearest vertex to a tee / pin / cup marker {dmin:.1f} m (need >= 1.5), nearest to the pin {dpin:.1f} m (need >= 6)")
    return out


def g_scrub_boundary(D, objs):
    """The colour step between LK_SCRUB and the green rough (the tips of the scrub polygon at the ridge ends, 23 terrain edges) is covered by SHRUBS AND ROCKS
    (round 2: the reviewer measured that the round-1 count, 'any 2 props within 3 m', was carried by 0.4 m tufts): every 1 m sample along every edge, and every
    edge midpoint, is judged by the shrubs / agave / loose rocks (origin within 3 m); the longest bare run is measured. The old any-prop >= 2 rule stays (stricter AND)."""
    ob = bpy.data.objects[D.terrain_name]
    D.terrain = ob
    edges = HL.scrub_boundary_edges(D)
    mid = HL.scrub_boundary_mid(D)
    big = [o for o in objs if o.name.startswith(("PLANT_SHRUB", "PLANT_AGAVE")) or (is_loose_rock(o.name) and "_WET" not in o.name and not o.name.startswith(("ROCK_SEASTACK", "ROCK_SPIRE")) and float(o.matrix_world.translation.z) > 4.0)]
    anyp = [o for o in objs if o.name.startswith(("PLANT_", "ROCK_")) and not o.name.startswith(("ROCK_SKIN", "ROCK_SEASTACK", "ROCK_SPIRE", "ROCK_CRAG"))]
    bxy = np.array([[o.matrix_world.translation.x, o.matrix_world.translation.y] for o in big])
    axy = np.array([[o.matrix_world.translation.x, o.matrix_world.translation.y] for o in anyp])

    def counts(pts, xy):
        return np.array([int((np.hypot(xy[:, 0] - x, xy[:, 1] - y) <= 3.0).sum()) for x, y in pts])
    smp, slen = [], []                                         # 1 m samples along the edges
    for (ax, ay, bx, by) in edges:
        ln = math.hypot(bx - ax, by - ay)
        n = max(1, int(math.ceil(ln)))
        for k in range(n):
            u = (k + 0.5) / n
            smp.append((ax + (bx - ax) * u, ay + (by - ay) * u))
            slen.append(ln / n)
    smp, slen = np.array(smp), np.array(slen)
    cs = counts(smp, bxy)
    cm = counts(mid[:, :2], bxy)
    cm_any = counts(mid[:, :2], axy)
    f1 = float(slen[cs >= 1].sum() / slen.sum())
    f2 = float(slen[cs >= 2].sum() / slen.sum())
    m1 = float(mid[:, 2][cm >= 1].sum() / mid[:, 2].sum())
    m2 = float(mid[:, 2][cm >= 2].sum() / mid[:, 2].sum())
    old = float(mid[:, 2][cm_any >= 2].sum() / mid[:, 2].sum())
    # longest bare run: cluster the bare samples (single link 3 m) and take the biggest cluster length
    bare = smp[cs == 0]
    bl = slen[cs == 0]
    used = np.zeros(len(bare), bool)
    worst = 0.0
    for i in range(len(bare)):
        if used[i]:
            continue
        st, tot = [i], 0.0
        used[i] = True
        while st:
            k = st.pop()
            tot += bl[k]
            for j in np.nonzero((np.hypot(bare[:, 0] - bare[k, 0], bare[:, 1] - bare[k, 1]) < 3.0) & ~used)[0]:
                used[j] = True
                st.append(j)
        worst = max(worst, tot)
    tot_len = float(mid[:, 2].sum())
    # the route rule (design: shrubs / rocks >= 4 yd off the route) means no shrub / rock origin can stand within 3 m of a boundary point that lies on the route axis: the bare
    # samples must ALL be in that zone (within 2.2 m of the axis: a stone's origin must stand >= 3.66 m + its radius off the route and within 3 m of the sample) and the longest bare run stays under 10 m (two crossings: the tee end and the green end of the ridge)
    rax = HL.route_m(D)
    off_axis_bare = int(((cs == 0) & (HL.dist_to_poly(rax, smp[:, 0], smp[:, 1]) >= 2.2)).sum())
    ok = tot_len > 0 and f1 >= 0.90 and f2 >= 0.75 and m1 >= 0.90 and m2 >= 0.75 and old >= 0.85 and worst <= 10.0 and off_axis_bare == 0
    return ok, (f"{len(mid)} terrain edges between LK_SCRUB and rough faces ({tot_len:.0f} m): SHRUBS/ROCKS within 3 m: >= 1 along {f1:.0%} of the 1 m samples (need 90 %) / {m1:.0%} of the edge midpoints, "
                f">= 2 along {f2:.0%} / {m2:.0%} (need 75 %); longest bare run {worst:.1f} m (need <= 10: it is the route crossing), bare samples farther than 2.2 m from the route axis: {off_axis_bare} (need 0); the round-1 rule (ANY 2 plants/rocks incl. tufts within 3 m at the midpoints, need 85 %): {old:.0%}; "
                f"{len(big)} shrub/agave/rock pieces counted, {sum(1 for o in big if o.get('h9_class') == 'cover')} of them the boundary chain")


def g_ribbon(D, objs):
    by = {o.name: o for o in objs}
    need = {"FAIRWAY": "LK_FAIRWAY", "GREEN": "LK_GREEN", "BUNKER_01": "LK_SAND", "BUNKER_02": "LK_SAND", "BUNKER_03": "LK_SAND", "FAIRWAY_FIRSTCUT": "LK_ROUGH", "TEE_BOX": "LK_FAIRWAY"}   # repair round 1: TEE_BOX is LK_FAIRWAY (stripes run on under the tee ball, no seam; verify allows {LK_GREEN, LK_FAIRWAY}); the FIRSTCUT collar is the rough lie: LK_ROUGH (verify allows {LK_FAIRWAY, LK_ROUGH})
    got = {}
    for n, m in need.items():
        mats = {sl.material.name for sl in by[n].material_slots if sl.material}
        got[n] = sorted(mats)
    bad = [n for n, m in need.items() if m not in got[n]]
    look = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "Look")
    files = [f for f in ("Fairway_C.png", "Fairway_N.png", "Green_C.png", "Green_N.png", "Sand_C.png", "Sand_N.png", "Scrub_C.png", "Scrub_N.png") if not os.path.exists(os.path.join(look, f))]
    nb = len([o for o in objs if o.name.startswith("BUNKER_") and not o.name.endswith("_LIP")])
    greens = [o.name for o in objs if o.name == "GREEN"]
    flat = [n for n in by if n.startswith(GROUND) and any(sl.material is not None and sl.material.name.startswith("MAT_") for sl in by[n].material_slots)]
    ok = not bad and not files and nb == 3 and len(greens) == 1 and not flat
    return ok, (f"ribbon: FAIRWAY {got['FAIRWAY']} (LK_FAIRWAY = mow-striped albedo + normal, the old MAT_FAIRWAY / MAT_FAIRWAY_STRIPE flat colours are gone), TEE_BOX {got['TEE_BOX']} (stripes run on under the ball), FAIRWAY_FIRSTCUT {got['FAIRWAY_FIRSTCUT']} (the rough-lie collar), {nb} bunkers on {got['BUNKER_01']} "
                f"(LK_SAND albedo + normal, lips {sorted({sl.material.name for sl in by['BUNKER_01_LIP'].material_slots if sl.material})}), ONE GREEN on {got['GREEN']}; flat MAT_ ground meshes {flat or 'none'}; "
                f"missing texture files {files or 'none'}; mismatches {bad or 'none'}")


def g_stacks(D, objs):
    stacks = [o for o in objs if group_of(o) == "stack"]
    mains = [o for o in stacks if o.name.split(".")[0] in PL.SPIRE_KINDS or o.name.split(".")[0].replace("_WET", "") in PL.SPIRE_KINDS]
    legacy = [o.name for o in bpy.data.objects if o.name.startswith("ROCK_STACK")]
    reps = {}
    for o in mains:
        k = o.data.name
        if k not in reps:
            reps[k] = PL.seastack_report(o.data)
    bad = [k for k, r in reps.items() if not r["ok"]]
    tops = [float(world_xyz(o)[:, 2].max()) for o in mains]
    des = [s["height_m"] for s in D.SCENERY["sea_stacks"]]
    ok = not legacy and not bad and len(mains) >= 8 and max(tops) >= 16
    return ok, (f"{len(mains)} spire pieces on {len(reps)} meshes (tapering, grassy crowns; the 8 design stacks are clusters of a main spire + satellites + skirt boulders); legacy ROCK_STACK_nn: {legacy or 'none'}; "
                f"seastack_report ok for all: {not bad} (worst taper {max(r['taper'] for r in reps.values()):.2f} <= 0.45, flat cap {max(r['cap_share'] for r in reps.values()):.0%}, fill {max(max(r['fills']) for r in reps.values()):.2f}); "
                f"summits {min(tops):.1f}..{max(tops):.1f} m above the water (design heights {min(des):g}..{max(des):g} m)")


def g_ridge_material(D, objs):
    ob = bpy.data.objects[D.terrain_name]
    me = ob.data
    names = [m.name if m else "" for m in me.materials]
    W, ls, lt, lv, mi, pn = L._mesh_arrays(ob)
    top = (pn[:, 2] > 0.5) & L._top_mask(D, W, ls, lt, lv, pn)
    pol = L._poly_of_loop(ls, lt, len(lv))
    cen = np.zeros((len(ls), 2))
    np.add.at(cen, pol, W[lv, :2])
    cen /= lt[:, None]
    mat = np.array(names, object)[mi]
    side = HL.mask_ridge_side(D)(cen[:, 0], cen[:, 1])
    scr = (mat == "LK_SCRUB") & top
    area = np.zeros(len(ls))
    # polygon areas (xy)
    for p in range(len(ls)):
        v = W[lv[ls[p]:ls[p] + lt[p]], :2]
        area[p] = 0.5 * abs(np.dot(v[:, 0], np.roll(v[:, 1], -1)) - np.dot(v[:, 1], np.roll(v[:, 0], -1)))
    in_ridge = float(area[scr & side].sum() / max(area[scr].sum(), 1e-9))
    ribbon_scrub = int((scr & ~side).sum())
    # lie stays Rough: the design lie at the scrub face centres is Rough / OutOfBounds, never fairway / green / tee / bunker / water
    codes = P.lie_codes_m(D, cen[scr, 0], cen[scr, 1])
    lie_bad = int(np.isin(codes, [P.LIE_FAIRWAY, P.LIE_GREEN, P.LIE_TEE, P.LIE_BUNKER, P.LIE_WATER]).sum())
    ok = in_ridge >= 0.95 and lie_bad == 0 and scr.sum() > 600
    return ok, (f"{int(scr.sum())} TERRAIN top faces on LK_SCRUB (olive dry scrub, Scrub_C hue ~50), {in_ridge:.1%} of their area on the ridge side (need >= 95 %), {ribbon_scrub} faces on the ribbon side; "
                f"scrub faces whose scoring lie is not Rough / OutOfBounds: {lie_bad} (the lie is Hole.cs: untouched)")


def g_sand_overlay(D, objs):
    """Round 2 (reviewer: the bunkers were pale flat ellipses): DRESS_SAND_01..03 lay sand in lobes on the bunker lips. Measured on the overlay, never on the play meshes:
    only LK_SAND; the PATH_UNDER_BALL numbers (top <= 1.5 cm over the collider at every vertex / edge midpoint / centroid sample, vertices <= 1.2 cm, >= 60 % of the
    samples >= 3 mm up); and the outline: how far the lobes reach past the scoring ellipse (per 10 degree sector), the share of sectors with a lobe and the number
    of lobes. The scoring BUNKER_nn meshes are the H_PLAY_UNCHANGED gate's business."""
    sand = sorted((o for o in objs if o.name.startswith("DRESS_SAND")), key=lambda o: o.name)
    hz = [h for h in D.hole.hazards if h[0] == "bunker"]
    if len(sand) != len(hz):
        return False, f"{len(sand)} DRESS_SAND meshes for {len(hz)} bunkers"
    bvh = ground_bvh(objs)
    d_all, d_vtx = [], []
    rows, bad = [], []
    for o, h in zip(sand, hz):
        mats = {sl.material.name for sl in o.material_slots if sl.material}
        if mats != {"LK_SAND"}:
            bad.append(f"{o.name} materials {sorted(mats)}")
        W = world_xyz(o)
        me = o.data
        me.calc_loop_triangles()
        T = np.array([t.vertices for t in me.loop_triangles])
        nrm = np.array([t.normal.z for t in me.loop_triangles])
        T = T[nrm > 0.5]
        a, b, c = W[T[:, 0]], W[T[:, 1]], W[T[:, 2]]
        samp = np.concatenate([a, b, c, (a + b + c) / 3, (a + b) / 2, (b + c) / 2, (c + a) / 2])
        for k, (x, y, z) in enumerate(samp.tolist()):
            hit = bvh.ray_cast((x, y, 40.0), (0, 0, -1), 80.0)
            if hit[0] is None:
                continue
            dz = z - hit[0].z
            d_all.append(dz)
            if k < 3 * len(T):
                d_vtx.append(dz)
        cx, cy = P.m(h[1], h[2])
        ea, eb = h[3] / 2 * YD, h[4] / 2 * YD
        V = W[np.unique(T.ravel())]
        rel = V[:, :2] - np.array([cx, cy])
        rho = np.hypot(rel[:, 0] / ea, rel[:, 1] / eb)
        ext = np.hypot(rel[:, 0], rel[:, 1]) * (1.0 - 1.0 / np.maximum(rho, 1e-6))
        th = np.arctan2(rel[:, 1] / eb, rel[:, 0] / ea)
        sec = ((th + math.pi) / (2 * math.pi) * 36).astype(int) % 36
        e_s = np.zeros(36)
        for k in range(36):
            m_ = sec == k
            if m_.any():
                e_s[k] = max(0.0, float(ext[m_].max()))
        lobe = e_s >= 0.3
        runs = sum(1 for k in range(36) if lobe[k] and not lobe[k - 1])
        rows.append((o.name, len(T), float(e_s.max()), float(lobe.mean()), runs if not lobe.all() else 1, float(np.std(e_s))))
    d_all, d_vtx = np.array(d_all), np.array(d_vtx)
    top, topv, emerge = float(d_all.max()), float(d_vtx.max()), float((d_all >= 0.003).mean())
    org = all(r[2] >= 0.9 and 0.25 <= r[3] <= 0.85 and r[4] >= 2 for r in rows)
    ok = not bad and top <= 0.015 and topv <= 0.012 and emerge >= 0.6 and org
    return ok, (f"{len(sand)} DRESS_SAND meshes ({', '.join(f'{r[0]} {r[1]} up-facing tris' for r in rows)}), LK_SAND only {not bad}; over the collider: top at most {top * 100:.2f} cm at {len(d_all)} samples (need <= 1.5), "
                f"vertices {topv * 100:.2f} cm (<= 1.2), {emerge:.0%} of samples >= 3 mm up (>= 60 %); outline past the scoring ellipse per bunker: " +
                "; ".join(f"{r[0][-2:]}: reach {r[2]:.2f} m (need >= 0.9), lobes on {r[3]:.0%} of the 36 sectors (25-85 %), {r[4]} lobes (>= 2), sd {r[5]:.2f} m" for r in rows))


def g_ridge_relief(D, objs):
    """Round 2 (reviewer: the ridge read as a flat olive mat): DRESS_KNOLL hummocks + the crag groups. Counted, measured and checked against the ONE_GREEN lawn rule
    (flat grass-coloured area per DRESS_ object < 10 m2, all DRESS_ together < 20 m2, the verifier's own numbers)."""
    kn = [o for o in objs if o.name.startswith("DRESS_KNOLL")]
    crags = [o for o in objs if o.name.startswith("ROCK_CRAG")]
    pz = D.play_z
    tops = [float(world_xyz(o)[:, 2].max()) - pz for o in kn]
    lawn_each, lawn_tot, rock_share = [], 0.0, []
    for o in kn:
        me = o.data
        names = [m.name if m else "" for m in me.materials]
        W, ls, lt, lv, mi, pn = L._mesh_arrays(o)
        area = np.zeros(len(ls))
        for p in range(len(ls)):
            v = W[lv[ls[p]:ls[p] + lt[p]]]
            area[p] = 0.5 * np.linalg.norm(sum(np.cross(v[i] - v[0], v[i + 1] - v[0]) for i in range(1, len(v) - 1)))
        mname = np.array(names, object)[np.minimum(mi, len(names) - 1)]
        flat = (pn[:, 2] > 0.95) & np.isin(mname, ["LK_SCRUB", "LK_ROUGH", "LK_GREEN", "LK_FAIRWAY", "LK_PLANTS"])
        lawn_each.append(float(area[flat].sum()))
        rock_share.append(float(area[mname == "LK_ROCK"].sum() / max(float(area.sum()), 1e-9)))
    lawn_tot = sum(lawn_each)
    spine = HL.spine_m(D)
    st = []
    for k in range(len(spine) - 1):
        for u in np.linspace(0, 1, 8, endpoint=False):
            st.append(spine[k] * (1 - u) + spine[k + 1] * u)
    st = np.array(st)
    def centre(o):                                   # knolls are baked in world space (identity matrix): their centre is the bounding-box centre
        W = world_xyz(o)
        return [float((W[:, 0].min() + W[:, 0].max()) / 2), float((W[:, 1].min() + W[:, 1].max()) / 2)]
    feat = np.array([centre(o) for o in kn] + [[o.matrix_world.translation.x, o.matrix_world.translation.y] for o in crags]) if (kn or crags) else np.zeros((1, 2))
    near = np.array([float(np.hypot(feat[:, 0] - x, feat[:, 1] - y).min()) for x, y in st])
    share = float((near <= 14.0).mean())
    # round 3 (reviewer: knoll summits read as flat grey patches): the rock cap is a faceted outcrop, not a disc and not a grey mound: rock area is 3-45 % of every knoll
    ok = (len(kn) >= 10 and (min(tops) if tops else 0) >= 0.7 and max(lawn_each or [0.0]) < 10.0 and lawn_tot < 20.0 and len(crags) >= 12 and share >= 0.7
          and bool(rock_share) and min(rock_share) >= 0.03 and max(rock_share) <= 0.45)
    return ok, (f"{len(kn)} DRESS_KNOLL hummocks (R {min([o['knoll_R'] for o in kn] or [0]):.1f}..{max([o['knoll_R'] for o in kn] or [0]):.1f} m, summits {min(tops or [0]):.1f}..{max(tops or [0]):.1f} m over the ridge, "
                f"faceted LK_ROCK outcrop caps = {min(rock_share or [0]):.0%}..{max(rock_share or [0]):.0%} of the hummock area (need 3-45 %), LK_SCRUB flanks; need >= 10 and >= 0.7 m) + {len(crags)} ROCK_CRAG pieces; flat grass-coloured area per hummock at most {max(lawn_each or [0.0]):.1f} m2 (< 10), all {lawn_tot:.1f} m2 (< 20): the ONE_GREEN lawn rule; "
                f"{share:.0%} of the {len(st)} spine stations (every 5 yd) have a knoll or crag within 14 m (need >= 70 %)")


def g_thickets(D, objs):
    """Round 2 (reviewer: 'sparse green dots'): the ridge shrubs grow in thickets. Single-link clusters (4.5 m) of the ridge-side shrubs (boundary chain excluded)."""
    side = HL.mask_ridge_side(D)
    shr = [o for o in objs if o.name.startswith("PLANT_SHRUB") and o.get("h9_class") != "cover"]
    xy = np.array([[o.matrix_world.translation.x, o.matrix_world.translation.y] for o in shr])
    keep = side(xy[:, 0], xy[:, 1])
    xy = xy[keep]
    n = len(xy)
    lab = -np.ones(n, int)
    k = 0
    for i in range(n):
        if lab[i] >= 0:
            continue
        st = [i]
        lab[i] = k
        while st:
            a = st.pop()
            for j in np.nonzero((np.hypot(xy[:, 0] - xy[a, 0], xy[:, 1] - xy[a, 1]) <= 4.5) & (lab < 0))[0]:
                lab[j] = k
                st.append(j)
        k += 1
    sizes = np.bincount(lab) if n else np.zeros(1, int)
    thick = int((sizes >= 3).sum())
    in_thick = float(sizes[sizes >= 3].sum() / max(n, 1))
    ok = thick >= 10 and in_thick >= 0.6
    return ok, (f"{n} ridge-side shrubs (boundary chain excluded) form {k} single-link clusters (4.5 m): {thick} thickets of >= 3 shrubs (need >= 10) hold {in_thick:.0%} of them (need >= 60 %), "
                f"largest {int(sizes.max())}, isolated single shrubs {int((sizes == 1).sum())}")


def geom_hash(objs):
    h = hashlib.sha256()
    n_v, n_f = 0, 0
    for o in sorted(objs, key=lambda o: o.name):
        me = o.data
        co = np.empty(len(me.vertices) * 3)
        me.vertices.foreach_get("co", co)
        h.update(o.name.encode())
        h.update(np.round(np.array(o.matrix_world), 5).tobytes())
        h.update(np.round(co, 5).tobytes())
        lv = np.empty(len(me.loops), np.int64)
        me.loops.foreach_get("vertex_index", lv)
        h.update(lv.tobytes())
        h.update(",".join(sl.material.name if sl.material else "-" for sl in o.material_slots).encode())
        n_v += len(me.vertices)
        n_f += len(me.polygons)
    return h.hexdigest(), n_v, n_f


# ----------------------------------------------------------------------------------------------- main
t0 = time.time()
D = P.start("hole09_design", out_dir="/tmp/hole09_qa_scratch")
D.mod.SCENERY["tee_box_m"] = (10.4, 13.2)
bpy.ops.wm.open_mainfile(filepath=BLEND)
D.terrain = bpy.data.objects[D.terrain_name]
loops = L.terrain_top_loops(D, D.terrain)
D.loops = [[(float(x), float(y)) for x, y in Pl] for Pl, _ in loops]
D.loop_kinds = ["outer" if P.signed_area(np.array(l_)) > 0 else "hole" for l_ in D.loops]
D._distfield = None
objs = export_meshes()
shore_xy = np.array([P.m(x, d) for x, d in D.SHORE] + [P.m(*D.SHORE[0])], float)

ok, d = g_play_unchanged(D)
gate("H_PLAY_UNCHANGED", ok, d)
ok, d = g_ribbon(D, objs)
gate("H_RIBBON_FAIRWAY_SAND_GREEN", ok, d)
ok, d = g_ridge_material(D, objs)
gate("H_RIDGE_SCRUB", ok, d)
ok, d = g_ridge_features(D, objs)
gate("H_RIDGE_PLANTS_ROCKS", ok, d)
ok, d = g_ruin(D, objs)
gate("H_RUIN_MASONRY", ok, d)
ok, d = g_stacks(D, objs)
gate("H_SEA_STACKS_SPIRES", ok, d)
ok, d = g_no_12_vertex_rock("blend", objs)
gate("H_NO_12_VERTEX_ROCK", ok, d)
ok, d = g_no_stretch(objs)
gate("H_NO_STRETCH_2X", ok, d)
ok, d = g_no_plant_on_water("blend", D, objs, shore_xy)
gate("H_NO_PLANT_ON_WATER", ok, d)
v2 = L.v2_gates(D, pockets=L.sea_pockets(D, None))
ok, d = g_foam(objs, v2)
gate("H_NO_FOAM_STRIP", ok, d)
for name, ok_, detail in v2:
    if name in ("SHELF_THIN", "SKIN_NOT_OVER_WATER", "SKIN_FACES_OUTWARD"):
        gate("H_" + name, ok_, detail[:520])
ok, d = g_budget(objs, FBX)
gate("H_BUDGET", ok, d)
ok, d = g_scrub_boundary(D, objs)
gate("H_SCRUB_BOUNDARY_COVERED", ok, d)
ok, d = g_sand_overlay(D, objs)
gate("H_BUNKER_SAND_OVERLAY", ok, d)
ok, d = g_ridge_relief(D, objs)
gate("H_RIDGE_RELIEF", ok, d)
ok, d = g_thickets(D, objs)
gate("H_RIDGE_THICKETS", ok, d)
leg = g_legacy_rules(D, objs, shore_xy)
for k, (ok, d) in leg.items():
    gate("H_LEGACY_" + k, ok, d)
hh, nv, nf = geom_hash(objs)
gate("H_GEOMETRY_HASH", True, f"sha256 {hh} over {len(objs)} mesh objects, {nv} vertices, {nf} faces (names, world matrices, vertex positions rounded 1e-5, loop indices, material names)")

if FBX:
    # FBX re-import into an empty scene: the rock / plant gates again on what Unity will import
    P.start("hole09_design", out_dir="/tmp/hole09_qa_scratch")                     # empties the scene (design handle kept in P._ST)
    D2 = P._ST["D"]
    bpy.ops.import_scene.fbx(filepath=FBX)
    objs2 = [o for o in bpy.data.objects if o.type == 'MESH']
    D2.loops, D2._distfield = D.loops, None
    ok, d = g_no_12_vertex_rock("fbx", objs2)
    gate("H_NO_12_VERTEX_ROCK@fbx", ok, d)
    ok, d = g_no_plant_on_water("fbx", D2, objs2, shore_xy)
    gate("H_NO_PLANT_ON_WATER@fbx", ok, d)
    foam2 = [o.name for o in bpy.data.objects if "FOAM" in o.name.upper()]
    mats2 = sorted({sl.material.name for o in objs2 for sl in o.material_slots if sl.material})
    gate("H_NO_FOAM_STRIP@fbx", not foam2 and not any("FOAM" in m.upper() for m in mats2), f"FBX re-import: {len(objs2)} meshes, *FOAM* objects {foam2 or 'none'}, materials {mats2}")

fails = [r["id"] for r in RES if not r["ok"]]
print(f"HOLE09_QA: {len(RES)} gates, {len(fails)} FAIL {fails} ({time.time() - t0:.0f}s)", flush=True)
if JSON_OUT:
    json.dump(dict(blend=BLEND, fbx=FBX, gates=RES, fails=fails, geom_hash=hh), open(JSON_OUT, "w"), indent=1)
sys.exit(1 if fails else 0)
