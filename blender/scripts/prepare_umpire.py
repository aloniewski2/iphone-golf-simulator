"""The chair umpire: Higgsfield (Tripo H3.1) model -> a small rig -> Unity.

The model is generated already seated. It gets three bones so the game can bring him to life
without authored clips: Hips (root), Spine (lean in for a close call) and Head (follows the
ball, nods on a call). Everything above the collar belongs to the head; the torso blends
into the spine; the legs stay on the hips.

The model is scaled so he matches the players (a seated height of 1.3 m for these
proportions) and his origin is put where his seat is, so the game places him by the
chair's seat alone. A "UmpireFacing" marker sits in front of his face, so the game can turn
him toward the court whatever the export axes do.

Run:  Blender --background --python blender/scripts/prepare_umpire.py
"""
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "SportsLibrary/ArtDirection/Tennis/Generated/models/umpire.glb"
OUT = ROOT / "Unity/Assets/Resources/Tennis/Umpire/TennisUmpire.fbx"
TEXTURES = ROOT / "Unity/Assets/Resources/Tennis/Characters"
SEATED_HEIGHT = 1.30


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
    body = next(o for o in bpy.context.scene.objects if o.type == "MESH")
    for o in list(bpy.context.scene.objects):
        if o is not body: bpy.data.objects.remove(o, do_unlink=True)
    body.parent = None
    bpy.context.view_layer.objects.active = body; body.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    me = body.data
    lo = min(v.co.z for v in me.vertices); hi = max(v.co.z for v in me.vertices)
    s = SEATED_HEIGHT / (hi - lo)
    # Tripo faces +X; turn to face -Y like the players, scale, feet at 0.
    for v in me.vertices:
        x, y, z = v.co
        v.co = Vector((y * s, -x * s, (z - lo) * s))
    me.update()
    # The seat: the underside of the buttocks, i.e. the lowest point behind the knees.
    back = [v.co for v in me.vertices if v.co.y > .06 and v.co.z > .25]
    seat = min(p.z for p in back)
    seat_y = sum(p.y for p in back if p.z < seat + .05) / max(1, len([p for p in back if p.z < seat + .05]))
    for v in me.vertices: v.co -= Vector((0, seat_y, seat))
    me.update()
    top = max(v.co.z for v in me.vertices)
    # Collar: the narrowest slice between the shoulders and the head.
    def width(z):
        xs = [v.co.x for v in me.vertices if abs(v.co.z - z) < .01]
        return (max(xs) - min(xs)) if xs else 9
    candidates = [top * f for f in (.52, .54, .56, .58, .60, .62, .64)]
    neck = min(candidates, key=width)

    body.name = "V4 Higgs Umpire"; me.name = body.name; me.materials[0].name = body.name
    for node in me.materials[0].node_tree.nodes:
        if node.type != "TEX_IMAGE" or not node.image: continue
        kind = "Color" if node.image.name.startswith("Color") else "Normal" if node.image.name.startswith("Normal") else None
        if not kind: continue
        img = node.image.copy(); img.scale(*(2048, 2048) if kind == "Color" else (1024, 1024))
        img.filepath_raw = str(TEXTURES / f"HiggsUmpire_{kind}.png"); img.file_format = "PNG"; img.save()

    arm_data = bpy.data.armatures.new("Umpire rig"); rig = bpy.data.objects.new("Umpire rig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.ops.object.select_all(action="DESELECT"); rig.select_set(True); bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    hips = arm_data.edit_bones.new("Hips"); hips.head = (0, 0, 0); hips.tail = (0, 0, .15)
    spine = arm_data.edit_bones.new("Spine"); spine.head = (0, 0, .15); spine.tail = (0, 0, neck); spine.parent = hips
    head = arm_data.edit_bones.new("Head"); head.head = (0, 0, neck); head.tail = (0, 0, top); head.parent = spine
    bpy.ops.object.mode_set(mode="OBJECT")

    groups = {n: body.vertex_groups.new(name=n) for n in ("Hips", "Spine", "Head")}
    for v in me.vertices:
        z = v.co.z
        h = min(1, max(0, (z - (neck - .04)) / .08))            # head, blended over the collar
        sp = min(1, max(0, (z - .05) / .2)) * (1 - h)            # torso above the seat
        hp = 1 - h - sp
        for n, w in (("Head", h), ("Spine", sp), ("Hips", hp)):
            if w > 1e-3: groups[n].add([v.index], w, "REPLACE")
    body.parent = rig
    body.modifiers.new("Armature", "ARMATURE").object = rig
    bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
    bpy.ops.object.shade_smooth()

    facing = bpy.data.objects.new("UmpireFacing", None); bpy.context.scene.collection.objects.link(facing)
    facing.parent = rig; facing.location = (0, -1, neck)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=str(OUT), use_selection=True, object_types={"ARMATURE", "MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, path_mode="AUTO")
    print(f"UMPIRE seated height {SEATED_HEIGHT} seat at 0, collar {neck:.3f}, crown {top:.3f}, verts {len(me.vertices)}", flush=True)


main()
