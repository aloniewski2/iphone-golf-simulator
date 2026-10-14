"""GLOVE: the navy velcro tab (strap, hook-and-loop free end) and its stitched anchor patch.
Both are 'painted grids' on the glove's outer surface: (triangle, barycentric) samples chosen on the product, re-evaluated on any fitted outer mesh.
build_strap / build_patch turn a grid of surface points (nu, nv, 3) into closed thick shells (acc_geo.thick_grid): rounded ends, real thickness, rounded edge."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import acc_geo as G
import glove_curves as C

TH0, TH1 = math.radians(-122), math.radians(96)         # strap runs around the wrist from the ulnar side (-) to the thumb side (+), measured from the dorsal line
A0, A1 = -0.0298, -0.0042                               # axial extent of the strap (G frame y), across its width
PTH0, PTH1 = math.radians(8), math.radians(90)          # anchor patch
PA0, PA1 = -0.0316, -0.0026
T_STRAP = 0.00155
T_PATCH = 0.00105
LIFT = 0.00035
K_STRAP, K_PATCH, K_HOOK = 2, 3, 4
FAB_RIB, FAB_PIQUE = 9, 7


def smooth01(x): x = np.clip(x, 0, 1); return x * x * (3 - 2 * x)


def centre_at(Opos, a, half=0.0025):
    m = np.abs(Opos[:, 1] - a) < half
    return Opos[m].mean(0)


def grid_samples(surf, Opos, th0, th1, a0, a1, nu, nv):
    """(tri (nu,nv), bary (nu,nv,3)) painted on the surface: u steps around the wrist (theta from the dorsal line, + toward the thumb), v along the axis."""
    tri = np.zeros((nu, nv), np.int64); bary = np.zeros((nu, nv, 3))
    ths = np.linspace(th0, th1, nu); As = np.linspace(a0, a1, nv)
    for j, a in enumerate(As):
        c = centre_at(Opos, a)
        c = np.array([c[0], a, c[2]])
        for i, th in enumerate(ths):
            d = np.array([math.sin(th), 0.0, math.cos(th)])
            h = surf.ray(c, d, .1)
            if h is None: raise RuntimeError('strap ray miss')
            tri[i, j] = h[0]; bary[i, j] = h[1]
    return tri, bary, ths, As


def smooth_grid(P, iters=4):
    """3x3 relaxation of an (nu, nv, 3) grid, border rows/cols kept moving along (their neighbours only); removes the facet noise of a coarse mesh."""
    P = P.copy()
    for _ in range(iters):
        Q = P.copy()
        Q[1:-1, 1:-1] = (P[:-2, 1:-1] + P[2:, 1:-1] + P[1:-1, :-2] + P[1:-1, 2:] + 4 * P[1:-1, 1:-1]) / 8
        Q[0, 1:-1] = (P[0, :-2] + P[0, 2:] + 2 * P[0, 1:-1]) / 4; Q[-1, 1:-1] = (P[-1, :-2] + P[-1, 2:] + 2 * P[-1, 1:-1]) / 4
        Q[1:-1, 0] = (P[:-2, 0] + P[2:, 0] + 2 * P[1:-1, 0]) / 4; Q[1:-1, -1] = (P[:-2, -1] + P[2:, -1] + 2 * P[1:-1, -1]) / 4
        P = Q
    return P


def eval_grid(tri, bary, Ofit, T, smooth=5):
    P = np.einsum('ijk,ijkc->ijc', bary, Ofit[T[tri]])
    return smooth_grid(P, smooth) if smooth else P


def arc_along(P):
    """cumulative arc length along u of the centre row"""
    mid = P[:, P.shape[1] // 2]
    seg = np.linalg.norm(np.diff(mid, axis=0), axis=1)
    return np.concatenate([[0], np.cumsum(seg)])


def round_ends(P, half_fn, rowfrac):
    """resample rows so each column's v-extent is scaled by half_fn(i) (rounded ends). rowfrac: original v fractions 0..1"""
    nu, nv, _ = P.shape
    out = np.zeros_like(P)
    for i in range(nu):
        f = half_fn(i)
        t = 0.5 + (rowfrac - 0.5) * f
        x = t * (nv - 1); j0 = np.clip(np.floor(x).astype(int), 0, nv - 2); w = (x - j0)[:, None]
        out[i] = P[i, j0] * (1 - w) + P[i, j0 + 1] * w
    return out


def end_profile(ell, L, R):
    """width factor along the strap: 1 mid, quarter-round at both ends (radius R)"""
    def f(l):
        k = 1.0
        if l < R: k = math.sqrt(max(1 - ((R - l) / R) ** 2, 0.0))
        elif l > L - R: k = math.sqrt(max(1 - ((l - (L - R)) / R) ** 2, 0.0))
        return max(k, 0.18)
    return f


def build_strap(P0, ths, mode='closed', open_deg=110.0, patch_lift=T_PATCH, thickness=T_STRAP, R_end=0.0075):
    """P0: (nu, nv, 3) surface points under the strap. Returns (part-object-ready dict of fields..., shell params) -> use make_thick."""
    nu, nv, _ = P0.shape
    ell = arc_along(P0); L = ell[-1]
    N = G.grid_normals(P0)
    prof = end_profile(ell, L, R_end)
    rowfrac = np.linspace(0, 1, nv)
    # lift over the patch (the free end rides on the patch)
    lift = LIFT + patch_lift * smooth01((ths - PTH0) / math.radians(7))
    I = P0 + N * lift[:, None, None]              # the surface the strap lies on
    Nn = N.copy()
    if mode == 'open':
        ih = int(np.argmin(np.abs(ths - math.radians(8))))
        C0 = I[:, nv // 2]
        tau = G.unit(C0[min(ih + 1, nu - 1)] - C0[max(ih - 1, 0)])
        Bv = G.unit(I[ih, -1] - I[ih, 0])
        Nh = G.unit(np.cross(tau, Bv))
        Nh = Nh * np.sign(Nh @ N[ih, nv // 2])
        ph = ell[ih]
        pos_c = [C0[i] for i in range(nu)]
        psi_max = math.radians(open_deg)
        cur = C0[ih].copy(); prev = ph
        newI = I.copy(); newN = N.copy()
        for i in range(ih, nu):
            l = ell[i]
            psi = psi_max * smooth01((l - ph) / 0.014)
            d = tau * math.cos(psi) + Nh * math.sin(psi)
            if i > ih: cur = cur + d * (l - prev)
            prev = l
            Np = -tau * math.sin(psi) + Nh * math.cos(psi)
            off = (rowfrac - 0.5) * np.linalg.norm(I[i, -1] - I[i, 0])
            newI[i] = cur[None, :] + Bv[None, :] * off[:, None]
            newN[i] = Np
        I, Nn = newI, newN
    Pout = I + Nn * thickness
    Pout = round_ends(Pout, lambda i: prof(ell[i]), rowfrac)
    Iout = round_ends(I, lambda i: prof(ell[i]), rowfrac)
    # across-offset coordinate per row (metres, signed), with the end rounding applied
    wid = np.linalg.norm(P0[:, -1] - P0[:, 0], axis=1)
    sv = np.zeros((nu, nv))
    for i in range(nu): sv[i] = (0.5 + (rowfrac - 0.5) * prof(ell[i]) - 0.5) * wid[i]
    su = np.repeat(ell[:, None], nv, 1)
    hookf = np.repeat((ths[:, None] > math.radians(11)).astype(float), nv, 1)
    return dict(P=Pout, T=thickness, fields=dict(su=su, sv=sv, slen=np.full((nu, nv), L), hookf=hookf, half=np.repeat((wid * 0.5)[:, None], nv, 1)))


def build_patch(P0, ths, R_c=0.0030):
    nu, nv, _ = P0.shape
    N = G.grid_normals(P0)
    ell = arc_along(P0); L = ell[-1]
    rowfrac = np.linspace(0, 1, nv)
    prof = end_profile(ell, L, R_c)
    Pout = P0 + N * (LIFT * 0.6 + T_PATCH)
    Pout = round_ends(Pout, lambda i: prof(ell[i]), rowfrac)
    wid = np.linalg.norm(P0[:, -1] - P0[:, 0], axis=1)
    sv = np.zeros((nu, nv))
    for i in range(nu): sv[i] = (rowfrac - 0.5) * prof(ell[i]) * wid[i]
    su = np.repeat(ell[:, None], nv, 1)
    return dict(P=Pout, T=T_PATCH, fields=dict(su=su, sv=sv, slen=np.full((nu, nv), L), half=np.repeat((wid * .5)[:, None], nv, 1)))


def make_thick(name, d, edge_steps=3):
    return G.thick_grid(name, d['P'], d['T'], edge_steps=edge_steps, rim_u=(True, True), rim_v=(True, True), fields=d['fields'])
