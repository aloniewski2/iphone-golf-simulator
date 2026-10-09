import bpy, bmesh, math, sys, importlib
import os, bpy; sys.path.insert(0, os.path.join(bpy.path.abspath("//"), "scripts"))
import hole07_lib as H; importlib.reload(H)
import hole07_design as D; importlib.reload(D)
from hole07_design import *
from mathutils import Vector, Matrix

MAT_WOOD  = H.get_material("MAT_WOOD",  H.rgb(112, 74, 46))
MAT_ROOF  = H.get_material("MAT_ROOF",  H.rgb(64, 58, 60))
MAT_WALL  = H.get_material("MAT_WALL",  H.rgb(222, 206, 176))
MAT_GLASS = H.get_material("MAT_GLASS", H.rgb(150, 205, 235), roughness=0.2)
MAT_FLAG  = H.get_material("MAT_FLAG",  H.rgb(230, 40, 40))
MAT_POLE  = H.get_material("MAT_POLE",  H.rgb(240, 240, 240), roughness=0.5)
MAT_CUP   = H.get_material("MAT_CUP",   H.rgb(30, 30, 30))
MAT_BALL  = H.get_material("MAT_BALL",  H.rgb(250, 250, 250), roughness=0.4)
MAT_STONE = H.get_material("MAT_STONE", H.rgb(150, 146, 140))

root = H.get_root()

# ------------------------------------------------------------------ tiny box/prism builders
def box(bm, cx, cy, cz, sx, sy, sz, mat):
    r = bmesh.ops.create_cube(bm, size=1.0)
    verts = r["verts"]
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=verts)
    bmesh.ops.translate(bm, vec=(cx, cy, cz), verts=verts)
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat; f.smooth = False
    return verts

def gable_roof(bm, cx, cy, z0, sx, sy, rise, overhang, mat):
    """triangular prism roof along the x axis."""
    hx, hy = sx / 2 + overhang, sy / 2 + overhang
    a = bm.verts.new((cx - hx, cy - hy, z0)); b = bm.verts.new((cx + hx, cy - hy, z0))
    c = bm.verts.new((cx + hx, cy + hy, z0)); d = bm.verts.new((cx - hx, cy + hy, z0))
    r1 = bm.verts.new((cx - hx, cy, z0 + rise)); r2 = bm.verts.new((cx + hx, cy, z0 + rise))
    faces = [bm.faces.new((a, b, r2, r1)), bm.faces.new((c, d, r1, r2)),
             bm.faces.new((a, r1, d)), bm.faces.new((b, c, r2)), bm.faces.new((a, d, c, b))]
    for f in faces:
        f.material_index = mat; f.smooth = False

def build_object(name, col, builder, mats, loc=(0, 0, 0), rot_z=0.0):
    ob = H.new_mesh_object(name, col)
    bm = bmesh.new()
    builder(bm)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for m in mats:
        ob.data.materials.append(m)
    bm.to_mesh(ob.data); bm.free()
    ob.location = loc
    ob.rotation_euler = (0, 0, rot_z)
    return ob

# ------------------------------------------------------------------ Phase 9: clubhouse (landmark, not architecture)
CH = CLUBHOUSE
gz = height(CH["cx"], CH["cy"])
def clubhouse(bm):
    W, Dp = CH["w"], CH["d"]
    # stone plinth / terrace
    box(bm, 0, -2, 0.6, W + 6, Dp + 6, 1.2, 4)
    # main hall
    box(bm, -3, 0, 1.2 + 3.2, W * 0.62, Dp, 6.4, 0)
    gable_roof(bm, -3, 0, 1.2 + 6.4, W * 0.62, Dp, 3.4, 1.6, 1)
    # side wing (lower)
    box(bm, W * 0.30, -1.5, 1.2 + 2.3, W * 0.36, Dp * 0.8, 4.6, 2)
    gable_roof(bm, W * 0.30, -1.5, 1.2 + 4.6, W * 0.36, Dp * 0.8, 2.4, 1.4, 1)
    # veranda roof on posts along the front (south)
    box(bm, -3, -Dp / 2 - 3.0, 1.2 + 5.2, W * 0.62 + 2, 6.0, 0.5, 1)
    for i in range(5):
        x = -3 - (W * 0.62) / 2 + 1.0 + i * (W * 0.62 - 2) / 4
        box(bm, x, -Dp / 2 - 5.4, 1.2 + 2.6, 0.5, 0.5, 5.2, 0)
    # window bands (flat glass quads just proud of the walls)
    box(bm, -3, -Dp / 2 - 0.05, 1.2 + 3.6, W * 0.62 - 3, 0.3, 2.4, 3)
    box(bm, W * 0.30, -Dp * 0.8 / 2 - 1.5 - 0.05, 1.2 + 2.6, W * 0.36 - 2, 0.3, 1.8, 3)
    # chimney
    box(bm, -9, 2.5, 1.2 + 6.4 + 2.8, 1.6, 1.6, 3.2, 4)

clubhouse_ob = build_object("CLUBHOUSE", "STRUCTURES", clubhouse,
                            [MAT_WOOD, MAT_ROOF, MAT_WALL, MAT_GLASS, MAT_STONE],
                            loc=(CH["cx"], CH["cy"], gz - 0.2), rot_z=CH["rot"])

# ------------------------------------------------------------------ Phase 10: gameplay landmarks
PIN = Vector((GREEN["cx"] + 6, GREEN["cy"] + 4))
pin_z = height(PIN.x, PIN.y) + 0.26

def cylinder(bm, r, h, segs, mat, z0=0.0):
    ring0 = [bm.verts.new((math.cos(a) * r, math.sin(a) * r, z0)) for a in [math.tau * i / segs for i in range(segs)]]
    ring1 = [bm.verts.new((math.cos(a) * r, math.sin(a) * r, z0 + h)) for a in [math.tau * i / segs for i in range(segs)]]
    faces = [bm.faces.new((ring0[i], ring0[(i + 1) % segs], ring1[(i + 1) % segs], ring1[i])) for i in range(segs)]
    faces.append(bm.faces.new(list(reversed(ring0)))); faces.append(bm.faces.new(ring1))
    for f in faces:
        f.material_index = mat; f.smooth = False

def cup(bm):
    cylinder(bm, 0.7, 0.05, 12, 0, z0=0.0)      # readable dark disc (stylised cup, oversized for the overview)
build_object("HOLE_CUP", "GAMEPLAY", cup, [MAT_CUP], loc=(PIN.x, PIN.y, pin_z + 0.01))

def flag(bm):
    cylinder(bm, 0.16, 10.0, 6, 0)                 # pole (exaggerated height for readability at overview scale)
    a = bm.verts.new((0.16, 0, 10.0)); b = bm.verts.new((0.16, 0, 7.6)); c = bm.verts.new((4.6, 0, 8.8))
    f = bm.faces.new((a, b, c)); f.material_index = 1; f.smooth = False
    d = bm.verts.new((0.16, 0.01, 10.0)); e = bm.verts.new((0.16, 0.01, 7.6)); g = bm.verts.new((4.6, 0.01, 8.8))
    f2 = bm.faces.new((d, g, e)); f2.material_index = 1; f2.smooth = False
build_object("FLAG_POLE", "GAMEPLAY", flag, [MAT_POLE, MAT_FLAG], loc=(PIN.x, PIN.y, pin_z), rot_z=math.radians(-30))
# separate named FLAG object (linked to the same landmark) for the engine to animate
H.remove_object("FLAG")
flag_only = H.new_mesh_object("FLAG", "GAMEPLAY")
bm = bmesh.new()
a = bm.verts.new((0.16, 0, 10.0)); b = bm.verts.new((0.16, 0, 7.6)); c = bm.verts.new((4.6, 0, 8.8))
bm.faces.new((a, b, c))
bm.to_mesh(flag_only.data); bm.free()
flag_only.data.materials.append(MAT_FLAG)
flag_only.location = (PIN.x, PIN.y, pin_z); flag_only.rotation_euler = (0, 0, math.radians(-30))
# remove the duplicated flag faces from FLAG_POLE so only the pole remains there
fp = bpy.data.objects["FLAG_POLE"]
bm = bmesh.new(); bm.from_mesh(fp.data)
bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index == 1], context='FACES')
bm.to_mesh(fp.data); bm.free()

# tee markers: two white blocks on the tee box, ball start between them
tz = height(TEE["cx"], TEE["cy"]) + 0.24
for i, dx in enumerate((-4.5, 4.5)):
    def marker(bm, dx=dx):
        box(bm, 0, 0, 0.45, 0.9, 0.9, 0.9, 0)
    build_object(f"TEE_MARKER_{i + 1}", "GAMEPLAY", marker, [MAT_POLE],
                 loc=(TEE["cx"] + dx, TEE["cy"] - 6, tz), rot_z=math.radians(15))

H.remove_object("BALL_START")
ball = H.new_mesh_object("BALL_START", "GAMEPLAY")
bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.35)
for f in bm.faces: f.smooth = True
bm.to_mesh(ball.data); bm.free()
ball.data.materials.append(MAT_BALL)
ball.location = (TEE["cx"], TEE["cy"] - 6, tz + 0.35)

# empties the game can read directly
# MARKER_UP sits 50 m straight above the tee marker so an importer can recover the up axis
for name, loc in (("MARKER_TEE", (TEE["cx"], TEE["cy"] - 6, tz)), ("MARKER_PIN", (PIN.x, PIN.y, pin_z)), ("MARKER_UP", (TEE["cx"], TEE["cy"] - 6, tz + 50))):
    H.remove_object(name)
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = 'SPHERE'; e.empty_display_size = 2
    bpy.context.scene.collection.objects.link(e)
    H.link_to(e, "GAMEPLAY"); e.parent = root; e.location = loc

H.save()
result = {"clubhouse_faces": len(clubhouse_ob.data.polygons), "pin": list(PIN), "pin_z": pin_z}
