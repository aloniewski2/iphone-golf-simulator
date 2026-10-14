"""Build garments for one hero. Blender -b --factory-startup --python build.py -- <hero> <garment,...>"""
import sys, os, time
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
import cv2io, glib as G
import importlib

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
hero_id = argv[0]
which = argv[1].split(',') if len(argv) > 1 else ['top']
hero = cv2io.Hero(hero_id)
t0 = time.time()
body = G.Body(hero)
print('body ready', round(time.time() - t0, 1), 's')
out = os.path.join(cv2io.ROOT, 'out', hero_id); os.makedirs(out, exist_ok=True)

import designs
D = designs.design(hero_id)
class Ctx: pass
ctx = Ctx(); ctx.surfaces = {}          # name -> (pos, tri) outer surfaces of pieces built so far
body.ctx = ctx
order = D.get('order', list(D))
if which == ['all']: which = order
for w in [o for o in order if o in which]:
    mod, fn = D[w]['builder'].split('.')
    m = importlib.import_module(mod)
    t1 = time.time()
    res = getattr(m, fn)(body, D[w])
    for piece, g in res.items():
        if not isinstance(g, dict):
            outer = g.tri[g.F['part'] == 0]
            np.savez(os.path.join(out, 'surf_' + piece + '.npz'), pos=g.pos, tri=outer)
            g = g.export()
        if 'col' not in g:
            col = np.ones((len(g['pos']), 3))
            if 'a_trim' in g: col *= (1 - .85 * g['a_trim'][:, None])
            if 'a_cavity' in g: col *= g['a_cavity'][:, None]
            g['col'] = col
        np.savez(os.path.join(out, piece + '.npz'), **g)
        print(f'{piece}: v={len(g["pos"])} tris={len(g["tri"])}  ({time.time() - t1:.1f}s)')
