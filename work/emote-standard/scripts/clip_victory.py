"""Emote_Victory - spin jump and roar (triumph: a won match / round).

Beats (30 fps): wind-up dip with the trunk and both arms coiled to the right (0-10) -> push (10-15) -> airborne full 360 turn to the left,
arms pulled in, knees tucked (15-~30, real gravity) -> lands square to the net and EXPLODES into a wide power stance, both fists thrown
up in a V, chest out, head back (roar) -> shaking fists held high, two upward punches (40-70) -> back to READY (70-95).
Golf / menu: same body, empty fists."""
import math

import numpy as np

import std
from ek import to_char

NAME = 'Emote_Victory'
N = 96
DIP_END, PUSH_END = 10, 15


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
    dz, info = std.jump_profile(H, N, DIP_END, PUSH_END, 0.15 * ks, -0.16 * ks,
                                after=[(5, -0.06 * ks), (10, -0.085 * ks), (40, -0.08 * ks), (52, 0.004), (58, 0.0)])
    H.info = info
    ft, fb = info['f_td'], info['f_bot']
    T['hp'] = np.zeros((N, 3))
    T['hp'][:, 2] = dz
    T['hp'][:, 1] = T.k1([(0, 0), (9, -0.04), (PUSH_END, 0.02), (ft, 0.0), (fb, -0.03), (fb + 8, 0.0), (N - 1, 0)]) * ks
    # the spin: smooth through the flight, finished a frame before touch-down
    yaw = np.zeros(N)
    for i in range(N):
        u = (i - PUSH_END + 1) / (ft - 1 - PUSH_END + 1)
        u = min(max(u, 0.0), 1.0)
        yaw[i] = -360.0 * u * u * (3 - 2 * u)
    T['yaw'] = yaw
    T['hb'] = T.k3([(0, Z), (9, (10, 0, 14)), (PUSH_END, (-4, 0, 4)), (22, (-6, 0, 0)), (ft, (0, 0, 0)), (fb, (6, 0, 0)), (fb + 5, (-8, 0, 0)),
                    (fb + 35, (-6, 0, 0)), (N - 8, Z), (N - 1, Z)])
    T.torso([(0, Z), (9, (9, 0, 22)), (12, (6, 0, 14)), (PUSH_END, (-2, 0, 0)), (22, (2, 0, -8)), (ft, (-2, 0, 0)), (fb, (12, 0, 0)), (fb + 4, (-14, 0, 0)),
             (fb + 8, (-16, 0, 0)), (fb + 36, (-14, 0, 0)), (N - 10, (0, 0, 0)), (N - 1, Z)])
    T.neckhead([(0, Z), (9, (-6, 0, -10)), (PUSH_END, (-8, 0, 0)), (22, (4, 0, 8)), (ft, (0, 0, 0)), (fb, (10, 0, 0)), (fb + 4, (-18, 0, 0)),
                (fb + 8, (-24, 0, 0)), (fb + 22, (-22, 4, 0)), (fb + 36, (-20, -3, 0)), (N - 10, Z), (N - 1, Z)])
    for s, lag in (('r', 0), ('l', 1)):
        T['cl_' + s] = T.k3([(0, Z), (9, (-2, -6, 0)), (PUSH_END, (6, 4, 0)), (22, (-2, 8, 0)), (ft, (0, 0, 0)), (fb + 4, (16, -4, 0)), (fb + 36, (14, -2, 0)),
                              (N - 10, Z), (N - 1, Z)], lag=lag)[:, :2]
    # arms: coil right, tucked through the spin, then a big V with shaking fists and two punches to the sky
    V_r, V_l = S([0.47, 0.08, 0.47]), S([-0.47, 0.08, 0.47])
    shake = lambda base, k: base + np.array([0, 0, 0.022 * ka * (1 if k % 2 == 0 else -1)])
    rk = [(0, wr0), (3, wr0 + [0.04, -0.02, 0.0]), (9, S([0.40, -0.18, -0.28])), (PUSH_END, S([0.22, 0.20, -0.06])), (18, S([0.13, 0.17, -0.12])),
          (ft - 2, S([0.13, 0.17, -0.12])), (ft, S([0.30, 0.15, 0.10])), (fb + 3, V_r)]
    lk = [(0, wl0), (3, wl0 + [-0.05, -0.05, 0.0]), (9, S([0.10, 0.18, -0.22])), (PUSH_END, S([-0.22, 0.20, -0.06])), (18, S([-0.13, 0.17, -0.12])),
          (ft - 2, S([-0.13, 0.17, -0.12])), (ft, S([-0.30, 0.15, 0.10])), (fb + 3, V_l)]
    t0 = int(fb) + 6
    for k, f in enumerate(range(t0, t0 + 18, 3)):
        rk.append((f, shake(V_r, k)))
        lk.append((f + 1, shake(V_l, k)))
    p1, p2 = t0 + 20, t0 + 28
    rk += [(p1 - 3, S([0.38, 0.12, 0.25])), (p1, S([0.43, 0.06, 0.52])), (p2 - 3, S([0.38, 0.12, 0.25])), (p2, S([0.43, 0.06, 0.52])),
           (N - 16, S([0.30, 0.15, -0.10])), (N - 7, wr0), (N - 1, wr0)]
    lk += [(p1 - 2, S([-0.38, 0.12, 0.25])), (p1 + 1, S([-0.43, 0.06, 0.52])), (p2 - 2, S([-0.38, 0.12, 0.25])), (p2 + 1, S([-0.43, 0.06, 0.52])),
           (N - 15, S([-0.20, 0.25, -0.20])), (N - 7, wl0), (N - 1, wl0)]
    T['wr'], T['wl'] = T.k3(rk), T.k3(lk)
    T['pr'] = T.k3([(0, pr0), (9, S([0.5, -0.5, -0.2])), (PUSH_END, S([0.4, -0.3, -0.5])), (ft, S([0.4, -0.3, -0.5])), (fb + 3, S([0.85, -0.2, 0.15])),
                    (N - 12, S([0.85, -0.2, 0.15])), (N - 5, pr0), (N - 1, pr0)])
    T['pl'] = T.k3([(0, pl0), (9, S([-0.3, -0.4, -0.4])), (PUSH_END, S([-0.4, -0.3, -0.5])), (ft, S([-0.4, -0.3, -0.5])), (fb + 3, S([-0.85, -0.2, 0.15])),
                    (N - 12, S([-0.85, -0.2, 0.15])), (N - 5, pl0), (N - 1, pl0)])
    T['shaft'] = T.k3([(0, sh0), (3, sh0), (9, (0.55, -0.55, -0.62)), (PUSH_END, (0.45, 0.35, 0.82)), (18, (0.45, 0.15, 0.88)), (ft - 2, (0.45, 0.15, 0.88)),
                       (fb + 3, (0.30, -0.10, 0.95)), (N - 16, (0.35, 0.3, 0.88)), (N - 6, sh0), (N - 1, sh0)])
    T['face'] = T.k3([(0, fa0), (3, fa0), (9, (-0.8, 0.0, 0.3)), (PUSH_END, (-0.9, 0.3, 0.0)), (fb + 3, (-0.3, 0.95, 0.0)), (N - 16, (-0.6, 0.6, 0.2)),
                      (N - 6, fa0), (N - 1, fa0)])
    T['kn_l'] = T.k3([(0, kn0), (3, kn0), (9, (0.6, 0.3, -0.7)), (PUSH_END, (-0.2, 0.3, 0.9)), (ft, (-0.2, 0.3, 0.9)), (fb + 3, (-0.45, 0.05, 0.89)),
                      (N - 12, (-0.45, 0.05, 0.89)), (N - 6, kn0), (N - 1, kn0)])
    T['pa_l'] = T.k3([(0, pa0), (3, pa0), (9, (0.0, -1.0, 0.0)), (PUSH_END, (0.8, -0.5, 0.0)), (fb + 3, (0.4, 0.9, 0.1)), (N - 12, (0.4, 0.9, 0.1)),
                      (N - 6, pa0), (N - 1, pa0)])
    T['curl_l'] = T.k1([(0, 0), (2, 0), (6, 1), (N - 12, 1), (N - 6, 0), (N - 1, 0)])
    T['curl_r_golf'] = T.k1([(0, 0), (3, 0), (8, 1), (N - 11, 1), (N - 5, 0), (N - 1, 0)])
    # legs: power stance after landing (feet stay where they were; the hips sit low and wide)
    feet = std.flight_feet(H, dz, info, PUSH_END, lambda u, side: (-0.08 * side, -0.05, 0.20 * math.sin(math.pi * min(u / 0.85, 1.0)) if u < 0.85 else 0.0))
    for s in 'rl':
        ank, heel = feet[s]
        hg = T.k1([(0, 0), (DIP_END, 0), (PUSH_END, 40), (PUSH_END + 1, 40)])
        hl = T.k1([(ft, 25), (ft + 3, 0), (N - 1, 0)])
        hh = np.where(np.isnan(heel), np.where(np.arange(N) <= PUSH_END, hg, hl), heel)
        T['ank_' + s], T['heel_' + s] = ank, hh
        T['kp_' + s] = T.k3([(0, Z), (PUSH_END, Z), (20, (0, 0, -0.2)), (ft, Z), (N - 1, Z)])
    return N, std.pose_fn(H, T, golf=getattr(H, 'prop', 'racket') == 'none')
