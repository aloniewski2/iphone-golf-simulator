from pathlib import Path
import numpy as np,c3d,json,zipfile
P=Path(__file__).resolve().parents[1];S=P/'source/mocapdata-tennis/samples'
clips=[('Forehand','tennis-01-forehand-yamaoka.c3d',2231,2759,2447),('Backhand','tennis-11-backhand hardhit-yamaoka.c3d',880,1408,1096),('Serve','tennis-15-first service-yamaoka.c3d',1207,1735,1423),('Volley','tennis-05-forehand volley-yamaoka.c3d',1648,2176,1864),('ReadyIdle','tennis-17-receive-yamaoka.c3d',None,None,None)]
result={}
for name,file,start,end,peak in clips:
 if not (S/file).exists():
  with zipfile.ZipFile(P/'source/mocapdata-tennis/tennis.zip') as z:
   entry=next(n for n in z.namelist() if n.endswith('/'+file));(S/file).write_bytes(z.read(entry))
 with (S/file).open('rb') as h:
  r=c3d.Reader(h);fps=float(r.point_rate);first=r.first_frame;labels=[str(n).strip() for n in r.point_labels];raw=np.array([p.copy() for _,p,_ in r.read_frames()])
 N=len(raw);data={}
 for n in ['RFWT','RBWT','LFWT','LBWT','RSHO','LSHO','RELB','LELB','RWRA','RWRB','LWRA','LWRB','RFIN','LFIN','RKNE','LKNE','RANK','LANK','RTOE','LTOE','RHEE','LHEE','RFHD','LFHD','RBHD','LBHD']:
  a=np.full((N,3),np.nan)
  for i,l in enumerate(labels):
   if l==n or l.startswith(n+'-'):
    valid=(raw[:,i,3]>=0)&(np.linalg.norm(raw[:,i,:3],axis=1)>1)&np.isnan(a[:,0]);a[valid]=raw[valid,i,:3]/1000
  valid=np.isfinite(a[:,0]);assert valid.sum()>2,n
  for j in range(3):a[:,j]=np.interp(np.arange(N),np.where(valid)[0],a[valid,j])
  data[n]=a
 if start is None:
  # Quietest complete 2.4-second receive stance away from capture startup/end.
  speed=sum(np.linalg.norm(np.gradient(data[n],axis=0)*fps,axis=1) for n in ['RANK','LANK','RWRA','LWRA'])
  score=np.convolve(speed,np.ones(288)/288,mode='valid');score[:360]=999;score[-360:]=999
  start=int(np.argmin(score))+first;end=start+288;peak=start+144
 a,b=start-first,end-first
 origin=(data['RFWT'][a]+data['RBWT'][a]+data['LFWT'][a]+data['LBWT'][a])/4
 origin[2]=np.percentile(np.r_[data['RTOE'][a:b,2],data['LTOE'][a:b,2]],5)
 left=np.mean(data['LSHO'][a:a+20]-data['RSHO'][a:a+20],axis=0);left[2]=0;left/=np.linalg.norm(left)
 back=np.cross([0,0,1],left)
 # +X anatomical left, +Y back, Z up, matching the existing Blender hero.
 idx=np.linspace(a,b,round((b-a)/fps*30)+1)
 out={}
 for n,v in data.items():
  v=v-origin;v=np.stack([v@left,v@back,v[:,2]],axis=1)
  out[n]=np.stack([np.interp(idx,np.arange(N),v[:,j]) for j in range(3)],axis=1).tolist()
 result[name]={'markers':out,'source':file,'source_frames':[start,end],'contact_proxy_seconds':(peak-start)/fps,'fps':30}
 print(name,start,end)
(P/'own_mocap_tools/markers.json').write_text(json.dumps(result))
