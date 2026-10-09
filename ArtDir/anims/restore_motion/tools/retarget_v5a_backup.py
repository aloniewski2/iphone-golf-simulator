# RestoreMotion retarget: Eyes Japan C3D (markers + tracked racket) -> locked V4 hero (finger rig).
# Body: marker frames. Arms: aim + elbow-hinge twist. Right hand: solved from the REAL racket orientation through the
# shared grip socket, forearm takes 60% of the roll. 2HBH/Ready: left hand locked onto the handle above the right hand.
import bpy,sys,json,math,os
sys.path.insert(0,os.path.dirname(__file__))
from common import *
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
P=Path(__file__).resolve().parents[1]
GRIP=json.loads((P/'tools/grip.json').read_text())
D=json.loads((P/'tools/markers_v2.json').read_text())
ONLY=sys.argv[sys.argv.index('--')+1].split(',') if '--' in sys.argv else list(D)
bpy.ops.wm.open_mainfile(filepath=str(P/'Hero_01_FingerRig.blend'))
rig=bpy.data.objects['Hero_01_Rig'];body=bpy.data.objects['Body_Skin'];scene=bpy.context.scene;scene.render.fps=30
rig.animation_data_clear();rig.animation_data_create()
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
B={b.name:b for b in rig.data.bones}
REST={n:b.matrix_local.to_3x3() for n,b in B.items()}
RQ={n:m.to_quaternion() for n,m in REST.items()}
HEAD={n:b.head_local.copy() for n,b in B.items()}
ORDER=[b.name for b in rig.data.bones]  # parents before children
PARENT={n:(b.parent.name if b.parent else None) for n,b in B.items()}
OFF={n:(REST[PARENT[n]].inverted()@(HEAD[n]-HEAD[PARENT[n]]) if PARENT[n] else HEAD[n]) for n in B}
S=socket_matrix(GRIP);SR=S.to_3x3();ST=S.translation
def V(a):return Vector(a)
def nz(v):
 v=Vector(v);return v.normalized() if v.length>1e-9 else v
def basis(a,b):
 a=nz(a);b=nz(b-a*b.dot(a));return Matrix((a,b,a.cross(b))).transposed()
def mrot(m):
 return m.to_quaternion()
def slerp(a,b,t):return a.slerp(b,t)
def smooth(seq,n):
 # centred moving average for Vector sequences
 h=n//2;out=[]
 for i in range(len(seq)):
  js=range(max(0,i-h),min(len(seq),i+h+1));v=Vector((0,0,0))
  for j in js:v+=seq[j]
  out.append(v/len(js))
 return out
def qsmooth(seq,n):
 h=n//2;out=[]
 for i in range(len(seq)):
  acc=Vector((0,0,0,0));ref=seq[i]
  for j in range(max(0,i-h),min(len(seq),i+h+1)):
   q=seq[j]
   if q.dot(ref)<0:q=-q
   acc+=Vector(q)
  out.append(Quaternion(acc).normalized())
 return out
def fk(Q,hips_head):
 pos={}
 for n in ORDER:
  p=PARENT[n]
  if n not in Q:Q[n]=(Q[p]@RQ[p].inverted()@RQ[n]) if p else RQ[n]
  pos[n]=(hips_head if n=='Hips' else HEAD[n]) if (p is None or n=='Hips') else pos[p]+Q[p]@(OFF[n])
  if n=='Hips':pos[n]=hips_head
 return pos
def tail(Q,pos,n):return pos[n]+Q[n]@Vector((0,B[n].length,0))
def two_bone(root,target,l1,l2,pole):
 d=target-root;L=min(d.length,l1+l2-1e-3);L=max(L,abs(l1-l2)+1e-3);dn=d.normalized()
 a=(l1*l1-l2*l2+L*L)/(2*L);h=math.sqrt(max(0,l1*l1-a*a))
 side=pole-dn*pole.dot(dn)
 if side.length<1e-6:side=Vector((0,-1,0))-dn*dn.y*-1
 mid=root+dn*a+side.normalized()*h;end=root+dn*L
 return mid,end
def limb(Q,upper,lower,u,f,fwd,prevh):
 u0=REST[upper]@Vector((0,1,0));f0=REST[lower]@Vector((0,1,0));h0=nz(u0.cross(fwd))
 u=nz(u);f=nz(f);hm=u.cross(f);bend=math.degrees(u.angle(f)) if hm.length>1e-6 else 0
 w=max(0,min(1,(bend-8)/25));w=w*w*(3-2*w)
 hp=prevh if prevh is not None else (Quaternion(u0.rotation_difference(u))@h0)
 hp=nz(hp-u*hp.dot(u));h=nz((nz(hm) if hm.length>1e-6 else hp)*w+hp*(1-w))
 if hm.length>1e-6 and h.dot(nz(hm))<0 and w>.5:h=nz(hm)
 Ru=basis(u,h)@basis(u0,h0).transposed();Rf=basis(f,h)@basis(f0,h0).transposed()
 Q[upper]=mrot(Ru)@RQ[upper];Q[lower]=mrot(Rf)@RQ[lower];return h
def swing_twist(q,axis):
 p=Vector((q.x,q.y,q.z));proj=axis*p.dot(axis);tw=Quaternion((q.w,proj.x,proj.y,proj.z))
 if tw.magnitude<1e-9:return Quaternion(),q
 tw.normalize();return q@tw.inverted(),tw
def frame_from(x,yback):
 x=nz(x);yb=nz(yback-x*yback.dot(x));z=x.cross(yb);return Matrix((x,yb,z)).transposed()
def mk(m,i,n):return Vector(m[n][i])
def pelvis(m,i):return (mk(m,i,'RFWT')+mk(m,i,'RBWT')+mk(m,i,'LFWT')+mk(m,i,'LBWT'))/4
def hipsF(m,i):return frame_from((mk(m,i,'LFWT')+mk(m,i,'LBWT'))-(mk(m,i,'RFWT')+mk(m,i,'RBWT')),(mk(m,i,'LBWT')+mk(m,i,'RBWT'))-(mk(m,i,'LFWT')+mk(m,i,'RFWT')))
def chestF(m,i):
 back=mk(m,i,'C7')-mk(m,i,'CLAV') if 'C7' in m and 'CLAV' in m else (mk(m,i,'LSHO')+mk(m,i,'RSHO'))/2-pelvis(m,i)
 return frame_from(mk(m,i,'LSHO')-mk(m,i,'RSHO'),back)
def headF(m,i):return frame_from((mk(m,i,'LFHD')+mk(m,i,'LBHD'))-(mk(m,i,'RFHD')+mk(m,i,'RBHD')),(mk(m,i,'LBHD')+mk(m,i,'RBHD'))-(mk(m,i,'LFHD')+mk(m,i,'RFHD')))
def handF(m,i,s):
 W=(mk(m,i,s+'WRA')+mk(m,i,s+'WRB'))/2;y=nz(mk(m,i,s+'FIN')-W);z=nz(mk(m,i,s+'WRB')-mk(m,i,s+'WRA'));z=nz(z-y*z.dot(y))
 return Matrix((y.cross(z),y,z)).transposed()
LEAN=.5
TWIST=1.0
def frame_xz(x,up):
 x=nz(x);z=nz(up-x*up.dot(x));return Matrix((x,z.cross(x),z)).transposed()
def tiltscale(q,k):
 sw,tw=swing_twist(q,Vector((0,0,1)));return Quaternion().slerp(sw,k)@tw
# Reference calibration (upright neutral) from Ready clip average
RM={k:v for k,v in D['Ready']['markers'].items()};NR=len(RM['RFWT'])
def avgF(fn):
 acc=Vector((0,0,0,0));q0=None
 for i in range(NR):
  q=fn(RM,i).to_quaternion()
  if q0 is None:q0=q
  if q.dot(q0)<0:q=-q
  acc+=Vector(q)
 return Quaternion(acc).normalized()
# Yaw-free references: keep only tilt offsets (marker placement), not heading.
def untilt(ref):
 f=ref@Vector((0,1,0));yaw=math.atan2(-f.x,f.y);return Quaternion((0,0,1),yaw).inverted()@ref
REF={'hips':untilt(avgF(hipsF)),'chest':untilt(avgF(chestF)),'head':untilt(avgF(headF))}
# Source dimensions
def src_leg(m,i,s):
 hj=(mk(m,i,s+'FWT')+mk(m,i,s+'BWT'))/2+Vector((0,0,-.09));return (hj-mk(m,i,s+'KNE')).length+(mk(m,i,s+'KNE')-mk(m,i,s+'ANK')).length
SRC_LEG=sum(src_leg(RM,i,s) for i in range(NR) for s in 'RL')/(2*NR)
HERO_LEG=(HEAD['UpperLeg.R']-HEAD['Foot.R']).length
SC=HERO_LEG/SRC_LEG
print('SCALE',SC,SRC_LEG,HERO_LEG)
LEN={n:B[n].length for n in B}
GRIPQ={'R':GRIP}
GL=dict(GRIP);GL['c1']=GRIP['c1']*.9
def left_socket():
 # Mirror of the right socket for the left hand; racket x/z reflected so the frame stays proper.
 HR=B['Hand.R'].matrix_local;HL=B['Hand.L'].matrix_local;M4=MR.to_4x4()
 m=HL.inverted()@M4@HR@S@Matrix.Diagonal((-1,1,1,1));return m
SL=left_socket();SLR=SL.to_3x3();SLT=SL.translation
def build(name,clip,flips=None,roll=0.0):
 SRc=SR@Matrix.Rotation(math.radians(roll),3,'Y')
 m=clip['markers'];Rk=[Matrix(r) if r else None for r in clip['racket']];N=len(m['RFWT'])
 # fill missing racket frames by hold
 last=None
 for i in range(N):
  if Rk[i] is None:Rk[i]=last
  else:last=Rk[i]
 for i in range(N-1,-1,-1):
  if Rk[i] is None:Rk[i]=Rk[i+1] if i+1<N else None
 pel0=pelvis(m,0);ank0={s:mk(m,0,s+'ANK') for s in 'RL'}
 hj0={s:(mk(m,0,s+'FWT')+mk(m,0,s+'BWT'))/2+Vector((0,0,-.09)) for s in 'RL'}
 bias={s:Vector((HEAD['UpperLeg.'+s].x-SC*(hj0[s]-pel0).x,0,0)) for s in 'RL'}
 frames=[];prevh={};prevRk=None
 hipsQ=[];chestQ=[];headQ=[];pel=[]
 for i in range(N):
  pv=pelvis(m,i);sh=(mk(m,i,'LSHO')+mk(m,i,'RSHO'))/2
  hipsQ.append(frame_xz((mk(m,i,'LFWT')+mk(m,i,'LBWT'))-(mk(m,i,'RFWT')+mk(m,i,'RBWT')),Vector((0,0,1))).to_quaternion())
  chestQ.append(tiltscale(frame_xz(mk(m,i,'LSHO')-mk(m,i,'RSHO'),sh-pv).to_quaternion(),clip.get('lean',LEAN)))
  headQ.append(tiltscale(headF(m,i).to_quaternion()@REF['head'].inverted(),clip.get('headlean',.2)))
  pel.append(pv)
 hipsQ=qsmooth(hipsQ,3);chestQ=qsmooth(chestQ,3);headQ=qsmooth(headQ,5)
 # Lower body placement: scaled leg shape, ankles on the ground, slow travel removed (steps kept).
 ground={s:sorted(m[s+'ANK'][k][2] for k in range(N))[max(0,N//20)] for s in 'RL'}
 HJ=lambda i,s:(mk(m,i,s+'FWT')+mk(m,i,s+'BWT'))/2+Vector((0,0,-.09))
 ANK={s:[] for s in 'RL'};HH=[]
 hipoff=HEAD['Hips']-(HEAD['UpperLeg.R']+HEAD['UpperLeg.L'])/2
 for i in range(N):
  est=[]
  for s in 'RL':
   a=(mk(m,i,s+'ANK')-pel0)*SC+bias[s]+Vector((0,HEAD['Foot.'+s].y,0));a.z=HEAD['Foot.'+s].z+SC*(mk(m,i,s+'ANK').z-ground[s]);a.z=max(a.z,HEAD['Foot.'+s].z-.005)
   ANK[s].append(a);est.append(a+SC*(HJ(i,s)-mk(m,i,s+'ANK')))
  HH.append((est[0]+est[1])/2+hipsQ[i]@hipoff)
 sig=clip.get('drift_sigma',12);drift=[]
 for i in range(N):
  wsum=0;acc=Vector((0,0,0))
  for j in range(N):
   w=math.exp(-((i-j)/sig)**2/2);acc+=w*Vector((HH[j].x,HH[j].y,0));wsum+=w
  drift.append(acc/wsum-Vector((HH[0].x,HH[0].y,0))*0-Vector((HEAD['Hips'].x,HEAD['Hips'].y,0)))
 keep=clip.get('keep_travel',.15)
 for i in range(N):
  d=drift[i]*(1-keep);HH[i]=HH[i]-d
  for s in 'RL':ANK[s][i]=ANK[s][i]-d
 metrics={'wrist_swing_max_deg':0,'left_lock_frames':0,'foot_reach_clamped':0}
 for i in range(N):
  Q={}
  Q['Root']=RQ['Root']
  hq=hipsQ[i];cq=chestQ[i];dq=headQ[i]
  Q['Hips']=hq@RQ['Hips'];Q['Spine']=hq.slerp(cq,.45)@RQ['Spine'];Q['Chest']=cq@RQ['Chest']
  Q['Neck']=cq.slerp(dq,.5)@RQ['Neck'];Q['Head']=cq.slerp(dq,.6)@RQ['Head']
  for s in 'LR':Q['Shoulder.'+s]=cq@RQ['Shoulder.'+s]
  hh=HH[i]
  pos=fk(Q,hh)
  # ---- legs
  for s in 'LR':
   up,lo='UpperLeg.'+s,'LowerLeg.'+s
   ank=ANK[s][i]
   root=pos[up];knee_src=mk(m,i,s+'KNE');pole=nz((knee_src-(mk(m,i,s+'ANK')+hj0[s])/2)+Vector((0,-.05,0)))
   if (ank-root).length>LEN[up]+LEN[lo]-1e-3:metrics['foot_reach_clamped']+=1
   mid,end=two_bone(root,ank,LEN[up],LEN[lo],pole)
   prevh[s+'leg']=limb(Q,up,lo,mid-root,end-mid,Vector((0,1,0)),prevh.get(s+'leg'))
   # foot: heading from heel->toe, pitch relative to first frame
   ht=mk(m,i,s+'TOE')-mk(m,i,s+'HEE');ht0=mk(m,0,s+'TOE')-mk(m,0,s+'HEE')
   yaw=math.atan2(ht.x,-ht.y)-math.atan2(ht0.x,-ht0.y)*0
   pitch=math.atan2(ht.z,ht.xy.length)-math.atan2(ht0.z,ht0.xy.length)
   yaw=max(-.9,min(.9,yaw));pitch=max(-.5,min(.6,pitch))
   Rf=Quaternion((0,0,1),yaw)@Quaternion((1,0,0),-pitch)
   Q['Foot.'+s]=Rf@RQ['Foot.'+s];Q['Toes.'+s]=Quaternion((0,0,1),yaw)@RQ['Toes.'+s]
  pos=fk(Q,hh)
  # ---- arms from marker directions
  for s,fwd in [('L',Vector((0,-1,0))),('R',Vector((0,-1,0)))]:
   W=(mk(m,i,s+'WRA')+mk(m,i,s+'WRB'))/2
   prevh[s+'arm']=limb(Q,'UpperArm.'+s,'LowerArm.'+s,mk(m,i,s+'ELB')-mk(m,i,s+'SHO'),W-mk(m,i,s+'ELB'),fwd,prevh.get(s+'arm'))
  # ---- right hand from the tracked racket
  R=Rk[i].copy()
  # choose the racket roll (180 deg about racket Y is ambiguous in a planar marker fit) that keeps the wrist most neutral
  # Party-readability face assist: ease the string bed toward the net over +/-8 frames of contact (<=35 deg, solved by the wrist).
  cfr=clip.get('contact_s');fa=0.0
  if cfr:
   u=max(0,1-abs(i-cfr*30)/8);fa=u*u*(3-2*u)
  if fa>0:
   n=(R@Vector((0,0,1))).normalized();tgt=Vector((0,-1,0)) if n.y<0 else Vector((0,1,0))
   rd=n.rotation_difference(tgt);ang=rd.angle
   if ang>1e-4:
    k=min(.85*ang,math.radians(35))/ang*fa;R=Quaternion().slerp(rd,k).to_matrix()@R
   metrics.setdefault('assist_deg',[]).append(math.degrees(min(.85*ang,math.radians(35))*fa))
  cands=[]
  for flip in (Matrix.Identity(3),Matrix.Diagonal((-1,1,-1))):
   Rc=R@flip;Hq=(Rc@SRc.inverted()).to_quaternion()
   neutral=Q['LowerArm.R']@RQ['LowerArm.R'].inverted()@RQ['Hand.R']
   sw,_=swing_twist(neutral.inverted()@Hq,Vector((0,1,0)));a_=sw.angle;a_=min(a_,2*math.pi-a_)
   cands.append((a_,Rc,Hq))
  metrics.setdefault('flipcost',[]).append([c[0] for c in cands])
  k=flips[i] if flips is not None else (0 if cands[0][0]<=cands[1][0] else 1)
  best=cands[k]
  _,R,Hq=best;prevRk=R
  neutral=Q['LowerArm.R']@RQ['LowerArm.R'].inverted()@RQ['Hand.R']
  delta=neutral.inverted()@Hq;sw,tw=swing_twist(delta,Vector((0,1,0)))
  twa=2*math.acos(max(-1,min(1,tw.w)))
  if twa>math.pi:tw=-tw
  Q['LowerArm.R']=Q['LowerArm.R']@(Quaternion().slerp(tw,TWIST))
  Q['Hand.R']=Hq
  nn=Q['LowerArm.R']@RQ['LowerArm.R'].inverted()@RQ['Hand.R'];yv=(nn.inverted()@Hq)@Vector((0,1,0))
  metrics.setdefault('flex',[]).append(math.degrees(math.atan2(yv.x,yv.y)));metrics.setdefault('dev',[]).append(math.degrees(math.atan2(yv.z,yv.y)))
  n2=Q['LowerArm.R']@RQ['LowerArm.R'].inverted()@RQ['Hand.R'];a_=n2.rotation_difference(Hq).angle;metrics['wrist_swing_max_deg']=max(metrics['wrist_swing_max_deg'],math.degrees(min(a_,2*math.pi-a_)))
  # ---- left hand
  pos=fk(Q,hh)
  nL=Q['LowerArm.L']@RQ['LowerArm.L'].inverted()@RQ['Hand.L']
  Fh=handF(m,i,'L');Qh=Fh.to_quaternion();Qf=(Fh@Matrix.Diagonal((-1,1,-1))).to_quaternion()
  if nL.rotation_difference(Qf).angle<nL.rotation_difference(Qh).angle:Qh=Qf
  d=nL.inverted()@Qh;sw,tw=swing_twist(d,Vector((0,1,0)))
  Q['LowerArm.L']=Q['LowerArm.L']@(Quaternion().slerp(tw,.5));Q['Hand.L']=nL.slerp(Qh,.6)
  # lock onto the handle when the source hands are together (2HBH, ready throat support)
  lw=(mk(m,i,'LWRA')+mk(m,i,'LWRB'))/2;rw=(mk(m,i,'RWRA')+mk(m,i,'RWRB'))/2;gap=(lw-rw).length
  lock=max(0,min(1,(clip.get('lock_far',.20)-gap)/.06));lock=lock*lock*(3-2*lock)
  if clip.get('lock_force') is not None:lock=clip['lock_force']
  if lock>0:
   metrics['left_lock_frames']+=lock>.99
   pos=fk(Q,hh)
   racketW=Matrix.LocRotScale(pos['Hand.R'],Q['Hand.R'],Vector((1,1,1)))@Matrix.Translation(ST)@SRc.to_4x4()  # racket world (bone head space)
   # left hand bone orientation/position from mirrored socket placed further up the handle
   up=clip.get('left_on',.135)-GRIP['grip_y']
   target=racketW@Matrix.Translation((0,up,0))@SL.inverted()
   HqL=target.to_quaternion();hand_head=target.translation
   root=pos['UpperArm.L'];pole=nz(mk(m,i,'LELB')-(mk(m,i,'LSHO')+lw)/2)
   mid,end=two_bone(root,hand_head,LEN['UpperArm.L'],LEN['LowerArm.L'],pole)
   Qik=dict(Q);prevh['Lik']=limb(Qik,'UpperArm.L','LowerArm.L',mid-root,end-mid,Vector((0,-1,0)),prevh.get('Lik'))
   for n in ['UpperArm.L','LowerArm.L']:Q[n]=Q[n].slerp(Qik[n],lock)
   nL=Q['LowerArm.L']@RQ['LowerArm.L'].inverted()@RQ['Hand.L'];d=nL.inverted()@HqL;sw,tw=swing_twist(d,Vector((0,1,0)))
   Q['LowerArm.L']=Q['LowerArm.L']@(Quaternion().slerp(tw,.5*lock));Q['Hand.L']=Q['Hand.L'].slerp(HqL,lock)
  frames.append((Q,hh-HEAD['Hips'],lock))
 return frames,metrics
def solve_flips(cost,penalty=1.2):
 N=len(cost);acc=[list(cost[0])];back=[]
 for i in range(1,N):
  row=[];bk=[]
  for k in (0,1):
   opts=[acc[-1][j]+(penalty if j!=k else 0) for j in (0,1)];j=min((0,1),key=lambda j:opts[j]);row.append(opts[j]+cost[i][k]);bk.append(j)
  acc.append(row);back.append(bk)
 k=min((0,1),key=lambda k:acc[-1][k]);path=[k]
 for bk in reversed(back):k=bk[k];path.append(k)
 return path[::-1]
def grip_consistent_flips(clip):
 # Racket is rigid in the real hand: choose each frame's planar-fit roll so racket-in-hand matches the clip median.
 m=clip['markers'];N=len(m['RFWT']);F=Matrix.Diagonal((-1,1,-1))
 last=None;Rk=[]
 for r in clip['racket']:
  if r:last=Matrix(r)
  Rk.append(last)
 first=next(r for r in Rk if r is not None);Rk=[r if r is not None else first for r in Rk]
 rel=[(handF(m,i,'R').transposed()@Rk[i],handF(m,i,'R').transposed()@Rk[i]@F) for i in range(N)]
 ref=rel[0][0];ch=[0]*N
 for it in range(4):
  ch=[0 if (ref.to_quaternion().rotation_difference(r0.to_quaternion()).angle<=ref.to_quaternion().rotation_difference(r1.to_quaternion()).angle) else 1 for r0,r1 in rel]
  acc=Vector((0,0,0,0));q0=None
  for (r0,r1),c in zip(rel,ch):
   q=(r0 if c==0 else r1).to_quaternion()
   if q0 is None:q0=q
   if q.dot(q0)<0:q=-q
   acc+=Vector(q)
  ref=Quaternion(acc).normalized().to_matrix()
 spread=sorted(math.degrees(ref.to_quaternion().rotation_difference((r0 if c==0 else r1).to_quaternion()).angle) for (r0,r1),c in zip(rel,ch))
 return ch,spread[len(spread)//2]
def build2(name,clip):
 ch,spread=grip_consistent_flips(clip)
 best=None
 for g in (0,1):
  fl=[c^g for c in ch];f2,m2=build(name,clip,fl)
  sw=sorted(max(abs(a),abs(b)) for a,b in zip(m2['flex'],m2['dev']));score=sw[len(sw)//2]
  if best is None or score<best[0]:best=(score,f2,m2,fl)
 _,f2,m2,fl=best
 # per-stroke grip roll about the handle axis (eastern vs continental); handle stays in the same finger cradle
 med=sorted(m2['flex'])[len(m2['flex'])//2];roll=0.0
 target=25 if name=='Forehand' else 0
 if abs(med-target)>20:
  opts=[]
  for r in (med-target,-(med-target)):
   f3,m3=build(name,clip,fl,r);opts.append((abs(sorted(m3['flex'])[len(m3['flex'])//2]-target),r,f3,m3))
  _,roll,f2,m2=min(opts,key=lambda o:o[0])
 m2['grip_roll_deg']=round(roll,1);m2['flip_switches']=sum(1 for a,b in zip(fl,fl[1:]) if a!=b);m2['grip_spread_med_deg']=round(spread,1);m2.pop('flipcost',None);return f2,m2
def make_action(name,frames,ready=None,blend_in=.2,blend_out=.35,loop=False):
 act=bpy.data.actions.new(name);act.use_fake_user=True;rig.animation_data.action=act;N=len(frames)
 prev={}
 for i,(Q,dp,lock) in enumerate(frames):
  t=i/30;T=(N-1)/30
  w=1.0
  if ready is not None and not loop:
   w=min(1,t/blend_in if blend_in>0 else 1,(T-t)/blend_out if blend_out>0 else 1);w=max(0,w);w=w*w*(3-2*w)
  for n in ORDER:
   q=Q.get(n,None)
   if q is None:continue
   if ready is not None and w<1:q=ready[0][n].slerp(q,w)
   p=PARENT[n];pq=(Q[p] if ready is None or w>=1 else ready[0][p].slerp(Q[p],w)) if p else Quaternion()
   local=(RQ[p].inverted()@RQ[n]) if p else RQ[n]
   lq=local.inverted()@(pq.inverted()@q if p else q)
   pb=rig.pose.bones[n];pb.rotation_mode='QUATERNION'
   if n in prev and prev[n].dot(lq)<0:lq=-lq
   prev[n]=lq;pb.rotation_quaternion=lq
   pb.keyframe_insert('rotation_quaternion',frame=i+1,group=n)
  d=dp if (ready is None or w>=1) else ready[1].lerp(dp,w)
  hb=rig.pose.bones['Hips'];hb.location=RQ['Hips'].inverted()@d;hb.keyframe_insert('location',frame=i+1,group='Hips')
 return act
def export(path,act):
 rig.animation_data.action=act;scene.frame_start=1;scene.frame_end=int(act.frame_range[1]);scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);body.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y',path_mode='AUTO',embed_textures=False)
CFG={'Ready':{'lock_far':.30,'left_on':.17},'Forehand':{'lock_far':.0},'Backhand':{'lock_far':.24,'left_on':.135},'Serve':{'lock_far':.0},'Volley':{'lock_far':.0},'Smash':{'lock_far':.0}}
out={};OUT=P/'fbx';OUT.mkdir(exist_ok=True);report={}
ready_frames,_=build2('Ready',dict(D['Ready'],**CFG['Ready']))
ready0=(ready_frames[0][0],ready_frames[0][1])
for name in ONLY:
 clip=dict(D[name],**CFG[name]);frames,met=build2(name,clip) if name!='Ready' else (ready_frames,_)
 if name=='Ready':
  # loop: distribute endpoint error over the cycle
  frames=frames[:66];N=len(frames);fixed=[]
  for i,(Q,dp,l) in enumerate(frames):
   u=i/(N-1);Q2={}
   for n,q in Q.items():
    corr=frames[0][0][n]@frames[-1][0][n].inverted();Q2[n]=Quaternion().slerp(corr,u)@q
   fixed.append((Q2,dp-(frames[-1][1]-frames[0][1])*u,l))
  act=make_action('Hero_Ready_v5',fixed,None,loop=True)
 else:
  act=make_action('Hero_'+name+'_v5',frames,ready0)
 export(OUT/(act.name+'.fbx'),act)
 met['face_assist_max_deg']=round(max(met.get('assist_deg',[0])),1);met.pop('assist_deg',None)
 bad=[i for i,(a,b) in enumerate(zip(met['flex'],met['dev'])) if abs(a)>80 or abs(b)>45]
 met['wrist_outlier_frames']=str(bad[:40])+(' n=%d'%len(bad))
 for k in ('flex','dev'):
  v=sorted(met.get(k,[0]));met[k]='p5 %.0f p50 %.0f p95 %.0f'%(v[len(v)//20],v[len(v)//2],v[-1-len(v)//20])
 report[name]=dict(met,frames=len(frames),seconds=round((len(frames)-1)/30,3),contact_s=D[name]['contact_s'])
 print('CLIP',name,report[name],flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_RestoreMotion_v5.blend'))
(P/'retarget_report.json').write_text(json.dumps({'scale':SC,'clips':report},indent=1))
print('RETARGET_DONE')
