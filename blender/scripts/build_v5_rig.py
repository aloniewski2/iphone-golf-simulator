"""V5 proportions for the tennis rigs (plan phase B), matching the concept video's character.

The rigs are edited in place -- same objects, same 22 bone names, same NLA strips -- so the
exporter, the Unity importer, TennisActor and the tests keep working. Only bone positions and
lengths change; every bone keeps its V4 direction, so the clips' local rotations still mean the
same poses. What does not carry over automatically is the hips' travel: a crouch or a stride
on shorter legs moves the pelvis less, so every Hips location key is scaled by the leg ratio.

  total height 1.69 m (head crown), head 28% of it (1.22-1.69), shoulders kept near the V4
  1.2 m so racket contact heights barely move, hips 0.60 m (crotch at 33%), shoulders narrower
  than the head (joints at +-0.19 m), slim arms a touch shorter, big shoes (ankle 0.12 m).

Run:  Blender --background STUDIO.blend --python build_v5_rig.py -- [--save]
"""
import sys

import bpy
from mathutils import Vector

# bone: (head, tail) for the left side / centre; right side mirrors x.
LAYOUT = {
    "Root": ((0, 0, 0), (0, 0, .15)),
    "Hips": ((0, 0, .60), (0, 0, .72)),
    "Spine": ((0, 0, .72), (0, 0, .92)),
    "Chest": ((0, 0, .92), (0, 0, 1.14)),
    "Neck": ((0, 0, 1.14), (0, 0, 1.22)),
    "Head": ((0, 0, 1.22), (0, 0, 1.69)),
    "Shoulder.L": ((0, 0, 1.13), (.19, 0, 1.12)),
    "UpperArm.L": ((.19, 0, 1.12), (.236, 0, .925)),
    "LowerArm.L": ((.236, 0, .925), (.275, -.005, .74)),
    "Hand.L": ((.275, -.005, .74), (.283, -.016, .65)),
    "UpperLeg.L": ((.10, 0, .60), (.117, 0, .36)),
    "LowerLeg.L": ((.117, 0, .36), (.125, 0, .12)),
    "Foot.L": ((.125, 0, .12), (.125, -.15, .06)),
    "Toes.L": ((.125, -.15, .06), (.125, -.25, .06)),
}
V4_LEG, V5_LEG = .76 - .13, .60 - .12          # hip joint to ankle


def layout():
    out = {}
    for name, (h, t) in LAYOUT.items():
        out[name] = (Vector(h), Vector(t))
        if name.endswith(".L"):
            out[name[:-2] + ".R"] = (Vector((-h[0], h[1], h[2])), Vector((-t[0], t[1], t[2])))
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    bones = layout()
    actions = set()
    for gender in ("Male", "Female"):
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
        for o in bpy.context.view_layer.objects: o.select_set(False)
        rig.select_set(True); bpy.context.view_layer.objects.active = rig
        bpy.ops.object.mode_set(mode="EDIT")
        for eb in rig.data.edit_bones:
            if eb.name not in bones: continue
            h, t = bones[eb.name]
            eb.use_connect = False
            eb.head, eb.tail = h, t
            eb.roll = 0
        bpy.ops.object.mode_set(mode="OBJECT")
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        actions |= {s.action for s in track.strips}
    # Hips travel scales with the legs.
    sys.path.insert(0, __import__("os").path.dirname(__file__))
    from retarget_intro_emotes import channels
    k = V5_LEG / V4_LEG
    scaled = 0
    for action in actions:
        for fc in channels(action):
            if fc.data_path == 'pose.bones["Hips"].location':
                for kp in fc.keyframe_points:
                    kp.co.y *= k; kp.handle_left.y *= k; kp.handle_right.y *= k
                fc.update(); scaled += 1
    print(f"V5RIG rebuilt Male/Female rigs; {scaled} hip location curves scaled x{k:.3f} across {len(actions)} actions", flush=True)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("V5RIG saved", flush=True)


if __name__ == "__main__":
    main()
