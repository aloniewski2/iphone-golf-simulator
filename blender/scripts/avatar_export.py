"""Rig the avatar kit to the Hero's skeleton and write the game's part FBXs.

    Blender -b --factory-startup --python blender/scripts/avatar_export.py -- [--out Unity/Assets/Resources/Hero]
        [--rig Unity/Assets/Resources/Hero/hero_golf.fbx] [--only body,head,shirt,shorts,shoes,hair,hats,glasses]

One FBX per slot, each carrying the shared 42-bone Hero_01_Rig (taken from hero_golf.fbx, the golf clips' own
skeleton, so every bone sits exactly where the clips expect), and the game rebinds each part to its one skeleton by bone
name (HeroGolfer.AddPart):

    hero_body.fbx     Hero_BodySkin                         neck, torso, arms, hands, legs
    hero_head.fbx     Hero_Head, Hero_Face                  the head block, the eyes / brows / nose / mouth / blush / ears
    hero_shirt.fbx    Top_<Name> (Polo, Tee, Hoodie, Vest)
    hero_shorts.fbx   Bottom_<Name> (Shorts, Trousers, Skirt)
    hero_shoes.fbx    Shoes_Golf
    hero_hair.fbx     Hair_<Cut> and Hair_<Cut>_Hat        every haircut twice: as it is, and with its crown pressed
                                                           down for when a hat is on (the game shows one of the two)
    hero_hats.fbx     Hat_<Name>
    hero_glasses.fbx  Glasses_<Name>, Face_<Name> (facial hair)

Body parts are skinned by envelope weights (each bone owns a tube round it; a vertex takes the bones whose tubes it is
in, nearest first; at most two bones, which is what the phone's quality level 2 keeps anyway) and the weights are
smoothed along the mesh; head parts are rigid to the Head bone.
"""
import bpy, bmesh, sys, os, re, math
from mathutils import Vector

SCRIPTS = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPTS)
import avatar_kit as K, avatar_base as B, avatar_parts as P, avatar_head_parts as H
import avatar_catalog as C

REPO = os.path.normpath(os.path.join(SCRIPTS, "..", ".."))
ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
arg = lambda k, d=None: ARGV[ARGV.index(k) + 1] if k in ARGV else d
OUT = arg("--out", os.path.join(REPO, "Unity", "Assets", "Resources", "Hero"))
RIG = arg("--rig", os.path.join(REPO, "Unity", "Assets", "Resources", "Hero", "hero_golf.fbx"))
ONLY = arg("--only"); ONLY = ONLY.split(",") if ONLY else None
os.makedirs(OUT, exist_ok=True)

# the tube round each bone that owns the vertices in it (metres); the head's is small here: only the top of the neck follows it
ENVELOPE = dict(Hips=0.26, Spine=0.22, Chest=0.23, Neck=0.09, Head=0.12, Shoulder=0.11, UpperArm=0.11, LowerArm=0.085, Hand=0.085,
                Index=0.032, Middle=0.032, Ring=0.032, Pinky=0.032, Thumb=0.035, UpperLeg=0.125, LowerLeg=0.10, Foot=0.095, Toes=0.085)
SMOOTH_PASSES = 3


def load_rig():
    bpy.ops.import_scene.fbx(filepath=RIG)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    for o in list(bpy.data.objects):
        if o is not arm: bpy.data.objects.remove(o, do_unlink=True)          # the clubs, anything else in the golf FBX
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    arm.animation_data_clear()
    arm.name = "Hero_01_Rig"; arm.data.name = "Hero_01_Rig"
    for b in arm.data.bones:
        if b.name in ("Club", "Root"): b.use_deform = False
    return arm


def bone_tubes(arm):
    mw = arm.matrix_world; out = {}
    for b in arm.data.bones:
        if not b.use_deform: continue
        base = re.sub(r"\d+$", "", b.name.split(".")[0])
        if base not in ENVELOPE: continue
        out[b.name] = (mw @ b.head_local, mw @ b.tail_local, ENVELOPE[base])
    return out


def seg_dist(p, a, b):
    ab = b - a; t = 0.0 if ab.length_squared < 1e-12 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def weights_of(p, tubes):
    ws = {}
    for name, (a, b, r) in tubes.items():
        t = seg_dist(p, a, b) / r
        if t < 1.0: ws[name] = (1.0 - t * t) ** 2
    if not ws:                                                  # outside every tube: the nearest one owns it
        name = min(tubes, key=lambda n: seg_dist(p, tubes[n][0], tubes[n][1]) / tubes[n][2]); ws[name] = 1.0
    return ws


def skin_envelope(obj, arm, tubes):
    me = obj.data; n = len(me.vertices)
    W = [weights_of(obj.matrix_world @ v.co, tubes) for v in me.vertices]
    nb = [[] for _ in range(n)]
    for e in me.edges:
        a, b = e.vertices; nb[a].append(b); nb[b].append(a)
    for _ in range(SMOOTH_PASSES):                              # smooth along the mesh so a joint bends, not snaps
        new = []
        for i in range(n):
            acc = {k: v * 0.5 for k, v in W[i].items()}
            if nb[i]:
                share = 0.5 / len(nb[i])
                for j in nb[i]:
                    for k, v in W[j].items(): acc[k] = acc.get(k, 0.0) + v * share
            new.append(acc)
        W = new
    bind(obj, arm, W)


def skin_rigid(obj, arm, bone="Head"):
    bind(obj, arm, [{bone: 1.0} for _ in obj.data.vertices])


def bind(obj, arm, W):
    for g in list(obj.vertex_groups): obj.vertex_groups.remove(g)
    groups = {}
    for i, w in enumerate(W):
        top = sorted(w.items(), key=lambda kv: -kv[1])[:2]               # two bones at most
        s = sum(v for _, v in top) or 1.0
        for name, v in top:
            if v / s < 0.02: continue
            g = groups.get(name) or groups.setdefault(name, obj.vertex_groups.new(name=name))
            g.add([i], v / s, 'REPLACE')
    obj.parent = arm
    for m in list(obj.modifiers): obj.modifiers.remove(m)
    mod = obj.modifiers.new("Armature", 'ARMATURE'); mod.object = arm


def export(path, arm, objs):
    for o in bpy.context.scene.objects: o.select_set(False)
    for o in [arm, *objs]: o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    with bpy.context.temp_override(selected_objects=[arm, *objs], active_object=arm, object=arm):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                                 use_mesh_modifiers=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                                 use_armature_deform_only=True, armature_nodetype='NULL', colors_type='LINEAR',
                                 bake_anim=False, path_mode='STRIP', embed_textures=False)
    print("wrote", os.path.relpath(path, REPO), [o.name for o in objs], sum(K.stats(o)[1] for o in objs), "tris")


def head_space_part(make, **kw):
    """A head part made against a fresh head, then put on the body (the head itself is thrown away)."""
    head = B.head_mesh()
    part = make(head, **kw)
    B.place_head([part])
    bpy.data.objects.remove(head, do_unlink=True)
    return part


def main():
    K.reset()
    arm = load_rig()
    tubes = bone_tubes(arm)
    want = lambda slot: ONLY is None or slot in ONLY
    # make the body parts first (in rig space), then the head's
    if want("body"):
        o = B.body_skin(); skin_envelope(o, arm, tubes); export(os.path.join(OUT, "hero_body.fbx"), arm, [o])
    if want("shirt"):
        tops = []
        for key, name, make in C.TOPS:
            o = make(); o.name = "Top_" + name; skin_envelope(o, arm, tubes); tops.append(o)
        export(os.path.join(OUT, "hero_shirt.fbx"), arm, tops)
    if want("shorts"):
        bottoms = []
        for key, name, make in C.BOTTOMS:
            o = make(); o.name = "Bottom_" + name; skin_envelope(o, arm, tubes); bottoms.append(o)
        export(os.path.join(OUT, "hero_shorts.fbx"), arm, bottoms)
    if want("shoes"):
        o = P.golf_shoes(); skin_envelope(o, arm, tubes); export(os.path.join(OUT, "hero_shoes.fbx"), arm, [o])
    if want("head"):
        head = B.head_mesh(); face = B.build_face(head, "smile"); B.place_head([head] + face)
        fobj = K.join("Hero_Face", face); head.name = "Hero_Head"
        for o in (head, fobj): skin_rigid(o, arm)
        export(os.path.join(OUT, "hero_head.fbx"), arm, [head, fobj])
    if want("hair"):
        hairs = []
        for key, name, make in C.HAIR:
            if not make: continue
            for hat in (False, True):
                o = head_space_part(make, under_hat=hat)
                o.name = "Hair_" + name + ("_Hat" if hat else "")
                skin_rigid(o, arm); hairs.append(o)
        export(os.path.join(OUT, "hero_hair.fbx"), arm, hairs)
    if want("hats"):
        hats = []
        for key, name, make in C.HATS:
            if not make: continue
            o = head_space_part(make); o.name = "Hat_" + name; skin_rigid(o, arm); hats.append(o)
        export(os.path.join(OUT, "hero_hats.fbx"), arm, hats)
    if want("glasses"):
        extras = []
        for key, name, make in C.GLASSES:
            if not make: continue
            o = head_space_part(make); o.name = "Glasses_" + name; skin_rigid(o, arm); extras.append(o)
        for key, name, make in C.FACIAL:
            if not make: continue
            o = head_space_part(make); o.name = "Face_" + name; skin_rigid(o, arm); extras.append(o)
        export(os.path.join(OUT, "hero_glasses.fbx"), arm, extras)

if __name__ == "__main__":
    main()
