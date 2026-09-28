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
def qang(a,b):
 return 2*math.acos(min(1.0,abs(a.dot(b))))
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
def hinge_of(Q,upper):
 u0=REST[upper]@Vector((0,1,0));h0=nz(u0.cross(Vector((0,-1,0))))
 return (Q[upper]@RQ[upper].inverted())@h0
def wrist_cost(Q,s,Hq):
 n=Q['LowerArm.'+s]@RQ['LowerArm.'+s].inverted()@RQ['Hand.'+s];d=n.inverted()@Hq
 if d.w<0:d=-d
 sw,tw=swing_twist(d,Vector((0,1,0)))
 if tw.w<0:tw=-tw
 twa=math.degrees(2*math.acos(min(1,tw.w)))
 y=sw@Vector((0,1,0));fl=abs(math.degrees(math.atan2(y.x,y.y)));dv=abs(math.degrees(math.atan2(y.z,y.y)))
 return max(0,twa-85)/30+max(0,fl-70)/30+max(0,dv-35)/15+ (fl+dv+twa)/600
def set_hand(Q,s,Hq,tw_frac,max_swing):
 # Forearm takes the roll (humanoid forearm twist), hand keeps flex/deviation, clamped to a human wrist range.
 n=Q['LowerArm.'+s]@RQ['LowerArm.'+s].inverted()@RQ['Hand.'+s]
 d=n.inverted()@Hq
 if d.w<0:d=-d
 sw,tw=swing_twist(d,Vector((0,1,0)))
 if tw.w<0:tw=-tw
 twa=2*math.acos(min(1,tw.w))
 if s=='L' and twa>math.radians(95):tw=Quaternion().slerp(tw,math.radians(95)/twa)
 Q['LowerArm.'+s]=Q['LowerArm.'+s]@Quaternion().slerp(tw,tw_frac)
 n=Q['LowerArm.'+s]@RQ['LowerArm.'+s].inverted()@RQ['Hand.'+s];d=n.inverted()@Hq
 if d.w<0:d=-d
 a=2*math.acos(min(1,d.w))
 if a>max_swing:d=Quaternion().slerp(d,max_swing/a)
 y=d@Vector((0,1,0));fl=math.atan2(y.x,y.y);dv=math.atan2(y.z,y.y)
 flc=max(-math.radians(78),min(math.radians(78),fl));dvc=max(-math.radians(40),min(math.radians(40),dv))
 if s=='L' and (abs(flc-fl)>1e-4 or abs(dvc-dv)>1e-4):
  yc=Vector((math.sin(flc)*math.cos(dvc),math.cos(flc)*math.cos(dvc),math.sin(dvc))).normalized()
  d=y.rotation_difference(yc)@d
 Q['Hand.'+s]=n@d
 return twa
VOL=json.loads((P/'tools/body_volume.json').read_text())
SL_=VOL['slices']
def torso_shape(z):
 if z<SL_[0]['z'] or z>SL_[-1]['z']:return None
 for a_,b_ in zip(SL_,SL_[1:]):
  if a_['z']<=z<=b_['z']:
   t=(z-a_['z'])/(b_['z']-a_['z']);f=lambda k:a_[k]*(1-t)+b_[k]*t
   return f('a'),(f('ymin')+f('ymax'))/2,(f('ymax')-f('ymin'))/2
 return None
def to_rest(Q,pos,p):
 for bn,zlim in (('Chest',1.03),('Spine',.87),('Hips',-9)):
  r=HEAD[bn]+RQ[bn]@(Q[bn].inverted()@(p-pos[bn]))
  if r.z>=zlim:return r
 return r
def pen(Q,pos,p,rad):
 r=to_rest(Q,pos,p);sh=torso_shape(r.z)
 if sh is None:return 0.0
 a,cy,b=sh;x=r.x;y=r.y-cy;s_=math.sqrt((x/a)**2+(y/b)**2)
 if s_<1e-6:return rad+min(a,b)
 d_out=(s_-1)*math.sqrt(x*x+y*y)/s_
 return max(0.0,rad+.005-d_out)
def arm_cost(Q,pos,S,E,W,hdir):
 c=0.0
 for t in (.8,1.0):c+=pen(Q,pos,S.lerp(E,t),VOL['UpperArm']*.85)
 for t in (0,.25,.5,.75,1.0):c+=pen(Q,pos,E.lerp(W,t),VOL['LowerArm'])
 c+=pen(Q,pos,W+hdir*.07,VOL['Hand']*.9)
 return c
def rot_about(p,o,axis,ang):return o+Quaternion(axis,ang)@(p-o)
def clear_arm(Q,hh,side,allow_move,metrics):
 pos=fk(Q,hh);S=pos['UpperArm.'+side];E=pos['LowerArm.'+side];W=pos['Hand.'+side];hdir=Q['Hand.'+side]@Vector((0,1,0));Hq=Q['Hand.'+side]
 c0=arm_cost(Q,pos,S,E,W,hdir)
 metrics.setdefault('pen_before_'+side,[]).append(c0)
 if c0<.003:
  metrics.setdefault('pen_after_'+side,[]).append(c0);return
 best=(c0,E,W,0)
 moves=[0]+([math.radians(a) for a in range(5,65,5)] if allow_move else [])
 ax_sw=(W-S).normalized()
 for mv in moves:
  if mv:
   ctr=to_rest(Q,pos,W);sh=torso_shape(ctr.z);cy=sh[1] if sh else 0.05
   centre=pos['Chest']+Q['Chest']@(RQ['Chest'].inverted()@Vector((0,cy-HEAD['Chest'].y,0)))
   out=(W-Vector((centre.x,centre.y,W.z)));out.z=0
   if out.length<1e-6:continue
   ax=(W-S).cross(out.normalized())
   if ax.length<1e-6:continue
   ax.normalize();W2=rot_about(W,S,ax,mv);E2=rot_about(E,S,ax,mv);h2=Quaternion(ax,mv)@hdir*0+hdir
  else:W2,E2,h2=W,E,hdir
  axs=(W2-S).normalized()
  for sw in range(-100,105,5):
   E3=rot_about(E2,S,axs,math.radians(sw))
   c=arm_cost(Q,pos,S,E3,W2,h2)+abs(sw)*.00004+mv*.004
   if c<best[0]-1e-6:best=(c,E3,W2,sw)
  if best[0]<.003+mv*.004:break
 _,E,W,_=best
 hp=hinge_of(Q,'UpperArm.'+side)
 limb(Q,'UpperArm.'+side,'LowerArm.'+side,E-S,W-E,Vector((0,-1,0)),hp)
 set_hand(Q,side,Hq,1.0,math.pi)
 pos=fk(Q,hh);metrics.setdefault('pen_after_'+side,[]).append(arm_cost(Q,pos,pos['UpperArm.'+side],pos['LowerArm.'+side],pos['Hand.'+side],Q['Hand.'+side]@Vector((0,1,0))))
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
  if not os.environ.get('NO_CLEAR_R'):clear_arm(Q,hh,'R',True,metrics)
  metrics.setdefault('flex',[]).append(math.degrees(math.atan2(yv.x,yv.y)));metrics.setdefault('dev',[]).append(math.degrees(math.atan2(yv.z,yv.y)))
  n2=Q['LowerArm.R']@RQ['LowerArm.R'].inverted()@RQ['Hand.R'];a_=n2.rotation_difference(Hq).angle;metrics['wrist_swing_max_deg']=max(metrics['wrist_swing_max_deg'],math.degrees(min(a_,2*math.pi-a_)))
  # ---- left hand
  pos=fk(Q,hh)
  nL=Q['LowerArm.L']@RQ['LowerArm.L'].inverted()@RQ['Hand.L']
  Fh=handF(m,i,'L')
  if clip.get('left_swap'):Fh=Fh@Matrix.Diagonal((-1,1,-1))
  Qh=Fh.to_quaternion()
  metrics.setdefault('ltw',[]).append(set_hand(Q,'L',Qh,1.0,math.radians(55)))
  # lock onto the handle when the source hands are together (2HBH, ready throat support)
  lw=(mk(m,i,'LWRA')+mk(m,i,'LWRB'))/2;rw=(mk(m,i,'RWRA')+mk(m,i,'RWRB'))/2;gap=(lw-rw).length
  lock=max(0,min(1,(clip.get('lock_far',.20)-gap)/.06));lock=lock*lock*(3-2*lock)
  if clip.get('lock_force') is not None:lock=clip['lock_force']
  ra=clip.get('release_at')
  if ra is not None and i>=ra:lock*=max(0.0,1-(i-ra+1)/4.0)
  REACH=[0.0]
  def apply_lock(Q):
   pos=fk(Q,hh)
   racketW=Matrix.LocRotScale(pos['Hand.R'],Q['Hand.R'],Vector((1,1,1)))@Matrix.Translation(ST)@SRc.to_4x4()
   up=clip.get('left_on',.135)-GRIP['grip_y']
   root=pos['UpperArm.L'];pole0=nz(mk(m,i,'LELB')-(mk(m,i,'LSHO')+lw)/2)
   best=None
   # hand rolls around the handle (handle stays through the finger cradle, thumb side toward the head)
   for deg in range(-180,180,15):
    target=racketW@Matrix.Translation((0,up,0))@Matrix.Rotation(math.radians(deg),4,'Y')@SL.inverted()
    HqL=target.to_quaternion();hand_head=target.translation
    for pole in (pole0,nz(pole0+Vector((0,0,-.6))),nz(pole0+Vector((.5,0,0)))):
     mid,end=two_bone(root,hand_head,LEN['UpperArm.L'],LEN['LowerArm.L'],pole)
     Qik=dict(Q);limb(Qik,'UpperArm.L','LowerArm.L',mid-root,end-mid,Vector((0,-1,0)),None)
     c=wrist_cost(Qik,'L',HqL)+(end-hand_head).length*20
     pk=fk(Qik,hh);c+=arm_cost(Qik,pk,pk['UpperArm.L'],pk['LowerArm.L'],pk['Hand.L'],HqL@Vector((0,1,0)))*3
     if best is None or c<best[0]:best=(c,Qik,HqL,deg,(end-hand_head).length)
   _,Qik,HqL,deg,_r=best;metrics.setdefault('left_roll',[]).append(deg);REACH[0]=_r
   for n in ['UpperArm.L','LowerArm.L']:Q[n]=Q[n].slerp(Qik[n],lock)
   Qfree=Q['Hand.L'];set_hand(Q,'L',HqL,1.0,math.radians(180))
   if lock<1:Q['Hand.L']=Qfree.slerp(Q['Hand.L'],lock)
   return Q
  def left_cost(Q):
   pos=fk(Q,hh);return arm_cost(Q,pos,pos['UpperArm.L'],pos['LowerArm.L'],pos['Hand.L'],Q['Hand.L']@Vector((0,1,0)))
  if lock>0:
   metrics['left_lock_frames']+=lock>.99
   base=dict(Q);Q=apply_lock(dict(base));scratch={}
   clear_arm(Q,hh,'L',False,scratch)
   if lock>.5 and (left_cost(Q)>.01 or REACH[0]>.015):
    # Two-hand hold through a thick chibi torso: carry the racket forward/out (face kept) until the left arm clears.
    posb=fk(base,hh);S=posb['UpperArm.R'];E=posb['LowerArm.R'];W=posb['Hand.R']
    fwd=base['Chest']@RQ['Chest'].inverted()@Vector((0,-1,0));fwd.z=0;fwd.normalize()
    best=(left_cost(Q)+REACH[0]*8,Q,0,REACH[0])
    pole=E-(S+W)/2
    Lsh=posb['UpperArm.L'];tol=nz(Vector((Lsh.x-W.x,Lsh.y-W.y,0)))
    dirs=[nz(fwd+Vector((0,0,.25))),tol,nz(fwd+tol),nz(tol-fwd*.5),Vector((0,0,1))]
    for dv in dirs:
     for cm in range(4,40,4):
      Qt=dict(base);W2=W+dv*(cm/100);Hq=Qt['Hand.R']
      E2,W2=two_bone(S,W2,LEN['UpperArm.R'],LEN['LowerArm.R'],pole)
      limb(Qt,'UpperArm.R','LowerArm.R',E2-S,W2-E2,Vector((0,-1,0)),hinge_of(Qt,'UpperArm.R'));set_hand(Qt,'R',Hq,1.0,math.pi)
      pr=fk(Qt,hh);rc=arm_cost(Qt,pr,pr['UpperArm.R'],pr['LowerArm.R'],pr['Hand.R'],Qt['Hand.R']@Vector((0,1,0)))
      Qt=apply_lock(Qt);clear_arm(Qt,hh,'L',False,scratch)
      c=left_cost(Qt)+rc+cm*.0004+REACH[0]*8
      if c<best[0]:best=(c,Qt,cm,REACH[0])
      if left_cost(Qt)+rc<.01 and REACH[0]<.01:break
    _,Q,deg,REACH[0]=best;metrics.setdefault('racket_carry_deg',[]).append(deg)  # cm moved
    Q=apply_lock(Q) if False else Q
   metrics.setdefault('left_reach_err',[]).append(REACH[0])
   metrics.setdefault('lock_frame_idx',[]).append(i)
  else:
   clear_arm(Q,hh,'L',True,{})
  pl=fk(Q,hh);metrics.setdefault('pen_after_L',[]).append(left_cost(Q))
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
  ch=[0 if qang(ref.to_quaternion(),r0.to_quaternion())<=qang(ref.to_quaternion(),r1.to_quaternion()) else 1 for r0,r1 in rel]
  acc=Vector((0,0,0,0));q0=None
  for (r0,r1),c in zip(rel,ch):
   q=(r0 if c==0 else r1).to_quaternion()
   if q0 is None:q0=q
   if q.dot(q0)<0:q=-q
   acc+=Vector(q)
  ref=Quaternion(acc).normalized().to_matrix()
 spread=sorted(math.degrees(qang(ref.to_quaternion(),(r0 if c==0 else r1).to_quaternion())) for (r0,r1),c in zip(rel,ch))
 return ch,spread[len(spread)//2]
def build2(name,clip):
 # Left wrist marker label convention per clip: the one needing less forearm twist is anatomically plausible.
 med=[]
 for sw_ in (False,True):
  _,mm=build(name,dict(clip,left_swap=sw_),[0]*len(clip['markers']['RFWT']));v=sorted(mm['ltw']);med.append(v[len(v)//2])
 clip=dict(clip,left_swap=med[1]<med[0]);print('LEFTLABEL',name,'swap' if clip['left_swap'] else 'as-labelled','median forearm twist deg',[round(math.degrees(x)) for x in med],flush=True)
 ch,spread=grip_consistent_flips(clip)
 if clip.get('release_after_contact') and clip.get('contact_s'):
  # pre-pass: first post-contact frame where the two-hand hold can't be kept without clipping/overreach
  _,mm=build(name,clip,ch);c=round(clip['contact_s']*30);pl=mm.get('pen_after_L',[]);idx=mm.get('lock_frame_idx',[]);rr=mm.get('left_reach_err',[])
  trouble=[f for f,rv in zip(idx,rr) if f>c+3 and (rv>.03 or pl[f]>.03)]
  if trouble:clip=dict(clip,release_at=max(c+4,trouble[0]-3));print('RELEASE',name,'left hand releases from frame',clip['release_at'],'(contact',c,')',flush=True)
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
if os.environ.get('RM_DEBUG'):
 Q0={n:RQ[n] for n in ORDER};pos0=fk(Q0,HEAD['Hips'])
 for sd in 'LR':
  S=pos0['UpperArm.'+sd];E=pos0['LowerArm.'+sd];W=pos0['Hand.'+sd]
  print('RESTPEN',sd,[round(pen(Q0,pos0,S.lerp(E,t),VOL['UpperArm']*.9),3) for t in (.55,.8,1)],[round(pen(Q0,pos0,E.lerp(W,t),VOL['LowerArm']),3) for t in (0,.5,1)])
 fr,_=build2('Ready',dict(D['Ready'],lock_far=.30,left_on=.17))
 for fi in (5,30,55):
  Q=fr[fi][0];pos=fk(Q,fr[fi][1]+HEAD['Hips']);S=pos['UpperArm.L'];E=pos['LowerArm.L'];W=pos['Hand.L'];h=Q['Hand.L']@Vector((0,1,0))
  print('READYPEN',fi,[round(pen(Q,pos,S.lerp(E,t),VOL['UpperArm']*.85),3) for t in (.8,1)],[round(pen(Q,pos,E.lerp(W,t),VOL['LowerArm']),3) for t in (0,.25,.5,.75,1)],round(pen(Q,pos,W+h*.07,VOL['Hand']*.9),3),'W rest',tuple(round(x,2) for x in to_rest(Q,pos,W)))
 raise SystemExit
if os.environ.get('RUNS_FIX'):
 # Surgical: original GameplayFixed run actions; only the LEFT arm is moved out of the torso volume.
 src=str(P.parent/'gameplay_fixed/Hero_Gameplay_Fixed_v1.blend')
 with bpy.data.libraries.load(src) as (fr_,to_):to_.actions=[a for a in fr_.actions if 'Run' in a]
 OUT=P/'fbx';rep={};HR={}
 for act0 in to_.actions:
  rig.animation_data.action=act0;n0=int(act0.frame_range[1]);frames=[];met={}
  for f in range(1,n0+1):
   bpy.context.scene.frame_set(f);Q={pb.name:pb.matrix.to_quaternion() for pb in rig.pose.bones if pb.name in B}
   hh=rig.pose.bones['Hips'].head.copy()
   clear_arm(Q,hh,'L',True,met)
   frames.append((Q,hh-HEAD['Hips'],0))
  name=act0.name.replace('Hero_Gameplay_','Hero_').replace('_v1','_v5')
  act=make_action(name,frames,None,loop=True);export(OUT/(name+'.fbx'),act)
  b_=met['pen_before_L'];a_=met['pen_after_L']
  rep[name]={'frames>1cm before':sum(1 for x in b_ if x>.01),'after':sum(1 for x in a_ if x>.01),'max before':round(max(b_),3),'max after':round(max(a_),3)}
  ref=[]
  for f in range(1,n0+1):
   bpy.context.scene.frame_set(f);row=[]
   for sd in 'RL':
    pb=rig.pose.bones;h=pb['Hand.'+sd].head;fd=(pb['Middle1.'+sd].head-h).normalized();wd=(pb['Pinky1.'+sd].head-pb['Index1.'+sd].head).normalized()
    row+=[[-fd.x,fd.z,-fd.y],[-wd.x,wd.z,-wd.y]]
   ref.append(row)
  HR[name.replace('Hero_','').replace('_v5','')]=ref
  print('RUNFIX',name,rep[name],flush=True)
 (P/'runs_fix_report.json').write_text(json.dumps(rep,indent=1));(P/'hand_ref_runs.json').write_text(json.dumps(HR))
 raise SystemExit
CFG={'Ready':{'lock_force':1.0,'left_on':.17},'Forehand':{'lock_far':.0},'Backhand':{'lock_far':.24,'left_on':.135,'release_after_contact':True},'Serve':{'lock_far':.0},'Volley':{'lock_far':.0},'Smash':{'lock_far':.0}}
HANDREF={};out={};OUT=P/'fbx';OUT.mkdir(exist_ok=True);report={}
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
 # authored hand geometry for the Unity playback check (Unity hero space: x=-bx, y=bz, z=-by)
 ref=[]
 for f in range(1,int(act.frame_range[1])+1):
  bpy.context.scene.frame_set(f);row=[]
  for sd in 'RL':
   pb=rig.pose.bones;h=pb['Hand.'+sd].head;fd=(pb['Middle1.'+sd].head-h).normalized();wd=(pb['Pinky1.'+sd].head-pb['Index1.'+sd].head).normalized()
   row+= [[-fd.x,fd.z,-fd.y],[-wd.x,wd.z,-wd.y]]
  ref.append(row)
 HANDREF[name]=ref
 met.pop('ltw',None)
 pb=[a+b for a,b in zip(met.get('pen_before_R',[0]),met.get('pen_before_R',[0]))]
 allb=[r+l for r,l in zip(met.get('pen_before_R',[]),[0]*len(met.get('pen_before_R',[])))]
 met['worst_clip_frame']=max(range(len(met.get('pen_before_R',[0]))),key=lambda k:met['pen_before_R'][k]) if met.get('pen_before_R') else 0
 met.pop('left_roll',None);met.pop('lock_frame_idx',None);lr=met.pop('left_reach_err',[0]);met['left_reach_profile']=' '.join('%d'%round(x*100) for x in lr);met['left_reach_err_max_cm']=round(max(lr)*100,1);met['left_reach_err_frames_gt2cm']=sum(1 for x in lr if x>.02);met['racket_carry_max_cm']=max(met.pop('racket_carry_deg',[0]))
 met['penL_profile']=' '.join('%d'%round(x*100) for x in met.get('pen_after_L',[]))
 for k in [k for k in met if k.startswith('pen_')]:
  v=met[k];met[k]='frames>1cm %d / max %.3f'%(sum(1 for x in v if x>.01),max(v))
 met['face_assist_max_deg']=round(max(met.get('assist_deg',[0])),1);met.pop('assist_deg',None)
 bad=[i for i,(a,b) in enumerate(zip(met['flex'],met['dev'])) if abs(a)>80 or abs(b)>45]
 met['wrist_outlier_frames']=str(bad[:40])+(' n=%d'%len(bad))
 for k in ('flex','dev'):
  v=sorted(met.get(k,[0]));met[k]='p5 %.0f p50 %.0f p95 %.0f'%(v[len(v)//20],v[len(v)//2],v[-1-len(v)//20])
 report[name]=dict(met,frames=len(frames),seconds=round((len(frames)-1)/30,3),contact_s=D[name]['contact_s'])
 print('CLIP',name,report[name],flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Hero_RestoreMotion_v5.blend'))
(P/'hand_ref.json').write_text(json.dumps(HANDREF));(P/'retarget_report.json').write_text(json.dumps({'scale':SC,'clips':report},indent=1))
print('RETARGET_DONE')
