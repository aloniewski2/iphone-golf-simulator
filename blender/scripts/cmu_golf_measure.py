#!/usr/bin/env python3
"""Measure real golf form from CMU's motion capture of a golfer (subject 64: blender/cmugolf/64_01.bvh ... 64_15.bvh, fetched from the public CMU database).

Not a retarget: the swing animations are authored from scratch (blender/scripts/golf_swing.py); this reads the captured swings to find what real form IS, in numbers that
do not depend on the skeleton or the golfer's size: angles of the pelvis, shoulders, spine and head, where the hands are in the body's own frame, how straight each arm is,
how far the weight goes over the lead foot, and WHEN each happens. Pure Python: forward kinematics of the BVH.

    python3 blender/scripts/cmu_golf_measure.py [64_01 64_02 ...]       prints a table per swing and the mean of them
    python3 blender/scripts/cmu_golf_measure.py --json out.json         also writes every swing's features per frame (for fitting)
"""
import math, os, sys, json
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
DIR = os.path.normpath(os.path.join(HERE, "..", "cmugolf"))
SCALE = 0.0564           # CMU's units are about 5.64 cm (1 / 0.45 inch x 2.54)


def rot(axis, deg):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    if axis == "X": return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    if axis == "Y": return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


class Bvh:
    def __init__(self, path):
        self.names, self.parent, self.offset, self.channels = [], [], [], []
        stack, frames, self.fps = [], [], 120.0
        lines = open(path).read().split("\n")
        i = 0
        while i < len(lines):
            t = lines[i].split()
            if not t: i += 1; continue
            if t[0] in ("ROOT", "JOINT"):
                self.names.append(t[1]); self.parent.append(stack[-1] if stack else -1)
                stack.append(len(self.names) - 1)
            elif t[0] == "End": stack.append(-2)
            elif t[0] == "OFFSET" and stack and stack[-1] >= 0: self.offset.append(np.array(list(map(float, t[1:4]))) * SCALE)
            elif t[0] == "CHANNELS": self.channels.append(t[2:])
            elif t[0] == "}": stack.pop()
            elif t[0] == "Frame" and t[1] == "Time:": self.fps = 1.0 / float(t[2])
            elif t[0] == "MOTION":
                i += 1
                continue
            elif t[0] == "Frames:": n = int(t[1])
            elif t[0].replace(".", "").replace("-", "").replace("e", "").isdigit() and len(t) > 10: frames.append(list(map(float, t)))
            i += 1
        self.data = np.array(frames)
        self.n = len(frames)
        self.idx = {n: k for k, n in enumerate(self.names)}

    def world(self):
        """Joint positions [frame, joint, 3] and rotations [frame, joint, 3, 3] (BVH: Y up)."""
        N, J = self.n, len(self.names)
        P = np.zeros((N, J, 3)); R = np.zeros((N, J, 3, 3))
        for f in range(N):
            row = self.data[f]; c = 0
            for j in range(J):
                ch = self.channels[j]; local = np.eye(3); pos = self.offset[j].copy()
                for name in ch:
                    v = row[c]; c += 1
                    if name == "Xposition": pos[0] = v * SCALE
                    elif name == "Yposition": pos[1] = v * SCALE
                    elif name == "Zposition": pos[2] = v * SCALE
                    else: local = local @ rot(name[0], v)
                p = self.parent[j]
                if p < 0: P[f, j] = pos; R[f, j] = local
                else: P[f, j] = P[f, p] + R[f, p] @ pos; R[f, j] = R[f, p] @ local
        return P, R


def unit(v): return v / max(np.linalg.norm(v), 1e-9)


def yaw_of(v):
    """Heading of a horizontal-ish vector in the BVH's ground plane (X, Z), degrees."""
    return math.degrees(math.atan2(v[0], v[2]))


def analyse(path):
    b = Bvh(path)
    P, R = b.world()
    J = b.idx
    g = lambda n: P[:, J[n]]
    # drop the T-pose frame the conversion puts first
    sl = slice(1, None)
    hips, neck, head = g("Hips")[sl], g("Neck")[sl], g("Head")[sl]
    lh, rh = g("LeftHand")[sl], g("RightHand")[sl]
    lsh, rsh = g("LeftArm")[sl], g("RightArm")[sl]
    lel, rel = g("LeftForeArm")[sl], g("RightForeArm")[sl]
    lhip, rhip = g("LeftUpLeg")[sl], g("RightUpLeg")[sl]
    lkn, rkn = g("LeftLeg")[sl], g("RightLeg")[sl]
    lft, rft = g("LeftFoot")[sl], g("RightFoot")[sl]
    N = len(hips)
    up = np.array([0, 1, 0.0])
    hz = lambda v: np.array([v[0], 0, v[2]])

    # which way is the golfer facing and where is the target: the shoulder line's heading at address and at the finish, from the data
    sh_line = rsh - lsh                                      # (to the golfer's right)
    face = np.array([unit(np.cross(up, hz(s))) for s in sh_line])       # the way the chest faces (up x right = forward ... checked below)
    # the phases from the shoulders' turn (a swing is one excursion and back through square): heading of the shoulder line, unwrapped, from address
    s_line = (rsh - lsh)
    hd = np.array([math.degrees(math.atan2(v[0], v[2])) for v in s_line])
    hd = np.degrees(np.unwrap(np.radians(hd)))
    addr = 5
    s = hd - hd[addr]
    k = 9
    s = np.convolve(s, np.ones(k) / k, mode="same")
    fps = b.fps
    t1 = int(next(i for i in range(addr, N) if abs(s[i]) > 35))         # the backswing has got going
    sgn = 1.0 if s[t1] > 0 else -1.0
    ss = sgn * s
    # the top: the extreme of that first turn, before the shoulders come back to where they were
    back_end = int(next((i for i in range(t1, N) if ss[i] < 0.15 * ss[t1:i + 1].max() and ss[t1:i + 1].max() > 50), N - 1))
    top = int(t1 + np.argmax(ss[t1:back_end + 1]))
    # impact: after the top, the shoulders back at square (+-12 degrees past it) and the hands nearest where they were at address
    cross = int(next((i for i in range(top, N) if ss[i] < 12), N - 1))
    hand_mid = (lh + rh) / 2
    win = np.arange(max(top + 5, cross - int(0.12 * fps)), min(N, cross + int(0.10 * fps)))
    imp = int(win[np.argmin(np.linalg.norm(hand_mid[win] - hand_mid[addr], axis=1))]) if len(win) else cross
    # finish: when the hands have nearly stopped for good
    speed = np.linalg.norm(np.diff(hand_mid, axis=0), axis=1)
    speed = np.convolve(speed, np.ones(9) / 9, mode="same")
    fin = N - 1
    for i in range(imp + 10, N - 1):
        if np.all(speed[i:min(N - 1, i + int(0.25 * fps))] < 0.0012): fin = i; break
    return dict(b=b, N=N, top=top, imp=imp, fin=fin, addr=addr, P=P[sl], R=R[sl], J=J, hand_mid=hand_mid)


def features(a):
    """Per-frame form numbers, in the golfer's own frame (forward, target and up, set from the swing itself)."""
    P, J, N = a["P"], a["J"], a["N"]
    g = lambda n: P[:, J[n]]
    up = np.array([0, 1, 0.0])
    hz = lambda v: np.array([v[0], 0, v[2]])
    top, imp, addr, fin = a["top"], a["imp"], a["addr"], a["fin"]
    lsh, rsh = g("LeftArm"), g("RightArm")
    lhip, rhip = g("LeftUpLeg"), g("RightUpLeg")
    hips, neck, head = g("Hips"), g("Neck"), g("Head")
    lh, rh = g("LeftHand"), g("RightHand")
    lel, rel = g("LeftForeArm"), g("RightForeArm")
    lft, rft = g("LeftFoot"), g("RightFoot")
    lkn, rkn = g("LeftLeg"), g("RightLeg")
    mid_ft = (lft + rft) / 2

    # the roles: the arm that folds at the top is the TRAIL arm (the lead arm stays long); the lead foot is on the lead arm's side. A left-hander reads like a right-hander.
    def ang_(a_, b_, c_):
        u, v = unit(a_ - b_), unit(c_ - b_); return math.degrees(math.acos(max(-1, min(1, u.dot(v)))))
    lead_left = ang_(lsh[top], lel[top], lh[top]) > ang_(rsh[top], rel[top], rh[top])
    lead_ft, trail_ft = (lft, rft) if lead_left else (rft, lft)
    # the golf frame: T from the trail foot to the lead foot at address (the stance line, pointing at the target); F across it, the way the chest faces at address
    T = unit(hz(lead_ft[addr] - trail_ft[addr]))
    def facing(i):
        s = hz(rsh[i] - lsh[i]); return unit(np.cross(s, up))
    F = unit(hz(facing(addr)))
    F = unit(F - T * F.dot(T))
    body = float(np.linalg.norm(neck[addr] - hips[addr]) + np.linalg.norm(hips[addr] - mid_ft[addr]))        # a size
    leg = float(np.linalg.norm(lhip[addr] - lkn[addr]) + np.linalg.norm(lkn[addr] - lft[addr]))
    # the roles for each side's joints
    if lead_left: lsh_, lel_, lh_, lhip_, lkn_, lft_ = lsh, lel, lh, lhip, lkn, lft; tsh_, tel_, th_, thip_, tkn_, tft_ = rsh, rel, rh, rhip, rkn, rft
    else: lsh_, lel_, lh_, lhip_, lkn_, lft_ = rsh, rel, rh, rhip, rkn, rft; tsh_, tel_, th_, thip_, tkn_, tft_ = lsh, lel, lh, lhip, lkn, lft
    body = float(np.linalg.norm(neck[addr] - hips[addr]) + np.linalg.norm(hips[addr] - mid_ft[addr]))        # a size
    leg = float(np.linalg.norm(lhip[addr] - lkn[addr]) + np.linalg.norm(lkn[addr] - lft[addr]))

    def yaw_in_golf(v):         # the heading of a horizontal vector in the golf frame
        v = hz(v); return math.degrees(math.atan2(v.dot(F), v.dot(T)))
    # the line from the trail side to the lead side, for the shoulders and the hips
    sh_dir = lambda i: lsh_[i] - tsh_[i]
    hip_dir = lambda i: lhip_[i] - thip_[i]
    # a turn is positive going back (away from the target): the sign is fixed from the top of the swing
    def turn(line, i):
        d = (yaw_in_golf(line(i)) - yaw_in_golf(line(addr)) + 180) % 360 - 180
        return d
    sgn_sh = 1.0 if turn(sh_dir, top) > 0 else -1.0
    sgn_hip = 1.0 if turn(hip_dir, top) > 0 else -1.0

    out = {}
    L = lambda a_, b_: float(np.linalg.norm(a_ - b_))
    def ang(a_, b_, c_):
        u, v = unit(a_ - b_), unit(c_ - b_); return math.degrees(math.acos(max(-1, min(1, u.dot(v)))))
    rows = []
    for i in range(N):
        sh_line = rsh[i] - lsh[i]           # left shoulder to right shoulder
        hip_line = rhip[i] - lhip[i]
        # the lead side: when the lead is the left foot, lead = left
        spine = unit(neck[i] - hips[i])
        tilt_fwd = math.degrees(math.atan2(spine.dot(F), spine.dot(up)))               # + : leaning toward the ball
        tilt_side = math.degrees(math.atan2(spine.dot(T), spine.dot(up)))              # + : leaning toward the target
        hip_t = unit(hip_line); sh_t = unit(sh_line)
        mid_sh = (lsh[i] + rsh[i]) / 2
        hl = lambda p: np.array([(p - hips[i]).dot(F), (p - hips[i]).dot(T), (p - hips[i]).dot(up)])
        rows.append(dict(
            i=i,
            hip_turn=sgn_hip * turn(hip_dir, i), shoulder_turn=sgn_sh * turn(sh_dir, i),
            tilt_fwd=tilt_fwd, tilt_side=tilt_side,
            pelvis_height=float((hips[i, 1] - hips[addr, 1])),
            pelvis_slide=float((hips[i] - hips[addr]).dot(T)), pelvis_fwd=float((hips[i] - hips[addr]).dot(F)),
            head_slide=float((head[i] - head[addr]).dot(T)), head_fwd=float((head[i] - head[addr]).dot(F)), head_height=float(head[i, 1] - head[addr, 1]),
            lead_elbow=ang(lsh_[i], lel_[i], lh_[i]), trail_elbow=ang(tsh_[i], tel_[i], th_[i]),
            lead_knee=ang(lhip_[i], lkn_[i], lft_[i]), trail_knee=ang(thip_[i], tkn_[i], tft_[i]),
            lead_foot_lift=float(lft_[i, 1] - lft_[addr, 1]), trail_foot_lift=float(tft_[i, 1] - tft_[addr, 1]),
            hands_fwd=float((((lh[i] + rh[i]) / 2) - mid_ft[addr]).dot(F)), hands_slide=float((((lh[i] + rh[i]) / 2) - mid_ft[addr]).dot(T)), hands_up=float(((lh[i] + rh[i]) / 2)[1]),
            lead_arm_dir=[float(x) for x in (unit(lh_[i] - lsh_[i]).dot(F), unit(lh_[i] - lsh_[i]).dot(T), unit(lh_[i] - lsh_[i]).dot(up))],
            head_turn=yaw_in_golf(head[i] - neck[i]) if False else 0.0,
        ))
    return dict(rows=rows, F=F, T=T, body=body, leg=leg, lead_left=bool(lead_left))


def phase_table(a, f):
    top, imp, addr, fin = a["top"], a["imp"], a["addr"], a["fin"]
    rows = f["rows"]
    # the in-between moments: hands passing hip height on the way up and down
    hu = np.array([r["hands_up"] for r in rows])
    hip_h = a["P"][addr, a["J"]["Hips"], 1]
    up_i = int(next((i for i in range(addr, top) if hu[i] > hip_h + 0.10), (addr + top) // 2))
    dn_i = int(next((i for i in range(top, imp) if hu[i] < hip_h + 0.10), (top + imp) // 2))
    ft_i = int(next((i for i in range(imp, a["N"]) if hu[i] > hip_h + 0.25), imp + 10))
    marks = [("address", addr), ("takeaway (hands hip high)", up_i), ("top", top), ("downswing (hands hip high)", dn_i), ("impact", imp), ("follow-through (hands hip high)", ft_i), ("finish", a["fin"])]
    return marks


def show(name, a, f, marks):
    rows = f["rows"]
    fps = a["b"].fps
    print(f"\n== {name}: {a['N']} frames at {fps:.0f} fps; lead foot {'left' if f['lead_left'] else 'right'}; address {a['addr']}, top {a['top']} ({(a['top'] - a['addr']) / fps:.2f} s from address), "
          f"impact {a['imp']} ({(a['imp'] - a['top']) / fps:.2f} s after the top), finish {a['fin']} ({(a['fin'] - a['imp']) / fps:.2f} s after impact)")
    keys = ["hip_turn", "shoulder_turn", "tilt_fwd", "tilt_side", "pelvis_slide", "pelvis_fwd", "pelvis_height", "head_slide", "head_fwd", "head_height",
            "lead_elbow", "trail_elbow", "lead_knee", "trail_knee", "lead_foot_lift", "trail_foot_lift"]
    print(f"{'':32s}" + "".join(f"{k[:9]:>10s}" for k in keys))
    for label, i in marks:
        r = rows[min(i, len(rows) - 1)]
        print(f"{label:32s}" + "".join(f"{r[k] * (100 if k in ('pelvis_slide','pelvis_fwd','pelvis_height','head_slide','head_fwd','head_height','lead_foot_lift','trail_foot_lift') else 1):10.0f}" for k in keys))


if __name__ == "__main__":
    args = [x for x in sys.argv[1:] if not x.startswith("--") and not x.endswith(".json")]
    names = args or [f"64_{i:02d}" for i in range(1, 11)]
    out_json = sys.argv[sys.argv.index("--json") + 1] if "--json" in sys.argv else None
    allres = {}
    acc = {}
    for nme in names:
        a = analyse(os.path.join(DIR, nme + ".bvh"))
        f = features(a)
        marks = phase_table(a, f)
        show(nme, a, f, marks)
        allres[nme] = dict(top=a["top"], imp=a["imp"], fin=a["fin"], addr=a["addr"], fps=a["b"].fps, marks=[(l, int(i)) for l, i in marks], rows=f["rows"][:], body=f["body"], leg=f["leg"])
        for label, i in marks:
            r = f["rows"][min(i, len(f["rows"]) - 1)]
            acc.setdefault(label, []).append(r)
    if len(names) > 1:
        print("\n== MEAN of", len(names), "swings (cm for distances, degrees for angles)")
        keys = ["hip_turn", "shoulder_turn", "tilt_fwd", "tilt_side", "pelvis_slide", "pelvis_fwd", "pelvis_height", "head_slide", "head_fwd", "head_height",
                "lead_elbow", "trail_elbow", "lead_knee", "trail_knee", "lead_foot_lift", "trail_foot_lift"]
        print(f"{'':32s}" + "".join(f"{k[:9]:>10s}" for k in keys))
        for label, rs in acc.items():
            print(f"{label:32s}" + "".join(f"{np.mean([r[k] for r in rs]) * (100 if k in ('pelvis_slide','pelvis_fwd','pelvis_height','head_slide','head_fwd','head_height','lead_foot_lift','trail_foot_lift') else 1):10.0f}" for k in keys))
    if out_json:
        json.dump(allres, open(out_json, "w"))
        print("wrote", out_json)
