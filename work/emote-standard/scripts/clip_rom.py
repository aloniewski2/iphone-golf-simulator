"""ROM_Standard - the range-of-motion test clip of the ANIMATION STANDARD (work/anim-standard/STANDARD.md).

Every pose an animation is ALLOWED to use is somewhere in this clip; garments must pass the audit on it (and their correctives are fitted to it),
animations must stay inside it. Empty hands (relaxed), no racket.  ~17.5 s at 30 fps.
  segment                       frames      what
  settle                        0-30        READY -> standing, arms hanging
  arms forward to overhead      30-110      shoulder flexion 0 -> 175 -> 0
  arms sideways to overhead     110-190     abduction 0 -> 175 -> 0
  arms behind                   190-240     extension 0 -> 55 -> 0
  hands on head                 240-300     both palms on the crown, elbows wide
  arms crossed                  300-350     horizontal adduction, forearms crossed at the chest
  hands on hips                 350-400     fists on the iliac crest, elbows out
  trunk forward / back          400-450     bend 50 forward, 20 back
  trunk side / twist            450-510     side bend +-25, twist +-40
  squat                         510-550     deep squat, arms forward
  high knees                    550-590     right then left knee to hip height
  kneel                         590-650     both knees down, up again (the Defeat pose)
  back to READY                 650-680"""
import math

import numpy as np

import std
from ek import to_char

NAME = 'ROM_Standard'
N = 681


def build(H):
    T = std.Tracks(H, N)
    ka, ks = H.k, H.leg_len / 0.808
    Z = (0, 0, 0)
    wl0, wr0 = to_char(H.wrist_l0 - H.chest0), to_char(H.wrist_r0 - H.chest0)
    pl0 = np.array(to_char(np.array(H.ctrl['arm_l'][1].location)))
    pr0 = np.array(to_char(np.array(H.ctrl['arm_r'][1].location)))
    kn0l, pa0l = H.ready_hand_axes('l')
    kn0r, pa0r = H.ready_hand_axes('r')
    L = 0.94 * H.arm_len

    def arm(side, elev, azim, reach=1.0, lift=0.0):
        """wrist in chest space from the shoulder: elev = degrees up from hanging (0 down, 90 horizontal, 180 up), azim = 0 forward, 90 out to the side,
        -50 behind the body plane"""
        s = 1 if side == 'r' else -1
        e, a = math.radians(elev), math.radians(azim)
        d = np.array([math.sin(e) * math.sin(a) * s, math.sin(e) * math.cos(a), -math.cos(e)])
        sh = np.array([0.198 * s * ka, 0.0, 0.03 * lift])
        return sh + d * L * reach

    def aim(side, elev, azim):
        """knuckles along the arm, palm facing the body's midline (thumb forward) - a neutral wrist"""
        s = 1 if side == 'r' else -1
        e, a = math.radians(elev), math.radians(azim)
        kn = np.array([math.sin(e) * math.sin(a) * s, math.sin(e) * math.cos(a), -math.cos(e)])
        med = np.array([-s, 0.0, 0.0])
        pa = med - (med @ kn) * kn
        if np.linalg.norm(pa) < 1e-3:
            pa = np.array([0.0, -1.0, 0.0])
        return kn, pa / np.linalg.norm(pa)

    # arm key table: (frame, elev, azim, reach, clavicle elevation)
    seq = [(0, None), (30, (8, 20, 1.0, 0)),
           (70, (175, 5, 1.08, 28)), (110, (8, 20, 1.0, 0)),
           (150, (175, 88, 1.08, 30)), (190, (8, 20, 1.0, 0)),
           (215, (55, -60, 1.0, -4)), (240, (8, 20, 1.0, 0)),
           (300, (8, 20, 1.0, 0)), (350, (8, 20, 1.0, 0)), (400, (8, 20, 1.0, 0)),
           (510, (8, 20, 1.0, 0)), (530, (90, 10, 1.0, 6)), (550, (8, 20, 1.0, 0)),
           (650, (8, 20, 1.0, 0)), (N - 1, None)]
    keysW = {'r': [], 'l': []}
    keysK = {'r': [], 'l': []}
    keysP = {'r': [], 'l': []}
    keysC = {'r': [], 'l': []}
    for f, v in seq:
        for s in 'rl':
            if v is None:
                keysW[s].append((f, wr0 if s == 'r' else wl0))
                kn, pa = (kn0r, pa0r) if s == 'r' else (kn0l, pa0l)
                keysK[s].append((f, kn)); keysP[s].append((f, pa))
                keysC[s].append((f, (0, 0, 0)))
                continue
            e, a, rch, cl = v
            keysW[s].append((f, arm(s, e, a, rch, cl / 30)))
            kn, pa = aim(s, e, a)
            keysK[s].append((f, kn)); keysP[s].append((f, pa))
            keysC[s].append((f, (cl, 0, 0)))
    # hands on head (head frame), crossed arms, hands on hips (chest space)
    for s in 'rl':
        sg = 1 if s == 'r' else -1
        hw = [(0, 0), (240, 0), (258, 1), (282, 1), (300, 0), (N - 1, 0)]
        T['w%s_hw' % s] = T.k1(hw)
        T['w%s_head' % s] = T.k3([(0, (0.10 * sg, -0.01, 0.15)), (N - 1, (0.10 * sg, -0.01, 0.15))])
        keysW[s] += [(258, arm(s, 150, 70, 0.7, 20)), (282, arm(s, 150, 70, 0.7, 20)),
                     (318, np.array([-0.12 * sg, 0.22, -0.18]) * ka), (332, np.array([-0.12 * sg, 0.22, -0.18]) * ka),
                     (368, np.array([0.17 * sg, 0.02, -0.46]) * ka), (382, np.array([0.17 * sg, 0.02, -0.46]) * ka)]
        keysK[s] += [(258, (-0.6 * sg, 0.3, -0.3)), (282, (-0.6 * sg, 0.3, -0.3)), (318, (-0.9 * sg, 0.3, 0.2)), (332, (-0.9 * sg, 0.3, 0.2)),
                     (368, (-0.2 * sg, 0.6, -0.75)), (382, (-0.2 * sg, 0.6, -0.75))]
        keysP[s] += [(258, (-0.1 * sg, 0.0, -1.0)), (282, (-0.1 * sg, 0.0, -1.0)), (318, (0.0, -1.0, 0.0)), (332, (0.0, -1.0, 0.0)),
                     (368, (-1.0 * sg, 0.0, 0.0)), (382, (-1.0 * sg, 0.0, 0.0))]
        keysC[s] += [(258, (20, -6, 0)), (282, (20, -6, 0)), (318, (0, 14, 0)), (332, (0, 14, 0)), (368, (2, -4, 0)), (382, (2, -4, 0))]
        T['w' + s] = T.k3(sorted(keysW[s], key=lambda k: k[0]))
        T['kn_' + s] = T.k3(sorted(keysK[s], key=lambda k: k[0]))
        T['pa_' + s] = T.k3(sorted(keysP[s], key=lambda k: k[0]))
        T['cl_' + s] = T.k3(sorted(keysC[s], key=lambda k: k[0]))[:, :2]
        pole = [(0, pr0 if s == 'r' else pl0), (30, np.array([0.45 * sg, -0.45, -0.2]) * ka), (70, np.array([0.6 * sg, -0.35, 0.3]) * ka),
                (150, np.array([0.3 * sg, -0.5, 0.4]) * ka), (215, np.array([0.5 * sg, -0.3, -0.3]) * ka), (258, np.array([0.8 * sg, 0.0, 0.25]) * ka),
                (318, np.array([0.5 * sg, -0.3, -0.5]) * ka), (368, np.array([0.8 * sg, -0.2, -0.2]) * ka), (530, np.array([0.5 * sg, -0.4, -0.3]) * ka),
                (650, np.array([0.45 * sg, -0.45, -0.2]) * ka), (N - 1, pr0 if s == 'r' else pl0)]
        T['p' + s] = T.k3(pole)
    T['curl_l'] = T.k1([(0, 0), (30, -0.6), (650, -0.6), (N - 1, 0)])
    T['curl_r_golf'] = T.k1([(0, 0), (30, -0.6), (650, -0.6), (N - 1, 0)])
    # trunk + hips
    T['hb'] = T.k3([(0, Z), (30, (-3, 0, 0)), (400, (-3, 0, 0)), (425, (25, 0, 0)), (440, (-8, 0, 0)), (450, (-3, 0, 0)), (465, (-3, 12, 0)), (475, (-3, -12, 0)),
                    (485, (-3, 0, 0)), (497, (-3, 0, 15)), (507, (-3, 0, -15)), (510, (-3, 0, 0)), (530, (35, 0, 0)), (550, (-3, 0, 0)), (N - 1, Z)])
    T.torso([(0, Z), (30, (-6, 0, 0)), (400, (-6, 0, 0)), (425, (30, 0, 0)), (440, (-18, 0, 0)), (450, (-6, 0, 0)), (465, (-6, 25, 0)), (475, (-6, -25, 0)),
             (485, (-6, 0, 0)), (497, (-6, 0, 40)), (507, (-6, 0, -40)), (510, (-6, 0, 0)), (530, (10, 0, 0)), (550, (-6, 0, 0)), (590, (-6, 0, 0)),
             (615, (20, 0, 0)), (630, (20, 0, 0)), (650, (-6, 0, 0)), (N - 1, Z)])
    T.neckhead([(0, Z), (30, (6, 0, 0)), (70, (-10, 0, 0)), (110, (6, 0, 0)), (400, (6, 0, 0)), (425, (-20, 0, 0)), (440, (14, 0, 0)), (650, (6, 0, 0)), (N - 1, Z)])
    # hips height: stand up out of READY, squat, high knees, kneel
    from ek import cvec
    import math as _m
    b = H.rig.data.bones
    L1, L2 = b['RightUpperLeg'].length, b['RightLowerLeg'].length
    A = H.heel_lift_ankle('r', 68.0)
    A_f, A_z = to_char(A)[1], A[2]
    K_f = A_f + _m.sqrt(max(L2 ** 2 - (A_z - 0.082) ** 2, 0.0)) * 0.97
    hip_j = np.array(H.W['RightUpperLeg'].translation)
    lat = abs(hip_j[0] - A[0]) * 0.5
    reach = _m.sqrt(max(L1 ** 2 - lat ** 2, 0.0))
    H_f, H_z = K_f - reach * _m.sin(_m.radians(12)), 0.082 + reach * _m.cos(_m.radians(12))
    dz_k, df_k = H_z - hip_j[2], H_f - to_char(hip_j)[1]
    T['hp'] = T.k3([(0, Z), (30, (0, 0, 0.02 * ks)), (510, (0, 0, 0.02 * ks)), (530, (0, -0.10, -0.32 * ks)), (550, (0, 0, 0.02 * ks)),
                    (590, (0, 0, 0.02 * ks)), (605, (0, df_k, dz_k)), (635, (0, df_k, dz_k)), (650, (0, 0, 0.02 * ks)), (N - 1, Z)])
    T['ank_r'] = T.k3([(0, Z), (555, Z), (562, (0.0, 0.18, 0.40 * ks)), (569, Z), (N - 1, Z)])
    T['ank_l'] = T.k3([(0, Z), (572, Z), (579, (0.0, 0.18, 0.40 * ks)), (586, Z), (N - 1, Z)])
    for s in 'rl':
        T['kp_' + s] = T.k3([(0, Z), (590, Z), (603, (0, 0.3, -0.6)), (637, (0, 0.3, -0.6)), (650, Z), (N - 1, Z)])
        T['heel_' + s] = T.k1([(0, 0), (510, 0), (530, 15), (550, 0), (590, 0), (603, 68.0), (637, 68.0), (650, 0), (N - 1, 0)])
    return N, std.pose_fn(H, T, golf=True)
