import sys, time
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np, bpy
import acc_common as A, acc_geo as G, acc_pipeline as PL, bag as B
t0 = time.time()
parts, S, infos = B.build_product('folded')
print('build', time.time()-t0)
def rule(ob):
    n = ob.name
    if n in ('Shell', 'Lining', 'Base'): return 'crown'
    if n.startswith(('Strap_', 'Handle', 'FrontPocket', 'LowerPocket', 'BackPocket', 'Hub', 'Zip', 'Head_')) and 'Buckle' not in n: return 'sheet'
    return 'smart'
for ob in parts:
    PL.unwrap_part(ob, rule(ob))
print('unwrap', time.time()-t0)
joined = G.join(parts, 'Kit_Bag')
PL.pack_joined(joined)
piece = PL.extract(joined)
print('pack', time.time()-t0, 'verts', len(piece['pos']), 'tris', len(piece['tri']), 'S', piece['S'], 'px/mm@2048', 2048/piece['S']/1000)
a3 = 0
P = piece['pos']; T = piece['tri']
area = np.linalg.norm(np.cross(P[T[:,1]]-P[T[:,0]], P[T[:,2]]-P[T[:,0]]), axis=1)*0.5
print('total area m2', area.sum())
for k in np.unique(piece['kind']):
    print(' kind', k, 'area %.3f' % area[piece['kind']==k].sum(), 'tris', (piece['kind']==k).sum())
uv = piece['uv']
print('uv bbox', uv.min(0), uv.max(0))
