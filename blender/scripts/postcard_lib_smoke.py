"""End-to-end self test of postcard_lib.py.

    /Applications/Blender.app/Contents/MacOS/Blender -b --factory-startup --python blender/scripts/postcard_lib_smoke.py \
        -- [--out DIR] [--no-render] [--samples N] [--force-fail]

(--force-fail adds one deliberately failing gate, to show that the process exits 1.)

1. SMOKE: builds postcard_smoke_design.py (hole 90: a needle ribbon, ONE water ellipse swallowing the neck, ONE bunker,
   a green) through every library function (terrain, play surfaces, water, rocks, arch, ruin wall, scatter, gameplay,
   cameras, overview render, .blend, FBX), re-imports the FBX in a CLEAN scene and asserts the contract.
2. STRESS (prefix S_): a large synthetic island (about 180 x 520 yd) with overlapping water ellipses, an ellipse sticking out
   past the shore, a pond inside the land, bunkers that overlap water and sit near the shore; same geometric gates, no FBX.
3. LAVA: MAT_LAVA is created (raw sRGB floats) only when design SCENERY['lava'] is truthy.

Prints one `GATE: NAME PASS|FAIL - detail` line per check and exits non-zero on any failure. It only ever writes into --out
(default: the session scratchpad lib_smoke folder) and refuses an --out inside blender/ or Unity/; hole_07 files are only
read (sha256 compared before and after).
"""
import math
import os
import sys
import time
import types

sys.dont_write_bytecode = True
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import numpy as np          # noqa: E402
import bpy                  # noqa: E402
from mathutils import Vector                    # noqa: E402
from mathutils.bvhtree import BVHTree           # noqa: E402

import postcard_lib as P    # noqa: E402

DEFAULT_OUT = "/private/tmp/claude-501/-Users-adnanyonathan-Documents-Codex-2026-09-20-wh-outputs-iphone-golf-simulator/0ee4ff4a-2e0f-4c17-b498-c813aed3cbc5/scratchpad/lib_smoke"
H07_BLEND_SHA = "a7842f25ca64a1a91be9d881611ff84de63eb1f2e1ad8111330b9834edc4c4af"
H07_FBX_SHA = "813850bff6d107c0424dd9ca22eeaedf1ba9d3fc7ef6e9f0eed54bc67856c6ed"

GATES = []


def gate(name, ok, detail=""):
    GATES.append((name, bool(ok), detail))
    print(f"GATE: {name} {'PASS' if ok else 'FAIL'} - {detail}", flush=True)


def mesh_world(ob):
    """(verts (n,3) world, tris (m,3), poly index per tri) of a mesh object, numpy."""
    me = ob.data
    n = len(me.vertices)
    co = np.empty(n * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(ob.matrix_world)
    w = co @ mw[:3, :3].T + mw[:3, 3]
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    tv = np.empty(nt * 3, dtype=np.int64)
    me.loop_triangles.foreach_get("vertices", tv)
    pi = np.empty(nt, dtype=np.int64)
    me.loop_triangles.foreach_get("polygon_index", pi)
    return w, tv.reshape(-1, 3), pi


def tri_normals(w, t):
    a, b, c = w[t[:, 0]], w[t[:, 1]], w[t[:, 2]]
    n = np.cross(b - a, c - a)
    L = np.maximum(np.linalg.norm(n, axis=1), 1e-12)
    return n / L[:, None], 0.5 * L


def category(name, D):
    if name == D.terrain_name:
        return "TERRAIN"
    if name == "FAIRWAY":
        return "FAIRWAY"
    if name == "FAIRWAY_FIRSTCUT":
        return "FIRSTCUT"
    if name == "GREEN":
        return "GREEN"
    if name == "GREEN_APRON":
        return "APRON"
    if name == "TEE_BOX":
        return "TEE"
    if name.startswith("BUNKER_") and name.endswith("_LIP"):
        return "LIP"
    if name.startswith("BUNKER_"):
        return "SAND"
    return "?"


# a point's top mesh must be one of these for its scoring lie (the bunker lip is the raised rim around the sand: it sits
# on fairway / rough / out-of-bounds cells, exactly as in hole 7)
EXPECT = {"Green": {"GREEN"}, "Fairway": {"FAIRWAY", "TEE", "LIP"}, "Tee": {"TEE", "FAIRWAY", "LIP"},
          "Rough": {"TERRAIN", "FIRSTCUT", "APRON", "LIP"}, "OutOfBounds": {"TERRAIN", "APRON", "LIP"}, "Bunker": {"SAND"}}


def geometry_gates(tag, D, ground, rng, n_ray=3000, expect_pads=None, expect_holes=None):
    """Terrain / ground gates on {name: object} (live or re-imported). `tag` prefixes the gate names."""
    h = D.hole
    pz = D.play_z
    bx0, by0, bx1, by1 = P._shore_bbox_m(D, 12.0)
    terr = ground[D.terrain_name]
    w, t, _ = mesh_world(terr)
    nrm, area = tri_normals(w, t)
    up = nrm[:, 2] > 1e-3
    topz = w[t[up]][:, :, 2]
    gate(f"{tag}TERRAIN_TOP_FLAT", up.sum() > 100 and np.abs(topz - pz).max() <= 1e-3,
         f"{int(up.sum())} upward triangles, z range {topz.min():.4f}..{topz.max():.4f} (play_z {pz})")
    cent = w[t[up]].mean(axis=1)
    inside_shore = P._inside_np([P.m(x, d) for x, d in h.shore], cent[:, 0], cent[:, 1])
    tv = w[t[up]].reshape(-1, 3)
    ell_bad = 0
    for kind, hx, hd, ww, ll, _tag in h.hazards:
        if kind != "water":
            continue
        cx, cy = P.m(hx, hd)
        a, b = ww / 2 * P.YD - 0.05, ll / 2 * P.YD - 0.05
        ell_bad += int((((cent[:, 0] - cx) / a) ** 2 + ((cent[:, 1] - cy) / b) ** 2 < 1).sum())
        ell_bad += int((((tv[:, 0] - cx) / a) ** 2 + ((tv[:, 1] - cy) / b) ** 2 < 1).sum())
    gate(f"{tag}TERRAIN_NO_TOP_INSIDE_WATER_ELLIPSE", ell_bad == 0 and bool(inside_shore.all()),
         f"{ell_bad} top triangle centroids/vertices inside a water ellipse; every top triangle inside the shore: {bool(inside_shore.all())}")
    wall_up = [i for i in np.nonzero(up)[0] if abs(w[t[i]][:, 2].max() - pz) > 1e-3 or abs(w[t[i]][:, 2].min() - pz) > 1e-3]
    gate(f"{tag}TERRAIN_WALLS_NO_UPWARD_FACE", not wall_up,
         f"{len(wall_up)} triangles with an upward normal that are not on the top plane (max wall n.z {float(nrm[~up][:, 2].max()):.4f})")
    wall_v = np.unique(t[~up].ravel())
    low = wall_v[w[wall_v][:, 2] < pz - 0.5]
    sd = P.signed_dist_m(D, w[low][:, 0], w[low][:, 1])
    gate(f"{tag}TERRAIN_WALLS_UNDERCUT_NOT_BULGING", float(sd.max()) <= 0.01,
         f"{len(low)} wall vertices below the lip: max signed distance outside the top outline {float(sd.max()):.4f} m (<= 0: inside the footprint)")
    horiz = (~up) & (np.hypot(nrm[:, 0], nrm[:, 1]) > 0.5)
    idx = np.nonzero(horiz)[0]
    cw = w[t[idx]].mean(axis=1)
    nxy = nrm[idx][:, :2] / np.maximum(np.hypot(nrm[idx][:, 0], nrm[idx][:, 1]), 1e-9)[:, None]
    far = P.signed_dist_m(D, cw[:, 0] + nxy[:, 0] * 0.4, cw[:, 1] + nxy[:, 1] * 0.4)
    near_ = P.signed_dist_m(D, cw[:, 0] - nxy[:, 0] * 0.4, cw[:, 1] - nxy[:, 1] * 0.4)
    frac = float((far >= near_ - 1e-6).mean())
    gate(f"{tag}TERRAIN_WALLS_FACE_OUTWARD", frac > 0.995, f"{frac * 100:.2f}% of {len(idx)} wall triangles face toward the water")
    me = terr.data
    used = sorted({p.material_index for p in me.polygons})
    gate(f"{tag}TERRAIN_BANDED_MATERIALS", len(me.materials) == 3 and used == [0, 1, 2],
         f"slots {[mt_.name for mt_ in me.materials]}, material indices used {used}")
    kinds = D.loop_kinds
    if expect_holes is not None:
        gate(f"{tag}TERRAIN_LOOPS", kinds.count("hole") == expect_holes and kinds.count("outer") >= 1,
             f"{len(D.loops)} boundary loops {[len(L) for L in D.loops]} kinds {kinds} (want {expect_holes} hole loops)")
    # other ground meshes
    z_bad, up_bad = [], []
    for n, o in ground.items():
        if n == D.terrain_name:
            continue
        wv, tvv, _ = mesh_world(o)
        nn, _a = tri_normals(wv, tvv)
        if wv[:, 2].min() < pz - 1e-3 or wv[:, 2].max() > pz + 0.6 + 1e-3:
            z_bad.append(f"{n} z {wv[:, 2].min():.3f}..{wv[:, 2].max():.3f}")
        if (nn[:, 2] <= 0).any():
            up_bad.append(f"{n}: {int((nn[:, 2] <= 0).sum())} tris not facing up")
    gate(f"{tag}GROUND_SURFACES_IN_PLAY_BAND", not z_bad and w[:, 2].max() <= pz + 1e-3 and w[:, 2].min() >= -6.001,
         f"non-terrain ground inside play_z..play_z+0.6: {'ok' if not z_bad else z_bad}; terrain z {w[:, 2].min():.2f}..{w[:, 2].max():.3f}")
    gate(f"{tag}GROUND_SURFACES_FACE_UP", not up_bad, "every triangle of every non-terrain ground mesh faces up (single-sided colliders)"
         if not up_bad else "; ".join(up_bad))

    if expect_pads is not None:                         # connected components of the top surface
        key, ids = {}, []
        for tri in t[up]:
            ids.append([key.setdefault(tuple(np.round(w[vi][:2] * 1000).astype(int)), len(key)) for vi in tri])
        parent = list(range(len(key)))

        def f2(a):
            while parent[a] != a:
                parent[a] = parent[parent[a]]
                a = parent[a]
            return a
        for row in ids:
            r0 = f2(row[0])
            for v in row[1:]:
                parent[f2(v)] = r0
        areas = {}
        for row, a in zip(ids, area[up]):
            areas[f2(row[0])] = areas.get(f2(row[0]), 0.0) + float(a)
        gate(f"{tag}TERRAIN_PADS_SEPARATED", len(areas) == expect_pads and min(areas.values()) > 500 * P.YD ** 2,
             f"{len(areas)} separate top pieces, areas {[round(a / P.YD ** 2) for a in sorted(areas.values())]} yd2 (the ellipse swallows the neck)")

    # ---- ray casts against the scoring lie
    names, verts_all, tris_all, owner, off = [], [], [], [], 0
    for n, o in sorted(ground.items()):
        wv, tvv, _ = mesh_world(o)
        verts_all.append(wv)
        tris_all.append(tvv + off)
        owner.extend([len(names)] * len(tvv))
        names.append(n)
        off += len(wv)
    V = np.vstack(verts_all)
    T = np.vstack(tris_all)
    owner = np.array(owner)
    bvh = BVHTree.FromPolygons([tuple(v) for v in V.tolist()], [tuple(x) for x in T.tolist()], all_triangles=True)

    # rim bands (1 yd around the shore and the water ellipses) are ignored, as the brief of the test says
    pc = D.pc
    sa, sb = [], []
    polys = [h.shore] + [pc.ellipse_pts(k, 180) for k in h.hazards if k[0] == "water"]
    for poly in polys:
        a_ = np.array(poly)
        sa.append(a_)
        sb.append(np.roll(a_, -1, axis=0))
    SA, SB = np.vstack(sa), np.vstack(sb)

    def rim_dist_yd(px, py):
        return P._dist_pts_segs(np.stack([px, py], 1), SA, SB)

    def cast(x, y):
        r = bvh.ray_cast(Vector((x, y, 1000.0)), Vector((0, 0, -1)))
        if r[0] is None:
            return None, None
        return r[0].z, names[owner[r[2]]]
    xs = rng.uniform(bx0, bx1, n_ray)
    ys = rng.uniform(by0, by1, n_ray)
    near_rim = rim_dist_yd(xs / P.YD, ys / P.YD) <= 1.0
    ray_bad = []
    for x, y, skip in zip(xs, ys, near_rim):
        if skip:
            continue
        lie = h.lie_at((x / P.YD, y / P.YD))
        z, nm = cast(float(x), float(y))
        if lie == "Water":
            if z is not None:
                ray_bad.append((round(x / P.YD, 1), round(y / P.YD, 1), lie, nm, round(z, 2)))
        elif z is None or not (pz - 1e-3 <= z <= pz + 0.6 + 1e-3):
            ray_bad.append((round(x / P.YD, 1), round(y / P.YD, 1), lie, nm, None if z is None else round(z, 2)))
    gate(f"{tag}RAY_AGREES_WITH_LIE_AT", not ray_bad,
         f"{int((~near_rim).sum())} random points ({int(near_rim.sum())} within 1 yd of the shore/water rims ignored): land -> hit within "
         f"play_z..play_z+0.6, water/outside shore -> no hit; mismatches {len(ray_bad)} {ray_bad[:4]}")

    # ---- region agreement: points whose 8 neighbours at 0.5 yd share the lie must hit the matching top mesh
    rr = 0.5 * P.YD
    ring = [(np.cos(a_) * rr, np.sin(a_) * rr) for a_ in np.linspace(0, 2 * np.pi, 8, endpoint=False)]
    pts = [(float(x), float(y)) for x, y in zip(rng.uniform(bx0, bx1, 3000), rng.uniform(by0, by1, 3000))]
    targets = [(0.0, 0.0), P.m(*h.pin)] + [P.m(k[1], k[2]) for k in h.hazards]
    for cx, cy in targets:
        for _ in range(350):
            ang, rad = rng.uniform(0, 2 * np.pi), rng.uniform(0, 26 * P.YD)
            pts.append((cx + rad * np.cos(ang), cy + rad * np.sin(ang)))
    for k in h.hazards:
        cx, cy = P.m(k[1], k[2])
        for _ in range(300):
            ang, s_ = rng.uniform(0, 2 * np.pi), rng.uniform(0.85, 1.25)
            pts.append((cx + k[3] / 2 * P.YD * s_ * np.cos(ang), cy + k[4] / 2 * P.YD * s_ * np.sin(ang)))
    cl = h.center
    for _ in range(800):
        i = int(rng.integers(1, len(cl)))
        tt_ = rng.uniform()
        ax, ay = cl[i - 1]
        bx, by = cl[i]
        L = math.hypot(bx - ax, by - ay)
        nx2, ny2 = -(by - ay) / L, (bx - ax) / L
        sgn = 1 if rng.uniform() < 0.5 else -1
        dist = h.fw / 2 + rng.uniform(-3, 3 + h.rough)
        pts.append(((ax + (bx - ax) * tt_ + sgn * nx2 * dist) * P.YD, (ay + (by - ay) * tt_ + sgn * ny2 * dist) * P.YD))
    pa = np.array(pts)
    c0 = P.lie_codes_m(D, pa[:, 0], pa[:, 1])
    uniform = np.ones(len(pa), bool)
    for dx, dy in ring:
        uniform &= P.lie_codes_m(D, pa[:, 0] + dx, pa[:, 1] + dy) == c0
    total = ok = 0
    bad, counts = [], {}
    for i in np.nonzero(uniform)[0]:
        lie = P.LIE_NAMES[c0[i]]
        if lie == "Water":
            continue
        z, nm = cast(float(pa[i, 0]), float(pa[i, 1]))
        total += 1
        counts[lie] = counts.get(lie, 0) + 1
        if nm is not None and category(nm, D) in EXPECT[lie]:
            ok += 1
        elif len(bad) < 6:
            bad.append((round(pa[i, 0] / P.YD, 1), round(pa[i, 1] / P.YD, 1), lie, nm))
    gate(f"{tag}PLAY_REGIONS_MATCH_SCORING_LIE", total > 1500 and ok == total,
         f"{total} interior points (all 8 neighbours at 0.5 yd share the lie), top mesh matches the lie for {ok}; by lie {counts}; first misses {bad}")


def make_stress_design():
    """A big synthetic island with the awkward cases: overlapping water ellipses, an ellipse past the shore, an inland pond,
    bunkers overlapping water and sitting near the shore. Not a playable hole (no design gates are asked of it)."""
    mod = types.ModuleType("postcard_smoke_stress")
    mod.NUMBER, mod.NAME, mod.PAR, mod.PLAY_Z = 92, "Stress", 5, 30.0
    mod.CENTERLINE = [(0.0, 0.0), (8.0, 70.0), (14.0, 150.0), (10.0, 230.0), (2.0, 310.0), (-4.0, 390.0), (-2.0, 470.0)]
    mod.FAIRWAY_WIDTH, mod.GREEN_RADIUS, mod.ROUGH_WIDTH = 18.0, 14.0, 30.0
    cx, cd, a, b = 6.0, 250.0, 100.0, 300.0
    shore = []
    for k in range(200):
        th = 2 * math.pi * k / 200
        r = 1 + 0.05 * math.sin(5 * th + 0.3) + 0.03 * math.sin(9 * th + 1.0)
        shore.append((round(cx + a * r * math.cos(th), 1), round(cd + b * r * math.sin(th), 1)))
    mod.SHORE = shore
    def kiss(i, gap):
        """Circle that comes within `gap` yd of the shore polygon at vertex i (negative: bites that far into the shore)."""
        vx, vy = shore[i]
        px, py = shore[i - 1]
        qx, qy = shore[(i + 1) % len(shore)]
        tx, ty = qx - px, qy - py
        tl = math.hypot(tx, ty)
        nx, ny = ty / tl, -tx / tl
        if nx * (vx - cx) + ny * (vy - cd) < 0:
            nx, ny = -nx, -ny
        ccx, ccy = vx - nx * 6.0, vy - ny * 6.0
        dmin = 1e9
        for k in range(len(shore)):
            ax, ay = shore[k]
            bx, by = shore[(k + 1) % len(shore)]
            ex, ey = bx - ax, by - ay
            u = max(0.0, min(1.0, ((ccx - ax) * ex + (ccy - ay) * ey) / (ex * ex + ey * ey)))
            dmin = min(dmin, math.hypot(ccx - ax - u * ex, ccy - ay - u * ey))
        r = round(dmin - gap, 1)
        return round(ccx, 1), round(ccy, 1), round(2 * r, 1)
    k1 = kiss(12, 0.3)       # a pond with 0.3 yd of land between it and the sea: thin-strip walls must stay inside it
    k2 = kiss(130, -0.3)     # a pond that bites 0.3 yd into the shore: a very narrow neck
    mod.HAZARDS = [
        dict(kind="water", x=k1[0], d=k1[1], width=k1[2], length=k1[2], tag="kiss"),
        dict(kind="water", x=k2[0], d=k2[1], width=k2[2], length=k2[2], tag="bitenarrow"),
        dict(kind="water", x=14.0, d=150.0, width=60.0, length=70.0, tag="lakeA"),
        dict(kind="water", x=36.0, d=190.0, width=44.0, length=52.0, tag="lakeB"),
        dict(kind="water", x=100.0, d=250.0, width=44.0, length=60.0, tag="bite"),
        dict(kind="water", x=-40.0, d=70.0, width=14.0, length=16.0, tag="pond"),
        dict(kind="bunker", x=10.0, d=110.0, width=10.0, length=14.0, tag="b1"),
        dict(kind="bunker", x=-6.0, d=330.0, width=12.0, length=10.0, tag="b2"),
        dict(kind="bunker", x=44.0, d=150.0, width=16.0, length=14.0, tag="overlapsA"),
        dict(kind="bunker", x=88.0, d=330.0, width=10.0, length=12.0, tag="nearShore"),
    ]
    mod.LIMITS = {}
    mod.SCENERY = {}
    return mod


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out = DEFAULT_OUT
    render = "--no-render" not in args
    samples = 16
    if "--out" in args:
        out = args[args.index("--out") + 1]
    if "--samples" in args:
        samples = int(args[args.index("--samples") + 1])
    out = os.path.abspath(out)
    for forbidden in (P.BLENDER_DIR, os.path.join(P.REPO, "Unity")):
        if out == forbidden or out.startswith(forbidden + os.sep):
            print(f"REFUSING --out {out}: the smoke test never writes into {forbidden}")
            return 2
    os.makedirs(out, exist_ok=True)
    before = {p: P.sha256_file(p) for p in (P.HOLE07_BLEND, P.HOLE07_FBX, P.HOLE07_META)}
    t_start = time.time()

    # ------------------------------------------------------------------ build: the short readable sequence
    D = P.start("postcard_smoke_design", out_dir=out)
    P.build_terrain(D)
    P.build_play_surfaces(D)
    P.build_water(D)
    P.import_rocks(D)
    arch = P.build_arch(D, P.m(-3.0, 190.0), 0.0, span_m=9.0, height_m=9.0)
    ruin = P.build_ruin_wall(D, P.m(-8.0, 60.0), P.m(-8.0, 82.0), height_m=2.6)
    stacks = P.scatter_rocks(D, count=10, seed=1, zone="sea")
    foot = P.scatter_rocks(D, count=6, seed=2, zone="foot", min_gap=7.0)
    pin_m = P.m(*D.hole.pin)
    rim = P.scatter_rocks(D, count=3, seed=3, zone="rim", allow=("Rough",), dist=(1.5, 4.0), min_gap=8.0,
                          avoid=[(0.0, 0.0, 12.0), (pin_m[0], pin_m[1], 20.0)])
    P.build_gameplay(D)
    P.build_cameras_and_light(D)
    live_ground = {}
    for ob in bpy.data.objects:
        if ob.type == 'MESH' and ob.name.startswith(P.GROUND_PREFIXES):
            w, t, _ = mesh_world(ob)
            live_ground[ob.name] = (len(ob.data.polygons), float(tri_normals(w, t)[1].sum()))
    pic = P.render_overview(D, samples=samples) if render else None
    if render:                                           # a close look at the arch (smoke only, not part of the library)
        c = P.m(-3.0, 190.0)
        cam = bpy.data.objects.new("CAM_SMOKE_ARCH", bpy.data.cameras.new("CAM_SMOKE_ARCH"))
        bpy.context.scene.collection.objects.link(cam)
        cam.location = (c[0] + 6, c[1] - 34, D.play_z + 7)
        cam.rotation_euler = (Vector((c[0], c[1], D.play_z + 4)) - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()
        cam.data.lens = 28
        P.render_camera("CAM_SMOKE_ARCH", os.path.join(out, "hole_90_arch_closeup.png"), (900, 600), samples)
        bpy.data.objects.remove(cam)
        bpy.context.scene.camera = bpy.data.objects["CAM_HOLE_OVERVIEW"]
    P.save(D)
    fbx = P.export_fbx(D)
    st = P.stats(D)
    t_build = time.time() - t_start
    print(f"[smoke] objects {st['objects']} meshes {st['meshes']} tris {st['tris']} ground_tris {st['ground_tris']}")
    print(f"[smoke] timings {({k: round(v, 2) for k, v in st['timings'].items()})}")
    gate("BUILD_TIME_UNDER_90S", t_build < 90.0, f"build + render + save + export took {t_build:.1f} s")
    gate("OVERVIEW_RENDERED", (not render) or (pic and os.path.getsize(pic) > 20000), f"{pic}")
    n_rocks = len(arch) + len(ruin) + len(stacks) + len(foot) + len(rim)
    gate("ROCK_HELPERS", len(arch) >= 14 and len(ruin) >= 5 and len(stacks) >= 3 and len(foot) >= 1,
         f"arch {len(arch)}, ruin {len(ruin)}, sea stacks {len(stacks)}, cliff-foot {len(foot)}, rim {len(rim)} (total {n_rocks})")

    rng = np.random.default_rng(12345)
    h = D.hole
    bx0, by0, bx1, by1 = P._shore_bbox_m(D, 12.0)
    Xr, Yr = rng.uniform(bx0, bx1, 6000), rng.uniform(by0, by1, 6000)
    codes = P.lie_codes_m(D, Xr, Yr)
    bad = sum(1 for x, y, c in zip(Xr, Yr, codes) if P.LIE_NAMES[c] != h.lie_at((x / P.YD, y / P.YD)))
    gate("LIE_MIRROR_EXACT", bad == 0, f"numpy lie_codes_m vs postcard_check Hole.lie_at at 6000 random points: {bad} mismatches")
    gate("TERRAIN_LOOPS", len(D.loops) == 2 and all(len(L) >= 20 for L in D.loops) and D.loop_kinds == ["outer", "outer"],
         f"{len(D.loops)} boundary loops {[len(L) for L in D.loops]} kinds {D.loop_kinds} (two pads, each one closed loop: shore + ellipse arc)")

    # ------------------------------------------------------------------ re-import the FBX in a CLEAN scene
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
    except Exception as e:                                                     # pragma: no cover
        print("read_factory_settings failed, purging instead:", e)
        P._purge_data()
    bpy.ops.import_scene.fbx(filepath=fbx)
    objs = {o.name: o for o in bpy.data.objects}
    meshes = {n: o for n, o in objs.items() if o.type == 'MESH'}
    gate("FBX_WRITTEN", os.path.getsize(fbx) > 50000 and os.path.exists(fbx + ".meta"),
         f"{fbx} {os.path.getsize(fbx) // 1024} KB, meta present {os.path.exists(fbx + '.meta')}")
    meta_txt = open(fbx + ".meta").read()
    guid = [ln.split(":")[1].strip() for ln in meta_txt.splitlines() if ln.startswith("guid:")][0]
    h07_guid = [ln.split(":")[1].strip() for ln in open(P.HOLE07_META).read().splitlines() if ln.startswith("guid:")][0]
    gate("FBX_META_FRESH_GUID", len(guid) == 32 and guid != h07_guid and "isReadable: 1" in meta_txt and "useSRGBMaterialColor: 1" in meta_txt,
         f"guid {guid} (hole 7 keeps {h07_guid}), isReadable 1, useSRGBMaterialColor 1")
    pz = D.play_z
    px, py = pin_m

    def near(o, v, tol=1e-3):
        return o is not None and (Vector(o.matrix_world.translation) - Vector(v)).length < tol
    mt, mp, mu = objs.get("MARKER_TEE"), objs.get("MARKER_PIN"), objs.get("MARKER_UP")
    gate("MARKERS_WHERE_THE_CONTRACT_SAYS",
         near(mt, (0, 0, pz + 0.24)) and near(mp, (px, py, pz + 0.26)) and near(mu, (0, 0, pz + 50.24)),
         f"MARKER_TEE {tuple(round(v, 3) for v in mt.matrix_world.translation) if mt else None}, MARKER_PIN "
         f"{tuple(round(v, 3) for v in mp.matrix_world.translation) if mp else None} (want {px:.3f},{py:.3f},{pz + .26:.2f}), "
         f"MARKER_UP {tuple(round(v, 3) for v in mu.matrix_world.translation) if mu else None}")
    need = ["HOLE_90_ROOT", "HOLE_CUP", "FLAG_POLE", "FLAG", "TEE_MARKER_1", "TEE_MARKER_2", "BALL_START", "WATER_OCEAN",
            "WATER_SHALLOW", "WATER_FOAM", "FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN", "GREEN_APRON", "TEE_BOX", "BUNKER_01",
            "BUNKER_01_LIP", D.terrain_name]
    miss = [n for n in need if n not in objs]
    gate("CONTRACT_OBJECTS", not miss, f"missing {miss}" if miss else f"all {len(need)} contract objects present")
    stray = [n for n, o in objs.items() if o.type in ('CAMERA', 'LIGHT') or n in P.ROCK_NAMES]
    gate("EXPORT_EXCLUDES_CAMERAS_LIGHTS_LIBRARY", not stray, f"stray {stray}" if stray else "no cameras, lights or library originals in the FBX")
    rock_inst = [n for n in meshes if n.startswith(("ROCK_", "CLIFF_ROCK"))]
    gate("ROCK_INSTANCES_EXPORTED", len(rock_inst) == n_rocks and all(not n.startswith(P.GROUND_PREFIXES) for n in rock_inst),
         f"{len(rock_inst)} rock instances in the FBX (built {n_rocks}), none starts with a ground prefix")
    ground = {n: o for n, o in meshes.items() if n.startswith(P.GROUND_PREFIXES)}
    gate("GROUND_MESHES_SAME_AS_BUILT", set(ground) == set(live_ground),
         f"re-imported ground meshes {sorted(ground)} vs built {sorted(live_ground)}")
    diffs = []
    for n, o in ground.items():
        if n in live_ground:
            w, t, _ = mesh_world(o)
            a = float(tri_normals(w, t)[1].sum())
            pc_, ar = live_ground[n]
            if len(o.data.polygons) != pc_ or abs(a - ar) > 1e-4 * max(ar, 1.0):
                diffs.append(f"{n}: polys {len(o.data.polygons)} vs {pc_}, area {a:.2f} vs {ar:.2f}")
    gate("FBX_ROUNDTRIP_GEOMETRY", not diffs, "; ".join(diffs) if diffs else f"{len(ground)} ground meshes: same polygon counts and areas")
    geometry_gates("", D, ground, rng, expect_pads=2, expect_holes=0)

    from io_scene_fbx import parse_fbx

    def glob(path):
        root, _ = parse_fbx.parse(path)
        d = {}
        for e in root.elems:
            if e.id == b'GlobalSettings':
                for p in e.elems:
                    if p.id == b'Properties70':
                        for q in p.elems:
                            if q.props[0] in (b'UpAxis', b'UpAxisSign', b'FrontAxis', b'FrontAxisSign', b'CoordAxis', b'CoordAxisSign',
                                              b'UnitScaleFactor', b'OriginalUnitScaleFactor'):
                                d[q.props[0]] = q.props[4]
        return d
    g_new, g_old = glob(fbx), glob(P.HOLE07_FBX)
    gate("FBX_GLOBAL_SETTINGS_MATCH_HOLE07", g_new == g_old and len(g_new) == 8, f"new {g_new} == hole_07 {g_old}")
    mat_names = {mt_.name for mt_ in bpy.data.materials}
    want = {"MAT_ROUGH", "MAT_CLIFF", "MAT_CLIFF_DARK", "MAT_FAIRWAY", "MAT_FAIRWAY_STRIPE", "MAT_FIRSTCUT", "MAT_GREEN", "MAT_SAND",
            "MAT_BUNKER_LIP", "MAT_WATER", "MAT_WATER_SHALLOW", "MAT_FOAM", "MAT_ROCK", "MAT_FLAG", "MAT_POLE", "MAT_CUP", "MAT_BALL"}
    gate("FBX_MATERIAL_NAMES", want <= mat_names and "MAT_LAVA" not in mat_names,
         f"missing {sorted(want - mat_names)}, MAT_LAVA absent: {'MAT_LAVA' not in mat_names}")

    # ------------------------------------------------------------------ stress island (live scene, no FBX)
    t0 = time.time()
    D3 = P.start(make_stress_design(), out_dir=out)
    P.build_terrain(D3)
    P.build_play_surfaces(D3)
    P.build_water(D3)
    P.import_rocks(D3)
    P.build_gameplay(D3)
    P.build_cameras_and_light(D3)
    g3 = {o.name: o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(P.GROUND_PREFIXES)}
    t_stress = time.time() - t0
    stress_pic = P.render_overview(D3, samples=8, res=(1000, 1000)) if render else None
    gate("S_BUILD_TIME", t_stress < 60.0, f"stress island built in {t_stress:.1f} s; timings { {k: round(v, 2) for k, v in D3.timings.items()} }")
    geometry_gates("S_", D3, g3, np.random.default_rng(777), n_ray=3000, expect_pads=None, expect_holes=3)
    lips = [n for n in g3 if n.endswith("_LIP")]
    gate("S_ALL_PLAY_MESHES_BUILT", {"FAIRWAY", "FAIRWAY_FIRSTCUT", "GREEN", "GREEN_APRON", "TEE_BOX", "BUNKER_01", "BUNKER_02",
                                     "BUNKER_03", "BUNKER_04"} <= set(g3) and len(lips) >= 2, f"ground meshes {sorted(g3)}")

    # ------------------------------------------------------------------ lava material variant (separate tiny build)
    import postcard_smoke_design as sd_mod
    lava = types.ModuleType("postcard_smoke_lava")
    for k, v in vars(sd_mod).items():
        if k.isupper():
            setattr(lava, k, v)
    lava.NUMBER = 91
    lava.SCENERY = dict(lava=True)
    D2 = P.start(lava, out_dir=out)
    P.build_terrain(D2)
    P.build_water(D2, "WATER_LAVA", "MAT_LAVA", shallow_material=None, foam_material=None, crest=False)
    ml = bpy.data.materials.get("MAT_LAVA")
    bsdf = next(n for n in ml.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bc = tuple(round(v, 4) for v in bsdf.inputs["Base Color"].default_value[:3])
    lv = bpy.data.objects.get("WATER_LAVA")
    gate("LAVA_MATERIAL_RAW_SRGB", bc == tuple(round(v, 4) for v in P.LAVA_RGB) and bsdf.inputs["Emission Strength"].default_value > 0.5
         and lv is not None and lv.data.materials[0].name == "MAT_LAVA" and not lv.name.startswith(P.GROUND_PREFIXES),
         f"MAT_LAVA base colour {bc} (raw floats, not linearised), emission {bsdf.inputs['Emission Strength'].default_value:.1f}, "
         f"mesh {lv.name if lv else None} uses {lv.data.materials[0].name if lv else None}")

    if "--force-fail" in args:
        gate("FORCED_FAILURE", False, "--force-fail was given")
    after = {p: P.sha256_file(p) for p in (P.HOLE07_BLEND, P.HOLE07_FBX, P.HOLE07_META)}
    gate("HOLE07_FILES_UNCHANGED", after == before and after[P.HOLE07_BLEND] == H07_BLEND_SHA and after[P.HOLE07_FBX] == H07_FBX_SHA,
         f"blend {after[P.HOLE07_BLEND][:12]}, fbx {after[P.HOLE07_FBX][:12]}, meta {after[P.HOLE07_META][:12]} (same as before the run and as the README hashes)")
    fails = [n for n, ok_, _ in GATES if not ok_]
    print(f"[smoke] total {time.time() - t_start:.1f} s; outputs in {out}: hole_90.blend, hole_90.fbx(+.meta), hole_90_overview.png, "
          f"hole_90_arch_closeup.png, {os.path.basename(stress_pic) if stress_pic else '-'}")
    print("RESULT:", "ALL PASS" if not fails else f"FAILURES {fails}", flush=True)
    return 0 if not fails else 1


if __name__ == "__main__":
    code = 1
    try:
        code = main()
    except SystemExit as e:
        code = int(e.code or 0)
    except Exception:
        import traceback
        traceback.print_exc()
        code = 1
    sys.stdout.flush()
    sys.exit(code)
