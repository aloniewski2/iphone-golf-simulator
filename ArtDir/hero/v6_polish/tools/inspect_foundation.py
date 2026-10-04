import bpy,bmesh,json,collections
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
report={}
for name in ['Body_Skin','Body_Legs','Body_Legs_Full','Shirt_Default','Shorts_Default','Shoes_Default']:
 o=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
 points=collections.Counter(tuple(round(x,6) for x in v.co) for v in bm.verts)
 orig=len(bm.verts);bound=sum(e.is_boundary for e in bm.edges)
 seams={}
 for lo,hi,label in [(1.1,1.3,'neck'),(1.3,1.7,'head'),(.35,.5,'knee'),(.63,.72,'pelvis')]:
  ps=[o.matrix_world@v.co for v in bm.verts if v.is_boundary and lo< (o.matrix_world@v.co).z<hi]
  seams[label]={'vertices':len(ps),'bounds':[[min(p[i] for p in ps),max(p[i] for p in ps)] for i in range(3)] if ps else []}
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
 unseen=set(bm.verts);comps=[]
 while unseen:
  v=unseen.pop();part={v};stack=[v]
  while stack:
   v=stack.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in unseen:unseen.remove(w);part.add(w);stack.append(w)
  if len(part)>10:comps.append({'vertices':len(part),'bounds':[[min((o.matrix_world@v.co)[i] for v in part),max((o.matrix_world@v.co)[i] for v in part)] for i in range(3)]})
 report[name]={'original_vertices':orig,'duplicate_positions':sum(c-1 for c in points.values()),'original_boundary_edges':bound,'welded_vertices':len(bm.verts),'welded_boundary_edges':sum(e.is_boundary for e in bm.edges),'seams':seams,'components':sorted(comps,key=lambda v:-v['vertices'])[:15]};bm.free()
(O/'foundation-inspection.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
