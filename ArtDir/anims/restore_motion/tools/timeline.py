# Per-clip timeline over a wide window: hip yaw, pelvis speed, racket-hand speed (10 Hz rows).
import numpy as np,c3d,json,math,warnings,sys
from pathlib import Path
warnings.filterwarnings('ignore')
P=Path(__file__).resolve().parents[2];S=P/'source/mocapdata-tennis/samples'
W=json.load(open(Path(__file__).parent/'../source_windows.json'))
for name,w in W.items():
 if name=='Ready':continue
 r=c3d.Reader((S/w['take']).open('rb'));first=r.first_frame;labels=[str(n).strip() for n in r.point_labels];raw=np.array([p.copy() for _,p,_ in r.read_frames()])
 def g(n):
  i=[k for k,l in enumerate(labels) if l==n][0];a=raw[:,i,:3]/1000;v=(raw[:,i,3]>=0)&(np.linalg.norm(a,axis=1)>.001)
  for j in range(3):a[:,j]=np.interp(np.arange(len(a)),np.where(v)[0],a[v,j])
  return a
 L=(g('LFWT')+g('LBWT'))/2;R=(g('RFWT')+g('RBWT'))/2;pel=(L+R)/2;x=L-R;yaw=np.degrees(np.unwrap(np.arctan2(x[:,1],x[:,0])))
 wr=(g('RWRA')+g('RWRB'))/2;sp=np.linalg.norm(np.gradient(wr,axis=0)*120,axis=1);ps=np.linalg.norm(np.gradient(pel[:,:2],axis=0)*120,axis=1)
 pk=w['peak']-first;print(f'== {name} contact at 0.0s (source frame {w["peak"]})')
 base=yaw[pk]
 for t in np.arange(-2.4,2.01,.1):
  i=int(pk+t*120)
  if 0<=i<len(raw):print(f'  {t:+.1f}s yaw {yaw[i]-base:+6.0f}  pelvis {ps[i]:.2f} m/s  wrist {sp[i]:5.2f}  wristZ {wr[i,2]:.2f}  pelZ {pel[i,2]:.2f}')
