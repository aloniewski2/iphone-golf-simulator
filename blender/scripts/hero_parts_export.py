"""The Hero's parts, out of Adnan's FBXs and into the game, mended on the way.

Adnan's modular Hero (origin/tennisgameplaydone-needtofixcharacters, Unity/Assets/ArtDirection/Hero01/Models)
is one skeleton and a set of skinned parts: body, hair cuts, headwear, shirt, shorts, shoes. This reads
those and writes Unity/Assets/Resources/Hero/hero_<part>.fbx, one file per slot, each with the shared
42-bone Hero_01_Rig so the game can rebind every part to one skeleton by bone name.

    Blender -b --factory-startup --python blender/scripts/hero_parts_export.py -- \
        --hero-dir <Unity/Assets/ArtDirection/Hero01 from his branch> [--out Unity/Assets/Resources/Hero]

The repairs (see hero_repair.py, called from here) are made on the meshes before they are written; the
sources are never touched.
"""
import bpy, sys, os, math
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    return ARGV[ARGV.index(name) + 1] if name in ARGV else default


HERO_DIR = arg("--hero-dir", os.environ.get("HERO_DIR"))
OUT = arg("--out", os.path.join(REPO, "Unity", "Assets", "Resources", "Hero"))
os.makedirs(OUT, exist_ok=True)

# slot -> (source FBX under Models/, the mesh objects to keep, output name)
PARTS = {
    "body": ("RestoreMotion/Hero_01_FingerBody.fbx", ["Body_Skin"]),
    "eyes": ("Hero_01_Mixamo_Bind.fbx", ["Body_EyeSphere_L", "Body_EyeSphere_R"]),
    "shirt": ("Hero_01_Shirt_Default.fbx", ["Shirt_Default"]),
    "shorts": ("Hero_01_Shorts_Default.fbx", ["Shorts_Default"]),
    "shoes": ("Hero_01_Shoes_Default.fbx", ["Shoes_Default"]),
}
# The head is made in one scene: the closed head (hero_head.py; from his face plate, his bald skull and his buzz
# cut's UVs), the haircuts made to sit on it (hero_hair.py: hero_hair.fbx with the scalp) and the hats made to sit
# over each haircut (hero_hats.py: hero_visor/cap/sweatband.fbx). None of his hair or hat sculpts is used.
HAT_FILES = {"Visor": "visor", "Cap": "cap", "Sweatband": "sweatband"}
ONLY = arg("--only")
if ONLY: PARTS = {k: v for k, v in PARTS.items() if k in ONLY.split(",")}
DO_HEAD = not ONLY or "head" in ONLY.split(",")


def export(path, objs):
    for o in bpy.context.scene.objects: o.select_set(False)
    for o in objs: o.select_set(True)
    arm = next(o for o in objs if o.type == 'ARMATURE')
    bpy.context.view_layer.objects.active = arm
    with bpy.context.temp_override(selected_objects=objs, active_object=arm, object=arm):
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
                                 apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                                 axis_forward='-Z', axis_up='Y', bake_space_transform=False,
                                 use_mesh_modifiers=False, mesh_smooth_type='OFF', add_leaf_bones=False,
                                 use_armature_deform_only=True, armature_nodetype='NULL', colors_type='LINEAR',
                                 bake_anim=False, path_mode='STRIP', embed_textures=False)


def repair(slot, meshes):
    """Hook for the repairs to a slot's meshes (hero_repair.py)."""
    try:
        import hero_repair
    except ImportError:
        return
    hero_repair.apply(slot, meshes, HERO_DIR)


def import_meshes(fbx, keep):
    """Import fbx and return {name: mesh object} for the names in keep (the rest, and its armature, removed)."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(HERO_DIR, "Models", fbx))
    got = {}
    for o in [o for o in bpy.data.objects if o not in before]:
        if o.type == 'MESH' and o.name in keep: got[o.name] = o
    for o in [o for o in bpy.data.objects if o not in before]:
        if o.name not in got and o.type != 'ARMATURE': bpy.data.objects.remove(o, do_unlink=True)
    return got


def export_head():
    import hero_head, hero_hair, hero_hats
    bpy.ops.wm.read_factory_settings(use_empty=True)
    face = import_meshes("RestoreMotion/Hero_01_FingerBody.fbx", {"Body_Skin"})["Body_Skin"]
    arm = face.parent
    eyes = list(import_meshes("Hero_01_Mixamo_Bind.fbx", {"Body_EyeSphere_L", "Body_EyeSphere_R"}).values())
    skulls = import_meshes("Hero_01_Hair_Default.fbx", {"Hair_Buzz"})   # (only its UVs are used: the short-hair maps)
    shirt = import_meshes("Hero_01_Shirt_Default.fbx", {"Shirt_Default"})["Shirt_Default"]   # what long hair must clear
    # one skeleton for everything made here (every FBX carries the same rig)
    for o in list(bpy.data.objects):
        if o.type == 'ARMATURE' and o is not arm: bpy.data.objects.remove(o, do_unlink=True)
    arm.name = "Hero_01_Rig"; arm.data.name = "Hero_01_Rig"
    for o in [*eyes, *skulls.values(), shirt]:
        m = o.matrix_world.copy(); o.parent = arm; o.matrix_world = m
        for mod in o.modifiers:
            if mod.type == 'ARMATURE': mod.object = arm
    scalp, report = hero_head.build_head(arm, face, skulls["Hair_Buzz"])
    for k, v in report.items(): print("head:", k, v)
    head = hero_hair.Head(scalp, face, eyes, shirt)
    cuts = {}
    for name, cut in hero_hair.make_cuts().items():
        obj = hero_hair.to_object(hero_hair.build_cut(head, cut, seed=3), name, arm)
        hero_hair.bind(obj, arm)
        cuts[name] = obj
        print(f"hair: {name} {len(obj.data.vertices)} vertices, {sum(len(p.vertices) - 2 for p in obj.data.polygons)} triangles")
    for obj in cuts.values(): hero_hats.press_hair(obj, head)
    hats = {h: hero_hats.bake_hat(h, head, arm, cuts) for h in hero_hats.HATS}
    export(os.path.join(OUT, "hero_hair.fbx"), [arm, scalp, *cuts.values()])
    for h, slot in HAT_FILES.items():
        export(os.path.join(OUT, f"hero_{slot}.fbx"), [arm, hats[h]])
    print("hero_hair.fbx:", [scalp.name, *cuts], "hats:", list(hats))


for slot, (fbx, keep) in PARTS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=os.path.join(HERO_DIR, "Models", fbx))
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    arm.name = "Hero_01_Rig"; arm.data.name = "Hero_01_Rig"
    meshes = []
    for o in list(bpy.data.objects):
        if o.type == 'MESH':
            if keep is None or o.name in keep: meshes.append(o)
            else: bpy.data.objects.remove(o, do_unlink=True)
        elif o.type == 'EMPTY':
            bpy.data.objects.remove(o, do_unlink=True)
    repair(slot, meshes)
    if slot == "body":
        # his mask off the body (the head is the round shell of hero_head.py, exported with the hair) and the neck
        # mended: the torn shards peeled and the hole behind the collar closed
        import hero_head
        print("neck:", hero_head.fix_neck(meshes[0]))
    export(os.path.join(OUT, f"hero_{slot}.fbx"), [arm] + meshes)
    print(f"hero_{slot}.fbx: {[m.name for m in meshes]}")


if DO_HEAD:
    export_head()
