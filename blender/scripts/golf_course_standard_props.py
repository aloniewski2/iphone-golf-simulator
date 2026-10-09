"""Export unchanged Postcard tuft meshes as one reusable library, without rebuilding its atlas."""
import os, sys
import bpy
sys.path.insert(0, os.path.dirname(__file__))
import postcard_props_lib as lib
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
kinds=['PLANT_TUFT_A','PLANT_TUFT_D','PLANT_TUFT_S','PLANT_TUFT_T','PLANT_FLOWER_WHITE']
objects=[lib.place(None,k,(0,0,0),name=k+'_SHARED',allow_low=True) for k in kinds]
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
root=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
path=os.path.join(root,'Unity/Assets/Resources/Course/Standard/Fringe.fbx')
bpy.ops.export_scene.fbx(filepath=path,use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_anim=False,use_mesh_modifiers=True,add_leaf_bones=False,path_mode='AUTO')
print('Exported shared Postcard fringe',path)
