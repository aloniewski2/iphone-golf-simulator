"""Two source fixes to the V4 tennis animation block.

1. Racket grip on the converted clips.
   The racket is a separate, unparented object whose world transform is baked per frame.
   Those keys stop at the end of the original V4 block (frame 3148), so the five clips that
   `author_tennis_clips.py` appended afterwards (Celebrate, ForehandTopspin, RecoverLeft,
   RecoverRight, Slice) played with the racket frozen in mid-air -- measured in Unity as a
   racket head that never moves during a topspin forehand or a slice. The grip in every
   original clip is perfectly rigid (0.0 mm / 0.0 deg drift), so the racket is re-baked from
   the gripping hand with that exact offset.

2. Two-handed backhand torso.
   Measured: the authored Forehand and Backhand rotate the chest by identical amounts, so the
   backhand has the forehand's body. A runtime layer faked the deep shoulder coil; this puts
   it into the animation itself. The coil builds into the backswing, peaks just before
   contact and unwinds through the ball -- the shape of the reference two-hander. The racket
   is re-baked relative to the new hand pose so the grip stays locked.

Run:  Blender --background sports-animation-studio-v4.blend --python fix_tennis_racket_and_backhand.py -- [--save] [--sign 1|-1]
"""
import math
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

V4_TRACK = "V4 | wardrobe + expanded motions"
CONVERTED = ("Celebrate", "ForehandTopspin", "RecoverLeft", "RecoverRight", "Slice")
COILED = ("Backhand", "RunningBackhand")
COIL_DEGREES = 42.0
# Where the runtime playhead enters and leaves a stroke clip, and where within that span the
# coil peaks (TennisRules.StrokeEntry/Exit and BackhandCoilAt).
ENTRY, EXIT, PEAK_SPAN = .26, .74, .35


def smoothstep(a, b, x):
    x = max(0.0, min(1.0, (x - a) / (b - a) if b != a else 1.0))
    return x * x * (3 - 2 * x)


def coil_at(phase):
    """Same curve as TennisRules.BackhandCoilAt, but over the whole clip: zero outside the
    played window, building to the peak, then releasing faster than it built."""
    span = (phase - ENTRY) / (EXIT - ENTRY)
    if span < 0:
        # Ease in from the start of the clip so preparation (which the game holds before a
        # swing) already shows some turn.
        return smoothstep(-0.6, 0, span) * 0.35
    if span < PEAK_SPAN:
        return 0.35 + 0.65 * smoothstep(0, PEAK_SPAN, span)
    return 1 - smoothstep(PEAK_SPAN, PEAK_SPAN + 0.5, span)


def channelbag(action):
    return action.layers[0].strips[0].channelbag(action.slots[0])


def curves_for(action, bone, prop):
    bag = channelbag(action)
    path = f'pose.bones["{bone}"].{prop}'
    return sorted((f for f in bag.fcurves if f.data_path == path), key=lambda f: f.array_index)


def strip_frame(strip, action_frame):
    a0, a1 = strip.action_frame_start, strip.action_frame_end
    return strip.frame_start + (action_frame - a0) * (strip.frame_end - strip.frame_start) / max(1e-6, a1 - a0)


def set_frame(scene, f):
    whole = int(math.floor(f))
    scene.frame_set(whole, subframe=f - whole)


def key_racket(racket, frame, matrix):
    racket.matrix_world = matrix
    racket.keyframe_insert("location", frame=frame)
    racket.keyframe_insert("rotation_quaternion", frame=frame)
    racket.keyframe_insert("scale", frame=frame)


def fix(gender, sign, report):
    scene = bpy.data.scenes["03 TENNIS"]
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    prop = next(c for c in scene.collection.children if c.name.startswith(f"V4 PROP {gender} V4 racket"))
    racket = next(o for o in prop.objects if o.parent is None)
    track = next(t for t in rig.animation_data.nla_tracks if t.name == V4_TRACK)
    strips = {s.name: s for s in track.strips}

    def hand_matrix(hand):
        return rig.matrix_world @ rig.pose.bones["Hand." + ("R" if hand == "RH" else "L")].matrix

    # Rigid grip offset per hand, from the forehand, which is known to be clean.
    grip = {}
    for hand in ("RH", "LH"):
        st = strips[f"Forehand {hand}"]
        set_frame(scene, (st.frame_start + st.frame_end) / 2)
        grip[hand] = hand_matrix(hand).inverted() @ racket.matrix_world.copy()

    # 1. Converted clips: re-bake the racket from the hand, every frame.
    for clip in CONVERTED:
        for hand in ("RH", "LH"):
            st = strips.get(f"{clip} {hand}")
            if not st:
                continue
            frames = range(int(st.frame_start), int(st.frame_end) + 1)
            mats = []
            for f in frames:
                set_frame(scene, f)
                mats.append(hand_matrix(hand) @ grip[hand])
            for f, m in zip(frames, mats):
                key_racket(racket, f, m)
            report.append(f"{gender} {clip} {hand}: racket re-baked over {len(mats)} frames")

    # 2. Backhand coil in the source.
    for clip in COILED:
        for hand in ("RH", "LH"):
            st = strips.get(f"{clip} {hand}")
            if not st:
                continue
            side = sign * (1 if hand == "RH" else -1)
            frames = list(range(int(st.frame_start), int(st.frame_end) + 1))
            before = {}
            for f in frames:
                set_frame(scene, f)
                before[f] = (hand_matrix(hand), racket.matrix_world.copy())
            action = st.action
            chest = rig.pose.bones["Chest"]
            spine = chest.parent
            quats = curves_for(action, "Chest", "rotation_quaternion")
            if len(quats) != 4:
                raise SystemExit(f"{action.name}: Chest has no quaternion curves")
            times = sorted({round(k.co.x, 4) for fc in quats for k in fc.keyframe_points})
            up = rig.matrix_world.inverted().to_3x3() @ Vector((0, 0, 1))
            new_values = []
            for t in times:
                f = strip_frame(st, t)
                set_frame(scene, f)
                phase = (f - st.frame_start) / max(1, st.frame_end - st.frame_start)
                angle = math.radians(COIL_DEGREES) * coil_at(phase) * side
                # Chest pose = P @ Rot(q); a rotation about the armature's vertical through
                # the chest head is Rot(axis', angle) in the chest's own frame, where axis'
                # is the vertical expressed in P's rotation.
                parent = spine.matrix @ spine.bone.matrix_local.inverted() @ chest.bone.matrix_local
                axis = (parent.to_3x3().normalized().inverted() @ up).normalized()
                q = Quaternion([fc.evaluate(t) for fc in quats])
                new_values.append((t, (Quaternion(axis, angle) @ q).normalized()))
            for t, q in new_values:
                for i, fc in enumerate(quats):
                    fc.keyframe_points.insert(t, q[i], options={"REPLACE", "FAST"})
            for fc in quats:
                fc.update()
            # Racket follows the re-posed hand with the grip it had.
            for f in frames:
                set_frame(scene, f)
                old_hand, old_racket = before[f]
                key_racket(racket, f, hand_matrix(hand) @ old_hand.inverted() @ old_racket)
            set_frame(scene, st.frame_start + .43 * (st.frame_end - st.frame_start))
            report.append(f"{gender} {clip} {hand}: chest coil {COIL_DEGREES:.0f} deg (side {side:+d}) over {len(times)} keys")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    sign = int(argv[argv.index("--sign") + 1]) if "--sign" in argv else 1
    bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
    report = []
    for gender in ("Male", "Female"):
        fix(gender, sign, report)
    print("\n".join("FIX " + r for r in report), flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile()
        print("FIX saved", bpy.data.filepath, flush=True)


main()
