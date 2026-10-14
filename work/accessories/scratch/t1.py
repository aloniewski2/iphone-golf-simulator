import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np, bpy
import acc_common as A, acc_geo as G, bag as B, bag_geo as BG, bag_view as V
A.reset()
S = B.Shell()
print('perimeter', S.L)
parts = []
sh, phis = B.build_shell(S); parts.append(sh)
li, fl = B.build_lining(S); parts += [li, fl]
parts += B.build_base(S)
parts.append(B.build_rim(S))
parts += B.build_pipings(S)
parts += B.build_dividers(S)
for p in parts: print(p.name, G.mesh_stats(p))
V.flat_materials(parts)
V.views_render('/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/scratch', 't1', {'front': (0, 8), 'side': (90, 8), '34': (38, 22), 'top': (0, 75)})
