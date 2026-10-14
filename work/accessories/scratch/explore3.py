import sys
sys.path.insert(0, 'py')
import numpy as np
import acc_common as A
for sex in ('Male','Female'):
    hd = A.HeroData(sex); P = hd.body_pos
    print(sex)
    for z in np.arange(0.60, 1.50, 0.05):
        m = (np.abs(P[:,2]-z)<0.01)
        q = P[m]
        # torso: exclude arms: |x| < half width guess  (use points whose x within 0.20)
        t = q[np.abs(q[:,0])<0.17]
        t2 = q[np.abs(q[:,0])<0.10]
        print('  z=%.2f  back y max(|x|<.17)=%.3f (|x|<.10)=%.3f  front ymin=%.3f  xmax torso(|x|<.30)=%.3f' % (z, t[:,1].max(), t2[:,1].max(), t[:,1].min(), np.abs(q[np.abs(q[:,0])<0.30][:,0]).max()))
