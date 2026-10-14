import sys, time, math
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_fit as F, bag as B
parts, S, infos = B.build_product('folded', straps=(), clubs=False)
P = np.concatenate([G.positions(p) for p in parts])
print('bag verts', len(P))
rng = np.random.default_rng(1); sub = P[rng.choice(len(P), 2500, replace=False)]
def Rx(d): return F.Rx(d)
def Rz(d): return F.Rz(d)
def place(P, lean, t, p0=np.array([0, S.YB, 0.45])):
    R = Rx(-lean) @ Rz(180)
    return (P - p0) @ R.T + np.asarray(t)
for sex in ('Male', 'Female'):
    hd = A.HeroData(sex)
    clear = F.Clearance(hd.body_pos, hd.body_tri)
    head = F.region(hd, hd.rest_pos['Head'][2] - 0.10)
    ch = F.Clearance(*head)
    print(sex)
    for lean in (8, 10, 12):
        for rim_target in (1.40, 1.45, 1.50):
            # world z of the anchor: rim top (z=0.783 product) maps to z = tz + (0.783-0.45)*cos(lean)
            tz = rim_target - (0.783 - 0.45) * math.cos(math.radians(lean))
            lo, hi = 0.02, 0.40
            for _ in range(14):
                ty = 0.5 * (lo + hi)
                W = place(sub, lean, (0.012, ty, tz))
                c = clear(W)
                if c.min() >= 0.012: hi = ty
                else: lo = ty
            ty = hi
            W = place(sub, lean, (0.012, ty, tz))
            c = clear(W); cH = ch(W)
            topz = place(np.array([[0, S.YB, 0.783]]), lean, (0.012, ty, tz))[0]
            botz = place(np.array([[0, S.YB, 0.0]]), lean, (0.012, ty, tz))[0]
            print('  lean %2d rim %.2f -> ty=%.3f  minclear %.4f  head min %.3f  top(y=%.3f,z=%.3f) bottom(y=%.3f,z=%.3f)' % (lean, rim_target, ty, c.min(), cH.min(), topz[1], topz[2], botz[1], botz[2]))
