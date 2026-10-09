"""Side-by-side look at generated outfit models (Tripo vs Higgsfield) on our tennis rigs.

Each model is imported into the studio, joined, scaled to the rig's standing height (feet on
the court, centred on the rig), bound to the V5 rig with weights copied from our body mesh, and rendered with
its own textures: at rest (front, three-quarter, back) and posed at key frames of the
rotomated clips (Ready, Serve trophy/contact, Forehand contact, Backhand finish).

Run:  Blender --background STUDIO.blend --python kit_compare.py -- OUT_DIR Male=a.glb,b.fbx Female=c.glb,d.fbx
The studio is not saved.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))


def import_model(path):
    before = set(bpy.data.objects)
    if path.lower().endswith(".fbx"): bpy.ops.import_scene.fbx(filepath=path)
    else: bpy.ops.import_scene.gltf(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    for o in new:
        if o.type != "MESH": o.hide_render = True
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    # Bake parent transforms, then join into one body.
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(meshes) > 1: bpy.ops.object.join()
    return bpy.context.view_layer.objects.active, [o for o in new if o.type != "MESH"]


def fit_to_rig(body, rig):
    """Upright (+Z), feet at the rig's floor, as tall as the rig's head top, centred."""
    vs = [body.matrix_world @ v.co for v in body.data.vertices]
    lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    ext = hi - lo
    # glTF/FBX may arrive Y-up if the importer did not convert; stand the tallest axis up.
    if ext.y > ext.z * 1.3:
        body.rotation_euler = (1.5708, 0, 0); bpy.context.view_layer.update()
        bpy.ops.object.transform_apply(rotation=True)
        vs = [body.matrix_world @ v.co for v in body.data.vertices]
        lo = Vector([min(v[i] for v in vs) for i in range(3)]); hi = Vector([max(v[i] for v in vs) for i in range(3)])
    head_top = (rig.matrix_world @ rig.data.bones["Head"].tail_local).z
    floor = rig.matrix_world.translation.z
    s = (head_top - floor) / (hi.z - lo.z)
    centre = (lo + hi) / 2
    body.scale = (s, s, s)
    body.location = Vector((rig.matrix_world.translation.x - centre.x * s, rig.matrix_world.translation.y - centre.y * s, floor - lo.z * s))
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # Face the rig's front (-Y): the design images are front views facing the camera, which
    # the importers leave facing -Y already.


def bind(body, rig, gender):
    """Bone-heat weights (Blender's automatic weights). Generated meshes are often not
    watertight and heat can fail for some bones; any vertex left unweighted then takes its
    weights from our own body mesh, nearest surface."""
    rig.data.pose_position = "REST"; bpy.context.view_layer.update()
    if len(body.data.vertices) > 80000:
        # Game budget (and a fair comparison): about 60k vertices.
        dec = body.modifiers.new("budget", "DECIMATE"); dec.ratio = 60000 / len(body.data.vertices)
        bpy.context.view_layer.objects.active = body; bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except RuntimeError as e:
        print(f"KIT bone heat failed ({e}); weights from the body mesh", flush=True)
        for g in list(body.vertex_groups): body.vertex_groups.remove(g)
        bpy.ops.object.parent_set(type="ARMATURE_NAME")
    missing = [v.index for v in body.data.vertices if sum(g.weight for g in v.groups) < 1e-3]
    if missing:
        source = max((o for o in bpy.data.collections[f"V4 {gender} Tennis | BODY"].objects if o.type == "MESH" and "face" not in o.name),
                     key=lambda o: len(o.data.vertices))
        for g in source.vertex_groups:
            if g.name not in body.vertex_groups: body.vertex_groups.new(name=g.name)
        holes = body.vertex_groups.new(name="_unweighted"); holes.add(missing, 1.0, "REPLACE")
        dt = body.modifiers.new("fill", "DATA_TRANSFER"); dt.object = source; dt.vertex_group = holes.name
        dt.use_vert_data = True; dt.data_types_verts = {"VGROUP_WEIGHTS"}; dt.vert_mapping = "POLYINTERP_NEAREST"
        dt.layers_vgroup_select_src = "ALL"; dt.layers_vgroup_select_dst = "NAME"
        bpy.context.view_layer.objects.active = body
        bpy.ops.object.modifier_move_to_index(modifier=dt.name, index=0)
        bpy.ops.object.modifier_apply(modifier=dt.name)
        body.vertex_groups.remove(body.vertex_groups["_unweighted"])
    print(f"KIT weights: {len(missing)} of {len(body.data.vertices)} vertices filled from the body mesh", flush=True)
    rig.data.pose_position = "POSE"


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    out = Path(argv[0]); out.mkdir(parents=True, exist_ok=True)
    jobs = {}
    for a in argv[1:]:
        g, files = a.split("="); jobs[g] = files.split(",")
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items} else "BLENDER_EEVEE"
    scene.view_settings.view_transform = "Standard"; scene.view_settings.look = "None"
    scene.render.resolution_x = 520; scene.render.resolution_y = 680; scene.render.film_transparent = False
    world = scene.world or bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (1, 1, 1, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
    cam = bpy.data.objects.new("Kit cam", bpy.data.cameras.new("Kit cam")); scene.collection.objects.link(cam); cam.data.lens = 60; scene.camera = cam
    sun = bpy.data.objects.new("Kit sun", bpy.data.lights.new("Kit sun", "SUN")); scene.collection.objects.link(sun)
    sun.data.energy = 3.0; sun.rotation_euler = (0.9, 0.2, 0.5)
    for o in scene.objects:
        if o.type in {"MESH", "CURVE"}: o.hide_render = True
    for gender, files in jobs.items():
        rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
        track = next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))
        strips = {s.name: s for s in track.strips}
        poses = [("rest", None), ("ready", ("Ready RH", .2)), ("trophy", ("Serve RH", .33)), ("serve", ("Serve RH", .62)),
                 ("forehand", ("Forehand RH", .55)), ("backhand", ("Backhand RH", .8))]
        for path in files:
            path, _, turn = path.partition("@")      # "@DEG": turn a sideways model about Z first
            tag = Path(path).stem
            body, extra = import_model(path)
            if turn:
                body.rotation_euler = (0, 0, math.radians(float(turn))); bpy.context.view_layer.update()
                bpy.ops.object.transform_apply(rotation=True)
            fit_to_rig(body, rig)
            bind(body, rig, gender)
            body.hide_render = False
            c = rig.matrix_world.translation + Vector((0, 0, .85))
            for name, pose in poses:
                if pose:
                    s = strips[pose[0]]; scene.frame_set(int(s.frame_start + (s.frame_end - s.frame_start) * pose[1]))
                    views = (("three", Vector((2.2, -3.2, .5))),)
                else:
                    scene.frame_set(int(strips["Ready RH"].frame_start))
                    rig.data.pose_position = "REST"
                    views = (("front", Vector((0, -4.2, .3))), ("three", Vector((2.6, -3.3, .5))), ("back", Vector((0, 4.2, .3))))
                for view, d in views:
                    cam.location = c + d; cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
                    scene.render.filepath = str(out / f"{gender}-{tag}-{name}-{view}.png")
                    bpy.ops.render.render(write_still=True)
                rig.data.pose_position = "POSE"
            body.hide_render = True
            print(f"KIT {gender} {tag}: {len(body.data.vertices)} verts", flush=True)


if __name__ == "__main__":
    main()
