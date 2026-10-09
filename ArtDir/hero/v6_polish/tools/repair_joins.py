"""Repair the V6 crown gap and duplicate scalp overlap without moving face vertices."""
import bpy,bmesh,json,hashlib,struct
from pathlib import Path
R=Path(__file__).resolve().parents[4];O=R/'ArtDir/hero/v6_polish'
bpy.ops.wm.open_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
b=bpy.data.objects['Body_Skin']
def fh():return hashlib.sha256(b''.join(struct.pack('fff',*v.co) for v in b.data.vertices if (b.matrix_world@v.co).z>1.2)).hexdigest()
report={'face_before':fh(),'hair_components':{}}
# The existing source skull already reaches the crown. The added generic dome made a second silhouette.
s=bpy.data.objects.get('Body_Scalp')
if s:bpy.data.objects.remove(s,do_unlink=True)
for m in b.modifiers:
 if m.type=='MASK':
  g=b.vertex_groups[m.vertex_group];indices=[v.index for v in b.data.vertices if (b.matrix_world@v.co).z>1.12];g.add(indices,0 if m.invert_vertex_group else 1,'REPLACE');report['head_vertices_unmasked']=len(indices)
mat=bpy.data.materials['Hero_V6_Scalp'];b.data.materials.append(mat);mi=len(b.data.materials)-1
for p in b.data.polygons:
 if b.data.materials[p.material_index].name in ['skin_CoveredFoundation','skin_MatteCloth'] and all((b.matrix_world@b.data.vertices[i].co).z>1.12 for i in p.vertices):p.material_index=mi
for name in ['Hair_Default','Hair_Default_Free']:
 o=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(o.data);unseen=set(bm.verts);stats=[]
 while unseen:
  seed=unseen.pop();comp={seed};stack=[seed]
  while stack:
   v=stack.pop()
   for e in v.link_edges:
    w=e.other_vert(v)
    if w in unseen:unseen.remove(w);comp.add(w);stack.append(w)
  low=min(v.co.z for v in comp);high=max(v.co.z for v in comp)
  # Five added arcs stand above the fitted core. Seat them 42mm into the crown.
  arc=low>1.58 and high>1.72
  if arc:
   for v in comp:v.co.z-=.042
  elif len(comp)>600:
   for v in comp:
    if v.co.z>1.6:v.co.z+=min(1,(v.co.z-1.6)/.10)*.012
  stats.append({'vertices':len(comp),'min_z':low,'max_z':high,'arc_seated':arc})
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();report['hair_components'][name]=stats
report['face_after']=fh();report['face_unchanged']=report['face_before']==report['face_after'];assert report['face_unchanged']
(O/'repair-joins.json').write_text(json.dumps(report,indent=2));bpy.ops.wm.save_as_mainfile(filepath=str(O/'Hero_V6_Polish.blend'))
