import bpy,json
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2]
out=R/'Unity/Assets/Resources/StandardCharacters';out.mkdir(parents=True,exist_ok=True)
reports=[]
for gender in ['Male','Female']:
 bpy.ops.wm.open_mainfile(filepath=str(R/'SportsLibrary/Blender/sports-animation-studio-v4.blend'))
 s=bpy.data.scenes['01 GOLF'];bpy.context.window.scene=s
 rig=bpy.data.objects[f'{gender}_Golf_Rig'];delta=rig.location.copy()
 body=bpy.data.collections[f'V4 {gender} Golf | BODY'];kit=bpy.data.collections[f'V4 {gender} Golf | KIT 1']
 prop=next(c for c in s.collection.children if c.name.startswith('V4 PROP '+gender+' V4 Driver'))
 objects=list(dict.fromkeys([rig]+list(body.objects)+list(kit.objects)+list(prop.objects)))
 club=next(o for o in prop.objects if o.parent is None)
 # Fix the sparse source quaternion keys BEFORE evaluating intermediate frames.
 # Correcting sampled matrices afterward cannot undo a spin already baked into them.
 club.animation_data.action=club.animation_data.action.copy()
 for layer in club.animation_data.action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    curves=sorted([c for c in bag.fcurves if c.data_path=='rotation_quaternion'],key=lambda c:c.array_index)
    if len(curves)!=4:continue
    previous=None
    for i in range(len(curves[0].keyframe_points)):
     keys=[c.keyframe_points[i] for c in curves]
     values=[k.co.y for k in keys]
     if previous is not None and sum(a*b for a,b in zip(previous,values))<0:
      for k in keys:
       k.co.y=-k.co.y;k.handle_left.y=-k.handle_left.y;k.handle_right.y=-k.handle_right.y
      values=[-v for v in values]
     for k in keys:k.interpolation='LINEAR'
     previous=values
    for c in curves:c.update()
 poses=[]
 for f in range(1,92):
  s.frame_set(f);poses.append((f,club.matrix_world.copy()))
 club.animation_data_clear()
 previous_rotation=None
 for f,m in poses:
  m.translation-=delta;club.matrix_world=m
  # Equivalent q/-q rotations must share a hemisphere before sub-frame FBX sampling.
  # Otherwise Blender's scalar quaternion curves can spin the club during follow-through.
  if previous_rotation is not None and club.rotation_quaternion.dot(previous_rotation)<0:
   club.rotation_quaternion.negate()
  previous_rotation=club.rotation_quaternion.copy()
  for channel in ['location','rotation_quaternion','scale']:club.keyframe_insert(channel,frame=f)
 for layer in club.animation_data.action.layers:
  for strip in layer.strips:
   for bag in strip.channelbags:
    for curve in bag.fcurves:
     for key in curve.keyframe_points:key.interpolation='LINEAR'
 rig.location-=delta
 for c in s.collection.children:c.hide_viewport=False
 for o in s.objects:o.select_set(False)
 for o in objects:
  o.hide_set(False);o.hide_viewport=False;o.hide_render=False;o.select_set(True)
  if o.type=='MESH':o.data=o.data.copy()
  if o.type=='CURVE':
   bpy.context.view_layer.objects.active=o;bpy.ops.object.convert(target='MESH')
 contact=bpy.data.objects.new('StandardClubContact',None);s.collection.objects.link(contact);contact.parent=club;contact.location=(.04,-.03,-1.05);contact.select_set(True);objects.append(contact)
 s.frame_start=1;s.frame_end=91;s.render.fps=30;s.frame_set(1)
 bpy.context.view_layer.objects.active=rig
 filename=out/f'standard_{gender.lower()}_golf.fbx'
 bpy.ops.export_scene.fbx(filepath=str(filename),use_selection=True,object_types={'ARMATURE','MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_force_startend_keying=True,bake_anim_step=.5,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
 reports.append({'character':gender,'file':filename.name,'objects':len(objects),'bytes':filename.stat().st_size,'source_frames':[1,91],'source_fps':30,'sample_fps':60})
print('EXPORTED '+json.dumps(reports),flush=True)
