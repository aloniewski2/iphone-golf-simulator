"""Fit the same supplied bases to golf's taller rest skeleton, retaining sport-specific bind poses."""
import bpy
from pathlib import Path
from mathutils import Vector
R=Path(__file__).resolve().parents[2];U=R/'Unity/Assets/Resources/Tennis/Customization'
for sex in ['Male','Female']:
 bpy.ops.wm.open_mainfile(filepath=str(R/f'SportsLibrary/Customization/Bases/Player{sex}-rigged.blend'))
 tennis=next(o for o in bpy.data.objects if o.type=='ARMATURE');parts=[o for o in bpy.data.objects if o.type=='MESH'];before=set(bpy.data.objects)
 bpy.ops.import_scene.fbx(filepath=str(R/f'Unity/Assets/Resources/StandardCharacters/standard_{sex.lower()}_golf.fbx'))
 golf=next(o for o in bpy.data.objects if o not in before and o.type=='ARMATURE');golf.animation_data_clear();golf.data.pose_position='REST'
 for o in list(bpy.data.objects):
  if o not in before and o is not golf:bpy.data.objects.remove(o,do_unlink=True)
 transforms={b.name:golf.data.bones[b.name].matrix_local@b.matrix_local.inverted() for b in tennis.data.bones}
 for o in parts:
  names={g.index:g.name for g in o.vertex_groups}
  # Basis coordinates share storage with mesh vertices. Snapshot every target before
  # writing, otherwise updating v.co and Basis transforms the base twice.
  blocks=list(o.data.shape_keys.key_blocks) if o.data.shape_keys else []
  sets=[(key.data,[v.co.copy() for v in key.data]) for key in blocks] if blocks else [(o.data.vertices,[v.co.copy() for v in o.data.vertices])]
  for data,points in sets:
   for v,point in zip(o.data.vertices,points):
    q=Vector((0,0,0));total=0
    for g in v.groups:
     q+=(transforms[names[g.group]]@point)*g.weight;total+=g.weight
    if total:data[v.index].co=q/total
  if blocks:
   # FBX reads mesh vertices for its base and key blocks for targets. Keep both
   # representations at the same converted rest pose, without a second transform.
   for v,point in zip(o.data.vertices,blocks[0].data):v.co=point.co.copy()
  o.data.update()
  for mod in o.modifiers:
   if mod.type=='ARMATURE':mod.object=golf
  if o.name.startswith('V4 Higgs body'):o.name+='Golf'
 bpy.data.objects.remove(tennis,do_unlink=True);bpy.ops.object.select_all(action='DESELECT')
 for o in [golf]+parts:o.select_set(True)
 bpy.context.view_layer.objects.active=golf
 bpy.ops.export_scene.fbx(filepath=str(U/f'Player{sex}Golf.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,armature_nodetype='NULL',bake_anim=False,path_mode='AUTO')
 print('GOLF BASE',sex,flush=True)
