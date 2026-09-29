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
# The head is made in one scene (hero_head.py): the scalp and the haircuts (hero_hair.fbx) and each hat
# (hero_visor/cap/sweatband.fbx), since the hats are fitted over the scalp and the hair under the hats.
# Each cut comes from Adnan's hat-less ("_Free") sculpt; the game's names drop the suffix.
CUTS = {"Hair_Default": "Hair_Default_Free", "Hair_Ponytail": "Hair_Ponytail_Free", "Hair_Bob": "Hair_Bob_Free",
        "Hair_Long": "Hair_Long_Free", "Hair_Curly": "Hair_Curly_Free"}
HATS = {"Visor": ("Hero_01_Hat_Visor.fbx", "Hat_Visor", "visor"),
        "Cap": ("Cosmetics/Hero_01_Hat_Cap.fbx", "Hat_Cap", "cap"),
        "Sweatband": ("Cosmetics/Hero_01_Hat_Sweatband.fbx", "Hat_Sweatband", "sweatband")}
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
    import hero_head, hero_repair
    bpy.ops.wm.read_factory_settings(use_empty=True)
    face = import_meshes("RestoreMotion/Hero_01_FingerBody.fbx", {"Body_Skin"})["Body_Skin"]
    arm = face.parent
    eyes = list(import_meshes("Hero_01_Mixamo_Bind.fbx", {"Body_EyeSphere_L", "Body_EyeSphere_R"}).values())
    hair = import_meshes("Hero_01_Hair_Default.fbx", set(CUTS.values()) | {"Hair_Bald", "Hair_Buzz"})
    hats = {k: import_meshes(fbx, {mesh})[mesh] for k, (fbx, mesh, _) in HATS.items()}
    # one skeleton for everything made here (every FBX carries the same rig)
    for o in list(bpy.data.objects):
        if o.type == 'ARMATURE' and o is not arm: bpy.data.objects.remove(o, do_unlink=True)
    arm.name = "Hero_01_Rig"; arm.data.name = "Hero_01_Rig"
    for o in [*eyes, *hair.values(), *hats.values()]:
        m = o.matrix_world.copy(); o.parent = arm; o.matrix_world = m
        for mod in o.modifiers:
            if mod.type == 'ARMATURE': mod.object = arm
    cuts = {}
    for game, src in CUTS.items():
        hair[src].name = game; hair[src].data.name = game; cuts[game] = hair[src]
    scalp, report = hero_head.build_head(arm, face, eyes, hair["Hair_Bald"], hair["Hair_Buzz"], cuts, hats,
                                         flex=hero_repair.hair_flex)
    for k, v in report.items(): print("head:", k, v)
    export(os.path.join(OUT, "hero_hair.fbx"), [arm, scalp, *cuts.values()])
    for k, (_, _, slot) in HATS.items():
        export(os.path.join(OUT, f"hero_{slot}.fbx"), [arm, hats[k]])
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
        # the head's torn top trimmed back to where the scalp (hero_head.py) takes over, as the head export does
        import hero_head
        eyes = list(import_meshes("Hero_01_Mixamo_Bind.fbx", {"Body_EyeSphere_L", "Body_EyeSphere_R"}).values())
        skull = import_meshes("Hero_01_Hair_Default.fbx", {"Hair_Bald"})["Hair_Bald"]
        trimmed, near_edge = hero_head.trim_face(meshes[0], eyes)
        scalp = hero_head.build_scalp(meshes[0], skull, arm, eyes)
        print("face trimmed:", trimmed, "edge blended:", hero_head.blend_face_edge(meshes[0], scalp, near_edge))
        for o in [o for o in bpy.data.objects if o not in meshes and o is not arm]: bpy.data.objects.remove(o, do_unlink=True)
    export(os.path.join(OUT, f"hero_{slot}.fbx"), [arm] + meshes)
    print(f"hero_{slot}.fbx: {[m.name for m in meshes]}")


if DO_HEAD:
    export_head()
