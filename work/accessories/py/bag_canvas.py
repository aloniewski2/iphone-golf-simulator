"""BAG copy of acc_canvas.py with a per-texel metres-per-texel (islands may have different scales; metresPerUv stays the average).
ACCESSORIES map baker: N (tangent-space normal, +Y up) and M (R AO, G cavity, B trim mix, A fabric id/9) maps for TennisCloth, from the piece's own
construction fields (seams, stitch rows, vents, patches). Same encoding as work/clothing-v2/py/maps.py (kept compatible; copied so this folder is self-contained).
Run inside Blender (mathutils for AO): Blender -b --python <script using bake()>"""
import os, sys, math, ctypes
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import acc_common as A
sys.path.insert(0, os.path.join(A.CV2, 'py'))
from PIL_free import write_png                                      # noqa: E402

lib = ctypes.CDLL(os.path.join(A.CV2, 'c', 'libraster.dylib'))
fp = ctypes.POINTER(ctypes.c_float); ip = ctypes.POINTER(ctypes.c_int); bp = ctypes.POINTER(ctypes.c_ubyte)
lib.raster.argtypes = [ctypes.c_int, fp, ctypes.c_int, ctypes.c_int, ip, fp]
lib.dilate.argtypes = [ctypes.c_int, ctypes.c_int, ctypes.c_int, fp, bp, ctypes.c_int]


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def gauss(d, w): return np.exp(-(d / w) ** 2)


def band(d, lo, hi, aa):
    return smoothstep(lo - aa, lo + aa, d) * (1 - smoothstep(hi - aa, hi + aa, d))


class Canvas:
    """per-texel fields of one piece (every triangle is baked: outer, rim and inner zones)."""
    def __init__(self, g, res):
        self.g = g; self.res = res
        tri = g['tri']
        sel = np.arange(len(tri))
        uv = g['uv'][tri[sel]].astype(np.float32).reshape(-1, 6)
        tid = np.full(res * res, -1, np.int32); bary = np.zeros(res * res * 3, np.float32)
        lib.raster(len(sel), uv.ctypes.data_as(fp), res, res, tid.ctypes.data_as(ip), bary.ctypes.data_as(fp))
        self.valid = tid >= 0
        self.face = sel[tid[self.valid]]
        self.V = tri[self.face].astype(np.int32); self.B = bary.reshape(-1, 3)[self.valid].astype(np.float32)
        self.n = int(self.valid.sum())
        self.H = np.zeros(self.n); self.C = np.ones(self.n)
        S = float(g['S'])
        idx = np.nonzero(self.valid)[0]
        self.um = ((idx % res) + .5) / res * S; self.vm = ((idx // res) + .5) / res * S
        # metres per texel of the triangle each texel belongs to (islands carry different scales)
        P3 = g['pos'][tri]; UV = g['uv'][tri].astype(np.float64)
        a3 = np.linalg.norm(np.cross(P3[:, 1] - P3[:, 0], P3[:, 2] - P3[:, 0]), axis=1) * .5
        e1 = UV[:, 1] - UV[:, 0]; e2 = UV[:, 2] - UV[:, 0]
        a2 = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]) * .5
        tf = np.sqrt(np.where(a2 > 1e-14, a3 / np.maximum(a2, 1e-14), S * S)) / res
        self.texel = tf[self.face]
        self.kind = g['kind'][self.face]; self.zone = g['zone'][self.face]; self.fabric = g['fabric'][self.face]; self.sub = g['sub'][self.face]
        self.pos = self.I(g['pos']).astype(np.float32)

    def I(self, a):
        a = np.asarray(a)
        if a.ndim == 1: return (a[self.V] * self.B).sum(1)
        return (a[self.V] * self.B[:, :, None]).sum(1)

    def A(self, name, default=None):
        k = 'a_' + name
        if k not in self.g.files: return None if default is None else np.full(self.n, default)
        return self.I(self.g[k])

    # ---- construction marks (heights in metres, d = distance field in metres)
    def groove(self, d, at, mask, width=.0007, depth=.00045, dark=.28):
        w = gauss(d - at, width) * mask
        self.H -= depth * w; self.C *= 1 - dark * w

    def stitch(self, d, at, along, mask, pitch=.0035, width=.0005, height=.00022):
        """returns the thread mask (line * dash), so a caller can colour the thread."""
        line = gauss(d - at, width) * mask
        ph = (along / pitch) % 1.0
        dash = smoothstep(.0, .12, ph) * (1 - smoothstep(.62, .74, ph))
        self.H += height * line * dash
        self.C *= 1 - .22 * line * dash - .10 * line * (1 - dash)
        return line * dash

    def ridge(self, d, at, mask, width=.0012, height=.0006):
        w = gauss(d - at, width) * mask
        self.H += height * w
        self.C *= 1 - .05 * gauss(d - at, width * .6) * mask

    def dimple(self, m, depth=.0006, dark=.85):
        self.H -= depth * m; self.C *= 1 - dark * m

    def folds(self, coord, wavelength, amp, mask, phase_seed=0):
        rng = np.random.default_rng(phase_seed)
        ph = rng.uniform(0, 2 * math.pi, 4)
        c = coord / wavelength * 2 * math.pi
        f = np.sin(c + ph[0]) * .6 + np.sin(c * 1.73 + ph[1]) * .3 + np.sin(c * .61 + ph[2]) * .25
        self.H += amp * f * mask
        self.C *= 1 - .06 * np.clip(-f, 0, None) * mask


def vertex_ao(pos, nrm, tris, rays=14, dist=.04, extra=None, seed=1):
    """per-vertex AO of a piece against itself and optional `extra` (pos, tri) occluders (the hero's head/body)."""
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    P = [pos]; T = [tris]; off = len(pos)
    if extra is not None:
        P.append(extra[0]); T.append(extra[1] + off)
    P = np.concatenate(P); T = np.concatenate(T)
    tree = BVHTree.FromPolygons([tuple(x) for x in P], [tuple(t) for t in T], all_triangles=True)
    rng = np.random.default_rng(seed)
    u1 = rng.random(rays); u2 = rng.random(rays)
    dirs = np.stack([np.sqrt(u1) * np.cos(2 * math.pi * u2), np.sqrt(u1) * np.sin(2 * math.pi * u2), np.sqrt(1 - u1)], 1)
    ao = np.ones(len(pos))
    for i in range(len(pos)):
        n = nrm[i]
        a = np.cross(n, [0, 0, 1.] if abs(n[2]) < .9 else [1, 0, 0.]); a /= np.linalg.norm(a) + 1e-12; b = np.cross(n, a)
        o = pos[i] + n * .0007
        hit = 0.0
        for d in dirs:
            w = a * d[0] + b * d[1] + n * d[2]
            r = tree.ray_cast(Vector(o), Vector(w), dist)
            if r[0] is not None: hit += 1 - (r[3] / dist) ** .5
        ao[i] = 1 - hit / rays
    return ao


def bake(npz_path, out_dir, tag, detail_fn, ao=None, report=None):
    g = np.load(npz_path)
    res = int(g['res'])
    cv = Canvas(g, res)
    trim = detail_fn(cv)
    if trim is None: trim = np.zeros(cv.n)
    aoT = cv.I(ao) if ao is not None else np.ones(cv.n)
    cav = np.clip(cv.C, .45, 1)
    fab = cv.fabric.astype(np.float64)
    Himg = np.zeros(res * res); Himg[cv.valid] = cv.H
    mask = cv.valid.astype(np.uint8).copy()
    Hf = Himg.astype(np.float32).reshape(res, res).copy()
    lib.dilate(res, res, 1, Hf.ctypes.data_as(fp), mask.reshape(-1).ctypes.data_as(bp), 6)
    Hd = Hf.astype(np.float64)
    Tm = np.full(res * res, float(np.mean(cv.texel)), np.float32); Tm[cv.valid] = cv.texel
    t8 = cv.valid.astype(np.uint8).copy()
    lib.dilate(res, res, 1, Tm.ctypes.data_as(fp), t8.reshape(-1).ctypes.data_as(bp), 6)
    Tm = Tm.reshape(res, res).astype(np.float64)
    dx = (np.roll(Hd, -1, 1) - np.roll(Hd, 1, 1)) / (2 * Tm)
    dy = (np.roll(Hd, -1, 0) - np.roll(Hd, 1, 0)) / (2 * Tm)
    nz = 1.0 / np.sqrt(1 + dx * dx + dy * dy)
    N = np.stack([-dx * nz, -dy * nz, nz], -1).reshape(-1, 3)
    M = np.zeros((res * res, 4)); M[:, :] = (1, 1, 0, 0)
    M[cv.valid, 0] = np.clip(aoT, 0, 1); M[cv.valid, 1] = cav; M[cv.valid, 2] = np.clip(trim, 0, 1); M[cv.valid, 3] = fab / 9.0
    m2 = cv.valid.astype(np.uint8).copy(); Mf = M.astype(np.float32).copy()
    lib.dilate(res, res, 4, Mf.ctypes.data_as(fp), m2.ctypes.data_as(bp), 24)
    m3 = cv.valid.astype(np.uint8).copy(); Nf = N.astype(np.float32).copy()
    lib.dilate(res, res, 3, Nf.ctypes.data_as(fp), m3.ctypes.data_as(bp), 24)
    Nf[m3 == 0] = (0, 0, 1)
    Mf[m2 == 0] = (1, 1, 0, Mf[cv.valid, 3].mean() if cv.n else 0)
    n8 = np.clip((Nf.reshape(res, res, 3)[::-1] * .5 + .5) * 255 + .5, 0, 255).astype(np.uint8)
    m8 = np.clip(Mf.reshape(res, res, 4)[::-1] * 255 + .5, 0, 255).astype(np.uint8)
    os.makedirs(out_dir, exist_ok=True)
    write_png(os.path.join(out_dir, f'{tag}_N.png'), n8)
    write_png(os.path.join(out_dir, f'{tag}_M.png'), m8)
    rep = dict(res=res, coverage=round(cv.n / (res * res), 3), metres_per_uv=float(g['S']), px_per_mm=round(res / float(g['S']) / 1000, 2), px_per_mm_min=round(1 / float(cv.texel.max()) / 1000, 2), px_per_mm_max=round(1 / float(cv.texel.min()) / 1000, 2),
               ao_min=round(float(aoT.min()), 3), ao_mean=round(float(aoT.mean()), 3))
    print(tag, rep)
    return rep
