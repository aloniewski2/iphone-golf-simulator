import json,numpy as np,math
D=json.load(open('markers_v2.json'))
def n(v):return v/np.linalg.norm(v,axis=-1,keepdims=True)
def socket(tilt=20):
 a=math.radians(tilt);y=np.array([0,math.sin(a),-math.cos(a)]);z=np.array([1,0,0]);x=np.cross(y,z);return np.stack([x,y,z],1)
S=socket()
for name,c in D.items():
 m={k:np.array(v) for k,v in c['markers'].items()};R=[np.array(r) for r in c['racket']]
 Wc=(m['RWRA']+m['RWRB'])/2;y=n(m['RFIN']-Wc);z=n(m['RWRB']-m['RWRA']);z=n(z-y*(z*y).sum(1,keepdims=True));x=np.cross(y,z)
 thumbdir=[];dots=[];rel=[]
 for i,r in enumerate(R):
  H=np.stack([x[i],y[i],z[i]],1)
  thumbdir.append(np.dot(r[:,1],-z[i]))  # racket head toward thumb side?
  dots.append(np.dot(r[:,2],x[i]))
  rel.append(H.T@r)
 rel=np.array(rel);dots=np.array(dots)
 # sign-fix by palm
 sg=np.sign(dots);rel2=np.array([r@np.diag([s,1,s]) for r,s in zip(rel,sg)])
 M=rel2.mean(0);U,_,Vt=np.linalg.svd(M);Mr=U@Vt
 ang=[math.degrees(math.acos(max(-1,min(1,(np.trace(Mr.T@r)-1)/2)))) for r in rel2]
 d=math.degrees(math.acos(max(-1,min(1,(np.trace(S.T@Mr)-1)/2))))
 print(f"{name:9s} head->thumb {np.mean(thumbdir):+.2f}  |face.palm| {np.mean(abs(dots)):.2f}  grip spread med {np.median(ang):.0f}deg  vs my socket {d:.0f}deg")
 print('   mean racket axes in hand frame: Y',Mr[:,1].round(2),' Z(normal)',Mr[:,2].round(2))
