"""hole10_look: the hole-10 wall / crust / cone / UV helpers (POSTCARD_LOOK continue, round 3). Imported by hole10_build.py and by the stage gates.

Library basalt builders only (postcard_props_lib, registered at run time; the lib files are never edited), deterministic seeds.

    register_kinds()               ROCK_WALL_BASALT_N1..N3 / F1..F3: terraced ("ragged") columnar wall segments built from the library's own hex-column primitive
    author_uv(me, rnd, ...)        Basalt_E-aware UV windows: every side face of a column shows one slice of the texture, so the glow sits IN a joint (the face
                                   edge) or the face is glow-free; tops sample glow-free slices; long vertical streaks (low v density)
    uv_variants(kinds, rnd)        4 UV-window clones of each library mesh
    near_wall / far_ring           the crater wall (near terraced wall + the 295-355 yd ring of the brief)
    crust_field(...)               round 3: the cooled crust that covers the crater floor, cut by winding lava rivers (hex-plate chunks ROCK_CRUST_nn)
    build_cone(...)                DRESS_CONE with glowing lava flows down its flanks and a molten summit (LK_BASALT + LK_LAVA faces)
    smoke_group(...)               repair round 1: a stacked DRESS_SMOKE plume (3 big cards facing a proof camera)

Why (reviewer round 2): the wall read as a brown field of orange dashes ('rain'), a flat barcode skyline; the floor was a 45-50 % open orange lake with
floating black rectangles; the volcano was a brown hatched mass without glow.
"""
import math
import json
import random

import numpy as np
import bpy
from mathutils import Vector
from mathutils import noise as mnoise

import postcard_lib as P
import postcard_props_lib as PL
import postcard_look_lib as L
import hole09_stone as ST

MIN_SCALE = 0.58                            # library instances never go below this x their jitter (>= 0.55): the no-stretch gate's 0.5 limit with margin for the FBX float round trip
MAX_SCALE = 1.9                             # x a +-3 % jitter stays under the gate's 2.0
TEE_XY = (0.0, 0.0)
WINDOW = (-10.0, 40.0)            # headings from the tee (deg from +D toward +X) where the walls are kept low so the volcano and its smoke show above them
NEAR_WINDOW_H = (13.0, 18.0)      # m above the lava, near wall inside the window
FAR_WINDOW_H = (15.0, 23.0)       # far ring inside the window
NEAR_R_MIN_YD = 214.0             # the near wall never comes closer to the crater centre than this ...
NEAR_SHORE_GAP_YD = 30.0          # ... nor closer than this to the farthest shore point of its heading
NEAR_STACK_GAP_M = 16.0           # radial clearance to a stack's base (a stack is never inside the wall)
NEAR_ROW_GAP_M = 9.5              # second row, further out
N_VARIANTS = 4                    # UV-window clones per wall kind
FAR_SPIRES = 7                    # tall spire segments (scale up to 1.9, ~80 m) breaking the far skyline
YD = 0.9144

# natural wall segments (library hex-column primitive, registered at run time): kind, length, depth, height, col_r, seed, front_ratio, broken, low_mid, terraces per height
NEAR_KINDS = (("ROCK_WALL_BASALT_N1", 22.0, 7.0, 24.0, 1.7, 31, 0.50, 0.14, 0.0, 10.0),
              ("ROCK_WALL_BASALT_N2", 26.0, 8.0, 28.0, 2.0, 32, 0.45, 0.16, 0.20, 10.0),
              ("ROCK_WALL_BASALT_N3", 18.0, 6.0, 20.0, 1.5, 33, 0.55, 0.14, 0.0, 9.0),
              ("ROCK_WALL_BASALT_F1", 40.0, 12.0, 44.0, 2.6, 61, 0.55, 0.12, 0.0, 12.0),
              ("ROCK_WALL_BASALT_F2", 36.0, 11.0, 40.0, 2.4, 62, 0.60, 0.14, 0.12, 12.0),
              ("ROCK_WALL_BASALT_F3", 30.0, 10.0, 34.0, 2.2, 63, 0.50, 0.12, 0.0, 11.0))
FAR_ROWS = ((0.45, (34.0, 50.0), 1.0, 1.0), (1.0, (46.0, 62.0), 1.0, 0.95))      # (t, heights m above lava, share of segments, spacing x width)


# ----------------------------------------------------------------------------- ragged (terraced) wall segments from the library's hex-column primitive
def ragged_wall(kind, length, depth, height, col_r, seed, front_ratio, broken, low_mid, terr, bury=6.0, end_drop=0.12, max_faces=500):
    """Like postcard_props_lib.build_basalt_wall (same hex-column primitive, same lattice, same <= 500 faces) but the skyline is TERRACED: heights follow a
    low-frequency noise (+-10 %) plus a few % per column (neighbouring columns differ by 1-2 m: a stair-step slope, not a fence of equal posts), broken
    groups drop by 10 %, tops are tilted up to 0.42 rad and chipped two times in three: ragged crags, not flat-topped skyscrapers (round 2 read as a city)."""
    rng = random.Random(seed * 6151 + 29)
    cs = PL._hex_lattice(col_r, -length / 2, -depth / 2, length / 2, depth / 2, rng, rows_x=True)
    cs.sort(key=lambda p: (abs(p[0]) / length + 0.3 * rng.random()))
    mb = PL._MB(("LK_BASALT",))
    q = height / terr
    for x, y in cs:
        if len(mb.F) + 10 > max_faces:
            break
        fy = (y + depth / 2) / depth
        h = height * (front_ratio + (1 - front_ratio) * fy)
        h *= 1.0 + 0.10 * mnoise.noise(Vector((x / (length * 0.30) + seed * 3.7, fy * 0.8, 0.5)))
        ex = abs(x) / (length / 2)
        if ex > 0.7:
            h *= 1.0 - end_drop * min(1.0, (ex - 0.7) / 0.3) ** 1.5
        if low_mid > 0:
            h *= 1.0 - low_mid * math.exp(-((x / (length * 0.22)) ** 2))
        if mnoise.noise(Vector((x / (col_r * 3.0) + seed * 1.3, y / (col_r * 3.0), 2.5))) > 0.55 - broken:
            h *= 0.90
        h = max(1.5 * q, h * (1.0 + rng.uniform(-0.035, 0.035)))
        PL._hex_column(mb, rng, x, y, h, -bury, col_r, tilt_max=0.42, chip_p=0.65, a_off=math.pi / 6)
    return PL._finalize(kind, mb, "box", smooth_angle=None)


def register_kinds():
    for kind, ln, dp, ht, cr, seed, fr, br, lm, terr in NEAR_KINDS:
        PL.BUILDERS[kind] = (lambda kind=kind, ln=ln, dp=dp, ht=ht, cr=cr, seed=seed, fr=fr, br=br, lm=lm, terr=terr:
                             ragged_wall(kind, ln, dp, ht, cr, seed, fr, br, lm, terr))


# ----------------------------------------------------------------------------- UV windows (Basalt_C / Basalt_E aware)
# Basalt_E (8 m tile, u around, v = height): glow only in vertical joint lines at u = GLOW_U (cores 0.010 wide, 45-68 % of the height in irregular stretches) and
# the fork / T-junction cracks that branch off them (the texture-finish pass of 2026-10-04, "v5": the fork cracks spread glow over +-0.05 of u around each line).
# A column face is mapped to ONE slice of the tile: a "glow" slice starts (or ends) exactly on a glow line, so the glow runs along the face EDGE, i.e. in the
# joint between two columns; every other face takes a slice from a glow-FREE zone (a u interval whose columns hold no glow texel at all, over the whole height,
# measured with a 2 px margin: the v5 forks closed 6 of the 8 round-3 zones). Tops take glow-free slices too. The v density is low (tall streaks, not 0.5-1.5 m rain).
# All numbers measured from Basalt_E.png / Basalt_C.png (v6 tile, repair round 1 2026-10-04 21:41: Basalt_E sha256 d9f18f5b1c2837ba.., Basalt_C ecfd1e968968c3b0..; the six glow cores and the five glow-free zones are the SAME as v5, only the Basalt_C luminance profile moved by up to 0.023; work/postcard-look/v2/hole10/r3/basalt_zones_v5.py
# and basalt_profile128_v5.py); `texture_measure()` re-measures them from the PNGs at build time (H_UV_WINDOWS fails if the texture moved away from these numbers).
GLOW_U = (0.123, 0.446, 0.534, 0.718, 0.806, 0.930)
GLOW_EDGE = 0.016
ZONES = ((0.009, 0.061), (0.165, 0.385), (0.576, 0.685), (0.772, 0.796), (0.854, 0.893))
ZONE_W = np.array([b - a for a, b in ZONES])
# Basalt_C column-mean luminance in 128 bins of u (bin = 8 px; texture mean 0.1473; v6 values, repair round 1): a slice is drawn with weight (0.14 / lum)^3, so the walls come out darker, not lighter,
# than the texture average (the zone is drawn first, then the window position inside it)
PROFILE128 = (0.143, 0.142, 0.143, 0.145, 0.135, 0.121, 0.120, 0.112, 0.065, 0.053, 0.119, 0.184, 0.194, 0.190, 0.139, 0.108, 0.114, 0.158, 0.194, 0.234, 0.228, 0.221, 0.217, 0.209, 0.188, 0.174, 0.131, 0.058, 0.074, 0.124, 0.131, 0.136,
              0.155, 0.184, 0.181, 0.177, 0.178, 0.181, 0.184, 0.185, 0.183, 0.167, 0.149, 0.146, 0.110, 0.051, 0.069, 0.116, 0.149, 0.175, 0.179, 0.178, 0.178, 0.176, 0.159, 0.146, 0.111, 0.097, 0.130, 0.160, 0.200, 0.221, 0.220, 0.219,
              0.217, 0.203, 0.181, 0.127, 0.100, 0.084, 0.101, 0.107, 0.139, 0.150, 0.150, 0.147, 0.146, 0.142, 0.129, 0.105, 0.064, 0.086, 0.175, 0.216, 0.275, 0.305, 0.306, 0.301, 0.291, 0.248, 0.193, 0.123, 0.091, 0.109, 0.111, 0.142,
              0.154, 0.154, 0.153, 0.152, 0.141, 0.125, 0.092, 0.088, 0.105, 0.152, 0.168, 0.170, 0.166, 0.146, 0.110, 0.049, 0.044, 0.095, 0.114, 0.111, 0.115, 0.099, 0.081, 0.080, 0.093, 0.093, 0.095, 0.098, 0.109, 0.138, 0.146, 0.144)


def win_lum(u0, su):
    """Mean Basalt_C luminance of the window [u0, u0 + su] (9 samples of PROFILE128, u wraps)."""
    return sum(PROFILE128[int(((u0 + su * (k + 0.5) / 9.0) % 1.0) * 128) % 128] for k in range(9)) / 9.0


ZONE_LUM = tuple(win_lum(a, b - a) for a, b in ZONES)
ZONE_P = [(0.14 / l) ** 3 for l in ZONE_LUM]


def pick_zone(rnd, ok):
    w = [ZONE_P[k] for k in ok]
    r = rnd.random() * sum(w)
    for k, wk in zip(ok, w):
        r -= wk
        if r <= 0:
            return k
    return ok[-1]


def pick_u0(rnd, lo, hi, su, n=8):
    """Window start inside the zone [lo, hi]: n evenly spaced candidates, drawn with weight (0.14 / window luminance)^3 (one random number)."""
    span = max(hi - lo - su, 0.0)
    if span < 1e-6:
        rnd.random()
        return lo
    cand = [lo + span * (k + 0.5) / n for k in range(n)]
    w = [(0.14 / max(win_lum(c, su), 0.03)) ** 3 for c in cand]
    r = rnd.random() * sum(w)
    for c, wc in zip(cand, w):
        r -= wc
        if r <= 0:
            return c
    return cand[-1]


_TEX_CACHE = {}


def texture_measure():
    """Re-measure the v5 numbers above from the PNGs on disk (Blender image decode): glow share of every glow-free zone, glow share of an edge window at every glow
    line, the mean Basalt_C luminance of the zones, and the max deviation of PROFILE128. Returns a dict (cached)."""
    if _TEX_CACHE:
        return _TEX_CACHE
    import os
    d = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Unity", "Assets", "Resources", "Course", "Look"))

    def load(n):
        im = bpy.data.images.load(os.path.join(d, n), check_existing=False)
        w, h = im.size
        px = np.empty(w * h * 4, np.float32)
        im.pixels.foreach_get(px)
        a = px.reshape(h, w, 4)[:, :, :3].copy()
        bpy.data.images.remove(im)
        return a, w, h
    E, w, h = load("Basalt_E.png")
    C, _, _ = load("Basalt_C.png")
    G = E.max(axis=2) > 0.2                                                  # the stage gate's glow definition
    lum = C.mean(axis=2)
    prof = lum.mean(axis=0)[: (w // 128) * 128].reshape(128, -1).mean(axis=1)

    def share(a, b):
        cols = [i % w for i in range(int(round(a * w)), int(round(b * w)))]
        return float(G[:, cols].mean())
    zshare = [share(a, b) for a, b in ZONES]
    eshare = []
    for g in GLOW_U:                                                         # the narrowest edge window a glow face can get (0.05 u) on either side of each line
        eshare.append(min(share(g - GLOW_EDGE, g - GLOW_EDGE + 0.05), share(g + GLOW_EDGE - 0.05, g + GLOW_EDGE)))
    _TEX_CACHE.update(zone_glow_max=max(zshare), zone_glow=zshare, edge_glow_min=min(eshare), edge_glow=eshare, lum_mean=float(lum.mean()),
                      profile_dev=float(np.abs(prof - np.array(PROFILE128)).max()), glow_mean=float(G.mean()))
    return _TEX_CACHE
TILE = 8.0                                  # LK_BASALT contract tile (m)
CONE_UV = 0.42                              # the volcano's basalt UV density x the 8 m tile (UV_DENSITY gate: >= 1/3): bigger, sparser strata and dashes


def _polys(me):
    nl = len(me.loops)
    vidx = np.empty(nl, np.int64)
    me.loops.foreach_get("vertex_index", vidx)
    ls = np.empty(len(me.polygons), np.int64)
    lt = np.empty(len(me.polygons), np.int64)
    me.polygons.foreach_get("loop_start", ls)
    me.polygons.foreach_get("loop_total", lt)
    co = np.empty(len(me.vertices) * 3, np.float64)
    me.vertices.foreach_get("co", co)
    return vidx, ls, lt, co.reshape(-1, 3)


def _newell(pts):
    n = np.zeros(3)
    for i in range(len(pts)):
        a, b = pts[i], pts[(i + 1) % len(pts)]
        n[0] += (a[1] - b[1]) * (a[2] + b[2])
        n[1] += (a[2] - b[2]) * (a[0] + b[0])
        n[2] += (a[0] - b[0]) * (a[1] + b[1])
    ln = float(np.linalg.norm(n))
    return n / ln if ln > 1e-12 else np.array([0.0, 0.0, 1.0])


_UV_GLOW_CACHE = {}


def _face_glow(uv):
    """Actual decoded emission samples; intended window placement is not evidence."""
    if not _UV_GLOW_CACHE:
        import os
        path=os.path.join(os.path.dirname(__file__),'../../Unity/Assets/Resources/Course/Look/Basalt_E.png')
        im=bpy.data.images.load(os.path.abspath(path),check_existing=True)
        w,h=im.size;px=np.empty(w*h*4,np.float32);im.pixels.foreach_get(px)
        rng=np.random.default_rng(1010);r=rng.random((96,2));q=np.sqrt(r[:,0])
        _UV_GLOW_CACHE.update(G=px.reshape(h,w,4)[:,:,:3].max(2)>.2,w=w,h=h,bary=np.c_[1-q,q*(1-r[:,1]),q*r[:,1]])
    c=_UV_GLOW_CACHE;lit=total=0
    for i in range(1,len(uv)-1):
        pts=c['bary']@np.array([uv[0],uv[i],uv[i+1]])
        x=np.floor((pts[:,0]%1)*c['w']).astype(int)%c['w'];y=np.floor((pts[:,1]%1)*c['h']).astype(int)%c['h']
        lit+=int(c['G'][y,x].sum());total+=len(x)
    return lit/max(total,1)


def author_uv(me, rnd, uf=0.62, vf=0.42, p_glow=0.30, top_vf=0.60, slot=None):
    """Rewrite UV0 of a mesh (object space metres): see the section comment. uf / vf = u / v density x the 8 m tile (the UV_DENSITY gate wants the
    geometric mean in 1/3..3 and the anisotropy <= 4:1), p_glow = share of side faces whose window carries a glow line at its edge. slot = only polygons of
    that material slot (None = all). Returns dict(side, glow, top, top_glow_free) counters. The tangent frame stays right-handed (u = z x n for sides,
    u = +x / v = +y for tops), so the normal map is not mirrored."""
    vidx, ls, lt, co = _polys(me)
    lay = me.uv_layers.active or me.uv_layers.new(name="UVMap")
    nl = len(me.loops)
    try:
        uv = np.empty(nl * 2, np.float64)
        lay.uv.foreach_get("vector", uv)
        uv = uv.reshape(-1, 2)
    except Exception:
        uv = np.zeros((nl, 2))
    mi = np.zeros(len(me.polygons), np.int32)
    me.polygons.foreach_get("material_index", mi)
    st = dict(side=0, glow=0, top=0, top_glow_free=0)
    for pi in range(len(me.polygons)):
        if slot is not None and mi[pi] != slot:
            continue
        l0, n = int(ls[pi]), int(lt[pi])
        pts = co[vidx[l0:l0 + n]]
        nrm = _newell(pts)
        if abs(nrm[2]) >= 0.80:                                             # a TOP (chamfers and chips are steep enough to count as sides): a glow-free slice, v long, u compressed to the zone
            c = pts.mean(0)
            d = pts - c
            rx = max(float(np.abs(d[:, 0]).max()), 0.05)
            ok = [k for k in range(len(ZONES)) if ZONE_W[k] >= (0.150 if rx > 2.2 else 0.060)]        # big tops take the widest glow-free zones
            k = pick_zone(rnd, ok)
            lo, hi = ZONES[k]
            zw = hi - lo
            du = min(uf / TILE, 0.9 * zw / (2.0 * rx))
            half = rx * du
            zc = pick_u0(rnd, lo, hi, 2.0 * half) + half                      # centre of the cap's slice (one random number): the darker spots of the zone are favoured
            uv[l0:l0 + n, 0] = zc + d[:, 0] * du
            uv[l0:l0 + n, 1] = rnd.random() + d[:, 1] * min(top_vf / TILE, 3.0 * du)         # anisotropy <= 3:1
            st["top"] += 1
            st["top_glow_free"] += int(_face_glow(uv[l0:l0+n]) <= 2e-4)
            continue
        h = np.array([nrm[0], nrm[1], 0.0])
        hl = float(np.linalg.norm(h))
        h = h / hl if hl > 1e-9 else np.array([1.0, 0.0, 0.0])
        t = np.array([-h[1], h[0], 0.0])                                    # z x h
        s = pts @ t
        w = max(float(s.max() - s.min()), 0.05)
        su = w * uf / TILE
        glow = rnd.random() < p_glow
        if glow:
            g = GLOW_U[int(rnd.random() * len(GLOW_U)) % len(GLOW_U)]
            u0 = g - GLOW_EDGE if rnd.random() < 0.5 else g + GLOW_EDGE - su
            su_eff = su
        else:
            ok = [k for k in range(len(ZONES)) if ZONE_W[k] >= su * 1.03]
            if ok:
                k = pick_zone(rnd, ok)
                su_eff = su
            else:
                k = int(np.argmax(ZONE_W))
                su_eff = 0.96 * float(ZONE_W[k])
            lo, hi = ZONES[k]
            u0 = pick_u0(rnd, lo, hi, su_eff)
        uv[l0:l0 + n, 0] = u0 + (s - s.min()) / w * su_eff
        uv[l0:l0 + n, 1] = pts[:, 2] * (vf / TILE) + rnd.random()
        st["side"] += 1
        st["glow"] += int(_face_glow(uv[l0:l0+n]) > 0)
    try:
        lay.uv.foreach_set("vector", uv.ravel())
    except Exception:
        lay.data.foreach_set("uv", uv.ravel())
    me.update()
    return st


UV_STATS = {}                                # variant kind -> counters written by author_uv (read by the build's gate)
UV_PARAMS = ((0.62, 0.36), (0.70, 0.32), (0.58, 0.38), (0.66, 0.34), (0.60, 0.36))


def uv_variants(kinds, rnd, p_glow=0.30):
    """{kind: [variant kinds]}: clones of the library meshes with their UVs authored by author_uv (own random windows, own density factors inside the
    UV_DENSITY gate's 1/3..3 x). Same shape, same materials, no mirroring."""
    out = {}
    for kind in kinds:
        src = PL.library_object(kind)
        vs = []
        for k in range(N_VARIANTS):
            vk = f"{kind}_V{k + 1}"
            me = src.data.copy()
            me.name = vk
            me["lk_kind"] = vk
            uf, vf = UV_PARAMS[(k + len(vs) + len(out)) % len(UV_PARAMS)]
            UV_STATS[vk] = dict(author_uv(me, rnd, uf=uf, vf=vf, p_glow=p_glow), uf=uf, vf=vf)
            ob = bpy.data.objects.new(vk, me)
            PL._lib_collection().objects.link(ob)
            ob.hide_viewport = ob.hide_render = True
            vs.append(vk)
        out[kind] = vs
    return out


_BAGS = {}


def pick_variant(kinds_var, kind, rnd):
    """Next UV variant of `kind` from a shuffled bag (every variant is used once before any is used twice: a balanced, deterministic spread)."""
    bag = _BAGS.get(kind)
    if not bag:
        bag = list(kinds_var[kind])
        rnd.shuffle(bag)
        _BAGS[kind] = bag
    return bag.pop()


def macro(theta, phases, amps=(0.26, 0.20, 0.15, 0.10, 0.06), ks=(2, 3, 5, 8, 13)):
    """Low-frequency height multiplier along the ring (1 +- ~0.55): ridges and saddles instead of one level fence. Clipped to [0.55, 1.45]."""
    m = 1.0 + sum(a * math.sin(k * theta + p) for a, k, p in zip(amps, ks, phases))
    return min(1.45, max(0.55, m))


def heading_from_tee(x, y):
    return math.degrees(math.atan2(x - TEE_XY[0], y - TEE_XY[1]))


def near_radius_profile(D, W, n=720):
    """R(theta) (m from the crater centre) of the near wall's front row: max(R_MIN, farthest shore point of the heading + gap, a pushed-out bay round each
    stack), made an upper envelope (max-filter) and then box-smoothed with a window not larger than the max-filter's, so the smoothed value never drops
    below a constraint."""
    cx, cy = P.m(W["cx"], W["cd"])
    th = np.linspace(0.0, 2 * math.pi, n, endpoint=False)
    con = np.full(n, NEAR_R_MIN_YD * P.YD)
    for x, d in D.SHORE:
        px, py = P.m(x, d)
        a = math.atan2(py - cy, px - cx) % (2 * math.pi)
        r = math.hypot(px - cx, py - cy) + NEAR_SHORE_GAP_YD * P.YD
        i0 = int(round(a / (2 * math.pi) * n))
        for k in range(-24, 25):
            j = (i0 + k) % n
            con[j] = max(con[j], r)
    for s in D.scenery["stacks"]:
        px, py = P.m(s["x"], s["d"])
        a = math.atan2(py - cy, px - cx) % (2 * math.pi)
        dist = math.hypot(px - cx, py - cy)
        rad = s["r"] * P.YD + 8.0                                          # base radius + the satellites standing beside the main piece
        for j in range(n):
            lat = abs((th[j] - a + math.pi) % (2 * math.pi) - math.pi) * dist
            if lat < rad + 60.0:
                con[j] = max(con[j], dist + rad + NEAR_STACK_GAP_M - 0.55 * max(0.0, lat - rad - 6.0))
    w_max, w_box = 30, 24
    pad = np.concatenate([con[-w_max:], con, con[:w_max]])
    mx = np.array([pad[i:i + 2 * w_max + 1].max() for i in range(n)])
    pad2 = np.concatenate([mx[-w_box:], mx, mx[:w_box]])
    ker = np.ones(2 * w_box + 1) / (2 * w_box + 1)
    sm = np.convolve(pad2, ker, mode="valid")[:n]
    sm = np.maximum(sm, con)                                              # numerical safety (never below a constraint)
    return th, sm


NEAR_ROWS = ((0.0, (26.0, 40.0), 0.06), (NEAR_ROW_GAP_M, (36.0, 52.0), 0.0))     # (radial offset m, heights m above the lava, gap share)


def near_wall(D, W, kinds_var):
    """Two terraced rows of tangent library wall segments (ROCK_WALL_BASALT_N* / F2 / F3) round the crater, continuous (the second row behind the first, 6 % gaps in the
    front row only), heights = macro(theta) x (row range), low inside the tee shot's window. Returns the instances."""
    rnd = random.Random(1020)
    cx, cy = P.m(W["cx"], W["cd"])
    th_p, R_p = near_radius_profile(D, W)
    phases = [rnd.uniform(0, math.tau) for _ in range(5)]
    kinds = ["ROCK_WALL_BASALT_N1", "ROCK_WALL_BASALT_N2", "ROCK_WALL_BASALT_N3", "ROCK_WALL_BASALT_F2", "ROCK_WALL_BASALT_F3"]
    dims = {}
    for k in kinds:
        lo, hi = PL.lib_bbox(k)
        dims[k] = (float(hi.x - lo.x), float(hi.y - lo.y), float(hi.z))
    out = []
    for ri, (dr, (hlo, hhi), gap) in enumerate(NEAR_ROWS):
        th = rnd.uniform(0.0, 0.3) + ri * 0.11
        th_end = th + 2 * math.pi
        while th < th_end:
            Rm = float(np.interp(th % (2 * math.pi), th_p, R_p, period=2 * math.pi)) + dr + rnd.uniform(-1.2, 1.2)
            h = min(58.0, 0.5 * (hlo + hhi) * rnd.uniform(0.93, 1.07) * macro(th, phases))
            kind = kinds[rnd.randrange(len(kinds))]
            xl, yl, zt = dims[kind]
            tc = th + 0.5 * (xl * 0.7) / Rm
            hd = heading_from_tee(cx + Rm * math.cos(tc), cy + Rm * math.sin(tc))
            if WINDOW[0] <= hd <= WINDOW[1]:
                h = min(h, rnd.uniform(*NEAR_WINDOW_H))
            s = min(1.7, max(MIN_SCALE, h / zt))
            width = xl * s
            dth = width * 0.93 / Rm
            if rnd.random() < gap:
                th += dth
                continue
            tc = th + 0.5 * dth
            Rm2 = float(np.interp(tc % (2 * math.pi), th_p, R_p)) + dr
            vk = pick_variant(kinds_var, kind, rnd)
            yaw = tc - math.pi / 2 + rnd.uniform(-0.05, 0.05)
            sc3 = (s * rnd.uniform(0.97, 1.03), s * rnd.uniform(0.97, 1.03), s)
            out.append(PL.place(D, vk, (cx + Rm2 * math.cos(tc), cy + Rm2 * math.sin(tc), 0.0), yaw=yaw, scale=sc3))
            th += dth
    return out, th_p, R_p


def far_ring(D, W, kinds_var):
    """The 295-355 yd ring (brief), two rows: a middle row (34-50 m) and a tall outer row (46-62 m), uniform scales (a few % jitter), a macro skyline (ridges and
    saddles), a handful of spire segments, a low window in the tee shot's headings. Segments are the terraced F1 / F2 / F3 kinds."""
    rnd = random.Random(1010)
    cx, cy = P.m(W["cx"], W["cd"])
    r0, r1 = W["r_inner"] * P.YD, W["r_outer"] * P.YD
    phases = [rnd.uniform(0, math.tau) for _ in range(5)]
    seg_kinds = ("ROCK_WALL_BASALT_F1", "ROCK_WALL_BASALT_F2", "ROCK_WALL_BASALT_F3")
    dims = {}
    for k in seg_kinds:
        lo, hi = PL.lib_bbox(k)
        dims[k] = (float(hi.x - lo.x), float(hi.y - lo.y), float(hi.z))
    out = []
    spire_at = sorted(rnd.uniform(0.0, 2 * math.pi) for _ in range(FAR_SPIRES))
    for k_row, (t, (hlo, hhi), seg_share, spacing) in enumerate(FAR_ROWS):
        r = r0 + (r1 - r0) * t
        th = rnd.uniform(0.0, 0.2) + k_row * 0.07
        th_end = th + 2 * math.pi
        while th < th_end:
            rr = r + rnd.uniform(-6.0, 6.0)
            h = 0.5 * (hlo + hhi) * rnd.uniform(0.93, 1.07) * macro(th * 1.0 + 0.9 * k_row, phases)
            hd = heading_from_tee(cx + rr * math.cos(th + 0.5 * 34.0 / rr), cy + rr * math.sin(th + 0.5 * 34.0 / rr))
            in_window = WINDOW[0] <= hd <= WINDOW[1]
            if in_window:
                h = min(h, rnd.uniform(*FAR_WINDOW_H))
            spire = (k_row == 1 and not in_window and any(0.0 <= (a - th) % (2 * math.pi) < 0.05 for a in spire_at))
            if spire:
                kind, h = "ROCK_WALL_BASALT_F1", rnd.uniform(68.0, 74.0)
                s = min(1.9, h / dims[kind][2])
                yaw = rnd.uniform(0, math.tau)
                sc3 = (s * rnd.uniform(0.97, 1.03), s * rnd.uniform(0.97, 1.03), s)
                width = dims[kind][0] * s
            else:
                kind = "ROCK_WALL_BASALT_F3" if in_window else seg_kinds[rnd.randrange(3)]
                xl, yl, zt = dims[kind]
                s = min(1.9, max(MIN_SCALE, h / zt))
                yaw = th - math.pi / 2 + rnd.uniform(-0.08, 0.08)
                sc3 = (s * rnd.uniform(0.96, 1.04), s * rnd.uniform(0.96, 1.04), s)
                width = xl * s * 0.92
            dth = width * 0.95 * spacing / rr
            if rnd.random() < 0.05 and not spire:
                th += dth
                continue
            tc = th + 0.5 * dth
            vk = pick_variant(kinds_var, kind, rnd)
            out.append(PL.place(D, vk, (cx + rr * math.cos(tc), cy + rr * math.sin(tc), 0.0), yaw=yaw, scale=sc3))
            th += dth
    return out


def seg_dist(px, py, ax, ay, bx, by):
    abx, aby = bx - ax, by - ay
    t = np.clip(((px - ax) * abx + (py - ay) * aby) / max(abx * abx + aby * aby, 1e-9), 0.0, 1.0)
    return np.hypot(px - (ax + t * abx), py - (ay + t * aby))


# ----------------------------------------------------------------------------- the crust that covers the crater floor
CRUST_COL_R = 4.3                 # hex plate circumradius (m): plates ~6 m across
CRUST_SHORE_GAP_M = 24.0 * YD     # the keep-outs of round 2's H_MESAS, unchanged: 24 yd off every play surface,
CRUST_LINE_GAP_M = 38.0 * YD      # 38 yd off the line of play (the centerline polyline incl. the carry), 
CRUST_CHORD_GAP_M = 25.0 * YD     # 25 yd off the tee -> C chord (the corner-cutting line)
CRUST_STACK_GAP_M = 6.0           # a lava ring round every basalt stack
CRUST_TOP = (1.4, 3.6)            # plate tops, m above the lava (every sight line is above the 6 m play height: nothing here can cut one)
CRUST_TOP_MAX = 5.0               # highest vertex of any crust chunk incl. tilt and stumps (the sight lines are all above 6.5 m)
CRUST_BOTTOM = -4.0
CRUST_CELL_M = 44.0               # chunk grid (meshes of >= 14 plates, >= 100 faces)
CRUST_MIN_PLATES = 14
CRUST_MAX_FACES = 480             # props <= 500 faces each (PROP_FACE_BUDGET)
LIGHT_RULE_M = 25.0 * YD + 0.4    # a ROCK_ piece within 25 yd of a lava light may not be wider than 24 yd: crust chunks never come that close


def crust_plan(D, W, near_th, near_R, stacks_xy, lights, seed=1060):
    """Candidate plates [(x, y, h)] (world metres): a jittered hex lattice kept where the clearances hold, cut by winding rivers (zero crossings of a Perlin field)
    and lakes, heights = smooth noise quantised to 0.5 m terraces + a rising shelf along the wall foot."""
    rnd = random.Random(seed)
    cx, cy = P.m(W["cx"], W["cd"])
    cl = [P.m(*p) for p in D.CENTERLINE]
    R = 215.0 * YD
    cs = PL._hex_lattice(CRUST_COL_R, -R, -R, R, R, rnd, jitter=0.04, rows_x=True)
    xs = np.array([c[0] + cx for c in cs])
    ys = np.array([c[1] + cy for c in cs])
    rho = np.hypot(xs - cx, ys - cy)
    th = np.arctan2(ys - cy, xs - cx) % (2 * math.pi)
    rwall = np.interp(th, near_th, near_R, period=2 * math.pi)
    keep = rho + CRUST_COL_R + 5.0 < rwall
    sd = P.signed_dist_m(D, xs, ys)
    keep &= sd >= CRUST_SHORE_GAP_M + CRUST_COL_R
    dl = np.min([seg_dist(xs, ys, *cl[i], *cl[i + 1]) for i in range(len(cl) - 1)], axis=0)
    keep &= dl >= CRUST_LINE_GAP_M + CRUST_COL_R
    keep &= seg_dist(xs, ys, *cl[0], *cl[3]) >= CRUST_CHORD_GAP_M + CRUST_COL_R
    for sx, sy, sr in stacks_xy:
        keep &= np.hypot(xs - sx, ys - sy) >= sr + CRUST_STACK_GAP_M + CRUST_COL_R
    for lx, ly in lights:
        keep &= np.hypot(xs - lx, ys - ly) >= LIGHT_RULE_M + 8.0 + CRUST_COL_R
    riv = np.array([abs(mnoise.noise(Vector((x / 52.0 + 11.3, y / 52.0 + 7.1, 0.3)))) for x, y in zip(xs, ys)])
    riv2 = np.array([abs(mnoise.noise(Vector((x / 29.0 + 3.7, y / 29.0 + 19.2, 1.1)))) for x, y in zip(xs, ys)])
    lake = np.array([mnoise.noise(Vector((x / 41.0 - 5.5, y / 41.0 + 2.2, 2.7))) for x, y in zip(xs, ys)])
    keep &= (riv > 0.026) & (riv2 > 0.016) & (lake < 0.95)
    hn = np.array([0.5 + 0.5 * mnoise.noise(Vector((x / 47.0 + 1.9, y / 47.0 - 8.4, 0.9))) for x, y in zip(xs, ys)])
    h = CRUST_TOP[0] + (CRUST_TOP[1] - CRUST_TOP[0]) * 0.75 * hn
    h = np.round(h / 0.5) * 0.5
    shelf = np.clip((rho - (rwall - 46.0)) / 46.0, 0.0, 1.0)                  # the last 46 m before the wall rise to the wall foot
    h = np.minimum(CRUST_TOP[1], h + 1.6 * shelf)
    rr = np.array([rnd.uniform(-0.06, 0.06) for _ in xs])
    idx = np.nonzero(keep)[0]
    return [(float(xs[i]), float(ys[i]), float(h[i] + rr[i])) for i in idx], (cx, cy)


def crust_field(D, W, kinds_var_unused, near_th, near_R, stacks_xy, lights, rnd_uv):
    """The crust: hex plates (the library hex-column primitive, tops tilted <= 0.12 rad, most chipped) plus small basalt stumps standing on some of them, grouped into
    ROCK_CRUST_nn meshes of 100..480 faces (the props' 500-face budget) inside 44 m cells (six passes on shifted grids so fragments of one grid join the next, a group that
    comes out over 480 faces is split at its median); chunks closer than 25 yd to a lava light are dropped (H_ROCK_CHUNKING), chunks of < 100 faces too (ROCK_NOT_ICOSAHEDRON).
    UVs authored with author_uv (glow in the joints, glow-free tops). Returns (objects, plates kept, plates planned)."""
    plan, (cx, cy) = crust_plan(D, W, near_th, near_R, stacks_xy, lights)
    rng = random.Random(1061)
    groups={}
    for c in plan:groups.setdefault((int(c[0]//CRUST_CELL_M),int(c[1]//CRUST_CELL_M)),[]).append(c)
    keys=list(groups);random.Random(1069).shuffle(keys)
    selected=[]
    for key in keys:
        if len(selected)>=750:break
        selected.extend(groups[key])
    left=list(selected)
    out, kept = [], 0
    drop = dict(light=0, faces=0)

    def build(cols):
        mb = PL._MB(("LK_BASALT",))
        for x, y, h in cols:
            PL._hex_column(mb, rng, x, y, h, CRUST_BOTTOM, CRUST_COL_R, gap=0.06, tilt_max=0.12, chip_p=0.7, a_off=math.pi / 6)
        for x, y, h in rng.sample(cols, max(3, len(cols) // 5)):                       # stumps: small broken columns standing on a plate
            a_ = rng.uniform(0, math.tau)
            r_ = rng.uniform(0.0, 0.35) * CRUST_COL_R
            hh = rng.uniform(0.6, 1.4)
            PL._hex_column(mb, rng, x + r_ * math.cos(a_), y + r_ * math.sin(a_), min(h + hh, 4.3), h - 0.4, rng.uniform(0.9, 1.5), gap=0.08, tilt_max=0.25, chip_p=0.6, a_off=rng.uniform(0, 1.0))
        return mb

    def emit(cols):
        """-> list of (mb, cols) chunks of <= 480 faces (split at the median of the longer axis while over)."""
        mb = build(cols)
        if len(mb.F) <= CRUST_MAX_FACES:
            return [(mb, cols)]
        xs = [c[0] for c in cols]
        ys = [c[1] for c in cols]
        k = 0 if (max(xs) - min(xs)) >= (max(ys) - min(ys)) else 1
        srt = sorted(cols, key=lambda c: c[k])
        half = len(srt) // 2
        return emit(srt[:half]) + emit(srt[half:])

    for off in ((0.0, 0.0), (0.5, 0.0), (0.0, 0.5), (0.5, 0.5), (0.25, 0.25), (0.75, 0.75)):
        cells = {}
        for x, y, h in left:
            cells.setdefault((int(math.floor(x / CRUST_CELL_M + off[0])), int(math.floor(y / CRUST_CELL_M + off[1]))), []).append((x, y, h))
        used = set()
        for key in sorted(cells):
            if len(cells[key]) < CRUST_MIN_PLATES:
                continue
            for mb, cols in emit(cells[key]):
                xs = [v[0] for v in mb.V]
                ys = [v[1] for v in mb.V]
                lo, hi = (min(xs), min(ys)), (max(xs), max(ys))
                near_light = False
                for lx, ly in lights:
                    dx = max(lo[0] - lx, 0.0, lx - hi[0])
                    dy = max(lo[1] - ly, 0.0, ly - hi[1])
                    if dx * dx + dy * dy <= LIGHT_RULE_M ** 2:
                        near_light = True
                if near_light or len(mb.F) < 100:
                    drop["light" if near_light else "faces"] += len(cols)
                    continue
                name = f"ROCK_CRUST_{len(out):02d}"
                me = PL._finalize(name, mb, "box", smooth_angle=None, library=False)
                UV_STATS[name] = dict(author_uv(me, rnd_uv, uf=0.72, vf=0.48, p_glow=0.42), uf=0.72, vf=0.48)
                ob = bpy.data.objects.new(name, me)
                PL._collection_for(name).objects.link(ob)
                PL._parent(D, ob)
                out.append(ob)
                kept += len(cols)
                used.update((round(x, 3), round(y, 3)) for x, y, h in cols)
        left = [c for c in left if (round(c[0], 3), round(c[1], 3)) not in used]
    print(f"[hole10_look] crust: {len(plan)} plates planned, kept {kept} in {len(out)} chunks, dropped plates {drop}, left over {len(left)}", flush=True)
    return out, kept, len(plan)


# ----------------------------------------------------------------------------- the volcano: basalt strata with glowing lava flows and a molten crater
def build_cone(D, center, base_r=125.0, height=135.0, seed=1, name="DRESS_CONE", notch_deg=250.0, sides=72, bands=7, crater_frac=0.2,
               notch_depth=0.2, crater_depth=0.1, flows=5):
    """DRESS_CONE like the library's background cone (chunky strata: a steep step + a gentler slope per band, never undercut, a V-notch in the crater rim, a crater dish),
    but with `sides` = 72 and `flows` winding strips of LK_LAVA faces from the crater rim to the foot (crater.jpg's volcano glows along its flanks) + a molten
    crater floor. Slot 0 = LK_BASALT (box UV at CONE_UV x the 8 m tile), slot 1 = LK_LAVA (planar UV at the 24 m tile). Visual only, no collider."""
    rng = random.Random(PL._seed_int("cone", seed, name))
    cx, cy, cz = center
    mb = PL._MB(("LK_BASALT",))                  # slot 1 (LK_LAVA, the look lib's material) is appended after _finalize: the props lib has no LK_LAVA spec
    notch = math.radians(notch_deg)
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
        if kind == 1:
            r_prof = (rim_r + (base_r - rim_r) * (1.0 - zband / height) ** 1.6) * 0.95
        bidx = min(li // 2, bands)
        ring = []
        for i, a in enumerate(angs):
            r = r_prof * jit[bidx][i]
            r = min(r, prev[i])
            prev[i] = r
            zz = z
            da = math.atan2(math.sin(a - notch), math.cos(a - notch))
            if tz > 0.55:
                zz -= notch_depth * height * max(0.0, math.cos(da)) ** 6 * ((tz - 0.55) / 0.45) ** 1.2
            if kind == 2:
                zz += rng.uniform(-0.015, 0.015) * height
            zz = max(zz, prev_z[i] + 0.006 * height)
            prev_z[i] = zz
            ring.append(mb.v((cx + math.cos(a) * r, cy + math.sin(a) * r, cz + zz)))
        rings.append(ring)
    nlev = len(rings)
    # flows: each starts at a random angular index of the top ring and walks down (+-1 index per level with p 0.4), a strip of single lava quads; neighbouring flows never touch
    lava_cells = set()
    starts = sorted(rng.sample(range(sides), flows))
    for s0 in starts:
        i = s0
        for k in range(nlev - 2, -1, -1):                                  # from the top (last ring pair) to the foot
            lava_cells.add((k, i))
            if k > 0 and rng.random() < 0.4:
                i = (i + rng.choice((-1, 1))) % sides
                lava_cells.add((k, i))
    faces = []
    for k in range(nlev - 1):
        for i in range(sides):
            j = (i + 1) % sides
            lava = (k, i) in lava_cells
            faces.append(((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]), 1 if lava else 0))
    rim = rings[-1]
    inner = []
    floor_z = min(min(prev_z) - 0.03 * height, height * (1.0 - crater_depth))
    for i, a in enumerate(angs):
        inner.append(mb.v((cx + math.cos(a) * rim_r * 0.62, cy + math.sin(a) * rim_r * 0.62, cz + floor_z)))
    for i in range(sides):
        j = (i + 1) % sides
        faces.append(((rim[i], rim[j], inner[j], inner[i]), 1))            # the molten inner wall
    faces.append((tuple(inner), 1))
    for idx, slot in faces:
        mb.f(idx, 0)
    me = PL._finalize(name, mb, "box", smooth_angle=None, library=False)
    me.materials.append(bpy.data.materials["LK_LAVA"])
    me.polygons.foreach_set("material_index", np.array([slot for _, slot in faces], np.int32))
    me.update()
    # UV: basalt faces keep the world box UV at 0.45 x (a 40 m tile: the strata stay readable), lava faces a planar map (u along the slope's horizontal tangent, v up the slope) at the 24 m tile
    vidx, ls, lt, co = _polys(me)
    lay = me.uv_layers.active
    mi = np.zeros(len(me.polygons), np.int32)
    me.polygons.foreach_get("material_index", mi)
    uv = np.zeros((len(me.loops), 2))
    for pi in range(len(me.polygons)):
        l0, n = int(ls[pi]), int(lt[pi])
        pts = co[vidx[l0:l0 + n]]
        nrm = _newell(pts)
        if mi[pi] == 1:
            c = pts.mean(0)
            hh = np.array([nrm[0], nrm[1], 0.0])
            hl = float(np.linalg.norm(hh))
            if hl < 1e-6:
                t_, s_ = np.array([1.0, 0.0, 0.0]), np.array([0.0, 1.0, 0.0])
            else:
                hh /= hl
                t_ = np.array([-hh[1], hh[0], 0.0])
                s_ = np.cross(nrm, t_)
            uv[l0:l0 + n, 0] = (pts @ t_) / 24.0
            uv[l0:l0 + n, 1] = (pts @ s_) / 24.0
        else:
            if abs(nrm[2]) >= 0.5:
                uv[l0:l0 + n, 0] = pts[:, 0] * CONE_UV / TILE
                uv[l0:l0 + n, 1] = pts[:, 1] * CONE_UV / TILE
            else:
                hh = np.array([nrm[0], nrm[1], 0.0])
                hh /= max(float(np.linalg.norm(hh)), 1e-9)
                t_ = np.array([-hh[1], hh[0], 0.0])
                uv[l0:l0 + n, 0] = (pts @ t_) * CONE_UV / TILE
                uv[l0:l0 + n, 1] = pts[:, 2] * CONE_UV / TILE
    lay.uv.foreach_set("vector", uv.ravel())
    me.update()
    if bpy.data.objects.get(name) is not None:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
    ob = bpy.data.objects.new(name, me)
    PL._collection_for("DRESS_CONE").objects.link(ob)
    PL._parent(D, ob)
    return ob


# ----------------------------------------------------------------------------- smoke plumes (repair round 1, 2026-10-04: SMOKE_IN_FRAME)
def smoke_group(D, cam_xy, p, wh, n=3, seed=1, spread=0.10, parallel=False):
    """A stacked plume: n DRESS_SMOKE_nn cards (the library card, every one its own mesh) of about w x h metres, bottom centre near p, each turned to face `cam_xy` (the proof camera
    the plume is for), spread sideways by `spread` x w and jittered in depth / height / size; every second card has its U mirrored so the stack does not repeat one wisp. Why stacked and big:
    Smoke_C peaks at alpha .50 (x GolfLook AlphaGain 1.35 = .67) and only 11 % of the card area reaches an effective alpha of .30, so a plume that the verifier (SMOKE_IN_FRAME:
    >= 1.5 % of the frame at >= .30, unoccluded, in EACH proof frame) and the player can pick out needs ~10 % of the frame as card area; the stack accumulates (1 - prod(1 - a))."""
    rnd = random.Random(seed * 7919 + 13)
    fx, fy = cam_xy
    dx, dy = p[0] - fx, p[1] - fy
    ln = math.hypot(dx, dy)
    lx, ly = -dy / ln, dx / ln
    obs = []
    for i in range(n):
        k = (i - (n - 1) / 2.0) if n > 1 else 0.0
        off = k * spread * wh[0] + rnd.uniform(-0.03, 0.03) * wh[0]
        dep = rnd.uniform(-0.04, 0.04) * wh[0]
        s = 1.0 + rnd.uniform(-0.12, 0.12)
        pos = (p[0] + lx * off + dx / ln * dep, p[1] + ly * off + dy / ln * dep, p[2] + rnd.uniform(-0.04, 0.04) * wh[1])
        ob = PL.build_smoke_cards(D, [(pos, wh[0] * s, wh[1] * s)], seed=seed * 10 + i, face=(fx, fy))[0]
        if parallel:ob.rotation_euler.z=math.atan2(fy-p[1],fx-p[0])+math.pi/2
        ob['lk_v3_smoke_group']=int(seed)
        ob['lk_v3_vent_anchor']=tuple(float(q) for q in p)
        if i % 2 == 1:
            lay = ob.data.uv_layers[0]
            uv = np.empty(len(lay.data) * 2)
            lay.data.foreach_get("uv", uv)
            uv[0::2] = 1.0 - uv[0::2]
            lay.data.foreach_set("uv", uv)
            ob.data.update()
        obs.append(ob)
    return obs


# ----------------------------------------------------------------------------- ground UV repair + tee kerb (repair round 2, 2026-10-05)
UV_SLIVER_TH = 2.5                 # re-UV a ground triangle whose UV -> world Jacobian singular ratio is above this (the verifier's GROUND_UV_LOCAL_SMEAR limit is 4:1 / 15 m2 per mesh / 10 m2 per triangle)


def tri_uv_ratio(W, tl, uv):
    """Per-triangle (singular-value ratio of the UV -> world Jacobian, world area m2); the closed form of postcard_look_verify.uv_aniso_world (a UV triangle that collapses to a line = 1000).
    W (nv, 3) world positions (3D, like the verifier: a tilted wall triangle is measured in its own plane), tl (nt, 3) vertex ids, uv (nt, 3, 2) the three loop UVs of every triangle (tile units, the world is divided by the same tile by the caller: the ratio is scale free)."""
    A, B, C = W[tl[:, 0]], W[tl[:, 1]], W[tl[:, 2]]
    e1, e2 = B - A, C - A
    d1, d2 = uv[:, 1] - uv[:, 0], uv[:, 2] - uv[:, 0]
    sd = d1[:, 0] * d2[:, 1] - d1[:, 1] * d2[:, 0]
    area = 0.5 * np.sqrt(np.maximum((e1 * e1).sum(1) * (e2 * e2).sum(1) - ((e1 * e2).sum(1)) ** 2, 0.0))
    good = np.abs(sd) > 1e-12
    sd = np.where(good, sd, 1.0)
    Pu = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / sd[:, None]
    Pv = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / sd[:, None]
    g11, g22, g12 = (Pu * Pu).sum(1), (Pv * Pv).sum(1), (Pu * Pv).sum(1)
    tr, dt = g11 + g22, g11 * g22 - g12 * g12
    disc = np.sqrt(np.maximum(tr * tr / 4.0 - dt, 0.0))
    lmax, lmin = tr / 2.0 + disc, np.maximum(tr / 2.0 - disc, 1e-30)
    ratio = np.where(good, np.sqrt(lmax / lmin), 1e3)
    return np.minimum(ratio, 1e3), area


def mesh_tris_uv(ob):
    """(world xy per vertex, triangles' vertex ids (nt, 3), triangles' loop ids (nt, 3), per-loop uv (nl, 2)) of an all-triangle ground mesh (the scoring meshes are triangulated)."""
    me = ob.data
    nv = len(me.vertices)
    co = np.empty(nv * 3)
    me.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    mw = np.array(ob.matrix_world)
    W = co @ mw[:3, :3].T + mw[:3, 3]
    me.calc_loop_triangles()
    nt = len(me.loop_triangles)
    tv = np.empty(nt * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", tv)
    tl = np.empty(nt * 3, np.int64)
    me.loop_triangles.foreach_get("loops", tl)
    uv = np.empty(len(me.loops) * 2)
    me.uv_layers[0].data.foreach_get("uv", uv)
    return W, tv.reshape(-1, 3), tl.reshape(-1, 3), uv.reshape(-1, 2)


def unfold_sliver_uv(ob, tile, th=UV_SLIVER_TH):
    """Re-author the UV of the long thin sliver triangles of a ground mesh, GEOMETRY UNTOUCHED (the scoring surface keeps every vertex).

    Why: assign_uvs gives every VERTEX the centerline UV of its nearest point on the smoothed centerline. A long sliver between a curved outline and the chord of two far-apart outline
    vertices (FAIRWAY_FIRSTCUT, 2.8 m wide and 51 m long on the bend at course x 80-100 yd) then has three vertex UVs that lie on one line in UV space (all three at lateral 8-10 m), although
    the triangle is 2.8 m wide in the world: the Jacobian ratio is 26:1 / 4.7:1 / collapsed and the texture is smeared into hard streaks (the wedge of horizontal streaks in the upper-left
    fairway of the lava-rim still; verifier GROUND_UV_LOCAL_SMEAR 103 m2). The world -> UV map of the centerline is a proper rotation, so the repair is a SIMILARITY: every connected
    cluster of sliver triangles (triangles above `th`, joined through shared edges) gets ONE complex-linear map w = a z + b (z = x + i y in the world, w = u + i v in the UV, a = rotation x scale) fitted
    by weighted least squares to the clusters' original per-vertex UVs (weight 4 on a vertex that a normal triangle also uses: its UV is held by that neighbour, weight 1 on the others). Every triangle of
    the cluster is then exactly isotropic (ratio 1.000), the triangles of one cluster stay continuous with each other (one map), and the only jump left is the least-squares residual at the cluster's vertices
    (reported). Returns dict(n_bad, n_zero, n_fixed, n_clusters, area_fixed, max_ratio_before, max_ratio_after, max_ratio_mesh, max_jump_m, max_resid_m) (jump = the largest UV difference, in metres of this tile, of a
    shared vertex between two triangles of the mesh after the repair, over the edges that touch a re-authored triangle; resid = the largest distance of a fitted corner UV to its original vertex UV)."""
    me = ob.data
    W, tv, tl, uv = mesh_tris_uv(ob)
    nt = len(tv)
    if nt != len(me.polygons):
        raise RuntimeError(f"{ob.name}: not an all-triangle mesh ({len(me.polygons)} polygons, {nt} triangles)")
    uvt = uv[tl].copy()                                            # (nt, 3, 2) original loop UVs
    ratio0, area = tri_uv_ratio(W, tv, uvt)
    bad = (ratio0 > th) & (area > 0.0)
    zero = area <= 0.0                                             # exactly degenerate triangles: nothing to texture, they still get a sane UV below
    todo = bad | zero
    edge_polys = {}
    for t in range(nt):
        for k in range(3):
            a, b = int(tv[t, k]), int(tv[t, (k + 1) % 3])
            edge_polys.setdefault((min(a, b), max(a, b)), []).append(t)
    parent = list(range(nt))

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x
    for ts in edge_polys.values():
        if len(ts) == 2 and todo[ts[0]] and todo[ts[1]]:
            parent[find(ts[0])] = find(ts[1])
    clusters = {}
    for t in np.nonzero(todo)[0].tolist():
        clusters.setdefault(find(t), []).append(t)
    good_verts = set(np.unique(tv[~todo]).tolist())
    new_uv = uvt.copy()
    max_resid = 0.0
    for ts in clusters.values():
        z = np.array([complex(*W[tv[t, k], :2]) for t in ts for k in range(3)])
        w = np.array([complex(*uvt[t, k]) for t in ts for k in range(3)])
        wt = np.array([4.0 if int(tv[t, k]) in good_verts else 1.0 for t in ts for k in range(3)])
        zc, wc = (wt * z).sum() / wt.sum(), (wt * w).sum() / wt.sum()
        den = (wt * np.abs(z - zc) ** 2).sum()
        a_ = (wt * np.conj(z - zc) * (w - wc)).sum() / den if den > 1e-12 else 1.0 / tile
        b_ = wc - a_ * zc
        fit = a_ * z + b_
        max_resid = max(max_resid, float(np.abs(fit - w).max()) * tile)
        for j, (t, k) in enumerate((t, k) for t in ts for k in range(3)):
            new_uv[t, k] = (fit[j].real, fit[j].imag)
    changed = np.nonzero(todo)[0]
    lay = me.uv_layers[0].data
    flat = np.empty(len(me.loops) * 2)
    lay.foreach_get("uv", flat)
    flat = flat.reshape(-1, 2)
    flat[tl[changed].ravel()] = new_uv[changed].reshape(-1, 2)
    lay.foreach_set("uv", flat.ravel())
    me.update()
    ratio1, _ = tri_uv_ratio(W, tv, flat[tl])
    ch = set(changed.tolist())
    jump = 0.0
    for (a, b), ts in edge_polys.items():
        if len(ts) != 2 or not (ts[0] in ch or ts[1] in ch):
            continue
        for v in (a, b):
            u0 = flat[tl[ts[0]][int(np.nonzero(tv[ts[0]] == v)[0][0])]]
            u1 = flat[tl[ts[1]][int(np.nonzero(tv[ts[1]] == v)[0][0])]]
            jump = max(jump, float(np.hypot(*(u0 - u1))) * tile)
    return dict(n_bad=int(bad.sum()), n_zero=int(zero.sum()), n_fixed=int(todo.sum()), n_clusters=len(clusters), area_fixed=float(area[changed].sum()),
                max_ratio_before=float(ratio0[changed].max()) if len(changed) else 0.0, max_ratio_after=float(ratio1[changed].max()) if len(changed) else 0.0,
                max_ratio_mesh=float(ratio1[area > 0].max()), max_jump_m=jump, max_resid_m=max_resid)


def pad_outline(ob):
    """The TEE_BOX plan outline: the boundary edges of the triangulated pad chained into one counter-clockwise loop of world (x, y) points."""
    me = ob.data
    cnt = {}
    for p in me.polygons:
        vs = list(p.vertices)
        for k in range(len(vs)):
            a, b = vs[k], vs[(k + 1) % len(vs)]
            cnt[(min(a, b), max(a, b))] = cnt.get((min(a, b), max(a, b)), 0) + 1
    nxt = {}
    for (a, b), c in cnt.items():
        if c == 1:
            nxt.setdefault(a, []).append(b)
            nxt.setdefault(b, []).append(a)
    start = next(iter(nxt))
    loop, prev, cur = [start], None, start
    while True:
        cand = [v for v in nxt[cur] if v != prev]
        if not cand:
            break
        n_ = cand[0]
        if n_ == start:
            break
        loop.append(n_)
        prev, cur = cur, n_
    mw = np.array(ob.matrix_world)
    pts = np.array([(mw @ np.array([*me.vertices[v].co, 1.0]))[:2] for v in loop])
    ar = 0.5 * float(np.dot(pts[:, 0], np.roll(pts[:, 1], -1)) - np.dot(pts[:, 1], np.roll(pts[:, 0], -1)))
    return pts if ar > 0 else pts[::-1].copy()


def inset_polygon(pts, d):
    """Miter offset of a convex counter-clockwise polygon by d metres inward (every vertex moves along the bisector so that both edges move d)."""
    n = len(pts)
    out = np.empty_like(pts)
    for i in range(n):
        p0, p1, p2 = pts[i - 1], pts[i], pts[(i + 1) % n]
        e1, e2 = p1 - p0, p2 - p1
        n1 = np.array([-e1[1], e1[0]]) / max(float(np.hypot(*e1)), 1e-12)
        n2 = np.array([-e2[1], e2[0]]) / max(float(np.hypot(*e2)), 1e-12)
        out[i] = p1 + d * (n1 + n2) / (1.0 + float(n1 @ n2))
    return out


def tee_kerb_polyline(ob, aim, inset_m, gap_m, step_m=0.25):
    """The tee kerb centre line: the pad outline inset by `inset_m`, resampled every `step_m`, walked counter-clockwise from the right of the back-centre gap round the front to the left of
    it. `aim` = unit vector of the first chord (tee -> A): the back of the pad is the point of the inset loop furthest against `aim`. gap_m = the opening behind the player. Returns (points (n, 2), s_back)."""
    pts = inset_polygon(pad_outline(ob), inset_m)
    closed = np.vstack([pts, pts[:1]])
    seg = np.hypot(*np.diff(closed, axis=0).T)
    s = np.concatenate([[0.0], np.cumsum(seg)])
    total = float(s[-1])
    ns = max(int(total / step_m), 8)
    ss = np.linspace(0.0, total, ns, endpoint=False)
    xs = np.interp(ss, s, closed[:, 0])
    ys = np.interp(ss, s, closed[:, 1])
    dense = np.stack([xs, ys], 1)
    k_back = int(np.argmin(dense @ np.asarray(aim, float)))
    s_back = float(ss[k_back])
    start = s_back + 0.5 * gap_m
    stop = s_back - 0.5 * gap_m + total
    sw = np.arange(start, stop, step_m)
    sm = np.mod(sw, total)
    xs = np.interp(sm, s, closed[:, 0])
    ys = np.interp(sm, s, closed[:, 1])
    return np.stack([xs, ys], 1), s_back


# ----------------------------------------------------------------------------- repair round 3: the tee pad's dark edge ribbon (replaces the round-2 flagstone kerb)
# Review (low): "Tee reads as the square tee pad; no paved road across the fairway": the round-2 kerb is a 1.3 m LK_PATH flagstone strip, tan-brown, 44 pixel rows over the full frame width of the tee still; it reads as a
# crosswalk / road and its brown appears nowhere in crater.jpg (a grass SQUARE on BLACK rock). The review offers two fixes: a dark basalt rim, or a 20-30 cm dark border.
#   * material="LK_BASALT" (uv_mode="basalt"): a continuous ragged ribbon of black basalt (tops on glow-free slices of Basalt_C / Basalt_E). It looks right (v2/hole10/stage_rim_variant/, repair_r3/iter2) but the verifier
#     (postcard_look_verify.rock_candidates) judges any mesh with >= 50 % of its area on a rock material a ROCK, whatever its name: a flush 1.15 cm ribbon is a flat open "box" (ROCK_NOT_ICOSAHEDRON) and a baked flat stretch
#     (NO_ROCK_STRETCH_2X): 4 lines at blend + FBX fail, every other gate passes. Not the main build.
#   * material="LK_PLANTS" (uv_mode="atlas", the MAIN build, DRESS_PADEDGE): the same ribbon, ~0.35 m wide, painted with the two darkest GREEN swatches of the Plants atlas (G_DEEP / G_DARK = the stills' dark-green
#     "pillar rim" family); one swatch per slab. Counted by ONE_GREEN's DRESS_ lawn budget (< 20 m2 of flat grass-material top over all DRESS_ meshes), which caps its width.
#   * material="LK_ROUGH" (uv_mode="planar"): the same with the Rough grass; too faint (about 20 % darker than the fairway), kept for the record.
RIM_Z_OFF = 0.0115                         # top over the LOWEST ground sample of its neighbourhood (the library's path value: under the ball)
RIM_SKIRT = 0.04                           # the apron under the ground (no gap, no light leak at the edge)
ATLAS_SLABS = ("G_DEEP", "G_DARK", "G_DARK")    # Plants-atlas swatches of the round-3 'atlas' ribbon (the two darkest GREEN swatches: under the warm Crater key OLIVE_DARK turns brown, the review's complaint about the tan kerb)
# repair round 4 (review, low: "the replacement is a full-width dark olive band with a black wavy outline and 3 flat-shaded facets ... reads as a trench"): the 3 facets were the per-slab swatch changes
# (G_DEEP next to G_DARK: two flat colours with a hard diagonal joint every 0.45-1.05 m) and the black outline was the VERTICAL inner apron (1.15 cm over the ground, facing the tee camera, unlit by the key).
# The main ribbon is now ONE swatch with a smooth luminance gradient along the arc and across (the atlas swatches are vertical gradients x0.68 .. x1.12: only the v of the UV changes, continuously, round the closed
# loop: no joint anywhere), a thin 0.17-0.45 m border, and its inner edge is a shallow RAMP (9 cm wide, from the top down to 6 mm under the collider: the visible part is 1.15 cm of a 7 degree slope that the key
# lights like the top) instead of a vertical face. The outer apron (facing away from the camera, 3 cm inside the pad lip) is unchanged.
ATLAS_SINGLE = "G_DARK"
ATLAS_T = (0.50, 0.15, 0.09)                   # (mean v, along-the-arc wander, half the across-the-ribbon gradient: the outer edge is darker, the inner edge lighter, like a shadowed rim)
RIM_RAMP_W = 0.09
RIM_RAMP_DROP = 0.006


def _rim_noise(s, ph, seed):
    """Deterministic smooth noise in [-1, 1] along the arc length s (three octaves, random phases): the ribbon's width wander."""
    r = random.Random(seed)
    out = np.zeros_like(s)
    for per, amp in ((3.4, 0.55), (1.35, 0.30), (0.62, 0.15)):
        out += amp * np.sin(2.0 * math.pi * (s / per) + r.uniform(0.0, 2.0 * math.pi) + ph)
    return out


def tee_rim_plan(ob, seed=11, w_mean=0.50, w_amp=0.30, edge_in=0.03, step=0.20, slab=(0.45, 1.05), uf=0.62, bites=(0.40, 0.7, 1.2), bite_every=9.0, w_clip=(0.24, 0.80), aim=None, far_boost=(0.85, 0.85, 0.15)):
    """The rim's plan: the outer boundary = the pad's lip inset by `edge_in` (every vertex stays on the pad top: it never climbs the 10 cm riser), the inner boundary wanders (width w_mean x (1 +- w_amp),
    plus a few bites 0.7-1.2 m long that take `bites[0]` of the width), rows every `step` m round the CLOSED loop. Returns dict(P outer (n, 2), Q inner (n, 2), t (n, 2), s (n+1), w (n), slab (n) = the slab id
    of the row interval i -> i+1, total)."""
    rnd = random.Random(seed * 131 + 7)
    outer = inset_polygon(pad_outline(ob), edge_in)
    closed = np.vstack([outer, outer[:1]])
    seg = np.hypot(*np.diff(closed, axis=0).T)
    s_c = np.concatenate([[0.0], np.cumsum(seg)])
    total = float(s_c[-1])
    n = max(int(round(total / step)), 16)
    ss = np.linspace(0.0, total, n, endpoint=False)
    P0 = np.stack([np.interp(ss, s_c, closed[:, 0]), np.interp(ss, s_c, closed[:, 1])], 1)
    # tangents by central differences on the closed row loop, left normal = inward (the pad outline is counter-clockwise)
    t = np.roll(P0, -1, axis=0) - np.roll(P0, 1, axis=0)
    t /= np.maximum(np.hypot(t[:, 0], t[:, 1]), 1e-9)[:, None]
    nin = np.stack([-t[:, 1], t[:, 0]], 1)
    w = w_mean * (1.0 + w_amp * _rim_noise(ss, 0.0, seed + 1))
    if aim is not None:
        # the tee camera only sees the FAR edge (the pad's other edges show in aerials): the ribbon is wider where the camera looks at it and narrower along the sides and the back, so the lawn budget
        # (< 20 m2 of flat grass-material top over all DRESS_ meshes) buys the visible band: factor = base + gain x (far-edge-ness) - loss x (back-edge-ness), far = 1.7x, sides 0.85x, back 0.7x with the defaults
        sfar = -(nin @ np.asarray(aim, float))
        w = w * (far_boost[0] + far_boost[1] * np.maximum(sfar, 0.0) - far_boost[2] * np.maximum(-sfar, 0.0))
    n_bites = max(int(total / bite_every), 2)
    for _ in range(n_bites):
        c = rnd.uniform(0.0, total)
        ln = rnd.uniform(0.7, 1.2) * 0.5
        d = np.abs(((ss - c) + 0.5 * total) % total - 0.5 * total)
        w *= 1.0 - bites[0] * np.exp(-(d / ln) ** 2)
    w = np.clip(w, *w_clip)
    Q = P0 + nin * w[:, None]
    # slabs: contiguous runs of 0.45-1.05 m (a joint between two slabs = a UV discontinuity = a crack in the rock, geometry stays one continuous ribbon)
    sid, k, acc = np.zeros(n, np.int64), 0, 0.0
    lim = rnd.uniform(*slab)
    for i in range(n):
        sid[i] = k
        acc += total / n
        if acc >= lim:
            k += 1
            acc = 0.0
            lim = rnd.uniform(*slab)
    return dict(P=P0, Q=Q, t=t, nin=nin, s=np.concatenate([ss, [total]]), w=w, slab=sid, total=total, n=n, step=total / n)


def build_tee_rim(D, L, ob, seed=11, name="DRESS_PADRIM", n_pieces=4, uf=0.62, material="LK_BASALT", uv_mode="basalt", **kw):
    """Build the ribbon round the TEE_BOX lip (see the section comment). material / name / uv_mode: "LK_BASALT" / "DRESS_PADRIM" / "basalt" = the dark basalt rim (the variant the verifier's rock classification rejects),
    "LK_ROUGH" / "DRESS_PADEDGE" / "planar" = the dark-green rough collar (world-planar UV at the contract tile). L = postcard_look_lib. Pieces <name>_nn (one material, no collision prefix), a flat ribbon whose top is
    RIM_Z_OFF over the lowest collider sample of its neighbourhood (so it can never hide the ball), an apron RIM_SKIRT under the ground on both edges. UV0: u along the arc, v across (u x v = +z: the
    normal map is not mirrored), one slice of the Basalt tile per slab, u always inside a glow-free zone of Basalt_E (author_uv's rule for tops). Returns (objects, stats)."""
    pl = tee_rim_plan(ob, seed=seed, **kw)
    Pp, Qq, tt, nin, ss, slab_id, n = pl["P"], pl["Q"], pl["t"], pl["nin"], pl["s"], pl["slab"], pl["n"]
    bvh = L._ground_bvh(D)
    down = Vector((0, 0, -1))

    def ground(x, y):
        # the collider right under the vertex (NOT the lowest of a neighbourhood: the rim hugs the pad's lip, 10 cm over the fairway, and a neighbourhood minimum would sink it into the pad)
        h = bvh.ray_cast(Vector((x, y, D.play_z + 3.0)), down, 50.0)
        return D.play_z if h[0] is None else h[0].z
    zg_o = np.array([ground(*p) for p in Pp])
    zg_i = np.array([ground(*q) for q in Qq])
    # one z per row (the lowest ground of its two edge vertices and of its two neighbour rows): a flat ribbon across, never above the ground by more than RIM_Z_OFF anywhere
    zr = np.minimum(zg_o, zg_i)
    zr = np.minimum(zr, np.minimum(np.roll(zr, 1), np.roll(zr, -1))) + RIM_Z_OFF
    rnd = random.Random(seed * 977 + 3)
    du = uf / TILE                                          # uv units per metre (u along the arc, v across / up): isotropic at the contract density x uf
    n_slab = int(slab_id.max()) + 1
    win = []                                                # per slab: (u0, v0, zone, mean luminance of the window)
    planar = uv_mode == "planar"
    atlas = uv_mode == "atlas"
    tile_m = float(L.LOOK[material][2]) if planar else TILE
    for k in range(n_slab):
        rows = np.flatnonzero(slab_id == k)
        length = float(ss[rows[-1] + 1] - ss[rows[0]])
        span = length * du
        if planar:
            win.append((0.0, 0.0, -1, 0.0, span))
            continue
        if atlas:                                           # round 4: ONE palette swatch for the whole ribbon (the round-3 per-slab swatch change was the review's "flat-shaded facets")
            win.append((0.0, 0.0, ATLAS_SINGLE, 0.0, span))
            continue
        ok = [z for z in range(len(ZONES)) if ZONE_W[z] >= span * 1.03]
        z = pick_zone(rnd, ok)
        lo, hi = ZONES[z]
        u0 = pick_u0(rnd, lo, hi, span)
        win.append((u0, rnd.random(), z, win_lum(u0, span), span))
    first_row = {}
    for i in range(n):
        first_row.setdefault(int(slab_id[i]), i)
    mat = L.look_material(material, D)
    # round 4 (atlas mode): the luminance gradient of the single swatch = the v of the UV, a smooth function of the arc length (closed loop: the same value at row n and row 0)
    t_mid = ATLAS_T[0] + ATLAS_T[1] * _rim_noise(ss[:-1], 0.0, seed + 5)
    t_out = np.clip(t_mid - ATLAS_T[2], 0.0, 1.0)
    t_in = np.clip(t_mid + ATLAS_T[2], 0.0, 1.0)
    # pieces: contiguous slab groups (a piece starts at a slab boundary)
    bounds = [int(round(n_slab * k / n_pieces)) for k in range(n_pieces + 1)]
    objs, st_faces = [], 0
    for pi in range(n_pieces):
        k0, k1 = bounds[pi], bounds[pi + 1]
        rows = [i for i in range(n) if k0 <= slab_id[i] < k1]
        if not rows:
            continue
        verts, faces, uvs = [], [], []

        def vid(p):
            verts.append(p)
            return len(verts) - 1
        for i in rows:
            j = (i + 1) % n
            k = int(slab_id[i])
            u0, v0, _, _, _ = win[k]
            a0 = float(ss[i] - ss[first_row[k]])
            a1 = float(a0 + (ss[i + 1] - ss[i]))
            za, zb = float(zr[i]), float(zr[j])
            po, pj_o = Pp[i], Pp[j]
            qi, qj = Qq[i], Qq[j]
            # top quad (o_i, o_j, q_j, q_i): counter-clockwise from above
            ids = [vid((float(po[0]), float(po[1]), za)), vid((float(pj_o[0]), float(pj_o[1]), zb)), vid((float(qj[0]), float(qj[1]), zb)), vid((float(qi[0]), float(qi[1]), za))]
            faces.append(tuple(ids))
            w0, w1 = float(pl["w"][i]), float(pl["w"][j])
            if atlas:
                sw_ = win[k][2]
                uvs.append([PL.swatch_uv(sw_, 0.5, float(t_out[i])), PL.swatch_uv(sw_, 0.5, float(t_out[j])), PL.swatch_uv(sw_, 0.5, float(t_in[j])), PL.swatch_uv(sw_, 0.5, float(t_in[i]))])
            elif planar:                                         # world-planar UV (u = x / tile, v = y / tile: u x v = +z, the tangent frame of every planar ground face)
                uvs.append([(po[0] / tile_m, po[1] / tile_m), (pj_o[0] / tile_m, pj_o[1] / tile_m), (qj[0] / tile_m, qj[1] / tile_m), (qi[0] / tile_m, qi[1] / tile_m)])
            else:
                uvs.append([(u0 + a0 * du, v0), (u0 + a1 * du, v0), (u0 + a1 * du, v0 + w1 * du), (u0 + a0 * du, v0 + w0 * du)])
            # aprons: the outer edge faces outward (away from the pad centre), the inner edge faces the pad centre
            fo = float(zg_o[i]) - RIM_SKIRT
            fj = float(zg_o[j]) - RIM_SKIRT
            fo = min(fo, za - RIM_Z_OFF - RIM_SKIRT)
            fj = min(fj, zb - RIM_Z_OFF - RIM_SKIRT)
            hh0, hh1 = (za - fo), (zb - fj)
            ids_o = [vid((float(pj_o[0]), float(pj_o[1]), zb)), vid((float(po[0]), float(po[1]), za)), vid((float(po[0]), float(po[1]), fo)), vid((float(pj_o[0]), float(pj_o[1]), fj))]
            faces.append(tuple(ids_o))
            if atlas:
                uvs.append([PL.swatch_uv(win[k][2], 0.5, float(t_out[j])), PL.swatch_uv(win[k][2], 0.5, float(t_out[i])), PL.swatch_uv(win[k][2], 0.5, max(float(t_out[i]) - 0.15, 0.0)),
                            PL.swatch_uv(win[k][2], 0.5, max(float(t_out[j]) - 0.15, 0.0))])
            elif planar:
                uvs.append([((ss[i] + a1 - a0) / tile_m, hh1 / tile_m), (ss[i] / tile_m, hh0 / tile_m), (ss[i] / tile_m, 0.0), ((ss[i] + a1 - a0) / tile_m, 0.0)])
            else:
                uvs.append([(u0 + a1 * du, v0 + 0.31 + hh1 * du), (u0 + a0 * du, v0 + 0.31 + hh0 * du), (u0 + a0 * du, v0 + 0.31), (u0 + a1 * du, v0 + 0.31)])
            if atlas:
                # round 4: the inner edge is a shallow RAMP, not a vertical face (the black outline of the round-3 still): the top quad's inner edge (q_i, q_j) continues inward by RIM_RAMP_W down to
                # RIM_RAMP_DROP under the collider (counter-clockwise from above like the top quad), so the visible 1.15 cm is a ~7 degree slope the key light reaches
                ri = (float(qi[0] + nin[i][0] * RIM_RAMP_W), float(qi[1] + nin[i][1] * RIM_RAMP_W))
                rj = (float(qj[0] + nin[j][0] * RIM_RAMP_W), float(qj[1] + nin[j][1] * RIM_RAMP_W))
                gi = float(zg_i[i]) - RIM_RAMP_DROP
                gj = float(zg_i[j]) - RIM_RAMP_DROP
                ids_i = [vid((float(qi[0]), float(qi[1]), za)), vid((float(qj[0]), float(qj[1]), zb)), vid((rj[0], rj[1], gj)), vid((ri[0], ri[1], gi))]
                faces.append(tuple(ids_i))
                uvs.append([PL.swatch_uv(win[k][2], 0.5, float(t_in[i])), PL.swatch_uv(win[k][2], 0.5, float(t_in[j])), PL.swatch_uv(win[k][2], 0.5, min(float(t_in[j]) + 0.04, 1.0)),
                            PL.swatch_uv(win[k][2], 0.5, min(float(t_in[i]) + 0.04, 1.0))])
                continue
            gi, gj = float(zg_i[i]) - RIM_SKIRT, float(zg_i[j]) - RIM_SKIRT
            gi = min(gi, za - RIM_Z_OFF - RIM_SKIRT)
            gj = min(gj, zb - RIM_Z_OFF - RIM_SKIRT)
            ids_i = [vid((float(qi[0]), float(qi[1]), za)), vid((float(qj[0]), float(qj[1]), zb)), vid((float(qj[0]), float(qj[1]), gj)), vid((float(qi[0]), float(qi[1]), gi))]
            faces.append(tuple(ids_i))
            if planar:
                uvs.append([(ss[i] / tile_m, (za - gi) / tile_m), ((ss[i] + a1 - a0) / tile_m, (zb - gj) / tile_m), ((ss[i] + a1 - a0) / tile_m, 0.0), (ss[i] / tile_m, 0.0)])
            else:
                uvs.append([(u0 + a0 * du, v0 + 0.57 + (za - gi) * du), (u0 + a1 * du, v0 + 0.57 + (zb - gj) * du), (u0 + a1 * du, v0 + 0.57), (u0 + a0 * du, v0 + 0.57)])
        ob_ = L._new_piece(D, L._next_name(D, name), verts, faces, uvs, [0] * len(faces), [mat], col="STRUCTURES", sharp_deg=50.0)
        objs.append(ob_)
        st_faces += len(faces)
    lums = [wv[3] for wv in win]
    stats = dict(rows=n, total_m=pl["total"], slabs=n_slab, faces=st_faces, w_min=float(pl["w"].min()), w_max=float(pl["w"].max()), w_mean=float(pl["w"].mean()),
                 win_lum_mean=float(np.mean(lums)), win_lum_max=float(np.max(lums)), zones=sorted({wv[2] for wv in win}), du=du, z_lo=float(zr.min()), z_hi=float(zr.max()))
    return objs, stats


# Phase 4: world-sized complete columns, adapted from the measured scratch fixture.
def _closed_cut_column(xy, z0, z1, rng, cuts=4):
    """Connected prism from actual5/6-side footprint plus four real corner cuts.

    The cuts truncate two upper and two lower corners without splitting a chip
    into a detached component. Horizontal cap remains one5..8-corner polygon.
    No stone is scaled to its height; its planes define final world dimensions.
    """
    xy=np.asarray(xy,float)
    cen=xy.mean(0); n=len(xy)
    normals=[]; offsets=[]
    for i in range(n):
        a,b=xy[i],xy[(i+1)%n]
        edge=b-a
        normal=np.array([edge[1],-edge[0],0.])
        normal/=np.linalg.norm(normal)
        if normal[:2]@(cen-a)>0: normal=-normal
        normals.append(normal); offsets.append(float(normal[:2]@a))
    normals.extend(([0.,0.,1.],[0.,0.,-1.]))
    offsets.extend((z1,-z0))
    width=max(np.linalg.norm(a-b) for a in xy for b in xy)
    first=rng.randrange(n)
    # Each fracture joins three actual incident edges. Radial planes can graze
    # an irregular footprint's second edge and produce a millimetre sliver.
    # Edge-point planes remove one real corner with bounded, visible chip legs.
    corners=(first,(first+n//2)%n,(first+1)%n,(first+1+n//2)%n)
    for i,iz in list(zip(corners,(1,1,0,0)))[:cuts]:
        corner=np.r_[xy[i],z1 if iz else z0]
        leg=rng.uniform(.16,.23)
        a=np.r_[xy[i]+leg*(xy[(i-1)%n]-xy[i]),corner[2]]
        b=np.r_[xy[i]+leg*(xy[(i+1)%n]-xy[i]),corner[2]]
        c=corner+np.array([0.,0.,-rng.uniform(.22,.36) if iz else rng.uniform(.22,.36)])
        normal=np.cross(b-a,c-a);normal/=np.linalg.norm(normal)
        if normal@(corner-a)<0:normal=-normal
        normals.append(normal);offsets.append(float(normal@a))
    result=ST.polyhedron(normals,offsets)
    if result is None: raise RuntimeError('column half-spaces are empty')
    vertices,planes=result
    faces=[f for _i,f in planes]
    if not ST.valid(vertices,planes,min_area=.0004,min_edge=.012):
        raise RuntimeError('column cut creates a sliver')
    # Direct across-corner width is measurement truth, rather than the input.
    actual=max(np.linalg.norm(a-b) for a in vertices[:,:2] for b in vertices[:,:2])
    if not (.6<=actual<=1.8): raise RuntimeError(f'actual column width{actual:g} outside.6..1.8m')
    return vertices,faces,dict(sides=n,width=actual,bottom=z0,top=z1,cuts=cuts)


def compact_ring_tile(D,path,top_z,bottom_z=-1.6,rows=3,seed=0,
                      width_m=(1.68,1.78),pitch_m=1.875,depth_pitch_m=3.25,
                      name='ROCK_WALL_COMPACT',max_faces=490,max_span_m=20.,clip=None,bottoms=None):
    """Compact full-height geological tile with actual depth and one cut/shaft.

    Three real staggered rows of broad 5/6-sided columns fit a ~9.375m path tile.
    They are whole closed connected stones. The single visible top fracture
    reduces wall triangle cost; no normals, material names or gates exempt it.
    Path should be oriented so the left side contains the backing rock. The
    caller must account for its ~8m real inward depth in clearance checks.
    Optional bottoms is the prior returned columns' exact top values, in the
    same grid/clip order, for physically supported upper fracture bands. Keep
    path, width, grid, seed and clip identical to preserve their real footprint.
    Each chunk is still independently checked by inherited rock_shape and PCA.
    This helper never flushes an underfilled thin tail or clips part of a stone.
    """
    if not (1<=max_faces<=490 and 1.9<=max_span_m<=20.):
        raise ValueError('compact rock caps remain <=490 faces and <=20m span')
    if not (.6<=width_m[0]<=width_m[1]<=1.8):raise ValueError('invalid world column widths')
    pts=np.asarray(path,float)[:,:2]
    seg=np.diff(pts,axis=0);lens=np.linalg.norm(seg,axis=1)
    acc=np.r_[0.,np.cumsum(lens)];length=float(acc[-1])
    if length<1e-6:raise ValueError('empty tile path')
    heights=np.full(len(pts),float(top_z)) if np.isscalar(top_z) else np.asarray(top_z,float)
    if len(heights)!=len(pts):raise ValueError('one height per path point required')
    count=max(1,int(round(length/pitch_m)))
    rng=random.Random(seed+7519);mb=PL._MB(('LK_BASALT',));specs=[]
    hex_columns=set(rng.sample(range(rows*count),max(1,round(rows*count*.25))))
    for row in range(rows):
        for k in range(count):
            # Stagger neighboring rows without appending a trailing narrow batch.
            sc=length*(k+.5+.15*(row%2))/count
            si=min(len(seg)-1,max(0,int(np.searchsorted(acc,sc,side='right')-1)))
            f=(sc-acc[si])/lens[si];tangent=seg[si]/lens[si]
            left=np.array([-tangent[1],tangent[0]])
            center=pts[si]+seg[si]*f+left*(.68+row*depth_pitch_m)
            width=rng.uniform(*width_m)
            angle=math.atan2(tangent[1],tangent[0])+math.pi/6+rng.uniform(-.12,.12)
            n=6 if row*count+k in hex_columns else 5
            xy=np.array([center+.5*width*np.array([math.cos(angle+math.tau*j/n),math.sin(angle+math.tau*j/n)]) for j in range(n)])
            if clip is not None and not all(clip(x,y) for x,y in xy):continue
            ht=float(heights[si]*(1-f)+heights[si+1]*f)-rng.uniform(.02,.55)
            if bottoms is not None and len(specs)>=len(bottoms):raise ValueError('missing per-shaft bottom')
            z0=float(bottoms[len(specs)]) if bottoms is not None else float(bottom_z)
            if ht<=z0+.4:raise ValueError('band has no legitimate column height')
            vertices,faces,spec=_closed_cut_column(xy,z0,ht,rng,cuts=1)
            vi,fi=len(mb.V),len(mb.F);mb.V.extend(tuple(v) for v in vertices)
            for face in faces:mb.f(tuple(vi+i for i in face))
            spec.update(grid_index=row*count+k,verts=[vi,len(mb.V)],faces=[fi,len(mb.F)])
            specs.append(spec)
    if not specs:raise RuntimeError('no complete legal columns')
    if bottoms is not None and len(bottoms)!=len(specs):raise ValueError('extra per-shaft bottoms')
    span=float(np.linalg.norm(np.ptp(np.array(mb.V)[:,:2],axis=0)))
    if len(mb.F)>max_faces or span>max_span_m:
        raise RuntimeError(f'compact tile exceeds {len(mb.F)}faces,{span:.3f}m span')
    mesh=PL._finalize(name,mb,'box',smooth_angle=None,library=False)
    ob=bpy.data.objects.new(name,mesh);PL._collection_for('ROCK_').objects.link(ob);PL._parent(D,ob)
    ob['lk_v3_columns']=json.dumps(specs);ob['lk_v3_world_sized']=True
    ob['lk_v3_compact_tile']=True
    return dict(object=ob,columns=specs,horizontal_span=span,path_length=length,
                rows=rows,columns_per_row=count,triangle_projection_per_m=(sum(len(f)-2 for f in mb.F)/length))


def _ring_tiles(cx, cy, th, radius, target_m=9.375):
    """Closed actual shoreline paths partitioned by distance, no thin final tail."""
    xy=np.c_[cx+radius*np.cos(th),cy+radius*np.sin(th)]
    xy=np.vstack([xy,xy[:1]])
    ds=np.linalg.norm(np.diff(xy,axis=0),axis=1);arc=np.r_[0.,np.cumsum(ds)]
    n=max(1,round(arc[-1]/target_m))
    for k in range(n):
        q=np.linspace(arc[-1]*k/n,arc[-1]*(k+1)/n,5)
        yield k,np.c_[np.interp(q,arc,xy[:,0]),np.interp(q,arc,xy[:,1])]


def near_wall(D,W,kinds_var):
    """Three real broad column rows, low full-height tiles; no scaled wall pieces."""
    cx,cy=P.m(W['cx'],W['cd']);th,rad=near_radius_profile(D,W)
    out=[]
    for k,path in _ring_tiles(cx,cy,th,rad):
        c=path.mean(0);a=math.atan2(c[1]-cy,c[0]-cx)
        h=22.5+3.0*math.sin(5*a+.2)
        if WINDOW[0]<=heading_from_tee(*c)<=WINDOW[1]:h=15.5+2.0*math.sin(3*a)
        r=compact_ring_tile(D,path,h,seed=102000+k,name=f'ROCK_WALL_BASALT_N_V3_{k:03d}')
        ob=r['object'];UV_STATS[ob.name]=L.author_basalt_paired_columns(ob,seed=104000+k,front_dir=(c[0]-cx,c[1]-cy),plain_author=author_uv)
        out.append(ob)
    return out,th,rad


def far_ring(D,W,kinds_var):
    """Continuous low ring with seven genuine narrow tall fractured ridge sectors."""
    cx,cy=P.m(W['cx'],W['cd']);th=np.linspace(0,math.tau,1440,endpoint=False)
    rad=np.full(len(th),.5*(W['r_inner']+W['r_outer'])*P.YD)
    out=[]
    for k,path in _ring_tiles(cx,cy,th,rad):
        c=path.mean(0);a=math.atan2(c[1]-cy,c[0]-cx)%math.tau
        in_window=WINDOW[0]<=heading_from_tee(*c)<=WINDOW[1]
        h=21.5+3*math.sin(9*a+.7)
        peak=abs((a-.2+math.pi/7)%(math.tau/7)-math.pi/7)<math.radians(4.3) and not in_window
        heights=[h,44.2,69.2] if peak else [h]
        if in_window:heights=[18.5+2*math.sin(4*a)]
        prior=None
        for j,top in enumerate(heights):
            r=compact_ring_tile(D,path,top,seed=101000+k,name=f'ROCK_WALL_BASALT_F_V3_{k:03d}_{j}',bottoms=None if prior is None else [c['top'] for c in prior['columns']])
            ob=r['object'];UV_STATS[ob.name]=L.author_basalt_paired_columns(ob,seed=105000+k,front_dir=(c[0]-cx,c[1]-cy),plain_author=author_uv)
            if prior is not None:
                ob['lk_v3_support_gap_m']=max(abs(c['bottom']-b['top']) for c,b in zip(r['columns'],prior['columns']))
            out.append(ob);prior=r
    return out


def make_lava_stack(D,s,top,seed):
    x,y=P.m(s['x'],s['d']);yaw=(seed*.618)%math.tau
    tangent=np.array([math.cos(yaw),math.sin(yaw)]);left=np.array([-tangent[1],tangent[0]])
    c=np.array([x,y])-left*3.9
    path=np.array([c-tangent*4.6875,c+tangent*4.6875])
    r=compact_ring_tile(D,path,top,bottom_z=-1.6,seed=seed,name=f'ROCK_BASALT_STACK_V3_{seed}')
    ob=r['object'];UV_STATS[ob.name]=L.author_basalt_paired_columns(ob,seed=seed+100,front_dir=(x,y),plain_author=author_uv)
    # Preserve world geometry while giving a real stack its design-centre origin.
    for v in ob.data.vertices:v.co.x-=x;v.co.y-=y
    ob.location=(x,y,0.);ob.data.update()
    return dict(obs=[ob],main=ob,nat_h=top,top=top,scale0=1.,sat_scale0=[],design=s,seed=seed)


def build_tee_platform(D,pad):
    """Closed stone body under the exact frozen grass square; real interior piers."""
    hull=pad_outline(pad);path=inset_polygon(hull,.75);closed=np.vstack([path,path[:1]])
    seg=np.diff(closed,axis=0);lens=np.linalg.norm(seg,axis=1);arc=np.r_[0.,np.cumsum(lens)]
    total=float(arc[-1]);count=round(total/1.2);rng=random.Random(10245)
    mb=PL._MB(('LK_BASALT',));columns=[];z=max(v.co.z for v in pad.data.vertices)+.0115
    for k in range(count):
        sc=total*(k+.5)/count;j=min(len(seg)-1,int(np.searchsorted(arc,sc,side='right')-1))
        t=(sc-arc[j])/lens[j];p=path[j]+seg[j]*t;tan=seg[j]/lens[j]
        width=rng.uniform(1.13,1.25);ang=math.atan2(tan[1],tan[0])+math.pi/6+rng.uniform(-.1,.1)
        n=6 if k%4==0 else 5
        xy=np.array([p+.5*width*np.array([math.cos(ang+math.tau*i/n),math.sin(ang+math.tau*i/n)]) for i in range(n)])
        vv,ff,spec=_closed_cut_column(xy,-1.6,z,rng,cuts=1);vi,fi=len(mb.V),len(mb.F)
        mb.V.extend(tuple(v) for v in vv)
        for f in ff:mb.f(tuple(vi+i for i in f))
        spec.update(verts=[vi,len(mb.V)],faces=[fi,len(mb.F)]);columns.append(spec)
    for gx in (-3.,-1.,1.,3.):
        for gy in (-2.1,0.,2.1):
            p=np.array([gx,gy]);width=rng.uniform(.95,1.3);ang=rng.uniform(-.2,.2);n=6 if rng.random()<.25 else 5
            xy=np.array([p+.5*width*np.array([math.cos(ang+math.tau*i/n),math.sin(ang+math.tau*i/n)]) for i in range(n)])
            vv,ff,spec=_closed_cut_column(xy,-1.6,z-.07,rng,cuts=1);vi,fi=len(mb.V),len(mb.F)
            mb.V.extend(tuple(v) for v in vv)
            for f in ff:mb.f(tuple(vi+i for i in f))
            spec.update(verts=[vi,len(mb.V)],faces=[fi,len(mb.F)]);columns.append(spec)
    me=PL._finalize('DRESS_PADRIM_V3',mb,'box',smooth_angle=None,library=False)
    ob=bpy.data.objects.new('DRESS_PADRIM_V3',me);PL._collection_for('DRESS_').objects.link(ob);PL._parent(D,ob)
    ob['lk_v3_columns']=json.dumps(columns);ob['lk_v3_world_sized']=True
    UV_STATS[ob.name]=L.author_basalt_paired_columns(ob,seed=10246,front_dir=(0.,-1.),plain_author=author_uv)
    return [ob],dict(rows=count,total_m=total,slabs=len(columns),faces=len(mb.F),w_min=min(c['width'] for c in columns),w_max=max(c['width'] for c in columns),w_mean=float(np.mean([c['width'] for c in columns])),win_lum_mean=float(texture_measure()['lum_mean']),win_lum_max=float(texture_measure()['lum_mean']),zones=list(range(len(ZONES))),du=.7/TILE,z_lo=z,z_hi=z)
