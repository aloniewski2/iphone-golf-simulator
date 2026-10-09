"""Render measured C3D joint paths with a neutral capsule figure; no invented motion."""
from pathlib import Path
import c3d,numpy as np,json,math
from PIL import Image,ImageDraw,ImageFont
import imageio_ffmpeg
P=Path(__file__).resolve().parent;S=P.parent/'samples'
fontpath='/System/Library/Fonts/Supplemental/Arial.ttf';bold='/System/Library/Fonts/Supplemental/Arial Bold.ttf'
F=lambda n:ImageFont.truetype(fontpath,n)
B=lambda n:ImageFont.truetype(bold,n)
clips=[('01','FOREHAND','tennis-01-forehand-yamaoka.c3d'),('02','BACKHAND · HARD HIT','tennis-11-backhand hardhit-yamaoka.c3d'),('03','FIRST SERVE','tennis-15-first service-yamaoka.c3d'),('04','FOREHAND VOLLEY','tennis-05-forehand volley-yamaoka.c3d')]
report=[];allclips=[]
for number,title,file in clips:
 with (S/file).open('rb') as h:
  r=c3d.Reader(h);fps=float(r.point_rate);first=r.first_frame;labels=[str(n).strip() for n in r.point_labels];raw=np.array([p.copy() for _,p,_ in r.read_frames()]);N=len(raw)
 needed=['RFWT','RBWT','LFWT','LBWT','RSHO','LSHO','RELB','LELB','RWRA','RWRB','LWRA','LWRB','RFIN','LFIN','RKNE','LKNE','RANK','LANK','RTOE','LTOE','RHEE','LHEE','RFHD','LFHD','RBHD','LBHD']
 data={};valids=[]
 for n in needed:
  ids=[i for i,label in enumerate(labels) if label==n or label.startswith(n+'-')]
  arr=np.full((N,3),np.nan)
  for i in ids:
   good=(raw[:,i,3]>=0)&(np.linalg.norm(raw[:,i,:3],axis=1)>1)
   use=good&np.isnan(arr[:,0]);arr[use]=raw[use,i,:3]/1000
  valid=np.isfinite(arr[:,0]);valids.append(valid)
  if valid.sum()<2:raise RuntimeError('Missing marker '+n)
  for j in range(3):arr[:,j]=np.interp(np.arange(N),np.where(valid)[0],arr[valid,j])
  data[n]=arr
 def avg(*names):return sum(data[n] for n in names)/len(names)
 joints={'hipR':avg('RFWT','RBWT'),'hipL':avg('LFWT','LBWT'),'shoulderR':data['RSHO'],'shoulderL':data['LSHO'],'elbowR':data['RELB'],'elbowL':data['LELB'],'wristR':avg('RWRA','RWRB'),'wristL':avg('LWRA','LWRB'),'handR':data['RFIN'],'handL':data['LFIN'],'kneeR':data['RKNE'],'kneeL':data['LKNE'],'ankleR':data['RANK'],'ankleL':data['LANK'],'toeR':data['RTOE'],'toeL':data['LTOE'],'heelR':data['RHEE'],'heelL':data['LHEE'],'head':avg('RFHD','LFHD','RBHD','LBHD')}
 joints['pelvis']=(joints['hipR']+joints['hipL'])/2;joints['neck']=(joints['shoulderR']+joints['shoulderL'])/2
 # Favor actual swing bursts over capture startup/calibration, avoiding marker dropouts.
 velocity=np.linalg.norm(np.gradient(joints['wristR'],axis=0)*fps,axis=1)
 velocity=np.convolve(velocity,np.ones(13)/13,mode='same')
 valid=np.mean(valids,axis=0);quality=np.convolve(valid,np.ones(61)/61,mode='same')
 score=velocity.copy();score[:int(3*fps)]=0;score[-int(3*fps):]=0;score[quality<.94]=0
 peak=int(np.argmax(score));start=max(0,peak-int(1.8*fps));end=min(N-1,start+int(4.4*fps))
 origin=np.median(joints['pelvis'][start:end],axis=0);origin[2]=np.percentile(np.concatenate([joints['toeR'][start:end,2],joints['toeL'][start:end,2]]),5)
 # Align one fixed camera pair to initial torso; no camera motion follows the stroke.
 right=np.median((joints['shoulderR']-joints['shoulderL'])[start:start+20],axis=0);right[2]=0;right/=np.linalg.norm(right)
 forward=np.cross([0,0,1],right)
 for n,arr in joints.items():
  v=arr-origin;joints[n]=np.stack([v@right,v@forward,v[:,2]],axis=1)
 frames=np.linspace(start,end,round((end-start)/fps*30)+1)
 info={'title':title,'source':file,'source_fps':fps,'source_first_frame':first,'excerpt_frames':[start+first,end+first],'peak_frame':peak+first,'valid_marker_fraction':float(np.mean(valid[start:end])),'playback_speed':1.0}
 report.append(info);allclips.append((number,title,joints,frames,peak))
 print(info,flush=True)

W,H=1280,720
bg='#101D2D';panel='#172B40';muted='#9BAFC3';blue='#51B9FF'
def draw_frame(number,title,joints,index,progress):
 im=Image.new('RGB',(W,H),bg);d=ImageDraw.Draw(im)
 d.text((40,23),'TENNIS / SOURCE MOTION REVIEW',font=B(17),fill=blue)
 d.text((40,51),number+'  '+title,font=B(29),fill='white')
 d.text((1238,30),'30 FPS  ·  REAL TIME',font=F(16),fill=muted,anchor='ra')
 for view,(angle,cx) in enumerate([(-.48,330),(1.12,950)]):
  d.rounded_rectangle((25+view*620,105,625+view*620,646),radius=18,fill=panel)
  d.text((47+view*620,123),'THREE-QUARTER' if view==0 else 'SIDE ANGLE',font=B(14),fill=muted)
  def project(v):
   x,y,z=v;c,s=math.cos(angle),math.sin(angle);xx=c*x-s*y;depth=s*x+c*y;return (cx+xx*205,584-z*205+depth*35,depth)
  for x in np.arange(-1.5,1.6,.5):
   a=project([x,-1.4,0]);b=project([x,1.4,0]);d.line([a[:2],b[:2]],fill='#254058',width=1)
  for y in np.arange(-1.5,1.6,.5):
   a=project([-1.5,y,0]);b=project([1.5,y,0]);d.line([a[:2],b[:2]],fill='#254058',width=1)
  i=min(len(joints['head'])-1,max(0,int(index)));pts={n:a[i] for n,a in joints.items()}
  primitives=[]
  def bone(a,b,r,col):primitives.append(((pts[a][1]+pts[b][1])/2,'bone',project(pts[a]),project(pts[b]),r,col))
  bone('neck','head',9,'#F0BE96')
  for side in ['R','L']:
   bone('shoulder'+side,'elbow'+side,13,'#58B9E9');bone('elbow'+side,'wrist'+side,10,'#70C9F1');bone('wrist'+side,'hand'+side,9,'#F0BE96')
   bone('hip'+side,'knee'+side,17,'#49708F');bone('knee'+side,'ankle'+side,12,'#6288A5');bone('heel'+side,'toe'+side,10,'#E0EAF2')
  torso=[pts[n] for n in ['hipL','hipR','shoulderR','shoulderL']]
  primitives.append((np.mean(torso,axis=0)[1],'torso',[project(p)[:2] for p in torso]))
  primitives.append((pts['head'][1],'head',project(pts['head'])))
  # Painter order uses view-space depth to handle crossing limbs.
  for item in primitives:
   if item[1]=='bone':pass
  primitives.sort(key=lambda p:(p[2][2]+p[3][2])/2 if p[1]=='bone' else project(np.mean(torso,axis=0))[2] if p[1]=='torso' else p[2][2],reverse=True)
  for item in primitives:
   if item[1]=='torso':
    d.polygon(item[2],fill='#D7E5EE');d.line(item[2]+[item[2][0]],fill='#A9C0D2',width=2)
   elif item[1]=='head':
    x,y,_=item[2];d.ellipse((x-19,y-28,x+19,y+20),fill='#F0BE96',outline='#F8D9BA',width=2)
   else:
    _,_,a,b,r,col=item;a=a[:2];b=b[:2];d.line([a,b],fill=col,width=r*2)
    for x,y in [a,b]:d.ellipse((x-r,y-r,x+r,y+r),fill=col)
    d.line([(a[0]-r*.22,a[1]),(b[0]-r*.22,b[1])],fill={'#58B9E9':'#82D0F0','#70C9F1':'#93D8F5','#49708F':'#6286A1','#6288A5':'#7FA0B9'}.get(col,col),width=max(2,r//3))
  trail=[project(p)[:2] for p in joints['handR'][max(0,i-30):i:3]]
  if len(trail)>1:d.line(trail,fill='#FFAB64',width=3)
 d.text((40,666),'MocapData / Eyes, JAPAN  •  Neutral marker-driven figure  •  Not retargeted to V4',font=F(15),fill=muted)
 d.text((1240,666),'Orange trail: right hand',font=F(15),fill='#FFAB64',anchor='ra')
 d.rectangle((40,698,1240,702),fill='#29435C');d.rectangle((40,698,40+1200*progress,702),fill=blue)
 return im
movie=P/'tennis_source_preview.mp4';writer=imageio_ffmpeg.write_frames(str(movie),(W,H),fps=30,codec='libx264',quality=8,macro_block_size=1,output_params=['-pix_fmt','yuv420p','-movflags','+faststart']);writer.send(None)
for number,title,joints,frames,peak in allclips:
 for j,index in enumerate(frames):writer.send(np.asarray(draw_frame(number,title,joints,index,j/max(1,len(frames)-1))))
 still=draw_frame(number,title,joints,peak,.5);still.save(P/(number+'_peak.png'))
writer.close();(P/'preview_manifest.json').write_text(json.dumps(report,indent=2));print('DONE',movie,flush=True)
