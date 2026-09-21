"""Read-only source conversion of the approved resort and V4 tennis standards."""
import bpy, json, math
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2]
arena=R/'Unity/Assets/Resources/Tennis';arena.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(R/'SportsLibrary/Arenas/Tennis/coastal-tennis-resort-v1.blend'))
s=bpy.context.scene
selected=[]
for o in list(s.objects):
 if o.type not in {'MESH','CURVE'} or o.hide_render or any(c.name.startswith('07') for c in o.users_collection):continue
 o.hide_set(False);o.hide_viewport=False;selected.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in selected:o.select_set(True)
bpy.context.view_layer.objects.active=selected[0]
bpy.ops.object.convert(target='MESH')
# Join by spatial tile to reduce object overhead while retaining cullable pieces.
groups={}
for o in list(bpy.context.selected_objects):
 p=o.matrix_world.translation;k=(math.floor(p.x/24),math.floor(p.y/24))
 groups.setdefault(k,[]).append(o)
joined=[]
for k,objects in groups.items():
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
 o=bpy.context.object;o.name=f'Resort tile {k[0]} {k[1]}';joined.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in joined:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(arena/'CoastalTennisResort.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,use_mesh_modifiers=True,path_mode='AUTO')
print('ARENA',len(joined),flush=True)
for gender in ['Male','Female']:
 bpy.ops.wm.open_mainfile(filepath=str(R/'SportsLibrary/Blender/sports-animation-studio-v4.blend'))
 s=bpy.data.scenes['03 TENNIS'];bpy.context.window.scene=s
 rig=bpy.data.objects[f'{gender}_Tennis_Rig'];delta=rig.location.copy()
 prop=next(c for c in s.collection.children if c.name.startswith('V4 PROP '+gender+' V4 racket'))
 racket=next(o for o in prop.objects if o.parent is None)
 poses=[]
 for f in range(1,683):
  s.frame_set(f);poses.append((f,racket.matrix_world.copy()))
 racket.animation_data_clear();previous=None
 for f,m in poses:
  m.translation-=delta;racket.matrix_world=m
  if previous and racket.rotation_quaternion.dot(previous)<0:racket.rotation_quaternion.negate()
  previous=racket.rotation_quaternion.copy()
  for channel in ['location','rotation_quaternion','scale']:racket.keyframe_insert(channel,frame=f)
 rig.location-=delta
 selected=[rig]+list(bpy.data.collections[f'V4 {gender} Tennis | BODY'].objects)+list(bpy.data.collections[f'V4 {gender} Tennis | KIT 1'].objects)+list(prop.objects)
 for c in s.collection.children:c.hide_viewport=False
 for o in s.objects:o.select_set(False)
 for o in selected:
  o.hide_set(False);o.hide_viewport=False;o.hide_render=False;o.select_set(True)
  if o.type=='MESH':o.data=o.data.copy()
  if o.type=='CURVE':bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
 marker=bpy.data.objects.new('TennisSweetSpot',None);s.collection.objects.link(marker);marker.parent=racket;marker.location=(0,0,.4);marker.select_set(True)
 for name,position in [('TennisStringRight',(.2,0,.4)),('TennisStringUp',(0,0,.67)),('TennisStringNormal',(0,.1,.4))]:
  marker=bpy.data.objects.new(name,None);s.collection.objects.link(marker);marker.parent=racket;marker.location=position;marker.select_set(True)
 s.frame_start=1;s.frame_end=682;s.frame_set(1);s.render.fps=30
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(R/f'Unity/Assets/Resources/StandardCharacters/standard_{gender.lower()}_tennis.fbx'),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO')
 print('TENNIS',gender,flush=True)
