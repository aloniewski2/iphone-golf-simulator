import bpy, sys, importlib.util
from pathlib import Path
from mathutils import Matrix
T = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('th', T / 'tripo_hair.py'); th = importlib.util.module_from_spec(spec); spec.loader.exec_module(th)
bpy.ops.wm.read_factory_settings(use_empty=True)
glb = next((T.parent / 'v5_tripo/hc/tripo-out').glob('*/model.glb'))
bpy.ops.import_scene.gltf(filepath=str(glb))
src = max((o for o in bpy.data.objects if o.type == 'MESH'), key=lambda o: len(o.data.polygons))
src.data.transform(src.matrix_world); src.matrix_world = Matrix.Identity(4)
cls = th.classify(src); vs = [v.co for v in src.data.vertices]
from collections import defaultdict
sl = defaultdict(lambda: defaultdict(list))
for p, c in zip(src.data.polygons, cls):
    for i in p.vertices: sl[round(vs[i].z / .04) * .04][c].append(vs[i])
for z in sorted(sl, reverse=True):
    row = []
    for c in ('hair', 'skin', 'white', 'orange', 'other'):
        pts = sl[z][c]
        if pts: row.append(f"{c}:x[{min(p.x for p in pts):+.2f},{max(p.x for p in pts):+.2f}] y[{min(p.y for p in pts):+.2f},{max(p.y for p in pts):+.2f}]")
    print(f"Z {z:+.2f} " + '  '.join(row))
