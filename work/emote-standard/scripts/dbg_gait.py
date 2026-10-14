import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, bpy, ek, importlib, gait
H = ek.Hero('male'); H.prop='none'
N, pose = gait.build(H, gait.RUN, True)
for f in range(0, 20, 2):
    pose(f); bpy.context.view_layer.update()
    out=[]
    for s, pre in (('r','Right'),('l','Left')):
        tgt = np.array(H.ctrl['leg_'+s][0].matrix_world.translation); a = np.array(H.world_mat(pre+'Foot').translation)
        out.append(f"{s} tgt f{ek.to_char(tgt)[1]:+.2f} u{tgt[2]:.2f} act u{a[2]:.2f} miss {np.linalg.norm(a-tgt)*1000:.0f}mm")
    print(f, ' | '.join(out), 'hips', round(H.world_mat('Hips').translation.z,3))
