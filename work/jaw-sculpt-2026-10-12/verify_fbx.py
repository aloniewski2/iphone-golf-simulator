"""Round-trip check: the new FBX against the original one (work/reference-rebuild/face-finish30/composed-ears, byte-identical to what Unity used). Same vertex count/order, triangles, weights, bones, eye/brow mesh and
shape-key deltas; only head/neck positions above the collar may move.  Blender -b --factory-startup --python verify_fbx.py
"""
import bpy, numpy as np, pathlib, json
W = pathlib.Path(__file__).resolve().parent
R = W.parents[1]


def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    d = {}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            me = o.data
            co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get('co', co); co = co.reshape(-1, 3)
            tri = np.empty(len(me.polygons) and sum(len(p.vertices) for p in me.polygons), dtype=np.int64); me.polygons.foreach_get('vertices', tri)
            groups = [o.vertex_groups[g.group].name + f'{g.weight:.6f}' for v in me.vertices for g in v.groups]
            keys = {}
            if me.shape_keys:
                for k in me.shape_keys.key_blocks:
                    a = np.empty(len(me.vertices) * 3); k.data.foreach_get('co', a); keys[k.name] = a.reshape(-1, 3)
            d[o.name] = dict(co=co, tri=tri, groups=hash(tuple(groups)), keys=keys, mats=[m.name for m in me.materials])
        if o.type == 'ARMATURE':
            d['__bones'] = hash(tuple((b.name, tuple(np.round(np.array(b.matrix_local), 6).ravel())) for b in o.data.bones))
    return d


report = {}
for sex in ('Male', 'Female'):
    old = load(R / f'work/reference-rebuild/face-finish30/composed-ears/{sex}_OriginalFaceFinish30ComposedEars.fbx')   # = the FBX the game used before
    new = load(W / 'export' / f'{sex}.fbx')
    rep = {'meshes_old': sorted(k for k in old if not k.startswith('__')), 'meshes_new': sorted(k for k in new if not k.startswith('__')),
           'bones_same': old['__bones'] == new['__bones']}
    for name in ('Body', 'ReferenceFeatures'):
        a, b = old[name], new[name]
        d = np.linalg.norm(b['co'] - a['co'], axis=1)
        moved = d > 1e-6
        r = {'vertices': [len(a['co']), len(b['co'])], 'triangles_same': bool(np.array_equal(a['tri'], b['tri'])), 'weights_same': a['groups'] == b['groups'],
             'materials_same': a['mats'] == b['mats'], 'moved': int(moved.sum()), 'max_move_mm': round(float(d.max() * 1000), 3)}
        if moved.any():
            r['moved_z_range_fbx'] = [round(float(a['co'][moved][:, i].min()), 4) for i in (2,)] + [round(float(a['co'][moved][:, 2].max()), 4)]
        if a['keys']:
            err = 0.0
            for k in a['keys']:
                da = a['keys'][k] - a['co']; db = b['keys'][k] - b['co']
                err = max(err, float(np.abs(da - db).max()))
            r['shape_keys'] = sorted(a['keys']); r['shape_delta_max_err_mm'] = round(err * 1000, 6)
        rep[name] = r
    report[sex] = rep
    print(sex, json.dumps(rep), flush=True)
(W / 'export' / 'VERIFY.json').write_text(json.dumps(report, indent=2) + '\n')
