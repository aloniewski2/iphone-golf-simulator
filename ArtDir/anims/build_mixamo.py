import bpy,json,sys
from pathlib import Path
from mathutils import Matrix
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'hero/modular/Hero_01_Assembled.blend'))
rig=bpy.data.objects['Hero_01_Rig'];meshes=[o for o in bpy.data.collections['Hero_01'].objects if o.type=='MESH']
socket_world={n:bpy.data.objects[n].matrix_world.copy() for n in ['Hat','Hand_R','Hand_L','Back','FaceExtra']}
bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for b in rig.data.edit_bones:
 if b.name!='Root':b.head.y+=.055;b.tail.y+=.055
bpy.ops.object.mode_set(mode='OBJECT');bpy.context.view_layer.update()
for n,m in socket_world.items():bpy.data.objects[n].matrix_world=m
report={'clips':{},'rest_matrix_error':{},'method':'Mixamo recognized and retargeted uploaded shared 22-bone skeleton; no second skeleton.'}
actions={}
for clip,file in [('Idle','Mixamo_BreathingIdle.fbx'),('Ready','Mixamo_ReadyIdle.fbx')]:
 if not (P/'source'/file).exists():continue
 before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(P/'source'/file));added=set(bpy.data.objects)-before;src=next(o for o in added if o.type=='ARMATURE')
 report['rest_matrix_error'][clip]=max(abs(src.data.bones[b.name].matrix_local[i][j]-b.matrix_local[i][j]) for b in rig.data.bones for i in range(4) for j in range(4))
 # Bake from world pose into the approved rig, avoiding FBX axis/rest-roll assumptions.
 start,end=map(round,src.animation_data.action.frame_range);act=bpy.data.actions.new(clip);act.use_fake_user=True;rig.animation_data_create();rig.animation_data.action=act
 for f in range(start,end+1):
  bpy.context.scene.frame_set(f)
  for pb in rig.pose.bones:
   pb.rotation_mode='QUATERNION'
   correction=Matrix.Translation((0,.055,0)) if pb.name!='Root' else Matrix.Identity(4)
   pb.matrix=rig.matrix_world.inverted()@correction@src.matrix_world@src.pose.bones[pb.name].matrix
   bpy.context.view_layer.update()
   pb.keyframe_insert('location',frame=f);pb.keyframe_insert('rotation_quaternion',frame=f);pb.keyframe_insert('scale',frame=f)
 actions[clip]=act;report['clips'][clip]={'source':file,'frames':[start,end],'fps':30,'duration_seconds':(end-start)/30}
 for o in added:bpy.data.objects.remove(o,do_unlink=True)
rig.animation_data.action=actions['Idle'];scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=round(actions['Idle'].frame_range[1]);scene.frame_set(1)
(P/'build-report.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
scene.cycles.samples=16;scene.render.resolution_percentage=50
for name,a in actions.items():
 rig.animation_data.action=a
 for idx,f in enumerate([int(a.frame_range[0]),int(sum(a.frame_range)/2)]):
  scene.frame_set(f);scene.render.filepath=str(P/f'qa_{name}_{idx}.png');bpy.ops.render.render(write_still=True)
print('BUILD_DONE',json.dumps(report))
