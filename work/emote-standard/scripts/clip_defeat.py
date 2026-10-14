"""Emote_Defeat - drop to the knees (a lost match / round).

Beats (30 fps): stunned: shoulders sag, head drops, the left hand lets go (0-6) -> the legs give: up on the toes, both knees drop
forward onto the court (6-17, accelerating into the knees) -> the trunk folds over, head hangs, left hand on the thigh, the racket
head rests on the court beside the right knee (17-24) -> long sigh + slow head shake (24-62) -> pushes back up onto the feet (64-84)
-> READY (84-99).  Golf / menu: same body, open limp hands."""
import math

import numpy as np

import std
from ek import to_char, cvec

NAME = 'Emote_Defeat'
N = 100
KNEEL_Z = 0.082          # knee joint centre above the court when the kneecap touches


def build(H):
    T = std.Tracks(H, N)
    ka, ks = H.k, H.leg_len / 0.808
    S = lambda v: np.array(v) * ka
    wl0 = to_char(H.wrist_l0 - H.chest0)
    wr0 = to_char(H.wrist_r0 - H.chest0)
    pl0 = np.array(to_char(np.array(H.ctrl['arm_l'][1].location)))
    pr0 = np.array(to_char(np.array(H.ctrl['arm_r'][1].location)))
    kn0, pa0 = H.ready_hand_axes('l')
    sh0, fa0 = H.ready_racket_axes()
    Z = (0, 0, 0)
    b = H.rig.data.bones
    L1, L2 = b['RightUpperLeg'].length, b['RightLowerLeg'].length
    # kneeling geometry in the sagittal plane: the foot stays put, up on its toes; knee on the court in front of the ankle; thigh leans back 12 deg
    heel_k = 68.0
    A = H.heel_lift_ankle('r', heel_k)
    A_f, A_z = to_char(A)[1], A[2]
    K_f = A_f + math.sqrt(max(L2 ** 2 - (A_z - KNEEL_Z) ** 2, 0.0)) * 0.97
    phi = math.radians(12)
    hip_j = np.array(H.W['RightUpperLeg'].translation)
    lat = abs(hip_j[0] - A[0]) * 0.5
    reach = math.sqrt(max(L1 ** 2 - lat ** 2, 0.0))
    H_f = K_f - reach * math.sin(phi)
    H_z = KNEEL_Z + reach * math.cos(phi)
    dz_k, df_k = H_z - hip_j[2], H_f - to_char(hip_j)[1]
    H.info = dict(kneel_dz=dz_k, kneel_df=df_k, knee_f=K_f, ankle_z=A_z)
    # hips: stunned sag -> drop (accelerating, lands on the knees at 17) -> settle -> sigh -> push back up
    T['hp'] = T.k3([(0, Z), (6, (0, 0.0, -0.03)), (17, (0.0, df_k, dz_k)), (20, (0.0, df_k - 0.01, dz_k - 0.012)), (24, (0.0, df_k - 0.02, dz_k - 0.006)),
                    (34, (0.0, df_k - 0.02, dz_k)), (46, (0.0, df_k - 0.02, dz_k - 0.008)), (62, (0.0, df_k - 0.02, dz_k - 0.006)),
                    (68, (0.0, df_k * 0.9, dz_k * 0.92)), (80, (0, 0.02, 0.006)), (85, Z), (N - 1, Z)])
    T.impact('hp', 9, 17, power=1.8)
    T['hb'] = T.k3([(0, Z), (6, (4, 0, 0)), (17, (-6, 0, 0)), (22, (10, 0, 0)), (34, (6, 0, 0)), (46, (12, 0, 0)), (62, (10, 0, 0)), (72, (16, 0, 0)),
                    (84, Z), (N - 1, Z)])
    T.torso([(0, Z), (6, (8, 0, 0)), (17, (2, 0, 0)), (22, (24, 0, 0)), (34, (14, 0, 0)), (46, (28, 0, 0)), (62, (26, 0, 0)), (72, (20, 0, 0)),
             (84, (0, 0, 0)), (N - 1, Z)], lags=(0, 2, 3))
    T.neckhead([(0, Z), (6, (24, 0, 0)), (17, (14, 0, 0)), (22, (40, 0, 0)), (34, (30, 0, 0)), (40, (42, 0, 9)), (47, (44, 0, -9)), (54, (42, 0, 6)),
                (62, (40, 0, 0)), (72, (20, 0, 0)), (86, Z), (N - 1, Z)], lags=(2, 4))
    for s, lag in (('r', 0), ('l', 2)):
        T['cl_' + s] = T.k3([(0, Z), (6, (-8, 6, 0)), (22, (-10, 12, 0)), (34, (2, 4, 0)), (46, (-12, 12, 0)), (62, (-10, 10, 0)), (80, Z), (N - 1, Z)],
                             lag=lag)[:, :2]
    # arms: limp; left hand to the left thigh, the racket head down on the court beside the right knee
    T['wl'] = T.k3([(0, wl0), (4, wl0 + [-0.06, -0.05, -0.02]), (12, S([-0.22, 0.10, -0.46])), (22, S([-0.20, 0.30, -0.44])), (62, S([-0.20, 0.31, -0.44])),
                    (72, S([-0.22, 0.30, -0.42])), (82, S([-0.12, 0.22, -0.35])), (88, wl0), (N - 1, wl0)])
    T['wr'] = T.k3([(0, wr0), (4, wr0), (12, S([0.23, 0.08, -0.46])), (22, S([0.27, 0.22, -0.40])), (62, S([0.27, 0.23, -0.40])), (72, S([0.27, 0.22, -0.39])),
                    (82, S([0.15, 0.12, -0.40])), (88, wr0), (N - 1, wr0)])
    T['pl'] = T.k3([(0, pl0), (12, S([-0.5, -0.4, -0.1])), (72, S([-0.5, -0.4, -0.1])), (86, pl0), (N - 1, pl0)])
    T['pr'] = T.k3([(0, pr0), (12, S([0.5, -0.4, -0.1])), (72, S([0.5, -0.4, -0.1])), (86, pr0), (N - 1, pr0)])
    T['kn_l'] = T.k3([(0, kn0), (4, kn0), (12, (0.1, 0.1, -0.99)), (22, (0.15, 0.6, -0.78)), (72, (0.15, 0.6, -0.78)), (82, (0.3, 0.6, -0.4)), (88, kn0), (N - 1, kn0)])
    T['pa_l'] = T.k3([(0, pa0), (4, pa0), (12, (1.0, -0.1, 0.0)), (22, (0.2, -0.3, -0.93)), (72, (0.2, -0.3, -0.93)), (88, pa0), (N - 1, pa0)])
    T['shaft'] = T.k3([(0, sh0), (4, sh0), (12, (0.40, 0.60, -0.55)), (22, (0.5, 0.70, 0.46)), (72, (0.5, 0.70, 0.46)), (82, (0.3, 0.6, -0.5)),
                       (88, sh0), (N - 1, sh0)])
    T['face'] = T.k3([(0, fa0), (4, fa0), (12, (-0.95, 0.2, 0.1)), (22, (-0.8, 0.4, 0.3)), (72, (-0.8, 0.4, 0.3)), (88, fa0), (N - 1, fa0)])
    T['curl_l'] = T.k1([(0, 0), (4, 0), (12, -0.6), (82, -0.6), (88, 0), (N - 1, 0)])
    T['curl_r_golf'] = T.k1([(0, 0), (4, 0), (12, -0.6), (82, -0.6), (88, 0), (N - 1, 0)])
    # feet stay put and roll up onto the toes; knees forward/down onto the court
    for s in 'rl':
        T['heel_' + s] = T.k1([(0, 0), (6, 0), (15, heel_k), (66, heel_k), (78, 20), (84, 0), (N - 1, 0)])
        T['kp_' + s] = T.k3([(0, Z), (6, Z), (15, (0, 0.3, -0.6)), (66, (0, 0.3, -0.6)), (82, Z), (N - 1, Z)])
    return N, std.pose_fn(H, T, golf=getattr(H, 'prop', 'racket') == 'none')
