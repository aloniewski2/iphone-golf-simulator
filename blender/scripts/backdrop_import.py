"""Bring a generated backdrop model (a Higgsfield / Tripo image-to-3D GLB) into the game, light.

    Blender -b --python blender/scripts/backdrop_import.py -- <in.glb> <name> [faces] [texture px]

The generated meshes are dense (hundreds of thousands of faces, 2–4K textures) for things the
game only ever sees across the water. This joins the model into one mesh, decimates it to about
`faces` (default 3000), shrinks its base-colour texture to `texture px` (default 512), drops any
other maps, sets it on the ground at the origin with its longest side along X, one unit = one
metre of the source's own scale (HoleView scales and places it), and writes:

  Unity/Assets/Resources/Course/Backdrop/<name>.fbx and <name>.png
  blender/previews/backdrop_<name>.png
"""
import bpy, os, sys, math
from mathutils import Vector

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
args = sys.argv[sys.argv.index("--") + 1:]
src, name = args[0], args[1]
faces = int(args[2]) if len(args) > 2 else 3000
tex_px = int(args[3]) if len(args) > 3 else 512
OUT = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "Backdrop")
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
print("imported", len(meshes), "meshes,", sum(len(o.data.polygons) for o in meshes), "faces")
for o in bpy.context.scene.objects: o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
ob.name = name.upper()
bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# lighter: decimate to the budget
have = len(ob.data.polygons)
if have > faces:
    dec = ob.modifiers.new("decimate", 'DECIMATE'); dec.ratio = faces / have
    bpy.ops.object.modifier_apply(modifier=dec.name)
print("faces now", len(ob.data.polygons))

# on the ground at the origin, longest side along X
bb = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
lo = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
hi = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
ob.location -= Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
bpy.ops.object.transform_apply(location=True)
if hi.y - lo.y > hi.x - lo.x:
    ob.rotation_euler.z = math.pi / 2; bpy.ops.object.transform_apply(rotation=True)
size = hi - lo
print("size", tuple(round(c, 3) for c in size))

# one base-colour texture, small, saved beside the FBX; every other map dropped
texture = None
for slot in ob.material_slots:
    m = slot.material
    if not m or not m.use_nodes: continue
    bsdf = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if not bsdf: continue
    link = bsdf.inputs["Base Color"].links
    if link and link[0].from_node.type == 'TEX_IMAGE' and link[0].from_node.image:
        texture = link[0].from_node.image
    for socket in ("Metallic", "Roughness", "Normal", "Emission Color"):
        if socket in bsdf.inputs:
            for l in list(bsdf.inputs[socket].links): m.node_tree.links.remove(l)
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.8
    m.name = "MAT_" + name.upper()
if texture:
    texture.scale(tex_px, tex_px)
    png = os.path.join(OUT, name + ".png")
    texture.filepath_raw = png; texture.file_format = 'PNG'; texture.save()
    print("texture", png)

fbx = os.path.join(OUT, name + ".fbx")
bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                         axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True,
                         mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False)
print("fbx", fbx, os.path.getsize(fbx) // 1024, "KB")

# a preview render
sc = bpy.context.scene
cam = bpy.data.objects.new("CAM", bpy.data.cameras.new("CAM")); sc.collection.objects.link(cam)
reach = max(size) * 1.6
cam.location = (reach * 0.8, -reach, size.z * 0.9 + reach * 0.35)
cam.rotation_euler = (Vector((0, 0, size.z * 0.4)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.camera = cam
sun = bpy.data.objects.new("SUN", bpy.data.lights.new("SUN", 'SUN')); sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sun.data.energy = 3
world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.45, 0.75, 1.0, 1)
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'CYCLES'
sc.render.resolution_x = sc.render.resolution_y = 600
prev = os.path.join(REPO, "blender", "previews", f"backdrop_{name}.png")
sc.render.filepath = prev
bpy.ops.render.render(write_still=True)
print("preview", prev)
