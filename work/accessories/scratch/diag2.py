import sys, json
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A, bag_pose as BP
a = A.args_after_dashes(); sex, clip = a[0], a[1]
rig = BP.Rig(sex, f'GolfKitHero_{sex}')
pj = json.load(open(f'/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/out/{sex}/golf_bag/pose_check.json'))
pf = pj[f'GolfKitHero_{sex}:{clip}']['per_frame']
fi = int(np.argmin([b for b, s in pf]))
body, bones = rig.hero.clip(clip)
q, cc, tree = BP.frame_metrics(rig, body[fi].astype(np.float64), bones[fi].astype(np.float64))
names = {v: k for k, v in __import__('bag').K.items()}
print(clip, 'worst bag frame', fi, 'min', cc.min())
bad = np.nonzero(cc < -0.01)[0]
from collections import Counter
cnt = Counter()
for j in bad:
    i = rig.test[j]; kind = int(rig.kind[np.nonzero((rig.tri == i).any(1))[0][0]]); cnt[names.get(kind, kind)] += 1
print(cnt)
print('frames with bag < 0 :', [i for i, (b, s) in enumerate(pf) if b < 0])
