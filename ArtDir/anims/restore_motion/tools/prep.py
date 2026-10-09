# Eyes Japan C3D -> hero-frame markers + fitted racket frames at 30 fps, one window per stroke.
from pathlib import Path
import numpy as np,c3d,json,itertools,warnings
warnings.filterwarnings('ignore')
P=Path(__file__).resolve().parents[2];S=P/'source/mocapdata-tennis/samples'
CLIPS={ # name: take, pre, post (seconds around the racket-speed peak)
 'Forehand':('tennis-03-forehand hardhit-yamaoka.c3d',1.4,1.1),
 'Backhand':('tennis-12-backhand double hardhit-yamaoka.c3d',1.4,1.2),
 'Serve':('tennis-15-first service-yamaoka.c3d',2.2,1.2),
 'Volley':('tennis-05-forehand volley-yamaoka.c3d',1.2,0.55),
 'Smash':('tennis-06-forehand smash-yamaoka.c3d',2.2,1.1),
 'Ready':('tennis-17-receive-yamaoka.c3d',None,None)}
MODEL=np.array([[-.065,0,0],[.065,0,0],[-.15,.256,0],[.15,.256,0],[0,.385,0]])
NAMES=['RFWT','RBWT','LFWT','LBWT','RSHO','LSHO','RELB','LELB','RWRA','RWRB','LWRA','LWRB','RFIN','LFIN','RKNE','LKNE','RANK','LANK','RTOE','LTOE','RHEE','LHEE','RFHD','LFHD','RBHD','LBHD','C7','CLAV','STRN','T10','RTHI','LTHI']
def kabsch(A,B):
 ca=A.mean(0);cb=B.mean(0);H=(A-ca).T@(B-cb);U,s,Vt=np.linalg.svd(H);d=np.sign(np.linalg.det(Vt.T@U.T));D=np.diag([1,1,d]);R=Vt.T@D@U.T;return R,cb-R@ca
def load(t):
 r=c3d.Reader((S/t).open('rb'));fps=float(r.point_rate);first=r.first_frame;labels=[str(n).strip() for n in r.point_labels];raw=np.array([p.copy() for _,p,_ in r.read_frames()])
 ok=(raw[:,:,3]>=0)&(np.linalg.norm(raw[:,:,:3],axis=2)>1);pos=raw[:,:,:3]/1000
 data={}
 for n in NAMES:
  a=np.full((len(raw),3),np.nan)
  for i,l in enumerate(labels):
   if l==n or l.startswith(n+'-'):
    v=ok[:,i]&np.isnan(a[:,0]);a[v]=pos[v,i]
  v=np.isfinite(a[:,0])
  if v.sum()<3:continue
  for j in range(3):a[:,j]=np.interp(np.arange(len(a)),np.where(v)[0],a[v,j])
  data[n]=a
 un=[i for i,l in enumerate(labels) if l.startswith('*')]
 return fps,first,data,pos,ok,un
def fit_racket(data,pos,ok,un,f):
 W=(data['RWRA'][f]+data['RWRB'][f])/2;cand={'t':[],'h':[],'e':[]}
 for i in un:
  if not ok[f,i]:continue
  d=np.linalg.norm(pos[f,i]-W)
  if .26<d<.40:cand['t'].append(pos[f,i])
  elif .52<d<.645:cand['h'].append(pos[f,i])
  elif .645<d<.77:cand['e'].append(pos[f,i])
 best=None
 for ts in itertools.permutations(cand['t'],min(2,len(cand['t']))):
  for hs in itertools.permutations(cand['h'],min(2,len(cand['h']))):
   for es in ([cand['e'][0]] if cand['e'] else [None],):
    for e in es:
     A=[];B=[]
     for k,p in enumerate(ts):A.append(MODEL[k]);B.append(p)
     for k,p in enumerate(hs):A.append(MODEL[2+k]);B.append(p)
     if e is not None:A.append(MODEL[4]);B.append(e)
     if len(A)<3:continue
     A=np.array(A);B=np.array(B);R,t=kabsch(A,B);res=np.sqrt(((A@R.T+t-B)**2).sum(1).mean())
     if best is None or res<best[0]:best=(res,R,t,len(A))
 return best
out={};report={}
for name,(take,pre,post) in CLIPS.items():
 fps,first,data,pos,ok,un=load(take);N=len(pos)
 W=(data['RWRA']+data['RWRB'])/2;sp=np.linalg.norm(np.gradient(W,axis=0)*fps,axis=1);sp=np.convolve(sp,np.ones(9)/9,'same')
 if pre is None:
  body=sum(np.linalg.norm(np.gradient(data[n],axis=0)*fps,axis=1) for n in ['RANK','LANK','RWRA','LWRA','RFWT'])
  # Athletic ready: racket head up in front of the chest, left hand near the right (throat support), low motion.
  step=6;hz=np.full(N,np.nan)
  for f in range(0,N,step):
   x=fit_racket(data,pos,ok,un,f)
   if x and x[0]<.02:hz[f]=(x[1]@np.array([0,.2,0])+x[2])[2]
  ok2=np.isfinite(hz);hz=np.interp(np.arange(N),np.where(ok2)[0],hz[ok2])
  gap=np.linalg.norm((data['LWRA']+data['LWRB'])/2-(data['RWRA']+data['RWRB'])/2,axis=1)
  cost=body/np.median(body)+4*np.clip(1.05-hz,0,None)*10+3*np.clip(gap-.25,0,None)*10
  L=int(2.4*fps);score=np.convolve(cost,np.ones(L)/L,'valid');score[:360]=1e9;score[-360:]=1e9;a=int(np.argmin(score));b=a+L;peak=None
  print('  ready window head z',round(float(hz[a:b].mean()),2),'wrist gap',round(float(gap[a:b].mean()),2))
  windows=[(a,b,None)]
 else:
  peaks=[]
  for i in np.argsort(-sp):
   if sp[i]<2.5:break
   if all(abs(i-p)>fps*2 for p in peaks):peaks.append(int(i))
  windows=[(p-int(pre*fps),p+int(post*fps),p) for p in peaks if p-int(pre*fps)>=0 and p+int(post*fps)<N]
 scored=[]
 for a,b,p in windows:
  fits=[fit_racket(data,pos,ok,un,f) for f in range(a,b)]
  good=[x for x in fits if x and x[0]<.02]
  scored.append((len(good)/(b-a),a,b,p,fits))
 # prefer the take used for the report (same repetition as the timeline) when coverage is comparable
 scored.sort(key=lambda s:-s[0]);cov,a,b,peak,fits=scored[0]
 # racket rotations in source frame; fill gaps by nearest valid with slerp-free hold (then smoothed via quaternion averaging)
 Rs=[x[1] if x and x[0]<.02 else None for x in fits]
 # source-to-hero frame (+X anatomical left, +Y back, Z up), centered on the start pelvis
 origin=(data['RFWT'][a]+data['RBWT'][a]+data['LFWT'][a]+data['LBWT'][a])/4
 origin[2]=np.percentile(np.r_[data['RTOE'][a:b,2],data['LTOE'][a:b,2]],5)
 left=np.mean(data['LSHO'][a:a+20]-data['RSHO'][a:a+20],axis=0);left[2]=0;left/=np.linalg.norm(left);back=np.cross([0,0,1],left)
 if peak is not None:
  # Net direction = horizontal racket-tip velocity at contact (independent of the face-normal check).
  def tip(f):
   x=fits[f-a];return None if not x or x[0]>.02 else x[1]@MODEL[4]+x[2]
  k=peak;t0=tip(k-2);t1=tip(k+2)
  if t0 is not None and t1 is not None:
   v=t1-t0;v[2]=0;fwd=v/np.linalg.norm(v);back=-fwd;left=np.cross(back,[0,0,1])
 C=np.stack([left,back,[0,0,1]])  # rows: new axes
 contact=None
 if peak is not None:
  def rk(f):
   x=fits[f-a] if a<=f<b else None;return None if not x or x[0]>.02 else x
  head={f:(rk(f)[1]@np.array([0,.2,0])+rk(f)[2]) for f in range(max(a,peak-40),min(b,peak+10)) if rk(f)}
  fr=sorted(head);sp={f:np.linalg.norm(head[f]-head[g])*fps for g,f in zip(fr,fr[1:])}
  fwd3=-back
  if name in ('Serve','Smash'):cand=[f for f in fr if peak-30<=f<=peak+4];contact=max(cand,key=lambda f:head[f][2])
  else:
   mx=max(sp.get(f,0) for f in fr if peak-16<=f<=peak+4)
   cand=[f for f in fr if peak-16<=f<=peak+4 and sp.get(f,0)>.6*mx]
   contact=max(cand,key=lambda f:abs(rk(f)[1][:,2]@fwd3))
  # heading at true contact
  t0=rk(contact-2);t1=rk(contact+2)
  if t0 and t1:
   v=(t1[1]@MODEL[4]+t1[2])-(t0[1]@MODEL[4]+t0[2]);v[2]=0;fwd=v/np.linalg.norm(v);back=-fwd;left=np.cross(back,[0,0,1]);C=np.stack([left,back,[0,0,1]])
  # 30 fps sampling with a 0.5x slow-in over +/-100 ms around contact so the string face reads on screen
  pre_=list(np.arange(contact-12,a-1,-4))[::-1];slow=list(np.arange(contact-10,contact+12,2));post=list(np.arange(contact+12,b,4))
  idx=np.array(sorted(set(pre_+slow+post)),float)
 else:
  idx=np.arange(a,b,fps/30.0)
 mk={}
 for n,v in data.items():
  v=(v-origin)@C.T;mk[n]=np.stack([np.interp(idx,np.arange(N),v[:,j]) for j in range(3)],1).round(5).tolist()
 rk=[]
 for x in idx:
  f=int(round(x))-a;f=min(max(f,0),len(Rs)-1)
  # nearest valid fit
  best=None
  for d in range(0,30):
   for g in (f-d,f+d):
    if 0<=g<len(Rs) and Rs[g] is not None:best=Rs[g];break
   if best is not None:break
  rk.append(None if best is None else (C@best).round(6).tolist())
 out[name]={'take':take,'source_frames':[a+first,b+first],'peak':None if peak is None else peak+first,'contact_s':None if contact is None else round(float(np.where(idx==contact)[0][0])/30,3),'contact_source_frame':None if contact is None else int(contact+first),'racket_fit_coverage':round(cov,3),'markers':mk,'racket':rk}
 report[name]={k:out[name][k] for k in ['take','source_frames','peak','contact_s','contact_source_frame','racket_fit_coverage']}
 print(name,report[name],'reps',len(windows))
(P/'restore_motion/tools/markers_v2.json').write_text(json.dumps(out))
(P/'restore_motion/source_windows.json').write_text(json.dumps(report,indent=1))
