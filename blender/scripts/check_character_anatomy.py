"""Reject Higgsfield/Tripo character meshes with the wrong number of limbs.

Slices the mesh horizontally and counts separate islands: a standing biped with its arms down
shows 2 legs at shin height and 3 blobs (arm, torso, arm) at waist height. Multiview jobs fed
views in the wrong order fuse two silhouettes at 90 degrees into a four-legged figure, which
this catches before anything is rigged.

Run:  Blender --background --python blender/scripts/check_character_anatomy.py -- model.glb
"""
import sys

import bpy
from mathutils import Vector
path=sys.argv[sys.argv.index("--")+1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=path)
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
vs=[obj.matrix_world@v.co for v in obj.data.vertices]
lo=min(v.z for v in vs); hi=max(v.z for v in vs); H=hi-lo
def blobs(frac, band=.012, cell=.012):
    z=lo+frac*H
    cells={(round(v.x/cell),round(v.y/cell)) for v in vs if abs(v.z-z)<band*H}
    seen=set(); n=0; sizes=[]
    for c in cells:
        if c in seen: continue
        n+=1; stack=[c]; seen.add(c); k=0
        while stack:
            x,y=stack.pop(); k+=1
            for dx in (-1,0,1):
                for dy in (-1,0,1):
                    q=(x+dx,y+dy)
                    if q in cells and q not in seen: seen.add(q); stack.append(q)
        sizes.append(k)
    return n, sorted(sizes, reverse=True)
report={f:blobs(f) for f in (.12,.2,.28,.45,.5,.55)}
for f,(n,s) in report.items(): print(f"ANATOMY {f:.2f}H blobs={n} sizes={s[:6]}")
legs=report[.2][0]
print("ANATOMY VERDICT", "OK" if legs==2 else f"BROKEN: {legs} legs at shin height")
