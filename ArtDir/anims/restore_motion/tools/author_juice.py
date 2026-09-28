# Party/juice clips for the locked V4 finger-rig hero, authored as eased key poses on the same solver
# as the approved swings: leg IK, arm IK with elbow pole, hands set RELATIVE TO THE FOREARM (twist/flex/dev,
# so wrists stay human and the left hand can never invert), then the arm-vs-torso clearance pass.
import os,sys,json,math
sys.path.insert(0,os.path.dirname(__file__))
_src=open(os.path.join(os.path.dirname(__file__),'retarget.py')).read()
exec(_src[:_src.index("if os.environ.get('RM_DEBUG')")])   # rig, rest data, IK, set_hand, clear_arm, make_action, export
ONLYJ=os.environ.get('JUICE','').split(',') if os.environ.get('JUICE') else None

def ease(t):return t*t*(3-2*t)
def interp(keys,t,ch,default):
 ks=[k for k in keys if ch in k]
 if not ks:return default
 if t<=ks[0]['t']:return ks[0][ch]
 if t>=ks[-1]['t']:return ks[-1][ch]
 for a,b in zip(ks,ks[1:]):
  if a['t']<=t<=b['t']:
   u=ease((t-a['t'])/max(1e-6,b['t']-a['t']))
   va,vb=a[ch],b[ch]
   return [x+(y-x)*u for x,y in zip(va,vb)] if isinstance(va,(list,tuple)) else va+(vb-va)*u
def E(yaw,pitch,roll):
 return Quaternion((0,0,1),math.radians(yaw))@Quaternion((1,0,0),math.radians(pitch))@Quaternion((0,1,0),math.radians(roll))
FOOT0={s:HEAD['Foot.'+s].copy() for s in 'LR'}
def hand_rel(Q,s,tw,fl,dv):
 n=Q['LowerArm.'+s]@RQ['LowerArm.'+s].inverted()@RQ['Hand.'+s]
 sgn=1 if s=='R' else -1   # mirror so the same numbers mean the same anatomy on both hands
 return n@Quaternion((0,1,0),math.radians(tw*sgn))@Quaternion((0,0,1),math.radians(-fl*sgn))@Quaternion((1,0,0),math.radians(dv))
def pose_at(keys,t,met):
 g=lambda ch,d:interp(keys,t,ch,d)
 spin=Quaternion((0,0,1),math.radians(g('spin',0)))
 hq=spin@E(*g('hips',[0,0,0]));cq=hq@E(*g('chest',[0,0,0]));dq=cq@E(*g('head',[0,0,0]))
 Q={'Root':RQ['Root'],'Hips':hq@RQ['Hips'],'Spine':hq.slerp(cq,.5)@RQ['Spine'],'Chest':cq@RQ['Chest'],'Neck':cq.slerp(dq,.5)@RQ['Neck'],'Head':dq@RQ['Head']}
 for s in 'LR':Q['Shoulder.'+s]=cq@RQ['Shoulder.'+s]
 hh=HEAD['Hips']+spin@Vector(g('pelvis',[0,0,0]))
 pos=fk(Q,hh)
 for s in 'LR':
  f=g('foot'+s,[0,0,0,0]);ank=spin@(FOOT0[s]+Vector(f[:3])-Vector((0,HEAD['Hips'].y,0)))+Vector((0,HEAD['Hips'].y,0));ank.z=max(ank.z,FOOT0[s].z-.005)
  up,lo='UpperLeg.'+s,'LowerLeg.'+s;root=pos[up]
  pole=spin@Vector((0.15 if s=='L' else -.15,-1,.05))
  mid,end=two_bone(root,ank,LEN[up],LEN[lo],pole)
  limb(Q,up,lo,mid-root,end-mid,Vector((0,1,0)),None)
  yq=spin@Quaternion((0,0,1),math.radians(f[3]))
  Q['Foot.'+s]=yq@Quaternion((1,0,0),math.radians(g('toe'+s,0)))@RQ['Foot.'+s];Q['Toes.'+s]=yq@RQ['Toes.'+s]
 pos=fk(Q,hh)
 for s in 'LR':
  h=g('hand'+s,None)
  if h is None:continue
  # hand target & elbow pole in the chest frame
  C=cq
  target=pos['Chest']+C@Vector(h[:3]);pole=C@Vector(g('elbow'+s,[(-1 if s=='R' else 1)*.6,.3,-.6]))
  root=pos['UpperArm.'+s];mid,end=two_bone(root,target,LEN['UpperArm.'+s],LEN['LowerArm.'+s],pole)
  limb(Q,'UpperArm.'+s,'LowerArm.'+s,mid-root,end-mid,Vector((0,-1,0)),None)
  if os.environ.get('JDBG'):print('JD after limb',s,tuple(round(x,3) for x in fk(Q,hh)['Hand.'+s]))
  if s=='R' and g('rdir',None) is not None:
   yv=nz(C@Vector(g('rdir',None)));zn=C@Vector(g('rnrm',[0,-1,0]));zn=nz(zn-yv*zn.dot(yv));xv=yv.cross(zn)
   Rk=Matrix((xv,yv,zn)).transposed();Hq=(Rk@SR.inverted()).to_quaternion()
  else:
   w=g('wrist'+s,[0,0,0]);Hq=hand_rel(Q,s,*w)
  set_hand(Q,s,Hq,1.0,math.pi if s=='R' else math.radians(70))
  if os.environ.get('JDBG'):print('JD after set_hand',s,tuple(round(x,3) for x in fk(Q,hh)['Hand.'+s]))
  if s=='R':
   clear_arm(Q,hh,'R',False,met)
   if os.environ.get('JDBG'):print('JD after clear',tuple(round(x,3) for x in fk(Q,hh)['Hand.R']))
   if g('throat',0)>.5:
    # left hand locked on the racket throat: roll search around the handle (same method as the approved Ready)
    pos=fk(Q,hh);racketW=Matrix.LocRotScale(pos['Hand.R'],Q['Hand.R'],Vector((1,1,1)))@S
    root=pos['UpperArm.L'];best=None
    for deg in range(-180,180,15):
     target=racketW@Matrix.Translation((0,.17-GRIP['grip_y'],0))@Matrix.Rotation(math.radians(deg),4,'Y')@SL.inverted()
     HqL=target.to_quaternion();hand_head=target.translation
     for pole in (Vector((.6,.3,-.6)),Vector((.8,0,-.2)),Vector((.3,.6,-.8))):
      mid,end=two_bone(root,hand_head,LEN['UpperArm.L'],LEN['LowerArm.L'],pole)
      Qik=dict(Q);limb(Qik,'UpperArm.L','LowerArm.L',mid-root,end-mid,Vector((0,-1,0)),None)
      pk=fk(Qik,hh);c=wrist_cost(Qik,'L',HqL)+(end-hand_head).length*20+arm_cost(Qik,pk,pk['UpperArm.L'],pk['LowerArm.L'],pk['Hand.L'],HqL@Vector((0,1,0)))*3
      if best is None or c<best[0]:best=(c,Qik,HqL)
    _,Qik,HqL=best
    for n_ in ['UpperArm.L','LowerArm.L']:Q[n_]=Qik[n_]
    set_hand(Q,'L',HqL,1.0,math.radians(180));met.setdefault('throat',[]).append(1)
    break
 clear_arm(Q,hh,'L',g('throat',0)<=.5,met)
 return Q,hh-HEAD['Hips']
READY_HANDS={'handR':[-.14,-.34,-.20],'rdir':[.55,-.35,.75],'rnrm':[0,-1,0],'throat':1,'handL':[.10,-.34,-.12],'wristL':[0,0,0]}
def K(t,**kw):
 kw['t']=t;return kw
STANCE=dict(footL=[.03,0,0,-10],footR=[-.03,0,0,10],pelvis=[0,0,-.03])
WALK_T=.9; WALK_S=.26
def _walk_keys():
 import math
 ks=[];n=36
 for i in range(n+1):
  t=WALK_T*i/n; kw={}
  for side,ph in (('L',0.0),('R',.5)):
   u=((t/WALK_T)+ph)%1.0
   if u<.6:   # stance: front (-y) to back (+y), linearly
    y=-WALK_S+2*WALK_S*(u/.6); z=0.0; toe=0.0 if u<.45 else -25*((u-.45)/.15)
   else:      # swing: back to front, lifted
    w=(u-.6)/.4; y=WALK_S-2*WALK_S*(w*w*(3-2*w)); z=.075*math.sin(math.pi*w); toe=-25*(1-w)+8*math.sin(math.pi*w)
   x=.03 if side=='L' else -.03
   kw['foot'+side]=[x,y,z,-10 if side=='L' else 10]; kw['toe'+side]=toe
  c=2*math.pi*t/WALK_T
  kw['pelvis']=[.018*math.sin(c),0,-.035+.02*math.cos(2*c)]   # sway toward the planted foot, bob up mid-stance
  kw['hips']=[-6*math.sin(c),2,0]; kw['chest']=[5*math.sin(c),-2,0]; kw['head']=[-2*math.sin(c),0,0]
  sw=math.sin(c)
  kw['handR']=[-.34,-.06+.09*sw,-.35]; kw['handL']=[.33,-.02-.11*sw,-.36]
  if i==0: kw.update(elbowR=[-.4,.8,-.2],rdir=[-.1,-.5,-.86],rnrm=[1,0,0],throat=0,elbowL=[.4,.8,-.2],wristL=[0,0,0])
  kw['t']=t; ks.append(kw)
 return ks
WALK_KEYS=_walk_keys()
CLIPS={
 # between points: relaxed, racket down at the side, breathing and a slow weight shift (loops)
 'Idle':dict(loop=True,len=3.0,keys=[
  K(0,**STANCE,hips=[0,0,0],chest=[0,-3,0],head=[0,0,0],handR=[-.34,-.06,-.36],elbowR=[-.4,.8,-.2],rdir=[-.1,-.5,-.86],rnrm=[1,0,0],throat=0,handL=[.33,-.02,-.36],elbowL=[.4,.8,-.2],wristL=[0,0,0]),
  K(1.5,pelvis=[.025,0,-.04],hips=[4,0,-3],chest=[-4,1,2],head=[6,-3,0],handR=[-.35,-.04,-.37],handL=[.34,.0,-.35]),
  K(3.0,pelvis=[0,0,-.03],hips=[0,0,0],chest=[0,-3,0],head=[0,0,0],handR=[-.34,-.06,-.36],handL=[.33,-.02,-.36])]),
 # walk (serve walk-in / between points): relaxed walk, racket carried low at the side, planted feet
 # (stance slides linearly under the body), heel-to-toe roll, hip sway, counter-swinging arms. Loops.
 'Walk':dict(loop=True,len=WALK_T,keys=WALK_KEYS),
 # perfect hit flourish: racket held out in the finish, hop, left fist pump
 'HitPerfect':dict(len=1.1,keys=[
  K(0,**STANCE,hips=[20,0,0],chest=[15,0,0],head=[0,0,0],handR=[-.46,-.14,.06],elbowR=[-.9,.2,-.3],rdir=[-.45,-.2,.87],rnrm=[0,-1,0],throat=0,handL=[.2,-.3,-.15],wristL=[0,0,0]),
  K(.25,pelvis=[0,0,.10],footL=[.03,0,.12,-10],footR=[-.03,0,.12,10],handL=[.22,-.18,.18],elbowL=[.8,.2,-.4],wristL=[0,-40,0],head=[0,-12,0]),
  K(.45,pelvis=[0,0,-.06],footL=[.03,0,0,-10],footR=[-.03,0,0,10],handL=[.20,-.26,-.02],wristL=[0,-20,0],head=[0,4,0]),
  K(.6,handL=[.22,-.18,.16]),K(.75,handL=[.20,-.26,-.02]),
  K(1.1,pelvis=[0,0,-.03],hips=[0,0,0],chest=[0,0,0],head=[0,0,0])]),
 # whiff (Plan 1B): party miss. The swing carries on into an overswing wrap, momentum hops them onto the
 # front foot with the back leg kicking out, a double take back at the ball that got past, a sheepish
 # scratch of the head, then a shake-off into Ready. Elbows always bent, off-arm clear of the body.
 'MissWhiff':dict(len=2.0,keys=[
  K(0,**STANCE,spin=0,hips=[-30,0,0],chest=[-20,4,0],head=[10,0,0],handR=[-.46,-.12,-.02],elbowR=[-.7,.3,-.4],rdir=[-.6,-.75,.25],rnrm=[0,0,1],throat=0,handL=[.34,-.22,-.06],elbowL=[.7,.3,-.4],wristL=[0,0,0]),
  # overswing: racket whips across and wraps high over the left shoulder, torso over-rotates
  K(.22,spin=35,hips=[30,6,0],chest=[40,10,-6],head=[20,6,0],pelvis=[.02,-.04,-.05],footR=[-.03,.02,.03,40],toeR=30,
    handR=[.22,-.30,.22],elbowR=[-.4,-.7,.3],rdir=[.55,.45,.7],rnrm=[.5,-.7,.2],handL=[.40,.05,-.10],elbowL=[.8,.4,-.3]),
  # momentum: hop onto the front foot, back leg kicks out, arms flung out bent for balance
  K(.48,spin=55,hips=[35,14,8],chest=[35,16,12],head=[30,-4,10],pelvis=[.05,-.10,.02],footL=[.04,-.14,.06,-10],footR=[-.10,.26,.30,30],toeR=-20,
    handR=[-.10,-.20,.42],elbowR=[-.8,-.2,.5],rdir=[.2,.3,.93],rnrm=[1,0,0],handL=[.46,-.02,.14],elbowL=[.8,.2,.4],wristL=[0,-30,0]),
  # land wobbly, knees give
  K(.68,spin=45,hips=[25,10,-6],chest=[20,12,-8],head=[25,10,-8],pelvis=[.03,-.08,-.10],footL=[.04,-.14,0,-10],footR=[-.06,.14,0,25],toeR=0,
    handR=[-.46,-.26,.04],elbowR=[-.9,.1,-.2],rdir=[-.85,-.2,.48],rnrm=[0,-1,0],handL=[.42,-.12,-.02],elbowL=[.8,.3,-.3],wristL=[0,0,0]),
  # double take: whips round to look back at the ball that got by, shoulders up
  K(.92,spin=-10,hips=[-25,-4,0],chest=[-35,-6,0],head=[-55,-12,0],pelvis=[0,-.02,-.04],footL=[.04,-.05,0,-15],footR=[-.05,.05,0,20],
    handR=[-.40,-.10,-.02],elbowR=[-.8,.3,-.3],rdir=[-.3,-.3,-.9],rnrm=[1,0,0],handL=[.38,-.12,.02],elbowL=[.9,.1,-.2]),
  K(1.08,head=[-60,-14,4],chest=[-38,-8,0]),
  # sheepish: turns back, slumps, left hand scratches the back of the head, racket tip droops
  K(1.3,spin=0,hips=[0,0,0],chest=[0,16,0],head=[8,22,0],pelvis=[0,0,-.05],footL=[.03,0,0,-10],footR=[-.03,0,0,10],
    handR=[-.36,-.12,-.30],elbowR=[-.5,.7,-.3],rdir=[.15,-.55,-.82],rnrm=[1,0,0],handL=[.20,.10,.40],elbowL=[.95,-.1,.3],wristL=[0,-40,0]),
  K(1.45,head=[-6,22,4]),K(1.58,head=[8,20,-4]),
  # shake it off: straighten, shoulders drop, back to the ready stance (blend-out lands on Ready)
  K(2.0,pelvis=[0,0,-.03],hips=[0,0,0],chest=[0,0,0],head=[0,0,0],handR=[-.24,-.30,-.18],elbowR=[-.6,.3,-.6],rdir=[.35,-.85,.35],rnrm=[0,-1,0],handL=[.30,-.28,-.12],elbowL=[.6,.3,-.6],wristL=[0,0,0])]),
 # point won: racket raised overhead, jump, left fist pump
 'CelebratePoint':dict(len=1.5,keys=[
  K(0,**STANCE,hips=[0,0,0],chest=[0,0,0],head=[0,0,0],handR=[-.24,-.30,-.18],rdir=[.35,-.85,.35],rnrm=[0,-1,0],handL=[.24,-.28,-.16],wristL=[0,0,0]),
  K(.2,pelvis=[0,0,-.10],chest=[0,12,0],handR=[-.25,-.2,.10],rdir=[-.2,-.4,.9],throat=0,handL=[.18,-.25,-.05]),
  K(.45,pelvis=[0,0,.16],footL=[.05,0,.16,-10],footR=[-.05,0,.16,10],chest=[0,-10,0],head=[0,-18,0],handR=[-.22,-.05,.62],elbowR=[-.8,.2,.2],rdir=[-.05,-.1,1],rnrm=[0,-1,0],handL=[.25,-.15,.22],elbowL=[.8,.2,-.3],wristL=[0,-40,0]),
  K(.7,pelvis=[0,0,-.07],footL=[.03,0,0,-10],footR=[-.03,0,0,10],chest=[0,4,0],head=[0,-6,0],handL=[.22,-.25,.0]),
  K(.9,handL=[.25,-.15,.20]),K(1.05,handL=[.22,-.25,.0]),
  K(1.5,pelvis=[0,0,-.03],chest=[0,0,0],head=[0,0,0])]),
 # point lost: slump, racket tip drops to the court, head shake, sigh
 'SadPointLost':dict(len=1.7,keys=[
  K(0,**STANCE,hips=[0,0,0],chest=[0,0,0],head=[0,0,0],handR=[-.24,-.30,-.18],rdir=[.35,-.85,.35],rnrm=[0,-1,0],handL=[.24,-.28,-.16],wristL=[0,0,0]),
  K(.4,pelvis=[0,0,-.06],chest=[0,22,0],head=[0,22,0],handR=[-.33,-.14,-.40],elbowR=[-.3,.8,-.1],rdir=[.1,-.4,-.9],rnrm=[0,-1,0],throat=0,handL=[.22,.06,-.30],elbowL=[.7,.4,-.1],wristL=[0,30,0]),
  K(.7,head=[-15,22,0]),K(.95,head=[15,22,0]),K(1.2,head=[-8,20,0]),
  K(1.7,pelvis=[0,0,-.03],chest=[0,0,0],head=[0,0,0])]),
 # match won: two jumps, spin in the air, both arms up
 'MatchWin':dict(len=2.6,keys=[
  K(0,**STANCE,spin=0,hips=[0,0,0],chest=[0,0,0],head=[0,0,0],handR=[-.24,-.30,-.18],rdir=[.35,-.85,.35],rnrm=[0,-1,0],handL=[.24,-.28,-.16],wristL=[0,0,0]),
  K(.2,pelvis=[0,0,-.12],chest=[0,15,0],handR=[-.25,-.2,.05],rdir=[-.2,-.4,.9],throat=0,handL=[.25,-.2,.05]),
  K(.5,pelvis=[0,0,.20],footL=[.05,0,.2,-10],footR=[-.05,0,.2,10],chest=[0,-12,0],head=[0,-22,0],handR=[-.25,0,.62],elbowR=[-.8,.2,.2],rdir=[-.05,-.1,1],rnrm=[0,-1,0],handL=[.25,0,.60],elbowL=[.8,.2,.2],wristL=[0,-10,0]),
  K(.8,pelvis=[0,0,-.10],footL=[.03,0,0,-10],footR=[-.03,0,0,10],chest=[0,6,0]),
  K(1.2,spin=0,pelvis=[0,0,.24],footL=[.05,0,.24,-10],footR=[-.05,0,.24,10],head=[0,-20,0]),
  K(1.6,spin=360,pelvis=[0,0,-.08],footL=[.03,0,0,-10],footR=[-.03,0,0,10]),
  K(2.0,pelvis=[0,0,-.02],chest=[0,-6,0],head=[0,-14,0],handR=[-.28,0,.58],handL=[.28,-.05,.55]),
  K(2.6,pelvis=[0,0,-.03],chest=[0,0,0],head=[0,0,0])]),
 # match lost: deep slump, racket dragged, left hand to the forehead, slow head shake
 'MatchLose':dict(len=2.6,keys=[
  K(0,**STANCE,hips=[0,0,0],chest=[0,0,0],head=[0,0,0],handR=[-.24,-.30,-.18],rdir=[.35,-.85,.35],rnrm=[0,-1,0],handL=[.24,-.28,-.16],wristL=[0,0,0]),
  K(.6,pelvis=[0,0,-.10],chest=[0,28,0],head=[0,25,0],handR=[-.34,-.14,-.42],elbowR=[-.3,.8,-.1],rdir=[.1,-.4,-.9],rnrm=[0,-1,0],throat=0,handL=[.10,-.30,.30],elbowL=[.8,-.2,-.2],wristL=[0,-60,0]),
  K(1.1,head=[-14,25,0]),K(1.5,head=[14,25,0]),K(1.9,head=[-8,22,0]),
  K(2.6,pelvis=[0,0,-.05],chest=[0,15,0],head=[0,12,0],handR=[-.33,-.14,-.40],handL=[.24,.04,-.32],elbowL=[.7,.4,-.1],wristL=[0,20,0])]),
}
ready_frames,_=build2('Ready',dict(D['Ready'],**{'lock_force':1.0,'left_on':.17}))
READY0=(ready_frames[0][0],ready_frames[0][1])
report={};HR={}
ready_first=None
for name,c in CLIPS.items():
 if ONLYJ and name not in ONLYJ:continue
 met={};n=int(round(c['len']*30))+1;frames=[]
 for i in range(n):
  Q,dp=pose_at(c['keys'],i/30,met);frames.append((Q,dp,0))
 act=make_action('Hero_'+name+'_v5',frames,None if c.get('loop') else READY0,blend_in=.22,blend_out=.4,loop=bool(c.get('loop')));export(P/'fbx'/(act.name+'.fbx'),act)
 pa=met.get('pen_after_R',[0])+met.get('pen_after_L',[0])
 report[name]={'seconds':c['len'],'frames':n,'arm_torso_frames_gt1cm':sum(1 for x in pa if x>.01),'max':round(max(pa),3)}
 ref=[]
 for f in range(1,n+1):
  bpy.context.scene.frame_set(f);row=[]
  for sd in 'RL':
   pb=rig.pose.bones;h=pb['Hand.'+sd].head;fd=(pb['Middle1.'+sd].head-h).normalized();wd=(pb['Pinky1.'+sd].head-pb['Index1.'+sd].head).normalized()
   row+=[[-fd.x,fd.z,-fd.y],[-wd.x,wd.z,-wd.y]]
  ref.append(row)
 HR[name]=ref
 print('JUICE',name,report[name],flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_Juice_v5.blend'))
(P/'juice_report.json').write_text(json.dumps(report,indent=1));(P/'hand_ref_juice.json').write_text(json.dumps(HR))
