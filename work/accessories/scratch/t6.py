import sys, os, json
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A
import bag_pose as BP
from mathutils import Vector
sex = 'Male'
rig = BP.Rig(sex, 'GolfKitHero_Male')
def diag(clip, fi, thr=-0.006):
    body, bones = rig.hero.clip(clip)
    q, cc, tree = BP.frame_metrics(rig, body[fi].astype(np.float64), bones[fi].astype(np.float64))
    bad = np.nonzero(cc < thr)[0]
    print(clip, fi, 'bad', len(bad), 'of', len(cc))
    if not len(bad): return
    names = rig.hero.body.bones
    out = {}
    for j in bad:
        i = rig.test[j]
        kind = int(rig.kind[np.nonzero((rig.tri == i).any(1))[0][0]])
        # nearest posed body vertex -> dominant bone (hero body)
        bp = A.unity_to_N(body[fi].astype(np.float64))
        d = np.linalg.norm(bp - q[i], axis=1); v = np.argmin(d)
        k = np.argmax(rig.hero.body.bw[v]); bn = names[rig.hero.body.bi[v][k]]
        key = (kind, bn)
        out.setdefault(key, []).append(cc[j])
    for k, v in sorted(out.items(), key=lambda kv: min(kv[1])):
        print('   kind', k[0], 'nearest bone', k[1], 'count', len(v), 'worst %.1f mm' % (min(v) * 1000))
diag('Idle', 5)
diag('Walk', 2)
diag('Walk', 12)
