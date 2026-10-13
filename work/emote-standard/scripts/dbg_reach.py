import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, bpy, ek, importlib
H = ek.Hero('male'); mod = importlib.import_module('clip_jumpjoy'); N, pose = mod.build(H)
for f in (0, 21, 25):
    pose(f); bpy.context.view_layer.update()
    for s, pre in (('l','Left'),('r','Right')):
        tgt = np.array(H.ctrl['arm_'+s][0].matrix_world.translation)
        w = np.array(H.world_mat(pre+'Hand').translation); sh = np.array(H.world_mat(pre+'UpperArm').translation)
        el = np.array(H.world_mat(pre+'LowerArm').translation)
        cs = np.array(H.cs.matrix_world.translation)
        print(f, s, 'tgt-sh %.3f' % np.linalg.norm(tgt-sh), 'wrist-sh %.3f' % np.linalg.norm(w-sh), 'miss %.3f' % np.linalg.norm(w-tgt), 'tgt char', ek.to_char(tgt-cs).round(3), 'cs', ek.to_char(cs).round(3))
