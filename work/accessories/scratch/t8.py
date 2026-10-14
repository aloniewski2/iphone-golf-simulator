import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A, bag_pose as BP
sex = sys.argv[sys.argv.index('--')+1]
rig = BP.Rig(sex, f'GolfKitHero_{sex}')
sa = rig.g['a_sa']; P = rig.pos_N
c = rig.rest_clear
sv = np.nonzero(rig.strap_v)[0]
bad = sv[c[sv] < 0.006]
print('strap verts', len(sv), 'bad at rest', len(bad))
for lo in np.arange(0, 1.5, 0.1):
    m = (sa[bad] >= lo) & (sa[bad] < lo + 0.1)
    if m.any(): print('  sa %.1f: n=%d min %.1f mm  at z~%.3f x~%.3f y~%.3f' % (lo, m.sum(), c[bad][m].min()*1000, P[bad][m][:,2].mean(), P[bad][m][:,0].mean(), P[bad][m][:,1].mean()))
