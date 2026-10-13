"""Standard locomotion cycles (EMOTE STANDARD): Walk and Run, seamless loops, in place (the game moves the root and plays the clip at
speed / planted-foot speed, HeroTennisDriver.walkClipSpeed / runClipSpeed, HubHeroAnimator).

Everything is a function of the cycle phase, so frame P equals frame 0 exactly.
Feet: stance = the foot on the ground slides back at exactly the clip speed v (zero slip once the game moves the root at v); a heel rocker
(rotation about the heel's floor point) after heel strike, flat foot, then a forefoot rocker (about the floor under the ball) to toe-off.
Swing = cubic Hermite whose end velocities equal the stance velocity (no foot pop at lift-off / strike) + a lift bump (+ a heel kick for the run).
Hips bob twice per cycle, sway over the stance foot, pelvis twist / list; chest counter-rotates; arms swing opposite to the legs; head stays level."""
import math

import numpy as np

import ek
from ek import cvec, to_char

FPS = 30

WALK = dict(name='Walk', v=1.20, P=32, duty=0.60, bias=-0.055, width=0.095, toe_out=8.0,
            z0=0.004, bob=0.030, low_at=0.06, sway=0.018, twist=6.0, list_=4.0, tilt=1.0,
            chest_twist=-8.0, lean=2.0, upright=-11.0, head_comp=1.0,
            hs=14.0, to=32.0, lift=0.075, lift_at=0.32, kick=0.0, clear=-6.0,
            heel_end=0.14, flat_end=0.55,
            arm_sw=0.17, arm_R=0.462, arm_lift=0.035, hang_f=0.02, hang_r=0.215, arm_in=0.02,
            curl=-0.35, rk_sw=0.07)
RUN = dict(name='Run', v=3.50, P=20, duty=0.36, bias=-0.12, width=0.06, toe_out=6.0,
           z0=-0.030, bob=0.032, low_at=0.18, sway=0.012, twist=9.0, list_=4.0, tilt=3.0,
           chest_twist=-12.0, lean=6.0, upright=-11.0, head_comp=1.0,
           hs=4.0, to=38.0, lift=0.34, lift_at=0.34, kick=0.04, clear=-4.0, slope=0.25, lift_pow=0.6,
           heel_end=0.06, flat_end=0.42,
           arm_sw=0.21, arm_R=0.335, arm_lift=0.07, hang_f=0.06, hang_r=0.185, arm_in=0.05,
           curl=0.55, rk_sw=0.12)


def herm(p0, p1, m0, m1, u):
    u2, u3 = u * u, u * u * u
    return (2 * u3 - 3 * u2 + 1) * p0 + (u3 - 2 * u2 + u) * m0 + (-2 * u3 + 3 * u2) * p1 + (u3 - u2) * m1


def smooth01(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


def rot_r(deg):
    """rotation about -r (+ = heel up / toes down)"""
    return np.array(ek.quat_from_rotvec(math.radians(deg) * (-ek.R_)).to_matrix())


def rot_u(deg):
    """rotation about -u (+ = toes / chest turn to the hero's right)"""
    return np.array(ek.quat_from_rotvec(math.radians(deg) * (-ek.U_)).to_matrix())


def build(H, G, golf):
    P = G['P']
    N = P + 1
    T = P / FPS
    v = G['v']
    ks = H.leg_len / 0.808
    ka = H.k
    d = G['duty']
    stance_len = v * d * T
    # foot geometry (READY, right foot; the left mirrors in x)
    geo = {}
    for s, side in (('r', 1), ('l', -1)):
        a0 = H.ankle_r0 if s == 'r' else H.ankle_l0
        b0 = H.ball_r0 if s == 'r' else H.ball_l0
        # straighten the READY toe-out (22 deg) to the gait's toe-out: yaw the foot about the ankle
        yaw_fix = -side * (math.degrees(math.atan2(abs(b0[0] - a0[0]), abs(b0[1] - a0[1]))) - G['toe_out'])
        Ry = rot_u(yaw_fix)
        ball = a0 + Ry @ (b0 - a0)
        geo[s] = dict(a0=a0, ball=ball, Ry=Ry, heel_back=0.055 * ks, ball_fwd=0.03 * ks, yaw=yaw_fix)
    hip_j = {s: np.array(H.W[('Right' if s == 'r' else 'Left') + 'UpperLeg'].translation) for s in 'rl'}

    def foot_flat(s, cf):
        """ankle position (world) of a flat foot whose ankle sits at forward offset cf (from the hip line) and the gait's lateral position"""
        g = geo[s]
        side = 1 if s == 'r' else -1
        p = cvec(G['width'] * ks * side, cf, 0.0)
        p[2] = g['a0'][2]
        return p

    def foot_pose(s, cf, theta):
        """ankle position + foot rotation for a foot on the ground at cf pitched by theta (+ heel up about the forefoot, - toes up about the heel)"""
        g = geo[s]
        A = foot_flat(s, cf)
        fwd = g['Ry'] @ np.array([0, -1.0, 0])
        fwd[2] = 0
        fwd /= np.linalg.norm(fwd)
        if theta >= 0:
            piv = A + fwd * (np.linalg.norm((g['ball'] - g['a0'])[:2]) + g['ball_fwd'])
        else:
            piv = A - fwd * g['heel_back']
        piv[2] = 0.0
        A2 = piv + rot_r(theta) @ (A - piv)
        R = g['Ry'] @ np.array(H.W[('Right' if s == 'r' else 'Left') + 'Foot'].to_3x3())
        return A2, rot_r(theta) @ R

    # ---- per-frame
    out = {k: [] for k in ('hp', 'hb', 'spine', 'chest', 'upper', 'neck', 'head', 'wr', 'wl', 'pr', 'pl', 'kn_l', 'pa_l', 'kn_r', 'pa_r',
                           'shaft', 'face', 'cl_r', 'cl_l')}
    feet = {s: dict(A=[], R=[], toe=[], pole=[]) for s in 'rl'}
    sh0, fa0 = H.ready_racket_axes()
    maxext = 0.0
    for i in range(N):
        ph = (i % P) / P
        # hips: two bobs per cycle (lowest after each heel strike), sway toward the stance foot, twist/list with the legs
        bz = G['z0'] * ks - G['bob'] * ks * math.cos(4 * math.pi * (ph - G['low_at']))
        sway = G['sway'] * math.sin(2 * math.pi * (ph - 0.05)) * ks           # + toward the right stance foot early in the cycle
        tw = G['twist'] * math.cos(2 * math.pi * ph)                            # right leg forward at right heel strike -> pelvis turns left (twist -)
        out['hp'].append([sway, 0.0, bz])
        # READY leans ~11 deg forward (tennis crouch): `upright` takes it back out, `lean` / `tilt` are the gait's own
        up = G['upright']
        out['hb'].append([G['tilt'] + up * 0.25, -G['list_'] * math.sin(2 * math.pi * (ph - 0.1)), -tw])
        ct = G['chest_twist'] * math.cos(2 * math.pi * (ph - 0.03))
        lean = G['lean']
        out['spine'].append([lean * 0.4 + up * 0.3, 0, -ct * 0.25 + tw * 0.4])
        out['chest'].append([lean * 0.35 + up * 0.25, 0, -ct * 0.35])
        out['upper'].append([lean * 0.25 + up * 0.2, 0, -ct * 0.4])
        hc = G['head_comp']
        tot = G['tilt'] + lean + up                                     # the whole trunk's pitch change: neck + head give it back (gaze level)
        out['neck'].append([-tot * 0.45 * hc, 0, ct * 0.35 * hc])
        out['head'].append([-tot * 0.55 * hc, G['list_'] * 0.3 * math.sin(2 * math.pi * (ph - 0.1)), ct * 0.4 * hc + tw * 0.2])
        # arms: opposite to the legs (right arm forward when the LEFT leg is forward = right heel strike + half a cycle)
        for s, side in (('r', 1), ('l', -1)):
            a = math.cos(2 * math.pi * (ph + (0.5 if s == 'r' else 0.0)) - 0.35)   # +1 = arm forward (lags the legs a little)
            fwdp = max(a, 0.0)
            sw = G['arm_sw'] * (G['rk_sw'] / G['arm_sw'] if (s == 'r' and not golf and G['name'] == 'Walk') else 1.0)
            wf = G['hang_f'] + sw * a
            Rr = G['arm_R']
            wu = -math.sqrt(max(Rr * Rr - wf * wf, 0.01)) + G['arm_lift'] * fwdp ** 1.3     # a pendulum about the shoulder, the elbow bends more in front
            wrr = G['hang_r'] - G['arm_in'] * fwdp
            out['w' + s].append(list(np.array([wrr * side, wf, wu]) * ka))
            out['p' + s].append(list(np.array([0.30 * side, -0.55, -0.25]) * ka))
            out['cl_' + s].append([1.5 * fwdp, 4.0 * a])
        kn = [0.12, 0.25 + 0.15 * math.cos(2 * math.pi * ph), -0.95]
        out['kn_l'].append([kn[0], kn[1], kn[2]])
        out['pa_l'].append([1.0, 0.05, 0.0])
        out['kn_r'].append([-0.12, 0.25 + 0.15 * math.cos(2 * math.pi * (ph + 0.5)), -0.95])
        out['pa_r'].append([-1.0, 0.05, 0.0])
        a_r = math.cos(2 * math.pi * (ph + 0.5))
        out['shaft'].append([0.25, 0.80 + 0.08 * a_r, -0.55 + 0.10 * a_r])
        out['face'].append([-0.95, 0.15, 0.25])
        # feet
        for s, off in (('r', 0.0), ('l', 0.5)):
            phs = (ph + off) % 1.0
            f_hs = stance_len / 2 + G['bias'] * ks
            if phs < d:
                tau = phs / d
                cf = f_hs - v * phs * T
                if tau < G['heel_end']:
                    th = -G['hs'] * (1 - smooth01(tau / G['heel_end']))
                elif tau < G['flat_end']:
                    th = 0.0
                else:
                    th = G['to'] * smooth01((tau - G['flat_end']) / (1 - G['flat_end'])) ** 1.3
                A, R = foot_pose(s, cf, th)
                toe = max(th, 0.0)
            else:
                sg = (phs - d) / (1 - d)
                Ts = (1 - d) * T
                A0, R0 = foot_pose(s, f_hs - v * d * T, G['to'])
                A1, R1 = foot_pose(s, f_hs, -G['hs'])
                m = -v * Ts * G.get('slope', 1.0)             # end slopes (metres per unit sg) = stance velocity (scaled down for the run)
                fpos = herm(0.0, 1.0, m / max(1e-6, (A1 - A0) @ ek.F_), m / max(1e-6, (A1 - A0) @ ek.F_), sg)
                A = A0 + (A1 - A0) * fpos
                kick = G['kick'] * ks * math.sin(math.pi * sg) ** 2 * (1 - sg) * 2.0
                lift = G['lift'] * ks * math.sin(math.pi * min(sg / (2 * G['lift_at']), 1.0) if sg < 2 * G['lift_at'] else 0.0)
                lift = G['lift'] * ks * (math.sin(math.pi * sg / (2 * G['lift_at'])) ** G.get('lift_pow', 1.0) if sg < G['lift_at'] else math.cos(0.5 * math.pi * (sg - G['lift_at']) / (1 - G['lift_at'])))
                A = A + cvec(0, -kick, max(lift, 0.0))
                th = G['to'] + (G['clear'] - G['to']) * smooth01(sg / 0.55) if sg < 0.55 else G['clear'] + (-G['hs'] - G['clear']) * smooth01((sg - 0.55) / 0.45)
                R = rot_r(th) @ geo[s]['Ry'] @ np.array(H.W[('Right' if s == 'r' else 'Left') + 'Foot'].to_3x3())
                toe = max(th, 0.0) * 0.3
            feet[s]['A'].append(A)
            feet[s]['R'].append(R)
            feet[s]['toe'].append(toe)
            side = 1 if s == 'r' else -1
            hip_now = hip_j[s] + cvec(*out['hp'][-1])
            ext = np.linalg.norm(A - hip_now) / H.leg_len
            maxext = max(maxext, ext)
            feet[s]['pole'].append(np.array([A[0], hip_now[1], 0]) + cvec(0.05 * side, 0.7, 0.45 + out['hp'][-1][2]))
    arr = {k: np.array(vv) for k, vv in out.items()}
    # reach guard: a leg never locks straight (IK would pull the foot off its track). Where a leg would pass 98.5 % of its length the hips
    # sink just enough; the correction is a circular min filter + gaussian, so the loop stays seamless and smooth.
    LIM = 0.985 * H.leg_len
    need = np.zeros(N)
    for i in range(N):
        for s in 'rl':
            hj = hip_j[s] + cvec(*arr['hp'][i])
            A = feet[s]['A'][i]
            hor = np.linalg.norm((hj - A)[:2])
            z_ok = A[2] + math.sqrt(max(LIM * LIM - hor * hor, 0.0))
            need[i] = min(need[i], z_ok - hj[2])
    c = need[:P]
    c = np.array([min(c[(j + k) % P] for k in range(-2, 3)) for j in range(P)])
    ker = np.exp(-0.5 * (np.arange(-4, 5) / 1.6) ** 2)
    ker /= ker.sum()
    c = np.array([sum(ker[k + 4] * c[(j + k) % P] for k in range(-4, 5)) for j in range(P)])
    c = np.minimum(c, np.array([min(need[(j + k) % P] for k in range(-1, 2)) for j in range(P)]))
    corr = np.append(c, c[0])
    arr['hp'][:, 2] += corr
    for s in 'rl':
        for i in range(N):
            feet[s]['pole'][i] = feet[s]['pole'][i] + np.array([0, 0, corr[i]])
    maxext = 0.0
    for i in range(N):
        for s in 'rl':
            hj = hip_j[s] + cvec(*arr['hp'][i])
            maxext = max(maxext, np.linalg.norm(feet[s]['A'][i] - hj) / H.leg_len)
    H.info = dict(max_leg_extension=maxext, clip_speed=v, period_frames=P, step_m=v * T / 2, cadence_spm=120 / T,
                  hips_guard_mm=float(-corr.min() * 1000))
    curl = G['curl']

    def pose(f):
        i = int(f)
        H.set_hips(tuple(arr['hp'][i]), bend=arr['hb'][i, 0], side=arr['hb'][i, 1], twist=arr['hb'][i, 2])
        for b, k in (('Spine', 'spine'), ('Chest', 'chest'), ('UpperChest', 'upper'), ('Neck', 'neck'), ('Head', 'head')):
            H.set_fk(b, bend=arr[k][i, 0], side=arr[k][i, 1], twist=arr[k][i, 2])
        H.clavicle('r', *arr['cl_r'][i])
        H.clavicle('l', *arr['cl_l'][i])
        if golf or G['name'] == 'Run':
            Rr = H.hand_R('r', arr['kn_r'][i], arr['pa_r'][i])
        else:
            Rr = H.racket_hand_R(arr['shaft'][i], arr['face'][i])
        H.set_arm('r', arr['wr'][i], Rr, arr['pr'][i])
        H.set_arm('l', arr['wl'][i], H.hand_R('l', arr['kn_l'][i], arr['pa_l'][i]), arr['pl'][i])
        H.fingers('l', curl)
        if golf or G['name'] == 'Run':
            H.fingers('r', curl)
        for s in 'rl':
            H.set_leg(s, feet[s]['A'][i], feet[s]['R'][i], feet[s]['pole'][i])
            H.set_local_rot(('Right' if s == 'r' else 'Left') + 'Toes', (1, 0, 0), feet[s]['toe'][i])
    return N, pose
