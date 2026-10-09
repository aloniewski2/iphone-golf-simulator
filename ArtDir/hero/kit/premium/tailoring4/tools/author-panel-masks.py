"""Sport tailoring masks derived from the exact construction UV inputs.
Only channel B (contrast panel amount) changes. AO/cavity/fabric IDs and all
geometry stay identical; saved shirt/shoe base colours remain customizable.
"""
import pathlib,json,hashlib,sys,shutil,numpy as np
from PIL import Image
R=pathlib.Path(__file__).resolve().parents[6];W=pathlib.Path(__file__).resolve().parents[1];O=W/'generated/panel-maps';O.mkdir(parents=True,exist_ok=True)
import cv2
rows=[]
for golf in [False,True]:
 for sex in ['Male','Female']:
  for tag in ['Top','ShoeL','ShoeR']:
   piece='Kit_'+({'ShoeL':'Shoe_L','ShoeR':'Shoe_R'}.get(tag,tag));prefix='Golf' if golf else 'Kit'
   source=W/'inputs'/('Golf' if golf else 'Tennis')/(sex+'_'+piece+'.npz')
   prior=W/'inputs/baseline-masks'/f'{prefix}_{sex}_{tag}_M.png'
   if tag=='Top':
    out=O/prior.name;shutil.copyfile(prior,out)
    rows.append({'sport':'Golf' if golf else 'Tennis','sex':sex,'piece':tag,'source':str(source.relative_to(R)),'prior':str(prior.relative_to(R)),'output':str(out.relative_to(R)),'sourceSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'priorSHA256':hashlib.sha256(prior.read_bytes()).hexdigest(),'outputSHA256':hashlib.sha256(out.read_bytes()).hexdigest(),'AO_cavity_fabric_exact':True,'panelCoveredFraction':0,'status':'Original clean polo map exact; broad panel candidate rejected'})
    continue
   data=np.load(source);P=data['P'];UV=data['uv'];tri=data['tri'];loops=data['triloops']
   original=np.array(Image.open(prior).convert('RGBA'));mask=original[::-1].copy();S=mask.shape[0]
   pos=np.zeros((S,S,3),np.float32);covered=np.zeros((S,S),bool)
   for ids,ls in zip(tri,loops):
    uv=UV[ls]*S;mn=np.maximum(np.floor(uv.min(0)).astype(int),0);mx=np.minimum(np.ceil(uv.max(0)).astype(int),S-1)
    if (mx<mn).any():continue
    a,b,c=uv;den=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    if abs(den)<1e-10:continue
    xx,yy=np.meshgrid(np.arange(mn[0],mx[0]+1)+.5,np.arange(mn[1],mx[1]+1)+.5)
    w1=((xx-a[0])*(c[1]-a[1])-(yy-a[1])*(c[0]-a[0]))/den
    w2=((b[0]-a[0])*(yy-a[1])-(b[1]-a[1])*(xx-a[0]))/den;w0=1-w1-w2
    valid=(w0>=0)&(w1>=0)&(w2>=0)
    if not valid.any():continue
    iy,ix=np.where(valid);y=iy+mn[1];x=ix+mn[0]
    pos[y,x]=np.stack([w0[valid],w1[valid],w2[valid]],1)@P[ids];covered[y,x]=True
   q=pos[covered];x,y,z=q.T;ax=abs(x)
   if tag=='Top':
    # Clean reference polo: retain the original sewn collar/cuff seams.
    # The broad shoulder panels and chest stripe were rejected in actual22.
    panel=np.zeros(len(q),bool)
   else:
    # Upper toe bumper and heel counter are dark; the mesh tongue remains
    # cream. The authored rubber sole is preserved as a distinct material ID.
    floor=z.min();length=y.max()-y.min();front=y.min()+length*.21;heel=y.max()-length*.16
    panel=((y<front)|(y>heel))&(z>floor+.065)
    panel |= (z>(.12 if sex=='Male' else .105))
    # Do not darken the sole's continuous sidewall.
    alpha=mask[covered,3];fabric=np.round(alpha*(9 if golf else 5))
    panel &= ~np.isin(fabric,[3,4])
   amounts=mask[covered,2];amounts=np.maximum(amounts,panel.astype(np.uint8)*255);mask[covered,2]=amounts
   _,labels=cv2.distanceTransformWithLabels((~covered).astype(np.uint8),cv2.DIST_L2,3,labelType=cv2.DIST_LABEL_PIXEL)
   keys=labels[covered];nearest=np.zeros(keys.max()+1,np.uint8);nearest[keys]=mask[covered,2];mask[~covered,2]=nearest[labels[~covered]]
   result=mask[::-1];out=O/prior.name;Image.fromarray(result).save(out)
   assert np.array_equal(original[:,:,[0,1,3]],result[:,:,[0,1,3]])
   rows.append({'sport':'Golf' if golf else 'Tennis','sex':sex,'piece':tag,'source':str(source.relative_to(R)),'prior':str(prior.relative_to(R)),'output':str(out.relative_to(R)),'sourceSHA256':hashlib.sha256(source.read_bytes()).hexdigest(),'priorSHA256':hashlib.sha256(prior.read_bytes()).hexdigest(),'outputSHA256':hashlib.sha256(out.read_bytes()).hexdigest(),'AO_cavity_fabric_exact':True,'panelCoveredFraction':float(panel.mean()),'status':'STAGED; actual production proof required'})
(W/'panel-mask-manifest.json').write_text(json.dumps(rows,indent=2)+'\n')
print(json.dumps(rows,indent=2))
