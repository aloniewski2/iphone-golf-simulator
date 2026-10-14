"""GLOVE: the hand-shaped implicit solid (numpy only) the glove's INNER surface is built on, plus a surface-nets mesher.

Frame 'G' (see glove_hand.py): origin = wrist bone, +Y along the fingers, +X toward the thumb side, +Z = dorsal (back of the hand). Metres.
The solid = smooth union of: palm+forearm loft (superelliptic sections), four finger chains, a thumb chain, a thenar capsule (wrist -> thumb root).
Distances are approximate (anisotropic capsules / lofts) but monotone, which is all the mesher and the projection need.
"""
import math
import numpy as np

MM = 1e-3


def smin(a, b, k):
    """polynomial smooth min."""
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)


def unit(v):
    v = np.asarray(v, float); return v / (np.linalg.norm(v, axis=-1, keepdims=True) + 1e-15)


def round_cone(p, a, b, ra, rb):
    """exact signed distance to a round cone (capsule with two radii). p (n,3)."""
    ba = b - a; l2 = ba @ ba; rr = ra - rb; a2 = l2 - rr * rr; il2 = 1.0 / l2
    pa = p - a
    y = pa @ ba; z = y - l2
    t = pa * l2 - np.outer(y, ba)
    x2 = (t * t).sum(1)
    y2 = y * y * l2; z2 = z * z * l2
    k = np.sign(rr) * rr * rr * x2
    d3 = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - ra
    d1 = np.sqrt(x2 + z2) * il2 - rb
    d2 = np.sqrt(x2 + y2) * il2 - ra
    return np.where(np.sign(z) * a2 * z2 > k, d1, np.where(np.sign(y) * a2 * y2 < k, d2, d3))


def aniso_cone(p, a, b, ra, rb, e1, e2, s1=1.0, s2=1.0):
    """round cone whose lateral radii are scaled by s1 (along e1) and s2 (along e2): evaluated in the stretched space; distance scaled by min(s)."""
    u = unit(b - a)
    pa = p - a
    al = pa @ u
    l1 = pa @ e1; l2 = pa @ e2
    q = a + np.outer(al, u) + np.outer(l1 / s1, e1) + np.outer(l2 / s2, e2)
    return round_cone(q, a, b, ra, rb) * min(1.0, s1, s2)


class Chain:
    """a finger / thumb: joint positions P (k,3), radii r (k,), thickness ratio s2 (thickness along `dorsal` vs width) per point."""
    def __init__(self, P, r, flat=0.88, dorsal=(0, 0, 1.0), k=0.0025):
        self.P = np.asarray(P, float); self.r = np.asarray(r, float); self.flat = np.broadcast_to(np.asarray(flat, float), (len(P),)); self.dorsal = np.asarray(dorsal, float); self.k = k

    def seglens(self):
        return np.linalg.norm(np.diff(self.P, axis=0), axis=1)

    def length(self, cap=True):
        return float(self.seglens().sum() + (self.r[-1] if cap else 0))

    def frame_at(self, s):
        """point, axis u, lateral e1 (toward +x side), thickness axis e2 (~dorsal) at arc length s along the chain; beyond the last joint the chain continues straight."""
        L = self.seglens(); cum = np.concatenate([[0], np.cumsum(L)])
        i = int(np.clip(np.searchsorted(cum, s, side='right') - 1, 0, len(L) - 1))
        a, b = self.P[i], self.P[i + 1]
        u = unit(b - a)
        t = s - cum[i]
        p = a + u * t
        e2 = unit(self.dorsal - u * (self.dorsal @ u)); e1 = np.cross(u, e2)
        # e1 = u x e2 points toward -x for u=+y, e2=+z: flip so that e1 is the +x (thumb) side
        e1 = -e1
        return p, u, e1, e2

    def radius_at(self, s):
        L = self.seglens(); cum = np.concatenate([[0], np.cumsum(L)])
        return float(np.interp(s, cum, self.r))

    def __call__(self, p):
        d = None
        for i in range(len(self.P) - 1):
            a, b = self.P[i], self.P[i + 1]
            u = unit(b - a)
            e2 = unit(self.dorsal - u * (self.dorsal @ u))        # thickness axis ~ dorsal, orthogonal to the bone
            e1 = np.cross(u, e2)
            s2 = 0.5 * (self.flat[i] + self.flat[i + 1])
            di = aniso_cone(p, a, b, self.r[i], self.r[i + 1], e1, e2, 1.0, s2)
            d = di if d is None else smin(d, di, self.k)
        return d


class Loft:
    """palm+wrist+forearm: sections ellipse-ish (exponent n) at y stations; sections interpolated linearly in y; closed by rounded ends."""
    def __init__(self, ys, cx, cz, a, b, n=2.6, y0=None, y1=None):
        self.ys = np.asarray(ys, float); self.cx = np.asarray(cx, float); self.cz = np.asarray(cz, float)
        self.a = np.asarray(a, float); self.b = np.asarray(b, float); self.n = n
        self.y0 = self.ys[0] if y0 is None else y0; self.y1 = self.ys[-1] if y1 is None else y1

    def sec(self, y):
        yc = np.clip(y, self.ys[0], self.ys[-1])
        f = lambda arr: np.interp(yc, self.ys, arr)
        return f(self.cx), f(self.cz), f(self.a), f(self.b)

    def __call__(self, p):
        cx, cz, a, b = self.sec(p[:, 1])
        qx = np.abs(p[:, 0] - cx) / a; qz = np.abs(p[:, 2] - cz) / b
        r = (qx ** self.n + qz ** self.n) ** (1.0 / self.n)
        dsec = (r - 1.0) * np.minimum(a, b)
        # ends (straight caps; the product cuts the far end anyway; the near end is rounded by shrinking sections)
        w1 = dsec; w2 = np.maximum(self.y0 - p[:, 1], p[:, 1] - self.y1)
        return np.minimum(np.maximum(w1, w2), 0) + np.hypot(np.maximum(w1, 0), np.maximum(w2, 0))


class HandSDF:
    def __init__(self, loft, fingers, thumb, thenar, k_palm=0.0075, k_thenar=0.010, k_fing=0.0035):
        self.loft, self.fingers, self.thumb, self.thenar = loft, fingers, thumb, thenar
        self.k_palm, self.k_thenar, self.k_fing = k_palm, k_thenar, k_fing
        self.spheres = np.zeros((0, 4))          # extra cover spheres (x, y, z, r): volume the measured recipe missed (hero skin that stuck out of the first fit)

    def __call__(self, p):
        p = np.asarray(p, float)
        d = self.loft(p)
        df = None
        for f in self.fingers:
            x = f(p); df = x if df is None else smin(df, x, self.k_fing)
        d = smin(d, df, self.k_palm)
        dt = self.thumb(p)
        dn = self.thenar(p)
        d = smin(d, dn, self.k_thenar)
        d = smin(d, dt, self.k_thenar)
        for sp in self.spheres:
            d = smin(d, np.linalg.norm(p - sp[:3], axis=1) - sp[3], 0.004)
        return d

    def grad(self, p, h=2e-4):
        p = np.asarray(p, float)
        g = np.zeros_like(p)
        for k in range(3):
            e = np.zeros(3); e[k] = h
            g[:, k] = (self(p + e) - self(p - e)) / (2 * h)
        return g


# ---------------------------------------------------------------------------------------------- the product hand
def dirvec(yaw, pitch):
    y, p = math.radians(yaw), math.radians(pitch)
    return np.array([math.sin(y) * math.cos(p), math.cos(y) * math.cos(p), math.sin(p)])


def build_chain(base, yaws, pitches, lens):
    pts = [np.asarray(base, float)]
    for yw, pt, ln in zip(yaws, pitches, lens):
        pts.append(pts[-1] + dirvec(yw, pt) * ln)
    return np.array(pts)


# product skeleton, millimetres (converted): the product is an adult medium-large glove, open hand, fingers slightly splayed and softly curved
PRODUCT = dict(
    fingers=dict(
        Index=dict(base=(33, 98, 1.0), yaw=(8, 9, 10), pitch=(-3, -9, -15), lens=(37, 23, 16), r=(11.0, 10.6, 10.0, 9.2), flat=(.94, .92, .90, .88)),
        Middle=dict(base=(10.5, 103, 1.5), yaw=(1, 1, 1), pitch=(-3, -9, -15), lens=(41, 25, 17.5), r=(11.2, 10.8, 10.2, 9.3), flat=(.94, .92, .90, .88)),
        Ring=dict(base=(-13.5, 99, 1.0), yaw=(-6, -7, -8), pitch=(-3, -9, -15), lens=(37, 23.5, 16), r=(10.8, 10.4, 9.8, 9.0), flat=(.94, .92, .90, .88)),
        Little=dict(base=(-37, 88, 0.5), yaw=(-12, -14, -16), pitch=(-3, -9, -15), lens=(29, 19, 14.5), r=(9.8, 9.4, 8.9, 8.2), flat=(.94, .92, .90, .88)),
    ),
    thumb=dict(base=(36, 26, -7), yaw=(40, 34, 28), pitch=(-24, -18, -12), lens=(28, 22, 16), r=(14.6, 13.2, 12.0, 10.6), flat=(.92, .92, .90, .88)),
    thenar=dict(a=(2, 0, 0), b=(36, 26, -7), ra=17.0, rb=14.6, flat=.72),
    loft=dict(ys=(-80, -36, -20, 0, 20, 40, 60, 80, 94, 101, 106),
              cx=(0, 0, 0, 0, -2, -3, -4, -3, -2, -2, -2), cz=(0, 0, 0, .5, 0, 0, 0, 0, -.5, -1, -1),
              a=(31.5, 33.5, 32.5, 31.5, 38, 44.5, 49, 50.5, 50, 47, 40), b=(22.5, 22.0, 20.5, 18.5, 16.8, 15.5, 14.6, 13.6, 12.2, 10.2, 6.5)),
)


def make_product(P=PRODUCT):
    """HandSDF from a parameter dict: PRODUCT (angles + lengths) or a hero fit (explicit joint points under 'pts')."""
    fingers = []
    for nm, f in P['fingers'].items():
        pts = np.array(f['pts']) * MM if 'pts' in f else build_chain(np.array(f['base']) * MM, f['yaw'], f['pitch'], np.array(f['lens']) * MM)
        fingers.append(Chain(pts, np.array(f['r']) * MM, flat=np.array(f['flat'])))
    t = P['thumb']
    tp = np.array(t['pts']) * MM if 'pts' in t else build_chain(np.array(t['base']) * MM, t['yaw'], t['pitch'], np.array(t['lens']) * MM)
    thumb = Chain(tp, np.array(t['r']) * MM, flat=np.array(t['flat']), k=0.004)
    th = P['thenar']
    a, b = np.array(th['a']) * MM, np.array(th['b']) * MM
    u = unit(b - a); e2 = unit(np.array([0, 0, 1.0]) - u * u[2]); e1 = np.cross(u, e2)
    thenar = lambda p: aniso_cone(p, a, b, th['ra'] * MM, th['rb'] * MM, e1, e2, 1.0, th['flat'])
    L = P['loft']
    loft = Loft(np.array(L['ys']) * MM, np.array(L['cx']) * MM, np.array(L['cz']) * MM, np.array(L['a']) * MM, np.array(L['b']) * MM, n=2.5)
    ks = P.get('smooth', {})
    return HandSDF(loft, fingers, thumb, thenar, k_palm=ks.get('k_palm', 0.0075), k_thenar=ks.get('k_thenar', 0.010), k_fing=ks.get('k_fing', 0.0035)), dict(fingers=fingers, thumb=thumb, thumb_pts=tp)


# ---------------------------------------------------------------------------------------------- surface nets
_CORN = [(i, j, k) for i in (0, 1) for j in (0, 1) for k in (0, 1)]
_EDGES = [(a, b) for a in range(8) for b in range(a + 1, 8) if sum(abs(x - y) for x, y in zip(_CORN[a], _CORN[b])) == 1]


def grid_eval(sdf, lo, hi, h, chunk=400000):
    xs = np.arange(lo[0], hi[0] + h, h); ys = np.arange(lo[1], hi[1] + h, h); zs = np.arange(lo[2], hi[2] + h, h)
    X, Y, Z = np.meshgrid(xs, ys, zs, indexing='ij')
    P = np.stack([X.ravel(), Y.ravel(), Z.ravel()], 1)
    f = np.empty(len(P))
    for a in range(0, len(P), chunk): f[a:a + chunk] = sdf(P[a:a + chunk])
    return f.reshape(X.shape), np.array([xs[0], ys[0], zs[0]])


def surface_nets(f, x0, h):
    nx, ny, nz = f.shape
    s = f < 0
    cs = [s[i:nx - 1 + i, j:ny - 1 + j, k:nz - 1 + k] for (i, j, k) in _CORN]
    cnt = sum(c.astype(np.int8) for c in cs)
    cell = (cnt > 0) & (cnt < 8)
    idx = np.argwhere(cell)
    ids = -np.ones(cell.shape, np.int64); ids[cell] = np.arange(len(idx))
    acc = np.zeros((len(idx), 3)); n = np.zeros(len(idx))
    for (ca, cb) in _EDGES:
        oa, ob = np.array(_CORN[ca]), np.array(_CORN[cb])
        ia, ib = idx + oa, idx + ob
        fa = f[ia[:, 0], ia[:, 1], ia[:, 2]]; fb = f[ib[:, 0], ib[:, 1], ib[:, 2]]
        cr = (fa < 0) != (fb < 0)
        t = np.where(cr, fa / np.where(cr, fa - fb, 1), 0)
        pos = ia + (ib - ia) * t[:, None]
        acc[cr] += pos[cr]; n[cr] += 1
    V = x0 + (acc / n[:, None]) * h
    faces = []
    for ax in range(3):
        b_, c_ = (ax + 1) % 3, (ax + 2) % 3
        sl = [slice(None)] * 3
        # edge between node (i) and (i+1) along ax
        lo = [slice(1, -1)] * 3; lo[ax] = slice(0, -1)
        hi = [slice(1, -1)] * 3; hi[ax] = slice(1, None)
        fa = f[tuple(lo)]; fb = f[tuple(hi)]
        cr = (fa < 0) != (fb < 0)
        ii = np.argwhere(cr)                                   # index into the (1:-1) sub-grid, ax range 0..n-2
        if not len(ii): continue
        node = ii + 1; node[:, ax] = ii[:, ax]                 # node index of the first end
        inside_first = fa[cr] < 0
        quads = []
        for (db, dc) in ((-1, -1), (0, -1), (0, 0), (-1, 0)):
            cidx = node.copy(); cidx[:, b_] += db; cidx[:, c_] += dc
            quads.append(ids[cidx[:, 0], cidx[:, 1], cidx[:, 2]])
        q = np.stack(quads, 1)
        q = np.where(inside_first[:, None], q, q[:, ::-1])
        faces.append(q)
    Q = np.concatenate(faces)
    Q = Q[(Q >= 0).all(1)]
    return V, Q


def quads_to_tris(V, Q):
    d02 = np.linalg.norm(V[Q[:, 0]] - V[Q[:, 2]], axis=1); d13 = np.linalg.norm(V[Q[:, 1]] - V[Q[:, 3]], axis=1)
    use02 = d02 <= d13
    t1 = np.where(use02[:, None], Q[:, [0, 1, 2]], Q[:, [0, 1, 3]])
    t2 = np.where(use02[:, None], Q[:, [0, 2, 3]], Q[:, [1, 2, 3]])
    return np.concatenate([t1, t2])


def edges_of(T):
    e = np.concatenate([T[:, [0, 1]], T[:, [1, 2]], T[:, [2, 0]]])
    e = np.sort(e, 1)
    return np.unique(e, axis=0)


def vertex_normals(V, T):
    fn = np.cross(V[T[:, 1]] - V[T[:, 0]], V[T[:, 2]] - V[T[:, 0]])
    acc = np.zeros_like(V)
    for k in range(3): np.add.at(acc, T[:, k], fn)
    return unit(acc)


def relax_project(V, T, sdf, iters=12, lam=0.5, fixed=None, plane=None, newton=3):
    """tangential Laplacian smoothing followed by projection onto the zero set. `fixed` vertex mask stays put; `plane` = (point, normal): boundary vertices
    flagged in `fixed` slide in that plane only (they are re-projected to the zero set inside the plane)."""
    E = edges_of(T)
    n = len(V)
    deg = np.bincount(E.ravel(), minlength=n).astype(float)
    for it in range(iters):
        acc = np.zeros_like(V)
        np.add.at(acc, E[:, 0], V[E[:, 1]]); np.add.at(acc, E[:, 1], V[E[:, 0]])
        avg = acc / np.maximum(deg, 1)[:, None]
        g = unit(sdf.grad(V))
        d = avg - V
        d -= g * (d * g).sum(1, keepdims=True)
        V = V + lam * d
        for _ in range(newton):
            g = sdf.grad(V); gn = np.linalg.norm(g, axis=1, keepdims=True)
            V = V - sdf(V)[:, None] * g / np.maximum(gn, 1e-9) ** 2
    return V
