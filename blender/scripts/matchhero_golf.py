"""Adnan's match heroes swing our golf clips.

The golfer is Adnan's rigged match hero (HERO_MAINSTAY, branch newmapsandmenus): the grey body, the painted
face, the white tennis kit (polo, shorts or skort, socks, shoes) and his default hair, on his 53-bone
rig (named after the humanoid bones). This script builds ONE golfer FBX per sex from his sources and gives
it the game's golf clips:

    Blender -b --factory-startup --python blender/scripts/matchhero_golf.py -- --sex Male|Female \
        [--src blender/matchhero] [--out Unity/Assets/Resources/Hero] [--preview <dir>] [--frames 1,53,71,91]
        [--only Drive,Putt] [--assemble-only]

Sources (blender/scripts/fetch_matchheroes.sh downloads them from his branch's Git LFS):
    <src>/<Sex>_ReadyIdle.fbx    rig + body + face + racket        <src>/<Sex>_Kit.fbx   same rig + the six kit meshes
    <src>/Hair_Default_<M|F>.fbx his hair (static), placed at the HairRoot of docs/matchhero-golfer.md

The golf clips (Drive, IronSwing, HalfSwing, Chip, Putt, Idle, Wave, Cheer, FistPump) were authored on the studio's
V4 rig (Unity/Assets/Resources/Golfer/golfer_m.fbx). The two skeletons rest differently (V4's arms hang, his are
angled out; his legs are longer, his shoulders narrower), so a rotation copy would leave the hands off the club.
Instead each frame is rebuilt on his skeleton:

  * the pelvis, spine, neck and head take the V4 bones' world rotation change from rest, and the hips travel as V4's did,
    scaled by the height ratio (a similarity map X about the hips: same ground, same swing, a taller golfer);
  * each leg and arm is solved by two-bone IK to where V4's ankle or wrist was (through X), the knee and elbow on the
    side V4 bent them; an arm bone first has its rest direction aimed at V4's, so the twist is the V4 arm's;
  * the club is V4's club (scaled by X) moved by however far the lead hand fell short of V4's wrist, so it never
    leaves the hands, and the fingers close round the handle in the finger bones' own curl axis (local X).

Writes <out>/golfer_<m|f>.fbx (rig, body, face, kit, hair, the four clubs, one take per clip) and
<out>/golfer_<m|f>_clips.json (top and impact frames, and where the clubhead is at address and impact).
"""
import bpy, sys, os, math, json
from mathutils import Vector, Matrix, Quaternion

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    return ARGV[ARGV.index(name) + 1] if name in ARGV else default


SEX = arg("--sex", "Male")
FEMALE = SEX == "Female"
SRC = arg("--src", os.path.join(REPO, "blender", "matchhero"))
OUT = arg("--out", os.path.join(REPO, "Unity", "Assets", "Resources", "Hero"))
V4 = arg("--v4", os.path.join(REPO, "Unity", "Assets", "Resources", "Golfer", "golfer_m.fbx"))
PREVIEW = arg("--preview")
ONLY = arg("--only")
FRAMES = [int(x) for x in arg("--frames", "").split(",") if x]
ASSEMBLE_ONLY = "--assemble-only" in ARGV
TAG = "f" if FEMALE else "m"

CLIPS = ["Drive", "IronSwing", "HalfSwing", "Chip", "Putt", "Idle", "Wave", "Cheer", "FistPump"]
HELD = {"Drive": "Driver", "IronSwing": "Iron", "HalfSwing": "Iron", "Chip": "Wedge", "Putt": "Putter"}
# Adnan's own clips (Characters/MatchHeroes/<Sex>/<Sex>_<Name>.fbx): on this very rig, so they are copied over as they are (30 fps, in place)
BORROWED = ["Walk", "RunForward", "Emote_Scuba", "Emote_Spike", "Emote_Thrust", "Intro_BringIt", "Intro_Pushups", "Intro_Wave"]
if ONLY:
    CLIPS = [c for c in CLIPS if c in ONLY.split(",")]
    BORROWED = [c for c in BORROWED if c in ONLY.split(",")]

# the triangle budget per part after the reduction pass (his meshes are 20-90k each)
BUDGET = {"Body": 26000 if SEX == "Male" else 22000, "Kit_Top": 9000, "Kit_Bottom": 7000, "Kit_Shoe_L": 4500, "Kit_Shoe_R": 4500, "Kit_Sock_L": 2000, "Kit_Sock_R": 2000}
# his default hair sits on the head at the HairRoot of his README (Unity Y-up metres)
HAIR_ROOT_Z = 1.5572519 if FEMALE else 1.5795953

V4_LANDMARKS = {c["name"]: c for c in json.load(open(os.path.splitext(V4)[0] + "_clips.json"))["clips"]}
EXTEND = float(arg("--extend", "1.0"))      # how much of his arm a golfer's swing leaves (the arc away from the ball is wider than V4's short arms made it)

# V4 bone -> his bone
SIDES = (("L", "Left"), ("R", "Right"))
NO_PUSH = "--nopush" in ARGV
NO_ARC = "--noarc" in ARGV
ALPHA0 = {}
# A real golfer's address: the clubs at their real lengths, the hands at belt height, the back hinged from the hips. V4's golfer is short and stylised (hands at
# chest height on a 1.3 m club-and-golfer); "--v4like" keeps its poses scaled up, as the first version did.
REAL = "--v4like" not in ARGV
# real club lengths in metres (driver 45", iron ~38", wedge 36", putter 34") against the studio club meshes' own lengths (1.156, 1.018, 0.955, 0.906)
CLUB_LENGTH = {"Driver": 1.14, "Iron": 0.97, "Wedge": 0.92, "Putter": 0.86}
V4_CLUB_LENGTH = {"Driver": 1.156, "Iron": 1.018, "Wedge": 0.955, "Putter": 0.906}
HINGE_BONES = ("Spine", "Chest", "UpperChest", "LeftShoulder", "RightShoulder")
# a golfer sits back and bends the knees: the pelvis goes back (away from the ball: +X in the clips) and down while the feet stay; the back hinges forward to bring
# the shoulders over the hands. Per club: how far back and how far down (metres): the driver's ball is the farthest away, the putter's the nearest.
STANCE_BACK = {"Driver": 0.20, "Iron": 0.12, "Wedge": 0.10, "Putter": 0.08}
STANCE_DROP = float(arg("--drop", "0.04"))
FEET_SHARE = float(arg("--feet-share", "0.5"))
HINGE_MIN, HINGE_MAX = float(arg("--hinge-min", "12")), float(arg("--hinge-max", "38"))
ARM_EASE = float(arg("--arm-ease", "0.93"))     # how much of his arm's length a golfer's address leaves: the arms hang a touch bent
BONE_MAP = {"Root": "Root", "Hips": "Hips", "Spine": "Spine", "Chest": "Chest", "Neck": "Neck", "Head": "Head"}
for s, side in SIDES:
    for a, b in (("Shoulder", "Shoulder"), ("UpperArm", "UpperArm"), ("LowerArm", "LowerArm"), ("Hand", "Hand"),
                 ("UpperLeg", "UpperLeg"), ("LowerLeg", "LowerLeg"), ("Foot", "Foot"), ("Toes", "Toes")):
        BONE_MAP[f"{a}.{s}"] = side + b
# his bones that follow another V4 bone's rotation (V4 has no UpperChest)
FOLLOW = {"UpperChest": "Chest"}
# FK bones, parent first (arms and legs below the shoulder and hip are solved by IK)
FK = ["Root", "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "LeftShoulder", "RightShoulder"]
FINGERS = ("Index", "Middle", "Ring", "Little")
# the curl of each finger bone round a handle, degrees about the bone's own X (the same sign on both hands)
GRIP = {"Index": (50, 70, 40), "Middle": (55, 75, 45), "Ring": (62, 80, 45), "Little": (68, 82, 45)}
GRIP_THUMB = {"Proximal": 0, "Intermediate": 0, "Distal": 0}

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def remove(o):
    if o and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)


def tris(o): return sum(len(p.vertices) - 2 for p in o.data.polygons)


# ======================================================================= the sources
v4_objs = load(V4)
v4 = next(o for o in v4_objs if o.type == 'ARMATURE')
v4.data.pose_position = 'POSE'
club_meshes = [o for o in v4_objs if o.type == 'MESH' and o.name.startswith("CLUB_")]

hero_objs = load(os.path.join(SRC, f"{SEX}_ReadyIdle.fbx"))
rig = next(o for o in hero_objs if o.type == 'ARMATURE')
rig.animation_data_clear()
for a in list(bpy.data.actions):
    if not a.name.endswith(tuple("|" + c for c in CLIPS)) and not a.name.startswith("Male_Golf_Rig|"): bpy.data.actions.remove(a)
rig.data.pose_position = 'REST'
for pb in rig.pose.bones:
    pb.rotation_mode = 'QUATERNION'; pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.scale = (1, 1, 1)
bpy.context.view_layer.update()
body = next(o for o in hero_objs if o.type == 'MESH' and o.name.startswith("Body_"))
face = next(o for o in hero_objs if o.type == 'MESH' and o.name.startswith("Face_"))
for o in hero_objs:   # the tennis racket and its socket
    if o in (rig, body, face): continue
    remove(o)
body.name = "Body"; body.data.name = "Body"
face.name = "Face"; face.data.name = "Face"


def skin_rigid(o, bone):
    """A static mesh carried by one bone: its vertices weighted 1.0 to it, an armature modifier, parented to the rig."""
    world = o.matrix_world.copy()
    o.parent = None
    o.matrix_world = world
    for vg in list(o.vertex_groups): o.vertex_groups.remove(vg)
    vg = o.vertex_groups.new(name=bone)
    vg.add([v.index for v in o.data.vertices], 1.0, 'REPLACE')
    for m in list(o.modifiers): o.modifiers.remove(m)
    mod = o.modifiers.new("Armature", 'ARMATURE'); mod.object = rig
    o.parent = rig; o.matrix_parent_inverse = rig.matrix_world.inverted()
    o.parent_type = 'OBJECT'


def rebind(o):
    """A mesh skinned to a copy of the rig: the same vertex groups on this rig."""
    world = o.matrix_world.copy()
    o.parent = rig
    for m in o.modifiers:
        if m.type == 'ARMATURE': m.object = rig
    o.matrix_parent_inverse = rig.matrix_world.inverted()
    o.matrix_world = world


# the painted face: not skinned in his FBX (a child of the head), so it is skinned rigidly to the Head bone
skin_rigid(face, "Head")

# the kit: skinned in his Kit FBX to a copy of this very rig
kit_objs = load(os.path.join(SRC, f"{SEX}_Kit.fbx"))
kit_rig = next(o for o in kit_objs if o.type == 'ARMATURE')
kit = [o for o in kit_objs if o.type == 'MESH']
for o in kit: rebind(o)
remove(kit_rig)

# his default hair, rigid on the Head bone, at the HairRoot
hair_objs = load(os.path.join(SRC, f"Hair_Default_{'Female' if FEMALE else 'Male'}.fbx"))
hair = next(o for o in hair_objs if o.type == 'MESH')
hair.name = "Hair_Default"; hair.data.name = "Hair_Default"
hair.parent = None
HAIR_LIFT = float(arg("--hair-lift", "1.03"))     # his hair lies on the scalp: lifted 3 %, the two never z-fight
for v in hair.data.vertices: v.co *= HAIR_LIFT
hair.location = Vector((0, 0, HAIR_ROOT_Z)) + Vector(hair.location)
bpy.context.view_layer.update()
skin_rigid(hair, "Head")
for o in hair_objs:
    if o is not hair: remove(o)

# ======================================================================= scale: V4 -> his rig
def rest_rot(arm, n): return arm.data.bones[n].matrix_local.to_3x3()
def rest_head(arm, n): return arm.data.bones[n].head_local.copy()
def rest_dir(arm, n):
    b = arm.data.bones[n]; return (b.tail_local - b.head_local).normalized()


S = rest_head(rig, "Hips").z / rest_head(v4, "Hips").z      # one similarity for the whole swing: same ground, a taller golfer
A0 = rest_head(rig, "Hips"); B0 = rest_head(v4, "Hips")
print(f"{SEX}: scale {S:.4f}")


OFFSET = Vector((0, 0, 0))     # the clip's own shift on the ground (set per clip, so the club head meets the ball where V4's did)


def X(p):
    """A V4 armature-space point on his skeleton."""
    return Vector((A0.x + S * (p.x - B0.x), A0.y + S * (p.y - B0.y), A0.z + S * (p.z - B0.z))) + OFFSET


# the clubs: V4's, scaled by the same map, skinned to a Club bone at the origin (rest = identity, so mesh coordinates are the bone's)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones.new("Club"); eb.head = (0, 0, 0); eb.tail = (0, 0.1, 0); eb.roll = 0; eb.use_deform = True
bpy.ops.object.mode_set(mode='OBJECT')
for o in club_meshes:
    o.parent = rig
    for m in o.modifiers:
        if m.type == 'ARMATURE': m.object = rig
    o.matrix_parent_inverse = Matrix.Identity(4)
    if not REAL:
        for v in o.data.vertices: v.co *= S


# ======================================================================= the hidden skin
def strip_hidden():
    """Delete the body skin the clothes cover (the torso and the upper arms under the polo, the thighs under the shorts, the legs under the
    socks, the feet in the shoes): it cannot be seen, it costs triangles, and a thinned body would poke through the cloth in a deep bend.
    A face is covered when a ray out along its normal meets the kit."""
    from mathutils.bvhtree import BVHTree
    import bmesh
    kbm = bmesh.new()
    for o in kit: kbm.from_mesh(o.data)
    bvh = BVHTree.FromBMesh(kbm)
    bm = bmesh.new(); bm.from_mesh(body.data); bm.faces.ensure_lookup_table()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)   # one consistent outward side, whatever the source did
    bm.normal_update()
    covered = {}
    neck_z = rest_head(rig, "Neck").z - 0.05      # the neck and the head are never stripped: the collar's edge would show a ragged skin
    for f in bm.faces:
        c = f.calc_center_median()
        hit = bvh.ray_cast(c + f.normal * 0.0005, f.normal, 0.09)
        # (the girl's skort is a wrap, open at the side: her thighs stay, whole, so no cut edge of skin shows in the slit)
        covered[f.index] = hit[0] is not None and c.z < neck_z and not (FEMALE and c.z < rest_head(rig, "Hips").z)
    drop = []
    for f in bm.faces:
        nb = [nf.index for e in f.edges for nf in e.link_faces if nf is not f]
        # a face is dropped when it is covered, or when it is a scrap among covered faces (it would stick out of the cloth in a deep bend)
        if covered[f.index] or sum(1 for i in nb if covered[i]) >= 2: drop.append(f)
    n0 = len(bm.faces)
    bmesh.ops.delete(bm, geom=drop, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    # what is left in scraps (islands of a few hundred triangles at the waist, the hip, the shoulder) would show through the cloth in a bend: gone
    bm.faces.ensure_lookup_table()
    seen, islands = set(), []
    for f0 in bm.faces:
        if f0.index in seen: continue
        stack, comp = [f0], []
        seen.add(f0.index)
        while stack:
            f = stack.pop(); comp.append(f)
            for e in f.edges:
                for nf in e.link_faces:
                    if nf.index not in seen: seen.add(nf.index); stack.append(nf)
        islands.append(comp)
    small = [comp for comp in islands if sum(len(f.verts) - 2 for f in comp) < 1500]
    scraps = [f for comp in small for f in comp]
    n_scraps, n_islands, n_pieces = len(scraps), len(small), len(islands)
    bmesh.ops.delete(bm, geom=scraps, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    print(f"  scraps: {n_scraps} faces in {n_islands} islands deleted; {n_pieces} pieces in all")
    bm.to_mesh(body.data); bm.free(); kbm.free()
    body.data.update()
    print(f"  hidden skin: {len(drop)} of {n0} body faces deleted, {tris(body)} tris left")


# ======================================================================= the reduction pass
def reduce(o, target, weights=None):
    """Collapse-decimate o to about `target` triangles. `weights` (vertex -> 0..1) says how much each vertex is protected: the head and the hands keep their shape."""
    n = tris(o)
    if n <= target: return
    if weights:
        vg = o.vertex_groups.new(name="_keep")
        for w in sorted(set(weights.values())):
            vg.add([i for i, x in weights.items() if x == w], w, 'REPLACE')
    mod = o.modifiers.new("Decimate", 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'; mod.ratio = target / n; mod.use_collapse_triangulate = True
    if weights: mod.vertex_group = "_keep"; mod.invert_vertex_group = True; mod.vertex_group_factor = 1.0
    with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
        bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if weights: o.vertex_groups.remove(o.vertex_groups["_keep"])
    print(f"  {o.name}: {n} -> {tris(o)} tris")


def reduce_all():
    neck_z = rest_head(rig, "Neck").z + 0.03
    w = {}
    for v in body.data.vertices:
        p = body.matrix_world @ v.co
        if p.z > neck_z: w[v.index] = 1.0
        elif abs(p.x) > 0.40 and p.z < 1.25: w[v.index] = 0.45   # the hands
    reduce(body, BUDGET["Body"], w)
    for o in kit:
        if o.name in BUDGET: reduce(o, BUDGET[o.name])


def inset_under_kit(margin=0.008):
    """Body skin that lies under the clothes goes at least `margin` inside them, so cloth and skin can bend a little differently through
    an extreme swing without the skin showing through (the kit's face normals may point either way: the sign is read off the data)."""
    from mathutils.bvhtree import BVHTree
    import bmesh
    bm = bmesh.new()
    for o in kit: bm.from_mesh(o.data)
    bm.normal_update()
    bvh = BVHTree.FromBMesh(bm)
    inv = body.matrix_world.inverted()
    hits = []
    for v in body.data.vertices:
        w = body.matrix_world @ v.co
        h = bvh.find_nearest(w, 0.03)
        if h[0] is not None: hits.append((v, w, h[1], (w - h[0]).dot(h[1])))
    pos = sum(1 for *_x, d in hits if d > 0)
    sgn = 1.0 if pos >= len(hits) / 2 else -1.0
    moved = 0
    for v, w, nrm, d in hits:
        dd = d * sgn
        if dd < margin:
            v.co = inv @ (w + nrm * sgn * (margin - dd))
            moved += 1
    bm.free()
    print(f"  inset {moved} of {len(hits)} body vertices near the kit to {margin * 1000:.0f} mm under it")


def follow_body():
    """The kit takes the body's skin weights (nearest surface), so cloth and skin bend together through an extreme swing."""
    for o in kit:
        for vg in list(o.vertex_groups): o.vertex_groups.remove(vg)
        for vg in body.vertex_groups: o.vertex_groups.new(name=vg.name)
        mod = o.modifiers.new("Transfer", 'DATA_TRANSFER')
        mod.object = body; mod.use_vert_data = True
        mod.data_types_verts = {'VGROUP_WEIGHTS'}
        mod.vert_mapping = 'POLYINTERP_NEAREST'
        mod.layers_vgroup_select_src = 'ALL'; mod.layers_vgroup_select_dst = 'NAME'
        mod.mix_mode = 'REPLACE'; mod.mix_factor = 1.0
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
            bpy.ops.object.modifier_apply(modifier=mod.name)
    for o in [body] + kit:
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
            bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
    print("  kit weights follow the body:", {o.name: len(o.vertex_groups) for o in kit})


# ======================================================================= hairstyles (blender/scripts/matchhero_hair.py), made against his head before it is reduced
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import matchhero_hair
STYLE_OBJS = {}


def hair_material(role_name):
    """The FBX's own material names for the hair roles: Hair_M / Hair_F for the colour, Hair_Dark for the deeper strands."""
    name = ("Hair_F" if FEMALE else "Hair_M") if role_name == "Hero_Hair" else "Hair_Dark"
    m = bpy.data.materials.get(name)
    if m is None: m = bpy.data.materials.new(name)
    return m


def make_styles():
    made = matchhero_hair.build(body, FEMALE, rest_head(rig, "Neck").z)
    scalp = made.pop("Scalp")
    for name, o in made.items():
        o.name = o.data.name = "Hair_" + name
        STYLE_OBJS[name] = o
    # the classic: his own hair, with a scalp cap in the hair's colour under it
    classic = join_objs("Hair_Classic", [hair, scalp])
    STYLE_OBJS["Classic"] = classic
    for o in STYLE_OBJS.values():
        for i, m in enumerate(list(o.data.materials)):
            if m and m.name.split(".")[0] in ("Hero_Hair", "Hero_HairB"): o.data.materials[i] = hair_material(m.name.split(".")[0])
    for name, o in STYLE_OBJS.items():
        if name != "Classic": skin_rigid(o, "Head")


def join_objs(name, objs):
    """His hair (already on the Head bone) and the scalp cap as one mesh."""
    import avatar_kit as _K
    for o in objs: o.parent = None
    out = _K.join(name, objs)
    # his hair material first, the cap's (renamed below) after
    return out


if "--nostyles" not in ARGV:
    # (his own mesh is object `hair`; the classic joins it to the scalp, so it has to be joined before it is made rigid)
    hair.parent = None
    for vg in list(hair.vertex_groups): hair.vertex_groups.remove(vg)
    for m_ in list(hair.modifiers): hair.modifiers.remove(m_)
    make_styles()
    hair = STYLE_OBJS["Classic"]
    skin_rigid(hair, "Head")
    for n_, o_ in STYLE_OBJS.items(): print(f"  hair {n_}: {tris(o_)} tris")
    if "--hair-check" in ARGV:
        for n_, o_ in STYLE_OBJS.items():
            bad = matchhero_hair.exposed(o_, n_)
            rows = {}
            for az_, el_ in bad: rows.setdefault(el_, []).append(az_)
            print(f"HAIRCHECK {SEX} {n_}: {len(bad)} directions show the scalp above the hairline" + ("" if not bad else "; e.g. " + ", ".join(f"el {e}: az {min(a)}..{max(a)} ({len(a)})" for e, a in list(rows.items())[:6])))
else:
    STYLE_OBJS["Classic"] = hair
    hair.name = hair.data.name = "Hair_Classic"

if "--follow" in ARGV:
    follow_body()          # (from the whole body, before its covered skin goes)
if "--nostrip" not in ARGV:
    strip_hidden()
if "--noreduce" not in ARGV:
    reduce_all()
if "--inset" in ARGV:
    inset_under_kit(float(arg("--inset-mm", "6")) / 1000)

parts = [body, face] + [STYLE_OBJS[n] for n in (["Classic"] + [k for k in STYLE_OBJS if k != "Classic"])] + kit
total = sum(tris(o) for o in parts)
print(f"{SEX}: {total} tris in the golfer, {sum(tris(o) for o in club_meshes)} in the four clubs")
if ASSEMBLE_ONLY and not PREVIEW: sys.exit(0)

# ======================================================================= retarget
names_fk = [n for n in FK if n in rig.data.bones]
LIMBS = {}   # arm/leg chains: (root, mid, end) on his rig and the V4 names
for s, side in SIDES:
    LIMBS[f"arm{s}"] = (f"{side}UpperArm", f"{side}LowerArm", f"{side}Hand", f"UpperArm.{s}", f"LowerArm.{s}", f"Hand.{s}")
    LIMBS[f"leg{s}"] = (f"{side}UpperLeg", f"{side}LowerLeg", f"{side}Foot", f"UpperLeg.{s}", f"LowerLeg.{s}", f"Foot.{s}")
V4_OF = {v: k for k, v in BONE_MAP.items()}
parent_of = {b.name: (b.parent.name if b.parent else None) for b in rig.data.bones}


def arc(frm, to): return frm.rotation_difference(to).to_matrix()


def fix_for(n):
    """The rotation that turns his rest direction of limb bone n to V4's (about the joint): his arms rest angled out, V4's hang."""
    v4n = V4_OF.get(n)
    if not v4n or not any(n.endswith(k) for k in ("UpperArm", "LowerArm", "Hand")): return Matrix.Identity(3)
    return arc(rest_dir(rig, n), rest_dir(v4, v4n))


FIX = {n: fix_for(n) for n in rig.data.bones.keys()}
LEN = {}
for key, (a, b, c, *_r) in LIMBS.items():
    LEN[key] = ((rest_head(rig, b) - rest_head(rig, a)).length, (rest_head(rig, c) - rest_head(rig, b)).length)


KK = EXTEND * (LEN["armL"][0] + LEN["armL"][1]) / ((rest_head(v4, "LowerArm.L") - rest_head(v4, "UpperArm.L")).length + (rest_head(v4, "Hand.L") - rest_head(v4, "LowerArm.L")).length)


def two_bone(Sp, E, T, l1, l2, radius=None):
    """The elbow of a two-bone chain from Sp to reach T with lengths l1, l2, on E's side of the Sp->T line, and the end it reaches.
    `radius` (p -> how deep p is in the torso, 1 on the skin) turns the elbow about the Sp->T line, the least it takes, until it is out of the torso."""
    d = (T - Sp).length
    d = min(max(d, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    u = (T - Sp).normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    h = math.sqrt(max(l1 * l1 - a * a, 0))
    v = (E - Sp) - u * (E - Sp).dot(u)
    if v.length < 1e-6: v = Vector((0, -1, 0)) - u * Vector((0, -1, 0)).dot(u)
    v.normalize()
    elbow = Sp + u * a + v * h
    if radius is not None and radius(elbow) < 1.0:
        w = u.cross(v)
        for k in range(1, 37):
            for sign in (1, -1):
                ang = math.radians(5 * k) * sign
                cand = Sp + u * a + (v * math.cos(ang) + w * math.sin(ang)) * h
                if radius(cand) >= 1.0: return cand, Sp + u * d
    return elbow, Sp + u * d


def bone_dir(R): return (R @ Vector((0, 1, 0))).normalized()


# the torso an arm must stay out of: an ellipse round the spine (his half-width and half-depth, and an arm's thickness and the sleeve's more)
TORSO_A, TORSO_B = (0.19 + 0.05, 0.13 + 0.05) if not FEMALE else (0.165 + 0.05, 0.12 + 0.05)


def torso_frame(posed, heads):
    d = posed["Chest"] @ rest_rot(rig, "Chest").inverted()
    return d @ Vector((1, 0, 0)), d @ Vector((0, -1, 0)), d @ Vector((0, 0, 1))


def torso_r(p, posed, heads):
    """How deep p is in the torso: 1 on its skin, 0 on the spine, large outside; 9 beyond the neck and the hips."""
    lat, fwd, up = torso_frame(posed, heads)
    v = p - heads["Hips"]
    t = v.dot(up)
    if not (0.02 < t < rest_head(rig, "Neck").z - rest_head(rig, "Hips").z): return 9.0
    return math.sqrt((v.dot(lat) / TORSO_A) ** 2 + (v.dot(fwd) / TORSO_B) ** 2)


def out_of_torso(p, posed, heads, clear=1.0):
    """p, or the nearest place outside the torso's ellipse (only between the hips and the neck's base)."""
    lat, fwd, up = torso_frame(posed, heads)
    base = heads["Hips"]
    v = p - base
    t = v.dot(up)
    if not (0.02 < t < rest_head(rig, "Neck").z - rest_head(rig, "Hips").z): return p
    x, y = v.dot(lat), v.dot(fwd)
    r = math.sqrt((x / TORSO_A) ** 2 + (y / TORSO_B) ** 2)
    if r >= clear: return p
    if r < 1e-4: x, y, r = 0.0, TORSO_B * 0.01, 0.01
    k = clear / r
    return base + lat * (x * k) + fwd * (y * k) + up * t


def club_target(sp, clip):
    """The club in this frame: V4's head path and shaft angle, at a real club's length (the grip end follows from the head), and where V4's
    hands were on it (the same distances from the grip end). Returns the club's matrix and the two hands' places, or None."""
    if clip not in HELD: return None
    club = HELD[clip]
    m = next(o for o in club_meshes if o.name == "CLUB_" + club.upper())
    hl = min((v.co for v in m.data.vertices), key=lambda v: v.z).copy()          # (the studio's club, unscaled: its head end, in the club bone's own space)
    M = sp["Club"].matrix
    loc, rot, scl = M.decompose()
    R = rot.to_matrix()
    k = CLUB_LENGTH[club] / V4_CLUB_LENGTH[club]
    head = M @ hl
    origin = head - R @ Vector((hl.x, hl.y, hl.z * k))
    hands = {s: origin + R @ (R.inverted() @ (sp[f"Hand.{s}"].head - loc)) for s in ("L", "R")}
    return Matrix.Translation(origin) @ R.to_4x4() @ Matrix.Diagonal(Vector((1, 1, k, 1))), hands, hl


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def arc_weight(clip, i):
    """How much of the swing's arc is his own rather than V4's: 0 at address and through the ball (the club head must be where V4's was: it is the ball's),
    1 in the backswing, at the top and in the follow-through, where V4's short arms folded and his reach on."""
    L = V4_LANDMARKS[clip]; top, imp = L["top"], L["impact"]
    if i <= 8: return 0.0
    if i < top - 8: return smooth((i - 8) / max(1, top - 16))
    if i <= top + 3: return 1.0
    if i < imp - 5: return 1.0 - smooth((i - (top + 3)) / max(1, imp - 8 - top))
    if i <= imp + 3: return 0.0
    return smooth((i - (imp + 3)) / 9.0)


def retarget_clip(clip):
    act = next(a for a in bpy.data.actions if a.name.endswith("|" + clip) and a.name.startswith("Male_Golf_Rig"))
    v4.animation_data_create(); v4.animation_data.action = act
    try: v4.animation_data.action_slot = act.slots[0]
    except Exception: pass
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    held = clip in HELD
    real = REAL and held

    def inputs(f):
        scn.frame_set(f)
        sp = v4.pose.bones
        delta = {}
        for n in rig.data.bones.keys():
            src = BONE_MAP_INV.get(n)
            if src: delta[n] = sp[src].matrix.to_3x3().normalized() @ rest_rot(v4, src).inverted()
        return sp, delta

    def trunk(sp, delta, alpha):
        """The hips, the spine, the neck, the head and the collar bones: the V4 bone's world rotation change, the hips travelling as V4's did,
        and (a real address) the back hinged forward `alpha` about the lateral axis from the spine's base."""
        posed, heads, basis = {}, {}, {}

        def put(n, R, head=None):
            p = parent_of[n]
            if p is None:
                base = rest_rot(rig, n)
                exp = rest_head(rig, n)
            else:
                base = posed[p] @ rest_rot(rig, p).inverted() @ rest_rot(rig, n)
                exp = heads[p] + posed[p] @ rest_rot(rig, p).inverted() @ (rest_head(rig, n) - rest_head(rig, p))
            h = exp if head is None else head
            basis[n] = (base.inverted() @ R, base.inverted() @ (h - exp))
            posed[n] = R; heads[n] = h

        hinge = Matrix.Rotation(alpha, 3, delta["Hips"] @ Vector((1, 0, 0))) if alpha else None
        seat = Vector((STANCE_BACK[HELD[clip]], 0, -STANCE_DROP)) if real else Vector((0, 0, 0))
        for n in names_fk:
            dsrc = delta[n] if n in delta else delta[FOLLOW[n]]
            R = dsrc @ rest_rot(rig, n)
            if hinge is not None and n in HINGE_BONES: R = hinge @ R
            head = None
            if n == "Root":
                head = rest_head(rig, n) + S * (sp["Root"].head - rest_head(v4, "Root")) + OFFSET
            elif n == "Hips":
                head = X(sp["Hips"].head) + seat
            put(n, R, head)
        return posed, heads, basis, put

    def shoulder(posed, heads, n):
        par = parent_of[n]
        return heads[par] + posed[par] @ rest_rot(rig, par).inverted() @ (rest_head(rig, n) - rest_head(rig, par))

    # ---- a real address: how far the back is hinged forward in every frame (just enough that his arms reach the hands on the club at a comfortable bend)
    alphas = {}
    if real:
        arm_len = {s: sum(LEN[f"arm{s}"]) for s, _ in SIDES}
        for f in range(f0, f1 + 1):
            sp, delta = inputs(f)
            hands = club_target(sp, clip)[1]
            alphas[f] = math.radians(60)
            for deg in range(-6, 61, 2):
                posed, heads, _b, _p = trunk(sp, delta, math.radians(deg))
                ok = all((shoulder(posed, heads, f"{side}UpperArm") - hands[s]).length <= ARM_EASE * arm_len[s] for s, side in SIDES)
                if ok: alphas[f] = math.radians(deg); break
        if "--hinge-follow" in ARGV:
            # smoothed over a few frames, so the hinge never flickers (a golfer's back moves slowly)
            keys = sorted(alphas)
            vals = [alphas[k] for k in keys]
            sm = []
            for i in range(len(vals)):
                lo, hi = max(0, i - 4), min(len(vals), i + 5)
                sm.append(sum(vals[lo:hi]) / (hi - lo))
            alphas = dict(zip(keys, sm))
        else:
            # a golfer keeps the angle of the back from address to finish: the hinge that suits the hands at address, all the way through, except where
            # the hands go somewhere the arms cannot reach from it (the top of the backswing, the finish): there the back comes up, as far as it must
            a0 = max(math.radians(HINGE_MIN), min(math.radians(HINGE_MAX), alphas[f0]))      # (a back hinged 12-38 degrees: the hands at address decide, within what a golfer does)
            if clip == "HalfSwing" and "IronSwing" in ALPHA0: a0 = ALPHA0["IronSwing"]      # (a half swing starts from the iron's address)
            ALPHA0[clip] = a0
            alphas = {}
            for f in range(f0, f1 + 1):
                sp, delta = inputs(f)
                hands = club_target(sp, clip)[1]
                a = a0
                if clip == "HalfSwing":       # (it keeps the iron's back all the way: the first frames only look unreachable because its club starts in the air)
                    alphas[f] = a0
                    continue
                while a > math.radians(-12):
                    posed, heads, _b, _p = trunk(sp, delta, a)
                    if all((shoulder(posed, heads, f"{side}UpperArm") - hands[s]).length <= 0.985 * arm_len[s] for s, side in SIDES): break
                    a -= math.radians(2)
                alphas[f] = a
            keys = sorted(alphas)
            vals = [alphas[k] for k in keys]
            alphas = {k: min(a0, sum(vals[max(0, i - 2):i + 3]) / len(vals[max(0, i - 2):i + 3])) for i, k in enumerate(keys)}

    frames = []
    short_worst = 0.0
    for f in range(f0, f1 + 1):
        sp, delta = inputs(f)
        posed, heads, basis, put = trunk(sp, delta, alphas.get(f, 0.0))
        ct = club_target(sp, clip) if real else None
        if real and not NO_ARC and clip not in ("Putt", "Chip"):       # (a putt or a chip keeps its triangle of arms: V4's hands, as they were)
            w = arc_weight(clip, f - f0)
            if w > 0:
                # the arc is wider than V4's: the lead hand reaches from his shoulder as far as V4's did from its own, scaled by the arms (and the club goes with it)
                Sp = shoulder(posed, heads, "LeftUpperArm")
                want = Sp + (sp["Hand.L"].head - sp["UpperArm.L"].head) * KK
                d = (want - ct[1]["L"]) * w
                ct = (Matrix.Translation(d) @ ct[0], {k: v + d for k, v in ct[1].items()}, ct[2])

        # ---- legs and arms: two-bone IK to where V4's ankle and wrist were
        def solve(key, target, pole, R_root, R_mid, R_end):
            a, b, c = LIMBS[key][:3]
            Sp = shoulder(posed, heads, a)
            l1, l2 = LEN[key]
            newE, newW = two_bone(Sp, pole, target, l1, l2, (lambda q: torso_r(q, posed, heads)) if key.startswith("arm") and not NO_PUSH else None)
            R1 = arc(bone_dir(R_root), (newE - Sp).normalized()) @ R_root
            R2 = arc(bone_dir(R_mid), (newW - newE).normalized()) @ R_mid
            put(a, R1, Sp)
            put(b, R2, newE)
            put(c, R_end, newW)
            return (newW - target).length

        for s, side in SIDES:
            # legs: the ankle goes where V4's went, relative to where each rig rests
            ank = rest_head(rig, f"{side}Foot") + (X(sp[f"Foot.{s}"].head) - X(rest_head(v4, f"Foot.{s}")))
            if real: ank += Vector((STANCE_BACK[HELD[clip]] * FEET_SHARE, 0, 0))      # (the feet step back a little way too: the knees do not have to take all of it)
            a, b, c, va, vb, vc = LIMBS[f"leg{s}"]
            Rs = [delta[x] @ rest_rot(rig, x) for x in (a, b, c)]
            solve(f"leg{s}", ank, X(sp[vb].head), *Rs)
            put(f"{side}Toes", delta[f"{side}Toes"] @ rest_rot(rig, f"{side}Toes"))
        # arms: the lead hand (V4's L) first; the club follows it
        shift = Vector((0, 0, 0))
        for s, side in (("L", "Left"), ("R", "Right")):
            a, b, c, va, vb, vc = LIMBS[f"arm{s}"]
            Rs = [delta[x] @ FIX[x] @ rest_rot(rig, x) for x in (a, b, c)]
            if held:
                want = ct[1][s] if real else X(sp[vc].head)
                tgt = out_of_torso(want + shift, posed, heads) if not NO_PUSH else want + shift
                Sp = shoulder(posed, heads, a)
                # the elbow bends where V4's did, from its own shoulder
                pole = Sp + (sp[vb].head - sp[va].head) if real else X(sp[vb].head)
            else:
                # a free arm (the wave, the cheers) reaches as V4's did from ITS shoulder, scaled by the arm: his shoulders sit lower than V4's scaled ones
                Sp = shoulder(posed, heads, a)
                kk = (LEN[f"arm{s}"][0] + LEN[f"arm{s}"][1]) / ((sp[vb].head - sp[va].head).length + (sp[vc].head - sp[vb].head).length)
                tgt = Sp + (sp[vc].head - sp[va].head) * kk
                pole = Sp + (sp[vb].head - sp[va].head) * kk
            miss = solve(f"arm{s}", tgt, pole, *Rs)
            if held and miss > 0.03 and "--miss" in ARGV: print(f"MISS {clip} f{f} {s}: {miss * 100:.0f} cm (hinge {math.degrees(alphas.get(f, 0.0)):.0f})")
            if s == "L" and held:
                shift = heads[c] - want      # (the club goes with the lead hand: any way it was moved out of the torso, and the trail hand follows)
            short_worst = max(short_worst, miss)
        club_M = None
        if held:
            if real:
                club_M = Matrix.Translation(shift) @ ct[0]
            else:
                loc, rot, scl = sp["Club"].matrix.decompose()
                # (V4 stretches or shortens each club along its shaft: 0.99 the driver, 1.13-1.15 the irons and the putter; the same on his scaled club)
                club_M = Matrix.Translation(X(loc) + shift) @ rot.to_matrix().to_4x4() @ Matrix.Diagonal(Vector((scl.x, scl.y, scl.z, 1)))
        frames.append((f, {"basis": basis, "club": club_M, "posed": posed, "heads": heads, "v4club": sp["Club"].matrix.copy(), "alpha": alphas.get(f, 0.0)}))
    if real: print(f"{clip}: back hinged {math.degrees(alphas[f0]):.0f} deg at address, {math.degrees(min(alphas.values())):.0f}-{math.degrees(max(alphas.values())):.0f} through the swing")
    print(f"{clip}: {len(frames)} frames, worst reach shortfall {short_worst * 100:.1f} cm")
    return f0, f1, frames


BONE_MAP_INV = {}
for k, v in BONE_MAP.items(): BONE_MAP_INV[v] = k
for fol, src in FOLLOW.items(): BONE_MAP_INV.setdefault(fol, None)


def write_action(name, f0, f1, frames_data, held, fingers=True):
    """An action on his rig from per-frame basis values (bulk F-curves; Blender 5 layered actions)."""
    for pb in rig.pose.bones: pb.rotation_mode = 'QUATERNION'
    rig.animation_data_create()
    act = bpy.data.actions.new(name)
    rig.animation_data.action = act
    slot = act.slots.new('OBJECT', rig.name) if not len(act.slots) else act.slots[0]
    rig.animation_data.action_slot = slot
    layer = act.layers.new("L") if not len(act.layers) else act.layers[0]
    strip = layer.strips.new(type='KEYFRAME') if not len(layer.strips) else layer.strips[0]
    cb = strip.channelbag(slot, ensure=True)
    n_f = len(frames_data)

    def curve(path, idx, values):
        fc = cb.fcurves.new(path, index=idx)
        fc.keyframe_points.add(n_f)
        co = []
        for (fr, _), v in zip(frames_data, values): co += [float(fr), float(v)]
        fc.keyframe_points.foreach_set('co', co)
        for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
        fc.update()

    def quats(getter):
        qs = [getter(rec) for _, rec in frames_data]
        for i in range(1, len(qs)):
            if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
        return qs

    bones = [b for b in rig.data.bones.keys() if b in frames_data[0][1]["basis"]]
    for n in bones:
        qs = quats(lambda rec: rec["basis"][n][0].to_quaternion())
        for k in range(4): curve(f'pose.bones["{n}"].rotation_quaternion', k, [q[k] for q in qs])
        if n in ("Root", "Hips"):
            for k in range(3): curve(f'pose.bones["{n}"].location', k, [rec["basis"][n][1][k] for _, rec in frames_data])
    # the fingers close round the handle while a club is held
    for s, side in SIDES:
        for fin in FINGERS if fingers else ():
            for i, part in enumerate(("Proximal", "Intermediate", "Distal")):
                n = f"{side}{fin}{part}"
                if n not in rig.data.bones: continue
                q = Quaternion((1, 0, 0), math.radians(GRIP[fin][i] if held else 0))
                for k in range(4): curve(f'pose.bones["{n}"].rotation_quaternion', k, [q[k]] * n_f)
    # the club, parked away from the hands when nothing is held
    qs, ls, ss = [], [], []
    for fr, rec in frames_data:
        M = rec["club"] if rec["club"] is not None else Matrix.Translation((0, 0, -30))
        loc, rot, scl = M.decompose(); qs.append(rot); ls.append(loc); ss.append(scl)
    for i in range(1, len(qs)):
        if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
    for k in range(4): curve('pose.bones["Club"].rotation_quaternion', k, [q[k] for q in qs])
    for k in range(3): curve('pose.bones["Club"].location', k, [l[k] for l in ls])
    for k in range(3): curve('pose.bones["Club"].scale', k, [sc[k] for sc in ss])
    return act


def arm_diag(clip, data):
    """How often the elbows and the wrists are inside the torso (an ellipse round the spine: his half-width and half-depth, an arm's thickness more)."""
    ax, ay = (0.19, 0.13) if not FEMALE else (0.165, 0.12)
    ax += 0.045; ay += 0.045
    hips0 = rest_head(rig, "Hips"); neck0 = rest_head(rig, "Neck")
    rows = []
    for fr, rec in data:
        pos, heads = rec["posed"], rec["heads"]
        d = pos["Chest"] @ rest_rot(rig, "Chest").inverted()
        lat, fwd, up = d @ Vector((1, 0, 0)), d @ Vector((0, -1, 0)), d @ Vector((0, 0, 1))
        base = heads["Hips"] + (heads["Chest"] - heads["Hips"]) * 0.0
        out = {}
        for who in ("LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand"):
            v = heads[who] - base
            t = v.dot(up)
            if not (0.05 < t < (neck0.z - hips0.z)): out[who] = None; continue
            x, y = v.dot(lat), v.dot(fwd)
            out[who] = math.sqrt((x / ax) ** 2 + (y / ay) ** 2)
        rows.append((fr, out))
    for who in ("LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand"):
        vals = [(fr, o[who]) for fr, o in rows if o[who] is not None]
        inside = [(fr, r) for fr, r in vals if r < 1.0]
        worst = min((r for fr, r in vals), default=9)
        print(f"ARMDIAG {SEX} {clip} {who}: inside the torso in {len(inside)} of {len(rows)} frames, deepest {worst:.2f} (1 = on the skin); first frames {[fr for fr, r in inside[:6]]}")


if "--grid" in ARGV:
    for clip in [c for c in CLIPS if c in HELD]:
        act = next(a for a in bpy.data.actions if a.name.endswith("|" + clip) and a.name.startswith("Male_Golf_Rig"))
        v4.animation_data_create(); v4.animation_data.action = act
        try: v4.animation_data.action_slot = act.slots[0]
        except Exception: pass
        scn.frame_set(int(act.frame_range[0]))
        sp = v4.pose.bones
        delta = {}
        for n in rig.data.bones.keys():
            src = BONE_MAP_INV.get(n)
            if src: delta[n] = sp[src].matrix.to_3x3().normalized() @ rest_rot(v4, src).inverted()
        ct = club_target(sp, clip)
        hands = ct[1]
        arm = sum(LEN["armL"])
        print(f"GRID {SEX} {clip}: hands at {tuple(round(x, 2) for x in hands['L'])} / {tuple(round(x, 2) for x in hands['R'])}, grip end {tuple(round(x, 2) for x in ct[0].to_translation())}, arm {arm:.2f}")
        for deg in (0, 10, 20, 30, 40):
            row = []
            for drop in (0.0, 0.05, 0.10):
                best = None
                for bx in [i * 0.01 for i in range(-60, 61)]:
                    OFFSET = Vector((bx, 0, -drop))
                    # the hinge as in trunk(): rebuild the shoulders' places
                    hinge = Matrix.Rotation(math.radians(deg), 3, delta["Hips"] @ Vector((1, 0, 0)))
                    # hips
                    hips = X(sp["Hips"].head)
                    d = delta["Hips"]
                    Rsp = hinge @ delta["Spine"] @ rest_rot(rig, "Spine")
                    # spine head: hips + delta_hips @ (rest spine - rest hips); chest etc follow Rsp-chain (all hinge-rotated absolute): approximate by rotating rest offsets with each bone's own delta
                    pos = hips; Rprev = delta["Hips"] @ rest_rot(rig, "Hips"); name_prev = "Hips"
                    for nme in ("Spine", "Chest", "UpperChest"):
                        off = rest_head(rig, nme) - rest_head(rig, name_prev)
                        pos = pos + Rprev @ rest_rot(rig, name_prev).inverted() @ off
                        Rprev = hinge @ (delta[nme] if nme in delta else delta["Chest"]) @ rest_rot(rig, nme); name_prev = nme
                    dist = []
                    for s_, side in SIDES:
                        sh_off = rest_head(rig, f"{side}Shoulder") - rest_head(rig, "UpperChest")
                        sh = pos + Rprev @ rest_rot(rig, "UpperChest").inverted() @ sh_off
                        Rsh = hinge @ delta[f"{side}Shoulder"] @ rest_rot(rig, f"{side}Shoulder")
                        ua = sh + Rsh @ rest_rot(rig, f"{side}Shoulder").inverted() @ (rest_head(rig, f"{side}UpperArm") - rest_head(rig, f"{side}Shoulder"))
                        dist.append((ua - hands[s_]).length)
                    err = abs(sum(dist) / 2 - ARM_EASE * arm)
                    if best is None or err < best[0]: best = (err, bx, sum(dist) / 2)
                row.append(f"drop {drop:.2f}: shift {best[1]:+.2f} (dist {best[2]:.2f}, err {best[0]:.3f})")
            print(f"GRID   hinge {deg:2d}: " + " | ".join(row))
    sys.exit(0)

STITCH_AT, STITCH_BLEND = 19, 6
RETARGETED = {}


def mix_rec(a, b, t):
    """A pose between two retargeted frames: each bone's rotation by slerp, its place by lerp; the club likewise."""
    basis = {}
    for n in a["basis"]:
        qa, qb = a["basis"][n][0].to_quaternion(), b["basis"][n][0].to_quaternion()
        basis[n] = (qa.slerp(qb, t).to_matrix(), a["basis"][n][1].lerp(b["basis"][n][1], t))
    club = None
    if a["club"] is not None and b["club"] is not None:
        la, ra, sa = a["club"].decompose(); lb, rb, sb = b["club"].decompose()
        club = Matrix.Translation(la.lerp(lb, t)) @ ra.slerp(rb, t).to_matrix().to_4x4() @ Matrix.Diagonal(Vector((*sa.lerp(sb, t), 1)))
    return dict(b, basis=basis, club=club)


def stitch(first, second, upto=STITCH_AT, blend=STITCH_BLEND):
    out = [rec for _f, rec in first[:upto]]
    hold = first[upto - 1][1]
    for k, (_f, rec) in enumerate(second):
        out.append(mix_rec(hold, rec, smooth((k + 1) / blend)) if k < blend else rec)
    return [(i + 1, rec) for i, rec in enumerate(out)]


results, actions = {}, {}
for clip in CLIPS:
    OFFSET = Vector((0, 0, 0))
    f0, f1, data = retarget_clip(clip)
    if clip in HELD and not REAL:
        # stand the golfer so the club head is at the ball as V4's was at address: shift the whole clip along the ground
        m = next(o for o in club_meshes if o.name == "CLUB_" + HELD[clip].upper())
        hl = min((v.co for v in m.data.vertices), key=lambda v: v.z).copy()
        mine = data[0][1]["club"] @ hl
        v4h = data[0][1]["v4club"] @ (hl / S)
        if "--dbg" in ARGV:
            print(f"DBG2 hl {tuple(round(x, 3) for x in hl)}, S {S:.3f}; his club matrix {[tuple(round(x, 3) for x in r) for r in data[0][1]['club']]}; v4 club matrix {[tuple(round(x, 3) for x in r) for r in data[0][1]['v4club']]}")
        OFFSET = Vector((v4h.x - mine.x, v4h.y - mine.y, 0))
        print(f"{clip}: ground shift {OFFSET.x * 100:+.1f}, {OFFSET.y * 100:+.1f} cm; head at address: V4 z {v4h.z:.3f}, his z {mine.z:.3f} (a similarity gives {S * v4h.z:.3f}); shaft at address {tuple(round(x, 2) for x in (data[0][1]['club'].to_3x3() @ Vector((0, 0, -1))))}")
        f0, f1, data = retarget_clip(clip)
    RETARGETED[clip] = (f0, f1, data)
    if clip == "HalfSwing" and "IronSwing" in RETARGETED and "--nostitch" not in ARGV:
        # V4's half swing starts with the club already in the air (head a yard up at frame 1), not at the ball. A golfer addresses the ball first: the iron's
        # address and takeaway, then (a short blend) the half swing from where its club is as high.
        data = stitch(RETARGETED["IronSwing"][2], data)
        f0, f1 = data[0][0], data[-1][0]
        V4_LANDMARKS["HalfSwing"] = dict(V4_LANDMARKS["HalfSwing"], frames=len(data), top=V4_LANDMARKS["HalfSwing"]["top"] + STITCH_AT, impact=V4_LANDMARKS["HalfSwing"]["impact"] + STITCH_AT)
        print(f"HalfSwing: stitched on the iron's first {STITCH_AT} frames: {len(data)} frames, top {V4_LANDMARKS['HalfSwing']['top']}, impact {V4_LANDMARKS['HalfSwing']['impact']}")
    if "--armdiag" in ARGV: arm_diag(clip, data)
    if "--elbows" in ARGV:
        rows = dict(data)
        def elbow(h, side):
            a, b, c = h[f"{side}UpperArm"], h[f"{side}LowerArm"], h[f"{side}Hand"]
            return math.degrees((a - b).angle(c - b))
        print(f"ELBOWS {SEX} {clip}: " + " | ".join(f"f{fr}: lead {elbow(rows[fr]['heads'], 'Left'):.0f} trail {elbow(rows[fr]['heads'], 'Right'):.0f}" for fr in range(f0, f1 + 1, 10)))
    if "--turn" in ARGV:
        def yaw(h, a, b):
            v = h[b] - h[a]; return math.degrees(math.atan2(v.y, v.x))
        rows = dict(data)
        y0s = yaw(rows[f0]["heads"], "RightUpperArm", "LeftUpperArm"); y0h = yaw(rows[f0]["heads"], "RightUpperLeg", "LeftUpperLeg")
        out = []
        for fr in range(f0, f1 + 1, 10):
            h = rows[fr]["heads"]
            ds = (yaw(h, "RightUpperArm", "LeftUpperArm") - y0s + 180) % 360 - 180; dh = (yaw(h, "RightUpperLeg", "LeftUpperLeg") - y0h + 180) % 360 - 180
            out.append(f"f{fr}: shoulders {ds:+.0f} hips {dh:+.0f}")
        print(f"TURN {SEX} {clip}: " + " | ".join(out))
    if "--stance" in ARGV:
        for fr in (data[0][0], [f for f, _ in data][len(data) // 2]):
            h = dict(data)[fr]["heads"]
            ang = lambda a, b: math.degrees(math.atan2(math.hypot(h[b].x - h[a].x, h[b].y - h[a].y), h[b].z - h[a].z))
            print(f"STANCE {SEX} {clip} f{fr}: spine tilt from vertical (hips->neck) {ang('Hips', 'Neck'):.0f}, hips->chest {ang('Hips', 'Chest'):.0f}, neck->head {ang('Neck', 'Head'):.0f}; hips z {h['Hips'].z:.2f}; knee bend: thigh {ang('LeftUpperLeg', 'LeftLowerLeg'):.0f} shin {ang('LeftLowerLeg', 'LeftFoot'):.0f}; head z {h['Head'].z:.2f}")
    act = write_action(clip, f0, f1, data, clip in HELD)
    actions[clip] = (act, f0, f1)
    if clip in HELD:
        m = next(o for o in club_meshes if o.name == "CLUB_" + HELD[clip].upper())
        head_local = min((v.co for v in m.data.vertices), key=lambda v: v.z).copy()
        heads = [rec["club"] @ head_local for _, rec in data]
        results[clip] = {"address": list(heads[0]), "path": [list(h) for h in heads]}

def borrowed_clip(name):
    """One of his clips on this rig: the pose of every bone at every frame, read off his armature (the bone names are the same)."""
    before_objs, before_acts = set(bpy.data.objects), set(bpy.data.actions)
    objs = load(os.path.join(SRC, f"{SEX}_{name}.fbx"))
    src = next(o for o in objs if o.type == 'ARMATURE')
    act = src.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    frames = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        basis = {}
        for pb in src.pose.bones:
            if pb.name not in rig.data.bones: continue
            loc, rot, _ = pb.matrix_basis.decompose()
            basis[pb.name] = (rot.to_matrix(), loc)
        frames.append((f, {"basis": basis, "club": None}))
    for o in list(bpy.data.objects):
        if o not in before_objs: remove(o)
    for a in list(bpy.data.actions):
        if a not in before_acts: bpy.data.actions.remove(a)
    return f0, f1, frames


for name in BORROWED:
    f0, f1, data = borrowed_clip(name)
    actions[name] = (write_action(name, f0, f1, data, False, fingers=False), f0, f1)
    CLIPS.append(name)
    print(f"{name}: {len(data)} frames (his)")

v4_json = json.load(open(os.path.splitext(V4)[0] + "_clips.json"))
clips_out = []
for c in v4_json["clips"]:
    if c["name"] not in CLIPS: continue
    entry = dict(c, **{k: v for k, v in V4_LANDMARKS.get(c["name"], {}).items() if k in ("frames", "top", "impact")})
    if c["name"] in results:
        r = results[c["name"]]
        # where the club head is back at the ball, after the top: V4 labels the half swing's impact eight frames before its club gets there (the ball would leave early)
        addr = Vector(r["path"][0])
        best = min(range(min(entry["top"], len(r["path"]) - 1), len(r["path"])), key=lambda i: (Vector(r["path"][i]) - addr).length)
        if abs(best - entry["impact"]) > 3:
            print(f"{c['name']}: the club is back at the ball at frame {best}, not {entry['impact']}: impact moved")
            entry["impact"] = best
        imp = min(entry["impact"], len(r["path"]) - 1)
        entry["headAddress"] = r["path"][0]
        entry["headImpact"] = r["path"][imp]
    clips_out.append(entry)
os.makedirs(OUT, exist_ok=True)
json.dump({"gender": SEX, "clips": clips_out}, open(os.path.join(OUT, f"golfer_{TAG}_clips.json"), "w"), indent=2)
print("clip landmarks written")

# ======================================================================= preview
if PREVIEW:
    import math as _m
    os.makedirs(PREVIEW, exist_ok=True)
    if not ASSEMBLE_ONLY: rig.data.pose_position = 'POSE'
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    scn.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in engines else 'BLENDER_EEVEE_NEXT'
    scn.render.resolution_x = 600; scn.render.resolution_y = 800
    SKIN = (0.93, 0.62, 0.45, 1)
    COLORS = {"Blockout_Grey": SKIN, "Base_Grey_F": SKIN, "Skin_F": SKIN, "Hair_M": (0.35, 0.22, 0.12, 1), "Hair_F": (0.35, 0.22, 0.12, 1), "Hair_Dark": (0.25, 0.15, 0.08, 1),
              "Kit_Shirt": (0.92, 0.92, 0.94, 1), "Kit_ShirtTrim": (0.04, 0.04, 0.05, 1), "Kit_Shorts": (0.92, 0.92, 0.94, 1),
              "Kit_ShortsBand": (0.04, 0.04, 0.05, 1), "Kit_Shoe": (0.95, 0.95, 0.95, 1), "Kit_Sole": (0.66, 0.65, 0.63, 1), "Kit_Sock": (0.95, 0.95, 0.95, 1),
              "Face_Brow": (0.2, 0.12, 0.08, 1), "Face_BrowSoft": (0.2, 0.12, 0.08, 1), "Face_Sclera": (1, 1, 1, 1), "Face_Iris": (0.25, 0.15, 0.08, 1),
              "Face_IrisIn": (0.18, 0.1, 0.05, 1), "Face_Pupil": (0.02, 0.02, 0.02, 1), "Face_Limbal": (0.08, 0.05, 0.03, 1), "Face_Catch": (1, 1, 1, 1),
              "Face_LidLine": (0.35, 0.18, 0.12, 1), "Face_LidLower": (0.7, 0.4, 0.3, 1), "Face_LidCrease": (0.7, 0.4, 0.3, 1), "Face_Lip": (0.75, 0.3, 0.3, 1),
              "Face_LipUp": (0.7, 0.28, 0.28, 1), "Face_Seam": (0.55, 0.3, 0.22, 1), "Face_Nostril": (0.45, 0.22, 0.16, 1)}
    for o in parts + club_meshes:
        for slot in o.material_slots:
            mt = slot.material
            if not mt: continue
            mt.use_nodes = True; nt = mt.node_tree; nt.nodes.clear()
            b = nt.nodes.new('ShaderNodeBsdfPrincipled'); out_ = nt.nodes.new('ShaderNodeOutputMaterial')
            nt.links.new(b.outputs['BSDF'], out_.inputs['Surface'])
            b.inputs['Base Color'].default_value = COLORS.get(mt.name.split(".")[0], (0.25, 0.26, 0.3, 1))
            b.inputs['Roughness'].default_value = 0.6
    for o in v4_objs:
        if o.type == 'MESH' and o not in club_meshes: o.hide_render = True; o.hide_viewport = True
    v4.hide_render = True
    w = bpy.data.worlds.new('w'); w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.72, 0.76, 0.82, 1); scn.world = w
    sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3.2
    so = bpy.data.objects.new('sun', sun); scn.collection.objects.link(so); so.rotation_euler = (_m.radians(55), 0, _m.radians(-25))

    def cam(name, loc, target, lens):
        c = bpy.data.cameras.new(name); c.lens = lens
        co = bpy.data.objects.new(name, c); scn.collection.objects.link(co); co.location = loc
        co.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler(); return co
    cams = {"a": cam("a", (3.4, -4.2, 1.5), (0.0, 0.0, 0.8), 55), "b": cam("b", (-3.6, 3.0, 1.4), (0.0, 0.0, 0.8), 55),
            "c": cam("c", (0.0, -4.5, 1.1), (0.0, 0.0, 0.85), 55), "d": cam("d", (1.1, -1.3, 1.25), (0.0, -0.3, 1.0), 60), "e": cam("e", (-1.1, -1.3, 1.25), (0.0, -0.3, 1.0), 60)}
    if "--hair-turn" in ARGV:
        # every hairstyle seen all the way round (every 45 degrees) at three heights, from above and from low behind: for the eyes that look for bald spots and overlaps
        rig.animation_data_clear()
        sk = matchhero_hair.LAST["skull"]
        c = bpy.data.objects.new("tcam", bpy.data.cameras.new("tcam")); scn.collection.objects.link(c); c.data.lens = 70
        scn.render.resolution_x = 360; scn.render.resolution_y = 360
        views = [(a, 8) for a in range(0, 360, 45)] + [(a, 40) for a in range(0, 360, 45)] + [(a, 72) for a in range(0, 360, 90)] + [(a, -14) for a in (135, 180, 225, 90)]
        for style, obj in STYLE_OBJS.items():
            for o in STYLE_OBJS.values(): o.hide_render = o is not obj
            for i, (az, el) in enumerate(views):
                d = sk.dir(az, el); loc = sk.C + d * 0.62
                c.location = loc; c.rotation_euler = (sk.C - loc).to_track_quat('-Z', 'Y').to_euler()
                scn.camera = c; scn.render.filepath = os.path.join(PREVIEW, f"turn_{TAG}_{style}_{i:02d}.png"); bpy.ops.render.render(write_still=True)
        sys.exit(0)
    if "--hair-preview" in ARGV:
        # every hairstyle on this head, from the front, the side, three-quarters above and behind
        rig.animation_data_clear()
        hz = rest_head(rig, "Head").z + 0.06
        hcams = {"h1": cam("h1", (0, -0.75, hz), (0, 0, hz), 70), "h2": cam("h2", (0.75, 0, hz), (0, 0, hz), 70),
                 "h3": cam("h3", (0.5, -0.5, hz + 0.35), (0, 0, hz), 70), "h4": cam("h4", (-0.5, 0.5, hz + 0.2), (0, 0, hz), 70)}
        scn.render.resolution_x = 500; scn.render.resolution_y = 500
        for style, obj in STYLE_OBJS.items():
            for o in STYLE_OBJS.values(): o.hide_render = o is not obj
            for k, c in hcams.items():
                scn.camera = c; scn.render.filepath = os.path.join(PREVIEW, f"hair_{TAG}_{style}_{k}.png"); bpy.ops.render.render(write_still=True)
        sys.exit(0)
    if "--icons" in ARGV:
        # the locker's picture tiles, transparent, one sex per run (blender/scripts/matchhero_icons.py sizes them): the head with and without
        # his hair, and the polo
        rig.animation_data_clear()
        scn.render.film_transparent = True
        scn.render.resolution_x = 512; scn.render.resolution_y = 512
        SKIN_ICON = (0.86, 0.50, 0.34, 1)
        for o in parts:
            for slot in o.material_slots:
                mt = slot.material
                if mt and mt.name.split(".")[0] in ("Blockout_Grey", "Base_Grey_F", "Skin_F"):
                    mt.node_tree.nodes[0].inputs['Base Color'].default_value = SKIN_ICON
        for o in club_meshes: o.hide_render = True
        hz = rest_head(rig, "Head").z + 0.075
        c = cam("icon", (0, -1.05, hz), (0, 0, hz), 100)
        c.rotation_euler = (math.radians(90), 0, 0)
        scn.camera = c
        for o in parts: o.hide_render = False
        for style, obj in STYLE_OBJS.items():
            for o in STYLE_OBJS.values(): o.hide_render = o is not obj
            scn.render.filepath = os.path.join(PREVIEW, f"head_{style.lower()}_{TAG}.png"); bpy.ops.render.render(write_still=True)
        for o in STYLE_OBJS.values(): o.hide_render = True
        scn.render.filepath = os.path.join(PREVIEW, f"head_bald_{TAG}.png"); bpy.ops.render.render(write_still=True)
        # the polo alone, front on
        for o in parts: o.hide_render = o.name != "Kit_Top"
        c2 = cam("icon2", (0, -2.6, 1.17), (0, 0, 1.17), 100)
        c2.rotation_euler = (math.radians(90), 0, 0)
        scn.camera = c2
        scn.render.filepath = os.path.join(PREVIEW, f"polo_{TAG}.png"); bpy.ops.render.render(write_still=True)
        sys.exit(0)
    if ASSEMBLE_ONLY:
        rig.animation_data_clear()
        hz = rest_head(rig, "Head").z + 0.06
        cams = {"h1": cam("h1", (0, -0.75, hz), (0, 0, hz), 70), "h2": cam("h2", (0.75, 0, hz), (0, 0, hz), 70),
                "h3": cam("h3", (0.5, -0.5, hz + 0.35), (0, 0, hz), 70), "h4": cam("h4", (-0.5, 0.5, hz + 0.2), (0, 0, hz), 70)}
        for k, c in cams.items():
            scn.camera = c; scn.render.filepath = os.path.join(PREVIEW, f"assembled_{TAG}_{k}.png"); bpy.ops.render.render(write_still=True)
        sys.exit(0)
    def diag(tag):
        """How far the body's skin comes through the kit (mm) in the current pose, and through which bones."""
        from mathutils.bvhtree import BVHTree
        dg = bpy.context.evaluated_depsgraph_get()
        import bmesh
        bm = bmesh.new()
        for o in kit:
            ev = o.evaluated_get(dg); me = ev.to_mesh(); bm.from_mesh(me); ev.to_mesh_clear()
        bm.normal_update(); bvh = BVHTree.FromBMesh(bm)
        ev = body.evaluated_get(dg); me = ev.to_mesh()
        sgn = 1.0
        worst = []
        for v in me.vertices:
            h = bvh.find_nearest(v.co, 0.05)
            if h[0] is None: continue
            d = (v.co - h[0]).dot(h[1]) * sgn
            worst.append((d, v.index, v.co.copy()))
        pos = sum(1 for d, *_ in worst if d > 0)
        if pos < len(worst) / 2: worst = [(-d, i, c) for d, i, c in worst]
        out = [w for w in worst if w[0] < 0.0]
        by = {}
        for d, i, c in out:
            g = max(body.data.vertices[i].groups, key=lambda x: x.weight).group
            n = body.vertex_groups[g].name
            by[n] = by.get(n, 0) + 1
        print(f"DIAG {tag}: {len(out)} of {len(worst)} body vertices outside the kit, worst {min((d for d, *_ in worst), default=0) * 1000:.1f} mm; by bone {dict(sorted(by.items(), key=lambda kv: -kv[1])[:8])}")
        ev.to_mesh_clear(); bm.free()

    for clip in CLIPS:
        act, f0, f1 = actions[clip]
        rig.animation_data.action = act
        try: rig.animation_data.action_slot = act.slots[0]
        except Exception: pass
        for o in club_meshes: o.hide_render = not (clip in HELD and o.name == "CLUB_" + HELD[clip].upper())
        if "--nobody" in ARGV: body.hide_render = True
        if "--nokit" in ARGV:
            for o in kit: o.hide_render = True
        for f in FRAMES or [f0]:
            if f < f0 or f > f1: continue
            scn.frame_set(f)
            if "--diag" in ARGV: diag(f"{clip} {f}")
            mid = (rig.pose.bones['LeftHand'].head + rig.pose.bones['RightHand'].head) / 2
            for key, off in (("d", Vector((0.55, -0.55, 0.2))), ("e", Vector((-0.55, -0.55, 0.2)))):
                c = cams[key]; c.location = mid + off
                c.rotation_euler = (mid - c.location).to_track_quat('-Z', 'Y').to_euler(); c.data.lens = 70
            if "--hips" in ARGV:
                hp = rig.pose.bones['Hips'].head
                for key, off in (("d", Vector((0.9, -0.9, 0.1))), ("e", Vector((-0.9, -0.9, 0.1)))):
                    c = cams[key]; c.location = hp + off
                    c.rotation_euler = (hp - c.location).to_track_quat('-Z', 'Y').to_euler(); c.data.lens = 60
            for k, c in cams.items():
                scn.camera = c
                scn.render.filepath = os.path.join(PREVIEW, f"{TAG}_{clip}_{k}_{f:03d}.png")
                bpy.ops.render.render(write_still=True)
    print("preview written")
    sys.exit(0)

# ======================================================================= export
for o in list(bpy.data.objects):
    if o.name in bpy.data.objects and o.type in ('MESH', 'ARMATURE', 'EMPTY') and o is not rig and o not in parts and o not in club_meshes: remove(o)
rig.animation_data.action = None
for tr in list(rig.animation_data.nla_tracks): rig.animation_data.nla_tracks.remove(tr)
track = rig.animation_data.nla_tracks.new()
at = 1
for clip in CLIPS:
    act, f0, f1 = actions[clip]
    strip = track.strips.new(clip, at, act)
    try: strip.action_slot = act.slots[0]
    except Exception: pass
    at = int(strip.frame_end) + 10
rig.data.pose_position = 'POSE'
scn.render.fps = 30; scn.render.fps_base = 1.0       # the clips are 30 fps (his FBX set the scene to 24, which Unity would play 25 % slow)
sel = [rig] + parts + club_meshes
for o in scn.objects: o.select_set(False)
for o in sel: o.select_set(True)
bpy.context.view_layer.objects.active = rig
with bpy.context.temp_override(selected_objects=sel, active_object=rig, object=rig):
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"golfer_{TAG}.fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             use_armature_deform_only=True, armature_nodetype='NULL',
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                             bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
print("exported", os.path.join(OUT, f"golfer_{TAG}.fbx"))
