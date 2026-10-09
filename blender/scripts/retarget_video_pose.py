"""Put motion tracked from the approved gameplay video onto the V4 tennis rig.

The video (Generated/video/tennis-gameplay-concept.mp4) is the target for how the players
move. MediaPipe Pose tracks the near player in every frame (see track_video_pose.py) and
gives 33 joints in metric 3D per frame. This turns them into bone rotations:

  * Pelvis and chest orientation from the hip line and the shoulder line (the spine is
    halfway between); the head from the ears and the nose.
  * Every limb bone is aimed along its tracked segment (shoulder->elbow, elbow->wrist,
    hip->knee, knee->ankle, ankle->toe), swinging from where its parent carried it, so the
    rig keeps its own proportions and only the directions -- the pose -- come from the
    video. The Mii-style characters' big heads and short limbs do not distort anything.
  * The hand is aimed wrist->knuckles and rolled so the palm faces as tracked; the racket
    rides in the racket hand at its measured grip offset.
  * Height: the lowest foot rests on the court, except through any airborne window, where
    the body follows a ballistic arc (MediaPipe's coordinates are hip-centred, so a jump
    has to be put back).

The video is 24 fps; the clip is keyed at 30 fps and optionally slowed (`--slow`), then
written over the frames of an existing strip so the export and runtime pick it up.

Run:  Blender --background STUDIO.blend --python retarget_video_pose.py -- POSES.json FIRST LAST
          CLIP [--start F] [--slow 1.0] [--air A:B:H] [--rigs Male,Female] [--render DIR] [--save]
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))

NOSE, L_EAR, R_EAR = 0, 7, 8
L_SH, R_SH, L_EL, R_EL, L_WR, R_WR = 11, 12, 13, 14, 15, 16
L_PINKY, R_PINKY, L_INDEX, R_INDEX = 17, 18, 19, 20
L_HIP, R_HIP, L_KNEE, R_KNEE, L_ANK, R_ANK = 23, 24, 25, 26, 27, 28
L_HEEL, R_HEEL, L_TOE, R_TOE = 29, 30, 31, 32
SWAP = {L_EAR: R_EAR, L_SH: R_SH, L_EL: R_EL, L_WR: R_WR, L_PINKY: R_PINKY, L_INDEX: R_INDEX, L_HIP: R_HIP,
        L_KNEE: R_KNEE, L_ANK: R_ANK, L_HEEL: R_HEEL, L_TOE: R_TOE}
SWAP.update({v: k for k, v in list(SWAP.items())})


def to_blender(p):
    """MediaPipe world (x image-right, y down, z away from the camera) to the rig's frame
    (the camera is behind the player, so image-right is the player's right, -X, and away
    from the camera is toward the net, -Y)."""
    return Vector((-p[0], -p[2], -p[1]))


# The video's frame size: image landmarks are fractions of it.
FRAME_W, FRAME_H = 1920, 1080


def settle_lean(pts, keep):
    """Scale how far the trunk leans toward or away from the camera by `keep`, turning the
    upper body about the hips. Single-image body fits read a big cartoon head as the head
    being nearer the camera and tip the trunk toward it; the lean across the picture, which
    the image shows directly, is left alone."""
    mid_hip = (pts[L_HIP] + pts[R_HIP]) / 2
    up = (pts[L_SH] + pts[R_SH]) / 2 - mid_hip
    want = Vector((up.x, up.y * keep, up.z))
    q = up.rotation_difference(want)
    return [mid_hip + q @ (v - mid_hip) if k < L_HIP else v for k, v in enumerate(pts)]


def facing(pts):
    """Which way the body is turned about Z: the heading of its hip line, 0 when it points
    along +X (the body's left), i.e. when the body faces the net, -Y."""
    left = pts[L_HIP] - pts[R_HIP]
    return math.atan2(left.y, left.x)


def load(path, first, last, mirror, source="image", lean=1.0, skip=(), yaw=None):
    """`source` "image": MediaPipe's image landmarks -- x and y exactly where the joints are
    on screen, z its relative depth on the same scale. Its separate "world" estimate is
    metric but, on these stylised characters, can put a hand at the hip that the picture
    shows by the head; the picture is the ground truth for the pose."""
    data = json.loads(Path(path).read_text())
    frames = []
    for i in range(first, last + 1):
        entry = data.get(str(i))
        if entry is None or i in skip: frames.append(None); continue
        if source == "image":
            pts = [to_blender((p[0] * FRAME_W, p[1] * FRAME_H, p[2] * FRAME_W)) for p in entry["image"]]
        else:
            pts = [to_blender(p) for p in entry["world"]]
            if lean != 1.0: pts = settle_lean(pts, lean)
        if yaw:
            q = Quaternion((0, 0, 1), yaw(i))
            pts = [q @ v for v in pts] if not mirror else pts
        if mirror:
            pts = [Vector((-p.x, p.y, p.z)) for p in pts]
            pts = [pts[SWAP.get(k, k)] for k in range(len(pts))]
            if yaw:
                q = Quaternion((0, 0, 1), yaw(i)); pts = [q @ v for v in pts]
        frames.append(pts)
    # Fill gaps (sparse keyframes, dropped frames) by interpolating between the neighbours,
    # then smooth: a light [1 2 1] pass twice removes tracking jitter without rounding off the
    # fast parts of a swing.
    known = [i for i, f in enumerate(frames) if f is not None]
    for i, f in enumerate(frames):
        if f is not None: continue
        before = max((k for k in known if k < i), default=None); after = min((k for k in known if k > i), default=None)
        if before is None: frames[i] = frames[after]
        elif after is None: frames[i] = frames[before]
        else:
            t = (i - before) / (after - before)
            frames[i] = [frames[before][k].lerp(frames[after][k], t) for k in range(len(frames[before]))]
    for _ in range(2):
        out = []
        for i in range(len(frames)):
            a, b, c = frames[max(0, i - 1)], frames[i], frames[min(len(frames) - 1, i + 1)]
            out.append([(a[k] + b[k] * 2 + c[k]) / 4 for k in range(len(b))])
        frames = out
    return frames


def basis(left, up):
    """Rotation taking the rest frame (left +X, up +Z) to one with these axes."""
    left = left.normalized()
    up = (up - left * up.dot(left)).normalized()
    back = up.cross(left)
    return Matrix((left, back, up)).transposed()


def rot3(m): return m.to_3x3().normalized()


class Retarget:
    def __init__(self, rig):
        self.rig = rig
        self.rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}

    def carried(self, name, parent_world):
        """World rotation of `name` if it kept its rest pose relative to its parent."""
        b = self.rig.data.bones[name]
        rel = self.rest[b.parent.name].inverted() @ self.rest[name]
        return parent_world @ rot3(rel)

    def aim(self, name, parent_world, direction, roll_axis=None):
        """Swing the carried bone so its length points along `direction`; optionally roll
        it about its length so its X axis is as close as possible to `roll_axis`."""
        carried = self.carried(name, parent_world)
        y = carried.col[1]
        q = y.rotation_difference(direction.normalized())
        world = q.to_matrix() @ carried
        if roll_axis is not None:
            y = world.col[1]; x = world.col[0]
            want = (roll_axis - y * roll_axis.dot(y))
            if want.length > 1e-4:
                want.normalize()
                ang = math.atan2(x.cross(want).dot(y), x.dot(want))
                world = Quaternion(y, ang).to_matrix() @ world
        return world

    def pose(self, p, arm=None):
        """World (armature) rotations for every bone from one frame of landmarks."""
        mid_hip = (p[L_HIP] + p[R_HIP]) / 2; mid_sh = (p[L_SH] + p[R_SH]) / 2
        W = {}
        hips = basis(p[L_HIP] - p[R_HIP], mid_sh - mid_hip)
        chest = basis(p[L_SH] - p[R_SH], mid_sh - mid_hip)
        # Root stays exactly at rest, as in every authored clip. (Identity here turned it by
        # its ~90 degree rest tilt, and blending into or out of any other clip swung the hips.)
        W["Root"] = rot3(self.rest["Root"])
        W["Hips"] = hips @ rot3(self.rest["Hips"])
        spine = hips.to_quaternion().slerp(chest.to_quaternion(), .5).to_matrix()
        W["Spine"] = spine @ rot3(self.rest["Spine"])
        W["Chest"] = chest @ rot3(self.rest["Chest"])
        mid_ear = (p[L_EAR] + p[R_EAR]) / 2
        head = basis(p[L_EAR] - p[R_EAR], (mid_ear - mid_sh) * 1.0 + (p[NOSE] - mid_ear) * .0)
        # The nose leads the face: tip the head so its forward follows ears->nose.
        head_q = chest.to_quaternion().slerp(head.to_quaternion(), 1.0)
        W["Neck"] = chest.to_quaternion().slerp(head_q, .5).to_matrix() @ rot3(self.rest["Neck"])
        W["Head"] = head_q.to_matrix() @ rot3(self.rest["Head"])
        for s, sh, el, wr, idx, pk in (("L", L_SH, L_EL, L_WR, L_INDEX, L_PINKY), ("R", R_SH, R_EL, R_WR, R_INDEX, R_PINKY)):
            W[f"Shoulder.{s}"] = self.carried(f"Shoulder.{s}", W["Chest"])
            W[f"UpperArm.{s}"] = self.aim(f"UpperArm.{s}", W[f"Shoulder.{s}"], p[el] - p[sh])
            W[f"LowerArm.{s}"] = self.aim(f"LowerArm.{s}", W[f"UpperArm.{s}"], p[wr] - p[el])
            knuckles = (p[idx] + p[pk]) / 2
            across = p[idx] - p[pk]
            W[f"Hand.{s}"] = self.aim(f"Hand.{s}", W[f"LowerArm.{s}"], knuckles - p[wr], across if s == "L" else -across)
            if arm and arm[0] == s:
                # Keyed arm directions (in the chest's frame: X the body's left, -Y its front, Z
                # up), blended over the tracked ones where the tracking misreads the arm.
                _, upper, fore, w = arm
                chest_rot = chest
                def blend(tracked, keyed):
                    return tracked.normalized().lerp((chest_rot @ keyed).normalized(), w)
                up_dir = blend(p[el] - p[sh], upper); fore_dir = blend(p[wr] - p[el], fore)
                W[f"UpperArm.{s}"] = self.aim(f"UpperArm.{s}", W[f"Shoulder.{s}"], up_dir)
                W[f"LowerArm.{s}"] = self.aim(f"LowerArm.{s}", W[f"UpperArm.{s}"], fore_dir)
                W[f"Hand.{s}"] = self.aim(f"Hand.{s}", W[f"LowerArm.{s}"], fore_dir)
        for s, hip, kn, an, toe in (("L", L_HIP, L_KNEE, L_ANK, L_TOE), ("R", R_HIP, R_KNEE, R_ANK, R_TOE)):
            W[f"UpperLeg.{s}"] = self.aim(f"UpperLeg.{s}", W["Hips"], p[kn] - p[hip])
            W[f"LowerLeg.{s}"] = self.aim(f"LowerLeg.{s}", W[f"UpperLeg.{s}"], p[an] - p[kn])
            W[f"Foot.{s}"] = self.aim(f"Foot.{s}", W[f"LowerLeg.{s}"], p[toe] - p[an])
            W[f"Toes.{s}"] = self.carried(f"Toes.{s}", W[f"Foot.{s}"])
        return W

    def apply(self, W, hips_pos):
        rig = self.rig
        order = sorted(rig.data.bones, key=lambda b: len(b.parent_recursive))
        for b in order:
            pb = rig.pose.bones[b.name]
            if b.name not in W: continue
            m = W[b.name].to_4x4()
            if b.name == "Hips": m.translation = hips_pos
            else:
                parent = rig.pose.bones[b.parent.name].matrix if b.parent else Matrix()
                m.translation = (parent @ (self.rest[b.parent.name].inverted() @ self.rest[b.name])).translation if b.parent else self.rest[b.name].translation
            pb.matrix = m
            bpy.context.view_layer.update()


def racket_axis(racket, offset):
    """The racket's length (butt -> head) in the hand bone's frame: the longest extent of its
    meshes, pointing away from the grip."""
    pts = []
    for o in [racket] + list(racket.children_recursive):
        if o.type != "MESH": continue
        local = racket.matrix_world.inverted() @ o.matrix_world
        pts += [local @ Vector(c) for c in o.bound_box]
    lo = Vector([min(q[i] for q in pts) for i in range(3)]); hi = Vector([max(q[i] for q in pts) for i in range(3)])
    k = max(range(3), key=lambda i: hi[i] - lo[i])
    axis = Vector((0, 0, 0)); axis[k] = 1 if abs(hi[k]) > abs(lo[k]) else -1
    return (offset.to_3x3() @ axis).normalized()


def screen(v, yaw=0.0):
    """A direction in the rig's frame as it points on screen (x right, y down) for the video
    camera, which looks along -Y turned by the shot's `yaw`."""
    v = Quaternion((0, 0, 1), -yaw) @ v
    d = Vector((-v.x, -v.z)); return d.normalized() if d.length > 1e-6 else d


def steer_hand(hand_world, carried, axis, want, prev, yaw=0.0):
    """Turn the hand (twist about the forearm plus up to 100 degrees of wrist bend) so the racket
    points on screen the way the video shows; among turns that match, stay near the tracked
    hand and the previous frame so the racket does not flip through the depth ambiguity."""
    base = carried                     # the hand as the forearm carries it at rest
    fy = base.col[1].normalized()
    best = None
    for tw in range(-180, 180, 10):
        qt = Quaternion(fy, math.radians(tw))
        for fx in range(-100, 101, 10):
            for fz in range(-100, 101, 10):
                q = qt @ Quaternion(base.col[0], math.radians(fx)) @ Quaternion(base.col[2], math.radians(fz))
                m = q.to_matrix() @ base
                v = m @ axis
                d = screen(v, yaw)
                # On screen as shown, and not pointed into the camera (a racket end-on to the view
                # matches any direction).
                err = 2 * (1 - d.dot(want)) + .3 * abs((Quaternion((0, 0, 1), -yaw) @ v).y)
                err += .05 * (1 - (m.to_quaternion().dot(hand_world.to_quaternion())) ** 2)
                if prev is not None: err += .1 * (1 - (m.to_quaternion().dot(prev.to_quaternion())) ** 2)
                err += .02 * (fx * fx + fz * fz) / 6400
                if best is None or err < best[0]: best = (err, m)
    return best[1]


def authored_pose(action, frame):
    """Local channels {bone: (location, rotation)} of an authored action at `frame`."""
    from retarget_intro_emotes import channels
    vals = {}
    for fc in channels(action):
        if not fc.data_path.startswith("pose.bones["): continue
        bone = fc.data_path.split('"')[1]; prop = fc.data_path.rsplit(".", 1)[1]
        vals.setdefault(bone, {}).setdefault(prop, {})[fc.array_index] = fc.evaluate(frame)
    out = {}
    for bone, v in vals.items():
        loc = v.get("location", {}); rot = v.get("rotation_quaternion", {})
        out[bone] = (Vector([loc.get(i, 0.0) for i in range(3)]),
                     Quaternion([rot.get(i, 1.0 if i == 0 else 0.0) for i in range(4)]).normalized())
    return out


def parse_timeline(text):
    """"A1:0,A25:12,V1:15,..." -> [(source, time, clip frame)]: A = the authored clip's action
    frame, V = video frame; between two anchors the time runs linearly, and between an A and a
    V anchor the pose crossfades from one to the other."""
    out = []
    for part in text.split(","):
        src, clip_frame = part.split(":")
        out.append((src[0], float(src[1:]), float(clip_frame)))
    return out


def run(rig, racket, frames, clip, start, slow, air, hand, offset, steer=None, arm_keys=None, first=1,
        timeline=None, authored=None):
    """Key the retargeted frames into a new action. Without a timeline the video plays through
    at 30 fps (times `slow`); with one, the clip is laid out anchor by anchor, which lets a
    stroke keep the contact frame the game's timing expects and lets it start from the
    authored clip's own opening."""
    rt = Retarget(rig)
    track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
    track.mute = True
    rig.animation_data.action = None
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = (1, 0, 0, 0); pb.location = (0, 0, 0); pb.scale = (1, 1, 1)
    n_video = len(frames)
    last = first + n_video - 1
    if not timeline:
        timeline = [("V", first, 0), ("V", last, int(round((n_video - 1) * 30 / 24 * slow)))]
    n_clip = int(timeline[-1][2]) + 1
    rest_hips = rt.rest["Hips"].translation.copy()
    side = "R" if hand == "RH" else "L"
    state = {"hand": None}

    def video_pose(vf):
        """Pose the rig from the video at (absolute) frame `vf`; returns local channels."""
        t = min(max(vf - first, 0), n_video - 1)
        i = int(t); a = t - i
        p = frames[i] if a < 1e-6 or i + 1 >= n_video else [frames[i][k].lerp(frames[i + 1][k], a) for k in range(len(frames[i]))]
        arm = None
        if arm_keys:
            k = arm_keys(t)
            if k: arm = (side,) + k
        W = rt.pose(p, arm)
        if steer:
            want = steer(t)
            if want is not None:
                want, yaw = want
                W[f"Hand.{side}"] = steer_hand(W[f"Hand.{side}"], rt.carried(f"Hand.{side}", W[f"LowerArm.{side}"]), racket_axis(racket, offset), want, state["hand"], yaw)
        state["hand"] = W[f"Hand.{side}"]
        # Rest the lowest ankle on the court, then lift through the airborne window.
        rt.apply(W, rest_hips)
        ground = min((rig.matrix_world @ rig.pose.bones[f"Foot.{s}"].head).z for s in "LR") - rt.rest["Foot.L"].translation.z
        lift = 0.0
        if air:
            a0, a1, h = air
            vt = t + 1                                             # video frame, 1-based within the range
            if a0 <= vt <= a1:
                u = (vt - a0) / max(1e-6, a1 - a0); lift = 4 * h * u * (1 - u)
        hips = rest_hips.copy(); hips.z += -ground + lift
        rt.apply(W, hips)
        state["air"] = lift > 0
        return {pb.name: (pb.location.copy(), pb.rotation_quaternion.copy()) for pb in rig.pose.bones}

    action = bpy.data.actions.new(f"{rig.name.split('_')[0]}__Tennis__{clip}__{hand}__Video")
    rackets, prev = [], {}
    for c in range(n_clip):
        k = max(j for j in range(len(timeline) - 1) if timeline[j][2] <= c) if c < timeline[-1][2] else len(timeline) - 2
        (s0, t0, c0), (s1, t1, c1) = timeline[k], timeline[k + 1]
        u = min(1.0, max(0.0, (c - c0) / max(1e-6, c1 - c0)))
        state["air"] = False
        if s0 == s1:
            tt = t0 + (t1 - t0) * u
            pose = video_pose(tt) if s0 == "V" else authored_pose(authored, tt)
        else:
            w = u * u * (3 - 2 * u)
            pa = video_pose(t0) if s0 == "V" else authored_pose(authored, t0)
            pb_ = video_pose(t1) if s1 == "V" else authored_pose(authored, t1)
            pose = {}
            for name in pb_:
                la, qa = pa.get(name, (Vector(), Quaternion()))
                lb, qb = pb_[name]
                if qa.dot(qb) < 0: qb = -qb
                pose[name] = (la.lerp(lb, w), qa.slerp(qb, w))
        # Pose with no action attached: an update with the clip's own keys active would
        # re-apply them over this pose.
        rig.animation_data.action = None
        for pbone in rig.pose.bones:
            loc, q = pose.get(pbone.name, (Vector(), Quaternion()))
            if pbone.name in prev and prev[pbone.name].dot(q) < 0: q = -q
            prev[pbone.name] = q.copy()
            pbone.location = loc; pbone.rotation_quaternion = q
        bpy.context.view_layer.update()
        if not state["air"]:
            # Every grounded frame -- blends between the authored opening and the video
            # included -- rests its lowest ankle on the court. Blending two grounded poses
            # bone by bone does not keep the feet down: mid-blend the hips sank 20cm.
            ground = min((rig.matrix_world @ rig.pose.bones[f"Foot.{s}"].head).z for s in "LR") - rt.rest["Foot.L"].translation.z
            hb = rig.pose.bones["Hips"]; m = hb.matrix.copy(); m.translation.z -= ground; hb.matrix = m
            bpy.context.view_layer.update()
        rackets.append(rig.matrix_world @ rig.pose.bones[f"Hand.{side}"].matrix @ offset)
        rig.animation_data.action = action
        for pbone in rig.pose.bones:
            pbone.keyframe_insert("location", frame=1 + c); pbone.keyframe_insert("rotation_quaternion", frame=1 + c)
        rig.animation_data.action = None
    track.mute = False
    return action, rackets, n_clip


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    path, first, last, clip = argv[0], int(argv[1]), int(argv[2]), argv[3]
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d
    slow = float(opt("--slow", 1.0))
    air = tuple(float(x) for x in opt("--air").split(":")) if opt("--air") else None
    rigs = opt("--rigs", "Male").split(",")
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    from retarget_intro_emotes import channels, racket_offset
    report = []
    for gender in rigs:
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        prop = next(c for c in scene.collection.children if c.name.startswith(f"V4 PROP {gender} V4 racket"))
        racket = next(o for o in prop.objects if o.parent is None)
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        strips = {s.name: s for s in track.strips}
        offsets = {"RH": racket_offset(rig, racket, "Hand.R", int(strips["Ready RH"].frame_start) + 1),
                   "LH": racket_offset(rig, racket, "Hand.L", int(strips["Ready LH"].frame_start) + 1)}
        for hand in ("RH", "LH"):
            yaw = None
            if opt("--shots"):
                # Each camera shot has its own view of the body; turn each so the character faces
                # where the game needs it: "A25" = as the authored clip does at its frame 25,
                # "net" = toward the net (-Y). "@F" names the video frame to match (default: the
                # shot's first frame for A, its last for net).
                raw = load(path, first, last, mirror=hand == "LH", source=opt("--source", "image"), lean=float(opt("--lean", 1.0)),
                           skip={int(x) for x in opt("--skip", "").split(",") if x})
                ranges = []
                for spec in opt("--shots").split(","):
                    span, target = spec.split(":")
                    a, b = (int(x) for x in span.split("-"))
                    target, _, at = target.partition("@")
                    if target == "net":
                        ref = int(at) if at else b; want = math.atan2(0, 1)
                    else:
                        ref = int(at) if at else a
                        base_strip = strips.get(f"{opt('--base', clip)} {hand}")
                        rig.animation_data.action = None; track.mute = True
                        for bone, (loc, q) in authored_pose(base_strip.action, float(target[1:])).items():
                            pbn = rig.pose.bones.get(bone)
                            if pbn: pbn.rotation_mode = "QUATERNION"; pbn.location = loc; pbn.rotation_quaternion = q
                        bpy.context.view_layer.update()
                        hips_m = rig.pose.bones["Hips"].matrix.to_3x3() @ rig.data.bones["Hips"].matrix_local.to_3x3().inverted()
                        left = hips_m.col[0]; want = math.atan2(left.y, left.x)
                        track.mute = False
                    have = facing(raw[ref - first])
                    ranges.append((a, b, want - have))
                    print(f"VIDEO {hand} shot {a}-{b}: turn {math.degrees(want - have):.0f} deg", flush=True)

                def yaw(i, ranges=ranges):
                    return next((d for a, b, d in ranges if a <= i <= b), 0.0)
            frames = load(path, first, last, mirror=hand == "LH", source=opt("--source", "image"), lean=float(opt("--lean", 1.0)),
                          skip={int(x) for x in opt("--skip", "").split(",") if x}, yaw=yaw)
            steer = None
            if opt("--racket"):
                marks = {int(k): Vector(v).normalized() for k, v in json.loads(Path(opt("--racket")).read_text()).items() if not k.startswith("_")}
                sign = -1 if hand == "LH" else 1

                def steer(t, marks=marks, sign=sign):
                    f = first + t; a, b = int(math.floor(f)), int(math.floor(f)) + 1
                    va, vb = marks.get(a), marks.get(b, marks.get(a))
                    if va is None: return None
                    v = va.lerp(vb, f - a); v = Vector((v.x * sign, v.y))
                    return v.normalized(), (yaw(round(f)) if yaw else 0.0)
            arm_keys = None
            if opt("--arm"):
                keys = {int(k): v for k, v in json.loads(Path(opt("--arm")).read_text()).items() if not k.startswith("_")}
                mx = -1 if hand == "LH" else 1

                def arm_keys(t, keys=keys, mx=mx):
                    f = first + t
                    a = max((k for k in keys if k <= f), default=None); b = min((k for k in keys if k >= f), default=None)
                    if a is None or b is None: return None
                    u = 0 if a == b else (f - a) / (b - a)
                    vec = lambda k, n: Vector((keys[k][n][0] * mx, keys[k][n][1], keys[k][n][2])).normalized()
                    return (vec(a, "upper").lerp(vec(b, "upper"), u), vec(a, "fore").lerp(vec(b, "fore"), u),
                            keys[a]["w"] + (keys[b]["w"] - keys[a]["w"]) * u)
            timeline = parse_timeline(opt("--timeline")) if opt("--timeline") else None
            base = strips.get(f"{opt('--base', clip)} {hand}")
            action, rackets, n = run(rig, racket, frames, clip, None, slow, air, hand, offsets[hand], steer, arm_keys, first,
                                     timeline, base.action if base else None)
            name = f"{clip} {hand}"
            if opt("--start"):
                start = int(opt("--start"))
            else:
                old = strips.get(name)
                start = int(old.frame_start) if old else max(int(s.frame_end) for s in track.strips) + 10
            for old in [s for s in track.strips if s.name == name]: track.strips.remove(old)
            strip = track.strips.new(name, start, action); strip.name = name
            end = start + n - 1
            for fc in channels(racket.animation_data.action):
                for kp in reversed(list(fc.keyframe_points)):
                    if start - .5 <= kp.co.x <= end + .5: fc.keyframe_points.remove(kp)
            prev = None
            for k, m in enumerate(rackets):
                racket.matrix_world = m
                if racket.rotation_mode != "QUATERNION": racket.rotation_mode = "QUATERNION"
                if prev is not None and racket.rotation_quaternion.dot(prev) < 0: racket.rotation_quaternion.negate()
                prev = racket.rotation_quaternion.copy()
                for ch in ("location", "rotation_quaternion", "scale"): racket.keyframe_insert(ch, frame=start + k)
            report.append(f"{gender} {name}: frames {start}-{end} ({n} keys from video {first}-{last})")
    print("\n".join("VIDEO " + r for r in report), flush=True)
    if opt("--render"):
        import author_tennis_motion as author
        if "--avatar" in argv:
            import fit_avatar
            fit_avatar.use("Avatar")
            fit_avatar.fit.HEIGHT = 1.196 / fit_avatar.LANDMARKS["shoulder"]
            fit_avatar.fit.build_face = fit_avatar.avatar_face; fit_avatar.fit.remove_fragments = fit_avatar.small_fragments
            fit_avatar.fit.remove_fists = fit_avatar.cut_hands; fit_avatar.fit.soften_skirt = fit_avatar.grey_skirt
            rep = []; r, body = fit_avatar.fit.fit_character("Male", rep, fit_avatar.GLB, "Avatar", fit_avatar.LANDMARKS)
            fit_avatar.scale_head(body, r, .8); fit_avatar.big_fists("Male")
        author.render(opt("--render"), [clip], "Male", per=int(opt("--per", 12)), views=tuple(opt("--views", "back").split(",")))
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("VIDEO saved", flush=True)


if __name__ == "__main__":
    main()
