import bpy,json,math,copy
from pathlib import Path
from mathutils import Vector,Quaternion,Matrix
P=Path(__file__).resolve().parents[1];A=P.parent
bpy.ops.wm.open_mainfile(filepath=str(A/'knees_offarm/Hero_01_Knees_QA.blend'));rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin']
data=json.loads((A/'anim_repair/gameplay_pose_bake.json').read_text());original=copy.deepcopy(data);names=data['bones'];C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
def V(v):return Vector(tuple(v[k] for k in 'xyz'))
def Q(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def ov(v):return dict(x=v.x,y=v.y,z=v.z)
def oq(v):return dict(x=v.x,y=v.y,z=v.z,w=v.w)
def read(f):return ({n:V(v) for n,v in zip(names,f['positions'])},{n:Q(v) for n,v in zip(names,f['rotations'])})
def save(f,ps,qs):f['positions']=[ov(ps[n]) for n in names];f['rotations']=[oq(qs[n]) for n in names]
rest={n:Q(v) for n,v in zip(names,data['restRotations'])};bind={b.name:C@b.head_local for b in rig.data.bones};up=Vector((0,1,0));fw=Vector((0,0,1));right=Vector((1,0,0));identity=Quaternion();socket=Quaternion((.70710678,.70710678,0,0))
parents={b.name:(b.parent.name if b.parent else None) for b in rig.data.bones}
palm={}
for s in ['L','R']:
 idx=body.vertex_groups['Hand.'+s].index;vs=[C@v.co for v in body.data.vertices if any(g.group==idx and g.weight>.75 for g in v.groups)]
 center=sum(vs,Vector())/len(vs);palm[s]=rest['Hand.'+s].inverted()@(center-bind['Hand.'+s])
palm={'R':Vector((.060,.092,-.065)), 'L':Vector((.060,.092,-.020))}
nextbone={'UpperArm.R':'LowerArm.R','LowerArm.R':'Hand.R','UpperArm.L':'LowerArm.L','LowerArm.L':'Hand.L','UpperLeg.L':'LowerLeg.L','LowerLeg.L':'Foot.L','UpperLeg.R':'LowerLeg.R','LowerLeg.R':'Foot.R'}
def aim(n,d,fr):return (fr@(bind[nextbone[n]]-bind[n])).rotation_difference(d.normalized())@fr@rest[n]
def sample(keys,t):
 for (a,x),(b,y) in zip(keys,keys[1:]):
  if t<=b:
   u=max(0,min(1,(t-a)/(b-a)));u=u*u*(3-2*u)
   return Vector(x).lerp(Vector(y),u) if isinstance(x,tuple) else x+(y-x)*u
 return Vector(keys[-1][1]) if isinstance(keys[-1][1],tuple) else keys[-1][1]
def ik(root,target,a,b,c,pole):
 l1=(bind[b]-bind[a]).length;l2=(bind[c]-bind[b]).length;axis=target-root;dist=axis.length
 if dist>l1+l2-.006 or dist<.12:return None
 axis.normalize();pole-=axis*pole.dot(axis)
 if pole.length<.0001:return None
 pole.normalize();along=(l1*l1-l2*l2+dist*dist)/(2*dist);joint=root+axis*along+pole*math.sqrt(max(0,l1*l1-along*along));return joint
# Solve the arm roll from the desired hand orientation, then penalize wrist break.
def arm(ps,qs,s,wrist,handq,fr,prev=None):
 a,b,c='UpperArm.'+s,'LowerArm.'+s,'Hand.'+s;root=ps[a];best=None;outward=fr@Vector((1 if s=='R' else -1,0,0))
 for poleangle in range(-70,71,10):
  pole=Quaternion((wrist-root).normalized(),math.radians(poleangle))@(outward-fr@up*.25+fr@fw*.25)
  elbow=ik(root,wrist,a,b,c,pole)
  if elbow is None:continue
  qa=aim(a,elbow-root,fr);qb=aim(b,wrist-elbow,fr);axis=(wrist-elbow).normalized();neutral=qb@rest[b].inverted()@rest[c];diff=handq@neutral.inverted()
  if diff.w<0:diff.negate()
  twist=max(-65,min(65,math.degrees(2*math.atan2(Vector((diff.x,diff.y,diff.z)).dot(axis),diff.w))))
  qb=Quaternion(axis,math.radians(twist))@qb;neutral=qb@rest[b].inverted()@rest[c];wristangle=neutral.rotation_difference(handq).angle;wristangle=min(wristangle,2*math.pi-wristangle)
  torso=(ps['Hips']+ps['Chest'])*.5;clear=math.sqrt((elbow.x-torso.x)**2+(elbow.z-torso.z)**2)-.22
  cost=wristangle*5+max(0,.06-clear)*25+abs(twist)*.002
  if prev is not None:cost+=(elbow-prev).length*2
  if best is None or cost<best[0]:best=(cost,elbow,qa,qb,handq,wristangle,twist)
 return best
# Racket-frame roll is optimized jointly for both wrists. Hands remain on the handle.
def grip(ps,qs,center,direction,two,normal,previous=None):
 fr=qs['Chest']@rest['Chest'].inverted();axis=direction.normalized();normal=-(normal-axis*normal.dot(axis)).normalized();xx=axis.cross(normal).normalized();rq=Matrix((xx,axis,normal)).transposed().to_quaternion()
 hr=rq@socket.inverted();wr=center-hr@palm['R'];ar=arm(ps,qs,'R',wr,hr,fr);al=wl=None
 if two:
  # Opposing palms, both centers constrained to the same handle, 90 mm apart.
  lp=center+axis*.135;best=None
  for angle in range(-180,181,10):
   hl=Quaternion(axis,math.radians(angle))@hr;lw=lp-hl@palm['L'];la=arm(ps,qs,'L',lw,hl,fr)
   if la is not None:
    cost=la[0]+abs(abs(angle)-160)*.01
    if best is None or cost<best[0]:best=(cost,la,lw)
  if best:_,al,wl=best
 if ar is None or (two and al is None):raise RuntimeError('Unreachable grip '+str(center)+' R='+str(ar is not None)+' L='+str(al is not None)+' wrist='+str(wr)+' shoulder='+str(ps['UpperArm.R']))
 for s,res,wrist in [('R',ar,wr),('L',al,wl)]:
  if res is None:continue
  _,elbow,qa,qb,hq,wa,tw=res;ps['LowerArm.'+s]=elbow;ps['Hand.'+s]=wrist;qs['UpperArm.'+s]=qa;qs['LowerArm.'+s]=qb;qs['Hand.'+s]=hq
 return rq,math.degrees(ar[5]),math.degrees(al[5]) if al else 0
report={'socket_position_metres':list(palm['R']),'socket_quaternion_xyzw':[.70710678,0,0,.70710678],'changed':['Ready','Backhand','Volley','Forehand','JumpServe','Smash'],'untouched':['RunForward','RunRight','RunLeft'],'samples':[]}
ready=next(c for c in data['clips'] if c['name']=='Ready')
for i,f in enumerate(ready['frames']):
 ps,qs=read(f);grip(ps,qs,Vector((.16,.89,.38)),Vector((.25,.96,0)),False,fw);save(f,ps,qs)
bp,bq=read(ready['frames'][0])
def bodypose(hy,cy,step):
 ps={};qs={}
 for n in names:
  par=parents[n];yaw=hy if n=='Hips' else (hy+cy)*.5 if n=='Spine' else cy if n.startswith(('Chest','Shoulder','UpperArm','LowerArm','Hand')) else cy*.15 if n in ['Neck','Head'] else 0
  qs[n]=Quaternion(up,math.radians(yaw))@bq[n];ps[n]=ps[par]+qs[par]@bq[par].inverted()@(bp[n]-bp[par]) if par else bp[n].copy()
  if n=='Hips':ps[n]+=Vector((-.025*step,-.025*abs(step),.05*step))
 for s in ['L','R']:
  target=bp['Foot.'+s]+(Vector((-.06,0,.15))*step if s=='R' else Vector());a,b,c='UpperLeg.'+s,'LowerLeg.'+s,'Foot.'+s;elbow=ik(ps[a],target,a,b,c,fw.copy())
  if elbow:
   fr=qs['Hips']@rest['Hips'].inverted();ps[b]=elbow;ps[c]=target;qs[a]=aim(a,elbow-ps[a],fr);qs[b]=aim(b,target-elbow,fr)
  qs[c]=bq[c];qs['Toes.'+s]=bq['Toes.'+s];ps['Toes.'+s]=ps[c]+bp['Toes.'+s]-bp[c]
 return ps,qs
for name in ['Backhand','Volley']:
 clip=next(c for c in data['clips'] if c['name']==name)
 for i,f in enumerate(clip['frames']):
  t=i/30
  if name=='Backhand':
   hy=sample([(0,0),(.9,-57),(1.3,-57),(1.55,-25),(1.7,0),(2.5,40),(3.2,40),(4,0)],t)
   cy=sample([(0,0),(1,-82),(1.4,-82),(1.6,-40),(1.7,-35),(2.6,65),(3.2,65),(4,0)],t)
   step=sample([(0,0),(1,-.3),(1.5,-.3),(1.8,1),(3.2,1),(4,0)],t);ps,qs=bodypose(hy,cy,step)
   center=sample([(0,(.06,.93,.36)),(1,(-.28,1.00,.08)),(1.4,(-.28,.94,.15)),(1.7,(-.10,.98,.40)),(2.1,(.12,1.08,.42)),(2.7,(.28,1.23,.25)),(3.2,(.28,1.23,.25)),(4,(.06,.93,.36))],t)
   direction=sample([(0,(.2,.98,0)),(1,(-.55,.45,-.70)),(1.4,(-.65,-.50,-.56)),(1.7,(-.98,.20,0)),(2.1,(.3,.90,0)),(2.7,(.7,.7,-.15)),(3.2,(.7,.7,-.15)),(4,(.2,.98,0))],t)
   normal=sample([(0,(0,0,1)),(1,(-.6,0,.8)),(1.4,(-.6,0,.8)),(1.7,(0,0,1)),(2.1,(0,0,1)),(2.7,(.8,.3,0)),(3.2,(.8,.3,0)),(4,(0,0,1))],t)
  else:
   hy=sample([(0,0),(.7,8),(1.2,-4),(1.5,-6),(2.4,0),(4,0)],t);cy=sample([(0,0),(.7,22),(1.2,2),(1.5,-8),(2.4,0),(4,0)],t);ps,qs=bodypose(hy,cy,0)
   center=sample([(0,(.16,.89,.38)),(.7,(.25,1.02,.25)),(1.2,(.22,1.00,.38)),(1.5,(.19,.98,.42)),(2.4,(.16,.89,.38)),(4,(.16,.89,.38))],t)
   direction=sample([(0,(.25,.96,0)),(.7,(.45,.89,0)),(1.2,(.7,.71,0)),(1.5,(.75,.66,0)),(2.4,(.25,.96,0)),(4,(.25,.96,0))],t);normal=fw
  rq,wr,wl=grip(ps,qs,center,direction,name=='Backhand',normal);save(f,ps,qs)
  report['samples'].append({'clip':name,'frame':i,'wrist_R_deg':wr,'wrist_L_deg':wl,'hip_yaw':hy,'chest_yaw':cy,'hoop':list(center+direction.normalized()*.395),'face_normal':list(rq@fw)})
# Keep body kinetic chain of existing serve/drive/smash. Re-seat the handle and square the face near the original contact.
for name,contact in [('Forehand',18),('JumpServe',23),('Smash',20)]:
 clip=next(c for c in data['clips'] if c['name']==name)
 for i,f in enumerate(clip['frames']):
  ps,qs=read(f);old=qs['Hand.R']@Quaternion(Vector((0,0,1)),-math.pi/2);center=ps['Hand.R']+qs['Hand.R']@Vector((-.00394226045,.0874634832,-.0277425803));axis=old@up
  normal=old@fw;weight=max(0,1-abs(i-contact)/10);toward=fw-axis*fw.dot(axis)
  if toward.length>.1:normal=normal.lerp(toward.normalized(),weight)
  try:grip(ps,qs,center,axis,False,normal)
  except RuntimeError:
   center=ps['UpperArm.R']+(center-ps['UpperArm.R']).normalized()*.27;grip(ps,qs,center,axis,False,normal)
  save(f,ps,qs)
for a,b in zip(original['clips'],data['clips']):
 if a['name'].startswith('Run'):assert a==b
(P/'gameplay_pose_bake.json').write_text(json.dumps(data));(P/'author_report.json').write_text(json.dumps(report,indent=2));print('AUTHOR_COMPLETE',flush=True)
