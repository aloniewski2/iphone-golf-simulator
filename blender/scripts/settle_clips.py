"""Finish tennis clips after body edits (the video retarget, soften_knees.py):

  smooth   Gaussian-smooth the bone curves of the video-derived clips (joins between the
           authored opening and the video, and tracking jitter, can jump a hip 10+ cm in one
           frame) and the hip height of every clip the knee pass lifted. Looping clips smooth
           around the loop.
  unflip   Make every bone's rotation keys sign-continuous. A quaternion and its negative are
           the same rotation, but keys that alternate sign interpolate the long way round,
           and edits that set bones from matrices (the knee pass) pick either sign.
  grip     Keep the racket in the hand. The racket is keyed in world space, so moving the body
           leaves it behind; `dump` records, per frame, where the racket sits relative to the
           racket hand in a reference file, and `grip` re-keys it on the edited body.

Run:  Blender --background REFERENCE.blend --python settle_clips.py -- dump OUT.json
      Blender --background STUDIO.blend --python settle_clips.py -- smooth [--save]
      Blender --background STUDIO.blend --python settle_clips.py -- grip OUT.json [--save]
"""
import json
import math
import sys

import bpy
from mathutils import Matrix

VIDEO = {"Serve", "Forehand", "Backhand", "Smash", "Celebrate"}
LOOPS = {"Ready", "RunLeft", "RunRight"}
LIFTED = {"Ready", "SplitStep", "RunLeft", "RunRight", "BrakeLeft", "BrakeRight", "DirectionChange", "Retreat",
          "RecoverLeft", "RecoverRight", "Forehand", "Backhand", "RunningForehand", "RunningBackhand", "ForehandTopspin",
          "Lob", "MissedSwing", "LowPickup", "Slice", "VolleyForehand", "VolleyBackhand", "Serve", "Smash"}


def channels(action):
    sys.path.insert(0, __import__("os").path.dirname(__file__))
    from retarget_intro_emotes import channels as ch
    return ch(action)


def track(rig):
    return next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))


def racket_of(scene, gender):
    prop = next(c for c in scene.collection.children if c.name.startswith(f"V4 PROP {gender} V4 racket"))
    return next(o for o in prop.objects if o.parent is None)


def gaussian(values, sigma, loop):
    n = len(values)
    if n < 3 or sigma <= 0: return values
    radius = int(math.ceil(sigma * 2.5))
    weights = [math.exp(-(k / sigma) ** 2 / 2) for k in range(-radius, radius + 1)]
    out = []
    for i in range(n):
        total = wsum = 0.0
        for k, w in zip(range(-radius, radius + 1), weights):
            j = i + k
            if loop: j %= n
            elif j < 0 or j >= n: continue
            total += values[j] * w; wsum += w
        out.append(total / wsum)
    return out


def smooth(scene, report, only=None):
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        for strip in track(rig).strips:
            clip = strip.name.rsplit(" ", 1)[0]
            if only and clip not in only: continue
            video, lifted = clip in VIDEO, clip in LIFTED
            if not (video or lifted): continue
            loop = clip in LOOPS
            touched = 0
            for fc in channels(strip.action):
                if not fc.data_path.startswith("pose.bones["): continue
                bone = fc.data_path.split('"')[1]
                hip_loc = bone == "Hips" and fc.data_path.endswith("location")
                if not (video or hip_loc): continue
                sigma = 1.6 if hip_loc else 1.1
                keys = sorted(fc.keyframe_points, key=lambda k: k.co.x)
                vals = gaussian([k.co.y for k in keys], sigma, loop)
                for k, v in zip(keys, vals):
                    k.co.y = v; k.handle_left.y = v; k.handle_right.y = v
                fc.update(); touched += 1
            report.append(f"{gender} {strip.name}: {touched} curves smoothed")


def unflip(report):
    flipped = 0
    for action in {s.action for g in ("Male", "Female") for s in track(bpy.data.objects[f"{g}_Tennis_Rig"]).strips}:
        quats = {}
        for fc in channels(action):
            if fc.data_path.startswith("pose.bones[") and fc.data_path.endswith("rotation_quaternion"):
                quats.setdefault(fc.data_path, {})[fc.array_index] = fc
        for path, comps in quats.items():
            if len(comps) != 4: continue
            keys = [sorted(comps[i].keyframe_points, key=lambda k: k.co.x) for i in range(4)]
            prev = None
            for j in range(min(len(k) for k in keys)):
                q = [keys[i][j].co.y for i in range(4)]
                if prev is not None and sum(a * b for a, b in zip(prev, q)) < 0:
                    for i in range(4):
                        k = keys[i][j]; k.co.y = -k.co.y; k.handle_left.y = -k.handle_left.y; k.handle_right.y = -k.handle_right.y
                    q = [-v for v in q]; flipped += 1
                prev = q
            for fc in comps.values(): fc.update()
    report.append(f"{flipped} flipped keys made continuous")


def dump(scene, path):
    out = {}
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]; racket = racket_of(scene, gender)
        for strip in track(rig).strips:
            bone = rig.pose.bones["Hand.L" if strip.name.endswith("LH") else "Hand.R"]
            rel = []
            for f in range(int(strip.frame_start), int(strip.frame_end) + 1):
                scene.frame_set(f)
                m = (rig.matrix_world @ bone.matrix).inverted() @ racket.matrix_world
                rel.append([list(r) for r in m])
            out[f"{gender}|{strip.name}"] = rel
    json.dump(out, open(path, "w"))
    print(f"SETTLE dumped {len(out)} strips", flush=True)


def grip(scene, path, report):
    rel = json.load(open(path))
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]; racket = racket_of(scene, gender)
        chans = channels(racket.animation_data.action)
        for strip in track(rig).strips:
            key = f"{gender}|{strip.name}"
            if key not in rel: continue
            bone = rig.pose.bones["Hand.L" if strip.name.endswith("LH") else "Hand.R"]
            first = int(strip.frame_start)
            frames = list(range(first, int(strip.frame_end) + 1))
            world = []
            for i, f in enumerate(frames):
                scene.frame_set(f)
                world.append((rig.matrix_world @ bone.matrix) @ Matrix(rel[key][min(i, len(rel[key]) - 1)]))
            for fc in chans:
                for kp in reversed(list(fc.keyframe_points)):
                    if first - .5 <= kp.co.x <= frames[-1] + .5: fc.keyframe_points.remove(kp)
            prev = None
            for f, m in zip(frames, world):
                racket.matrix_world = m
                if racket.rotation_mode != "QUATERNION": racket.rotation_mode = "QUATERNION"
                if prev is not None and racket.rotation_quaternion.dot(prev) < 0: racket.rotation_quaternion.negate()
                prev = racket.rotation_quaternion.copy()
                for ch in ("location", "rotation_quaternion", "scale"): racket.keyframe_insert(ch, frame=f)
            report.append(f"{gender} {strip.name}: racket re-keyed on {len(frames)} frames")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    report = []
    if argv[0] == "dump": dump(scene, argv[1]); return
    if argv[0] == "smooth": smooth(scene, report, set(argv[argv.index("--clips") + 1].split(",")) if "--clips" in argv else None)
    if argv[0] == "unflip": unflip(report)
    if argv[0] == "grip": grip(scene, argv[1], report)
    print("\n".join("SETTLE " + r for r in report[-6:]), f"\nSETTLE {len(report)} strips", flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("SETTLE saved", flush=True)


if __name__ == "__main__":
    main()
