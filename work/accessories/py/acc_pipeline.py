"""ACCESSORIES pipeline: product parts (Blender objects) -> unwrapped, packed, triangulated piece (numpy) -> per-hero .bin / npz / manifest.
Every builder produces parts with face attrs (zone, kind, fabric, sub) and float point attrs; this module does the rest.

Frames: everything here is N (see acc_common). `write_piece` converts to the body's mesh space only for the .bin (X flip + winding flip).
"""
import os, sys, json, math
import numpy as np
import bpy, bmesh
sys.path.insert(0, os.path.dirname(__file__))
import acc_common as A
import acc_geo as G


# ------------------------------------------------------------------ UVs
def _edge_seams(ob, extra_rule=None):
    me = ob.data
    zone = np.zeros(len(me.polygons), np.int32)
    if 'zone' in me.attributes: me.attributes['zone'].data.foreach_get('value', zone)
    ds = G.point_attr(ob, 'd_seam') if 'd_seam' in me.attributes else None
    bm = bmesh.new(); bm.from_mesh(me)
    bm.faces.ensure_lookup_table(); bm.edges.ensure_lookup_table()
    for e in bm.edges:
        fs = e.link_faces
        zs = {1 if zone[f.index] == 2 else 0 for f in fs}       # inner faces are their own islands; outer + rim share one
        s = (len(zs) > 1) or (len(fs) == 1)
        if extra_rule == 'crown' and ds is not None and len(fs) >= 2:
            s = s or (ds[e.verts[0].index] < 2e-5 and ds[e.verts[1].index] < 2e-5)
        e.seam = bool(s)
    bm.to_mesh(me); bm.free(); me.update()


def unwrap_part(ob, rule='sheet'):
    """rule: 'crown' (panel gores), 'sheet' (outer+rim | inner), 'smart' (tubes, small solids)."""
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    if not ob.data.uv_layers: ob.data.uv_layers.new(name='UVMap')
    if rule == 'smart':
        bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.003)
        bpy.ops.object.mode_set(mode='OBJECT')
        return
    _edge_seams(ob, 'crown' if rule == 'crown' else None)
    G.unwrap(ob, margin=.003)


def pack_joined(ob, margin=.004):
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.selected_objects: o.select_set(False)
    ob.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=False, margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


# ------------------------------------------------------------------ extraction
def split_by_uv(pos, tri, cuv, nrm=None):
    """vertices duplicated where corner UVs differ -> (new tris, source vertex index per new vertex, uv per new vertex)."""
    key = np.concatenate([tri.reshape(-1, 1).astype(np.float64), np.round(cuv.reshape(-1, 2), 6)], 1)
    uniq, first, inv = np.unique(key, axis=0, return_index=True, return_inverse=True)
    src = uniq[:, 0].astype(np.int64)
    uvm = uniq[:, 1:3]
    nt = inv.reshape(-1, 3).astype(np.int32)
    return nt, src, uvm


def extract(ob):
    """triangulate + read: dict(pos, nrm, uv, tri, src, zone, kind, fabric, sub, fields, S)."""
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free(); me.update()
    n = len(me.vertices)
    pos = np.zeros(n * 3); me.vertices.foreach_get('co', pos); pos = pos.reshape(-1, 3)
    vn = np.zeros(n * 3); me.vertices.foreach_get('normal', vn); vn = vn.reshape(-1, 3)
    li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
    uvl = np.zeros(len(me.loops) * 2); me.uv_layers.active.data.foreach_get('uv', uvl)
    tri = li.reshape(-1, 3); cuv = uvl.reshape(-1, 3, 2)

    def fattr(name, default=0):
        if name not in me.attributes: return np.full(len(me.polygons), default, np.int32)
        v = np.zeros(len(me.polygons), np.int32); me.attributes[name].data.foreach_get('value', v); return v

    fields = {}
    for a in me.attributes:
        if a.domain == 'POINT' and a.data_type == 'FLOAT':
            v = np.zeros(n, np.float32); a.data.foreach_get('value', v); fields[a.name] = v.astype(np.float64)
    nt, src, uvm = split_by_uv(pos, tri, cuv)
    P = pos[src]; N = vn[src]
    a3 = np.linalg.norm(np.cross(P[nt[:, 1]] - P[nt[:, 0]], P[nt[:, 2]] - P[nt[:, 0]]), axis=1).sum() * .5
    e1 = uvm[nt[:, 1]] - uvm[nt[:, 0]]; e2 = uvm[nt[:, 2]] - uvm[nt[:, 0]]
    a2 = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]).sum() * .5
    S = float(np.sqrt(a3 / a2))
    return dict(pos=P, nrm=N, uv=uvm, tri=nt, src=src, zone=fattr('zone'), kind=fattr('kind'), fabric=fattr('fabric', 6), sub=fattr('sub'),
                fields={k: v[src] for k, v in fields.items()}, S=S)


# ------------------------------------------------------------------ writing (per hero, mesh space)
def write_piece(out_dir, name, piece, bones, bi, bw, bindposes, res, submesh_roles, detail=None, extra=None):
    """piece: extract() dict with pos/nrm already in the hero's N frame (fitted). bones: list of bone names (index space of bi), bindposes: (len(bones),4,4)
    in the layout of hero.body.bind (mesh -> bone local). Writes <name>_final.npz (N frame, for the map baker), <name>.bin (mesh space) and returns the
    manifest entry. Triangles are ordered by submesh (role index in `sub`)."""
    os.makedirs(out_dir, exist_ok=True)
    pos = piece['pos']; nrm = piece['nrm']; tri = piece['tri']
    order = np.argsort(piece['sub'], kind='stable')
    tri_s = tri[order]
    sub_s = piece['sub'][order]
    # N -> mesh: flip X, reverse winding
    pm = A.N_to_mesh(pos); nm = A.N_to_mesh(nrm)
    tri_m = A.flip_tris(tri_s)
    arrays = dict(pos=pos, nrm=nrm, uv=piece['uv'], tri=tri_s.astype(np.int32), zone=piece['zone'][order], kind=piece['kind'][order], fabric=piece['fabric'][order],
                  sub=sub_s, S=piece['S'], res=res, bi=bi, bw=bw)
    for k, v in piece['fields'].items(): arrays['a_' + k] = v
    if detail: arrays['detail'] = detail
    if extra: arrays.update(extra)
    np.savez(os.path.join(out_dir, name + '_final.npz'), **arrays)
    # remap bone indices to the bones actually used
    used = np.unique(bi[bw > 0]); remap = -np.ones(len(bones), np.int64); remap[used] = np.arange(len(used))
    bi2 = np.where(bw > 0, remap[bi], 0)
    with open(os.path.join(out_dir, name + '.bin'), 'wb') as fh:
        fh.write(pm.astype(np.float32).tobytes()); fh.write(nm.astype(np.float32).tobytes()); fh.write(piece['uv'].astype(np.float32).tobytes())
        fh.write(bi2.astype(np.int32).tobytes()); fh.write(bw.astype(np.float32).tobytes()); fh.write(tri_m.astype(np.int32).tobytes())
    subs = []
    for s, role in enumerate(submesh_roles):
        idx = np.nonzero(sub_s == s)[0]
        if len(idx): subs.append({'role': role, 'start': int(idx[0]), 'count': int(len(idx))})
    entry = {'name': name, 'vertexCount': int(len(pm)), 'triangleCount': int(len(tri_m)), 'submeshes': subs,
             'bones': [bones[i] for i in used], 'bindposes': [float(v) for i in used for v in np.asarray(bindposes[i]).T.reshape(-1)],
             'metresPerUv': piece['S'], 'texture': int(res)}
    return entry


def merge_manifest(out_dir, hero_id, entry):
    path = os.path.join(out_dir, 'manifest.json')
    man = json.load(open(path)) if os.path.exists(path) else {'hero': hero_id, 'pieces': []}
    man['pieces'] = [p for p in man['pieces'] if p['name'] != entry['name']] + [entry]
    json.dump(man, open(path, 'w'))
