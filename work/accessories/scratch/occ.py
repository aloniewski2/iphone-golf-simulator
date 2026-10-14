import sys, math
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A, bag_fit as BF, bag_place as BPL
from mathutils import Vector
sex = sys.argv[sys.argv.index('--')+1]
hd = A.HeroData(sex); tree0 = BF.body_tree(hd)
mot = BPL.Motion(sex, step=2)
print(sex, 'frames', len(mot.frames))
zs = [1.00, 1.05, 1.10, 1.15, 1.20, 1.25, 1.30]
ths = np.arange(0, 360, 20)
# axis centre per z: mean of torso-ish points |x|<0.1
P = hd.body_pos
rows = []
pts = []
for z in zs:
    m = (np.abs(P[:,2]-z) < 0.01) & (np.abs(P[:,0]) < 0.10)
    cy = 0.5*(P[m][:,1].min()+P[m][:,1].max())
    for th in ths:
        d = np.array([-math.sin(math.radians(th)), -math.cos(math.radians(th)), 0.0])   # th=0 front(-y); th=90 hero's right (-x)... sign: -sin -> -x for th=90
        o = np.array([0.0, cy, z])
        r = tree0.ray_cast(Vector(o), Vector(d))
        if r[0] is None: pts.append(np.array([np.nan]*3)); continue
        p = np.array(r[0]) + np.array(r[1]) * 0.004
        pts.append(p)
pts = np.array(pts)
ok = ~np.isnan(pts[:,0])
res = np.full(len(pts), np.nan)
pu = A.N_to_unity(np.nan_to_num(pts))
p4 = np.concatenate([pu, np.ones((len(pu),1))], 1)
worst = np.full(len(pts), 9.0)
for name, fi, tree, M in mot.frames:
    q = A.unity_to_N((p4 @ M.T)[:, :3])
    for i in np.nonzero(ok)[0]:
        loc, nrm, idx, dist = tree.find_nearest(Vector(q[i]))
        s = dist if np.dot(nrm, q[i]-np.array(loc)) >= 0 else -dist
        if s < worst[i]: worst[i] = s
worst[~ok] = np.nan
print('rows z, cols theta deg (0=front, 90=hero right, 180=back); min clearance mm (rigid UpperChest), offset 4 mm')
print('      ' + ' '.join('%4d' % t for t in ths))
for iz, z in enumerate(zs):
    print('z=%.2f ' % z + ' '.join(('%4.0f' % (worst[iz*len(ths)+it]*1000)) if not np.isnan(worst[iz*len(ths)+it]) else '   .' for it in range(len(ths))))
