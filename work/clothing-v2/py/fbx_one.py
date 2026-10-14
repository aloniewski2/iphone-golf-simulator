"""export the Kit_* object of the open .blend as FBX (metres, Z up, hero faces -Y).  Blender -b <final.blend> --python fbx_one.py -- <out.fbx>"""
import sys, bpy
out = sys.argv[sys.argv.index('--') + 1]
bpy.ops.object.select_all(action='DESELECT')
for o in bpy.data.objects:
    if o.name.startswith('Kit_') and o.type == 'MESH': o.select_set(True); bpy.context.view_layer.objects.active = o
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, global_scale=1.0, apply_unit_scale=True, axis_forward='-Y', axis_up='Z', mesh_smooth_type='FACE', add_leaf_bones=False)
print('FBX', out)
