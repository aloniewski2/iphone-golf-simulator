import bpy
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'hero/modular/Hero_01_Assembled.blend'))
bpy.ops.object.select_all(action='DESELECT')
b=bpy.data.objects['Body_Skin'];b.modifiers['Default outfit coverage'].show_viewport=False;b.modifiers['Default outfit coverage'].show_render=False
for o in [b,bpy.data.objects['Body_EyeSphere_L'],bpy.data.objects['Body_EyeSphere_R']]:
 for m in list(o.modifiers):
  if m.type=='ARMATURE':o.modifiers.remove(m)
 mat=o.matrix_world.copy();o.parent=None;o.matrix_world=mat;o.vertex_groups.clear();o.select_set(True)
bpy.context.view_layer.objects.active=b
bpy.ops.export_scene.fbx(filepath=str(P/'source/Hero_01_Body_Unrigged.fbx'),use_selection=True,object_types={'MESH'},bake_anim=False,axis_forward='-Z',axis_up='Y',path_mode='COPY',embed_textures=True)
