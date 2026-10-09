"""Seated spectators for the stands: light copies of the fitted Higgsfield players.

The stands held primitive blob "spectators" (orange shirt shapes) -- the most obviously
unfinished thing in the background. They are replaced by the real characters, sitting:

  * each gender's fitted body, face decal and hands, decimated for the phone (a crowd of 36
    at full resolution would skin more vertices than both players several times over)
  * on a copy of the V4 rig, with two mocap clips from the Higgsfield/Meshy library
    retargeted on: SitIdle (loops) and SitCheer (a seated fist-raise, played on big points)
  * exported to Unity/Assets/Resources/Tennis/Crowd/crowd_<gender>.fbx, clip ranges and
    the seat offset (root to seat surface) in blender/crowd-clips.json

Run:  Blender --background sports-animation-studio-v4.blend --python build_crowd.py -- \
          --idle sit_idle.glb --cheer sit_cheer.glb
Nothing is saved back to the studio file.
"""
import json
import sys
from pathlib import Path

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import retarget_intro_emotes as rt   # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Unity/Assets/Resources/Tennis/Crowd"
BODY_FACES = 4200
HAND_FACES = 500
GAP = 10


def decimate(obj, faces):
    have = len(obj.data.polygons)
    if have <= faces: return
    mod = obj.modifiers.new("Crowd LOD", "DECIMATE"); mod.ratio = faces / have
    bpy.context.view_layer.objects.active = obj
    # The armature modifier must stay last: move the decimate to the top and apply it.
    while obj.modifiers.find(mod.name) > 0: bpy.ops.object.modifier_move_up(modifier=mod.name)
    bpy.ops.object.modifier_apply(modifier=mod.name)


def build(gender, idle, cheer, report):
    scene = bpy.context.scene
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    rig.data.pose_position = "REST"
    crowd = bpy.data.collections.new(f"Crowd {gender}"); scene.collection.children.link(crowd)
    crig = rig.copy(); crig.data = rig.data.copy(); crig.animation_data_clear(); crig.name = f"Crowd {gender} rig"
    crowd.objects.link(crig)
    crig.data.pose_position = "POSE"
    for pb in crig.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    parts = []
    for o in bpy.data.collections[f"V4 {gender} Tennis | BODY"].objects:
        c = o.copy(); c.data = o.data.copy(); crowd.objects.link(c)
        world = o.matrix_world.copy(); c.parent = crig; c.matrix_world = world
        for m in c.modifiers:
            if m.type == "ARMATURE": m.object = crig
        if "Higgs body" in o.name: decimate(c, BODY_FACES)
        elif "grip hand" in o.name: decimate(c, HAND_FACES)
        parts.append(c)
    rig.data.pose_position = "POSE"
    # Clips.
    crig.animation_data_create()
    track = crig.animation_data.nla_tracks.new(); track.name = "Crowd"
    clips, start = [], 1
    for name, path in (("SitIdle", idle), ("SitCheer", cheer)):
        action = bpy.data.actions.new(f"Crowd {gender} {name}")
        src, imported = rt.import_source(path)
        frames = rt.retarget(src, crig, action, 1)
        for o in imported: bpy.data.objects.remove(o, do_unlink=True)
        strip = track.strips.new(name, start, action)
        clips.append({"name": name, "firstFrame": start - 1, "lastFrame": start - 1 + frames - 1, "loop": name == "SitIdle"})
        start += frames + GAP
    # Seat offset: where the backside is when sitting, measured on the first idle frame.
    scene.frame_set(2)
    hips = crig.matrix_world @ crig.pose.bones["Hips"].head
    seat = hips.z - crig.matrix_world.translation.z - .14
    # Centre on the origin, facing -Y, for export.
    delta = crig.location.copy(); crig.location -= delta
    bpy.ops.object.select_all(action="DESELECT")
    for o in [crig] + parts: o.select_set(True)
    bpy.context.view_layer.objects.active = crig
    scene.frame_start = 1; scene.frame_end = start - GAP
    OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(OUT / f"crowd_{gender.lower()}.fbx"), use_selection=True,
                             object_types={"ARMATURE", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, add_leaf_bones=False,
                             armature_nodetype="NULL", bake_anim=True, bake_anim_use_all_bones=True,
                             bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False, bake_anim_step=1,
                             bake_anim_simplify_factor=0, path_mode="AUTO")
    faces = sum(len(p.data.polygons) for p in parts)
    report.append(f"{gender}: {faces} faces, clips {clips}, seat offset {seat:.3f}")
    return {"clips": clips, "seatOffset": round(seat, 3)}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    args = {argv[i]: argv[i + 1] for i in range(len(argv) - 1) if argv[i].startswith("--")}
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    # The tennis rigs' own animation must not drive anything while the crowd is built.
    for g in ("Male", "Female"):
        for t in bpy.data.objects[f"{g}_Tennis_Rig"].animation_data.nla_tracks: t.mute = True
    report, manifest = [], {}
    for gender in ("Male", "Female"):
        manifest[gender.lower()] = build(gender, args["--idle"], args["--cheer"], report)
    (ROOT / "blender/crowd-clips.json").write_text(json.dumps(manifest, indent=1))
    print("\n".join("CROWD " + r for r in report), flush=True)


main()
