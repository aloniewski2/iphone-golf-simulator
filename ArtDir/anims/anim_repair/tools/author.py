import bpy,json,math,copy
from pathlib import Path
from mathutils import Vector,Quaternion,Matrix
P=Path(__file__).resolve().parents[1];A=P.parent
bpy.ops.wm.open_mainfile(filepath=str(A/'knees_offarm/Hero_01_Knees_QA.blend'));rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin']
data=json.loads((A/'backhand_run/gameplay_pose_bake.json').read_text());original=copy.deepcopy(data);names=data['bones'];C=Matrix(((-1,0,0),(0,0,1),(0,-1,0)))
def V(v):return Vector(tuple(v[k] for k in 'xyz'))
def Q(v):return Quaternion((v['w'],v['x'],v['y'],v['z']))
def ov(v):return dict(x=v.x,y=v.y,z=v.z)
def oq(v):return dict(x=v.x,y=v.y,z=v.z,w=v.w)
def read(f):return ({n:V(v) for n,v in zip(names,f['positions'])},{n:Q(v) for n,v in zip(names,f['rotations'])})
def save(f,ps,qs):f['positions']=[ov(ps[n]) for n in names];f['rotations']=[oq(qs[n]) for n in names]
rest={n:Q(v) for n,v in zip(names,data['restRotations'])};bind={b.name:C@b.head_local for b in rig.data.bones};up=Vector((0,1,0));fw=Vector((0,0,1));right=Vector((1,0,0));identity=Quaternion();socket=Quaternion(Vector((0,0,1)),-math.pi/2)
parents={b.name:(b.parent.name if b.parent else None) for b in rig.data.bones}
palm={}
for s in ['L','R']:
 idx=body.vertex_groups['Hand.'+s].index;vs=[C@v.co for v in body.data.vertices if any(g.group==idx and g.weight>.75 for g in v.groups)]
 center=sum(vs,Vector())/len(vs);palm[s]=rest['Hand.'+s].inverted()@(center-bind['Hand.'+s])
palm['R']=Vector((-.00394226045,.0874634832,-.0277425803))
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
def grip(ps,qs,center,direction,two,previous=None):
 fr=qs['Chest']@rest['Chest'].inverted();base=up.rotation_difference(direction.normalized());best=None
 for roll in range(-180,181,10):
  rq=Quaternion(direction.normalized(),math.radians(roll))@base;hr=rq@socket.inverted();wr=center-hr@palm['R'];ar=arm(ps,qs,'R',wr,hr,fr)
  if ar is None:continue
  cost=ar[0];al=None;hl=None;wl=None
  if two:
   # Opposite palm wraps from the far side of the same handle.
   lp=center+direction.normalized()*.085;leftbest=None
   for leftroll in range(-180,181,30):
    lq=Quaternion(direction.normalized(),math.radians(leftroll))@hr;lw=lp-lq@palm['L'];la=arm(ps,qs,'L',lw,lq,fr)
    if la is not None and (leftbest is None or la[0]<leftbest[0]):leftbest=(la[0],la,lw,lq)
   if leftbest is None:continue
   _,al,wl,hl=leftbest
   cost+=al[0]+max(ar[5],al[5])**2*30
  if previous:cost+=previous.rotation_difference(rq).angle*.8
  if best is None or cost<best[0]:best=(cost,rq,ar,wr,al,wl)
 if best is None:raise RuntimeError('No reachable coupled grip '+str(center))
 _,rq,ar,wr,al,wl=best
 for s,res,wrist in [('R',ar,wr),('L',al,wl)]:
  if res is None:continue
  _,elbow,qa,qb,hq,wa,tw=res;ps['LowerArm.'+s]=elbow;ps['Hand.'+s]=wrist;qs['UpperArm.'+s]=qa;qs['LowerArm.'+s]=qb;qs['Hand.'+s]=hq
 return rq,math.degrees(ar[5]),math.degrees(al[5]) if al else 0
report={'palm_offsets':{s:list(v) for s,v in palm.items()},'socket_euler':[0,0,-90],'source':'Hand-keyed two-hand constraints using Eyes Japan Backhand Hard Hit timing reference; previous Backhand curves ignored','changed':['Ready','Backhand','Volley','RunForward'],'untouched':['RunRight','RunLeft'],'grip_samples':[]}
# Ready keeps all lower-body keys. Gentle breathing stays in original clip.
ready=next(c for c in data['clips'] if c['name']=='Ready');previous=None
for i,f in enumerate(ready['frames']):
 ps,qs=read(f);fr=qs['Chest']@rest['Chest'].inverted();center=Vector((.14,.87+math.sin(i/119*2*math.pi)*.006,.41));previous,wa,_=grip(ps,qs,center,Vector((.45,.70,.55)),False,previous);save(f,ps,qs)
report['ready_wrist_angle']=wa
bp,bq=read(ready['frames'][0]);clip=next(c for c in data['clips'] if c['name']=='Backhand');previous=None
# 4 sec: load 0–1.50 (38%), contact 1.50–1.60 (3 frames), follow 1.60–3.20 (40%), settle.
for i,f in enumerate(clip['frames']):
 t=i/30;hy=sample([(0,0),(1.5,-34),(1.6,-15),(2.9,27),(3.2,27),(4,0)],t);cy=sample([(0,0),(1.5,-52),(1.6,-30),(2.9,38),(3.2,38),(4,0)],t);step=sample([(0,0),(1.35,1),(2.9,1),(4,0)],t)
 ps={};qs={}
 for n in names:
  par=parents[n];yaw=hy if n=='Hips' else hy*.5+cy*.5 if n=='Spine' else cy if n.startswith(('Chest','Shoulder','UpperArm','LowerArm','Hand')) else cy*.22 if n in ['Neck','Head'] else 0
  qs[n]=Quaternion(up,math.radians(yaw))@bq[n];ps[n]=ps[par]+qs[par]@bq[par].inverted()@(bp[n]-bp[par]) if par else bp[n].copy()
  if n=='Hips':ps[n]+=Vector((-.02*step,-.03*step,.035*step))
 for s in ['L','R']:
  target=bp['Foot.'+s]+(Vector((-.05,0,.12))*step if s=='R' else Vector());a,b,c='UpperLeg.'+s,'LowerLeg.'+s,'Foot.'+s;elbow=ik(ps[a],target,a,b,c,fw.copy())
  if elbow:
   fr=qs['Hips']@rest['Hips'].inverted();ps[b]=elbow;ps[c]=target;qs[a]=aim(a,elbow-ps[a],fr);qs[b]=aim(b,target-elbow,fr)
  qs[c]=bq[c];qs['Toes.'+s]=bq['Toes.'+s];ps['Toes.'+s]=ps[c]+bp['Toes.'+s]-bp[c]
 center=sample([(0,(.05,.94,.43)),(1.5,(-.12,.98,.39)),(1.6,(-.17,.99,.46)),(2.9,(.20,.93,.35)),(3.2,(.20,.93,.35)),(4,(.05,.94,.43))],t)
 direction=sample([(0,(0,.9,.43)),(1.5,(-.65,.67,.25)),(1.6,(-.85,.20,.49)),(2.9,(.75,.42,.50)),(3.2,(.75,.42,.50)),(4,(0,.9,.43))],t)
 previous,wr,wl=grip(ps,qs,center,direction,True,previous);save(f,ps,qs)
 report['grip_samples'].append({'frame':i,'right_wrist_deg':wr,'left_wrist_deg':wl,'right_palm':list(center),'left_palm':list(center+direction.normalized()*.085),'hip_yaw':hy,'hoop':list(center+direction.normalized()*.395)})
# Volley is a short front block with a 3-frame punch, not a full drive.
clip=next(c for c in data['clips'] if c['name']=='Volley');previous=None
for i,f in enumerate(clip['frames']):
 ps={n:v.copy() for n,v in bp.items()};qs={n:v.copy() for n,v in bq.items()};t=i/30
 extend=sample([(0,0),(.40,0),(.56,.10),(.66,1),(.83,.8),(1.35,0),(4,0)],t)
 center=Vector((.15,.90,.40+extend*.16));previous,wa,_=grip(ps,qs,center,Vector((.45,.70,.55)),False,previous)
 # Original repaired off hand stays clear; small counterbalance only.
 for n in ['LowerArm.L','Hand.L']:ps[n]+=Vector((-.025*extend,0,.025*extend))
 save(f,ps,qs)
# Forward run only: controlled right-arm pump with flexed elbow, neutral wrist.
clip=next(c for c in data['clips'] if c['name']=='RunForward');previous=None
for i,f in enumerate(clip['frames']):
 ps,qs=read(f);fr=qs['Chest']@rest['Chest'].inverted();leftdepth=(fr.inverted()@(ps['Hand.L']-ps['UpperArm.L'])).z
 phase=max(-1,min(1,(leftdepth-.13)/.15));center=ps['Chest']+fr@Vector((.27,-.20+.02*phase,.33-.08*phase))
 previous,wa,_=grip(ps,qs,center,fr@Vector((.05,.82,.57)),False,previous);save(f,ps,qs)
for a,b in zip(original['clips'],data['clips']):
 if a['name'] not in report['changed']:assert a==b
(P/'gameplay_pose_bake.json').write_text(json.dumps(data));(P/'author_report.json').write_text(json.dumps(report,indent=2));print('AUTHOR_COMPLETE',flush=True)
