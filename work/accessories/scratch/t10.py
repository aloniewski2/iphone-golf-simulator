import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A, bag_pose as BP
sex = sys.argv[sys.argv.index('--')+1]; clip = sys.argv[sys.argv.index('--')+2]; fi = int(sys.argv[sys.argv.index('--')+3])
rig = BP.Rig(sex, f'GolfKitHero_{sex}')
body, bones = rig.hero.clip(clip)
q, cc, tree = BP.frame_metrics(rig, body[fi].astype(np.float64), bones[fi].astype(np.float64))
sa = rig.g['a_sa']
bad = [j for j in np.nonzero(cc < -0.03)[0] if rig.strap_v[rig.test[j]]]
print('n bad (<-30mm)', len(bad))
names = ['Back_Bag'] + rig.allowed
for j in bad[:12]:
    i = rig.test[j]
    w = {names[rig.bi[i][k]]: round(rig.bw[i][k], 2) for k in range(4) if rig.bw[i][k] > 0}
    print('  sa %.3f  clr %.1f mm  rest %s  now %s  w=%s' % (sa[i], cc[j]*1000, np.round(rig.pos_N[i], 3), np.round(q[i], 3), w))
