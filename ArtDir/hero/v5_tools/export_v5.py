"""Export Hero V5 into the Unity asset paths the gameplay prefab already references (same file and mesh names,
so the prefab keeps its GUID/fileID links): the finger-rig body and the five wardrobe slots.
Usage: Blender -b ArtDir/hero/v5/Hero_01_V5.blend --python export_v5.py"""
import bpy
from pathlib import Path
from mathutils import Matrix
ROOT = Path(__file__).resolve().parents[3]
M = ROOT / 'Unity/Assets/ArtDirection/Hero01/Models'
rig = bpy.data.objects['Hero_01_Rig']
if rig.animation_data: rig.animation_data.action = None
for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
def export(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    rig.select_set(True)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH', 'ARMATURE'}, add_leaf_bones=False,
                             bake_anim=False, axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True)
    print('EXPORTED', path)
# the body (with its outfit-coverage mask applied) is exported exactly as restore_motion/tools/export_body.py did
def baked_mask(o):
    """FBX drops shape keys on a mesh with a non-armature modifier: bake the coverage mask into an export copy
    (bmesh keeps the shape-key layers), same object name so the Unity mesh name is unchanged."""
    import bmesh
    masks = [m for m in o.modifiers if m.type == 'MASK']
    if not masks or not o.data.shape_keys: return o, None
    c = o.copy(); c.data = o.data.copy()
    for col in o.users_collection: col.objects.link(c)
    for m in masks:
        gi = c.vertex_groups[m.vertex_group].index
        bm = bmesh.new(); bm.from_mesh(c.data); dl = bm.verts.layers.deform.active
        def w(v): return v[dl].get(gi, 0.0) if dl else 0.0
        kill = [v for v in bm.verts if (w(v) > m.threshold) == m.invert_vertex_group]
        bmesh.ops.delete(bm, geom=kill, context='VERTS'); bm.to_mesh(c.data); bm.free()
        c.modifiers.remove(c.modifiers[m.name])
    name = o.name; o.name = name + '_src'; c.name = name
    return c, o
body_x, body_src = baked_mask(bpy.data.objects['Body_Skin'])
export(M / 'RestoreMotion/Hero_01_FingerBody.fbx', [body_x])
if body_src: bpy.data.objects.remove(body_x); body_src.name = 'Body_Skin'
for name in ['Hair_Default', 'Hat_Visor', 'Shirt_Default', 'Shorts_Default', 'Shoes_Default']:
    objs = [bpy.data.objects[name]]
    if name == 'Hair_Default':   # every haircut (+ its band fill) ships in the hair FBX; the game / locker pick one
        objs += [o for o in bpy.data.objects if o.type == 'MESH' and o.name != 'Hair_Default' and (o.name.startswith('Hair_') )]
    export(M / ('Hero_01_' + name + '.fbx'), objs)
print('EXPORT_V5_DONE')
