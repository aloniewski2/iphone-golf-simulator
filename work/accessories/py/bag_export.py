"""BAG: unwrap / join / pack / extract the product, bake its maps, write out/bag_product_<pose>/ ; (fit to the heroes lives in bag_fit.py).
Blender -b --factory-startup --python bag_export.py -- product <folded|open> [--skip-maps] [--res 2048]"""
import sys, os, json, math, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_pipeline as PL, bag_canvas as CV
import bag as B, bag_geo as BG, bag_maps as BM

OUT = os.path.join(A.ROOT, 'out')
SHEET_PARTS = ('Strap_', 'Handle', 'FrontPocket', 'LowerPocket', 'BackPocket', 'Hub', 'Zip')


def rule_for(ob):
    n = ob.name
    if n in ('Shell', 'Lining', 'Base'): return 'crown'
    if n.startswith(SHEET_PARTS) and 'Buckle' not in n: return 'sheet'
    if n.startswith('Head_'): return 'sheet'
    return 'smart'


def pack_weighted(ob, margin=.0015):
    """average the island scales, then give every part its own texel-density factor (bag.uv_weight; the shell front / back panel via the face attribute uvw100), then pack."""
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.object.mode_set(mode='OBJECT')
    me = ob.data
    n = len(me.loops)
    uvl = me.uv_layers.active.data
    uv = np.zeros(n * 2, np.float32); uvl.foreach_get('uv', uv); uv = uv.reshape(-1, 2).astype(np.float64)
    npoly = len(me.polygons)
    pid = np.zeros(npoly, np.int32); w100 = np.full(npoly, 100, np.int32)
    me.attributes['pid'].data.foreach_get('value', pid)
    me.attributes['uvw100'].data.foreach_get('value', w100)
    pt = np.zeros(npoly, np.int32); me.polygons.foreach_get('loop_total', pt)
    loop_poly = np.repeat(np.arange(npoly), pt)
    gid = pid[loop_poly].astype(np.int64) * 1000 + w100[loop_poly]
    for g in np.unique(gid):
        m = gid == g; c = 0.5 * (uv[m].min(0) + uv[m].max(0)); w = (g % 1000) / 100.0
        uv[m] = c + (uv[m] - c) * w
    uvl.foreach_set('uv', uv.astype(np.float32).ravel())
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=False, margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def make_piece(parts, name='Kit_Bag'):
    for i, ob in enumerate(parts):
        PL.unwrap_part(ob, rule_for(ob))
        if 'uvw100' not in ob.data.attributes: G.set_face_attr(ob, 'uvw100', B.uv_weight(ob.name))
        G.set_face_attr(ob, 'pid', i)
    joined = G.join(parts, name)
    pack_weighted(joined)
    return PL.extract(joined), joined


def px_by_kind(piece, res):
    """area-weighted px/mm (texels per millimetre) of every kind of part"""
    P, T, UV = piece['pos'], piece['tri'], piece['uv']
    a3 = np.linalg.norm(np.cross(P[T[:, 1]] - P[T[:, 0]], P[T[:, 2]] - P[T[:, 0]]), axis=1) * .5
    e1 = UV[T[:, 1]] - UV[T[:, 0]]; e2 = UV[T[:, 2]] - UV[T[:, 0]]
    a2 = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]) * .5
    pxmm = res * np.sqrt(np.maximum(a2, 1e-14) / np.maximum(a3, 1e-12)) / 1000
    out = {}
    names = {v: k for k, v in B.K.items()}
    for kd in np.unique(piece['kind']):
        m = (piece['kind'] == kd) & (a3 > 1e-9)
        if m.any(): out[names.get(int(kd), str(kd))] = round(float((pxmm[m] * a3[m]).sum() / a3[m].sum()), 2)
    return out


def vertex_set(piece, kinds):
    m = np.isin(piece['kind'], list(kinds))
    return np.unique(piece['tri'][m])


def make_ao(piece, extra=None, rays=12):
    P, N, T = piece['pos'], piece['nrm'], piece['tri']
    ao = CV.vertex_ao(P, N, T, rays=rays, dist=.035, extra=extra)
    dark = vertex_set(piece, {B.K['BASE'], B.K['FOOT']})
    ao[dark] *= 0.55                                      # near-black rubber: the colour is the trim navy darkened by baked occlusion
    return ao


def write_product(piece, out_dir, res=4096, maps=True):
    n = len(piece['pos'])
    bones = ['Root']; bi = np.zeros((n, 4), np.int32); bw = np.zeros((n, 4), np.float32); bw[:, 0] = 1
    bind = [np.eye(4)]
    entry = PL.write_piece(out_dir, 'Kit_Bag', piece, bones, bi, bw, bind, res, ['Kit_Bag', 'Kit_Buckle'], detail='bag')
    if maps:
        t0 = time.time()
        ao = make_ao(piece)
        print('ao', round(time.time() - t0, 1), 's')
        CV.bake(os.path.join(out_dir, 'Kit_Bag_final.npz'), out_dir, 'Bag', BM.details_bag, ao=ao)
    return entry


def main():
    a = A.args_after_dashes()
    mode = a[0]
    if mode == 'product':
        pose = a[1]
        res = int(a[a.index('--res') + 1]) if '--res' in a else 4096
        parts, S, infos = B.build_product(pose)
        piece, joined = make_piece(parts)
        print('piece', len(piece['pos']), 'verts', len(piece['tri']), 'tris, S=%.4f m/uv, %.2f px/mm' % (piece['S'], res / piece['S'] / 1000))
        out_dir = os.path.join(OUT, 'bag_product_' + pose)
        entry = write_product(piece, out_dir, res, maps='--skip-maps' not in a)
        json.dump(dict(tris=int(len(piece['tri'])), verts=int(len(piece['pos'])), S=piece['S'], px_per_mm=res / piece['S'] / 1000), open(os.path.join(out_dir, 'build.json'), 'w'))


if __name__ == '__main__':
    main()
