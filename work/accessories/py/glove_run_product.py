"""Build the glove alone (product): pieces, UVs, maps, files in products/glove_v1/.  Blender -b --python glove_run_product.py -- [open] [--skip-maps]
Outputs: products/glove_v1/{closed,open}/Kit_Glove_L_final.npz, Glove_N.png, Glove_M.png ; uv cache products/glove_v1/uv.npz"""
import sys, os, json, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_pipeline as PL, acc_canvas as CV, glove_bake as GBK
import glove_sdf as S, glove_shell as SH, glove_curves as C, glove_pattern as PA, glove_product as GP, glove_maps as GM

PROD = os.path.join(A.ROOT, 'products', 'glove_v1')
os.makedirs(PROD, exist_ok=True)


def build_product(mode='closed', maps=True, ao_rays=10, res=2048):
    t0 = time.time()
    d = np.load(os.path.join(A.ROOT, 'products/glove_inner_v1.npz'))
    V, T, loop = d['V'], d['T'], d['loop']
    A.reset()
    sdf, sk = S.make_product()
    probe = SH.assemble_shell(V, T, loop)
    surf = C.Surf(probe['O'], T)
    tab = GP.product_tab_samples(probe['O'], T, surf)
    cur = PA.build_pattern(surf, sk)
    holes = PA.perforations(surf, sk)
    parts = GP.build_all(V, T, loop, tab, mode=mode, open_deg=150.0)
    objs = GP.make_objects(parts)
    uvfile = os.path.join(PROD, 'uv.npz')
    uv = np.load(uvfile)['uv'] if (os.path.exists(uvfile) and mode == 'open') else None
    piece, joined, uvc = GP.unwrap_pack_extract(objs, uv)
    if uv is None and mode == 'closed': np.savez(uvfile, uv=uvc)
    print(mode, 'piece verts', len(piece['pos']), 'tris', len(piece['tri']), 'S=%.4f' % piece['S'], 'time', round(time.time() - t0, 1))
    n = len(piece['pos'])
    bi = np.zeros((n, 4), np.int32); bw = np.zeros((n, 4), np.float32); bw[:, 0] = 1
    out = os.path.join(PROD, mode); os.makedirs(out, exist_ok=True)
    extra = GP.curve_extras(cur, holes, T, len(V), piece['src'])
    PL.write_piece(out, 'Kit_Glove_L', piece, ['LeftHand'], bi, bw, [np.eye(4)], res, ['Kit_Glove', 'Kit_Buckle'], detail='glove', extra=extra)
    if maps:
        from acc_fit import recompute_normals
        nt = piece['tri']
        ao = CV.vertex_ao(piece['pos'], piece['nrm'], nt, rays=ao_rays, dist=.03)
        ao = 1 - .6 * (1 - ao)
        GBK.bake(os.path.join(out, 'Kit_Glove_L_final.npz'), out, 'Glove', GM.details_glove, ao=ao)
    return piece


if __name__ == '__main__':
    a = A.args_after_dashes()
    for mode in (['open'] if 'open' in a else ['closed']):
        build_product(mode, maps='--skip-maps' not in a)
