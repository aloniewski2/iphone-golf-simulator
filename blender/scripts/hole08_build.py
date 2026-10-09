"""Hole 8 NEEDLE: scene build (three flat 6 m pads standing in the sea, two water gaps) + the LOOK pass (POSTCARD_LOOK continue, 2026-10-04).

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/hole08_build.py
    ... -- --out DIR    (optional: write the .blend / overview / FBX into DIR instead of the repo; the look build is STAGED this way,
                         DIR = work/postcard-look/v2/hole08/stage, until the Game-view calibration is done)

Writes blender/hole_08.blend, blender/previews/hole_08_overview.png and Unity/Assets/Resources/Course/hole_08.fbx (+ .meta only if missing) -- or the same
three names inside DIR -- prints its own GATE lines (mesh quality, the look gates of hole08_look.py, the library audits) and exits 1 when one fails.
The FBX is written with postcard_look_lib.export_look_fbx (LK_* material names, no texture references), never postcard_lib.export_fbx.

main() is the whole build:
  1. the SCORING build, unchanged since the flat version: terrain, weld, overhang fix, play surfaces, water bands (replaced later by the look pass),
     flat-mesh tidy, bunker lip trim, gameplay markers. Top surfaces / outlines / heights of TERRAIN, FAIRWAY, GREEN, TEE_BOX, BUNKER are bit-identical
     to work/postcard-look/baseline/blender/hole_08.blend (gate H_PLAY_UNCHANGED, compare_baseline.py).
  2. hole08_look.look_pass(D): the look (paths, masonry arch on its outcrop, tapering sea stacks, boulders, plants, strata cliff skin, vines, waterfall,
     thin shelf + sparse surf, UVs) and hole08_look.look_gates(D, info) (the hole's own gates, re-expressed for the library pieces).
     Round 2 (2026-10-04, reviewer findings): hole08_look.mow_pattern (stripe phase on the LK_FAIRWAY faces, cross-mown LK_FAIRWAY tee box: UV + one material
     slot only), re-routed flagstone paths, shorter tufts, shelf preview colour. No vertex of a ground mesh moves.
     Round 3 (2026-10-04, second reviewer round): both flagstone paths re-routed along the ball's left side (hole08_look.PATHS) so the lower phone frames carry stones;
     new gate PATH_CLEAR_OF_BALL. Still no vertex of a ground mesh moves.
     Round 5 (2026-10-04, repair round 1, review of the installed holes): the tee box continues the fairway's UVs (no 90 degree stripe turn = no hard colour line), the tee path runs
     straight along the view across the tee box's front edge (no sideways jump), the strata skin's capping ledges wear turf instead of dark LK_CLIFF, the outcrop's flank hangs vines and
     its rim carries a plant fringe, the bunker lip wears LK_ROUGH; the .blend is saved with absolute texture paths. Gates TEE_SEAM_UV, PATH_SEAM_JUMP, LEDGES_ARE_TURF, BUNKER_LIP_NOT_GREEN,
     OUTCROP_VINES. Still no vertex of a scoring mesh moves (H_PLAY_UNCHANGED).
     Round 6 (2026-10-05, repair round 2, review of the installed round-5 holes): the tee step (hole08_look.step_path_over_plate: the flagstone ribbon is cut along the 10 cm tee plate's outline, the plate part lifted
     onto the plate, a riser between; the TEE_BOX UVs and the plate part of the ribbon are shifted by the 0.35 m band the step hides from the address camera) and the outcrop's camera-side wall (hole08_look.dress_outcrop:
     cut, displaced, turf bank). New gates TEE_SEAM_SEEN / PATH_STEP_CONTINUOUS (ray casts through the tee frame) and ARCH_SLAB_COVERED (ray casts through the arch frame); round 5's plan-view TEE_SEAM_UV is retired.
     Still no vertex of a scoring mesh moves (H_PLAY_UNCHANGED). Only this docstring of hole08_build.py changed in round 6 (the code did not).
     Round 8 (2026-10-05, repair round 0 of the GRASS LIVELINESS + GENTLE SWAY add-on; reviewer findings: the near ground reads as a carpet, a hard polygonal fold in the lower third of the arch shot, the foreground island has
     no plants): hole08_look.look_pass now cheapens the old 117-triangle tuft carpet (PL.cheapen_tufts, near_m 14), scatters the EDGE FRINGE (PL.scatter_fringe: clumps of 3-7 tall / filler tufts along the rough edge of the pads and the cliff
     lips, never on a play surface / the sea / within 4 m of a shot ball), clumps of the same tufts on the arch island (hole08_look.outcrop_fringe), the tufts on the sea-stack crowns (crown_tufts, a verifier-conform wrapper of
     PL.tuftify_crowns), attaches every vine root to its wall (PL.attach_vines; the outcrop vines follow the wall's slope), and dress_outcrop rounds the grass rim of the arch island with a bevelled shoulder + smooth turf normals (gate
     ARCH_FOLD_SOFT). Every PLANT_* library mesh carries the sway colours (Col: R weight, G phase) the libs write; the FBX keeps them (export_look_fbx, colors_type LINEAR). New gates GRASS_FRINGE_PRESENT / _NOT_ON_PLAY_SURFACES /
     _NOT_ON_WATER / _BALL_CLEAR / _TRI_BUDGET, GRASS_OUTCROP_FRINGE, CROWN_TUFTS, VINE_ROOT_GAP, ARCH_FOLD_SOFT (hole08_look.look_gates). No vertex of a scoring mesh moves (H_PLAY_UNCHANGED).
  3. overview render, save, FBX export.
The helpers under it do what the library does not:
  * weld_terrain      the shore meets each water ellipse at four pad corners and the library leaves sub-yard edges there
                      (a shore vertex 0.1-0.4 yd from the crossing, an ellipse vertex 0.05 yd from it). Edges shorter than
                      WELD_M are merged, together with the same edge on every ring of the cliff wall below, so there is no
                      needle triangle or zero-area face; nothing on the boundary moves more than WELD_M / 2 (0.28 yd measured).
  * fix_overhangs     a weld can twist a wall triangle out by a degree or two: pushed back in (walls stay vertical or undercut).
  * split_deep_band   the underwater wall band (z 0 to -6) is cut in two, so no wall triangle is a 6 m sliver.
  * tidy_flat         play-surface meshes are re-triangulated (constrained Delaunay on a welded, simplified boundary, boundary slivers
                      peeled): a flat surface has no needle triangles either.
The flat build's decorative rocks (ROCK_SMALL/MEDIUM/LARGE/CLIFF_ROCK icosahedra, the arch of ROCK_LARGE, the stacked-rock needles, RockField and its
contact / rest gates) are gone; their gates live on in hole08_look.look_gates for the library pieces.
"""
import sys
import os
import math
import random
import hashlib

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np  # noqa: E402
import bpy  # noqa: E402
import bmesh  # noqa: E402
from mathutils import Vector, Matrix  # noqa: E402
from mathutils import geometry as G  # noqa: E402
from mathutils.bvhtree import BVHTree  # noqa: E402
from mathutils.kdtree import KDTree  # noqa: E402

import postcard_lib as P  # noqa: E402
import postcard_look_lib as L  # noqa: E402
import hole08_look as K  # noqa: E402

YD = P.YD
WELD_M = 0.8                # terrain boundary edges shorter than this are welded (0.875 yd)
OVERVIEW_ROT = (50.0, 0.0, 4.0)   # camera tilt, roll, yaw: the hole runs almost straight up the frame (tee at the bottom)
OVERVIEW_RES = (900, 1800)
# mesh name -> (boundary edge length h in metres, boundary sliver peel angle in degrees, weld distance, simplify tolerance);
# the water bands are soft decoration (a 0.35 m simplification is invisible), the play surfaces keep their scoring edges (0.05 m)
FLAT_MESHES = {"FAIRWAY": (2.4, 5.0, 0.25, 0.05), "FAIRWAY_FIRSTCUT": (2.4, 5.0, 0.25, 0.05), "GREEN": (2.4, 5.0, 0.25, 0.05),
               "GREEN_APRON": (2.4, 5.0, 0.25, 0.05), "TEE_BOX": (1.6, 5.0, 0.25, 0.05), "BUNKER_01": (2.0, 5.0, 0.25, 0.05),
               "WATER_SHALLOW": (5.0, 0.0, 0.6, 0.35), "WATER_FOAM": (3.0, 0.0, 0.6, 0.30), "WATER_FOAM_CREST": (1.6, 0.0, 0.3, 0.08)}


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = os.path.abspath(args[args.index("--out") + 1]) if "--out" in args else None
    D = P.start("hole08_design", out_dir=out)
    # --- 1. the scoring build (unchanged since the flat version)
    P.build_terrain(D)                                      # flat top at 6 m, both ellipses cut out, undercut cliffs
    welds, move = weld_terrain(D)                           # no sub-metre edges at the four pad corners
    fix_overhangs(D)
    split_deep_band(D)
    P.build_play_surfaces(D)                                # fairway, first cut, green, apron, tee box, bunker + lip
    P.build_water(D)                                        # flat sea + bands: replaced by the look pass (L.build_sea); kept so the pass sees the same start
    tidy_all_flat(D)                                        # clean triangles on every flat mesh
    trim_lip_off_green(D)
    P.build_gameplay(D)                                     # markers, flag, cup: the scatter masks need them
    # --- 2. the look
    sea_seed = int(args[args.index("--sea-seed") + 1]) if "--sea-seed" in args else None
    info = K.look_pass(D, sea_seed=sea_seed)
    ok = True
    for name, good, detail in K.look_gates(D, info):
        ok &= _gate(name, good, detail)
    ok &= _gate("LOOK_TOP_SURFACES_UNCHANGED", *info["compare_ok"])
    for name, good, detail in L.audit_look(D) + L.v2_gates(D, pockets=L.sea_pockets(D, info["pockets"])):
        if name == "LOOK_WATER_STACK":                      # the library's check wants every WATER_* mesh flat (z 0..0.2): the waterfall sheet is vertical by design
            flat = [o for o in P._export_objects(D) if o.type == 'MESH' and o.name.startswith("WATER_") and not o.name.startswith("WATER_FALL")]
            zs = [(o.name, float(L._mesh_arrays(o)[0][:, 2].min()), float(L._mesh_arrays(o)[0][:, 2].max())) for o in flat if len(o.data.vertices)]
            badz = [z for z in zs if z[1] < -1e-3 or z[2] > 0.2]
            good, detail = not badz, f"{len(zs)} WATER_* meshes except the vertical WATER_FALL_nn sheet inside z 0..0.2 (library audit LOOK_WATER_STACK counts the sheet: LIB_BUGS.md)" + (f"; BAD {badz[:4]}" if badz else "")
        ok &= _gate(name, good, detail)
    ok &= qa_gates(D, move)
    P.build_cameras_and_light(D, overview_rot_deg=OVERVIEW_ROT)
    frame_overview(D, OVERVIEW_RES)
    if "--no-render" not in args:
        P.render_overview(D, res=OVERVIEW_RES)
    # save like P.save but WITHOUT relative_remap (round 5, proof/PROOF.md: LK_TEXTURED@blend failed on the installed blender/hole_08.blend): the LK_* image paths stay the absolute
    # Unity/Assets/Resources/Course/Look/*.png paths they were loaded with, so the .blend finds its textures from the staging folder AND after it is copied to blender/hole_08.blend
    # (paths remapped relative to the staging folder break on the copy)
    blend_path = P._blend_path(D)
    P._guard_target(D, blend_path)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, relative_remap=False)
    L.export_look_fbx(D)                                    # NOT P.export_fbx
    st = P.stats(D)
    print(f"stats: {st['objects']} objects, {st['meshes']} meshes, {st['tris']} triangles ({st['ground_tris']} ground), "
          f"{welds} terrain welds; geometry fingerprint {fingerprint()}")
    if out:
        K.write_info(D, info, os.path.join(out, "look_info.json"))
    if not ok:
        sys.exit(1)


def fingerprint():
    """sha256 of every mesh object's name and 0.1 mm rounded vertex coordinates: two builds must print the same."""
    h = hashlib.sha256()
    for ob in sorted((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: o.name):
        mw = np.array(ob.matrix_world)
        co = np.array([v.co[:] for v in ob.data.vertices]).reshape(-1, 3) @ mw[:3, :3].T + mw[:3, 3]
        h.update(ob.name.encode())
        h.update(np.round(co, 4).tobytes())
    return h.hexdigest()[:16]


# ----------------------------------------------------------------------------- terrain hygiene
def weld_terrain(D, weld_m=WELD_M):
    """Merge the two ends of every terrain top-boundary edge shorter than weld_m (and the same edge on every ring of the
    wall below it) at their midpoint, then re-trace D.loops. Returns (welds, largest vertex move in metres)."""
    ob, pz = D.terrain, D.play_z
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    n_weld, max_move = 0, 0.0
    for _ in range(200):
        bm.edges.ensure_lookup_table()
        best = None
        for e in bm.edges:
            L = e.calc_length()
            if L >= weld_m or (best is not None and L >= best[0]):
                continue
            if all(abs(v.co.z - pz) < 1e-6 for v in e.verts) and any(len(f.verts) == 4 for f in e.link_faces):
                best = (L, e)
        if best is None:
            break
        e = best[1]
        pairs = [(e.verts[0], e.verts[1])]
        cur, ca = e, e.verts[0]
        seen = set()
        while True:                                   # walk down the wall: the same edge on every ring
            q = next((f for f in cur.link_faces if len(f.verts) == 4 and f not in seen), None)
            if q is None:
                break
            seen.add(q)
            bot = next((x for x in q.edges if not (set(x.verts) & set(cur.verts))), None)
            na = next((x.other_vert(ca) for x in q.edges if bot is not None and ca in x.verts and x is not cur
                       and x.other_vert(ca) in bot.verts), None)
            if bot is None or na is None:
                break
            pairs.append((na, bot.other_vert(na)))
            cur, ca = bot, na
        tmap = {}
        for a, b in pairs:
            mid = (a.co + b.co) * 0.5
            max_move = max(max_move, (a.co - mid).length)
            a.co = mid
            b.co = mid
            tmap[b] = a
        bmesh.ops.weld_verts(bm, targetmap=tmap)
        n_weld += 1
        bm.verts.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
    me = ob.data
    me.calc_loop_triangles()
    co = np.array([v.co[:] for v in me.vertices])
    T = np.array([t.vertices[:] for t in me.loop_triangles])
    Tt = T[np.abs(co[T][:, :, 2] - pz).max(1) < 1e-6]
    loops = _boundary_loops(Tt)                                 # land on the left of every loop
    D.loops = [[(float(co[i][0]), float(co[i][1])) for i in lp] for lp in loops]
    D.loop_kinds = ["outer" if P.signed_area(np.array(L)) > 0 else "hole" for L in D.loops]
    D._distfield = None
    return n_weld, max_move


def _boundary_loops(T):
    """Closed boundary loops (vertex index lists) of a counter-clockwise triangle set; the land is on the left."""
    directed = {(a, b) for t in T.tolist() for a, b in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0]))}
    nxt = {a: b for (a, b) in directed if (b, a) not in directed}
    assert len(nxt) == sum(1 for (a, b) in directed if (b, a) not in directed), "pinched terrain boundary"
    loops, seen = [], set()
    for s0 in sorted(nxt):
        if s0 in seen:
            continue
        loop, cur = [], s0
        while cur not in seen:
            seen.add(cur)
            loop.append(cur)
            cur = nxt[cur]
        loops.append(loop)
    return loops


def fix_overhangs(D, step=0.03):
    """Welding twists the wall where two columns with different insets merge (a pad corner): a triangle may then lean out by a
    degree or two. Move the lower vertices of any wall triangle whose normal points up inward until none does (walls stay
    vertical or undercut). Returns the number of vertex moves."""
    me, pz = D.terrain.data, D.play_z
    co = np.array([v.co[:] for v in me.vertices])
    me.calc_loop_triangles()
    T = np.array([t.vertices[:] for t in me.loop_triangles])
    moves = 0
    for _ in range(60):
        a, b, c = co[T[:, 0]], co[T[:, 1]], co[T[:, 2]]
        n = np.cross(b - a, c - a)
        n /= np.maximum(np.linalg.norm(n, axis=1), 1e-12)[:, None]
        wall = np.abs(co[T][:, :, 2] - pz).max(1) > 1e-6
        bad = np.nonzero(wall & (n[:, 2] > 0.004))[0]
        if len(bad) == 0:
            break
        push = {}
        for i in bad:
            h = np.hypot(n[i, 0], n[i, 1])
            if h < 1e-9:
                continue
            zmin = co[T[i]][:, 2].min()
            for v in T[i]:
                if co[v][2] <= zmin + 1e-6:
                    push.setdefault(int(v), []).append(-n[i, :2] / h)
        for v, ds in push.items():
            co[v][:2] += np.mean(ds, axis=0) * step
            moves += 1
    for v, p in zip(me.vertices, co):
        v.co = p
    me.update()
    return moves


def split_deep_band(D):
    """Cut the lowest wall band (waterline z 0 down to z -6, never visible) at z -3: two 3 m quads instead of one 6 m one."""
    bm = bmesh.new()
    bm.from_mesh(D.terrain.data)
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0.0, 0.0, -3.0),
                           plane_no=(0.0, 0.0, 1.0), dist=1e-6)
    bm.to_mesh(D.terrain.data)
    bm.free()
    D.terrain.data.update()


def _evenodd(Pt, A, B):
    out = np.zeros(len(Pt), bool)
    ax, ay, bx, by = A[:, 0][None], A[:, 1][None], B[:, 0][None], B[:, 1][None]
    with np.errstate(divide='ignore', invalid='ignore'):
        for s in range(0, len(Pt), 512):
            px, py = Pt[s:s + 512, 0:1], Pt[s:s + 512, 1:2]
            cond = (ay > py) != (by > py)
            xint = (bx - ax) * (py - ay) / (by - ay) + ax
            out[s:s + 512] = ((cond & (px < xint)).sum(axis=1) & 1).astype(bool)
    return out


def _tri_min_angle(V, T):
    a, b, c = V[T[:, 0]], V[T[:, 1]], V[T[:, 2]]

    def ang(p, q, r):
        u, v = q - p, r - p
        cs = (u * v).sum(1) / np.maximum(np.hypot(u[:, 0], u[:, 1]) * np.hypot(v[:, 0], v[:, 1]), 1e-12)
        return np.degrees(np.arccos(np.clip(cs, -1, 1)))
    return np.minimum(np.minimum(ang(a, b, c), ang(b, c, a)), ang(c, a, b))


def tidy_flat(ob, h, peel=6.0, weld=0.25, simp=0.05, grid=1.7, margin=0.9):
    """Re-triangulate a flat (constant z) triangle mesh. Constraints = the region boundary and the material interfaces; their
    vertices closer than `weld` merge, chains are simplified (RDP `simp`) and cut to edges <= h; interior points sit on a
    grid of grid*h kept margin*h from every constraint; constrained Delaunay; boundary triangles with an angle under `peel`
    degrees are dropped (repeatedly). Face materials are carried over. Returns False when the mesh is not flat."""
    me = ob.data
    me.calc_loop_triangles()
    co = np.array([v.co[:] for v in me.vertices])
    if co[:, 2].max() - co[:, 2].min() > 1e-6:
        return False
    z0 = float(co[0, 2])
    T = np.array([t.vertices[:] for t in me.loop_triangles])
    mat = np.array([me.polygons[t.polygon_index].material_index for t in me.loop_triangles])
    emap = {}
    for k, (a, b, c) in enumerate(T.tolist()):
        for u, v in ((a, b), (b, c), (c, a)):
            emap.setdefault((min(u, v), max(u, v)), []).append(k)
    kind = {}                                         # edge -> 'B' region boundary | 'M' material interface
    for e, ks in emap.items():
        if len(ks) != 2:
            kind[e] = 'B'
        elif mat[ks[0]] != mat[ks[1]]:
            kind[e] = 'M'
    cv = sorted({v for e in kind for v in e})
    kd = KDTree(len(cv))
    for i, v in enumerate(cv):
        kd.insert((co[v][0], co[v][1], 0.0), i)
    kd.balance()
    par = list(range(len(cv)))

    def find(i):
        while par[i] != i:
            par[i] = par[par[i]]
            i = par[i]
        return i
    for i, v in enumerate(cv):
        for (_p, j, _d) in kd.find_range((co[v][0], co[v][1], 0.0), weld):
            ri, rj = find(i), find(j)
            if ri != rj:
                par[rj] = ri
    groups = {}
    for i, v in enumerate(cv):
        groups.setdefault(find(i), []).append(v)
    rep, pts = {}, []
    for g in groups.values():
        p = co[g][:, :2].mean(axis=0)
        for v in g:
            rep[v] = len(pts)
        pts.append(p)
    edges = {}
    for (u, v), k_ in kind.items():
        a, b = rep[u], rep[v]
        if a != b:
            e = (min(a, b), max(a, b))
            if edges.get(e) != 'B':
                edges[e] = k_
    adj = {}
    for a, b in edges:
        adj.setdefault(a, set()).add(b)
        adj.setdefault(b, set()).add(a)
    junction = {n for n, nb in adj.items() if len(nb) != 2}
    seen_e, chains = set(), []

    def walk(start, nxt):
        ch = [start, nxt]
        seen_e.add((min(start, nxt), max(start, nxt)))
        prev, cur = start, nxt
        while cur not in junction and cur != start:
            n2 = [x for x in adj[cur] if x != prev]
            if not n2:
                break
            prev, cur = cur, n2[0]
            e = (min(prev, cur), max(prev, cur))
            if e in seen_e:
                break
            seen_e.add(e)
            ch.append(cur)
        return ch
    for j in sorted(junction):
        for nb in sorted(adj[j]):
            if (min(j, nb), max(j, nb)) not in seen_e:
                chains.append(walk(j, nb))
    for n in sorted(adj):
        for nb in sorted(adj[n]):
            if (min(n, nb), max(n, nb)) not in seen_e:
                chains.append(walk(n, nb))

    def rdp(idx, tol):
        keep = {0, len(idx) - 1}
        stack = [(0, len(idx) - 1)]
        while stack:
            i, j = stack.pop()
            if j <= i + 1:
                continue
            a, b = pts[idx[i]], pts[idx[j]]
            seg = b - a
            L = float(np.hypot(*seg))
            best, bk = -1.0, -1
            for k in range(i + 1, j):
                p = pts[idx[k]]
                d = abs(seg[0] * (p[1] - a[1]) - seg[1] * (p[0] - a[0])) / L if L > 1e-12 else float(np.hypot(*(p - a)))
                if d > best:
                    best, bk = d, k
            if best > tol:
                keep.add(bk)
                stack.append((i, bk))
                stack.append((bk, j))
        return [idx[k] for k in sorted(keep)]
    sub, bnd_edges = set(), []
    for ch in chains:
        k0 = edges[(min(ch[0], ch[1]), max(ch[0], ch[1]))]
        if ch[0] == ch[-1] and len(ch) > 6:
            far = max(range(len(ch)), key=lambda k: float(np.hypot(*(pts[ch[k]] - pts[ch[0]]))))
            ch2 = rdp(ch[:far + 1], simp)[:-1] + rdp(ch[far:], simp)
        else:
            ch2 = rdp(ch, simp) if len(ch) > 2 else ch
        for a, b in zip(ch2[:-1], ch2[1:]):
            k = int(math.ceil(float(np.hypot(*(pts[b] - pts[a]))) / h))
            prev = a
            for j in range(1, k):
                pts.append(pts[a] + (pts[b] - pts[a]) * j / k)
                e = (min(prev, len(pts) - 1), max(prev, len(pts) - 1))
                sub.add(e)
                if k0 == 'B':
                    bnd_edges.append(e)
                prev = len(pts) - 1
            e = (min(prev, b), max(prev, b))
            sub.add(e)
            if k0 == 'B':
                bnd_edges.append(e)
    live = sorted({n for e in sub for n in e})                     # only the vertices a constraint still uses (RDP dropped the rest)
    renum = {n: i for i, n in enumerate(live)}
    Pp = np.array([pts[n] for n in live])
    E = np.array(sorted((renum[a], renum[b]) for a, b in sub))
    Bn = np.array(sorted({(renum[a], renum[b]) for a, b in bnd_edges}))
    BA, BB = Pp[Bn[:, 0]], Pp[Bn[:, 1]]
    bvh = BVHTree.FromPolygons([tuple(c) for c in co.tolist()], [tuple(t) for t in T.tolist()], all_triangles=True)
    sp = grid * h
    x0, y0 = Pp.min(0)
    x1, y1 = Pp.max(0)
    GX, GY = np.meshgrid(np.arange(x0 + sp * 0.37, x1, sp), np.arange(y0 + sp * 0.61, y1, sp), indexing='ij')
    C = np.stack([GX.ravel(), GY.ravel()], 1)
    C = C[_evenodd(C, BA, BB)]
    A_, B_ = Pp[E[:, 0]], Pp[E[:, 1]]
    ab = B_ - A_
    l2 = np.maximum((ab ** 2).sum(1), 1e-12)
    keep = np.zeros(len(C), bool)
    for s in range(0, len(C), 256):
        c = C[s:s + 256]
        t = np.clip(((c[:, None, 0] - A_[None, :, 0]) * ab[None, :, 0] + (c[:, None, 1] - A_[None, :, 1]) * ab[None, :, 1]) / l2[None], 0, 1)
        dx = c[:, None, 0] - (A_[None, :, 0] + ab[None, :, 0] * t)
        dy = c[:, None, 1] - (A_[None, :, 1] + ab[None, :, 1] * t)
        keep[s:s + 256] = np.sqrt(dx * dx + dy * dy).min(1) >= margin * h
    C = C[keep]
    allp = np.vstack([Pp, C]) if len(C) else Pp
    vo, _eo, fo, _a, _b, _c = G.delaunay_2d_cdt([Vector((float(x), float(y))) for x, y in allp],
                                                [(int(a), int(b)) for a, b in E], [], 0, 1e-5, False)
    V = np.array([(v.x, v.y) for v in vo])
    Tn = np.array([list(f) for f in fo], dtype=np.int64).reshape(-1, 3)
    a, b, c = V[Tn[:, 0]], V[Tn[:, 1]], V[Tn[:, 2]]
    ar = (b[:, 0] - a[:, 0]) * (c[:, 1] - a[:, 1]) - (b[:, 1] - a[:, 1]) * (c[:, 0] - a[:, 0])
    Tn[ar < 0] = Tn[ar < 0][:, [0, 2, 1]]                         # counter-clockwise = facing up
    Tn = Tn[np.abs(ar) > 2e-3]
    Tn = Tn[_evenodd(V[Tn].mean(axis=1), BA, BB)]
    newmat = np.array([mat[bvh.find_nearest(Vector((float(x), float(y), z0)))[2]] for x, y in V[Tn].mean(axis=1)])
    for _ in range(40):                                           # peel boundary slivers
        cnt = {}
        for t in Tn.tolist():
            for u, v in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])):
                e = (min(u, v), max(u, v))
                cnt[e] = cnt.get(e, 0) + 1
        onb = np.array([any(cnt[(min(u, v), max(u, v))] == 1 for u, v in ((t[0], t[1]), (t[1], t[2]), (t[2], t[0])))
                        for t in Tn.tolist()])
        bad = onb & (_tri_min_angle(V, Tn) < peel)
        if not bad.any():
            break
        Tn, newmat = Tn[~bad], newmat[~bad]
    used = np.unique(Tn)
    remap = -np.ones(len(V), np.int64)
    remap[used] = np.arange(len(used))
    Vn, Tn = V[used], remap[Tn]
    mats = list(me.materials)
    smooth = any(p.use_smooth for p in me.polygons)
    me.clear_geometry()
    me.from_pydata([(float(x), float(y), z0) for x, y in Vn], [], [tuple(int(i) for i in t) for t in Tn])
    me.materials.clear()
    for m_ in mats:
        me.materials.append(m_)
    for p, mi in zip(me.polygons, newmat):
        p.material_index = int(mi)
        p.use_smooth = smooth
    me.update()
    return True


def trim_lip_off_green(D):
    """The bunker's raised grass lip may not lie on the green disc (the green is flat at +0.26 m): drop lip quads that touch it."""
    for name in [n for n in D.objects if n.endswith("_LIP")]:
        ob = bpy.data.objects[name]
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        pts = np.array([(v.co.x, v.co.y) for v in bm.verts])
        green = P.lie_codes_m(D, pts[:, 0], pts[:, 1]) == P.LIE_GREEN
        bad = [f for f in bm.faces if any(green[v.index] for v in f.verts)]
        bmesh.ops.delete(bm, geom=bad, context='FACES')
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
        bm.to_mesh(ob.data)
        bm.free()
        ob.data.update()


def tidy_all_flat(D):
    done = []
    for name, (h, peel, weld, simp) in FLAT_MESHES.items():
        ob = bpy.data.objects.get(name)
        if ob is not None and tidy_flat(ob, h, peel=peel, weld=weld, simp=simp):
            done.append(name)
    return done


def frame_overview(D, res, margin=1.05):
    """CAM_HOLE_OVERVIEW framed on everything that is built (pads, needles, arch), same view direction as the library's.
    Blender's ortho_scale spans the longer image side."""
    cam = bpy.data.objects["CAM_HOLE_OVERVIEW"]
    R = cam.rotation_euler.to_matrix()
    right, up, view = R @ Vector((1, 0, 0)), R @ Vector((0, 1, 0)), R @ Vector((0, 0, -1))
    pts = []
    for ob in bpy.data.objects:
        if ob.type != 'MESH' or ob.name.startswith("WATER") or ob.hide_render or ob.name in P.ROCK_NAMES:
            continue
        pts += [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    rs = [p.dot(right) for p in pts]
    us = [p.dot(up) for p in pts]
    cr, cu = (min(rs) + max(rs)) / 2, (min(us) + max(us)) / 2
    w, h = res
    span_u, span_r = max(us) - min(us), max(rs) - min(rs)
    cam.data.ortho_scale = (max(span_u, span_r * h / w) if h >= w else max(span_r, span_u * w / h)) * margin
    cam.location = right * cr + up * cu - view * 1500.0


# ----------------------------------------------------------------------------- the build's own gates
def _gate(name, ok, detail):
    print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)
    return ok


def _world_verts(obs):
    out = []
    for ob in obs:
        mw = np.array(ob.matrix_world)
        co = np.array([v.co[:] for v in ob.data.vertices])
        out.append(co @ mw[:3, :3].T + mw[:3, 3])
    return np.vstack(out)


def qa_gates(D, weld_move):
    """Mesh hygiene of the scoring geometry (unchanged gates of the flat build): no needle triangles / zero-area faces on any ground mesh, and the terrain weld."""
    ok = True
    worst = []
    for name in ("TERRAIN_NEEDLE", "FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN", "GREEN_APRON", "TEE_BOX", "BUNKER_01", "BUNKER_01_LIP"):
        me = bpy.data.objects[name].data
        me.calc_loop_triangles()
        co = np.array([v.co[:] for v in me.vertices])
        T = np.array([t.vertices[:] for t in me.loop_triangles])
        a, b, c = co[T[:, 0]], co[T[:, 1]], co[T[:, 2]]
        area = 0.5 * np.linalg.norm(np.cross(b - a, c - a), axis=1)

        def ang(p, q, r):
            u, v = q - p, r - p
            return np.degrees(np.arccos(np.clip((u * v).sum(1) / np.maximum(np.linalg.norm(u, axis=1) * np.linalg.norm(v, axis=1), 1e-12), -1, 1)))
        mn = np.minimum(np.minimum(ang(a, b, c), ang(b, c, a)), ang(c, a, b))
        worst.append((name, float(mn.min()), float(area.min())))
    bad = [w for w in worst if w[1] < 5.0 or w[2] < 0.02]
    ok &= _gate("MESH_QUALITY", not bad, "min triangle angle / min area over every ground mesh (the flat water bands are gone): " +
                ", ".join(f"{n} {a_:.1f} deg {ar:.2f} m2" for n, a_, ar in worst) + (f"; BAD {bad}" if bad else " (limits 5 deg, 0.02 m2)"))
    ok &= _gate("TERRAIN_WELD", weld_move <= 0.5 * YD, f"largest boundary vertex move {weld_move:.3f} m = {weld_move / YD:.2f} yd "
                f"(<= 0.5 yd, SHORE_MATCH measures the result); loops {[len(L_) for L_ in D.loops]}, shortest loop edge "
                f"{min(float(np.min(np.hypot(*(np.roll(np.array(L_), -1, 0) - np.array(L_)).T))) for L_ in D.loops):.2f} m")
    return ok


if __name__ == "__main__":
    main()
