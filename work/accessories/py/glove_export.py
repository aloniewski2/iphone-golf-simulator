"""GLOVE export for one hero: fitted inner surface (products/glove_hero_<sex>.npz from glove_hero.py) -> shell + strap + patch -> UV/pack/extract -> skin weights -> .bin / npz / maps / piece.json.
Blender -b --factory-startup --python glove_export.py -- <Male|Female> [--skip-maps]"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_pipeline as PL, acc_canvas as CV, acc_fit as F, glove_bake as GBK
import glove_sdf as S, glove_mesh as M, glove_shell as SH, glove_curves as C, glove_pattern as PA, glove_product as GPR, glove_maps as GM, glove_fit as FT, glove_weights as GW

OUT = os.path.join(A.ROOT, 'out')


def tag_of(sex, side='Left'): return sex if side == 'Left' else sex + '_R'


def load_hero(sex, side='Left'):
    d = np.load(os.path.join(A.ROOT, 'products', f'glove_hero_{tag_of(sex, side)}.npz'))
    P = json.load(open(os.path.join(A.ROOT, 'products', f'glove_hero_{tag_of(sex, side)}_params.json')))
    return d, P


def export(sex, side='Left', maps=True, res=2048, ao_rays=10, wk=2, wsmooth=0):
    t0 = time.time()
    pname = 'Kit_Glove_L' if side == 'Left' else 'Kit_Glove_R'; mtag = 'Glove' if side == 'Left' else 'GloveR'
    hs = FT.HeroSkin(sex, side)
    d, P = load_hero(sex, side)
    V, T, loop, Vh = d['V'], d['T'], d['loop'], d['Vh']
    sx, sy = float(d['sx']), float(d['sy'])
    sdf, sk = S.make_product(P); sdf.spheres = d['spheres']
    # pattern + tab on the straight (unbent) hero glove, painted as (triangle, barycentric) samples
    probe = SH.assemble_shell(V, T, loop)
    surf = C.Surf(probe['O'], T)
    tab = GPR.product_tab_samples(probe['O'], T, surf)
    cur = PA.build_pattern(surf, sk, P=P, sx=sx, sy=sy)
    holes = PA.perforations(surf, sk, sx=sx, sy=sy)
    parts = GPR.build_all(Vh, T, loop, tab, Vg=V, mode='closed')
    objs = GPR.make_objects(parts)
    piece, joined, uvc = GPR.unwrap_pack_extract(objs, None)
    print(f'[{sex}] piece verts {len(piece["pos"])} tris {len(piece["tri"])} S={piece["S"]:.4f} t={time.time()-t0:.0f}s')
    H = hs.H
    # G -> N (rotation + translation; the left frame is a proper rotation)
    posN = H.from_G(piece['pos']); nrmN = piece['nrm'] @ H.R.T
    piece['pos'] = posN; piece['nrm'] = nrmN
    if side == 'Right': piece['tri'] = piece['tri'][:, ::-1].copy()      # the right frame is a reflection of the left one: keep the winding outward in N
    # ---- weights: per inner-surface vertex, then to every piece vertex
    n_in = len(Vh)
    VhN = H.from_G(Vh)
    VhN0 = H.from_G(d['Vh0']) if 'Vh0' in d else VhN                       # weights are taken from the clearance-hug fit (before pose inflation)
    names, Wd = GW.copy_weights_surface(hs, VhN0, side=side)
    if 'Wfit' in d and not os.environ.get('NO_WFIT'):
        Wd = d['Wfit']; print('using fitted weights (glove_wfit)')
    if wsmooth: Wd = GW.smooth_weights(Wd, T, iters=wsmooth, lam=.25)
    src_in = np.concatenate([np.arange(n_in), np.arange(n_in), np.asarray(loop)] + [np.asarray(loop)] * (parts['shell']['rim_rows'] - 1))[parts['shell']['used']]
    nshell = len(parts['shell']['pos'])
    src = piece['src']
    from mathutils.kdtree import KDTree
    kd = KDTree(n_in)
    ON = H.from_G(parts['O'])
    for i, p in enumerate(ON): kd.insert(tuple(p), i)
    kd.balance()
    Wp = np.zeros((len(src), Wd.shape[1]))
    for j, sidx in enumerate(src):
        if sidx < nshell: Wp[j] = Wd[src_in[sidx]]
        else: Wp[j] = Wd[kd.find(tuple(posN[j]))[1]]
    bi, bw = GW.top4(Wp)
    bind = [hs.hd.bind[hs.hd.idx(n)] for n in names]
    out_dir = os.path.join(OUT, sex, 'golf_glove'); os.makedirs(out_dir, exist_ok=True)
    extra = GPR.curve_extras(cur, holes, T, n_in, piece['src'])
    entry = PL.write_piece(out_dir, pname, piece, names, bi, bw, bind, res, ['Kit_Glove', 'Kit_Buckle'], detail='glove', extra=extra)
    entry['name'] = pname
    # ---- fit report
    c_in, _ = hs.clear(Vh, with_dir=True)
    nrmh = S.vertex_normals(Vh, T); Oh = Vh + nrmh * SH.T_BASE
    bad, ncand = FT.uncovered(hs, Oh, T)
    rep = dict(inner_clearance_min_mm=float(c_in.min() * 1000), inner_clearance_mean_mm=float(c_in.mean() * 1000), inner_clearance_p98_mm=float(np.percentile(c_in, 98) * 1000),
               inner_clearance_max_mm=float(c_in.max() * 1000), skin_vertices_not_enclosed=int(len(bad)), skin_vertices_tested=int(ncand), tris=int(len(piece['tri'])), verts=int(len(piece['pos'])),
               metres_per_uv=float(piece['S']), cover_spheres=int(len(d['spheres'])), px_per_mm=round(res / piece['S'] / 1000, 2))
    mpath = os.path.join(out_dir, 'piece.json')
    meta = json.load(open(mpath)) if os.path.exists(mpath) else dict(item='golf_glove', sex=sex, slot='HandGlove', npz='Kit_Glove_L_final.npz', tag='Glove', pieces=[], parent_bone='LeftHand', socket=None,
                                                              socket_local_position=None, socket_local_rotation=None, fit={}, fit_report={}, entries={}, tags={}, piece_bones={}, fit_reports={})
    meta['pieces'] = sorted(set(meta['pieces']) | {pname}, key=lambda x: x[-1] != 'L')
    meta['entries'][pname] = entry; meta['tags'][pname] = mtag; meta.setdefault('piece_bones', {})[pname] = names; meta.setdefault('fit_reports', {})[pname] = rep
    meta['fit'][pname] = dict(method='hero-measured hand recipe (SDF) + clearance hug', sx=sx, sy=sy, clearance_mm=FT.CLEAR * 1000)
    if side == 'Left': meta['piece'] = entry; meta['fit_report'] = rep; meta['bones'] = names
    json.dump(meta, open(mpath, 'w'), indent=1)
    if maps:
        hp = hs.hd.body_pos[hs.ids]; ht = hs.T                      # occluder: the hand itself (N positions of the region, region triangles)
        ao = CV.vertex_ao(piece['pos'], piece['nrm'], piece['tri'], rays=ao_rays, dist=.025, extra=(hp, ht))
        ao = 1 - .6 * (1 - ao)                                       # per-vertex AO on a 4 mm mesh smudges: keep it as a soft hint
        GBK.bake(os.path.join(out_dir, pname + '_final.npz'), out_dir, mtag, GM.details_glove, ao=ao)
    print(f'[{sex}] wrote {out_dir} t={time.time()-t0:.0f}s', json.dumps(rep))
    return piece, parts, hs


if __name__ == '__main__':
    a = A.args_after_dashes()
    export(a[0], side=(a[1] if len(a) > 1 and a[1] in ('Left', 'Right') else 'Left'), maps='--skip-maps' not in a, wk=int(a[a.index('--wk') + 1]) if '--wk' in a else 2, wsmooth=int(a[a.index('--ws') + 1]) if '--ws' in a else 0)
