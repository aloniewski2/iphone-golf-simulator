"""Point and match emotes: motion capture from the Higgsfield/Meshy library, retargeted onto
the V4 tennis rigs exactly as the intro emotes are (see retarget_intro_emotes.py).

    Celebrate  winning a point outright (a winner, an ace, a long rally)  - Victory_Fist_Pump
    Win        winning the match                                            - victory
    Upset      losing a point that hurt (a long rally, a double fault)      - Head_Hold_in_Pain

Each is keyed as "<Name> RH" and "<Name> LH" strips on the V4 track (replacing a strip of the
same name, else appended past the end), so the export publishes them as clips. The racket is
keyed to follow the racket hand throughout.

Run:  Blender --background STUDIO.blend --python retarget_emotes.py -- Name=file.glb[:first:last] ... [--save]
"""
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from retarget_intro_emotes import GAP, channels, import_source, racket_offset, retarget  # noqa: E402


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    specs = []
    for a in argv:
        if "=" not in a: continue
        name, rest = a.split("=", 1)
        parts = rest.split(":")
        trim = (float(parts[1]), float(parts[2])) if len(parts) == 3 else None
        specs.append((name, parts[0], trim))
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    report = []
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        prop = next(c for c in scene.collection.children if c.name.startswith("V4 PROP " + gender + " V4 racket"))
        racket = next(o for o in prop.objects if o.parent is None)
        strips = {s.name: s for s in track.strips}
        offsets = {"RH": racket_offset(rig, racket, "Hand.R", int(strips["Ready RH"].frame_start) + 1),
                   "LH": racket_offset(rig, racket, "Hand.L", int(strips["Ready LH"].frame_start) + 1)}
        for name, path, trim in specs:
            track.mute = True
            action = bpy.data.actions.new(f"{gender}__Tennis__{name}__Mocap")
            src, imported = import_source(path)
            frames = retarget(src, rig, action, 1, trim, trunk_align=False)
            for o in imported: bpy.data.objects.remove(o, do_unlink=True)
            track.mute = False
            for hand in ("RH", "LH"):
                # Replace any earlier take, and append past the end of the block so a longer
                # take never overlaps the clip after it.
                for old in [s for s in track.strips if s.name == f"{name} {hand}"]: track.strips.remove(old)
                start = max(int(s.frame_end) for s in track.strips) + GAP + 1
                strip = track.strips.new(f"{name} {hand}", start, action)
                strip.name = f"{name} {hand}"
                strip.extrapolation = "HOLD"
                bone = "Hand.R" if hand == "RH" else "Hand.L"
                last = int(strip.frame_end)
                for fc in channels(racket.animation_data.action):
                    for kp in reversed(list(fc.keyframe_points)):
                        if start - .5 <= kp.co.x <= last + .5: fc.keyframe_points.remove(kp)
                for f in range(start, last + 1):
                    scene.frame_set(f)
                    racket.matrix_world = (rig.matrix_world @ rig.pose.bones[bone].matrix) @ offsets[hand]
                    racket.keyframe_insert("location", frame=f)
                    racket.keyframe_insert("rotation_quaternion" if racket.rotation_mode == "QUATERNION" else "rotation_euler", frame=f)
                    racket.keyframe_insert("scale", frame=f)
                report.append(f"{gender} {name} {hand}: {start}-{last} ({frames} frames)")
            scene.frame_end = max(scene.frame_end, max(int(s.frame_end) for s in track.strips))
    print("\n".join("EMOTE " + r for r in report), flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("EMOTE saved", flush=True)


if __name__ == "__main__":
    main()
