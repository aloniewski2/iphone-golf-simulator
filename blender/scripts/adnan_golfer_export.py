"""Adnan's standard golfer, out of his sports animation studio and into the game.

    Blender -b <sports-animation-studio-v4.blend> --python blender/scripts/adnan_golfer_export.py -- --gender Male|Female

The studio file (SportsLibrary/Blender/sports-animation-studio-v4.blend on origin/all-design-mockups,
a Git LFS object) holds `Male_Golf_Rig` / `Female_Golf_Rig` in scene "01 GOLF": a 22-bone rig with
the V4 golf actions laid out as NLA strips (Drive, IronSwing, HalfSwing, Chip, Putt, SwingAbort;
RH and LH), the golf kit as separately skinned garments, and the clubs as props animated on their
own root empties along the same timeline (parked at z = -30 when not in use). This script:

  * keeps the right-handed swing strips the game uses and names them for Unity's takes;
  * adds a `Club` bone and bakes the in-use club's motion into it for every clip, so the club
    travels in the rig's own animation (an FBX take carries one rig, not a second animated prop);
  * builds one club mesh per club type (golf_clubs.py), skinned to that bone, so the game shows
    the club in hand;
  * turns the rig so the ball is on Unity +Z (GolferView stands the figure facing the ball),
    renames the skin material MAT_SKIN so skin tones still apply;
  * exports Unity/Assets/Resources/Golfer/golfer_<m|f>.fbx and writes golfer_<m|f>_clips.json
    with each clip's top-of-backswing and impact frames, found from the club head's path.
"""
import bpy, bmesh, json, math, os, sys
from mathutils import Matrix, Vector

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GENDER = ARGS[ARGS.index("--gender") + 1] if "--gender" in ARGS else "Male"
TAG = "m" if GENDER == "Male" else "f"
FBX = os.path.join(REPO, "Unity", "Assets", "Resources", "Golfer", f"golfer_{TAG}.fbx")
CLIPS = os.path.join(REPO, "Unity", "Assets", "Resources", "Golfer", f"golfer_{TAG}_clips.json")

# The clips the game plays, by the strip names in the studio's "V4 | wardrobe + expanded motions" track.
KEEP = {"Drive RH": "Drive", "IronSwing RH": "IronSwing", "HalfSwing RH": "HalfSwing", "Chip RH": "Chip", "Putt RH": "Putt"}
CLUBS = ("Driver", "Iron", "Wedge", "Putter")

sc = bpy.data.scenes["01 GOLF"]
bpy.context.window.scene = sc
vl = bpy.context.view_layer
O = bpy.data.objects
rig = O[f"{GENDER}_Golf_Rig"]
# Only the V4 track goes out; the archived tracks are dropped so their strip names ("Drive",
# "Putt"...) are free for the takes Unity will see.
for t in list(rig.animation_data.nla_tracks):
    if not t.name.startswith("V4"): rig.animation_data.nla_tracks.remove(t)
track = rig.animation_data.nla_tracks[0]
track.mute = False
roots = {c: O[f"{GENDER} V4 {c} Golf_{c}_ROOT"] for c in CLUBS}


def visible(o): return not o.hide_render and not o.hide_viewport and o.visible_get(view_layer=vl)


# ------------------------------------------------------------------ 1. the Club bone
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); vl.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
eb = rig.data.edit_bones.new("Club")
eb.head = (0, 0, 0); eb.tail = (0, 0.1, 0); eb.roll = 0   # rest = identity, so the club mesh's coordinates are the bone's
eb.use_deform = True
bpy.ops.object.mode_set(mode='OBJECT')
club_bone = rig.pose.bones["Club"]
club_bone.rotation_mode = 'QUATERNION'

# ------------------------------------------------------------------ 2. club meshes, in their root's space
# The clubs are our own (golf_clubs.py: a carbon driver, chrome irons, a satin wedge, a blade
# putter), modelled in the studio clubs' root space so the studio's club motion carries them.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import golf_clubs

def club_mesh(club):
    ob = golf_clubs.build(club)
    sc.collection.objects.link(ob)
    ob.parent = rig
    ob.vertex_groups.new(name="Club").add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
    ob.modifiers.new("Armature", 'ARMATURE').object = rig
    # the club head: the point farthest from the grip, tracked below to find top and impact
    tip = max((v.co for v in ob.data.vertices), key=lambda c: c.length)
    return ob, Vector(tip)

club_objects, club_tips = {}, {}
for c in CLUBS: club_objects[c], club_tips[c] = club_mesh(c)

# ------------------------------------------------------------------ 2b. hair, fitted to the head
# Adnan's heads are bald. Three styles are built as shells that hug the head mesh itself (its
# surface is found by ray-casting from the head's centre), weighted to the Head bone like the head,
# in one material the game colours: HAIR_SHORT, a crop; HAIR_LONG, a bob to the jaw; HAIR_CURLY,
# a puff of curls. They are fitted with the rig at rest, where the face is toward the rig's -Y (the
# golf clips turn the whole figure a quarter turn to face its ball on -X; the rest pose doesn't).
MAT_HAIR = bpy.data.materials.get("MAT_HAIR") or bpy.data.materials.new("MAT_HAIR")
if not MAT_HAIR.use_nodes: MAT_HAIR.use_nodes = True
MAT_HAIR.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.06, 0.03, 0.015, 1)
MAT_HAIR.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.65


def hair(style):
    head = next(c for c in rig.children if c.type == 'MESH' and "source head" in c.name and visible(c))
    pts = [v.co for v in head.data.vertices]
    centre = Vector(((min(p.x for p in pts) + max(p.x for p in pts)) / 2, (min(p.y for p in pts) + max(p.y for p in pts)) / 2,
                     (min(p.z for p in pts) + max(p.z for p in pts)) / 2))

    def surface(direction):
        hit, loc, normal, _ = head.ray_cast(centre + direction * 2.0, -direction)   # from outside, in
        return (loc, normal) if hit else (centre + direction * 0.22, direction)

    # where the hair stops, by azimuth (0 = front, pi = back), as a polar angle from the crown
    def edge(azimuth):
        front = (1 + math.cos(azimuth)) / 2         # 1 at the brow, 0 at the nape
        if style == "SHORT": return math.radians(58 * front + 108 * (1 - front))
        if style == "CURLY": return math.radians(60 * front + 112 * (1 - front))
        return math.radians(56 * front + 150 * (1 - front))   # LONG: down past the ears at the sides and back

    segs, rings = 40, 14
    bm = bmesh.new()
    grid = []
    for i in range(segs):
        azimuth = 2 * math.pi * i / segs
        stop = edge(azimuth)
        column = []
        for j in range(rings + 1):
            theta = stop * j / rings
            # direction from the crown: front (azimuth 0) is -Y, the face at rest
            d = Vector((math.sin(theta) * math.sin(azimuth), -math.sin(theta) * math.cos(azimuth), math.cos(theta)))
            loc, normal = surface(d)
            lift = 0.014
            if style == "CURLY": lift = 0.03 + 0.012 * (0.5 + 0.5 * math.sin(azimuth * 7) * math.sin(theta * 9 + azimuth))
            if style == "LONG" and theta > math.radians(95):
                # below the ears the bob falls straight rather than tucking under the head
                rim, rim_n = surface(Vector((math.sin(math.radians(95)) * math.sin(azimuth), -math.sin(math.radians(95)) * math.cos(azimuth), math.cos(math.radians(95)))))
                drop = (theta - math.radians(95)) / (stop - math.radians(95))
                loc = Vector((rim.x, rim.y, rim.z - drop * 0.16)) + Vector((rim_n.x, rim_n.y, 0)).normalized() * (0.004 * drop)
                normal = Vector((rim_n.x, rim_n.y, 0)).normalized()
            if j >= rings - 1 and style != "LONG": lift *= 0.55 if j == rings - 1 else 0.15   # a rounded edge
            column.append(bm.verts.new(loc + normal * lift))
        grid.append(column)
    crown = bm.verts.new(surface(Vector((0, 0, 1)))[0] + Vector((0, 0, 0.014 if style != "CURLY" else 0.036)))
    for i in range(segs):
        a, b = grid[i], grid[(i + 1) % segs]
        bm.faces.new((crown, a[1], b[1]))
        for j in range(1, rings):
            bm.faces.new((a[j], a[j + 1], b[j + 1], b[j]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces: f.smooth = True
    me = bpy.data.meshes.new(f"HAIR_{style}")
    bm.to_mesh(me); bm.free()
    me.materials.append(MAT_HAIR)
    ob = O.new(me.name, me)
    sc.collection.objects.link(ob)
    ob.parent = rig
    ob.vertex_groups.new(name="Head").add(list(range(len(me.vertices))), 1.0, 'REPLACE')
    ob.modifiers.new("Armature", 'ARMATURE').object = rig
    return ob

# ray_cast hits the evaluated (posed) head, and the hair is skinned on top of that pose: fit it
# with the rig at rest.
# With the Higgsfield golf body (fit_golf_characters.py) the character has its own hair and cap
# and the source head is gone: no shells.
higgs = next((c for c in rig.children if c.type == 'MESH' and c.name.startswith("V4 Higgs body")), None)
rig.data.pose_position = 'REST'; vl.update()
hair_objects = [] if higgs else [hair(style) for style in ("SHORT", "LONG", "CURLY")]
rig.data.pose_position = 'POSE'; vl.update()

# ------------------------------------------------------------------ 3. bake the in-use club into the bone, per clip
def in_use(frame):
    """The club that isn't parked this frame (the studio drops unused clubs to z = -30)."""
    sc.frame_set(frame)
    best = None
    for c in CLUBS:
        z = roots[c].matrix_world.translation.z
        if z > -5 and (best is None or z > roots[best].matrix_world.translation.z): best = c
    return best

landmarks = []
for strip in track.strips:
    if strip.name not in KEEP: strip.mute = True; continue
    clip = KEEP[strip.name]; strip.name = clip
    start, end = int(strip.frame_start), int(strip.frame_end)
    club = in_use((start + end) // 2)
    root, tip = roots[club], club_tips[club]
    matrices, heads = [], []
    for f in range(start, end + 1):
        sc.frame_set(f)
        matrices.append(rig.matrix_world.inverted() @ root.matrix_world)
        heads.append(root.matrix_world @ tip)
    # Top of the backswing: the club head farthest from where it addressed the ball (in the
    # first three quarters of the clip); impact: its closest return afterwards.
    away = [(h - heads[0]).length for h in heads]
    top = max(range(int(len(away) * 0.75)), key=lambda i: away[i])
    impact = min(range(top, int(len(away) * 0.97)), key=lambda i: away[i])
    landmarks.append({"name": clip, "club": club, "frames": end - start + 1, "fps": sc.render.fps, "top": top, "impact": impact})
    print(f"{GENDER} {clip}: {club}, {end - start + 1} frames, top at {top}, impact at {impact} ({away[top]:.2f} m back, {away[impact]:.3f} m off address)")

    rig.animation_data.action = strip.action
    curves = {}
    for path, n in (('pose.bones["Club"].location', 3), ('pose.bones["Club"].rotation_quaternion', 4), ('pose.bones["Club"].scale', 3)):
        curves[path] = [strip.action.fcurve_ensure_for_datablock(rig, path, index=i) for i in range(n)]
    for i, M in enumerate(matrices):
        club_bone.matrix = M
        frame = strip.action_frame_start + i
        for k, v in enumerate(club_bone.location): curves['pose.bones["Club"].location'][k].keyframe_points.insert(frame, v, options={'FAST'})
        for k, v in enumerate(club_bone.rotation_quaternion): curves['pose.bones["Club"].rotation_quaternion'][k].keyframe_points.insert(frame, v, options={'FAST'})
        for k, v in enumerate(club_bone.scale): curves['pose.bones["Club"].scale'][k].keyframe_points.insert(frame, v, options={'FAST'})
    for fcs in curves.values():
        for fc in fcs: fc.update()
    rig.animation_data.action = None

# ------------------------------------------------------------------ 3b. the moves off the course
# The intro and the gallery: the studio's shared Idle, Wave and Celebrate (made on the base rig,
# the same 22 bones; they face a quarter turn from the golf clips, which the game allows for — see
# GolferView.ClipYaw) and golf's own fist-pump Celebrate, all empty-handed. The Club bone is keyed
# at rest in them, so it doesn't hold the last swing's pose; the game hides the club for these.
EXTRA = {"Idle": "Shared__Idle", "Wave": "Shared__Wave", "Cheer": "Shared__Celebrate", "FistPump": "Golf__Celebrate"}
at = max(int(s.frame_end) for s in track.strips) + 10
for clip, source in EXTRA.items():
    act = bpy.data.actions[f"{GENDER}__{source}"].copy()
    act.name = f"{GENDER}__{clip}__game"
    slot = act.slots[0] if len(act.slots) else None
    rig.animation_data.action = act
    if slot: rig.animation_data.action_slot = slot
    club_bone.matrix_basis = Matrix.Identity(4)
    first, last = (int(v) for v in act.frame_range)
    for f in (first, last):
        for path, values in (('location', club_bone.location), ('rotation_quaternion', club_bone.rotation_quaternion), ('scale', club_bone.scale)):
            for k, v in enumerate(values):
                act.fcurve_ensure_for_datablock(rig, f'pose.bones["Club"].{path}', index=k).keyframe_points.insert(f, v, options={'FAST'})
    rig.animation_data.action = None
    strip = track.strips.new(clip, at, act)
    if slot: strip.action_slot = slot
    strip.mute = False
    landmarks.append({"name": clip, "club": "", "frames": last - first + 1, "fps": sc.render.fps, "top": 0, "impact": 0})
    print(f"{GENDER} {clip}: {source}, {last - first + 1} frames at {at}")
    at = int(strip.frame_end) + 10

# ------------------------------------------------------------------ 4. face the ball, name the skin, export
skin = bpy.data.materials.get("V4 skin" if GENDER == "Male" else "V4 skin female")
if higgs:
    # The body's colour map goes to Unity beside the FBX (GolferView puts it on "V4 Higgs body");
    # the grip hands take the skin colour of the face in that map instead of the player's tone.
    import numpy as np
    img = next(n.image for n in higgs.data.materials[0].node_tree.nodes
               if n.type == "TEX_IMAGE" and n.image and "normal" not in n.image.name.lower())
    out = img.copy(); out.scale(1024, 1024)
    out.filepath_raw = os.path.join(os.path.dirname(FBX), f"higgs_{TAG}_color.png"); out.file_format = "PNG"; out.save()
    px = np.empty(img.size[0] * img.size[1] * 4, dtype=np.float32); img.pixels.foreach_get(px)
    px = px.reshape(img.size[1], img.size[0], 4)
    head = higgs.vertex_groups["Head"].index
    me = higgs.data; uv = me.uv_layers.active.data
    face = [v for v in me.vertices if any(g.group == head and g.weight > .9 for g in v.groups)]
    zs = sorted(v.co.z for v in face); mid = zs[len(zs) // 3]
    front = min((v for v in face if abs(v.co.z - mid) < .03), key=lambda v: v.co.y)   # the face looks down -Y at rest
    loop = next(l for l in me.loops if l.vertex_index == front.index)
    u, v = uv[loop.index].uv
    tone = px[int(v * (img.size[1] - 1)), int(u * (img.size[0] - 1))][:3]
    if skin:
        skin.name = "V4 Higgs hands"
        bsdf = skin.node_tree.nodes.get("Principled BSDF") if skin.use_nodes else None
        if bsdf: bsdf.inputs["Base Color"].default_value = (*tone, 1)
        skin.diffuse_color = (*tone, 1)
    print(f"{GENDER}: Higgsfield body, colour map -> higgs_{TAG}_color.png, hands {tuple(round(float(c), 3) for c in tone)}")
elif skin: skin.name = "MAT_SKIN"
# The studio addresses a ball on the rig's -X; the game stands the golfer facing the ball on
# Unity +Z, which is Blender -Y: a quarter turn.
rig.matrix_world = Matrix.Rotation(math.radians(90), 4, 'Z')
sc.frame_set(1)

garments = [c for c in rig.children if c.type == 'MESH' and visible(c) and c not in hair_objects and c not in club_objects.values()]
exported = [rig] + garments + list(club_objects.values()) + hair_objects
for o in sc.objects: o.select_set(False)
for o in exported: o.select_set(True)
vl.objects.active = rig
os.makedirs(os.path.dirname(FBX), exist_ok=True)
with bpy.context.temp_override(selected_objects=exported, active_object=rig, object=rig):
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             use_armature_deform_only=True, armature_nodetype='NULL',
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                             bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
with open(CLIPS, "w") as f:
    json.dump({"gender": GENDER, "clips": landmarks}, f, indent=2)
print(f"exported {FBX} ({os.path.getsize(FBX) // 1024} KB): {len(garments)} garments, {len(club_objects)} clubs, {len(hair_objects)} hair styles, "
      f"{sum(len(g.data.vertices) for g in garments)} garment verts; landmarks in {os.path.basename(CLIPS)}")
