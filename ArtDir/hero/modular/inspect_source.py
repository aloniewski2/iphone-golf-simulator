import bpy,bmesh,json
from pathlib import Path
P=Path(__file__).resolve().parent
bpy.ops.wm.open_mainfile(filepath=str(P.parent/'Hero_01.blend'))
out={}
for o in bpy.data.collections['Hero_01'].objects:
 if o.type!='MESH':continue
 bm=bmesh.new();bm.from_mesh(o.data);remain=set(bm.verts);comps=[]
 while remain:
  stack=[remain.pop()];vs=[]
  while stack:
   v=stack.pop();vs.append(v)
   for e in v.link_edges:
    u=e.other_vert(v)
    if u in remain:remain.remove(u);stack.append(u)
  comps.append({'verts':len(vs),'z':[min(v.co.z for v in vs),max(v.co.z for v in vs)]})
 out[o.name]={'components':sorted(comps,key=lambda x:-x['verts'])[:20],'boundaries':sum(e.is_boundary for e in bm.edges),'materials':{m.name:sum(f.material_index==i for f in o.data.polygons) for i,m in enumerate(o.data.materials)}};bm.free()
(P/'source-inspection.json').write_text(json.dumps(out,indent=2));print(json.dumps(out))
