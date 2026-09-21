"""Representative actual V4 poses for testing the sport-independent Unity arm correction."""
import bpy,json
from pathlib import Path
R=Path(__file__).resolve().parents[2]
out=R/'Unity/Assets/Tests/Fixtures/StandardCharacters';out.mkdir(parents=True,exist_ok=True)
manifest=json.loads((R/'SportsLibrary/Animations/V4/Reports/action-manifest.json').read_text())
for sport,clip in [('Tennis','Forehand'),('Bowling','DeliveryStraight'),('Boxing','HookLead')]:
 for gender in ['Male','Female']:
  bpy.ops.wm.open_mainfile(filepath=str(R/'SportsLibrary/Blender/sports-animation-studio-v4.blend'))
  scene=bpy.data.scenes[{'Tennis':'03 TENNIS','Bowling':'02 BOWLING','Boxing':'04 BOXING'}[sport]];bpy.context.window.scene=scene
  rig=bpy.data.objects[f'{gender}_{sport}_Rig'];rig.location=(0,0,0)
  for c in scene.collection.children:c.hide_viewport=False
  selected=[rig]+list(bpy.data.collections[f'V4 {gender} {sport} | BODY'].objects)+list(bpy.data.collections[f'V4 {gender} {sport} | KIT 1'].objects)
  for ob in scene.objects:ob.select_set(False)
  for ob in selected:
   ob.hide_set(False);ob.hide_viewport=False;ob.select_set(True)
   if ob.type=='MESH':ob.data=ob.data.copy()
  e=next(e for e in manifest if e['sport']==sport and e['gender']==gender and e['clip']==clip and e['hand']=='RH')
  scene.frame_start=e['start'];scene.frame_end=e['end'];scene.frame_set(e['start']);scene.render.fps=30
  bpy.context.view_layer.objects.active=rig
  bpy.ops.export_scene.fbx(filepath=str(out/f'{gender.lower()}_{sport.lower()}.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO')
  print('FIXTURE',gender,sport,flush=True)
