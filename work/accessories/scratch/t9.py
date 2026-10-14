import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A, bag_fit as BF
for sex in ('Male', 'Female'):
    hd = A.HeroData(sex); tree = BF.body_tree(hd)
    print(sex)
    for y in (-0.03, 0.0, 0.03):
        for z in np.arange(1.04, 1.26, 0.02):
            o = np.array([0.0, y, z]); d = np.array([-1.0, 0, 0])
            r1 = tree.ray_cast(BF.Vector(o), BF.Vector(d))
            if r1[0] is None: print('  y=%.2f z=%.2f none' % (y, z)); continue
            p1 = np.array(r1[0]); r2 = tree.ray_cast(BF.Vector(p1 + d*1e-4), BF.Vector(d))
            s = ''
            if r2[0] is not None: s = 'next x=%.3f dot=%.2f gap=%.1f mm' % (r2[0][0], np.dot(np.array(r2[1]), d), abs(r2[0][0]-p1[0])*1000)
            print('  y=%+.2f z=%.2f torso x=%.3f %s' % (y, z, p1[0], s))
