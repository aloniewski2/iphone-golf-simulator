"""Fit the campaign opponents onto the tennis rigs and export their bodies for Unity.

Each opponent is a Higgsfield design (Generated/models/oppN-*-apose-design.png) turned into a
Tripo H3.1 model (Generated/models/opp-*.glb). fit_higgs_characters.fit_character puts it on
the male or female tennis rig exactly as it does the players' own bodies; this script then
exports just the rig, the fitted body and its face decal, with no animation, to
Unity/Assets/Resources/Tennis/Opponents/<Name>.fbx. At runtime TennisActor.WearBody rebinds
that body onto the standard character, whose clips drive every body alike.

The studio file is opened fresh for every opponent and never saved.

Run:  Blender --background --python fit_opponents.py -- [Name ...] [--render DIR]
"""
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import fit_higgs_characters as fit  # noqa: E402

ROOT = HERE.parents[1]
STUDIO = ROOT / "SportsLibrary/Blender/sports-animation-studio-v4.blend"
OUT = ROOT / "Unity/Assets/Resources/Tennis/Opponents"
# Name (also the Unity key and texture name), rig, model. Measured with measure_landmarks.py,
# all four share the players' proportions (they were designed from the same reference), so
# each uses its rig's landmarks.
OPPONENTS = [
    ("Milo", "Male", "opp-milo.glb"),
    ("Suki", "Female", "opp-suki.glb"),
    ("Bruno", "Male", "opp-bruno.glb"),
    ("Viktor", "Male", "opp-viktor.glb"),
]


def export(name, gender, rig):
    collection = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    parts = [o for o in collection.objects if o.name.startswith(("V4 Higgs body", "V4 face decal"))]
    # A body that brings its own fists (the base avatars) ships them too.
    if name.startswith("Avatar"): parts += [o for o in collection.objects if o.name.startswith("V4 grip hand")]
    rig.animation_data_clear()
    rig.location = Vector((0, 0, 0))
    bpy.ops.object.select_all(action="DESELECT")
    for o in [rig] + parts:
        o.hide_set(False); o.hide_viewport = False; o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    OUT.mkdir(parents=True, exist_ok=True)
    # The standard characters' export settings (export_tennis_runtime.py), minus animation.
    bpy.ops.export_scene.fbx(filepath=str(OUT / f"{name}.fbx"), use_selection=True,
        object_types={"ARMATURE", "MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True, add_leaf_bones=False,
        armature_nodetype="NULL", bake_anim=False, path_mode="AUTO")
    return parts


def render(name, gender, rig, directory, frames):
    """Front and three-quarter views in a few authored poses, for review."""
    scene = bpy.context.scene
    collection = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    keep = set(collection.objects) | {rig}
    for o in scene.objects:
        if o.type == "MESH" and o not in keep and "grip hand" not in o.name: o.hide_render = True
    cam_data = bpy.data.cameras.new("Review"); cam_data.lens = 50
    cam = bpy.data.objects.new("Review camera", cam_data); scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "TEXTURE"
    scene.render.resolution_x = 540; scene.render.resolution_y = 720
    target = rig.location + Vector((0, 0, 1.05))
    for frame in frames:
        scene.frame_set(frame)
        for view, offset in (("front", Vector((0, -5.2, .3))), ("side", Vector((3.8, -3.8, .6)))):
            cam.location = target + offset
            cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = str(Path(directory) / f"{name}-{frame:04d}-{view}.png")
            bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    directory = argv[argv.index("--render") + 1] if "--render" in argv else None
    wanted = [a for a in argv if not a.startswith("--") and a != directory]
    for name, gender, glb in OPPONENTS:
        if wanted and name not in wanted: continue
        bpy.ops.wm.open_mainfile(filepath=str(STUDIO))
        bpy.context.window.scene = bpy.data.scenes["03 TENNIS"]
        report = []
        rig, body = fit.fit_character(gender, report, fit.MODELS / glb, name)
        print("\n".join("FIT " + r for r in report), flush=True)
        if directory: render(name, gender, rig, directory, (1, 700, 1500))
        bpy.context.scene.frame_set(1)
        parts = export(name, gender, rig)
        print(f"OPPONENT {name}: exported {[p.name for p in parts]}", flush=True)


if __name__ == "__main__":
    main()
