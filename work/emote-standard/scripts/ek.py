"""Emote kit (EMOTE STANDARD, 2026-10-12): pose-to-pose authoring on the match-hero rig, in Blender (bpy + numpy).

How a clip is made
  * every channel is a list of keys (frame, value); values are offsets from the hero's READY pose (ReadyIdle frame 0), so every clip starts and ends blendable to READY;
  * keys are joined with a monotone cubic (PCHIP = Blender's auto-clamped feel: no overshoot between keys, flat at extremes) - overshoot / settle is authored as explicit keys;
  * channels can be time-shifted (`lag`) to build overlapping action (hips lead, chest, then head; shoulder, then wrist);
  * arms and legs are IK: wrists live in CHEST space (they ride the spine), ankles in world space, knees / elbows aim at pole points;
  * the per-frame pose is solved by Blender's own IK constraints, read back as plain local transforms and written as the game's clip format
    (LINEAR quaternion keys on every non-Root bone + Hips location, 30 fps) - the same layout as work/emotes / work/match-anim-set bakes.

Character axes (the hero faces the net): r = hero's right = -X, f = forward = -Y, u = up = +Z (Blender world, metres, origin on the floor).
Rotation offsets are (bend, side, twist) in degrees: bend + = fold forward, side + = lean to the hero's right, twist + = chest turns to the hero's right."""
import math
import os
import numpy as np
import bpy
import mathutils as mu

R_ = np.array([-1.0, 0.0, 0.0])
F_ = np.array([0.0, -1.0, 0.0])
U_ = np.array([0.0, 0.0, 1.0])
FPS = 30


def cvec(r, f, u):
    """character (r, f, u) -> Blender world vector"""
    return r * R_ + f * F_ + u * U_


def to_char(v):
    v = np.asarray(v, float)
    return np.array([v @ R_, v @ F_, v @ U_])


def rotvec_char(bend, side, twist):
    """(bend, side, twist) degrees -> world rotation vector (axis * radians)"""
    return math.radians(bend) * (-R_) + math.radians(side) * F_ + math.radians(twist) * (-U_)


def quat_from_rotvec(rv):
    rv = np.asarray(rv, float)
    a = np.linalg.norm(rv)
    if a < 1e-9:
        return mu.Quaternion((1, 0, 0, 0))
    return mu.Quaternion(mu.Vector(rv / a), a)


# ------------------------------------------------------------------------------------------------ interpolation
def pchip(xk, yk, x):
    """monotone piecewise cubic Hermite (Fritsch-Carlson) of keys (xk[K], yk[K, D]) at x[N] -> [N, D]; flat outside the key range"""
    xk = np.asarray(xk, float)
    yk = np.asarray(yk, float)
    if yk.ndim == 1:
        yk = yk[:, None]
    x = np.asarray(x, float)
    K = len(xk)
    if K == 1:
        return np.repeat(yk, len(x), axis=0)
    h = np.diff(xk)
    d = np.diff(yk, axis=0) / h[:, None]
    m = np.zeros_like(yk)
    for k in range(1, K - 1):
        w1 = 2 * h[k] + h[k - 1]
        w2 = h[k] + 2 * h[k - 1]
        same = d[k - 1] * d[k] > 0
        m[k] = np.where(same, (w1 + w2) / (w1 / np.where(same, d[k - 1], 1) + w2 / np.where(same, d[k], 1)), 0.0)
    # end tangents flat (every clip key at the ends is a held pose)
    out = np.empty((len(x), yk.shape[1]))
    xc = np.clip(x, xk[0], xk[-1])
    idx = np.clip(np.searchsorted(xk, xc, side='right') - 1, 0, K - 2)
    t = (xc - xk[idx]) / h[idx]
    t = t[:, None]
    h00 = 2 * t ** 3 - 3 * t ** 2 + 1
    h10 = t ** 3 - 2 * t ** 2 + t
    h01 = -2 * t ** 3 + 3 * t ** 2
    h11 = t ** 3 - t ** 2
    out = h00 * yk[idx] + h10 * h[idx][:, None] * m[idx] + h01 * yk[idx + 1] + h11 * h[idx][:, None] * m[idx + 1]
    return out


def track(keys, frames, lag=0.0, dim=3):
    """keys: list of (frame, value); value scalar or sequence. lag: frames the whole track is delayed (overlap)."""
    if not keys:
        return np.zeros((len(frames), dim))
    ks = sorted(keys, key=lambda k: k[0])
    xk = [k[0] + lag for k in ks]
    yk = [np.atleast_1d(np.asarray(k[1], float)) for k in ks]
    return pchip(xk, np.array(yk), frames)


# ------------------------------------------------------------------------------------------------ the hero
class Hero:
    LIMBS = dict(arm_l=('LeftUpperArm', 'LeftLowerArm', 'LeftHand'), arm_r=('RightUpperArm', 'RightLowerArm', 'RightHand'),
                 leg_l=('LeftUpperLeg', 'LeftLowerLeg', 'LeftFoot'), leg_r=('RightUpperLeg', 'RightLowerLeg', 'RightFoot'))

    def __init__(self, tag):
        self.tag = tag
        self.rig = bpy.data.objects['Rig_Male' if tag == 'male' else 'Rig_Female']
        self.sc = bpy.context.scene
        self.sc.render.fps = FPS
        self.sc.render.fps_base = 1.0
        self.racket = bpy.data.objects['Racket_Classic']
        rig = self.rig
        rig.animation_data_create()
        a = bpy.data.actions['ReadyIdle']
        rig.animation_data.action = a
        try:
            rig.animation_data.action_slot = a.slots[0]
        except Exception:
            pass
        self.sc.frame_set(0)
        bpy.context.view_layer.update()
        self.names = [b.name for b in rig.data.bones]
        self.q_ready = {n: rig.pose.bones[n].rotation_quaternion.copy() for n in self.names}
        self.hips_loc_ready = rig.pose.bones['Hips'].location.copy()
        self.W = {n: self.world_mat(n) for n in self.names}          # READY world matrices
        self.rk_ready = self.racket.matrix_world.copy()
        # stop playing the idle: the clip is written by hand from here
        rig.animation_data.action = None
        for n in self.names:
            pb = rig.pose.bones[n]
            pb.rotation_mode = 'QUATERNION'
            pb.rotation_quaternion = self.q_ready[n]
        rig.pose.bones['Hips'].location = self.hips_loc_ready
        bpy.context.view_layer.update()
        # landmarks (READY, world)
        P = lambda n: np.array(self.W[n].translation)
        self.hips0 = P('Hips')
        self.sh_l, self.sh_r = P('LeftUpperArm'), P('RightUpperArm')
        self.chest0 = 0.5 * (self.sh_l + self.sh_r)
        self.wrist_l0, self.wrist_r0 = P('LeftHand'), P('RightHand')
        self.elbow_l0, self.elbow_r0 = P('LeftLowerArm'), P('RightLowerArm')
        self.knee_l0, self.knee_r0 = P('LeftLowerLeg'), P('RightLowerLeg')
        self.ankle_l0, self.ankle_r0 = P('LeftFoot'), P('RightFoot')
        self.ball_l0, self.ball_r0 = P('LeftToes'), P('RightToes')
        b = rig.data.bones
        self.arm_len = (b['RightUpperArm'].length + b['RightLowerArm'].length)
        self.leg_len = (b['RightUpperLeg'].length + b['RightLowerLeg'].length)
        self.k = self.arm_len / 0.5086                                  # male arm length = 1.0
        # racket frame relative to the right hand (fixed socket)
        Rh = self.W['RightHand'].to_3x3()
        Rk = self.rk_ready.to_3x3()
        self.h2r = Rh.inverted() @ Rk                                   # hand -> racket rotation
        self.rk_shaft0 = np.array(Rk.col[1]).copy()
        self.rk_face0 = np.array(Rk.col[2]).copy()
        self._controls()

    def world_mat(self, n):
        return (self.rig.matrix_world @ self.rig.pose.bones[n].matrix).copy()

    # ---- control rig ------------------------------------------------------------------------------
    def _empty(self, name, parent=None):
        o = bpy.data.objects.get(name)
        if o is None:
            o = bpy.data.objects.new(name, None)
            self.sc.collection.objects.link(o)
        o.empty_display_size = 0.05
        o.parent = parent
        o.matrix_parent_inverse = mu.Matrix.Identity(4)
        o.rotation_mode = 'QUATERNION'
        return o

    def _controls(self):
        rig = self.rig
        for pb in rig.pose.bones:
            for c in list(pb.constraints):
                pb.constraints.remove(c)
        # chest space: world aligned at READY, origin between the shoulders, carried by UpperChest
        cs = self._empty('EK_ChestSpace')
        cs.matrix_world = mu.Matrix.Translation(mu.Vector(self.chest0))
        co = cs.constraints.get('EK_child') or cs.constraints.new('CHILD_OF')
        co.name = 'EK_child'
        co.target = rig
        co.subtarget = 'UpperChest'
        bpy.context.view_layer.update()
        co.inverse_matrix = self.W['UpperChest'].inverted()
        self.cs = cs
        self.ctrl = {}
        for limb, (up, lo, end) in self.LIMBS.items():
            arm = limb.startswith('arm')
            tgt = self._empty('EK_' + limb, cs if arm else None)
            pole = self._empty('EK_pole_' + limb, cs if arm else None)
            Wend = self.W[end]
            if arm:
                tgt.location = mu.Vector(np.array(Wend.translation) - self.chest0)
            else:
                tgt.location = Wend.translation
            tgt.rotation_quaternion = Wend.to_quaternion()
            mid = np.array(self.W[lo].translation)
            if arm:
                side = 1 if limb == 'arm_r' else -1
                pole.location = mu.Vector(mid - self.chest0 + cvec(0.25 * side, -0.35, -0.1))
            else:
                side = 1 if limb == 'leg_r' else -1
                pole.location = mu.Vector(mid + cvec(0.15 * side, 0.6, 0.0))
            ik = rig.pose.bones[lo].constraints.new('IK')
            ik.name = 'EK_ik'
            ik.target, ik.pole_target = tgt, pole
            ik.chain_count = 2
            ik.use_tail = True
            ik.use_stretch = False
            ik.iterations = 500
            cr = rig.pose.bones[end].constraints.new('COPY_ROTATION')
            cr.name = 'EK_rot'
            cr.target = tgt
            cr.target_space = 'WORLD'
            cr.owner_space = 'WORLD'
            self.ctrl[limb] = (tgt, pole, ik)
        bpy.context.view_layer.update()
        # pole angles: the IK must reproduce READY exactly with the default poles
        self.pole_err = {}
        for limb, (tgt, pole, ik) in self.ctrl.items():
            lo = self.LIMBS[limb][1]
            want = np.array(self.W[lo].translation)
            best = None
            for coarse in range(-180, 180, 6):
                ik.pole_angle = math.radians(coarse)
                bpy.context.view_layer.update()
                e = np.linalg.norm(np.array(self.world_mat(lo).translation) - want)
                if best is None or e < best[0]:
                    best = (e, coarse)
            a0 = best[1]
            for fine in np.arange(a0 - 6, a0 + 6.01, 0.25):
                ik.pole_angle = math.radians(fine)
                bpy.context.view_layer.update()
                e = np.linalg.norm(np.array(self.world_mat(lo).translation) - want)
                if e < best[0]:
                    best = (e, fine)
            ik.pole_angle = math.radians(best[1])
            self.pole_err[limb] = best
        bpy.context.view_layer.update()

    # ---- per-frame pose setters -----------------------------------------------------------------
    def set_fk(self, name, bend=0.0, side=0.0, twist=0.0, extra=None):
        """rotation offset of one FK bone (pre-multiplied, axis in the bone's rest frame) on top of READY"""
        pb = self.rig.pose.bones[name]
        rv = rotvec_char(bend, side, twist)
        rest = np.array(pb.bone.matrix_local.to_3x3())
        ql = quat_from_rotvec(rest.T @ rv)
        q = ql @ self.q_ready[name]
        if extra is not None:
            q = q @ extra
        pb.rotation_quaternion = q

    def set_local_rot(self, name, axis_local, deg):
        """rotation about a LOCAL bone axis on top of READY (fingers / toes: x = curl)"""
        pb = self.rig.pose.bones[name]
        pb.rotation_quaternion = self.q_ready[name] @ mu.Quaternion(mu.Vector(axis_local), math.radians(deg))

    def set_hips(self, d_char, bend=0.0, side=0.0, twist=0.0):
        """hips translation (character axes, metres, from READY) + rotation offset"""
        pb = self.rig.pose.bones['Hips']
        rest = pb.bone.matrix_local.to_3x3()
        d_w = mu.Vector(cvec(*d_char))
        # Hips is a child of the (unanimated) Root: its local translation is in its rest frame
        pb.location = self.hips_loc_ready + rest.inverted() @ d_w
        self.set_fk('Hips', bend, side, twist)

    def set_arm(self, side, wrist_cs, hand_R_world=None, pole_cs=None):
        """wrist position in chest space (character axes from the READY shoulder midpoint), hand world rotation (3x3, READY-relative axes), elbow pole"""
        tgt, pole, ik = self.ctrl['arm_' + side]
        tgt.location = mu.Vector(cvec(*wrist_cs))
        if hand_R_world is not None:
            tgt.rotation_quaternion = mu.Matrix(hand_R_world).to_quaternion()
        if pole_cs is not None:
            pole.location = mu.Vector(cvec(*pole_cs))

    def set_leg(self, side, ankle_w, foot_R_world=None, pole_w=None):
        tgt, pole, ik = self.ctrl['leg_' + side]
        tgt.location = mu.Vector(ankle_w)
        if foot_R_world is not None:
            tgt.rotation_quaternion = mu.Matrix(foot_R_world).to_quaternion()
        if pole_w is not None:
            pole.location = mu.Vector(pole_w)

    # ---- aims ---------------------------------------------------------------------------------
    def racket_hand_R(self, shaft_c, face_c):
        """right-hand world rotation (in chest-space axes) that points the racket shaft along shaft_c and the string normal along face_c (character axes)"""
        y = unit(cvec(*shaft_c))
        z = np.asarray(cvec(*face_c), float)
        z = unit(z - (z @ y) * y)
        x = np.cross(y, z)
        Rk = np.column_stack([x, y, z])
        return Rk @ np.array(self.h2r).T

    def hand_R(self, side, knuckle_c, palm_c):
        """hand world rotation from the knuckle direction (wrist -> knuckles, the bone's Y) and the palm normal (+Z = the side the fingers curl to)"""
        y = unit(cvec(*knuckle_c))
        z = np.asarray(cvec(*palm_c), float)
        z = unit(z - (z @ y) * y)
        x = np.cross(y, z)
        return np.column_stack([x, y, z])

    def ready_hand_axes(self, side):
        W = self.W[('Left' if side == 'l' else 'Right') + 'Hand'].to_3x3()
        return to_char(np.array(W.col[1])), to_char(np.array(W.col[2]))

    def ready_racket_axes(self):
        return to_char(self.rk_shaft0), to_char(self.rk_face0)

    def foot_R(self, side, heel_lift_deg):
        """foot world rotation for a heel lift (+ = heel up / toes down) about the hero's left-right axis"""
        W = np.array(self.W[('Left' if side == 'l' else 'Right') + 'Foot'].to_3x3())
        q = quat_from_rotvec(math.radians(heel_lift_deg) * (-R_))   # + about -r: the forward end goes down, the heel up
        return np.array(q.to_matrix()) @ W

    def heel_lift_ankle(self, side, heel_lift_deg):
        """ankle position when the foot pivots on the ball by heel_lift_deg (ball stays on the floor)"""
        a0 = self.ankle_l0 if side == 'l' else self.ankle_r0
        b0 = (self.ball_l0 if side == 'l' else self.ball_r0).copy()
        b0[2] = 0.0                                   # the foot rolls on the sole under the ball joint, not on the joint centre
        b0 = b0 + float(os.environ.get('EK_PIVOT_FWD', '0.03')) * F_
        Rm = np.array(quat_from_rotvec(math.radians(heel_lift_deg) * (-R_)).to_matrix())
        return b0 + Rm @ (a0 - b0)

    def clavicle(self, side, elev_deg, fwd_deg=0.0):
        """shoulder girdle: elevation (+ = shoulder up) and protraction (+ = shoulder forward)"""
        if side == 'r':
            self.set_fk('RightShoulder', side=-elev_deg, twist=fwd_deg)
        else:
            self.set_fk('LeftShoulder', side=elev_deg, twist=-fwd_deg)

    def fingers(self, side, curl):
        """curl in [0, 1]: 0 = READY fingers, 1 = a tight fist (proximal 82 / intermediate 100 / distal 55 deg about the hinge = local x); thumb folds in a little"""
        pre = 'Left' if side == 'l' else 'Right'
        fist = dict(Proximal=82.0, Intermediate=100.0, Distal=55.0)
        relaxed = dict(Proximal=14.0, Intermediate=20.0, Distal=10.0)     # curl -1: a loose, open hand
        c = float(np.clip(curl, -1, 1))
        spread = [6.0, 2.0, -2.0, -6.0]                                    # relaxed fingers fan out a little (about local z)
        for di, d in enumerate(('Index', 'Middle', 'Ring', 'Little')):
            for j in fist:
                n = pre + d + j
                q0 = self.q_ready[n]
                if c >= 0:
                    q1 = mu.Quaternion((1, 0, 0), math.radians(fist[j]))
                    self.rig.pose.bones[n].rotation_quaternion = q0.slerp(q1, c)
                else:
                    q1 = mu.Quaternion((1, 0, 0), math.radians(relaxed[j]))
                    if j == 'Proximal':
                        q1 = mu.Quaternion((0, 0, 1), math.radians(spread[di] * (1 if side == 'l' else -1))) @ q1
                    self.rig.pose.bones[n].rotation_quaternion = q0.slerp(q1, -c)
        for j, a in dict(Proximal=12.0, Intermediate=25.0, Distal=30.0).items():
            n = pre + 'Thumb' + j
            q0 = self.q_ready[n]
            q1 = q0 @ mu.Quaternion((1, 0, 0), math.radians(a if c >= 0 else -a * 0.4))
            self.rig.pose.bones[n].rotation_quaternion = q0.slerp(q1, abs(c))

    # ---- read back ------------------------------------------------------------------------------
    def read_local(self):
        """visual local rotation (and Hips location) of every bone, constraints included"""
        bpy.context.view_layer.update()
        rig = self.rig
        quats, hips = {}, None
        for n in self.names:
            pb = rig.pose.bones[n]
            M = rig.convert_space(pose_bone=pb, matrix=pb.matrix, from_space='POSE', to_space='LOCAL')
            quats[n] = M.to_quaternion()
            if n == 'Hips':
                hips = M.translation.copy()
        return quats, hips

    def landmarks(self):
        """world positions used by the checks"""
        out = {}
        for n in ('Hips', 'Head', 'LeftHand', 'RightHand', 'LeftFoot', 'RightFoot', 'LeftToes', 'RightToes', 'LeftLowerArm', 'RightLowerArm', 'UpperChest'):
            out[n] = np.array(self.world_mat(n).translation)
        out['racket_head'] = np.array(self.racket.matrix_world @ mu.Vector((0, 0.577, 0)))
        return out


def unit(v):
    v = np.asarray(v, float)
    n = np.linalg.norm(v)
    return v / n if n > 1e-12 else v


def write_action(hero, clip, Q, H):
    """Q: list over frames of {bone: Quaternion}; H: list of Hips locations -> action `clip` (game layout: LINEAR, quaternion on every non-Root bone + Hips location)"""
    rig = hero.rig
    old = bpy.data.actions.get(clip)
    if old is not None:
        bpy.data.actions.remove(old)
    act = bpy.data.actions.new(clip)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    nF = len(Q)
    # sign continuity
    for n in hero.names:
        for f in range(1, nF):
            if Q[f][n].dot(Q[f - 1][n]) < 0:
                Q[f][n] = -Q[f][n]

    def put(path, idx, group, vals):
        fc = act.fcurve_ensure_for_datablock(rig, path, index=idx, group_name=group)
        fc.keyframe_points.add(nF)
        co = np.empty(nF * 2)
        co[0::2] = np.arange(nF)
        co[1::2] = vals
        fc.keyframe_points.foreach_set('co', co)
        for kp in fc.keyframe_points:
            kp.interpolation = 'LINEAR'
        fc.update()

    for n in hero.names:
        if n == 'Root':
            continue
        arr = np.array([[Q[f][n][c] for c in range(4)] for f in range(nF)])
        for c in range(4):
            put(f'pose.bones["{n}"].rotation_quaternion', c, n, arr[:, c])
        if n == 'Hips':
            harr = np.array([[H[f][c] for c in range(3)] for f in range(nF)])
            for c in range(3):
                put('pose.bones["Hips"].location', c, 'Hips', harr[:, c])
    act.use_frame_range = True
    act.frame_start = 0
    act.frame_end = nF - 1
    act['clip'] = clip
    act['hero'] = hero.tag
    act['length_s'] = (nF - 1) / FPS
    act['fps'] = FPS
    act['loop'] = False
    return act


def strip_controls(hero):
    rig = hero.rig
    for pb in rig.pose.bones:
        for c in list(pb.constraints):
            if c.name.startswith('EK_'):
                pb.constraints.remove(c)
    for o in list(bpy.data.objects):
        if o.name.startswith('EK_'):
            bpy.data.objects.remove(o, do_unlink=True)
