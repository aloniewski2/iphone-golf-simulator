"""BAG: fit the product to a hero (carry pose, behind the back, ONE strap over the hero's right shoulder, legs folded), weight it, bake maps, write the piece.
Blender -b --factory-startup --python bag_fit.py -- <Male|Female|both> [--skip-maps] [--res 2048]
Output: out/<Sex>/golf_bag/{Kit_Bag.bin, Kit_Bag_final.npz, Bag_N.png, Bag_M.png, piece.json}

Placement frame: product point p -> N world q = Rx(-lean) Rz(180) (p - P0) + T   (the product's back panel faces the hero's back, the pockets face away,
the product's +X strap lands on the hero's RIGHT (= -X in N)).  The socket Back_Bag is a child of UpperChest with identity (body-aligned) axes at q0 = T.
"""
import sys, os, json, math, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, acc_geo as G, acc_pipeline as PL, bag_canvas as CV, acc_fit as F
import bag as B, bag_geo as BG, bag_maps as BM, bag_export as BE
from bag_geo import unit, sstep

OUT = os.path.join(A.ROOT, 'out')
P0 = np.array([0.0, B.Shell.YB, 0.45])                       # product anchor (centre of the back panel at mid height)
CFG = dict(fist_w=1.0, route_file={}, crest_z0=1.455, crest_chest=0.0, crest_arm=0.0, weights_across=True, weights_mode='skin', route_mode='auto', pad_clear=0.0105, web_clear={'Male': 0.010, 'Female': 0.012}, span_lift=0.040,
           route={'Male': [(1.335, 12, .0105), (1.285, 13, .0105), (1.225, 16, .0105), (1.165, 22, .008), (1.115, 36, .006), (1.075, 58, .006), (1.050, 85, .006), (1.045, 112, .006), (1.055, 138, .006)],
                  'Female': [(1.335, 12, .0105), (1.285, 14, .0105), (1.235, 16, .0105), (1.190, 20, .008), (1.150, 32, .006), (1.120, 55, .006), (1.100, 80, .006), (1.100, 108, .006), (1.100, 135, .006)]},
           beta0=0.02, beta1=0.10, w_sigma=0.045, lean=-6.0, roll=3.0, band_target=0.020, tx=0.012, min_bag_motion=0.010, rim_top={'Male': 1.400, 'Female': 1.300}, min_bag=0.012, strap_clear=0.0105,
           xs={'Male': -0.130, 'Female': -0.114},
           front_dx={'Male': (-0.002, -0.010, -0.022, -0.038, -0.052), 'Female': (0.010, 0.020, 0.026, 0.028, 0.022)},
           front_z={'Male': (1.335, 1.288, 1.250, 1.222, 1.205), 'Female': (1.335, 1.290, 1.250, 1.212, 1.168)},
           tail_idx={'Male': 12, 'Female': 12},
           flank_z={'Male': (1.198, 1.192), 'Female': (1.100, 1.095)})
ALLOWED = ['UpperChest', 'Chest', 'Spine', 'Hips', 'RightShoulder', 'RightUpperArm']


class Placement:
    """product -> N world. lean < 0: top toward the spine (bottom swung out); roll > 0: top toward the hero's left (+X)."""
    def __init__(self, lean, t, roll=0.0):
        self.lean = lean; self.roll = roll; self.t = np.asarray(t, float)
        self.R = F.Ry(roll) @ F.Rx(-lean) @ F.Rz(180)

    def to_world(self, P): return (np.asarray(P) - P0) @ self.R.T + self.t
    def to_prod(self, Q): return (np.asarray(Q) - self.t) @ self.R + P0
    def dir_world(self, v): return np.asarray(v) @ self.R.T
    def dir_prod(self, v): return np.asarray(v) @ self.R


def solve_placement(hd, clear, S, sex):
    parts, _, _ = B.build_product('folded', straps=(), clubs=False, S=S, drop_hidden=False)
    P = np.concatenate([G.positions(p) for p in parts])
    rng = np.random.default_rng(2); sub = P[rng.choice(len(P), min(3000, len(P)), replace=False)]
    lean = CFG['lean']; rim = CFG['rim_top'][sex]
    top_prod = S.ZT + 0.003 + 0.0148
    tz = rim - (top_prod - P0[2]) * math.cos(math.radians(lean))
    lo, hi = 0.02, 0.40
    for _ in range(16):
        ty = 0.5 * (lo + hi)
        W = Placement(lean, (CFG['tx'], ty, tz)).to_world(sub)
        if clear(W).min() >= CFG['min_bag']: hi = ty
        else: lo = ty
    return Placement(lean, (CFG['tx'], hi, tz))


# --------------------------------------------------------------------------------------------- the strap path on the hero
def body_tree(hd):
    return BVHTree.FromPolygons([tuple(p) for p in hd.body_pos], [tuple(t) for t in hd.body_tri], all_triangles=True)


def cast(tree, o, d):
    r = tree.ray_cast(Vector(o), Vector(d))
    return None if r[0] is None else np.array(r[0])


def lateral_gap_point(tree, y, z, margin=0.0125):
    """strap centre under the arm: cast from the body centre toward the hero's right (-X). first hit = where the torso ends; if the next surface
    is the arm coming back (a gap), the centre is half way (at most `margin` off the torso), else just `margin` off the surface."""
    o = np.array([0.0, y, z]); d = np.array([-1.0, 0, 0])
    r1 = tree.ray_cast(Vector(o), Vector(d))
    if r1[0] is None: return np.array([-0.12, y, z])
    p1 = np.array(r1[0]); gap = 9.0
    r2 = tree.ray_cast(Vector(p1 + d * 1e-4), Vector(d))
    if r2[0] is not None and np.dot(np.array(r2[1]), d) < 0: gap = abs(r2[0][0] - p1[0])
    off = min(gap * 0.5, margin)
    return p1 + d * off


def strap_guides(hd, sex, pl, S, tree):
    """control points (world N) of the strap, a flag per point (1 = lies on the body, 0 = free), hints (unit normals, away from the body)."""
    xs = CFG['xs'][sex]
    # crest of the right trapezius at x = xs
    best = None
    for y in np.linspace(-0.10, 0.12, 45):
        r = cast(tree, (xs, y, 1.52), (0, 0, -1))
        if r is not None and (best is None or r[2] > best[2]): best = r
    crest = best
    pts = []; flag = []; hn = []
    L2 = S.L / 2
    # start: on the back panel, top region (product dx=0.075, z=0.700), pushed out of the panel
    start = pl.to_world(S.at(np.array([L2 - 0.075]), np.array([0.700]), 0.0085)[0])
    pts.append(start); flag.append(0); hn.append(unit(np.array([0, 0.5, 0.85])))
    # free span: halfway to the crest, above the back of the shoulder
    mid = 0.5 * (start + crest) + np.array([0, 0.0, 0.040])
    pts.append(mid); flag.append(0); hn.append(unit(np.array([-0.2, 0.2, 1.0])))
    pts.append(crest.copy()); flag.append(1); hn.append(np.array([0, 0, 1.0]))
    # down the front of the chest: heights and outward drift
    for k, (z, dx) in enumerate(zip(CFG['front_z'][sex], CFG['front_dx'][sex])):
        x = xs + dx
        r = cast(tree, (x, -0.7, z), (0, 1, 0))
        if r is None: r = np.array([x, -0.05, z])
        pts.append(r + np.array([0, -0.002, 0])); flag.append(1); hn.append(unit(np.array([-0.15, -1.0, 0.1])))
    # under the arm: the flank, then round to the back
    for z, y in zip(CFG['flank_z'][sex], (0.0, 0.045)):
        r = lateral_gap_point(tree, y, z)
        pts.append(r); flag.append(2); hn.append(unit(np.array([-1.0, 0.0, 0.0])))
    return pts, flag, hn


def wc(sex):
    v = CFG['web_clear']
    return v[sex] if isinstance(v, dict) else v


def torso_point(tree, hd, z, th_deg):
    """first surface hit from the torso axis at height z toward azimuth th (0 = front, 90 = hero's right side, 180 = back)."""
    P = hd.body_pos
    m = (np.abs(P[:, 2] - z) < 0.01) & (np.abs(P[:, 0]) < 0.10)
    cy = 0.5 * (P[m][:, 1].min() + P[m][:, 1].max())
    d = np.array([-math.sin(math.radians(th_deg)), -math.cos(math.radians(th_deg)), 0.0])
    r = tree.ray_cast(Vector((0.0, cy, z)), Vector(tuple(d)))
    return None if r[0] is None else np.array(r[0])


def torso_hit(tree, hd, z, th_deg):
    """like torso_point but also returns the (outward) surface normal at the hit"""
    P = hd.body_pos
    m = (np.abs(P[:, 2] - z) < 0.01) & (np.abs(P[:, 0]) < 0.10)
    cy = 0.5 * (P[m][:, 1].min() + P[m][:, 1].max())
    d = np.array([-math.sin(math.radians(th_deg)), -math.cos(math.radians(th_deg)), 0.0])
    r = tree.ray_cast(Vector((0.0, cy, z)), Vector(tuple(d)))
    if r[0] is None: return None, None
    n = np.array(r[1])
    if np.dot(n, d) < 0: n = -n
    return np.array(r[0]), n


def strap_guides2(hd, sex, pl, S, tree):
    """route 'low': over the crest, down the medial chest, then DIAGONALLY down to the rib-cage level where the arm leaves a gap, round the flank to the bag.
    returns control points (world N), flags (1 on body, 0 free), hints, per-guide skin offsets (m)."""
    xs = CFG['xs'][sex]
    best = None
    for y in np.linspace(-0.10, 0.12, 45):
        r = cast(tree, (xs, y, CFG['crest_z0']), (0, 0, -1))
        if r is not None and (best is None or r[2] > best[2]): best = r
    crest = best
    L2 = S.L / 2
    pts = []; flag = []; hn = []; off = []
    start = pl.to_world(S.at(np.array([L2 - 0.075]), np.array([0.700]), 0.0085)[0])
    pts.append(start); flag.append(0); hn.append(unit(np.array([0, 0.5, 0.85]))); off.append(CFG['pad_clear'])
    mid = 0.5 * (start + crest) + np.array([0, 0.0, CFG['span_lift']])
    pts.append(mid); flag.append(0); hn.append(unit(np.array([-0.2, 0.2, 1.0]))); off.append(CFG['pad_clear'])
    pts.append(crest.copy()); flag.append(1); hn.append(np.array([0, 0, 1.0])); off.append(CFG['pad_clear'])
    rt = CFG['route'][sex]
    rj = os.path.join(OUT, sex, 'golf_bag', CFG['route_file'].get(sex, 'route.json') if isinstance(CFG['route_file'], dict) else CFG['route_file'])
    if CFG['route_mode'] == 'auto' and os.path.exists(rj): rt = json.load(open(rj))
    for z, th, o in rt:
        r, nrm = torso_hit(tree, hd, z, th)
        if r is None: continue
        pts.append(r); flag.append(1)
        rad = np.array([-math.sin(math.radians(th)), -math.cos(math.radians(th)), 0.0])
        hn.append(unit(0.5 * unit(nrm) + 0.5 * rad))              # smooth blend of the skin normal and the radial direction (no flips in the armpit)
        off.append(o)
    return pts, flag, hn, off


def relax_path(ctrl, wbody, clear, d_target, iters=14, n_per=10):
    # d_target: scalar or per-control-point list
    """dense path through ctrl; the 'on-body' part is shrink-wrapped to the offset surface d_target (centre of the strap's body-side face),
    the free part is only pushed out where it would come closer than d_target."""
    P = BG.catmull(np.asarray(ctrl), n_per)
    idx = np.arange(len(P)) / n_per
    wb = np.interp(idx, np.arange(len(ctrl)), np.asarray(wbody, float))
    d_target = np.interp(idx, np.arange(len(ctrl)), np.broadcast_to(np.asarray(d_target, float), (len(ctrl),)))
    pin_i = [i * n_per for i, w in enumerate(wbody) if w > 1.5]
    pin_P = P[pin_i].copy()
    for it in range(iters):
        c, u = clear(P, with_dir=True)
        pull = np.where(wb > 1.5, 0.0, np.where(wb > 0.5, d_target - c, np.maximum(d_target - c, 0.0)))
        P = P + u * pull[:, None] * 0.8
        Q = P.copy(); Q[1:-1] = 0.25 * P[:-2] + 0.5 * P[1:-1] + 0.25 * P[2:]
        P = np.where((wb > 0.5)[:, None], 0.5 * P + 0.5 * Q, Q)
        if pin_i: P[pin_i] = pin_P
    c, u = clear(P, with_dir=True)
    return P, u, wb


def fit_strap_ctrl(hd, sex, pl, S, clear, tree):
    ti = CFG['tail_idx'][sex]
    if CFG['route_mode'] in ('low', 'auto'): pts, flag, hn, doff = strap_guides2(hd, sex, pl, S, tree)
    else:
        pts, flag, hn = strap_guides(hd, sex, pl, S, tree); doff = [CFG['strap_clear']] * len(pts)
    # re-entry on the bag's side wall (product ctrl idx 12..17), world
    prod_ctrl, prod_hint = B.product_strap_ctrl(S, +1)
    tail = pl.to_world(prod_ctrl[ti:]); tail_h = pl.dir_world(prod_hint[ti:])
    prev = pts[-1]
    corner = tail[0] + unit(prev - tail[0]) * 0.040 + np.array([0, 0, 0.012])
    guides = list(pts) + [corner, tail[0]]
    wflag = list(flag) + [0, 0]
    dense, u, wb = relax_path(guides, wflag, clear, list(doff) + [CFG['pad_clear'], CFG['pad_clear']])
    # hints: away from the body on the body part, blended to the guide hints on the free parts
    gh = np.array(hn + [tail_h[0], tail_h[0]])
    t = np.arange(len(dense)) / 10.0
    gh_d = np.stack([np.interp(t, np.arange(len(gh)), gh[:, j]) for j in range(3)], 1)
    hints = unit(np.where((wb > 0.5)[:, None], 0.85 * gh_d + 0.15 * u, 0.55 * gh_d + 0.45 * u))
    # smooth hints along the path
    for _ in range(6):
        h2 = hints.copy(); h2[1:-1] = 0.25 * hints[:-2] + 0.5 * hints[1:-1] + 0.25 * hints[2:]; hints = unit(h2)
    # sparsify: keep every 2nd dense sample as control points (the builder re-splines)
    sel = np.arange(0, len(dense), 2)
    ctrl_w = np.vstack([dense[sel], tail[1:]]); hint_w = np.vstack([hints[sel], tail_h[1:]])
    return ctrl_w, hint_w


def web_length(ctrl_w, z_front=1.262):
    """length of the strap measured from its lower end back to where the path first drops below z_front after the crest = where the narrow webbing starts."""
    d = BG.catmull(np.asarray(ctrl_w), 8)
    sa = BG.arclen(d)
    ic = int(np.argmax(d[:, 2]))
    j = ic + int(np.argmax(d[ic:, 2] < z_front))
    return float(sa[-1] - sa[j])


def conform_strap(ob_info_fn, ctrl_w, hint_w, pl, clear, S, iters=4, low_web=0.34, web_clear=0.012):
    """build the strap, check the clearance of the body-side face over its full width, lift the centre line where it is short; returns the final ctrl."""
    ctrl = ctrl_w.copy()
    for it in range(iters):
        ctrl_p = pl.to_prod(ctrl); hint_p = pl.dir_prod(hint_w)
        ob, info = B.build_strap('tmp_strap', ctrl_p, hint_p, low_web=low_web)
        C = pl.to_world(info['C']); tang, W, Nn = info['frame']
        Wn = pl.dir_world(W); Nw = pl.dir_world(Nn)
        hw, T0, crown, pad = B.strap_profile(info['sa'], info['tot'], low_web=low_web)
        worst = 0
        deficit = np.zeros(len(C))
        target = np.where(pad > 0.5, CFG['pad_clear'], web_clear) - 0.0015
        for x in np.linspace(-1, 1, 7):
            q = C + Wn * (hw * x)[:, None]
            c = clear(q)
            deficit = np.maximum(deficit, target - c)
        bpy.data.objects.remove(ob)
        if deficit.max() < 0.0008: break
        # lift the dense centre line along its normals where short, map back onto the control points by arclength
        sa_c = BG.arclen(ctrl); sa_i = info['sa']
        lift = np.interp(sa_c / sa_c[-1], sa_i / sa_i[-1], deficit)
        ctrl = ctrl + hint_w * (lift * 1.05)[:, None]
    return ctrl


# --------------------------------------------------------------------------------------------- weights
def body_weights_at(hd, tree, pts):
    """per point: weights over ALLOWED bones taken from the nearest body vertex, renormalised (fallback UpperChest)."""
    ai = [hd.idx(n) for n in ALLOWED]
    out = np.zeros((len(pts), len(ALLOWED)))
    for i, p in enumerate(pts):
        loc, nrm, fi, d = tree.find_nearest(Vector(p))
        tri = hd.body_tri[fi]
        v = tri[np.argmin(np.linalg.norm(hd.body_pos[tri] - np.array(loc), axis=1))]
        for k in range(4):
            if hd.bw[v, k] > 0 and hd.bi[v, k] in ai: out[i, ai.index(hd.bi[v, k])] += hd.bw[v, k]
    s = out.sum(1)
    out[s < 0.15] = 0; out[s < 0.15, 0] = 1.0
    return out / np.maximum(out.sum(1, keepdims=True), 1e-9)


def strap_weights_arrays(hd, tree, pos, sa, tot, s_join, ev=None, sx=None, hwid=None):
    """weights (n, 1 + len(ALLOWED)) for strap vertices: socket at both ends, the body's own weights (smoothed along the strap) in between."""
    nb = 1 + len(ALLOWED)
    if CFG['weights_mode'] == 'skin' and ev is not None:
        col = np.round(sx / np.maximum(hwid, 1e-6) * 3).astype(np.int64) if (CFG['weights_across'] and sx is not None) else None
        bwts = ev.fit_rows(hd, tree, pos, sa, ALLOWED, col=col)              # weights fitted to the real skin motion over the critical clips
    else:
        bwts = body_weights_at(hd, tree, pos)
        key = np.round(sa / 2e-4).astype(np.int64)
        uk, inv = np.unique(key, return_inverse=True)
        row_sa = np.array([sa[inv == r].mean() for r in range(len(uk))])
        row_w = np.array([bwts[inv == r].mean(0) for r in range(len(uk))])
        sig = CFG['w_sigma']
        sm = np.zeros_like(row_w)
        for r in range(len(uk)):
            w = np.exp(-((row_sa - row_sa[r]) / sig) ** 2); sm[r] = (w[:, None] * row_w).sum(0) / w.sum()
        bwts = sm[inv]
        bwts = bwts / np.maximum(bwts.sum(1, keepdims=True), 1e-9)
    if CFG['crest_arm'] > 0:                                              # shoulder-crest rows: part of the clavicle weight goes to the upper arm (it rides up with a raised arm)
        rs = ALLOWED.index('RightShoulder'); ra = ALLOWED.index('RightUpperArm')
        mv = bwts[:, rs] * CFG['crest_arm'] * (bwts[:, rs] > 0.4)
        bwts[:, rs] -= mv; bwts[:, ra] += mv
    if CFG['crest_chest'] > 0:                                            # ... or toward UpperChest (the strap stays with the back while the arm goes up)
        rs = ALLOWED.index('RightShoulder'); rc = ALLOWED.index('UpperChest')
        mv = bwts[:, rs] * CFG['crest_chest'] * (bwts[:, rs] > 0.4)
        bwts[:, rs] -= mv; bwts[:, rc] += mv
    beta = np.maximum(1 - sstep(CFG['beta0'], CFG['beta1'], sa), sstep(s_join - 0.14, s_join + 0.01, sa))      # 1 = socket
    Wb = np.zeros((len(pos), nb)); Wb[:, 1:] = bwts
    return beta[:, None] * np.eye(nb)[0][None, :] + (1 - beta)[:, None] * Wb


def top4(W):
    n = len(W)
    bi = np.zeros((n, 4), np.int32); bw = np.zeros((n, 4), np.float32)
    for i in range(n):
        o = np.argsort(-W[i])[:4]
        w = W[i][o]; w = w / w.sum()
        bi[i] = o; bw[i] = w
    return bi, bw


def strap_weights(hd, tree, piece, tot, s_join, ev=None):
    """bi (n,4), bw (n,4) over bones [Back_Bag, *ALLOWED]; bag parts weight 1 on the socket; strap vertices blend the socket into the body weights."""
    n = len(piece['pos'])
    nb = 1 + len(ALLOWED)
    W = np.zeros((n, nb)); W[:, 0] = 1.0
    strap_v = vertex_of(piece, {B.K['STRAP']})
    W[strap_v] = strap_weights_arrays(hd, tree, piece['pos'][strap_v], piece['fields']['sa'][strap_v], tot, s_join, ev=ev, sx=piece['fields']['sx'][strap_v], hwid=piece['fields']['hwid'][strap_v])
    return top4(W)


def vertex_of(piece, kinds):
    m = np.isin(piece['kind'], list(kinds))
    return np.unique(piece['tri'][m])


def inner_vertices(piece, kind):
    m = (piece['kind'] == kind) & (piece['zone'] == 2)
    return np.unique(piece['tri'][m])


# --------------------------------------------------------------------------------------------- main
def head_clear(hd, pts):
    hv = np.unique(hd.body_tri[(hd.body_pos[hd.body_tri][..., 2] > hd.rest_pos['Head'][2] - 0.06).all(1)])
    tris = hd.body_tri[(hd.body_pos[hd.body_tri][..., 2] > hd.rest_pos['Head'][2] - 0.06).all(1)]
    tree = BVHTree.FromPolygons([tuple(hd.body_pos[i]) for i in hv], [tuple(np.searchsorted(hv, t)) for t in tris], all_triangles=True)
    top = pts[pts[:, 2] > hd.rest_pos['Head'][2] - 0.2]
    return min((tree.find_nearest(Vector(p))[3] for p in top[::3]), default=9.0)


def get_placement(sex):
    """hang placement (negative lean, small roll): cached in out/<Sex>/golf_bag/placement.json; --research recomputes."""
    pj = os.path.join(OUT, sex, 'golf_bag', 'placement.json')
    cfg = [CFG['lean'], CFG['roll'], CFG['band_target'], CFG['rim_top'][sex], CFG['tx']]
    if os.path.exists(pj) and '--research' not in sys.argv:
        d = json.load(open(pj))
        if d['cfg'] == cfg: return Placement(d['lean'], d['t'], d['roll']), d['report']
    import bag_hang as H
    pl, rep = H.solve_hang(sex, CFG['lean'], CFG['roll'], rim=CFG['rim_top'][sex], band_target=CFG['band_target'])
    json.dump(dict(cfg=cfg, lean=pl.lean, roll=pl.roll, t=[float(v) for v in pl.t], report={k: (float(v) if isinstance(v, (np.floating,)) else v) for k, v in rep.items()}), open(pj, 'w'))
    return pl, rep


def fit(sex, maps=True, res=4096, verbose=True):
    t0 = time.time()
    hd = A.HeroData(sex)
    clear = F.Clearance(hd.body_pos, hd.body_tri)
    tree = body_tree(hd)
    S = B.Shell()
    pl, mrep = get_placement(sex)
    if verbose: print(sex, 'hang placement', mrep)
    if verbose: print(sex, 'placement lean', pl.lean, 't', np.round(pl.t, 4), round(time.time() - t0, 1), 's')
    ctrl_w, hint_w = fit_strap_ctrl(hd, sex, pl, S, clear, tree)
    low_web = web_length(ctrl_w)
    ctrl_w = conform_strap(None, ctrl_w, hint_w, pl, clear, S, low_web=low_web, web_clear=wc(sex))
    ctrl_p, hint_p = pl.to_prod(ctrl_w), pl.dir_prod(hint_w)
    parts, S, infos = B.build_product('folded', straps=(1,), clubs=True, S=S, strap_override={1: (ctrl_p, hint_p)}, drop_hidden=True, low_web=low_web)
    piece, joined = BE.make_piece(parts)
    tot = infos[1]['tot']
    # where the strap re-enters the bag side wall (arclength along the strap) = nearest strap sample to the first tail control point
    tail0 = ctrl_p[len(ctrl_p) - (len(B.product_strap_ctrl(S, 1)[0]) - CFG['tail_idx'][sex])]
    s_join = float(infos[1]['sa'][np.argmin(np.linalg.norm(infos[1]['C'] - tail0, axis=1))])
    if verbose: print('piece', len(piece['pos']), 'verts', len(piece['tri']), 'tris, px/mm %.2f' % (res / piece['S'] / 1000), 'strap len %.3f join %.3f' % (tot, s_join), round(time.time() - t0, 1), 's')
    # to the hero's world
    P = pl.to_world(piece['pos']); Nn = pl.dir_world(piece['nrm'])
    piece = dict(piece); piece['pos'] = P; piece['nrm'] = Nn
    import bag_strap_eval as SE
    ev = SE.StrapEval(sex)
    bi, bw = strap_weights(hd, tree, piece, tot, s_join, ev=ev)
    Wm = F.rt4(np.eye(3), A.N_to_unity(pl.to_world(P0)))
    bones = ['Back_Bag'] + ALLOWED
    bind = [F.bindpose_for(hd, Wm)] + [hd.bind[hd.idx(n)] for n in ALLOWED]
    out_dir = os.path.join(OUT, sex, 'golf_bag'); os.makedirs(out_dir, exist_ok=True)
    entry = PL.write_piece(out_dir, 'Kit_Bag', piece, bones, bi, bw, bind, res, ['Kit_Bag', 'Kit_Buckle'], detail='bag')
    entry['name'] = 'golf_bag'
    # numbers
    c_all = clear(P)
    sv = vertex_of(piece, {B.K['STRAP']}); bv = np.setdiff1d(np.arange(len(P)), sv)
    inn = inner_vertices(piece, B.K['STRAP'])
    cs_in = clear(P[inn])
    sa_in = piece['fields']['sa'][inn]
    key = np.round(sa_in / 2e-4).astype(np.int64)
    uk, inv = np.unique(key, return_inverse=True)
    row_min = np.array([cs_in[inv == r].min() for r in range(len(uk))])
    onb = row_min < 0.016                                                  # cross-sections lying on the body (within 16 mm of the skin)
    back = np.nonzero(np.isin(np.arange(len(P)), vertex_of(piece, {B.K['SHELL']})) & (piece['nrm'][:, 1] < -0.7))[0]
    cb = c_all[back]
    rep = dict(min_clear_bag_mm=round(float(c_all[bv].min()) * 1000, 2), min_clear_strap_mm=round(float(c_all[sv].min()) * 1000, 2),
               min_clear_strap_inner_mm=round(float(cs_in.min()) * 1000, 2),
               strap_on_body_rows=int(onb.sum()), strap_on_body_min_mm=round(float(row_min[onb].min()) * 1000, 2), strap_on_body_mean_mm=round(float(row_min[onb].mean()) * 1000, 2),
               strap_on_body_max_mm=round(float(row_min[onb].max()) * 1000, 2),
               bag_back_panel_gap_mm=dict(min=round(float(cb.min()) * 1000, 1), median=round(float(np.median(cb)) * 1000, 1), max=round(float(cb.max()) * 1000, 1)),
               tris=int(len(piece['tri'])), verts=int(len(P)), px_per_mm=round(res / piece['S'] / 1000, 2), px_per_mm_by_kind=BE.px_by_kind(piece, res), texture=res, metres_per_uv=float(piece['S']),
               strap_length_m=round(float(tot), 3), strap_second='omitted from the exported mesh (it would lie between the bag and the hero)',
               dropped_hidden_parts=['BackPocket', 'Hub', 'second strap', 'base bottom cap'], legs='folded',
               bag_top_z=round(float(pl.to_world(np.array([[0, B.Shell.YB, B.Shell.ZT + .0178]]))[0, 2]), 3),
               bag_bottom_z=round(float(pl.to_world(np.array([[0, B.Shell.YB, 0.0]]))[0, 2]), 3), lean_deg=pl.lean, roll_deg=pl.roll,
               head_min_mm_rest=round(float(head_clear(hd, P[bv])) * 1000, 1))
    if verbose: print(sex, json.dumps(rep))
    pos_l, quat, scl = F.local_pose_under(hd, 'UpperChest', Wm)
    meta = dict(item='golf_bag', sex=sex, slot='BackCarry', npz='Kit_Bag_final.npz', tag='Bag', pieces=['Kit_Bag'], parent_bone='UpperChest', socket='Back_Bag',
                socket_local_position=pos_l, socket_local_rotation=quat,
                fit=dict(bones=bones, hang_placement=mrep, lean_deg=pl.lean, roll_deg=pl.roll, T_N=[float(v) for v in pl.t], P0_product=[float(v) for v in P0], strap_side='hero right shoulder', socket_origin_N=[float(v) for v in pl.to_world(P0)]),
                fit_report=rep, piece=entry, entries={'Kit_Bag': entry}, tags={'Kit_Bag': 'Bag'})
    json.dump(meta, open(os.path.join(out_dir, 'piece.json'), 'w'), indent=1)
    if maps:
        hp, ht = F.region(hd, 0.55)
        ao = BE.make_ao(piece, extra=(hp, ht))
        CV.bake(os.path.join(out_dir, 'Kit_Bag_final.npz'), out_dir, 'Bag', BM.details_bag, ao=ao)
    if verbose: print('wrote', out_dir, round(time.time() - t0, 1), 's')
    return rep


def main():
    a = A.args_after_dashes()
    which = a[0] if a else 'both'
    res = int(a[a.index('--res') + 1]) if '--res' in a else 4096
    if '--lean' in a: CFG['lean'] = float(a[a.index('--lean') + 1])
    for sex in (['Male', 'Female'] if which == 'both' else [which]):
        fit(sex, maps='--skip-maps' not in a, res=res)


if __name__ == '__main__':
    main()
