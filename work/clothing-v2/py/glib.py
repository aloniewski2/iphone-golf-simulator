"""CLOTHING V2 garment library (runs inside headless Blender for BVH queries; numpy everywhere else).
All geometry is in the body's MESH space: metres, Z up, hero faces -Y, hero's right = +X. Triangle winding: (p1-p0)x(p2-p0) points outward."""
import math
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

# ------------------------------------------------------------------ basic mesh ops

def face_normals(pos, tri):
    return np.cross(pos[tri[:, 1]] - pos[tri[:, 0]], pos[tri[:, 2]] - pos[tri[:, 0]])


def vertex_normals(pos, tri):
    fn = face_normals(pos, tri)
    vn = np.zeros_like(pos)
    for k in range(3): np.add.at(vn, tri[:, k], fn)
    return vn / (np.linalg.norm(vn, axis=1, keepdims=True) + 1e-15)


def edges_of(tri):
    e = np.concatenate([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]])
    return e


def unique_edges(tri):
    e = np.sort(edges_of(tri), axis=1)
    return np.unique(e, axis=0)


def neighbours(n, tri):
    """CSR adjacency (indptr, indices)."""
    e = unique_edges(tri)
    a = np.concatenate([e[:, 0], e[:, 1]]); b = np.concatenate([e[:, 1], e[:, 0]])
    order = np.argsort(a, kind='stable'); a = a[order]; b = b[order]
    indptr = np.zeros(n + 1, np.int64); np.add.at(indptr, a + 1, 1); indptr = np.cumsum(indptr)
    return indptr, b


def umbrella(pos, nb):
    indptr, idx = nb
    cnt = np.diff(indptr).astype(np.float64)
    s = np.add.reduceat(pos[idx], indptr[:-1], axis=0) if len(idx) else np.zeros_like(pos)
    s[cnt == 0] = pos[cnt == 0]; cnt[cnt == 0] = 1
    return s / cnt[:, None] - pos


def boundary_edges(tri):
    e = edges_of(tri)
    k = np.sort(e, axis=1)
    _, inv, cnt = np.unique(k, axis=0, return_inverse=True, return_counts=True)
    return e[cnt[inv] == 1]          # directed as in their triangle


def boundary_loops(tri):
    be = boundary_edges(tri)
    nxt = {int(a): int(b) for a, b in be}
    loops, seen = [], set()
    for s in list(nxt):
        if s in seen: continue
        loop = [s]; seen.add(s); c = nxt[s]
        while c != s and c in nxt and c not in seen:
            loop.append(c); seen.add(c); c = nxt[c]
        loops.append(np.array(loop))
    return loops


def boundary_mask(n, tri):
    m = np.zeros(n, bool); be = boundary_edges(tri); m[be.ravel()] = True; return m


def compact(pos, tri, *attrs):
    used = np.unique(tri); remap = -np.ones(len(pos), np.int64); remap[used] = np.arange(len(used))
    out = [pos[used], remap[tri]] + [a[used] if a is not None else None for a in attrs]
    return out + [used]


def subdivide(pos, tri, *attrs):
    """1 -> 4 midpoint subdivision (linear). attrs interpolate linearly."""
    e = unique_edges(tri)
    n = len(pos)
    key = e[:, 0] * (n + 1) + e[:, 1]
    mid = (pos[e[:, 0]] + pos[e[:, 1]]) * .5
    npos = np.concatenate([pos, mid])
    def eid(a, b):
        k = np.minimum(a, b) * (n + 1) + np.maximum(a, b)
        return n + np.searchsorted(key, k)
    a, b, c = tri[:, 0], tri[:, 1], tri[:, 2]
    ab, bc, ca = eid(a, b), eid(b, c), eid(c, a)
    ntri = np.concatenate([np.stack([a, ab, ca], 1), np.stack([ab, b, bc], 1), np.stack([ca, bc, c], 1), np.stack([ab, bc, ca], 1)])
    nat = []
    for at in attrs:
        if at is None: nat.append(None); continue
        nat.append(np.concatenate([at, (at[e[:, 0]] + at[e[:, 1]]) * .5]))
    return [npos, ntri] + nat


def cut_isoline(pos, tri, f, attrs=(), eps=1e-7):
    """Insert the f = 0 isoline into the mesh (no faces removed). Returns pos, tri, f, attrs; new vertices have f = 0 exactly."""
    f = np.where(np.abs(f) < eps, eps, f).astype(np.float64)
    s = f > 0
    t0, t1, t2 = tri[:, 0], tri[:, 1], tri[:, 2]
    cross = ~((s[t0] == s[t1]) & (s[t1] == s[t2]))
    keep = tri[~cross]
    ct = tri[cross]
    n = len(pos)
    new_pos, new_f, new_attr = [], [], [[] for _ in attrs]
    emap = {}
    def ev(a, b):
        k = (a, b) if a < b else (b, a)
        if k in emap: return emap[k]
        t = f[a] / (f[a] - f[b])
        idx = n + len(new_pos)
        new_pos.append(pos[a] + (pos[b] - pos[a]) * t); new_f.append(0.0)
        for j, at in enumerate(attrs): new_attr[j].append(at[a] + (at[b] - at[a]) * t)
        emap[k] = idx; return idx
    out = []
    for a, b, c in ct:
        sa, sb, sc = s[a], s[b], s[c]
        # rotate so that the lonely vertex is first
        if sa == sb: a, b, c = c, a, b
        elif sa == sc: a, b, c = b, c, a
        # now a is alone; b, c on the other side
        ab = ev(a, b); ac = ev(a, c)
        out.append((a, ab, ac)); out.append((ab, b, c)); out.append((ab, c, ac))
    ntri = np.concatenate([keep, np.array(out, np.int64).reshape(-1, 3)]) if out else keep
    npos = np.concatenate([pos, np.array(new_pos).reshape(-1, 3)]) if new_pos else pos
    nf = np.concatenate([f, np.array(new_f)]) if new_f else f
    natt = [np.concatenate([at, np.array(new_attr[j]).reshape((-1,) + at.shape[1:])]) if new_pos else at for j, at in enumerate(attrs)]
    return npos, ntri, nf, natt


def keep_side(pos, tri, f, attrs=(), side=1, tol=1e-9):
    """drop faces lying on the f < 0 side (after cut_isoline every face is on one side)."""
    fc = f[tri].sum(1) if side > 0 else -f[tri].sum(1)
    t = tri[fc > tol]
    r = compact(pos, t, f, *attrs)
    return r[0], r[1], r[2], r[3:-1], r[-1]


def largest_component(pos, tri, *attrs):
    n = len(pos); parent = np.arange(n)
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for a, b in unique_edges(tri):
        ra, rb = find(a), find(b)
        if ra != rb: parent[ra] = rb
    roots = np.array([find(i) for i in range(n)])
    used = np.unique(tri)
    vals, cnt = np.unique(roots[used], return_counts=True)
    best = vals[np.argmax(cnt)]
    t = tri[roots[tri[:, 0]] == best]
    r = compact(pos, t, *attrs)
    return r


# ------------------------------------------------------------------ body

class Body:
    def __init__(self, hero):
        self.hero = hero
        b = hero.body
        self.pos = b.pos.copy(); self.tri = b.tri.copy(); self.nrm = b.nrm.copy()
        self.bones = b.bones; self.bi = b.bi; self.bw = b.bw
        self.bvh = BVHTree.FromPolygons([tuple(p) for p in self.pos], [tuple(t) for t in self.tri], all_triangles=True)
        inv = np.linalg.inv(b.r2w)
        self.bone_mesh = {name: inv @ hero.rest_bones[i] for i, name in enumerate(self.bones)}   # bone frames in mesh space
        # dense weights (n, nb)
        W = np.zeros((len(self.pos), len(self.bones)))
        for k in range(4): np.add.at(W, (np.arange(len(self.pos)), self.bi[:, k]), self.bw[:, k])
        self.W = W
        self.fnorm = face_normals(self.pos, self.tri); self.fnorm /= np.linalg.norm(self.fnorm, axis=1, keepdims=True) + 1e-15
        self.classify()

    def classify(self):
        """Region memberships from the TENNIS body's weights (the golf body has the same vertices, coarser 53-bone weights)."""
        import cv2io
        tid = 'MatchHero_' + ('Female' if self.hero.female else 'Male')
        src = self.hero if self.hero.id == tid else cv2io.Hero(tid)
        b = src.body
        W = np.zeros((len(b.pos), len(b.bones)))
        for k in range(4): np.add.at(W, (np.arange(len(b.pos)), b.bi[:, k]), b.bw[:, k])
        def grp(pred): return W[:, [i for i, n in enumerate(b.bones) if pred(n)]].sum(1)
        digits = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
        def arm(side, a, bb, c):
            return grp(lambda n: (n.startswith(side) and any(k in n for k in ['UpperArm', 'LowerArm', 'Hand'] + digits)) or any(n.startswith('ArmBlend_%s_' % x) for x in [a, bb, c]))
        self.armL = arm('Left', '7_8', '8_9', '9_10'); self.armR = arm('Right', '26_27', '27_28', '28_29')
        self.forearmL = grp(lambda n: (n.startswith('Left') and any(k in n for k in ['LowerArm', 'Hand'] + digits)) or n.startswith('ArmBlend_9_10_'))
        self.forearmR = grp(lambda n: (n.startswith('Right') and any(k in n for k in ['LowerArm', 'Hand'] + digits)) or n.startswith('ArmBlend_28_29_'))
        self.handL = grp(lambda n: n.startswith('Left') and any(k in n for k in ['Hand'] + digits)); self.handR = grp(lambda n: n.startswith('Right') and any(k in n for k in ['Hand'] + digits))
        self.legL = grp(lambda n: n.startswith('Left') and any(k in n for k in ['UpperLeg', 'LowerLeg', 'Foot', 'Toes']))
        self.legR = grp(lambda n: n.startswith('Right') and any(k in n for k in ['UpperLeg', 'LowerLeg', 'Foot', 'Toes']))
        self.shinL = grp(lambda n: n.startswith('Left') and any(k in n for k in ['LowerLeg', 'Foot', 'Toes'])); self.shinR = grp(lambda n: n.startswith('Right') and any(k in n for k in ['LowerLeg', 'Foot', 'Toes']))
        self.footL = grp(lambda n: n.startswith('Left') and any(k in n for k in ['Foot', 'Toes'])); self.footR = grp(lambda n: n.startswith('Right') and any(k in n for k in ['Foot', 'Toes']))
        self.headw = grp(lambda n: n in ('Head',) or 'Eye' in n or 'Jaw' in n or n.startswith('Face') or 'Brow' in n or 'Lid' in n)
        self.neckw = grp(lambda n: n == 'Neck')

    def head(self, bone):
        return self.bone_mesh[bone][:3, 3].copy()

    def group_weight(self, names):
        idx = [i for i, n in enumerate(self.bones) if any(n == x or n.startswith(x) for x in names)]
        return self.W[:, idx].sum(1)

    def nearest(self, pts):
        n = len(pts); cp = np.zeros((n, 3)); fi = np.zeros(n, np.int64); d = np.zeros(n)
        fnd = self.bvh.find_nearest
        for i in range(n):
            r = fnd(Vector(pts[i]))
            cp[i] = r[0]; fi[i] = r[2]; d[i] = r[3]
        # sign from the smooth face normal
        sgn = np.sign(np.einsum('ij,ij->i', pts - cp, self.fnorm[fi])); sgn[sgn == 0] = 1
        return cp, fi, d * sgn

    def bary(self, pts, fi):
        t = self.tri[fi]; a, b, c = self.pos[t[:, 0]], self.pos[t[:, 1]], self.pos[t[:, 2]]
        v0, v1, v2 = b - a, c - a, pts - a
        d00 = (v0 * v0).sum(1); d01 = (v0 * v1).sum(1); d11 = (v1 * v1).sum(1); d20 = (v2 * v0).sum(1); d21 = (v2 * v1).sum(1)
        den = d00 * d11 - d01 * d01; den[den == 0] = 1e-20
        v = (d11 * d20 - d01 * d21) / den; w = (d00 * d21 - d01 * d20) / den; u = 1 - v - w
        bc = np.clip(np.stack([u, v, w], 1), 0, 1); bc /= bc.sum(1, keepdims=True)
        return bc

    def smooth_normal_at(self, fi, bc):
        t = self.tri[fi]
        n = (self.nrm[t] * bc[:, :, None]).sum(1)
        return n / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-15)

    def weights_at(self, fi, bc):
        t = self.tri[fi]
        return (self.W[t] * bc[:, :, None]).sum(1)


# ------------------------------------------------------------------ fabric solve

def solve_shell(body, pos, tri, clearance, iters=60, lam=.5, mu=-.53, env=None, pin=None, push_iters=2, smooth_iters=1, start=None, obstacles=()):
    """Smooth envelope over the body: Taubin smoothing (kills muscle detail, keeps volume) + push-out to `clearance` (per vertex, metres)
    + optional envelope constraint env(pos) -> corrected pos (cylindrical hang rules). pin: mask of vertices kept fixed."""
    nb = neighbours(len(pos), tri)
    x = pos.copy() if start is None else start.copy()
    clearance = np.broadcast_to(np.asarray(clearance, np.float64), (len(pos),)).copy()
    for it in range(iters):
        for _ in range(smooth_iters):
            x += lam * umbrella(x, nb) * (1 if pin is None else (~pin)[:, None])
            x += mu * umbrella(x, nb) * (1 if pin is None else (~pin)[:, None])
        if env is not None: x = env(x)
        for _ in range(push_iters):
            cp, fi, d = body.nearest(x)
            bad = d < clearance
            if bad.any():
                bc = body.bary(cp[bad], fi[bad]); n = body.smooth_normal_at(fi[bad], bc)
                x[bad] = cp[bad] + n * clearance[bad, None]
            x = push_obstacles(x, obstacles)
    if iters == 0: x = push_obstacles(x, obstacles)
    return x


def push_obstacles(x, obstacles):
    for ob in obstacles:
        tree, clear = ob[0], ob[1]
        mask = ob[2] if len(ob) > 2 else None
        idx = np.nonzero(mask)[0] if mask is not None else range(len(x))
        for i in idx:
            r = tree.find_nearest(Vector(x[i]))
            if r[0] is None: continue
            c = np.array(r[0]); n = np.array(r[1]); d = np.dot(x[i] - c, n)
            cl = clear[i] if np.ndim(clear) else clear
            if d < cl and np.linalg.norm(x[i] - c) < .06: x[i] = c + n * cl
    return x


def relax_tangential(x, tri, iters=10, step=.5, pin=None):
    """even out vertex spacing without changing the surface much: move along the umbrella vector minus its normal component."""
    nb = neighbours(len(x), tri)
    for _ in range(iters):
        n = vertex_normals(x, tri)
        u = umbrella(x, nb)
        u -= n * np.einsum('ij,ij->i', u, n)[:, None]
        if pin is not None: u[pin] = 0
        x = x + step * u
    return x


# ------------------------------------------------------------------ envelopes (cylindrical "hang" rules)

def cyl_coords(x, origin, axis, ref):
    """s along axis, r, theta (ref = 0 deg direction, perpendicular to axis)."""
    axis = axis / np.linalg.norm(axis)
    ref = ref - axis * np.dot(ref, axis); ref /= np.linalg.norm(ref)
    side = np.cross(axis, ref)
    d = x - origin; s = d @ axis; q = d - np.outer(s, axis)
    r = np.linalg.norm(q, axis=1); th = np.arctan2(q @ side, q @ ref)
    return s, r, th, (axis, ref, side)


def from_cyl(s, r, th, origin, frame):
    axis, ref, side = frame
    return origin + np.outer(s, axis) + np.outer(r * np.cos(th), ref) + np.outer(r * np.sin(th), side)


def hang_envelope(s, r, th, mask, s_start, slope, nth=96, ds=.01, direction=-1, smooth_th=2, extra=0.0):
    """For masked vertices: r >= env(th, s) where env follows the fabric hanging from its widest point: walking from s_start in `direction`,
    env(s) = max(r_max(s), env(s_prev) - slope*ds). Returns new r (only masked changed)."""
    th_bin = ((th + math.pi) / (2 * math.pi) * nth).astype(int) % nth
    smin, smax = s[mask].min(), s[mask].max()
    nsb = int(math.ceil((smax - smin) / ds)) + 1
    s_bin = np.clip(((s - smin) / ds).astype(int), 0, nsb - 1)
    rmax = np.zeros((nth, nsb))
    np.maximum.at(rmax, (th_bin[mask], s_bin[mask]), r[mask])
    # fill empty bins by neighbours in s
    for i in range(nth):
        row = rmax[i]
        nz = np.nonzero(row)[0]
        if len(nz): rmax[i] = np.interp(np.arange(nsb), nz, row[nz])
    # smooth around theta (circular)
    for _ in range(smooth_th):
        rmax = np.maximum(rmax, (np.roll(rmax, 1, 0) + np.roll(rmax, -1, 0) + rmax) / 3)
    env = rmax.copy()
    start = int(np.clip((s_start - smin) / ds, 0, nsb - 1))
    rng = range(start - 1, -1, -1) if direction < 0 else range(start + 1, nsb)
    prev = start
    for j in rng:
        env[:, j] = np.maximum(env[:, j], env[:, prev] - slope * ds)
        prev = j
    rn = r.copy()
    e = env[th_bin, s_bin] + extra
    m = mask & (r < e)
    rn[m] = e[m]
    return rn


# ------------------------------------------------------------------ skin weights

def transfer_weights(body, x, smooth=0, tri=None, allowed=None, max_inf=4, smooth_mask=None):
    cp, fi, d = body.nearest(x)
    bc = body.bary(cp, fi)
    W = body.weights_at(fi, bc)
    if allowed is not None: W[:, ~allowed] = 0
    if smooth and tri is not None:
        nb = neighbours(len(x), tri)
        for _ in range(smooth):
            u = umbrella(W, nb)
            W = W + (.5 * u if smooth_mask is None else .5 * u * smooth_mask[:, None])
    return prune(W, max_inf)


def prune(W, k=4):
    idx = np.argsort(-W, axis=1)[:, :k]
    w = np.take_along_axis(W, idx, 1); w = np.clip(w, 0, None)
    s = w.sum(1, keepdims=True); s[s == 0] = 1; w = w / s
    w[w < .001] = 0; w /= w.sum(1, keepdims=True)
    return idx.astype(np.int32), w


# ------------------------------------------------------------------ thickness: rim + inner facing

def offset_inner(x, tri, t):
    return x - vertex_normals(x, tri) * t


def ring_strip(a_idx, b_idx, closed=True):
    """triangles joining two vertex loops of equal length (a -> b)."""
    n = len(a_idx); out = []
    m = n if closed else n - 1
    for i in range(m):
        j = (i + 1) % n
        out.append((a_idx[i], a_idx[j], b_idx[j])); out.append((a_idx[i], b_idx[j], b_idx[i]))
    return np.array(out, np.int64)


# ------------------------------------------------------------------ cross-section hulls (fabric spans hollows, rests on high points)

def slice_points(pos, edges, origin, normal, mask=None):
    """intersection points of mesh edges with the plane through origin (normal). mask: per-vertex, edge kept if both ends masked."""
    h = (pos - origin) @ normal
    a, b = edges[:, 0], edges[:, 1]
    c = (np.sign(h[a]) != np.sign(h[b]))
    if mask is not None: c &= mask[a] & mask[b]
    a, b = a[c], b[c]
    t = h[a] / (h[a] - h[b])
    return pos[a] + (pos[b] - pos[a]) * t[:, None]


def convex_hull_2d(p):
    p = np.unique(np.round(p, 7), axis=0)
    if len(p) < 3: return p
    p = p[np.lexsort((p[:, 1], p[:, 0]))]
    def cross(o, a, b): return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])
    lo, up = [], []
    for q in p:
        while len(lo) >= 2 and cross(lo[-2], lo[-1], q) <= 0: lo.pop()
        lo.append(tuple(q))
    for q in p[::-1]:
        while len(up) >= 2 and cross(up[-2], up[-1], q) <= 0: up.pop()
        up.append(tuple(q))
    return np.array(lo[:-1] + up[:-1])


def hull_radius(hull, centre, thetas):
    """distance from centre to the convex polygon along each direction theta (radians, measured in the polygon's 2D frame)."""
    if len(hull) < 3: return np.zeros(len(thetas))
    d = np.stack([np.cos(thetas), np.sin(thetas)], 1)              # (k,2)
    a = hull - centre; b = np.roll(hull, -1, 0) - centre             # (m,2)
    e = b - a
    # solve centre + t d = a + u e  ->  t d - u e = a
    det = d[:, None, 0] * (-e[None, :, 1]) - d[:, None, 1] * (-e[None, :, 0])
    det[np.abs(det) < 1e-12] = 1e-12
    t = (a[None, :, 0] * (-e[None, :, 1]) - a[None, :, 1] * (-e[None, :, 0])) / det
    u = (d[:, None, 0] * a[None, :, 1] - d[:, None, 1] * a[None, :, 0]) / det
    ok = (u >= -1e-9) & (u <= 1 + 1e-9) & (t > 0)
    t = np.where(ok, t, 0)
    return t.max(1)


class Envelope:
    """r_env(theta, s) on a grid; bilinear lookup; built from hull radii per slice, swept to model hanging fabric."""
    def __init__(self, s0, ds, thetas, R):
        self.s0, self.ds, self.th, self.R = s0, ds, thetas, R
    def sweep(self, start_index, direction, slope):
        R = self.R; ns = R.shape[1]
        rng = range(start_index - 1, -1, -1) if direction < 0 else range(start_index + 1, ns)
        prev = start_index
        for j in rng:
            R[:, j] = np.maximum(R[:, j], R[:, prev] - slope * self.ds); prev = j
        return self
    def smooth(self, k_th=2, k_s=1, iters=2):
        R = self.R
        for _ in range(iters):
            R = (np.roll(R, 1, 0) + R * 2 + np.roll(R, -1, 0)) / 4 if k_th else R
            if k_s:
                Rp = np.concatenate([R[:, :1], R, R[:, -1:]], 1)
                R = (Rp[:, :-2] + 2 * Rp[:, 1:-1] + Rp[:, 2:]) / 4
        self.R = R; return self
    def __call__(self, th, s):
        nth = len(self.th)
        u = (th - self.th[0]) / (self.th[1] - self.th[0]); u = np.mod(u, nth)
        i0 = np.floor(u).astype(int) % nth; i1 = (i0 + 1) % nth; fu = u - np.floor(u)
        v = np.clip((s - self.s0) / self.ds, 0, self.R.shape[1] - 1.001)
        j0 = np.floor(v).astype(int); j1 = j0 + 1; fv = v - j0
        R = self.R
        return (R[i0, j0] * (1 - fu) * (1 - fv) + R[i1, j0] * fu * (1 - fv) + R[i0, j1] * (1 - fu) * fv + R[i1, j1] * fu * fv)


def build_envelope(body, origin_fn, axis, ref, s_lo, s_hi, ds, mask, nth=180, pad=None):
    """slices perpendicular to `axis` from s_lo to s_hi; origin_fn(s) = slice centre (3,) (may follow a bent axis).
    Hull of the body's masked surface in each slice; radius per theta. Returns Envelope and the frame."""
    axis = axis / np.linalg.norm(axis); ref = ref - axis * (ref @ axis); ref /= np.linalg.norm(ref); side = np.cross(axis, ref)
    E = unique_edges(body.tri)
    thetas = np.linspace(-math.pi, math.pi, nth, endpoint=False)
    ss = np.arange(s_lo, s_hi + ds * .5, ds)
    R = np.zeros((nth, len(ss)))
    for j, s in enumerate(ss):
        o = origin_fn(np.array([s]))[0]
        pts = slice_points(body.pos, E, o, axis, mask)
        if len(pts) < 3: continue
        q = np.stack([(pts - o) @ ref, (pts - o) @ side], 1)
        h = convex_hull_2d(q)
        R[:, j] = hull_radius(h, np.zeros(2), thetas)
    # fill empty slices
    for i in range(nth):
        nz = np.nonzero(R[i])[0]
        if len(nz): R[i] = np.interp(np.arange(len(ss)), nz, R[i, nz])
    return Envelope(s_lo, ds, thetas, R), (axis, ref, side)


def frame_coords(x, origin_fn, frame, s_ref_origin):
    """s along axis measured from s_ref_origin; theta and r around origin_fn(s)."""
    axis, ref, side = frame
    s = (x - s_ref_origin) @ axis
    o = origin_fn(s)
    q = x - o - np.outer((x - o) @ axis, axis)
    r = np.linalg.norm(q, axis=1); th = np.arctan2(q @ side, q @ ref)
    return s, th, r, o


def apply_envelope(x, mask, env, origin_fn, frame, s_ref_origin, ease, blend=None):
    """push masked vertices radially out to env + ease (ease may be per-vertex). blend: per-vertex 0..1 weight of the rule."""
    if not mask.any(): return x
    axis, ref, side = frame
    xm = x[mask]
    s, th, r, o = frame_coords(xm, origin_fn, frame, s_ref_origin)
    target = env(th, s) + (ease[mask] if np.ndim(ease) else ease)
    if blend is not None: target = r + (np.maximum(target, r) - r) * blend[mask]
    rn = np.maximum(r, target)
    q = np.outer(np.cos(th), ref) + np.outer(np.sin(th), side)
    x = x.copy()
    x[mask] = o + np.outer((xm - o) @ axis, axis) + q * rn[:, None]
    return x


# ------------------------------------------------------------------ cutting with smooth fields

def smooth_field(f, tri, iters=20, fixed=None):
    nb = neighbours(len(f), tri)
    g = f.astype(np.float64).copy()
    for _ in range(iters):
        u = umbrella(g[:, None], nb)[:, 0]
        if fixed is not None: u[fixed] = 0
        g += .5 * u
    return g


def cut_keep(x, tri, f, attrs=()):
    """keep the f >= 0 side; the f = 0 isoline becomes the clean border."""
    x2, t2, f2, a2 = cut_isoline(x, tri, f, attrs)
    x3, t3, f3, a3, used = keep_side(x2, t2, f2, a2, side=1)
    return x3, t3, a3


def loop_resample_check(x, loops):
    return [np.linalg.norm(x[l] - x[np.roll(l, -1)], axis=1) for l in loops]


# ------------------------------------------------------------------ finished edges: rolled rim + inner facing

def geodesic_from(x, tri, seeds, maxd=1.0):
    """Dijkstra distance along edges from seed vertices (approximate geodesic)."""
    import heapq
    nb = neighbours(len(x), tri)
    indptr, idx = nb
    d = np.full(len(x), np.inf)
    h = []
    for s in seeds: d[s] = 0; h.append((0.0, int(s)))
    heapq.heapify(h)
    while h:
        dv, v = heapq.heappop(h)
        if dv > d[v] or dv > maxd: continue
        for k in range(indptr[v], indptr[v + 1]):
            w = idx[k]; nd = dv + np.linalg.norm(x[w] - x[v])
            if nd < d[w]: d[w] = nd; heapq.heappush(h, (nd, w))
    return d


def finish(x, tri, thickness=.0025, facing=.03, rim_rows=3, normals=None):
    """Outer shell -> closed-looking garment: every border gets a rounded rim of `thickness`, and an inner facing strip
    (the outer faces within `facing` of a border, offset inward, reversed). Returns pos, tri, part ids (0 outer, 1 rim, 2 facing),
    and for every new vertex the outer vertex it came from (for weights / uvs)."""
    n = vertex_normals(x, tri) if normals is None else normals
    loops = boundary_loops(tri)
    seeds = np.concatenate(loops) if loops else np.array([], np.int64)
    d = geodesic_from(x, tri, seeds, maxd=facing * 1.5)
    # inner facing: faces with all verts within facing
    fm = (d[tri] <= facing).all(1)
    ftri = tri[fm]
    fv = np.unique(ftri)
    remap = -np.ones(len(x), np.int64); remap[fv] = np.arange(len(fv)) + len(x)
    inner = x[fv] - n[fv] * thickness
    pos = [x, inner]; src = [np.arange(len(x)), fv]
    tris = [tri, remap[ftri][:, ::-1]]
    part = [np.zeros(len(tri), np.int32), np.full(len(ftri), 2, np.int32)]
    base = len(x) + len(fv)
    # rims: outer loop -> mid rows (bulging outward past the border) -> inner loop
    for loop in loops:
        L = len(loop)
        p = x[loop]; nn = n[loop]
        tang = np.roll(p, -1, 0) - np.roll(p, 1, 0); tang /= np.linalg.norm(tang, axis=1, keepdims=True) + 1e-12
        outdir = np.cross(tang, nn); outdir /= np.linalg.norm(outdir, axis=1, keepdims=True) + 1e-12
        # outdir must point away from the garment: check against the neighbouring interior
        cen = x[tri[(np.isin(tri, loop)).any(1)].ravel()].mean(0)
        if np.mean(np.einsum('ij,ij->i', outdir, p - cen)) < 0: outdir = -outdir
        rows = [loop]
        for k in range(1, rim_rows):
            a = math.pi * k / rim_rows
            q = p - nn * thickness * (1 - math.cos(a)) * .5 + outdir * thickness * .5 * math.sin(a)
            ids = np.arange(base, base + L); base += L
            pos.append(q); src.append(loop); rows.append(ids)
        rows.append(remap[loop])
        for a_, b_ in zip(rows[:-1], rows[1:]):
            st = ring_strip(a_, b_, closed=True)
            # orient: outer -> inner should face outward-ish (away from the garment interior)
            fnv = face_normals(np.concatenate(pos), st[:1])
            tris.append(st); part.append(np.ones(len(st), np.int32))
    P = np.concatenate(pos); T = np.concatenate(tris); S = np.concatenate(src); Pt = np.concatenate(part)
    # fix rim orientation per strip: rim face normals should point along outdir/normal mix -> compare with centroid offset from source surface
    rim = Pt == 1
    fn = face_normals(P, T[rim]); c = P[T[rim]].mean(1); sc = x[S[T[rim][:, 0]]]
    flip = np.einsum('ij,ij->i', fn, c - (sc - n[S[T[rim][:, 0]]] * thickness * .5)) < 0
    Tr = T[rim]; Tr[flip] = Tr[flip][:, ::-1]; T[rim] = Tr
    return P, T, Pt, S


def edge_distance(x, tri, loops, step=.002, maxd=.05):
    """Euclidean distance from every vertex to the (resampled) border loops: smooth enough to cut bands of a few mm."""
    samples = []
    for l in loops:
        p = x[l]; q = np.concatenate([p, p[:1]])
        seg = np.linalg.norm(np.diff(q, axis=0), axis=1); s = np.concatenate([[0], np.cumsum(seg)])
        t = np.arange(0, s[-1], step)
        samples.append(np.stack([np.interp(t, s, q[:, k]) for k in range(3)], 1))
    S = np.concatenate(samples)
    d = np.full(len(x), np.inf)
    for i in range(0, len(x), 2000):
        blk = x[i:i + 2000]
        dd = np.sqrt(((blk[:, None, :] - S[None, :, :]) ** 2).sum(-1)).min(1)
        d[i:i + 2000] = dd
    return d


def smax(a, b, k=.01):
    return (a + b + np.sqrt((a - b) ** 2 + k * k)) * .5


def smin(a, b, k=.01):
    return (a + b - np.sqrt((a - b) ** 2 + k * k)) * .5


def load_surface(hero_id, piece):
    import os, cv2io
    f = os.path.join(cv2io.ROOT, 'out', hero_id, 'surf_' + piece + '.npz')
    if not os.path.exists(f): return None
    d = np.load(f)
    return d['pos'], d['tri']


def obstacle(hero_id, piece, clear, mask_fn=None):
    s = load_surface(hero_id, piece)
    if s is None: return None
    tree = BVHTree.FromPolygons([tuple(p) for p in s[0]], [tuple(t) for t in s[1]], all_triangles=True)
    return (tree, clear) if mask_fn is None else (tree, clear, mask_fn)


# ------------------------------------------------------------------ pattern-first shaping: the garment's own cut, the body only pushes it out

def superellipse(a, b, n=2.4, k=96, cx=0.0, cy=0.0):
    t = np.linspace(0, 2 * math.pi, k, endpoint=False)
    c, s = np.cos(t), np.sin(t)
    return np.stack([cx + a * np.sign(c) * np.abs(c) ** (2 / n), cy + b * np.sign(s) * np.abs(s) ** (2 / n)], 1)


def girth_section(girth, aspect, n=2.4, cx=0.0, cy=0.0):
    """closed section of the given girth (perimeter, metres), width/depth = aspect, centred at (cx, cy) in the slice frame (ref, side)."""
    p = superellipse(1.0, 1.0 / aspect, n)
    per = np.linalg.norm(p - np.roll(p, 1, 0), axis=1).sum()
    k = girth / per
    return superellipse(k, k / aspect, n, cx=cx, cy=cy)


def pattern_envelope(hull_env, girth_fn, aspect_fn, centre_fn, clear_min, n=2.4, smooth=2):
    """R(theta, s) = max(the garment's own section of girth_fn(s), the body hull + clear_min). centre_fn(s) -> (cx, cy) in the slice frame."""
    E = Envelope(hull_env.s0, hull_env.ds, hull_env.th, hull_env.R.copy())
    for j in range(E.R.shape[1]):
        s = E.s0 + j * E.ds
        g = girth_fn(s)
        if g is None or g <= 0: continue
        cx, cy = centre_fn(s)
        sec = girth_section(g, aspect_fn(s), n, cx, cy)
        rd = hull_radius(sec, np.zeros(2), E.th)
        E.R[:, j] = np.maximum(rd, hull_env.R[:, j] + clear_min)
    if smooth: E.smooth(iters=smooth)
    return E


def hull_centre(hull_env, s):
    """centroid (in the slice frame) of the hull at s, from the radius table."""
    j = int(np.clip(round((s - hull_env.s0) / hull_env.ds), 0, hull_env.R.shape[1] - 1))
    r = hull_env.R[:, j]; th = hull_env.th
    return float(np.mean(r * np.cos(th))), float(np.mean(r * np.sin(th)))


def hull_aspect(hull_env, s):
    j = int(np.clip(round((s - hull_env.s0) / hull_env.ds), 0, hull_env.R.shape[1] - 1))
    r = hull_env.R[:, j]; th = hull_env.th
    x = r * np.cos(th); y = r * np.sin(th)
    return float((x.max() - x.min()) / max(1e-6, (y.max() - y.min())))


def fill_tiny_holes(pos, tri, extent=.035, *attrs):
    """Close tiny holes (the body mesh has a few at the back of the shoulders): boundary edges grouped by connectivity; every group whose
    bounding box is smaller than `extent` gets a fan to a new centre vertex (edges reversed, so the winding matches). attrs are averaged."""
    be = boundary_edges(tri)
    if len(be) == 0: return (pos, tri) + tuple(attrs)
    parent = {}
    def find(a):
        while parent.setdefault(a, a) != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for a, b in be:
        ra, rb = find(int(a)), find(int(b))
        if ra != rb: parent[ra] = rb
    groups = {}
    for k, (a, b) in enumerate(be): groups.setdefault(find(int(a)), []).append(k)
    newp, newt, newa = [], [], [[] for _ in attrs]
    n = len(pos)
    for g, ks in groups.items():
        vs = np.unique(be[ks].ravel())
        ext = pos[vs].max(0) - pos[vs].min(0)
        if ext.max() > extent: continue
        ci = n + len(newp); newp.append(pos[vs].mean(0))
        for j, a in enumerate(attrs): newa[j].append(a[vs].mean(0))
        for k in ks:
            a, b = be[k]; newt.append((b, a, ci))
    if not newp: return (pos, tri) + tuple(attrs)
    pos = np.concatenate([pos, np.array(newp)]); tri = np.concatenate([tri, np.array(newt, np.int64)])
    attrs = tuple(np.concatenate([a, np.array(na)]) for a, na in zip(attrs, newa))
    return (pos, tri) + attrs


def body_no_arms(body, thresh=.5):
    """a copy of the Body whose nearest-surface queries ignore the arms and hands (a hand hanging beside a skirt must not push it)."""
    import copy
    b2 = copy.copy(body)
    arm = (body.armL + body.armR)
    keep = (arm[body.tri] < thresh).all(1)
    b2.tri = body.tri[keep]; b2.fnorm = body.fnorm[keep]
    b2.bvh = BVHTree.FromPolygons([tuple(p) for p in body.pos], [tuple(t) for t in b2.tri], all_triangles=True)
    return b2
