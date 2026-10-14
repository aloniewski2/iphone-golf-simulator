"""The authored Drive shaft at 2.50 s (frame index 75, Blender frame 76), as GolfSwingClearance.driveReleaseShaft wants it (Unity model space).
    Blender -b --factory-startup --python release_shaft.py -- <golfer fbx>"""
import bpy, sys
from mathutils import Vector
fbx = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
club = next(o for o in bpy.data.objects if o.name == "CLUB_DRIVER")
far = max((v.co.copy() for v in club.data.vertices), key=lambda v: v.length_squared)      # (the Club bone rests at the origin: mesh space = bone space)
act = next(a for a in bpy.data.actions if a.name.split("|")[-1] == "Drive")
rig.animation_data.action = act
if len(act.slots): rig.animation_data.action_slot = act.slots[0]
for f in (76,):
    bpy.context.scene.frame_set(f)
    M = rig.matrix_world @ rig.pose.bones["Club"].matrix
    s = (M.to_3x3() @ far).normalized()
    print(f"SHAFT {fbx} frame {f}: blender {tuple(round(x, 7) for x in s)}  unity(-x,z,-y) {(-s.x, s.z, -s.y)}  unity(x,z,y) {(s.x, s.z, s.y)}")
