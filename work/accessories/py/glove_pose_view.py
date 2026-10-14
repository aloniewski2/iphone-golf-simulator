"""Render a posed frame: skin hand/forearm region + glove outer wall; poking skin vertices red. Blender -b --python glove_pose_view.py -- <sex> <clip> <frame> <outprefix>"""
import sys, os, json
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, glove_pose as GPZ, glove_view as GV, glove_fit as FT
sex, clip, fr, prefix = A.args_after_dashes()[:4]; fr = int(fr)
pc = GPZ.PoseCheck(sex, os.environ.get('SIDE', 'Left'))
bodyf, bones = pc.h.clip(clip)
r = pc.frame(bodyf[fr], bones[fr])
gp = A.unity_to_N(r['gp'])                               # world (unity) -> N for viewing
bp = A.unity_to_N(bodyf[fr].astype(np.float64))
hs = pc.hs
A.reset()
# the unity->N map is a reflection (det -1): flip the winding of everything we draw
reg_tri = A.flip_tris(hs.hd.body_tri[np.isin(hs.hd.body_tri, hs.ids).all(1)]) if False else None
skin_tri = pc.region_tri
skin = A.mesh_object('skin', bp, A.flip_tris(skin_tri)); skin.data.materials.append(A.material('skin', A.srgb('C08060'), .55))
gl = A.mesh_object('glove', gp, A.flip_tris(A.flip_tris(pc.outer_tri))) if False else A.mesh_object('glove', gp, pc.outer_tri)
gl.data.materials.append(A.material('g', A.srgb('F1F0EC'), .55, .3))
red = A.material('red', (1, 0, 0), .5)
for v in r['poke']:
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.0011, location=tuple(bp[v]), segments=6, ring_count=4); bpy.context.active_object.data.materials.append(red)
GV.setup((900, 900))
out = os.path.join(A.ROOT, 'proof/glove')
os.makedirs(out + '/pose', exist_ok=True)
c = bp[hs.ids].mean(0)
tgt = tuple(A.unity_to_N(bodyf[fr][hs.ids[hs.P[:, 1] > 0.02][0]].astype(np.float64)))
hand = np.array(tgt)
if os.environ.get('FINGERS'):
    sel = hs.ids[hs.P[:, 1] > 0.12]
    hand = bp[sel].mean(0)
print('poke count', len(r['poke']), 'max depth mm', max(r['depth']) * 1000 if r['depth'] else 0)
for nm, dirv in (('a', (1, -.3, .5)), ('b', (-1, -.3, .5)), ('c', (0, -1, -.3)), ('d', (0, 0.5, 1))):
    GV.shoot(os.path.join(out, f'pose/{prefix}_{nm}.png'), tuple(hand), dirv, dist=float(os.environ.get('DIST', .35)), lens=70, up=(0, 0, 1))
