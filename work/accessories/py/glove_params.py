"""GLOVE: measure a hero's hand (rays from the rig's joints into the skin) and write it as a HandSDF parameter dict (same format as glove_sdf.PRODUCT, explicit joint points),
so the glove's inner surface for that hero is built from the SAME recipe as the product (fingers, thumb, thenar, palm+forearm loft; smooth unions), at the hero's own
skeleton and thickness, instead of squeezing the product through a warp. The forearm is built straight and bent about the wrist afterwards (bend_fn)."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import glove_sdf as S, glove_hand as GH, glove_fit as FT

MM = 1e-3
CLEAR = FT.CLEAR
SMOOTH_FING, SMOOTH_PALM, SMOOTH_THENAR = 0.0060, 0.0100, 0.0130        # hero hands are tight: bridge the finger webs / thumb crease more (the product keeps its own 3.5 / 7.5 / 10 mm)


def smooth01(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


def rd(hs, p, d, maxd=.06):
    r = hs.ray(p, d, maxd)
    return None if r is None else float(np.linalg.norm(r[0] - p))


def fill(a, u, default):
    a = np.array([np.nan if v is None else v for v in a], float)
    ok = ~np.isnan(a)
    if ok.sum() == 0: return np.full(len(a), default)
    return np.interp(u, u[ok], a[ok])


def smooth_arr(a, k=1):
    a = np.asarray(a, float)
    for _ in range(k):
        b = a.copy(); b[1:-1] = (a[:-2] + 2 * a[1:-1] + a[2:]) / 4; a = b
    return a


def measure_chain(hs, spine, frame_fn, valid1_from=.22, rmax=.024, nst=15):
    u = np.linspace(0.03, 0.97, nst)
    C = np.zeros((nst, 3)); R1 = []; R2 = []
    for i, uu in enumerate(u):
        ph, t = FT.polyline_point(spine, uu)
        e1, e2 = frame_fn(t)
        dp1, dn1, dp2, dn2 = rd(hs, ph, e1), rd(hs, ph, -e1), rd(hs, ph, e2), rd(hs, ph, -e2)
        ok1 = dp1 is not None and dn1 is not None and max(dp1, dn1) < rmax * 1.3 and uu >= valid1_from
        ok2 = dp2 is not None and dn2 is not None and max(dp2, dn2) < rmax * 1.3
        c = ph.copy()
        if ok1: c = c + e1 * (dp1 - dn1) / 2
        if ok2: c = c + e2 * (dp2 - dn2) / 2
        C[i] = c
        R1.append((dp1 + dn1) / 2 if ok1 else None); R2.append((dp2 + dn2) / 2 if ok2 else None)
    nvalid = sum(1 for a, b in zip(R1, R2) if a is not None and b is not None)
    r2 = fill(R2, u, 0.009)
    r1 = fill(R1, u, np.nan)
    if np.isnan(r1).any(): r1 = r2 / 0.92
    r1 = np.clip(smooth_arr(r1, 2), 0.004, rmax); r2 = np.clip(smooth_arr(r2, 2), 0.003, rmax)
    return u, C, r1, r2, nvalid


def chain_params(hs, spine, frame_fn, flat_free=True, thumb=False):
    u, C, r1, r2, nvalid = measure_chain(hs, spine, frame_fn, valid1_from=.0 if thumb else .22)
    L = np.linalg.norm(np.diff(spine, axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(L)]); tot = cum[-1]
    r_tip = float(np.interp(.92, u, r1)) + CLEAR
    uj = np.array([0.0, cum[1] / tot, cum[2] / tot, max(1 - r_tip / tot, .78)])
    pts = np.stack([np.interp(uj, u, C[:, k]) for k in range(3)], 1)
    rr = np.interp(uj, u, r1) + CLEAR
    rr[3] = r_tip
    flat = np.clip(np.interp(uj, u, r2 / np.maximum(r1, 1e-4)), .76, 1.0) if not thumb else np.ones(4)
    if thumb: rr = np.interp(uj, u, (r1 + r2) / 2) + CLEAR; rr[3] = float(np.interp(.92, u, (r1 + r2) / 2)) + CLEAR
    return dict(pts=(pts / MM).tolist(), r=(rr / MM).tolist(), flat=flat.tolist(), valid=nvalid / len(u))


def thumb_from_cloud(hs, Wl):
    """thumb chain from the skin itself: principal axis of the vertices weighted to the thumb bones (the rig's thumb joints are not always inside the thumb skin)"""
    side = hs.side
    w = sum(Wl[:, hs.widx[f'{side}Thumb{sg}']] for sg in GH.SEGS)
    X = hs.P[w > .3]
    c = X.mean(0)
    _, _, Vt = np.linalg.svd(X - c, full_matrices=False)
    ax = Vt[0]
    ref = hs.apex['Thumb'] - hs.J['Thumb'][0]
    if ax @ ref < 0: ax = -ax
    t = (X - c) @ ax
    rad = np.linalg.norm((X - c) - np.outer(t, ax), axis=1)
    r = float(np.percentile(rad, 60))
    root = c + ax * (t.min() - 0.012); apex = c + ax * t.max()
    L = float(np.linalg.norm(apex - root))
    r_tip = r * 0.95 + CLEAR
    q = [root, root + (apex - root) * .40, root + (apex - root) * .75, apex - ax * r_tip]
    rr = np.array([r * 1.15, r * 1.08, r * 1.0, r * 0.95]) + CLEAR
    return dict(pts=(np.array(q) / MM).tolist(), r=(rr / MM).tolist(), flat=[1, 1, 1, 1], valid=1.0, cloud=True)


def hero_params(hs, prod=S.PRODUCT, verbose=True):
    """-> (params dict, info dict(Rf, sx, sy, My))"""
    P = dict(fingers={}, thumb=None, thenar=None, loft=None)
    for f in ['Index', 'Middle', 'Ring', 'Little']:
        spine = np.vstack([hs.J[f][0], hs.J[f][1], hs.J[f][2], hs.apex[f]])
        P['fingers'][f] = chain_params(hs, spine, FT.frame_finger)
    spine = np.vstack([hs.J['Thumb'][0], hs.J['Thumb'][1], hs.J['Thumb'][2], hs.apex['Thumb']])
    Wl0 = hs.W[hs.ids]
    P['thumb'] = chain_params(hs, spine, FT.frame_thumb, thumb=True)
    if verbose: print('thumb ray validity', round(P['thumb']['valid'], 2))
    if P['thumb']['valid'] < .7:
        P['thumb'] = thumb_from_cloud(hs, Wl0)
        if verbose: print('thumb from the skin cloud:', np.round(P['thumb']['pts'], 0).tolist(), 'r', np.round(P['thumb']['r'], 1))
    P['thumb']['flat'] = [1, 1, 1, 1]
    tp = np.array(P['thumb']['pts']); tr = np.array(P['thumb']['r'])
    P['thenar'] = dict(a=(2.0, 0.0, 0.0), b=tuple(tp[0]), ra=float(max(tr[0] * 1.25, 12.0)), rb=float(tr[0] * 1.05), flat=0.78)
    # ---- palm + forearm loft
    hd = hs.hd; side = hs.side
    Wl = hs.W[hs.ids]
    arm_only = (Wl[:, hs.widx[f'{side}LowerArm']] + Wl[:, hs.widx[f'{side}Hand']]) > .6
    My = float(np.linalg.norm(hs.J['Middle'][0]))
    sy = My / 0.1034
    yv = np.array([0.0, 20.0, 40.0, 60.0, 80.0]) * sy * MM
    rows = []                                           # (y, cx, cz, a, b) metres
    yhat = np.array([0, 1.0, 0])
    for y in yv:
        p0 = np.array([0.0, y * 1.0, 0.0])
        ph = FT.recentre(hs, p0, yhat, .10, arm_only, slab=.007)
        hits = {}
        for nm, d in (('xp', (1.0, 0, 0)), ('xn', (-1.0, 0, 0)), ('zp', (0, 0, 1.0)), ('zn', (0, 0, -1.0))):
            r = hs.ray(ph, np.array(d), .12); hits[nm] = None if r is None else r[0]
        if any(v is None for v in hits.values()): continue
        a = (hits['xp'][0] - hits['xn'][0]) / 2; cx = (hits['xp'][0] + hits['xn'][0]) / 2
        b = (hits['zp'][2] - hits['zn'][2]) / 2; cz = (hits['zp'][2] + hits['zn'][2]) / 2
        rows.append((y, cx, cz, a + CLEAR, b + CLEAR))
    palm = np.array(rows)
    axh = FT.unit(hs.elbow - hs.wrist)
    tt = -axh                                           # toward the hand
    e1f, e2f = FT.frame_finger(tt)
    Rf = np.stack([e1f, tt, e2f], 1)
    frows = []
    for t in (85, 70, 52, 36, 24, 12, 0):
        tm = t * MM
        p0 = axh * tm
        ph = FT.recentre(hs, p0, tt, .12, arm_only, slab=.008)
        d = {nm: rd(hs, ph, v, .12) for nm, v in (('p1', e1f), ('n1', -e1f), ('p2', e2f), ('n2', -e2f))}
        if any(v is None for v in d.values()): continue
        q = ph - hs.wrist
        frows.append((-tm, q @ e1f + (d['p1'] - d['n1']) / 2, q @ e2f + (d['p2'] - d['n2']) / 2, (d['p1'] + d['n1']) / 2 + CLEAR, (d['p2'] + d['n2']) / 2 + CLEAR))
    forearm = np.array(frows)
    if verbose: print('palm sections', len(palm), 'forearm sections', len(forearm))
    # wrist: average the forearm station at 0 and the palm station at 0 (the loft must be continuous)
    allr = np.vstack([forearm[forearm[:, 0] < -1e-6], palm[palm[:, 0] > 1e-6]])
    w0 = [r for r in forearm if abs(r[0]) < 1e-6] + [r for r in palm if abs(r[0]) < 1e-6]
    w0 = np.mean(w0, 0) if len(w0) else None
    if w0 is not None: allr = np.vstack([allr, w0[None]])
    allr = allr[np.argsort(allr[:, 0])]
    # front end: the product's rounding of the palm edge, scaled onto the measured last station
    y_last = allr[-1, 0]
    pl = prod['loft']; ysp = np.array(pl['ys']); ap = np.array(pl['a']); bp = np.array(pl['b'])
    ends = []
    for ye in (94.0, 101.0, 106.0):
        ratio_a = np.interp(ye, ysp, ap) / np.interp(80.0, ysp, ap); ratio_b = np.interp(ye, ysp, bp) / np.interp(80.0, ysp, bp)
        ends.append((ye * sy * MM, allr[-1, 1], allr[-1, 2] - 0.0004, allr[-1, 3] * ratio_a, allr[-1, 4] * ratio_b))
    allr = np.vstack([allr, np.array([e for e in ends if e[0] > y_last + 0.002])]) if ends else allr
    P['smooth'] = dict(k_fing=SMOOTH_FING, k_palm=SMOOTH_PALM, k_thenar=SMOOTH_THENAR)
    P['loft'] = dict(ys=(allr[:, 0] / MM).tolist(), cx=(allr[:, 1] / MM).tolist(), cz=(allr[:, 2] / MM).tolist(), a=(allr[:, 3] / MM).tolist(), b=(allr[:, 4] / MM).tolist())
    a60 = float(np.interp(60.0 * sy * MM, allr[:, 0], allr[:, 3])); sx = a60 / (np.interp(60.0, ysp, ap) * MM)
    info = dict(Rf=Rf, sx=float(sx), sy=float(sy), My=My)
    return P, info


def bend_fn(Rf, width=.030):
    def bend(V):
        w = smooth01(-V[:, 1] / width)[:, None]
        return (1 - w) * V + w * (V @ Rf.T)
    return bend
