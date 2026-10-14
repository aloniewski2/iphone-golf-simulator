"""GLOVE product, step 1: the inner (hand-shaped) surface mesh S_in of the glove, from the implicit hand: surface nets -> adaptive decimation ->
relax + project on the zero set -> exact cuff cut -> constrained relax. Cached in products/glove_inner_v1.npz (topology is what every fit re-uses).
Blender -b --python glove_build.py -- [ratio]"""
import os, sys, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import acc_common as A
import glove_sdf as S, glove_mesh as M

CUFF_Y = -0.036            # the cuff edge plane (G frame), 3.6 cm above the wrist bone
CACHE = os.path.join(A.ROOT, 'products', 'glove_inner_v1.npz')


def relax_bounded(V, T, sdf, bloops, y_plane, iters=14, lam=.5):
    n = len(V)
    bmask = np.zeros(n, bool)
    for l in bloops: bmask[l] = True
    E = S.edges_of(T); deg = np.bincount(E.ravel(), minlength=n).astype(float)
    # boundary neighbours (for 1D smoothing along the loop)
    bn = {}
    for l in bloops:
        k = len(l)
        for i, v in enumerate(l): bn[int(v)] = (int(l[(i - 1) % k]), int(l[(i + 1) % k]))
    for it in range(iters):
        acc = np.zeros_like(V)
        np.add.at(acc, E[:, 0], V[E[:, 1]]); np.add.at(acc, E[:, 1], V[E[:, 0]])
        avg = acc / np.maximum(deg, 1)[:, None]
        g = S.unit(sdf.grad(V))
        d = avg - V
        d -= g * (d * g).sum(1, keepdims=True)
        step = lam * d
        step[bmask] = 0
        # loop smoothing for boundary vertices (in-plane)
        for v, (a, b) in bn.items():
            t = .5 * (V[a] + V[b]) - V[v]; t[1] = 0
            step[v] = .5 * t
        V = V + step
        for _ in range(3):
            g = sdf.grad(V)
            g2 = g.copy(); g2[bmask, 1] = 0
            gn = np.linalg.norm(g2, axis=1, keepdims=True)
            V = V - sdf(V)[:, None] * g2 / np.maximum(gn, 1e-9) ** 2
        V[bmask, 1] = y_plane
    return V


def make_inner(h=0.002, target_tris=5200, verbose=True, sdf=None, bounds=None):
    t0 = time.time()
    if sdf is None: sdf, parts = S.make_product()
    lo, hi = bounds if bounds is not None else ((-.115, -.085, -.065), (.125, .215, .065))
    f, x0 = S.grid_eval(sdf, lo, hi, h)
    V, Q = S.surface_nets(f, x0, h)
    T = S.quads_to_tris(V, Q)
    # the far end of the forearm tube is cut off anyway: drop it before decimating so no budget is wasted there
    V, T, y0 = M.bisect_plane(V, T, (0, CUFF_Y - 0.006, 0), (0, 1, 0), keep_positive=True)[0:2] + (None,)
    if verbose: print('surface nets (cut)', len(V), len(T), 'time', round(time.time() - t0, 1))
    # decimate for the full mesh to ~ target (+ cuff strip trimmed after)
    ratio = target_tris * 1.07 / len(T)
    V, T = M.decimate(V, T, ratio)
    if verbose: print('decimated', len(V), len(T))
    V = S.relax_project(V, T, sdf, iters=10, lam=.5)
    V, T, onp = M.bisect_plane(V, T, (0, CUFF_Y, 0), (0, 1, 0), keep_positive=True)
    loops = M.boundary_loops(T)
    if verbose: print('cut', len(V), len(T), 'loops', [len(l) for l in loops])
    V = relax_bounded(V, T, sdf, loops, CUFF_Y, iters=14)
    return V, T, loops, sdf


if __name__ == '__main__':
    a = A.args_after_dashes()
    tt = int(a[0]) if a else 8200
    V, T, loops, sdf = make_inner(target_tris=tt)
    ar = M.face_areas(V, T)
    print('tris', len(T), 'verts', len(V), 'area cm2', ar.sum() * 1e4, 'edge lens mm (mean)', np.mean(np.linalg.norm(V[S.edges_of(T)[:, 0]] - V[S.edges_of(T)[:, 1]], axis=1)) * 1e3)
    os.makedirs(os.path.dirname(CACHE), exist_ok=True)
    np.savez(CACHE, V=V, T=T, loop=loops[0])
    # wire preview
    import bpy, glove_view as GV
    A.reset()
    ob = A.mesh_object('inner', V, T)
    ob.data.materials.append(A.material('M', A.srgb('E8E6E0'), .6))
    mod = ob.modifiers.new('w', 'WIREFRAME'); mod.thickness = .0003; mod.use_replace = False
    GV.setup((900, 900))
    out = os.path.join(A.ROOT, 'proof/glove/scratch')
    for nm in ('back', 'palm', 'side', 'q34'):
        d, up = GV.GVIEWS[nm]
        GV.shoot(os.path.join(out, f'inner_{nm}.png'), (0, .06, 0), d, dist=.62, lens=70, up=up)
