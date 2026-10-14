"""ACCESSORIES fit helpers: head / body BVH with signed clearance, Nelder-Mead, socket maths (Unity world), bind poses.
Runs inside Blender (mathutils BVH)."""
import os, sys, math, json
import numpy as np
sys.path.insert(0, os.path.dirname(__file__))
import acc_common as A


class Clearance:
    """signed distance to a closed-ish triangle mesh (N frame): + outside, - inside (by the face normal at the nearest point; outward normals via recalc)."""
    def __init__(self, pos, tri):
        import bmesh
        from mathutils.bvhtree import BVHTree
        bm = bmesh.new()
        vs = [bm.verts.new(tuple(p)) for p in pos]
        for t in tri:
            try: bm.faces.new((vs[t[0]], vs[t[1]], vs[t[2]]))
            except ValueError: pass
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self.tree = BVHTree.FromBMesh(bm)
        bm.free()

    def __call__(self, pts, with_dir=False):
        """signed distances (+ outside); with_dir also returns the unit vector pointing from the nearest surface point OUT of the head (toward the point if outside)."""
        from mathutils import Vector
        out = np.zeros(len(pts)); dirs = np.zeros((len(pts), 3)); fn = self.tree.find_nearest
        for i, p in enumerate(pts):
            loc, nrm, idx, d = fn(Vector(p))
            if loc is None: out[i] = 1.0; dirs[i] = (0, 0, 1); continue
            v = np.array(p) - np.array(loc)
            sgn = 1.0 if np.dot(nrm, v) >= 0 else -1.0
            out[i] = sgn * d
            n = np.linalg.norm(v)
            dirs[i] = (sgn * v / n) if n > 1e-9 else np.array(nrm)
        return (out, dirs) if with_dir else out


def region(hd, zmin, zmax=9.0, xlim=9.0):
    """body triangles whose vertices all lie in the box (compact re-indexing)."""
    P, T = hd.body_pos, hd.body_tri
    ok = (P[:, 2] >= zmin) & (P[:, 2] <= zmax) & (np.abs(P[:, 0]) <= xlim)
    keep = ok[T].all(1)
    t = T[keep]
    used = np.unique(t); remap = -np.ones(len(P), np.int64); remap[used] = np.arange(len(used))
    return P[used], remap[t].astype(np.int32)


def nelder_mead(f, x0, step, iters=200, tol=1e-9):
    n = len(x0); x0 = np.asarray(x0, float); step = np.asarray(step, float)
    simplex = [x0] + [x0 + np.eye(n)[i] * step[i] for i in range(n)]
    fv = [f(x) for x in simplex]
    for it in range(iters):
        order = np.argsort(fv); simplex = [simplex[i] for i in order]; fv = [fv[i] for i in order]
        if abs(fv[-1] - fv[0]) < tol: break
        cen = np.mean(simplex[:-1], 0)
        xr = cen + (cen - simplex[-1]); fr = f(xr)
        if fr < fv[0]:
            xe = cen + 2 * (cen - simplex[-1]); fe = f(xe)
            if fe < fr: simplex[-1], fv[-1] = xe, fe
            else: simplex[-1], fv[-1] = xr, fr
        elif fr < fv[-2]:
            simplex[-1], fv[-1] = xr, fr
        else:
            xc = cen + .5 * (simplex[-1] - cen); fc = f(xc)
            if fc < fv[-1]: simplex[-1], fv[-1] = xc, fc
            else:
                simplex = [simplex[0]] + [simplex[0] + .5 * (s - simplex[0]) for s in simplex[1:]]
                fv = [fv[0]] + [f(s) for s in simplex[1:]]
    i = int(np.argmin(fv)); return simplex[i], fv[i]


# ---------------------------------------------------------------- sockets (Unity world)
def Rx(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def Ry(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def Rz(deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def rt4(R, t):
    m = np.eye(4); m[:3, :3] = R; m[:3, 3] = t; return m


def product_to_socket(p):
    """product frame (N: hero's left +X, front -Y, up +Z) -> socket-local axes (Unity-like: x right, y up, z forward)."""
    p = np.asarray(p, np.float64); return np.stack([-p[..., 0], p[..., 2], -p[..., 1]], -1)


def socket_to_world_N(W_unity, q):
    """socket-local points -> N world."""
    q = np.asarray(q, np.float64)
    pu = q @ W_unity[:3, :3].T + W_unity[:3, 3]
    return A.unity_to_N(pu)


def socket_dir_to_world_N(W_unity, q):
    return A.unity_to_N(np.asarray(q, np.float64) @ W_unity[:3, :3].T)


def bindpose_for(hd, W_unity):
    """mesh -> socket local, in the layout the installer reads (like hero.body.bind[i])."""
    return np.linalg.inv(W_unity) @ hd.r2w


def local_pose_under(hd, parent_bone, W_unity):
    """(position, quaternion xyzw) of a socket whose world matrix is W_unity, as a child of `parent_bone` at rest (Unity local axes)."""
    Wp = hd.rest_L2W_unity[hd.idx(parent_bone)]
    L = np.linalg.inv(Wp) @ W_unity
    R = L[:3, :3]
    sc = np.linalg.norm(R, axis=0)
    R = R / sc
    # quaternion from rotation matrix
    tr = np.trace(R)
    if tr > 0:
        s = math.sqrt(tr + 1) * 2; q = [(R[2, 1] - R[1, 2]) / s, (R[0, 2] - R[2, 0]) / s, (R[1, 0] - R[0, 1]) / s, .25 * s]
    elif R[0, 0] > R[1, 1] and R[0, 0] > R[2, 2]:
        s = math.sqrt(1 + R[0, 0] - R[1, 1] - R[2, 2]) * 2; q = [.25 * s, (R[0, 1] + R[1, 0]) / s, (R[0, 2] + R[2, 0]) / s, (R[2, 1] - R[1, 2]) / s]
    elif R[1, 1] > R[2, 2]:
        s = math.sqrt(1 + R[1, 1] - R[0, 0] - R[2, 2]) * 2; q = [(R[0, 1] + R[1, 0]) / s, .25 * s, (R[1, 2] + R[2, 1]) / s, (R[0, 2] - R[2, 0]) / s]
    else:
        s = math.sqrt(1 + R[2, 2] - R[0, 0] - R[1, 1]) * 2; q = [(R[0, 2] + R[2, 0]) / s, (R[1, 2] + R[2, 1]) / s, .25 * s, (R[1, 0] - R[0, 1]) / s]
    return [float(v) for v in L[:3, 3]], [float(v) for v in q], [float(v) for v in sc]


def recompute_normals(pos, tri, src):
    """area-weighted vertex normals, accumulated over the vertices that came from the same source vertex (UV-split duplicates)."""
    fn = np.cross(pos[tri[:, 1]] - pos[tri[:, 0]], pos[tri[:, 2]] - pos[tri[:, 0]])
    acc = np.zeros((src.max() + 1, 3))
    for k in range(3): np.add.at(acc, src[tri[:, k]], fn)
    n = acc[src]
    return n / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-15)


def gaussian_transfer(src_pts, src_vals, dst_pts, sigma, chunk=2000):
    """Gaussian-weighted average of src_vals at dst_pts (normalised)."""
    out = np.zeros((len(dst_pts), src_vals.shape[1])); inv = 1.0 / (2 * sigma * sigma)
    for a in range(0, len(dst_pts), chunk):
        d = dst_pts[a:a + chunk, None, :] - src_pts[None, :, :]
        w = np.exp(-(d * d).sum(-1) * inv)
        s = w.sum(1, keepdims=True)
        out[a:a + chunk] = (w @ src_vals) / np.maximum(s, 1e-12)
        far = (s[:, 0] < 1e-9)
        if far.any():                                   # nothing within reach: nearest source
            j = np.argmin((d[far] ** 2).sum(-1), 1); out[a:a + chunk][far] = src_vals[j]
    return out
