"""Emote_JumpJoy - jump for joy (first pass, 2026-10-12).

Beats (30 fps):  READY -> quick dip, both arms swing down-back, the left hand lets go of the racket throat and balls into a fist (0-10)
                 -> explosive push, arms whip up through the front (10-15) -> airborne: big V with both fists, racket raised overhead, chest open,
                 head up, heels kicked up behind (15-29, real gravity) -> land on the balls, knees soak it up while both fists PUMP down ("YES!")
                 -> bounce up onto the toes, second smaller pump -> settle back to READY, left hand back on the throat.
Physics: dip -> constant-acceleration push -> ballistic flight (g = 9.81) -> Hermite landing absorb whose start velocity is the impact velocity,
so the body never changes speed in one frame. Leg extension at take-off / touch-down is capped at 97.5 % of the leg.
All keys are offsets from READY in the hero's own frame; arm reach is scaled by the hero's arm length, heights by leg length."""
import math
import numpy as np
import ek
from ek import track, cvec

NAME = 'Emote_JumpJoy'
N = 63                    # frames 0..62  (2.07 s)
G = 9.81
DIP_END, PUSH_END = 10, 15


def smoother(u):
    u = np.clip(u, 0, 1)
    return u * u * u * (u * (u * 6 - 15) + 10)


def build(H):
    fr = np.arange(N, dtype=float)
    ks = H.leg_len / 0.808                 # leg scale (male 1.0)
    ka = H.k                               # arm scale

    # ---------------------------------------------------------------- hips height (physics)
    D = 0.13 * ks                          # dip depth
    hip_r = np.array(H.W['RightUpperLeg'].translation)

    def max_dz(heel, df):
        """highest hips offset with the right ankle on a ball-pivot heel lift and the leg at 97.5 %"""
        a = H.heel_lift_ankle('r', heel)
        hj = hip_r + cvec(0, df, 0)
        hor = np.linalg.norm((hj - a)[:2])
        L = 0.975 * H.leg_len
        return a[2] + math.sqrt(max(L * L - hor * hor, 0)) - hip_r[2]

    df_to, df_td = 0.02, 0.0
    dz_to = max_dz(40, df_to)
    dz_td = max_dz(25, df_td)
    T = (PUSH_END - DIP_END) / ek.FPS
    a_push = 2 * (dz_to + D) / T ** 2
    v_to = a_push * T
    # flight: dz_to + v t - g t^2/2 = dz_td
    disc = v_to ** 2 - 2 * G * (dz_td - dz_to)
    t_fl = (v_to + math.sqrt(disc)) / G
    f_td = PUSH_END + t_fl * ek.FPS
    v_td = v_to - G * t_fl                 # negative
    bottom = -0.15 * ks
    T_abs = 1.5 * (dz_td - bottom) / (-v_td)
    f_bot = f_td + T_abs * ek.FPS
    after = [(f_bot, bottom), (f_bot + 4, -0.012 * ks), (f_bot + 8, -0.065 * ks), (f_bot + 13, 0.006 * ks), (f_bot + 17, -0.002), (N - 4, 0.0)]
    dz = np.zeros(N)
    for i, f in enumerate(fr):
        if f <= DIP_END:
            dz[i] = -D * smoother(f / DIP_END)
        elif f <= PUSH_END:
            t = (f - DIP_END) / ek.FPS
            dz[i] = -D + 0.5 * a_push * t * t
        elif f <= f_td:
            t = (f - PUSH_END) / ek.FPS
            dz[i] = dz_to + v_to * t - 0.5 * G * t * t
        elif f <= f_bot:
            u = (f - f_td) / (f_bot - f_td)
            m0 = v_td * T_abs               # Hermite: start slope = impact velocity, end slope 0
            dz[i] = (2 * u ** 3 - 3 * u ** 2 + 1) * dz_td + (u ** 3 - 2 * u ** 2 + u) * m0 + (-2 * u ** 3 + 3 * u ** 2) * bottom
        else:
            dz[i] = track(after, [f], dim=1)[0, 0]
    H.info = dict(dz_to=dz_to, dz_td=dz_td, v_to=v_to, v_td=v_td, t_flight=t_fl, f_td=f_td, f_bot=f_bot, apex=dz_to + v_to ** 2 / (2 * G))

    # ---------------------------------------------------------------- other hips / spine channels
    hip_f = track([(0, 0), (9, -0.04), (PUSH_END, 0.02), (22, 0.02), (f_td, 0.0), (f_bot, -0.035), (f_bot + 4, 0.0), (f_bot + 8, -0.02), (f_bot + 14, 0.004), (N - 4, 0)], fr, dim=1)[:, 0] * ks
    hip_bend = track([(0, 0), (9, 10), (PUSH_END, -4), (22, -8), (28, -2), (f_bot, 8), (f_bot + 4, 0), (f_bot + 8, 4), (f_bot + 15, -1), (N - 4, 0)], fr, dim=1)[:, 0]
    torso = [(0, 0), (9, 9), (11, 8), (PUSH_END, -2), (19, -7), (25, -6), (29, -1), (f_bot, 10), (f_bot + 2, 11), (f_bot + 5, 2), (f_bot + 9, 8), (f_bot + 15, -1), (N - 4, 0)]
    spine = track(torso, fr, lag=0, dim=1)[:, 0]
    chest = track(torso, fr, lag=1, dim=1)[:, 0]
    upper = track(torso, fr, lag=2, dim=1)[:, 0]
    headk = [(0, 0), (9, -6), (12, -6), (16, -10), (20, -14), (25, -12), (29, -4), (f_bot, 16), (f_bot + 2, 18), (f_bot + 5, 2), (f_bot + 9, 12), (f_bot + 15, -2), (N - 4, 0)]
    neck = track(headk, fr, lag=1, dim=1)[:, 0] * 0.5
    head = track(headk, fr, lag=2, dim=1)[:, 0] * 0.5
    head_side = track([(0, 0), (16, 0), (21, 5), (27, 4), (f_bot, -2), (f_bot + 6, 2), (N - 4, 0)], fr, lag=2, dim=1)[:, 0]
    clav_e = [(0, 0), (9, -3), (16, 8), (20, 14), (27, 12), (31, 5), (f_bot, -4), (f_bot + 4, 2), (f_bot + 8, -3), (f_bot + 15, 0), (N - 4, 0)]
    clav_p = [(0, 0), (9, -6), (13, 6), (20, 0), (f_bot, 8), (f_bot + 4, 2), (f_bot + 8, 6), (N - 4, 0)]
    clav_er, clav_el = track(clav_e, fr, 0, 1)[:, 0], track(clav_e, fr, 1, 1)[:, 0]
    clav_pr, clav_pl = track(clav_p, fr, 0, 1)[:, 0], track(clav_p, fr, 1, 1)[:, 0]

    # ---------------------------------------------------------------- arms (chest space, metres from the READY shoulder midpoint)
    wr0 = ek.to_char(H.wrist_r0 - H.chest0)
    wl0 = ek.to_char(H.wrist_l0 - H.chest0)
    fb = f_bot

    def S(v):
        return np.array(v) * ka

    R_keys = [(0, wr0), (3, wr0 + [0.03, -0.01, -0.01]), (9, S([0.27, -0.15, -0.40])), (11, S([0.28, -0.16, -0.40])), (13, S([0.36, 0.34, -0.10])),
              (16, S([0.42, 0.20, 0.30])), (19, S([0.47, 0.05, 0.47])), (22, S([0.46, 0.07, 0.455])), (27, S([0.455, 0.08, 0.45])), (30, S([0.42, 0.14, 0.30])),
              (fb - 1, S([0.35, 0.15, -0.11])), (fb, S([0.35, 0.15, -0.13])), (fb + 4, S([0.37, 0.15, 0.10])), (fb + 8, S([0.34, 0.15, -0.13])),
              (fb + 13, S([0.19, 0.15, -0.27])), (N - 10, wr0 + [-0.006, 0.006, -0.006]), (N - 6, wr0), (N - 1, wr0)]
    L_keys = [(0, wl0), (3, wl0 + [-0.075, -0.058, -0.025]), (10, S([-0.27, -0.14, -0.40])), (12, S([-0.28, -0.15, -0.40])), (14, S([-0.36, 0.34, -0.10])),
              (17, S([-0.42, 0.20, 0.30])), (20, S([-0.47, 0.05, 0.47])), (23, S([-0.46, 0.07, 0.455])), (28, S([-0.455, 0.08, 0.45])), (31, S([-0.42, 0.14, 0.30])),
              (fb, S([-0.35, 0.15, -0.11])), (fb + 1, S([-0.35, 0.15, -0.13])), (fb + 5, S([-0.37, 0.15, 0.10])), (fb + 9, S([-0.34, 0.15, -0.13])),
              (fb + 14, S([-0.16, 0.22, -0.26])), (N - 9, wl0 + [-0.025, -0.008, 0.015]), (N - 6, wl0), (N - 1, wl0)]
    wrist_r = track(R_keys, fr)
    wrist_l = track(L_keys, fr)
    # elbow poles (chest space): back on the wind-up, out and slightly back overhead, DOWN-back-out on the pumps (elbows drive to the ribs)
    pr0 = np.array(ek.to_char(np.array(H.ctrl['arm_r'][1].location)))
    pl0 = np.array(ek.to_char(np.array(H.ctrl['arm_l'][1].location)))
    PR = [(0, pr0), (9, S([0.45, -0.55, -0.15])), (13, S([0.60, -0.10, -0.40])), (17, S([0.80, -0.15, 0.10])), (27, S([0.80, -0.20, 0.15])),
          (fb - 1, S([0.85, -0.40, -0.30])), (fb + 8, S([0.85, -0.40, -0.30])), (N - 6, pr0), (N - 1, pr0)]
    PL = [(0, pl0), (10, S([-0.45, -0.55, -0.15])), (14, S([-0.60, -0.10, -0.40])), (18, S([-0.80, -0.15, 0.10])), (28, S([-0.80, -0.20, 0.15])),
          (fb, S([-0.85, -0.40, -0.30])), (fb + 9, S([-0.85, -0.40, -0.30])), (N - 6, pl0), (N - 1, pl0)]
    pole_r, pole_l = track(PR, fr), track(PL, fr)
    # racket aim (right hand): shaft and string normal, character axes (chest space). The head stays OUTSIDE the right leg on the swing
    # and beside (never in front of) the face on the pumps.
    sh0, fa0 = H.ready_racket_axes()
    SH = [(0, sh0), (3, sh0), (9, [0.35, -0.45, -0.82]), (11, [0.35, -0.40, -0.85]), (13, [0.50, 0.55, -0.67]), (16, [0.35, 0.75, 0.55]),
          (19, [0.30, -0.15, 0.94]), (22, [0.25, -0.05, 0.97]), (27, [0.22, 0.00, 0.98]), (30, [0.30, 0.30, 0.90]), (fb - 1, [0.55, 0.20, 0.81]),
          (fb + 4, [0.50, 0.30, 0.81]), (fb + 8, [0.55, 0.20, 0.81]), (fb + 13, [0.30, 0.55, 0.78]), (N - 6, sh0), (N - 1, sh0)]
    FA = [(0, fa0), (3, fa0), (9, [-0.95, -0.2, 0.1]), (13, [-0.85, 0.3, 0.1]), (16, [-0.9, 0.2, 0.1]), (19, [-0.3, 0.95, 0.0]), (27, [-0.3, 0.95, 0.0]),
          (30, [-0.5, 0.8, 0.0]), (fb - 1, [-0.6, 0.75, 0.2]), (fb + 8, [-0.6, 0.75, 0.2]), (fb + 13, [-0.9, 0.1, 0.3]), (N - 6, fa0), (N - 1, fa0)]
    shaft, face = track(SH, fr), track(FA, fr)
    # left fist aim: knuckle direction and palm normal
    kn0, pa0 = H.ready_hand_axes('l')
    KN = [(0, kn0), (3, kn0), (10, [-0.10, -0.35, -0.93]), (14, [-0.15, 0.60, -0.78]), (17, [-0.20, 0.55, 0.80]), (20, [-0.45, 0.05, 0.89]),
          (28, [-0.45, 0.10, 0.88]), (31, [-0.25, 0.40, 0.88]), (fb, [-0.10, 0.25, 0.96]), (fb + 9, [-0.10, 0.25, 0.96]), (fb + 14, [0.30, 0.60, -0.40]),
          (N - 6, kn0), (N - 1, kn0)]
    PA = [(0, pa0), (3, pa0), (10, [0.8, -0.5, 0.0]), (14, [1.0, 0.0, 0.0]), (17, [0.8, -0.3, 0.3]), (20, [0.4, 0.9, 0.1]), (28, [0.4, 0.9, 0.1]),
          (fb, [0.6, -0.8, 0.0]), (fb + 9, [0.6, -0.8, 0.0]), (fb + 14, [0.9, 0.1, 0.4]), (N - 6, pa0), (N - 1, pa0)]
    knuck, palm = track(KN, fr), track(PA, fr)
    curl_l = track([(0, 0), (2, 0), (6, 1), (N - 11, 1), (N - 6, 0), (N - 1, 0)], fr, dim=1)[:, 0]
    curl_r = track([(0, 0), (3, 0), (8, 1), (N - 10, 1), (N - 5, 0), (N - 1, 0)], fr, dim=1)[:, 0]

    # ---------------------------------------------------------------- legs
    heel_g = track([(0, 0), (DIP_END, 0), (PUSH_END, 40)], fr, dim=1)[:, 0]
    heel_land = track([(f_td, 25), (f_td + 3, 0), (fb + 4, 12), (fb + 8, 0), (N - 1, 0)], fr, dim=1)[:, 0]
    hz = dz                                  # hips vertical offset
    feet = {}
    for s, side in (('r', 1), ('l', -1)):
        a_to = H.heel_lift_ankle(s, 40)
        a_td = H.heel_lift_ankle(s, 25)
        A = np.zeros((N, 3))
        heel = np.zeros(N)
        for i, f in enumerate(fr):
            if f <= PUSH_END:
                heel[i] = heel_g[i]
                A[i] = H.heel_lift_ankle(s, heel[i])
            elif f < f_td:
                u = (f - PUSH_END) / (f_td - PUSH_END)
                base = (1 - u) * a_to + u * a_td
                ride = hz[i] - ((1 - u) * dz_to + u * dz_td)
                amp = 1.0 if s == 'r' else 0.8          # a little asymmetry: the left heel kicks up less, a frame later
                uu = u if s == 'r' else max(0.0, u - 0.04)
                bump = math.sin(math.pi * min(uu / 0.82, 1.0)) ** 1.2 if uu < 0.82 else 0.0
                tuck = cvec(-0.10 * side, -0.42, 0.34) * ks * bump * amp
                A[i] = base + np.array([0, 0, ride]) + tuck
                heel[i] = 40 + (25 - 40) * u + 12 * bump
            else:
                heel[i] = heel_land[i]
                A[i] = H.heel_lift_ankle(s, heel[i])
        feet[s] = (A, heel)
    knee0 = {'r': H.knee_r0, 'l': H.knee_l0}
    kick = np.array([1.0 if PUSH_END < f < f_td else 0.0 for f in fr])
    kick = np.convolve(np.pad(kick, 3, mode='edge'), np.ones(7) / 7, mode='valid')

    def pose(f):
        i = int(f)
        H.set_hips((0, hip_f[i], hz[i]), bend=hip_bend[i])
        H.set_fk('Spine', bend=spine[i])
        H.set_fk('Chest', bend=chest[i])
        H.set_fk('UpperChest', bend=upper[i])
        H.set_fk('Neck', bend=neck[i])
        H.set_fk('Head', bend=head[i], side=head_side[i])
        H.clavicle('r', clav_er[i], clav_pr[i])
        H.clavicle('l', clav_el[i], clav_pl[i])
        H.set_arm('r', wrist_r[i], H.racket_hand_R(shaft[i], face[i]), pole_r[i])
        H.set_arm('l', wrist_l[i], H.hand_R('l', knuck[i], palm[i]), pole_l[i])
        H.fingers('l', curl_l[i])
        if getattr(H, 'prop', 'racket') == 'none':
            H.fingers('r', curl_r[i])
        for s, side in (('r', 1), ('l', -1)):
            A, heel = feet[s]
            pole = knee0[s] + cvec((0.15 - 0.13 * kick[i]) * side, 0.6, -0.25 * kick[i]) + cvec(0, hip_f[i], hz[i])
            H.set_leg(s, A[i], H.foot_R(s, heel[i]), pole)
            # toes stay flat on the floor while the heel is up on the ground; in the air they point a little
            grounded = f <= PUSH_END or f >= f_td
            toe = heel[i] if grounded else heel[i] * 0.4
            H.set_local_rot(('Right' if s == 'r' else 'Left') + 'Toes', (1, 0, 0), toe)
    return N, pose
