import bpy,json
from mathutils import Vector
from pathlib import Path
P=Path(__file__).resolve().parents[1];bpy.ops.wm.open_mainfile(filepath=str(P.parent/'knees_offarm/Hero_01_Knees_QA.blend'))
r=bpy.data.objects['Hero_01_Rig'];o=bpy.data.objects['Body_Skin'];r.animation_data_clear()
for b in r.pose.bones:b.matrix_basis.identity()
vs=[]
b=r.data.bones['Hand.R'];inv=b.matrix_local.inverted()
for v in o.data.vertices:
 if any(g.group==o.vertex_groups['Hand.R'].index and g.weight>.1 for g in v.groups):vs.append({'i':v.index,'p':list(inv@v.co)})
(P/'hand_vertices.json').write_text(json.dumps({'verts':vs,'polys':[[v for v in f.vertices] for f in o.data.polygons],'modifiers':[(m.name,m.type) for m in o.modifiers]}))
print('HAND',len(vs),[(m.name,m.type) for m in o.modifiers])
