"""Export the built polo (polo_v2.blend, object Kit_Top) for Unity: packed pattern UVs, triangles, normals, fields, part (outer / rim / inner),
skin weights copied from the body (nearest skin, restricted to the bones a shirt follows, smoothed over the cloth).
Blender -b polo/polo_v2.blend --python polo2_export.py -- <hero>"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import cv2io, glib as G, finalize

FRONT, BACK, SLEEVE_R, SLEEVE_L, COLLAR, PLACKET, CUFF, BUTTON = range(8)
FABRIC = {FRONT: 7, BACK: 7, SLEEVE_R: 7, SLEEVE_L: 7, COLLAR: 9, PLACKET: 7, CUFF: 9, BUTTON: 4}
KIND = {FRONT: 0, BACK: 0, SLEEVE_R: 0, SLEEVE_L: 0, COLLAR: 1, PLACKET: 2, BUTTON: 3, CUFF: 20}

hero_id = sys.argv[sys.argv.index('--') + 1]
ob = bpy.data.objects['Kit_Top']
bpy.context.view_layer.objects.active = ob
for o in bpy.context.selected_objects: o.select_set(False)
ob.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=False, margin=.006)
bpy.ops.object.mode_set(mode='OBJECT')
me = ob.data
bm = bmesh.new(); bm.from_mesh(me)
bmesh.ops.triangulate(bm, faces=bm.faces)
bm.to_mesh(me); bm.free(); me.update()
n = len(me.vertices)
pos = np.zeros(n * 3); me.vertices.foreach_get('co', pos); pos = pos.reshape(-1, 3)
vn = np.zeros(n * 3); me.vertices.foreach_get('normal', vn); vn = vn.reshape(-1, 3)
li = np.zeros(len(me.loops), np.int32); me.loops.foreach_get('vertex_index', li)
uvl = np.zeros(len(me.loops) * 2); me.uv_layers.active.data.foreach_get('uv', uvl); uvl = uvl.reshape(-1, 2)
tri = li.reshape(-1, 3)
cuv = uvl.reshape(-1, 3, 2)
panel = np.zeros(len(me.polygons), np.int32); me.attributes['panel'].data.foreach_get('value', panel)
def vattr(name):
    if name not in me.attributes: return np.zeros(n)
    v = np.zeros(n, np.float32); me.attributes[name].data.foreach_get('value', v); return v.astype(np.float64)
fields = {k: vattr(k) for k in ('d_seam', 'd_hem', 'd_neck', 'd_cuff', 'collar_edge', 'pk_x', 'pk_z')}
mi = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', mi)
fpart = np.array([{0: 0, 1: 2, 2: 1}.get(int(m), 0) for m in mi], np.int32)     # 0 outer, 1 rim, 2 inner
# split vertices where the corner UVs differ
nt, src, uvm = finalize.split_by_uv(pos, tri, cuv)
P = pos[src]; N = vn[src]
# weights: nearest skin on the bones a shirt follows, then smoothed over the cloth (per connected part) so it moves as fabric, not as skin
hero = cv2io.Hero(hero_id); body = G.Body(hero)
W = finalize.restricted_weights(body, pos, 'Kit_Top')
# sleeves (incl. the sleeve cap over the shoulder) follow their arm only: shoulder, upper arm and the arm's blend bones, never the chest
vpan = np.full(n, -1); vpan[tri.ravel()] = np.repeat(panel, 3)
def arm_bones(left):
    ids = ('7', '8') if left else ('26', '27')
    return np.array([(nm in (('LeftShoulder', 'LeftUpperArm') if left else ('RightShoulder', 'RightUpperArm'))) or
                     (nm.startswith('ArmBlend_') and any(x in nm.split('_')[1:3] for x in ids)) for nm in body.bones])
for pan, left in ((SLEEVE_L, True), (SLEEVE_R, False)):
    m = vpan == pan
    if not m.any(): continue
    Ws = finalize.restricted_weights(body, pos[m], 'Kit_Top')
    Ws[:, ~arm_bones(left)] = 0
    ok = Ws.sum(1) > 1e-6
    Ws[ok] /= Ws[ok].sum(1, keepdims=True)
    Wm = W[m]; Wm[ok] = Ws[ok]; W[m] = Wm
W = finalize.smooth_weights(W, tri, int(os.environ.get('CV2_WSMOOTH', '4')))
bi, bw = G.prune(W, 4)
used = np.unique(bi[bw > 0]); remap = -np.ones(len(body.bones), np.int64); remap[used] = np.arange(len(used))
bi2 = np.where(bw > 0, remap[bi], 0)
# metres per uv unit
a3 = np.linalg.norm(np.cross(P[nt[:, 1]] - P[nt[:, 0]], P[nt[:, 2]] - P[nt[:, 0]]), axis=1).sum() * .5
e1 = uvm[nt[:, 1]] - uvm[nt[:, 0]]; e2 = uvm[nt[:, 2]] - uvm[nt[:, 0]]
a2 = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]).sum() * .5
S = float(np.sqrt(a3 / a2))
udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity'); os.makedirs(udir, exist_ok=True)
res = 2048
np.savez(os.path.join(udir, 'Kit_Top_final.npz'), pos=P, nrm=N, uv=uvm, tri=nt.astype(np.int32), mat=np.zeros(len(nt), np.int32),
         fabric=np.array([FABRIC[p] for p in panel], np.int32), part=fpart, kind=np.array([KIND[p] for p in panel], np.int32), panel=panel,
         src=src, S=S, res=res, bi=bi[src], bw=bw[src], polo2=1, **{'a_' + k: v[src] for k, v in fields.items()})
with open(os.path.join(udir, 'Kit_Top.bin'), 'wb') as fh:
    fh.write(P.astype(np.float32).tobytes()); fh.write(N.astype(np.float32).tobytes()); fh.write(uvm.astype(np.float32).tobytes())
    fh.write(bi2[src].astype(np.int32).tobytes()); fh.write(bw[src].astype(np.float32).tobytes()); fh.write(nt.astype(np.int32).tobytes())
man = {'hero': hero_id, 'pieces': [{'name': 'Kit_Top', 'vertexCount': int(len(P)), 'triangleCount': int(len(nt)), 'submeshes': [{'role': 'Kit_Shirt', 'start': 0, 'count': int(len(nt))}],
       'bones': [body.bones[i] for i in used], 'bindposes': [float(v) for i in used for v in hero.body.bind[i].T.reshape(-1)], 'metresPerUv': S, 'texture': res}]}
json.dump(man, open(os.path.join(udir, 'manifest.json'), 'w'))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(cv2io.ROOT, 'polo', 'polo_v2_final.blend'))
print('POLO2 export: verts %d tris %d bones %d metres/uv %.3f (%.1f px/cm at %d)' % (len(P), len(nt), len(used), S, res / S / 100, res))
