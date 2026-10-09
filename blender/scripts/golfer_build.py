"""GOLFER: low-poly rigged golfer + driver, with a keyframed full swing built from swing mechanics.

Frame: the golfer faces -Y (the ball is in front of them), the target is +X (their left: a
right-hander), up is +Z, metres. The model origin is the point beside the ball where the game
stands the golfer; the ball sits at BALL.

Swing model (per key pose): pelvis turn, thorax turn (on top of pelvis), forward spine bend,
side bend, lateral weight shift, knee flex, lead-arm angle in the swing plane (alpha: 0 hanging,
+ toward the trail side / backswing, - toward the target / follow-through), and the arm-shaft
angle beta (180 = arm and shaft in line; 90 = fully hinged). The lead arm stays straight; the
trail arm folds through IK to reach lower on the grip.
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Quaternion, Euler

# Two builds share the rig and the swing; only proportions, hair and stance width differ.
VARIANT = globals().get("VARIANT", "male")
VARIANTS = {
    "male":   dict(tag="m", shoulder_x=0.19, hip_x=0.12, knee_x=0.19, ankle_x=0.22, chest=(0.15, 0.20), waist=0.155, hips=0.165,
                   limb=1.0, head=1.0, hair="short", bust=False, neck=0.055),
    "female": dict(tag="f", shoulder_x=0.165, hip_x=0.125, knee_x=0.17, ankle_x=0.19, chest=(0.125, 0.165), waist=0.12, hips=0.17,
                   limb=0.85, head=0.96, hair="ponytail", bust=True, neck=0.048),
}
V = VARIANTS[VARIANT]
BLEND = f"/Users/andrewloniewski/iPhoneGolfSimulator/blender/golfer_{V['tag']}.blend"
FBX = f"/Users/andrewloniewski/iPhoneGolfSimulator/Unity/Assets/Resources/Golfer/golfer_{V['tag']}.fbx"
PREVIEW = f"/Users/andrewloniewski/iPhoneGolfSimulator/blender/previews/golfer_{V['tag']}"

BALL = Vector((0.0, -0.686, 0.021))       # where the game puts the ball relative to the golfer origin
STANCE = Vector((-0.22, 0.02, 0.0))       # stance centre: trail side of the ball, a touch behind the origin
FPS = 60

# ------------------------------------------------------------------ fresh file
bpy.ops.wm.read_homefile(use_empty=True)
sc = bpy.context.scene
sc.render.fps = FPS
sc.render.engine = 'BLENDER_EEVEE'
sc.view_settings.view_transform = 'Standard'

def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), 1.0)

def material(name, col, rough=0.85):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = col
    b.inputs["Roughness"].default_value = rough
    m.diffuse_color = col
    return m

MATS = {
    "MAT_SKIN": material("MAT_SKIN", srgb(226, 160, 110)),
    "MAT_SHIRT": material("MAT_SHIRT", srgb(38, 84, 176)),
    "MAT_TROUSERS": material("MAT_TROUSERS", srgb(214, 196, 160)),
    "MAT_BELT": material("MAT_BELT", srgb(34, 40, 62)),
    "MAT_EYE": material("MAT_EYE", srgb(28, 24, 22)),
    "MAT_LOGO": material("MAT_LOGO", srgb(38, 84, 176)),
    "MAT_CAP": material("MAT_CAP", srgb(245, 245, 245)),
    "MAT_GLOVE": material("MAT_GLOVE", srgb(240, 240, 240)),
    "MAT_SHOE": material("MAT_SHOE", srgb(240, 240, 240)),
    "MAT_SHOE_SOLE": material("MAT_SHOE_SOLE", srgb(40, 40, 44)),
    "MAT_SHAFT": material("MAT_SHAFT", srgb(190, 192, 198), 0.35),
    "MAT_CLUBHEAD": material("MAT_CLUBHEAD", srgb(40, 42, 48), 0.4),
    "MAT_GRIP": material("MAT_GRIP", srgb(30, 30, 34)),
    "MAT_HAIR": material("MAT_HAIR", srgb(70, 48, 30)),
    "MAT_LIPS": material("MAT_LIPS", srgb(196, 84, 90)),
    "MAT_SHIRT_DARK": material("MAT_SHIRT_DARK", srgb(28, 62, 136)),
}
mat_index = {name: i for i, name in enumerate(MATS)}

# ------------------------------------------------------------------ armature
arm_data = bpy.data.armatures.new("GOLFER_RIG")
arm = bpy.data.objects.new("GOLFER_RIG", arm_data)
sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
arm.select_set(True)
def mode(m):
    with bpy.context.temp_override(active_object=arm, object=arm, selected_objects=[arm], selected_editable_objects=[arm]):
        bpy.ops.object.mode_set(mode=m)
mode('EDIT')

S = STANCE
HIP_Z, KNEE_Z, ANKLE_Z = 0.95, 0.50, 0.08
SHOULDER_Z, SHOULDER_X = 1.43, V['shoulder_x']
UPPER_ARM, FOREARM, HAND = 0.29, 0.27, 0.08

def bone(name, head, tail, parent=None, connect=False):
    b = arm_data.edit_bones.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.roll = 0.0
    if parent:
        b.parent = arm_data.edit_bones[parent]
        b.use_connect = connect
    return b

bone("root", (0, 0, 0), (0, 0.15, 0))
bone("hips", S + Vector((0, 0, HIP_Z)), S + Vector((0, 0, 1.06)), "root")
bone("spine", S + Vector((0, 0, 1.06)), S + Vector((0, 0, 1.25)), "hips", True)
bone("chest", S + Vector((0, 0, 1.25)), S + Vector((0, 0, SHOULDER_Z + 0.02)), "spine", True)
bone("neck", S + Vector((0, 0, SHOULDER_Z + 0.02)), S + Vector((0, 0, 1.54)), "chest", True)
bone("head", S + Vector((0, 0, 1.54)), S + Vector((0, 0, 1.78)), "neck", True)
for side, sx in (("L", 1), ("R", -1)):
    sh = S + Vector((sx * SHOULDER_X, 0, SHOULDER_Z))
    bone(f"shoulder.{side}", S + Vector((sx * 0.05, 0, SHOULDER_Z), ), sh, "chest")
    el = sh + Vector((sx * 0.04, -0.03, -UPPER_ARM))
    wr = el + Vector((sx * 0.02, -0.06, -FOREARM))
    bone(f"upper_arm.{side}", sh, el, f"shoulder.{side}", True)
    bone(f"forearm.{side}", el, wr, f"upper_arm.{side}", True)
    bone(f"hand.{side}", wr, wr + Vector((0, -0.02, -HAND)), f"forearm.{side}", True)
    # elbow pole: behind and outside the shoulder, so elbows fold back/down, never forward
    bone(f"elbow_pole.{side}", sh + Vector((sx * 0.45, 0.55, -0.35)), sh + Vector((sx * 0.45, 0.55, -0.25)), "chest")
    hp = S + Vector((sx * V["hip_x"], 0, HIP_Z))
    kn = S + Vector((sx * V["knee_x"], 0, KNEE_Z)); an = S + Vector((sx * V["ankle_x"], 0, ANKLE_Z))
    bone(f"thigh.{side}", hp, kn, "hips")
    bone(f"shin.{side}", kn, an, f"thigh.{side}", True)
    bone(f"foot.{side}", an, an + Vector((0, -0.22, 0.02 - ANKLE_Z)), f"shin.{side}", True)
# grip: Y axis runs down the shaft toward the club head; the lead hand sits at its head
g0 = S + Vector((0.12, -0.35, 0.80))
bone("grip", g0, g0 + Vector((0, -0.05, -0.085)), "root")
bone("grip_L", g0, g0 + Vector((0.03, 0, 0)), "grip")
gR = g0 + Vector((0, -0.05, -0.085)) * (0.10 / 0.0986)
bone("grip_R", gR, gR + Vector((-0.03, 0, 0)), "grip")
mode('POSE')

# IK: each forearm reaches for its spot on the grip
for side in ("L", "R"):
    pb = arm.pose.bones[f"forearm.{side}"]
    ik = pb.constraints.new('IK')
    ik.target = arm; ik.subtarget = f"grip_{side}"
    ik.pole_target = arm; ik.pole_subtarget = f"elbow_pole.{side}"
    ik.chain_count = 2
    ik.use_stretch = True     # a little give so both hands always sit on the grip
    ik.pole_angle = math.radians(-90)
    arm.pose.bones[f"upper_arm.{side}"].ik_stretch = 0.08
    arm.pose.bones[f"forearm.{side}"].ik_stretch = 0.08
for pb in arm.pose.bones:
    pb.rotation_mode = 'XYZ'
arm.pose.bones["grip"].rotation_mode = 'QUATERNION'
mode('OBJECT')

# ------------------------------------------------------------------ body mesh (rigid parts, one skinned mesh)
bm = bmesh.new()
groups = {}   # vertex -> bone name
deform = bm.verts.layers.deform.verify()

def tag(verts, bone_name, mat, smooth=True):
    for v in verts:
        groups[v] = bone_name
    for f in {f for v in verts for f in v.link_faces}:
        f.material_index = mat_index[mat]
        f.smooth = smooth

def limb(p0, p1, r0, r1, bone_name, mat, segs=10, cap=True):
    p0, p1 = Vector(p0), Vector(p1)
    axis = (p1 - p0)
    L = axis.length
    q = axis.normalized().to_track_quat('Z', 'Y')
    verts = []
    for z, r in ((0.0, r0), (L, r1)):
        ring = []
        for i in range(segs):
            a = math.tau * i / segs
            ring.append(bm.verts.new(p0 + q @ Vector((math.cos(a) * r, math.sin(a) * r, z))))
        verts.append(ring)
    faces = [bm.faces.new((verts[0][i], verts[0][(i + 1) % segs], verts[1][(i + 1) % segs], verts[1][i])) for i in range(segs)]
    if cap:
        for capf in (bm.faces.new(list(reversed(verts[0]))), bm.faces.new(verts[1])):
            capf.smooth = False
    tag(verts[0] + verts[1], bone_name, mat)
    for f in {f for v in verts[0] + verts[1] for f in v.link_faces}:
        if len(f.verts) > 4: f.smooth = False   # end caps stay flat
    return verts[0] + verts[1]

def icosphere_verts_faces(subdiv):
    """An icosphere built in its own bmesh (building one inside the shared bmesh at subdiv 2
    invalidates unrelated verts), returned as coordinates + index faces."""
    tmp = bmesh.new()
    bmesh.ops.create_icosphere(tmp, subdivisions=subdiv, radius=1.0)
    tmp.verts.ensure_lookup_table()
    coords = [v.co.copy() for v in tmp.verts]
    faces = [[v.index for v in f.verts] for f in tmp.faces]
    tmp.free()
    return coords, faces

def blob(center, rx, ry, rz, bone_name, mat, subdiv=1, squash_bottom=None):
    coords, faces = icosphere_verts_faces(subdiv)
    verts = []
    for c in coords:
        co = Vector((center[0] + c.x * rx, center[1] + c.y * ry, center[2] + c.z * rz))
        if squash_bottom is not None and co.z < center[2] - squash_bottom:
            co.z = center[2] - squash_bottom
        verts.append(bm.verts.new(co))
    for f in faces:
        try:
            bm.faces.new([verts[i] for i in f])
        except ValueError:
            pass   # a squashed, degenerate triangle
    tag(verts, bone_name, mat)
    return verts

def box(center, sx, sy, sz, bone_name, mat, rot=None):
    res = bmesh.ops.create_cube(bm, size=1.0)
    for v in res["verts"]:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
        if rot: v.co = rot @ v.co
        v.co += Vector(center)
    tag(res["verts"], bone_name, mat, smooth=False)
    return res["verts"]

# torso: tapered blocks, shirt over the chest, trousers on the hips
LB = V["limb"]
limb(S + Vector((0, 0, 1.24)), S + Vector((0, 0, SHOULDER_Z + 0.04)), V["chest"][0], V["chest"][1], "chest", "MAT_SHIRT", 8)
limb(S + Vector((0, 0, 1.04)), S + Vector((0, 0, 1.25)), V["waist"], V["chest"][0], "spine", "MAT_SHIRT", 8)
limb(S + Vector((0, 0, 0.88)), S + Vector((0, 0, 1.04)), V["hips"], V["waist"] + 0.01, "hips", "MAT_TROUSERS", 8)
blob(S + Vector((0, 0, SHOULDER_Z + 0.02)), SHOULDER_X + 0.02, 0.135 * LB + 0.01, 0.06, "chest", "MAT_SHIRT")   # shoulder yoke
if V["bust"]:
    blob(S + Vector((0, -0.09, 1.33)), 0.11, 0.07, 0.06, "chest", "MAT_SHIRT")
# neck + head (stylised: about a sixth of the height) + hair + cap with peak and logo + face
limb(S + Vector((0, 0, SHOULDER_Z + 0.02)), S + Vector((0, 0, 1.56)), V["neck"], V["neck"], "neck", "MAT_SKIN", 6)
HEAD_C = S + Vector((0, 0, 1.675))
HS = V["head"]
blob(HEAD_C, 0.125 * HS, 0.13 * HS, 0.125 * HS, "head", "MAT_SKIN", subdiv=2)
blob(S + Vector((0, 0, SHOULDER_Z + 0.03)), 0.07, 0.07, 0.05, "neck", "MAT_SKIN")   # neck base
if V["hair"] == "short":
    blob(HEAD_C + Vector((0, 0.03, 0.005)), 0.132, 0.125, 0.12, "head", "MAT_HAIR", squash_bottom=0.07)   # hair round the back and sides
else:
    blob(HEAD_C + Vector((0, 0.03, 0.0)), 0.13, 0.13, 0.125, "head", "MAT_HAIR", squash_bottom=0.09)      # hair to the nape
    blob(HEAD_C + Vector((0, 0.15, 0.02)), 0.05, 0.06, 0.05, "head", "MAT_HAIR")                          # ponytail through the cap
    limb(HEAD_C + Vector((0, 0.16, 0.0)), HEAD_C + Vector((0, 0.20, -0.22)), 0.04, 0.02, "head", "MAT_HAIR", 6)
blob(HEAD_C + Vector((0, 0.01, 0.07)), 0.142, 0.152, 0.09, "head", "MAT_CAP", subdiv=2, squash_bottom=0.0)   # crown
blob(HEAD_C + Vector((0, 0.01, 0.07)), 0.146, 0.156, 0.03, "head", "MAT_CAP", squash_bottom=0.0)          # cap band
box(HEAD_C + Vector((0, -0.165, 0.072)), 0.22, 0.17, 0.022, "head", "MAT_CAP")                             # peak, rooted in the band
box(HEAD_C + Vector((0, -0.148, 0.115)), 0.07, 0.012, 0.045, "head", "MAT_LOGO")                            # "M" logo plate
for sx in (-1, 1):                                                                                          # eyes + brows
    blob(HEAD_C + Vector((sx * 0.046, -0.118, 0.0)), 0.014, 0.012, 0.02, "head", "MAT_EYE")
    box(HEAD_C + Vector((sx * 0.048, -0.121, 0.038)), 0.044, 0.012, 0.011, "head", "MAT_HAIR")
box(HEAD_C + Vector((0, -0.127, -0.058)), 0.046, 0.012, 0.009, "head", "MAT_EYE")                            # smile
if V["hair"] == "ponytail":
    box(HEAD_C + Vector((0, -0.132, -0.06)), 0.056, 0.012, 0.014, "head", "MAT_LIPS")
# polo collar (two wings meeting in a V), placket, belt and buckle
for sx in (-1, 1):
    wing = Matrix.Rotation(math.radians(sx * 28), 4, 'Z') @ Matrix.Rotation(math.radians(-25), 4, 'X')
    box(S + Vector((sx * 0.055, -0.13 * LB - 0.02, SHOULDER_Z + 0.035)), 0.075, 0.012, 0.05, "chest", "MAT_SHIRT", rot=wing.to_3x3())
box(S + Vector((0, -V["chest"][1] - 0.004, 1.37)), 0.014, 0.008, 0.10, "chest", "MAT_SHIRT_DARK")   # placket line
limb(S + Vector((0, 0, 1.03)), S + Vector((0, 0, 1.065)), V["waist"] + 0.012, V["waist"] + 0.012, "hips", "MAT_BELT", 10)
box(S + Vector((0, -V["waist"] - 0.012, 1.047)), 0.045, 0.012, 0.03, "hips", "MAT_SHOE_SOLE")   # buckle
for side, sx in (("L", 1), ("R", -1)):
    sh = S + Vector((sx * SHOULDER_X, 0, SHOULDER_Z))
    el = sh + Vector((sx * 0.04, -0.03, -UPPER_ARM))
    wr = el + Vector((sx * 0.02, -0.06, -FOREARM))
    blob(sh, 0.08 * LB, 0.08 * LB, 0.075 * LB, f"upper_arm.{side}", "MAT_SHIRT")
    limb(sh, sh.lerp(el, 0.45), 0.075 * LB, 0.062 * LB, f"upper_arm.{side}", "MAT_SHIRT", 6)
    limb(sh.lerp(el, 0.45), el, 0.052 * LB, 0.048 * LB, f"upper_arm.{side}", "MAT_SKIN", 6)
    blob(el, 0.05 * LB, 0.05 * LB, 0.05 * LB, f"forearm.{side}", "MAT_SKIN")
    limb(el, wr, 0.05 * LB, 0.04 * LB, f"forearm.{side}", "MAT_SKIN", 6)
    hp = S + Vector((sx * V["hip_x"], 0, HIP_Z))
    kn = S + Vector((sx * V["knee_x"], 0, KNEE_Z)); an = S + Vector((sx * V["ankle_x"], 0, ANKLE_Z))
    limb(hp + Vector((0, 0, 0.02)), kn, 0.085 * LB, 0.065 * LB, f"thigh.{side}", "MAT_TROUSERS", 7)
    blob(kn, 0.065 * LB, 0.065 * LB, 0.065 * LB, f"shin.{side}", "MAT_TROUSERS")
    limb(kn, an, 0.06 * LB, 0.045 * LB, f"shin.{side}", "MAT_TROUSERS", 7)
    blob(an, 0.05 * LB, 0.05 * LB, 0.045, f"shin.{side}", "MAT_TROUSERS")                       # ankle / cuff
    box(an + Vector((0, -0.06, -0.04)), 0.09 * LB, 0.24 * (0.5 + 0.5 * LB), 0.06, f"foot.{side}", "MAT_SHOE")
    blob(an + Vector((0, -0.17 * (0.5 + 0.5 * LB), -0.045)), 0.048 * LB, 0.06, 0.03, f"foot.{side}", "MAT_SHOE")   # rounded toe
    box(an + Vector((0, -0.06, -0.07)), 0.095 * LB, 0.25 * (0.5 + 0.5 * LB), 0.02, f"foot.{side}", "MAT_SHOE_SOLE")
# hands on the grip (grip space: Y down the shaft). Lead hand (L, gloved) at the top, trail below.
def grip_space(v):  # grip-local (x right, y down shaft, z) -> rest world, using the grip bone's rest matrix
    return arm.matrix_world @ arm.data.bones["grip"].matrix_local @ Vector(v)
def hand(center_local, bone_name, mat):
    coords, faces = icosphere_verts_faces(1)
    verts = [bm.verts.new(grip_space(Vector(center_local) + Vector((c.x * 0.045, c.y * 0.06, c.z * 0.04)))) for c in coords]
    for f in faces: bm.faces.new([verts[i] for i in f])
    tag(verts, bone_name, mat)
hand((0.0, 0.03, 0.0), "grip", "MAT_GLOVE")
hand((0.0, 0.12, 0.0), "grip", "MAT_SKIN")
# driver: grip, shaft, head
def shaft(y0, y1, r0, r1, mat, segs=6):
    verts = []
    for y, r in ((y0, r0), (y1, r1)):
        ring = [bm.verts.new(grip_space(Vector((math.cos(a) * r, y, math.sin(a) * r)))) for a in [math.tau * i / segs for i in range(segs)]]
        verts.append(ring)
    faces = [bm.faces.new((verts[0][i], verts[0][(i + 1) % segs], verts[1][(i + 1) % segs], verts[1][i])) for i in range(segs)]
    faces.append(bm.faces.new(list(reversed(verts[0])))); faces.append(bm.faces.new(verts[1]))
    tag(verts[0] + verts[1], "grip", mat)
shaft(-0.06, 0.22, 0.014, 0.012, "MAT_GRIP")
shaft(0.22, 1.10, 0.008, 0.006, "MAT_SHAFT")
# head: a rounded block, face toward the target (+X at address = grip-local... the toe points away
# from the golfer, i.e. grip-local -Z (toward -Y world at address); face is the target-side face.
coords, faces = icosphere_verts_faces(1)
verts = [bm.verts.new(grip_space(Vector((0.02 + c.x * 0.03, 1.115 + c.y * 0.032, -0.05 + c.z * 0.062)))) for c in coords]
for f in faces: bm.faces.new([verts[i] for i in f])
tag(verts, "grip", "MAT_CLUBHEAD")

bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
body_me = bpy.data.meshes.new("GOLFER_BODY")
body = bpy.data.objects.new("GOLFER_BODY", body_me)
sc.collection.objects.link(body)
for m in MATS.values():
    body_me.materials.append(m)
# vertex groups: one per bone, weight 1 (rigid parts)
names = sorted(set(groups.values()))
vg_index = {}
for n in names:
    vg = body.vertex_groups.new(name=n)
    vg_index[n] = vg.index
for v, n in groups.items():
    v[deform][vg_index[n]] = 1.0
bm.to_mesh(body_me); bm.free()
body_me.update()
body.parent = arm
mod = body.modifiers.new("Armature", 'ARMATURE')
mod.object = arm

# ------------------------------------------------------------------ swing keys
# (frame, pelvis, thorax(rel), bend, side, shift, kneeL, kneeR, phi, hinge)
# turns in degrees about vertical (negative = away from the target = backswing).
# phi: where the hands are on their arc round the lead shoulder, in the swing plane: 0 points
#   from the shoulder at the ball, + is the backswing side, - the follow-through side.
# hinge: in-plane angle from that radial to the shaft. ~-15 at address (club ahead of the hands
#   to the ball), +90 halfway back (shaft vertical when the lead arm is level), ~100 at the top,
#   more in transition (lag), unwinding to ~0 at impact, then negative as the club releases.
# None = solved so the club head sits on the ball.
KEYS = [
    dict(f=0,   name="address",    pelvis=0,    thorax=0,    bend=30, side=6,  shift=0.00,  kneeL=14, kneeR=14, phi=None, hinge=None),
    dict(f=14,  name="takeaway",   pelvis=-8,   thorax=-28,  bend=30, side=6,  shift=-0.01, kneeL=15, kneeR=14, phi=50,   hinge=30),
    dict(f=30,  name="halfway",    pelvis=-24,  thorax=-42,  bend=32, side=6,  shift=-0.02, kneeL=22, kneeR=15, phi=95,   hinge=85),
    dict(f=48,  name="top",        pelvis=-45,  thorax=-58,  bend=33, side=4,  shift=-0.03, kneeL=26, kneeR=14, phi=160,  hinge=100),
    dict(f=54,  name="transition", pelvis=-30,  thorax=-60,  bend=33, side=8,  shift=0.02,  kneeL=24, kneeR=18, phi=145,  hinge=108),
    dict(f=60,  name="delivery",   pelvis=5,    thorax=-45,  bend=32, side=12, shift=0.06,  kneeL=18, kneeR=26, phi=70,   hinge=88),
    dict(f=64,  name="impact",     pelvis=38,   thorax=-16,  bend=32, side=17, shift=0.06,  kneeL=12, kneeR=30, phi=None, hinge=None),
    dict(f=70,  name="release",    pelvis=60,   thorax=0,    bend=27, side=10, shift=0.09,  kneeL=8,  kneeR=34, phi=-40,  hinge=-25, reach=0.95),
    dict(f=80,  name="follow",     pelvis=82,   thorax=22,   bend=20, side=8,  shift=0.10,  kneeL=6,  kneeR=38, phi=-95,  hinge=-55, reach=0.86),
    dict(f=105, name="finish",     pelvis=105,  thorax=30,   bend=4,  side=6,  shift=0.11,  kneeL=4,  kneeR=44, phi=-165, hinge=-100, reach=0.66),
    dict(f=130, name="hold",       pelvis=105,  thorax=30,   bend=4,  side=6,  shift=0.11,  kneeL=4,  kneeR=44, phi=-165, hinge=-100, reach=0.66),
]
TOP_FRAME, IMPACT_FRAME, END_FRAME = 48, 64, 130
sc.frame_start, sc.frame_end = 0, END_FRAME

ARM_LEN = UPPER_ARM + FOREARM - 0.01   # hub to the lead hand (lead arm stays straight)
SHAFT_TO_HEAD = 1.115                  # lead hand to the club head centre, along the shaft
PLANE_N = None                         # swing plane normal, fixed in space once address is posed
RADIAL0 = None                         # in-plane unit vector from the lead shoulder to the ball at address

def rot_about(v, axis, deg):
    return Quaternion(axis, math.radians(deg)) @ v

def set_torso(k):
    pb = arm.pose.bones
    pb["hips"].location = Vector((k["shift"], 0, 0))
    # forward bend split between hips and spine so the back curves; side bend at the spine
    pb["hips"].rotation_euler = Euler((math.radians(-k["bend"] * 0.55), math.radians(k["pelvis"]), 0), 'XYZ')
    pb["spine"].rotation_euler = Euler((math.radians(-k["bend"] * 0.45), math.radians(k["thorax"] * 0.4), math.radians(k["side"])), 'XYZ')
    pb["chest"].rotation_euler = Euler((0, math.radians(k["thorax"] * 0.6), 0), 'XYZ')
    total = k["pelvis"] + k["thorax"]
    # head stays on the ball until the follow-through lets it come up and round
    look = -total * 0.9 if total < 40 else -total * 0.9 + (total - 40) * 1.4
    pb["neck"].rotation_euler = Euler((math.radians(k["bend"] * 0.5), math.radians(look * 0.4), 0), 'XYZ')
    pb["head"].rotation_euler = Euler((math.radians(k["bend"] * 0.5), math.radians(look * 0.6), 0), 'XYZ')
    for side, key in (("L", "kneeL"), ("R", "kneeR")):
        flex = k[key]
        pb[f"thigh.{side}"].rotation_euler = Euler((math.radians(flex * 0.5), 0, 0), 'XYZ')
        pb[f"shin.{side}"].rotation_euler = Euler((math.radians(-flex), 0, 0), 'XYZ')
        pb[f"foot.{side}"].rotation_euler = Euler((math.radians(flex * 0.5), 0, 0), 'XYZ')
    # the trail foot rolls onto its toe as the hips clear
    if k["pelvis"] > 45:
        roll = (k["pelvis"] - 45) * 0.9
        pb["foot.R"].rotation_euler = Euler((math.radians(k["kneeR"] * 0.5 - roll), 0, 0), 'XYZ')
        pb["thigh.R"].rotation_euler = Euler((math.radians(k["kneeR"] * 0.5), math.radians(min(roll, 40)), 0), 'XYZ')
    bpy.context.view_layer.update()

def lead_shoulder():
    """The hands hang from a hub a third of the way from the lead shoulder to the trail one, so
    both arms reach the grip (the lead one straight, the trail one a little bent)."""
    L = arm.pose.bones["upper_arm.L"].head; R = arm.pose.bones["upper_arm.R"].head
    return L.lerp(R, 0.33)

REACH = [1.0]   # lead-arm extension for the current key (it folds after impact)
def hands_at(phi):
    return lead_shoulder() + rot_about(RADIAL0, PLANE_N, phi) * ARM_LEN * REACH[0]

def set_grip(G, c):
    pb = arm.pose.bones
    y = c.normalized(); z = PLANE_N.normalized(); x = y.cross(z).normalized(); z = x.cross(y).normalized()
    M = Matrix((x, y, z)).transposed().to_4x4()
    M.translation = G
    pb["grip"].matrix = M
    bpy.context.view_layer.update()

def place_grip(phi, hinge):
    G = hands_at(phi)
    c = rot_about(RADIAL0, PLANE_N, phi + hinge)
    set_grip(G, c)
    return G, G + c * SHAFT_TO_HEAD

def solve_to_ball(lo, hi):
    """phi in [lo, hi] at which the club head reaches the ball; shaft points hands -> ball."""
    best = None
    for i in range(0, 401):
        phi = lo + (hi - lo) * i / 400
        err = abs((BALL - hands_at(phi)).length - SHAFT_TO_HEAD)
        if best is None or err < best[0]:
            best = (err, phi)
    phi = best[1]
    G = hands_at(phi)
    c = (BALL - G).normalized()
    set_grip(G, c)
    # report the equivalent hinge for continuity checks
    r = rot_about(RADIAL0, PLANE_N, phi)
    hinge = math.degrees(math.atan2(r.cross(c).dot(PLANE_N), r.dot(c)))
    return phi, hinge, (G + c * SHAFT_TO_HEAD - BALL).length

report = []
for k in KEYS:
    sc.frame_set(k["f"])
    set_torso(k)
    REACH[0] = k.get("reach", 1.0)
    if PLANE_N is None:
        sh = lead_shoulder()
        PLANE_N = Vector((1, 0, 0)).cross(BALL - sh).normalized()
        RADIAL0 = (BALL - sh).normalized()
    if k["phi"] is None and k["name"] == "impact":
        # hands a touch ahead of the ball (phi -4), weight forward; the spine's tilt away from
        # the target (secondary tilt) is solved so the club still reaches the ball
        best = None
        for i in range(0, 41):
            k["side"] = 12 + 12 * i / 40
            set_torso(k)
            err = abs((BALL - hands_at(-4)).length - SHAFT_TO_HEAD)
            if best is None or err < best[0]:
                best = (err, k["side"])
        k["side"] = best[1]
        set_torso(k)
        report.append(f"impact side tilt solved: {best[1]:.1f} deg (residual {best[0] * 100:.1f} cm)")
        phi, hinge, err = solve_to_ball(-4.01, -3.99)
        k["phi"], k["hinge"] = phi, hinge
        report.append(f"{k['name']}: phi {phi:.1f} hinge {hinge:.1f} head-to-ball {err * 100:.1f} cm")
        G = hands_at(phi); head = BALL
    elif k["phi"] is None:
        phi, hinge, err = solve_to_ball(-5, 40)
        k["phi"], k["hinge"] = phi, hinge
        report.append(f"{k['name']}: phi {phi:.1f} hinge {hinge:.1f} head-to-ball {err * 100:.1f} cm")
        G = hands_at(phi); head = BALL
    else:
        G, head = place_grip(k["phi"], k["hinge"])
    pb = arm.pose.bones
    for name in ("hips", "spine", "chest", "neck", "head", "thigh.L", "shin.L", "foot.L", "thigh.R", "shin.R", "foot.R"):
        pb[name].keyframe_insert("rotation_euler", frame=k["f"])
    pb["hips"].keyframe_insert("location", frame=k["f"])
    pb["grip"].keyframe_insert("location", frame=k["f"])
    pb["grip"].keyframe_insert("rotation_quaternion", frame=k["f"])
    trail = (pb["upper_arm.R"].head - (G + rot_about(RADIAL0, PLANE_N, k["phi"] + k["hinge"]) * 0.10)).length
    report.append(f"  f{k['f']} {k['name']}: hands z {G.z:.2f} head {tuple(round(c, 2) for c in head)} trail-reach {trail:.2f}")

action = arm.animation_data.action
action.name = "Swing"
# ease: Bezier everywhere except a snappy downswing
def action_fcurves(act):
    if hasattr(act, "fcurves"):
        return list(act.fcurves)
    return [fc for layer in act.layers for strip in layer.strips for cb in strip.channelbags for fc in cb.fcurves]
for fc in action_fcurves(action):
    for kp in fc.keyframe_points:
        kp.interpolation = 'BEZIER'
        kp.handle_left_type = kp.handle_right_type = 'AUTO_CLAMPED'
sc.frame_set(0)

# ------------------------------------------------------------------ ball, floor, cameras, light for previews
bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.021, location=BALL)
ball = bpy.context.view_layer.objects.active; ball.name = "PREVIEW_BALL"
ball.data.materials.append(material("MAT_BALL", srgb(250, 250, 250), 0.4))
bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 0, 0))
floor = bpy.context.view_layer.objects.active; floor.name = "PREVIEW_FLOOR"
floor.data.materials.append(material("MAT_GRASS", srgb(118, 208, 56)))
def cam(name, loc, target):
    c = bpy.data.cameras.new(name); o = bpy.data.objects.new(name, c); sc.collection.objects.link(o)
    o.location = loc
    o.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    c.lens = 40
    return o
cam_face = cam("CAM_FACE_ON", (-0.2, -4.2, 1.1), (-0.2, 0, 0.95))
cam_dtl = cam("CAM_DOWN_LINE", (-4.2, -0.5, 1.15), (0.2, -0.3, 0.9))
cam_close = cam("CAM_CLOSE", (-1.0, -2.1, 1.35), (-0.22, 0.0, 1.2)); cam_close.data.lens = 60
sun = bpy.data.lights.new("SUN", 'SUN'); sun.energy = 3.5; sun.angle = math.radians(5)
so = bpy.data.objects.new("SUN", sun); sc.collection.objects.link(so); so.rotation_euler = (math.radians(50), 0, math.radians(30))
w = bpy.data.worlds.new("World"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = srgb(160, 210, 248)
w.node_tree.nodes["Background"].inputs[1].default_value = 0.9

os.makedirs(os.path.dirname(BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=BLEND)
result = {"report": report, "bones": len(arm.data.bones), "verts": len(body_me.vertices), "faces": len(body_me.polygons)}
