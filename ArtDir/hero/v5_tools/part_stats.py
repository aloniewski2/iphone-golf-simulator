import bpy, sys, math, json
import numpy as np
from mathutils import Matrix, Vector
argv = sys.argv[sys.argv.index('--') + 1:]; GLB = argv[0]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=GLB)
out = {}
for o in [o for o in bpy.data.objects if o.type == 'MESH']:
    me = o.data; o.data.transform(o.matrix_world); o.matrix_world = Matrix.Identity(4)
    mat = me.materials[0] if me.materials else None; img = None
    if mat and mat.use_nodes:
        for n in mat.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image: img = n.image; break
    if not img: continue
    W, H = img.size; px = np.asarray(img.pixels[:], dtype=np.float32).reshape(H, W, 4); uv = me.uv_layers.active.data
    cols = []
    for p in list(me.polygons)[::max(1, len(me.polygons) // 400)]:
        u = sum(uv[i].uv[0] for i in p.loop_indices) / p.loop_total; v = sum(uv[i].uv[1] for i in p.loop_indices) / p.loop_total
        cols.append(px[min(H - 1, int(v % 1 * H)), min(W - 1, int(u % 1 * W)), :3])
    c = np.median(np.array(cols), 0)
    vs = [v.co for v in me.vertices]
    out[o.name] = dict(n=len(me.polygons), rgb=[round(float(x), 3) for x in c], br=round(float(c[2] / max(c[0], 1e-3)), 3), gr=round(float(c[1] / max(c[0], 1e-3)), 3),
                      xmax=round(max(v.x for v in vs), 3), zmin=round(min(v.z for v in vs), 3), zmax=round(max(v.z for v in vs), 3))
print('STATS', json.dumps(out))
