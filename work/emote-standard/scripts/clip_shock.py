"""Emote_Shock - "no way!" hands on head (shock: a whiff, a lip-out, a net cord).

Beats (30 fps): startle (0-3): shoulders jump, head snaps back -> both hands fly to the head, elbows flared wide, the right foot staggers
back, trunk leans back, eyes to the sky (3-11) -> disbelief hold with a slow head shake, the hands riding the head (11-40)
-> hands come down, the right foot steps back into place, READY (40-62).  Tennis: the racket is held up behind the head.
Golf: open hands flat on the head."""
import numpy as np

import std
from ek import to_char

NAME = 'Emote_Shock'
N = 66


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
    T['hp'] = T.k3([(0, Z), (3, (0, 0.0, 0.035)), (8, (0.0, -0.07, 0.01)), (11, (0.0, -0.08, 0.0)), (25, (0.0, -0.09, -0.005)), (40, (0.0, -0.085, 0.0)),
                    (50, (0, -0.04, 0.0)), (57, (0, 0, 0.003)), (61, Z), (N - 1, Z)])
    T['hb'] = T.k3([(0, Z), (3, (-4, 0, 0)), (9, (-8, 0, 0)), (40, (-7, 0, 0)), (54, Z), (N - 1, Z)])
    T.torso([(0, Z), (3, (-6, 0, 0)), (9, (-12, 0, 0)), (11, (-13, 0, 0)), (24, (-11, 0, 0)), (40, (-12, 0, 0)), (46, (2, 0, 0)), (56, Z), (N - 1, Z)])
    T.neckhead([(0, Z), (3, (-14, 0, 0)), (9, (-18, 0, 0)), (11, (-16, 0, 0)), (16, (-14, 0, 11)), (23, (-15, 0, -11)), (30, (-14, 0, 9)), (37, (-15, 0, -5)),
                (40, (-12, 0, 0)), (46, (4, 0, 0)), (56, Z), (N - 1, Z)])
    for s, lag in (('r', 0), ('l', 1)):
        T['cl_' + s] = T.k3([(0, Z), (3, (14, 0, 0)), (9, (16, -6, 0)), (40, (14, -6, 0)), (48, (2, 0, 0)), (56, Z), (N - 1, Z)], lag=lag)[:, :2]
    # hands on the head (head frame: from the base of the skull, READY-relative axes); the weights blend them in and out
    T['wl_head'] = T.k3([(0, (-0.10, 0.03, 0.16)), (N - 1, (-0.10, 0.03, 0.16))])
    T['wr_head'] = T.k3([(0, (0.10, -0.01, 0.15)), (N - 1, (0.10, -0.01, 0.15))])
    T['wl_hw'] = T.k1([(0, 0), (4, 0), (9, 1), (40, 1), (46, 0), (N - 1, 0)])
    T['wr_hw'] = T.k1([(0, 0), (3, 0), (8, 1), (40, 1), (45, 0), (N - 1, 0)])
    T['wl'] = T.k3([(0, wl0), (3, wl0 + [-0.08, -0.04, 0.06]), (6, S([-0.32, 0.12, 0.22])), (40, S([-0.32, 0.12, 0.22])), (46, S([-0.25, 0.15, -0.25])),
                    (54, S([-0.08, 0.25, -0.31])), (58, wl0), (N - 1, wl0)])
    T['wr'] = T.k3([(0, wr0), (3, wr0 + [0.06, -0.02, 0.08]), (6, S([0.32, 0.08, 0.20])), (40, S([0.32, 0.08, 0.20])), (46, S([0.25, 0.10, -0.25])),
                    (54, S([0.12, 0.08, -0.33])), (58, wr0), (N - 1, wr0)])
    T['pl'] = T.k3([(0, pl0), (6, S([-0.8, 0.0, 0.25])), (40, S([-0.8, 0.0, 0.25])), (50, pl0), (N - 1, pl0)])
    T['pr'] = T.k3([(0, pr0), (6, S([0.8, -0.1, 0.25])), (40, S([0.8, -0.1, 0.25])), (50, pr0), (N - 1, pr0)])
    T['kn_l'] = T.k3([(0, kn0), (3, kn0), (8, (0.75, 0.25, -0.15)), (40, (0.75, 0.25, -0.15)), (48, (0.3, 0.5, -0.6)), (56, kn0), (N - 1, kn0)])
    T['pa_l'] = T.k3([(0, pa0), (3, pa0), (8, (0.15, 0.0, -1.0)), (40, (0.15, 0.0, -1.0)), (48, (0.9, 0.0, 0.2)), (56, pa0), (N - 1, pa0)])
    T['shaft'] = T.k3([(0, sh0), (2, sh0), (4, (0.75, 0.1, 0.65)), (8, (0.15, -0.45, 0.88)), (40, (0.15, -0.45, 0.88)), (44, (0.75, 0.1, 0.65)), (49, (0.35, 0.6, 0.6)), (56, sh0), (N - 1, sh0)])
    T['face'] = T.k3([(0, fa0), (3, fa0), (8, (-0.95, 0.0, 0.2)), (40, (-0.95, 0.0, 0.2)), (56, fa0), (N - 1, fa0)])
    T['kn_r'] = T.k3([(0, (-0.75, 0.25, -0.15)), (N - 1, (-0.75, 0.25, -0.15))])
    T['pa_r'] = T.k3([(0, (-0.15, 0.0, -1.0)), (N - 1, (-0.15, 0.0, -1.0))])
    T['curl_l'] = T.k1([(0, 0), (3, 0), (8, -0.85), (42, -0.85), (52, 0), (N - 1, 0)])
    T['curl_r_golf'] = T.k1([(0, 0), (3, 0), (8, -0.85), (42, -0.85), (52, 0), (N - 1, 0)])
    # stagger: the right foot steps back, then back into place
    T['ank_r'] = T.k3([(0, Z), (3, Z), (6, (0.01, -0.10, 0.07)), (9, (0.02, -0.20, 0.0)), (46, (0.02, -0.20, 0.0)), (50, (0.01, -0.10, 0.07)), (53, Z), (N - 1, Z)])
    T['heel_r'] = T.k1([(0, 0), (3, 0), (6, 10), (9, 0), (46, 0), (50, 10), (53, 0), (N - 1, 0)])
    T['heel_l'] = T.k1([(0, 0), (2, 0), (4, 12), (8, 0), (N - 1, 0)])
    return N, std.pose_fn(H, T, golf=getattr(H, 'prop', 'racket') == 'none')
