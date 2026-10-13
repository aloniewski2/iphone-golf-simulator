"""Emote_Slump - dejected slump (sad: a lost point).

Beats (30 fps): the air goes out of him: head drops, shoulders round and fall, arms hang, the racket dangles to the court, weight
sinks onto one leg, the other knee softens (0-12) -> one big sigh: chest rises, then sinks lower than before (12-36) -> slow head shake
while staring at the court (36-56) -> hold -> straighten back to READY, head last (60-77).  Golf: open, limp hands."""
import numpy as np

import std
from ek import to_char

NAME = 'Emote_Slump'
N = 78


def build(H):
    T = std.Tracks(H, N)
    ka = H.k
    S = lambda v: np.array(v) * ka
    wl0 = to_char(H.wrist_l0 - H.chest0)
    wr0 = to_char(H.wrist_r0 - H.chest0)
    pl0 = np.array(to_char(np.array(H.ctrl['arm_l'][1].location)))
    pr0 = np.array(to_char(np.array(H.ctrl['arm_r'][1].location)))
    kn0, pa0 = H.ready_hand_axes('l')
    sh0, fa0 = H.ready_racket_axes()
    Z = (0, 0, 0)
    # stands up out of the tennis crouch (legs straighter, pelvis tucked under), the upper back rounds over
    T['hp'] = T.k3([(0, Z), (12, (0.03, -0.035, 0.012)), (24, (0.03, -0.03, 0.022)), (36, (0.035, -0.04, 0.006)), (56, (0.035, -0.04, 0.006)),
                    (64, (0.01, -0.01, 0.0)), (72, Z), (N - 1, Z)])
    T['hb'] = T.k3([(0, Z), (12, (-10, -5, -3)), (24, (-8, -4, -3)), (36, (-11, -6, -3)), (56, (-11, -6, -2)), (70, Z), (N - 1, Z)])
    T.torso([(0, Z), (12, (24, 0, 0)), (24, (10, 0, 0)), (36, (30, 0, 0)), (56, (29, 0, 0)), (68, (3, 0, 0)), (73, Z), (N - 1, Z)], lags=(0, 2, 4), split=(0.0, 0.35, 0.65))
    T['spine'] = T.k3([(0, Z), (12, (-4, 0, 0)), (56, (-4, 0, 0)), (70, Z), (N - 1, Z)])
    T.neckhead([(0, Z), (12, (36, 0, 0)), (24, (22, 0, 0)), (36, (42, 0, 0)), (40, (42, 0, 10)), (46, (43, 0, -10)), (52, (42, 0, 7)), (56, (42, 0, 0)),
                (66, (20, 0, 0)), (75, Z), (N - 1, Z)], lags=(2, 4))
    for s, lag in (('r', 0), ('l', 2)):
        T['cl_' + s] = T.k3([(0, Z), (12, (-8, 10, 0)), (24, (4, 2, 0)), (36, (-12, 12, 0)), (56, (-11, 12, 0)), (68, (0, 0, 0)), (N - 1, Z)], lag=lag)[:, :2]
    # limp arms: hang straight, the racket dangles head-down in front of the right foot
    T['wl'] = T.k3([(0, wl0), (3, wl0 + [-0.05, -0.05, -0.02]), (12, S([-0.21, 0.06, -0.48])), (24, S([-0.21, 0.04, -0.47])), (36, S([-0.20, 0.07, -0.49])),
                    (56, S([-0.20, 0.07, -0.49])), (66, S([-0.12, 0.2, -0.38])), (72, wl0), (N - 1, wl0)])
    T['wr'] = T.k3([(0, wr0), (3, wr0), (12, S([0.22, 0.07, -0.48])), (24, S([0.22, 0.05, -0.47])), (36, S([0.21, 0.08, -0.49])), (56, S([0.21, 0.08, -0.49])),
                    (66, S([0.15, 0.12, -0.42])), (72, wr0), (N - 1, wr0)])
    T['pl'] = T.k3([(0, pl0), (12, S([-0.4, -0.5, -0.1])), (56, S([-0.4, -0.5, -0.1])), (70, pl0), (N - 1, pl0)])
    T['pr'] = T.k3([(0, pr0), (12, S([0.4, -0.5, -0.1])), (56, S([0.4, -0.5, -0.1])), (70, pr0), (N - 1, pr0)])
    T['kn_l'] = T.k3([(0, kn0), (3, kn0), (12, (0.1, 0.1, -0.99)), (56, (0.1, 0.1, -0.99)), (66, (0.3, 0.5, -0.5)), (72, kn0), (N - 1, kn0)])
    T['pa_l'] = T.k3([(0, pa0), (3, pa0), (12, (1.0, -0.2, 0.0)), (56, (1.0, -0.2, 0.0)), (72, pa0), (N - 1, pa0)])
    T['shaft'] = T.k3([(0, sh0), (3, sh0), (12, (0.10, 0.42, -0.90)), (24, (0.10, 0.38, -0.92)), (36, (0.10, 0.45, -0.89)), (56, (0.10, 0.45, -0.89)),
                       (66, (0.15, 0.7, -0.5)), (72, sh0), (N - 1, sh0)])
    T['face'] = T.k3([(0, fa0), (3, fa0), (12, (-0.95, 0.2, 0.1)), (56, (-0.95, 0.2, 0.1)), (72, fa0), (N - 1, fa0)])
    T['curl_l'] = T.k1([(0, 0), (3, 0), (12, -0.7), (60, -0.7), (70, 0), (N - 1, 0)])
    T['curl_r_golf'] = T.k1([(0, 0), (3, 0), (12, -0.7), (60, -0.7), (70, 0), (N - 1, 0)])
    # weight sinks onto the right leg, the left knee softens
    T['ank_l'] = T.k3([(0, Z), (12, (0.0, 0.03, 0.0)), (60, (0.0, 0.03, 0.0)), (70, Z), (N - 1, Z)])
    T['heel_l'] = T.k1([(0, 0), (12, 10), (60, 10), (70, 0), (N - 1, 0)])
    return N, std.pose_fn(H, T, golf=getattr(H, 'prop', 'racket') == 'none')
