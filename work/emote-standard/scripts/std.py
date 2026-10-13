"""Standard pose layer for the emote set: a clip fills a dict of per-frame tracks (numpy, offsets from READY) and `pose_fn` applies them.

Track names (all optional; character axes r/f/u, degrees for rotations):
  hp (N,3) hips translation   hb (N,3) hips (bend, side, twist)   spine/chest/upper/neck/head (N,3) (bend, side, twist)
  cl_r / cl_l (N,2) clavicle (elevation, protraction)
  wr / wl (N,3) wrist in chest space (READY shoulder midpoint origin)     pr / pl (N,3) elbow poles in chest space
  wr_head / wl_head (N,3) + wr_hw / wl_hw (N,) : wrist target given in the HEAD frame (offset from the head bone's head, READY-relative axes), blended in by weight
  shaft / face (N,3) racket aim (right hand)          kn_r / pa_r (N,3) right-hand aim when no racket aim is given
  kn_l / pa_l (N,3) left-hand knuckle direction / palm normal
  curl_l (N,) left fingers (-1 relaxed open .. 0 READY .. 1 fist)   curl_r_golf (N,) right fingers in the golf (empty-handed) variant
  ank_r / ank_l (N,3) ankle offset from the heel-lift ankle   heel_r / heel_l (N,) heel lift (+ = heel up, rolls on the forefoot)
  toe_r / toe_l (N,) toe bend (+ = toes bend up); default = heel while the foot is on the floor, 40 % of it in the air
  fyaw_r / fyaw_l (N,) foot yaw about the vertical (+ = toes turn to the hero's right)
  kp_r / kp_l (N,3) knee-pole offset (added to READY pole + hips translation)
  yaw (N,) whole-body yaw (degrees, + = turns to the hero's right) applied about the hips' vertical line to the hips AND the leg targets / poles,
           so spins and turns carry the feet with them (arms ride the chest anyway)."""
import math

import numpy as np
import bpy
import mathutils as mu

import ek
from ek import cvec, to_char, track


class Tracks(dict):
    def __init__(self, H, N):
        super().__init__()
        self.H, self.N = H, N
        self.fr = np.arange(N, dtype=float)

    def k1(self, keys, lag=0.0):
        return track(keys, self.fr, lag=lag, dim=1)[:, 0]

    def k3(self, keys, lag=0.0):
        return track(keys, self.fr, lag=lag, dim=3)

    def torso(self, keys, lags=(0, 1, 2), split=(1 / 3, 1 / 3, 1 / 3)):
        """one (bend, side, twist) key list spread over spine / chest / upper with overlap lags"""
        for name, lg, w in zip(('spine', 'chest', 'upper'), lags, split):
            self[name] = self.k3(keys, lag=lg) * w * 3 / 3

    def impact(self, name, f0, f1, power=2.2):
        """re-time frames f0..f1 of a track so it ACCELERATES into the value at f1 (a stomp / slam hits at full speed instead of easing in)"""
        a = self[name]
        v0, v1 = a[f0].copy(), a[f1].copy()
        for f in range(f0, f1 + 1):
            u = (f - f0) / (f1 - f0)
            a[f] = v0 + (v1 - v0) * u ** power

    def neckhead(self, keys, lags=(1, 2), split=(0.45, 0.55)):
        for name, lg, w in zip(('neck', 'head'), lags, split):
            self[name] = self.k3(keys, lag=lg) * w


def yaw_R(deg):
    return np.array(ek.quat_from_rotvec(math.radians(deg) * (-ek.U_)).to_matrix())


def pose_fn(H, T, golf=False):
    N = T.N
    Z3 = np.zeros((N, 3))
    Z = np.zeros(N)
    g = lambda k, d: T[k] if k in T else d
    hp, hb = g('hp', Z3), g('hb', Z3)
    yaw = g('yaw', Z)
    knee0 = {'r': H.knee_r0, 'l': H.knee_l0}
    hipc = H.hips0.copy()
    hipc[2] = 0.0
    sh0, fa0 = H.ready_racket_axes()
    knr0, par0 = H.ready_hand_axes('r')
    knl0, pal0 = H.ready_hand_axes('l')
    pr0 = np.array(to_char(np.array(H.ctrl['arm_r'][1].location)))
    pl0 = np.array(to_char(np.array(H.ctrl['arm_l'][1].location)))
    wr0 = to_char(H.wrist_r0 - H.chest0)
    wl0 = to_char(H.wrist_l0 - H.chest0)

    def pose(f):
        i = int(f)
        Ry = yaw_R(yaw[i])
        H.set_hips(tuple(hp[i]), bend=hb[i, 0], side=hb[i, 1], twist=hb[i, 2] + yaw[i])
        for b, k in (('Spine', 'spine'), ('Chest', 'chest'), ('UpperChest', 'upper'), ('Neck', 'neck'), ('Head', 'head')):
            v = g(k, Z3)[i]
            H.set_fk(b, bend=v[0], side=v[1], twist=v[2])
        clr, cll = g('cl_r', np.zeros((N, 2)))[i], g('cl_l', np.zeros((N, 2)))[i]
        H.clavicle('r', clr[0], clr[1])
        H.clavicle('l', cll[0], cll[1])
        # head-relative wrist targets need the posed head
        need_head = ('wr_hw' in T and T['wr_hw'][i] > 0) or ('wl_hw' in T and T['wl_hw'][i] > 0)
        head_cs = {}
        if need_head:
            bpy.context.view_layer.update()
            Mh = H.world_mat('Head')
            Rd = np.array(Mh.to_3x3()) @ np.array(H.W['Head'].to_3x3()).T
            CSi = H.cs.matrix_world.inverted()
            for s in ('r', 'l'):
                key = 'w%s_head' % s
                if key in T:
                    pw = np.array(Mh.translation) + Rd @ cvec(*T[key][i])
                    head_cs[s] = to_char(np.array(CSi @ mu.Vector(pw)))
        for s, w0, p0 in (('r', wr0, pr0), ('l', wl0, pl0)):
            w = g('w' + s, None)
            w = w[i] if w is not None else w0
            hw = g('w%s_hw' % s, Z)[i]
            if hw > 0 and s in head_cs:
                w = (1 - hw) * np.asarray(w) + hw * head_cs[s]
            pole = g('p' + s, None)
            pole = pole[i] if pole is not None else p0
            if s == 'r':
                if 'shaft' in T:
                    R = H.racket_hand_R(T['shaft'][i], T['face'][i])
                else:
                    R = H.hand_R('r', g('kn_r', None)[i] if 'kn_r' in T else knr0, g('pa_r', None)[i] if 'pa_r' in T else par0)
            else:
                R = H.hand_R('l', T['kn_l'][i] if 'kn_l' in T else knl0, T['pa_l'][i] if 'pa_l' in T else pal0)
            H.set_arm(s, w, R, pole)
        H.fingers('l', g('curl_l', Z)[i])
        if golf:
            H.fingers('r', g('curl_r_golf', g('curl_l', Z))[i])
        for s, side in (('r', 1), ('l', -1)):
            heel = g('heel_' + s, Z)[i]
            a = H.heel_lift_ankle(s, heel) + cvec(*g('ank_' + s, Z3)[i])
            fy = g('fyaw_' + s, Z)[i]
            FR = H.foot_R(s, heel)
            if fy:
                a0 = a.copy()
                FR = yaw_R(fy) @ FR
            # whole-body yaw about the hips' vertical line (hips translation carried along)
            c = hipc + cvec(hp[i][0], hp[i][1], 0)
            a = c + Ry @ (a - c)
            FR = Ry @ FR
            pole = knee0[s] + cvec(0.15 * side, 0.6, 0) + cvec(*hp[i]) + cvec(*g('kp_' + s, Z3)[i])
            pole = c + Ry @ (pole - c)
            H.set_leg(s, a, FR, pole)
            up = g('ank_' + s, Z3)[i][2]
            toe = g('toe_' + s, None)
            toe = toe[i] if toe is not None else (heel if up < 0.012 else heel * 0.4)
            H.set_local_rot(('Right' if s == 'r' else 'Left') + 'Toes', (1, 0, 0), toe)
    return pose


def jump_profile(H, N, dip_end, push_end, D, bottom, after, heel_to=40, heel_td=25, g=9.81, df_to=0.02, df_td=0.0):
    """hips height offset per frame for a jump: smoother dip -> constant-acceleration push -> ballistic flight -> Hermite absorb (starts at the impact
    velocity) -> `after` keys (frames counted from the bottom). Returns (dz, info)."""
    from ek import track, cvec
    fr = np.arange(N, dtype=float)
    hip_r = np.array(H.W['RightUpperLeg'].translation)

    def max_dz(heel, df):
        a = H.heel_lift_ankle('r', heel)
        hj = hip_r + cvec(0, df, 0)
        hor = np.linalg.norm((hj - a)[:2])
        L = 0.975 * H.leg_len
        return a[2] + math.sqrt(max(L * L - hor * hor, 0)) - hip_r[2]

    dz_to, dz_td = max_dz(heel_to, df_to), max_dz(heel_td, df_td)
    T = (push_end - dip_end) / ek.FPS
    a_push = 2 * (dz_to + D) / T ** 2
    v_to = a_push * T
    t_fl = (v_to + math.sqrt(v_to ** 2 - 2 * g * (dz_td - dz_to))) / g
    f_td = push_end + t_fl * ek.FPS
    v_td = v_to - g * t_fl
    T_abs = 1.5 * (dz_td - bottom) / (-v_td)
    f_bot = f_td + T_abs * ek.FPS
    dz = np.zeros(N)
    aft = [(f_bot + k, v) for k, v in after]
    for i, f in enumerate(fr):
        if f <= dip_end:
            u = min(max(f / dip_end, 0), 1)
            dz[i] = -D * u * u * u * (u * (u * 6 - 15) + 10)
        elif f <= push_end:
            t = (f - dip_end) / ek.FPS
            dz[i] = -D + 0.5 * a_push * t * t
        elif f <= f_td:
            t = (f - push_end) / ek.FPS
            dz[i] = dz_to + v_to * t - 0.5 * g * t * t
        elif f <= f_bot:
            u = (f - f_td) / (f_bot - f_td)
            m0 = v_td * T_abs
            dz[i] = (2 * u ** 3 - 3 * u ** 2 + 1) * dz_td + (u ** 3 - 2 * u ** 2 + u) * m0 + (-2 * u ** 3 + 3 * u ** 2) * bottom
        else:
            dz[i] = track([(f_bot, bottom)] + aft, [f], dim=1)[0, 0]
    info = dict(dz_to=dz_to, dz_td=dz_td, v_to=v_to, f_td=f_td, f_bot=f_bot, apex=dz_to + v_to ** 2 / (2 * g), t_flight=t_fl)
    return dz, info


def flight_feet(H, dz, info, push_end, tuck, heel_to=40, heel_td=25, side_amp=(1.0, 0.85)):
    """ankle offsets (for std's ank_r / ank_l, added to the heel-lift ankle) and heel angles through a jump: on the ground before the push and after
    touch-down (heel keys handled by the caller), in the air they ride with the hips and tuck by `tuck(u)` -> (r, f, u) offset in metres."""
    N = len(dz)
    out = {}
    f_td = info['f_td']
    for s, amp in (('r', side_amp[0]), ('l', side_amp[1])):
        side = 1 if s == 'r' else -1
        ank = np.zeros((N, 3))
        heel = np.full(N, np.nan)
        a_to, a_td = H.heel_lift_ankle(s, heel_to), H.heel_lift_ankle(s, heel_td)
        for i in range(N):
            f = float(i)
            if push_end < f < f_td:
                u = (f - push_end) / (f_td - push_end)
                hh = heel_to + (heel_td - heel_to) * u
                base = (1 - u) * a_to + u * a_td
                ride = dz[i] - ((1 - u) * info['dz_to'] + u * info['dz_td'])
                t = np.array(tuck(u, side)) * amp
                want = base + np.array([0, 0, ride]) + ek.cvec(*t)
                ank[i] = ek.to_char(want - H.heel_lift_ankle(s, hh))
                heel[i] = hh
        out[s] = (ank, heel)
    return out
