import sys, json, math
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np
import bag_fit as BF, bag_strap_eval as SE, acc_common as A
sex = A.args_after_dashes()[0]
BF.CFG['web_clear'] = 0.012
ev = SE.StrapEval(sex); st = SE.build_strap(sex, ev)
pos, sa = st['pos'], st['sa']
# per clip: the vertices below -6 mm at the worst frame, listed by (z, theta)
P = ev.hd.body_pos
for clip in ('RunForward', 'FistPump'):
    r = ev.evaluate(pos, st['bi'], st['bw'])[clip]
    fr = r['frame']
    # recompute that frame's per-vertex clearance
    from mathutils import Vector
    name, fi, tree, sk, bp = next(f for f in ev.frames if f[0] == clip and f[1] == fr)
    q = A.unity_to_N(SE.cv2io.lbs(A.N_to_mesh(pos), st['bi'], st['bw'].astype(np.float64), sk))
    bad = []
    for j, p in enumerate(q):
        loc, nrm, idx, d = tree.find_nearest(Vector(p))
        s = d if np.dot(nrm, p - np.array(loc)) >= 0 else -d
        if s < -0.006: bad.append((sa[j], s))
    bad = np.array(bad)
    print(clip, 'frame', fr, 'bad verts', len(bad), 'sa range %.2f-%.2f' % (bad[:,0].min(), bad[:,0].max()) if len(bad) else '')
    # where those sa are in rest space
    for lo in np.arange(0, 1.4, 0.05):
        m = (sa >= lo) & (sa < lo + 0.05)
        b = bad[(bad[:,0] >= lo) & (bad[:,0] < lo + 0.05)] if len(bad) else []
        if len(b):
            c = pos[m].mean(0)
            th = math.degrees(math.atan2(-c[0], -(c[1] - 0.0)))
            print('   sa %.2f-%.2f: n=%d worst %.1f mm; rest centre z=%.3f x=%.3f y=%.3f (theta~%.0f)' % (lo, lo+0.05, len(b), b[:,1].min()*1000, c[2], c[0], c[1], th))
