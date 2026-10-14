import sys, os, json
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import acc_common as A
import bag_pose as BP
sex = sys.argv[sys.argv.index('--')+1]
rig = BP.Rig(sex, f'GolfKitHero_{sex}')
sa_all = rig.g['a_sa']
def diag(clip, fi, thr=-0.004):
    body, bones = rig.hero.clip(clip)
    q, cc, tree = BP.frame_metrics(rig, body[fi].astype(np.float64), bones[fi].astype(np.float64))
    bad = [j for j in np.nonzero(cc < thr)[0] if rig.strap_v[rig.test[j]]]
    print(clip, fi, 'strap verts bad', len(bad))
    names = rig.hero.body.bones
    bp = A.unity_to_N(body[fi].astype(np.float64))
    out = {}
    for j in bad:
        i = rig.test[j]
        d = np.linalg.norm(bp - q[i], axis=1); v = np.argmin(d)
        k = np.argmax(rig.hero.body.bw[v]); bn = names[rig.hero.body.bi[v][k]]
        key = (int(sa_all[i] // 0.1), bn)
        out.setdefault(key, []).append(cc[j])
    for k, v in sorted(out.items()):
        print('   sa %.1f-%.1f m nearest %-14s count %3d worst %.1f mm' % (k[0]*0.1, k[0]*0.1+0.1, k[1], len(v), min(v)*1000))
for c, f in (('FistPump', 26), ('RunForward', 3), ('Wave', 6)):
    diag(c, f)
