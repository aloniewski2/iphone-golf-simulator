"""SHOE v2: a sneaker built as a product on a last. Parametric loft in the foot frame (x lateral OUT, y heel -> toe, z up from the ground):
midsole + outsole unit (flare, lip, toe spring, tread band), an upper (heel counter, vamp, toe box) lofted as two half arcs per row so the collar mouth and
the lacing throat are real edges, padded collar roll, tongue, eyestay overlays, laces, heel tab, toe cap, side stripe, mesh panels (via maps).
The foot only supplies the footprint (the last) and where the cloth has to be pushed out to clear skin (fit); the sneaker's own shape is designed.
Blender -b --factory-startup --python shoe2.py -- <hero> <design> <side L|R> <out.blend>"""
import sys, os, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import bpy, bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from polo2 import new_object, polys, apply_modifier, set_vattr, get_vattr, smoothstep, vnormals

KIND = {'shell': 0, 'tongue': 6, 'lace': 7, 'eyestay': 8, 'sole': 9, 'nub': 10, 'upper': 19, 'cord': 4, 'tab': 17, 'band': 12}
ROLE_SHOE, ROLE_SOLE, ROLE_HW = 0, 1, 2
FABRIC = dict(leather=8, mesh=2, rubber=3, foam=4, lace=5, rib=9, twill=6)


# ------------------------------------------------------------------ designs
def design_male_tennis():
    """white tennis shoe: mesh upper with synthetic overlays, padded collar, 5-eyelet lacing, navy side stripe + outsole rim, herringbone tread."""
    return dict(
        name='male_tennis', room=.0045, top_room=.008, flare=.0040, lip=.0105, sole_t=(.030, .0215), toe_spring=.013, toe_room=.016, heel_room=.010,
        collar=[(0.0, .106), (.06, .104), (.14, .097), (.24, .090), (.31, .095), (.38, .114)],      # collar top line: (y / L, z) heel -> throat start
        throat=(.37, .72), throat_w=(.034, .015), n_sec=2.5, upper_t=.0024, collar_roll=.0048, tongue_rise=.030,
        eyelets=5, lace_r=.0016, toecap=.84, counter=.30, stripe=False, stripe_trim=False, nubs=False, outsole_trim=True, spike=0,
        roles=('Kit_Shoe', 'Kit_Sole', 'Kit_Buckle'), fit_gap=.0035,
    )


def design_female_tennis():
    """plain white tennis shoe sized to the female foot (L .237): same construction, a little lower collar, slimmer sole."""
    D = design_male_tennis()
    D.update(name='female_tennis', sole_t=(.027, .020), toe_spring=.011, lip=.0095, upper_t=.0022, toe_room=.013, heel_room=.008, collar_roll=.0042, tongue_rise=.026,
             collar=[(0.0, .094), (.06, .092), (.14, .086), (.24, .080), (.31, .085), (.38, .102)], throat_w=(.030, .013))
    return D


def design_male_golf():
    """plain white golf shoe: low profile, 14 moulded cleats (nubs) under the sole, same construction as the tennis shoe."""
    D = design_male_tennis()
    D.update(name='male_golf', sole_t=(.024, .017), toe_spring=.010, nubs=True, nub_h=.0075, collar_roll=.0045,
             collar=[(0.0, .100), (.06, .098), (.14, .091), (.24, .085), (.31, .090), (.38, .108)], throat_w=(.032, .014))
    return D


def design_female_golf():
    D = design_female_tennis()
    D.update(name='female_golf', sole_t=(.022, .0155), toe_spring=.009, nubs=True, nub_h=.0070)
    return D


DESIGNS = {'male_tennis': design_male_tennis, 'female_tennis': design_female_tennis, 'male_golf': design_male_golf, 'female_golf': design_female_golf}


# ------------------------------------------------------------------ the last (foot frame + footprint)
class Frame:
    def __init__(self, body, side):
        s = 'Left' if side == 'L' else 'Right'
        self.side = side; sd = -1 if side == 'L' else 1
        W = body.W
        idx = lambda names: [i for i, n in enumerate(body.bones) if n in names]
        self.wf = W[:, idx([s + 'Foot', s + 'Toes'])].sum(1); self.wl = W[:, idx([s + 'LowerLeg'])].sum(1)
        sole = body.pos[(self.wf > .2) & (body.pos[:, 2] < .03)]
        toes = body.head(s + 'Toes'); ankle = body.head(s + 'Foot')
        f0 = toes - ankle; f0[2] = 0; f0 /= np.linalg.norm(f0)
        c = sole[:, :2].mean(0); _, _, vt = np.linalg.svd(sole[:, :2] - c); ax = vt[0]
        if ax @ f0[:2] < 0: ax = -ax
        self.fwd = np.array([ax[0], ax[1], 0.0])
        self.lat = np.cross(self.fwd, [0, 0, 1.]) * (-1 if sd > 0 else 1)
        self.ankle = ankle
        u = (sole - ankle) @ self.fwd
        self.y0 = u.min(); self.L = u.max() - u.min()
        self.origin = ankle + self.fwd * self.y0; self.origin[2] = 0
        self.body = body

    def to(self, P):
        d = np.asarray(P) - self.origin
        return np.stack([d @ self.lat, d @ self.fwd, np.asarray(P)[:, 2]], 1)

    def back(self, q):
        q = np.asarray(q)
        return self.origin + np.outer(q[:, 0], self.lat) + np.outer(q[:, 1], self.fwd) + np.outer(q[:, 2], [0, 0, 1.])


def footprint(fr, ys):
    """per row y: centre xc, half width w of the real sole (smoothed), foot-only top height and the ankle half-extent below z."""
    body = fr.body
    qs = fr.to(body.pos[(fr.wf > .2) & (body.pos[:, 2] < .03)])
    xc = np.zeros(len(ys)); w = np.zeros(len(ys))
    for j, y in enumerate(ys):
        m = np.abs(qs[:, 1] - min(max(y, .008), fr.L - .006)) < .008
        if m.sum() > 3: lo, hi = qs[m, 0].min(), qs[m, 0].max(); xc[j] = (lo + hi) / 2; w[j] = (hi - lo) / 2
        else: xc[j] = np.nan; w[j] = np.nan
    for a in (xc, w):
        g = ~np.isnan(a); a[:] = np.interp(ys, ys[g], a[g])
    for _ in range(10):
        xc[1:-1] = (xc[:-2] + 2 * xc[1:-1] + xc[2:]) / 4; w[1:-1] = (w[:-2] + 2 * w[1:-1] + w[2:]) / 4
    return xc, w


def foot_top(fr, ys, xc):
    """highest foot-only point per row (the instep line)."""
    body = fr.body
    qf = fr.to(body.pos[fr.wf > .5]); zt = np.zeros(len(ys))
    for j, y in enumerate(ys):
        m = np.abs(qf[:, 1] - y) < .008
        zt[j] = qf[m, 2].max() if m.sum() else 0
    g = zt > 0; zt = np.interp(ys, ys[g], zt[g])
    for _ in range(6): zt[1:-1] = (zt[:-2] + 2 * zt[1:-1] + zt[2:]) / 4
    return zt


def ankle_need(fr, ys, xc, zlim):
    """half extent (from the footprint centre) of foot + shin skin below the collar height, per row."""
    body = fr.body
    ql = fr.to(body.pos[(fr.wf + fr.wl) > .3]); out = np.zeros(len(ys))
    for j, y in enumerate(ys):
        m = (np.abs(ql[:, 1] - y) < .008) & (ql[:, 2] < zlim[j]) & (ql[:, 2] > .012)
        out[j] = np.abs(ql[m, 0] - xc[j]).max() if m.sum() else 0
    for _ in range(4): out[1:-1] = np.maximum(out[1:-1], (out[:-2] + 2 * out[1:-1] + out[2:]) / 4)
    return out


# ------------------------------------------------------------------ geometry helpers
def grid_quads(nu, nv, closed_u=False):
    F = []
    for i in range(nu if closed_u else nu - 1):
        i2 = (i + 1) % nu
        for j in range(nv - 1): F.append((i * nv + j, i2 * nv + j, i2 * nv + j + 1, i * nv + j + 1))
    return F


def prof(xs, table):
    t = np.array(table); return np.interp(xs, t[:, 0], t[:, 1])


def make_mesh(name, V, F, roles=None, kinds=None, fabric=None, panel=None, uv=None, vattr=None, corner_uv=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([tuple(v) for v in V], [], [tuple(int(i) for i in f) for f in F]); me.update()
    n = len(F)
    for k, v in (('role', roles), ('face_kind', kinds), ('fabric', fabric), ('panel', panel)):
        if v is None: continue
        a = me.attributes.new(k, 'INT', 'FACE'); a.data.foreach_set('value', np.broadcast_to(np.asarray(v, np.int32), (n,)).copy())
    if uv is not None:
        layer = me.uv_layers.new(name='UVMap'); li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
        layer.data.foreach_set('uv', np.asarray(uv, np.float32)[li].ravel())
    for k, v in (vattr or {}).items():
        a = me.attributes.new(k, 'FLOAT', 'POINT'); a.data.foreach_set('value', np.asarray(v, np.float32))
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    return ob


def tube_path(path, r, k=8, cap=True, flat=1.0):
    path = np.asarray(path, float); n = len(path)
    t = np.gradient(path, axis=0); t /= np.linalg.norm(t, axis=1, keepdims=True) + 1e-12
    V = []
    for i in range(n):
        a = np.cross(t[i], [0, 0, 1.] if abs(t[i][2]) < .95 else [1., 0, 0]); a /= np.linalg.norm(a); b = np.cross(t[i], a)
        rr = r[i] if hasattr(r, '__len__') else r
        for q in range(k):
            ph = 2 * math.pi * q / k
            V.append(path[i] + a * math.cos(ph) * rr + b * math.sin(ph) * rr * flat)
    F = []
    for i in range(n - 1):
        for q in range(k):
            q2 = (q + 1) % k; F.append((i * k + q, i * k + q2, (i + 1) * k + q2, (i + 1) * k + q))
    if cap: F.append(tuple(range(k))[::-1]); F.append(tuple(range((n - 1) * k, n * k)))
    arc = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(path, axis=0), axis=1))])
    return np.array(V), F, np.repeat(arc, k), np.tile(np.arange(k), n)


# ------------------------------------------------------------------ build
def build(hero_id, design, side, outp):
    D = DESIGNS[design]()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    import cv2io, glib as G, fit
    hero = cv2io.Hero(hero_id); body = G.Body(hero)
    fr = Frame(body, side); L = fr.L
    ny = 88; t = np.linspace(0, 1, ny)
    y0 = -D['heel_room']; y1 = L + D['toe_room']
    ys = y0 + (y1 - y0) * (.5 - .5 * np.cos(math.pi * t))
    xc, w_sole = footprint(fr, ys)
    ztop_foot = foot_top(fr, ys, xc)
    yf = ys / L
    def roll_max_(a, k):
        out = a.copy()
        for i in range(len(a)): out[i] = a[max(0, i - k):i + k + 1].max()
        return out
    # ---- collar mouth height profile Zr(y) and top height of the upper z_top(y)
    zr_collar = prof(yf, D['collar'])
    y_t0, y_t1 = D['throat'][0] * L, D['throat'][1] * L
    # rim height through the throat = just under the instep line; blend from the collar profile
    zr = np.where(yf <= D['collar'][-1][0], zr_collar, ztop_foot + D['top_room'] - .004)
    zr = np.maximum(zr, 0)
    z_top = np.where(yf < D['throat'][0] + .05, np.maximum(zr + .030, ztop_foot + D['top_room']), ztop_foot + D['top_room'])
    # toe box: rises to a bumper height at the very tip so the front is a dome, not a wedge
    z_top = np.maximum(z_top, .034 * smoothstep(.55, .9, yf) + .014)
    z_top = roll_max_(z_top, 3)
    for _ in range(12): z_top[1:-1] = (z_top[:-2] + 2 * z_top[1:-1] + z_top[2:]) / 4
    z_edge = np.full(ny, D['lip'] - .004)
    H = np.maximum(z_top - z_edge, .01)
    # half width of the upper at its bottom edge: the sole plus room; wider behind the throat so the ankle fits under the collar
    need = ankle_need(fr, ys, xc, zr)
    wu = np.maximum(w_sole + D['room'], np.where(yf < D['throat'][0] + .05, need + D['room'] * .8, 0))
    def roll_max(a, k):
        out = a.copy()
        for i in range(len(a)): out[i] = a[max(0, i - k):i + k + 1].max()
        return out
    wu = roll_max(wu, 4)
    for _ in range(14): wu[1:-1] = (wu[:-2] + 2 * wu[1:-1] + wu[2:]) / 4
    # rounded ends: width shrinks to zero over the last bit of the heel and the toe
    hl_ = D['heel_room'] + .022; e_h = np.sqrt(np.clip(1 - ((hl_ - (ys - y0)) / hl_) ** 2, 0, 1)); e_h = np.where(ys - y0 > hl_, 1, e_h)
    tr_ = D['toe_room'] + .018
    e_t = np.sqrt(np.clip(1 - ((ys - (y1 - tr_)) / tr_) ** 2, 0, 1)); e_t = np.where(ys < y1 - tr_, 1, e_t)
    wu = wu * e_h * e_t
    H = H * np.maximum(e_t ** .35, .16)
    # opening: collar mouth angle from the rim height, throat from its half width
    n = D['n_sec']
    th_c = np.arccos(np.clip(np.clip((zr - z_edge) / H, 0, 1) ** (n / 2), 0, 1))
    tw = np.interp(ys, [y_t0, y_t0 + .05 * L, y_t1], [D['throat_w'][0], D['throat_w'][0], D['throat_w'][1]])
    th_t = np.where((ys >= y_t0 - .02) & (ys < y_t1), np.arcsin(np.clip(tw / 2 / np.maximum(wu, .02), 0, .99)), 0.0)
    close = np.clip((y_t1 + .02 - ys) / .02, 0, 1)                       # round tip of the throat
    th_t = th_t * close
    th_o = np.maximum(th_c * (yf < D['throat'][0] + .02), th_t)
    th_o = np.where(ys > y_t1 + .02, 0.0, th_o)
    th_o = np.clip(th_o, 0, math.pi / 2 - .05)
    m = 16                                                                # columns per strip
    s = np.linspace(0, 1, m)
    # ---- containment: a SMOOTH radial envelope of the real foot (+ shin below the collar), from coarse slices, interpolated along the foot
    qa = fr.to(body.pos[(fr.wf + fr.wl) > .3]); qfoot = fr.to(body.pos[fr.wf > .3])
    zcore = .045; clear = D['upper_t'] + .0030
    nth = 72; thg = np.linspace(-.35, math.pi + .35, nth)                      # angle from the core (0 = outer, pi/2 = up, pi = inner)
    cy = np.linspace(y0 + .004, y1 - .004, 22)
    Rk = np.zeros((len(cy), nth))
    for k, yk in enumerate(cy):
        band = .014
        yy = yk
        jz = int(np.argmin(np.abs(ys - yk)))
        if yk < .012 or yk > L - .008: continue
        pts = qfoot[np.abs(qfoot[:, 1] - yy) < band]
        pts = pts[:, [0, 2]]
        if yk / L < D['collar'][-1][0] + .04:
            sl = (np.abs(qa[:, 1] - yy) < band) & (qa[:, 2] < zr[jz] + .004)
            pts = np.concatenate([pts[pts[:, 1] < zr[jz] + .004], qa[sl][:, [0, 2]]])
        pts = pts - np.array([xc[jz], zcore])
        if len(pts) < 3: continue
        h = G.convex_hull_2d(pts)
        Rk[k] = G.hull_radius(h, np.zeros(2), thg)
    good = Rk.sum(1) > 0
    for k in range(len(cy)):                                        # rows without foot (the very ends) take the nearest row's radius
        if not good[k]:
            kk = np.argmin(np.where(good, np.abs(np.arange(len(cy)) - k), 99)); Rk[k] = Rk[kk]
    for _ in range(3):
        Rk[1:-1] = (Rk[:-2] + 2 * Rk[1:-1] + Rk[2:]) / 4
        Rk[:, 1:-1] = (Rk[:, :-2] + 2 * Rk[:, 1:-1] + Rk[:, 2:]) / 4
    def env(j, th):
        """smooth hull radius at row j for angles th (interpolated between coarse rows)"""
        y = ys[j]; k = np.clip(np.searchsorted(cy, y) - 1, 0, len(cy) - 2); f = np.clip((y - cy[k]) / (cy[k + 1] - cy[k]), 0, 1)
        r0 = np.interp(th, thg, Rk[k]); r1 = np.interp(th, thg, Rk[k + 1]); return r0 * (1 - f) + r1 * f
    X = {}; Z = {}
    for sgn in (1, -1):
        x_ = np.zeros((ny, m)); z_ = np.zeros((ny, m))
        for j in range(ny):
            ph = s * (math.pi / 2 - th_o[j]); ph = ph if sgn > 0 else math.pi - ph
            cx = np.cos(ph); sx = np.sin(ph)
            x_[j] = xc[j] + wu[j] * np.sign(cx) * np.abs(cx) ** (2 / n)
            z_[j] = z_edge[j] + H[j] * np.abs(sx) ** (2 / n)
        X[sgn] = x_; Z[sgn] = z_
    def contain(rounds=2):
        """push the sheet out of the foot envelope by a SMOOTH field: deficits are dilated then blurred, so the result is never inside and never blocky"""
        for _ in range(rounds):
            for sgn in (1, -1):
                dfc = np.zeros((ny, m)); dirx = np.zeros((ny, m)); dirz = np.zeros((ny, m))
                for j in range(ny):
                    rel = np.stack([X[sgn][j] - xc[j], Z[sgn][j] - zcore], 1)
                    th_ = np.arctan2(rel[:, 1], rel[:, 0]); r_ = np.linalg.norm(rel, axis=1) + 1e-9
                    fade = float(smoothstep(.0, .022, ys[j]) * smoothstep(.0, .022, L - ys[j]))
                    Rh = (env(j, th_) + clear) * fade
                    d_ = np.where(rel[:, 1] < -.025, 0.0, np.maximum(Rh - r_, 0))
                    dfc[j] = d_; dirx[j] = rel[:, 0] / r_; dirz[j] = rel[:, 1] / r_
                dil = dfc.copy()
                for dj in range(-4, 5):
                    for di in range(-4, 5):
                        sh = np.roll(np.roll(dfc, dj, 0), di, 1); dil = np.maximum(dil, sh)
                for _b in range(5):
                    dil[1:-1] = (dil[:-2] + 2 * dil[1:-1] + dil[2:]) / 4
                    dil[:, 1:-1] = (dil[:, :-2] + 2 * dil[:, 1:-1] + dil[:, 2:]) / 4
                dil = np.where(dfc > 0, np.maximum(dil, dfc), dil)
                X[sgn] = X[sgn] + dirx * dil; Z[sgn] = Z[sgn] + dirz * dil
    contain(2)
    for sgn in (1, -1):                                                    # a last gentle smooth of the whole sheet; the field above kept it outside the foot with margin
        for _ in range(2):
            for A in (X[sgn], Z[sgn]):
                A[1:-1] = (A[:-2] + 2 * A[1:-1] + A[2:]) / 4
    for j in range(ny):                                                    # rows where the throat is closed: both strips meet on one line
        if th_o[j] < 1e-6:
            for A in (X, Z):
                mid = (A[1][j, -1] + A[-1][j, -1]) / 2; A[1][j, -1] = mid; A[-1][j, -1] = mid
    for sgn in (1, -1):                                                   # the collar rim never rises above its designed height + 4 mm
        for j in range(ny):
            if yf[j] < D['collar'][-1][0] + .02: Z[sgn][j] = np.minimum(Z[sgn][j], np.maximum(zr[j] + .004, z_edge[j] + .02) + np.linspace(0, 0, m))
    V = []; strip = {}
    for sgn in (1, -1):
        ids = np.zeros((ny, m), int)
        for j in range(ny):
            for i in range(m):
                V.append((X[sgn][j, i], ys[j], Z[sgn][j, i])); ids[j, i] = len(V) - 1
        strip[sgn] = ids
    V = np.array(V)
    nv_strip = ny * m
    F = []
    for sgn in (1, -1):
        ids = strip[sgn]
        for j in range(ny - 1):
            for i in range(m - 1):
                a_, b_, c_, d_ = ids[j, i], ids[j + 1, i], ids[j + 1, i + 1], ids[j, i + 1]
                F.append((a_, b_, c_, d_) if sgn > 0 else (a_, d_, c_, b_))
    F = np.array(F)
    Nrm = vnormals(V, F)
    # ---- upper UV: u = signed arc length from the rim (mouth edge / throat) down each side, v = y
    uv = np.zeros((len(V), 2))
    for sgn in (1, -1):
        ids = strip[sgn]
        for j in range(ny):
            pts = np.stack([X[sgn][j], Z[sgn][j]], 1); seg = np.linalg.norm(np.diff(pts, axis=0), axis=1); a_ = np.concatenate([[0], np.cumsum(seg)]); A = a_[-1]
            gap = abs(X[1][j, -1] - X[-1][j, -1]) * .5
            for i in range(m): uv[ids[j, i]] = (sgn * (gap + (A - a_[i])), ys[j])
    # ---- pieces. role / kind / fabric are face attributes; build every part as an object in frame space, then map to the world
    objs = []
    def add_obj(name, Vq, Fq, **kw):
        ob = make_mesh(name, Vq, Fq, **kw); objs.append(ob); return ob
    cells_j = np.arange(ny - 1)[:, None] * np.ones((1, m - 1), int); cells_i = np.ones((ny - 1, 1), int) * np.arange(m - 1)[None, :]
    def cell_faces(sel):
        out = []
        for sgn in (1, -1):
            ids = strip[sgn]; jj, ii = np.nonzero(sel[sgn])
            for j, i in zip(jj, ii):
                a_, b_, c_, d_ = ids[j, i], ids[j + 1, i], ids[j + 1, i + 1], ids[j, i + 1]
                out.append((a_, b_, c_, d_) if sgn > 0 else (a_, d_, c_, b_))
        return out
    def overlay(name, sel, lift, kind, fabric, role=ROLE_SHOE, trim=None):
        fl = cell_faces(sel)
        if not fl: return None
        used = np.unique(np.array(fl).ravel()); remap = {int(v): k for k, v in enumerate(used)}
        Vq = V[used] + Nrm[used] * (lift if np.ndim(lift) == 0 else lift[used])[..., None] if np.ndim(lift) else V[used] + Nrm[used] * lift
        Fq = [tuple(remap[int(v)] for v in f) for f in fl]
        va = {}
        if trim is not None: va['trim'] = trim[used]
        va['ov_u'] = uv[used, 0]; va['ov_v'] = uv[used, 1]
        return add_obj(name, Vq, Fq, roles=role, kinds=kind, fabric=fabric, panel=1, uv=uv[used], vattr=va)
    Y = ys / L
    # sole-line distance and rim distance fields (per vertex) for the maps
    d_rim = np.zeros(len(V)); d_edge = np.zeros(len(V))
    for sgn in (1, -1):
        ids = strip[sgn]
        for j in range(ny):
            pts = np.stack([X[sgn][j], Z[sgn][j]], 1); seg = np.linalg.norm(np.diff(pts, axis=0), axis=1); a_ = np.concatenate([[0], np.cumsum(seg)])
            for i in range(m): d_rim[ids[j, i]] = a_[-1] - a_[i]; d_edge[ids[j, i]] = a_[i]
    qy = V[:, 1] / L; qz = V[:, 2]; qx = V[:, 0] - np.interp(V[:, 1], ys, xc)
    side_o = np.sign(qx)
    fields = dict(d_rim=d_rim, d_edge=d_edge, qy=qy, qz=qz, qx=qx)
    # panels as smooth FIELDS on the upper itself (no stair-stepped overlay objects): lift = soft raised panel, distance fields for the maps
    ts, te = D['throat']
    yv = V[:, 1]; zv = V[:, 2]
    f_toe = (yv - (D['toecap'] * L - (zv - .01) * .5))
    f_counter = ((D['counter'] * L - (zv - .01) * .5) - yv)
    f_eye = d_rim - .017
    ey_fade = smoothstep(ts * L - .02, ts * L + .005, yv) * (1 - smoothstep(te * L - .004, te * L + .012, yv))
    p0 = np.array([.60 * L, .078]); p1 = np.array([.26 * L, .030]); dd = p1 - p0
    t_ = np.clip(((np.stack([yv, zv], 1) - p0) @ dd) / (dd @ dd), 0, 1); prj = p0 + t_[:, None] * dd
    f_stripe = np.linalg.norm(np.stack([yv, zv], 1) - prj, axis=1)
    onside = smoothstep(.02, .035, np.abs(qx)) * smoothstep(.015, .03, d_rim) * smoothstep(.006, .012, zv)
    lift = (.0010 * smoothstep(-.003, .003, f_toe) * smoothstep(.002, .006, d_rim)
            + .0010 * smoothstep(-.003, .003, f_counter) * smoothstep(.010, .016, d_rim)
            + .0013 * (1 - smoothstep(-.001, .004, f_eye)) * ey_fade
            + (.0014 * (1 - smoothstep(.0085, .0115, f_stripe)) * onside if D['stripe'] else 0))
    V = V + Nrm * lift[:, None]
    fields.update(f_toe=f_toe, f_counter=f_counter, f_eye=f_eye * ey_fade + (1 - ey_fade) * .05, f_stripe=np.where(onside > .5, f_stripe, .05), lift=lift)
    ob_up = add_obj('Upper', V, F, roles=ROLE_SHOE, kinds=KIND['upper'], fabric=FABRIC['leather'], panel=0, uv=uv, vattr=fields)
    # ---- rim lines (mouth + throat) for collar roll, tongue and laces
    rim_o = np.array([V[strip[1][j, -1]] for j in range(ny)]); rim_i = np.array([V[strip[-1][j, -1]] for j in range(ny)])
    n_o = np.array([Nrm[strip[1][j, -1]] for j in range(ny)]); n_i = np.array([Nrm[strip[-1][j, -1]] for j in range(ny)])
    j_ts = int(np.argmin(np.abs(ys - (ts - .012) * L))); j_te = int(np.argmin(np.abs(ys - te * L)))
    # collar roll: padded tube over the mouth edge, heel back -> both sides up to the collar front (tapering at the throat)
    path = np.concatenate([rim_o[j_ts:0:-1] * 1.0, rim_o[:1], rim_i[1:j_ts + 1]]) if False else np.concatenate([rim_o[j_ts:1:-1], [(rim_o[0] + rim_i[0]) / 2], rim_i[2:j_ts + 1]])
    pn = np.concatenate([n_o[j_ts:1:-1], [(n_o[0] + n_i[0]) / 2], n_i[2:j_ts + 1]])
    r_roll = D['collar_roll'] * np.concatenate([smoothstep(0, 8, np.arange(len(path)))[::-1] * 0 + 1])
    taper = np.minimum(smoothstep(0, 10, np.arange(len(path))), smoothstep(0, 10, np.arange(len(path))[::-1]))
    rr = D['collar_roll'] * (.35 + .65 * taper)
    pathp = path + pn * (rr[:, None] * .55)
    tv, tf, tarc, tang = tube_path(pathp, rr, k=10, cap=True, flat=.8)
    add_obj('CollarRoll', tv, tf, roles=ROLE_SHOE, kinds=KIND['band'], fabric=FABRIC['rib'], panel=2, uv=np.stack([tarc * 1.0, tang * .004], 1))
    # tongue: along the instep line under the throat, rising above the collar front as a padded top
    yy = np.linspace(te * L + .006, (ts - .02) * L, 26)
    zc_ = np.interp(yy, ys, (rim_o[:, 2] + rim_i[:, 2]) / 2) - .0045
    xc_ = np.interp(yy, ys, (rim_o[:, 0] + rim_i[:, 0]) / 2)
    wt = np.interp(yy, [te * L, ts * L], [.021, .030])
    # rising top: continue back and up
    tt = np.linspace(0, 1, 10)[1:]
    y_top = yy[-1] - .018 * tt; z_top_ = zc_[-1] + D['tongue_rise'] * np.sin(tt * math.pi / 2) ** 1.2 + .004 * tt
    yy = np.concatenate([yy, y_top]); zc_ = np.concatenate([zc_, z_top_]); xc_ = np.concatenate([xc_, np.full(len(tt), xc_[-1])])
    wt = np.concatenate([wt, wt[-1] * (1 + .12 * tt)])
    nu = 9; uu = np.linspace(-1, 1, nu)
    TV = []; TUV = []
    for k in range(len(yy)):
        for u in uu:
            TV.append((xc_[k] + u * wt[k], yy[k], zc_[k] + (1 - u * u) * .0035 * -1 + (u * u) * .0045))      # slight trough across, edges up under the rim
            TUV.append((u * wt[k], yy[k]))
    TF = grid_quads(len(yy), nu)
    tng = add_obj('Tongue', np.array(TV), TF, roles=ROLE_SHOE, kinds=KIND['tongue'], fabric=FABRIC['leather'], panel=3, uv=np.array(TUV),
                  vattr=dict(tg_edge=np.tile(1 - np.abs(uu), len(yy)) * 1.0, tg_y=np.repeat(yy, nu)))
    # laces: criss-cross over the throat, then a bow
    ne = D['eyelets']; ye = np.linspace((ts + .045) * L, (te - .055) * L, ne)
    je = [int(np.argmin(np.abs(ys - y))) for y in ye]
    pe_o = np.array([rim_o[j] + n_o[j] * .0019 - np.array([0, 0, 0]) for j in je]); pe_i = np.array([rim_i[j] + n_i[j] * .0019 for j in je])
    # eyelet rings (hardware role) set on the eyestay, a little outboard of the rim
    lace_paths = []
    for k in range(ne):
        for (a0, a1) in (((pe_o[k], pe_i[min(k + 1, ne - 1)]) if k < ne - 1 else (pe_o[k], pe_i[k])), ((pe_i[k], pe_o[min(k + 1, ne - 1)]) if k < ne - 1 else (pe_i[k], pe_o[k]))):
            mid = (a0 + a1) / 2; mid = mid + np.array([0, 0, 1.]) * .0026
            lace_paths.append(np.array([a0 + (mid - a0) * t_ + np.array([0, 0, .0] ) for t_ in (0, .25, .5, .75, 1)] if False else [a0, (a0 + mid) / 2 + np.array([0, 0, .0016]), mid, (mid + a1) / 2 + np.array([0, 0, .0016]), a1]))
    for pth in lace_paths:
        pp = np.array([np.interp(t_, [0, .25, .5, .75, 1], [0, 1, 2, 3, 4]) for t_ in np.linspace(0, 1, 9)])
        # smooth interpolation through the 5 control points
        ts_ = np.linspace(0, 1, 9); cps = pth
        curve = np.array([sum(cps[q] * np.maximum(0, 1 - abs(t_ * 4 - q)) for q in range(5)) for t_ in ts_])
        lv, lf, larc, lang = tube_path(curve, D['lace_r'], k=6, cap=True, flat=.55)
        add_obj('Lace', lv, lf, roles=ROLE_SHOE, kinds=KIND['lace'], fabric=FABRIC['lace'], panel=4, uv=np.stack([larc * 1.0, lang * .003], 1))
    # heel tab: a small pull loop at the back of the collar
    j0 = 1; pb = (rim_o[j0] + rim_i[j0]) / 2; nb = np.array([0, -1., 0])
    TBV = []; TBUV = []; nu_ = 5; nv_ = 6
    for a_ in range(nv_):
        for b_ in range(nu_):
            u_ = (b_ / (nu_ - 1) - .5) * .018; h_ = a_ / (nv_ - 1)
            TBV.append((pb[0] + u_, pb[1] - .002 - .007 * math.sin(h_ * math.pi * .9) * .6, pb[2] - .004 + .020 * h_ + .0 * u_)); TBUV.append((u_, h_ * .02))
    add_obj('HeelTab', np.array(TBV), grid_quads(nv_, nu_), roles=ROLE_SHOE, kinds=KIND['tab'], fabric=FABRIC['rib'], panel=6, uv=np.array(TBUV))
    # bow at the top of the lacing
    top_c = ((pe_o[0] + pe_i[0]) / 2) + np.array([0, -.004, .0035]); 
    # (the first row is the one nearest the collar: y smallest)
    for sgn, ang0 in ((1, 0.55), (-1, -0.55)):
        loop = []
        for t_ in np.linspace(0, 2 * math.pi, 14):
            loop.append(top_c + np.array([sgn * (.010 + .010 * math.sin(t_ / 2)) * math.sin(t_) * 1.0 + 0.0, -.004 * (1 - math.cos(t_)), .0034 * math.sin(t_)]))
        bv, bf, barc, bang = tube_path(np.array(loop), D['lace_r'] * .95, k=6, cap=False, flat=.55)
        add_obj('Bow', bv, bf, roles=ROLE_SHOE, kinds=KIND['lace'], fabric=FABRIC['lace'], panel=4, uv=np.stack([barc, bang * .003], 1))
        tail = np.array([top_c + np.array([sgn * .004 * t_, -.006 * t_, -.014 * t_ - .002 * math.sin(t_ * 5)]) for t_ in np.linspace(0, 1, 7)])
        tv2, tf2, tarc2, tang2 = tube_path(tail, D['lace_r'], k=6, cap=True, flat=.55)
        add_obj('Tail', tv2, tf2, roles=ROLE_SHOE, kinds=KIND['lace'], fabric=FABRIC['lace'], panel=4, uv=np.stack([tarc2, tang2 * .003], 1))
    # ---- sole unit: outline = the upper's bottom edge
    edge = np.concatenate([[V[strip[1][j, 0]] for j in range(ny)], [V[strip[-1][j, 0]] for j in range(ny - 1, -1, -1)]])
    outline = edge[:, [0, 1]]
    # arc-length resample
    seg = np.linalg.norm(np.diff(np.vstack([outline, outline[:1]]), axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)])
    NP = 132; st = np.linspace(0, cum[-1], NP, endpoint=False)
    outline = np.stack([np.interp(st, cum, np.append(outline[:, 0], outline[0, 0])), np.interp(st, cum, np.append(outline[:, 1], outline[0, 1]))], 1)
    for _ in range(4): outline = (np.roll(outline, 1, 0) + 2 * outline + np.roll(outline, -1, 0)) / 4
    cen_ = outline.mean(0)
    tan = np.roll(outline, -1, 0) - np.roll(outline, 1, 0); tan /= np.linalg.norm(tan, axis=1, keepdims=True)
    nrm2 = np.stack([tan[:, 1], -tan[:, 0]], 1)
    if np.mean(np.sum(nrm2 * (outline - cen_), axis=1)) < 0: nrm2 = -nrm2
    yo = outline[:, 1]
    T = np.interp(yo / L, [0, .62, 1], [D['sole_t'][0], D['sole_t'][1], D['sole_t'][1]])
    spring = D['toe_spring'] * np.clip((yo / L - .66) / .34, 0, 1) ** 2
    zb = -T + spring; zos = zb + .0065; fl = D['flare']; lip = D['lip']
    prof_rows = [(np.full(NP, lip), .0020, 0), (np.full(NP, lip - .0015), .0036, 0), (np.full(NP, lip - .0050), fl * .75, 0), ((zos + 0) * .5 + .0035, fl * 1.0, 0),
                 ((zos + 0) * .5 - .0035, fl * .95, 0), (zos + .0030, fl * .85, 0), (zos, fl * .9, 0), (zos - .0008, fl * 1.0, 1), (zb + .0020, fl * .95, 1), (zb, fl * .62, 1)]
    rowsR = []; row_role = []
    for z_, off, rl in prof_rows:
        zz = z_ if np.ndim(z_) else np.full(NP, z_)
        rowsR.append(np.concatenate([outline + nrm2 * off, zz[:, None]], 1)); row_role.append(rl)
    for kf in (.7, .4, .12):
        rowsR.append(np.concatenate([cen_ + (rowsR[-1][:, :2] - cen_) * kf if kf != .7 else cen_ + (rowsR[len(prof_rows) - 1][:, :2] - cen_) * kf, zb[:, None]], 1)); row_role.append(1)
    SR = np.stack(rowsR, 1)                                         # (NP, nrows, 3)
    nrw = SR.shape[1]; prof_n = len(prof_rows)
    SV = SR.reshape(-1, 3)
    SF = []; sf_role = []
    for i in range(NP):
        i2 = (i + 1) % NP
        for r_ in range(nrw - 1):
            SF.append((i * nrw + r_, i2 * nrw + r_, i2 * nrw + r_ + 1, i * nrw + r_ + 1)); sf_role.append(row_role[r_])
    cz = float(zb.mean()); SV = np.concatenate([SV, [[cen_[0], cen_[1], cz]]]); cidx = len(SV) - 1
    for i in range(NP):
        i2 = (i + 1) % NP
        SF.append((i * nrw + nrw - 1, i2 * nrw + nrw - 1, cidx)); sf_role.append(1)
    # insole cap (visible through the mouth): inside the lip, down to z = 0
    ins_rows = [outline - nrm2 * .0035, outline - nrm2 * .0055]
    zin = [lip - .0035, .0]
    IV = []
    base_ = len(SV)
    for rws, zz in zip(ins_rows, zin): IV.append(np.concatenate([rws, np.full((NP, 1), zz)], 1))
    for kf in (.7, .4, .12): IV.append(np.concatenate([cen_ + (ins_rows[1] - cen_) * kf, np.zeros((NP, 1))], 1))
    IVa = np.stack(IV, 1); nir = IVa.shape[1]
    SV = np.concatenate([SV, IVa.reshape(-1, 3), [[cen_[0], cen_[1], 0.0]]]); cidx2 = len(SV) - 1
    # lip top ring (row 0 of the sole) -> first insole ring
    for i in range(NP):
        i2 = (i + 1) % NP
        SF.append((i * nrw + 0, base_ + i * nir + 0, base_ + i2 * nir + 0, i2 * nrw + 0)); sf_role.append(0)
        for r_ in range(nir - 1):
            SF.append((base_ + i * nir + r_, base_ + i * nir + r_ + 1, base_ + i2 * nir + r_ + 1, base_ + i2 * nir + r_)); sf_role.append(0)
        SF.append((base_ + i * nir + nir - 1, base_ + i2 * nir + nir - 1, cidx2)); sf_role.append(0)
    # sole UV: u = along the outline (m), v = z
    ucoord = np.concatenate([np.arange(NP) * (cum[-1] / NP)])
    SUV = np.zeros((len(SV), 2))
    for i in range(NP):
        for r_ in range(nrw): SUV[i * nrw + r_] = (ucoord[i], SV[i * nrw + r_, 2])
    for i in range(NP):
        for r_ in range(prof_n, nrw): SUV[i * nrw + r_] = (SV[i * nrw + r_, 0] * 1.0 + 2.0, SV[i * nrw + r_, 1] * 1.0)
    SUV[cidx] = (cen_[0] + 2.0, cen_[1])
    for i in range(NP):
        for r_ in range(nir): SUV[base_ + i * nir + r_] = (ucoord[i], -.2 - r_ * .02)
    SUV[cidx2] = (0, -.3)
    d_out = np.zeros(len(SV)); d_sole_z = SV[:, 2].copy(); zos_v = np.zeros(len(SV))
    for i in range(NP):
        for r_ in range(nrw): zos_v[i * nrw + r_] = zos[i]
    solev = dict(sole_y=SV[:, 1] / L, sole_z=SV[:, 2], sole_zos=zos_v)
    add_obj('Sole', SV, SF, roles=np.array(sf_role), kinds=KIND['sole'], fabric=np.array([FABRIC['foam'] if r == 0 else FABRIC['rubber'] for r in sf_role]), panel=5, uv=SUV, vattr=solev)
    # ---- cleats (golf): short tapered prisms under the sole in two groups (forefoot ball, heel), on a staggered grid inside the outline
    if D.get('nubs'):
        def inside(pt, poly):
            x_, y_ = pt; c_ = False; n_ = len(poly)
            for i_ in range(n_):
                xi, yi = poly[i_]; xj, yj = poly[(i_ + 1) % n_]
                if ((yi > y_) != (yj > y_)) and (x_ < (xj - xi) * (y_ - yi) / (yj - yi + 1e-12) + xi): c_ = not c_
            return c_
        inset = outline - nrm2 * .012
        cle = []
        for yy in np.arange(.02, L + .0, .031):
            for xx in np.arange(-.12, .12, .034):
                xo = xx + (.017 if int(round(yy / .031)) % 2 else 0) + float(np.interp(yy, ys, xc))
                if inside((xo, yy), inset) and (yy < .105 * 1.0 + .0 or .50 * L < yy < .80 * L or yy > .86 * L and False): cle.append((xo, yy))
        nh = D['nub_h']; kseg = 8
        NV = []; NF = []; NUV = []
        for (cx_, cy_) in cle:
            z_top = float(np.interp(cy_, outline[:, 1][np.argsort(outline[:, 1])], zb[np.argsort(outline[:, 1])])) + .0006
            base = len(NV)
            for r_, zz, rr in ((0, z_top, .0068), (1, z_top - nh * .85, .0054), (2, z_top - nh, .0040)):
                for q in range(kseg):
                    ph = 2 * math.pi * q / kseg; NV.append((cx_ + rr * math.cos(ph), cy_ + rr * math.sin(ph), zz)); NUV.append((cx_ + 2.0, cy_))
            for r_ in range(2):
                for q in range(kseg):
                    q2 = (q + 1) % kseg; NF.append((base + r_ * kseg + q, base + r_ * kseg + q2, base + (r_ + 1) * kseg + q2, base + (r_ + 1) * kseg + q))
            NF.append(tuple(base + 2 * kseg + q for q in range(kseg))[::-1])
        if cle:
            add_obj('Cleats', np.array(NV), NF, roles=ROLE_SOLE, kinds=KIND['nub'], fabric=FABRIC['rubber'], panel=7, uv=np.array(NUV))
    # ---- map everything to the world, orient outward, solidify, join
    det = float(np.linalg.det(np.stack([fr.lat, fr.fwd, [0, 0, 1.]])))
    for ob in objs:
        me = ob.data
        p = np.zeros(len(me.vertices) * 3); me.vertices.foreach_get('co', p); p = p.reshape(-1, 3)
        me.vertices.foreach_set('co', fr.back(p).astype(np.float32).ravel()); me.update()
        if det < 0:
            bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.reverse_faces(bm, faces=bm.faces); bm.to_mesh(me); bm.free(); me.update()
    thick = {'Upper': D['upper_t'], 'Tongue': .0032, 'HeelTab': .0025}
    for ob in objs:
        ob.data.shade_smooth()
        for nm in ('outer', 'inner', 'rim'): ob.data.materials.append(bpy.data.materials.get(nm) or bpy.data.materials.new(nm))
        if ob.name in thick:
            sm = ob.modifiers.new('solid', 'SOLIDIFY'); sm.thickness = thick[ob.name]; sm.offset = -1; sm.use_even_offset = True
            sm.use_quality_normals = True; sm.use_rim = True; sm.material_offset = 1; sm.material_offset_rim = 2
            apply_modifier(ob, sm)
            bv = ob.modifiers.new('bevel', 'BEVEL'); bv.width = .0009; bv.segments = 2; bv.limit_method = 'ANGLE'; bv.angle_limit = math.radians(55)
            apply_modifier(ob, bv)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objs: ob.select_set(True)
    bpy.context.view_layer.objects.active = ob_up
    bpy.ops.object.join()
    ob_up.name = 'Kit_Shoe_' + side; ob_up['roles'] = list(D['roles']); ob_up['collar_prof'] = [float(v) for p in D['collar'] for v in p]
    bpy.ops.wm.save_as_mainfile(filepath=outp)
    print('SHOE2 %s L %.3f faces %d' % (side, L, len(ob_up.data.polygons)))


if __name__ == '__main__' or 'shoe2.py' in ' '.join(sys.argv):
    a = sys.argv[sys.argv.index('--') + 1:]
    build(a[0], a[1], a[2], a[3])
