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
MAT_CUP_MOUTH = material("MAT_CUP_MOUTH", (20, 20, 20), 0.9)
MAT_CUP_EDGE = material("MAT_CUP_EDGE", (92, 150, 58), 0.9)
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

# ---- the cup, as a real hole. The game's green isn't cut, so the hole is three pieces:
#   CUP_MOUTH  a disc just over the turf that marks where the hole is seen (the game draws it
#              into the stencil only — GolfArcade/HoleMask);
#   CUP        the inside, drawn through the green where the mouth marked it
#              (GolfArcade/HoleInside): the cut turf at the top, a band of soil, the white
#              plastic liner set an inch down, going grey and then dark toward the bottom, and
#              the dark ferrule hole in the middle of the bottom. Vertex colours carry all of it;
#   CUP_EDGE   a thin darker ring of cut grass on the turf round the mouth, so the hole reads
#              from across the green.
# Regulation size; the game scales all three with its ball (Hole.CupScale).
segs = 48
DEPTH = 0.102                 # a cup is 4" deep
bm = bmesh.new()
col = bm.loops.layers.color.new("Col")
def ring(r, z): return [bm.verts.new((math.cos(2 * math.pi * i / segs) * r, math.sin(2 * math.pi * i / segs) * r, z)) for i in range(segs)]
# (depth below the turf, colour) down the wall
BANDS = [(0.000, (0.36, 0.60, 0.24)), (0.010, (0.24, 0.42, 0.16)),      # cut turf
         (0.012, (0.40, 0.28, 0.17)), (0.024, (0.27, 0.18, 0.11)),      # soil
         (0.0255, (0.95, 0.95, 0.93)), (0.050, (0.80, 0.80, 0.78)),     # the liner, lit
         (0.078, (0.45, 0.45, 0.44)), (DEPTH, (0.16, 0.16, 0.16))]      # into the dark
rings = [(ring(CUP_RADIUS, -d), c) for d, c in BANDS]
def paint(face, colours):
    for loop, c in zip(face.loops, colours): loop[col] = (*c, 1.0)
for (upper, cu), (lower, cl) in zip(rings, rings[1:]):
    for i in range(segs):
        j = (i + 1) % segs
        f = bm.faces.new((upper[i], lower[i], lower[j], upper[j]))
        paint(f, (cu, cl, cl, cu))
# the bottom, with the ferrule's socket in the middle
bottom_edge = rings[-1][0]
socket = ring(0.012, -DEPTH); centre = bm.verts.new((0, 0, -DEPTH))
floor_col, socket_col = (0.13, 0.13, 0.13), (0.04, 0.04, 0.04)
for i in range(segs):
    j = (i + 1) % segs
    paint(bm.faces.new((bottom_edge[i], bottom_edge[j], socket[j], socket[i])), (floor_col, floor_col, socket_col, socket_col))
    paint(bm.faces.new((socket[i], socket[j], centre)), (socket_col, socket_col, socket_col))
bm.normal_update()
# seen from above, through the mouth: the wall must face the axis and the bottom face up (the
# game culls back faces)
for f in bm.faces:
    c = f.calc_center_median()
    if abs(f.normal.z) > 0.5:
        if f.normal.z < 0: f.normal_flip()
    elif f.normal.dot(Vector((-c.x, -c.y, 0))) < 0: f.normal_flip()
    f.smooth = abs(f.normal.z) <= 0.5
cup = new_object("CUP", bm, [MAT_CUP], root)

bm = bmesh.new()
mouth = [bm.verts.new((math.cos(2 * math.pi * i / segs) * CUP_RADIUS, math.sin(2 * math.pi * i / segs) * CUP_RADIUS, 0.003)) for i in range(segs)]
disc = bm.faces.new(mouth)
bm.normal_update()
if disc.normal.z < 0: disc.normal_flip()
cup_mouth = new_object("CUP_MOUTH", bm, [MAT_CUP_MOUTH], root)

bm = bmesh.new()
inner = [bm.verts.new((math.cos(2 * math.pi * i / segs) * CUP_RADIUS, math.sin(2 * math.pi * i / segs) * CUP_RADIUS, 0.002)) for i in range(segs)]
outer = [bm.verts.new((math.cos(2 * math.pi * i / segs) * (CUP_RADIUS + RIM * 0.5), math.sin(2 * math.pi * i / segs) * (CUP_RADIUS + RIM * 0.5), 0.002)) for i in range(segs)]
for i in range(segs):
    j = (i + 1) % segs
    bm.faces.new((inner[i], outer[i], outer[j], inner[j]))
bm.normal_update()
for f in bm.faces:
    if f.normal.z < 0: f.normal_flip()
cup_edge = new_object("CUP_EDGE", bm, [MAT_CUP_EDGE], root)

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
# The flag flutters: four shape keys a quarter wave apart, the ripple running from the pole out
# to the fly and the fly lifting and dropping with it. The game cross-fades them round a loop
# (WaterMotion, the same driver as the sea), so the wave travels instead of flapping in place.
def flutter(u, phase):
    return (0.055 * math.sin(u * math.pi * 2.2 - phase) * u,               # across: the ripple
            0.018 * u * u * math.sin(phase + u * 1.7))                     # up: the fly lifting
flag.shape_key_add(name="Basis", from_mix=False)
base = [v.co.copy() for v in flag.data.vertices]
for k in range(4):
    phase = k * math.pi / 2
    key = flag.shape_key_add(name=f"Flutter{k}", from_mix=False)
    for i, co in enumerate(base):
        u = max(0.0, (co.x - POLE_RADIUS) / FLAG_W)
        skin = co.y - 0.045 * math.sin(u * math.pi * 2.2) * u        # 0, or the back skin's hair of offset
        dy, dz = flutter(u, phase)
        key.data[i].co = (co.x, dy + skin, co.z + dz)

# ---- a preview set-up: a patch of green and a low afternoon sun
if "--render" in ARGS:
    green = material("MAT_GREEN", (156, 228, 72), 0.95)
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=2.0)
    patch = new_object("PREVIEW_GREEN", bm, [green], None)
    # the preview cuts the green where the game's stencil would, and shows the inside lit by its
    # vertex colours
    cutter_bm = bmesh.new(); bmesh.ops.create_cone(cutter_bm, cap_ends=True, segments=48, radius1=CUP_RADIUS, radius2=CUP_RADIUS, depth=0.2)
    cutter = new_object("PREVIEW_CUTTER", cutter_bm, [], None); cutter.hide_render = True
    cut = patch.modifiers.new("hole", 'BOOLEAN'); cut.object = cutter; cut.operation = 'DIFFERENCE'
    inside = MAT_CUP.node_tree
    attr = inside.nodes.new("ShaderNodeVertexColor"); attr.layer_name = "Col"
    inside.links.new(attr.outputs["Color"], inside.nodes["Principled BSDF"].inputs["Base Color"])
    cup_mouth.hide_render = True
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
    # and close on the hole, the way a putt's camera sees it
    cam_ob.location = (0.0, -0.42, 0.30)
    cam_ob.rotation_euler = (Vector((0, 0, -0.03)) - Vector(cam_ob.location)).to_track_quat('-Z', 'Y').to_euler()
    cam.lens = 40
    sc.render.resolution_x = 900; sc.render.resolution_y = 700
    sc.render.filepath = PREVIEW.replace(".png", "_cup.png")
    bpy.ops.render.render(write_still=True)
    print(f"rendered {sc.render.filepath}")
    inside.nodes.remove(attr)
    cup_mouth.hide_render = False
    bpy.data.objects.remove(patch); bpy.data.objects.remove(cutter); bpy.data.objects.remove(sun_ob); bpy.data.objects.remove(cam_ob)

os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND, compress=True)
exported = [root, cup, cup_mouth, cup_edge, pole, flag]
for o in bpy.data.objects: o.select_set(o in exported)
bpy.context.view_layer.objects.active = root
with bpy.context.temp_override(selected_objects=exported, active_object=root, object=root):
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'EMPTY', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             bake_anim=False, path_mode='STRIP', embed_textures=False)
print(f"saved {BLEND}\nexported {FBX} ({os.path.getsize(FBX) // 1024} KB)")
