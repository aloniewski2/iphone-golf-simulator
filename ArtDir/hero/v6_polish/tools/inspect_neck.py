import bpy,bmesh,json
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish';bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
o=bpy.data.objects['Body_Skin'];bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();unseen=set(bm.verts);parts=[]
while unseen:
 seed=unseen.pop();p={seed};s=[seed]
 while s:
  v=s.pop()
  for e in v.link_edges:
   w=e.other_vert(v)
   if w in unseen:unseen.remove(w);p.add(w);s.append(w)
 parts.append(p)
head=max((p for p in parts if max(v.co.z for v in p)>1.6),key=len);edges={e for v in head for e in v.link_edges if e.is_boundary};loops=[]
while edges:
 e=edges.pop();vv=set(e.verts);stack=list(e.verts);ee={e}
 while stack:
  v=stack.pop()
  for q in list(v.link_edges):
   if q in edges:edges.remove(q);ee.add(q);vv.update(q.verts);stack.extend(q.verts)
 loops.append({'vertices':len(vv),'edges':len(ee),'indices':sorted(v.index for v in vv),'bounds':[[min(v.co[i] for v in vv),max(v.co[i] for v in vv)] for i in range(3)]})
print(json.dumps(sorted(loops,key=lambda x:-x['vertices']),indent=2));(O/'neck-boundary-loops.json').write_text(json.dumps(sorted(loops,key=lambda x:-x['vertices']),indent=2))
