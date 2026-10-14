"""GLOVE: build one hero's glove inner surface: measured recipe (glove_params) -> SDF -> mesh -> bend about the wrist -> clearance hug, repeated with extra 'cover spheres'
where the hero's skin still sticks out (e.g. the male thumb, which the rig describes badly). Returns / caches the straight (unbent) and the fitted inner surface.
Blender -b --python glove_hero.py -- <Male|Female>"""
import os, sys, json, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import acc_common as A, glove_sdf as S, glove_mesh as M, glove_build as GB, glove_fit as FT, glove_params as GP, glove_shell as SH

R_COVER = 0.008


def bounds_for(P, spheres):
    pts = [np.array(f['pts']) * 1e-3 for f in P['fingers'].values()] + [np.array(P['thumb']['pts']) * 1e-3]
    if len(spheres): pts.append(spheres[:, :3])
    allp = np.vstack(pts)
    lo = (-.14, -.085, min(allp[:, 2].min() - .04, -.075)); hi = (.14, allp[:, 1].max() + .04, max(allp[:, 2].max() + .045, .07))
    return lo, hi


def build_hero_inner(sex, side='Left', target_tris=8200, iters=3, verbose=True, hs=None):
    t0 = time.time()
    hs = hs or FT.HeroSkin(sex, side)
    P, info = GP.hero_params(hs, verbose=False)
    bend = GP.bend_fn(info['Rf'])
    spheres = np.zeros((0, 4))
    hist = []
    for it in range(iters + 1):
        sdf, sk = S.make_product(P); sdf.spheres = spheres
        V, T, loops, _ = GB.make_inner(target_tris=target_tris, sdf=sdf, bounds=bounds_for(P, spheres), verbose=False)
        loop = loops[0]
        Vb = bend(V)
        dc = M.graph_distance(V, T, loop)
        tgt = FT.ease_field(dc)
        Vh, st = FT.hug(Vb, T, hs, tgt, iters=14, loop=loop)
        nrm = S.vertex_normals(Vh, T); O = Vh + nrm * SH.T_BASE
        bad, ncand = FT.uncovered(hs, O, T)
        hist.append((len(bad), ncand, len(spheres)))
        if verbose: print(f'[{sex}] fit pass {it}: tris {len(T)} spheres {len(spheres)} uncovered skin verts {len(bad)}/{ncand}  hug final {[round(x*1000,2) for x in st[-1][:4]]}  t={time.time()-t0:.0f}s')
        if len(bad) <= 4 or it == iters: break
        # new cover spheres on the uncovered skin (hand part only: the forearm is straight-framed, and the cuff region should not need any)
        nr = S.vertex_normals(hs.P, hs.T)
        keep = hs.P[bad][:, 1] > -0.012
        pts = hs.P[bad][keep]; nn = nr[bad][keep]
        if len(pts) == 0: break
        key = np.floor(pts / 0.007).astype(int)
        uniq, inv = np.unique(key, axis=0, return_inverse=True)
        inv = inv.ravel()
        new = []
        for k in range(len(uniq)):
            m = inv == k
            p = pts[m].mean(0); n = S.unit(nn[m].mean(0))
            new.append(np.concatenate([p - n * (R_COVER - FT.CLEAR), [R_COVER]]))
        spheres = np.vstack([spheres, np.array(new)])
    return dict(V=V, T=T, loop=loop, Vb=Vb, Vh=Vh, P=P, info=info, sk=sk, sdf=sdf, spheres=spheres, hist=hist, hs=hs)


def tag_of(sex, side='Left'): return sex if side == 'Left' else sex + '_R'


def save(sex, r, side='Left'):
    path = os.path.join(A.ROOT, 'products', f'glove_hero_{tag_of(sex, side)}.npz')
    np.savez(path, V=r['V'], T=r['T'], loop=r['loop'], Vb=r['Vb'], Vh=r['Vh'], spheres=r['spheres'], Rf=r['info']['Rf'], sx=r['info']['sx'], sy=r['info']['sy'])
    json.dump(r['P'], open(os.path.join(A.ROOT, 'products', f'glove_hero_{tag_of(sex, side)}_params.json'), 'w'))
    return path


if __name__ == '__main__':
    a = A.args_after_dashes()
    sex = a[0]; side = a[1] if len(a) > 1 else 'Left'
    r = build_hero_inner(sex, side)
    print('saved', save(sex, r, side))
