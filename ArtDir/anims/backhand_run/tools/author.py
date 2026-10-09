import bpy,json,math,copy
from pathlib import Path
from mathutils import Vector,Quaternion,Matrix
P=Path(__file__).resolve().parents[1];A=P.parent
bpy.ops.wm.open_mainfile(filepath=str(A/'knees_offarm/Hero_01_Knees_QA.blend'));rig=bpy.data.objects['Hero_01_Rig']
data=json.loads((A/'knees_offarm/gameplay_pose_bake.json').read_text());original=copy.deepcopy(data);old=json.loads((A/'gameplay_fixed/gameplay_pose_bake.json').read_text());names=data['bones'];C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
def V(v):return Vector(tuple(v[k] for k in 'xyz'))
def Q(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def ov(v):return dict(x=v.x,y=v.y,z=v.z)
def oq(v):return dict(x=v.x,y=v.y,z=v.z,w=v.w)
def read(f):return ({n:V(v) for n,v in zip(names,f['positions'])},{n:Q(v) for n,v in zip(names,f['rotations'])})
def save(f,ps,qs):f['positions']=[ov(ps[n]) for n in names];f['rotations']=[oq(qs[n]) for n in names]
rest={n:Q(v) for n,v in zip(names,data['restRotations'])};bind={b.name:C@b.head_local for b in rig.data.bones};up=Vector((0,1,0));fw=Vector((0,0,1));right=Vector((1,0,0));identity=Quaternion();socket=Quaternion(Vector((0,0,1)),-math.pi/2)
parents={b.name:(b.parent.name if b.parent else None) for b in rig.data.bones}
def aim(n,d,fr):return (fr@(bind[{'UpperArm.R':'LowerArm.R','LowerArm.R':'Hand.R','UpperArm.L':'LowerArm.L','LowerArm.L':'Hand.L','UpperLeg.L':'LowerLeg.L','LowerLeg.L':'Foot.L','UpperLeg.R':'LowerLeg.R','LowerLeg.R':'Foot.R'}[n]]-bind[n])).rotation_difference(d.normalized())@fr@rest[n]
def solve(ps,qs,a,b,c,target,pole,fr):
 root=ps[a];l1=(bind[b]-bind[a]).length;l2=(bind[c]-bind[b]).length;axis=target-root;d=max(.13,min(axis.length,l1+l2-.012));axis.normalize();target=root+axis*d
 pole=pole-axis*pole.dot(axis);pole.normalize();along=(l1*l1-l2*l2+d*d)/(2*d);joint=root+axis*along+pole*math.sqrt(max(0,l1*l1-along*along))
 ps[b]=joint;ps[c]=target;qs[a]=aim(a,joint-root,fr);qs[b]=aim(b,target-joint,fr)
def sample(keys,t):
 for (a,x),(b,y) in zip(keys,keys[1:]):
  if t<=b:
   u=max(0,min(1,(t-a)/(b-a)));u=u*u*(3-2*u)
   if isinstance(x,tuple):return Vector(x).lerp(Vector(y),u)
   return x+(y-x)*u
 return Vector(keys[-1][1]) if isinstance(keys[-1][1],tuple) else keys[-1][1]
report={'source':'Hand-keyed in Blender using Eyes Japan Backhand Hard Hit timing; no Owned v1 curves','source_file':'tennis-11-backhand hardhit-yamaoka.c3d','source_frames':[880,1408],'eyes_contact_reference_seconds':56/30,'authored_contact_seconds':1.10,'run_changes':{}}
# Runs: derive cadence from original forearm travel, then author a clean forward elbow hinge.
for name in ['RunForward','RunLeft']:
 clip=next(c for c in data['clips'] if c['name']==name);src=next(c for c in old['clips'] if c['name']==name)
 depths=[]
 for f in src['frames']:
  ps,qs=read(f);fr=qs['Chest']@rest['Chest'].inverted();depths.append((fr.inverted()@(ps['Hand.L']-ps['Chest'])).z)
 lo=min(depths);hi=max(depths);amplitude=27 if name=='RunForward' else 18
 angles=[]
 for i,f in enumerate(clip['frames']):
  ps,qs=read(f);fr=qs['Chest']@rest['Chest'].inverted();phase=(depths[i]-lo)/max(.001,hi-lo)*2-1;theta=math.radians(amplitude)*phase;bend=math.radians(86+8*phase)
  # Neutral clavicle; natural sagittal pump, with only a small lateral clearance.
  qs['Shoulder.L']=fr@rest['Shoulder.L'];ps['UpperArm.L']=ps['Shoulder.L']+fr@(bind['UpperArm.L']-bind['Shoulder.L'])
  outward=.16 if name=='RunForward' else .12
  u=fr@Vector((-outward,-math.cos(theta),math.sin(theta))).normalized();l=fr@Vector((-.09,-math.cos(theta+bend),math.sin(theta+bend))).normalized()
  ps['LowerArm.L']=ps['UpperArm.L']+u*(bind['LowerArm.L']-bind['UpperArm.L']).length;ps['Hand.L']=ps['LowerArm.L']+l*(bind['Hand.L']-bind['LowerArm.L']).length
  qs['UpperArm.L']=aim('UpperArm.L',u,fr);qs['LowerArm.L']=aim('LowerArm.L',l,fr);qs['Hand.L']=qs['LowerArm.L']@rest['LowerArm.L'].inverted()@rest['Hand.L']
  angles.append(math.degrees(theta));save(f,ps,qs)
 report['run_changes'][name]={'pump_degrees':[min(angles),max(angles)],'elbow_flexion_degrees':[78,94],'pre_clearance_cadence':True}
# Backhand: build a genuinely new kinetic chain, not any forehand/mirrored source.
ready=next(c for c in data['clips'] if c['name']=='Ready')['frames'][0];bp,bq=read(ready);clip=next(c for c in data['clips'] if c['name']=='Backhand')
marks=[]
for i,f in enumerate(clip['frames']):
 t=i/30;ps={};qs={}
 hipyaw=sample([(0,0),(.3,0),(.88,-25),(1.10,-10),(1.4,18),(1.9,18),(2.6,0),(4,0)],t)
 chestyaw=sample([(0,0),(.3,0),(.88,-58),(1.10,-38),(1.4,32),(1.9,32),(2.6,0),(4,0)],t)
 step=sample([(0,0),(.5,0),(.98,1),(1.35,1),(2.5,0),(4,0)],t);reach=sample([(0,0),(.28,0),(.72,1),(1.85,1),(2.7,0),(4,0)],t)
 for n in names:
  par=parents[n]
  yaw=hipyaw if n=='Hips' else hipyaw*.6+chestyaw*.4 if n=='Spine' else chestyaw if n in ['Chest','Shoulder.R','Shoulder.L','UpperArm.R','LowerArm.R','Hand.R','UpperArm.L','LowerArm.L','Hand.L'] else chestyaw*.22 if n in ['Neck','Head'] else 0
  qs[n]=Quaternion(up,math.radians(yaw))@bq[n]
  if par:ps[n]=ps[par]+qs[par]@bq[par].inverted()@(bp[n]-bp[par])
  else:ps[n]=bp[n].copy()
  if n=='Hips':ps[n]+=Vector((-.035*step,-.025*step,.04*step))
 fr=qs['Chest']@rest['Chest'].inverted();hf=qs['Hips']@rest['Hips'].inverted()
 for s in ['L','R']:
  target=bp['Foot.'+s].copy()
  if s=='R':target+=Vector((-.07*step, .025*math.sin(math.pi*step),.16*step))
  solve(ps,qs,'UpperLeg.'+s,'LowerLeg.'+s,'Foot.'+s,target,fw,hf);qs['Foot.'+s]=bq['Foot.'+s];qs['Toes.'+s]=bq['Toes.'+s];ps['Toes.'+s]=ps['Foot.'+s]+bq['Foot.'+s]@bq['Foot.'+s].inverted()@(bp['Toes.'+s]-bp['Foot.'+s])
 target=sample([(0,tuple(bp['Hand.R'])),(.28,tuple(bp['Hand.R'])),(.85,(-.15,1.04,.30)),(1.10,(-.20,1.00,.36)),(1.45,(.34,1.23,.27)),(1.9,(.34,1.23,.27)),(2.7,tuple(bp['Hand.R'])),(4,tuple(bp['Hand.R']))],t)
 target=bp['Hand.R'].lerp(target,reach)
 solve(ps,qs,'UpperArm.R','LowerArm.R','Hand.R',target,fr@Vector((.7,-.4,.6)),fr)
 neutral=qs['LowerArm.R']@rest['LowerArm.R'].inverted()@rest['Hand.R'];axis=(ps['Hand.R']-ps['LowerArm.R']).normalized()
 desiredUp=sample([(0,(0,.94,.34)),(.85,(-.75,.62,.2)),(1.10,(-.92,.10,.35)),(1.45,(.55,.8,.25)),(1.9,(.55,.8,.25)),(2.7,(0,.94,.34)),(4,(0,.94,.34))],t).normalized()
 currentUp=neutral@socket@up;desired=currentUp.rotation_difference(desiredUp)@neutral
 dif=desired@neutral.inverted()
 if dif.w<0:dif.negate()
 twist=max(-55,min(55,math.degrees(2*math.atan2(Vector((dif.x,dif.y,dif.z)).dot(axis),dif.w))))
 qs['LowerArm.R']=Quaternion(axis,math.radians(twist*.75))@qs['LowerArm.R'];neutral=qs['LowerArm.R']@rest['LowerArm.R'].inverted()@rest['Hand.R'];ang=neutral.rotation_difference(desired).angle
 qs['Hand.R']=neutral.slerp(desired,min(1,math.radians(28)/max(.00001,ang)))
 # Off hand accompanies windup, then opens behind as balance.
 lefttarget=sample([(0,tuple(bp['Hand.L'])),(.8,(-.27,1.02,.34)),(1.1,(-.34,.97,.32)),(1.5,(-.30,1.03,.40)),(2,(-.30,1.03,.40)),(2.7,tuple(bp['Hand.L'])),(4,tuple(bp['Hand.L']))],t)
 solve(ps,qs,'UpperArm.L','LowerArm.L','Hand.L',lefttarget,fr@Vector((-.7,-.3,.5)),fr);qs['Hand.L']=qs['LowerArm.L']@rest['LowerArm.L'].inverted()@rest['Hand.L']
 # Exact ready entry/exit, smooth transition in keyed local transformations.
 for n in names:
  ps[n]=bp[n].lerp(ps[n],reach);qs[n]=bq[n].slerp(qs[n],reach)
 save(f,ps,qs)
 head=ps['Hand.R']+(qs['Hand.R']@socket@up)*.395
 if i in [0,25,33,44,81]:marks.append({'frame':i,'wrist':list(ps['Hand.R']),'racket_hoop_center_approx':list(head),'hip_yaw':hipyaw,'chest_yaw':chestyaw})
report['backhand_landmarks']=marks
# Protected clips must be byte-equivalent in pose data.
for a,b in zip(original['clips'],data['clips']):
 if a['name'] not in ['Backhand','RunForward','RunLeft']:assert a==b
report['unchanged_clips']=['Ready','JumpServe','Forehand','RunRight','Volley','Smash']
(P/'gameplay_pose_bake.json').write_text(json.dumps(data));(P/'author_report.json').write_text(json.dumps(report,indent=2))
print('AUTHOR_COMPLETE',json.dumps(report),flush=True)
