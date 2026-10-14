"""GLOVE fit: retarget the product's inner surface S_in to a hero's hand.
 1. landmarks: ray casts from matching spine stations (product skeleton / hero skeleton) hit the product S_in and the hero skin -> surface correspondences;
 2. a thin-plate-spline warp of the whole product through those correspondences (hero skin + clearance as the target);
 3. clearance iterations ('hug'): smooth displacement of S_in so it sits c* above the skin everywhere (cuff gets a little slack).
All in the hero's hand frame G (glove_hand.Hand); numpy + Blender BVH (mathutils)."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, acc_fit as F
import glove_sdf as S, glove_hand as GH, glove_mesh as M, glove_curves as C

MM = 1e-3
CLEAR = 0.0010                       # target distance inner surface -> skin
EASE_NEAR = 0.0016                   # extra slack right at the cuff edge
EASE_WRIST = 0.0016                  # extra slack over the wrist (the wrist bends 60 degrees in a putt: LBS differences between glove and skin need room)
EASE_SPAN = 0.060


def unit(v): return S.unit(v)


class HeroSkin:
    """the hero's arm/hand skin in G: region mesh, BVH, bone-weight helpers."""
    def __init__(self, sex, side='Left'):
        self.hd = A.HeroData(sex); self.sex, self.side = sex, side
        self.H = GH.Hand(self.hd, side)
        hd = self.hd
        names = [f'{side}LowerArm', f'{side}Hand'] + self.H.names
        self.names = names
        # skin weights of the GOLF hero body (53 real bones, no runtime ArmBlend bones); the mesh and bind poses are identical to the match body
        gb = A.cv2io.Hero('GolfKitHero_' + sex).body
        self.gbones = list(gb.bones); self.gbody = gb
        W = np.zeros((len(hd.body_pos), len(self.gbones)))
        for k in range(4): np.add.at(W, (np.arange(len(W)), gb.bi[:, k]), gb.bw[:, k])
        self.W = W
        self.widx = {n: i for i, n in enumerate(self.gbones)}
        arm = W[:, [self.widx[n] for n in names]].sum(1) > .5
        self.arm = arm
        keep = arm[hd.body_tri].all(1)
        t = hd.body_tri[keep]
        used = np.unique(t); remap = -np.ones(len(arm), np.int64); remap[used] = np.arange(len(used))
        self.ids = used
        self.P = self.H.to_G(hd.body_pos[used])
        self.T = remap[t]
        if side == 'Right': self.T = self.T[:, ::-1]                      # mirrored frame: keep outward winding
        self.tree = BVHTree.FromPolygons([tuple(p) for p in self.P], [tuple(x) for x in self.T], all_triangles=True)
        self.clear = F.Clearance(self.P, self.T)
        Hh = self.H
        self.J = {f: [Hh.to_G(Hh.J[f + s]) for s in GH.SEGS] for f in GH.FINGERS}
        self.wrist = np.zeros(3)
        self.elbow = Hh.to_G(Hh.elbow)
        self.apex = {}
        for f in GH.FINGERS:
            P1, P2 = self.J[f][1], self.J[f][2]
            d = unit(P2 - P1)
            m = W[used][:, self.widx[f'{side}{f}Distal']] > .3
            proj = (self.P[m] - P2) @ d
            self.apex[f] = P2 + d * max(float(proj.max()), 0.012)

    def ray(self, o, d, maxd=.25):
        r = self.tree.ray_cast(Vector(o), Vector(d), maxd)
        return None if r[0] is None else (np.array(r[0]), np.array(r[1]))


def polyline_point(P, u):
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)])
    s = u * cum[-1]
    i = int(np.clip(np.searchsorted(cum, s, side='right') - 1, 0, len(seg) - 1))
    t = (s - cum[i]) / max(seg[i], 1e-12)
    p = P[i] * (1 - t) + P[i + 1] * t
    return p, unit(P[i + 1] - P[i])


def frame_finger(t):
    e2 = unit(np.array([0, 0, 1.0]) - t * t[2]); e1 = np.cross(t, e2); return e1, e2


def frame_thumb(t):
    ea = unit(np.cross(t, np.array([0, 1.0, 0]))); eb = np.cross(t, ea); return ea, eb


def frame_main(t):
    return frame_finger(t)


def recentre(hs, p, t, radius, bones=None, slab=.007):
    """move a hero spine point p (tangent t) to the centroid of the skin cross-section around it (vertices in a thin slab, within `radius`, optionally of given bones)"""
    d = hs.P - p
    along = d @ t
    rad = np.linalg.norm(d - np.outer(along, t), axis=1)
    m = (np.abs(along) < slab) & (rad < radius)
    if bones is not None: m &= bones
    if m.sum() < 4: return p
    c = hs.P[m].mean(0)
    return c - t * ((c - p) @ t)


def product_spines(sk):
    """product spines (G): fingers [P0..P3, apex], thumb, and the main axis"""
    sp = {}
    names = ['Index', 'Middle', 'Ring', 'Little']
    for nm, ch in zip(names, sk['fingers']):
        u = unit(ch.P[-1] - ch.P[-2]); sp[nm] = np.vstack([ch.P, ch.P[-1] + u * ch.r[-1]])
    th = sk['thumb']; u = unit(th.P[-1] - th.P[-2]); sp['Thumb'] = np.vstack([th.P, th.P[-1] + u * th.r[-1]])
    return sp


def hero_spines(hs):
    sp = {}
    for f in GH.FINGERS:
        sp[f] = np.vstack([hs.J[f][0], hs.J[f][1], hs.J[f][2], hs.apex[f]])
    return sp


def landmarks(sk, Vp, Tp, hs, clear=CLEAR, verbose=True):
    """returns (X product points, Y hero targets) lists and a log"""
    prod = C.Surf(Vp, Tp)
    X, Y, tag = [], [], []
    psp = product_spines(sk); hsp = hero_spines(hs)
    # ---- fingers and thumb: 4 rays at stations
    fr = {'Thumb': frame_thumb}
    for f in GH.FINGERS:
        Pp, Ph = psp[f], hsp[f]
        us = [0.04, 0.12, 0.22, 0.32, 0.43, 0.54, 0.65, 0.75, 0.84, 0.92, 0.97] if f == 'Thumb' else [0.24, 0.33, 0.43, 0.54, 0.65, 0.75, 0.84, 0.92, 0.97]
        fn = fr.get(f, frame_finger)
        base_us = [] if f == 'Thumb' else [0.0, 0.06, 0.12, 0.18]
        for u in base_us + us:
            pp, tp = polyline_point(Pp, u); ph, th = polyline_point(Ph, u)
            e1p, e2p = fn(tp); e1h, e2h = fn(th)
            dirs = ((e1p, e1h), (-e1p, -e1h), (e2p, e2h), (-e2p, -e2h))
            if u in base_us: dirs = dirs[2:]                      # at the finger base only the back and palm sides are well defined
            for (a, b) in dirs:
                hp = prod.ray(pp, a, .08)
                hh = hs.ray(ph, b, .08)
                if hp is None or hh is None: continue
                X.append(hp[2]); Y.append(hh[0] + b * clear); tag.append(f)
        # apex (end of the finger along the axis)
        pp, tp = Pp[-1], unit(Pp[-1] - Pp[-2]); ph, th = Ph[-1], unit(Ph[-1] - Ph[-2])
        hp = prod.ray(Pp[-2], tp, .12); hh = hs.ray(Ph[-2], th, .12)
        if hp is not None and hh is not None:
            X.append(hp[2]); Y.append(hh[0] + th * clear); tag.append(f + '_apex')
    # ---- main spine: forearm (absolute distance above the wrist) + palm (normalised)
    axp = np.array([0, -1.0, 0]); axh = unit(hs.elbow - hs.wrist)
    mp = sk['fingers'][1].P[0]; mh = hs.J['Middle'][0]
    hd = hs.hd; side = hs.side
    arm_only = (hs.W[hs.ids][:, hs.widx[f'{side}LowerArm']] + hs.W[hs.ids][:, hs.widx[f'{side}Hand']]) > .6
    mains = []
    for t in np.arange(0.0, 0.0601, 0.01):
        ph = recentre(hs, axh * t, -axh, .12, arm_only)
        mains.append((axp * t, ph, axp * -1.0, -axh))
    for u in np.linspace(0.10, 1.0, 9):
        pp = mp * u; ph0 = mh * u; th = unit(mh)
        ph = recentre(hs, ph0, th, .09, arm_only, slab=.005)
        mains.append((pp, ph, unit(mp), th))
    nmiss = 0
    for (pp, ph, tp, th) in mains:
        e1p, e2p = frame_main(tp); e1h, e2h = frame_main(th)
        for k in range(8):
            ang = k * math.pi / 4
            a = math.cos(ang) * e1p + math.sin(ang) * e2p; b = math.cos(ang) * e1h + math.sin(ang) * e2h
            hp = prod.ray(pp, a, .12); hh = hs.ray(ph, b, .16)
            if hp is None or hh is None: nmiss += 1; continue
            X.append(hp[2]); Y.append(hh[0] + b * clear); tag.append('main')
    if verbose: print('main-spine rays missed', nmiss)
    X, Y = np.array(X), np.array(Y)
    if verbose: print('landmarks', len(X), 'fingers', sum(1 for t in tag if t != 'main'), 'main', sum(1 for t in tag if t == 'main'))
    return X, Y, tag


class TPS:
    def __init__(self, X, Y, lam=2e-6):
        n = len(X)
        K = np.linalg.norm(X[:, None] - X[None], axis=2)
        Pm = np.hstack([np.ones((n, 1)), X])
        Amat = np.zeros((n + 4, n + 4)); Amat[:n, :n] = K + lam * np.eye(n); Amat[:n, n:] = Pm; Amat[n:, :n] = Pm.T
        rhs = np.zeros((n + 4, 3)); rhs[:n] = Y
        sol = np.linalg.solve(Amat, rhs)
        self.X, self.w, self.a = X, sol[:n], sol[n:]

    def __call__(self, P):
        out = np.zeros_like(P)
        for a in range(0, len(P), 2000):
            K = np.linalg.norm(P[a:a + 2000, None] - self.X[None], axis=2)
            out[a:a + 2000] = K @ self.w + np.hstack([np.ones((len(K), 1)), P[a:a + 2000]]) @ self.a
        return out


def ease_field(dcuff, tipf=None):
    """target clearance (m) per S_in vertex: 1 mm everywhere, a little slack at the cuff (so it opens over the wrist)"""
    return CLEAR + EASE_NEAR * SH_smooth(1 - dcuff / 0.018) + EASE_WRIST * SH_smooth(1 - dcuff / EASE_SPAN)


def SH_smooth(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


def signed_clearance(V, T, hs, fn, max_ray=.05, deep=.009):
    """signed distance to the skin per vertex (+ outside) and the direction to move along: rays along +/- the vertex normal (stable), nearest-point for vertices that
    are deep inside / have no usable ray hit (those must leave the volume by the shortest way)."""
    n = S.vertex_normals(V, T)
    tree = hs.tree
    c = np.zeros(len(V)); d = n.copy(); ok = np.zeros(len(V), bool)
    for i in range(len(V)):
        best = None
        for sgn in (1.0, -1.0):
            r = tree.ray_cast(Vector(V[i]), Vector(n[i] * sgn), max_ray)
            if r[0] is None: continue
            if np.dot(fn[r[2]], n[i]) < .3: continue
            if best is None or r[3] < best[0]: best = (r[3], sgn)
        if best is not None and best[0] < deep:
            c[i] = -best[0] if best[1] > 0 else best[0]; ok[i] = True
    if (~ok).any():
        cc, uu = hs.clear(V[~ok], with_dir=True)
        c[~ok] = cc; d[~ok] = uu
    return c, d, ok


def crevice_mask(V, hs, radius=.013, cos_open=0.0):
    """glove vertices sitting between two skin surfaces that face each other (finger webs, thumb against the palm): there the glove must bridge, never be pulled in"""
    from mathutils.kdtree import KDTree
    if not hasattr(hs, '_kd'):
        hs._kd = KDTree(len(hs.P))
        for i, p in enumerate(hs.P): hs._kd.insert(tuple(p), i)
        hs._kd.balance(); hs._nrm = S.vertex_normals(hs.P, hs.T)
    out = np.zeros(len(V), bool)
    for i, p in enumerate(V):
        res = hs._kd.find_range(tuple(p), radius)
        if len(res) < 3: continue
        idx = np.array([r[1] for r in res])
        d = p - hs.P[idx]; dn = np.linalg.norm(d, axis=1)
        dirs = d / np.maximum(dn, 1e-9)[:, None]
        seen = (dirs * hs._nrm[idx]).sum(1) > .15
        if seen.sum() < 3: continue
        D = dirs[seen]; m = D.mean(0); m /= np.linalg.norm(m) + 1e-9
        if (D @ m).min() < cos_open: out[i] = True
    return out


def fold_faces(V, T, thr=.3):
    fn = np.cross(V[T[:, 1]] - V[T[:, 0]], V[T[:, 2]] - V[T[:, 0]]); fn = fn / (np.linalg.norm(fn, axis=1, keepdims=True) + 1e-15)
    vn = S.vertex_normals(V, T)
    return np.nonzero((fn * vn[T].mean(1)).sum(1) < thr)[0]


def repair_folds(V, T, hs, target, rounds=5, rings=2):
    """smooth the neighbourhood of folded faces (a membrane bridging a tight crease), then restore the clearance by pushing out only"""
    E = S.edges_of(T); n = len(V)
    for r in range(rounds):
        bad = fold_faces(V, T)
        if len(bad) == 0: break
        mask = np.zeros(n, bool); mask[np.unique(T[bad])] = True
        for _ in range(rings):
            m2 = mask.copy(); m2[E[mask[E[:, 0]], 1]] = True; m2[E[mask[E[:, 1]], 0]] = True; mask = m2
        V = M.laplace_smooth(V, T, ~mask, lam=.5, iters=8)
        for _ in range(3):
            cc, uu = hs.clear(V, with_dir=True)
            push = np.clip(target - 0.0002 - cc, 0, .004) * mask
            V = V + uu * push[:, None]
    return V


def relax_folds(V, T, hs, floor=0.0005, rings=4, iters=20, rounds=8):
    """folded faces (tight finger webs / thumb creases the 4 mm mesh cannot follow) are smoothed away together with their neighbourhood; the clearance floor is relaxed there
    (the lining may come as close as `floor` to the skin) because a crumpled wall looks far worse than a fraction of a millimetre of ease"""
    E = S.edges_of(T); n = len(V)
    for r in range(rounds):
        bad = fold_faces(V, T)
        if len(bad) == 0: break
        mask = np.zeros(n, bool); mask[np.unique(T[bad])] = True
        for _ in range(rings):
            m2 = mask.copy(); m2[E[mask[E[:, 0]], 1]] = True; m2[E[mask[E[:, 1]], 0]] = True; mask = m2
        for it in range(iters):
            V = M.laplace_smooth(V, T, ~mask, lam=.5, iters=1)
            cc, uu = hs.clear(V, with_dir=True)
            V = V + uu * (np.clip(floor - cc, 0, .004) * mask)[:, None]
    return V


def hug(V, T, hs, target, iters=12, factor=.8, lim=.006, smooth_iters=4, fixed=None, polish=8, loop=None, verbose=False):
    """shrink-wrap to 'target distance from the skin'; smooth displacement; then a polish phase (tangential smoothing that never lets a vertex sink below the target)"""
    stats = []
    fixedm = np.zeros(len(V), bool) if fixed is None else fixed
    ftri = hs.P[hs.T]
    fn = np.cross(ftri[:, 1] - ftri[:, 0], ftri[:, 2] - ftri[:, 0]); fn = fn / (np.linalg.norm(fn, axis=1, keepdims=True) + 1e-15)
    for it in range(iters):
        c, d, ok = signed_clearance(V, T, hs, fn)
        need = np.clip((target - c) * factor, -lim, lim)
        crev = crevice_mask(V, hs)
        need[crev & (need < 0)] = 0                         # bridge crevices, never pull into them
        disp = d * need[:, None]
        disp[fixedm] = 0
        disp = M.laplace_smooth(disp, T, fixedm, lam=.5, iters=smooth_iters)
        V = V + disp
        stats.append((float(np.abs(c - target).mean()), float(c.min()), float(np.percentile(c, 2)), float(np.percentile(c, 98)), int((~ok).sum())))
    def loop_smooth(V):
        if loop is None: return V
        L = np.asarray(loop); P = V[L]
        V = V.copy(); V[L] = .5 * P + .25 * (np.roll(P, 1, 0) + np.roll(P, -1, 0))
        return V
    for it in range(polish):
        V = M.laplace_smooth(V, T, fixedm, lam=.35, iters=1)
        V = loop_smooth(V)
        cc, uu = hs.clear(V, with_dir=True)
        push = np.clip(target - 0.0003 - cc, 0, .004)
        V = V + uu * push[:, None]
    for it in range(4):                                   # final push-out only (never smooth after this): clearance first
        cc, uu = hs.clear(V, with_dir=True)
        push = np.clip(target - 0.0002 - cc, 0, .004)
        V = V + uu * push[:, None]
    c, d, ok = signed_clearance(V, T, hs, fn)
    stats.append((float(np.abs(c - target).mean()), float(c.min()), float(np.percentile(c, 2)), float(np.percentile(c, 98)), int((~ok).sum())))
    return V, stats


def uncovered(hs, O, T, axial_max=0.030, maxd=.03, tree=None):
    """skin vertices (hand + forearm above the cuff plane) that are NOT enclosed by the glove's outer surface: a ray from the vertex along its outward normal finds no glove wall.
    Returns (indices into hs.P, distances to the glove surface for those (nearest-point))."""
    if tree is None: tree = BVHTree.FromPolygons([tuple(p) for p in O], [tuple(t) for t in T], all_triangles=True)
    nrm = S.vertex_normals(hs.P, hs.T)
    ax = unit(hs.elbow - hs.wrist)
    cand = np.nonzero((hs.P @ ax) < axial_max)[0]
    bad = []
    for i in cand:
        r = tree.ray_cast(Vector(hs.P[i] + nrm[i] * 1e-4), Vector(nrm[i]), maxd)
        if r[0] is None: bad.append(i)
    bad = np.array(bad, int)
    return bad, len(cand)
