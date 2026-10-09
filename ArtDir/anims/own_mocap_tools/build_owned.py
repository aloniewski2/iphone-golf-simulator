import bpy,json,math,numpy as np
from pathlib import Path
from mathutils import Vector,Quaternion,Matrix
P=Path(__file__).resolve().parents[1];data=json.loads((P/'own_mocap_tools/markers.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_Mixamo_QA.blend'))
rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin'];scene=bpy.context.scene;scene.render.fps=30
rig.animation_data_clear();rig.animation_data_create()
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
bones=list(rig.pose.bones);rest={b.name:b.bone.matrix_local.to_quaternion() for b in bones};I=Quaternion();Z=Vector((0,0,1))
local={b.name:(rest[b.parent.name].inverted()@rest[b.name] if b.parent else rest[b.name]) for b in bones}
def smooth(a,n=5):
 return np.stack([np.convolve(np.pad(a[:,j],(n//2,n//2),mode='edge'),np.ones(n)/n,mode='valid') for j in range(3)],axis=1)
def frameq(x,z):
 z=z.normalized();x=(x-z*x.dot(z)).normalized();y=z.cross(x).normalized();return Matrix((x,y,z)).transposed().to_quaternion()
def aim(name,d,frame):
 base=frame@rest[name];return (base@Vector((0,1,0))).rotation_difference(d.normalized())@base

def setq(pb,q,parentq):
 pb.rotation_mode='QUATERNION';pb.rotation_quaternion=local[pb.name].inverted()@(parentq.inverted()@q if pb.parent else q);pb.location=Vector((0,0,0));pb.scale=Vector((1,1,1))
def ik(a,b,target,pole,frame,world):
 bpy.context.view_layer.update();head=rig.pose.bones[a].head.copy();v=target-head;dist=min(v.length,rig.data.bones[a].length+rig.data.bones[b].length-.002);direction=v.normalized();l1=rig.data.bones[a].length;l2=rig.data.bones[b].length
 along=(l1*l1-l2*l2+dist*dist)/(2*max(.01,dist));height=math.sqrt(max(.00001,l1*l1-along*along));side=pole-direction*pole.dot(direction)
 if side.length<.001:side=Vector((0,-1,0))-direction*direction.y*-1
 knee=head+direction*along+side.normalized()*height
 for n,d in [(a,knee-head),(b,target-knee)]:
  world[n]=aim(n,d,frame);pb=rig.pose.bones[n];setq(pb,world[n],world[pb.parent.name]);bpy.context.view_layer.update()

stages={};metrics={}
for stage in ['eyes_raw_retarget','eyes_clean']:
 stages[stage]={}
 for name,clip in data.items():
  m={n:np.array(v) for n,v in clip['markers'].items()};N=len(next(iter(m.values())));clean=stage=='eyes_clean'
  if clean:m={n:smooth(v,7 if name=='ReadyIdle' else 5) for n,v in m.items()}
  acts=[];locations=[]
  pelvis=(m['RFWT']+m['RBWT']+m['LFWT']+m['LBWT'])/4;neck=(m['RSHO']+m['LSHO'])/2
  for i in range(N):
   v=lambda n:Vector(m[n][i]);hip=frameq(v('LFWT')+v('LBWT')-v('RFWT')-v('RBWT'),Z)
   chest=frameq(v('LSHO')-v('RSHO'),Vector(neck[i]-pelvis[i]));head=frameq(v('LFHD')+v('LBHD')-v('RFHD')-v('RBHD'),Z)
   if clean:hip=I.slerp(hip,.65);chest=I.slerp(chest,.78);head=I.slerp(head,.25)
   world={'Root':rest['Root'],'Hips':hip@rest['Hips'],'Spine':hip.slerp(chest,.4)@rest['Spine'],'Chest':chest@rest['Chest'],'Neck':chest.slerp(head,.6)@rest['Neck'],'Head':head@rest['Head']}
   for s in ['L','R']:
    world['Shoulder.'+s]=chest@rest['Shoulder.'+s]
    for n,a,b,fr in [('UpperArm',s+'SHO',s+'ELB',chest),('LowerArm',s+'ELB',s+'WRA',chest),('Hand',s+'WRA',s+'FIN',chest),('UpperLeg',s+'FWT',s+'KNE',hip),('LowerLeg',s+'KNE',s+'ANK',hip),('Foot',s+'HEE',s+'TOE',I),('Toes',s+'HEE',s+'TOE',I)]:
     aa=(v(s+'WRA')+v(s+'WRB'))/2 if a==s+'WRA' else v(a);bb=(v(s+'WRA')+v(s+'WRB'))/2 if b==s+'WRA' else v(b)
     if n in ['Foot','Toes']:bb.z=aa.z
     world[n+'.'+s]=aim(n+'.'+s,bb-aa,fr)
     if clean and n in ['Foot','Toes']:
      direction=bb-aa;yaw=max(-.30,min(.30,math.atan2(direction.x,-direction.y)))
      world[n+'.'+s]=Quaternion(Z,yaw)@rest[n+'.'+s]
   if clean:
    for side in ['L','R']:
     # Marker on knuckle is not a wrist joint axis. Keep a firm tennis wrist.
     neutral=world['LowerArm.'+side]@rest['LowerArm.'+side].inverted()@rest['Hand.'+side]
     world['Hand.'+side]=neutral.slerp(world['Hand.'+side],.12)
   for pb in bones:setq(pb,world[pb.name],world.get(pb.parent.name,I) if pb.parent else I)
   delta=Vector((pelvis[i]-pelvis[0])*.72)
   if clean:delta.x=0;delta.y=0;delta.z=max(-.055,min(.055,delta.z))
   rig.pose.bones['Hips'].location=rest['Hips'].inverted()@delta
   if clean:
    for s in ['L','R']:
     # Plant ankles on the short-legged hero; preserve small source heel rise.
     target=rig.data.bones['Foot.'+s].head_local.copy();target.z+=max(0,min(.035,m[s+'ANK'][i,2]-np.percentile(m[s+'ANK'][:,2],15)))
     pole=Vector((0,-1,.05));ik('UpperLeg.'+s,'LowerLeg.'+s,target,pole,hip,world)
    if name=='Backhand':
     bpy.context.view_layer.update();right=rig.pose.bones['Hand.R'].head.copy();left=rig.pose.bones['Hand.L'].head.copy()
     # Stack toy palms along the shared grip only during the source two-hand clasp.
     sourcegap=(v('LWRA')-v('RWRA')).length
     if sourcegap<.38:
      blend=max(0,min(1,(.38-sourcegap)/.12));blend=blend*blend*(3-2*blend)
      target=left.lerp(right+Vector((.09,0,.04)),blend);pole=(v('LELB')-v('LSHO')).normalized();ik('UpperArm.L','LowerArm.L',target,pole,chest,world)
   acts.append({pb.name:list(pb.rotation_quaternion) for pb in bones});locations.append(list(rig.pose.bones['Hips'].location))
  stages[stage][name]={'rot':acts,'loc':locations}

# Save immutable raw/clean actions before authoring timing and posing.
def make_action(name,samples):
 act=bpy.data.actions.new(name);act.use_fake_user=True;rig.animation_data.action=act
 for i,(rot,loc) in enumerate(zip(samples['rot'],samples['loc']),1):
  for pb in bones:
   pb.rotation_quaternion=Quaternion(rot[pb.name]);pb.location=Vector(loc) if pb.name=='Hips' else Vector((0,0,0));pb.scale=(1,1,1)
   pb.keyframe_insert('rotation_quaternion',frame=i,group=pb.name)
   if pb.name=='Hips':pb.keyframe_insert('location',frame=i,group=pb.name)
 return act

def export_clip(path,act):
 rig.animation_data.action=act;scene.frame_start=1;scene.frame_end=int(act.frame_range[1]);scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='AUTO',embed_textures=False)

owned={};ready=stages['eyes_clean']['ReadyIdle'];base=ready['rot'][len(ready['rot'])//2]
for name,c in stages['eyes_clean'].items():
 if name=='ReadyIdle':
  sample={'rot':[],'loc':[]};N=len(c['rot'])
  for i in range(N):
   # Endpoint correction distributes loop seam error through the full cycle.
   u=i/(N-1);r={}
   for b in base:
    q=Quaternion(c['rot'][i][b]);start=Quaternion(c['rot'][0][b]);end=Quaternion(c['rot'][-1][b]);corr=start@end.inverted();q=I.slerp(corr,u)@q;r[b]=list(q)
   sample['rot'].append(r);sample['loc'].append(list(Vector(c['loc'][i])-Vector(c['loc'][-1])*u))
  owned[name]=sample;metrics[name]={'duration':(N-1)/30,'loop_endpoint_corrected':True};continue
 # Piecewise time warp: same source windup, contact compressed, follow-through held.
 # Source contact proxy 1.8 sec; measured wrist peak rather than an untracked ball.
 duration=3.6;contact={'Forehand':58/30,'Backhand':56/30,'Serve':52/30,'Volley':54/30}[name];out_t=[0,.95,1.08,1.27,1.72,2.12,3.6];src_t=[0,contact-.35,contact,contact+.20,contact+.35,contact+.35,4.4]
 sample={'rot':[],'loc':[]};ratios=[]
 for i in range(round(duration*30)+1):
  t=i/30;s=float(np.interp(t,out_t,src_t))*30;j=min(int(s),len(c['rot'])-2);f=s-j;r={}
  envelope=max(0,1-abs(t-.95)/.55) if t<=1.08 else 0
  for b in base:
   q=Quaternion(c['rot'][j][b]).slerp(Quaternion(c['rot'][j+1][b]),f)
   if b.startswith(('UpperArm','LowerArm','Chest','Spine')):
    ref=Quaternion(base[b]);delta=ref.inverted()@q;delta.normalize()
    if delta.w<0:delta.negate()
    axis,angle=delta.to_axis_angle();q=ref@Quaternion(axis,angle*(1+.22*envelope))
    if abs(t-.95)<.017 and angle>.02:ratios.append(1+.22*envelope)
   # Blend complete ready pose into/out of the authored stroke.
   blend=min(1,t/.20,(duration-t)/.38);blend=blend*blend*(3-2*blend);q=Quaternion(base[b]).slerp(q,max(0,blend));r[b]=list(q)
  sample['rot'].append(r);sample['loc'].append(list(Vector(c['loc'][j]).lerp(Vector(c['loc'][j+1]),f)*min(1,t/.20,(duration-t)/.38)))
 owned[name]=sample;metrics[name]={'duration':duration,'contact_seconds':1.08,'windup_seconds':.95,'anticipation_rotation_multiplier':sum(ratios)/len(ratios),'contact_source_interval':.35,'contact_owned_interval':.13,'followthrough_hold_seconds':.40,'root_xz_stripped':True,'source':data[name]['source'],'source_frames':data[name]['source_frames'],'source_contact_seconds':contact}
# Recheck the two-hand clearance after exaggeration; do not amplify hands into each other.
for rot,loc in zip(owned['Backhand']['rot'],owned['Backhand']['loc']):
 for pb in bones:
  pb.rotation_quaternion=Quaternion(rot[pb.name]);pb.location=Vector(loc) if pb.name=='Hips' else Vector((0,0,0))
 bpy.context.view_layer.update();right=rig.pose.bones['Hand.R'].head.copy();left=rig.pose.bones['Hand.L'].head.copy();gap=left-right
 if gap.length<.10:
  world={b.name:b.matrix.to_quaternion() for b in bones};chest=world['Chest']@rest['Chest'].inverted();target=right+(gap.normalized() if gap.length>.001 else Vector((1,0,0)))*.10
  pole=rig.pose.bones['UpperArm.L'].tail-rig.pose.bones['UpperArm.L'].head
  ik('UpperArm.L','LowerArm.L',target,pole,chest,world)
  for n in ['UpperArm.L','LowerArm.L']:rot[n]=list(rig.pose.bones[n].rotation_quaternion)
stages['hero_owned']=owned
for stage,clips in stages.items():
 for a in list(bpy.data.actions):bpy.data.actions.remove(a)
 actions={}
 for name,samples in clips.items():
  final='Hero_'+name+'_v1' if stage=='hero_owned' else name+'_'+('Raw' if stage=='eyes_raw_retarget' else 'Clean')
  act=make_action(final,samples);actions[name]=act;export_clip(P/stage/(final+'.fbx'),act)
 rig.animation_data.action=actions['ReadyIdle'];scene.frame_start=1;scene.frame_end=int(actions['ReadyIdle'].frame_range[1]);scene.frame_set(1)
 bpy.ops.wm.save_as_mainfile(filepath=str(P/stage/('Hero_Owned_v1.blend' if stage=='hero_owned' else 'Hero_'+stage+'.blend')))
(P/'hero_owned/authoring_metrics.json').write_text(json.dumps(metrics,indent=2));print('OWNED_EXPORT_COMPLETE',flush=True)
