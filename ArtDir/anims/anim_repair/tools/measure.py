import json,math,numpy as np
from pathlib import Path
P=Path(__file__).resolve().parents[1];d=json.loads((P/'gameplay_pose_bake.json').read_text());ns=d['bones']
def q(v):return np.array([v[k] for k in 'wxyz'])
def mul(a,b):
 w,x,y,z=a;v,i,j,k=b;return np.array([w*v-x*i-y*j-z*k,w*i+x*v+y*k-z*j,w*j-x*k+y*v+z*i,w*k+x*j-y*i+z*v])
def inv(a):return a*np.array([1,-1,-1,-1])/np.dot(a,a)
rest={n:q(v) for n,v in zip(ns,d['restRotations'])};report={}
for c in d['clips']:
 wrists=[];elbows=[];gaps=[]
 for f in c['frames']:
  ps={n:np.array([v[k] for k in 'xyz']) for n,v in zip(ns,f['positions'])};qs={n:q(v) for n,v in zip(ns,f['rotations'])};neutral=mul(mul(qs['LowerArm.R'],inv(rest['LowerArm.R'])),rest['Hand.R']);dot=abs(np.dot(neutral,qs['Hand.R']))/np.linalg.norm(neutral)/np.linalg.norm(qs['Hand.R']);wrists.append(math.degrees(2*math.acos(np.clip(dot,-1,1))))
  a=ps['UpperArm.R']-ps['LowerArm.R'];b=ps['Hand.R']-ps['LowerArm.R'];elbows.append(math.degrees(math.acos(np.clip(np.dot(a,b)/np.linalg.norm(a)/np.linalg.norm(b),-1,1))))
  a=ps['Hips']+np.array([0,.06,0]);b=ps['Chest']+np.array([0,.06,0]);ax=b-a;pt=ps['LowerArm.R'];t=np.clip(np.dot(pt-a,ax)/np.dot(ax,ax),0,1);gaps.append(np.linalg.norm(pt-a-t*ax)-.225)
 report[c['name']]={'right_wrist_deviation_max_deg':max(wrists),'right_elbow_interior_deg':[min(elbows),max(elbows)],'right_elbow_proxy_gap_min_m':min(gaps)}
(P/'acceptance_metrics.json').write_text(json.dumps(report,indent=2));print(json.dumps({n:report[n] for n in ['Ready','Backhand','Volley','RunForward']},indent=2))
