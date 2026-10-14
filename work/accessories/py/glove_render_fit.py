"""Overlay render of a cached hero inner fit (skin + glove outer), uncovered skin vertices in red. Blender -b --python glove_render_fit.py -- <Male|Female> <prefix>"""
import sys, os
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy
import acc_common as A, glove_sdf as S, glove_shell as SH, glove_view as GV, glove_fit as FT
sex = A.args_after_dashes()[0]; prefix = A.args_after_dashes()[1] if len(A.args_after_dashes()) > 1 else 'hf'
hs = FT.HeroSkin(sex)
d = np.load(os.path.join(A.ROOT, f'products/glove_hero_{sex}.npz')); Vh, T = d['Vh'], d['T']
nrm = S.vertex_normals(Vh, T); Oh = Vh + nrm * SH.T_BASE
bad, nc = FT.uncovered(hs, Oh, T)
print('uncovered', len(bad), 'of', nc)
A.reset()
skin = A.mesh_object('skin', hs.P, hs.T); skin.data.materials.append(A.material('skin', A.srgb('C08060'), .55))
gl = A.mesh_object('glove', Oh, T); gl.data.materials.append(A.material('g', A.srgb('F1F0EC'), .55, .3))
mred = A.material('red', (1, 0, 0), .5)
for i in bad:
    bpy.ops.mesh.primitive_uv_sphere_add(radius=.0012, location=tuple(hs.P[i]), segments=6, ring_count=4); bpy.context.active_object.data.materials.append(mred)
GV.setup((800, 800))
out = os.path.join(A.ROOT, 'proof/glove/scratch')
for nm, dirv, up in (('back', (0, 0, 1), (0, 1, 0)), ('palm', (0, 0, -1), (0, 1, 0)), ('thumbside', (1, 0, 0), (0, 0, 1)), ('little', (-1, 0, 0), (0, 0, 1))):
    GV.shoot(os.path.join(out, f'{prefix}_{sex}_{nm}.png'), (0, .07, 0), dirv, dist=.6, lens=70, up=up)
