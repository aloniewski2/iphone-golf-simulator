"""GLOVE: assemble the whole glove (shell + strap + anchor patch) from an inner surface, for the product or for any fitted hand, and turn it into the
unwrapped, packed, extracted piece the accessories pipeline writes.
build_all(V, T, loop, tab) -> numpy parts;  make_objects(parts) -> Blender objects (face attrs set);  unwrap_pack_extract(objs, uv=None) -> piece dict (+ uv cache)."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import acc_common as A, acc_geo as G, acc_pipeline as PL
import glove_sdf as S, glove_mesh as M, glove_shell as SH, glove_parts as PT, glove_curves as C, glove_strap as ST, glove_pattern as PA

K_SHELL, K_LINING, K_STRAP, K_PATCH, K_HOOK = SH.K_SHELL, SH.K_LINING, ST.K_STRAP, ST.K_PATCH, ST.K_HOOK


def product_tab_samples(O, T, surf):
    sT, sB, sth, sA = ST.grid_samples(surf, O, ST.TH0, ST.TH1, ST.A0, ST.A1, 30, 5)
    pT, pB, pth, pA = ST.grid_samples(surf, O, ST.PTH0, ST.PTH1, ST.PA0, ST.PA1, 22, 5)
    return dict(sT=sT, sB=sB, sth=sth, pT=pT, pB=pB, pth=pth)


def shell_fields(part, Vg, Tg, loop):
    """add product-frame coordinates and dorsal class to the shell's per-vertex fields (outer, inner and rim rows share their S_in vertex)."""
    n = part['n_inner']; k = len(loop)
    rows = part['rim_rows']
    src = np.concatenate([np.arange(n), np.arange(n)] + [np.asarray(loop)] * rows)[part['used']]
    nrm = part['nrm']
    f = part['fields']
    f['gx'], f['gy'], f['gz'] = Vg[src, 0], Vg[src, 1], Vg[src, 2]
    f['nzp'] = nrm[src, 2]
    # tight creases of the fitted surface (folded faces + their ring): the marks fade out there instead of turning into noise
    import glove_fit as FT
    bad = FT.fold_faces(part['O'], part['T'])
    fl = np.zeros(n); fl[np.unique(part['T'][bad])] = 1.0
    E = S.edges_of(part['T']); f2 = fl.copy(); f2[E[fl[E[:, 0]] > 0, 1]] = 1; f2[E[fl[E[:, 1]] > 0, 0]] = 1
    f['fold'] = f2[src]
    return src


INNER_Y = 0.030          # the lining only exists up to here (G frame y): deeper in, the hand fills the glove and nobody can see the inside


def inner_face_mask(Vg, T):
    return Vg[T][:, :, 1].max(1) <= INNER_Y


def build_all(V, T, loop, tab, Vg=None, mode='closed', open_deg=70.0):
    """V,T,loop: inner surface (any fit). Vg: product-frame positions of the same vertices (for the coordinate fields; default V = product)."""
    Vg = V if Vg is None else Vg
    gy = Vg[:, 1]
    inner_g = SH.smooth01((gy - (INNER_Y - 0.014)) / 0.014)
    import glove_fit as FT
    bad = FT.fold_faces(V, T, thr=0.5)
    fl = np.zeros(len(V)); fl[np.unique(T[bad])] = 1.0
    E_ = S.edges_of(T)
    for _ in range(3):
        f2 = fl.copy(); f2[E_[fl[E_[:, 0]] > 0, 1]] = np.maximum(f2[E_[fl[E_[:, 0]] > 0, 1]], 0.7 * fl[E_[fl[E_[:, 0]] > 0, 0]]); f2[E_[fl[E_[:, 1]] > 0, 0]] = np.maximum(f2[E_[fl[E_[:, 1]] > 0, 0]], 0.7 * fl[E_[fl[E_[:, 1]] > 0, 1]]); fl = f2
    tscale = 1 - 0.72 * fl
    part = SH.assemble_shell(V, T, loop, inner_faces=inner_face_mask(Vg, T), inner_g=inner_g, thick_scale=tscale)
    src = shell_fields(part, Vg, T, loop)
    O = part['O']
    P0 = ST.eval_grid(tab['sT'], tab['sB'], O, T)
    strap = ST.build_strap(P0, tab['sth'], mode=mode, open_deg=open_deg)
    Pp = ST.eval_grid(tab['pT'], tab['pB'], O, T)
    patch = ST.build_patch(Pp, tab['pth'])
    return dict(shell=part, strap=strap, patch=patch, O=O, src=src)


def dors_face_class(part):
    """per-face dorsal/palmar class (+1/-1) of the shell from its vertex normals' z (product frame)."""
    nz = part['fields']['nzp']; tris = part['tris']
    c = np.sign(nz[tris].mean(1)); c[c == 0] = 1
    return c.astype(np.int32)


def make_objects(parts):
    sh = parts['shell']
    ob_shell = PT.to_object('Glove_Shell', sh, fix=False)
    a = ob_shell.data.attributes.new('dors', 'INT', 'FACE'); a.data.foreach_set('value', dors_face_class(sh))
    st = ST.make_thick('Glove_Strap', parts['strap'])
    pa = ST.make_thick('Glove_Patch', parts['patch'])
    # strap faces: outer (zone 0) + rim = strap webbing; inner of the free end = hook; patch = loop fabric
    me = st.data
    zone = np.zeros(len(me.polygons), np.int32); me.attributes['zone'].data.foreach_get('value', zone)
    hk = G.point_attr(st, 'hookf')
    tri = np.array([[v for v in p.vertices] for p in me.polygons], dtype=object)
    fh = np.array([np.mean(hk[list(p.vertices)]) for p in me.polygons])
    kind = np.where((zone == 2) & (fh > .5), K_HOOK, K_STRAP)
    fab = np.where(kind == K_HOOK, ST.FAB_PIQUE, ST.FAB_RIB)
    G.set_face_attr(st, 'kind', kind); G.set_face_attr(st, 'fabric', fab); G.set_face_attr(st, 'sub', 0)
    G.set_face_attr(pa, 'kind', K_PATCH); G.set_face_attr(pa, 'fabric', ST.FAB_PIQUE); G.set_face_attr(pa, 'sub', 0)
    return ob_shell, st, pa


def unwrap_shell(ob):
    me = ob.data
    zone = np.zeros(len(me.polygons), np.int32); me.attributes['zone'].data.foreach_get('value', zone)
    dors = np.zeros(len(me.polygons), np.int32); me.attributes['dors'].data.foreach_get('value', dors)
    bm = bmesh.new(); bm.from_mesh(me); bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    for e in bm.edges:
        fs = e.link_faces
        s = len(fs) == 1
        if len(fs) == 2:
            za = 1 if zone[fs[0].index] == 2 else 0; zb = 1 if zone[fs[1].index] == 2 else 0
            s = (za != zb) or (dors[fs[0].index] != dors[fs[1].index])
        e.seam = bool(s)
    bm.to_mesh(me); bm.free(); me.update()
    G.unwrap(ob, margin=.002)


def pack_glove(ob, margin=.003):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=margin, shape_method='CONCAVE')
    bpy.ops.object.mode_set(mode='OBJECT')


def unwrap_pack_extract(objs, uv=None):
    shell, strap, patch = objs
    if uv is None:
        unwrap_shell(shell)
        PL.unwrap_part(strap, 'sheet'); PL.unwrap_part(patch, 'sheet')
        joined = G.join([shell, strap, patch], 'Kit_Glove_L')
        pack_glove(joined)
        me = joined.data
        uvl = np.zeros(len(me.loops) * 2); me.uv_layers.active.data.foreach_get('uv', uvl)
        uv_cache = uvl.reshape(-1, 2).copy()
    else:
        joined = G.join([shell, strap, patch], 'Kit_Glove_L')
        me = joined.data
        if not me.uv_layers: me.uv_layers.new(name='UVMap')
        assert len(me.loops) == len(uv), 'topology differs from the cached UV set'
        me.uv_layers.active.data.foreach_set('uv', uv.astype(np.float32).ravel()); uv_cache = uv
    piece = PL.extract(joined)
    return piece, joined, uv_cache


def curve_extras(cur, holes, T_in, n_in, src):
    ex = dict(cv_names=np.array(list(cur.keys())), T_in=np.asarray(T_in, np.int64), n_in=np.int64(n_in), src=np.asarray(src, np.int64),
              hole_tri=holes.tri, hole_bary=holes.bary)
    for k, c in cur.items():
        ex[f'cv_{k}_tri'] = c.tri; ex[f'cv_{k}_bary'] = c.bary; ex[f'cv_{k}_wf'] = c.wf; ex[f'cv_{k}_tip'] = np.int64(c.tip if c.tip is not None else 0)
        ex[f'cv_{k}_pts0'] = c.pts0; ex[f'cv_{k}_edge'] = c.edge_dist()
    return ex
