import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, bpy, mathutils as mu
import ek
tag = sys.argv[sys.argv.index('--') + 1]
H = ek.Hero(tag)
np.set_printoptions(precision=3, suppress=True)
print('POLE_ERR', {k: (round(v[0]*1000,2), v[1]) for k, v in H.pole_err.items()})
print('ROOT q', H.q_ready['Root'], 'hips loc', H.hips_loc_ready)
print('arm_len', H.arm_len, 'leg_len', H.leg_len, 'k', H.k)
print('chest0', H.chest0, 'hips0', H.hips0)
for s in 'lr':
    kn, pa = H.ready_hand_axes(s); print('hand', s, 'knuckle', kn, 'palm(+z)', pa)
sh, fa = H.ready_racket_axes(); print('racket shaft', sh, 'face', fa)
print('wrist_r cs', ek.to_char(H.wrist_r0 - H.chest0), 'wrist_l cs', ek.to_char(H.wrist_l0 - H.chest0))
print('sh_r cs', ek.to_char(H.sh_r - H.chest0), 'sh_l cs', ek.to_char(H.sh_l - H.chest0))
print('ankle_r', ek.to_char(H.ankle_r0), 'ball_r', ek.to_char(H.ball_r0), 'hipjoint_r', ek.to_char(np.array(H.W['RightUpperLeg'].translation)))
# palm side check: curl the left fingers fully, where do the tips go in the hand frame?
H.fingers('l', 1.0); bpy.context.view_layer.update()
W = H.world_mat('LeftHand'); tip = np.array(H.world_mat('LeftMiddleDistal').translation)
loc = np.array(W.to_3x3()).T @ (tip - np.array(W.translation)); print('fist middle-distal head in hand frame (x,y,z)', loc)
H.fingers('l', 0.0)
# heel lift sanity
for a in (0, 20, 40):
    print('heel', a, 'ankle', ek.to_char(H.heel_lift_ankle('r', a)))
# wrist angle at READY: hand Y vs forearm
for s, pre in (('l','Left'),('r','Right')):
    fa_ = np.array(H.W[pre+'Hand'].translation) - np.array(H.W[pre+'LowerArm'].translation)
    hy = np.array(H.W[pre+'Hand'].to_3x3().col[1])
    print('READY wrist bend', s, math.degrees(math.acos(np.clip(ek.unit(fa_) @ hy, -1, 1))))
# racket shaft vs hand axes
Rh = np.array(H.W['RightHand'].to_3x3()); print('racket shaft in hand frame', Rh.T @ H.rk_shaft0, 'face in hand frame', Rh.T @ H.rk_face0)
