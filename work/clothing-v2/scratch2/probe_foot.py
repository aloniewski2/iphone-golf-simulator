import sys; sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/clothing-v2/py')
import numpy as np, cv2io, glib as G, shoe2
for hid in cv2io.HEROES:
    hero = cv2io.Hero(hid); body = G.Body(hero); fr = shoe2.Frame(body, 'R'); L = fr.L
    ys = np.linspace(0, L, 12); xc, w = shoe2.footprint(fr, ys); zt = shoe2.foot_top(fr, ys, xc)
    print(hid, 'L %.3f' % L, 'ankle z %.3f' % fr.ankle[2], 'ankle y/L %.2f' % (((fr.ankle - fr.origin) @ fr.fwd) / L), flush=True)
    print('  y/L  ', np.round(ys / L, 2)); print('  top z', np.round(zt, 3)); print('  halfw ', np.round(w, 3))
    q = fr.to(body.pos[(fr.wf + fr.wl) > .3])
    for z in (.05, .07, .09, .11, .15, .25):
        m = (q[:, 2] > z - .005) & (q[:, 2] < z + .005)
        if m.sum(): print('  leg slice z=%.2f: y %.3f..%.3f  x %.3f..%.3f' % (z, q[m, 1].min(), q[m, 1].max(), q[m, 0].min(), q[m, 0].max()), flush=True)
