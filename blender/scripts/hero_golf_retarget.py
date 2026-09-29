"""Adnan's Hero rig swings our golf clips.

The golf clips (Drive, IronSwing, HalfSwing, Chip, Putt, Idle, Wave, Cheer, FistPump) were authored
on the studio's V4 rig (Unity/Assets/Resources/Golfer/golfer_m.fbx, made by adnan_golfer_export.py).
The Hero (SportsLibrary-independent modular toy athlete, origin/tennisgameplaydone-needtofixcharacters)
has the same 22 bone names plus 20 finger bones, in nearly the same proportions, so the clips are
retargeted by bone rotation:

    world rotation change of each bone (pose vs rest) on the V4 rig  ->  the same change on the Hero rig

with the hips' travel scaled by the leg lengths. Rotation alone leaves the hands a few centimetres off
where V4's hands were, so the club is then fixed to the Hero's LEFT hand (the lead hand of a
right-hander, at the frame's V4 hand-to-club offset) and the RIGHT arm is solved by two-bone IK to the
place on the shaft V4's right hand held. A grip curl closes the fingers round the handle.

    Blender -b --factory-startup --python blender/scripts/hero_golf_retarget.py -- \
        --hero-dir <dir with Models/ (Hero01 FBXs from the tennisgameplaydone branch)> \
        [--v4 Unity/Assets/Resources/Golfer/golfer_m.fbx] [--out Unity/Assets/Resources/Hero] \
        [--preview <dir>] [--only Drive,Putt] [--frames 1,53,71,91]

Writes <out>/hero_golf.fbx (the 42-bone Hero rig + a Club bone, the four club meshes, one take per
clip) and <out>/hero_golf_clips.json (top and impact frames per clip, from the V4 file, and where the
clubhead is at address and impact so the game can stand the golfer at the ball).
"""
import bpy, sys, os, math, json
from mathutils import Vector, Matrix, Quaternion

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    return ARGV[ARGV.index(name) + 1] if name in ARGV else default


HERO_DIR = arg("--hero-dir", os.environ.get("HERO_DIR"))
V4 = arg("--v4", os.path.join(REPO, "Unity", "Assets", "Resources", "Golfer", "golfer_m.fbx"))
OUT = arg("--out", os.path.join(REPO, "Unity", "Assets", "Resources", "Hero"))
PREVIEW = arg("--preview")
ONLY = arg("--only")
FRAMES = [int(x) for x in arg("--frames", "").split(",") if x]
ATLAS = arg("--atlas")

RH_HANDS = ("Hand.L", "Hand.R")   # (lead/top hand, trail/bottom hand) of a right-hander
CLIPS = ["Drive", "IronSwing", "HalfSwing", "Chip", "Putt", "Idle", "Wave", "Cheer", "FistPump"]
HELD = {"Drive": "Driver", "IronSwing": "Iron", "HalfSwing": "Iron", "Chip": "Wedge", "Putt": "Putter"}
if ONLY: CLIPS = [c for c in CLIPS if c in ONLY.split(",")]

# Fingers closed round the handle: degrees of curl about the hand's own across-the-knuckles axis, per
# finger bone (cumulative down the chain), a right-hander's top hand (L) and bottom hand (R).
GRIP = {"Index1": 58, "Index2": 62, "Middle1": 66, "Middle2": 66, "Ring1": 70, "Ring2": 66, "Pinky1": 72, "Pinky2": 60, "Thumb1": 20, "Thumb2": 28}
FINGER_PARENT = {f"{k}{i}": (f"{k}{i-1}" if i == 2 else "Hand") for k in ("Index", "Middle", "Ring", "Pinky", "Thumb") for i in (1, 2)}

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene


def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


v4_objs = load(V4)
v4 = next(o for o in v4_objs if o.type == 'ARMATURE')
hero_objs = load(os.path.join(HERO_DIR, "Models", "RestoreMotion", "Hero_01_FingerBody.fbx"))
hero = next(o for o in hero_objs if o.type == 'ARMATURE')
hero.name = "Hero_01_Rig"; hero.data.name = "Hero_01_Rig"
hero_body = next(o for o in hero_objs if o.type == 'MESH')
v4.data.pose_position = 'POSE'

# ---- shared bones, in parent-first order
names = [b.name for b in v4.data.bones if b.name in hero.data.bones and b.name != "Club"]
order = []
def visit(bone):
    if bone.name in names: order.append(bone.name)
    for c in bone.children: visit(c)
for b in hero.data.bones:
    if b.parent is None: visit(b)
parent = {n: (hero.data.bones[n].parent.name if hero.data.bones[n].parent else None) for n in order}


def rest_rot(arm, n): return arm.data.bones[n].matrix_local.to_3x3()
def rest_head(arm, n): return arm.data.bones[n].head_local.copy()


LEG = rest_head(hero, 'Hips').z / rest_head(v4, 'Hips').z


def two_bone(S, E, T, l1, l2):
    """The elbow for a two-bone chain from S to reach T with lengths l1, l2, on E's side of the S->T line."""
    d = (T - S).length
    d = min(max(d, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    u = (T - S).normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    h = math.sqrt(max(l1 * l1 - a * a, 0))
    v = (E - S) - u * (E - S).dot(u)
    if v.length < 1e-6: v = Vector((0, -1, 0)) - u * Vector((0, -1, 0)).dot(u)
    v.normalize()
    return S + u * a + v * h, S + u * d


def arc(frm, to):
    return frm.rotation_difference(to).to_matrix()


def grip_fingers(posed, basis):
    """Close both hands' fingers round the handle (in the hand's rest frame, so it follows the wrist)."""
    for side in ("L", "R"):
        hand = f"Hand.{side}"
        delta = posed[hand] @ rest_rot(hero, hand).inverted()
        sign = 1 if side == "L" else -1
        cum = {}
        for f in ("Index1", "Index2", "Middle1", "Middle2", "Ring1", "Ring2", "Pinky1", "Pinky2", "Thumb1", "Thumb2"):
            b = f"{f}.{side}"
            if b not in hero.data.bones: continue
            par = FINGER_PARENT[f]
            pn = hand if par == "Hand" else f"{par}.{side}"
            ang = GRIP[f] + (cum.get(pn, 0) if par != "Hand" else 0)
            cum[b] = ang
            W = Matrix.Rotation(math.radians(sign * ang), 3, 'Y')
            desired = delta @ W @ rest_rot(hero, b)
            base = posed[pn] @ rest_rot(hero, pn).inverted() @ rest_rot(hero, b)
            basis[b] = (base.inverted() @ desired, None)
            posed[b] = desired


def solve_arm(posed, heads, basis, side, T):
    """Two-bone IK: put the wrist of arm `side` at T (armature space), keeping the elbow on the side it bent."""
    ua, la, hd = f"UpperArm.{side}", f"LowerArm.{side}", f"Hand.{side}"
    S, E = heads[ua], heads[la]
    l1 = (heads[la] - heads[ua]).length; l2 = (heads[hd] - heads[la]).length
    newE, newW = two_bone(S, E, T, l1, l2)
    def bone_dir(R): return (R @ Vector((0, 1, 0))).normalized()
    R_ua = arc(bone_dir(posed[ua]), (newE - S).normalized()) @ posed[ua]
    R_la = arc(bone_dir(posed[la]), (newW - newE).normalized()) @ posed[la]
    for n, R in ((ua, R_ua), (la, R_la), (hd, posed[hd])):
        p = parent[n]
        parent_R = R_ua if p == ua else (R_la if p == la else posed[p])
        base = parent_R @ rest_rot(hero, p).inverted() @ rest_rot(hero, n)
        basis[n] = (base.inverted() @ R, None)
        posed[n] = R
    heads[la] = newE; heads[hd] = newW


def retarget_clip(clip):
    act = next(a for a in bpy.data.actions if a.name.endswith("|" + clip))
    v4.animation_data_create(); v4.animation_data.action = act
    try: v4.animation_data.action_slot = act.slots[0]
    except Exception: pass
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    club_held = clip in HELD
    rows = {}    # bone -> list of (quat, loc?) per frame
    club_rows = []
    info = {"head_address": None, "head_impact": None}
    frames_data = []
    for f in range(f0, f1 + 1):
        scn.frame_set(f)
        sp = v4.pose.bones
        posed, heads, basis = {}, {}, {}
        for n in order:
            sb = sp[n]
            desired = sb.matrix.to_3x3() @ rest_rot(v4, n).inverted() @ rest_rot(hero, n)
            p = parent[n]
            if p is None:
                base = rest_rot(hero, n)
                exp_head = rest_head(hero, n)
                disp_src = sb.head - rest_head(v4, n)
                head = exp_head + disp_src * LEG
                loc = base.inverted() @ (head - exp_head)
            else:
                base = posed[p] @ rest_rot(hero, p).inverted() @ rest_rot(hero, n)
                exp_head = heads[p] + posed[p] @ rest_rot(hero, p).inverted() @ (rest_head(hero, n) - rest_head(hero, p))
                if n == 'Hips':
                    # the hips' own travel on top of the root's, in the V4's proportions
                    disp = (sb.head - rest_head(v4, n)) * LEG
                    want = Vector((disp.x, disp.y, rest_head(hero, n).z + disp.z))
                    head = want
                    loc = base.inverted() @ (head - exp_head)
                else:
                    head = exp_head; loc = None
            basis[n] = (base.inverted() @ desired, loc)
            posed[n] = desired; heads[n] = head
        record = {"posed": posed, "heads": heads, "basis": basis, "v4club": sp["Club"].matrix.copy()}
        if club_held:
            grip_fingers(posed, basis)
        if club_held:
            # The club is exactly where the V4 clip had it (so the game's ball contact is unchanged);
            # both arms reach the places V4's hands held it.
            for hand in RH_HANDS:
                solve_arm(posed, heads, basis, hand[-1], sp[hand].head.copy())
            record["club"] = sp["Club"].matrix.copy()
        frames_data.append((f, record))
    return f0, f1, frames_data


def write_action(name, f0, f1, frames_data, hero_arm, club_held):
    """A Hero action from per-frame basis values, written as F-curves in bulk."""
    for n in order: hero_arm.pose.bones[n].rotation_mode = 'QUATERNION'
    club_pb = hero_arm.pose.bones["Club"]; club_pb.rotation_mode = 'QUATERNION'
    hero_arm.animation_data_create()
    act = bpy.data.actions.new(name)
    hero_arm.animation_data.action = act
    slot = act.slots.new('OBJECT', hero_arm.name) if not len(act.slots) else act.slots[0]
    hero_arm.animation_data.action_slot = slot
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

    for n in [b for b in hero_arm.data.bones.keys() if b in frames_data[0][1]["basis"]]:
        hero_arm.pose.bones[n].rotation_mode = 'QUATERNION'
        qs = [rec["basis"][n][0].to_quaternion() for _, rec in frames_data]
        # keep the quaternions continuous (no sign flips between frames)
        for i in range(1, len(qs)):
            if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
        for k in range(4): curve(f'pose.bones["{n}"].rotation_quaternion', k, [q[k] for q in qs])
        if n in ('Root', 'Hips'):
            for k in range(3): curve(f'pose.bones["{n}"].location', k, [rec["basis"][n][1][k] if rec["basis"][n][1] is not None else 0 for _, rec in frames_data])
    # the club: parked away from the hands when nothing is held
    qs, ls = [], []
    for fr, rec in frames_data:
        M = rec.get("club")
        if M is None: M = Matrix.Translation((0, 0, -30))
        loc, rot, _ = M.decompose(); qs.append(rot); ls.append(loc)
    for i in range(1, len(qs)):
        if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
    for k in range(4): curve('pose.bones["Club"].rotation_quaternion', k, [q[k] for q in qs])
    for k in range(3): curve('pose.bones["Club"].location', k, [l[k] for l in ls])
    return act


# ---- the Club bone on the Hero, as on the V4 rig (rest = identity, so the club mesh's coordinates are the bone's)
bpy.ops.object.select_all(action='DESELECT')
hero.select_set(True); bpy.context.view_layer.objects.active = hero
bpy.ops.object.mode_set(mode='EDIT')
eb = hero.data.edit_bones.new("Club"); eb.head = (0, 0, 0); eb.tail = (0, 0.1, 0); eb.roll = 0; eb.use_deform = True
bpy.ops.object.mode_set(mode='OBJECT')
# club meshes come across from the V4 file, skinned to the (new) Club bone
club_meshes = [o for o in v4_objs if o.type == 'MESH' and o.name.startswith("CLUB_")]
for o in club_meshes:
    o.parent = hero
    for m in o.modifiers:
        if m.type == 'ARMATURE': m.object = hero
    o.matrix_parent_inverse = Matrix.Identity(4)
    hero.pose.bones["Club"]   # (exists now)

results = {}
actions = {}
for clip in CLIPS:
    f0, f1, data = retarget_clip(clip)
    frames_n = f1 - f0 + 1
    print(f"{clip}: {frames_n} frames")
    act = write_action(clip, f0, f1, data, hero, clip in HELD)
    actions[clip] = (act, f0, f1)
    if clip in HELD:
        # where the clubhead is (the club mesh's lowest point, in the club bone's space)
        m = next(o for o in club_meshes if o.name == "CLUB_" + HELD[clip].upper())
        head_local = min((v.co for v in m.data.vertices), key=lambda v: v.z).copy()
        heads = [rec["club"] @ head_local for _, rec in data]
        v4heads = [rec["v4club"] @ head_local for _, rec in data]
        results[clip] = {"address": list(heads[0]), "path": [list(h) for h in heads], "v4path": [list(h) for h in v4heads]}

# ---- landmarks: copy from the V4 clips (timing is preserved), add the clubhead's place
v4_json = json.load(open(os.path.splitext(V4)[0] + "_clips.json"))
clips_out = []
for c in v4_json["clips"]:
    if c["name"] not in CLIPS: continue
    entry = dict(c)
    if c["name"] in results:
        r = results[c["name"]]
        imp = min(c["impact"], len(r["path"]) - 1)   # (frame numbers in the JSON are 0-based)
        entry["headAddress"] = r["path"][0]
        entry["headImpact"] = r["path"][imp]
        entry["v4HeadAddress"] = r["v4path"][0]
        entry["v4HeadImpact"] = r["v4path"][imp]
    clips_out.append(entry)

os.makedirs(OUT, exist_ok=True)
json.dump({"gender": "Hero", "clips": clips_out}, open(os.path.join(OUT, "hero_golf_clips.json"), "w"), indent=2)
print("clip landmarks written")

if PREVIEW:
    # the full Hero (body, clothes, hair, visor) on the retargeted rig, at the requested frames
    import math as _m
    os.makedirs(PREVIEW, exist_ok=True)
    PARTS_DIR = arg("--parts-dir", OUT)
    def part(fbx, names_):
        objs = load(os.path.join(PARTS_DIR, fbx))
        keep = []
        for o in objs:
            if o.type == 'MESH' and o.name in names_:
                for m in o.modifiers:
                    if m.type == 'ARMATURE': m.object = hero
                o.parent = hero; keep.append(o)
        for o in objs:
            if o not in keep and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
        return keep
    hero_body.parent = None; bpy.data.objects.remove(hero_body, do_unlink=True)
    parts = part("hero_body.fbx", ['Body_Skin'])
    hero_body = parts[0]
    parts += part("hero_eyes.fbx", ['Body_EyeSphere_L', 'Body_EyeSphere_R'])
    parts += part("hero_shirt.fbx", ['Shirt_Default'])
    parts += part("hero_shorts.fbx", ['Shorts_Default'])
    parts += part("hero_shoes.fbx", ['Shoes_Default'])
    parts += part("hero_hair.fbx", [arg("--hair", 'Hair_Default_Free')])
    parts += part("hero_visor.fbx", ['Hat_Visor'])
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    scn.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in engines else 'BLENDER_EEVEE_NEXT'
    scn.render.resolution_x = 600; scn.render.resolution_y = 800
    atlas = bpy.data.images.load(ATLAS)
    for o in parts + club_meshes:
        for slot in o.material_slots:
            mt = slot.material
            if not mt: continue
            mt.use_nodes = True; nt = mt.node_tree; nt.nodes.clear()
            b = nt.nodes.new('ShaderNodeBsdfPrincipled'); out_ = nt.nodes.new('ShaderNodeOutputMaterial')
            nt.links.new(b.outputs['BSDF'], out_.inputs['Surface'])
            nm = mt.name
            if nm.startswith('Hero_01_HairTuft'): b.inputs['Base Color'].default_value = (0.87, 0.70, 0.46, 1)
            elif nm.startswith('Hero_01_ShoeCleanWhite'): b.inputs['Base Color'].default_value = (0.9, 0.89, 0.87, 1)
            elif nm.startswith('Hero_01_ShoeOrange'): b.inputs['Base Color'].default_value = (0.9, 0.45, 0.1, 1)
            elif nm.startswith('Hero_01_EyeCornea') or nm.startswith('Hero_01_Catchlight'): b.inputs['Base Color'].default_value = (1, 1, 1, 1)
            elif nm.startswith('CLUB'): b.inputs['Base Color'].default_value = (0.25, 0.26, 0.3, 1)
            else:
                t = nt.nodes.new('ShaderNodeTexImage'); t.image = atlas
                nt.links.new(t.outputs['Color'], b.inputs['Base Color'])
    for o in v4_objs:
        if o.type == 'MESH' and o not in club_meshes: o.hide_render = True; o.hide_viewport = True
    v4.hide_render = True
    for o in club_meshes:
        o.hide_render = True
    w = bpy.data.worlds.new('w'); w.use_nodes = True
    w.node_tree.nodes['Background'].inputs[0].default_value = (0.72, 0.76, 0.82, 1); scn.world = w
    sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3.2
    so = bpy.data.objects.new('sun', sun); scn.collection.objects.link(so); so.rotation_euler = (_m.radians(55), 0, _m.radians(-25))
    def cam(name, loc, target, lens):
        c = bpy.data.cameras.new(name); c.lens = lens
        co = bpy.data.objects.new(name, c); scn.collection.objects.link(co); co.location = loc
        d = Vector(target) - Vector(loc); co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler(); return co
    cams = {"a": cam("a", (3.4, -4.2, 1.5), (0.0, 0.0, 0.8), 55), "b": cam("b", (-3.6, 3.0, 1.4), (0.0, 0.0, 0.8), 55), "c": cam("c", (0.0, -4.5, 1.1), (0.0, 0.0, 0.85), 55), "d": cam("d", (1.1, -1.3, 1.25), (0.0, -0.3, 1.0), 60), "e": cam("e", (-1.1, -1.3, 1.25), (0.0, -0.3, 1.0), 60)}
    for clip in CLIPS:
        act, f0, f1 = actions[clip]
        hero.animation_data.action = act
        try: hero.animation_data.action_slot = act.slots[0]
        except Exception: pass
        held = clip in HELD
        for o in club_meshes:
            o.hide_render = not (held and o.name == "CLUB_" + HELD[clip].upper())
        for f in FRAMES or [f0]:
            if f < f0 or f > f1: continue
            scn.frame_set(f)
            mid = (hero.pose.bones['Hand.L'].head + hero.pose.bones['Hand.R'].head) / 2
            for key, off in (("d", Vector((0.55, -0.55, 0.2))), ("e", Vector((-0.55, -0.55, 0.2)))):
                c = cams[key]; c.location = mid + off
                c.rotation_euler = (mid - c.location).to_track_quat('-Z', 'Y').to_euler()
                c.data.lens = 70
            for k, c in cams.items():
                scn.camera = c
                scn.render.filepath = os.path.join(PREVIEW, f"{clip}_{k}_{f:03d}.png")
                bpy.ops.render.render(write_still=True)
    print("preview written")
    sys.exit(0)

# ---- export the rig, the club meshes and the takes (as NLA strips, like adnan_golfer_export.py)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o not in club_meshes and o.name in bpy.data.objects: bpy.data.objects.remove(o, do_unlink=True)
for o in list(bpy.data.objects):
    if o.name in bpy.data.objects and o not in club_meshes and o is not hero and o.type in ('ARMATURE', 'EMPTY'):
        if o.type == 'ARMATURE' or o.name in ("Back", "FaceExtra", "Hat", "Hand_L", "Hand_R"): bpy.data.objects.remove(o, do_unlink=True)
hero.animation_data.action = None
for tr in list(hero.animation_data.nla_tracks): hero.animation_data.nla_tracks.remove(tr)
track = hero.animation_data.nla_tracks.new()
at = 1
for clip in CLIPS:
    act, f0, f1 = actions[clip]
    strip = track.strips.new(clip, at, act)
    try: strip.action_slot = act.slots[0]
    except Exception: pass
    at = int(strip.frame_end) + 10
sel = [hero] + club_meshes
for o in scn.objects: o.select_set(False)
for o in sel: o.select_set(True)
bpy.context.view_layer.objects.active = hero
with bpy.context.temp_override(selected_objects=sel, active_object=hero, object=hero):
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "hero_golf.fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                             use_mesh_modifiers=True, mesh_smooth_type='OFF', add_leaf_bones=False,
                             use_armature_deform_only=True, armature_nodetype='NULL',
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                             bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                             bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP', embed_textures=False)
print("exported", os.path.join(OUT, "hero_golf.fbx"))
