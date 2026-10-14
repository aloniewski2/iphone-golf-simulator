import sys
sys.path.insert(0, '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator/work/accessories/py')
import numpy as np, bpy
import acc_common as A, acc_geo as G, bag as B
parts, S, infos = B.build_product('folded')
rows = []
for p in parts: rows.append((p.name, G.mesh_stats(p)['tris']))
rows.sort(key=lambda r: -r[1])
for r in rows[:30]: print(r)
print(sum(r[1] for r in rows))
