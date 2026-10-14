"""GLOVE map bake: a copy of acc_canvas.bake with two additions: detail functions may set cv.aox (extra per-texel AO multiplier: perforations go black) and the cavity floor is .25.
The encoding is unchanged (N = tangent normal, M = R AO, G cavity, B trim mix, A fabric id / 9)."""
import os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import acc_canvas as CV
from acc_canvas import Canvas, lib, fp, bp, write_png


def bake(npz_path, out_dir, tag, detail_fn, ao=None, cav_floor=.25):
    g = np.load(npz_path)
    res = int(g['res'])
    cv = Canvas(g, res)
    cv.aox = np.ones(cv.n)
    trim = detail_fn(cv)
    if trim is None: trim = np.zeros(cv.n)
    aoT = cv.I(ao) if ao is not None else np.ones(cv.n)
    aoT = aoT * cv.aox
    cav = np.clip(cv.C, cav_floor, 1)
    fab = cv.fabric.astype(np.float64)
    Himg = np.zeros(res * res); Himg[cv.valid] = cv.H
    mask = cv.valid.astype(np.uint8).copy()
    Hf = Himg.astype(np.float32).reshape(res, res).copy()
    lib.dilate(res, res, 1, Hf.ctypes.data_as(fp), mask.reshape(-1).ctypes.data_as(bp), 6)
    Hd = Hf.astype(np.float64)
    dx = (np.roll(Hd, -1, 1) - np.roll(Hd, 1, 1)) / (2 * cv.texel)
    dy = (np.roll(Hd, -1, 0) - np.roll(Hd, 1, 0)) / (2 * cv.texel)
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
    rep = dict(res=res, coverage=round(cv.n / (res * res), 3), metres_per_uv=float(g['S']), px_per_mm=round(res / float(g['S']) / 1000, 2),
               ao_min=round(float(aoT.min()), 3), ao_mean=round(float(aoT.mean()), 3))
    print(tag, rep)
    return rep
