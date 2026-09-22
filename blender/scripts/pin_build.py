"""The pin: a cup with its white liner, a fibreglass flagstick and a fluttering flag.

    Blender -b --python blender/scripts/pin_build.py -- [--render]

Built in metres with the cup's centre at the origin and z = 0 on the putting surface, so the
game drops it on the ground at the pin and scales metres to yards. Saves blender/pin.blend,
exports Unity/Assets/Resources/Course/pin.fbx (HoleView takes CUP, FLAG_POLE and FLAG from it,
recoloured by material name) and, with --render, blender/previews/pin.png.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
BLEND = os.path.join(REPO, "blender", "pin.blend")
FBX = os.path.join(REPO, "Unity", "Assets", "Resources", "Course", "pin.fbx")
PREVIEW = os.path.join(REPO, "blender", "previews", "pin.png")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []

CUP_RADIUS = 0.054      # a regulation 4¼" cup
RIM = 0.014             # the liner's lip, proud of the turf
POLE_HEIGHT = 2.13      # 7 ft
POLE_RADIUS = 0.011
FLAG_W, FLAG_H = 0.50, 0.35

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.unit_settings.system = 'METRIC'


def srgb(r, g, b):
    def lin(c): c /= 255; return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), 1.0)


def material(name, rgb, roughness=0.8):
    m = bpy.data.materials.new(name)
    if not m.use_nodes: m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = srgb(*rgb)
    bsdf.inputs["Roughness"].default_value = roughness
    return m


MAT_CUP = material("MAT_CUP", (28, 28, 28), 0.9)
MAT_CUP_RIM = material("MAT_CUP_RIM", (245, 245, 240), 0.5)
MAT_POLE = material("MAT_POLE", (240, 240, 240), 0.4)
MAT_FLAG = material("MAT_FLAG", (232, 40, 40), 0.8)
MAT_POLE_BAND = material("MAT_POLE_BAND", (250, 200, 40), 0.5)


def new_object(name, bm, mats, parent):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    for m in mats: me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    sc.collection.objects.link(ob)
    ob.parent = parent
    return ob


root = bpy.data.objects.new("PIN_ROOT", None)
sc.collection.objects.link(root)

# ---- the cup: the liner's lip as a ring on the turf, the dark hole inside it, and the liner
# wall down to the bottom (seen from above through the mouth).
bm = bmesh.new()
segs = 32
def ring(r, z): return [bm.verts.new((math.cos(2 * math.pi * i / segs) * r, math.sin(2 * math.pi * i / segs) * r, z)) for i in range(segs)]
outer = ring(CUP_RADIUS + RIM, 0.006); inner = ring(CUP_RADIUS, 0.006)
skirt = ring(CUP_RADIUS + RIM, 0.0)
wall_top = ring(CUP_RADIUS, 0.006); wall_bottom = ring(CUP_RADIUS, -0.10)
bottom = ring(CUP_RADIUS * 0.6, -0.10)
centre = bm.verts.new((0, 0, -0.10))
# The mouth: a dark disc just under the lip, above the turf, so the hole reads as a hole even
# where the putting surface isn't cut (the game's green mesh runs straight under it).
mouth = ring(CUP_RADIUS - 0.002, 0.0045); mouth_centre = bm.verts.new((0, 0, 0.0045))
for i in range(segs):
    j = (i + 1) % segs
    f = bm.faces.new((mouth[j], mouth[i], mouth_centre)); f.material_index = 1
    f = bm.faces.new((inner[i], outer[i], outer[j], inner[j])); f.material_index = 0    # lip
    f = bm.faces.new((skirt[i], skirt[j], outer[j], outer[i])); f.material_index = 0     # lip's outer edge
    f = bm.faces.new((wall_top[i], wall_top[j], wall_bottom[j], wall_bottom[i])); f.material_index = 1  # liner wall, dark
    f = bm.faces.new((wall_bottom[i], wall_bottom[j], bottom[j], bottom[i])); f.material_index = 1
    f = bm.faces.new((bottom[i], bottom[j], centre)); f.material_index = 1
bm.faces.ensure_lookup_table()
bm.normal_update()
# A cup is seen from above and from inside, and the game culls back faces: the lip, mouth and
# bottom face up, the liner wall faces the axis.
for f in bm.faces:
    f.smooth = True
    c = f.calc_center_median()
    wanted_up = abs(f.normal.z) > 0.5
    if (wanted_up and f.normal.z < 0) or (not wanted_up and f.normal.dot(Vector((-c.x, -c.y, 0))) < 0):
        f.normal_flip()
cup = new_object("CUP", bm, [MAT_CUP_RIM, MAT_CUP], root)

# ---- the flagstick: a thin pole with a yellow band at the height a caddie's hand goes, a
# ferrule in the cup and a knob on top.
bm = bmesh.new()
bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=POLE_RADIUS, radius2=POLE_RADIUS * 0.8, depth=POLE_HEIGHT)
bmesh.ops.translate(bm, verts=bm.verts, vec=(0, 0, POLE_HEIGHT / 2 - 0.08))
for f in bm.faces: f.material_index = 0; f.smooth = True
band = bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=POLE_RADIUS * 1.25, radius2=POLE_RADIUS * 1.25, depth=0.10)
bmesh.ops.translate(bm, verts=band["verts"], vec=(0, 0, 0.55))
for v in band["verts"]:
    for f in v.link_faces: f.material_index = 1; f.smooth = True
knob = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.024)
bmesh.ops.translate(bm, verts=knob["verts"], vec=(0, 0, POLE_HEIGHT - 0.08))
for v in knob["verts"]:
    for f in v.link_faces: f.material_index = 1; f.smooth = True
pole = new_object("FLAG_POLE", bm, [MAT_POLE, MAT_POLE_BAND], root)

# ---- the flag: a cloth grid off the pole, waved along its length and lifting at the fly.
bm = bmesh.new()
nx, nz = 14, 8
grid = [[None] * (nz + 1) for _ in range(nx + 1)]
for i in range(nx + 1):
    u = i / nx
    for k in range(nz + 1):
        w = k / nz
        wave = 0.045 * math.sin(u * math.pi * 2.2) * u        # pinned at the pole, freest at the fly
        lift = 0.03 * u * u
        grid[i][k] = bm.verts.new((POLE_RADIUS + u * FLAG_W, wave, POLE_HEIGHT - 0.10 - FLAG_H + w * FLAG_H + lift))
for i in range(nx):
    for k in range(nz):
        f = bm.faces.new((grid[i][k], grid[i + 1][k], grid[i + 1][k + 1], grid[i][k + 1])); f.smooth = True
# Cloth has two sides and the game culls back faces: a second skin, reversed, a hair behind.
back = bmesh.ops.duplicate(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:])
back_faces = [g for g in back["geom"] if isinstance(g, bmesh.types.BMFace)]
bmesh.ops.reverse_faces(bm, faces=back_faces)
bmesh.ops.translate(bm, verts=[g for g in back["geom"] if isinstance(g, bmesh.types.BMVert)], vec=(0, 0.002, 0))
flag = new_object("FLAG", bm, [MAT_FLAG], root)

# ---- a preview set-up: a patch of green and a low afternoon sun
if "--render" in ARGS:
    green = material("MAT_GREEN", (156, 228, 72), 0.95)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=2.0)
    patch = new_object("PREVIEW_GREEN", bm, [green], None)
    sun = bpy.data.lights.new("SUN", 'SUN'); sun.energy = 3.5; sun.angle = math.radians(3)
    sun_ob = bpy.data.objects.new("SUN", sun); sc.collection.objects.link(sun_ob)
    sun_ob.rotation_euler = (math.radians(55), 0, math.radians(-40))
    cam = bpy.data.cameras.new("CAM"); cam.lens = 50
    cam_ob = bpy.data.objects.new("CAM_PIN", cam); sc.collection.objects.link(cam_ob)
    cam_ob.location = (2.4, -3.4, 1.5)
    cam_ob.rotation_euler = (Vector((0.2, 0, 1.05)) - Vector(cam_ob.location)).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam_ob
    world = bpy.data.worlds.new("World"); sc.world = world
    if not world.use_nodes: world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.72, 0.95, 1)
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 48
    sc.render.resolution_x = 900; sc.render.resolution_y = 1100
    sc.render.film_transparent = False
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    sc.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)
    print(f"rendered {PREVIEW}")
    bpy.data.objects.remove(patch); bpy.data.objects.remove(sun_ob); bpy.data.objects.remove(cam_ob)

os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
exported = [root, cup, pole, flag]
for o in bpy.data.objects: o.select_set(o in exported)
bpy.context.view_layer.objects.active = root
with bpy.context.temp_override(selected_objects=exported, active_object=root, object=root):
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'EMPTY', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
print(f"saved {BLEND}\nexported {FBX} ({os.path.getsize(FBX) // 1024} KB)")
