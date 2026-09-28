import bpy,json,math
from pathlib import Path
from mathutils import Matrix,Vector,Quaternion
P=Path(__file__).resolve().parents[1];A=P.parent;data=json.loads((P/'gameplay_pose_bake.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Knees_QA.blend'));rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin'];scene=bpy.context.scene
rig.animation_data_clear();rig.animation_data_create()
for action in list(bpy.data.actions):bpy.data.actions.remove(action)
scene.render.fps=30
C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)));CI=C.inverted()
def q(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def vec(v):return Vector((v['x'],v['y'],v['z']))
restB={b.name:b.matrix_local.to_quaternion() for b in rig.data.bones}
restU={n:q(v) for n,v in zip(data['bones'],data['restRotations'])}
max_error=0;report={}
for clip in data['clips']:
 name='Hero_Gameplay_'+clip['name']+'_v1';action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
 prev={}
 for f,frame in enumerate(clip['frames'],1):
  for n,rotation,position in zip(data['bones'],frame['rotations'],frame['positions']):
   world=(CI@(q(rotation)@restU[n].inverted()).to_matrix()@C).to_quaternion()@restB[n]
   pb=rig.pose.bones[n];desired=CI@vec(position)
   pb.rotation_mode='QUATERNION';pb.matrix=Matrix.LocRotScale(desired,world,Vector((1,1,1)));bpy.context.view_layer.update()
   if n in prev and prev[n].dot(pb.rotation_quaternion)<0:pb.rotation_quaternion.negate()
   prev[n]=pb.rotation_quaternion.copy()
   pb.keyframe_insert('rotation_quaternion',frame=f,group=n);pb.keyframe_insert('location',frame=f,group=n)
   max_error=max(max_error,(pb.head-desired).length)
 scene.frame_start=1;scene.frame_end=len(clip['frames']);scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(P/(name+'.fbx')),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='AUTO',embed_textures=False)
 report[name]={'frames':scene.frame_end,'fps':30,'duration':(scene.frame_end-1)/30}
 print('BAKED',name,flush=True)
rig.animation_data.action=bpy.data.actions['Hero_Gameplay_Ready_v1'];scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_Gameplay_Fixed_v1.blend'))
report['max_bone_position_conversion_error_metres']=max_error
(P/'bake_verification.json').write_text(json.dumps(report,indent=2));print('GAMEPLAY_BAKE_COMPLETE',max_error,flush=True)
