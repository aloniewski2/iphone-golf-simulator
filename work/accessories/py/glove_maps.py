"""GLOVE construction marks for the map baker: seams + stitching + fourchette strips along every finger side, pad outline, yoke / wrist-band seams,
perforations, rolled cuff hem, strap/patch stitching, hook-and-loop texture. Curves live on the glove as (triangle, barycentric) samples and are re-evaluated on
the fitted mesh, so every mark follows the fitted shape. Returns the B (trim mix) channel; edits cv.H (height) and cv.C (cavity)."""
import os, math
import numpy as np
from acc_canvas import smoothstep, band, gauss
import glove_shell as SH, glove_strap as ST

K_SHELL, K_LINING, K_STRAP, K_PATCH, K_HOOK = 0, 1, 2, 3, 4


def inside_poly(x, y, poly):
    """even-odd test of points (x, y) against a closed polygon (k, 2)"""
    inside = np.zeros(len(x), bool)
    k = len(poly)
    for i in range(k):
        x1, y1 = poly[i]; x2, y2 = poly[(i + 1) % k]
        cond = ((y1 > y) != (y2 > y))
        xi = x1 + (y - y1) * (x2 - x1) / (y2 - y1 + 1e-18)
        inside ^= cond & (x < xi)
    return inside


class Ctx:
    """per-bake context: fitted outer positions of the glove's inner-surface vertices, curve sample positions/normals, candidate-texel queries."""
    def __init__(self, cv):
        g = cv.g; self.cv = cv; self.g = g
        n_in = int(g['n_in']); self.T = g['T_in']
        src = g['src']; pos = g['pos']; nrm = g['nrm']
        sel = src < n_in
        self.O = np.zeros((n_in, 3)); self.O[src[sel]] = pos[sel]
        self.N = np.zeros((n_in, 3)); self.N[src[sel]] = nrm[sel]
        self.tex_n = cv.I(nrm)
        # which piece vertices are near what: used for candidate selection
        self.P = pos
        self.tri = g['tri']
        self.names = [str(s) for s in g['cv_names']]
        self.cache = {}

    def curve(self, name):
        g = self.g
        tri = g[f'cv_{name}_tri']; bary = g[f'cv_{name}_bary']
        Q = np.einsum('kj,kjc->kc', bary, self.O[self.T[tri]])
        Qn = np.einsum('kj,kjc->kc', bary, self.N[self.T[tri]])
        Qn /= np.linalg.norm(Qn, axis=1, keepdims=True) + 1e-12
        wf = g[f'cv_{name}_wf']
        arc = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(Q, axis=0), axis=1))])
        tip = int(g[f'cv_{name}_tip'])
        self.edge = g[f'cv_{name}_edge'] if f'cv_{name}_edge' in g.files else np.full(len(Q), 1.0)
        return Q, Qn, wf, arc, tip

    def points(self, tri, bary):
        Q = np.einsum('kj,kjc->kc', bary, self.O[self.T[tri]])
        Qn = np.einsum('kj,kjc->kc', bary, self.N[self.T[tri]])
        Qn /= np.linalg.norm(Qn, axis=1, keepdims=True) + 1e-12
        return Q, Qn

    def query(self, Q, Qn, R, cos_min=.25):
        """per-texel (distance to the nearest sample within R and with a compatible normal, sample index); far = (1.0, -1)"""
        cv = self.cv
        d = np.full(cv.n, 1.0); idx = np.full(cv.n, -1, np.int64)
        # candidate faces: any vertex within R + margin of a sample
        near = np.zeros(len(self.P), bool)
        for a in range(0, len(self.P), 3000):
            D = np.linalg.norm(self.P[a:a + 3000, None, :] - Q[None, :, :], axis=2).min(1)
            near[a:a + 3000] = D < R + .007
        fn = near[self.tri].any(1)
        cand = np.nonzero(fn[cv.face])[0]
        if len(cand) == 0: return d, idx
        X = cv.pos[cand]; NT = self.tex_n[cand]
        out_d = np.full(len(cand), 1.0); out_i = np.full(len(cand), -1, np.int64)
        for a in range(0, len(cand), 2500):
            xs = X[a:a + 2500]
            D = np.linalg.norm(xs[:, None, :] - Q[None, :, :], axis=2)
            ok = (NT[a:a + 2500] @ Qn.T) > cos_min
            D = np.where(ok, D, 9.0)
            j = D.argmin(1); dm = D[np.arange(len(xs)), j]
            good = dm < R
            out_d[a:a + 2500][good] = dm[good]; out_i[a:a + 2500][good] = j[good]
        d[cand] = out_d; idx[cand] = out_i
        return d, idx


def details_glove(cv):
    g = cv.g
    ctx = Ctx(cv)
    k, z = cv.kind, cv.zone
    trim = np.zeros(cv.n)
    outer = (k == K_SHELL) & (z != 2)
    lining = (k == K_LINING)
    nzp = cv.A('nzp', 0.0); gx = cv.A('gx', 0.0); gy = cv.A('gy', 0.0); gz = cv.A('gz', 0.0)
    dors = nzp > 0; palm = nzp < 0
    dc = cv.A('dcuff', 1.0)
    X = cv.pos

    # ---------------------------------------------------------------- finger / thumb / hand side seams: fourchette strips
    SKIP = os.environ.get('GLOVE_SKIP', '').split(',')
    for name in ('fin_Index', 'fin_Middle', 'fin_Ring', 'fin_Little', 'thumb'):
        if name in SKIP: continue
        Q, Qn, wf, arc, tip = ctx.curve(name)
        d, idx = ctx.query(Q, Qn, .0085, cos_min=.0)
        ok = idx >= 0
        wfi = np.where(ok, wf[np.maximum(idx, 0)], 1.0)
        along = np.where(ok, arc[np.maximum(idx, 0)], 0.0)
        dtip = np.linalg.norm(X - Q[tip], axis=1)
        w = .0021 * wfi * smoothstep(.0075, .0135, dtip) + .0006
        endf = smoothstep(.0, .006, np.where(ok, ctx.edge[np.maximum(idx, 0)], 1.0))                  # seams run out gracefully (no stitch rings around fragment ends)
        m = outer * ok * endf
        cv.H -= .00012 * smoothstep(w, w - .0008, d) * m                            # the strip sits a hair lower than the panels
        cv.groove(d, w, m, width=.00058, depth=.00052, dark=.36)
        cv.stitch(d, w + .00135, along, m * (d > w), pitch=.0026, width=.00044, height=.00021)
        cv.stitch(d, w - .00135, along, m * (d < w - .0007) * (wfi > .6), pitch=.0026, width=.00040, height=.00018)
    # ---------------------------------------------------------------- palm pad (outline, stitched, slightly raised) and thenar crease
    Q, Qn, wf, arc, tip = ctx.curve('pad')
    d, idx = ctx.query(Q, Qn, .008, cos_min=-1.0)
    ok = idx >= 0; along = np.where(ok, arc[np.maximum(idx, 0)], 0.0)
    poly = g['cv_pad_pts0'][:, :2]
    ins = inside_poly(gx, gy, poly) & palm
    m = outer * ok * palm
    cv.groove(d, 0.0, m, width=.0007, depth=.0006, dark=.38)
    cv.stitch(d, .0019, along, m * (~ins), pitch=.0027, width=.00046, height=.0002)
    sd = np.where(ins, d, -d)
    cv.H += .00030 * smoothstep(0, .0040, sd) * outer * palm * np.where(ok, 1.0, ins * 1.0)
    cv.C *= 1 - .05 * (outer * palm * ins) * (1 - smoothstep(0, .006, d))
    Q, Qn, wf, arc, tip = ctx.curve('crease')
    d, idx = ctx.query(Q, Qn, .006, cos_min=-1.0)
    ok = idx >= 0; along = np.where(ok, arc[np.maximum(idx, 0)], 0.0)
    m = outer * ok * palm * smoothstep(.0, .006, np.where(ok, ctx.edge[np.maximum(idx, 0)], 1.0))
    cv.groove(d, 0.0, m, width=.0006, depth=.00045, dark=.30)
    cv.stitch(d, .0014, along, m, pitch=.0026, width=.00042, height=.0002)
    # ---------------------------------------------------------------- back of the hand: knuckle yoke + wrist band (double stitched)
    for name in ('yoke', 'band'):
        if name in SKIP: continue
        Q, Qn, wf, arc, tip = ctx.curve(name)
        d, idx = ctx.query(Q, Qn, .006, cos_min=-1.0)
        ok = idx >= 0; along = np.where(ok, arc[np.maximum(idx, 0)], 0.0)
        m = outer * ok * dors * smoothstep(.0, .006, np.where(ok, ctx.edge[np.maximum(idx, 0)], 1.0))
        cv.groove(d, 0.0, m, width=.0007, depth=.0006, dark=.40)
        cv.stitch(d, .0017, along, m, pitch=.0027, width=.00044, height=.0002)
    # ---------------------------------------------------------------- perforations
    Qh, Qhn = ctx.points(g['hole_tri'], g['hole_bary'])
    d, idx = ctx.query(Qh, Qhn, .004, cos_min=.4)
    aa = cv.texel * .7
    hole = (1 - smoothstep(.00105 - aa, .00105 + aa, d)) * (idx >= 0) * outer * dors
    cv.dimple(hole, depth=.00085, dark=.93)
    if hasattr(cv, 'aox'): cv.aox *= 1 - .88 * hole                    # (glove_bake) holes read black
    # ---------------------------------------------------------------- cuff: rolled hem (outer), hem stitch rows, fold crease; lining edge
    th_ang = np.arctan2(gx, gz) * .033
    cv.ridge(dc, .0016, outer * (dc < .006), width=.0009, height=.00032)
    cv.groove(dc, .0102, outer, width=.0008, depth=.00042, dark=.30)
    for at in (.0050, .0072):
        cv.stitch(dc, at, th_ang, outer * (dc < .012), pitch=.0027, width=.00046, height=.0002)
    cv.groove(dc, .0102, lining, width=.0009, depth=.0003, dark=.22)
    cv.stitch(dc, .0058, th_ang, lining * (dc < .012), pitch=.0028, width=.00040, height=.00014)
    cv.folds(cv.um * 2 + cv.vm, .014, .00004, lining, 4)
    cv.C *= 1 - .42 * lining                                             # soft grey lining
    # ---------------------------------------------------------------- strap + patch
    stp = (k == K_STRAP) | (k == K_HOOK); pat = (k == K_PATCH)
    su = cv.A('su', 0.0); sv = cv.A('sv', 0.0); slen = cv.A('slen', 1.0); half = cv.A('half', .01)
    de = np.minimum(np.minimum(half - np.abs(sv), su), slen - su)
    trim = np.where(stp | pat, 1.0, trim)
    cv.ridge(de, .0007, stp & (z != 2), width=.0011, height=.0004)
    th = cv.stitch(de, .0017, su + 1.6 * np.abs(sv), stp * (de > .0006), pitch=.0027, width=.00046, height=.0002)
    trim = np.where((th > .30) & stp, 0.0, trim)
    # fixed end: box stitch with an X
    ex = np.abs(su - .0085) - .0048; ey = np.abs(sv) - (half - .0040)
    q = np.maximum(ex, ey)
    boxm = stp * (su < .0160) * (z != 2)
    thb = cv.stitch(np.abs(q), 0.0, su + np.abs(sv), boxm * (q < .0008), pitch=.0026, width=.00046, height=.0002)
    dx = (su - .0085) / .0048; dy = sv / np.maximum(half - .0040, 1e-4)
    xl = np.abs(np.abs(dx) - np.abs(dy)) * .0032
    thx = cv.stitch(xl, 0.0, su + np.abs(sv), boxm * (q < -.0001), pitch=.0026, width=.00044, height=.00018)
    trim = np.where(((thb > .30) | (thx > .30)) & stp, 0.0, trim)
    # patch: border stitch, soft raised edge, fuzzy loop surface
    pu = cv.A('su', 0.0); pv = cv.A('sv', 0.0)
    dp = np.minimum(np.minimum(half - np.abs(pv), pu), slen - pu)
    cv.ridge(dp, .0006, pat, width=.0010, height=.00035)
    thp = cv.stitch(dp, .0018, pu + 1.6 * np.abs(pv), pat * (dp > .0006), pitch=.0027, width=.00046, height=.0002)
    trim = np.where((thp > .30) & pat, 0.0, trim)
    fuzz = np.sin(cv.um * 9100 + 1.3) * np.sin(cv.vm * 8300 + .4) + .6 * np.sin((cv.um + cv.vm) * 6100 + 2.1)
    cv.H += .00012 * fuzz * (pat | (k == K_HOOK))
    cv.C *= 1 - .10 * np.clip(-fuzz, 0, 1) * (pat | (k == K_HOOK))
    # hook side: regular hex dots
    hk = (k == K_HOOK).astype(float)
    pitch = .00105
    uu = cv.um / pitch; vv = cv.vm / (pitch * .866)
    dots = np.cos(2 * math.pi * (uu + .5 * (np.floor(vv + .5) % 2))) * np.cos(2 * math.pi * vv) if False else (np.cos(2 * math.pi * uu) * np.cos(2 * math.pi * cv.vm / pitch))
    cv.H += .00018 * dots * hk
    cv.C *= 1 - .18 * np.clip(-dots, 0, 1) * hk
    # creases of the fitted surface: no marks there (they would only be noise)
    fm = smoothstep(.02, .45, cv.A('fold', 0.0))
    cv.H *= (1 - fm) ; cv.C = 1 - (1 - cv.C) * (1 - fm)
    return np.clip(trim, 0, 1)
