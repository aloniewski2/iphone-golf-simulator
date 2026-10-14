"""Export a built V2 garment (object `Kit_<Piece>` in the open .blend) for Unity: packed pattern UVs, triangles, normals, per-vertex fields, per-face
kind / fabric / role / part, skin weights copied from the body (nearest skin, restricted to the bones the piece follows, smoothed over the cloth),
the .bin + manifest entry the Unity installer reads (the manifest is MERGED: only this piece is replaced).
Blender -b <built.blend> --python export2.py -- <hero> <Kit_Piece> <detail> <final.blend> [extra args as k=v]
  detail = the name of the map-detail function in maps.py (shorts2, tank2, skirt2, ...)."""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, bmesh
import cv2io, glib as G, finalize

argv = sys.argv[sys.argv.index('--') + 1:]
hero_id, piece, detail, final_blend = argv[:4]
kv = dict(a.split('=', 1) for a in argv[4:])
ob = bpy.data.objects[piece]
roles = list(ob['roles'])
bpy.context.view_layer.objects.active = ob
for o in bpy.context.selected_objects: o.select_set(False)
ob.select_set(True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=bool(int(kv.get('rotate', 0))), margin=float(kv.get('margin', .006)))
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
tri = li.reshape(-1, 3); cuv = uvl.reshape(-1, 3, 2)
def fattr(name, default=0):
    if name not in me.attributes: return np.full(len(me.polygons), default, np.int32)
    v = np.zeros(len(me.polygons), np.int32); me.attributes[name].data.foreach_get('value', v); return v
panel = fattr('panel'); kind = fattr('face_kind'); fabric = fattr('fabric', 6); role = fattr('role'); kind0 = kind.copy()
mi = np.zeros(len(me.polygons), np.int32); me.polygons.foreach_get('material_index', mi)
part = np.array([{0: 0, 1: 2, 2: 1}.get(int(m), 0) for m in mi], np.int32)         # 0 outer, 1 rim, 2 inner
vfields = {}
for a in me.attributes:
    if a.domain == 'POINT' and a.data_type == 'FLOAT':
        v = np.zeros(n, np.float32); a.data.foreach_get('value', v); vfields[a.name] = v.astype(np.float64)
# split vertices where the corner UVs differ, then order triangles by role so every submesh is contiguous
nt, src, uvm = finalize.split_by_uv(pos, tri, cuv)
order = np.argsort(role, kind='stable')
nt = nt[order]; panel = panel[order]; kind = kind[order]; fabric = fabric[order]; role = role[order]; part = part[order]
P = pos[src]; N = vn[src]
# weights (the piece's own bones), smoothed so the cloth moves as fabric, not as skin
hero = cv2io.Hero(hero_id); body = G.Body(hero)
if piece.startswith('Kit_Shoe'):
    # a shoe is stiff: the Foot bone carries it, the forefoot flexes with the toes from the ball forward, the shin only touches the collar
    import shoe2
    fr = shoe2.Frame(body, 'L' if piece.endswith('_L') else 'R')
    q = fr.to(pos); sd_ = 'Left' if piece.endswith('_L') else 'Right'
    fi, ti, li = body.bones.index(sd_ + 'Foot'), body.bones.index(sd_ + 'Toes'), body.bones.index(sd_ + 'LowerLeg')
    wt = np.clip((q[:, 1] / fr.L - .66) / .22, 0, 1) * .55
    wl = np.clip((q[:, 2] - .06) / .05, 0, 1) * np.clip((.42 * fr.L - q[:, 1]) / (.15 * fr.L), 0, 1) * .3
    W = np.zeros((len(pos), len(body.bones))); W[:, ti] = wt; W[:, li] = wl; W[:, fi] = 1 - wt - wl
else:
    W = finalize.restricted_weights(body, pos, piece)
    W = finalize.smooth_weights(W, tri, int(kv.get('wsmooth', os.environ.get('CV2_WSMOOTH', '6'))))
    if (kind0 == 11).any():
        # a pleated skirt over tight under-shorts: the hip bone carries the waist; lower down the skirt moves WITH the under-shorts under it (the weights of the
        # nearest under-shorts vertex), so the thighs never push through it; smoothed over the cloth so pleats do not tear
        from mathutils import Vector
        from mathutils.bvhtree import BVHTree
        sv = np.zeros(n, bool); sv[tri[kind0 == 11].ravel()] = True
        un = np.zeros(n, bool); un[tri[kind0 == 18].ravel()] = True
        zt, zb = pos[sv, 2].max(), pos[sv, 2].min()
        t_ = np.clip((zt - pos[:, 2]) / max(1e-6, zt - zb), 0, 1)
        hi = body.bones.index('Hips')
        Wh = np.zeros_like(W); Wh[:, hi] = 1
        utri = tri[(kind0 == 18)]
        tree = BVHTree.FromPolygons([tuple(p) for p in pos], [tuple(t) for t in utri], all_triangles=True)
        Wu = W.copy()
        uidx = np.nonzero(un)[0]
        for v in np.nonzero(sv)[0]:
            r = tree.find_nearest(Vector(pos[v]))
            f = utri[r[2]]; q = np.array(r[0]); a_, b_, c_ = pos[f]
            v0, v1, v2 = b_ - a_, c_ - a_, q - a_
            d00, d01, d11, d20, d21 = v0 @ v0, v0 @ v1, v1 @ v1, v2 @ v0, v2 @ v1
            den = d00 * d11 - d01 * d01 or 1e-20
            vv = (d11 * d20 - d01 * d21) / den; ww = (d00 * d21 - d01 * d20) / den; uu = 1 - vv - ww
            bc = np.clip([uu, vv, ww], 0, 1); bc /= bc.sum()
            Wu[v] = bc[0] * W[f[0]] + bc[1] * W[f[1]] + bc[2] * W[f[2]]
        bmix = (np.clip((t_ - .02) / .22, 0, 1) ** 1.0)[:, None] * float(os.environ.get('CV2_SKIRT_FOLLOW', '1.0'))
        Ws = (1 - bmix) * Wh + bmix * Wu
        W[sv] = Ws[sv]
        W = finalize.smooth_weights(W, tri, 16, mask=sv.astype(float))
W = W / np.maximum(W.sum(1, keepdims=True), 1e-9)
bi, bw = G.prune(W, 4)
used = np.unique(bi[bw > 0]); remap = -np.ones(len(body.bones), np.int64); remap[used] = np.arange(len(used))
bi2 = np.where(bw > 0, remap[bi], 0)
a3 = np.linalg.norm(np.cross(P[nt[:, 1]] - P[nt[:, 0]], P[nt[:, 2]] - P[nt[:, 0]]), axis=1).sum() * .5
e1 = uvm[nt[:, 1]] - uvm[nt[:, 0]]; e2 = uvm[nt[:, 2]] - uvm[nt[:, 0]]
a2 = np.abs(e1[:, 0] * e2[:, 1] - e1[:, 1] * e2[:, 0]).sum() * .5
S = float(np.sqrt(a3 / a2))
res = int(kv.get('res', 2048))
udir = os.path.join(cv2io.ROOT, 'out', hero_id, 'unity'); os.makedirs(udir, exist_ok=True)
extra = {'collar_prof': np.array(ob['collar_prof']).reshape(-1, 2)} if 'collar_prof' in ob else {}
np.savez(os.path.join(udir, piece + '_final.npz'), pos=P, nrm=N, uv=uvm, tri=nt.astype(np.int32), mat=role.astype(np.int32), fabric=fabric.astype(np.int32),
         part=part.astype(np.int32), kind=kind.astype(np.int32), panel=panel.astype(np.int32), src=src, S=S, res=res, bi=bi[src], bw=bw[src],
         detail=np.array(detail), roles=np.array(roles), v2=1, **extra, **{'a_' + k: v[src] for k, v in vfields.items()})
with open(os.path.join(udir, piece + '.bin'), 'wb') as fh:
    fh.write(P.astype(np.float32).tobytes()); fh.write(N.astype(np.float32).tobytes()); fh.write(uvm.astype(np.float32).tobytes())
    fh.write(bi2[src].astype(np.int32).tobytes()); fh.write(bw[src].astype(np.float32).tobytes()); fh.write(nt.astype(np.int32).tobytes())
subs = []
for ri, r in enumerate(roles):
    c = int((role == ri).sum())
    if c: subs.append({'role': r, 'start': int(np.nonzero(role == ri)[0][0]), 'count': c})
entry = {'name': piece, 'vertexCount': int(len(P)), 'triangleCount': int(len(nt)), 'submeshes': subs, 'bones': [body.bones[i] for i in used],
         'bindposes': [float(v) for i in used for v in hero.body.bind[i].T.reshape(-1)], 'metresPerUv': S, 'texture': res}
mf = os.path.join(udir, 'manifest.json')
man = json.load(open(mf)) if os.path.exists(mf) else {'hero': hero_id, 'pieces': []}
man['pieces'] = [p for p in man['pieces'] if p['name'] != piece] + [entry]
json.dump(man, open(mf, 'w'))
bpy.ops.wm.save_as_mainfile(filepath=final_blend)
print('EXPORT2 %s: verts %d tris %d bones %d metres/uv %.3f (%.1f px/cm at %d) roles %s' % (piece, len(P), len(nt), len(used), S, res / S / 100, res, [s['role'] for s in subs]))
