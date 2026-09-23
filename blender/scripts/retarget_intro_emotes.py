"""Each player's signature intro emote: motion capture from the Higgsfield/Meshy animation
library, retargeted onto the V4 tennis rig as the "Intro" clip.

Meshy rigs our own (forward-facing) Higgsfield player model with its biped skeleton and bakes
a library action onto it. Only the skeleton's motion is used here; the mesh stays ours.

Retargeting is rotation based, bone by bone, parent first:
  * each source bone's world rotation change from its rest pose is applied to the matching
    V4 bone, after first swinging the V4 bone's rest direction onto the source's rest
    direction -- the two rests differ (their A-pose arms, our hanging arms), and without
    that swing the arms would be off by the difference all the way through;
  * the hips also take the source's root travel, scaled by the ratio of hip heights, so hops
    and jumps land where they should;
  * the glTF importer already brings the 24 fps library actions in at the scene's 30 fps,
    so source frames map one to one (windows are given in scene frames).

The clip is appended to the V4 NLA track as "Intro RH" and "Intro LH" (the export derives
clips from those strips), and the racket's world-baked action is extended so it stays in the
racket hand for the whole emote.

Run:  Blender --background sports-animation-studio-v4.blend --python retarget_intro_emotes.py -- \
          --male fistpump.glb,flex.glb --female jump.glb [--female-range 120:232] [--save]
"""
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

MAP = {
    "Hips": "Hips", "Spine": "Spine", "Spine02": "Chest", "neck": "Neck", "Head": "Head",
    "LeftShoulder": "Shoulder.L", "LeftArm": "UpperArm.L", "LeftForeArm": "LowerArm.L", "LeftHand": "Hand.L",
    "RightShoulder": "Shoulder.R", "RightArm": "UpperArm.R", "RightForeArm": "LowerArm.R", "RightHand": "Hand.R",
    "LeftUpLeg": "UpperLeg.L", "LeftLeg": "LowerLeg.L", "LeftFoot": "Foot.L", "LeftToeBase": "Toes.L",
    "RightUpLeg": "UpperLeg.R", "RightLeg": "LowerLeg.R", "RightFoot": "Foot.R", "RightToeBase": "Toes.R",
}
# The joint each V4 bone points at, used to measure limb directions from joint positions:
# glTF import guesses bone axes, so a source bone's own axis need not run along the limb.
NEXT = {
    "Hips": "Spine", "Spine": "Chest", "Chest": "Neck", "Neck": "Head",
    "Shoulder.L": "UpperArm.L", "UpperArm.L": "LowerArm.L", "LowerArm.L": "Hand.L",
    "Shoulder.R": "UpperArm.R", "UpperArm.R": "LowerArm.R", "LowerArm.R": "Hand.R",
    "UpperLeg.L": "LowerLeg.L", "LowerLeg.L": "Foot.L", "Foot.L": "Toes.L",
    "UpperLeg.R": "LowerLeg.R", "LowerLeg.R": "Foot.R", "Foot.R": "Toes.R",
}
# Both characters' Intro clips must be the same length: the export publishes one clip
# manifest for both FBXs. Shorter emotes hold their last pose to fill it.
INTRO_FRAMES = 113
# Frames left unkeyed between chained emotes, so one flows into the next.
BLEND = 6
GAP = 9


def rot3(m): return m.to_3x3().normalized()


def import_source(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    return arm, new


def retarget(src, rig, new_action, start=1, trim=None):
    """Keys the emote into `new_action` from frame `start`; returns the frame count. `trim`
    is an optional (first, last) source-frame window: library actions can run ten seconds,
    and an intro shot has under four."""
    scene = bpy.context.scene
    act = src.animation_data.action
    f0, f1 = trim if trim else act.frame_range
    frames = int(f1 - f0) + 1
    tgt_to_src = {v: k for k, v in MAP.items()}
    bones = rig.data.bones
    rest = {b.name: b.matrix_local.copy() for b in bones}
    rig_world = rig.matrix_world.copy(); rig_inv = rig_world.inverted()

    # Source rest, in world space.
    src_rest = {b.name: src.matrix_world @ b.matrix_local for b in src.data.bones}
    # Per mapped bone: swing from our rest direction to the source's rest direction, both
    # measured joint to joint. End bones (hands, toes, head) take their parent's swing.
    align = {}
    for s_name, t_name in MAP.items():
        if s_name not in src_rest: continue
        nxt = NEXT.get(t_name)
        s_next = tgt_to_src.get(nxt) if nxt else None
        if not nxt or s_next not in src_rest:
            parent = rig.data.bones[t_name].parent
            align[t_name] = align.get(parent.name, Quaternion()) if parent else Quaternion(); continue
        ours = (rig_world @ rest[nxt]).translation - (rig_world @ rest[t_name]).translation
        theirs = src_rest[s_next].translation - src_rest[s_name].translation
        align[t_name] = ours.normalized().rotation_difference(theirs.normalized())
    hip_ratio = (rig_world @ rest["Hips"]).translation.z / max(.1, src_rest["Hips"].translation.z)

    rig.animation_data.action = new_action
    order = sorted(bones, key=lambda b: len(b.parent_recursive))   # parents before children
    for i in range(frames):
        scene.frame_set(int(f0) + i)
        pose_arm = {}
        for b in order:
            name = b.name
            if b.parent:
                base = pose_arm[b.parent.name] @ rest[b.parent.name].inverted() @ rest[name]
            else:
                base = rest[name].copy()
            s_name = tgt_to_src.get(name)
            if s_name and s_name in src.pose.bones:
                s_pose_world = src.matrix_world @ src.pose.bones[s_name].matrix
                delta = rot3(s_pose_world) @ rot3(src_rest[s_name]).inverted()
                world_rot = delta @ align[name].to_matrix() @ rot3(rig_world @ rest[name])
                arm_rot = rot3(rig_inv) @ world_rot
                translation = base.translation.copy()
                if name == "Hips":
                    moved = s_pose_world.translation - src_rest[s_name].translation
                    translation = rest[name].translation + (rig_inv.to_3x3() @ moved) * hip_ratio
                desired = Matrix.Translation(translation) @ arm_rot.to_4x4()
            else:
                desired = base
            pose_arm[name] = desired
            local = base.inverted() @ desired
            pb = rig.pose.bones[name]
            pb.rotation_mode = "QUATERNION"
            pb.location = local.translation if name == "Hips" else Vector()
            pb.rotation_quaternion = local.to_quaternion()
            pb.scale = (1, 1, 1)
            pb.keyframe_insert("location", frame=start + i)
            pb.keyframe_insert("rotation_quaternion", frame=start + i)
            pb.keyframe_insert("scale", frame=start + i)
    rig.animation_data.action = None
    return frames


def hold_to_layered(rig, action, last):
    """Hold the final pose to frame `last`: evaluate the action at its last key and key
    that pose again at `last`."""
    scene = bpy.context.scene
    keyed = max((kp.co.x for fc in channels(action) for kp in fc.keyframe_points), default=1)
    if keyed >= last: return
    scene.frame_set(int(keyed))
    for pb in rig.pose.bones:
        for prop in ("location", "rotation_quaternion", "scale"):
            pb.keyframe_insert(prop, frame=last)


def channels(action):
    """F-curves of an action, for both classic and layered (Blender 4.4+) actions."""
    if getattr(action, "fcurves", None):
        return list(action.fcurves)
    out = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in strip.channelbags:
                out.extend(bag.fcurves)
    return out


def racket_offset(rig, racket, hand_bone, frame):
    bpy.context.scene.frame_set(frame)
    hand = rig.matrix_world @ rig.pose.bones[hand_bone].matrix
    return hand.inverted() @ racket.matrix_world


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {argv[i]: argv[i + 1] for i in range(len(argv) - 1) if argv[i].startswith("--")}
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    report = []
    for gender in ("Male", "Female"):
        path = args.get("--" + gender.lower())
        if not path: continue
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        for st in list(track.strips):
            if st.name.startswith("Intro "): track.strips.remove(st)
        prop = next(c for c in scene.collection.children if c.name.startswith("V4 PROP " + gender + " V4 racket"))
        racket = next(o for o in prop.objects if o.parent is None)
        ready = {s.name: s for s in track.strips}
        # Where the racket sits in each hand, measured in that hand's Ready stance.
        offsets = {"RH": racket_offset(rig, racket, "Hand.R", int(ready["Ready RH"].frame_start) + 1),
                   "LH": racket_offset(rig, racket, "Hand.L", int(ready["Ready LH"].frame_start) + 1)}
        # Evaluate the source with the V4 track silenced so nothing else drives the rig.
        track.mute = True
        # Several emotes may be chained ("a.glb,b.glb"), each with an optional window.
        windows = (args.get("--" + gender.lower() + "-range") or "").split(",")
        action = bpy.data.actions.new(f"{gender}__Tennis__Intro")
        start = 1
        for k, source in enumerate(path.split(",")):
            window = windows[k] if k < len(windows) and windows[k] else None
            trim = tuple(float(x) for x in window.split(":")) if window else None
            src, imported = import_source(source)
            start += retarget(src, rig, action, start, trim) + BLEND
            for o in imported: bpy.data.objects.remove(o, do_unlink=True)
        rig.animation_data.action = action
        hold_to_layered(rig, action, INTRO_FRAMES)
        rig.animation_data.action = None
        track.mute = False
        end = max(int(s.frame_end) for s in track.strips)
        for hand in ("RH", "LH"):
            start = end + GAP + 1
            strip = track.strips.new(f"Intro {hand}", start, action)
            strip.name = f"Intro {hand}"
            end = int(strip.frame_end)
            # Racket keys: follow the racket hand through the emote.
            bone = "Hand.R" if hand == "RH" else "Hand.L"
            for f in range(start, end + 1):
                scene.frame_set(f)
                hand_world = rig.matrix_world @ rig.pose.bones[bone].matrix
                racket.matrix_world = hand_world @ offsets[hand]
                racket.keyframe_insert("location", frame=f)
                racket.keyframe_insert("rotation_quaternion" if racket.rotation_mode == "QUATERNION" else "rotation_euler", frame=f)
                racket.keyframe_insert("scale", frame=f)
            report.append(f"{gender}: Intro {hand} frames {start}-{end}")
        scene.frame_end = max(scene.frame_end, end)
    print("\n".join("EMOTE " + r for r in report), flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("EMOTE saved", flush=True)


if __name__ == "__main__":
    main()
