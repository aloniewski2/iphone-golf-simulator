"""GLOVE weight fit: per glove-wall vertex, choose bone weights (<= the bones around it, on the simplex) that make the vertex follow 'the skin point under it + its rest offset along the posed skin normal'
through every frame of every golf clip (least squares, ridge toward the plain copy of the skin weights). Blender -b --python glove_wfit.py -- <Male|Female> [step]
Result: products/glove_hero_<sex>.npz gets Wfit (n_in x 17)."""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
import acc_common as A, glove_fit as FT, glove_sdf as S, glove_mesh as M, glove_shell as SH, glove_weights as GW, glove_posecore as PCR


def proj_simplex(V, mask):
    """euclidean projection of each row of V onto the probability simplex restricted to mask (others 0)"""
    n, k = V.shape
    U = np.where(mask, V, -1e9)
    Su = -np.sort(-U, axis=1)
    css = np.cumsum(Su, axis=1) - 1
    idx = np.arange(1, k + 1)[None, :]
    cond = (Su - css / idx) > 0
    rho = cond.sum(1)
    theta = css[np.arange(n), rho - 1] / rho
    W = np.maximum(U - theta[:, None], 0)
    return W * mask


def fit(sex, step=2, ridge=0.03, iters=400, verbose=True, cand_radius=0.009, side='Left'):
    t0 = time.time()
    hs = FT.HeroSkin(sex, side); h = A.cv2io.Hero('GolfKitHero_' + sex); H = hs.H
    path = os.path.join(A.ROOT, 'products', f'glove_hero_{sex if side == "Left" else sex + "_R"}.npz')
    d = dict(np.load(path)); T = d['T']; loop = d['loop']
    Vh = d['Vh'] if 'Vh0' not in d else d['Vh0']
    names, W0 = GW.copy_weights_surface(hs, H.from_G(Vh), side=side)
    nb = len(names)
    bidx = [h.body.bones.index(n) for n in names]
    dcuff = M.graph_distance(Vh, T, loop); thick = SH.thickness_field(dcuff)
    nrm = SH.vertex_normals_smooth(Vh, T); O = Vh + nrm * thick[:, None]
    Om = A.N_to_mesh(H.from_G(O))                                # wall vertices, body mesh space
    rt = h.body.tri[np.isin(h.body.tri, hs.ids).all(1)]
    rest_tree = BVHTree.FromPolygons([tuple(p) for p in h.body.pos], [tuple(t) for t in rt], all_triangles=True)
    n = len(Om)
    ti = np.zeros(n, np.int64); bc = np.zeros((n, 3)); dist = np.zeros(n)
    for i in range(n):
        r = rest_tree.find_nearest(Vector(Om[i]), .08)
        ti[i] = r[2]; a, b, c = h.body.pos[rt[r[2]]]
        v0, v1, v2 = b - a, c - a, np.array(r[0]) - a
        d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
        den = d00 * d11 - d01 * d01 or 1e-20
        vv = (d11 * d20 - d01 * d21) / den; ww = (d00 * d21 - d01 * d20) / den
        bc[i] = (1 - vv - ww, vv, ww)
        nrest = np.cross(b - a, c - a); nrest /= np.linalg.norm(nrest)
        dist[i] = float(np.dot(Om[i] - np.array(r[0]), nrest))      # signed rest offset of the wall vertex along the skin normal
    tv = rt[ti]
    # candidate bones: those carrying weight on the skin triangle (plus the plain copy's bones)
    Wb = np.zeros((len(h.body.pos), len(h.body.bones)))
    for k in range(4): np.add.at(Wb, (np.arange(len(Wb)), h.body.bi[:, k]), h.body.bw[:, k])
    colmap = -np.ones(len(h.body.bones), np.int64); colmap[bidx] = np.arange(nb)
    Wr = Wb[:, bidx]
    from mathutils.kdtree import KDTree
    kd = KDTree(len(h.body.pos[hs.ids]))
    for i, v in enumerate(hs.ids): kd.insert(tuple(h.body.pos[v]), i)
    kd.balance()
    cand = (Wr[tv[:, 0]] + Wr[tv[:, 1]] + Wr[tv[:, 2]] > 1e-3) | (W0 > 1e-3)
    Wreg = Wr[hs.ids]
    for i in range(n):
        near = [r[1] for r in kd.find_range(tuple(Om[i]), cand_radius)]
        if near: cand[i] |= (Wreg[near].max(0) > 0.02)
    frames = []
    for c in h.clips:
        bodyf, bones = h.clip(c['name'])
        for fi in range(0, len(bodyf), step): frames.append((bodyf[fi].astype(np.float64), bones[fi].astype(np.float64)))
    F = len(frames)
    if verbose: print(f'[{sex}] weight fit: {n} wall vertices, {F} frames, {nb} bones, candidates/vertex mean {cand.sum(1).mean():.1f}', flush=True)
    Gm = np.zeros((n, nb, nb)); Cv = np.zeros((n, nb))
    for (bodyf, bones) in frames:
        skin = h.skin_matrices(bones)[bidx]                      # (nb,4,4) mesh -> world
        pa, pb, pc_ = bodyf[tv[:, 0]], bodyf[tv[:, 1]], bodyf[tv[:, 2]]
        Ps = bc[:, 0:1] * pa + bc[:, 1:2] * pb + bc[:, 2:3] * pc_
        Ns = np.cross(pb - pa, pc_ - pa); Ns /= np.linalg.norm(Ns, axis=1, keepdims=True) + 1e-15
        target = Ps + Ns * dist[:, None]                           # (n,3)
        # candidate positions per bone: (n, nb, 3)
        Pb = np.einsum('bjk,nk->nbj', skin[:, :3, :3], Om) + skin[None, :, :3, 3]
        # normal equations: A_f (3 x nb) = Pb[n].T ; accumulate A^T A and A^T b
        Gm += np.einsum('nbj,ncj->nbc', Pb, Pb)
        Cv += np.einsum('nbj,nj->nb', Pb, target)
    # ridge toward the plain copy W0; solve on the simplex by projected gradient (batched)
    tr = np.trace(Gm, axis1=1, axis2=2) / nb
    lam = (ridge * tr)[:, None]
    Lip = np.linalg.eigvalsh(Gm).max(axis=1)[:, None] + lam
    w = W0 * cand; w = w / np.maximum(w.sum(1, keepdims=True), 1e-9)
    for it in range(iters):
        grad = np.einsum('nbc,nc->nb', Gm, w) - Cv + lam * (w - W0)
        w = proj_simplex(w - grad / Lip, cand)
    # top-4, renormalise
    idx = np.argsort(-w, axis=1)[:, 4:]
    w4 = w.copy(); np.put_along_axis(w4, idx, 0, axis=1); w4 /= np.maximum(w4.sum(1, keepdims=True), 1e-9)
    if verbose:
        err0 = np.einsum('nb,nbc,nc->n', W0, Gm, W0) - 2 * np.einsum('nb,nb->n', W0, Cv)
        err1 = np.einsum('nb,nbc,nc->n', w4, Gm, w4) - 2 * np.einsum('nb,nb->n', w4, Cv)
        print(f'[{sex}] quadratic residual (relative to the plain copy): mean change {np.mean(err1 - err0) / F * 1e6:.3f} (mm^2 per frame-vertex units), vertices improved {(err1 < err0).sum()}/{n}  t={time.time()-t0:.0f}s', flush=True)
    d['Wfit'] = w4; d['Wnames'] = np.array(names)
    np.savez(path, **d)
    return w4


if __name__ == '__main__':
    a = A.args_after_dashes()
    fit(a[0], step=int(a[1]) if len(a) > 1 and a[1].isdigit() else 2, side=a[-1] if a[-1] in ('Left', 'Right') else 'Left')
