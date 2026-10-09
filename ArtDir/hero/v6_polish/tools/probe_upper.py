import bpy,bmesh,json
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish'
bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
rig=bpy.data.objects['Hero_01_Rig'];out={'bones':{},'objects':{}}
for b in rig.data.bones:
 if b.name.startswith(('UpperArm','LowerArm','Hand','Shoulder','Chest','Spine','Neck','Head','Hips','UpperLeg','LowerLeg','Foot')):
  out['bones'][b.name]={'head':list(b.head_local),'tail':list(b.tail_local),'parent':b.parent.name if b.parent else None}
for n in ['Body_Skin','Shirt_Default','Body_Legs','Body_Legs_Full']:
 o=bpy.data.objects[n];bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();dl=bm.verts.layers.deform.active
 parts=[];unseen=set(bm.verts)
 while unseen:
  v=unseen.pop();p={v};st=[v]
  while st:
   for e in st.pop().link_edges:
    v=e.other_vert(st[-1]) if False else None
   # Independent flood below avoids preserving an arbitrary edge traversal order.
  todo=list(p)
  while todo:
   v=todo.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in unseen:unseen.remove(w);p.add(w);todo.append(w)
  if len(p)>10:parts.append(p)
 report={'matrix':list(map(list,o.matrix_world)),'modifiers':[(m.name,m.type) for m in o.modifiers],'materials':[m.name for m in o.data.materials],'components':[]}
 for p in sorted(parts,key=lambda x:-len(x)):
  fs={f for v in p for f in v.link_faces};be={e for v in p for e in v.link_edges if e.is_boundary}
  report['components'].append({'vertices':len(p),'bounds':[[min(v.co[i] for v in p),max(v.co[i] for v in p)] for i in range(3)],'boundary_edges':len(be),'material_counts':{m.name:sum(f.material_index==i for f in fs) for i,m in enumerate(o.data.materials)}})
 out['objects'][n]=report;bm.free()
(O/'upper-probe.json').write_text(json.dumps(out,indent=2));print(json.dumps(out,indent=2))
