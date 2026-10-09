import bpy, json, math, sys
from mathutils import Vector, Matrix
rig = bpy.data.objects['Hero_01_Rig']
if rig.animation_data: rig.animation_data.action = None
for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
dg = bpy.context.evaluated_depsgraph_get()
out = {}
def bone(n):
    b = rig.data.bones[n]; return [list(rig.matrix_world @ b.head_local), list(rig.matrix_world @ b.tail_local)]
out['bones'] = {n: bone(n) for n in ['Hips', 'Spine', 'Chest', 'Neck', 'Head', 'UpperLeg.L', 'LowerLeg.L', 'Foot.L', 'Toes.L', 'UpperArm.L', 'Shoulder.L']}
out['bone_names'] = [b.name for b in rig.data.bones]
def verts(name, evaluated=True):
    o = bpy.data.objects[name]
    me = o.evaluated_get(dg).to_mesh() if evaluated else o.data
    vs = [o.matrix_world @ v.co for v in me.vertices]
    return o, me, vs
def bbox(vs): return [[min(v[i] for v in vs) for i in range(3)], [max(v[i] for v in vs) for i in range(3)]]
for n in ['Hair_Default', 'Hat_Visor', 'Shorts_Default', 'Shoes_Default', 'Shirt_Default', 'Body_Skin', 'Body_EyeSphere_L']:
    o, me, vs = verts(n); out[n] = {'bbox': bbox(vs), 'verts': len(vs)}
# visor band: per height slice, radius around head axis
hc = Vector(out['bones']['Head'][0])
o, me, vs = verts('Hat_Visor')
zs = sorted(v.z for v in vs)
out['visor_z'] = [zs[0], zs[len(zs) // 2], zs[-1]]
# leg slices (left leg, x>0), body skin only, radius about the leg centre per 2 cm
o, me, vs = verts('Body_Skin')
mats = [p.material_index for p in me.polygons]
sl = {}
for v in vs:
    if v.x > .02 and v.z < .62:
        k = round(v.z / .02) * .02; sl.setdefault(k, []).append(v)
legs = {}
for k in sorted(sl):
    p = sl[k]; cx = sum(v.x for v in p) / len(p); cy = sum(v.y for v in p) / len(p)
    legs[f'{k:.2f}'] = dict(n=len(p), cx=round(cx, 3), cy=round(cy, 3), rx=round((max(v.x for v in p) - min(v.x for v in p)) / 2, 3), ry=round((max(v.y for v in p) - min(v.y for v in p)) / 2, 3))
out['leg_slices'] = legs
# head skin top (scalp) extent: body verts above 1.35
hv = [v for v in vs if v.z > 1.30]
out['head_bbox'] = bbox(hv)
# per-material vertex counts in body
from collections import Counter
cnt = Counter()
for p in me.polygons: cnt[me.materials[p.material_index].name] += 1
out['body_faces_by_mat'] = dict(cnt)
o, me, vs = verts('Shorts_Default'); out['shorts_hem_z_min'] = min(v.z for v in vs)
o, me, vs = verts('Shoes_Default')
out['shoes'] = {'bbox': bbox(vs), 'sock_top': max(v.z for v in vs)}
json.dump(out, open(sys.argv[sys.argv.index('--') + 1], 'w'), indent=1)
print('MEASURED')
