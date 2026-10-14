import sys, os
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A
for sex in ('Male','Female'):
    hd = A.HeroData(sex)
    P = hd.body_pos
    print(sex, 'verts', len(P), 'tris', len(hd.body_tri), 'zrange', P[:,2].min(), P[:,2].max())
    for n in ('Hips','Spine','Chest','UpperChest','Neck','Head','LeftShoulder','LeftUpperArm','RightShoulder','RightUpperArm','RightHand'):
        print('  ', n, np.round(hd.rest_pos[n],3))
    for z in np.arange(0.95, 1.50, 0.05):
        m = np.abs(P[:,2]-z) < 0.006
        # torso only: |x|<0.22 
        q = P[m]
        t = q[np.abs(q[:,0])<0.26]
        if len(t)==0: continue
        print('  z=%.2f  x[%.3f %.3f]  y[%.3f %.3f]  |x|<.26' % (z, t[:,0].min(), t[:,0].max(), t[:,1].min(), t[:,1].max()), ' all x[%.3f,%.3f]'%(q[:,0].min(), q[:,0].max()))
