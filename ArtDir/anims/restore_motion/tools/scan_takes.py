# Scan Eyes Japan tennis C3D takes: marker labels, length and right-wrist speed peaks (stroke candidates).
from pathlib import Path
import numpy as np,c3d,zipfile,json
P=Path(__file__).resolve().parents[2];S=P/'source/mocapdata-tennis/samples'
takes=['tennis-03-forehand hardhit-yamaoka.c3d','tennis-01-forehand-yamaoka.c3d','tennis-12-backhand double hardhit-yamaoka.c3d','tennis-15-first service-yamaoka.c3d','tennis-05-forehand volley-yamaoka.c3d','tennis-06-forehand smash-yamaoka.c3d','tennis-17-receive-yamaoka.c3d']
out={}
with zipfile.ZipFile(P/'source/mocapdata-tennis/tennis.zip') as z:
 for t in takes:
  if not (S/t).exists():
   e=next(n for n in z.namelist() if n.endswith('/'+t));(S/t).write_bytes(z.read(e))
  with (S/t).open('rb') as h:
   r=c3d.Reader(h);fps=float(r.point_rate);first=r.first_frame;labels=[str(n).strip() for n in r.point_labels];raw=np.array([p.copy() for _,p,_ in r.read_frames()])
  def mk(n):
   i=[k for k,l in enumerate(labels) if l==n or l.startswith(n+'-')]
   a=np.full((len(raw),3),np.nan)
   for k in i:
    v=(raw[:,k,3]>=0)&(np.linalg.norm(raw[:,k,:3],axis=1)>1)&np.isnan(a[:,0]);a[v]=raw[v,k,:3]/1000
   return a
  w=(mk('RWRA')+mk('RWRB'))/2;ok=np.isfinite(w[:,0])
  for j in range(3):w[:,j]=np.interp(np.arange(len(w)),np.where(ok)[0],w[ok,j])
  sp=np.linalg.norm(np.gradient(w,axis=0)*fps,axis=1);sp=np.convolve(sp,np.ones(9)/9,'same')
  peaks=[]
  for i in np.argsort(-sp):
   if all(abs(i-p)>fps*2 for p in peaks):peaks.append(int(i))
   if len(peaks)>=6:break
  peaks=sorted(p for p in peaks if sp[p]>2.5)
  out[t]={'fps':fps,'first':first,'frames':len(raw),'labels':labels,'peaks':[[p+first,round(float(sp[p]),2),round(float(w[p,2]),2)] for p in peaks]}
  print(t,fps,len(raw),'peaks(frame,speed m/s,wrist z)',out[t]['peaks'])
print(sorted(set(sum([v['labels'] for v in out.values()],[]))))
(P/'restore_motion/tools/takes_scan.json').write_text(json.dumps(out,indent=1))
