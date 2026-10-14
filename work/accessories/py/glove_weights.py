"""GLOVE skin weights: copied from the GOLF hero body (53 real bones, no runtime ArmBlend), restricted to LowerArm, Hand and the 15 finger bones, renormalised, smoothed on
the glove's inner-surface graph, <= 4 bones per vertex."""
import os, sys
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from mathutils.kdtree import KDTree
import acc_common as A, glove_sdf as S, glove_hand as GH


def allowed_bones(side='Left'):
    return [f'{side}LowerArm', f'{side}Hand'] + [f'{side}{f}{s}' for f in GH.FINGERS for s in GH.SEGS]


def body_weights(hs, side='Left'):
    """dense (n_region, nb) weights of the region skin vertices over the allowed bones (renormalised), and their N positions"""
    names = allowed_bones(side)
    cols = [hs.widx[n] for n in names]
    W = hs.W[hs.ids][:, cols]
    W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)
    posN = hs.hd.body_pos[hs.ids]
    return names, W, posN


def copy_weights(hs, pts_N, k=4, side='Left'):
    names, W, posN = body_weights(hs, side)
    kd = KDTree(len(posN))
    for i, p in enumerate(posN): kd.insert(tuple(p), i)
    kd.balance()
    out = np.zeros((len(pts_N), len(names)))
    for i, p in enumerate(pts_N):
        res = kd.find_n(tuple(p), k)
        w = np.array([1.0 / (d + 0.0015) ** 2 for (_, _, d) in res]); w /= w.sum()
        for ww, (_, idx, _) in zip(w, res): out[i] += ww * W[idx]
    return names, out


def copy_weights_surface(hs, pts_N, side='Left'):
    """weights at the closest POINT of the skin surface (barycentric blend of the triangle's vertex weights): the glove vertex then follows the skin point right under it"""
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    names, W, posN = body_weights(hs, side)
    tri = hs.T if side == 'Left' else hs.T[:, ::-1]
    tree = BVHTree.FromPolygons([tuple(p) for p in posN], [tuple(t) for t in tri], all_triangles=True)
    out = np.zeros((len(pts_N), len(names)))
    for i, p in enumerate(pts_N):
        r = tree.find_nearest(Vector(p), .05)
        if r[0] is None:
            out[i] = 0; continue
        t = tri[r[2]]; a, b, c = posN[t]
        v0, v1, v2 = b - a, c - a, np.array(r[0]) - a
        d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-20
        v = (d11 * d20 - d01 * d21) / den; w = (d00 * d21 - d01 * d20) / den
        u = 1 - v - w
        bw = np.clip(np.array([u, v, w]), 0, 1); bw /= bw.sum()
        out[i] = bw[0] * W[t[0]] + bw[1] * W[t[1]] + bw[2] * W[t[2]]
    missing = out.sum(1) < .5
    if missing.any():
        _, alt = copy_weights(hs, pts_N[missing], k=3, side=side)
        out[missing] = alt
    return names, out


def smooth_weights(Wd, T, iters=2, lam=.35):
    E = S.edges_of(T); n = len(Wd); deg = np.bincount(E.ravel(), minlength=n).astype(float)
    for _ in range(iters):
        acc = np.zeros_like(Wd)
        np.add.at(acc, E[:, 0], Wd[E[:, 1]]); np.add.at(acc, E[:, 1], Wd[E[:, 0]])
        Wd = (1 - lam) * Wd + lam * acc / np.maximum(deg, 1)[:, None]
    return Wd


def top4(Wd):
    n, nb = Wd.shape
    idx = np.argsort(-Wd, axis=1)[:, :4]
    w = np.take_along_axis(Wd, idx, 1)
    w = w / np.maximum(w.sum(1, keepdims=True), 1e-9)
    return idx.astype(np.int32), w.astype(np.float32)
