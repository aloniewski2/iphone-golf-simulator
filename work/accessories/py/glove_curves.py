"""GLOVE: construction curves painted on the glove surface (seams, pad outline, side seams) and perforation centres.
Every curve is stored as (triangle id, barycentric) samples on the product's outer mesh, so it follows the mesh through any fit (warp / hug / pose):
positions in any fitted state = bary-weighted fitted vertex positions. The rules below are written in the product frame G and use the product skeleton."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import glove_sdf as S

MM = 1e-3


class Surf:
    def __init__(self, O, T):
        self.O, self.T = np.asarray(O), np.asarray(T)
        self.tree = BVHTree.FromPolygons([tuple(p) for p in self.O], [tuple(t) for t in self.T], all_triangles=True)

    def _bary(self, loc, idx):
        a, b, c = self.O[self.T[idx]]
        v0, v1, v2 = b - a, c - a, np.asarray(loc) - a
        d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-20
        v = (d11 * d20 - d01 * d21) / den; w = (d00 * d21 - d01 * d20) / den
        return np.array([1 - v - w, v, w])

    def ray(self, o, d, maxd=.3):
        r = self.tree.ray_cast(Vector(o), Vector(d), maxd)
        if r[0] is None: return None
        return r[2], self._bary(r[0], r[2]), np.array(r[0])

    def snap(self, p, maxd=.05):
        r = self.tree.find_nearest(Vector(p), maxd)
        if r[0] is None: return None
        return r[2], self._bary(r[0], r[2]), np.array(r[0])


class Curve:
    def __init__(self, name, kind='seam', closed=False):
        self.name, self.kind, self.closed = name, kind, closed
        self.tri = []; self.bary = []; self.pts0 = []; self.wf = []; self.tip = None

    def add(self, hit, wf=1.0):
        if hit is None: return
        self.tri.append(hit[0]); self.bary.append(hit[1]); self.pts0.append(hit[2]); self.wf.append(wf)

    def finish(self):
        self.tri = np.asarray(self.tri, np.int64); self.bary = np.asarray(self.bary, float); self.pts0 = np.asarray(self.pts0, float); self.wf = np.asarray(self.wf, float)
        return self

    def clean(self, max_jump=0.0028, min_len=0.012):
        """drop stray fragments: split the sample list where consecutive samples jump (a ray that hit a different surface) and keep only runs longer than min_len"""
        P = self.pts0
        jump = np.linalg.norm(np.diff(P, axis=0), axis=1) > max_jump
        starts = np.concatenate([[0], np.nonzero(jump)[0] + 1]); ends = np.concatenate([np.nonzero(jump)[0] + 1, [len(P)]])
        keep = np.zeros(len(P), bool)
        for a, b in zip(starts, ends):
            if np.linalg.norm(np.diff(P[a:b], axis=0), axis=1).sum() >= min_len: keep[a:b] = True
        apex = P[self.tip] if self.tip is not None else None
        self.tri, self.bary, self.pts0, self.wf = self.tri[keep], self.bary[keep], self.pts0[keep], self.wf[keep]
        if apex is not None: self.tip = int(np.argmin(np.linalg.norm(self.pts0 - apex, axis=1)))
        return self

    def smooth(self, surf, win=6, passes=2):
        """moving-average the sample positions (ends kept) and snap them back onto the surface: removes the jitter of ray hits across concave webs"""
        P = self.pts0.copy(); n = len(P)
        for _ in range(passes):
            Q = P.copy()
            for i in range(n):
                a, b = max(0, i - win), min(n, i + win + 1)
                Q[i] = P[a:b].mean(0)
            Q[0], Q[-1] = P[0], P[-1]
            P = Q
        tri, bary, pts = [], [], []; wf = []
        apex = self.pts0[self.tip] if self.tip is not None else None
        for i, p in enumerate(P):
            h = surf.snap(p, .02)
            if h is None: continue
            tri.append(h[0]); bary.append(h[1]); pts.append(h[2]); wf.append(self.wf[i])
        self.tri, self.bary, self.pts0, self.wf = np.array(tri, np.int64), np.array(bary), np.array(pts), np.array(wf)
        if apex is not None: self.tip = int(np.argmin(np.linalg.norm(self.pts0 - apex, axis=1)))
        return self

    def densify(self, surf, step=0.0005, max_gap=0.0045):
        """fill small holes in the sample list (missed rays) by interpolating + snapping to the surface, so the distance field is a line and not a string of rings"""
        tri, bary, pts, wf = [], [], [], []
        P = self.pts0; n = len(P)
        for i in range(n):
            tri.append(self.tri[i]); bary.append(self.bary[i]); pts.append(P[i]); wf.append(self.wf[i])
            if i + 1 < n:
                g = np.linalg.norm(P[i + 1] - P[i])
                if step * 1.6 < g < max_gap:
                    for t in np.arange(step, g, step):
                        q = P[i] + (P[i + 1] - P[i]) * (t / g)
                        h = surf.snap(q, .01)
                        if h is None: continue
                        tri.append(h[0]); bary.append(h[1]); pts.append(h[2]); wf.append(self.wf[i] + (self.wf[i + 1] - self.wf[i]) * t / g)
        apex = self.pts0[self.tip] if self.tip is not None else None
        self.tri, self.bary, self.pts0, self.wf = np.array(tri, np.int64), np.array(bary), np.array(pts), np.array(wf)
        if apex is not None: self.tip = int(np.argmin(np.linalg.norm(self.pts0 - apex, axis=1)))
        return self

    def edge_dist(self, max_gap=0.0035):
        """per sample: arc distance (m) to the nearest END of its continuous fragment (fragments are split where the sample list jumps): used to run seams out gracefully"""
        P = self.pts0; n = len(P)
        seg = np.linalg.norm(np.diff(P, axis=0), axis=1)
        brk = np.concatenate([[True], seg > max_gap])
        fid = np.cumsum(brk) - 1
        arc = np.concatenate([[0], np.cumsum(np.where(seg > max_gap, 0, seg))])
        start = np.zeros(n); end = np.zeros(n)
        for f in range(fid.max() + 1):
            m = fid == f
            a0 = arc[m][0]; a1 = arc[m][-1]
            start[m] = arc[m] - a0; end[m] = a1 - arc[m]
        return np.minimum(start, end)

    def smooth_wf(self, win=18):
        """the strip-width factor changes where a finger seam turns into the side seam of the hand: blend it over ~2 cm so no ring appears at the join"""
        k = np.ones(2 * win + 1) / (2 * win + 1)
        w = np.concatenate([np.full(win, self.wf[0]), self.wf, np.full(win, self.wf[-1])])
        self.wf = np.convolve(w, k, mode='valid')
        return self

    def extend(self, other):
        self.tri = np.concatenate([self.tri, other.tri]); self.bary = np.concatenate([self.bary, other.bary])
        self.pts0 = np.concatenate([self.pts0, other.pts0]); self.wf = np.concatenate([self.wf, other.wf])
        return self

    def positions(self, Ofit, T):
        return np.einsum('kj,kjc->kc', self.bary, Ofit[T[self.tri]])


def resample(P, step, closed=False):
    P = np.asarray(P, float)
    if closed: P = np.vstack([P, P[:1]])
    seg = np.linalg.norm(np.diff(P, axis=0), axis=1); cum = np.concatenate([[0], np.cumsum(seg)])
    n = max(int(cum[-1] / step), 2)
    s = np.linspace(0, cum[-1], n + (0 if closed else 1), endpoint=not closed)
    return np.stack([np.interp(s, cum, P[:, k]) for k in range(3)], 1)


def smooth_polyline(P, closed=False, iters=2):
    P = np.asarray(P, float)
    for _ in range(iters):
        if closed: P = (np.roll(P, 1, 0) + 2 * P + np.roll(P, -1, 0)) / 4
        else: P = np.vstack([P[:1], (P[:-2] + 2 * P[1:-1] + P[2:]) / 4, P[-1:]])
    return P


def catmull(P, n=8, closed=False):
    """Catmull-Rom through the control points (k,3) -> dense polyline"""
    P = np.asarray(P, float)
    if closed: Q = np.vstack([P[-1:], P, P[:2]])
    else: Q = np.vstack([2 * P[0] - P[1], P, 2 * P[-1] - P[-2]])
    out = []
    for i in range(1, len(Q) - 2):
        p0, p1, p2, p3 = Q[i - 1], Q[i], Q[i + 1], Q[i + 2]
        for t in np.linspace(0, 1, n, endpoint=False):
            out.append(.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (-p0 + 3 * p1 - 3 * p2 + p3) * t ** 3))
    if not closed: out.append(P[-1])
    return np.array(out)


def surface_path(surf, ctrl, ray_dir, closed=False, step=0.0006, height=.12, n=10):
    """control points (x, y, [z unused]) in G -> a dense curve on the surface by casting rays along ray_dir from 'height' away (ray_dir (0,0,-1): from the dorsal side down)."""
    pts = catmull(np.asarray(ctrl, float), n=n, closed=closed)
    pts = resample(pts, step, closed)
    c = Curve('path', closed=closed)
    d = np.asarray(ray_dir, float)
    for p in pts:
        o = np.array([p[0], p[1], 0.0]) - d * height
        c.add(surf.ray(o, d))
    return c.finish()


def finger_equator(surf, chain, s_start, step=0.0006, lateral_sign=1, name='f', tip_over=True, s_start2=None):
    """side seam of a finger/thumb: up one side (lateral +e1*sign) from arc length s_start to the end of the last segment, over the tip cap and back down the
    other side to s_start2 (default s_start)."""
    L = chain.seglens().sum()
    s_start2 = s_start if s_start2 is None else s_start2
    c = Curve(name)
    ss = np.arange(s_start, L, step)
    for s in ss:
        p, u, e1, e2 = chain.frame_at(s); c.add(surf.ray(p, e1 * lateral_sign, .06))
    p, u, e1, e2 = chain.frame_at(L - 1e-6)
    rc = chain.radius_at(L)
    n_cap = max(int(math.pi * rc / step), 8)
    if tip_over:
        for ph in np.linspace(0, math.pi, n_cap):
            d = math.cos(ph) * e1 * lateral_sign + math.sin(ph) * u
            c.add(surf.ray(chain.P[-1], d, .06))
    for s in np.arange(s_start2, L, step)[::-1]:
        p, u, e1, e2 = chain.frame_at(s); c.add(surf.ray(p, -e1 * lateral_sign, .06))
    c.finish()
    apex = chain.P[-1] + u * rc
    c.tip = int(np.argmin(np.linalg.norm(c.pts0 - apex, axis=1)))
    return c


def side_line(surf, ys, centre_fn, sign, name, z_off=0.0, step=0.0006, wf=0.45):
    """seam along a side of the hand/forearm: rays along sign*x from the loft centre line"""
    c = Curve(name)
    for y in ys:
        cx, cz = centre_fn(y)
        c.add(surf.ray(np.array([cx, y, cz + z_off]), np.array([sign, 0, 0.0]), .15), wf)
    return c.finish()


def hex_grid(x0, x1, y0, y1, pitch):
    pts = []
    rows = int((y1 - y0) / (pitch * .866)) + 1
    for r in range(rows):
        y = y0 + r * pitch * .866
        off = (r % 2) * pitch * .5
        x = x0 + off
        while x <= x1:
            pts.append((x, y)); x += pitch
    return np.array(pts)
