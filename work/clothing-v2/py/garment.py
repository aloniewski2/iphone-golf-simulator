"""Garment container: positions + triangles + per-vertex attributes (smooth fields, masks) + per-face attributes (role, fabric, part).
Fabric IDs follow TennisCloth's version-2 decoder (M.A = id/9) with the CLOTHING V2 atlas:
  quadrant (0,0) woven twill   <- id 6 (shorts, trousers, skirts)
  quadrant (1,0) pique knit    <- id 7 (polo / tank / sock body), id 5 laces (tight), id 1 unused
  quadrant (0,1) leather grain <- id 8 (belt, glove, shoe uppers), id 2 (shoe mesh panels)
  quadrant (1,1) 1x1 rib       <- id 9 (collars, cuffs, waistbands, bindings)
  no weave: id 3 rubber outsole, id 4 foam midsole."""
import numpy as np
import glib as G

PIQUE, RIB, TWILL, LEATHER, MESH, RUBBER, FOAM, LACE = 7, 9, 6, 8, 2, 3, 4, 5
KIND = {'shell': 0, 'collar': 1, 'placket': 2, 'button': 3, 'cord': 4, 'loop': 5, 'tongue': 6, 'lace': 7, 'eyestay': 8, 'sole': 9, 'nub': 10,
        'skirt': 11, 'band': 12, 'belt': 13, 'buckle': 14, 'tip': 15, 'pompom': 16, 'tab': 17, 'under': 18, 'upper': 19}


class Garment:
    def __init__(self, pos, tri, role=0, fabric=PIQUE, roles=('Kit_Shirt',), **A):
        self.pos = np.asarray(pos, np.float64); self.tri = np.asarray(tri, np.int64)
        self.roles = list(roles)
        kind = A.pop('kind', 'shell')
        self.A = {k: np.asarray(v, np.float64) for k, v in A.items()}
        n = len(self.tri)
        self.F = {'role': np.full(n, role, np.int32), 'fabric': np.full(n, fabric, np.int32), 'part': np.zeros(n, np.int32), 'proj': np.full(n, 7, np.int32),
                  'kind': np.full(n, KIND[kind], np.int32)}
        self.A.setdefault('trim', np.zeros(len(self.pos)))      # M.B: 0 main colour, 1 trim colour
        self.A.setdefault('cavity', np.ones(len(self.pos)))     # M.G: 1 open, <1 seam / stitch / crease
        self.A.setdefault('lift', np.zeros(len(self.pos)))      # outward offset (m) applied at finish (bands, welts)

    # ---- attribute plumbing
    def _attrs(self):
        keys = list(self.A); return keys, [self.A[k] for k in keys]

    def _set(self, pos, tri, keys, vals, fmap):
        self.pos, self.tri = pos, tri
        self.A = dict(zip(keys, vals))
        self.F = {k: v[fmap] for k, v in self.F.items()}

    def cut(self, f, keep=1):
        """keep the f >= 0 side (keep=-1: f <= 0)."""
        keys, vals = self._attrs()
        f = f if keep > 0 else -f
        x2, t2, f2, a2 = G.cut_isoline(self.pos, self.tri, f, vals)
        fmap = self._face_origin(len(self.tri), t2)
        fc = f2[t2].sum(1)
        sel = fc > 1e-9
        r = G.compact(x2, t2[sel], *a2)
        self._set(r[0], r[1], keys, r[2:-1], fmap[sel])
        return self

    def insert(self, f):
        """insert the f = 0 isoline as an edge loop (nothing removed)."""
        keys, vals = self._attrs()
        x2, t2, f2, a2 = G.cut_isoline(self.pos, self.tri, f, vals)
        fmap = self._face_origin(len(self.tri), t2)
        self._set(x2, t2, keys, a2, fmap)
        return self

    def _face_origin(self, n_old, t_new):
        # cut_isoline keeps the uncut faces first (in order) then appends 3 faces per cut face, in the order of the cut faces
        return self._last_fmap

    def largest(self):
        keys, vals = self._attrs()
        r = G.largest_component(self.pos, self.tri, *vals)
        used_faces = None
        self._set(r[0], r[1], keys, r[2:-1], self._faces_kept(r[-1]))
        return self

    def _faces_kept(self, used):
        remap = -np.ones(len(self.pos) + 1, np.int64)
        return np.nonzero(np.isin(self.tri, used).all(1))[0]

    def merge(self, other):
        n = len(self.pos)
        roles = self.roles[:]
        rmap = []
        for r in other.roles:
            if r not in roles: roles.append(r)
            rmap.append(roles.index(r))
        keys = set(self.A) | set(other.A)
        A = {}
        for k in keys:
            a = self.A.get(k); b = other.A.get(k)
            if a is None: a = np.zeros((len(self.pos),) + b.shape[1:]) + (1 if k == 'cavity' else 0)
            if b is None: b = np.zeros((len(other.pos),) + a.shape[1:]) + (1 if k == 'cavity' else 0)
            A[k] = np.concatenate([a, b])
        F = {}
        for k in self.F:
            v = other.F[k].copy() if k in other.F else np.full(len(other.tri), 7 if k == 'proj' else 0, np.int32)
            if k == 'role': v = np.array(rmap, np.int32)[v]
            F[k] = np.concatenate([self.F[k], v])
        self.pos = np.concatenate([self.pos, other.pos]); self.tri = np.concatenate([self.tri, other.tri + n])
        self.roles = roles; self.A = A; self.F = F
        return self

    def normals(self):
        return G.vertex_normals(self.pos, self.tri)

    def export(self):
        return dict(pos=self.pos, tri=self.tri, nrm=self.normals(), roles=np.array(self.roles), mat=self.F['role'],
                    fabric=self.F['fabric'], part=self.F['part'], proj=self.F['proj'], kind=self.F['kind'], **{'a_' + k: v for k, v in self.A.items()})


# cut_isoline does not report which old face each new face came from; rebuild that mapping here (wraps glib.cut_isoline)
def _cut_with_map(pos, tri, f, attrs=(), eps=1e-7):
    f = np.where(np.abs(f) < eps, eps, f).astype(np.float64)
    s = f > 0
    cross = ~((s[tri[:, 0]] == s[tri[:, 1]]) & (s[tri[:, 1]] == s[tri[:, 2]]))
    keep_idx = np.nonzero(~cross)[0]; cut_idx = np.nonzero(cross)[0]
    x2, t2, f2, a2 = G.cut_isoline(pos, tri, f, attrs, eps)
    fmap = np.concatenate([keep_idx, np.repeat(cut_idx, 3)])
    return x2, t2, f2, a2, fmap


def _patched_cut(self, f, keep=1):
    keys, vals = self._attrs()
    f = f if keep > 0 else -f
    x2, t2, f2, a2, fmap = _cut_with_map(self.pos, self.tri, f, vals)
    fc = f2[t2].sum(1); sel = fc > 1e-9
    r = G.compact(x2, t2[sel], *a2)
    self._set(r[0], r[1], keys, r[2:-1], fmap[sel])
    return self


def _patched_insert(self, f):
    keys, vals = self._attrs()
    x2, t2, f2, a2, fmap = _cut_with_map(self.pos, self.tri, f, vals)
    self._set(x2, t2, keys, a2, fmap)
    return self


def _patched_largest(self):
    keys, vals = self._attrs()
    n = len(self.pos); parent = np.arange(n)
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for a, b in G.unique_edges(self.tri):
        ra, rb = find(a), find(b)
        if ra != rb: parent[ra] = rb
    roots = np.array([find(i) for i in range(n)])
    vals_, cnt = np.unique(roots[self.tri[:, 0]], return_counts=True)
    best = vals_[np.argmax(cnt)]
    sel = roots[self.tri[:, 0]] == best
    r = G.compact(self.pos, self.tri[sel], *vals)
    self._set(r[0], r[1], keys, r[2:-1], np.nonzero(sel)[0])
    return self


Garment.cut = _patched_cut
Garment.insert = _patched_insert
Garment.largest = _patched_largest


def solidify(g, thickness, facing=None, rim_rows=4):
    """thickness everywhere the fabric ends: rounded rim + inner facing (facing=None -> whole inner wall)."""
    n = g.normals()
    lift = g.A['lift']
    x = g.pos + n * lift[:, None]
    fac = 1e9 if facing is None else facing
    X, T, part, src = G.finish(x, g.tri, thickness, fac, rim_rows, normals=G.vertex_normals(x, g.tri))
    A = {k: v[src] for k, v in g.A.items()}
    # faces: outer keep their attributes; facing + rim faces inherit from an outer face sharing their source vertex
    nf_outer = len(g.tri)
    vert_face = np.zeros(len(g.pos), np.int64)
    vert_face[g.tri[:, 0]] = np.arange(nf_outer); vert_face[g.tri[:, 1]] = np.arange(nf_outer); vert_face[g.tri[:, 2]] = np.arange(nf_outer)
    fsrc = np.concatenate([np.arange(nf_outer), vert_face[src[T[nf_outer:, 0]]]])
    F = {k: v[fsrc] for k, v in g.F.items()}
    F['part'] = part
    out = Garment.__new__(Garment)
    out.pos, out.tri, out.roles, out.A, out.F = X, T, g.roles[:], A, F
    out.A['inner'] = np.zeros(len(X)); out.A['inner'][len(g.pos):] = 1
    return out


def smooth_borders(g, iters=6, lam=.4):
    loops = G.boundary_loops(g.tri)
    for l in loops:
        if len(l) < 6: continue
        p = g.pos[l].copy()
        for _ in range(iters):
            p = p + lam * ((np.roll(p, 1, 0) + np.roll(p, -1, 0)) * .5 - p)
        g.pos[l] = p
    return g


def fill_small_holes(g, max_n=12, max_len=.25):
    """close stray little holes left where cut fields meet (fan from the loop's centre)."""
    loops = G.boundary_loops(g.tri)
    newp, newt = [], []
    n = len(g.pos)
    for l in loops:
        L = np.linalg.norm(g.pos[l] - g.pos[np.roll(l, -1)], axis=1).sum()
        if len(l) < 3 or len(l) > max_n or L > max_len: continue
        c = g.pos[l].mean(0)
        ci = n + len(newp); newp.append((c, l))
        # boundary edges run in triangle order; the fan must run the other way round to match the neighbours
        for i in range(len(l)):
            newt.append((l[(i + 1) % len(l)], l[i], ci))
    if not newp: return g
    g.pos = np.concatenate([g.pos, np.array([p for p, _ in newp])])
    for k, v in g.A.items():
        g.A[k] = np.concatenate([v, np.array([v[l].mean(0) for _, l in newp])])
    t = np.array(newt, np.int64)
    src_face = np.zeros(len(t), np.int64)
    g.tri = np.concatenate([g.tri, t])
    for k in g.F: g.F[k] = np.concatenate([g.F[k], np.full(len(t), g.F[k][0], g.F[k].dtype)])
    return g
