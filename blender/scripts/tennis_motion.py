"""Author tennis clips on the V4 rig from a handful of timed key poses.

A clip is described the way a coach describes a stroke -- where the racket is and which way
its face points, how far the hips and shoulders have turned, where the feet are and which heel
is up -- at a few moments (unit turn, racket drop, contact, finish). Everything between is
spline-interpolated, the arms and legs are solved with IK to reach the racket and the ground,
and the result is baked to ordinary bone keys, so the runtime and the FBX export see plain
animation. The racket is keyed in world space every frame, as the rest of the library is.

Coordinates are character-local, in metres, for a RIGHT-handed player:
    r  to the player's right   (armature -X)
    f  forward, toward the net (armature -Y)
    u  up                      (armature +Z)
Angles are degrees: yaw + turns the body to its left (counter-clockwise from above), lean +
bends forward, roll + tilts to the right. The left-handed clip is the exact mirror.

Positions are authored for the male rig and scaled by the rig's shoulder height, so the same
description plays on the female rig.

Used by author_tennis_motion.py.
"""
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

MALE_SHOULDER = 1.20
RACKET_LENGTH = .68


def V(r, f, u): return Vector((-r, -f, u))


def mirror_vec(v): return Vector((-v.x, v.y, v.z))


def smooth(t): return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- interpolation

def catmull(p0, p1, p2, p3, t):
    t2, t3 = t * t, t * t * t
    return .5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)


def sample(keys, frame, loop=False, length=60):
    """Value of a keyed channel at `frame`. keys: sorted [(frame, value)], values are floats or
    Vectors. Catmull-Rom through the keys (a loop wraps its ends)."""
    if len(keys) == 1: return keys[0][1]
    frames = [k[0] for k in keys]
    if not loop:
        if frame <= frames[0]: return keys[0][1]
        if frame >= frames[-1]: return keys[-1][1]
    else:
        frame = frame % length
    # Locate the segment.
    n = len(keys)
    if loop:
        ext = [(keys[-1][0] - length, keys[-1][1])] + list(keys) + [(keys[0][0] + length, keys[0][1]), (keys[1][0] + length, keys[1][1])]
        for i in range(1, len(ext) - 2):
            if ext[i][0] <= frame <= ext[i + 1][0]:
                a, b = ext[i], ext[i + 1]
                t = (frame - a[0]) / max(1e-6, b[0] - a[0])
                return catmull(ext[i - 1][1], a[1], b[1], ext[i + 2][1], t)
        return keys[0][1]
    for i in range(n - 1):
        if frames[i] <= frame <= frames[i + 1]:
            a, b = keys[i], keys[i + 1]
            p0 = keys[i - 1][1] if i > 0 else a[1]
            p3 = keys[i + 2][1] if i + 2 < n else b[1]
            t = (frame - a[0]) / max(1e-6, b[0] - a[0])
            return catmull(p0, a[1], b[1], p3, t)
    return keys[-1][1]


# ---------------------------------------------------------------- the pose description

class Clip:
    """Timed key poses. Channels (each optional per key; missing ones carry over from the
    previous key that set them):

        hips      (r, f, u)            pelvis position (rest u = 0.76)
        hip_turn  (yaw, lean, roll)
        spine / chest / neck / head  (yaw, lean, roll), each relative to its parent
        grip      (r, f, u)            racket butt (the racket hand holds it)
        shaft     (r, f, u)            racket direction, grip to tip
        face      (r, f, u)            which way the strings face
        off       (r, f, u) | "racket" off hand: a free position, or on the racket's throat
        foot_l / foot_r  (r, f, yaw, heel, lift)  ball of the foot on the court, toe yaw, heel
                                        raise (deg), and lift off the court (m)
        elbow     (r, f, u)            where the racket elbow points (pole), relative to shoulder
    """
    def __init__(self, name, length=60, loop=False):
        self.name, self.length, self.loop = name, length, loop
        self.keys = []

    def key(self, frame, **channels):
        self.keys.append((frame, channels)); self.keys.sort(key=lambda k: k[0]); return self

    def channel(self, name):
        out = []
        for frame, ch in self.keys:
            if name in ch: out.append((frame, ch[name]))
        return out


NEUTRAL = dict(hips=(0, .02, .7), hip_turn=(0, 8, 0), spine=(0, 4, 0), chest=(0, 4, 0), neck=(0, -4, 0), head=(0, -6, 0),
               off=(-.05, .3, .92), foot_l=(-.28, .02, 8, 0, 0), foot_r=(.28, .02, -8, 0, 0), elbow=(.25, -.25, -.35))


# ---------------------------------------------------------------- baking onto the rig

class Author:
    """Bakes Clips onto one rig and its racket. `hand` is "RH" or "LH"."""
    ARM = {"RH": ("UpperArm.R", "LowerArm.R", "Hand.R"), "LH": ("UpperArm.L", "LowerArm.L", "Hand.L")}

    def __init__(self, rig, racket, support, hand, grip_offset, scale):
        self.rig, self.racket, self.support, self.hand = rig, racket, support, hand
        self.offset = grip_offset          # hand^-1 @ racket, armature space, for this hand
        self.scale = scale
        self.mirror = hand == "LH"
        self.racket_side, self.off_side = ("R", "L") if hand == "RH" else ("L", "R")
        self.controls = {}
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
        self.crouch = 0.0

    # --- conversions from the authored (right-handed, male) description ---
    def pos(self, t):
        v = V(*t) * self.scale
        return mirror_vec(v) if self.mirror else v

    def dirn(self, t):
        v = V(*t).normalized()
        return mirror_vec(v) if self.mirror else v

    def angles(self, t):
        yaw, lean, roll = t
        return (-yaw, lean, -roll) if self.mirror else (yaw, lean, roll)

    def foot_side(self, which):
        """Authored foot_l/foot_r to the rig's side (mirrored clips swap feet)."""
        if not self.mirror: return which
        return {"foot_l": "foot_r", "foot_r": "foot_l"}[which]

    # --- control objects and constraints ---
    def empty(self, name):
        e = bpy.data.objects.get(name) or bpy.data.objects.new(name, None)
        if e.name not in bpy.context.scene.collection.objects: bpy.context.scene.collection.objects.link(e)
        e.parent = self.rig; e.matrix_parent_inverse = Matrix()
        self.controls[name] = e
        return e

    def rig_up(self):
        rig = self.rig
        for side in ("L", "R"):
            hand_t = self.empty(f"AUTH hand {side}"); pole = self.empty(f"AUTH elbow {side}")
            ik = rig.pose.bones[f"LowerArm.{side}"].constraints.new("IK"); ik.name = "AUTH IK"
            ik.target = hand_t; ik.chain_count = 2; ik.pole_target = pole; ik.use_tail = True
            cr = rig.pose.bones[f"Hand.{side}"].constraints.new("COPY_ROTATION"); cr.name = "AUTH rot"
            cr.target = hand_t
            ankle = self.empty(f"AUTH ankle {side}"); knee = self.empty(f"AUTH knee {side}")
            ik = rig.pose.bones[f"LowerLeg.{side}"].constraints.new("IK"); ik.name = "AUTH IK"
            ik.target = ankle; ik.chain_count = 2; ik.pole_target = knee; ik.use_tail = True
            cr = rig.pose.bones[f"Foot.{side}"].constraints.new("COPY_ROTATION"); cr.name = "AUTH rot"
            cr.target = ankle
        self.calibrate_poles()

    def reset_pose(self):
        for pb in self.rig.pose.bones:
            pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0); pb.scale = (1, 1, 1)

    def calibrate_poles(self):
        """Blender's IK pole angle depends on each bone's roll: find, per limb, the angle
        that actually puts the elbow/knee toward its pole."""
        rig = self.rig
        rig.animation_data.action = None
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        track.mute = True
        self.reset_pose()
        for side in ("L", "R"):
            s = 1 if side == "L" else -1
            for limb, (upper, lower, end) in (("arm", ("UpperArm", "LowerArm", "Hand")), ("leg", ("UpperLeg", "LowerLeg", "Foot"))):
                ik = rig.pose.bones[f"{lower}.{side}"].constraints["AUTH IK"]
                top = self.rest[f"{upper}.{side}"].translation
                tip = self.rest[f"{end}.{side}"].translation
                if limb == "arm":
                    target = top + Vector((s * .1, -.25, -.22)); pole_dir = Vector((s * .3, .4, -.3))   # elbow back and out
                else:
                    target = top + Vector((0, -.12, -.5)); pole_dir = Vector((0, -1, 0))                # knee forward
                t_obj = ik.target; p_obj = ik.pole_target
                t_obj.matrix_world = self.rig.matrix_world @ Matrix.Translation(target)
                p_obj.matrix_world = self.rig.matrix_world @ Matrix.Translation(top + pole_dir)
                best, best_a = -9, 0
                for a in range(-180, 180, 5):
                    ik.pole_angle = math.radians(a)
                    bpy.context.view_layer.update()
                    mid = (rig.pose.bones[f"{lower}.{side}"].head)
                    along = (target - top).normalized()
                    off = (mid - top) - along * (mid - top).dot(along)
                    score = off.normalized().dot((pole_dir - along * pole_dir.dot(along)).normalized()) if off.length > 1e-4 else -1
                    if score > best: best, best_a = score, a
                ik.pole_angle = math.radians(best_a)
                self.pole_report = getattr(self, "pole_report", []) + [f"{limb}.{side} {best_a} ({best:.2f})"]
        track.mute = False

    def unrig(self):
        for pb in self.rig.pose.bones:
            for c in list(pb.constraints):
                if c.name.startswith("AUTH"): pb.constraints.remove(c)
        for e in self.controls.values(): bpy.data.objects.remove(e, do_unlink=True)
        self.controls = {}

    # --- evaluating a clip at a frame ---
    def value(self, clip, name, frame):
        keys = clip.channel(name)
        if not keys: keys = [(0, NEUTRAL[name])] if name in NEUTRAL else []
        if not keys: return None
        # "racket" markers mixed with positions: interpolate the positions only.
        numeric = [(f, Vector(v)) for f, v in keys if not isinstance(v, str)]
        if not numeric: numeric = [(0, Vector(NEUTRAL[name]))] if name in NEUTRAL else []
        return sample(numeric, frame, clip.loop, clip.length) if numeric else None

    def off_mode(self, clip, frame):
        mode = None
        for f, ch in clip.keys:
            if "off" in ch and f <= frame: mode = ch["off"]
        return mode

    def racket_matrix(self, clip, frame):
        grip = self.pos(self.value(clip, "grip", frame))
        shaft = self.dirn(self.value(clip, "shaft", frame))
        face = self.dirn(self.value(clip, "face", frame))
        face = (face - shaft * face.dot(shaft)).normalized()
        x = face.cross(shaft)
        m = Matrix((x, face, shaft)).transposed().to_4x4()
        m.translation = grip
        return m

    def rot(self, yaw, lean, roll):
        """Local rotation for the trunk bones (X left, Y up, Z forward)."""
        return (Quaternion((0, 1, 0), math.radians(yaw)) @ Quaternion((1, 0, 0), math.radians(lean))
                @ Quaternion((0, 0, 1), math.radians(roll)))

    def pose_frame(self, clip, frame):
        rig = self.rig
        # The IK solves from the current pose: start every frame from rest so the result does
        # not depend on the previous one.
        self.reset_pose()
        # Pelvis: position and orientation in armature space.
        hips = self.pos(self.value(clip, "hips", frame))
        hips.z -= self.crouch * self.scale
        yaw, lean, roll = self.angles(self.value(clip, "hip_turn", frame))
        world_rot = (Quaternion((0, 0, 1), math.radians(yaw)) @ Quaternion((1, 0, 0), math.radians(lean))
                     @ Quaternion((0, 1, 0), math.radians(-roll)))
        pb = rig.pose.bones["Hips"]
        m = world_rot.to_matrix().to_4x4() @ self.rest["Hips"].to_3x3().to_4x4()
        m.translation = hips
        pb.matrix = m
        for bone in ("Spine", "Chest", "Neck", "Head"):
            a = self.angles(self.value(clip, bone.lower(), frame))
            p = rig.pose.bones[bone]; p.rotation_mode = "QUATERNION"; p.rotation_quaternion = self.rot(*a); p.location = (0, 0, 0)
        # Racket and hands.
        racket = self.racket_matrix(clip, frame)
        hand = racket @ self.offset.inverted()
        self.controls[f"AUTH hand {self.racket_side}"].matrix_world = rig.matrix_world @ hand
        shoulder = self.rest[f"UpperArm.{self.racket_side}"].translation
        elbow = self.pos(self.value(clip, "elbow", frame))
        self.controls[f"AUTH elbow {self.racket_side}"].matrix_world = rig.matrix_world @ Matrix.Translation(shoulder + elbow)
        mode = self.off_mode(clip, frame)
        off_hand = self.controls[f"AUTH hand {self.off_side}"]
        if mode == "racket":
            throat = racket @ Matrix.Translation(Vector((0, 0, .2)))
            m = throat @ self.offset.inverted()
            m.translation = (racket @ Vector((0, 0, .17)))
            off_hand.matrix_world = rig.matrix_world @ m
        else:
            p = self.pos(self.value(clip, "off", frame))
            base = self.rest[f"Hand.{self.off_side}"]
            off_hand.matrix_world = rig.matrix_world @ Matrix.Translation(p) @ base.to_3x3().to_4x4()
        off_shoulder = self.rest[f"UpperArm.{self.off_side}"].translation
        s = 1 if self.off_side == "L" else -1
        self.controls[f"AUTH elbow {self.off_side}"].matrix_world = rig.matrix_world @ Matrix.Translation(off_shoulder + Vector((s * .35, .35, -.3)))
        # Feet: ball of the foot on the court, yaw, heel raise, lift.
        for which in ("foot_l", "foot_r"):
            side = "L" if self.foot_side(which) == "foot_l" else "R"
            r, f, fyaw, heel, lift = self.value(clip, which, frame)
            # (r, f) is where the ankle stands; a raised heel pivots about the ball of the foot.
            fyaw = -fyaw if self.mirror else fyaw
            rest_ankle = self.rest[f"Foot.{side}"]
            rest_ball = self.rest[f"Toes.{side}"].translation
            to_ankle = rest_ankle.translation - rest_ball
            yaw_only = Quaternion((0, 0, 1), math.radians(fyaw))
            ground = self.pos((r, f, 0))
            ball = ground - yaw_only @ Vector((to_ankle.x, to_ankle.y, 0))
            ball.z = rest_ball.z + lift * self.scale
            turn = yaw_only @ Quaternion((1, 0, 0), math.radians(heel))
            ankle_pos = ball + turn @ to_ankle
            m = turn.to_matrix().to_4x4() @ rest_ankle.to_3x3().to_4x4(); m.translation = ankle_pos
            self.controls[f"AUTH ankle {side}"].matrix_world = rig.matrix_world @ m
            # Knees track over the toes and a touch inside them, never splayed out.
            inward = -1 if side == "L" else 1          # toward the midline (armature X)
            knee = ankle_pos + (yaw_only @ Vector((inward * .12, -.6, .5)))
            self.controls[f"AUTH knee {side}"].matrix_world = rig.matrix_world @ Matrix.Translation(knee)
            toes = rig.pose.bones[f"Toes.{side}"]; toes.rotation_mode = "QUATERNION"
            toes.rotation_quaternion = Quaternion((1, 0, 0), math.radians(-heel * .9))
        return racket

    def bake(self, clip, action_name, strip_start):
        """Evaluate every frame, record the solved pose, then key it into a new action and
        the racket's world-space action. Returns the new action."""
        rig, scene = self.rig, bpy.context.scene
        rig.animation_data.action = None
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        track.mute = True
        frames = clip.length + 1
        poses, rackets = [], []
        for i in range(frames):
            racket = self.pose_frame(clip, i)
            bpy.context.view_layer.update()
            basis = {}
            for pb in rig.pose.bones:
                basis[pb.name] = rig.convert_space(pose_bone=pb, matrix=pb.matrix, from_space="POSE", to_space="LOCAL")
            poses.append(basis); rackets.append(rig.matrix_world @ racket)
        track.mute = False
        # The constraints stay for the next clip (unrig() removes them); keys written below
        # are the solved pose, so they do not depend on them.
        action = bpy.data.actions.get(action_name)
        if action: bpy.data.actions.remove(action)
        action = bpy.data.actions.new(action_name)
        rig.animation_data.action = action
        previous = {}
        for i, basis in enumerate(poses):
            for pb in rig.pose.bones:
                loc, q, _ = basis[pb.name].decompose()
                if pb.name in previous and previous[pb.name].dot(q) < 0: q.negate()
                previous[pb.name] = q.copy()
                pb.rotation_mode = "QUATERNION"
                pb.location = loc if pb.name in ("Hips", "Root") else Vector()
                pb.rotation_quaternion = q; pb.scale = (1, 1, 1)
                pb.keyframe_insert("location", frame=1 + i)
                pb.keyframe_insert("rotation_quaternion", frame=1 + i)
                pb.keyframe_insert("scale", frame=1 + i)
        rig.animation_data.action = None
        self.key_racket(rackets, strip_start)
        return action

    def key_racket(self, rackets, strip_start):
        racket = self.racket
        action = racket.animation_data.action
        first, last = strip_start, strip_start + len(rackets) - 1
        from retarget_intro_emotes import channels
        for fc in channels(action):
            for kp in reversed(list(fc.keyframe_points)):
                if first - .5 <= kp.co.x <= last + .5: fc.keyframe_points.remove(kp)
        previous = None
        for i, m in enumerate(rackets):
            racket.matrix_world = m
            if racket.rotation_mode != "QUATERNION": racket.rotation_mode = "QUATERNION"
            if previous is not None and racket.rotation_quaternion.dot(previous) < 0: racket.rotation_quaternion.negate()
            previous = racket.rotation_quaternion.copy()
            for ch in ("location", "rotation_quaternion", "scale"): racket.keyframe_insert(ch, frame=first + i)
