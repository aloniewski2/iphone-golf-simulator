import bpy,json,math
from pathlib import Path
from mathutils import Vector,Quaternion
P=Path(__file__).resolve().parents[1];report={};samples={}
for stage,file in [('raw','eyes_raw_retarget/Hero_eyes_raw_retarget.blend'),('clean','eyes_clean/Hero_eyes_clean.blend'),('owned','hero_owned/Hero_Owned_v1.blend')]:
 bpy.ops.wm.open_mainfile(filepath=str(P/file));r=bpy.data.objects['Hero_01_Rig'];scene=bpy.context.scene;out={};samples[stage]={}
 for action in bpy.data.actions:
  r.animation_data.action=action;root=[];feet=[];gap=[]
  for i in range(1,int(action.frame_range[1])+1):
   scene.frame_set(i);root.append(list(r.pose.bones['Hips'].head));feet.append([list(r.pose.bones['Foot.'+s].head) for s in ['L','R']]);gap.append((r.pose.bones['Hand.L'].head-r.pose.bones['Hand.R'].head).length)
  out[action.name]={'hip_horizontal_range_m':[max(p[k] for p in root)-min(p[k] for p in root) for k in [0,1]],'ankle_horizontal_range_m':{s:[max(p[j][k] for p in feet)-min(p[j][k] for p in feet) for k in [0,1]] for j,s in enumerate(['L','R'])},'min_wrist_distance_m':min(gap),'frames':len(root),'bones':len(r.pose.bones)}
  name=action.name.removeprefix('Hero_').removesuffix('_v1').removesuffix('_Raw').removesuffix('_Clean');samples[stage][name]={'q':[]}
  # Compare authored rotation displacement at the exact apex against quiet ready.
  if name=='ReadyIdle':f=(action.frame_range[1]+1)/2
  else:
   contact={'Forehand':58/30,'Backhand':56/30,'Serve':52/30,'Volley':54/30}[name];f=1+(.95 if stage=='owned' else contact-.35)*30
  scene.frame_set(int(f),subframe=f%1);samples[stage][name]['q']={b.name:list(b.rotation_quaternion) for b in r.pose.bones}
 report[stage]=out
angles={}
for name in ['Forehand','Backhand','Serve','Volley']:
 vals={}
 for b in ['UpperArm.R','LowerArm.R','Chest','Spine']:
  base=Quaternion(samples['clean']['ReadyIdle']['q'][b]);a=base.rotation_difference(Quaternion(samples['clean'][name]['q'][b])).angle;c=base.rotation_difference(Quaternion(samples['owned'][name]['q'][b])).angle
  vals[b]={'clean_degrees':math.degrees(a),'owned_degrees':math.degrees(c),'ratio':c/a if a>.001 else None}
 angles[name]=vals
report['windup_rotation_measurements']=angles
(P/'hero_owned/verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(angles,indent=2))
