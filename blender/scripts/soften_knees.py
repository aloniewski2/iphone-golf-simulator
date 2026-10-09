"""Take the squat out of the tennis clips: legs stay long and light, and the knees only sink
when the player loads up for a shot -- as the players in the approved gameplay video do.

Knee flex is measured against the rig's own rest angle (these legs are not quite straight at
rest, so a standing player already reads ~20 degrees). For every frame the extra flex is
scaled down and capped by a budget that depends on the clip:

  * idle and footwork (Ready, runs, brakes, recoveries): a light, springy flex;
  * groundstrokes: light flex, rising to a real load just before contact and releasing
    through it;
  * serve and smash: the deep load before the jump; the legs in the air are left alone;
  * low pickups keep a deeper budget; emotes, dives and the intro are untouched.

Feet stay where the clip planted them: the hips rise by what the straighter support leg
needs, and both legs are solved back onto their original ankles, knees pointing as before.

Run:  Blender --background STUDIO.blend --python soften_knees.py -- [--rigs Male,Female] [--clips A,B] [--save]
"""
import math
import sys

import bpy
from mathutils import Matrix, Vector

CONTACTS = {"Forehand": .55, "Backhand": .55, "RunningForehand": .546, "RunningBackhand": .546, "ForehandTopspin": .546,
            "Lob": .546, "MissedSwing": .546, "LowPickup": .546, "Slice": .592, "VolleyForehand": .5, "VolleyBackhand": .5,
            "Serve": .604, "Smash": .575}
FOOTWORK = {"Ready", "SplitStep", "RunLeft", "RunRight", "BrakeLeft", "BrakeRight", "DirectionChange", "Retreat",
            "RecoverLeft", "RecoverRight"}
JUMPS = {"Serve", "Smash"}


def budget(clip, t):
    """(scale, cap) for the extra knee flex at clip fraction t."""
    if clip in FOOTWORK:
        return .5, 26.0
    c = CONTACTS[clip]
    if clip in JUMPS:
        # Load under the trophy, before the legs drive up.
        peak, width, low, high = c - .2, .12, 16.0, 62.0
    else:
        # Load in the last moments of the backswing, release through contact.
        peak, width, low, high = c - .07, .1, 14.0, 42.0
    if clip == "LowPickup": low, high = 30.0, 70.0
    w = math.exp(-((t - peak) / width) ** 2)
    return .45 + .45 * w, low + (high - low) * w


def solve(hip, goal, pole, a, b):
    """Knee position for a two-bone chain from `hip` to `goal`, bending toward `pole`."""
    d = goal - hip
    dist = min(d.length, (a + b) * .9995)
    axis = d.normalized()
    x = (a * a - b * b + dist * dist) / (2 * dist)
    h = math.sqrt(max(0.0, a * a - x * x))
    side = pole - hip
    side = (side - axis * side.dot(axis))
    if side.length < 1e-6: side = Vector((0, -1, 0))
    return hip + axis * x + side.normalized() * h


def set_bone_world(pb, m):
    pb.matrix = m
    bpy.context.view_layer.update()


def process(rig, strip, clip, rest_flex, report):
    scene = bpy.context.scene
    action = strip.action
    first, last = int(strip.frame_start), int(strip.frame_end)
    bones = rig.pose.bones
    lengths = {s: ((bones[f"UpperLeg.{s}"].tail - bones[f"UpperLeg.{s}"].head).length,
                   (bones[f"LowerLeg.{s}"].tail - bones[f"LowerLeg.{s}"].head).length) for s in "LR"}
    frames = []
    for f in range(first, last + 1):
        scene.frame_set(f)
        snap = {pb.name: pb.matrix.copy() for pb in bones}
        frames.append((f, snap))
    # The court: the height the lower foot usually rests at (a toe pointing down at take-off
    # dips below it for a frame or two, so not the minimum).
    lows = sorted(min(snap[f"Foot.{s}"].translation.z for s in "LR") for _, snap in frames)
    ground = lows[len(lows) // 4]
    changed = 0
    # Key into the strip's own action: evaluate with the NLA off and the action active.
    tr = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
    tr.mute = True
    rig.animation_data.action = action
    offset = strip.frame_start - action.frame_range[0]
    for f, snap in frames:
        t = (f - first) / max(1, last - first)
        scale, cap = budget(clip, t)
        af = f - offset
        # Pose exactly as the clip has it.
        scene.frame_set(int(round(af)))
        ankles = {s: snap[f"Foot.{s}"].translation.copy() for s in "LR"}
        low = min(ankles.values(), key=lambda v: v.z)
        if low.z - ground > .08 and clip in JUMPS:
            continue                                     # both feet off the court
        flex = {s: knee_flex_from(snap, s) - rest_flex[s] for s in "LR"}
        want = {s: min(flex[s] * scale, cap) if flex[s] > 0 else flex[s] for s in "LR"}
        if all(abs(want[s] - flex[s]) < .5 for s in "LR"):
            continue
        # The support leg (the lower foot) sets the hip height its new bend needs.
        hip_m = snap["Hips"].copy()
        support = "L" if ankles["L"].z <= ankles["R"].z else "R"
        a, b = lengths[support]
        hip_joint = snap[f"UpperLeg.{support}"].translation
        inner = math.radians(180 - (want[support] + rest_flex[support]))
        reach = math.sqrt(a * a + b * b - 2 * a * b * math.cos(inner))
        d = ankles[support] - hip_joint
        horiz = Vector((d.x, d.y, 0)).length
        if reach <= horiz: continue
        new_v = math.sqrt(reach * reach - horiz * horiz)
        lift = max(0.0, new_v - (hip_joint.z - ankles[support].z))
        lift = min(lift, .25)
        hip_m.translation.z += lift
        set_bone_world(bones["Hips"], hip_m)
        # Everything above the legs rides along with the hips (children follow); resolve the legs.
        for s in "LR":
            up, lo, ft = bones[f"UpperLeg.{s}"], bones[f"LowerLeg.{s}"], bones[f"Foot.{s}"]
            hip = up.matrix.translation.copy()
            goal = ankles[s]
            knee_was = snap[f"LowerLeg.{s}"].translation
            pole = knee_was + (knee_was - (snap[f"UpperLeg.{s}"].translation + ankles[s]) / 2) * 4
            la, lb = lengths[s]
            knee = solve(hip, goal, pole, la, lb)
            um = up.matrix.copy()
            q = (um.to_3x3().col[1]).rotation_difference((knee - hip).normalized())
            um = Matrix.Translation(hip) @ (q.to_matrix().to_4x4() @ um.to_3x3().to_4x4())
            set_bone_world(up, um)
            lm = lo.matrix.copy()
            q = (lm.to_3x3().col[1]).rotation_difference((goal - knee).normalized())
            lm = Matrix.Translation(lo.matrix.translation) @ (q.to_matrix().to_4x4() @ lm.to_3x3().to_4x4())
            set_bone_world(lo, lm)
            fm = snap[f"Foot.{s}"].copy(); fm.translation = ft.matrix.translation
            set_bone_world(ft, fm)
        for name in ("Hips", "UpperLeg.L", "LowerLeg.L", "Foot.L", "UpperLeg.R", "LowerLeg.R", "Foot.R"):
            pb = bones[name]
            pb.keyframe_insert("location", frame=af)
            pb.keyframe_insert("rotation_quaternion", frame=af)
        changed += 1
    rig.animation_data.action = None
    tr.mute = False
    report.append(f"{rig.name} {strip.name}: {changed} of {len(frames)} frames eased")


def knee_flex_from(snap, s):
    """Knee flex from a snapshot of bone matrices (a bone's length runs along its Y axis)."""
    return math.degrees(snap[f"UpperLeg.{s}"].to_3x3().col[1].angle(snap[f"LowerLeg.{s}"].to_3x3().col[1]))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d
    rigs = opt("--rigs", "Male,Female").split(",")
    only = set(opt("--clips").split(",")) if opt("--clips") else None
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    report = []
    for gender in rigs:
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        tr = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        rest_flex = {}
        for s in "LR":
            u, l = rig.data.bones[f"UpperLeg.{s}"], rig.data.bones[f"LowerLeg.{s}"]
            rest_flex[s] = math.degrees((u.tail_local - u.head_local).angle(l.tail_local - l.head_local))
        for strip in list(tr.strips):
            clip = strip.name.rsplit(" ", 1)[0]
            if clip not in FOOTWORK and clip not in CONTACTS: continue
            if only and clip not in only: continue
            process(rig, strip, clip, rest_flex, report)
    print("\n".join("KNEES " + r for r in report), flush=True)
    if "--measure" in argv:
        for gender in rigs:
            rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
            tr = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
            for strip in tr.strips:
                clip = strip.name.rsplit(" ", 1)[0]
                if not strip.name.endswith("RH") or (only and clip not in only): continue
                vals = []
                for f in range(int(strip.frame_start), int(strip.frame_end) + 1, 2):
                    scene.frame_set(f)
                    snap = {pb.name: pb.matrix for pb in rig.pose.bones}
                    vals.append(max(knee_flex_from(snap, s) for s in "LR"))
                print(f"KNEES {gender} {strip.name}: mean {sum(vals) / len(vals):.1f} max {max(vals):.1f}", flush=True)
    if opt("--render"):
        sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))
        import author_tennis_motion as author, fit_avatar
        fit_avatar.use("Avatar")
        fit_avatar.fit.HEIGHT = 1.196 / fit_avatar.LANDMARKS["shoulder"]
        fit_avatar.fit.build_face = fit_avatar.avatar_face; fit_avatar.fit.remove_fragments = fit_avatar.small_fragments
        fit_avatar.fit.remove_fists = fit_avatar.cut_hands; fit_avatar.fit.soften_skirt = fit_avatar.grey_skirt
        r, body = fit_avatar.fit.fit_character("Male", [], fit_avatar.GLB, "Avatar", fit_avatar.LANDMARKS)
        fit_avatar.scale_head(body, r, .8); fit_avatar.big_fists("Male")
        author.render(opt("--render"), sorted(only), "Male", per=8, views=("three",))
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("KNEES saved", flush=True)


if __name__ == "__main__":
    main()
